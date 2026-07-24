using System.Linq;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Forging;
using ResonanceHunter.Core.Loot;
using Xunit;

namespace ResonanceHunter.Core.Tests.Forging;

/// <summary>
/// The tiered material economy: salvage sorts by rarity, and every tier drains through its own verb.
/// </summary>
public class MaterialTests
{
    private static ItemInstance Item(Rarity r, int ilvl)
        => new() { InstanceId = "x", BaseType = ItemBaseType.Weapon, Rarity = r, SellValue = 10, ItemLevel = ilvl };

    [Fact]
    public void test_salvage_tier_follows_rarity()
    {
        Assert.Equal(Material.Scrap, MaterialTiers.ForRarity(Rarity.Common));
        Assert.Equal(Material.Scrap, MaterialTiers.ForRarity(Rarity.Uncommon));
        Assert.Equal(Material.Essence, MaterialTiers.ForRarity(Rarity.Rare));
        Assert.Equal(Material.Core, MaterialTiers.ForRarity(Rarity.Epic));
        Assert.Equal(Material.Crystal, MaterialTiers.ForRarity(Rarity.Legendary));
    }

    [Fact]
    public void test_the_scrap_tier_is_the_legacy_materials_stock()
    {
        // Backward compat: the old single Materials API maps to Scrap, so chests/feed/old saves keep working.
        var h = new Hunter();
        h.AddMaterials(50);
        Assert.Equal(50, h.Materials);
        Assert.Equal(50, h.MaterialOf(Material.Scrap));
    }

    [Fact]
    public void test_spending_a_tier_you_cannot_afford_takes_nothing()
    {
        var h = new Hunter();
        h.AddMaterial(Material.Core, 5);
        Assert.False(h.SpendMaterial(Material.Core, 6));
        Assert.Equal(5, h.MaterialOf(Material.Core));
        Assert.True(h.SpendMaterial(Material.Core, 5));
        Assert.Equal(0, h.MaterialOf(Material.Core));
    }

    [Fact]
    public void test_refine_raises_item_level_by_one_and_costs_more_the_deeper_it_is()
    {
        var low = Forge.Refine(Item(Rarity.Rare, 10), ForgeTuning.Default);
        Assert.Equal(11, low.Product.ItemLevel);

        var high = Forge.Refine(Item(Rarity.Rare, 60), ForgeTuning.Default);
        Assert.True(high.Scrap > low.Scrap && high.Gold > low.Gold, "refine cost did not climb with item level");
    }

    [Fact]
    public void test_greater_refine_adds_five_levels_for_one_crystal()
    {
        var r = Forge.GreaterRefine(Item(Rarity.Legendary, 20), ForgeTuning.Default);
        Assert.Equal(25, r.Product.ItemLevel);
        Assert.Equal(1, r.Crystal);
        Assert.Equal(0, r.Scrap);
    }

    [Fact]
    public void test_refining_raises_the_items_affixes()
    {
        // The whole point of refining: a higher level makes the affixes stronger — the infinite upgrade.
        var before = ItemAffixes.Of(Item(Rarity.Legendary, 10)).Sum(a => a.Magnitude);
        var after = ItemAffixes.Of(Forge.Refine(Item(Rarity.Legendary, 10), ForgeTuning.Default).Product).Sum(a => a.Magnitude);
        Assert.True(after > before, "refining did not raise the item's affix magnitudes");
    }
}
