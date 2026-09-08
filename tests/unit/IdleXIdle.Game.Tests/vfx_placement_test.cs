using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// The VFX placement contract's arithmetic (brief §61–§68, §105).
/// </summary>
/// <remarks>
/// <para>
/// <b>There was no test in this repository touching VfxPlayer, an actor rectangle or a pad fraction.</b>
/// Every effect placement was a playtest-only discovery, which is how a shield came to draw a 132-px
/// dome on a 412-px hunter and stay that way. These are the baseline that did not exist.
/// </para>
/// <para>
/// <c>VfxResolver.Resolve</c> is a pure static over four values, so none of this needs a
/// GraphicsDevice: the whole point of separating the resolver from the player is that the placement
/// can be checked as arithmetic and only the pixels need a screenshot.
/// </para>
/// </remarks>
public class VfxPlacementTests
{
    /// <summary>A figure 200 wide and 400 tall, standing with its soles on y = 500.</summary>
    private static readonly VisualBounds Figure = new(new Rectangle(100, 100, 200, 400), 1);

    /// <summary>A strip whose art fills half its frame, centred — the shape a padded effect has.</summary>
    private static readonly ContentBox HalfFilled = new(0.25f, 0.25f, 0.25f, 0.25f);

    private const int Native = 512;

    private static VfxProfile Profile(
        VfxAnchor anchor = VfxAnchor.Center, float scale = 0.5f, float offX = 0f, float offY = 0f,
        VfxFacing facing = VfxFacing.Fixed, VfxBasis basis = VfxBasis.SubjectHeight) => new(
        "test", "fx_strike", VfxSubjectKind.Champion, anchor, scale,
        Basis: basis, OffsetX: offX, OffsetY: offY, Facing: facing);

    // ── The four anchors ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_anchor_center_puts_the_content_centre_on_the_visual_centre()
    {
        var p = VfxResolver.Resolve(Profile(), Figure, HalfFilled, Native, Native);
        Assert.Equal(Figure.Rect.Center, p.Content.Center);
        Assert.Equal(Figure.Rect.Center, p.Anchor);
    }

    [Fact]
    public void test_anchor_standing_puts_the_content_bottom_on_the_sole()
    {
        // This is the heal column's whole reason for existing. Its old spawn site carried a hand-tuned
        // "ChampBox.Bottom - 156" with a comment saying "so the effect's FOOT sits on the ground line" —
        // a sentence that is true of one strip at one size. STANDING is that sentence as a rule.
        var p = VfxResolver.Resolve(Profile(VfxAnchor.Standing), Figure, HalfFilled, Native, Native);
        Assert.Equal(Figure.Rect.Bottom, p.Content.Bottom);
        Assert.Equal(Figure.Rect.Bottom, p.Anchor.Y);
    }

    [Fact]
    public void test_anchor_head_centres_the_content_on_the_crown()
    {
        // The mark sigil used to be drawn 0.45 of the way down the body — an amplify marker inside the
        // creature's ribcage rather than over its head.
        var p = VfxResolver.Resolve(Profile(VfxAnchor.Head), Figure, HalfFilled, Native, Native);
        Assert.Equal(Figure.Rect.Top, p.Content.Center.Y);
    }

    // ── The normalized offset ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_offset_x_is_measured_in_subject_widths()
    {
        // The same profile on two hunters 200 and 400 px wide must move by 0.25 of EACH of them, which
        // is what makes one authored number correct on ten silhouettes.
        var narrow = VfxResolver.Resolve(Profile(offX: 0.25f), Figure, HalfFilled, Native, Native);
        var wide = VfxResolver.Resolve(Profile(offX: 0.25f),
            new VisualBounds(new Rectangle(0, 100, 400, 400), 1), HalfFilled, Native, Native);

        Assert.Equal(Figure.Rect.Center.X + 50, narrow.Content.Center.X);
        Assert.Equal(200 + 100, wide.Content.Center.X);
    }

    [Fact]
    public void test_offset_y_is_measured_in_subject_heights()
    {
        var p = VfxResolver.Resolve(Profile(offY: -0.02f), Figure, HalfFilled, Native, Native);
        Assert.Equal(Figure.Rect.Center.Y - 8, p.Content.Center.Y);   // -0.02 x 400
    }

    [Fact]
    public void test_offset_x_follows_the_subjects_facing_only_when_the_profile_asks()
    {
        var enemy = new VisualBounds(Figure.Rect, -1);

        // FIXED means screen-right whoever the subject is.
        var fixedOff = VfxResolver.Resolve(Profile(offX: 0.25f), enemy, HalfFilled, Native, Native);
        Assert.Equal(Figure.Rect.Center.X + 50, fixedOff.Content.Center.X);

        // FORWARD means toward the opponent — the champion throws right, a creature would throw left.
        var forward = VfxResolver.Resolve(Profile(offX: 0.25f, facing: VfxFacing.Forward),
                                          enemy, HalfFilled, Native, Native);
        Assert.Equal(Figure.Rect.Center.X - 50, forward.Content.Center.X);
        var mine = VfxResolver.Resolve(Profile(offX: 0.25f, facing: VfxFacing.Forward),
                                       Figure, HalfFilled, Native, Native);
        Assert.Equal(Figure.Rect.Center.X + 50, mine.Content.Center.X);
    }

    // ── The load-bearing case: scale measures CONTENT ─────────────────────────────────────────────

    [Fact]
    public void test_relative_scale_measures_the_content_not_the_frame()
    {
        // THE DEFECT THIS CONTRACT EXISTS FOR. fx_shield's dome USED to fill 0.492 of its 512-px frame
        // (the strip was regenerated 2026-09-04 and fills 1.000 now), so "scale 2.6" drew a 270-px frame
        // holding a 132-px picture — 0.32x a 412-px champion, where the brief asks for 1.10–1.20x.
        // Sizing by the frame is sizing by the padding, whatever art is in it — which is why the case
        // is posed here with a synthetic half-filled box rather than with whatever fx_shield measures
        // today.
        var padded = VfxResolver.Resolve(Profile(), Figure, HalfFilled, Native, Native);
        var full = VfxResolver.Resolve(Profile(), Figure, ContentBox.Full, Native, Native);

        // Same visible picture...
        Assert.Equal(200, padded.Content.Height);
        Assert.Equal(200, full.Content.Height);
        // ...from a frame twice as big, because half of that frame is empty.
        Assert.Equal(400, padded.Frame.Height);
        Assert.Equal(200, full.Frame.Height);
    }

    [Fact]
    public void test_the_content_height_is_the_relative_scale_times_the_visual_height()
    {
        foreach (var scale in new[] { 0.32f, 0.55f, 1.15f })
        {
            var p = VfxResolver.Resolve(Profile(scale: scale), Figure, HalfFilled, Native, Native);
            Assert.Equal((int)(scale * Figure.Rect.Height), p.Content.Height);
        }
    }

    [Fact]
    public void test_the_width_basis_measures_the_subjects_width_instead()
    {
        // The trap ring is the only profile that does this, and it has to: a row is nine hundred pixels
        // wide and one creature tall, so its height says nothing about how big a ring under it should be.
        var row = new VisualBounds(new Rectangle(0, 0, 900, 250), -1);
        var p = VfxResolver.Resolve(Profile(scale: 1f, basis: VfxBasis.SubjectWidth),
                                    row, HalfFilled, Native, Native);
        Assert.Equal(900, p.Content.Width);
    }

    // ── The budget, which is LAW 16's enforcement ─────────────────────────────────────────────────

    [Fact]
    public void test_native_ratio_is_the_frame_height_over_the_strips_own_frame_height()
    {
        var p = VfxResolver.Resolve(Profile(), Figure, HalfFilled, Native, Native);
        Assert.Equal(400f / Native, p.NativeRatio, 3);
    }

    [Fact]
    public void test_a_strip_is_never_magnified_past_the_budget_even_when_the_design_asks()
    {
        // A strip whose art fills a quarter of its frame would need a 4x frame to reach the asked-for
        // size. That IS "scale = 3.4" wearing a new coat, so the renderer refuses: it draws as large as
        // the art honestly goes and the SHORTFALL is reported rather than hidden inside a blur.
        var quarter = new ContentBox(0.375f, 0.375f, 0.375f, 0.375f);

        var honest = VfxResolver.Resolve(Profile(), Figure, quarter, Native, Native);
        Assert.True(honest.NativeRatio > VfxBudget.Max);
        Assert.Equal(VfxBudget.Verdict.Over, VfxBudget.Of(honest.NativeRatio));

        var drawn = VfxResolver.Resolve(Profile(), Figure, quarter, Native, Native, clampToBudget: true);
        Assert.Equal(VfxBudget.Max, drawn.NativeRatio, 3);
        Assert.Equal((int)(VfxBudget.Max * Native), drawn.Frame.Height);
        Assert.True(drawn.Content.Height < honest.Content.Height);
        // ...and it is still ANCHORED correctly. A clamp may cost size; it may never cost placement.
        Assert.Equal(Figure.Rect.Center, drawn.Content.Center);
    }

    [Fact]
    public void test_a_placement_inside_the_budget_is_untouched_by_the_clamp()
    {
        var honest = VfxResolver.Resolve(Profile(), Figure, HalfFilled, Native, Native);
        var drawn = VfxResolver.Resolve(Profile(), Figure, HalfFilled, Native, Native, clampToBudget: true);
        Assert.Equal(honest, drawn);
    }

    [Fact]
    public void test_the_budget_verdicts_are_the_brief_s_own_band()
    {
        Assert.Equal(VfxBudget.Verdict.Under, VfxBudget.Of(0.74f));
        Assert.Equal(VfxBudget.Verdict.Ok, VfxBudget.Of(0.75f));
        Assert.Equal(VfxBudget.Verdict.Ok, VfxBudget.Of(1.25f));
        Assert.Equal(VfxBudget.Verdict.Over, VfxBudget.Of(1.26f));
    }

    // ── The frame keeps the strip's aspect, always ────────────────────────────────────────────────

    [Fact]
    public void test_a_non_square_strip_keeps_its_own_aspect()
    {
        // Nothing shipped today is non-square, and that is exactly the hazard: the old player inferred
        // the frame COUNT from the aspect ratio, so a regenerated asset at another shape would have been
        // sliced wrong in silence. The contract declares the count and preserves the aspect.
        var p = VfxResolver.Resolve(Profile(), Figure, ContentBox.Full, 1024, 512);
        Assert.Equal(200, p.Frame.Height);
        Assert.Equal(400, p.Frame.Width);
    }

    // ── The figure's own bounds ───────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_figures_visible_sole_lands_on_the_bottom_of_its_box()
    {
        // UiKit.AnimSprite's grounding contract, stated as arithmetic. If this is wrong every
        // Standing anchor in the game is wrong with it.
        var box = new Rectangle(420, 451, 400, 430);
        var vis = VfxFigure.VisualRect(box, new ContentBox(0.258f, 0.223f, 0.258f, 0.033f));
        Assert.Equal(box.Bottom, vis.Bottom);
        Assert.Equal(box.Center.X, vis.Center.X);
    }

    [Fact]
    public void test_a_figure_is_shorter_and_narrower_than_the_box_that_holds_it()
    {
        // THE SEEKER's idle strip, measured (tools/asset-pipeline/fx_bounds.py): 0.223 of the frame is
        // empty sky and 0.258 is empty margin each side. ChampBox is 400x430; he draws 268x412.
        var vis = VfxFigure.VisualRect(new Rectangle(420, 451, 400, 430),
                                       new ContentBox(0.258f, 0.223f, 0.258f, 0.033f));
        Assert.Equal(412, vis.Height);
        Assert.Equal(268, vis.Width);
        Assert.True(vis.Width < 400, "the box over-claims the hunter's width by a third");
    }
}
