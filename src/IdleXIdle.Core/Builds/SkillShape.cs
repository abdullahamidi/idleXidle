using System;
using System.Collections.Generic;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Traits;

namespace IdleXIdle.Core.Builds;

/// <summary>
/// Everything the skill tree changes about HOW a build fights, as opposed to how big its numbers are.
/// </summary>
/// <remarks>
/// <para>
/// This type exists to make the design's first rule enforceable: <b>no node may be a bare multiplier</b>.
/// <see cref="BuildMods"/> holds five scalars (damage, health, rate, haul, rarity) and a tree built on it
/// can only ever be a list of percentages — which is exactly what the audit found, 31 of 43 nodes being
/// flat numbers. A branch needs to be able to say "hits below 60 deal nothing", "every skill strikes every
/// creature at 40%", "the first hit taken each wave is free". None of those are a scalar, and none of them
/// can be expressed at all until the sim has somewhere to read them from.
/// </para>
/// <para>
/// Every field here is READ by <see cref="SoloBattle"/>. That is not a nicety: a shape field nothing reads
/// is a node that does nothing, which is the failure mode that left the entire FORTUNE road inert for the
/// whole of development. If a field stops being read, its nodes must be deleted with it.
/// </para>
/// <para>
/// Neutral defaults throughout, so <see cref="None"/> is exactly "no tree allocated" and every call site
/// can take a shape unconditionally instead of null-checking one.
/// </para>
/// </remarks>
public sealed record SkillShape
{
    public static SkillShape None { get; } = new();

    // ── HIT SIZE — the WEIGHT axis. What flat enemy armour reads. ─────────────────────────────────

    /// <summary>Multiplies every skill hit's raw size. The axis's primary currency.</summary>
    public float HitSize { get; init; } = 1f;

    /// <summary>Multiplies the EXTRA a Vow pays, over and above the unsworn baseline of 1.</summary>
    /// <remarks>
    /// <para>
    /// Applied to the bonus, not to the total, which is the only formulation that means anything. A Vow
    /// worth x1.90 pays +0.90; at <c>VowPowerMultiplier = 1.4</c> it pays +1.26, so x2.26. Multiplying
    /// the whole factor instead would pay 1.4x on a build with NO Vow sworn, which is the opposite of
    /// what a Vow-specialist passive should do — it would be a flat damage bonus wearing a Vow's name.
    /// </para>
    /// <para>
    /// Exists because THE OATHBOUND's headline passive, TWICE SWORN, reads "Vows pay far more, and the
    /// Mark window they buy lasts longer" and only the second half was implemented. The character's
    /// Shape carried AmplifyWindowMultiplier and AmplifyPowerBonus and nothing whatever about Vows — the
    /// first clause of the sentence had no field to write to, on the one character built around a
    /// SYSTEM rather than a branch.
    /// </para>
    /// </remarks>
    public float VowPowerMultiplier { get; init; } = 1f;

    /// <summary>Flat armour subtracted before mitigation — the cheap answer to Plated bands.</summary>
    // ── RESONANCE. The branch that sells how strong every skill is BEFORE you choose one: the Source
    //    matchup, the affinity lean, and how much a point of resonance is worth. Never cooldown —
    //    VOLLEY owns that (design §9). Each is zero at the base line. ─────────────────────────────

    /// <summary>KEYED — how much MORE a strong Source matchup pays, on top of its own multiplier.</summary>
    public float StrongMatchupBonus { get; init; }

    /// <summary>DISCORD — how much of a weak matchup's penalty is refunded. 1 removes it entirely.</summary>
    public float WeakMatchupRelief { get; init; }

    /// <summary>CHORD — every matchup counts as strong, whatever the ring says.</summary>
    public bool AllMatchupsStrong { get; init; }

    /// <summary>NARROW — your discipline's own Form hits harder, and every other Form softer.</summary>
    public float AffinityStyleBonus { get; init; }
    public float OffStylePenalty { get; init; }

    /// <summary>BROAD — how much of the OPPOSITE Form's affinity penalty is given back. 1 removes it.</summary>
    public float OppositePenaltyRelief { get; init; }

    /// <summary>DEEP — a point of resonance is worth this much more.</summary>
    public float ResonanceWorth { get; init; }

    /// <summary>PURE — what every skill gains while the whole weave shares one Source.</summary>
    public float OneSourceBonus { get; init; }

    // ── LOOT. What you carry out — and every rule of it is keyed to something THE FIGHT already
    //    knows, which is the designer's own unblock: "hasar almadığın her saniye drop şansın artar
    //    gibi... hasar ve loot mekaniğini birleştirebilirsin". §9b wrote the branch around banking
    //    and this game deliberately has no bank (SoloExpedition pays every wave the instant it
    //    clears), so the tension it sells is the one an idle run actually has: how cleanly and how
    //    deep a single attempt goes. It also settles the no-bare-multiplier law by construction —
    //    a haul bonus you have to EARN inside the wave is not a flat percentage. ─────────────────

    /// <summary>UNTOUCHED — haul per second of the wave in which nothing bit you, to its own cap.</summary>
    public float HaulPerCleanSecond { get; init; }
    public float HaulCleanCap { get; init; }

    /// <summary>
    /// SPOTLESS and VEIN — what a wave you finish ABOVE NINE TENTHS HEALTH is worth on top.
    /// </summary>
    /// <remarks>
    /// It was "a wave nothing bit you in", and that wave does not exist: every creature in a wave
    /// attacks, so the condition was unreachable by construction and both nodes were dead — the same
    /// failure the reinforcement pass spent a day finding. "Finished at FULL health" was unreachable
    /// too — there is no full heal between waves, so a champion chipped once stays chipped. Nine
    /// tenths is the first version a real build can hold; it is still the strict opposite of
    /// BLOODPRICE's half, and it is a thing the player can read off their own health bar.
    /// </remarks>
    public float HaulUntouchedWave { get; init; }

    /// <summary>BLOODPRICE — the opposite pole: what a wave ended under half health is worth.</summary>
    public float HaulWhenHurt { get; init; }

    /// <summary>PROSPECT and LODE — haul per wave cleared past <see cref="HaulDepthFloor"/>.</summary>
    public float HaulPerWavePastDepth { get; init; }
    public int HaulDepthFloor { get; init; }

    /// <summary>SECOND LOOK — a clean wave raises the chest's quality, not just its gleam.</summary>
    public float RarityFromClean { get; init; }

    /// <summary>CACHE — every Nth wave cleared pays this much more.</summary>
    public int HaulEveryNthWave { get; init; }
    public float HaulNthWaveBonus { get; init; }

    public float ArmourPenetration { get; init; }

    /// <summary>
    /// BREAKER — the fraction of a hit's OVERKILL that carries to the next living creature.
    /// </summary>
    /// <remarks>
    /// The design named a part-break bonus here. The sim has no part-break model, and a node that reads
    /// nothing is worse than a node that reads something else — so this answers the same complaint from
    /// inside the model that exists: overkill is normally discarded, and discarded overkill is precisely
    /// the tax a large-hit build pays in a Swarm band.
    /// </remarks>
    public float OverkillCarry { get; init; }

    // ── TARGET COUNT — the SPREAD axis. What action economy reads. ────────────────────────────────

    /// <summary>Extra creatures every Form reaches per activation.</summary>
    public int ExtraTargets { get; init; }

    /// <summary>Extra targets for one STYLE specifically — the specialisations.</summary>
    public IReadOnlyDictionary<Style, int> StyleTargets { get; init; } = new Dictionary<Style, int>();

    /// <summary>
    /// A damage multiplier that applies to ONE Form only. A character's APTITUDE lives here.
    /// </summary>
    /// <remarks>
    /// Per-Form rather than flat, because that is what an aptitude means: the QUIVER is better with a
    /// Projectile than you would be, not better at everything. It also has to be a number the SIM
    /// reads — this codebase's recurring failure is a value resolved, carried and summed by correct
    /// code at every step and read by nothing, and an aptitude that existed only as screen text would
    /// be exactly that again.
    ///
    /// Multiplicative on combine, like every other multiplier here, so a character's aptitude and a
    /// Form specialisation node on the same Form compound instead of racing.
    /// </remarks>
    public IReadOnlyDictionary<Style, float> StylePower { get; init; } = new Dictionary<Style, float>();

    /// <summary>This STYLE's damage multiplier, or 1 when nothing favours it.</summary>
    public float StylePowerFor(Style style) => StylePower.TryGetValue(style, out var f) && f > 0f ? f : 1f;

    // ── CONDITIONAL DAMAGE. Each one is a shape: it asks a question about the target or the clock. ──

    /// <summary>Multiplier on the first hit each creature takes (FOLLOW THROUGH, OPENER, ALPHA).</summary>
    public float FirstHitMultiplier { get; init; } = 1f;

    /// <summary>Multiplier on every hit after the first — ALPHA's price.</summary>
    public float LaterHitMultiplier { get; init; } = 1f;

    /// <summary>CULL — bonus against creatures below <see cref="CullThreshold"/> of their health.</summary>
    public float CullThreshold { get; init; }
    public float CullBonus { get; init; }

    /// <summary>SWARMBANE — damage bonus per living creature in the wave.</summary>
    public float PerCreatureBonus { get; init; }

    /// <summary>FIRST STRIKE — the opening seconds are amplified, everything after is cut.</summary>
    public float OpeningSeconds { get; init; }
    public float OpeningBonus { get; init; }
    public float AfterOpeningPenalty { get; init; }

    /// <summary>
    /// INTERRUPT — damage amplified in the run-up to an enemy swing.
    /// </summary>
    /// <remarks>
    /// The design said "during an enemy windup". There is no windup model; the enemy bite is instantaneous
    /// on an interval. This is the same idea inside the model that exists — the last quarter of the bite
    /// interval IS the windup, and a build that lands its damage there is interrupting.
    /// </remarks>
    public float InterruptBonus { get; init; }

    /// <summary>BASTION — damage bonus per point of maximum health.</summary>
    public float DamagePerMaxHealth { get; init; }

    /// <summary>ANCHOR — hit size bonus per point of maximum health.</summary>
    public float HitSizePerMaxHealth { get; init; }

    /// <summary>Flat multiplier on all damage dealt — only ever a PRICE (Bulwark, Blitz).</summary>
    public float DamageDealt { get; init; } = 1f;

    // ── TEMPO. ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Multiplies skill rate; below 1 lengthens cooldowns.</summary>
    public float SkillRate { get; init; } = 1f;

    /// <summary>PREPARATION — every skill's first cast of a wave is free of its cooldown.</summary>
    public bool FreeOpeningCast { get; init; }

    /// <summary>FOCUS — added to the champion's critical chance, in percentage points.</summary>
    // ── SHIELD (combat-v2). Three dials, because three set rungs need them and nothing else does.

    /// <summary>MACHINE 3p — Shield granted at the start of every wave, as a share of maximum health.</summary>
    public float WaveStartShieldFraction { get; init; }

    /// <summary>MACHINE 4p — incoming damage multiplier while any Shield is held. 1 = no change.</summary>
    public float ShieldedDamageTaken { get; init; } = 1f;

    /// <summary>MACHINE 5p — once a wave, prevent the first bite that would actually deal damage.</summary>
    public bool PreventFirstDamagingBite { get; init; }

    /// <summary>MIND 3p — percentage points added to critical damage.</summary>
    public float BonusCritDamagePercent { get; init; }

    /// <summary>
    /// MIND 4p FOCUS — percentage points of critical chance a direct damaging hit that did not crit
    /// adds, and the ceiling those additions stack to.
    /// </summary>
    /// <remarks>
    /// Crit in this simulation is an EXPECTED VALUE, not a rolled event (see <c>critFactor</c>), so
    /// "a hit that does not crit" cannot be a branch. FOCUS counts the NON-CRIT SHARE of each hit —
    /// one whole hit at zero crit chance, half a hit at fifty percent — which is the expectation of
    /// the rule as written, is deterministic, and keeps the property the set is selling: the less you
    /// have been critting, the closer the next one is.
    /// </remarks>
    public float FocusPerHitPercent { get; init; }

    /// <summary>MIND 4p FOCUS — the ceiling, in percentage points.</summary>
    public float FocusCapPercent { get; init; }

    /// <summary>
    /// MIND 5p CERTAINTY — at the FOCUS ceiling the next direct damaging hit crits for certain, and
    /// FOCUS empties.
    /// </summary>
    public bool CertaintyAtFocusCap { get; init; }

    /// <summary>
    /// BODY 5p MOMENTUM — how much harder the basic attack lands when an Active has just resolved.
    /// The IMPACT swing also carries all of its overkill. One pending at a time.
    /// </summary>
    public float ImpactSwingBonus { get; init; }

    /// <summary>
    /// DEADWEIGHT (THE ANVIL) — the share of a primary direct hit (a skill's or the basic swing) that a
    /// SURVIVING target keeps, to suffer on the champion's next such hit against it. 0 = off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Its own dial, deliberately NOT <see cref="OverkillCarry"/>. It was written as a carry — a third
    /// of a kill's spill into the next enemy — and a carry is a field three other rules already write
    /// (BODY's fourth rung, BREAKTHROUGH, CLEAN CUT) and combine by MAX, so the character's innate
    /// contributed nothing beside any of them and nothing at all in a boss room (diagnosed 2026-09-07:
    /// live, 1.5-4.5% of damage, zero against a lone enemy). A character innate must not vanish because
    /// another subsystem writes the same generic field.
    /// </para>
    /// <para>
    /// The two are different identities and stay different mechanics: a CARRY is a dead enemy's
    /// excess moving forward; DEADWEIGHT is a live enemy retaining force — worth most against what
    /// does not die in one blow. State lives on the creature (<see cref="WaveCreature.StoredDeadweight"/>);
    /// the rule is applied at the one landing site in <see cref="SoloBattle"/> by hit provenance.
    /// </para>
    /// </remarks>
    public float DeadweightShare { get; init; }

    /// <summary>NATURE 3p — how much stronger combat healing lands. Never applied to shield.</summary>
    public float HealingMultiplier { get; init; } = 1f;

    /// <summary>NATURE 4p — how much wider the wave's healing ceiling opens.</summary>
    public float HealCeilingBonus { get; init; }

    /// <summary>
    /// NATURE 5p OVERGROWTH — the share of healing wasted at full health that becomes shield instead.
    /// Shield is not healing: it does not spend the ceiling again, and nothing that reads health reads it.
    /// </summary>
    public float OverhealToShield { get; init; }

    /// <summary>SHADOW 3p SHADE — how much harder the Active that spends a shade lands.</summary>
    public float ShadeActiveBonus { get; init; }

    /// <summary>SHADOW 4p — how many shades may be held at once.</summary>
    public int ShadeMax { get; init; }

    /// <summary>
    /// SHADOW 5p AFTERIMAGE — the share of a shade-spent Active's resolved direct damage that falls a
    /// second time on the same targets. It costs no beat, starts no cooldown, is not a cast, spends no
    /// shade, makes no shade, and cannot make another afterimage.
    /// </summary>
    public float AfterimageFraction { get; init; }

    /// <summary>
    /// SPIRIT 3p — how much stronger each skill's FIRST activation of a wave is. Narrow by design:
    /// direct damage, direct healing, a shield grant, and the bonus part of an amplify. Never a stun
    /// length, a slow ceiling or a defence-break floor.
    /// </summary>
    public float FirstActivationMagnitude { get; init; } = 1f;

    /// <summary>SPIRIT 4p RESONANCE — skill rate gained the first time each distinct skill activates.</summary>
    public float ResonanceRatePerSkill { get; init; }

    /// <summary>SPIRIT 4p RESONANCE — the ceiling that gain stacks to.</summary>
    public float ResonanceRateCap { get; init; }

    /// <summary>
    /// SPIRIT 5p HARMONY — once all four equipped skills have activated in a wave, each is given one
    /// charge: its next activation takes the 3p magnitude a second time. Needs four filled slots, so a
    /// Vow that asks for an empty one is a real conflict rather than a special case.
    /// </summary>
    public bool HarmonyCharges { get; init; }

    /// <summary>MIND 2p — percentage points added to critical chance.</summary>
    public float BonusCritPercent { get; init; }

    /// <summary>MARK MASTERY — stretches the amplify window and deepens it.</summary>
    public float AmplifyWindowMultiplier { get; init; } = 1f;
    public float AmplifyPowerBonus { get; init; }

    /// <summary>ASSASSINATE — once per wave, a creature below this health fraction dies to the next hit.</summary>
    public float AssassinateThreshold { get; init; }

    /// <summary>BRISK — multiplies the BASIC SWING's cadence only; skills keep their own rate.</summary>
    /// <remarks>
    /// The swing is a third of a build's damage since 2026-08-26 and had no node of its own. Read at the
    /// one place the swing's interval is set, and nowhere else, so it cannot leak into cooldowns.
    /// </remarks>
    public float AutoAttackRate { get; init; } = 1f;

    /// <summary>The basic attack's hit, multiplied — the BODY set's fifth piece. Skills never read it.</summary>
    public float AutoAttackDamage { get; init; } = 1f;

    /// <summary>
    /// RHYTHM — every cast since the last bite raises skill rate by this fraction, up to
    /// <see cref="CastRampMax"/> casts. A bite resets the count.
    /// </summary>
    /// <remarks>
    /// Front-loading stated as a shape: a build gets faster the longer it goes unbitten, which is exactly
    /// the Caster band, and loses it all in the Bruiser band where bites are constant. The reset is what
    /// makes it a Tempo node and not a rate node.
    /// </remarks>
    public float CastRampPerCast { get; init; }
    public int CastRampMax { get; init; }

    /// <summary>
    /// OPENING VOLLEY — each skill's FIRST activation of a wave is multiplied by
    /// <see cref="FirstCastMultiplier"/>, every later one by <see cref="LaterCastMultiplier"/>.
    /// </summary>
    /// <remarks>
    /// Per SKILL per wave, which is neither OPENER (per creature) nor FLASH (per second): four skills is
    /// four big opening casts and then a wave of slightly smaller ones. The later-cast price is what a
    /// Greater has to carry.
    /// </remarks>
    public float FirstCastMultiplier { get; init; } = 1f;
    public float LaterCastMultiplier { get; init; } = 1f;

    // ── ENDURE. ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>FEEDBACK — health healed per creature struck, as a fraction of maximum.</summary>
    public float HealPerTargetStruck { get; init; }

    /// <summary>PADDING — flat damage removed from every incoming bite.</summary>
    public float FlatDamageReduction { get; init; }

    /// <summary>Multiplier on damage taken. Below 1 is mitigation, above 1 is a price.</summary>
    public float DamageTaken { get; init; } = 1f;

    /// <summary>ABSORB — extra mitigation as health falls, up to this fraction at death's door.</summary>
    public float AbsorbAtLowHealth { get; init; }

    /// <summary>
    /// THORNS — every creature that bites the champion takes this fraction of its own bite back.
    /// </summary>
    /// <remarks>
    /// The Endure answer that SCALES WITH THE THREAT: a Bruiser's whole identity is the size of its
    /// bite, so the harder it bites the more it pays, while a Swarm of nibblers pays almost nothing per
    /// head. Read against the creature's RAW bite, before the champion's own mitigation — otherwise every
    /// other Endure node would quietly shrink this one, and a branch whose nodes cancel each other is a
    /// branch with fewer nodes than it says. The return lands as a non-skill hit that ignores armour,
    /// the way poison does, so it can never feed the tree's own hit-size rules or be eaten by plate.
    /// </remarks>
    public float ReflectFraction { get; init; }

    /// <summary>SECOND WIND — fraction of maximum health healed on clearing a wave.</summary>
    public float HealOnClear { get; init; }

    /// <summary>MENDING — a fraction of maximum health regained every second, inside the fight.</summary>
    /// <remarks>
    /// Distinct from RECOVERY (between waves) and from VITALITY's regeneration (a trained stat, outside
    /// the heal ceiling). This is a build's heal, so it spends the per-wave ceiling like LEECH does.
    /// </remarks>
    public float RegenFraction { get; init; }

    /// <summary>
    /// PAYBACK — every bite taken banks a bonus for the next skill activation, up to
    /// <see cref="BiteFuelMax"/> bites.
    /// </summary>
    /// <remarks>
    /// The mirror of RHYTHM across the Tempo/Endure axis: Tempo builds up until it is bitten, Endure
    /// builds up FROM being bitten. Spent whole on the next skill activation, every hit of it.
    /// </remarks>
    public float BiteFuelBonus { get; init; }
    public int BiteFuelMax { get; init; }

    /// <summary>REBOUND — this fraction of every bite that lands comes back as health.</summary>
    /// <remarks>
    /// Read against the damage that actually LANDED, after the rest of the branch, so it is worth most
    /// where bites are big — a Bruiser band — and under the per-wave heal ceiling like every other heal.
    /// </remarks>
    public float HealOnBiteFraction { get; init; }

    /// <summary>RECOVERY — fraction of maximum health regained between waves.</summary>
    /// <remarks>
    /// A structural exception. game-flow.md §3.3 makes "health does not regenerate between waves" a rule
    /// of the game; this node is the one thing that buys an exception to it, which is what makes ENDURE
    /// the only branch that raises the ceiling of all four bands.
    /// </remarks>
    public float BetweenWaveRegen { get; init; }

    /// <summary>ENDLESS — full health every wave, for half the pool.</summary>
    public bool FullHealBetweenWaves { get; init; }

    /// <summary>Multiplier on maximum health, applied at champion mint.</summary>
    public float MaxHealth { get; init; } = 1f;

    /// <summary>
    /// THE TRAITS a champion is wearing, as one typed group rather than thirty more fields here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This record was already about ninety fields, and <c>SkillCatalogue.cs</c> records the same
    /// answer for <c>SkillRules</c>: past the width where a reader stops reading, a related group gets
    /// a nested record instead of another column. Every read site in <see cref="SoloBattle"/> says
    /// <c>shape.Traits.X</c>, and <see cref="Combine"/> keeps it in step in one line.
    /// </para>
    /// <para>
    /// <see cref="TraitRules.None"/> is neutral on every dial, so a champion wearing no trait
    /// composes exactly as a build from before traits existed. The rule this whole record obeys
    /// applies inside it too: a trait dial that stops being read must be deleted with its trait.
    /// </para>
    /// </remarks>
    public TraitRules Traits { get; init; } = TraitRules.None;

    /// <summary>Targets this SKILL reaches, after the shape's general and per-style additions.</summary>
    public int TargetsFor(SkillDef def) => TargetsFor(def, def.Targets);

    /// <summary>
    /// The same, told the baseline outright — for the cast path, whose resolved def already carries
    /// the variation's own target count.
    /// </summary>
    public int TargetsFor(SkillDef def, int baseline)
    {
        if (baseline == int.MaxValue) return baseline;   // a field already reaches everything

        var extra = ExtraTargets + (StyleTargets.TryGetValue(def.Style, out var f) ? f : 0);
        return Math.Max(1, baseline + extra);
    }

    /// <summary>
    /// Combine two shapes. Multipliers multiply, additives add, flags OR, thresholds take the more
    /// generous value.
    /// </summary>
    /// <remarks>
    /// Thresholds do NOT add. Two nodes that each execute below 25% must not execute below 50% — a
    /// threshold is a condition, and stacking conditions by addition turns two modest nodes into one
    /// absurd one. The kinder of the two wins, which is what a player expects and what keeps the
    /// combination bounded.
    /// </remarks>
    public static SkillShape Combine(SkillShape a, SkillShape b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var targets = new Dictionary<Style, int>(a.StyleTargets);
        foreach (var (style, n) in b.StyleTargets)
            targets[style] = targets.TryGetValue(style, out var have) ? have + n : n;

        var power = new Dictionary<Style, float>(a.StylePower);
        foreach (var (style, f) in b.StylePower)
            power[style] = power.TryGetValue(style, out var had) ? had * f : f;

        return new SkillShape
        {
            HitSize = a.HitSize * b.HitSize,
            VowPowerMultiplier = a.VowPowerMultiplier * b.VowPowerMultiplier,

            HaulPerCleanSecond = a.HaulPerCleanSecond + b.HaulPerCleanSecond,
            HaulCleanCap = Math.Max(a.HaulCleanCap, b.HaulCleanCap),
            HaulUntouchedWave = a.HaulUntouchedWave + b.HaulUntouchedWave,
            HaulWhenHurt = a.HaulWhenHurt + b.HaulWhenHurt,
            HaulPerWavePastDepth = a.HaulPerWavePastDepth + b.HaulPerWavePastDepth,
            HaulDepthFloor = Pick(a.HaulDepthFloor, b.HaulDepthFloor, lower: true),
            RarityFromClean = a.RarityFromClean + b.RarityFromClean,
            HaulEveryNthWave = Pick(a.HaulEveryNthWave, b.HaulEveryNthWave, lower: true),
            HaulNthWaveBonus = a.HaulNthWaveBonus + b.HaulNthWaveBonus,

            StrongMatchupBonus = a.StrongMatchupBonus + b.StrongMatchupBonus,
            WeakMatchupRelief = Math.Min(1f, a.WeakMatchupRelief + b.WeakMatchupRelief),
            AllMatchupsStrong = a.AllMatchupsStrong || b.AllMatchupsStrong,
            AffinityStyleBonus = a.AffinityStyleBonus + b.AffinityStyleBonus,
            OffStylePenalty = a.OffStylePenalty + b.OffStylePenalty,
            OppositePenaltyRelief = Math.Min(1f, a.OppositePenaltyRelief + b.OppositePenaltyRelief),
            ResonanceWorth = a.ResonanceWorth + b.ResonanceWorth,
            OneSourceBonus = a.OneSourceBonus + b.OneSourceBonus,
            ArmourPenetration = a.ArmourPenetration + b.ArmourPenetration,
            OverkillCarry = Math.Max(a.OverkillCarry, b.OverkillCarry),

            ExtraTargets = a.ExtraTargets + b.ExtraTargets,
            StyleTargets = targets,
            StylePower = power,

            FirstHitMultiplier = a.FirstHitMultiplier * b.FirstHitMultiplier,
            LaterHitMultiplier = a.LaterHitMultiplier * b.LaterHitMultiplier,
            CullThreshold = Math.Max(a.CullThreshold, b.CullThreshold),
            CullBonus = a.CullBonus + b.CullBonus,
            PerCreatureBonus = a.PerCreatureBonus + b.PerCreatureBonus,
            OpeningSeconds = Math.Max(a.OpeningSeconds, b.OpeningSeconds),
            OpeningBonus = a.OpeningBonus + b.OpeningBonus,
            AfterOpeningPenalty = a.AfterOpeningPenalty + b.AfterOpeningPenalty,
            InterruptBonus = a.InterruptBonus + b.InterruptBonus,
            DamagePerMaxHealth = a.DamagePerMaxHealth + b.DamagePerMaxHealth,
            HitSizePerMaxHealth = a.HitSizePerMaxHealth + b.HitSizePerMaxHealth,
            DamageDealt = a.DamageDealt * b.DamageDealt,

            SkillRate = a.SkillRate * b.SkillRate,
            FreeOpeningCast = a.FreeOpeningCast || b.FreeOpeningCast,
            BonusCritPercent = a.BonusCritPercent + b.BonusCritPercent,

            // ── THE SIX SET LADDERS. Each rung is one of these, and the fold is chosen per dial:
            //    percentage points add, multipliers multiply, ceilings and rules take the stronger. ──
            BonusCritDamagePercent = a.BonusCritDamagePercent + b.BonusCritDamagePercent,
            FocusPerHitPercent = a.FocusPerHitPercent + b.FocusPerHitPercent,
            FocusCapPercent = Math.Max(a.FocusCapPercent, b.FocusCapPercent),
            CertaintyAtFocusCap = a.CertaintyAtFocusCap || b.CertaintyAtFocusCap,
            ImpactSwingBonus = a.ImpactSwingBonus + b.ImpactSwingBonus,
            DeadweightShare = Math.Max(a.DeadweightShare, b.DeadweightShare),
            HealingMultiplier = a.HealingMultiplier * b.HealingMultiplier,
            HealCeilingBonus = a.HealCeilingBonus + b.HealCeilingBonus,
            OverhealToShield = Math.Max(a.OverhealToShield, b.OverhealToShield),
            ShadeActiveBonus = a.ShadeActiveBonus + b.ShadeActiveBonus,
            ShadeMax = Math.Max(a.ShadeMax, b.ShadeMax),
            AfterimageFraction = Math.Max(a.AfterimageFraction, b.AfterimageFraction),
            FirstActivationMagnitude = a.FirstActivationMagnitude * b.FirstActivationMagnitude,
            ResonanceRatePerSkill = a.ResonanceRatePerSkill + b.ResonanceRatePerSkill,
            ResonanceRateCap = Math.Max(a.ResonanceRateCap, b.ResonanceRateCap),
            HarmonyCharges = a.HarmonyCharges || b.HarmonyCharges,

            // SHIELD: the wave-start grants add, the shielded-damage multipliers multiply, and
            // prevention is a rule either half may carry.
            WaveStartShieldFraction = a.WaveStartShieldFraction + b.WaveStartShieldFraction,
            ShieldedDamageTaken = a.ShieldedDamageTaken * b.ShieldedDamageTaken,
            PreventFirstDamagingBite = a.PreventFirstDamagingBite || b.PreventFirstDamagingBite,
            AmplifyWindowMultiplier = a.AmplifyWindowMultiplier * b.AmplifyWindowMultiplier,
            AmplifyPowerBonus = a.AmplifyPowerBonus + b.AmplifyPowerBonus,
            AssassinateThreshold = Math.Max(a.AssassinateThreshold, b.AssassinateThreshold),
            AutoAttackRate = a.AutoAttackRate * b.AutoAttackRate,
            AutoAttackDamage = a.AutoAttackDamage * b.AutoAttackDamage,
            CastRampPerCast = a.CastRampPerCast + b.CastRampPerCast,
            CastRampMax = Math.Max(a.CastRampMax, b.CastRampMax),
            FirstCastMultiplier = a.FirstCastMultiplier * b.FirstCastMultiplier,
            LaterCastMultiplier = a.LaterCastMultiplier * b.LaterCastMultiplier,

            HealPerTargetStruck = a.HealPerTargetStruck + b.HealPerTargetStruck,
            FlatDamageReduction = a.FlatDamageReduction + b.FlatDamageReduction,
            DamageTaken = a.DamageTaken * b.DamageTaken,
            AbsorbAtLowHealth = Math.Max(a.AbsorbAtLowHealth, b.AbsorbAtLowHealth),
            ReflectFraction = a.ReflectFraction + b.ReflectFraction,
            HealOnClear = a.HealOnClear + b.HealOnClear,
            RegenFraction = a.RegenFraction + b.RegenFraction,
            BiteFuelBonus = a.BiteFuelBonus + b.BiteFuelBonus,
            BiteFuelMax = Math.Max(a.BiteFuelMax, b.BiteFuelMax),
            HealOnBiteFraction = a.HealOnBiteFraction + b.HealOnBiteFraction,
            BetweenWaveRegen = a.BetweenWaveRegen + b.BetweenWaveRegen,
            FullHealBetweenWaves = a.FullHealBetweenWaves || b.FullHealBetweenWaves,
            MaxHealth = a.MaxHealth * b.MaxHealth,

            // THE TRAITS, folded by their own rule. One line here and one record there, which is the
            // whole cost of keeping thirty dials out of this one's field list.
            Traits = TraitRules.Combine(a.Traits, b.Traits),
        };
    }

    /// <summary>Take whichever value is set, preferring the more generous when both are.</summary>
    private static float Pick(float a, float b, bool lower)
    {
        if (a <= 0f) return b;
        if (b <= 0f) return a;
        return lower ? Math.Min(a, b) : Math.Max(a, b);
    }

    /// <summary>The same rule for a whole-number dial (a wave count, a depth floor).</summary>
    private static int Pick(int a, int b, bool lower)
    {
        if (a <= 0) return b;
        if (b <= 0) return a;
        return lower ? Math.Min(a, b) : Math.Max(a, b);
    }

    public static SkillShape Sum(IEnumerable<SkillShape> shapes)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        var total = None;
        foreach (var s in shapes) total = Combine(total, s);
        return total;
    }
}
