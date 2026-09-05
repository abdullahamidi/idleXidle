using Microsoft.Xna.Framework;

namespace IdleXIdle.Game;

/// <summary>
/// ONE resolved geometry per creature per frame: the box its strip is drawn into, and the rectangle the
/// figure visibly occupies inside it — the same visible rectangle the effects pass aims at
/// (<see cref="VfxFigure.VisualRect"/>, published to the bounds registry). Every interaction reads from
/// here: the hover, the inspector's anchor. Nothing rebuilds sprite geometry on its own.
/// </summary>
/// <remarks>
/// 2026-09-06: the hover tested the DRAW box, which is the visible figure plus its strip's transparent
/// margin — for some silhouettes a third of the box's width on each side — so a pointer clearly beside
/// a creature "hovered" it, and the inspector hung off that margin rather than off the body.
/// </remarks>
public readonly record struct CreaturePresentation(Rectangle DrawRect, Rectangle Body)
{
    /// <summary>What takes the pointer: the visible body, never the transparent margin around it.</summary>
    public Rectangle HoverRect => Body;

    /// <summary>What the inspector hangs off: the visible body.</summary>
    public Rectangle InspectorAnchor => Body;

    /// <summary>Is the pointer on the drawn creature?</summary>
    public bool Hovers(Point p) => HoverRect.Contains(p);
}
