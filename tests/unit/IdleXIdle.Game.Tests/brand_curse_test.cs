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
                while (stack.Count > 0)
                {
                    var i = stack.Pop();
                    size++;
                    int x = i % n, y = i / n;
                    edge |= x == 0 || y == 0 || x == n - 1 || y == n - 1;
                    if (x > 0 && !cov[i - 1] && !seen[i - 1]) { seen[i - 1] = true; stack.Push(i - 1); }
                    if (x < n - 1 && !cov[i + 1] && !seen[i + 1]) { seen[i + 1] = true; stack.Push(i + 1); }
                    if (y > 0 && !cov[i - n] && !seen[i - n]) { seen[i - n] = true; stack.Push(i - n); }
                    if (y < n - 1 && !cov[i + n] && !seen[i + n]) { seen[i + n] = true; stack.Push(i + n); }
                }
                Assert.True(edge || size <= 60, $"depth {d} encloses a {size}-texel cell: a loop");
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
