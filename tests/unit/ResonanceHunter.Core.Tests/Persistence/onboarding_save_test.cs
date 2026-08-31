using ResonanceHunter.Core.Persistence;
using Xunit;

namespace ResonanceHunter.Core.Tests.Persistence;

/// <summary>The intro flag and the explained-screens list survive a save round trip, and old saves load without them.</summary>
public class OnboardingSaveTest
{
    private const long Now = 1_700_000_000_000L;

    [Fact]
    public void test_intro_seen_and_explained_screens_round_trip_through_the_save()
    {
        var save = new SaveGame
        {
            SavedAtMs = Now,
            IntroSeen = true,
            ExplainedScreens = new() { "Stats", "Map", "SkillSlot2" },
        };

        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(save), Now);

        Assert.True(loaded.Ok);
        Assert.True(loaded.Save!.IntroSeen);
        Assert.Equal(new[] { "Stats", "Map", "SkillSlot2" }, loaded.Save.ExplainedScreens);
    }

    [Fact]
    public void test_a_save_written_before_the_intro_existed_loads_unseen_with_nothing_explained()
    {
        // Old saves carry neither member. They must load clean: IntroSeen false and an empty list. What
        // the game then does with that pair (treat every open screen as read) is Onboarding's rule, not
        // the loader's — the loader only has to not invent a value.
        var legacyJson = """
        {
          "Version": 2,
          "SavedAtMs": 1700000000000,
          "Gleam": 42
        }
        """;

        var loaded = SaveSystem.Deserialize(legacyJson, Now);

        Assert.True(loaded.Ok);
        Assert.False(loaded.Save!.IntroSeen);
        Assert.Empty(loaded.Save.ExplainedScreens);
        Assert.Equal(42, loaded.Save.Gleam);
    }
}
