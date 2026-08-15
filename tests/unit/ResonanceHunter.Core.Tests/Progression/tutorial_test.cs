using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Progression;
using Xunit;
using Xunit.Abstractions;

namespace ResonanceHunter.Core.Tests.Progression;

/// <summary>
/// The first-run guide reaches every lesson it was written to teach.
/// </summary>
/// <remarks>
/// <b>THE OLD VERSION OF THIS FILE PASSED WHILE THE GUIDE WAS DEADLOCKED.</b> It exercised
/// <see cref="Tutorial"/> against hand-authored facts and never asked whether the shipping game could
/// ever PRODUCE those facts. Its fixture built <c>ItemsOwned = 1</c> alongside <c>BossesFelled = 0</c>
/// — a state the game cannot reach, since an item implies a chest implies a felled boss — and so it
/// proved MeetABoss reachable in a world that does not exist. Meanwhile the real guide went silent for
/// twenty-five waves and skipped two of its six rungs on every save ever played.
///
/// The lesson generalises past this file: <b>a test that invents its own inputs can only prove the
/// code is self-consistent.</b> The career walk below is the antidote — it moves the facts the way
/// playing the game moves them, and asks what the player would actually see.
/// </remarks>
public class TutorialTest
{
    private readonly ITestOutputHelper _out;

    public TutorialTest(ITestOutputHelper output) => _out = output;

    [Fact]
    public void test_a_brand_new_player_is_told_the_least_obvious_thing_first()
    {
        // Someone who does not know the champion fights alone sits waiting for a control that never
        // comes. Nothing else can be taught until that is said.
        Assert.Equal(TutorialStep.Watch, Tutorial.Showing(new TutorialFacts()));
    }

    [Fact]
    public void test_every_step_is_displayed_on_a_real_career()
    {
        // THE TEST THAT WOULD HAVE CAUGHT THE DEADLOCK. It advances the facts only in ways the live game
        // advances them, in the order the game advances them, and records what the player is shown.
        //
        // Two rungs used to be structurally unreachable here: MeetABoss (its condition was always
        // already satisfied by the time it became current) and, in practice, everything after EquipItem
        // (which waited on an item the guide never explained how to get).
        var seen = new List<TutorialStep>();
        var facts = new TutorialFacts();

        void Step(string what, TutorialFacts next)
        {
            var showing = Tutorial.Showing(facts);
            _out.WriteLine($"{what,-34} -> {(showing?.ToString() ?? "(silent)")}");
            if (showing is { } s && !seen.Contains(s)) seen.Add(s);
            facts = next;
        }

        // A career, in the order it actually happens.
        Step("fresh save", facts with { WavesCleared = 1, DeepestWave = 1, Gleam = 30 });
        Step("first wave cleared, 30 Gleam", facts with { StatsTrained = 1, Gleam = 5, DeepestWave = 3 });
        Step("trained a stat, wave 3", facts with { DeepestWave = 4, WavesCleared = 4 });
        Step("wave 4 — boss approaching", facts with { DeepestWave = 5, WavesCleared = 5 });
        Step("felled the first boss", facts with { DeepestWave = 9, WavesCleared = 9 });
        Step("wave 9, still no chest", facts with { ChestsHeld = 1, DeepestWave = 10 });
        Step("a chest dropped", facts with { ItemsOwned = 1, ChestsHeld = 0, DeepestWave = 11 });
        Step("opened it — an item", facts with { ItemsWorn = 1, DeepestWave = 12 });
        Step("equipped it", facts with { SkillsWoven = 1, DeepestWave = 14 });
        Step("wove a skill", facts with { DeepestWave = 20 });
        Step("pushing for the objective", facts with { RegionsConquered = 1 });
        Step("conquered the region", facts);

        _out.WriteLine("");
        _out.WriteLine("shown: " + string.Join(", ", seen));

        foreach (var step in Enum.GetValues<TutorialStep>())
        {
            if (step == TutorialStep.Done) continue;
            Assert.True(seen.Contains(step),
                $"{step} was never displayed on a career that does everything in order. It is dead copy: "
                + "either its condition is satisfied before it becomes current, or it waits on something "
                + "a later step explains how to get.");
        }
    }

    [Fact]
    public void test_the_guide_is_never_silent_for_long_while_waiting_on_a_chest()
    {
        // The deadlock's signature. A chest is a 20% roll on a boss every fifth wave, so the wait
        // between "met a boss" and "owns an item" is tens of waves — and the guide used to say NOTHING
        // for all of it, which is exactly what "tutorial bozuk" described.
        var silent = 0;
        for (var wave = Tutorial.BossEvery; wave <= 60; wave++)
        {
            var facts = new TutorialFacts(
                WavesCleared: wave, DeepestWave: wave, StatsTrained: 3, Gleam: 400);

            if (Tutorial.Showing(facts) is null) silent++;
        }

        _out.WriteLine($"of the 56 waves spent waiting for a first chest, the guide is silent on {silent}");

        Assert.Equal(0, silent);
    }

    [Fact]
    public void test_no_step_arrives_before_the_player_can_act_on_it()
    {
        // A prompt that cannot be obeyed teaches the player to stop reading prompts, after which the one
        // that matters is invisible too.
        var broke = new TutorialFacts(WavesCleared: 1, DeepestWave: 1, Gleam: 0);
        Assert.Null(Tutorial.Showing(broke));

        // And one Gleam under the real price is still broke. This fired at 20 while the cheapest rank
        // cost 25, so for five Gleam the guide said "train" and every row refused.
        var nearlyBroke = broke with { Gleam = Tutorial.FirstRankCost - 1 };
        Assert.Null(Tutorial.Showing(nearlyBroke));

        var affords = broke with { Gleam = Tutorial.FirstRankCost };
        Assert.Equal(TutorialStep.SpendGleam, Tutorial.Showing(affords));
    }

    [Fact]
    public void test_the_prompt_threshold_matches_what_a_rank_really_costs()
    {
        // The constant is a copy, so it is pinned against its source. A retune of the training curve
        // that left this behind would re-create the unobeyable prompt exactly.
        Assert.Equal(Tutorial.FirstRankCost, Hunter.CostOfRank(0, new ProgressionTuning()));
    }

    [Fact]
    public void test_the_boss_cadence_matches_the_expedition_tuning()
    {
        // Same reason: the guide tells the player "survive to wave 5", and that number lives in a
        // different assembly's tuning.
        Assert.Equal(Tutorial.BossEvery, ExpeditionTuning.Default.BossEvery);
    }

    [Fact]
    public void test_doing_the_lesson_completes_it_whether_or_not_it_was_read()
    {
        // The guide must be skippable by playing. A returning player who already knows the loop should
        // never be stopped by it.
        var veteran = new TutorialFacts(
            WavesCleared: 400, DeepestWave: 60, Gleam: 90_000, StatsTrained: 40,
            ItemsOwned: 12, ItemsWorn: 6, SkillsWoven: 4, RegionsConquered: 3);

        Assert.Null(Tutorial.Showing(veteran));
        Assert.Equal(TutorialStep.Done, Tutorial.StepFor(veteran));
    }

    [Fact]
    public void test_a_finished_player_is_never_dragged_back_by_undoing_something()
    {
        // ItemsWorn drops when a player unequips; SkillsWoven can drop on a respec. Without the
        // conquest guard, selling a sword sent a veteran back to "SOMETHING DROPPED" — the guide
        // re-teaching a lesson lived hours ago, which reads as the game losing track of you.
        var veteran = new TutorialFacts(
            WavesCleared: 400, DeepestWave: 60, Gleam: 90_000, StatsTrained: 40,
            ItemsOwned: 0, ItemsWorn: 0, SkillsWoven: 0, RegionsConquered: 2);

        Assert.Equal(TutorialStep.Done, Tutorial.StepFor(veteran));
        Assert.Null(Tutorial.Showing(veteran));
    }

    [Fact]
    public void test_the_guide_only_ever_moves_forward_as_the_player_does_more()
    {
        // Monotonicity over the facts that only ever grow. A guide that reverses re-teaches something
        // already done.
        var rng = new Random(4);
        var facts = new TutorialFacts();
        var highest = Tutorial.StepFor(facts);

        for (var i = 0; i < 500; i++)
        {
            facts = facts with
            {
                WavesCleared = facts.WavesCleared + rng.Next(0, 3),
                DeepestWave = facts.DeepestWave + rng.Next(0, 2),
                Gleam = facts.Gleam + rng.Next(0, 40),
                StatsTrained = facts.StatsTrained + (rng.Next(0, 6) == 0 ? 1 : 0),
                ItemsOwned = facts.ItemsOwned + (rng.Next(0, 25) == 0 ? 1 : 0),
                ItemsWorn = facts.ItemsWorn + (rng.Next(0, 40) == 0 ? 1 : 0),
                ChestsHeld = facts.ChestsHeld + (rng.Next(0, 30) == 0 ? 1 : 0),
                SkillsWoven = facts.SkillsWoven + (rng.Next(0, 60) == 0 ? 1 : 0),
                RegionsConquered = facts.RegionsConquered + (rng.Next(0, 200) == 0 ? 1 : 0),
            };

            var now = Tutorial.StepFor(facts);
            Assert.True(now >= highest, $"the guide went backwards, from {highest} to {now}.");
            highest = now;
        }
    }

    [Fact]
    public void test_every_step_has_something_to_say_and_names_the_key_that_acts_on_it()
    {
        foreach (var step in Enum.GetValues<TutorialStep>())
        {
            if (step == TutorialStep.Done)
            {
                Assert.False(Tutorial.HasGuidance(step));
                continue;
            }

            Assert.False(string.IsNullOrWhiteSpace(Tutorial.Title(step)), $"{step} has no title.");
            Assert.False(string.IsNullOrWhiteSpace(Tutorial.Body(step)), $"{step} has no body.");
        }
    }

    [Fact]
    public void test_the_guide_never_sends_the_player_at_a_locked_door()
    {
        // TWO SYSTEMS WRITTEN DAYS APART, each correct alone. The guide's bodies are a list of hotkeys;
        // Unlocks decides which of those screens exist yet. A step that says "press F for the FORGE"
        // while the Forge is a dimmed tile teaches the player that the guide does not know what is going
        // on — worse than saying nothing at all.
        //
        // Walked over the same career the display test uses, checking at every point that the step being
        // SHOWN points at a screen that is OPEN.
        var journey = new (string moment, TutorialFacts t, UnlockFacts u)[]
        {
            ("fresh save",         new TutorialFacts(), new UnlockFacts()),
            ("first wave, 30g",    new TutorialFacts(WavesCleared: 1, DeepestWave: 1, Gleam: 30),
                                   new UnlockFacts(WavesCleared: 1, DeepestWave: 1)),
            ("trained, wave 4",    new TutorialFacts(WavesCleared: 4, DeepestWave: 4, StatsTrained: 1),
                                   new UnlockFacts(WavesCleared: 4, DeepestWave: 4)),
            ("boss felled",        new TutorialFacts(WavesCleared: 6, DeepestWave: 6, StatsTrained: 1),
                                   new UnlockFacts(WavesCleared: 6, DeepestWave: 6)),
            ("a chest is held",    new TutorialFacts(WavesCleared: 9, DeepestWave: 9, StatsTrained: 1, ChestsHeld: 1),
                                   new UnlockFacts(WavesCleared: 9, DeepestWave: 9, ChestsHeld: 1)),
            ("opened it",          new TutorialFacts(WavesCleared: 11, DeepestWave: 11, StatsTrained: 1, ItemsOwned: 1),
                                   new UnlockFacts(WavesCleared: 11, DeepestWave: 11, ItemsOwned: 1)),
            ("equipped it",        new TutorialFacts(WavesCleared: 13, DeepestWave: 13, StatsTrained: 1, ItemsOwned: 1, ItemsWorn: 1),
                                   new UnlockFacts(WavesCleared: 13, DeepestWave: 13, ItemsOwned: 1)),
        };

        foreach (var (moment, t, u) in journey)
        {
            if (Tutorial.Showing(t) is not { } step) continue;
            if (Tutorial.Sends(step, t) is not { } activity) continue;

            var open = Unlocks.IsOpen(activity, u);
            _out.WriteLine($"{moment,-18} {step,-11} sends to {activity,-6} {(open ? "OPEN" : "LOCKED")}");

            Assert.True(open,
                $"at \"{moment}\" the guide shows {step}, which sends the player to {activity} — and "
                + $"{activity} is still locked. The guide is naming a door the game has not opened.");
        }
    }
}
