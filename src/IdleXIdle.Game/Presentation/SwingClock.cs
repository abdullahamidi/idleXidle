using System;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// THE SWING CLOCK (design.md 5.18-5.27, the P1.3 decision): THE PLAIN ENVELOPE OWNS TIME, THE AUTHORED FILE OWNS THE
/// FRAMES. Pure: every answer is a function of its arguments, and nothing allocates.
/// </summary>
/// <remarks>
/// <para>
/// A basic attack is committed exactly as it always was: the plain clip's start, speed, handoff (PlanHandoff,
/// FitRecovery, CutAt) and yield are untouched, so every clip-start, handoff, yield and clip-end line, and with them the
/// starts of the performed actions behind a swing (SPRAY's, at a fast TEMPO, already depends on the outgoing swing's
/// Floor exit), cannot move. What changes is only WHICH authored frame is shown inside that window: the frames before
/// the anchor are laid into [start, anchor time], the anchor and the frames after it into [anchor time, planned exit].
/// The anchor time is the beat less the swing's travel, so the contact frame lands ON the beat.
/// </para>
/// <para>
/// Inside each half the authored durations are kept where they fit. Time to spare goes to the ELASTIC frames, by their
/// authored length (a slow wind-up before the blow); time short is taken from the elastic frames first, then from the
/// rigid ones by their length. After the anchor, spare time is held on the last frame, as the plain clip's settle is.
/// </para>
/// </remarks>
public static class SwingClock
{
    /// <summary>
    /// The step-in is home this long before the planned exit (two display frames), so the last frame drawn before the
    /// clip ends already stands home.
    /// </summary>
    public const float HomeMarginMs = 2000f / 60f;

    /// <summary>When the anchor frame begins, in playhead ms: the anchor time, never before the clip's start.</summary>
    public static float AnchorStart(float startMs, float anchorAtMs) => Math.Max(startMs, anchorAtMs);

    /// <summary>How long <paramref name="frame"/> is shown, in ms, for the swing's window.</summary>
    /// <param name="timing">The authored timing (the <c>.clip.json</c>).</param>
    /// <param name="anchor">The anchor frame (contact or release).</param>
    /// <param name="startMs">The plain envelope's start (playhead ms).</param>
    /// <param name="anchorAtMs">When the anchor frame must begin: the beat less the travel.</param>
    /// <param name="exitMs">The plain envelope's planned exit (its start plus its current, possibly handed-off, length).</param>
    /// <param name="frame">The frame asked about.</param>
    public static float FrameMs(ActionClipTiming timing, int anchor, float startMs, float anchorAtMs, float exitMs, int frame)
    {
        if (frame < 0 || frame >= timing.Frames) return 0f;
        var a = AnchorStart(startMs, anchorAtMs);
        return frame < anchor
            ? Share(timing, 0, anchor, a - startMs, frame, holdLast: false)
            : Share(timing, anchor, timing.Frames, Math.Max(0f, exitMs - a), frame, holdLast: true);
    }

    /// <summary>When <paramref name="frame"/> begins, in playhead ms (the clip's end for <c>frame == Frames</c>).</summary>
    public static float FrameStart(ActionClipTiming timing, int anchor, float startMs, float anchorAtMs, float exitMs, int frame)
    {
        var last = Math.Clamp(frame, 0, timing.Frames);
        // the anchor's start is the anchor time EXACTLY (no float sum stands between the beat and the contact frame)
        var from = last >= anchor ? anchor : 0;
        var t = last >= anchor ? AnchorStart(startMs, anchorAtMs) : startMs;
        for (var i = from; i < last; i++) t += FrameMs(timing, anchor, startMs, anchorAtMs, exitMs, i);
        return t;
    }

    /// <summary>
    /// The authored frame showing at <paramref name="playheadMs"/>: frame 0 before the start, the last shown frame past
    /// the exit. The anchor frame begins exactly at <see cref="AnchorStart"/>.
    /// </summary>
    public static int FrameAt(ActionClipTiming timing, int anchor, float startMs, float anchorAtMs, float exitMs, float playheadMs)
    {
        if (playheadMs <= startMs) return 0;
        anchor = Math.Clamp(anchor, 0, timing.Frames - 1);
        var a = AnchorStart(startMs, anchorAtMs);
        // before the anchor time: the wind-up's frames; from it on: the anchor and the rest (the anchor is never skipped)
        var pre = playheadMs < a;
        var t = pre ? startMs : a;
        var shown = pre ? 0 : anchor;
        for (var i = pre ? 0 : anchor; i < (pre ? anchor : timing.Frames); i++)
        {
            var d = FrameMs(timing, anchor, startMs, anchorAtMs, exitMs, i);
            if (d <= 0f) continue;
            if (playheadMs < t + d) return i;
            t += d;
            shown = i;
        }
        return shown;
    }

    /// <summary>
    /// THE STEP-IN (melee): a draw offset in px, read from the playhead (never a frame's dt). 0 until the commit frame,
    /// rising (accelerating into the hit) to <paramref name="peakPx"/> at the anchor time, eased back to 0 by the settle
    /// frame, and 0 from <see cref="HomeMarginMs"/> before the planned exit on, whatever the handoff did to that exit.
    /// <paramref name="power"/> shapes the rise (u^power; 2 is the default ease-in): a higher power holds the planted
    /// stance longer and puts the translation into the last moments before the hit, under the thrust (the Thornwall).
    /// </summary>
    public static float StepIn(ActionClipTiming timing, int anchor, int commit, int settle, float startMs, float anchorAtMs,
                               float exitMs, float peakPx, float playheadMs, float power = 2f)
    {
        if (peakPx <= 0f || playheadMs >= exitMs) return 0f;
        var a = AnchorStart(startMs, anchorAtMs);
        var from = Math.Min(FrameStart(timing, anchor, startMs, anchorAtMs, exitMs, Math.Min(commit, anchor)), a);
        if (playheadMs <= from) return 0f;
        if (playheadMs <= a)
        {
            var u = a - from <= 0f ? 1f : (playheadMs - from) / (a - from);
            return power == 2f ? peakPx * u * u : peakPx * MathF.Pow(u, power);
        }
        var home = Math.Min(FrameStart(timing, anchor, startMs, anchorAtMs, exitMs, Math.Max(settle, anchor)), exitMs - HomeMarginMs);
        if (playheadMs >= home) return 0f;
        var v = (playheadMs - a) / Math.Max(1e-3f, home - a);
        return peakPx * (1f - v * v * (3f - 2f * v));
    }

    // the share of `window` frame `frame` of [from, to) takes (see the remarks above)
    private static float Share(ActionClipTiming timing, int from, int to, float window, int frame, bool holdLast)
    {
        if (to <= from) return 0f;
        float rigid = 0f, elastic = 0f;
        for (var i = from; i < to; i++)
            if (timing.Elastic[i]) elastic += timing.FrameMs[i]; else rigid += timing.FrameMs[i];
        var own = timing.FrameMs[frame];
        var isElastic = timing.Elastic[frame];
        window = Math.Max(0f, window);
        if (window >= rigid + elastic)
        {
            var spare = window - rigid - elastic;
            if (holdLast) return frame == to - 1 ? own + spare : own;
            if (elastic > 0f) return isElastic ? own + spare * own / elastic : own;
            return frame == from ? own + spare : own;   // nothing elastic: the opening pose holds the spare
        }
        if (window >= rigid)
            return isElastic ? (elastic > 0f ? (window - rigid) * own / elastic : 0f) : own;
        return isElastic ? 0f : (rigid > 0f ? window * own / rigid : 0f);
    }
}
