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

    /// <summary>A timing from its parts. Every frame must last at least 1 ms.</summary>
    public ActionClipTiming(IReadOnlyList<float> frameMs, IReadOnlyList<bool>? elastic = null,
                            IReadOnlyDictionary<string, int>? markers = null,
                            IReadOnlyDictionary<(int Frame, string Name), ActionSocket>? sockets = null)
    {
        if (frameMs is null || frameMs.Count == 0) throw new ArgumentException("A clip needs at least one frame.", nameof(frameMs));
        if (frameMs.Any(ms => !(ms >= 1f))) throw new ArgumentException("Every frame must last at least 1 ms.", nameof(frameMs));
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
        return new ActionClipTiming(ms, Elastic, Markers, _sockets);
    }

    /// <summary>
    /// Parse a <c>.clip.json</c>:
    /// <c>{"frameMs":[..], "elastic":[..], "markers":{"release":3}, "sockets":{"3":{"ThrowHand":[x,y,deg]}}}</c>.
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
        return new ActionClipTiming(frames, elastic, markers, sockets);
    }
}
