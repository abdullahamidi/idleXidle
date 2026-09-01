using System;
using System.Collections.Generic;
using System.IO;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;
using Xunit;

namespace IdleXIdle.Core.Tests.Persistence;

/// <summary>
/// LOAD MUST NEVER THROW — step 0 of the 2026-08-31 refactor.
/// </summary>
/// <remarks>
/// <para>
/// The audit found two live boot-crash paths in the load: a strict <c>Enum.Parse&lt;ItemBaseType&gt;</c>
/// inside the item restore, and a bare dictionary index for every region id in
/// <c>SaveGame.RegionFarms</c>. Both sat outside any try/catch on the host's load path — one unknown
/// name in an old, edited, or future save took the game down BEFORE THE WINDOW OPENED, the exact
/// failure shape no screenshot or capture can reach (the rig deliberately skips the load).
/// </para>
/// <para>
/// The rule these tests pin: an unknown catalogue name in the save DEGRADES (the row is dropped, the
/// value clamps) exactly like every other lenient field in the format — it never throws. The whole
/// refactor renames content on top of this rule, so it must hold before anything else moves.
/// </para>
/// </remarks>
public class SaveArmourTest
{
    private const long Now = 1_700_000_000_000L;

    [Fact]
    public void test_an_item_with_an_unknown_base_type_is_dropped_not_a_boot_crash()
    {
        // Arrange — a save holding one real item and one from a retired/future catalogue.
        var save = new SaveGame
        {
            SavedAtMs = Now,
            Inventory = new List<SavedItem>
            {
                new() { InstanceId = "keep", BaseType = "Weapon", Rarity = 2, SellValue = 10 },
                new() { InstanceId = "drop", BaseType = "HatcheryEgg", Rarity = 1, SellValue = 5 },
            },
        };

        // Act — the exact call the host makes at boot.
        var items = SaveSystem.RestoreInventory(save);

        // Assert — the stranger is dropped; the rest of the bag survives.
        Assert.Single(items);
        Assert.Equal("keep", items[0].InstanceId);
    }

    [Fact]
    public void test_a_gem_with_an_unknown_base_type_is_dropped_from_its_host_item()
    {
        // Arrange — a worn weapon whose socket holds one real gem and one unknown relic.
        var save = new SaveGame
        {
            SavedAtMs = Now,
            Inventory = new List<SavedItem>
            {
                new()
                {
                    InstanceId = "host", BaseType = "Weapon", Rarity = 3, SellValue = 40,
                    Gems = new List<SavedItem>
                    {
                        new() { InstanceId = "g1", BaseType = "Gem", Rarity = 1, SellValue = 3 },
                        new() { InstanceId = "g2", BaseType = "SoulShard", Rarity = 1, SellValue = 3 },
                    },
                },
            },
        };

        // Act
        var items = SaveSystem.RestoreInventory(save);

        // Assert — the host survives with its one real gem; the load did not throw.
        Assert.Single(items);
        Assert.Single(items[0].Gems);
        Assert.Equal("g1", items[0].Gems[0].InstanceId);
    }

    [Fact]
    public void test_an_out_of_range_rarity_ordinal_is_clamped_into_the_ladder()
    {
        // Arrange — rarity is persisted as an ordinal; a hand-edited or future save can carry any int.
        var save = new SaveGame
        {
            SavedAtMs = Now,
            Inventory = new List<SavedItem>
            {
                new() { InstanceId = "high", BaseType = "Weapon", Rarity = 99, SellValue = 1 },
                new() { InstanceId = "low", BaseType = "Charm", Rarity = -3, SellValue = 1 },
            },
        };

        // Act
        var items = SaveSystem.RestoreInventory(save);

        // Assert — clamped to the ladder's ends, never an undefined enum value riding into a
        // colour-table index.
        Assert.Equal(Rarity.Legendary, items[0].Rarity);
        Assert.Equal(Rarity.Common, items[1].Rarity);
    }

    [Fact]
    public void test_a_region_farm_row_with_an_unknown_region_id_is_dropped_not_a_boot_crash()
    {
        // Arrange — a save from a build whose region list included one this build does not know.
        var world = new World();
        var save = new SaveGame
        {
            SavedAtMs = Now,
            RegionFarms = new List<RegionFarmSave>
            {
                new() { Id = VerdantHollow.RegionId, MasteryPoints = 120f, BestDepth = 9, StartWave = 5 },
                new() { Id = "sunken_atoll", MasteryPoints = 999f, BestDepth = 40 },
            },
        };

        // Act — must not throw (the old host code indexed the region table directly and did).
        SaveSystem.RestoreWorld(save, world);

        // Assert — the known region's progress is fully back; the stranger is simply gone.
        Assert.Equal(9, world.RegionFarm(VerdantHollow.RegionId).BestDepth);
    }

    [Fact]
    public void test_restore_world_brings_back_conquest_and_corruption_and_ignores_unknown_conquests()
    {
        // Arrange
        var world = new World();
        var save = new SaveGame
        {
            SavedAtMs = Now,
            ConqueredRegions = new List<string> { VerdantHollow.RegionId, "drowned_spire" },
            CorruptionTier = 1,
            CorruptionPeak = 2,
        };

        // Act
        SaveSystem.RestoreWorld(save, world);

        // Assert
        Assert.True(world.IsConquered(VerdantHollow.RegionId));
        Assert.False(world.IsConquered("drowned_spire"));
        Assert.Equal(1, world.CorruptionTier);
        Assert.Equal(2, world.PeakCorruptionTier);
    }

    [Fact]
    public void test_a_save_with_no_region_farms_folds_its_single_mastery_into_the_home_region()
    {
        // Arrange — the pre-multi-region shape: one bare mastery number, no farm records.
        var world = new World();
        var save = new SaveGame { SavedAtMs = Now, RegionMasteryPoints = 333f };

        // Act
        SaveSystem.RestoreWorld(save, world);

        // Assert
        Assert.Equal(333f, world.RegionFarm(VerdantHollow.RegionId).RegionMasteryPoints, 1);
    }
}

/// <summary>
/// The pre-migration snapshot: an older-format save is copied aside before this build's first write
/// can migrate it. File-based on purpose, like <see cref="SaveStoreTest"/> — the subject IS the disk.
/// </summary>
public class SaveSnapshotTest : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "rh_snapshot_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void test_an_older_format_save_is_snapshotted_once_before_migration()
    {
        // Arrange — an old-format file on disk, as the first boot after an upgrade finds it.
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SaveStore.SavePath(_dir), "{\"Version\": 1, \"SavedAtMs\": 1}");
        var when = new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

        // Act — the first boot snapshots; the second finds the witness and does nothing.
        var first = SaveStore.SnapshotBeforeUpgrade(_dir, 1, when);
        var second = SaveStore.SnapshotBeforeUpgrade(_dir, 1, when.AddMinutes(1));

        // Assert — one copy, named for the version being migrated TO, live file untouched.
        Assert.NotNull(first);
        Assert.StartsWith($"save.pre-v{SaveGame.CurrentVersion}-", Path.GetFileName(first!));
        Assert.Contains("\"Version\": 1", File.ReadAllText(first!));
        Assert.Null(second);
        Assert.True(File.Exists(SaveStore.SavePath(_dir)));
    }

    [Fact]
    public void test_a_current_format_save_is_not_snapshotted()
    {
        // Arrange
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SaveStore.SavePath(_dir), "{}");

        // Act / Assert — nothing is about to migrate, so nothing is copied.
        Assert.Null(SaveStore.SnapshotBeforeUpgrade(_dir, SaveGame.CurrentVersion, DateTimeOffset.UtcNow));
        Assert.Empty(Directory.GetFiles(_dir, "save.pre-v*"));
    }

    [Fact]
    public void test_a_missing_save_is_not_snapshotted()
    {
        // Arrange / Act / Assert — a fresh machine has nothing to witness; also must not throw.
        Assert.Null(SaveStore.SnapshotBeforeUpgrade(_dir, 0, DateTimeOffset.UtcNow));
    }
}
