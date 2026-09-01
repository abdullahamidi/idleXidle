using System.Collections.Generic;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;
using Xunit;

namespace IdleXIdle.Core.Tests.Persistence;

/// <summary>A gift chest's key survives a reload, and a save from before gifts existed loads with none.</summary>
public class GiftChestSaveTest
{
    private const long Now = 1_700_000_000_000L;

    [Fact]
    public void test_a_gift_chests_key_round_trips_through_the_save()
    {
        var save = new SaveGame
        {
            SavedAtMs = Now,
            UnopenedChests = new List<SavedChest>
            {
                new() { Rarity = (int)Rarity.Common, Tier = 1, Element = "Nature", Region = "verdant_hollow", Gift = GiftChests.WelcomeKey },
                new() { Rarity = (int)Rarity.Rare, Tier = 3 },
            },
        };

        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(save), Now);

        Assert.True(loaded.Ok);
        var back = loaded.Save!.UnopenedChests;
        Assert.Equal(GiftChests.WelcomeKey, back[0].Gift);
        Assert.Null(back[1].Gift);
        // And the key still names a real gift once it is a chest again.
        Assert.NotNull(GiftChests.Get(back[0].Gift));
    }

    [Fact]
    public void test_a_save_from_before_gifts_loads_its_chests_as_ordinary_ones()
    {
        // Old saves are untouched: no retroactive gift, and their chests carry no key.
        var legacyJson = """
        {
          "Version": 2,
          "SavedAtMs": 1700000000000,
          "UnopenedChests": [ { "Rarity": 2, "Tier": 9, "Element": "Shadow" } ]
        }
        """;

        var loaded = SaveSystem.Deserialize(legacyJson, Now);

        Assert.True(loaded.Ok);
        var chest = Assert.Single(loaded.Save!.UnopenedChests);
        Assert.Null(chest.Gift);
        Assert.Equal(9, chest.Tier);
    }
}
