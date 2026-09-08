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
    public void test_an_old_dismissed_rung_name_reads_forward_to_the_new_step_name()
    {
        Assert.Equal(nameof(TutorialStep.ChooseBuild), Tutorial.ModernRungName("WeaveBuild"));
        Assert.Equal("Watch", Tutorial.ModernRungName("Watch"));
    }
}
