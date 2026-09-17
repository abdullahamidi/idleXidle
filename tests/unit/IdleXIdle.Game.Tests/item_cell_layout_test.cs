using System.Text.RegularExpressions;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE ITEM CELL IS THREE LAYERS WITH THREE JOBS (2026-09-16): the slot well, the rarity frame and the
/// glyph, laid out by <see cref="ItemCellLayout"/> from measurements <see cref="ItemArtMetrics"/> takes.
/// These tests hold the geometry at every cell the game draws and every density profile, the scanners
/// on posed pixels, and the source itself to the rules the pass set: one rarity ramp, no second rarity
/// encoding in a square cell, and a selected look that is not gold.
/// </summary>
public class ItemCellLayoutTests
{
    /// <summary>Every square cell edge a caller passes today, at 100 % — the profile scales them.</summary>
    private static readonly int[] CellEdges = [30, 36, 48, 56, 64, 96, 120, 150];
    private static readonly int[] Profiles = [100, 125, 150];
    private static readonly Rarity[] Rarities = [Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary];

    /// <summary>A posed interior: 12 % left, 10 % top, 12 % right, 14 % bottom — see-through.</summary>
    private static readonly FrameInterior Posed = new(0.12f, 0.10f, 0.12f, 0.14f, true);

    private static IEnumerable<Rectangle> Cells()
    {
        foreach (var p in Profiles)
        {
            UiMetrics.Apply(p);
            foreach (var e in CellEdges)
            {
                var edge = UiMetrics.Control(e);
                yield return new Rectangle(37, 91, edge, edge);
            }
        }
        UiMetrics.Apply(100);
    }

    private static bool Inside(Rectangle inner, Rectangle outer)
        => inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

    // ── The geometry ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_frame_rect_is_square_inside_the_cell_and_at_least_92_percent_of_it()
    {
        foreach (var cell in Cells())
        {
            var frame = ItemCellLayout.FrameRect(cell);
            Assert.True(Inside(frame, cell), $"frame {frame} outside cell {cell}");
            Assert.Equal(frame.Width, frame.Height);
            Assert.True(frame.Width >= cell.Width * 0.92f, $"frame {frame.Width} under 92 % of cell {cell.Width}");
            // Centred: the rim is even to a pixel on both sides.
            Assert.True(Math.Abs((frame.X - cell.X) - (cell.Right - frame.Right)) <= 1);
            Assert.True(Math.Abs((frame.Y - cell.Y) - (cell.Bottom - frame.Bottom)) <= 1);
        }
    }

    [Fact]
    public void test_glyph_rect_sits_inside_the_frames_interior()
    {
        foreach (var cell in Cells())
        {
            var frame = ItemCellLayout.FrameRect(cell);
            foreach (var interior in new[] { Posed, FrameInterior.Fallback, FrameInterior.None })
            {
                var glyph = ItemCellLayout.GlyphRect(frame, interior);
                Assert.True(Inside(glyph, frame), $"glyph {glyph} outside frame {frame}");
                // Inside the hole the interior names, on every side.
                Assert.True(glyph.X >= frame.X + (int)(frame.Width * interior.Left) - 1);
                Assert.True(glyph.Y >= frame.Y + (int)(frame.Height * interior.Top) - 1);
                Assert.True(glyph.Right <= frame.Right - (int)(frame.Width * interior.Right) + 1);
                Assert.True(glyph.Bottom <= frame.Bottom - (int)(frame.Height * interior.Bottom) + 1);
                Assert.True(glyph.Width >= 1 && glyph.Height >= 1);
            }
        }
    }

    [Fact]
    public void test_fallback_interior_is_the_legacy_15_percent_and_not_see_through()
    {
        Assert.False(FrameInterior.Fallback.Transparent);
        Assert.Equal(ItemCellLayout.FallbackInset, FrameInterior.Fallback.Left);
        Assert.Equal(ItemCellLayout.FallbackInset, FrameInterior.Fallback.Bottom);
        var frame = new Rectangle(0, 0, 100, 100);
        Assert.Equal(new Rectangle(15, 15, 70, 70), ItemCellLayout.GlyphRect(frame, FrameInterior.Fallback));
    }

    [Theory]
    [InlineData(256, 256)]   // a full canvas
    [InlineData(57, 241)]    // item_charm_swift: a sliver on its chain
    [InlineData(241, 57)]    // the same, lying down
    [InlineData(40, 40)]     // a small file that must not be blurred up
    [InlineData(1, 1)]
    public void test_fit_keeps_aspect_stays_inside_and_never_upscales_past_the_pixels(int cw, int ch)
    {
        foreach (var cell in Cells())
        {
            var into = ItemCellLayout.GlyphRect(ItemCellLayout.FrameRect(cell), Posed);
            var fit = ItemCellLayout.Fit(new Point(cw, ch), into);
            Assert.True(Inside(fit, into), $"fit {fit} outside {into}");
            Assert.True(fit.Width >= 1 && fit.Height >= 1);
            // Never past 1:1 — a small glyph draws at its own size, centred.
            Assert.True(fit.Width <= cw && fit.Height <= ch, $"fit {fit} upscales {cw}x{ch}");
            // Aspect within a pixel of rounding: the binding axis is exact, so the drift is on the other one
            // (a sliver's width is a few pixels, and a half-pixel there is four on the height read back).
            var driftH = Math.Abs(fit.Height - fit.Width * ch / (float)cw);
            var driftW = Math.Abs(fit.Width - fit.Height * cw / (float)ch);
            Assert.True(Math.Min(driftH, driftW) <= 1f, $"aspect drift: {fit} for {cw}x{ch}");
            // Fills the room on its binding axis, or sits at native size when the room is bigger.
            var binds = fit.Width == into.Width || fit.Height == into.Height || (fit.Width == cw && fit.Height == ch);
            Assert.True(binds, $"fit {fit} neither fills {into} nor is native {cw}x{ch}");
            // Centred.
            Assert.True(Math.Abs((fit.X - into.X) - (into.Right - fit.Right)) <= 1);
            Assert.True(Math.Abs((fit.Y - into.Y) - (into.Bottom - fit.Bottom)) <= 1);
        }
    }

    [Fact]
    public void test_fit_is_content_aware_a_sliver_fills_more_than_its_canvas_would()
    {
        var into = new Rectangle(0, 0, 100, 100);
        var canvas = ItemCellLayout.Fit(new Point(256, 256), into);
        var sliver = ItemCellLayout.Fit(new Point(57, 241), into);
        // The whole canvas stretched into 100 px would draw the sliver's 57 px at 22 px wide; the
        // content-aware fit draws it at 100 tall and 24 wide — not narrower than the canvas would.
        Assert.Equal(100, canvas.Width);
        Assert.Equal(100, sliver.Height);
        Assert.True(sliver.Width >= 57 * 100 / 256);
    }

    [Fact]
    public void test_gem_pip_and_corner_overlays_sit_inside_the_frame()
    {
        foreach (var cell in Cells())
        {
            var frame = ItemCellLayout.FrameRect(cell);
            foreach (var r in new[]
                     {
                         ItemCellLayout.GemRect(frame), ItemCellLayout.GemRect(frame, Posed), ItemCellLayout.GemRect(frame, FrameInterior.None),
                         ItemCellLayout.PipRect(frame), ItemCellLayout.SourceGemRect(frame), ItemCellLayout.EnchantRect(frame),
                     })
            {
                Assert.True(Inside(r, frame), $"{r} outside frame {frame}");
                Assert.True(r.Width >= 1 && r.Height >= 1);
            }
            // A gem never sits shallower than GemInset, whatever the hole says.
            var gem = ItemCellLayout.GemRect(frame, FrameInterior.None);
            Assert.True(gem.X - frame.X >= (int)(frame.Width * ItemCellLayout.GemInset) - 1);
            // The two overlays are the frame's own corners.
            Assert.Equal(frame.Location, ItemCellLayout.SourceGemRect(frame).Location);
            Assert.Equal(new Point(frame.Right, frame.Bottom), new Point(ItemCellLayout.EnchantRect(frame).Right, ItemCellLayout.EnchantRect(frame).Bottom));
        }
        Assert.True(ItemCellLayout.EnchantMinEdge > ItemCellLayout.SourceGemMinEdge);
    }

    // ── The scanners, on posed pixels ──────────────────────────────────────────────────────────────────

    private static Color[] Canvas(int w, int h) => new Color[w * h];

    [Fact]
    public void test_content_bounds_finds_the_opaque_box_and_a_blank_canvas_reads_whole()
    {
        var data = Canvas(64, 64);
        for (var y = 10; y < 30; y++) for (var x = 20; x < 50; x++) data[y * 64 + x] = Color.White;
        data[5 * 64 + 5] = new Color(255, 255, 255, (int)ItemArtMetrics.AlphaFloor);   // at the floor: empty
        Assert.Equal(new Rectangle(20, 10, 30, 20), ItemArtMetrics.ContentBoundsOf(data, 64, 64));
        Assert.Equal(new Rectangle(0, 0, 64, 64), ItemArtMetrics.ContentBoundsOf(Canvas(64, 64), 64, 64));
        Assert.Equal(Rectangle.Empty, ItemArtMetrics.ContentBoundsOf(Canvas(4, 4), 8, 8));
    }

    [Fact]
    public void test_frame_interior_measures_a_see_through_ring_per_side()
    {
        // A 100 × 100 ring: 10 px thick on the left and top, 20 on the right, 5 on the bottom, hollow inside.
        var data = Canvas(100, 100);
        for (var y = 0; y < 100; y++)
            for (var x = 0; x < 100; x++)
                if (x < 10 || y < 10 || x >= 80 || y >= 95) data[y * 100 + x] = Color.White;
        var f = ItemArtMetrics.FrameInteriorOf(data, 100, 100);
        Assert.True(f.Transparent);
        Assert.Equal(0.10f, f.Left, 3);
        Assert.Equal(0.10f, f.Top, 3);
        Assert.Equal(0.20f, f.Right, 3);
        Assert.Equal(0.05f, f.Bottom, 3);
    }

    [Fact]
    public void test_frame_interior_of_a_painted_centre_is_the_fallback()
    {
        var data = Canvas(64, 64);
        for (var i = 0; i < data.Length; i++) data[i] = new Color(10, 8, 7, 255);
        Assert.Equal(FrameInterior.Fallback, ItemArtMetrics.FrameInteriorOf(data, 64, 64));
        Assert.Equal(FrameInterior.Fallback, ItemArtMetrics.FrameInterior(null));
        Assert.Equal(Rectangle.Empty, ItemArtMetrics.ContentBounds(null));
    }

    // ── The ramp ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_ramp_has_five_distinct_inks_and_selection_colours_are_not_among_them_but_common()
    {
        var inks = Rarities.Select(ItemCellLayout.RarityInk).ToArray();
        Assert.Equal(5, inks.Distinct().Count());
        Assert.Equal(UiInk.Primary, ItemCellLayout.RarityInk(Rarity.Common));
        Assert.Equal(UiInk.Good, ItemCellLayout.RarityInk(Rarity.Uncommon));
        Assert.Equal(UiInk.Accent, ItemCellLayout.RarityInk(Rarity.Legendary));
        // Epic reads on the ground: brighter than the plum it was (#8B3F82).
        var epic = ItemCellLayout.RarityInk(Rarity.Epic);
        Assert.True(epic.R + epic.G + epic.B > 0x8B + 0x3F + 0x82);
    }

    // ── The source pins ────────────────────────────────────────────────────────────────────────────────

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir, "CLAUDE.md")) && Directory.Exists(Path.Combine(dir, "src"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException("the repo root (CLAUDE.md beside src/) was not found above the test binary");
    }

    private static string GameSource(string file) => File.ReadAllText(Path.Combine(RepoRoot(), "src", "IdleXIdle.Game", file));

    [Fact]
    public void test_gear_screen_has_no_rarity_strip_in_a_square_cell()
    {
        var src = GameSource("GearScreen.cs");
        Assert.DoesNotContain("new Rectangle(cell.X, cell.Y, 5, cell.Height)", src);
        Assert.DoesNotContain("new Rectangle(box.X, box.Y, 5, box.Height)", src);
        // ...nor on the piece in hand, nor as a rarity ring round it or round the equip flight: a frame
        // already says it, and a second ring in the same colour is the double encoding the pass removed.
        Assert.DoesNotContain("new Rectangle(g.X, g.Y, 5, g.Height)", src);
        Assert.DoesNotContain("Ring(b, g, ItemCellLayout.RarityInk(", src);
        Assert.DoesNotContain("Ring(b, box, tint, 2);", src);
    }

    [Fact]
    public void test_gear_screen_selected_branches_are_not_gold()
    {
        var src = GameSource("GearScreen.cs");
        var selectedLines = src.Split('\n').Where(l => Regex.IsMatch(l, @"if \((sel|selected)\)")).ToList();
        Assert.NotEmpty(selectedLines);
        foreach (var line in selectedLines) Assert.DoesNotContain("Gold", line);
        // The mark itself is Primary, and it is the one place the selected look is drawn.
        var mark = src[src.IndexOf("private void SelectedMark", StringComparison.Ordinal)..];
        mark = mark[..mark.IndexOf("\n    }", StringComparison.Ordinal)];
        Assert.DoesNotContain("Gold", mark);
        Assert.Contains("Bone", mark);
    }

    [Fact]
    public void test_exactly_one_rarity_ramp_is_defined_across_the_game_assembly()
    {
        var dir = Path.Combine(RepoRoot(), "src", "IdleXIdle.Game");
        var hits = Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Select(f => (f, n: Regex.Matches(File.ReadAllText(f), @"0x4A, 0x90, 0xD9").Count))
            .Where(t => t.n > 0).ToList();
        Assert.Single(hits);
        Assert.EndsWith("ItemCellLayout.cs", hits[0].f);
        Assert.Equal(1, hits[0].n);
        foreach (var f in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(f);
            Assert.DoesNotContain("Color[] RarityColors", text);
            Assert.DoesNotContain("Color[] RarityInk", text);
            Assert.DoesNotContain("Color RarityColor(", text);
        }
    }

    // ── The art on disk ────────────────────────────────────────────────────────────────────────────────

    /// <summary>Every key ForgeScreen.ItemArt can build: slot × pool trait, the weapon by family.</summary>
    private static IEnumerable<(GearSlot slot, string key)> ReachableArtKeys()
    {
        foreach (var slot in Enum.GetValues<GearSlot>())
        {
            var slotWord = slot.ToString().ToLowerInvariant();
            foreach (var trait in GearTraits.PoolFor(slot))
            {
                var pick = trait.ToString().ToLowerInvariant();
                if (slot == GearSlot.Weapon)
                    foreach (var family in ItemNaming.WeaponFamilies)
                        yield return (slot, family == "blade" ? $"item_{slotWord}_{pick}" : $"item_{slotWord}_{family}_{pick}");
                else yield return (slot, $"item_{slotWord}_{pick}");
            }
        }
    }

    [Fact]
    public void test_every_reachable_item_glyph_and_every_rarity_frame_exists_on_disk()
    {
        var items = Path.Combine(RepoRoot(), "assets", "art", "ItemsLoot");
        var keys = ReachableArtKeys().ToList();
        Assert.Equal(44, keys.Count);
        foreach (var (slot, key) in keys)
            Assert.True(File.Exists(Path.Combine(items, "traits", slot.ToString().ToLowerInvariant(), key + ".png")), $"{key}.png is missing");
        foreach (var tier in new[] { "common", "uncommon", "rare", "epic", "legendary" })
            Assert.True(File.Exists(Path.Combine(items, "frames", $"ui_frame_rarity_{tier}.png")), $"the {tier} frame is missing");
    }

    [Fact]
    public void test_every_item_png_is_a_256_canvas_by_its_header()
    {
        var items = Path.Combine(RepoRoot(), "assets", "art", "ItemsLoot");
        var files = Directory.EnumerateFiles(items, "*.png", SearchOption.AllDirectories).ToList();
        Assert.True(files.Count >= 138, $"expected the 138 item PNGs, found {files.Count}");
        foreach (var f in files)
        {
            // PNG: 8-byte signature, then the IHDR chunk: length(4) "IHDR"(4) width(4) height(4), big-endian.
            var head = new byte[24];
            using var s = File.OpenRead(f);
            Assert.Equal(24, s.Read(head, 0, 24));
            Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(head, 12, 4));
            var w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            var h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
            Assert.True(w == 256 && h == 256, $"{Path.GetFileName(f)} is {w}x{h}");
        }
    }

    [Fact]
    public void test_the_item_art_gate_exists_and_check_all_runs_it()
    {
        var root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tools", "check_item_art.py")));
        Assert.Contains("check_item_art.py", File.ReadAllText(Path.Combine(root, "tools", "check_all.sh")));
    }
}
