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
    private Build _build;
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

    /// <summary>
    /// Per-wave measurements for this descent, which the post-run report reads.
    /// </summary>
    /// <remarks>
    /// The run has no decisions in it, so the report afterwards is the only place a player can learn
    /// anything. That makes recording these a requirement of the design rather than telemetry.
    /// </remarks>
    public RunRecorder Recorder { get; } = new();

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
    /// <summary>Build the report for this descent. Safe to call at any time; most useful once over.</summary>
    public RunReport Report(bool isRecord)
        => Recorder.Build(
            RegionId, Wave, isRecord, LastOutcome,
            wallWave: Math.Max(1, Wave + 1),
            LastWaveArchetype, LastWaveAffixes, LastWaveCreatures.Count,
            _champion.MaxHealth);

    /// <summary>The outcome of the most recent wave — what the report calls the run's ending.</summary>
    public WaveOutcome LastOutcome { get; private set; } = WaveOutcome.Cleared;


    /// <summary>
    /// Swap the build the NEXT waves are resolved with, keeping everything else about the run — the
    /// wave count, the carried haul, the recorder, the band cycle, the RNG stream.
    /// </summary>
    /// <remarks>
    /// Playtest 2026-08-23: "skillerimi değiştiriyorum, UI'de değişmiş görünüyor ama aktif HUNT'ta
    /// değişmiyor." The run composed its build once at StartRun and never looked again, so a weave,
    /// a socket, a mastery or dust buy reached the fight only after a death or a region change. The
    /// hunt screen now re-composes at each wave boundary when any of those changed and hands it here.
    /// The champion's health POOL is minted at run start and stays (MaxHealth is init-only): the new
    /// build's health multipliers apply from the next descent, its damage and skills from the next wave.
    /// </remarks>
    public void ReplaceBuild(Build build)
    {
        ArgumentNullException.ThrowIfNull(build);
        // The champion's cooldown table is keyed by SLOT INDEX. A slot that now holds a different skill
        // must not inherit the old skill's ready-at (a swapped-in Aura would wait out a Strike's long
        // cooldown, or fire early); unchanged slots keep their legitimate carried cooldowns. Never
        // Clear() — that would charge every unchanged skill a full cooldown for a swap it was not part of.
        var old = _build.Skills;
        var now = build.Skills;
        for (var i = 0; i < Math.Max(old.Count, now.Count); i++)
            if (i >= old.Count || i >= now.Count
                || old[i].Ability.Form != now[i].Ability.Form
                || old[i].Ability.Source != now[i].Ability.Source)
                _champion.ReadyAt.Remove(i);
        _build = build;
    }
    /// <summary>
    /// Begin this descent after <paramref name="wave"/> — the next push resolves wave + 1. Only before
    /// the first push; a checkpoint start (Checkpoints) skips the waves below it and pays no haul for them.
    /// </summary>
    public void StartAtWave(int wave)
    {
        if (Wave != 0 || Over) throw new InvalidOperationException("A descent can only start at a checkpoint before its first wave.");
        Wave = Math.Max(0, wave);
    }

    /// <summary>
    /// Re-mint the champion's pool from the hunter and build as they are NOW, keeping the same fraction
    /// of it filled. Called by the host at each wave boundary.
    /// </summary>
    /// <remarks>
    /// The pool used to be minted once at run start and held until a death, so a ring equipped
    /// mid-descent (or a rank trained, or a vow sworn) reached the fight's damage from the next wave
    /// but its health only from the next LIFE — and the three health numbers on screen disagreed for
    /// as long as the run lasted. Scaling by fraction rather than adding the difference means a
    /// half-dead champion stays half-dead: gear is never a heal. Not called from PushWave itself so
    /// benches and tests that mint a bespoke pool keep it.
    /// </remarks>
    public void RefreshPool()
    {
        var pool = SoloBattle.ChampionHealth(_build, _hunter);
        if (pool == _champion.MaxHealth || _champion.MaxHealth <= 0) return;
        var fraction = _champion.Health / (float)_champion.MaxHealth;
        _champion.MaxHealth = pool;
        _champion.Health = _champion.Alive ? Math.Clamp((int)MathF.Round(pool * fraction), 1, pool) : 0;
    }

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

        var compRng = new Random(Bands.Seed(RegionId, next, RunIndex));
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
                // Archetype is COPIED, and forgetting it inverted the one node that reads it. A creature
                // re-minted here kept its numbers and lost its tag, and WaveCreature.Archetype has exactly
                // one consumer in the game: SIEGE, "+45% to ARMOURED, -25% to everything else". A null tag
                // is not Armoured, so on a PLATED wave — the armoured wave, the wave this affix exists to
                // make more armoured — the anti-armour Greater applied its penalty instead of its bonus.
                // The player who bought the answer to armour was the one punished by it.
                .Select(c => new WaveCreature
                {
                    MaxHealth = c.MaxHealth, Health = c.Health, Damage = c.Damage,
                    Defense = c.Defense * defMult, Source = c.Source, Archetype = c.Archetype,
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
                Source = creatures[0].Source, Archetype = creatures[0].Archetype,
            });

        var biteInterval = Math.Max(100, (int)(interval * Bands.IntervalMultiplier(affixes)));

        var metrics = new WaveMetrics();
        var sustain = Bands.SustainMultiplier(affixes);
        var (outcome, events) = SoloBattle.ResolveWave(
            _champion, _build, _hunter, creatures, biteInterval, _tuning, _rng, bonus, isBoss, metrics,
            sustain);
        Recorder.Record(next, metrics);

        LastOutcome = outcome;
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

        // ── BETWEEN WAVES. game-flow.md §3.3 makes "health does not regenerate between waves" a rule of
        //    the game — it is what turns a descent into one continuous fight rather than a series of
        //    independent ones. RECOVERY and ENDLESS are the only two things in the game that buy an
        //    exception to it, which is why the ENDURE branch is the one that raises every band's
        //    ceiling and why its nodes are priced honestly rather than generously. ──────────────────
        var shape = _build.Shape;
        if (shape.FullHealBetweenWaves)
        {
            _champion.Health = _champion.MaxHealth;
        }
        else if (shape.BetweenWaveRegen > 0f && !_build.Triggers(_hunter).Contains(BuildTrigger.NoHealing))
        {
            // BLOOD MAGIC and JUGGERNAUT say "YOU CANNOT BE HEALED", and until the heal rework this
            // one regain ignored them — the in-wave funnel refused every heal and the between-wave
            // one paid out regardless, so a Blood Magic build that walked to RECOVERY was healed by
            // the node its keystone forbids. The heal balance probe caught it: the "no heals" twin
            // of the ENDURE walk was still regaining 3.4% of its pool per wave.
            //
            // NOT under HealTuning.MaxHealFractionPerWave, on purpose — see that record's remarks:
            // this regain is bounded by its own fraction, halved by the ENDLESS band, and priced as
            // the one structural exception to "no regeneration between waves".
            // The band's ENDLESS affix reaches here too — RECOVERY is regeneration by name, and an affix
            // that halves regeneration but spares the one node called REGAIN HEALTH BETWEEN WAVES would
            // be pressuring ENDURE everywhere except its own answer. (Note the name collision: the
            // mastery node "endless" is FullHealBetweenWaves above; Affix.Endless is the band.)
            var back = (int)MathF.Round(_champion.MaxHealth * shape.BetweenWaveRegen * sustain);
            _champion.Health = Math.Min(_champion.MaxHealth, _champion.Health + back);
        }

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
    /// <summary>Gleam paid per wave, before depth scale and the build's Haul multiplier.</summary>
    /// <remarks>
    /// Named rather than inline so the economy test can cite it and so a retune is one edit in one
    /// place. Every other term in the wave payout is a multiplier on this.
    /// </remarks>
    public const float GleamPerWaveCoefficient = 3f;

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

        // BOSS-WAVE CORES ARE GONE (2026-08-24). They fed the hatchery of the retired creature
        // subsystem — "2 per boss" into a currency that could never be spent. Only the skill payouts
        // (HARVEST/LODESTONE) still write the channel, and nothing consumes it; see WaveBonus.Cores.
        return new Haul(
            Cores: bonus.Cores,
            // THE COEFFICIENT WAS 8 AND IS MEASURED, NOT GUESSED. At 8 a trained champion earned 287
            // Gleam per minute of fight time, which funds the whole 79,578 lifetime training sink in
            // 4.6 hours — and that was the SMALL faucet, next to the Warren's 1,994/min. Playtest:
            // "çok fazla gold geliyor."
            //
            // At 3 the champion is the PRIMARY earner (roughly twice the retuned Warren), which is the
            // relationship the game wants: playing pays more than leaving it running. See
            // gleam_economy_test, which states both sides of the economy as hours of play — the only
            // form in which either number means anything.
            Gleam: (int)MathF.Round(GleamPerWaveCoefficient * scale * haul),
            Quality: MathF.Max(0.05f, mods.Rarity) + bonus.Quality);      // SPLINTER adds quality
    }

    // NO Bank / Retreat / Wipe payout, and no ExitShare. The idle loop pays every wave the instant it
    // clears (see LastWaveHaul), so nothing is ever "at risk" waiting to be banked — a wipe simply ends
    // this attempt, and the caller starts a fresh one. Removing the priced exit is the whole point of the
    // idle turn: the player stops being asked "bank or push?" after every fight and just watches, and the
    // depth their build can reach becomes the only thing that gates the haul.
}
