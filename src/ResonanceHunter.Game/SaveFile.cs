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
    private static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ResonanceHunter",
        "save.json");

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
