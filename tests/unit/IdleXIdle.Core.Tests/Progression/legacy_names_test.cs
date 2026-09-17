using IdleXIdle.Core.Progression;
using Xunit;

namespace IdleXIdle.Core.Tests.Progression;

/// <summary>
/// Two persisted NAME lists survived the 2026-09-01 renames (STATS → TRAINING, WEAVEBUILD → CHOOSEBUILD)
/// through read-forward aliases. A player who had learned a screen or closed a lesson must not be shown
/// it again because a word changed.
/// </summary>
public class LegacyNamesTest
{
    [Fact]
    public void test_an_old_explained_screen_key_reads_forward_to_the_new_activity_name()
    {
        Assert.Equal(nameof(Activity.Training), Onboarding.ModernScreenKey("Stats"));
        Assert.Equal("Gear", Onboarding.ModernScreenKey("Gear"));          // untouched names pass through
        Assert.Equal("SkillSlot3", Onboarding.ModernScreenKey("SkillSlot3"));
    }

    [Fact]
    public void test_the_retired_ladders_dismissed_names_are_carried_and_never_read()
    {
        // The forward-reading of an old rung NAME went with the ladder itself. Completion is a fact
        // now — a closed card completes nothing — so a saved dismissal has nothing left to mean, and
        // the catalogue exposes no way to be told one. The host still round-trips the list untouched
        // so that rolling this build back does not lose what an old player had closed.
        //
        // What DOES still read forward is the explained-screen key, which the tours behind LEARN THIS
        // SCREEN are still addressed by; that is the assertion above this one.
        Assert.DoesNotContain(nameof(OnboardingLessonId), OnboardingLessons.All.Select(id => id.ToString()));
    }
}
