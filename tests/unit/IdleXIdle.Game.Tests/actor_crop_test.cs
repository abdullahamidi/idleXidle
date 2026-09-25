using System;
using IdleXIdle.Game;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE TWO PRESENTATION LIMITS, each with one owner: the CROP ceiling that bounds how much
/// transparent headroom the renderer will trim (<see cref="UiKit.CropCeiling"/>, applied by
/// <c>UiKit.ResolveCrop</c>) and the MAGNIFICATION ceiling that bounds how far it will enlarge what is
/// left (<see cref="UiKit.RasterCeiling"/>, applied by <see cref="UiKit.DrawScale"/>). The renderer and
/// the published actor geometry both resolve through them, so the drawn figure and the rectangle every
/// consumer reads cannot disagree.
/// </summary>
/// <remarks>
/// The crop ceiling is SYNTHETIC here on purpose: no shipped strip approaches it (the deepest headroom
/// in the catalogue is 151 of 512, and a sweep of all 126 actor textures across all seven arena boxes
/// finds none over the limit), so the only way to exercise the clamp is to state the boundary as
/// arithmetic. Manufacturing a production asset to reach it would be the wrong kind of proof.
/// </remarks>
public class actor_crop_test
{
    private const int Frame = 512;
    private static readonly Rectangle Box = new(1200, 400, 488, 492);

    /// <summary>
    /// A box shallow enough that the MAGNIFICATION ceiling never binds, so the CROP ceiling is the
    /// only rule under test. On an arena box a deep crop saturates RasterCeiling as well, and a test
    /// that cannot tell the two limits apart proves neither of them.
    /// </summary>
    private const int Shallow = 120;
    private static readonly Rectangle ShallowBox = new(0, 0, 512, Shallow);

    /// <summary>The unit the renderer resolves for a clip cropped by <paramref name="crop"/>.</summary>
    private static float Unit(float crop, int boxHeight = 492) => UiKit.DrawScale(Frame, crop, boxHeight) * Frame;

    // ── The crop ceiling ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0f)]
    [InlineData(0.02f)]           // the champion's static-pose crop
    [InlineData(114 / 512f)]      // the seeker's idle headroom
    [InlineData(151 / 512f)]      // the deepest headroom in the shipped catalogue
    [InlineData(0.6f)]            // the ceiling itself
    public void test_a_crop_at_or_under_the_ceiling_is_taken_as_it_is(float crop)
    {
        // Below the limit the clamp must be invisible — this is the guarantee that the cleanup moved
        // no shipped actor, since every shipped headroom is one of these.
        Assert.Equal(crop, UiKit.HoldCrop(crop), 5);
        Assert.Equal(Shallow / (float)(Frame - (int)(Frame * crop)) * Frame, Unit(crop, Shallow), 0.01);
    }

    [Theory]
    [InlineData(0.61f)]
    [InlineData(0.75f)]
    [InlineData(0.99f)]
    public void test_a_crop_past_the_ceiling_resolves_to_the_ceiling(float crop)
    {
        // A measurement claiming most of a frame is empty is a broken asset, not an instruction to
        // magnify what is left until it fills the box. The renderer trims no further than the limit.
        //
        // POSED ON THE SHALLOW BOX, and that is the whole difficulty of testing this: on an arena box
        // the MAGNIFICATION ceiling saturates a deep crop too, so both sides come out at 640 and the
        // crop ceiling could be deleted with every assertion still green. At 120 px neither 0.7 nor
        // 0.6 reaches RasterCeiling, so only the clamp can make these equal.
        var held = UiKit.HoldCrop(crop);

        Assert.Equal(UiKit.CropCeiling, held, 5);
        Assert.Equal(Unit(UiKit.CropCeiling, Shallow), Unit(held, Shallow), 0.01);
        Assert.NotEqual(Unit(crop, Shallow), Unit(held, Shallow), 0.01);
    }

    [Fact]
    public void test_only_one_clamp_survives_and_the_geometry_is_not_it()
    {
        // THE ANTI-DUPLICATION TEST. DrawScale must honour whatever crop it is handed — the clamp
        // belongs to HoldCrop alone. If a second clamp were ever re-added inside DrawScale (or inside
        // the geometry), an unheld crop and a held one would resolve alike and this would fail.
        Assert.NotEqual(UiKit.DrawScale(Frame, 0.7f, Shallow), UiKit.DrawScale(Frame, UiKit.HoldCrop(0.7f), Shallow), 4);

        // ...and the geometry lands whatever unit it is given, without a rule of its own: the same
        // content at two different units gives two different rectangles.
        var art = new ContentBox(0.1f, UiKit.CropCeiling, 0.1f, 0.05f);
        Assert.NotEqual(VfxFigure.Land(ShallowBox, art, Unit(0.7f, Shallow)),
                        VfxFigure.Land(ShallowBox, art, Unit(UiKit.HoldCrop(0.7f), Shallow)));
    }

    [Fact]
    public void test_the_ceilings_are_independent_limits()
    {
        // Two limits, two owners: a crop held at the ceiling can still ask for more magnification
        // than it may have, and the magnification ceiling then binds on top of it.
        var deep = Unit(UiKit.CropCeiling, boxHeight: 900);
        Assert.Equal(UiKit.RasterCeiling * Frame, deep, 0.01);

        // ...while a shallow box under both limits is untouched by either.
        Assert.Equal(492 / (float)(Frame - 114) * Frame, Unit(114 / 512f), 0.01);
    }

    // ── The static fallback resolves like the strip it stands in for ─────────────────────────────

    [Fact]
    public void test_a_fallback_pose_under_the_ceiling_is_landed_at_the_size_its_box_asked_for()
    {
        // Under both ceilings, resolving is a no-op: the still is landed at exactly the rectangle the
        // box asked for. That is what makes the fallback path safe to route through the same owner —
        // a creature that loses its strip is drawn at the size it always was.
        var art = new ContentBox(89 / 512f, 114 / 512f, 89 / 512f, 17 / 512f);
        var swarm = new Rectangle(1200, 500, 252, 255);

        var resolved = VfxFigure.Land(swarm, art, UiKit.DrawScale(Frame, art.Top, swarm.Height) * Frame);

        Assert.Equal(VfxFigure.VisualRect(swarm, art), resolved);   // the ask, granted
        Assert.Equal(swarm.Bottom, resolved.Bottom);
    }

    [Fact]
    public void test_a_fallback_envelope_holds_every_still_the_draw_can_show()
    {
        // The fallback draw picks its pose the way the animated one does — the attack still while the
        // creature bites, the idle still otherwise — so the envelope must union BOTH. Built from the
        // idle alone, the attack pose escapes the rectangle it is hovered by, which is the defect the
        // strip path was repaired for. Measured from the shipped shadeling stills.
        var box = new Rectangle(1200, 500, 252, 255);
        var idle = new ContentBox(94 / 512f, 114 / 512f, 109 / 512f, 17 / 512f);
        var attack = new ContentBox(106 / 512f, 111 / 512f, 67 / 512f, 17 / 512f);

        var idleOnly = VfxFigure.Envelope(box, Clips(box, idle));
        var both = VfxFigure.Envelope(box, Clips(box, idle, attack));

        Assert.True(both.Contains(VfxFigure.Land(box, attack, Unit(attack.Top, box.Height))),
                    "the attack still is inside the envelope");
        Assert.False(idleOnly.Contains(VfxFigure.Land(box, attack, Unit(attack.Top, box.Height))),
                     "the fixture must pose the escape the union exists for");
        Assert.True(both.Width > idleOnly.Width);
    }

    /// <summary>The clips of one figure in one box, each carrying the unit it is drawn at.</summary>
    private static ResolvedClip[] Clips(Rectangle box, params ContentBox[] cs)
    {
        var r = new ResolvedClip[cs.Length];
        for (var i = 0; i < cs.Length; i++) r[i] = new ResolvedClip(cs[i], Unit(cs[i].Top, box.Height));
        return r;
    }

    [Fact]
    public void test_a_fallback_pose_over_the_ceiling_is_capped_like_the_animated_path()
    {
        // The fallback used to have no magnification ceiling at all, so a tall box could enlarge a
        // still past what the strip beside it was allowed. Both now answer with the same number.
        var art = new ContentBox(0.1f, 126 / 512f, 0.1f, 17 / 512f);
        var tall = new Rectangle(0, 0, 488, 900);

        var scale = UiKit.DrawScale(Frame, art.Top, tall.Height);

        Assert.Equal(UiKit.RasterCeiling, scale, 5);
        Assert.Equal(VfxFigure.Land(tall, art, UiKit.RasterCeiling * Frame),
                     VfxFigure.Land(tall, art, scale * Frame));
    }

    [Fact]
    public void test_a_fallback_actor_publishes_the_one_pose_it_is_drawn_from()
    {
        // A still has ONE silhouette, so its body and its reach are the same rectangle — there is no
        // second clip to widen it. The published envelope must not quietly become the layout box.
        var art = new ContentBox(89 / 512f, 114 / 512f, 89 / 512f, 17 / 512f);
        var box = new Rectangle(1200, 500, 252, 255);
        var unit = UiKit.DrawScale(Frame, art.Top, box.Height) * Frame;

        var body = VfxFigure.Land(box, art, unit);
        var envelope = VfxFigure.Envelope(box, new[] { new ResolvedClip(art, unit) });

        Assert.Equal(body, envelope);
        Assert.NotEqual(box, envelope);
        Assert.True(box.Contains(envelope), "the pose is inside the box it is drawn into, not equal to it");
    }

    [Fact]
    public void test_a_fallback_actors_effects_are_sized_from_that_same_pose()
    {
        // The VFX body is the published body, whichever texture it came from: an effect on a creature
        // drawn from its still is sized against the still, not against the box the still sits in.
        var art = new ContentBox(89 / 512f, 114 / 512f, 89 / 512f, 17 / 512f);
        var box = new Rectangle(1200, 500, 252, 255);
        var body = VfxFigure.Land(box, art, UiKit.DrawScale(Frame, art.Top, box.Height) * Frame);

        var placed = VfxResolver.Resolve(VfxProfiles.DeathCreature, new VisualBounds(body, -1), ContentBox.Full, Frame, Frame);

        Assert.Equal(VfxProfiles.DeathCreature.RelativeScale * body.Height, placed.Content.Height, 1.0);
        Assert.True(placed.Content.Height < VfxProfiles.DeathCreature.RelativeScale * box.Height,
                    "sized from the figure, not from the box it stands in");
    }

    // ── Regression: the shipped catalogue is untouched ───────────────────────────────────────────

    [Theory]
    // actor, headroom, side pad, sole pad, box height, and the rectangle recorded BEFORE this pass.
    [InlineData(126, 151, 17, 492, 262, 461)]   // a 126 px sky in the Bruiser box (the retired rift guardian's), capped
    [InlineData(117, 88, 17, 492, 419, 471)]    // stone sentinel, just under the ceiling
    [InlineData(114, 93, 17, 492, 403, 471)]    // shadeling, just under
    [InlineData(114, 132, 17, 430, 268, 412)]   // the seeker in the champion box
    [InlineData(35, 49, 17, 430, 373, 415)]     // quiver, the widest idle
    [InlineData(35, 166, 17, 430, 162, 415)]    // the oathbound, the narrowest
    [InlineData(71, 25, 17, 540, 566, 519)]     // forge colossus, nearest the ceiling of the bosses
    public void test_a_shipped_actor_resolves_to_the_dimensions_it_always_did(
        int topPad, int sidePad, int solePad, int boxHeight, int expectedWidth, int expectedHeight)
    {
        // The crop cleanup must move NOTHING that ships, because no shipped headroom reaches the
        // clamp. These are the measured rectangles from before it; they are still these.
        var art = new ContentBox(sidePad / (float)Frame, topPad / (float)Frame,
                                 sidePad / (float)Frame, solePad / (float)Frame);
        var box = new Rectangle(0, 0, 512, boxHeight);

        var resolved = VfxFigure.Land(box, art, UiKit.DrawScale(Frame, art.Top, boxHeight) * Frame);

        Assert.Equal(expectedWidth, resolved.Width);
        Assert.Equal(expectedHeight, resolved.Height);
    }

    [Fact]
    public void test_an_action_placed_as_the_idle_puts_every_row_where_the_idle_does()
    {
        // Arrange: the Seeker's idle (headroom 114, sole 495) and HARD HANDS (a raised fist at 117, a stance to
        // 498), both keyed pixel-exact in the same 512 frame space (ADR-011)
        var box = new Rectangle(420, 451, 400, 430);
        const int idleTop = 114, idleSole = 17, handsTop = 117;
        var idleScale = UiKit.DrawScale(Frame, idleTop / (float)Frame, box.Height);
        float ScreenY(Rectangle dest, int cropTop, float scale, int row) => dest.Y + (row - cropTop) * scale;

        // Act: the idle placed by its own measurements, the action by its own crop but PLACED AS the idle
        var idle = UiKit.PlaceFrame(box, 300, Frame - idleTop, idleScale, idleSole);
        var hands = UiKit.PlaceFrame(box, 320, Frame - handsTop, idleScale, idleSole);

        // Assert: the feet (row 494) and the head (row 120) land on the same screen lines within a pixel
        foreach (var row in new[] { 120, 300, 494 })
            Assert.InRange(ScreenY(hands, handsTop, idleScale, row) - ScreenY(idle, idleTop, idleScale, row), -1f, 1f);
        // ...where measured on its own extremes (the old draw) the action stood 3 px lower
        var own = UiKit.PlaceFrame(box, 320, Frame - handsTop, UiKit.DrawScale(Frame, handsTop / (float)Frame, box.Height), 14);
        Assert.True(MathF.Abs(ScreenY(own, handsTop, UiKit.DrawScale(Frame, handsTop / (float)Frame, box.Height), 494)
                              - ScreenY(idle, idleTop, idleScale, 494)) >= 2f);
    }
}
