using IdleXIdle.Game;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// An actor's hover is its stable ENVELOPE — the visible body and everything its attack reaches
/// beyond it — never the draw box around it: the box carries the strip's transparent margin, which
/// no pointer should be able to "hover", and the body alone is too narrow for a swing.
/// </summary>
public class actor_presentation_test
{
    // A draw box the size of the enemy layout box, with the idle figure standing in its middle third
    // (the shape VfxFigure.VisualRect produces for a strip with wide side padding), and an envelope
    // 30 px wider on each side and 8 px taller — where the attack's claw reaches.
    private static readonly ActorPresentation Swarm = new(
        DrawRect: new Rectangle(1000, 600, 436, 440),
        Body: new Rectangle(1090, 620, 256, 420),
        Envelope: new Rectangle(1060, 612, 316, 428));

    [Theory]
    [InlineData(1200, 700)]   // the chest
    [InlineData(1092, 625)]   // the top-left corner of the body
    [InlineData(1344, 1038)]  // the bottom-right corner of the body
    public void test_a_point_on_the_visible_body_hovers(int x, int y)
    {
        Assert.True(Swarm.Hovers(new Point(x, y)));
    }

    [Theory]
    [InlineData(1070, 800)]   // left of the idle body, where the attack's claw reaches
    [InlineData(1370, 800)]   // right of the idle body, inside the envelope
    [InlineData(1200, 614)]   // above the idle crown, inside the envelope
    public void test_a_point_the_attack_reaches_hovers_even_while_the_idle_plays(int x, int y)
    {
        // The envelope is measured from what the actor CAN play, so the point hovers on every frame.
        Assert.True(Swarm.Hovers(new Point(x, y)));
    }

    [Theory]
    [InlineData(1020, 800)]   // inside the draw box, in the left transparent margin
    [InlineData(1420, 800)]   // inside the draw box, in the right transparent margin
    [InlineData(1200, 605)]   // inside the draw box, above the envelope's crown
    [InlineData(1500, 700)]   // beside the creature altogether
    [InlineData(1200, 1100)]  // under its feet
    public void test_a_point_off_the_envelope_does_not_hover(int x, int y)
    {
        // Arrange / Act
        var p = new Point(x, y);

        // Assert: the margin is drawn-nothing, and hovers nothing.
        Assert.False(Swarm.Hovers(p));
    }

    [Fact]
    public void test_the_hover_and_the_inspector_anchor_are_the_one_envelope()
    {
        Assert.Equal(Swarm.Envelope, Swarm.HoverRect);
        Assert.Equal(Swarm.Envelope, Swarm.InspectorAnchor);
    }

    [Fact]
    public void test_the_hover_is_never_the_draw_box()
    {
        // The one thing this fixture can still prove: the pointer does not read the box. (That the
        // envelope HOLDS the body is a law of where the envelope is computed — actor_envelope_test —
        // not of a rectangle typed here.)
        Assert.NotEqual(Swarm.DrawRect, Swarm.HoverRect);
    }

    /// <summary>
    /// THE HUNTER, at 100 %: the seeker's real geometry (measured from the shipped strips) in the
    /// 400 x 430 champion box. His swing reaches 45 px past the box on each side, so the envelope is
    /// NOT inside the draw box — the layout box is not an outer bound on an actor's body, and a test
    /// that says it is would send a reader to clip the envelope and put the escape back.
    /// </summary>
    private static readonly ActorPresentation Hunter = new(
        DrawRect: new Rectangle(420, 451, 400, 430),
        Body: new Rectangle(486, 469, 268, 412),
        Envelope: new Rectangle(375, 469, 490, 412));

    [Fact]
    public void test_a_hunters_swing_reaches_outside_his_layout_box_and_is_still_hovered()
    {
        Assert.False(Hunter.DrawRect.Contains(Hunter.Envelope), "the seeker's clips reach past his box");
        Assert.True(Hunter.Hovers(new Point(400, 700)), "left of the box, inside the swing");
        Assert.False(Hunter.Hovers(new Point(360, 700)), "further left, in the attack frame's padding");
    }

    [Fact]
    public void test_an_actor_without_a_reach_beyond_its_body_hovers_exactly_its_body()
    {
        // The champion of a fixture whose only clip is the idle: the envelope IS the body.
        var still = new ActorPresentation(Swarm.DrawRect, Swarm.Body, Swarm.Body);
        Assert.Equal(Swarm.Body, still.HoverRect);
        Assert.False(still.Hovers(new Point(1070, 800)));
    }
}
