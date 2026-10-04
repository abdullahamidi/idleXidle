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
/// brief 2026-09-29): "That enemy has been branded... It quietly persists there. When the mark deepens, it bites further
/// inward. If it spreads or transfers, the same mark migrates cleanly to the next target." A state on the enemy: not a
/// projectile, a field, a trap or a champion performance. These pin the mark's TRUTH (who carries it, its phases, the
/// stage it shows and when, the SPRAWL schedule, the authored body points), which the curse presents (ADR-013); the etched
/// cut's atlas and drawing are gone, and with them their tests.
/// </summary>
public class brand_mark_test
{
    private static readonly MarkRecipe Brand = MarkRecipes.SeekerBrand;
    private const float Frame = 1000f / 60f;

    /// <summary>The footprint the authored body points were reviewed with (mark_points.py: a share of the creature's
    /// visible height, kept on its body); data, not a drawing value.</summary>
    private const float PointFootprint = 0.28f;

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
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
        Assert.Same(Brand, MarkRecipes.For("oathbound", "sign_brand"));   // BRAND on another hunter: the same instance (the agnostic tier)
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
        Assert.Equal((-1, 0, -1), FieldRoles.Choose(alone, "oathbound"));   // another hunter's BRAND: the same mark (the agnostic tier)
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
            // the halo the tool cleared the head with is the recipe's (the points the curse hangs from keep off the head)
            Assert.Equal(Brand.HaloWidthShare, halo[0].GetSingle(), 3);
            Assert.Equal(Brand.HaloHeightShare, halo[1].GetSingle(), 3);
            using var img = SixLaborsFree.Load(Path.Combine(dir, name + ".png"));
            var size = img.Height;
            Assert.Equal(img.Width / size, points.Count);
            var opaque = img.Opaque();
            // the footprint the points were reviewed with: a share of the visible height, an ellipse (4 : 5)
            var (top, bottom) = img.OpaqueRows(0, size);
            var rx = PointFootprint * (bottom - top + 1) / 2f;
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
            var rx = PointFootprint * (bottom - top + 1) / 2f;
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
        Assert.Equal(1, Brand.StageOf(70));     // DEPTH 1: base BRAND
        Assert.Equal(1, Brand.StageOf(120));    // DEPTH 1: ETCH's first tick
        Assert.Equal(2, Brand.StageOf(170));    // DEPTH 2
        Assert.Equal(3, Brand.StageOf(220));    // DEPTH 3, the final readable silhouette
        Assert.Equal(3, Brand.StageOf(240));
        Assert.Equal(3, Brand.StageOf(320));
        for (var pct = 0; pct < 400; pct++)
            Assert.True(Brand.StageOf(pct) <= Brand.StageOf(pct + 1));
        // THREE VISIBLE DEPTHS quantize the gameplay depths: 220 % and 240 % are one picture
        Assert.Equal(4, Brand.Stages);
        Assert.Equal(Brand.StageOf(220), Brand.StageOf(240));
    }

    [Fact]
    public void test_a_deepen_is_shown_once_the_hosts_own_bite_has_settled()
    {
        // on the tick that deepened (120 -> 170 %: depth 1 -> 2) the old stage stays until the deepen's second step
        var m = Front(Etch);
        Assert.Equal(m.StageFor(0, 3999f), m.DrawnStage(0, 4000f + 1f));
        Assert.Equal(m.StageFor(0, 4001f), m.DrawnStage(0, 4000f + Brand.CarveStepMs * 2f + 1f));
        // 220 -> 240 % stays on the fully branded depth: the picture never changes
        var host = m.FrontAt(8000f);
        Assert.Equal(m.DrawnStage(host, 7999f), m.DrawnStage(host, 8000f + Brand.CarveStepMs * 3f + 50f));
        // a bite on the tick: the deepen waits until its attack clip has ended (the depth is true on the tick all the same)
        var settled = new MarkPerformance(Brand, 2, Etch, new[] { (6700f, 0), (8200f, 1) }, 4, false, 0,
                                          Etch.Select(t => t.Item1).ToArray());
        Assert.Equal(2, settled.StageFor(0, 4001f));
        Assert.Equal(1, settled.DrawnStage(0, 4000f + Brand.SettleMs + 1f));
        Assert.Equal(2, settled.DrawnStage(0, 4000f + Brand.SettleMs + Brand.CarveStepMs * 2f + 1f));
        // never over a bite: clear of its wind-up before the strike and of its clip after it
        MarkPerformance With(params float[] strikes) => new(Brand, 2, Etch, Array.Empty<(float, int)>(), 4, false, 0, strikes);
        Assert.Equal(4000f + Brand.SettleMs, With(4000f).SettledStart(4000f));
        Assert.Equal(4150f + Brand.SettleMs, With(4150f).SettledStart(4000f));
        Assert.Equal(4000f, With(3500f).SettledStart(4000f));
        Assert.Equal(4000f, With(4000f + Brand.CarveStepMs * 3f + Brand.SettleBeforeMs + 1f).SettledStart(4000f));
        Assert.Equal(4300f + Brand.SettleMs, With(4000f, 4300f).SettledStart(4000f));
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        Assert.Contains("if (wave[ej].Kind == BattleEventKind.EnemyStrike) markStrikes.Add(wave[ej].AtMs);", hunt);
    }

    // ── APPLY, IDLE ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_mark_is_true_from_the_first_tick_and_its_cut_lands_on_it()
    {
        var m = Front(Plain);
        Assert.Equal(2000f, m.FirstTickMs);
        Assert.Equal(MarkPhase.None, m.TryPhase(0, 2000f + Brand.GatherFromMs - 1f, out _));
        Assert.Equal(MarkPhase.Apply, m.TryPhase(0, 2000f + Brand.GatherFromMs + 1f, out var u));
        Assert.Equal(Brand.GatherFromMs + 1f, u, 3);   // u from the tick: the curse's bloom starts ON it
        Assert.InRange(Brand.GatherFromMs, -300f, -150f);
        // a tick beside an action, a reaction or the field's crush is quiet (the curse's light gives way there)
        var quiet = Front(new[] { (2000f, 70, true), (4000f, 70, false) });
        Assert.True(quiet.TickQuietNear(2000f, 450f));
        Assert.False(quiet.TickQuietNear(4000f, 450f));
        Assert.False(m.TickQuietNear(2000f, 450f));
        var none = new MarkPerformance(Brand, 2, Array.Empty<(float, int, bool)>(), Array.Empty<(float, int)>(), 3, false, 0);
        Assert.Equal(MarkPhase.None, none.TryPhase(0, 5000f, out _));
    }

    [Fact]
    public void test_a_transfer_waits_on_the_new_front_then_seeps_in()
    {
        // TRANSFER: nothing is drawn between the bodies; the old host's curse collapses on its body, the new front carries
        // a faint shade, and the same mark seeps in on it
        var m = Front(Etch);
        var lands = 6700f + Brand.FlightFromMs + Brand.FlightMs;
        Assert.Equal(MarkPhase.Waiting, m.TryPhase(1, 6750f, out _));      // the new front carries a dark stain at once
        Assert.Equal(MarkPhase.Reform, m.TryPhase(1, lands + 1f, out _));  // then the mark seeps in on it
        // ...once the new front's own bite AND the reaction it draws have played out (JAWS snaps on the bitten body
        // ~333 ms after the contact: an etch-in there was never seen)
        var bitten = new MarkPerformance(Brand, 2, Etch, new[] { (6700f, 0), (8200f, 1) }, 4, false, 0, new[] { 7000f });
        Assert.Equal(MarkPhase.Waiting, bitten.TryPhase(1, 7000f + Brand.SettleMs + 1f, out _));
        Assert.Equal(MarkPhase.Reform, bitten.TryPhase(1, 7000f + Brand.TransferSettleMs + 1f, out _));
        Assert.True(Brand.TransferSettleMs >= 500f && Brand.TransferSettleMs + Brand.ReformMs <= 1000f - Brand.SettleBeforeMs);
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
        Assert.Equal(1f, u, 3);
        // AT THE SAME DEPTH: the stage it carried is the stage it keeps (ETCH's "keeps its depth when it moves")
        Assert.Equal(m.StageFor(0, 6699f), m.StageFor(1, lands + 1f));
        // short and clean: the coil comes apart, flies and is drawn in within about half a second
        Assert.True(Brand.FlightFromMs + Brand.FlightMs + Brand.ReformMs <= 480f);
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
    public void test_sprawl_propagates_from_its_source_and_anchor_keeps_the_front_full()
    {
        var falls = new[] { (6700f, 0) };
        var sprawl = new MarkPerformance(Brand, 2, new[] { (2000f, 35, false), (4000f, 35, false) }, falls, 4, wholeWave: true, frontFullPercent: 0);
        Assert.Equal(MarkPhase.Apply, sprawl.TryPhase(0, 2001f, out _));
        // Core amplifies every creature from the first tick: every one carries it from there (a dark stain until its
        // thread lands). The condition PROPAGATES from its source: every thread leaves the front's mark, SpreadStaggerMs
        // after the one before, and the row etches near-simultaneously (one after another, never on one frame)
        Assert.All(Enumerable.Range(0, 4), s => Assert.True(sprawl.Carries(s, 2000f)));
        for (var s = 1; s < 4; s++)
        {
            Assert.Equal(0, sprawl.HopFromOf(s));
            if (s > 1) Assert.Equal(Brand.SpreadStaggerMs, sprawl.HopLeavesAt(s) - sprawl.HopLeavesAt(s - 1), 3);
        }
        var reached = Enumerable.Range(1, 3).Select(s => Enumerable.Range(0, 2000).Select(i => 2000f + i)
                                                            .First(p => sprawl.TryPhase(s, p, out _) == MarkPhase.Reform)).ToArray();
        Assert.True(reached[0] < reached[1] && reached[1] < reached[2], string.Join(",", reached));
        Assert.True(reached[2] - reached[0] <= 2f * Brand.SpreadStaggerMs + 1f, $"the row etches over {reached[2] - reached[0]} ms");
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
        // WINNOW: 45 -> 55 crosses into the base stage on every creature at once in truth; on screen each deepen is shown
        // at its own place in the spread (the curse's new territory blooms there)
        var ticks = new[] { (2000f, 45, false), (4000f, 55, false), (6000f, 65, false) };
        var m = new MarkPerformance(Brand, 2, ticks, Array.Empty<(float, int)>(), 4, wholeWave: true, frontFullPercent: 0);
        var starts = new float[4];
        for (var s = 0; s < 4; s++)
            starts[s] = Enumerable.Range(0, 800).Select(i => 4000f + i).First(p => m.DrawnStage(s, p) == 1);
        Assert.Equal(4, starts.Distinct().Count());
        for (var s = 1; s < 4; s++)
            Assert.True(starts[s] - starts[s - 1] >= Brand.CarveStepMs, $"slots {s - 1} and {s} deepen together");
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
            Assert.Equal(1, m.DrawnStage(0, p));
        Assert.Equal(70, m.DepthFor(0, 4500f));
        Assert.Equal(55, m.DepthFor(2, 4500f));
    }

    // ── A DEEPEN IS NEVER LOST, NEVER SKIPPED PAST ─────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_deepen_the_host_falls_in_the_middle_of_is_carried_and_cut_on_the_next_host()
    {
        // ETCH, the host's own bite on every tick (the phrase waits for it): the 6000 tick (170 -> 220 %: depth 2 -> 3)
        // starts its phrase at 6300, and the host falls at 6320, before the new depth shows. It SHOWED depth 2; the mark
        // collapses at depth 2, seeps in on the next host at depth 2, and is cut to depth 3 there once the death smoke clears
        var strikes = Etch.Select(t => t.Item1).ToArray();
        var m = new MarkPerformance(Brand, 2, Etch, new[] { (5200f, 0), (6320f, 1) }, 4, false, 0, strikes);
        Assert.Equal(2, m.DrawnStage(1, 6319f));
        Assert.Equal(3, m.StageFor(1, 6319f));
        var lands = 6320f + Brand.FlightFromMs + Brand.FlightMs;
        Assert.Equal(MarkPhase.Reform, m.TryPhase(2, lands + 1f, out _));
        Assert.Equal(2, m.DrawnStage(2, lands + 1f));
        var cut = m.SettledStart(Math.Max(lands + Brand.ReformMs, 6320f + Brand.DeathClearMs));
        for (var p = lands; p < cut + Brand.CarveStepMs * 2f - 1f; p += Frame)
            Assert.Equal(2, m.DrawnStage(2, p));
        Assert.Equal(3, m.DrawnStage(2, cut + Brand.CarveStepMs * 2f + 1f));
    }

    [Fact]
    public void test_a_deepen_while_the_mark_is_in_flight_is_cut_once_it_lands()
    {
        // the front falls at 3800; the 4000 tick raises 120 -> 170 % while the mark moves: it seeps in at the depth it
        // left with (depth 1) and is cut to depth 2 once whole (never skipped, never the whole mark lit)
        var m = Front(Etch, (3800f, 0), (9000f, 1));
        var lands = 3800f + Brand.FlightFromMs + Brand.FlightMs;
        Assert.Equal(1, m.DrawnStage(1, lands + 1f));
        var cut = Math.Max(lands + Brand.ReformMs, 3800f + Brand.DeathClearMs);
        Assert.Equal(1, m.DrawnStage(1, cut - 1f));
        Assert.Equal(2, m.DrawnStage(1, cut + Brand.CarveStepMs * 2f + 1f));
    }

    // ── IT NEVER COSTS THE FRAME ─────────────────────────────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static float ReadAll(MarkPerformance m)
    {
        var sink = 0f;
        for (var p = 0f; p < 12000f; p += Frame)
            for (var s = 0; s < 4; s++)
            {
                sink += m.StageFor(s, p) + m.FrontAt(p) + m.DepthAt(p) + m.DrawnStage(s, p) + (m.TickQuietNear(p, 450f) ? 1 : 0);
                var phase = m.TryPhase(s, p, out var u);
                if (phase != MarkPhase.None) sink += u;
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
    public void test_the_curse_is_drawn_on_the_body_between_the_creature_and_its_hit_flash()
    {
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        Assert.Equal(3, hunt.Split("DrawMarkOn(b, ").Length - 1);
        // (the creature's draw colour rides along: the curse pass draws the frame with it, as the creature's one draw)
        var loop = hunt.IndexOf("DrawMarkOn(b, i, stripKey, creatureTint, look.IdleStrip, cursed);", StringComparison.Ordinal);
        Assert.True(loop > 0);
        var silhouette = hunt.LastIndexOf("SilhouetteOver(b, stripKey, box,", loop, StringComparison.Ordinal);
        var flash = hunt.IndexOf("FlashOver(b, stripKey, box,", loop, StringComparison.Ordinal);
        Assert.True(silhouette > 0 && silhouette < loop && flash > loop, "the curse is on the body: after it, before its flash");
        Assert.Contains("DrawMarkOn(b, 0, stripKey, enterTint, look.IdleStrip, cursed);", hunt);
        Assert.Contains("DrawMarkOn(b, 0, key, bossTint, bossArt?.IdleStrip, cursed);", hunt);
        // nothing is drawn between the bodies any more (the etched cut's loosening and strands are gone)
        Assert.DoesNotContain("DrawLoose(", hunt);
        // the body shudders on every creature path, the single creature's and the boss's too, and a waiting creature's
        // faint shade rides its own draw tint
        Assert.Equal(3, hunt.Split(".Convulse(").Length - 1);
        Assert.Equal(3, hunt.Split(".Shade(").Length - 1);
        // a dying host's curse leaves it on the pack's and the boss's death frames alike
        Assert.Contains("DrawCurseLeaving(b, slot, EnemyTint * fade, idleKey, cursed);", hunt);
        Assert.Contains("DrawCurseLeaving(b, 0, EnemyTint, bossArt.IdleStrip, cursedFall);", hunt);
        Assert.Contains("PinMarkOnDeath(0, bossArt.DeathStrip);", hunt);
        // A CURSED BODY IS DRAWN ONCE: on every living and falling path the arena hands the creature's draw to the curse
        // pass (its frame drawn transparent) whenever the pass will run; drawn twice, its soft edges hardened
        Assert.Equal(4, hunt.Split("cursed ? Color.Transparent : ").Length - 1);
        Assert.Equal(1, hunt.Split("cursedFall ? Color.Transparent : ").Length - 1);
        Assert.Equal(3, hunt.Split("= CurseDraws(").Length - 1);
        Assert.Equal(2, hunt.Split("= CurseLeavingDraws(").Length - 1);
        Assert.Contains("curse.PassDue(slot, _playheadMs, CurseHostData.For(restKey))", hunt);
        // nothing of the curse in the light pass
        var begin = hunt.IndexOf("_vfx.BeginLight", StringComparison.Ordinal);
        var end = hunt.IndexOf("_vfx.EndLight", begin, StringComparison.Ordinal);
        Assert.DoesNotContain("_mark", hunt[begin..end]);
        Assert.DoesNotContain("_curse", hunt[begin..end]);
        Assert.Contains("if (wave[ej].Kind == BattleEventKind.Marked) { percent = wave[ej].Slot; break; }", hunt);
        Assert.Contains("markSk.Def.AmplifyWholeWave, (int)MathF.Round(markSk.Def.Rule.AmplifyFrontFull * 100f)", hunt);
        // the authored body point of the frame just drawn, by the strip key the draw used (a reverse texture lookup
        // missed strips loaded on first use)
        Assert.Contains("var bodyPoint = stripKey is null ? null : MarkPoints.For(stripKey)?.ToArena(frame);", hunt);
        Assert.DoesNotContain("KeyOf(frame.Texture)", hunt);
    }

    [Fact]
    public void test_the_curse_is_brands_default_presentation_and_the_prototype_is_gone()
    {
        // BRAND's curse is THE presentation whenever the mark recipe is on (RH_MARK_RECIPES=0 is the uncursed twin): built
        // in the mark's own block, loaded once and reused by every wave
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        var block = hunt.IndexOf("MarkRecipes.For(Character.Id, markSk.Def.Id) is { } markRecipe", StringComparison.Ordinal);
        var load = hunt.IndexOf("_curse = _curseLoaded ??= CursePresentation.Load(_ui.Device);", StringComparison.Ordinal);
        var wave = hunt.IndexOf("_curse.BeginWave(_mark, _run.LastWaveCreatures.Count);", StringComparison.Ordinal);
        Assert.True(block > 0 && load > block && wave > load);
        Assert.Contains("CurseHostData.Warm();", hunt);
        // no dev selector, no concept B / C, no stencil, no etched atlas
        var src = Directory.GetFiles(RepoFile("src", "IdleXIdle.Game"), "*.cs", SearchOption.AllDirectories)
                           .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                           .Select(File.ReadAllText).ToArray();
        foreach (var gone in new[] { "RH_BRAND_CONCEPT", "RH_CURSE_NOCLIP", "CursePrototype", "CurseConcept", "new AlphaTestEffect",
                                     "new DualTextureEffect", "Depth24Stencil8", "fxp_seeker_brand" })
            Assert.DoesNotContain(src, code => code.Contains(gone, StringComparison.Ordinal));
        Assert.False(File.Exists(RepoFile("assets", "art", "VFX", "parts", "fxp_seeker_brand.png")), "the etched atlas is still a runtime asset");
        Assert.True(File.Exists(RepoFile("tools", "asset-pipeline", "v2", "keypose_sources", "seeker_brand_history", "fxp_seeker_brand_etched_cut_39b59aaa.png")));
        Assert.True(File.Exists(RepoFile("tools", "asset-pipeline", "v2", "seeker_brand_cut.py")));
    }
}
