using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>Where the mark on a creature stands at one moment (see <see cref="MarkPerformance.TryPhase"/>).</summary>
public enum MarkPhase
{
    /// <summary>No mark.</summary>
    None,

    /// <summary>Applied on the wave's first tick: the curse gathers, then blooms (u from the tick).</summary>
    Apply,

    /// <summary>The mark is still on its way to this creature, which Core already amplifies: it carries a faint shade (u
    /// negative, to the landing).</summary>
    Waiting,

    /// <summary>The mark arrived (a transfer, a SPRAWL hop): every territory it carries blooms, then rests (u from the
    /// landing).</summary>
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
/// THE TRUTH ONLY (ADR-013 §8): the mark is DRAWN by the curse (<see cref="Curse.CursePresentation"/>), which reads its
/// phases, shown stages, SPRAWL schedule and quiet windows here; every creature's body point is pinned here
/// (<see cref="Pin"/>) for the curse's territories to hang from. The etched cut that used to be drawn here is gone.
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

    // THE PINS: every creature's body point, from the frame drawn this frame (or the last one it was drawn)
    private readonly Vector2[] _anchor;
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

    /// <summary>Was the tick nearest <paramref name="atMs"/> (within <paramref name="withinMs"/>) a QUIET one: beside an
    /// action, a presented reaction or the field's crush, where the mark gives way?</summary>
    internal bool TickQuietNear(float atMs, float withinMs)
    {
        var best = withinMs;
        var quiet = false;
        for (var i = 0; i < _tickAt.Length; i++)
        {
            var d = Math.Abs(_tickAt[i] - atMs);
            if (d <= best) { best = d; quiet = _tickQuiet[i]; }
        }
        return quiet;
    }

    /// <summary>The curse's draw on one host counted as this mark's (the trace's <c>mark-draw</c>): its sprites so far
    /// this frame, one more host, the deepest stage drawn.</summary>
    internal void CountCurse(int sprites, int stage)
    {
        SpriteCount = sprites;
        LastHosts++;
        LastStage = Math.Max(LastStage, stage);
    }

    /// <summary>The curse's sprites so far this frame, from a falling host's leaving (not a host any more).</summary>
    internal void CountCurseSprites(int sprites) => SpriteCount = Math.Max(SpriteCount, sprites);

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

    /// <summary>
    /// When the creature in <paramref name="slot"/> began WAITING for the mark it carries at <paramref name="playheadMs"/>
    /// (SPRAWL: the first tick; a transfer: the fall that sent it), +inf when it never waited (applied, or no mark). The
    /// curse's faint waiting shade rises from here (<see cref="MarkRecipe.WaitingRampMs"/>) instead of in one frame.
    /// </summary>
    internal float WaitingFrom(int slot, float playheadMs)
    {
        if (_firstFront < 0) return float.PositiveInfinity;
        if (_wholeWave) return slot == _firstFront ? float.PositiveInfinity : FirstTickMs;
        for (var k = _hostCount - 1; k >= 0; k--)
        {
            if (_hostSlot[k] != slot) continue;
            return k == 0 || playheadMs < _hostFall[k] ? float.PositiveInfinity : _hostFall[k];
        }
        return float.PositiveInfinity;
    }

    // ── THE SCHEDULE, READ ONCE AT BEGINWAVE (the voice, MarkVoice: never per frame) ────────────────────────────────

    /// <summary>The creature slots this mark was built for.</summary>
    internal int CreatureSlots => _deathAt.Length;

    /// <summary>SPRAWL (every creature carries it) rather than one front host at a time.</summary>
    internal bool WholeWave => _wholeWave;

    /// <summary>The creature the mark is applied to on the first tick (-1: this wave has none).</summary>
    internal int FirstFront => _firstFront;

    /// <summary>Front mode: the hosts the mark is carried by, in order (0: the applied one).</summary>
    internal int HostCount => _wholeWave ? 0 : _hostCount;

    /// <summary>Front mode: host <paramref name="k"/>'s creature slot.</summary>
    internal int HostSlotAt(int k) => _hostSlot[k];

    /// <summary>Front mode: when host <paramref name="k"/> received the mark (k = 0: the first tick; else the landing).</summary>
    internal float HostLandsAt(int k) => _landAt[k];

    /// <summary>SPRAWL: when the hop to <paramref name="slot"/> lands (+inf: it never does).</summary>
    internal float HopLandsAt(int slot) => slot >= 0 && slot < _hopLand.Length ? _hopLand[slot] : float.PositiveInfinity;

    /// <summary>SPRAWL: <paramref name="slot"/>'s place in the spread (0 the source, 1 the first creature it reached...).</summary>
    internal int ChainOrderOf(int slot) => slot >= 0 && slot < _chainOrder.Length ? _chainOrder[slot] : 0;

    /// <summary>
    /// A moment past which no coil's SHOWN stage can change any more (every tick's chisel, every catch-up and ANCHOR's
    /// front cut are behind it): the voice scans the shown stages up to here, never further.
    /// </summary>
    internal float ShownSettledBy
    {
        get
        {
            var reveal = Recipe.CarveStepMs * 2f;
            var ripple = _wholeWave ? _deathAt.Length * Recipe.CarveStepMs * 1.5f : 0f;
            var end = FirstTickMs;
            for (var i = 0; i < _tickAt.Length; i++) end = Math.Max(end, _tickAt[i] + _tickSettle[i] + ripple + reveal);
            for (var k = 0; k < _hostCount; k++) end = Math.Max(end, Math.Max(_catchAt[k], _landAt[k] + Recipe.ReformMs) + reveal);
            for (var s = 0; s < _deathAt.Length; s++)
            {
                if (!float.IsPositiveInfinity(_hopCatch[s])) end = Math.Max(end, _hopCatch[s] + reveal);
                if (!float.IsPositiveInfinity(_deathAt[s])) end = Math.Max(end, _deathAt[s] + reveal);
            }
            return end + 1f;
        }
    }

    /// <summary>SPRAWL: the creature the hop to <paramref name="slot"/> leaves from (-1: none, the coil forms with no strand).</summary>
    internal int HopFromOf(int slot) => slot >= 0 && slot < _hopFrom.Length ? _hopFrom[slot] : -1;

    /// <summary>SPRAWL: when the hop to <paramref name="slot"/> leaves (+inf: it never does).</summary>
    internal float HopLeavesAt(int slot) => slot >= 0 && slot < _hopAt.Length ? _hopAt[slot] : float.PositiveInfinity;

    /// <summary>Does the creature in <paramref name="slot"/> carry the mark at <paramref name="playheadMs"/> (Core's truth
    /// as the screen draws it: applied, waiting for the smoke, re-formed)?</summary>
    public bool Carries(int slot, float playheadMs) => TryPhase(slot, playheadMs, out _) != MarkPhase.None;

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

    /// <summary>The body point a creature was last pinned at (arena px), if it has been drawn.</summary>
    public bool TryAnchor(int slot, out Vector2 anchor)
    {
        anchor = default;
        if (slot < 0 || slot >= _pinned.Length || !_pinned[slot]) return false;
        anchor = _anchor[slot];
        return true;
    }

    /// <summary>
    /// Pins <paramref name="slot"/>'s body point from the frame just drawn for it (every creature, marked or not, so a
    /// migration knows where it lands): the AUTHORED body point of that frame (MarkPoints: every creature strip has one,
    /// brand_mark_test), else the frame's centre; never a pixel read back from the texture (ADR-013). A falling host is
    /// pinned from its death clip's own point, so the curse leaves from the falling body, not from where a lunge last put
    /// it. The curse's territories hang from this point.
    /// </summary>
    public void Pin(int slot, SpriteFrame frame, Vector2? bodyPoint)
    {
        if (slot < 0 || slot >= _pinned.Length) return;
        _anchor[slot] = bodyPoint ?? frame.Dest.Center.ToVector2();
        _pinned[slot] = true;
    }
}
