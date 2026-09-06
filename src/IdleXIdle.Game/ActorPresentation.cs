using Microsoft.Xna.Framework;

namespace IdleXIdle.Game;

/// <summary>
/// ONE resolved geometry per actor per frame — the hunter or a creature: the box its strip is drawn
/// into, the rectangle the figure visibly occupies inside it (the same one the effects pass measures,
/// <see cref="Vfx.VisualBounds.Rect"/>), and the stable ENVELOPE every one of its combat clips lands
/// inside (<see cref="Vfx.VisualBounds.Envelope"/>). Every interaction reads from here: the hover, the
/// inspector's anchor, the keep-out around the hunter. Nothing rebuilds sprite geometry on its own.
/// </summary>
/// <remarks>
/// <para>
/// 2026-09-06: the hover tested the DRAW box, which is the visible figure plus its strip's transparent
/// margin — for some silhouettes a third of the box's width on each side — so a pointer clearly beside
/// a creature "hovered" it, and the inspector hung off that margin rather than off the body.
/// </para>
/// <para>
/// Later the same day: the body alone was too narrow. It is read off the idle strip (stable, and the
/// number every effect is sized from), but a creature's attack reaches past it — a spear, a claw, a
/// swing — so for the frames of a bite the drawn figure visibly escaped the rectangle the pointer
/// hit-tested. The envelope is the union of the idle and the attack (and, for the hunter, every
/// action clip), measured from what the actor CAN play rather than what it is playing, so it holds
/// the attack's full extension without moving when the clip changes. On the hunter's side the
/// inspector used to keep clear of the whole 400 x 430 layout box; now it keeps clear of this.
/// </para>
/// </remarks>
/// <param name="DrawRect">The layout box the strip is drawn into — the frame, not the figure.</param>
/// <param name="Body">The visible figure as the idle reference lands it — what the effects measure.</param>
/// <param name="Envelope">The stable rectangle every combat clip stays inside — what is pointed at.</param>
public readonly record struct ActorPresentation(Rectangle DrawRect, Rectangle Body, Rectangle Envelope)
{
    /// <summary>What takes the pointer: the envelope — the body and everything a clip reaches beyond it — never the transparent margin.</summary>
    public Rectangle HoverRect => Envelope;

    /// <summary>What the inspector hangs off: the envelope, so a plate beside a creature is never crossed by its swing.</summary>
    public Rectangle InspectorAnchor => Envelope;

    /// <summary>Is the pointer on the drawn actor?</summary>
    public bool Hovers(Point p) => HoverRect.Contains(p);
}
