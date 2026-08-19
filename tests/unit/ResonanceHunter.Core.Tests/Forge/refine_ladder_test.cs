using System;
using System.Linq;
using ResonanceHunter.Core.Forging;
using ResonanceHunter.Core.Loot;
using Xunit;

namespace ResonanceHunter.Core.Tests.Forging;

/// <summary>
/// The refine LADDER: 15 rungs, the first five safe, a rising slip chance, a hard top.
/// </summary>
/// <remarks>
/// Playtest, upgrade rework: "en yüksek upgrade seviyesinin +15 olarak sınırlayarak, upgrade cost'u
/// biraz arttırarak ve upgrade yapıldığında giderek yükselecek şekilde downgrade oranı koyarak."
/// </remarks>
public class RefineLadderTest
{
    private static readonly ForgeTuning T = ForgeTuning.Default;

    private static ItemInstance At(int upgrades, int ilvl = 10) => new()
    {
        InstanceId = "itm_ladder", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Rare,
        SellValue = 30, ItemLevel = ilvl, Upgrades = upgrades,
    };

    [Fact]
    public void test_refine_ladder_first_five_rungs_never_fail_then_risk_climbs_to_a_cap()
    {
        // Arrange + Act + Assert: safe rungs, then strictly rising, then capped.
        for (var u = 0; u < T.RefineSafeUpgrades; u++)
            Assert.Equal(0, Forge.RefineFailChance(At(u), T));

        var prev = 0.0;
        for (var u = T.RefineSafeUpgrades; u < T.MaxUpgrades; u++)
        {
            var chance = Forge.RefineFailChance(At(u), T);
            Assert.True(chance > 0 && chance <= T.RefineFailCap,
                $"rung {u + 1} has chance {chance:P0} outside (0, {T.RefineFailCap:P0}]");
            Assert.True(chance >= prev, $"rung {u + 1} got SAFER than rung {u}");
            prev = chance;
        }
    }

    [Fact]
    public void test_refine_ladder_stops_at_fifteen()
    {
        Assert.False(Forge.AtRefineCap(At(14), T));
        Assert.True(Forge.AtRefineCap(At(15), T));
    }

    [Fact]
    public void test_refine_ladder_cost_climbs_with_the_rung()
    {
        // The same item level, a higher rung: strictly dearer in both currencies.
        var low = Forge.Refine(At(0), T);
        var high = Forge.Refine(At(10), T);

        Assert.True(high.Scrap > low.Scrap);
        Assert.True(high.Gold > low.Gold);
    }

    [Fact]
    public void test_refine_ladder_a_roll_either_climbs_or_slips_one_whole_rung()
    {
        // Arrange: rung 15's attempt carries the biggest risk — both outcomes must appear.
        var item = At(14, ilvl: 20);
        var climbed = 0;
        var slipped = 0;

        for (var seed = 0; seed < 200; seed++)
        {
            var o = Forge.TryRefine(item, T, new Random(seed));
            if (o.Failed)
            {
                slipped++;
                Assert.Equal(19, o.Product.ItemLevel);
                Assert.Equal(13, o.Product.Upgrades);   // the slip re-cheapens the next attempt
            }
            else
            {
                climbed++;
                Assert.Equal(21, o.Product.ItemLevel);
                Assert.Equal(15, o.Product.Upgrades);
            }
            Assert.Equal(item.InstanceId, o.Product.InstanceId);
        }

        Assert.True(climbed > 0 && slipped > 0,
            $"200 rolls at rung 15 produced climbed={climbed}, slipped={slipped} — the risk is not real");
    }

    [Fact]
    public void test_refine_ladder_a_slip_never_falls_below_the_floor()
    {
        // A level-1, rung-0 item cannot go negative even if a slip lands (forced via many rolls at a
        // synthetic high rung with ilvl 1).
        var fragile = At(14, ilvl: 1);
        var slip = Enumerable.Range(0, 300)
            .Select(s => Forge.TryRefine(fragile, T, new Random(s)))
            .First(o => o.Failed);

        Assert.Equal(1, slip.Product.ItemLevel);
        Assert.Equal(13, slip.Product.Upgrades);
    }

    [Fact]
    public void test_refine_ladder_greater_refine_is_safe_and_respects_the_cap()
    {
        // +5 normally; only what remains near the top; zero at the top.
        Assert.Equal(5, Forge.GreaterRefine(At(0), T).Steps);
        Assert.Equal(2, Forge.GreaterRefine(At(13), T).Steps);
        Assert.Equal(0, Forge.GreaterRefine(At(15), T).Steps);

        var near = Forge.GreaterRefine(At(13, ilvl: 10), T);
        Assert.Equal(12, near.Product.ItemLevel);
        Assert.Equal(15, near.Product.Upgrades);
    }
}
