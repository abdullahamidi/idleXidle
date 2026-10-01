using System;
using System.Collections.Generic;
using System.IO;
using IdleXIdle.Game.Presentation;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// BRAND as a curse, Concept A (Living Shadow Corruption), the TERRITORY pass (owner's brief from 9aa407fd): the curse is
/// separate regions of the body becoming corrupted, never a vein network. These pin what film cannot count: depth is how
/// many territories the body carries; territories sit apart and never in a row; no corruption feature runs long (a long
/// line followed a collar and read as "a sash"); a territory is a region, never a band; its light runs in tapered pieces,
/// never beads; the host's own material is drained of its colour and keeps its shading; the host's look is measured.
/// </summary>
public class brand_curse_test
{
    private static readonly CursePrototype.TerritoryAtlas Atlas = CursePrototype.TerritoryAtlas.Grow_(20261002);
    private const int N = CursePrototype.TerritoryAtlas.N;

    [Fact]
    public void test_each_depth_infects_more_territories_of_the_body()
    {
        // one infected territory, then a second elsewhere, then as many as the body holds (three or four)
        Assert.Equal(1, CursePrototype.TerritoriesAt(1));
        Assert.Equal(2, CursePrototype.TerritoriesAt(2));
        Assert.True(CursePrototype.TerritoriesAt(3) >= 3);
        var (alpha, w, h) = TallBody();
        var seats = CursePrototype.TerritorySeats(alpha, w, h, new Vector2(78, 80), Diameters(50f), headAtLeft: true);
        Assert.True(seats.Count >= 3, $"a tall body holds only {seats.Count} territories");
        var (wa, ww, wh) = WideBody();
        var wide = CursePrototype.TerritorySeats(wa, ww, wh, new Vector2(120, 58), Diameters(34f), headAtLeft: true);
        Assert.True(wide.Count >= 3, $"a wide body holds only {wide.Count} territories");
        Dump();
    }

    [Fact]
    public void test_a_thin_body_still_shows_more_territories_at_depth_three()
    {
        // a skeleton's narrow ribcage and pelvis under a hood: the strict rules hold two; the gentler second pass
        // finds a third, so depth 3 always shows more of the body than depth 2
        const int w = 140, h = 220;
        var a = new byte[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var hood = Sq(x - 70) + Sq(y - 30) < 24 * 24;
                var ribs = y >= 54 && y < 120 && MathF.Abs(x - 70) < 18;
                var pelvis = y >= 120 && y < 150 && MathF.Abs(x - 70) < 22;
                var legs = y >= 150 && y < 220 && (MathF.Abs(x - 58) < 5 || MathF.Abs(x - 82) < 5);
                var arms = y >= 70 && y < 76 && x >= 10 && x < 130;
                if (hood || ribs || pelvis || legs || arms) a[y * w + x] = 255;
            }
        var seats = CursePrototype.TerritorySeats(a, w, h, new Vector2(74, 100), Diameters(34f), headAtLeft: true);
        Assert.True(seats.Count >= 3, $"a thin body holds only {seats.Count} territories at depth 3");
    }

    [Fact]
    public void test_the_first_territory_takes_hold_where_the_host_visibly_changes()
    {
        // a torso whose left half is a dark sash the drain barely changes and whose right half is coloured cloth: the
        // first territory takes hold in the cloth, within its reach of where the curse entered (on a dark sash or an
        // inner tunic the patch read as part of the costume)
        var (alpha, w, h) = TallBody();
        var change = new byte[w * h];
        for (var i = 0; i < change.Length; i++) change[i] = (byte)(i % w < 80 ? 10 : 200);
        var seats = CursePrototype.TerritorySeats(alpha, w, h, new Vector2(78, 90), Diameters(50f), headAtLeft: true, change);
        Assert.True(seats[0].X > 80f, $"the first territory sat on the unchanging half at {seats[0]}");
        Assert.True(Vector2.Distance(seats[0], new Vector2(78, 90)) <= 1.3f * 50f + 2f, "the first territory wandered off");
        // colour lost counts more than brightness shifted: pale bone darkened is a smaller change than red cloth greyed
        var bone = CursePrototype.Visible(new Color(220, 210, 190), new Color(150, 146, 150));
        var cloth = CursePrototype.Visible(new Color(170, 30, 36), new Color(96, 92, 100));
        Assert.True(cloth > bone, $"greyed cloth ({cloth}) changed less visibly than darkened bone ({bone})");
    }

    [Fact]
    public void test_territories_sit_apart_and_never_in_a_row()
    {
        // each territory is ELSEWHERE on the body: a gap clear of every other; on an upright body three never in a line
        // (a band through them read as "a sash" again); none on the head band or the feet
        foreach (var (alpha, w, h, first, d0, tall) in new[]
                 {
                     (TallBody().Alpha, TallBody().W, TallBody().H, new Vector2(78, 80), 50f, true),
                     (WideBody().Alpha, WideBody().W, WideBody().H, new Vector2(120, 58), 34f, false),
                 })
        {
            var d = Diameters(d0);
            var seats = CursePrototype.TerritorySeats(alpha, w, h, first, d, headAtLeft: true);
            for (var i = 0; i < seats.Count; i++)
                for (var j = i + 1; j < seats.Count; j++)
                {
                    var gap = Vector2.Distance(seats[i], seats[j]) / (0.5f * (d[i] + d[j]));
                    Assert.True(gap >= 0.94f, $"territories {i} and {j} overlap ({gap:0.00} diameters apart)");
                }
            // (on an upright body: a wide beast's body is itself the row)
            if (tall && seats.Count >= 3)
            {
                var (a, b, c) = (seats[0], seats[1], seats[2]);
                var area = MathF.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) * 0.5f;
                var side = MathF.Max(Vector2.DistanceSquared(a, b), MathF.Max(Vector2.DistanceSquared(a, c), Vector2.DistanceSquared(b, c)));
                Assert.True(area / side >= 0.09f, $"three territories in a row ({area / side:0.00})");
            }
            foreach (var s in seats)
            {
                // (the hem and the feet: the lowest fifth of the tall body, y > 156; the legs: the wide one's lowest quarter)
                if (tall) Assert.True(s.Y < 157f, $"a territory on the hem at {s}");
                else Assert.True(s.Y < 92f, $"a territory on the legs at {s}");
                // (the head: y 6..34 on the tall body, x 8..48 on the wide one; the tall body's arms reach past its core
                // column, x 52..108)
                if (tall) Assert.True(s.Y > 34f, $"a territory on the head at {s}");
                if (tall) Assert.InRange(s.X, 50f, 110f);
                else Assert.True(s.X > 48f, $"a territory on the head at {s}");
            }
        }
    }

    [Fact]
    public void test_no_corruption_feature_runs_long()
    {
        // every vein fragment is short (under a third of its territory's span) and lies inside its infected region: a
        // long line followed a collar, a robe edge or a belt and read as "a sash", "a stole", "embroidery"
        for (var v = 0; v < CursePrototype.TerritoryAtlas.Variants; v++)
        {
            var span = Span(Atlas.TissueF[v], 0.3f);
            Assert.NotEmpty(Atlas.Fragments[v]);
            foreach (var len in Atlas.Fragments[v])
                Assert.True(len <= span / 3f, $"variant {v}: a fragment {len:0} texels long in a territory {span:0} across");
            for (var i = 0; i < N * N; i++)
                if (Atlas.VeinF[v][i] > 0.5f)
                    Assert.True(Atlas.DrainF[v][i] > 0.1f, $"variant {v}: a vein outside its territory at {i % N},{i / N}");
        }
    }

    [Fact]
    public void test_a_territory_is_a_region_never_a_band()
    {
        // the infected tissue spreads in two dimensions: principal axes within 1.6 : 1 (a band on a torso reads as a
        // garment whatever its angle)
        for (var v = 0; v < CursePrototype.TerritoryAtlas.Variants; v++)
        {
            var ratio = Elongation(Atlas.TissueF[v], 0.3f);
            Assert.True(ratio <= 1.6, $"variant {v}: the territory is a band ({ratio:0.00} : 1)");
        }
    }

    [Fact]
    public void test_the_light_runs_in_tapered_pieces_never_round_beads()
    {
        // every lit run along a fragment is ELONGATED (round, evenly spaced knots read as "a string of beads")
        int pieces = 0, round = 0;
        var worst = new List<string>();
        for (var v = 0; v < CursePrototype.TerritoryAtlas.Variants; v++)
        {
            var lit = new bool[N * N];
            for (var i = 0; i < lit.Length; i++) lit[i] = Atlas.RunF[v][i] > 0.3f;
            var seen = new bool[N * N];
            for (var start = 0; start < lit.Length; start++)
            {
                if (!lit[start] || seen[start]) continue;
                var members = Component(lit, seen, start);
                if (members.Count < 6) continue;
                pieces++;
                var ratio = Elongation(members);
                if (ratio < 2.0) { round++; worst.Add($"v{v} {members.Count}px x{ratio:0.0}"); }
            }
        }
        Assert.True(pieces >= 6, $"only {pieces} lit runs");
        Assert.True(round <= pieces * 0.25, $"{round} of {pieces} lit runs are round (beads): {string.Join("; ", worst)}");
    }

    [Fact]
    public void test_a_territory_holds_dark_tissue_and_light_from_inside()
    {
        // depth 1 is ONE territory and must read on its own: infected tissue, dark fragments, a restrained light from
        // inside, and no light outside the region (the glow supports the region, it never outlines it as a decal)
        for (var v = 0; v < CursePrototype.TerritoryAtlas.Variants; v++)
        {
            int tissue = 0, dark = 0, lit = 0, soft = 0;
            for (var i = 0; i < N * N; i++)
            {
                if (Atlas.TissueF[v][i] > 0.3f) tissue++;
                if (Atlas.DarkF[v][i] > 0.3f) dark++;
                if (Atlas.EmitF[v][i] > 0.3f) lit++;
                if (Atlas.EmitF[v][i] > 0.1f) soft++;
                if (Atlas.EmitF[v][i] > 0.1f) Assert.True(Atlas.DrainF[v][i] > 0.02f, $"variant {v}: light outside its territory");
            }
            Assert.True(tissue >= 2000, $"variant {v}: the tissue covers {tissue} texels");
            Assert.True(dark >= 150, $"variant {v}: the dark covers {dark} texels");
            // (restrained: a few brighter cracks and runs, and a soft light through the region; a full glow read as
            // "a purple patch")
            // (thin violet fissures: the brief allows one or two short fragments, so a few dozen bright texels)
            Assert.True(lit >= 60, $"variant {v}: the bright light covers {lit} texels");
            Assert.True(soft >= 400, $"variant {v}: the soft light covers {soft} texels");
            Assert.True(lit <= tissue / 4, $"variant {v}: the light floods the region ({lit} of {tissue})");
        }
    }

    [Fact]
    public void test_the_bloom_grows_outward_and_ends_on_the_whole_region()
    {
        // the bloom's first frame holds less of the region than its middle one, and its last reveals every texel (the
        // settled masks then follow without a pop); every mask is empty at the field's border (the drain samples clamp)
        var frames = CursePrototype.TerritoryAtlas.Frames;
        for (var v = 0; v < CursePrototype.TerritoryAtlas.Variants; v++)
        {
            double first = 0, middle = 0, last = 0, all = 0;
            for (var i = 0; i < N * N; i++)
            {
                var t = Atlas.TissueF[v][i];
                all += t;
                first += t * CursePrototype.TerritoryAtlas.RevealAt(1f / frames * CursePrototype.TerritoryAtlas.BloomReach, Atlas.BirthF[v][i]);
                middle += t * CursePrototype.TerritoryAtlas.RevealAt(frames / 2f / frames * CursePrototype.TerritoryAtlas.BloomReach, Atlas.BirthF[v][i]);
                last += t * CursePrototype.TerritoryAtlas.RevealAt(CursePrototype.TerritoryAtlas.BloomReach, Atlas.BirthF[v][i]);
            }
            Assert.True(first < middle * 0.6, $"variant {v}: the bloom does not grow ({first:0} then {middle:0})");
            Assert.True(last >= all * 0.999, $"variant {v}: the bloom's last frame leaves {all - last:0.0} of the region unborn");
            for (var k = 0; k < N; k++)
                foreach (var i in new[] { k, (N - 1) * N + k, k * N, k * N + N - 1 })
                {
                    Assert.Equal(0f, Atlas.TissueF[v][i]);
                    Assert.True(Atlas.DrainF[v][i] < 0.004f, $"variant {v}: the drain reaches the border");
                    Assert.True(Atlas.EmitF[v][i] < 0.004f, $"variant {v}: the light reaches the border");
                }
        }
    }

    [Fact]
    public void test_the_drained_material_loses_its_colour_and_keeps_its_shading()
    {
        // an infected territory DRAINS the host: its native colour goes (a red robe greys), its folds stay (a darker pixel
        // stays darker), it darkens; on a host already rich in violet the tint is ash, never more violet
        var mid = new CursePrototype.HostLook(0.4f, 0.3f, 0f);
        var red = CursePrototype.DrainPixel(new Color(200, 40, 40), mid);
        Assert.True(Chroma(red) <= Chroma(new Color(200, 40, 40)) * 0.25f, $"the red kept its colour: {red}");
        var lo = CursePrototype.DrainPixel(new Color(60, 60, 60), mid);
        var hi = CursePrototype.DrainPixel(new Color(160, 160, 160), mid);
        Assert.True(Luma(hi) > Luma(lo) + 20f, "the folds flattened away");
        Assert.True(Luma(hi) < 160f, "the drained material did not darken");
        // the drained material moves AWAY from the host's own brightness: a dark host lightens, a pale one darkens
        Assert.True(CursePrototype.DrainedMean(0.1f) > 0.25f, "a dark host's infected flesh did not turn to ash");
        Assert.True(CursePrototype.DrainedMean(0.27f) > 0.32f, "a dark caster's infected cloth did not pale");
        Assert.True(CursePrototype.DrainedMean(0.62f) < 0.4f, "a pale host's infected cloth did not darken");
        var violetHost = new CursePrototype.HostLook(0.2f, 0.3f, 0.5f);
        var purple = CursePrototype.DrainPixel(new Color(120, 60, 170), violetHost);
        Assert.True(purple.B - purple.G <= 6, $"a violet host's drain handed the violet back: {purple}");
        // a near-black host's infected flesh turns to ASH: lighter, grey, its folds kept
        var black = new CursePrototype.HostLook(0.08f, 0.04f, 0f);
        var ashLo = CursePrototype.DrainPixel(new Color(8, 6, 12), black);
        var ashHi = CursePrototype.DrainPixel(new Color(60, 40, 80), black);
        Assert.True(Luma(ashLo) > 40f, $"black flesh did not turn to ash: {ashLo}");
        Assert.True(Luma(ashHi) > Luma(ashLo), "the ash lost the folds");
        Assert.True(Chroma(ashHi) < 25f, $"the ash kept the host's violet: {ashHi}");
        var transparent = CursePrototype.DrainPixel(Color.Transparent, mid);
        Assert.Equal(0, transparent.A);
        Assert.True(CursePrototype.DrainStrength(new CursePrototype.HostLook(0.4f, 0.35f, 0f)) > CursePrototype.DrainStrength(new CursePrototype.HostLook(0.4f, 0.03f, 0f)),
                    "a colourful host is drained no more visibly than a grey one");
    }

    [Fact]
    public void test_the_host_look_measures_brightness_colour_and_native_violet()
    {
        var purple = Fill(new Color(110, 50, 170), 100);
        var red = Fill(new Color(180, 30, 30), 100);
        var black = Fill(new Color(12, 10, 14), 100);
        Assert.True(CursePrototype.Measure(purple).Violet > 0.9f);
        Assert.True(CursePrototype.Measure(red).Violet < 0.05f);
        Assert.True(CursePrototype.Measure(black).Chroma < 0.05f);
        Assert.True(CursePrototype.Measure(black).Luma < 0.1f);
        Assert.True(CursePrototype.VioletNorm(CursePrototype.Measure(purple).Violet) > 0.95f);
    }

    [Fact]
    public void test_the_pocket_moves_off_a_thin_limb_into_the_bodys_mass()
    {
        // a torso and a thin arm reaching out of it: a pocket landing on the arm moves into the torso (on a wrist, a hand
        // or a weapon the curse read as the creature's own magic, "a bracelet"); a pocket already deep stays
        const int w = 140, h = 100;
        var alpha = new byte[w * h];
        void Solid(int x0, int y0, int x1, int y1)
        {
            for (var y = y0; y < y1; y++)
                for (var x = x0; x < x1; x++) alpha[y * w + x] = 255;
        }
        Solid(20, 10, 80, 90);    // the torso
        Solid(80, 40, 130, 48);   // the arm
        var fromArm = CursePrototype.ThickNear(alpha, w, h, new Vector2(120, 44), 0.6f);
        Assert.InRange(fromArm.X, 20f, 80f);
        Assert.InRange(fromArm.Y, 10f, 90f);
        var inTorso = new Vector2(50, 50);
        Assert.Equal(inTorso, CursePrototype.ThickNear(alpha, w, h, inTorso, 0.6f));
    }

    // ── FIXTURES ──────────────────────────────────────────────────────────────────────────────────────────────────────

    private static float[] Diameters(float d0) => new[] { d0, d0 * 0.82f, d0 * 0.72f, d0 * 0.64f };

    /// <summary>A tall robed body: a head, a torso widening into a skirt, two thin arms, two legs.</summary>
    private static (byte[] Alpha, int W, int H) TallBody()
    {
        const int w = 160, h = 200;
        var a = new byte[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var head = Sq(x - 80) + Sq(y - 20) < 14 * 14;
                var half = 26 + (y - 34) * 0.18f;
                var torso = y >= 34 && y < 165 && MathF.Abs(x - 80) < half;
                var arms = y >= 60 && y < 67 && x >= 20 && x < 140;
                var legs = y >= 165 && y < 200 && (MathF.Abs(x - 66) < 6 || MathF.Abs(x - 94) < 6);
                if (head || torso || arms || legs) a[y * w + x] = 255;
            }
        return (a, w, h);
    }

    /// <summary>A wide four-legged body facing left: a head at the left, a long torso, thin legs.</summary>
    private static (byte[] Alpha, int W, int H) WideBody()
    {
        const int w = 220, h = 120;
        var a = new byte[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var head = Sq(x - 28) + Sq(y - 40) < 20 * 20;
                var torso = Sq((x - 118) / 82f) + Sq((y - 56) / 30f) < 1f;
                var legs = y >= 70 && y < 116 && (MathF.Abs(x - 62) < 4 || MathF.Abs(x - 92) < 4 || MathF.Abs(x - 150) < 4 || MathF.Abs(x - 178) < 4);
                if (head || torso || legs) a[y * w + x] = 255;
            }
        return (a, w, h);
    }

    private static Color[] Fill(Color c, int n)
    {
        var a = new Color[n];
        Array.Fill(a, c);
        return a;
    }

    private static float Sq(float x) => x * x;

    private static float Luma(Color c) => 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;

    private static float Chroma(Color c) => Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));

    /// <summary>The widest extent of a field's region (texels), along x or y.</summary>
    private static float Span(float[] f, float at)
    {
        int minX = N, maxX = -1, minY = N, maxY = -1;
        for (var i = 0; i < f.Length; i++)
        {
            if (f[i] <= at) continue;
            minX = Math.Min(minX, i % N); maxX = Math.Max(maxX, i % N); minY = Math.Min(minY, i / N); maxY = Math.Max(maxY, i / N);
        }
        return Math.Max(maxX - minX, maxY - minY);
    }

    private static double Elongation(float[] f, float at)
    {
        var members = new List<int>();
        for (var i = 0; i < f.Length; i++) if (f[i] > at) members.Add(i);
        return Elongation(members);
    }

    private static double Elongation(List<int> members)
    {
        double mx = 0, my = 0;
        foreach (var i in members) { mx += i % N; my += i / N; }
        mx /= members.Count;
        my /= members.Count;
        double sxx = 0, syy = 0, sxy = 0;
        foreach (var i in members) { double dx = i % N - mx, dy = i / N - my; sxx += dx * dx; syy += dy * dy; sxy += dx * dy; }
        var tr = sxx + syy;
        var disc = Math.Sqrt(Math.Max(0, tr * tr / 4 - (sxx * syy - sxy * sxy)));
        return Math.Sqrt((tr / 2 + disc) / Math.Max(1e-6, tr / 2 - disc));
    }

    private static List<int> Component(bool[] mask, bool[] seen, int start)
    {
        var members = new List<int>();
        var stack = new Stack<int>();
        stack.Push(start);
        seen[start] = true;
        while (stack.Count > 0)
        {
            var i = stack.Pop();
            members.Add(i);
            int x = i % N, y = i / N;
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    int px = x + dx, py = y + dy;
                    if (px < 0 || py < 0 || px >= N || py >= N) continue;
                    var j = py * N + px;
                    if (mask[j] && !seen[j]) { seen[j] = true; stack.Push(j); }
                }
        }
        return members;
    }

    /// <summary>RH_CURSE_DUMP=&lt;dir&gt;: each territory variant's fields as PGM images, for looking at them alone.</summary>
    private static void Dump()
    {
        var dir = Environment.GetEnvironmentVariable("RH_CURSE_DUMP");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        for (var v = 0; v < CursePrototype.TerritoryAtlas.Variants; v++)
        {
            Write(Path.Combine(dir, $"tissue{v}.pgm"), Atlas.TissueF[v]);
            Write(Path.Combine(dir, $"drain{v}.pgm"), Atlas.DrainF[v]);
            Write(Path.Combine(dir, $"dark{v}.pgm"), Atlas.DarkF[v]);
            Write(Path.Combine(dir, $"emit{v}.pgm"), Atlas.EmitF[v]);
        }
    }

    private static void Write(string path, float[] v)
    {
        using var f = File.Create(path);
        f.Write(System.Text.Encoding.ASCII.GetBytes($"P5\n{N} {N}\n255\n"));
        var px = new byte[v.Length];
        for (var i = 0; i < v.Length; i++) px[i] = (byte)Math.Clamp((int)(v[i] * 255f), 0, 255);
        f.Write(px);
    }
}
