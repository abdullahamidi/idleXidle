using System;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// What a REACH strand is made of (design.md section 1 / 5.27): a stretched material body, an object at its end, and how
/// it moves. Data only; <see cref="ReachStrand"/> plays it. Shared: the Oathbound's chain lash (P1.4) and DRINK's REACH
/// mode (Phase 3) are two instances, never two code paths.
/// </summary>
public sealed record ReachLook
{
    /// <summary>The strand's body: a cross-section texture stretched along each segment (untinted, alpha-blended).</summary>
    public string StrandKey { get; init; } = "prop_seeker_chain_body";

    /// <summary>
    /// LINKS stamped along the strand over its body (its material, untinted), or null for a plain body: a texture of two
    /// equal cells side by side, a link face-on and edge-on, drawn alternately so the strand reads as the same chain the
    /// strip carries at rest (the Phase 1 review: a smooth rod beside the strip's chunky links).
    /// </summary>
    public string? LinkKey { get; init; }

    /// <summary>A link's drawn height as a multiple of the strand's thickness (its length follows the cell's aspect).</summary>
    public float LinkHeight { get; init; } = 1.9f;

    /// <summary>The distance between links along the strand, as a share of a link's length (below 1: they interlock).</summary>
    public float LinkStep { get; init; } = 0.72f;

    /// <summary>The object at the strand's end (its material, untinted); null draws none.</summary>
    public string? EndKey { get; init; }

    /// <summary>The end object's emissive edge mask (light); null draws none.</summary>
    public string? EndEdgeKey { get; init; }

    /// <summary>Where the strand meets the end object, in its texture's pixels (the hook's eye).</summary>
    public Vector2 EndPivot { get; init; }

    /// <summary>How bright the end object's edge is, as an opacity (light = opacity squared).</summary>
    public float EndEdgeBrightness { get; init; } = 0.3f;

    /// <summary>The strand's drawn thickness, as a share of the actor's visible height.</summary>
    public float Thickness { get; init; } = 0.022f;

    /// <summary>The slack's deepest sag at full slack, as a share of the strand's straight length.</summary>
    public float Sag { get; init; } = 0.12f;

    /// <summary>How long the strand takes to come home after the beat, ms (design.md: the lash recoils over 120 ms).</summary>
    public float RecoilMs { get; init; } = 120f;

    /// <summary>
    /// How long the strand HOLDS taut from the beat before the recoil starts, ms: one display frame, so the frame that
    /// presents the beat (the playhead crosses it a few ms late) shows it straight and at the creature.
    /// </summary>
    public float TautHoldMs { get; init; } = 1000f / 60f;
}

/// <summary>One straight piece of a strand, placed (arena px): drawn as the body texture from <see cref="From"/> to <see cref="To"/>.</summary>
public readonly record struct StrandSegment(Vector2 From, Vector2 To);

/// <summary>
/// THE REACH (design.md section 1, 5.27; shared with Phase 3's DRINK REACH mode): a strand that LEAVES THE HAND over the
/// lash frames, reaches the creature's contact point and SNAPS TAUT exactly on the beat (held one display frame), then
/// recoils home over <see cref="ReachLook.RecoilMs"/>. The body never moves: the strand bridges the gap. Read from the playhead only; fixed
/// arrays, nothing allocated after construction.
/// </summary>
/// <remarks>
/// <para>
/// Out: its reach is u squared of the way (it flicks out, fastest as it arrives) and its slack falls to nothing as it
/// goes, so it is a straight line on the beat and only then. Back: the reach falls as (1 - v) squared over the recoil (it
/// leaves the creature at once) while the slack returns.
/// </para>
/// <para>
/// The screen <see cref="Aim"/>s it every update: where the hand is on the frame on the figure (its socket) and where
/// the contact point is. Everything else is a function of the playhead.
/// </para>
/// </remarks>
public sealed class ReachStrand
{
    /// <summary>How many straight pieces draw the strand.</summary>
    public const int Segments = 12;

    private readonly StrandSegment[] _segments = new StrandSegment[Segments];

    /// <summary>What the strand is made of (null before the first <see cref="Begin"/>).</summary>
    public ReachLook? Look { get; private set; }

    /// <summary>True from <see cref="Begin"/> until <see cref="Reset"/> (it may still be drawing its recoil after its clip).</summary>
    public bool Live { get; private set; }

    /// <summary>When the strand leaves the hand (the first lash frame's start), playhead ms.</summary>
    public float OutFromMs { get; private set; }

    /// <summary>The beat: the strand is taut on it.</summary>
    public float BeatMs { get; private set; }

    /// <summary>Where the strand starts (the hand's socket), arena px.</summary>
    public Vector2 Hand { get; private set; }

    /// <summary>Where it reaches on the beat (the creature's contact point), arena px.</summary>
    public Vector2 Target { get; private set; }

    /// <summary>The actor's visible height, px (the thickness is a share of it).</summary>
    public float ActorHeight { get; private set; }

    /// <summary>The end object's scale: arena px per texture px (the actor's own draw scale).</summary>
    public float PixelScale { get; private set; } = 1f;

    /// <summary>Strand pieces the last <see cref="Compose"/> placed.</summary>
    public int SegmentCount { get; private set; }

    /// <summary>The pieces the last <see cref="Compose"/> placed (the first <see cref="SegmentCount"/>).</summary>
    public ReadOnlySpan<StrandSegment> Pieces => new(_segments, 0, SegmentCount);

    /// <summary>Where the end object sits after the last <see cref="Compose"/> (the strand's tip).</summary>
    public Vector2 EndAt { get; private set; }

    /// <summary>The end object's turn after the last <see cref="Compose"/>, radians (along the strand's last piece).</summary>
    public float EndRotation { get; private set; }

    /// <summary>The strand's drawn thickness, px.</summary>
    public float ThicknessPx => Look is { } l ? MathF.Max(1f, l.Thickness * ActorHeight) : 1f;

    /// <summary>Begin a reach: out from <paramref name="outFromMs"/>, taut on <paramref name="beatMs"/>.</summary>
    public void Begin(ReachLook look, float outFromMs, float beatMs)
    {
        Look = look ?? throw new ArgumentNullException(nameof(look));
        OutFromMs = Math.Min(outFromMs, beatMs);
        BeatMs = beatMs;
        SegmentCount = 0;
        Live = true;
    }

    /// <summary>Where the hand is now and where the strand reaches (arena px), the actor's height and draw scale.</summary>
    public void Aim(Vector2 hand, Vector2 target, float actorHeight, float pixelScale)
    {
        Hand = hand;
        Target = target;
        ActorHeight = Math.Max(1f, actorHeight);
        PixelScale = pixelScale > 0f ? pixelScale : 1f;
    }

    /// <summary>Forget the strand (a new wave, a rewind).</summary>
    public void Reset()
    {
        Live = false;
        SegmentCount = 0;
    }

    /// <summary>How far out the strand is at <paramref name="playheadMs"/>: 0 (in the hand) .. 1 (at the creature, on the beat).</summary>
    public float Reach(float playheadMs)
    {
        if (!Live || Look is not { } look || playheadMs <= OutFromMs) return 0f;
        if (playheadMs <= BeatMs)
        {
            var u = BeatMs - OutFromMs <= 0f ? 1f : (playheadMs - OutFromMs) / (BeatMs - OutFromMs);
            return u * u;
        }
        if (playheadMs <= RecoilFromMs) return 1f;
        var v = (playheadMs - RecoilFromMs) / Math.Max(1f, look.RecoilMs);
        if (v >= 1f) return 0f;
        return (1f - v) * (1f - v);
    }

    /// <summary>The strand's slack at <paramref name="playheadMs"/>, a share of its length: 0 exactly on the beat (TAUT).</summary>
    public float Slack(float playheadMs)
    {
        if (!Live || Look is not { } look || playheadMs <= OutFromMs) return 0f;
        if (playheadMs <= BeatMs)
        {
            var u = BeatMs - OutFromMs <= 0f ? 1f : (playheadMs - OutFromMs) / (BeatMs - OutFromMs);
            return look.Sag * (1f - u);
        }
        if (playheadMs <= RecoilFromMs) return 0f;
        var v = (playheadMs - RecoilFromMs) / Math.Max(1f, look.RecoilMs);
        return v >= 1f ? 0f : look.Sag * v;
    }

    /// <summary>When the recoil starts: the beat plus <see cref="ReachLook.TautHoldMs"/>.</summary>
    public float RecoilFromMs => BeatMs + (Look?.TautHoldMs ?? 0f);

    /// <summary>When the strand is home again: the recoil's end.</summary>
    public float HomeMs => RecoilFromMs + (Look?.RecoilMs ?? 0f);

    /// <summary>True when the strand is fully out and straight: from the beat through its one-frame hold, and only there.</summary>
    public bool Taut(float playheadMs) => Live && Reach(playheadMs) >= 1f && Slack(playheadMs) <= 0f;

    /// <summary>True while any of the strand is out of the hand at <paramref name="playheadMs"/>.</summary>
    public bool Drawing(float playheadMs) => Live && Look is not null && playheadMs > OutFromMs && playheadMs < HomeMs;

    /// <summary>
    /// Place the strand at <paramref name="playheadMs"/>: its pieces along a sagging line from the hand to its tip, and its
    /// end object at the tip. Returns the piece count (0 when nothing is out). Nothing allocates.
    /// </summary>
    public int Compose(float playheadMs)
    {
        SegmentCount = 0;
        var reach = Reach(playheadMs);
        if (reach <= 0f || Look is null) return 0;
        var line = Target - Hand;
        var tip = Hand + line * reach;
        var span = tip - Hand;
        var length = span.Length();
        if (length < 0.5f) return 0;
        // the slack hangs DOWN (screen +y), deepest at the middle of what is out
        var sag = Slack(playheadMs) * length;
        var prev = Hand;
        for (var i = 1; i <= Segments; i++)
        {
            var s = i / (float)Segments;
            var p = Hand + span * s + new Vector2(0f, sag * 4f * s * (1f - s));
            _segments[SegmentCount++] = new StrandSegment(prev, p);
            prev = p;
        }
        EndAt = tip;
        var last = _segments[SegmentCount - 1];
        var d = last.To - last.From;
        EndRotation = d.LengthSquared() > 1e-6f ? MathF.Atan2(d.Y, d.X) : MathF.Atan2(line.Y, line.X);
        return SegmentCount;
    }
}
