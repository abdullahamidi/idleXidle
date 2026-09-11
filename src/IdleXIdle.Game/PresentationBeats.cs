using System;
using System.Collections.Generic;
using IdleXIdle.Core.Expeditions;

namespace IdleXIdle.Game;

/// <summary>
/// THE REPLAY BARRIER, as arithmetic: where the playhead may go when a beat has to be talked about
/// before any of it is shown.
/// </summary>
/// <remarks>
/// <para>
/// Pure, so the rule can be proved against a real wave's own events without a graphics device. The
/// HUNT screen owns the playhead and asks this where it may go; nothing here knows what a tutorial
/// is — it knows a kind of event, a wind-up, and a millisecond.
/// </para>
/// <para>
/// <b>Short of the PRESENTATION, not only of the event.</b> A cast's clip starts one contact-length
/// ahead of its event, so the event lands on the frame the blow connects. A barrier that parked one
/// millisecond before the event therefore froze the Hunter in the last frames of the cast, under the
/// very card that was meant to come first (playtest 2026-09-11: "the Signature Skill starts at the same
/// time the tutorial appears"). The park now sits ahead of the wind-up as well.
/// </para>
/// </remarks>
public static class ReplayBarrier
{
    /// <summary>Where the playhead goes this frame, and which beat it is being held short of.</summary>
    /// <param name="PlayheadMs">The playhead after this frame's advance.</param>
    /// <param name="HeldAtMs">The held event's own timestamp, or null when nothing was held back.</param>
    public readonly record struct Step(float PlayheadMs, int? HeldAtMs)
    {
        /// <summary>Did the barrier take anything off this frame's advance?</summary>
        public bool Held => HeldAtMs is not null;
    }

    /// <summary>
    /// Advance toward <paramref name="freeMs"/>, stopping short of the next event of
    /// <paramref name="kind"/> and of the presentation that leads into it.
    /// </summary>
    /// <param name="replay">The wave being watched.</param>
    /// <param name="playheadMs">Where the playhead stands now.</param>
    /// <param name="freeMs">Where it would stand after this frame with no barrier at all.</param>
    /// <param name="kind">The kind of beat being held back, or null to run freely.</param>
    /// <param name="leadMs">For an event at a given millisecond, how long before it its presentation starts.</param>
    /// <returns>The clamped playhead, and the held event's timestamp when the clamp bit.</returns>
    public static Step Advance(WaveReplay replay, float playheadMs, float freeMs, BattleEventKind? kind,
                               Func<int, float> leadMs)
    {
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentNullException.ThrowIfNull(leadMs);
        if (kind is not { } k) return new Step(freeMs, null);

        var at = replay.NextEventOfKindAfter(playheadMs, k);
        if (at == int.MaxValue) return new Step(freeMs, null);

        // Events cross on `AtMs <= toMs` (WaveReplay.Advance), so a park at least one millisecond short
        // of the event provably never crosses it.
        var park = at - MathF.Max(0f, leadMs(at)) - 1f;
        // NEVER BACKWARDS. A playhead already inside the lead window stays where it stands: rewinding
        // would play the player something they have already seen, a second time.
        var to = MathF.Max(playheadMs, MathF.Min(freeMs, park));
        return to < freeMs ? new Step(to, at) : new Step(to, null);
    }
}

/// <summary>
/// THE CLEAR, as arithmetic: when a cleared wave has finished being SHOWN, and whether the break
/// between waves may hand the stage to the next pack.
/// </summary>
/// <remarks>
/// A wave pays on the frame its last creature dies — correct for the economy, and far too early for a
/// sentence about it. What the player has SEEN is the fall played through, the body lying and fading,
/// and the haul risen and gone; these two functions are the whole of that rule, kept apart from the
/// screen so the ordering can be proved frame by frame.
/// </remarks>
public static class ClearBeat
{
    /// <summary>Where a held break rests: above zero, so "is the stage between waves?" still says yes.</summary>
    public const float Resting = 1e-4f;

    /// <summary>Has every one of these falls played through, lain and faded by <paramref name="now"/>?</summary>
    /// <param name="now">The screen's animation clock, in seconds.</param>
    /// <param name="diedAt">When each fallen creature died, on the same clock.</param>
    /// <param name="fallSeconds">A fall's whole length: its clip, the body lying there, and the fade.</param>
    public static bool FallsPlayed(float now, IEnumerable<float> diedAt, float fallSeconds)
    {
        ArgumentNullException.ThrowIfNull(diedAt);
        foreach (var at in diedAt)
            if (now - at < fallSeconds) return false;
        return true;
    }

    /// <summary>
    /// One frame of the between-wave break. True on the one frame the next wave may begin.
    /// </summary>
    /// <param name="breakTimer">The break's countdown, in seconds; positive while the stage is between waves.</param>
    /// <param name="dt">This frame's length, in seconds.</param>
    /// <param name="holdNext">Keep the next wave off the stage: the break plays out, then rests on its last instant.</param>
    public static bool Tick(ref float breakTimer, float dt, bool holdNext)
    {
        breakTimer -= dt;
        if (breakTimer > 0f) return false;
        if (!holdNext) return true;
        breakTimer = Resting;
        return false;
    }
}
