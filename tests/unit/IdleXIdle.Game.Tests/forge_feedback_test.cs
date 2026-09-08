using IdleXIdle.Game;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// The Forge's NUMBER FEEDBACK (UI polish brief §36, §53–§55): on an operation a watched value ticks
/// from where it was to where it is over <see cref="UiMotion.Transition"/> — and under Reduced Motion
/// it is simply at the end value, with no intermediate frame ever shown (§32, §102–§107).
/// </summary>
/// <remarks>
/// The screen photographs the same three phases through <c>RH_SHOT_FEEL</c> / <c>RH_SHOT_FEEL_T</c>
/// (see <c>ForgeScreen.ApplyDevFeel</c>); these assert the arithmetic behind the picture, which a
/// screenshot can show but not prove — a tick that overshot, or that ran backwards, would still look
/// plausible in one frame.
/// </remarks>
public class ForgeFeedbackTests
{
    // The two ends of a real UPGRADE on the capture fixture's hero item: LEVEL 8 becomes LEVEL 9.
    private const float Before = 8f;
    private const float After = 9f;

    [Fact]
    public void test_tick_at_the_instant_it_fires_shows_the_old_number()
    {
        UiMotion.Reduced = false;
        Assert.Equal(Before, ForgeScreen.TickShown(Before, After, 1f), 3);
    }

    [Fact]
    public void test_tick_halfway_shows_a_value_between_the_two()
    {
        UiMotion.Reduced = false;
        var mid = ForgeScreen.TickShown(Before, After, 0.5f);
        Assert.True(mid > After - 1f && mid < Before + 1f, $"halfway was {mid}");
        Assert.NotEqual(Before, mid, 3);
        Assert.NotEqual(After, mid, 3);
    }

    [Fact]
    public void test_tick_at_the_end_shows_the_new_number()
    {
        UiMotion.Reduced = false;
        Assert.Equal(After, ForgeScreen.TickShown(Before, After, 0f), 3);
    }

    /// <summary>A tick only ever moves TOWARD the new value — no overshoot, no bounce (§30–§32).</summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(0.1f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(0.9f)]
    [InlineData(1f)]
    public void test_tick_never_leaves_the_span_between_the_two_values(float phase)
    {
        UiMotion.Reduced = false;
        var v = ForgeScreen.TickShown(Before, After, phase);
        Assert.InRange(v, Before, After);
    }

    /// <summary>Falling numbers (a slip, a crush, a balance being spent) run the same way.</summary>
    [Fact]
    public void test_tick_runs_downward_for_a_value_that_fell()
    {
        UiMotion.Reduced = false;
        Assert.Equal(12_600f, ForgeScreen.TickShown(12_600f, 12_400f, 1f), 3);
        Assert.Equal(12_400f, ForgeScreen.TickShown(12_600f, 12_400f, 0f), 3);
        Assert.InRange(ForgeScreen.TickShown(12_600f, 12_400f, 0.5f), 12_400f, 12_600f);
    }

    /// <summary>The phase is monotone: every step of the clock brings the number closer to its end.</summary>
    [Fact]
    public void test_tick_is_monotone_from_the_old_number_to_the_new_one()
    {
        UiMotion.Reduced = false;
        var previous = ForgeScreen.TickShown(Before, After, 1f);
        for (var step = 9; step >= 0; step--)
        {
            var v = ForgeScreen.TickShown(Before, After, step / 10f);
            Assert.True(v >= previous, $"phase {step / 10f} went backwards: {v} after {previous}");
            previous = v;
        }
        Assert.Equal(After, previous, 3);
    }

    /// <summary>
    /// REDUCED MOTION: the SAME end state, immediately — the brief's rule that an animation collapses
    /// to an instant state change rather than to a different one.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(0.5f)]
    [InlineData(0f)]
    public void test_reduced_motion_shows_the_end_value_at_every_phase(float phase)
    {
        UiMotion.Reduced = true;
        try
        {
            Assert.Equal(After, ForgeScreen.TickShown(Before, After, phase), 3);
        }
        finally
        {
            UiMotion.Reduced = false;
        }
    }

    /// <summary>
    /// The curve is the house smoothstep, not a linear ramp — one easing for every motion in the game.
    /// </summary>
    [Fact]
    public void test_tick_uses_the_house_easing()
    {
        UiMotion.Reduced = false;
        for (var step = 0; step <= 10; step++)
        {
            var phase = step / 10f;
            Assert.Equal(After + (Before - After) * UiMotion.Smooth(phase), ForgeScreen.TickValue(Before, After, phase), 4);
        }
    }
}
