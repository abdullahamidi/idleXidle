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
/// THE REACTION CONTRACT (ADR-011, JAWS; the production direction of 2026-09-27, ONE SHADOW PIRANHA): a reaction is
/// presented on its own layer, belongs to its SKILL, never owns the champion's figure and moves nothing; it is ONE
/// object in visual silence (no second bite, no smear, no glint, no behind layer): a small Shadow jaw-head staged in
/// the empty space in front of the creature that bit, moving in open, closing on its outer front edge, holding shut so
/// the eye registers the bite, fading; ~200 ms; ONE cue on the closed frame; a reduced flash that never erases it.
/// </summary>
/// <remarks>
/// The failures these stop were measured across the JAWS work: a row-wide rope ring no one could tie to the bite, five
/// generic sounds on one frame, a post-bite "lay a trap" clip that never played, a dock tile that looked the same armed
/// and rearming, a spring-loaded bear trap with a chain the owner rejected as a mechanism, and then three near-equal
/// piranhas whose states changed a display frame apart and read as "purple activity".
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

    // ── ONE OBJECT IN VISUAL SILENCE ─────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_reaction_is_one_piranha_and_nothing_else()
    {
        // no swarm, no residue, no glint, no behind layer: the recipe has no such dials and the layer has no such passes
        var props = typeof(ReactionRecipe).GetProperties().Select(pr => pr.Name).ToArray();
        foreach (var banned in new[] { "Jaws", "Residue", "Glint", "Behind", "Smear", "Spark", "Yank", "Chain", "Retract", "Tether", "Whip", "Pivot" })
            Assert.DoesNotContain(props, n => n.Contains(banned, StringComparison.Ordinal));
        var methods = typeof(ReactionPerformance).GetMethods().Select(m => m.Name).ToArray();
        Assert.DoesNotContain("DrawBehind", methods);
        Assert.DoesNotContain("DrawLight", methods);
        Assert.Contains("DrawMaterial", methods);
        Assert.InRange(Jaws.JawBodyShare, 0.30f, 0.42f);          // ~35 % of the target: it bites PART of the body
        Assert.True(Jaws.JawMaxPx <= 64f);
        Assert.False(Jaws.Callout);
    }

    [Fact]
    public void test_the_open_head_is_staged_in_negative_space_and_moves_in_to_the_front_edge()
    {
        var r = Jaws;
        // it comes from the Seeker's side (the left), and bites just inside the creature's front edge, upper-front
        Assert.True(r.From.X > 0.9f);
        Assert.InRange(r.BiteShare.X, 0.02f, 0.12f);
        Assert.InRange(r.BiteShare.Y, 0.3f, 0.5f);
        // the spawn is a good share of its OWN width outside the bite point: the whole open head against the arena
        Assert.InRange(r.SpawnWidthShare, 0.6f, 0.9f);
        var height = Math.Clamp(r.JawBodyShare * 170f, r.JawMinPx, r.JawMaxPx);
        var headWidth = r.ArtHeadWidth * (height / r.ArtHeight);
        Assert.InRange(r.SpawnWidthShare * headWidth, 40f, 70f);    // far enough to establish the silhouette, never across the arena
        // held at the spawn on the first frame, still short of the target at 25, on the bite point exactly at the chomp
        Assert.Equal(0f, ReactionPerformance.Approach(r, 0f));
        Assert.True(ReactionPerformance.Approach(r, 25f) is > 0.1f and < 0.6f, "at ~25 ms: open and approaching");
        Assert.True(ReactionPerformance.Approach(r, 45f) is > 0.6f and < 0.98f, "at ~45 ms: half closed, near contact");
        Assert.Equal(1f, ReactionPerformance.Approach(r, r.ChompAtMs));
        var last = 0f;
        for (var u = 0f; u <= r.ChompAtMs; u += 4f)
        {
            var a = ReactionPerformance.Approach(r, u);
            Assert.True(a >= last, "it only ever moves TOWARD the target");
            last = a;
        }
    }

    [Fact]
    public void test_the_mouth_closes_over_three_display_states_and_holds_shut_on_the_creature()
    {
        var r = Jaws;
        Assert.InRange(r.ChompAtMs, 55f, 70f);                                  // the main close: slower than 48, still fast
        Assert.Equal(0, ReactionPerformance.Mouth(r, 0f));                      // OPEN, staged
        Assert.Equal(0, ReactionPerformance.Mouth(r, 25f));                     // OPEN, approaching
        Assert.Equal(1, ReactionPerformance.Mouth(r, 46f));                     // HALF, near contact
        Assert.Equal(2, ReactionPerformance.Mouth(r, r.ChompAtMs));            // SHUT: the chomp
        Assert.InRange(r.HoldMs, 55f, 70f);                                     // the semantic pose, held
        var holdEnd = r.ChompAtMs + r.HoldMs;
        Assert.Equal(1f, ReactionPerformance.Opacity(r, holdEnd - 1f));         // whole through the hold
        Assert.Equal(0f, ReactionPerformance.Recoil(r, holdEnd - 1f));          // and not moving off the creature
        Assert.True(ReactionPerformance.Recoil(r, holdEnd + r.RecoilMs) is >= 4f and <= 8f, "a small 4-8 px recoil on the exit");
        Assert.True(ReactionPerformance.Opacity(r, holdEnd + 20f) < 1f, "then a quick fade");
        Assert.Equal(0f, ReactionPerformance.Opacity(r, r.GoneMs));
        Assert.InRange(r.EndMs, 180f, 220f);                                    // ~200: still a short reaction, never the trap's 300+
        Assert.NotEqual(Vector2.One, ReactionPerformance.Squash(r, r.ChompAtMs));
        Assert.Equal(Vector2.One, ReactionPerformance.Squash(r, r.ChompAtMs + r.SquashMs));
    }

    [Fact]
    public void test_the_answer_lands_on_the_closed_frame_and_the_flash_never_erases_the_head()
    {
        var r = Jaws;
        Assert.Equal(r.ChompAtMs, r.AnswerAtMs);
        // the JAWS flash is its own override, never the generic F2 on the chomp's frame: reduced, or the full F2 a frame later
        Assert.True(r.TargetFlash <= 0.30f || r.TargetFlashDelayMs >= 16f, $"flash {r.TargetFlash} on the chomp's frame would erase the head");
        Assert.InRange(BitePresentation.UsualFlash.Peak, 0.40f, 0.50f);          // the global default is untouched
        var p = Answer();
        var stage = new Stage();
        p.Update(7000, stage);
        Assert.False(p.Clamped);
        p.Update(7000 + r.ChompAtMs - 10f, stage);
        Assert.False(p.Clamped, "not on the open or half frames");
        var step = p.Update(7000 + r.ChompAtMs, stage);
        Assert.True(step.Clamped && p.Clamped, "the answer (number, the cue) is presented on the closed frame");
        Assert.Equal(r.TargetFlashDelayMs <= 0f, step.Flashed);                 // the flash on the same frame, or its own later one
        if (r.TargetFlashDelayMs > 0f)
            Assert.True(p.Update(7000 + r.ChompAtMs + r.TargetFlashDelayMs, stage).Flashed);
        Assert.False(p.Finished(7000 + r.EndMs - 1f));
        Assert.True(p.Finished(7000 + r.EndMs));
    }

    [Fact]
    public void test_one_cue_for_the_bite_and_it_is_not_the_trap()
    {
        Assert.Equal("sfx_seeker_jaws_chomp", Jaws.SnapCues[0]);
        Assert.DoesNotContain("sfx_seeker_jaws_snap", Jaws.SnapCues);   // the dry-steel clack belonged to the rejected trap
        Assert.InRange(Jaws.SnapVolume, 0.25f, 0.5f);                   // quiet enough to repeat
        Assert.True(File.Exists(RepoFile("assets", "audio", "combat", "sfx_seeker_jaws_chomp.wav")));
        // one bite in the ear: the generator has no secondary ticks any more
        var gen = File.ReadAllText(RepoFile("tools", "asset-pipeline", "make_action_sfx.py"));
        var chomp = gen[gen.IndexOf("def jaws_chomp(", StringComparison.Ordinal)..];
        chomp = chomp[..chomp.IndexOf("\ndef ", StringComparison.Ordinal)];
        Assert.DoesNotContain("for at, g in", chomp);
    }

    [Fact]
    public void test_the_source_art_is_one_tiny_head_in_three_states()
    {
        foreach (var key in new[] { Jaws.OpenKey, Jaws.HalfKey, Jaws.ShutKey })
            Assert.True(File.Exists(RepoFile("assets", "art", "VFX", "parts", key + ".png")), $"{key}.png is not filed");
        Assert.DoesNotContain("trap", Jaws.OpenKey);
        Assert.True(Jaws.ArtHeight <= 64f && Jaws.ArtHeadWidth <= 64f);
    }

    [Fact]
    public void test_a_reaction_allocates_nothing_frame_to_frame()
    {
        var stage = new Stage();
        var p = Answer(0, 1);
        p.Update(7000, stage);   // the first frame pins the front edge (a probe may allocate once)
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
    public void test_the_answer_lands_on_the_chomp_and_the_bite_keeps_its_own_moment()
    {
        // PRESENTATION SCHEDULING ONLY. The fight resolved the reflected blow at the bite; on screen the answer's number,
        // the cue, a kill's fall and the creature's flash wait for the piranha's closed frame; the enemy's bite keeps t 0
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
        // released on the chomp (the flash on its own frame), dropped on a rewind; a deferred-dead creature is drawn standing until then
        var update = src[src.IndexOf("private void UpdateReactions()", StringComparison.Ordinal)..];
        update = update[..update.IndexOf("private long _reactionAllocBytes", StringComparison.Ordinal)];
        Assert.Contains("ReleaseEchoes(r, present: true, keepFlash: !step.Flashed);", update);
        Assert.Contains("if (step.Flashed && !step.Clamped) ReleaseEchoes(r, present: true);", update);
        Assert.Contains("Sound?.PlayFirst(r.Recipe.SnapCues", update);              // the cue's transient on the closed frame
        Assert.Contains("ReleaseEchoes(r, present: _playheadMs >= r.TriggerMs - 1f);", update);
        Assert.Contains("!_replay.CreatureAlive(i) && !_deathDeferred.Contains(i)", src);
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
