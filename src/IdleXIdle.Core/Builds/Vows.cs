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
    EveryWeaveFilled,

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

    /// <summary>No keystone socketed. Refuses the trait tree's whole payoff.</summary>
    NoKeystone,
}

/// <summary>Which slot a <see cref="VowDemand.SlotLeftBare"/> Vow forbids.</summary>
/// <remarks>
/// A local enum rather than Economy.GearSlot: the Vow layer must not depend on the economy layer, and
/// the host maps this to the real slot when it builds the context. Only the slots worth refusing are here.
/// </remarks>
public enum BareSlot { None, Boots, Gloves, Helm, Ring, Charm }

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
public readonly record struct WeaveContext(
    int DistinctStyles,
    int DistinctSources,
    int SkillsWoven,
    int SkillSlots,
    float CritPercent,
    float BaseCritPercent,
    float SkillRate,
    int Defence,
    int KeystonesWorn,
    IReadOnlySet<BareSlot> WornSlots)
{
    /// <summary>A build that satisfies nothing — the safe default for a caller with no build to hand.</summary>
    public static WeaveContext Empty { get; } =
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
        },
        new()
        {
            Id = "vow_pure", Name = "VOW OF THE PURE", Kind = VowKind.Demand,
            Demand = VowDemand.SingleSource, Severity = 0.6f,
            Short = "ONE SOURCE ONLY",
            Description = "EVERY SKILL MUST USE ONE SOURCE. NO PICKING WHAT THE ENEMY IS WEAK TO.",
        },
        new()
        {
            // The one Vow most builds already satisfy, and priced accordingly — it exists so the
            // catalogue has an entry a new player can take without giving anything up yet.
            Id = "vow_complete", Name = "VOW OF COMPLETION", Kind = VowKind.Demand,
            Demand = VowDemand.EveryWeaveFilled, Severity = 0.2f,
            Short = "NO EMPTY SLOT",
            Description = "EVERY SKILL SLOT YOU OWN MUST BE FILLED. NOTHING HELD BACK.",
        },

        // ── STAT SHAPE. These read the character sheet, so they pull against the STATS screen's
        //    training and against the mastery tree's numbered nodes at the same time. ─────────────
        new()
        {
            Id = "vow_bluntedge", Name = "VOW OF THE BLUNT EDGE", Kind = VowKind.Demand,
            Demand = VowDemand.NoCritInvestment, Severity = 0.55f,
            Short = "NO CRITICAL BONUS",
            Description = "YOUR CRITICAL CHANCE MUST BE UNTOUCHED. NO LUCKY HITS, ONLY SURE ONES.",
        },
        new()
        {
            Id = "vow_deliberate", Name = "VOW OF THE DELIBERATE", Kind = VowKind.Demand,
            Demand = VowDemand.CadenceAtOrBelow, Threshold = 1.0f, Severity = 0.5f,
            Short = "SKILL RATE MAX 1.00x",
            Description = "YOUR SKILLS MAY NOT BE SPED UP AT ALL. SLOW HANDS, HEAVY BLOWS.",
        },
        new()
        {
            Id = "vow_frantic", Name = "VOW OF THE FRANTIC", Kind = VowKind.Demand,
            Demand = VowDemand.CadenceAtOrAbove, Threshold = 1.4f, Severity = 0.5f,
            Short = "SKILL RATE MIN 1.40x",
            Description = "YOUR SKILLS MUST BE SPED UP BY 40% OR MORE. NEVER STILL.",
        },
        new()
        {
            Id = "vow_unguarded", Name = "VOW OF THE UNGUARDED", Kind = VowKind.Demand,
            Demand = VowDemand.NoDefence, Severity = 0.7f,
            Short = "NO DEFENCE AT ALL",
            Description = "NO DEFENCE FROM TRAINING OR GEAR. NOTHING BETWEEN YOU AND THE WAVE.",
        },
        new()
        {
            Id = "vow_unbound", Name = "VOW OF THE UNBOUND", Kind = VowKind.Demand,
            Demand = VowDemand.NoKeystone, Severity = 0.8f,
            Short = "NO KEYSTONE IN USE",
            Description = "YOU MAY WEAR NO KEYSTONE. YOU GIVE UP THE TRAIT TREE'S PRIZE.",
        },

        // ── SACRIFICE. A bare slot costs its stats AND its enchantment AND its affixes, all of which
        //    the player can see before agreeing — which is what makes it the purest ask here. ─────
        new()
        {
            Id = "vow_barefoot", Name = "VOW OF THE BAREFOOT", Kind = VowKind.Demand,
            Demand = VowDemand.SlotLeftBare, Bare = BareSlot.Boots, Severity = 0.65f,
            Short = "NO BOOTS",
            Description = "YOU MAY WEAR NO BOOTS. WALK THE DEPTHS ON YOUR OWN FEET.",
        },
        new()
        {
            Id = "vow_openhand", Name = "VOW OF THE OPEN HAND", Kind = VowKind.Demand,
            Demand = VowDemand.SlotLeftBare, Bare = BareSlot.Gloves, Severity = 0.65f,
            Short = "NO GLOVES",
            Description = "YOU MAY WEAR NO GLOVES. NOTHING BETWEEN YOUR HANDS AND THE WORK.",
        },
        new()
        {
            Id = "vow_bareskull", Name = "VOW OF THE BARE SKULL", Kind = VowKind.Demand,
            Demand = VowDemand.SlotLeftBare, Bare = BareSlot.Helm, Severity = 0.7f,
            Short = "NO HELM",
            Description = "YOU MAY WEAR NO HELM. LOOK THE DEPTHS IN THE FACE.",
        },

        // ── STATIC COST. Always on, always paying — the entire distinction from a demand. ────────
        new()
        {
            Id = "vow_fragility", Name = "VOW OF FRAGILITY", Kind = VowKind.StaticCost,
            StaticCostMagnitude = 0.111f,   // eHP fraction — invariant across every build
            DamageTakenIncrease = 0.125f,   // +12.5% damage taken, applied AFTER mitigation
            Short = "ALWAYS: TAKES +12.5%",
            Description = "ALWAYS ON. YOU TAKE 12.5% MORE DAMAGE.",
        },
        new()
        {
            Id = "vow_reckless_offering", Name = "RECKLESS OFFERING", Kind = VowKind.StaticCost,
            StaticCostMagnitude = 0.15f,    // -15% max health == -15% eHP, linearly
            Short = "ALWAYS: -15% MAX HEALTH",
            Description = "ALWAYS ON. YOU LOSE 15% OF YOUR MAXIMUM HEALTH.",
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
    public static bool IsActive(Vow? vow, WeaveContext ctx)
    {
        if (vow is null) return false;
        if (vow.Kind == VowKind.StaticCost) return true;

        return vow.Demand switch
        {
            VowDemand.SingleStyle => ctx.DistinctStyles <= 1,
            VowDemand.SingleSource => ctx.DistinctSources <= 1,
            VowDemand.EveryWeaveFilled => ctx.SkillsWoven >= ctx.SkillSlots,
            VowDemand.NoCritInvestment => ctx.CritPercent <= ctx.BaseCritPercent + 0.01f,
            VowDemand.CadenceAtOrBelow => ctx.SkillRate <= vow.Threshold + 0.001f,
            VowDemand.CadenceAtOrAbove => ctx.SkillRate >= vow.Threshold - 0.001f,
            VowDemand.NoDefence => ctx.Defence <= 0,
            VowDemand.NoKeystone => ctx.KeystonesWorn == 0,
            VowDemand.SlotLeftBare => !ctx.WornSlots.Contains(vow.Bare),
            _ => false,
        };
    }
}
