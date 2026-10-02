using System;
using System.Collections.Generic;

namespace IdleXIdle.Game.Presentation.Curse;

/// <summary>A step of the drawn stage on one carried segment: at <c>At</c> it went from <c>From</c> to <c>To</c>.</summary>
internal readonly record struct StageStep(float At, int From, int To);

/// <summary>
/// Each territory's own bloom at one moment: when it was born, how long it grows, how (1 apply, 2 arrival, 3 deepen);
/// the last deepen (its old territories react, a shadow travels to the new) and the latest bloom's start.
/// </summary>
internal sealed class TerritoryClock
{
    /// <summary>When each territory began to bloom (playhead ms).</summary>
    public readonly float[] Born = new float[CurseSeating.MaxTerritories];

    /// <summary>How long each territory's bloom takes (ms).</summary>
    public readonly float[] Grow = new float[CurseSeating.MaxTerritories];

    /// <summary>How each territory bloomed: 1 apply, 2 arrival (a transfer, a hop), 3 deepen.</summary>
    public readonly byte[] Kind = new byte[CurseSeating.MaxTerritories];

    /// <summary>The event (apply, arrival, deepen step) each territory was born of.</summary>
    public readonly float[] EventOf = new float[CurseSeating.MaxTerritories];

    /// <summary>When each territory began to FADE (a step down; +inf while it is shown).</summary>
    public readonly float[] Gone = new float[CurseSeating.MaxTerritories];

    /// <summary>Territories drawn: the ones shown and the ones still fading after a step down.</summary>
    public int Count;

    /// <summary>Every deepen on the segment so far, oldest first (the last <see cref="MaxDeepens"/>): its step's moment
    /// and the territories shown before and after it. The old territories react to EACH and each one's shadow travels on
    /// its own clock, so a second deepen landing inside the first's reaction never cuts it.</summary>
    public readonly float[] DeepenAts = new float[MaxDeepens];

    /// <inheritdoc cref="DeepenAts"/>
    public readonly int[] DeepenBefores = new int[MaxDeepens], DeepenAfters = new int[MaxDeepens];

    /// <summary>Deepens recorded in <see cref="DeepenAts"/>.</summary>
    public int DeepenCount;

    /// <summary>Deepens remembered (a stage moves up at most twice on one segment; the oldest is dropped past this).</summary>
    public const int MaxDeepens = 4;

    /// <summary>The last deepen's step (-inf: none), and the territories shown before and after it.</summary>
    public float DeepenAt = float.NegativeInfinity;

    /// <inheritdoc cref="DeepenAt"/>
    public int DeepenBefore, DeepenAfter;

    /// <summary>The latest bloom's event (the apply, the arrival or the last deepen).</summary>
    public float EventAt = float.NegativeInfinity;

    /// <summary>How far territory <paramref name="k"/> has bloomed at <paramref name="t"/> (0..1) and, after a step down,
    /// how much of it is left as it fades (<see cref="CurseTimeline.FadeMs"/>: never hidden in one frame).</summary>
    public float Reveal(int k, float t)
    {
        var r = CurseTimeline.Smooth(Math.Clamp((t - Born[k]) / Grow[k], 0f, 1f));
        return t < Gone[k] ? r : r * (1f - CurseTimeline.Smooth(Math.Clamp((t - Gone[k]) / CurseTimeline.FadeMs, 0f, 1f)));
    }

    /// <summary>Is territory <paramref name="k"/> fading at <paramref name="t"/> (a step down took it)?</summary>
    public bool Fading(int k, float t) => t >= Gone[k];

    /// <summary>When every shown territory has finished blooming.</summary>
    public float GrownAt
    {
        get
        {
            var end = float.NegativeInfinity;
            for (var k = 0; k < Count; k++) end = Math.Max(end, Born[k] + Grow[k]);
            return end;
        }
    }
}

/// <summary>
/// ONE SLOT'S TERRITORY TIMELINE (ADR-013 §5), pure and device-free: the segment the mark's truth
/// (<see cref="MarkPerformance"/>) gives this creature (an apply or an arrival), the stage steps found on it, and each
/// territory's own bloom clock replayed from them. Reused across waves (<see cref="Reset"/>); the same answers whatever
/// the frame times or the order of the moments asked (a seek, a rewind).
/// </summary>
/// <remarks>
/// <para>
/// Moved unchanged from the approved prototype (39b59aaa <c>ClockAt</c> / <c>Replay</c>): the stage steps are scanned
/// once, forward, on a lattice anchored at the segment's start (<see cref="LatticeMs"/>, a stride of
/// <see cref="ScanStride"/>), each step bisected to its first lattice point. A step up blooms EVERY newly shown
/// territory, one after another, after the shadow's travel; a step down fades the ones no longer shown (the prototype
/// hid them in one frame); an arrival's territories keep their own clocks through a deepen.
/// </para>
/// <para>Allocation: <see cref="Steps"/> is sized for a segment's steps at construction (a stage changes at most three
/// times on one carried segment); nothing else allocates.</para>
/// </remarks>
internal sealed class CurseTimeline
{
    // ── TIMING (ms; the approved prototype's) ─────────────────────────────────────────────────────────────────────────
    /// <summary>An apply blooms its first territory over this.</summary>
    internal const float GrowMs = 520f;

    /// <summary>An arrival (a transfer, a hop) blooms every territory it carries...</summary>
    internal const float ArriveGrowMs = 460f;

    /// <summary>... one after another.</summary>
    internal const float ArriveStaggerMs = 70f;

    /// <summary>A deepen: the shadow travelling under the skin to the new territory...</summary>
    internal const float TravelMs = 170f;

    /// <summary>... then the new territory blooms...</summary>
    internal const float DeepenGrowMs = 400f;

    /// <summary>... the new ones one after another (a multi-depth jump never pops one in).</summary>
    internal const float DeepenStaggerMs = 90f;

    /// <summary>Then the extra light settles.</summary>
    internal const float SettleMs = 380f;

    /// <summary>The settle's light rises in over the bloom's last this-many ms as the front runs out of mask (it used to
    /// switch on at +50 % on the frame the bloom ended).</summary>
    internal const float HandoffMs = 120f;

    /// <summary>A territory a step down no longer shows fades over this. (The truth never steps a segment down today: a
    /// shown stage only deepens, and a transfer starts a new segment on another creature. This keeps the picture
    /// continuous if it ever does.)</summary>
    internal const float FadeMs = 400f;

    /// <summary>The steps' lattice: a stage step is found to this, from the segment's own start (never the frame's time).</summary>
    internal const float LatticeMs = 0.25f;

    /// <summary>The scan's stride on the lattice (50 ms).</summary>
    internal const long ScanStride = 200;

    private float _start = float.NaN;
    private byte _kind;
    private int _stage0, _stageAt;
    private long _scanned;

    /// <summary>The stage steps found on the current segment so far.</summary>
    internal readonly List<StageStep> Steps = new(8);

    /// <summary>The territories' clocks at the last moment asked.</summary>
    internal readonly TerritoryClock Clock = new();

    /// <summary>Forgets the segment (a new wave).</summary>
    internal void Reset()
    {
        _start = float.NaN;
        _kind = 0;
        _stage0 = _stageAt = 0;
        _scanned = 0;
        Steps.Clear();
        Clock.Count = 0;
        Clock.DeepenAt = Clock.EventAt = float.NegativeInfinity;
        Clock.DeepenBefore = Clock.DeepenAfter = 0;
        Clock.DeepenCount = 0;
    }

    /// <summary>The stage the curse shows on <paramref name="slot"/> (an applied mark under SPRAWL's half strength shows one).</summary>
    internal static int StageOn(MarkPerformance mark, int slot, float t) => Math.Max(1, mark.DrawnStage(slot, t));

    /// <summary>
    /// This slot's territories at <paramref name="t"/> (Count 0 while it carries no bloom: waiting, or nothing), at most
    /// <paramref name="available"/> (the body's seats).
    /// </summary>
    internal TerritoryClock ClockAt(MarkPerformance mark, int slot, float t, int available)
    {
        var c = Clock;
        var phase = mark.TryPhase(slot, t, out var u);
        if (phase is not (MarkPhase.Apply or MarkPhase.Reform))
        {
            c.Count = 0;
            return c;
        }
        var start = t - u;
        var kind = phase == MarkPhase.Apply ? (byte)1 : (byte)2;
        if (float.IsNaN(_start) || MathF.Abs(_start - start) > 1f || _kind != kind)
        {
            _start = start;
            _kind = kind;
            _stage0 = _stageAt = StageOn(mark, slot, start);
            _scanned = 0;
            Steps.Clear();
        }
        var n = (long)MathF.Floor((t - _start) / LatticeMs);
        var x = _scanned;
        while (x < n)
        {
            var next = Math.Min(x + ScanStride, n);
            if (StageOn(mark, slot, _start + next * LatticeMs) == _stageAt)
            {
                x = next;
                continue;
            }
            long lo = x, hi = next;
            while (hi - lo > 1)
            {
                var mid = (lo + hi) / 2;
                if (StageOn(mark, slot, _start + mid * LatticeMs) == _stageAt) lo = mid;
                else hi = mid;
            }
            var to = StageOn(mark, slot, _start + hi * LatticeMs);
            Steps.Add(new StageStep(_start + hi * LatticeMs, _stageAt, to));
            _stageAt = to;
            x = hi;
        }
        if (n > _scanned) _scanned = n;
        Replay(_start, _kind, _stage0, Steps, t, available, c);
        return c;
    }

    /// <summary>
    /// THE TERRITORIES' TIMELINE, replayed to <paramref name="t"/>: the segment's first bloom (an apply's territory, an
    /// arrival's every territory one after another), then each stage step in order. A step up blooms EVERY newly shown
    /// territory, one after another (a two-step catch-up popped one in without its bloom); a step down FADES the ones no
    /// longer shown (<see cref="FadeMs"/>; they used to vanish in one frame). Each territory keeps its own clock, so
    /// blooms that overlap never fight (a deepen landing during an arrival made the arrival's territories flicker between
    /// the two flares), and every deepen is remembered (<see cref="TerritoryClock.DeepenAts"/>), so reactions overlap.
    /// </summary>
    internal static void Replay(float start, byte kind, int stage0, IReadOnlyList<StageStep> steps, float t, int available, TerritoryClock c)
    {
        var count = Math.Min(available, CurseSeating.TerritoriesAt(stage0));
        for (var k = 0; k < CurseSeating.MaxTerritories; k++) c.Gone[k] = float.PositiveInfinity;
        for (var k = 0; k < count; k++)
        {
            c.Born[k] = start + (kind == 2 || count > 1 ? k * ArriveStaggerMs : 0f);
            c.Grow[k] = kind == 1 ? GrowMs : ArriveGrowMs;
            c.Kind[k] = kind;
            c.EventOf[k] = start;
        }
        c.EventAt = start;
        c.DeepenAt = float.NegativeInfinity;
        c.DeepenBefore = c.DeepenAfter = 0;
        c.DeepenCount = 0;
        var drawn = count;
        for (var s = 0; s < steps.Count; s++)
        {
            var step = steps[s];
            if (step.At > t) break;
            var next = Math.Min(available, CurseSeating.TerritoriesAt(step.To));
            if (next > count)
            {
                for (var k = count; k < next; k++)
                {
                    c.Born[k] = step.At + TravelMs + (k - count) * DeepenStaggerMs;
                    c.Grow[k] = DeepenGrowMs;
                    c.Kind[k] = 3;
                    c.EventOf[k] = step.At;
                    c.Gone[k] = float.PositiveInfinity;
                }
                c.DeepenAt = step.At;
                c.DeepenBefore = count;
                c.DeepenAfter = next;
                c.EventAt = step.At;
                if (c.DeepenCount == TerritoryClock.MaxDeepens)
                {
                    for (var j = 1; j < TerritoryClock.MaxDeepens; j++)
                    {
                        c.DeepenAts[j - 1] = c.DeepenAts[j];
                        c.DeepenBefores[j - 1] = c.DeepenBefores[j];
                        c.DeepenAfters[j - 1] = c.DeepenAfters[j];
                    }
                    c.DeepenCount--;
                }
                c.DeepenAts[c.DeepenCount] = step.At;
                c.DeepenBefores[c.DeepenCount] = count;
                c.DeepenAfters[c.DeepenCount] = next;
                c.DeepenCount++;
            }
            else
                for (var k = next; k < count; k++) c.Gone[k] = Math.Min(c.Gone[k], step.At);
            count = next;
            drawn = Math.Max(drawn, count);
        }
        // the ones a step down took stay drawn while they fade
        while (drawn > count && t >= c.Gone[drawn - 1] + FadeMs) drawn--;
        c.Count = drawn;
    }

    /// <summary>
    /// The stage the segment shows at <paramref name="t"/> (after <see cref="ClockAt"/>), EASED from one to the next over
    /// <see cref="StageEaseMs"/> after each step: what the body's overall shade follows (it darkened by a whole step on one
    /// frame). Never above the stage in force, never past <paramref name="t"/>'s steps.
    /// </summary>
    internal float EasedStage(float t)
    {
        if (float.IsNaN(_start)) return 0f;
        var s = (float)_stage0;
        for (var i = 0; i < Steps.Count; i++)
        {
            var step = Steps[i];
            if (step.At > t) break;
            s += (step.To - step.From) * Smooth(Math.Clamp((t - step.At) / StageEaseMs, 0f, 1f));
        }
        return s;
    }

    /// <summary>The body's shade eases to a new stage over this (the new territory's travel and bloom).</summary>
    internal const float StageEaseMs = TravelMs + DeepenGrowMs;

    /// <summary>A deepen's reaction in the territories already infected: a quick swell of their light, then back.</summary>
    internal static float ReactEnvelope(float age)
    {
        if (age < 0f) return 0f;
        if (age < 80f) return age / 80f;
        var f = 1f - (age - 80f) / 420f;
        return f <= 0f ? 0f : f * f;
    }

    /// <summary>Smoothstep of 0..1.</summary>
    internal static float Smooth(float x) => x * x * (3f - 2f * x);
}
