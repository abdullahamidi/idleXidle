using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// <see cref="BattleEventKind.ReactionArmed"/> (JAWS, ADR-011, 2026-09-25): a Reaction's real ready moment, reported
/// by the fight so the rail can sweep a rearm without rebuilding it.
/// </summary>
/// <remarks>
/// Two promises. It REPORTS and never decides: the same wave resolved with the report on and off is the same fight,
/// event for event and value for value, apart from the reports themselves. And it is TRUE: every report lands where
/// the fight's own cooldown table says the reaction came due, whatever shaped that table (COILED, RECOIL, the action
/// rate, a cleared cooldown), which is checked against what the fight then does: an armed reaction answers the
/// very next bite, and a rearming one answers none.
/// </remarks>
public class reaction_armed_test
{
    private const int JawsSlot = 0;

    // ── fixtures ─────────────────────────────────────────────────────────────────────────────────

    private static Champion Fresh(int pool = 400_000) => new() { MaxHealth = pool, Health = pool };

    private static Build WithTrigger(Build b, BuildTrigger t)
    {
        b.Take(new Keystone { Id = $"t_{t}", Name = t.ToString(), Blurb = "test", Grants = new[] { t } });
        return b;
    }

    private static Build Equip(params EquippedSkill[] skills)
    {
        var b = new Build();
        foreach (var s in skills) b.Equip(s);
        return b;
    }

    private static List<WaveCreature> Pack(int count, float health, float damage)
        => Enumerable.Range(0, count).Select(_ => new WaveCreature { MaxHealth = health, Health = health, Damage = damage }).ToList();

    /// <summary>Every build shape the report has to stay silent about, named for the failure it would expose.</summary>
    public static IEnumerable<object[]> Battery()
    {
        yield return new object[] { "jaws" };
        yield return new object[] { "jaws_coiled" };
        yield return new object[] { "jaws_net_recoil" };
        yield return new object[] { "jaws_iron" };
        yield return new object[] { "jaws_loose_again" };
        yield return new object[] { "backdraw_loose_again" };
        yield return new object[] { "seeker_fight_build" };
        yield return new object[] { "no_reaction" };
        yield return new object[] { "jaws_champion_dies" };
    }

    private static (Build Build, Func<List<WaveCreature>> Pack, int BiteMs, int Pool) Case(string name) => name switch
    {
        "jaws" => (TestBuilds.Of("snare_jaws"), () => Pack(3, 9_000f, 40f), 1000, 400_000),
        "jaws_coiled" => (WithTrigger(TestBuilds.Of("snare_jaws"), BuildTrigger.Coiled), () => Pack(3, 9_000f, 40f), 1000, 400_000),
        "jaws_net_recoil" => (Equip(TestBuilds.Chosen("snare_jaws", "NET", null, "RECOIL")), () => Pack(4, 6_000f, 40f), 1300, 400_000),
        "jaws_iron" => (Equip(TestBuilds.Chosen("snare_jaws", "IRON")), () => Pack(2, 9_000f, 60f), 1000, 400_000),
        "jaws_loose_again" => (WithTrigger(TestBuilds.Of("snare_jaws", "volley_spray"), BuildTrigger.LooseAgain), () => Pack(6, 500f, 30f), 700, 400_000),
        "backdraw_loose_again" => (WithTrigger(TestBuilds.Of("sig_quiver_backdraw", "volley_spray"), BuildTrigger.LooseAgain), () => Pack(6, 700f, 30f), 900, 400_000),
        "seeker_fight_build" => (TestBuilds.Of("sig_seeker_hard_hands", "volley_spray", "hammer_press", "snare_jaws"), () => Pack(4, 4_000f, 50f), 1000, 400_000),
        "no_reaction" => (TestBuilds.Of("volley_spray", "hammer_press"), () => Pack(4, 3_000f, 50f), 1000, 400_000),
        "jaws_champion_dies" => (TestBuilds.Of("snare_jaws"), () => Pack(3, 90_000f, 400f), 1000, 1_000),
        _ => throw new ArgumentException(name),
    };

    /// <summary>Three consecutive waves on one champion, so a rearm carried over a boundary is exercised too.</summary>
    private static (List<(WaveOutcome Outcome, List<BattleEvent> Events, string Metrics)> Waves, Champion Champ) Run(string name, bool report)
    {
        var (build, pack, biteMs, pool) = Case(name);
        var champ = Fresh(pool);
        var rng = new Random(11);
        var tuning = ExpeditionTuning.Default with { TickCeilingMs = 20_000 };
        var waves = new List<(WaveOutcome, List<BattleEvent>, string)>();
        for (var w = 0; w < 3 && champ.Alive; w++)
        {
            var metrics = new WaveMetrics();
            var (outcome, events) = SoloBattle.ResolveWave(champ, build, new Hunter(), pack(), biteMs, tuning, rng,
                                                           metrics: metrics, reportReadiness: report);
            waves.Add((outcome, events, Snapshot(metrics)));
        }
        return (waves, champ);
    }

    /// <summary>Every public property of <paramref name="o"/>, dictionaries sorted: a value-for-value fingerprint.</summary>
    private static string Snapshot(object o)
    {
        var sb = new StringBuilder();
        foreach (var p in o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
        {
            if (p.GetIndexParameters().Length > 0) continue;
            var v = p.GetValue(o);
            sb.Append(p.Name).Append('=');
            if (v is IDictionary d)
                foreach (var k in d.Keys.Cast<object>().OrderBy(k => k.ToString())) sb.Append(k).Append(':').Append(d[k]).Append(',');
            else sb.Append(v);
            sb.Append(';');
        }
        return sb.ToString();
    }

    // ── 1. IT REPORTS AND NEVER DECIDES ─────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Battery))]
    public void test_the_readiness_report_changes_nothing_the_fight_decides(string name)
    {
        var (on, champOn) = Run(name, report: true);
        var (off, champOff) = Run(name, report: false);

        Assert.Equal(off.Count, on.Count);
        for (var w = 0; w < on.Count; w++)
        {
            Assert.Equal(off[w].Outcome, on[w].Outcome);
            Assert.Empty(off[w].Events.Where(e => e.Kind == BattleEventKind.ReactionArmed));
            // event for event, in order: the reports are the ONLY difference
            Assert.Equal(off[w].Events, on[w].Events.Where(e => e.Kind != BattleEventKind.ReactionArmed).ToList());
            Assert.Equal(off[w].Metrics, on[w].Metrics);
            // the wave's length is its last event: a report never lengthens it
            Assert.Equal(off[w].Events.Max(e => e.AtMs), on[w].Events.Max(e => e.AtMs));
        }
        Assert.Equal(champOff.Health, champOn.Health);
        Assert.Equal(champOff.ElapsedMs, champOn.ElapsedMs);
        Assert.Equal(champOff.BeatCount, champOn.BeatCount);
        Assert.Equal(champOff.CurrentShield, champOn.CurrentShield);
        Assert.Equal(champOff.ReadyAt.OrderBy(kv => kv.Key), champOn.ReadyAt.OrderBy(kv => kv.Key));
        Assert.Equal(champOff.ReadyAtBeat.OrderBy(kv => kv.Key), champOn.ReadyAtBeat.OrderBy(kv => kv.Key));
    }

    [Theory]
    [MemberData(nameof(Battery))]
    public void test_the_reports_keep_the_event_list_in_time_order(string name)
    {
        var (waves, _) = Run(name, report: true);
        foreach (var (_, events, _) in waves)
            for (var i = 1; i < events.Count; i++)
                Assert.True(events[i].AtMs >= events[i - 1].AtMs,
                            $"{name}: {events[i].Kind} at {events[i].AtMs} follows {events[i - 1].Kind} at {events[i - 1].AtMs}");
    }

    [Fact]
    public void test_a_build_with_no_reaction_reports_nothing()
    {
        var (waves, champ) = Run("no_reaction", report: true);
        Assert.All(waves, w => Assert.DoesNotContain(w.Events, e => e.Kind == BattleEventKind.ReactionArmed));
        Assert.Empty(champ.ReactionRearmFrom);
    }

    // ── 2. IT IS TRUE ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The report against the fight's own behaviour, for a bite-answering reaction in <paramref name="slot"/>: every
    /// trigger opens a rearm, every report closes the last one and dates its start to that trigger, no trigger
    /// happens while rearming, and once armed the reaction answers the very next bite.
    /// </summary>
    private static void AssertAgreesWithTheFight(List<BattleEvent> events, int slot, string label)
    {
        int? open = null;
        var armedAt = (int?)null;   // armed and waiting since this ms (null while rearming)
        var everTriggered = false;
        for (var i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.Skill && e.Slot == slot)
            {
                Assert.True(open is null, $"{label}: triggered at {e.AtMs} while still rearming from {open}");
                open = e.AtMs;
                armedAt = null;
                everTriggered = true;
            }
            else if (e.Kind == BattleEventKind.ReactionArmed && e.Slot == slot)
            {
                if (open is { } t) Assert.Equal(t, e.AtMs - e.Amount);   // the rearm is dated to its own trigger
                else Assert.True(e.AtMs - e.Amount <= 0, $"{label}: a report at {e.AtMs} closes no rearm of this wave");
                open = null;
                armedAt = e.AtMs;
            }
            else if (e.Kind == BattleEventKind.EnemyStrike && armedAt is { } since && e.AtMs >= since)
            {
                // ARMED means the fight answers this bite: the trigger follows at the same millisecond
                var answered = events.Skip(i + 1).TakeWhile(x => x.AtMs == e.AtMs)
                                     .Any(x => x.Kind == BattleEventKind.Skill && x.Slot == slot);
                Assert.True(answered, $"{label}: reported armed since {since} but the bite at {e.AtMs} went unanswered");
            }
        }
        Assert.True(everTriggered, $"{label}: the fixture never triggered the reaction");
    }

    [Theory]
    [InlineData("jaws")]
    [InlineData("jaws_coiled")]
    [InlineData("jaws_net_recoil")]
    [InlineData("jaws_iron")]
    [InlineData("jaws_loose_again")]
    [InlineData("seeker_fight_build")]
    public void test_every_report_agrees_with_when_the_fight_lets_the_reaction_answer(string name)
    {
        var slot = name == "seeker_fight_build" ? 3 : JawsSlot;
        var (waves, _) = Run(name, report: true);
        foreach (var (w, i) in waves.Select((w, i) => (w, i)))
            AssertAgreesWithTheFight(w.Events, slot, $"{name} wave {i + 1}");
    }

    /// <summary>The length of every whole rearm the fight reported for JAWS in the first wave of <paramref name="name"/>.</summary>
    private static List<int> Rearms(string name)
        => Run(name, report: true).Waves[0].Events
               .Where(e => e.Kind == BattleEventKind.ReactionArmed && e.Slot == JawsSlot && e.AtMs - e.Amount >= 0)
               .Select(e => e.Amount).ToList();

    [Fact]
    public void test_a_plain_jaws_report_is_the_fights_rearm_not_the_cards_three_seconds()
    {
        // This fixture's champion acts at a rate a little over 1, so the fight rearms JAWS a little SOONER than the
        // card's 3 s, and it comes due between two bites: a real armed window that "last trigger + RearmMs" would
        // have drawn as none. The report carries the fight's own number.
        var rearms = Rearms("jaws");
        Assert.NotEmpty(rearms);
        Assert.All(rearms, ms => Assert.True(ms is > 2_000 and < 3_000, $"rearm {ms}"));
        Assert.Single(rearms.Distinct());
        var events = Run("jaws", report: true).Waves[0].Events;
        var armed = events.First(e => e.Kind == BattleEventKind.ReactionArmed && e.AtMs - e.Amount >= 0);
        var answer = events.First(e => e.Kind == BattleEventKind.Skill && e.Slot == JawsSlot && e.AtMs >= armed.AtMs);
        Assert.True(answer.AtMs > armed.AtMs, "armed and waiting for the next bite");
    }

    [Theory]
    [InlineData("jaws_coiled", 0.5f)]       // COILED halves the trap's rearm
    [InlineData("jaws_net_recoil", 0.66f)]  // RECOIL: CooldownMultiplier 0.66
    [InlineData("jaws_iron", 2f)]           // IRON: RearmMs 6000 against the base 3000
    public void test_the_report_follows_every_cooldown_modifier(string name, float factor)
    {
        var plain = Rearms("jaws")[0];
        var rearms = Rearms(name);
        Assert.NotEmpty(rearms);
        // the fight truncates to whole ms at each step, so one ms of slack either way
        Assert.All(rearms, ms => Assert.InRange(ms, (int)(plain * factor) - 2, (int)(plain * factor) + 2));
    }

    /// <summary>
    /// A JAWS whose rearm comes due exactly ON a bite: the zero-ms window the discovery measured in the game. The
    /// champion's action rate decides where the ready moment falls, so the fixture looks for the card rearm that
    /// puts it on a bite tick rather than guessing one.
    /// </summary>
    private static List<BattleEvent> ZeroWindowWave()
    {
        for (var rearm = 3_000; rearm <= 3_400; rearm += 10)
        {
            var r = rearm;
            var build = new Build();
            build.Equip(TestBuilds.Skill("snare_jaws", tweak: d => d with { RearmMs = r }));
            var (_, events) = SoloBattle.ResolveWave(Fresh(), build, new Hunter(), Pack(3, 90_000f, 40f), 1000,
                                                     ExpeditionTuning.Default with { TickCeilingMs = 12_000 }, new Random(7));
            if (events.Any(a => a.Kind == BattleEventKind.ReactionArmed
                                && events.Any(s => s.Kind == BattleEventKind.Skill && s.Slot == JawsSlot && s.AtMs == a.AtMs)))
                return events;
        }
        throw new InvalidOperationException("no card rearm between 3.0 and 3.4 s lands on a bite");
    }

    [Fact]
    public void test_armed_and_the_trigger_on_one_millisecond_come_armed_first()
    {
        var events = ZeroWindowWave();
        var sameMs = 0;
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i].Kind != BattleEventKind.ReactionArmed) continue;
            var trigger = events.FindIndex(e => e.Kind == BattleEventKind.Skill && e.Slot == JawsSlot && e.AtMs == events[i].AtMs);
            if (trigger < 0) continue;
            sameMs++;
            Assert.True(i < trigger, $"the report at {events[i].AtMs} came after its own trigger");
            // ...and before the bite it answers
            var bite = events.FindIndex(e => e.Kind == BattleEventKind.EnemyStrike && e.AtMs == events[i].AtMs);
            Assert.True(bite < 0 || i < bite);
        }
        Assert.True(sameMs > 0, "the fixture never posed a zero-ms ready window");
    }

    [Fact]
    public void test_a_ready_window_between_bites_is_reported_at_its_own_moment()
    {
        // bites every 1300 against a 3000 rearm: ready 3000 after a trigger, 900 ms before the bite that answers
        var champ = Fresh();
        var (_, events) = SoloBattle.ResolveWave(champ, TestBuilds.Of("snare_jaws"), new Hunter(), Pack(2, 90_000f, 40f), 1300,
                                                 ExpeditionTuning.Default with { TickCeilingMs = 12_000 }, new Random(3));
        var report = events.First(e => e.Kind == BattleEventKind.ReactionArmed && e.AtMs - e.Amount >= 0);
        var nextTrigger = events.First(e => e.Kind == BattleEventKind.Skill && e.Slot == JawsSlot && e.AtMs > report.AtMs - report.Amount);
        Assert.NotEqual(0, report.AtMs % 1300);   // said at its own moment, not at a bite
        Assert.True(nextTrigger.AtMs > report.AtMs, "the window between ready and the next bite must be real");
        Assert.DoesNotContain(events, e => e.Kind == BattleEventKind.EnemyStrike && e.AtMs >= report.AtMs && e.AtMs < nextTrigger.AtMs);
    }

    [Fact]
    public void test_a_cleared_cooldown_arms_the_reaction_when_it_was_cleared()
    {
        // LOOSE AGAIN readies every skill on a death: JAWS is armed at that death, not at trigger + 3 s
        var (waves, _) = Run("jaws_loose_again", report: true);
        var events = waves[0].Events;
        // a full rearm runs the longest; one CUT SHORT was pulled forward by the clear
        var whole = events.Where(e => e.Kind == BattleEventKind.ReactionArmed && e.AtMs - e.Amount >= 0).ToList();
        var early = whole.Where(e => e.Amount < whole.Max(x => x.Amount)).ToList();
        Assert.NotEmpty(early);
        Assert.All(early, r => Assert.Contains(events, e => e.Kind == BattleEventKind.EnemyDown && e.AtMs == r.AtMs));
    }

    [Fact]
    public void test_a_rearm_that_outlives_its_wave_is_reported_by_the_next_wave()
    {
        var champ = Fresh();
        var build = TestBuilds.Of("snare_jaws");
        var tuning = ExpeditionTuning.Default with { TickCeilingMs = 20_000 };
        var rng = new Random(5);
        // a short wave: a weak pack the swing clears quickly, while a rearm is still running
        var (_, first) = SoloBattle.ResolveWave(champ, build, new Hunter(), Pack(1, 2_600f, 40f), 1000, tuning, rng);
        var lastTrigger = first.Last(e => e.Kind == BattleEventKind.Skill && e.Slot == JawsSlot);
        Assert.DoesNotContain(first, e => e.Kind == BattleEventKind.ReactionArmed && e.AtMs > lastTrigger.AtMs);
        var firstStart = champ.ElapsedMs - first.Max(e => e.AtMs);
        Assert.True(champ.ReactionRearmFrom.ContainsKey(JawsSlot), "the fixture must end the wave mid-rearm");
        Assert.Equal(firstStart + lastTrigger.AtMs, champ.ReactionRearmFrom[JawsSlot]);

        var secondStart = champ.ElapsedMs;
        var (_, second) = SoloBattle.ResolveWave(champ, build, new Hunter(), Pack(1, 90_000f, 40f), 1000, tuning, rng);
        var carried = second.First(e => e.Kind == BattleEventKind.ReactionArmed && e.Slot == JawsSlot);
        // it began in the previous wave, at that wave's last trigger
        Assert.True(carried.AtMs - carried.Amount < 0);
        Assert.Equal(firstStart + lastTrigger.AtMs, secondStart + carried.AtMs - carried.Amount);
    }

    [Fact]
    public void test_a_killing_bite_still_answers_and_the_report_stays_true()
    {
        var (waves, champ) = Run("jaws_champion_dies", report: true);
        Assert.False(champ.Alive);
        var last = waves[^1].Events;
        var down = last.First(e => e.Kind == BattleEventKind.Down);
        // JAWS answers the bite that kills him, before he falls (Core order, unchanged)
        var answer = last.FindIndex(e => e.Kind == BattleEventKind.Skill && e.Slot == JawsSlot && e.AtMs == down.AtMs);
        Assert.True(answer >= 0 && answer < last.IndexOf(down));
        AssertAgreesWithTheFight(last, JawsSlot, "the fatal wave");
    }

    // ── 3. THE REPLAY READS IT ──────────────────────────────────────────────────────────────────

    private static WaveReplay Replay(params BattleEvent[] events)
        => new(events, new Dictionary<int, int> { [0] = 100 }, new Dictionary<int, int> { [0] = 100 }, 100f);

    private static BattleEvent Trigger(int at) => new(BattleEventKind.Skill, 3, 0, at);
    private static BattleEvent Armed(int at, int ranMs) => new(BattleEventKind.ReactionArmed, 3, ranMs, at);

    [Fact]
    public void test_the_replay_sweeps_the_rearm_the_fight_reported()
    {
        var r = Replay(Trigger(1000), Armed(4000, 3000), Trigger(4000), Armed(7000, 3000), Trigger(8000));
        Assert.False(r.ReactionReadinessAt(500, 3).IsRearming);                  // armed before its first bite
        Assert.Equal(0f, r.ReactionReadinessAt(1000, 3).Progress);               // the trigger starts the sweep
        Assert.Equal(0.5f, r.ReactionReadinessAt(2500, 3).Progress, 3);
        var zero = r.ReactionReadinessAt(4000, 3);                                // armed and answered at once:
        Assert.True(zero.IsRearming);                                             // truthfully rearming again
        Assert.Equal(0f, zero.Progress);
        Assert.False(r.ReactionReadinessAt(7500, 3).IsRearming);                 // a real armed window
        Assert.Equal(7000, r.ReactionReadinessAt(6999, 3).ReadyMs);
    }

    [Fact]
    public void test_the_replay_reads_a_rearm_carried_in_from_the_last_wave()
    {
        var r = Replay(Armed(700, 1500), Trigger(1200));
        var at0 = r.ReactionReadinessAt(0, 3);
        Assert.True(at0.IsRearming);
        Assert.Equal(-800, at0.FromMs);
        Assert.Equal(800f / 1500f, at0.Progress, 3);
        Assert.False(r.ReactionReadinessAt(900, 3).IsRearming);
    }

    [Fact]
    public void test_the_wave_s_last_trigger_sweeps_to_the_fight_s_pending_moment_or_reads_armed()
    {
        var r = Replay(Trigger(9000));
        var pending = r.ReactionReadinessAt(10_500, 3, pendingReadyMs: 12_000);
        Assert.True(pending.IsRearming);
        Assert.Equal(0.5f, pending.Progress, 3);
        // no pending rearm: a reaction that never rearms (no number of its own) is simply armed
        Assert.False(r.ReactionReadinessAt(10_500, 3, pendingReadyMs: null).IsRearming);
    }

    [Fact]
    public void test_the_replay_ignores_other_slots_and_creature_strikes()
    {
        // a Strike on CREATURE 3 is not skill slot 3's trigger
        var r = Replay(new BattleEvent(BattleEventKind.Strike, 3, 40, 500, HitSource.Swing), Trigger(1000), Armed(4000, 3000),
                       new BattleEvent(BattleEventKind.ReactionArmed, 1, 900, 2000));
        Assert.False(r.ReactionReadinessAt(700, 3).IsRearming);
        Assert.True(r.ReactionReadinessAt(2500, 3).IsRearming);
    }
}
