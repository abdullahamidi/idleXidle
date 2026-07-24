using System;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Forging;
using ResonanceHunter.Core.Loot;
using Xunit;

// Tests.Forging, not Tests.Forge — a Tests.Forge namespace shadows the Forge/Reforge classes and every
// call resolves to the namespace instead. Same trap SalvageTests documents.
namespace ResonanceHunter.Core.Tests.Forging;

/// <summary>
/// The Reforge: spending materials to re-roll an item's frozen passives.
/// </summary>
/// <remarks>
/// Items were a pure function of their id — trait and enchant fixed at mint, unchangeable forever. The
/// playtest ask was the opposite: "item properties are shallow, I must be able to change/customise them."
/// These tests pin the verb that changes them, and the invariants a random re-roll must never break:
/// it stays in the slot pool, it is always a real change, and it never touches the item's identity.
/// </remarks>
public class ReforgeTests
{
    private static readonly ReforgeTuning T = ReforgeTuning.Default;

    private static ItemInstance Item(ItemBaseType type, Rarity rarity = Rarity.Rare, string id = "itm_1")
        => new() { InstanceId = id, BaseType = type, Rarity = rarity, SellValue = 34, Element = Source.Shadow };

    // ── Trait reforge ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_reforged_trait_stays_in_the_slots_pool()
    {
        // A charm must never reforge into HEAVY — the roll is drawn from the slot's own pool.
        foreach (var (type, slot) in new[]
                 {
                     (ItemBaseType.Weapon, GearSlot.Weapon),
                     (ItemBaseType.Charm, GearSlot.Charm),
                     (ItemBaseType.AbilityFocus, GearSlot.Focus),
                 })
        {
            var pool = GearTraits.PoolFor(slot);
            for (var seed = 0; seed < 100; seed++)
            {
                var result = Reforge.ReforgeTrait(Item(type), new Random(seed), T);
                Assert.True(result.Success);
                Assert.Contains(GearTraits.TraitOf(result.Product)!.Value, pool);
            }
        }
    }

    [Fact]
    public void test_a_trait_reforge_is_always_a_real_change()
    {
        // Paying materials to land the trait you already had reads as a bug, not a gamble. Every reforge
        // must move the trait — the roll excludes the current value.
        var item = Item(ItemBaseType.Weapon);
        var before = GearTraits.TraitOf(item);

        for (var seed = 0; seed < 100; seed++)
        {
            var after = GearTraits.TraitOf(Reforge.ReforgeTrait(item, new Random(seed), T).Product);
            Assert.NotEqual(before, after);
        }
    }

    [Fact]
    public void test_the_reforged_trait_overrides_the_id_derived_one()
    {
        var item = Item(ItemBaseType.Weapon);
        var product = Reforge.ReforgeTrait(item, new Random(7), T).Product!;

        Assert.NotNull(product.TraitOverride);
        Assert.Equal(product.TraitOverride, GearTraits.TraitOf(product));  // the lookup honours the override
        Assert.NotEqual(GearTraits.TraitOf(item), GearTraits.TraitOf(product));
    }

    [Fact]
    public void test_reforging_preserves_the_items_identity()
    {
        // A reforge changes what an item IS, not which item it is — same id, so worn slots and the merge
        // tray keep pointing at it instead of losing it.
        var item = Item(ItemBaseType.Weapon, Rarity.Epic, "itm_keepme");
        var product = Reforge.ReforgeTrait(item, new Random(1), T).Product!;

        Assert.Equal(item.InstanceId, product.InstanceId);
        Assert.Equal(item.BaseType, product.BaseType);
        Assert.Equal(item.Rarity, product.Rarity);
        Assert.Equal(item.Element, product.Element);
        Assert.Equal(item.SellValue, product.SellValue);
    }

    [Fact]
    public void test_reforging_a_trait_on_an_unwearable_item_is_refused()
    {
        var result = Reforge.ReforgeTrait(Item(ItemBaseType.Material), new Random(1), T);

        Assert.False(result.Success);
        Assert.Null(result.Product);
        Assert.False(string.IsNullOrWhiteSpace(result.Rejection));
    }

    // ── Enchant reforge ─────────────────────────────────────────────────────────────────────────

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
    public void test_reforging_the_enchant_keeps_a_previously_reforged_trait()
    {
        // Two reforges must compose: setting the enchant override must not wipe the trait override, or
        // crafting an item toward a build would be one-step-forward-one-step-back.
        var item = Item(ItemBaseType.AbilityFocus, Rarity.Epic);
        var withTrait = Reforge.ReforgeTrait(item, new Random(2), T).Product!;
        var withBoth = Reforge.ReforgeEnchant(withTrait, new Random(4), T).Product!;

        Assert.Equal(withTrait.TraitOverride, withBoth.TraitOverride);
        Assert.NotNull(withBoth.EnchantOverride);
        Assert.Equal(GearTraits.TraitOf(withTrait), GearTraits.TraitOf(withBoth));
    }

    // ── The cost curve ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_reforge_costs_rise_with_rarity()
    {
        // Casually perfecting a Legendary would dissolve the power fantasy — reach must be paid for, so
        // a higher rarity costs strictly more to re-roll.
        foreach (var pair in new[]
                 {
                     (Rarity.Common, Rarity.Uncommon), (Rarity.Uncommon, Rarity.Rare),
                     (Rarity.Rare, Rarity.Epic), (Rarity.Epic, Rarity.Legendary),
                 })
        {
            Assert.True(T.TraitCostFor(pair.Item2) > T.TraitCostFor(pair.Item1),
                $"trait reforge for {pair.Item2} must cost more than {pair.Item1}");
        }

        foreach (var pair in new[] { (Rarity.Rare, Rarity.Epic), (Rarity.Epic, Rarity.Legendary) })
            Assert.True(T.EnchantCostFor(pair.Item2) > T.EnchantCostFor(pair.Item1),
                $"enchant reforge for {pair.Item2} must cost more than {pair.Item1}");
    }

    [Theory]
    [InlineData(Rarity.Rare)]
    [InlineData(Rarity.Epic)]
    [InlineData(Rarity.Legendary)]
    public void test_the_enchant_costs_more_to_reforge_than_the_trait(Rarity rarity)
    {
        // The enchant is the Form-combo — the build-defining roll — so churning traits is cheap and
        // committing to an enchant re-roll is dear.
        Assert.True(T.EnchantCostFor(rarity) > T.TraitCostFor(rarity));
    }

    [Fact]
    public void test_the_result_reports_the_cost_the_caller_must_spend()
    {
        var item = Item(ItemBaseType.Weapon, Rarity.Epic);
        Assert.Equal(T.TraitCostFor(Rarity.Epic), Reforge.ReforgeTrait(item, new Random(1), T).Cost);

        var focus = Item(ItemBaseType.AbilityFocus, Rarity.Epic);
        Assert.Equal(T.EnchantCostFor(Rarity.Epic), Reforge.ReforgeEnchant(focus, new Random(1), T).Cost);
    }
}
