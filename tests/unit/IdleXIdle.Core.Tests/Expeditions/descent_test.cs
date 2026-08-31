using System;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using Xunit;

namespace IdleXIdle.Core.Tests.Expeditions;

/// <summary>
/// The Core run driver (P5). These are the rules the hunt screen used to enforce privately —
/// a fresh champion per descent, one reward per cleared wave, checkpoint starts, a monotonic run
/// index — now stated against the headless machine both the screen and OfflineHunt drive.
/// </summary>
public class DescentTest
{
    private static Descent NewDescent() => new() { Rng = new Random(7) };

    [Fact]
    public void test_a_descent_mints_a_fresh_champion_per_run_and_counts_them()
    {
        // Arrange — enemies no bare build survives.
        var d = NewDescent();
        var build = TestBuilds.Of("hammer_blow");
        var hunter = new Hunter();

        // Act — first life dies; the second is minted whole.
        d.StartRun(build, hunter, enemyBaseHealth: 1e7f, enemyBaseDamage: 1e7f);
        var first = d.PushWave();
        var fallenChampion = d.Champion!;
        d.StartRun(build, hunter, enemyBaseHealth: 1e7f, enemyBaseDamage: 1e7f);

        // Assert
        Assert.NotEqual(WaveOutcome.Cleared, first);
        Assert.True(d.Run!.Over == false, "a fresh StartRun must hand back a run that is not over");
        Assert.NotSame(fallenChampion, d.Champion);
        Assert.Equal(d.Champion!.MaxHealth, d.Champion.Health);
        Assert.Equal(2, d.RunIndex);
    }

    [Fact]
    public void test_each_cleared_wave_queues_exactly_one_reward_in_order()
    {
        // Arrange — enemies a bare BLOW flattens.
        var d = NewDescent();
        d.StartRun(TestBuilds.Of("hammer_blow"), new Hunter(), enemyBaseHealth: 50f, enemyBaseDamage: 0.01f);

        // Act
        for (var i = 0; i < 5; i++) Assert.Equal(WaveOutcome.Cleared, d.PushWave());

        // Assert — five rewards, numbered 1..5, and the visit's record advanced with them.
        for (var wave = 1; wave <= 5; wave++)
        {
            Assert.True(d.HasReward, $"wave {wave}'s reward is missing");
            var r = d.TakeReward();
            Assert.Equal(wave, r.Wave);
            Assert.True(r.Haul.Gleam > 0, "a cleared wave pays");
        }
        Assert.False(d.HasReward);
        Assert.Equal(5, d.Deepest);
        // The fifth wave is the boss (ExpeditionTuning.BossEvery) — the flag rode the reward out
        // above; re-push one wave and check the flag directly on the next boss instead of banking
        // state: wave 5 was IsBoss when taken.
    }

    [Fact]
    public void test_the_fifth_wave_reward_carries_the_boss_flag()
    {
        var d = NewDescent();
        d.StartRun(TestBuilds.Of("hammer_blow"), new Hunter(), enemyBaseHealth: 50f, enemyBaseDamage: 0.01f);
        for (var i = 0; i < 5; i++) d.PushWave();

        var boss = false;
        while (d.HasReward)
        {
            var r = d.TakeReward();
            if (r.Wave == ExpeditionTuning.Default.BossEvery) boss = r.IsBoss;
        }
        Assert.True(boss, "the boss wave's reward must say so — the host's chest roll reads the flag");
    }

    [Fact]
    public void test_a_checkpoint_start_skips_waves_and_pays_nothing_for_them()
    {
        // Arrange/Act
        var d = NewDescent();
        d.StartRun(TestBuilds.Of("hammer_blow"), new Hunter(),
                   enemyBaseHealth: 50f, enemyBaseDamage: 0.01f, startWave: 10);

        // Assert — the record already stands at the checkpoint, but no skipped wave paid out...
        Assert.Equal(10, d.Deepest);
        Assert.False(d.HasReward, "skipped waves must pay no haul");

        // ...and the first push resolves wave 11.
        Assert.Equal(WaveOutcome.Cleared, d.PushWave());
        Assert.Equal(11, d.TakeReward().Wave);
    }

    [Fact]
    public void test_reset_drops_the_visit_but_never_the_run_index()
    {
        // Arrange — a run with a banked, untaken reward and a record.
        var d = NewDescent();
        d.StartRun(TestBuilds.Of("hammer_blow"), new Hunter(), enemyBaseHealth: 50f, enemyBaseDamage: 0.01f);
        d.PushWave();

        // Act — travel.
        d.Reset();

        // Assert — the visit is gone; the index is not, so a replayed composition can never collide
        // with one from before the trip.
        Assert.Null(d.Run);
        Assert.Null(d.Champion);
        Assert.Equal(0, d.Deepest);
        Assert.False(d.HasReward);
        d.StartRun(TestBuilds.Of("hammer_blow"), new Hunter(), enemyBaseHealth: 50f, enemyBaseDamage: 0.01f);
        Assert.Equal(2, d.RunIndex);
    }

    [Fact]
    public void test_pushing_with_no_run_refuses_loudly()
    {
        Assert.Throws<InvalidOperationException>(() => NewDescent().PushWave());
    }
}
