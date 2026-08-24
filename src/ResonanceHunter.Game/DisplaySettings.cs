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
    public static Rectangle PresentFit(int viewportWidth, int viewportHeight)
    {
        var r = CanvasFit.PresentFit(viewportWidth, viewportHeight);
        return new Rectangle(r.X, r.Y, r.Width, r.Height);
    }

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
    /// Volumes are 0..100 percent (2026-08-24, for the draggable settings sliders — the old 0..10
    /// notches could not express "a little quieter"). ASK BEFORE SCRAP lives here rather than in the
    /// save for the same reason the display mode does: it is a preference about how the player wants to
    /// be treated, and starting a new game must not resurrect a dialog they turned off.
    /// </remarks>
    public readonly record struct GamePrefs(
        DisplayMode Mode, int WindowedScale, int SfxVolume, int MusicVolume, bool AskBeforeScrap,
        bool ShowDamageNumbers = true, bool ShowSkillCallouts = true, bool ShowHitEffects = true, bool ShowScreenFlash = true);

    // ── FILE FORMAT ────────────────────────────────────────────────────────────────────────────
    // One value per line: Mode, WindowedScale, SfxVolume, MusicVolume, AskBeforeScrap, then the four
    // fight-effect switches (1/0 each). Lines 3-4 are the volumes, and their SCALE changed on
    // 2026-08-24: files written since carry a trailing marker line, "volume=percent", and store 0..100;
    // files without the marker are the old 0..10 notches and are migrated on load by multiplying by 10.
    // A new-scale file read by an old build clamps its volumes into 0..10, which is safely loud rather
    // than silent. Nothing here versions the whole file — every other line kept its meaning.
    private const string PercentMarker = "volume=percent";

    public static void Save(GamePrefs p)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefsPath)!);
            System.IO.File.WriteAllText(PrefsPath,
                $"{p.Mode}\n{p.WindowedScale}\n{p.SfxVolume}\n{p.MusicVolume}\n{(p.AskBeforeScrap ? 1 : 0)}\n"
                + $"{(p.ShowDamageNumbers ? 1 : 0)}\n{(p.ShowSkillCallouts ? 1 : 0)}\n{(p.ShowHitEffects ? 1 : 0)}\n{(p.ShowScreenFlash ? 1 : 0)}\n"
                + PercentMarker + "\n");
        }
        catch (System.IO.IOException) { /* prefs are a convenience; never block the game on them */ }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// The out-of-the-box default: Fullscreen, a windowed fallback of scale 4 (= exactly 1920x1080, the
    /// canvas's native size), effects at 80% and music at 50% (the volumes the SoundBank always used),
    /// and the SELL/SALVAGE dialog asking.
    /// </summary>
    private static readonly GamePrefs Default = new(DisplayMode.Fullscreen, 4, 80, 50, true);

    public static GamePrefs Load()
    {
        try
        {
            if (!System.IO.File.Exists(PrefsPath)) return Default;
            var lines = System.IO.File.ReadAllLines(PrefsPath);
            string At(int i) => lines.Length > i ? lines[i].Trim() : "";
            var mode = Enum.TryParse<DisplayMode>(At(0), out var m) ? m : Default.Mode;
            // A retired scale (the old 2x = 960x540) migrates to the SMALLEST surviving size — its
            // owner chose small on purpose, and the largest window on that screen may not even fit.
            var scale = int.TryParse(At(1), out var s)
                ? (Array.IndexOf(WindowedScales, s) >= 0 ? s : WindowedScales[0])
                : Default.WindowedScale;
            // Lines 3+ arrived with the sound settings; an older two-line file just gets the defaults.
            // The volume SCALE is decided by the marker line — see FILE FORMAT above: with it the
            // values are already 0..100; without it they are the old 0..10 notches, migrated by ×10.
            var percent = Array.Exists(lines, l => l.Trim() == PercentMarker);
            var sfx = int.TryParse(At(2), out var fx)
                ? (percent ? Math.Clamp(fx, 0, 100) : Math.Clamp(fx, 0, 10) * 10)
                : Default.SfxVolume;
            var music = int.TryParse(At(3), out var mu)
                ? (percent ? Math.Clamp(mu, 0, 100) : Math.Clamp(mu, 0, 10) * 10)
                : Default.MusicVolume;
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
