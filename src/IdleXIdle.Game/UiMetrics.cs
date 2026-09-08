using System;

namespace IdleXIdle.Game;

/// <summary>
/// THE ONE SOURCE FOR EVERY SCALE-SENSITIVE SIZE. UI SCALE (100 / 125 / 150 %) is a DENSITY profile, not a
/// canvas zoom: the page stays 1920×1080 and what grows is type, rows, buttons, icons, hit targets and
/// padding — all of it read from here (UI polish brief §7–§11, LAW 7).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not scale the canvas.</b> The first UI SCALE (UX V2) multiplied every authored coordinate: the
/// page became 1280×720 logical at 150 % and every screen laid out for 1080 px of height overflowed —
/// GEAR's paper doll ran through its footer, TRAINING drew two groups over its reset bar — so the step
/// was withdrawn. A 1600×900 window "at 150 %" has 1067×600 of room; FORGE cannot fit in that at any
/// scale, and it does not need to: what a player at 150 % needs bigger is the text they read and the
/// things they click, not the paper doll or the map.
/// </para>
/// <para>
/// <b>The three rates.</b> Text and controls scale at the full factor (1.25, 1.5). Spacing scales at
/// HALF the rate (1.125, 1.25): padding that grew as fast as the type would spend the page's room on
/// air, and the brief's 150 % is an accessibility layout that keeps its structure by reflowing content,
/// not by inflating gaps. A screen never writes <c>* 1.25f</c>; it names a base size and asks
/// <see cref="Text"/>, <see cref="Control"/> or <see cref="Space"/> for the profile's version of it.
/// </para>
/// <para>
/// <b>Rounding.</b> Every result is an integer, away from zero at .5, so a rung of 22 is 28 at 125 %
/// rather than 27 or 27.5 — the font cache keys on integer pixel sizes and a half pixel would mint an
/// atlas per frame. Set once by <c>Game1.ApplyUiScale</c>; read everywhere.
/// </para>
/// </remarks>
public static class UiMetrics
{
    /// <summary>The profile: 100, 125 or 150.</summary>
    public static int Percent { get; private set; } = 100;

    /// <summary>The profile as a factor: 1, 1.25 or 1.5.</summary>
    public static float Scale => Percent / 100f;

    /// <summary>What text grows by — the full factor.</summary>
    public static float TextScale => Scale;

    /// <summary>What buttons, rows, icons and hit targets grow by — the full factor.</summary>
    public static float ControlScale => Scale;

    /// <summary>What padding and gaps grow by — half the rate, so the page keeps its room.</summary>
    public static float SpacingScale => 1f + (Scale - 1f) * 0.5f;

    /// <summary>Apply a profile. Anything but 100, 125 or 150 is 100 — a preference must never resolve to a state the game cannot draw.</summary>
    public static void Apply(int percent) => Percent = percent is 125 or 150 ? percent : 100;

    /// <summary>A text size (a <see cref="UiTypography"/> rung, at 100 %) at this profile.</summary>
    public static int Text(int px) => Round(px * TextScale);

    /// <summary>A control dimension (a button height, an icon edge, a row) at this profile.</summary>
    public static int Control(int px) => Round(px * ControlScale);

    /// <summary>A spacing (a pad, a gap, an inset) at this profile.</summary>
    public static int Space(int px) => Round(px * SpacingScale);

    // ── The named sizes: one base each, so two screens cannot disagree about what a row is ─────────

    /// <summary>A list row's pitch, for generic lists (settings toggles, dropdown rows).</summary>
    public static int RowHeight => Control(48);

    /// <summary>A standard button's height.</summary>
    public static int ButtonHeight => Control(52);

    /// <summary>A small button's height — a chip-sized verb, an inline OK.</summary>
    public static int ButtonHeightSmall => Control(40);

    /// <summary>A panel's primary call-to-action height (the inspector's one lit button).</summary>
    public static int ButtonHeightPrimary => Control(64);

    /// <summary>An icon's edge beside a row or a label.</summary>
    public static int IconSize => Control(40);

    /// <summary>A small glyph's edge — a Source gem in a corner, a lock on a tile.</summary>
    public static int IconSmall => Control(24);

    /// <summary>A panel's inner padding where the frame's ornament does not dictate it.</summary>
    public static int PanelPadding => Space(24);

    /// <summary>The gap between two things that belong together.</summary>
    public static int Gap => Space(12);

    /// <summary>A scrollbar's grab width — wide enough to take at every profile.</summary>
    public static int ScrollbarWidth => Control(10);

    /// <summary>The smallest edge an interactive target may have (brief §107).</summary>
    public static int HitTargetMinimum => Control(40);

    /// <summary>
    /// An inspector column's width for a page this wide: the house 496 at 100 %, wider at the larger
    /// profiles so the bigger type keeps its line length, never more than two fifths of the page.
    /// </summary>
    public static int InspectorWidth(int pageWidth) => Math.Min(Control(496), pageWidth * 2 / 5);

    private static int Round(float v) => (int)MathF.Round(v, MidpointRounding.AwayFromZero);
}
