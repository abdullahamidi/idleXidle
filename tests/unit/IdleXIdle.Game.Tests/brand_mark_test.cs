using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using IdleXIdle.Core.Builds;
using IdleXIdle.Game.Presentation;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE MARK CONTRACT (ADR-011, the MARK / PERSISTENT TARGET-ATTACHED STATE archetype; BRAND is the reference, the owner's
/// brief 2026-09-29): "That enemy has been branded. The mark is living shadow, etched into its body. It quietly persists
/// there. When the mark deepens, it bites further inward. If it spreads or transfers, the same mark migrates cleanly to
/// the next target." A state on the enemy: not a projectile, a field, a trap or a champion performance.
/// </summary>
public class brand_mark_test
{
    private static readonly MarkRecipe Brand = MarkRecipes.SeekerBrand;
    private const float Frame = 1000f / 60f;

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static (int W, int H) PngSize(string key)
    {
        var png = File.ReadAllBytes(RepoFile("assets", "art", "VFX", "parts", key + ".png"));
        int Be(int at) => (png[at] << 24) | (png[at + 1] << 16) | (png[at + 2] << 8) | png[at + 3];
        return (Be(16), Be(20));
    }

    private static readonly (float, int, bool)[] Plain =
        { (2000f, 70, false), (4000f, 70, false), (6000f, 70, false), (8000f, 70, false), (10000f, 70, false) };

    private static readonly (float, int, bool)[] Etch =
        { (2000f, 120, false), (4000f, 170, false), (6000f, 220, false), (8000f, 240, false), (10000f, 240, false) };

    /// <summary>The seeded fight the films show: BRAND ticking every 2 s, the front falling at 6.7 s and 8.2 s.</summary>
    private static MarkPerformance Front((float, int, bool)[] ticks, params (float, int)[] falls)
        => new(Brand, 2, ticks, falls.Length > 0 ? falls : new[] { (6700f, 0), (8200f, 1) }, 4, wholeWave: false, frontFullPercent: 0);

    // ── WHO OWNS IT ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_brand_is_the_seekers_mark_and_only_brand()
    {
        Assert.Same(Brand, MarkRecipes.For("seeker", "sign_brand"));
        // CALL (a cast mark) and OATHMARK (a reaction mark) share BRAND's "mark" art key, and are not states: untouched
        Assert.Null(MarkRecipes.For("seeker", "sign_call"));
        Assert.Null(MarkRecipes.For("seeker", "sig_oathbound_oathmark"));
        Assert.Null(MarkRecipes.For("oathbound", "sign_brand"));
        Assert.Null(FieldRecipes.For("seeker", "sign_brand"));
        var brand = SkillCatalogue.All.Single(d => d.Id == "sign_brand");
        Assert.Equal(SkillKind.Field, brand.Kind);
        Assert.Equal(SkillEffect.Amplify, brand.Effect);
    }

    [Fact]
    public void test_the_fields_are_chosen_by_recipe_in_any_slot_order()
    {
        // the first Field used to win: BRAND woven before PRESS drew a reticle behind the hunter and PRESS vanished
        var ids = new[] { "hammer_press", "sign_brand", "field_mire", "volley_spray" };
        foreach (var order in Permutations(ids))
        {
            var skills = order.Select(id => new EquippedSkill(SkillCatalogue.ById(id), IdleXIdle.Core.Sources.Source.Shadow)).ToList();
            var (performed, mark, held) = FieldRoles.Choose(skills, "seeker");
            Assert.Equal("hammer_press", skills[performed].Def.Id);
            Assert.Equal("sign_brand", skills[mark].Def.Id);
            Assert.Equal("field_mire", skills[held].Def.Id);
        }
        // BRAND alone keeps no held aura (its reticle is retired); another hunter's BRAND keeps the old way
        var alone = new List<EquippedSkill> { new(SkillCatalogue.ById("sign_brand"), IdleXIdle.Core.Sources.Source.Shadow) };
        Assert.Equal((-1, 0, -1), FieldRoles.Choose(alone, "seeker"));
        Assert.Equal((-1, -1, 0), FieldRoles.Choose(alone, "oathbound"));
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        Assert.Contains("var roles = FieldRoles.Choose(_waveSkills, Character.Id);", hunt);
        Assert.DoesNotContain("var fieldSk = _waveSkills.FirstOrDefault(k => k.Def.Kind == SkillKind.Field);", hunt);
    }

    private static IEnumerable<string[]> Permutations(string[] items)
    {
        if (items.Length <= 1) { yield return items; yield break; }
        for (var i = 0; i < items.Length; i++)
            foreach (var rest in Permutations(items.Where((_, j) => j != i).ToArray()))
                yield return new[] { items[i] }.Concat(rest).ToArray();
    }

    // ── THE PARTS ────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_one_atlas_of_pixel_cells_from_a_pixellab_silhouette_pinned_to_its_generator()
    {
        Assert.Equal((Brand.Cell * Brand.Columns, Brand.Cell * Brand.Stages * 2 + Brand.ThreadCell), PngSize(Brand.AtlasKey));
        Assert.Equal(Brand.Stages * Brand.Cell * 2, Brand.ThreadY);
        var spans = JsonDocument.Parse(File.ReadAllText(RepoFile("tools", "asset-pipeline", "v2", "keypose_sources", "seeker_brand_spans.json"))).RootElement;
        Assert.Equal(Brand.Cell, spans.GetProperty("cell").GetInt32());
        Assert.Equal(Brand.Stages, spans.GetProperty("stages").GetArrayLength());
        Assert.Equal(Brand.Columns, spans.GetProperty("columns").GetArrayLength());
        Assert.Equal(Brand.HaloRow0, spans.GetProperty("halo_row0").GetInt32());
        Assert.Equal(Brand.ThreadY, spans.GetProperty("thread_y").GetInt32());
        Assert.Equal(MarkRecipe.IdlePhases, spans.GetProperty("phases").GetInt32());
        Assert.Equal(Brand.CoilBox, spans.GetProperty("box").GetSingle(), 1);
        Assert.Equal(Brand.Centre.X, spans.GetProperty("centre")[0].GetSingle(), 1);
        Assert.Equal(Brand.Centre.Y, spans.GetProperty("centre")[1].GetSingle(), 1);
        Assert.Equal(Brand.Tip.X, spans.GetProperty("tip")[0].GetSingle(), 1);
        Assert.Equal(Brand.Tip.Y, spans.GetProperty("tip")[1].GetSingle(), 1);
        var columns = spans.GetProperty("columns");
        Assert.Equal("gather0", columns[MarkRecipe.Gather0].GetString());
        Assert.Equal("idle0", columns[MarkRecipe.Idle0].GetString());
        Assert.Equal("edge", columns[MarkRecipe.Edge].GetString());
        Assert.Equal("carve0", columns[MarkRecipe.Carve0].GetString());
        Assert.Equal("form0", columns[MarkRecipe.Form0].GetString());
        Assert.Equal("loosen0", columns[MarkRecipe.Loosen0].GetString());
        // the SHAPE is PixelLab's (a pixen silhouette, cached because its link expires); nothing generated is used raw: its
        // path is redrawn at the game's pixel material (a logical pixel of 1/3 of the cell, value bands, nearest upscale)
        Assert.True(File.Exists(RepoFile("tools", "asset-pipeline", "v2", "keypose_sources", "seeker_brand_src", "pixen_1bf52462.png")));
        var gen = File.ReadAllText(RepoFile("tools", "asset-pipeline", "v2", "seeker_brand.py"));
        foreach (var pin in new[] { "pixen_1bf52462.png", "SEED = 20260929", "LOW = 3", "np.kron(", "DARK, MID, LIGHT = ", "def coil_path(",
                                    "def clean(", "def carve(", "def form(" })
            Assert.Contains(pin, gen);
        // a state, never the other archetypes' language: no teeth (JAWS), no blades or shards (SPRAY, HARD HANDS), no reticle
        foreach (var banned in new[] { "teeth", "tooth", "fang", "blade", "shard", "knife", "reticle", "crosshair", "ring", "particle", "burst", "orb" })
            Assert.DoesNotContain(typeof(MarkRecipe).GetProperties(), p => p.Name.ToLowerInvariant().Contains(banned));
    }

    [Fact]
    public void test_the_marks_drawn_extent_is_the_halo_the_head_is_kept_clear_of_and_no_cell_is_clipped()
    {
        // every cell but the loosen (which plays on a falling body), cut and halo, at the largest the snap to thirds makes
        // a whelp-sized coil (x 7/6), inside the recipe's halo share of the body height; and nothing on any cell's outer
        // 3 px ring (smoke clipped by the border drew a straight edge)
        float halfW = 0f, up = 0f, down = 0f;
        for (var row = 0; row < Brand.Stages * 2; row++)
            for (var column = 0; column < Brand.Columns; column++)
            {
                var (a, _) = Cell(row, column);
                for (var i = 0; i < a.Length; i++)
                {
                    if (a[i] < 8) continue;
                    int x = i % Brand.Cell, y = i / Brand.Cell;
                    Assert.False(x < 3 || y < 3 || x >= Brand.Cell - 3 || y >= Brand.Cell - 3, $"cell {row},{column} is clipped at its border");
                    if (column >= MarkRecipe.Loosen0) continue;
                    halfW = Math.Max(halfW, Math.Max(Brand.Centre.X - x, x + 1 - Brand.Centre.X));
                    up = Math.Max(up, Brand.Centre.Y - y);
                    down = Math.Max(down, y + 1 - Brand.Centre.Y);
                }
            }
        var perPx = Brand.SizeShare / Brand.CoilBox * 7f / 6f;
        Assert.True(2f * halfW * perPx <= Brand.HaloWidthShare, $"the mark spans {2f * halfW * perPx:0.000} of the body, wider than its halo");
        Assert.True(2f * Math.Max(up, down) * perPx <= Brand.HaloHeightShare, $"the mark spans {2f * Math.Max(up, down) * perPx:0.000} of the body, taller than its halo");
    }

    /// <summary>The atlas cell (runtime px) of <paramref name="row"/>, <paramref name="column"/>: its pixels' alpha and grey.</summary>
    private static (byte[] A, byte[] L) Cell(int row, int column)
    {
        using var img = SixLaborsFree.Load(RepoFile("assets", "art", "VFX", "parts", Brand.AtlasKey + ".png"));
        return img.Cell(column * Brand.Cell, row * Brand.Cell, Brand.Cell);
    }

    [Fact]
    public void test_each_deeper_stage_cuts_further_inward_never_bigger_and_never_lights_brighter()
    {
        // the rest cell of each stage (idle phase 0): the lit texels (above the groove's rest band), the cut's area, its
        // box, and its texels' mean distance from the coil's centre
        var lit = new int[Brand.Stages];
        var area = new int[Brand.Stages];
        var brightest = new int[Brand.Stages];
        var box = new (int X0, int Y0, int X1, int Y1)[Brand.Stages];
        var cells = new byte[Brand.Stages][];
        float Reach(int i) => MathF.Sqrt(MathF.Pow(i % Brand.Cell + 0.5f - Brand.Centre.X, 2) + MathF.Pow(i / Brand.Cell + 0.5f - Brand.Centre.Y, 2));
        for (var k = 0; k < Brand.Stages; k++)
        {
            var (a, l) = Cell(k, MarkRecipe.Idle0);
            cells[k] = a;
            box[k] = (int.MaxValue, int.MaxValue, -1, -1);
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] < 8) continue;
                area[k]++;
                brightest[k] = Math.Max(brightest[k], l[i]);
                if (l[i] > 200) lit[k]++;
                int x = i % Brand.Cell, y = i / Brand.Cell;
                box[k] = (Math.Min(box[k].X0, x), Math.Min(box[k].Y0, y), Math.Max(box[k].X1, x), Math.Max(box[k].Y1, y));
            }
        }
        for (var k = 2; k < Brand.Stages; k++)
        {
            var added = Enumerable.Range(0, cells[k].Length).Where(i => cells[k][i] >= 8 && cells[k - 1][i] < 8).ToArray();
            var before = Enumerable.Range(0, cells[k - 1].Length).Where(i => cells[k - 1][i] >= 8).ToArray();
            Assert.True(area[k] > area[k - 1], $"stage {k} cuts nothing more than stage {k - 1}");
            if (k <= 3)
            {
                // deep1 widens the groove and deep2 bites into the hollow's wall: clearly more of the body each
                Assert.True(area[k] >= area[k - 1] * 1.10f, $"stage {k} cuts barely more than stage {k - 1} ({area[k]} vs {area[k - 1]})");
            }
            else
            {
                // deep3 and deep4 cut the hook further ALONG THE SPIRAL: the new cut lies deep inside the coil, and the
                // outline never grows (a burn spreading out of the ring read as "just bigger"; blind read 3, unanimous)
                Assert.True(added.Average(Reach) < before.Average(Reach) * 0.6f,
                            $"stage {k}: its new cut is not further inward ({added.Average(Reach):0.0} vs {before.Average(Reach):0.0})");
                Assert.Equal(box[3], box[k]);
            }
            // NEVER BRIGHTER: the brightest texel at rest is the same band at every depth
            Assert.Equal(brightest[1], brightest[k]);
        }
        Assert.True(area[5] >= area[1] * 1.5f, $"the deepest cut is not much more than the base ({area[5]} vs {area[1]})");
        // the smoky roots grow with the cut they bleed out of (they never fill the hollow: as ink on a light body that
        // closed the host's own outlines into a dark disc, an eye)
        var halo = Enumerable.Range(0, Brand.Stages).Select(k => Cell(Brand.HaloRow0 + k, MarkRecipe.Idle0).A.Count(v => v >= 8)).ToArray();
        Assert.True(halo[3] >= halo[2] && halo[4] > halo[3] && halo[5] > halo[4], string.Join(",", halo));
        // at rest the lit core is only a travelling part of the coil: the whole coil lights only on a beat (the edge cell)
        var (edgeA, _) = Cell(1, MarkRecipe.Edge);
        Assert.True(lit[1] * 2 < edgeA.Count(v => v >= 8), "the base coil's rest is as lit as its beat");
    }

    [Fact]
    public void test_the_deepen_beat_is_a_chisel_moving_inward_never_the_whole_coil()
    {
        for (var k = 1; k < Brand.Stages; k++)
        {
            var (cutA, _) = Cell(k, MarkRecipe.Idle0);
            var (prevA, _) = Cell(k - 1, MarkRecipe.Idle0);
            var whole = cutA.Count(v => v >= 8);
            var swept = new bool[cutA.Length];
            var last = Array.Empty<byte>();
            for (var step = 0; step < 3; step++)
            {
                var (carveA, _) = Cell(k, MarkRecipe.Carve0 + step);
                var lit = carveA.Count(v => v >= 8);
                Assert.True(lit > 0, $"stage {k} carve {step} is empty");
                Assert.True(lit < whole * 0.5f, $"stage {k} carve {step} lights {lit} of the coil's {whole}: a brightening, not a chisel");
                for (var i = 0; i < carveA.Length; i++) swept[i] |= carveA[i] >= 8;
                // no counter anywhere in a chisel stretch (a lit knot with a hole read as an S / 5 / 9 / @ or a pupil)
                Assert.Empty(Enclosed(carveA));
                last = carveA;
            }
            if (k <= 2) continue;   // spread -> base -> deep1 only WIDEN the groove: their chisel is its core line
            // THE NEW CUT IS CUT UNDER THE CHISEL: what this deep stage adds lies under its sweep, and the last stretch is
            // mostly the new cut itself (the rest a one-pixel lip of the groove it bites into), never old groove relit
            var added = Enumerable.Range(0, cutA.Length).Where(i => cutA[i] >= 8 && prevA[i] < 8).ToArray();
            Assert.True(added.Count(i => swept[i]) >= added.Length * 0.9f, $"stage {k}: the chisel misses the new cut");
            var lastLit = Enumerable.Range(0, last.Length).Where(i => last[i] >= 8).ToArray();
            Assert.True(lastLit.Count(i => cutA[i] >= 8 && prevA[i] < 8) >= lastLit.Length * 0.4f, $"stage {k}: the chisel relights old groove");
        }
        // on the screen: the chisel's three steps in turn, on the tick that deepened, then the last one cooling
        var m = Front(Etch);
        Assert.Equal(0f, m.HotAt(0, 3999f, out _));
        Assert.True(m.HotAt(0, 4000f + 1f, out var c0) > 0.7f);
        Assert.Equal(MarkRecipe.Carve0, c0);
        m.HotAt(0, 4000f + Brand.CarveStepMs + 1f, out var c1);
        Assert.Equal(MarkRecipe.Carve0 + 1, c1);
        m.HotAt(0, 4000f + Brand.CarveStepMs * 2f + 1f, out var c2);
        Assert.Equal(MarkRecipe.Carve0 + 2, c2);
        Assert.Equal(0f, m.HotAt(0, 4000f + Brand.CarveStepMs * 3f + Brand.DeepenEdgeMs, out _));
        // the old cut stays on screen until the chisel's last step arrives, then the new one is there under it
        Assert.Equal(m.StageFor(0, 3999f), m.DrawnStage(0, 4000f + 1f));
        Assert.Equal(m.StageFor(0, 4001f), m.DrawnStage(0, 4000f + Brand.CarveStepMs * 2f + 1f));
        // a bite on the tick: the chisel waits until its attack clip has ended (the depth is true on the tick all the same)
        var settled = new MarkPerformance(Brand, 2, Etch, new[] { (6700f, 0), (8200f, 1) }, 4, false, 0,
                                          Etch.Select(t => t.Item1).ToArray());
        Assert.Equal(0f, settled.HotAt(0, 4000f + 1f, out _));
        Assert.True(settled.HotAt(0, 4000f + Brand.SettleMs + 1f, out _) > 0.7f);
        Assert.Equal(3, settled.StageFor(0, 4001f));
        Assert.Equal(2, settled.DrawnStage(0, 4000f + Brand.SettleMs + 1f));
        // no chisel is traced over a bite: clear of its wind-up before the strike and of its clip after it
        MarkPerformance With(params float[] strikes) => new(Brand, 2, Etch, Array.Empty<(float, int)>(), 4, false, 0, strikes);
        Assert.Equal(4000f + Brand.SettleMs, With(4000f).SettledStart(4000f));
        Assert.Equal(4150f + Brand.SettleMs, With(4150f).SettledStart(4000f));            // a bite just after the tick
        Assert.Equal(4000f, With(3500f).SettledStart(4000f));                              // one long settled
        Assert.Equal(4000f, With(4000f + Brand.CarveStepMs * 3f + Brand.SettleBeforeMs + 1f).SettledStart(4000f));
        Assert.Equal(4300f + Brand.SettleMs, With(4000f, 4300f).SettledStart(4000f));      // bites in a row: after the last
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        Assert.Contains("if (wave[ej].Kind == BattleEventKind.EnemyStrike) markStrikes.Add(wave[ej].AtMs);", hunt);
    }

    /// <summary>The background texels of a cell (at the logical pixel, 3 runtime px) that the cut ENCLOSES (not reachable
    /// from the cell's edge): the size of each enclosed region.</summary>
    private static List<int> Enclosed(byte[] alpha)
    {
        const int G = 28;
        var cut = new bool[G * G];
        for (var y = 0; y < G; y++)
            for (var x = 0; x < G; x++)
                cut[y * G + x] = alpha[(y * 3 + 1) * Brand.Cell + x * 3 + 1] >= 8;
        var seen = new bool[G * G];
        var sizes = new List<int>();
        for (var start = 0; start < G * G; start++)
        {
            if (cut[start] || seen[start]) continue;
            var stack = new Stack<int>();
            stack.Push(start);
            seen[start] = true;
            int size = 0;
            var edge = false;
            while (stack.Count > 0)
            {
                var i = stack.Pop();
                size++;
                int x = i % G, y = i / G;
                edge |= x == 0 || y == 0 || x == G - 1 || y == G - 1;
                foreach (var n in new[] { x > 0 ? i - 1 : -1, x < G - 1 ? i + 1 : -1, y > 0 ? i - G : -1, y < G - 1 ? i + G : -1 })
                    if (n >= 0 && !cut[n] && !seen[n]) { seen[n] = true; stack.Push(n); }
            }
            if (!edge) sizes.Add(size);
        }
        return sizes;
    }

    [Fact]
    public void test_no_cell_holds_a_counter_a_letter_or_an_eye_could_be_read_in()
    {
        // THE SPIRAL STAYS OPEN: the cut encloses nothing at any depth, not even the coil's own hollow (closed, every deep
        // stage was a ring round a pupil: an O, an eye), and nothing smaller: the bowl of a 9, the counter of an S (the
        // round-3 deep stages drew all three at the inner end). The apply's lit line and the chisel enclose nothing either.
        for (var k = 0; k < Brand.Stages; k++)
        {
            foreach (var column in Enumerable.Range(MarkRecipe.Idle0, MarkRecipe.IdlePhases).Append(MarkRecipe.Form0 + 2))
                Assert.Empty(Enclosed(Cell(k, column).A));
            Assert.Empty(Enclosed(Cell(k, MarkRecipe.Edge).A));
        }
        // the inner hook is never lit at rest (lit, it read as a pupil or a letter's stroke), and never thickened
        var gen = File.ReadAllText(RepoFile("tools", "asset-pipeline", "v2", "seeker_brand.py"));
        Assert.Contains("core = inside & (DIST <= 0.75) & (TPAR <= RING[1])", gen);
        Assert.Contains("(DIST_HOOK > CHANNEL)", gen);
        Assert.Contains("inside &= ~OPEN", gen);
        Assert.DoesNotContain("EXTEND", gen);
    }

    [Fact]
    public void test_the_strand_is_one_thread_below_the_creatures_heads()
    {
        var m = Front(Plain);
        foreach (var distance in new[] { 50f, 90f, 140f, 260f, 400f })
        {
            // at the eased path's peak speed two puffs are less than a puff's visible width apart: they overlap
            var lag = m.BeadLag(distance, Brand.FlightMs, 1f);
            var peak = 1.5f * distance / Brand.FlightMs;
            Assert.True(peak * lag <= Brand.ThreadCell * Brand.ThreadVisibleShare * 0.85f + 0.01f, $"puffs {peak * lag:0.0} px apart over {distance} px");
            // the path sags toward the tip: never above the higher of its two ends (a lift crossed the next creature's face)
            var from = new Microsoft.Xna.Framework.Vector2(1100f, 770f);
            var to = new Microsoft.Xna.Framework.Vector2(1100f + distance, 760f);
            var tip = to + (Brand.Tip - Brand.Centre);
            for (var t = 0f; t <= 1f; t += 0.05f)
                Assert.True(m.FlightPoint(from, to, 1f, t).Y >= Math.Min(from.Y, tip.Y) - 0.01f, $"the strand rises over the heads at t {t:0.00}");
        }
        Assert.True(Brand.Beads >= 6);
    }

    [Fact]
    public void test_every_creature_strip_has_an_authored_body_point_on_its_body()
    {
        // WHERE A MARK SITS is authored per strip frame (tools/asset-pipeline/v2/mark_points.py, reviewed on a contact
        // sheet), never guessed: a luma rule put the coil on legs, hems, staffs and spike tips, and against the next
        // creature's face. Every idle and attack strip of every creature and boss has one point per frame; the coil's
        // footprint there (its box at the runtime size: a share of the creature's visible height) lies on the creature's
        // own opaque pixels; the point moves with the body, never jumping across it between two frames or on the loop's
        // wrap (the clip restarts at frame 0 on every bite); two identical frames carry one point (the whelp's frames 0
        // and 7 are one picture: two points slid the coil across a still body); and the mark's whole smoky halo, at the
        // largest size the snap to thirds gives, stays clear of the creature's HEAD (its face, eyes, helmet, bell: a box
        // per frame reviewed on the sheet) - a coil on the round-4 whelp's frame 0 lit its eye on every deepen.
        var strips = new[] { "Enemies", "Bosses" }
            .SelectMany(f => Directory.GetDirectories(RepoFile("assets", "art", "Animations", f)))
            .Where(d => !Path.GetFileName(d).EndsWith("_death", StringComparison.Ordinal)).ToArray();
        Assert.Equal(60, strips.Length);
        foreach (var dir in strips)
        {
            var name = Path.GetFileName(dir) + "_strip8_512";
            var json = Path.Combine(dir, name + ".mark.json");
            Assert.True(File.Exists(json), $"{name} has no body point");
            var text = File.ReadAllText(json);
            var points = MarkPoints.Parse(text).Points;
            var root = JsonDocument.Parse(text).RootElement;
            var heads = root.GetProperty("head");
            var halo = root.GetProperty("halo");
            // the halo the tool cleared the head with IS the runtime's (measured on the atlas below), never its own guess
            Assert.Equal(Brand.HaloWidthShare, halo[0].GetSingle(), 3);
            Assert.Equal(Brand.HaloHeightShare, halo[1].GetSingle(), 3);
            using var img = SixLaborsFree.Load(Path.Combine(dir, name + ".png"));
            var size = img.Height;
            Assert.Equal(img.Width / size, points.Count);
            var opaque = img.Opaque();
            // the coil's footprint: SizeShare of the visible height, an ellipse as lopsided as the coil (4 : 5)
            var (top, bottom) = img.OpaqueRows(0, size);
            var rx = Brand.SizeShare * (bottom - top + 1) / 2f;
            var ry = rx * 0.8f;
            for (var k = 0; k < points.Count; k++)
            {
                float cx = points[k].X * size, cy = points[k].Y * size;
                int on = 0, all = 0;
                for (var y = (int)(cy - ry); y <= (int)(cy + ry); y += 2)
                    for (var x = (int)(cx - rx); x <= (int)(cx + rx); x += 2)
                    {
                        var dx = (x - cx) / rx;
                        var dy = (y - cy) / ry;
                        if (dx * dx + dy * dy > 1f) continue;
                        all++;
                        if (x >= 0 && y >= 0 && x < size && y < size && opaque[y * img.Width + k * size + x]) on++;
                    }
                Assert.True(on >= all * 0.8f, $"{name} frame {k}: only {on * 100 / Math.Max(1, all)} % of the coil on the body");
                // never a slide across the body: the point moves at most 0.16 of the frame beyond what the body itself
                // moved (its opaque centroid: the whelp's lunge carries the whole body a fifth of the frame)
                var pk = k == 0 ? points.Count - 1 : k - 1;
                var moved = Microsoft.Xna.Framework.Vector2.Distance(img.Centroid(k, size), img.Centroid(pk, size)) / size;
                Assert.True(Microsoft.Xna.Framework.Vector2.Distance(points[k], points[pk]) <= 0.16f + moved, $"{name}: the point jumps at frame {k}");
                for (var j = 0; j < k; j++)
                    if (img.SameFrame(j, k, size))
                        Assert.Equal(points[j], points[k]);
                if (heads.GetArrayLength() == 0) continue;   // a creature with no head (the archive swarm's glyph)
                // the halo's ellipse (fractions of the frame) against the head box's nearest point; within the authoring
                // tool's working resolution (a quarter of the frame's)
                var box = heads[k];
                float x0 = box[0].GetSingle(), y0 = box[1].GetSingle(), x1 = box[2].GetSingle(), y1 = box[3].GetSingle();
                var hx = Brand.HaloWidthShare * (bottom - top + 1) / size / 2f;
                var hy = Brand.HaloHeightShare * (bottom - top + 1) / size / 2f;
                var nx = Math.Clamp(points[k].X, x0, x1) - points[k].X;
                var ny = Math.Clamp(points[k].Y, y0, y1) - points[k].Y;
                Assert.True((nx / hx) * (nx / hx) + (ny / hy) * (ny / hy) > 0.95f, $"{name} frame {k}: the mark's halo reaches its head");
            }
            // THE HEAD BOX IS THE HEAD: on the reference whelp, whose eyes are white specks, every eye pixel of every frame
            // lies inside that frame's box (a box drifted off the head would let the halo onto the eyes and still pass)
            if (name.StartsWith("umbral_swarm_", StringComparison.Ordinal))
                for (var k = 0; k < points.Count; k++)
                {
                    var box = heads[k];
                    foreach (var (ex, ey) in img.BrightSpecks(k, size, 200))
                        Assert.True(ex >= box[0].GetSingle() * size - 3 && ex <= box[2].GetSingle() * size + 3
                                    && ey >= box[1].GetSingle() * size - 3 && ey <= box[3].GetSingle() * size + 3,
                                    $"{name} frame {k}: an eye pixel at {ex},{ey} lies outside the head box");
                }
        }
        // A FALLING HOST is pinned from its death clip: every creature's death strip carries one point on the falling
        // body's torso (its first frames, while the coil comes apart and the strand leaves), the halo off the head
        var deaths = new[] { "Enemies", "Bosses" }
            .SelectMany(f => Directory.GetDirectories(RepoFile("assets", "art", "Animations", f)))
            .Where(d => Path.GetFileName(d).EndsWith("_death", StringComparison.Ordinal)).ToArray();
        Assert.Equal(30, deaths.Length);
        foreach (var dir in deaths)
        {
            var name = Path.GetFileName(dir) + "_strip8_512";
            var json = Path.Combine(dir, name + ".mark.json");
            Assert.True(File.Exists(json), $"{name} has no body point");
            var points = MarkPoints.Parse(File.ReadAllText(json)).Points;
            Assert.All(points, pt => Assert.Equal(points[0], pt));   // held: the coil only comes apart on the first frames
            using var img = SixLaborsFree.Load(Path.Combine(dir, name + ".png"));
            var size = img.Height;
            var opaque = img.Opaque();
            var (top, bottom) = img.OpaqueRows(0, size);
            var rx = Brand.SizeShare * (bottom - top + 1) / 2f;
            var ry = rx * 0.8f;
            float cx = points[0].X * size, cy = points[0].Y * size;
            int on = 0, all = 0;
            for (var y = (int)(cy - ry); y <= (int)(cy + ry); y += 2)
                for (var x = (int)(cx - rx); x <= (int)(cx + rx); x += 2)
                {
                    var dx = (x - cx) / rx;
                    var dy = (y - cy) / ry;
                    if (dx * dx + dy * dy > 1f) continue;
                    all++;
                    if (x >= 0 && y >= 0 && x < size && y < size && opaque[y * img.Width + x]) on++;
                }
            Assert.True(on >= all * 0.7f, $"{name}: only {on * 100 / Math.Max(1, all)} % of the coil on the falling body");
        }
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        Assert.Contains("PinMarkOnDeath(slot, deathStrip);", hunt);
        // mapped with the very transform that drew the frame: a side-cropped source, a flipped frame
        var pts = new[] { new Microsoft.Xna.Framework.Vector2(0.5f, 0.5f), new Microsoft.Xna.Framework.Vector2(0.25f, 0.75f) };
        var at = MarkPoints.ToArena(pts, 512, new Microsoft.Xna.Framework.Rectangle(512 + 64, 32, 384, 480),
                                    new Microsoft.Xna.Framework.Rectangle(1000, 500, 192, 240), false)!.Value;
        Assert.Equal(1000f + (128f - 64f) * 0.5f, at.X, 2);
        Assert.Equal(500f + (384f - 32f) * 0.5f, at.Y, 2);
        var flipped = MarkPoints.ToArena(pts, 512, new Microsoft.Xna.Framework.Rectangle(512 + 64, 32, 384, 480),
                                         new Microsoft.Xna.Framework.Rectangle(1000, 500, 192, 240), true)!.Value;
        Assert.Equal(1192f - (128f - 64f) * 0.5f, flipped.X, 2);
        Assert.Null(MarkPoints.ToArena(pts, 512, new Microsoft.Xna.Framework.Rectangle(512 * 5, 0, 512, 512),
                                       new Microsoft.Xna.Framework.Rectangle(0, 0, 10, 10), false));
        var csproj = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "IdleXIdle.Game.csproj"));
        Assert.Contains("*.mark.json", csproj);
    }

    // ── THE DEPTH LADDER: deeper, never brighter or bigger ───────────────────────────────────────────────────────────

    [Fact]
    public void test_a_stage_is_the_depth_in_force_whichever_variation_reached_it()
    {
        Assert.Equal(0, Brand.StageOf(35));
        Assert.Equal(0, Brand.StageOf(45));
        Assert.Equal(1, Brand.StageOf(52));
        Assert.Equal(1, Brand.StageOf(70));
        Assert.Equal(2, Brand.StageOf(120));
        Assert.Equal(3, Brand.StageOf(170));
        Assert.Equal(4, Brand.StageOf(220));
        Assert.Equal(5, Brand.StageOf(240));
        Assert.Equal(5, Brand.StageOf(320));
        for (var pct = 0; pct < 400; pct++)
            Assert.True(Brand.StageOf(pct) <= Brand.StageOf(pct + 1));
        // the size never follows the depth: one scale per body, snapped to thirds (a logical pixel is whole screen pixels)
        var etch = Front(Etch);
        foreach (var h in new[] { 150f, 190f, 230f, 300f, 460f })
        {
            var s = etch.ScaleFor(h);
            Assert.Equal(0f, s * 3f - MathF.Round(s * 3f), 3);
            Assert.InRange(s, Brand.MinScale, Brand.MaxScale);
        }
        Assert.Equal(1f, etch.ScaleFor(214f), 3);   // a whelp's canonical body: the coil's 60 px box, 3 px a logical pixel
    }

    [Fact]
    public void test_a_refresh_that_changes_nothing_shows_nothing()
    {
        var m = Front(Etch);
        // ETCH at its cap (the 10 s tick: 240 -> 240) on the host of the moment, and every tick of plain BRAND
        var host = m.FrontAt(10000f);
        Assert.True(m.Carries(host, 10000f));
        for (var u = 0f; u < 400f; u += Frame)
            Assert.Equal(0f, m.HotAt(host, 10000f + u, out _));
        var plain = Front(Plain);
        foreach (var tick in new[] { 4000f, 6000f })
            for (var u = 0f; u < 400f; u += Frame)
                Assert.Equal(0f, plain.HotAt(0, tick + u, out _));
    }

    // ── APPLY, IDLE ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_mark_is_true_from_the_first_tick_and_its_cut_lands_on_it()
    {
        var m = Front(Plain);
        Assert.Equal(2000f, m.FirstTickMs);
        Assert.Equal(MarkPhase.None, m.TryPhase(0, 2000f + Brand.GatherFromMs - 1f, out _));
        Assert.Equal(MarkPhase.Apply, m.TryPhase(0, 2000f + Brand.GatherFromMs + 1f, out var u));
        Assert.Equal(MarkRecipe.Gather0, m.ColumnAt(0, MarkPhase.Apply, u, 1800f));
        Assert.Equal(MarkRecipe.Gather0 + 2, m.ColumnAt(0, MarkPhase.Apply, Brand.Gather2Ms + 1f, 1905f));
        Assert.InRange(m.ColumnAt(0, MarkPhase.Apply, Brand.FormMs + 1f, 1971f), MarkRecipe.Idle0, MarkRecipe.Idle0 + MarkRecipe.IdlePhases - 1);
        Assert.InRange(Brand.GatherFromMs, -300f, -150f);   // 4-6 steps of appearance, no explosion
        // the whole cut lights ON the tick (the moment Core amplifies), not before
        Assert.Equal(0f, m.HotAt(0, 2000f - Frame, out _));
        Assert.True(m.HotAt(0, 2000f, out var column) >= Brand.EtchAlpha - 0.001f);
        Assert.Equal(MarkRecipe.Edge, column);
        Assert.Equal(0f, m.HotAt(0, 2000f + Brand.EtchMs, out _));
        var quiet = Front(new[] { (2000f, 70, true), (4000f, 70, false) });
        Assert.Equal(Brand.EtchAlpha * Brand.QuietShare, quiet.HotAt(0, 2000f, out _), 3);
        var none = new MarkPerformance(Brand, 2, Array.Empty<(float, int, bool)>(), Array.Empty<(float, int)>(), 3, false, 0);
        Assert.Equal(MarkPhase.None, none.TryPhase(0, 5000f, out _));
    }

    [Fact]
    public void test_the_idle_is_quiet_a_stepped_swirl_and_no_light()
    {
        var m = Front(Plain);
        var changes = 0;
        var last = -1;
        for (var p = 2400f; p < 6400f; p += Frame)
        {
            Assert.Equal(0f, m.HotAt(0, p, out _));
            var phase = m.TryPhase(0, p, out var u);
            var column = m.ColumnAt(0, phase, u, p);
            Assert.InRange(column, MarkRecipe.Idle0, MarkRecipe.Idle0 + MarkRecipe.IdlePhases - 1);
            if (last >= 0 && column != last) changes++;
            last = column;
        }
        Assert.InRange(changes, 1, (int)(4000f / Brand.SwirlStepMs) + 1);
        // LOW AMPLITUDE: a slow step (at 400 ms it was the loudest thing the mark did), and only the lit band travels: the
        // cut's shape and the smoke roots are the same in every idle phase
        Assert.True(Brand.SwirlStepMs >= 700f);
        for (var k = 0; k < Brand.Stages; k++)
        {
            var (cut0, _) = Cell(k, MarkRecipe.Idle0);
            var (halo0, _) = Cell(Brand.HaloRow0 + k, MarkRecipe.Idle0);
            for (var ph = 1; ph < MarkRecipe.IdlePhases; ph++)
            {
                var (cut, _) = Cell(k, MarkRecipe.Idle0 + ph);
                var (halo, _) = Cell(Brand.HaloRow0 + k, MarkRecipe.Idle0 + ph);
                Assert.True(cut.Select(v => v >= 8).SequenceEqual(cut0.Select(v => v >= 8)), $"stage {k} phase {ph}: the cut changes shape");
                Assert.True(halo.SequenceEqual(halo0), $"stage {k} phase {ph}: the smoke roots are redrawn");
            }
        }
    }

    [Fact]
    public void test_a_sprawl_row_never_steps_its_swirl_in_lockstep()
    {
        var m = new MarkPerformance(Brand, 2, new[] { (2000f, 35, false) }, Array.Empty<(float, int)>(), 4, wholeWave: true, frontFullPercent: 0);
        var last = new int[4];
        for (var s = 0; s < 4; s++) last[s] = m.ColumnAt(s, MarkPhase.Reform, 1000f, 3000f);
        for (var p = 3000f + Frame; p < 7000f; p += Frame)
        {
            var changed = 0;
            for (var s = 0; s < 4; s++)
            {
                var c = m.ColumnAt(s, MarkPhase.Reform, 1000f, p);
                if (c != last[s]) changed++;
                last[s] = c;
            }
            Assert.True(changed <= 1, $"{changed} coils stepped on the same frame at {p:0}");
        }
    }

    // ── WHO CARRIES IT ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_exactly_one_creature_carries_it_from_the_first_tick_to_the_last_fall_and_it_keeps_its_depth()
    {
        var m = Front(Etch);
        Assert.Equal(0, m.FrontAt(6699f));
        Assert.Equal(1, m.FrontAt(6700f));
        // Core amplifies the front from the first tick, and the new front at once when the old one falls: the screen shows
        // exactly one carrier every moment (the new front carries a faint smoke while the strand flies to it)
        for (var p = 2000f; p < 12000f; p += Frame)
        {
            var carriers = Enumerable.Range(0, 4).Where(s => m.Carries(s, p)).ToArray();
            Assert.Single(carriers);
            Assert.Equal(m.FrontAt(p), carriers[0]);
        }
        Assert.Equal(MarkPhase.Waiting, m.TryPhase(1, 6700f, out var wait));
        Assert.True(wait < 0f);
        var lands = 6700f + Brand.FlightFromMs + Brand.FlightMs;
        Assert.Equal(MarkPhase.Reform, m.TryPhase(1, lands + 1f, out var u));
        Assert.Equal(MarkRecipe.Form0, m.ColumnAt(1, MarkPhase.Reform, u, lands + 1f));
        // AT THE SAME DEPTH: the stage it carried is the stage it keeps (ETCH's "keeps its depth when it moves")
        Assert.Equal(m.StageFor(0, 6699f), m.StageFor(1, lands + 1f));
        // short and clean: the coil comes apart, flies and is drawn in within about half a second
        Assert.True(Brand.FlightFromMs + Brand.FlightMs + Brand.ReformMs <= 480f);
        Assert.InRange(Brand.LoosenMs, 150f, Brand.FlightFromMs + Brand.FlightMs);
    }

    [Fact]
    public void test_a_host_that_falls_while_the_smoke_flies_to_it_is_passed_over()
    {
        // the front falls at 6700 and the next at 6800, before the smoke lands: it lands on the third, the one standing
        var m = Front(Plain, (6700f, 0), (6800f, 1));
        var lands = 6700f + Brand.FlightFromMs + Brand.FlightMs;
        Assert.Equal(MarkPhase.None, m.TryPhase(1, 6750f, out _));
        Assert.Equal(MarkPhase.Waiting, m.TryPhase(2, 6750f, out _));
        Assert.Equal(MarkPhase.Reform, m.TryPhase(2, lands + 1f, out _));
    }

    [Fact]
    public void test_a_jaws_kill_keeps_the_brand_on_its_host_until_the_jaws_shut()
    {
        // the screen draws a creature JAWS killed standing between the jaws until the snap: the fall it is given is the
        // snap's, so the brand stays on the body until then and only then comes apart
        var snap = 6700f + ReactionRecipes.For("seeker", "snare_jaws")!.SnapAtMs;
        var m = Front(Plain, (snap, 0), (8200f, 1));
        Assert.Equal(MarkPhase.Apply, m.TryPhase(0, 6800f, out _));
        Assert.Equal(MarkPhase.None, m.TryPhase(0, snap, out _));
        Assert.Equal(MarkPhase.Waiting, m.TryPhase(1, snap, out _));
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        Assert.Contains("{ shownAt = e.AtMs + answering.SnapAtMs; break; }", hunt);
        Assert.Contains("falls.Add((shownAt, e.Slot));", hunt);
    }

    [Fact]
    public void test_sprawl_hops_the_mark_down_the_row_and_anchor_keeps_the_front_full()
    {
        var falls = new[] { (6700f, 0) };
        var sprawl = new MarkPerformance(Brand, 2, new[] { (2000f, 35, false), (4000f, 35, false) }, falls, 4, wholeWave: true, frontFullPercent: 0);
        Assert.Equal(MarkPhase.Apply, sprawl.TryPhase(0, 2001f, out _));
        // Core amplifies every creature from the first tick: every one carries it from there (a faint smoke until its hop
        // lands), and the coils form one creature after another down the row, never all at once, within 700 ms
        Assert.All(Enumerable.Range(0, 4), s => Assert.True(sprawl.Carries(s, 2000f)));
        var reached = Enumerable.Range(1, 3).Select(s => Enumerable.Range(0, 2000).Select(i => 2000f + i)
                                                            .First(p => sprawl.TryPhase(s, p, out _) == MarkPhase.Reform)).ToArray();
        Assert.True(reached[0] < reached[1] && reached[1] < reached[2], string.Join(",", reached));
        Assert.True(reached[2] <= 2000f + 700f, $"the row forms only at {reached[2]}");
        var settled = reached[2] + Brand.ReformMs + 10f;
        Assert.All(Enumerable.Range(0, 4), s => Assert.True(sprawl.Carries(s, settled)));
        Assert.All(Enumerable.Range(0, 4), s => Assert.Equal(0, sprawl.StageFor(s, settled)));
        Assert.False(sprawl.Carries(0, 6700f));
        Assert.True(sprawl.Carries(1, 6700f));
        var anchor = new MarkPerformance(Brand, 2, new[] { (2000f, 35, false) }, falls, 4, wholeWave: true, frontFullPercent: 70);
        Assert.Equal(1, anchor.StageFor(0, 3000f));
        Assert.Equal(0, anchor.StageFor(1, 3000f));
        Assert.Equal(1, anchor.StageFor(1, 6700f));
    }

    [Fact]
    public void test_sprawl_never_spreads_out_of_a_fallen_creature()
    {
        // slot 1 falls at 2150, before its own hop lands: it is passed over (no strand TO a corpse), and the next hop
        // leaves the latest creature still standing that carries it (never FROM a corpse)
        var m = new MarkPerformance(Brand, 2, new[] { (2000f, 35, false), (4000f, 35, false) }, new[] { (2150f, 1) }, 4,
                                    wholeWave: true, frontFullPercent: 0);
        Assert.Equal(MarkPhase.None, m.TryPhase(1, 2150f, out _));
        // Core amplifies it until it falls: it carries the faint smoke meanwhile, never a coil
        Assert.Equal(MarkPhase.Waiting, m.TryPhase(1, 2100f, out _));
        for (var p = 2000f; p < 3500f; p += Frame)
        {
            Assert.NotEqual(MarkPhase.Reform, m.TryPhase(1, p, out _));
            foreach (var s in new[] { 2, 3 })
                if (m.TryPhase(s, p, out _) == MarkPhase.Reform)
                    Assert.True(m.Standing(m.HopFromOf(s), m.HopLeavesAt(s)) || m.HopFromOf(s) < 0, $"slot {s}'s strand leaves a fallen creature");
        }
        Assert.All(new[] { 0, 2, 3 }, s => Assert.True(m.Carries(s, 3000f)));
        Assert.Equal(0, m.HopFromOf(2));
    }

    [Fact]
    public void test_a_sprawl_deepen_ripples_down_the_row_never_a_whole_row_flash()
    {
        // WINNOW: 45 -> 55 crosses into the base stage on every coil at once in truth; on screen each chisel starts at its
        // own place in the spread
        var ticks = new[] { (2000f, 45, false), (4000f, 55, false), (6000f, 65, false) };
        var m = new MarkPerformance(Brand, 2, ticks, Array.Empty<(float, int)>(), 4, wholeWave: true, frontFullPercent: 0);
        var starts = new float[4];
        for (var s = 0; s < 4; s++)
            starts[s] = Enumerable.Range(0, 800).Select(i => 4000f + i).First(p => m.HotAt(s, p, out _) > 0.5f);
        Assert.Equal(4, starts.Distinct().Count());
        for (var s = 1; s < 4; s++)
            Assert.True(starts[s] - starts[s - 1] >= Brand.CarveStepMs, $"slots {s - 1} and {s} chisel together");
    }

    [Fact]
    public void test_under_sprawl_and_anchor_a_fall_never_pops_another_coils_deepen_and_the_front_never_retraces()
    {
        // SPRAWL + ANCHOR (the front full at 70) + WINNOW (45 -> 55 -> 65), a bite on every tick, the second creature
        // falling at 4050: the rest of the row shows the 55 % stage only under its own chisel, never at the fall
        var ticks = new[] { (2000f, 45, false), (4000f, 55, false), (6000f, 65, false) };
        var m = new MarkPerformance(Brand, 2, ticks, new[] { (4050f, 1) }, 4, wholeWave: true, frontFullPercent: 70,
                                    strikes: ticks.Select(t => t.Item1).ToArray());
        foreach (var slot in new[] { 2, 3 })
        {
            var start = 4000f + Brand.SettleMs + slot * Brand.CarveStepMs * 1.5f;   // after the bite, at its place in the spread
            Assert.Equal(0, m.DrawnStage(slot, start + Brand.CarveStepMs * 2f - 1f));
            Assert.Equal(1, m.DrawnStage(slot, start + Brand.CarveStepMs * 2f + 1f));
        }
        // the front's depth never changes under ANCHOR (70 %), so WINNOW's rise shows nothing on it
        for (var p = 4000f; p < 4800f; p += Frame)
            Assert.Equal(0f, m.HotAt(0, p, out _));
        Assert.Equal(70, m.DepthFor(0, 4500f));
        Assert.Equal(55, m.DepthFor(2, 4500f));
    }

    // ── A DEEPEN IS NEVER LOST, NEVER SKIPPED PAST ─────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_deepen_the_host_falls_in_the_middle_of_is_carried_and_cut_on_the_next_host()
    {
        // ETCH, the host's own bite on every tick (the chisel waits 200 ms), and the host falls at 8200: the 8000 tick's
        // chisel (220 -> 240 %) would start exactly then. The fallen host SHOWED deep3; the coil comes apart at deep3, is
        // drawn in on the next host at deep3, and is cut to deep4 there once whole
        var strikes = Etch.Select(t => t.Item1).ToArray();
        var m = new MarkPerformance(Brand, 2, Etch, new[] { (5200f, 0), (8200f, 1) }, 4, false, 0, strikes);
        Assert.Equal(4, m.DrawnStage(1, 8199f));
        Assert.Equal(5, m.StageFor(1, 8199f));
        var lands = 8200f + Brand.FlightFromMs + Brand.FlightMs;
        Assert.Equal(MarkPhase.Reform, m.TryPhase(2, lands + 1f, out _));
        Assert.Equal(4, m.DrawnStage(2, lands + 1f));                        // drawn in at what it showed
        // cut once the fallen creature's white death smoke has cleared off the new host (inside it, the step was unseen)
        var cut = Math.Max(lands + Brand.ReformMs, 8200f + Brand.DeathClearMs);
        for (var p = lands; p < cut; p += Frame)
            Assert.Equal(0f, m.HotAt(2, p, out _));
        Assert.True(m.HotAt(2, cut + 1f, out var column, out var stage) > 0.7f);
        Assert.Equal(MarkRecipe.Carve0, column);                              // the chisel, never a whole-coil flash
        Assert.Equal(5, stage);
        Assert.Equal(4, m.DrawnStage(2, cut + Brand.CarveStepMs * 2f - 1f));   // the new cut appears under the chisel
        Assert.Equal(5, m.DrawnStage(2, cut + Brand.CarveStepMs * 2f + 1f));
    }

    [Fact]
    public void test_a_deepen_while_the_mark_is_in_flight_is_cut_once_it_lands()
    {
        // the front falls at 3800; the 4000 tick raises 120 -> 170 % while the smoke flies: the coil is drawn in at the
        // stage it left with (deep1) and cut to deep2 once whole, by the chisel (never skipped, never the whole coil lit)
        var m = Front(Etch, (3800f, 0), (9000f, 1));
        var lands = 3800f + Brand.FlightFromMs + Brand.FlightMs;
        Assert.Equal(2, m.DrawnStage(1, lands + 1f));
        var cut = Math.Max(lands + Brand.ReformMs, 3800f + Brand.DeathClearMs);
        Assert.True(m.HotAt(1, cut + 1f, out var column, out var stage) > 0.7f);
        Assert.Equal(MarkRecipe.Carve0, column);
        Assert.Equal(3, stage);
        Assert.Equal(2, m.DrawnStage(1, cut - 1f));
        Assert.Equal(3, m.DrawnStage(1, cut + Brand.CarveStepMs * 2f + 1f));
        // a coil that lands at the depth it left with is cut once, faintly, on the Edge: once the fall's white death smoke
        // has cleared off it (at the re-form itself the smoke covered it), never before
        var plain = Front(Plain);
        var plainLands = 6700f + Brand.FlightFromMs + Brand.FlightMs;
        var recut = Math.Max(plainLands + Brand.ReformMs, 6700f + Brand.DeathClearMs);
        for (var p = plainLands; p < recut; p += Frame)
            Assert.Equal(0f, plain.HotAt(1, p, out _));
        Assert.True(plain.HotAt(1, recut + 1f, out var pc) is > 0.2f and < 0.6f);
        Assert.Equal(MarkRecipe.Edge, pc);
    }

    // ── IT NEVER COSTS THE FRAME ─────────────────────────────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static float ReadAll(MarkPerformance m)
    {
        var sink = 0f;
        for (var p = 0f; p < 12000f; p += Frame)
            for (var s = 0; s < 4; s++)
            {
                sink += m.HotAt(s, p, out var hc) + hc + m.StageFor(s, p) + m.FrontAt(p) + m.DepthAt(p) + m.DrawnStage(s, p);
                var phase = m.TryPhase(s, p, out var u);
                if (phase != MarkPhase.None) sink += m.ColumnAt(s, phase, u, p);
            }
        return sink;
    }

    [Fact]
    public void test_reading_the_mark_on_the_playhead_allocates_nothing()
    {
        var m = new MarkPerformance(Brand, 2, new[] { (2000f, 120, false), (4000f, 170, true), (6000f, 220, false) },
                                    new[] { (6700f, 0), (8200f, 1) }, 4, wholeWave: true, frontFullPercent: 70);
        var sink = ReadAll(m) + ReadAll(m);   // warm every method on the path (tiering)
        // the least of three runs: a real per-frame allocation would show in every one
        var least = long.MaxValue;
        for (var run = 0; run < 3; run++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            sink += ReadAll(m);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.Equal(0L, least);
        Assert.True(sink > 0f);
    }

    // ── THE SCREEN (HuntScreen needs a device: its text is the contract) ────────────────────────────────────────────

    [Fact]
    public void test_the_brand_is_drawn_on_the_body_between_the_creature_and_its_hit_flash()
    {
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        Assert.Equal(3, hunt.Split("DrawMarkOn(b, ").Length - 1);
        var loop = hunt.IndexOf("DrawMarkOn(b, i, squash, stripKey);", StringComparison.Ordinal);
        Assert.True(loop > 0);
        var silhouette = hunt.LastIndexOf("SilhouetteOver(b, stripKey, box,", loop, StringComparison.Ordinal);
        var flash = hunt.IndexOf("FlashOver(b, stripKey, box,", loop, StringComparison.Ordinal);
        Assert.True(silhouette > 0 && silhouette < loop && flash > loop, "the brand is burned into the body: after it, before its flash");
        var row = hunt.IndexOf("else DrawNormalEnemy(b, attacking);", StringComparison.Ordinal);
        var loose = hunt.IndexOf("looseMark.DrawLoose(", StringComparison.Ordinal);
        var reactions = hunt.IndexOf("foreach (var r in _reactions) r.DrawMaterial(", StringComparison.Ordinal);
        Assert.True(row > 0 && row < loose && loose < reactions);
        var begin = hunt.IndexOf("_vfx.BeginLight", StringComparison.Ordinal);
        var end = hunt.IndexOf("_vfx.EndLight", begin, StringComparison.Ordinal);
        Assert.DoesNotContain("_mark", hunt[begin..end]);
        Assert.Contains("if (wave[ej].Kind == BattleEventKind.Marked) { percent = wave[ej].Slot; break; }", hunt);
        Assert.Contains("markSk.Def.AmplifyWholeWave, (int)MathF.Round(markSk.Def.Rule.AmplifyFrontFull * 100f)", hunt);
        // the authored body point of the frame just drawn (looked up once per texture)
        // (by the strip key the draw used: a reverse texture lookup missed strips loaded on first use)
        Assert.Contains("var bodyPoint = stripKey is null ? null : MarkPoints.For(stripKey)?.ToArena(frame);", hunt);
        Assert.Contains("DrawMarkOn(b, 0, bossSquash, key);", hunt);
        Assert.DoesNotContain("KeyOf(frame.Texture)", hunt);
    }
}

/// <summary>A tiny PNG reader for the atlas tests (8-bit RGBA, non-interlaced; the atlas the generator writes).</summary>
internal sealed class SixLaborsFree : IDisposable
{
    private readonly byte[] _rgba;
    private readonly int _w;

    private SixLaborsFree(byte[] rgba, int w) { _rgba = rgba; _w = w; }

    public int Width => _w;

    public int Height => _rgba.Length / 4 / _w;

    /// <summary>Every pixel's opacity (alpha above 100, the probes' and the authoring tool's rule), row-major.</summary>
    public bool[] Opaque()
    {
        var o = new bool[_rgba.Length / 4];
        for (var i = 0; i < o.Length; i++) o[i] = _rgba[i * 4 + 3] > 100;
        return o;
    }

    /// <summary>The pixels of square frame <paramref name="k"/> brighter than <paramref name="luma"/> (an eye's white speck), in
    /// the frame's own px, from its upper 70 %.</summary>
    public IEnumerable<(int X, int Y)> BrightSpecks(int k, int size, int luma)
    {
        for (var y = 0; y < Height * 7 / 10; y++)
            for (var x = 0; x < size; x++)
            {
                var i = (y * _w + k * size + x) * 4;
                if (_rgba[i + 3] > 200 && (_rgba[i] * 3 + _rgba[i + 1] * 6 + _rgba[i + 2]) / 10 > luma)
                    yield return (x, y);
            }
    }

    /// <summary>The opaque centroid of square frame <paramref name="k"/>, in the frame's own px.</summary>
    public Microsoft.Xna.Framework.Vector2 Centroid(int k, int size)
    {
        double sx = 0, sy = 0;
        var n = 0;
        for (var y = 0; y < Height; y += 2)
            for (var x = 0; x < size; x += 2)
                if (_rgba[(y * _w + k * size + x) * 4 + 3] > 100) { sx += x; sy += y; n++; }
        return n == 0 ? default : new Microsoft.Xna.Framework.Vector2((float)(sx / n), (float)(sy / n));
    }

    /// <summary>Whether square frames <paramref name="a"/> and <paramref name="b"/> of a strip are the same picture.</summary>
    public bool SameFrame(int a, int b, int size)
    {
        for (var y = 0; y < Height; y++)
            if (!_rgba.AsSpan((y * _w + a * size) * 4, size * 4).SequenceEqual(_rgba.AsSpan((y * _w + b * size) * 4, size * 4)))
                return false;
        return true;
    }

    /// <summary>The first and last opaque rows of the square frame from column <paramref name="x0"/>.</summary>
    public (int Top, int Bottom) OpaqueRows(int x0, int size)
    {
        int top = -1, bottom = -1;
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < size; x++)
                if (_rgba[(y * _w + x0 + x) * 4 + 3] > 100)
                {
                    if (top < 0) top = y;
                    bottom = y;
                    break;
                }
        return (top, bottom);
    }

    public static SixLaborsFree Load(string path)
    {
        var png = File.ReadAllBytes(path);
        int Be(int at) => (png[at] << 24) | (png[at + 1] << 16) | (png[at + 2] << 8) | png[at + 3];
        var w = Be(16);
        var h = Be(20);
        Assert.Equal(8, png[24]);
        Assert.Equal(6, png[25]);
        using var idat = new MemoryStream();
        for (var at = 8; at < png.Length;)
        {
            var len = Be(at);
            var type = System.Text.Encoding.ASCII.GetString(png, at + 4, 4);
            if (type == "IDAT") idat.Write(png, at + 8, len);
            at += 12 + len;
        }
        idat.Position = 2;   // the zlib header
        using var z = new System.IO.Compression.DeflateStream(idat, System.IO.Compression.CompressionMode.Decompress);
        var raw = new byte[(w * 4 + 1) * h];
        var read = 0;
        while (read < raw.Length)
        {
            var n = z.Read(raw, read, raw.Length - read);
            if (n <= 0) break;
            read += n;
        }
        var rgba = new byte[w * h * 4];
        var stride = w * 4;
        for (var y = 0; y < h; y++)
        {
            var f = raw[y * (stride + 1)];
            for (var x = 0; x < stride; x++)
            {
                var v = raw[y * (stride + 1) + 1 + x];
                var a = x >= 4 ? rgba[y * stride + x - 4] : 0;
                var b = y > 0 ? rgba[(y - 1) * stride + x] : 0;
                var c = x >= 4 && y > 0 ? rgba[(y - 1) * stride + x - 4] : 0;
                int pr = f switch
                {
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => 0,
                };
                rgba[y * stride + x] = (byte)(v + pr);
            }
        }
        return new SixLaborsFree(rgba, w);
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    /// <summary>A square cell: its alpha and its grey (the red channel of the white-tinted art).</summary>
    public (byte[] A, byte[] L) Cell(int x0, int y0, int size)
    {
        var a = new byte[size * size];
        var l = new byte[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var i = ((y0 + y) * _w + x0 + x) * 4;
                a[y * size + x] = _rgba[i + 3];
                l[y * size + x] = _rgba[i];
            }
        return (a, l);
    }

    public void Dispose() { }
}
