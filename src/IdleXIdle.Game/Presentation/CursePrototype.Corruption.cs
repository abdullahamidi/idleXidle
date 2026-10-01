using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using IdleXIdle.Game.Vfx;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// CONCEPT A, LIVING SHADOW CORRUPTION (chosen by the owner 2026-10-01), the glow pass: dark Shadow veins as the BODY of
/// the curse, a violet-magenta EMISSION on some of them as its second read, so a cursed creature is found at once even
/// when it is near-black.
/// </summary>
/// <remarks>
/// <para>
/// THE STRUCTURE (<see cref="CorruptionAtlas"/>): veins GROW along paths from the seat (each texel's birth is its path
/// length, not its distance from the centre), from a few asymmetric roots, with wandering thickness, broken stretches,
/// branches that die early and smoky blotches at some junctions; a vein that runs into another stops (no loop, no mesh:
/// the first A read "a net / a web / a sigil"). Depth is REACH along the paths: more body, more branching, more infected
/// regions, never simply more light.
/// </para>
/// <para>
/// THE MATERIAL: dark first (the veins, a faint darkening of the body), violet second (an additive emission clipped to
/// the body: only some segments glow, junction nodes more, the rest stay dark). At rest a slow breath and one section at a
/// time gently answering. An apply, an arrival and a deepen grow the NEW paths with a bright front travelling through
/// them only, then settle; the SPRAWL source lights before its victims ignite; a dying host brightens and collapses back
/// along its paths.
/// </para>
/// </remarks>
public sealed partial class CursePrototype
{
    // the dark body of a vein, and its light: violet-magenta at rest, hotter on a growing front
    private static readonly Color VeinDark = new(22, 10, 38);

    // THE STAIN (multiply): the curse DISCOLOURS the host's own material, keeping its folds and shading, instead of lying
    // on top of it (drawn over pale cloth or armour, dark lines read as "embroidery", "stitching", "a sash", "trim").
    // Veins stain it a deep bruised violet; the infected tissue round them a greyed lavender.
    private static readonly Color VeinStain = new(46, 18, 78);
    private static readonly Color BruiseStain = new(128, 92, 150);
    private static readonly Color WispSmoke = new(62, 34, 92);
    private static readonly Color AfflictedShade = new(150, 124, 176);

    // DEPTH 1'S HIGH-CONTRAST REGION, suited to the host (a rule of its brightness, never of its name; fresh reads at
    // depth 1, whole bosses at play size, 2026-10-01): on a PALE body the bruise is a deep SATURATED violet (the greyed
    // lavender read as "dirt", "a smudge", "a dye stain" on pale cloth); on a DARK one the infected region is LIT from
    // inside by a dim violet haze screened over it (multiplied, a bruise cannot show on black: "it nearly disappears")
    private static readonly Color PaleBruise = new(104, 46, 168);
    private static readonly Color TissueGlow = new(112, 44, 178);

    /// <summary>
    /// SCREEN for the curse's STEADY light: src x (1 - dst) + dst. It glows fully on a dark body and in the shadowed
    /// folds and creases of a pale one, and fades where the host is already bright, so the light sits IN the host's
    /// own shading instead of lying across it (additive light on pale cloth and armour read as "a ribbon", "a sash",
    /// "a brooch", "painted trim"). The growing front keeps its additive flare.
    /// </summary>
    private static readonly BlendState Screen = new()
    {
        Name = "CursePrototype.Screen",
        ColorSourceBlend = Blend.InverseDestinationColor,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };

    /// <summary>Multiply for premultiplied sources: dst x lerp(1, tint, alpha).</summary>
    private static readonly BlendState Stain = new()
    {
        Name = "CursePrototype.Stain",
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.InverseSourceAlpha,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };
    private static readonly Color Emission = new(168, 70, 236);
    private static readonly Color EmissionHot = new(236, 150, 255);

    private const float GrowMs = 540f;          // an apply / arrival grows the curse in over this
    private const float DeepenGrowMs = 420f;    // a deepen grows its new paths over this
    private const float SettleMs = 380f;        // then the extra light settles
    private const float IdleEmission = 0.85f;   // the resting emission (the same at every depth: the reach is the depth)
    private const float FrontPeak = 0.72f;      // a growing front at its brightest (0.95 took the deepen to depth 3 above PRESS)
    private const float QuietShare = 0.55f;     // beside an action, a reaction or the field's crush
    private const float ArrivalShare = 0.55f;   // a transfer's / a hop's awakening, against the apply

    private CorruptionAtlas? _corruption;

    private CorruptionAtlas Corruption(GraphicsDevice device) => _corruption ??= CorruptionAtlas.For(device);

    /// <summary>Each creature's corruption is the grown field mirrored on alternate slots (a row is never a stamp).</summary>
    private SpriteEffects _veinFlip;

    /// <summary>The field runs along the body's LONG axis: grown upright, it is turned a quarter on a body wider than it
    /// is tall (a whelp), so the corruption spreads along the torso and never round a waist.</summary>
    private float _veinTurn;

    private void V(SpriteBatch b, Texture2D tex, Rectangle r, Color c)
    {
        var half = CorruptionAtlas.N * 0.5f;
        b.Draw(tex, r.Center.ToVector2(), null, c, _veinTurn, new Vector2(half, half), r.Width / (float)CorruptionAtlas.N, _veinFlip, 0f);
        SpriteCount++;
    }

    private void Orient(int slot, Rectangle body)
    {
        var flip = slot % 2 == 1;
        _veinFlip = flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        // on a wide body the field is turned so its own long axis (~5 degrees in the
        // texture, measured) runs along the torso; turned a flat quarter, the territories fell off a whelp's silhouette
        _veinTurn = body.Width > body.Height * 1.05f ? (flip ? WideTurn : -WideTurn) : 0f;
    }

    /// <summary>The field's long axis in the texture (upper territory to lower), which a wide body turns level.</summary>
    private const float WideTurn = 0.09f;   // measured: the depth-3 coverage's principal axis, 5 degrees off level (ratio 1.27)

    private Rectangle VeinRect(Vector2 seat, Rectangle body)
    {
        // SIZED BY THE WHOLE BODY, never stretched to its width (stretched, a trunk crossing a narrow waist became a band
        // from side to side: "a belt", "a sash tied round the waist"); capped so a boss's curse stays a curse
        // (the same share of a large body as of a small one, up to 660 px: capped lower, on a boss it was a patch the
        // size of a belt buckle or a brooch and read as one; spread over several regions it is no garment)
        var size = Math.Clamp(MathF.Sqrt(body.Width * (float)body.Height) * 1.4f, 240f, 660f);
        return new Rectangle((int)(seat.X - size / 2), (int)(seat.Y - size / 2), (int)size, (int)size);
    }

    /// <summary>
    /// HOST-AWARE LIGHT (a rule of the host's brightness, never of its name): the violet emission's share on this body.
    /// Additive light GLOWS on a dark body but only TINTS a pale one, where a lit knot read as "a brooch", "a bow", "a
    /// knot of yarn" and a lit line as "a ribbon", "a cord" (fresh reads, 2026-10-01): full on a dark body, about half
    /// on a pale one, where the dark corruption leads.
    /// </summary>
    private static float LightFor(float luma)
    {
        // (the screened light already fades on bright pixels, per pixel; the host rule trims the additive front a little
        // on a pale body, where it was the loudest, and on a near-black body LETS THE VIOLET CARRY THE STATE: up to 1.4x,
        // since the dark corruption cannot show on black and the violet sat close to such a host's own outline)
        var p = Math.Clamp((luma - 0.35f) / 0.35f, 0f, 1f);
        var dark = Math.Clamp((0.2f - luma) / 0.12f, 0f, 1f);
        return (1f - 0.15f * p * p * (3f - 2f * p)) * (1f + 0.4f * dark * dark * (3f - 2f * dark));
    }

    private static readonly Dictionary<Texture2D, float> HostLumaCache = new();

    /// <summary>The host's own brightness: the mean luma of its opaque pixels in the frame first seen from this strip
    /// (read once per strip and kept; a prototype's read-back, not a production path).</summary>
    internal static float HostLuma(SpriteFrame frame)
    {
        if (HostLumaCache.TryGetValue(frame.Texture, out var known)) return known;
        var src = frame.Src;
        var data = new Color[src.Width * src.Height];
        frame.Texture.GetData(0, src, data, 0, data.Length);
        double sum = 0;
        var n = 0;
        foreach (var c in data)
        {
            if (c.A < 128) continue;
            var a = c.A / 255.0;
            sum += (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0 / a;
            n++;
        }
        var luma = n == 0 ? 0.3f : (float)Math.Clamp(sum / n, 0.0, 1.0);
        HostLumaCache[frame.Texture] = luma;
        if (PresentTrace.Enabled) PresentTrace.Log("curse-host", $"luma={luma:0.000}	light={LightFor(luma):0.00}	src={src.Width}x{src.Height}");
        return luma;
    }

    private static float Ramp(float x, float a, float b)
    {
        var t = Math.Clamp((x - a) / (b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>1 on a near-black host (its infected region is lit from inside), 0 from a mid-dark one.</summary>
    private static float DarkHost(float luma) => Ramp(luma, 0.32f, 0.12f);

    /// <summary>1 on a pale host (its bruise is a deep saturated violet), 0 below a mid one.</summary>
    private static float PaleHost(float luma) => Ramp(luma, 0.42f, 0.62f);

    private static Color BruiseFor(float luma) => Color.Lerp(BruiseStain, PaleBruise, PaleHost(luma));

    /// <summary>
    /// THE CURSE SITS IN THE BODY'S MASS (a rule of the host's own silhouette, never of its name): the entry pocket is
    /// moved off a thin limb, a wrist, a weapon or a hem into the nearest part of the body at least
    /// <see cref="MassShare"/> as thick as its thickest. Pushed off-centre by the field, it landed on the Spirit Matron's
    /// outstretched wrist, beside the Reaper's hand on the scythe and on the Lich's sash, and was read as "a bracelet",
    /// "a spell in its hand", "a belt ornament" (fresh reads at depth 1, 2026-10-01: on a hand, a wrist or a garment's
    /// place it is the creature's own magic or its outfit; in the body's mass it is something done to it). Measured
    /// once per strip and orientation, so the pocket keeps its place through the strip's frames (a prototype's
    /// read-back, like <see cref="HostLuma"/>).
    /// </summary>
    private const float MassShare = 0.6f;

    private static readonly Dictionary<(Texture2D, bool), Vector2> SeatShiftCache = new();
    private Vector2[]? _seatShift;

    private Vector2 CorruptionSeat(int slot, Vector2 anchor, Rectangle body, SpriteFrame frame)
    {
        _seatShift ??= new Vector2[_kind.Length];
        Orient(slot, body);
        var key = (frame.Texture, _veinFlip == SpriteEffects.FlipHorizontally);
        if (!SeatShiftCache.TryGetValue(key, out var shift))
        {
            var pocket = FieldToScreen(CorruptionAtlas.Entry, VeinRect(anchor, body));
            var src = frame.Src;
            var data = new Color[src.Width * src.Height];
            frame.Texture.GetData(0, src, data, 0, data.Length);
            var alpha = new byte[data.Length];
            for (var i = 0; i < data.Length; i++) alpha[i] = data[i].A;
            var flipX = (frame.Effects & SpriteEffects.FlipHorizontally) != 0;
            var kx = frame.Dest.Width / (float)src.Width;
            var ky = frame.Dest.Height / (float)src.Height;
            var lx = (pocket.X - frame.Dest.X) / kx;
            if (flipX) lx = src.Width - lx;
            var to = ThickNear(alpha, src.Width, src.Height, new Vector2(lx, (pocket.Y - frame.Dest.Y) / ky), MassShare);
            var tx = flipX ? src.Width - to.X : to.X;
            shift = new Vector2(frame.Dest.X + tx * kx, frame.Dest.Y + to.Y * ky) - pocket;
            SeatShiftCache[key] = shift;
            if (PresentTrace.Enabled) PresentTrace.Log("curse-seat", $"shift={shift.X:0},{shift.Y:0}	pocket={pocket.X:0},{pocket.Y:0}	src={src.Width}x{src.Height}");
        }
        _seatShift[slot] = shift;
        return anchor + shift;
    }

    /// <summary>
    /// The point nearest <paramref name="p"/> (source pixels) where the silhouette is at least <paramref name="share"/>
    /// as thick as at its thickest (a chamfer distance to the outline, on a coarse grid); <paramref name="p"/> itself
    /// when it is already that deep in the body.
    /// </summary>
    internal static Vector2 ThickNear(byte[] alpha, int w, int h, Vector2 p, float share)
    {
        var cell = Math.Max(2, Math.Max(w, h) / 96);
        int gw = (w + cell - 1) / cell, gh = (h + cell - 1) / cell;
        var d = new float[gw * gh];
        for (var cy = 0; cy < gh; cy++)
            for (var cx = 0; cx < gw; cx++)
            {
                int solid = 0, all = 0;
                for (var y = cy * cell; y < Math.Min(h, cy * cell + cell); y++)
                    for (var x = cx * cell; x < Math.Min(w, cx * cell + cell); x++)
                    {
                        all++;
                        if (alpha[y * w + x] >= 128) solid++;
                    }
                d[cy * gw + cx] = solid * 2 > all ? float.MaxValue : 0f;
            }
        // two chamfer passes (1 straight, 1.414 diagonal); beyond the grid is outside the body
        float At(int x, int y) => x < 0 || y < 0 || x >= gw || y >= gh ? 0f : d[y * gw + x];
        for (var y = 0; y < gh; y++)
            for (var x = 0; x < gw; x++)
            {
                var i = y * gw + x;
                if (d[i] == 0f) continue;
                d[i] = Math.Min(d[i], Math.Min(Math.Min(At(x - 1, y), At(x, y - 1)) + 1f, Math.Min(At(x - 1, y - 1), At(x + 1, y - 1)) + 1.414f));
            }
        for (var y = gh - 1; y >= 0; y--)
            for (var x = gw - 1; x >= 0; x--)
            {
                var i = y * gw + x;
                if (d[i] == 0f) continue;
                d[i] = Math.Min(d[i], Math.Min(Math.Min(At(x + 1, y), At(x, y + 1)) + 1f, Math.Min(At(x + 1, y + 1), At(x - 1, y + 1)) + 1.414f));
            }
        var max = 0f;
        foreach (var v in d) max = Math.Max(max, v);
        if (max <= 0f) return p;
        var need = share * max;
        if (At((int)(p.X / cell), (int)(p.Y / cell)) >= need) return p;
        var best = 0;
        var bestD = float.MaxValue;
        for (var i = 0; i < d.Length; i++)
        {
            if (d[i] < need) continue;
            var dx = (i % gw + 0.5f) * cell - p.X;
            var dy = (i / gw + 0.5f) * cell - p.Y;
            if (dx * dx + dy * dy < bestD) { bestD = dx * dx + dy * dy; best = i; }
        }
        return new Vector2((best % gw + 0.5f) * cell, (best / gw + 0.5f) * cell);
    }

    /// <summary>How much this slot's SPRAWL source is lit: a hop leaving it (the curse gathering in the source).</summary>
    private float SourceGlow(int slot, float playheadMs)
    {
        var best = 0f;
        for (var j = 0; j < _kind.Length; j++)
        {
            if (j == slot || Mark.HopFromOf(j) != slot) continue;
            var age = playheadMs - Mark.HopLeavesAt(j) + 120f;   // it gathers a little before the hop leaves
            if (age < 0f || age > 520f) continue;
            var e = age < 120f ? age / 120f : 1f - (age - 120f) / 400f;
            best = Math.Max(best, e);
        }
        return best;
    }

    private void DrawCorruptionGlow(SpriteBatch b, int slot, float playheadMs, float t, Rectangle body, Vector2 seat, RasterizerState raster,
                                    SpriteFrame frame)
    {
        var atlas = Corruption(b.GraphicsDevice);
        var luma = HostLuma(frame);
        var light = LightFor(luma);
        var dark = DarkHost(luma);
        var bruise = BruiseFor(luma);
        // on a pale host the saturated bruise is laid twice (a deeper violet in the cloth: once, it read as "a dye stain")
        var deep = 0.5f * PaleHost(luma);
        Orient(slot, body);
        var stage = _stage[slot];
        var kind = _flareKind[slot];
        var age = _flareAge[slot];
        var rect = VeinRect(seat, body);
        var inside = _inside[slot];
        var quiet = kind == 0 ? 1f : Mark.TickQuietNear(playheadMs - age, 450f) ? QuietShare : 1f;

        // ── THE DARK BODY: a stain in the host's own material ──
        b.Begin(SpriteSortMode.Deferred, Stain, SamplerState.LinearClamp, inside, raster);
        if (_kind[slot] == 1)
        {
            Fill(b, body, Color.Black * 0.05f);   // waiting: the curse is on its way, a faint shadow only
            b.End();
            return;
        }
        // THE WHOLE BODY IS AFFLICTED: a violet-grey shadow over the entire host, a little deeper with each depth (a
        // tint, never a glow; clipped to the silhouette): the host looks cursed as a whole and the patches read as where
        // it comes from, not as something it wears
        Fill(b, body, AfflictedShade * (0.22f + 0.08f * stage));
        float grown;   // 0..1: how far the growth this flare runs has got (1: settled)
        if (kind is 1 or 2)
        {
            grown = Smooth(Math.Min(1f, age / GrowMs));
            V(b, atlas.Tissue[stage - 1], rect, bruise * grown);
            if (deep > 0f) V(b, atlas.Tissue[stage - 1], rect, bruise * (deep * grown));
            DarkGrowth(b, atlas, rect, 0f, stage, grown * stage);
        }
        else if (kind == 3 && stage > 1)
        {
            grown = Smooth(Math.Min(1f, age / DeepenGrowMs));
            V(b, atlas.Tissue[stage - 2], rect, bruise * (1f - grown));
            V(b, atlas.Tissue[stage - 1], rect, bruise * grown);
            if (deep > 0f)
            {
                V(b, atlas.Tissue[stage - 2], rect, bruise * (deep * (1f - grown)));
                V(b, atlas.Tissue[stage - 1], rect, bruise * (deep * grown));
            }
            V(b, atlas.Dark[stage - 2], rect, VeinStain);
            DarkGrowth(b, atlas, rect, stage - 1, stage, stage - 1 + grown);
        }
        else
        {
            grown = 1f;
            V(b, atlas.Tissue[stage - 1], rect, bruise);
            if (deep > 0f) V(b, atlas.Tissue[stage - 1], rect, bruise * deep);
            V(b, atlas.Dark[stage - 1], rect, VeinStain);
        }
        b.End();

        // ── THE LIGHT, clipped to the body: the steady light SCREENED into the host, the growing front additive ──
        b.Begin(SpriteSortMode.Deferred, Screen, SamplerState.LinearClamp, inside, raster);
        var breath = 1f + 0.12f * MathF.Sin(t * MathF.Tau / 5200f);
        var settleAge = kind == 1 || kind == 2 ? age - GrowMs : kind == 3 ? age - DeepenGrowMs : float.PositiveInfinity;
        var settle = settleAge < 0f ? 0f : settleAge >= SettleMs ? 1f : settleAge / SettleMs;
        var afterglow = settleAge >= 0f && settleAge < SettleMs ? 0.5f * (1f - settle) * quiet : 0f;
        var source = SourceGlow(slot, playheadMs);
        var level = IdleEmission * light * breath * (1f + afterglow + 0.9f * source);
        // ON A DARK HOST THE INFECTED REGION IS LIT FROM INSIDE: a dim violet haze over the whole tissue, breathing with
        // the light (depth 1's high-contrast region where the dark corruption cannot show; it grows in with the tissue)
        if (dark > 0f)
        {
            var haze = TissueGlow * (0.7f * dark * breath);
            if (kind is 1 or 2)
                V(b, atlas.Tissue[stage - 1], rect, haze * grown);
            else if (kind == 3 && stage > 1)
            {
                V(b, atlas.Tissue[stage - 2], rect, haze * (1f - grown));
                V(b, atlas.Tissue[stage - 1], rect, haze * grown);
            }
            else
                V(b, atlas.Tissue[stage - 1], rect, haze);
        }
        if (kind is 1 or 2)
        {
            // the settled light fades in behind the growth
            V(b, atlas.Emit[stage - 1], rect, Emission * (level * Smooth(grown) * (settleAge < 0f ? 0.35f : 0.35f + 0.65f * settle)));
        }
        else if (kind == 3 && stage > 1)
        {
            // the old light stays as it was; only the NEW paths light as they form
            V(b, atlas.Emit[stage - 2], rect, Emission * (level * (1f - (settleAge < 0f ? 0f : settle))));
            if (settleAge >= 0f) V(b, atlas.Emit[stage - 1], rect, Emission * (level * settle));
        }
        else
            V(b, atlas.Emit[stage - 1], rect, Emission * level);
        // ONE SECTION AT A TIME gently answers (a 4.2 s cycle per section, each slot at its own moment)
        var cycle = 4200f;
        var n = (int)MathF.Floor(t / cycle);
        var ph = (t - n * cycle) / cycle;
        var answer = MathF.Sin(ph * MathF.PI);
        V(b, atlas.Idle[(stage - 1) * CorruptionAtlas.IdleGroups + ((n % CorruptionAtlas.IdleGroups) + CorruptionAtlas.IdleGroups) % CorruptionAtlas.IdleGroups],
             rect, Emission * (0.30f * light * answer * answer * (kind == 0 ? 1f : settle)));
        b.End();
        if (kind == 0 || grown >= 1f) return;
        b.Begin(SpriteSortMode.Deferred, VfxBlend.PremultipliedAdditive, SamplerState.LinearClamp, inside, raster);
        if (kind is 1 or 2)
            // an ARRIVAL (a transfer, a hop) wakes quieter than the apply: it grows through every depth it carries at once,
            // and at full strength it was the loudest thing the curse ever drew
            Front(b, atlas, rect, 0f, stage, grown * stage, FrontPeak * light * quiet * (kind == 2 ? ArrivalShare : 1f));
        else if (stage > 1)
            Front(b, atlas, rect, stage - 1, stage, stage - 1 + grown, FrontPeak * light * quiet);
        b.End();
    }

    /// <summary>The dark veins grown from <paramref name="from"/> to <paramref name="at"/> (in stages: 1.5 = halfway from
    /// depth 1 to depth 2), transition by transition, each in its own frames.</summary>
    private void DarkGrowth(SpriteBatch b, CorruptionAtlas atlas, Rectangle rect, float from, int to, float at)
    {
        for (var tr = (int)from; tr < to; tr++)
        {
            var p = Math.Clamp(at - tr, 0f, 1f);
            if (p <= 0f) break;
            var f = (int)MathF.Ceiling(p * CorruptionAtlas.Frames) - 1;
            V(b, atlas.Grow[tr * CorruptionAtlas.Frames + Math.Clamp(f, 0, CorruptionAtlas.Frames - 1)], rect, VeinStain);
        }
    }

    /// <summary>The bright growing FRONT at <paramref name="at"/> (in stages), travelling through the new paths only.</summary>
    private void Front(SpriteBatch b, CorruptionAtlas atlas, Rectangle rect, float from, int to, float at, float peak)
    {
        if (at >= to - 0.001f || peak <= 0f) return;
        var tr = Math.Clamp((int)MathF.Floor(at), (int)from, to - 1);
        var p = Math.Clamp(at - tr, 0f, 1f);
        var x = p * (CorruptionAtlas.Frames - 1);
        var i = (int)MathF.Floor(x);
        var w = x - i;
        var ramp = Math.Min(1f, (at - from) * 6f);   // it kindles in, rather than switching on
        V(b, atlas.Front[tr * CorruptionAtlas.Frames + i], rect, EmissionHot * (peak * ramp * (1f - w)));
        if (i + 1 < CorruptionAtlas.Frames) V(b, atlas.Front[tr * CorruptionAtlas.Frames + i + 1], rect, EmissionHot * (peak * ramp * w));
    }

    /// <summary>A dying host's curse: it brightens once, then collapses back along its paths to the seat.</summary>
    private void DrawCorruptionLeaving(SpriteBatch b, int slot, int stage, float age, Rectangle rect, Rectangle body, DepthStencilState inside, RasterizerState raster,
                                       SpriteFrame frame)
    {
        var luma = HostLuma(frame);
        var light = LightFor(luma);
        Orient(slot, body);
        var atlas = Corruption(b.GraphicsDevice);
        const float flash = 150f, collapse = 600f;
        var p = age < flash ? 0f : Math.Min(1f, (age - flash) / collapse);
        var at = stage * (1f - Smooth(p));
        b.Begin(SpriteSortMode.Deferred, Stain, SamplerState.LinearClamp, inside, raster);
        V(b, atlas.Tissue[stage - 1], rect, BruiseFor(luma) * (1f - p));
        DarkGrowth(b, atlas, rect, 0f, stage, at);
        b.End();
        if (age < flash)
        {
            b.Begin(SpriteSortMode.Deferred, Screen, SamplerState.LinearClamp, inside, raster);
            V(b, atlas.Emit[stage - 1], rect, Emission * (IdleEmission * light * (1f + 0.8f * MathF.Sin(age / flash * MathF.PI))));
            b.End();
        }
        else if (at > 0.02f)
        {
            b.Begin(SpriteSortMode.Deferred, VfxBlend.PremultipliedAdditive, SamplerState.LinearClamp, inside, raster);
            Front(b, atlas, rect, 0f, stage, at, FrontPeak * light * 0.8f * (1f - p));
            b.End();
        }
    }

    private void Streak(SpriteBatch b, Vector2 at, float w, float h, Color c)
    {
        if (c.A == 0) return;
        b.Draw(_puff, new Rectangle((int)(at.X - w / 2), (int)(at.Y - h / 2), (int)w, (int)h), c);
        SpriteCount++;
    }

    /// <summary>A texel of the corruption field on screen (the same transform <see cref="V"/> draws with).</summary>
    private Vector2 FieldToScreen(Vector2 texel, Rectangle rect)
    {
        var half = CorruptionAtlas.N * 0.5f;
        var d = texel - new Vector2(half, half);
        if (_veinFlip == SpriteEffects.FlipHorizontally) d.X = -d.X;
        d *= rect.Width / (float)CorruptionAtlas.N;
        var c = MathF.Cos(_veinTurn);
        var sn = MathF.Sin(_veinTurn);
        return rect.Center.ToVector2() + new Vector2(d.X * c - d.Y * sn, d.X * sn + d.Y * c);
    }

    /// <summary>
    /// THE CURSE LEAKS: each infected territory reached at this depth breathes out ONE slow wisp of shadow with a faint
    /// violet core, rising out of the body past its outline (cloth and armour cannot do that: a stain clipped inside
    /// the silhouette read as "a sash", "a belt", "a bodice", "a pendant" in stills). Quiet: one wisp per territory,
    /// ~2.8 s each, low alpha; more territories as the curse deepens.
    /// </summary>
    private void DrawCorruptionWisps(SpriteBatch b, int slot, float t, Rectangle body, Vector2 seat, RasterizerState raster)
    {
        if (_kind[slot] != 2) return;
        var atlas = Corruption(b.GraphicsDevice);
        Orient(slot, body);
        var rect = VeinRect(seat, body);
        var reach = CorruptionAtlas.Reach[Math.Clamp(_stage[slot], 1, 3)] * atlas.Longest;
        var rise = Math.Clamp(body.Height * 0.2f, 40f, 90f);
        var drift = Math.Clamp(body.Width * 0.3f, 30f, 110f);
        var size = Math.Clamp(body.Height * 0.1f, 22f, 46f);
        const float life = 3200f;
        for (var pass = 0; pass < 2; pass++)
        {
            b.Begin(SpriteSortMode.Deferred, pass == 0 ? BlendState.AlphaBlend : Screen, SamplerState.LinearClamp, DepthStencilState.None, raster);
            for (var w = 0; w < atlas.Seats.Count * 2; w++)
            {
                var k = w >> 1;
                var (at, birth) = atlas.Seats[k];
                if (birth > reach) continue;
                // two wisps per territory, half a life apart, so one is always rising
                var age = ((t + k * 977f + slot * 431f + (w & 1) * life * 0.5f) % life + life) % life;
                var q = age / life;
                var env = MathF.Pow(MathF.Sin(q * MathF.PI), 1.3f);
                var from = FieldToScreen(at, rect);
                var sway = MathF.Sin(q * 3.1f + w * 1.7f) * size * 0.35f;
                // it drifts OUT of the body (away from its centre line) as it rises, across the outline
                var outward = from.X >= body.Center.X ? 1f : -1f;
                var p = from + new Vector2(sway + outward * drift * q * q, -rise * q);
                var sz = size * (0.55f + 0.7f * q);
                // (violet-grey smoke, not black: black smoke vanished on the dark arena and the dark bodies)
                // a RISING STREAK of smoke, taller than wide (a round puff read as "an orb" floating by the body)
                if (pass == 0)
                    Streak(b, p, sz * 0.62f, sz * 1.5f, WispSmoke * (0.62f * env));
                else
                    Streak(b, p + new Vector2(0f, sz * 0.12f), sz * 0.36f, sz * 0.95f, Emission * (0.5f * env * (1f - 0.5f * q)));
            }
            b.End();
        }
    }

    private void DrawCorruptionLoose(SpriteBatch b, int slot, float t, Rectangle body, Vector2 seat)
    {
        // THE LEAKS: a wisp of shadow per depth at rest, faint; a few more as it grows
        var stage = _stage[slot];
        var f = _flare[slot];
        var count = stage + (f > 0.05f ? 4 : 0);
        for (var i = stage; i < count; i++)
        {
            var burst = i >= stage;
            var life = burst ? FlareMs : 2400f;
            var age = burst ? _flareAge[slot] - (i - stage) * 60f : ((t + i * 733f) % life + life) % life;
            if (age < 0f || age >= life) continue;
            var q = age / life;
            var hx = Hash(slot * 13 + i * 5) - 0.5f;
            var hy = Hash(slot * 7 + i * 11) - 0.5f;
            var from = seat + new Vector2(hx * body.Width * 0.45f, hy * body.Height * 0.4f);
            var at = from + new Vector2(MathF.Sin(q * 4f + i) * 4f, -q * (burst ? 40f : 24f));
            var a = (burst ? 0.26f * f : 0.09f) * MathF.Sin(q * MathF.PI);
            Puff(b, at, body.Height * (burst ? 0.08f : 0.065f) * (0.7f + q), Ink * a);
        }
    }

    /// <summary>
    /// The corruption's textures, built once per device: white premultiplied masks (the tint is the colour), N x N,
    /// centred on the seat. <see cref="Dark"/> and <see cref="Emit"/> per depth; <see cref="Idle"/> the resting emission
    /// split into sections that answer in turn; <see cref="Grow"/> the NEW dark paths of each transition (0 -> 1, 1 -> 2,
    /// 2 -> 3) in <see cref="Frames"/> steps; <see cref="Front"/> the bright front travelling through them.
    /// </summary>
    internal sealed class CorruptionAtlas
    {
        public const int N = 320;
        public const int Frames = 7;
        public const int IdleGroups = 3;
        private const int SeatVeinlets = 3;

        private static CorruptionAtlas? _built;
        private static GraphicsDevice? _device;

        public Texture2D[] Dark = null!, Emit = null!, Idle = null!, Grow = null!, Front = null!, Tissue = null!;

        /// <summary>The corruption's path reach at each depth, as a share of the longest path (depth 0 = nothing).</summary>
        public static readonly float[] Reach = { 0f, 0.24f, 0.5f, 0.86f };

        // the grown field, texel by texel (kept for the tests and the measures)
        internal float[] Cover = null!, Birth = null!, Glow = null!, Blot = null!, BlotBirth = null!, Leak = null!;
        internal int[] Group = null!;
        internal float Longest;

        /// <summary>Where each infected territory sits (texel space) and when it is born (path length): the entry
        /// pocket, the side and lower territories, and the upper one at its path's end. Each leaks a shadow wisp.</summary>
        internal readonly List<(Vector2 At, float Birth)> Seats = new();

        /// <summary>Where the corruption ENTERED (texel space): off the field's centre, so no path is the body's axis.</summary>
        /// (well off the torso's centre line, toward one flank: a patch on the centre line sat where a belt buckle, a
        /// brooch or a bodice goes, and read as one; garments are symmetric about that line, an infection is not)
        internal static readonly Vector2 Entry = new(N / 2f + 28f, N / 2f - 6f);

        public static CorruptionAtlas For(GraphicsDevice device)
        {
            if (_built is not null && _device == device) return _built;
            _device = device;
            var a = Grow_(20261001);
            a.Upload(device);
            _built = a;
            return _built;
        }

        // a growing vein: Kind 0 a branch, 1 a PATH to another territory (crooked, broken once, ends in a territory),
        // 2 a veinlet of the entry pocket, 3 a territory's fork
        private readonly record struct Tip(float X, float Y, float Ang, float W0, float Len, float MaxLen, int Parent, int Side, int Kind, bool Lit);

        /// <summary>Grows the field (no device: the tests read it).</summary>
        /// <remarks>
        /// THE COMPOSITION (owner's brief, 2026-10-01): an off-centre ENTRY POCKET (a smoky infection patch leaking violet
        /// from inside, with short uneven veinlets: depth 1); two crooked PATHS to other territories, each broken once by
        /// a gap that only smoke bridges, and one short branch across the body's axis; each path opens into a TERRITORY
        /// (forks fanning out, one of them sideways, and infected patches). No one line runs through the body (two
        /// trunks up and down read as "a zipper", "a seam", "a sash" on a robe), no loop closes, no path meets another.
        /// THE LIGHT: short, tapered, irregular RUNS inside the veins (dark, a dim run, a brighter short one, dark), set
        /// a little off each vein's centre; no round node, no evenly spaced knots (gated knots read as "a string of beads").
        /// </remarks>
        internal static CorruptionAtlas Grow_(int seed)
        {
            var a = new CorruptionAtlas();
            var n2 = N * N;
            a.Cover = new float[n2];
            a.Birth = new float[n2];
            Array.Fill(a.Birth, float.PositiveInfinity);
            a.Glow = new float[n2];
            a.Blot = new float[n2];
            a.BlotBirth = new float[n2];
            Array.Fill(a.BlotBirth, float.PositiveInfinity);
            a.Leak = new float[n2];
            a.Group = new int[n2];
            var owner = new int[n2];
            Array.Fill(owner, -1);
            var rng = new Random(seed);
            float R() => (float)rng.NextDouble();
            var c = N / 2f;
            var maxR = N * 0.48f;
            var tips = new Queue<Tip>();
            var nextId = 0;
            var puffs = new List<(float X, float Y, float Birth, float Ang, float Size, float Leak)>();
            var ex = Entry.X;
            var ey = Entry.Y;
            a.Seats.Add((new Vector2(ex, ey), 0f));
            // THE ENTRY POCKET: a smoky patch, wider than tall, leaking violet from inside (depth 1's high-contrast region:
            // dark on a pale body, lit on a black one)
            // (big enough to be found at once on a boss: depth 1 is small, never faint)
            // (wide and loose, a bruise spreading: compact and lit it read as "a brooch", "a rosette", "a knot"; small
            // and tight at depth 1, "a bracelet", "a gem worn on the wrist", "small compared to the large creatures")
            for (var k = 0; k < 10; k++)
                puffs.Add((ex + (R() - 0.5f) * 80f, ey + (R() - 0.5f) * 38f, R() * 8f, R() * MathF.PI, 6.5f + R() * 6.5f, 0.68f));
            // ITS ACTIVE CORE: where the curse is still pouring in, an ELONGATED hot smear (depth 1's one cue that reads on
            // any host; never a round bead)
            puffs.Add((ex + (R() - 0.5f) * 8f, ey + (R() - 0.5f) * 4f, 0f, R() * MathF.PI, 7f, 1.0f));
            // its veinlets: few, uneven, bleeding out of ONE side of the stain (all round it, a stain with legs read as
            // "a spider", "a starburst"); the first two catch the light as they leave it
            var bleed = R() * MathF.Tau;
            // (grown AFTER the paths, so a veinlet heading into a path stops short of it; grown first, one curled up
            // beside the leaving path and closed a lens with it)
            var veinlets = new List<Tip>();
            for (var k = 0; k < SeatVeinlets; k++)
            {
                var ang = bleed + (k - 1) * 1.05f + (R() - 0.5f) * 0.6f;
                var from = 7f + R() * 5f;
                var born = 2f + R() * 8f;
                // (two of them longer and lit: depth 1's "two short branches" of light leaving the stain)
                veinlets.Add(new Tip(ex + MathF.Cos(ang) * from, ey + MathF.Sin(ang) * from * 0.7f, ang, 2.8f + R() * 1.0f,
                                     born, born + (k < 2 ? 18f : 10f) + R() * 16f, -2, R() < 0.5f ? -1 : 1, 2, k < 2));
            }
            // THE PATHS: short, crooked, broken, each opening into a territory: one up and to one side, one ACROSS the
            // body's axis to the other side. THE REGION IS TWO-DIMENSIONAL: territories laid along one line through the
            // pocket read as a garment on a torso whatever the line's bearing ("a zipper", "a seam" upright; "a sash"
            // shoulder to hip; "a belt" level), so they sit round the pocket, never in a row with it
            // (ONE path leaves the pocket: two leaving it drew a bent line through it, however they were aimed; the other
            // territories are reached by smoke alone)
            var paths = new (float Ang, float Len, float W, int Kind)[]
            {
                (-2.3f, 0.2f, 5.0f, 1),     // up, to one side: the upper territory
            };
            for (var r = 0; r < paths.Length; r++)
            {
                var (pa, pl, pw, kind) = paths[r];
                var ang = pa + (R() - 0.5f) * 0.25f;
                var start = 11f + R() * 5f;
                var born = 18f + r * 7f + R() * 6f;
                tips.Enqueue(new Tip(ex + MathF.Cos(ang) * start, ey + MathF.Sin(ang) * start * 0.7f, ang, pw + R() * 0.6f,
                                     born, born + N * pl * (0.9f + R() * 0.2f), -1, R() < 0.5f ? -1 : 1, kind, true));
            }
            // THE LOWER TERRITORY: its own infected region, below the pocket and off to the other side, joined to it only
            // by a trail of small smoke patches; it wakes as the curse deepens (born later than the pocket's veins) and
            // grows its own short veins, never toward the pocket
            // (below and to the SAME side as the upper territory: with the across path's territory on the other side the
            // three sit round the pocket; below and to the other side, they lined up shoulder to hip into "a sash")
            var lx = ex - 22f + R() * 8f;
            var ly = ey + 58f + R() * 8f;
            a.Seats.Add((new Vector2(lx, ly), 60f));
            for (var k = 1; k <= 3; k++)
            {
                var t = k / 4f;
                puffs.Add((ex + (lx - ex) * t + (R() - 0.5f) * 8f, ey + (ly - ey) * t, 26f + 10f * k, 1.2f + (R() - 0.5f) * 0.8f,
                           3f + R() * 2.5f, 0.22f));
            }
            for (var k = 0; k < 4; k++)
                puffs.Add((lx + (R() - 0.5f) * 20f, ly + (R() - 0.5f) * 14f, 58f + R() * 8f, R() * MathF.PI, 5f + R() * 4f, 0.5f));
            // (lopsided: one long root down and to one side, two short ones; two roots leaving it in opposite directions
            // drew a bar across the lower body)
            // (all to one side, down and away: roots in opposite directions crossed into an 'X')
            var lowerFans = new (float Ang, float Len)[] { (2.3f, 0.17f), (1.45f, 0.09f), (3.1f, 0.07f) };
            for (var k = 0; k < lowerFans.Length; k++)
            {
                var ang = lowerFans[k].Ang + (R() - 0.5f) * 0.3f;
                var born = 62f + k * 6f + R() * 4f;
                tips.Enqueue(new Tip(lx + MathF.Cos(ang) * 9f, ly + MathF.Sin(ang) * 6f, ang, 4.4f - k * 0.7f + R() * 0.5f,
                                     born, born + N * lowerFans[k].Len * (0.85f + R() * 0.3f), -1, k % 2 == 0 ? 1 : -1, 0, k != 1));
            }
            // THE SIDE TERRITORY: across the body's axis from the upper one, its own infected region joined to the pocket
            // by smoke only; its veins reach out and back across the axis, never to the pocket
            // THREE TERRITORIES ~120 DEGREES APART round the pocket (up-left, up-right, down-left), no two of them opposite:
            // two opposite territories drew a band through the pocket, and on a robe a band reads as a garment whatever
            // its angle ("a sash" shoulder to hip, "a belt" level, "a seam" upright); the region measures 1.3 : 1
            var sx = ex + 40f + R() * 8f;
            var sy = ey - 26f + R() * 8f;
            a.Seats.Add((new Vector2(sx, sy), 36f));
            for (var k = 1; k <= 2; k++)
                puffs.Add((ex + (sx - ex) * k / 3f, ey + (sy - ey) * k / 3f + (R() - 0.5f) * 6f, 20f + 8f * k, R() * MathF.PI, 3f + R() * 2.5f, 0.22f));
            for (var k = 0; k < 3; k++)
                puffs.Add((sx + (R() - 0.5f) * 16f, sy + (R() - 0.5f) * 12f, 34f + R() * 8f, R() * MathF.PI, 4.5f + R() * 4f, 0.5f));
            var sideFans = new (float Ang, float Len)[] { (0.35f, 0.13f), (-0.55f, 0.08f), (1.3f, 0.09f) };
            for (var k = 0; k < sideFans.Length; k++)
            {
                var ang = sideFans[k].Ang + (R() - 0.5f) * 0.3f;
                var born = 38f + k * 5f + R() * 4f;
                tips.Enqueue(new Tip(sx + MathF.Cos(ang) * 8f, sy + MathF.Sin(ang) * 6f, ang, 4.2f - k * 0.6f + R() * 0.5f,
                                     born, born + N * sideFans[k].Len * (0.85f + R() * 0.3f), -1, k % 2 == 0 ? 1 : -1, 0, k != 1));
            }
            foreach (var v in veinlets) tips.Enqueue(v);
            var kindOf = new List<int>();
            while (tips.Count > 0)
            {
                var tip = tips.Dequeue();
                var (x, y, ang) = (tip.X, tip.Y, tip.Ang);
                var id = nextId++;
                kindOf.Add(tip.Kind);
                var len = tip.Len;
                var turn = 0f;
                var gap = 0;
                var steps = 0;
                var heading = ang;
                var noiseSeed = R() * 100f;
                var gapsLeft = tip.Kind == 1 ? 2 : 0;
                var gapAt = 0.28f + R() * 0.1f;
                // THE LIGHT'S RHYTHM on this vein: runs and dark stretches of irregular length
                var inRun = tip.Lit;
                var runLen = inRun ? 8 + rng.Next(8) : 0;
                var runLeft = inRun ? runLen : 3 + rng.Next(14);
                var runPeak = inRun ? 0.6f + R() * 0.3f : 0f;
                var runGroup = rng.Next(IdleGroups);
                var dim = 0f;
                // the STRUCTURE carries the light (the paths, the territories' forks and roots): on a black body only the
                // violet shows the corruption's reach, so its runs are fuller than a twig's or a veinlet's
                var structural = tip.Kind is 1 or 3 || tip.Parent == -1;
                while (len < tip.MaxLen)
                {
                    // it MEANDERS round its own heading and never curls back (a curling vein drew hooks, a '2', a loop);
                    // a path's heading itself drifts, so it runs crooked, never ruled
                    if (tip.Kind == 1) heading += (R() - 0.5f) * 0.13f;   // (a smooth long line read as "a strap", "a sash")
                    turn = turn * 0.86f + (R() - 0.5f) * 0.11f;
                    ang += turn + (heading - ang) * 0.07f;
                    x += MathF.Cos(ang) * 1.2f;
                    y += MathF.Sin(ang) * 1.2f;
                    len += 1.2f;
                    steps++;
                    if (MathF.Sqrt(Sq(x - c) + Sq(y - c)) > maxR) break;
                    var life = 1f - (len - tip.Len) / Math.Max(1f, tip.MaxLen - tip.Len);
                    var w = tip.W0 * (0.45f + 0.55f * life) * (0.7f + 0.6f * Noise1(len / 20f + noiseSeed));
                    // a vein that thins to a hairline DIES there (hairlines on cloth read as "threads", "a knot of yarn")
                    if (w < 1.25f && steps > 3) break;
                    // the light: a run rises fast, holds, and tapers long; then the vein is dark for a while
                    if (runLeft-- <= 0)
                    {
                        inRun = !inRun;
                        if (inRun)
                        {
                            var hot = R() < 0.18f;
                            runLen = hot ? 5 + rng.Next(5) : 6 + rng.Next(9);   // (long lit runs read as a cord)
                            runPeak = hot ? 0.9f + R() * 0.1f : structural ? 0.55f + R() * 0.35f : 0.45f + R() * 0.35f;
                            runGroup = rng.Next(IdleGroups);
                            runLeft = runLen;
                        }
                        else
                        {
                            runLeft = structural ? 3 + rng.Next(7) : 4 + rng.Next(10);
                            // half the dark stretches of a lit vein still carry a DIM violet: a bright run then sits inside
                            // dim light (dark, dim, a brighter run, dark), never a lone dash between dark gaps
                            dim = tip.Lit && R() < 0.5f ? 0.14f + R() * 0.1f : 0f;
                        }
                    }
                    var glow = inRun ? Math.Max(dim, runPeak * RunProfile(1f - (runLeft + 1f) / Math.Max(1, runLen)))
                                       * (0.85f + 0.15f * Noise1(len / 3f + noiseSeed))
                                     : dim * (0.7f + 0.3f * Noise1(len / 5f + noiseSeed));
                    // A PATH BREAKS ONCE: a gap only smoke bridges (the eye cannot follow one line through the body)
                    if (gapsLeft > 0 && len - tip.Len > gapAt * (tip.MaxLen - tip.Len))
                    {
                        gapsLeft--;
                        gapAt += 0.3f + R() * 0.1f;
                        heading += (R() < 0.5f ? -1f : 1f) * (0.25f + R() * 0.2f);   // and it resumes on a new bearing
                        gap = 6 + rng.Next(5);
                        puffs.Add((x + MathF.Cos(ang) * gap * 0.6f, y + MathF.Sin(ang) * gap * 0.6f, len, ang, 5f + R() * 3f, 0.3f));
                    }
                    if (gap > 0) { gap--; continue; }
                    // BROKEN: a thin vein now and then breaks for a few texels
                    if (w < 2.6f && steps > 10 && R() < 0.02f) { gap = 2 + rng.Next(5); continue; }
                    // NO MESH: a vein that would run into another ends BEFORE it touches (after it has left its own branch
                    // point): it looks a few texels ahead, since stopping on contact had already closed the cell
                    if (steps > 6 && WouldMeet(a, owner, kindOf, x, y, ang, w, id)) break;
                    Stamp(a, owner, x, y, ang, w, len, id, glow, runGroup, (Noise1(len / 6f + noiseSeed * 2f) * 2f - 1f) * 0.22f);
                    // BRANCHES: asymmetric (mostly to one side), forking FORWARD at a shallow angle; many die early; a
                    // branch leaves its fork with a tapered highlight, never a round bead on the fork
                    if (steps > 5 && w > 1.0f && R() < 0.06f)
                    {
                        var s = R() < 0.7f ? tip.Side : -tip.Side;
                        var childAng = ang + s * (0.35f + R() * 0.5f);
                        var childLen = (tip.MaxLen - len) * (R() < 0.65f ? 0.08f + R() * 0.2f : 0.3f + R() * 0.5f);
                        tips.Enqueue(new Tip(x, y, childAng, Math.Max(2.0f, w * (0.55f + R() * 0.2f)), len, len + childLen, id, s, 0, R() < 0.45f));
                        if (R() < 0.25f) puffs.Add((x, y, len, ang, 3.5f + R() * 3f, 0.3f));
                    }
                    // INFECTED PATCHES along the way, more of them far out (the deepest depth's)
                    if (tip.Kind != 2 && R() < (len > 0.45f * N ? 0.014 : 0.006))
                        puffs.Add((x, y, len, ang, 4f + R() * 4f, 0.35f));
                    // A PATH OPENS INTO A TERRITORY: forks fanning out (one sideways, across the body) and a cluster of
                    // infected patches; a branch's end breaks into two or three uneven forks
                    if (len + 1.2f >= tip.MaxLen && tip.Kind != 2 && tip.W0 > 2.2f)
                    {
                        if (tip.Kind == 1)
                        {
                            a.Seats.Add((new Vector2(x, y), len));
                            // (the sideways fan turns AWAY from where the path came from: turned back, it closed a cell
                            // with the path and the patches)
                            // (two, uneven: three or more fanned out like fingers, "a hand", "a crown")
                            var fans = new[] { -0.45f, 1.0f };
                            foreach (var f in fans)
                                tips.Enqueue(new Tip(x, y, ang + f + (R() - 0.5f) * 0.3f, tip.W0 * 0.6f, len,
                                                     len + N * (0.1f + R() * 0.1f), id, f < 0 ? -1 : 1, 3, R() < 0.6f));
                            for (var k = 0; k < 3; k++)
                                puffs.Add((x + (R() - 0.5f) * 22f, y + (R() - 0.5f) * 22f, len + 4f + R() * 18f, R() * MathF.PI, 5f + R() * 4f, 0.45f));
                        }
                        else
                        {
                            var forks = 2 + rng.Next(2);
                            for (var k = 0; k < forks; k++)
                            {
                                var fa = ang + (k - (forks - 1) * 0.5f) * (0.45f + R() * 0.35f);
                                var fl = (tip.MaxLen - tip.Len) * (0.3f + R() * 0.35f);
                                tips.Enqueue(new Tip(x, y, fa, Math.Max(2.0f, tip.W0 * 0.6f), len, len + fl, id, k % 2 == 0 ? tip.Side : -tip.Side, 0, R() < 0.4f));
                            }
                        }
                    }
                }
            }
            a.Longest = 0f;
            for (var i = 0; i < n2; i++) if (!float.IsPositiveInfinity(a.Birth[i])) a.Longest = Math.Max(a.Longest, a.Birth[i]);
            // THE PATCHES: smoky, stretched along the vein they sit on, ragged at the edge; each leaks violet from inside
            foreach (var (x, y, birth, ang, size, leak) in puffs)
                for (var k = 0; k < 3; k++)
                    Puff(a, x + (R() - 0.5f) * size, y + (R() - 0.5f) * size * 0.7f, size * (0.8f + R() * 0.6f),
                         size * (0.45f + R() * 0.3f), ang + (R() - 0.5f) * 0.6f, 0.85f, leak, birth);
            return a;
        }

        /// <summary>Does another vein lie just ahead (its cover, a few texels on, beyond this one's own half width)? A
        /// veinlet of the pocket never stops a path (the paths grow first; a veinlet stops at a path instead).</summary>
        private static bool WouldMeet(CorruptionAtlas a, int[] owner, List<int> kindOf, float x, float y, float ang, float w, int id)
        {
            for (var d = 1f; d <= w * 0.5f + 4f; d += 1f)
                for (var side = -1; side <= 1; side++)
                {
                    var px = (int)(x + MathF.Cos(ang) * d - MathF.Sin(ang) * side * w * 0.4f);
                    var py = (int)(y + MathF.Sin(ang) * d + MathF.Cos(ang) * side * w * 0.4f);
                    if (px < 0 || py < 0 || px >= N || py >= N) continue;
                    var i = py * N + px;
                    var o = owner[i];
                    if (o >= 0 && o != id && kindOf[o] != 2 && a.Cover[i] > 0.3f) return true;
                }
            return false;
        }

        /// <summary>A run of light along a vein, 0..1 over its length: it rises fast, holds, and tapers long (never a dot).</summary>
        private static float RunProfile(float t)
        {
            if (t < 0.22f) { var u = t / 0.22f; return u * u * (3f - 2f * u); }
            if (t < 0.5f) return 1f;
            var v = 1f - (t - 0.5f) / 0.5f;
            return v <= 0f ? 0f : MathF.Pow(v, 1.4f);
        }

        private static void Stamp(CorruptionAtlas a, int[] owner, float x, float y, float ang, float w, float len, int id, float glow, int group, float lateral)
        {
            var rad = (int)MathF.Ceiling(w * 0.5f + 1f);
            // the light sits a little OFF the vein's centre, to one side and then the other: an irregular hot edge
            var nx = -MathF.Sin(ang) * lateral * w;
            var ny = MathF.Cos(ang) * lateral * w;
            for (var oy = -rad; oy <= rad; oy++)
                for (var ox = -rad; ox <= rad; ox++)
                {
                    int px = (int)x + ox, py = (int)y + oy;
                    if (px < 0 || py < 0 || px >= N || py >= N) continue;
                    var d = MathF.Sqrt(Sq(px + 0.5f - x) + Sq(py + 0.5f - y));
                    var cover = Math.Clamp(w * 0.5f + 0.5f - d, 0f, 1f);
                    if (cover <= 0f) continue;
                    var i = py * N + px;
                    if (cover > a.Cover[i]) a.Cover[i] = cover;
                    if (len < a.Birth[i]) a.Birth[i] = len;
                    if (owner[i] < 0) owner[i] = id;
                    if (glow <= 0f) continue;
                    var dc = MathF.Sqrt(Sq(px + 0.5f - x - nx) + Sq(py + 0.5f - y - ny));
                    var core = Math.Clamp(w * 0.28f + 0.5f - dc, 0f, 1f) * cover;
                    if (glow * core > a.Glow[i]) { a.Glow[i] = glow * core; a.Group[i] = group; }
                }
        }

        /// <summary>A smoky puff: an ellipse along <paramref name="ang"/>, its edge ragged by noise; dark, and leaking violet.</summary>
        private static void Puff(CorruptionAtlas a, float x, float y, float rMajor, float rMinor, float ang, float value, float leak, float birth)
        {
            var rad = (int)MathF.Ceiling(rMajor + 2f);
            var ca = MathF.Cos(ang);
            var sa = MathF.Sin(ang);
            for (var oy = -rad; oy <= rad; oy++)
                for (var ox = -rad; ox <= rad; ox++)
                {
                    int px = (int)x + ox, py = (int)y + oy;
                    if (px < 0 || py < 0 || px >= N || py >= N) continue;
                    var dx = px + 0.5f - x;
                    var dy = py + 0.5f - y;
                    var u = (dx * ca + dy * sa) / rMajor;
                    var v = (-dx * sa + dy * ca) / rMinor;
                    var d = MathF.Sqrt(u * u + v * v) * (0.8f + 0.45f * Noise2(px / 4f, py / 4f));
                    if (d >= 1f) continue;
                    var f = Sq(1f - d * d);
                    var i = py * N + px;
                    if (value * f > a.Blot[i]) a.Blot[i] = value * f;
                    if (birth < a.BlotBirth[i]) a.BlotBirth[i] = birth;
                    // the leak comes through the patch's smoke from inside (a lit border drew "a rosette", "a brooch")
                    if (leak * f > a.Leak[i]) a.Leak[i] = leak * f;
                }
        }

        /// <summary>A mottling of smoke (0..1): where a patch's inner light comes through, and where its dark is thicker.</summary>
        private static float Smoke(int i)
        {
            int x = i % N, y = i / N;
            var n = 0.65f * Noise2(x / 5f, y / 5f) + 0.35f * Noise2(x / 2.3f + 40f, y / 2.3f + 17f);
            return Math.Clamp((n - 0.32f) / 0.42f, 0f, 1f);
        }

        /// <summary>The dark alpha of everything born within <paramref name="lo"/>..<paramref name="hi"/> (path length).</summary>
        internal float[] DarkField(float lo, float hi)
        {
            var line = new float[N * N];
            for (var i = 0; i < line.Length; i++)
            {
                var v = Birth[i] > lo && Birth[i] <= hi ? Cover[i] : 0f;
                var bl = BlotBirth[i] > lo && BlotBirth[i] <= hi ? Blot[i] * (0.7f + 0.3f * Smoke(i)) : 0f;
                line[i] = Math.Max(v, bl);
            }
            // a halo of infected tissue round every vein (the broad bruise is its own layer, TissueField)
            var halo = Blur(Blur(line, 4), 4);
            var a = new float[N * N];
            for (var i = 0; i < a.Length; i++) a[i] = Math.Max(line[i], Math.Min(1f, halo[i] * 1.4f) * 0.42f);
            return a;
        }

        /// <summary>INFECTED TISSUE: a broad, mottled bruise under everything born within reach, soft and ragged at the
        /// edge; the veins sit IN discoloured flesh or cloth, never on top of it.</summary>
        internal float[] TissueField(float hi)
        {
            var line = new float[N * N];
            for (var i = 0; i < line.Length; i++)
            {
                var v = Birth[i] <= hi ? Cover[i] : 0f;
                var bl = BlotBirth[i] <= hi ? Blot[i] : 0f;
                line[i] = Math.Max(v, bl);
            }
            var bruise = Blur(Blur(Blur(line, 8), 8), 8);
            var a = new float[N * N];
            for (var i = 0; i < a.Length; i++) a[i] = Math.Min(1f, bruise[i] * 3.4f) * 0.75f * (0.45f + 0.8f * Smoke(i));
            return a;
        }

        /// <summary>The emission of what is born within reach (a group only, or all when <paramref name="group"/> is -1).</summary>
        internal float[] EmitField(float lo, float hi, int group)
        {
            var e = new float[N * N];
            for (var i = 0; i < e.Length; i++)
            {
                if (Birth[i] > lo && Birth[i] <= hi && (group < 0 || Group[i] == group)) e[i] = Glow[i];
                // an infected patch leaks violet from inside, mottled by its smoke (never a round lit disc)
                if (group < 0 && BlotBirth[i] > lo && BlotBirth[i] <= hi) e[i] = Math.Max(e[i], Leak[i] * Smoke(i));
            }
            var near = Blur(e, 1);
            var bloom = Blur(Blur(e, 3), 3);
            for (var i = 0; i < e.Length; i++) e[i] = Math.Min(1f, Math.Max(e[i], near[i] * 0.85f) + bloom[i] * 0.6f);
            return e;
        }

        /// <summary>The growing front: the veins just born at <paramref name="at"/> (a soft band behind it), lit along their
        /// cores, tapering behind the tip (a streak through the new paths, never a lit tube).</summary>
        internal float[] FrontField(float lo, float at, float band)
        {
            var e = new float[N * N];
            for (var i = 0; i < e.Length; i++)
            {
                var b = Birth[i];
                if (b <= lo || b > at || b < at - band) continue;
                var k = 1f - (at - b) / band;
                e[i] = Cover[i] * Cover[i] * (0.25f + 0.75f * k * k);
            }
            var bloom = Blur(Blur(e, 3), 3);
            for (var i = 0; i < e.Length; i++) e[i] = Math.Min(1f, e[i] + bloom[i] * 0.55f);
            return e;
        }

        private void Upload(GraphicsDevice d)
        {
            float R(int k) => Reach[k] * Longest;
            Dark = new Texture2D[3];
            Emit = new Texture2D[3];
            Idle = new Texture2D[3 * IdleGroups];
            Grow = new Texture2D[3 * Frames];
            Front = new Texture2D[3 * Frames];
            Tissue = new Texture2D[3];
            for (var k = 1; k <= 3; k++)
            {
                Dark[k - 1] = Mask(d, DarkField(-1f, R(k)));
                Tissue[k - 1] = Mask(d, TissueField(R(k)));
                Emit[k - 1] = Mask(d, EmitField(-1f, R(k), -1));
                for (var g = 0; g < IdleGroups; g++) Idle[(k - 1) * IdleGroups + g] = Mask(d, EmitField(-1f, R(k), g));
                var lo = R(k - 1);
                var hi = R(k);
                for (var f = 0; f < Frames; f++)
                {
                    var at = lo + (hi - lo) * (f + 1) / Frames;
                    Grow[(k - 1) * Frames + f] = Mask(d, DarkField(lo < 0.5f ? -1f : lo, at));
                    Front[(k - 1) * Frames + f] = Mask(d, FrontField(lo < 0.5f ? -1f : lo, at, Math.Max(10f, (hi - lo) * 0.3f)));
                }
            }
        }

        private static Texture2D Mask(GraphicsDevice d, float[] a)
        {
            var data = new Color[a.Length];
            for (var i = 0; i < data.Length; i++)
            {
                var v = (byte)Math.Clamp((int)MathF.Round(a[i] * 255f), 0, 255);
                data[i] = new Color(v, v, v, v);
            }
            var tex = new Texture2D(d, N, N);
            tex.SetData(data);
            return tex;
        }

        private static float[] Blur(float[] src, int r)
        {
            var tmp = new float[src.Length];
            var dst = new float[src.Length];
            for (var y = 0; y < N; y++)
            {
                float acc = 0f;
                for (var x = -r; x <= r; x++) acc += src[y * N + Math.Clamp(x, 0, N - 1)];
                for (var x = 0; x < N; x++)
                {
                    tmp[y * N + x] = acc / (2 * r + 1);
                    acc += src[y * N + Math.Min(N - 1, x + r + 1)] - src[y * N + Math.Max(0, x - r)];
                }
            }
            for (var x = 0; x < N; x++)
            {
                float acc = 0f;
                for (var y = -r; y <= r; y++) acc += tmp[Math.Clamp(y, 0, N - 1) * N + x];
                for (var y = 0; y < N; y++)
                {
                    dst[y * N + x] = acc / (2 * r + 1);
                    acc += tmp[Math.Min(N - 1, y + r + 1) * N + x] - tmp[Math.Max(0, y - r) * N + x];
                }
            }
            return dst;
        }

        private static float Noise2(float x, float y)
        {
            int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            var h00 = Hash(xi * 73856093 ^ yi * 19349663);
            var h10 = Hash((xi + 1) * 73856093 ^ yi * 19349663);
            var h01 = Hash(xi * 73856093 ^ (yi + 1) * 19349663);
            var h11 = Hash((xi + 1) * 73856093 ^ (yi + 1) * 19349663);
            var top = h00 + (h10 - h00) * fx;
            var bottom = h01 + (h11 - h01) * fx;
            return top + (bottom - top) * fy;
        }

        private static float Noise1(float x)
        {
            var i = (int)MathF.Floor(x);
            var f = x - i;
            f = f * f * (3f - 2f * f);
            return Hash(i * 7919) + (Hash((i + 1) * 7919) - Hash(i * 7919)) * f;
        }

        private static float Sq(float x) => x * x;
    }
}
