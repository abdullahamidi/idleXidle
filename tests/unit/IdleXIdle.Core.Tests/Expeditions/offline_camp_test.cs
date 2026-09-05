using System;
using System.Linq;
using IdleXIdle.Core.Combat;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Warrens;
using Xunit;

namespace IdleXIdle.Core.Tests.Expeditions;

/// <summary>
/// THE CAMP'S LAW: an absence is capped, the cap and the efficiency grow with the Warren, and no
/// absence ever pays what the same hours of play pay.
/// </summary>
public class offline_camp_test
{
    private static Warren WarrenWith(int conquered, int levelEach)
    {
        var w = new Warren { ConqueredRegions = conquered, FacilityLevelCap = int.MaxValue };
        foreach (var f in Facilities.All)
            if (w.IsUnlocked(f.Kind))
                for (var i = 1; i < levelEach; i++) w.Upgrade(f.Kind);
        return w;
    }

    private static OfflineHunt.Result Sim(double seconds)
        => OfflineHunt.Simulate(TestBuilds.Of("hammer_blow"), new Hunter(), seconds, "", AttackBias.Balanced,
                                enemyBaseHealth: 110f, enemyBaseDamage: 9f, seed: 11);

    [Fact]
    public void test_offline_camp_a_fresh_account_holds_two_hours_and_no_more()
    {
        // Arrange / Act
        var held = OfflineCamp.CreditedSeconds(24 * 3600, null);

        // Assert
        Assert.Equal(OfflineCamp.BaseHours * 3600.0, held, 1);
        Assert.Equal(2f, OfflineCamp.HoursFor(null));
        Assert.Equal(OfflineCamp.BaseEfficiency, OfflineCamp.EfficiencyFor(null));
    }

    [Fact]
    public void test_offline_camp_capacity_and_efficiency_grow_with_the_warren_and_stop_at_the_ceiling()
    {
        // Arrange
        var fresh = new Warren();
        var early = WarrenWith(1, 4);
        var mid = WarrenWith(3, 9);
        var developed = WarrenWith(5, 25);

        // Assert — strictly more capacity and efficiency at each step, up to the ceilings.
        Assert.True(OfflineCamp.HoursFor(early) > OfflineCamp.HoursFor(fresh));
        Assert.True(OfflineCamp.HoursFor(mid) > OfflineCamp.HoursFor(early));
        Assert.True(OfflineCamp.HoursFor(developed) >= OfflineCamp.HoursFor(mid));
        Assert.Equal(OfflineCamp.MaxHours, OfflineCamp.HoursFor(developed));
        Assert.True(OfflineCamp.EfficiencyFor(early) > OfflineCamp.EfficiencyFor(fresh));
        Assert.True(OfflineCamp.EfficiencyFor(mid) > OfflineCamp.EfficiencyFor(early));
        Assert.Equal(OfflineCamp.MaxEfficiency, OfflineCamp.EfficiencyFor(developed));
        Assert.True(OfflineCamp.MaxEfficiency < 1f, "away must never be as good as playing");
        // One more level is a visible, sellable step.
        Assert.True(OfflineCamp.HoursAfterOneMoreLevel(early) > OfflineCamp.HoursFor(early));
    }

    [Fact]
    public void test_offline_camp_a_day_away_never_out_earns_the_hours_it_holds_of_play()
    {
        // Arrange — the same absence on a fresh account and a developed one.
        var developed = WarrenWith(5, 25);
        var hour = Sim(3600);
        var live = hour.GleamPerSecond * 3600;

        // Act
        var freshHeld = Sim(OfflineCamp.CreditedSeconds(24 * 3600, null));
        var developedHeld = Sim(OfflineCamp.CreditedSeconds(24 * 3600, developed));
        var freshDay = OfflineCamp.HuntCredit(freshHeld, null, 24 * 3600);
        var developedDay = OfflineCamp.HuntCredit(developedHeld, developed, 24 * 3600);

        // Assert — the model's own promise: a fresh day holds under one hour of play's worth
        // (2 h × 0.3), so by construction the fresh day is under an hour of the SAME hunt.
        Assert.True(OfflineCamp.BaseHours * OfflineCamp.BaseEfficiency < 1f, "a fresh day must be worth less than an hour of play");
        Assert.True(freshDay < freshHeld.GleamPerSecond * 3600 + 1, $"fresh day {freshDay} vs an hour {freshHeld.GleamPerSecond * 3600}");
        Assert.True(freshDay < live * 1.25f + 1, $"fresh day {freshDay} vs a measured hour {live}");
        Assert.True(developedDay <= developedHeld.GleamPerSecond * OfflineCamp.MaxHours * 3600 * OfflineCamp.MaxEfficiency + 1);
        Assert.True(developedDay > freshDay, "the Warren must make an absence worth more");
    }

    [Fact]
    public void test_offline_camp_hours_read_as_hours_and_minutes()
    {
        Assert.Equal("2H", OfflineCamp.HoursText(2f));
        Assert.Equal("2H 30M", OfflineCamp.HoursText(2.5f));
        Assert.Equal("12H", OfflineCamp.HoursText(12f));
        Assert.Equal("0H 42M", OfflineCamp.HoursText(0.7f));
    }
}
