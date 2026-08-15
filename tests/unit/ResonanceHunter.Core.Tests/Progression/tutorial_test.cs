using System;
using System.Linq;
using ResonanceHunter.Core.Progression;
using Xunit;
using Xunit.Abstractions;

namespace ResonanceHunter.Core.Tests.Progression;

/// <summary>
/// The first-run guide teaches in the loop's order, and never before the player can act.
/// </summary>
public class TutorialTest
{
    private readonly ITestOutputHelper _out;

    public TutorialTest(ITestOutputHelper output) => _out = output;

    private static TutorialFacts Fresh => new(0, 0, 0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void test_a_brand_new_player_is_told_the_least_obvious_thing_first()
    {
        // The single most confusing property of this game is that nobody swings for you. A player who
        // does not know that sits waiting for a control that never arrives.
        Assert.Equal(TutorialStep.Watch, Tutorial.StepFor(Fresh));
        Assert.Contains("ON ITS OWN", Tutorial.Title(TutorialStep.Watch));
    }

    [Fact]
    public void test_no_step_arrives_before_the_player_can_act_on_it()
    {
        // "Spend your Gleam" with no Gleam is noise, and noise in a tutorial teaches a player to stop
        // reading it. Each lesson waits for the thing it is about to exist.
        // The step is SpendGleam either way — the order never changes — but with nothing to spend it
        // shows nothing and waits, rather than skipping ahead to a lesson out of sequence.
        var clearedOneWave = Fresh with { WavesCleared = 1 };
        Assert.Equal(TutorialStep.SpendGleam, Tutorial.StepFor(clearedOneWave));
        Assert.Null(Tutorial.Showing(clearedOneWave));

        var hasGleam = clearedOneWave with { Gleam = 40 };
        Assert.Equal(TutorialStep.SpendGleam, Tutorial.Showing(hasGleam));

        // Same rule for gear: nothing to equip, nothing to say about equipping.
        var trained = hasGleam with { StatsTrained = 1 };
        Assert.Null(Tutorial.Showing(trained));
        Assert.Equal(TutorialStep.EquipItem, Tutorial.Showing(trained with { ItemsOwned = 1 }));
    }

    [Fact]
    public void test_doing_the_lesson_completes_it_whether_or_not_it_was_read()
    {
        // A player who already knows the loop must never be stopped. Every step is satisfied by the
        // ACTION, so someone who trains, equips and weaves in the first minute clears the whole guide
        // without acknowledging a single prompt.
        var expert = new TutorialFacts(
            WavesCleared: 6, Gleam: 500, StatsTrained: 3, ItemsOwned: 4, ItemsWorn: 2,
            BossesFelled: 1, SkillsWoven: 4, DeepestWave: 9, RegionsConquered: 1);

        Assert.Equal(TutorialStep.Done, Tutorial.StepFor(expert));
        Assert.False(Tutorial.HasGuidance(TutorialStep.Done));
    }

    [Fact]
    public void test_the_guide_only_ever_moves_forward_as_the_player_does_more()
    {
        // Walk a plausible career and assert the step never goes BACKWARD. A guide that reverses is
        // worse than none: it re-teaches something the player has already done, which reads as the game
        // losing track of them.
        var steps = new[]
        {
            Fresh,
            Fresh with { WavesCleared = 1 },
            Fresh with { WavesCleared = 2, Gleam = 60 },
            Fresh with { WavesCleared = 3, Gleam = 60, StatsTrained = 1 },
            Fresh with { WavesCleared = 4, Gleam = 60, StatsTrained = 1, ItemsOwned = 1 },
            Fresh with { WavesCleared = 5, Gleam = 60, StatsTrained = 1, ItemsOwned = 1, ItemsWorn = 1 },
            Fresh with { WavesCleared = 6, Gleam = 60, StatsTrained = 1, ItemsOwned = 1, ItemsWorn = 1, BossesFelled = 1 },
            Fresh with { WavesCleared = 9, Gleam = 60, StatsTrained = 1, ItemsOwned = 1, ItemsWorn = 1, BossesFelled = 1, SkillsWoven = 4 },
            Fresh with { WavesCleared = 12, Gleam = 60, StatsTrained = 1, ItemsOwned = 1, ItemsWorn = 1, BossesFelled = 1, SkillsWoven = 4, RegionsConquered = 1 },
        };

        var last = -1;
        foreach (var f in steps)
        {
            var step = (int)Tutorial.StepFor(f);
            _out.WriteLine($"   waves {f.WavesCleared,2}  ->  {(TutorialStep)step}");
            Assert.True(step >= last, $"the guide went backwards to {(TutorialStep)step}");
            last = step;
        }

        Assert.Equal(TutorialStep.Done, Tutorial.StepFor(steps[^1]));
    }

    [Fact]
    public void test_every_step_has_something_to_say_and_names_the_key_that_acts_on_it()
    {
        foreach (var step in Enum.GetValues<TutorialStep>().Where(s => s != TutorialStep.Done))
        {
            var title = Tutorial.Title(step);
            var body = Tutorial.Body(step);

            _out.WriteLine($"   {step,-12} {title}");
            _out.WriteLine($"                {body}");

            Assert.False(string.IsNullOrWhiteSpace(title), $"{step} has no title");
            Assert.False(string.IsNullOrWhiteSpace(body), $"{step} has no body");

            // A lesson that does not say HOW is a complaint. Every step past the first names its key.
            if (step != TutorialStep.Watch && step != TutorialStep.MeetABoss && step != TutorialStep.Conquer)
                Assert.True(body.Contains(" V ") || body.Contains(" C ") || body.Contains(" B ")
                            || body.Contains("(F)"),
                    $"{step} tells the player what to do and not how: \"{body}\"");
        }
    }
}
