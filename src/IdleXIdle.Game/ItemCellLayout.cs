using System;
using IdleXIdle.Core.Loot;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game;

/// <summary>
/// A rarity frame's INTERIOR — how far in from each edge the see-through hole starts, as a fraction of
/// the frame's edge — and whether the centre is see-through at all.
/// </summary>
/// <remarks>
/// Measured from the texture by <see cref="ItemArtMetrics.FrameInterior(Microsoft.Xna.Framework.Graphics.Texture2D?)"/>.
/// A frame whose centre is painted (the 2026 legacy set, near-black inside its ring) cannot be measured
/// and reports <see cref="Fallback"/>: the 15 % inset the renderer used before any frame was measured,
/// with <see cref="Transparent"/> false so the renderer knows to keep tinting it.
/// </remarks>
public readonly record struct FrameInterior(float Left, float Top, float Right, float Bottom, bool Transparent)
{
    /// <summary>An unmeasurable frame: the legacy 15 % inset on every side, painted centre.</summary>
    public static readonly FrameInterior Fallback = new(ItemCellLayout.FallbackInset, ItemCellLayout.FallbackInset,
                                                        ItemCellLayout.FallbackInset, ItemCellLayout.FallbackInset, false);

    /// <summary>No frame at all: the whole rectangle is interior.</summary>
    public static readonly FrameInterior None = new(0f, 0f, 0f, 0f, true);
}

/// <summary>
/// THE ITEM CELL'S GEOMETRY, in one place. A cell is three layers with three jobs — the SLOT WELL (the
/// caller's cell, which says "a place an item goes"), the RARITY FRAME (which says how good), and the
/// GLYPH (which says what) — and every rectangle each layer is drawn into comes from here.
/// </summary>
/// <remarks>
/// <para>
/// Before this class (2026-09-16) the one item renderer took a box and did its own arithmetic inside
/// it, while every caller inset the box by its own breath, painted its own 5 px rarity strip beside it
/// and drew its selection ring around a rectangle the frame did not share. Rarity was said twice in a
/// square cell (the frame's tint and the strip), the selected look was GOLD — the Legendary colour —
/// and a glyph was the whole 256 px canvas stretched into a 15 % inset, so a thin charm on its chain
/// drew at a third of the room it had. ADR-006 asks for one geometry that both halves read; this is
/// it for items: a caller passes its CELL, asks <see cref="FrameRect"/> for the frame, and draws its
/// rings and hairlines on the same rectangles the renderer draws into.
/// </para>
/// <para>
/// <b>Rarity has one ramp</b> (<see cref="RarityInk"/>). The Forge, the Vault, the Gear screen and the
/// tooltip each carried their own five colours, and they disagreed on Epic (a plum too dark to read on
/// the ground) and on Common. Selection is NOT a rarity: it is Primary (bone), never gold.
/// </para>
/// <para>
/// Pure and static: nothing here touches a texture, so every rectangle is testable at every density
/// profile without a graphics device. The measured inputs (a frame's interior, a glyph's content
/// bounds) come from <see cref="ItemArtMetrics"/>.
/// </para>
/// </remarks>
public static class ItemCellLayout
{
    /// <summary>The frame's edge as a share of the cell's shorter side. What remains is the slot well's rim.</summary>
    public const float FrameShare = 0.94f;

    /// <summary>The inset per side a frame gets when its interior cannot be measured — the pre-2026-09-16 rule.</summary>
    public const float FallbackInset = 0.15f;

    /// <summary>
    /// A breath between the measured hole and the glyph, as a share of the frame's edge, so an
    /// anti-aliased ring edge never kisses the art.
    /// </summary>
    public const float InteriorBreath = 0.02f;

    /// <summary>A gem medallion's inset per side of the frame, at least — a coin sits deeper than a glyph.</summary>
    public const float GemInset = 0.18f;

    /// <summary>The CELL edge under which the Source gem overlay is skipped (it would be a smear).</summary>
    public const int SourceGemMinEdge = 80;

    /// <summary>The CELL edge under which the enchant accent overlay is skipped.</summary>
    public const int EnchantMinEdge = 88;

    /// <summary>A corner overlay's edge as a share of the frame's edge.</summary>
    public const float OverlayShare = 0.4f;

    /// <summary>
    /// The GROUND a glyph stands on: painted under a see-through frame (and on a legacy frame's hole).
    /// </summary>
    /// <remarks>
    /// A warm mid-brown (the house rule colour), not a darkening. Almost every wearable is painted as
    /// near-black iron on transparency, and on a dark cell a helm, a boot and a blade all read as an empty
    /// slot (playtest: "half of the item art was never actually visible"). The shelf that fixed that on
    /// the old opaque frames is this same ink, now drawn as the well itself.
    /// </remarks>
    public static readonly Color WellInk = UiInk.Rule * 0.9f;

    /// <summary>
    /// The square rarity frame inside a cell: <see cref="FrameShare"/> of the shorter side, centred.
    /// </summary>
    /// <remarks>A rectangular cell (a row's icon box is square already; a doll well is square) still
    /// gets a square frame, sitting in the middle of the longer axis.</remarks>
    public static Rectangle FrameRect(Rectangle cell)
    {
        var side = Math.Min(cell.Width, cell.Height);
        var edge = Math.Max(1, (int)MathF.Round(side * FrameShare));
        return new Rectangle(cell.X + (cell.Width - edge) / 2, cell.Y + (cell.Height - edge) / 2, edge, edge);
    }

    /// <summary>The room a glyph may fill inside a frame, from the frame's measured (or fallback) interior.</summary>
    public static Rectangle GlyphRect(Rectangle frame, FrameInterior interior)
    {
        var breath = interior.Transparent && interior != FrameInterior.None ? InteriorBreath : 0f;
        return Inset(frame, interior.Left + breath, interior.Top + breath, interior.Right + breath, interior.Bottom + breath);
    }

    /// <summary>
    /// A gem medallion's rectangle: the frame's interior, and never shallower than <see cref="GemInset"/>.
    /// </summary>
    public static Rectangle GemRect(Rectangle frame, FrameInterior interior)
        => Inset(frame, Math.Max(GemInset, interior.Left), Math.Max(GemInset, interior.Top),
                 Math.Max(GemInset, interior.Right), Math.Max(GemInset, interior.Bottom));

    /// <summary>A gem medallion's rectangle in a frame whose interior is unknown: <see cref="GemInset"/> on every side.</summary>
    public static Rectangle GemRect(Rectangle frame) => GemRect(frame, FrameInterior.Fallback);

    /// <summary>
    /// Where a glyph's CONTENT lands: scaled uniformly to fill <paramref name="into"/>, centred, its aspect
    /// kept — and never past the content's own pixels (scale 1), so a small file is drawn small rather
    /// than blurred up to fill the room.
    /// </summary>
    /// <param name="contentSize">The glyph's content bounds in texture pixels (see <see cref="ItemArtMetrics.ContentBounds"/>).</param>
    /// <param name="into">The room it may fill.</param>
    public static Rectangle Fit(Point contentSize, Rectangle into)
    {
        if (contentSize.X <= 0 || contentSize.Y <= 0 || into.Width <= 0 || into.Height <= 0) return into;
        var scale = MathF.Min(1f, MathF.Min(into.Width / (float)contentSize.X, into.Height / (float)contentSize.Y));
        var w = Math.Clamp((int)MathF.Round(contentSize.X * scale), 1, into.Width);
        var h = Math.Clamp((int)MathF.Round(contentSize.Y * scale), 1, into.Height);
        return new Rectangle(into.X + (into.Width - w) / 2, into.Y + (into.Height - h) / 2, w, h);
    }

    /// <summary>The Source gem overlay: the frame's top-left corner, <see cref="OverlayShare"/> of its edge.</summary>
    public static Rectangle SourceGemRect(Rectangle frame)
    {
        var s = OverlayEdge(frame);
        return new Rectangle(frame.X, frame.Y, s, s);
    }

    /// <summary>The enchant accent overlay: the frame's bottom-right corner, <see cref="OverlayShare"/> of its edge.</summary>
    public static Rectangle EnchantRect(Rectangle frame)
    {
        var s = OverlayEdge(frame);
        return new Rectangle(frame.Right - s, frame.Bottom - s, s, s);
    }

    /// <summary>The rarity pip drawn when an item has no art at all: the middle half of the frame.</summary>
    public static Rectangle PipRect(Rectangle frame)
        => new(frame.X + frame.Width / 4, frame.Y + frame.Height / 4, Math.Max(1, frame.Width / 2), Math.Max(1, frame.Height / 2));

    /// <summary>
    /// THE ONE RARITY RAMP. Common is Primary (bone: no claim), Uncommon is Good, Rare a clear blue,
    /// Epic a violet bright enough to read on the ground, Legendary the active gold.
    /// </summary>
    public static Color RarityInk(Rarity rarity) => rarity switch
    {
        Rarity.Uncommon => UiInk.Good,
        Rarity.Rare => new Color(0x4A, 0x90, 0xD9),
        Rarity.Epic => new Color(0xB0, 0x6A, 0xC8),
        Rarity.Legendary => UiInk.Accent,
        _ => UiInk.Primary,
    };

    private static int OverlayEdge(Rectangle frame) => Math.Max(1, (int)MathF.Round(frame.Width * OverlayShare));

    private static Rectangle Inset(Rectangle r, float left, float top, float right, float bottom)
    {
        var l = (int)MathF.Round(r.Width * left);
        var t = (int)MathF.Round(r.Height * top);
        var w = Math.Max(1, r.Width - l - (int)MathF.Round(r.Width * right));
        var h = Math.Max(1, r.Height - t - (int)MathF.Round(r.Height * bottom));
        return new Rectangle(r.X + l, r.Y + t, w, h);
    }
}
