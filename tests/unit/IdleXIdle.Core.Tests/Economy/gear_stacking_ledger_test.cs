using System.Linq;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using Xunit;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// THE STACKING LEDGER (P11b, decision G7): every gear channel's ceiling, pinned in one place, so
/// "additive inside a channel, saturating at its ceiling" stays a law instead of a habit.
/// </summary>
/// <remarks>
/// RECORDED, NOT ASSERTED — two audit findings that are playtest questions, not numbers to move
/// blind: (1) total DEFENCE is an unbounded additive sum (Defense affixes + WARD gems, player-
/// chooseable) under its hyperbolic 100/(100+D) — the one remaining runaway candidate; (2) the
/// three item-level curves saturate at DIFFERENT ceilings (rarity-direct 2.0, affixes 2.8,
/// family/gems 4.0), so a refine buys different amounts of different layers on the same item.
/// </remarks>
public class GearStackingLedgerTest
{
    [Fact]
    public void test_worn_channel_bonuses_saturate_below_their_ceilings()
    {
        // Eight identical bonus pieces approach a channel's ceiling and never reach it — the n-th
        // piece of the same thing always buys less than the one before.
        GearMods Only(float d = 1f, float h = 1f, float l = 1f, float r = 1f)
            => new(Damage: d, Health: h, Haul: l, SkillRate: r);

        var damage = GearMods.Stack(Enumerable.Repeat(Only(d: 1.5f), 8));
        Assert.True(damage.Damage < 1f + GearMods.DamageCeiling);
        Assert.True(damage.Damage > 1f + GearMods.DamageCeiling * 0.5f, "eight pieces should get well past half the ceiling");

        Assert.True(GearMods.Stack(Enumerable.Repeat(Only(h: 1.5f), 8)).Health < 1f + GearMods.HealthCeiling);
        Assert.True(GearMods.Stack(Enumerable.Repeat(Only(l: 1.5f), 8)).Haul < 1f + GearMods.HaulCeiling);
        Assert.True(GearMods.Stack(Enumerable.Repeat(Only(r: 1.5f), 8)).SkillRate < 1f + GearMods.SkillRateCeiling);

        // And stacked DRAWBACKS saturate too, floored so nothing ever zeroes a channel.
        Assert.True(GearMods.Stack(Enumerable.Repeat(Only(d: 0.5f), 12)).Damage >= 0.25f);
    }

    [Fact]
    public void test_the_three_item_level_curves_hold_their_own_ceilings()
    {
        // Rarity-direct (weapon/charm/focus): 1 → 2, never past it. REFINE stays an infinite gold
        // sink that buys bounded power.
        Assert.True(Gear.ItemLevelFactor(1_000_000) < 2f);
        Assert.True(Gear.ItemLevelFactor(60) > Gear.ItemLevelFactor(20));

        // Affixes: asymptote 1 + slope 0.04 × half-point 45 = 2.8 (the remark used to promise 4).
        Assert.True(ItemAffixes.IlvlFactor(1_000_000) < 2.8f);
        Assert.True(ItemAffixes.IlvlFactor(1_000_000) > 2.7f);

        // Family favour and gems: linear ramps into a hard 4× cap — level past the cap buys nothing.
        ItemInstance W(int il) => new()
        {
            InstanceId = "w", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Common,
            SellValue = 1, ItemLevel = il, Family = 0,
        };
        Assert.Equal(ItemFamilies.BonusOf(W(200))!.Value.Magnitude,
                     ItemFamilies.BonusOf(W(100_000))!.Value.Magnitude, precision: 4);

        ItemInstance G(int il) => new()
        {
            InstanceId = "g", BaseType = ItemBaseType.Gem, Rarity = Rarity.Common,
            SellValue = 1, ItemLevel = il,
        };
        Assert.Equal(GemCraft.Magnitude(G(200)), GemCraft.Magnitude(G(100_000)), precision: 4);
    }

    [Fact]
    public void test_the_weapon_multiplier_tops_out_below_fifteen()
    {
        // 1 + RarityPower(Legendary)=7 × ItemLevelFactor→2: approaches 15, never reaches it.
        var legendary = new ItemInstance
        {
            InstanceId = "w", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Legendary,
            SellValue = 1, ItemLevel = 1_000_000,
        };
        Assert.True(Gear.WeaponDamageMultiplier(legendary) < 15f);
        Assert.True(Gear.WeaponDamageMultiplier(legendary) > 14f);
    }
}
