using System;
using System.IO;
using ResonanceHunter.Core.Persistence;

namespace ResonanceHunter.Client;

/// <summary>
/// The file-system half of saving. Core owns the format; this owns the disk.
/// </summary>
/// <remarks>
/// Kept out of Core deliberately (ADR-001) so the save FORMAT can be unit-tested without any file I/O,
/// which the project's test standards forbid in unit tests.
/// </remarks>
public static class SaveFile
{
    /// <summary>
    /// Where the save lives — LocalApplicationData, unless <c>RH_SAVE_DIR</c> redirects it.
    /// </summary>
    /// <remarks>
    /// The override exists so the save SYSTEM can be exercised without a player's save being the only
    /// available subject. Two paths matter and neither could be reached safely before: a FIRST launch
    /// with no file at all — the branch that seeds a new game, and the branch whose early return hid a
    /// startup crash for the whole of development — and a corrupt file, where the rule is that a bad
    /// save is never silently replaced. Testing either meant moving the real save aside, which is a
    /// procedure that only has to go wrong once.
    ///
    /// It is read every time rather than cached, so a test can point it somewhere, run, and point it
    /// back without the process holding a stale answer.
    /// </remarks>
    private static string Path
    {
        get
        {
            var dir = Environment.GetEnvironmentVariable("RH_SAVE_DIR");
            if (string.IsNullOrWhiteSpace(dir))
                dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ResonanceHunter");

            return System.IO.Path.Combine(dir, "save.json");
        }
    }

    public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Write atomically: to a temp file, then replace.
    /// </summary>
    /// <remarks>
    /// A crash or power cut mid-write would otherwise leave a half-written JSON file — and the player
    /// would lose everything, having done nothing wrong. Writing to a temp file and moving it means the
    /// real save is either the old one or the new one, never a torn mixture of both.
    /// </remarks>
    public static bool TryWrite(SaveGame save, out string error)
    {
        error = "";

        try
        {
            var dir = System.IO.Path.GetDirectoryName(Path)!;
            Directory.CreateDirectory(dir);

            var temp = Path + ".tmp";
            File.WriteAllText(temp, SaveSystem.Serialize(save));

            File.Move(temp, Path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    public static LoadResult Read()
    {
        try
        {
            if (!File.Exists(Path)) return new LoadResult { Failure = LoadFailure.Missing };
            return SaveSystem.Deserialize(File.ReadAllText(Path), NowMs);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new LoadResult { Failure = LoadFailure.Corrupt };
        }
    }
}
