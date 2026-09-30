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
        _veinFlip = slot % 2 == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        _veinTurn = body.Width > body.Height * 1.05f ? -MathF.PI * 0.5f : 0f;
    }

    private Rectangle VeinRect(Vector2 seat, Rectangle body)
    {
        // SIZED BY THE WHOLE BODY, never stretched to its width (stretched, a trunk crossing a narrow waist became a band
        // from side to side: "a belt", "a sash tied round the waist"); capped so a boss's curse stays a curse
        var size = Math.Clamp(MathF.Sqrt(body.Width * (float)body.Height) * 1.4f, 240f, 440f);
        return new Rectangle((int)(seat.X - size / 2), (int)(seat.Y - size / 2), (int)size, (int)size);
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

    private void DrawCorruptionGlow(SpriteBatch b, int slot, float playheadMs, float t, Rectangle body, Vector2 seat, RasterizerState raster)
    {
        var atlas = Corruption(b.GraphicsDevice);
        Orient(slot, body);
        var stage = _stage[slot];
        var kind = _flareKind[slot];
        var age = _flareAge[slot];
        var rect = VeinRect(seat, body);
        var inside = _inside[slot];
        var quiet = kind == 0 ? 1f : Mark.TickQuietNear(playheadMs - age, 450f) ? QuietShare : 1f;

        // ── THE DARK BODY ──
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, inside, raster);
        if (_kind[slot] == 1)
        {
            Fill(b, body, Color.Black * 0.05f);   // waiting: the curse is on its way, a faint shadow only
            b.End();
            return;
        }
        Fill(b, body, Color.Black * (0.03f + 0.02f * stage));
        float grown;   // 0..1: how far the growth this flare runs has got (1: settled)
        if (kind is 1 or 2)
        {
            grown = Smooth(Math.Min(1f, age / GrowMs));
            DarkGrowth(b, atlas, rect, 0f, stage, grown * stage);
        }
        else if (kind == 3 && stage > 1)
        {
            grown = Smooth(Math.Min(1f, age / DeepenGrowMs));
            V(b, atlas.Dark[stage - 2], rect, VeinDark * 0.9f);
            DarkGrowth(b, atlas, rect, stage - 1, stage, stage - 1 + grown);
        }
        else
        {
            grown = 1f;
            V(b, atlas.Dark[stage - 1], rect, VeinDark * 0.9f);
        }
        b.End();

        // ── THE LIGHT: additive, clipped to the body ──
        b.Begin(SpriteSortMode.Deferred, VfxBlend.PremultipliedAdditive, SamplerState.LinearClamp, inside, raster);
        var breath = 1f + 0.12f * MathF.Sin(t * MathF.Tau / 5200f);
        var settleAge = kind == 1 || kind == 2 ? age - GrowMs : kind == 3 ? age - DeepenGrowMs : float.PositiveInfinity;
        var settle = settleAge < 0f ? 0f : settleAge >= SettleMs ? 1f : settleAge / SettleMs;
        var afterglow = settleAge >= 0f && settleAge < SettleMs ? 0.5f * (1f - settle) * quiet : 0f;
        var source = SourceGlow(slot, playheadMs);
        var level = IdleEmission * breath * (1f + afterglow + 0.9f * source);
        if (kind is 1 or 2)
        {
            // the settled light fades in behind the growth
            V(b, atlas.Emit[stage - 1], rect, Emission * (level * Smooth(grown) * (settleAge < 0f ? 0.35f : 0.35f + 0.65f * settle)));
            // an ARRIVAL (a transfer, a hop) wakes quieter than the apply: it grows through every depth it carries at once,
            // and at full strength it was the loudest thing the curse ever drew
            Front(b, atlas, rect, 0f, stage, grown * stage, FrontPeak * quiet * (kind == 2 ? ArrivalShare : 1f));
        }
        else if (kind == 3 && stage > 1)
        {
            // the old light stays as it was; only the NEW paths light as they form
            V(b, atlas.Emit[stage - 2], rect, Emission * (level * (1f - (settleAge < 0f ? 0f : settle))));
            if (settleAge >= 0f) V(b, atlas.Emit[stage - 1], rect, Emission * (level * settle));
            Front(b, atlas, rect, stage - 1, stage, stage - 1 + grown, FrontPeak * quiet);
        }
        else
            V(b, atlas.Emit[stage - 1], rect, Emission * level);
        // ONE SECTION AT A TIME gently answers (a 4.2 s cycle per section, each slot at its own moment)
        var cycle = 4200f;
        var n = (int)MathF.Floor(t / cycle);
        var ph = (t - n * cycle) / cycle;
        var answer = MathF.Sin(ph * MathF.PI);
        V(b, atlas.Idle[(stage - 1) * CorruptionAtlas.IdleGroups + ((n % CorruptionAtlas.IdleGroups) + CorruptionAtlas.IdleGroups) % CorruptionAtlas.IdleGroups],
             rect, Emission * (0.30f * answer * answer * (kind == 0 ? 1f : settle)));
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
            V(b, atlas.Grow[tr * CorruptionAtlas.Frames + Math.Clamp(f, 0, CorruptionAtlas.Frames - 1)], rect, VeinDark * 0.9f);
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
    private void DrawCorruptionLeaving(SpriteBatch b, int slot, int stage, float age, Rectangle rect, Rectangle body, DepthStencilState inside, RasterizerState raster)
    {
        Orient(slot, body);
        var atlas = Corruption(b.GraphicsDevice);
        const float flash = 150f, collapse = 600f;
        var p = age < flash ? 0f : Math.Min(1f, (age - flash) / collapse);
        var at = stage * (1f - Smooth(p));
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, inside, raster);
        DarkGrowth(b, atlas, rect, 0f, stage, at);
        b.End();
        b.Begin(SpriteSortMode.Deferred, VfxBlend.PremultipliedAdditive, SamplerState.LinearClamp, inside, raster);
        if (age < flash)
            V(b, atlas.Emit[stage - 1], rect, Emission * (IdleEmission * (1f + 0.8f * MathF.Sin(age / flash * MathF.PI))));
        else if (at > 0.02f)
            Front(b, atlas, rect, 0f, stage, at, FrontPeak * 0.8f * (1f - p));
        b.End();
    }

    private void DrawCorruptionLoose(SpriteBatch b, int slot, float t, Rectangle body, Vector2 seat)
    {
        // THE LEAKS: a wisp of shadow per depth at rest, faint; a few more as it grows
        var stage = _stage[slot];
        var f = _flare[slot];
        var count = stage + (f > 0.05f ? 4 : 0);
        for (var i = 0; i < count; i++)
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
        private const int SeatVeinlets = 5;

        private static CorruptionAtlas? _built;
        private static GraphicsDevice? _device;

        public Texture2D[] Dark = null!, Emit = null!, Idle = null!, Grow = null!, Front = null!;

        /// <summary>The corruption's path reach at each depth, as a share of the longest path (depth 0 = nothing).</summary>
        public static readonly float[] Reach = { 0f, 0.24f, 0.5f, 0.86f };

        // the grown field, texel by texel (kept for the tests and the measures)
        internal float[] Cover = null!, Birth = null!, Glow = null!, Blot = null!, BlotBirth = null!;
        internal int[] Group = null!;
        internal float Longest;

        public static CorruptionAtlas For(GraphicsDevice device)
        {
            if (_built is not null && _device == device) return _built;
            _device = device;
            var a = Grow_(20261001);
            a.Upload(device);
            _built = a;
            return _built;
        }

        /// <summary>Grows the field (no device: the tests read it).</summary>
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
            a.Group = new int[n2];
            var owner = new int[n2];
            Array.Fill(owner, -1);
            var rng = new Random(seed);
            var c = N / 2f;
            var maxR = N * 0.48f;
            var tips = new Queue<(float X, float Y, float Ang, float W0, float Len, float MaxLen, int Parent, int Side)>();
            var nextId = 0;
            var nodes = new List<(float X, float Y, float Birth, bool Lit, bool Blotch)>();
            // THE SEAT is an irregular smoky STAIN with short fuzzy veinlets bleeding out of it at uneven angles: depth 1 is
            // that stain (two strokes from one point always drew a letter: a star, a pinwheel, a cross, a 'V')
            for (var k = 0; k < 7; k++)
                nodes.Add((c + (float)(rng.NextDouble() - 0.5) * 26f, c + (float)(rng.NextDouble() - 0.5) * 18f, 0f, false, true));
            for (var k = 0; k < SeatVeinlets; k++)
            {
                var ang = (float)(rng.NextDouble() * MathF.Tau);
                var from = 5f + (float)rng.NextDouble() * 7f;
                var born = (float)rng.NextDouble() * 10f;
                tips.Enqueue((c + MathF.Cos(ang) * from, c + MathF.Sin(ang) * from * 0.75f, ang, 2.0f + (float)rng.NextDouble() * 1.2f,
                              born, born + 8f + (float)rng.NextDouble() * 22f, -2, rng.NextDouble() < 0.5 ? -1 : 1));
            }
            // A FEW ASYMMETRIC ROOTS: the curse entered at the seat and spread unevenly (never a wheel of evenly spaced spokes)
            // the trunks run UP toward the chest and DOWN toward the limbs, bent, never across: a trunk crossing the waist
            // (where the seat sits) drew a sash round it: "a thorny belt", "a cord tied round the waist"
            var baseAng = MathF.PI * 1.5f + (float)(rng.NextDouble() - 0.5) * 0.1f;   // (a tilt drew a strap across a tall body)
            // TWO TRUNKS, roughly opposite but bent, and a short stub: the curse runs THROUGH the body, never a star or a
            // pinwheel of arms round one centre (four arms at depth 1 read as a symbol)
            // (bent apart, never one straight bar through the seat; and no stub across it: the first glow pass read as a
            // cross / a dagger stuck in the body)
            var offsets = new[] { 0f, -2.7f };
            // (short trunks that break into forks: one long line across a body read as a cord tied round it)
            var lengths = new[] { 0.46f, 0.38f };
            var widths = new[] { 5.6f, 4.8f };
            for (var r = 0; r < offsets.Length; r++)
            {
                var ang = baseAng + offsets[r] + (float)(rng.NextDouble() - 0.5) * 0.4f;
                var w = widths[r] + (float)rng.NextDouble() * 0.8f;
                var maxLen = N * lengths[r] * (0.9f + (float)rng.NextDouble() * 0.2f);
                // each root leaves from its own point on the patch's edge, a little later than the last
                var start = 12f + (float)rng.NextDouble() * 5f;   // the trunks leave from the stain's edge, after it formed
                var born = 26f + r * 10f + (float)rng.NextDouble() * 8f;
                tips.Enqueue((c + MathF.Cos(ang) * start, c + MathF.Sin(ang) * start * 0.75f, ang, w, born, born + maxLen, -1, rng.NextDouble() < 0.5 ? -1 : 1));
            }
            while (tips.Count > 0)
            {
                var (x, y, ang, w0, len0, maxLen, parent, side) = tips.Dequeue();
                var id = nextId++;
                var len = len0;
                var turn = 0f;
                var gap = 0;
                var segLeft = 0;
                var segGlow = 0f;
                var segGroup = 0;
                var steps = 0;
                var heading = ang;
                var noiseSeed = (float)rng.NextDouble() * 100f;
                while (len < maxLen)
                {
                    // it MEANDERS round its own heading and never curls back (a curling vein drew hooks, a '2', a loop)
                    turn = turn * 0.86f + (float)(rng.NextDouble() - 0.5) * 0.11f;
                    ang += turn + (heading - ang) * 0.07f;
                    x += MathF.Cos(ang) * 1.2f;
                    y += MathF.Sin(ang) * 1.2f;
                    len += 1.2f;
                    steps++;
                    if (MathF.Sqrt(Sq(x - c) + Sq(y - c)) > maxR) break;
                    var life = 1f - (len - len0) / Math.Max(1f, maxLen - len0);
                    var w = w0 * (0.4f + 0.6f * life) * (0.5f + 1.0f * Noise1(len / 20f + noiseSeed));
                    if (segLeft-- <= 0)
                    {
                        // a new stretch: some catch the light, most stay dark
                        segLeft = 10 + rng.Next(30);
                        // the first stretch of a root always catches the light (depth 1 must read); later, half do
                        segGlow = steps < 4 && parent == -1 || rng.NextDouble() < 0.55 ? 0.45f + (float)rng.NextDouble() * 0.55f : 0f;
                        segGroup = rng.Next(IdleGroups);
                    }
                    // BROKEN: a thin vein now and then breaks for a few texels
                    if (gap > 0) { gap--; continue; }
                    if (w < 2.6f && steps > 10 && rng.NextDouble() < 0.02) { gap = 2 + rng.Next(5); continue; }
                    // NO MESH: running into another vein ends this one (after it has left its own branch point)
                    var cx = (int)x;
                    var cy = (int)y;
                    if (steps > 6 && cx >= 0 && cy >= 0 && cx < N && cy < N)
                    {
                        var o = owner[cy * N + cx];
                        // (the stain's own veinlets, the first ids, never stop a trunk leaving it)
                        // (after its first steps, not even its parent or a sibling fork: forks crossing closed a loop)
                        if (o >= SeatVeinlets && o != id && a.Cover[cy * N + cx] > 0.5f) break;
                    }
                    // the light wanders along a lit stretch: brighter knots, dim runs between them
                    Stamp(a, owner, x, y, w, len, id, segGlow * MathF.Max(0f, Noise1(len / 4.5f + noiseSeed * 3f) * 1.4f - 0.4f), segGroup);
                    // BRANCHES: asymmetric (mostly to one side), and many die early
                    if (steps > 5 && w > 1.0f && rng.NextDouble() < 0.075)
                    {
                        var s = rng.NextDouble() < 0.7 ? side : -side;
                        // forks FORWARD at a shallow angle, as veins do (a branch square to its parent read as a cross)
                        var childAng = ang + s * (0.35f + (float)rng.NextDouble() * 0.5f);
                        // most branches are short twigs that die early; a few carry the curse on
                        var childLen = (maxLen - len) * (rng.NextDouble() < 0.65 ? 0.08f + (float)rng.NextDouble() * 0.2f : 0.3f + (float)rng.NextDouble() * 0.6f);
                        tips.Enqueue((x, y, childAng, w * (0.45f + (float)rng.NextDouble() * 0.25f), len, len + childLen, id, s));
                        nodes.Add((x, y, len, rng.NextDouble() < 0.55, rng.NextDouble() < 0.3));
                    }
                    // INFECTED REGIONS: blotches of smoky contamination, more of them far out (the deepest depth's)
                    // (dense: the curse reads as stains spreading with veinlets between them, not as drawn strokes)
                    if (parent != -2 && rng.NextDouble() < (len > 0.45f * N ? 0.016 : 0.008))
                        nodes.Add((x, y, len, rng.NextDouble() < 0.35, true));
                    // A BUSHY END: a vein that has run its length breaks into two or three forks, uneven, instead of stopping
                    if (len + 1.2f >= maxLen && parent != -2 && w0 > 2.2f)
                    {
                        var forks = 2 + rng.Next(2);
                        for (var k = 0; k < forks; k++)
                        {
                            var fa = ang + (k - (forks - 1) * 0.5f) * (0.45f + (float)rng.NextDouble() * 0.35f);
                            var fl = (maxLen - len0) * (0.3f + (float)rng.NextDouble() * 0.35f);
                            tips.Enqueue((x, y, fa, w0 * 0.55f, len, len + fl, id, k % 2 == 0 ? side : -side));
                        }
                    }
                }
            }
            a.Longest = 0f;
            for (var i = 0; i < n2; i++) if (!float.IsPositiveInfinity(a.Birth[i])) a.Longest = Math.Max(a.Longest, a.Birth[i]);
            // THE NODES: a lit junction glows; a blotch is an irregular smoky patch
            foreach (var (x, y, birth, lit, blotch) in nodes)
            {
                if (lit) Disc(a.Glow, x, y, 3.2f, 1f, null, 0f);
                if (blotch)
                {
                    // smoky: a few overlapping soft puffs of uneven size, dense at the middle
                    var r = 5f + (float)rng.NextDouble() * 5f;
                    for (var k = 0; k < 6; k++)
                        Disc(a.Blot, x + (float)(rng.NextDouble() - 0.5) * r * 1.3f, y + (float)(rng.NextDouble() - 0.5) * r,
                             r * (0.35f + (float)rng.NextDouble() * 0.6f), 0.8f, a.BlotBirth, birth);
                }
            }
            return a;
        }

        private static void Stamp(CorruptionAtlas a, int[] owner, float x, float y, float w, float len, int id, float glow, int group)
        {
            var rad = (int)MathF.Ceiling(w * 0.5f + 1f);
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
                    // the light sits in the vein's core, never its whole width
                    var core = Math.Clamp(w * 0.36f + 0.5f - d, 0f, 1f);
                    if (glow * core > a.Glow[i]) { a.Glow[i] = glow * core; a.Group[i] = group; }
                }
        }

        private static void Disc(float[] field, float x, float y, float r, float value, float[]? birth, float b)
        {
            var rad = (int)MathF.Ceiling(r + 1f);
            for (var oy = -rad; oy <= rad; oy++)
                for (var ox = -rad; ox <= rad; ox++)
                {
                    int px = (int)x + ox, py = (int)y + oy;
                    if (px < 0 || py < 0 || px >= N || py >= N) continue;
                    var d = MathF.Sqrt(Sq(px + 0.5f - x) + Sq(py + 0.5f - y)) / r;
                    if (d >= 1f) continue;
                    var v = value * Sq(1f - d * d);
                    var i = py * N + px;
                    if (v > field[i]) field[i] = v;
                    if (birth is not null && b < birth[i]) birth[i] = b;
                }
        }

        /// <summary>The dark alpha of everything born within <paramref name="lo"/>..<paramref name="hi"/> (path length).</summary>
        internal float[] DarkField(float lo, float hi)
        {
            var line = new float[N * N];
            for (var i = 0; i < line.Length; i++)
            {
                var v = Birth[i] > lo && Birth[i] <= hi ? Cover[i] : 0f;
                var bl = BlotBirth[i] > lo && BlotBirth[i] <= hi ? Blot[i] : 0f;
                line[i] = Math.Max(v, bl);
            }
            var halo = Blur(Blur(line, 3), 3);
            var a = new float[N * N];
            for (var i = 0; i < a.Length; i++) a[i] = Math.Max(line[i], Math.Min(1f, halo[i] * 1.3f) * 0.3f);
            return a;
        }

        /// <summary>The emission of what is born within reach (a group only, or all when <paramref name="group"/> is -1).</summary>
        internal float[] EmitField(float lo, float hi, int group)
        {
            var e = new float[N * N];
            for (var i = 0; i < e.Length; i++)
            {
                if (Birth[i] > lo && Birth[i] <= hi && (group < 0 || Group[i] == group)) e[i] = Glow[i];
                // an infected patch smoulders faintly (on a black body the patches were dark on dark)
                if (group < 0 && BlotBirth[i] > lo && BlotBirth[i] <= hi) e[i] = Math.Max(e[i], Blot[i] * 0.36f);
            }
            var near = Blur(e, 1);
            var bloom = Blur(Blur(e, 4), 4);
            for (var i = 0; i < e.Length; i++) e[i] = Math.Min(1f, Math.Max(e[i], near[i] * 0.9f) + bloom[i] * 0.8f);
            return e;
        }

        /// <summary>The growing front: the veins just born at <paramref name="at"/> (a soft band behind it), lit.</summary>
        internal float[] FrontField(float lo, float at, float band)
        {
            var e = new float[N * N];
            for (var i = 0; i < e.Length; i++)
            {
                var b = Birth[i];
                if (b <= lo || b > at || b < at - band) continue;
                var k = 1f - (at - b) / band;
                e[i] = Cover[i] * (0.35f + 0.65f * k * k);
            }
            var bloom = Blur(Blur(e, 3), 3);
            for (var i = 0; i < e.Length; i++) e[i] = Math.Min(1f, e[i] + bloom[i] * 0.6f);
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
            for (var k = 1; k <= 3; k++)
            {
                Dark[k - 1] = Mask(d, DarkField(-1f, R(k)));
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
