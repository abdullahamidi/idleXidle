using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game.Presentation.Curse;

/// <summary>
/// BRAND's INFECTED TERRITORIES, grown once (ADR-013 §2): the approved prototype's procedural masks (same generator, same
/// seed <see cref="Seed"/>), pure and device-free, and their packing into ONE RGBA texture the curse shader samples.
/// Each variant: the infected <see cref="TissueF"/> (an irregular smoky region with satellite blotches, never a disc),
/// the softer, wider <see cref="DrainF"/> (where the host's material changes), the hard-edged <see cref="BurnF"/> (where
/// an Ash-Burn host burns to ash), the <see cref="DarkF"/> vein fragments and mottled darkening, the <see cref="EmitF"/>
/// light from inside, the Ash-Burn <see cref="FissureF"/> and burnt <see cref="EdgeF"/>, the idle accent's three groups,
/// and the bloom's <see cref="BirthF"/> order and its front.
/// </summary>
/// <remarks>
/// <para>
/// THE PACKED TEXTURE (<see cref="Pack"/>, <see cref="Upload"/>): a <see cref="Columns"/> x <see cref="Variants"/> grid of
/// <see cref="N"/>-texel cells, one row per variant, three PAGES per row: page 0 = (drain, burn, tissue, dark), page 1 =
/// (edge, emit, fissure, birth / <see cref="BirthScale"/>), page 2 = (idle group 0, 1, 2, front / <see cref="FrontScale"/>);
/// the fourth column's first cell holds the wisp puff (<see cref="PuffSource"/>, a white premultiplied image the
/// arena batch can draw). It is uploaded once at load (<c>SetData</c> only), never read back.
/// </para>
/// <para>
/// THE BLOOM IS ANALYTIC: the prototype's six stepped bloom frames multiplied each mask by
/// <see cref="RevealAt"/>(at, birth) at <c>at = (f + 1) / 6 x </c><see cref="BloomReach"/>; the shader evaluates the same
/// reveal at a continuous <c>at = reveal x BloomReach</c>, so the bloom grows smoothly instead of in ~80 ms steps.
/// THE FRONT's soft halo: the prototype added <c>Blur(front, 2) x 0.6</c> to each frame's front band. A band times a
/// source cannot be blurred in the shader, so the halo is baked into the source instead:
/// <c>source' = source + 0.6 x Blur(source, 2)</c>, and the shader multiplies the sharp band by it
/// (<c>min(1, band x source')</c>). The band is ~11 texels wide against the blur's 5, so the two agree closely; the
/// difference is pinned in <c>brand_curse_shader_test</c>.
/// </para>
/// </remarks>
internal sealed class CurseAtlas
{
    /// <summary>One cell's side, in texels.</summary>
    public const int N = 160;

    /// <summary>Region variants (each territory of a creature takes its own).</summary>
    public const int Variants = 4;

    /// <summary>The idle accent's section groups.</summary>
    public const int IdleGroups = 3;

    /// <summary>The prototype's generator seed (the approved territories).</summary>
    public const int Seed = 20261002;

    /// <summary>The share of the field's side a territory's region spans (its diameter is this x the side).</summary>
    public const float Fill = 0.68f;

    /// <summary>The bloom's end reaches this far in birth (every texel of the region is born by 1.2), so a whole bloom
    /// ends on the whole region and the settled masks follow without a pop.</summary>
    public const float BloomReach = 1.36f;

    /// <summary>A texel's birth is stored over this range (0..1.2).</summary>
    public const float BirthScale = 1.2f;

    /// <summary>The front source with its baked halo is stored over this range (source + 0.6 x its blur, at most 1.6).</summary>
    public const float FrontScale = 1.6f;

    /// <summary>The front band's half-width in birth.</summary>
    public const float FrontBand = 0.1f;

    /// <summary>The reveal ramp's width in birth.</summary>
    public const float RevealBand = 0.14f;

    /// <summary>Pages per variant in the packed texture.</summary>
    public const int Pages = 3;

    /// <summary>Cells across the packed texture (three pages and the puff's column).</summary>
    public const int Columns = 4;

    /// <summary>The packed texture's width, in texels.</summary>
    public const int Width = Columns * N;

    /// <summary>The packed texture's height, in texels.</summary>
    public const int Height = Variants * N;

    /// <summary>The wisp puff's side, in texels (the prototype's 48).</summary>
    public const int PuffSide = 48;

    /// <summary>The wisp puff in the packed texture (zero texels round it, so linear sampling never bleeds).</summary>
    public static readonly Rectangle PuffSource = new(Pages * N + 8, 8, PuffSide, PuffSide);

    /// <summary>A texel's reveal in a bloom whose front stands at <paramref name="at"/>.</summary>
    internal static float RevealAt(float at, float birth) => Math.Clamp((at - birth) / RevealBand, 0f, 1f);

    // the grown fields, texel by texel
    internal readonly float[][] TissueF = new float[Variants][], DrainF = new float[Variants][], DarkF = new float[Variants][],
                                VeinF = new float[Variants][], EmitF = new float[Variants][], RunF = new float[Variants][],
                                BirthF = new float[Variants][], EdgeF = new float[Variants][], BurnF = new float[Variants][],
                                FissureF = new float[Variants][];
    internal readonly int[][] GroupF = new int[Variants][];

    /// <summary>The idle accent's groups per variant: <c>[v * IdleGroups + g]</c>, the light of section g, softened.</summary>
    internal readonly float[][] IdleF = new float[Variants * IdleGroups][];

    /// <summary>The bloom front's SOURCE (what a front band lights where it passes), before its halo.</summary>
    internal readonly float[][] FrontSourceF = new float[Variants][];

    /// <summary>The front source with its halo baked in (source + 0.6 x Blur(source, 2)), 0..<see cref="FrontScale"/>.</summary>
    internal readonly float[][] FrontF = new float[Variants][];

    /// <summary>Each variant's vein fragments' lengths (texels).</summary>
    internal readonly List<float>[] Fragments = new List<float>[Variants];

    private static CurseAtlas? _grown;

    /// <summary>The approved atlas (<see cref="Seed"/>), grown once per process.</summary>
    internal static CurseAtlas Approved => _grown ??= Grow(Seed);

    /// <summary>Grows every variant (no device).</summary>
    internal static CurseAtlas Grow(int seed)
    {
        var a = new CurseAtlas();
        var rng = new Random(seed);
        for (var v = 0; v < Variants; v++) a.GrowVariant(v, rng);
        return a;
    }

    /// <summary>The bloom's front band through a texel born at <paramref name="birth"/>, the front standing at <paramref name="at"/>.</summary>
    internal static float FrontBandAt(float at, float birth) => Math.Max(0f, 1f - MathF.Abs(at - birth) / FrontBand);

    /// <summary>The packed texture's pixels (row-major, <see cref="Width"/> x <see cref="Height"/>).</summary>
    internal Color[] Pack()
    {
        var data = new Color[Width * Height];
        for (var v = 0; v < Variants; v++)
        {
            Cell(data, 0, v, DrainF[v], BurnF[v], TissueF[v], DarkF[v], 1f);
            Cell(data, 1, v, EdgeF[v], EmitF[v], FissureF[v], BirthF[v], 1f / BirthScale);
            Cell(data, 2, v, IdleF[v * IdleGroups], IdleF[v * IdleGroups + 1], IdleF[v * IdleGroups + 2], FrontF[v], 1f / FrontScale);
        }
        var puff = Puff();
        for (var y = 0; y < PuffSide; y++)
            for (var x = 0; x < PuffSide; x++)
            {
                var b = Byte(puff[y * PuffSide + x]);
                data[(PuffSource.Y + y) * Width + PuffSource.X + x] = new Color(b, b, b, b);
            }
        return data;
    }

    /// <summary>Uploads the packed texture (one <c>SetData</c>; load time only).</summary>
    internal Texture2D Upload(GraphicsDevice device)
    {
        var tex = new Texture2D(device, Width, Height, false, SurfaceFormat.Color);
        tex.SetData(Pack());
        return tex;
    }

    /// <summary>The atlas UV of variant <paramref name="v"/>'s page 0 cell origin.</summary>
    internal static Vector2 CellOrigin(int v) => new(0f, v * N / (float)Height);

    /// <summary>The prototype's wisp puff (48², <c>(1 - r²)²</c>), as a field.</summary>
    internal static float[] Puff()
    {
        const int P = PuffSide;
        var puff = new float[P * P];
        for (var y = 0; y < P; y++)
            for (var x = 0; x < P; x++)
            {
                var r = MathF.Sqrt(Sq((x + 0.5f) / P * 2f - 1f) + Sq((y + 0.5f) / P * 2f - 1f));
                puff[y * P + x] = r >= 1f ? 0f : Sq(1f - r * r);
            }
        return puff;
    }

    private static void Cell(Color[] data, int page, int v, float[] r, float[] g, float[] b, float[] a, float aScale)
    {
        for (var y = 0; y < N; y++)
            for (var x = 0; x < N; x++)
            {
                var i = y * N + x;
                data[(v * N + y) * Width + page * N + x] = new Color(Byte(r[i]), Byte(g[i]), Byte(b[i]), Byte(a[i] * aScale));
            }
    }

    /// <summary>A field value as a byte, exactly as the prototype's masks stored it.</summary>
    internal static byte Byte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);

    private void GrowVariant(int v, Random rng)
    {
        float R() => (float)rng.NextDouble();
        var n2 = N * N;
        var c = N / 2f;
        var rad = N * 0.27f;
        var tissue = new float[n2];
        // the infection's own centre, a little off the field's
        var ox = c + (R() - 0.5f) * rad * 0.3f;
        var oy = c + (R() - 0.5f) * rad * 0.3f;
        // LOBES: overlapping smoky puffs round it (an irregular region, never a disc, never a ring)
        var lobes = 5 + rng.Next(3);
        var spin = R() * MathF.Tau;
        for (var i = 0; i < lobes; i++)
        {
            var ang = spin + i * MathF.Tau / lobes + (R() - 0.5f) * 0.9f;
            var dist = i == 0 ? 0f : rad * (0.25f + R() * 0.35f);
            var maj = rad * (0.42f + R() * 0.25f);
            Puff(tissue, ox + MathF.Cos(ang) * dist, oy + MathF.Sin(ang) * dist * 0.85f, maj, maj * (0.6f + R() * 0.3f), R() * MathF.PI, 0.9f);
        }
        // SATELLITES: small blotches just off the edge (the edge breaks up: a clean outline read as "a patch", "a decal")
        var sats = 2 + rng.Next(2);
        for (var i = 0; i < sats; i++)
        {
            var ang = R() * MathF.Tau;
            var dist = rad * (0.88f + R() * 0.28f);
            var maj = rad * (0.15f + R() * 0.1f);
            Puff(tissue, ox + MathF.Cos(ang) * dist, oy + MathF.Sin(ang) * dist, maj, maj * (0.6f + R() * 0.3f), R() * MathF.PI, 0.75f);
        }
        // contaminated tissue, mottled (never a flat fill)
        for (var i = 0; i < n2; i++) tissue[i] *= 0.62f + 0.38f * Smoke(i);
        // where the HOST's material changes: softer and a little wider than the tissue
        var drainBlur = Blur(Blur(tissue, 4), 4);
        var drain = new float[n2];
        for (var i = 0; i < n2; i++) drain[i] = Math.Min(1f, Math.Max(tissue[i], drainBlur[i] * 1.45f));
        // THE BURN (Ash-Burn): where the host's material is burned to ash, HARD-EDGED and ragged (the soft drain,
        // burned, read as a fog laid over the body), mottled
        var burn = new float[n2];
        for (var i = 0; i < n2; i++)
        {
            var e = Math.Clamp((drain[i] - 0.3f) / 0.3f, 0f, 1f);
            burn[i] = e * e * (3f - 2f * e) * (0.82f + 0.18f * Smoke(i + 151));
        }
        // THE BURNT EDGE (Ash-Burn): a ragged near-black rim where the host's material gives way to the ash, ON the
        // burn's edge, broken in places, and burnt specks creeping into the ash (a clean ring read as "a patch", "a
        // badge"; smooth ash read as "a see-through hole")
        var edge = new float[n2];
        for (var i = 0; i < n2; i++)
        {
            int x = i % N, y = i / N;
            var band = 1f - MathF.Abs(drain[i] - 0.44f) / 0.13f;
            var rim = band <= 0f ? 0f : band * band * (3f - 2f * band) * (Noise2(x / 5.5f + 61f, y / 5.5f + 23f) > 0.36f ? 1f : 0.3f)
                                       * (0.7f + 0.3f * Noise2(x / 2.6f + 5f, y / 2.6f + 91f));
            var speck = tissue[i] > 0.45f ? Math.Clamp((Noise2(x / 3.2f + 17f, y / 3.2f + 47f) - 0.74f) / 0.1f, 0f, 1f) * 0.7f : 0f;
            edge[i] = Math.Max(rim, speck);
        }
        edge = Blur(edge, 1);
        // THE BLOOM's order: outward from the infection's centre, raggedly
        var birth = new float[n2];
        for (var i = 0; i < n2; i++)
        {
            int x = i % N, y = i / N;
            var dd = MathF.Sqrt(Sq(x + 0.5f - ox) + Sq(y + 0.5f - oy)) / (rad * 1.3f);
            birth[i] = Math.Clamp(dd + 0.28f * (Noise2(x / 7f, y / 7f) - 0.5f), 0f, 1.2f);
        }
        // VEIN FRAGMENTS: two to four, short (under half the region's radius), tapered, heading outward, ending
        // inside the region (a long line followed a collar or a seam: "a sash", "a stole", "embroidery")
        var veins = new float[n2];
        var runs = new float[n2];
        var group = new int[n2];
        Fragments[v] = new List<float>();
        var frags = 2 + rng.Next(3);
        for (var fI = 0; fI < frags; fI++)
        {
            var a0 = R() * MathF.Tau;
            var r0 = rad * (0.1f + R() * 0.3f);
            var x = ox + MathF.Cos(a0) * r0;
            var y = oy + MathF.Sin(a0) * r0 * 0.85f;
            // outward, or across the region (half of them), so a fragment ends inside it
            var ang = a0 + (R() < 0.5f ? 0f : (R() < 0.5f ? -1f : 1f) * MathF.PI * 0.5f) + (R() - 0.5f) * 0.7f;
            var length = rad * (0.3f + R() * 0.3f);
            var w0 = 2.8f + R() * 1.0f;
            var turn = 0f;
            var len = 0f;
            var runFrom = length * (0.1f + R() * 0.25f);
            var runLen = length * (0.45f + R() * 0.25f);
            var runPeak = 0.72f + R() * 0.28f;   // (the violet lives in the fissures now)
            var seed = R() * 100f;
            var gI = rng.Next(IdleGroups);
            while (len < length)
            {
                turn = turn * 0.85f + (R() - 0.5f) * 0.16f;
                ang += turn;
                x += MathF.Cos(ang) * 1.1f;
                y += MathF.Sin(ang) * 1.1f;
                len += 1.1f;
                var ix = (int)x;
                var iy = (int)y;
                if (ix < 2 || iy < 2 || ix >= N - 2 || iy >= N - 2 || tissue[iy * N + ix] < 0.18f) break;
                var life = 1f - len / length;
                var w = w0 * (0.4f + 0.6f * life) * (0.75f + 0.5f * Noise1(len / 9f + seed));
                if (w < 0.95f) break;
                var q = (len - runFrom) / runLen;
                var glow = q is > 0f and < 1f ? runPeak * RunProfile(q) : 0f;
                Stamp(veins, runs, group, x, y, ang, w, glow, gI, (Noise1(len / 5f + seed * 2f) * 2f - 1f) * 0.22f);
            }
            Fragments[v].Add(len);
        }
        // the dark: the fragments, and the tissue's own darker mottling
        // (smoky, soft: hard-edged spots read as a beast's own markings)
        var mottle = new float[n2];
        for (var i = 0; i < n2; i++) mottle[i] = tissue[i] * 0.42f * Math.Clamp((Smoke(i + 53) - 0.4f) / 0.4f, 0f, 1f);
        mottle = Blur(mottle, 2);
        var dark = new float[n2];
        for (var i = 0; i < n2; i++) dark[i] = Math.Max(veins[i], mottle[i]);
        var halo = Blur(Blur(veins, 2), 2);
        for (var i = 0; i < n2; i++) dark[i] = Math.Max(dark[i], Math.Min(1f, halo[i] * 1.2f) * 0.35f);
        // THE LIGHT FROM INSIDE: a soft mottled core, the fragments' runs, and a broken bright contaminated edge on
        // one side of the region (an edge lit all round drew "a patch", "a badge")
        var emit = new float[n2];
        var cx2 = ox + (R() - 0.5f) * rad * 0.3f;
        var cy2 = oy + (R() - 0.5f) * rad * 0.3f;
        var edgeAng = R() * MathF.Tau;
        for (var i = 0; i < n2; i++)
        {
            int x = i % N, y = i / N;
            // (the inner light follows the region's own ragged shape, mottled by its smoke: a round lit core read
            // as "a glowing gem", "an orb")
            var dd = MathF.Sqrt(Sq(x + 0.5f - cx2) + Sq(y + 0.5f - cy2)) / (rad * 0.95f) * (0.8f + 0.4f * Noise2(x / 6f + 31f, y / 6f + 7f));
            var inner = dd < 1f ? 1f - dd : 0f;
            var core = tissue[i] * inner * (0.15f + 0.85f * Sq(Smoke(i + 97))) * 0.18f;
            var rim = 0f;
            var tI = tissue[i];
            if (tI > 0.16f && tI < 0.62f)
            {
                var a = MathF.Atan2(y + 0.5f - oy, x + 0.5f - ox);
                var da = MathF.Abs(MathF.IEEERemainder(a - edgeAng, MathF.Tau));
                if (da < 0.95f)
                    rim = 0.66f * (1f - MathF.Abs(tI - 0.38f) / 0.24f) * (1f - da / 0.95f) * (Noise2(x / 3.5f + 11f, y / 3.5f) > 0.45f ? 1f : 0.25f);
            }
            emit[i] = Math.Max(core, Math.Max(runs[i], Math.Max(0f, rim)));
            if (runs[i] <= 0f)
            {
                var a = MathF.Atan2(y + 0.5f - oy, x + 0.5f - ox);
                group[i] = (int)((a + MathF.PI) / MathF.Tau * IdleGroups + v) % IdleGroups;
            }
        }
        var near = Blur(emit, 1);
        var bloom = Blur(Blur(emit, 3), 3);
        for (var i = 0; i < n2; i++) emit[i] = Math.Min(1f, Math.Max(emit[i], near[i] * 0.85f) + bloom[i] * 0.5f) * Math.Min(1f, drain[i] * 2f);
        TissueF[v] = tissue;
        DrainF[v] = drain;
        DarkF[v] = dark;
        VeinF[v] = veins;
        EmitF[v] = emit;
        RunF[v] = runs;
        BirthF[v] = birth;
        GroupF[v] = group;
        EdgeF[v] = edge;
        BurnF[v] = burn;
        // THE FISSURES' OWN LIGHT (Ash-Burn): the runs and the broken bright edge only, crisp, a thin leak round them
        // (the soft core and bloom, pale, read as a white fog)
        var fissure = new float[n2];
        var leak = Blur(runs, 1);
        for (var i = 0; i < n2; i++)
        {
            int x = i % N, y = i / N;
            var rimI = 0f;
            var tI = tissue[i];
            if (tI > 0.16f && tI < 0.62f)
            {
                var a = MathF.Atan2(y + 0.5f - oy, x + 0.5f - ox);
                var da = MathF.Abs(MathF.IEEERemainder(a - edgeAng, MathF.Tau));
                if (da < 0.95f)
                    rimI = 0.5f * (1f - MathF.Abs(tI - 0.38f) / 0.24f) * (1f - da / 0.95f) * (Noise2(x / 3.5f + 11f, y / 3.5f) > 0.45f ? 1f : 0.2f);
            }
            fissure[i] = Math.Min(1f, Math.Max(runs[i], Math.Max(rimI, leak[i] * 0.45f))) * Math.Min(1f, burn[i] * 1.5f);
        }
        FissureF[v] = fissure;
        // THE IDLE ACCENT's sections (the prototype's Idle masks): the light of each group, softened by a texel
        for (var g = 0; g < IdleGroups; g++)
        {
            var e = new float[n2];
            for (var i = 0; i < n2; i++) if (group[i] == g) e[i] = emit[i];
            IdleF[v * IdleGroups + g] = Blur(e, 1);
        }
        // THE BLOOM FRONT's source (the prototype's FrontGrow before its band), and its halo baked in
        var front = new float[n2];
        for (var i = 0; i < n2; i++)
            front[i] = tissue[i] > 0.1f ? (0.35f + 0.65f * Math.Max(veins[i], Smoke(i))) * Math.Min(1f, tissue[i] * 2f) : 0f;
        FrontSourceF[v] = front;
        var soft = Blur(front, 2);
        var withHalo = new float[n2];
        for (var i = 0; i < n2; i++) withHalo[i] = Math.Min(FrontScale, front[i] + soft[i] * 0.6f);
        FrontF[v] = withHalo;
    }

    private static float RunProfile(float t)
    {
        if (t < 0.22f) { var u = t / 0.22f; return u * u * (3f - 2f * u); }
        if (t < 0.5f) return 1f;
        var v = 1f - (t - 0.5f) / 0.5f;
        return v <= 0f ? 0f : MathF.Pow(v, 1.4f);
    }

    private static void Stamp(float[] cover, float[] glowF, int[] groupF, float x, float y, float ang, float w, float glow, int group, float lateral)
    {
        var rad = (int)MathF.Ceiling(w * 0.5f + 1f);
        var nx = -MathF.Sin(ang) * lateral * w;
        var ny = MathF.Cos(ang) * lateral * w;
        for (var oy = -rad; oy <= rad; oy++)
            for (var ox = -rad; ox <= rad; ox++)
            {
                int px = (int)x + ox, py = (int)y + oy;
                if (px < 0 || py < 0 || px >= N || py >= N) continue;
                var d = MathF.Sqrt(Sq(px + 0.5f - x) + Sq(py + 0.5f - y));
                var cov = Math.Clamp(w * 0.5f + 0.5f - d, 0f, 1f);
                if (cov <= 0f) continue;
                var i = py * N + px;
                if (cov > cover[i]) cover[i] = cov;
                if (glow <= 0f) continue;
                var dc = MathF.Sqrt(Sq(px + 0.5f - x - nx) + Sq(py + 0.5f - y - ny));
                var core = Math.Clamp(w * 0.28f + 0.5f - dc, 0f, 1f) * cov;
                if (glow * core > glowF[i]) { glowF[i] = glow * core; groupF[i] = group; }
            }
    }

    /// <summary>A smoky puff: an ellipse along <paramref name="ang"/>, its edge ragged by noise.</summary>
    private static void Puff(float[] field, float x, float y, float rMajor, float rMinor, float ang, float value)
    {
        var rad = (int)MathF.Ceiling(rMajor + 2f);
        var ca = MathF.Cos(ang);
        var sa = MathF.Sin(ang);
        for (var oy = -rad; oy <= rad; oy++)
            for (var ox = -rad; ox <= rad; ox++)
            {
                int px = (int)x + ox, py = (int)y + oy;
                if (px < 1 || py < 1 || px >= N - 1 || py >= N - 1) continue;
                var dx = px + 0.5f - x;
                var dy = py + 0.5f - y;
                var u = (dx * ca + dy * sa) / rMajor;
                var w = (-dx * sa + dy * ca) / rMinor;
                var d = MathF.Sqrt(u * u + w * w) * (0.8f + 0.45f * Noise2(px / 4f, py / 4f));
                if (d >= 1f) continue;
                // a solid infected interior, a soft ragged edge (squared falloff left only the puffs' centres solid)
                var e = Math.Clamp((1f - d) / 0.45f, 0f, 1f);
                var f = e * e * (3f - 2f * e) * value;
                var i = py * N + px;
                if (f > field[i]) field[i] = f;
            }
    }

    /// <summary>A mottling of smoke (0..1) at texel index <paramref name="i"/>.</summary>
    internal static float Smoke(int i)
    {
        int x = i % N, y = i / N;
        // (two octaves turned off the noise's grid: axis-aligned, the mottling read as "a woven", "a crosshatched"
        // pattern, a garment's)
        float u1 = x * 0.83f - y * 0.56f, v1 = x * 0.56f + y * 0.83f;
        float u2 = x * 0.47f + y * 0.88f, v2 = -x * 0.88f + y * 0.47f;
        var n = 0.65f * Noise2(u1 / 6.1f, v1 / 6.1f) + 0.35f * Noise2(u2 / 2.7f + 40f, v2 / 2.7f + 17f);
        return Math.Clamp((n - 0.32f) / 0.42f, 0f, 1f);
    }

    /// <summary>A box blur of radius <paramref name="r"/> over one N x N field (edges clamped).</summary>
    internal static float[] Blur(float[] src, int r)
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

    /// <summary>The prototype's integer hash, 0..1 (also picks each territory's variant turn and mirror).</summary>
    internal static float Hash(int n)
    {
        unchecked
        {
            var h = (uint)n * 747796405u + 2891336453u;
            h = ((h >> (int)((h >> 28) + 4u)) ^ h) * 277803737u;
            return ((h >> 22) ^ h) / (float)uint.MaxValue;
        }
    }

    private static float Sq(float x) => x * x;
}
