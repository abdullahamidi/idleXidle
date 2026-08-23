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

    /// <summary>Everything the prefs file holds — machine-level, never playthrough-level.</summary>
    /// <remarks>
    /// Volumes are 0..10 steps rather than floats: eleven notches give the slider a real middle, and the
    /// file stays hand-readable. ASK BEFORE SCRAP lives here rather than in the save for the same reason
    /// the display mode does: it is a preference about how the player wants to be treated, and starting
    /// a new game must not resurrect a dialog they turned off.
    /// </remarks>
    public readonly record struct GamePrefs(
        DisplayMode Mode, int WindowedScale, int SfxVolume, int MusicVolume, bool AskBeforeScrap,
        bool ShowDamageNumbers = true, bool ShowSkillCallouts = true, bool ShowHitEffects = true, bool ShowScreenFlash = true);

    public static void Save(GamePrefs p)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefsPath)!);
            System.IO.File.WriteAllText(PrefsPath,
                $"{p.Mode}\n{p.WindowedScale}\n{p.SfxVolume}\n{p.MusicVolume}\n{(p.AskBeforeScrap ? 1 : 0)}\n"
                + $"{(p.ShowDamageNumbers ? 1 : 0)}\n{(p.ShowSkillCallouts ? 1 : 0)}\n{(p.ShowHitEffects ? 1 : 0)}\n{(p.ShowScreenFlash ? 1 : 0)}\n");
        }
        catch (System.IO.IOException) { /* prefs are a convenience; never block the game on them */ }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// The out-of-the-box default: Fullscreen, a windowed fallback of scale 4 (= exactly 1920x1080, the
    /// canvas's native size), effects at 8/10 and music at 5/10 (the volumes the SoundBank always used),
    /// and the SELL/SALVAGE dialog asking.
    /// </summary>
    private static readonly GamePrefs Default = new(DisplayMode.Fullscreen, 4, 8, 5, true);

    public static GamePrefs Load()
    {
        try
        {
            if (!System.IO.File.Exists(PrefsPath)) return Default;
            var lines = System.IO.File.ReadAllLines(PrefsPath);
            string At(int i) => lines.Length > i ? lines[i].Trim() : "";
            var mode = Enum.TryParse<DisplayMode>(At(0), out var m) ? m : Default.Mode;
            var scale = int.TryParse(At(1), out var s) && Array.IndexOf(WindowedScales, s) >= 0
                ? s : Default.WindowedScale;
            // Lines 3+ arrived with the sound settings; an older two-line file just gets the defaults.
            var sfx = int.TryParse(At(2), out var fx) ? Math.Clamp(fx, 0, 10) : Default.SfxVolume;
            var music = int.TryParse(At(3), out var mu) ? Math.Clamp(mu, 0, 10) : Default.MusicVolume;
            var ask = At(4) != "0";
            // Lines 6-9 (2026-08-23): the fight's text and effects. Absent in an older file = on.
            return new GamePrefs(mode, scale, sfx, music, ask,
                ShowDamageNumbers: At(5) != "0", ShowSkillCallouts: At(6) != "0",
                ShowHitEffects: At(7) != "0", ShowScreenFlash: At(8) != "0");
        }
        catch (System.IO.IOException) { return Default; }
        catch (UnauthorizedAccessException) { return Default; }
    }
}
