using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Combat;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Traits;

namespace IdleXIdle.Core.Builds;

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

    /// <summary>
    /// The STYLE whose skills dealt the most damage LAST wave — what WARDED wards. Null until a wave
    /// has resolved: the affix needs history and must not guess, so it never applies on a run's first
    /// wave. Ties break toward more casts, then by style order — deterministic, because the player
    /// must be able to predict what tomorrow's wall resists.
    /// </summary>
    public Style? LastWaveTopStyle { get; private set; }

    /// <summary>The composition the last resolved wave held — the report reads this.</summary>
    public IReadOnlyList<WaveCreature> LastWaveCreatures { get; private set; } = Array.Empty<WaveCreature>();

    /// <summary>The bite clock the last wave was resolved on, before any slow — what the enemy inspector measures a slow against.</summary>
    public int LastWaveIntervalMs { get; private set; } = EnemyIntervalMs;

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
        _rng = rng ?? new Random(20260716);
    }

    public Champion Champion => _champion;

    /// <summary>
    /// The equipped skills of the build THE FIGHT IS RUNNING — slot order matches every
    /// <see cref="BattleEvent"/>'s slot payload. The screen renders from this, never from the
    /// editable loadout: the two can differ for a whole wave after an edit (ReplaceBuild lands at
    /// the boundary), and drawing the loadout is the exact UI-state/sim-state split this project
    /// has been bitten by before.
    /// </summary>
    public IReadOnlyList<EquippedSkill> Skills => _build.Skills;
    /// <summary>
    /// Where the woven skills bank the levels they earn, or null when nothing is tracking them.
    /// </summary>
    /// <remarks>
    /// Injected rather than owned: the expedition is minted per run and a skill's levels outlive
    /// every run, so an expedition that owned this would hand back a champion whose skills forgot
    /// everything the moment it banked. Null in the tests that only measure a fight.
    /// </remarks>
    public SkillProgress? Progress { get; init; }

    /// <summary>
    /// The account's trait ledger, fed by every CLEARED wave, or null when nothing is watching.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Injected for the same reason <see cref="Progress"/> is: an expedition is minted per run and a
    /// discovered trait outlives every one. Null in a probe or a bench, which is why the balance
    /// suites are entirely unaffected by traits existing.
    /// </para>
    /// <para>
    /// <b>Only a cleared wave feeds it</b> — the rule skill experience already follows: a run that
    /// dies teaches nothing on its way out. WHAT KILLED YOU is the deliberate exception and is
    /// evaluated at run END by the host, from the saved run log.
    /// </para>
    /// </remarks>
    public TraitWatch? TraitWatch { get; init; }

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
    /// For each Vow, how many waves of THIS descent were cleared with its rule held and the Vow unsworn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Vow's condition is a property of the build, so an end-of-descent check would need no counter at
    /// all — except that the build can CHANGE mid-descent (see <see cref="ReplaceBuild"/>). A player
    /// could clear fourteen waves on a four-skill, four-source, fully-armoured build, drop to one skill
    /// in the breath before wave fifteen, die, and have the end-of-run context report one Source, one
    /// Style, no defence and three bare slots — six Vows proved by a build that never fought.
    /// </para>
    /// <para>
    /// So the proof is counted per CLEARED wave, as the descent happens. The wave that killed you does
    /// not count, and a checkpoint start cannot buy proof it never fought for. Never persisted, never
    /// crosses a run: this is the only new state the whole Vow-discovery rule needs.
    /// </para>
    /// </remarks>
    public IReadOnlyDictionary<string, int> VowProofWaves => _vowProofWaves;

    private readonly Dictionary<string, int> _vowProofWaves = new(StringComparer.Ordinal);


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
                || old[i].Def.Id != now[i].Def.Id
                || old[i].Source != now[i].Source)
            {
                _champion.ReadyAt.Remove(i);
                _champion.ReadyAtBeat.Remove(i);   // the beat-counted table is keyed the same way
            }
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
        // THROUGH THIS RUN'S OWN TUNING. The wave-length transform scales the pool as well as the wave
        // (see ExpeditionTuning.WaveLengthScale), and re-minting from the DEFAULT scale here would hand
        // a probe sweeping that knob a champion from a different game than the wave it is fighting —
        // which is how the first measurement of this change came back wrong.
        var pool = SoloBattle.ChampionHealth(_build, _hunter, _tuning.ChampionPoolScale);
        if (pool == _champion.MaxHealth || _champion.MaxHealth <= 0) return;
        var fraction = _champion.Health / (float)_champion.MaxHealth;
        _champion.MaxHealth = pool;
        _champion.Health = _champion.Alive ? Math.Clamp((int)MathF.Round(pool * fraction), 1, pool) : 0;
    }

    /// <summary>
    /// Credit one cleared wave to every Vow whose rule this build kept without swearing it.
    /// </summary>
    /// <remarks>
    /// One <c>DescribeBuild</c> per cleared wave — the same context the fight itself builds once per
    /// wave for exactly the same reason. Vow discovery reads the BUILD; trait discovery reads the wave's
    /// metrics. That is the line between the two systems, and it is why they can never answer the same
    /// question twice.
    /// </remarks>
    private void CountVowProof()
    {
        var ctx = SoloBattle.DescribeBuild(_build, _hunter);
        var sworn = _build.Vows;

        foreach (var vow in Vows.Discoverable)
        {
            if (sworn.Any(v => v.Id == vow.Id)) continue;   // a Vow you wore proves nothing
            if (!Vows.RuleHeld(vow, ctx)) continue;
            _vowProofWaves[vow.Id] = _vowProofWaves.GetValueOrDefault(vow.Id) + 1;
        }
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
        LastWaveIntervalMs = interval;

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

        // WAVE LENGTH. The wave carries ExpeditionTuning.WaveLengthScale times the health, and the
        // CHAMPION'S POOL carries the same factor (SoloBattle.ChampionHealth) — so the fight lasts that
        // many times longer and costs exactly the same share of the champion. The bite itself is
        // untouched: see that knob's remarks for why scaling health alone, or health against a thinned
        // bite, both move depth instead of only time.
        var health = _enemyBaseHealth * scale * repeat * Bands.HealthMultiplier(affixes)
                     * MathF.Max(0.1f, _tuning.WaveLengthScale);
        var damage = _enemyBaseDamage * damageScale * dmgMult * repeat
                     * Bands.DamageMultiplier(affixes, wavesIntoBand);

        var creatures = Archetypes.Compose(
            archetype, health, damage, next, BandCycles.RosterFor(RegionId), compRng, forceSingle: isBoss);

        // PLATED thickens whatever armour the composition brought. On a Swarm band that is zero, and the
        // affix is wasted — which is allowed. Re-rolling affixes to avoid it would cost the player the
        // one thing this system gives them: a ladder they can learn before they descend.
        var defMult = Bands.DefenseMultiplier(affixes);
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
            sustain,
            wardedStyle: affixes.Contains(Affix.Warded) ? LastWaveTopStyle : null,
            entrenched: affixes.Contains(Affix.Entrenched),
            legionSplits: affixes.Contains(Affix.Legion));
        Recorder.Record(next, metrics);
        if (outcome == WaveOutcome.Cleared) CountVowProof();

        // The ledger WARDED reads next wave: this wave's top style by damage, ties toward more casts,
        // then style order. A wave in which no skill landed wards nothing.
        LastWaveTopStyle = metrics.StyleDamage.Count == 0
            ? null
            : metrics.StyleDamage
                .OrderByDescending(kv => kv.Value)
                .ThenByDescending(kv => metrics.StyleActivations.GetValueOrDefault(kv.Key))
                .ThenBy(kv => (int)kv.Key)
                .First().Key;

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

        // ── THE SKILLS EARN THEIR OWN LEVELS. One per CLEARED wave, per equipped skill, which is the
        //    unit an active and a passive share: an active casts a few times a wave and a Field ticks
        //    a dozen times, so counting activations would level a passive three times faster for
        //    doing the same job — and the player did not choose the tick rate, the catalogue did.
        //    Only a cleared wave counts, so a run that dies teaches nothing on its way out. ─────────
        if (Progress is { } prog)
            foreach (var sk in _build.Skills)
                prog.RecordWave(sk.Def.Id);

        // ── AND THE TRAITS LEARN FROM IT TOO, on the same terms and for the same reason: a cleared
        //    wave taught something and a lost one did not. The facts are built HERE rather than in
        //    the fight because half of them are build facts the wave's metrics do not carry — how
        //    much of the pool the champion was standing in, what its critical chance is, how many
        //    elements it carries — and the fight must not learn about the account to answer them.
        if (TraitWatch is { } watch)
            watch.WaveCleared(TraitFactsFor(metrics), next);

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

    /// <summary>
    /// Everything one cleared wave contributes to the trait ledger: the wave's own measurements, plus
    /// the build facts the measurements cannot carry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing here reads a roll.</b> Creature count and archetype are rolled by the band, so no
    /// rule may read either; every fact below is either the champion's own build, a measurement of
    /// what the champion DID, or a deterministic property of the weave. That is BRIEF §30 satisfied
    /// by construction rather than by inspection.
    /// </para>
    /// <para>
    /// Built once per cleared wave. <c>DescribeBuild</c> walks four skills and is the same call the
    /// vow layer already makes every wave, so this adds one of them and no allocation the fight did
    /// not already make.
    /// </para>
    /// </remarks>
    private TraitWaveFacts TraitFactsFor(WaveMetrics m)
    {
        var ctx = SoloBattle.DescribeBuild(_build, _hunter);
        var shape = _build.Shape;

        var wide = false;
        for (var i = 0; i < _build.Skills.Count; i++)
        {
            // A WIDE wave is one this build could have reached three creatures with — a property of
            // the weave, not of how many creatures the band happened to roll.
            if (shape.TargetsFor(_build.Skills[i].Def) >= TraitDiscovery.WideTargets) wide = true;
        }

        return new TraitWaveFacts(
            ChampionMaxHealth: _champion.MaxHealth,
            HeavyHits: m.HeavyHits,
            Overkill: m.Overkill,
            ShieldGained: m.ShieldGained,
            ShieldBreaks: m.ShieldBreaks,
            ShieldAbsorbed: m.ShieldAbsorbed,
            Healed: m.Healed,
            ReflectedDamage: m.ReflectedDamage,
            LowestHealthFraction: m.LowestHealthFraction,
            HealthLost: m.HealthLost,
            CreaturesKilled: m.CreaturesKilled,
            MarkCasts: m.MarkCasts,
            CarriedWideSkill: wide,
            CreaturesPresent: m.CreaturesPresent,
            CritPercent: ctx.CritPercent,
            OneElement: _build.Skills.Count > 0
                        && _build.Skills.All(s => s.Source == _build.Skills[0].Source),
            DistinctElements: ctx.DistinctSources,
            // The promises this build is KEEPING, asked of the whole build and counted once per vow —
            // the same call the fight makes when it decides what those promises pay.
            VowsKept: Vows.KeptCount(_build.Vows, ctx));
    }

    private Haul HaulForWave(int wave, WaveBonus bonus)
    {
        var scale = WaveScaling.HaulScale(wave, _tuning);
        var mods = _build.Resolve(_hunter);

        var haul = MathF.Max(0.05f, mods.Haul);
        var shape = _build.Shape;

        // ── LOOT (design §9b, re-keyed 2026-08-30). Every one of these reads THE WAVE THAT JUST
        //    HAPPENED rather than a banking decision this game does not have. LastWaveEvents is
        //    written above, before this runs, so the bites the champion took — and did not take —
        //    are the currency the branch trades in.
        var bites = LastWaveEvents.Where(e => e.Kind == BattleEventKind.EnemyStrike).ToList();
        var waveMs = LastWaveEvents.Count == 0 ? 0 : LastWaveEvents.Max(e => e.AtMs);
        // The longest stretch of the wave with nothing landing on you. Measured as a GAP between
        // consecutive bites (and from the wave's start and to its end), because "seconds without
        // damage" is a run of quiet, not a count of quiet ticks.
        var cleanMs = waveMs;
        if (bites.Count > 0)
        {
            var last = 0;
            cleanMs = 0;
            foreach (var e in bites.OrderBy(e => e.AtMs))
            {
                cleanMs = Math.Max(cleanMs, e.AtMs - last);
                last = e.AtMs;
            }
            cleanMs = Math.Max(cleanMs, waveMs - last);
        }

        // UNTOUCHED — the quiet itself pays, to the node's own ceiling.
        if (shape.HaulPerCleanSecond > 0f)
        {
            var earned = shape.HaulPerCleanSecond * (cleanMs / 1000f);
            haul *= 1f + (shape.HaulCleanCap > 0f ? MathF.Min(shape.HaulCleanCap, earned) : earned);
        }

        // SPOTLESS and VEIN — a wave you finish NEARLY WHOLE, at nine tenths or better.
        //
        // Two earlier readings were unreachable and would have shipped dead. "A wave nothing bit you
        // in" never happens: every creature in a wave attacks. "A wave finished at FULL health" never
        // happens either, because there is no full heal between waves — the champion is chipped once
        // and stays chipped for the run. Nine tenths is the first version a real build can hold, and
        // it is still the strict opposite of BLOODPRICE's half.
        if (shape.HaulUntouchedWave > 0f
            && _champion.Health * 10 >= _champion.MaxHealth * 9)
            haul *= 1f + shape.HaulUntouchedWave;

        // BLOODPRICE — the opposite pole, and the reason UNTOUCHED is a fork rather than a ladder:
        // one branch of LOOT pays for never being hit, the other for finishing the wave nearly dead.
        if (shape.HaulWhenHurt > 0f && _champion.Health * 2 < _champion.MaxHealth)
            haul *= 1f + shape.HaulWhenHurt;

        // PROSPECT and LODE — depth itself, past a floor. This is the "rest of the run" the design
        // asked for, expressed the only way a per-wave payout can express it: the deeper the wave,
        // the larger every one of them is.
        if (shape.HaulPerWavePastDepth > 0f)
            haul *= 1f + shape.HaulPerWavePastDepth * Math.Max(0, wave - shape.HaulDepthFloor);

        // CACHE — every Nth wave is worth more. A rhythm rather than a ramp.
        if (shape.HaulEveryNthWave > 0 && wave % shape.HaulEveryNthWave == 0)
            haul *= 1f + shape.HaulNthWaveBonus;

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
        // subsystem — "2 per boss" into a currency that could never be spent. The skill payouts
        // (HARVEST/LODESTONE) still write the channel, and the HOST pays it out as the CORE material
        // (Game1 credits Haul.Cores into the material wallet) — live, despite the retired name.
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
            // ...times the wave's own LENGTH. Gleam per second is what gleam_economy_test bounds, and
            // it is per-wave gleam over per-wave WALL TIME, so a wave that lasts longer and pays the
            // same would have quietly cut income. See ExpeditionTuning.WaveHaulScale for why the factor
            // is measured rather than borrowed from the health multiplier.
            Gleam: (int)MathF.Round(GleamPerWaveCoefficient * scale * haul
                                    * MathF.Max(0.1f, _tuning.WaveHaulScale)),
            // SECOND LOOK — the clean stretch raises what the chest IS, not just what the wave pays.
            // Rarity is the one channel gleam cannot buy, so it is deliberately the greater's reward.
            Quality: MathF.Max(0.05f, mods.Rarity) + bonus.Quality
                     + shape.RarityFromClean * (cleanMs / 1000f));      // SPLINTER adds quality
    }

    // NO Bank / Retreat / Wipe payout, and no ExitShare. The idle loop pays every wave the instant it
    // clears (see LastWaveHaul), so nothing is ever "at risk" waiting to be banked — a wipe simply ends
    // this attempt, and the caller starts a fresh one. Removing the priced exit is the whole point of the
    // idle turn: the player stops being asked "bank or push?" after every fight and just watches, and the
    // depth their build can reach becomes the only thing that gates the haul.
}
