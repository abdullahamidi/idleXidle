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
    /// <summary>The hunter's LAYOUT box — what the request used to avoid, and must not any more.</summary>
    private static readonly Rectangle ChampBox = new(420, 320, 400, 430);

    /// <summary>
    /// A SYNTHETIC narrow envelope — 50 px inside the box on each side, 18 px under its crown. No
    /// shipped hunter is this narrow (see <see cref="WideChamp"/>); it is the shape that shows the
    /// reclaimed margin, and most of the placement cases below only need "a hunter, somewhere left".
    /// </summary>
    private static readonly Rectangle Champ = new(470, 338, 300, 412);

    /// <summary>
    /// A SHIPPED envelope: the seeker's, whose swing reaches 45 px past the layout box on each side.
    /// The direction that actually occurs in the game, and the one the keep-out must handle.
    /// </summary>
    private static readonly Rectangle WideChamp = new(375, 338, 490, 412);

    private static readonly Rectangle IdlePanel = new(1560, 100, 350, 280);
    private static readonly Rectangle Viewport = new(186, 290, 1734, 660);   // ceiling 290, floor 950
    private static readonly Point Plate = new(396, 348);                      // the 125 % plate

    private static Rectangle KeepOut => EnemyInspectorPlacement.HunterKeepOut(Champ);

    private static bool Inside(Rectangle r, Rectangle v) => r.X >= v.X && r.Y >= v.Y && r.Right <= v.Right && r.Bottom <= v.Bottom;

    // ── The avoid set ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_hard_avoid_set_holds_the_hunters_keep_out_and_the_idle_panel()
    {
        var avoid = EnemyInspectorPlacement.Avoid(Champ, IdlePanel);
        Assert.Contains(KeepOut, avoid);
        Assert.Contains(IdlePanel, avoid);
        Assert.Equal(2, avoid.Count);
    }

    [Fact]
    public void test_without_a_laid_out_panel_only_the_hunter_is_avoided()
    {
        var avoid = EnemyInspectorPlacement.Avoid(Champ, Rectangle.Empty);
        Assert.Single(avoid);
        Assert.Equal(KeepOut, avoid[0]);
    }

    [Fact]
    public void test_the_keep_out_is_the_envelope_plus_the_one_clearance_on_every_side()
    {
        var c = EnemyInspectorPlacement.HunterClearance;
        Assert.InRange(c, 1, 24);   // a breath, not a box
        Assert.Equal(Champ.X - c, KeepOut.X);
        Assert.Equal(Champ.Y - c, KeepOut.Y);
        Assert.Equal(Champ.Right + c, KeepOut.Right);
        Assert.Equal(Champ.Bottom + c, KeepOut.Bottom);
    }

    [Fact]
    public void test_the_keep_out_follows_the_envelope_wherever_it_falls_against_the_layout_box()
    {
        // The keep-out is the ENVELOPE's, in both directions — it is not clamped to the layout box
        // and it is not derived from it. A narrow hunter reclaims margin the box used to refuse; a
        // shipped one (whose swing leaves the box) is protected out there, which the box never did.
        Assert.True(KeepOut.X > ChampBox.X && KeepOut.Right < ChampBox.Right, "a narrow hunter reclaims margin");

        var wide = EnemyInspectorPlacement.HunterKeepOut(WideChamp);
        Assert.True(wide.X < ChampBox.X && wide.Right > ChampBox.Right, "a shipped hunter's swing leaves the box");
    }

    [Fact]
    public void test_a_shipped_hunters_swing_is_protected_where_the_layout_box_never_reached()
    {
        // Arrange: a creature just right of the seeker's swing. The old rule avoided ChampBox, whose
        // right edge is 45 px INSIDE the envelope, so it would have allowed a plate over the blade.
        var body = new Rectangle(1000, 560, 180, 240);
        var wall = new Rectangle(1192, 0, 800, 1200);          // RIGHT walled off, so the plate goes LEFT
        var tall = new Rectangle(186, 290, 1734, 840);

        // Act
        var plate = EnemyInspectorPlacement.Place(body, Plate, tall, WideChamp, wall);

        // Assert: clear of the swing, and of the strip of it that lies outside the layout box.
        var keepOut = EnemyInspectorPlacement.HunterKeepOut(WideChamp);
        Assert.False(plate.Intersects(keepOut), $"the plate {plate} covers the swing {keepOut}");
        Assert.False(plate.Intersects(new Rectangle(ChampBox.Right, WideChamp.Y, WideChamp.Right - ChampBox.Right, WideChamp.Height)),
                     "the blade past the box's right edge is body, not margin");
    }

    // ── Where the plate stands ───────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_plate_may_stand_in_the_margin_the_layout_box_used_to_refuse()
    {
        // Arrange: a creature whose LEFT placement lands the plate between the hunter's envelope and
        // the edge of his old layout box. RIGHT is walled off; ABOVE and BELOW have no room.
        var gap = PopoverPlacement.Gap;
        var body = new Rectangle(800 + gap + Plate.X, 560, 180, 240);
        var wall = new Rectangle(body.Right + 1, 0, 1000, 1200);
        var tight = new Rectangle(186, body.Y - 100, 1734, body.Height + 200);

        // Act
        var plate = EnemyInspectorPlacement.Place(body, Plate, tight, Champ, wall);

        // Assert: left of the creature, clear of the hunter's keep-out — and inside the old box.
        Assert.True(plate.Right <= body.X, $"the plate {plate} should stand left of the creature {body}");
        Assert.False(plate.Intersects(KeepOut), $"the plate {plate} touches the hunter's keep-out {KeepOut}");
        Assert.True(plate.Intersects(ChampBox), "the reclaimed margin is exactly what the layout box used to refuse");
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
        Assert.False(plate.Intersects(KeepOut));
        Assert.False(plate.Intersects(body), "the plate stands beside the creature, not on it");
        Assert.True(Inside(plate, Viewport));
    }

    [Fact]
    public void test_a_creature_beside_the_hunter_keeps_the_plate_off_the_hunter()
    {
        // Arrange: the creature just right of the hunter; RIGHT is walled off by the panel and ABOVE
        // would reach the hunter's envelope — the plate must still find a place off the hunter.
        var body = new Rectangle(1000, 660, 180, 240);
        var wall = new Rectangle(1192, 300, 700, 700);
        var tallViewport = new Rectangle(186, 290, 1734, 840);

        // Act
        var plate = EnemyInspectorPlacement.Place(body, Plate, tallViewport, Champ, wall);

        // Assert
        Assert.False(plate.Intersects(KeepOut), $"the plate {plate} covers the hunter");
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
