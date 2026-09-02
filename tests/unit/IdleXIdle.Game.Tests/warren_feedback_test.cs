using IdleXIdle.Game;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE WARREN'S UPGRADE FEEDBACK (UI polish brief §30–§38, §102–§107), pinned where a screenshot cannot
/// reach: the two pure curves behind it. A capture can photograph the feedback at an instant — the rig
/// poses one with RH_SHOT_UPGRADE — but it cannot prove what the figure reads at every instant, and it
/// cannot pose Reduced Motion at all (the flag is the host's, set from the accessibility setting before
/// any screen draws). So the number's journey is asserted here at t = 0, mid and end, in both motion
/// modes, and the milestone's "one extra pulse" is asserted as the shape of one envelope rather than as
/// a second pulse the draw pass would have had to arm.
/// </summary>
public class WarrenFeedbackTests
{
    // The figures are a real upgrade from the `warrenready` fixture — SENTRY BURROWS at level 12 — so a
    // number here can be checked against the capture that photographs the same moment.
    private const int OutputBefore = 213;
    private const int OutputAfter = 247;
    private const long GleamBefore = 300_000;
    private const long GleamAfter = 276_651;

    [Fact]
    public void test_a_number_leaves_the_old_value_and_arrives_at_the_new_one()
    {
        UiMotion.Reduced = false;

        // A pulse reads 1 the instant it fires and 0 when it is over, so the figure travels backwards
        // along it: it IS the old value at the start and the new one at the end.
        Assert.Equal(OutputBefore, WarrenScreen.Ticked(OutputBefore, OutputAfter, 1f));
        Assert.Equal(OutputAfter, WarrenScreen.Ticked(OutputBefore, OutputAfter, 0f));

        var middle = WarrenScreen.Ticked(OutputBefore, OutputAfter, 0.5f);
        Assert.InRange(middle, OutputBefore + 1, OutputAfter - 1);
    }

    [Fact]
    public void test_what_you_spent_ticks_down_and_never_past_what_you_hold()
    {
        UiMotion.Reduced = false;

        // §37: spending reacts where the price is. The balance falls, so every value on the way is
        // between the two — a tick that overshot would print a number the player never had.
        Assert.Equal(GleamBefore, WarrenScreen.Ticked(GleamBefore, GleamAfter, 1f));
        Assert.Equal(GleamAfter, WarrenScreen.Ticked(GleamBefore, GleamAfter, 0f));

        foreach (var p in new[] { 0.9f, 0.75f, 0.5f, 0.25f, 0.1f })
            Assert.InRange(WarrenScreen.Ticked(GleamBefore, GleamAfter, p), GleamAfter, GleamBefore);
    }

    [Fact]
    public void test_a_number_only_moves_one_way()
    {
        UiMotion.Reduced = false;

        // The curve is monotonic: a figure that wobbled on its way would read as two changes.
        var previous = WarrenScreen.Ticked(OutputBefore, OutputAfter, 1f);
        for (var step = 9; step >= 0; step--)
        {
            var value = WarrenScreen.Ticked(OutputBefore, OutputAfter, step / 10f);
            Assert.True(value >= previous, $"the figure went backwards at p={step / 10f}");
            previous = value;
        }
        Assert.Equal(OutputAfter, previous);
    }

    [Fact]
    public void test_reduced_motion_shows_the_end_state_at_every_instant()
    {
        // §32 / §102–§107: Reduced Motion drops the journey and keeps the destination. The state a
        // player ends on is identical either way — which is the whole contract a collapsed animation
        // has to keep — so there is no instant at which the old figure is on screen.
        try
        {
            UiMotion.Reduced = true;
            foreach (var p in new[] { 1f, 0.75f, 0.5f, 0.25f, 0f })
            {
                Assert.Equal(OutputAfter, WarrenScreen.Ticked(OutputBefore, OutputAfter, p));
                Assert.Equal(GleamAfter, WarrenScreen.Ticked(GleamBefore, GleamAfter, p));
            }
        }
        finally
        {
            UiMotion.Reduced = false;
        }
    }

    [Fact]
    public void test_an_upgrade_glows_once_and_a_milestone_glows_twice()
    {
        UiMotion.Reduced = false;   // the flag is a static: this test pins its own, like the other five

        // The card frame's envelope. ONE pulse is armed per event either way (a second Flash armed when
        // the first ended would be the draw pass re-arming its own animation); what a milestone gets is
        // a second hump inside that one pulse. (Under Reduced Motion the SCREEN asks for one hump
        // instead of two — the lobe count is decided at the draw site; this curve stays pure.)
        Assert.Equal(0f, WarrenScreen.Lobes(1f, 1));    // the instant it fires: nothing yet
        Assert.Equal(0f, WarrenScreen.Lobes(0f, 1));    // and nothing left when it is over
        Assert.Equal(0f, WarrenScreen.Lobes(0f, 2));

        // ONE hump, at the middle of the pulse's life.
        Assert.True(WarrenScreen.Lobes(0.5f, 1) > 0.7f);

        // TWO humps for a milestone: a peak either side of a trough in the middle.
        Assert.True(WarrenScreen.Lobes(0.75f, 2) > 0.7f);
        Assert.True(WarrenScreen.Lobes(0.25f, 2) > 0.4f);
        Assert.True(WarrenScreen.Lobes(0.5f, 2) < 0.05f);

        // And the second hump is the quieter one — a pulse decays, it does not build.
        Assert.True(WarrenScreen.Lobes(0.25f, 2) < WarrenScreen.Lobes(0.75f, 2));
    }

    [Fact]
    public void test_nothing_glows_outside_its_own_pulse()
    {
        // Every read is clamped, so a value that arrived late (or a dial set past its end) cannot leave
        // a frame lit — "nothing flashes continuously" is enforced by the curve, not by a caller.
        Assert.Equal(0f, WarrenScreen.Lobes(-1f, 1));
        Assert.Equal(0f, WarrenScreen.Lobes(2f, 1));
        Assert.Equal(0f, WarrenScreen.Lobes(0.5f, 0));

        UiMotion.Reduced = false;
        Assert.Equal(OutputBefore, WarrenScreen.Ticked(OutputBefore, OutputAfter, 2f));
        Assert.Equal(OutputAfter, WarrenScreen.Ticked(OutputBefore, OutputAfter, -1f));
    }
}
