using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Builds;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// VOWS — restriction bought as power. Moved here whole from Weaving/ResonanceWeaving.cs
// (2026-08-31): the Vow system was born inside the Source×Form weaving library and outlived it.
// Everything in this file is CURRENT architecture; the file it left is the legacy rump.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A restriction accepted in exchange for power. Vows are the whole point of the system.
/// </summary>
public enum VowKind
{
    /// <summary>
    /// Pays only if the BUILD meets its demand. Harsher demand = more power.
    /// </summary>
    /// <remarks>
    /// Named for what it reads. It was Conditional when the condition was a moment in the fight; the
    /// distinction that matters now is that a demand is answered at the workbench, before the descent,
    /// and holds for every wave of it.
    /// </remarks>
    Demand,

    /// <summary>Always active, always costing you something. Priced on effective-HP given up.</summary>
    StaticCost,
}

/// <summary>
/// What a Vow demands of the BUILD. The machine-readable half of the restriction.
/// </summary>
/// <remarks>
/// <para>
/// These used to watch the FIGHT — below 40% health, against a boss, after ten seconds. In a game with
/// no in-run decisions that is not a restriction a player accepts, it is a lottery on how the fight
/// happens to go: whether the Vow paid at all was decided by the wave, not by them. A player who cannot
/// tell "I built this wrong" from "that wave went badly" is the failure the whole flow document is
/// written against.
/// </para>
/// <para>
/// A build demand is the opposite. It is known before the descent starts, it is visible on the
/// workbench, it holds for every wave equally, and the post-run report can state it as a fact. Most
/// importantly it PULLS AGAINST THE BUILD'S OTHER AXES: a Vow that pays for carrying one Style is an
/// argument against breadth, and one that pays for owning no crit is an argument against TEMPO.
/// </para>
/// </remarks>
public enum VowDemand
{
    /// <summary>No demand — a static-cost Vow, always on and always paying.</summary>
    None,

    /// <summary>
    /// Every woven skill belongs to the same STYLE. Argues against breadth.
    /// </summary>
    /// <remarks>
    /// Was <c>SingleForm</c> until 2026-08-31. Provably the same verdict for every build a player
    /// could save under the old model — a Style's two skills shared one Form through the save's
    /// (Form, Passive) round trip, so "one Form" and "one Style" always agreed. The rename is the
    /// honest current name, not a behaviour change.
    /// </remarks>
    SingleStyle,

    /// <summary>Every woven skill draws the same Source. Costs you the Source matchup wheel.</summary>
    SingleSource,

    /// <summary>No skill slot is empty. A real cost only while slots are scarce.</summary>
    EverySlotFilled,

    /// <summary>Critical chance is untouched from base. Argues directly against TEMPO's FOCUS road.</summary>
    NoCritInvestment,

    /// <summary>Skill rate at or below the threshold. The slow, heavy build's Vow.</summary>
    CadenceAtOrBelow,

    /// <summary>Skill rate at or above the threshold. The frantic build's Vow.</summary>
    CadenceAtOrAbove,

    /// <summary>Defence is zero — no training, no charm, no affix. Nothing between you and the wave.</summary>
    NoDefence,

    /// <summary>A named gear slot must be EMPTY. The purest sacrifice the game can ask.</summary>
    /// <remarks>
    /// It costs the slot's stats AND its enchantment AND its affixes, all of which the player can see
    /// before agreeing. Priced highest for that reason, and the only demand that touches the gear layer.
    /// </remarks>
    SlotLeftBare,

    /// <summary>No keystone socketed. Refuses every doctrine the world has handed over.</summary>
    NoKeystone,
}

/// <summary>Which slot a <see cref="VowDemand.SlotLeftBare"/> Vow forbids.</summary>
/// <remarks>
/// A local enum rather than Economy.GearSlot: the Vow layer must not depend on the economy layer, and
/// the host maps this to the real slot when it builds the context. Only the slots worth refusing are here.
/// </remarks>
public enum BareSlot { None, Boots, Gloves, Helm, Ring, Charm }

/// <summary>How a Vow is found.</summary>
/// <remarks>
/// A Vow used to be BOUGHT: five nodes on the trait tree taught all thirteen between them, so the
/// whole system was invisible to a player who spent their points elsewhere. Now a Vow reveals itself
/// when the player has already kept its rule without it — which is the only discovery rule that
/// teaches the thing it unlocks.
/// </remarks>
public enum VowProof
{
    /// <summary>Handed over with the BUILD screen, so the system is discoverable at all.</summary>
    Granted,

    /// <summary>Proved by keeping its own demand for a run, with the Vow unsworn.</summary>
    Demand,

    /// <summary>
    /// Proved by CONDUCT — you had already accepted the cost this Vow charges, from somewhere else.
    /// </summary>
    /// <remarks>
    /// The two static-cost Vows have no demand to keep: you cannot voluntarily take 12.5% more damage
    /// or give up 15% of your health pool — there is no dial for it, and <see cref="VowDemand.None"/>
    /// says so, since a static Vow's demand is always met. Rather than invent a fake restriction they
    /// ask for the honest version of the same sentence: you had already agreed to be hurt, and now the
    /// game offers to pay you for it. Both are still intentional build choices — a mastery greater, a
    /// socketed keystone — both deterministic and both reversible.
    /// </remarks>
    Conduct,
}

/// <summary>
/// What the account had to OWN for a kept rule to count as a restriction.
/// </summary>
/// <remarks>
/// This clause is what makes a discovery a restriction rather than a starting condition. A brand-new
/// hunter has one slot, one skill, one Source, no crit training, no defence, no keystone and no gear —
/// which satisfies ten of the eleven demands on wave one. You cannot obey a rule you were never able
/// to break, so a proof only counts once the player owned the thing they refused.
/// </remarks>
public enum VowTemptation
{
    /// <summary>Nothing to own. Used only where the demand cannot be met by accident.</summary>
    None,

    /// <summary>You own an item carrying the very stat you refused. See <see cref="Vow.TemptationAffix"/>.</summary>
    AnAffixInHand,

    /// <summary>You own an item for the gear slot you left empty.</summary>
    GearForTheBareSlot,

    /// <summary>Your mastery reaches skills of two or more styles, and you carried one style anyway.</summary>
    TwoStylesInReach,

    /// <summary>Every woven skill has a chosen variation, so its Source was a decision and not a default.</summary>
    EveryWovenSourceChosen,

    /// <summary>The world has already given you at least one keystone.</summary>
    AKeystoneKnown,
}

/// <summary>
/// What the account owned on the run being judged — the temptation half of a Vow's proof.
/// </summary>
/// <remarks>
/// Read ONCE, at the end of a descent, beside the build that ran it. Every field defaults to false or
/// zero, which is a brand-new account, so a caller naming only what it means gets the safe answer for
/// everything else rather than a compile error pushing it toward numbers it does not mean.
/// </remarks>
public readonly record struct VowTemptationFacts(
    bool OwnsCritItem = false,
    bool OwnsSkillRateItem = false,
    bool OwnsDefenceItem = false,
    bool OwnsDamageItem = false,
    bool OwnsHealthItem = false,
    bool OwnsHaulItem = false,
    bool OwnsBoots = false,
    bool OwnsGloves = false,
    bool OwnsHelm = false,
    bool OwnsRing = false,
    bool OwnsCharm = false,
    bool EveryWovenSourceChosen = false,
    int StylesInReach = 0,
    int KeystonesKnown = 0)
{
    /// <summary>Does the account own an item carrying this stat?</summary>
    public bool OwnsAffix(Economy.AffixStat stat) => stat switch
    {
        Economy.AffixStat.Crit => OwnsCritItem,
        Economy.AffixStat.SkillRate => OwnsSkillRateItem,
        Economy.AffixStat.Defense => OwnsDefenceItem,
        Economy.AffixStat.Damage => OwnsDamageItem,
        Economy.AffixStat.Health => OwnsHealthItem,
        Economy.AffixStat.Haul => OwnsHaulItem,
        _ => false,
    };

    /// <summary>Does the account own a piece for this slot?</summary>
    public bool OwnsGearFor(BareSlot slot) => slot switch
    {
        BareSlot.Boots => OwnsBoots,
        BareSlot.Gloves => OwnsGloves,
        BareSlot.Helm => OwnsHelm,
        BareSlot.Ring => OwnsRing,
        BareSlot.Charm => OwnsCharm,
        _ => false,
    };
}

public sealed record Vow
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required VowKind Kind { get; init; }

    /// <summary>What the Vow demands of the build. <see cref="VowDemand.None"/> for static-cost Vows.</summary>
    public VowDemand Demand { get; init; } = VowDemand.None;

    /// <summary>The demand's threshold — a skill-rate multiplier. Ignored by demands that take none.</summary>
    public float Threshold { get; init; }

    /// <summary>Which slot must be left empty, for <see cref="VowDemand.SlotLeftBare"/>.</summary>
    public BareSlot Bare { get; init; } = BareSlot.None;

    /// <summary>
    /// The restriction in a few words, for a UI row. <see cref="Description"/> is the prose version.
    /// </summary>
    /// <remarks>
    /// A separate field rather than a truncation: the Description is a full sentence sized for a
    /// tooltip, and at 480x270 it overran the row and collided with the numbers on both sides of it. A
    /// terse label is not the same string with fewer letters — it is a different piece of writing.
    /// </remarks>
    public required string Short { get; init; }

    /// <summary>
    /// Demand Vows only: 0..1, how much of the build space this demand forbids. Strictly inside (0,1).
    /// </summary>
    /// <remarks>
    /// This replaced EXPECTED UPTIME and prices the Vow the same way, because it is the same quantity
    /// asked honestly. Uptime made sense when a Vow watched the fight — how often is the condition true?
    /// A build demand is true for a whole descent or false for a whole descent, so the question becomes
    /// how much it COSTS you to satisfy, and that is what severity names. A demand nearly every build
    /// already meets is worth almost nothing; one that forbids a whole branch is worth a great deal.
    /// </remarks>
    public float Severity { get; init; } = 0.5f;

    /// <summary>Static-cost only: the fraction of EFFECTIVE HP sacrificed — never a raw stat fraction.</summary>
    public float StaticCostMagnitude { get; init; }

    /// <summary>Static-cost only: extra damage taken, applied AFTER mitigation. See remarks.</summary>
    /// <remarks>
    /// Applied post-mitigation, deliberately. The Hunter's defense starts at ZERO, so a cost expressed
    /// as "-20% of your defense" costs a player who never trained defense exactly nothing — the Vow
    /// would be free. A cost expressed as a fraction of an investable stat is always avoidable by
    /// declining the investment. You cannot decline to take damage.
    /// </remarks>
    public float DamageTakenIncrease { get; init; }

    public required string Description { get; init; }

    // ── Discovery. How this Vow reveals itself. ──────────────────────────────────────────────────

    /// <summary>How the player finds this Vow. See <see cref="VowProof"/>.</summary>
    public VowProof Proof { get; init; } = VowProof.Demand;

    /// <summary>
    /// How many CLEARED waves of one descent the rule must hold for. Zero for a granted Vow.
    /// </summary>
    /// <remarks>
    /// Cleared waves, never depth: a checkpoint sets the wave number from a purchase, so a player could
    /// buy a start at wave 40, die on 41, and claim a twenty-wave proof having fought nothing. It also
    /// scales with <see cref="Severity"/>, on the same logic the catalogue is priced by — a heavier
    /// restriction asks for a longer proof.
    /// </remarks>
    public int ProofWaves { get; init; }

    /// <summary>What the account had to own for the kept rule to count as a refusal.</summary>
    public VowTemptation Temptation { get; init; } = VowTemptation.None;

    /// <summary>For <see cref="VowTemptation.AnAffixInHand"/>: the stat the player owned and refused.</summary>
    public Economy.AffixStat? TemptationAffix { get; init; }

    /// <summary>
    /// The one plain past-tense sentence the reveal shows: what the player actually did.
    /// </summary>
    /// <remarks>
    /// This is the ONLY place a discovery rule is ever stated in the game, and it is stated after the
    /// fact. That is how hidden conditions and a discoverable system coexist: the player never reads a
    /// checklist, but every reveal teaches the grammar and lets them guess at the next one.
    /// </remarks>
    public string ProofLine { get; init; } = "";

    /// <summary>One sentence of flavour, shown under the reveal.</summary>
    public string RevealLine { get; init; } = "";
}

/// <summary>The Vow pricing curve's knobs — carved out of the retired WeavingTuning.</summary>
public sealed record VowTuning
{
    /// <summary>Demand Vows: the ceiling of the severity→power curve.</summary>
    public float MaxPowerBonus { get; init; } = 1.5f;
    public float CurveExponent { get; init; } = 1.0f;

    /// <summary>Static Vows: power granted per 1.0 of effective HP sacrificed.</summary>
    /// <remarks>
    /// 8.0, from 3.0 (2026-08-28). At 3.0 the two static-cost Vows were the only ones in the catalogue
    /// a player was WRONG to swear: on the vow sweep's mid-career fixture, FRAGILITY and RECKLESS
    /// OFFERING each cost depth while every demand Vow paid. The reason is structural, not a mispriced
    /// fraction: a static Vow charges the WHOLE CHAMPION (health, or damage taken) and pays only the
    /// SKILLS, and under the beat model the basic attack is about a third of a build's damage. Swept on
    /// the MEAN over forty seeds (median is quantised to whole waves and could not resolve the
    /// question); 8.0 lands both Vows beside VOW OF COMPLETION, the weakest demand Vow, and keeps both
    /// multipliers inside the demand band that x2.20 tops.
    /// </remarks>
    public float StaticCostConversionRate { get; init; } = 8.0f;

    /// <summary>
    /// The most bonus a build may hold across every Vow it has sworn, added together.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Vow's bonus used to ride a SKILL SLOT, and each skill took exactly one Vow's multiplier — so
    /// four different Vows paid the same per-skill bonus as one Vow repeated on all four slots, while
    /// charging four prices and demanding four restrictions hold at once. The rational play was to
    /// satisfy the single harshest restriction you could and bind that one Vow everywhere, which made
    /// BIND TO SLOT a decision with one correct answer, told four times.
    /// </para>
    /// <para>
    /// So a Vow is a promise about the BUILD, capacity is a real number, and the bonuses of the Vows
    /// sworn are SUMMED under this ceiling. 1.35 sits just above the single best Vow in the catalogue
    /// (+1.20), so a second and third Vow are worth swearing and cannot run away: three maximal Vows
    /// plus every Vow-power multiplier in the game land on one bounded number a sweep can print. The
    /// clamp is applied BEFORE <c>SkillShape.VowPowerMultiplier</c>, so mastery and THE OATHBOUND still
    /// pay for their investment.
    /// </para>
    /// </remarks>
    public float CombinedBonusCeiling { get; init; } = 1.35f;

    public static VowTuning Default { get; } = new();
}

/// <summary>
/// The BUILD a Vow's demand is judged against. Pure data — no MonoGame, no sim internals.
/// </summary>
/// <remarks>
/// It used to carry the fight — health fraction, elapsed milliseconds, whether the enemy was a boss —
/// because Vows watched the fight. Every field here is instead something the player SET before they
/// descended, which is what makes a Vow a decision rather than a lottery on how the wave went.
///
/// Static for a whole descent, so the sim builds one per wave and reuses it for every skill.
/// </remarks>
public readonly record struct BuildContext(
    int DistinctStyles,
    int DistinctSources,
    int SkillsWoven,
    int SkillSlots,
    float CritPercent,
    float BaseCritPercent,
    float SkillRate,
    int Defence,
    int KeystonesWorn,
    IReadOnlySet<BareSlot> WornSlots,
    // What the build already does to damage taken. Above 1 means something in it is hurting you —
    // read by VOW OF FRAGILITY's conduct proof, and by nothing else.
    float DamageTakenMultiplier = 1f,
    // The health multiplier the SOCKETED KEYSTONES alone contribute. Below 1 means a doctrine already
    // cost you maximum health — read by RECKLESS OFFERING's conduct proof, and by nothing else. The
    // keystone contribution alone, so training can never mask it.
    float KeystoneHealthMultiplier = 1f)
{
    /// <summary>A build that satisfies nothing — the safe default for a caller with no build to hand.</summary>
    public static BuildContext Empty { get; } =
        new(0, 0, 0, 0, 0f, 0f, 1f, 0, 0, new HashSet<BareSlot>());
}

public static class Vows
{
    /// <summary>
    /// Formula 1 — a demand Vow's multiplier. Rarer condition, more power.
    /// </summary>
    /// <remarks>
    /// Strictly decreasing in uptime, so no demand Vow can dominate another: a Vow that is easier
    /// to satisfy is always worth less. Uptime must be strictly inside (0,1) — a Vow that is always on
    /// is not a restriction, and one that is never on is not an ability.
    /// </remarks>
    public static float ConditionalMultiplier(float expectedUptime, VowTuning? tuning = null)
    {
        tuning ??= VowTuning.Default;
        if (expectedUptime is <= 0f or >= 1f)
            throw new ArgumentOutOfRangeException(nameof(expectedUptime), "Uptime must be strictly inside (0,1).");

        return 1f + tuning.MaxPowerBonus * MathF.Pow(1f - expectedUptime, tuning.CurveExponent);
    }

    /// <summary>
    /// Formula 2 — a static-cost Vow's multiplier, priced on EFFECTIVE HP given up.
    /// </summary>
    /// <remarks>
    /// The magnitude is an effective-HP fraction, never a raw stat fraction. Two Vows that sacrifice
    /// "20%" of two different stats are not paying comparable prices — mitigation is hyperbolic, so
    /// 20% of defense costs far less survivability than 20% of health. Pricing off the raw fraction
    /// silently rewards whichever stat has the flatter curve, which is exactly how vow_fragility ended
    /// up strictly dominant.
    /// </remarks>
    public static float StaticMultiplier(float staticCostMagnitude, VowTuning? tuning = null)
        => 1f + (tuning ?? VowTuning.Default).StaticCostConversionRate * staticCostMagnitude;

    public static float Multiplier(Vow? vow, VowTuning? tuning = null)
    {
        if (vow is null) return 1f;

        return vow.Kind == VowKind.Demand
            // 1 - Severity, because the formula prices RARITY: a lower argument means a condition that
            // holds less often and therefore pays more. Severity runs the other way by design — it names
            // how much the demand COSTS you, which is the honest question for a build condition — so it
            // is inverted here rather than the catalogue being written backwards to suit the formula.
            ? ConditionalMultiplier(1f - vow.Severity, tuning)
            : StaticMultiplier(vow.StaticCostMagnitude, tuning);
    }

    /// <summary>
    /// The Vow catalogue. Every demand argues against a real axis of the build, which is the point: a
    /// Vow that costs you nothing you wanted is not a Vow.
    /// </summary>
    public static IReadOnlyList<Vow> Catalog { get; } = new List<Vow>
    {
        // ── BUILD SHAPE. ─────────────────────────────────────────────────────────────────────────
        new()
        {
            Id = "vow_singular", Name = "VOW OF THE SINGULAR", Kind = VowKind.Demand,
            Demand = VowDemand.SingleStyle, Severity = 0.75f,
            Short = "ONE STYLE ONLY",
            Description = "EVERY SKILL YOU CARRY MUST BE THE SAME STYLE. GO DEEP, NOT WIDE.",
            Proof = VowProof.Demand, ProofWaves = 15,
            Temptation = VowTemptation.TwoStylesInReach,
            ProofLine = "YOU CLEARED 15 WAVES CARRYING ONE STYLE OF SKILL ONLY, AND LEFT SLOTS EMPTY "
                        + "RATHER THAN FILL THEM WITH ANYTHING ELSE.",
            RevealLine = "YOU WENT DEEP WHERE THE ROAD WAS NARROW.",
        },
        new()
        {
            Id = "vow_pure", Name = "VOW OF THE PURE", Kind = VowKind.Demand,
            Demand = VowDemand.SingleSource, Severity = 0.6f,
            Short = "ONE SOURCE ONLY",
            Description = "EVERY SKILL MUST USE ONE SOURCE. NO PICKING WHAT THE ENEMY IS WEAK TO.",
            Proof = VowProof.Demand, ProofWaves = 12,
            Temptation = VowTemptation.EveryWovenSourceChosen,
            ProofLine = "YOU CLEARED 12 WAVES WITH EVERY SKILL DRAWING ONE SOURCE, AND YOU CHOSE EACH "
                        + "OF THOSE SOURCES YOURSELF.",
            RevealLine = "YOU SPOKE IN ONE VOICE LONG BEFORE ANYONE WAS LISTENING.",
        },
        new()
        {
            // The one Vow most builds already satisfy, and priced accordingly — it exists so the
            // catalogue has an entry a new player can take without giving anything up yet.
            Id = "vow_complete", Name = "VOW OF COMPLETION", Kind = VowKind.Demand,
            Demand = VowDemand.EverySlotFilled, Severity = 0.2f,
            Short = "NO EMPTY SLOT",
            Description = "EVERY SKILL SLOT YOU OWN MUST BE FILLED. NOTHING HELD BACK.",
            // THE ONE VOW THAT IS SIMPLY GIVEN. Its own severity is why: most builds already meet
            // it, so it costs almost nothing to keep and teaches the whole grammar in one card —
            // fill your slots and be paid, break the rule and be paid nothing. Without it a player
            // could reach the end of the game without ever learning that Vows exist.
            Proof = VowProof.Granted,
            ProofLine = "FILL EVERY SKILL SLOT YOU OWN AND EVERY SKILL HITS 30% HARDER. LEAVE ONE "
                        + "EMPTY AND THE VOW PAYS NOTHING.",
            RevealLine = "THE FIRST PROMISE IS THE EASY ONE. THAT IS WHY IT IS FIRST.",
        },

        // ── STAT SHAPE. These read the character sheet, so they pull against the STATS screen's
        //    training and against the mastery tree's numbered nodes at the same time. ─────────────
        new()
        {
            Id = "vow_bluntedge", Name = "VOW OF THE BLUNT EDGE", Kind = VowKind.Demand,
            Demand = VowDemand.NoCritInvestment, Severity = 0.55f,
            Short = "NO CRITICAL BONUS",
            Description = "YOUR CRITICAL CHANCE MUST BE UNTOUCHED. NO LUCKY HITS, ONLY SURE ONES.",
            Proof = VowProof.Demand, ProofWaves = 12,
            Temptation = VowTemptation.AnAffixInHand, TemptationAffix = Economy.AffixStat.Crit,
            ProofLine = "YOU CLEARED 12 WAVES WITH YOUR CRITICAL CHANCE UNTOUCHED, THOUGH YOU OWNED "
                        + "GEAR THAT WOULD HAVE RAISED IT.",
            RevealLine = "NO LUCKY HITS. ONLY SURE ONES.",
        },
        new()
        {
            Id = "vow_deliberate", Name = "VOW OF THE DELIBERATE", Kind = VowKind.Demand,
            Demand = VowDemand.CadenceAtOrBelow, Threshold = 1.0f, Severity = 0.5f,
            Short = "SKILL RATE MAX 1.00x",
            Description = "YOUR SKILLS MAY NOT BE SPED UP AT ALL. SLOW HANDS, HEAVY BLOWS.",
            Proof = VowProof.Demand, ProofWaves = 10,
            Temptation = VowTemptation.AnAffixInHand, TemptationAffix = Economy.AffixStat.SkillRate,
            ProofLine = "YOU CLEARED 10 WAVES AT SKILL RATE 1.00x, THOUGH YOU OWNED GEAR THAT WOULD "
                        + "HAVE MADE YOUR SKILLS COME BACK SOONER.",
            RevealLine = "SLOW HANDS. HEAVY BLOWS.",
        },
        new()
        {
            Id = "vow_frantic", Name = "VOW OF THE FRANTIC", Kind = VowKind.Demand,
            Demand = VowDemand.CadenceAtOrAbove, Threshold = 1.4f, Severity = 0.5f,
            Short = "SKILL RATE MIN 1.40x",
            Description = "YOUR SKILLS MUST BE SPED UP BY 40% OR MORE. NEVER STILL.",
            // NO TEMPTATION CLAUSE: a skill rate of 1.40x cannot be reached by accident. It is
            // itself the proof that the player went and built for it.
            Proof = VowProof.Demand, ProofWaves = 10, Temptation = VowTemptation.None,
            ProofLine = "YOU CLEARED 10 WAVES WITH YOUR SKILLS SPED UP BY 40% OR MORE.",
            RevealLine = "YOU NEVER STOOD STILL LONG ENOUGH TO BE FOUND.",
        },
        new()
        {
            Id = "vow_unguarded", Name = "VOW OF THE UNGUARDED", Kind = VowKind.Demand,
            Demand = VowDemand.NoDefence, Severity = 0.7f,
            Short = "NO DEFENCE AT ALL",
            Description = "NO DEFENCE FROM TRAINING OR GEAR. NOTHING BETWEEN YOU AND THE WAVE.",
            Proof = VowProof.Demand, ProofWaves = 15,
            Temptation = VowTemptation.AnAffixInHand, TemptationAffix = Economy.AffixStat.Defense,
            ProofLine = "YOU CLEARED 15 WAVES WITH NO DEFENCE AT ALL, THOUGH YOU OWNED GEAR THAT "
                        + "WOULD HAVE GIVEN YOU SOME.",
            RevealLine = "NOTHING BETWEEN YOU AND THE WAVE, AND THE WAVE NOTICED.",
        },
        new()
        {
            Id = "vow_unbound", Name = "VOW OF THE UNBOUND", Kind = VowKind.Demand,
            Demand = VowDemand.NoKeystone, Severity = 0.8f,
            Short = "NO KEYSTONE IN USE",
            // The old line read "YOU GIVE UP THE TRAIT TREE'S PRIZE." Keystones come from the world
            // now, not from a tree, so the sentence had to name what the player is actually refusing.
            Description = "YOU MAY WEAR NO KEYSTONE. THE WORLD'S DOCTRINES ARE NOT FOR YOU.",
            Proof = VowProof.Demand, ProofWaves = 20,
            Temptation = VowTemptation.AKeystoneKnown,
            ProofLine = "YOU CLEARED 20 WAVES WITH NO KEYSTONE SOCKETED, THOUGH THE WORLD HAD "
                        + "ALREADY GIVEN YOU ONE.",
            RevealLine = "YOU WERE HANDED A DOCTRINE, AND YOU SET IT DOWN.",
        },

        // ── SACRIFICE. A bare slot costs its stats AND its enchantment AND its affixes, all of which
        //    the player can see before agreeing — which is what makes it the purest ask here. ─────
        new()
        {
            Id = "vow_barefoot", Name = "VOW OF THE BAREFOOT", Kind = VowKind.Demand,
            Demand = VowDemand.SlotLeftBare, Bare = BareSlot.Boots, Severity = 0.65f,
            Short = "NO BOOTS",
            Description = "YOU MAY WEAR NO BOOTS. WALK THE DEPTHS ON YOUR OWN FEET.",
            Proof = VowProof.Demand, ProofWaves = 15,
            Temptation = VowTemptation.GearForTheBareSlot,
            ProofLine = "YOU CLEARED 15 WAVES WITH NO BOOTS, THOUGH YOU OWNED A PAIR.",
            RevealLine = "YOU WALKED THE DEPTHS ON YOUR OWN FEET.",
        },
        new()
        {
            Id = "vow_openhand", Name = "VOW OF THE OPEN HAND", Kind = VowKind.Demand,
            Demand = VowDemand.SlotLeftBare, Bare = BareSlot.Gloves, Severity = 0.65f,
            Short = "NO GLOVES",
            Description = "YOU MAY WEAR NO GLOVES. NOTHING BETWEEN YOUR HANDS AND THE WORK.",
            Proof = VowProof.Demand, ProofWaves = 15,
            Temptation = VowTemptation.GearForTheBareSlot,
            ProofLine = "YOU CLEARED 15 WAVES WITH NO GLOVES, THOUGH YOU OWNED A PAIR.",
            RevealLine = "NOTHING BETWEEN YOUR HANDS AND THE WORK.",
        },
        new()
        {
            Id = "vow_bareskull", Name = "VOW OF THE BARE SKULL", Kind = VowKind.Demand,
            Demand = VowDemand.SlotLeftBare, Bare = BareSlot.Helm, Severity = 0.7f,
            Short = "NO HELM",
            Description = "YOU MAY WEAR NO HELM. LOOK THE DEPTHS IN THE FACE.",
            Proof = VowProof.Demand, ProofWaves = 15,
            Temptation = VowTemptation.GearForTheBareSlot,
            ProofLine = "YOU CLEARED 15 WAVES WITH NO HELM, THOUGH YOU OWNED ONE.",
            RevealLine = "YOU LOOKED THE DEPTHS IN THE FACE.",
        },

        // ── STATIC COST. Always on, always paying — the entire distinction from a demand. ────────
        new()
        {
            Id = "vow_fragility", Name = "VOW OF FRAGILITY", Kind = VowKind.StaticCost,
            StaticCostMagnitude = 0.111f,   // eHP fraction — invariant across every build
            DamageTakenIncrease = 0.125f,   // +12.5% damage taken, applied AFTER mitigation
            Short = "ALWAYS: TAKES +12.5%",
            Description = "ALWAYS ON. YOU TAKE 12.5% MORE DAMAGE.",
            // CONDUCT, not a demand: you cannot volunteer to take more damage, so the proof is that
            // you had already accepted that cost from somewhere else. Three mastery greaters raise
            // damage taken, and every one of them is a deliberate purchase.
            Proof = VowProof.Conduct, ProofWaves = 15, Temptation = VowTemptation.None,
            ProofLine = "YOU CLEARED 15 WAVES CARRYING SOMETHING THAT ALREADY MADE YOU TAKE MORE DAMAGE.",
            RevealLine = "YOU HAD AGREED TO BLEED. NOW YOU WILL BE PAID FOR IT.",
        },
        new()
        {
            Id = "vow_reckless_offering", Name = "RECKLESS OFFERING", Kind = VowKind.StaticCost,
            StaticCostMagnitude = 0.15f,    // -15% max health == -15% eHP, linearly
            Short = "ALWAYS: -15% MAX HEALTH",
            Description = "ALWAYS ON. YOU LOSE 15% OF YOUR MAXIMUM HEALTH.",
            // CONDUCT: you ran a descent with a keystone that had already cost you maximum health —
            // GLASS CANNON, BLOODLUST or DYNAMO. The keystone contribution alone is read, so training
            // more health can never mask it.
            Proof = VowProof.Conduct, ProofWaves = 15, Temptation = VowTemptation.AKeystoneKnown,
            ProofLine = "YOU CLEARED 15 WAVES WITH A KEYSTONE THAT ALREADY COST YOU MAXIMUM HEALTH.",
            RevealLine = "YOU GAVE PART OF YOURSELF AWAY, AND CAME BACK ANYWAY.",
        },
    };

    public static Vow? ById(string? id) => id is null ? null : Catalog.FirstOrDefault(v => v.Id == id);

    /// <summary>
    /// Does a Vow's demand hold for this build?
    /// </summary>
    /// <remarks>
    /// Static-cost Vows are always active — they are paying whether or not they are useful, which is the
    /// entire distinction from a demand.
    /// </remarks>
    public static bool IsActive(Vow? vow, BuildContext ctx)
    {
        if (vow is null) return false;
        if (vow.Kind == VowKind.StaticCost) return true;

        return vow.Demand switch
        {
            VowDemand.SingleStyle => ctx.DistinctStyles <= 1,
            VowDemand.SingleSource => ctx.DistinctSources <= 1,
            VowDemand.EverySlotFilled => ctx.SkillsWoven >= ctx.SkillSlots,
            VowDemand.NoCritInvestment => ctx.CritPercent <= ctx.BaseCritPercent + 0.01f,
            VowDemand.CadenceAtOrBelow => ctx.SkillRate <= vow.Threshold + 0.001f,
            VowDemand.CadenceAtOrAbove => ctx.SkillRate >= vow.Threshold - 0.001f,
            VowDemand.NoDefence => ctx.Defence <= 0,
            VowDemand.NoKeystone => ctx.KeystonesWorn == 0,
            VowDemand.SlotLeftBare => !ctx.WornSlots.Contains(vow.Bare),
            _ => false,
        };
    }

    // ── DISCOVERY — proof before reward ───────────────────────────────────────────────────────────
    //
    //     A vow reveals itself when a descent ends in which its restriction held, for enough cleared
    //     waves, on a build that had something to break it with — and the vow was not sworn.
    //
    // Four clauses, one function, one call site. Nothing here is a roll, a drop or a missable event:
    // every clause is a build property or an owned-item test, so a vow can be proved again at any
    // point in the career, in any region, for as long as the game runs.

    /// <summary>The Vow the BUILD screen hands over, so the system is discoverable at all.</summary>
    public static IReadOnlyList<Vow> Granted { get; } =
        Catalog.Where(v => v.Proof == VowProof.Granted).ToList();

    /// <summary>Every Vow that has to be proved.</summary>
    public static IReadOnlyList<Vow> Discoverable { get; } =
        Catalog.Where(v => v.Proof != VowProof.Granted).ToList();

    /// <summary>
    /// Did the player own the thing this Vow's restriction refuses?
    /// </summary>
    /// <remarks>
    /// Without this clause, ten of the eleven demand Vows unlock on a brand-new hunter's first descent:
    /// one slot, one skill, one Source, no crit, no defence, no keystones and no gear satisfies almost
    /// the whole catalogue by simply not having anything yet.
    /// </remarks>
    public static bool WasTempted(Vow vow, VowTemptationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(vow);

        return vow.Temptation switch
        {
            VowTemptation.None => true,
            VowTemptation.AnAffixInHand => vow.TemptationAffix is { } stat && facts.OwnsAffix(stat),
            VowTemptation.GearForTheBareSlot => facts.OwnsGearFor(vow.Bare),
            VowTemptation.TwoStylesInReach => facts.StylesInReach >= 2,
            VowTemptation.EveryWovenSourceChosen => facts.EveryWovenSourceChosen,
            VowTemptation.AKeystoneKnown => facts.KeystonesKnown >= 1,
            _ => false,
        };
    }

    /// <summary>
    /// Did this build keep the rule this Vow is about, this wave?
    /// </summary>
    /// <remarks>
    /// A demand Vow asks <see cref="IsActive"/>, which is exactly the question the fight already asks
    /// of a sworn one — the discovery machine for eleven of the thirteen was already written. The two
    /// static-cost Vows have no demand to keep, so they ask the conduct question instead.
    /// </remarks>
    public static bool RuleHeld(Vow? vow, BuildContext ctx)
    {
        if (vow is null) return false;

        return vow.Proof switch
        {
            VowProof.Granted => false,
            // FRAGILITY: something in the build was already making you take more damage.
            // RECKLESS OFFERING: a socketed keystone had already cost you maximum health.
            VowProof.Conduct => vow.Id == "vow_reckless_offering"
                ? ctx.KeystoneHealthMultiplier < 0.999f
                : ctx.DamageTakenMultiplier > 1.001f,
            _ => IsActive(vow, ctx),
        };
    }

    /// <summary>
    /// Which Vows a finished descent reveals: the rule held long enough, unsworn, with something to break.
    /// </summary>
    /// <param name="proofWaves">Cleared waves per Vow id, counted during the descent.</param>
    /// <param name="sworn">The Vows the build actually swore — those prove nothing.</param>
    /// <param name="known">What the account already knows; already-known Vows are never re-revealed.</param>
    /// <param name="facts">What the account owned, for the temptation clause.</param>
    public static IReadOnlyList<Vow> Revealed(
        IReadOnlyDictionary<string, int> proofWaves,
        IEnumerable<string> sworn,
        IEnumerable<string> known,
        VowTemptationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(proofWaves);
        ArgumentNullException.ThrowIfNull(sworn);
        ArgumentNullException.ThrowIfNull(known);

        var swornIds = sworn.ToHashSet(StringComparer.Ordinal);
        var knownIds = known.ToHashSet(StringComparer.Ordinal);
        var found = new List<Vow>();

        foreach (var vow in Discoverable)
        {
            if (knownIds.Contains(vow.Id)) continue;          // nobody discovers the same thing twice
            if (swornIds.Contains(vow.Id)) continue;          // a Vow you wore proves nothing
            if (!WasTempted(vow, facts)) continue;            // you must have owned what you refused
            if (proofWaves.GetValueOrDefault(vow.Id) < vow.ProofWaves) continue;
            found.Add(vow);
        }
        return found;
    }

    // ── CAPACITY — a Vow is a promise about the BUILD ─────────────────────────────────────────────

    /// <summary>
    /// What every sworn Vow pays this build together, as a multiplier on a skill's damage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The BONUSES are summed and then clamped, never the factors multiplied: two Vows worth x1.90 and
    /// x1.75 pay +0.90 and +0.75, which is +1.65 before the ceiling, not x3.32. A Vow whose demand is
    /// BROKEN pays nothing at all and is simply skipped — a promise you did not keep is not a promise.
    /// </para>
    /// <para>
    /// The clamp sits BEFORE the Vow-power multiplier, so mastery's PLEDGE and ZEALOT and THE
    /// OATHBOUND's own passive still pay for the investment they cost, and the strongest possible Vow
    /// build in the game is one bounded number rather than a stack nobody can measure.
    /// </para>
    /// </remarks>
    /// <summary>
    /// What ONE kept Vow pays THIS build: its catalogue multiplier with the build's Vow-power
    /// (mastery's PLEDGE and ZEALOT, THE OATHBOUND's TWICE SWORN) applied to the bonus.
    /// </summary>
    /// <remarks>
    /// The screens printed <see cref="Multiplier"/> — the catalogue's own number — so THE OATHBOUND
    /// switched in and every "HOLDS x1.30" on the BUILD screen stayed x1.30 while the sim paid x1.45.
    /// A displayed value is the FINAL resolved value or it is a lie (2026-09-06). The same arithmetic
    /// <see cref="CombinedFactor"/> applies to the summed bonus, applied to one Vow's bonus alone.
    /// </remarks>
    public static float ResolvedMultiplier(Vow? vow, float vowPowerMultiplier, VowTuning? tuning = null)
        => vow is null ? 1f : 1f + (Multiplier(vow, tuning) - 1f) * MathF.Max(0f, vowPowerMultiplier);

    public static float CombinedFactor(
        IEnumerable<Vow?>? sworn, BuildContext ctx, float vowPowerMultiplier = 1f, VowTuning? tuning = null)
    {
        if (sworn is null) return 1f;
        tuning ??= VowTuning.Default;

        var bonus = 0f;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var vow in sworn)
        {
            // ONCE PER VOW, however many slots carry it. A Vow is sworn, not equipped.
            if (vow is null || !seen.Add(vow.Id)) continue;
            // A promise you are not keeping pays NOTHING. Restriction buys power, so a restriction
            // you are not accepting buys none of it — there is no partial credit anywhere in here.
            if (!IsActive(vow, ctx)) continue;
            bonus += Multiplier(vow, tuning) - 1f;
        }
        if (bonus <= 0f) return 1f;

        return 1f + MathF.Min(tuning.CombinedBonusCeiling, bonus) * MathF.Max(0f, vowPowerMultiplier);
    }

    /// <summary>
    /// How many DIFFERENT Vows this build is sworn to and actually keeping.
    /// </summary>
    /// <remarks>
    /// The count THE WEIGHT OF VOWS is paid on, and the same count the trait's discovery rule is fed
    /// from at the end of a cleared wave — one question, asked one way, so what earns the trait and
    /// what the trait pays for cannot drift apart. Distinct, because a promise made twice is one
    /// promise; kept, because restriction buys power and a broken restriction buys none of it.
    /// </remarks>
    public static int KeptCount(IEnumerable<Vow?>? sworn, BuildContext ctx)
    {
        if (sworn is null) return 0;

        var kept = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var vow in sworn)
        {
            if (vow is null || !seen.Add(vow.Id)) continue;
            if (IsActive(vow, ctx)) kept++;
        }
        return kept;
    }

    /// <summary>Is at least one of these Vows sworn AND kept? The affinity buy-back's question.</summary>
    /// <remarks>
    /// Restriction buys power — so a BROKEN Vow must buy nothing. The buy-back used to ask only
    /// whether a Vow had been NAMED at all, which paid an off-discipline skill's affinity back for
    /// merely CLAIMING a restriction the build was not keeping.
    /// </remarks>
    public static bool AnyKept(IEnumerable<Vow?>? sworn, BuildContext ctx)
        => KeptCount(sworn, ctx) > 0;
}
