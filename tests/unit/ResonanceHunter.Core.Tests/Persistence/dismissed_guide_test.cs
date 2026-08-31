using ResonanceHunter.Core.Persistence;
using Xunit;

namespace ResonanceHunter.Core.Tests.Persistence;

/// <summary>Dismissed guide rungs survive a save round trip, and old saves load without them.</summary>
public class DismissedGuideTest
{
    private const long Now = 1_700_000_000_000L;

    [Fact]
    public void test_dismissed_rungs_round_trip_through_the_save()
    {
        var save = new SaveGame
        {
            SavedAtMs = Now,
            DismissedGuideRungs = new() { "MeetABoss", "OpenChest" },
        };

        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(save), Now);

        Assert.True(loaded.Ok);
        Assert.Equal(new[] { "MeetABoss", "OpenChest" }, loaded.Save!.DismissedGuideRungs);
    }

    [Fact]
    public void test_a_save_written_before_dismissal_existed_loads_with_none()
    {
        // Old saves carry no DismissedGuideRungs member at all. They must load clean and EMPTY — the
        // guide behaves exactly as it did before the close button existed, and nothing else is lost.
        var legacyJson = """
        {
          "Version": 2,
          "SavedAtMs": 1700000000000,
          "Gleam": 42
        }
        """;

        var loaded = SaveSystem.Deserialize(legacyJson, Now);

        Assert.True(loaded.Ok);
        Assert.Empty(loaded.Save!.DismissedGuideRungs);
        Assert.Equal(42, loaded.Save.Gleam);
    }
}
