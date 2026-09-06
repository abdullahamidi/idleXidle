using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game;

/// <summary>
/// THE ENEMY INSPECTOR'S PLACEMENT REQUEST, as a pure function of the frame's rectangles — so the
/// screen and its tests make the same request. The side AWAY FROM THE HUNTER first, then above, then
/// below, then toward the hunter; the hunter's keep-out and the right column's IDLE panel are hard;
/// the viewport (the arena between the header stack and the skill dock) bounds it.
/// </summary>
/// <remarks>
/// <para>
/// 2026-09-06: at 125 % a creature near the right edge sent the plate ABOVE, over the IDLE panel's
/// OPEN VAULT door — the request protected the hunter and nothing else. The panel is the whole
/// surface the player reads and clicks, so the whole panel is avoided, from the rectangle the column
/// lays out (never a second copy of its coordinates).
/// </para>
/// <para>
/// Later the same day: the hunter was protected as his LAYOUT BOX — <c>HuntScreen.ChampBox</c>, 400 x
/// 430 — rather than as the figure. The box is filled only where the hunter's clips reach (a
/// narrow hunter's idle is 177 frame-px wide; his swing 400), so the plate refused room the art
/// never used, and the two rectangles disagreed with the one the pointer hit-tests. The request now
/// takes the hunter's published ENVELOPE — the one geometry every interaction reads — and adds the
/// single clearance it owns (<see cref="HunterClearance"/>): enough breath that a plate never touches
/// a sleeve, and nothing more. The margin is defined here, once, so no caller can grow it back into
/// a box.
/// </para>
/// </remarks>
public static class EnemyInspectorPlacement
{
    /// <summary>
    /// How much room the plate leaves around the hunter's envelope — a readability breath, sized like
    /// any other gap on the page. Small on purpose: the envelope already holds every clip's reach.
    /// </summary>
    public static int HunterClearance => UiMetrics.Space(8);

    /// <summary>The hunter's keep-out: his envelope with the clearance on every side.</summary>
    public static Rectangle HunterKeepOut(Rectangle hunterEnvelope)
    {
        var r = hunterEnvelope;
        r.Inflate(HunterClearance, HunterClearance);
        return r;
    }

    /// <summary>What the inspector must not cover: the hunter's keep-out, and the IDLE panel with its doors.</summary>
    /// <param name="hunterEnvelope">The hunter's published envelope (<see cref="ActorPresentation.Envelope"/>) — never his layout box.</param>
    /// <param name="idlePanel">The right column's IDLE panel as laid out this frame; empty when there is none.</param>
    public static IReadOnlyList<Rectangle> Avoid(Rectangle hunterEnvelope, Rectangle idlePanel)
        => idlePanel.IsEmpty ? new[] { HunterKeepOut(hunterEnvelope) } : new[] { HunterKeepOut(hunterEnvelope), idlePanel };

    /// <summary>The sides in the order the inspector tries them: away from the hunter first, toward it last.</summary>
    public static IReadOnlyList<PopoverSide> Order(Rectangle anchor, Rectangle hunterEnvelope)
        => anchor.Center.X >= hunterEnvelope.Center.X
            ? new[] { PopoverSide.Right, PopoverSide.Above, PopoverSide.Below, PopoverSide.Left }
            : new[] { PopoverSide.Left, PopoverSide.Above, PopoverSide.Below, PopoverSide.Right };

    /// <summary>Where the plate stands for a creature whose envelope is <paramref name="anchor"/>.</summary>
    public static Rectangle Place(Rectangle anchor, Point size, Rectangle viewport, Rectangle hunterEnvelope, Rectangle idlePanel)
        => PopoverPlacement.Place(anchor, size, viewport, Avoid(hunterEnvelope, idlePanel), Order(anchor, hunterEnvelope));
}
