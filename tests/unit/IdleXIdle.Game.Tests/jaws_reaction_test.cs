using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Sources;
using IdleXIdle.Game.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE REACTION CONTRACT (ADR-011, JAWS; a SHADOW MAW MADE OF MIST, the owner, 2026-09-28): a reaction is presented on
/// its own layer, belongs to its SKILL, never owns the champion's figure and moves nothing; it is ONE MISTY MAW and
/// nothing else (no separate fog, no loose teeth, no creature body, no eye, no glint, no residue, no particles, no
/// additive light, no flash): loose Shadow smoke gathers in front of the creature that bit and forms an upper and a
/// lower jaw that open wide, snap shut past closed on its front (the rows of teeth interlocking), clench (a jolt and a
/// swell), hold the bite, then let go and dissolve back into rising smoke; ONE cue on the snap; the number after it.
/// </summary>
/// <remarks>
/// The failures these stop were measured across the JAWS work: a row-wide rope ring no one could tie to the bite, five
/// generic sounds on one frame, a post-bite "lay a trap" clip that never played, a dock tile that looked the same armed
/// and rearming, a spring-loaded bear trap the owner rejected as a mechanism, a whole piranha too small and detailed to
/// read at true speed, four teeth that stopped on the creature's outline and never met, and four misty teeth that did
/// close but read as "four triangles coming to the middle" rather than a jaw that opens and bites.
/// </remarks>
public class JawsReactionTest
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static readonly ReactionRecipe Jaws = ReactionRecipes.SeekerJaws;

    private sealed class Stage : IReactionStage
    {
        public Rectangle Champion = new(420, 451, 180, 400);
        public Rectangle Target = new(1050, 640, 160, 170);

        public bool TryActorFrame(string clipKey, int frame, out SpriteFrame drawn, out int frameSize)
        {
            drawn = default;
            frameSize = 0;
            return false;
        }

        public bool TryTargetBody(int slot, out Rectangle body)
        {
            body = Target;
            return true;
        }

        public bool TryCaughtBody(int slot, out Rectangle body)
        {
            body = Target;
            return true;
        }

        public Texture2D? Texture(string key) => null;
        public float CasterHeight => Champion.Height;

        public bool TryChampionBody(out Rectangle body)
        {
            body = Champion;
            return true;
        }

        public bool TryTargetFrame(int slot, out SpriteFrame frame)
        {
            frame = default;
            return false;
        }
    }

    /// <summary>A pack: each slot its own canonical body (a slot with none has no body).</summary>
    private sealed class PackStage : IReactionStage
    {
        public readonly Dictionary<int, Rectangle> Bodies = new();

        public bool TryActorFrame(string clipKey, int frame, out SpriteFrame drawn, out int frameSize)
        {
            drawn = default;
            frameSize = 0;
            return false;
        }

        public bool TryTargetBody(int slot, out Rectangle body) => Bodies.TryGetValue(slot, out body);
        public bool TryCaughtBody(int slot, out Rectangle body) => Bodies.TryGetValue(slot, out body);
        public Texture2D? Texture(string key) => null;
        public float CasterHeight => 400f;

        public bool TryChampionBody(out Rectangle body)
        {
            body = new Rectangle(420, 451, 180, 400);
            return true;
        }

        public bool TryTargetFrame(int slot, out SpriteFrame frame)
        {
            frame = default;
            return false;
        }
    }

    private static ReactionPerformance Answer(params int[] targets) => new(Jaws, 3, 7000, targets.Length == 0 ? new[] { 0 } : targets, Color.MediumPurple);

    // ── IDENTITY: the skill's own, never its Form's ─────────────────────────────────────────────

    [Fact]
    public void test_the_jaws_recipe_belongs_to_the_skill_and_repay_keeps_its_own()
    {
        // JAWS and REPAY share the Snare Form's clip and effect words: a lookup by those would give REPAY the jaws
        var jaws = SkillCatalogue.ById("snare_jaws");
        var repay = SkillCatalogue.ById("snare_repay");
        Assert.Equal(jaws.ClipKey, repay.ClipKey);
        Assert.Equal(jaws.FxKey, repay.FxKey);

        Assert.Same(ReactionRecipes.SeekerJaws, ReactionRecipes.For("seeker", "snare_jaws"));
        Assert.Equal("seeker.jaws", ReactionRecipes.SeekerJaws.Id);
        Assert.Null(ReactionRecipes.For("seeker", "snare_repay"));
        Assert.Null(ReactionRecipes.For("magpie", "snare_jaws"));   // another champion keeps the legacy reaction
        // ...and neither is an ACTION: a reaction never owns the figure
        Assert.Null(ActionRecipes.For("seeker", jaws.Id, jaws.ClipKey, jaws.FxKey));
        Assert.Null(ActionRecipes.For("seeker", repay.Id, repay.ClipKey, repay.FxKey));
    }

    [Fact]
    public void test_a_presented_reaction_loads_no_lay_a_trap_clip_and_repay_still_does()
    {
        var seeker = CharacterRoster.Get("seeker");
        var into = new List<string>();
        ActorClips.ChampionStrips(seeker, new[] { new EquippedSkill(SkillCatalogue.ById("snare_jaws"), Source.Shadow, null) }, _ => true, into);
        Assert.DoesNotContain(into, k => k.Contains("trap", StringComparison.Ordinal));
        ActorClips.ChampionStrips(seeker, new[] { new EquippedSkill(SkillCatalogue.ById("snare_repay"), Source.Shadow, null) }, _ => true, into);
        Assert.Contains(into, k => k.Contains("trap", StringComparison.Ordinal));   // REPAY's own cast clip
    }

    // ── ONE MISTY MAW, AND NOTHING ELSE ──────────────────────────────────────────────────────────

    /// <summary>The frames a 60 fps screen draws of one reaction (its origin is the first frame, so u = 0, 16.7, 33.3, ...).</summary>
    private static IEnumerable<float> Frames(ReactionRecipe r)
    {
        for (var n = 0; n * (1000f / 60f) < r.GoneMs; n++) yield return n * (1000f / 60f);
    }

    private static readonly Rectangle Whelp = new(1077, 697, 250, 187);   // a bitten whelp's drawn silhouette, as filmed

    [Fact]
    public void test_the_reaction_is_one_misty_maw_and_nothing_else()
    {
        // no loose teeth, no separate fog, no creature body, no residue, no glint, no particles, no behind or light pass:
        // the recipe has no such dials and the layer has no such passes; its only art is the one maw strip
        var props = typeof(ReactionRecipe).GetProperties().Select(pr => pr.Name).ToArray();
        foreach (var banned in new[] { "Fang", "Tooth", "Teeth", "Piranha", "Head", "Eye", "Tail", "Residue", "Glint", "Behind", "Smear",
                                       "Spark", "Particle", "Trail", "Yank", "Chain", "Tether", "Whip", "Pivot", "Half", "Mist", "Wisp", "Pocket" })
            Assert.DoesNotContain(props, n => n.StartsWith(banned, StringComparison.Ordinal));
        Assert.Equal(new[] { "MawKey" }, props.Where(n => n.EndsWith("Key", StringComparison.Ordinal)).ToArray());
        var methods = typeof(ReactionPerformance).GetMethods().Select(m => m.Name).ToArray();
        foreach (var pass in new[] { "DrawUnder", "DrawBehind", "DrawLight" })
            Assert.DoesNotContain(pass, methods);
        Assert.Contains("DrawMaterial", methods);
        Assert.False(Jaws.Callout);
    }

    /// <summary>What seeker_maw.py wrote about the art (keypose_sources/seeker_maw_spans.json).</summary>
    private static System.Text.Json.JsonElement MawArt()
        => System.Text.Json.JsonDocument.Parse(File.ReadAllText(RepoFile("tools", "asset-pipeline", "v2", "keypose_sources", "seeker_maw_spans.json"))).RootElement;

    [Fact]
    public void test_the_maw_is_two_misty_jaw_pieces_whose_teeth_interlock()
    {
        var r = Jaws;
        // the strip: MawStates states side by side, the upper jaw above the lower (read off the PNG's own header)
        var png = File.ReadAllBytes(RepoFile("assets", "art", "VFX", "parts", r.MawKey + ".png"));
        int Be(int at) => (png[at] << 24) | (png[at + 1] << 16) | (png[at + 2] << 8) | png[at + 3];
        Assert.Equal(r.MawStates * r.PieceWidth, Be(16));
        Assert.Equal(2 * r.PieceHeight, Be(20));
        // the recipe describes the art the script wrote: its states, cell, hinge and jaw length
        var art = MawArt();
        Assert.Equal(r.MawStates, art.GetProperty("states").GetInt32());
        Assert.Equal(r.PieceWidth, art.GetProperty("cell")[0].GetInt32());
        Assert.Equal(r.PieceHeight, art.GetProperty("cell")[1].GetInt32());
        Assert.Equal(r.Hinge.X, art.GetProperty("hinge")[0].GetSingle());
        Assert.Equal(r.Hinge.Y, art.GetProperty("hinge")[1].GetSingle());
        Assert.Equal(r.JawArtLength, art.GetProperty("jaw_length").GetSingle());
        // the rows INTERLOCK: five teeth each, every lower tooth half a tooth along from the upper ones
        var up = art.GetProperty("upper_tip_x").EnumerateArray().Select(e => e.GetSingle()).ToArray();
        var lo = art.GetProperty("lower_tip_x").EnumerateArray().Select(e => e.GetSingle()).ToArray();
        Assert.Equal(5, up.Length);
        Assert.Equal(5, lo.Length);
        var pitch = up[1] - up[0];
        for (var i = 0; i < 5; i++)
            Assert.InRange(lo[i] - up[i], 0.4f * pitch, 0.6f * pitch);
        // GRADED, so the shut rows never read as a zipper: the front fang at least 1.6x the back tooth (heights off the gum)
        var uy = art.GetProperty("upper_tip_y").EnumerateArray().Select(e => e.GetSingle()).ToArray();
        var hook = art.GetProperty("gum_hook")[0].GetSingle();
        float Height(int i) => uy[i] - r.Hinge.Y - hook * MathF.Pow((up[i] - r.Hinge.X) / r.JawArtLength, 3f);
        Assert.True(Height(4) >= 1.6f * Height(0), $"the teeth are not graded: back {Height(0):0.0} px, front {Height(4):0.0} px");
        // the SEAM (where the teeth close, a share of the jaw from the hinge) lies on the art's own tooth rows
        Assert.InRange(r.BiteSeamShare, (up.Average() - r.Hinge.X) / r.JawArtLength - 0.05f, (lo.Average() - r.Hinge.X) / r.JawArtLength + 0.05f);
        // authored procedurally, one fixed noise field per piece
        var gen = File.ReadAllText(RepoFile("tools", "asset-pipeline", "v2", "seeker_maw.py"));
        Assert.Contains($"STATES = {r.MawStates}", gen);
        Assert.Equal(1, gen.Split("default_rng(").Length - 1);
    }

    [Fact]
    public void test_the_jaw_opens_wide_holds_it_then_bites_past_its_rest_across_the_creatures_head()
    {
        var r = Jaws;
        // the mist condenses into a nearly SHUT jaw, which WAITS, nearly shut, until the biting creature's own lunge drawing
        // is gone (~120-150 ms after its contact: opened over it, violet on violet, the jaw faded in already wide), then
        // visibly OPENS WIDE as its own motion and HOLDS it
        var early = ReactionPerformance.Gape(r, r.GatherMs - 1f);
        var wide = ReactionPerformance.Gape(r, r.OpenedAtMs);
        Assert.True(early.Y - early.X <= 20f, $"the jaw is not nearly shut when it forms: {early}");
        Assert.Equal(r.GatherGape, ReactionPerformance.Gape(r, r.OpenFromMs - 1f));
        Assert.InRange(r.OpenFromMs, 120f, 160f);
        Assert.True(wide.Y - wide.X >= 50f, $"the mouth never opens wide: {wide}");
        var opening = Frames(r).Count(u => u > r.OpenFromMs && u < r.OpenedAtMs);
        Assert.True(opening >= 3, $"the opening is seen in only {opening} frame(s)");
        Assert.Equal(wide, ReactionPerformance.Gape(r, r.CloseFromMs - 1f));
        Assert.True(Frames(r).Count(u => u >= r.OpenedAtMs && u < r.CloseFromMs) >= 2, "the wide mouth is not held");
        // REST: a slightly open mouth, so the jaws stay two wedges with the creature between them while the teeth interlock
        Assert.InRange(r.RestGape.Y - r.RestGape.X, 16f, 24f);
        // SNAP: past the rest, ON a frame, and held there at least two frames (a bite the eye can catch)
        Assert.Contains(Frames(r), u => Math.Abs(u - r.SnapAtMs) < 0.5f);
        var snap = ReactionPerformance.Gape(r, r.SnapAtMs);
        Assert.True(snap.Y - snap.X < r.RestGape.Y - r.RestGape.X - 3f, $"the jaws do not bite past their rest: {snap}");
        Assert.True(Frames(r).Count(u => u >= r.SnapAtMs - 0.5f && u < r.SnapAtMs + r.OvershootHoldMs) >= 2, "the bite past the rest is not held two frames");
        // then the rest, tightening slowly through the bite (never frozen), and opening as it lets go
        var rest = ReactionPerformance.Gape(r, r.SnapAtMs + r.OvershootHoldMs + r.SettleMs);
        Assert.Equal(r.RestGape.X, rest.X, 3);
        var tight = ReactionPerformance.Gape(r, r.ReleaseAtMs - 1f);
        Assert.True(tight.Y - tight.X < rest.Y - rest.X, "the hold is frozen");
        var release = ReactionPerformance.Gape(r, r.GoneMs - 1f);
        Assert.True(release.Y - release.X > tight.Y - tight.X && release.Y - release.X < wide.Y - wide.X, $"the release: {release}");
        // AIM: anchored to the creature's CANONICAL body horizontally (not a lunge's wider art), the shut seam crossing its
        // DRAWN silhouette on the bite line where the teeth close (the throat, below the eyes), most of the jaw over it.
        // The drawn lunge differs from the layout body in both axes, so each axis is seen to come from its own rectangle.
        var lunge = new Rectangle(Whelp.X - 80, Whelp.Y + 60, Whelp.Width + 80, Whelp.Height - 40);
        var m = ReactionPerformance.Maw(r, lunge, Whelp, r.ReleaseAtMs - 1f);
        var length = r.MawLengthShare * Whelp.Width;
        var tilt = MathHelper.ToRadians(r.MawTiltDegrees);
        var seam = m.HingeAt + r.BiteSeamShare * length * new Vector2(MathF.Cos(tilt), MathF.Sin(tilt));
        var biteY = lunge.Y + r.BiteLineShare * lunge.Height;
        Assert.InRange(seam.Y, biteY - 2f, biteY + 2f);
        Assert.True(Math.Abs(seam.Y - (Whelp.Y + r.BiteLineShare * Whelp.Height)) > 10f, "the bite line is read off the layout, not the drawn silhouette");
        Assert.InRange(r.BiteLineShare, 0.52f, 0.66f);
        Assert.True(m.HingeAt.X < Whelp.X && m.HingeAt.X + length > Whelp.X + 0.25f * Whelp.Width, "the jaw does not lie over the creature");
        Assert.True(m.HingeAt.X > lunge.X, "the maw is anchored to the lunge's art, not the creature");
        Assert.True(length <= Whelp.Width, "the maw is longer than the creature it bites");
        Assert.InRange(r.MawTiltDegrees, 0f, 8f);
    }

    [Fact]
    public void test_the_snap_stops_where_the_teeth_reach_the_gums_and_the_rows_never_invert()
    {
        // the art's tooth tips and gum lines (seeker_maw.py), each jaw turned by its gape about the shared hinge: from the
        // snap on, no tooth tip ever passes through the other jaw's gum (shut to a line, the rows looked inside-out), and
        // at the rest the rows still INTERLOCK across the creature (the upper tips below the lower tips)
        var r = Jaws;
        var art = MawArt();
        float[] Row(string name) => art.GetProperty(name).EnumerateArray().Select(e => e.GetSingle()).ToArray();
        var (ux, uy, lx, ly) = (Row("upper_tip_x"), Row("upper_tip_y"), Row("lower_tip_x"), Row("lower_tip_y"));
        var hook = Row("gum_hook");
        var lowerLength = art.GetProperty("lower_jaw_length").GetSingle();
        var h = r.Hinge;
        float GumUp(float x) => h.Y + hook[0] * MathF.Pow(Math.Clamp((x - h.X) / r.JawArtLength, 0f, 1f), 3f);
        float GumLo(float x) => h.Y - hook[1] * MathF.Pow(Math.Clamp((x - h.X) / lowerLength, 0f, 1f), 3f);
        Vector2 Turn(Vector2 p, float degrees)
        {
            var a = MathHelper.ToRadians(degrees);
            var d = p - h;
            return h + new Vector2(d.X * MathF.Cos(a) - d.Y * MathF.Sin(a), d.X * MathF.Sin(a) + d.Y * MathF.Cos(a));
        }
        float Clearance(Vector2 g)
        {
            var worst = float.MaxValue;
            for (var i = 0; i < ux.Length; i++)
            {
                var q = Turn(Turn(new Vector2(ux[i], uy[i]), g.X), -g.Y);   // an upper tip, in the lower jaw's own frame
                worst = Math.Min(worst, GumLo(q.X) - q.Y);
            }
            for (var i = 0; i < lx.Length; i++)
            {
                var q = Turn(Turn(new Vector2(lx[i], ly[i]), g.Y), -g.X);   // a lower tip, in the upper jaw's own frame
                worst = Math.Min(worst, q.Y - GumUp(q.X));
            }
            return worst;
        }
        foreach (var u in Frames(r).Where(u => u >= r.GatherMs))
            Assert.True(Clearance(ReactionPerformance.Gape(r, u)) >= -1.5f, $"a tooth passes through the other jaw's gum at {u:0} ms: {ReactionPerformance.Gape(r, u)}");
        // the snap is as tight as the jaw goes: a little tighter and the teeth would pass through the gums
        Assert.InRange(Clearance(r.Overshoot), -1.5f, 3f);
        // at rest the rows interlock across the creature by a good part of a tooth
        float Interlock(Vector2 g)
            => Enumerable.Range(0, ux.Length).Max(i => Turn(new Vector2(ux[i], uy[i]), g.X).Y)
               - Enumerable.Range(0, lx.Length).Min(i => Turn(new Vector2(lx[i], ly[i]), g.Y).Y);
        Assert.True(Interlock(r.RestGape) >= 12f, $"the teeth do not interlock at rest: {Interlock(r.RestGape):0.0} px");
    }

    [Fact]
    public void test_when_one_answer_bites_two_creatures_each_maw_bites_its_own()
    {
        // two whelps of a pack as filmed (layout bodies 136 px apart, overlapping): the front one gets the full maw, the
        // one behind it a smaller maw hinged at its own front edge; the two maws never overlap while they hold the bite,
        // so neither reaches over the other creature's head
        var r = Jaws;
        var front = new Rectangle(1112, 630, 268, 245);
        var rear = new Rectangle(1248, 630, 268, 245);
        var reach = MawArt().GetProperty("lower_jaw_length").GetSingle() / r.JawArtLength;   // the longer (lower) jaw, in jaw lengths
        var firstTooth = (MawArt().GetProperty("upper_tip_x")[0].GetSingle() - r.Hinge.X) / r.JawArtLength;
        foreach (var u in Frames(r))
        {
            var a = ReactionPerformance.Maw(r, front, front, u);
            var b = ReactionPerformance.Maw(r, rear, rear, u, secondary: true);
            var aEnd = a.HingeAt.X + reach * r.JawArtLength * a.Scale;
            // the rear maw forms IN PLACE, hinged at its own creature's front edge: never over the creature in front of it
            Assert.True(b.HingeAt.X >= rear.X - 1f, $"the rear maw reaches over the creature in front of it at {u:0} ms");
            // the front maw's snout never reaches the rear maw's teeth (at most its smoky hinge, on the swell)
            Assert.True(aEnd < b.HingeAt.X + firstTooth * r.JawArtLength * b.Scale, $"the two maws' teeth overlap at {u:0} ms: {aEnd:0}");
            Assert.True(b.Scale < a.Scale || u >= r.GoneMs, "the rear maw is not the smaller one");
        }
    }

    [Fact]
    public void test_the_front_creature_is_the_one_nearest_the_seeker_whatever_the_fight_order()
    {
        // the answer's targets come in the fight's event order, not front to back: the full maw goes to the creature whose
        // canonical body stands nearest the Seeker; a creature with no body is skipped; on a tie the first one wins
        var pack = new PackStage();
        pack.Bodies[0] = new Rectangle(1112, 630, 268, 245);
        pack.Bodies[2] = new Rectangle(1248, 630, 268, 245);
        Assert.Equal(1, ReactionPerformance.FrontTarget(pack, new[] { 2, 0 }));
        Assert.Equal(0, ReactionPerformance.FrontTarget(pack, new[] { 0, 2 }));
        Assert.Equal(1, ReactionPerformance.FrontTarget(pack, new[] { 5, 0, 2 }));   // slot 5 has no body
        Assert.Equal(-1, ReactionPerformance.FrontTarget(pack, new[] { 5 }));
        pack.Bodies[3] = new Rectangle(1112, 630, 268, 245);
        Assert.Equal(0, ReactionPerformance.FrontTarget(pack, new[] { 0, 3 }));
    }

    [Theory]
    [InlineData(100f)]
    [InlineData(80f)]
    [InlineData(133f)]
    [InlineData(200f)]
    public void test_after_the_snap_nothing_jumps_whatever_the_hold(float holdMs)
    {
        // every segment after the snap is clamped to the release, so a retuned hold never makes the gape, the swell or the
        // condensation jump on a drawn frame (the snap frame itself is the one deliberate jump)
        var r = new ReactionRecipe { Id = "seeker.jaws", HoldMs = holdMs };
        var drawn = Frames(r).Where(u => u >= r.SnapAtMs + 1f).ToArray();
        for (var i = 1; i < drawn.Length; i++)
        {
            var (a, b) = (ReactionPerformance.Gape(r, drawn[i - 1]), ReactionPerformance.Gape(r, drawn[i]));
            Assert.True(Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y) <= 12f, $"hold {holdMs}: the gape jumps at {drawn[i]:0} ms: {a} -> {b}");
            Assert.True(Math.Abs(ReactionPerformance.MawScale(r, drawn[i]) - ReactionPerformance.MawScale(r, drawn[i - 1])) <= 0.06f, $"hold {holdMs}: the scale jumps at {drawn[i]:0} ms");
            Assert.True(Math.Abs(ReactionPerformance.StateAt(r, drawn[i]) - ReactionPerformance.StateAt(r, drawn[i - 1])) <= 2, $"hold {holdMs}: the condensation jumps at {drawn[i]:0} ms");
            Assert.True(ReactionPerformance.Jolt(r, drawn[i]) == 0f || drawn[i] < r.ReleaseAtMs, $"hold {holdMs}: the jolt runs into the dissolve");
        }
    }

    [Fact]
    public void test_the_closing_is_seen_and_accelerates_into_the_snap()
    {
        var r = Jaws;
        var last = -1f;
        for (var u = 0f; u <= r.SnapAtMs; u += 2f)
        {
            var c = ReactionPerformance.Close(r, u);
            Assert.True(c >= last - 1e-5f, "the jaws only ever close until the snap");
            last = c;
        }
        var closing = Frames(r).Where(u => ReactionPerformance.Close(r, u) is > 0.05f and < 0.95f).ToArray();
        Assert.True(closing.Length >= 2, $"only {closing.Length} frame(s) show the jaws part-way closed");
        // on the frames actually drawn: each step is larger than the one before, the LARGEST on the snap frame itself (the
        // bite lands with the sound), yet no single frame carries more than 60 % of the travel (the close is seen)
        var positions = Frames(r).Where(u => u >= r.CloseFromMs && u <= r.SnapAtMs + 0.5f).Select(u => ReactionPerformance.Close(r, u)).ToArray();
        var steps = positions.Zip(positions.Skip(1), (x, y) => y - x).ToArray();
        Assert.True(steps.Max() <= 0.6f, "one frame carries most of the close");
        Assert.Equal(steps.Max(), steps[^1]);
        for (var i = 1; i < steps.Length; i++)
            Assert.True(steps[i] >= steps[i - 1] - 1e-4f, $"the close does not accelerate: steps {string.Join(", ", steps.Select(x => x.ToString("0.00")))}");
    }

    [Fact]
    public void test_the_first_bite_is_pronounced_a_jolt_into_the_creature_and_a_swell()
    {
        var r = Jaws;
        // the JOLT, along the jaw: ONE push into the creature, part-way on the snap frame and whole on the next (the bite
        // is SEEN to drive in after the cue: at its peak on the snap frame and backing off at once, it read as a bounce),
        // then easing back frame by frame to nothing (never a rebound: an in-out-in jolt read as a 30 Hz buzz)
        // (sampled on the frames the screen draws, from the snap frame on)
        var drawn = Frames(r).Where(u => u >= r.SnapAtMs - 0.5f).ToArray();
        Assert.Equal(0f, ReactionPerformance.Jolt(r, r.SnapAtMs - 17f));
        var jolt = drawn.Take(7).Select(u => ReactionPerformance.Jolt(r, u)).ToArray();
        Assert.InRange(jolt[1], 12f, 24f);
        Assert.True(jolt[0] > 0f && jolt[0] < jolt[1], $"the push does not drive in after the snap: {string.Join(", ", jolt)}");
        for (var n = 2; n < jolt.Length; n++)
            Assert.True(jolt[n] <= jolt[n - 1] && jolt[n] >= 0f, $"the jolt is not one push easing back: {string.Join(", ", jolt)}");
        Assert.True(jolt.Count(j => j > 0f) >= 4, "the push backs off too fast to be seen");
        Assert.Equal(0f, jolt[^1]);
        // ...and it goes INTO the creature: at the push's peak the maw sits further toward it than once the jolt is over
        var pushed = ReactionPerformance.Maw(r, Whelp, Whelp, drawn[1]).HingeAt;
        var still = ReactionPerformance.Maw(r, Whelp, Whelp, r.SnapAtMs + r.ClenchMs).HingeAt;
        Assert.True(pushed.X - still.X >= 0.9f * jolt[1] && Whelp.Center.X > pushed.X, $"the jolt does not drive into the creature: {pushed} vs {still}");
        // the SWELL: 12-20 %, held on the snap frame and the next, then easing back to 1 in EVEN steps
        var swell = drawn.Take(6).Select(u => ReactionPerformance.MawScale(r, u)).ToArray();
        Assert.InRange(swell[0], 1.12f, 1.20f);
        Assert.Equal(swell[0], swell[1], 2);
        Assert.Equal(1f, swell[^1], 3);
        var easing = swell.Zip(swell.Skip(1), (x, y) => x - y).Where(d => d > 1e-3f).ToArray();
        Assert.True(easing.Length >= 2 && easing.Max() <= 1.6f * easing.Min(), $"the swell does not ease evenly: {string.Join(", ", swell.Select(x => x.ToString("0.000")))}");
        // the bite is HELD long enough to be read, and the whole phrase is longer (the owner allowed it)
        Assert.InRange(r.HoldMs, 90f, 160f);
        Assert.InRange(r.EndMs, 450f, 650f);
    }

    [Fact]
    public void test_the_maw_comes_as_mist_condenses_bites_and_dissolves_back_into_mist()
    {
        var r = Jaws;
        // it grows out of nothing as LOOSE smoke, larger, coming in from the Seeker's side, and the loose states are what
        // is seen for most of the gathering; it stays smoky while it opens, HARDENS only as it closes (the crisp jaw is the
        // bite), and never jumps: at most two strip states from one frame to the next
        Assert.Equal(0, ReactionPerformance.StateAt(r, 0f));
        Assert.InRange(ReactionPerformance.Opacity(r, 0f), 0.05f, 0.25f);
        var ramp = Frames(r).Where(u => u <= r.OpacityRampMs + 17f).Select(u => ReactionPerformance.Opacity(r, u)).ToArray();
        Assert.True(ramp.Zip(ramp.Skip(1), (x, y) => y - x).Max() <= 0.4f, $"the mist pops in: {string.Join(", ", ramp.Select(x => x.ToString("0.00")))}");
        Assert.True(ReactionPerformance.StateAt(r, 0.6f * r.GatherMs) <= (r.MawStates - 1) / 3, "the smoke condenses too early to be seen");
        Assert.True(ReactionPerformance.MawScale(r, 0f) >= 1.1f, "the mist does not tighten as it comes");
        var start = ReactionPerformance.Maw(r, Whelp, Whelp, 0f).HingeAt.X;
        var formed = ReactionPerformance.Maw(r, Whelp, Whelp, r.GatherMs).HingeAt.X;
        Assert.True(formed - start >= 20f, "the maw does not come in");
        Assert.Equal(r.MawStates - 1, ReactionPerformance.StateAt(r, r.SnapAtMs));
        Assert.Equal(r.MawStates - 1, ReactionPerformance.StateAt(r, r.SnapAtMs + 1000f / 60f));
        Assert.True(ReactionPerformance.StateAt(r, r.CloseFromMs) <= r.MawStates - 3, "the jaw is already hard before it closes");
        Assert.True(ReactionPerformance.StateAt(r, r.OpenedAtMs) >= (r.MawStates - 1) * 3 / 4, "the open jaw is too loose a smoke to be read");
        Assert.InRange(ReactionPerformance.StateAt(r, r.ReleaseAtMs - 1f), r.MawStates - 4, r.MawStates - 2);
        var states = Frames(r).Select(u => ReactionPerformance.StateAt(r, u)).ToArray();
        var jump = states.Zip(states.Skip(1), (x, y) => Math.Abs(y - x)).Max();
        Assert.True(jump <= 2, $"the condensation jumps {jump} states in one frame: {string.Join(",", states)}");
        Assert.Equal(1f, ReactionPerformance.Opacity(r, r.ReleaseAtMs - 1f));
        // it LOOSENS into smoke over several frames and fades evenly: at least six frames of the jaw coming apart,
        // halfway through the dissolve it is smoke yet still more than half there, the last frame is faint, and it rises
        var loosening = Frames(r).Count(u => u > r.ReleaseAtMs && ReactionPerformance.Condense(r, u) is > 0.05f and < 0.95f);
        Assert.True(loosening >= 6, $"the jaw comes apart in only {loosening} frame(s)");
        var mid = (r.ReleaseAtMs + r.GoneMs) * 0.5f;
        Assert.True(ReactionPerformance.Condense(r, mid) <= 0.55f, "halfway through, the jaw is still a jaw");
        Assert.True(ReactionPerformance.Opacity(r, mid) >= 0.5f, "halfway through, the smoke is already gone");
        Assert.True(ReactionPerformance.Opacity(r, Frames(r).Last()) <= 0.25f, "the last frame cuts off a dense smoke");
        var held = ReactionPerformance.Maw(r, Whelp, Whelp, r.ReleaseAtMs).HingeAt.Y;
        Assert.True(ReactionPerformance.Maw(r, Whelp, Whelp, r.GoneMs - 1f).HingeAt.Y < held - 10f, "the smoke does not rise");
        Assert.Equal(0f, ReactionPerformance.Opacity(r, r.GoneMs));
    }

    [Fact]
    public void test_the_maw_draws_over_the_creatures_under_the_champion_two_sprites_a_target()
    {
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "ReactionPerformance.cs"));
        var draw = src[src.IndexOf("public void DrawMaterial(", StringComparison.Ordinal)..];
        draw = draw[..draw.IndexOf("\n    }", StringComparison.Ordinal)];   // CRLF-safe: no trailing newline in the key
        Assert.Equal(2, draw.Split("b.Draw(").Length - 1);                      // the upper and the lower jaw, one state each
        Assert.Contains("var m = Maw(Recipe, body, layout, u, secondary: k != front);", draw);   // the pose the tests pin is the pose drawn
        Assert.Contains("var front = FrontTarget(stage, Targets);", draw);                          // the front creature, as tested
        Assert.Equal(2, draw.Split("Recipe.Hinge,").Length - 1);                // both turn about the hinge
        Assert.Contains("ReactionRecipe.VisibleFloor", draw);
        // the screen draws the maw AFTER the creatures (over the creature it bites) and BEFORE the champion (when she stands
        // in front of that creature, as HARD HANDS does, she is in front of its jaw), and has no pass under the creatures
        var hunt = Hunt();
        var jaws = hunt.IndexOf("foreach (var r in _reactions) r.DrawMaterial(b, this, _playheadMs);", StringComparison.Ordinal);
        Assert.True(jaws > hunt.IndexOf("else DrawNormalEnemy(b, attacking);", StringComparison.Ordinal), "the maw is drawn before the creatures");
        Assert.True(jaws < hunt.IndexOf("DrawChampion(b, _champDrawBox, dead: _mode == Mode.Downed);", StringComparison.Ordinal), "the maw is drawn over the champion");
        Assert.DoesNotContain("r.DrawUnder(", hunt);
    }

    [Fact]
    public void test_no_jaws_flash_and_the_number_comes_a_few_frames_after_the_snap()
    {
        var r = Jaws;
        Assert.Equal(0f, r.TargetFlash);                                        // the maw IS the hit feedback
        Assert.InRange(BitePresentation.UsualFlash.Peak, 0.40f, 0.50f);          // the global F2 is untouched
        Assert.InRange(r.NumberDelayMs, 30f, 50f);
        Assert.Equal(r.SnapAtMs + r.NumberDelayMs, r.AnswerAtMs);
        var p = Answer();
        var stage = new Stage();
        p.Update(7000, stage);
        Assert.False(p.Snapped);
        p.Update(7000 + r.SnapAtMs - 10f, stage);
        Assert.False(p.Snapped, "not on the open or closing frames");
        var step = p.Update(7000 + r.SnapAtMs, stage);
        Assert.True(step.Snapped && p.Snapped, "the cue and a kill's fall are presented on the snap");
        Assert.False(step.Answered, "the number is NOT on the snap frame");
        Assert.False(p.Update(7000 + r.SnapAtMs + 17f, stage).Answered);
        var later = p.Update(7000 + r.AnswerAtMs, stage);
        Assert.True(later.Answered && !later.Snapped && p.Answered, "the number on its own frame, after the bite was seen");
        Assert.False(p.Finished(7000 + r.EndMs - 1f));
        Assert.True(p.Finished(7000 + r.EndMs + 0.01f));   // (a hundredth of a ms: 7000 + 592.9 is not exact in float)
    }

    [Fact]
    public void test_one_cue_on_the_snap_and_it_is_neither_the_trap_nor_the_piranha()
    {
        Assert.Equal("sfx_seeker_jaws_fangs", Jaws.SnapCues[0]);
        Assert.DoesNotContain("sfx_seeker_jaws_snap", Jaws.SnapCues);    // the dry-steel clack belonged to the rejected trap
        Assert.DoesNotContain("sfx_seeker_jaws_chomp", Jaws.SnapCues);   // the chomp belonged to the rejected piranha
        Assert.InRange(Jaws.SnapVolume, 0.25f, 0.5f);                    // quiet enough to repeat
        Assert.True(File.Exists(RepoFile("assets", "audio", "combat", "sfx_seeker_jaws_fangs.wav")));
        // one bite in the ear: the generator has no secondary ticks
        var gen = File.ReadAllText(RepoFile("tools", "asset-pipeline", "make_action_sfx.py"));
        var fangs = gen[gen.IndexOf("def jaws_fangs(", StringComparison.Ordinal)..];
        fangs = fangs[..fangs.IndexOf("\ndef ", StringComparison.Ordinal)];
        Assert.DoesNotContain("for at, g in", fangs);
        Assert.DoesNotContain("thump(", fangs);                          // no pitch-dropping skull, no bone crunch
        Assert.DoesNotContain("crunch(", fangs);
    }

    [Fact]
    public void test_a_reaction_allocates_nothing_frame_to_frame()
    {
        var stage = new Stage();
        var p = Answer(0, 1);
        p.Update(7000, stage);   // the first frame pins each silhouette (a probe may allocate once)
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var ms = 17; ms < 220; ms += 17) p.Update(7000 + ms, stage);
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    // ── THE SCREEN (source law: HuntScreen needs a device to construct) ─────────────────────────

    private static string Hunt() => File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));

    [Fact]
    public void test_a_presented_reaction_never_commits_the_post_bite_trap_clip()
    {
        var src = Hunt();
        var loop = src[src.IndexOf("float? lastTrap = null;", StringComparison.Ordinal)..];
        loop = loop[..loop.IndexOf("if (beatMs is null && lastTrap", StringComparison.Ordinal)];
        Assert.Contains("ReactionRecipes.For(Character.Id, _waveSkills[ri].Def.Id) is not null) continue;", loop);
    }

    [Fact]
    public void test_the_answer_is_scheduled_on_the_snap_and_the_bite_keeps_its_own_moment()
    {
        // PRESENTATION SCHEDULING ONLY. The fight resolved the reflected blow at the bite; on screen a kill's fall and the
        // cue wait for the fangs' snap and the number for its own frame after it; there is NO JAWS flash; the enemy's
        // bite keeps t 0
        var src = Hunt();
        var strike = src[src.IndexOf("case BattleEventKind.Strike:", StringComparison.Ordinal)..];
        strike = strike[..strike.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)];
        Assert.Contains("if (reactionHit && reactionHitPerf is { Answered: false } answering)", strike);
        Assert.Contains("ReactionEchoKind.Number", strike);
        Assert.Contains("if (!auraTick && reactionHit && reactionHitRecipe!.TargetFlash <= 0f)", strike);   // no flash under the fangs
        var down = src[src.IndexOf("case BattleEventKind.EnemyDown:", StringComparison.Ordinal)..];
        down = down[..down.IndexOf("case BattleEventKind.Charge:", StringComparison.Ordinal)];
        Assert.Contains("reactionHitPerf is { Snapped: false } killing", down);
        Assert.Contains("ReactionEchoKind.Death", down);
        Assert.Contains("_deathDeferred.Add(e.Slot)", down);
        // released on the snap (the number on its own frame), dropped on a rewind; a deferred-dead creature is drawn standing until then
        var update = src[src.IndexOf("private void UpdateReactions()", StringComparison.Ordinal)..];
        update = update[..update.IndexOf("private long _reactionAllocBytes", StringComparison.Ordinal)];
        Assert.Contains("ReleaseEchoes(r, present: true, keepNumber: !step.Answered);", update);
        Assert.Contains("if (step.Answered && !step.Snapped) ReleaseEchoes(r, present: true);", update);
        Assert.Contains("Sound?.PlayFirst(r.Recipe.SnapCues", update);              // the cue's transient on the snap
        Assert.Contains("ReleaseEchoes(r, present: _playheadMs >= r.TriggerMs - 1f);", update);
        Assert.Contains("!_replay.CreatureAlive(i) && !_deathDeferred.Contains(i)", src);
        var release = src[src.IndexOf("private void ReleaseEchoes(", StringComparison.Ordinal)..];
        release = release[..release.IndexOf("private readonly List<ReactionPerformance> _reactions", StringComparison.Ordinal)];
        Assert.Contains("case ReactionEchoKind.Flash when r.Recipe.TargetFlash > 0f", release);   // a recipe without a flash flashes nothing
        var spawn = src[src.IndexOf("private ReactionPerformance? SpawnReaction(", StringComparison.Ordinal)..];
        spawn = spawn[..spawn.IndexOf("private void UpdateReactions()", StringComparison.Ordinal)];
        Assert.DoesNotContain("PlayFirst", spawn);                                  // never at the spawn
        // the enemy's own bite is untouched: its thud stays on its own frame, and no glyph is drawn on the champion
        var bite = src[src.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)..];
        bite = bite[..bite.IndexOf("break;", StringComparison.Ordinal)];
        Assert.DoesNotContain("Echo", bite);
        Assert.DoesNotContain("DrawBiteContact", src);
        // the reaction moves nothing and draws nothing behind or in the light pass
        Assert.DoesNotContain("YankOffsetX", src);
        Assert.DoesNotContain("_recoilPx", src);
        Assert.DoesNotContain("in _reactions) r.DrawBehind(", src);
        Assert.DoesNotContain("in _reactions) r.DrawLight(", src);
    }

    [Fact]
    public void test_a_presented_reaction_is_one_sentence_not_five_generic_cues()
    {
        var src = Hunt();
        var skill = src[src.IndexOf("case BattleEventKind.Skill:", StringComparison.Ordinal)..];
        skill = skill[..skill.IndexOf("case BattleEventKind.Heal:", StringComparison.Ordinal)];
        Assert.Contains("SpawnReaction(reactionRecipe, e, castSk.Source)", skill);
        Assert.Contains("if (!performed && reactionRecipe is null) PlaySkillVfx(", skill);                 // no row ring
        Assert.Contains("if (!performed && reactionRecipe is null) Sound?.Play(\"sfx_cast\"", skill);      // no cast breath
        Assert.Contains("if (isReaction && reactionRecipe is null) Sound?.Play(\"sfx_hit\"", skill);       // no reaction thud
        var strike = src[src.IndexOf("case BattleEventKind.Strike:", StringComparison.Ordinal)..];
        strike = strike[..strike.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)];
        Assert.Contains("!performedHit && !reactionHit) Sound?.Play(\"sfx_hit\"", strike);                // the answer's thud
        Assert.Contains("!performedHit && !reactionHit && (_strikeCount++ & 1) == 0", strike);             // ...and its puff
        // the enemy's own bite stays: it is the cause
        var bite = src[src.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)..];
        Assert.Contains("Sound?.Play(\"sfx_hit\", 0.30f, pitch: -0.25f", bite[..bite.IndexOf("break;", StringComparison.Ordinal)]);
    }

    [Fact]
    public void test_the_dock_reads_a_reactions_rearm_from_the_fight_and_never_rebuilds_it()
    {
        var src = Hunt();
        var timing = src[src.IndexOf("private SkillTiming Timing(int i, SkillDef def)", StringComparison.Ordinal)..];
        // the code, not its comments (which name the rule it refuses)
        var reaction = string.Join('\n', timing[..timing.IndexOf("var isPassiveSlot", StringComparison.Ordinal)]
                                             .Split('\n').Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        Assert.Contains("_replay.ReactionReadinessAt(_playheadMs, i, _run?.ReactionReadyAfterLastWave(i))", reaction);
        foreach (var rule in new[] { "RearmMs", "CooldownMultiplier", "CoiledCooldownFactor", "RailCooldownMs", "LastTrapBefore" })
            Assert.DoesNotContain(rule, reaction);
    }
}
