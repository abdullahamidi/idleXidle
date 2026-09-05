using IdleXIdle.Game;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// A creature's hover is its VISIBLE body, not the draw box around it: the box carries the strip's
/// transparent margin, which no pointer should be able to "hover".
/// </summary>
public class creature_presentation_test
{
    // A draw box the size of the enemy layout box, with the figure standing in its middle third —
    // the shape VfxFigure.VisualRect produces for a strip with wide side padding.
    private static readonly CreaturePresentation Swarm =
        new(DrawRect: new Rectangle(1000, 600, 436, 440), Body: new Rectangle(1090, 620, 256, 420));

    [Theory]
    [InlineData(1200, 700)]   // the chest
    [InlineData(1092, 625)]   // the top-left corner of the body
    [InlineData(1344, 1038)]  // the bottom-right corner of the body
    public void test_a_point_on_the_visible_body_hovers(int x, int y)
    {
        Assert.True(Swarm.Hovers(new Point(x, y)));
    }

    [Theory]
    [InlineData(1020, 800)]   // inside the draw box, in the left transparent margin
    [InlineData(1420, 800)]   // inside the draw box, in the right transparent margin
    [InlineData(1200, 605)]   // inside the draw box, above the figure's crown
    [InlineData(1500, 700)]   // beside the creature altogether
    [InlineData(1200, 1100)]  // under its feet
    public void test_a_point_off_the_visible_body_does_not_hover(int x, int y)
    {
        // Arrange / Act
        var p = new Point(x, y);

        // Assert: the margin is drawn-nothing, and hovers nothing.
        Assert.False(Swarm.Hovers(p));
    }

    [Fact]
    public void test_the_hover_and_the_inspector_anchor_are_the_one_body()
    {
        Assert.Equal(Swarm.Body, Swarm.HoverRect);
        Assert.Equal(Swarm.Body, Swarm.InspectorAnchor);
    }
}
