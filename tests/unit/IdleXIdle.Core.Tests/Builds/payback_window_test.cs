using System;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// REPAY's bank obeys its window. VENGEANCE's card said "only damage taken in the last 3s counts"
/// from the day it was authored, and the sim accumulated since the last cast with no window at all —
/// the variation's stated price was pure upside (audit 2026-08-31, skills §10.5).
/// </summary>
public class PaybackWindowTest
{
    /// <summary>A one-skill REPAY build whose payback window is the test's own dial.</summary>
    private static Build RepayBuild(int windowMs)
    {
        var build = new Build();
        build.Weave(TestBuilds.Skill("snare_repay", Source.Shadow,
            tweak: d => d with { PaysBackDamageTaken = 2.0f, PaybackWindowMs = windowMs }));
        return build;
    }

    private static float SkillDamage(Build build, int enemyIntervalMs)
    {
        var champ = new Champion { MaxHealth = 1_000_000, Health = 1_000_000 };
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, new Hunter(), enemyHealth: 1e9f, enemyDamage: 200f,
            enemyIntervalMs: enemyIntervalMs, ExpeditionTuning.Default, new Random(11));
        return events.Where(e => e.Kind == BattleEventKind.Strike && e.FromSkill).Sum(e => (float)e.Amount);
    }

    [Fact]
    public void test_stale_damage_falls_out_of_a_windowed_payback_bank()
    {
        // Arrange/Act — slow, heavy bites (every 6s): by the time REPAY casts, most of what was
        // taken is older than a 3s window. The unbounded twin banks all of it.
        var unbounded = SkillDamage(RepayBuild(windowMs: 0), enemyIntervalMs: 6_000);
        var windowed = SkillDamage(RepayBuild(windowMs: 3_000), enemyIntervalMs: 6_000);

        // Assert — the window is a real price, not decoration.
        Assert.True(windowed < unbounded,
            $"a 3s window banked {windowed:F0} against the unbounded {unbounded:F0} — stale damage is not expiring");
    }

    [Fact]
    public void test_a_window_longer_than_the_cast_cycle_changes_nothing()
    {
        // Arrange/Act — REPAY's cycle is five beats (~7.5s), so ANY finite window shorter than the
        // cycle drops something; one longer than it can drop nothing. This is the half that stops
        // the window from becoming a silent global nerf: it only ever removes what is genuinely
        // older than the promise on the card.
        var unbounded = SkillDamage(RepayBuild(windowMs: 0), enemyIntervalMs: 1_000);
        var windowed = SkillDamage(RepayBuild(windowMs: 60_000), enemyIntervalMs: 1_000);

        Assert.Equal(unbounded, windowed, precision: 0);
    }

    [Fact]
    public void test_vengeance_carries_the_window_its_card_promises()
    {
        // The content itself: VENGEANCE's resolved def pays 3.5x and counts only the last 3s.
        var vengeance = TestBuilds.Resolved("snare_repay", "VENGEANCE");

        Assert.Equal(3.5f, vengeance.PaysBackDamageTaken, precision: 3);
        Assert.Equal(3_000, vengeance.PaybackWindowMs);
    }
}
