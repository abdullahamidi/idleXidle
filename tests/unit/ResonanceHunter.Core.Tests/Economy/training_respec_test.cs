using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Economy;
using Xunit;

namespace ResonanceHunter.Core.Tests.Economy;

/// <summary>
/// RESET ALL TRAINING — the respec — must be a promise the economy keeps exactly.
/// </summary>
/// <remarks>
/// <para>
/// The refund is 100% of the Gleam ever spent on ranks, and it is COMPUTED from the geometric cost
/// curve rather than tracked in a counter, so the one property that matters is: for ANY pattern of
/// ranks, the refund equals precisely what those ranks cost to buy. A tracked total would be a second
/// copy of the formula, and this codebase has repeatedly found that the copy is the one that drifts.
/// </para>
/// <para>
/// The price side is one Crystal — the rarest forge material — and the tests here pin the charter
/// discipline: validate first, spend last, so the Crystal leaves the stock only when a reset actually
/// happens, and never for a reset that would refund nothing.
/// </para>
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
    public void test_the_refund_equals_what_was_paid_for_every_rank_pattern()
    {
        // Uneven on purpose: untouched stats, a single rank, a mid pile, and one stat at the cap —
        // the same shapes a real save reaches. Every pattern must refund exactly what it cost.
        var patterns = new[]
        {
            new Dictionary<HunterStat, int> { [HunterStat.AttackPower] = 1 },
            new Dictionary<HunterStat, int> { [HunterStat.Vitality] = 7, [HunterStat.Guile] = 3 },
            new Dictionary<HunterStat, int>
            {
                [HunterStat.AttackPower] = 12, [HunterStat.CriticalChance] = 6,
                [HunterStat.Defense] = 5, [HunterStat.Vitality] = 8,
            },
            Enum.GetValues<HunterStat>().ToDictionary(s => s, _ => ProgressionTuning.Default.StatRankCap),
        };

        foreach (var pattern in patterns)
        {
            var hunter = new Hunter();
            var paid = pattern.Sum(p => TrainPaying(hunter, p.Key, p.Value));

            Assert.Equal(paid, hunter.TrainingRefund());
        }
    }

    [Fact]
    public void test_the_refund_is_computed_from_the_tuning_actually_in_play()
    {
        // A steeper custom curve — the refund must follow the curve the Hunter was built with,
        // not the default. Boundary numbers ARE the point here, so they are asserted directly.
        var tuning = new ProgressionTuning { BaseTrainingCost = 10, TrainingGrowthRate = 2.0f };
        var hunter = new Hunter(tuning);
        var paid = TrainPaying(hunter, HunterStat.Focus, 3);   // 10 + 20 + 40

        Assert.Equal(70, paid);
        Assert.Equal(70, hunter.TrainingRefund());
    }

    [Fact]
    public void test_a_fresh_hunter_has_nothing_to_refund_and_cannot_reset()
    {
        var hunter = new Hunter();
        hunter.AddMaterial(Material.Crystal, 1);

        Assert.Equal(0, hunter.TrainingRefund());
        Assert.False(hunter.CanResetTraining);
        Assert.Equal(0, hunter.ResetTraining());
        // The refusal must be free: a Crystal spent on a reset that returns nothing is a paid refusal.
        Assert.Equal(1, hunter.MaterialOf(Material.Crystal));
    }

    [Fact]
    public void test_reset_returns_the_gleam_zeroes_every_rank_and_pays_exactly_once()
    {
        var hunter = new Hunter();
        var paid = TrainPaying(hunter, HunterStat.AttackPower, 9)
                   + TrainPaying(hunter, HunterStat.MaxHealth, 4)
                   + TrainPaying(hunter, HunterStat.Guile, 2);
        hunter.AddMaterial(Material.Crystal, 2);
        var gleamBefore = hunter.Gleam;

        var refund = hunter.ResetTraining();

        Assert.Equal(paid, refund);
        Assert.Equal(gleamBefore + paid, hunter.Gleam);
        foreach (var stat in Enum.GetValues<HunterStat>())
            Assert.Equal(0, hunter.RankOf(stat));
        Assert.Equal(1, hunter.MaterialOf(Material.Crystal));   // exactly one Crystal left the stock

        // Nothing is trained any more, so a second reset must be a free no-op — no double refund,
        // no second Crystal taken.
        Assert.Equal(0, hunter.ResetTraining());
        Assert.Equal(gleamBefore + paid, hunter.Gleam);
        Assert.Equal(1, hunter.MaterialOf(Material.Crystal));
    }

    [Fact]
    public void test_the_crystal_is_spent_only_when_the_reset_happens()
    {
        // Ranks to refund, but no Crystal: the reset must refuse and change nothing at all.
        var hunter = new Hunter();
        var paid = TrainPaying(hunter, HunterStat.Vitality, 5);
        var gleamBefore = hunter.Gleam;

        Assert.True(hunter.TrainingRefund() > 0);
        Assert.False(hunter.CanResetTraining);
        Assert.Equal(0, hunter.ResetTraining());
        Assert.Equal(gleamBefore, hunter.Gleam);
        Assert.Equal(5, hunter.RankOf(HunterStat.Vitality));
        Assert.Equal(paid, hunter.TrainingRefund());   // the debt is still owed, untouched

        // Hand over the Crystal and the same call goes through.
        hunter.AddMaterial(Material.Crystal, 1);
        Assert.True(hunter.CanResetTraining);
        Assert.Equal(paid, hunter.ResetTraining());
        Assert.Equal(0, hunter.MaterialOf(Material.Crystal));
    }

    [Fact]
    public void test_training_again_after_a_reset_starts_at_the_first_rank_price()
    {
        // The whole point of the respec: the curve starts over. After a reset the next rank of a
        // previously-deep stat costs the tutorial's first-rank price again.
        var hunter = new Hunter();
        TrainPaying(hunter, HunterStat.AttackPower, 20);
        hunter.AddMaterial(Material.Crystal, 1);
        hunter.ResetTraining();

        Assert.Equal(Hunter.CostOfRank(0, ProgressionTuning.Default),
                     hunter.NextRankCost(HunterStat.AttackPower));
    }
}
