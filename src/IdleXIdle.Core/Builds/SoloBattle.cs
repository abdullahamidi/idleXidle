using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Expeditions;

namespace IdleXIdle.Core.Builds;

/// <summary>
/// The rules SHIELD obeys, in one place.
/// </summary>
/// <remarks>
/// <para>
/// SHIELD is a temporary combat resource that absorbs incoming damage before Health. <b>It is not
/// Health.</b> It never changes what current health, maximum health, a low-health condition, a
/// full-health condition, health scaling or a death threshold mean — every one of those reads
/// <see cref="Champion.Health"/> and only that.
/// </para>
/// <para>
/// One global capacity and one pool, deliberately: a Machine shield, a Nature shield and a Snare
/// shield would be three resources the player has to hold in their head to read one bar. Everything
/// that grants Shield grants THIS Shield, additively, to the same ceiling.
/// </para>
/// <para>
/// <b>Wave-local.</b> It resets to zero at the start of every wave, and wave-start effects grant
/// after that reset. An idle game whose shield accumulated while nothing was happening would make
/// standing still the strongest defensive play.
/// </para>
/// </remarks>
public static class ShieldRules
{
    /// <summary>The ceiling, as a share of the pool: half of maximum health.</summary>
    public const float CapFraction = 0.5f;

    /// <summary>The most Shield a hunter with this pool may hold.</summary>
    public static int CapFor(int maxHealth) => (int)MathF.Round(Math.Max(0, maxHealth) * CapFraction);
}

/// <summary>Live state for ONE character across a whole expedition. Health and cooldowns persist.</summary>
public sealed class Champion
{
    /// <summary>
    /// The pool. Settable, not init-only, since the 2026-08-25 health model: a worn ring or a trained
    /// rank changes the pool the champion is standing in, and <see cref="SoloExpedition.RefreshPool"/>
    /// rescales it at the next wave boundary instead of waiting for a death.
    /// </summary>
    public required int MaxHealth { get; set; }
    public int Health { get; set; }
    public bool Alive => Health > 0;

    /// <summary>Ready-at time per skill index, in expedition-absolute ms.</summary>
    public Dictionary<int, int> ReadyAt { get; } = new();

    /// <summary>Expedition-absolute ms elapsed before the current wave. See SkillClock for why.</summary>
    public int ElapsedMs { get; set; }

    /// <summary>UNDYING is once per EXPEDITION, so it lives here and not in a wave-scoped local.</summary>
    public bool UndyingSpent { get; set; }

    /// <summary>
    /// Beats the champion has taken across the whole expedition. Beat-counted cooldowns
    /// (<see cref="ReadyAtBeat"/>) are measured against it, so they persist across waves like the
    /// time-counted ones.
    /// </summary>
    public int BeatCount { get; set; }

    /// <summary>
    /// SHADOW 3p SHADE — deaths waiting to be spent by a damaging Active. On the CHAMPION rather than
    /// in the wave, because the design lets a shade wait for the next one; the ceiling is the set's
    /// (<c>SkillShape.ShadeMax</c>), so taking the gear off simply stops it climbing.
    /// </summary>
    public int Shades { get; set; }

    /// <summary>Per skill slot: the beat at which a beat-counted skill is ready again (see FormBehaviour.CooldownBeats).</summary>
    public Dictionary<int, int> ReadyAtBeat { get; } = new();

    // ── SHIELD ───────────────────────────────────────────────────────────────────────────────────
    //
    // STATE, not an event trail. A screen that opens halfway through a fight must be able to draw the
    // shield without having witnessed the grant that made it, so the figure lives here beside Health
    // and the events exist to explain the CHANGES rather than to carry the value.

    /// <summary>Shield held right now. Absorbs incoming damage before <see cref="Health"/> does.</summary>
    public float CurrentShield { get; private set; }

    /// <summary>The ceiling this hunter's Shield is clamped to — half the pool.</summary>
    public int MaxShield => ShieldRules.CapFor(MaxHealth);

    /// <summary>
    /// Grant Shield, clamped to the cap. Returns what was ACTUALLY added, which is zero at the cap.
    /// </summary>
    /// <remarks>
    /// Every producer goes through here — REPAY/BANKED, IRON/PLATING, the MACHINE set, NATURE's
    /// OVERGROWTH — so the cap is enforced once rather than at four call sites, and a caller can
    /// report what it really gave rather than what it offered.
    /// </remarks>
    public float GainShield(float amount)
    {
        if (amount <= 0f) return 0f;
        var before = CurrentShield;
        CurrentShield = MathF.Min(MaxShield, CurrentShield + amount);
        return CurrentShield - before;
    }

    /// <summary>
    /// Spend Shield against POST-MITIGATION damage. Returns what the Shield ate; the caller sends the
    /// remainder to Health.
    /// </summary>
    /// <remarks>
    /// Post-mitigation on purpose: the Shield absorbs the damage the hunter would actually have taken,
    /// not the raw bite. Subtracting the raw figure would make every other defensive system worth less
    /// the more Shield you held, which is the opposite of composable.
    /// </remarks>
    public float AbsorbWithShield(float damage)
    {
        if (damage <= 0f || CurrentShield <= 0f) return 0f;
        var eaten = MathF.Min(CurrentShield, damage);
        CurrentShield -= eaten;
        return eaten;
    }

    /// <summary>Wave start: Shield is wave-local and always begins at zero.</summary>
    public void ResetShield() => CurrentShield = 0f;
}

/// <summary>
/// One enemy in a wave. A wave holds between one and five of these.
/// </summary>
/// <remarks>
/// <para>
/// Waves used to be a single float of health, which meant a build could only ever be too SMALL, never
/// the wrong SHAPE — there was nothing for target count to be about and no way for an archetype to
/// punish anything. Everything the redesign asks of content (Swarm, Armoured, Caster, Bruiser) is
/// expressed here: count, health, damage, and flat armour.
/// </para>
/// <para>
/// <see cref="Source"/> is per creature, not per wave. A composition draws mixed Sources so that
/// picking a Source is a bet on the wave's weighting rather than a lookup with one correct answer.
/// </para>
/// </remarks>
public sealed class WaveCreature
{
    public required float MaxHealth { get; init; }
    public float Health { get; set; }

    /// <summary>What this creature deals per bite, before the champion's mitigation.</summary>
    public required float Damage { get; init; }

    /// <summary>
    /// FLAT per-hit mitigation — see <see cref="SoloBattle.MinHitFraction"/>.
    /// </summary>
    /// <remarks>
    /// Settable, not init-only, because SUNDER strips armour permanently FOR THE REST OF THE WAVE. That
    /// is the whole node: a Weight build gets stronger as an Armoured wave goes on, which is the opposite
    /// of how flat mitigation normally behaves. Creatures are minted per wave, so the mutation cannot
    /// leak into the next one.
    /// </remarks>
    public float Defense { get; set; }

    public Source? Source { get; init; }

    /// <summary>What this creature IS — read by SIEGE, which pays against Armoured and taxes the rest.</summary>
    public Encounters.Archetype? Archetype { get; init; }

    public bool Alive => Health > 0f;

    public static WaveCreature Single(float health, float damage, float defense = 0f, Source? source = null)
        => new() { MaxHealth = health, Health = health, Damage = damage, Defense = defense, Source = source };
}

/// <summary>
/// What one wave actually did, measured while it resolved.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the player cannot watch the fight and change anything. With no in-run decisions
/// the run is a measurement, and the ONLY moment they can learn is the report afterwards — so the sim
/// has to record the facts that point at a lever. Every field here maps to something the player can
/// change: absorbed damage points at hit size, targets-per-activation points at action economy, health
/// lost points at sustain.
/// </para>
/// <para>
/// <see cref="RawDamage"/> is measured BEFORE enemy armour and <see cref="DeliveredDamage"/> after, and
/// the gap between them is the single most useful number in the game — it is the difference between
/// "my build is too small" and "my build is the wrong shape", which is exactly the distinction the
/// design says the player must never be unable to make.
/// </para>
/// </remarks>
public sealed class WaveMetrics
{
    /// <summary>Damage swung, before enemy armour ate any of it.</summary>
    public float RawDamage { get; set; }

    /// <summary>Damage that actually landed.</summary>
    public float DeliveredDamage { get; set; }

    /// <summary>Individual hits landed, of any kind.</summary>
    public int Hits { get; set; }

    /// <summary>Skill activations — one per cast, however many creatures it reached.</summary>
    public int Activations { get; set; }

    /// <summary>Creatures struck, summed across activations. Divided by Activations this is reach.</summary>
    public int TargetsStruck { get; set; }

    /// <summary>Creatures present when the wave began.</summary>
    public int CreaturesPresent { get; set; }

    public int CreaturesKilled { get; set; }

    /// <summary>Health the champion lost during this wave.</summary>
    public int HealthLost { get; set; }

    // ── FOUR DIFFERENT FACTS, KEPT APART. "Damage taken" was one ambiguous number, and once a bite
    //    can be prevented outright or eaten by a shield it stops answering any of the questions a
    //    build asks: was I protected, or was I never hit?

    /// <summary>Post-mitigation damage the wave tried to deal — before prevention or Shield.</summary>
    public float DamageAttempted { get; set; }

    /// <summary>Of that, what a hard preventer stopped outright (IRON, MACHINE's PLATING).</summary>
    public float DamagePrevented { get; set; }

    /// <summary>Of the rest, what SHIELD absorbed. Never counts as damage taken.</summary>
    public float ShieldAbsorbed { get; set; }

    /// <summary>What actually reached the pool. This is the figure REPAY and low-health rules read.</summary>
    public int HealthDamage { get; set; }

    public int DurationMs { get; set; }

    /// <summary>How much of the swing enemy armour ate. The Weight axis, as one number.</summary>
    public float AbsorbedFraction => RawDamage <= 0f ? 0f : 1f - DeliveredDamage / RawDamage;

    public float AverageHitSize => Hits <= 0 ? 0f : DeliveredDamage / Hits;

    /// <summary>Creatures reached per cast. The Spread axis, as one number.</summary>
    public float TargetsPerActivation => Activations <= 0 ? 0f : TargetsStruck / (float)Activations;

    /// <summary>Skill damage landed, by the skill's STYLE — the ledger WARDED reads next wave.</summary>
    public Dictionary<Style, float> StyleDamage { get; } = new();

    /// <summary>Skill casts by STYLE — WARDED's deterministic tie-break.</summary>
    public Dictionary<Style, int> StyleActivations { get; } = new();
}

/// <summary>
/// The fight, for a single character. No squad, no slots, no roles.
/// </summary>
/// <remarks>
/// <para>
/// The roster is gone: the player has ONE character and a BUILD, and every decision that shapes this
/// fight was made before it started — which skills were woven, which keystones were accepted, what is
/// worn. The fight itself is fully automatic. That is the design: the player deals with items and
/// builds, and the game plays itself.
/// </para>
/// <para>
/// Deterministic given a seeded Random, so a build can be reasoned about rather than tested by feel.
/// Pure Core — no MonoGame (ADR-001). It emits <see cref="BattleEvent"/>s for the view to replay.
/// </para>
/// </remarks>
public static class SoloBattle
{
    /// <summary>How hard the character swings without a skill. Deliberately small.</summary>
    /// <remarks>
    /// The auto-attack exists so a fight cannot stall to zero, not so it can carry a build. If it ever
    /// out-damages the woven skills, the skill tree stops mattering and the game is idle-clicker mush.
    /// </remarks>
    /// <summary>
    /// The basic attack's raw damage at zero MIGHT — MIGHT multiplies it (Hunter.AutoDamageMultiplier), then
    /// the shared multiplier (weapon, worn mods, build) like every hit.
    /// </summary>
    /// <remarks>
    /// 18, from 6 (2026-08-26). Measured with the old 6: the swing was 9–15% of a one-skill build's damage
    /// and, against any armoured wave, landed for the minimum fraction — twelve swings dealt 12 in a
    /// 17-second wave. A basic attack that armour eats whole is a stat the player trains for nothing.
    /// At 18 the swing is about a third of a one-skill build's damage and survives armour.
    /// </remarks>
    public const float AutoAttackDamage = 72f;   // 18 → 48 under the beat (2026-08-27): one swing per 1.5 s, not per 1.2, and the beat's ceiling (see FormBaseValue)
    /// <summary>
    /// THE BEAT at action speed 1.0. The champion acts on a metronome — one cast or one swing per beat,
    /// never two actions inside one — so every animation plays whole, with a settle before the next
    /// (playtest 2026-08-27: "one action must finish before the next; let's try 1.5 s"). TEMPO divides it.
    /// </summary>
    public const int DefaultBeatMs = 1_500;

    /// <summary>
    /// HOW LONG A WAVE LASTS: the multiplier on every creature's health, and so on the number of beats
    /// a wave takes. See <see cref="Expeditions.ExpeditionTuning.WaveLengthScale"/> for the whole
    /// measurement and for why nothing but health may move with it.
    /// </summary>
    public const float WaveLengthScale = 2.5f;

    /// <summary>
    /// THE CHAMPION'S SHARE OF THAT STRETCH — what <see cref="ChampionHealth"/> multiplies the pool by.
    /// Deliberately BELOW <see cref="WaveLengthScale"/>, and the gap is measured, not chosen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The arithmetic says these two should be equal: scale both sides' health and every per-wave
    /// quantity lands on the same factor, so depth is untouched and only time stretches. Measured, they
    /// are not — at 2.5 and 2.5 a run goes DEEPER, because two things quietly favour the longer fight:
    /// </para>
    /// <list type="bullet">
    /// <item><description>the wave's flat opening pause
    /// (<see cref="Expeditions.ExpeditionTuning.WaveOpeningMs"/>) is paid once per wave and now amortises
    /// over 2.5x the beats, so the champion's average damage rises and the wave comes out ~1.6-2.2x
    /// longer rather than 2.5x;</description></item>
    /// <item><description>healing interleaves with damage more finely. In a two-exchange wave a leech
    /// lands while the champion is still near full and is thrown away as overheal; in a five-exchange
    /// wave it lands on a real wound. Measured on a four-skill build, healing rose from 13.0% to 16.4%
    /// of the pool per wave while damage taken held at 15-17%, which at depth is the difference between
    /// losing 2% of the pool a wave and losing 0.5%.</description></item>
    /// </list>
    /// <para>
    /// Both push the same way and neither is a defect — a longer fight SHOULD let sustain work. So the
    /// pool's share is measured against the one thing that must not move, depth. Median depth for the
    /// vow sweep's plain four-skill build on its mid-career hunter, wave held at 2.5:
    /// </para>
    /// <code>
    ///   pool x   median depth        (the pre-stretch baseline is 16)
    ///     2.5         20
    ///     2.2         18
    ///     2.0         17
    ///     1.8         16   &lt;- here
    ///     1.6         15
    ///     1.4         13
    /// </code>
    /// <para>
    /// It does NOT touch wave length, which is the whole reason it can be tuned separately: length is
    /// the wave's health over the champion's damage, and the pool is in neither. Swept from 1.4 to 2.5
    /// a fresh champion's wave stayed at 6.70 s to the centisecond.
    /// </para>
    /// </remarks>
    public const float ChampionPoolScale = 1.8f;

    /// <summary>The beat's length for a build's action speed: DefaultBeatMs ÷ rate, never under 200 ms.</summary>
    public static int BeatFor(float rate, int beatMs = DefaultBeatMs) => Math.Max(200, (int)MathF.Round(beatMs / MathF.Max(0.1f, rate)));

    /// <summary>
    /// VENOM's poison, as a fraction of the skill hit that inflicts it, for a source that carries no
    /// magnitude of its own — i.e. the VENOMANCER keystone. A worn Venom enchant raises it to its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This constant is the whole reason VENOM stopped being a lie. The trigger existed, VENOMANCER
    /// granted it and PAID −20% damage for it, and nothing in the sim applied a single point of poison —
    /// so the keystone was a pure downside and the weapon enchant was decoration. A dormant trigger,
    /// exactly the bug species this codebase keeps rediscovering.
    /// </para>
    /// <para>
    /// It was 0.30, and at 0.30 VENOMANCER was STILL a trap: the poison recovered most of the −20%
    /// damage but not all of it, so a venomancer build did 4021 to a plain build's 4039 over the full
    /// wave ceiling — a strict downgrade, the exact thing the keystone's "worse early, better late"
    /// promise forbids. Raised to 0.50 so the DoT clearly out-earns the penalty on a long fight
    /// (~+12.6% at the ceiling) while still ramping from nothing, so it stays a trade and not a stat.
    /// The lever is deliberately the poison magnitude, not the penalty: the penalty is the keystone's
    /// identity, and SoloBattleTests isolates the poison by holding an equal-penalty twin against it.
    /// </para>
    /// </remarks>
    public const float VenomBasePoison = 0.50f;

    /// <summary>How much of the standing poison bleeds every half-second. The rest carries to later ticks.</summary>
    /// <remarks>
    /// 0.12, from 0.25 (2026-08-29) and 0.5 before that. Skills land on beats, and since the cooldowns
    /// doubled a rhythm skill poisons every 4-6 beats (6-9 s); a pool that shed a quarter every half
    /// second was gone long before the next hit, so venom "arrived whole" instead of ramping — its whole
    /// identity. At 0.12 a pool keeps about a fifth across six beats, so successive hits stack.
    /// </remarks>
    public const float VenomBleedPerHalfSecond = 0.12f;

    // ── Commander-stat tuning. CRIT (chance), FOCUS (crit damage) and DEFENSE (mitigation) were three
    //    trained stats that reached NO formula after the pivot to the solo model — the Character screen's
    //    damage bench showed them moving nothing. They are read here now, so training them is real. ──────

    /// <summary>A critical skill hit's floor multiplier, before FOCUS raises it.</summary>
    public const float CritBaseMultiplier = 1.5f;

    /// <summary>The ceiling on critical chance. Shared by the sim and every screen that states it.</summary>
    public const float MaxCritChance = 0.75f;

    /// <summary>Each point of FOCUS adds this to the crit multiplier — FOCUS is the crit-DAMAGE stat.</summary>
    public const float FocusCritDamagePerPoint = 0.01f;

    /// <summary>DEFENSE mitigates a bite on a diminishing curve: taken × K / (K + defense). Never 100%.</summary>
    public const float DefenseMitigationConstant = 100f;

    // ── SIGNATURES. Every Source does one small mechanical thing whenever its skills land — the
    //    Hatsu layer: your element is an identity you build around, not a colour you swap. They are
    //    deliberately SMALL (glue, not payload): if a signature alone beat a mastery capstone, the
    //    capstone axis would die. Public so BuildGlossary formats its text from the same numbers. ──

    /// <summary>BODY — each hit leaves a WOUND on the target. Everyone hits a wounded foe harder.</summary>
    public const float SignatureWoundPerStack = 0.03f;
    public const int SignatureWoundMaxStacks = 5;

    /// <summary>SHADOW — the assassin's cut: bonus damage against foes below half health.</summary>
    public const float SignatureShadowThreshold = 0.5f;
    public const float SignatureShadowBonus = 0.12f;

    /// <summary>MACHINE — every hit bends the target's armour down for the rest of the wave.</summary>
    public const float SignatureMachineStrip = 1f;
    public const int SignatureMachineStripCap = 5;

    /// <summary>MIND — a cast stretches an open MARK window, up to a budget per window.</summary>
    public const int SignatureMindExtendMs = 250;
    public const int SignatureMindExtendCapMs = 1000;

    /// <summary>NATURE — its skills heal for a sliver of what they deal.</summary>
    /// <remarks>
    /// Reads the live <see cref="HealTuning"/> default so the glossary stays one source. The sim itself
    /// reads the injected <c>tuning.Heal</c> — same object unless a test swaps it.
    /// </remarks>
    public static float SignatureNatureLeech => HealTuning.Default.NatureSignatureLeech;

    /// <summary>SPIRIT — the conductor: after it casts, the next OTHER-Source cast hits harder.</summary>
    public const float SignatureSpiritBonus = 0.15f;

    // ── CHARGE — the shared stack primitive the keystones bend. Every skill cast stores a point;
    //    only a reader (REND) makes the pool real, so without one the counter is never tracked. ──

    public const int ChargeCap = 10;
    public const int ChargeCapExtended = 20;            // CAPACITOR
    public const float ChargeRendPerPoint = 0.05f;      // REND — bonus per point spent
    public const int ChargeDynamoPerBite = 2;           // DYNAMO

    /// <summary>
    /// The floor under a hit after ENEMY armour. Armour subtracts a flat amount per hit; this stops it
    /// subtracting everything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Enemy mitigation is FLAT and per-hit, unlike the champion's own <see cref="DefenseMitigationConstant"/>
    /// curve, and the difference is the entire reason the Armoured archetype can exist. A multiplicative
    /// curve scales every hit by the same factor, so it is completely indifferent to whether damage arrives
    /// as ten hits of 40 or one hit of 400. Under it, "few large hits" and "many small hits" are the same
    /// build and there is nothing for a Weight branch to be about.
    /// </para>
    /// <para>
    /// Flat subtraction makes hit SIZE the thing armour reads. Against 25 armour, ten hits of 40 deliver
    /// 150 and one hit of 400 delivers 375 — same raw damage, 2.5x the result.
    /// </para>
    /// </remarks>
    public const float MinHitFraction = 0.15f;

    // ── Form-combo enchantment tuning. Each effect only fires for its Form, so the item is dead weight
    //    on a build that doesn't run that Form — the whole point of a combo. See Enchantments.NeedsForm. ──

    /// <summary>EXECUTE — a STRIKE against an enemy below this fraction of its max HP hits far harder.</summary>
    /// <summary>How often VITALITY's regeneration ticks, in ms.</summary>
    public const int RegenTickMs = 1000;

    /// <summary>How long UNDYING's shield reads as lit on the champion, in ms — the event's Amount.</summary>
    public const int UndyingShieldMs = 1_500;

    /// <summary>WARDED — what the warded STYLE's skills still deal (the other 60% is resisted).</summary>
    public const float WardedResistFactor = 0.4f;

    /// <summary>ENTRENCHED — the share the first hit on each creature deals.</summary>
    public const float EntrenchedFirstHitFactor = 0.25f;

    /// <summary>LEGION — a split child arrives at this share of its parent's strength.</summary>
    public const float LegionChildFactor = 0.5f;

    /// <summary>LEGION — the wave never grows past this many creatures (a Swarm band cannot soft-lock).</summary>
    public const int LegionMaxCreatures = 8;

    /// <summary>
    /// The gap between two casts at skill rate 1: the cast clip's authored length. The real gap is this
    /// divided by the build's skill rate (see the cast loop), so a faster build casts — and animates —
    /// faster, and no cast ever begins before the last one's clip has finished. Playtest 2026-08-26:
    /// "no skill may be thrown until the previous skill's animation ends; skill rate should speed the
    /// animation up." Was a flat 300 ms that let three casts land inside one swing.
    /// </summary>

    public const float ExecuteThreshold = 0.30f;

    /// <summary>How much harder an EXECUTE-boosted STRIKE lands on a weakened enemy.</summary>
    public const float ExecuteMultiplier = 1.6f;

    /// <summary>COILED shortens the TRAP's cooldown by this factor, so it answers far more bites.</summary>
    public const float CoiledCooldownFactor = 0.5f;

    /// <summary>SIPHON multiplies TRANSFORMATION's on-hit leech. Lives in <see cref="HealTuning"/>.</summary>
    public static float SiphonLeechMultiplier => HealTuning.Default.SiphonMultiplier;

    /// <summary>HOARDER — how much of the haul multiplier above 1 becomes hit size.</summary>
    public const float HoarderHaulToForce = 0.20f;

    // ── Keystone tuning, and the enchantments that sharpen it. ─────────────────────────────────────
    //
    // These two were bare 0.8f literals at their use sites. Naming them is not tidying: FERVOUR and
    // BULWARK are defined as "steeper than the keystone alone", which is a claim about a number that
    // has to be readable from one place to stay true. A tuning pass that changed the literal and not
    // the enchantment would silently rebalance both.

    /// <summary>BLOODLUST — how much damage the FULL missing-health fraction is worth.</summary>
    public const float BloodlustScale = 0.8f;

    /// <summary>ZEAL — the mirror, over the health still PRESENT.</summary>
    public const float ZealScale = 0.8f;

    /// <summary>WEAVER — the share of a hit its woven second Form delivers.</summary>
    public const float WeaverEchoFraction = 0.45f;

    /// <summary>
    /// Resolve one wave. Terminates on a kill, a death, or the tick ceiling — never hangs.
    /// </summary>
    /// <summary>
    /// Resolve a wave holding ONE enemy. Kept because most of the game and its tests describe a wave
    /// that way; it builds a single-creature composition and calls the real overload.
    /// </summary>
    public static (WaveOutcome Outcome, List<BattleEvent> Events) ResolveWave(
        Champion champ,
        Build build,
        Economy.Hunter hunter,
        float enemyHealth,
        float enemyDamage,
        int enemyIntervalMs,
        ExpeditionTuning tuning,
        Random rng,
        WaveBonus? bonus = null,
        Source? enemySource = null,
        bool isBoss = false,
        float enemyDefense = 0f)
        => ResolveWave(
            champ, build, hunter,
            new[] { WaveCreature.Single(enemyHealth, enemyDamage, enemyDefense, enemySource) },
            enemyIntervalMs, tuning, rng, bonus, isBoss);

    /// <summary>
    /// Resolve one wave against a composition. Terminates on a clear, a death, or the tick ceiling.
    /// </summary>
    public static (WaveOutcome Outcome, List<BattleEvent> Events) ResolveWave(
        Champion champ,
        Build build,
        Economy.Hunter hunter,
        IReadOnlyList<WaveCreature> creatures,
        int enemyIntervalMs,
        ExpeditionTuning tuning,
        Random rng,
        WaveBonus? bonus = null,
        bool isBoss = false,
        WaveMetrics? metrics = null,
        float sustain = 1f,
        Style? wardedStyle = null,
        bool entrenched = false,
        bool legionSplits = false)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        if (creatures.Count == 0) throw new ArgumentException("A wave needs at least one creature.", nameof(creatures));
        ArgumentNullException.ThrowIfNull(champ);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(hunter);
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(rng);

        // LEGION appends its splits into the CALLER'S list — the live path always passes a List — so
        // the expedition's LastWaveCreatures, and through it the screen's composition and the enemy
        // bar's total, include the brood. Every roster walk in here is index-based, so appending
        // mid-wave is safe. Only creatures minted WITH the wave split; a child does not re-split.
        var legionRoster = legionSplits ? creatures as List<WaveCreature> : null;
        var legionBrood = legionRoster is null ? null : new HashSet<WaveCreature>(creatures);

        var events = new List<BattleEvent>();
        var mods = build.Resolve(hunter);

        // THE SKILL TREE'S SHAPE. Everything below that reads `shape` is a node in MasteryCatalog; if a
        // field of it stops being read here, its nodes must be deleted, because a node that does nothing
        // is the failure that left the whole loot-rarity chain inert for the length of development.
        // The build's shape PLUS the worn weapon's favoured Forms (GearShape) — the one seam through
        // which gear reaches the per-Form multipliers, so a bow really is the archer's weapon.
        var shape = SkillShape.Combine(build.Shape, GearShape.Of(hunter));

        // The Vow context is a property of the BUILD, so it is built once and every skill of every tick
        // is judged against the same one. See DescribeBuild.
        var weaveCtx = DescribeBuild(build, hunter);

        // Per-wave state the shape's conditional nodes need. All of it is local, so nothing leaks into
        // the next wave — which matters most for SUNDER, whose armour strip is explicitly wave-scoped.
        var struckOnce = new HashSet<WaveCreature>();   // FOLLOW THROUGH / OPENER / ALPHA
        var entrenchedStruck = entrenched ? new HashSet<WaveCreature>() : null;   // ENTRENCHED — first hit each
        var assassinated = false;                       // ASSASSINATE — once per wave
        // SIGNATURE state. Wounds and bent armour are per-CREATURE (they die with the wave); the
        // Spirit prime and the Mind budget are per-champion moments inside it.
        var wounds = new Dictionary<WaveCreature, int>();       // BODY
        var armourBent = new Dictionary<WaveCreature, int>();   // MACHINE
        var spiritPrimed = false;                               // SPIRIT
        var mindExtendBudget = 0;                               // MIND — refilled when a MARK opens

        var biteFuel = 0;                               // PAYBACK — bites banked for the next skill
        var castRamp = 0;                               // RHYTHM — casts since the last bite
        var staggeredThisBite = false;                  // STAGGER — one push-back per bite
        var castOnce = new HashSet<int>();              // OPENING VOLLEY — skills that have cast this wave
        var triggers = build.Triggers(hunter);
        // CHARGE. Tracked only when a keystone reads or feeds the pool — an untracked pool cannot
        // rot into dormant state, and the HUD only shows what the events say.
        var chargeLive = triggers.Contains(BuildTrigger.Rend)
                         || triggers.Contains(BuildTrigger.Capacitor)
                         || triggers.Contains(BuildTrigger.Dynamo)
                         || triggers.Contains(BuildTrigger.Lodestone);
        var chargeCap = triggers.Contains(BuildTrigger.Capacitor) ? ChargeCapExtended : ChargeCap;
        var charge = 0;

        var skills = build.Skills;
        // DEEP (RESONANCE) — a point of resonance is worth more. Applied HERE, at the single place the
        // stat enters the fight, so it lifts every skill's base damage rather than one branch of it.
        var resonance = hunter.ValueOf(Economy.HunterStat.ResonanceAffinity)
                        * (1f + Math.Max(0f, shape.ResonanceWorth));

        // VENOM — how much of each skill hit lingers as poison. Zero unless the build carries the trigger
        // (VENOMANCER, or a Venom weapon); a worn enchant sets its own strength, a bare keystone the base.
        var venomFrac = triggers.Contains(BuildTrigger.Venom)
            ? Math.Max(VenomBasePoison,
                       Economy.Enchantments.MagnitudeOf(hunter.WornEnchantments, Economy.EnchantKind.Venom))
            : 0f;
        var poison = 0f;   // standing poison damage yet to bleed out

        // HARVEST reads its WORN magnitude now, like Venom does — so a rarer Harvest procs a spare core more
        // often, and the tooltip's rarity-scaled % is the truth instead of decoration (the sim used a flat 25%,
        // so a Legendary blurb promising 38% delivered 25%). A bare trigger with no worn enchant keeps a floor.
        var harvestChance = triggers.Contains(BuildTrigger.Harvest)
            ? Math.Max(0.10f, Economy.Enchantments.MagnitudeOf(hunter.WornEnchantments, Economy.EnchantKind.Harvest))
            : 0f;

        // THE KEYSTONE AND VOW COMBOS. Hoisted here like Venom and Harvest, and read below only INSIDE
        // the branch their partner opens, so an unpaired one contributes exactly nothing rather than a
        // little. Sharpening a keystone is the whole promise on the item's card; paying out without the
        // keystone would make it a flat damage roll wearing a combo's name, which is the trap this
        // project already walked into once with VENOMANCER.
        // ── EVERY WAVE OPENS WITH A BREATH. Cooldowns persist across waves (that is what makes a
        //    descent one continuous fight), so by each wave's first tick most skills had long
        //    recovered and the opening was every skill firing inside the first half-second —
        //    playtest: "skillerin cooldown'ları başlangıçta biraz daha yüksek olması lazım."
        //    The breath is a FLAT delay, deliberately not cooldown-proportional: a half-cooldown
        //    opening taxed slow, heavy builds hardest and broke both balance sweeps (Weight fell 1.41x
        //    behind Endure). A flat pause costs every build the same instant. PREPARATION
        //    (FreeOpeningCast) waives it — waiving openings is that node's entire identity. ──
        // (The ms-cooldown opening-pause writes that stood here were unreachable — every Active
        // in the catalogue counts BEATS, and a beat-counted skill never reads ReadyAt. The breath
        // itself lives on the beat clock: see nextBeat below. 2026-08-31 audit, dead path #8.)

                var fervour = Economy.Enchantments.MagnitudeOf(hunter.WornEnchantments, Economy.EnchantKind.Fervour);
        var bulwark = Economy.Enchantments.MagnitudeOf(hunter.WornEnchantments, Economy.EnchantKind.Bulwark);
        var reverb = Economy.Enchantments.MagnitudeOf(hunter.WornEnchantments, Economy.EnchantKind.Reverb);
        var tithe = Economy.Enchantments.MagnitudeOf(hunter.WornEnchantments, Economy.EnchantKind.Tithe);

        // TITHE counts DISTINCT Vows, the same way the FRAGILITY price does. A Vow is sworn, not
        // equipped: weaving one Vow onto all four skills is one promise kept, not four, and paying it
        // four times would make the tithe strongest on the build that committed least.
        var swornVows = tithe > 0f ? DistinctVows(build.Skills).Count() : 0;

        // CRIT × FOCUS, folded to a deterministic expected-value factor on SKILL damage (chance × extra),
        // so the sim stays reproducible — no rng draw, no crit-lottery variance to break a seeded test.
        // DEFENSE (including the worn charm's, itself long inert) mitigates each incoming bite below.
        var critChance = CritChance(hunter, shape);
        var critMult = CritMultiplier(hunter) + shape.BonusCritDamagePercent / 100f;   // MIND 3p
        var critFactor = 1f + critChance * (critMult - 1f);
        var defenseFactor = DefenseMitigationConstant / (DefenseMitigationConstant + hunter.Defense);

        // VOW COSTS. A Vow that grants power must charge for it, or it is a stat with good PR. FRAGILITY is
        // paid as extra damage taken — the product across every worn Vow that names a damage-taken price.
        // (RECKLESS OFFERING's price is health, charged once at champion mint via VowHealthMultiplier, so it
        // is deliberately NOT billed here — doing both would bill the same effective-HP twice.) The Vow's
        // BENEFIT is applied per-skill in VowFactor; this is the other half, which the solo model had lost
        // when the squad engine that used to charge it was retired.
        // ONCE PER VOW, not once per skill wearing it. A Vow is sworn, not equipped: FRAGILITY's card
        // says "+12.5% damage taken" and a build that wove it onto four skills was charged 1.125^4 =
        // +60%, because the price compounded per skill while the BENEFIT did not — a skill's Vow bonus
        // is a multiplier on that skill, so four skills at x1.33 is still x1.33 of damage, never x1.33^4.
        // The asymmetry made both static-cost Vows strictly negative to swear, which is what the balance
        // sweep found: FRAGILITY -1 depth, RECKLESS OFFERING -1 depth, while every demand Vow paid.
        var fragilityMult = 1f;
        foreach (var v in DistinctVows(skills))
            if (v.DamageTakenIncrease > 0f) fragilityMult *= 1f + v.DamageTakenIncrease;

        // TARGETING is "first alive in spawn order", deliberately, and overkill is discarded.
        //
        // Focusing the weakest would let a single-target build tidy up a Swarm efficiently, which is
        // exactly the pressure Swarm exists to apply. Spawn order is also predictable, which matters
        // when the player cannot watch and react — they must be able to reason about a composition
        // before they descend.
        var alive = creatures.Count;

        // ── WHAT THE TWELVE SKILLS' BASE LINES NEED, held for the wave. Each is zero unless a woven
        //    skill carries the matching dial, so a build without them pays nothing at all. ─────────
        //
        // WILT's attack break and MIRE's slow are held as factors rather than written into the
        // creatures: WaveCreature.Damage is init-only on purpose (only Defense is settable, for
        // SUNDER), and the enemy interval is a local. Keeping them here also means they cannot leak
        // into the next wave, which is the same reason creatures are minted per wave.
        var attackBreak = 0f;      // WILT — fraction of every enemy's bite removed, toward its floor
        var frontBreak = 0f;       // WILT/SHRIVEL — the same, but only on the creature in front
        var slowTicks = 0;         // MIRE/NUMB — how many times the field has deepened its slow
        var executes = 0;          // HAMMER/FINISH — its execute is once a wave; TWICE buys a second
        var secondBreak = 0f;      // DRAIN/HOLLOW — SHRIVEL reaching past the front enemy
        var bites = 0;             // SNARE/MESH and HARDEN — how many bites the trap has answered
        var breaks = new Dictionary<int, int>();   // HAMMER/PRESS — how deep each creature is broken
        // WHICH BITTEN REACTIONS ANSWER THIS BITE. Decided BEFORE the damage lands, so a trap that
        // stops a bite prevents it rather than refunding it afterwards — which is what lets a second
        // preventer see that the bite is already gone and keep its own charge. Hoisted out of the
        // tick loop: this is walked on every bite and a fresh list per bite is a per-frame allocation.
        var answering = new List<(int Index, float Reflect, bool Stops)>();

        // ── THE RULES THE REINFORCEMENTS TURN (see SkillRules). Every one is wave-local, and every
        //    one is read in exactly one place below. ────────────────────────────────────────────────
        var castCarry = 0f;        // BREAKTHROUGH / CLEAN CUT / — the carry belonging to the cast landing now
        var trailArmed = false;    // HAMMER/TRAIL — the next basic attack ignores defence, once
        var pinArmed = 0;          // HAMMER/INTERCEPT — a stun held back for the bite that is coming
        var ampHitsLeft = 0f;      // SIGN/SPEND — empowered hits still owed (fractional: see PERFECT CLAUSE)
        var ampFrontFull = 0f;     // SIGN/ANCHOR — the front enemy's own deeper mark
        var ampCritKeeps = false;  // SIGN/PERFECT CLAUSE
        var deadThisWave = 0;      // FIELD/REMNANT — enemies this wave has already lost

        // ── THE SIX SET LADDERS (ElementSets). One typed field per rule the design names — not a
        //    generic buff runtime, and not six engines: every rung below is written in the vocabulary
        //    the fight already speaks (overkill, shield, critical, healing, a kill, an activation). ──
        var mindFocus = 0f;          // MIND 4p — critical chance banked since the last certainty, in points
        var impactPending = false;   // BODY 5p — an Active has resolved; the next swing is an IMPACT
        var impactSwing = false;     // BODY 5p — true only while that swing is landing, so its overkill carries
        var resonanceRate = 0f;      // SPIRIT 4p — rate gained from the distinct skills used this wave
        var harmonyGiven = false;    // SPIRIT 5p — the charges are handed out once a wave
        var harmonyCharge = new bool[skills.Count];   // SPIRIT 5p — one per skill, spent by its next activation
        var activated = new HashSet<int>();           // SPIRIT — every skill that has ACTED this wave, cast or pulse

        // REMNANT — the mire keeps what it has drowned, and the survivors wade through it. Read once,
        // from whichever skill carries the rule, because it is a property of the field standing on the
        // ground rather than of any one cast.
        var weakenPerDead = 0f;
        var weakenCap = 0f;
        for (var k = 0; k < skills.Count; k++)
            if (skills[k].Def.Rule.WeakenPerDeadEnemy > 0f)
            {
                weakenPerDead = MathF.Max(weakenPerDead, skills[k].Def.Rule.WeakenPerDeadEnemy);
                weakenCap = MathF.Max(weakenCap, skills[k].Def.Rule.WeakenPerDeadCap);
            }
        var ampCountsHits = false; // SIGN/SPEND — is this window counted in hits rather than seconds?
        var steadyAmp = 0f;        // SIGN/STEADY — the swell it has built this wave
        // VOLLEY/TORRENT pays the standing bleed out faster and CARRION makes it linger; both are
        // read from the WOVEN SKILLS at the wave's start rather than set by the first kill, because
        // the pool bleeds from the first tick and a rate that arrives late is a rate that never
        // applied to most of it.
        var bleedRate = 1f;
        var bleedShed = VenomBleedPerHalfSecond;
        var bleedFromHits = 0f;
        // PURE (RESONANCE) — does every woven skill share one Source? Read once, at the wave's start.
        var oneSource = skills.Count > 0 && skills.All(x => x.Source == skills[0].Source);
        foreach (var wsk in skills)
        {
            if (wsk.Def.BleedRate > 1f) bleedRate = Math.Max(bleedRate, wsk.Def.BleedRate);
            if (wsk.Def.BleedCarriesWaves) bleedShed = VenomBleedPerHalfSecond * 0.45f;
            // FLIGHT, RUPTURE, EBB, ONSET — a bleed skill in the loadout makes the champion's CASTS
            // feed the pool. Gathered from the whole weave rather than read off the casting skill,
            // because two of the four sit on WEEP, and WEEP is a Reaction that never casts: read the
            // other way they were dials on a skill that could not turn them.
            bleedFromHits = Math.Max(bleedFromHits, wsk.Def.BleedFromHits);
        }
        // HAMMER/TRAIL and DRAIN/TRICKLE — the plain swing borrows a skill's rule. Read here, once,
        // for the same reason the bleed rate is: the swing fires from the very first beat, and a rule
        // gathered at the first CAST would miss every swing before it.
        var swingIgnoresArmour = false;
        var swingLifesteal = 0f;
        foreach (var wsk in skills)
        {
            if (wsk.Def.SwingIgnoresArmour) swingIgnoresArmour = true;
            swingLifesteal = Math.Max(swingLifesteal, wsk.Def.SwingLifesteal);
        }
        var ampBonus = 0f;         // SIGN — the amplifier's current depth
        var ampUntil = 0;          // SIGN — the absolute ms the window holds until. Wave-local.
        var markDeepen = 0f;       // SIGN/ETCH — how far it has deepened so far this wave
        var ampWholeWave = false;  // SIGN — CALL, STEADY and SPRAWL cover everything; BRAND the front
        var slowFactor = 0f;       // MIRE — fraction by which the enemy interval is stretched
        var takenSinceCast = new Dictionary<int, List<(int Ms, float Amount)>>();   // REPAY — the bites banked for slot i, stamped
        var since = champ.ElapsedMs;
        var healthAtStart = champ.Health;

        // THE HEAL BUDGET. Every in-wave heal — leech, FEEDBACK, the Transformation's own leech,
        // SIPHON, the Nature signature, SECOND WIND — passes through Heal() below, and Heal() spends
        // from this one budget. Wave-scoped on purpose: it is a ceiling per wave, and a champion
        // that carries a spent budget into the next wave would be a build punished for surviving.
        // The numbers are the INJECTED tuning's, so the balance probe can measure old against new.
        // SIPHON raises the ceiling as well as the leech — measured, the leech half alone was
        // dormant under any ceiling (see HealTuning).
        var heal = tuning.Heal;
        var healBudget = heal.BudgetFor(champ.MaxHealth, siphon: triggers.Contains(BuildTrigger.Siphon));
        // The wave's healing ceiling, widened by any skill that buys the room. Without this a bigger
        // pulse heal is unbuyable: WILT's 1% a pulse already fills the budget, so 1.5% healed exactly
        // as much and the reinforcement changed nothing a player could ever see.
        // NATURE 4p opens it too — the set and the skill each widen the same ceiling.
        var healRoomBonus = shape.HealCeilingBonus;
        for (var k = 0; k < skills.Count; k++)
            healRoomBonus = MathF.Max(healRoomBonus, skills[k].Def.Rule.HealCeilingBonus);
        if (healRoomBonus > 0f) healBudget = (long)MathF.Round(healBudget * (1f + healRoomBonus));
        long healedThisWave = 0;

        // ── SHIELD IS WAVE-LOCAL. Zero at the start of every wave, before any wave-start effect
        //    grants into it. Without the reset an idle run would accumulate a shield while nothing
        //    was happening, and standing still would be the strongest defensive play in the game.
        champ.ResetShield();

        // FOUNDATION — STEADY's swell does not start from nothing. Applied at the wave's start, after
        // the reset that clears everything else, so it is a standing start rather than a carried one.
        foreach (var wsk in skills)
            if (wsk.Def.Rule.AmplifyStartsPrimed && wsk.Def.AmplifyPerCast > 0f)
            {
                steadyAmp = wsk.Def.AmplifyCap > 0f
                    ? Math.Min(wsk.Def.AmplifyCap, wsk.Def.AmplifyPerCast)
                    : wsk.Def.AmplifyPerCast;
                ampBonus = steadyAmp;
                ampWholeWave = true;
                ampUntil = int.MaxValue / 4;
            }

        /// <summary>MACHINE 5p is once a wave and its charge is spent only on a bite that would land.</summary>
        var platingSpent = false;

        // Every producer goes through here so the cap is enforced once, the event reports what was
        // ACTUALLY added, and a grant at the ceiling says so instead of lying about its size.
        void GrantShield(float amount, int atMs)
        {
            var added = champ.GainShield(amount);
            if (added > 0f)
                events.Add(new BattleEvent(BattleEventKind.ShieldGained, 0, (int)MathF.Round(added), atMs));
        }

        // WAVE-START SHIELD — the MACHINE set's 3p rung and BANKED's CARRIED, granted AFTER the reset
        // so they are a fresh start rather than a carry-over.
        if (shape.WaveStartShieldFraction > 0f)
            GrantShield(champ.MaxHealth * shape.WaveStartShieldFraction, 0);
        foreach (var wsk in skills)
            if (wsk.Def.WaveStartShieldFraction > 0f)
                GrantShield(champ.MaxHealth * wsk.Def.WaveStartShieldFraction, 0);
        if (metrics is not null) metrics.CreaturesPresent = creatures.Count;
        // THE BEAT. Actions happen on it and only on it; the first beat of a wave is its breath.
        var beatLen = BeatFor(mods.SkillRate * shape.SkillRate, tuning.BeatMs);
        var nextBeat = Math.Min(beatLen, tuning.WaveOpeningMs);   // the wave's breath, then the metronome

        // THIS BEAT HAS NOT BEEN COUNTED YET.
        //
        // Champion.BeatCount is what every rhythm cooldown is measured against, so it has to mean
        // "actions the champion has taken" — and it did not. The counter was advanced at the FOOT of
        // the tick, below six `return Kill(ms)` sites, and every wave ends on one of them: the blow
        // that kills the last creature returned before the counter moved. One action per wave, every
        // wave, never counted.
        //
        // The cost was cumulative and invisible. A skill that cast just before a boundary needed FIVE
        // real actions to come round instead of four, because one of them never reached the counter —
        // which is exactly the "bazen 3 vuruştan, bazen 4 vuruştan sonra" the playtest reported, and
        // why it read as random: INSIDE a wave the cadence was always exactly right.
        var beatOwed = false;
        var nextBite = enemyIntervalMs;

        // THE LIVE SKILL RATE. The build's rate, then RHYTHM (faster per cast since the last
        // bite) — read at every cast and swing, so a bite changes the NEXT cooldown, not the wave's.
        float RateNow() => mods.SkillRate * shape.SkillRate
                           * (1f + shape.CastRampPerCast * castRamp)
                           * (1f + resonanceRate);   // SPIRIT 4p RESONANCE

        (WaveOutcome, List<BattleEvent>) Finish(WaveOutcome o, int atMs)
        {
            champ.ElapsedMs += atMs;
            if (metrics is not null)
            {
                metrics.DurationMs = atMs;
                metrics.HealthLost = Math.Max(0, healthAtStart - champ.Health);
            }
            return (o, events);
        }

        (WaveOutcome, List<BattleEvent>) Kill(int atMs)
        {
            // THE KILLING BLOW WAS AN ACTION. Settle the beat before leaving, or the champion's own
            // counter forgets the swing that ended the wave — see beatOwed.
            if (beatOwed) { champ.BeatCount++; beatOwed = false; events.Add(new BattleEvent(BattleEventKind.Beat, 0, champ.BeatCount, atMs)); }

            // No EnemyDown here — LandOn emits one per creature as it falls, so this would double the
            // last one and the screen would remove a sprite that was already gone.

            // SECOND WIND — healed on the CLEAR, so it is a reward for finishing rather than a trickle.
            if (shape.HealOnClear > 0f)
                Heal((int)MathF.Round(champ.MaxHealth * shape.HealOnClear), atMs);

            if (harvestChance > 0f && rng.NextDouble() < harvestChance) bonus?.AddCores(1);
            // LODESTONE — the holder's reward, deterministic: a FULL pool carried to the clear is a
            // spare core. The rhythm argument with REND is settled here, at the only moment that counts.
            if (chargeLive && triggers.Contains(BuildTrigger.Lodestone) && charge >= chargeCap)
                bonus?.AddCores(1);
            if (triggers.Contains(BuildTrigger.Splinter)) bonus?.AddQuality(0.15f);   // richer loot on a kill (see the blurb)

            return Finish(WaveOutcome.Cleared, atMs);
        }

        // How much a hit is worth right now: build mods, AFFINITY, the Source matchup, BLOODLUST, and MARK.
        float Amp(int absMs, Source? skillSource, SkillDef? skillDef = null, WaveCreature? against = null)
        {
            var m = mods.Damage;

            // AFFINITY — the Nen hexagon. A skill in your affinity's Form hits far harder than one in its
            // opposite. Auto-attacks pass no Form and are unaffected.
            if (build.Affinity is { } aff && skillDef is { } d0)
            {
                var factor = StyleAffinity.Factor(aff, d0.Style);
                // BROAD — the far side of the hexagon is walked back toward parity. Only ever LIFTS a
                // factor under 1, so it cannot turn a penalty into a bonus.
                if (shape.OppositePenaltyRelief > 0f && factor < 1f)
                    factor += (1f - factor) * Math.Min(1f, shape.OppositePenaltyRelief);
                // NARROW — the opposite trade: your own Form harder, every other one softer.
                if (aff == d0.Style) factor *= 1f + shape.AffinityStyleBonus;
                else factor *= Math.Max(0f, 1f - shape.OffStylePenalty);
                m *= factor;
            }

            if (against?.Source is { } target && skillSource is { } s)
            {
                // CHORD reads every matchup as strong; KEYED deepens a strong one; DISCORD refunds a
                // weak one. All three turn the SAME number, so a build carrying two of them cannot
                // stack the same promise twice.
                var match = shape.AllMatchupsStrong
                    ? SourceMatchup.Strong
                    : SourceMatchup.Effectiveness(s, target);
                if (match > 1f) match += (match - 1f) * shape.StrongMatchupBonus;
                else if (match < 1f) match += (1f - match) * Math.Min(1f, shape.WeakMatchupRelief);
                m *= match;
            }

            // PURE — the reward for refusing the matchup game entirely. Read from the woven skills, so a
            // build that swaps one slot to another Source loses it the moment it does.
            if (shape.OneSourceBonus > 0f && oneSource) m *= 1f + shape.OneSourceBonus;

            // BLOODLUST — damage scales with health MISSING. The keystone that rewards the edge.
            if (triggers.Contains(BuildTrigger.Bloodlust))
            {
                var missing = 1f - champ.Health / (float)Math.Max(1, champ.MaxHealth);
                m *= 1f + (BloodlustScale + fervour) * missing;   // FERVOUR steepens it; dead alone
            }

            // ZEAL — the mirror: damage scales with health PRESENT. JUGGERNAUT's reward for staying whole,
            // and the natural partner of the UNBROKEN vow. It denies healing (the keystone grants NoHealing
            // too), so the bonus fades as you are chipped and can never be bought back — a front-loaded
            // fortress, not a sustain build.
            if (triggers.Contains(BuildTrigger.Zeal))
            {
                var present = champ.Health / (float)Math.Max(1, champ.MaxHealth);
                m *= 1f + (ZealScale + bulwark) * present;        // BULWARK steepens it; dead alone
            }

            // REVERB — ECHO trades power for a second cast (60% damage, twice). This buys some of that
            // price back, and only while ECHO is socketed.
            if (reverb > 0f && triggers.Contains(BuildTrigger.Echo)) m *= 1f + reverb;

            // TITHE — every Vow sworn pays. Dead on a build that has promised nothing, and worth most to
            // the player who has given up the most, which is the same bargain the Vows themselves make.
            if (swornVows > 0) m *= 1f + tithe * swornVows;

            // THE AMPLIFY WINDOW — SIGN's identity as ONE state: a depth, a reach, a clock.
            // The Form era ran TWO amplifiers here — a flat x1.6 whenever the window was open, and
            // the dials' own depth on top — so SPEND measured x4.8 where its card said x3.0, and a
            // depth written by STEADY outlived its wave. One wave-local state now; the base line's
            // own AmplifyPercent (0.60 on CALL, 0.70 on BRAND) carries what the constant used to.
            if (ampUntil > absMs && ampBonus > 0f)
            {
                var front = against is not null && ReferenceEquals(against, FirstAlive());
                var amped = ampWholeWave || front;
                // SPEND — the window empowers a COUNT of damaging hits rather than a stretch of
                // seconds. An idle player does not choose when anything fires, so a two-second window
                // was a lottery ticket; a number of hits is a promise the build can be built around.
                if (amped && ampHitsLeft > 0f)
                {
                    // PERFECT CLAUSE, under this sim's crit model. Crit here is an EXPECTED VALUE
                    // (see critFactor) rather than a rolled event, so "a critical hit does not spend a
                    // charge" cannot be a branch — it is the expectation of one: the charge is spent
                    // at the rate a hit is NOT a crit. Deterministic, and it makes crit chance feed
                    // SPEND, which is the interaction the reinforcement is for.
                    ampHitsLeft -= ampCritKeeps ? MathF.Max(0.05f, 1f - critChance) : 1f;
                    if (ampHitsLeft <= 0f) ampUntil = 0;   // spent: the window closes on the count
                }
                else if (amped && ampHitsLeft <= 0f && ampCountsHits)
                {
                    amped = false;   // a counted window with nothing left empowers nothing
                }
                if (amped)
                {
                    // ANCHOR — the spread keeps the wave, and the front enemy takes the full mark.
                    var depth = (front && ampFrontFull > 0f ? ampFrontFull : ampBonus) + shape.AmplifyPowerBonus;
                    // A SIGN adept's attunement lands on the amplifier's DEPTH — the one lever that
                    // scales with the whole build rather than with one skill (measured when the old
                    // Mark-specialist node was a -23% trap; see the port note in StyleAffinity).
                    if (build.Affinity is { } signAff) depth *= StyleAffinity.Factor(signAff, Style.Sign);
                    m *= 1f + depth;
                }
            }

            // ── THE SKILL TREE. Everything past here is gated on skillForm, so the background
            //    auto-attack never triggers a node — it is a trickle, not a build. ────────────────────
            if (skillDef is null) return m;

            m *= shape.HitSize * shape.DamageDealt;

            // The worn weapon family's favoured styles (and any other per-style favour). Behind
            // the same skillDef gate as everything else here, so it lifts woven skills and never
            // the background auto-attack.
            m *= shape.StylePowerFor(skillDef.Style);

            // HOARDER — the AVARICE terminal. Every point of haul the path bought becomes force, so a
            // Greed/Fortune build finally has a reason to exist in a fight instead of only in the bag.
            // A fifth of the haul multiplier, so a doubled haul is +20% hit size — enough to matter,
            // far short of making the economy path the strongest damage road in the game.
            if (triggers.Contains(BuildTrigger.Hoarder))
                m *= 1f + HoarderHaulToForce * MathF.Max(0f, mods.Haul - 1f);
            if (shape.HitSizePerMaxHealth > 0f) m *= 1f + shape.HitSizePerMaxHealth * champ.MaxHealth;   // ANCHOR
            if (shape.DamagePerMaxHealth > 0f) m *= 1f + shape.DamagePerMaxHealth * champ.MaxHealth;     // BASTION

            if (against is not null)
            {
                // FOLLOW THROUGH / OPENER / ALPHA — the first hit a creature takes, and the price ALPHA
                // charges on every later one.
                m *= struckOnce.Contains(against) ? shape.LaterHitMultiplier : shape.FirstHitMultiplier;

                // CULL — finishing damage. Reads the creature in front, so in a multi-creature wave it
                // fires on whichever is hurt rather than on the wave as a whole.
                if (shape.CullBonus > 0f && against.Health < against.MaxHealth * shape.CullThreshold)
                    m *= 1f + shape.CullBonus;

            }

            // SWARMBANE — the more of them there are, the harder you hit. The mirror of what a Swarm
            // band does to a single-target build.
            if (shape.PerCreatureBonus > 0f) m *= 1f + shape.PerCreatureBonus * alive;

            // FIRST STRIKE — the opening seconds are everything, and everything after is nothing.
            if (shape.OpeningSeconds > 0f)
                m *= absMs - since < shape.OpeningSeconds * 1000f
                    ? 1f + shape.OpeningBonus
                    : 1f - shape.AfterOpeningPenalty;

            // INTERRUPT. There is no windup model; the enemy bites on a clock, so the last quarter of
            // the gap before the NEXT bite is the windup, and damage landed there is what interrupting
            // means here.
            //
            // IT READS `nextBite`, NOT A MODULO. This was `(absMs - since) % enemyIntervalMs >=
            // enemyIntervalMs * 3 / 4` — the model the bite clock itself abandoned twenty lines below,
            // where the note explains that an exact modulo only fires where the interval divides a
            // multiple of the 100ms tick. The bite loop was corrected to ACCUMULATE and this read of it
            // was left behind, so INTERRUPT was timing its bonus against a clock the enemy no longer
            // keeps — and MIRE's slow, which stretches `nextBite` and not the interval, pulled the two
            // further apart the more a build invested in it. Found 2026-08-30 when the mastery liveness
            // probe was widened past RESONANCE: taking INTERRUPT changed nothing at all.
            if (shape.InterruptBonus > 0f
                && absMs - since > 0
                && absMs - since >= nextBite - enemyIntervalMs / 4)
                m *= 1f + shape.InterruptBonus;

            return m;
        }

        /// <summary>The first living creature in spawn order, or null when the wave is clear.</summary>
        WaveCreature? FirstAlive()
        {
            for (var i = 0; i < creatures.Count; i++)
                if (creatures[i].Alive) return creatures[i];
            return null;
        }

        /// <summary>The one behind the front. DRAIN/HOLLOW is the only thing that asks.</summary>
        WaveCreature? SecondAlive()
        {
            var seen = 0;
            for (var i = 0; i < creatures.Count; i++)
                if (creatures[i].Alive && ++seen == 2) return creatures[i];
            return null;
        }

        int IndexOf(WaveCreature c)
        {
            for (var i = 0; i < creatures.Count; i++)
                if (ReferenceEquals(creatures[i], c)) return i;
            return 0;
        }

        // ── The hit-side signatures, shared by every skill-hit path (main spread, CHAIN/RICOCHET
        //    extra hit) so the wound bonus really does pay EVERY skill hit, as its comment claims. ──
        float SignatureAmp(float hit, WaveCreature c, Source? sig)
        {
            if (sig is null) return hit;
            if (wounds.TryGetValue(c, out var w) && w > 0)
                hit *= 1f + SignatureWoundPerStack * w;
            if (sig == Source.Shadow && c.Health < c.MaxHealth * SignatureShadowThreshold)
                hit *= 1f + SignatureShadowBonus;
            return hit;
        }

        void SignatureLay(WaveCreature c, Source? sig)
        {
            if (sig == Source.Body)
                wounds[c] = Math.Min(SignatureWoundMaxStacks, wounds.GetValueOrDefault(c) + 1);
            if (sig == Source.Machine && c.Defense > 0f
                && armourBent.GetValueOrDefault(c) < SignatureMachineStripCap)
            {
                c.Defense = MathF.Max(0f, c.Defense - SignatureMachineStrip);
                armourBent[c] = armourBent.GetValueOrDefault(c) + 1;
            }
        }

        // `swing` is PROVENANCE — was this the champion's basic attack — and it is what the event carries.
        // `fromSkill` stays what it always was: does this hit obey the SKILL rules (crit, venom, the
        // hit-size nodes). The two are not the same, and conflating them told the screen that VENOM's
        // poison bleed, THORNS' reflect, BREAKER's overkill spill and ASSASSINATE's execution were all
        // basic attacks — so the champion's swing clip committed to a poison tick every 500 ms and the
        // cooldown dial ran four times fast (review 2026-08-30).
        void LandOn(WaveCreature? target, float dmg, int atMs, bool fromSkill = false, bool ignoresArmour = false,
                    bool swing = false)
        {
            if (target is null || !target.Alive) return;

            // CRIT lands on SKILL hits only — the idle auto-swing and the poison bleed never crit (both
            // call this with fromSkill:false). Applied first, so a crit stings with more poison too.
            if (fromSkill)
            {
                // MIND 4p FOCUS and 5p CERTAINTY. A critical here is an EXPECTED VALUE, not a rolled
                // event (see critFactor), so "a hit that does not crit" is not a branch — it is a
                // SHARE of every hit, and that share is exactly what FOCUS banks. A build already
                // critting often climbs slowly, which is the reset clause the design wrote, expressed
                // as the expectation it actually has. CERTAINTY is the one hit that is not an
                // expectation at all: at the ceiling the next one crits outright and the climb restarts,
                // and because it empties FOCUS it cannot chain into itself.
                if (shape.FocusPerHitPercent > 0f)
                {
                    var chance = Math.Min(MaxCritChance, critChance + mindFocus / 100f);
                    if (shape.CertaintyAtFocusCap && mindFocus >= shape.FocusCapPercent)
                    {
                        chance = 1f;
                        mindFocus = 0f;
                    }
                    else
                    {
                        mindFocus = MathF.Min(shape.FocusCapPercent,
                                              mindFocus + shape.FocusPerHitPercent * (1f - critChance));
                    }
                    dmg *= 1f + chance * (critMult - 1f);
                }
                else dmg *= critFactor;
            }

            // VENOM poisons on SKILL hits only (the blurb says "SKILLS POISON") — never on the auto-attack,
            // and never on the poison's own bleed, or it would feed itself. Fed from the hit's RAW force,
            // before armour: poison is a fraction of how hard you swung, not of how much got through,
            // otherwise armour would shrink the pool AND the bleed and "poison answers armour" would be
            // false twice over.
            if (fromSkill && venomFrac > 0f) poison += dmg * venomFrac;

            // ENEMY ARMOUR is flat and per-hit (see MinHitFraction), so it reads hit SIZE. Poison bypasses
            // it entirely — a bleed tick is small by construction and flat armour would erase it, which
            // would leave the Venom path with nothing to be good at.
            if (metrics is not null) metrics.RawDamage += dmg;

            // ENTRENCHED — the first hit on each creature deals a quarter. ANY first hit: the swing, a
            // cast, even the poison's first tick. Dug-in is dug-in — it punishes openers by name, which
            // is the affix's whole pressure (TEMPO, and Alpha/First Strike specifically).
            if (entrenchedStruck is not null && entrenchedStruck.Add(target))
                dmg *= EntrenchedFirstHitFactor;

            var raw = dmg;

            // ARMOUR IS FLAT AND SIGNED. It used to be guarded on `Defense > 0f`, and under that guard
            // a creature broken PAST zero was identical to one broken exactly to zero — which made
            // PRESS's "-25", CRUSHING's "-50" and SETTLE's "-90" three ways of writing the same
            // promise, three floors no player could ever tell apart. The same subtraction run with a
            // negative number is the reading those three lines already assume: broken armour does not
            // merely stop mitigating, it starts helping. Found by ReinforcementLivenessTests, which is
            // what it is for.
            if (!ignoresArmour && target.Defense != 0f)
            {
                var armour = target.Defense;

                // SHARPENED / EXECUTIONER cut flat armour.
                if (fromSkill && armour > 0f)
                    armour = MathF.Max(0f, armour - shape.ArmourPenetration);

                if (armour > 0f) dmg = MathF.Max(dmg * MinHitFraction, dmg - armour);
                // BROKEN PAST ZERO. Bounded by the hit itself, so a deep break can at most double a
                // blow rather than turning every stray tick into a hit the break's depth is worth.
                else if (armour < 0f) dmg += MathF.Min(-armour, dmg);
            }

            if (metrics is not null)
            {
                metrics.DeliveredDamage += dmg;
                metrics.Hits++;
            }

            // Overkill is DISCARDED rather than carried to the next creature. A 110 Trap hit into a
            // 30-health swarm creature wastes 80, and that waste is the whole cost of bringing a
            // large-hit build to a Swarm band.
            target.Health -= dmg;
            if (fromSkill) struckOnce.Add(target);

            if (metrics is not null && target.Health <= 0f) metrics.CreaturesKilled++;
            var idx = IndexOf(target);
            events.Add(new BattleEvent(BattleEventKind.Strike, idx, (int)MathF.Round(dmg), atMs, FromSkill: !swing));
            if (!target.Alive)
            {
                alive--;
                // One EnemyDown per CREATURE, not per wave. The screen needs to know which sprite to
                // remove; the wave-cleared signal is the outcome, not this event.
                events.Add(new BattleEvent(BattleEventKind.EnemyDown, idx, 0, atMs));

                // LEGION — the creature splits once on death: two half-strength copies join the wave.
                // Only originals split (children carry no brood mark), and the roster never grows past
                // LegionMaxCreatures — the design's cap, so a Swarm band cannot soft-lock.
                if (legionRoster is not null && legionBrood!.Remove(target))
                    for (var twin = 0; twin < 2 && legionRoster.Count < LegionMaxCreatures; twin++)
                    {
                        legionRoster.Add(new WaveCreature
                        {
                            MaxHealth = MathF.Max(1f, target.MaxHealth * LegionChildFactor),
                            Health = MathF.Max(1f, target.MaxHealth * LegionChildFactor),
                            Damage = target.Damage * LegionChildFactor,
                            Defense = target.Defense, Source = target.Source, Archetype = target.Archetype,
                        });
                        alive++;
                    }

                // WEEP — a kill leaves bleed on the wave, worth a share of what died. Read here
                // rather than in the skill loop so ANY kill pays: a cast, the plain swing, a carried
                // overkill or the bleed itself. It feeds the standing poison pool, which already
                // bleeds into the front of the wave every half second — one pool, not a second
                // parallel system, because two decaying pools would be two rules for one idea.
                deadThisWave++;
                // SHADOW 3p SHADE — every death, from any source, leaves one. Held on the champion
                // rather than in the wave, because the design lets a shade wait for the next one.
                if (shape.ShadeMax > 0 && champ.Shades < shape.ShadeMax) champ.Shades++;
                for (var k = 0; k < skills.Count; k++)
                {
                    var wd = skills[k].Def;
                    if (wd.BleedOnKillFraction > 0f)
                    {
                        // FLOOD — a wave already bleeding takes a further kill harder. Bounded: a flat
                        // extra share of the ORDINARY kill contribution, never a share of the standing
                        // pool, so it adds and never compounds.
                        var onKill = target.MaxHealth * wd.BleedOnKillFraction;
                        if (wd.Rule.ExtraBleedWhileBleeding > 0f && poison > 0f)
                            onKill *= 1f + wd.Rule.ExtraBleedWhileBleeding;
                        poison += onKill;
                    }
                }

                // LOOSE AGAIN — every skill is ready the instant something dies. Clearing the whole
                // table rather than one entry is deliberate: the card says "the NEXT shot", and which
                // skill that is depends on what the rotation reaches first.
                if (triggers.Contains(BuildTrigger.LooseAgain))
                    // READY, not FORGOTTEN. Clearing the tables left every slot with no entry — which is
                    // "ready forever" on the time side and "ready on the wave's first beat" on the beat
                    // side, and it also wiped the wave-opening breath. Zero means ready NOW and is a real
                    // entry, so the next cast writes an honest cooldown over it.
                    for (var k = 0; k < skills.Count; k++) { champ.ReadyAt[k] = 0; champ.ReadyAtBeat[k] = 0; }

                // MOMENTUM — every kill takes time off every cooldown. It edits the entries that EXIST
                // and adds none: a skill with no ReadyAt entry is already ready, and writing one would
                // turn a refund into a delay. Any kill counts — skill, poison bleed, carried overkill —
                // because the card says EVERY KILL, and this is read here rather than in the skill loop
                // so a kill by the auto-swing or a BREAKER spill pays the same as a kill by a cast.
                // BREAKER — half of the overkill carries on. The design named a part-break bonus here;
                // the sim has no part-break model, and this answers the same complaint from inside the
                // model that exists, because discarded overkill IS the tax a large-hit build pays in a
                // Swarm band. One level only: a chain of carries would let one hit clear a whole wave.
                // BREAKTHROUGH and CLEAN CUT — the SKILL's own carry, beside the build's. Taken as the
                // larger of the two rather than the sum: two rules that both answer "wasted overkill"
                // should not multiply into a chain that clears a wave from one blow.
                var spill = -target.Health;
                var carry = MathF.Max(shape.OverkillCarry, castCarry);
                // BODY 5p — an IMPACT is the one basic attack whose waste carries. Everywhere else the
                // carry is a direct-SKILL rule, which is what keeps a bleed or a thorn from feeding it.
                if ((fromSkill || impactSwing) && carry > 0f && spill > 0f && FirstAlive() is { } next)
                    // fromSkill:false on the carried hit is what bounds it — a carry cannot carry again.
                    LandOn(next, spill * carry, atMs, fromSkill: false, ignoresArmour: true);
            }
        }

        /// <summary>
        /// Land one activation across up to <paramref name="targets"/> living creatures. Returns the raw
        /// total dealt, which is what leech reads.
        /// </summary>
        float LandSpread(float raw, int atMs, int targets, Source? skillSource, SkillDef? skillDef, int absMs,
                         bool fromSkill = true, bool swing = false, bool countsAsActivation = true,
                         bool ignoresArmour = false, int hitsPerTarget = 1)
        {
            if (targets <= 0) return 0f;
            // An AURA tick is not an activation: counting one every 500 ms made the run report's REACH
            // read 4.6 creatures per cast for a build whose casts reach 1.6 (review 2026-08-30).
            if (fromSkill && countsAsActivation && metrics is not null) metrics.Activations++;

            // PAYBACK — bites banked, spent whole on the next skill activation, every hit of it.
            // Raw is scaled before the per-target loop so every hit carries the same bank.
            if (fromSkill && biteFuel > 0) { raw *= 1f + shape.BiteFuelBonus * biteFuel; biteFuel = 0; }

            // WARDED — the wave resists the style that hurt it most LAST wave (SoloExpedition keeps the
            // ledger and passes it). Previous wave on purpose: the player can see it coming, and an
            // affix that reacted to the wave in progress would be unanswerable in a game with no
            // in-run decisions. It reads the SKILL's style, so the plain swing is never warded.
            if (fromSkill && wardedStyle is { } ws && skillDef?.Style == ws) raw *= WardedResistFactor;

            // CLEANUP and PUNCH THROUGH — hits owed to an enemy that died before they landed. Bounded
            // by construction: `pending` only ever falls, and a hit that lands is never re-owed.
            var retarget = skillDef?.Rule.RetargetOnDeath == true;
            var pending = 0;

            // CLEANUP — the finishing bonus, read once per activation.
            var underShare = skillDef?.Rule.BonusUnderHealth ?? 0f;
            var underBonus = skillDef?.Rule.BonusUnderHealthAmount ?? 0f;

            var dealt = 0f;
            var struck = 0;
            for (var i = 0; i < creatures.Count && (struck < targets || pending > 0); i++)
            {
                var c = creatures[i];
                if (!c.Alive) continue;
                // HITS PER TARGET (CLUSTER's five arrows): each lands separately, re-amplified —
                // so the first arrow gets the first-hit rule and armour taxes every one, which is
                // the whole trade against one big blow. Amp stays per TARGET per HIT: the Source
                // matchup belongs to the creature being hit.
                var want = Math.Max(1, hitsPerTarget) + pending;
                pending = 0;
                var landed = 0;
                for (var h = 0; h < want && c.Alive; h++)
                {
                    landed++;
                    var hit = raw * Amp(absMs, skillSource, skillDef, c);
                    // CLEANUP — read per HIT, not per target, so with CLUSTER's five arrows the ones
                    // that land after the target crosses the line are the ones that get the bonus.
                    if (underShare > 0f && c.MaxHealth > 0f && c.Health < c.MaxHealth * underShare)
                        hit *= 1f + underBonus;

                    // ── SIGNATURES, hit-side. The WOUND bonus reads stacks laid by ANY Body skill
                    //    and pays EVERY skill hit; laying happens after the landing, so a hit never
                    //    feeds itself. ──
                    if (fromSkill) hit = SignatureAmp(hit, c, skillSource);
                    LandOn(c, hit, atMs, fromSkill, swing: swing, ignoresArmour: ignoresArmour);
                    if (fromSkill) SignatureLay(c, skillSource);
                    dealt += hit;
                }
                // The corpse's share moves on rather than vanishing — which is the whole purchase.
                if (retarget && landed < want) pending = want - landed;
                struck++;
                if (fromSkill && countsAsActivation && metrics is not null) metrics.TargetsStruck++;
            }

            if (fromSkill && skillDef is { } sd && metrics is not null)
            {
                // The per-style ledger WARDED reads next wave: damage, and casts for its tie-break.
                metrics.StyleDamage[sd.Style] = metrics.StyleDamage.GetValueOrDefault(sd.Style) + dealt;
                if (countsAsActivation)
                    metrics.StyleActivations[sd.Style] = metrics.StyleActivations.GetValueOrDefault(sd.Style) + 1;
            }

            if (!fromSkill) return dealt;

            // FEEDBACK — the Spread/Endure bridge. Sustain that scales with how WIDE you are, which is
            // the only way the two branches have of paying each other.
            if (shape.HealPerTargetStruck > 0f && struck > 0)
                Heal((int)MathF.Round(champ.MaxHealth * shape.HealPerTargetStruck * struck), atMs);

            return dealt;
        }

        // `underCeiling: false` is VITALITY's regeneration — a trained stat, not a build's heal, so the
        // per-wave ceiling on skill healing (HealTuning) does not spend on it; BLOOD MAGIC, the band's
        // sustain and the pool's edge still apply. Everything else counts.
        void Heal(int amount, int atMs, bool underCeiling = true)
        {
            // BLOOD MAGIC. The cost is total: it does not reduce healing, it removes it.
            if (triggers.Contains(BuildTrigger.NoHealing)) return;

            // THE BAND'S SUSTAIN MULTIPLIER, applied at the one funnel every heal already passes through
            // — leech, HEAL PER TARGET, SECOND WIND's on-clear heal, all four call sites. Bands.Endless
            // ("leech and regeneration halved") computed its 0.5 and had NO CALLER anywhere in the
            // solution, so the affix that exists to pressure ENDURE did nothing to it whatever.
            // Deliberately NOT applied to FullHealBetweenWaves: that is a reset, not regeneration, and
            // halving a binary is a design change rather than a repair.
            // NATURE 3p — legitimate healing only, and never applied to a shield: the shield below is
            // computed from what this heal COULD NOT USE, so multiplying it here would pay twice.
            amount = (int)MathF.Round(amount * sustain * shape.HealingMultiplier);
            if (amount <= 0) return;

            // THE CEILING (HealTuning.MaxHealFractionPerWave). Charged against what actually LANDS:
            // the heal is first trimmed to the room left in the pool, then to the room left in the
            // budget, and only the part that reaches the champion is spent. Overhealing at full
            // health costs nothing, so a build is not taxed for being healthy; and a heal that
            // arrives after the budget is gone is a Heal event of 0 — i.e. no event at all — which
            // is what the HUD should show, because nothing happened.
            // LONG-CLAMPED: an int.MaxValue-health fixture champion plus any heal wrapped negative
            // and died OF HEALING the moment the NATURE signature made Heal reachable from every
            // build. Sum in long, clamp, then narrow.
            var room = Math.Max(0L, (long)champ.MaxHealth - champ.Health);
            var landed = Math.Min((long)amount, room);
            // NATURE 5p OVERGROWTH — healing wasted AT FULL HEALTH becomes shield, half of it. Only
            // the part the pool had no room for: healing the wave's ceiling refused was never eligible,
            // and turning that into shield would be a way around the ceiling rather than a use for the
            // overflow. Shield is not healing — it spends no budget, and nothing that reads Health
            // reads it — so there is no path back into this function and no recursion.
            if (shape.OverhealToShield > 0f && amount > room)
                GrantShield((amount - room) * shape.OverhealToShield, atMs);

            if (underCeiling) landed = Math.Min(landed, healBudget - healedThisWave);
            if (landed <= 0) return;
            if (underCeiling) healedThisWave += landed;
            champ.Health = (int)(champ.Health + landed);
            events.Add(new BattleEvent(BattleEventKind.Heal, 0, (int)landed, atMs));
        }

        for (var ms = tuning.TickMs; ms <= tuning.TickCeilingMs; ms += tuning.TickMs)
        {
            var abs = since + ms;
            // ON THE BEAT the champion takes ONE action: the first ready skill in slot order, else a swing.
            var onBeat = champ.Alive && ms >= nextBeat;
            if (onBeat) beatOwed = true;
            var acted = false;

            // ── VITALITY: life regained every second, a fraction of the pool (Hunter.RegenPerSecond).
            //    Through Heal() like every other heal, so BLOOD MAGIC's "no healing" and the band's
            //    sustain multiplier apply to it — a trained stat is not a way around a keystone's price —
            //    but OUTSIDE the per-wave heal ceiling, which is a budget on a build's skills. Never on
            //    a corpse. ──
            if (ms % RegenTickMs == 0 && champ.Alive && hunter.RegenPerSecond > 0f && champ.Health < champ.MaxHealth)
                // `ms`, NOT `abs`: events are stamped wave-relative, and the replay walks them in order —
                // a run-cumulative stamp here froze the replay from wave two on (review 2026-08-26).
                Heal(Math.Max(1, (int)MathF.Round(champ.MaxHealth * hunter.RegenPerSecond)), ms, underCeiling: false);

            // ── MENDING: the tree's own trickle, a fraction of the pool a second. A BUILD's heal, so it
            //    spends the per-wave ceiling like LEECH — the trained stat above is the one exception. ──
            if (ms % RegenTickMs == 0 && champ.Alive && shape.RegenFraction > 0f && champ.Health < champ.MaxHealth)
                Heal(Math.Max(1, (int)MathF.Round(champ.MaxHealth * shape.RegenFraction)), ms);

            // ── VENOM bleeds first, so poison from earlier ticks can finish an enemy before it swings. ──
            if (poison > 0.5f && ms % 500 == 0)
            {
                var bite = poison * bleedShed * bleedRate;
                poison -= bite;
                // Poison bleeds into the front of the wave. It is a single pool, not per creature — a
                // build that poisons then watches its target die keeps the standing damage.
                LandOn(FirstAlive(), bite, ms, ignoresArmour: true);   // not fromSkill — must not re-poison
                if (alive == 0) return Kill(ms);
            }

            // ── SKILLS ────────────────────────────────────────────────────────────────────────
            for (var i = 0; i < skills.Count; i++)
            {
                var sk = skills[i];

                // THE THREE-WAY FORK, asked about the SKILL instead of about the Form. The shape of
                // this loop is unchanged — a Reaction is answered at the enemy's swing, a Field ticks
                // on its own clock, an Active takes the beat — but the question is now the skill's,
                // which is what lets one style be woven either way. (Rework stage 3.)
                var kind = sk.Def.Kind;

                // A REACTION fires on its event, not here.
                if (kind == SkillKind.Reaction) continue;

                if (kind == SkillKind.Field)
                {
                    // AURA: always on, no cooldown. Ticks steadily for the whole fight. RADIANCE (an item
                    // enchantment) ticks it faster — same damage per tick, so more ticks is more DPS, and
                    // that item is worth nothing to a build that isn't running Aura.
                    //
                    // THE SKILL'S OWN CLOCK, at last. This read FormBehaviour.AuraTickMs for every Field
                    // alike, so SkillDef.IntervalMs — declared per skill, validated by
                    // test_a_skill_carries_the_timing_its_kind_needs, printed on the weave screen — was
                    // decoration: PRESS and BRAND both say "every 2s" and both ticked every 1s, and the
                    // six reinforcements that buy a faster clock bought nothing at all. Found by
                    // ReinforcementLivenessTests 2026-08-30. The constant stays as the fallback for a
                    // skill with no clock of its own (a spilled active resolves to one that has).
                    var ownTick = sk.Def.IntervalMs > 0 ? sk.Def.IntervalMs : 1_000;
                    var auraTick = triggers.Contains(BuildTrigger.Radiance) ? ownTick * 3 / 5 : ownTick;
                    // IN ARREARS, NOT IN ADVANCE. `ms` starts at 0 and 0 is a multiple of everything, so
                    // the wave's very first instant used to pay a whole interval of a field that had
                    // been up for no time at all. That was worth half a second at a 500 ms tick and
                    // became a whole second at 1000 ms (2026-08-28), which is the entire reason a purely
                    // cosmetic pacing change moved depth and income: an Aura front-loading twice as hard
                    // kills the opening creatures sooner, and everything downstream is measured off when
                    // things die. A field pays for time it has ALREADY been up, so the first pulse lands
                    // one interval in — which is also what "ticks every second" means to a player.
                    if (ms == 0 || ms % auraTick != 0) continue;
                    // The tick announces itself, so the screen never has to infer one from a timestamp.
                    // The tick announces WHICH SLOT ticked; the screen resolves art and name
                    // from the equipped skill rather than decoding a Form ordinal.
                    events.Add(new BattleEvent(BattleEventKind.Aura, i, 0, ms));
                    // SPIRIT counts a pulse as this skill acting. A passive that never "casts" would
                    // otherwise make HARMONY unreachable for every build that runs one — which is most
                    // of them, since two of the four slots are passive.
                    activated.Add(i);

                    // A FIELD IS NOT ALWAYS A DAMAGE FIELD. Kind says WHEN a skill acts and Effect says
                    // WHAT it does, and they are separate axes on purpose — so a Field that amplifies
                    // (SIGN's BRAND) is a setting of two existing boxes rather than a new system.
                    //
                    // This branch exists because the slot split made it REACHABLE: a build saved with
                    // four beat-taking skills spills its overflow into the passive slots, and if that
                    // overflow is a MARK it arrives here. Without this it would fall through to the
                    // damage path, where BaseDamage(Mark) is zero by definition — a skill the player
                    // can see in a slot, doing nothing at all, which is this codebase's signature
                    // failure wearing yet another hat.
                    // ── PRESS, MIRE and WILT: a Field that breaks, slows or wears down rather
                    //    than dealing. Each reads its own dial off the catalogue, so a skill without
                    //    one falls straight through to the damage path below. ─────────────────────
                    var def = sk.Def;

                    if (def.StunMs > 0)
                    {
                        // INTERCEPT — a stun spent just after a bite is very nearly wasted, and one
                        // spent just before it takes the whole interval. The machine HOLDS its stun for
                        // the bite that is coming rather than firing on its own clock. Deterministic:
                        // it reads the wave's own bite time, which is the only attack clock this model
                        // has (there are no per-enemy timers to prioritise between).
                        if (def.Rule.StunTimedToBite)
                        {
                            pinArmed = def.StunMs;
                        }
                        // PRESS / PIN — the weight stops breaking and starts holding. The wave's next
                        // bite is pushed back, which is HAMMER's owned stun; the same push STAGGER
                        // uses, so a build carrying both cannot stack them into immunity.
                        else if (!staggeredThisBite)
                        {
                            nextBite += def.StunMs;
                            staggeredThisBite = true;
                        }
                        // NO UNCONDITIONAL `continue`. PIN is the one variation that stuns INSTEAD of
                        // breaking, so the stun used to end the tick outright — and BUCKLE, whose whole
                        // purchase is "each stun strips 10 defence", was read one branch too late to
                        // exist. A stun that also carries a break falls through to it.
                        if (def.DefenceBreakPerTick <= 0f) continue;
                    }

                    if (def.DefenceBreakPerTick > 0f)
                    {
                        // PRESS — the weight on the front enemy. Defense is settable for exactly this
                        // kind of lasting strip (see WaveCreature.Defense), and creatures are minted
                        // per wave so it cannot carry into the next one.
                        // SEIZE widens it past the front one; the base line is 1 + 0.
                        var want = 1 + def.TargetsBonus;
                        for (var ci = 0; ci < creatures.Count && want > 0; ci++)
                        {
                            var c = creatures[ci];
                            if (!c.Alive) continue;
                            var before = c.Defense;
                            c.Defense = Math.Max(def.DefenceBreakFloor, c.Defense - def.DefenceBreakPerTick);
                            // PUBLISHED, so the fight can SHOW it. Only when the number actually moved:
                            // once a creature is at the floor the break is doing nothing, and a badge
                            // that keeps counting past the floor would be lying about the last tick.
                            if (c.Defense < before)
                                events.Add(new BattleEvent(BattleEventKind.Break, ci,
                                                          breaks[ci] = breaks.GetValueOrDefault(ci) + 1, ms));
                            want--;
                        }
                        continue;
                    }

                    if (def.SlowFraction > 0f || def.SlowPerEnemy > 0f)
                        // MIRE — read at the bite, where the interval is decided. Set rather than
                        // accumulated: the base line is a flat slow, and its variations deepen it.
                        //
                        // NO `continue`. MIRE also DEALS, falling through to the damage path below,
                        // and the reason is FIELD's own sentence: "you hit the whole wave, all the
                        // time". A passive that only slows contradicts the style it belongs to — and
                        // it is the skill a saved AURA migrates onto, so without this every existing
                        // aura build would silently lose all of its damage and the RADIANCE
                        // enchantment would have nothing left to speed up.
                    {
                        var want = def.SlowFraction;
                        // NUMB deepens with time; TEEMING deepens with the crowd. Both are capped by
                        // the skill's own ceiling, so neither can stop the wave attacking altogether.
                        if (def.SlowDeepenPerTick > 0f) want += def.SlowDeepenPerTick * (slowTicks + 1);
                        if (def.SlowPerEnemy > 0f) want += def.SlowPerEnemy * alive;
                        if (def.SlowCeiling > 0f) want = Math.Min(want, def.SlowCeiling);
                        // The grip HOLDS. A mire does not let go of a wave because part of it died,
                        // and — measured — a grip that tracked the survivors was no grip at all: a
                        // swarm is gone in two seconds, so for almost the whole wave it read one
                        // enemy, and CLOG's 10% of one enemy and TEEMING's 6% of one enemy rounded to
                        // the same millisecond on the bite clock.
                        slowFactor = Math.Max(slowFactor, want);
                        slowTicks++;
                    }

                    if (def.AttackBreakPerTick > 0f)
                    {
                        // WILT — every enemy bites softer, to a floor. Held as a factor because
                        // WaveCreature.Damage is init-only. SHRIVEL narrows it to the front enemy and
                        // goes far deeper, which is the trade: one creature nearly silenced against
                        // the whole wave taken down a step.
                        if (def.FrontEnemyOnly)
                        {
                            frontBreak = Math.Max(def.AttackBreakFloor, frontBreak - def.AttackBreakPerTick);
                            // HOLLOW — the same break on the one behind it, at its own fraction, and
                            // floored the same way so two reinforcements cannot silence a wave.
                            if (def.BreakSecondEnemy > 0f)
                                secondBreak = Math.Max(def.AttackBreakFloor,
                                                       secondBreak - def.AttackBreakPerTick * def.BreakSecondEnemy);
                        }
                        else
                            attackBreak = Math.Max(def.AttackBreakFloor, attackBreak - def.AttackBreakPerTick);

                        // SUP — the pulse also returns a share of the champion's pool. Through Heal so
                        // the per-wave healing ceiling and BLOOD MAGIC's refusal both still apply.
                        if (def.HealPerPulse > 0f)
                            Heal((int)MathF.Round(champ.MaxHealth * def.HealPerPulse), ms);
                        continue;
                    }

                    if (def.Effect == SkillEffect.Amplify)
                    {
                        // The standing mark: a window refreshed on the field's own clock instead of
                        // opened by a cast. It costs no beat, so it is deliberately the SHORTER window —
                        // it holds until the next tick rather than for a cast's full duration.
                        var standing = (int)(auraTick * shape.AmplifyWindowMultiplier);
                        if (triggers.Contains(BuildTrigger.Linger)) standing = standing * 9 / 5;
                        ampUntil = Math.Max(ampUntil, abs + standing);

                        // HOW DEEP the mark is. ETCH deepens it every tick to its own ceiling; SPRAWL
                        // trades depth for reach and the whole wave carries it. Held as a bonus over
                        // the Form's flat MarkMultiplier so a skill with no amplify dials at all is
                        // exactly what it was.
                        // ANCHOR — the spread keeps the wave and the front enemy takes the full mark.
                        ampFrontFull = def.Rule.AmplifyFrontFull;
                        var depth = def.AmplifyPercent;
                        if (def.AmplifyDeepenPerTick > 0f)
                        {
                            markDeepen = def.AmplifyDeepenCap > 0f
                                ? Math.Min(def.AmplifyDeepenCap, markDeepen + def.AmplifyDeepenPerTick)
                                : markDeepen + def.AmplifyDeepenPerTick;
                            depth += markDeepen;
                        }
                        ampBonus = depth;
                        ampWholeWave = def.AmplifyWholeWave;
                        continue;
                    }

                    // THE SKILL'S OWN NUMBER, in its own units: a Field's BasePower is per
                    // second, and a tick pays its interval's share — so a faster clock (REMNANT)
                    // buys cadence for the slow, never free damage, and RADIANCE still buys full
                    // ticks faster because its acceleration touches the SCHEDULE, not this line.
                    var aura = SkillCatalogue.PoweredBase(def, resonance)
                               * VowFactor(sk, weaveCtx, shape)
                               * (def.IntervalMs > 0 ? def.IntervalMs / 1000f : 1f)
                               * def.DamageMultiplier;   // MIRE/SILT
                    var auraDealt = LandSpread(aura, ms, shape.TargetsFor(def) + def.TargetsBonus,
                                               sk.Source, def, abs, countsAsActivation: false);
                    // NATURE'S SIGNATURE follows the DAMAGE, not the cast — an Aura that deals must heal.
                    if (sk.Source == Source.Nature && auraDealt > 0f)
                        Heal((int)MathF.Round(auraDealt * heal.NatureSignatureLeech), ms);
                    if (alive == 0) return Kill(ms);
                    continue;
                }

                // A CAST IS A BEAT'S ACTION — never between beats, never two in one. A ready skill that
                // loses the beat to an earlier slot stays ready (nothing is written) and takes the next.
                if (!onBeat || acted) continue;
                // THE SKILL'S OWN CADENCE. FormBehaviour.CooldownBeats answered per FORM, so every
                // skill of a style shared one number and a variation could never change it. The
                // catalogue owns it now (design §5), which is also what makes Build.BeatDemand tell
                // the truth — it reads the same field, and a readout that disagrees with the fight is
                // the failure this codebase keeps paying for.
                var beats = sk.Def.Beats;
                // CROWDED — FIELD buys its cadence with FIELD's own condition rather than with an
                // unconditional beat off the top. An unconditional cut spends the beat budget the
                // basic attack needs, and BODY's own set capstone is built on that swing.
                if (beats > 1 && sk.Def.CrowdedBeats > 0 && alive >= sk.Def.CrowdedBeats) beats--;
                if (beats > 0)
                {
                    // COUNTED IN BEATS: "every fourth action". PREPARATION waives the opening wait.
                    //
                    // THE OPENING IS THE RULE, NOT AN EXCEPTION TO IT. This used to be
                    // `Math.Min(1, beats - 1)` — a skill with no entry yet could fire from the run's
                    // SECOND action, whatever its cooldown said. It is only reachable on the first wave
                    // of a run, which is the wave every player sees most often, and it is what the
                    // playtest hit: "öldükten sonra 2. vuruşta skill'i attı." A rail that promises EVERY
                    // FOURTH ACTION and then opens on the second is not describing a rule.
                    //
                    // `beats - 1` places the first cast ON the Nth action, which is where every later
                    // cast lands too: the cycle is cast, hit, hit, hit, cast — three plain hits — so a
                    // run that opens with three hits and then casts is the same cycle, joined at the
                    // start. The old note (a fresh champion "dead by wave 3" under a full opening) was
                    // measured against a wave a third of today's length; re-measured, the pacing band
                    // does not move. PREPARATION still takes the very first beat.
                    var openingBeat = shape.FreeOpeningCast ? 0 : beats - 1;
                    if (champ.BeatCount < champ.ReadyAtBeat.GetValueOrDefault(i, openingBeat)) continue;
                    champ.ReadyAtBeat[i] = champ.BeatCount + beats;
                }
                else
                {
                    // Unreachable for the current catalogue — every Active counts beats — and kept
                    // shut rather than kept dead (2026-08-31 audit, dead path #8). A future
                    // time-counted Active gets its clock back deliberately, with a test.
                    continue;
                }
                acted = true;
                // BODY 5p MOMENTUM — a skill has been thrown, and the body follows it. One pending at a
                // time by construction: it is a flag, so a second cast before the swing re-arms rather
                // than stacking.
                if (shape.ImpactSwingBonus > 0f) impactPending = true;

                // RHYTHM counts every cast, a MARK included; OPENING VOLLEY remembers which skills have
                // cast this wave. Both decided here, before the Form branches, so no path forgets them.
                if (castRamp < shape.CastRampMax) castRamp++;
                var firstCast = castOnce.Add(i);
                activated.Add(i);

                // SPIRIT 4p RESONANCE — a rate bonus the first time each DISTINCT skill acts, to its
                // ceiling. Read by RateNow, so it moves the beat for the rest of the wave.
                if (firstCast && shape.ResonanceRatePerSkill > 0f)
                    resonanceRate = MathF.Min(shape.ResonanceRateCap, resonanceRate + shape.ResonanceRatePerSkill);

                // SPIRIT 5p HARMONY — all four slots filled, and every one of them has acted. The
                // four-slot requirement is the design's, and it is a REAL conflict with a Vow that asks
                // for an empty slot rather than a branch that excuses one. Handed out once a wave, one
                // charge each, spent by that skill's next activation.
                if (!harmonyGiven && shape.HarmonyCharges
                    && skills.Count >= 4 && activated.Count >= skills.Count)
                {
                    harmonyGiven = true;
                    for (var h = 0; h < harmonyCharge.Length; h++) harmonyCharge[h] = true;
                }

                if (sk.Def.Effect == SkillEffect.Amplify)
                {
                    // A SIGN cast deals nothing: it opens the window its OWN dials describe. The
                    // Form constants (a flat 1.6x, a 6,000 ms window) are gone — CALL's base line
                    // carries them as AmplifyPercent/AmplifyMs now, so a variation replaces the
                    // base instead of stacking on a hidden one.
                    var window = sk.Def.AmplifyMs > 0 ? sk.Def.AmplifyMs : 6_000;
                    if (triggers.Contains(BuildTrigger.Linger)) window = window * 9 / 5;
                    window = (int)(window * shape.AmplifyWindowMultiplier);   // MARK MASTERY

                    if (sk.Def.AmplifyPerCast > 0f)
                    {
                        // STEADY — a build-wide swell that holds for the REST OF THE WAVE. Bounded
                        // by the tick ceiling rather than an int.MaxValue sentinel: the sentinel
                        // overflowed the clock the moment a MIND cast stretched the window, and the
                        // champion's amplifier flipped negative for the rest of the run.
                        steadyAmp = sk.Def.AmplifyCap > 0f
                            ? Math.Min(sk.Def.AmplifyCap, steadyAmp + sk.Def.AmplifyPerCast)
                            : steadyAmp + sk.Def.AmplifyPerCast;
                        ampBonus = steadyAmp;
                        ampWholeWave = true;
                        window = tuning.TickCeilingMs;
                    }
                    else if (sk.Def.AmplifyPercent > 0f)
                    {
                        ampBonus = sk.Def.AmplifyPercent;
                        ampWholeWave = sk.Def.AmplifyWholeWave;
                    }

                    // SPEND — the window is spent in hits. Refilled on every cast, never stacked.
                    ampHitsLeft = sk.Def.Rule.AmplifyHits;
                    ampCountsHits = sk.Def.Rule.AmplifyHits > 0;
                    ampCritKeeps = sk.Def.Rule.CritKeepsAmplifyCharge;

                    // SPIRIT 3p / 5p — the opening strength reaches the BONUS half of an amplify and
                    // nothing else about it: not the window, which is a duration, and not the count.
                    var signMagnitude = castOnce.Contains(i) && firstCast ? shape.FirstActivationMagnitude : 1f;
                    if (harmonyCharge[i])
                    {
                        harmonyCharge[i] = false;
                        signMagnitude *= shape.FirstActivationMagnitude;
                    }
                    ampBonus *= signMagnitude;

                    ampUntil = abs + window;
                    mindExtendBudget = SignatureMindExtendCapMs;   // MIND's signature stretches THIS window
                    // A SIGN cast is a real CAST — cooldown, its own Skill event — so it stores
                    // CHARGE like any other cast, and a SPIRIT one primes the next other-Source cast.
                    if (sk.Source == Source.Spirit) spiritPrimed = true;
                    if (chargeLive && charge < chargeCap)
                        events.Add(new BattleEvent(BattleEventKind.Charge, 0, ++charge, ms));
                    events.Add(new BattleEvent(BattleEventKind.Skill, i, 0, ms));
                    continue;
                }

                // ECHO fires the whole thing twice; OVERDRAW adds a cast to PROJECTILE only. Both are
                // build commitments — Echo is a keystone you socketed, Overdraw is an item that is dead
                // unless this skill is a Projectile.
                var casts = triggers.Contains(BuildTrigger.Echo) ? 2 : 1;
                if (sk.Def.Style == Style.Volley && triggers.Contains(BuildTrigger.Overdraw)) casts += 1;
                // ONE EVENT PER ACTIVATION, and it comes first. ECHO doubles the loop and OVERDRAW adds a
                // third pass for a Projectile, and each pass used to announce itself — so the screen
                // started fx_projectile, sfx_cast and the callout two or three times on the same frame,
                // which is what "the projectile effect does not play properly" was (review 2026-08-30).
                // Emitting it above the loop also puts it ahead of ASSASSINATE's execution strike, which
                // used to land BEFORE its own cast's event and read as a basic attack.
                events.Add(new BattleEvent(BattleEventKind.Skill, i, 0, ms));
                for (var c = 0; c < casts; c++)
                {
                    // REPAY is the one active whose size is not its Form's base value: it deals a
                    // multiple of what the champion has taken since it last fired, which is SNARE's
                    // whole sentence said as an action. The bank is spent here and cleared, so two
                    // casts never pay for the same bite twice.
                    float raw;
                    if (sk.Def.PaysBackDamageTaken > 0f)
                    {
                        // Only what the WINDOW still counts — VENGEANCE's "last 3s" is a real price
                        // now, not a card decoration. The bank clears either way: two casts never
                        // pay for the same bite twice.
                        var owed = 0f;
                        if (takenSinceCast.TryGetValue(i, out var bank))
                        {
                            foreach (var (bMs, amount) in bank)
                                if (sk.Def.PaybackWindowMs <= 0 || ms - bMs <= sk.Def.PaybackWindowMs)
                                    owed += amount;
                            bank.Clear();
                        }
                        raw = owed * sk.Def.PaysBackDamageTaken;
                        // SCARRED — hurt, it hits back harder. The threshold reads the POOL: a shield
                        // standing in front of a wounded hunter does not make them well.
                        if (sk.Def.Rule.PaybackBelowHealth > 0f && champ.MaxHealth > 0
                            && champ.Health < champ.MaxHealth * sk.Def.Rule.PaybackBelowHealth)
                            raw *= 1f + sk.Def.Rule.PaybackBelowHealthBonus;
                    }
                    else
                    {
                        raw = SkillCatalogue.PoweredBase(sk.Def, resonance);
                    }
                    raw *= VowFactor(sk, weaveCtx, shape);

                    // OPENING VOLLEY — this skill's first activation of the wave, or one of the later ones.
                    raw *= firstCast ? shape.FirstCastMultiplier : shape.LaterCastMultiplier;

                    // SPIRIT 3p and 5p — the opening strength, and the charge that gives it once more.
                    // Deliberately NARROW: it multiplies direct damage here, direct healing and a
                    // shield grant where those are produced, and the bonus half of an amplify in the
                    // Sign branch. It never touches a stun length, a slow ceiling or a break floor,
                    // because "20% stronger" has no honest meaning for those.
                    var magnitude = firstCast ? shape.FirstActivationMagnitude : 1f;
                    if (harmonyCharge[i])
                    {
                        harmonyCharge[i] = false;
                        magnitude *= shape.FirstActivationMagnitude;
                    }
                    raw *= magnitude;

                    // SHADOW 3p — a damaging Active spends one death to make the next one bigger. Only
                    // a DAMAGING one: this branch is the damage path, so a Sign never eats a shade.
                    var spentShade = false;
                    if (shape.ShadeActiveBonus > 0f && champ.Shades > 0)
                    {
                        champ.Shades--;
                        spentShade = true;
                        raw *= 1f + shape.ShadeActiveBonus;
                    }

                    // THE NEN BUY-BACK: a sworn Vow pulls an off-discipline skill one ring toward the
                    // build's affinity (restriction buys power — the rule the Vows were born from).
                    // Applied as a RATIO over Amp's base affinity factor, so it composes with every
                    // later multiplication and can never double-apply.
                    if (build.Affinity is { } sworn && sk.Vow is not null)
                        raw *= StyleAffinity.Factor(sworn, sk.Def.Style, vowSworn: true)
                               / StyleAffinity.Factor(sworn, sk.Def.Style);

                    // SPIRIT'S SIGNATURE, consumed: a Spirit cast primes the NEXT cast of any other
                    // Source. The conductor raises the orchestra, not itself.
                    if (spiritPrimed && sk.Source != Source.Spirit)
                    {
                        raw *= 1f + SignatureSpiritBonus;
                        spiritPrimed = false;
                    }

                    // REND — a Strike spends the whole CHARGE pool. The pool is what the PREVIOUS
                    // casts stored; the Strike's own point lands after it resolves, so a dump can
                    // never feed itself. c == 0: one dump per activation SET — without it, Echo's
                    // second activation drained the point the first had just stored, a permanent
                    // trivial +5% that broke this very invariant.
                    if (chargeLive && charge > 0 && sk.Def.Style == Style.Hammer && c == 0
                        && triggers.Contains(BuildTrigger.Rend))
                    {
                        raw *= 1f + ChargeRendPerPoint * charge;
                        charge = 0;
                        events.Add(new BattleEvent(BattleEventKind.Charge, 0, 0, ms));
                    }

                    // EXECUTE — a STRIKE finishes a weakened enemy. Reads the CURRENT target's own health
                    // fraction, so in a multi-creature wave it fires on whichever creature is in front and
                    // hurt, not on the wave as a whole. Dead without a Strike, and strongest on bosses —
                    // trash is dead before it reaches the threshold.
                    if (sk.Def.Style == Style.Hammer && triggers.Contains(BuildTrigger.Execute)
                        && FirstAlive() is { } victim
                        && victim.Health < victim.MaxHealth * ExecuteThreshold)
                        raw *= ExecuteMultiplier;

                    // ASSASSINATE — once a wave, a weakened creature simply dies. Deliberately not a
                    // damage bonus: against a Bruiser with a large pool, "kill it outright" and "hit it
                    // hard" are different promises, and only the first one answers a Caster band.
                    if (!assassinated && shape.AssassinateThreshold > 0f
                        && FirstAlive() is { } mark
                        && mark.Health < mark.MaxHealth * shape.AssassinateThreshold)
                    {
                        assassinated = true;
                        LandOn(mark, mark.Health, ms, fromSkill: false, ignoresArmour: true);
                        if (alive == 0) return Kill(ms);
                    }

                    // ── WHAT THE VARIATIONS TURN ON THE CAST. Each is zero at the base line, so a
                    //    skill whose variation is unchosen lands exactly as it always did. ─────────
                    var vdef = sk.Def;

                    // ── AND WHAT THE REINFORCEMENTS TURN ON IT. Each is neutral at the base line
                    //    (a multiplier of 1, a bonus of 0), so an unbought reinforcement is invisible.
                    raw *= vdef.DamageMultiplier;

                    // GLUT — DRAIN trades its lifesteal for damage that rises with the health it holds.
                    if (vdef.DamagePerHealth > 0f && champ.MaxHealth > 0)
                    {
                        // HEALTH, never shield: GLUT pays for being biologically well, and a shield in
                        // front of a half-empty pool is not wellness.
                        var share = champ.Health / (float)champ.MaxHealth;
                        var scale = vdef.DamagePerHealth;
                        // RIPE — kept near full, the scaling reaches half again as far.
                        if (vdef.Rule.HealthScalingAbove > 0f && share >= vdef.Rule.HealthScalingAbove)
                            scale *= 1f + vdef.Rule.HealthScalingAboveBonus;
                        raw *= 1f + scale * share;
                    }

                    // THRONG — FIELD's pulse pays for the crowd it lands in.
                    if (vdef.DamagePerLivingEnemy > 0f) raw *= 1f + vdef.DamagePerLivingEnemy * alive;

                    // SPLAY — "an arrow at every enemy, and never fewer than five". Against a small
                    // wave the surplus arrows double up rather than being discarded, which is the
                    // half of that sentence LandSpread cannot express: its loop stops at the last
                    // living creature and throws the rest away.
                    if (vdef.MinimumHits > 0 && alive > 0 && alive < vdef.MinimumHits)
                        raw *= vdef.MinimumHits / (float)alive;

                    // SHARE — one pool split between the living, and never more ways than its own cap.
                    // The trade THRONG refuses: the same total however many are standing.
                    var spreadTargets = shape.TargetsFor(vdef, vdef.Targets) + vdef.TargetsBonus;
                    if (vdef.SplitPool > 0f)
                    {
                        var ways = Math.Max(1, Math.Min(alive, vdef.SplitMaxWays > 0 ? vdef.SplitMaxWays : alive));
                        // BALANCE — a share poured into an almost-dead enemy is a share wasted, so the
                        // pool is measured against the enemies that can absorb it. Deterministic: the
                        // count of enemies holding more than an even share of the pool, in wave order,
                        // never a search. A pool split fewer ways is a bigger share each.
                        if (vdef.Rule.SplitByHealth && ways > 1)
                        {
                            var even = raw * vdef.SplitPool / ways;
                            var worth = 0;
                            for (var ci = 0; ci < creatures.Count; ci++)
                                if (creatures[ci].Alive && creatures[ci].Health >= even) worth++;
                            // Nobody left standing can absorb a full share — so the pool stops being
                            // a pool and goes into one enemy. That is the case this reinforcement is
                            // FOR: a thinned wave is exactly where an even split wastes the most.
                            ways = worth > 0 ? Math.Min(ways, worth) : 1;
                        }
                        raw = raw * vdef.SplitPool / ways;
                        spreadTargets = ways;
                    }

                    // FINISH — HAMMER's execute. Once a wave, and outright rather than as a bonus:
                    // against a Bruiser "kill it" and "hit it hard" are different promises.
                    if (executes < vdef.ExecutesPerWave && vdef.ExecuteFraction > 0f && FirstAlive() is { } weak
                        && weak.Health < weak.MaxHealth * vdef.ExecuteFraction)
                    {
                        executes++;
                        // CLEAN CUT — the excess an execute throws away is the branch's own waste, so
                        // it carries. Landed with the cast's carry armed, and disarmed straight after.
                        castCarry = vdef.Rule.OverkillCarry;
                        LandOn(weak, weak.Health, ms, fromSkill: true, ignoresArmour: true);
                        castCarry = 0f;
                        if (alive == 0) return Kill(ms);
                    }

                    // BANKED — SNARE keeps what it was owed instead of spending it. Through the same
                    // one Shield the rest of the game uses: same cap, same wave reset, same bar.
                    if (vdef.ShieldInsteadOfDamage)
                    {
                        GrantShield(raw, ms);
                        continue;
                    }

                    // FLATTEN — the blow ignores defence entirely.
                    // BREAKTHROUGH — the carry belongs to THIS cast while it lands, and to nothing
                    // else: set here, cleared immediately after, so a thorn or a bleed never inherits it.
                    castCarry = vdef.Rule.OverkillCarry;
                    var dealt = LandSpread(raw, ms, spreadTargets, sk.Source, vdef, abs,
                                           ignoresArmour: vdef.DefenceIgnore,
                                           hitsPerTarget: vdef.HitsPerTarget);
                    castCarry = 0f;

                    // SHADOW 5p AFTERIMAGE — the blow falls a second time, half as hard, across the same
                    // enemies. It is NOT a cast: it takes no beat, starts no cooldown, counts as no
                    // activation (so it is not skill-progression use and stores no CHARGE), spends no
                    // shade and creates none, and it re-lands DAMAGE only — never the skill's stateful
                    // logic, so REPAY's bank cannot be spent twice by it. It cannot make another
                    // afterimage because nothing here reads spentShade again.
                    if (spentShade && shape.AfterimageFraction > 0f && dealt > 0f && alive > 0)
                    {
                        dealt += LandSpread(raw * shape.AfterimageFraction, ms, spreadTargets,
                                            sk.Source, vdef, abs, countsAsActivation: false,
                                            ignoresArmour: vdef.DefenceIgnore,
                                            hitsPerTarget: vdef.HitsPerTarget);
                        if (alive == 0) return Kill(ms);
                    }

                    // TRAIL — the blow leaves the hunter's arm moving: the NEXT basic attack borrows
                    // its rule. One use, armed here and spent by the swing.
                    if (vdef.Rule.TrailNextSwing) trailArmed = true;

                    // FLIGHT, RUPTURE, EBB, ONSET — the cast leaves bleed behind it. Fed from the RAW
                    // force for the same reason VENOM is: armour must not shrink the pool AND the bleed.
                    if (bleedFromHits > 0f) poison += raw * bleedFromHits;

                    // DRINK and THIRST — DRAIN gives back its OWN dial's share of what it dealt,
                    // through Heal, so the per-wave ceiling and BLOOD MAGIC's refusal still hold.
                    // SIPHON sharpens the dial here, and stays dead without a DRAIN skill. (The Form
                    // path used to heal TransformationLeech for ANY Transformation-Form skill,
                    // dial or no dial — which made GLUT's "No lifesteal" a lie and hid half of
                    // THIRST's arithmetic in a constant.)
                    if (vdef.Lifesteal > 0f && dealt > 0f)
                    {
                        var leech = vdef.Lifesteal;
                        if (sk.Def.Style == Style.Drain && triggers.Contains(BuildTrigger.Siphon))
                            leech *= heal.SiphonMultiplier;
                        Heal((int)MathF.Round(dealt * leech), ms);
                    }

                    // WEAVER — the ARTIFICE terminal. The cast ALSO lands as the next Form in the loadout,
                    // with that Form's own base damage, target count and Source matchup. One slot answers
                    // two of the content's four demands, which nothing else in the game does: a Strike
                    // followed by an Aura is a Weight hit and a Spread hit from one activation.
                    //
                    // Skipped for Mark (which deals nothing and only opens a window) and for Trap (which
                    // fires on being bitten, not on a cooldown) — weaving into either would be a silent
                    // no-op or a second Trap that ignored its own rule.
                    if (triggers.Contains(BuildTrigger.Weaver) && skills.Count > 1)
                    {
                        var wovenIdx = (i + 1) % skills.Count;
                        var woven = skills[wovenIdx];
                        // Echo only what CAN echo: a Reaction answers a bite, an amplifier deals
                        // nothing, and a zero-power base line (PRESS, WILT, REPAY) has no number
                        // to fire at a fraction of.
                        if (woven.Def is { Kind: not SkillKind.Reaction, Effect: not SkillEffect.Amplify, BasePower: > 0f })
                        {
                            var wovenRaw = SkillCatalogue.PoweredBase(woven.Def, resonance)
                                           * VowFactor(woven, weaveCtx, shape)
                                           * WeaverEchoFraction;
                            // The woven echo carries ITS OWN skill's buy-back, same ratio rule as above.
                            if (build.Affinity is { } wovenAff && woven.Vow is not null)
                                wovenRaw *= StyleAffinity.Factor(wovenAff, woven.Def.Style, vowSworn: true)
                                            / StyleAffinity.Factor(wovenAff, woven.Def.Style);
                            events.Add(new BattleEvent(BattleEventKind.Skill, wovenIdx, 0, ms));
                            dealt += LandSpread(wovenRaw, ms, shape.TargetsFor(woven.Def),
                                                woven.Source, woven.Def, abs);
                            if (alive == 0) return Kill(ms);
                        }
                    }

                    // ── SIGNATURES, cast-side. ────────────────────────────────────────────────
                    // MIND stretches an open MARK window — a Mind rotation keeps the amplifier lit
                    // longer than the window's own clock would allow, budgeted so it cannot become
                    // a permanent mark.
                    if (sk.Source == Source.Mind && ampUntil > abs && mindExtendBudget > 0)
                    {
                        var stretch = Math.Min(SignatureMindExtendMs, mindExtendBudget);
                        ampUntil += stretch;
                        mindExtendBudget -= stretch;
                    }
                    // NATURE heals a sliver of the total dealt. On a Transformation it stacks with
                    // the Form's own leech ON PURPOSE — Nature + Transformation IS the lifedrain
                    // build, and the two reading the same total keeps them honest with each other.
                    if (sk.Source == Source.Nature && dealt > 0f)
                        Heal((int)MathF.Round(dealt * heal.NatureSignatureLeech), ms);
                    // SPIRIT primes the next other-Source cast (consumed at the raw, above).
                    if (sk.Source == Source.Spirit) spiritPrimed = true;
                    // CHARGE — the cast itself stores a point. One per ACTIVATION: the Weaver echo
                    // and Echo's second cast ride their activation rather than stacking the pool.
                    if (chargeLive && c == 0 && charge < chargeCap)
                        events.Add(new BattleEvent(BattleEventKind.Charge, 0, ++charge, ms));

                    if (alive == 0) return Kill(ms);
                }
            }

            // ── THE BASIC ATTACK: the beat's action when no skill took it. MIGHT's hit. ─────────────
            if (onBeat && !acted && tuning.AutoAttackDamage > 0f)
            {
                // BODY 5p MOMENTUM — the IMPACT. Harder, and the only basic attack whose overkill
                // carries; the flag is cleared whether or not the set is worn, so nothing can hold one.
                var impact = impactPending && shape.ImpactSwingBonus > 0f;
                impactPending = false;
                impactSwing = impact;
                if (impact) castCarry = 1f;
                var swung = LandSpread(tuning.AutoAttackDamage * hunter.AutoDamageMultiplier * shape.AutoAttackDamage
                                       * (impact ? 1f + shape.ImpactSwingBonus : 1f),
                                       ms, 1, null, null, abs, fromSkill: false, swing: true,
                                       ignoresArmour: swingIgnoresArmour || trailArmed);
                impactSwing = false;
                castCarry = 0f;
                if (swingLifesteal > 0f && swung > 0f)
                    Heal((int)MathF.Round(swung * swingLifesteal), ms);
                if (alive == 0) return Kill(ms);
            }
            if (onBeat)
            {
                // THE NEXT BEAT at the LIVE rate (TIDE, RHYTHM move it mid-wave) — and BRISK shortens a
                // beat the champion spent on the swing, so a brisk build swings sooner but casts no faster.
                var swung = !acted && tuning.AutoAttackDamage > 0f;
                nextBeat = ms + BeatFor(RateNow() * (swung ? shape.AutoAttackRate : 1f), tuning.BeatMs);
                if (beatOwed) { champ.BeatCount++; beatOwed = false; events.Add(new BattleEvent(BattleEventKind.Beat, 0, champ.BeatCount, ms)); }
            }

            // ── THE ENEMY BITES BACK ──────────────────────────────────────────────────────────
            //
            // ACCUMULATED, never `ms % enemyIntervalMs`, which is what it used to be and which made the
            // SWIFT affix do the opposite of its name. The loop steps in TickMs (100ms), so an exact
            // modulo only fires where the interval divides a multiple of the tick — the real gap between
            // bites is lcm(tick, interval), not the interval. SWIFT multiplies the interval by 0.7:
            //
            //   attack bias   plain      SWIFT wanted    SWIFT actually got
            //   Normal 1500   1500 ms    1050 ms         2100 ms   (40% FEWER bites)
            //   Heavy  2200   2200 ms    1540 ms         7700 ms   (5x fewer)
            //   Fast   1000   1000 ms     700 ms          700 ms   (correct, by luck of the divisor)
            //
            // "The creatures strike more often" was a band the player could learn to fear, and on two of
            // the three attack biases it was a band that let them rest. The auto-attack ten lines above
            // has always accumulated its own next time; this now does the same.
            // INTERCEPT spends its held stun HERE, on the bite it was kept for: the wave is held and
            // this bite does not happen at all. A stun is not damage prevention — nothing was dealt to
            // absorb or bank — so nothing here touches the shield or REPAY.
            if (ms >= nextBite && pinArmed > 0)
            {
                // The whole bite is intercepted, not delayed. A DELAY is worth nothing against this
                // model: there is one wave-wide bite clock, so pushing it back a second costs the wave
                // the same second whenever the push happens — measured, and it moved no number. A
                // machine that catches the attack is what MACHINE actually promises, and it is a real
                // difference: that damage never arrives rather than arriving later.
                nextBite += (int)MathF.Round(enemyIntervalMs * (1f + slowFactor));
                pinArmed = 0;
                staggeredThisBite = false;
            }
            else if (ms >= nextBite)
            {
                // MIRE stretches the interval; the slow is a fraction of it, so 25% is a quarter
                // longer between bites rather than a quarter less damage.
                nextBite += (int)MathF.Round(enemyIntervalMs * (1f + slowFactor));
                staggeredThisBite = false;   // STAGGER may push the next one
                castRamp = 0;                // RHYTHM — a bite resets the run of casts
                // EVERY LIVING CREATURE BITES. This is what makes action economy real: a Swarm's combined
                // damage is its threat, and every creature killed is incoming damage removed. A build that
                // cannot clear a Swarm quickly does not merely kill slowly, it takes the full wave's
                // damage for the whole fight.
                var incoming = 0f;
                var front = FirstAlive();
                for (var ci = 0; ci < creatures.Count; ci++)
                    if (creatures[ci].Alive)
                    {
                        var bite = creatures[ci].Damage;
                        // WILT/SHRIVEL breaks ONE creature far harder than WILT breaks the wave, so
                        // the front one is counted at its own depth.
                        if (frontBreak < 0f && ReferenceEquals(creatures[ci], front))
                            bite *= Math.Max(0f, 1f + frontBreak);
                        else if (secondBreak < 0f && ReferenceEquals(creatures[ci], SecondAlive()))
                            bite *= Math.Max(0f, 1f + secondBreak);
                        incoming += bite;
                    }

                // mods.Health no longer divides the bite — it multiplies the pool (see ChampionHealth).
                // WILT's attack break, applied to the whole wave's bite. Negative, and floored by
                // the skill's own dial, so it can never turn a bite into healing.
                if (attackBreak < 0f) incoming *= Math.Max(0f, 1f + attackBreak);

                var taken = incoming * defenseFactor * fragilityMult;

                // ── ENDURE. Applied in this order on purpose: multipliers first, then the flat cut, so
                //    PADDING is worth MORE to a build that already mitigates — small bites are what a
                //    flat reduction erases, and that is the branch's whole answer to a Swarm. ────────
                taken *= shape.DamageTaken;

                // REMNANT — the mire keeps what it has drowned, and everything still standing wades
                // through it. Floored by the skill's own cap, so a long wave cannot weaken the bite to
                // nothing: what is bought is a wave that gets easier as you cut it down, never a wave
                // that stops mattering.
                if (weakenPerDead > 0f && deadThisWave > 0)
                    taken *= MathF.Max(1f - weakenCap, 1f - weakenPerDead * deadThisWave);

                // ABSORB — mitigation rises as health falls, to its cap at death's door. The node that
                // makes a low-health build survivable without making a healthy one invincible.
                // READS HEALTH, NEVER SHIELD: "how badly hurt am I" is a question about the pool.
                if (shape.AbsorbAtLowHealth > 0f)
                {
                    var missing = 1f - champ.Health / (float)Math.Max(1, champ.MaxHealth);
                    taken *= 1f - shape.AbsorbAtLowHealth * missing;
                }

                // MACHINE 4p — plate holds better while there is plate left. Applied HERE, before the
                // shield spends itself, so the mitigation and the absorption are not counted twice.
                if (shape.ShieldedDamageTaken < 1f && champ.CurrentShield > 0f)
                    taken *= shape.ShieldedDamageTaken;

                taken = MathF.Max(0f, taken - shape.FlatDamageReduction);

                // ── FROM HERE `taken` IS THE POST-MITIGATION WOULD-BE DAMAGE — what the hunter would
                //    actually lose if nothing else intervened. Everything below decides who pays it:
                //    a preventer, the Shield, or the pool. The three are different facts and the
                //    metrics keep them apart. ───────────────────────────────────────────────────────
                var attempted = taken;
                if (metrics is not null) metrics.DamageAttempted += attempted;

                // ── HARD PREVENTION, BEFORE ANYTHING IS SPENT. ───────────────────────────────────
                //
                // A bite that deals nothing must not consume Shield, must not feed REPAY, and must not
                // spend a SECOND preventer's once-per-wave charge. IRON used to charge the pool and
                // refund it a hundred lines later, which made "was this bite stopped?" unanswerable at
                // the moment anything else needed to know.
                var prevented = 0f;

                // The traps that answer this bite, and whether one of them stops it outright. The arm
                // is consumed here — a trap that fires has fired, whether it reflected or prevented.
                answering.Clear();
                for (var idx = 0; idx < skills.Count; idx++)
                {
                    var sk = skills[idx];
                    if (sk.Def.Kind != SkillKind.Reaction || sk.Def.On != ReactionOn.Bitten) continue;

                    // COILED re-arms the TRAP far faster, so it answers more bites. This loop only runs
                    // for an equipped Trap, so the enchant is naturally dead on any build without one.
                    var trapBase = (int)(Math.Max(1_000, sk.Def.RearmMs) * sk.Def.CooldownMultiplier);   // RECOIL, BLUNT
                    if (triggers.Contains(BuildTrigger.Coiled)) trapBase = (int)(trapBase * CoiledCooldownFactor);
                    var cd = Math.Max(1, (int)(trapBase / Math.Max(0.1f, RateNow())));
                    if (abs < champ.ReadyAt.GetValueOrDefault(idx, 0)) continue;
                    champ.ReadyAt[idx] = abs + cd;

                    // MESH and SPITE — the reflect grows with every bite the trap has answered this
                    // wave, to its own ceiling. Zero growth at the base line, so a plain JAWS is flat.
                    var reflect = sk.Def.ReflectFraction;
                    if (sk.Def.ReflectGrowthPerBite > 0f)
                        reflect += Math.Min(sk.Def.ReflectGrowthCap, sk.Def.ReflectGrowthPerBite * bites);
                    bites++;
                    answering.Add((idx, reflect, sk.Def.StopsWholeBite));
                }

                // IRON — the trap stops a whole bite. A stop is prevention now, decided before the pool
                // or the shield is touched.
                var ironStopped = 0f;
                for (var a = 0; a < answering.Count; a++)
                    if (answering[a].Stops && taken > 0f)
                    {
                        ironStopped = taken;
                        prevented += taken;
                        taken = 0f;
                    }

                // MACHINE 5p PLATING — once a wave, the first bite that would ACTUALLY deal damage is
                // prevented outright, and its would-be damage becomes Shield. `taken > 0f` is the whole
                // composition rule: a bite IRON already stopped is not eligible, so the charge is still
                // there for the next real one.
                var platingStopped = 0f;
                if (shape.PreventFirstDamagingBite && !platingSpent && taken > 0f)
                {
                    platingSpent = true;
                    platingStopped = taken;
                    prevented += taken;
                    taken = 0f;
                }

                if (metrics is not null) metrics.DamagePrevented += prevented;

                // ── THE SHIELD EATS THE REMAINDER, AND ONLY WHAT GETS PAST IT IS HEALTH DAMAGE. ──
                var absorbed = champ.AbsorbWithShield(taken);
                var healthDamage = taken - absorbed;
                if (absorbed > 0f)
                {
                    if (metrics is not null) metrics.ShieldAbsorbed += absorbed;
                    events.Add(new BattleEvent(BattleEventKind.ShieldAbsorbed, 0, (int)MathF.Round(absorbed), ms));
                    if (champ.CurrentShield <= 0f)
                        events.Add(new BattleEvent(BattleEventKind.ShieldBroken, 0, 0, ms));
                }

                // REPAY banks what the hunter took, per slot, until that slot casts. HEALTH DAMAGE
                // ONLY: a bite the Shield ate cost the hunter nothing, and paying it back would turn
                // every defensive layer into an offensive one. Banked for every slot rather than only
                // the one that pays it back, because a build may carry two and each has its own clock.
                if (healthDamage > 0f)
                    for (var k = 0; k < skills.Count; k++)
                        if (skills[k].Def.PaysBackDamageTaken > 0f)
                        {
                            if (!takenSinceCast.TryGetValue(k, out var bank))
                                takenSinceCast[k] = bank = new List<(int, float)>();
                            bank.Add((ms, healthDamage));
                        }

                var healthLost = (int)MathF.Round(healthDamage);
                champ.Health -= healthLost;
                if (metrics is not null) metrics.HealthDamage += healthLost;
                events.Add(new BattleEvent(BattleEventKind.EnemyStrike, 0, healthLost, ms));

                // PAYBACK banks the bite for the next skill; REBOUND turns a share of what LANDED back
                // into health — after the rest of the branch has had its say, so it reads the real bite.
                if (shape.BiteFuelBonus > 0f && biteFuel < shape.BiteFuelMax) biteFuel++;
                if (shape.HealOnBiteFraction > 0f && champ.Alive && healthDamage > 0f)
                    Heal((int)MathF.Round(healthDamage * shape.HealOnBiteFraction), ms);

                // PLATING's reward, once the bite it stopped is settled: the whole of what that bite
                // would have dealt becomes Shield.
                if (platingStopped > 0f) GrantShield(platingStopped, ms);

                // THORNS — every biter takes a fraction of its own RAW bite back. Read against the
                // creature's bite before the champion's mitigation, so PADDING and BULWARK do not
                // quietly shrink this node — an Endure road whose nodes cancel each other is shorter
                // than it looks. Landed the way poison lands: not a skill hit (no crit, no hit-size
                // rule, no Venom feeding itself) and through armour, because a Bruiser is often plated
                // and a thorn that plate could erase would be dormant in the one band it answers.
                // A dead champion has no thorns: the bite that finished the champion must not also
                // credit kills and MOMENTUM refunds to a corpse (review 2026-08-25).
                if (shape.ReflectFraction > 0f && champ.Alive)
                {
                    for (var ci = 0; ci < creatures.Count; ci++)
                    {
                        var biter = creatures[ci];
                        if (!biter.Alive) continue;
                        LandOn(biter, biter.Damage * shape.ReflectFraction, ms, fromSkill: false, ignoresArmour: true);
                    }
                    // A wave the thorns finished is a clear — unless this same bite finished the
                    // champion too, in which case the death handling below still has to run.
                    if (alive == 0 && champ.Alive) return Kill(ms);
                }

                // DYNAMO — the bite winds the spring.
                if (chargeLive && triggers.Contains(BuildTrigger.Dynamo) && charge < chargeCap)
                {
                    charge = Math.Min(chargeCap, charge + ChargeDynamoPerBite);
                    events.Add(new BattleEvent(BattleEventKind.Charge, 0, charge, ms));
                }

                // ── THE TRAPS PAY OUT. Their arm was spent above; this is what they do with it. ──
                //
                // JAWS REFLECTS THE BITE, it does not throw a blow of its own — which is what "being
                // attacked works in your favour" actually says. IRON gave that up for the stop, and
                // REPRISAL buys it back: a stopped bite is ALSO returned, in full.
                for (var a = 0; a < answering.Count; a++)
                {
                    var (idx, reflect, stops) = answering[a];
                    var sk = skills[idx];

                    // The figure a reflect is a share OF: what this bite would have dealt. A stopped
                    // bite reflects its own prevented size, so REPRISAL is worth what it says.
                    var basis = stops ? ironStopped : attempted;
                    var trapRaw = reflect > 0f
                        ? basis * reflect
                        : stops ? 0f : SkillCatalogue.PoweredBase(sk.Def, resonance);
                    trapRaw *= VowFactor(sk, weaveCtx, shape)
                               // OPENING VOLLEY — the Trap's first spring counts as its first cast.
                               * (castOnce.Add(idx) ? shape.FirstCastMultiplier : shape.LaterCastMultiplier);

                    // PLATING (the JAWS reinforcement) — a stopped bite is also armour: half of what it
                    // would have dealt becomes Shield.
                    if (stops && sk.Def.ShieldFromStoppedBite > 0f && ironStopped > 0f)
                        GrantShield(ironStopped * sk.Def.ShieldFromStoppedBite, ms);

                    events.Add(new BattleEvent(BattleEventKind.Skill, idx, 0, ms));
                    if (trapRaw > 0f)
                    {
                        var trapDealt = LandSpread(trapRaw, ms, shape.TargetsFor(sk.Def), sk.Source, sk.Def, abs);
                        // NATURE'S SIGNATURE follows the damage here too — a Trap that bites back heals
                        // its sliver. A Trap never CASTS, so the cast-following rules (CHARGE, Spirit's
                        // prime, Mind's stretch) are rightly silent on this path.
                        if (sk.Source == Source.Nature && trapDealt > 0f)
                            Heal((int)MathF.Round(trapDealt * heal.NatureSignatureLeech), ms);
                    }
                    if (alive == 0) return Kill(ms);
                }

                if (!champ.Alive)
                {
                    if (!champ.UndyingSpent && triggers.Contains(BuildTrigger.Undying))
                    {
                        champ.UndyingSpent = true;
                        champ.Health = 1;
                        // THE EVENT CARRIES ITS DURATION. It was stamped 0, and the replay opens the
                        // window as AtMs + Amount — so the shield closed the instant it opened and the
                        // screen's steel outline never drew once (review 2026-08-30: a dormant feature
                        // whose unit test passed because the test built its own event).
                        // Its OWN kind: Shield's Amount is a banked HEALTH figure, and this one
                        // is a DURATION — one overloaded payload with two meanings was a live
                        // replay bug (the audit's D8).
                        events.Add(new BattleEvent(BattleEventKind.Undying, 0, UndyingShieldMs, ms));
                    }
                    else
                    {
                        champ.Health = 0;
                        events.Add(new BattleEvent(BattleEventKind.Down, 0, 0, ms));
                        return Finish(WaveOutcome.Wiped, ms);
                    }
                }
            }
        }

        // Could not kill it inside the ceiling: a stall, not a hang. Paid out as a retreat.
        return Finish(WaveOutcome.Stalled, tuning.TickCeilingMs);
    }

    /// <summary>The floor under a champion's pool, so an untrained Hunter still has a run to lose.</summary>
    private const int MinimumChampionHealth = 60;

    /// <summary>
    /// A StaticCost Vow's HEALTH price, as a multiplier on the champion's max health at mint.
    /// </summary>
    /// <remarks>
    /// RECKLESS OFFERING pays for its power this way — a smaller pool for the whole run. FRAGILITY, the
    /// other StaticCost Vow, pays as damage taken instead (see the fragility multiplier in ResolveWave), so
    /// it is excluded here — charging its effective-HP as health as well would bill the same cost twice.
    /// Applied ONCE at mint (the champion persists across waves), which is why it lives here as a helper the
    /// mint sites call rather than inside the per-wave sim. The product across every such Vow the build wears.
    /// </remarks>
    /// <summary>Critical chance as a 0..0.75 fraction — trained, plus affixes, plus the tree's nodes.</summary>
    /// <remarks>
    /// PUBLIC because the STATS screen needs the same answer, and it was computing its own: it summed
    /// the trained stat and the gear affixes and left out <see cref="SkillShape.BonusCritPercent"/>
    /// entirely, so every crit node the player bought on the mastery tree was real in the fight and
    /// invisible on the page that exists to tell them what their numbers are. A screen that recomputes
    /// a formula is a second implementation of it, and the second one is the one that drifts.
    /// </remarks>
    public static float CritChance(Economy.Hunter hunter, SkillShape shape)
    {
        ArgumentNullException.ThrowIfNull(hunter);
        return Math.Clamp(
            (hunter.ValueOf(Economy.HunterStat.CriticalChance) + hunter.AffixTotal(Economy.AffixStat.Crit)
             + shape.BonusCritPercent) / 100f,
            0f, MaxCritChance);
    }

    /// <summary>What a critical hit multiplies by — the base, raised by FOCUS.</summary>
    public static float CritMultiplier(Economy.Hunter hunter)
    {
        ArgumentNullException.ThrowIfNull(hunter);
        return CritBaseMultiplier + hunter.ValueOf(Economy.HunterStat.Focus) * FocusCritDamagePerPoint;
    }

    /// <summary>The champion's starting health for a run: the Hunter's pool, as the BUILD changes it.</summary>
    /// <remarks>
    /// <para>
    /// EVERY mint site calls this. It exists because there were three of them — the live screen, the
    /// bench, and a balance harness — each open-coding <c>Math.Max(60, hunter.MaxHealth) * ...</c>, and
    /// the third had quietly dropped the <see cref="VowHealthMultiplier"/> term. Nothing shipped was
    /// wrong; the MEASUREMENTS were, which is worse in its way, because a measurement that lies is
    /// trusted. RECKLESS OFFERING looked like the best Vow in the game for a while on the strength of a
    /// harness that never charged its health price, and a build's health looked worthless because a
    /// third of the multiplier chain was missing from the only place anybody was reading numbers off.
    /// </para>
    /// <para>
    /// The lesson is the one this codebase keeps relearning: a formula copied is a formula that will
    /// drift, and the copy that drifts is never the one you are looking at.
    /// </para>
    /// </remarks>
    /// <param name="poolScale">
    /// The pool's share of the wave-length stretch — see <see cref="ChampionPoolScale"/>. Pass 1 to mint
    /// the pre-2026-08-28 pool, which is the only way a probe can measure the change rather than assume it.
    /// </param>
    public static int ChampionHealth(Build build, Economy.Hunter hunter,
                                     float poolScale = ChampionPoolScale)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(hunter);
        // VowHealthMultiplier already folds in the build's own MaxHealth term; do not apply it twice.
        //
        // THE HEALTH CHANNEL IS HEALTH NOW (playtest 2026-08-25: "we wore a ring that says -X% health
        // and the champion's health did not change"). Until this change every HEALTH multiplier in the
        // game — trained VITALITY, the worn charm, a HEALTH affix, a LIFE gem, a keystone's health term,
        // the trait tree's "more health" nodes — was a DIVISOR on incoming damage that the sim applied
        // silently, while the tooltips, the affix names and the stats screen all said "health". The
        // survivability was real; the number the player was shown never moved, and a stat the player
        // cannot see move is a stat the player believes is broken. So the whole channel now multiplies
        // the POOL, here, and the bite is just the bite. Same hits-to-die, honest number.
        // The worn gear's SHAPE reaches the pool too — the BODY set's third piece (+10% maximum health)
        // is a MaxHealth term like TOUGHNESS's, and lives on the hunter, not the build (2026-08-27).
        var pool = Math.Max(MinimumChampionHealth, hunter.MaxHealth)
                   * VowHealthMultiplier(build)
                   * GearShape.Of(hunter).MaxHealth
                   * MathF.Max(0.05f, build.Resolve(hunter).Health)
                   // ...and the champion's share of the wave-length stretch. NOT the wave's own factor:
                   // see ChampionPoolScale for the two effects that make a longer fight favour the
                   // champion, and for the sweep that priced the difference.
                   * MathF.Max(0.1f, poolScale);
        return Math.Max(1, (int)MathF.Round(pool));
    }

    public static float VowHealthMultiplier(Build build)
    {
        ArgumentNullException.ThrowIfNull(build);
        var mult = 1f;
        foreach (var v in DistinctVows(build.Skills))
            if (v is { Kind: VowKind.StaticCost, StaticCostMagnitude: > 0f, DamageTakenIncrease: 0f })
                mult *= 1f - v.StaticCostMagnitude;

        // TOUGHNESS raises the pool and ENDLESS halves it, and both belong here rather than in the wave
        // sim for the same reason a Vow's health price does: the champion persists across a whole descent,
        // so a per-wave multiplier would compound the change once for every wave walked.
        mult *= build.Shape.MaxHealth;

        return MathF.Max(0.05f, mult);
    }

    /// <summary>Every Vow the build has sworn, counted once however many skills carry it.</summary>
    /// <remarks>
    /// The single place that decides what "wearing a Vow twice" means, so the two price sites cannot
    /// answer it differently. It means nothing: a Vow is a promise about the build, and a promise made
    /// on four skills is one promise. Keyed on Id rather than the record, because two catalogue entries
    /// could in principle be value-equal and still be different Vows.
    /// </remarks>
    private static IEnumerable<Vow> DistinctVows(IEnumerable<EquippedSkill> skills)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sk in skills)
            if (sk.Vow is { } v && seen.Add(v.Id))
                yield return v;
    }

    /// <summary>A skill's Vow multiplier, if the BUILD meets its demand.</summary>
    /// <remarks>
    /// The context is a property of the build, not of the moment, so it is built once per wave and every
    /// skill is judged against the same one. A Vow that read the fight — below 40% health, against a
    /// boss — was a lottery on how the wave went in a game where the player cannot react; this is a
    /// decision they made at the workbench and can see the consequences of in the report.
    /// </remarks>
    private static float VowFactor(EquippedSkill sk, BuildContext ctx, SkillShape shape)
    {
        if (sk.Vow is not { } vow) return 1f;
        if (!Vows.IsActive(vow, ctx)) return 1f;

        // THE BONUS is scaled, not the factor. A Vow worth x1.90 pays +0.90; TWICE SWORN at 1.4 makes
        // that +1.26. Scaling the whole factor would pay out on a build with no Vow sworn at all, which
        // would make a Vow-specialist passive into a flat damage bonus that happens to be named after
        // Vows — and would pay most to the player who ignored the system it is about.
        var bonus = Vows.Multiplier(vow) - 1f;
        return 1f + bonus * MathF.Max(0f, shape.VowPowerMultiplier);
    }

    /// <summary>Describe a build to the Vow layer.</summary>
    public static BuildContext DescribeBuild(Build build, Economy.Hunter hunter)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(hunter);

        var worn = new HashSet<BareSlot>();
        foreach (var (slot, bare) in new[]
                 {
                     (Economy.GearSlot.Boots, BareSlot.Boots),
                     (Economy.GearSlot.Gloves, BareSlot.Gloves),
                     (Economy.GearSlot.Helm, BareSlot.Helm),
                     (Economy.GearSlot.Ring, BareSlot.Ring),
                     (Economy.GearSlot.Charm, BareSlot.Charm),
                 })
            if (hunter.Worn(slot) is not null) worn.Add(bare);

        var mods = build.Resolve(hunter);
        return new BuildContext(
            DistinctStyles: build.Skills.Select(s => s.Def.Style).Distinct().Count(),
            DistinctSources: build.Skills.Select(s => s.Source).Distinct().Count(),
            SkillsWoven: build.Skills.Count,
            // The BUILD's capacity, not the type's floor. VOW OF COMPLETION demands "no skill slot is
            // empty", and against a hard 4 a player who had bought the fifth weave met it with four
            // skills and an empty fifth slot in front of them.
            SkillSlots: build.SlotCapacity,
            CritPercent: hunter.ValueOf(Economy.HunterStat.CriticalChance)
                         + hunter.AffixTotal(Economy.AffixStat.Crit)
                         + build.Shape.BonusCritPercent,
            BaseCritPercent: Economy.ProgressionTuning.Default.BaseValue[Economy.HunterStat.CriticalChance],
            SkillRate: mods.SkillRate * build.Shape.SkillRate,
            Defence: hunter.Defense,
            KeystonesWorn: build.Keystones.Count,
            WornSlots: worn);
    }
}
