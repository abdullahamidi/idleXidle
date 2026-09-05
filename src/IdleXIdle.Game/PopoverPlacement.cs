using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game;

/// <summary>
/// Where a popover would rather stand, relative to what it hangs off. OVER is the last resort a caller
/// may name: on the anchor itself, when the anchor is the one surface it is allowed to cover.
/// </summary>
public enum PopoverSide { Right, Left, Below, Above, Over }

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
/// One rule, one set of tests.
/// </para>
/// <para>
/// AVOIDED RECTANGLES HAVE TWO PRIORITIES. HARD ones are information — a BUY, the hunter, a summary
/// strip, a neighbouring card, a scroll hint; SOFT ones are decorative room — a panel's own padding.
/// A candidate is CLEAN only when it stands beside the anchor over neither. When nothing is clean, a
/// hard rectangle's covered area counts a millionfold, the anchor's own a thousandfold and a soft
/// one's once, so the least bad candidate covers decoration before the anchor, and the anchor before
/// information. The viewport's exterior is never a candidate: everything is clamped inside it first.
/// </para>
/// <para>
/// THE CANDIDATES, in the order they may win: each side's own place and then that side's SLIDES along
/// the anchor's edge (up or left, then down or right, to the far side of what it covered — hard or
/// soft — while it still shares half its extent with the anchor) — side after side in the caller's
/// order; and only after every side has been tried where it belongs, each side's PUSHES away from
/// the anchor, past every hard rectangle it covered, as far as the viewport allows. A tip under a scrolling grid steps past the
/// grid's caption band into the free room below; the trader's tall card beside a tall offer slides up
/// clear of the BUY under the next offer. The first clean candidate wins; otherwise the least bad,
/// ties to the earlier — so the answer is deterministic.
/// </para>
/// <para>
/// OVER (2026-09-06) is never clean and never beside: a caller lists it last when the anchor is the one
/// surface the popover may cover — a card's own hover tip in a grid with no room around it. It stands
/// at the anchor's top, or its foot, and slides off any hard rectangle inside the anchor (the card's
/// figures) like a side slides along an edge. A side the clamp pushed onto the anchor is not a place
/// but an accident, and is scored as if it covered the whole anchor, so OVER wins over it.
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

    /// <summary>How many rectangles a push may step past before it gives up on that side.</summary>
    private const int MaxPushes = 4;

    /// <summary>
    /// Place a popover of <paramref name="size"/> beside <paramref name="anchor"/>, inside
    /// <paramref name="viewport"/>, clear of <paramref name="avoid"/> where a side allows it.
    /// </summary>
    /// <param name="anchor">What the popover hangs off — the control, the card, the creature.</param>
    /// <param name="size">The popover's own width and height, already measured.</param>
    /// <param name="viewport">The area it must stay inside; the page, or a panel's interior.</param>
    /// <param name="avoid">HARD: rectangles it must not cover if any placement avoids them — a BUY, the hunter, a strip.</param>
    /// <param name="preferred">The sides to try, in order. Null for right, left, above, below.</param>
    /// <param name="gap">The breath between anchor and popover; null for <see cref="Gap"/>.</param>
    /// <param name="soft">SOFT: decorative room it would rather not cover — a panel's padding. Null for none.</param>
    public static Rectangle Place(Rectangle anchor, Point size, Rectangle viewport,
                                  IReadOnlyList<Rectangle>? avoid = null, IReadOnlyList<PopoverSide>? preferred = null,
                                  int? gap = null, IReadOnlyList<Rectangle>? soft = null)
    {
        var g = gap ?? Gap;
        var order = preferred is { Count: > 0 } ? preferred : DefaultOrder;
        avoid ??= Array.Empty<Rectangle>();
        soft ??= Array.Empty<Rectangle>();

        // A popover wider or taller than the viewport can only be clamped; do that and say so by fitting.
        var w = Math.Min(size.X, Math.Max(1, viewport.Width - 2 * g));
        var h = Math.Min(size.Y, Math.Max(1, viewport.Height - 2 * g));
        var anchorOnly = new[] { anchor };

        Rectangle best = default;
        var bestScore = long.MaxValue;
        foreach (var (side, c) in Candidates(order, anchor, w, h, g, viewport, avoid, soft))
        {
            var beside = !c.Intersects(anchor) || Fits(side, c, anchor);
            var hard = Overlap(c, avoid);
            var decor = Overlap(c, soft);
            if (beside && hard == 0 && decor == 0) return c;
            var over = beside || side == PopoverSide.Over ? Overlap(c, anchorOnly) : (long)anchor.Width * anchor.Height;
            var score = hard * 1_000_000L + over * 1_000L + decor;
            if (score < bestScore)
            {
                best = c;
                bestScore = score;
            }
        }
        return best;
    }

    /// <summary>Every candidate, in the order it is entitled to win: each side's place and slides; then each side's pushes.</summary>
    private static IEnumerable<(PopoverSide Side, Rectangle Rect)> Candidates(IReadOnlyList<PopoverSide> order, Rectangle anchor,
                                                                              int w, int h, int g, Rectangle viewport,
                                                                              IReadOnlyList<Rectangle> avoid, IReadOnlyList<Rectangle> soft)
    {
        // A slide clears anything covered, hard or soft — a soft rectangle costs less to cover, but a
        // stance that covers nothing costs less still. A push steps past hard rectangles only.
        var blockers = soft.Count == 0 ? avoid : avoid.Concat(soft).ToList();
        var primaries = new Rectangle[order.Count];
        for (var i = 0; i < order.Count; i++)
        {
            var c = Clamp(At(order[i], anchor, w, h, g), viewport, g);
            primaries[i] = c;
            yield return (order[i], c);
            foreach (var s in Slides(order[i], c, anchor, viewport, blockers, g)) yield return (order[i], s);
            if (order[i] != PopoverSide.Over) continue;
            // OVER has a second stance: at the anchor's foot, with its own slides.
            var foot = Clamp(new Rectangle(anchor.Center.X - w / 2, anchor.Bottom - h, w, h), viewport, g);
            yield return (order[i], foot);
            foreach (var s in Slides(order[i], foot, anchor, viewport, blockers, g)) yield return (order[i], s);
        }
        for (var i = 0; i < order.Count; i++)
            foreach (var p in Pushes(order[i], primaries[i], viewport, avoid, g)) yield return (order[i], p);
    }

    /// <summary>The rectangle a side would put the popover in, before any clamping.</summary>
    private static Rectangle At(PopoverSide side, Rectangle anchor, int w, int h, int g) => side switch
    {
        PopoverSide.Right => new Rectangle(anchor.Right + g, anchor.Y, w, h),
        PopoverSide.Left => new Rectangle(anchor.X - g - w, anchor.Y, w, h),
        PopoverSide.Above => new Rectangle(anchor.Center.X - w / 2, anchor.Y - g - h, w, h),
        PopoverSide.Over => new Rectangle(anchor.Center.X - w / 2, anchor.Y, w, h),
        _ => new Rectangle(anchor.Center.X - w / 2, anchor.Bottom + g, w, h),
    };

    /// <summary>
    /// A candidate that covers a rectangle (hard or soft), slid along the anchor's edge to the far side
    /// of what it covered — up/left first, then down/right, rectangles in the caller's order — keeping
    /// only the slides that still share half their extent with the anchor.
    /// </summary>
    private static IEnumerable<Rectangle> Slides(PopoverSide side, Rectangle c, Rectangle anchor, Rectangle viewport,
                                                 IReadOnlyList<Rectangle> blockers, int g)
    {
        var alongY = side is PopoverSide.Right or PopoverSide.Left or PopoverSide.Over;
        foreach (var a in blockers)
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

    /// <summary>
    /// A candidate that covers avoided rectangles, pushed away from the anchor past the farthest edge of
    /// all it covers, again and again while the viewport has room — each step a candidate of its own.
    /// It stops where the clamp would put it back onto what it tried to pass.
    /// </summary>
    private static IEnumerable<Rectangle> Pushes(PopoverSide side, Rectangle c, Rectangle viewport,
                                                 IReadOnlyList<Rectangle> avoid, int g)
    {
        if (side == PopoverSide.Over) yield break;   // there is no "away" from over the anchor
        var outward = side is PopoverSide.Right or PopoverSide.Below;   // the edge grows away from the anchor
        for (var step = 0; step < MaxPushes; step++)
        {
            var any = false;
            var edge = 0;
            foreach (var a in avoid)
            {
                if (!c.Intersects(a)) continue;
                var e = side switch { PopoverSide.Right => a.Right, PopoverSide.Left => a.X, PopoverSide.Below => a.Bottom, _ => a.Y };
                edge = !any ? e : outward ? Math.Max(edge, e) : Math.Min(edge, e);
                any = true;
            }
            if (!any) yield break;
            var moved = side switch
            {
                PopoverSide.Right => new Rectangle(edge + g, c.Y, c.Width, c.Height),
                PopoverSide.Left => new Rectangle(edge - g - c.Width, c.Y, c.Width, c.Height),
                PopoverSide.Below => new Rectangle(c.X, edge + g, c.Width, c.Height),
                _ => new Rectangle(c.X, edge - g - c.Height, c.Width, c.Height),
            };
            var r = Clamp(moved, viewport, g);
            var stillCovers = side switch
            {
                PopoverSide.Right => r.X < edge,
                PopoverSide.Left => r.Right > edge,
                PopoverSide.Below => r.Y < edge,
                _ => r.Bottom > edge,
            };
            if (r == c || stillCovers) yield break;
            yield return r;
            c = r;
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
        PopoverSide.Over => false,
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
