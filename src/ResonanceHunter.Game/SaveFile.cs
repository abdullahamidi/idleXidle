using System;
using ResonanceHunter.Core.Persistence;

namespace ResonanceHunter.Client;

/// <summary>
/// The Game layer's single door to the save file. Core owns the format AND the disk mechanics
/// (<see cref="SaveStore"/>); this resolves WHERE the save lives and forwards.
/// </summary>
/// <remarks>
/// Kept as one door on purpose (ADR-001): screens never touch the disk — they ask the host, the
/// host asks this. The mechanics themselves (atomic write, the save.bak backup, the corrupt-file
/// quarantine) moved into Core's <see cref="SaveStore"/> so they could be unit-tested against a
/// throwaway directory instead of a player's real save.
/// </remarks>
public static class SaveFile
{
    /// <summary>
    /// Where the save lives — LocalApplicationData, unless <c>RH_SAVE_DIR</c> redirects it.
    /// </summary>
    /// <remarks>
    /// The override exists so the save SYSTEM can be exercised without a player's save being the only
    /// available subject — the fresh-launch branch and the corrupt-file branch both need a directory
    /// that is not the real one. It is read every time rather than cached, so a test can point it
    /// somewhere, run, and point it back without the process holding a stale answer.
    /// </remarks>
    private static string Dir
    {
        get
        {
            var dir = Environment.GetEnvironmentVariable("RH_SAVE_DIR");
            if (string.IsNullOrWhiteSpace(dir))
                dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ResonanceHunter");

            return dir;
        }
    }

    public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Write atomically, keeping the previous good file as save.bak — see <see cref="SaveStore.TryWrite"/>.</summary>
    public static bool TryWrite(SaveGame save, out string error) => SaveStore.TryWrite(Dir, save, out error);

    /// <summary>Read the save, or say precisely why not — see <see cref="SaveStore.Read"/>.</summary>
    public static LoadResult Read() => SaveStore.Read(Dir, NowMs);

    /// <summary>Move a damaged save aside as save.corrupt-&lt;time&gt;.json, out of every later write's reach.</summary>
    public static string? QuarantineCorrupt() => SaveStore.QuarantineCorrupt(Dir, DateTimeOffset.UtcNow);

    /// <summary>Move a newer build's save aside as save.newer-&lt;time&gt;.json — kept, never deleted.</summary>
    public static string? QuarantineNewer() => SaveStore.QuarantineNewer(Dir, DateTimeOffset.UtcNow);

    /// <summary>Read the previous good generation (save.bak) — the corrupt-load recovery path.</summary>
    public static LoadResult ReadBackup() => SaveStore.ReadBackup(Dir, NowMs);

    /// <summary>Set the backup aside before a deliberate reset, so fresh autosaves cannot rotate it away.</summary>
    public static string? PreserveBackupAside() => SaveStore.PreserveBackupAside(Dir, DateTimeOffset.UtcNow);

    /// <summary>Delete the live save — the deliberate START A NEW GAME path. Backups and quarantines stay.</summary>
    public static bool TryDelete(out string error) => SaveStore.TryDelete(Dir, out error);

    /// <summary>Copy an older-format save aside before this build's first write migrates it — see <see cref="SaveStore.SnapshotBeforeUpgrade"/>.</summary>
    public static string? SnapshotBeforeUpgrade(int fileVersion) => SaveStore.SnapshotBeforeUpgrade(Dir, fileVersion, DateTimeOffset.UtcNow);

    /// <summary>Must this session refuse to write, given how the load ended?</summary>
    public static bool LocksSaving(LoadFailure failure) => SaveStore.LocksSaving(failure);
}
