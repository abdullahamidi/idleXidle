using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Builds;

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
/// <b>The point of this file is that the rule and the readout are pinned TOGETHER.</b> They used to be
/// two independent pieces of arithmetic — <c>SoloBattle</c>'s beat counter, and a readout that rebuilt
/// its own count by walking the damage stream — agreeing by hand. A disagreement between them is
/// invisible in a test that checks only one, and it showed up as a player counting swings and getting a
/// different answer from the ring. The sim publishes its beat now
/// (<see cref="BattleEventKind.Beat"/>); what is pinned here is that the readout reads it.
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
        // THE READOUT, against the same wave the rule was read from, and read through the SAME helper
        // the rail's own arithmetic is mirrored in.
        //
        // THIS TEST USED TO MEASURE SOMETHING THE SCREEN NEVER DRAWS. It pinned `from` to the previous
        // cast by hand and asserted the fill was full AT the next one — but the rail gets `from` from
        // LastSkillBefore, which at that instant has already returned the new cast, so the value it
        // asserted on was never on screen for a single frame. It passed while the ring the player
        // actually watched was one notch short at every cast (playtest 2026-08-28). A test that
        // computes its own expression instead of the one under test proves only that arithmetic works.
        var events = LongWave(form);
        var replay = new WaveReplay(events, new Dictionary<int, int> { [0] = 100 },
                                    new Dictionary<int, int> { [0] = 100 }, 400_000f);
        var actions = ActionTimes(events);
        var castMs = events.Where(e => e.Kind == BattleEventKind.Skill).Select(e => e.AtMs).Distinct().ToList();

        foreach (var cast in castMs.Skip(1).Take(4))
        {
            var before = actions.Where(a => a < cast).ToList();
            var last = before[^1];

            Assert.True(Ring(replay, last, 0, Source.Body, form) >= 1f,
                $"{form}'s ring was short of full on the action before it cast — the player is shown a "
                + "skill still winding up and then watches it fire.");
            if (before.Count >= 2)
                Assert.True(Ring(replay, before[^2], 0, Source.Body, form) < 1f,
                    $"{form}'s ring was already full two actions before the cast — it reads as ready "
                    + "and refusing to fire.");
        }

        _out.WriteLine($"{form}: the ring fills across the cycle's plain actions and is full on the last "
                       + "one, with the cast as the action after.");
    }

    /// <summary>
    /// The rail's own arithmetic, re-implemented here because the Game assembly has no test project.
    /// </summary>
    /// <remarks>
    /// SoloExpeditionScreen fills a skill's ring with the BEATS since its last cast over the cycle's
    /// plain actions, and adds a CARRY when the wave being replayed holds no cast of its own — the beats
    /// that passed in earlier waves. What is pinned here is that arithmetic, which is the part that can
    /// drift; the screen's wiring of it cannot be reached from here.
    /// </remarks>
    private static (int Carry, float Ms) Fold(WaveReplay replay, int endMs, int carry, float carryMs,
                                              Source src, Form form)
    {
        var last = replay.LastSkillBefore(endMs + 1, (int)src, (int)form);
        var lastBeat = replay.LastBeat;
        if (lastBeat < 0) return (carry, carryMs);
        return last >= 0
            ? (lastBeat - replay.BeatAt(last), endMs - last)
            : (carry + (lastBeat - replay.FirstBeat + 1), carryMs + endMs);
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

            // THE RING AT THIS WAVE'S OPENING, read through the same helper the rail's arithmetic is
            // mirrored in, on the wave's first action.
            var firstAction = ActionTimes(events).FirstOrDefault(-1);
            var opening = firstAction < 0 ? 0f : Ring(replay, firstAction, carry, Source.Body, Form.Projectile);
            openings.Add(opening);

            var cast = replay.LastSkillBefore(endMs + 1, (int)Source.Body, (int)Form.Projectile);
            if (cast < 0) wavesWithoutACast++;

            _out.WriteLine($"wave {run.Wave}: opens with the ring at {opening:P0} "
                           + $"({carry} of {beats} actions carried), "
                           + (cast < 0 ? "no cast this wave" : $"cast at {cast}ms"));

            (carry, carryMs) = Fold(replay, endMs, carry, carryMs, Source.Body, Form.Projectile);
        }

        // THE CLAIM. Some wave must open MID-CYCLE — a ring part-filled by beats that happened in an
        // earlier wave. Before the carry existed every wave opened at zero, so this is the whole of it.
        Assert.Contains(openings.Skip(1), o => o > 0f);

        // The "sat a whole wave out" branch of the fold is pinned by its own test below. It used to be
        // asserted here as well, on the theory that this fixture reached it — and it did, only because
        // the cadence was drifting a beat per wave. With the drift fixed every wave holds a cast, so
        // demanding one that does not would be demanding the bug back.
        _out.WriteLine($"waves the skill sat out entirely: {wavesWithoutACast}");
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

    /// <summary>A real loadout: four skills competing for one action per beat.</summary>
    /// <summary>
    /// The loadout a real player carries: two actives and two passives, through the composer.
    /// </summary>
    /// <remarks>
    /// It used to weave four skills into a bare <see cref="Build"/>, which after the slot rework is a
    /// state no player can reach — the composer divides the budget, and four beat-taking skills
    /// contended so hard that a Projectile could wait six actions and the ring sat full for two of
    /// them. That is a true observation about a build the game no longer produces, and pinning a
    /// READOUT against an unreachable fixture is how a test outlives the thing it was protecting.
    /// </remarks>
    private static Build FourSkillLoadout()
        => BuildComposer.Compose(
            new MemoryDustTree(), Taught.Everything(), character: null,
            skills: new[]
            {
                new BuildComposer.SkillPick(Source.Body, Form.Strike, null, "Strike"),
                new BuildComposer.SkillPick(Source.Mind, Form.Projectile, null, "Projectile"),
                new BuildComposer.SkillPick(Source.Nature, Form.Aura, null, "Aura"),
                new BuildComposer.SkillPick(Source.Spirit, Form.Mark, null, "Mark"),
            },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4);

    /// <summary>The ring, read exactly the way SoloExpeditionScreen reads it.</summary>
    private static float Ring(WaveReplay replay, float playhead, int carry, Source src, Form form)
    {
        var prev = replay.LastSkillBefore(playhead, (int)src, (int)form);
        var from = prev >= 0 ? prev : -1;
        var carried = prev >= 0 ? 0 : carry;
        var beatNow = replay.BeatAt(playhead);
        var since = prev >= 0
            ? Math.Max(0, beatNow - replay.BeatAt(prev))
            : Math.Max(0, beatNow - replay.FirstBeat + 1) + carried;
        // THE PLAIN ACTIONS IN THIS CYCLE, one fewer than the cooldown because the next cast is itself
        // an action. See the test below.
        var span = Math.Max(1, FormBehaviour.CooldownBeats(form) - 1);
        return Math.Clamp(since / (float)span, 0f, 1f);
    }

    [Fact]
    public void test_the_ring_is_never_short_of_full_on_the_action_a_skill_casts_on()
    {
        // THE BUG (playtest 2026-08-28): "Projectile kullanıyorum, bazen 3 vuruştan, bazen 4 vuruştan
        // sonra skill atıyor. Skill henüz cooldowndayken (3. tik) atıyor."
        //
        // The nominal rule is "every fourth action", and the rail divided by exactly that. The SIM
        // leaves a different gap in two real cases, and neither is a defect in the fight:
        //
        //   THE OPENING — on a run's first wave a slot has no ReadyAtBeat entry, so the default is
        //   min(1, beats-1) = 1: the skill may cast from the wave's SECOND beat. Traced on a four-skill
        //   loadout, the first Projectile lands on the third action, with a ring reading 3/4.
        //
        //   CONTENTION — one action per beat, so a ready skill that loses the beat to an earlier slot
        //   takes the next one. Measured gap: five actions, so the ring sat full for an action doing
        //   nothing, which reads as a skill that is ready and refuses to fire.
        //
        // A readout that reports a nominal rule instead of the fight is a readout that lies twice.
        var build = FourSkillLoadout();
        var hunter = new Hunter();
        var pool = SoloBattle.ChampionHealth(build, hunter);
        var champ = new Champion { MaxHealth = pool, Health = pool };
        var run = new SoloExpedition(build, champ, hunter, 110f, 9f,
                                     ExpeditionTuning.Default, Source.Nature, new Random(17));

        var carry = 0;
        var checkedCasts = 0;

        // TEN WAVES, from five. With two actives instead of four the champion clears a wave in fewer
        // actions, so five waves no longer hold the three casts this needs to prove anything.
        for (var w = 0; w < 10; w++)
        {
            run.RefreshPool();
            run.PushWave();
            var events = run.LastWaveEvents;
            if (events.Count == 0) break;
            var endMs = events.Max(e => e.AtMs);
            var replay = new WaveReplay(events, new Dictionary<int, int> { [0] = 100 },
                                        new Dictionary<int, int> { [0] = 100 }, 400_000f);

            var actions = ActionTimes(events);
            var casts = events.Where(e => e.Kind == BattleEventKind.Skill
                                          && e.Slot == (int)Source.Mind
                                          && (Form)e.Amount == Form.Projectile)
                              .Select(e => e.AtMs).Distinct().ToList();

            foreach (var cast in casts)
            {
                // THE ACTION IMMEDIATELY BEFORE THE CAST is where the ring has to be full: that is the
                // last frame the player sees before the skill goes off, and "the skill becomes active,
                // THEN it throws" is exactly what they said they expect. Reading the ring AT the cast
                // measures nothing — LastSkillBefore has already flipped to that cast and the ring is
                // correctly starting the next cycle, which is what the gold flash covers.
                var before = actions.Where(a => a < cast).ToList();
                if (before.Count == 0) continue;      // the wave opened on this cast; nothing to show yet
                checkedCasts++;

                var ring = Ring(replay, before[^1], carry, Source.Mind, Form.Projectile);
                _out.WriteLine($"wave {run.Wave}: cast at {cast}ms — the ring on the action before it "
                               + $"({before[^1]}ms) reads {ring:P0}");
                Assert.True(ring >= 1f,
                    $"wave {run.Wave}: the Projectile fired at {cast}ms while its ring still read "
                    + $"{ring:P0} on the action before. The player is told the skill is waiting and "
                    + "then watches it fire anyway.");

                // And it must not have been full any EARLIER than that, or the skill reads as ready
                // and refusing to fire — the other half of the same complaint.
                foreach (var earlier in before.Take(before.Count - 1))
                {
                    var prevCast = casts.LastOrDefault(c => c < earlier, -1);
                    // A WAVE'S FIRST CAST IS EXEMPT. Its cooldown was carried in from the previous
                    // wave, so the ring is legitimately already full when the wave opens — the skill
                    // is not refusing to fire, it is waiting for the wave to hand it an action. With
                    // two actives instead of four the champion clears a wave in fewer actions, so
                    // this case is now the common one rather than the rare one.
                    if (prevCast < 0) continue;
                    Assert.True(Ring(replay, earlier, carry, Source.Mind, Form.Projectile) < 1f,
                        $"wave {run.Wave}: the ring was already full at {earlier}ms but the skill did "
                        + $"not fire until {cast}ms — it reads as ready and refusing.");
                }
            }

            (carry, _) = Fold(replay, endMs, carry, 0f, Source.Mind, Form.Projectile);
        }

        Assert.True(checkedCasts >= 3, $"only {checkedCasts} casts seen — too few to prove anything");
    }

    [Fact]
    public void test_every_action_the_champion_takes_advances_the_beat_counter()
    {
        // THE DEFECT, and it is the one the playtest kept running into: "bazen 3 vuruştan, bazen 4
        // vuruştan sonra skill atıyor."
        //
        // Champion.BeatCount is what every rhythm cooldown is measured against — ReadyAtBeat[i] =
        // BeatCount + beats — so it has to mean "actions the champion has taken". It did not. The beat's
        // bookkeeping sits at the FOOT of the tick, after six `return Kill(ms)` sites, and every wave
        // ends on one of them: the blow that kills the last creature returns before the counter moves.
        //
        // One lost beat per wave, cumulative and invisible. A skill that cast just before a boundary
        // then needed FIVE real actions to come round instead of four, because one of them never
        // reached the counter. Inside a single wave the cadence was always exactly right, which is why
        // it reads as random rather than as broken.
        var build = OneRhythmSkill(Form.Projectile);
        var hunter = new Hunter();
        var pool = SoloBattle.ChampionHealth(build, hunter) * 50;   // survives; the count is the subject
        var champ = new Champion { MaxHealth = pool, Health = pool };
        var run = new SoloExpedition(build, champ, hunter, 110f, 1f,
                                     ExpeditionTuning.Default, Source.Nature, new Random(17));

        var lastBeat = 0;
        for (var w = 0; w < 5; w++)
        {
            run.RefreshPool();
            run.PushWave();
            if (run.LastWaveEvents.Count == 0) break;

            var actions = ActionTimes(run.LastWaveEvents).Count;
            var beats = champ.BeatCount - lastBeat;
            lastBeat = champ.BeatCount;

            _out.WriteLine($"wave {run.Wave}: {actions} actions, {beats} beats");
            Assert.True(actions == beats,
                $"wave {run.Wave} held {actions} champion actions but advanced BeatCount by {beats}. "
                + "Every rhythm cooldown is counted against that number, so it has to be the same number "
                + "the player is watching.");
        }
    }

    [Theory]
    [InlineData(Form.Strike)]
    [InlineData(Form.Projectile)]
    public void test_a_new_run_waits_the_full_cooldown_before_its_first_cast(Form form)
    {
        // THE SECOND REPORT: "öldükten sonra 2. vuruşta skill'i attı." A death starts a new run, which
        // mints a new Champion — so ReadyAtBeat is empty, and the opening default decided instead of
        // the cooldown: `min(1, beats - 1)` let a rhythm skill fire from the wave's SECOND beat.
        //
        // The rail says EVERY FOURTH ACTION. A rule with an unnamed exception on the one wave every
        // player sees most often — the first one after dying — is not a rule, and the exception was
        // measured against a wave a third of the length waves are now.
        var beats = FormBehaviour.CooldownBeats(form);
        var build = OneRhythmSkill(form);
        var hunter = new Hunter();
        var pool = SoloBattle.ChampionHealth(build, hunter) * 50;
        var champ = new Champion { MaxHealth = pool, Health = pool };
        var run = new SoloExpedition(build, champ, hunter, 110f, 1f,
                                     ExpeditionTuning.Default, Source.Nature, new Random(17));
        run.PushWave();

        var actions = ActionTimes(run.LastWaveEvents);
        var first = run.LastWaveEvents.First(e => e.Kind == BattleEventKind.Skill).AtMs;
        var index = actions.IndexOf(first);

        _out.WriteLine($"{form} (waits {beats}) first cast of a new run on action #{index}");

        // Zero-based, so the Nth action is index N-1: a four-action skill casts ON the fourth action.
        Assert.Equal(beats - 1, index);
    }

    [Fact]
    public void test_there_is_one_counter_and_the_readout_reads_it()
    {
        // THERE USED TO BE TWO. The cadence lived in SoloBattle's BeatCount and the dial rebuilt its own
        // count by walking the damage stream — "every Strike that is not a skill's own hit is an action"
        // — which is a guess about DAMAGE standing in for a fact about RHYTHM. They agreed only by hand,
        // and four damage sources share the flag it guessed on (the poison bleed, THORNS, BREAKER's
        // overkill spill, a MARK detonation), each of which would have turned the ring a notch the
        // champion never earned.
        //
        // The sim publishes its own counter now (BattleEventKind.Beat), so this asserts the thing that
        // actually matters: one Beat per action the champion took, carrying the number the cooldowns
        // are measured against. A Trap raises a Skill event and is NOT an action — it fires on being
        // bitten, off the beat — so it must not appear here either.
        var events = LongWave(Form.Strike);
        var replay = new WaveReplay(events, new Dictionary<int, int> { [0] = 100 },
                                    new Dictionary<int, int> { [0] = 100 }, 400_000f);
        var actions = ActionTimes(events);
        var beats = events.Where(e => e.Kind == BattleEventKind.Beat).ToList();

        _out.WriteLine($"{actions.Count} champion actions, {beats.Count} beats published");

        Assert.Equal(actions.Count, beats.Count);
        // One per action, in step, and each carrying the running count rather than a wave-local index.
        for (var i = 0; i < actions.Count; i++)
        {
            Assert.Equal(actions[i], beats[i].AtMs);
            Assert.Equal(beats[0].Amount + i, beats[i].Amount);
            Assert.Equal(beats[i].Amount, replay.BeatAt(actions[i]));
        }
    }
}
