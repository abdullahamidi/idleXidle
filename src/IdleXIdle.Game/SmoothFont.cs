using System;
using System.IO;
using System.Linq;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game;

/// <summary>Which of the two typefaces a string is set in (art bible 7.1's hybrid typography).</summary>
public enum TextFace
{
    /// <summary>The data face, at the weight the size implies. Everything by default.</summary>
    Data,

    /// <summary>The data face, always at structural weight — buttons and anything that is an action.</summary>
    Strong,

    /// <summary>The ceremony face. Screen titles and headlines only, never a column.</summary>
    Display,
}

/// <summary>
/// The game's type stack: two bundled open faces, three optical weights, drawn crisp inside the
/// 4x-scaled canvas. Mirrors <see cref="PixelFont"/>'s API so <see cref="UiKit"/> can use either, and
/// falls back to the pixel font if nothing loads.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE GAME USED TO SHIP NO FONT AT ALL.</b> This class read <c>C:\Windows\Fonts\bahnschrift.ttf</c>
/// off the player's own machine, so the metrics of every label in the game were whatever that install
/// happened to have, and on a machine without it the entire UI silently dropped to the blocky pixel
/// fallback. The loader did have a bundled-font branch — but <c>assets/fonts</c> was never copied to the
/// output directory, so in the whole life of the project that branch had never once executed. It is
/// wired up in the .csproj now, alongside the art and audio copies.
/// </para>
/// <para>
/// <b>THE TWO FACES</b> are art bible 7.1's requirement, which had no implementation:
/// </para>
/// <list type="bullet">
/// <item><b>Data — IBM Plex Sans Condensed</b> (SIL OFL). Chosen on the bible's own two hard criteria.
/// Its figures are TABULAR — all ten digits are 540 units wide, in all three weights — so a right-aligned
/// column of 1,190 / 2,004 / 888 keeps its separators on one vertical line, and a number does not shift
/// when it changes weight. This game is almost entirely number columns. Its digits are also
/// unambiguous at small size (0/O, 1/l/I, 5/S, 8/B), the bible's second demand. And being CONDENSED it
/// buys about 12% horizontal room over a normal-width grotesque at the same height — which is not a
/// stylistic bonus but a direct fix for the truncation the layout has been fighting everywhere.
/// <c>tools/check_font_digits.py</c> enforces the tabular rule so a future swap cannot quietly undo it.</item>
/// <item><b>Display — Cinzel</b> (SIL OFL). Inscriptional Roman capitals — literally carved letterforms,
/// which is what 7.1 asks the ceremony face for. It is used ONLY at title sizes, so its low small-size
/// legibility costs nothing, and every title in this game is already set in caps, so its small-cap
/// lowercase never shows.</item>
/// </list>
/// <para>
/// <b>Cinzel is loaded with the data face behind it in the same FontSystem</b>, which makes FontStashSharp
/// resolve any glyph Cinzel lacks from Plex instead of drawing nothing. A display face has a narrow
/// character set, and a missing glyph in this engine is not a box — the text closes over the hole and
/// reads as a different sentence, which is the exact failure <c>check_font_coverage.py</c> exists for.
/// </para>
/// <para>
/// <b>WEIGHT IS DERIVED FROM SIZE, in one place rather than at 146 call sites.</b> Until now the entire
/// game was set in a single weight, and hierarchy was carried by size and colour alone — which is a large
/// part of why dense screens read as flat and tangled. The rule is the ordinary optical one: text sizes
/// are set Regular, display sizes are set heavier, because a 36px title at Regular looks under-set next
/// to a wall of 18px labels. See <see cref="WeightFor"/>.
/// </para>
/// <para>
/// The canvas is authored in 480x270 logical units and scaled x4 to 1920x1080. If we rasterised the glyph
/// atlas at the logical size it would be blown up 4x and blur, so we rasterise at the ACTUAL pixel size
/// and draw it at 1/4, landing 1:1 on screen — sharp, not upscaled. Measurement is returned in LOGICAL
/// units so right/centre alignment lands correctly with the proportional metrics.
/// </para>
/// </remarks>
public sealed class SmoothFont
{
    private const int RasterPx = 32;   // ui-size-ok: the glyph ATLAS's raster height, not a drawn size
    private const float VOffset = -0.5f;             // small nudge so it sits like the pixel font's top-left

    /// <summary>At and above this logical height, text is set SemiBold rather than Regular.</summary>
    /// <remarks>
    /// Sits between Body (19) and NavigationLabel (21) so it splits the tokens exactly where the meaning
    /// splits: everything that is prose or a caption stays Regular, everything that is a title, a heading
    /// or a headline value gets weight. Nothing between 19 and 21 exists to be surprised by.
    /// </remarks>
    private static int SemiBoldFrom => UiTypography.NavigationLabel;   // the ladder decides; see UiTypography

    /// <summary>At and above this logical height, text is set Bold.</summary>
    /// <remarks>
    /// Catches PrimaryValue, the two damage sizes and ScreenTitle — the handful of things meant to be
    /// read from across the room, and the only ones a third weight earns its file size for.
    /// </remarks>
    private static int BoldFrom => UiTypography.PrimaryValue;

    /// <summary>
    /// RASTER DENSITY: how many canvas pixels one logical pixel of text becomes under the batch's matrix.
    /// 4 for the 480×270 logical screens, 1 for chrome authored in 1920×1080, and the PAGE scale (about
    /// 0.9 at UI SCALE 100%, 1.12 at 125%) for a menu screen drawn through the overlay matrix. Glyphs are
    /// rasterised at logicalPx × Density and drawn at 1/Density, so after the matrix they land 1:1 — crisp
    /// at every scale instead of a 1080-authored glyph resampled by the overlay. Set by the host per batch.
    /// </summary>
    /// <remarks>
    /// Fonts are cached per (weight, pixel size). Only the four quantised densities ever reach this — never
    /// a continuously varying present scale, which would mint a new atlas every frame.
    /// </remarks>
    public float Density { get; set; } = 4f;

    private readonly FontSystem? _regular;
    private readonly FontSystem? _semiBold;
    private readonly FontSystem? _bold;
    private readonly FontSystem? _display;
    private readonly SpriteFontBase? _font;
    private readonly PixelFont _fallback;

    /// <summary>True when a real TTF loaded; false means every call transparently uses the pixel font.</summary>
    public bool Loaded { get; }

    /// <summary>True when the ceremony face loaded; false means titles quietly use the data face.</summary>
    public bool HasDisplayFace { get; }

    public SmoothFont(PixelFont fallback)
    {
        _fallback = fallback;

        var data = ReadFace("IBMPlexSansCondensed-Regular") ?? ReadSystemFace();
        if (data is null) { Loaded = false; return; }

        _regular = Systems(data);
        // A weight that did not ship falls back to the one below it, so a partial install degrades to
        // "less hierarchy" rather than to no text.
        _semiBold = ReadFace("IBMPlexSansCondensed-SemiBold") is { } sb ? Systems(sb) : _regular;
        _bold = ReadFace("IBMPlexSansCondensed-Bold") is { } bd ? Systems(bd) : _semiBold;

        // Cinzel first, the data face behind it: FontStashSharp resolves a glyph the display face lacks
        // from the fallback rather than drawing nothing at all.
        if (ReadFace("Cinzel") is { } disp) { _display = Systems(disp, data); HasDisplayFace = true; }
        else { _display = _bold; HasDisplayFace = false; }

        _font = _regular.GetFont(RasterPx);
        Loaded = true;
    }

    private static FontSystem Systems(params byte[][] faces)
    {
        var system = new FontSystem();
        foreach (var face in faces) system.AddFont(face);
        return system;
    }

    /// <summary>Read a bundled face by file stem, from the assets copied next to the executable.</summary>
    private static byte[]? ReadFace(string stem)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "assets", "fonts");
            if (!Directory.Exists(dir)) return null;
            var file = Directory.EnumerateFiles(dir, stem + ".ttf")
                .Concat(Directory.EnumerateFiles(dir, stem + ".otf")).FirstOrDefault();
            return file is null ? null : File.ReadAllBytes(file);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    /// <summary>
    /// Last resort for a dev build run before the assets are copied: a clean system face.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT the shipping path — system fonts are not redistributable and are not on every
    /// machine. This exists so a broken asset copy shows up as "the type looks wrong" rather than as the
    /// pixel fallback, which is a much harder symptom to trace back to a missing file.
    /// </remarks>
    private static byte[]? ReadSystemFace()
    {
        foreach (var sys in new[] { @"C:\Windows\Fonts\bahnschrift.ttf", @"C:\Windows\Fonts\segoeui.ttf" })
        {
            try { if (File.Exists(sys)) return File.ReadAllBytes(sys); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return null;
    }

    /// <summary>The system a given face and size resolves to.</summary>
    private FontSystem WeightFor(int logicalPx, TextFace face) => face switch
    {
        TextFace.Display => _display!,
        TextFace.Strong => _semiBold!,
        _ => logicalPx >= BoldFrom ? _bold! : logicalPx >= SemiBoldFrom ? _semiBold! : _regular!,
    };

    private SpriteFontBase FontAt(int logicalPx, TextFace face)
        => WeightFor(logicalPx, face).GetFont(Math.Max(6, logicalPx) * Density);

    /// <summary>Logical width of the text.</summary>
    public int Measure(string text)
        => Loaded ? (int)MathF.Round(_font!.MeasureString(text).X * (1f / Density)) : PixelFont.Measure(text);

    public void Draw(SpriteBatch b, string text, int x, int y, Color c)
    {
        if (!Loaded) { _fallback.Draw(b, text, x, y, c); return; }
        // DrawText(batch, text, position, color, rotation, origin, scale?, …) — scale is the 7th arg.
        _font!.DrawText(b, text, new Vector2(x, y + VOffset), c, 0f, Vector2.Zero, new Vector2(1f / Density));
    }

    public void DrawRight(SpriteBatch b, string text, int right, int y, Color c) => Draw(b, text, right - Measure(text), y, c);
    public void DrawCentered(SpriteBatch b, string text, int cx, int y, Color c) => Draw(b, text, cx - Measure(text) / 2, y, c);

    // ── Sized variants: rasterise crisp at ANY logical height. Lets the UI build a real type hierarchy —
    // big titles and damage numbers, small labels — instead of one flat size. ────────────────────────────
    public int Measure(string text, int logicalPx, TextFace face = TextFace.Data)
        => Loaded ? (int)MathF.Round(FontAt(logicalPx, face).MeasureString(text).X * (1f / Density)) : PixelFont.Measure(text);

    public void Draw(SpriteBatch b, string text, int x, int y, Color c, int logicalPx, TextFace face = TextFace.Data)
    {
        if (!Loaded) { _fallback.Draw(b, text, x, y, c); return; }
        FontAt(logicalPx, face).DrawText(b, text, new Vector2(x, y + VOffset), c, 0f, Vector2.Zero, new Vector2(1f / Density));
    }

    public void DrawRight(SpriteBatch b, string text, int right, int y, Color c, int logicalPx, TextFace face = TextFace.Data)
        => Draw(b, text, right - Measure(text, logicalPx, face), y, c, logicalPx, face);

    public void DrawCentered(SpriteBatch b, string text, int cx, int y, Color c, int logicalPx, TextFace face = TextFace.Data)
        => Draw(b, text, cx - Measure(text, logicalPx, face) / 2, y, c, logicalPx, face);
}
