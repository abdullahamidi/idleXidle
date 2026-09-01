using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using IdleXIdle.Core.Presentation;

namespace IdleXIdle.Game;

public enum DisplayMode { Windowed, Borderless, Fullscreen }

/// <summary>
/// One window size in real screen pixels — a single entry in the settings panel's WINDOW SIZE list.
/// </summary>
/// <remarks>
/// The game always renders the same 1920×1080 canvas; a window size only decides how big that canvas
/// is drawn. So this is a PRESENTATION number, never a layout one: nothing on any screen moves when it
/// changes, and a size that is not 16:9 simply gets letterbox bars.
/// </remarks>
public readonly record struct WindowSize(int Width, int Height)
{
    /// <summary>"1920 × 1080" — the multiplication sign, which is the one the font gate has proven.</summary>
    public override string ToString() => $"{Width} × {Height}";
}

/// <summary>
/// How the 1920×1080 canvas reaches the monitor — the MonoGame-facing skin over <see cref="CanvasFit"/>.
/// </summary>
/// <remarks>
/// The geometry lives in Core (pure, tested); this only translates it into MonoGame's Rectangle/Point
/// and owns the prefs file. Keep it that way — anything with arithmetic in it belongs on the other side.
/// </remarks>
public static class Display
{
    public const int CanvasWidth = CanvasFit.CanvasWidth;
    public const int CanvasHeight = CanvasFit.CanvasHeight;

    // THE INTEGER-SCALE WRAPPERS ARE GONE (2026-08-27): Present(vw, vh, scale) and
    // LargestIntegerScale had one caller each, the windowed branch of RecomputePresent, and windowed
    // now fits the same fractional way every other mode does. CanvasFit still owns and tests the
    // arithmetic; nothing in the client asks for it any more, and a wrapper nothing calls is the kind
    // of thing that gets built on by mistake.

    // ── WINDOW SIZES ──────────────────────────────────────────────────────────────────────────
    // WHAT REPLACED THE INTEGER SCALE LADDER (2026-08-27). WINDOW SIZE used to offer two entries,
    // 3× and 4× the 480×270 canvas — 1440×810 and 1920×1080. Neither is a resolution anybody's
    // monitor advertises, the list read as a developer's unit rather than a display setting, and a
    // 1440p or 4K player had no size that used their screen. Core's CanvasFit.WindowedScales is the
    // retired ladder; it stays there because its arithmetic is still what the fullscreen fit is
    // tested against, and nothing offers it to a player any more.
    //
    // THE RULE, in one sentence: offer the standard 16:9 sizes that FIT on this desktop, plus the
    // desktop's own size, marked "(native)". Never a size larger than the desktop — a window bigger
    // than the screen has its title bar off the top and cannot be moved back.

    /// <summary>The standard sizes, smallest first. All 16:9, the canvas's own aspect, so none letterboxes.</summary>
    /// <remarks>
    /// 1280×720 is the floor: the chrome's body type is authored at 19 logical px, which lands at 12.7
    /// physical px there — the smallest the hand-drawn face still reads at (the 2026-08-23 playtest
    /// killed 960×540 for exactly this reason). 3840×2160 is the ceiling anybody ships.
    /// </remarks>
    private static readonly WindowSize[] Catalogue =
    {
        new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3840, 2160),
    };

    /// <summary>
    /// The WINDOW SIZE list for this machine: every catalogue size that fits on the desktop, plus the
    /// desktop's own size, sorted small to large.
    /// </summary>
    /// <remarks>
    /// The desktop's own size is always in the list even when it is not a catalogue size, which is what
    /// makes this useful on the odd panels: a 1366×768 laptop is offered 1280 × 720 and 1366 × 768
    /// (native) and nothing else. The list can never be empty for the same reason — a desktop always
    /// fits itself.
    /// </remarks>
    public static WindowSize[] OfferedWindowSizes(int desktopWidth, int desktopHeight)
    {
        var offered = new List<WindowSize>();
        foreach (var s in Catalogue)
            if (s.Width <= desktopWidth && s.Height <= desktopHeight) offered.Add(s);

        var native = new WindowSize(Math.Max(1, desktopWidth), Math.Max(1, desktopHeight));
        if (!offered.Contains(native)) offered.Add(native);
        offered.Sort((a, b) => a.Width != b.Width ? a.Width.CompareTo(b.Width) : a.Height.CompareTo(b.Height));
        return offered.ToArray();
    }

    /// <summary>What one row of the WINDOW SIZE list says: "1600 × 900", or "1920 × 1080 (native)".</summary>
    public static string WindowSizeLabel(WindowSize s, int desktopWidth, int desktopHeight)
        => s.Width == desktopWidth && s.Height == desktopHeight ? $"{s} (native)" : s.ToString();

    /// <summary>
    /// The offered size nearest one that was asked for — how a stored, migrated or now-impossible size
    /// becomes a real one.
    /// </summary>
    /// <remarks>
    /// Every door into windowed mode goes through here, so a prefs file written on a 4K desktop and
    /// carried to a laptop opens a window that fits rather than one hanging off the screen.
    /// </remarks>
    public static WindowSize NearestOffered(WindowSize wanted, int desktopWidth, int desktopHeight)
    {
        var offered = OfferedWindowSizes(desktopWidth, desktopHeight);
        var best = offered[0];
        var bestGap = int.MaxValue;
        foreach (var s in offered)
        {
            if (s == wanted) return s;
            var gap = Math.Abs(s.Width - wanted.Width) + Math.Abs(s.Height - wanted.Height);
            if (gap >= bestGap) continue;
            bestGap = gap;
            best = s;
        }
        return best;
    }

    /// <summary>Where the canvas is drawn inside the viewport: uniformly scaled, centred, letterboxed.</summary>
    /// <remarks>
    /// EVERY display mode goes through here now, windowed included. A window size is a real resolution
    /// rather than a whole multiple of the canvas, so the fit has to be fractional — and it is a smooth
    /// DOWNSCALE of a 1920×1080 render target through LinearClamp, not a pixel-art upscale. The scale is
    /// the same on both axes, so nothing is ever stretched; a non-16:9 window gets bars, not distortion.
    /// </remarks>
    public static Rectangle PresentFit(int viewportWidth, int viewportHeight)
    {
        var r = CanvasFit.PresentFit(viewportWidth, viewportHeight);
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
        SaveFile.AppDataFolderName, "display.txt");

    /// <summary>Everything the prefs file holds — machine-level, never playthrough-level.</summary>
    /// <remarks>
    /// Volumes are 0..100 percent (2026-08-24, for the draggable settings sliders — the old 0..10
    /// notches could not express "a little quieter"). ASK BEFORE SCRAP lives here rather than in the
    /// save for the same reason the display mode does: it is a preference about how the player wants to
    /// be treated, and starting a new game must not resurrect a dialog they turned off.
    /// </remarks>
    /// <param name="UiScalePercent">
    /// UI SCALE: 100, 125 or 150, or 0 for AUTO (125 in a window under 1600 px wide, else 100). It scales the
    /// PAGE — every menu screen's panels and text — inside the fixed 1920×1080 canvas; the nav rail and the
    /// fight are not pages and do not move. Written as <c>uiscale=100|125|150|auto</c>; an old build never
    /// reads the line, a new build reads 100 when it is absent. 100 is the default until every page screen
    /// lays out to <see cref="UiKit.Page"/> (UX V2 P1); Auto becomes the default then.
    /// </param>
    public readonly record struct GamePrefs(
        DisplayMode Mode, WindowSize Window, int SfxVolume, int MusicVolume, bool AskBeforeScrap,
        bool ShowDamageNumbers = true, bool ShowSkillCallouts = true, bool ShowHitEffects = true, bool ShowScreenFlash = true,
        int UiScalePercent = 100, bool ReducedMotion = false);

    /// <summary>
    /// The UI SCALE steps the game OFFERS. 0 is AUTO.
    /// </summary>
    /// <remarks>
    /// <b>150 IS NOT OFFERED YET, AND THE REASON IS VERTICAL.</b> The page a scale leaves is 1920/s wide by
    /// 1080/s tall, so 150% gives a screen 720 logical pixels of height where its rows, its slot columns and
    /// its grids were laid out against 1080. Posed at RH_SHOT_UISCALE=150 on 2026-09-01, every converted
    /// screen overflowed: BUILD's bench readout printed through its third slot row, GEAR's paper doll ran out
    /// of its panel, TRAITS' permanence line landed on YOU NEED FIRST. Width was fine everywhere — the columns
    /// already follow <see cref="UiKit.Page"/> — so this is not a layout bug to nudge, it is work: every
    /// vertical rhythm (row pitch, slot pitch, visible grid rows) has to be derived from the height that is
    /// actually there rather than assumed. Until that pass lands, offering the step would ship a setting that
    /// produces a broken screen. The rig can still pose 150 (RH_SHOT_UISCALE) so that work can see it.
    /// </remarks>
    public static readonly int[] UiScaleSteps = { 100, 125, 0 };

    /// <summary>What a UI SCALE preference resolves to for a present rect this wide: AUTO picks 125 under 1600 px.</summary>
    public static int ResolveUiScale(int percent, int presentWidth)
        => percent is 100 or 125 or 150 ? percent : presentWidth < 1600 ? 125 : 100;

    /// <summary>The preference's name in the settings: "100%", "150%", or "AUTO".</summary>
    public static string UiScaleLabel(int percent) => percent is 100 or 125 or 150 ? $"{percent}%" : "AUTO";

    private const string UiScaleKey = "uiscale=";

    /// <summary>REDUCED MOTION, written as <c>motion=0|1</c>. Absent in an older file means off.</summary>
    private const string MotionKey = "reducedmotion=";

    // ── FILE FORMAT ────────────────────────────────────────────────────────────────────────────
    // One value per line: Mode, Window, SfxVolume, MusicVolume, AskBeforeScrap, then the four
    // fight-effect switches (1/0 each). Lines 3-4 are the volumes, and their SCALE changed on
    // 2026-08-24: files written since carry a trailing marker line, "volume=percent", and store 0..100;
    // files without the marker are the old 0..10 notches and are migrated on load by multiplying by 10.
    // A new-scale file read by an old build clamps its volumes into 0..10, which is safely loud rather
    // than silent. Nothing here versions the whole file — every other line kept its meaning.
    //
    // LINE 2 CHANGED SHAPE on 2026-08-27, and BOTH DIRECTIONS ARE SAFE. It used to be a bare integer
    // scale ("3" or "4"); it is now "WIDTHxHEIGHT" ("1920x1080"), written with a plain ASCII x so the
    // file stays greppable and locale-proof — the × is for the screen only. Reading an old file, a bare
    // integer is migrated below. An OLD build reading a NEW file fails its int.TryParse and falls back
    // to its own default scale, which is a window, not a black screen.
    private const string PercentMarker = "volume=percent";

    public static void Save(GamePrefs p)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefsPath)!);
            System.IO.File.WriteAllText(PrefsPath,
                $"{p.Mode}\n{p.Window.Width}x{p.Window.Height}\n{p.SfxVolume}\n{p.MusicVolume}\n{(p.AskBeforeScrap ? 1 : 0)}\n"
                + $"{(p.ShowDamageNumbers ? 1 : 0)}\n{(p.ShowSkillCallouts ? 1 : 0)}\n{(p.ShowHitEffects ? 1 : 0)}\n{(p.ShowScreenFlash ? 1 : 0)}\n"
                + PercentMarker + "\n"
                + UiScaleKey + (p.UiScalePercent is 100 or 125 or 150 ? p.UiScalePercent.ToString() : "auto") + "\n"
                + MotionKey + (p.ReducedMotion ? "1" : "0") + "\n");
        }
        catch (System.IO.IOException) { /* prefs are a convenience; never block the game on them */ }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// The out-of-the-box default: Fullscreen, a windowed fallback of 1920×1080 (the canvas's own size),
    /// effects at 80% and music at 50% (the volumes the SoundBank always used), and the SELL/SALVAGE
    /// dialog asking.
    /// </summary>
    private static readonly GamePrefs Default = new(DisplayMode.Fullscreen, new WindowSize(1920, 1080), 80, 50, true);

    /// <summary>
    /// Line 2, in either shape: "1920x1080", or an old bare integer scale.
    /// </summary>
    /// <remarks>
    /// The retired ladder was 3× (1440×810) and 4× (1920×1080). 4 survives verbatim, because it is a
    /// size the new list actually offers. 3 does not, and it migrates DOWN to the smallest offered size
    /// rather than up — its owner chose the small window on purpose, and the machine it is loaded on
    /// may not have room for a bigger one. The size is only a request either way:
    /// <see cref="NearestOffered"/> has the last word once the desktop is known.
    /// </remarks>
    private static WindowSize ParseWindow(string line)
    {
        var parts = line.Split('x', 'X');
        if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h)
            && w > 0 && h > 0)
            return new WindowSize(w, h);
        if (int.TryParse(line, out var scale)) return scale >= 4 ? new WindowSize(1920, 1080) : new WindowSize(1280, 720);
        return Default.Window;
    }

    public static GamePrefs Load()
    {
        try
        {
            if (!System.IO.File.Exists(PrefsPath)) return Default;
            var lines = System.IO.File.ReadAllLines(PrefsPath);
            string At(int i) => lines.Length > i ? lines[i].Trim() : "";
            var mode = Enum.TryParse<DisplayMode>(At(0), out var m) ? m : Default.Mode;
            var window = ParseWindow(At(1));
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
            // A keyed line (2026-09-01), found wherever it sits, so the positional lines above never move.
            var uiScale = 100;
            var reduced = false;
            foreach (var l in lines)
                if (l.Trim().StartsWith(MotionKey, StringComparison.Ordinal))
                    reduced = l.Trim()[MotionKey.Length..] == "1";
            foreach (var l in lines)
            {
                if (!l.Trim().StartsWith(UiScaleKey, StringComparison.Ordinal)) continue;
                var v = l.Trim()[UiScaleKey.Length..];
                // A saved 150 comes back as 125 while the step is not offered (see UiScaleSteps): a preference
                // must never resolve to a state the game cannot draw.
                uiScale = v == "auto" ? 0 : int.TryParse(v, out var pct) && pct is 100 or 125 ? pct : v == "150" ? 125 : 100;
            }
            // Lines 6-9 (2026-08-23): the fight's text and effects. Absent in an older file = on.
            return new GamePrefs(mode, window, sfx, music, ask,
                ShowDamageNumbers: At(5) != "0", ShowSkillCallouts: At(6) != "0",
                ShowHitEffects: At(7) != "0", ShowScreenFlash: At(8) != "0",
                UiScalePercent: uiScale, ReducedMotion: reduced);
        }
        catch (System.IO.IOException) { return Default; }
        catch (UnauthorizedAccessException) { return Default; }
    }
}
