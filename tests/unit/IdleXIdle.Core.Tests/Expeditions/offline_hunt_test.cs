using System;
using IdleXIdle.Core.Combat;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using Xunit;

namespace IdleXIdle.Core.Tests.Expeditions;

/// <summary>
/// Offline credit is a REAL simulation now (P5) — these pin the properties that make it safe to
/// run at every load: deterministic for a seed, bounded in CPU whatever the absence, extrapolated
/// honestly past its cap, and paying the stated haircut of live play.
/// </summary>
public class OfflineHuntTest
{
    private static OfflineHunt.Result Sim(double seconds, int seed = 11,
                                          float enemyHealth = 110f, float enemyDamage = 9f)
        => OfflineHunt.Simulate(
            TestBuilds.Of("hammer_blow"), new Hunter(), seconds,
            regionId: "", bias: AttackBias.Balanced,
            enemyBaseHealth: enemyHealth, enemyBaseDamage: enemyDamage, seed: seed);

    [Fact]
    public void test_offline_is_deterministic_for_a_seed()
    {
        // Arrange/Act — the same absence, twice.
        var a = Sim(1800, seed: 42);
        var b = Sim(1800, seed: 42);

        // Assert — byte-for-byte the same trip. A load must never roll dice twice.
        Assert.Equal(a, b);
    }

    [Fact]
    public void test_zero_seconds_earns_nothing()
    {
        var r = Sim(0);
        Assert.Equal(0, r.Gleam);
        Assert.Equal(0, r.WavesCleared);
        Assert.Equal(0.0, r.SimulatedSeconds);
    }

    [Fact]
    public void test_a_real_absence_earns_real_waves()
    {
        // Half an hour against the starter baseline: the champion fights, falls, regroups, earns.
        var r = Sim(1800);
        Assert.True(r.WavesCleared > 0, "the champion cleared nothing in half an hour");
        Assert.True(r.Gleam > 0, "cleared waves must pay");
        Assert.True(r.DeepestWave >= 1);
        // The whole budget was simulated — no extrapolation needed this far under the wave cap.
        Assert.Equal(0.0, r.ExtrapolatedSeconds);
    }

    [Fact]
    public void test_the_wave_cap_bounds_the_simulation_and_extrapolates_the_rest()
    {
        // Arrange/Act — a full 24h credit (the persistence-layer maximum).
        var r = Sim(24 * 3600);

        // Assert — CPU is bounded by the wave cap, the remainder is covered at the measured rate,
        // and the two halves add up to the whole credit.
        Assert.True(r.WavesCleared <= OfflineHunt.MaxSimulatedWaves);
        Assert.True(r.ExtrapolatedSeconds > 0, "a 24h credit should outrun the wave cap");
        Assert.Equal(24 * 3600, r.SimulatedSeconds + r.ExtrapolatedSeconds, precision: 0);

        // The credit equals rate x whole-credit x haircut (the extrapolation IS that identity).
        var expected = (long)(r.GleamPerSecond * (r.SimulatedSeconds + r.ExtrapolatedSeconds)
                              * OfflineHunt.Haircut);
        Assert.InRange(r.Gleam, expected - 1, expected + 1);
    }

    [Fact]
    public void test_twice_the_absence_earns_about_twice_the_gleam()
    {
        // Not exact — runs end mid-cycle — but an idle credit must scale with time, or the player
        // learns to log in hourly to farm the boundary.
        var half = Sim(1800);
        var full = Sim(3600);
        Assert.InRange(full.Gleam / (double)Math.Max(1, half.Gleam), 1.5, 2.5);
    }

    [Fact]
    public void test_a_champion_that_cannot_fight_earns_nothing_and_still_terminates()
    {
        // Arrange/Act — enemies that one-shot the pool: every run is a fall on wave 1.
        var r = Sim(600, enemyHealth: 1e7f, enemyDamage: 1e7f);

        // Assert — no cleared waves, no pay, and the loop spent the budget on falls rather than
        // spinning forever.
        Assert.Equal(0, r.WavesCleared);
        Assert.Equal(0, r.Gleam);
        Assert.True(r.Falls > 0);
        Assert.True(r.SimulatedSeconds > 0);
    }
}
