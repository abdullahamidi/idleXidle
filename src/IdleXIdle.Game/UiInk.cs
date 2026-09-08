using Microsoft.Xna.Framework;

namespace IdleXIdle.Game;

/// <summary>
/// THE INK TOKENS — the one place a text colour or a surface colour is named.
/// </summary>
/// <remarks>
/// <para>
/// UX V2 P0.3 gave the text its tokens; the follow-up polish of 2026-09-06 gave the SURFACES theirs
/// and warmed the whole set. The menus read noticeably colder than the arena — every ground was a
/// violet-black spelt eight ways across the screens (#0A0810, #0C0916, #0E0B16, #100D18, #14101A,
/// #14111A, #14111C, #14111E) under a cold blue-grey Secondary — and the arena is earth, moss and
/// stone. The grounds are charcoal / brown-black now, the hairlines and passive ornament aged bronze,
/// the active gold a step brighter: bronze is structure, gold is importance and interaction.
/// </para>
/// <para>
/// Contrast, measured against <see cref="Ground"/> (#15100F): Primary 15.6:1, Secondary 8.0:1,
/// Accent 9.7:1, Disabled 2.9:1 (the one ink under 4:1, by design — it is the disabled state), Good
/// 9.0:1, Danger 4.4:1. Secondary was 6.4:1 on the old ground; it is brighter, not as bright as
/// Primary, so the hierarchy PRIMARY / SECONDARY / DISABLED stands.
/// </para>
/// </remarks>
public static class UiInk
{
    /// <summary>Body and headline text — the art bible's Bone Parchment.</summary>
    public static readonly Color Primary = new(0xE8, 0xDF, 0xC8);

    /// <summary>
    /// Secondary ink — captions, labels, the second line. Brighter and warmer since 2026-09-06 (it
    /// was #8A96A8, a cold blue-grey): a warm stone-grey, comfortably readable at monitor distance and
    /// still clearly under Primary.
    /// </summary>
    public static readonly Color Secondary = new(0xA9, 0xA5, 0x9E);

    /// <summary>The active gold — selected, affordable, important. A step brighter than the Hearth Gold it was (#F0A830).</summary>
    public static readonly Color Accent = new(0xF6, 0xB2, 0x3A);

    /// <summary>The one ink under 4:1, on purpose: a control that cannot be used.</summary>
    public static readonly Color Disabled = new(0x66, 0x5E, 0x58);

    /// <summary>Hairlines and quiet edges — aged bronze now, not slate: structure is bronze, importance is gold.</summary>
    public static readonly Color Rule = new(0x4A, 0x3F, 0x36);

    /// <summary>Favourable to the hunter — a met requirement, a gain.</summary>
    public static readonly Color Good = new(0x6E, 0xC8, 0x7A);

    /// <summary>Unfavourable — a shortfall, a loss, a refusal. The art bible's Ember Threat.</summary>
    public static readonly Color Danger = new(0xD8, 0x48, 0x3A);

    /// <summary>The QUIET surface — the warm charcoal-brown ground, translucent (it was a cold violet-black #100D18).</summary>
    public static readonly Color Plate = new(0x15, 0x10, 0x0F, 0xE0);

    /// <summary>A placeholder's ink — an empty slot's word, a zero rate.</summary>
    public static readonly Color Empty = Secondary * 0.6f;

    // ── THE GROUNDS (2026-09-06), named once so the chrome sits nearer the arena's earth and stone. ──

    /// <summary>The page's own ground, opaque — what a panel's field and a well are painted.</summary>
    public static readonly Color Ground = new(0x15, 0x10, 0x0F);

    /// <summary>One readable value step above the ground — a raised panel, a card.</summary>
    public static readonly Color Raised = new(0x22, 0x1A, 0x17);

    /// <summary>A row under the pointer, a selected cell — one more step, still dark.</summary>
    public static readonly Color Hover = new(0x2E, 0x24, 0x20);

    /// <summary>The void behind everything: the canvas clear, the letterbox.</summary>
    public static readonly Color Void = new(0x1D, 0x17, 0x14);

    /// <summary>Passive ornament — the tint that turns the gold frame art to aged bronze for the quiet tier.</summary>
    public static readonly Color Bronze = new(0x7A, 0x62, 0x44);
}
