using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Persistence;
using Xunit;

namespace IdleXIdle.Core.Tests.Persistence;

/// <summary>
/// The banked unlocked set: it round-trips, it is never taken back, and a save from before it was
/// banked is seeded from the gates that were true when it was written.
/// </summary>
/// <remarks>
/// The tiered roster (2026-08-26) tightened every second champion's gate — THE THORNWALL went from the
/// first region's conquest to the whole map. Unlocks were purely derived until then, so without this
/// seam every player who had THE THORNWALL would have lost it on their next launch.
/// </remarks>
public class UnlockedCharactersTest
{
    private const long Now = 1_700_000_000_000L;

    [Fact]
    public void test_a_pre_tier_save_keeps_the_champions_it_had_earned_under_the_old_gates()
    {
        // Written before UnlockedCharacters existed: two conquests, the old Hollow Hunt done.
        // Under the OLD rules that is THE THORNWALL (verdant_hollow), THE ANVIL and THE MAGPIE
        // (cinderworks) and THE QUIVER (q_hollow_hunt). Under the NEW rules three of those are
        // second-tier and far from earned.
        var legacyJson = """
        {
          "Version": 2,
          "SavedAtMs": 1700000000000,
          "ActiveCharacterId": "seeker",
          "ConqueredRegions": ["verdant_hollow", "cinderworks"],
          "QuestsDone": ["q_hollow_hunt"]
        }
        """;
        var loaded = SaveSystem.Deserialize(legacyJson, Now);
        Assert.True(loaded.Ok);
        Assert.Empty(loaded.Save!.UnlockedCharacters);

        var state = new CharacterState();
        SaveSystem.RestoreCharacters(loaded.Save, state);

        foreach (var id in new[] { "seeker", "thornwall", "anvil", "magpie", "quiver" })
            Assert.True(state.IsUnlocked(id), $"{id} was earned under the old gates and must stay");
        foreach (var id in new[] { "tower", "chorus", "metronome", "unbroken", "oathbound" })
            Assert.False(state.IsUnlocked(id), $"{id} was never earned");
    }

    [Fact]
    public void test_a_pre_tier_save_that_met_the_old_hollow_hunt_without_latching_it_keeps_the_quiver()
    {
        // Depth 20 was reached, but the save was written before the host latched the quest.
        var save = new SaveGame
        {
            SavedAtMs = Now,
            RegionFarms = { new RegionFarmSave { Id = "verdant_hollow", BestDepth = LegacyUnlocks.HollowHuntDepth } },
        };
        var state = new CharacterState();
        SaveSystem.RestoreCharacters(save, state);
        Assert.True(state.IsUnlocked("quiver"));

        // One wave short, and the old rule would not have given it either.
        var shy = new SaveGame
        {
            SavedAtMs = Now,
            RegionFarms = { new RegionFarmSave { Id = "verdant_hollow", BestDepth = LegacyUnlocks.HollowHuntDepth - 1 } },
        };
        var other = new CharacterState();
        SaveSystem.RestoreCharacters(shy, other);
        Assert.False(other.IsUnlocked("quiver"));
    }

    [Fact]
    public void test_a_pre_tier_save_with_one_kept_vow_keeps_the_oathbound()
    {
        var save = new SaveGame { SavedAtMs = Now, RunsWithVowKept = 1 };
        var state = new CharacterState();
        SaveSystem.RestoreCharacters(save, state);
        Assert.True(state.IsUnlocked("oathbound"));
        // The new gate wants three; a fresh Refresh must not undo the seed.
        Assert.Empty(state.Refresh(System.Array.Empty<string>()));
        Assert.True(state.IsUnlocked("oathbound"));
    }

    [Fact]
    public void test_a_banked_set_is_never_reseeded_from_the_old_gates()
    {
        // A NEW save: the starter is banked, the first region is conquered. Under the old gates that
        // conquest was THE THORNWALL; under the new ones it is nothing, and the bank says so.
        var save = new SaveGame
        {
            SavedAtMs = Now,
            UnlockedCharacters = { CharacterRoster.StarterId },
            ConqueredRegions = { "verdant_hollow" },
        };
        var state = new CharacterState();
        SaveSystem.RestoreCharacters(save, state);
        state.Refresh(save.ConqueredRegions);
        Assert.False(state.IsUnlocked("thornwall"));
    }

    [Fact]
    public void test_the_banked_set_round_trips_through_the_save_file()
    {
        var state = new CharacterState();
        state.Refresh(new[] { "cinderworks", "umbral_reach" });
        state.CompleteQuest("q_magpie_chests");   // THE MAGPIE's — the loot champion has the chest quest
        state.Refresh(new[] { "cinderworks", "umbral_reach" });
        Assert.Equal(new[] { "seeker", "anvil", "chorus", "magpie" }, state.SaveUnlocked());

        var save = new SaveGame
        {
            SavedAtMs = Now,
            ActiveCharacterId = "chorus",
            UnlockedCharacters = state.SaveUnlocked().ToList(),
            QuestsDone = state.SaveQuests().ToList(),
        };
        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(save), Now);
        Assert.True(loaded.Ok);

        var back = new CharacterState();
        SaveSystem.RestoreCharacters(loaded.Save!, back);
        Assert.Equal("chorus", back.ActiveId);
        Assert.Equal(new[] { "seeker", "anvil", "chorus", "magpie" }, back.SaveUnlocked());
        // Restored with NO conquests handed to Refresh: nothing is reported fresh, nothing is lost.
        Assert.Empty(back.Refresh(System.Array.Empty<string>()));
        Assert.True(back.IsUnlocked("magpie"));
    }

    [Fact]
    public void test_a_fresh_state_always_banks_the_starter_so_empty_means_old()
    {
        // "Empty" is the legacy signal; a new save must never write it.
        Assert.Equal(new[] { CharacterRoster.StarterId }, new CharacterState().SaveUnlocked());
    }

    [Fact]
    public void test_a_banked_id_the_roster_no_longer_has_is_dropped_quietly()
    {
        var state = new CharacterState();
        state.Restore("seeker", null, new[] { "anvil", "a_champion_that_was_cut" });
        Assert.True(state.IsUnlocked("anvil"));
        Assert.False(state.IsUnlocked("a_champion_that_was_cut"));
        Assert.Equal(new[] { "seeker", "anvil" }, state.SaveUnlocked());
    }

    [Fact]
    public void test_the_legacy_table_covers_the_whole_roster_and_is_the_pre_tier_roster()
    {
        // Every champion has an old gate, and the retired quest ids are the ones it names.
        foreach (var c in CharacterRoster.All)
            Assert.True(LegacyUnlocks.Before.ContainsKey(c.Id), $"{c.Id} has no legacy gate");
        Assert.Equal(LegacyUnlocks.HollowHuntId, LegacyUnlocks.Before["quiver"].QuestId);
        Assert.Equal(LegacyUnlocks.FirstVowId, LegacyUnlocks.Before["oathbound"].QuestId);
        Assert.Equal("verdant_hollow", LegacyUnlocks.Before["thornwall"].RegionId);
    }
}
