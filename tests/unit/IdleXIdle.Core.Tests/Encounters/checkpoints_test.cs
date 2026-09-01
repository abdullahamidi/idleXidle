using System;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using Xunit;

namespace IdleXIdle.Core.Tests.Encounters;

/// <summary>Pins conquest at wave 20 and the checkpoints Memory Dust buys (Checkpoints).</summary>
public class checkpoints_test
{
    [Fact]
    public void test_conquest_is_the_twentieth_wave()
        => Assert.Equal(20, Checkpoints.ConquestWave);

    [Fact]
    public void test_a_conquered_region_offers_a_start_every_ten_waves_up_to_its_record()
    {
        Assert.Equal(new[] { 0, 10, 20, 30, 40 }, Checkpoints.Options(bestDepth: 43, conquered: true));
        Assert.Equal(new[] { 0 }, Checkpoints.Options(bestDepth: 43, conquered: false));
        Assert.Equal(new[] { 0 }, Checkpoints.Options(bestDepth: 9, conquered: true));
    }

    [Fact]
    public void test_a_checkpoint_costs_dust_per_wave_skipped_and_the_top_is_free()
    {
        Assert.Equal(0, Checkpoints.DustCost(0));
        Assert.Equal(30 * Checkpoints.DustPerWave, Checkpoints.DustCost(30));
    }

    [Fact]
    public void test_a_remembered_start_falls_back_when_the_record_or_the_conquest_is_gone()
    {
        Assert.Equal(30, Checkpoints.Clamp(30, bestDepth: 35, conquered: true));
        Assert.Equal(20, Checkpoints.Clamp(30, bestDepth: 25, conquered: true));   // record below the wish
        Assert.Equal(0, Checkpoints.Clamp(30, bestDepth: 35, conquered: false));  // not conquered: the top
        Assert.Equal(0, Checkpoints.Clamp(-5, bestDepth: 35, conquered: true));
    }

    [Fact]
    public void test_a_descent_started_at_a_checkpoint_fights_the_next_wave_first()
    {
        var hunter = new Hunter();
        var build = new Build();
        var champ = new Champion { MaxHealth = 1_000_000, Health = 1_000_000 };
        var run = new SoloExpedition(build, champ, hunter, enemyBaseHealth: 1f, enemyBaseDamage: 0f, rng: new Random(3));
        run.StartAtWave(30);
        Assert.Equal(30, run.Wave);
        var outcome = run.PushWave();
        Assert.Equal(WaveOutcome.Cleared, outcome);
        Assert.Equal(31, run.Wave);
        Assert.Throws<InvalidOperationException>(() => run.StartAtWave(10));   // never mid-descent
    }
}
