using Microsoft.Xna.Framework;

namespace IdleXIdle.Game;

/// <summary>
/// THE INKS. One owner for every text and rule colour in the game.
/// </summary>
/// <remarks>
/// <para>
/// The 2026-09-01 UX audit found the palette copied privately into thirteen files — and disagreeing:
/// <c>Dim</c> was <c>#2C2C36</c> on HUNT and MASTERY, <c>#3A3A44</c> on seven other screens and
/// <c>#5E5A6E</c> on TRAITS; gold was <c>#F0A830</c> on the screens but <c>#F0B24A</c> on the rail, the
/// titles and the style ring; the Forge had its own second "faint" grey. Worse, the SECONDARY ink and
/// the DISABLED ink shared a luminance band (6.45:1 vs 4.4–5:1 on the panel), so "less important" and
/// "unavailable" read as the same grey. The screens keep their short local names as aliases of these
/// tokens — so no call site moved — but the VALUE lives here, once.
/// </para>
/// <para>
/// Contrast ratios are measured on the panel interior (<c>#100C16</c>). The rule the audit set:
/// Secondary never falls below 6:1 (a 720p player needs it more than a 1080p one); Disabled is the ONLY
/// ink allowed under 4:1, and it is always paired with a second cue — a lock glyph, a strike, a
/// requirement line — because colour alone must never carry a state.
/// </para>
/// </remarks>
public static class UiInk
{
    /// <summary>Names, values, sentences — anything the player must read. 14.6:1.</summary>
    public static readonly Color Primary = new(0xE8, 0xDF, 0xC8);

    /// <summary>Labels, units, category names, metadata. 6.45:1 — readable, quieter, never disabled.</summary>
    public static readonly Color Secondary = new(0x8A, 0x96, 0xA8);

    /// <summary>Hearth Gold: earned, selected, active, the primary action. Never decoration. 9.5:1.</summary>
    public static readonly Color Accent = new(0xF0, 0xA8, 0x30);

    /// <summary>The only ink under 4:1. Off buttons, locked rows, actions you cannot take right now.</summary>
    public static readonly Color Disabled = new(0x5A, 0x56, 0x64);

    /// <summary>Hairlines and dividers. Never text.</summary>
    public static readonly Color Rule = new(0x3A, 0x3A, 0x44);

    /// <summary>A met condition, a gain, a healthy state.</summary>
    public static readonly Color Good = new(0x6E, 0xC8, 0x7A);

    /// <summary>An unmet condition, a loss, a destructive action.</summary>
    public static readonly Color Danger = new(0xD8, 0x48, 0x3A);

    /// <summary>The QUIET tier's surface — dark, translucent, drawn by <see cref="UiKit.Plate"/>.</summary>
    public static readonly Color Plate = new(0x10, 0x0D, 0x18, 0xE0);

    /// <summary>An empty slot's placeholder: Secondary at 60% — distinct from Disabled by shape, never tone alone.</summary>
    public static readonly Color Empty = Secondary * 0.6f;
}
