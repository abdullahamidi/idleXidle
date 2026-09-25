using System.Linq;
using IdleXIdle.Game.Presentation;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE ACTION HANDOFF (ADR-011, 2026-09-25): an action whose blow has landed hands the figure to the next action by
/// ARRIVING at its exit pose. Only its recovery (and the settle held on the exit pose) may be borrowed from.
/// </summary>
/// <remarks>
/// The failure this stops, filmed on a fast TEMPO build: SPRAY took the figure at its latest start while the basic
/// swing was still in its low lunge, and the Seeker jumped in one frame to SPRAY's ready pose.
/// </remarks>
public class ActionHandoffTest
{
    private const float Pose = 2000f / 60f;   // two display frames

    [Fact]
    public void test_handoff_plain_clip_carries_its_phases_and_its_settle()
    {
        // Arrange / Act: a swing at 2.3x speed with 150 ms of settle
        var t = ActionClipTiming.Plain(54f, 150f);

        // Assert: eight frames, contact on 5, recovery on 7, the settle held on the exit pose
        Assert.Equal(8, t.Frames);
        Assert.Equal(5 * 54f, t.MarkerMs("contact"), 3);
        Assert.Equal(7 * 54f, t.MarkerMs("recovery"), 3);
        Assert.Equal(8 * 54f + 150f, t.TotalMs, 3);
        Assert.Equal(54f, t.RecoveryMotionMs, 3);
        Assert.Equal(150f, t.HoldMs, 3);
    }

    [Fact]
    public void test_handoff_settle_gives_way_before_the_recovery_pace()
    {
        // Arrange
        var t = ActionClipTiming.Plain(54f, 150f);

        // Act: 100 ms from the recovery's start to the next action
        var fitted = t.FitRecovery(100f, Pose, out var compression);

        // Assert: the recovery plays at its own pace, the hold shrinks, nothing before the recovery moved
        Assert.Equal(1f, compression);
        Assert.Equal(54f, fitted.RecoveryMotionMs, 3);
        Assert.Equal(t.MarkerMs("recovery") + 100f, fitted.TotalMs, 3);
        for (var i = 0; i < 7; i++) Assert.Equal(t.FrameMs[i], fitted.FrameMs[i]);
    }

    [Fact]
    public void test_handoff_compresses_only_the_recovery_and_ends_on_the_exit_pose()
    {
        // Arrange: an authored clip with two recovery poses (recovery marker on 6)
        var t = new ActionClipTiming(new[] { 90f, 110f, 150f, 50f, 230f, 90f, 90f, 110f },
                                     markers: new System.Collections.Generic.Dictionary<string, int> { ["release"] = 3, ["recovery"] = 6 });

        // Act: 100 ms for 200 ms of recovery
        var fitted = t.FitRecovery(100f, Pose, out var compression);

        // Assert: 2x, every protected frame intact, both poses shown for at least two display frames, the clip ends on time
        Assert.Equal(2f, compression, 3);
        for (var i = 0; i < 6; i++) Assert.Equal(t.FrameMs[i], fitted.FrameMs[i]);
        Assert.True(fitted.FrameMs[6] >= Pose && fitted.FrameMs[7] >= Pose);
        Assert.Equal(t.MarkerMs("recovery") + 100f, fitted.TotalMs, 2);
        Assert.Equal(7, fitted.FrameAt(fitted.TotalMs - 1f));
    }

    [Fact]
    public void test_handoff_too_little_time_keeps_the_first_recovery_pose_and_the_exit()
    {
        // Arrange: three recovery poses, room for two readable ones
        var t = new ActionClipTiming(new[] { 100f, 100f, 100f, 100f, 100f },
                                     markers: new System.Collections.Generic.Dictionary<string, int> { ["contact"] = 1, ["recovery"] = 2 });

        // Act
        var fitted = t.FitRecovery(2.2f * Pose, Pose, out _);

        // Assert: the middle pose is skipped (0 ms, never drawn); the first and the exit are each readable
        Assert.True(fitted.FrameMs[2] >= Pose && fitted.FrameMs[4] >= Pose);
        Assert.Equal(0f, fitted.FrameMs[3]);
        Assert.DoesNotContain(3, Enumerable.Range(0, 400).Select(ms => fitted.FrameAt(ms)));
    }

    [Fact]
    public void test_handoff_is_not_planned_when_the_clip_ends_before_the_next_action_wants_the_figure()
    {
        // Arrange / Act: a slow build — the settle is over long before the next wind-up
        var plan = ActionHandoff.Plan(nowMs: 1000f, recoveryStartMs: 1100f, recoveryMotionMs: 125f, recoveryFloorMs: 42f,
                                      naturalEndMs: 1525f, idealStartMs: 1800f, latestStartMs: 1900f);

        // Assert
        Assert.Null(plan);
    }

    [Fact]
    public void test_handoff_arrives_at_the_exit_exactly_when_the_next_action_wants_the_figure()
    {
        // Act: the swing's recovery would run past SPRAY's ideal start; compressing it fits
        var plan = ActionHandoff.Plan(nowMs: 5110f, recoveryStartMs: 5208f, recoveryMotionMs: 54f, recoveryFloorMs: 33.3f,
                                      naturalEndMs: 5562f, idealStartMs: 5250f, latestStartMs: 5330f)!.Value;

        // Assert
        Assert.Equal(HandoffFit.Compressed, plan.Fit);
        Assert.Equal(5250f, plan.ExitMs, 2);
        Assert.Equal(42f, plan.AvailableMs, 2);
    }

    [Fact]
    public void test_handoff_at_the_readable_floor_lets_the_next_wind_up_absorb_the_rest()
    {
        // Act: the ideal start is before the recovery's readable floor, the latest start after it
        var plan = ActionHandoff.Plan(nowMs: 5110f, recoveryStartMs: 5208f, recoveryMotionMs: 54f, recoveryFloorMs: 33.3f,
                                      naturalEndMs: 5562f, idealStartMs: 5200f, latestStartMs: 5330f)!.Value;

        // Assert: the recovery keeps its floor; the next action starts later, inside its own elastic range
        Assert.Equal(HandoffFit.Floor, plan.Fit);
        Assert.Equal(5208f + 33.3f, plan.ExitMs, 2);
        Assert.True(plan.ExitMs <= 5330f);
    }

    [Fact]
    public void test_handoff_reservation_a_swing_that_cannot_clear_the_next_wind_up_is_not_begun()
    {
        // Arrange: the fastest build (TEMPO at its cap): 32 ms frames, the swing starts 160 ms before its beat at
        // 4000, and SPRAY's beat is 400 ms later with a 470 ms minimum wind-up (latest start 3930)
        var swing = ActionClipTiming.Plain(32f, 0f);

        // Act
        var exit = ActionHandoff.EarliestExit(3840f, swing, Pose, 3f);

        // Assert: its follow-through alone ends after SPRAY must have started, so the reservation keeps the figure;
        // on the realistic fast build (54 ms frames, 700 ms between them) the same swing fits with room to spare
        Assert.True(exit > 4400f - 470f);
        Assert.True(ActionHandoff.EarliestExit(5100f - 272f, ActionClipTiming.Plain(54.4f, 0f), Pose, 3f) <= 5800f - 470f);
    }

    [Fact]
    public void test_handoff_never_shortens_a_protected_phase_and_reports_a_squeeze()
    {
        // Act: the next action's latest start comes before the recovery has even begun
        var plan = ActionHandoff.Plan(nowMs: 5110f, recoveryStartMs: 5208f, recoveryMotionMs: 54f, recoveryFloorMs: 33.3f,
                                      naturalEndMs: 5562f, idealStartMs: 5100f, latestStartMs: 5150f)!.Value;

        // Assert: the exit is never before the recovery's start (the follow-through plays whole), and it says so
        Assert.Equal(HandoffFit.Squeezed, plan.Fit);
        Assert.Equal(5208f, plan.ExitMs, 2);
        Assert.Equal(0f, plan.AvailableMs, 2);
    }
}
