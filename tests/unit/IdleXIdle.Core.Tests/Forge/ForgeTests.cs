using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Forging;
using IdleXIdle.Core.Loot;
using Xunit;

namespace IdleXIdle.Core.Tests.Forging;

public class ForgeTests
{
    private static readonly ForgeTuning Tuning = ForgeTuning.Default;
    private static readonly LootTuning Loot = LootTuning.Default;
    private static Random Seeded() => new(31337);

    private static ItemInstance Item(Rarity rarity, string id, string? equippedTo = null) => new()
    {
        InstanceId = id,
        BaseType = ItemBaseType.Weapon,
        Rarity = rarity,
        SellValue = Loot.RaritySellValue[(int)rarity],
        EquippedToCreatureId = equippedTo,
    };

    private static List<ItemInstance> Three(Rarity r) =>
        [Item(r, "a"), Item(r, "b"), Item(r, "c")];

    [Fact]
    public void test_three_items_merge_into_one_of_the_next_rarity()
    {
        var result = Forge.Merge(Three(Rarity.Common), Seeded(), Tuning, Loot);

        Assert.True(result.Success);
        Assert.Equal(Rarity.Uncommon, result.Product!.Rarity);
    }

    /// <summary>
    /// Merge carries the BEST input's item level forward. Without this the product defaulted to iL1,
    /// silently voiding every Refine spent on the fused items (affix magnitude is item-level-scaled).
    /// </summary>
    [Fact]
    public void test_merge_inherits_the_highest_input_item_level()
    {
        List<ItemInstance> inputs =
        [
            Item(Rarity.Rare, "a") with { ItemLevel = 5 },
            Item(Rarity.Rare, "b") with { ItemLevel = 30 },
            Item(Rarity.Rare, "c") with { ItemLevel = 12 },
        ];

        var result = Forge.Merge(inputs, Seeded(), Tuning, Loot);

        Assert.True(result.Success);
        Assert.Equal(30, result.Product!.ItemLevel);
    }

    /// <summary>
    /// THE economic invariant: merging is a SINK. Three items in, one out — always worth less.
    /// </summary>
    /// <remarks>
    /// If merging produced more value than it consumed, the loot faucet plus the Forge would be a
    /// printing press and every other economic control in the game would be irrelevant.
    /// </remarks>
    [Theory]
    [InlineData(Rarity.Common)]
    [InlineData(Rarity.Uncommon)]
    [InlineData(Rarity.Rare)]
    [InlineData(Rarity.Epic)]
    public void test_merging_always_destroys_value_rather_than_creating_it(Rarity rarity)
    {
        var inputValue = Loot.RaritySellValue[(int)rarity] * Tuning.MergeInputCount;
        var outputValue = Loot.RaritySellValue[(int)rarity + 1];

        Assert.True(outputValue < inputValue,
            $"Merging 3x {rarity} ({inputValue}g) yields {outputValue}g — the Forge PRINTS money.");
    }

    /// <summary>
    /// The merge-then-dismantle laundering loop must be strictly unprofitable, not merely break-even.
    /// </summary>
    [Theory]
    [InlineData(Rarity.Common)]
    [InlineData(Rarity.Uncommon)]
    [InlineData(Rarity.Rare)]
    [InlineData(Rarity.Epic)]
    [InlineData(Rarity.Legendary)]
    public void test_the_merge_economy_is_closed(Rarity rarity)
        => Assert.True(Forge.MergeEconomyIsClosed(rarity, Tuning, Loot),
            $"merge->dismantle at {rarity} is a value-laundering loop.");

    /// <summary>Dismantling must always lose value, or it becomes an infinite reroll.</summary>
    [Theory]
    [InlineData(Rarity.Common)]
    [InlineData(Rarity.Legendary)]
    public void test_dismantling_returns_less_than_the_item_is_worth(Rarity rarity)
    {
        var item = Item(rarity, "x");
        Assert.True(Forge.Dismantle(item, Tuning) < item.SellValue);
    }

    // ── The Universal Input Gate ──────────────────────────────────────────────────────────────

    /// <summary>
    /// An equipped charm is ineligible for EVERY operation. This hole shipped once already.
    /// </summary>
    /// <remarks>
    /// The eligibility field existed on the item schema and no operation read it, so an equipped charm
    /// stayed fully sellable, mergeable, dismantleable and feedable. Four operations is four chances to
    /// forget. Hence one gate.
    /// </remarks>
    [Fact]
    public void test_an_equipped_item_is_rejected_by_every_forge_operation()
    {
        var equipped = Item(Rarity.Epic, "e", equippedTo: "creature_1");

        Assert.False(Forge.IsEligible(equipped));
        Assert.Equal(IneligibleReason.EquippedToCreature, Forge.CheckEligible(equipped));

        // Merge rejects it...
        var merge = Forge.Merge([equipped, Item(Rarity.Epic, "b"), Item(Rarity.Epic, "c")], Seeded(), Tuning, Loot);
        Assert.False(merge.Success);
        Assert.NotNull(merge.Rejection);

        // ...and dismantle refuses to pay out for it.
        Assert.Equal(0, Forge.Dismantle(equipped, Tuning));
    }

    /// <summary>A rejection must SAY why. A silently ignored click reads as a broken game.</summary>
    [Fact]
    public void test_every_rejection_explains_itself()
    {
        var merge = Forge.Merge([Item(Rarity.Common, "a")], Seeded(), Tuning, Loot);
        Assert.False(merge.Success);
        Assert.False(string.IsNullOrWhiteSpace(merge.Rejection));

        Assert.False(string.IsNullOrWhiteSpace(Forge.Explain(IneligibleReason.EquippedToCreature)));
        Assert.False(string.IsNullOrWhiteSpace(Forge.Explain(IneligibleReason.VowBound)));
    }

    // ── Trap prevention ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mixed rarities are refused rather than silently consuming the best item.
    /// </summary>
    /// <remarks>
    /// Allowing 2 Commons + 1 Legendary would let a player destroy a Legendary by misclicking. That is
    /// a trap, not a decision.
    /// </remarks>
    [Fact]
    public void test_mixed_rarities_are_refused_rather_than_eating_the_best_item()
    {
        var mixed = new List<ItemInstance>
        {
            Item(Rarity.Common, "a"),
            Item(Rarity.Common, "b"),
            Item(Rarity.Legendary, "c"),
        };

        var result = Forge.Merge(mixed, Seeded(), Tuning, Loot);

        Assert.False(result.Success);
        Assert.Contains("SAME RARITY", result.Rejection!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Legendary cannot merge — there is nothing above it, so it would be pure destruction.</summary>
    [Fact]
    public void test_legendary_cannot_be_merged_into_nothing()
    {
        var result = Forge.Merge(Three(Rarity.Legendary), Seeded(), Tuning, Loot);

        Assert.False(result.Success);
        Assert.Contains("HIGHEST RARITY", result.Rejection!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The same item cannot be counted three times.</summary>
    [Fact]
    public void test_an_item_cannot_be_merged_with_itself()
    {
        var one = Item(Rarity.Rare, "same");
        var result = Forge.Merge([one, one, one], Seeded(), Tuning, Loot);

        Assert.False(result.Success);
        Assert.Contains("ITSELF", result.Rejection!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void test_merge_requires_exactly_three_items()
    {
        Assert.False(Forge.Merge([Item(Rarity.Common, "a"), Item(Rarity.Common, "b")], Seeded(), Tuning, Loot).Success);

        var four = Three(Rarity.Common).Append(Item(Rarity.Common, "d")).ToList();
        Assert.False(Forge.Merge(four, Seeded(), Tuning, Loot).Success);
    }

    /// <summary>The product is one of the types you put in — inputs steer the result.</summary>
    [Fact]
    public void test_the_merge_product_is_one_of_the_input_types()
    {
        var inputs = new List<ItemInstance>
        {
            Item(Rarity.Rare, "a") with { BaseType = ItemBaseType.Charm },
            Item(Rarity.Rare, "b") with { BaseType = ItemBaseType.Charm },
            Item(Rarity.Rare, "c") with { BaseType = ItemBaseType.Charm },
        };

        var result = Forge.Merge(inputs, Seeded(), Tuning, Loot);

        Assert.True(result.Success);
        Assert.Equal(ItemBaseType.Charm, result.Product!.BaseType);
    }

    /// <summary>
    /// Sell and Feed pay the SAME. The choice must be "which resource do I need", never "which pays".
    /// </summary>
    [Fact]
    public void test_sell_and_feed_convert_at_the_same_rate()
        => Assert.Equal(1.0f, Tuning.FeedConversionRate);
}
