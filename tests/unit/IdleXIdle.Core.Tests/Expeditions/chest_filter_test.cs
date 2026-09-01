using System;
using System.Linq;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;
using Xunit;

namespace IdleXIdle.Core.Tests.Expeditions;

/// <summary>
/// The VAULT's keep-filter: a tier floor and a slot lean, applied where chests DROP.
/// </summary>
/// <remarks>
/// Playtest: "Gereksiz chestleri almak istemezsem diye loot filtresi koyabilmem lazım... örneğin
/// sadece tier 10 üstü, veya sadece ring atan kutuları al." A filtered chest never lands; it pays a
/// little Scrap instead, so the filter is a convenience and never a farm.
/// </remarks>
public class ChestFilterTest
{
    private static Chest At(int tier, string? region = null)
        => new() { Rarity = Rarity.Rare, Tier = tier, Region = region };

    [Fact]
    public void test_chest_filter_tier_floor_discards_below_and_keeps_at_and_above()
    {
        // Arrange
        var low = At(9);
        var at = At(10);
        var above = At(11);

        // Act + Assert
        Assert.False(Chests.PassesKeepFilter(low, minTier: 10, slot: null));
        Assert.True(Chests.PassesKeepFilter(at, minTier: 10, slot: null));
        Assert.True(Chests.PassesKeepFilter(above, minTier: 10, slot: null));
        Assert.True(Chests.PassesKeepFilter(low, minTier: 0, slot: null));   // 0 = keep everything
    }

    [Fact]
    public void test_chest_filter_slot_lean_keeps_favoured_and_no_lean_chests_only()
    {
        // Arrange: a chest with a real regional lean, and one with none (no region profile).
        var leaning = At(12, VerdantHollow.RegionId);
        var favoured = ChestDossiers.For(leaning).Favoured;
        Assert.True(favoured.Count > 0, "fixture region must actually lean, or this test tests nothing");
        var unfavoured = Enum.GetValues<ItemBaseType>()
            .Where(t => Gear.SlotFor(t) is not null)
            .First(t => !favoured.Contains(t));

        var noLean = At(12);
        Assert.Empty(ChestDossiers.For(noLean).Favoured);

        // Act + Assert: favoured slot passes, unfavoured fails, no-lean passes ANY slot filter —
        // a chest that can drop anything must never be thrown away by a slot choice.
        Assert.True(Chests.PassesKeepFilter(leaning, 0, favoured[0]));
        Assert.False(Chests.PassesKeepFilter(leaning, 0, unfavoured));
        Assert.True(Chests.PassesKeepFilter(noLean, 0, favoured[0]));
        Assert.True(Chests.PassesKeepFilter(noLean, 0, unfavoured));
    }

    [Fact]
    public void test_chest_filter_compensation_is_positive_and_grows_with_tier()
    {
        // Arrange
        var shallow = At(2);
        var deep = At(40);

        // Act
        var small = Chests.FilterCompensation(shallow);
        var big = Chests.FilterCompensation(deep);

        // Assert
        Assert.True(small > 0);
        Assert.True(big > small);
    }

    [Fact]
    public void test_chest_filter_settings_survive_a_save_round_trip()
    {
        // Arrange
        var save = new SaveGame { ChestKeepMinTier = 14, ChestKeepSlot = ItemBaseType.Ring.ToString() };

        // Act
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(save), nowMs: 0).Save!;

        // Assert
        Assert.Equal(14, back.ChestKeepMinTier);
        Assert.Equal("Ring", back.ChestKeepSlot);
    }

    [Fact]
    public void test_chest_filter_several_wanted_slots_pass_when_any_is_favoured()
    {
        // Arrange: a leaning chest; the wanted set holds an unfavoured slot AND a favoured one.
        var leaning = At(12, VerdantHollow.RegionId);
        var favoured = ChestDossiers.For(leaning).Favoured;
        var unfavoured = Enum.GetValues<ItemBaseType>().Where(t => Gear.SlotFor(t) is not null).First(t => !favoured.Contains(t));

        // Act + Assert: any favoured slot in the set keeps the chest; none does not; an empty set is "any".
        Assert.True(Chests.PassesKeepFilter(leaning, 0, new[] { unfavoured, favoured[0] }));
        Assert.False(Chests.PassesKeepFilter(leaning, 0, new[] { unfavoured }));
        Assert.True(Chests.PassesKeepFilter(leaning, 0, Array.Empty<ItemBaseType>()));
        Assert.True(Chests.PassesKeepFilter(At(12), 0, new[] { unfavoured }));   // no lean: anything goes
        Assert.False(Chests.PassesKeepFilter(leaning, 99, new[] { favoured[0] }));   // the tier floor still applies
    }
}
