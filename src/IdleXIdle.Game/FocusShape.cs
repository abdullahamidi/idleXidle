using System.Collections.Generic;
using IdleXIdle.Core.Progression;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game;

/// <summary>What a focus light is shaped like.</summary>
public enum FocusKind
{
    /// <summary>A rounded rectangle with a soft edge — a control, a panel, a rail tile.</summary>
    RoundedRect,
    /// <summary>A soft ellipse inscribed in the rectangle.</summary>
    Ellipse,
    /// <summary>A figure's own outline: its white mask, drawn exactly where the frame was.</summary>
    Silhouette,
}

/// <summary>
/// One lit shape in the tutorial's focus light — the VISUAL only. The rectangle a step lights for the
/// click and for the card (<c>Game1.TourSpotlights</c>, <c>ClickableOf</c>, <c>TourCardRect</c>) is
/// not here and is not changed by anything here: this is what the scrim is cut around, and the hole
/// may follow a figure's outline where the hit-test still follows its rectangle.
/// </summary>
/// <param name="Kind">Which primitive the renderer paints.</param>
/// <param name="Rect">The shape's rectangle in canvas pixels — a silhouette's is the frame it was drawn into.</param>
/// <param name="Mask">A silhouette's white mask (<see cref="AssetLibrary.WhiteMask"/>); null for the other kinds.</param>
/// <param name="Src">The frame inside <see cref="Mask"/>, for a silhouette.</param>
/// <param name="Effects">How the frame was mirrored when it was drawn, for a silhouette.</param>
public readonly record struct FocusShape(FocusKind Kind, Rectangle Rect, Texture2D? Mask = null,
                                         Rectangle Src = default, SpriteEffects Effects = SpriteEffects.None)
{
    /// <summary>A soft rounded rectangle over <paramref name="rect"/>.</summary>
    public static FocusShape RoundedRect(Rectangle rect) => new(FocusKind.RoundedRect, rect);

    /// <summary>A soft ellipse inscribed in <paramref name="rect"/>.</summary>
    public static FocusShape Ellipse(Rectangle rect) => new(FocusKind.Ellipse, rect);

    /// <summary>A figure's silhouette: the mask of the strip a frame came from, at that frame, where it landed.</summary>
    public static FocusShape Actor(SpriteFrame frame, Texture2D mask)
        => new(FocusKind.Silhouette, frame.Dest, mask, frame.Src, frame.Effects);

    /// <summary>
    /// Turn a step's lit rectangles into shapes, asking the arena for its figures where the target is one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rectangles are the production answer and stay the production answer; this only decides
    /// what the light looks like inside them. On the HUNT, <see cref="TourTarget.Champion"/> becomes
    /// the hunter's silhouette (the frame the arena drew this frame, through its own mask), and
    /// <see cref="TourTarget.Enemies"/> one silhouette per laid-out creature — the boss is slot 0 —
    /// with the header's hole still a rounded rectangle. A figure with no frame recorded this frame
    /// falls back to the body the arena published, and when no figure recorded anything at all the
    /// light falls back to the first hole, so a fixture with no strips still lights the place the
    /// card points at. A creature that has faded out (a corpse past its fade) records nothing and is
    /// skipped while others still stand: a glowing rectangle over an empty floor is not a light.
    /// </para>
    /// <para>
    /// Every other target is one rounded rectangle per hole, and the whole-canvas sentinel — the
    /// resolver's "no screen knows this target" — yields nothing, exactly as it lit nothing before.
    /// </para>
    /// </remarks>
    /// <param name="screen">The screen the holes were resolved on.</param>
    /// <param name="target">What the step is about.</param>
    /// <param name="holes">The lit rectangles, in canvas pixels, as the step resolved them.</param>
    /// <param name="actors">The arena's figures this frame; null off the HUNT.</param>
    /// <param name="into">Cleared, then filled. Preallocated by the caller.</param>
    public static void Resolve(Activity screen, TourTarget target, IReadOnlyList<Rectangle> holes,
                               IFocusActors? actors, List<FocusShape> into)
    {
        into.Clear();
        if (holes.Count == 0) return;
        for (var i = 0; i < holes.Count; i++)
            if (IsSentinel(holes[i])) return;

        var first = 0;
        if (screen == Activity.Hunt && actors is not null)
        {
            if (target == TourTarget.Champion)
            {
                if (!Figure(actors, VfxSubject.Champion, into) && !Body(actors, VfxSubject.Champion, into))
                    into.Add(RoundedRect(holes[0]));
                first = 1;
            }
            else if (target == TourTarget.Enemies)
            {
                var lit = 0;
                var count = actors.LaidOutCreatureCount;
                for (var slot = 0; slot < count; slot++)
                    if (Figure(actors, VfxSubject.Creature(slot), into)) lit++;
                if (lit == 0)
                    for (var slot = 0; slot < count; slot++)
                        if (Body(actors, VfxSubject.Creature(slot), into)) lit++;
                if (lit == 0) into.Add(RoundedRect(holes[0]));
                first = 1;
            }
        }
        for (var i = first; i < holes.Count; i++) into.Add(RoundedRect(holes[i]));
    }

    /// <summary>The resolver's "no screen claims this target" answer: the whole page.</summary>
    public static bool IsSentinel(Rectangle hole) => hole.Width >= 1900 && hole.Height >= 1060;

    private static bool Figure(IFocusActors actors, VfxSubject subject, List<FocusShape> into)
    {
        if (!actors.TryDrawnFrame(subject, out var frame) || actors.MaskOf(frame.Texture) is not { } mask) return false;
        into.Add(Actor(frame, mask));
        return true;
    }

    private static bool Body(IFocusActors actors, VfxSubject subject, List<FocusShape> into)
    {
        if (!actors.TryBody(subject, out var body) || body.Width <= 0 || body.Height <= 0) return false;
        into.Add(RoundedRect(body));
        return true;
    }
}

/// <summary>
/// What the arena tells the focus light about its figures this frame: the frame each one was drawn
/// from, the body it published, how many creatures stand, and the mask a frame's strip has.
/// </summary>
/// <remarks>
/// The HUNT screen answers this from what it just drew (<c>HuntScreen.ActorSprite</c> records the
/// frame; <c>LayoutActors</c> clears the record first), so the silhouette is the figure of THIS frame,
/// never last frame's pose. It is an interface so the resolver can be driven without a device.
/// </remarks>
public interface IFocusActors
{
    /// <summary>The frame this figure was drawn from this frame, if it drew one.</summary>
    bool TryDrawnFrame(VfxSubject subject, out SpriteFrame frame);

    /// <summary>The visible body the arena published for this figure this frame.</summary>
    bool TryBody(VfxSubject subject, out Rectangle body);

    /// <summary>How many creature slots were laid out this frame (a boss is one, in slot 0).</summary>
    int LaidOutCreatureCount { get; }

    /// <summary>The white silhouette of the strip a frame's texture belongs to, or null when it has none.</summary>
    Texture2D? MaskOf(Texture2D texture);
}
