using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Progression;
using Xunit;

namespace IdleXIdle.Core.Tests.Automation;

/// <summary>
/// The region progress record that survived the creature-farm retirement (2026-08-24): mastery from
/// the champion's own kills, the depth record that pays skill points, and the away-earnings readout.
/// </summary>
public class RegionFarmTests
{
    private static readonly AutomationTuning Tuning = AutomationTuning.Default;
    private const int Par = 15;

    private static Region NewRegion() => new("verdant_hollow", Par, Tuning);

    [Fact]
    public void test_region_farm_active_kills_accrue_mastery_points()
    {
        // Arrange
        var region = NewRegion();

        // Act
        for (var i = 0; i < 20; i++) region.RecordActiveKill();

        // Assert — Formula 1's surviving half: RmpPerActiveKillBonus per kill, nothing else.
        Assert.Equal(20 * Tuning.RmpPerActiveKillBonus, region.RegionMasteryPoints, precision: 3);
    }

    [Fact]
    public void test_region_farm_mastery_levels_climb_in_order()
    {
        // Arrange
        var region = NewRegion();
        Assert.Equal(MasteryLevel.NewlyConquered, region.MasteryLevel);

        // Act + Assert — cross each threshold with kills, in order.
        while (region.RegionMasteryPoints < Tuning.RmpThreshold1) region.RecordActiveKill();
        Assert.Equal(MasteryLevel.PartiallyMastered, region.MasteryLevel);

        while (region.RegionMasteryPoints < Tuning.RmpThreshold2) region.RecordActiveKill();
        Assert.Equal(MasteryLevel.FullyMastered, region.MasteryLevel);

        while (region.RegionMasteryPoints < Tuning.RmpThreshold3) region.RecordActiveKill();
        Assert.Equal(MasteryLevel.Perfected, region.MasteryLevel);
    }

    /// <summary>
    /// PERFECTED is the ladder's top (P7): the third mastery goal, earned by fighting. It was
    /// unreachable while it was gated on the retired creature-team subsystem — and the trait
    /// budget's third point per region silently went with it.
    /// </summary>
    [Fact]
    public void test_region_farm_mastery_tops_at_perfected()
    {
        // Arrange
        var region = NewRegion();

        // Act — a mastery total far beyond every threshold.
        region.RestoreMasteryPoints(1_000_000_000f);

        // Assert
        Assert.Equal(MasteryLevel.Perfected, region.MasteryLevel);
    }

    /// <summary>The map's away-earnings line: always the band FLOOR for the current mastery level.</summary>
    [Fact]
    public void test_region_farm_idle_efficiency_reads_the_band_floor()
    {
        // Arrange
        var region = NewRegion();

        // Act + Assert — at each mastery level, the readout is that band's minimum (team quality is
        // pinned to zero — exactly what an unstaffable farm always scored).
        Assert.Equal(EfficiencyContract.IdleBand(MasteryLevel.NewlyConquered).Min, region.IdleEfficiencyPercent());

        region.RestoreMasteryPoints(Tuning.RmpThreshold1);
        Assert.Equal(EfficiencyContract.IdleBand(MasteryLevel.PartiallyMastered).Min, region.IdleEfficiencyPercent());

        region.RestoreMasteryPoints(Tuning.RmpThreshold2);
        Assert.Equal(EfficiencyContract.IdleBand(MasteryLevel.FullyMastered).Min, region.IdleEfficiencyPercent());

        region.RestoreMasteryPoints(Tuning.RmpThreshold3);
        Assert.Equal(EfficiencyContract.IdleBand(MasteryLevel.Perfected).Min, region.IdleEfficiencyPercent());
    }

    /// <summary>Par is the fixed anchor of the efficiency contract. Nothing here may move it.</summary>
    [Fact]
    public void test_region_farm_par_is_never_altered()
    {
        // Arrange
        var region = NewRegion();

        // Act
        for (var i = 0; i < 1000; i++) region.RecordActiveKill();
        region.RecordDepth(40);

        // Assert
        Assert.Equal(Par, region.ParClearTimeSeconds);
    }

    /// <summary>Depth records pay first-time only — the source of skill points must not re-pay.</summary>
    [Fact]
    public void test_region_farm_depth_record_pays_first_time_only()
    {
        // Arrange
        var region = NewRegion();

        // Act + Assert
        Assert.True(region.RecordDepth(10));    // a new record
        Assert.False(region.RecordDepth(10));   // the same depth again — no record
        Assert.False(region.RecordDepth(7));    // shallower — no record
        Assert.True(region.RecordDepth(11));    // deeper — a record again
        Assert.Equal(11, region.BestDepth);
    }

    /// <summary>Restores clamp garbage — a tampered or corrupt save must not go negative.</summary>
    [Fact]
    public void test_region_farm_restores_clamp_negative_values_to_zero()
    {
        // Arrange
        var region = NewRegion();

        // Act
        region.RestoreMasteryPoints(-500f);
        region.RestoreBestDepth(-3);

        // Assert
        Assert.Equal(0f, region.RegionMasteryPoints);
        Assert.Equal(0, region.BestDepth);
    }
}
