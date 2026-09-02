using IdleXIdle.Game;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// UiMotion.Ease is asked EVERY FRAME by every control that has a hover, whether or not anything is
/// happening — that is the shape of an immediate-mode UI. So the one thing it must never do is move a
/// control that is standing still: brief §30 is "animate CHANGE, not everything", and a value that
/// wanders on its own is the difference between an interface that reacts and one that fidgets.
/// </summary>
/// <remarks>
/// Four of the eleven screen agents in the UI polish pass independently wrote a private guard around
/// this call ("only ask while something is actually moving"), which is what a shared primitive looks
/// like when it cannot be trusted. These tests pin the contract instead.
/// </remarks>
public class UiMotionRestTest
{
    private const int Key = 0x5E77_1ED;

    private static void Frames(int n, float dt = 1f / 60f)
    {
        for (var i = 0; i < n; i++) UiMotion.Tick(dt);
    }

    [Fact]
    public void test_a_control_at_rest_never_moves()
    {
        UiMotion.Clear();
        UiMotion.Reduced = false;
        UiMotion.Tick(1f / 60f);

        // Twenty frames of a control nobody is touching. Every single ask must read exactly zero:
        // one frame of 0.9 in this loop is a flash on a button nobody is pointing at.
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(0f, UiMotion.Ease(Key, 0f));
            UiMotion.Tick(1f / 60f);
        }
    }

    [Fact]
    public void test_a_hover_fades_in_and_lands_on_one()
    {
        UiMotion.Clear();
        UiMotion.Reduced = false;
        UiMotion.Tick(1f / 60f);

        // The first ask of a hover starts from rest, not from the top.
        var first = UiMotion.Ease(Key, 1f, UiMotion.Fast);
        Assert.True(first < 0.5f, $"a hover must fade in from rest, not appear at {first}");

        // And it gets all the way there, well inside the brief's 80-120 ms band.
        Frames(12);
        for (var i = 0; i < 12; i++) { UiMotion.Ease(Key, 1f, UiMotion.Fast); UiMotion.Tick(1f / 60f); }
        Assert.Equal(1f, UiMotion.Ease(Key, 1f, UiMotion.Fast));
    }

    [Fact]
    public void test_a_hover_that_ends_fades_out_and_stays_out()
    {
        UiMotion.Clear();
        UiMotion.Reduced = false;
        UiMotion.Tick(1f / 60f);

        for (var i = 0; i < 24; i++) { UiMotion.Ease(Key, 1f, UiMotion.Fast); UiMotion.Tick(1f / 60f); }
        Assert.Equal(1f, UiMotion.Ease(Key, 1f, UiMotion.Fast));

        // Let go: it comes down, and then it STAYS down — the settle must not re-arm the fade.
        for (var i = 0; i < 24; i++) { UiMotion.Ease(Key, 0f, UiMotion.Fast); UiMotion.Tick(1f / 60f); }
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(0f, UiMotion.Ease(Key, 0f, UiMotion.Fast));
            UiMotion.Tick(1f / 60f);
        }
    }

    [Fact]
    public void test_reduced_motion_lands_on_the_end_state_at_once()
    {
        UiMotion.Clear();
        UiMotion.Reduced = true;
        UiMotion.Tick(1f / 60f);

        Assert.Equal(1f, UiMotion.Ease(Key, 1f, UiMotion.Fast));
        Assert.Equal(0f, UiMotion.Ease(Key, 0f, UiMotion.Fast));

        UiMotion.Reduced = false;
    }
}
