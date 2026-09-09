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
/// <summary>WHICH figure a return tile carries. The screen maps this to a picture; Core never sees one.</summary>
public enum WelcomeFigure
{
    /// <summary>Waves the absence held.</summary>
    Waves,

    /// <summary>Gleam earned — by the hunt, or by the camp.</summary>
    Gleam,

    /// <summary>Bosses felled.</summary>
    Bosses,

    /// <summary>The deepest wave the absence stood on.</summary>
    Deepest,

    /// <summary>How many times the champion fell. The one figure here that is not a gain.</summary>
    Falls,

    /// <summary>Memory Dust the camp produced.</summary>
    Dust,

    /// <summary>Scrap the camp produced.</summary>
    Scrap,

    /// <summary>Essence the camp produced.</summary>
    Essence,
}

/// <summary>One tile on the return screen: what it is, the figure, and the word under it.</summary>
/// <param name="Figure">What this tile counts — the screen picks the icon from it.</param>
/// <param name="Value">The figure, already grouped and locale-independent.</param>
/// <param name="Label">The word under the figure, singular or plural to match it.</param>
public readonly record struct WelcomeTile(WelcomeFigure Figure, string Value, string Label);

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

    /// <summary>
    /// THE RETURN IS A RESULT, NOT A STORY — one figure per tile, and no tile for a zero.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Playtest 2026-09-09: <i>"you have written the resources and statistics on the Welcome Back panel
    /// as flat text. Make them iconed and like a results screen — we are not telling a story."</i> The
    /// panel had been the other way twice over: first a run of joined fragments
    /// ("+12,340 GLEAM · 86 WAVES CLEARED · FELL 4 TIMES"), then six narrated sentences stacked on top
    /// of them. Both are prose about numbers, and prose is the slowest way to read a number.
    /// </para>
    /// <para>
    /// So Core stops writing sentences and hands over the figures, each tagged with WHAT it is. The
    /// screen owns the picture — an icon is presentation, and Core knows nothing about textures — but
    /// the value, the label and the decision not to print a zero stay here, with every other figure in
    /// this file. The narration and the joined hunt fragments were DELETED rather than left unread: the
    /// thing this codebase punishes hardest is a function that still compiles and nothing calls.
    /// </para>
    /// </remarks>
    public IReadOnlyList<WelcomeTile> HuntTiles()
    {
        var tiles = new List<WelcomeTile>(5);
        if (Hunt.WavesCleared > 0) tiles.Add(new(WelcomeFigure.Waves, N(Hunt.WavesCleared), Hunt.WavesCleared == 1 ? "WAVE HELD" : "WAVES HELD"));
        if (PaidGleam > 0) tiles.Add(new(WelcomeFigure.Gleam, N(PaidGleam), "GLEAM EARNED"));
        if (Hunt.BossesFelled > 0) tiles.Add(new(WelcomeFigure.Bosses, N(Hunt.BossesFelled), Hunt.BossesFelled == 1 ? "BOSS FELLED" : "BOSSES FELLED"));
        if (Hunt.DeepestWave > 0) tiles.Add(new(WelcomeFigure.Deepest, N(Hunt.DeepestWave), "DEEPEST WAVE"));
        // FALLS LAST, and only when there were any. It is the one figure on the panel that is not a
        // gain, and leading with it would make a good night read as a bad one.
        if (Hunt.Falls > 0) tiles.Add(new(WelcomeFigure.Falls, N(Hunt.Falls), Hunt.Falls == 1 ? "FALL" : "FALLS"));
        return tiles;
    }

    /// <summary>The Warren's tiles — what the camp produced, one per resource, zeros omitted.</summary>
    public IReadOnlyList<WelcomeTile> WarrenTiles()
    {
        var tiles = new List<WelcomeTile>(4);
        if (!WarrenOpen) return tiles;
        if (Warren.Gleam > 0) tiles.Add(new(WelcomeFigure.Gleam, N(Warren.Gleam), "GLEAM"));
        if (Warren.Dust > 0) tiles.Add(new(WelcomeFigure.Dust, N(Warren.Dust), "MEMORY DUST"));
        if (Warren.Scrap > 0) tiles.Add(new(WelcomeFigure.Scrap, N(Warren.Scrap), "SCRAP"));
        if (Warren.Essence > 0) tiles.Add(new(WelcomeFigure.Essence, N(Warren.Essence), "ESSENCE"));
        return tiles;
    }

    /// <summary>
    /// The WARREN row as joined fragments — for the Warren screen's own one-line "while you were away".
    /// </summary>
    /// <remarks>
    /// Kept as a sentence because its one consumer is a sentence: <c>WarrenScreen</c> prints it inline
    /// under the camp's title, where a grid of tiles would be a second, competing summary of a panel the
    /// player has just dismissed. The RETURN PANEL uses <see cref="WarrenTiles"/>.
    /// </remarks>
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
