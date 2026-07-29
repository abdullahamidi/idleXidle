using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using Xunit;

namespace ResonanceHunter.Core.Tests.Economy;

/// <summary>
/// The prefix-naming layer that gives the item pool depth: PREFIX (dominant affix) + ELEMENT + TYPE, all
/// derived from the item's id/affixes so it's stable and needs no new art.
/// </summary>
public class ItemNamingTests
{
    private static ItemInstance Weapon(string id, Rarity rarity, int ilvl = 10, Source? el = null) => new()
    {
        InstanceId = id, BaseType = ItemBaseType.Weapon, Rarity = rarity, SellValue = 10, ItemLevel = ilvl, Element = el,
    };

    [Fact]
    public void test_prefix_comes_from_the_items_dominant_affix()
    {
        var item = Weapon("leg-prefix", Rarity.Legendary, 20);
        var dom = ItemNaming.DominantAffix(item);

        Assert.NotNull(dom);
        Assert.Contains(dom!.Value, ItemAffixes.Of(item).Select(a => a.Stat));
        Assert.False(string.IsNullOrEmpty(ItemNaming.Prefix(item)));
    }

    [Fact]
    public void test_a_common_item_has_no_prefix()
    {
        // Commons roll zero affixes, so there is no dominant stat to prefix from.
        var common = Weapon("common-1", Rarity.Common);
        Assert.Null(ItemNaming.Prefix(common));
        Assert.Equal("SHADOW BLADE".Split(' ').Length,
            ItemNaming.FullName(common with { Element = Source.Shadow }).Split(' ').Length); // ELEMENT + TYPE, no prefix
    }

    [Fact]
    public void test_full_name_is_prefix_element_type()
    {
        var item = Weapon("leg-name", Rarity.Legendary, 10, Source.Shadow);
        var name = ItemNaming.FullName(item);

        Assert.StartsWith(ItemNaming.Prefix(item)!, name);
        Assert.Contains("SHADOW", name);
        Assert.EndsWith(ItemNaming.TypeWord(item), name);   // a weapon family: BLADE/BOW/SPEAR/SCYTHE
    }

    [Fact]
    public void test_weapon_family_name_and_prefix_are_stable_across_item_level()
    {
        // Refining changes item level; it must NOT change what a weapon looks like or is called.
        var low = Weapon("stable-1", Rarity.Legendary, 1, Source.Nature);
        var high = low with { ItemLevel = 60 };

        Assert.Equal(ItemNaming.WeaponArtKey(low), ItemNaming.WeaponArtKey(high));
        Assert.Equal(ItemNaming.TypeWord(low), ItemNaming.TypeWord(high));
        Assert.Equal(ItemNaming.FullName(low), ItemNaming.FullName(high));
    }

    [Fact]
    public void test_weapon_type_word_is_a_family_and_matches_the_art_key()
    {
        var item = Weapon("fam-1", Rarity.Rare, 5);
        var family = ItemNaming.WeaponFamilies[ItemNaming.WeaponFamilyIndex(item)];

        Assert.Equal(family.ToUpperInvariant(), ItemNaming.TypeWord(item));
        Assert.StartsWith($"weapon_{family}_", ItemNaming.WeaponArtKey(item));
    }
}
