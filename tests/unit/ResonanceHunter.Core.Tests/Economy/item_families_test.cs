using System;
using System.Linq;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using Xunit;

namespace ResonanceHunter.Core.Tests.Economy;

/// <summary>
/// Weapon families are stat identities — the item-system redesign's "sword hits, bow crits" layer.
/// </summary>
public class ItemFamiliesTest
{
    private static ItemInstance At(ItemBaseType type, string id, int ilvl = 1) => new()
    {
        InstanceId = id, BaseType = type, Rarity = Rarity.Common, SellValue = 10, ItemLevel = ilvl,
    };

    [Fact]
    public void test_item_families_each_weapon_family_owns_its_channel()
    {
        // Arrange: find one weapon per family (the family is id-derived and stable).
        foreach (var familyIndex in Enumerable.Range(0, ItemNaming.WeaponFamilies.Length))
        {
            var weapon = Enumerable.Range(0, 500)
                .Select(i => At(ItemBaseType.Weapon, $"w_{i}"))
                .First(w => ItemNaming.WeaponFamilyIndex(w) == familyIndex);

            // Act
            var bonus = ItemFamilies.BonusOf(weapon);

            // Assert: the bonus channel is exactly the family's declared one.
            Assert.NotNull(bonus);
            Assert.Equal(ItemFamilies.WeaponChannel[familyIndex], bonus!.Value.Stat);
            Assert.True(bonus.Value.Magnitude > 0f);
        }
    }

    [Fact]
    public void test_item_families_bonus_grows_with_item_level()
    {
        // Refining a weapon must deepen what it IS — the identity scales with item level.
        var low = ItemFamilies.BonusOf(At(ItemBaseType.Weapon, "w_scale", 1))!.Value;
        var high = ItemFamilies.BonusOf(At(ItemBaseType.Weapon, "w_scale", 40))!.Value;

        Assert.Equal(low.Stat, high.Stat);
        Assert.True(high.Magnitude > low.Magnitude);

        // And it SATURATES at the same 4x the affix curve does — an infinite sink must not buy
        // unbounded power through the one lever that forgot its cap.
        var far = ItemFamilies.BonusOf(At(ItemBaseType.Weapon, "w_scale", 60))!.Value;
        var farther = ItemFamilies.BonusOf(At(ItemBaseType.Weapon, "w_scale", 600))!.Value;
        Assert.Equal(far.Magnitude, farther.Magnitude);
    }

    [Fact]
    public void test_item_families_every_wearable_slot_has_an_identity_and_nothing_else_does()
    {
        // A prefixless Common used to feed nothing at all from its item level; the built-in identity
        // is what makes REFINE always mean something, on every slot.
        foreach (var type in Enum.GetValues<ItemBaseType>())
        {
            var bonus = ItemFamilies.BonusOf(At(type, $"t_{type}"));
            if (Gear.SlotFor(type) is not null && type != ItemBaseType.Gem)
                Assert.NotNull(bonus);
            else
                Assert.Null(bonus);
        }
    }

    [Fact]
    public void test_item_families_a_worn_weapons_identity_reaches_the_hunter()
    {
        // The end of the chain: wearing the weapon moves its channel's total by exactly the bonus.
        var weapon = At(ItemBaseType.Weapon, "w_worn", 10);
        var bonus = ItemFamilies.BonusOf(weapon)!.Value;

        var hunter = new Hunter();
        var before = hunter.AffixTotal(bonus.Stat);
        hunter.Equip(weapon);

        Assert.Equal(before + bonus.Magnitude, hunter.AffixTotal(bonus.Stat), 5);
    }
}
