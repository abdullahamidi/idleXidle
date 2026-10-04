using System;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Game.Presentation;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// A QUIET DERIVED HIT IS NEVER A REACTION'S ANSWER (the Phase 1 review of the real footage): the hunt echoed EVERY Strike
/// a presented reaction owned at its millisecond as the reaction's skill-grade, captioned, Source-outlined answer, so the
/// Anvil's DEADWEIGHT release printed "-26 JAWS" beside the real "-2 JAWS" (and a bleed tick or a carry on the bite's ms
/// would do the same). A quiet hit is now held for the snap like any answer (its drop still follows the shown bite) but
/// prints at Quiet grade, with no word and no outline; the reaction's own Strike is unchanged.
/// </summary>
public class reaction_quiet_answer_test
{
    private static readonly Color Shadow = new(0x9B, 0x7B, 0xFF);

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static string Hunt() => File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));

    [Theory]
    [InlineData(HitSource.Deadweight)]
    [InlineData(HitSource.Bleed)]
    [InlineData(HitSource.Carry)]
    [InlineData(HitSource.Reflect)]
    public void test_a_quiet_hit_at_a_presented_reactions_ms_prints_quiet_uncaptioned_and_unoutlined(HitSource hit)
    {
        // what the screen would have printed for a Strike the reaction owns at its ms: skill grade, its word, its Source
        var look = GenericHits.Look(GenericHits.IsQuietHit(hit), skill: true, word: "JAWS", outline: Shadow);
        Assert.Equal(NumberGrade.Quiet, look.Grade);
        Assert.Null(look.Word);
        Assert.Null(look.Outline);
    }

    [Theory]
    [InlineData(HitSource.Primary)]
    [InlineData(HitSource.Swing)]
    public void test_the_reactions_own_strike_keeps_its_grade_word_and_outline(HitSource hit)
    {
        var look = GenericHits.Look(GenericHits.IsQuietHit(hit), skill: true, word: "JAWS", outline: Shadow);
        Assert.Equal(NumberGrade.Skill, look.Grade);
        Assert.Equal("JAWS", look.Word);
        Assert.Equal(Shadow, look.Outline);
        // a plain blow stays plain, and FIRST BEAT's white survives on a real blow
        var plain = GenericHits.Look(quietHit: false, skill: false, word: null, outline: Color.White);
        Assert.Equal(NumberGrade.Plain, plain.Grade);
        Assert.Equal(Color.White, plain.Outline);
    }

    [Fact]
    public void test_the_held_answer_carries_a_grade_and_the_snap_prints_it()
    {
        var src = Hunt();
        var strike = src[src.IndexOf("case BattleEventKind.Strike:", StringComparison.Ordinal)..];
        strike = strike[..strike.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)];
        // both the held answer and the at-once number decide grade, word and outline in one place, by quietness first
        var held = strike[strike.IndexOf("if (reactionHit && reactionHitPerf is { Answered: false } answering)", StringComparison.Ordinal)..];
        held = held[..held.IndexOf("else", StringComparison.Ordinal)];
        Assert.Contains("GenericHits.Look(quietHit, skill,", held);
        Assert.Contains("held.Grade, hits,", held);
        Assert.Contains("held.Word, held.Outline", held);
        Assert.Contains("SpawnDamage(total, e.Slot, crit, look.Grade, hits, look.Word, look.Outline);", strike);
        // the echo holds a grade (no Skill bool any more), and the snap prints that grade; a quiet echo never speaks
        Assert.Contains("NumberGrade Grade = NumberGrade.Plain", src);
        Assert.DoesNotContain("bool Skill = false", src);
        var release = src[src.IndexOf("private void ReleaseEchoes(", StringComparison.Ordinal)..];
        release = release[..release.IndexOf("private readonly List<ReactionPerformance> _reactions", StringComparison.Ordinal)];
        Assert.Contains("SpawnDamage(echo.Amount, echo.Slot, echo.Crit, echo.Grade, echo.Hits, echo.Word, echo.Outline);", release);
        Assert.Contains("if (echo.Crit && echo.Grade != NumberGrade.Quiet)", release);
    }

    [Fact]
    public void test_the_quiet_number_trace_names_its_grade()
    {
        // the echoed trace line carries grade=Quiet (SpawnDamage's quiet branch), and a quiet look never asks for an outline
        Assert.Contains("\\tgrade=Quiet\"", Hunt());
        Assert.Null(GenericHits.Look(quietHit: true, skill: false, word: null, outline: Color.White).Outline);
    }
}
