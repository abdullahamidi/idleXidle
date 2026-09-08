using System;
using System.IO;
using IdleXIdle.Core.Persistence;
using Xunit;

namespace IdleXIdle.Core.Tests.Persistence;

/// <summary>
/// THE PRODUCT'S RENAME DOES NOT COST ANYONE THEIR GAME.
/// </summary>
/// <remarks>
/// <para>
/// The save folder was the last place the retired product name reached a player's machine. Renaming a
/// save directory is the one change that can silently delete a player's progress — the game finds no
/// save, starts fresh, writes over nothing, and the old files sit on disk unreferenced with no error
/// anywhere. So the rename ships with an adoption, and the adoption ships with this.
/// </para>
/// <para>
/// The rule it states is "take over the old folder ONCE, and only when there is nothing to lose": a
/// new folder that already holds a save is the live game and is never touched, an old folder with no
/// save has nothing to give, and the same path twice is not a migration. It MOVES, so afterwards there
/// is exactly one live save and nobody can edit the copy the game stopped reading.
/// </para>
/// <para>
/// Directories here are real, because that is what the function does; each test makes its own under
/// the system temp and removes it. (The project's no-file-IO rule is about tests reaching a player's
/// real files — this reaches a throwaway path it created, which is how <c>SaveStore</c>'s own atomic
/// write and quarantine are tested too.)
/// </para>
/// </remarks>
public class save_folder_adoption_test : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "idlexidle_adopt_" + Guid.NewGuid().ToString("N"));

    private string Dir(string name)
    {
        var d = Path.Combine(_root, name);
        Directory.CreateDirectory(d);
        return d;
    }

    private static void Write(string dir, string file, string body) => File.WriteAllText(Path.Combine(dir, file), body);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void test_a_players_game_moves_into_the_renamed_folder_whole()
    {
        // Everything in the old folder is the player's: the live save, the rolling backup, the display
        // preference and the dated snapshots a reset left behind.
        var legacy = Dir("old");
        var now = Path.Combine(_root, "new");            // deliberately absent — the first launch after a rename
        Write(legacy, SaveStore.FileName, "{\"Gleam\":42}");
        Write(legacy, SaveStore.BackupFileName, "{\"Gleam\":41}");
        Write(legacy, "display.txt", "150");
        Write(legacy, "save.bak-pre-reset-20260824-165601.json", "{}");

        Assert.True(SaveStore.AdoptOnce(legacy, now));

        Assert.Equal("{\"Gleam\":42}", File.ReadAllText(SaveStore.SavePath(now)));
        Assert.Equal("{\"Gleam\":41}", File.ReadAllText(SaveStore.BackupPath(now)));
        Assert.Equal("150", File.ReadAllText(Path.Combine(now, "display.txt")));
        Assert.True(File.Exists(Path.Combine(now, "save.bak-pre-reset-20260824-165601.json")));

        // MOVED, not copied: one live save afterwards, so nobody plays the copy the game stopped reading.
        Assert.False(File.Exists(SaveStore.SavePath(legacy)));
    }

    [Fact]
    public void test_a_folder_that_already_holds_a_game_is_never_touched()
    {
        // THE DANGEROUS CASE. If the new folder has a save, that save is the live game — adopting over
        // it would hand the player an older self, which is the very failure this migration exists to
        // avoid. Nothing moves, and the old folder is left exactly as it was.
        var legacy = Dir("old");
        var now = Dir("new");
        Write(legacy, SaveStore.FileName, "{\"Gleam\":1}");
        Write(now, SaveStore.FileName, "{\"Gleam\":999}");

        Assert.False(SaveStore.AdoptOnce(legacy, now));

        Assert.Equal("{\"Gleam\":999}", File.ReadAllText(SaveStore.SavePath(now)));
        Assert.Equal("{\"Gleam\":1}", File.ReadAllText(SaveStore.SavePath(legacy)));
    }

    [Fact]
    public void test_adoption_is_once_and_is_safe_to_repeat()
    {
        // The launch path may ask more than once (a redirected run, a second process); the second answer
        // must be "nothing to do" rather than a second move.
        var legacy = Dir("old");
        var now = Path.Combine(_root, "new");
        Write(legacy, SaveStore.FileName, "{\"Gleam\":7}");

        Assert.True(SaveStore.AdoptOnce(legacy, now));
        Assert.False(SaveStore.AdoptOnce(legacy, now));
        Assert.Equal("{\"Gleam\":7}", File.ReadAllText(SaveStore.SavePath(now)));
    }

    [Fact]
    public void test_nothing_to_adopt_is_not_an_error()
    {
        // A fresh install has no old folder at all, and an old folder with no save has nothing to give.
        var now = Path.Combine(_root, "new");
        Assert.False(SaveStore.AdoptOnce(Path.Combine(_root, "never_existed"), now));

        var empty = Dir("empty");
        Assert.False(SaveStore.AdoptOnce(empty, now));
        Assert.False(File.Exists(SaveStore.SavePath(now)));

        // And a folder cannot adopt itself.
        var same = Dir("same");
        Write(same, SaveStore.FileName, "{}");
        Assert.False(SaveStore.AdoptOnce(same, same));
        Assert.True(File.Exists(SaveStore.SavePath(same)));
    }

    [Fact]
    public void test_an_adopted_save_reads_back_as_the_same_game()
    {
        // The whole point, end to end: what the player had is what the game loads afterwards.
        var legacy = Dir("old");
        var now = Path.Combine(_root, "new");
        var save = new SaveGame { SavedAtMs = 1_700_000_000_000L, Gleam = 12_345 };
        Assert.True(SaveStore.TryWrite(legacy, save, out _));

        Assert.True(SaveStore.AdoptOnce(legacy, now));

        var loaded = SaveStore.Read(now, 1_700_000_000_000L);
        Assert.Equal(LoadFailure.None, loaded.Failure);
        Assert.Equal(12_345, loaded.Save!.Gleam);
    }
}
