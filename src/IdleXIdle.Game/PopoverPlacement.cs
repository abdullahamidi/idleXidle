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
/// One rule, one set of tests: try the preferred sides in order, take the first that fits the viewport
/// and touches no avoided rectangle; failing that, the side that overlaps the avoided rectangles least;
/// then clamp to the viewport with the margin kept.
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

        // TWO PASSES IN ONE. A side that still stands beside the anchor after clamping and covers no
        // avoided rectangle wins outright, first in the caller's order. Failing that, every side is
        // scored — an avoided rectangle's area a thousandfold, the anchor's own area once — and the
        // least bad wins; ties go to the earlier side, so the answer is deterministic.
        Rectangle best = default;
        var bestScore = long.MaxValue;
        for (var i = 0; i < order.Count; i++)
        {
            var candidate = Clamp(At(order[i], anchor, w, h, g), viewport, g);
            var beside = !candidate.Intersects(anchor) || Fits(order[i], candidate, anchor);
            var hard = Overlap(candidate, avoid);
            if (beside && hard == 0) return candidate;
            var score = hard * 1000L + Overlap(candidate, new[] { anchor });
            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }
        return best;
    }

    /// <summary>The rectangle a side would put the popover in, before any clamping.</summary>
    private static Rectangle At(PopoverSide side, Rectangle anchor, int w, int h, int g) => side switch
    {
        PopoverSide.Right => new Rectangle(anchor.Right + g, anchor.Y, w, h),
        PopoverSide.Left => new Rectangle(anchor.X - g - w, anchor.Y, w, h),
        PopoverSide.Above => new Rectangle(anchor.Center.X - w / 2, anchor.Y - g - h, w, h),
        _ => new Rectangle(anchor.Center.X - w / 2, anchor.Bottom + g, w, h),
    };

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

    /// <summary>The area a candidate covers of the avoided rectangles, summed.</summary>
    private static long Overlap(Rectangle r, IReadOnlyList<Rectangle> avoid)
    {
        long sum = 0;
        foreach (var a in avoid)
        {
            var i = Rectangle.Intersect(r, a);
            if (i.Width > 0 && i.Height > 0) sum += (long)i.Width * i.Height;
        }
        return sum;
    }
}
