using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Animation;

/// <summary>Easing curves. Deliberately few — a cutout rig does not need a curve editor.</summary>
public enum Easing
{
    Linear,

    /// <summary>Slow out, fast in. The windup of a strike: weight gathering.</summary>
    EaseIn,

    /// <summary>Fast out, slow in. The recovery of a strike: weight settling.</summary>
    EaseOut,

    /// <summary>Fast through the middle. The strike itself — the frames the player must read.</summary>
    EaseInOut,
}

/// <summary>One bone's angle at one moment in a clip.</summary>
public readonly record struct Keyframe(float TimeSeconds, float Angle, Easing Easing = Easing.Linear);

/// <summary>
/// A named animation: per-bone keyframed angles over a duration.
/// </summary>
/// <remarks>
/// The telegraph contract lives here as much as in the AI. A creature's windup is the player's ONLY
/// warning, so a strike clip's shape is a gameplay-critical value, not decoration: the windup must be
/// long enough to read (600 ms — the retired Telegraph model's floor, from WCAG 2.3.1
/// anti-strobe limits plus Hick's Law) and the strike itself must be visually distinct from it.
/// </remarks>
public sealed class Clip
{
    public required string Name { get; init; }
    public required float DurationSeconds { get; init; }
    public bool Loops { get; init; }

    /// <summary>Keyframes per bone id. Each track must be sorted by time.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<Keyframe>> Tracks { get; init; }

    /// <summary>Sample the clip, producing a pose the rig can evaluate.</summary>
    public Dictionary<string, float> Sample(float timeSeconds)
    {
        var t = Loops
            ? (DurationSeconds <= 0f ? 0f : Mod(timeSeconds, DurationSeconds))
            : Math.Clamp(timeSeconds, 0f, DurationSeconds);

        var pose = new Dictionary<string, float>(Tracks.Count);

        foreach (var (boneId, track) in Tracks)
        {
            if (track.Count == 0) continue;
            pose[boneId] = SampleTrack(track, t);
        }

        return pose;
    }

    private static float SampleTrack(IReadOnlyList<Keyframe> track, float t)
    {
        if (t <= track[0].TimeSeconds) return track[0].Angle;

        var last = track[^1];
        if (t >= last.TimeSeconds) return last.Angle;

        for (var i = 0; i < track.Count - 1; i++)
        {
            var a = track[i];
            var b = track[i + 1];
            if (t < a.TimeSeconds || t > b.TimeSeconds) continue;

            var span = b.TimeSeconds - a.TimeSeconds;
            if (span <= 0f) return b.Angle;

            var u = (t - a.TimeSeconds) / span;
            // The easing of the segment is owned by the keyframe it departs FROM.
            return Lerp(a.Angle, b.Angle, Ease(u, a.Easing));
        }

        return last.Angle;
    }

    private static float Ease(float u, Easing easing) => easing switch
    {
        Easing.Linear => u,
        Easing.EaseIn => u * u,
        Easing.EaseOut => 1f - (1f - u) * (1f - u),
        Easing.EaseInOut => u < 0.5f ? 2f * u * u : 1f - MathF.Pow(-2f * u + 2f, 2f) / 2f,
        _ => u,
    };

    private static float Lerp(float a, float b, float u) => a + (b - a) * u;

    private static float Mod(float a, float m)
    {
        var r = a % m;
        return r < 0f ? r + m : r;
    }

    /// <summary>
    /// The Attacker strike arc — the spike case.
    /// </summary>
    /// <remarks>
    /// This is the widest motion arc in the roster and therefore the worst case for pixel-art rotation
    /// artifacts. systems-index.md's risk register requires it be validated BEFORE the creature roster
    /// is committed to this rig. Three beats:
    ///
    ///   0.00-0.60s  WINDUP   — the arm draws back and holds. Eased IN (weight gathering). This is the
    ///                          player's entire warning, and it is exactly the 600 ms anti-strobe floor.
    ///   0.60-0.75s  STRIKE   — a fast sweep through ~170 degrees. Eased IN-OUT so the readable frames
    ///                          land at the extremes, not smeared through the middle.
    ///   0.75-1.10s  RECOVER  — settles back to rest. Eased OUT (weight dissipating).
    ///
    /// If angle-snapping is going to look bad anywhere, it will look bad in the STRIKE beat.
    /// </remarks>
    public static Clip AttackerStrike() => new()
    {
        Name = "attacker_strike",
        DurationSeconds = 1.10f,
        Loops = false,
        Tracks = new Dictionary<string, IReadOnlyList<Keyframe>>
        {
            ["upper_arm"] = new List<Keyframe>
            {
                new(0.00f, 0.00f, Easing.EaseIn),
                new(0.60f, -1.20f, Easing.EaseInOut),  // drawn back and held
                new(0.75f, 1.75f, Easing.EaseOut),     // ~170 degrees of sweep
                new(1.10f, 0.00f),
            },
            // The forearm's relative angle is the ELBOW. Zero is a straight arm, because the art draws
            // the forearm continuing the upper arm's line. So the strike keyframe must be near zero:
            // the arm EXTENDS into the blow. It used to be +0.60 — the elbow folded shut at the moment
            // of impact, which parked the hand (and the sword in it) beside the character's own face.
            // Cocked at the windup, straight at the strike, is both correct and what reads.
            ["forearm"] = new List<Keyframe>
            {
                new(0.00f, 0.00f, Easing.EaseIn),
                new(0.60f, -0.95f, Easing.EaseInOut),   // cocked back
                new(0.75f, -0.10f, Easing.EaseOut),     // extended through the blow
                new(1.10f, 0.00f),
            },
            ["torso"] = new List<Keyframe>
            {
                new(0.00f, 0.00f, Easing.EaseIn),
                new(0.60f, -0.18f, Easing.EaseInOut),  // counter-rotation sells the weight
                new(0.75f, 0.22f, Easing.EaseOut),
                new(1.10f, 0.00f),
            },
        },
    };

    /// <summary>A slow breathing idle, so a creature at rest never looks like a static image.</summary>
    public static Clip Idle() => new()
    {
        Name = "idle",
        DurationSeconds = 2.4f,
        Loops = true,
        Tracks = new Dictionary<string, IReadOnlyList<Keyframe>>
        {
            ["torso"] = new List<Keyframe>
            {
                new(0.0f, 0.00f, Easing.EaseInOut),
                new(1.2f, 0.05f, Easing.EaseInOut),
                new(2.4f, 0.00f),
            },
            ["upper_arm"] = new List<Keyframe>
            {
                new(0.0f, 0.00f, Easing.EaseInOut),
                new(1.2f, 0.08f, Easing.EaseInOut),
                new(2.4f, 0.00f),
            },
        },
    };
}
