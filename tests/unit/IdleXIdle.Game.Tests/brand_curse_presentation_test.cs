using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Game.Presentation;
using IdleXIdle.Game.Presentation.Curse;
using static IdleXIdle.Game.Presentation.Curse.CurseMaterial;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// BRAND's curse, the PRODUCTION PRESENTATION (ADR-013 §1, §5, §8): what the shader is handed for each creature, composed
/// device-free from the mark's truth on the playhead. These pin what the runtime swap must keep and what it fixed: no
/// texture is ever read back, a steady frame allocates nothing, overlapping blooms compose the same picture whatever the
/// frames, a multi-depth jump blooms every new territory in turn, and depth is how many territories the body shows.
/// </summary>
public class brand_curse_presentation_test
{
    private static readonly MarkRecipe Brand = MarkRecipes.SeekerBrand;
    private const float Frame = 1000f / 60f;

    /// <summary>A creature drawn at half its 512 px strip frame (the texture itself is never touched by composition).</summary>
    private static readonly SpriteFrame Drawn = new(null!, new Rectangle(0, 0, 512, 512), new Rectangle(1000, 500, 256, 256), SpriteEffects.None);

    private static readonly Rectangle Body = new(1040, 540, 176, 216);

    private static readonly Vector2 BodyPoint = new(1128, 640);

    /// <summary>A host whose body seats four territories (in source pixels, from its idle strip's mean body point).</summary>
    private static CurseHostData Host(int seats = 4, HostLook? look = null)
    {
        Layout Lay()
        {
            var lay = new Layout { Available = seats };
            var offsets = new[] { new Vector2(0, 0), new Vector2(46, 40), new Vector2(-44, 36), new Vector2(4, 92) };
            var share = new[] { 1f, 0.82f, 0.72f, 0.64f };
            for (var k = 0; k < seats; k++)
            {
                lay.Offset[k] = offsets[k];
                lay.Diameter[k] = 120f * share[k];
                lay.Burn[k] = k == 2 ? 0.4f : 0f;
            }
            return lay;
        }
        var l = look ?? new HostLook(0.3f, 0.2f, 0f, 0.3f);
        return new CurseHostData(l, CurseHost.AshBurn(l), 512, 8, new Rectangle(100, 60, 300, 420), new Vector2(256, 280), Lay(), Lay());
    }

    private static CursePresentation Curse(MarkPerformance mark, int slots)
    {
        var c = CursePresentation.Headless();
        c.BeginWave(mark, slots);
        for (var s = 0; s < slots; s++) mark.Pin(s, Drawn, BodyPoint);
        return c;
    }

    private static readonly (float, int, bool)[] Etch =
        { (2000f, 120, false), (4000f, 170, false), (6000f, 220, false), (8000f, 240, false) };

    // ── NOTHING IS READ BACK ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_curse_reads_no_texture_back_at_runtime()
    {
        // ADR-013 §3: no Texture2D.GetData on any path the curse runs during play: its own code, the mark's truth it
        // reads, and the screen's call sites (the prototype read a whole strip back on the frame a creature first lunged
        // and on the frame it died)
        var dir = RepoFile("src", "IdleXIdle.Game", "Presentation", "Curse");
        foreach (var file in Directory.GetFiles(dir, "*.cs").Append(RepoFile("src", "IdleXIdle.Game", "Presentation", "MarkPerformance.cs")))
        {
            var code = Code(File.ReadAllText(file));
            Assert.DoesNotContain("GetData", code);
            Assert.DoesNotContain("SilhouetteProbe", code);   // (the torso probe reads a strip back: the mark no longer falls back to it)
        }
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        foreach (var site in new[] { "private void DrawMarkOn(", "private void DrawCurseLeaving(", "private void PinMarkOnDeath(" })
            Assert.DoesNotContain("GetData", MethodBody(hunt, site));
        // the curse never even holds a texture it could read: it is handed the creature's frame to DRAW, and the only
        // texture its shader owns is the atlas it uploaded itself
        foreach (var type in new[] { typeof(CursePresentation), typeof(CurseHostData), typeof(MarkPerformance) })
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Assert.DoesNotContain(type.GetFields(all), f => f.FieldType == typeof(Texture2D) || f.FieldType == typeof(Texture2D[]));
            Assert.DoesNotContain(type.GetMethods(all).SelectMany(m => m.GetParameters()), p => p.ParameterType == typeof(Texture2D));
        }
        var effect = Code(File.ReadAllText(Path.Combine(dir, "BrandCurseEffect.cs")));
        Assert.Contains("Atlas = CurseAtlas.Approved.Upload(device);", effect);
        // and the driver's program link (its first draw) is paid at load too, never on the first cursed combat frame
        Assert.Contains("fx.Warm();", MethodBody(File.ReadAllText(Path.Combine(dir, "BrandCurseEffect.cs")), "public static BrandCurseEffect Load("));
        // ... and the composition's first-run compile: the rehearsal runs at load and composes every path without a device
        Assert.Contains("Rehearse();", MethodBody(File.ReadAllText(Path.Combine(dir, "CursePresentation.cs")), "public static CursePresentation Load("));
        CursePresentation.Rehearse();
        // the shader binary and the baked host data are read once at load (BeginWave), never on a Draw frame
        Assert.Contains("CurseHostData.Warm();", MethodBody(hunt, "private void BeginWave("));
        Assert.Contains("CursePresentation.Load(_ui.Device)", MethodBody(hunt, "private void BeginWave("));
    }

    // ── IT NEVER COSTS THE FRAME ─────────────────────────────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static float ComposeAll(CursePresentation c, CurseHostData data, int slots)
    {
        var sink = 0f;
        for (var p = 1500f; p < 11000f; p += Frame)
            for (var s = 0; s < slots; s++)
            {
                sink += c.Convulse(s, p).Y + c.Shade(s, p, Color.White).R;
                if (c.ComposeLiving(s, p, Drawn, 4096, 512, Body, data))
                    sink += c.Territories[0].At + c.Puffs[0].Radius + c.StreakCount + c.Host.Shade.X;
                if (c.ComposeLeaving(s, p, Drawn, 4096, 512, data, out var pass))
                    sink += c.StreakCount + (pass ? c.Territories[0].Front : 0f);
            }
        return sink;
    }

    [Fact]
    public void test_composing_the_curse_on_steady_frames_allocates_nothing()
    {
        // every per-frame value is a field reused or on the stack: SPRAWL's row with its hops, deepens and two falls, and
        // a front that carries the mark through two transfers, each composed every frame for nine seconds
        var data = Host();
        var sprawl = Curse(new MarkPerformance(Brand, 2, Etch, new[] { (6700f, 0), (8200f, 1) }, 4, wholeWave: true, frontFullPercent: 0), 4);
        var front = Curse(new MarkPerformance(Brand, 2, Etch, new[] { (6700f, 0), (8200f, 1) }, 4, wholeWave: false, frontFullPercent: 0), 4);
        var sink = ComposeAll(sprawl, data, 4) + ComposeAll(front, data, 4);   // warm every path (tiering, the timelines' steps)
        var least = long.MaxValue;
        for (var run = 0; run < 3; run++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            sink += ComposeAll(sprawl, data, 4) + ComposeAll(front, data, 4);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.Equal(0L, least);
        Assert.True(sink > 0f);
        // and a new wave reuses the per-slot tables: no growth unless the row is larger than any before it
        var mark = new MarkPerformance(Brand, 2, Etch, Array.Empty<(float, int)>(), 4, wholeWave: false, frontFullPercent: 0);
        sprawl.BeginWave(mark, 4);
        var waveBefore = GC.GetAllocatedBytesForCurrentThread();
        sprawl.BeginWave(mark, 3);
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - waveBefore);
    }

    // ── EACH TERRITORY ON ITS OWN CLOCK, THROUGH THE PRESENTATION ───────────────────────────────────────────────────────

    /// <summary>SPRAWL: the source applies at 2000; the hop to slot 1 lands at 2200 (an ARRIVAL), and the 2250 tick's deepen
    /// is shown on it at 2430, inside the arrival's bloom.</summary>
    private static MarkPerformance Overlap(int secondPercent)
        => new(Brand, 2, new[] { (2000f, 120, false), (2250f, secondPercent, false) }, Array.Empty<(float, int)>(), 4,
               wholeWave: true, frontFullPercent: 0);

    private static TerritoryParams[] At(CursePresentation c, int slot, float p, CurseHostData data)
        => c.ComposeLiving(slot, p, Drawn, 4096, 512, Body, data) ? (TerritoryParams[])c.Territories.Clone() : Array.Empty<TerritoryParams>();

    [Fact]
    public void test_an_arrival_and_a_deepen_that_overlap_compose_one_picture_whatever_the_frames()
    {
        var data = Host();
        var mark = Overlap(170);
        Assert.Equal(MarkPhase.Reform, mark.TryPhase(1, 2201f, out _));
        Assert.Equal(1, mark.DrawnStage(1, 2429f));
        Assert.Equal(2, mark.DrawnStage(1, 2431f));
        var samples = Enumerable.Range(0, 30).Select(i => 2150f + i * 50f).ToArray();
        // the same moments, reached frame by frame, at a ragged 7 ms, and backwards (a seek, a rewind)
        var byFrame = Curse(mark, 4);
        var expected = new Dictionary<float, TerritoryParams[]>();
        for (var p = 2100f; p <= 3700f; p += Frame) At(byFrame, 1, p, data);
        foreach (var p in samples) expected[p] = At(byFrame, 1, p, data);
        var ragged = Curse(Overlap(170), 4);
        for (var p = 2100f; p <= 3700f; p += 7f) At(ragged, 1, p, data);
        var backwards = Curse(Overlap(170), 4);
        foreach (var p in samples.Reverse())
        {
            Assert.Equal(expected[p], At(backwards, 1, p, data));
            Assert.Equal(expected[p], At(ragged, 1, p, data));
        }
        // the arrival's territory keeps its own bloom through the deepen (never restarted), and the deepen's territory
        // blooms after the shadow's travel
        var last = -1f;
        for (var p = 2200f; p <= 3700f; p += Frame)
        {
            var t = At(byFrame, 1, p, data);
            Assert.True(t[0].At >= last - 1e-5f, $"the arrival's territory stepped back at {p:0}");
            last = t[0].At;
        }
        var clock = byFrame.TimelineOf(1).Clock;
        At(byFrame, 1, 3000f, data);
        Assert.Equal(2, clock.Kind[0]);
        Assert.Equal(3, clock.Kind[1]);
        Assert.Equal(2200f, clock.Born[0], 1);
        Assert.Equal(2430f + CurseTimeline.TravelMs, clock.Born[1], 0);
        Assert.True(clock.Born[1] < clock.Born[0] + clock.Grow[0], "the deepen did not land during the arrival's bloom");
    }

    [Fact]
    public void test_a_multi_depth_jump_blooms_every_new_territory_one_after_another()
    {
        // 120 -> 220 % on one tick: depth 1 straight to depth 3. Every new territory blooms, staggered, none popped in
        var data = Host();
        var c = Curse(Overlap(220), 4);
        var first = new float[4];
        Array.Fill(first, float.NaN);
        var partway = new bool[4];
        for (var p = 2200f; p <= 4000f; p += Frame)
        {
            var t = At(c, 1, p, data);
            for (var k = 1; k < 4 && k < t.Length; k++)
            {
                if (t[k].At > 0f && float.IsNaN(first[k])) first[k] = p;
                partway[k] |= t[k].At is > 0.2f and < 1.2f && t[k].DrainAlpha < DrainAt(data, k) - 0.01f;
            }
        }
        Assert.Equal(4, c.TerritoryCount);
        for (var k = 1; k < 4; k++)
        {
            Assert.False(float.IsNaN(first[k]), $"territory {k} never bloomed");
            Assert.True(partway[k], $"territory {k} popped in without its bloom");
        }
        for (var k = 2; k < 4; k++)
            Assert.InRange(first[k] - first[k - 1], CurseTimeline.DeepenStaggerMs - Frame, CurseTimeline.DeepenStaggerMs + Frame);
        // the travel: a deepen's shadow under the skin, at most the shader's four brightest puffs, from an old territory
        var travelled = false;
        var c2 = Curse(Overlap(220), 4);
        for (var p = 2430f; p <= 2800f; p += Frame)
        {
            At(c2, 1, p, data);
            travelled |= c2.Puffs.Any(f => f.Radius > 0f);
            Assert.True(c2.Puffs.Count(f => f.Radius > 0f) <= CurseMaterial.MaxPuffs);
        }
        Assert.True(travelled, "the deepen's shadow never travelled");
    }

    private static float DrainAt(CurseHostData data, int k)
    {
        var lay = data.LayoutFor(1);
        var whole = CurseMaterial.Living(data.Look, lay.Burn[k], 1f, 1f, 1f, 1f, 0f, 0, 1f, 0f);
        return whole.DrainAlpha;
    }

    [Fact]
    public void test_depth_is_how_many_territories_the_presentation_shows()
    {
        // one, then a second elsewhere, then every one the body seats (three or four), the curse's own draw each time
        foreach (var seats in new[] { 4, 3 })
        {
            var data = Host(seats);
            var c = Curse(new MarkPerformance(Brand, 2, Etch, Array.Empty<(float, int)>(), 4, wholeWave: false, frontFullPercent: 0), 4);
            foreach (var (p, want) in new[] { (3500f, 1), (5500f, 2), (7500f, Math.Min(seats, CurseSeating.TerritoriesAt(3))) })
            {
                for (var q = p - 1500f; q <= p; q += Frame) At(c, 0, q, data);
                var t = At(c, 0, p, data);
                Assert.Equal(want, c.TerritoryCount);
                Assert.Equal(want, t.Count(x => x.DrainAlpha > 0f));
                // every territory sits elsewhere on the body (its own mask placement)
                Assert.Equal(want, t.Take(want).Select(x => x.MaskU.Z).Distinct().Count());
            }
            // the creature the mark is not on shows nothing
            Assert.False(c.ComposeLiving(1, 7500f, Drawn, 4096, 512, Body, data));
        }
        // a creature the mark is still on its way to takes a faint shade through its own tint, never a pass
        var sprawl = Curse(Overlap(170), 4);
        Assert.Equal(MarkPhase.Waiting, sprawl.Mark!.TryPhase(2, 2200f, out _));
        var shaded = sprawl.Shade(2, 2200f, new Color(200, 180, 160, 255));   // (risen: it waits from 2000)
        Assert.Equal(new Color(190, 171, 152, 255), shaded);
        Assert.False(sprawl.ComposeLiving(2, 2200f, Drawn, 4096, 512, Body, Host()));
        Assert.Equal(Color.White, sprawl.Shade(0, 2100f, Color.White));
    }

    [Fact]
    public void test_old_territories_keep_their_accent_and_light_through_a_deepen()
    {
        // ADR-013 §5: the territories already infected never lose their idle accent for a whole deepen, and their light
        // never jumps by half when the new territory finishes (each settles on its own bloom's end)
        var data = Host();
        var c = Curse(new MarkPerformance(Brand, 2, Etch, Array.Empty<(float, int)>(), 4, wholeWave: false, frontFullPercent: 0), 4);
        for (var p = 2000f; p < 3800f; p += Frame) At(c, 0, p, data);
        var last = At(c, 0, 3800f, data)[0];
        for (var p = 3800f + Frame; p < 5200f; p += Frame)
        {
            var now = At(c, 0, p, data)[0];
            Assert.True(MathF.Abs(now.Accent - last.Accent) < 0.03f, $"the old territory's accent jumped {last.Accent:0.000} -> {now.Accent:0.000} at {p:0}");
            Assert.True(MathF.Abs(now.Emit - last.Emit) < 0.25f, $"the old territory's light jumped {last.Emit:0.000} -> {now.Emit:0.000} at {p:0}");
            last = now;
        }
    }

    [Fact]
    public void test_a_falling_host_collapses_its_territories_and_smokes_out()
    {
        // the dying host plays the death story (a flash, a collapse, its territories smoking out), the boss's too; a host
        // that only waited for the mark wears none
        var data = Host();
        var mark = new MarkPerformance(Brand, 2, Etch, new[] { (6700f, 0) }, 2, wholeWave: false, frontFullPercent: 0);
        var c = Curse(mark, 2);
        for (var p = 2000f; p < 6700f; p += Frame) At(c, 0, p, data);
        Assert.True(c.ComposeLeaving(0, 6700f + 60f, Drawn, 4096, 512, data, out var flash) && flash);
        Assert.Contains(c.Territories, t => t.EmitAdd > 0f || t.Fissure > 0f);
        Assert.True(c.ComposeLeaving(0, 6700f + 500f, Drawn, 4096, 512, data, out var collapse) && collapse);
        Assert.Contains(c.Territories, t => t.Front > 0f);
        Assert.True(c.StreakCount > 0, "no smoke-out");
        Assert.False(c.ComposeLeaving(0, 6700f + 900f, Drawn, 4096, 512, data, out _));
        Assert.False(c.ComposeLeaving(1, 6800f, Drawn, 4096, 512, data, out _));   // slot 1 is waiting: it wore nothing
    }

    [Fact]
    public void test_the_pass_is_due_exactly_when_the_curse_composes_one()
    {
        // ADR-013 §1: the pass is the creature's ONLY draw, so the arena asks beforehand whether it will run and draws the
        // frame transparent if so. The answer must be the composition's own, every moment, every slot: a "yes" with no pass
        // would lose the creature for a frame (DrawOn then draws it plain), a "no" with a pass would composite it twice
        var data = Host();
        foreach (var (name, make) in new (string, Func<MarkPerformance>)[]
                 {
                     ("front with a transfer", () => Front(Etch, (6700f, 0))),
                     ("sprawl death in a deepen", () => new MarkPerformance(Brand, 2, Etch, new[] { (4200f, 1) }, 4, wholeWave: true, frontFullPercent: 0)),
                     ("arrival + jump", () => Overlap(220)),
                 })
        {
            var c = Curse(make(), 4);
            var passes = 0;
            for (var p = 1500f; p < 9500f; p += 5f)
                for (var s = 0; s < 4; s++)
                {
                    var leaving = c.ComposeLeaving(s, p, Drawn, 4096, 512, data, out var pass);
                    Assert.True((leaving && pass) == c.LeavingPassDue(s, p, data), $"{name}: slot {s}'s leaving pass at {p:0}");
                    if (leaving) continue;
                    var living = c.ComposeLiving(s, p, Drawn, 4096, 512, Body, data);
                    Assert.True(living == c.PassDue(s, p, data), $"{name}: slot {s}'s living pass at {p:0}");
                    if (living) passes++;
                }
            Assert.True(passes > 0, $"{name}: no pass ever composed");
            Assert.False(c.PassDue(0, 5000f, null));   // no baked host: the faint shade only, the arena draws it
        }
    }

    [Fact]
    public void test_a_wave_ending_kills_leaving_holds_the_break_open_until_it_is_over()
    {
        // the leaving is timed on the playhead, which stops when the wave's replay is over unless something is still
        // playing: the curse leaving a host must count, or a wave-ending kill's flash froze on the corpse for the whole
        // break (a cursed boss's, always). It runs out inside the break.
        var c = Curse(Front(Etch, (6700f, 0)), 4);
        Assert.False(c.StillLeaving(6699f));
        Assert.True(c.StillLeaving(6700f));
        Assert.True(c.StillLeaving(6700f + CursePresentation.LeaveMs - 1f));
        Assert.False(c.StillLeaving(6700f + CursePresentation.LeaveMs));
        Assert.False(Curse(Front(Etch), 4).StillLeaving(7000f));   // nobody fell
        // a host that only waited for the mark (a SPRAWL hop's target that fell first) wears nothing: nothing leaves it
        var sprawl = new MarkPerformance(Brand, 2, new[] { (2000f, 120, false), (2250f, 170, false) }, new[] { (2100f, 3) }, 4,
                                         wholeWave: true, frontFullPercent: 0);
        Assert.Equal(MarkPhase.Waiting, sprawl.TryPhase(3, 2099f, out _));
        Assert.False(Curse(sprawl, 4).StillLeaving(2200f));
        Assert.True(Descent.WaveBreakSeconds * 1000f >= CursePresentation.LeaveMs, "the break is shorter than the leaving");
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        Assert.Contains("|| _curse is { } c && c.StillLeaving(_playheadMs)", MethodBody(hunt, "private bool ActionStillPlaying()"));
    }

    [Fact]
    public void test_a_host_that_falls_before_a_territory_blooms_never_leaks_from_it()
    {
        // a deepen's territory is born after the shadow's travel; a host that falls in between never showed it, so neither
        // its resting leak nor its bloom's burst may rise from that seat while the host collapses (only what was in flight
        // at the fall goes on). The second seat sits far to the side, so any streak from it is told by where it is
        var lay = new Layout { Available = 2 };
        lay.Offset[0] = Vector2.Zero;
        lay.Diameter[0] = 120f;
        lay.Offset[1] = new Vector2(400f, 0f);
        lay.Diameter[1] = 100f;
        var look = new HostLook(0.3f, 0.2f, 0f, 0.3f);
        var data = new CurseHostData(look, CurseHost.AshBurn(look), 512, 8, new Rectangle(100, 60, 300, 420), new Vector2(256, 280), lay, lay);
        var probe = Front(Etch);
        var shown = 3000f;
        while (probe.DrawnStage(0, shown) < 2) shown += 1f;
        var died = shown + 100f;
        var c = Curse(Front(Etch, (died, 0)), 4);
        for (var p = 2000f; p < died; p += Frame) At(c, 0, p, data);
        var seat1 = BodyPoint.X + 400f * Drawn.Dest.Width / Drawn.Src.Width;
        var fromSeat0 = 0;
        for (var p = died; p < died + CursePresentation.LeaveMs; p += 5f)
        {
            if (!c.ComposeLeaving(0, p, Drawn, 4096, 512, data, out _)) continue;
            for (var i = 0; i < c.StreakCount; i++)
            {
                var x = c.Streaks[i].Dest.Center.X;
                Assert.True(x < seat1 - 100f, $"a streak rose at x {x} from the seat the host never showed, {p - died:0} ms after its fall");
                fromSeat0++;
            }
        }
        // the fixture: the territory was counted (its deepen had passed) but born after the fall; the shown one smoked out
        var clock = c.TimelineOf(0).Clock;
        Assert.Equal(2, clock.Count);
        Assert.True(clock.Born[1] > died, $"the second territory was born at {clock.Born[1]:0}, before the fall at {died:0}");
        Assert.True(fromSeat0 > 0, "the territory the host wore never smoked out");
    }

    // ── NOTHING STEPS (ADR-013 §5): every level the shader and the streaks are handed is continuous on the playhead ─────

    /// <summary>One moment of one creature's curse, as the screen would draw it (its leaving once it fell, else its
    /// living curse): per territory its light (glow + emission + accent + fissures + flash), its cover (drain + burn), its
    /// bloom front's reach and level; how dark the body is drawn (draw tint x body shade); the streaks' and puffs' light.</summary>
    private sealed record Moment(float[] Light, float[] Cover, float[] At, float[] Front, float Body, float Streaks, float Puffs);

    private static Moment Sample(CursePresentation c, int slot, float p, CurseHostData data)
    {
        var light = new float[ShaderTerritories];
        var cover = new float[ShaderTerritories];
        var at = new float[ShaderTerritories];
        var front = new float[ShaderTerritories];
        c.Convulse(slot, p);
        var body = 1f - c.ShadeOf(slot, p);
        var drawn = c.ComposeLeaving(slot, p, Drawn, 4096, 512, data, out var pass);
        if (!drawn) pass = drawn = c.ComposeLiving(slot, p, Drawn, 4096, 512, Body, data);
        if (pass)
        {
            body *= c.Host.Shade.X;
            for (var k = 0; k < ShaderTerritories; k++)
            {
                var t = c.Territories[k];
                light[k] = t.Glow + t.Emit + t.Accent + t.Fissure + t.EmitAdd;
                cover[k] = t.DrainAlpha + t.BurnAlpha;
                at[k] = t.At;
                front[k] = t.Front;
            }
        }
        var streaks = 0f;
        for (var i = 0; drawn && i < c.StreakCount; i++)
        {
            var s = c.Streaks[i].Colour;
            streaks += Math.Max(Math.Max(s.R, s.G), Math.Max(s.B, s.A)) / 255f;
        }
        var puffs = pass ? c.Puffs.Sum(f => f.Colour.X + f.Colour.Y + f.Colour.Z) : 0f;
        return new Moment(light, cover, at, front, body, streaks, puffs);
    }

    /// <summary>
    /// The largest change of any level between two moments 1 ms apart over [<paramref name="from"/>, <paramref name="to"/>]:
    /// a level that moves continuously changes by far less in 1 ms than <paramref name="limit"/>, a pop by far more,
    /// whatever the frame rate. (The bloom front is only counted while its band still finds mask: past
    /// <see cref="FrontSeen"/> it lights nothing, whatever its level.)
    /// </summary>
    private static void AssertNoStep(string scenario, Func<CursePresentation> make, int slot, float from, float to, CurseHostData data,
                                     float limit = 0.03f, float bodyLimit = 0.004f)
    {
        var c = make();
        var last = Sample(c, slot, from, data);
        for (var p = from + 1f; p <= to; p += 1f)
        {
            var now = Sample(c, slot, p, data);
            for (var k = 0; k < ShaderTerritories; k++)
            {
                Check(now.Light[k] - last.Light[k], limit, $"territory {k}'s light");
                Check(now.Cover[k] - last.Cover[k], limit, $"territory {k}'s cover");
                Check(now.At[k] - last.At[k], limit, $"territory {k}'s reveal");
                if (now.At[k] < FrontSeen && last.At[k] < FrontSeen) Check(now.Front[k] - last.Front[k], limit, $"territory {k}'s front");
            }
            Check(now.Body - last.Body, bodyLimit, "the body's shade");
            Check(now.Streaks - last.Streaks, 2f * limit, "the wisps / smoke");
            Check(now.Puffs - last.Puffs, 3f * limit, "the travelling puffs");   // (three channels, up to four puffs rising together)
            last = now;

            void Check(float d, float max, string what)
                => Assert.True(MathF.Abs(d) <= max, $"{scenario}: {what} stepped by {d:0.000} in 1 ms at {p:0}");
        }
    }

    /// <summary>The front's band (0.1 wide) finds no texel born later than 1.2: past this reach it lights nothing.</summary>
    private const float FrontSeen = 1.29f;

    private static MarkPerformance Front(IReadOnlyList<(float, int, bool)> ticks, params (float, int)[] falls)
        => new(Brand, 2, ticks, falls, 4, wholeWave: false, frontFullPercent: 0);

    [Fact]
    public void test_apply_and_every_deepen_never_step_a_territorys_light_or_reveal()
    {
        // the apply's gather and bloom, each deepen (old territories' accent, afterglow and reaction, the new one's bloom,
        // the afterglow taking over from the front), depth 1 -> 2 -> 3, on a violet host and on an ash-burned one
        foreach (var look in new HostLook?[] { null, new HostLook(0.12f, 0.1f, 0.6f, 0.7f) })
        {
            var data = Host(look: look);
            AssertNoStep($"etch {(look is null ? "violet" : "ash")}", () => Curse(Front(Etch), 4), 0, 1500f, 9000f, data);
        }
    }

    [Fact]
    public void test_a_deepen_inside_the_last_ones_reaction_never_cuts_it()
    {
        // two deepens 100 ms apart (a tick, then a catch-up): the first's reaction in the old territory, its shadow's
        // travel, and the second's (only the last counted: the old reaction was cut to nothing in one frame)
        var ticks = new[] { (2000f, 120, false), (4000f, 170, false), (4100f, 220, false) };
        var mark = Front(ticks);
        Assert.True(mark.DrawnStage(0, 4090f) == 2 && mark.DrawnStage(0, 4190f) == 3, "the fixture lost its two close deepens");
        AssertNoStep("close deepens", () => Curse(Front(ticks), 4), 0, 3800f, 5200f, Host());
        // both reactions are really there: the old territory is lifted by the first deepen while the second lands
        var c = Curse(Front(ticks), 4);
        var calm = Sample(c, 0, 3900f, Host()).Light[0];
        Assert.True(Sample(c, 0, 4190f, Host()).Light[0] > calm * 1.2f, "the old territory never reacted to the first deepen");
    }

    [Fact]
    public void test_overlapping_blooms_and_a_multi_depth_jump_never_step()
    {
        // an arrival with a deepen inside it (170 %), and an arrival with a jump straight to depth 3 (220 %): every
        // territory staggered, more puffs alive than the shader takes (the four kept fade out at the fifth's level)
        AssertNoStep("arrival + deepen", () => Curse(Overlap(170), 4), 1, 1900f, 4500f, Host());
        AssertNoStep("arrival + jump", () => Curse(Overlap(220), 4), 1, 1900f, 4500f, Host());
        AssertNoStep("source of the row", () => Curse(Overlap(220), 4), 0, 1500f, 4500f, Host());
    }

    [Fact]
    public void test_waiting_arriving_and_transfer_never_step_the_body_or_the_curse()
    {
        // SPRAWL: each creature's faint waiting shade rises in, then hands over to the carrying shade as it arrives;
        // a transfer: the new front waits (no projectile), then the corruption awakens and blooms
        for (var s = 1; s < 4; s++)
            AssertNoStep($"sprawl slot {s}", () => Curse(Overlap(170), 4), s, 1900f, 4500f, Host());
        AssertNoStep("transfer, new host", () => Curse(Front(Etch, (6700f, 0)), 4), 1, 6000f, 10000f, Host());
        var c = Curse(Front(Etch, (6700f, 0)), 4);
        Assert.Equal(MarkPhase.Waiting, c.Mark!.TryPhase(1, 6800f, out _));
        Assert.True(c.ShadeOf(1, 6700f + 200f) > 0.04f, "the new host never took the waiting shade");
        Assert.False(c.ComposeLiving(1, 6800f, Drawn, 4096, 512, Body, Host()));
        var lands = 6800f;
        while (c.Mark!.TryPhase(1, lands, out _) == MarkPhase.Waiting) lands += 1f;
        Assert.Equal(MarkPhase.Reform, c.Mark.TryPhase(1, lands, out _));
        Assert.True(c.ComposeLiving(1, lands + 300f, Drawn, 4096, 512, Body, Host()) && c.TerritoryCount >= 1, "the corruption never awakened");
        Assert.Equal(2, c.TimelineOf(1).Clock.Kind[0]);
    }

    [Fact]
    public void test_a_falling_host_flares_collapses_and_smokes_out_without_a_step()
    {
        // grown (depth 3), mid-bloom (it fell 300 ms into its first bloom), and a SPRAWL host falling inside a deepen;
        // the living light carries into the flare and goes out with the collapse, the smoke eases in
        AssertNoStep("death at depth 3", () => Curse(Front(Etch, (8300f, 0)), 4), 0, 7800f, 9400f, Host());
        AssertNoStep("death at depth 3, ash", () => Curse(Front(Etch, (8300f, 0)), 4), 0, 7800f, 9400f, Host(look: new HostLook(0.12f, 0.1f, 0.6f, 0.7f)));
        AssertNoStep("death mid-bloom", () => Curse(Front(Etch, (2300f, 0)), 4), 0, 1700f, 3400f, Host());
        AssertNoStep("sprawl death in a deepen", () => Curse(new MarkPerformance(Brand, 2, Etch, new[] { (4200f, 1) }, 4, wholeWave: true, frontFullPercent: 0), 4),
                     1, 3800f, 5300f, Host());
        // and it is the death story: the flare peaks above the living light, then all of it goes out
        var c = Curse(Front(Etch, (8300f, 0)), 4);
        var before = Sample(c, 0, 8299f, Host()).Light.Sum();
        Assert.True(Sample(c, 0, 8300f + 75f, Host()).Light.Sum() > before, "no flare");
        Assert.Equal(0f, Sample(c, 0, 8300f + 760f, Host()).Light.Sum(), 3);
        Assert.True(Sample(c, 0, 8300f + 500f, Host()).Streaks > 0f, "no smoke-out");
    }

    [Fact]
    public void test_a_step_down_fades_its_territories_instead_of_hiding_them()
    {
        // the truth never steps a segment down today (a shown stage only deepens; a transfer is a new segment on another
        // creature), but the timeline must never pop if it does: the territories it no longer shows fade out, then go
        var c = new TerritoryClock();
        var steps = new[] { new StageStep(500f, 1, 3), new StageStep(2000f, 3, 1) };
        var last = new float[4];
        for (var t = 0f; t <= 3000f; t += 1f)
        {
            CurseTimeline.Replay(0f, 1, 1, steps, t, 4, c);
            for (var k = 0; k < 4; k++)
            {
                var r = k < c.Count ? c.Reveal(k, t) : 0f;
                Assert.True(MathF.Abs(r - last[k]) < 0.01f, $"territory {k} stepped {last[k]:0.000} -> {r:0.000} at {t}");
                last[k] = r;
            }
            if (t is > 2000f and < 2000f + CurseTimeline.FadeMs) Assert.Equal(4, c.Count);
        }
        Assert.Equal(1, c.Count);
        Assert.True(c.Fading(3, 2100f) && !c.Fading(0, 2100f));
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────────────────────────────────────────────

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln"))) dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    /// <summary>The code without its comment lines (a comment may name what the code must never do).</summary>
    private static string Code(string text)
        => string.Join("\n", text.Split('\n').Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    /// <summary>A method's body: from its signature to the next member at the same indent.</summary>
    private static string MethodBody(string text, string signature)
    {
        var at = text.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"{signature} not found");
        var end = text.IndexOf("\n    }", at, StringComparison.Ordinal);
        return Code(text[at..end]);
    }
}
