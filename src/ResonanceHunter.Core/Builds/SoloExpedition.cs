using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Combat;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Expeditions;

namespace ResonanceHunter.Core.Builds;

/// <summary>
/// A whole run for ONE character: push wave after wave, paying out each cleared wave at once, until it dies.
/// </summary>
/// <remarks>
/// <para>
/// This wraps <see cref="SoloBattle"/> across waves. It replaced a squad sibling that wrapped a
/// multi-creature auto-battler the same way; that engine is gone, and this is the one loop the game runs.
/// </para>
/// <para>
/// It owns almost no logic of its own. The <see cref="Champion"/> carries every scrap of cross-wave
/// state — health (attrition), skill cooldowns, spent UNDYING, elapsed time — so a wave is just
/// <see cref="SoloBattle.ResolveWave"/> handed the same champion again. All this class adds is the
/// enemy's per-wave scaling and the haul.
/// </para>
/// <para>
/// Threat compounds (<see cref="WaveScaling.EnemyScale"/>) while haul grows linearly
/// (<see cref="WaveScaling.HaulScale"/>), so a fixed build always eventually falls behind: there is a wave
/// it cannot hold, and the run ends there. There is no priced exit any more — the idle loop banks every
/// wave the instant it clears (see the note above <see cref="PushWave"/> and at the end of this file), so
/// nothing waits to be lost. The depth the build can reach is the only thing that gates the haul.
/// </para>
/// </remarks>
public sealed class SoloExpedition
{
    private readonly Build _build;
    private readonly Champion _champion;
    private readonly Hunter _hunter;
    private readonly ExpeditionTuning _tuning;
    private readonly float _enemyBaseHealth;
    private readonly float _enemyBaseDamage;
    private readonly Source? _enemySource;
    private readonly Random _rng;

    /// <summary>The balanced enemy bite cadence, in ms. A region's bias bends this — see <see cref="BiasTempo"/>.</summary>
    private const int EnemyIntervalMs = 1500;

    /// <summary>
    /// The region's combat character. It gives each region its OWN feel instead of all six fighting alike.
    /// </summary>
    /// <remarks>
    /// <see cref="AttackBias"/> was set on every region and advertised in the docs ("a Machine region grinds
    /// you down with slow, heavy blows; a Shadow region harries you with fast ones") but reached NO formula —
    /// the champion always ate a flat 1500ms bite. Now it bends the enemy's TEMPO: HEAVY bites slower and
    /// harder (spikes that punish a low health pool), FAST bites quicker and lighter (a flurry — which a TRAP
    /// build, firing on being hit, answers far more often). The total damage stays roughly even, so the bias
    /// is FEEL and build-synergy, not a difficulty knob — depth and the region ladder own difficulty.
    /// </remarks>
    public AttackBias EnemyBias { get; set; } = AttackBias.Balanced;

    /// <summary>
    /// Which region's band cycle this run walks. Empty falls back to the baseline cycle.
    /// </summary>
    /// <remarks>
    /// The cycle decides what each wave IS — which archetypes turn up and which affix holds — so this is
    /// the single hook that turns six identically-shaped regions into six places with reputations.
    /// </remarks>
    public string RegionId { get; set; } = string.Empty;

    /// <summary>
    /// Distinguishes one descent from another when seeding a wave's composition.
    /// </summary>
    /// <remarks>
    /// Composition is a pure function of (region, wave, runIndex), never of wall time, so replaying a
    /// wave produces the same creatures. Fast-forward pays the haul a wave originally paid, and a
    /// re-rolled composition would let a player bank a wave they never actually proved.
    /// </remarks>
    public int RunIndex { get; set; }

    /// <summary>The Form that dealt the most damage last wave — what WARDED reads.</summary>
    /// <remarks>
    /// It deliberately lags by a wave. An affix that reacted to the wave in progress would be
    /// unanswerable in a game with no in-run decisions; lagging lets the player see it coming.
    /// </remarks>
    public Form? LastWaveTopForm { get; private set; }

    /// <summary>The composition the last resolved wave held — the report reads this.</summary>
    public IReadOnlyList<WaveCreature> LastWaveCreatures { get; private set; } = Array.Empty<WaveCreature>();

    /// <summary>The archetype and affixes the last resolved wave carried.</summary>
    public Archetype LastWaveArchetype { get; private set; }
    public IReadOnlyList<Affix> LastWaveAffixes { get; private set; } = Array.Empty<Affix>();

    private (int IntervalMs, float DamageMult) BiasTempo() => EnemyBias switch
    {
        AttackBias.Heavy => (2200, 2200f / EnemyIntervalMs),   // slow + heavy: fewer, bigger, spikier bites
        AttackBias.Fast => (1000, 1000f / EnemyIntervalMs),    // fast + light: a flurry TRAP loves
        _ => (EnemyIntervalMs, 1f),
    };

    /// <summary>
    /// DESPERATION's haul kick when the character is near death.
    /// </summary>
    /// <remarks>
    /// The vision's "richer haul at low health", verbatim — it argues directly with bank-or-push,
    /// because the wave about to kill you is the wave that pays best. The squad read this magnitude off
    /// the enchantment; the solo model routes every behaviour through <see cref="Build.Triggers"/>, which
    /// is a set, so the kick is a fixed bonus rather than a summed magnitude.
    /// </remarks>
    private const float DesperationHaulBonus = 0.5f;

    public SoloExpedition(
        Build build,
        Champion champion,
        Hunter hunter,
        float enemyBaseHealth,
        float enemyBaseDamage,
        ExpeditionTuning? tuning = null,
        Source? enemySource = null,
        Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(champion);
        ArgumentNullException.ThrowIfNull(hunter);

        _build = build;
        _champion = champion;
        _hunter = hunter;
        _tuning = tuning ?? ExpeditionTuning.Default;
        _enemyBaseHealth = enemyBaseHealth;
        _enemyBaseDamage = enemyBaseDamage;
        _enemySource = enemySource;
        _rng = rng ?? new Random(20260716);
    }

    public Champion Champion => _champion;
    public int Wave { get; private set; }
    public bool Over { get; private set; }
    public Haul Carried { get; private set; }
    public IReadOnlyList<BattleEvent> LastWaveEvents { get; private set; } = Array.Empty<BattleEvent>();

    /// <summary>
    /// What the wave JUST cleared was worth, on its own. Zero if the last push did not clear.
    /// </summary>
    /// <remarks>
    /// The idle loop pays out PER WAVE, the instant it clears — there is no banking any more. So the
    /// caller reads this after each <see cref="PushWave"/> and credits it immediately, rather than reading
    /// the accumulated <see cref="Carried"/> once at the end of a run that no longer ends on the player's
    /// say-so.
    /// </remarks>
    public Haul LastWaveHaul { get; private set; }

    /// <summary>Whether the wave just cleared was a boss — the loot beat (see WaveScaling.IsBossWave).</summary>
    public bool LastWaveWasBoss { get; private set; }

    /// <summary>Push one wave deeper. In the idle loop this is called on a timer, not a button.</summary>
    public WaveOutcome PushWave()
    {
        LastWaveHaul = default;
        LastWaveWasBoss = false;
        if (Over) return WaveOutcome.Wiped;

        var next = Wave + 1;
        var scale = WaveScaling.EnemyScale(next, _tuning);
        var bonus = new WaveBonus();
        var (interval, dmgMult) = BiasTempo();   // the region's combat character bends the enemy's tempo

        // Health takes the boss spike; damage does not. They used to be the same number — see
        // WaveScaling.EnemyDamageScale.
        var damageScale = WaveScaling.EnemyDamageScale(next, _tuning);
        var isBoss = WaveScaling.IsBossWave(next, _tuning);

        // ── WHAT THIS WAVE IS ────────────────────────────────────────────────────────────────────
        //
        // The band decides the archetype and the affix; the affix bends the numbers; the archetype
        // decides how many creatures carry them. Seeded from (region, wave, run) and never from wall
        // time, so a replayed wave is the same wave.
        var cycle = BandCycles.For(RegionId);
        var band = cycle[Bands.CycleIndex(next, cycle.Count)];
        var affixes = Bands.AffixesOf(band).ToList();
        var wavesIntoBand = (next - 1) % Bands.WavesPerBand;
        var repeat = Bands.RepeatScale(next, cycle.Count);

        var compRng = new Random(HashCode.Combine(RegionId, next, RunIndex));
        var archetype = Bands.Roll(band, compRng);

        var health = _enemyBaseHealth * scale * repeat * Bands.HealthMultiplier(affixes);
        var damage = _enemyBaseDamage * damageScale * dmgMult * repeat
                     * Bands.DamageMultiplier(affixes, wavesIntoBand);

        var creatures = Archetypes.Compose(
            archetype, health, damage, next, BandCycles.RosterFor(RegionId), compRng, forceSingle: isBoss);

        // PLATED thickens whatever armour the composition brought. On a Swarm band that is zero, and the
        // affix is wasted — which is allowed. Re-rolling affixes to avoid it would cost the player the
        // one thing this system gives them: a ladder they can learn before they descend.
        var defMult = Bands.DefenceMultiplierOrOne(affixes);
        if (defMult != 1f)
            creatures = creatures
                .Select(c => new WaveCreature
                {
                    MaxHealth = c.MaxHealth, Health = c.Health, Damage = c.Damage,
                    Defense = c.Defense * defMult, Source = c.Source,
                })
                .ToList();

        // NUMBERS adds a creature — but never to a BOSS. A boss is one creature by definition; its band
        // supplies the stat shape only. Letting the affix add a second would turn the run's heartbeat
        // into a different encounter every fifth wave depending on which band it landed in.
        var extra = isBoss ? 0 : Bands.ExtraCreatures(affixes);
        for (var i = 0; i < extra; i++)
            creatures.Add(new WaveCreature
            {
                MaxHealth = creatures[0].MaxHealth, Health = creatures[0].MaxHealth,
                Damage = creatures[0].Damage, Defense = creatures[0].Defense,
                Source = creatures[0].Source,
            });

        var biteInterval = Math.Max(100, (int)(interval * Bands.IntervalMultiplier(affixes)));

        var (outcome, events) = SoloBattle.ResolveWave(
            _champion, _build, _hunter, creatures, biteInterval, _tuning, _rng, bonus, isBoss);

        LastWaveEvents = events;
        LastWaveCreatures = creatures;
        LastWaveArchetype = archetype;
        LastWaveAffixes = affixes;

        if (outcome != WaveOutcome.Cleared)
        {
            Over = true;
            return outcome;
        }

        Wave = next;
        LastWaveHaul = HaulForWave(next, bonus);
        LastWaveWasBoss = WaveScaling.IsBossWave(next, _tuning);
        Carried = Carried.Plus(LastWaveHaul);
        return outcome;
    }

    /// <summary>
    /// What a cleared wave is worth.
    /// </summary>
    /// <remarks>
    /// Simpler than the squad's, because there are no economy ROLES to weigh — a solo character has no
    /// Crafter to raise rarity or Producer to raise volume. Instead the build itself carries those knobs:
    /// <see cref="BuildMods.Haul"/> is the volume multiplier (gear, stats, keystones) and
    /// <see cref="BuildMods.Rarity"/> is the quality that tilts the loot roll. So the composition puzzle
    /// moved from "which five creatures" to "which keystones and gear", and this reads it off the build.
    /// </remarks>
    private Haul HaulForWave(int wave, WaveBonus bonus)
    {
        var scale = WaveScaling.HaulScale(wave, _tuning);
        var mods = _build.Resolve(_hunter);

        var haul = MathF.Max(0.05f, mods.Haul);
        if (_build.Triggers(_hunter).Contains(BuildTrigger.Desperation)
            && _champion.Health <= _champion.MaxHealth / 3)
        {
            // Read the WORN magnitude so a rarer Desperation charm pays more and its "HAUL +N%" blurb is
            // honest (it read a rarity-scaled % but the kick was a flat +50%). A mastery-notable source, with
            // no worn enchant, keeps the base bonus.
            var despMag = Economy.Enchantments.MagnitudeOf(_hunter.WornEnchantments, Economy.EnchantKind.Desperation);
            haul *= 1f + (despMag > 0f ? despMag : DesperationHaulBonus);
        }

        // Cores (which become hatchlings) drop only on BOSS waves — playtest: "eggs pour in constantly".
        // Paying a core every single wave, with wave count compounding the scale, buried the Warren in
        // creatures the player had no use for. A boss every fifth wave is a trickle, not a flood; the
        // idle GLEAM faucet (below) stays per-wave, because gleam feeds COMMAND and never piles up unused.
        var boss = WaveScaling.IsBossWave(wave, _tuning);
        var cores = boss ? (int)MathF.Round(2f * haul) : 0;

        return new Haul(
            Cores: cores + bonus.Cores,                                   // HARVEST adds cores
            Gleam: (int)MathF.Round(8f * scale * haul),
            Quality: MathF.Max(0.05f, mods.Rarity) + bonus.Quality);      // SPLINTER adds quality
    }

    // NO Bank / Retreat / Wipe payout, and no ExitShare. The idle loop pays every wave the instant it
    // clears (see LastWaveHaul), so nothing is ever "at risk" waiting to be banked — a wipe simply ends
    // this attempt, and the caller starts a fresh one. Removing the priced exit is the whole point of the
    // idle turn: the player stops being asked "bank or push?" after every fight and just watches, and the
    // depth their build can reach becomes the only thing that gates the haul.
}
