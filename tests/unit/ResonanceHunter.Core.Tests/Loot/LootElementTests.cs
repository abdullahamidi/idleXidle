using System;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Loot;
using Xunit;

namespace ResonanceHunter.Core.Tests.Loot;

/// <summary>
/// Loot actually carries the region's element.
/// </summary>
/// <remarks>
/// I very nearly shipped the same orphan I spent this whole session removing: ItemInstance.Element
/// existed, the Forge carried it through merges, MergeRecipe read it, and NOTHING EVER SET IT — so
/// every item in the game would have been null, and the entire element layer would have been
/// machinery around nothing. Found by asking "who writes this?", which is the question the rest of
/// the codebase kept failing to ask.
/// </remarks>
public class LootElementTests
{
    private static KillContext Ctx(Source? element) => new()
    {
        PowerTier = 5,
        Element = element,
    };

    [Fact]
    public void test_a_regions_loot_is_attuned_to_that_region()
    {
        var items = LootSystem.Roll(Ctx(Source.Machine), new Random(3), LootTuning.Default);
        var wearables = items.Where(i => i.BaseType is ItemBaseType.Weapon or ItemBaseType.Charm
                                                    or ItemBaseType.AbilityFocus).ToList();

        Assert.All(wearables, i => Assert.Equal(Source.Machine, i.Element));
    }

    [Fact]
    public void test_a_boss_drop_in_a_region_carries_that_regions_element()
    {
        // The wire that matters — the idle loop rolls boss loot through THIS, not LootSystem directly. A
        // single boss drops one or two items, so a run's worth is sampled to guarantee some wearables.
        var rng = new Random(9);
        var items = Enumerable.Range(0, 40)
            .SelectMany(_ => ExpeditionLoot.RollBoss(powerTier: 5, quality: 2f, rng, element: Source.Shadow))
            .ToList();

        var wearables = items.Where(i => i.BaseType is not (ItemBaseType.Material or ItemBaseType.CreatureCore)).ToList();
        Assert.NotEmpty(wearables);
        Assert.All(wearables, i => Assert.Equal(Source.Shadow, i.Element));
    }

    [Fact]
    public void test_materials_and_cores_are_never_attuned()
    {
        // An elemental lump of scrap is noise — and it would let a trio of materials carry an element
        // into a hybrid, which is the one thing mixing is supposed to cost you.
        var items = LootSystem.Roll(Ctx(Source.Machine), new Random(3), LootTuning.Default);

        foreach (var i in items.Where(i => i.BaseType is ItemBaseType.Material or ItemBaseType.CreatureCore))
            Assert.Null(i.Element);
    }

    [Fact]
    public void test_an_unattuned_region_drops_inert_loot()
    {
        var items = LootSystem.Roll(Ctx(null), new Random(3), LootTuning.Default);
        Assert.All(items, i => Assert.Null(i.Element));
    }

    [Fact]
    public void test_a_regions_whole_haul_shares_one_element_so_a_trio_can_be_matched()
    {
        // Hoarding by element is only possible because the element comes from the REGION, not a roll.
        // A per-drop random element would make a matched trio pure luck, and the Forge's carry-through
        // rule would be unusable in practice.
        var rng = new Random(11);
        var items = Enumerable.Range(0, 40)
            .SelectMany(_ => ExpeditionLoot.RollBoss(powerTier: 6, quality: 3f, rng, element: Source.Nature))
            .ToList();

        var elements = items
            .Where(i => i.BaseType is not (ItemBaseType.Material or ItemBaseType.CreatureCore))
            .Select(i => i.Element).Distinct().ToList();

        Assert.Single(elements);
    }
}
