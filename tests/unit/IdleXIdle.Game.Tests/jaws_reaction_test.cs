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
/// THE REACTION CONTRACT (ADR-011, JAWS; SHADOW FANGS MADE OF MIST, the owner, 2026-09-28): a reaction is presented on
/// its own layer, belongs to its SKILL, never owns the champion's figure and moves nothing; it is FOUR TEETH MADE OF MIST
/// and nothing else (no separate fog, no creature, no eye, no glint, no residue, no particles, no additive light, no
/// flash): drifts of Shadow mist come in and condense into teeth, the jaws CLOSE on the creature (the upper points pass
/// the lower ones across its middle, the rows interlocking), hold the bite, then let go and dissolve back into mist;
/// ONE cue on the snap; the number a few frames after the snap.
/// </summary>
/// <remarks>
/// The failures these stop were measured across the JAWS work: a row-wide rope ring no one could tie to the bite, five
/// generic sounds on one frame, a post-bite "lay a trap" clip that never played, a dock tile that looked the same armed
/// and rearming, a spring-loaded bear trap with a chain the owner rejected as a mechanism, piranhas too small to read at
/// true speed, and then four teeth that stopped on the creature's outline and never met (the owner: "the teeth don't
/// close"), popping in as hard sprites where the owner wanted them to come like mist.
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

    // ── FOUR TEETH MADE OF MIST, AND NOTHING ELSE ────────────────────────────────────────────────

    /// <summary>The frames a 60 fps screen draws of one reaction (its origin is the first frame, so u = 0, 16.7, 33.3, ...).</summary>
    private static IEnumerable<float> Frames(ReactionRecipe r)
    {
        for (var n = 0; n * (1000f / 60f) < r.GoneMs; n++) yield return n * (1000f / 60f);
    }

    [Fact]
    public void test_the_reaction_is_four_misty_teeth_and_nothing_else()
    {
        // no separate fog, no creature, no swarm, no residue, no glint, no particles, no behind or light pass: the recipe
        // has no such dials and the layer has no such passes; its only art is the one misty fang strip
        var props = typeof(ReactionRecipe).GetProperties().Select(pr => pr.Name).ToArray();
        foreach (var banned in new[] { "Jaw", "Piranha", "Head", "Mouth", "Eye", "Tail", "Residue", "Glint", "Behind", "Smear", "Spark", "Particle",
                                       "Trail", "Yank", "Chain", "Tether", "Whip", "Pivot", "Half", "Shut", "Mist", "Wisp", "Pocket" })
            Assert.DoesNotContain(props, n => n.StartsWith(banned, StringComparison.Ordinal));
        Assert.Equal(new[] { "FangKey" }, props.Where(n => n.EndsWith("Key", StringComparison.Ordinal)).ToArray());
        var methods = typeof(ReactionPerformance).GetMethods().Select(m => m.Name).ToArray();
        foreach (var pass in new[] { "DrawUnder", "DrawBehind", "DrawLight" })
            Assert.DoesNotContain(pass, methods);
        Assert.Contains("DrawMaterial", methods);
        Assert.False(Jaws.Callout);
    }

    [Fact]
    public void test_the_teeth_are_one_misty_fang_in_condensation_states()
    {
        var r = Jaws;
        // the strip: FangStates states side by side (read off the PNG's own header, no decoder needed)
        var png = File.ReadAllBytes(RepoFile("assets", "art", "VFX", "parts", r.FangKey + ".png"));
        int Be(int at) => (png[at] << 24) | (png[at + 1] << 16) | (png[at + 2] << 8) | png[at + 3];
        Assert.Equal(r.FangStates * r.StateWidth, Be(16));
        Assert.Equal(r.StateHeight, Be(20));
        Assert.InRange(r.FangStates, 12, 24);                    // enough states that ONE per frame reads as a smooth condensing
        Assert.InRange(r.TipPoint.X, 0f, r.StateWidth);
        Assert.InRange(r.TipPoint.Y, r.StateHeight * 0.7f, r.StateHeight);
        Assert.True(r.ToothArtHeight < r.StateHeight, "the loose mist is wider and taller than the tooth it becomes");
        // authored procedurally from the approved fang's outline, on ONE fixed noise field (so the states condense, never boil)
        var gen = File.ReadAllText(RepoFile("tools", "asset-pipeline", "v2", "seeker_mist_fangs.py"));
        Assert.Contains("import seeker_fangs as F", gen);
        Assert.Equal(1, gen.Split("default_rng(").Length - 1);
        Assert.Contains($"STATES = {r.FangStates}", gen);
        // the lower tooth is the state flipped vertically: its origin is the same point measured from the state's bottom
        Assert.Equal(r.TipPoint, ReactionPerformance.ToothOrigin(r, lower: false));
        Assert.Equal(new Vector2(r.TipPoint.X, r.StateHeight - 1f - r.TipPoint.Y), ReactionPerformance.ToothOrigin(r, lower: true));
    }

    private static readonly Rectangle Whelp = new(1045, 700, 250, 187);   // a bitten whelp's drawn silhouette, as filmed

    [Fact]
    public void test_the_jaws_close_the_upper_teeth_pass_the_lower_ones_and_the_rows_interlock()
    {
        var r = Jaws;
        Span<ReactionPerformance.ToothPose> t = stackalloc ReactionPerformance.ToothPose[4];
        ReactionPerformance.ToothPoses(r, Whelp, r.SnapAtMs, t);
        // SHUT, on screen: both upper points are BELOW both lower points (the teeth passed each other), by a visible margin
        var upperPointY = Math.Min(t[0].At.Y, t[1].At.Y);
        var lowerPointY = Math.Max(t[2].At.Y, t[3].At.Y);
        Assert.True(upperPointY - lowerPointY >= 0.08f * Whelp.Height, $"the teeth do not close: {upperPointY - lowerPointY:0} px");
        // ...across the creature's lower middle (off its face and eyes)
        Assert.InRange((upperPointY + lowerPointY) * 0.5f, Whelp.Y + 0.5f * Whelp.Height, Whelp.Y + 0.7f * Whelp.Height);
        // they INTERLOCK like a shut mouth: upper-left, lower-left, lower-right, upper-right, left to right...
        Assert.True(t[0].At.X < t[2].At.X && t[2].At.X < t[3].At.X && t[3].At.X < t[1].At.X, "the rows do not interlock");
        // ...each upper point close beside its lower neighbour (the teeth meet, they do not slide past with air between)...
        var upperTooth = r.UpperToothShare * Whelp.Height;
        Assert.True(t[2].At.X - t[0].At.X <= 0.35f * upperTooth, "the rows slide past each other with a gap");
        // ...and the lower pair far enough apart that it never merges into one shape (the tooth is ~0.46 of its height wide)
        Assert.True(t[3].At.X - t[2].At.X >= 0.45f * upperTooth * r.LowerToothScale, "the lower pair overlaps itself");
        // each point leans IN: the left teeth turn so their points swing toward the centre (the lower row mirrored)
        Assert.True(t[0].Rotation < 0f && t[1].Rotation > 0f && t[2].Rotation > 0f && t[3].Rotation < 0f);
        // the jaw forms on the body: the open points are inside the creature's top and bottom (clear of its health bar
        // above and the skill dock below), and it stays shut through the bite
        Assert.InRange(r.UpperOpenShare, 0.05f, 0.35f);
        Assert.InRange(r.LowerOpenShare, 0.15f, 0.40f);
        Span<ReactionPerformance.ToothPose> h = stackalloc ReactionPerformance.ToothPose[4];
        ReactionPerformance.ToothPoses(r, Whelp, r.ReleaseAtMs - 1f, h);
        Assert.Equal(t[0].At.Y, h[0].At.Y, 3);
    }

    [Fact]
    public void test_the_closing_is_seen_and_accelerates_into_the_snap()
    {
        var r = Jaws;
        // the rows only ever close until the snap
        var last = -1f;
        for (var u = 0f; u <= r.SnapAtMs; u += 2f)
        {
            var c = ReactionPerformance.Close(r, u);
            Assert.True(c >= last - 1e-5f, "the jaws only ever close until the snap");
            last = c;
        }
        Assert.Equal(0f, ReactionPerformance.Close(r, r.GatherMs));
        Assert.Equal(1f, ReactionPerformance.Close(r, r.SnapAtMs));
        // at 60 fps at least three frames show the jaws PART-WAY (visibly moved, not yet shut): the closing is SEEN
        var closing = Frames(r).Where(u => ReactionPerformance.Close(r, u) is > 0.05f and < 0.95f).ToArray();
        Assert.True(closing.Length >= 3, $"only {closing.Length} frame(s) show the jaws part-way closed");
        // no single frame step carries more than half the travel (the snap does not teleport), and each step is larger
        // than the one before (it accelerates into the bite)
        var positions = Frames(r).Where(u => u >= r.GatherMs && u < r.SnapAtMs - 0.5f).Append(r.SnapAtMs)
                                 .Select(u => ReactionPerformance.Close(r, u)).ToArray();
        var steps = positions.Zip(positions.Skip(1), (x, y) => y - x).ToArray();
        Assert.True(steps.Max() <= 0.5f, "one frame carries most of the close");
        for (var i = 1; i < steps.Length - 1; i++)
            Assert.True(steps[i] >= steps[i - 1] - 1e-4f, $"the close does not accelerate: steps {string.Join(", ", steps.Select(x => x.ToString("0.00")))}");
    }

    [Fact]
    public void test_the_teeth_come_as_mist_condense_bite_and_dissolve_back_into_mist()
    {
        var r = Jaws;
        // they grow out of nothing as LOOSE mist, far apart and larger, and the loose states are what is seen for most of
        // the gathering (the condensation eases in); wholly condensed when the jaws shut
        Assert.Equal(0, ReactionPerformance.StateAt(r, 0f));
        Assert.InRange(ReactionPerformance.Opacity(r, 0f), 0.05f, 0.25f);
        // it GROWS in over several frames: no 60 fps step of the fade-in adds more than 0.4 (the mist never pops in)
        var ramp = Frames(r).Where(u => u <= r.OpacityRampMs + 17f).Select(u => ReactionPerformance.Opacity(r, u)).ToArray();
        Assert.True(ramp.Zip(ramp.Skip(1), (x, y) => y - x).Max() <= 0.4f, $"the mist pops in: {string.Join(", ", ramp.Select(x => x.ToString("0.00")))}");
        Assert.True(ReactionPerformance.StateAt(r, 0.6f * r.GatherMs) <= (r.FangStates - 1) / 3, "the smoke condenses too early to be seen");
        var (scale0, spread0) = ReactionPerformance.MistSpread(r, 0f);
        Assert.True(scale0 >= 1.2f && spread0 >= 1.5f, "the mist does not come in");
        var (scaleG, spreadG) = ReactionPerformance.MistSpread(r, r.GatherMs);
        Assert.Equal(1f, scaleG, 3);
        Assert.Equal(1f, spreadG, 3);
        Assert.Equal(r.FangStates - 1, ReactionPerformance.StateAt(r, r.SnapAtMs));
        Assert.Equal(r.FangStates - 1, ReactionPerformance.StateAt(r, r.ReleaseAtMs - 1f));
        Assert.Equal(1f, ReactionPerformance.Opacity(r, r.ReleaseAtMs - 1f));
        var prev = 0f;
        for (var u = 0f; u <= r.SnapAtMs; u += 2f)
        {
            var c = ReactionPerformance.Condense(r, u);
            Assert.True(c >= prev - 1e-5f, "they only ever condense until the snap");
            prev = c;
        }
        // they LOOSEN into smoke over several frames and fade evenly: at least three 60 fps frames show teeth coming apart,
        // halfway through the dissolve they are smoke yet still more than half there, and the last frame is faint
        var loosening = Frames(r).Count(u => u > r.ReleaseAtMs && ReactionPerformance.Condense(r, u) is > 0.05f and < 0.95f);
        Assert.True(loosening >= 3, $"the teeth come apart in only {loosening} frame(s)");
        var mid = (r.ReleaseAtMs + r.GoneMs) * 0.5f;
        Assert.True(ReactionPerformance.Condense(r, mid) <= 0.3f, "halfway through, the teeth are still teeth");
        Assert.True(ReactionPerformance.Opacity(r, mid) >= 0.5f, "halfway through, the smoke is already gone");
        Assert.True(ReactionPerformance.Opacity(r, Frames(r).Last()) <= 0.25f, "the last frame cuts off a dense smoke");
        prev = 1f;
        for (var u = r.ReleaseAtMs; u < r.GoneMs; u += 2f)
        {
            var c = ReactionPerformance.Condense(r, u);
            Assert.True(c <= prev + 1e-5f, "they only ever dissolve after the release");
            prev = c;
        }
        // the jaws open, and the smoke rises off the creature and spreads as it goes, then nothing is left
        Assert.Equal(0f, ReactionPerformance.ReleaseDrift(r, r.ReleaseAtMs));
        Assert.Equal(0f, ReactionPerformance.ReleaseRise(r, r.ReleaseAtMs));
        Assert.InRange(r.ReleaseDriftPx, 6f, 40f);
        Assert.InRange(r.ReleaseRisePx, 15f, 40f);
        Assert.InRange(ReactionPerformance.MistSpread(r, r.GoneMs - 0.5f).Scale, 1.25f, 1.6f);
        Assert.Equal(0f, ReactionPerformance.Opacity(r, r.GoneMs));
        var o = 1f;
        for (var u = r.ReleaseAtMs; u < r.GoneMs; u += 1f)
        {
            var a = ReactionPerformance.Opacity(r, u);
            Assert.True(a <= o + 1e-5f && o - a < 0.04f, $"a smooth fade at {u} ms");
            o = a;
        }
        // the bite holds long enough to be read, and the phrase stays a short reaction
        Assert.InRange(r.HoldMs, 50f, 90f);
        Assert.InRange(r.EndMs, 250f, 320f);
    }

    [Fact]
    public void test_the_jaws_draw_over_everything_four_sprites_a_target()
    {
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "ReactionPerformance.cs"));
        var draw = src[src.IndexOf("public void DrawMaterial(", StringComparison.Ordinal)..];
        draw = draw[..draw.IndexOf("\n    }", StringComparison.Ordinal)];   // CRLF-safe: no trailing newline in the key
        Assert.Contains("for (var f = 0; f < 4; f++)", draw);                   // four teeth...
        Assert.Equal(1, draw.Split("b.Draw(").Length - 1);                      // ...one state each (no thinning cross-fade)
        Assert.Contains("ToothPoses(Recipe, body, u, teeth);", draw);           // the pose the tests pin is the pose drawn
        Assert.Contains("ToothOrigin(Recipe, p.Lower)", draw);
        Assert.Contains("ReactionRecipe.FangVisibleFloor", draw);
        // the screen draws the jaws AFTER the creatures (over the creature they bite) and has no pass under them
        var hunt = Hunt();
        var jaws = hunt.IndexOf("foreach (var r in _reactions) r.DrawMaterial(b, this, _playheadMs);", StringComparison.Ordinal);
        Assert.True(jaws > hunt.IndexOf("else DrawNormalEnemy(b, attacking);", StringComparison.Ordinal), "the jaws are drawn before the creatures");
        Assert.DoesNotContain("r.DrawUnder(", hunt);
    }

    [Fact]
    public void test_no_jaws_flash_and_the_number_comes_a_few_frames_after_the_snap()
    {
        var r = Jaws;
        Assert.Equal(0f, r.TargetFlash);                                        // the fangs ARE the hit feedback
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
        Assert.True(later.Answered && !later.Snapped && p.Answered, "the number on its own frame, after the fangs were seen");
        Assert.False(p.Finished(7000 + r.EndMs - 1f));
        Assert.True(p.Finished(7000 + r.EndMs));
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
