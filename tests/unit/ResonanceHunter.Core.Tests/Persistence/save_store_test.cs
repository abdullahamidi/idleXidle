using System;
using System.IO;
using ResonanceHunter.Core.Persistence;
using Xunit;

namespace ResonanceHunter.Core.Tests.Persistence;

/// <summary>
/// The DISK half of saving: the atomic write, the rolling backup, and what happens to a file that
/// cannot be read. The promises here are the ones the boot message makes to the player.
/// </summary>
/// <remarks>
/// <para>
/// REGRESSION SUITE for the broken corrupt-save promise: the boot message said "your old file has
/// not been overwritten" while the ten-second autosave overwrote it with a blank game. The fix has
/// three parts — a write latch (policy, <see cref="SaveStore.LocksSaving"/>), a quarantine rename,
/// and a per-write backup — and each part is pinned below.
/// </para>
/// <para>
/// These tests touch the file system ON PURPOSE, despite the no-file-IO rule for unit tests: the
/// subject IS the file-system behaviour (a rename, a backup, an atomic replace), which no pure
/// function can witness. Each test owns a throwaway directory under the OS temp path and deletes it
/// afterwards, so they stay isolated, order-independent and deterministic — which is what the rule
/// exists to protect.
/// </para>
/// </remarks>
public class SaveStoreTest : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "rh_savestore_" + Guid.NewGuid().ToString("N"));

    private const long Now = 1_700_000_000_000L;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void test_save_store_a_corrupt_file_is_renamed_aside_never_overwritten()
    {
        // Arrange — a damaged save on disk.
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SaveStore.SavePath(_dir), "{ not json ]]");

        // Act — the load fails, the file is quarantined, and a NEW game saves afterwards.
        var result = SaveStore.Read(_dir, Now);
        var kept = SaveStore.QuarantineCorrupt(_dir, new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));
        var wrote = SaveStore.TryWrite(_dir, new SaveGame { SavedAtMs = Now }, out var error);

        // Assert — the damaged bytes survive under the quarantine name, even after a later write.
        Assert.Equal(LoadFailure.Corrupt, result.Failure);
        Assert.NotNull(kept);
        Assert.StartsWith("save.corrupt-", Path.GetFileName(kept!));
        Assert.Equal("{ not json ]]", File.ReadAllText(kept!));
        Assert.True(wrote, error);
        Assert.True(SaveStore.Read(_dir, Now).Ok);                 // the fresh save is a separate file
        Assert.Equal("{ not json ]]", File.ReadAllText(kept!));    // the evidence is still untouched
    }

    [Fact]
    public void test_save_store_two_quarantines_in_the_same_second_do_not_collide()
    {
        // Arrange — the same wall-clock second, twice.
        var clock = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SaveStore.SavePath(_dir), "first corpse");
        var first = SaveStore.QuarantineCorrupt(_dir, clock);
        File.WriteAllText(SaveStore.SavePath(_dir), "second corpse");

        // Act
        var second = SaveStore.QuarantineCorrupt(_dir, clock);

        // Assert — two distinct files, neither overwritten.
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
        Assert.Equal("first corpse", File.ReadAllText(first!));
        Assert.Equal("second corpse", File.ReadAllText(second!));
    }

    [Fact]
    public void test_save_store_the_second_write_keeps_the_previous_good_file_as_backup()
    {
        // Arrange / Act — two generations of save.
        var first = new SaveGame { SavedAtMs = Now, Gleam = 111 };
        var second = new SaveGame { SavedAtMs = Now + 1, Gleam = 222 };
        Assert.True(SaveStore.TryWrite(_dir, first, out var e1), e1);
        Assert.True(SaveStore.TryWrite(_dir, second, out var e2), e2);

        // Assert — the live file is the second; the backup is the FIRST, intact and loadable.
        Assert.Equal(222, SaveStore.Read(_dir, Now + 1).Save!.Gleam);
        Assert.True(File.Exists(SaveStore.BackupPath(_dir)));
        var backup = SaveSystem.Deserialize(File.ReadAllText(SaveStore.BackupPath(_dir)), Now);
        Assert.True(backup.Ok);
        Assert.Equal(111, backup.Save!.Gleam);
    }

    [Fact]
    public void test_save_store_a_newer_version_save_reports_from_newer_and_locks_saving()
    {
        // Arrange — a file written by a future build.
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SaveStore.SavePath(_dir),
            SaveSystem.Serialize(new SaveGame { Version = SaveGame.CurrentVersion + 1, SavedAtMs = Now }));

        // Act
        var result = SaveStore.Read(_dir, Now);

        // Assert — refused, and the policy says this session must not write. Corrupt locks too;
        // Missing and None do not — a first launch has nothing to protect.
        Assert.Equal(LoadFailure.FromNewerVersion, result.Failure);
        Assert.True(SaveStore.LocksSaving(LoadFailure.FromNewerVersion));
        Assert.True(SaveStore.LocksSaving(LoadFailure.Corrupt));
        Assert.False(SaveStore.LocksSaving(LoadFailure.Missing));
        Assert.False(SaveStore.LocksSaving(LoadFailure.None));
    }

    [Fact]
    public void test_save_store_delete_removes_the_save_and_a_fresh_read_reports_missing()
    {
        // Arrange — a save and a backup on disk (the START A NEW GAME state).
        Assert.True(SaveStore.TryWrite(_dir, new SaveGame { SavedAtMs = Now, Gleam = 1 }, out _));
        Assert.True(SaveStore.TryWrite(_dir, new SaveGame { SavedAtMs = Now + 1, Gleam = 2 }, out _));

        // Act
        var deleted = SaveStore.TryDelete(_dir, out var error);

        // Assert — the live save is gone; the backup deliberately survives the player's choice.
        Assert.True(deleted, error);
        Assert.Equal(LoadFailure.Missing, SaveStore.Read(_dir, Now).Failure);
        Assert.True(File.Exists(SaveStore.BackupPath(_dir)));
    }

    [Fact]
    public void test_save_store_a_missing_directory_reads_as_missing_not_a_crash()
    {
        // Arrange / Act / Assert — the very first launch on a machine.
        Assert.Equal(LoadFailure.Missing, SaveStore.Read(_dir, Now).Failure);
    }
}
