using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleXIdle.Game.Presentation;
using IdleXIdle.Game.Presentation.Curse;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// BRAND's OFFLINE HOST BAKE (ADR-013 §3-4): every creature's idle strip has a <c>.brand.json</c> beside it, recomputed
/// here from the PNG with the game's own measuring and seating code. This test IS the bake tool: it fails when a committed
/// file is stale, and with <c>RH_BRAND_BAKE=1</c> it writes the files instead. <c>RH_BRAND_SEATS=&lt;png&gt;</c> renders a
/// contact sheet of every creature's seats (depth 1 red, depth 2 yellow, depth 3 cyan; the odd slot's first seat magenta)
/// for review.
/// </summary>
public class brand_host_bake_test
{
    private const string Source = "brand_host_bake_test (RH_BRAND_BAKE=1): CurseSeating.Bake over the idle strip and its .mark.json";

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    /// <summary>Every creature's idle strip (24 family cells and 6 bosses), by name, without the extension.</summary>
    internal static IReadOnlyList<string> IdleStrips()
        => new[] { "Enemies", "Bosses" }
            .SelectMany(f => Directory.GetDirectories(RepoFile("assets", "art", "Animations", f)).OrderBy(d => d, StringComparer.Ordinal))
            .Where(d => Path.GetFileName(d).EndsWith("_idle", StringComparison.Ordinal))
            .Select(d => Path.Combine(d, Path.GetFileName(d) + "_strip8_512"))
            .ToArray();

    private sealed record Decoded(Color[] Pixels, int Width, int Height, MarkPoints Points);

    /// <summary>A strip decoded on the CPU (premultiplied as the game premultiplies it) with its authored body points
    /// (decoded each time: 60 strips held at once are half a gigabyte).</summary>
    private static Decoded Strip(string path)
    {
        using var img = SixLaborsFree.Load(path + ".png");
        return new Decoded(img.Premultiplied(), img.Width, img.Height, MarkPoints.Parse(File.ReadAllText(path + ".mark.json")));
    }

    /// <summary>The creature's attack strip beside its idle one.</summary>
    internal static string AttackOf(string idlePath)
    {
        var name = Path.GetFileName(idlePath).Replace("_idle_", "_attack_", StringComparison.Ordinal);
        var dir = Path.GetFileName(Path.GetDirectoryName(idlePath)!).Replace("_idle", "_attack", StringComparison.Ordinal);
        return Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(idlePath)!)!, dir, name);
    }

    /// <summary>The bake of one creature, from its idle strip (and its attack strip, for the first seat).</summary>
    internal static CurseHostData Bake(string idlePath) => Baked.GetOrAdd(idlePath, BakeNow);

    private static readonly ConcurrentDictionary<string, CurseHostData> Baked = new();

    private static CurseHostData BakeNow(string idlePath)
    {
        var s = Strip(idlePath);
        var a = Strip(AttackOf(idlePath));
        return CurseSeating.Bake(s.Pixels, s.Width, s.Height, s.Points, a.Pixels, a.Width, a.Height, a.Points);
    }

    [Fact]
    public void test_every_creature_has_fresh_baked_host_data_beside_its_idle_strip()
    {
        // the committed file is exactly what the measuring and seating code makes of the PNG today: an edited strip, mark
        // point or rule that was not re-baked fails here (RH_BRAND_BAKE=1 re-bakes)
        var write = Environment.GetEnvironmentVariable("RH_BRAND_BAKE") == "1";
        var strips = IdleStrips();
        Assert.Equal(30, strips.Count);
        var stale = new List<string>();
        foreach (var path in strips)
        {
            var text = CurseHostData.Format(Bake(path), Source);
            var file = path + CurseHostData.Extension;
            if (write)
            {
                File.WriteAllText(file, text);
                continue;
            }
            if (!File.Exists(file) || File.ReadAllText(file).Replace("\r\n", "\n") != text) stale.Add(Path.GetFileName(file));
            // the file reads back to what was written
            Assert.Equal(text, CurseHostData.Format(CurseHostData.Parse(text), Source));
        }
        Assert.True(stale.Count == 0, $"stale BRAND host data (re-bake with RH_BRAND_BAKE=1): {string.Join(", ", stale)}");
        // the game ships them beside the strips, like the body points
        var csproj = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "IdleXIdle.Game.csproj"));
        Assert.Contains("<Content Include=\"..\\..\\assets\\art\\**\\*.brand.json\"", csproj);
        var loaded = CurseHostData.Load(RepoFile("assets", "art", "Animations"));
        Assert.Equal(30, loaded.Count);
        foreach (var path in strips) Assert.True(loaded.ContainsKey(Path.GetFileName(path)), $"{Path.GetFileName(path)} was not loaded");
    }

    [Fact]
    public void test_baking_twice_or_from_another_first_frame_gives_identical_seats()
    {
        // the seat is a function of the strip, never of which frame happened to be drawn first: the same pixels bake to
        // the same file, and the idle strip's frames (with their points) started from any frame bake to it too
        foreach (var path in IdleStrips().Where((_, i) => i % 7 == 3))
        {
            var once = CurseHostData.Format(BakeNow(path), Source);
            Assert.Equal(once, CurseHostData.Format(BakeNow(path), Source));
            var s = Strip(path);
            var a = Strip(AttackOf(path));
            var frames = s.Width / s.Height;
            var rolled = new Color[s.Pixels.Length];
            for (var y = 0; y < s.Height; y++)
                for (var f = 0; f < frames; f++)
                    Array.Copy(s.Pixels, y * s.Width + (f + 3) % frames * s.Height, rolled, y * s.Width + f * s.Height, s.Height);
            var points = new MarkPoints(Enumerable.Range(0, frames).Select(f => s.Points.Points[(f + 3) % frames]).ToArray(),
                                        s.Points.Heads.Count == frames ? Enumerable.Range(0, frames).Select(f => s.Points.Heads[(f + 3) % frames]).ToArray() : s.Points.Heads);
            var other = CurseSeating.Bake(rolled, s.Width, s.Height, points, a.Pixels, a.Width, a.Height, a.Points);
            var first = Bake(path);
            for (var p = 0; p < 2; p++)
            {
                Assert.Equal(first.Layouts[p].Available, other.Layouts[p].Available);
                for (var k = 0; k < first.Layouts[p].Available; k++)
                {
                    Assert.True(Vector2.Distance(first.Layouts[p].Offset[k], other.Layouts[p].Offset[k]) < 0.01f,
                                $"{Path.GetFileName(path)}: seat {k} moved with the first frame");
                    Assert.Equal(first.Layouts[p].Diameter[k], other.Layouts[p].Diameter[k], 3);
                }
            }
        }
    }

    [Fact]
    public void test_every_creature_holds_one_two_then_more_territories_by_depth()
    {
        // depth 1 one territory, depth 2 a second elsewhere, depth 3 as many as the body holds (three or four)
        Assert.Equal(0, CurseSeating.TerritoriesAt(0));
        Assert.Equal(1, CurseSeating.TerritoriesAt(1));
        Assert.Equal(2, CurseSeating.TerritoriesAt(2));
        Assert.Equal(CurseSeating.MaxTerritories, CurseSeating.TerritoriesAt(3));
        var thin = new List<string>();
        foreach (var path in IdleStrips())
        {
            var data = Bake(path);
            foreach (var lay in data.Layouts)
            {
                Assert.InRange(lay.Available, 1, CurseSeating.MaxTerritories);
                for (var k = 1; k < lay.Available; k++)
                    Assert.True(lay.Diameter[k] <= lay.Diameter[0] + 0.01f, $"{Path.GetFileName(path)}: a later territory larger than the first");
                if (lay.Available < 3) thin.Add(Path.GetFileName(path));
            }
        }
        // (KNOWN, carried from the prototype: the cinder gnat's slim body holds only its first territory; see the
        // production notes. Any other creature that falls under three is a regression)
        Assert.All(thin, n => Assert.StartsWith("cinder_swarm", n));
    }

    [Fact]
    public void test_the_first_territory_keeps_off_every_creatures_head_and_holds_its_stable_body()
    {
        // on every creature the first territory's whole disc keeps (almost) off the authored head of every frame, and its
        // centre is on the body in every idle frame's pose (never in the air beside a swaying limb)
        foreach (var path in IdleStrips())
        {
            var s = Strip(path);
            var data = Bake(path);
            var heads = s.Points.Heads.Select(hb => hb * data.FrameSize).ToArray();
            foreach (var lay in data.Layouts)
            {
                var c = data.Anchor + lay.Offset[0];
                var share = HeadShare(c, lay.Diameter[0], heads);
                // (its rim may brush a chin or a collar: the prototype's lay up to 31 % on a head)
                Assert.True(share <= 0.12f, $"{Path.GetFileName(path)}: the first territory lies {share:P0} on the head");
                var frames = s.Width / s.Height;
                var on = 0;
                for (var f = 0; f < frames; f++)
                    if (s.Pixels[(int)c.Y * s.Width + f * s.Height + (int)c.X].A >= 128) on++;
                Assert.True(on >= frames - 1, $"{Path.GetFileName(path)}: the first territory's centre is off the body in {frames - on} frames");
            }
        }
    }

    [Fact]
    public void test_every_later_territory_keeps_off_the_head_and_on_the_body_of_every_creature()
    {
        // depth 2 and 3 are what the player sees most: every seat after the first, in both slot layouts, on every one of
        // the 30 real creatures keeps (mostly) off the authored head of every frame, and its centre is on the body in the
        // idle frames (never on a skull, a swinging weapon or in the air). The rules that keep them so are pinned on
        // synthetic bodies (brand_curse_test); this pins what they make of the shipped art. (The cinder gnat seats only
        // its first territory: it has no later seat to check, see test_every_creature_holds_one_two_then_more_territories_by_depth.)
        var faults = new List<string>();
        var checkedSeats = 0;
        foreach (var path in IdleStrips())
        {
            var s = Strip(path);
            var data = Bake(path);
            var heads = s.Points.Heads.Select(hb => hb * data.FrameSize).ToArray();
            var frames = s.Width / s.Height;
            var name = Path.GetFileName(path);
            for (var p = 0; p < data.Layouts.Count; p++)
            {
                var lay = data.Layouts[p];
                for (var k = 1; k < lay.Available; k++)
                {
                    checkedSeats++;
                    var c = data.Anchor + lay.Offset[k];
                    var share = HeadShare(c, lay.Diameter[k], heads);
                    // (KNOWN, looked at on the seat sheet: the scarab's authored head box is a rectangle that takes in the front
                    // of its shell, and its second territory sits on that shell, the pronotum above the eyes: 21 %)
                    var limit = name.StartsWith("verdant_swarm_", StringComparison.Ordinal) && k == 1 ? ScarabShellShare : LaterHeadShare;
                    if (share > limit) faults.Add($"{name} parity {p} seat {k}: {share:P0} on the head");
                    var on = 0;
                    if (c.X >= 0f && c.Y >= 0f && c.X < s.Height && c.Y < s.Height)
                        for (var f = 0; f < frames; f++)
                            if (s.Pixels[(int)c.Y * s.Width + f * s.Height + (int)c.X].A >= 128) on++;
                    if (on < frames - 1) faults.Add($"{name} parity {p} seat {k}: its centre is off the body in {frames - on} of {frames} frames");
                }
            }
        }
        Assert.True(checkedSeats >= 29 * 2 * 2, $"only {checkedSeats} later seats were checked");
        Assert.True(faults.Count == 0, "later territories misplaced on the shipped creatures:\n" + string.Join("\n", faults));
    }

    /// <summary>The most of a later territory's disc that may lie on an authored head box (its rim may brush a chin, a
    /// collar or a hood's edge; never the skull itself).</summary>
    private const float LaterHeadShare = 0.2f;

    /// <summary>The verdant scarab's second territory: on its shell, inside its head box's rectangle (measured 0.21).</summary>
    private const float ScarabShellShare = 0.25f;

    [Fact]
    public void test_each_approved_host_keeps_its_material_on_its_baked_data()
    {
        // HOST MODE SELECTION (ADR-013 §3) on the creatures the owner approved it on (the parity stills): Ash-Burn on the
        // near-black and conflicting hosts, Shadow violet on the contrasting ones, the Spirit Matron's smoky blend between.
        // A re-bake that moves any of them to the other material fails here, not only in a diff of the JSON. The pins are
        // the committed files' (read as the game reads them), picked by their approved parity shots, never by a rule in
        // the game code (the curse knows no creature's name).
        var baked = CurseHostData.Load(RepoFile("assets", "art", "Animations"));
        CurseHostData Of(string idle) => baked[idle + "_idle_strip8_512"];
        foreach (var (idle, lo, hi, why) in new[]
                 {
                     ("void_reaper", 0.95f, 1f, "Ash-Burn (approved: cold ash on a blue-black robe)"),
                     ("umbral_swarm", 0.95f, 1f, "Ash-Burn (approved: the black whelp, ash on a near-black host)"),
                     ("crystal_lich", 0f, 0.1f, "Shadow violet (approved: a violet bruise on the chest)"),
                     ("forge_colossus", 0f, 0.1f, "Shadow violet (approved: violet against the orange lava)"),
                     ("choir_swarm", 0f, 0.1f, "Shadow violet (approved: the pale wisp, a dark bruise)"),
                     ("spirit_matron", 0.55f, 0.85f, "a blend (approved: the smoky drained ribcage)"),
                 })
        {
            var data = Of(idle);
            Assert.True(data.Burn >= lo && data.Burn <= hi, $"{idle}: host burn {data.Burn:0.00}, approved {why}");
            foreach (var lay in data.Layouts)
                for (var k = 0; k < lay.Available; k++)
                    Assert.True(lay.Burn[k] >= lo && lay.Burn[k] <= hi, $"{idle}: territory {k}'s burn {lay.Burn[k]:0.00}, approved {why}");
            // the burn is the host's own measure, never a value written in by hand: the bake gives it back from the look
            Assert.Equal(CurseHost.AshBurn(data.Look), data.Burn, 3);
        }
        // the whole near-black region reads in ash, as its whelp does (every umbral host)
        foreach (var (name, data) in baked.Where(e => e.Key.StartsWith("umbral_", StringComparison.Ordinal)))
            Assert.True(data.Burn >= 0.95f, $"{name}: an umbral host is no longer Ash-Burn ({data.Burn:0.00})");
    }

    [Fact]
    public void test_the_crystal_casters_first_territory_takes_its_chest_never_its_waist_cloth()
    {
        // REGRESSION (the owner's review, 2026-10-02): on the crystal lich depth 1 took the waist cloth hanging below its
        // sash (a robe's flared skirt is its thickest, most central mass) and read as part of the costume. Its authored
        // body point sits on the sash, so the first territory's centre must be above it, on the chest, and off the skull
        var path = IdleStrips().Single(p => Path.GetFileName(p).StartsWith("crystal_lich_idle", StringComparison.Ordinal));
        var data = Bake(path);
        foreach (var lay in data.Layouts)
        {
            Assert.True(lay.Offset[0].Y < -10f, $"the lich's first territory sits {lay.Offset[0].Y:0} px from its sash (below it is the waist cloth)");
            var heads = Strip(path).Points.Heads.Select(hb => hb * data.FrameSize).ToArray();
            Assert.True(HeadShare(data.Anchor + lay.Offset[0], lay.Diameter[0], heads) <= 0.1f, "the lich's first territory climbed onto its skull");
        }
    }

    [Fact]
    public void test_the_baked_data_is_read_once_and_looked_up_without_allocating()
    {
        // (the files are copied beside the binary, like the body points, and read once)
        CurseHostData.Warm();
        Assert.NotNull(CurseHostData.For("crystal_lich_idle_strip8_512"));
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            CurseHostData.For("crystal_lich_idle_strip8_512");
            CurseHostData.For("no_such_strip");
        }
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        // a broken or partial file is refused, never half-read
        var text = CurseHostData.Format(Bake(IdleStrips()[0]), Source);
        Assert.Throws<FormatException>(() => CurseHostData.Parse(text.Replace("\"available\": ", "\"available\": 9", StringComparison.Ordinal)));
    }

    [Fact]
    public void test_seat_sheet_renders_when_asked()
    {
        var path = Environment.GetEnvironmentVariable("RH_BRAND_SEATS");
        if (string.IsNullOrEmpty(path)) return;
        var all = IdleStrips();
        // (RH_BRAND_SEATS_ONLY=12,24: a close look at a few creatures, larger)
        var only = Environment.GetEnvironmentVariable("RH_BRAND_SEATS_ONLY");
        var pick = string.IsNullOrEmpty(only) ? Enumerable.Range(0, all.Count).ToArray() : only.Split(',').Select(int.Parse).ToArray();
        var strips = pick.Select(i => all[i]).ToArray();
        var cell = string.IsNullOrEmpty(only) ? 160 : 320;
        const int pad = 4;
        var cols = Math.Min(6, strips.Length);
        var rows = (strips.Length + cols - 1) / cols;
        int w = cols * (cell + pad) + pad, h = rows * (cell + pad) + pad;
        var rgba = new byte[w * h * 4];
        for (var i = 0; i < w * h; i++) { rgba[i * 4] = 20; rgba[i * 4 + 1] = 20; rgba[i * 4 + 2] = 24; rgba[i * 4 + 3] = 255; }
        for (var n = 0; n < strips.Length; n++)
        {
            var s = Strip(strips[n]);
            var data = SheetBake(strips[n]);
            int ox = pad + n % cols * (cell + pad), oy = pad + n / cols * (cell + pad);
            var k = s.Height / (float)cell;
            // frame 0 over a mid-dark ground, box-filtered down
            for (var y = 0; y < cell; y++)
                for (var x = 0; x < cell; x++)
                {
                    float r = 0, g = 0, b = 0;
                    var cnt = 0;
                    for (var sy = (int)(y * k); sy < (int)((y + 1) * k); sy++)
                        for (var sx = (int)(x * k); sx < (int)((x + 1) * k); sx++)
                        {
                            var c = s.Pixels[sy * s.Width + sx];
                            var inv = 1f - c.A / 255f;
                            r += c.R + 58 * inv; g += c.G + 56 * inv; b += c.B + 52 * inv;
                            cnt++;
                        }
                    Put(rgba, w, ox + x, oy + y, (byte)(r / cnt), (byte)(g / cnt), (byte)(b / cnt));
                }
            Vector2 ToSheet(Vector2 p) => new(ox + p.X / k, oy + p.Y / k);
            var even = data.Layouts[0];
            for (var t = even.Available - 1; t >= 0; t--)
            {
                var (cr, cg, cb) = t == 0 ? (255, 40, 40) : t == 1 ? (255, 220, 40) : (40, 220, 255);
                Ring(rgba, w, h, ToSheet(data.Anchor + even.Offset[t]), even.Diameter[t] * 0.5f / k, t == 0 ? 2f : 1.2f, (byte)cr, (byte)cg, (byte)cb);
            }
            var odd = data.Layouts[1];
            Ring(rgba, w, h, ToSheet(data.Anchor + odd.Offset[0]), odd.Diameter[0] * 0.5f / k, 1f, 255, 60, 255, dashed: true);
            var a = ToSheet(data.Anchor);
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++) Put(rgba, w, (int)a.X + dx, (int)a.Y + dy, 255, 255, 255);
            Digits(rgba, w, ox + 3, oy + 3, pick[n]);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        SixLaborsFree.Save(path, rgba, w, h);
        File.WriteAllLines(Path.ChangeExtension(path, ".txt"), strips.Select((p, i) =>
        {
            var d = SheetBake(p);
            var l = d.Layouts[0];
            var heads = Strip(p).Points.Heads.Select(hb => hb * d.FrameSize).ToArray();
            return $"{pick[i]}\t{Path.GetFileName(p)}\tburn={d.Burn:0.00}\tavail={l.Available}/{d.Layouts[1].Available}\td1=({l.Offset[0].X:0},{l.Offset[0].Y:0})"
                   + $"\thead={HeadShare(d.Anchor + l.Offset[0], l.Diameter[0], heads):0.00}/{HeadShare(d.Anchor + d.Layouts[1].Offset[0], d.Layouts[1].Diameter[0], heads):0.00}";
        }));
    }

    /// <summary>The sheet's bake: the shipped one, or with <c>RH_BRAND_SEATS_BEFORE=1</c> the prototype's rules (the idle
    /// silhouette alone, none of the first seat's extra evidence), for a before / after.</summary>
    private static CurseHostData SheetBake(string idlePath)
    {
        if (Environment.GetEnvironmentVariable("RH_BRAND_SEATS_BEFORE") != "1") return Bake(idlePath);
        var s = Strip(idlePath);
        var look = CurseHost.Measure(s.Pixels);
        var burn = CurseHost.AshBurn(look);
        var strip = HostStrip.From(s.Pixels, s.Width, s.Height, look, burn);
        return new CurseHostData(look, burn, strip.Size, strip.Frames, strip.Content(), s.Points.MeanPoint * strip.Size,
                                 CurseSeating.Seat(strip, s.Points, look, burn, false), CurseSeating.Seat(strip, s.Points, look, burn, true));
    }

    /// <summary>The share of a territory's disc over any frame's authored head box.</summary>
    internal static float HeadShare(Vector2 c, float diameter, IReadOnlyList<Vector4> heads)
    {
        int on = 0, all = 0;
        var r = diameter * 0.5f;
        for (var y = c.Y - r; y <= c.Y + r; y += 2f)
            for (var x = c.X - r; x <= c.X + r; x += 2f)
            {
                if ((x - c.X) * (x - c.X) + (y - c.Y) * (y - c.Y) > r * r) continue;
                all++;
                foreach (var hb in heads)
                    if (x > hb.X && x < hb.Z && y > hb.Y && y < hb.W) { on++; break; }
            }
        return all == 0 ? 0f : on / (float)all;
    }

    // ── SHEET DRAWING ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static void Put(byte[] rgba, int w, int x, int y, byte r, byte g, byte b)
    {
        if (x < 0 || y < 0 || x >= w || (y * w + x) * 4 >= rgba.Length) return;
        var i = (y * w + x) * 4;
        rgba[i] = r; rgba[i + 1] = g; rgba[i + 2] = b; rgba[i + 3] = 255;
    }

    private static void Ring(byte[] rgba, int w, int h, Vector2 c, float radius, float width, byte r, byte g, byte b, bool dashed = false)
    {
        for (var y = (int)(c.Y - radius - width - 1); y <= (int)(c.Y + radius + width + 1); y++)
            for (var x = (int)(c.X - radius - width - 1); x <= (int)(c.X + radius + width + 1); x++)
            {
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                if (MathF.Abs(d - radius) > width * 0.5f) continue;
                if (dashed && ((int)(MathF.Atan2(y - c.Y, x - c.X) * 6f) & 1) == 0) continue;
                Put(rgba, w, x, y, r, g, b);
            }
    }

    private static readonly string[] Font =
    {
        "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
        "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111",
    };

    private static void Digits(byte[] rgba, int w, int x, int y, int n)
    {
        var s = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        for (var i = 0; i < s.Length; i++)
        {
            var glyph = Font[s[i] - '0'];
            for (var gy = 0; gy < 5; gy++)
                for (var gx = 0; gx < 3; gx++)
                    if (glyph[gy * 3 + gx] == '1')
                        for (var py = 0; py < 2; py++)
                            for (var px = 0; px < 2; px++) Put(rgba, w, x + i * 8 + gx * 2 + px, y + gy * 2 + py, 255, 255, 255);
        }
    }
}
