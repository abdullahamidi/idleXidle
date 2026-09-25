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
/// never owns the champion's figure, lives about a third of a second from the enemy's contact, and yields to a death.
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

    // ── THE LIFECYCLE, on the fight's playhead ──────────────────────────────────────────────────

    [Fact]
    public void test_the_snap_is_the_first_forty_to_seventy_ms_and_the_jaws_are_gone_by_a_third_of_a_second()
    {
        Assert.InRange(Jaws.SnapMs, 40f, 70f);
        Assert.False(ReactionPerformance.Shut(Jaws, 0f, Jaws.ReleaseAtMs));        // open, rising, on the contact frame
        Assert.True(ReactionPerformance.Shut(Jaws, Jaws.SnapMs, Jaws.ReleaseAtMs)); // shut by the end of the snap
        var p = Answer();
        Assert.InRange(p.EndMs, 250f, 350f);
        Assert.False(p.Finished(7000 + p.EndMs - 1f));
        Assert.True(p.Finished(7000 + p.EndMs));
        Assert.Equal(1f, ReactionPerformance.Opacity(Jaws, Jaws.ReleaseAtMs - 1f, Jaws.ReleaseAtMs));
        Assert.Equal(0f, ReactionPerformance.Opacity(Jaws, Jaws.ReleaseAtMs + Jaws.ReleaseMs, Jaws.ReleaseAtMs), 3);
    }

    [Fact]
    public void test_the_chain_starts_slack_goes_taut_and_slackens_as_it_lets_go()
    {
        Assert.Equal(0f, ReactionPerformance.Tension(Jaws, 0f, Jaws.ReleaseAtMs));
        Assert.Equal(1f, ReactionPerformance.Tension(Jaws, Jaws.TautMs, Jaws.ReleaseAtMs), 3);
        Assert.True(ReactionPerformance.Tension(Jaws, Jaws.TautMs * 0.3f, Jaws.ReleaseAtMs) is > 0f and < 1f);
        Assert.True(ReactionPerformance.Tension(Jaws, Jaws.ReleaseAtMs + Jaws.ReleaseMs * 0.6f, Jaws.ReleaseAtMs) < 0.05f);
    }

    [Fact]
    public void test_the_recoil_starts_at_the_snap_peaks_and_returns_and_stays_a_few_pixels()
    {
        Assert.Equal(0f, ReactionPerformance.Recoil(Jaws, Jaws.SnapMs));
        Assert.Equal(1f, ReactionPerformance.Recoil(Jaws, Jaws.SnapMs + Jaws.RecoilMs * Jaws.RecoilPeakAt), 3);
        Assert.Equal(0f, ReactionPerformance.Recoil(Jaws, Jaws.SnapMs + Jaws.RecoilMs));
        var p = Answer();
        var peak = Enumerable.Range(0, 400).Max(ms => p.RecoilOffsetX(0, 7000 + ms, 400f, falling: false));
        Assert.InRange(peak, Jaws.RecoilMinPx, Jaws.RecoilMaxPx);   // even a 400 px creature: a few pixels
        Assert.Equal(0f, p.RecoilOffsetX(5, 7000 + 90, 160f, falling: false));   // only the caught creature moves
    }

    [Fact]
    public void test_a_caught_creature_that_falls_is_let_go_and_never_dragged()
    {
        var stage = new Stage { Falling = true };
        var p = Answer();
        p.Update(7000 + 10, stage);
        Assert.Equal(Jaws.SnapMs + Jaws.DeathReleaseAfterSnapMs, p.ReleaseFromMs);   // the snap still plays
        Assert.Equal(0f, p.RecoilOffsetX(0, 7000 + 90, 160f, falling: true));
        Assert.Equal(0f, p.RecoilOffsetX(0, 7000 + 90, 160f, falling: false));        // released: no push
        Assert.True(p.EndMs < Jaws.ReleaseAtMs + Jaws.ReleaseMs);
    }

    [Fact]
    public void test_a_bite_that_fells_the_champion_is_still_answered_then_let_go()
    {
        var stage = new Stage { ChampionDown = true };
        var p = Answer();
        p.Update(7000, stage);
        Assert.Equal(Jaws.SnapMs + Jaws.DeathReleaseAfterSnapMs, p.ReleaseFromMs);
        Assert.False(p.Finished(7000 + Jaws.SnapMs));   // the answer is not cancelled by the fall
    }

    [Fact]
    public void test_the_chain_is_bounded_and_never_gapped()
    {
        foreach (var length in new[] { 0f, 40f, 300f, 700f, 1200f, 5000f })
        {
            var (count, link) = ReactionPerformance.LinksFor(Jaws, length);
            Assert.InRange(count, 1, Jaws.MaxLinks);
            Assert.True(count * link * (1f - Jaws.LinkOverlap) >= length - 0.01f, $"a {length} px chain has a gap");
        }
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
