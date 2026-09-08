using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game;

/// <summary>
/// WHAT THE UI ACTUALLY MAGNIFIES — the ledger BRIEF §74 asks for, measured instead of argued.
/// </summary>
/// <remarks>
/// <para>
/// §74's complaint is a specific one: <i>"we generated the wrong resolution and stretched it"</i>, and
/// its rule is that decorative raster frames may not be arbitrarily enlarged. That is a claim about
/// numbers — a texture's native pixels against the pixels it is painted across — and numbers are not
/// something a screenshot can settle. The VFX contract already learned this: it ships
/// <c>RH_VFX_BUDGET</c>, which measures every effect against every strip it can wear, and that ledger
/// is what turned "the shield looks small" into "the dome fills 0.492 of its frame, so reaching the
/// authored band needs 1.88x, which is forbidden". The UI had no equivalent, so the same question
/// about frames could only ever be answered by eye.
/// </para>
/// <para>
/// Set <c>RH_UI_BUDGET=1</c> and every scaled texture draw UiKit performs is recorded here, then
/// summarised per asset at exit: the worst magnification each key reached, and where. Nothing is
/// recorded when the dial is off — <see cref="On"/> is read once and the call sites are a branch on a
/// static bool.
/// </para>
/// <para>
/// The verdict bands follow the VFX ledger's, so one habit reads both:
/// </para>
/// <list type="bullet">
///   <item><c>ok</c> — drawn at or below native. A downscale is always safe.</item>
///   <item><c>soft</c> — up to 1.25x. Visible only on a hard edge; the VFX budget's own ceiling.</item>
///   <item><c>OVER</c> — above 1.25x. This is the §74 failure: regenerate the asset, do not magnify.</item>
/// </list>
/// <para>
/// A nine-sliced frame reports its CORNER's magnification, never the panel's overall size, because a
/// nine-slice is the fix for stretching rather than an instance of it: its corners hold native scale
/// while its edges tile in one direction and its centre fills. Reporting the whole panel would flag
/// every large frame in the game and teach nobody anything.
/// </para>
/// </remarks>
public static class UiRasterLedger
{
    /// <summary>Read once. Off costs one static bool test per draw.</summary>
    public static readonly bool On =
        Environment.GetEnvironmentVariable("RH_UI_BUDGET") is { Length: > 0 } v && v != "0";

    /// <summary>The worst magnification a single asset key reached, and the draw that reached it.</summary>
    private sealed record Worst(string Key, float Ratio, int SrcW, int SrcH, int DstW, int DstH, string Site);

    private static readonly Dictionary<string, Worst> _worst = new(StringComparer.Ordinal);

    /// <summary>
    /// Record one scaled draw. <paramref name="srcW"/>/<paramref name="srcH"/> are the SOURCE pixels
    /// actually sampled — a strip frame, a nine-slice corner, a cropped sprite — not the whole texture,
    /// because the ratio that matters is between the pixels read and the pixels painted.
    /// </summary>
    public static void Note(string key, int srcW, int srcH, int dstW, int dstH, string site)
    {
        if (!On || srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0) return;

        // The larger of the two axes: a frame stretched only horizontally is still stretched.
        var ratio = MathF.Max(dstW / (float)srcW, dstH / (float)srcH);
        if (_worst.TryGetValue(key, out var had) && had.Ratio >= ratio) return;
        _worst[key] = new Worst(key, ratio, srcW, srcH, dstW, dstH, site);
    }

    /// <summary>Convenience for the common case where the whole texture is the source.</summary>
    public static void Note(Texture2D? tex, string key, Rectangle dst, string site)
    {
        if (!On || tex is null) return;
        Note(key, tex.Width, tex.Height, dst.Width, dst.Height, site);
    }

    private static string Verdict(float r) => r <= 1.001f ? "ok" : r <= 1.25f ? "soft" : "OVER";

    /// <summary>
    /// The ledger, worst first. Printed with a <c>ui</c> prefix so the capture rig can grep it out of
    /// the game's stdout the same way <c>vfx_dump.sh</c> greps the VFX lines.
    /// </summary>
    public static void Dump()
    {
        if (!On) return;

        var rows = _worst.Values.OrderByDescending(w => w.Ratio).ToList();
        var over = rows.Count(w => w.Ratio > 1.25f);

        Console.Out.WriteLine($"ui\tassets={rows.Count}\tover={over}\tceiling=1.25");
        foreach (var w in rows)
            Console.Out.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "ui\tkey={0}\tsrc={1}x{2}\tdst={3}x{4}\tratio={5:0.000}\t{6}\tat={7}",
                w.Key, w.SrcW, w.SrcH, w.DstW, w.DstH, w.Ratio, Verdict(w.Ratio), w.Site));

        if (over > 0)
            Console.Out.WriteLine($"ui\t{over} UI ASSET(S) OVER THE RASTER BUDGET — REGENERATE, DO NOT MAGNIFY (BRIEF 74)");
    }
}
