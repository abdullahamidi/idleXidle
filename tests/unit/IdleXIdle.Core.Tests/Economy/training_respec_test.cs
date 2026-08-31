using System;
using IdleXIdle.Core.Economy;
using Xunit;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// RESET ALL TRAINING — the respec — erases every trained rank for one Crystal and gives NOTHING back.
/// </summary>
/// <remarks>
/// The designer's call (2026-08-24): the first cut refunded 100% of the Gleam, which made the reset a
/// free re-deal — undo, retrain, repeat. Now the Gleam spent on ranks is gone for good, the armed
/// confirm says so in plain words, and the Crystal is the gate. The charter discipline stays:
/// validate first, spend last, so the Crystal leaves the stock only when a reset actually happens.
/// </remarks>
public class TrainingRespecTest
{
    /// <summary>Train a stat n ranks, paying the real price each time, and return the Gleam it took.</summary>
    private static int TrainPaying(Hunter hunter, HunterStat stat, int ranks)
    {
        var paid = 0;
        for (var i = 0; i < ranks; i++)
        {
            var cost = hunter.NextRankCost(stat);
            hunter.AddGleam(cost);
            paid += cost;
            Assert.True(hunter.Train(stat), $"training {stat} rank {i} must succeed when its exact cost is held");
        }
        return paid;
    }

    [Fact]
    public void test_reset_gives_no_gleam_back()
    {
        // Arrange — a trained hunter holding the Crystal price and some walking-around Gleam.
        var h = new Hunter();
        TrainPaying(h, HunterStat.AttackPower, 6);
        TrainPaying(h, HunterStat.MaxHealth, 3);
        h.AddMaterial(Material.Crystal, 1);
        h.AddGleam(500);
        var gleamBefore = h.Gleam;

        // Act
        var did = h.ResetTraining();

        // Assert — ranks erased, Crystal spent, and NOT ONE Gleam returned.
        Assert.True(did);
        Assert.Equal(0, h.TotalTrainedRanks);
        Assert.Equal(0, h.MaterialOf(Material.Crystal));
        Assert.Equal(gleamBefore, h.Gleam);
    }

    [Fact]
    public void test_reset_refuses_without_the_crystal_and_takes_nothing()
    {
        var h = new Hunter();
        TrainPaying(h, HunterStat.AttackPower, 4);
        var ranks = h.TotalTrainedRanks;
        var gleam = h.Gleam;

        Assert.False(h.CanResetTraining);
        Assert.False(h.ResetTraining());
        Assert.Equal(ranks, h.TotalTrainedRanks);   // nothing erased
        Assert.Equal(gleam, h.Gleam);               // nothing paid, nothing paid out
    }

    [Fact]
    public void test_reset_refuses_when_there_is_nothing_to_erase()
    {
        // A reset that takes the rarest material and erases nothing would be a paid refusal.
        var h = new Hunter();
        h.AddMaterial(Material.Crystal, 3);

        Assert.False(h.CanResetTraining);
        Assert.False(h.ResetTraining());
        Assert.Equal(3, h.MaterialOf(Material.Crystal));
    }

    [Fact]
    public void test_reset_spends_exactly_one_crystal_and_a_second_reset_refuses()
    {
        var h = new Hunter();
        TrainPaying(h, HunterStat.Guile, 2);
        h.AddMaterial(Material.Crystal, 5);

        Assert.True(h.ResetTraining());
        Assert.Equal(4, h.MaterialOf(Material.Crystal));
        Assert.False(h.ResetTraining());            // nothing left to erase
        Assert.Equal(4, h.MaterialOf(Material.Crystal));
    }

    [Fact]
    public void test_after_a_reset_the_next_rank_costs_the_rank_zero_price_again()
    {
        var h = new Hunter();
        var firstRankCost = h.NextRankCost(HunterStat.AttackPower);
        TrainPaying(h, HunterStat.AttackPower, 8);
        Assert.True(h.NextRankCost(HunterStat.AttackPower) > firstRankCost);   // the curve climbed

        h.AddMaterial(Material.Crystal, 1);
        Assert.True(h.ResetTraining());

        Assert.Equal(firstRankCost, h.NextRankCost(HunterStat.AttackPower));   // back at the bottom
    }
}
