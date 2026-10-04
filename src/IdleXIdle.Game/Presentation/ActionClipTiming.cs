using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// A named point on one frame of an action clip — where a hand, a weapon tip or a foot IS on that frame —
/// and the direction a prop held there points.
/// </summary>
/// <param name="X">The point's x as a fraction of the square strip frame (0 = left edge, 1 = right edge).</param>
/// <param name="Y">The point's y as a fraction of the square strip frame (0 = top, 1 = bottom).</param>
/// <param name="AngleDegrees">Screen-space direction a held prop points: 0 = right, +90 = down.</param>
/// <remarks>
/// Stored against the FRAME, not the arena, so it survives every layout and scale: <see cref="ActorSocketMap"/>
/// turns it into an arena point with the same transform that draws the sprite. A later bone rig replaces how
/// a socket is looked up; the recipe that names it does not change (ADR-011).
/// </remarks>
public readonly record struct ActionSocket(float X, float Y, float AngleDegrees);

/// <summary>
/// AUTHORED TIMING for one action clip (ADR-011): how long each frame is held, where its key moments are,
/// and the sockets on its frames. Read from a small <c>.clip.json</c> beside the strip.
/// </summary>
/// <remarks>
/// <para>
/// Eight frames do not mean eight equal beats. A throw holds its coil, flashes through its release and eases
/// out of its follow-through; the old model gave every frame 125 ms and put contact on frame 5 of every clip
/// of every champion. A clip WITH a timing file is played by it; a clip without one keeps the old behaviour
/// exactly, so the game can move over one action at a time.
/// </para>
/// <para>
/// ELASTIC frames absorb tempo: when a fast build shortens the time before the beat, only they shrink (the
/// wait before the throw, the settle after it). The release and the contact frames never do.
/// </para>
/// </remarks>
public sealed class ActionClipTiming
{
    private readonly float[] _starts;
    private readonly Dictionary<(int Frame, string Name), ActionSocket> _sockets;

    /// <summary>How long each frame is held, in ms at normal speed.</summary>
    public IReadOnlyList<float> FrameMs { get; }

    /// <summary>Which frames may be shortened to make the clip fit a faster beat.</summary>
    public IReadOnlyList<bool> Elastic { get; }

    /// <summary>The named key moments (anticipation, release, contact, recovery, settle), by frame index.</summary>
    public IReadOnlyDictionary<string, int> Markers { get; }

    /// <summary>The clip's whole length, in ms.</summary>
    public float TotalMs { get; }

    /// <summary>How many frames the clip has.</summary>
    public int Frames => FrameMs.Count;

    /// <summary>
    /// How much of the last frame's duration is a SETTLE HOLD, in ms: the exit pose held still after its motion,
    /// before idle. Nothing moves in it, so a handoff trims it before it compresses any motion.
    /// </summary>
    public float HoldMs { get; }

    /// <summary>
    /// True when the timing file says <c>"place": "own"</c>: the strip keeps its OWN measured placement, and is not
    /// keyed onto the idle (see HuntScreen's <c>PlacedAs</c>).
    /// </summary>
    /// <remarks>
    /// An authored action strip drawn by keyposes.py is built on its champion's idle transform, so it is placed AS the
    /// idle. A strip that was generated whole (the basic attacks) has its own headroom and margins: keyed onto the idle
    /// it would draw at another scale and move the champion envelope every champion-side effect measures. Its timing
    /// file is DATA for the performance that reads it, not a request to re-place the figure.
    /// </remarks>
    public bool PlaceOwn { get; private init; }

    /// <summary>A timing from its parts. Every frame must last at least 1 ms.</summary>
    public ActionClipTiming(IReadOnlyList<float> frameMs, IReadOnlyList<bool>? elastic = null,
                            IReadOnlyDictionary<string, int>? markers = null,
                            IReadOnlyDictionary<(int Frame, string Name), ActionSocket>? sockets = null,
                            float holdMs = 0f)
        : this(frameMs, elastic, markers, sockets, holdMs, allowSkipped: false)
    {
    }

    // allowSkipped: a handoff (FitRecovery) may give an intermediate recovery pose 0 ms, which FrameAt never shows
    private ActionClipTiming(IReadOnlyList<float> frameMs, IReadOnlyList<bool>? elastic,
                             IReadOnlyDictionary<string, int>? markers,
                             IReadOnlyDictionary<(int Frame, string Name), ActionSocket>? sockets,
                             float holdMs, bool allowSkipped)
    {
        if (frameMs is null || frameMs.Count == 0) throw new ArgumentException("A clip needs at least one frame.", nameof(frameMs));
        if (frameMs.Any(ms => !(ms >= (allowSkipped ? 0f : 1f)))) throw new ArgumentException("Every frame must last at least 1 ms.", nameof(frameMs));
        if (!(holdMs >= 0f) || holdMs > frameMs[^1]) throw new ArgumentException("The settle hold is part of the last frame.", nameof(holdMs));
        HoldMs = holdMs;
        FrameMs = frameMs.ToArray();
        Elastic = elastic is { } e && e.Count == frameMs.Count ? e.ToArray() : new bool[frameMs.Count];
        Markers = new Dictionary<string, int>(markers ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase);
        foreach (var (name, f) in Markers)
            if (f < 0 || f >= frameMs.Count) throw new ArgumentException($"Marker '{name}' names frame {f} of {frameMs.Count}.", nameof(markers));
        _sockets = new Dictionary<(int, string), ActionSocket>(sockets ?? new Dictionary<(int, string), ActionSocket>());
        _starts = new float[frameMs.Count + 1];
        for (var i = 0; i < frameMs.Count; i++) _starts[i + 1] = _starts[i] + frameMs[i];
        TotalMs = _starts[^1];
    }

    /// <summary>The old model: <paramref name="frames"/> equal frames, contact on frame 5 of 8.</summary>
    public static ActionClipTiming Uniform(int frames, float frameMs, int contactFrame)
        => new(Enumerable.Repeat(frameMs, frames).ToArray(), markers: new Dictionary<string, int> { ["contact"] = contactFrame });

    /// <summary>
    /// A PLAIN clip (a strip with no timing file) as the champion plays it: eight equal frames of
    /// <paramref name="frameMs"/>, contact on frame 5, the follow-through on 6, the RECOVERY and exit pose on 7,
    /// and <paramref name="settleMs"/> of settle held on that exit pose.
    /// </summary>
    /// <remarks>
    /// The same pictures the old continuous clock drew. What it adds is the phases, so a handoff knows what it
    /// may borrow: nothing up to the follow-through, the recovery's pace, and the settle's stillness.
    /// </remarks>
    public static ActionClipTiming Plain(float frameMs, float settleMs)
    {
        var ms = Enumerable.Repeat(frameMs, 8).ToArray();
        ms[^1] += Math.Max(0f, settleMs);
        return new ActionClipTiming(ms, markers: new Dictionary<string, int> { ["contact"] = 5, ["recovery"] = 7 },
                                    holdMs: Math.Max(0f, settleMs));
    }

    /// <summary>
    /// The recovery's MOTION at normal pace, in ms: the frames from the <c>recovery</c> marker to the end, less the
    /// settle hold. Zero when the clip names no recovery.
    /// </summary>
    public float RecoveryMotionMs => HasMarker("recovery") ? TotalMs - MarkerMs("recovery") - HoldMs : 0f;

    /// <summary>
    /// The shortest the recovery may become and still be read: each of its poses on screen for at least
    /// <paramref name="minPoseMs"/>, and none played more than <paramref name="maxCompression"/> times faster.
    /// </summary>
    public float MinRecoveryMs(float minPoseMs, float maxCompression)
    {
        if (!HasMarker("recovery")) return 0f;
        var from = Markers["recovery"];
        var sum = 0f;
        for (var i = from; i < Frames; i++)
        {
            var motion = i == Frames - 1 ? FrameMs[i] - HoldMs : FrameMs[i];
            sum += Math.Max(minPoseMs, motion / Math.Max(1f, maxCompression));
        }
        return sum;
    }

    /// <summary>
    /// THE HANDOFF (ADR-011): this timing with its recovery, from the <c>recovery</c> marker to the end, fitted into
    /// <paramref name="availableMs"/>. Every frame before the marker keeps its length.
    /// </summary>
    /// <param name="availableMs">From the recovery's start to the moment the next action takes the figure.</param>
    /// <param name="minPoseMs">The shortest a recovery pose may be on screen and still be read.</param>
    /// <param name="compression">How much faster than authored the recovery's motion plays (1 = not at all).</param>
    /// <remarks>
    /// <para>
    /// Borrowed in the order of what it costs the eye: first the settle hold (the exit pose standing still),
    /// then the recovery's pace. The exit pose, the last frame, is always shown, so the next action begins
    /// from the pose the recovery was travelling to.
    /// </para>
    /// <para>
    /// When the poses cannot all get <paramref name="minPoseMs"/>, the exit pose is kept first, then the first
    /// recovery pose, then the intermediates evenly: a pose flashed for one display frame reads as a pop, and a
    /// skipped intermediate reads as a faster recovery.
    /// </para>
    /// </remarks>
    public ActionClipTiming FitRecovery(float availableMs, float minPoseMs, out float compression)
    {
        compression = 1f;
        if (!HasMarker("recovery")) return this;
        var from = Markers["recovery"];
        var n = Frames - from;
        var available = Math.Max(0f, availableMs);
        var motion = new float[n];
        for (var i = 0; i < n; i++) motion[i] = FrameMs[from + i] - (i == n - 1 ? HoldMs : 0f);
        var motionMs = motion.Sum();
        var ms = FrameMs.ToArray();
        if (available >= motionMs + HoldMs) return this;               // nothing to borrow
        if (available >= motionMs)
        {
            // only the settle hold gives way: the recovery plays at its own pace and the exit pose holds less
            ms[^1] = motion[n - 1] + (available - motionMs);
            return new ActionClipTiming(ms, Elastic, Markers, _sockets, ms[^1] - motion[n - 1], allowSkipped: false) { PlaceOwn = PlaceOwn };
        }
        compression = motionMs / Math.Max(1e-3f, available);

        // which poses fit at the readable minimum: the exit first, then the first recovery pose, then intermediates
        var fit = Math.Clamp((int)(available / Math.Max(1f, minPoseMs)), 1, n);
        var keep = new bool[n];
        keep[n - 1] = true;
        if (fit >= 2) keep[0] = true;
        for (var k = 1; k <= fit - 2; k++) keep[(int)MathF.Round(k * (n - 1) / (float)(fit - 1))] = true;

        // share the time among the kept poses by their authored motion, none under the minimum (water-filling)
        var share = new float[n];
        var free = Enumerable.Range(0, n).Where(i => keep[i]).ToList();
        var left = available;
        while (free.Count > 0)
        {
            var weight = free.Sum(i => motion[i]);
            var floorHit = free.Where(i => weight > 0f && left * motion[i] / weight < minPoseMs).ToList();
            if (floorHit.Count == 0 || floorHit.Count == free.Count)
            {
                foreach (var i in free) share[i] = weight > 0f ? left * motion[i] / weight : left / free.Count;
                break;
            }
            foreach (var i in floorHit) { share[i] = Math.Min(minPoseMs, left); left -= share[i]; free.Remove(i); }
        }
        for (var i = 0; i < n; i++) ms[from + i] = keep[i] ? Math.Max(0f, share[i]) : 0f;
        if (ms[^1] < 1f) ms[^1] = 1f;                                   // the exit pose is always drawn
        return new ActionClipTiming(ms, Elastic, Markers, _sockets, 0f, allowSkipped: true) { PlaceOwn = PlaceOwn };
    }

    /// <summary>
    /// This timing ending at <paramref name="ms"/> into the clip: the frame showing then is its last, shortened, and
    /// every frame after it is never shown. A YIELD (<see cref="HandoffFit.Yielded"/>): the outgoing action hands the
    /// figure over right after its contact, so the next performed action keeps its readable wind-up.
    /// </summary>
    public ActionClipTiming CutAt(float ms)
    {
        if (ms >= TotalMs) return this;
        var cut = Math.Max(1f, ms);
        var frame = FrameAt(cut);
        var frames = FrameMs.ToArray();
        frames[frame] = Math.Max(1f, cut - _starts[frame]);
        for (var i = frame + 1; i < frames.Length; i++) frames[i] = 0f;
        return new ActionClipTiming(frames, Elastic, Markers, _sockets, 0f, allowSkipped: true) { PlaceOwn = PlaceOwn };
    }

    /// <summary>When <paramref name="frame"/> begins, in ms from the clip's start.</summary>
    public float StartOf(int frame) => _starts[Math.Clamp(frame, 0, Frames)];

    /// <summary>True when the clip names this key moment.</summary>
    public bool HasMarker(string marker) => Markers.ContainsKey(marker);

    /// <summary>When the named key moment begins, in ms from the clip's start.</summary>
    public float MarkerMs(string marker)
        => Markers.TryGetValue(marker, out var f) ? StartOf(f) : throw new KeyNotFoundException($"The clip has no '{marker}' marker.");

    /// <summary>The frame showing at <paramref name="ms"/> into the clip; before 0 is frame 0, past the end the last.</summary>
    public int FrameAt(float ms)
    {
        if (ms <= 0f) return 0;
        for (var i = 0; i < Frames; i++)
            if (ms < _starts[i + 1]) return i;
        return Frames - 1;
    }

    /// <summary>The socket named <paramref name="name"/> on <paramref name="frame"/>, if that frame authors it.</summary>
    public ActionSocket? Socket(int frame, string name)
        => _sockets.TryGetValue((frame, name), out var s) ? s : null;

    /// <summary>Every frame that authors the socket named <paramref name="name"/>, in order.</summary>
    public IEnumerable<int> FramesWithSocket(string name)
        => _sockets.Keys.Where(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase)).Select(k => k.Frame).OrderBy(f => f);

    /// <summary>
    /// This timing made to reach <paramref name="marker"/> within <paramref name="availableMs"/>: only the elastic
    /// frames before it shrink, and never below <paramref name="minimumShare"/> of their length. Returns this
    /// timing when it already fits.
    /// </summary>
    public ActionClipTiming FitBefore(string marker, float availableMs, float minimumShare = 0.35f)
    {
        var at = MarkerMs(marker);
        if (availableMs >= at) return this;
        var m = Markers[marker];
        float elastic = 0f, rigid = 0f;
        for (var i = 0; i < m; i++)
            if (Elastic[i]) elastic += FrameMs[i]; else rigid += FrameMs[i];
        if (elastic <= 0f) return this;
        var k = Math.Clamp((availableMs - rigid) / elastic, Math.Clamp(minimumShare, 0.05f, 1f), 1f);
        var ms = FrameMs.ToArray();
        for (var i = 0; i < m; i++)
            if (Elastic[i]) ms[i] = Math.Max(1f, ms[i] * k);
        return new ActionClipTiming(ms, Elastic, Markers, _sockets, HoldMs) { PlaceOwn = PlaceOwn };
    }

    /// <summary>
    /// Parse a <c>.clip.json</c>:
    /// <c>{"frameMs":[..], "elastic":[..], "markers":{"release":3}, "sockets":{"3":{"ThrowHand":[x,y,deg]}}}</c>,
    /// with an optional <c>"place": "own"</c> (<see cref="PlaceOwn"/>). Any other key (a "source" block) is ignored.
    /// </summary>
    public static ActionClipTiming Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;
        var frames = root.GetProperty("frameMs").EnumerateArray().Select(e => e.GetSingle()).ToArray();
        var elastic = root.TryGetProperty("elastic", out var el) ? el.EnumerateArray().Select(e => e.GetBoolean()).ToArray() : null;
        var markers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("markers", out var mk))
            foreach (var p in mk.EnumerateObject()) markers[p.Name] = p.Value.GetInt32();
        var sockets = new Dictionary<(int, string), ActionSocket>();
        if (root.TryGetProperty("sockets", out var sk))
            foreach (var frame in sk.EnumerateObject())
                foreach (var s in frame.Value.EnumerateObject())
                {
                    var v = s.Value.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                    sockets[(int.Parse(frame.Name), s.Name)] = new ActionSocket(v[0], v[1], v.Length > 2 ? v[2] : 0f);
                }
        var placeOwn = root.TryGetProperty("place", out var place) && place.ValueKind == JsonValueKind.String
                       && string.Equals(place.GetString(), "own", StringComparison.OrdinalIgnoreCase);
        return new ActionClipTiming(frames, elastic, markers, sockets) { PlaceOwn = placeOwn };
    }
}
