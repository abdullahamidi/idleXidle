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
    /// <summary>Only usable while a condition holds. Rarer condition = more power.</summary>
    Conditional,

    /// <summary>Always active, always costing you something. Priced on effective-HP given up.</summary>
    StaticCost,
}

/// <summary>
/// What a conditional Vow actually watches. The machine-readable half of the restriction.
/// </summary>
/// <remarks>
/// A Vow used to carry only an <see cref="Vow.ExpectedUptime"/> and a prose description, which meant
/// the CALLER had to decide when the condition held — and no caller ever existed, so no Vow condition
/// was ever evaluated by anything. Naming the trigger in the data puts the rule next to the number it
/// prices, so a Vow cannot promise "only while bloodied" and be checked for something else.
/// </remarks>
public enum VowTrigger
{
    /// <summary>Static-cost Vows: always on, always costing.</summary>
    None,

    /// <summary>The weaver's own health is at or below <see cref="Vow.Threshold"/> (0..1).</summary>
    BelowHealthFraction,

    /// <summary>The current wave is a boss wave.</summary>
    AgainstBoss,

    /// <summary>At least <see cref="Vow.Threshold"/> ms have elapsed in the expedition.</summary>
    AfterElapsedMs,

    /// <summary>
    /// The weaver's own health is at or ABOVE <see cref="Vow.Threshold"/> (0..1) — the mirror of
    /// <see cref="BelowHealthFraction"/>. Power while you are still whole.
    /// </summary>
    /// <remarks>
    /// This replaced <c>InTheFront</c>, which watched <c>Slot == 0</c>. Once the squad was gone the
    /// champion is ALWAYS slot 0, so that trigger could never be false — a conditional Vow priced for
    /// 20% uptime that paid its full multiplier for free, every fight. A condition that can never be
    /// false is as broken as one that can never be true (the reason VOW OF THE OPENING was deleted). A
    /// solo fight has no slots to stand in; it does have a health bar that a real fight actually moves.
    /// </remarks>
    AboveHealthFraction,
}

public sealed record Vow
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required VowKind Kind { get; init; }

    /// <summary>What the condition watches. <see cref="VowTrigger.None"/> for static-cost Vows.</summary>
    public VowTrigger Trigger { get; init; } = VowTrigger.None;

    /// <summary>The trigger's threshold — a health fraction, or a duration in ms. Ignored otherwise.</summary>
    public float Threshold { get; init; }

    /// <summary>
    /// The restriction in a few words, for a UI row. <see cref="Description"/> is the prose version.
    /// </summary>
    /// <remarks>
    /// A separate field rather than a truncation: the Description is a full sentence sized for a
    /// tooltip, and at 480x270 it overran the row and collided with the numbers on both sides of it. A
    /// terse label is not the same string with fewer letters — it is a different piece of writing.
    /// </remarks>
    public required string Short { get; init; }

    /// <summary>Conditional only: 0..1, how often the condition holds. Must be strictly inside (0,1).</summary>
    public float ExpectedUptime { get; init; } = 0.5f;

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

    public IReadOnlyDictionary<Form, float> FormBaseValue { get; init; } = new Dictionary<Form, float>
    {
        [Form.Strike] = 45f,
        [Form.Projectile] = 38f,
        [Form.Aura] = 22f,
        [Form.Trap] = 55f,
        [Form.Mark] = 0f,          // Mark deals no direct damage — it amplifies other sources.
        [Form.Transformation] = 30f,
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

        return vow.Kind == VowKind.Conditional
            ? ConditionalMultiplier(vow.ExpectedUptime, tuning)
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
        new()
        {
            Id = "vow_bloodied", Name = "VOW OF THE BLOODIED", Kind = VowKind.Conditional,
            Trigger = VowTrigger.BelowHealthFraction, Threshold = 0.40f,
            ExpectedUptime = 0.25f,
            Short = "BELOW 40% HP",
            Description = "ONLY WHILE BELOW 40% HEALTH. DESPERATION IS POWER.",
        },
        new()
        {
            Id = "vow_boss_bound", Name = "VOW OF THE BOUND", Kind = VowKind.Conditional,
            Trigger = VowTrigger.AgainstBoss,
            ExpectedUptime = 0.25f,
            Short = "VS BOSSES ONLY",
            Description = "ONLY AGAINST BOSSES. SAVED FOR WHAT MATTERS.",
        },
        new()
        {
            // Re-pointed for the solo model. Its old trigger, InTheFront, was ALWAYS true once the squad
            // was gone — the champion is always slot 0 — so a Vow priced at 0.2 uptime paid its full 2.2x
            // for nothing: a strictly-dominant pick, the exact Pillar-4 sin this catalog forbids, and the
            // mirror of the deleted VOW OF THE OPENING (never-true instead of never-false). It now watches
            // your own health — the mirror of THE BLOODIED — a condition a real fight actually moves.
            Id = "vow_vanguard", Name = "VOW OF THE UNBROKEN", Kind = VowKind.Conditional,
            Trigger = VowTrigger.AboveHealthFraction, Threshold = 0.70f,
            ExpectedUptime = 0.6f,          // usually whole, but a fight that threatens you drops it
            Short = "ABOVE 70% HP",
            Description = "ONLY WHILE ABOVE 70% HEALTH. POWER WHILE YOU ARE STILL WHOLE.",
        },
        new()
        {
            Id = "vow_patience", Name = "VOW OF PATIENCE", Kind = VowKind.Conditional,
            Trigger = VowTrigger.AfterElapsedMs, Threshold = 10_000f,
            ExpectedUptime = 0.6f,
            Short = "AFTER 10S",
            Description = "ONLY AFTER 10 SECONDS OF EXPEDITION. WEAKER, BUT EASY TO MEET.",
        },
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

        return vow.Trigger switch
        {
            VowTrigger.BelowHealthFraction => ctx.HealthFraction <= vow.Threshold,
            VowTrigger.AgainstBoss => ctx.IsBoss,
            VowTrigger.AfterElapsedMs => ctx.ElapsedMs >= vow.Threshold,
            VowTrigger.AboveHealthFraction => ctx.HealthFraction >= vow.Threshold,
            _ => false,
        };
    }
}

/// <summary>
/// The battle state a Vow's condition is judged against. Pure data — no MonoGame, no sim internals.
/// </summary>
public readonly record struct WeaveContext(float HealthFraction, int ElapsedMs, bool IsBoss, int Slot);
