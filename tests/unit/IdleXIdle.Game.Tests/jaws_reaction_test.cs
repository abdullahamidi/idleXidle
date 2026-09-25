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
/// THE REACTION CONTRACT (ADR-011, JAWS, 2026-09-25): a reaction is presented on its own layer, belongs to its SKILL,
/// never owns the champion's figure, is a RIGID mechanism (a spring-loaded clamp: each arm turns about its own hinge,
/// the metal never scales), is fired from and reeled back to the Seeker within about a quarter of a second, pulls its
/// creature toward him, and yields to a death.
/// </summary>
/// <remarks>
/// The failures these stop were measured in the JAWS discovery: a row-wide rope ring that no one could tie to the bite,
/// five generic sounds on one frame, a post-bite "lay a trap" clip that never played in 70 triggers (and would have
/// told the story backwards if it had), and a dock tile that looked the same armed and rearming.
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
        public bool Falling, ChampionDown, Focus;

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

        public bool TargetFalling(int slot) => Falling;
        public bool ChampionFalling => ChampionDown;
        public bool ActionInFocus => Focus;
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

    // ── THE MECHANISM, on the fight's playhead (u = ms after the first frame that showed the bite) ─

    [Fact]
    public void test_the_jaws_are_open_on_the_first_frame_and_shut_by_the_next()
    {
        // the open head is SEEN once (the tether fired), then the slam: the closed pose is on screen one 60 fps frame later
        Assert.Equal(Jaws.OpenDeg, ReactionPerformance.JawAngle(Jaws, 0f, Jaws.RetractAtMs));
        Assert.InRange(Jaws.CloseMs, 1f, 1000f / 60f);
        Assert.Equal(Jaws.StopDeg, ReactionPerformance.JawAngle(Jaws, Jaws.CloseMs, Jaws.RetractAtMs), 3);
        // ACCELERATING: most of the travel happens in the second half of the close (a spring's slam, not an ease)
        var half = ReactionPerformance.JawAngle(Jaws, Jaws.CloseMs / 2f, Jaws.RetractAtMs);
        Assert.True(Jaws.OpenDeg - half < (Jaws.OpenDeg - Jaws.StopDeg) / 2f, "the jaws close accelerating");
    }

    [Fact]
    public void test_the_jaws_stop_hard_recoil_by_a_few_degrees_and_lock_on_the_limb()
    {
        var held = (int)(Jaws.RetractAtMs - Jaws.CloseMs);   // from the stop to the moment it starts home
        var angles = Enumerable.Range(0, held).Select(ms => ReactionPerformance.JawAngle(Jaws, Jaws.CloseMs + ms, Jaws.RetractAtMs)).ToArray();
        Assert.All(angles, a => Assert.InRange(a, Jaws.StopDeg, Jaws.StopDeg + Jaws.ReboundDeg));   // a recoil, never a re-open
        Assert.Equal(Jaws.StopDeg, angles[^1], 3);                                                   // locked
        Assert.True(Jaws.StopDeg > 0f, "a trap stops ON what it bites: the jaws stand a little apart");
        // and they unlock only as the head is reeled home
        Assert.Equal(Jaws.UnlockDeg, ReactionPerformance.JawAngle(Jaws, Jaws.RetractAtMs + Jaws.UnlockMs, Jaws.RetractAtMs), 3);
    }

    [Fact]
    public void test_the_metal_is_never_scaled_only_rotated()
    {
        // the first build pumped the whole sprite from 0.82 to a 1.12 overshoot: the recipe has no scale to pump now,
        // and the draw passes one scale for every part of the clamp (the size, from the creature)
        var props = typeof(ReactionRecipe).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain(props, n => n.Contains("Overshoot", StringComparison.Ordinal) || n.Contains("RiseFrom", StringComparison.Ordinal));
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "ReactionPerformance.cs"));
        var draw = src[src.IndexOf("private void DrawPart(", StringComparison.Ordinal)..];
        draw = draw[..draw.IndexOf("_sprites++;", StringComparison.Ordinal)];
        Assert.Contains("pose.Scale", draw);
    }

    // ── IDENTITY: a mechanical hunting clamp, never an animal's head (the identity pass, 2026-09-25) ──

    [Fact]
    public void test_each_arm_turns_about_its_own_hinge_and_the_housing_never_turns()
    {
        // the polish pass hinged two long jaws on ONE round hub behind them: at true speed a snout, two jaws and an eye.
        // A trap's arms hinge at the two ends of its base: two pins, one above and one below the clamp's line
        Assert.NotEqual(Jaws.UpperPivot, Jaws.LowerPivot);
        Assert.True(Jaws.UpperPivot.Y < Jaws.BitePoint.Y && Jaws.LowerPivot.Y > Jaws.BitePoint.Y, "one hinge above the line, one below");
        Assert.Equal(Jaws.BitePoint.Y - Jaws.UpperPivot.Y, Jaws.LowerPivot.Y - Jaws.BitePoint.Y, 3);
        Assert.True(Jaws.UpperPivot.X < Jaws.BitePoint.X && Jaws.Eye.X < Jaws.UpperPivot.X, "shackle, hinges, then the arms' grip");
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "ReactionPerformance.cs"));
        var material = src[src.IndexOf("public void DrawMaterial(", StringComparison.Ordinal)..];
        material = material[..material.IndexOf("private void DrawPart(", StringComparison.Ordinal)];
        Assert.Contains("DrawPart(b, stage, Recipe.UpperKey, pose.UpperPivot, Recipe.UpperPivot, pose, pose.Angle - pose.Jaw", material);
        Assert.Contains("DrawPart(b, stage, Recipe.LowerKey, pose.LowerPivot, Recipe.LowerPivot, pose, pose.Angle + pose.Jaw", material);
        Assert.Contains("DrawPart(b, stage, Recipe.BaseKey, pose.Bite, Recipe.BitePoint, pose, pose.Angle,", material);   // rigid on the line
    }

    [Fact]
    public void test_the_recipe_describes_a_clamp_with_no_hub_and_no_head()
    {
        // no single shared pivot (the hub read as an eye) and no "head" to size: the object is a clamp
        var props = typeof(ReactionRecipe).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("Pivot", props);
        Assert.DoesNotContain(props, n => n.StartsWith("Head", StringComparison.Ordinal));
        Assert.Contains("ClampArtLength", props);
    }

    [Fact]
    public void test_the_sparks_are_few_and_come_from_the_hinges()
    {
        // the mechanism's motion carries the snap: at most a spark per hinge, where steel met steel, never a fan round the target
        Assert.InRange(Jaws.Sparks, 0, 2);
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "ReactionPerformance.cs"));
        var light = src[src.IndexOf("public void DrawLight(", StringComparison.Ordinal)..];
        light = light[..light.IndexOf("private void DrawChain(", StringComparison.Ordinal)];
        Assert.Contains("var hinge = s == 0 ? pose.UpperPivot : pose.LowerPivot;", light);
        Assert.Contains("Math.Min(Recipe.Sparks, 2)", light);
    }

    [Fact]
    public void test_the_clamp_parts_share_one_canvas_and_its_points_lie_on_it()
    {
        // the housing, both arms and both teeth masks are drawn on ONE canvas, so the pins, the shackle and the clamp
        // point serve them all. PNG IHDR: width and height are the big-endian ints at bytes 16..24 (no device needed)
        var sizes = new[] { Jaws.BaseKey, Jaws.UpperKey, Jaws.LowerKey, Jaws.UpperEdgeKey, Jaws.LowerEdgeKey }.Select(key =>
        {
            var head = new byte[24];
            using (var f = File.OpenRead(RepoFile("assets", "art", "Props", key + ".png"))) f.ReadExactly(head);
            return (W: (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19], H: (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23]);
        }).Distinct().ToArray();
        Assert.Single(sizes);
        foreach (var at in new[] { Jaws.UpperPivot, Jaws.LowerPivot, Jaws.Eye, Jaws.BitePoint })
        {
            Assert.InRange(at.X, 0f, sizes[0].W);
            Assert.InRange(at.Y, 0f, sizes[0].H);
        }
        Assert.InRange(Jaws.ClampArtLength, Jaws.BitePoint.X - Jaws.Eye.X, sizes[0].W);
    }

    [Fact]
    public void test_the_chain_yanks_the_creature_toward_the_seeker_a_few_pixels()
    {
        Assert.Equal(0f, ReactionPerformance.Yank(Jaws, Jaws.CloseMs));
        Assert.Equal(1f, ReactionPerformance.Yank(Jaws, Jaws.CloseMs + Jaws.YankPeakMs), 3);
        Assert.Equal(0f, ReactionPerformance.Yank(Jaws, Jaws.CloseMs + Jaws.YankMs));
        var p = Answer();
        p.Update(7000, new Stage());
        var offsets = Enumerable.Range(0, 300).Select(ms => p.YankOffsetX(0, 7000 + ms, 400f, falling: false)).ToArray();
        Assert.All(offsets, x => Assert.True(x <= 0f, "the chain pulls the creature TOWARD the champion (to its left)"));
        Assert.InRange(-offsets.Min(), Jaws.YankMinPx, Jaws.YankMaxPx);   // even a 400 px creature: a few pixels
        Assert.Equal(0f, p.YankOffsetX(5, 7000 + 40, 160f, falling: false));   // only the creature on the chain moves
    }

    [Fact]
    public void test_the_clamp_is_reeled_home_and_nothing_stays()
    {
        var p = Answer();
        p.Update(7000, new Stage());
        Assert.InRange(p.EndMs, 220f, 320f);
        Assert.Equal(0f, ReactionPerformance.Retract(Jaws, Jaws.RetractAtMs, Jaws.RetractAtMs));
        Assert.Equal(1f, ReactionPerformance.Retract(Jaws, Jaws.RetractAtMs + Jaws.RetractMs, Jaws.RetractAtMs), 3);
        // ACCELERATING home (a reel takes up the slack, then snaps it in)
        Assert.True(ReactionPerformance.Retract(Jaws, Jaws.RetractAtMs + Jaws.RetractMs / 2f, Jaws.RetractAtMs) < 0.5f);
        // it is whole while it travels and fades only over its last stretch into the belt
        Assert.Equal(1f, ReactionPerformance.Opacity(Jaws, Jaws.RetractAtMs + Jaws.RetractMs * 0.5f, Jaws.RetractAtMs, null));
        Assert.Equal(0f, ReactionPerformance.Opacity(Jaws, Jaws.RetractAtMs + Jaws.RetractMs, Jaws.RetractAtMs, null), 3);
        Assert.False(p.Finished(7000 + p.EndMs - 1f));
        Assert.True(p.Finished(7000 + p.EndMs));
    }

    [Fact]
    public void test_the_chain_goes_from_the_whip_to_taut()
    {
        Assert.Equal(0f, ReactionPerformance.Tension(Jaws, 0f));
        Assert.Equal(1f, ReactionPerformance.Tension(Jaws, Jaws.TautMs), 3);
        Assert.True(ReactionPerformance.Tension(Jaws, Jaws.TautMs * 0.3f) is > 0f and < 1f);
    }

    [Fact]
    public void test_a_caught_creature_that_falls_is_let_go_and_never_dragged()
    {
        var stage = new Stage { Falling = true };
        var p = Answer();
        p.Update(7000 + 10, stage);
        Assert.Equal(Jaws.CloseMs + Jaws.DeathHoldMs, p.RetractFromMs);   // the snap still reads, then it lets go
        Assert.Equal(0f, p.YankOffsetX(0, 7000 + 40, 160f, falling: true));
        Assert.Equal(0f, p.YankOffsetX(0, 7000 + 40, 160f, falling: false));   // let go: no pull on a corpse
        Assert.False(p.Slack);                                                 // and the clamp still goes home
        Assert.True(p.EndMs < Jaws.RetractAtMs + Jaws.RetractMs);
    }

    [Fact]
    public void test_a_bite_that_fells_the_champion_is_still_answered_then_goes_slack()
    {
        var stage = new Stage { ChampionDown = true };
        var p = Answer();
        p.Update(7000, stage);
        Assert.True(p.Slack);                                          // no heroic retract through his fall
        Assert.False(p.Finished(7000 + Jaws.CloseMs));                 // the answer is not cancelled by the fall
        Assert.InRange(p.EndMs, Jaws.CloseMs, Jaws.CloseMs + 30f + Jaws.SlackFadeMs);
    }

    [Fact]
    public void test_the_chain_is_a_dark_body_with_a_few_link_accents()
    {
        // artistic economy: a small reaction needs 8-16 readable links, not sixty-four equal stamps
        Assert.InRange(Jaws.LinkAt.Count, 8, 16);
        Assert.All(Jaws.LinkAt, f => Assert.InRange(f, 0f, 1f));
        // denser at both ends (the hardware at the belt and at the shackle) than across the middle
        var gaps = Jaws.LinkAt.Zip(Jaws.LinkAt.Skip(1), (a, b) => b - a).ToArray();
        Assert.True(gaps[0] < gaps[gaps.Length / 2] && gaps[^1] < gaps[gaps.Length / 2]);
        Assert.True(Jaws.BodySegments >= 8, "the dark body carries the chain between the accents: no gaps");
    }

    [Fact]
    public void test_a_reaction_allocates_nothing_frame_to_frame()
    {
        var stage = new Stage();
        var p = Answer(0, 1);
        p.Update(7000, stage);   // the first frame pins the clamp points (a probe may allocate once)
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var ms = 17; ms < 330; ms += 17) p.Update(7000 + ms, stage);
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
