using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Traits;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// THE THREE THINGS A BITE CAN BE, told apart in the event stream — because the fight screen prints
/// a Health number from one of them and must never print one for the other two.
/// </summary>
/// <remarks>
/// <para>
/// Health damage is <see cref="BattleEventKind.EnemyStrike"/> with an Amount; Shield absorption is
/// <see cref="BattleEventKind.ShieldAbsorbed"/> and leaves the strike's Amount at what reached the
/// pool; a hit the build PREVENTED outright is a strike with Amount 0 and no absorption at all.
/// The screen's rule (HuntScreen.HunterHitText) is "a number only when the Amount is above zero",
/// and this file is what makes that rule true of the sim rather than a hope about it.
/// </para>
/// </remarks>
public class champion_damage_events_test
{
    private static readonly ExpeditionTuning NoSwing = ExpeditionTuning.Default with { AutoAttackDamage = 0f };

    private static List<WaveCreature> Biters(float damage, int count = 1)
        => Enumerable.Range(0, count).Select(_ => new WaveCreature
        {
            MaxHealth = 1_000_000f, Health = 1_000_000f, Damage = damage,
        }).ToList();

    private static (List<BattleEvent> Events, WaveMetrics Metrics) Fight(Champion champ, Build build, List<WaveCreature> creatures)
    {
        var metrics = new WaveMetrics();
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, new Hunter(), creatures, 900, NoSwing, new Random(5), metrics: metrics);
        return (events, metrics);
    }

    [Fact]
    public void test_champion_damage_an_unshielded_bite_is_health_damage_and_nothing_else()
    {
        // Arrange
        var champ = new Champion { MaxHealth = 10_000, Health = 10_000 };

        // Act
        var (events, metrics) = Fight(champ, TestBuilds.Of("hammer_blow"), Biters(120f));

        // Assert — every bite reached the pool as a strike with an amount; nothing was absorbed; and
        // the strikes' amounts ARE the Health the metrics say was lost.
        var strikes = events.Where(e => e.Kind == BattleEventKind.EnemyStrike).ToList();
        Assert.NotEmpty(strikes);
        Assert.All(strikes, s => Assert.True(s.Amount > 0, "an unshielded bite must carry its Health amount"));
        Assert.DoesNotContain(events, e => e.Kind == BattleEventKind.ShieldAbsorbed);
        Assert.Equal(metrics.HealthDamage, strikes.Sum(s => s.Amount));
    }

    [Fact]
    public void test_champion_damage_a_bite_eaten_by_shield_is_absorption_not_health_loss()
    {
        // Arrange — HOLD FAST raises a wall every two seconds; the bites after it are smaller than
        // the wall. (A shield granted before the wave is reset at its start — the wall must go up
        // inside the fight, the way it does in play.)
        var champ = new Champion { MaxHealth = 10_000, Health = 10_000 };
        var build = TestBuilds.Of("sig_unbroken_hold_fast", "hammer_blow");

        // Act
        var (events, _) = Fight(champ, build, Biters(20f));

        // Assert — the first bite AFTER the wall is up reports ZERO Health lost, and the absorption
        // beside it says what the shield ate.
        var wallUp = events.First(e => e.Kind == BattleEventKind.ShieldGained).AtMs;
        var strike = events.First(e => e.Kind == BattleEventKind.EnemyStrike && e.AtMs > wallUp);
        var absorb = events.First(e => e.Kind == BattleEventKind.ShieldAbsorbed && e.AtMs > wallUp);
        Assert.Equal(0, strike.Amount);
        Assert.True(absorb.Amount > 0, "the shield must report what it absorbed");
        Assert.Equal(absorb.AtMs, strike.AtMs);
    }

    [Fact]
    public void test_champion_damage_a_prevented_bite_is_neither_health_loss_nor_absorption()
    {
        // Arrange — THE UNBROKEN THREAD stops the first bite outright at full health.
        var thread = TraitCatalogue.Find("t_unbroken_thread")!.Shape(default);
        var build = new Build { Shape = thread };
        build.Equip(TestBuilds.Skill("hammer_blow"));
        var champ = new Champion { MaxHealth = 10_000, Health = 10_000 };

        // Act
        var (events, _) = Fight(champ, build, Biters(120f));

        // Assert — the first strike carries nothing, no shield was involved, and the second one hurts.
        var strikes = events.Where(e => e.Kind == BattleEventKind.EnemyStrike).ToList();
        Assert.True(strikes.Count >= 2, "the fixture needs at least two bites");
        Assert.Equal(0, strikes[0].Amount);
        Assert.DoesNotContain(events, e => e.Kind == BattleEventKind.ShieldAbsorbed && e.AtMs <= strikes[0].AtMs);
        Assert.True(strikes[1].Amount > 0, "the guard is spent after one bite");
    }
}
