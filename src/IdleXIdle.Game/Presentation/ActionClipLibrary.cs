using System;
using System.Collections.Generic;
using System.IO;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// The authored timing files (<c>*.clip.json</c>) that sit beside action strips, keyed by the strip key they
/// time. Read once, on first use, from the build's <c>assets/art</c> folder (the csproj copies them).
/// </summary>
/// <remarks>
/// A strip with no timing file is played the old way, frame by equal frame with contact on frame 5 — so the
/// file's presence is the whole switch, and each champion's actions can be moved over one at a time.
/// </remarks>
public static class ActionClipLibrary
{
    private static Dictionary<string, ActionClipTiming>? _byStrip;

    /// <summary>The timing authored for <paramref name="stripKey"/>, or null: that clip keeps the old timing.</summary>
    public static ActionClipTiming? For(string stripKey)
    {
        _byStrip ??= Load(Path.Combine(AppContext.BaseDirectory, "assets", "art"));
        return _byStrip.GetValueOrDefault(stripKey);
    }

    /// <summary>Read every <c>*.clip.json</c> under <paramref name="root"/>, keyed by the strip name it sits beside.</summary>
    public static Dictionary<string, ActionClipTiming> Load(string root)
    {
        var map = new Dictionary<string, ActionClipTiming>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root)) return map;
        foreach (var path in Directory.EnumerateFiles(root, "*.clip.json", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(path);
            var key = name[..^".clip.json".Length];
            try { map[key] = ActionClipTiming.Parse(File.ReadAllText(path)); }
            catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or KeyNotFoundException or ArgumentException or InvalidOperationException)
            {
                // A broken timing file must not take the fight down: that clip plays the old way, loudly.
                Console.Error.WriteLine($"clip timing '{name}' ignored: {ex.Message}");
            }
        }
        return map;
    }
}
