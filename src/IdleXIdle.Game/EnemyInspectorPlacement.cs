using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game;

/// <summary>
/// THE ENEMY INSPECTOR'S PLACEMENT REQUEST, as a pure function of the frame's rectangles — so the
/// screen and its tests make the same request. The side AWAY FROM THE HUNTER first, then above, then
/// below, then toward the hunter; the hunter's rectangle and the right column's IDLE panel are hard;
/// the viewport (the arena between the header stack and the skill dock) bounds it.
/// </summary>
/// <remarks>
/// 2026-09-06: at 125 % a creature near the right edge sent the plate ABOVE, over the IDLE panel's
/// OPEN VAULT door — the request protected the hunter and nothing else. The panel is the whole
/// surface the player reads and clicks, so the whole panel is avoided, from the rectangle the column
/// lays out (never a second copy of its coordinates).
/// </remarks>
public static class EnemyInspectorPlacement
{
    /// <summary>What the inspector must not cover: the hunter, and the IDLE panel with its doors.</summary>
    public static IReadOnlyList<Rectangle> Avoid(Rectangle champ, Rectangle idlePanel)
        => idlePanel.IsEmpty ? new[] { champ } : new[] { champ, idlePanel };

    /// <summary>The sides in the order the inspector tries them: away from the hunter first, toward it last.</summary>
    public static IReadOnlyList<PopoverSide> Order(Rectangle anchor, Rectangle champ)
        => anchor.Center.X >= champ.Center.X
            ? new[] { PopoverSide.Right, PopoverSide.Above, PopoverSide.Below, PopoverSide.Left }
            : new[] { PopoverSide.Left, PopoverSide.Above, PopoverSide.Below, PopoverSide.Right };

    /// <summary>Where the plate stands for a creature whose visible body is <paramref name="anchor"/>.</summary>
    public static Rectangle Place(Rectangle anchor, Point size, Rectangle viewport, Rectangle champ, Rectangle idlePanel)
        => PopoverPlacement.Place(anchor, size, viewport, Avoid(champ, idlePanel), Order(anchor, champ));
}
