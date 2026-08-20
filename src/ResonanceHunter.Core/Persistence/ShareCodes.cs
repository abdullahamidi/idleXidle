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

    // A legitimate code is a few hundred bytes; deflate expands up to ~1000:1. Without these caps a
    // one-megabyte pasted "code" could balloon toward a gigabyte of string from a single click.
    private const int MaxCodeChars = 16_000;
    private const int MaxInflatedChars = 262_144;

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
        }
        // BROAD on purpose: the checksum proves the paste survived transit, not that the payload is
        // honest — the FNV algorithm ships in the binary, so a crafted code passes it. FromSavedItem
        // throws ArgumentException on an unknown BaseType and NREs on an explicit-null Gems list
        // (measured, not guessed), and a paste must NEVER be able to crash the game.
        catch (Exception e) when (e is JsonException or ArgumentException or NullReferenceException
                                  or InvalidOperationException or OverflowException)
        {
            error = DamagedError;
            return false;
        }

        if (!ValidItem(item))
        {
            item = null;
            error = DamagedError;
            return false;
        }
        return true;
    }

    /// <summary>Range-checks a decoded item, recursively through its gems — lenient parsing can
    /// still produce out-of-range enums ((Rarity)77 draws by INDEXING a colour table) and absurd
    /// magnitudes, and the inspect UI must be handed only things the game itself could mint.</summary>
    private static bool ValidItem(ItemInstance i)
        => !string.IsNullOrEmpty(i.InstanceId) && i.InstanceId.Length <= 64
           && Enum.IsDefined(i.BaseType) && Enum.IsDefined(i.Rarity)
           && (i.Element is not { } el || Enum.IsDefined(el))
           && (i.TraitOverride is not { } tr || Enum.IsDefined(tr))
           && (i.EnchantOverride is not { } en || Enum.IsDefined(en))
           && i.ItemLevel is >= 1 and <= 9_999
           && i.Upgrades is >= 0 and <= 99
           && i.SellValue is >= 0 and <= 1_000_000
           && i.Gems is not null && i.Gems.Count <= 8 && i.Gems.All(ValidItem);

    public static bool TryDecodeBuild(string? code, out SharedBuild? build, out string error)
    {
        build = null;
        if (!TryDecode(BuildPrefix, code, out var json, out error)) return false;
        try
        {
            build = JsonSerializer.Deserialize<SharedBuild>(json);
        }
        catch (JsonException)
        {
            error = DamagedError;
            return false;
        }

        // `required` in System.Text.Json checks PRESENCE, not non-null: {"Skills":null} and
        // {"Source":null} both deserialize "successfully" and then NRE the inspect card every
        // frame. Everything the card will print is checked here, lengths included.
        if (build is null
            || build.Skills is null || build.Keystones is null || build.Mastery is null
            || build.Skills.Count > 8 || build.Keystones.Count > 8 || build.Mastery.Count > 200
            || build.Skills.Any(s => s is null
                                     || string.IsNullOrEmpty(s.Source) || s.Source.Length > 40
                                     || string.IsNullOrEmpty(s.Form) || s.Form.Length > 40
                                     || s.VowId is { Length: > 64 })
            || build.Keystones.Any(k => string.IsNullOrEmpty(k) || k.Length > 64)
            || build.Mastery.Any(m => string.IsNullOrEmpty(m) || m.Length > 64))
        {
            build = null;
            error = DamagedError;
            return false;
        }
        return true;
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

        if (code.Length > MaxCodeChars) { error = DamagedError; return false; }

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
            // Bounded, never ReadToEnd: the cap above limits the COMPRESSED size; this one stops a
            // deflate bomb on the way out.
            var buffer = new char[MaxInflatedChars + 1];
            var read = reader.ReadBlock(buffer, 0, buffer.Length);
            if (read > MaxInflatedChars) { error = DamagedError; return false; }
            json = new string(buffer, 0, read);
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
