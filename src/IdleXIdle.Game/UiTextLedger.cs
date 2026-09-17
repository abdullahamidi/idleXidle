using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace IdleXIdle.Game;

/// <summary>
/// WHAT THE UI ACTUALLY CUTS — every label the layout could not fit, measured instead of hunted for.
/// </summary>
/// <remarks>
/// <para>
/// The sibling of <see cref="UiRasterLedger"/>, and it exists for the same reason: a question about
/// pixels cannot be settled by argument, and it should not have to be settled by squinting at
/// fourteen screens across three density profiles. When the body face changed on 2026-09-09 the
/// question became urgent and precise — set in caps Spectral runs about 22% wider than the condensed
/// grotesque it replaced, so every label in the game had less room than the layout was tuned for, and
/// "which ones stopped fitting" is a list somebody had to produce.
/// </para>
/// <para>
/// <b>Truncation is silent by design</b>, which is exactly what makes it dangerous.
/// <see cref="UiKit.ShortenBig"/> is the house's fitting rule and it always succeeds: a label that
/// does not fit comes back shorter with an ellipsis, nothing throws, nothing is logged, and the screen
/// looks finished. A word lost off the end of a stat name or a button verb is a real defect that
/// leaves no trace at all in the source, because the source is a correct call.
/// </para>
/// <para>
/// Set <c>RH_UI_TEXT=1</c> and every cut is recorded — the whole string, what survived, the rung it
/// was set at and the width it was given — then printed at exit, worst loss first. Nothing is recorded
/// when the dial is off: <see cref="On"/> is read once and the call site is a branch on a static bool.
/// Run it under <c>tools/check_boot.sh</c>, which draws every screen, and once per density profile.
/// </para>
/// <para>
/// A cut is not automatically a bug. Some are deliberate — an item's rolled name in a narrow bag cell
/// has nowhere else to go, and the hover card carries the full name. What the ledger gives is the
/// LIST, so the deliberate ones can be recognised and the rest fixed, rather than the whole class
/// being invisible.
/// </para>
/// </remarks>
public static class UiTextLedger
{
    /// <summary>Read once. Off costs one static bool test per fitted string.</summary>
    public static readonly bool On =
        Environment.GetEnvironmentVariable("RH_UI_TEXT") is { Length: > 0 } v && v != "0";

    /// <summary>One cut label: what it said, what survived, and the box that decided.</summary>
    private sealed record Cut(string Full, string Shown, int Width, int Px, int Lost);

    private static readonly Dictionary<string, Cut> _cuts = new(StringComparer.Ordinal);

    /// <summary>
    /// Record one label that had to be shortened to fit.
    /// </summary>
    /// <remarks>
    /// Keyed by the FULL string, so a row redrawn sixty times a second is one entry, and the worst
    /// case for a string that appears in two places at two widths is the one that kept the least.
    /// </remarks>
    public static void Note(string full, string shown, int width, int px)
    {
        if (!On || string.IsNullOrEmpty(full)) return;
        var lost = Math.Max(0, full.Length - shown.Length);
        if (lost <= 0) return;
        if (_cuts.TryGetValue(full, out var had) && had.Lost >= lost) return;
        _cuts[full] = new Cut(full, shown, width, px, lost);
    }

    /// <summary>
    /// Print every cut this run made, worst loss first.
    /// </summary>
    /// <remarks>
    /// Tab-separated and single-line per row, like the raster ledger, so a run across three profiles
    /// can be diffed. The share is what makes a row worth reading: losing two characters off a long
    /// sentence is a hyphen nobody notices; losing half a button's verb is a broken control.
    /// </remarks>
    public static void Dump()
    {
        if (!On) return;

        var rows = _cuts.Values.OrderByDescending(c => c.Lost / (float)Math.Max(1, c.Full.Length))
                               .ThenByDescending(c => c.Lost).ToList();
        var bad = rows.Count(c => c.Lost / (float)Math.Max(1, c.Full.Length) > 0.25f);

        Console.Out.WriteLine($"text\tcuts={rows.Count}\tsevere={bad}\tceiling=25% lost");
        foreach (var c in rows)
            Console.Out.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "text\tlost={0}\tshare={1:0.00}\tpx={2}\twidth={3}\tshown={4}\tfull={5}",
                c.Lost, c.Lost / (float)Math.Max(1, c.Full.Length), c.Px, c.Width, c.Shown, c.Full));
    }
}
