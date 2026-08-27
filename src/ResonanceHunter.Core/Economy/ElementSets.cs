using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;

namespace ResonanceHunter.Core.Economy;

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
/// The shape of every set is the same so it can be learned once: the 2- and 4-piece rungs make the
/// element's OWN skills hit harder (+8% each, +16% at four), the 3-piece rung is a plain stat in the
/// element's character, and the 5-piece rung is the element's one special rule. A build that mixes
/// elements gets a little from each; a build that commits gets the rule. Eight slots are worn, so a
/// full five leaves three for anything.
/// </para>
/// </remarks>
public static class ElementSets
{
    /// <summary>The worn pieces each rung asks for.</summary>
    public static readonly IReadOnlyList<int> Rungs = new[] { 2, 3, 4, 5 };

    /// <summary>How much the element's own skills gain at the 2- and 4-piece rungs, each.</summary>
    public const float OwnSkillBonusPerRung = 0.08f;

    /// <summary>The set's name as a title: "SPIRIT SET".</summary>
    public static string Name(Source element) => $"{element.ToString().ToUpperInvariant()} SET";

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

    private static SkillShape Own(Source element) => new()
    {
        SourceBonus = new Dictionary<Source, float> { [element] = OwnSkillBonusPerRung },
    };

    private static string OwnLine(Source element, int rung) =>
        rung == 2
            ? $"Your {element.ToString().ToUpperInvariant()} skills hit 8% harder."
            : $"Your {element.ToString().ToUpperInvariant()} skills hit 8% harder again — 16% in all.";

    private static IReadOnlyList<SetTier> Set(Source element, string three, SkillShape threeShape, string five, SkillShape fiveShape) => new[]
    {
        new SetTier(2, OwnLine(element, 2), Own(element)),
        new SetTier(3, three, threeShape),
        new SetTier(4, OwnLine(element, 4), Own(element)),
        new SetTier(5, five, fiveShape),
    };

    private static readonly IReadOnlyDictionary<Source, IReadOnlyList<SetTier>> Catalogue = new Dictionary<Source, IReadOnlyList<SetTier>>
    {
        // BODY — flesh and muscle: the pool, and the swing MIGHT owns.
        [Source.Body] = Set(Source.Body,
            "+10% maximum health.", new SkillShape { MaxHealth = 1.10f },
            "Your basic attack hits 25% harder.", new SkillShape { AutoAttackDamage = 1.25f }),

        // MACHINE — plate and pistons: what a bite gets through.
        [Source.Machine] = Set(Source.Machine,
            "Every bite deals 4 less.", new SkillShape { FlatDamageReduction = 4f },
            "The first bite of every wave deals nothing.", new SkillShape { FirstBiteFree = true }),

        // MIND — the precise hit and the open window.
        [Source.Mind] = Set(Source.Mind,
            "+6% critical chance.", new SkillShape { BonusCritPercent = 6f },
            "A MARK's window lasts 50% longer.", new SkillShape { MarkWindowMultiplier = 1.5f }),

        // NATURE — growth: life that comes back.
        [Source.Nature] = Set(Source.Nature,
            "Regain 0.3% of maximum health every second.", new SkillShape { RegenFraction = 0.003f },
            "Heal 2% of the damage you deal.", new SkillShape { Leech = 0.02f }),

        // SHADOW — the finisher.
        [Source.Shadow] = Set(Source.Shadow,
            "+12% against creatures under 30% health.", new SkillShape { CullThreshold = 0.30f, CullBonus = 0.12f },
            "Every kill takes a beat off your cooldowns.", new SkillShape { CooldownRefundOnKillMs = SoloBattle.DefaultBeatMs }),

        // SPIRIT — tempo and the opening word.
        [Source.Spirit] = Set(Source.Spirit,
            "You act 6% faster.", new SkillShape { SkillRate = 1.06f },
            "Each skill's first cast of a wave hits 25% harder.", new SkillShape { FirstCastMultiplier = 1.25f }),
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
