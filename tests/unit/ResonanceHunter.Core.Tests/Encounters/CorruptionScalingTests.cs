using ResonanceHunter.Core.Encounters;
using Xunit;

namespace ResonanceHunter.Core.Tests.Encounters;

public class CorruptionScalingTests
{
    /// <summary>At the base world (tier 0), corruption changes nothing — every multiplier is identity.</summary>
    [Fact]
    public void test_tier_zero_is_a_no_op()
    {
        Assert.Equal(1f, CorruptionScaling.HealthMultiplier(0));
        Assert.Equal(0, CorruptionScaling.TierBonus(0));
        Assert.Equal(1f, CorruptionScaling.RewardMultiplier(0));
    }

    /// <summary>Deeper corruption makes creatures both tankier and stronger.</summary>
    [Fact]
    public void test_higher_tiers_raise_threat()
    {
        Assert.True(CorruptionScaling.HealthMultiplier(2) > CorruptionScaling.HealthMultiplier(1));
        Assert.True(CorruptionScaling.TierBonus(2) > CorruptionScaling.TierBonus(1));
    }

    /// <summary>Threat and reward rise together — deepening is worth it, not just harder.</summary>
    [Fact]
    public void test_reward_scales_with_the_tier()
    {
        Assert.Equal(2f, CorruptionScaling.RewardMultiplier(1)); // ×2 at tier 1
        Assert.Equal(3f, CorruptionScaling.RewardMultiplier(2)); // ×3 at tier 2
    }

    /// <summary>Reaching a new tier pays a one-off Dust milestone that grows with depth.</summary>
    [Fact]
    public void test_deepening_award_grows_with_depth()
    {
        Assert.True(CorruptionScaling.DeepeningDustAward(2) > CorruptionScaling.DeepeningDustAward(1));
        Assert.True(CorruptionScaling.DeepeningDustAward(1) > 0);
    }

    /// <summary>Negative/garbage tiers never produce sub-identity scaling.</summary>
    [Fact]
    public void test_negative_tiers_are_clamped()
    {
        Assert.Equal(1f, CorruptionScaling.HealthMultiplier(-3));
        Assert.Equal(0, CorruptionScaling.TierBonus(-3));
        Assert.Equal(1f, CorruptionScaling.RewardMultiplier(-3));
    }
}
