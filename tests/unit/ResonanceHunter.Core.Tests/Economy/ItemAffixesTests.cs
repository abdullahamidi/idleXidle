using System;
using System.Linq;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using Xunit;

namespace ResonanceHunter.Core.Tests.Economy;

/// <summary>
/// Explicit affixes: the rolled multi-stat bonuses that make two items of the same base differ, and make a
/// deeper drop worth chasing. They must reach the fight, or they are the recurring "tested but uncalled" bug.
/// </summary>
public class ItemAffixesTests
{
    private static ItemInstance Item(string id, ItemBaseType type, Rarity rarity, int ilvl)
        => new() { InstanceId = id, BaseType = type, Rarity = rarity, SellValue = 10, ItemLevel = ilvl };

    [Fact]
    public void test_rarity_sets_the_affix_count()
    {
        Assert.Equal(0, ItemAffixes.CountFor(Rarity.Common));
        Assert.Equal(1, ItemAffixes.CountFor(Rarity.Uncommon));
        Assert.Equal(2, ItemAffixes.CountFor(Rarity.Rare));
        Assert.Equal(3, ItemAffixes.CountFor(Rarity.Epic));
        Assert.Equal(4, ItemAffixes.CountFor(Rarity.Legendary));
    }

    [Fact]
    public void test_a_common_carries_none_a_legendary_four()
    {
        Assert.Empty(ItemAffixes.Of(Item("c", ItemBaseType.Weapon, Rarity.Common, 10)));
        Assert.Equal(4, ItemAffixes.Of(Item("l", ItemBaseType.Weapon, Rarity.Legendary, 10)).Count);
    }

    [Fact]
    public void test_materials_and_cores_carry_no_affixes()
    {
        Assert.Empty(ItemAffixes.Of(Item("m", ItemBaseType.Material, Rarity.Legendary, 50)));
        Assert.Empty(ItemAffixes.Of(Item("k", ItemBaseType.CreatureCore, Rarity.Legendary, 50)));
    }

    [Fact]
    public void test_affix_magnitude_grows_with_item_level()
    {
        // Same id and rarity — only the ilvl differs — so a deeper drop rolls strictly bigger numbers.
        float Total(int ilvl) => ItemAffixes.Of(Item("x", ItemBaseType.Weapon, Rarity.Legendary, ilvl)).Sum(a => a.Magnitude);
        Assert.True(Total(60) > Total(1), "a higher item level did not raise affix magnitude");
    }

    [Fact]
    public void test_a_damage_affix_folds_into_worn_mods()
    {
        var item = Enumerable.Range(0, 300)
            .Select(i => Item($"w{i}", ItemBaseType.Weapon, Rarity.Legendary, 30))
            .First(it => ItemAffixes.Of(it).Any(a => a.Stat == AffixStat.Damage));

        var hunter = new Hunter();
        hunter.Equip(item);

        var trait = GearTraits.ModsOf(item);
        var affixDmg = ItemAffixes.Of(item).Where(a => a.Stat == AffixStat.Damage).Sum(a => a.Magnitude);
        Assert.True(MathF.Abs(hunter.WornMods.Damage - trait.Damage * (1f + affixDmg)) < 0.001f,
            "a DAMAGE affix did not fold into WornMods.Damage");
    }

    [Fact]
    public void test_a_new_slots_enchant_reaches_worn_enchantments()
    {
        // WornEnchantments reads all eight slots now: an enchanted RING (a new slot) must contribute its
        // trigger to the fight, where before only Weapon/Charm/Focus were read and it sat inert.
        var ring = Enumerable.Range(0, 300)
            .Select(i => new ItemInstance
            {
                InstanceId = $"r{i}", BaseType = ItemBaseType.Ring, Rarity = Rarity.Legendary, SellValue = 200,
            })
            .First(it => Enchantments.Of(it) is not null);

        var hunter = new Hunter();
        hunter.Equip(ring);
        Assert.Contains(hunter.WornEnchantments, e => e.Kind == Enchantments.Of(ring)!.Kind);
    }

    [Fact]
    public void test_a_defense_affix_reaches_the_hunters_defense()
    {
        // A WEAPON, so no charm-defense muddies it — only the affix contributes above the base.
        var item = Enumerable.Range(0, 300)
            .Select(i => Item($"d{i}", ItemBaseType.Weapon, Rarity.Legendary, 30))
            .First(it => ItemAffixes.Of(it).Any(a => a.Stat == AffixStat.Defense));

        var hunter = new Hunter();
        var baseDef = hunter.Defense;
        hunter.Equip(item);

        var affixDef = ItemAffixes.Of(item).Where(a => a.Stat == AffixStat.Defense).Sum(a => a.Magnitude);
        Assert.Equal(baseDef + (int)MathF.Round(affixDef), hunter.Defense);
    }
}
