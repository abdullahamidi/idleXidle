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
/// never owns the champion's figure, is a RIGID mechanism (a spring-loaded bear trap: each jaw turns about its own pin,
/// the metal never scales) whose close is SEEN (open, moving, shut over ~50 ms), whose answer lands on its stop, is fired
/// from and reeled back to the Seeker within about a third of a second, pulls its creature toward him, and yields to a
/// death.
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
    public void test_the_jaws_close_over_several_frames_the_eye_can_see()
    {
        // THE CLOSE IS SEEN. The polish pass shut in 16 ms: one open frame, one shut frame, no motion anyone perceived.
        // At 60 fps the eye now gets OPEN (+0), still open (+17), about half shut (+33), SHUT (+50)
        const float frame = 1000f / 60f;
        Assert.Equal(Jaws.OpenDeg, ReactionPerformance.JawAngle(Jaws, 0f, Jaws.RetractAtMs));
        Assert.InRange(Jaws.CloseMs, 2.9f * frame, 4.2f * frame);
        Assert.Equal(Jaws.StopDeg, ReactionPerformance.JawAngle(Jaws, Jaws.CloseMs, Jaws.RetractAtMs), 3);
        var travel = Jaws.OpenDeg - Jaws.StopDeg;
        float Travelled(float ms) => (Jaws.OpenDeg - ReactionPerformance.JawAngle(Jaws, ms, Jaws.RetractAtMs)) / travel;
        Assert.InRange(Travelled(frame), 0.02f, 0.2f);        // the first frame after the bite: still clearly open, starting
        Assert.InRange(Travelled(2f * frame), 0.3f, 0.6f);    // the next: visibly on its way
        // A SPRING, NOT AN EASE: slow first degrees, most of the travel near contact (the last third of the close)
        Assert.True(1f - Travelled(Jaws.CloseMs * 2f / 3f) > 0.5f, "the majority of the travel happens near contact");
        // and the open pose is unmistakably not the shut one: the jaws each travel a good 30 degrees
        Assert.True(travel >= 28f, "open must look unmistakably different from shut");
    }

    [Fact]
    public void test_the_jaws_stop_hard_rebound_once_and_lock_on_the_limb()
    {
        var held = (int)(Jaws.RetractAtMs - Jaws.CloseMs);   // from the stop to the moment it starts home
        var angles = Enumerable.Range(0, held).Select(ms => ReactionPerformance.JawAngle(Jaws, Jaws.CloseMs + ms, Jaws.RetractAtMs)).ToArray();
        Assert.All(angles, a => Assert.InRange(a, Jaws.StopDeg, Jaws.StopDeg + Jaws.ReboundDeg + 0.001f));   // never re-opens
        // ONE small rebound of 3-5 degrees: up, then down, then locked (metal hitting resistance, not a spring bouncing)
        Assert.InRange(Jaws.ReboundDeg, 3f, 5f);
        Assert.InRange(angles.Max() - Jaws.StopDeg, Jaws.ReboundDeg * 0.9f, Jaws.ReboundDeg + 0.001f);
        var peak = Array.IndexOf(angles, angles.Max());
        for (var i = 1; i <= peak; i++) Assert.True(angles[i] >= angles[i - 1] - 1e-4f, "the rebound rises once");
        for (var i = peak + 1; i < angles.Length; i++) Assert.True(angles[i] <= angles[i - 1] + 1e-4f, "then settles, never bounces again");
        Assert.InRange(Jaws.ReboundMs, 20f, 30f);
        Assert.Equal(Jaws.StopDeg, angles[(int)Jaws.ReboundMs + 1], 3);   // locked
        Assert.True(Jaws.StopDeg > 0f, "a trap stops ON what it bites: the jaws stand a little apart");
        // and they unlock only as the trap is reeled home
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

    // ── THE OBJECT: a bear trap whose jaws dominate, never a head or a hook (identity + readable-clamp passes) ──

    [Fact]
    public void test_each_jaw_turns_about_its_own_pin_and_the_base_never_turns()
    {
        // the polish pass hinged two long jaws on ONE round hub (a snout and an eye); the identity pass put two thin arms
        // on pins 57 px apart at a plate's corners (a bracket with two hooks). A bear trap's two jaws turn on a COMPACT
        // pair of pins on its base, below the limb they close around
        Assert.NotEqual(Jaws.NearPivot, Jaws.FarPivot);
        Assert.True(Jaws.NearPivot.X < Jaws.BitePoint.X && Jaws.FarPivot.X > Jaws.BitePoint.X, "the near jaw's pin on the Seeker's side, the far one past it");
        Assert.True(Vector2.Distance(Jaws.NearPivot, Jaws.FarPivot) < 0.25f * Jaws.ClampArtHeight, "a compact pair, never a tall bracket");
        Assert.True(Jaws.NearPivot.Y > Jaws.BitePoint.Y && Jaws.FarPivot.Y > Jaws.BitePoint.Y, "the pins on the base, below the jaws' grip");
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "ReactionPerformance.cs"));
        var material = src[src.IndexOf("public void DrawMaterial(", StringComparison.Ordinal)..];
        material = material[..material.IndexOf("private void DrawPart(", StringComparison.Ordinal)];
        Assert.Contains("DrawPart(b, stage, Recipe.NearKey, pose.NearPivot, Recipe.NearPivot, pose, pose.Angle - pose.Jaw", material);
        Assert.Contains("DrawPart(b, stage, Recipe.FarKey, pose.FarPivot, Recipe.FarPivot, pose, pose.Angle + pose.Jaw", material);
        Assert.Contains("DrawPart(b, stage, Recipe.BaseKey, pose.Bite, Recipe.BitePoint, pose, pose.Angle,", material);   // rigid
    }

    [Fact]
    public void test_the_recipe_describes_a_trap_with_no_hub_no_head_and_no_arms()
    {
        // no single shared pivot (the hub read as an eye), no "head" (a crocodile), no upper/lower "arms" (two hooks)
        var props = typeof(ReactionRecipe).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("Pivot", props);
        Assert.DoesNotContain(props, n => n.StartsWith("Head", StringComparison.Ordinal) || n.StartsWith("Upper", StringComparison.Ordinal)
                                          || n.StartsWith("Lower", StringComparison.Ordinal));
        Assert.Contains("ClampArtHeight", props);
    }

    [Fact]
    public void test_one_jaw_is_drawn_behind_the_caught_creature()
    {
        // THE LIMB IS BETWEEN THE JAWS: the screen draws each trap's NEAR jaw just before the creature it caught, in the
        // same batch, and the material pass then skips it; its glint is never drawn through the limb that hides it. The
        // near jaw, not the far one: the far jaw lies over the body, and behind a dark creature it vanished (the close read
        // as one jaw swinging up)
        var perf = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "ReactionPerformance.cs"));
        var behind = perf[perf.IndexOf("public void DrawBehind(", StringComparison.Ordinal)..];
        behind = behind[..behind.IndexOf("public void DrawMaterial(", StringComparison.Ordinal)];
        Assert.Contains("Recipe.NearKey", behind);
        Assert.DoesNotContain("Recipe.FarKey", behind);
        Assert.Contains("if (_nearBehind[k]) _sprites++;", perf);
        Assert.Contains("if (!_nearBehind[k]) DrawPart(b, stage, Recipe.NearEdgeKey", perf);
        var src = Hunt();
        foreach (var drawer in new[] { "private void DrawComposition(", "private void DrawNormalEnemy(", "private void DrawBoss(" })
        {
            var body = src[src.IndexOf(drawer, StringComparison.Ordinal)..];
            var call = body.IndexOf("r.DrawBehind(b, this, _playheadMs,", StringComparison.Ordinal);
            var sprite = body.IndexOf("ActorSprite(b,", StringComparison.Ordinal);
            Assert.True(call > 0 && call < sprite, $"{drawer} draws the far jaw BEFORE the creature's sprite");
        }
    }

    [Fact]
    public void test_the_sparks_are_few_and_come_from_the_pins()
    {
        // the jaws' motion carries the snap: at most a spark per pin, where steel met steel, never a fan round the target
        Assert.InRange(Jaws.Sparks, 0, 2);
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "ReactionPerformance.cs"));
        var light = src[src.IndexOf("public void DrawLight(", StringComparison.Ordinal)..];
        light = light[..light.IndexOf("private void DrawChain(", StringComparison.Ordinal)];
        Assert.Contains("var hinge = s == 0 ? pose.NearPivot : pose.FarPivot;", light);
        Assert.Contains("Math.Min(Recipe.Sparks, 2)", light);
    }

    [Fact]
    public void test_the_trap_parts_share_one_canvas_and_its_points_lie_on_it()
    {
        // the base, both jaws and both teeth masks are drawn on ONE canvas, so the pins, the eye and the clamp point serve
        // them all. PNG IHDR: width and height are the big-endian ints at bytes 16..24 (no device needed)
        var sizes = new[] { Jaws.BaseKey, Jaws.NearKey, Jaws.FarKey, Jaws.NearEdgeKey, Jaws.FarEdgeKey }.Select(key =>
        {
            var head = new byte[24];
            using (var f = File.OpenRead(RepoFile("assets", "art", "Props", key + ".png"))) f.ReadExactly(head);
            return (W: (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19], H: (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23]);
        }).Distinct().ToArray();
        Assert.Single(sizes);
        foreach (var at in new[] { Jaws.NearPivot, Jaws.FarPivot, Jaws.Eye, Jaws.BitePoint })
        {
            Assert.InRange(at.X, 0f, sizes[0].W);
            Assert.InRange(at.Y, 0f, sizes[0].H);
        }
        Assert.InRange(Jaws.ClampArtHeight, 0.5f * sizes[0].H, sizes[0].H);
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
    public void test_the_trap_is_reeled_home_and_nothing_stays()
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
    public void test_the_answer_lands_on_the_jaws_stop_and_the_bite_keeps_its_own_moment()
    {
        // PRESENTATION SCHEDULING ONLY. The fight resolved the reflected blow at the bite; on screen the answer's number,
        // the creature's flash and a kill's fall wait for the jaws' stop (~50 ms), and the enemy's bite keeps t 0
        var src = Hunt();
        var strike = src[src.IndexOf("case BattleEventKind.Strike:", StringComparison.Ordinal)..];
        strike = strike[..strike.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)];
        Assert.Contains("if (reactionHit && reactionHitPerf is { Clamped: false } answering)", strike);
        Assert.Contains("ReactionEchoKind.Number", strike);
        Assert.Contains("ReactionEchoKind.Flash", strike);
        var down = src[src.IndexOf("case BattleEventKind.EnemyDown:", StringComparison.Ordinal)..];
        down = down[..down.IndexOf("case BattleEventKind.Charge:", StringComparison.Ordinal)];
        Assert.Contains("ReactionEchoKind.Death", down);
        Assert.Contains("_deathDeferred.Add(e.Slot)", down);
        // released on the stop, dropped on a rewind; a deferred-dead creature is drawn standing until then
        var update = src[src.IndexOf("private void UpdateReactions()", StringComparison.Ordinal)..];
        update = update[..update.IndexOf("private long _reactionAllocBytes", StringComparison.Ordinal)];
        Assert.Contains("if (step.Clamped) ReleaseEchoes(r, present: true);", update);
        Assert.Contains("ReleaseEchoes(r, present: _playheadMs >= r.TriggerMs - 1f);", update);
        Assert.Contains("!_replay.CreatureAlive(i) && !_deathDeferred.Contains(i)", src);
        // the enemy's own bite is untouched: its thud and burst stay on its own frame
        var bite = src[src.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)..];
        bite = bite[..bite.IndexOf("break;", StringComparison.Ordinal)];
        Assert.DoesNotContain("Echo", bite);
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
