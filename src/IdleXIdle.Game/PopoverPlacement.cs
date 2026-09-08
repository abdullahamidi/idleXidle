using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game;

/// <summary>Where a popover would rather stand, relative to what it hangs off.</summary>
public enum PopoverSide { Right, Left, Below, Above }

/// <summary>
/// THE ONE PLACEMENT RULE for everything that floats beside an anchor: a tooltip, the enemy inspector,
/// a hover card. Pure rectangle arithmetic, no rendering — so it is tested without a device.
/// </summary>
/// <remarks>
/// <para>
/// Three surfaces each hand-rolled their own rule before 2026-09-06 (UiKit.HoverTip flipped left at the
/// page's edge and never clamped; ItemTooltip flipped horizontally and clamped vertically; the enemy
/// inspector tried right, then above, then left) and each had the same bug in a different place: the
/// trader's tooltip sat on the BUY button it hung under, the inspector sat on the hunter at 150 %.
/// One rule, one set of tests: try the preferred sides in order, take the first that stands beside
/// the anchor inside the viewport and touches no avoided rectangle; failing that, the least bad by a
/// score — an avoided rectangle's covered area a millionfold, the anchor's own once — ties to the
/// earlier, so the answer is deterministic.
/// </para>
/// <para>
/// THE CANDIDATES, in the order they may win: each side's own place and then that side's SLIDES along
/// the anchor's edge (up or left, then down or right, to the far side of what it covered, while it
/// still shares half its extent with the anchor) — side after side in the caller's order. The slide
/// is the trader's: its tall card beside a tall offer slides up clear of the BUY under the next offer.
/// </para>
/// <para>
/// THE VOCABULARY IS WHAT THE GAME USES (2026-09-06). Soft, decorative avoid regions, an OVER-the-anchor
/// stance and PUSHES away from the anchor past what blocked a side were added for the Warren's facility
/// tips and removed with them: the Warren says its hover in a rail under the grid now, and none of the
/// four requests left can reach a push — the item card avoids nothing; the pointer tip's one blocker is
/// the action band at the page's foot, with ABOVE clean before it; the enemy inspector's one blocker is
/// the hunter, with only the arena's edge behind it; the trader's slide always covers less than a push
/// would. Reachable in theory is not a consumer.
/// </para>
/// <para>
/// The rectangle returned is the rectangle to DRAW and the rectangle to HIT-TEST — a caller never
/// nudges it afterwards, or the two drift apart.
/// </para>
/// </remarks>
public static class PopoverPlacement
{
    /// <summary>The breath between an anchor and its popover, and between a popover and the viewport's edge.</summary>
    public static int Gap => UiMetrics.Space(12);

    private static readonly PopoverSide[] DefaultOrder = { PopoverSide.Right, PopoverSide.Left, PopoverSide.Above, PopoverSide.Below };

    /// <summary>
    /// Place a popover of <paramref name="size"/> beside <paramref name="anchor"/>, inside
    /// <paramref name="viewport"/>, clear of <paramref name="avoid"/> where a side allows it.
    /// </summary>
    /// <param name="anchor">What the popover hangs off — the control, the card, the creature.</param>
    /// <param name="size">The popover's own width and height, already measured.</param>
    /// <param name="viewport">The area it must stay inside; the page, or a panel's interior.</param>
    /// <param name="avoid">Rectangles it must not cover if any placement avoids them — a BUY button, the hunter.</param>
    /// <param name="preferred">The sides to try, in order. Null for right, left, above, below.</param>
    /// <param name="gap">The breath between anchor and popover; null for <see cref="Gap"/>.</param>
    public static Rectangle Place(Rectangle anchor, Point size, Rectangle viewport,
                                  IReadOnlyList<Rectangle>? avoid = null, IReadOnlyList<PopoverSide>? preferred = null,
                                  int? gap = null)
    {
        var g = gap ?? Gap;
        var order = preferred is { Count: > 0 } ? preferred : DefaultOrder;
        avoid ??= Array.Empty<Rectangle>();

        // A popover wider or taller than the viewport can only be clamped; do that and say so by fitting.
        var w = Math.Min(size.X, Math.Max(1, viewport.Width - 2 * g));
        var h = Math.Min(size.Y, Math.Max(1, viewport.Height - 2 * g));
        var anchorOnly = new[] { anchor };

        Rectangle best = default;
        var bestScore = long.MaxValue;
        foreach (var (side, c) in Candidates(order, anchor, w, h, g, viewport, avoid))
        {
            var beside = !c.Intersects(anchor) || Fits(side, c, anchor);
            var hard = Overlap(c, avoid);
            if (beside && hard == 0) return c;
            var score = hard * 1_000_000L + Overlap(c, anchorOnly);
            if (score < bestScore)
            {
                best = c;
                bestScore = score;
            }
        }
        return best;
    }

    /// <summary>Every candidate, in the order it is entitled to win: each side's place, then that side's slides.</summary>
    private static IEnumerable<(PopoverSide Side, Rectangle Rect)> Candidates(IReadOnlyList<PopoverSide> order, Rectangle anchor,
                                                                              int w, int h, int g, Rectangle viewport,
                                                                              IReadOnlyList<Rectangle> avoid)
    {
        for (var i = 0; i < order.Count; i++)
        {
            var c = Clamp(At(order[i], anchor, w, h, g), viewport, g);
            yield return (order[i], c);
            foreach (var s in Slides(order[i], c, anchor, viewport, avoid, g)) yield return (order[i], s);
        }
    }

    /// <summary>The rectangle a side would put the popover in, before any clamping.</summary>
    private static Rectangle At(PopoverSide side, Rectangle anchor, int w, int h, int g) => side switch
    {
        PopoverSide.Right => new Rectangle(anchor.Right + g, anchor.Y, w, h),
        PopoverSide.Left => new Rectangle(anchor.X - g - w, anchor.Y, w, h),
        PopoverSide.Above => new Rectangle(anchor.Center.X - w / 2, anchor.Y - g - h, w, h),
        _ => new Rectangle(anchor.Center.X - w / 2, anchor.Bottom + g, w, h),
    };

    /// <summary>
    /// A candidate that covers an avoided rectangle, slid along the anchor's edge to the far side of
    /// what it covered — up/left first, then down/right, rectangles in the caller's order — keeping
    /// only the slides that still share half their extent with the anchor.
    /// </summary>
    private static IEnumerable<Rectangle> Slides(PopoverSide side, Rectangle c, Rectangle anchor, Rectangle viewport,
                                                 IReadOnlyList<Rectangle> avoid, int g)
    {
        var alongY = side is PopoverSide.Right or PopoverSide.Left;
        foreach (var a in avoid)
        {
            if (!c.Intersects(a)) continue;
            var moves = alongY
                ? new[] { new Rectangle(c.X, a.Y - g - c.Height, c.Width, c.Height), new Rectangle(c.X, a.Bottom + g, c.Width, c.Height) }
                : new[] { new Rectangle(a.X - g - c.Width, c.Y, c.Width, c.Height), new Rectangle(a.Right + g, c.Y, c.Width, c.Height) };
            foreach (var moved in moves)
            {
                var r = Clamp(moved, viewport, g);
                if (SharesHalf(alongY, r, anchor)) yield return r;
            }
        }
    }

    /// <summary>Does a moved candidate still share half its extent (the smaller of the two) with the anchor along its edge?</summary>
    private static bool SharesHalf(bool alongY, Rectangle r, Rectangle anchor)
    {
        var shared = alongY
            ? Math.Min(r.Bottom, anchor.Bottom) - Math.Max(r.Y, anchor.Y)
            : Math.Min(r.Right, anchor.Right) - Math.Max(r.X, anchor.X);
        var need = (alongY ? Math.Min(r.Height, anchor.Height) : Math.Min(r.Width, anchor.Width)) / 2;
        return shared >= need;
    }

    /// <summary>Does a clamped candidate still stand on its side of the anchor, rather than over it?</summary>
    private static bool Fits(PopoverSide side, Rectangle r, Rectangle anchor) => side switch
    {
        PopoverSide.Right => r.X >= anchor.Right,
        PopoverSide.Left => r.Right <= anchor.X,
        PopoverSide.Above => r.Bottom <= anchor.Y,
        _ => r.Y >= anchor.Bottom,
    };

    /// <summary>Slide a rectangle inside the viewport, keeping the margin on every edge it touches.</summary>
    public static Rectangle Clamp(Rectangle r, Rectangle viewport, int margin)
    {
        var minX = viewport.X + margin;
        var maxX = viewport.Right - margin - r.Width;
        var minY = viewport.Y + margin;
        var maxY = viewport.Bottom - margin - r.Height;
        var x = maxX >= minX ? Math.Clamp(r.X, minX, maxX) : viewport.X + (viewport.Width - r.Width) / 2;
        var y = maxY >= minY ? Math.Clamp(r.Y, minY, maxY) : viewport.Y + (viewport.Height - r.Height) / 2;
        return new Rectangle(x, y, r.Width, r.Height);
    }

    /// <summary>The area a candidate covers of the given rectangles, summed.</summary>
    private static long Overlap(Rectangle r, IReadOnlyList<Rectangle> rects)
    {
        long sum = 0;
        foreach (var a in rects)
        {
            var i = Rectangle.Intersect(r, a);
            if (i.Width > 0 && i.Height > 0) sum += (long)i.Width * i.Height;
        }
        return sum;
    }
}
