using System.Collections.Generic;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;
using Xunit;

namespace IdleXIdle.Core.Tests.Persistence;

/// <summary>
/// A set's first completion is celebrated ONCE (UI polish brief §52): the save remembers which sets
/// have already had their "MACHINE SET COMPLETE · PLATING ACTIVE" moment, so re-equipping the fifth
/// piece after a swap does not replay it. Additive field — an older save loads with none celebrated.
/// </summary>
public class CompletedSetsSaveTests
{
    private const long Now = 1_700_000_000_000L;

    [Fact]
    public void test_the_sets_already_celebrated_survive_a_reload()
    {
        // Arrange
        var save = SaveSystem.Capture(new Hunter(), new List<ItemInstance>(), Now) with
        {
            CompletedSets = new List<string> { "Machine", "Nature" },
        };

        // Act
        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(save), Now);

        // Assert
        Assert.True(loaded.Ok);
        Assert.Equal(new[] { "Machine", "Nature" }, loaded.Save!.CompletedSets);
    }

    [Fact]
    public void test_an_older_save_without_the_field_has_celebrated_nothing()
    {
        var json = """
        { "Version": 3, "SavedAtMs": 1700000000000 }
        """;

        var loaded = SaveSystem.Deserialize(json, Now);

        Assert.True(loaded.Ok);
        Assert.Empty(loaded.Save!.CompletedSets);
    }
}
