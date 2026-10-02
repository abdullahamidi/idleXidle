using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using IdleXIdle.Game.Presentation.Curse;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using static IdleXIdle.Game.Presentation.Curse.CurseHost;
using static IdleXIdle.Game.Presentation.Curse.CurseMaterial;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// BRAND's curse SHADER (ADR-013 §1, §2, §7): the pure C# mirror of <c>BrandCurse.fx</c> (<see cref="CurseMaterial.Composite"/>)
/// is pinned against the approved prototype's own composite (its CPU material functions and the SpriteBatch blends it
/// drew with), pass by pass, on dark violet, pale, black and colourful hosts; the packed atlas holds the prototype's
/// masks; the analytic bloom front stays close to the prototype's blurred one; the committed binary is built from the
/// current source.
/// </summary>
public class brand_curse_shader_test
{
    private static readonly HostLook DarkViolet = new(0.18f, 0.2f, 0.5f, 0.7f);
    private static readonly HostLook Pale = new(0.7f, 0.1f, 0f, 0.05f);
    private static readonly HostLook Black = new(0.06f, 0.03f, 0f, 0.9f);
    private static readonly HostLook Colourful = new(0.4f, 0.35f, 0.02f, 0.1f);
    private static readonly HostLook[] Looks = { DarkViolet, Pale, Black, Colourful };

    private static readonly Color[] Texels =
    {
        new(40, 30, 70), new(200, 190, 170), new(10, 10, 14), new(30, 160, 200), new(220, 60, 40), new(128, 128, 128),
    };

    private static readonly Color[] Tints = { Color.White, new(255, 214, 190) };

    // (the prototype's sprite colours were bytes: Color x float truncates, Color.Lerp rounds, once per sprite, and a
    // pixel under every pass takes a dozen of them; the mirror is float, so it may stand a few 255ths off)
    private const float Tolerance = 4.5f / 255f;

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln"))) dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    // ── THE PROTOTYPE'S COMPOSITE (39b59aaa), written out with its own Colors and SpriteBatch blends ───────────────────

    /// <summary>One territory's mask values at one pixel (the prototype's settled / grown masks there).</summary>
    private readonly record struct Masks(float Drain, float Burn, float Tissue, float Dark, float Edge, float Emit, float Fissure, float Idle, float Front);

    private static Vector3 Over(Vector3 d, Vector4 src) => new Vector3(src.X, src.Y, src.Z) + d * (1f - src.W);

    private static Vector3 Stain(Vector3 d, Color c, float m)
    {
        var s = c.ToVector4() * m;
        return d * (new Vector3(s.X, s.Y, s.Z) + new Vector3(1f - s.W));
    }

    private static Vector3 ScreenOn(Vector3 d, Color c, float m)
    {
        var s = c.ToVector4() * m;
        return d + new Vector3(s.X, s.Y, s.Z) * (Vector3.One - d);
    }

    private static Vector3 AddOn(Vector3 d, Color c, float m)
    {
        var s = c.ToVector4() * m;
        return Vector3.Clamp(d + new Vector3(s.X, s.Y, s.Z), Vector3.Zero, Vector3.One);
    }

    /// <summary>
    /// The prototype's passes over one opaque host pixel inside the stencil, a living territory at <paramref name="reveal"/>:
    /// the DualTextureEffect drain quads (DrainPixel copies x the unpremultiplied tint, alpha = strength), the Stain batch,
    /// the Screen batch, the fissures and the bloom (premultiplied additive). Every level is the prototype's own expression.
    /// </summary>
    private static Vector3 Prototype(Color host, Color tint, HostLook look, int stage, float burn, float reveal, float breath, float boost,
                                     float answer, float settle, float peak, Masks settled, float birth)
    {
        // a blooming territory's grown masks (each settled mask x its texel's reveal) and its front band
        var at = Math.Min(reveal, 1f) * CurseAtlas.BloomReach;
        var rev = CurseAtlas.RevealAt(at, birth);
        var m = settled with
        {
            Drain = settled.Drain * rev, Burn = settled.Burn * rev, Tissue = settled.Tissue * rev, Dark = settled.Dark * rev, Edge = settled.Edge * rev,
            Front = Math.Min(1f, CurseAtlas.FrontBandAt(at, birth) * settled.Front),
        };
        var tissueSettled = settled.Tissue;
        var hostBurn = AshBurn(look);
        var tu = tint.ToVector3();
        var d = host.ToVector3() * tu;
        var mat = new Mat(look, burn);
        // the drain quads (DrainTerritory + DrainQuad)
        var s = Math.Clamp(DrainStrength(look, burn) * Math.Min(1f, reveal * 3f), 0f, 1f);
        if (burn < 1f)
        {
            var a = Math.Min(1f, s * (1f - burn) / Math.Max(1e-3f, 1f - s * burn));
            var c = DrainPixel(host, look, 0f).ToVector4();
            d = Over(d, new Vector4(new Vector3(c.X, c.Y, c.Z) * tu * m.Drain * a, c.W * m.Drain * a));
        }
        if (burn > 0f)
        {
            var a = Math.Min(1f, s * burn);
            var c = DrainPixel(host, look, 1f).ToVector4();
            d = Over(d, new Vector4(new Vector3(c.X, c.Y, c.Z) * tu * m.Burn * a, c.W * m.Burn * a));
        }
        // the Stain batch
        d = Stain(d, AfflictedShade * ((0.08f + 0.04f * stage) * (1f - 0.7f * hostBurn)), 1f);
        var deep = 0.5f * PaleHost(look.Luma);
        d = Stain(d, mat.Bruise * mat.Tissue, m.Tissue);
        if (deep > 0f) d = Stain(d, mat.Bruise * (TissueShare * deep), m.Tissue);
        d = Stain(d, mat.Vein, m.Dark);
        if (mat.Burn > 0f) d = Stain(d, BurnEdge * (BurnEdgeShare * mat.Burn), m.Edge);
        // the Screen batch
        var lit = reveal * reveal;
        var level = IdleEmission * mat.Light * breath * boost * lit;
        if (mat.Glow > 0f) d = ScreenOn(d, TissueGlow * (mat.Glow * breath * lit), tissueSettled);
        var soft = 1f - mat.Burn;
        d = ScreenOn(d, mat.Emission * (Math.Min(1f, level) * soft), m.Emit);
        if (level > 1f) d = ScreenOn(d, mat.Emission * (Math.Min(1f, level - 1f) * soft), m.Emit);
        d = ScreenOn(d, mat.Accent * (0.3f * mat.Light * answer * answer * lit * settle), m.Idle);
        // the fissures and the bloom
        if (mat.Burn > 0f) d = AddOn(d, mat.Emission * Math.Min(1f, FissureLight * mat.Light * breath * boost * lit * mat.Burn), m.Fissure);
        if (reveal < 1f) d = AddOn(d, mat.Hot * (peak * Math.Min(1f, reveal * 5f) * (1f - 0.6f * reveal)), m.Front);
        return d;
    }

    /// <summary>Texels holding the settled masks <paramref name="m"/> (the front's source with its halo, 0..1.6) and a texel's <paramref name="birth"/>.</summary>
    private static TerritoryTexels TexelsFor(Masks m, float birth)
    {
        birth /= CurseAtlas.BirthScale;
        var front = m.Front / CurseAtlas.FrontScale;
        var page0 = new Vector4(m.Drain, m.Burn, m.Tissue, m.Dark);
        var page1 = new Vector4(m.Edge, m.Emit, m.Fissure, birth);
        return new TerritoryTexels { Page0 = page0, Page0Swell = page0, Page1 = page1, Page1Swell = page1, Page2 = new Vector4(m.Idle, 0f, 0f, front) };
    }

    private static Vector3 Mirror(Color host, Color tint, HostLook look, int stage, TerritoryParams t, TerritoryTexels x)
    {
        var h = CurseMaterial.Host(look, ShadeLevel(stage, AshBurn(look)));
        var o = Composite(host.ToVector4(), tint.ToVector4(), h, new[] { t }, new[] { x }, ReadOnlySpan<PuffParams>.Empty, Vector2.Zero);
        Assert.True(o.W > 0.999f, "an opaque host under a white tint is wholly inside");
        return new Vector3(o.X, o.Y, o.Z);
    }

    private static void Near(Vector3 expected, Vector3 actual, string what)
    {
        var e = Math.Max(Math.Abs(expected.X - actual.X), Math.Max(Math.Abs(expected.Y - actual.Y), Math.Abs(expected.Z - actual.Z)));
        Assert.True(e <= Tolerance, $"{what}: expected {expected}, the mirror gives {actual} (off by {e * 255f:0.0}/255)");
    }

    [Fact]
    public void test_the_drained_and_burned_material_is_the_prototypes_drain_pixel()
    {
        // a whole drain (mask 1, alpha 1) gives exactly CurseHost.DrainPixel at burn 0, a whole burn exactly at burn 1,
        // each x the creature's tint as the DualTextureEffect drew it; a half-burned territory keeps 1-s of the host and
        // takes s(1-b) of the drained copy and sb of the burned one (the prototype's two "over" draws)
        foreach (var look in Looks)
            foreach (var host in Texels)
                foreach (var tint in Tints)
                {
                    var tu = tint.ToVector3();
                    var x = TexelsFor(new Masks(1f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f), 0f);
                    var h = CurseMaterial.Host(look, 0f);
                    Vector3 Run(float drain, float burn)
                    {
                        var t = new TerritoryParams { At = CurseAtlas.BloomReach, DrainAlpha = drain, BurnAlpha = burn, InvSwell = 1f };
                        var o = Composite(host.ToVector4(), tint.ToVector4(), h, new[] { t }, new[] { x }, ReadOnlySpan<PuffParams>.Empty, Vector2.Zero);
                        return new Vector3(o.X, o.Y, o.Z);
                    }
                    Near(DrainPixel(host, look, 0f).ToVector3() * tu, Run(1f, 0f), $"drained {host} on {look}");
                    Near(DrainPixel(host, look, 1f).ToVector3() * tu, Run(0f, 1f), $"burned {host} on {look}");
                    const float s = 0.8f, b = 0.4f;
                    var expected = host.ToVector3() * tu * (1f - s) + DrainPixel(host, look, 0f).ToVector3() * tu * (s * (1f - b))
                                   + DrainPixel(host, look, 1f).ToVector3() * tu * (s * b);
                    Near(expected, Run(s * (1f - b) / (1f - s * b), s * b), $"half-burned {host} on {look}");
                }
    }

    [Fact]
    public void test_the_mirror_composes_every_pass_as_the_prototype_drew_it()
    {
        // every pass at once (drain, stain, screen, fissures, bloom) on each host, through mixed masks: the mirror's
        // per-territory levels (CurseMaterial.Living) and colours give the prototype's passes, Colors and blends
        var masks = new[]
        {
            new Masks(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1.6f),
            new Masks(0.7f, 0.5f, 0.6f, 0.3f, 0.4f, 0.5f, 0.6f, 0.2f, 0.45f),
            new Masks(0.2f, 0f, 0.9f, 0.8f, 0f, 0.1f, 0f, 0.7f, 1.2f),
        };
        foreach (var look in Looks)
            foreach (var burn in new[] { 0f, AshBurn(look), 0.5f, 1f })
                foreach (var reveal in new[] { 0.4f, 1f })
                    foreach (var m in masks)
                        foreach (var host in Texels)
                        {
                            const float breath = 1.1f, boost = 1.3f, answer = 0.8f, settle = 0.9f, peak = 0.6f;
                            const int stage = 2, group = 1;
                            var t = Living(look, burn, reveal, 1f, breath, boost, answer, group, settle, peak);
                            // (mid-bloom, a texel half revealed under the front's band; whole, every texel revealed)
                            var birth = reveal < 1f ? t.At - 0.07f : 1f;
                            var expected = Prototype(host, Tints[1], look, stage, burn, reveal, breath, boost, answer, settle, peak, m, birth);
                            var x = TexelsFor(m, birth);
                            x.Page2 = new Vector4(0f, m.Idle, 0f, x.Page2.W);   // the accent's group 1
                            var actual = Mirror(host, Tints[1], look, stage, t, x);
                            Near(expected, actual, $"{host} on {look}, burn {burn:0.00}, reveal {reveal}, masks {m}");
                        }
    }

    [Fact]
    public void test_a_territorys_levels_are_the_prototypes_material()
    {
        // the per-territory levels are the prototype's expressions of Mat, LightFor, Restraint and DrainStrength
        foreach (var look in Looks)
            foreach (var burn in new[] { 0f, 0.3f, 1f })
            {
                var m = new Mat(look, burn);
                Assert.Equal(LightFor(look.Luma) * MathHelper.Lerp(Restraint(look), BurnRestraint, burn), m.Light, 5);
                var t = Living(look, burn, 0.5f, 1.02f, 1.1f, 1.4f, 0.6f, 2, 0.5f, 0.7f);
                var s = Math.Clamp(DrainStrength(look, burn) * 1f, 0f, 1f);
                Assert.Equal(burn < 1f ? Math.Min(1f, s * (1f - burn) / Math.Max(1e-3f, 1f - s * burn)) : 0f, t.DrainAlpha, 5);
                Assert.Equal(burn > 0f ? s * burn : 0f, t.BurnAlpha, 5);
                Assert.Equal(0.5f * CurseAtlas.BloomReach, t.At, 5);
                Assert.Equal(1f / 1.02f, t.InvSwell, 5);
                Assert.Equal(m.Tissue, t.Tissue, 5);
                Assert.Equal(TissueShare * 0.5f * PaleHost(look.Luma), t.Pale, 5);
                Assert.Equal(burn > 0f ? BurnEdgeShare * burn : 0f, t.Edge, 5);
                Assert.Equal(IdleEmission * m.Light * 1.1f * 1.4f * 0.25f, t.Emit, 5);
                Assert.Equal(0.3f * m.Light * 0.36f * 0.25f * 0.5f, t.Accent, 5);
                Assert.Equal(2f, t.AccentGroup);
                Assert.Equal(burn > 0f ? Math.Min(1f, FissureLight * m.Light * 1.1f * 1.4f * 0.25f * burn) : 0f, t.Fissure, 5);
                Assert.Equal(0.7f * 1f * (1f - 0.3f), t.Front, 5);
            }
        // nothing bloomed draws nothing; a whole territory has no front
        Assert.Equal(default, Living(Pale, 0f, 0f, 1f, 1f, 1f, 1f, 0, 1f, 1f));
        Assert.Equal(0f, Living(Pale, 0f, 1f, 1f, 1f, 1f, 1f, 0, 1f, 1f).Front);
        // a dying host: its light flashes for 150 ms (the fissures on an ash-burned host), then its front collapses
        var flash = Leaving(Black, 1f, 1f, 1f, 75f);
        Assert.True(flash.Fissure > 0f && flash.EmitAdd == 0f);
        Assert.Equal(0.5f * FrontPeak * new Mat(Black, 1f).Light * 0.7f, flash.Front, 5);   // (handing over from the living front, here none)
        var flashViolet = Leaving(Colourful, 0f, 1f, 1f, 75f);
        Assert.True(flashViolet.EmitAdd > 0f && flashViolet.Fissure == 0f);
        var collapse = Leaving(Colourful, 0f, 1f, 0.5f, 400f);
        Assert.Equal(FrontPeak * new Mat(Colourful, 0f).Light * 0.7f * 0.5f, collapse.Front, 5);
        Assert.Equal(0.5f * CurseAtlas.BloomReach, collapse.At, 5);
        Assert.Equal(0f, collapse.Emit);
        Assert.Equal(default, Leaving(Colourful, 0f, 1f, 0.005f, 800f));
    }

    [Fact]
    public void test_the_coverage_replaces_the_stencil_on_its_threshold()
    {
        // the pass is the creature's only draw, at the sprite's own alpha (host x tint); the corrupted host replaces the
        // plain one by the old stencil's threshold (alpha 90/255) as a narrow smooth ramp: under it the creature exactly as
        // the arena draws it, above it the curse; the tint's alpha scales the result, never its colour
        var h = CurseMaterial.Host(Pale, ShadeLevel(1, 0f));
        Vector4 Run(Vector4 host, Vector4 tint)
            => Composite(host, tint, h, ReadOnlySpan<TerritoryParams>.Empty, ReadOnlySpan<TerritoryTexels>.Empty, ReadOnlySpan<PuffParams>.Empty, Vector2.Zero);
        var rgb = new Vector3(200, 100, 50) / 255f;
        var opaque = Run(new Color(200, 100, 50).ToVector4(), Vector4.One);
        Assert.Equal(1f, opaque.W, 5);
        Assert.Equal(rgb.X * h.Shade.X, opaque.X, 4);   // (the body's faint shade: the curse)
        var faint = Run(new Vector4(rgb * (70f / 255f), 70f / 255f), Vector4.One);
        Assert.Equal(70f / 255f, faint.W, 5);
        Assert.Equal(rgb.X * 70f / 255f, faint.X, 5);   // under the threshold: the plain sprite
        var at = Run(new Vector4(rgb * (90f / 255f), 90f / 255f), Vector4.One);
        Assert.Equal(90f / 255f, at.W, 4);
        Assert.Equal(MathHelper.Lerp(rgb.X, opaque.X, 0.5f), at.X / at.W, 4);   // halfway up the ramp
        var faded = Run(new Color(200, 100, 50).ToVector4(), Vector4.One * 0.5f);
        Assert.Equal(0.5f, faded.W, 4);
        Assert.Equal(opaque.Y, faded.Y / faded.W, 4);
    }

    [Fact]
    public void test_the_curse_pass_composites_every_texel_once_like_the_plain_sprite()
    {
        // the pass DRAWS the creature (the arena hands its draw over): over any background B it leaves exactly (1 - A) of B,
        // A = host.a x tint.a, as the plain sprite draw does: a soft edge texel (a = 0.5) is never composited twice (the pass
        // over the creature already drawn left 0.25 B: the rim hardened and popped as the pass began and ended). And a texel
        // no territory lies on, under no shade, IS the plain sprite: 0.5 S + 0.5 B
        static Vector3 Over(Vector4 src, Vector3 dst) => new Vector3(src.X, src.Y, src.Z) + (1f - src.W) * dst;
        var background = new Vector3(0.1f, 0.6f, 0.3f);
        var unshaded = CurseMaterial.Host(Pale, 0f);
        Assert.Equal(Vector3.One, unshaded.Shade);
        var masks = new Masks(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1.6f);
        var t = Living(Pale, 0.5f, 1f, 1f, 1.1f, 1.3f, 0.8f, 1, 0.9f, 0.6f);
        foreach (var a in new[] { 0.05f, 74f / 255f, 0.35f, 0.5f, 0.8f, 1f })
            foreach (var tint in new[] { Color.White, new Color(255, 214, 190), Color.White * 0.5f })
            {
                var host = new Vector4(new Vector3(0.8f, 0.4f, 0.2f) * a, a);
                var tv = tint.ToVector4();
                var plain = Over(host * tv, background);
                var none = Composite(host, tv, unshaded, ReadOnlySpan<TerritoryParams>.Empty, ReadOnlySpan<TerritoryTexels>.Empty,
                                     ReadOnlySpan<PuffParams>.Empty, Vector2.Zero);
                Near(plain, Over(none, background), $"an untouched texel, a = {a:0.00}, tint {tint}");
                // a texel under a whole territory and the body's shade: the curse's colour, the sprite's own coverage
                var cursed = Composite(host, tv, CurseMaterial.Host(Pale, ShadeLevel(3, 0f)), new[] { t }, new[] { TexelsFor(masks, 0f) },
                                       ReadOnlySpan<PuffParams>.Empty, Vector2.Zero);
                Assert.Equal(a * tv.W, cursed.W, 5);
            }
        // the worked case of the review: a = 0.5, no territory, the shade 0.9: 0.45 S + 0.5 B (it was 0.70 S + 0.25 B)
        var half = new Vector4(new Vector3(0.8f, 0.4f, 0.2f) * 0.5f, 0.5f);
        var shaded = CurseMaterial.Host(Pale, ShadeLevel(3, 0f));
        var o = Composite(half, Vector4.One, shaded, ReadOnlySpan<TerritoryParams>.Empty, ReadOnlySpan<TerritoryTexels>.Empty,
                          ReadOnlySpan<PuffParams>.Empty, Vector2.Zero);
        Near(0.5f * new Vector3(0.8f, 0.4f, 0.2f) * shaded.Shade + 0.5f * background, Over(o, background), "a soft edge under the shade");
    }

    [Fact]
    public void test_a_territory_is_placed_on_the_frame_as_the_prototypes_sprite()
    {
        // the host-UV to mask affine lands every point where the prototype's drain quad mapped it (its frame's flip and
        // squash, the field's turn and mirror), and the field's centre on the mask's middle
        var src = new Rectangle(1024, 64, 448, 448);
        var dest = new Rectangle(300, 200, 190, 170);   // squashed
        foreach (var fx in new[] { SpriteEffects.None, SpriteEffects.FlipHorizontally })
            foreach (var flipMask in new[] { false, true })
            {
                var centre = new Vector2(380, 270);
                const float side = 120f, turn = 0.6f;
                var t = new TerritoryParams();
                Place(ref t, src, dest, fx, 4096, 512, centre, side, turn, flipMask, 2);
                Assert.Equal(CurseAtlas.CellOrigin(2), new Vector2(t.MaskU.W, t.MaskV.W));
                foreach (var p in new[] { new Vector2(310, 205), new Vector2(480, 205), new Vector2(395, 360), centre })
                {
                    // the prototype's DrainQuad corner
                    var lx = (p.X - dest.X) / dest.Width;
                    var ly = (p.Y - dest.Y) / dest.Height;
                    if (fx == SpriteEffects.FlipHorizontally) lx = 1f - lx;
                    var uv = new Vector2((src.X + lx * src.Width) / 4096f, (src.Y + ly * src.Height) / 512f);
                    float cos = MathF.Cos(-turn), sin = MathF.Sin(-turn), dx = p.X - centre.X, dy = p.Y - centre.Y;
                    var mx = (dx * cos - dy * sin) / side + 0.5f;
                    var my = (dx * sin + dy * cos) / side + 0.5f;
                    if (flipMask) mx = 1f - mx;
                    Assert.Equal(mx, t.MaskU.X * uv.X + t.MaskU.Y * uv.Y + t.MaskU.Z, 3);
                    Assert.Equal(my, t.MaskV.X * uv.X + t.MaskV.Y * uv.Y + t.MaskV.Z, 3);
                    var px = UvToPx(src, dest, fx, 4096, 512);
                    Assert.Equal(p.X, uv.X * px.X + px.Z, 2);
                    Assert.Equal(p.Y, uv.Y * px.Y + px.W, 2);
                }
            }
    }

    [Fact]
    public void test_the_packed_atlas_holds_the_prototypes_masks_in_their_cells()
    {
        // one texture: per variant three pages (drain, burn, tissue, dark / edge, emit, fissure, birth / the idle groups,
        // the front) and the wisp puff, every value the byte the prototype's masks stored; the masks are empty at their
        // cells' borders, so a clamped sample never reads a neighbour
        var a = CurseAtlas.Grow(CurseAtlas.Seed);
        var data = a.Pack();
        Assert.Equal(CurseAtlas.Width * CurseAtlas.Height, data.Length);
        const int N = CurseAtlas.N;
        Color At(int page, int v, int i) => data[(v * N + i / N) * CurseAtlas.Width + page * N + i % N];
        for (var v = 0; v < CurseAtlas.Variants; v++)
            for (var i = 0; i < N * N; i += 37)
            {
                Assert.Equal(new Color(CurseAtlas.Byte(a.DrainF[v][i]), CurseAtlas.Byte(a.BurnF[v][i]), CurseAtlas.Byte(a.TissueF[v][i]), CurseAtlas.Byte(a.DarkF[v][i])), At(0, v, i));
                Assert.Equal(new Color(CurseAtlas.Byte(a.EdgeF[v][i]), CurseAtlas.Byte(a.EmitF[v][i]), CurseAtlas.Byte(a.FissureF[v][i]),
                                       CurseAtlas.Byte(a.BirthF[v][i] / CurseAtlas.BirthScale)), At(1, v, i));
                Assert.Equal(new Color(CurseAtlas.Byte(a.IdleF[v * 3][i]), CurseAtlas.Byte(a.IdleF[v * 3 + 1][i]), CurseAtlas.Byte(a.IdleF[v * 3 + 2][i]),
                                       CurseAtlas.Byte(a.FrontF[v][i] / CurseAtlas.FrontScale)), At(2, v, i));
            }
        for (var v = 0; v < CurseAtlas.Variants; v++)
            for (var page = 0; page < CurseAtlas.Pages; page++)
                for (var k = 0; k < N; k++)
                    foreach (var i in new[] { k, (N - 1) * N + k, k * N, k * N + N - 1 })
                    {
                        var c = At(page, v, i);
                        Assert.True(c.R <= 1 && c.G <= 1 && c.B <= 1 && (page == 1 || c.A <= 1), $"variant {v} page {page}: a mask reaches the cell's border ({c})");
                    }
        var puff = CurseAtlas.Puff();
        var mid = CurseAtlas.Byte(puff[24 * CurseAtlas.PuffSide + 24]);
        Assert.Equal(new Color(mid, mid, mid, mid), data[(CurseAtlas.PuffSource.Y + 24) * CurseAtlas.Width + CurseAtlas.PuffSource.X + 24]);
        Assert.Equal(Color.Transparent, data[(CurseAtlas.PuffSource.Y - 1) * CurseAtlas.Width + CurseAtlas.PuffSource.X + 24]);
        // the idle accent's groups are the prototype's Idle masks: the light of each section, softened by a texel
        for (var v = 0; v < CurseAtlas.Variants; v++)
            for (var g = 0; g < CurseAtlas.IdleGroups; g++)
            {
                var e = new float[N * N];
                for (var i = 0; i < e.Length; i++) if (a.GroupF[v][i] == g) e[i] = a.EmitF[v][i];
                Assert.Equal(CurseAtlas.Blur(e, 1), a.IdleF[v * CurseAtlas.IdleGroups + g]);
            }
    }

    [Fact]
    public void test_the_analytic_bloom_front_keeps_the_prototypes_soft_halo()
    {
        // the prototype lit each stepped bloom frame's front band and added Blur(front, 2) x 0.6; the shader multiplies
        // the band by a source with that halo baked in. Over every variant and the prototype's six frames, the two
        // fronts carry the same light on the whole (measured -0.1 %; no frame off by more than 10 %, measured 7.8 %, the
        // first frame's small front) and differ by little per texel where either shows (measured 0.045). A wider band or
        // a stronger halo only added light (tried: band 0.105..0.12, halo 0.7..0.8)
        var a = CurseAtlas.Approved;
        const int N = CurseAtlas.N;
        double worstEnergy = 0, worstMean = 0, worstPeak = 0, signed = 0;
        var frames = 0;
        for (var v = 0; v < CurseAtlas.Variants; v++)
            for (var f = 0; f < 6; f++)
            {
                var at = (f + 1) / 6f * CurseAtlas.BloomReach;
                var fg = new float[N * N];
                for (var i = 0; i < fg.Length; i++) fg[i] = a.FrontSourceF[v][i] * CurseAtlas.FrontBandAt(at, a.BirthF[v][i]);
                var bl = CurseAtlas.Blur(fg, 2);
                double proto = 0, shader = 0, diff = 0;
                var shown = 0;
                for (var i = 0; i < fg.Length; i++)
                {
                    var p = Math.Min(1f, fg[i] + bl[i] * 0.6f);
                    var stored = CurseAtlas.Byte(a.FrontF[v][i] / CurseAtlas.FrontScale) / 255f * CurseAtlas.FrontScale;
                    var birth = CurseAtlas.Byte(a.BirthF[v][i] / CurseAtlas.BirthScale) / 255f * CurseAtlas.BirthScale;
                    var s = Math.Min(1f, CurseAtlas.FrontBandAt(at, birth) * stored);
                    proto += p;
                    shader += s;
                    if (p <= 0.02f && s <= 0.02f) continue;
                    shown++;
                    diff += Math.Abs(p - s);
                    worstPeak = Math.Max(worstPeak, Math.Abs(p - s));
                }
                if (proto < 1) continue;
                worstEnergy = Math.Max(worstEnergy, Math.Abs(shader - proto) / proto);
                signed += (shader - proto) / proto;
                frames++;
                worstMean = Math.Max(worstMean, diff / Math.Max(1, shown));
            }
        Assert.True(Math.Abs(signed / frames) <= 0.02, $"the shader's front carries {signed / frames:P1} more light than the prototype's on the whole");
        Assert.True(worstEnergy <= 0.1, $"a frame of the shader's front carries {worstEnergy:P1} more or less light than the prototype's");
        Assert.True(worstMean <= 0.06, $"the shader's front differs by {worstMean:0.000} per lit texel (worst texel {worstPeak:0.00})");
    }

    [Fact]
    public void test_the_shader_follows_the_mirror_line_by_line_and_its_binary_is_current()
    {
        // BrandCurse.fx and CurseMaterial.Composite carry the same tagged steps; the effect's every parameter is declared;
        // the committed binary was built from this source (an edited shader that was not rebuilt fails here: run
        // tools/shaders/build_shaders.sh); it ships beside the game and never goes through Content.mgcb (CI cannot run mgfxc)
        var fx = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Content", "Shaders", "BrandCurse.fx"));
        var mirror = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "Curse", "CurseMaterial.cs"));
        var loader = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "Curse", "BrandCurseEffect.cs"));
        string[] Tags(string s) => Regex.Matches(s, @"\[(H\d|F\d|P\d|R|K)\]").Select(m => m.Value).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "[F1]", "[F2]", "[H1]", "[H2]", "[H3]", "[K]", "[P1]", "[P2]", "[P3]", "[P4]", "[R]" }, Tags(fx));
        Assert.Equal(Tags(fx), Tags(mirror));
        foreach (Match m in Regex.Matches(loader, "Required\\(p, \"(\\w+)\"\\)"))
            Assert.Matches(new Regex(@"^(Texture2D|float[34]?)\s+" + m.Groups[1].Value + @"\b", RegexOptions.Multiline), fx);
        Assert.DoesNotContain("Parameters[", loader);

        var binary = RepoFile("assets", "shaders", "brand_curse.mgfxo");
        Assert.True(File.Exists(binary), "assets/shaders/brand_curse.mgfxo is missing: run tools/shaders/build_shaders.sh");
        Assert.Equal("MGFX", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(binary), 0, 4));
        var source = File.ReadAllBytes(RepoFile("src", "IdleXIdle.Game", "Content", "Shaders", "BrandCurse.fx")).Where(b => b != (byte)'\r').ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
        Assert.Equal(hash, File.ReadAllText(binary + ".source-sha256").Trim());

        var csproj = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "IdleXIdle.Game.csproj"));
        Assert.Contains(@"<Content Include=""..\..\assets\shaders\*.mgfxo""", csproj);
        Assert.DoesNotContain(".fx", File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Content", "Content.mgcb")));
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, BrandCurseEffect.RelativePath)), "the shader is not copied beside the game");
    }
}
