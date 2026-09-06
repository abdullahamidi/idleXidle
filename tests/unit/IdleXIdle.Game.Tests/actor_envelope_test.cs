using IdleXIdle.Game;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// The stable actor envelope (<see cref="VfxFigure.Envelope"/>): one rectangle per figure that every
/// combat clip's opaque silhouette lands inside, measured from the clips the figure CAN play — never
/// from the one playing — so nothing pulses, shifts or jitters when the clip changes. Pure geometry,
/// no device; the content boxes are the shipped strips' real margins (scratchpad measure_strips.py,
/// 2026-09-06), so the numbers below are the arena's.
/// </summary>
public class actor_envelope_test
{
    /// <summary>The champion's layout box at 100 % — 400 x 430, the sole on y=881.</summary>
    private static readonly Rectangle ChampBox = new(420, 451, 400, 430);

    /// <summary>A swarm creature's box — the enemy box at its archetype scale.</summary>
    private static readonly Rectangle SwarmBox = new(1200, 500, 300, 302);

    // THE SEEKER: idle x[133..378] y[114..494], attack x[31..480] y[117..494] of a 512 frame.
    private static readonly ContentBox SeekerIdle = new(133 / 512f, 114 / 512f, 133 / 512f, 17 / 512f);
    private static readonly ContentBox SeekerAttack = new(31 / 512f, 117 / 512f, 31 / 512f, 17 / 512f);
    private static readonly ContentBox SeekerCast = new(67 / 512f, 105 / 512f, 67 / 512f, 17 / 512f);

    // THE RIFT GUARDIAN: idle x[152..358], attack x[65..445] — a spear that nearly doubles the width.
    private static readonly ContentBox GuardianIdle = new(152 / 512f, 126 / 512f, 153 / 512f, 17 / 512f);
    private static readonly ContentBox GuardianAttack = new(65 / 512f, 117 / 512f, 66 / 512f, 17 / 512f);

    // THE STONE SENTINEL: an attack NARROWER than its idle (x[94..417] against x[89..421]).
    private static readonly ContentBox SentinelIdle = new(89 / 512f, 117 / 512f, 90 / 512f, 17 / 512f);
    private static readonly ContentBox SentinelAttack = new(94 / 512f, 105 / 512f, 94 / 512f, 17 / 512f);

    // THE SOUL LEECH: idle x[89..422] y[35..494], attack x[58..453] y[144..494] — the attack is both
    // the wider clip and the harder letterboxed one, which is what makes it the renderer's cap case.
    private static readonly ContentBox LeechIdle = new(89 / 512f, 35 / 512f, 89 / 512f, 17 / 512f);
    private static readonly ContentBox LeechAttack = new(58 / 512f, 144 / 512f, 58 / 512f, 17 / 512f);

    private static readonly ContentBox[] Seeker = { SeekerIdle, SeekerAttack, SeekerCast };
    private static readonly ContentBox[] Guardian = { GuardianIdle, GuardianAttack };
    private static readonly ContentBox[] Leech = { LeechIdle, LeechAttack };

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

    // ── What the envelope holds ───────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_envelope_contains_the_idle_silhouette()
    {
        var envelope = VfxFigure.Envelope(ChampBox, Clips(ChampBox, Seeker));
        Assert.True(envelope.Contains(VfxFigure.VisualRect(ChampBox, SeekerIdle)));
    }

    [Fact]
    public void test_the_envelope_contains_the_attack_silhouette_the_body_does_not()
    {
        // Arrange: the idle body, and where the swing lands.
        var body = VfxFigure.VisualRect(ChampBox, SeekerIdle);
        var swing = VfxFigure.VisualRect(ChampBox, SeekerAttack);

        // Act
        var envelope = VfxFigure.Envelope(ChampBox, Clips(ChampBox, Seeker));

        // Assert: the swing escapes the body and is held by the envelope.
        Assert.False(body.Contains(swing), "the attack should reach past the idle body — that is the whole defect");
        Assert.True(envelope.Contains(swing));
        Assert.True(envelope.Width > body.Width);
    }

    [Fact]
    public void test_a_creature_whose_attack_is_wider_than_its_idle_is_held_by_the_envelope()
    {
        var idle = VfxFigure.VisualRect(SwarmBox, GuardianIdle);
        var spear = VfxFigure.VisualRect(SwarmBox, GuardianAttack);
        var envelope = VfxFigure.Envelope(SwarmBox, Clips(SwarmBox, Guardian));

        Assert.True(spear.Width > idle.Width);
        Assert.True(envelope.Contains(idle));
        Assert.True(envelope.Contains(spear));
        Assert.Equal(spear.Width, envelope.Width);   // grows exactly to the real silhouette, not beyond
    }

    [Fact]
    public void test_a_creature_whose_attack_is_narrower_than_its_idle_keeps_its_idle_width()
    {
        var idle = VfxFigure.VisualRect(SwarmBox, SentinelIdle);
        var envelope = VfxFigure.Envelope(SwarmBox, Clips(SwarmBox, SentinelIdle, SentinelAttack));

        Assert.Equal(idle.Width, envelope.Width);
        Assert.True(envelope.Contains(VfxFigure.VisualRect(SwarmBox, SentinelAttack)));
    }

    // ── What it excludes ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_envelope_excludes_the_strips_transparent_padding()
    {
        // The seeker's widest clip (the attack) still leaves 31 px of its 512 frame empty each side,
        // and every clip leaves sky above the crown. A point 15 frame-px into that padding — landed
        // where AnimSprite lands the attack's frame — is outside the envelope; the crown's headroom is
        // outside it; and it stands on the sole. (The attack's frame is WIDER than the 400-px box:
        // the swing genuinely reaches past the layout, so the box's own edge is not the measure.)
        var envelope = VfxFigure.Envelope(ChampBox, Clips(ChampBox, Seeker));
        var unit = ChampBox.Height / (1f - SeekerAttack.Top);          // one attack frame, as drawn
        var inLeftPad = ChampBox.Center.X + (int)((15 / 512f - 0.5f) * unit);
        var inRightPad = ChampBox.Center.X + (int)((0.5f - 15 / 512f) * unit);

        Assert.True(envelope.Y > ChampBox.Y, "the headroom above the crown is not hoverable");
        Assert.Equal(ChampBox.Bottom, envelope.Bottom);
        Assert.False(envelope.Contains(new Point(inLeftPad, ChampBox.Center.Y)));
        Assert.False(envelope.Contains(new Point(inRightPad, ChampBox.Center.Y)));
        Assert.False(envelope.Contains(new Point(ChampBox.Center.X, ChampBox.Y + 2)));
        Assert.True(envelope.Contains(new Point(inLeftPad + 40, ChampBox.Center.Y)), "just inside the padding the swing is there");
    }

    [Fact]
    public void test_a_padded_creature_is_narrower_than_its_box_on_every_clip()
    {
        var envelope = VfxFigure.Envelope(SwarmBox, Clips(SwarmBox, Guardian));
        Assert.True(envelope.Width < SwarmBox.Width);
        Assert.True(envelope.X > SwarmBox.X && envelope.Right < SwarmBox.Right);
    }

    // ── The stability law ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_envelope_is_the_same_whichever_clip_is_playing()
    {
        // The envelope takes the SET of clips, so the frame on screen is not an input at all; the
        // strongest thing the arithmetic can still get wrong is to depend on the order it was told
        // them in, or on a clip being listed twice — neither may move it by a pixel.
        var idleFirst = VfxFigure.Envelope(ChampBox, Clips(ChampBox, SeekerIdle, SeekerAttack, SeekerCast));
        var attackFirst = VfxFigure.Envelope(ChampBox, Clips(ChampBox, SeekerAttack, SeekerCast, SeekerIdle));
        var attackHeld = VfxFigure.Envelope(ChampBox, Clips(ChampBox, SeekerIdle, SeekerAttack, SeekerAttack, SeekerCast));

        Assert.Equal(idleFirst, attackFirst);
        Assert.Equal(idleFirst, attackHeld);
    }

    [Fact]
    public void test_the_envelope_follows_the_box_and_nothing_else()
    {
        // The lunge moves the draw box 40 px right; the envelope moves with it, unchanged in size.
        var standing = VfxFigure.Envelope(ChampBox, Clips(ChampBox, Seeker));
        var lunged = VfxFigure.Envelope(new Rectangle(ChampBox.X + 40, ChampBox.Y, ChampBox.Width, ChampBox.Height), Clips(new Rectangle(ChampBox.X + 40, ChampBox.Y, ChampBox.Width, ChampBox.Height), Seeker));

        Assert.Equal(standing.Size, lunged.Size);
        Assert.Equal(standing.X + 40, lunged.X);
    }

    [Fact]
    public void test_no_resolved_clip_is_the_draw_box_itself()
    {
        // Missing art falls back to the layout box — what the draw itself falls back to.
        Assert.Equal(ChampBox, VfxFigure.Envelope(ChampBox, System.Array.Empty<ResolvedClip>()));
    }

    [Fact]
    public void test_a_travelling_figures_envelope_spans_its_travel_and_holds_still()
    {
        // THE LUNGE. The champion's box slides up to 40 px right on a swing; the envelope published
        // for him is the union of standing and fully lunged, so the inspector's keep-out is the same
        // rectangle at every point of the swing instead of sliding with it. The union is what makes
        // that safe: it holds the figure at both ends of the travel.
        const int lunge = 40;
        var standing = VfxFigure.Envelope(ChampBox, Clips(ChampBox, Seeker));
        var lunged = standing;
        lunged.Offset(lunge, 0);
        var published = Rectangle.Union(standing, lunged);

        Assert.True(published.Contains(standing) && published.Contains(lunged));
        Assert.Equal(standing.Width + lunge, published.Width);
        // ...and it does not move when the box does, which is the whole point.
        var mid = VfxFigure.Envelope(new Rectangle(ChampBox.X + 17, ChampBox.Y, ChampBox.Width, ChampBox.Height), Clips(new Rectangle(ChampBox.X + 17, ChampBox.Y, ChampBox.Width, ChampBox.Height), Seeker));
        Assert.True(published.Contains(mid), "a half-lunge is inside the published keep-out");
    }

    // ── The renderer's magnification ceiling ─────────────────────────────────────────────────────

    [Fact]
    public void test_a_capped_clip_reports_the_size_it_is_drawn_at_not_the_size_the_box_asked_for()
    {
        // UiKit.AnimSprite refuses to magnify past RasterCeiling (1.25). The soul leech's ATTACK is
        // letterboxed hard — 144 px of the frame is sky — so the 492-px box of a multi-creature wave
        // at Bruiser scale asks 1.34 and the renderer draws 1.25. That clip is the creature's widest,
        // so an uncapped
        // envelope would hand the pointer 34 px of frame the figure is never drawn into.
        var bruiser = new Rectangle(1200, 400, 488, 492);

        // What the box ASKED for: each clip landed at its own unclamped ask.
        var asked = Rectangle.Union(VfxFigure.VisualRect(bruiser, LeechIdle), VfxFigure.VisualRect(bruiser, LeechAttack));
        // What the renderer DRAWS: each clip at the unit UiKit resolved for it.
        var drawn = VfxFigure.Envelope(bruiser, Clips(bruiser, Leech));

        Assert.True(drawn.Width < asked.Width, "the cap binds on this box");
        Assert.Equal(495, drawn.Width);            // 396 opaque frame-px at the 1.25 ceiling
        Assert.Equal(529, asked.Width);            // what the box asked for and cannot have
        Assert.Equal(bruiser.Bottom, drawn.Bottom);   // still standing on the sole
    }

    [Fact]
    public void test_the_ceiling_does_nothing_to_a_box_that_does_not_reach_it()
    {
        // The champion box asks 512/(512-114) = 1.08 of the seeker's attack frame — far under the
        // ceiling — so resolving changes not one pixel: the envelope is the AUTHORED union exactly.
        // A cap must not become a second geometry.
        var authored = Rectangle.Union(Rectangle.Union(VfxFigure.VisualRect(ChampBox, SeekerIdle),
                                                       VfxFigure.VisualRect(ChampBox, SeekerAttack)),
                                       VfxFigure.VisualRect(ChampBox, SeekerCast));
        Assert.Equal(authored, VfxFigure.Envelope(ChampBox, Clips(ChampBox, Seeker)));
    }

    // ── What the registry publishes ──────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_published_envelope_always_holds_the_published_body()
    {
        // The two are measured on different rules — the body from the draw's symmetric crop, which
        // keeps a pixel of slack per side, the envelope from the exact silhouette — so on an actor
        // whose clips never out-reach its idle (the stone sentinel) the raw envelope lands INSIDE
        // the body. The registry unions them, so every subject it holds satisfies the contract.
        var box = new Rectangle(1200, 500, 300, 302);
        var body = VfxFigure.VisualRect(box, new ContentBox(88 / 512f, 117 / 512f, 88 / 512f, 17 / 512f));   // Content's crop
        var raw = VfxFigure.Envelope(box, Clips(box, SentinelIdle, SentinelAttack));                            // the exact union
        Assert.False(raw.Contains(body), "the fixture must pose the case the union exists for");

        var registry = new VfxBoundsRegistry();
        registry.Publish(VfxSubject.Champion, body, raw, facing: 1);

        Assert.True(registry.TryBounds(VfxSubject.Champion, out var vb));
        Assert.True(vb.Envelope.Contains(vb.Rect), "the published envelope holds the published body");
        Assert.True(vb.Envelope.Contains(raw));
    }

    [Fact]
    public void test_a_subject_published_with_its_body_as_its_envelope_reaches_exactly_its_body()
    {
        var body = new Rectangle(400, 400, 200, 300);
        var registry = new VfxBoundsRegistry();
        registry.Publish(VfxSubject.Creature(0), body, body, facing: -1);

        Assert.True(registry.TryBounds(VfxSubject.Creature(0), out var vb));
        Assert.Equal(body, vb.Envelope);
    }

    // ── VisualRect with an ASYMMETRIC content box ────────────────────────────────────────────────

    [Fact]
    public void test_a_symmetric_content_box_lands_centred_as_it_always_did()
    {
        // The draw's own crop is symmetric; the rectangle it lands is centred on the box, and its
        // width and height are the ones the placement tests have always asserted.
        var vis = VfxFigure.VisualRect(ChampBox, new ContentBox(0.258f, 0.223f, 0.258f, 0.033f));
        Assert.Equal(412, vis.Height);
        Assert.Equal(268, vis.Width);
        Assert.Equal(ChampBox.Center.X, vis.Center.X);
        Assert.Equal(ChampBox.Bottom, vis.Bottom);
    }

    [Fact]
    public void test_an_asymmetric_silhouette_lands_where_its_opaque_pixels_are()
    {
        // A figure whose art sits left of its frame's centre: 10 % empty on the left, 30 % on the
        // right, 20 % of sky, 5 % under the sole. One frame at the drawn scale is 430 / 0.8 = 537.5 px,
        // so the span runs from 0.10 to 0.70 of that, measured from the box's left edge at -0.5.
        var vis = VfxFigure.VisualRect(ChampBox, new ContentBox(0.10f, 0.20f, 0.30f, 0.05f));

        Assert.Equal(ChampBox.Center.X - 215, vis.X);        // (0.10 - 0.5) * 537.5
        Assert.Equal(ChampBox.Center.X + 107, vis.Right);    // (0.70 - 0.5) * 537.5, rounded
        Assert.Equal(ChampBox.Bottom, vis.Bottom);
        Assert.True(vis.Center.X < ChampBox.Center.X, "the span leans left with the art");
    }
}
