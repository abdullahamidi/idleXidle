using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Expeditions;

/// <summary>
/// THE ENEMY INSPECTOR READS THE REPLAY, AND THE REPLAY MATCHES THE SIM. What the fight screen prints
/// for a hovered creature — defence now, attack under its break, the bite clock under its slow, the
/// stagger and the Mark standing — is replayed from the sim's own events, never re-derived from its
/// rules; so the number the replay holds at the end of a wave must be the number the sim left behind.
/// </summary>
public class enemy_inspector_replay_test
{
    private static readonly ExpeditionTuning NoSwing = ExpeditionTuning.Default with { AutoAttackDamage = 0f };

    private static List<WaveCreature> Pack(int count, float defence = 40f, float damage = 30f)
        => Enumerable.Range(0, count).Select(_ => new WaveCreature
        {
            MaxHealth = 1_000_000f, Health = 1_000_000f, Damage = damage, Defense = defence,
        }).ToList();

    private static (List<BattleEvent> Events, WaveReplay Replay, List<WaveCreature> Creatures) Run(
        string skill, Source source, int count = 3, int intervalMs = 900)
    {
        // The replay is told the composition AS IT STOOD — a snapshot — because the sim mutates the
        // creatures it is handed, and the base a hover shows is the number before the fight touched it.
        var creatures = Pack(count);
        var snapshot = creatures.Select(c => new WaveCreature
        {
            MaxHealth = c.MaxHealth, Health = c.Health, Damage = c.Damage, Defense = c.Defense,
        }).ToList();
        var build = new Build();
        build.Equip(TestBuilds.Skill(skill, source));
        var champ = new Champion { MaxHealth = 200_000, Health = 200_000 };
        var (_, events) = SoloBattle.ResolveWave(champ, build, new Hunter(), creatures, intervalMs, NoSwing,
                                                 new Random(3), metrics: new WaveMetrics());
        var replay = new WaveReplay(events, new Dictionary<int, int> { [0] = 200_000 },
                                    new Dictionary<int, int> { [0] = 200_000 }, count * 1_000_000f);
        replay.SetComposition(snapshot, intervalMs);
        return (events, replay, creatures);
    }

    [Fact]
    public void test_enemy_inspector_defence_now_replays_to_the_sims_final_defence()
    {
        // Arrange / Act — PRESS strips the front creature's defence 5 a tick.
        var (events, replay, creatures) = Run("hammer_press", Source.Body);
        replay.Advance(events[^1].AtMs + 1);

        // Assert — base is what the wave opened with; now is what the sim left.
        Assert.Contains(events, e => e.Kind == BattleEventKind.DefenceNow);
        Assert.Equal(40f, replay.CreatureBaseDefence(0));
        Assert.True(replay.CreatureDefenceNow(0) < replay.CreatureBaseDefence(0), "PRESS never broke the front creature");
        Assert.Equal(MathF.Round(creatures[0].Defense), replay.CreatureDefenceNow(0));
    }

    [Fact]
    public void test_enemy_inspector_a_slow_stretches_the_bite_clock_it_reports()
    {
        // Arrange / Act — MIRE slows the wave's attacks by a quarter.
        var (events, replay, _) = Run("field_mire", Source.Nature);
        replay.Advance(events[^1].AtMs + 1);

        // Assert
        Assert.Contains(events, e => e.Kind == BattleEventKind.Slowed);
        Assert.True(replay.SlowPercent > 0, "MIRE reported no slow");
        Assert.Equal(900, replay.EnemyIntervalMs);
        Assert.Equal((int)MathF.Round(900 * (1f + replay.SlowPercent / 100f)), replay.BiteEveryMsNow);
    }

    [Fact]
    public void test_enemy_inspector_an_attack_break_lowers_the_damage_it_reports()
    {
        // Arrange / Act — WILT breaks every creature's attack a pulse at a time.
        var (events, replay, _) = Run("drain_wilt", Source.Shadow);
        replay.Advance(events[^1].AtMs + 1);

        // Assert — the base bite is the creature's own; the current one is under the standing break.
        Assert.Contains(events, e => e.Kind == BattleEventKind.AttackBreak);
        Assert.True(replay.CreatureAttackBreakPercent(0) < 0, "WILT reported no break");
        Assert.Equal(30f, replay.CreatureBaseDamage(0));
        Assert.True(replay.CreatureDamageNow(0) < replay.CreatureBaseDamage(0));
        Assert.Equal(30f * (1f + replay.CreatureAttackBreakPercent(0) / 100f), replay.CreatureDamageNow(0), 3);
    }

    [Fact]
    public void test_enemy_inspector_a_mark_stands_for_its_window_and_then_lapses()
    {
        // Arrange / Act — CALL opens a Mark on the wave.
        var (events, replay, _) = Run("sign_call", Source.Mind, count: 1);
        var mark = events.First(e => e.Kind == BattleEventKind.Marked);
        replay.Advance(mark.AtMs);

        // Assert — standing at the opening, with its percent; gone once its window has passed.
        Assert.True(replay.MarkLeftMs > 0, "the Mark is not standing at its own opening");
        Assert.True(replay.MarkPercent > 0, "the Mark has no depth");
        Assert.Equal(mark.Amount, replay.MarkLeftMs);
        replay.Advance(mark.AtMs + mark.Amount + 1);
        if (!events.Any(e => e.Kind == BattleEventKind.Marked && e.AtMs > mark.AtMs && e.AtMs <= mark.AtMs + mark.Amount + 1))
            Assert.Equal(0, replay.MarkLeftMs);
    }

    [Fact]
    public void test_enemy_inspector_a_fresh_composition_carries_no_status_from_the_last_wave()
    {
        // Arrange — a replay that has seen a slow, then told the next wave's composition.
        var (events, replay, _) = Run("field_mire", Source.Nature);
        replay.Advance(events[^1].AtMs + 1);
        Assert.True(replay.SlowPercent > 0);

        // Act
        replay.SetComposition(Pack(2), 1200);

        // Assert — every standing status is the new wave's: none.
        Assert.Equal(0, replay.SlowPercent);
        Assert.Equal(0, replay.CreatureAttackBreakPercent(0));
        Assert.Equal(0, replay.MarkLeftMs);
        Assert.Equal(1200, replay.BiteEveryMsNow);
        Assert.Equal(40f, replay.CreatureDefenceNow(0));
    }
}
