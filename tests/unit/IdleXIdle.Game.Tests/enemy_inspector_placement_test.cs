using System.Linq;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// The enemy inspector's placement request — the one the screen makes — with the arena's real shape:
/// the hunter at the left, the IDLE panel in the right column reaching under the header stack, the
/// viewport between the header stack and the skill dock. Pure rectangles; no device.
/// </summary>
public class enemy_inspector_placement_test
{
    private static readonly Rectangle Champ = new(420, 320, 400, 430);
    private static readonly Rectangle IdlePanel = new(1560, 100, 350, 280);
    private static readonly Rectangle Viewport = new(186, 290, 1734, 660);   // ceiling 290, floor 950
    private static readonly Point Plate = new(396, 348);                      // the 125 % plate

    private static bool Inside(Rectangle r, Rectangle v) => r.X >= v.X && r.Y >= v.Y && r.Right <= v.Right && r.Bottom <= v.Bottom;

    [Fact]
    public void test_the_hard_avoid_set_holds_the_hunter_and_the_idle_panel()
    {
        var avoid = EnemyInspectorPlacement.Avoid(Champ, IdlePanel);
        Assert.Contains(Champ, avoid);
        Assert.Contains(IdlePanel, avoid);
    }

    [Fact]
    public void test_without_a_laid_out_panel_only_the_hunter_is_avoided()
    {
        var avoid = EnemyInspectorPlacement.Avoid(Champ, Rectangle.Empty);
        Assert.Single(avoid);
        Assert.Equal(Champ, avoid[0]);
    }

    [Fact]
    public void test_a_creature_near_the_right_edge_does_not_put_the_plate_on_the_idle_panel()
    {
        // Arrange: RIGHT cannot fit; ABOVE would cover the panel's foot; LEFT is free.
        var body = new Rectangle(1520, 660, 180, 240);

        // Act
        var plate = EnemyInspectorPlacement.Place(body, Plate, Viewport, Champ, IdlePanel);

        // Assert
        Assert.False(plate.Intersects(IdlePanel), $"the plate {plate} covers the IDLE panel");
        Assert.False(plate.Intersects(Champ));
        Assert.False(plate.Intersects(body), "the plate stands beside the creature, not on it");
        Assert.True(Inside(plate, Viewport));
    }

    [Fact]
    public void test_a_creature_beside_the_hunter_keeps_the_plate_off_the_hunter()
    {
        // Arrange: the creature just right of the hunter; RIGHT is walled off by the panel and ABOVE
        // would reach the hunter's box — the plate must still find a place off the hunter.
        var body = new Rectangle(1000, 660, 180, 240);
        var wall = new Rectangle(1192, 300, 700, 700);
        var tallViewport = new Rectangle(186, 290, 1734, 840);

        // Act
        var plate = EnemyInspectorPlacement.Place(body, Plate, tallViewport, Champ, wall);

        // Assert
        Assert.False(plate.Intersects(Champ), $"the plate {plate} covers the hunter");
        Assert.False(plate.Intersects(wall));
        Assert.True(Inside(plate, tallViewport));
    }

    [Fact]
    public void test_the_side_away_from_the_hunter_is_tried_first()
    {
        var right = EnemyInspectorPlacement.Order(new Rectangle(1200, 600, 180, 240), Champ);
        var left = EnemyInspectorPlacement.Order(new Rectangle(100, 600, 180, 240), Champ);
        Assert.Equal(PopoverSide.Right, right[0]);
        Assert.Equal(PopoverSide.Left, right.Last());
        Assert.Equal(PopoverSide.Left, left[0]);
        Assert.Equal(PopoverSide.Right, left.Last());
    }

    [Fact]
    public void test_the_plate_never_leaves_the_viewport()
    {
        foreach (var body in new[] { new Rectangle(1700, 660, 180, 240), new Rectangle(900, 300, 180, 240), new Rectangle(1200, 880, 180, 60) })
        {
            var plate = EnemyInspectorPlacement.Place(body, Plate, Viewport, Champ, IdlePanel);
            Assert.True(Inside(plate, Viewport), $"body {body} put the plate at {plate}");
        }
    }
}
