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
/// THE REACTION CONTRACT (ADR-011, JAWS; the production direction of 2026-09-27, SHADOW PIRANHA): a reaction is
/// presented on its own layer, belongs to its SKILL, never owns the champion's figure and moves nothing; it is a short
/// target-local phrase (three tiny Shadow jaws that appear on the creature that bit, dart in, CHOMP and are gone within
/// ~140 ms), staggered so it is a brief swarm bite and not three stamps, with ONE cue and a small F2-scale flash; its
/// answer lands on the main chomp; nothing travels from the Seeker to the creature and nothing stays in the world.
/// </summary>
/// <remarks>
/// The failures these stop were measured across the JAWS work: a row-wide rope ring that no one could tie to the bite,
/// five generic sounds on one frame, a post-bite "lay a trap" clip that never played in 70 triggers, a dock tile that
/// looked the same armed and rearming, and then a spring-loaded bear trap with a chain and a reel-in that the owner
/// rejected as a mechanism nobody should have to understand.
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

    // ── THE PHRASE, on the fight's playhead (v = ms after a jaw's own start; u = after the first frame) ─

    [Fact]
    public void test_one_hero_and_two_small_secondaries_staggered_chomp_tick_tick()
    {
        var jaws = Jaws.Jaws;
        Assert.Equal(3, jaws.Count);   // one hero, two secondaries; never ten particles
        var hero = jaws[0];
        Assert.Equal(0f, hero.DelayMs);
        Assert.Equal(1f, hero.Scale);
        Assert.False(hero.Behind, "the hero is always foreground");
        Assert.InRange(hero.BiteShare.X, 0.1f, 0.35f);          // an obvious visible edge: the front of the body...
        Assert.InRange(hero.BiteShare.Y, 0.25f, 0.5f);          // ...above its middle, never the torso's dark centre
        // the secondaries are clearly subordinate, and they FOLLOW the hero: chomp, tick, tick
        foreach (var j in jaws.Skip(1))
        {
            Assert.InRange(j.Scale, 0.55f, 0.78f);
            Assert.True(j.DelayMs + j.ChompAtMs > hero.ChompAtMs + 15f, "a secondary chomps after the hero's, never on its frame");
            Assert.True(j.GoneMs <= hero.GoneMs);
        }
        Assert.True(jaws[1].Scale > jaws[2].Scale);
        Assert.True(jaws[2].DelayMs > jaws[1].DelayMs);
        Assert.Equal(1, jaws.Count(j => j.Behind));            // exactly one drawn behind the creature: depth
        Assert.True(jaws.Select(j => j.BiteShare).Distinct().Count() == 3, "one upper/front, one lower/front, one behind/side");
        foreach (var j in jaws) Assert.InRange(j.From.Length(), 0.99f, 1.01f);
    }

    [Fact]
    public void test_the_hero_closes_over_three_display_states_and_holds_shut()
    {
        var r = Jaws;
        var hero = r.Jaws[0];
        Assert.Equal(0, ReactionPerformance.Mouth(r, hero, 0f));                   // OPEN on arrival
        Assert.Equal(0, ReactionPerformance.Mouth(r, hero, 20f));                  // still clearly OPEN at ~20 (a whole display frame)
        Assert.Equal(1, ReactionPerformance.Mouth(r, hero, 33f));                  // HALF at ~32
        Assert.Equal(2, ReactionPerformance.Mouth(r, hero, hero.ChompAtMs));      // SHUT at the chomp
        Assert.InRange(hero.ChompAtMs, 45f, 52f);                                  // ~48: the main chomp lands on a 60 fps frame boundary
        Assert.InRange(hero.HoldMs, 40f, 50f);                                     // the readability pause: shut for 40-50 ms
        Assert.Equal(1f, ReactionPerformance.Opacity(r, hero, hero.ChompAtMs + hero.HoldMs - 1f));   // whole through the hold
        Assert.Equal(0f, ReactionPerformance.Recoil(r, hero, hero.ChompAtMs + hero.HoldMs - 1f));    // and not moving off the body
        Assert.True(ReactionPerformance.Opacity(r, hero, hero.ChompAtMs + hero.HoldMs + 20f) < 1f, "dissolving after the hold");
        Assert.Equal(0f, ReactionPerformance.Opacity(r, hero, hero.GoneMs));
        Assert.True(ReactionPerformance.Recoil(r, hero, hero.GoneMs - 1f) is > 0f and < 12f);
        // the dart lands before the mouth begins to close
        Assert.Equal(1f, ReactionPerformance.Approach(r, r.DartMs), 4);
        Assert.True(r.DartMs < hero.ChompAtMs * r.HalfAtShare);
        Assert.NotEqual(Vector2.One, ReactionPerformance.Squash(r, hero, hero.ChompAtMs));
        Assert.Equal(Vector2.One, ReactionPerformance.Squash(r, hero, hero.ChompAtMs + r.SquashMs));
    }

    [Fact]
    public void test_the_whole_phrase_is_short_and_the_answer_lands_on_the_heros_closed_frame()
    {
        var r = Jaws;
        Assert.Equal(r.Jaws[0].ChompAtMs, r.AnswerAtMs);         // the presentation peak IS the hero's closed frame
        Assert.InRange(r.AnswerAtMs, 45f, 52f);
        Assert.InRange(r.EndMs, 160f, 200f);                     // ~180: modestly longer than the first piranha, never the 300 ms trap
        Assert.True(r.ResidueGoneMs <= r.EndMs);
        Assert.Equal(0f, ReactionPerformance.Residue(r, r.Jaws[0], r.AnswerAtMs - 1f));
        Assert.True(ReactionPerformance.Residue(r, r.Jaws[0], r.AnswerAtMs + 10f) > 0f);
        Assert.True(r.ResiduePeak <= 0.4f, "the residue is secondary to the mouth");
        var p = Answer();
        var stage = new Stage();
        Assert.False(p.Finished(7000 + r.EndMs - 1f));
        Assert.True(p.Finished(7000 + r.EndMs));
        p.Update(7000, stage);
        Assert.False(p.Clamped);
        p.Update(7000 + r.AnswerAtMs - 10f, stage);
        Assert.False(p.Clamped, "not on the open or half frames");
        p.Update(7000 + r.AnswerAtMs, stage);
        Assert.True(p.Clamped, "the answer (number, flash, glint, the cue) is presented on the hero's closed frame");
    }

    [Fact]
    public void test_the_jaws_are_small_target_local_and_spawn_a_short_way_off_the_body()
    {
        var r = Jaws;
        Assert.InRange(r.JawBodyShare, 0.25f, 0.40f);            // the hero bites PART of the body, never a mouth around it
        Assert.True(r.JawMaxPx <= 64f);
        var height = Math.Clamp(r.JawBodyShare * 170f, r.JawMinPx, r.JawMaxPx);
        var spawn = r.SpawnDistShare * height + r.SpawnDistPx;
        Assert.InRange(spawn, 10f, 30f);                          // ~10-25 px off the target surface, no world travel
        foreach (var j in r.Jaws)
        {
            Assert.InRange(j.BiteShare.X, 0f, 0.8f);
            Assert.InRange(j.BiteShare.Y, 0.2f, 0.85f);
        }
    }

    [Fact]
    public void test_the_reaction_moves_nothing_and_stays_secondary()
    {
        var r = Jaws;
        // the creature is not yanked, the champion does nothing, no chain, no reel: the recipe has no such dials
        foreach (var name in new[] { "Yank", "Chain", "Retract", "Tether", "Whip", "Pivot", "Hinge" })
            Assert.DoesNotContain(typeof(ReactionRecipe).GetProperties(), pr => pr.Name.Contains(name, StringComparison.Ordinal));
        Assert.InRange(r.TargetFlash, 0.40f, 0.60f);              // the F2 scale, never a white-out
        Assert.InRange(r.TargetFlashMs, 60f, 100f);
        Assert.InRange(r.LightUnderAction, 0.3f, 0.7f);           // secondary under SPRAY / HARD HANDS
        Assert.True(r.GlintPeak <= 0.7f && r.GlintMs <= 60f);
        Assert.False(r.Callout);
    }

    [Fact]
    public void test_one_cue_for_the_whole_phrase_and_it_is_not_the_trap()
    {
        Assert.Equal("sfx_seeker_jaws_chomp", Jaws.SnapCues[0]);
        Assert.DoesNotContain("sfx_seeker_jaws_snap", Jaws.SnapCues);   // the dry-steel clack belonged to the rejected trap
        Assert.InRange(Jaws.SnapVolume, 0.25f, 0.5f);                   // quiet enough to repeat
        Assert.True(File.Exists(RepoFile("assets", "audio", "combat", "sfx_seeker_jaws_chomp.wav")));
    }

    [Fact]
    public void test_the_source_art_is_one_tiny_head_in_three_states()
    {
        foreach (var key in new[] { Jaws.OpenKey, Jaws.HalfKey, Jaws.ShutKey })
            Assert.True(File.Exists(RepoFile("assets", "art", "VFX", "parts", key + ".png")), $"{key}.png is not filed");
        Assert.DoesNotContain("trap", Jaws.OpenKey);
        Assert.DoesNotContain("trap", Jaws.ShutKey);
        Assert.True(Jaws.ArtHeight <= 64f);
    }

    [Fact]
    public void test_a_reaction_allocates_nothing_frame_to_frame()
    {
        var stage = new Stage();
        var p = Answer(0, 1);
        p.Update(7000, stage);   // the first frame pins the bite shares (a probe may allocate once)
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var ms = 17; ms < 200; ms += 17) p.Update(7000 + ms, stage);
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
    public void test_the_answer_lands_on_the_main_chomp_and_the_bite_keeps_its_own_moment()
    {
        // PRESENTATION SCHEDULING ONLY. The fight resolved the reflected blow at the bite; on screen the answer's number,
        // the creature's flash and a kill's fall wait for the main chomp (~25 ms), and the enemy's bite keeps t 0
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
        // released on the chomp, dropped on a rewind; a deferred-dead creature is drawn standing until then
        var update = src[src.IndexOf("private void UpdateReactions()", StringComparison.Ordinal)..];
        update = update[..update.IndexOf("private long _reactionAllocBytes", StringComparison.Ordinal)];
        Assert.Contains("ReleaseEchoes(r, present: true);", update);
        Assert.Contains("Sound?.PlayFirst(r.Recipe.SnapCues", update);              // the cue's transient on the hero's closed frame
        var spawn = src[src.IndexOf("private ReactionPerformance? SpawnReaction(", StringComparison.Ordinal)..];
        spawn = spawn[..spawn.IndexOf("private void UpdateReactions()", StringComparison.Ordinal)];
        Assert.DoesNotContain("PlayFirst", spawn);                                  // never at the spawn
        Assert.Contains("ReleaseEchoes(r, present: _playheadMs >= r.TriggerMs - 1f);", update);
        Assert.Contains("!_replay.CreatureAlive(i) && !_deathDeferred.Contains(i)", src);
        // the enemy's own bite is untouched: its thud stays on its own frame, and no glyph is drawn on the champion
        var bite = src[src.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)..];
        bite = bite[..bite.IndexOf("break;", StringComparison.Ordinal)];
        Assert.DoesNotContain("Echo", bite);
        Assert.DoesNotContain("DrawBiteContact", src);
        // the reaction moves nothing: no yank on the creature's box, no recoil on the champion
        Assert.DoesNotContain("YankOffsetX", src);
        Assert.DoesNotContain("_recoilPx", src);
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
