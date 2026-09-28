using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IdleXIdle.Game.Presentation;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE FIELD CONTRACT (ADR-011, the FIELD / AURA archetype; PRESS is the reference, the owner's brief 2026-09-28): one
/// visual sentence, "the Seeker's field pulses -> a wave goes out -> that enemy gets crushed". A quiet field around the
/// Seeker; a small compression before each tick; one broad pressure FRONT that leaves him and ARRIVES on the fight's
/// tick; a short crush on the creature the tick affected (two pressing arcs and its body buckling, feet on the floor);
/// a settle before the next tick. Presentation only: Core decides the tick and its target.
/// </summary>
public class press_field_test
{
    private static readonly FieldRecipe Press = FieldRecipes.SeekerPress;
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

    private static IEnumerable<float> Frames(float from, float to)
    {
        for (var u = from; u <= to; u += Frame) yield return u;
    }

    [Fact]
    public void test_press_is_the_seekers_field_recipe_and_only_press()
    {
        Assert.Same(Press, FieldRecipes.For("seeker", "hammer_press"));
        // a presentation belongs to its SKILL: another field, or PRESS on another hunter, keeps the generic held aura
        Assert.Null(FieldRecipes.For("seeker", "drain_wilt"));
        Assert.Null(FieldRecipes.For("oathbound", "hammer_press"));
        // PRESS ticks every 2 s (the fight's clock, the catalogue's IntervalMs): the whole phrase fits between two ticks
        var press = IdleXIdle.Core.Builds.SkillCatalogue.All.Single(d => d.Id == "hammer_press");
        Assert.Equal(IdleXIdle.Core.Builds.SkillKind.Field, press.Kind);
        Assert.True(Press.EndMs - Press.ContractFromMs < press.IntervalMs * 0.6f, "the phrase runs into the next tick");
    }

    [Fact]
    public void test_four_pixel_hard_parts_composed_at_runtime_their_cells_pinned()
    {
        Assert.Equal((Press.FieldCell.X, Press.FieldCell.Y), PngSize(Press.FieldKey));
        Assert.Equal((4 * Press.WaveCell.X, Press.WaveCell.Y), PngSize(Press.WaveKey));        // body, edge, echo, dissolve
        Assert.Equal((Press.ClampCells * Press.ClampCell.X, Press.ClampCell.Y), PngSize(Press.ClampKey));   // whole + 3 dissolving + edge
        // the chevron accent ("> TARGET <") was removed: read as arrowheads / a lock-on reticle (the brief bans arrows)
        Assert.False(File.Exists(RepoFile("assets", "art", "VFX", "parts", "fxp_seeker_press_crease.png")));
        Assert.Equal(5, Press.ClampCells);
        Assert.Equal(FieldRecipe.ClampEdgeCell, Press.ClampCells - 1);
        var spans = JsonDocument.Parse(File.ReadAllText(RepoFile("tools", "asset-pipeline", "v2", "keypose_sources", "seeker_press_spans.json"))).RootElement;
        Assert.Equal(Press.WaveLeadX, spans.GetProperty("wave_lead_x").GetSingle(), 1);
        Assert.Equal(Press.ClampPressY, spans.GetProperty("clamp_press_y").GetSingle(), 1);
        Assert.Equal(4, spans.GetProperty("wave_cells").GetInt32());
        Assert.Equal(Press.ClampCells, spans.GetProperty("clamp_cells").GetInt32());
        // PIXEL-HARD (the owner: "too smooth, too soft and too vector-like"): authored at a LOW logical resolution chosen
        // so the drawn pixel lands near the world's (~3 px): the wave at 1/5, the clamp at 1/8; upscaled NEAREST, one source
        // pixel of soften at most, in value bands; holes and dissolves AUTHORED (erosion, a few large holes), never a
        // uniform random mask (that read as grain)
        Assert.Equal(5, spans.GetProperty("wave_logical_resolution").GetInt32());
        Assert.Equal(8, spans.GetProperty("clamp_logical_resolution").GetInt32());
        var gen = File.ReadAllText(RepoFile("tools", "asset-pipeline", "v2", "seeker_press.py"));
        Assert.Contains("np.kron(a, k)", gen);
        Assert.Contains("a = np.clip(blur(a, 0.4), 0, 1)", gen);
        Assert.Contains("DARK, MID, LIGHT, PEAK = ", gen);
        Assert.Contains("def erode(", gen);
        Assert.DoesNotContain("def chunks(", gen);
        Assert.Contains("light=EDGE_LIGHT", gen);                          // the clamp's edge cell: a lit line, not a slab
        // no soft afterimage: one SOLID darker echo a frame behind, and no body glow flattening the value bands
        Assert.DoesNotContain(typeof(FieldRecipe).GetProperties(), pr => pr.Name.Contains("Afterimage") || pr.Name.Contains("BodyGlow"));
        Assert.InRange(Press.EchoAlpha, 0.2f, 0.45f);
        Assert.InRange(Press.EchoLagMs, 10f, 20f);
        // one travelling shape, never particles, debris or bullets
        foreach (var banned in new[] { "particle", "debris", "bullet" })
            Assert.DoesNotContain(banned, typeof(FieldRecipe).GetProperties().Select(p => p.Name.ToLowerInvariant()));
    }

    [Fact]
    public void test_the_field_is_quiet_then_compresses_before_the_tick_then_springs_out()
    {
        var r = Press;
        // A: quiet at rest (never dominating), breathing a little
        var rest = FieldPerformance.Field(r, float.NaN, 0f);
        Assert.Equal(1f, rest.Scale);
        Assert.InRange(rest.Alpha, 0.18f, 0.32f);                                   // quiet, but present (the first review)
        // FORWARD of his middle, so its leaning edge stands in front of his enemy-facing side, where it can be seen
        Assert.InRange(r.FieldCentre.X, 0.55f, 0.75f);
        Assert.InRange(r.FieldGlowShare, 0.15f, 0.45f);
        Assert.True(FieldPerformance.Field(r, float.NaN, 1f).Alpha - FieldPerformance.Field(r, float.NaN, -1f).Alpha <= 0.1f);
        // B: it draws IN and gathers from ContractFromMs to the launch: small but visible
        var tight = FieldPerformance.Field(r, r.LaunchMs - 0.01f, 0f);
        Assert.InRange(tight.Scale, 0.8f, 0.92f);
        Assert.True(tight.Alpha > rest.Alpha + 0.1f, "the compression does not gather");
        var before = Frames(r.ContractFromMs, r.LaunchMs - 1f).Select(u => FieldPerformance.Field(r, u, 0f)).ToArray();
        Assert.True(before.Zip(before.Skip(1), (a, b) => b.Scale <= a.Scale + 1e-4f).All(x => x), "the compression is not one gathering");
        // C: it springs back OUT past its size as it lets the front go, then settles
        var after = Frames(r.LaunchMs, r.LaunchMs + r.SpringMs).Select(u => FieldPerformance.Field(r, u, 0f).Scale).ToArray();
        Assert.True(after.Max() > 1.06f, "the field does not release its pressure");
        // THE RELEASE SNAPS (the start of the hard beat): it is out past its size within ~2 frames of the launch
        Assert.True(FieldPerformance.Field(r, r.LaunchMs + 2f * Frame, 0f).Scale > 1.05f, "the release is a gentle spring");
        Assert.True(r.SpringScale - r.ContractScale >= 0.25f, "too little contrast between the tight state and the release");
        Assert.Equal(1f, FieldPerformance.Field(r, r.LaunchMs + r.SpringMs + 1f, 0f).Scale, 3);
        // the field never out-brightens the crush
        Assert.True(r.ContractAlpha < r.ClampPeakAlpha);
    }

    [Fact]
    public void test_one_pressure_front_leaves_the_seeker_and_arrives_on_the_tick()
    {
        var r = Press;
        Assert.Equal(default, FieldPerformance.Wave(r, r.LaunchMs - 1f));
        Assert.InRange(-r.LaunchMs, 250f, 350f);                                    // a force, not a thrown thing
        Assert.Equal(0f, FieldPerformance.Wave(r, r.LaunchMs).Progress, 3);
        Assert.Equal(1f, FieldPerformance.Wave(r, 0f).Progress, 3);                  // CONTACT ON THE BEAT (ADR-011)
        var path = Frames(r.LaunchMs, 0f).Select(u => FieldPerformance.Wave(r, u)).ToArray();
        Assert.True(path.Zip(path.Skip(1), (a, b) => b.Progress >= a.Progress).All(x => x), "the front goes back");
        // it ACCELERATES into the contact: the largest step is the last (it hits rather than lands)
        Assert.True(FieldPerformance.Wave(r, r.LaunchMs / 2f).Progress < 0.5f, "the front decelerates into the target");
        var steps = path.Zip(path.Skip(1), (a, b) => b.Progress - a.Progress).ToArray();
        Assert.Equal(steps.Max(), steps[^1], 3);
        Assert.True(path.Count(p => p.Progress is > 0.05f and < 0.95f) >= 8, "the front is not seen travelling");
        // it is broad from the start and grows a LITTLE on the way: one cell scaled far in flight drew its art pixel from ~2 to
        // ~3 screen px (a material that changes); within 1.15x the pixel drifts less than half a world pixel
        var grows = FieldPerformance.WaveHeight(r, 1f, 420f, 200f) / FieldPerformance.WaveHeight(r, 0f, 420f, 200f);
        Assert.InRange(grows, 1.05f, 1.15f);
        Assert.Equal(FieldPerformance.WaveHeight(r, 0f, 420f, 200f), FieldPerformance.WaveHeight(r, 0f, 999f, 200f), 3);   // not the Seeker's size
        Assert.InRange(r.WaveArriveShare, 1.3f, 2.2f);
        Assert.True(path[^1].Alpha > path[3].Alpha, "the front does not gather weight");
        // and it collapses into the crush
        Assert.Equal(default, FieldPerformance.Wave(r, r.WaveCollapseMs + 1f));
        Assert.True(FieldPerformance.Wave(r, r.WaveCollapseMs * 0.9f).Alpha < 0.1f);
    }

    [Fact]
    public void test_the_crush_presses_the_target_and_is_the_brightest_moment_then_settles()
    {
        var r = Press;
        Assert.Equal(default, FieldPerformance.Crush(r, -1f));
        var crush = Frames(0f, r.EndMs).Select(u => (u, c: FieldPerformance.Crush(r, u))).ToArray();
        // the arcs press IN (accelerating), hold, let go
        var closing = crush.Where(x => x.u <= r.CrushInMs).Select(x => x.c.Close).ToArray();
        Assert.True(closing.Zip(closing.Skip(1), (a, b) => b >= a).All(x => x));
        Assert.Equal(1f, FieldPerformance.Crush(r, r.CrushInMs + 1f).Close, 3);
        Assert.True(r.ClampShutShare < 0.5f, "the arcs do not bite into the body");
        // THE BRIGHTEST MOMENT is the crush: its arcs peak above the front and well above the gathering field; the front's
        // rim is dimmer on the way than on arrival (a hot rim for the whole travel read as a sword beam)
        Assert.True(crush.Max(x => x.c.Alpha) >= r.WaveArriveAlpha && r.ClampPeakAlpha > r.ContractAlpha * 1.5f);
        Assert.InRange(r.WaveTravelGlow, 0.3f, 0.7f);
        Assert.Equal(1f, FieldPerformance.Crush(r, 0f).Heat, 3);                     // hot on the tick
        Assert.True(FieldPerformance.Crush(r, 3f * Frame).Heat < 0.9f, "the heat does not cool");
        Assert.True(ClampTint(r, r.CrushInMs + r.CrushHoldMs + r.CrushOutMs - 1f).B > ClampTint(r, r.CrushInMs + r.CrushHoldMs + r.CrushOutMs - 1f).G + 60,
                    "the arcs do not cool to violet");
        // THE HEAT IS A LINE, NOT A SLAB: the arcs' body is violet on the tick (lit whole, the two arcs made the tick as bright
        // as SPRAY's hit and twice HARD HANDS'); the heat lights only the pressing edge, dimmed
        Assert.Equal(r.ClampColor, ClampTint(r, 0f));
        Assert.InRange(r.ClampGlowShare, 0.4f, 0.8f);
        // the BODY buckles, feet on the floor: shorter and a little wider, back to itself before the phrase ends
        var deepest = FieldPerformance.SquashAt(r, r.CrushInMs + 1f);
        Assert.InRange(deepest.Y, 0.79f, 0.83f);
        Assert.InRange(deepest.X, 1.07f, 1.11f);
        // HARD: arrival -> the compressed pose in ~2 frames, held long enough to register, and no stun pose
        Assert.InRange(r.CrushInMs, 25f, 35f);
        Assert.InRange(r.CrushHoldMs, 50f, 70f);
        Assert.True(FieldPerformance.Crush(r, Frame).Squash > 0.75f, "the body becomes shorter slowly");
        // the release loses cohesion in pixel chunks: whole through the hold, then the three dissolve states
        Assert.Equal(0, FieldPerformance.ClampCellAt(r, r.CrushInMs + r.CrushHoldMs - 1f));
        var cells = Frames(r.CrushInMs + r.CrushHoldMs, r.CrushInMs + r.CrushHoldMs + r.CrushOutMs - 1f).Select(u => FieldPerformance.ClampCellAt(r, u)).ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, cells.Distinct().OrderBy(x => x));
        // no accent glyphs and no micro push (both judged and dropped: arrowheads / a reticle; a push nobody could see)
        Assert.DoesNotContain(typeof(FieldRecipe).GetProperties(), pr => pr.Name.Contains("Crease") || pr.Name.Contains("Push"));
        Assert.Equal(Vector2.One, FieldPerformance.SquashAt(r, -1f));
        Assert.Equal(Vector2.One, FieldPerformance.SquashAt(r, r.EndMs));
        // E: brief, and gone before the phrase ends
        Assert.InRange(r.CrushInMs + r.CrushHoldMs + r.CrushOutMs, 200f, 320f);
        Assert.Equal(default, FieldPerformance.Crush(r, r.CrushInMs + r.CrushHoldMs + r.CrushOutMs + 1f));
    }

    private static Color ClampTint(FieldRecipe r, float u) => FieldPerformance.ClampTint(r, u);

    [Fact]
    public void test_the_crush_lands_on_the_creature_the_tick_affected()
    {
        // the tick at 2000 names creature 1 (its Break); the one at 4000 names none (the target at its floor): the first
        // creature still standing, as the fight picks it
        var p = new FieldPerformance(Press, 2, new List<(float, int, bool, bool)> { (2000f, 1, false, false), (4000f, -1, true, true) })
        {
            CreatureSlots = 3,
            IsStanding = s => s != 0,
        };
        Assert.Equal(2, p.TickCount);
        Assert.True(p.TryPhrase(2000f, out var u, out var k) && u == 0f && k == 0);
        Assert.Equal(1, p.TargetOf(0));
        Assert.Equal(1, p.TargetOf(1));                                              // slot 0 has fallen
        Assert.False(p.TryPhrase(2000f + Press.ContractFromMs - 1f, out _, out _));
        Assert.False(p.TryPhrase(2000f + Press.EndMs + 1f, out _, out _));
        // only that creature buckles
        Assert.True(p.Squash(1, 2000f + Press.CrushInMs).Y < 1f);
        Assert.Equal(Vector2.One, p.Squash(0, 2000f + Press.CrushInMs));
        Assert.Equal(Vector2.One, p.Squash(2, 2000f + Press.CrushInMs));
        Assert.Equal(Vector2.One, p.Squash(1, 3000f));
        // a tick with a presented reaction around it gives way (no arcs); its creature still buckles
        Assert.False(p.Yields(0));
        Assert.True(p.Yields(1));
        // a tick an action's contact lands close to is quiet (the contact can land before the performance draws)
        Assert.False(p.Quiet(0));
        Assert.True(p.Quiet(1));
        Assert.InRange(Press.QuietShare, 0.4f, 0.75f);
        Assert.True(p.Squash(1, 4000f + Press.CrushInMs).Y < 1f);
        // ...and its flattened front is gone before JAWS' fang crown thickens beside it
        Assert.Equal(1f, FieldPerformance.FlattenFade(Press, 0f), 3);
        Assert.Equal(0f, FieldPerformance.FlattenFade(Press, Press.FlattenMs), 3);
        Assert.InRange(Press.FlattenMs, 60f, 140f);
        // the hot arcs are a pale lavender, never SPRAY's white steel
        Assert.True(Press.ClampHotColor.B > Press.ClampHotColor.G + 30);
    }

    [Fact]
    public void test_the_crush_is_made_of_the_front_folding_over_and_under_the_target()
    {
        var r = Press;
        var champ = new Rectangle(420, 450, 200, 430);
        var foe = new Rectangle(1060, 690, 220, 190);
        var (_, arrive) = FieldPerformance.Path(r, champ, foe);
        // the arcs START where the arriving crescent's tips are (at the target's front, half its arrival height out)...
        var open = FieldPerformance.ClampPlace(r, champ, foe, 0f);
        Assert.Equal(arrive, open.Centre);
        Assert.Equal(Math.Min(0.5f * FieldPerformance.WaveHeight(r, 1f, champ.Height, foe.Height), r.ClampStartCap * foe.Height), open.Edge, 1);
        Assert.True(open.Edge <= 0.7f * foe.Height, "the arcs start over the health bars and the dock");
        // ...and fold over and under its BODY (not its tail, not the next creature), their edges biting into it
        var shut = FieldPerformance.ClampPlace(r, champ, foe, 1f);
        Assert.InRange(shut.Centre.X, foe.X + 0.3f * foe.Width, foe.X + 0.55f * foe.Width);
        Assert.True(shut.Edge < 0.5f * foe.Height, "the shut arcs float off the body");
        Assert.InRange(r.ClampWidthShare, 0.6f, 1.0f);
        // the front leaves from the FIELD's leading edge, in front of the Seeker (the wave visibly leaves the field)
        var (from, _) = FieldPerformance.Path(r, champ, foe);
        Assert.True(from.X > champ.Right - 20 && from.X < champ.Right + 0.5f * champ.Height, $"the front leaves from {from.X}");
    }

    [Fact]
    public void test_the_field_allocates_nothing_frame_to_frame()
    {
        Func<int, bool> standing = s => true;
        var p = new FieldPerformance(Press, 2, new List<(float, int, bool, bool)> { (2000f, 0, false, false), (4000f, 0, true, false), (6000f, 1, false, true) })
        {
            CreatureSlots = 4,
            IsStanding = standing,
        };
        p.Squash(0, 1500f);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var ms = 1400f; ms < 6500f; ms += Frame)
        {
            p.Squash(0, ms);
            p.TryPhrase(ms, out var u, out var k);
            p.TargetOf(k);
            FieldPerformance.Field(Press, u, 0.3f);
            FieldPerformance.Wave(Press, u);
            FieldPerformance.Crush(Press, u);
        }
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void test_the_screen_performs_press_in_place_of_the_generic_aura()
    {
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        // built per wave from the wave's own Aura events for the field's slot, each with its Break's creature
        Assert.Contains("FieldRecipes.For(Character.Id, fieldSk.Def.Id)", hunt);
        Assert.Contains("events[ei].Kind != BattleEventKind.Aura || events[ei].Slot != fieldSlot", hunt);
        Assert.Contains("o.Kind == BattleEventKind.Break && o.AtMs == at", hunt);
        // the generic held aura gives way to it
        Assert.Contains("_field is not null) return;   // a performed field draws itself", hunt);
        // the three passes, and the crush's buckle on every creature path (the pack, a lone creature, a boss)
        Assert.Contains("_field.DrawUnder(b, this, _playheadMs, _anim)", hunt);
        Assert.Contains("_field.DrawMaterial(b, this, _playheadMs)", hunt);
        Assert.Contains("_field?.DrawLight(b, this, _playheadMs, _anim)", hunt);
        Assert.Equal(3, hunt.Split("_field?.Squash(").Length - 1);
        Assert.Contains("_waveSkills[o.Slot].Def.Kind is not (SkillKind.Reaction or SkillKind.Field)", hunt);
        Assert.Contains("o.AtMs >= at - fieldRecipe.QuietBeforeMs && o.AtMs <= at + fieldRecipe.QuietAfterMs", hunt);
        Assert.DoesNotContain("_field?.Shove(", hunt);
        // the front that gives way loses cohesion at its own scale (a squeeze crushed its pixels into a smooth lens)
        var perf = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "FieldPerformance.cs"));
        Assert.Contains("DrawFlattened(b, flat, FieldRecipe.WaveDissolveCell, champ, foe, Recipe.WaveColor * (", perf);
        // ...in its own violet, and only its leading edge lit (the hot tint over the whole cell was a pale slab beside JAWS)
        Assert.Contains("DrawFlattened(b, flat, FieldRecipe.WaveEdgeCell, champ, foe, Recipe.RimColor * (flare * Recipe.WaveGlowShare", perf);
        Assert.DoesNotContain("DrawFlattened(b, flat, FieldRecipe.WaveDissolveCell, champ, foe, ClampTint(", perf);
        Assert.DoesNotContain("DrawFlattened(b, flat, FieldRecipe.WaveDissolveCell, champ, foe, Recipe.RimColor", perf);
        // the heat lights the clamp's EDGE cell only, and the front is drawn axis-aligned on whole pixels (turned along its
        // path, the NEAREST grid tilted off the stage's and its rim read as a serrated blade)
        Assert.Contains("DrawClamps(b, clamp, FieldRecipe.ClampEdgeCell, champ, body, _arcWidth[tick], c, Recipe.ClampHotColor * (flare * Recipe.ClampGlowShare))", perf);
        Assert.DoesNotContain("MathF.Atan2(to.Y - from.Y, to.X - from.X)", perf);
        // the crush re-pins the target's drawn body one frame in (a creature caught in its own lunge is pressed where it is),
        // once: followed frame by frame, the arcs jumped with every lunge frame mid-hold
        Assert.Contains("if (target >= 0 && u >= 0.5f * Recipe.CrushInMs && _crush[tick] is null && TryProbeShares(stage, target, out var now)) _crush[tick] = now;", perf);
        Assert.Contains("DrawClamps(b, clamp, ClampCellAt(Recipe, u), champ, body, _arcWidth[tick], c, ClampTint(Recipe, u) * (c.Alpha * quiet))", perf);
        // the arcs' SIZE comes from the silhouette pinned at launch (sized from a lunge's long silhouette, their art pixel
        // grew coarser than the world's); the crush pose only places them, BUCKLED, so they press the body down and touch it
        Assert.Contains("if (_arcWidth[tick] <= 0f) _arcWidth[tick] = foe.Width * Recipe.ClampWidthShare;", perf);
        Assert.Contains("return UiKit.Buckle(body, SquashAt(Recipe, u));", perf);
        Assert.DoesNotContain("var scale = foe.Width * Recipe.ClampWidthShare", perf);
        // no white flash on the target: PRESS's feedback is the deformation
        Assert.DoesNotContain(typeof(FieldRecipe).GetProperties(), pr => pr.Name.Contains("Flash"));
        // a tick near a presented reaction (JAWS) gives way: JAWS' fang rows and the arcs on one creature read as one jaw
        Assert.Contains("_waveSkills[o.Slot].Def.Kind == SkillKind.Reaction", hunt);
        Assert.Contains("o.AtMs >= at - fieldRecipe.YieldBeforeMs && o.AtMs <= at + fieldRecipe.YieldAfterMs", hunt);
        // the buckle keeps the feet on the floor
        var kit = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "UiKit.cs"));
        Assert.Contains("new Rectangle(dest.Center.X - w / 2, dest.Bottom - h, w, h)", kit);
        Assert.Equal(new Rectangle(100, 200, 50, 80), UiKit.Buckle(new Rectangle(100, 200, 50, 80), Vector2.One));
        var buckled = UiKit.Buckle(new Rectangle(100, 200, 50, 80), new Vector2(1.1f, 0.85f));
        Assert.Equal(280, buckled.Bottom);                                          // the feet stay on the floor
        Assert.Equal(125, buckled.Center.X);
        Assert.True(buckled.Height < 80 && buckled.Width > 50);
    }
}
