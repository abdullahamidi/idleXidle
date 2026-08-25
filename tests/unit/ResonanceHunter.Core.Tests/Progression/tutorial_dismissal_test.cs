using System;
using System.Linq;
using ResonanceHunter.Core.Progression;
using Xunit;

namespace ResonanceHunter.Core.Tests.Progression;

/// <summary>
/// The guide strip's close button: a dismissed rung is skipped for display, and only for display.
/// </summary>
/// <remarks>
/// Playtest nine, item 7: "Messages like EVERY FIFTH WAVE IS A BOSS stay forever until I do the
/// thing. I should be able to close them." Closing a rung must show the NEXT lesson exactly as if
/// this one were completed — but it must never fake the underlying facts, because the unlock gates
/// read the same facts and a click on an x must not open a door.
/// </remarks>
public class TutorialDismissalTest
{
    [Fact]
    public void test_a_dismissed_rung_is_skipped_and_the_next_lesson_shows()
    {
        // MEET A BOSS waits for wave 5 — the one rung a player literally cannot rush, and the one the
        // complaint named. Closing it must show the next lesson, not silence.
        var facts = new TutorialFacts(WavesCleared: 3, DeepestWave: 3, StatsTrained: 1, Gleam: 100);
        Assert.Equal(TutorialStep.MeetABoss, Tutorial.Showing(facts));

        var dismissed = new[] { nameof(TutorialStep.MeetABoss) };
        Assert.Equal(TutorialStep.OpenChest, Tutorial.Showing(facts, dismissed));   // conquest is the LAST rung since it moved to wave 20
    }

    [Fact]
    public void test_a_dismissed_rung_never_returns_as_the_facts_grow()
    {
        var dismissed = new[] { nameof(TutorialStep.MeetABoss) };
        for (var wave = 1; wave < Tutorial.BossEvery; wave++)
        {
            var facts = new TutorialFacts(WavesCleared: wave, DeepestWave: wave, StatsTrained: 1, Gleam: 100);
            Assert.NotEqual(TutorialStep.MeetABoss, Tutorial.Showing(facts, dismissed));
        }
    }

    [Fact]
    public void test_dismissal_changes_the_display_and_never_the_facts()
    {
        // The plain ladder still reports the rung as current, so nothing derived from the facts —
        // unlocks, gates, skill slots — can be moved by a click on an x.
        var facts = new TutorialFacts(WavesCleared: 3, DeepestWave: 3, StatsTrained: 1, Gleam: 100);
        var dismissed = new[] { nameof(TutorialStep.MeetABoss) };

        Assert.Equal(TutorialStep.OpenChest, Tutorial.StepFor(facts, dismissed));
        Assert.Equal(TutorialStep.MeetABoss, Tutorial.StepFor(facts));
    }

    [Fact]
    public void test_dismissing_every_rung_silences_the_guide()
    {
        var facts = new TutorialFacts(WavesCleared: 1, DeepestWave: 1, Gleam: 100);
        var all = Enum.GetValues<TutorialStep>().Select(s => s.ToString()).ToList();

        Assert.Equal(TutorialStep.Done, Tutorial.StepFor(facts, all));
        Assert.Null(Tutorial.Showing(facts, all));
    }

    [Fact]
    public void test_a_dismissed_rung_whose_successor_is_not_ready_stays_silent()
    {
        // A brand-new save: WATCH is current; the next rung, SPEND GLEAM, cannot be obeyed with an
        // empty purse. Closing WATCH must leave the guide waiting silently — the ladder's oldest rule
        // — rather than skipping ahead to a prompt the player cannot act on.
        var facts = new TutorialFacts();
        Assert.Equal(TutorialStep.Watch, Tutorial.Showing(facts));

        var dismissed = new[] { nameof(TutorialStep.Watch) };
        Assert.Null(Tutorial.Showing(facts, dismissed));
    }

    [Fact]
    public void test_an_unknown_dismissed_name_is_harmless()
    {
        // A save edited by hand, or written by a build whose enum had a rung this one does not: the
        // stray name matches nothing and the guide behaves as if it were absent.
        var facts = new TutorialFacts();
        var dismissed = new[] { "SomeRetiredRung" };

        Assert.Equal(TutorialStep.Watch, Tutorial.Showing(facts, dismissed));
    }
}
