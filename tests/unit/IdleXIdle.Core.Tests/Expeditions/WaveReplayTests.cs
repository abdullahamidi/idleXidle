using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using Xunit;

namespace IdleXIdle.Core.Tests.Expeditions;

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

    private static Build With(params string[] skillIds) => TestBuilds.Of(skillIds);

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
        // BLOW deals damage but leeches nothing, so no Heal beat ever enters the stream. A huge enemy
        // means the wave stalls at the ceiling — the champion takes many bites without ever winning.
        var (sim, replayed) = RunAndReplay(With("hammer_blow"), championHp: 4000, enemyHealth: 500_000f, enemyDamage: 5f);

        Assert.Equal(sim, replayed);
    }

    /// <summary>
    /// THE regression. DRINK leeches health mid-wave, so a replay that only ever subtracts drifts
    /// LOW — the bar shows the champion dying while the sim has it healthy, then snaps upward at the next
    /// wave with no explanation. If the screen can't show the heal, the whole skill looks worthless.
    /// </summary>
    [Fact]
    public void test_replayed_health_matches_the_sim_when_a_heal_lands_mid_wave()
    {
        // A moderate bite hurts the champion; DRINK's lifesteal heals some back. The trajectory
        // rises and falls, so a replay that dropped the Heal beats would end on a different number.
        var (sim, replayed) = RunAndReplay(With("drain_drink"), championHp: 400, enemyHealth: 500_000f, enemyDamage: 22f);

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
            // UNDYING, not Shield: the duration-carrying cover got its own kind when the audit
            // found Shield's Amount meaning milliseconds for one producer and banked HEALTH for
            // the other (D8) — one payload, two meanings, and the replay could only honour one.
            new(BattleEventKind.Undying, 1, ShieldMs, 1000),
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

    // ── PER-CREATURE REPLAY (playtest 2026-08-26: "enemy health bars don't seem to drop correctly") ──

    /// <summary>
    /// A composition of four unequal creatures, replayed BEAT BY BEAT against a ledger of what the sim's
    /// own events say it dealt. At every timestamp each creature's replayed health must equal its start
    /// less every Strike the events have landed on it; and once the playhead has crossed everything, each
    /// creature must stand exactly where the sim left it — dead where the sim killed it, and within the
    /// rounding the events themselves carry everywhere else.
    /// </summary>
    /// <remarks>
    /// The bars the player watches are these per-creature fractions, so this is the property that decides
    /// whether "the bars drop correctly". Four DIFFERENT pools, so a strike credited to the wrong slot
    /// cannot hide behind a twin. It was written to find the bug in the report and found none here: the
    /// replay is honest, and the number that disagreed with the bars was the screen's own invention
    /// (see HuntScreen — the damage callouts now print the event's amount).
    /// </remarks>
    [Fact]
    public void test_each_creature_replays_to_exactly_where_the_sim_left_it()
    {
        var creatures = new List<WaveCreature>
        {
            WaveCreature.Single(90f, 4f), WaveCreature.Single(140f, 4f),
            WaveCreature.Single(60f, 4f), WaveCreature.Single(200f, 4f),
        };
        var pools = creatures.Select(c => c.MaxHealth).ToList();
        var champ = new Champion { MaxHealth = 400, Health = 400 };

        var (outcome, events) = SoloBattle.ResolveWave(
            champ, With("hammer_blow", "field_mire"), new Hunter(), creatures,
            enemyIntervalMs: 1000, ExpeditionTuning.Default, new Random(7), new WaveBonus());
        Assert.Equal(WaveOutcome.Cleared, outcome);
        Assert.True(events.Count(e => e.Kind == BattleEventKind.Strike) > 8, "a wave this size must take many strikes");

        var replay = new WaveReplay(events, new Dictionary<int, int> { [0] = 400 },
                                    new Dictionary<int, int> { [0] = 400 }, pools.Sum());
        replay.SetComposition(pools);

        // The ledger: what the EVENTS say each creature has taken so far.
        var ledger = pools.ToArray();
        var struck = new int[pools.Count];
        foreach (var at in events.Select(e => e.AtMs).Distinct().OrderBy(t => t))
        {
            foreach (var e in events.Where(e => e.AtMs == at))
            {
                if (e.Kind == BattleEventKind.Strike)
                {
                    Assert.InRange(e.Slot, 0, pools.Count - 1);
                    ledger[e.Slot] = MathF.Max(0f, ledger[e.Slot] - e.Amount);
                    struck[e.Slot]++;
                }
                // The kill is its own beat. A strike's amount is ROUNDED, so a 12.4 blow that finishes a
                // creature holding 12.3 arrives as "12" — the EnemyDown that follows it is what says the
                // creature is gone, and the replay (rightly) zeroes the bar on it.
                else if (e.Kind == BattleEventKind.EnemyDown) ledger[e.Slot] = 0f;
            }
            replay.Advance(at);
            for (var i = 0; i < pools.Count; i++)
                Assert.Equal(ledger[i] / pools[i], replay.CreatureHealthFraction(i), 3);
        }

        // And the end state agrees with the SIM, not just with the ledger: dead where it killed, and
        // within half a point per rounded strike everywhere else.
        for (var i = 0; i < pools.Count; i++)
        {
            var simHealth = MathF.Max(0f, creatures[i].Health);
            Assert.Equal(simHealth <= 0f, !replay.CreatureAlive(i));
            Assert.InRange(replay.CreatureHealthFraction(i) * pools[i],
                           simHealth - 0.5f * struck[i] - 0.01f, simHealth + 0.5f * struck[i] + 0.01f);
        }
    }

    /// <summary>
    /// The same property through the door the screen actually uses: <see cref="SoloExpedition.PushWave"/>
    /// on a swarm region, replaying <see cref="SoloExpedition.LastWaveEvents"/> over
    /// <see cref="SoloExpedition.LastWaveCreatures"/>. The event's Slot must index THAT list — a wave the
    /// sim re-minted (PLATED, NUMBERS) or reordered would put every strike on the wrong bar.
    /// </summary>
    [Fact]
    public void test_the_expedition_stamps_each_strike_with_the_index_of_the_creature_it_reports()
    {
        var champ = new Champion { MaxHealth = 5000, Health = 5000 };
        var run = new SoloExpedition(With("hammer_blow", "volley_spray"), champ, new Hunter(),
            enemyBaseHealth: 80f, enemyBaseDamage: 2f, ExpeditionTuning.Default, rng: new Random(11))
        { RegionId = "umbral_reach" };

        var multiCreatureWaves = 0;
        for (var wave = 0; wave < 8 && !run.Over; wave++)
        {
            var startHealth = champ.Health;
            run.PushWave();
            var comp = run.LastWaveCreatures;
            if (comp.Count > 1) multiCreatureWaves++;

            var replay = new WaveReplay(run.LastWaveEvents, new Dictionary<int, int> { [0] = startHealth },
                                        new Dictionary<int, int> { [0] = champ.MaxHealth }, comp.Sum(c => c.MaxHealth));
            replay.SetComposition(comp.Select(c => c.MaxHealth).ToList());
            replay.Advance(float.MaxValue);

            var struck = new int[comp.Count];
            foreach (var e in run.LastWaveEvents.Where(e => e.Kind == BattleEventKind.Strike))
            {
                Assert.InRange(e.Slot, 0, comp.Count - 1);
                struck[e.Slot]++;
            }
            for (var i = 0; i < comp.Count; i++)
            {
                var simHealth = MathF.Max(0f, comp[i].Health);
                Assert.Equal(simHealth <= 0f, !replay.CreatureAlive(i));
                Assert.InRange(replay.CreatureHealthFraction(i) * comp[i].MaxHealth,
                               simHealth - 0.5f * struck[i] - 0.01f, simHealth + 0.5f * struck[i] + 0.01f);
            }
        }
        Assert.True(multiCreatureWaves > 0, "the swarm region must have fielded at least one wave of several creatures");
    }
}
