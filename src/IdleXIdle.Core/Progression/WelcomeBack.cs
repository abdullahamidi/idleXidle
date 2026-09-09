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
public readonly record struct WelcomeSummary(double AwaySeconds, OfflineHunt.Result Hunt, WarrenYield Warren, bool WarrenOpen,
                                             double CampSeconds = -1, long HuntGleamPaid = -1)
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
        if (PaidGleam > 0) parts.Add($"+{N(PaidGleam)} GLEAM");
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

    /// <summary>
    /// WHAT THE HUNTER DID WHILE YOU WERE AWAY, in sentences — the return screen's narration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Playtest 2026-09-09: <i>"The player should be greeted by a nice welcome back screen upon
    /// relaunching the game, showing them what the hunter has been doing in this world while they were
    /// away."</i> The panel had six lines maximum, every one of them a figure with a label — AWAY, then
    /// "+12,340 GLEAM · 86 WAVES CLEARED · FELL 4 TIMES · DEEPEST WAVE 31". True, and not an account of
    /// anything.
    /// </para>
    /// <para>
    /// <b>Every line is still a figure the model actually produced</b> — the discipline this file was
    /// written under, and the reason it has no NOTABLE block: offline does not level skills, drop items
    /// or complete sets, and a line the model cannot back is a lie. These sentences add no facts. They
    /// say the same numbers in the world's own voice, and a line whose figure is zero is not printed.
    /// <see cref="HuntParts"/> and <see cref="WarrenParts"/> are untouched: their exact fragments are
    /// pinned by tests, and the narration is built to sit around them rather than replace them.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Story()
    {
        var lines = new List<string>(6);
        if (AwaySeconds > 0) lines.Add($"YOU WERE GONE {AwayText(AwaySeconds)}. THE HUNTER KEPT STANDING.");
        if (Hunt.WavesCleared > 0)
            lines.Add(PaidGleam > 0
                          ? $"{N(Hunt.WavesCleared)} WAVE{(Hunt.WavesCleared == 1 ? "" : "S")} HELD. THE WORLD PAID {N(PaidGleam)} GLEAM FOR THEM."
                          : $"{N(Hunt.WavesCleared)} WAVE{(Hunt.WavesCleared == 1 ? "" : "S")} HELD.");
        if (Hunt.BossesFelled > 0)
            lines.Add(Hunt.BossesFelled == 1
                          ? "ONE OF THEM GATHERED INTO A SINGLE SHAPE. IT FELL TOO."
                          : $"{N(Hunt.BossesFelled)} OF THEM GATHERED INTO SINGLE SHAPES. ALL OF THEM FELL.");
        if (Hunt.Falls > 0)
            lines.Add(Hunt.Falls == 1
                          ? "IT FELL ONCE, AND THE WORLD PUT IT BACK."
                          : $"IT FELL {N(Hunt.Falls)} TIMES, AND EACH TIME THE WORLD PUT IT BACK.");
        if (Hunt.DeepestWave > 0) lines.Add($"DEEPEST IT STOOD: WAVE {N(Hunt.DeepestWave)}.");
        if (WarrenOpen && WarrenParts().Count > 0)
            lines.Add($"THE WARREN WORKED THROUGH ALL OF IT — {string.Join("  ·  ", WarrenParts())}.");
        return lines;
    }

    /// <summary>Where the next descent picks up, or 0 when the absence ended on a fall.</summary>
    /// <remarks>
    /// The one line on this panel that is about what happens NEXT rather than what happened. It is the
    /// answer to "the game shouldn't start at Wave 1 every time it's launched", said out loud — a
    /// resume the player is not told about is a resume they will not notice.
    /// <para>
    /// The DEEPEST wave the absence held, not the wave it happened to stop on. The stopping point is a
    /// snapshot of a cycle — the champion climbs, falls, climbs again — so a four-hour absence could
    /// end on wave three while a ten-minute one ended on twenty, and a returning player would be sent
    /// BACKWARDS for having been away longer. Measured, on the fixture that found it.
    /// </para>
    /// </remarks>
    public int ResumeWave => Hunt.DeepestWave;

    /// <summary>"PICKING UP AT WAVE 34", or null when there is nothing to pick up.</summary>
    /// <remarks>
    /// Silent at wave one, which is where a descent starts anyway: a promise that changes nothing is
    /// noise, and this line's whole job is to be noticed. It is also silent after a fall, because the
    /// absence ended with the champion back at the bottom.
    /// </remarks>
    public string? ResumeLine()
        => ResumeWave > 1 ? $"PICKING UP AT WAVE {N(ResumeWave)}" : null;

    /// <summary>Thousands-grouped, invariant — the screen speaks one language whatever the machine's locale.</summary>
    /// <summary>What the hunt actually paid — the camp's credit when the host gave one, else the simulation's own figure.</summary>
    public long PaidGleam => HuntGleamPaid >= 0 ? HuntGleamPaid : Hunt.Gleam;

    /// <summary>True when the absence outran the camp — the sentence below is owed.</summary>
    public bool CampHeldLess => CampSeconds >= 0 && AwaySeconds > CampSeconds + 60;

    /// <summary>"THE CAMP HELD 2H OF THE 9H 40M AWAY — EACH WARREN LEVEL HOLDS MORE", or null when nothing was lost.</summary>
    public string? CampLine()
        => CampHeldLess
            ? $"THE CAMP HELD {OfflineCamp.HoursText((float)(CampSeconds / 3600.0))} OF THE {AwayText(AwaySeconds)} AWAY — EACH WARREN LEVEL HOLDS MORE"
            : null;

    private static string N(long v) => v.ToString("N0", CultureInfo.InvariantCulture);
}
