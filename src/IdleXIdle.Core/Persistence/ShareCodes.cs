using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using IdleXIdle.Core.Loot;

namespace IdleXIdle.Core.Persistence;

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
    private const string FeedbackPrefix = "RHF";
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

    /// <summary>One worn piece, summarised for a feedback report: where it sits, how rare, how deep.</summary>
    public sealed record WornItemSummary
    {
        public string Slot { get; init; } = "";
        public int Rarity { get; init; }
        public int Level { get; init; }
    }

    /// <summary>
    /// Everything a bug report needs, as one pasteable line: which build produced it, how far the
    /// player is, what they are running, and the last few run reports.
    /// </summary>
    /// <remarks>
    /// Made for the COPY FEEDBACK CODE button in settings — the player pastes it to the developer
    /// beside their words, and the developer decodes the exact state the complaint happened in,
    /// instead of asking six follow-up questions over chat. Playtime is not in here because the game
    /// does not track it. Format: <c>RHF1.&lt;payload&gt;.&lt;check&gt;</c>, the same machinery as
    /// the item and build codes.
    /// </remarks>
    public sealed record SharedFeedback
    {
        /// <summary>The assembly build stamp — see <see cref="BuildStamp"/>.</summary>
        public string Build { get; init; } = "";

        /// <summary>The save format this build writes (<see cref="SaveGame.CurrentVersion"/>).</summary>
        public int SaveVersion { get; init; }

        public int DeepestWave { get; init; }
        public int RegionsConquered { get; init; }
        public int CorruptionTier { get; init; }
        public long Gleam { get; init; }

        /// <summary>Total training ranks bought across every stat.</summary>
        public int TrainingRanks { get; init; }

        /// <summary>The woven build — skills, keystones and the mastery walk.</summary>
        public SharedBuild Loadout { get; init; } = new();

        /// <summary>What is worn right now, one line per filled slot.</summary>
        public List<WornItemSummary> Worn { get; init; } = new();

        /// <summary>The persisted run log — the last few descents, exactly as the save keeps them.</summary>
        public List<RunReportSave> RunLog { get; init; } = new();
    }

    public static string EncodeItem(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Encode(ItemPrefix, JsonSerializer.Serialize(SaveSystem.ToSavedItem(item)));
    }

    /// <summary>A feedback report as one pasteable line — see <see cref="SharedFeedback"/>.</summary>
    public static string EncodeFeedback(SharedFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        return Encode(FeedbackPrefix, JsonSerializer.Serialize(feedback));
    }

    /// <summary>Is this a feedback code? The developer-side dispatcher.</summary>
    public static bool LooksLikeFeedback(string? code)
        => code?.TrimStart().StartsWith(FeedbackPrefix, StringComparison.Ordinal) == true;

    /// <summary>Decode a pasted feedback code. Fails loudly on damage; never throws.</summary>
    public static bool TryDecodeFeedback(string? code, out SharedFeedback? feedback, out string error)
    {
        feedback = null;
        if (!TryDecode(FeedbackPrefix, code, out var json, out error)) return false;
        try
        {
            feedback = JsonSerializer.Deserialize<SharedFeedback>(json);
        }
        catch (JsonException)
        {
            error = DamagedError;
            return false;
        }

        // Same discipline as the build decoder: `required` and defaults check PRESENCE, not non-null,
        // and everything a report reader prints is checked here — lengths and counts included.
        if (feedback is null
            || feedback.Build is null || feedback.Build.Length > 128
            || feedback.Loadout is null || feedback.Loadout.Skills is null
            || feedback.Loadout.Keystones is null || feedback.Loadout.Mastery is null
            || feedback.Worn is null || feedback.Worn.Count > 16
            || feedback.Worn.Any(w => w is null || w.Slot is null || w.Slot.Length > 32)
            || feedback.RunLog is null || feedback.RunLog.Count > 64
            || feedback.RunLog.Any(r => r is null || r.RegionId is null || r.RegionId.Length > 64))
        {
            feedback = null;
            error = DamagedError;
            return false;
        }
        return true;
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
            if (saved is null || !ValidSaved(saved)) { error = DamagedError; return false; }
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

    /// <summary>
    /// Refuses a RAW payload the save path would quietly repair. The save is deliberately lenient
    /// — an unknown BaseType is dropped and an out-of-range rarity clamps
    /// (<see cref="SaveSystem.CanRestore"/> / <c>FromSavedItem</c>) — because a player's own file
    /// must load whatever happens to it. A PASTED CODE is the opposite contract: it claims to be an
    /// item this game minted, and one the game could not have minted is refused whole, never
    /// silently rewritten into a different item than the sender saw.
    /// </summary>
    private static bool ValidSaved(SavedItem s)
        => Enum.TryParse<Loot.ItemBaseType>(s.BaseType, out _)
           && s.Rarity >= (int)Loot.Rarity.Common && s.Rarity <= (int)Loot.Rarity.Legendary
           && s.Gems is not null && s.Gems.All(ValidSaved);

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
           && (i.Class is not { } cl || Enum.IsDefined(cl))
           && (i.Family is not { } fam || (fam >= 0 && fam < Economy.ItemNaming.WeaponFamilies.Length))
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
