using System;
using System.Collections.Generic;
using System.Globalization;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Warrens;

namespace IdleXIdle.Core.Progression;

/// <summary>
/// What a returning player is owed: how long they were away and what the two economies paid while they were.
/// </summary>
/// <remarks>
/// <para>
/// UX V2 P1.3 (brief §25, D6). The welcome used to be a two-line toast that faded in seven seconds and named
/// one number — while the Warren's Dust, Scrap and Essence were credited unreported and the offline hunt's
/// waves, falls and deepest wave were computed and dropped. Everything here is a figure the host already
/// has: <see cref="OfflineHunt.Result"/> and <see cref="WarrenYield"/>. Nothing is estimated; a zero is
/// omitted rather than printed.
/// </para>
/// <para>
/// No NOTABLE block. Offline does not level skills, drop items or complete sets (see <c>OfflineHunt</c>),
/// so there is nothing notable to report — and a line the model cannot back is a lie (brief §92).
/// </para>
/// </remarks>
/// <param name="AwaySeconds">The credited absence.</param>
/// <param name="Hunt">What the hunter earned and did while away.</param>
/// <param name="Warren">What the Warren produced while away — default when it is not open yet.</param>
/// <param name="WarrenOpen">Whether the Warren exists for this player; a closed Warren has no row.</param>
public readonly record struct WelcomeSummary(double AwaySeconds, OfflineHunt.Result Hunt, WarrenYield Warren, bool WarrenOpen)
{
    /// <summary>Below this the trip is short: a toast, not a panel.</summary>
    public const double PanelFromSeconds = 60;

    /// <summary>Did the absence produce anything worth a line?</summary>
    public bool HasNews => Hunt.Gleam > 0 || Hunt.WavesCleared > 0
                           || (WarrenOpen && (Warren.Gleam > 0 || Warren.Dust > 0 || Warren.Scrap > 0 || Warren.Essence > 0));

    /// <summary>The held panel with CONTINUE — for a real absence that paid; otherwise the toast (or nothing).</summary>
    public bool ShowsPanel => AwaySeconds >= PanelFromSeconds && HasNews;

    /// <summary>"6h 42m" · "18 min" · "3 days 2h" — the absence in the words a person would use.</summary>
    public static string AwayText(double seconds)
    {
        var s = Math.Max(0, seconds);
        if (s < 3600) return $"{Math.Max(1, (int)Math.Round(s / 60))} MIN";
        var hours = (int)(s / 3600);
        var minutes = (int)((s % 3600) / 60);
        if (hours < 24) return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        var days = hours / 24;
        var rest = hours % 24;
        return rest > 0 ? $"{days} DAY{(days == 1 ? "" : "S")} {rest}h" : $"{days} DAY{(days == 1 ? "" : "S")}";
    }

    /// <summary>The HUNT row's parts, non-zero only, in the order they matter: Gleam, waves, falls, deepest.</summary>
    public IReadOnlyList<string> HuntParts()
    {
        var parts = new List<string>(4);
        if (Hunt.Gleam > 0) parts.Add($"+{N(Hunt.Gleam)} GLEAM");
        if (Hunt.WavesCleared > 0) parts.Add($"{N(Hunt.WavesCleared)} WAVE{(Hunt.WavesCleared == 1 ? "" : "S")} CLEARED");
        if (Hunt.Falls > 0) parts.Add($"FELL {N(Hunt.Falls)} TIME{(Hunt.Falls == 1 ? "" : "S")}");
        if (Hunt.DeepestWave > 0) parts.Add($"DEEPEST WAVE {N(Hunt.DeepestWave)}");
        return parts;
    }

    /// <summary>The WARREN row's parts, non-zero only; empty when the Warren is not open.</summary>
    public IReadOnlyList<string> WarrenParts()
    {
        var parts = new List<string>(4);
        if (!WarrenOpen) return parts;
        if (Warren.Gleam > 0) parts.Add($"+{N(Warren.Gleam)} GLEAM");
        if (Warren.Dust > 0) parts.Add($"+{N(Warren.Dust)} DUST");
        if (Warren.Scrap > 0) parts.Add($"+{N(Warren.Scrap)} SCRAP");
        if (Warren.Essence > 0) parts.Add($"+{N(Warren.Essence)} ESSENCE");
        return parts;
    }

    /// <summary>Thousands-grouped, invariant — the screen speaks one language whatever the machine's locale.</summary>
    private static string N(long v) => v.ToString("N0", CultureInfo.InvariantCulture);
}
