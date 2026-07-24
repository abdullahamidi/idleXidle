using System;
using System.Collections.Generic;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using Xunit;

namespace ResonanceHunter.Core.Tests.Expeditions;

/// <summary>
/// The replay must agree with the sim. If it doesn't, the health bar lies to the player — and in an
/// auto-battler the bar IS the feedback, because there is no other way to tell what the champion is doing.
/// </summary>
/// <remarks>
/// Drives the live single-champion <see cref="SoloBattle"/> (slot 0), not the retired squad engine. The
/// hand-built cases below still exercise multi-slot shielding and playhead windowing — those are the
/// replay's own rules and hold regardless of how many slots the sim ever fills.
/// </remarks>
public class WaveReplayTests
{
    // A stand-in shield duration for the hand-built cases. The replay reads the duration off the event's
    // Amount, so the exact number is arbitrary — it only has to outlast, then lapse within, the advances.
    private const int ShieldMs = 2500;

    private static EquippedSkill Sk(Form form)
        => new(new WovenAbility { Name = form.ToString(), Source = Source.Nature, Form = form },
               FormBehaviour.BaseCooldownMs(form));

    private static Build With(params Form[] forms)
    {
        var b = new Build();
        foreach (var f in forms) b.Weave(Sk(f));
        return b;
    }

    /// <summary>
    /// Resolve one wave for a champion and replay it to the end, returning (sim health, replayed health).
    /// </summary>
    /// <remarks>
    /// Drives <see cref="SoloBattle.ResolveWave"/> directly rather than <see cref="SoloExpedition.PushWave"/>:
    /// PushWave also applies between-wave healing, which is NOT part of the event stream and would
    /// therefore mask the very drift under test.
    /// </remarks>
    private static (int Sim, int Replayed) RunAndReplay(Build build, int championHp, float enemyHealth, float enemyDamage)
    {
        var champ = new Champion { MaxHealth = championHp, Health = championHp };
        var start = new Dictionary<int, int> { [0] = champ.Health };
        var max = new Dictionary<int, int> { [0] = champ.MaxHealth };

        var (_, events) = SoloBattle.ResolveWave(
            champ, build, new Hunter(), enemyHealth, enemyDamage,
            enemyIntervalMs: 1000, ExpeditionTuning.Default, new Random(7), new WaveBonus());

        var replay = new WaveReplay(events, start, max, enemyHealth);
        replay.Advance(float.MaxValue);

        return (champ.Health, replay.HealthOf(0));
    }

    /// <summary>A champion that never heals: the replay is a plain subtraction and must line up exactly.</summary>
    [Fact]
    public void test_replayed_health_matches_the_sim_without_healing()
    {
        // STRIKE deals damage but leeches nothing, so no Heal beat ever enters the stream. A huge enemy
        // means the wave stalls at the ceiling — the champion takes many bites without ever winning.
        var (sim, replayed) = RunAndReplay(With(Form.Strike), championHp: 4000, enemyHealth: 500_000f, enemyDamage: 5f);

        Assert.Equal(sim, replayed);
    }

    /// <summary>
    /// THE regression. TRANSFORMATION leeches health mid-wave, so a replay that only ever subtracts drifts
    /// LOW — the bar shows the champion dying while the sim has it healthy, then snaps upward at the next
    /// wave with no explanation. If the screen can't show the heal, the whole Form looks worthless.
    /// </summary>
    [Fact]
    public void test_replayed_health_matches_the_sim_when_a_heal_lands_mid_wave()
    {
        // A moderate bite hurts the champion; TRANSFORMATION's on-hit leech heals some back. The trajectory
        // rises and falls, so a replay that dropped the Heal beats would end on a different number.
        var (sim, replayed) = RunAndReplay(With(Form.Transformation), championHp: 400, enemyHealth: 500_000f, enemyDamage: 22f);

        Assert.Equal(sim, replayed);
    }

    /// <summary>A heal must clamp at the ceiling exactly as the sim clamps it — never overshoot.</summary>
    [Fact]
    public void test_a_heal_never_pushes_a_slot_above_its_maximum()
    {
        var events = new List<BattleEvent>
        {
            new(BattleEventKind.EnemyStrike, 0, 5, 100),
            new(BattleEventKind.Heal, 0, 999, 200),   // wildly more than the slot can hold
        };

        var replay = new WaveReplay(
            events,
            new Dictionary<int, int> { [0] = 100 },
            new Dictionary<int, int> { [0] = 100 },
            enemyHealth: 50f);

        replay.Advance(float.MaxValue);

        Assert.Equal(100, replay.HealthOf(0));
    }

    /// <summary>The playhead is what makes it a replay: nothing before its time.</summary>
    [Fact]
    public void test_events_apply_only_once_the_playhead_reaches_them()
    {
        var events = new List<BattleEvent>
        {
            new(BattleEventKind.EnemyStrike, 0, 10, 500),
            new(BattleEventKind.EnemyStrike, 0, 10, 1500),
        };

        var replay = new WaveReplay(
            events,
            new Dictionary<int, int> { [0] = 100 },
            new Dictionary<int, int> { [0] = 100 },
            enemyHealth: 50f);

        Assert.Equal(100, replay.HealthOf(0));      // nothing yet

        var crossed = replay.Advance(1000f);
        Assert.Single(crossed);
        Assert.Equal(90, replay.HealthOf(0));       // only the first beat
        Assert.False(replay.Finished);

        replay.Advance(2000f);
        Assert.Equal(80, replay.HealthOf(0));
        Assert.True(replay.Finished);
    }

    /// <summary>
    /// A shield is visible only while it holds, and only on the slots the SIM says it covers. The view must
    /// never re-derive that reach — a second copy of the rule is a second thing to get wrong.
    /// </summary>
    [Fact]
    public void test_a_shield_shows_only_on_covered_slots_and_only_while_it_holds()
    {
        var events = new List<BattleEvent>
        {
            new(BattleEventKind.Shield, 1, ShieldMs, 1000),
        };

        var replay = new WaveReplay(
            events,
            new Dictionary<int, int> { [0] = 100, [1] = 100, [2] = 100 },
            new Dictionary<int, int> { [0] = 100, [1] = 100, [2] = 100 },
            enemyHealth: 50f);

        replay.Advance(1200f);
        Assert.True(replay.IsShielded(1));
        Assert.False(replay.IsShielded(0));   // never told it was covered, so it isn't drawn as covered

        replay.Advance(1000f + ShieldMs + 1);
        Assert.False(replay.IsShielded(1));   // it lapses — WHEN it fires has to matter
    }

    /// <summary>The windup ring needs to know when the next bite lands — that's the anticipation read.</summary>
    [Fact]
    public void test_next_enemy_strike_finds_the_following_swing()
    {
        var events = new List<BattleEvent>
        {
            new(BattleEventKind.Strike, 0, 10, 400),
            new(BattleEventKind.EnemyStrike, 0, 10, 1500),
            new(BattleEventKind.EnemyStrike, 0, 10, 3000),
        };

        var replay = new WaveReplay(
            events,
            new Dictionary<int, int> { [0] = 100 },
            new Dictionary<int, int> { [0] = 100 },
            enemyHealth: 50f);

        Assert.Equal(1500, replay.NextEnemyStrikeAfter(0f));
        Assert.Equal(3000, replay.NextEnemyStrikeAfter(1500f));
        Assert.Equal(int.MaxValue, replay.NextEnemyStrikeAfter(3000f));
    }
}
