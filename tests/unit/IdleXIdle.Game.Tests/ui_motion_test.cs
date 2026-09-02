using IdleXIdle.Game;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// <see cref="UiMotion.Ease"/>'s resting contract. Every custom-drawn cell in the game asks for its
/// hover EVERY FRAME, hovered or not — <c>UiKit.Button</c>, MapScreen's region cards, RosterScreen's
/// hunter cards and BUILD's tiles, rows, cards and chips all do. So the answer for a control the mouse
/// is NOT on has to be a flat zero, forever; anything else is motion with no change behind it, which
/// is the one thing the UI polish brief §30 rules out and this class's own summary ("nothing here
/// loops") promises.
/// </summary>
/// <remarks>
/// It did loop. An absent key was read as <c>1 - target</c>, so a resting control (target 0) started at
/// ONE, faded to zero over <see cref="UiMotion.Fast"/>, was dropped from the store on settling, and was
/// absent again on the next frame — a six-frame sawtooth that never stopped. On BUILD, where the wash is
/// a 22 % lavender rather than a button's 7 % white, every library tile, keystone chip, loadout row and
/// reinforcement chip measured 19,16,26 ↔ 42,37,59 at 10 Hz. A still capture cannot see it: the shutter
/// falls on frame 60, which is the trough.
/// </remarks>
public class UiMotionEaseTests
{
    private const float Frame = 1f / 60f;

    private static void Frames(int n, Action<int> each)
    {
        for (var i = 0; i < n; i++) { UiMotion.Tick(Frame); each(i); }
    }

    [Fact]
    public void test_a_control_at_rest_reads_zero_on_every_frame()
    {
        UiMotion.Clear();
        UiMotion.Reduced = false;
        const int key = 4242;

        // Two full Fast durations' worth of frames — three times round the old sawtooth.
        Frames(24, _ => Assert.Equal(0f, UiMotion.Ease(key, 0f, UiMotion.Fast)));
    }

    [Fact]
    public void test_a_hover_still_fades_in_from_nothing_and_settles_at_one()
    {
        UiMotion.Clear();
        UiMotion.Reduced = false;
        const int key = 4243;

        var first = 1f;
        Frames(1, _ => first = UiMotion.Ease(key, 1f, UiMotion.Fast));
        Assert.True(first > 0f && first < 1f);      // it MOVED, and it is not there yet

        var seen = new List<float> { first };
        Frames(5, _ => seen.Add(UiMotion.Ease(key, 1f, UiMotion.Fast)));
        for (var i = 1; i < seen.Count; i++) Assert.True(seen[i] >= seen[i - 1]);   // monotonic
        Assert.Equal(1f, seen[^1], 3);                                              // and it arrives

        // It STAYS there while the mouse stays on it — a settled hover never restarts.
        Frames(10, _ => Assert.Equal(1f, UiMotion.Ease(key, 1f, UiMotion.Fast)));
    }

    [Fact]
    public void test_leaving_a_hovered_control_fades_out_once_and_then_rests()
    {
        UiMotion.Clear();
        UiMotion.Reduced = false;
        const int key = 4244;

        Frames(10, _ => UiMotion.Ease(key, 1f, UiMotion.Fast));   // hovered, and settled at 1

        var seen = new List<float>();
        Frames(6, _ => seen.Add(UiMotion.Ease(key, 0f, UiMotion.Fast)));
        for (var i = 1; i < seen.Count; i++) Assert.True(seen[i] <= seen[i - 1]);   // monotonic down
        Assert.Equal(0f, seen[^1]);

        Frames(12, _ => Assert.Equal(0f, UiMotion.Ease(key, 0f, UiMotion.Fast)));   // and it stays out
    }

    [Fact]
    public void test_reduced_motion_answers_the_target_at_once_either_way()
    {
        UiMotion.Clear();
        UiMotion.Reduced = true;
        const int key = 4245;
        try
        {
            UiMotion.Tick(Frame);
            Assert.Equal(1f, UiMotion.Ease(key, 1f, UiMotion.Fast));
            Assert.Equal(0f, UiMotion.Ease(key, 0f, UiMotion.Fast));
        }
        finally { UiMotion.Reduced = false; }
    }

    /// <summary>The three rungs are the brief's three bands (§31): 80–120, 150–220, 250–450 ms.</summary>
    [Fact]
    public void test_the_three_durations_sit_inside_the_briefs_bands()
    {
        Assert.InRange(UiMotion.Fast, 0.080f, 0.120f);
        Assert.InRange(UiMotion.Transition, 0.150f, 0.220f);
        Assert.InRange(UiMotion.Reward, 0.250f, 0.450f);
    }
}
