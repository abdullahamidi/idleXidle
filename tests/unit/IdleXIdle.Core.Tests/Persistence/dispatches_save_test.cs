using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Progression;
using Xunit;

namespace IdleXIdle.Core.Tests.Persistence;

/// <summary>
/// THE INBOX IN THE SAVE: which dispatches exist, which are read, the stable payload that rebuilds
/// them, and the known-key set — never copy, never a rectangle. A file from before the inbox is
/// seeded as already knowing every accomplishment it carries, with no unread mail; a file written
/// with an inbox is believed exactly as written.
/// </summary>
public class DispatchesSaveTest
{
    private const long Now = 1_700_000_000_000L;

    [Fact]
    public void test_the_inbox_survives_a_reload_rows_read_state_order_columns_and_known_keys()
    {
        var inbox = new Inbox();
        inbox.Post(Dispatches.Region("verdant_hollow", Now));
        inbox.Post(Dispatches.Trait("t_deep_cut", Now + 1));
        inbox.Post(Dispatches.Migration(DispatchKeys.MigrationKeystones, Now + 2, count: 4));
        inbox.Post(Dispatches.Gem(Now + 3));
        inbox.MarkRead(DispatchKeys.Trait("t_deep_cut"));
        inbox.Know(DispatchKeys.QuestComplete("q_old"));   // known with no row: what a seed writes

        var save = new SaveGame
        {
            SavedAtMs = Now,
            Dispatches = inbox.ToSave(),
            KnownDispatchKeys = inbox.KnownToSave(),
            DispatchesOpenedEver = true,
        };

        var json = SaveSystem.Serialize(save);
        var loaded = SaveSystem.Deserialize(json, Now);
        Assert.True(loaded.Ok);
        var back = new Inbox();
        back.Restore(SaveSystem.RestoreDispatches(loaded.Save!), loaded.Save!.KnownDispatchKeys);

        Assert.Equal(inbox.Ledger, back.Ledger);   // every row, every column, Read, created order
        Assert.Equal(inbox.Rows, back.Rows);
        Assert.Equal(inbox.Known.OrderBy(k => k), back.Known.OrderBy(k => k));
        Assert.Equal(3, back.Unread);
        Assert.True(loaded.Save.DispatchesOpenedEver);

        // Columns travel flat: absent ones are dropped from the file, present ones kept, Kind by NAME.
        Assert.Contains("\"Kind\": \"Migration\"", json);
        Assert.Contains("\"Count\": 4", json);
        Assert.Contains("\"RegionId\": \"verdant_hollow\"", json);
        Assert.DoesNotContain("\"SubjectId\": null", json);
    }

    [Fact]
    public void test_a_pre_inbox_save_loads_with_an_empty_inbox_and_is_seeded_as_knowing_what_it_carries()
    {
        // ALWAYS WRITE "Version": a fixture without it reads as CURRENT (SaveGame.Version defaults to
        // CurrentVersion) and this test would pass for the wrong reason.
        var json = """
        {
          "Version": 7,
          "SavedAtMs": 1700000000000,
          "ConqueredRegions": [ "verdant_hollow" ],
          "DiscoveredTraits": [ "t_deep_cut" ],
          "DiscoveredKeystoneIds": [ "glass_cannon" ],
          "DiscoveredVowIds": [ "vow_pure" ],
          "QuestsDone": [ "q_cinder_deep" ],
          "UnlockedCharacters": [ "seeker", "anvil" ],
          "CompletedSets": [ "Machine" ],
          "MemoryDustUnlocks": [ "ks_ironclad", "socket_2" ]
        }
        """;
        var loaded = SaveSystem.Deserialize(json, Now);
        Assert.True(loaded.Ok);
        var save = loaded.Save!;
        Assert.Empty(save.Dispatches);
        Assert.Empty(save.KnownDispatchKeys);
        Assert.False(save.DispatchesOpenedEver);

        var inbox = new Inbox();
        inbox.Restore(SaveSystem.RestoreDispatches(save), save.KnownDispatchKeys);
        Assert.Empty(inbox.Rows);
        Assert.Equal(0, inbox.Unread);

        Assert.True(Dispatches.SeedKnown(save, new[] { Activity.Hunt, Activity.Training, Activity.Gear }, inbox));

        // NO ROWS, NO TIMESTAMPS, NO RECONSTRUCTED HISTORY — only knowledge.
        Assert.Empty(inbox.Rows);
        Assert.Equal(0, inbox.Unread);
        foreach (var key in new[]
                 {
                     DispatchKeys.RegionConquered("verdant_hollow"),
                     DispatchKeys.Trait("t_deep_cut"),
                     DispatchKeys.Keystone("glass_cannon"),
                     DispatchKeys.Keystone("ironclad"),          // the legacy tree's grant, unioned in
                     DispatchKeys.Vow("vow_pure"),
                     DispatchKeys.QuestComplete("q_cinder_deep"),
                     DispatchKeys.HunterJoined("seeker"),
                     DispatchKeys.HunterJoined("anvil"),
                     DispatchKeys.SetComplete("Machine"),
                     DispatchKeys.Unlock(Activity.Training),
                     DispatchKeys.Unlock(Activity.Gear),
                     DispatchKeys.SocketFirst,                   // a conquest opened one
                 })
            Assert.Contains(key, inbox.Known);
        Assert.DoesNotContain(DispatchKeys.Unlock(Activity.Hunt), inbox.Known);
        Assert.DoesNotContain(DispatchKeys.GemFirst, inbox.Known);   // no gem in the file: still news

        // What the file proves is refused as news; what it does not prove is still news.
        Assert.False(inbox.Post(Dispatches.Region("verdant_hollow", Now)));
        Assert.True(inbox.Post(Dispatches.Region("cinderworks", Now)));
        Assert.Equal(1, inbox.Unread);
    }

    [Fact]
    public void test_a_pre_inbox_save_that_banked_no_champions_is_seeded_from_the_legacy_roster()
    {
        var json = """
        { "Version": 7, "SavedAtMs": 1700000000000, "ConqueredRegions": [ "verdant_hollow" ] }
        """;
        var save = SaveSystem.Deserialize(json, Now).Save!;
        var inbox = new Inbox();

        Assert.True(Dispatches.SeedKnown(save, new[] { Activity.Hunt }, inbox));

        // The starter is always on the legacy roster; the seed reads the same rule RestoreCharacters does.
        Assert.Contains(DispatchKeys.HunterJoined(CharacterRoster.StarterId), inbox.Known);
    }

    [Fact]
    public void test_an_inbox_save_is_believed_as_written_an_unread_row_stays_unread_and_nothing_is_seeded()
    {
        var json = """
        {
          "Version": 8,
          "SavedAtMs": 1700000000000,
          "ConqueredRegions": [ "verdant_hollow" ],
          "Dispatches": [
            { "Key": "region.verdant_hollow.conquered", "Kind": "Region", "AtMs": 1700000000000, "Read": false, "RegionId": "verdant_hollow" }
          ],
          "KnownDispatchKeys": [ "region.verdant_hollow.conquered" ]
        }
        """;
        var save = SaveSystem.Deserialize(json, Now).Save!;
        var inbox = new Inbox();
        inbox.Restore(SaveSystem.RestoreDispatches(save), save.KnownDispatchKeys);
        Assert.Equal(1, inbox.Unread);

        Assert.False(Dispatches.SeedKnown(save, new[] { Activity.Training }, inbox));

        Assert.Equal(1, inbox.Unread);
        Assert.False(inbox.Rows.Single().Read);
        Assert.Single(inbox.Known);   // Training was NOT marked known: an honest file is believed

        // ...and an honestly EMPTY inbox at 8 stays empty.
        var emptyJson = """
        { "Version": 8, "SavedAtMs": 1700000000000, "ConqueredRegions": [ "verdant_hollow" ] }
        """;
        var empty = SaveSystem.Deserialize(emptyJson, Now).Save!;
        var inbox2 = new Inbox();
        inbox2.Restore(SaveSystem.RestoreDispatches(empty), empty.KnownDispatchKeys);
        Assert.False(Dispatches.SeedKnown(empty, new[] { Activity.Training }, inbox2));
        Assert.Empty(inbox2.Known);
        Assert.Empty(inbox2.Rows);
    }

    [Fact]
    public void test_a_row_whose_kind_this_build_does_not_know_is_dropped_never_a_crash()
    {
        var json = """
        {
          "Version": 8,
          "SavedAtMs": 1700000000000,
          "Dispatches": [
            { "Key": "prophecy.one", "Kind": "Prophecy", "AtMs": 1700000000000, "Read": false },
            { "Key": "gem.first", "Kind": "Gem", "AtMs": 1700000000001, "Read": false }
          ]
        }
        """;
        var loaded = SaveSystem.Deserialize(json, Now);
        Assert.True(loaded.Ok);

        var rows = SaveSystem.RestoreDispatches(loaded.Save!);

        Assert.Single(rows);
        Assert.Equal(DispatchKind.Gem, rows[0].Kind);
    }

    [Fact]
    public void test_a_duplicated_key_collapses_to_the_first_row_in_file_order()
    {
        var json = """
        {
          "Version": 8,
          "SavedAtMs": 1700000000000,
          "Dispatches": [
            { "Key": "socket.first", "Kind": "Socket", "AtMs": 1700000000000, "Read": true },
            { "Key": "gem.first", "Kind": "Gem", "AtMs": 1700000000001, "Read": false },
            { "Key": "socket.first", "Kind": "Socket", "AtMs": 1700000000002, "Read": false }
          ]
        }
        """;
        var save = SaveSystem.Deserialize(json, Now).Save!;

        var rows = SaveSystem.RestoreDispatches(save);

        Assert.Equal(new[] { "socket.first", "gem.first" }, rows.Select(r => r.Key));
        Assert.True(rows[0].Read);
        Assert.Equal(1700000000000L, rows[0].AtMs);

        var inbox = new Inbox();
        inbox.Restore(rows, save.KnownDispatchKeys);
        Assert.Equal(2, inbox.Count);
        Assert.Equal(1, inbox.Unread);
    }

    [Fact]
    public void test_an_older_save_without_the_fields_has_been_told_nothing_and_opened_nothing()
    {
        var json = """
        { "Version": 3, "SavedAtMs": 1700000000000 }
        """;

        var loaded = SaveSystem.Deserialize(json, Now);

        Assert.True(loaded.Ok);
        Assert.Empty(loaded.Save!.Dispatches);
        Assert.Empty(loaded.Save.KnownDispatchKeys);
        Assert.False(loaded.Save.DispatchesOpenedEver);
    }

    [Fact]
    public void test_a_save_carrying_dispatches_resaves_byte_for_byte()
    {
        var inbox = new Inbox();
        inbox.Post(Dispatches.Region("verdant_hollow", Now));
        inbox.Post(Dispatches.Migration(DispatchKeys.MigrationFifthSlot, Now + 1));
        inbox.MarkRead(DispatchKeys.RegionConquered("verdant_hollow"));
        inbox.Know(DispatchKeys.Unlock(Activity.Training));
        var save = SaveSystem.Capture(new Hunter(), new List<ItemInstance>(), Now) with
        {
            Dispatches = inbox.ToSave(),
            KnownDispatchKeys = inbox.KnownToSave(),
            DispatchesOpenedEver = true,
        };

        var once = SaveSystem.Serialize(save);
        var twice = SaveSystem.Serialize(SaveSystem.Deserialize(once, Now).Save!);

        Assert.Equal(once, twice);
    }
}
