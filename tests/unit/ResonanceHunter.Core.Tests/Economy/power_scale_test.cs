using System.Linq;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using Xunit;

namespace ResonanceHunter.Core.Tests.Economy;

/// <summary>
/// The SCALE of power, pinned end to end. Every per-item cap in the economy was green while a full set
/// of eight compounded to a seven-digit ITEM POWER (playtest 2026-08-23: "1.5m item power veren silah
/// var") — nobody had ever asked what eight items do at once. These tests do.
/// </summary>
public class PowerScaleTests
{
    private static ItemInstance Item(string id, ItemBaseType type, Rarity rarity, int ilvl, GearTrait trait)
        => new() { InstanceId = id, BaseType = type, Rarity = rarity, SellValue = 1, ItemLevel = ilvl, TraitOverride = trait };

    /// <summary>A realistic endgame set: every slot Legendary at a refined item level, traits chosen the
    /// way a min-maxer would (FOCUSED on every slot that can take it, HEAVY weapon, VITAL charm).</summary>
    private static Hunter EndgameSet(int ilvl = 45)
    {
        var h = new Hunter();
        h.Equip(Item("w", ItemBaseType.Weapon, Rarity.Legendary, ilvl, GearTrait.Heavy));
        h.Equip(Item("c", ItemBaseType.Charm, Rarity.Legendary, ilvl, GearTrait.Vital));
        foreach (var (id, type) in new[]
                 {
                     ("f", ItemBaseType.AbilityFocus), ("h", ItemBaseType.Helm), ("ch", ItemBaseType.Chest),
                     ("g", ItemBaseType.Gloves), ("b", ItemBaseType.Boots), ("r", ItemBaseType.Ring),
                 })
            h.Equip(Item(id, type, Rarity.Legendary, ilvl, GearTrait.Focused));
        return h;
    }

    [Fact]
    public void test_a_bare_hunter_reads_in_the_low_hundreds()
    {
        var bare = new Hunter().PowerRating;
        Assert.InRange(bare, 100, 400);
    }

    [Fact]
    public void test_a_full_legendary_set_stays_in_four_digits()
    {
        // The whole point. Before the 2026-08-23 rebalance this fixture computed to ~24,000,000.
        var geared = EndgameSet();
        Assert.InRange(geared.PowerRating, 1_000, 9_999);
    }

    [Fact]
    public void test_the_weapon_is_the_biggest_item_but_not_a_million()
    {
        var geared = EndgameSet();
        var weapon = geared.Worn(GearSlot.Weapon)!;
        var contribution = geared.PowerContribution(weapon);
        Assert.InRange(contribution, 200, 8_000);
        // And it is still the item that matters most — the design's "a Legendary weapon transforms you".
        var others = new[] { GearSlot.Charm, GearSlot.Focus, GearSlot.Helm, GearSlot.Chest, GearSlot.Gloves, GearSlot.Boots, GearSlot.Ring }
            .Select(s => geared.PowerContribution(geared.Worn(s)!)).Max();
        Assert.True(contribution > others, $"weapon {contribution} should outrank every other slot ({others})");
    }

    [Fact]
    public void test_eight_focused_slots_cannot_run_the_skill_clock_away()
    {
        // Six FOCUSED Legendaries used to stack to a x290 skill-rate channel. With additive, saturating
        // stacking the worn channel stays under its ceiling (+150%) whatever is worn.
        var geared = EndgameSet();
        Assert.True(geared.WornMods.SkillRate < 1f + GearMods.SkillRateCeiling,
            $"worn skill rate {geared.WornMods.SkillRate} must stay under the ceiling");
        Assert.True(geared.WornMods.SkillRate > 1.5f, "and a full FOCUSED set must still be a real bonus");
    }

    [Fact]
    public void test_more_rarity_and_level_still_mean_more()
    {
        // Saturation must not flatten the ladder the player climbs: a Rare set < an Epic set < Legendary,
        // and a refined set beats the same set at level 1.
        int Rating(Rarity r, int ilvl)
        {
            var h = new Hunter();
            h.Equip(Item("w", ItemBaseType.Weapon, r, ilvl, GearTrait.Heavy));
            h.Equip(Item("f", ItemBaseType.AbilityFocus, r, ilvl, GearTrait.Focused));
            h.Equip(Item("h", ItemBaseType.Helm, r, ilvl, GearTrait.Focused));
            return h.PowerRating;
        }
        Assert.True(Rating(Rarity.Rare, 10) < Rating(Rarity.Epic, 10));
        Assert.True(Rating(Rarity.Epic, 10) < Rating(Rarity.Legendary, 10));
        Assert.True(Rating(Rarity.Legendary, 1) < Rating(Rarity.Legendary, 30));
    }
}
