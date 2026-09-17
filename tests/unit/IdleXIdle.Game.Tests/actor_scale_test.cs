using IdleXIdle.Game;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE RESOLVED PRESENTATION SCALE: authored silhouette → layout box → the renderer's magnification
/// ceiling → the geometry every consumer reads. One owner (<see cref="UiKit.DrawScale"/>) answers for
/// the renderer and for the geometry alike, so a figure and the effects measured against it cannot be
/// sized by two different rules. Pure arithmetic; the content boxes are the shipped strips' real
/// margins (scratchpad cap_audit.py, 2026-09-06).
/// </summary>
public class actor_scale_test
{
    private const int Frame = 512;                     // every shipped actor strip is 8 square frames

    // The boxes the arena lays out: EnemyBox 436x440 at the archetype's scale — lone or packed, the
    // same box since 2026-09-07 (HuntScreen.CreatureRow) — and ChampBox.
    private static readonly Rectangle SwarmBox = new(1200, 500, 252, 255);      // 0.58
    private static readonly Rectangle BruiserBox = new(1200, 400, 488, 492);    // 1.12
    private static readonly Rectangle ChampBox = new(420, 451, 400, 430);

    // A 126 PX SKY (the retired rift guardian's; the Bruisers that cap now have 120), an IDLE is letterboxed deeply enough (126 px of sky) that the
    // 488 x 492 box asks 1.275 and the renderer answers 1.25. That box is every Bruiser wave now — a
    // Bruiser rolls one creature, and a lone creature wears its archetype's scale like a pack does.
    private const float GuardianTop = 126 / (float)Frame;
    private static readonly ContentBox GuardianIdleDrawn = new(151 / (float)Frame, GuardianTop, 151 / (float)Frame, 17 / (float)Frame);
    private static readonly ContentBox GuardianAttack = new(65 / (float)Frame, 117 / (float)Frame, 66 / (float)Frame, 17 / (float)Frame);

    // THE STONE SENTINEL at the same box: 1.246, just under the ceiling — the near-miss case.
    private const float SentinelTop = 117 / (float)Frame;
    private static readonly ContentBox SentinelIdleDrawn = new(88 / (float)Frame, SentinelTop, 88 / (float)Frame, 17 / (float)Frame);

    // THE SEEKER's idle, in the champion box: 1.080, far under.
    private const float SeekerTop = 114 / (float)Frame;
    private static readonly ContentBox SeekerIdleDrawn = new(132 / (float)Frame, SeekerTop, 132 / (float)Frame, 17 / (float)Frame);
    private static readonly ContentBox SeekerAttack = new(31 / (float)Frame, 117 / (float)Frame, 31 / (float)Frame, 17 / (float)Frame);

    private static float Ceiling => UiKit.RasterCeiling * Frame;

    /// <summary>One whole frame at the size the RENDERER resolves for this clip in this box.</summary>
    /// <remarks>
    /// Through <see cref="UiKit.DrawScale"/> on purpose: the tests must agree with the renderer by
    /// consuming it, not by restating its arithmetic. The crop is the clip's own headroom, which is
    /// what the arena passes for every actor.
    /// </remarks>
    private static float Unit(Rectangle box, ContentBox c) => UiKit.DrawScale(512, c.Top, box.Height) * 512;

    /// <summary>The clips of one figure in one box, each carrying the unit it is drawn at.</summary>
    private static ResolvedClip[] Clips(Rectangle box, params ContentBox[] cs)
    {
        var r = new ResolvedClip[cs.Length];
        for (var i = 0; i < cs.Length; i++) r[i] = new ResolvedClip(cs[i], Unit(box, cs[i]));
        return r;
    }

    // ── The ceiling has one owner ────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_geometry_resolves_to_the_scale_the_renderer_draws_at()
    {
        // THE ANTI-DRIFT TEST. The geometry lands a frame at `unit` box-pixels; the renderer draws it
        // at `DrawScale` times its frame size. If those two ever disagree, an effect is sized against
        // a figure that is not on screen — which is the defect this pass exists to close. Checked
        // across every arena box and every letterboxing depth the shipped art has, capped and not.
        foreach (var box in new[] { SwarmBox, BruiserBox, ChampBox, new Rectangle(0, 0, 540, 540) })
            foreach (var top in new[] { 0f, 35 / (float)Frame, 71 / (float)Frame, SeekerTop, SentinelTop, GuardianTop, 151 / (float)Frame })
            {
                // Arrange: a full-frame silhouette, so the rectangle's height IS the resolved unit.
                var clip = new ContentBox(0f, top, 0f, 0f);

                // Act
                var geometry = VfxFigure.Land(box, clip, Unit(box, clip)).Height;
                var renderer = UiKit.DrawScale(Frame, top, box.Height) * Frame * (1f - top);

                // Assert
                Assert.Equal(renderer, geometry, 0.5);
            }
    }

    [Fact]
    public void test_the_renderer_caps_what_a_box_asks_for()
    {
        // The owner itself: a box that asks for more than the ceiling is answered with the ceiling,
        // and one that asks for less is answered with its own ask.
        Assert.Equal(UiKit.RasterCeiling, UiKit.DrawScale(Frame, GuardianTop, BruiserBox.Height), 3);
        Assert.True(UiKit.DrawScale(Frame, SentinelTop, BruiserBox.Height) < UiKit.RasterCeiling);
        Assert.Equal(BruiserBox.Height / (float)(Frame - 117), UiKit.DrawScale(Frame, SentinelTop, BruiserBox.Height), 3);
    }

    // ── What changes, and what must not ──────────────────────────────────────────────────────────

    [Fact]
    public void test_an_actor_under_the_ceiling_is_unchanged_by_resolving()
    {
        // Every actor but one: resolving must be a no-op, or this pass would have moved geometry it
        // was not asked to move. The seeker in his box, the sentinel in a Bruiser box, a swarm.
        foreach (var (box, clip) in new[]
                 {
                     (ChampBox, SeekerIdleDrawn), (ChampBox, SeekerAttack),
                     (BruiserBox, SentinelIdleDrawn), (SwarmBox, GuardianIdleDrawn),
                 })
            Assert.Equal(VfxFigure.VisualRect(box, clip), VfxFigure.Land(box, clip, Unit(box, clip)));
    }

    [Fact]
    public void test_the_capped_actors_body_shrinks_to_the_figure_that_is_drawn()
    {
        // Arrange: a 126 px-sky idle in the 488 x 492 box.
        var authored = VfxFigure.VisualRect(BruiserBox, GuardianIdleDrawn);

        // Act
        var resolved = VfxFigure.Land(BruiserBox, GuardianIdleDrawn, Unit(BruiserBox, GuardianIdleDrawn));

        // Assert: the measured mismatch, gone — and the sole still on the box's floor.
        Assert.Equal(new Point(268, 470), new Point(authored.Width, authored.Height));
        Assert.Equal(new Point(262, 461), new Point(resolved.Width, resolved.Height));
        Assert.Equal(BruiserBox.Bottom, resolved.Bottom);
        Assert.Equal(BruiserBox.Center.X, resolved.Center.X);
    }

    [Fact]
    public void test_an_actor_relative_effect_is_sized_from_the_resolved_body()
    {
        // The whole point of the fix, at the seam where it is spent: a profile measured against the
        // subject's height resolves from the figure that is drawn, not from the one the box asked for.
        var profile = VfxProfiles.DeathCreature;
        var authored = new VisualBounds(VfxFigure.VisualRect(BruiserBox, GuardianIdleDrawn), -1);
        var resolved = new VisualBounds(VfxFigure.Land(BruiserBox, GuardianIdleDrawn, Unit(BruiserBox, GuardianIdleDrawn)), -1);

        var before = VfxResolver.Resolve(profile, authored, ContentBox.Full, Frame, Frame);
        var after = VfxResolver.Resolve(profile, resolved, ContentBox.Full, Frame, Frame);

        Assert.True(after.Content.Height < before.Content.Height, "the effect follows the drawn figure down");
        Assert.Equal(profile.RelativeScale * resolved.Rect.Height, after.Content.Height, 1.0);
    }

    // ── The envelope law, preserved ──────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_resolved_envelope_holds_every_clip_as_it_is_drawn()
    {
        // Each clip is capped on its own terms — the guardian's idle is, its attack is not — and the
        // envelope holds both at the size the renderer gives them.
        var envelope = VfxFigure.Envelope(BruiserBox, Clips(BruiserBox, GuardianIdleDrawn, GuardianAttack));

        Assert.True(envelope.Contains(VfxFigure.Land(BruiserBox, GuardianIdleDrawn, Unit(BruiserBox, GuardianIdleDrawn))));
        Assert.True(envelope.Contains(VfxFigure.Land(BruiserBox, GuardianAttack, Unit(BruiserBox, GuardianAttack))));
    }

    [Fact]
    public void test_a_hunters_envelope_is_not_clamped_to_his_layout_box()
    {
        // The previous pass's law, guarded against this one: the seeker's swing genuinely leaves the
        // 400-px box, and resolving through the ceiling must not pull it back in. Nothing about his
        // clips reaches the ceiling, so the envelope is the authored one exactly.
        var resolved = VfxFigure.Envelope(ChampBox, Clips(ChampBox, SeekerIdleDrawn, SeekerAttack));
        var authored = Rectangle.Union(VfxFigure.VisualRect(ChampBox, SeekerIdleDrawn),
                                       VfxFigure.VisualRect(ChampBox, SeekerAttack));

        Assert.Equal(authored, resolved);   // nothing of his reaches a ceiling, so nothing is pulled in
        Assert.True(resolved.Width > ChampBox.Width, "the swing leaves the box, and stays out of it");
        Assert.True(resolved.X < ChampBox.X && resolved.Right > ChampBox.Right);
    }

    // ── Motion is not jitter ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_translating_an_actor_moves_its_geometry_without_resizing_it()
    {
        // A figure that slides, bobs or recoils takes its body with it — that is the drawing being
        // honest, not jitter. What must not change is the SIZE.
        var shifted = new Rectangle(BruiserBox.X + 37, BruiserBox.Y - 11, BruiserBox.Width, BruiserBox.Height);
        var still = VfxFigure.Land(BruiserBox, GuardianIdleDrawn, Unit(BruiserBox, GuardianIdleDrawn));
        var moved = VfxFigure.Land(shifted, GuardianIdleDrawn, Unit(shifted, GuardianIdleDrawn));

        Assert.Equal(still.Size, moved.Size);
        Assert.Equal(still.X + 37, moved.X);
        Assert.Equal(still.Y - 11, moved.Y);
    }

    [Fact]
    public void test_the_resolved_scale_does_not_depend_on_which_frame_is_playing()
    {
        // No pulsing: the resolution takes the strip's measured crop and the box, and an animation
        // frame is neither. The same clip resolves to the same rectangle however many times it is
        // asked, and a clip set resolves the same whichever order it is given.
        var a = VfxFigure.Envelope(BruiserBox, Clips(BruiserBox, GuardianIdleDrawn, GuardianAttack));
        var b = VfxFigure.Envelope(BruiserBox, Clips(BruiserBox, GuardianAttack, GuardianIdleDrawn));
        var c = VfxFigure.Envelope(BruiserBox, Clips(BruiserBox, GuardianAttack, GuardianAttack, GuardianIdleDrawn));

        Assert.Equal(a, b);
        Assert.Equal(a, c);
    }
}
