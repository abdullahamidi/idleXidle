using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Warrens;
using Xunit;

namespace ResonanceHunter.Core.Tests.Warrens;

public class WarrenTests
{
    [Fact]
    public void test_new_warren_starts_at_level_one_with_eight_facilities_all_level_one()
    {
        var w = new Warren();

        Assert.Equal(1, w.Level);
        Assert.Equal(0, w.Xp);
        Assert.Equal(8, w.AllFacilities.Count());
        Assert.All(w.AllFacilities, f => Assert.Equal(1, f.Level));
    }

    [Fact]
    public void test_facility_output_scales_linearly_within_a_milestone_band()
    {
        var w = new Warren();
        var atOne = w.Facility(FacilityKind.Nursery).BaseOutputPerMin;   // L1, milestone tier 0

        w.Restore(level: 1, xp: 0, facilityLevels: new Dictionary<FacilityKind, int> { [FacilityKind.Nursery] = 4 });
        var atFour = w.Facility(FacilityKind.Nursery).BaseOutputPerMin;   // L4, still tier 0

        Assert.True(atFour > atOne);
        Assert.Equal(atOne * 4, atFour);   // linear in level below the first milestone
    }

    [Fact]
    public void test_facility_crosses_a_milestone_every_five_levels_and_output_jumps()
    {
        var below = new Warren();
        below.Restore(1, 0, new Dictionary<FacilityKind, int> { [FacilityKind.Nursery] = 4 });
        var at = new Warren();
        at.Restore(1, 0, new Dictionary<FacilityKind, int> { [FacilityKind.Nursery] = 5 });

        Assert.Equal(0, below.Facility(FacilityKind.Nursery).MilestoneTier);
        Assert.Equal(1, at.Facility(FacilityKind.Nursery).MilestoneTier);
        Assert.True(at.Facility(FacilityKind.Nursery).MilestoneMultiplier > 1f);

        // Crossing the milestone is worth MORE than a plain linear level (L5 beats 5/4 of L4).
        Assert.True(at.Facility(FacilityKind.Nursery).BaseOutputPerMin
                    > below.Facility(FacilityKind.Nursery).BaseOutputPerMin * 5 / 4);
    }

    [Fact]
    public void test_next_level_output_includes_a_milestone_jump()
    {
        var w = new Warren();
        w.Restore(1, 0, new Dictionary<FacilityKind, int> { [FacilityKind.Nursery] = 4 });
        var f = w.Facility(FacilityKind.Nursery);

        // Upgrading L4 -> L5 crosses a milestone, so NEXT output beats a plain linear step.
        Assert.True(f.NextLevelOutput > f.BaseOutputPerMin * 5 / 4);
        Assert.Equal(5, f.NextMilestoneLevel);   // from L4, the next milestone lands at L5
    }

    [Fact]
    public void test_higher_warren_level_multiplies_production()
    {
        var low = new Warren();
        var high = new Warren();
        high.Restore(level: 20, xp: 0, facilityLevels: null);

        Assert.True(high.Multiplier(WarrenResource.Gleam) > low.Multiplier(WarrenResource.Gleam));
        Assert.True(high.ProductionPerMinute(WarrenResource.Gleam) > low.ProductionPerMinute(WarrenResource.Gleam));
    }

    [Fact]
    public void test_conquering_regions_raises_production()
    {
        var none = new Warren();
        var some = new Warren { ConqueredRegions = 4 };

        Assert.True(some.ConquestBonus > 0f);
        Assert.True(some.ProductionPerMinute(WarrenResource.Gleam) > none.ProductionPerMinute(WarrenResource.Gleam));
    }

    [Fact]
    public void test_each_currency_is_produced_by_at_least_one_facility()
    {
        var w = new Warren();
        foreach (var r in new[] { WarrenResource.Gleam, WarrenResource.Mastery, WarrenResource.Dust })
            Assert.True(w.ProductionPerMinute(r) > 0, $"{r} should have a producing facility");
    }

    [Fact]
    public void test_upgrade_cost_rises_with_facility_level()
    {
        var w = new Warren();
        var cheap = w.UpgradeCost(FacilityKind.Nursery);
        w.Restore(level: 1, xp: 0, facilityLevels: new Dictionary<FacilityKind, int> { [FacilityKind.Nursery] = 15 });
        var dear = w.UpgradeCost(FacilityKind.Nursery);

        Assert.True(dear.Gleam > cheap.Gleam);
        Assert.True(dear.Mastery > cheap.Mastery);
        Assert.True(dear.Dust > cheap.Dust);
    }

    [Fact]
    public void test_can_afford_gates_on_all_three_currencies()
    {
        var w = new Warren();
        var c = w.UpgradeCost(FacilityKind.Nursery);

        Assert.True(w.CanAfford(FacilityKind.Nursery, c.Gleam, c.Mastery, c.Dust));
        Assert.False(w.CanAfford(FacilityKind.Nursery, c.Gleam - 1, c.Mastery, c.Dust));
        Assert.False(w.CanAfford(FacilityKind.Nursery, c.Gleam, c.Mastery - 1, c.Dust));
        Assert.False(w.CanAfford(FacilityKind.Nursery, c.Gleam, c.Mastery, c.Dust - 1));
    }

    [Fact]
    public void test_upgrade_raises_level_and_grants_xp()
    {
        var w = new Warren();
        var before = w.Facility(FacilityKind.Tunnels).Level;

        w.Upgrade(FacilityKind.Tunnels);

        Assert.Equal(before + 1, w.Facility(FacilityKind.Tunnels).Level);
        Assert.True(w.Xp > 0 || w.Level > 1);   // XP granted (possibly rolled into a level-up)
    }

    [Fact]
    public void test_enough_xp_levels_the_warren_up()
    {
        var w = new Warren();
        // Bulk-upgrade one facility many times; each upgrade grants XP that must eventually level the Warren.
        for (var i = 0; i < 40; i++) w.Upgrade(FacilityKind.HoardVaults);

        Assert.True(w.Level > 1);
    }

    [Fact]
    public void test_tick_accrues_whole_units_and_carries_the_remainder()
    {
        var w = new Warren();
        w.Restore(level: 10, xp: 0, facilityLevels: null);

        // A one-minute tick should credit roughly the per-minute rate.
        var perMin = w.ProductionPerMinute(WarrenResource.Gleam);
        var oneMinute = new Warren();
        oneMinute.Restore(level: 10, xp: 0, facilityLevels: null);
        var y = oneMinute.Tick(60f);

        Assert.Equal(perMin, y.Gleam);
    }

    [Fact]
    public void test_many_small_ticks_equal_one_big_tick()
    {
        // Determinism / no-truncation guard: 3600 one-second ticks must equal a single 3600-second tick.
        var incremental = new Warren();
        incremental.Restore(level: 15, xp: 0, facilityLevels: null);
        long g = 0, m = 0, d = 0;
        for (var i = 0; i < 3600; i++) { var t = incremental.Tick(1f); g += t.Gleam; m += t.Mastery; d += t.Dust; }

        var bulk = new Warren();
        bulk.Restore(level: 15, xp: 0, facilityLevels: null);
        var big = bulk.Tick(3600f);

        Assert.Equal(big.Gleam, g);
        Assert.Equal(big.Mastery, m);
        Assert.Equal(big.Dust, d);
    }

    [Fact]
    public void test_restore_round_trips_level_xp_and_facility_levels()
    {
        var w = new Warren();
        var levels = new Dictionary<FacilityKind, int>
        {
            [FacilityKind.Nursery] = 18, [FacilityKind.Tunnels] = 17, [FacilityKind.ForagingPits] = 16,
            [FacilityKind.ScavengerRuns] = 15, [FacilityKind.BreedingChamber] = 16, [FacilityKind.RitualNest] = 14,
            [FacilityKind.HoardVaults] = 13, [FacilityKind.SentryBurrows] = 12,
        };

        w.Restore(level: 23, xp: 18_540, facilityLevels: levels);

        Assert.Equal(23, w.Level);
        Assert.Equal(18_540, w.Xp);
        Assert.Equal(18, w.Facility(FacilityKind.Nursery).Level);
        Assert.Equal(12, w.Facility(FacilityKind.SentryBurrows).Level);
        Assert.Equal(levels, w.FacilityLevels);
    }
}
