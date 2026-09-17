using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using IdleXIdle.Core.Progression;
using IdleXIdle.Game;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE LIGHT IS CUT TO THE THING; THE RECTANGLE IS STILL THE RECTANGLE. <see cref="FocusShape.Resolve"/>
/// turns a step's lit rectangles into the shapes the scrim is cut around — a figure's silhouette on
/// the HUNT, a soft plate everywhere else — and nothing it does moves the rectangle a click or a card
/// is measured against.
/// </summary>
/// <remarks>
/// The resolver is driven through <see cref="IFocusActors"/> with a fake arena, so a frame, a body and
/// a mask can be posed without a device. The one device type it carries, <see cref="Texture2D"/>, is
/// stood in for by an uninitialised instance (its finaliser suppressed, so the runtime never tries to
/// free a GPU handle it does not have): the resolver only ever compares and stores the reference.
/// </remarks>
public class focus_shapes_test
{
    private readonly ITestOutputHelper _out;

    public focus_shapes_test(ITestOutputHelper output) => _out = output;

    private static Texture2D Phantom()
    {
        var t = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
        GC.SuppressFinalize(t);
        return t;
    }

    private sealed class FakeArena : IFocusActors
    {
        public readonly Dictionary<VfxSubject, SpriteFrame> Frames = new();
        public readonly Dictionary<VfxSubject, Rectangle> Bodies = new();
        public readonly Dictionary<Texture2D, Texture2D> Masks = new();
        public int LaidOutCreatureCount { get; set; }
        public bool TryDrawnFrame(VfxSubject subject, out SpriteFrame frame) => Frames.TryGetValue(subject, out frame);
        public bool TryBody(VfxSubject subject, out Rectangle body) => Bodies.TryGetValue(subject, out body);
        public Texture2D? MaskOf(Texture2D texture) => Masks.GetValueOrDefault(texture);

        /// <summary>Pose a figure with a drawn frame whose strip has a mask; returns the mask.</summary>
        public Texture2D Figure(VfxSubject subject, Rectangle dest, Rectangle src, SpriteEffects fx = SpriteEffects.None)
        {
            var strip = Phantom();
            var mask = Phantom();
            Frames[subject] = new SpriteFrame(strip, src, dest, fx);
            Masks[strip] = mask;
            return mask;
        }
    }

    private static readonly Rectangle Page = new(0, 0, 1920, 1080);
    private static readonly Rectangle Sentinel = new(0, 0, 1920, 1080);

    private static List<FocusShape> Resolve(Activity screen, TourTarget target, Rectangle[] holes, IFocusActors? arena)
    {
        var into = new List<FocusShape> { FocusShape.Ellipse(new Rectangle(1, 1, 1, 1)) };   // must be cleared first
        FocusShape.Resolve(screen, target, holes, arena, into);
        return into;
    }

    // ── THE HUNTER ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_champion_target_is_the_hunters_own_silhouette()
    {
        var arena = new FakeArena();
        var dest = new Rectangle(455, 420, 330, 440);
        var src = new Rectangle(3 * 512 + 60, 40, 392, 472);
        var mask = arena.Figure(VfxSubject.Champion, dest, src, SpriteEffects.FlipHorizontally);
        var holes = HuntScreen.Spotlights(TourTarget.Champion);

        var shapes = Resolve(Activity.Hunt, TourTarget.Champion, holes, arena);

        var only = Assert.Single(shapes);
        Assert.Equal(FocusKind.Silhouette, only.Kind);
        Assert.Same(mask, only.Mask);
        Assert.Equal(dest, only.Rect);
        Assert.Equal(src, only.Src);
        Assert.Equal(SpriteEffects.FlipHorizontally, only.Effects);
        // ...and the rectangle the click and the card use is untouched by the resolve.
        Assert.Equal(HuntScreen.Spotlights(TourTarget.Champion), holes);
    }

    [Fact]
    public void test_a_champion_with_no_frame_this_frame_falls_back_to_its_body_then_to_the_hole()
    {
        var holes = HuntScreen.Spotlights(TourTarget.Champion);

        var bodied = new FakeArena();
        bodied.Bodies[VfxSubject.Champion] = new Rectangle(500, 430, 240, 430);
        var fromBody = Assert.Single(Resolve(Activity.Hunt, TourTarget.Champion, holes, bodied));
        Assert.Equal(FocusShape.RoundedRect(new Rectangle(500, 430, 240, 430)), fromBody);

        var bare = new FakeArena();
        var fromHole = Assert.Single(Resolve(Activity.Hunt, TourTarget.Champion, holes, bare));
        Assert.Equal(FocusShape.RoundedRect(holes[0]), fromHole);
    }

    [Fact]
    public void test_a_frame_whose_strip_has_no_mask_is_not_a_silhouette()
    {
        // A texture the library never loaded has no key and no mask: the light falls back to the body
        // rather than to a silhouette of nothing.
        var arena = new FakeArena();
        arena.Frames[VfxSubject.Champion] = new SpriteFrame(Phantom(), new Rectangle(0, 0, 512, 512), new Rectangle(460, 400, 320, 460), SpriteEffects.None);
        arena.Bodies[VfxSubject.Champion] = new Rectangle(500, 430, 240, 430);

        var only = Assert.Single(Resolve(Activity.Hunt, TourTarget.Champion, HuntScreen.Spotlights(TourTarget.Champion), arena));
        Assert.Equal(FocusKind.RoundedRect, only.Kind);
        Assert.Equal(new Rectangle(500, 430, 240, 430), only.Rect);
    }

    // ── THE PACK ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_enemies_target_is_one_silhouette_per_creature_and_a_plate_for_the_header()
    {
        var arena = new FakeArena { LaidOutCreatureCount = 3 };
        var masks = new List<Texture2D>();
        for (var slot = 0; slot < 3; slot++)
            masks.Add(arena.Figure(VfxSubject.Creature(slot), new Rectangle(1100 + slot * 150, 500, 140, 360), new Rectangle(slot * 512, 20, 512, 492)));
        var holes = HuntScreen.Spotlights(TourTarget.Enemies);
        Assert.Equal(2, holes.Length);   // the creatures' band, and the header

        var shapes = Resolve(Activity.Hunt, TourTarget.Enemies, holes, arena);

        Assert.Equal(4, shapes.Count);
        for (var slot = 0; slot < 3; slot++)
        {
            Assert.Equal(FocusKind.Silhouette, shapes[slot].Kind);
            Assert.Same(masks[slot], shapes[slot].Mask);
            Assert.Equal(new Rectangle(1100 + slot * 150, 500, 140, 360), shapes[slot].Rect);
        }
        Assert.Equal(FocusShape.RoundedRect(holes[1]), shapes[3]);
        // The band the creatures stand in is NOT a plate: the figures are the light.
        Assert.DoesNotContain(shapes, s => s.Kind == FocusKind.RoundedRect && s.Rect == holes[0]);
    }

    [Fact]
    public void test_a_boss_is_the_one_creature_in_slot_zero()
    {
        var arena = new FakeArena { LaidOutCreatureCount = 1 };
        var mask = arena.Figure(VfxSubject.Creature(0), new Rectangle(1200, 300, 540, 540), new Rectangle(0, 0, 512, 512));
        var holes = HuntScreen.Spotlights(TourTarget.Enemies);

        var shapes = Resolve(Activity.Hunt, TourTarget.Enemies, holes, arena);

        Assert.Equal(2, shapes.Count);
        Assert.Equal(FocusKind.Silhouette, shapes[0].Kind);
        Assert.Same(mask, shapes[0].Mask);
        Assert.Equal(FocusShape.RoundedRect(holes[1]), shapes[1]);
    }

    [Fact]
    public void test_a_creature_that_recorded_nothing_is_skipped_while_others_stand()
    {
        // A corpse past its fade draws nothing this frame. Lighting its published body would put a
        // glowing rectangle over empty floor beside the figures that are actually there.
        var arena = new FakeArena { LaidOutCreatureCount = 3 };
        arena.Figure(VfxSubject.Creature(0), new Rectangle(1100, 500, 140, 360), new Rectangle(0, 0, 512, 512));
        arena.Bodies[VfxSubject.Creature(1)] = new Rectangle(1250, 500, 140, 360);
        arena.Figure(VfxSubject.Creature(2), new Rectangle(1400, 500, 140, 360), new Rectangle(0, 0, 512, 512));
        var holes = HuntScreen.Spotlights(TourTarget.Enemies);

        var shapes = Resolve(Activity.Hunt, TourTarget.Enemies, holes, arena);

        Assert.Equal(3, shapes.Count);
        Assert.Equal(2, shapes.Count(s => s.Kind == FocusKind.Silhouette));
        Assert.DoesNotContain(shapes, s => s.Rect == new Rectangle(1250, 500, 140, 360));
    }

    [Fact]
    public void test_creatures_with_no_frames_at_all_light_their_bodies_then_the_band()
    {
        var holes = HuntScreen.Spotlights(TourTarget.Enemies);

        var bodied = new FakeArena { LaidOutCreatureCount = 2 };
        bodied.Bodies[VfxSubject.Creature(0)] = new Rectangle(1100, 500, 140, 360);
        bodied.Bodies[VfxSubject.Creature(1)] = new Rectangle(1300, 500, 140, 360);
        var fromBodies = Resolve(Activity.Hunt, TourTarget.Enemies, holes, bodied);
        Assert.Equal(new[]
        {
            FocusShape.RoundedRect(new Rectangle(1100, 500, 140, 360)),
            FocusShape.RoundedRect(new Rectangle(1300, 500, 140, 360)),
            FocusShape.RoundedRect(holes[1]),
        }, fromBodies);

        var bare = new FakeArena { LaidOutCreatureCount = 2 };
        var fromBand = Resolve(Activity.Hunt, TourTarget.Enemies, holes, bare);
        Assert.Equal(new[] { FocusShape.RoundedRect(holes[0]), FocusShape.RoundedRect(holes[1]) }, fromBand);
    }

    // ── EVERYTHING ELSE ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_every_other_target_is_exactly_one_plate_per_hole()
    {
        // With figures recorded and the arena present, so a target that is not a figure cannot borrow one.
        var arena = new FakeArena { LaidOutCreatureCount = 2 };
        arena.Figure(VfxSubject.Champion, new Rectangle(455, 420, 330, 440), new Rectangle(0, 0, 512, 512));
        arena.Figure(VfxSubject.Creature(0), new Rectangle(1100, 500, 140, 360), new Rectangle(0, 0, 512, 512));
        arena.Figure(VfxSubject.Creature(1), new Rectangle(1300, 500, 140, 360), new Rectangle(0, 0, 512, 512));
        var holes = new[] { new Rectangle(200, 20, 420, 205), new Rectangle(630, 18, 560, 135) };

        var checkedTargets = 0;
        foreach (var target in Enum.GetValues<TourTarget>())
        {
            if (target is TourTarget.Champion or TourTarget.Enemies) continue;
            foreach (var screen in new[] { Activity.Hunt, Activity.Gear, Activity.Vault, Activity.Training, Activity.Mastery })
            {
                var shapes = Resolve(screen, target, holes, screen == Activity.Hunt ? arena : null);
                Assert.Equal(new[] { FocusShape.RoundedRect(holes[0]), FocusShape.RoundedRect(holes[1]) }, shapes);
            }
            checkedTargets++;
        }
        Assert.True(checkedTargets >= 40, $"only {checkedTargets} targets were checked — the enum lost members");
    }

    [Fact]
    public void test_off_the_hunt_an_actor_target_is_a_plate_too()
    {
        // A lesson about the pack lights the HUNT's rail tile from another screen: no arena, no silhouette.
        var tile = new Rectangle(8, 8, 164, 96);
        Assert.Equal(new[] { FocusShape.RoundedRect(tile) }, Resolve(Activity.Gear, TourTarget.Enemies, new[] { tile }, null));
        Assert.Equal(new[] { FocusShape.RoundedRect(tile) }, Resolve(Activity.Vault, TourTarget.Champion, new[] { tile }, null));
        // ...and on the HUNT with nothing laid out yet, the rectangle is still lit rather than nothing.
        Assert.Equal(new[] { FocusShape.RoundedRect(tile) }, Resolve(Activity.Hunt, TourTarget.Enemies, new[] { tile }, new FakeArena()));
    }

    [Fact]
    public void test_the_whole_canvas_sentinel_and_an_empty_answer_light_nothing()
    {
        var arena = new FakeArena { LaidOutCreatureCount = 1 };
        arena.Figure(VfxSubject.Champion, new Rectangle(455, 420, 330, 440), new Rectangle(0, 0, 512, 512));

        Assert.Empty(Resolve(Activity.Hunt, TourTarget.Champion, new[] { Sentinel }, arena));
        Assert.Empty(Resolve(Activity.Hunt, TourTarget.Enemies, new[] { new Rectangle(940, 300, 680, 500), Sentinel }, arena));
        Assert.Empty(Resolve(Activity.Gear, TourTarget.InventoryItem, new[] { Sentinel }, null));
        Assert.Empty(Resolve(Activity.Gear, TourTarget.InventoryItem, Array.Empty<Rectangle>(), null));
        Assert.True(FocusShape.IsSentinel(Sentinel));
        Assert.False(FocusShape.IsSentinel(new Rectangle(0, 0, 1899, 1080)));
    }

    [Fact]
    public void test_shapes_compare_by_value_so_an_unchanged_list_costs_no_rebuild()
    {
        // The renderer rebuilds its scrim only when the list changes; a record struct compares every
        // field, and a mask by reference — so the same pose is equal and the next frame of a strip is not.
        var strip = Phantom();
        var a = FocusShape.Actor(new SpriteFrame(strip, new Rectangle(0, 0, 512, 512), new Rectangle(455, 420, 330, 440), SpriteEffects.None), strip);
        var same = FocusShape.Actor(new SpriteFrame(strip, new Rectangle(0, 0, 512, 512), new Rectangle(455, 420, 330, 440), SpriteEffects.None), strip);
        var nextFrame = FocusShape.Actor(new SpriteFrame(strip, new Rectangle(512, 0, 512, 512), new Rectangle(455, 420, 330, 440), SpriteEffects.None), strip);
        Assert.Equal(a, same);
        Assert.NotEqual(a, nextFrame);
        Assert.NotEqual(FocusShape.RoundedRect(new Rectangle(1, 2, 3, 4)), FocusShape.Ellipse(new Rectangle(1, 2, 3, 4)));
    }

    // ── THE PRODUCTION RECTANGLES ARE THE SAME RECTANGLES ─────────────────────────────────────────

    public static IEnumerable<object[]> ProductionRects()
    {
        // LITERALS, on purpose: what the HUNT answered for each actor and panel target at each density
        // before the light changed shape, printed from this same call and pinned. A drift here is the
        // light moving off the control, which no picture of the light alone can show.
        yield return new object[] { 100, TourTarget.Champion, new[] { new Rectangle(470, 441, 460, 450) } };
        yield return new object[] { 100, TourTarget.Enemies, new[] { new Rectangle(940, 379, 680, 512), new Rectangle(620, 8, 580, 155) } };
        yield return new object[] { 100, TourTarget.HunterHud, new[] { new Rectangle(186, 4, 440, 125) } };
        yield return new object[] { 100, TourTarget.StageHeader, new[] { new Rectangle(620, 8, 580, 155) } };
        yield return new object[] { 125, TourTarget.Champion, new[] { new Rectangle(470, 404, 460, 450) } };
        yield return new object[] { 125, TourTarget.Enemies, new[] { new Rectangle(940, 342, 680, 512), new Rectangle(620, 8, 580, 183) } };
        yield return new object[] { 125, TourTarget.HunterHud, new[] { new Rectangle(186, 4, 440, 125) } };
        yield return new object[] { 125, TourTarget.StageHeader, new[] { new Rectangle(620, 8, 580, 183) } };
        yield return new object[] { 150, TourTarget.Champion, new[] { new Rectangle(470, 372, 460, 450) } };
        yield return new object[] { 150, TourTarget.Enemies, new[] { new Rectangle(940, 310, 680, 512), new Rectangle(620, 8, 580, 210) } };
        yield return new object[] { 150, TourTarget.HunterHud, new[] { new Rectangle(186, 4, 440, 125) } };
        yield return new object[] { 150, TourTarget.StageHeader, new[] { new Rectangle(620, 8, 580, 210) } };
    }

    [Theory]
    [MemberData(nameof(ProductionRects))]
    public void test_the_hunts_lit_rectangles_are_the_ones_they_were(int percent, TourTarget target, Rectangle[] expected)
    {
        UiMetrics.Apply(percent);
        try
        {
            var actual = HuntScreen.Spotlights(target);
            _out.WriteLine($"{percent,3}% {target,-12} {string.Join(" ", actual)}");
            Assert.Equal(expected, actual);
            foreach (var r in actual) Assert.True(Page.Contains(r), $"{target} at {percent}% leaves the page: {r}");
        }
        finally { UiMetrics.Apply(100); }
    }

    [Fact]
    public void test_the_clickable_control_is_still_the_light_less_its_halo()
    {
        Assert.Equal(new Rectangle(410, 310, 220, 40), Game1.ClickableOf(new Rectangle(400, 300, 240, 60)));
        Assert.Equal(new Rectangle(480, 366, 440, 430), Game1.ClickableOf(new Rectangle(470, 356, 460, 450)));
    }
}
