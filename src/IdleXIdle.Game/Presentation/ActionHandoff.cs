using System;

namespace IdleXIdle.Game.Presentation;

/// <summary>How a handoff met the next action's reservation.</summary>
public enum HandoffFit
{
    /// <summary>The recovery played at its own pace; only the settle hold gave way.</summary>
    Natural,

    /// <summary>The recovery played faster, and arrived at its exit pose exactly when the next action wanted the figure.</summary>
    Compressed,

    /// <summary>
    /// The recovery at its readable floor still arrives after the next action's IDEAL start, so the next action
    /// begins later and its own elastic wind-up absorbs the rest (the approved timing model's late start).
    /// </summary>
    Floor,

    /// <summary>
    /// The next action's LATEST start came before the recovery's readable floor: the recovery was squeezed below it.
    /// Reported, never silent: presentation demand exceeds the time the fight leaves.
    /// </summary>
    Squeezed,

    /// <summary>
    /// THE FAST-TEMPO FALLBACK (ADR-011): the next action is PERFORMED and its latest start falls inside this
    /// action's protected frames. The incoming action keeps its minimum readable anticipation, so this one YIELDS the
    /// figure at that latest start, which is never before its contact: its gameplay event has resolved, the rest of it
    /// (the follow-through and recovery) is not shown, and a melee body's lunge is carried home under the wind-up.
    /// Reported, never silent: it is a fallback, not the model.
    /// </summary>
    Yielded,
}

/// <summary>When the outgoing clip reaches its exit pose and hands the figure over, and how.</summary>
/// <param name="ExitMs">Playhead ms the outgoing clip ends on its exit pose; the next action starts here.</param>
/// <param name="AvailableMs">From the recovery's start to <paramref name="ExitMs"/>.</param>
/// <param name="Fit">How the reservation was met.</param>
public readonly record struct HandoffPlan(float ExitMs, float AvailableMs, HandoffFit Fit);

/// <summary>
/// THE ACTION HANDOFF (ADR-011): an action whose blow has landed yields the figure to the next committed action
/// by ARRIVING at its exit pose, never by being cut mid-pose.
/// </summary>
/// <remarks>
/// <para>
/// An action is anticipation, commit, contact, follow-through, recovery. The first four are protected: they are
/// what the player reads as the action. The recovery (and the settle held on its exit pose) carries little
/// information, and it is the only phase the next action may borrow from. The replay already knows the next
/// action, so its reservation is known before the recovery starts: its IDEAL start (its full wind-up) and its
/// LATEST start (its wind-up at the tightest fit its own timing allows).
/// </para>
/// <para>
/// The outgoing recovery is fitted to end at the ideal start: the settle gives way first, then the recovery's
/// pace, down to a readable floor. Past the floor the next action starts later, inside its own elastic range.
/// Only when even its latest start is earlier than the floor is the recovery squeezed below it, and the plan
/// says so. The beat, the release and every piece of gameplay feedback stay where the fight put them.
/// </para>
/// <para>
/// Before this, the next action simply took the figure at its latest start: at a fast TEMPO the basic swing was
/// still in its low lunge (its follow-through), and the Seeker jumped in one frame to SPRAY's ready pose.
/// </para>
/// </remarks>
public static class ActionHandoff
{
    /// <summary>
    /// Plan the handoff, or null when the outgoing clip ends on its own before the next action wants the figure.
    /// </summary>
    /// <param name="nowMs">The playhead now (the outgoing blow has landed).</param>
    /// <param name="recoveryStartMs">When the outgoing clip's recovery begins (its protected phases end).</param>
    /// <param name="recoveryMotionMs">The recovery's motion at its own pace, without the settle hold.</param>
    /// <param name="recoveryFloorMs">The shortest the recovery may be and still be read (<see cref="ActionClipTiming.MinRecoveryMs"/>).</param>
    /// <param name="naturalEndMs">When the outgoing clip would end on its own, settle included.</param>
    /// <param name="idealStartMs">When the next action wants the figure for its full wind-up.</param>
    /// <param name="latestStartMs">The last moment the next action can start and keep its release on time.</param>
    /// <param name="incomingPerformed">
    /// The next action is PERFORMED (it has a minimum readable anticipation): past its latest start this one yields
    /// (<see cref="HandoffFit.Yielded"/>) instead of making it start late.
    /// </param>
    /// <summary>
    /// The earliest a clip that begins at <paramref name="startMs"/> can hand the figure over: every frame before its
    /// recovery whole, then its recovery at the readable floor (or its own pace, if that is shorter).
    /// </summary>
    /// <remarks>
    /// THE RESERVATION reads it: a clip whose earliest exit is after the next performed action's latest start
    /// cannot be shown whole AND leave that action its wind-up, so it is not begun (HuntScreen's `yield`).
    /// </remarks>
    public static float EarliestExit(float startMs, ActionClipTiming timing, float minPoseMs, float maxCompression)
        => !timing.HasMarker("recovery")
            ? startMs + timing.TotalMs
            : startMs + timing.MarkerMs("recovery")
              + Math.Min(timing.MinRecoveryMs(minPoseMs, maxCompression), timing.RecoveryMotionMs);

    public static HandoffPlan? Plan(float nowMs, float recoveryStartMs, float recoveryMotionMs, float recoveryFloorMs,
                                    float naturalEndMs, float idealStartMs, float latestStartMs, bool incomingPerformed = false)
    {
        if (naturalEndMs <= idealStartMs) return null;
        var start = Math.Max(nowMs, recoveryStartMs);
        var earliest = Math.Min(naturalEndMs, Math.Max(start, recoveryStartMs + Math.Min(recoveryFloorMs, recoveryMotionMs)));
        var exit = Math.Clamp(idealStartMs, earliest, naturalEndMs);
        var fit = exit >= recoveryStartMs + recoveryMotionMs ? HandoffFit.Natural
                : exit <= idealStartMs + 0.01f ? HandoffFit.Compressed
                : HandoffFit.Floor;
        if (exit > latestStartMs)
        {
            // a performed action behind this one keeps its wind-up: this one yields right after its contact (now),
            // even inside its protected frames; a plain one behind it starts late instead, as it always did
            exit = incomingPerformed ? Math.Max(latestStartMs, nowMs) : Math.Max(latestStartMs, start);
            fit = incomingPerformed && exit < start ? HandoffFit.Yielded : HandoffFit.Squeezed;
        }
        return new HandoffPlan(exit, Math.Max(0f, exit - recoveryStartMs), fit);
    }
}
