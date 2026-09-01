using System;

namespace IdleXIdle.Core.Presentation;

/// <summary>A rectangle in screen pixels. Plain data — Core owns no MonoGame types (ADR-001).</summary>
public readonly record struct FitRect(int X, int Y, int Width, int Height)
{
    public bool Contains(int px, int py) => px >= X && py >= Y && px < X + Width && py < Y + Height;
}

/// <summary>
/// How the fixed 480x270 canvas fits onto a monitor of any size.
/// </summary>
/// <remarks>
/// <para>
/// <b>Integer scale only, always.</b> Technical preferences forbid non-integer scaling on pixel art:
/// a fractional scale duplicates some source pixels and not others, so a 1px outline is 2px on one
/// side of the screen and 1px on the other, and it crawls as things move. A fullscreen canvas is
/// therefore never stretched to fit — it is drawn at the largest whole multiple that fits and centred,
/// with the remainder as letterbox. Slightly smaller and perfectly crisp beats edge-to-edge and smeared.
/// </para>
/// <para>
/// This lives in Core, and not next to the <c>GraphicsDeviceManager</c> that consumes it, because it is
/// pure arithmetic and it decides where every click lands. Once the canvas is letterboxed, a screen
/// pixel is no longer a canvas pixel times a constant, and getting that wrong misroutes input by a few
/// pixels — a bug that looks like "the button is flaky", not like bad math. So it gets tested.
/// </para>
/// </remarks>
public static class CanvasFit
{
    public const int CanvasWidth = 480;
    public const int CanvasHeight = 270;

    /// <summary>
    /// Scales offered in windowed mode. Beyond 4x exceeds most laptop panels. 2x (960x540) was cut
    /// after the 2026-08-23 playtest — "I cannot read any of the text, everything is tiny": the 1920
    /// chrome's body type lands at ~11 physical pixels there, below what the hand-drawn face can hold.
    /// 3x (1440x810) is the floor that stays readable.
    /// </summary>
    public static readonly int[] WindowedScales = { 3, 4 };

    /// <summary>
    /// The biggest whole multiple of the canvas that fits in a viewport.
    /// </summary>
    /// <remarks>
    /// Clamped to a floor of 1: on a viewport too small to hold even one full canvas, overflowing the
    /// screen is survivable, but a 0x scale is a divide-by-zero in the mouse mapping and a collapsed
    /// destination rect — an instant black screen.
    /// </remarks>
    public static int LargestIntegerScale(int viewportWidth, int viewportHeight)
        => Math.Max(1, Math.Min(viewportWidth / CanvasWidth, viewportHeight / CanvasHeight));

    /// <summary>Where the canvas is drawn: whole-number scaled and centred, leftover as letterbox.</summary>
    public static FitRect Present(int viewportWidth, int viewportHeight, int scale)
    {
        scale = Math.Max(1, scale);
        var w = CanvasWidth * scale;
        var h = CanvasHeight * scale;
        return new FitRect((viewportWidth - w) / 2, (viewportHeight - h) / 2, w, h);
    }

    /// <summary>
    /// Aspect-fit at ANY scale: the canvas fills the viewport's shorter axis edge to edge, centred.
    /// </summary>
    /// <remarks>
    /// The integer-only rule above belonged to the abandoned flat-fill pixel-art direction; the live
    /// renderer is hand-drawn art presented through LinearClamp, and the source is a 1920x1080 render
    /// target, so a fractional fit is a smooth DOWNSCALE, not pixel smearing. What forced this was the
    /// 2026-08-23 audit: on a 1366x768 or 1280x720 laptop the integer rule presented 960x540 with a
    /// letterbox on every side and ~7-pixel body text. Fullscreen and borderless use this; windowed
    /// keeps the exact integer sizes it advertises.
    /// </remarks>
    public static FitRect PresentFit(int viewportWidth, int viewportHeight)
    {
        var scale = MathF.Max(1f, MathF.Min(viewportWidth / (float)CanvasWidth, viewportHeight / (float)CanvasHeight));
        var w = (int)MathF.Round(CanvasWidth * scale);
        var h = (int)MathF.Round(CanvasHeight * scale);
        return new FitRect((viewportWidth - w) / 2, (viewportHeight - h) / 2, w, h);
    }

    /// <summary>
    /// Screen point → canvas point, through the letterbox.
    /// </summary>
    /// <remarks>
    /// A cursor on a letterbox bar maps outside the canvas, which every caller already treats as "no
    /// hit" — every hit-test is a Contains. Note the floor-division must round toward negative infinity,
    /// not toward zero: C# integer division truncates, so a screen point one pixel LEFT of the canvas
    /// would map to canvas x=0 and click the leftmost button from off-canvas.
    /// </remarks>
    public static (int X, int Y) ToCanvas(int screenX, int screenY, FitRect present)
    {
        // Float scale, so a PresentFit rect maps as exactly as an integer one. Floor toward negative
        // infinity for the same reason the old integer path did: a point one pixel LEFT of the canvas
        // must not round into it and click the leftmost button from off-canvas.
        var scale = MathF.Max(1f, present.Width / (float)CanvasWidth);
        return ((int)MathF.Floor((screenX - present.X) / scale), (int)MathF.Floor((screenY - present.Y) / scale));
    }

}
