using System;
using System.Collections.Generic;
using System.IO;
using IdleXIdle.Game.Presentation;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// BRAND as a curse, Concept A (Living Shadow Corruption, the owner's choice 2026-10-01): the corruption's STRUCTURE,
/// grown on the CPU exactly as the game uploads it. The picture is judged on film; these pin what film cannot count:
/// depth is reach (geometry, not light), no depth encloses a loop (the first A read "a net / a web"), and the light is
/// varied (some stretches dark, some lit), never a uniformly glowing network.
/// </summary>
public class brand_curse_test
{
    private static readonly CursePrototype.CorruptionAtlas Atlas = CursePrototype.CorruptionAtlas.Grow_(20261001);

    private static float Reach(int depth) => CursePrototype.CorruptionAtlas.Reach[depth] * Atlas.Longest;

    private static bool[] Covered(int depth)
    {
        var n = CursePrototype.CorruptionAtlas.N;
        var r = Reach(depth);
        var m = new bool[n * n];
        for (var i = 0; i < m.Length; i++)
            m[i] = (Atlas.Cover[i] > 0.3f && Atlas.Birth[i] <= r) || (Atlas.Blot[i] > 0.3f && Atlas.BlotBirth[i] <= r);
        return m;
    }

    [Fact]
    public void test_each_depth_reaches_further_along_its_paths()
    {
        var area = new int[4];
        for (var d = 1; d <= 3; d++)
            foreach (var c in Covered(d)) if (c) area[d]++;
        Assert.True(area[1] > 250, $"depth 1 covers {area[1]} texels: too small to read");
        Assert.True(area[2] >= area[1] * 1.5f, $"depth 2 covers {area[2]} against depth 1's {area[1]}");
        Assert.True(area[3] >= area[2] * 1.4f, $"depth 3 covers {area[3]} against depth 2's {area[2]}");
        Dump(area);
    }

    [Fact]
    public void test_no_depth_closes_a_loop_a_net_could_be_read_in()
    {
        // every empty region the corruption encloses (not joined to the border) must be a pinhole between overlapping
        // puffs, never a cell of a mesh
        var n = CursePrototype.CorruptionAtlas.N;
        for (var d = 1; d <= 3; d++)
        {
            var cov = Covered(d);
            var seen = new bool[n * n];
            for (var start = 0; start < cov.Length; start++)
            {
                if (cov[start] || seen[start]) continue;
                var stack = new Stack<int>();
                stack.Push(start);
                seen[start] = true;
                int size = 0;
                var edge = false;
                long sx = 0, sy = 0;
                while (stack.Count > 0)
                {
                    var i = stack.Pop();
                    size++;
                    int x = i % n, y = i / n;
                    sx += x;
                    sy += y;
                    edge |= x == 0 || y == 0 || x == n - 1 || y == n - 1;
                    if (x > 0 && !cov[i - 1] && !seen[i - 1]) { seen[i - 1] = true; stack.Push(i - 1); }
                    if (x < n - 1 && !cov[i + 1] && !seen[i + 1]) { seen[i + 1] = true; stack.Push(i + 1); }
                    if (y > 0 && !cov[i - n] && !seen[i - n]) { seen[i - n] = true; stack.Push(i - n); }
                    if (y < n - 1 && !cov[i + n] && !seen[i + n]) { seen[i + n] = true; stack.Push(i + n); }
                }
                Assert.True(edge || size <= 60, $"depth {d} encloses a {size}-texel cell round {sx / Math.Max(1, size)},{sy / Math.Max(1, size)}: a loop");
            }
        }
    }

    [Fact]
    public void test_the_light_is_varied_some_stretches_dark_some_lit()
    {
        var n = CursePrototype.CorruptionAtlas.N;
        var r = Reach(3);
        int veins = 0, lit = 0;
        for (var i = 0; i < n * n; i++)
        {
            if (Atlas.Cover[i] < 0.6f || Atlas.Birth[i] > r) continue;
            veins++;
            if (Atlas.Glow[i] > 0.25f) lit++;
        }
        var share = lit / (float)veins;
        Assert.InRange(share, 0.15f, 0.6f);
    }

    [Fact]
    public void test_the_light_runs_along_the_veins_never_in_round_beads()
    {
        // every lit piece (a run of light, a patch's leak) is ELONGATED: its spread along its long axis at least twice its
        // spread across it. Round, evenly spaced knots read as "a string of beads" (owner's final reads, d2885245)
        var n = CursePrototype.CorruptionAtlas.N;
        var r = Reach(3);
        var lit = new bool[n * n];
        for (var i = 0; i < lit.Length; i++) lit[i] = Atlas.Birth[i] <= r && Atlas.Glow[i] > 0.3f;
        var seen = new bool[n * n];
        int pieces = 0, round = 0;
        var worst = new List<string>();
        for (var start = 0; start < lit.Length; start++)
        {
            if (!lit[start] || seen[start]) continue;
            var members = Component(lit, seen, start, n);
            if (members.Count < 6) continue;
            pieces++;
            double mx = 0, my = 0;
            foreach (var i in members) { mx += i % n; my += i / n; }
            mx /= members.Count; my /= members.Count;
            double sxx = 0, syy = 0, sxy = 0;
            foreach (var i in members) { double dx = i % n - mx, dy = i / n - my; sxx += dx * dx; syy += dy * dy; sxy += dx * dy; }
            var tr = sxx + syy;
            var det = sxx * syy - sxy * sxy;
            var disc = Math.Sqrt(Math.Max(0, tr * tr / 4 - det));
            var l1 = tr / 2 + disc;
            var l2 = Math.Max(1e-6, tr / 2 - disc);
            var ratio = Math.Sqrt(l1 / l2);
            if (ratio < 2.0) { round++; worst.Add($"{members.Count}px@{mx:0},{my:0} x{ratio:0.0}"); }
        }
        Assert.True(pieces >= 8, $"only {pieces} lit pieces");
        Assert.True(round <= pieces * 0.25, $"{round} of {pieces} lit pieces are round (beads): {string.Join("; ", worst)}");
    }

    [Fact]
    public void test_no_single_vein_runs_the_length_of_the_body_a_seam_could_be_read_in()
    {
        // the field's long axis is the texture's Y (a tall body keeps it; a wide body is turned a quarter): no one
        // connected vein may span more than 60 % of the whole corruption along it (one line down a robe read as a zipper,
        // a seam, a sash); the corruption is territories joined by broken paths
        var n = CursePrototype.CorruptionAtlas.N;
        var r = Reach(3);
        var vein = new bool[n * n];
        int ymin = n, ymax = -1;
        for (var i = 0; i < vein.Length; i++)
        {
            vein[i] = Atlas.Cover[i] > 0.3f && Atlas.Birth[i] <= r;
            var any = vein[i] || (Atlas.Blot[i] > 0.3f && Atlas.BlotBirth[i] <= r);
            if (any) { ymin = Math.Min(ymin, i / n); ymax = Math.Max(ymax, i / n); }
        }
        var span = ymax - ymin;
        var seen = new bool[n * n];
        var longest = 0;
        for (var start = 0; start < vein.Length; start++)
        {
            if (!vein[start] || seen[start]) continue;
            var members = Component(vein, seen, start, n);
            int lo = n, hi = -1;
            foreach (var i in members) { lo = Math.Min(lo, i / n); hi = Math.Max(hi, i / n); }
            longest = Math.Max(longest, hi - lo);
        }
        Assert.True(longest <= span * 0.6f, $"one vein spans {longest} of the corruption's {span} texels along the body");
    }

    [Fact]
    public void test_depth_one_holds_a_dark_patch_and_a_violet_leak()
    {
        // depth 1 is small but never faint: a dark infected patch (it reads on a pale body) with violet light in it and
        // on its veinlets (it reads on a black one)
        var n = CursePrototype.CorruptionAtlas.N;
        var r = Reach(1);
        int dark = 0, light = 0;
        var emit = Atlas.EmitField(-1f, r, -1);
        for (var i = 0; i < n * n; i++)
        {
            if (Atlas.Blot[i] > 0.5f && Atlas.BlotBirth[i] <= r) dark++;
            if (emit[i] > 0.3f) light++;
        }
        Assert.True(dark >= 150, $"depth 1's dark patch covers {dark} texels");
        Assert.True(light >= 120, $"depth 1's violet light covers {light} texels");
    }

    [Fact]
    public void test_the_corruption_is_a_region_never_a_band_a_garment_could_be_read_in()
    {
        // at depth 2 and 3 the corruption spreads in two dimensions round its entry: its principal axes stay within
        // 1.6 : 1 (three territories ~120 degrees apart measure 1.3). Territories laid along one line through the pocket read as clothing on a torso whatever the bearing
        // ("a zipper", "a seam" upright, "a sash" shoulder to hip, "a belt" level: fresh reads, 2026-10-01; the
        // d2885245 field measured 3.7 : 1)
        var n = CursePrototype.CorruptionAtlas.N;
        for (var d = 2; d <= 3; d++)
        {
            double sx = 0, sy = 0, count = 0;
            var cov = Covered(d);
            for (var i = 0; i < cov.Length; i++) if (cov[i]) { sx += i % n; sy += i / n; count++; }
            var mx = sx / count;
            var my = sy / count;
            double xx = 0, yy = 0, xy = 0;
            for (var i = 0; i < cov.Length; i++)
            {
                if (!cov[i]) continue;
                double dx = i % n - mx, dy = i / n - my;
                xx += dx * dx; yy += dy * dy; xy += dx * dy;
            }
            var tr = xx + yy;
            var disc = Math.Sqrt(Math.Max(0, tr * tr / 4 - (xx * yy - xy * xy)));
            var ratio = Math.Sqrt((tr / 2 + disc) / Math.Max(1e-6, tr / 2 - disc));
            Assert.True(ratio <= 1.6, $"depth {d}: the corruption is a band ({ratio:0.00} : 1)");
        }
    }

    [Fact]
    public void test_the_pocket_moves_off_a_thin_limb_into_the_bodys_mass()
    {
        // a torso and a thin arm reaching out of it: a pocket landing on the arm moves into the torso (on a wrist, a hand
        // or a weapon the curse read as the creature's own magic, "a bracelet": fresh reads at depth 1, 2026-10-01); a
        // pocket already deep in the body stays where it is
        const int w = 140, h = 100;
        var alpha = new byte[w * h];
        void Solid(int x0, int y0, int x1, int y1)
        {
            for (var y = y0; y < y1; y++)
                for (var x = x0; x < x1; x++) alpha[y * w + x] = 255;
        }
        Solid(20, 10, 80, 90);    // the torso
        Solid(80, 40, 130, 48);   // the arm
        var fromArm = CursePrototype.ThickNear(alpha, w, h, new Microsoft.Xna.Framework.Vector2(120, 44), 0.6f);
        Assert.InRange(fromArm.X, 20f, 80f);
        Assert.InRange(fromArm.Y, 10f, 90f);
        var inTorso = new Microsoft.Xna.Framework.Vector2(50, 50);
        Assert.Equal(inTorso, CursePrototype.ThickNear(alpha, w, h, inTorso, 0.6f));
    }

    private static List<int> Component(bool[] mask, bool[] seen, int start, int n)
    {
        var members = new List<int>();
        var stack = new Stack<int>();
        stack.Push(start);
        seen[start] = true;
        while (stack.Count > 0)
        {
            var i = stack.Pop();
            members.Add(i);
            int x = i % n, y = i / n;
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    int px = x + dx, py = y + dy;
                    if (px < 0 || py < 0 || px >= n || py >= n) continue;
                    var j = py * n + px;
                    if (mask[j] && !seen[j]) { seen[j] = true; stack.Push(j); }
                }
        }
        return members;
    }

    /// <summary>RH_CURSE_DUMP=&lt;dir&gt;: the fields as PGM images, for looking at the structure alone.</summary>
    private static void Dump(int[] area)
    {
        var dir = Environment.GetEnvironmentVariable("RH_CURSE_DUMP");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        var n = CursePrototype.CorruptionAtlas.N;
        for (var d = 1; d <= 3; d++)
        {
            Write(Path.Combine(dir, $"dark{d}.pgm"), Atlas.DarkField(-1f, Reach(d)), n);
            Write(Path.Combine(dir, $"emit{d}.pgm"), Atlas.EmitField(-1f, Reach(d), -1), n);
        }
        File.WriteAllText(Path.Combine(dir, "area.txt"), string.Join(",", area));
        for (var d = 1; d <= 3; d++)
        {
            var vein = new float[n * n];
            var blot = new float[n * n];
            for (var i = 0; i < n * n; i++)
            {
                vein[i] = Atlas.Cover[i] > 0.3f && Atlas.Birth[i] <= Reach(d) ? 1f : 0f;
                blot[i] = Atlas.Blot[i] > 0.3f && Atlas.BlotBirth[i] <= Reach(d) ? 1f : 0f;
            }
            Write(Path.Combine(dir, $"vein{d}.pgm"), vein, n);
            Write(Path.Combine(dir, $"blot{d}.pgm"), blot, n);
        }
    }

    private static void Write(string path, float[] v, int n)
    {
        using var f = File.Create(path);
        var head = System.Text.Encoding.ASCII.GetBytes($"P5\n{n} {n}\n255\n");
        f.Write(head);
        var px = new byte[v.Length];
        for (var i = 0; i < v.Length; i++) px[i] = (byte)Math.Clamp((int)(v[i] * 255f), 0, 255);
        f.Write(px);
    }
}
