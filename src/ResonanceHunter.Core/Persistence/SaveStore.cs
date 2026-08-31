using System;
using System.IO;

namespace ResonanceHunter.Core.Persistence;

/// <summary>
/// The disk half of saving: atomic writes, a rolling backup, and quarantine for files that cannot
/// be read. Parameterised by directory so every behaviour here is testable against a scratch folder.
/// </summary>
/// <remarks>
/// <para>
/// Lives in Core beside the format (<see cref="SaveSystem"/>) rather than in the Game project,
/// because the disk behaviour is exactly the part that once broke silently: the boot message
/// promised a damaged file had not been overwritten while the ten-second autosave overwrote it with
/// a blank game. Promises need tests, and the Game assembly has no test project — so the mechanics
/// moved here, and the Game layer's <c>SaveFile</c> only resolves WHERE the save lives.
/// </para>
/// <para>
/// Three rules, each existing because its absence lost (or nearly lost) a real save:
/// (1) WRITES ARE ATOMIC — temp file, then swap — so a crash mid-write leaves the old save, never a
/// torn half of both. (2) EVERY WRITE KEEPS THE PREVIOUS GOOD FILE as <c>save.bak</c>, so even a bug
/// that writes valid-but-wrong data leaves one generation to recover. (3) A FILE THAT CANNOT BE READ
/// is renamed aside (<c>save.corrupt-&lt;time&gt;.json</c>), never deleted and never overwritten —
/// it is the player's hours, and it is the evidence.
/// </para>
/// </remarks>
public static class SaveStore
{
    /// <summary>The live save's file name.</summary>
    public const string FileName = "save.json";

    /// <summary>The previous good save, refreshed on every write.</summary>
    public const string BackupFileName = "save.bak";

    /// <summary>Full path of the live save inside <paramref name="dir"/>.</summary>
    public static string SavePath(string dir) => Path.Combine(dir, FileName);

    /// <summary>Full path of the rolling backup inside <paramref name="dir"/>.</summary>
    public static string BackupPath(string dir) => Path.Combine(dir, BackupFileName);

    /// <summary>Must a session refuse to write, given how its load ended?</summary>
    /// <remarks>
    /// A session that could not READ the player's file has nothing but a blank game in memory, and
    /// writing that over the file it failed to read is indistinguishable from deleting the player's
    /// progress. Missing is the one failure that genuinely means "nothing to protect".
    /// </remarks>
    public static bool LocksSaving(LoadFailure failure)
        => failure is LoadFailure.Corrupt or LoadFailure.FromNewerVersion;

    /// <summary>
    /// Write atomically: temp file, then swap — keeping the file being replaced as <c>save.bak</c>.
    /// </summary>
    /// <remarks>
    /// A crash or power cut mid-write would otherwise leave a half-written JSON file — and the player
    /// would lose everything, having done nothing wrong. The swap means the real save is either the
    /// old one or the new one, never a torn mixture; the backup means even the "old one" survives the
    /// swap by one generation.
    /// </remarks>
    public static bool TryWrite(string dir, SaveGame save, out string error)
    {
        error = "";
        try
        {
            Directory.CreateDirectory(dir);
            var path = SavePath(dir);
            var temp = path + ".tmp";
            File.WriteAllText(temp, SaveSystem.Serialize(save));

            if (File.Exists(path))
            {
                try
                {
                    // One call: the old file becomes the backup and the temp becomes the save.
                    File.Replace(temp, path, BackupPath(dir), ignoreMetadataErrors: true);
                }
                catch (PlatformNotSupportedException)
                {
                    // A filesystem without native replace still gets both halves, in two steps.
                    File.Copy(path, BackupPath(dir), overwrite: true);
                    File.Move(temp, path, overwrite: true);
                }
            }
            else
            {
                File.Move(temp, path);
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Read the save in <paramref name="dir"/>, or say precisely why not.</summary>
    public static LoadResult Read(string dir, long nowMs)
    {
        try
        {
            var path = SavePath(dir);
            if (!File.Exists(path)) return new LoadResult { Failure = LoadFailure.Missing };
            return SaveSystem.Deserialize(File.ReadAllText(path), nowMs);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new LoadResult { Failure = LoadFailure.Corrupt };
        }
    }

    /// <summary>
    /// Move a damaged save aside as <c>save.corrupt-&lt;time&gt;.json</c>. Returns the new path, or
    /// null if there was nothing to move (or the move itself failed — the file is then left alone).
    /// </summary>
    /// <remarks>
    /// Renamed, never deleted: the file is the player's hours and the developer's evidence. Renaming
    /// also means the NEXT boot finds no save and starts a fresh, saveable game, instead of hitting
    /// the same unreadable file and locking every session forever.
    /// </remarks>
    public static string? QuarantineCorrupt(string dir, DateTimeOffset utcNow)
        => MoveAside(SavePath(dir), Path.Combine(dir, $"save.corrupt-{utcNow:yyyyMMdd-HHmmss}"));

    /// <summary>
    /// Move a save from a NEWER build aside as <c>save.newer-&lt;time&gt;.json</c>.
    /// </summary>
    /// <remarks>
    /// The file is perfectly good — the newer build it belongs to can read it — so START A NEW GAME
    /// must not delete it (review 2026-08-23, high: the title screen promises "your old file is kept
    /// on disk, untouched", and the reset then File.Delete'd it). Renaming keeps the promise and
    /// still leaves the live path clear for the fresh game.
    /// </remarks>
    public static string? QuarantineNewer(string dir, DateTimeOffset utcNow)
        => MoveAside(SavePath(dir), Path.Combine(dir, $"save.newer-{utcNow:yyyyMMdd-HHmmss}"));

    /// <summary>
    /// Read the PREVIOUS good generation — <c>save.bak</c>. The recovery half of the backup TryWrite
    /// keeps (review 2026-08-23, high: the backup was written on every save and read by nothing).
    /// </summary>
    public static LoadResult ReadBackup(string dir, long nowMs)
    {
        try
        {
            var path = BackupPath(dir);
            if (!File.Exists(path)) return new LoadResult { Failure = LoadFailure.Missing };
            return SaveSystem.Deserialize(File.ReadAllText(path), nowMs);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new LoadResult { Failure = LoadFailure.Corrupt };
        }
    }

    /// <summary>
    /// Set the backup aside as <c>save.bak-pre-reset-&lt;time&gt;</c> before a deliberate reset, so the
    /// fresh game's autosaves cannot rotate the last real generation away twenty seconds later.
    /// </summary>
    public static string? PreserveBackupAside(string dir, DateTimeOffset utcNow)
        => MoveAside(BackupPath(dir), Path.Combine(dir, $"save.bak-pre-reset-{utcNow:yyyyMMdd-HHmmss}"));

    /// <summary>
    /// Copy an OLDER-format save aside as <c>save.pre-v&lt;current&gt;-&lt;time&gt;.json</c> before this
    /// build's first write migrates it. Once per format version — a later boot that finds a
    /// snapshot for the current version does nothing.
    /// </summary>
    /// <remarks>
    /// The rolling backup is one generation deep and the autosave runs every ten seconds, so after
    /// a format upgrade the last old-format file leaves <c>save.bak</c> within about twenty seconds
    /// — which was the entire window in which a bad migration stayed recoverable (2026-08-31
    /// audit). This snapshot is the migration's evidence and its undo. A COPY, never a move: the
    /// live file goes on being the live file.
    /// </remarks>
    public static string? SnapshotBeforeUpgrade(string dir, int fileVersion, DateTimeOffset utcNow)
    {
        try
        {
            if (fileVersion >= SaveGame.CurrentVersion) return null;   // nothing is about to migrate
            var path = SavePath(dir);
            if (!File.Exists(path)) return null;
            if (Directory.GetFiles(dir, $"save.pre-v{SaveGame.CurrentVersion}-*.json").Length > 0)
                return null;                                           // this upgrade is already witnessed
            var target = Path.Combine(dir, $"save.pre-v{SaveGame.CurrentVersion}-{utcNow:yyyyMMdd-HHmmss}.json");
            File.Copy(path, target);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Rename a file out of every later write's reach. Null if absent or the move failed.</summary>
    private static string? MoveAside(string path, string stem)
    {
        try
        {
            if (!File.Exists(path)) return null;

            var target = stem + ".json";
            // Two moves inside one second must not collide — the second would throw and leave the
            // file in place, exactly where the next write could reach it.
            for (var n = 2; File.Exists(target); n++) target = $"{stem}-{n}.json";

            File.Move(path, target);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Delete the live save — the deliberate START A NEW GAME path, never an error path.
    /// </summary>
    /// <remarks>
    /// The backup and any quarantined files stay: deleting the present is a choice the player made;
    /// deleting the past would be one they did not.
    /// </remarks>
    public static bool TryDelete(string dir, out string error)
    {
        error = "";
        try
        {
            var path = SavePath(dir);
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }
}
