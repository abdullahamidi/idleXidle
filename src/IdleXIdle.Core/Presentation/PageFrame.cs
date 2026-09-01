using System;

namespace IdleXIdle.Core.Presentation;

/// <summary>
/// THE ONE CURSOR TRANSFORM. Where a page pixel lands on the screen, and — exactly inverted — which
/// page pixel a screen position points at. Pure arithmetic, tested, and the only place the mouse is
/// mapped (UI polish brief §12–§16; LAW 5, LAW 6).
/// </summary>
/// <remarks>
/// <para>
/// The game draws a fixed <paramref name="CanvasWidth"/>×<paramref name="CanvasHeight"/> canvas, presents
/// it aspect-fit inside the window (<see cref="Present"/>, a fractional uniform scale with letterbox), and
/// draws each menu page through an inset matrix: page × <see cref="OverlayScale"/> then shifted right by
/// <see cref="OverlayLeft"/> (the nav rail). The chrome — rail, pills, settings — and the fight are drawn
/// at scale 1 in canvas coordinates.
/// </para>
/// <para>
/// <b>Why it is a float all the way down.</b> Until 2026-09-01 the menu screens received a cursor that had
/// been floored to a 480×270 canvas, multiplied by four and divided by the page scale — a ~4.5 page-pixel
/// grid on every hover and click, and a truncation toward zero that could fold a point one pixel LEFT of
/// the page onto its first column. Every step here stays floating point; the caller floors ONCE, with
/// <see cref="Floor"/>, to the page pixel — which is where the drawn rectangle's edge is, so a click half
/// a screen pixel inside a button's drawn edge is inside its hit rectangle and half a pixel outside is
/// outside. Draw and hit-test share the same integer rectangle; the cursor is quantised to the screen,
/// never to a coarser grid.
/// </para>
/// </remarks>
/// <param name="Present">Where the canvas sits in the window, in screen pixels.</param>
/// <param name="CanvasWidth">The canvas's width in canvas pixels (1920).</param>
/// <param name="CanvasHeight">The canvas's height in canvas pixels (1080).</param>
/// <param name="OverlayScale">Page pixels → canvas pixels, for the inset menu pages.</param>
/// <param name="OverlayLeft">The canvas x the inset page starts at (the nav rail's width).</param>
public readonly record struct PageFrame(FitRect Present, int CanvasWidth, int CanvasHeight,
                                        float OverlayScale, float OverlayLeft)
{
    /// <summary>Screen pixels per canvas pixel — the aspect-fit's uniform scale.</summary>
    public float PresentScale => Present.Width / (float)Math.Max(1, CanvasWidth);

    // ── canvas (the chrome's space, scale 1) ─────────────────────────────────────────────────────

    /// <summary>A canvas point's position on the screen.</summary>
    public (float X, float Y) CanvasToScreen(float cx, float cy)
        => (Present.X + cx * PresentScale, Present.Y + cy * PresentScale);

    /// <summary>The canvas point a screen position points at — the chrome's cursor.</summary>
    public (float X, float Y) ScreenToCanvas(float sx, float sy)
    {
        var s = PresentScale;
        if (s <= 0f) return (sx, sy);
        return ((sx - Present.X) / s, (sy - Present.Y) / s);
    }

    // ── page (an inset menu screen's space) ──────────────────────────────────────────────────────

    /// <summary>A page point's position on the screen: through the overlay matrix, then the present.</summary>
    public (float X, float Y) PageToScreen(float px, float py)
        => CanvasToScreen(px * OverlayScale + OverlayLeft, py * OverlayScale);

    /// <summary>The page point a screen position points at — a menu screen's cursor, still a float.</summary>
    public (float X, float Y) ScreenToPage(float sx, float sy)
    {
        var (cx, cy) = ScreenToCanvas(sx, sy);
        var o = OverlayScale <= 0f ? 1f : OverlayScale;
        return ((cx - OverlayLeft) / o, cy / o);
    }

    /// <summary>
    /// The one rounding step: toward negative infinity, so a point just left of the page lands OUTSIDE it
    /// rather than on its first column (C# integer casts truncate toward zero, which is the bug).
    /// </summary>
    public static (int X, int Y) Floor((float X, float Y) p)
        => ((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y));
}
