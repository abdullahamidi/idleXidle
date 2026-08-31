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

    [Fact]
    public void test_the_ladder_has_a_top_and_every_function_respects_it()
    {
        // 2026-08-23: DEEPEN is bounded. Past MaxTier nothing grows — the world has a floor.
        var top = CorruptionScaling.MaxTier;
        Assert.Equal(CorruptionScaling.HealthMultiplier(top), CorruptionScaling.HealthMultiplier(top + 7), 4);
        Assert.Equal(CorruptionScaling.RewardMultiplier(top), CorruptionScaling.RewardMultiplier(top + 7), 4);
        Assert.Equal(CorruptionScaling.TierBonus(top), CorruptionScaling.TierBonus(top + 7));
        Assert.Equal(CorruptionScaling.DeepeningDustAward(top), CorruptionScaling.DeepeningDustAward(top + 7));
        Assert.True(CorruptionScaling.HealthMultiplier(top) > CorruptionScaling.HealthMultiplier(top - 1));
    }

    [Fact]
    public void test_every_tier_has_a_name_a_look_and_a_label()
    {
        for (var t = 0; t <= CorruptionScaling.MaxTier; t++)
        {
            var look = CorruptionLook.For(t);
            Assert.False(string.IsNullOrWhiteSpace(look.Name));
            Assert.False(string.IsNullOrWhiteSpace(look.Blurb));
            Assert.Contains($"{t} / {CorruptionScaling.MaxTier}", CorruptionLook.Label(t));
            Assert.Contains(look.Name, CorruptionLook.Label(t));
            if (t > 0) Assert.False(string.IsNullOrWhiteSpace(look.Epithet), "a corrupted tier names the boss");
        }
        Assert.Equal(string.Empty, CorruptionLook.For(0).Epithet);
        // Deeper is darker: the arena AND the creatures only ever lose light down the ladder; the base
        // world draws everything as authored (white).
        Assert.Equal(((byte)255, (byte)255, (byte)255), CorruptionLook.For(0).Enemy);
        for (var t = 1; t <= CorruptionScaling.MaxTier; t++)
        {
            Assert.True(CorruptionLook.For(t).Arena.G < CorruptionLook.For(t - 1).Arena.G);
            Assert.True(CorruptionLook.For(t).Enemy.G < CorruptionLook.For(t - 1).Enemy.G, "creatures lose light down the ladder too");
        }
        Assert.Equal(CorruptionLook.For(CorruptionScaling.MaxTier), CorruptionLook.For(CorruptionScaling.MaxTier + 3));
    }
}
