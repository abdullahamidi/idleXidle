using System;
using IdleXIdle.Core.Economy;
using Xunit;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// <see cref="Hunter.Preview{T}"/> — the one Core member the TRAINING screen's before → after rests on.
/// </summary>
/// <remarks>
/// The value of these tests is that they compare the preview against ACTUALLY TRAINING the stat. A
/// preview that agrees with a formula copied beside it proves nothing; a preview that agrees with the
/// purchase proves the screen cannot promise a number the game will not deliver.
/// </remarks>
public class HunterPreviewTest
{
    private static Hunter Fresh()
    {
        var h = new Hunter();
        h.AddGleam(10_000_000);
        return h;
    }

    [Fact]
    public void test_preview_restores_the_rank_and_the_gleam()
    {
        var h = Fresh();
        var rank = h.RankOf(HunterStat.AttackPower);
        var gleam = h.Gleam;

        h.Preview(HunterStat.AttackPower, x => x.AutoDamageMultiplier);

        Assert.Equal(rank, h.RankOf(HunterStat.AttackPower));
        Assert.Equal(gleam, h.Gleam);
    }

    [Fact]
    public void test_preview_matches_actually_training_it()
    {
        foreach (var stat in Enum.GetValues<HunterStat>())
        {
            var h = Fresh();
            var promised = h.Preview(stat, x => x.ValueOf(stat));
            Assert.True(h.Train(stat), $"{stat} could not be trained with a full purse");
            Assert.Equal(promised, h.ValueOf(stat));
        }
    }

    [Fact]
    public void test_preview_of_a_derived_number_matches_the_purchase()
    {
        // The point of the member: a stat whose fight number is behind a private constant.
        var h = Fresh();
        var promised = h.Preview(HunterStat.AttackPower, x => x.AutoDamageMultiplier);
        h.Train(HunterStat.AttackPower);
        Assert.Equal(promised, h.AutoDamageMultiplier);
    }

    [Fact]
    public void test_preview_at_the_cap_reads_the_current_value()
    {
        var h = Fresh();
        while (h.RankOf(HunterStat.Guile) < h.StatRankCap) Assert.True(h.Train(HunterStat.Guile));

        Assert.Equal(h.HaulMultiplier, h.Preview(HunterStat.Guile, x => x.HaulMultiplier));
        Assert.Equal(h.StatRankCap, h.RankOf(HunterStat.Guile));
    }
}
