using System;

namespace ResonanceHunter.Core.Presentation;

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
        var scale = Math.Max(1, present.Width / CanvasWidth);
        return (FloorDiv(screenX - present.X, scale), FloorDiv(screenY - present.Y, scale));
    }

    private static int FloorDiv(int a, int b)
    {
        var q = a / b;
        return a % b != 0 && (a < 0) != (b < 0) ? q - 1 : q;
    }
}
