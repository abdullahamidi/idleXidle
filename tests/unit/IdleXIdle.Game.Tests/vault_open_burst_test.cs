using IdleXIdle.Core.Loot;
using IdleXIdle.Game;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// The VAULT's open burst (UI polish brief §30–§32, §56–§60): a reward-band pulse whose emphasis scales
/// with rarity and whose WAIT does not; under Reduced Motion the ring (movement) is dropped, the edge's
/// fade stays, and both end on the same empty frame. The rig can hold one instant of it
/// (RH_SHOT_VAULT_POSE); these hold the start, the middle and the end.
/// </summary>
public class VaultOpenBurstTests
{
    private const float Mid = 0.5f;

    [Fact]
    public void test_the_burst_runs_for_the_reward_band_and_no_longer()
    {
        // The brief's reward band is 250–450 ms; the screen uses the one vocabulary's constant.
        Assert.Equal(UiMotion.Reward, VaultScreen.OpenBurstSeconds);
        Assert.InRange(VaultScreen.OpenBurstSeconds, 0.25f, 0.45f);
    }

    [Theory]
    [InlineData(Rarity.Common)]
    [InlineData(Rarity.Rare)]
    [InlineData(Rarity.Legendary)]
    public void test_at_the_click_the_edge_is_full_and_the_ring_is_at_the_chest(Rarity grade)
    {
        var f = VaultScreen.BurstAt(1f, grade, reduced: false);

        Assert.Equal(1f, f.Edge);
        Assert.Equal(0f, f.Ring);
        Assert.Equal(1f, f.RingAlpha);
        Assert.Equal(0f, f.Echo);
        Assert.Equal(0f, f.EchoAlpha);
    }

    [Theory]
    [InlineData(Rarity.Common)]
    [InlineData(Rarity.Epic)]
    public void test_half_way_the_edge_has_half_faded_and_the_ring_is_half_out(Rarity grade)
    {
        var f = VaultScreen.BurstAt(Mid, grade, reduced: false);

        Assert.Equal(Mid, f.Edge, 3);
        Assert.Equal(UiMotion.Smooth(Mid), f.Ring, 3);
        Assert.Equal(0.25f, f.RingAlpha, 3);
    }

    [Theory]
    [InlineData(Rarity.Common)]
    [InlineData(Rarity.Uncommon)]
    [InlineData(Rarity.Rare)]
    [InlineData(Rarity.Epic)]
    [InlineData(Rarity.Legendary)]
    public void test_at_the_end_nothing_is_left_to_draw_with_or_without_reduced_motion(Rarity grade)
    {
        var full = VaultScreen.BurstAt(0f, grade, reduced: false);
        var reduced = VaultScreen.BurstAt(0f, grade, reduced: true);

        // The same end state (LAW: Reduced collapses to an instant change with the SAME end state).
        Assert.Equal(0f, full.Edge);
        Assert.Equal(0f, full.RingAlpha);
        Assert.Equal(0f, full.EchoAlpha);
        Assert.Equal(0f, reduced.Edge);
        Assert.Equal(0f, reduced.RingAlpha);
        Assert.Equal(0f, reduced.EchoAlpha);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(Mid)]
    [InlineData(0.1f)]
    public void test_reduced_motion_keeps_the_fade_and_drops_the_ring(float pulse)
    {
        var full = VaultScreen.BurstAt(pulse, Rarity.Legendary, reduced: false);
        var reduced = VaultScreen.BurstAt(pulse, Rarity.Legendary, reduced: true);

        Assert.Equal(full.Edge, reduced.Edge);        // the simple fade survives
        Assert.Equal(0f, reduced.Ring);               // the movement does not
        Assert.Equal(0f, reduced.RingAlpha);
        Assert.Equal(0f, reduced.EchoAlpha);
    }

    [Theory]
    [InlineData(Rarity.Common)]
    [InlineData(Rarity.Uncommon)]
    [InlineData(Rarity.Rare)]
    public void test_under_epic_there_is_never_an_echo(Rarity grade)
    {
        for (var pulse = 1f; pulse >= 0f; pulse -= 0.05f)
        {
            var f = VaultScreen.BurstAt(pulse, grade, reduced: false);
            Assert.Equal(0f, f.Echo);
            Assert.Equal(0f, f.EchoAlpha);
        }
    }

    [Theory]
    [InlineData(Rarity.Epic)]
    [InlineData(Rarity.Legendary)]
    public void test_epic_and_up_add_one_echo_inside_the_same_window(Rarity grade)
    {
        // Before the echo's start: nothing. Just after it: the echo is at the chest, full strength.
        // A HAIR either side of the boundary, never ON it — `1f - EchoStart` does not round-trip back
        // to EchoStart in float, so a test that asked for the exact instant would be asking whether
        // one ulp fell left or right and would report a design fault when it fell the wrong way.
        var before = VaultScreen.BurstAt(1f - VaultScreen.EchoStart + 0.01f, grade, reduced: false);
        var at = VaultScreen.BurstAt(1f - VaultScreen.EchoStart - 0.001f, grade, reduced: false);
        var end = VaultScreen.BurstAt(0f, grade, reduced: false);

        Assert.Equal(0f, before.EchoAlpha);
        Assert.Equal(0f, at.Echo, 3);
        Assert.Equal(1f, at.EchoAlpha, 3);
        // It is gone by the window's end — an extra pulse, never a longer wait.
        Assert.Equal(0f, end.EchoAlpha, 3);
    }

    [Fact]
    public void test_the_edge_only_fades_and_the_ring_only_sweeps_out()
    {
        var lastEdge = 2f;
        var lastRing = -1f;
        for (var pulse = 1f; pulse >= 0f; pulse -= 0.05f)
        {
            var f = VaultScreen.BurstAt(pulse, Rarity.Epic, reduced: false);
            Assert.True(f.Edge < lastEdge, $"the edge brightened again at pulse {pulse}");
            Assert.True(f.Ring >= lastRing, $"the ring came back in at pulse {pulse}");
            lastEdge = f.Edge;
            lastRing = f.Ring;
        }
    }

    [Theory]
    [InlineData(Rarity.Common, "sfx_chest_open")]
    [InlineData(Rarity.Uncommon, "sfx_chest_open")]
    [InlineData(Rarity.Rare, "sfx_chest_open")]
    [InlineData(Rarity.Epic, "sfx_chest_open,sfx_chest_rare")]
    [InlineData(Rarity.Legendary, "sfx_chest_open,sfx_chest_rare")]
    public void test_the_cue_adds_the_rare_layer_from_epic_up(Rarity grade, string expected)
    {
        Assert.Equal(expected, VaultScreen.CueFor(grade));
    }
}
