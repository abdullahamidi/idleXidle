using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Game.Presentation;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE GENERIC STRIKE PATH (the remaining-skill sweep, Phase 0 / P0.3; design.md sections 1, 3 and 8): derived hits are
/// quiet, N creatures make one thud per batch millisecond, and a Strike belongs to the LAST Aura or Skill at its ms.
/// The decisions live in <see cref="GenericHits"/> / <see cref="StrikeOwner"/>; the text pins hold the hunt's Strike case
/// to them.
/// </summary>
public class HuntGenericHitsTest
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

    /// <summary>The hunt's Strike case, up to the enemy's bite.</summary>
    private static string StrikeCase()
    {
        var src = Hunt();
        var strike = src[src.IndexOf("case BattleEventKind.Strike:", StringComparison.Ordinal)..];
        return strike[..strike.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)];
    }

    private static BattleEvent Strike(int slot, int ms, HitSource? hit = HitSource.Primary)
        => new(BattleEventKind.Strike, slot, 10, ms, hit);

    [Theory]
    [InlineData(HitSource.Bleed)]
    [InlineData(HitSource.Reflect)]
    [InlineData(HitSource.Carry)]
    [InlineData(HitSource.Deadweight)]
    public void test_a_bleed_reflect_carry_or_deadweight_hit_is_quiet(HitSource hit)
    {
        Assert.True(GenericHits.IsQuietHit(hit));
    }

    [Fact]
    public void test_a_swing_and_a_primary_hit_are_not_quiet()
    {
        Assert.False(GenericHits.IsQuietHit(HitSource.Swing));
        Assert.False(GenericHits.IsQuietHit(HitSource.Primary));
        Assert.False(GenericHits.IsQuietHit(HitSource.Other));
        Assert.False(GenericHits.IsQuietHit(null));
    }

    [Fact]
    public void test_a_strike_belongs_to_the_most_recent_aura_or_skill_at_its_ms()
    {
        // the fight's order on a shared ms: the field fork's Aura and its Strikes, then the cast's Skill and its Strikes
        // (a PULSE cast on a MIRE tick)
        const int at = 3000;
        var owner = StrikeOwner.None;
        owner = owner.After(new BattleEvent(BattleEventKind.Aura, 1, 0, at));
        var mire = Strike(0, at);
        Assert.True(owner.IsAuraTick(mire));
        Assert.Equal(1, owner.Slot);
        owner = owner.After(mire);                                  // a Strike changes no owner
        Assert.Equal(StrikeOwnerKind.Aura, owner.Kind);
        owner = owner.After(new BattleEvent(BattleEventKind.Skill, 2, 0, at));
        var pulse = Strike(0, at);
        Assert.False(owner.IsAuraTick(pulse));                      // the cast's blow, not a tick
        Assert.True(owner.Owns(pulse));
        Assert.Equal(StrikeOwnerKind.Skill, owner.Kind);
        Assert.Equal(2, owner.Slot);
        // a swing on the same ms is nobody's, and a later ms is not this owner's
        Assert.False(owner.Owns(Strike(0, at, HitSource.Swing)));
        Assert.False(owner.Owns(Strike(0, at + 500)));
    }

    [Fact]
    public void test_an_aura_alone_owns_its_ticks()
    {
        var owner = StrikeOwner.None.After(new BattleEvent(BattleEventKind.Aura, 1, 0, 2000));
        Assert.True(owner.IsAuraTick(Strike(0, 2000)));
        Assert.True(owner.IsAuraTick(Strike(3, 2000)));
        Assert.False(owner.IsAuraTick(Strike(0, 2500)));            // the next ms is not the tick's
        Assert.False(owner.IsAuraTick(Strike(0, 2000, HitSource.Swing)));
        Assert.False(StrikeOwner.None.Owns(Strike(0, -1)));          // no owner owns nothing
    }

    [Fact]
    public void test_the_strike_case_reads_is_quiet_hit_before_flash_puff_and_thud()
    {
        var strike = StrikeCase();
        var quiet = strike.IndexOf("var quietHit = GenericHits.IsQuietHit(e.Hit);", StringComparison.Ordinal);
        Assert.True(quiet >= 0, "the Strike case must ask GenericHits.IsQuietHit");
        Assert.True(quiet < strike.IndexOf("Sound?.Play(\"sfx_hit\"", StringComparison.Ordinal));
        Assert.True(quiet < strike.IndexOf("_hitFlash[e.Slot] = 1f;", StringComparison.Ordinal));
        Assert.True(quiet < strike.IndexOf("VfxProfiles.ImpactWeak", StringComparison.Ordinal));
        Assert.Contains("if (!auraTick && !quietHit && !voicedThisMs && !performedHit && !reactionHit) Sound?.Play(\"sfx_hit\"", strike);
        Assert.Contains("else if (!auraTick && !quietHit && _hitFlash.GetValueOrDefault(e.Slot) <= 0f)", strike);
        Assert.Contains("if (!auraTick && !quietHit && !performedHit && !reactionHit && (_strikeCount++ & 1) == 0)", strike);
        // quiet grade, and a derived hit never folds into a real blow's number
        Assert.Contains("NumberGrade.Quiet", strike);
        Assert.Contains("var total = GenericHits.Fold(batch, bi, _summed, out var hits);", strike);
        Assert.Contains("if (crit && !quietHit && e.AtMs != _critVoicedAtMs)", strike);
    }

    [Fact]
    public void test_sfx_hit_is_voiced_at_most_once_per_ms()
    {
        var strike = StrikeCase();
        Assert.Contains("var voicedThisMs = e.AtMs == _hitVoicedAtMs;", strike);
        Assert.Contains("if (!auraTick && !quietHit && !performedHit && !reactionHit) _hitVoicedAtMs = e.AtMs;", strike);
        // exactly one generic thud site in the Strike case
        var first = strike.IndexOf("Sound?.Play(\"sfx_hit\"", StringComparison.Ordinal);
        Assert.Equal(-1, strike.IndexOf("Sound?.Play(\"sfx_hit\"", first + 1, StringComparison.Ordinal));
    }

    [Fact]
    public void test_aura_at_ms_no_longer_exists_and_the_owner_is_the_last_one()
    {
        var src = Hunt();
        Assert.DoesNotContain("auraAtMs", src);
        Assert.Contains("var lastOwner = StrikeOwner.None;", src);
        Assert.Contains("var auraTick = lastOwner.IsAuraTick(e);", StrikeCase());
        var skill = src[src.IndexOf("case BattleEventKind.Skill:", StringComparison.Ordinal)..];
        skill = skill[..skill.IndexOf("SkillCastsSeen++;", StringComparison.Ordinal)];
        Assert.Contains("lastOwner = lastOwner.After(e);", skill);   // before any early break
    }

    [Fact]
    public void test_a_synthetic_batch_through_the_pure_helpers_allocates_nothing_and_voices_once_per_ms()
    {
        // a fightmulti-shaped batch: a field tick on four creatures, a cast on the same ms hitting four, a bleed, a carry
        var batch = new[]
        {
            Strike(0, 1000, HitSource.Bleed),
            new BattleEvent(BattleEventKind.Aura, 1, 0, 1000),
            Strike(0, 1000), Strike(1, 1000), Strike(2, 1000), Strike(3, 1000),
            new BattleEvent(BattleEventKind.Skill, 2, 0, 1000),
            Strike(0, 1000), Strike(1, 1000), Strike(2, 1000), Strike(3, 1000),
            Strike(1, 1000, HitSource.Carry),
            Strike(0, 1500, HitSource.Swing), Strike(1, 1500, HitSource.Reflect),
        };
        var voiced = 0;
        var ticks = 0;
        var quiet = 0;
        RunBatch(batch, ref voiced, ref ticks, ref quiet);   // warm (JIT)
        voiced = ticks = quiet = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
            RunBatch(batch, ref voiced, ref ticks, ref quiet);
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(2000, voiced);   // one thud at 1000 (the cast's four), one at 1500 (the swing): never one per creature
        Assert.Equal(4000, ticks);    // the field's four blows stay the field's
        Assert.Equal(3000, quiet);    // bleed, carry, reflect
    }

    // ── THE LAST-OWNER RULE FOR THE NUMBERS (the review of Phase 0) ──────────────────────────────────────────────────
    //    after_pulse_on_mire: PULSE's 134 / 143 / 124 printed as -158 / -155 / -138 x2 (MIRE's 24 / 12 / 14 folded in) and
    //    MIRE's 50 printed again as its tick total. The fold ends at the next Aura or Skill; the slot loop produces both
    //    orders on a shared ms, so both are pinned.

    private static BattleEvent Hit(int slot, int amount, int ms) => new(BattleEventKind.Strike, slot, amount, ms, HitSource.Primary);

    /// <summary>The tick total as the hunt sums it: every Strike the last owner says is an aura tick.</summary>
    private static int TickTotal(BattleEvent[] batch)
    {
        var owner = StrikeOwner.None;
        var total = 0;
        foreach (var e in batch)
        {
            owner = owner.After(e);
            if (e.Kind == BattleEventKind.Strike && owner.IsAuraTick(e)) total += e.Amount;
        }
        return total;
    }

    [Fact]
    public void test_skill_then_aura_on_one_ms_folds_the_cast_alone()
    {
        const int at = 9000;
        var batch = new[]
        {
            new BattleEvent(BattleEventKind.Skill, 2, 0, at),
            Hit(0, 134, at), Hit(1, 143, at), Hit(2, 124, at),
            new BattleEvent(BattleEventKind.Aura, 1, 0, at),
            Hit(0, 24, at), Hit(1, 12, at), Hit(2, 14, at),
        };
        var summed = new HashSet<int>();
        Assert.Equal(134, GenericHits.Fold(batch, 1, summed, out var hits));
        Assert.Equal(1, hits);
        Assert.Equal(143, GenericHits.Fold(batch, 2, summed, out hits));
        Assert.Equal(1, hits);
        Assert.Empty(summed);                    // no aura-owned Strike is ever folded into the cast's number
        Assert.Equal(50, TickTotal(batch));      // ...and the tick total is the tick's alone
    }

    [Fact]
    public void test_aura_then_skill_on_one_ms_folds_the_cast_alone()
    {
        const int at = 9000;
        var batch = new[]
        {
            new BattleEvent(BattleEventKind.Aura, 1, 0, at),
            Hit(0, 24, at), Hit(1, 12, at), Hit(2, 14, at),
            new BattleEvent(BattleEventKind.Skill, 2, 0, at),
            Hit(0, 134, at), Hit(1, 143, at), Hit(2, 124, at),
        };
        var summed = new HashSet<int>();
        Assert.Equal(134, GenericHits.Fold(batch, 5, summed, out var hits));
        Assert.Equal(1, hits);
        Assert.Equal(124, GenericHits.Fold(batch, 7, summed, out hits));
        Assert.Equal(1, hits);
        Assert.Empty(summed);
        Assert.Equal(50, TickTotal(batch));
    }

    [Fact]
    public void test_a_multi_hit_cast_still_folds_up_to_the_next_owner()
    {
        const int at = 4000;
        var batch = new[]
        {
            new BattleEvent(BattleEventKind.Skill, 0, 0, at),
            Hit(0, 100, at), Hit(0, 100, at), Hit(0, 100, at),
            new BattleEvent(BattleEventKind.Skill, 1, 0, at),   // a woven echo: its blow is its own number
            Hit(0, 40, at),
        };
        var summed = new HashSet<int>();
        Assert.Equal(300, GenericHits.Fold(batch, 1, summed, out var hits));
        Assert.Equal(3, hits);
        Assert.Equal(new[] { 2, 3 }, summed.OrderBy(i => i));
        Assert.Equal(40, GenericHits.Fold(batch, 5, summed, out hits));
        Assert.Equal(1, hits);
    }

    [Fact]
    public void test_the_fold_allocates_nothing()
    {
        var batch = new[]
        {
            new BattleEvent(BattleEventKind.Skill, 0, 0, 10), Hit(0, 5, 10), Hit(0, 5, 10),
            new BattleEvent(BattleEventKind.Aura, 1, 0, 10), Hit(0, 2, 10),
        };
        var summed = new HashSet<int>();
        GenericHits.Fold(batch, 1, summed, out _);
        var total = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            summed.Clear();
            total += GenericHits.Fold(batch, 1, summed, out _);
        }
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(10_000, total);
    }

    [Fact]
    public void test_the_tick_total_is_flushed_when_its_batch_ends_not_on_a_wall_clock()
    {
        var src = Hunt();
        Assert.DoesNotContain("_auraTotalMs + 1_000 * 0.5f", src);
        // the flush follows the batch loop: after the last case of the switch, before the heal's sum
        var flush = src.IndexOf("        if (_auraTotal > 0) FlushAuraTotal();", StringComparison.Ordinal);
        Assert.True(flush > 0, "the tick total is not flushed after the batch");
        Assert.True(flush > src.IndexOf("case BattleEventKind.ShieldBroken:", StringComparison.Ordinal));
        Assert.True(flush < src.IndexOf("if (_healReceive.Update(_playheadMs, out var healed))", StringComparison.Ordinal));
    }

    // ── THE NUMBER IS BORN ON THE BODY (the review of Phase 0) ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(700, 760, 220, 150)]    // a crouching whelp's drawn body
    [InlineData(600, 420, 420, 480)]    // a boss
    [InlineData(1100, 830, 120, 60)]    // a small creature
    public void test_a_quiet_number_starts_inside_the_struck_body(int x, int y, int w, int h)
    {
        var body = new Microsoft.Xna.Framework.Rectangle(x, y, w, h);
        for (var stack = 0; stack < 3; stack++)
        {
            var top = NumberLane.QuietY(body, stack, 22);
            Assert.InRange(top, body.Top - (int)(0.3f * h), body.Bottom);   // inside, or at most 0.3 h above
            Assert.True(top >= body.Top, "a quiet number climbed out of the body it describes");
        }
        Assert.Equal(body.Top + (int)MathF.Round(h * NumberLane.QuietBodyShare), NumberLane.QuietY(body, 0, 22));
    }

    [Fact]
    public void test_a_plain_number_starts_at_the_drawn_head_not_the_row_lane()
    {
        var body = new Microsoft.Xna.Framework.Rectangle(700, 760, 220, 150);
        var head = NumberLane.HeadY(body, 16, 54);
        Assert.Equal(body.Top - 16 - 54, head);
        // ...and the hunt reads the creature's drawn bounds for both, not the row's layout box
        var src = Hunt();
        var spawn = src[src.IndexOf("private void SpawnDamage(", StringComparison.Ordinal)..];
        spawn = spawn[..spawn.IndexOf("private void UpdateFight(", StringComparison.Ordinal)];
        Assert.Contains("Y = EnemyNumberY(slot, quiet),", spawn);
        Assert.Contains("_actors.TryBounds(VfxSubject.Creature(slot), out var vb)", spawn);
        Assert.Contains("NumberLane.QuietY(vb.Rect,", spawn);
        Assert.Contains("NumberLane.HeadY(vb.Rect,", spawn);
        Assert.DoesNotContain("Y = EnemyCalloutBase - StackSlot(CalloutLane.Enemy)", spawn);
        // each body stacks its own numbers: three creatures struck at once print at one height, not a staircase
        Assert.Contains("StackSlot(CalloutLane.Enemy, CalloutLanesDeep, slot)", spawn);
        Assert.Contains("StackSlot(CalloutLane.EnemyQuiet, QuietLanesDeep, slot)", spawn);
        Assert.Contains("Owner = slot,", spawn);
    }

    /// <summary>The Strike case's decisions, as the hunt makes them, over one batch.</summary>
    private static void RunBatch(BattleEvent[] batch, ref int voiced, ref int ticks, ref int quiet)
    {
        var owner = StrikeOwner.None;
        var voicedAtMs = -1;
        for (var i = 0; i < batch.Length; i++)
        {
            var e = batch[i];
            owner = owner.After(e);
            if (e.Kind != BattleEventKind.Strike) continue;
            var auraTick = owner.IsAuraTick(e);
            var quietHit = GenericHits.IsQuietHit(e.Hit);
            if (auraTick) ticks++;
            if (quietHit) quiet++;
            if (!auraTick && !quietHit && e.AtMs != voicedAtMs) voiced++;
            if (!auraTick && !quietHit) voicedAtMs = e.AtMs;
        }
    }
}
