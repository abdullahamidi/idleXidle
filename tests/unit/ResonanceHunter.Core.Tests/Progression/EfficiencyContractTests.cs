using ResonanceHunter.Core.Progression;
using Xunit;

namespace ResonanceHunter.Core.Tests.Progression;

/// <summary>
/// The IDLE efficiency contract — the farm's locked bands and its clear-time curve.
/// </summary>
/// <remarks>
/// This file once also held the ACTIVE-vs-idle contract (combat-encounter-system AC21-23), the executable
/// guard for Blocker B1. That half went with the manual-combat model — there is no "active play" in the
/// single-champion auto-battler, so EfficiencyContract.ActiveEfficiencyPercent / ActiveLootBonus were dead
/// and are removed. The idle half below is live: the Region record reads it for the map's away-earnings line.
/// </remarks>
public class EfficiencyContractTests
{
    /// <summary>Whelp Thicket, the MVP reference encounter (encounter-spawn-system Formula 4).</summary>
    private const float ParSeconds = 15f;

    /// <summary>Automation demonstrably gets faster as a region is mastered.</summary>
    [Fact]
    public void test_automation_clear_time_improves_monotonically_with_mastery()
    {
        var newlyConquered = EfficiencyContract.AutomationClearTimeSeconds(
            ParSeconds, EfficiencyContract.IdleEfficiencyPercent(MasteryLevel.NewlyConquered, 1.0f));
        var optimized = EfficiencyContract.AutomationClearTimeSeconds(
            ParSeconds, EfficiencyContract.IdleEfficiencyPercent(MasteryLevel.OptimizedTeam, 1.0f));

        // 15/0.40 = 37.5s  ->  15/1.20 = 12.5s : automation gets 3x faster.
        Assert.True(optimized < newlyConquered);
        Assert.Equal(12.5f, optimized, precision: 2);
    }

    /// <summary>The locked bands from the design brief, asserted verbatim.</summary>
    [Theory]
    [InlineData(MasteryLevel.NewlyConquered, 25f, 40f)]
    [InlineData(MasteryLevel.PartiallyMastered, 50f, 70f)]
    [InlineData(MasteryLevel.FullyMastered, 80f, 100f)]
    [InlineData(MasteryLevel.OptimizedTeam, 100f, 120f)]
    public void test_idle_efficiency_never_leaves_its_locked_band(MasteryLevel level, float min, float max)
    {
        for (var q = 0f; q <= 1.0f; q += 0.05f)
        {
            var idle = EfficiencyContract.IdleEfficiencyPercent(level, q);
            Assert.InRange(idle, min, max);
        }
    }
}
