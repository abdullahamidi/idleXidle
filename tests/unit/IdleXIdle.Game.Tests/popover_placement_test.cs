using System.Collections.Generic;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// The one placement rule every floating surface shares: inside the viewport, on the preferred side
/// where it fits, flipped when it does not, clear of what it must not cover. Pure arithmetic — no
/// device, no batch.
/// </summary>
public class popover_placement_test
{
    private static readonly Rectangle Page = new(0, 0, 1920, 1080);
    private static readonly Point Size = new(300, 200);

    private static bool Inside(Rectangle r, Rectangle v) => r.X >= v.X && r.Y >= v.Y && r.Right <= v.Right && r.Bottom <= v.Bottom;

    [Fact]
    public void test_the_preferred_side_is_used_where_it_fits()
    {
        var anchor = new Rectangle(800, 500, 100, 100);
        var r = PopoverPlacement.Place(anchor, Size, Page);
        Assert.True(r.X >= anchor.Right, "should stand to the RIGHT");
        Assert.Equal(anchor.Y, r.Y);
        Assert.True(Inside(r, Page));
    }

    [Fact]
    public void test_an_anchor_near_the_right_edge_flips_left()
    {
        var anchor = new Rectangle(1800, 500, 100, 100);
        var r = PopoverPlacement.Place(anchor, Size, Page);
        Assert.True(r.Right <= anchor.X, "should flip to the LEFT");
        Assert.True(Inside(r, Page));
    }

    [Fact]
    public void test_an_anchor_near_the_left_edge_stays_right()
    {
        var anchor = new Rectangle(10, 500, 100, 100);
        var r = PopoverPlacement.Place(anchor, Size, Page, preferred: new[] { PopoverSide.Left, PopoverSide.Right });
        Assert.True(r.X >= anchor.Right, "LEFT cannot fit, so RIGHT");
        Assert.True(Inside(r, Page));
    }

    [Fact]
    public void test_an_anchor_near_the_top_goes_below_and_near_the_bottom_goes_above()
    {
        var top = new Rectangle(800, 5, 100, 50);
        var r = PopoverPlacement.Place(top, Size, Page, preferred: new[] { PopoverSide.Above, PopoverSide.Below });
        Assert.True(r.Y >= top.Bottom, "ABOVE cannot fit, so BELOW");
        Assert.True(Inside(r, Page));

        var bottom = new Rectangle(800, 1020, 100, 50);
        r = PopoverPlacement.Place(bottom, Size, Page, preferred: new[] { PopoverSide.Below, PopoverSide.Above });
        Assert.True(r.Bottom <= bottom.Y, "BELOW cannot fit, so ABOVE");
        Assert.True(Inside(r, Page));
    }

    [Fact]
    public void test_an_avoided_rectangle_on_the_preferred_side_flips_the_popover()
    {
        var anchor = new Rectangle(800, 500, 100, 100);
        var buyButton = new Rectangle(920, 520, 200, 60);   // right where RIGHT would land
        var r = PopoverPlacement.Place(anchor, Size, Page, avoid: new[] { buyButton });
        Assert.False(r.Intersects(buyButton), "must not cover the avoided rectangle");
        Assert.True(Inside(r, Page));
    }

    [Fact]
    public void test_a_beside_popover_that_covers_an_avoided_rectangle_slides_along_the_anchor_before_it_flips()
    {
        // Arrange: the trader — a tall card beside a tall offer would cover the BUY under the NEXT offer.
        var anchor = new Rectangle(800, 300, 200, 500);
        var buy = new Rectangle(1012, 700, 200, 60);   // right where RIGHT's foot would land

        // Act
        var r = PopoverPlacement.Place(anchor, new Point(300, 450), Page, new[] { buy });

        // Assert: still RIGHT, slid up clear of the BUY, still beside the offer.
        Assert.True(r.X >= anchor.Right, "should still stand to the RIGHT");
        Assert.True(r.Bottom <= buy.Y, "slid up, clear of the BUY");
        Assert.True(r.Y < anchor.Bottom && r.Bottom > anchor.Y, "still beside the offer");
        Assert.False(r.Intersects(buy));
        Assert.True(Inside(r, Page));
    }

    [Fact]
    public void test_a_slide_that_would_leave_the_anchor_flips_instead()
    {
        // Arrange: a short anchor; clearing the BUY beside it would leave the popover hanging off a corner.
        var anchor = new Rectangle(800, 500, 100, 100);
        var buy = new Rectangle(920, 520, 200, 60);

        // Act
        var r = PopoverPlacement.Place(anchor, Size, Page, new[] { buy });

        // Assert
        Assert.True(r.Right <= anchor.X, "flips LEFT rather than slide away from the anchor");
        Assert.False(r.Intersects(buy));
    }

    [Fact]
    public void test_multiple_avoided_rectangles_leave_the_one_clean_side()
    {
        var anchor = new Rectangle(800, 500, 100, 100);
        var avoid = new List<Rectangle>
        {
            new(912, 450, 400, 160),   // RIGHT, beside the anchor
            new(400, 450, 380, 160),   // LEFT, beside the anchor
            new(600, 200, 500, 300),   // ABOVE, over the anchor's head
        };
        var r = PopoverPlacement.Place(anchor, Size, Page, avoid);
        Assert.True(r.Y >= anchor.Bottom, "only BELOW is clean");
        foreach (var a in avoid) Assert.False(r.Intersects(a));
    }

    [Fact]
    public void test_when_every_side_covers_something_the_least_covering_side_is_chosen_deterministically()
    {
        // Arrange: a viewport that ends right past the blocks, so no push can step clear of them.
        var anchor = new Rectangle(800, 500, 100, 100);
        var view = new Rectangle(380, 230, 940, 700);
        var avoid = new List<Rectangle>
        {
            new(900, 500, 400, 300),   // RIGHT, full cover
            new(500, 500, 300, 300),   // LEFT, full cover
            new(650, 250, 400, 250),   // ABOVE, full cover
            new(700, 612, 100, 100),   // BELOW, a corner only
        };

        // Act
        var a = PopoverPlacement.Place(anchor, Size, view, avoid);
        var b = PopoverPlacement.Place(anchor, Size, view, avoid);

        // Assert
        Assert.Equal(a, b);
        Assert.True(a.Y >= anchor.Bottom, "BELOW covers least");
        Assert.True(Inside(a, view));
    }

    [Fact]
    public void test_a_below_popover_pushes_past_a_covered_band_into_free_room()
    {
        // Arrange: the Warren — a card whose foot sits on the grid's scroll hint, with the page's free
        // room under the grid. BELOW covers the hint; sliding along the card cannot clear a band the
        // width of the page; pushing past it can.
        var card = new Rectangle(800, 700, 100, 100);
        var hint = new Rectangle(0, 812, 1920, 30);

        // Act
        var r = PopoverPlacement.Place(card, Size, Page, new[] { hint }, new[] { PopoverSide.Below });

        // Assert: under the hint, still centred on its card, inside the page.
        Assert.True(r.Y > hint.Bottom, "pushed past the hint");
        Assert.False(r.Intersects(hint));
        Assert.Equal(card.Center.X, r.Center.X);
        Assert.True(Inside(r, Page));
    }

    [Fact]
    public void test_a_push_is_tried_only_after_every_side_has_been()
    {
        // Arrange: RIGHT is blocked by a BUY it cannot slide clear of; LEFT is free. A push past the
        // BUY would also be clean — but it is farther, and every side comes first.
        var anchor = new Rectangle(800, 500, 100, 100);
        var buy = new Rectangle(912, 500, 300, 60);

        // Act
        var r = PopoverPlacement.Place(anchor, Size, Page, new[] { buy });

        // Assert
        Assert.True(r.Right <= anchor.X, "LEFT, not RIGHT pushed past the BUY");
        Assert.False(r.Intersects(buy));
    }

    [Fact]
    public void test_a_scaled_viewport_keeps_the_popover_inside_with_its_margin()
    {
        // A 150 % profile: bigger popovers, the same page. The result never leaves the viewport.
        var big = new Point(700, 480);
        foreach (var anchor in new[] { new Rectangle(1850, 40, 60, 60), new Rectangle(10, 1000, 60, 60), new Rectangle(1700, 950, 100, 100) })
        {
            var r = PopoverPlacement.Place(anchor, big, Page);
            Assert.True(Inside(r, Page), $"anchor {anchor} put the popover at {r}");
        }
    }

    [Fact]
    public void test_a_popover_larger_than_the_viewport_is_fitted_not_lost()
    {
        var small = new Rectangle(100, 100, 400, 300);
        var r = PopoverPlacement.Place(new Rectangle(150, 150, 40, 40), new Point(900, 900), small);
        Assert.True(Inside(r, small));
    }
}
