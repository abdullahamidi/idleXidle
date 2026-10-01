using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using IdleXIdle.Game.Vfx;

namespace IdleXIdle.Game.Presentation;

/// <summary>Which BRAND curse concept a <see cref="CursePrototype"/> draws (direction selection, 2026-10-01).</summary>
public enum CurseConcept
{
    /// <summary>A — LIVING SHADOW CORRUPTION: dark veins spreading under the skin from one seat, internal pulses, leaks.</summary>
    Corruption,

    /// <summary>B — WITHERING CURSE: the body drained toward ash, blighted blotches and dry cracks, falling ash.</summary>
    Withering,

    /// <summary>C — SHADOW POSSESSION: a dark double that does not quite fit the body, and a mass moving inside it.</summary>
    Possession,
}

/// <summary>
/// A DIRECTION-SELECTION PROTOTYPE for BRAND (ADR-011, the MARK reference; owner's brief 2026-10-01): the mark presented
/// as a CURSE the whole body suffers instead of a sign on one body point. Dev-only (<c>RH_BRAND_CONCEPT=A|B|C</c>); not
/// a production path until the owner picks a concept.
/// </summary>
/// <remarks>
/// <para>
/// THE TRUTH IS <see cref="MarkPerformance"/>'s, unchanged: who carries the mark (the front; every creature under
/// SPRAWL), its phase (the apply on the first tick, waiting for a hop or a transfer, arriving), the depth it shows and
/// when a deepen lands, when a host falls. Only the picture is new.
/// </para>
/// <para>
/// THE BODY IS THE CANVAS: each afflicted creature's current frame is written into the STENCIL buffer through the stock
/// <see cref="AlphaTestEffect"/> (no compiled shader), and the curse's textures are drawn with a stencil test, so they
/// live inside that exact silhouette at any size; a boss is no different from a whelp. Cost: four batch boundaries per
/// afflicted creature (the brief's prototype; a production version would be one shader pass).
/// </para>
/// <para>Allocation-free per frame: states, textures and tables are built once; every per-frame value is on the stack.</para>
/// </remarks>
public sealed partial class CursePrototype
{
    /// <summary>The concept this prototype draws.</summary>
    public CurseConcept Concept { get; }

    /// <summary>The mark whose truth this curse presents.</summary>
    public MarkPerformance Mark { get; }

    /// <summary>Sprites drawn this frame (the trace).</summary>
    public int SpriteCount { get; private set; }

    /// <summary>The concept named by <c>RH_BRAND_CONCEPT</c> (A / B / C), or null when unset.</summary>
    public static CurseConcept? FromEnvironment()
        => Environment.GetEnvironmentVariable("RH_BRAND_CONCEPT")?.Trim().ToUpperInvariant() switch
        {
            "A" => CurseConcept.Corruption,
            "B" => CurseConcept.Withering,
            "C" => CurseConcept.Possession,
            _ => null,
        };

    // ── PALETTE (premultiplied at use: colour * alpha) ────────────────────────────────────────────────────────────────
    private static readonly Color Ink = new(12, 7, 20);
    private static readonly Color Violet = new(128, 94, 200);
    private static readonly Color Ash = new(122, 120, 110);
    private static readonly Color Blight = new(30, 22, 26);
    private static readonly Color Crack = new(8, 6, 8);
    private static readonly Color Shade = new(10, 6, 18);
    private static readonly Color Double = new(44, 26, 82);        // the possessing double: shadow violet

    // ── TIMING (ms) ─────────────────────────────────────────────────────────────────────────────────────────────────
    private const float FlareMs = 700f;       // an apply / arrival / deepen flare's life
    private const float LeaveMs = 900f;       // a falling host: the curse leaving it
    private const float DeepenLookMs = 650f;  // how far back a shown-stage change still flares

    private readonly Texture2D _pixel, _puff, _band, _blob;
    private readonly Texture2D[] _blight, _cracks;
    private readonly AlphaTestEffect _alphaTest;
    private readonly BlendState _noColour = new() { ColorWriteChannels = ColorWriteChannels.None };
    private readonly DepthStencilState[] _write = new DepthStencilState[32], _inside = new DepthStencilState[32], _outside = new DepthStencilState[32];

    // per slot, cached for the frame's playhead (Convulse is asked before DrawOn for the same slot and moment)
    private readonly float[] _cacheAt;
    private readonly byte[] _kind;           // 0 none, 1 waiting, 2 carrying
    private readonly int[] _stage;
    private readonly float[] _flare;         // 0..1 envelope of the current flare
    private readonly float[] _flareAge;      // ms since the flare began
    private readonly byte[] _flareKind;      // 0 none, 1 apply, 2 arrival (hop / transfer), 3 deepen
    private readonly Rectangle[] _body;

    /// <summary>Builds the concept's textures and states once, for one wave.</summary>
    public CursePrototype(CurseConcept concept, MarkPerformance mark, GraphicsDevice device, int slots)
    {
        Concept = concept;
        Mark = mark;
        var n = Math.Max(1, slots);
        _cacheAt = new float[n];
        Array.Fill(_cacheAt, float.NaN);
        _kind = new byte[n];
        _stage = new int[n];
        _flare = new float[n];
        _flareAge = new float[n];
        _flareKind = new byte[n];
        _body = new Rectangle[n];
        var t = Textures.For(device);
        _pixel = t.Pixel;
        _puff = t.Puff;
        _band = t.Band;
        _blob = t.Blob;
        _blight = t.Blight;
        _cracks = t.Cracks;
        _alphaTest = new AlphaTestEffect(device) { VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 90 };
        // the corruption's field is grown here, never on a Draw frame (built at the first apply, it cost one frame 160 MB)
        if (concept == CurseConcept.Corruption) _corruption = CorruptionAtlas.For(device);
        for (var i = 0; i < _write.Length; i++)
        {
            _write[i] = new DepthStencilState
            {
                DepthBufferEnable = false, StencilEnable = true, StencilFunction = CompareFunction.Always,
                StencilPass = StencilOperation.Replace, ReferenceStencil = i + 1,
            };
            _inside[i] = new DepthStencilState
            {
                DepthBufferEnable = false, StencilEnable = true,
                // PROBE ONLY (RH_CURSE_NOCLIP): the curse drawn unclipped, to see where the field lands on a body
                StencilFunction = Environment.GetEnvironmentVariable("RH_CURSE_NOCLIP") is null ? CompareFunction.Equal : CompareFunction.Always,
                StencilPass = StencilOperation.Keep, ReferenceStencil = i + 1,
            };
            _outside[i] = new DepthStencilState
            {
                DepthBufferEnable = false, StencilEnable = true, StencilFunction = CompareFunction.NotEqual,
                StencilPass = StencilOperation.Keep, ReferenceStencil = i + 1,
            };
        }
    }

    /// <summary>Starts a frame's counters.</summary>
    public void BeginFrame() => SpriteCount = 0;

    // ── THE STATE ON THE PLAYHEAD ─────────────────────────────────────────────────────────────────────────────────

    private void Prepare(int slot, float t)
    {
        if (_cacheAt[slot] == t) return;
        _cacheAt[slot] = t;
        _flare[slot] = 0f;
        _flareKind[slot] = 0;
        _flareAge[slot] = 0f;
        var phase = Mark.TryPhase(slot, t, out var u);
        if (phase == MarkPhase.None) { _kind[slot] = 0; return; }
        if (phase == MarkPhase.Waiting) { _kind[slot] = 1; _stage[slot] = 0; return; }
        _kind[slot] = 2;
        var stage = Math.Max(1, Mark.DrawnStage(slot, t));
        _stage[slot] = stage;
        if (u >= 0f && u < FlareMs)
        {
            _flareKind[slot] = phase == MarkPhase.Apply ? (byte)1 : (byte)2;
            _flareAge[slot] = u;
            _flare[slot] = Envelope(u);
        }
        // A DEEPEN: the shown stage stepped up within the last DeepenLookMs (MarkPerformance reveals it after its chisel)
        for (var k = 1; k * 50f <= DeepenLookMs; k++)
        {
            var back = t - k * 50f;
            if (!Mark.Carries(slot, back)) break;
            if (Math.Max(1, Mark.DrawnStage(slot, back)) < stage)
            {
                var age = k * 50f - 25f;
                var e = Envelope(age);
                if (e > _flare[slot]) { _flare[slot] = e; _flareKind[slot] = 3; _flareAge[slot] = age; }
                break;
            }
        }
    }

    /// <summary>0..1: a fast rise (60 ms), a short hold, a long fall.</summary>
    private static float Envelope(float age)
    {
        if (age < 0f) return 0f;
        if (age < 60f) return age / 60f;
        if (age < 160f) return 1f;
        var f = 1f - (age - 160f) / (FlareMs - 160f);
        return f <= 0f ? 0f : f * f;
    }

    /// <summary>The body's own reaction to the curse flaring (multiplied into the creature's squash before it is drawn).</summary>
    public Vector2 Convulse(int slot, float playheadMs)
    {
        if (slot < 0 || slot >= _kind.Length) return Vector2.One;
        Prepare(slot, playheadMs);
        var f = _flare[slot];
        if (f <= 0.01f) return Vector2.One;
        var age = _flareAge[slot];
        switch (Concept)
        {
            case CurseConcept.Corruption:
            {
                // a shudder: two quick tremors, the body tightening on the pulse
                var s = MathF.Sin(age * 0.055f) * f;
                return new Vector2(1f + 0.025f * s, 1f - 0.03f * MathF.Abs(s));
            }
            case CurseConcept.Withering:
                // a sag: the body loses its strength, a little lower and wider, and recovers slowly
                return new Vector2(1f + 0.02f * f, 1f - 0.035f * f);
            default:
            {
                // a jerk: the possession fights the body, a sharp flicker of the silhouette
                var s = MathF.Sign(MathF.Sin(age * 0.09f)) * f;
                return new Vector2(1f + 0.035f * s, 1f + 0.01f * s);
            }
        }
    }

    // ── DRAWING ───────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The curse on the creature in <paramref name="slot"/>, right after it was drawn (the arena batch is open: it is
    /// ended, the stencil passes run, and it is reopened in the arena's own state).
    /// </summary>
    public void DrawOn(SpriteBatch b, int slot, float playheadMs, SpriteFrame frame, Rectangle body, Texture2D? mask,
                       RasterizerState raster)
    {
        if (slot < 0 || slot >= _kind.Length) return;
        _body[slot] = body;
        Prepare(slot, playheadMs);
        if (_kind[slot] == 0) return;
        Mark.TryAnchor(slot, out var anchor, out _);
        if (anchor == default) anchor = body.Center.ToVector2();
        // the corruption's pocket sits in the body's mass, never on a thin limb, a wrist or a weapon
        if (Concept == CurseConcept.Corruption) anchor = CorruptionSeat(slot, anchor, body, frame);
        var device = b.GraphicsDevice;
        b.End();
        WriteStencil(b, device, slot, frame, raster);
        var t = playheadMs + slot * 377f;
        switch (Concept)
        {
            case CurseConcept.Corruption: DrawCorruptionGlow(b, slot, playheadMs, t, body, anchor, raster, frame); break;
            case CurseConcept.Withering: DrawWithering(b, slot, t, body, anchor, raster); break;
            default: DrawPossession(b, slot, t, body, frame, mask, raster); break;
        }
        if (Concept == CurseConcept.Corruption) DrawCorruptionWisps(b, slot, t, body, anchor, raster);
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, raster);
        DrawLoose(b, slot, t, body, anchor);
        Mark.CountCurse(SpriteCount, _stage[slot]);
    }

    /// <summary>
    /// A FALLING host that carried the curse: the curse leaving it (drawn on its death frame, after it). Nothing when it
    /// did not carry it.
    /// </summary>
    public void DrawLeaving(SpriteBatch b, int slot, float playheadMs, SpriteFrame frame, Texture2D? mask, RasterizerState raster)
    {
        if (slot < 0 || slot >= _kind.Length) return;
        var died = Mark.DeathAt(slot);
        if (float.IsPositiveInfinity(died) || !Mark.Carries(slot, died - 1f)) return;
        var age = playheadMs - died;
        if (age < 0f || age >= LeaveMs) return;
        var p = age / LeaveMs;
        var fade = (1f - p) * (1f - p);
        var body = _body[slot].Width > 0 ? _body[slot] : frame.Dest;
        var stage = Math.Max(1, Mark.DrawnStage(slot, died - 1f));
        var anchor = Mark.TryAnchor(slot, out var a, out _) ? a : body.Center.ToVector2();
        // (it leaves from where it sat: the last seat this slot was drawn with)
        if (Concept == CurseConcept.Corruption && _seatShift is not null) anchor += _seatShift[slot];
        var device = b.GraphicsDevice;
        var refSlot = Math.Min(_write.Length - 1, 16 + slot);
        b.End();
        WriteStencil(b, device, refSlot, frame, raster);
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, _inside[refSlot], raster);
        switch (Concept)
        {
            case CurseConcept.Corruption:
                // the curse brightens once, then collapses back along its paths to the seat (its own passes)
                b.End();
                DrawCorruptionLeaving(b, slot, stage, age, VeinRect(anchor, body), body, _inside[refSlot], raster, frame);
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, _inside[refSlot], raster);
                break;
            case CurseConcept.Withering:
                // the whole body greys out: it crumbles
                Fill(b, body, Ash * (0.25f + 0.35f * MathF.Min(1f, p * 3f)) * fade);
                Draw(b, _blight[Math.Min(2, stage)], CoverBody(body), Blight * (0.55f * fade));
                break;
            default:
                Fill(b, body, Shade * (0.25f * fade));
                break;
        }
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, raster);
        switch (Concept)
        {
            case CurseConcept.Corruption:
                // a little dark smoke escapes from the seat as it collapses
                for (var i = 0; i < 3; i++)
                {
                    var q = Math.Clamp(p * 1.4f - i * 0.1f, 0f, 1f);
                    var at = anchor + new Vector2(MathF.Sin(i * 1.7f + p * 5f) * 8f, -q * body.Height * 0.4f);
                    Puff(b, at, body.Height * (0.08f + 0.05f * q), Ink * (0.3f * (1f - q) * fade));
                }
                break;
            case CurseConcept.Withering:
                // ash falling off the collapsing body
                for (var i = 0; i < 16; i++)
                {
                    var hx = Hash(slot * 31 + i);
                    var hy = Hash(slot * 17 + i * 7);
                    var at = new Vector2(body.X + hx * body.Width, body.Y + (0.2f + 0.6f * hy) * body.Height + p * 60f * (0.6f + hy));
                    Fill(b, new Rectangle((int)at.X, (int)at.Y, 3, 3), Ash * (0.7f * fade));
                }
                break;
            default:
                // the possessor leaves: the dark double rises out of the falling body and thins away
                if (mask is not null)
                {
                    var rise = new Vector2(-6f * p, -body.Height * 0.35f * p);
                    var dest = frame.Dest;
                    dest.Offset((int)rise.X, (int)rise.Y);
                    b.Draw(mask, dest, frame.Src, Shade * (0.6f * fade), 0f, Vector2.Zero, frame.Effects, 0f);
                    SpriteCount++;
                }
                break;
        }
    }

    private void WriteStencil(SpriteBatch b, GraphicsDevice device, int refSlot, SpriteFrame frame, RasterizerState raster)
    {
        var vp = device.Viewport;
        _alphaTest.Projection = Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0, 1);
        b.Begin(SpriteSortMode.Deferred, _noColour, SamplerState.LinearClamp, _write[Math.Min(refSlot, _write.Length - 1)], raster, _alphaTest);
        b.Draw(frame.Texture, frame.Dest, frame.Src, Color.White, 0f, Vector2.Zero, frame.Effects, 0f);
        b.End();
    }

    // ── A: LIVING SHADOW CORRUPTION ─────────────────────────────────────────────────────────────────────────────────

    // ── B: WITHERING CURSE ──────────────────────────────────────────────────────────────────────────────────────────

    private void DrawWithering(SpriteBatch b, int slot, float t, Rectangle body, Vector2 anchor, RasterizerState raster)
    {
        var stage = _stage[slot];
        var f = _flare[slot];
        var kind = _flareKind[slot];
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, _inside[slot], raster);
        if (_kind[slot] == 1)
            Fill(b, body, Ash * 0.06f);
        else
        {
            // THE DRAIN: the body pulled toward ash (less colour, less life), darker with depth
            var sweep = kind == 0 ? 1f : Smooth(Math.Min(1f, _flareAge[slot] / 480f));
            var from = kind == 3 ? stage - 1 : 0;
            var drain = Lerp(Drain(from), Drain(stage), sweep);
            Fill(b, body, Ash * drain);
            Fill(b, body, Color.Black * (0.04f * stage));
            // THE BLIGHT: blotches and dry cracks spreading over the body; a new depth fades in behind the sweep
            var cover = CoverBody(body);
            if (kind == 3 && stage > 1)
            {
                Draw(b, _blight[stage - 2], cover, Blight * 0.65f);
                Draw(b, _cracks[stage - 2], cover, Crack * 0.8f);
            }
            var bl = kind == 0 ? 1f : sweep;
            Draw(b, _blight[stage - 1], cover, Blight * (0.65f * bl));
            Draw(b, _cracks[stage - 1], cover, Crack * (0.8f * bl));
            if (kind != 0 && f > 0.01f)
            {
                // THE SWEEP: the withering passes over the body, top to bottom (an arrival rises from the feet)
                var p = Math.Min(1f, _flareAge[slot] / 480f);
                var y = kind == 2 ? body.Bottom - p * body.Height * 1.2f : body.Y - body.Height * 0.2f + p * body.Height * 1.2f;
                var h = (int)(body.Height * 0.35f);
                Draw(b, _band, new Rectangle(body.X - 8, (int)y - h / 2, body.Width + 16, h), Ash * (0.55f * f));
            }
        }
        b.End();
    }

    private static float Drain(int stage) => stage <= 0 ? 0f : 0.16f + 0.08f * stage;

    // ── C: SHADOW POSSESSION ────────────────────────────────────────────────────────────────────────────────────────

    private void DrawPossession(SpriteBatch b, int slot, float t, Rectangle body, SpriteFrame frame, Texture2D? mask, RasterizerState raster)
    {
        Mark.TryAnchor(slot, out var seat, out _);
        if (seat == default) seat = body.Center.ToVector2();
        DrawPossession(b, slot, t, body, seat, frame, mask, raster);
    }

    /// <summary>Where the mass <paramref name="i"/> is: wandering a little round the middle of the body (halfway between
    /// the seat and the body's centre; round the seat alone, on a low seat it drifted off the silhouette and was clipped away).</summary>
    private static Vector2 MassAt(int i, float t, Rectangle body, Vector2 seat)
        => (seat + body.Center.ToVector2()) * 0.5f
           + new Vector2(MathF.Sin(t * (0.00031f + i * 0.00007f) + i * 2.1f) * body.Width * 0.10f,
                         MathF.Sin(t * (0.00023f + i * 0.00011f) + i * 1.3f + 0.7f) * body.Height * 0.12f);

    private void DrawPossession(SpriteBatch b, int slot, float t, Rectangle body, Vector2 seat, SpriteFrame frame, Texture2D? mask, RasterizerState raster)
    {
        var stage = _stage[slot];
        var f = _flare[slot];
        var kind = _flareKind[slot];
        // THE MASS INSIDE: one dark presence per depth, drifting under the body's surface
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, _inside[slot], raster);
        if (_kind[slot] == 1)
            Fill(b, body, Shade * 0.05f);
        else
        {
            Fill(b, body, Shade * (0.06f + 0.03f * stage));
            var size = body.Height * 0.56f;
            for (var i = 0; i < stage; i++)
            {
                var grow = kind == 3 && i == stage - 1 ? Smooth(Math.Min(1f, _flareAge[slot] / 350f)) : 1f;
                if (kind is 1 or 2) grow = Smooth(Math.Min(1f, _flareAge[slot] / 300f));
                var c = MassAt(i, t, body, seat);
                var s = size * (0.8f + 0.2f * MathF.Sin(t * 0.0021f + i)) * grow;
                Draw(b, _blob, new Rectangle((int)(c.X - s / 2), (int)(c.Y - s / 2), (int)s, (int)s), Shade * (0.5f + 0.25f * f));
            }
            if (f > 0.01f) Fill(b, body, Shade * (0.2f * f));
        }
        b.End();
        if (_kind[slot] == 2)
        {
            // the mass seen from inside a black body: a faint violet glow moving under its surface
            b.Begin(SpriteSortMode.Deferred, VfxBlend.PremultipliedAdditive, SamplerState.LinearClamp, _inside[slot], raster);
            var size = body.Height * 0.56f;
            for (var i = 0; i < stage; i++)
            {
                var c = MassAt(i, t, body, seat);
                var s = size * 0.7f;
                Draw(b, _blob, new Rectangle((int)(c.X - s / 2), (int)(c.Y - s / 2), (int)s, (int)s), Violet * (0.10f + 0.3f * f));
            }
            b.End();
        }
        if (_kind[slot] != 2 || mask is null) return;
        // THE DOUBLE: a dark copy of the body that does not quite fit it, seen only where it spills outside the silhouette;
        // at rest it breathes a few px out of place, twitching now and then; an apply / arrival slams it in from outside,
        // a deepen tears it out and snaps it back
        var breath = 3f + 1.5f * stage + 1.5f * MathF.Sin(t * 0.0017f);
        var cycle = 3400f;
        var ph = ((t % cycle) + cycle) % cycle;
        if (ph < 160f) breath += 8f * MathF.Sin(ph / 160f * MathF.PI);   // the twitch: it slips out of the body and back
        var off = new Vector2(-0.8f, -0.45f) * breath;
        var alpha = 0.40f + 0.07f * stage;
        if (kind is 1 or 2)
        {
            var p = Math.Min(1f, _flareAge[slot] / 320f);
            off = new Vector2(-1f, -0.5f) * Lerp(34f, breath, Smooth(p));
            alpha = Lerp(0.7f, alpha, p);
        }
        else if (kind == 3)
        {
            var a = _flareAge[slot];
            var tear = a < 180f ? MathF.Sin(a / 180f * MathF.PI * 0.5f) : Math.Max(0f, 1f - (a - 180f) / 140f);
            off = new Vector2(-1f, -0.5f) * (breath + 18f * tear);
            alpha = Lerp(alpha, 0.7f, tear);
        }
        var dest = frame.Dest;
        dest.Offset((int)MathF.Round(off.X), (int)MathF.Round(off.Y));
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, _outside[slot], raster);
        b.Draw(mask, dest, frame.Src, Double * alpha, 0f, Vector2.Zero, frame.Effects, 0f);
        SpriteCount++;
        b.End();
    }

    // ── OUTSIDE THE BODY: leaks, ash, nothing for the possession ────────────────────────────────────────────────────

    private void DrawLoose(SpriteBatch b, int slot, float t, Rectangle body, Vector2 anchor)
    {
        if (_kind[slot] != 2) return;
        var stage = _stage[slot];
        var f = _flare[slot];
        switch (Concept)
        {
            case CurseConcept.Corruption:
                DrawCorruptionLoose(b, slot, t, body, anchor);
                break;
            case CurseConcept.Withering:
            {
                // ASH: a flake or two falling off the body at rest, a small shower on a flare
                var count = stage + (f > 0.05f ? 8 : 0);
                for (var i = 0; i < count; i++)
                {
                    var burst = i >= stage;
                    var life = burst ? FlareMs : 1900f;
                    var age = burst ? _flareAge[slot] - (i - stage) * 25f : ((t + i * 617f) % life + life) % life;
                    if (age < 0f || age >= life) continue;
                    var q = age / life;
                    var hx = Hash(slot * 19 + i * 3);
                    var hy = Hash(slot * 5 + i * 13);
                    var at = new Vector2(body.X + (0.2f + 0.6f * hx) * body.Width, body.Y + (0.3f + 0.4f * hy) * body.Height + q * 34f);
                    var a = (burst ? 0.75f * f : 0.45f) * (1f - q);
                    Fill(b, new Rectangle((int)at.X, (int)at.Y, 3, 3), Ash * a);
                }
                break;
            }
        }
    }

    // ── HELPERS ──────────────────────────────────────────────────────────────────────────────────────────────────────

    private static Rectangle CoverBody(Rectangle body)
    {
        var s = (int)(Math.Max(body.Width, body.Height) * 1.15f);
        return new Rectangle(body.Center.X - s / 2, body.Center.Y - s / 2, s, s);
    }

    private void Fill(SpriteBatch b, Rectangle r, Color c)
    {
        b.Draw(_pixel, r, c);
        SpriteCount++;
    }

    private void Draw(SpriteBatch b, Texture2D tex, Rectangle r, Color c)
    {
        b.Draw(tex, r, c);
        SpriteCount++;
    }

    private void Puff(SpriteBatch b, Vector2 at, float size, Color c)
    {
        if (c.A == 0) return;
        b.Draw(_puff, new Rectangle((int)(at.X - size / 2), (int)(at.Y - size / 2), (int)size, (int)size), c);
        SpriteCount++;
    }

    private static float Smooth(float x) => x * x * (3f - 2f * x);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Hash(int n)
    {
        unchecked
        {
            var h = (uint)n * 747796405u + 2891336453u;
            h = ((h >> (int)((h >> 28) + 4u)) ^ h) * 277803737u;
            return ((h >> 22) ^ h) / (float)uint.MaxValue;
        }
    }

    // ── THE TEXTURES (built once per device, white premultiplied masks: the tint is the colour) ──────────────────────

    private sealed class Textures
    {
        private static Textures? _built;
        private static GraphicsDevice? _device;

        public Texture2D Pixel = null!, Puff = null!, Band = null!, Blob = null!;
        public Texture2D[] Blight = null!, Cracks = null!;

        public static Textures For(GraphicsDevice device)
        {
            if (_built is not null && _device == device) return _built;
            _device = device;
            _built = Build(device);
            return _built;
        }

        private static Texture2D Mask(GraphicsDevice d, int w, int h, float[] a)
        {
            var data = new Color[w * h];
            for (var i = 0; i < data.Length; i++)
            {
                var v = (byte)Math.Clamp((int)MathF.Round(a[i] * 255f), 0, 255);
                data[i] = new Color(v, v, v, v);
            }
            var tex = new Texture2D(d, w, h);
            tex.SetData(data);
            return tex;
        }

        private static Textures Build(GraphicsDevice d)
        {
            var t = new Textures { Pixel = Mask(d, 1, 1, new[] { 1f }) };
            // a soft puff
            const int P = 48;
            var puff = new float[P * P];
            for (var y = 0; y < P; y++)
                for (var x = 0; x < P; x++)
                {
                    var r = MathF.Sqrt(Sq((x + 0.5f) / P * 2f - 1f) + Sq((y + 0.5f) / P * 2f - 1f));
                    puff[y * P + x] = r >= 1f ? 0f : Sq(1f - r * r);
                }
            t.Puff = Mask(d, P, P, puff);
            // the sweep band
            var band = new float[4 * 64];
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 4; x++) band[y * 4 + x] = Sq(MathF.Sin(MathF.PI * (y + 0.5f) / 64f));
            t.Band = Mask(d, 4, 64, band);
            // the presence: a soft, irregular dark mass
            const int B = 96;
            var blob = new float[B * B];
            for (var y = 0; y < B; y++)
                for (var x = 0; x < B; x++)
                {
                    var dx = (x + 0.5f) / B * 2f - 1f;
                    var dy = (y + 0.5f) / B * 2f - 1f;
                    var ang = MathF.Atan2(dy, dx);
                    var warp = 1f + 0.22f * MathF.Sin(ang * 3f + 0.6f) + 0.12f * MathF.Sin(ang * 5f + 2f);
                    var r = MathF.Sqrt(dx * dx + dy * dy) * warp;
                    blob[y * B + x] = r >= 1f ? 0f : MathF.Pow(1f - r * r, 1.6f);
                }
            t.Blob = Mask(d, B, B, blob);
            (t.Blight, t.Cracks) = BuildBlight(d);
            return t;
        }

        private static float Sq(float x) => x * x;

        /// <summary>Blotches of blight at three coverages, and the dry cracks along their edges.</summary>
        private static (Texture2D[] Blight, Texture2D[] Cracks) BuildBlight(GraphicsDevice d)
        {
            const int N = 256;
            var noise = new float[N * N];
            var fine = new float[N * N];
            for (var y = 0; y < N; y++)
                for (var x = 0; x < N; x++)
                {
                    noise[y * N + x] = 0.55f * Value(x / 42f, y / 42f, 1) + 0.3f * Value(x / 19f, y / 19f, 2) + 0.15f * Value(x / 8f, y / 8f, 3);
                    fine[y * N + x] = Value(x / 3.5f, y / 3.5f, 4);
                }
            var thresholds = new[] { 0.60f, 0.52f, 0.44f };
            var blight = new Texture2D[3];
            var cracks = new Texture2D[3];
            for (var k = 0; k < 3; k++)
            {
                var th = thresholds[k];
                var a = new float[N * N];
                var c = new float[N * N];
                for (var i = 0; i < a.Length; i++)
                {
                    var n = noise[i];
                    var s = Math.Clamp((n - th) / 0.06f, 0f, 1f);
                    a[i] = s * s * (3f - 2f * s) * (0.65f + 0.35f * fine[i]);
                    var e = MathF.Abs(n - th - 0.012f);
                    c[i] = e < 0.01f ? 1f - e / 0.01f : 0f;
                    // a few fractures inside the blotch
                    var inner = MathF.Abs(n - th - 0.08f);
                    if (n > th + 0.05f && inner < 0.006f) c[i] = Math.Max(c[i], 0.8f * (1f - inner / 0.006f));
                }
                blight[k] = Mask(d, N, N, a);
                cracks[k] = Mask(d, N, N, c);
            }
            return (blight, cracks);
        }

        private static float[] BoxBlur(float[] src, int n, int r)
        {
            var tmp = new float[src.Length];
            var dst = new float[src.Length];
            for (var y = 0; y < n; y++)
            {
                float acc = 0f;
                for (var x = -r; x <= r; x++) acc += src[y * n + Math.Clamp(x, 0, n - 1)];
                for (var x = 0; x < n; x++)
                {
                    tmp[y * n + x] = acc / (2 * r + 1);
                    acc += src[y * n + Math.Min(n - 1, x + r + 1)] - src[y * n + Math.Max(0, x - r)];
                }
            }
            for (var x = 0; x < n; x++)
            {
                float acc = 0f;
                for (var y = -r; y <= r; y++) acc += tmp[Math.Clamp(y, 0, n - 1) * n + x];
                for (var y = 0; y < n; y++)
                {
                    dst[y * n + x] = acc / (2 * r + 1);
                    acc += tmp[Math.Min(n - 1, y + r + 1) * n + x] - tmp[Math.Max(0, y - r) * n + x];
                }
            }
            return dst;
        }

        private static float Value(float x, float y, int salt)
        {
            int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float H(int i, int j) => Hash(i * 73856093 ^ j * 19349663 ^ salt * 83492791);
            var a = H(xi, yi) + (H(xi + 1, yi) - H(xi, yi)) * fx;
            var b2 = H(xi, yi + 1) + (H(xi + 1, yi + 1) - H(xi, yi + 1)) * fx;
            return a + (b2 - a) * fy;
        }
    }
}
