using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using Xunit;
using Xunit.Abstractions;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// "EVERY FOURTH ACTION" must mean every fourth action — in the sim, and on the dial that reports it.
/// </summary>
/// <remarks>
/// <para>
/// Playtest 2026-08-28: <i>"it says 4 but it casts on the 4th hit."</i> The rail read "EVERY 4 ACTIONS",
/// which a player hears as four actions of WAITING and then a cast — five actions to the cycle. The rule
/// is the other one: <c>ReadyAtBeat = BeatCount + beats</c>, so the cast IS the fourth action and the
/// cycle is cast, hit, hit, hit, cast. The wording was corrected to the ordinal (the label for 2 and 3
/// was already "EVERY OTHER ACTION" / "EVERY THIRD ACTION"); the rule was deliberately left alone.
/// </para>
/// <para>
/// <b>The point of this file is that the rule and the readout are pinned TOGETHER.</b> They were two
/// independent pieces of arithmetic — <c>SoloBattle</c>'s beat counter and <c>WaveReplay</c>'s
/// <see cref="WaveReplay.ActionsBetween"/> — agreeing by hand, and a disagreement between them is
/// invisible in a test that only checks one of them. It shows up as a player counting swings and getting
/// a different answer from the ring on the medallion.
/// </para>
/// </remarks>
public class BeatCadenceTest
{
    private readonly ITestOutputHelper _out;

    public BeatCadenceTest(ITestOutputHelper output) => _out = output;

    /// <summary>One beat-counted skill and nothing else, so every cast in the stream is that skill's.</summary>
    private static Build OneRhythmSkill(Form form)
    {
        var b = new Build();
        b.Weave(new EquippedSkill(
            new WovenAbility { Name = form.ToString(), Source = Source.Body, Form = form },
            FormBehaviour.BaseCooldownMs(form)));
        return b;
    }

    /// <summary>
    /// A wave long enough to hold many casts: one creature with a great deal of health, biting for
    /// nothing, against a champion that cannot die. What is being measured is the CADENCE, and a fight
    /// that ends early measures one cycle of it.
    /// </summary>
    private static List<BattleEvent> LongWave(Form form)
    {
        var champ = new Champion { MaxHealth = 5_000_000, Health = 5_000_000 };
        var (_, events) = SoloBattle.ResolveWave(
            champ, OneRhythmSkill(form), new Hunter(),
            enemyHealth: 400_000f, enemyDamage: 0f, enemyIntervalMs: 1_500,
            ExpeditionTuning.Default, new Random(11));
        return events;
    }

    /// <summary>
    /// Every ACTION in the stream, in order — the same definition the dial counts by.
    /// </summary>
    /// <remarks>
    /// A cast, or an auto-swing that is not a skill's own hit. Deliberately re-derived here from the raw
    /// events rather than borrowed from <see cref="WaveReplay"/>, so this file can catch the replay
    /// drifting rather than agree with it by construction.
    /// </remarks>
    private static List<int> ActionTimes(IEnumerable<BattleEvent> events)
    {
        var times = new List<int>();
        foreach (var e in events)
        {
            var isAction = (e.Kind == BattleEventKind.Skill && (Form)e.Amount != Form.Trap)
                           || (e.Kind == BattleEventKind.Strike && !e.FromSkill);
            if (!isAction) continue;
            if (times.Count > 0 && times[^1] == e.AtMs) continue;
            times.Add(e.AtMs);
        }

        return times;
    }

    [Theory]
    [InlineData(Form.Strike)]        // six
    [InlineData(Form.Projectile)]    // four
    public void test_a_rhythm_skill_casts_on_exactly_every_nth_action(Form form)
    {
        var beats = FormBehaviour.CooldownBeats(form);
        var events = LongWave(form);
        var actions = ActionTimes(events);
        var castMs = events.Where(e => e.Kind == BattleEventKind.Skill).Select(e => e.AtMs).Distinct().ToList();

        // The INDEX of each cast in the action stream — which action of the fight it was.
        var castIndices = castMs.Select(ms => actions.IndexOf(ms)).ToList();

        _out.WriteLine($"{form} waits {beats} actions. {actions.Count} actions, {castIndices.Count} casts.");
        _out.WriteLine($"   cast on action #: {string.Join(", ", castIndices.Take(12))}");

        Assert.True(castIndices.Count >= 4, $"only {castIndices.Count} casts — too few to read a cadence from");
        Assert.DoesNotContain(-1, castIndices);

        // THE CLAIM. Consecutive casts sit exactly `beats` actions apart: cast, then beats-1 plain
        // actions, then the next cast. Not beats+1 — that is the reading the label used to invite.
        var gaps = castIndices.Zip(castIndices.Skip(1), (a, b) => b - a).ToList();
        Assert.All(gaps, g => Assert.Equal(beats, g));
    }

    [Theory]
    [InlineData(Form.Strike)]
    [InlineData(Form.Projectile)]
    public void test_the_dial_reads_full_on_the_action_the_skill_casts_on(Form form)
    {
        // THE READOUT, against the same wave the rule was read from. The rail fills the ring with
        // ActionsBetween(previous cast, now) / beats, so "full" must land on the casting action and on
        // no action before it — the readout and the rule are one claim, checked in one place.
        var beats = FormBehaviour.CooldownBeats(form);
        var events = LongWave(form);
        var replay = new WaveReplay(events, new Dictionary<int, int> { [0] = 100 }, new Dictionary<int, int> { [0] = 100 }, 400_000f);
        var actions = ActionTimes(events);
        var castMs = events.Where(e => e.Kind == BattleEventKind.Skill).Select(e => e.AtMs).Distinct().ToList();

        // Between one cast and the next, walk every action and read the dial the way the rail does.
        for (var c = 0; c + 1 < Math.Min(castMs.Count, 5); c++)
        {
            var from = castMs[c];
            var next = castMs[c + 1];
            foreach (var at in actions.Where(a => a > from && a <= next))
            {
                var fill = Math.Clamp(replay.ActionsBetween(from, at) / (float)beats, 0f, 1f);
                if (at == next)
                    Assert.True(fill >= 1f,
                        $"{form}'s dial read {fill:P0} on the action it cast on — the ring is not full "
                        + "when the skill fires, so the player is told to expect one more action.");
                else
                    Assert.True(fill < 1f,
                        $"{form}'s dial was already full {(next - at)}ms before the cast — it reads "
                        + "ready while the skill still has an action to wait.");
            }
        }

        _out.WriteLine($"{form}: the ring fills across {beats} actions and is full exactly on the cast.");
    }

    /// <summary>
    /// The rail's own arithmetic, re-implemented here because the Game assembly has no test project.
    /// </summary>
    /// <remarks>
    /// SoloExpeditionScreen fills a skill's ring with <c>ActionsBetween(last cast, now) / beats</c>, and
    /// since 2026-08-28 it adds a CARRY when the wave being replayed holds no cast of its own — the
    /// actions that passed in earlier waves. What is pinned here is that carry's arithmetic, which is
    /// the part that can drift; the screen's wiring of it cannot be reached from here.
    /// </remarks>
    private static (int Carry, float Ms) Fold(WaveReplay replay, int endMs, int carry, float carryMs,
                                              Source src, Form form)
    {
        var last = replay.LastSkillBefore(endMs + 1, (int)src, (int)form);
        return last >= 0
            ? (replay.ActionsBetween(last, endMs + 1), endMs - last)
            : (carry + replay.ActionsBetween(-1, endMs + 1), carryMs + endMs);
    }

    [Fact]
    public void test_a_skills_cycle_does_not_restart_when_a_wave_does()
    {
        // THE COMPLAINT (playtest 2026-08-28): "wave başlayınca skiller resetlenmesin, aynı akışında
        // devam etsin." The SIM was already continuous — Champion.BeatCount never resets — but every
        // wave gets a fresh WaveReplay that knows only its own events, so the readout wound back to
        // empty on a skill that was four actions into a six-action cycle. Worse, a skill could sit out
        // a whole short wave (its ring crawling up from zero) and then cast on the FIRST action of the
        // next one, which reads as random.
        var beats = FormBehaviour.CooldownBeats(Form.Projectile);
        var build = OneRhythmSkill(Form.Projectile);
        var hunter = new Hunter();
        var pool = SoloBattle.ChampionHealth(build, hunter) * 200;   // survives; the cadence is the subject
        var champ = new Champion { MaxHealth = pool, Health = pool };
        var run = new SoloExpedition(build, champ, hunter, 110f, 1f,
                                     ExpeditionTuning.Default, Source.Nature, new Random(5));

        var carry = 0;
        var carryMs = 0f;
        var wavesWithoutACast = 0;
        var openings = new List<float>();

        for (var w = 0; w < 6; w++)
        {
            run.RefreshPool();
            run.PushWave();
            var events = run.LastWaveEvents;
            if (events.Count == 0) break;
            var endMs = events.Max(e => e.AtMs);
            var replay = new WaveReplay(events, new Dictionary<int, int> { [0] = 100 },
                                        new Dictionary<int, int> { [0] = 100 }, 400_000f);

            // THE RING AT THIS WAVE'S OPENING, read the way the rail reads it: no cast yet in this
            // replay, so the whole value is the carry.
            var opening = Math.Clamp(carry / (float)beats, 0f, 1f);
            openings.Add(opening);

            var cast = replay.LastSkillBefore(endMs + 1, (int)Source.Body, (int)Form.Projectile);
            if (cast < 0) wavesWithoutACast++;

            _out.WriteLine($"wave {run.Wave}: opens with the ring at {opening:P0} "
                           + $"({carry} of {beats} actions carried), "
                           + (cast < 0 ? "no cast this wave" : $"cast at {cast}ms"));

            (carry, carryMs) = Fold(replay, endMs, carry, carryMs, Source.Body, Form.Projectile);
        }

        // THE CLAIM. Some wave must open mid-cycle — otherwise this fixture never exercises the carry
        // and the assertion below is vacuous.
        Assert.Contains(openings.Skip(1), o => o > 0f);

        // And the carry must survive a wave the skill sat out entirely: that is the case that used to
        // re-zero the ring, and the case a short wave produces most often.
        Assert.True(wavesWithoutACast > 0,
            "no wave passed without a cast, so the fixture never reached the case the carry exists for");
    }

    [Fact]
    public void test_a_wave_the_skill_sits_out_adds_to_the_carry_rather_than_replacing_it()
    {
        // The fold has two branches and only one is obvious. When the skill DID cast, the carry is what
        // has happened since. When it did NOT, the wave's whole length must be ADDED to what was already
        // carried — overwriting there would re-zero a skill every time a wave passed without it, which
        // is the same bug one level down.
        var events = LongWave(Form.Strike);
        var end = events.Max(e => e.AtMs);
        var replay = new WaveReplay(events, new Dictionary<int, int> { [0] = 100 },
                                    new Dictionary<int, int> { [0] = 100 }, 400_000f);

        // A Form this wave never cast: the fold must fall to the adding branch.
        var (carry, ms) = Fold(replay, end, carry: 3, carryMs: 900f, Source.Body, Form.Projectile);
        var actions = ActionTimes(events).Count;

        _out.WriteLine($"a wave of {actions} actions with no Projectile cast: carry 3 -> {carry}, ms 900 -> {ms}");

        Assert.Equal(3 + actions, carry);
        Assert.Equal(900f + end, ms);
    }

    [Fact]
    public void test_the_two_counters_agree_about_what_an_action_is()
    {
        // The dial's counter lives in WaveReplay and the cadence lives in SoloBattle. They agree only by
        // hand, and a Trap is the case that proves it matters: it fires on being BITTEN, off the beat,
        // so it is not an action and must not turn the ring (review 2026-08-30). Anything new that
        // raises a Skill event has to make the same decision in both places.
        var events = LongWave(Form.Strike);
        var replay = new WaveReplay(events, new Dictionary<int, int> { [0] = 100 }, new Dictionary<int, int> { [0] = 100 }, 400_000f);
        var mine = ActionTimes(events);

        var last = mine[^1];
        Assert.Equal(mine.Count, replay.ActionsBetween(-1, last));
    }
}
