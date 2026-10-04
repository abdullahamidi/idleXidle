using System;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Game.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE REACH BASIC, PERFORMED (design.md section 1 / 5.27, vfx sweep Phase 1 / P1.4): the Oathbound's chain lash. The
/// body never moves; the chain (<c>prop_seeker_chain_body</c>, a shared iron material) leaves his lash hand over the lash
/// frames, is TAUT at the creature exactly on the beat with his hook at its end, and recoils home over 120 ms.
/// <see cref="ReachStrand"/> is shared: Phase 3's DRINK REACH mode is another <see cref="ReachLook"/>.
/// </summary>
public class swing_reach_test
{
    private const float ClipMs = 1000f;
    private const float ShareOfBeat = 0.55f;
    private const float MaxClipSpeed = 1.5f;
    private const float SettleCeiling = 300f;
    private const float Frame = 1000f / 60f;
    private static readonly float[] Tempos = { 2500f, SoloBattle.DefaultBeatMs, 792f };

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static ActionClipTiming Timing()
        => ActionClipTiming.Parse(File.ReadAllText(RepoFile("assets", "art", "Animations", "Roster", "oathbound_attack", "char_oathbound_attack_strip8_512.clip.json")));

    private static (float Start, ActionClipTiming Plain) Envelope(float period, int beat, float? lateLeadMs = null)
    {
        var baseSpeed = Math.Max(0.6f, ClipMs / (period * ShareOfBeat));
        var contactMs = ClipMs * 5f / 8f / baseSpeed;
        var lead = lateLeadMs ?? contactMs;
        var speed = Math.Clamp(baseSpeed * contactMs / Math.Max(1f, lead), baseSpeed, Math.Max(baseSpeed, MaxClipSpeed));
        var settle = Math.Min(SettleCeiling, Math.Max(0f, period - ClipMs / speed));
        return (beat - lead, ActionClipTiming.Plain(ClipMs / 8f / speed, settle));
    }

    private sealed class Stage : ISwingStage
    {
        public static readonly Rectangle Dest = new(420, 451, 400, 400);

        public bool TrySwingFrame(int frame, out SpriteFrame drawn, out int frameSize)
        {
            drawn = new SpriteFrame(null!, new Rectangle(frame * 512, 0, 512, 512), Dest, SpriteEffects.None);
            frameSize = 512;
            return true;
        }

        public bool TrySwingTarget(int slot, out Rectangle body)
        {
            body = new Rectangle(1200 + slot * 90, 640, 120, 160);
            return true;
        }

        public Texture2D? SwingTexture(string key) => null;

        public float SwingCasterHeight => 412f;
    }

    private static SwingPerformance Begun(float start, float exit, int beat = 5000)
    {
        var perf = new SwingPerformance();
        perf.TargetBuffer[0] = 0;
        perf.Begin(SwingRecipes.Oathbound, Timing(), start, beat, exit, 300f, 1);
        return perf;
    }

    [Fact]
    public void test_the_oathbound_swing_is_a_t1_reach_with_the_chain_and_the_hook()
    {
        var r = SwingRecipes.For("oathbound");
        Assert.Same(SwingRecipes.Oathbound, r);
        Assert.Equal("oathbound.swing", r!.Id);
        Assert.Equal(SwingKind.Reach, r.Kind);
        Assert.Equal("contact", r.AnchorMarker);
        Assert.Equal(4, Timing().Markers["contact"]);
        Assert.Equal("LashHand", r.HandSocket);
        Assert.Equal(0f, r.StepInShare);
        Assert.Equal(0f, r.TravelMs);
        Assert.Null(r.Missile);
        Assert.Empty(r.ReleaseCues);
        Assert.Equal(ActionWeight.Ordinary, r.Weight);
        Assert.InRange(r.ContactVolume, 0.34f, 0.38f);
        Assert.Equal(new[] { "sfx_oathbound_swing_hit", "sfx_fist_hit", "sfx_hit" }, r.ContactCues);
        // a spark + a small flash 0.30 at the hook
        Assert.Equal(0.30f, r.Impact.FlashSize, 3);
        Assert.Equal(1, r.Impact.Sparks);
        Assert.InRange(r.Impact.Extent, 0f, 0.35f);
        Assert.InRange(r.Impact.LifeMs, 1f, 250f);
        var look = r.Reach!;
        Assert.Equal("prop_seeker_chain_body", look.StrandKey);
        Assert.Equal("prop_oathbound_hook", look.EndKey);
        Assert.Equal("prop_oathbound_hook_edge", look.EndEdgeKey);
        Assert.Equal(120f, look.RecoilMs);
        Assert.True(File.Exists(RepoFile("assets", "art", "Props", "prop_seeker_chain_body.png")));
        Assert.True(File.Exists(RepoFile("assets", "art", "Props", "prop_oathbound_hook.png")));
        Assert.True(File.Exists(RepoFile("assets", "art", "Props", "prop_oathbound_hook_edge.png")));
    }

    [Fact]
    public void test_the_strand_leaves_on_the_first_lash_frame_and_is_taut_exactly_on_the_beat()
    {
        var t = Timing();
        const int beat = 5000;
        foreach (var (start, plain) in Tempos.Select(p => Envelope(p, beat)).Append(Envelope(SoloBattle.DefaultBeatMs, beat, lateLeadMs: 60f)))
        {
            var exit = start + plain.TotalMs;
            var perf = Begun(start, exit);
            var s = perf.Strand;
            Assert.True(s.Live);
            Assert.Equal(beat, s.BeatMs);
            // the first frame that authors LashHand is 2: the strand starts out with it
            Assert.Equal(Math.Min(beat, SwingClock.FrameStart(t, 4, start, beat, exit, 2)), s.OutFromMs, 3);
            Assert.Equal(0f, s.Reach(s.OutFromMs));
            // TAUT on the beat: all the way out, no slack — and not a moment before
            Assert.True(s.Taut(beat));
            Assert.Equal(1f, s.Reach(beat));
            Assert.Equal(0f, s.Slack(beat));
            if (beat - s.OutFromMs > 2f)
            {
                Assert.False(s.Taut(beat - 1f));
                Assert.True(s.Reach(beat - 1f) < 1f);
                Assert.True(s.Slack(beat - 1f) > 0f);
                // it flicks out: monotonic, accelerating
                var half = s.OutFromMs + (beat - s.OutFromMs) / 2f;
                Assert.InRange(s.Reach(half), 0.2f, 0.3f);
                for (var ms = s.OutFromMs; ms < beat; ms += 1f) Assert.True(s.Reach(ms + 1f) >= s.Reach(ms));
            }
            // the contact frame is on the beat (the swing clock) while the strand is taut
            Assert.Equal(4, perf.FrameAt(beat));
        }
    }

    [Fact]
    public void test_the_strand_holds_one_frame_and_recoils_over_120_ms()
    {
        var perf = Begun(4400f, 5800f);
        var s = perf.Strand;
        Assert.Equal(5000f + Frame, s.RecoilFromMs, 3);
        Assert.Equal(120f, s.HomeMs - s.RecoilFromMs, 3);
        // the frame that presents the beat (a few ms after it) still shows it straight, at the creature
        Assert.True(s.Taut(5000f + Frame * 0.5f));
        Assert.True(s.Taut(s.RecoilFromMs));
        // then the recoil: it leaves the creature at once, slack returns, home at +120
        Assert.Equal(0.25f, s.Reach(s.RecoilFromMs + 60f), 3);
        Assert.True(s.Slack(s.RecoilFromMs + 60f) > 0f);
        Assert.False(s.Taut(s.RecoilFromMs + 1f));
        Assert.Equal(0f, s.Reach(s.HomeMs));
        Assert.True(s.Drawing(s.HomeMs - 1f));
        Assert.False(s.Drawing(s.HomeMs));
        for (var ms = s.RecoilFromMs; ms < s.HomeMs; ms += 1f) Assert.True(s.Reach(ms + 1f) <= s.Reach(ms));
        // a new wave / rewind forgets it
        perf.Reset();
        Assert.False(s.Live);
        Assert.Equal(0, s.Compose(5000f));
    }

    [Fact]
    public void test_the_body_never_moves_at_any_tempo()
    {
        const int beat = 5000;
        foreach (var (start, plain) in Tempos.Select(p => Envelope(p, beat)).Append(Envelope(SoloBattle.DefaultBeatMs, beat, lateLeadMs: 60f)))
        {
            var perf = Begun(start, start + plain.TotalMs);
            Assert.Equal(0f, perf.PeakPx);
            for (var ms = start - 50f; ms < start + plain.TotalMs + 300f; ms += 2f) Assert.Equal(0f, perf.StepPx(ms));
        }
    }

    [Fact]
    public void test_the_strand_runs_from_the_lash_hand_to_the_contact_point_and_is_straight_when_taut()
    {
        var t = Timing();
        var stage = new Stage();
        var perf = Begun(4400f, 5800f);
        perf.Update(5000f, Frame / 1000f, stage);
        var s = perf.Strand;
        var frame = perf.FrameAt(5000f);
        var hand = ActorSocketMap.ToArena(new SpriteFrame(null!, new Rectangle(frame * 512, 0, 512, 512), Stage.Dest, SpriteEffects.None), 512, frame,
                                          t.Socket(frame, "LashHand")!.Value);
        Assert.Equal(hand, s.Hand);
        stage.TrySwingTarget(0, out var body);
        var contact = new Vector2(body.X + body.Width * SwingRecipes.Oathbound.ContactPoint.X, body.Y + body.Height * SwingRecipes.Oathbound.ContactPoint.Y);
        Assert.Equal(contact, s.Target);
        Assert.Equal(400f / 512f, s.PixelScale, 4);   // the hook is drawn at his own scale
        Assert.Equal(ReachStrand.Segments, s.Compose(5000f));
        Assert.Equal(hand, s.Pieces[0].From);
        Assert.Equal(contact.X, s.EndAt.X, 2);
        Assert.Equal(contact.Y, s.EndAt.Y, 2);
        // taut: every joint on the straight line
        foreach (var piece in s.Pieces.ToArray())
        {
            var u = (piece.To.X - hand.X) / (contact.X - hand.X);
            Assert.Equal(hand.Y + (contact.Y - hand.Y) * u, piece.To.Y, 2);
        }
        Assert.InRange(s.ThicknessPx, 6f, 12f);
        // before the beat it hangs: the middle sags below the line
        perf.Update(4950f, Frame / 1000f, stage);
        s.Compose(4950f);
        var mid = s.Pieces[ReachStrand.Segments / 2 - 1].To;
        var line = s.Hand.Y + (s.EndAt.Y - s.Hand.Y) * ((mid.X - s.Hand.X) / (s.EndAt.X - s.Hand.X));
        Assert.True(mid.Y > line + 0.5f, "slack hangs down");
        // nothing out before the first lash frame
        Assert.Equal(0, s.Compose(s.OutFromMs - 1f));
    }

    [Fact]
    public void test_a_strand_composed_frame_by_frame_allocates_nothing()
    {
        var t = Timing();
        var stage = new Stage();
        var perf = new SwingPerformance();
        var body = new Rectangle(1200, 640, 120, 160);
        perf.TargetBuffer[0] = 0;
        perf.Begin(SwingRecipes.Oathbound, t, 0f, 500, 900f, 300f, 1);
        for (var ms = 0f; ms < 900f; ms += Frame)
        {
            perf.Update(ms, Frame / 1000f, stage);
            perf.Strand.Compose(ms);
            perf.Compose(ms, true);
            perf.Compose(ms, false);
        }
        var sink = 0f;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var s = 0; s < 200; s++)
        {
            var start = 1000f * (s + 1);
            var beat = (int)start + 450;
            perf.TargetBuffer[0] = s % 4;
            perf.Begin(SwingRecipes.Oathbound, t, start, beat, start + 800f, 300f, 1);
            for (var f = 0; f < 45; f++)
            {
                var ms = start + f * Frame;
                if (f == 30) perf.Retime(start + 700f);
                perf.Update(ms, Frame / 1000f, stage);
                if (ms >= beat && ms - Frame < beat) perf.Contact(beat, body);
                var n = perf.Strand.Compose(ms);
                foreach (var piece in perf.Strand.Pieces) sink += piece.To.X;
                sink += n + perf.Strand.EndRotation + perf.Strand.Reach(ms) + perf.Strand.Slack(ms);
                sink += perf.Compose(ms, light: false) + perf.Compose(ms, light: true) + perf.StepPx(ms);
                sink += perf.Drawing(ms) ? 1f : 0f;
            }
        }
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sink > 0f);
    }

    [Fact]
    public void test_the_chain_is_stamped_with_the_strips_links_along_the_strand()
    {
        // the Phase 1 review: the strand was a smooth iron rod beside the strip's chunky links at rest
        var reach = SwingRecipes.Oathbound.Reach!;
        Assert.Equal("prop_seeker_chain_link", reach.LinkKey);
        Assert.True(File.Exists(RepoFile("assets", "art", "Props", "prop_seeker_chain_link.png")));
        Assert.InRange(reach.LinkStep, 0.5f, 1f);      // interlocking, never gapped
        Assert.InRange(reach.LinkHeight, 1.2f, 2.5f);  // a link is wider than the body it rides
        Assert.Null(new ReachLook().LinkKey);          // a plain strand by default (DRINK's REACH chooses its own)
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        var objects = src[src.IndexOf("private int DrawSwingObjects(", StringComparison.Ordinal)..];
        objects = objects[..objects.IndexOf("private void DrawSwing(", StringComparison.Ordinal)];
        Assert.Contains("if (look.LinkKey is not null && _ui.Assets.Get(look.LinkKey) is { } links) n += DrawStrandLinks(b, strand, look, links);", objects);
        // by arc length along the pieces, face-on and edge-on alternately, untinted
        Assert.Contains("foreach (var piece in strand.Pieces)", objects);
        Assert.Contains("var cell = new Rectangle((n & 1) * cellW, 0, cellW, links.Height);", objects);
        Assert.Contains("b.Draw(links, at, cell, Color.White, rotation, origin, scale, SpriteEffects.None, 0f);", objects);
    }

    [Fact]
    public void test_the_screen_draws_the_chain_untinted_and_the_hook_with_its_edge_light()
    {
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        var objects = src[src.IndexOf("private int DrawSwingObjects(", StringComparison.Ordinal)..];
        objects = objects[..objects.IndexOf("private void DrawSwing(", StringComparison.Ordinal)];
        Assert.Contains("if (strand.Look is not { } look || strand.Compose(_playheadMs) == 0) return n;", objects);
        Assert.Contains("if (!light && _ui.Assets.Get(look.StrandKey) is { } body)", objects);
        Assert.Contains("b.Draw(body, piece.From, null, Color.White,", objects);   // iron, untinted
        Assert.Contains("var endKey = light ? look.EndEdgeKey : look.EndKey;", objects);
        Assert.Contains("VfxBlend.Light(Color.White * look.EndEdgeBrightness)", objects);
        Assert.Contains("PresentTrace.Log(\"swing-strand\"", src);
        // the strand is shared: no Oathbound name inside the class
        var strand = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "ReachStrand.cs"));
        Assert.DoesNotContain("\"oathbound", strand);
        Assert.DoesNotContain("prop_oathbound", strand);
    }
}
