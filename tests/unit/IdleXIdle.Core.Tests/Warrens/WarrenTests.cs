using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Warrens;
using Xunit;

namespace IdleXIdle.Core.Tests.Warrens;

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
        // At full conquest — the material facilities sit at the end of the unlock ramp.
        var w = new Warren { ConqueredRegions = 6 };
        foreach (var r in new[] { WarrenResource.Gleam, WarrenResource.Dust, WarrenResource.Scrap, WarrenResource.Essence })
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
        Assert.True(dear.Dust > cheap.Dust);
    }

    [Fact]
    public void test_can_afford_gates_on_both_currencies()
    {
        var w = new Warren();
        var c = w.UpgradeCost(FacilityKind.Nursery);

        Assert.True(w.CanAfford(FacilityKind.Nursery, c.Gleam, c.Dust));
        Assert.False(w.CanAfford(FacilityKind.Nursery, c.Gleam - 1, c.Dust));
        Assert.False(w.CanAfford(FacilityKind.Nursery, c.Gleam, c.Dust - 1));
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
        // Full conquest, so all four channels (materials included) are exercised.
        var incremental = new Warren { ConqueredRegions = 6 };
        incremental.Restore(level: 15, xp: 0, facilityLevels: null);
        long g = 0, d = 0, s = 0, e = 0;
        for (var i = 0; i < 3600; i++) { var t = incremental.Tick(1f); g += t.Gleam; d += t.Dust; s += t.Scrap; e += t.Essence; }

        var bulk = new Warren { ConqueredRegions = 6 };
        bulk.Restore(level: 15, xp: 0, facilityLevels: null);
        var big = bulk.Tick(3600f);

        Assert.Equal(big.Gleam, g);
        Assert.Equal(big.Dust, d);
        Assert.Equal(big.Scrap, s);
        Assert.Equal(big.Essence, e);
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

    [Fact]
    public void test_the_depth_a_facility_needs_follows_its_own_level_not_the_cap()
    {
        // The screen used to answer a blocked upgrade with the CAP, and the cap is the wrong number
        // twice: a player cannot spend it, and it can contradict the level printed directly above it.
        // FacilityLevelCap is derived by the host from the deepest run ANYWHERE, so if that figure
        // falls — a region id renamed out from under its recorded BestDepth would do it — a level 18
        // facility sits under a cap of 1 and the panel announces "CAPPED AT LEVEL 1" beside "LEVEL 18".
        var w = new Warren();
        w.Restore(level: 23, xp: 0, facilityLevels: new Dictionary<FacilityKind, int>
        {
            [FacilityKind.Nursery] = 18,
        });

        w.FacilityLevelCap = 1;   // the contradictory state, reproduced

        Assert.True(w.IsAtLevelCap(FacilityKind.Nursery));
        Assert.Equal(95, w.DepthForNextLevel(FacilityKind.Nursery));   // (18 + 1) * 5
        Assert.True(w.DepthForNextLevel(FacilityKind.Nursery)
                    > w.Facility(FacilityKind.Nursery).Level,
                    "the answer must never be a number below the level already on screen");
    }

    [Fact]
    public void test_a_fresh_facility_asks_for_one_levels_worth_of_depth()
    {
        var w = new Warren();
        Assert.Equal(1, w.Facility(FacilityKind.Nursery).Level);
        Assert.Equal(2 * Warren.DepthPerFacilityLevel, w.DepthForNextLevel(FacilityKind.Nursery));
    }

    // ── P12: unlock-by-conquest, the material facilities, the depth-cap derivation ──

    /// <summary>game-flow 3.8: facilities unlock by conquest — a fresh Warren is three cards, not eight.</summary>
    [Fact]
    public void test_facilities_unlock_one_per_conquest_in_catalog_order()
    {
        var unopened = new Warren();                        // 0 conquests — the Warren itself is not even open
        Assert.Equal(2, unopened.UnlockedFacilityCount);

        var opened = new Warren { ConqueredRegions = 1 };   // the conquest that opens the Warren
        Assert.Equal(3, opened.UnlockedFacilityCount);
        Assert.True(opened.IsUnlocked(Facilities.All[0].Kind));
        Assert.True(opened.IsUnlocked(Facilities.All[2].Kind));
        Assert.False(opened.IsUnlocked(Facilities.All[3].Kind));

        var full = new Warren { ConqueredRegions = 6 };     // full current content
        Assert.All(full.AllFacilities, f => Assert.True(full.IsUnlocked(f.Kind)));
    }

    /// <summary>A locked facility neither produces nor upgrades — it is a promise, not a faucet.</summary>
    [Fact]
    public void test_a_locked_facility_produces_nothing_and_cannot_upgrade()
    {
        var w = new Warren { ConqueredRegions = 1 };
        const long plenty = 1_000_000_000;
        var locked = Facilities.All[^1].Kind;   // the last ramp slot — far beyond one conquest

        Assert.False(w.IsUnlocked(locked));
        Assert.False(w.CanUpgrade(locked, plenty, plenty));
        Assert.Equal(0, w.ProductionPerMinute(WarrenResource.Essence));   // both Essence facilities locked
    }

    /// <summary>The grandfather rule: a facility already raised past level 1 stays open under any ramp.</summary>
    /// <remarks>
    /// The ramp shipped after the facilities did. A save that invested in a building must never watch
    /// its production vanish because a rule arrived later.
    /// </remarks>
    [Fact]
    public void test_a_facility_already_raised_stays_unlocked()
    {
        var w = new Warren();   // 0 conquests — BreedingChamber's ramp slot is the very last
        w.Restore(level: 1, xp: 0, facilityLevels: new Dictionary<FacilityKind, int>
        {
            [FacilityKind.BreedingChamber] = 3,
        });

        Assert.True(w.IsUnlocked(FacilityKind.BreedingChamber));
        Assert.True(w.ProductionPerMinute(WarrenResource.Essence) > 0);
    }

    /// <summary>The host's cap arithmetic lives on the model: one facility level per five proven waves.</summary>
    [Fact]
    public void test_the_depth_cap_derivation_is_one_level_per_five_waves_with_a_floor_of_one()
    {
        Assert.Equal(1, Warren.CapForDepth(0));
        Assert.Equal(1, Warren.CapForDepth(4));    // the floor: a new player can still read the screen
        Assert.Equal(1, Warren.CapForDepth(5));
        Assert.Equal(2, Warren.CapForDepth(10));
        Assert.Equal(12, Warren.CapForDepth(60));
    }

    /// <summary>The two material facilities pay the Forge's wallets — Scrap and Essence, in the yield.</summary>
    [Fact]
    public void test_the_material_facilities_pay_scrap_and_essence()
    {
        var w = new Warren { ConqueredRegions = 6 };
        var y = w.Tick(600f);   // ten minutes, so the slow Essence trickle clears whole units

        Assert.True(y.Scrap > 0, "SCAVENGER RUNS / HOARD VAULTS should have paid Scrap");
        Assert.True(y.Essence > 0, "RITUAL NEST / BREEDING CHAMBER should have paid Essence");
    }
}
