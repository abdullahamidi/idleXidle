using IdleXIdle.Game;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// The TRAITS screen's two pieces of P3-P6 feedback, as pure functions: the camera's eased glide to a
/// road (UI polish brief SS73-SS82 "smooth road focus", SS31 transition) and the purchase pulse that runs
/// along the lit connection from the prerequisite to the node just taken (SS73-SS82 "connection lights,
/// short pulse").
/// </summary>
/// <remarks>
/// <para>
/// These are the shapes a screenshot cannot assert. The captures prove the two states are DRAWN and
/// legible (build/shots/p2_traits_glidemid_*, p2_traits_wiretravel_*, p2_traits_wireland_*); what a still
/// cannot prove is that the eased camera LANDS on exactly the framing a cut would have given, or that the
/// band on the wire crosses once, in order, and never runs off either end. That is what is asserted here,
/// at t = 0, mid, and end.
/// </para>
/// <para>
/// Both functions are static and take every input, so nothing here needs a GraphicsDevice, a UiKit or a
/// screen instance - the same rule the rest of this project's Game tests follow.
/// </para>
/// </remarks>
public class TraitsFeedbackTests
{
    private static readonly Vector2 FromPan = new(1200f, -400f);
    private static readonly Vector2 ToPan = new(-800f, 260f);
    private const float FromZoom = 0.55f;
    private const float ToZoom = 1.24f;

    // ---- The camera's glide -------------------------------------------------------------------

    [Fact]
    public void test_the_glide_begins_at_the_framing_the_player_was_looking_at()
    {
        var (pan, zoom) = TraitsScreen.GlideAt(FromPan, FromZoom, ToPan, ToZoom, 0f);

        Assert.Equal(FromPan, pan);
        Assert.Equal(FromZoom, zoom);
    }

    [Fact]
    public void test_the_glide_lands_on_the_asked_for_framing_exactly()
    {
        // THE WHOLE POINT of the ease: the end state is the cut's end state, to the bit. A lerp that
        // merely got close would leave a road framed a pixel off from the framing HOME and 1-4 promise,
        // and Reduced Motion (which jumps straight here) would then disagree with the animated path.
        var (pan, zoom) = TraitsScreen.GlideAt(FromPan, FromZoom, ToPan, ToZoom, 1f);

        Assert.Equal(ToPan, pan);
        Assert.Equal(ToZoom, zoom);
    }

    [Theory]
    [InlineData(-0.5f)]
    [InlineData(1.5f)]
    public void test_a_progress_outside_the_glide_is_one_of_its_two_ends(float t)
    {
        var (pan, zoom) = TraitsScreen.GlideAt(FromPan, FromZoom, ToPan, ToZoom, t);

        if (t < 0f) { Assert.Equal(FromPan, pan); Assert.Equal(FromZoom, zoom); }
        else { Assert.Equal(ToPan, pan); Assert.Equal(ToZoom, zoom); }
    }

    [Fact]
    public void test_the_glide_moves_pan_and_zoom_together_and_never_turns_back()
    {
        // One progress drives both axes, so the picture swoops instead of sliding and then zooming.
        // Sampled across the whole glide: every step is closer to the target than the last, on both.
        var lastPanGap = Vector2.Distance(FromPan, ToPan) + 1f;
        var lastZoomGap = MathF.Abs(FromZoom - ToZoom) + 1f;
        for (var i = 0; i <= 20; i++)
        {
            var t = i / 20f;
            var (pan, zoom) = TraitsScreen.GlideAt(FromPan, FromZoom, ToPan, ToZoom, t);
            var panGap = Vector2.Distance(pan, ToPan);
            var zoomGap = MathF.Abs(zoom - ToZoom);
            Assert.True(panGap <= lastPanGap + 1e-3f, $"pan went backwards at t={t}");
            Assert.True(zoomGap <= lastZoomGap + 1e-4f, $"zoom went backwards at t={t}");
            lastPanGap = panGap;
            lastZoomGap = zoomGap;
        }
        Assert.True(lastPanGap < 1e-3f);
        Assert.True(lastZoomGap < 1e-4f);
    }

    [Fact]
    public void test_the_glide_stays_between_the_two_framings()
    {
        // No overshoot: an eased camera that sails past the road and comes back is a camera the eye
        // has to chase, which is the thing the cut was replaced to stop.
        var minZoom = MathF.Min(FromZoom, ToZoom);
        var maxZoom = MathF.Max(FromZoom, ToZoom);
        for (var i = 0; i <= 20; i++)
        {
            var (pan, zoom) = TraitsScreen.GlideAt(FromPan, FromZoom, ToPan, ToZoom, i / 20f);
            Assert.InRange(zoom, minZoom - 1e-4f, maxZoom + 1e-4f);
            Assert.InRange(pan.X, MathF.Min(FromPan.X, ToPan.X) - 1e-3f, MathF.Max(FromPan.X, ToPan.X) + 1e-3f);
            Assert.InRange(pan.Y, MathF.Min(FromPan.Y, ToPan.Y) - 1e-3f, MathF.Max(FromPan.Y, ToPan.Y) + 1e-3f);
        }
    }

    // ---- The purchase pulse -------------------------------------------------------------------

    [Fact]
    public void test_the_pulse_starts_at_the_prerequisite_with_nothing_lit_yet()
    {
        var (head, tail, glow) = TraitsScreen.WirePulseAt(0f);

        Assert.Equal(0f, head);
        Assert.Equal(0f, tail);
        Assert.Equal(0f, glow);
    }

    [Fact]
    public void test_the_band_is_part_way_along_the_wire_in_the_middle_of_the_pulse()
    {
        var (head, tail, glow) = TraitsScreen.WirePulseAt(0.35f);

        Assert.InRange(head, 0.01f, 0.99f);
        Assert.True(tail < head, "the band's tail must trail its head");
        Assert.True(tail >= 0f, "the band must not start behind the prerequisite");
        Assert.Equal(0f, glow);   // nothing has arrived yet, so the node is not lit
    }

    [Fact]
    public void test_the_band_reaches_the_node_and_the_arrival_lights_it()
    {
        // The band crosses in the first 70% of the pulse; the rest is the landing, which is the beat
        // the node's own glow is drawn on (the p2_traits_wireland_* captures).
        var (head, _, glow) = TraitsScreen.WirePulseAt(0.70f);

        Assert.Equal(1f, head, 3);
        Assert.Equal(1f, glow, 3);
    }

    [Fact]
    public void test_the_pulse_ends_with_the_node_settled_and_nothing_still_lit()
    {
        var (head, _, glow) = TraitsScreen.WirePulseAt(1f);

        Assert.Equal(1f, head, 3);
        Assert.Equal(0f, glow, 3);   // a pulse plays once and finishes: nothing is left glowing
    }

    [Fact]
    public void test_the_band_never_runs_off_either_end_of_the_wire()
    {
        for (var i = 0; i <= 40; i++)
        {
            var s = i / 40f;
            var (head, tail, glow) = TraitsScreen.WirePulseAt(s);
            Assert.InRange(head, 0f, 1f);
            Assert.InRange(tail, 0f, 1f);
            Assert.InRange(glow, 0f, 1f);
            Assert.True(tail <= head, $"the tail passed the head at s={s}");
        }
    }

    [Fact]
    public void test_the_band_only_moves_forward()
    {
        var last = 0f;
        for (var i = 0; i <= 40; i++)
        {
            var (head, _, _) = TraitsScreen.WirePulseAt(i / 40f);
            Assert.True(head >= last - 1e-4f, "the light on the wire ran backwards");
            last = head;
        }
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(2f)]
    public void test_a_progress_outside_the_pulse_is_one_of_its_two_ends(float s)
    {
        var (head, tail, glow) = TraitsScreen.WirePulseAt(s);

        Assert.Equal(s < 0f ? 0f : 1f, head, 3);
        Assert.InRange(tail, 0f, 1f);
        Assert.Equal(0f, glow, 3);
    }

    // ---- The timings the brief allows ---------------------------------------------------------

    [Fact]
    public void test_every_timing_this_screen_animates_on_is_inside_the_briefs_bands()
    {
        // The glide runs on Transition (150-220 ms) and the purchase pulse on Reward (250-450 ms),
        // both taken from UiMotion rather than written here, so this fails the day either drifts.
        Assert.InRange(UiMotion.Transition, 0.150f, 0.220f);
        Assert.InRange(UiMotion.Reward, 0.250f, 0.450f);
        Assert.InRange(UiMotion.Fast, 0.080f, 0.120f);   // the camera buttons' hover lift
    }
}
