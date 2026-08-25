using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;
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

    // ── The refine LADDER (playtest, upgrade rework): 15 rungs, the first 5 safe, then a rising
    //    slip chance. A slip drops one level and one rung. ──
    public int MaxUpgrades { get; init; } = 15;
    public int RefineSafeUpgrades { get; init; } = 5;
    public float RefineFailStep { get; init; } = 0.06f;
    public float RefineFailCap { get; init; } = 0.60f;

    /// <summary>REFINE's base Gold cost; the full cost adds 8× the item's level.</summary>
    public int RefineGoldBase { get; init; } = 15;

    public static ForgeTuning Default { get; } = new();
}

/// <summary>The outcome of a refine: the leveled-up item and what it costs across the tiers.</summary>
public sealed record RefineResult(ItemInstance Product, int Scrap, int Gold, int Crystal)
{
    /// <summary>Chance this refine SLIPS (see <see cref="Forge.RefineFailChance"/>). Zero on the safe rungs.</summary>
    public double FailChance { get; init; }

    /// <summary>GreaterRefine only: how many rungs it actually buys (the +15 cap can shrink it).</summary>
    public int Steps { get; init; } = 1;
}

/// <summary>What a rolled refine actually did: the product, and whether it slipped a level.</summary>
public sealed record RefineOutcome(ItemInstance Product, bool Failed);

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

        // THE CLASS IS INHERITED, NEVER ROLLED, in the same spirit as the type and the element: the
        // first input of the product's own type that has a class decides, then the first input with
        // any class at all. Three legacy pieces (no class) fuse into a legacy piece — anyone's — so a
        // pre-class bag keeps its promise all the way through the forge. A universal product (charm,
        // ring, focus) never carries a class.
        var cls = ItemClasses.IsClassLocked(type)
            ? inputs.FirstOrDefault(i => i.BaseType == type && i.Class is not null)?.Class
              ?? inputs.FirstOrDefault(i => i.Class is not null)?.Class
            : null;
        // A weapon's family follows the same rule: the first weapon input whose family the class can
        // carry keeps its shape; otherwise the class picks one of its own. A legacy product keeps
        // null and derives its family from the id, exactly like a legacy drop.
        // Drawn AFTER the id and the prefix, so a seeded merge still mints the id it always did.
        var id = $"itm_{rng.Next(int.MaxValue):x8}";
        // A fused item is a NEW item, so its prefix is rolled fresh like any other mint.
        var prefix = Economy.GearTraits.RollPrefix(type, rng);
        int? family = null;
        if (type == ItemBaseType.Weapon && cls is { } c)
        {
            var kept = inputs.FirstOrDefault(i => i.BaseType == ItemBaseType.Weapon
                                                  && ItemClasses.FamilyAllowed(c, ItemNaming.WeaponFamilyIndex(i)));
            family = kept is not null ? ItemNaming.WeaponFamilyIndex(kept) : ItemClasses.RollFamily(c, rng);
        }

        return new MergeResult
        {
            Product = new ItemInstance
            {
                InstanceId = id,
                BaseType = type,
                Rarity = upgraded,
                Element = element,
                SellValue = loot.RaritySellValue[(int)upgraded],
                // Inherit the best input's item level. Without this the product defaults to iL1, silently
                // voiding every Refine spent on the fused items (affix magnitude is ilvl-scaled). SellValue
                // is rarity-only, so carrying the level up concentrates investment without paying anything.
                ItemLevel = inputs.Max(i => i.ItemLevel),
                TraitOverride = prefix,
                Class = cls,
                Family = family,
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
    /// <summary>
    /// The chance the NEXT refine of this item slips. Zero through the safe rungs, then climbing.
    /// </summary>
    /// <remarks>
    /// Playtest, upgrade rework: "+15 olarak sınırlayarak... giderek yükselecek şekilde downgrade
    /// oranı koyarak." Rung 6 risks 6%, each further rung +6%, capped at 60% for rung 15. A slip
    /// steps the item level AND the rung back by one — the ladder is climbed, not bought, and a slip
    /// also re-cheapens the next attempt, so a bad streak softens itself.
    /// </remarks>
    public static double RefineFailChance(ItemInstance item, ForgeTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(tuning);
        var attempt = item.Upgrades + 1;
        if (attempt <= tuning.RefineSafeUpgrades) return 0;
        return Math.Min(tuning.RefineFailCap, (attempt - tuning.RefineSafeUpgrades) * tuning.RefineFailStep);
    }

    /// <summary>Has this item climbed the whole ladder?</summary>
    public static bool AtRefineCap(ItemInstance item, ForgeTuning tuning)
        => item.Upgrades >= tuning.MaxUpgrades;

    /// <summary>
    /// The refine PREVIEW: the success product, the price, and the slip chance. Deterministic — the
    /// screens draw from this; the actual roll is <see cref="TryRefine"/>.
    /// </summary>
    public static RefineResult Refine(ItemInstance item, ForgeTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(tuning);
        // The cost climbs with the item level AND with the rung — the ladder's top must be earned.
        var costScale = 4 + item.Upgrades;
        return new RefineResult(
            item with { ItemLevel = item.ItemLevel + 1, Upgrades = item.Upgrades + 1 },
            Scrap: (tuning.RefineScrapBase + item.ItemLevel) * costScale / 4,
            Gold: (tuning.RefineGoldBase + item.ItemLevel * 8) * costScale / 4,
            Crystal: 0)
        { FailChance = RefineFailChance(item, tuning) };
    }

    /// <summary>
    /// Roll the refine: the success product, or the SLIP product (one level and one rung down, never
    /// below the floor). The caller has already checked the cap and paid — a slip is not a refund.
    /// </summary>
    public static RefineOutcome TryRefine(ItemInstance item, ForgeTuning tuning, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var preview = Refine(item, tuning);
        if (rng.NextDouble() >= preview.FailChance) return new RefineOutcome(preview.Product, Failed: false);
        return new RefineOutcome(
            item with
            {
                ItemLevel = Math.Max(1, item.ItemLevel - 1),
                Upgrades = Math.Max(0, item.Upgrades - 1),
            },
            Failed: true);
    }

    /// <summary>
    /// GREATER REFINE: up to +5 rungs for one CRYSTAL (plus Gold) — SAFE, never slips. The premium
    /// currency's promise is certainty; it still cannot pass the +15 cap (Steps says what it bought).
    /// </summary>
    public static RefineResult GreaterRefine(ItemInstance item, ForgeTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(tuning);
        var steps = Math.Clamp(tuning.MaxUpgrades - item.Upgrades, 0, 5);
        return new RefineResult(
            item with { ItemLevel = item.ItemLevel + steps, Upgrades = item.Upgrades + steps },
            Scrap: 0,
            Gold: (tuning.RefineGoldBase + item.ItemLevel * 8) * 3,
            Crystal: 1)
        { Steps = steps };
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
