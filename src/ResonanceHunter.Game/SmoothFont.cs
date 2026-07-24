using System;
using System.IO;
using System.Linq;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ResonanceHunter.Client;

/// <summary>
/// Smooth TTF text via FontStashSharp, drawn crisp inside the 4×-scaled canvas. Mirrors <see cref="PixelFont"/>'s
/// API so <see cref="UiKit"/> can use either, and falls back to the pixel font if no usable TTF is found.
/// </summary>
/// <remarks>
/// The canvas is authored in 480×270 logical units and scaled ×4 to 1920×1080. If we rasterised the glyph
/// atlas at the logical size it would be blown up 4× and blur, so we rasterise at the ACTUAL pixel size
/// (<see cref="RasterPx"/>) and draw it at 1/4, landing 1:1 on screen — sharp, not upscaled. Measurement is
/// returned in LOGICAL units so right/centre alignment lands correctly with the proportional metrics.
/// </remarks>
public sealed class SmoothFont
{
    private const int ArtScale = 4;
    private const int RasterPx = 32;                 // actual glyph height in px
    private const float DrawScale = 1f / ArtScale;   // 32px atlas → 8 logical → 32 actual, crisp
    private const float VOffset = -0.5f;             // small nudge so it sits like the pixel font's top-left

    private readonly FontSystem? _system;
    private readonly SpriteFontBase? _font;
    private readonly PixelFont _fallback;

    /// <summary>True when a real TTF loaded; false means every call transparently uses the pixel font.</summary>
    public bool Loaded { get; }

    public SmoothFont(PixelFont fallback)
    {
        _fallback = fallback;
        var bytes = TryReadFont();
        if (bytes is null) { Loaded = false; return; }

        _system = new FontSystem();
        _system.AddFont(bytes);
        _font = _system.GetFont(RasterPx);
        Loaded = true;
    }

    /// <summary>A bundled font wins; otherwise a clean system font for dev (ship an open TTF in assets/fonts).</summary>
    private static byte[]? TryReadFont()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "assets", "fonts");
            if (Directory.Exists(dir))
            {
                var ttf = Directory.EnumerateFiles(dir, "*.ttf")
                    .Concat(Directory.EnumerateFiles(dir, "*.otf")).FirstOrDefault();
                if (ttf is not null) return File.ReadAllBytes(ttf);
            }
            foreach (var sys in new[] { @"C:\Windows\Fonts\bahnschrift.ttf", @"C:\Windows\Fonts\segoeui.ttf" })
                if (File.Exists(sys)) return File.ReadAllBytes(sys);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }

    /// <summary>Logical width of the text.</summary>
    public int Measure(string text)
        => Loaded ? (int)MathF.Round(_font!.MeasureString(text).X * DrawScale) : PixelFont.Measure(text);

    public void Draw(SpriteBatch b, string text, int x, int y, Color c)
    {
        if (!Loaded) { _fallback.Draw(b, text, x, y, c); return; }
        // DrawText(batch, text, position, color, rotation, origin, scale?, …) — scale is the 7th arg.
        _font!.DrawText(b, text, new Vector2(x, y + VOffset), c, 0f, Vector2.Zero, new Vector2(DrawScale));
    }

    public void DrawRight(SpriteBatch b, string text, int right, int y, Color c) => Draw(b, text, right - Measure(text), y, c);
    public void DrawCentered(SpriteBatch b, string text, int cx, int y, Color c) => Draw(b, text, cx - Measure(text) / 2, y, c);

    // ── Sized variants: rasterise crisp at ANY logical height (default is 8). Lets the UI build a real type
    // hierarchy — big titles and damage numbers, small labels — instead of one flat size. ────────────────
    private SpriteFontBase FontAt(int logicalPx) => _system!.GetFont(Math.Max(6, logicalPx) * ArtScale);

    public int Measure(string text, int logicalPx)
        => Loaded ? (int)MathF.Round(FontAt(logicalPx).MeasureString(text).X * DrawScale) : PixelFont.Measure(text);

    public void Draw(SpriteBatch b, string text, int x, int y, Color c, int logicalPx)
    {
        if (!Loaded) { _fallback.Draw(b, text, x, y, c); return; }
        FontAt(logicalPx).DrawText(b, text, new Vector2(x, y + VOffset), c, 0f, Vector2.Zero, new Vector2(DrawScale));
    }

    public void DrawRight(SpriteBatch b, string text, int right, int y, Color c, int logicalPx) => Draw(b, text, right - Measure(text, logicalPx), y, c, logicalPx);
    public void DrawCentered(SpriteBatch b, string text, int cx, int y, Color c, int logicalPx) => Draw(b, text, cx - Measure(text, logicalPx) / 2, y, c, logicalPx);
}
