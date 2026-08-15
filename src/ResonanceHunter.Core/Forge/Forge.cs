using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Forging;

public enum ForgeOperation { Merge, Dismantle, Sell, Feed }

/// <summary>Why an item cannot be used. Rejections must be explainable, never silent.</summary>
public enum IneligibleReason
{
    Eligible,
    EquippedToCreature,
    VowBound,
}

public sealed record ForgeTuning
{
    /// <summary>Three items in, one out. The 3:1 ratio is what makes merging a SINK, not a printer.</summary>
    public int MergeInputCount { get; init; } = 3;

    /// <summary>
    /// Fraction of an item's sell value returned as materials when dismantled.
    /// </summary>
    /// <remarks>
    /// Strictly below 1.0 on purpose. If dismantling returned full value, merge -> dismantle -> merge
    /// would be a value-neutral loop that could be run forever to launder rarity. It must lose value.
    /// </remarks>
    public float DismantleReturnRate { get; init; } = 0.4f;

    /// <summary>Sell and Feed convert at the SAME rate, deliberately.</summary>
    /// <remarks>
    /// The player's choice must be "which resource do I need?", never "which one pays more?". If one
    /// dominated numerically, the other would be a trap and the choice would be fake. The UI must
    /// therefore never render them on a shared numeric axis.
    /// </remarks>
    public float FeedConversionRate { get; init; } = 1.0f;

    /// <summary>REFINE's base Scrap cost; the full cost adds the item's current level, so it always climbs.</summary>
    public int RefineScrapBase { get; init; } = 4;

    /// <summary>REFINE's base Gold cost; the full cost adds 8× the item's level.</summary>
    public int RefineGoldBase { get; init; } = 15;

    public static ForgeTuning Default { get; } = new();
}

/// <summary>The outcome of a refine: the leveled-up item and what it costs across the tiers.</summary>
public sealed record RefineResult(ItemInstance Product, int Scrap, int Gold, int Crystal);

public sealed record MergeResult
{
    public ItemInstance? Product { get; init; }
    public bool Success => Product is not null;
    public string? Rejection { get; init; }
}

/// <summary>
/// The Forge: merge, dismantle, sell, feed.
/// </summary>
/// <remarks>
/// <b>Merging is a sink, not a faucet.</b> Three items become one. It concentrates value into a
/// higher rarity; it never creates value. That is what stops the item economy inflating as the loot
/// faucet runs.
/// </remarks>
public static class Forge
{
    /// <summary>
    /// The Universal Input Gate — checked ONCE, for every operation.
    /// </summary>
    /// <remarks>
    /// This lives in one place because the alternative was tried and failed. The eligibility field
    /// existed on the item schema and <b>no operation actually read it</b>, so an equipped charm stayed
    /// fully sellable, mergeable, dismantleable and feedable. Four operations, four chances to forget.
    /// One gate cannot be forgotten in three places.
    /// </remarks>
    public static IneligibleReason CheckEligible(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.EquippedToCreatureId is not null) return IneligibleReason.EquippedToCreature;
        return IneligibleReason.Eligible;
    }

    public static bool IsEligible(ItemInstance item) => CheckEligible(item) == IneligibleReason.Eligible;

    public static string Explain(IneligibleReason reason) => reason switch
    {
        IneligibleReason.EquippedToCreature => "EQUIPPED TO A CREATURE. UNEQUIP IT FIRST.",
        IneligibleReason.VowBound => "A VOW IS BOUND TO THIS ITEM. IT CANNOT BE UNMADE.",
        _ => "",
    };

    /// <summary>
    /// Merge three items of the same rarity into one of the next rarity up.
    /// </summary>
    /// <remarks>
    /// Requires all inputs at the SAME rarity — otherwise a player could feed two Commons and one
    /// Legendary into the machine and lose the Legendary, which is a trap, not a decision.
    /// Legendary cannot be merged: there is nothing above it, so it would be a pure destruction.
    /// </remarks>
    /// <param name="ignoreRarity">
    /// A MERGE CHART was spent: mix rarities, and the LOWEST of the three sets the grade.
    /// </param>
    /// <remarks>
    /// The same-rarity rule exists so a player cannot feed a Legendary in beside two Commons and lose
    /// it — a trap, not a decision. The chart does not remove that protection, it PRICES it: the output
    /// is built from the lowest input, so mixing still costs you the difference and you can see exactly
    /// what you are giving up before you spend the paper.
    /// </remarks>
    public static MergeResult Merge(IReadOnlyList<ItemInstance> inputs, Random rng, ForgeTuning tuning, LootTuning loot,
                                    bool ignoreRarity = false)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (inputs.Count != tuning.MergeInputCount)
            return new MergeResult { Rejection = $"MERGE NEEDS EXACTLY {tuning.MergeInputCount} ITEMS." };

        if (inputs.Select(i => i.InstanceId).Distinct().Count() != inputs.Count)
            return new MergeResult { Rejection = "CANNOT MERGE AN ITEM WITH ITSELF." };

        foreach (var item in inputs)
        {
            var reason = CheckEligible(item);
            if (reason != IneligibleReason.Eligible)
                return new MergeResult { Rejection = Explain(reason) };
        }

        var rarity = inputs[0].Rarity;
        if (inputs.Any(i => i.Rarity != rarity))
        {
            if (!ignoreRarity)
                return new MergeResult { Rejection = "ALL THREE ITEMS MUST BE THE SAME RARITY." };

            // The chart's whole effect, and its whole price: the weakest input decides the grade.
            rarity = inputs.Min(i => i.Rarity);
        }

        if (rarity == Rarity.Legendary)
            return new MergeResult { Rejection = "LEGENDARY IS THE HIGHEST RARITY. NOTHING TO MERGE INTO." };

        var upgraded = rarity + 1;

        // The output type and element are DECIDED, never rolled. See MergeRecipe.
        var type = MergeRecipe.TypeOf(inputs);
        var element = MergeRecipe.ElementOf(inputs);

        return new MergeResult
        {
            Product = new ItemInstance
            {
                InstanceId = $"itm_{rng.Next(int.MaxValue):x8}",
                BaseType = type,
                Rarity = upgraded,
                Element = element,
                SellValue = loot.RaritySellValue[(int)upgraded],
                // Inherit the best input's item level. Without this the product defaults to iL1, silently
                // voiding every Refine spent on the fused items (affix magnitude is ilvl-scaled). SellValue
                // is rarity-only, so carrying the level up concentrates investment without paying anything.
                ItemLevel = inputs.Max(i => i.ItemLevel),
            },
        };
    }

    /// <summary>What a given trio WOULD produce, without consuming it. Powers the merge preview.</summary>
    public static (ItemBaseType Type, Source? Element, bool IsHybrid) Preview(IReadOnlyList<ItemInstance> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        return (MergeRecipe.TypeOf(inputs), MergeRecipe.ElementOf(inputs), MergeRecipe.IsHybrid(inputs));
    }

    /// <summary>
    /// Break an item down into MATERIALS for the shared stock. Returns strictly less than it is worth.
    /// </summary>
    /// <remarks>
    /// This returned <b>Gleam</b>, despite <see cref="ForgeTuning.DismantleReturnRate"/>'s own doc
    /// saying "returned as materials" — so dismantle was a worse SELL, which is not a choice, it is a
    /// trap for anyone who read the button. There was no material inventory for it to pay into, so
    /// "dismantled into materials" from the design could not be built.
    /// </remarks>
    public static int Dismantle(ItemInstance item, ForgeTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(tuning);
        if (!IsEligible(item)) return 0;

        return (int)MathF.Floor(item.SellValue * tuning.DismantleReturnRate);
    }

    /// <summary>
    /// Feed an item straight to one creature — materials into ITS evolution, nobody else's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ForgeOperation.Feed"/> was declared, and <see cref="ForgeTuning.FeedConversionRate"/>
    /// carried a fifteen-line justification of its rate — and <b>the method never existed</b>. The enum
    /// member had zero references in the entire source tree. "Fed to creatures to influence their
    /// evolution" was a design with a tuning constant and no code.
    /// </para>
    /// <para>
    /// The full rate against DISMANTLE's 0.4 is what makes the pair a decision rather than a ranking:
    /// feeding is <b>efficient but committed</b> (all of it, into one creature, now), dismantling is
    /// <b>lossy but liquid</b> (into a stock you can spend on anyone later). Neither dominates, which is
    /// exactly what the tuning's own note demands — the choice must be "which resource do I need?",
    /// never "which one pays more?".
    /// </para>
    /// </remarks>
    public static int Feed(ItemInstance item, ForgeTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(tuning);
        if (!IsEligible(item)) return 0;

        return (int)MathF.Floor(item.SellValue * tuning.FeedConversionRate);
    }

    /// <summary>
    /// REFINE: +1 item level, which raises the item's affixes. The bulk, infinite sink.
    /// </summary>
    /// <remarks>
    /// Costs Scrap and Gold, BOTH scaling with the item's current level, so an ever-refined item costs
    /// ever more — the drain stays hungry after content is maxed, which the economy research names as the
    /// one thing an idle economy must have or its late-game currency inflates into meaninglessness. Pure:
    /// it returns the leveled item and the price; the caller checks the stock and spends, like Reforge.
    /// </remarks>
    public static RefineResult Refine(ItemInstance item, ForgeTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(tuning);
        return new RefineResult(
            item with { ItemLevel = item.ItemLevel + 1 },
            Scrap: tuning.RefineScrapBase + item.ItemLevel,
            Gold: tuning.RefineGoldBase + item.ItemLevel * 8,
            Crystal: 0);
    }

    /// <summary>GREATER REFINE: +5 item levels for one CRYSTAL (plus Gold) — the premium sink for the rarest tier.</summary>
    public static RefineResult GreaterRefine(ItemInstance item, ForgeTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(tuning);
        return new RefineResult(
            item with { ItemLevel = item.ItemLevel + 5 },
            Scrap: 0,
            Gold: (tuning.RefineGoldBase + item.ItemLevel * 8) * 3,
            Crystal: 1);
    }

    /// <summary>
    /// Is the merge economy closed? i.e. can a player launder value by merging then dismantling?
    /// </summary>
    /// <remarks>
    /// The loop to defeat: take 3 items, merge into 1 of the next tier, dismantle it, and see whether
    /// you come out ahead of simply dismantling the 3 directly. If merging ever paid MORE than the
    /// inputs were worth, the economy would be a printing press.
    /// </remarks>
    public static bool MergeEconomyIsClosed(Rarity rarity, ForgeTuning tuning, LootTuning loot)
    {
        if (rarity == Rarity.Legendary) return true;

        var inputValue = loot.RaritySellValue[(int)rarity] * tuning.MergeInputCount;
        var mergedValue = loot.RaritySellValue[(int)rarity + 1];

        var dismantleThree = (int)MathF.Floor(loot.RaritySellValue[(int)rarity] * tuning.DismantleReturnRate)
                             * tuning.MergeInputCount;
        var dismantleMerged = (int)MathF.Floor(mergedValue * tuning.DismantleReturnRate);

        // Two things must both hold:
        //  1. Merging costs value (3 items in are worth more than the 1 that comes out).
        //  2. Merge-then-dismantle is strictly worse than dismantling directly — so the loop is not
        //     merely unprofitable in theory, but actively a loss in practice.
        return mergedValue < inputValue && dismantleMerged < dismantleThree;
    }
}
