using System;
using Microsoft.Xna.Framework;
using ResonanceHunter.Core.Presentation;

namespace ResonanceHunter.Client;

public enum DisplayMode { Windowed, Borderless, Fullscreen }

/// <summary>
/// How the 480x270 canvas reaches the monitor — the MonoGame-facing skin over <see cref="CanvasFit"/>.
/// </summary>
/// <remarks>
/// The geometry lives in Core (pure, tested); this only translates it into MonoGame's Rectangle/Point
/// and owns the prefs file. Keep it that way — anything with arithmetic in it belongs on the other side.
/// </remarks>
public static class Display
{
    public const int CanvasWidth = CanvasFit.CanvasWidth;
    public const int CanvasHeight = CanvasFit.CanvasHeight;

    public static int[] WindowedScales => CanvasFit.WindowedScales;

    public static int LargestIntegerScale(int viewportWidth, int viewportHeight)
        => CanvasFit.LargestIntegerScale(viewportWidth, viewportHeight);

    /// <summary>Where the canvas is drawn inside the viewport: integer-scaled, centred, letterboxed.</summary>
    public static Rectangle Present(int viewportWidth, int viewportHeight, int scale)
    {
        var r = CanvasFit.Present(viewportWidth, viewportHeight, scale);
        return new Rectangle(r.X, r.Y, r.Width, r.Height);
    }

    /// <summary>Screen point → canvas point, accounting for the letterbox origin.</summary>
    public static Point ToCanvas(Point screen, Rectangle present)
    {
        var (x, y) = CanvasFit.ToCanvas(screen.X, screen.Y,
            new FitRect(present.X, present.Y, present.Width, present.Height));
        return new Point(x, y);
    }

    /// <summary>
    /// Display prefs live in their OWN file, not in the save.
    /// </summary>
    /// <remarks>
    /// A monitor is a property of the machine, not of the playthrough: starting a new game must not
    /// throw the player back into a small window, and a corrupt save must not cost them a readable
    /// screen. It is two lines — parsed by hand rather than dragging in a serializer, and any failure
    /// falls back to the default rather than blocking startup on a prefs file.
    /// </remarks>
    private static string PrefsPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ResonanceHunter", "display.txt");

    public static void Save(DisplayMode mode, int windowedScale)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefsPath)!);
            System.IO.File.WriteAllText(PrefsPath, $"{mode}\n{windowedScale}\n");
        }
        catch (System.IO.IOException) { /* prefs are a convenience; never block the game on them */ }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// The out-of-the-box default: Fullscreen, and a windowed fallback of scale 4 (= exactly 1920x1080, the
    /// canvas's native size). On a 1080p monitor Fullscreen presents at 1920x1080; anything else is one
    /// click away in Settings. The scale only matters once the player switches to Windowed.
    /// </summary>
    private static readonly (DisplayMode Mode, int Scale) Default = (DisplayMode.Fullscreen, 4);

    public static (DisplayMode Mode, int Scale) Load()
    {
        try
        {
            if (!System.IO.File.Exists(PrefsPath)) return Default;
            var lines = System.IO.File.ReadAllLines(PrefsPath);
            var mode = lines.Length > 0 && Enum.TryParse<DisplayMode>(lines[0].Trim(), out var m) ? m : Default.Mode;
            var scale = lines.Length > 1 && int.TryParse(lines[1].Trim(), out var s) && Array.IndexOf(WindowedScales, s) >= 0
                ? s : Default.Scale;
            return (mode, scale);
        }
        catch (System.IO.IOException) { return Default; }
        catch (UnauthorizedAccessException) { return Default; }
    }
}
