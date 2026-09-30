using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game.Presentation;

/// <summary>Where a creature's coil stands at one moment (see <see cref="MarkPerformance.TryPhase"/>).</summary>
public enum MarkPhase
{
    /// <summary>No coil.</summary>
    None,

    /// <summary>Applied on the wave's first tick: gathering, then the coil (u from the tick).</summary>
    Apply,

    /// <summary>The smoke is still flying to this creature, which Core already amplifies: it carries a faint smoke (u
    /// negative, to the landing).</summary>
    Waiting,

    /// <summary>The smoke landed on its coil's tip: the coil is drawn in, then rests (u from the landing).</summary>
    Reform,
}

/// <summary>
/// One wave's MARK (ADR-011, the MARK / PERSISTENT TARGET-ATTACHED STATE reference, BRAND): built from the wave's own
/// events and read on the fight's playhead, so a seek or a rewind simply draws what that moment holds.
/// </summary>
/// <remarks>
/// <para>
/// THE TRUTH IT PRESENTS (Core's, never re-derived beyond the build's reach): the mark exists from the wave's FIRST tick
/// (the first Marked event of BRAND's slot), its depth is the last tick's reported percent (reported IN FORCE since
/// 2026-09-29), and its host is the FRONT creature, the first one still standing, exactly Core's FirstAlive (SPRAWL:
/// every creature; ANCHOR: the front at its own full depth). Core has no transfer: the front is re-evaluated on every
/// hit, so when the host falls the next creature is marked at once. The picture's MIGRATION is that moment, shown: the
/// new front carries a faint smoke from the fall, and its coil is drawn in when the strand lands. A fall is the one the
/// SCREEN shows (a JAWS kill falls on the jaws' snap: the caller passes that time).
/// </para>
/// <para>
/// ONE SHOWN STAGE PER COIL: the depth in force is the truth, but the picture only ever cuts deeper under a chisel. A
/// coil shows the stage it arrived with (applied, or carried from the host it left, as that host SHOWED it when it fell)
/// and moves to a deeper one only when a chisel's last step reaches it: a tick that deepened it (after the host's own
/// bite settles), ANCHOR's new front, or a CATCH-UP once a migrated coil is whole again (the host fell mid-chisel, or a
/// tick deepened the mark while it was in flight). So no deepen is ever lost and none is ever skipped past.
/// </para>
/// <para>
/// DRAWN IN TWO PLACES: <see cref="DrawOn"/> inside the creature loop, on the body it belongs to (after the creature,
/// before its hit flash), which also pins every creature's torso for the second; <see cref="DrawLoose"/> after the row,
/// for what is between bodies: the coil coming apart on a fallen host and the smoke in flight.
/// </para>
/// <para>
/// Allocation-free after construction: every table is an array sized here, no LINQ, no closures per frame (the body
/// point is authored per frame, <see cref="MarkPoints"/>; only a strip with none falls back to reading its pixels).
/// </para>
/// </remarks>
public sealed class MarkPerformance
{
    /// <summary>How this mark is drawn.</summary>
    public MarkRecipe Recipe { get; }

    /// <summary>The skill slot of the marking field (BRAND), whose Aura / Marked events this wave built it from.</summary>
    public int Slot { get; }

    private readonly float[] _tickAt;
    private readonly int[] _tickPercent;
    private readonly bool[] _tickQuiet;
    private readonly float[] _tickSettle;        // how long after the tick its chisel waits for the bites round it to settle
    private readonly float[] _strikes;           // the wave's enemy strikes (ms, sorted): no chisel is traced over a bite
    private readonly float[] _deathAt;           // per creature slot: when it falls on screen (+inf: it stands to the end)
    private readonly bool _wholeWave;
    private readonly int _frontFullPercent;
    private readonly int _firstFront;

    // THE HOSTS (front mode): segment k's host, the fall that sent the mark to it (k = 0: the first tick), where it came
    // from (-1: applied), and when the smoke lands on its coil (k = 0: the first tick)
    private readonly int[] _hostSlot;
    private readonly float[] _hostFall;
    private readonly int[] _hostFromSlot;
    private readonly float[] _landAt;
    private readonly float[] _catchAt;           // when a migrated coil that deepened on the way is cut (after the fall's smoke)
    private readonly int _hostCount;

    // SPRAWL: each creature's hop down the row (the slot it leaves from, when it leaves, when it lands; +inf: none)
    private readonly int[] _hopFrom;
    private readonly float[] _hopAt;
    private readonly float[] _hopLand;
    private readonly float[] _hopCatch;
    private readonly int[] _chainOrder;          // SPRAWL: each creature's place in the spread (its beats ripple, never lockstep)

    // THE PINS: every creature's torso and scale, from the frame drawn this frame (or the last one it was drawn)
    private readonly Vector2[] _anchor;
    private readonly float[] _scale;
    private readonly bool[] _pinned;

    /// <summary>
    /// A mark from BRAND's <paramref name="ticks"/> (its Aura ms, the Marked percent reported with it, and whether an
    /// action, a reaction or the field's crush sounds close by), the wave's creature <paramref name="falls"/> as the screen
    /// shows them, how many creature slots it has, and the build's reach (<paramref name="wholeWave"/>: SPRAWL;
    /// <paramref name="frontFullPercent"/>: ANCHOR's front depth, 0 without it), and optionally the wave's enemy
    /// <paramref name="strikes"/> (ms): no chisel is traced over a bite's wind-up or its attack clip (a chisel drawn over a
    /// lunge could not be read).
    /// </summary>
    public MarkPerformance(MarkRecipe recipe, int slot, IReadOnlyList<(float AtMs, int Percent, bool Quiet)> ticks,
                           IReadOnlyList<(float AtMs, int Slot)> falls, int creatureSlots, bool wholeWave, int frontFullPercent,
                           IReadOnlyList<float>? strikes = null)
    {
        Recipe = recipe;
        Slot = slot;
        _tickAt = new float[ticks.Count];
        _tickPercent = new int[ticks.Count];
        _tickQuiet = new bool[ticks.Count];
        _tickSettle = new float[ticks.Count];
        _strikes = new float[strikes?.Count ?? 0];
        for (var i = 0; i < _strikes.Length; i++) _strikes[i] = strikes![i];
        Array.Sort(_strikes);
        for (var i = 0; i < ticks.Count; i++)
        {
            (_tickAt[i], _tickPercent[i], _tickQuiet[i]) = ticks[i];
            _tickSettle[i] = SettledStart(_tickAt[i]) - _tickAt[i];
        }
        var n = Math.Max(1, creatureSlots);
        _deathAt = new float[n];
        Array.Fill(_deathAt, float.PositiveInfinity);
        for (var i = 0; i < falls.Count; i++)
            if (falls[i].Slot >= 0 && falls[i].Slot < n) _deathAt[falls[i].Slot] = Math.Min(_deathAt[falls[i].Slot], falls[i].AtMs);
        _wholeWave = wholeWave;
        _frontFullPercent = frontFullPercent;
        _anchor = new Vector2[n];
        _scale = new float[n];
        _pinned = new bool[n];
        _hostSlot = new int[n + 1];
        _hostFall = new float[n + 1];
        _hostFromSlot = new int[n + 1];
        _landAt = new float[n + 1];
        _catchAt = new float[n + 1];
        _hopFrom = new int[n];
        _hopAt = new float[n];
        _hopLand = new float[n];
        _hopCatch = new float[n];
        Array.Fill(_hopFrom, -1);
        Array.Fill(_hopAt, float.PositiveInfinity);
        Array.Fill(_hopLand, float.PositiveInfinity);
        Array.Fill(_hopCatch, float.PositiveInfinity);
        _chainOrder = new int[n];
        var first = FirstTickMs;
        _firstFront = float.IsPositiveInfinity(first) ? -1 : FrontAt(first);
        if (_firstFront < 0) return;
        if (wholeWave)
        {
            // SPRAWL: the condition PROPAGATES from its source: short ink threads branch out of the front's mark to every
            // other creature near-simultaneously (each leaving SpreadStaggerMs after the one before), and the row etches
            // almost at once. A creature that falls before its thread would land is passed over (no thread to a corpse);
            // a thread never leaves a fallen source (with none standing, the mark forms with no thread at all)
            var received = new List<int>(n) { _firstFront };
            var leave = first + recipe.SpreadFromMs;
            var prevCatch = float.NegativeInfinity;
            for (var s = 0; s < n; s++)
            {
                if (s == _firstFront || _deathAt[s] <= first) continue;
                var land = leave + recipe.FlightMs * 0.75f;
                if (_deathAt[s] <= land) continue;
                var from = _deathAt[_firstFront] > leave ? _firstFront : -1;
                _hopFrom[s] = from;
                _hopAt[s] = leave;
                _hopLand[s] = land;
                // each coil's re-cut at its own place in the chain (a bite settling them all at one moment lit the whole
                // row on one frame)
                _hopCatch[s] = SettledStart(Math.Max(land + recipe.ReformMs, prevCatch + recipe.CarveStepMs * 1.5f));
                prevCatch = _hopCatch[s];
                _chainOrder[s] = received.Count;
                received.Add(s);
                leave += recipe.SpreadStaggerMs;
            }
            return;
        }
        // the host chain: applied to the front on the first tick, then carried at each fall to the creature that will be
        // standing when the smoke LANDS (one falling in the meantime is passed over)
        var host = _firstFront;
        _hostSlot[0] = host;
        _hostFall[0] = first;
        _hostFromSlot[0] = -1;
        _landAt[0] = first;
        _hostCount = 1;
        while (_hostCount <= n)
        {
            var fell = _deathAt[host];
            if (float.IsPositiveInfinity(fell)) break;
            // the mark seeps in on the new front once its own bite has played out (the deepen's rule: under a bite the
            // etch-in went unseen)
            var lands = SettledStart(fell + recipe.FlightFromMs + recipe.FlightMs, recipe.TransferSettleMs);
            var next = FrontAt(lands);
            if (next < 0) break;   // nobody is left standing: the coil comes apart and nothing receives it
            _hostSlot[_hostCount] = next;
            _hostFall[_hostCount] = fell;
            _hostFromSlot[_hostCount] = host;
            _landAt[_hostCount] = lands;
            // a coil that deepened on the way is cut once whole, after the fallen creature's death smoke has cleared off
            // it and no bite is in the way (cut inside the smoke, the deepest step was never seen)
            _catchAt[_hostCount] = SettledStart(Math.Max(lands + recipe.ReformMs, fell + recipe.DeathClearMs));
            _hostCount++;
            host = next;
        }
    }

    /// <summary>
    /// The first moment at or after <paramref name="fromMs"/> a chisel's travel (its three steps) can run on a settled
    /// body: clear of every bite's wind-up (<see cref="MarkRecipe.SettleBeforeMs"/> before the strike) and its attack clip
    /// (<see cref="MarkRecipe.SettleMs"/> after it).
    /// </summary>
    internal float SettledStart(float fromMs, float? afterMs = null)
    {
        var after = afterMs ?? Recipe.SettleMs;
        var start = fromMs;
        var span = Recipe.CarveStepMs * 3f;
        for (var pass = 0; pass < 8; pass++)
        {
            var moved = false;
            for (var i = 0; i < _strikes.Length; i++)
                if (_strikes[i] - Recipe.SettleBeforeMs < start + span && _strikes[i] + after > start)
                {
                    start = _strikes[i] + after;
                    moved = true;
                }
            if (!moved) break;
        }
        return start;
    }

    /// <summary>The wave's first BRAND tick: the mark exists from here (+inf: this wave never had one).</summary>
    public float FirstTickMs => _tickAt.Length > 0 ? _tickAt[0] : float.PositiveInfinity;

    /// <summary>Sprites drawn since <see cref="BeginFrame"/> (the trace's budget line).</summary>
    public int SpriteCount { get; private set; }

    /// <summary>Bodies carrying a coil this frame, and the deepest stage drawn (the trace).</summary>
    public int LastHosts { get; private set; }

    /// <inheritdoc cref="LastHosts"/>
    public int LastStage { get; private set; }

    /// <summary>Starts a frame's counters.</summary>
    public void BeginFrame()
    {
        SpriteCount = 0;
        LastHosts = 0;
        LastStage = -1;
    }

    // ── THE TRUTH ON THE PLAYHEAD ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>When the creature in <paramref name="slot"/> falls on screen (+inf: it stands to the end).</summary>
    internal float DeathAt(int slot) => slot >= 0 && slot < _deathAt.Length ? _deathAt[slot] : float.PositiveInfinity;

    /// <summary>A curse prototype's draw counted as this mark's (the trace reads one mark either way).</summary>
    internal void CountCurse(int sprites, int stage)
    {
        SpriteCount = sprites;
        LastHosts++;
        LastStage = Math.Max(LastStage, stage);
    }

    /// <summary>Is the creature in <paramref name="slot"/> still standing (on screen) at <paramref name="playheadMs"/>?</summary>
    public bool Standing(int slot, float playheadMs) => slot >= 0 && slot < _deathAt.Length && playheadMs < _deathAt[slot];

    /// <summary>The front creature at <paramref name="playheadMs"/>: the first one still standing (Core's FirstAlive), -1 if none.</summary>
    public int FrontAt(float playheadMs)
    {
        for (var s = 0; s < _deathAt.Length; s++)
            if (playheadMs < _deathAt[s]) return s;
        return -1;
    }

    /// <summary>The depth in force at <paramref name="playheadMs"/> (the last tick's reported percent; before the first
    /// tick, the first one's, which the gathering smoke condenses into).</summary>
    public int DepthAt(float playheadMs)
    {
        if (_tickAt.Length == 0) return 0;
        var pct = _tickPercent[0];
        for (var i = 1; i < _tickAt.Length && _tickAt[i] <= playheadMs; i++) pct = _tickPercent[i];
        return pct;
    }

    /// <summary>The depth in force on <paramref name="slot"/> (ANCHOR: the front at its own full depth, whatever WINNOW does).</summary>
    public int DepthFor(int slot, float playheadMs)
        => _wholeWave && _frontFullPercent > 0 && slot == FrontAt(playheadMs) ? _frontFullPercent : DepthAt(playheadMs);

    /// <summary>The stage the coil on <paramref name="slot"/> is drawn at (ANCHOR: the front at its own full depth).</summary>
    public int StageFor(int slot, float playheadMs) => Recipe.StageOf(DepthFor(slot, playheadMs));

    /// <summary>
    /// Where the coil on <paramref name="slot"/> stands at <paramref name="playheadMs"/>, and <paramref name="u"/>: ms from
    /// the first tick (APPLY), to the landing (WAITING, negative) or from the landing (REFORM).
    /// </summary>
    public MarkPhase TryPhase(int slot, float playheadMs, out float u)
    {
        u = 0f;
        if (_firstFront < 0 || !Standing(slot, playheadMs)) return MarkPhase.None;
        if (_wholeWave)
        {
            if (slot == _firstFront)
            {
                u = playheadMs - FirstTickMs;
                return u >= Recipe.GatherFromMs ? MarkPhase.Apply : MarkPhase.None;
            }
            // Core amplifies every creature standing from the first tick: each waits (a faint smoke) until its hop lands; one
            // the hop never reaches (it falls first) keeps the faint smoke until it falls
            if (playheadMs < FirstTickMs) return MarkPhase.None;
            if (float.IsPositiveInfinity(_hopLand[slot]))
            {
                u = -Recipe.WaitingRampMs;
                return MarkPhase.Waiting;
            }
            u = playheadMs - _hopLand[slot];
            return u < 0f ? MarkPhase.Waiting : MarkPhase.Reform;
        }
        for (var k = _hostCount - 1; k >= 0; k--)
        {
            if (_hostSlot[k] != slot) continue;
            if (k == 0)
            {
                u = playheadMs - _landAt[0];
                return u >= Recipe.GatherFromMs ? MarkPhase.Apply : MarkPhase.None;
            }
            if (playheadMs < _hostFall[k]) return MarkPhase.None;
            u = playheadMs - _landAt[k];
            return u < 0f ? MarkPhase.Waiting : MarkPhase.Reform;
        }
        return MarkPhase.None;
    }

    /// <summary>SPRAWL: the creature the hop to <paramref name="slot"/> leaves from (-1: none, the coil forms with no strand).</summary>
    internal int HopFromOf(int slot) => slot >= 0 && slot < _hopFrom.Length ? _hopFrom[slot] : -1;

    /// <summary>SPRAWL: when the hop to <paramref name="slot"/> leaves (+inf: it never does).</summary>
    internal float HopLeavesAt(int slot) => slot >= 0 && slot < _hopAt.Length ? _hopAt[slot] : float.PositiveInfinity;

    /// <summary>Does the creature in <paramref name="slot"/> carry the mark at <paramref name="playheadMs"/> (Core's truth
    /// as the screen draws it: applied, waiting for the smoke, re-formed)?</summary>
    public bool Carries(int slot, float playheadMs) => TryPhase(slot, playheadMs, out _) != MarkPhase.None;

    /// <summary>The atlas column the mark on a body shows at this phase: a GATHER cell while it forms, a FORM cell while
    /// it seeps in after a transfer, else the idle cell of its (slow, per-creature) step.</summary>
    public int ColumnAt(int slot, MarkPhase phase, float u, float playheadMs)
    {
        var r = Recipe;
        if (phase == MarkPhase.Apply)
        {
            if (u < r.Gather1Ms) return MarkRecipe.Gather0;
            if (u < r.Gather2Ms) return MarkRecipe.Gather0 + 1;
            if (u < r.FormMs) return MarkRecipe.Gather0 + 2;
        }
        else if (phase == MarkPhase.Waiting) return MarkRecipe.Gather0;
        else if (phase == MarkPhase.Reform && u < r.ReformMs)
            return MarkRecipe.Form0 + Math.Min(2, (int)(u / (r.ReformMs / 3f)));
        // THE SWIRL, stepped, each creature stepping at its own moment (a SPRAWL row never ticks in lockstep)
        var t = Math.Max(0f, playheadMs) + slot * r.SwirlStepMs * 0.382f;
        var step = (int)MathF.Floor(t / r.SwirlStepMs);
        return MarkRecipe.Idle0 + ((step % MarkRecipe.IdlePhases) + MarkRecipe.IdlePhases) % MarkRecipe.IdlePhases;
    }

    /// <summary>
    /// The coil's segment on <paramref name="slot"/>: its phase, when it was whole (<paramref name="formedAt"/>: the first
    /// tick for an applied coil, the landing plus the re-form for a migrated one) and the stage it <paramref name="arrived"/>
    /// with (applied: the depth on the first tick; migrated: the stage its last host SHOWED when it fell).
    /// </summary>
    private MarkPhase Segment(int slot, float playheadMs, out float u, out float formedAt, out int arrived, out float catchAt)
    {
        formedAt = float.PositiveInfinity;
        catchAt = float.PositiveInfinity;
        arrived = 0;
        var phase = TryPhase(slot, playheadMs, out u);
        if (phase == MarkPhase.None) return phase;
        if (phase == MarkPhase.Apply)
        {
            formedAt = catchAt = FirstTickMs;
            arrived = StageFor(slot, FirstTickMs);
            return phase;
        }
        var landed = playheadMs - u;
        formedAt = landed + Recipe.ReformMs;
        arrived = CarriedStage(slot, landed);
        catchAt = CatchAt(slot, landed);
        return phase;
    }

    /// <summary>When a migrated coil's CATCH-UP chisel may cut (the deepening it carried in): after the fallen creature's
    /// death smoke and clear of bites; a SPRAWL hop has no fall, so as soon as it is whole and settled.</summary>
    private float CatchAt(int slot, float landed)
    {
        if (_wholeWave) return slot >= 0 && slot < _hopCatch.Length ? _hopCatch[slot] : float.PositiveInfinity;
        for (var k = 1; k < _hostCount; k++)
            if (_hostSlot[k] == slot && Math.Abs(_landAt[k] - landed) < 0.5f) return _catchAt[k];
        return landed + Recipe.ReformMs;
    }

    /// <summary>The stage a migrating coil brings to <paramref name="slot"/>: what its last host showed as it fell (under
    /// SPRAWL, the depth in force when the hop left).</summary>
    private int CarriedStage(int slot, float landed)
    {
        if (_wholeWave) return StageFor(slot, _hopAt[slot]);
        for (var k = 1; k < _hostCount; k++)
            if (_hostSlot[k] == slot && Math.Abs(_landAt[k] - landed) < 0.5f)
                return Math.Min(DrawnStage(_hostFromSlot[k], _hostFall[k] - 0.5f), StageFor(slot, landed));
        return StageFor(slot, landed);
    }

    /// <summary>
    /// The beat on <paramref name="slot"/> at <paramref name="playheadMs"/>: the atlas <paramref name="column"/> drawn hot
    /// and its opacity (0: none). APPLY catches the cut's rim and outer edge once, ON the tick. A DEEPEN runs its phrase
    /// (ink gathering where the new cut will be, the new cut, its rim answering, then a short cooling) whenever the mark's
    /// shown stage moves deeper: a
    /// tick that deepened it (once the host's own bite settles), ANCHOR's new front at the fall, and the CATCH-UP once a
    /// migrated coil is whole again, if the mark deepened while it was carried; a depth that rose inside a stage runs it
    /// faintly; a re-formed coil at the same depth is cut once, faintly, when it is whole. A tick that changes nothing
    /// shows nothing. Quiet beside an action, a reaction or the field's crush.
    /// </summary>
    public float HotAt(int slot, float playheadMs, out int column) => HotAt(slot, playheadMs, out column, out _);

    /// <inheritdoc cref="HotAt(int, float, out int)"/>
    /// <param name="stage">The stage whose cell <paramref name="column"/> is drawn from: a chisel is the stage it cuts to.</param>
    public float HotAt(int slot, float playheadMs, out int column, out int stage)
    {
        column = MarkRecipe.Edge;
        var phase = Segment(slot, playheadMs, out var u, out var formedAt, out var arrived, out var catchAt);
        stage = arrived;
        if (phase is MarkPhase.None or MarkPhase.Waiting) return 0f;
        var r = Recipe;
        var hot = 0f;
        if (phase == MarkPhase.Apply)
        {
            if (u >= 0f && u < r.EtchMs)
                hot = r.EtchAlpha * (_tickQuiet.Length > 0 && _tickQuiet[0] ? r.QuietShare : 1f) * Fall(u / r.EtchMs);
        }
        else
        {
            var caught = StageFor(slot, catchAt);
            if (caught > arrived)
            {
                // THE CATCH-UP: carried at one depth, it deepened on the way (or its host fell mid-chisel): cut it here,
                // once the fall's smoke has cleared and no bite is in the way
                var a = ChiselAt(playheadMs - catchAt, out var c) * r.DeepenEdgeAlpha;
                if (a > hot) { hot = a; column = c; stage = caught; }
            }
            else if (playheadMs >= catchAt && playheadMs < catchAt + r.EtchMs)
                // re-formed at the same depth: one faint re-cut, when the catch-up would have cut (after the fall's
                // death smoke, clear of bites): at the re-form itself the white smoke covered it
                hot = r.ReformEtchAlpha * Fall((playheadMs - catchAt) / r.EtchMs);
        }
        for (var i = 1; i < _tickAt.Length; i++)
        {
            var at = _tickAt[i];
            var start = TickChiselStart(slot, i);
            if (at <= catchAt || start > playheadMs || playheadMs - start >= BeatMs) continue;
            var to = StageFor(slot, at);
            var deepens = to > StageFor(slot, at - 0.5f);
            var rose = DepthFor(slot, at) > DepthFor(slot, at - 0.5f);     // ANCHOR's front never rises with WINNOW
            if (!deepens && !rose) continue;
            var a = ChiselAt(playheadMs - start, out var c) * (deepens ? r.DeepenEdgeAlpha : r.RetraceAlpha) * (_tickQuiet[i] ? r.QuietShare : 1f);
            if (a > hot) { hot = a; column = c; stage = to; }
        }
        if (_wholeWave && _frontFullPercent > 0)
            for (var s = 0; s < _deathAt.Length; s++)
            {
                var at = _deathAt[s];
                if (float.IsPositiveInfinity(at) || at <= catchAt || at > playheadMs || playheadMs - at >= BeatMs) continue;
                var to = StageFor(slot, at);
                if (to <= StageFor(slot, at - 0.5f)) continue;
                var a = ChiselAt(playheadMs - at, out var c) * r.DeepenEdgeAlpha;
                if (a > hot) { hot = a; column = c; stage = to; }
            }
        return hot;
    }

    /// <summary>
    /// The stage DRAWN on <paramref name="slot"/>: the stage the coil arrived with, moved deeper only when a chisel's last
    /// step reaches the deeper cut (a tick's, ANCHOR's at a fall, or the catch-up of a migrated coil): the old cut stays on
    /// screen while the chisel is on its way, and a deepen the host fell in the middle of is carried, never lost.
    /// </summary>
    public int DrawnStage(int slot, float playheadMs)
    {
        var phase = Segment(slot, playheadMs, out _, out var formedAt, out var shown, out var catchAt);
        if (phase == MarkPhase.None) return StageFor(slot, Math.Max(playheadMs, FirstTickMs));
        var reveal = Recipe.CarveStepMs * 2f;
        if (phase == MarkPhase.Waiting || playheadMs < formedAt) return shown;
        if (playheadMs >= catchAt + reveal) shown = Math.Max(shown, StageFor(slot, catchAt));
        for (var i = 1; i < _tickAt.Length; i++)
        {
            var at = _tickAt[i];
            if (at <= catchAt || at > playheadMs) continue;
            if (playheadMs >= TickChiselStart(slot, i) + reveal) shown = Math.Max(shown, StageFor(slot, at));
        }
        if (_wholeWave && _frontFullPercent > 0)
            for (var s = 0; s < _deathAt.Length; s++)
            {
                // only a fall that deepened THIS coil (ANCHOR's new front), never another coil's deepen popping in early
                var at = _deathAt[s];
                if (float.IsPositiveInfinity(at) || at <= catchAt || playheadMs < at + reveal) continue;
                var to = StageFor(slot, at);
                if (to > StageFor(slot, at - 0.5f)) shown = Math.Max(shown, to);
            }
        return shown;
    }

    /// <summary>When tick <paramref name="i"/>'s chisel starts on <paramref name="slot"/>: after the bite settles, and under
    /// SPRAWL a step and a half later per place in the spread, so a deepen ripples down the row (never a whole-row flash).</summary>
    private float TickChiselStart(int slot, int i)
        => _tickAt[i] + _tickSettle[i] + (_wholeWave && slot >= 0 && slot < _chainOrder.Length ? _chainOrder[slot] * Recipe.CarveStepMs * 1.5f : 0f);

    /// <summary>A whole deepen beat: the chisel's three steps and the last one's cooling.</summary>
    private float BeatMs => Recipe.CarveStepMs * 3f + Recipe.DeepenEdgeMs;

    /// <summary>The chisel's step (column) and strength <paramref name="v"/> ms into a deepen (0 before it starts).</summary>
    private float ChiselAt(float v, out int column)
    {
        column = MarkRecipe.Carve0;
        if (v < 0f || v >= BeatMs) return 0f;
        var step = Recipe.CarveStepMs;
        if (v < step * 3f)
        {
            column = MarkRecipe.Carve0 + Math.Min(2, (int)(v / step));
            return 1f;
        }
        column = MarkRecipe.Carve0 + 2;
        return Fall((v - step * 3f) / Recipe.DeepenEdgeMs);
    }

    private static float Fall(float t) => t >= 1f ? 0f : (1f - Math.Max(0f, t)) * (1f - Math.Max(0f, t));

    /// <summary>The scale a coil takes on a body of this canonical visible height: a share of it, snapped to thirds.</summary>
    public float ScaleFor(float bodyHeight)
    {
        var s = Recipe.SizeShare * bodyHeight / Recipe.CoilBox;
        s = MathF.Round(s * 3f) / 3f;
        return Math.Clamp(s, Recipe.MinScale, Recipe.MaxScale);
    }

    /// <summary>The torso a creature was last pinned at (arena px), if it has been drawn.</summary>
    public bool TryAnchor(int slot, out Vector2 anchor, out float scale)
    {
        anchor = default;
        scale = 1f;
        if (slot < 0 || slot >= _pinned.Length || !_pinned[slot]) return false;
        anchor = _anchor[slot];
        scale = _scale[slot];
        return true;
    }

    // ── DRAWING ────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ON THE BODY: pins the creature's torso from the frame just drawn (every creature, marked or not, so a migration
    /// knows where it lands), then, if it carries the coil, draws its halo (as ink, then as smoke), its cut, and a beat's
    /// lit line. Called in the creature loop right after the creature, before its hit flash.
    /// </summary>
    public void DrawOn(SpriteBatch b, Texture2D? atlas, int slot, float playheadMs, SpriteFrame frame, float bodyHeight, Vector2 squash,
                       Vector2? bodyPoint = null)
    {
        if (slot < 0 || slot >= _pinned.Length) return;
        var r = Recipe;
        Pin(slot, frame, bodyHeight, bodyPoint);
        if (atlas is null) return;
        var phase = TryPhase(slot, playheadMs, out var u);
        if (phase == MarkPhase.None) return;
        var stage = DrawnStage(slot, playheadMs);
        var column = ColumnAt(slot, phase, u, playheadMs);
        var sq = squash == default ? Vector2.One : squash;
        var dest = CellDest(_anchor[slot], _scale[slot] * sq.X, _scale[slot] * sq.Y);
        var halo = CellSrc(r.HaloRow0 + stage, column);
        if (phase == MarkPhase.Waiting)
        {
            // Core already amplifies it: a dark ink stain gathering on it, thickening until the mark seeps in
            var ramp = Math.Clamp(1f + u / r.WaitingRampMs, r.WaitingFloor, 1f);
            b.Draw(atlas, dest, halo, r.Ink * (r.InkAlpha * r.WaitingSmokeAlpha * ramp));
            SpriteCount++;
            return;
        }
        b.Draw(atlas, dest, halo, r.Ink * r.InkAlpha);
        b.Draw(atlas, dest, halo, r.Smoke * r.SmokeAlpha);
        b.Draw(atlas, dest, CellSrc(stage, column), r.Groove);
        SpriteCount += 3;
        var hot = HotAt(slot, playheadMs, out var hotColumn, out var hotStage);
        if (hot > 0.01f && column >= MarkRecipe.Idle0 && column < MarkRecipe.Form0)
        {
            // a chisel's cells are the stage it cuts TO (each carve cell is that stage's new cut), whatever the rest shows
            b.Draw(atlas, dest, CellSrc(hotStage, hotColumn), r.Hot * hot);
            SpriteCount++;
        }
        LastHosts++;
        LastStage = Math.Max(LastStage, stage);
    }

    /// <summary>
    /// Pins <paramref name="slot"/>'s torso from the frame just drawn for it: the AUTHORED body point (MarkPoints: on the
    /// torso, the halo off the head, the coil inside the silhouette), else the torso probe. A falling host is pinned from its
    /// death clip at its standing torso, so the coil comes apart and the strand leaves from the body, not from where a
    /// lunge last put it.
    /// </summary>
    public void Pin(int slot, SpriteFrame frame, float bodyHeight, Vector2? bodyPoint)
    {
        if (slot < 0 || slot >= _pinned.Length) return;
        _scale[slot] = ScaleFor(bodyHeight);
        _anchor[slot] = bodyPoint ?? SilhouetteProbe.Torso(frame, facesLeft: true, Recipe.TorsoFrom, Recipe.TorsoTo) ?? frame.Dest.Center.ToVector2();
        _pinned[slot] = true;
    }

    /// <summary>
    /// BETWEEN THE BODIES, after the creature row: the coil coming apart on a fallen host (at its last pinned torso) and
    /// the smoke carrying a migrating (or, under SPRAWL, spreading) mark along a low arc to the new coil's tip.
    /// </summary>
    public void DrawLoose(SpriteBatch b, Texture2D? atlas, float playheadMs)
    {
        if (atlas is null || _firstFront < 0) return;
        var r = Recipe;
        for (var s = 0; s < _deathAt.Length; s++)
        {
            var fell = _deathAt[s];
            var u = playheadMs - fell;
            if (float.IsPositiveInfinity(fell) || u < 0f || u >= r.LoosenMs || !_pinned[s] || !Formed(s, fell - 0.5f)) continue;
            var half = r.LoosenMs * 0.5f;
            var column = u < half ? MarkRecipe.Loosen0 : MarkRecipe.Loosen0 + 1;
            var a = u < half ? 1f : 1f - (u - half) / half;
            var stage = DrawnStage(s, fell - 0.5f);        // what it SHOWED as it fell, never a depth it never showed
            var dest = CellDest(_anchor[s], _scale[s], _scale[s]);
            b.Draw(atlas, dest, CellSrc(r.HaloRow0 + stage, column), r.Ink * (r.InkAlpha * a));
            b.Draw(atlas, dest, CellSrc(r.HaloRow0 + stage, column), r.Smoke * (r.SmokeAlpha * a));
            b.Draw(atlas, dest, CellSrc(stage, column), r.Groove * a);
            SpriteCount += 3;
        }
        if (_wholeWave)
        {
            for (var s = 0; s < _deathAt.Length; s++)
                if (_hopFrom[s] >= 0 && Standing(s, _hopLand[s]) && Standing(_hopFrom[s], _hopAt[s]))
                    DrawFlight(b, atlas, _hopFrom[s], s, playheadMs - _hopAt[s], _hopLand[s] - _hopAt[s]);
            return;
        }
        if (!r.TransferThread) return;   // a transfer draws no bridge: the old mark collapses, the new one seeps in
        for (var k = 1; k < _hostCount; k++)
            DrawFlight(b, atlas, _hostFromSlot[k], _hostSlot[k], playheadMs - (_hostFall[k] + r.FlightFromMs), r.FlightMs);
    }

    /// <summary>Was the coil whole on <paramref name="slot"/> at this moment (so its fall takes it apart)?</summary>
    private bool Formed(int slot, float playheadMs)
    {
        var phase = TryPhase(slot, playheadMs, out var u);
        return phase == MarkPhase.Reform || (phase == MarkPhase.Apply && u >= Recipe.FormMs);
    }

    /// <summary>The smoke from one torso to another coil's TIP, <paramref name="u"/> ms into a flight of
    /// <paramref name="flightMs"/>: ONE strand of overlapping puffs (each a puff's VISIBLE width behind the one before at
    /// the strand's peak speed), along a path that SAGS toward the tip, below the creatures' heads and eyes (a lift carried
    /// it across the next creature's face, the one path that read as something shot at it); no head, no fading tail.</summary>
    private void DrawFlight(SpriteBatch b, Texture2D atlas, int from, int to, float u, float flightMs)
    {
        var r = Recipe;
        if (u < 0f || from < 0 || !_pinned[from] || !_pinned[to]) return;
        var a = _anchor[from];
        var distance = Math.Max(1f, Vector2.Distance(a, _anchor[to] + (r.Tip - r.Centre) * _scale[to]));
        var size = r.ThreadCell * _scale[from];
        var lag = BeadLag(distance, flightMs, _scale[from]);
        if (u >= flightMs + lag * (r.Beads - 1)) return;
        for (var k = 0; k < r.Beads; k++)
        {
            var t = (u - k * lag) / flightMs;
            if (t <= 0f || t >= 1f) continue;
            t = t * t * (3f - 2f * t);   // a migration eases out and in
            var p = FlightPoint(a, _anchor[to], _scale[to], t);
            // the trailing half thins, so the strand breaks up behind the lead rather than holding one width
            var puff = size * (k < r.Beads / 2 ? 1f : 0.85f);
            var dest = new Rectangle((int)MathF.Round(p.X - puff / 2), (int)MathF.Round(p.Y - puff / 2),
                                     (int)MathF.Round(puff), (int)MathF.Round(puff));
            // tapered: the lead and the last puff are the small cell, the thread's body the full one throughout (no head,
            // no tail; alternating full and small cells read as a chain of squares with waists)
            var cell = k == 0 || k == r.Beads - 1 ? 1 : 0;
            // each puff's own density (deterministic by its index): overlaps build smoke, never one flat violet slab
            var density = 0.55f + 0.2f * ((k * 37) % 5) / 4f;
            b.Draw(atlas, dest, new Rectangle(cell * r.ThreadCell, r.ThreadY, r.ThreadCell, r.ThreadCell), r.Groove * density);
            SpriteCount++;
        }
    }

    /// <summary>The time between two puffs of a strand over <paramref name="distance"/> px: at the eased path's peak speed
    /// (1.5 x distance / flight) they are 0.8 of a puff's VISIBLE width apart, so they overlap into one thread.</summary>
    internal float BeadLag(float distance, float flightMs, float scale)
    {
        var visible = Recipe.ThreadCell * scale * Recipe.ThreadVisibleShare;
        return Math.Clamp(flightMs * visible * 0.8f / (1.5f * Math.Max(1f, distance)), 1f, 40f);
    }

    /// <summary>The strand's path at <paramref name="t"/> (0..1, eased) from one creature's torso to another's coil tip:
    /// the path the flight draws (for the tests: it never rises above the higher of its two ends).</summary>
    public Vector2 FlightPoint(Vector2 fromAnchor, Vector2 toAnchor, float toScale, float t)
    {
        var r = Recipe;
        var c = toAnchor + (r.Tip - r.Centre) * toScale;
        var sag = Math.Clamp(r.ArcLift * Vector2.Distance(fromAnchor, c), r.ArcMin, r.ArcMax);
        var mid = (fromAnchor + c) * 0.5f + new Vector2(0f, sag * 0.5f);
        return (1 - t) * (1 - t) * fromAnchor + 2 * (1 - t) * t * mid + t * t * c;
    }

    private Rectangle CellSrc(int row, int column) => new(column * Recipe.Cell, row * Recipe.Cell, Recipe.Cell, Recipe.Cell);

    private Rectangle CellDest(Vector2 anchor, float sx, float sy)
        => new((int)MathF.Round(anchor.X - Recipe.Centre.X * sx), (int)MathF.Round(anchor.Y - Recipe.Centre.Y * sy),
               (int)MathF.Round(Recipe.Cell * sx), (int)MathF.Round(Recipe.Cell * sy));
}
