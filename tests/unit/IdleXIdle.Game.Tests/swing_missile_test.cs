using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Game.Presentation;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE MISSILE BASICS, PERFORMED (design.md 5.24-5.26, vfx sweep Phase 1 / P1.4): the Quiver's arrow, the Chorus's bone
/// charm and the Unbroken's stone chip. The body never moves; ONE missile per swung creature leaves the hand's socket on
/// the release frame, which lands <see cref="SwingRecipe.TravelMs"/> before the beat when the plain envelope leaves room
/// (and is CLAMPED to the clip's start, the flight shortened, when it does not), and every missile lands ON the beat.
/// One release cue, one contact cue; no strip projectile.
/// </summary>
public class swing_missile_test
{
    private const float ClipMs = 1000f;           // HuntScreen.ClipMs
    private const float ShareOfBeat = 0.55f;      // HuntScreen.ClipShareOfBeat
    private const float MaxClipSpeed = 1.5f;      // HuntScreen.MaxClipSpeed
    private const float SettleCeiling = 300f;     // HuntScreen.ClipSettleMs
    private const float Frame = 1000f / 60f;

    /// <summary>The three missile basics: champion, release frame, hand socket, travel, prop, archetype hit cue.</summary>
    public static IEnumerable<object[]> Missiles() => new[]
    {
        new object[] { "quiver", 4, "BowHand", 200f, "prop_quiver_arrow", "sfx_blade_hit" },
        new object[] { "chorus", 5, "ThrowHand", 200f, "prop_chorus_charm", "sfx_wood_hit" },
        new object[] { "unbroken", 5, "ThrowHand", 220f, "prop_unbroken_chip", "sfx_stone_hit" },
    };

    /// <summary>Three TEMPOs: slow, the default 1500, the ref_fast TEMPO's 792 ms.</summary>
    private static readonly float[] Tempos = { 2500f, SoloBattle.DefaultBeatMs, 792f };

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static ActionClipTiming Timing(string id)
        => ActionClipTiming.Parse(File.ReadAllText(RepoFile("assets", "art", "Animations", "Roster", $"{id}_attack", $"char_{id}_attack_strip8_512.clip.json")));

    private static string Source(params string[] parts) => File.ReadAllText(RepoFile(parts));

    /// <summary>The plain envelope exactly as HuntScreen commits a swing (see swing_melee_test.Envelope).</summary>
    private static (float Start, ActionClipTiming Plain) Envelope(float period, int beat, float? lateLeadMs = null)
    {
        var baseSpeed = Math.Max(0.6f, ClipMs / (period * ShareOfBeat));
        var contactMs = ClipMs * 5f / 8f / baseSpeed;
        var lead = lateLeadMs ?? contactMs;
        var speed = Math.Clamp(baseSpeed * contactMs / Math.Max(1f, lead), baseSpeed, Math.Max(baseSpeed, MaxClipSpeed));
        var settle = Math.Min(SettleCeiling, Math.Max(0f, period - ClipMs / speed));
        return (beat - lead, ActionClipTiming.Plain(ClipMs / 8f / speed, settle));
    }

    /// <summary>A screen: the champion's strip drawn at 400 px, creatures to the right, no textures (no GraphicsDevice).</summary>
    private sealed class Stage : ISwingStage
    {
        public int FrameAsks;

        public bool TrySwingFrame(int frame, out SpriteFrame drawn, out int frameSize)
        {
            FrameAsks++;
            drawn = new SpriteFrame(null!, new Rectangle(frame * 512, 0, 512, 512), new Rectangle(420, 451, 400, 400), SpriteEffects.None);
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

    /// <summary>Play one swing frame by frame from its start to well past the beat: what each update crossed.</summary>
    private static (int Releases, int Lands, float ReleasedAt, float LandedAt) Play(SwingPerformance perf, Stage stage, float start, float until)
    {
        int releases = 0, lands = 0;
        float releasedAt = float.NaN, landedAt = float.NaN;
        for (var ms = start; ms <= until; ms += Frame)
        {
            var step = perf.Update(ms, Frame / 1000f, stage);
            if (step.Released) { releases++; releasedAt = ms; }
            if (step.Landed) { lands++; landedAt = ms; }
        }
        return (releases, lands, releasedAt, landedAt);
    }

    // ── THE TABLE ────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Missiles))]
    public void test_each_missile_recipe_is_t1_and_throws_its_own_prop(string id, int release, string socket, float travel, string prop, string archetype)
    {
        var r = SwingRecipes.For(id);
        Assert.NotNull(r);
        Assert.Equal($"{id}.swing", r!.Id);
        Assert.Equal(SwingKind.Missile, r.Kind);
        Assert.Equal("release", r.AnchorMarker);
        Assert.Equal(socket, r.HandSocket);
        Assert.Equal(travel, r.TravelMs);
        Assert.Equal(0f, r.StepInShare);   // the body never moves
        var t = Timing(id);
        Assert.Equal(release, t.Markers["release"]);
        Assert.NotNull(t.Socket(release, socket));
        // T1 Ordinary: the hit cue 0.34-0.38, the release 0.16-0.18, the flash 0.30 / 110 with no rise, the picture small
        Assert.Equal(ActionWeight.Ordinary, r.Weight);
        Assert.InRange(r.ContactVolume, 0.34f, 0.38f);
        Assert.InRange(r.ReleaseVolume, 0.16f, 0.18f);
        Assert.Equal(0.30f, r.TargetFlash, 3);
        Assert.Equal(110f, r.TargetFlashMs, 3);
        Assert.Equal(0f, r.TargetFlashRise);
        Assert.InRange(r.Impact.Extent, 0f, 0.35f);
        Assert.InRange(r.Impact.LifeMs, 0f, 250f);
        Assert.InRange(r.Impact.Brightness, 0.3f, 0.9f);
        // the chains: the champion's own cue, its archetype, the generic; the release to the throw archetype
        Assert.Equal(new[] { $"sfx_{id}_swing_hit", archetype, "sfx_hit" }, r.ContactCues);
        Assert.Equal("sfx_throw_release", r.ReleaseCues[^1]);
        Assert.StartsWith($"sfx_{id}_", r.ReleaseCues[0]);
        // the composite: the champion's prop as the head (an untinted material + its emissive edge), a trail, a glint
        var look = r.Missile;
        Assert.NotNull(look);
        Assert.Equal(prop, look!.MaterialKey);
        Assert.Equal(prop + "_edge", look.HeadKey);
        Assert.NotNull(look.HeadContent);
        Assert.Equal("fxp_trail_soft", look.TrailKey);
        Assert.Equal("fxp_glint_star", look.GlintKey);
        Assert.InRange(look.EdgeBrightness, 0.1f, 0.4f);   // quiet: a basic attack's object, never a lit stick
        Assert.Equal(0, look.ImpactShards);                // the debris is the swing's contact picture, at the creature
        Assert.Null(r.Reach);
    }

    [Fact]
    public void test_each_missile_has_the_contact_design_md_names()
    {
        // the Quiver: an arrow contact, a short FORWARD slash along the arrow's own line
        Assert.True(ProjectileLooks.QuiverArrow.ImpactSlash > 0f);
        Assert.InRange(ProjectileLooks.QuiverArrow.WobbleDegrees, 0f, 2f);   // an arrow flies true
        // the Chorus: a small flash + 3 pale slivers (untinted: a bone light, never a Source)
        Assert.True(ProjectileLooks.ChorusCharm.FlashSize > 0f);
        Assert.Equal(3, SwingRecipes.Chorus.Impact.Slivers);
        Assert.Equal(0f, ProjectileLooks.ChorusCharm.ImpactSlash);
        Assert.True(ProjectileLooks.ChorusCharm.EdgeBrightness <= 0.25f, "the charm is bone in the air, not glowing baked art");
        // the Unbroken: a small flash + 2 untinted chips of the same stone, on a short heavy LOB (bowed up)
        Assert.True(ProjectileLooks.UnbrokenChip.FlashSize > 0f);
        Assert.Equal(2, SwingRecipes.Unbroken.Impact.Chips);
        Assert.Equal("prop_unbroken_chip", SwingRecipes.Unbroken.Impact.ChipKey);
        Assert.True(SwingRecipes.Unbroken.FlightBulge < 0f, "a lob bows up");
        Assert.True(SwingRecipes.Unbroken.TravelMs > SwingRecipes.Quiver.TravelMs, "a heavier, slower throw");
        // the missile looks are NOT strip-keyed: no strip draws as them, and the strip table still holds only SPRAY's knife
        Assert.Single(ProjectileLooks.All);
        Assert.Same(ProjectileLooks.SeekerKnife, ProjectileLooks.For("fx_seeker_projectile_strip8_512"));
    }

    [Fact]
    public void test_the_prop_files_exist_with_their_edge_masks()
    {
        foreach (var prop in new[] { "prop_quiver_arrow", "prop_chorus_charm", "prop_unbroken_chip", "prop_oathbound_hook" })
        {
            Assert.True(File.Exists(RepoFile("assets", "art", "Props", prop + ".png")), prop);
            Assert.True(File.Exists(RepoFile("assets", "art", "Props", prop + "_edge.png")), prop + "_edge");
            var script = prop.Replace("prop_", "") + ".py";
            Assert.True(File.Exists(RepoFile("tools", "asset-pipeline", "v2", script)), script);
        }
        // the charm's one PixelLab generation is recorded in its source block
        Assert.Contains("befc30a0-7b75-4953-9ef6-7d03dcfe2bc2", Source("tools", "asset-pipeline", "v2", "chorus_charm.py"));
        // and each look's content box lies inside its square canvas (the scripts print and --check it)
        foreach (var look in new[] { ProjectileLooks.QuiverArrow, ProjectileLooks.ChorusCharm, ProjectileLooks.UnbrokenChip })
        {
            var c = look.HeadContent!.Value;
            Assert.True(c.X > 0 && c.Y > 0 && c.Width > c.Height && c.Height > 0, look.MaterialKey);
        }
    }

    // ── THE CLOCK ────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Missiles))]
    public void test_the_release_is_exactly_travel_before_the_beat_when_there_is_room_and_clamped_otherwise(string id, int release, string socket, float travel, string prop, string archetype)
    {
        _ = socket; _ = prop; _ = archetype;
        var r = SwingRecipes.For(id)!;
        var t = Timing(id);
        const int beat = 5000;
        var cases = Tempos.Select(p => Envelope(p, beat)).Append(Envelope(SoloBattle.DefaultBeatMs, beat, lateLeadMs: 60f)).ToList();
        var clamped = 0;
        foreach (var (start, plain) in cases)
        {
            var perf = new SwingPerformance();
            var exit = start + plain.TotalMs;
            perf.Begin(r, t, start, beat, exit, 300f, 1);
            if (beat - travel >= start)
            {
                Assert.Equal(beat - travel, perf.ReleaseMs);
                Assert.Equal(travel, perf.FlightMs);
                Assert.False(perf.ReleaseClamped);
            }
            else
            {
                // A LATE OR FAST START: the release clamps to the clip's start and the flight is shorter; the contact is
                // still on the beat
                clamped++;
                Assert.Equal(start, perf.ReleaseMs);
                Assert.Equal(beat - start, perf.FlightMs, 3);
                Assert.True(perf.ReleaseClamped);
            }
            // the release frame shows from the release on; the clip still fills the plain envelope exactly
            Assert.Equal(release, perf.FrameAt(perf.ReleaseMs + 0.5f));
            Assert.True(perf.FrameAt(perf.ReleaseMs - 0.5f) < release || perf.ReleaseClamped, "the release frame is not shown early");
            Assert.Equal(exit, SwingClock.FrameStart(t, release, start, beat - travel, exit, t.Frames), 2);
            // and the body never moves
            for (var ms = start; ms < exit; ms += 5f) Assert.Equal(0f, perf.StepPx(ms));
        }
        Assert.True(clamped >= 1, "the late start is a clamp case");
    }

    [Theory]
    [MemberData(nameof(Missiles))]
    public void test_one_flight_per_swung_target_leaves_the_hand_once_and_lands_on_the_beat(string id, int release, string socket, float travel, string prop, string archetype)
    {
        _ = prop; _ = archetype;
        var r = SwingRecipes.For(id)!;
        Assert.Equal(travel, r.TravelMs);
        var t = Timing(id);
        const int beat = 5000;
        foreach (var (start, plain) in Tempos.Select(p => Envelope(p, beat)).Append(Envelope(SoloBattle.DefaultBeatMs, beat, lateLeadMs: 60f)))
        {
            var perf = new SwingPerformance();
            var stage = new Stage();
            perf.TargetBuffer[0] = 0;
            perf.TargetBuffer[1] = 2;
            perf.TargetBuffer[2] = 3;
            perf.Begin(r, t, start, beat, start + plain.TotalMs, 300f, 3);
            var (releases, lands, releasedAt, landedAt) = Play(perf, stage, start, beat + 400f);
            Assert.Equal(1, releases);                              // ONE release (the one release cue)
            Assert.Equal(1, lands);
            Assert.Equal(3, perf.FlightCount);                      // one flight per swung creature
            Assert.True(releasedAt >= perf.ReleaseMs && releasedAt < perf.ReleaseMs + Frame, "released on the first frame at the release");
            Assert.True(landedAt >= beat && landedAt < beat + Frame, "landed on the frame that presents the beat");
            // each flight left the hand's socket on the release frame and its TIP met its own creature's contact point
            var hand = ActorSocketMap.ToArena(new SpriteFrame(null!, new Rectangle(release * 512, 0, 512, 512), new Rectangle(420, 451, 400, 400), SpriteEffects.None),
                                              512, release, t.Socket(release, socket)!.Value);
            for (var k = 0; k < 3; k++)
            {
                var v = perf.Flight(k);
                Assert.True(v.Placed && v.Landed);
                Assert.True(v.Driven);
                stage.TrySwingTarget(perf.Target(k), out var body);
                var contact = new Vector2(body.X + body.Width * r.ContactPoint.X, body.Y + body.Height * r.ContactPoint.Y);
                Assert.True(Vector2.Distance(v.Position, contact) <= v.HeadLengthPx * 0.5f + 0.5f, $"{id}: flight {k} lands at its creature");
                Assert.True(v.HeadLengthPx > 0f);
            }
            Assert.True(Vector2.Distance(perf.Flight(0).Trail(perf.Flight(0).TrailSamples - 1).Position, hand) < perf.Flight(0).HeadLengthPx, "from the hand");
            Assert.True(perf.Released && perf.Landed);
        }
    }

    [Fact]
    public void test_before_the_release_nothing_flies_and_the_flight_is_in_the_air_until_the_beat()
    {
        var r = SwingRecipes.Quiver;
        var t = Timing("quiver");
        var perf = new SwingPerformance();
        var stage = new Stage();
        perf.TargetBuffer[0] = 1;
        perf.Begin(r, t, 4400f, 5000, 5800f, 300f, 1);
        Assert.False(perf.Update(4700f, Frame / 1000f, stage).Released);
        Assert.Equal(0, perf.FlightCount);
        Assert.False(perf.Drawing(4700f));
        Assert.True(perf.Update(4800f, Frame / 1000f, stage).Released);   // exactly TravelMs before the beat
        Assert.Equal(1, perf.FlightCount);
        Assert.True(perf.Drawing(4800f));
        var mid = perf.Update(4900f, 0.1f, stage);
        Assert.False(mid.Landed);
        Assert.False(perf.Flight(0).Landed);
        Assert.True(perf.Update(4999.5f, 0.1f, stage) is { Landed: false });
        Assert.True(perf.Update(5000f, 0.0005f, stage).Landed);           // ON the beat
        Assert.True(perf.Flight(0).Landed);
        // one contact cue per swing ms: the first creature voices, the rest only take their picture
        Assert.True(perf.IsBeat(5000));
        Assert.True(perf.Contact(5000, new Rectangle(1290, 640, 120, 160)));
        Assert.False(perf.Contact(5000, new Rectangle(1380, 640, 120, 160)));
        // the flight's residue plays out and then the swing stops drawing
        for (var ms = 5000f; ms < 5600f; ms += Frame) perf.Update(ms, Frame / 1000f, stage);
        Assert.False(perf.Drawing(5600f));
        // a rewind / a new wave forgets the flights
        perf.Reset();
        Assert.Equal(0, perf.FlightCount);
    }

    [Fact]
    public void test_a_swing_whose_clip_left_the_figure_before_its_release_throws_nothing()
    {
        var perf = new SwingPerformance();
        var stage = new Stage();
        perf.Begin(SwingRecipes.Chorus, Timing("chorus"), 4400f, 5000, 5800f, 300f, 1);
        perf.EndClip();
        Assert.False(perf.Update(4900f, Frame / 1000f, stage).Released);
        Assert.Equal(0, perf.FlightCount);
    }

    [Fact]
    public void test_the_unbroken_lob_bows_up_and_the_arrow_flies_level()
    {
        var stage = new Stage();
        // half way through its flight, how far above the straight hand-to-creature line the missile is (px; + = above)
        float Lift(SwingRecipe r, string id)
        {
            var perf = new SwingPerformance();
            perf.TargetBuffer[0] = 0;
            perf.Begin(r, Timing(id), 4000f, 5000, 5800f, 300f, 1);
            perf.Update(5000f - r.TravelMs, 0f, stage);
            var v = perf.Flight(0);
            var from = v.Position;
            perf.Update(5000f - r.TravelMs / 2f, r.TravelMs / 2000f, stage);
            stage.TrySwingTarget(0, out var body);
            var contact = new Vector2(body.X + body.Width * r.ContactPoint.X, body.Y + body.Height * r.ContactPoint.Y);
            var straight = from.Y + (contact.Y - from.Y) * ((v.Position.X - from.X) / Math.Max(1f, contact.X - from.X));
            return straight - v.Position.Y;
        }
        Assert.True(Lift(SwingRecipes.Unbroken, "unbroken") > 10f, "the chip's lob bows up");
        Assert.InRange(Lift(SwingRecipes.Quiver, "quiver"), -3f, 3f);   // the arrow flies level
    }

    // ── ALLOCATION ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_two_hundred_missile_swings_with_flights_allocate_nothing()
    {
        var recipes = new[] { SwingRecipes.Quiver, SwingRecipes.Chorus, SwingRecipes.Unbroken };
        var timings = new[] { Timing("quiver"), Timing("chorus"), Timing("unbroken") };
        var perf = new SwingPerformance();
        var stage = new Stage();
        var body = new Rectangle(1200, 640, 120, 160);
        // warm every path once
        for (var k = 0; k < recipes.Length; k++)
        {
            perf.TargetBuffer[0] = 0;
            perf.TargetBuffer[1] = 1;
            perf.Begin(recipes[k], timings[k], 0f, 500, 900f, 300f, 2);
            for (var ms = 0f; ms < 900f; ms += Frame)
            {
                perf.Update(ms, Frame / 1000f, stage);
                if (perf.IsBeat((int)ms) || (ms >= 500 && ms - Frame < 500)) perf.Contact(500, body);
                perf.Compose(ms, true);
                perf.Compose(ms, false);
                perf.Drawing(ms);
            }
        }
        var sink = 0f;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var s = 0; s < 200; s++)
        {
            var k = s % recipes.Length;
            var start = 1000f * (s + 1);
            var beat = (int)start + 450;
            perf.TargetBuffer[0] = s % 4;
            perf.TargetBuffer[1] = (s + 1) % 4;
            perf.TargetBuffer[2] = (s + 2) % 4;
            perf.Begin(recipes[k], timings[k], start, beat, start + 800f, 300f, 1 + s % 3);
            for (var f = 0; f < 45; f++)
            {
                var ms = start + f * Frame;
                if (f == 30) perf.Retime(start + 700f);
                var step = perf.Update(ms, Frame / 1000f, stage);
                sink += step.Released ? 1f : 0f;
                sink += perf.FrameAt(ms) + perf.StepPx(ms);
                if (ms >= beat && ms - Frame < beat) perf.Contact(beat, body);
                sink += perf.Compose(ms, light: false) + perf.Compose(ms, light: true);
                foreach (var sprite in perf.Sprites) sink += sprite.Size.X;
                for (var j = 0; j < perf.FlightCount; j++) sink += perf.Flight(j).Position.X;
                sink += perf.Drawing(ms) ? 1f : 0f;
            }
        }
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sink > 0f);
    }

    [Fact]
    public void test_a_reused_projectile_visual_resets_without_allocating_and_spray_keeps_its_own_path()
    {
        var v = new ProjectileVisual(ProjectileLooks.QuiverArrow, 1, sparkCapacity: 4, shardCapacity: 8);
        var content = ProjectileLooks.QuiverArrow.HeadContent!.Value;
        v.PlaceDriven(Vector2.Zero, new Vector2(400f, 0f), Color.White, null, null, 0.9f, content, null, null, null, null, null);
        v.Drive(0.1f, 1f, new Vector2(400f, 0f), Vector2.UnitX);
        v.Land();
        Assert.True(v.Landed);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            v.Reset(i % 2 == 0 ? ProjectileLooks.ChorusCharm : ProjectileLooks.UnbrokenChip, i);
            v.PlaceDriven(Vector2.Zero, new Vector2(400f, 0f), Color.White, null, null, 0.9f, content, null, null, null, null, null);
            v.Drive(0.016f, 0.5f, new Vector2(200f, 0f), Vector2.UnitX);
            v.Land();
            v.Linger(0.016f);
        }
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        v.Reset(ProjectileLooks.QuiverArrow, 3);
        Assert.False(v.Placed);
        Assert.False(v.Landed);
        Assert.Equal(0, v.TrailSamples);
        Assert.Equal(content.Width * 0.9f, new Func<float>(() =>
        {
            v.PlaceDriven(Vector2.Zero, Vector2.UnitX * 300f, Color.White, null, null, 0.9f, content, null, null, null, null, null);
            return v.HeadLengthPx;
        })(), 3);
        // a look that sheds more than the instance holds is refused, never silently grown
        Assert.Throws<ArgumentException>(() => v.Reset(ProjectileLooks.SeekerThrowingKnife with { Sparks = 9 }, 0));
        // SPRAY's composite is untouched: its own constructor, its pad placement
        var spray = Source("src", "IdleXIdle.Game", "Presentation", "ActionPerformance.cs");
        Assert.Contains("var v = new ProjectileVisual(look, f.Slot * 97 + 13);", spray);
        Assert.Contains("v.PlaceDriven(f.From, f.To, Tint, material!, edge!, scale, KnifePad, trail!, glint!, spark!, shard!, flash!);", spray);
    }

    // ── THE SCREEN ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_screen_updates_the_swing_before_the_events_and_voices_one_release_never_lead()
    {
        var src = Source("src", "IdleXIdle.Game", "HuntScreen.cs");
        // after the performance, before the frame's events are crossed, on the fight's clock AND on the wave-clear break's
        Assert.Contains("UpdatePerformance(dt);\n        UpdateSwing(dt);\n        UpdateReactions();", src.Replace("\r\n", "\n"));
        Assert.Contains("UpdatePerformance(dt);\n                UpdateSwing(dt);\n                UpdateReactions();", src.Replace("\r\n", "\n"));
        var update = src[src.IndexOf("private void UpdateSwing(float dt)", StringComparison.Ordinal)..];
        update = update[..update.IndexOf("private int DrawSwingObjects(", StringComparison.Ordinal)];
        Assert.Contains("var step = _swing.Update(_playheadMs, dt, this);", update);
        Assert.Contains("Sound?.PlayFirst(r.ReleaseCues, r.ReleaseVolume, 0f, Pan(step.ReleaseAt.X, 0.3f), 0.04f);", update);
        Assert.DoesNotContain("lead: true", update);
        Assert.DoesNotContain("Duck", update);
        Assert.DoesNotContain("Say(", update);
        Assert.Contains("PresentTrace.Log(\"swing-release\"", update);
        Assert.Contains("PresentTrace.Log(\"swing-land\"", update);
        // the swing's stage resolves its strip by the key cached at the commit: no per-frame string
        Assert.Contains("_swingStripKey = key;", src);
        Assert.Contains("bool ISwingStage.TrySwingFrame(int frame, out SpriteFrame drawn, out int frameSize)", src);
        // the flights draw their material untinted with the swing's material and their light inside the light pass
        var objects = src[src.IndexOf("private int DrawSwingObjects(", StringComparison.Ordinal)..];
        objects = objects[..objects.IndexOf("private void DrawSwing(", StringComparison.Ordinal)];
        Assert.Contains("if (light) { v.Draw(b); n += v.LastSprites; }", objects);
        Assert.Contains("else if (!v.Landed) { v.DrawMaterial(b); n++; }", objects);
        Assert.Contains("_swingDrawSprites += DrawSwingObjects(b, light);", src);
    }

    [Fact]
    public void test_a_performed_swing_never_spawns_a_strip_projectile()
    {
        var src = Source("src", "IdleXIdle.Game", "HuntScreen.cs");
        // every swing method of the screen, from the commit to the draw, and the swing's own classes
        var swing = src[src.IndexOf("private bool SwingOnFigure", StringComparison.Ordinal)..];
        swing = swing[..swing.IndexOf("private Color? AgnosticOutline(", StringComparison.Ordinal)];
        foreach (var text in new[]
                 {
                     swing,
                     Source("src", "IdleXIdle.Game", "Presentation", "SwingPerformance.cs"),
                     Source("src", "IdleXIdle.Game", "Presentation", "SwingRecipe.cs"),
                     Source("src", "IdleXIdle.Game", "Presentation", "ReachStrand.cs"),
                 })
        {
            Assert.DoesNotContain("PlayFx(", text);
            Assert.DoesNotContain("_projectile", text);
            Assert.DoesNotContain("_strike", text);
            Assert.DoesNotContain("FxFor(", text);
            Assert.DoesNotContain("_vfx.", text);
        }
        // and the Strike case's generic puff is still kept off a performed swing's blow
        Assert.Contains("if (!swingHit) PlayFx(VfxProfiles.ImpactWeak, VfxSubject.Creature(e.Slot), Steel);", src);
    }
}
