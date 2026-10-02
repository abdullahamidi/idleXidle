using System;
using System.Collections.Generic;
using IdleXIdle.Game.Presentation.Curse;

namespace IdleXIdle.Game.Presentation;

/// <summary>What a scheduled BRAND cue voices (design/audio/seeker-brand-audio-brief.md).</summary>
public enum MarkCueKind : byte
{
    /// <summary>The first infection, on the wave's first tick.</summary>
    Apply,

    /// <summary>A shown deepen to depth 2.</summary>
    Deepen,

    /// <summary>A shown deepen that reaches depth 3 (a multi-depth jump included).</summary>
    DeepenDeep,

    /// <summary>SPRAWL: one victim takes the curse at its landing.</summary>
    Infect,

    /// <summary>A cursed host falls (a transfer's start, or the final collapse).</summary>
    Leave,

    /// <summary>A transfer's landing: the curse awakens in the new host.</summary>
    Awaken,

    /// <summary>The Ash-Burn accent, layered on another cue's take-hold.</summary>
    Ash,
}

/// <summary>One scheduled BRAND cue: when it starts, the picture's moment it voices, and how it is weighted.</summary>
/// <param name="StartMs">When the cue starts (playhead ms): its take-hold minus the transient's offset in the file.</param>
/// <param name="AtMs">The picture's event it voices (the tick, the shown step, the landing, the fall).</param>
/// <param name="TakeHoldMs">The playhead moment its take-hold transient lands on.</param>
/// <param name="Kind">What it voices.</param>
/// <param name="Slot">The creature it sounds on (pan).</param>
/// <param name="Burn">The host's baked Ash-Burn weight (0..1).</param>
/// <param name="Share">The SPRAWL step-down share (1 for every other cue).</param>
/// <param name="Pitch">Its pitch (octaves; the SPRAWL step-down).</param>
/// <param name="Yields">A presented reaction snaps close to its take-hold: it gives way.</param>
/// <param name="Quiet">An action (or the field's crush) lands close to its take-hold: it is quiet.</param>
public readonly record struct MarkCue(float StartMs, float AtMs, float TakeHoldMs, MarkCueKind Kind, int Slot, float Burn,
                                      float Share, float Pitch, bool Yields, bool Quiet);

/// <summary>
/// THE CURSE'S VOICE (ADR-011's MARK reference, BRAND; design/audio/seeker-brand-audio-brief.md): every cue of one wave,
/// scheduled ONCE at BeginWave from the same deterministic truth the curse is drawn from (<see cref="MarkPerformance"/>),
/// so sound and picture cannot disagree, then asked on the playhead with PRESS's rule (<see cref="CueDue"/>).
/// </summary>
/// <remarks>
/// <para>
/// THE MOMENTS: APPLY on the first tick; a DEEPEN on every SHOWN stage step, found exactly where the curse finds it (the
/// same <see cref="CurseTimeline"/> lattice scan of <see cref="MarkPerformance.DrawnStage"/>), one cue per step and never
/// one per territory (a multi-depth jump is one <see cref="MarkCueKind.DeepenDeep"/>), none for a step whose host falls
/// before its first new territory is born, one for a SPRAWL row's ripple; an INFECT per SPRAWL victim at its landing,
/// each quieter and lower than the one before; a LEAVE at each cursed host's fall (the picture's own leaving rule: it
/// carried the mark and was not only waiting), several falling on one frame being one; an AWAKEN at each transfer's
/// landing. The ASH accent rides each of them on a host with Ash-Burn weight. The idle state is silent.
/// </para>
/// <para>Allocation-free after construction: <see cref="CueDue"/> and <see cref="CueVolume"/> read arrays only.</para>
/// </remarks>
public sealed class MarkVoice
{
    private readonly MarkCue[] _cues;
    private readonly bool[] _cued;
    private readonly int[] _mainOf;      // an ash accent's main cue (the one it rides on), -1 for a main cue
    private readonly float[] _heard;     // the share x duck a main cue was asked at since it was armed (NaN: not asked)

    /// <summary>The recipe whose cue keys and volumes this voice plays.</summary>
    public MarkRecipe Recipe { get; }

    /// <summary>
    /// The wave's cues for <paramref name="mark"/>: each creature slot's baked Ash-Burn weight (<paramref name="burn"/>,
    /// <see cref="CurseHostData.Burn"/>) and its territory seats (<paramref name="available"/>, 0: no baked host data, the
    /// curse is not drawn on it, so it is not voiced either); the presented reactions' snaps (<paramref name="reactionSnaps"/>,
    /// ms) a cue gives way to; and the moments a cue is quiet beside (<paramref name="quietMoments"/>: the actions' Skill
    /// events and a performed field's crush ticks).
    /// </summary>
    public MarkVoice(MarkPerformance mark, IReadOnlyList<float> burn, IReadOnlyList<int> available,
                     IReadOnlyList<float> reactionSnaps, IReadOnlyList<float> quietMoments)
    {
        Recipe = mark.Recipe;
        var r = Recipe;
        var list = new List<MarkCue>();
        var n = mark.CreatureSlots;
        DeepenRippleMs = RippleSpanMs(r, n);
        float BurnOf(int s) => s >= 0 && s < burn.Count ? Math.Clamp(burn[s], 0f, 1f) : 0f;
        bool Drawn(int s) => s >= 0 && s < available.Count && available[s] > 0;

        void Add(float atMs, float takeHold, MarkCueKind kind, int slot, float b, float share, float pitch)
        {
            var yields = false;
            for (var i = 0; i < reactionSnaps.Count; i++)
                if (reactionSnaps[i] >= takeHold - r.YieldBeforeMs && reactionSnaps[i] <= takeHold + r.YieldAfterMs) yields = true;
            var quiet = false;
            for (var i = 0; i < quietMoments.Count; i++)
                if (quietMoments[i] >= takeHold - r.QuietBeforeMs && quietMoments[i] <= takeHold + r.QuietAfterMs) quiet = true;
            list.Add(new MarkCue(takeHold - r.CueStartOf(kind), atMs, takeHold, kind, slot, b, share, pitch, yields, quiet));
            if (b > 0f)
                list.Add(new MarkCue(takeHold - r.CueStartOf(MarkCueKind.Ash), atMs, takeHold, MarkCueKind.Ash, slot, b, share, pitch, yields, quiet));
        }

        var first = mark.FirstTickMs;
        if (mark.FirstFront < 0 || float.IsPositiveInfinity(first))
        {
            _cues = Array.Empty<MarkCue>();
            _cued = Array.Empty<bool>();
            _mainOf = Array.Empty<int>();
            _heard = Array.Empty<float>();
            return;
        }

        // APPLY: the first territory is born on the first tick, its flare peaks from +60
        var front = mark.FirstFront;
        if (Drawn(front) && mark.DeathAt(front) > first + r.ApplyTakeHoldMs)
            Add(first, first + r.ApplyTakeHoldMs, MarkCueKind.Apply, front, BurnOf(front), 1f, 0f);

        // INFECT (SPRAWL): one per victim at its landing, stepping down the row; a victim that falls first is passed over
        if (mark.WholeWave)
            for (var s = 0; s < n; s++)
            {
                if (s == front || !Drawn(s)) continue;
                var land = mark.HopLandsAt(s);
                if (float.IsPositiveInfinity(land) || mark.DeathAt(s) <= land + r.InfectTakeHoldMs) continue;
                var step = Math.Max(0, mark.ChainOrderOf(s) - 1);
                Add(land, land + r.InfectTakeHoldMs, MarkCueKind.Infect, s, BurnOf(s), MathF.Pow(r.InfectStepShare, step), r.InfectStepPitch * step);
            }

        // AWAKEN (a transfer): at each landing on a new front; the creature a fall passed over never received it
        for (var k = 1; k < mark.HostCount; k++)
        {
            var s = mark.HostSlotAt(k);
            var lands = mark.HostLandsAt(k);
            if (!Drawn(s) || mark.DeathAt(s) <= lands + r.AwakenTakeHoldMs) continue;
            Add(lands, lands + r.AwakenTakeHoldMs, MarkCueKind.Awaken, s, BurnOf(s), 1f, 0f);
        }

        // DEEPEN: every SHOWN step, found exactly as the curse finds it (its own lattice scan of the drawn stage)
        var horizon = mark.ShownSettledBy;
        var steps = new List<(float At, int Slot, bool Deep)>();
        var timeline = new CurseTimeline();
        for (var s = 0; s < n; s++)
        {
            if (!Drawn(s)) continue;
            var end = Math.Min(horizon, mark.DeathAt(s) - 0.5f);
            if (mark.TryPhase(s, end, out _) is not (MarkPhase.Apply or MarkPhase.Reform)) continue;
            timeline.Reset();
            timeline.ClockAt(mark, s, end, available[s]);
            for (var i = 0; i < timeline.Steps.Count; i++)
            {
                var st = timeline.Steps[i];
                var before = Math.Min(available[s], CurseSeating.TerritoriesAt(st.From));
                var after = Math.Min(available[s], CurseSeating.TerritoriesAt(st.To));
                if (after <= before) continue;                                                 // no new territory blooms
                if (mark.DeathAt(s) < st.At + CurseTimeline.TravelMs) continue;               // it never would
                steps.Add((st.At, s, st.To >= r.Stages - 1));
            }
        }
        steps.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : a.Slot.CompareTo(b.Slot));
        foreach (var group in RippleGroups(steps, mark.WholeWave, DeepenRippleMs, n))
        {
            // a SPRAWL row's ripple is ONE deepen for the ear (deep if any creature reaches depth 3); every other step its own
            var (at, slot, deep) = steps[group[0]];
            var b = BurnOf(slot);
            for (var g = 1; g < group.Count; g++)
            {
                deep |= steps[group[g]].Deep;
                b = Math.Max(b, BurnOf(steps[group[g]].Slot));
            }
            Add(at, at + r.DeepenTakeHoldMs, deep ? MarkCueKind.DeepenDeep : MarkCueKind.Deepen, slot, b, 1f, 0f);
        }

        // LEAVE: each cursed host's fall (the picture's leaving rule), the hosts falling on one frame as one
        var falls = new List<(float At, int Slot)>();
        for (var s = 0; s < n; s++)
        {
            var died = mark.DeathAt(s);
            if (!Drawn(s) || float.IsPositiveInfinity(died)) continue;
            var phase = mark.TryPhase(s, died - 1f, out _);
            if (phase is not (MarkPhase.Apply or MarkPhase.Reform)) continue;
            falls.Add((died, s));
        }
        falls.Sort((a, b) => a.At.CompareTo(b.At));
        for (var i = 0; i < falls.Count;)
        {
            var (at, slot) = falls[i];
            var b = BurnOf(slot);
            var j = i + 1;
            for (; j < falls.Count && falls[j].At - at <= r.LeaveMergeMs; j++) b = Math.Max(b, BurnOf(falls[j].Slot));
            Add(at, at + r.LeaveTakeHoldMs, MarkCueKind.Leave, slot, b, 1f, 0f);
            i = j;
        }

        list.Sort((a, b) => a.StartMs != b.StartMs ? a.StartMs.CompareTo(b.StartMs) : a.Kind.CompareTo(b.Kind));
        _cues = list.ToArray();
        _cued = new bool[_cues.Length];
        _heard = new float[_cues.Length];
        Array.Fill(_heard, float.NaN);
        _mainOf = new int[_cues.Length];
        for (var k = 0; k < _cues.Length; k++)
        {
            _mainOf[k] = -1;
            if (_cues[k].Kind != MarkCueKind.Ash) continue;
            for (var m = 0; m < _cues.Length && _mainOf[k] < 0; m++)
                if (_cues[m].Kind != MarkCueKind.Ash && _cues[m].Slot == _cues[k].Slot && _cues[m].TakeHoldMs == _cues[k].TakeHoldMs)
                    _mainOf[k] = m;
        }
    }

    /// <summary>
    /// How far after a SPRAWL ripple's first shown step its last can come (ms): the whole row's ripple
    /// (<c>(slots - 1) x CarveStepMs x 1.5</c>, <see cref="MarkPerformance"/>'s chisel stagger) plus
    /// <see cref="MarkRecipe.DeepenRippleSlackMs"/>.
    /// </summary>
    public float DeepenRippleMs { get; }

    /// <summary>A row of <paramref name="slots"/> creatures' ripple span (see <see cref="DeepenRippleMs"/>).</summary>
    internal static float RippleSpanMs(MarkRecipe r, int slots)
        => Math.Max(0, slots - 1) * r.CarveStepMs * 1.5f + r.DeepenRippleSlackMs;

    /// <summary>
    /// The deepen cues of a wave's shown <paramref name="steps"/> (sorted by time), as groups of step indices (the first
    /// is the cue's own step). Outside SPRAWL (<paramref name="wholeWave"/> false) every step is its own cue. In SPRAWL a
    /// step gathers the later steps of OTHER creatures within <paramref name="rippleMs"/> of it (one tick's ripple down
    /// the row, however long the row); a creature's second step in that window is never folded in: it starts its own
    /// cue (one cue per step on a creature: a catch-up, then the next tick's chisel, are two).
    /// </summary>
    internal static List<List<int>> RippleGroups(IReadOnlyList<(float At, int Slot, bool Deep)> steps, bool wholeWave,
                                                 float rippleMs, int slots)
    {
        var groups = new List<List<int>>();
        var used = new bool[steps.Count];
        var inGroup = new bool[Math.Max(1, slots)];
        for (var i = 0; i < steps.Count; i++)
        {
            if (used[i]) continue;
            used[i] = true;
            var group = new List<int> { i };
            groups.Add(group);
            if (!wholeWave) continue;
            Array.Clear(inGroup);
            if (steps[i].Slot >= 0 && steps[i].Slot < inGroup.Length) inGroup[steps[i].Slot] = true;
            for (var j = i + 1; j < steps.Count && steps[j].At - steps[i].At <= rippleMs; j++)
            {
                var s = steps[j].Slot;
                if (used[j] || s < 0 || s >= inGroup.Length || inGroup[s]) continue;
                inGroup[s] = true;
                used[j] = true;
                group.Add(j);
            }
        }
        return groups;
    }

    /// <summary>The wave's cues, in start order.</summary>
    public int Count => _cues.Length;

    /// <summary>Scheduled cue <paramref name="i"/>.</summary>
    public MarkCue Cue(int i) => _cues[i];

    /// <summary>
    /// The next cue at or after index <paramref name="from"/> that starts now, or -1. Each cue is asked ONCE; a rewind
    /// before its start re-arms it; a start passed by more than <see cref="MarkRecipe.CueLateMs"/> (a seek, a long hitch)
    /// is skipped rather than played late (PRESS's rule). Several may be due on one frame: ask from 0, then from each
    /// answer + 1 until -1 (every cue is visited once per frame, so every one is re-armed or latched).
    /// </summary>
    public int CueDue(float playheadMs, int from = 0)
    {
        for (var k = Math.Max(0, from); k < _cues.Length; k++)
        {
            var at = _cues[k].StartMs;
            if (playheadMs < at - 1f) { _cued[k] = false; _heard[k] = float.NaN; continue; }
            if (_cued[k]) continue;
            _cued[k] = true;
            if (playheadMs <= at + Recipe.CueLateMs) return k;
        }
        return -1;
    }

    /// <summary>
    /// Cue <paramref name="i"/>'s volume: its kind's volume x the SPRAWL step-down, the Ash-Burn rule (the main cue x(1 -
    /// <see cref="MarkRecipe.AshMainCut"/> burn), the accent x burn; giving way to a reaction, the accent is omitted (0)
    /// and the main cue whole), then the yield share, or the quiet share near an action or while the champion is
    /// <paramref name="performing"/>, unless the action's duck already applies (<paramref name="ducked"/>: the duck alone).
    /// THE ACCENT FOLLOWS ITS CUE: an ash accent starts up to ~225 ms after the cue it rides on (on the take-hold), when
    /// the champion may have started or stopped performing and the duck changed; so once its main cue was asked, the
    /// accent is heard at the SAME share x <paramref name="duck"/> as that cue was (divided by the frame's own duck, which
    /// the bank applies again; never above its full volume), never louder than the layer it accents. An accent whose cue
    /// was never asked (its start skipped late by a seek or a hitch) or never heard (the bank's throttle dropped it,
    /// <see cref="Heard"/>) is omitted (0): the crumble never plays alone. Its full volume is also held
    /// <see cref="MarkRecipe.AshUnderMainDb"/> under its cue's kind at any burn (<see cref="MarkRecipe.AshShareUnder"/>).
    /// </summary>
    public float CueVolume(int i, bool ducked = false, bool performing = false, float duck = 1f)
    {
        var c = _cues[i];
        var r = Recipe;
        var share = c.Yields ? r.CueYieldShare : (c.Quiet || performing) && !ducked ? r.CueQuietShare : 1f;
        if (c.Kind != MarkCueKind.Ash)
        {
            _heard[i] = share * (ducked ? duck : 1f);
            return r.VolumeOf(c.Kind) * (c.Yields ? 1f : 1f - r.AshMainCut * c.Burn) * c.Share * share;
        }
        if (c.Yields) return 0f;
        var main = _mainOf[i];
        if (main < 0 || float.IsNaN(_heard[main])) return 0f;
        var full = r.AshVolume * r.AshShareUnder(_cues[main].Kind) * c.Burn * c.Share;
        return Math.Min(full, full * _heard[main] / (ducked ? Math.Max(duck, 0.05f) : 1f));
    }

    /// <summary>
    /// What the bank did with main cue <paramref name="i"/> once it was played: <paramref name="throttleShare"/> is the
    /// share of its volume the repeat throttle let through (<see cref="SoundBank.LastThrottleShare"/>: 1 whole, below 1
    /// quieter-when-recent, 0 dropped; 0 too when no file resolved). Its accent follows it (a dropped cue's accent is
    /// omitted, a quieter cue's accent is as much quieter). Ignored for an accent and for a cue not asked.
    /// </summary>
    public void Heard(int i, float throttleShare)
    {
        if (_cues[i].Kind == MarkCueKind.Ash || float.IsNaN(_heard[i])) return;
        _heard[i] *= Math.Clamp(throttleShare, 0f, 1f);
    }
}
