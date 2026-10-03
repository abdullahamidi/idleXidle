using System;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// WHERE AN ENEMY'S DAMAGE NUMBER IS BORN (the review of Phase 0): on the body it describes, not on a lane hung from the
/// row's layout box. The lane used to start at <c>_rowTopY + NameplateOffset - CritPx - 6</c>, which on the crouching
/// whelps is about 250 px over the drawn figure: a quiet bleed number (half size, 70 %, bare Bone) on grey stone did not
/// read at all, and mixed combat stood as a column of numbers on the wall while the hunter's own numbers printed AT his
/// body. Both anchors now read the creature's DRAWN silhouette (the visual bounds the arena publishes per frame).
/// </summary>
/// <remarks>
/// A plain, skill or critical number starts at the HEAD: just over the creature's life pip, which sits on the drawn top,
/// and climbs the existing stack from there. A QUIET number (bleed, reflect, carry, DEADWEIGHT) starts INSIDE the body,
/// <see cref="QuietBodyShare"/> of its height down from its top, where the near-black figure is its contrast; it
/// keeps the one-pixel drop shadow every callout has (it still rises onto stone), and no outline.
/// </remarks>
public static class NumberLane
{
    /// <summary>How far down the drawn body a quiet number starts: this share of the body's height from its top.</summary>
    /// <remarks>0.4, not the review's ~0.3: a body's drawn bounds reach its tallest pose (spines, a tail), so 0.3 of
    /// them landed on the creature's back line in a crouch; 0.4 is in the mass.</remarks>
    public const float QuietBodyShare = 0.4f;

    /// <summary>
    /// The top of a QUIET number's glyphs on <paramref name="body"/>, raised by <paramref name="stack"/> lines of
    /// <paramref name="lineHeight"/> (never above the body's top).
    /// </summary>
    /// <param name="body">The struck creature's drawn silhouette this frame.</param>
    /// <param name="stack">Which line of the quiet stack this number takes (0 is the lowest).</param>
    /// <param name="lineHeight">The gap between two stacked quiet numbers.</param>
    public static int QuietY(Rectangle body, int stack, int lineHeight)
        => Math.Max(body.Y, body.Y + (int)MathF.Round(body.Height * QuietBodyShare) - Math.Max(0, stack) * Math.Max(0, lineHeight));

    /// <summary>
    /// The top of a plain / skill / critical number's glyphs over <paramref name="body"/>: <paramref name="clearance"/>
    /// over the drawn top (the life pip lives there) and <paramref name="tallestPx"/> more so the tallest glyph clears it.
    /// The stack climbs from here.
    /// </summary>
    /// <param name="body">The struck creature's drawn silhouette this frame.</param>
    /// <param name="clearance">The room the life pip takes over the drawn top.</param>
    /// <param name="tallestPx">The tallest glyph that stacks here (a critical's).</param>
    public static int HeadY(Rectangle body, int clearance, int tallestPx) => body.Y - clearance - tallestPx;
}
