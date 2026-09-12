using System;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game;

/// <summary>
/// ONE RAIL TILE'S CONTENT GRID: where its label sits, where its icon sits, and the band left over
/// above them — derived from the label rung, so the whole tile follows the density profile from one
/// number.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a type and not four lines inside the draw.</b> It WAS four lines inside
/// <c>Game1.DrawHexNav</c>, and the chain drawn on a locked tile had no way to read them:
/// <see cref="NavChain"/> is handed the tile rectangle and nothing else, so it hung its padlock at a
/// fixed fraction of the TILE while the label moved with the profile. The rung grows at the text rate
/// (24 / 30 / 36) and the padlock at the spacing rate (22 / 25 / 28), so the two closed on each other
/// one profile at a time: clear at 100 %, touching at 125 %, and at 150 % the padlock sat on the first
/// letter of a short word like MAP. Two things laying out the same tile from two different rules is
/// what produced that, and one shared grid is the fix — not a nudge per label.
/// </para>
/// <para>
/// <b>The tile itself does not scale.</b> The rail is 180 wide and a tile is 1080/11 = 98 tall at every
/// profile (see <c>Game1.NavRailWidth</c> / <c>NavTileHeight</c>), which is the whole reason the grid
/// is tight: at 150 % a 36 px label plus its foot takes 46 of those 98 px and everything else shares
/// what is left.
/// </para>
/// </remarks>
/// <param name="Tile">The rail tile, canvas space.</param>
/// <param name="LabelHeight">The label's rung at this profile (<see cref="UiTypography.NavigationLabel"/>).</param>
/// <param name="LabelTop">The top of the label's line box — what <c>TextCenterBig</c> is given as its y.</param>
/// <param name="Icon">The glyph's square, centred on the tile and sitting on the label.</param>
public readonly record struct NavTileGrid(Rectangle Tile, int LabelHeight, int LabelTop, Rectangle Icon)
{
    /// <summary>How far the label's bottom clears the tile's foot. Unscaled — the HUNT tile's health bar lives in this band.</summary>
    public const int LabelFoot = 10;

    /// <summary>The breath between the icon and the label.</summary>
    public const int IconGap = 2;

    /// <summary>The icon's edge: never past 48 (its art), never under 30 (a glyph stops reading).</summary>
    public const int IconMax = 48, IconMin = 30;

    /// <summary>The room a label leaves at each side of the tile — the rail's 3 px seam and its mirror.</summary>
    public const int LabelInset = 3;

    /// <summary>
    /// THE CLEAR BAND: the tile above the label's line box, where the icon sits and where anything else
    /// drawn on the tile may go without printing on a word.
    /// </summary>
    /// <remarks>
    /// Its foot is the icon's foot, which is the point: a padlock resting here is exactly as clear of
    /// the label as the glyph beside it already is, at every profile, for free. Never empty — the icon's
    /// floor (<see cref="IconMin"/>) guarantees the band is at least 30 px tall.
    /// </remarks>
    public Rectangle ClearBand
        => new(Tile.Left, Tile.Top, Tile.Width, Math.Max(0, LabelTop - IconGap - Tile.Top));

    /// <summary>
    /// Lay a rail tile out for the current density profile.
    /// </summary>
    /// <remarks>
    /// THE LAYOUT IS DERIVED FROM THE LABEL RUNG, not written. The label sits <see cref="LabelFoot"/>
    /// above the tile's foot and the icon takes the room above it — 38 px at 100 % on the 98 px tile —
    /// and the icon gives up its TOP pad before its floor, so at 150 % a 36 px label gets a 42 px icon
    /// rather than an icon it prints through. (Before this rule the icon was sized from the tile alone
    /// and the label drawn at Bottom-34: at 150 % the label ran two pixels past the tile's foot into
    /// the next tile's divider, and the rail was the one place a bigger profile made the glyphs
    /// SMALLER — chrome-06.)
    /// </remarks>
    public static NavTileGrid Of(Rectangle tile)
    {
        var labelH = UiTypography.NavigationLabel;
        var labelTop = tile.Bottom - LabelFoot - labelH;
        var iconPx = Math.Clamp(
            Math.Min(UiMetrics.Control(38), labelTop - IconGap - tile.Y - UiMetrics.Space(6)),
            IconMin, IconMax);
        var iconY = labelTop - IconGap - iconPx;
        return new NavTileGrid(tile, labelH, labelTop,
                               new Rectangle(tile.Center.X - iconPx / 2, iconY, iconPx, iconPx));
    }
}
