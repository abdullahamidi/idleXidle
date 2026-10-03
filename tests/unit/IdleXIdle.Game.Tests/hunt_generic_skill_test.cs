using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Sources;
using IdleXIdle.Game.Presentation;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE GENERIC SKILL PATH (the remaining-skill sweep, Phase 0 / P0.4; design.md sections 1, 3, 5.14, 5.16, 5.32 and 8):
/// an Active calls out its own name and the rest say nothing; a damaging skill that struck nothing (BACKDRAW's phantom on
/// the wave's last kill) draws nothing; a second Skill on one millisecond is a WEAVER echo; no reaction takes the figure
/// with the old `trap` clip. The decisions live in <see cref="GenericHits"/>; the text pins hold the hunt's Skill case
/// to them.
/// </summary>
public class HuntGenericSkillTest
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static string Hunt() => File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));

    /// <summary>The hunt's Skill case, up to the heal.</summary>
    private static string SkillCase()
    {
        var src = Hunt();
        var skill = src[src.IndexOf("case BattleEventKind.Skill:", StringComparison.Ordinal)..];
        return skill[..skill.IndexOf("case BattleEventKind.Heal:", StringComparison.Ordinal)];
    }

    private static BattleEvent Skill(int slot, int ms) => new(BattleEventKind.Skill, slot, 0, ms);
    private static BattleEvent Strike(int slot, int ms) => new(BattleEventKind.Strike, slot, 10, ms, HitSource.Primary);
    private static BattleEvent Down(int slot, int ms) => new(BattleEventKind.EnemyDown, slot, 0, ms);

    [Theory]
    [InlineData("hammer_blow", "BLOW")]
    [InlineData("snare_repay", "REPAY")]
    [InlineData("sign_call", "CALL")]
    [InlineData("field_pulse", "PULSE")]
    [InlineData("drain_drink", "DRINK")]
    [InlineData("sig_anvil_hardface", "HARDFACE")]
    [InlineData("sig_metronome_clockwork", "CLOCKWORK")]
    [InlineData("sig_magpie_paying_work", "PAYING WORK")]
    [InlineData("volley_spray", "SPRAY")]
    [InlineData("sig_seeker_hard_hands", "HARD HANDS")]
    public void test_an_active_calls_out_its_own_name(string id, string name)
    {
        Assert.Equal(name, GenericHits.CalloutText(SkillCatalogue.ById(id)));
    }

    [Fact]
    public void test_a_field_or_reaction_calls_out_nothing()
    {
        var quiet = SkillCatalogue.All.Where(d => d.Kind != SkillKind.Active).ToList();
        Assert.Contains(quiet, d => d.Kind == SkillKind.Field);
        Assert.Contains(quiet, d => d.Kind == SkillKind.Reaction);
        foreach (var def in quiet) Assert.Null(GenericHits.CalloutText(def));
        // ...and every Active says its own name, never its style's word
        var styleWords = new[] { "HAMMER", "VOLLEY", "FIELD", "SNARE", "SIGN", "DRAIN" };
        foreach (var def in SkillCatalogue.All.Where(d => d.Kind == SkillKind.Active))
        {
            Assert.Equal(def.Name, GenericHits.CalloutText(def));
            Assert.DoesNotContain(def.Name, styleWords);
        }
        // the style words left the arena: the hunt no longer spells them as callouts
        var src = Hunt();
        foreach (var word in styleWords) Assert.DoesNotContain($"(\"{word}\"", src);
        Assert.Contains("private static (string Text, Color Color)? CalloutFor(SkillDef def)", src);
        Assert.DoesNotContain("CalloutFor(castDef.Style)", src);
        Assert.DoesNotContain(".Def.Style);", src[src.IndexOf("private void Voice(", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void test_a_damaging_skill_with_no_strike_is_skipped()
    {
        // BACKDRAW's phantom: the wave's last kill, then the reaction's Skill with no survivor to strike
        var backdraw = SkillCatalogue.ById("sig_quiver_backdraw");
        Assert.True(GenericHits.DealsDamage(backdraw));
        var batch = new List<BattleEvent> { Strike(2, 4000), Down(2, 4000), Skill(1, 4000) };
        Assert.False(GenericHits.IsStruck(batch, 2));
        Assert.True(GenericHits.IsPhantom(backdraw, struck: false));
        // a later owner's blows at the same ms are not this skill's
        var owned = new List<BattleEvent> { Skill(1, 4000), Skill(0, 4000), Strike(0, 4000) };
        Assert.False(GenericHits.IsStruck(owned, 0));
        Assert.True(GenericHits.IsStruck(owned, 1));
        // a blow at another ms is not this one's either
        Assert.False(GenericHits.IsStruck(new List<BattleEvent> { Skill(1, 4000), Strike(0, 4100) }, 0));
        // a skill that struck draws as before
        var struck = new List<BattleEvent> { Down(2, 4000), Skill(1, 4000), Strike(0, 4000), Strike(1, 4000) };
        Assert.True(GenericHits.IsStruck(struck, 1));
        Assert.False(GenericHits.IsPhantom(backdraw, struck: true));
    }

    [Fact]
    public void test_a_zero_power_skill_with_no_strike_is_not_skipped()
    {
        // REPAY (a zero base line: its bank) and the Marks (amplifiers) strike nothing by design; their zero cast is
        // Phase 5's fizzle, not a phantom
        foreach (var id in new[] { "snare_repay", "sign_call", "sig_oathbound_oathmark" })
        {
            var def = SkillCatalogue.ById(id);
            Assert.False(GenericHits.DealsDamage(def), id);
            Assert.False(GenericHits.IsPhantom(def, struck: false), id);
        }
    }

    [Fact]
    public void test_a_second_skill_on_the_same_ms_is_an_echo()
    {
        // the fight's WEAVER order: the cast, its blows, then the woven Skill of the next slot and its blows
        var batch = new List<BattleEvent> { Skill(0, 2000), Strike(0, 2000), Skill(1, 2000), Strike(0, 2000) };
        Assert.False(GenericHits.IsEcho(batch, 0));
        Assert.True(GenericHits.IsEcho(batch, 2));
        Assert.True(GenericHits.IsStruck(batch, 0));       // the first cast's blow is its own...
        Assert.True(GenericHits.IsStruck(batch, 2));       // ...and the echo's its own
        // the ECHO keystone's second cast is the SAME slot: not a woven echo
        Assert.False(GenericHits.IsEcho(new List<BattleEvent> { Skill(0, 2000), Strike(0, 2000), Skill(0, 2000) }, 2));
        // a reaction answering a bite on a cast's ms neither is an echo nor makes one
        var reactions = new HashSet<int> { 3 };
        Assert.False(GenericHits.IsEcho(new List<BattleEvent> { Skill(0, 2000), Skill(3, 2000) }, 1, reactions));
        Assert.False(GenericHits.IsEcho(new List<BattleEvent> { Skill(3, 2000), Skill(0, 2000) }, 1, reactions));
        // another ms is another cast
        Assert.False(GenericHits.IsEcho(new List<BattleEvent> { Skill(0, 1900), Skill(1, 2000) }, 1));

        // THE CLIP PICKER NEVER TAKES THE ECHO: it reads the FIRST Skill after the playhead, which is the original...
        var events = new List<BattleEvent> { Skill(0, 2000), Strike(0, 2000), Strike(1, 2000), Skill(1, 2000), Strike(2, 2000) };
        var replay = new WaveReplay(events, new Dictionary<int, int> { [0] = 100 }, new Dictionary<int, int> { [0] = 100 }, 1000f);
        var next = replay.NextSkillEventAfter(1500f, new HashSet<int>());
        Assert.Equal(0, next!.Value.Slot);
        Assert.Null(replay.NextSkillEventAfter(2000f, new HashSet<int>()));
        // ...and the original's targets stop at the echo's Skill
        Assert.Equal(new[] { 0, 1 }, ActionTargets.StruckBy(events, events[0]));
    }

    [Fact]
    public void test_the_echo_and_struck_reads_allocate_nothing()
    {
        var batch = new List<BattleEvent> { Down(2, 4000), Skill(1, 4000), Skill(0, 2000), Strike(0, 2000), Skill(1, 2000), Strike(0, 2000) };
        var reactions = new HashSet<int> { 3 };
        IReadOnlyList<BattleEvent> view = batch;
        var hits = 0;
        for (var i = 0; i < batch.Count; i++) hits += (GenericHits.IsStruck(view, i) ? 1 : 0) + (GenericHits.IsEcho(view, i, reactions) ? 1 : 0);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var run = 0; run < 1000; run++)
            for (var i = 0; i < batch.Count; i++)
                hits += (GenericHits.IsStruck(view, i) ? 1 : 0) + (GenericHits.IsEcho(view, i, reactions) ? 1 : 0);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(hits > 0);
    }

    [Fact]
    public void test_no_reaction_loads_or_commits_the_trap_clip()
    {
        var reactions = SkillCatalogue.All.Where(d => d.Kind == SkillKind.Reaction).ToList();
        Assert.NotEmpty(reactions);
        Assert.Equal(10, CharacterRoster.All.Count);
        var into = new List<string>();
        foreach (var who in CharacterRoster.All)
        {
            var trapKeys = who.StripKeys("trap").ToHashSet();
            foreach (var def in reactions)
            {
                ActorClips.ChampionStrips(who, new[] { new EquippedSkill(def, Source.Body) }, _ => true, into);
                Assert.DoesNotContain(into, k => trapKeys.Contains(k));
            }
            // an Active whose clip is the trap (REPAY) still loads it
            ActorClips.ChampionStrips(who, new[] { new EquippedSkill(SkillCatalogue.ById("snare_repay"), Source.Body) }, _ => true, into);
            Assert.Contains(into, k => trapKeys.Contains(k));
        }
        // ...and the hunt commits none: the opportunistic post-bite clip and its grace are gone
        var src = Hunt();
        Assert.DoesNotContain("CommitPlainClip(\"trap\"", src);
        Assert.DoesNotContain("TrapClipGraceMs", src);
        Assert.DoesNotContain("LastTrapBefore", src);
    }

    [Fact]
    public void test_the_skill_case_skips_a_phantom_before_any_feedback()
    {
        var skill = SkillCase();
        var gate = skill.IndexOf("if (reactionRecipe is null && GenericHits.IsPhantom(castDef, struck))", StringComparison.Ordinal);
        Assert.True(gate > 0, "the phantom gate is missing from the Skill case");
        Assert.Contains("var struck = GenericHits.IsStruck(batch, bi);", skill);
        Assert.Contains("PresentTrace.Log(\"skill-skip\"", skill);
        // nothing that speaks or draws comes before the gate
        var before = skill[..gate];
        foreach (var feedback in new[] { "UiMotion.Flash(", "Say(", "PlaySkillVfx(", "Sound?.Play(", "SpawnReaction(" })
            Assert.DoesNotContain(feedback, before);
        // a legacy reaction thuds only for an answer that struck (OATHMARK opens a window and strikes nothing)
        var thud = skill.IndexOf("if (isReaction && reactionRecipe is null) Sound?.Play(\"sfx_hit\"", StringComparison.Ordinal);
        Assert.True(thud > 0);
        var guard = skill.LastIndexOf("if (struck)", thud, StringComparison.Ordinal);
        Assert.True(guard > 0 && skill[guard..thud].Count(c => c == '}') == 0, "the reaction thud is not inside `if (struck)`");
    }

    [Fact]
    public void test_the_skill_case_quiets_an_echo()
    {
        var skill = SkillCase();
        Assert.Contains("var echo = GenericHits.IsEcho(batch, bi, ReactionSlots());", skill);
        Assert.Contains("PresentTrace.Log(\"echo\", $\"slot=", skill);
        Assert.Contains("if (ShowSkillCallouts && !echo && CalloutFor(castDef) is { } call", skill);
        Assert.Contains("PlaySkillVfx(castDef, castSk.Source, castTarget, echo)", skill);
        var cast = skill.IndexOf("Sound?.Play(\"sfx_cast\"", StringComparison.Ordinal);
        var guard = skill.LastIndexOf("if (!echo && !isReaction)", cast, StringComparison.Ordinal);
        Assert.True(guard > 0 && skill[guard..cast].Count(c => c == '}') == 0, "sfx_cast is not inside `if (!echo && !isReaction)`");
        Assert.Equal(0.6f, GenericHits.EchoScale);
        Assert.Equal(0.5f, GenericHits.EchoCueScale);
    }

    // ── THE LAST-OWNER RULE FOR THE PERFORMED AND THE REACTION HIT (the review of Phase 0) ─────────────────────────────
    //    weaver_trace.log 11200: HARD HANDS struck slot 0, SPRAY's echo struck slots 1-3, and each echo Strike took HARD
    //    HANDS' signature flash (0.55 / 120) and no cue, because performedHit keyed on the beat's ms alone.

    [Fact]
    public void test_an_echo_s_strikes_are_not_the_performed_action_s()
    {
        const int at = 11200;
        const int performedSlot = 0;
        var batch = new List<BattleEvent>
        {
            Skill(0, at), Strike(0, at),                          // HARD HANDS, performed
            Skill(1, at), Strike(1, at), Strike(2, at), Strike(3, at),   // SPRAY, woven under WEAVER
        };
        var owner = StrikeOwner.None;
        var echoSlot = -1;
        var performed = new List<int>();
        var echoed = new List<int>();
        for (var i = 0; i < batch.Count; i++)
        {
            var e = batch[i];
            owner = owner.After(e);
            if (e.Kind == BattleEventKind.Skill && GenericHits.IsEcho(batch, i)) echoSlot = e.Slot;
            if (e.Kind != BattleEventKind.Strike) continue;
            if (owner.IsSkillsBlow(e, performedSlot)) performed.Add(i);
            else if (owner.IsSkillsBlow(e, echoSlot)) echoed.Add(i);
        }
        Assert.Equal(new[] { 1 }, performed);             // only the action's own blow is performed
        Assert.Equal(new[] { 3, 4, 5 }, echoed);          // the echo's three are generic, at echo scale
        // a swing on the beat's ms is nobody's
        Assert.False(owner.IsSkillsBlow(new BattleEvent(BattleEventKind.Strike, 0, 10, at, HitSource.Swing), 1));
    }

    [Fact]
    public void test_the_strike_case_requires_the_owner_s_slot_and_scales_the_echo()
    {
        var src = Hunt();
        var strike = src[src.IndexOf("case BattleEventKind.Strike:", StringComparison.Ordinal)..];
        strike = strike[..strike.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)];
        Assert.Contains("&& lastOwner.IsSkillsBlow(e, performer.SkillSlot);", strike);
        Assert.Contains("&& lastOwner.IsSkillsBlow(e, reactionHitSlot);", strike);
        Assert.Contains("var echoHit = !auraTick && !performedHit && !reactionHit && e.AtMs == echoAtMs && lastOwner.IsSkillsBlow(e, echoSlot);", strike);
        Assert.Contains("(e.FromSkill ? 0.22f : 0.38f) * (echoHit ? GenericHits.EchoCueScale : 1f)", strike);
        Assert.Contains("(UsualFlash.Peak * GenericHits.EchoScale, UsualFlash.Rise, UsualFlash.Rate)", strike);
        var skill = SkillCase();
        Assert.Contains("if (echo) { echoAtMs = e.AtMs; echoSlot = e.Slot; }", skill);
        Assert.Contains("reactionHitSlot = e.Slot;", skill);
    }

    [Fact]
    public void test_a_legacy_reaction_plays_no_cast_breath_and_its_thud_voices_the_ms()
    {
        var skill = SkillCase();
        // the cast breath is gated off for every reaction, as CalloutFor gates its name
        var cast = skill.IndexOf("Sound?.Play(\"sfx_cast\"", StringComparison.Ordinal);
        var isReaction = skill.IndexOf("var isReaction = castDef.Kind == SkillKind.Reaction;", StringComparison.Ordinal);
        Assert.True(isReaction > 0 && isReaction < cast, "the reaction test must come before the cast breath");
        // the pitched answer marks the ms voiced, so its own Strike's generic 0.22 does not also ask
        var thud = skill.IndexOf("Sound?.Play(\"sfx_hit\", 0.40f, pitch: 0.25f", StringComparison.Ordinal);
        Assert.True(thud > 0);
        Assert.Contains("if (isReaction && reactionRecipe is null) _hitVoicedAtMs = e.AtMs;", skill);
        var mark = skill.IndexOf("if (isReaction && reactionRecipe is null) _hitVoicedAtMs = e.AtMs;", StringComparison.Ordinal);
        Assert.True(mark > thud && skill[thud..mark].Count(c => c == '}') == 0, "the reaction thud does not voice its ms inside `if (struck)`");
    }

    [Fact]
    public void test_the_owner_reads_allocate_nothing()
    {
        var batch = new List<BattleEvent> { Skill(0, 2000), Strike(0, 2000), Skill(1, 2000), Strike(1, 2000), Strike(2, 2000) };
        var n = 0;
        void Run()
        {
            var owner = StrikeOwner.None;
            for (var i = 0; i < batch.Count; i++)
            {
                owner = owner.After(batch[i]);
                if (owner.IsSkillsBlow(batch[i], 0)) n++;
                if (owner.IsSkillsBlow(batch[i], 1)) n++;
            }
        }
        Run();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var r = 0; r < 1000; r++) Run();
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(3003, n);
    }
}
