using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;

namespace ResonanceHunter.Core.Abilities;

/// <summary>What an ability DOES. Form is the verb.</summary>
public enum Form { Strike, Projectile, Aura, Trap, Mark, Transformation }

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
/// importantly it PULLS AGAINST THE SKILL TREE: a Vow that pays for carrying one Form is an argument
/// against the Spread branch, and one that pays for owning no crit is an argument against Tempo.
/// </para>
/// </remarks>
public enum VowDemand
{
    /// <summary>No demand — a static-cost Vow, always on and always paying.</summary>
    None,

    /// <summary>Every woven skill is the same Form. Argues against SPREAD's breadth.</summary>
    SingleForm,

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
/// A local enum rather than Economy.GearSlot: Weaving must not depend on the economy layer, and the
/// host maps this to the real slot when it builds the context. Only the slots worth refusing are here.
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

public sealed record WeavingTuning
{
    /// <summary>Conditional Vows: the ceiling of the uptime->power curve.</summary>
    public float MaxPowerBonus { get; init; } = 1.5f;
    public float CurveExponent { get; init; } = 1.0f;

    /// <summary>Static Vows: power granted per 1.0 of effective HP sacrificed.</summary>
    public float StaticCostConversionRate { get; init; } = 3.0f;

    /// <summary>How strongly the Hunter's resonance_affinity feeds ability power.</summary>
    public float SourceScalingCoefficient { get; init; } = 0.008f;

    public float StrongMultiplier { get; init; } = 1.5f;
    public float WeakMultiplier { get; init; } = 0.667f;

    /// <summary>
    /// What one activation of each Form is worth. These are HIT SIZES, and the spread between them is
    /// load-bearing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// They used to sit between 22 and 55 — a 2.5x spread, and only 1.18x between Strike and Projectile.
    /// Enemy armour is flat per hit (<see cref="Builds.SoloBattle.MinHitFraction"/>), so it reads hit
    /// SIZE; with the old numbers it cost a small-hit build 58% and a large-hit build 52%, which is the
    /// same thing twice. The Armoured archetype and the whole Weight axis of the skill tree had nothing
    /// to be about, because there was no such thing as a large hit.
    /// </para>
    /// <para>
    /// Cooldowns are deliberately UNCHANGED. They are tuned against wave length, and this file's own
    /// history records what happens when that is forgotten: 5-10s cooldowns meant skills that never
    /// fired at all. Spreading damage alone is the change that creates the axis without touching that.
    /// </para>
    /// <para>
    /// The cost is that Projectile and Aura lose single-target throughput. That is intended and is
    /// repaid in target COUNT once waves hold more than one creature — they are the Spread Forms, and
    /// their value is meant to be how many things they touch, not how hard.
    /// </para>
    /// </remarks>
    public IReadOnlyDictionary<Form, float> FormBaseValue { get; init; } = new Dictionary<Form, float>
    {
        [Form.Trap] = 110f,        // the largest hit in the game, and the rarest
        [Form.Strike] = 70f,       // the large-hit workhorse
        [Form.Transformation] = 40f,
        [Form.Projectile] = 20f,   // small and fast — pays in targets, not in size
        [Form.Aura] = 12f,         // smallest of all; it is a field, not a blow
        [Form.Mark] = 0f,          // Mark deals no direct damage — it amplifies other sources.
    };

    public static WeavingTuning Default { get; } = new();
}

/// <summary>An ability the player has woven: Source x Form x Vow.</summary>
public sealed record WovenAbility
{
    public required string Name { get; init; }
    public required Source Source { get; init; }
    public required Form Form { get; init; }
    public Vow? Vow { get; init; }
}

public static class Weaving
{
    /// <summary>
    /// The 6x6 Source effectiveness table — a circulant cycle, so no Source dominates.
    /// </summary>
    /// <remarks>
    /// Each Source is strong against the next two in the cycle and weak against the previous two.
    /// Every Source therefore has exactly 2 strengths and 2 weaknesses. There is no "best element",
    /// and the table is antisymmetric by construction rather than by a hand-authored grid someone has
    /// to keep balanced.
    /// </remarks>
    public static float SourceEffectiveness(Source attacker, Source target, WeavingTuning tuning)
    {
        var a = (int)attacker;
        var t = (int)target;

        var delta = ((t - a) % 6 + 6) % 6;

        return delta switch
        {
            1 or 2 => tuning.StrongMultiplier,   // strong against the next two
            4 or 5 => tuning.WeakMultiplier,     // weak against the previous two
            _ => 1.0f,                           // self, and the opposite
        };
    }

    /// <summary>
    /// Formula 1 — a conditional Vow's multiplier. Rarer condition, more power.
    /// </summary>
    /// <remarks>
    /// Strictly decreasing in uptime, so no conditional Vow can dominate another: a Vow that is easier
    /// to satisfy is always worth less. Uptime must be strictly inside (0,1) — a Vow that is always on
    /// is not a restriction, and one that is never on is not an ability.
    /// </remarks>
    public static float ConditionalMultiplier(float expectedUptime, WeavingTuning tuning)
    {
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
    public static float StaticMultiplier(float staticCostMagnitude, WeavingTuning tuning)
        => 1f + tuning.StaticCostConversionRate * staticCostMagnitude;

    public static float VowMultiplier(Vow? vow, WeavingTuning tuning)
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

    /// <summary>Formula 3 — base power, scaled by the Hunter's resonance affinity.</summary>
    public static float BasePower(Form form, float resonanceAffinity, WeavingTuning tuning)
        => tuning.FormBaseValue[form] * (1f + tuning.SourceScalingCoefficient * resonanceAffinity);

    /// <summary>
    /// The full pipeline: base power -> Vow -> Source effectiveness.
    /// </summary>
    /// <param name="vowActive">
    /// For a conditional Vow, whether its condition currently holds. A conditional Vow grants NOTHING
    /// when its condition is unmet — that is the restriction it trades power for.
    /// </param>
    public static float AbilityPower(
        WovenAbility ability, Source targetSource, float resonanceAffinity, bool vowActive, WeavingTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(ability);

        var power = BasePower(ability.Form, resonanceAffinity, tuning);

        if (ability.Vow is { } vow)
        {
            var applies = vow.Kind == VowKind.StaticCost || vowActive;
            if (applies) power *= VowMultiplier(vow, tuning);
        }

        return power * SourceEffectiveness(ability.Source, targetSource, tuning);
    }

    /// <summary>
    /// The starting Vow catalog — every one of them re-pointed at the CREATURE that swears it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These were written for manual combat and for the HUNTER: "you take 12.5% more damage", "you lose
    /// 15% of your maximum health". The Hunter has not swung a weapon since the pivot, so every "you"
    /// here addressed someone who is not in the fight. In an auto-battler the weaver is the CREATURE,
    /// and the costs land on the creature that took the Vow — which is what makes a Vow a squad decision
    /// rather than an account-wide toggle.
    /// </para>
    /// <para>
    /// <b>VOW OF THE OPENING is gone.</b> It read "only during a weak-point window" — weak points were
    /// manual combat's whole skill expression, and they were deleted with it. A Vow whose condition can
    /// never be true is not a restriction, it is a dead ability, and it would have priced itself at
    /// +90% power for a bonus that never once paid out. VOW OF THE VANGUARD replaces it and watches
    /// something this game actually has: the front slot. It is the better trade anyway — it pays the
    /// creature that is standing where the blows land, so it argues with slot order instead of ignoring
    /// it.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Vow> Catalog { get; } = new List<Vow>
    {
        // ── BUILD SHAPE. Each one argues against a branch of the skill tree, which is the point: a Vow
        //    that costs you nothing you wanted is not a Vow. ──────────────────────────────────────
        new()
        {
            Id = "vow_singular", Name = "VOW OF THE SINGULAR", Kind = VowKind.Demand,
            Demand = VowDemand.SingleForm, Severity = 0.75f,
            Short = "ONE FORM ONLY",
            Description = "EVERY SKILL YOU CARRY MUST BE THE SAME FORM. DEPTH INSTEAD OF BREADTH.",
        },
        new()
        {
            Id = "vow_pure", Name = "VOW OF THE PURE", Kind = VowKind.Demand,
            Demand = VowDemand.SingleSource, Severity = 0.6f,
            Short = "ONE SOURCE ONLY",
            Description = "EVERY SKILL MUST DRAW ONE SOURCE. THE MATCHUP WHEEL STOPS BEING YOURS.",
        },
        new()
        {
            // The one Vow most builds already satisfy, and priced accordingly — it exists so the
            // catalogue has an entry a new player can take without giving anything up yet.
            Id = "vow_complete", Name = "VOW OF COMPLETION", Kind = VowKind.Demand,
            Demand = VowDemand.EveryWeaveFilled, Severity = 0.2f,
            Short = "NO EMPTY WEAVE",
            Description = "EVERY SKILL SLOT YOU OWN MUST BE FILLED. NOTHING HELD BACK.",
        },

        // ── STAT SHAPE. These read the character sheet, so they pull against the STATS screen's
        //    training and against the skill tree's numbered nodes at the same time. ───────────────
        new()
        {
            Id = "vow_bluntedge", Name = "VOW OF THE BLUNT EDGE", Kind = VowKind.Demand,
            Demand = VowDemand.NoCritInvestment, Severity = 0.55f,
            Short = "NO CRIT TRAINING",
            Description = "YOUR CRITICAL CHANCE MUST BE UNTOUCHED. NO LUCKY BLOWS, ONLY CERTAIN ONES.",
        },
        new()
        {
            Id = "vow_deliberate", Name = "VOW OF THE DELIBERATE", Kind = VowKind.Demand,
            Demand = VowDemand.CadenceAtOrBelow, Threshold = 1.0f, Severity = 0.5f,
            Short = "CADENCE AT OR BELOW 1.00x",
            Description = "YOUR SKILLS MUST NOT RETURN FASTER THAN BASE. SLOW HANDS, HEAVY BLOWS.",
        },
        new()
        {
            Id = "vow_frantic", Name = "VOW OF THE FRANTIC", Kind = VowKind.Demand,
            Demand = VowDemand.CadenceAtOrAbove, Threshold = 1.4f, Severity = 0.5f,
            Short = "CADENCE AT OR ABOVE 1.40x",
            Description = "YOUR SKILLS MUST RETURN AT LEAST 40% FASTER THAN BASE. NEVER STILL.",
        },
        new()
        {
            Id = "vow_unguarded", Name = "VOW OF THE UNGUARDED", Kind = VowKind.Demand,
            Demand = VowDemand.NoDefence, Severity = 0.7f,
            Short = "NO DEFENCE AT ALL",
            Description = "NO DEFENCE FROM TRAINING, CHARM OR AFFIX. NOTHING BETWEEN YOU AND THE WAVE.",
        },
        new()
        {
            Id = "vow_unbound", Name = "VOW OF THE UNBOUND", Kind = VowKind.Demand,
            Demand = VowDemand.NoKeystone, Severity = 0.8f,
            Short = "NO KEYSTONE SOCKETED",
            Description = "YOU MAY SOCKET NO KEYSTONE. THE PERMANENT TREE'S PAYOFF, REFUSED.",
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
            Description = "ALWAYS ON. IT TAKES 12.5% MORE DAMAGE.",
        },
        new()
        {
            Id = "vow_reckless_offering", Name = "RECKLESS OFFERING", Kind = VowKind.StaticCost,
            StaticCostMagnitude = 0.15f,    // -15% max health == -15% eHP, linearly
            Short = "ALWAYS: -15% MAX HP",
            Description = "ALWAYS ON. IT PERMANENTLY LOSES 15% OF ITS MAXIMUM HEALTH.",
        },
    };

    public static Vow? ById(string? id) => id is null ? null : Catalog.FirstOrDefault(v => v.Id == id);

    /// <summary>
    /// Does a conditional Vow's condition hold right now?
    /// </summary>
    /// <remarks>
    /// Static-cost Vows are always active — they are paying whether or not they are useful, which is the
    /// entire distinction from a conditional one.
    /// </remarks>
    public static bool IsActive(Vow? vow, WeaveContext ctx)
    {
        if (vow is null) return false;
        if (vow.Kind == VowKind.StaticCost) return true;

        return vow.Demand switch
        {
            VowDemand.SingleForm => ctx.DistinctForms <= 1,
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
    int DistinctForms,
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
