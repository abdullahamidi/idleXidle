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

/// <summary>
/// THE FALL, as arithmetic: how black the stage is at each instant of a death and of the descent that
/// begins under it.
/// </summary>
/// <remarks>
/// <para>
/// Falling is part of the idle loop, so a routine death is not news to be read off a plate: the Hunter
/// falls and is seen to have fallen, the stage fades to black, the next descent begins under the black,
/// and the stage comes back with the Hunter already standing in it. Nothing here moves WHEN the descent
/// restarts — <see cref="Descent.DownedSeconds"/> is spent as time by the offline simulation and the
/// Dust for a checkpoint is charged on the restart frame — so the fade-out sits inside that beat and
/// the restart lands on the frame it always did, at full black.
/// </para>
/// <para>
/// Pure, so the timeline can be stepped frame by frame in a test and posed by the capture rig from one
/// dial. The HUNT screen owns the two clocks and asks this what to paint; under Reduced Motion both
/// fades are cuts to the same end state.
/// </para>
/// </remarks>
public static class DeathTransition
{
    /// <summary>The fade to black: the LAST part of the downed beat, so the restart lands at full black.</summary>
    public const float FadeOut = UiMotion.Reward;

    /// <summary>Black held after the restart, so the reset itself is never seen.</summary>
    public const float Hold = UiMotion.Fast;

    /// <summary>The lift, over the new wave's first frames — its entrance is already walking in underneath.</summary>
    public const float FadeIn = UiMotion.Reward;

    /// <summary>
    /// What the fade-in clock is armed to on the restart frame: the hold and then the lift — or, under
    /// Reduced Motion, the hold alone, so the cut back ends the transition where the lift would have begun.
    /// </summary>
    public static float FadeInSeconds(bool reduced) => reduced ? Hold : Hold + FadeIn;

    /// <summary>How black the stage is, 0..1.</summary>
    /// <param name="downed">Is the champion in the downed beat?</param>
    /// <param name="downedTimer">Seconds left of the downed beat; the descent restarts the frame it reaches zero.</param>
    /// <param name="fadeInClock">Seconds left of the fade-in, counting down from <see cref="FadeInSeconds"/> on the restart frame.</param>
    /// <param name="reduced">Reduced Motion: a cut to black where the fade would start, and a cut back after the hold.</param>
    public static float Alpha(bool downed, float downedTimer, float fadeInClock, bool reduced)
    {
        if (downed)
        {
            if (downedTimer > FadeOut) return 0f;   // readable: the clip plays and the body lies there
            if (reduced) return 1f;
            return UiMotion.Smooth(1f - Math.Clamp(downedTimer / FadeOut, 0f, 1f));
        }
        if (fadeInClock <= 0f) return 0f;
        if (reduced || fadeInClock >= FadeIn) return 1f;   // the hold
        return UiMotion.Smooth(Math.Clamp(fadeInClock / FadeIn, 0f, 1f));
    }

    /// <summary>Does the transition own the stage — the downed beat, or the black and the lift after it?</summary>
    /// <remarks>
    /// The fact the host's lesson card and coach spotlight yield to: nothing is SAID over a fall from its
    /// first frame to the lift's last. Input is a narrower question — see <see cref="Covers"/>.
    /// </remarks>
    public static bool Up(bool downed, float fadeInClock) => downed || fadeInClock > 0f;

    /// <summary>Is the black covering the stage — does the screen's own HUD refuse input under it?</summary>
    /// <remarks>
    /// Exactly while the fill is visible, from the fade-out's first frame to the lift's last, and not a
    /// frame longer either side: "nothing clickable paints when its input is blocked" cuts both ways, so
    /// the readable phase — the medallion painted beside a fallen Hunter — is as live as any other frame.
    /// One rule for both halves (ADR-006): the paint and the refusal read the same alpha.
    /// </remarks>
    public static bool Covers(float alpha) => alpha > 0f;

    /// <summary>One instant of the transition, for the capture rig: which clock is running and where it stands.</summary>
    /// <param name="Restarted">Has the next descent begun — is the instant past the restart frame?</param>
    /// <param name="DownedTimer">Where the downed beat stands, before the restart.</param>
    /// <param name="FadeInClock">Where the fade-in stands, after it.</param>
    /// <param name="SinceRestart">Seconds since the restart, for whatever else began on that frame (the new wave's entrance).</param>
    public readonly record struct Pose(bool Restarted, float DownedTimer, float FadeInClock, float SinceRestart);

    /// <summary>
    /// The rig's one dial over the transition, linear in seconds: 0 the last readable instant before the
    /// fade, 0.5 the black with the next descent already begun beneath it, 1 the stage back.
    /// </summary>
    /// <remarks>
    /// The dial spans the transition's own window — from the fade's first frame to the frame the lift
    /// ends — whether or not Reduced Motion is on, so the same instant photographs the fade and the cut
    /// side by side. Under Reduced Motion the lift's instants are simply the stage, back.
    /// </remarks>
    public static Pose At(float t, bool reduced)
    {
        // THE DIAL'S ENDS ARE EXACT. The downed half counts forward from 0, so the first stop is the
        // fade's own first instant (the downed timer at FadeOut, alpha zero); the lift counts what is
        // LEFT of the window, so the last stop is a clock at exactly zero and not a float's residue of
        // one — at 1 the transition is over, and Up says so.
        var lift = Hold + FadeIn;
        var span = FadeOut + lift;
        var seconds = Math.Clamp(t, 0f, 1f) * span;
        if (seconds < FadeOut) return new Pose(false, FadeOut - seconds, 0f, 0f);
        var remaining = span - seconds;
        var since = lift - remaining;
        return new Pose(true, 0f, Math.Min(remaining, Math.Max(0f, FadeInSeconds(reduced) - since)), since);
    }
}
