using System;
using System.Diagnostics;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static IdleXIdle.Game.Presentation.Curse.CurseMaterial;

namespace IdleXIdle.Game.Presentation.Curse;

/// <summary>
/// BRAND AS A CURSE, the production path (ADR-013; the owner-approved look of 39b59aaa, "Living Shadow Corruption"):
/// separate infected TERRITORIES of the victim's body, its own material drained (Shadow Violet on a contrasting host,
/// Ash-Burn on a dark, violet or conflicting one), depth told by how much of the body is infected (one, two, then three
/// or four territories), Shadow wisps leaking from them. One instance lives as long as the hunt screen; each wave hands
/// it that wave's <see cref="MarkPerformance"/> (<see cref="BeginWave"/>), whose truth it presents unchanged.
/// </summary>
/// <remarks>
/// <para>
/// ONE SHADER PASS PER AFFLICTED CREATURE (ADR-013 §1): at each cursed creature the arena batch is ended, the
/// creature's CURRENT frame is drawn through <see cref="BrandCurseEffect"/> with that creature's parameters (its baked
/// host look, up to four territories, a deepen's travelling puffs), and the arena batch is reopened in its own state.
/// The pass IS the creature's draw: the arena draws that frame transparent (<see cref="PassDue"/>,
/// <see cref="LeavingPassDue"/>), so no soft texel is composited twice; the wisps then go into the reopened arena batch from the packed atlas's puff cell (one texture: they merge).
/// A dying host is drawn the same way with its leaving levels and smokes out. A creature still waiting for the mark
/// takes its faint shade through its own draw tint (<see cref="Shade"/>): no batch at all.
/// </para>
/// <para>
/// NOTHING IS READ BACK: the host's look and territory seats are baked offline (<see cref="CurseHostData"/>), the masks
/// were uploaded once at load (<see cref="CurseAtlas"/>); the curse never reads a texture's pixels.
/// </para>
/// <para>
/// PURE COMPOSITION, THEN SUBMISSION: <see cref="ComposeLiving"/> and <see cref="ComposeLeaving"/> compute every
/// parameter and streak from the playhead without a device (the tests drive them), and only <see cref="DrawOn"/> /
/// <see cref="DrawLeaving"/> touch the batch. The same moment always composes the same picture, whatever the frame
/// times or the order of the moments asked (a seek, a rewind).
/// </para>
/// <para>
/// Allocation-free per frame: the per-slot tables grow only when a wave has more creatures than any before it, each
/// slot's timeline is reused across waves, and every per-frame value is a field reused or on the stack. Trace strings are
/// built only under <see cref="PresentTrace.Enabled"/>.
/// </para>
/// </remarks>
internal sealed class CursePresentation
{
    // ── TIMING (ms) ─────────────────────────────────────────────────────────────────────────────────────────────────
    private const float FlareMs = 700f;        // an apply / arrival / deepen flare's life (the body's shudder)
    /// <summary>A falling host: the curse leaving it (its flash, collapse and smoke-out), on the playhead.</summary>
    internal const float LeaveMs = 900f;
    private const float LeaveFlashMs = 150f;   // ... its light flaring once
    private const float LeaveCollapseMs = 600f;   // ... then its territories collapsing toward their seats
    private const float DeepenLookMs = 650f;   // how far back a shown-stage change still flares
    private const float SlotPhaseMs = 377f;    // each creature's idle motion at its own moment
    private const float SmokeInShare = 0.12f;  // a smoke-out streak eases in over this share of its climb

    // ── LIGHT ───────────────────────────────────────────────────────────────────────────────────────────────────────
    private const float QuietShare = 0.55f;    // beside an action, a reaction or the field's crush
    private const float ArrivalShare = 0.55f;  // a transfer's / a hop's awakening, against the apply
    private const float TickQuietNearMs = 450f;

    /// <summary>The faint shade a creature the mark is still on its way to takes through its draw tint (the prototype's
    /// black at 0.05 multiplied over it).</summary>
    internal const float WaitingShade = 0.95f;

    /// <summary>The waiting shade rises in over this from the moment the creature began to wait.</summary>
    internal const float WaitingRiseMs = 120f;

    /// <summary>Streaks composed per creature at most (wisps: three per territory, smoke and light each).</summary>
    internal const int MaxStreaks = 32;

    private readonly BrandCurseEffect? _fx;

    // per slot (grown when a wave has more creatures than any before it, never per frame)
    private float[] _cacheAt = Array.Empty<float>();
    private byte[] _kind = Array.Empty<byte>();          // 0 none, 1 waiting, 2 carrying
    private float[] _waitShade = Array.Empty<float>();   // the faint waiting shade in the draw tint (0..1 - WaitingShade)
    private float[] _shadeIn = Array.Empty<float>();     // how far the carrying body shade has risen (0..1)
    private int[] _stage = Array.Empty<int>();
    private float[] _flare = Array.Empty<float>();       // 0..1 envelope of the current flare
    private float[] _flareAge = Array.Empty<float>();    // ms since the flare began
    private Rectangle[] _body = Array.Empty<Rectangle>();
    private CurseHostData?[] _host = Array.Empty<CurseHostData?>();
    private bool[] _missing = Array.Empty<bool>();       // carrying, but no baked host data: the faint shade only
    private bool[] _seatTraced = Array.Empty<bool>();
    private CurseTimeline[] _timeline = Array.Empty<CurseTimeline>();
    private int _slots;

    /// <summary>The wave's mark, whose truth this curse presents (null before the first wave).</summary>
    public MarkPerformance? Mark { get; private set; }

    // ── THE LAST COMPOSITION (the shader's parameters for one creature; the tests read them) ──────────────────────────

    /// <summary>The composed creature's host constants.</summary>
    internal HostParams Host;

    /// <summary>The composed creature's territories (every one, used or not: an unused one draws nothing).</summary>
    internal readonly TerritoryParams[] Territories = new TerritoryParams[ShaderTerritories];

    /// <summary>Territories shown in the last composition.</summary>
    internal int TerritoryCount;

    /// <summary>The composed deepen's travelling puffs (radius 0: none).</summary>
    internal readonly PuffParams[] Puffs = new PuffParams[MaxPuffs];

    /// <summary>The composed frame's host-UV to screen affine.</summary>
    internal Vector4 UvToPx;

    /// <summary>A wisp or a smoke-out streak: the puff stretched over <see cref="Dest"/>, premultiplied
    /// <see cref="Colour"/>; a <see cref="Light"/> streak is the emission core, drawn additively (alpha 0).</summary>
    internal readonly record struct Streak(Rectangle Dest, Color Colour, bool Light);

    /// <summary>The composed streaks (the first <see cref="StreakCount"/>).</summary>
    internal readonly Streak[] Streaks = new Streak[MaxStreaks];

    /// <summary>Streaks composed.</summary>
    internal int StreakCount;

    // ── THE TRACE (this frame; <c>mark-draw</c>) ───────────────────────────────────────────────────────────────────

    /// <summary>Sprites drawn this frame (each curse pass and each streak).</summary>
    public int SpriteCount { get; private set; }

    /// <summary>Draw calls of the curse's own passes this frame (measured), plus one per creature whose wisps joined the
    /// reopened arena batch (they are flushed with it as one run of the atlas). Counted only under the trace.</summary>
    public long Draws { get; private set; }

    /// <summary>Batch boundaries the curse added this frame: each pass's Begin/End pair and the arena split it needs.
    /// Counted only under the trace.</summary>
    public int Batches { get; private set; }

    /// <summary>Stopwatch ticks of CPU time spent in the curse's code this frame, the arena flushes it forces included.
    /// Counted only under the trace.</summary>
    public long Ticks { get; private set; }

    /// <summary>Of <see cref="Ticks"/>, the time spent flushing the arena batch the curse splits (the sprites drawn before
    /// the cursed creature, which the arena would have flushed later anyway). Counted only under the trace.</summary>
    public long ArenaFlushTicks { get; private set; }

    private CursePresentation(BrandCurseEffect? fx) => _fx = fx;

    // the shader and its atlas, loaded once per device (a rebuilt hunt screen reuses them; the device disposes them)
    private static BrandCurseEffect? _loaded;

    /// <summary>The curse with its shader and atlas (loaded once per device; load time, never a combat frame).</summary>
    public static CursePresentation Load(GraphicsDevice device)
    {
        if (_loaded is null || !_loaded.IsFor(device))
        {
            _loaded = BrandCurseEffect.Load(device);
            Rehearse();
        }
        return new CursePresentation(_loaded);
    }

    /// <summary>
    /// Composes a short made-up curse once, device-free, at load: an apply, a two-step deepen with its travel, the wisps,
    /// a fall and its smoke-out on a four-seat host. Measured: the first cursed frame of a run spent ~8 ms in the
    /// just-in-time compiler (the first bloom and the first fall ~1 ms more each); this pays it here, never on a combat
    /// frame. Nothing it builds is kept.
    /// </summary>
    internal static void Rehearse()
    {
        var lay = new Layout { Available = CurseSeating.MaxTerritories };
        for (var k = 0; k < lay.Available; k++)
        {
            lay.Offset[k] = new Vector2(k * 20f - 30f, k * 25f);
            lay.Diameter[k] = 90f - 10f * k;
            lay.Burn[k] = k == 1 ? 1f : 0f;
        }
        var look = new HostLook(0.3f, 0.2f, 0f, 0.3f);
        var data = new CurseHostData(look, CurseHost.AshBurn(look), 256, 8, new Rectangle(40, 30, 180, 200), new Vector2(128, 140), lay, lay);
        var mark = new MarkPerformance(MarkRecipes.SeekerBrand, 0, new[] { (100f, 120, false), (300f, 220, true) }, new[] { (1300f, 0) }, 2,
                                       wholeWave: true, frontFullPercent: 0);
        var frame = new SpriteFrame(null!, new Rectangle(0, 0, 256, 256), new Rectangle(400, 300, 256, 256), SpriteEffects.FlipHorizontally);
        var body = new Rectangle(420, 330, 200, 220);
        var c = Headless();
        c.BeginWave(mark, 2);
        for (var s = 0; s < 2; s++) mark.Pin(s, frame, body.Center.ToVector2());
        for (var p = -300f; p < 2400f; p += 25f)
            for (var s = 0; s < 2; s++)
            {
                c.Convulse(s, p);
                c.Shade(s, p, Color.White);
                if (c.StillLeaving(p)) c.LeavingPassDue(s, p, data);
                c.PassDue(s, p, data);
                if (!c.ComposeLeaving(s, p, frame, 2048, 256, data, out _)) c.ComposeLiving(s, p, frame, 2048, 256, body, data);
            }
    }

    /// <summary>A curse without a device: composition only (the tests).</summary>
    internal static CursePresentation Headless() => new(null);

    /// <summary>
    /// A new wave: its mark and creature count. The per-slot tables are reused (they grow only past the largest wave so
    /// far) and every slot's state and timeline is forgotten.
    /// </summary>
    public void BeginWave(MarkPerformance mark, int slots)
    {
        Mark = mark;
        var n = Math.Max(1, slots);
        if (n > _cacheAt.Length)
        {
            Array.Resize(ref _cacheAt, n);
            Array.Resize(ref _kind, n);
            Array.Resize(ref _waitShade, n);
            Array.Resize(ref _shadeIn, n);
            Array.Resize(ref _stage, n);
            Array.Resize(ref _flare, n);
            Array.Resize(ref _flareAge, n);
            Array.Resize(ref _body, n);
            Array.Resize(ref _host, n);
            Array.Resize(ref _missing, n);
            Array.Resize(ref _seatTraced, n);
            var old = _timeline.Length;
            Array.Resize(ref _timeline, n);
            for (var i = old; i < n; i++) _timeline[i] = new CurseTimeline();
        }
        _slots = n;
        for (var i = 0; i < n; i++)
        {
            _cacheAt[i] = float.NaN;
            _kind[i] = 0;
            _waitShade[i] = _shadeIn[i] = 0f;
            _stage[i] = 0;
            _flare[i] = _flareAge[i] = 0f;
            _body[i] = Rectangle.Empty;
            _host[i] = null;
            _missing[i] = _seatTraced[i] = false;
            _timeline[i].Reset();
        }
    }

    /// <summary>Starts a frame's counters.</summary>
    public void BeginFrame()
    {
        SpriteCount = 0;
        Draws = 0;
        Batches = 0;
        Ticks = 0;
        ArenaFlushTicks = 0;
    }

    // ── THE STATE ON THE PLAYHEAD ─────────────────────────────────────────────────────────────────────────────────

    private bool Valid(int slot) => Mark is not null && slot >= 0 && slot < _slots;

    private void Prepare(int slot, float t)
    {
        if (_cacheAt[slot] == t) return;
        _cacheAt[slot] = t;
        _flare[slot] = 0f;
        _flareAge[slot] = 0f;
        _waitShade[slot] = 0f;
        _shadeIn[slot] = 0f;
        var mark = Mark!;
        var phase = mark.TryPhase(slot, t, out var u);
        if (phase == MarkPhase.None) { _kind[slot] = 0; return; }
        // THE SHADES NEVER STEP: the faint waiting shade rises in (WaitingRiseMs) from the moment the creature began to
        // wait; on the arrival it hands over to the carrying body shade across the first territory's bloom (the two are
        // about equally dark), and an apply's body shade rises through the gather (it used to darken in one frame)
        var waitFrom = mark.WaitingFrom(slot, t);
        if (phase == MarkPhase.Waiting)
        {
            _kind[slot] = 1;
            _stage[slot] = 0;
            _waitShade[slot] = (1f - WaitingShade) * Rise(t - waitFrom, WaitingRiseMs);
            return;
        }
        _kind[slot] = 2;
        if (phase == MarkPhase.Apply)
        {
            var gather = -mark.Recipe.GatherFromMs;
            _shadeIn[slot] = gather > 0f ? Rise(u + gather, gather) : 1f;
        }
        else
        {
            var handover = Rise(u, CurseTimeline.ArriveGrowMs);
            _shadeIn[slot] = handover;
            _waitShade[slot] = (1f - WaitingShade) * Rise(t - u - waitFrom, WaitingRiseMs) * (1f - handover);
        }
        var stage = CurseTimeline.StageOn(mark, slot, t);
        _stage[slot] = stage;
        // (from its gather, u < 0, nothing flares until the bloom: the envelope is 0 there)
        if (u < FlareMs)
        {
            _flareAge[slot] = u;
            _flare[slot] = Envelope(u);
        }
        // A DEEPEN: the shown stage stepped up within the last DeepenLookMs (MarkPerformance reveals it after its chisel)
        for (var k = 1; k * 50f <= DeepenLookMs; k++)
        {
            var back = t - k * 50f;
            if (!mark.Carries(slot, back)) break;
            if (CurseTimeline.StageOn(mark, slot, back) < stage)
            {
                // the step's own moment, to under 2 ms (a 50 ms bin animated the whole deepen at 20 Hz)
                float lo = back, hi = back + 50f;
                for (var it = 0; it < 5; it++)
                {
                    var mid = 0.5f * (lo + hi);
                    if (mark.Carries(slot, mid) && CurseTimeline.StageOn(mark, slot, mid) < stage) lo = mid;
                    else hi = mid;
                }
                var age = t - hi;
                var e = Envelope(age);
                if (e > _flare[slot]) { _flare[slot] = e; _flareAge[slot] = age; }
                break;
            }
        }
    }

    /// <summary>0..1, smoothly, over <paramref name="ms"/> from <paramref name="age"/> 0 (+inf age: risen; NaN: none).</summary>
    private static float Rise(float age, float ms)
        => float.IsNaN(age) ? 1f : CurseTimeline.Smooth(Math.Clamp(age / ms, 0f, 1f));

    /// <summary>0..1: a fast rise (60 ms), a short hold, a long fall.</summary>
    private static float Envelope(float age)
    {
        if (age < 0f) return 0f;
        if (age < 60f) return age / 60f;
        if (age < 160f) return 1f;
        var f = 1f - (age - 160f) / (FlareMs - 160f);
        return f <= 0f ? 0f : f * f;
    }

    /// <summary>The body's own reaction to the curse flaring: a shudder, two quick tremors, the body tightening on the
    /// pulse (multiplied into the creature's squash before it is drawn; every creature path, the boss's too).</summary>
    public Vector2 Convulse(int slot, float playheadMs)
    {
        if (!Valid(slot)) return Vector2.One;
        var t0 = PresentTrace.Enabled ? Stopwatch.GetTimestamp() : 0L;
        Prepare(slot, playheadMs);
        var f = _flare[slot];
        var squash = Vector2.One;
        if (f > 0.01f)
        {
            var s = MathF.Sin(_flareAge[slot] * 0.055f) * f;
            squash = new Vector2(1f + 0.025f * s, 1f - 0.03f * MathF.Abs(s));
        }
        if (PresentTrace.Enabled) Ticks += Stopwatch.GetTimestamp() - t0;
        return squash;
    }

    /// <summary>
    /// The creature's draw tint with the curse's faint shade folded in: a creature the mark is still on its way to (a
    /// SPRAWL hop, a transfer in flight) is darkened a little, through its own draw (no batch); any other is unchanged.
    /// </summary>
    public Color Shade(int slot, float playheadMs, Color tint)
    {
        if (!Valid(slot)) return tint;
        var f = 1f - ShadeOf(slot, playheadMs);
        if (f >= 1f) return tint;
        return new Color((int)MathF.Round(tint.R * f), (int)MathF.Round(tint.G * f), (int)MathF.Round(tint.B * f), tint.A);
    }

    /// <summary>How much the faint waiting shade darkens <paramref name="slot"/>'s draw tint (0 .. 1 - <see cref="WaitingShade"/>).</summary>
    internal float ShadeOf(int slot, float playheadMs)
    {
        if (!Valid(slot)) return 0f;
        Prepare(slot, playheadMs);
        return _kind[slot] == 2 && _missing[slot] ? 1f - WaitingShade : _waitShade[slot];
    }

    /// <summary>This slot's timeline (for the tests: what <see cref="ComposeLiving"/> replayed).</summary>
    internal CurseTimeline TimelineOf(int slot) => _timeline[slot];

    // ── WHO DRAWS THE CREATURE ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Will the curse pass DRAW the living creature in <paramref name="slot"/> at <paramref name="playheadMs"/>, given its
    /// baked host <paramref name="data"/>? Exactly when <see cref="ComposeLiving"/> composes it. Then the pass is the
    /// creature's only draw and the arena must not draw it too (it draws it transparent, to keep its frame): a second draw
    /// composited every soft texel twice (an anti-aliased rim at 1 - (1 - a)^2, popping as the pass began and ended).
    /// </summary>
    public bool PassDue(int slot, float playheadMs, CurseHostData? data)
    {
        if (!Valid(slot) || data is null) return false;
        Prepare(slot, playheadMs);
        if (_kind[slot] != 2) return false;
        return _timeline[slot].ClockAt(Mark!, slot, playheadMs, data.LayoutFor(slot).Available).Count > 0;
    }

    /// <summary>
    /// Will the curse pass DRAW the falling host in <paramref name="slot"/> at <paramref name="playheadMs"/> (its leaving's
    /// flare and collapse)? Exactly when <see cref="ComposeLeaving"/> hands back a pass; as <see cref="PassDue"/>, the arena
    /// must then not draw the death frame itself. <paramref name="data"/> as <see cref="DrawLeaving"/>'s.
    /// </summary>
    public bool LeavingPassDue(int slot, float playheadMs, CurseHostData? data)
    {
        if (!Valid(slot) || !TryLeaving(slot, playheadMs, out var died, out var age)) return false;
        data ??= _host[slot];
        if (data is null) return false;
        var count = Math.Min(_timeline[slot].ClockAt(Mark!, slot, died - 1f, data.LayoutFor(slot).Available).Count, ShaderTerritories);
        return LeavingFall(age) > 0.01f && count > 0;
    }

    /// <summary>
    /// Is the curse LEAVING any falling host at <paramref name="playheadMs"/> (its flare, collapse and smoke-out not yet
    /// over)? The screen keeps the playhead running through the wave-clear break while it is: the leaving is timed on the
    /// playhead, and a wave-ending kill's leaving froze mid-flash on the corpse for the whole break.
    /// </summary>
    public bool StillLeaving(float playheadMs)
    {
        if (Mark is null) return false;
        for (var s = 0; s < _slots; s++)
            if (TryLeaving(s, playheadMs, out _, out _)) return true;
        return false;
    }

    /// <summary>The curse leaving the host in <paramref name="slot"/>: it fell at <paramref name="died"/> carrying a territory
    /// (not only waiting for one) and <paramref name="age"/> is inside <see cref="LeaveMs"/>.</summary>
    private bool TryLeaving(int slot, float playheadMs, out float died, out float age)
    {
        var mark = Mark!;
        died = mark.DeathAt(slot);
        age = playheadMs - died;
        if (float.IsPositiveInfinity(died) || !mark.Carries(slot, died - 1f)) return false;
        // the curse never leaves a host that only waited for it (it wore no territory: a SPRAWL host that fell first)
        if (mark.TryPhase(slot, died - 1f, out _) == MarkPhase.Waiting) return false;
        return age >= 0f && age < LeaveMs;
    }

    /// <summary>How much of a falling host's curse still stands <paramref name="age"/> after the fall: whole through the
    /// flash, then collapsing.</summary>
    private static float LeavingFall(float age)
    {
        var collapse = age < LeaveFlashMs ? 0f : Math.Min(1f, (age - LeaveFlashMs) / LeaveCollapseMs);
        return 1f - CurseTimeline.Smooth(collapse);
    }

    // ── DRAWING ───────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The curse on the creature in <paramref name="slot"/>, drawn from <paramref name="frame"/> with <paramref name="tint"/>
    /// (the arena batch is open in <paramref name="raster"/>: it is ended, the curse pass runs, and it is reopened in the
    /// arena's own state). <paramref name="data"/> is the creature's baked host data (by its idle strip); without it the
    /// creature shows the faint shade only.
    /// </summary>
    /// <param name="replaced">
    /// The arena handed the creature's draw over (it drew the frame transparent, because <see cref="PassDue"/> said the pass
    /// would draw it). The pass runs only then, so no texel is ever composited twice; should no pass run after all, the
    /// plain creature is drawn here instead, so it never vanishes.
    /// </param>
    public void DrawOn(SpriteBatch b, int slot, float playheadMs, SpriteFrame frame, Rectangle body, Color tint, CurseHostData? data,
                       RasterizerState raster, bool replaced)
    {
        if (!Valid(slot))
        {
            if (replaced) DrawPlain(b, frame, tint);
            return;
        }
        var t0 = PresentTrace.Enabled ? Stopwatch.GetTimestamp() : 0L;
        _body[slot] = body;
        Prepare(slot, playheadMs);
        var composed = false;
        if (_kind[slot] != 0)
        {
            if (data is not null) _host[slot] = data;
            _missing[slot] = data is null;
            if (_kind[slot] == 2 && data is not null)
            {
                if (PresentTrace.Enabled && !_seatTraced[slot]) TraceSeat(slot, data);
                composed = ComposeLiving(slot, playheadMs, frame, frame.Texture.Width, frame.Texture.Height, body, data);
            }
        }
        Present(b, frame, tint, raster, composed, pass: composed, replaced);
        if (_kind[slot] != 0) Mark!.CountCurse(SpriteCount, _stage[slot]);
        if (PresentTrace.Enabled) Ticks += Stopwatch.GetTimestamp() - t0;
    }

    /// <summary>
    /// A FALLING host that carried the curse, on its death frame: its territories flare once, collapse back toward their
    /// seats in the same material, and smoke out. Nothing for a host that only waited (it wore no territory).
    /// <paramref name="data"/> is its baked host data (by its idle strip), when it was never drawn alive;
    /// <paramref name="replaced"/> as <see cref="DrawOn"/>'s (by <see cref="LeavingPassDue"/>).
    /// </summary>
    public void DrawLeaving(SpriteBatch b, int slot, float playheadMs, SpriteFrame frame, Color tint, CurseHostData? data,
                            RasterizerState raster, bool replaced)
    {
        if (!Valid(slot))
        {
            if (replaced) DrawPlain(b, frame, tint);
            return;
        }
        var t0 = PresentTrace.Enabled ? Stopwatch.GetTimestamp() : 0L;
        var composed = ComposeLeaving(slot, playheadMs, frame, frame.Texture.Width, frame.Texture.Height, data, out var pass);
        Present(b, frame, tint, raster, composed, pass, replaced);
        if (composed) Mark!.CountCurseSprites(SpriteCount);
        if (PresentTrace.Enabled) Ticks += Stopwatch.GetTimestamp() - t0;
    }

    /// <summary>
    /// The creature's one draw and the composed streaks: the curse pass when it was composed AND the arena handed the
    /// creature over (<paramref name="replaced"/>), else the plain creature when the arena did hand it over, else nothing
    /// of the creature (the arena drew it); then the streaks, over it.
    /// </summary>
    private void Present(SpriteBatch b, SpriteFrame frame, Color tint, RasterizerState raster, bool composed, bool pass, bool replaced)
    {
        pass &= composed && replaced;
        if (replaced && !pass) DrawPlain(b, frame, tint);
        if (composed) Submit(b, frame, tint, raster, pass);
    }

    /// <summary>The creature drawn plainly into the open arena batch, exactly as the arena draws it.</summary>
    private static void DrawPlain(SpriteBatch b, SpriteFrame frame, Color tint)
        => b.Draw(frame.Texture, frame.Dest, frame.Src, tint, 0f, Vector2.Zero, frame.Effects, 0f);

    /// <summary>
    /// The composed creature drawn: when <paramref name="pass"/>, the arena batch ended, the curse pass drawing the frame
    /// (the creature's only draw), the arena batch reopened; then the streaks into the (reopened) arena batch.
    /// </summary>
    private void Submit(SpriteBatch b, SpriteFrame frame, Color tint, RasterizerState raster, bool pass)
    {
        var fx = _fx ?? throw new InvalidOperationException("CursePresentation.Headless() composes only; it cannot draw");
        if (pass)
        {
            var device = b.GraphicsDevice;
            var flushFrom = PresentTrace.Enabled ? Stopwatch.GetTimestamp() : 0L;
            b.End();
            if (PresentTrace.Enabled) ArenaFlushTicks += Stopwatch.GetTimestamp() - flushFrom;
            var before = PresentTrace.Enabled ? device.Metrics.DrawCount : 0L;
            fx.SetHost(Host);
            fx.SetFrame(UvToPx);
            for (var k = 0; k < ShaderTerritories; k++) fx.SetTerritory(k, Territories[k]);
            for (var i = 0; i < MaxPuffs; i++) fx.SetPuff(i, Puffs[i]);
            fx.Commit();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, raster, fx.Effect);
            b.Draw(frame.Texture, frame.Dest, frame.Src, tint, 0f, Vector2.Zero, frame.Effects, 0f);
            b.End();
            if (PresentTrace.Enabled)
            {
                Draws += device.Metrics.DrawCount - before;
                Batches += 2;   // the curse's own pair, and the arena split it needs
            }
            SpriteCount++;
            // the arena batch, reopened exactly as HuntScreen opened it
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, raster);
        }
        if (StreakCount == 0) return;
        // the wisps' Shadow smoke over the body, then their light: one atlas, so they merge into one run of the batch
        for (var light = 0; light < 2; light++)
            for (var i = 0; i < StreakCount; i++)
            {
                ref readonly var s = ref Streaks[i];
                if (s.Light == (light == 1)) b.Draw(fx.Atlas, s.Dest, CurseAtlas.PuffSource, s.Colour);
            }
        SpriteCount += StreakCount;
        if (PresentTrace.Enabled) Draws++;
    }

    // ── THE COMPOSITION (pure: no device) ──────────────────────────────────────────────────────────────────────────

    /// <summary>A territory's atlas variant, turn and mirror on this slot (a row of one creature is never a stamp).</summary>
    internal static (int Variant, float Turn, bool Flip) Look(int slot, int k)
        => ((k + slot) % CurseAtlas.Variants, (CurseAtlas.Hash(slot * 31 + k * 7) - 0.5f) * 1.6f, CurseAtlas.Hash(slot * 17 + k * 13) > 0.5f);

    private static readonly TerritoryParams Empty = new() { InvSwell = 1f };

    /// <summary>
    /// A living host's curse at <paramref name="playheadMs"/>, composed into <see cref="Host"/>, <see cref="Territories"/>,
    /// <see cref="Puffs"/>, <see cref="UvToPx"/> and the wisps' <see cref="Streaks"/>, for the creature drawn from
    /// <paramref name="frame"/> (its texture <paramref name="texW"/> x <paramref name="texH"/>). False when it shows no
    /// territory yet (nothing to draw).
    /// </summary>
    internal bool ComposeLiving(int slot, float playheadMs, in SpriteFrame frame, int texW, int texH, Rectangle body, CurseHostData data)
    {
        var mark = Mark!;
        Prepare(slot, playheadMs);
        StreakCount = 0;
        TerritoryCount = 0;
        if (_kind[slot] != 2) return false;
        var lay = data.LayoutFor(slot);
        var look = data.Look;
        var clock = _timeline[slot].ClockAt(mark, slot, playheadMs, lay.Available);
        var count = Math.Min(clock.Count, ShaderTerritories);
        if (count == 0) return false;
        TerritoryCount = count;
        var anchor = mark.TryAnchor(slot, out var pinned) ? pinned : body.Center.ToVector2();
        var map = new CurseFrameMap(frame);
        var t = playheadMs + slot * SlotPhaseMs;
        var source = SourceGlow(slot, playheadMs);
        Span<Vector2> at = stackalloc Vector2[ShaderTerritories];
        Span<float> diameter = stackalloc float[ShaderTerritories];
        for (var k = 0; k < count; k++)
        {
            at[k] = map.At(lay, k, anchor);
            diameter[k] = map.ScreenDiameter(lay, k);
            var p = LivingLevels(slot, k, playheadMs, clock, look, lay, source);
            if (p.DrainAlpha <= 0f && p.BurnAlpha <= 0f && p.At <= 0f) { Territories[k] = Empty; continue; }
            var (v, turn, flip) = Look(slot, k);
            Place(ref p, frame.Src, frame.Dest, frame.Effects, texW, texH, at[k], diameter[k] / CurseAtlas.Fill, turn, flip, v);
            Territories[k] = p;
        }
        for (var k = count; k < ShaderTerritories; k++) Territories[k] = Empty;
        Host = CurseMaterial.Host(look, ShadeLevel(_timeline[slot].EasedStage(playheadMs), data.Burn) * _shadeIn[slot]);
        UvToPx = CurseMaterial.UvToPx(frame.Src, frame.Dest, frame.Effects, texW, texH);
        ComposeTravel(clock, count, at, diameter, look, lay, playheadMs);
        ComposeWisps(slot, playheadMs, t, body, anchor, clock, count, at, diameter, look, lay, 1f, float.PositiveInfinity);
        return true;
    }

    /// <summary>
    /// Territory <paramref name="k"/>'s living levels at <paramref name="playheadMs"/> (no placement): its bloom, its slow
    /// breath and swell, the light's boost (the settle's afterglow, a SPRAWL source's gathering, the reaction to every
    /// deepen it was already there for), the idle accent and the bloom front. Every term is continuous on the playhead:
    /// nothing in a territory's light or reveal steps from one frame to the next (ADR-013 §5).
    /// </summary>
    private TerritoryParams LivingLevels(int slot, int k, float playheadMs, TerritoryClock clock, HostLook look, Layout lay, float source)
    {
        var mark = Mark!;
        var reveal = clock.Reveal(k, playheadMs);
        if (reveal <= 0f) return Empty;
        var t = playheadMs + slot * SlotPhaseMs;
        // each territory breathes and swells at its own pace; the old ones REACT to each deepen
        var swell = 1f + 0.022f * MathF.Sin(t * MathF.Tau / 3400f + k * 1.9f);
        var breath = 1f + 0.14f * MathF.Sin(t * MathF.Tau / (4300f + 700f * k) + k * 2.3f);
        var react = React(clock, k, playheadMs);
        // (each territory settles on its OWN bloom's end: one shared end switched an old territory's accent off for a
        // whole deepen and then lifted its light by half in one frame, ADR-013 §5)
        var eventQuiet = mark.TickQuietNear(clock.EventOf[k], TickQuietNearMs);
        var grown = clock.Born[k] + clock.Grow[k];
        var settleAge = playheadMs - grown;
        var settle = settleAge < 0f ? 0f : settleAge >= CurseTimeline.SettleMs ? 1f : settleAge / CurseTimeline.SettleMs;
        // the afterglow takes over from the front as it runs out of mask (the bloom's last HandoffMs: it used to switch on
        // at +50 % on the frame the bloom ended), then settles
        var glowIn = settleAge >= 0f ? 1f - settle : Rise(settleAge + CurseTimeline.HandoffMs, CurseTimeline.HandoffMs);
        var afterglow = 0.5f * glowIn * (eventQuiet ? QuietShare : 1f);
        // one section of the territory at a time gently answers (a slow internal change)
        var cycle = 3900f + 500f * k;
        var n = (int)MathF.Floor((t + k * 1300f) / cycle);
        var ph = (t + k * 1300f - n * cycle) / cycle;
        var answer = MathF.Sin(ph * MathF.PI);
        var group = ((n % CurseAtlas.IdleGroups) + CurseAtlas.IdleGroups) % CurseAtlas.IdleGroups;
        // the bloom front keeps the quiet of the event that made it; a fading territory (a step down) has none
        var quietOf = playheadMs < grown + CurseTimeline.SettleMs && eventQuiet ? QuietShare : 1f;
        var peak = clock.Fading(k, playheadMs) ? 0f : FrontPeak * LightFor(look.Luma) * quietOf * (clock.Kind[k] == 2 ? ArrivalShare : 1f);
        return Living(look, lay.Burn[k], reveal, swell, breath, 1f + afterglow + 0.9f * source + react, answer, group, settle, peak);
    }

    /// <summary>
    /// Territory <paramref name="k"/>'s reaction to the deepens it was already infected for: a quick swell of its light,
    /// then back, for EACH deepen (the strongest at the moment). Only the last one used to count, so a second deepen
    /// landing inside the first's reaction cut it to nothing in one frame.
    /// </summary>
    private float React(TerritoryClock clock, int k, float playheadMs)
    {
        var react = 0f;
        for (var j = 0; j < clock.DeepenCount; j++)
        {
            if (k >= clock.DeepenBefores[j]) continue;
            var at = clock.DeepenAts[j];
            var e = CurseTimeline.ReactEnvelope(playheadMs - at);
            if (e <= 0f) continue;
            react = Math.Max(react, 0.7f * e * (Mark!.TickQuietNear(at, TickQuietNearMs) ? QuietShare : 1f));
        }
        return react;
    }

    /// <summary>
    /// Each deepen's shadow TRAVELLING under the skin from the nearest territory infected before it to each new one, then
    /// gone: the prototype's four puffs per new territory, every deepen on its own clock, the brightest
    /// <see cref="MaxPuffs"/> kept (the shader composes that many).
    /// </summary>
    private void ComposeTravel(TerritoryClock clock, int count, ReadOnlySpan<Vector2> at, ReadOnlySpan<float> diameter, HostLook look,
                               Layout lay, float playheadMs)
    {
        for (var i = 0; i < MaxPuffs; i++) Puffs[i] = default;
        const int Candidates = CurseSeating.MaxTerritories * 4;
        Span<PuffParams> puffs = stackalloc PuffParams[Candidates];
        Span<float> weight = stackalloc float[Candidates];
        var found = 0;
        for (var d = 0; d < clock.DeepenCount; d++)
        {
            var before = clock.DeepenBefores[d];
            var after = clock.DeepenAfters[d];
            var newOnes = after - before;
            var reactAge = playheadMs - clock.DeepenAts[d];
            if (before <= 0 || newOnes <= 0 || reactAge < 0f
                || reactAge >= CurseTimeline.TravelMs * 1.5f + (newOnes - 1) * CurseTimeline.DeepenStaggerMs + 20f) continue;
            var quiet = Mark!.TickQuietNear(clock.DeepenAts[d], TickQuietNearMs) ? QuietShare : 1f;
            for (var k = before; k < Math.Min(count, after); k++)
            {
                var from = at[0];
                for (var j = 1; j < before; j++)
                    if (Vector2.DistanceSquared(at[j], at[k]) < Vector2.DistanceSquared(from, at[k])) from = at[j];
                var hot = Rgb(new Mat(look, lay.Burn[k]).Hot);
                for (var i = 0; i < 4 && found < Candidates; i++)
                {
                    var q = (reactAge - (k - before) * CurseTimeline.DeepenStaggerMs) / CurseTimeline.TravelMs - i * 0.14f;
                    if (q < 0f || q > 1f) continue;
                    var w = 0.32f * quiet * MathF.Sin(q * MathF.PI) * (1f - 0.2f * i);
                    if (w <= 0f) continue;
                    var size = diameter[k] * (0.22f - 0.03f * i);
                    puffs[found] = new PuffParams(Vector2.Lerp(from, at[k], CurseTimeline.Smooth(q)), size * 0.5f, hot * w);
                    weight[found++] = w;
                }
            }
        }
        // the brightest MaxPuffs, in a fixed order (stable for equal weights), each LESS the brightest one left out: a puff
        // leaving or entering the kept four is then at nothing, never cut while lit (only while more than four are alive)
        Span<int> kept = stackalloc int[MaxPuffs];
        var keptCount = 0;
        for (var i = 0; i < MaxPuffs; i++)
        {
            var best = -1;
            for (var j = 0; j < found; j++)
                if (weight[j] > 0f && (best < 0 || weight[j] > weight[best])) best = j;
            if (best < 0) break;
            kept[keptCount++] = best;
            Puffs[i] = puffs[best];
            weight[best] = -weight[best];
        }
        var cut = 0f;
        for (var j = 0; j < found; j++) cut = Math.Max(cut, weight[j]);
        if (cut <= 0f) return;
        for (var i = 0; i < keptCount; i++)
        {
            var w = -weight[kept[i]];
            ref var f = ref Puffs[i];
            f = f with { Colour = f.Colour * ((w - cut) / w) };
        }
    }

    /// <summary>How much this slot's SPRAWL source is lit: a hop leaving it (the curse gathering in the source).</summary>
    private float SourceGlow(int slot, float playheadMs)
    {
        var mark = Mark!;
        var best = 0f;
        for (var j = 0; j < _slots; j++)
        {
            if (j == slot || mark.HopFromOf(j) != slot) continue;
            var age = playheadMs - mark.HopLeavesAt(j) + 120f;   // it gathers a little before the hop leaves
            if (age < 0f || age > 520f) continue;
            var e = age < 120f ? age / 120f : 1f - (age - 120f) / 400f;
            best = Math.Max(best, e);
        }
        return best;
    }

    /// <summary>
    /// THE CURSE LEAKS OUT OF ITS TERRITORIES: now and then a slow wisp of Shadow leaves one infected territory, rises
    /// past the silhouette and is gone in about a second and a half; two more burst from each territory as it blooms, on
    /// that territory's own clock. Never an aura: one territory at a time, mostly. <paramref name="share"/> scales them
    /// (a dying host's wisps in flight fade with its curse instead of vanishing on the frame it falls).
    /// <paramref name="died"/> is a dying host's fall (+inf while it lives): only the wisps already rising then go on, from
    /// the territories it wore then (a deepen's territory born after the fall never leaks, nor bursts).
    /// </summary>
    private void ComposeWisps(int slot, float playheadMs, float t, Rectangle body, Vector2 anchor, TerritoryClock clock, int count,
                              ReadOnlySpan<Vector2> at, ReadOnlySpan<float> diameter, HostLook look, Layout lay, float share, float died)
    {
        var rise = Math.Clamp(body.Height * 0.17f, 30f, 80f);
        var drift = Math.Clamp(body.Width * 0.18f, 16f, 60f);
        const float period = 2800f, life = 1500f, burstLife = 950f;
        for (var k = 0; k < count; k++)
        {
            var m = new Mat(look, lay.Burn[k]);
            var reveal = clock.Reveal(k, Math.Min(playheadMs, died - 1f));
            var from = at[k];
            var size = Math.Clamp(diameter[k] * 0.36f, 20f, 58f);
            var outward = from.X >= anchor.X ? 1f : -1f;
            // the resting leak: one wisp every few seconds, each territory at its own moment, fading in with its bloom
            var w0 = ((t + CurseAtlas.Hash(slot * 13 + k * 29) * period) % period + period) % period;
            if (reveal > 0f && w0 < life && playheadMs - w0 <= died) Wisp(from, w0 / life, size, rise, drift * outward, k, m.Smoke, m.Emission, reveal * share);
            // the bloom's burst: two wisps escape as the territory takes hold (its own clock: never cut mid-flight)
            var start = clock.Born[k] + 0.45f * clock.Grow[k];
            for (var i = 0; i < 2; i++)
            {
                var ba = playheadMs - start - i * 160f;
                if (ba < 0f || ba > burstLife || start + i * 160f > died) continue;
                Wisp(from, ba / burstLife, size * 0.9f, rise * 1.1f, drift * (i == 0 ? outward : -outward) * 0.8f, k + 5 * i, m.Smoke, m.Emission,
                     (clock.Kind[k] == 2 ? ArrivalShare : 1f) * share);
            }
        }
    }

    private void Wisp(Vector2 from, float q, float size, float rise, float drift, int seed, Color smoke, Color emission, float share)
    {
        var env = MathF.Pow(MathF.Sin(q * MathF.PI), 1.2f) * (1f - 0.4f * q) * share;
        var sway = MathF.Sin(q * 3.1f + seed * 1.7f) * size * 0.3f;
        var p = from + new Vector2(sway + drift * q * q, -rise * q);
        var sz = size * (0.6f + 0.7f * q);
        // a RISING STREAK of Shadow smoke with a faint core of the territory's light (round puffs read as "orbs")
        AddStreak(p, sz * 0.62f, sz * 1.5f, smoke * (0.62f * env), false);
        AddStreak(p + new Vector2(0f, sz * 0.12f), sz * 0.36f, sz * 0.95f, emission * (0.5f * env * (1f - 0.5f * q)), true);
    }

    private void AddStreak(Vector2 at, float w, float h, Color c, bool light)
    {
        if (c.A == 0 || StreakCount >= MaxStreaks) return;
        // the light is ADDED (premultiplied, alpha 0) in the alpha-over arena batch: the prototype's screen, for these
        // dim cores
        if (light) c.A = 0;
        Streaks[StreakCount++] = new Streak(new Rectangle((int)(at.X - w / 2), (int)(at.Y - h / 2), (int)w, (int)h), c, light);
    }

    /// <summary>
    /// A dying host's curse at <paramref name="playheadMs"/> (what it WORE the moment before it fell, on each territory's own
    /// clock: a host that fell mid-bloom collapses from its bloom): the curse pass's parameters (<paramref name="pass"/>:
    /// while the collapse lasts) and its smoke-out streaks. False when there is nothing to draw.
    /// </summary>
    internal bool ComposeLeaving(int slot, float playheadMs, in SpriteFrame frame, int texW, int texH, CurseHostData? data, out bool pass)
    {
        pass = false;
        StreakCount = 0;
        TerritoryCount = 0;
        var mark = Mark!;
        if (!TryLeaving(slot, playheadMs, out var died, out var age)) return false;
        data ??= _host[slot];
        if (data is null) return false;
        var p = age / LeaveMs;
        var fade = (1f - p) * (1f - p);
        var body = _body[slot].Width > 0 ? _body[slot] : frame.Dest;
        var anchor = mark.TryAnchor(slot, out var pinned) ? pinned : body.Center.ToVector2();
        var lay = data.LayoutFor(slot);
        var look = data.Look;
        var map = new CurseFrameMap(frame);
        var clock = _timeline[slot].ClockAt(mark, slot, died - 1f, lay.Available);
        var count = Math.Min(clock.Count, ShaderTerritories);
        var fall = LeavingFall(age);
        pass = fall > 0.01f && count > 0;
        if (pass)
        {
            TerritoryCount = count;
            // what each territory looked like the moment before the fall: its light carries on into the flare and goes
            // out with the collapse (it used to switch off on the frame the host fell)
            var source = SourceGlow(slot, died - 1f);
            for (var k = 0; k < count; k++)
            {
                var wore = clock.Reveal(k, died - 1f);
                var alive = LivingLevels(slot, k, died - 1f, clock, look, lay, source);
                var t = Leaving(look, lay.Burn[k], wore, fall, age, alive);
                if (wore * fall <= 0.01f) { Territories[k] = Empty; continue; }
                var (v, turn, flip) = Look(slot, k);
                Place(ref t, frame.Src, frame.Dest, frame.Effects, texW, texH, map.At(lay, k, anchor), map.ScreenDiameter(lay, k) / CurseAtlas.Fill,
                      turn, flip, v);
                Territories[k] = t;
            }
            for (var k = count; k < ShaderTerritories; k++) Territories[k] = Empty;
            // a deepen's shadow still travelling when the host fell goes on, going out with the collapse
            Span<Vector2> to = stackalloc Vector2[ShaderTerritories];
            Span<float> size = stackalloc float[ShaderTerritories];
            for (var k = 0; k < count; k++)
            {
                to[k] = map.At(lay, k, anchor);
                size[k] = map.ScreenDiameter(lay, k);
            }
            ComposeTravel(clock, count, to, size, look, lay, playheadMs);
            for (var i = 0; i < MaxPuffs; i++) Puffs[i] = Puffs[i] with { Colour = Puffs[i].Colour * fall };
            Prepare(slot, died - 1f);
            Host = CurseMaterial.Host(look, ShadeLevel(_timeline[slot].EasedStage(died - 1f), data.Burn, fall) * _shadeIn[slot]);
            UvToPx = CurseMaterial.UvToPx(frame.Src, frame.Dest, frame.Effects, texW, texH);
        }
        // its territories SMOKE OUT as they collapse (outside the body): two streaks from each it wore, rising (each eases
        // in over its first SmokeInShare of the climb: they used to appear at full strength), gone
        for (var k = 0; k < count; k++)
        {
            var wore = clock.Reveal(k, died - 1f);
            if (wore <= 0.01f) continue;
            var smoke = new Mat(look, lay.Burn[k]).Smoke * wore;
            var from = map.At(lay, k, anchor);
            var size0 = map.ScreenDiameter(lay, k);
            for (var i = 0; i < 2; i++)
            {
                var q = Math.Clamp(p * 1.4f - i * 0.15f - k * 0.05f, 0f, 1f);
                if (q <= 0f) continue;
                var to = from + new Vector2(MathF.Sin(k * 1.7f + i) * 10f, -q * body.Height * 0.3f);
                var size = size0 * (0.3f + 0.25f * q);
                AddStreak(to, size * 0.6f, size * 1.3f, smoke * (0.55f * (1f - q) * fade * Rise(q, SmokeInShare)), false);
            }
        }
        // and the wisps already in flight rise on, fading with the curse (none is cut on the frame the host falls); none
        // starts after the fall, nor from a territory the host never showed (a deepen's still on its travel when it fell)
        if (count > 0)
        {
            Span<Vector2> at = stackalloc Vector2[ShaderTerritories];
            Span<float> diameter = stackalloc float[ShaderTerritories];
            for (var k = 0; k < count; k++)
            {
                at[k] = map.At(lay, k, anchor);
                diameter[k] = map.ScreenDiameter(lay, k);
            }
            ComposeWisps(slot, playheadMs, playheadMs + slot * SlotPhaseMs, body, anchor, clock, count, at, diameter, look, lay, fade, died);
        }
        return pass || StreakCount > 0;
    }

    // ── THE TRACE ──────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary><c>curse-seat</c>, once per creature per wave: the baked seats this slot wears (frame pixels of its idle
    /// strip), never measured at runtime.</summary>
    private void TraceSeat(int slot, CurseHostData data)
    {
        _seatTraced[slot] = true;
        var lay = data.LayoutFor(slot);
        var seats = new StringBuilder();
        for (var k = 0; k < lay.Available; k++)
        {
            var s = data.Anchor + lay.Offset[k];
            seats.Append($"{s.X:0},{s.Y:0}/{lay.Diameter[k]:0}/b{lay.Burn[k]:0.00} ");
        }
        PresentTrace.Log("curse-seat", $"slot={slot}\tterritories={lay.Available}\t{seats}\tanchor={data.Anchor.X:0},{data.Anchor.Y:0}"
                                       + $"\tframe={data.FrameSize}x{data.FrameSize}x{data.Frames}\tparity={slot & 1}\tburn={data.Burn:0.00}\tsource=baked");
    }
}
