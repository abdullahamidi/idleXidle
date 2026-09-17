using System;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using Xunit;

namespace IdleXIdle.Core.Tests.Expeditions;

/// <summary>UX V2 P1.1: the log's diagnosis names the limit; the name and the sentence come from the same thresholds.</summary>
public class run_report_limit_test
{
    private static RunReport Report(WaveOutcome outcome = WaveOutcome.Wiped, float absorbed = 0f, float targets = 3f,
                                    float creatures = 3f, float healthLost = 0f) => new()
    {
        RegionId = "verdant_hollow", Depth = 12, IsRecord = false, Outcome = outcome, WallWave = 12,
        WallArchetype = Archetype.Swarm, WallAffixes = Array.Empty<Affix>(), WallCreatures = 4,
        AbsorbedFraction = absorbed, AverageHitSize = 100f, TargetsPerActivation = targets, CreaturesPerWave = creatures,
        HealthLostPerWaveFraction = healthLost, SecondsPerWave = 10f, SampledWaves = 3,
    };

    [Fact]
    public void test_each_threshold_names_its_limit_in_the_verdicts_order()
    {
        Assert.Equal(RunLimit.Stalled, Report(outcome: WaveOutcome.Stalled, absorbed: 0.9f).Limit);
        Assert.Equal(RunLimit.Armour, Report(absorbed: 0.45f, targets: 0.5f, healthLost: 0.5f).Limit);
        Assert.Equal(RunLimit.Reach, Report(targets: 1.0f, creatures: 3.0f, healthLost: 0.5f).Limit);
        Assert.Equal(RunLimit.Sustain, Report(healthLost: 0.18f).Limit);
        Assert.Equal(RunLimit.OutScaled, Report().Limit);
    }

    [Fact]
    public void test_the_label_and_the_sentence_agree()
    {
        // The game formats under the invariant culture (Game1's constructor); the test host may not.
        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        var reach = Report(targets: 1.0f, creatures: 3.0f);
        Assert.Equal("REACH", reach.LimitLabel());
        Assert.Contains("1.0 of 3.0 creatures per cast", reach.Verdict());
        var armour = Report(absorbed: 0.6f);
        Assert.Equal("ARMOUR", armour.LimitLabel());
        Assert.StartsWith("Armour ate", armour.Verdict());
    }

    [Fact]
    public void test_reach_needs_a_real_pack_so_a_single_creature_never_reads_as_reach()
    {
        Assert.Equal(RunLimit.OutScaled, Report(targets: 0.4f, creatures: 1f).Limit);
    }
}
