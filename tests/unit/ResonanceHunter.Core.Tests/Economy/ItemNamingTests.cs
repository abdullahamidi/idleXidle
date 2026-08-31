using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using Xunit;

namespace ResonanceHunter.Core.Tests.Economy;

/// <summary>
/// The naming layer: PREFIX (the item's immutable prefix trait) + ELEMENT + TYPE. The prefix is data
/// rolled at mint (item-system redesign) — the name can never change under the player again.
/// </summary>
public class ItemNamingTests
{
    private static ItemInstance Weapon(string id, Rarity rarity, int ilvl = 10, Source? el = null,
                                       GearTrait? prefix = null) => new()
    {
        InstanceId = id, BaseType = ItemBaseType.Weapon, Rarity = rarity, SellValue = 10, ItemLevel = ilvl,
        Element = el, TraitOverride = prefix,
    };

    [Fact]
    public void test_prefix_is_the_items_prefix_traits_name()
    {
        var item = Weapon("leg-prefix", Rarity.Legendary, 20, prefix: GearTrait.Keen);

        Assert.Equal(GearTraits.NameOf(GearTrait.Keen), ItemNaming.Prefix(item));
    }

    [Fact]
    public void test_a_prefixless_item_has_no_name_prefix()
    {
        // Plain drops are the point — "legendary x sword bomboş prefixsiz de gelebilir".
        var plain = Weapon("plain-1", Rarity.Legendary);
        Assert.Null(ItemNaming.Prefix(plain));
        Assert.Equal("SHADOW BLADE".Split(' ').Length,
            ItemNaming.FullName(plain with { Element = Source.Shadow }).Split(' ').Length); // ELEMENT + TYPE, no prefix
    }

    [Fact]
    public void test_full_name_is_prefix_element_type()
    {
        var item = Weapon("leg-name", Rarity.Legendary, 10, Source.Shadow, prefix: GearTrait.Savage);
        var name = ItemNaming.FullName(item);

        Assert.StartsWith(ItemNaming.Prefix(item)!, name);
        Assert.Contains("SHADOW", name);
        Assert.EndsWith(ItemNaming.TypeWord(item), name);   // a weapon family: BLADE/BOW/SPEAR/SCYTHE
    }

    [Fact]
    public void test_a_gem_names_itself_by_stat_and_level()
    {
        var gem = new ItemInstance
        {
            InstanceId = "gem_1", BaseType = ItemBaseType.Gem, Rarity = Rarity.Rare, SellValue = 30, ItemLevel = 4,
        };

        Assert.Equal($"{GemCraft.NameOf(gem)} 4", ItemNaming.FullName(gem));
    }

    [Fact]
    public void test_weapon_family_name_and_prefix_are_stable_across_item_level()
    {
        // Refining changes item level; it must NOT change what a weapon looks like or is called.
        var low = Weapon("stable-1", Rarity.Legendary, 1, Source.Nature);
        var high = low with { ItemLevel = 60 };

        Assert.Equal(ItemNaming.WeaponFamilyIndex(low), ItemNaming.WeaponFamilyIndex(high));
        Assert.Equal(ItemNaming.TypeWord(low), ItemNaming.TypeWord(high));
        Assert.Equal(ItemNaming.FullName(low), ItemNaming.FullName(high));
    }

    [Fact]
    public void test_weapon_type_word_is_the_items_family()
    {
        // (WeaponArtKey is gone: the icon is an id-stable pick from the slot's art set now — see
        //  ForgeScreen.ItemArt — so the name's family and the family's STAT are what this layer owns.)
        var item = Weapon("fam-1", Rarity.Rare, 5);
        var family = ItemNaming.WeaponFamilies[ItemNaming.WeaponFamilyIndex(item)];

        Assert.Equal(family.ToUpperInvariant(), ItemNaming.TypeWord(item));
    }
}
