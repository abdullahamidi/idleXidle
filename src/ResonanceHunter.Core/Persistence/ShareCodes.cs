using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Persistence;

/// <summary>
/// Share codes — an item or a build as one copy-pasteable line. Show, don't trade.
/// </summary>
/// <remarks>
/// <para>
/// The second future-content direction the designer kept (2026-08-20): importing a code lets you
/// INSPECT the thing — it never enters your inventory and never changes your build. The grind
/// stays intact; the flex travels over Discord for free.
/// </para>
/// <para>
/// Format: <c>RHI1.&lt;payload&gt;.&lt;check&gt;</c> (item) / <c>RHB1.&lt;payload&gt;.&lt;check&gt;</c>
/// (build). Payload is JSON → Deflate → base64-url; the check is an FNV-1a hash of the payload so a
/// truncated paste fails LOUDLY instead of half-decoding. The version digit is in the prefix: a
/// future format bumps to RHI2 and an old client says "newer version" instead of guessing. Items
/// round-trip through the SAVE's own DTO (<see cref="SaveSystem.ToSavedItem"/>), so a shared item
/// gets the same lenient parsing and legacy migrations as a loaded save.
/// </para>
/// <para>
/// Errors are player-facing sentences (plain English, no jargon) — the UI shows them verbatim.
/// </para>
/// </remarks>
public static class ShareCodes
{
    private const string ItemPrefix = "RHI";
    private const string BuildPrefix = "RHB";
    private const int Version = 1;

    /// <summary>A build worth sharing: the four woven skills, the keystones, the mastery walk.</summary>
    /// <remarks>No stats, no gear, no names — identity, not power, and nothing a player types.</remarks>
    public sealed record SharedBuild
    {
        public List<SavedSkill> Skills { get; init; } = new();
        public List<string> Keystones { get; init; } = new();
        public List<string> Mastery { get; init; } = new();
    }

    public static string EncodeItem(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Encode(ItemPrefix, JsonSerializer.Serialize(SaveSystem.ToSavedItem(item)));
    }

    public static string EncodeBuild(SharedBuild build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return Encode(BuildPrefix, JsonSerializer.Serialize(build));
    }

    /// <summary>Which kind of code this is, if any — the UI's dispatcher.</summary>
    public static bool LooksLikeItem(string? code) => code?.TrimStart().StartsWith(ItemPrefix, StringComparison.Ordinal) == true;
    public static bool LooksLikeBuild(string? code) => code?.TrimStart().StartsWith(BuildPrefix, StringComparison.Ordinal) == true;

    public static bool TryDecodeItem(string? code, out ItemInstance? item, out string error)
    {
        item = null;
        if (!TryDecode(ItemPrefix, code, out var json, out error)) return false;
        try
        {
            var saved = JsonSerializer.Deserialize<SavedItem>(json);
            if (saved is null) { error = DamagedError; return false; }
            item = SaveSystem.FromSavedItem(saved);
            return true;
        }
        catch (JsonException)
        {
            error = DamagedError;
            return false;
        }
    }

    public static bool TryDecodeBuild(string? code, out SharedBuild? build, out string error)
    {
        build = null;
        if (!TryDecode(BuildPrefix, code, out var json, out error)) return false;
        try
        {
            build = JsonSerializer.Deserialize<SharedBuild>(json);
            if (build is null) { error = DamagedError; return false; }
            return true;
        }
        catch (JsonException)
        {
            error = DamagedError;
            return false;
        }
    }

    private const string DamagedError = "THE CODE IS DAMAGED — COPY THE WHOLE LINE AND TRY AGAIN.";

    private static string Encode(string prefix, string json)
    {
        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(Encoding.UTF8.GetBytes(json));
        var payload = Base64Url(buffer.ToArray());
        return $"{prefix}{Version}.{payload}.{Fnv(payload):x8}";
    }

    private static bool TryDecode(string prefix, string? code, out string json, out string error)
    {
        json = "";
        error = "";
        code = code?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length < prefix.Length + 2
            || !code.StartsWith(prefix, StringComparison.Ordinal))
        {
            error = "THIS IS NOT A SHARE CODE.";
            return false;
        }

        var parts = code.Split('.');
        if (parts.Length != 3) { error = DamagedError; return false; }

        if (!int.TryParse(parts[0].AsSpan(prefix.Length), out var version) || version > Version)
        {
            error = "THIS CODE COMES FROM A NEWER VERSION OF THE GAME.";
            return false;
        }

        if (!string.Equals(parts[2], $"{Fnv(parts[1]):x8}", StringComparison.Ordinal))
        {
            error = DamagedError;
            return false;
        }

        try
        {
            using var input = new MemoryStream(FromBase64Url(parts[1]));
            using var inflate = new DeflateStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(inflate, Encoding.UTF8);
            json = reader.ReadToEnd();
            return true;
        }
        catch (Exception e) when (e is FormatException or InvalidDataException)
        {
            error = DamagedError;
            return false;
        }
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string s)
    {
        var b64 = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '='));
    }

    private static uint Fnv(string s)
    {
        var h = 2166136261u;
        foreach (var c in s) h = (h ^ c) * 16777619u;
        return h;
    }
}
