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
/// THE REACTION CONTRACT (ADR-011, JAWS; a FRONTAL SHADOW BITE, the owner, 2026-09-28, with Roni Kangaskorte's "Bite VFX"
/// as the reference): a reaction is presented on its own layer, belongs to its SKILL, never owns the champion's figure
/// and moves nothing. Two rows of Shadow fangs seen from the front (an upper crown with long canines at its corners, a
/// lower row) condense out of mist, wide apart around the head of the creature that bit, charge from a pale
/// lavender-grey to magenta, part further, then slam together across it, interlocked; on that snap they break into the
/// impact (a flash, a slash, a ring, speed lines, splinters, a Shadow haze) that fades in about a third of a second. ONE
/// cue on the snap; the number after it; no flash of the creature's own sprite.
/// </summary>
/// <remarks>
/// The failures these stop were measured across the JAWS work: a row-wide rope ring no one could tie to the bite, five
/// generic sounds on one frame, a post-bite "lay a trap" clip that never played, a dock tile that looked the same armed
/// and rearming, a spring-loaded bear trap the owner rejected as a mechanism, a whole piranha too small and detailed to
/// read at true speed, four teeth that stopped on the creature's outline and never met, four misty teeth that read as
/// "four triangles coming to the middle", and a side-view maw the owner called "a bad jaw biting from the side".
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

    // ── A FRONTAL SHADOW BITE, COMPOSED OF SEVEN PARTS ───────────────────────────────────────────

    /// <summary>The frames a 60 fps screen draws of one reaction (its origin is the first frame, so u = 0, 16.7, 33.3, ...).</summary>
    private static IEnumerable<float> Frames(ReactionRecipe r)
    {
        for (var n = 0; n * (1000f / 60f) < r.GoneMs; n++) yield return n * (1000f / 60f);
    }

    private static readonly Rectangle Whelp = new(1077, 697, 250, 187);    // a bitten whelp's drawn silhouette, as filmed
    private static readonly Rectangle Layout = new(1112, 636, 268, 245);   // ...and its canonical layout body

    /// <summary>What seeker_bite.py wrote about the art (keypose_sources/seeker_bite_spans.json).</summary>
    private static System.Text.Json.JsonElement BiteArt()
        => System.Text.Json.JsonDocument.Parse(File.ReadAllText(RepoFile("tools", "asset-pipeline", "v2", "keypose_sources", "seeker_bite_spans.json"))).RootElement;

    private static float[] Row(System.Text.Json.JsonElement art, string name) => art.GetProperty(name).EnumerateArray().Select(e => e.GetSingle()).ToArray();

    private static (int W, int H) PngSize(string key)
    {
        var png = File.ReadAllBytes(RepoFile("assets", "art", "VFX", "parts", key + ".png"));
        int Be(int at) => (png[at] << 24) | (png[at + 1] << 16) | (png[at + 2] << 8) | png[at + 3];
        return (Be(16), Be(20));
    }

    [Fact]
    public void test_the_reaction_is_a_frontal_bite_composed_of_eight_parts_and_nothing_else()
    {
        // the owner's reference (Roni Kangaskorte's "Bite VFX"): two rows of fangs seen from the FRONT and an impact, all
        // composed at runtime from parts; the side-view maw's dials (a hinge, a gape, a tilt, a jolt) are gone with it
        var r = Jaws;
        var props = typeof(ReactionRecipe).GetProperties().Select(pr => pr.Name).ToArray();
        foreach (var banned in new[] { "Maw", "Hinge", "Gape", "Jolt", "Tilt", "Jaw", "Piranha", "Head", "Eye", "Tail", "Chain", "Tether", "Yank", "Trap", "Mist" })
            Assert.DoesNotContain(props, n => n.StartsWith(banned, StringComparison.Ordinal));
        var keys = props.Where(n => n.EndsWith("Key", StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "FlashKey", "LowerKey", "PuffKey", "RingKey", "ShardsKey", "SlashKey", "StreaksKey", "UpperKey" }, keys);
        foreach (var key in new[] { r.UpperKey, r.LowerKey, r.FlashKey, r.SlashKey, r.RingKey, r.StreaksKey, r.ShardsKey, r.PuffKey })
            Assert.True(File.Exists(RepoFile("assets", "art", "VFX", "parts", key + ".png")), key);
        // the rows: FangStates states side by side, mist to crisp
        Assert.Equal((r.FangStates * r.UpperCell.X, r.UpperCell.Y), PngSize(r.UpperKey));
        Assert.Equal((r.FangStates * r.LowerCell.X, r.LowerCell.Y), PngSize(r.LowerKey));
        // the recipe describes the art the script wrote
        var art = BiteArt();
        Assert.Equal(r.FangStates, art.GetProperty("states").GetInt32());
        Assert.Equal(r.UpperCell.X, art.GetProperty("upper_cell")[0].GetInt32());
        Assert.Equal(r.UpperCell.Y, art.GetProperty("upper_cell")[1].GetInt32());
        Assert.Equal(r.LowerCell.X, art.GetProperty("lower_cell")[0].GetInt32());
        Assert.Equal(r.LowerCell.Y, art.GetProperty("lower_cell")[1].GetInt32());
        Assert.Equal(r.UpperBitePoint.X, art.GetProperty("upper_centre_x").GetSingle());
        Assert.Equal(r.UpperBitePoint.Y, art.GetProperty("upper_inner_tip_y").GetSingle());
        Assert.Equal(r.LowerBitePoint.X, art.GetProperty("lower_centre_x").GetSingle());
        Assert.Equal(r.LowerBitePoint.Y, art.GetProperty("lower_tip_y").GetSingle());
        Assert.Equal(r.CrownArtWidth, art.GetProperty("upper_width").GetSingle());
        // authored procedurally: every random draw derives from one seed (no unseeded generator); no target flash
        var gen = File.ReadAllText(RepoFile("tools", "asset-pipeline", "v2", "seeker_bite.py"));
        Assert.Contains($"STATES = {r.FangStates}", gen);
        Assert.Contains("SEED = 20260928", gen);
        Assert.Equal(gen.Split("default_rng(").Length, gen.Split("default_rng(SEED").Length);
        Assert.False(r.Callout);
        Assert.Equal(0f, r.TargetFlash);
    }

    [Fact]
    public void test_the_crown_has_long_canines_at_its_corners_and_the_rows_interlock()
    {
        // the reference's jaw: an upper CROWN (two long canines at its corners, five packed teeth between them) over a
        // lower ROW (four middle teeth and a taller one at each end); shut, each middle lower point rises between two upper
        // points, and the canines close outside the whole row
        var r = Jaws;
        var art = BiteArt();
        var inner = Row(art, "upper_inner_tip_x").Select(x => x - r.UpperBitePoint.X).ToArray();
        var canines = Row(art, "upper_canine_tip_x").Select(x => x - r.UpperBitePoint.X).ToArray();
        var lower = Row(art, "lower_tip_x").Select(x => x - r.LowerBitePoint.X).ToArray();
        var ends = Row(art, "lower_end_tip_x").Select(x => x - r.LowerBitePoint.X).ToArray();
        Assert.Equal(5, inner.Length);
        Assert.Equal(2, canines.Length);
        Assert.Equal(4, lower.Length);
        Assert.Equal(2, ends.Length);
        Assert.True(canines.Min() < ends.Min() && canines.Max() > ends.Max(), "the canines do not close outside the lower row");
        Assert.True(ends.Min() < inner.Min() && ends.Max() > inner.Max(), "the end teeth do not rise outside the upper inner teeth");
        // each row's points lie on ONE line, so the shut pose interlocks along the whole row (opposite curves met only at
        // the outer teeth); the end teeth are the taller ones
        Assert.Single(Row(art, "upper_inner_tip_y_all").Distinct());
        Assert.Single(Row(art, "lower_tip_y_all").Distinct());
        Assert.True(Row(art, "lower_end_tip_y").Max() < art.GetProperty("lower_tip_y").GetSingle(), "the end teeth are not taller");
        var innerLength = art.GetProperty("upper_inner_tip_y").GetSingle();
        Assert.True(art.GetProperty("upper_canine_tip_y").GetSingle() - innerLength >= 0.3f * innerLength, "the canines are not the long teeth");
        // interlock: between two neighbouring upper points there is exactly one lower point (the rows alternate)
        for (var i = 1; i < inner.Length; i++)
            Assert.Single(lower, x => x > inner[i - 1] && x < inner[i]);
        // the crown is wider than the lower row, and both are symmetric about the centre line
        Assert.True(art.GetProperty("upper_width").GetSingle() > art.GetProperty("lower_width").GetSingle());
        Assert.InRange(inner.Sum(), -1f, 1f);
        Assert.InRange(lower.Sum(), -1f, 1f);
    }

    [Fact]
    public void test_the_bite_frames_the_creature_from_the_front_and_meets_on_it()
    {
        var r = Jaws;
        // OPEN: centred on the creature's canonical body, the crown above the meeting line and the row below it
        var open = ReactionPerformance.Fangs(r, Whelp, Layout, r.CloseFromMs - 1f);
        var centre = Layout.X + r.CentreShare * Layout.Width;
        Assert.Equal(centre, open.UpperAt.X, 3);
        Assert.Equal(centre, open.LowerAt.X, 3);
        Assert.True(open.UpperAt.Y < open.Meet.Y && open.LowerAt.Y > open.Meet.Y, "the rows do not open about the meeting line");
        // the rows meet on the DRAWN silhouette (not the layout's canvas), about its middle
        Assert.Equal(Whelp.Y + r.MeetShare * Whelp.Height, open.Meet.Y, 3);
        Assert.InRange(r.MeetShare, 0.4f, 0.6f);
        // sized off the canonical width, and the open rows stay near the creature: the crown's cell below its health bar,
        // the row's cell above the skill dock (the frame the owner's review found fitting)
        Assert.Equal(r.CrownWidthShare * Layout.Width, open.CrownWidth, 3);
        var crownTop = open.UpperAt.Y - r.UpperBitePoint.Y * open.Scale;
        var rowBottom = open.LowerAt.Y + (r.LowerCell.Y - r.LowerBitePoint.Y) * open.Scale;
        Assert.True(crownTop >= Whelp.Y - 0.5f * Whelp.Height, $"the open crown rises too far above the creature: {crownTop:0}");
        Assert.True(rowBottom <= Whelp.Bottom + 0.35f * Whelp.Height, $"the open row sinks too far below the creature: {rowBottom:0}");
        // SHUT: the bite lines have passed each other (the rows interlock) on the meeting line
        var shut = ReactionPerformance.Fangs(r, Whelp, Layout, r.SnapAtMs);
        Assert.True(shut.UpperAt.Y > shut.LowerAt.Y, "the rows do not interlock at the snap");
        Assert.Equal(r.InterlockArtPx * shut.CrownWidth / r.CrownArtWidth, shut.UpperAt.Y - shut.LowerAt.Y, 2);
        Assert.Equal(open.Meet, shut.Meet);
    }

    [Fact]
    public void test_the_teeth_condense_out_of_mist_and_charge_from_pale_lavender_to_magenta()
    {
        var r = Jaws;
        // they condense out of mist, one strip state a frame, never a jump
        Assert.Equal(0, ReactionPerformance.StateAt(r, 0f));
        Assert.Equal(r.FangStates - 1, ReactionPerformance.StateAt(r, r.AppearMs));
        var states = Frames(r).Select(u => ReactionPerformance.StateAt(r, u)).ToArray();
        Assert.True(states.Zip(states.Skip(1), (a, b) => Math.Abs(b - a)).Max() <= 1, $"the condensation jumps: {string.Join(",", states)}");
        Assert.Equal(Enumerable.Range(0, r.FangStates), states.Distinct().OrderBy(x => x));   // every state is shown, none skipped
        Assert.InRange(ReactionPerformance.TeethOpacity(r, 0f), 0.1f, 0.25f);
        var ramp = Frames(r).Where(u => u <= r.AppearMs + 17f).Select(u => ReactionPerformance.TeethOpacity(r, u)).ToArray();
        Assert.True(ramp.Zip(ramp.Skip(1), (a, b) => b - a).Max() <= 0.5f, "the teeth pop in");
        Assert.True(ReactionPerformance.Fangs(r, Whelp, Layout, 0f).Scale > ReactionPerformance.Fangs(r, Whelp, Layout, r.AppearMs).Scale, "the mist does not tighten in");
        // the CHARGE: pale lavender after the appear, heating steadily to magenta by the snap
        var cold = ReactionPerformance.TeethColor(r, r.AppearMs);
        var hot = ReactionPerformance.TeethColor(r, r.SnapAtMs - 1f);
        Assert.True(cold.R > 190 && cold.G > 180 && cold.B > 220 && cold.G < 225, $"the teeth do not start a pale lavender-grey: {cold}");
        // ...and not at full strength: the fangs appearing must not outshine the bite (they reach whole on the snap frame)
        Assert.InRange(ReactionPerformance.TeethOpacity(r, r.AppearMs), r.FormedOpacity - 0.02f, r.FormedOpacity + 0.02f);
        Assert.InRange(r.FormedOpacity, 0.6f, 0.9f);
        Assert.Equal(1f, ReactionPerformance.TeethOpacity(r, r.SnapAtMs), 3);
        Assert.True(Frames(r).Count(u => u > 0f && u < r.AppearMs) >= 5, "the teeth condense in too few frames (a pop)");
        Assert.True(hot.G < 120 && hot.R > 180 && hot.B > 200, $"the teeth do not end magenta: {hot}");
        var charge = Frames(r).Select(u => ReactionPerformance.Charge(r, u)).ToArray();
        Assert.True(charge.Zip(charge.Skip(1), (a, b) => b - a).Min() >= 0f, "the charge goes back");
        // on the snap frame the teeth flare white-hot
        var flare = ReactionPerformance.TeethColor(r, r.SnapAtMs);
        Assert.True(flare.G > hot.G + 40, $"the snap does not flare: {hot} -> {flare}");
    }

    [Fact]
    public void test_the_rows_wind_up_then_slam_shut_accelerating_into_the_snap()
    {
        var r = Jaws;
        // the WIND-UP: the rows part a little further before the slam
        Assert.True(ReactionPerformance.Gap(r, Whelp.Height, 1f, r.CloseFromMs) > ReactionPerformance.Gap(r, Whelp.Height, 1f, r.WindUpFromMs) + 5f, "no wind-up");
        // the SNAP is on a frame, and it ends a close that only ever closes, accelerating, seen part-way on at least two
        // frames, with the largest step on the snap frame itself
        Assert.Contains(Frames(r), u => Math.Abs(u - r.SnapAtMs) < 0.5f);
        var positions = Frames(r).Where(u => u >= r.CloseFromMs && u <= r.SnapAtMs + 0.5f).Select(u => ReactionPerformance.Close(r, u)).ToArray();
        var steps = positions.Zip(positions.Skip(1), (a, b) => b - a).ToArray();
        for (var i = 1; i < steps.Length; i++)
            Assert.True(steps[i] >= steps[i - 1] - 1e-4f, $"the close does not accelerate: {string.Join(", ", steps.Select(x => x.ToString("0.00")))}");
        Assert.True(positions.Count(c => c is > 0.05f and < 0.95f) >= 2, "the close is not seen");
        Assert.Equal(steps.Max(), steps[^1]);
        Assert.True(steps[^1] <= 0.6f, "one frame carries most of the close");
        // shut, the rows stay shut while the teeth break
        Assert.Equal(ReactionPerformance.Gap(r, Whelp.Height, 1f, r.SnapAtMs), ReactionPerformance.Gap(r, Whelp.Height, 1f, r.SnapAtMs + r.TeethBreakMs), 3);
        // the whole bite is longer than the misty teeth were (the owner allowed it) and still under a second
        Assert.InRange(r.EndMs, 500f, 800f);
    }

    [Fact]
    public void test_on_the_snap_the_teeth_break_into_the_impact()
    {
        var r = Jaws;
        const float Frame = 1000f / 60f;
        // nothing of the impact before the snap
        Assert.Equal(default, ReactionPerformance.Burst(r, r.SnapAtMs - Frame));
        // the teeth: whole on the snap frame, half the next, gone after
        Assert.Equal(1f, ReactionPerformance.TeethOpacity(r, r.SnapAtMs), 3);
        Assert.InRange(ReactionPerformance.TeethOpacity(r, r.SnapAtMs + Frame), 0.3f, 0.7f);
        Assert.True(ReactionPerformance.TeethOpacity(r, r.SnapAtMs + 2f * Frame) < ReactionRecipe.VisibleFloor + 0.05f);
        // the FLASH arrives on the snap frame and peaks on the next; the RING, the SPEED LINES and the PUFF grow; the
        // SPLINTERS fly out from a frame after, turning
        var snap = ReactionPerformance.Burst(r, r.SnapAtMs);
        var next = ReactionPerformance.Burst(r, r.SnapAtMs + Frame);
        Assert.True(snap.FlashAlpha > 0.9f && snap.FlashScale < next.FlashScale && next.FlashScale >= 0.99f, "the flash does not swell in one frame");
        Assert.Equal(1f, ReactionPerformance.Burst(r, r.SnapAtMs + 2f * Frame - 0.5f).FlashScale, 3);   // held whole two frames
        Assert.Equal(0f, snap.ShardsAlpha);
        Assert.True(next.ShardsAlpha > 0.9f);
        // the SLASH strikes across the flash from the frame after the snap, and is gone within a few frames
        Assert.Equal(0f, snap.SlashAlpha);
        Assert.True(next.SlashAlpha > 0.9f);
        Assert.True(ReactionPerformance.Burst(r, r.SnapAtMs + 6f * Frame).SlashAlpha < ReactionRecipe.VisibleFloor);
        var drawn = Frames(r).Where(u => u >= r.SnapAtMs - 0.5f).ToArray();
        var burst = drawn.Select(u => ReactionPerformance.Burst(r, u)).ToArray();
        for (var i = 1; i < burst.Length; i++)
        {
            Assert.True(burst[i].RingScale >= burst[i - 1].RingScale && burst[i].StreaksScale >= burst[i - 1].StreaksScale
                        && burst[i].PuffScale >= burst[i - 1].PuffScale && burst[i].ShardsTurn >= burst[i - 1].ShardsTurn, $"the impact shrinks back at {drawn[i]:0} ms");
            // each part fades in steps, never cut (the splinters' arrival is the one step up)
            Assert.True(burst[i - 1].RingAlpha - burst[i].RingAlpha <= 0.3f && burst[i - 1].StreaksAlpha - burst[i].StreaksAlpha <= 0.35f
                        && burst[i - 1].FlashAlpha - burst[i].FlashAlpha <= 0.35f && burst[i - 1].PuffAlpha - burst[i].PuffAlpha <= 0.2f
                        && burst[i - 1].SlashAlpha - burst[i].SlashAlpha <= 0.3f, $"a part is cut at {drawn[i]:0} ms");
            // the flash shrinks to a hot point before it has faded (a large, fading star read as a grey sticker)
            if (burst[i].FlashAlpha is > 0.05f and < 0.5f)
                Assert.True(burst[i].FlashScale < 0.6f, $"the flash's tail is a big dull star at {drawn[i]:0} ms");
        }
        // the payoff is bigger than the jaw that causes it: the DRAWN ring (its arcs sit at 0.8 of the texture's width)
        // grows past the crown while it is still bright, and the speed lines reach well past it
        Assert.True(burst.Where(p => p.RingAlpha >= 0.5f).Max(p => 0.8f * r.RingSize * p.RingScale) > 1.1f, "the ring stays inside the crown while it is bright");
        Assert.True(r.StreaksSize * burst.Max(p => p.StreaksScale) > 2.5f, "the speed lines stay close");
        // the slash is longer than the flash, so its ends reach past the star
        Assert.True(r.SlashSize > r.FlashSize * 1.3f, "the slash is swallowed by the flash");
        // the splinters are the LAST to fade (they carry the ending)
        float LastSeen(Func<ReactionPerformance.BurstPose, float> part) => drawn.Zip(burst).Where(q => part(q.Second) >= 0.05f).Select(q => q.First).DefaultIfEmpty(0f).Max();
        Assert.True(LastSeen(p => p.ShardsAlpha) > LastSeen(p => p.RingAlpha) && LastSeen(p => p.ShardsAlpha) > LastSeen(p => p.StreaksAlpha)
                    && LastSeen(p => p.ShardsAlpha) > LastSeen(p => p.FlashAlpha), "the splinters are not the last to fade");
        // by the end it is gone; the whole impact lasts about a third of a second
        Assert.InRange(r.BurstMs, 250f, 400f);
        var last = ReactionPerformance.Burst(r, Frames(r).Last());
        Assert.True(last.FlashAlpha < 0.1f && last.RingAlpha < 0.15f && last.StreaksAlpha < 0.15f && last.ShardsAlpha < 0.2f && last.PuffAlpha < 0.1f, "the impact cuts off");
        Assert.Equal(default, ReactionPerformance.Burst(r, r.GoneMs));
    }

    [Fact]
    public void test_when_one_answer_bites_two_creatures_each_gets_its_own_bite()
    {
        // two whelps of a pack as filmed (layout bodies 136 px apart, overlapping): the front one gets the full bite, the
        // one behind it a smaller bite on its own body; their crowns never overlap once the teeth have formed
        var r = Jaws;
        var front = new Rectangle(1112, 630, 268, 245);
        var rear = new Rectangle(1248, 630, 268, 245);
        foreach (var u in Frames(r).Where(u => u >= r.AppearMs))
        {
            var a = ReactionPerformance.Fangs(r, front, front, u);
            var b = ReactionPerformance.Fangs(r, rear, rear, u, secondary: true);
            Assert.True(b.CrownWidth <= 0.75f * a.CrownWidth, "the rear bite is not clearly the smaller one");
            Assert.True(a.Meet.X + 0.5f * a.CrownWidth <= b.Meet.X - 0.5f * b.CrownWidth + 2f, $"the two crowns overlap at {u:0} ms");
            Assert.Equal(rear.X + r.CentreShare * rear.Width, b.Meet.X, 3);
        }
        // both bites snap together (the fight answered both on the one bite: a later second bite let its creature fall
        // and its number show before its own rows had shut); the reaction's life is the same with one target or two
        Assert.Equal(r.EndMs, Answer(0, 1).EndMs, 3);
        Assert.Equal(r.EndMs, Answer(0).EndMs, 3);
        // the bite is on the creature's FRONT, where its head is (every creature faces the Seeker, left)
        Assert.InRange(r.CentreShare, 0.2f, 0.4f);
    }

    [Fact]
    public void test_the_front_creature_is_the_one_nearest_the_seeker_whatever_the_fight_order()
    {
        // the answer's targets come in the fight's event order, not front to back: the full bite goes to the creature whose
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

    [Fact]
    public void test_the_bite_draws_its_material_under_the_champion_and_its_light_in_the_light_pass()
    {
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "ReactionPerformance.cs"));
        string Body(string signature)
        {
            var at = src[src.IndexOf(signature, StringComparison.Ordinal)..];
            return at[..at.IndexOf("\n    }", StringComparison.Ordinal)];
        }
        var material = Body("public void DrawMaterial(");
        var light = Body("public void DrawLight(");
        // the pose the tests pin is the pose drawn, in both passes, on each target's own clock; the front creature as tested
        Assert.Contains("var f = Fangs(Recipe, body, layout, u, secondary: k != front);", material);
        Assert.Contains("var f = Fangs(Recipe, body, layout, u, secondary);", light);
        Assert.Contains("var secondary = k != _front;", light);
        Assert.Contains("var front = FrontTarget(stage, Targets);", material);
        // the glow builds with the charge (whole on the snap), so the formed teeth stay pale and the snap is the peak
        Assert.Contains("Recipe.GlowAtFormed", material);
        Assert.InRange(Jaws.GlowAtFormed, 0.1f, 0.4f);
        // material (under the champion): the haze, the two rows and their glow (a zero alpha: it only adds light), the
        // splinters (6 a target); light: the ring, the speed lines, the flash, the slash (4 a target); the TEETH are never
        // drawn in the light pass (drawn there, the glowing teeth landed on top of the Seeker)
        Assert.Equal(4, material.Split("b.Draw(").Length - 1);
        Assert.Equal(2, material.Split("DrawCentred(").Length - 1);
        Assert.Contains("(byte)0)", material);
        Assert.Equal(1, light.Split("b.Draw(").Length - 1);
        Assert.Equal(3, light.Split("DrawCentred(").Length - 1);
        Assert.Contains("Recipe.PuffKey", material);
        Assert.Contains("Recipe.ShardsKey", material);
        Assert.Contains("Recipe.FlashKey", light);
        Assert.Contains("Recipe.RingKey", light);
        Assert.Contains("Recipe.StreaksKey", light);
        Assert.Contains("Recipe.SlashKey", light);
        Assert.DoesNotContain("Recipe.UpperKey", light);
        Assert.DoesNotContain("Recipe.LowerKey", light);
        // the screen: the material after the creatures and BEFORE the champion; the light inside the shared additive pass
        var hunt = Hunt();
        var jaws = hunt.IndexOf("foreach (var r in _reactions) r.DrawMaterial(b, this, _playheadMs);", StringComparison.Ordinal);
        Assert.True(jaws > hunt.IndexOf("else DrawNormalEnemy(b, attacking);", StringComparison.Ordinal), "the bite is drawn before the creatures");
        Assert.True(jaws < hunt.IndexOf("DrawChampion(b, _champDrawBox, dead: _mode == Mode.Downed);", StringComparison.Ordinal), "the bite is drawn over the champion");
        var glow = hunt.IndexOf("foreach (var r in _reactions) r.DrawLight(b, this, _playheadMs);", StringComparison.Ordinal);
        Assert.True(glow > hunt.IndexOf("_vfx.BeginLight(b);", StringComparison.Ordinal) && glow < hunt.IndexOf("_vfx.EndLight(b);", StringComparison.Ordinal), "the bite's light is not in the light pass");
        Assert.DoesNotContain("r.DrawUnder(", hunt);
    }

    [Fact]
    public void test_no_jaws_flash_and_the_number_comes_a_few_frames_after_the_snap()
    {
        var r = Jaws;
        Assert.Equal(0f, r.TargetFlash);                                        // the bite IS the hit feedback
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
        Assert.True(p.Finished(7000 + r.EndMs + 0.01f));   // (a hundredth of a ms: 7000 + 676.6 is not exact in float)
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
        // cue wait for the rows' snap and the number for its own frame after it; there is NO JAWS flash; the enemy's
        // bite keeps t 0
        var src = Hunt();
        var strike = src[src.IndexOf("case BattleEventKind.Strike:", StringComparison.Ordinal)..];
        strike = strike[..strike.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)];
        Assert.Contains("if (reactionHit && reactionHitPerf is { Answered: false } answering)", strike);
        Assert.Contains("ReactionEchoKind.Number", strike);
        Assert.Contains("if (!auraTick && reactionHit && reactionHitRecipe!.TargetFlash <= 0f)", strike);   // no flash under the bite
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
        // the reaction moves nothing and draws nothing behind the figures (its light is the impact's glow, in the light pass)
        Assert.DoesNotContain("YankOffsetX", src);
        Assert.DoesNotContain("_recoilPx", src);
        Assert.DoesNotContain("in _reactions) r.DrawBehind(", src);
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
