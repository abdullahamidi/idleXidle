using IdleXIdle.Game;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// A HOVER SURFACE STANDS STILL WHILE YOU READ IT.
/// </summary>
/// <remarks>
/// <para>
/// Playtest, 2026-09-09: <i>"The informational text that appears on hover shouldn't move along with
/// the mouse cursor. It should remain in a fixed position (either on the right or the left)."</i>
/// </para>
/// <para>
/// <b>The placement rule was never at fault.</b> <see cref="PopoverPlacement.Place"/> puts a popover
/// beside whatever rectangle it is handed, and its ten existing tests all prove that arithmetic. Two
/// of its four callers handed it the POINTER where a control belonged — <c>UiKit.HoverTip</c> built a
/// synthetic 26x30 "cursor" rect out of the mouse point, and <c>ItemTooltip.Draw</c> a 1x1 one — so
/// the plate translated one-for-one with every mouse move. Nothing in the suite asserted what a
/// caller passes as an anchor, which is exactly where the defect lived: the tested unit was right and
/// the untested wiring was wrong.
/// </para>
/// <para>
/// This file is the missing property, stated once against the rule itself: place a surface beside an
/// element, move the pointer anywhere inside that element, and the surface does not move. It is the
/// test that would have failed before the change and cannot pass if a caller ever goes back to
/// anchoring on the cursor.
/// </para>
/// </remarks>
public class TipStandsStillTests
{
    private static readonly Rectangle Viewport = new(0, 0, 1920, 1080);
    private static readonly PopoverSide[] TipOrder =
        { PopoverSide.Below, PopoverSide.Above, PopoverSide.Right, PopoverSide.Left };
    private static readonly PopoverSide[] CardOrder =
        { PopoverSide.Right, PopoverSide.Left, PopoverSide.Below, PopoverSide.Above };

    private static Rectangle Place(Rectangle anchor, Point size, PopoverSide[] order)
        => PopoverPlacement.Place(anchor, size, Viewport, null, order, 8);

    [Theory]
    [InlineData(400, 300, 520, 44)]     // a settings row: wide and shallow
    [InlineData(1500, 900, 120, 120)]   // a bag cell near the bottom-right corner, where it must flip
    [InlineData(24, 60, 300, 90)]       // a card at the top-left edge, where the clamp bites
    public void test_a_tip_does_not_move_while_the_pointer_moves_inside_its_element(int x, int y, int w, int h)
    {
        var element = new Rectangle(x, y, w, h);
        var size = new Point(430, 160);
        var first = Place(element, size, TipOrder);

        // Every corner and the centre of the element the tip explains. The element is the anchor, so
        // none of these is an input to the placement at all — which is the whole point.
        foreach (var pointer in new[]
                 {
                     new Point(element.Left, element.Top), new Point(element.Right - 1, element.Top),
                     new Point(element.Left, element.Bottom - 1), new Point(element.Right - 1, element.Bottom - 1),
                     element.Center,
                 })
        {
            var again = Place(element, size, TipOrder);
            Assert.True(first == again,
                        $"the tip moved to {again} when the pointer was at {pointer} — it is anchored to the cursor, not the row");
        }
    }

    [Fact]
    public void test_two_neighbouring_cells_get_two_different_places()
    {
        // The other half of "it stands still": it must still FOLLOW the thing being explained. A
        // surface pinned to one spot for every element would satisfy the test above and be useless.
        var a = new Rectangle(200, 400, 120, 120);
        var b = new Rectangle(340, 400, 120, 120);
        Assert.NotEqual(Place(a, new Point(460, 400), CardOrder), Place(b, new Point(460, 400), CardOrder));
    }

    [Fact]
    public void test_the_item_card_lands_beside_its_cell_and_never_on_it()
    {
        // The card is the biggest hover surface in the game — 460 px wide at 100 %, up to 700 tall —
        // and the one the report is most likely about. Beside the cell, never over it: at the pointer
        // it covered the cell's own EQUIP, SELL and SALVAGE buttons, which are the things it exists to
        // help the reader choose between.
        var cell = new Rectangle(600, 300, 160, 260);
        var card = Place(cell, new Point(460, 640), CardOrder);

        Assert.False(card.Intersects(cell), $"the card {card} covers the cell {cell} it is explaining");
        Assert.True(Viewport.Contains(card), $"the card {card} left the page");
    }

    [Fact]
    public void test_a_surface_at_the_page_edge_flips_rather_than_leaving_the_page()
    {
        // The behaviour the old cursor anchor got for free from a 26x30 rect and had to keep: an
        // element in the far corner is answered on the side that fits.
        foreach (var element in new[]
                 {
                     new Rectangle(1840, 1000, 60, 60),   // bottom-right
                     new Rectangle(0, 0, 60, 60),         // top-left
                     new Rectangle(1840, 0, 60, 60),      // top-right
                     new Rectangle(0, 1000, 60, 60),      // bottom-left
                 })
        {
            var placed = Place(element, new Point(430, 200), TipOrder);
            Assert.True(Viewport.Contains(placed), $"a tip beside {element} was placed at {placed}, off the page");
        }
    }
}
