using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;

namespace IdleXIdle.Core.Economy;

/// <summary>One rung of an element set: worn pieces needed, the effect in one plain sentence, the effect itself.</summary>
public sealed record SetTier(int Pieces, string Line, SkillShape Shape);

/// <summary>
/// ELEMENT SETS — every worn item carries an element (its <see cref="ItemInstance.Element"/>, the
/// Source of the region it dropped in), and wearing several of one element unlocks that element's set
/// bonuses at 2, 3, 4 and 5 pieces. The bonuses reach the fight through <see cref="GearShape.Of"/>.
/// </summary>
/// <remarks>
/// <para>
/// Playtest 2026-08-27: "I really liked the set-bonus idea. As element pieces are worn, give set
/// bonuses that suit the element — 2-3-4-5 piece bonuses." Until then an item's element decided
/// nothing in a fight (only which trio the Forge would merge), and a property the player can see but
/// cannot feel reads as a hidden rule.
/// </para>
/// <para>
/// EVERY RUNG IS ITS OWN RULE, and none of them is "your matching-Source skills hit harder". That
/// model — +8% at two pieces and +16% at four — is gone, and is not replaced by a bigger percentage.
/// A set's Source is the philosophy of the EQUIPMENT, not a requirement on the skills: a BODY build
/// may wear SHADOW plate and receive everything SHADOW offers. Source still matters enormously
/// through the systems that are actually about Source — the variations, the matchup, the Vows — and
/// a set that paid you for agreeing with them was paying you twice for one decision.
/// </para>
/// <para>
/// The ladder climbs 2 · 3 · 4 · 5 across eight worn slots, so a hunter may run 5+3, 4+4 or 3+3+2.
/// The 5-piece rung is the most IDENTITY-DEFINING, which is not the same as the strongest: MOMENTUM
/// changes what a basic attack is for and CERTAINTY changes what a critical is, and either may be
/// worth less damage than two lower ladders paid in a straight line.
/// </para>
/// <para>
/// The six read from shared vocabulary — overkill, shield, critical, healing, a kill, an Active, a
/// first activation, skill rate — rather than being six engines. The only state any of them adds to
/// the wave is the state named in the design: the shield, MIND's focus, SHADOW's shades, BODY's
/// pending impact, and SPIRIT's activation mask and charges.
/// </para>
/// </remarks>
public static class ElementSets
{
    /// <summary>The worn pieces each rung asks for.</summary>
    public static readonly IReadOnlyList<int> Rungs = new[] { 2, 3, 4, 5 };

    /// <summary>The set's name as a title: "SPIRIT SET".</summary>
    public static string Name(Source element) => $"{element.ToString().ToUpperInvariant()} SET";

    /// <summary>The capstone's own name — the word the 5-piece rung is known by.</summary>
    public static string CapstoneName(Source element) => element switch
    {
        Source.Body => "MOMENTUM",
        Source.Machine => "PLATING",
        Source.Mind => "CERTAINTY",
        Source.Nature => "OVERGROWTH",
        Source.Shadow => "AFTERIMAGE",
        _ => "HARMONY",
    };

    /// <summary>
    /// How far along the set a worn count is, for a title: "3 OF 5 WORN" up to the last rung, and
    /// "8 WORN · COMPLETE" past it — eight slots can all carry one element, and "8 OF 5" is not a fact.
    /// </summary>
    public static string Progress(int worn)
    {
        var last = Rungs[^1];
        return worn <= last ? $"{worn} OF {last} WORN" : $"{worn} WORN · COMPLETE";
    }

    /// <summary>The four rungs of one element's set, in order.</summary>
    public static IReadOnlyList<SetTier> TiersOf(Source element) => Catalogue[element];

    private static IReadOnlyList<SetTier> Set(
        (string Line, SkillShape Shape) two, (string Line, SkillShape Shape) three,
        (string Line, SkillShape Shape) four, (string Line, SkillShape Shape) five) => new[]
    {
        new SetTier(2, two.Line, two.Shape),
        new SetTier(3, three.Line, three.Shape),
        new SetTier(4, four.Line, four.Shape),
        new SetTier(5, five.Line, five.Shape),
    };

    private static readonly IReadOnlyDictionary<Source, IReadOnlyList<SetTier>> Catalogue = new Dictionary<Source, IReadOnlyList<SetTier>>
    {
        // BODY — MOMENTUM. Weight, health, the swing, and force that does not stop at one body.
        [Source.Body] = Set(
            ("+10% maximum health.", new SkillShape { MaxHealth = 1.10f }),
            ("Your basic attack hits 20% harder.", new SkillShape { AutoAttackDamage = 1.20f }),
            // Direct hits only. The carry inside LandOn is already gated on fromSkill and lands its
            // carried hit with fromSkill:false, so a bleed cannot feed it and a carry cannot carry again.
            ("35% of a direct hit's overkill carries into the next enemy.",
             new SkillShape { OverkillCarry = 0.35f }),
            ("After a skill your next basic attack hits 75% harder and carries all its waste.",
             new SkillShape { ImpactSwingBonus = 0.75f })),

        // MACHINE — PLATING. Prevent first, absorb second, take the rest. It brings SHIELD to a build
        // that has no shield skill at all, which is the point of the ladder.
        [Source.Machine] = Set(
            ("Every bite deals 4 less.", new SkillShape { FlatDamageReduction = 4f }),
            ("Each wave begins with a shield worth 12% of your maximum health.",
             new SkillShape { WaveStartShieldFraction = 0.12f }),
            // Read BEFORE absorption, so it is not counted twice against the same bite.
            ("While you hold a shield, bites deal 10% less.",
             new SkillShape { ShieldedDamageTaken = 0.90f }),
            ("Once a wave, the first bite that would hurt you is stopped and becomes shield.",
             new SkillShape { PreventFirstDamagingBite = true })),

        // MIND — CERTAINTY. Precision, and less and less left to chance.
        [Source.Mind] = Set(
            ("+5% critical chance.", new SkillShape { BonusCritPercent = 5f }),
            ("+20% critical damage.", new SkillShape { BonusCritDamagePercent = 20f }),
            ("Every hit without a critical brings the next closer: up to +15%.",
             new SkillShape { FocusPerHitPercent = 3f, FocusCapPercent = 15f }),
            ("At that peak your next hit is a critical for certain, then it resets.",
             new SkillShape { CertaintyAtFocusCap = true })),

        // NATURE — OVERGROWTH. Recover, grow, waste nothing — including the healing itself.
        [Source.Nature] = Set(
            ("Regain 0.3% of maximum health every second.", new SkillShape { RegenFraction = 0.003f }),
            ("Healing in combat is 20% stronger.", new SkillShape { HealingMultiplier = 1.20f }),
            ("You can be healed 50% more each wave.", new SkillShape { HealCeilingBonus = 0.50f }),
            ("Healing you cannot use at full health becomes shield, half of it.",
             new SkillShape { OverhealToShield = 0.50f })),

        // SHADOW — AFTERIMAGE. One death prepares the next.
        [Source.Shadow] = Set(
            ("+12% against creatures under 30% health.",
             new SkillShape { CullThreshold = 0.30f, CullBonus = 0.12f }),
            ("A kill leaves a SHADE. Your next damaging skill spends it: +25%.",
             new SkillShape { ShadeActiveBonus = 0.25f, ShadeMax = 1 }),
            ("You can hold two shades at once.", new SkillShape { ShadeMax = 2 }),
            ("A skill that spends a shade strikes again for half as much.",
             new SkillShape { AfterimageFraction = 0.50f })),

        // SPIRIT — HARMONY. Everything you know, working together.
        [Source.Spirit] = Set(
            ("You act 6% faster.", new SkillShape { SkillRate = 1.06f }),
            ("Each skill's first use of a wave is 20% stronger.",
             new SkillShape { FirstActivationMagnitude = 1.20f }),
            ("Each new skill used in a wave speeds you up 2% more, up to 8%.",
             new SkillShape { ResonanceRatePerSkill = 0.02f, ResonanceRateCap = 0.08f }),
            ("With all four slots filled: once every skill has been used, each opens again.",
             new SkillShape { HarmonyCharges = true })),
    };

    /// <summary>Worn pieces per element, for every element with at least one worn.</summary>
    public static IReadOnlyDictionary<Source, int> WornCounts(Hunter hunter)
    {
        ArgumentNullException.ThrowIfNull(hunter);
        var counts = new Dictionary<Source, int>();
        foreach (var slot in Enum.GetValues<GearSlot>())
            if (hunter.Worn(slot)?.Element is { } el)
                counts[el] = counts.GetValueOrDefault(el) + 1;
        return counts;
    }

    /// <summary>Worn pieces of one element.</summary>
    public static int WornCount(Hunter hunter, Source element) => WornCounts(hunter).GetValueOrDefault(element);

    /// <summary>Every rung the hunter has reached, across all elements, in element then rung order.</summary>
    public static IEnumerable<(Source Element, SetTier Tier)> Active(Hunter hunter)
    {
        foreach (var (element, count) in WornCounts(hunter).OrderBy(kv => kv.Key))
            foreach (var tier in TiersOf(element))
                if (count >= tier.Pieces)
                    yield return (element, tier);
    }

    /// <summary>The shape every active rung adds up to — what <see cref="GearShape.Of"/> hands the fight.</summary>
    public static SkillShape ShapeFor(Hunter hunter)
    {
        var shape = SkillShape.None;
        foreach (var (_, tier) in Active(hunter)) shape = SkillShape.Combine(shape, tier.Shape);
        return shape;
    }
}
