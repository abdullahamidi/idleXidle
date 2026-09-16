using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game;

/// <summary>
/// WHAT THE ITEM ART ACTUALLY CONTAINS, measured from the pixels once and remembered: a glyph's content
/// bounds (the box its opaque pixels fill) and a rarity frame's interior (where its see-through hole is).
/// </summary>
/// <remarks>
/// <para>
/// The 138 item PNGs are all 256 × 256 canvases and none is full: the art inside runs from a fifth of
/// the canvas (a charm on its chain) to nearly all of it. Stretching the whole canvas into a cell drew
/// every glyph at the size of its padding, not its art. And the rarity frames are being regenerated as a
/// family with a transparent centre and one shared safe area — so the layout MEASURES the hole rather
/// than assuming 15 %, and falls back to 15 % when the centre is painted (today's legacy set).
/// </para>
/// <para>
/// One <c>GetData</c> per texture at first use, cached for the texture's life — the same discipline as
/// <c>UiKit.Measure</c> and <c>UiKit.TopPadFraction</c>; a Draw never reads a texture back. The scans
/// themselves are pure functions over a pixel array (<see cref="ContentBoundsOf"/>,
/// <see cref="FrameInteriorOf"/>) so the tests can pose a frame without a graphics device.
/// </para>
/// </remarks>
public static class ItemArtMetrics
{
    /// <summary>An alpha at or under this is empty — the same floor UiKit's pad scans use.</summary>
    public const byte AlphaFloor = 8;

    private static readonly Dictionary<Texture2D, Rectangle> Bounds = new();
    private static readonly Dictionary<Texture2D, FrameInterior> Interiors = new();

    /// <summary>
    /// The box a glyph's opaque pixels fill, in texture pixels. Null or an empty texture reads as empty;
    /// a texture with no opaque pixel at all reads as its whole canvas, so it still draws where it did.
    /// </summary>
    public static Rectangle ContentBounds(Texture2D? tex)
    {
        if (tex is null || tex.Width <= 0 || tex.Height <= 0) return Rectangle.Empty;
        if (Bounds.TryGetValue(tex, out var cached)) return cached;
        var data = new Color[tex.Width * tex.Height];
        tex.GetData(data);
        var r = ContentBoundsOf(data, tex.Width, tex.Height);
        Bounds[tex] = r;
        return r;
    }

    /// <summary>The pure scan behind <see cref="ContentBounds(Texture2D?)"/>: the bbox of every pixel over <see cref="AlphaFloor"/>.</summary>
    public static Rectangle ContentBoundsOf(ReadOnlySpan<Color> data, int w, int h)
    {
        if (w <= 0 || h <= 0 || data.Length < w * h) return Rectangle.Empty;
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (var y = 0; y < h; y++)
        {
            var row = y * w;
            for (var x = 0; x < w; x++)
            {
                if (data[row + x].A <= AlphaFloor) continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
        }
        return x1 < 0 ? new Rectangle(0, 0, w, h) : new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    /// <summary>
    /// A rarity frame's interior: from the centre outward along the middle row and column to the first
    /// pixel over <see cref="AlphaFloor"/> on each side, as a fraction of the edge. A frame whose centre
    /// pixel is painted — or no frame — reports <see cref="FrameInterior.Fallback"/>.
    /// </summary>
    public static FrameInterior FrameInterior(Texture2D? tex)
    {
        if (tex is null || tex.Width <= 0 || tex.Height <= 0) return IdleXIdle.Game.FrameInterior.Fallback;
        if (Interiors.TryGetValue(tex, out var cached)) return cached;
        var data = new Color[tex.Width * tex.Height];
        tex.GetData(data);
        var f = FrameInteriorOf(data, tex.Width, tex.Height);
        Interiors[tex] = f;
        return f;
    }

    /// <summary>The pure scan behind <see cref="FrameInterior(Texture2D?)"/>.</summary>
    public static FrameInterior FrameInteriorOf(ReadOnlySpan<Color> data, int w, int h)
    {
        if (w <= 0 || h <= 0 || data.Length < w * h) return IdleXIdle.Game.FrameInterior.Fallback;
        int cx = w / 2, cy = h / 2;
        if (data[cy * w + cx].A > AlphaFloor) return IdleXIdle.Game.FrameInterior.Fallback;

        // Each side: the first opaque pixel walking out from the centre. A side with none (an open
        // frame) has no band, so its inset is zero.
        var left = 0f; var right = 0f; var top = 0f; var bottom = 0f;
        for (var x = cx; x >= 0; x--) if (data[cy * w + x].A > AlphaFloor) { left = (x + 1) / (float)w; break; }
        for (var x = cx; x < w; x++) if (data[cy * w + x].A > AlphaFloor) { right = (w - x) / (float)w; break; }
        for (var y = cy; y >= 0; y--) if (data[y * w + cx].A > AlphaFloor) { top = (y + 1) / (float)h; break; }
        for (var y = cy; y < h; y++) if (data[y * w + cx].A > AlphaFloor) { bottom = (h - y) / (float)h; break; }
        return new FrameInterior(left, top, right, bottom, true);
    }

    /// <summary>Forget every measurement — for tests, and for a reload that replaces the textures.</summary>
    public static void Reset()
    {
        Bounds.Clear();
        Interiors.Clear();
    }
}
