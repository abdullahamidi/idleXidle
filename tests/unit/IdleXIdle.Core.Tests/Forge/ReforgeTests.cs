using System;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Forging;
using IdleXIdle.Core.Loot;
using Xunit;

// Tests.Forging, not Tests.Forge — a Tests.Forge namespace shadows the Forge/Reforge classes and every
// call resolves to the namespace instead. Same trap SalvageTests documents.
namespace IdleXIdle.Core.Tests.Forging;

/// <summary>
/// The Reforge: spending materials to re-roll an item's ENCHANTMENT — its one remaining verb.
/// </summary>
/// <remarks>
/// The trait re-roll is GONE (item-system redesign): an item's PREFIX is rolled once at mint and
/// immutable — playtest: re-rolling it read as the weapon turning into a different weapon. What the
/// Reforge still owns is the enchantment, the build-defining Form-combo, and these tests pin the
/// invariants a random re-roll must never break: it stays in the slot pool, it is always a real
/// change, it never touches the item's identity, and it never wipes the immutable prefix.
/// </remarks>
public class ReforgeTests
{
    private static readonly ReforgeTuning T = ReforgeTuning.Default;

    private static ItemInstance Item(ItemBaseType type, Rarity rarity = Rarity.Rare, string id = "itm_1",
                                     GearTrait? prefix = null)
        => new()
        {
            InstanceId = id, BaseType = type, Rarity = rarity, SellValue = 34,
            Element = Source.Shadow, TraitOverride = prefix,
        };

    [Theory]
    [InlineData(Rarity.Common)]
    [InlineData(Rarity.Uncommon)]
    public void test_reforging_an_enchant_below_rare_is_refused(Rarity rarity)
    {
        // Only Rare+ carry an enchantment at all — there is nothing there to re-roll.
        var result = Reforge.ReforgeEnchant(Item(ItemBaseType.AbilityFocus, rarity), new Random(1), T);

        Assert.False(result.Success);
        Assert.Null(result.Product);
    }

    [Fact]
    public void test_a_reforged_enchant_stays_in_the_pool_and_always_changes()
    {
        var pool = Enchantments.PoolFor(GearSlot.Focus);
        var item = Item(ItemBaseType.AbilityFocus, Rarity.Epic);
        var before = Enchantments.Of(item)!.Kind;

        for (var seed = 0; seed < 100; seed++)
        {
            var product = Reforge.ReforgeEnchant(item, new Random(seed), T).Product!;
            var after = Enchantments.Of(product)!.Kind;

            Assert.Contains(after, pool);
            Assert.NotEqual(before, after);
        }
    }

    [Fact]
    public void test_a_reforged_enchant_magnitude_still_follows_rarity()
    {
        // Only the KIND is stored; the magnitude is recomputed from rarity, so a reforged LINGER on a
        // Legendary is exactly as strong as a rolled one.
        var item = Item(ItemBaseType.AbilityFocus, Rarity.Legendary);
        var product = Reforge.ReforgeEnchant(item, new Random(3), T).Product!;
        var ench = Enchantments.Of(product)!;

        Assert.Equal(Enchantments.MagnitudeFor(ench.Kind, Rarity.Legendary), ench.Magnitude);
    }

    [Fact]
    public void test_reforging_the_enchant_preserves_the_items_identity_and_its_prefix()
    {
        // A reforge changes what an item's TRIGGER is, never which item it is — and never its PREFIX,
        // which is rolled at mint and immutable for the item's whole life.
        var item = Item(ItemBaseType.AbilityFocus, Rarity.Epic, "itm_keepme", prefix: GearTrait.Keen);
        var product = Reforge.ReforgeEnchant(item, new Random(4), T).Product!;

        Assert.Equal(item.InstanceId, product.InstanceId);
        Assert.Equal(item.BaseType, product.BaseType);
        Assert.Equal(item.Rarity, product.Rarity);
        Assert.Equal(item.Element, product.Element);
        Assert.Equal(item.SellValue, product.SellValue);
        Assert.Equal(GearTrait.Keen, product.TraitOverride);
        Assert.Equal(GearTraits.TraitOf(item), GearTraits.TraitOf(product));
        Assert.NotNull(product.EnchantOverride);
    }

    [Fact]
    public void test_enchant_reforge_costs_rise_with_rarity()
    {
        // Casually perfecting a Legendary would dissolve the power fantasy — reach must be paid for.
        foreach (var pair in new[] { (Rarity.Rare, Rarity.Epic), (Rarity.Epic, Rarity.Legendary) })
            Assert.True(T.EnchantCostFor(pair.Item2) > T.EnchantCostFor(pair.Item1),
                $"enchant reforge for {pair.Item2} must cost more than {pair.Item1}");
    }

    [Fact]
    public void test_the_result_reports_the_cost_the_caller_must_spend()
    {
        var focus = Item(ItemBaseType.AbilityFocus, Rarity.Epic);
        Assert.Equal(T.EnchantCostFor(Rarity.Epic), Reforge.ReforgeEnchant(focus, new Random(1), T).Cost);
    }
}
