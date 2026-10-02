using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using IdleXIdle.Game.Vfx;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// CONCEPT A, LIVING SHADOW CORRUPTION (the owner's choice, 2026-10-01), the TERRITORY pass (owner's brief, from
/// 9aa407fd): the curse is SEPARATE REGIONS OF THE BODY BECOMING CORRUPTED, never a vein network drawn across it. Long
/// connected paths lined up with collars, robe edges and belts and read as "a sash", "a stole", "embroidery"; violet light
/// laid over dark casters read as "its own aura". So the persistent state is now infected TERRITORIES, each one the
/// host's own material changed (drained of its colour, darkened, its local variation flattened, violet-grey) with a
/// restrained violet light from inside, a dark smoky tissue, a short vein fragment or two, and Shadow wisps that leak out
/// of it past the silhouette.
/// </summary>
/// <remarks>
/// <para>
/// DEPTH IS HOW MUCH OF THE BODY IS INFECTED: one territory at depth 1, a second elsewhere on the body at depth 2, three
/// or four at depth 3; never connected into a network (a deepen's propagation is a brief shadow travelling under the
/// skin, gone when the new territory has bloomed).
/// </para>
/// <para>
/// THE HOST DECIDES THE CONTRAST, by rule, never by name, as a continuous blend of two material responses. MODE A, SHADOW
/// VIOLET, where violet contrasts with the host naturally: its colour drained to violet-grey, a restrained violet light.
/// MODE B, ASH-BURN (owner's brief, from 91c1650b), where the host's own look is already dark, shadowy or violet and the
/// curse's violet read as "its own aura": the Shadow has BURNED THE COLOUR OUT of the victim, cold ash pushed away from
/// its own value, near-black burnt edges, pale spectral fissures leaking from inside, a very restrained violet, Shadow
/// wisps leaving the damage (never ice, holy light, electricity, poison, fire or a white glow). The blend is the host's
/// brightness, colourfulness, native violet and share of shadow-coloured pixels, and per territory the shadow colours
/// right under it. Territories are seated from the host's own STABLE silhouette (in its mass, apart, never three in a
/// row, clear of the authored head and the feet), the first in its central body mass.
/// </para>
/// </remarks>
public sealed partial class CursePrototype
{
    // ── THE MATERIAL ───────────────────────────────────────────────────────────────────────────────────────────────────
    // the stain MULTIPLIED into the host (it discolours the host's own material, keeping its folds and shading): the vein
    // fragments a deep bruised violet, the infected tissue a greyed lavender (on a pale host a deep saturated violet, laid
    // twice: once, grey lavender read as "dirt", "a dye stain")
    // (the probe: a lavender tissue and a full inner glow read as "a purple patch" laid on the creature; the HOST's
    // drained material leads now, the tissue darkens it, the violet stays in the cracks)
    private static readonly Color VeinStain = new(46, 18, 78);
    private static readonly Color BruiseStain = new(92, 88, 100);
    private static readonly Color PaleBruise = new(104, 96, 116);
    private static readonly Color WispSmoke = new(62, 34, 92);
    private static readonly Color AfflictedShade = new(150, 124, 176);

    // a dark host's territory faintly LIT FROM INSIDE (its flesh has already turned to ash: the drain carries it)
    private static readonly Color TissueGlow = new(92, 84, 112);   // the ash's own faint light, barely violet
    private const float TissueGlowShare = 0.35f;

    // the curse's light: violet-magenta; on a host already rich in violet it turns PALER, nearer white, so it never reads
    // as more of the creature's own violet
    private static readonly Color Emission = new(168, 70, 236);
    private static readonly Color EmissionPale = new(206, 178, 246);
    private static readonly Color EmissionHot = new(236, 150, 255);

    // MODE B, ASH-BURN: the colour burned out of the victim. Cold ash (a breath of blue in the grey, never cyan ice, never
    // warm), the host's folds kept in it; a near-black burnt rim, ragged, creeping inward in specks; the vein fragments
    // charred; the light a pale spectral leak from the fissures, barely lavender (a violet one on a dark caster read as
    // "its own aura"), restrained; the wisps Shadow smoke, ash-dark
    private static readonly Vector3 AshTint = new(0.95f, 0.97f, 1.0f);
    private const float AshFold = 1.3f;   // (kept at 0.9 the ash read as "a see-through haze laid over the robe")
    private static readonly Color BurnEdge = new(24, 17, 28);
    private const float BurnEdgeShare = 0.85f;
    private static readonly Color CharVein = new(40, 26, 54);
    private static readonly Color SpectralPale = new(198, 192, 226);
    private static readonly Color SpectralHot = new(226, 214, 252);
    private static readonly Color AshSmoke = new(52, 46, 64);
    private const float BurnRestraint = 0.62f;
    private const float FissureLight = 0.95f;   // the fissures' light, added, against the ash (Light carries the restraint)

    /// <summary>SCREEN for the steady light: it glows on a dark body and in the folds of a pale one, and fades where the
    /// host is already bright, so the light sits IN the host's shading instead of lying across it.</summary>
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

    // ── TIMING ─────────────────────────────────────────────────────────────────────────────────────────────────────────
    private const float GrowMs = 520f;          // an apply blooms its first territory over this
    private const float ArriveGrowMs = 460f;    // an arrival (a transfer, a hop) blooms every territory it carries
    private const float ArriveStaggerMs = 70f;  // ... one after another
    private const float TravelMs = 170f;        // a deepen: the shadow travelling under the skin to the new territory
    private const float DeepenGrowMs = 400f;    // ... then the new territory blooms
    private const float DeepenStaggerMs = 90f;  // (depth 3's two new territories, one after the other)
    private const float SettleMs = 380f;        // then the extra light settles
    private const float IdleEmission = 0.85f;   // the resting emission
    private const float FrontPeak = 0.72f;      // a blooming front at its brightest (0.95 took the old deepen above PRESS)
    private const float QuietShare = 0.55f;     // beside an action, a reaction or the field's crush
    private const float ArrivalShare = 0.55f;   // a transfer's / a hop's awakening, against the apply

    /// <summary>At most this many territories on one host (depth 3 shows three or four, as the body allows).</summary>
    internal const int MaxTerritories = 4;

    /// <summary>Territories shown at a depth: one, two, then as many as the body holds (three or four).</summary>
    internal static int TerritoriesAt(int stage) => stage <= 0 ? 0 : stage == 1 ? 1 : stage == 2 ? 2 : MaxTerritories;

    private TerritoryAtlas? _territories;

    private TerritoryAtlas Territories(GraphicsDevice device) => _territories ??= TerritoryAtlas.For(device);

    // ── THE HOST ───────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>What a host is made of, read once per strip: its mean brightness, its colourfulness (mean chroma of its
    /// opaque pixels, 0..1), its native violet (the share of its coloured pixels that are blue-violet to magenta) and its
    /// SHADOW share (of its opaque pixels, the blue-to-magenta tinted and the near-black: a look the curse's own Shadow
    /// colours belong to).</summary>
    internal readonly record struct HostLook(float Luma, float Chroma, float Violet, float Shadow = 0f);

    private sealed class HostEntry
    {
        public HostLook Look;
        public float Burn;                // its Ash-Burn share (0 Shadow Violet .. 1 Ash-Burn)
        public Texture2D Drained = null!;    // Shadow Violet (burn 0)
        public Texture2D Burned = null!;     // Ash-Burn (burn 1): each territory blends the two by its own share
        public byte[] Alpha = null!;
        public byte[] Change = null!;     // how visibly each pixel changes when drained (0..255)
        public byte[] Compete = null!;    // 1 where the host's own colour is blue-violet (the curse's violet competes there)
        public int Width;
    }

    private static readonly Dictionary<Texture2D, HostEntry> HostCache = new();

    /// <summary>
    /// The host's look and its DRAINED copy (every pixel through <see cref="DrainPixel(Color, HostLook, float)"/>, alpha
    /// kept), built once per strip texture: a prototype's read-back (the production path is one shader pass, after the
    /// picture is approved). Every strip of one creature takes the look of its resting strip (<paramref name="lookOf"/>):
    /// measured per strip, a creature's territories changed material when it lunged.
    /// </summary>
    private static HostEntry Host(GraphicsDevice device, Texture2D tex, HostEntry? lookOf = null)
    {
        if (HostCache.TryGetValue(tex, out var known)) return known;
        var w = tex.Width;
        var h = tex.Height;
        var data = new Color[w * h];
        tex.GetData(data);
        var look = lookOf?.Look ?? Measure(data);
        var burn = lookOf?.Burn ?? AshBurn(look);
        var drained = new Color[data.Length];
        var burned = new Color[data.Length];
        var alpha = new byte[data.Length];
        var change = new byte[data.Length];
        var compete = new byte[data.Length];
        for (var i = 0; i < data.Length; i++)
        {
            alpha[i] = data[i].A;
            drained[i] = DrainPixel(data[i], look, 0f);
            burned[i] = DrainPixel(data[i], look, 1f);
            change[i] = Visible(data[i], burn <= 0f ? drained[i] : DrainPixel(data[i], look, burn));
            compete[i] = Competes(data[i]) ? (byte)1 : (byte)0;
        }
        var tx = new Texture2D(device, w, h);
        tx.SetData(drained);
        var tb = new Texture2D(device, w, h);
        tb.SetData(burned);
        var e = new HostEntry { Look = look, Burn = burn, Drained = tx, Burned = tb, Alpha = alpha, Change = change, Compete = compete, Width = w };
        HostCache[tex] = e;
        if (PresentTrace.Enabled)
            PresentTrace.Log("curse-host", $"luma={look.Luma:0.000}	chroma={look.Chroma:0.000}	violet={look.Violet:0.000}	shadow={look.Shadow:0.000}	burn={burn:0.00}	drain={DrainStrength(look, burn):0.00}	size={w}x{h}");
        return e;
    }

    /// <summary>How visibly a pixel changes when drained: mostly the COLOUR it loses, a little its brightness shift
    /// (0..255; weighted by brightness, a territory went for pale bone at a hand and read as "a spell it is casting").</summary>
    internal static byte Visible(Color before, Color after)
    {
        if (before.A < 128) return 0;
        float lb = 0.299f * before.R + 0.587f * before.G + 0.114f * before.B;
        float la = 0.299f * after.R + 0.587f * after.G + 0.114f * after.B;
        float cb = Math.Max(before.R, Math.Max(before.G, before.B)) - Math.Min(before.R, Math.Min(before.G, before.B));
        float ca = Math.Max(after.R, Math.Max(after.G, after.B)) - Math.Min(after.R, Math.Min(after.G, after.B));
        return (byte)Math.Clamp(MathF.Abs(lb - la) * 0.3f + Math.Max(0f, cb - ca) * 1.0f, 0f, 255f);
    }

    /// <summary>Mean luma, mean chroma, native violet share and shadow share of the opaque pixels (premultiplied input).</summary>
    internal static HostLook Measure(Color[] data)
    {
        double sl = 0, sc = 0;
        int n = 0, coloured = 0, violet = 0, shadow = 0;
        foreach (var c in data)
        {
            if (c.A < 128) continue;
            var a = c.A / 255f;
            float r = c.R / 255f / a, g = c.G / 255f / a, bl = c.B / 255f / a;
            var max = MathF.Max(r, MathF.Max(g, bl));
            var min = MathF.Min(r, MathF.Min(g, bl));
            var l = 0.299f * r + 0.587f * g + 0.114f * bl;
            sl += l;
            sc += max - min;
            n++;
            var hue = max - min >= 0.04f ? Hue(r, g, bl, max, min) : -1f;
            if (l < 0.12f || hue is >= 220f and <= 340f) shadow++;
            if (max - min < 0.15f) continue;
            coloured++;
            if (hue is >= 250f and <= 330f) violet++;
        }
        if (n == 0) return new HostLook(0.3f, 0f, 0f, 0f);
        return new HostLook((float)Math.Clamp(sl / n, 0.0, 1.0), (float)Math.Clamp(sc / n, 0.0, 1.0),
                            coloured == 0 ? 0f : violet / (float)Math.Max(coloured, n / 4), shadow / (float)n);
    }

    /// <summary>Is this pixel's own colour blue-violet (the curse's violet would compete with it)?</summary>
    internal static bool Competes(Color c)
    {
        if (c.A < 128) return false;
        var a = c.A / 255f;
        float r = c.R / 255f / a, g = c.G / 255f / a, b = c.B / 255f / a;
        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        return max - min >= 0.06f && Hue(r, g, b, max, min) is >= 220f and <= 340f;
    }

    private static float Hue(float r, float g, float b, float max, float min)
    {
        var d = max - min;
        if (d <= 0f) return 0f;
        float h;
        if (max == r) h = (g - b) / d % 6f;
        else if (max == g) h = (b - r) / d + 2f;
        else h = (r - g) / d + 4f;
        h *= 60f;
        return h < 0f ? h + 360f : h;
    }

    /// <summary>
    /// 0..1, the host's ASH-BURN share: a look already dark and Shadow-coloured (most of its pixels blue-to-magenta or
    /// near-black, little other colour) or rich in native violet; never on a pale host (ash there would read as white).
    /// A continuous rule of the host's look, never of its name.
    /// </summary>
    internal static float AshBurn(HostLook h)
        => (1f - PaleHost(h.Luma)) * Math.Max(Ramp(h.Shadow, 0.42f, 0.7f) * (1f - 0.6f * Ramp(h.Chroma, 0.14f, 0.28f)), VioletNorm(h.Violet));

    // THE DRAINED MATERIAL: the infected tissue loses its native colour (grey), its local variation flattens toward the
    // host's mean (the folds stay, softer), it darkens, and it takes a cold contamination tint: violet-grey, or ASH on a
    // host already rich in violet (a violet tint there would hand the colour straight back)
    private const float DrainFlatten = 0.72f;   // local contrast kept
    private static readonly Vector3 VioletGrey = new(0.97f, 0.94f, 1.0f);   // a cold grey, barely violet
    private static readonly Vector3 AshGrey = new(0.96f, 0.96f, 0.97f);
    private static readonly Vector3 AshContamination = new(0.9f, 0.84f, 1.06f);

    /// <summary>
    /// The drained material's mean brightness: away from the host's own (a dark host lightens toward ash, up to +0.25; a
    /// pale one darkens, down by 0.26; a mid one darkens a little).
    /// </summary>
    internal static float DrainedMean(float luma)
        => luma + 0.25f * Ramp(luma, 0.4f, 0.12f) - 0.26f * PaleHost(luma) - 0.04f * (1f - Ramp(luma, 0.4f, 0.12f)) * (1f - PaleHost(luma));

    /// <summary>
    /// The ASH's mean brightness (Ash-Burn): far from the host's own value. A dark host's burned tissue is a mid ash, about
    /// half-bright (at least +0.28 over its own); a light one's chars darker instead (-0.24).
    /// </summary>
    internal static float AshMean(float luma)
        => Lerp(Math.Max(luma + 0.28f, 0.48f + 0.3f * luma), luma - 0.24f, Ramp(luma, 0.34f, 0.5f));

    /// <summary>One premultiplied pixel of the drained copy (alpha kept), at the host's own Ash-Burn share.</summary>
    internal static Color DrainPixel(Color c, HostLook host) => DrainPixel(c, host, AshBurn(host));

    /// <summary>One premultiplied pixel of the drained copy (alpha kept), Shadow Violet blended into Ash-Burn by
    /// <paramref name="burn"/>.</summary>
    internal static Color DrainPixel(Color c, HostLook host, float burn)
    {
        if (c.A == 0) return Color.Transparent;
        var a = c.A / 255f;
        float r = c.R / 255f / a, g = c.G / 255f / a, b = c.B / 255f / a;
        var l = 0.299f * r + 0.587f * g + 0.114f * b;
        // its folds flattened round a NEW mean, pushed off the host's own: a dark host's flesh turns to ash (lighter), a
        // pale host's cloth goes dark, a mid one only a little darker; its colour goes (grey). A patch the host's own
        // colours could have made (violet on a dark caster, purple on a robe) read as "its own aura", "a tunic", "a sash"
        var level = DrainedMean(host.Luma) + (l - host.Luma) * DrainFlatten;
        var tint = Vector3.Lerp(VioletGrey, AshGrey, VioletNorm(host.Violet));
        // (on a near-black host the ash takes a faint violet-grey contamination: a smooth neutral grey read as "a
        // see-through hole", the floor showing through)
        // (never on a host already rich in violet: that would hand its own colour back)
        tint = Vector3.Lerp(tint, AshContamination, DarkHost(host.Luma) * (1f - VioletNorm(host.Violet)));
        var v = Vector3.Clamp(tint * level, Vector3.Zero, Vector3.One);
        if (burn > 0f)
        {
            // ASH-BURN: the colour burned out, the value pushed FAR from the host's own, a cold ash, its folds kept
            var ash = AshMean(host.Luma) + (l - host.Luma) * AshFold;
            v = Vector3.Lerp(v, Vector3.Clamp(AshTint * ash, Vector3.Zero, Vector3.One), Math.Min(1f, burn));
        }
        v *= a;
        return new Color(v.X, v.Y, v.Z, a);
    }

    /// <summary>0..1: how much native violet the host has (0 below ~6 % of its pixels, 1 from ~30 %).</summary>
    internal static float VioletNorm(float violet) => Ramp(violet, 0.06f, 0.3f);

    /// <summary>
    /// How strongly a territory drains the host: more where there is more colour to lose (a cyan lich, a red matron, a
    /// gold angel: the territory is visibly grey), more on a host already violet (it must visibly change, not only glow),
    /// most where it burns to ash (the host material must visibly be WRONG there).
    /// </summary>
    internal static float DrainStrength(HostLook host) => DrainStrength(host, AshBurn(host));

    internal static float DrainStrength(HostLook host, float burn)
        => Math.Min(0.95f, Math.Clamp(0.55f + 0.3f * Ramp(host.Chroma, 0.05f, 0.35f) + 0.15f * VioletNorm(host.Violet) + 0.3f * Ramp(host.Luma, 0.4f, 0.12f), 0f, 0.92f)
                           + 0.3f * burn);

    private static float Ramp(float x, float a, float b)
    {
        var t = Math.Clamp((x - a) / (b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>1 on a near-black host (its territories are lit from inside), 0 from a mid-dark one.</summary>
    private static float DarkHost(float luma) => Ramp(luma, 0.32f, 0.12f);

    /// <summary>1 on a pale host (its bruise is a deep saturated violet), 0 below a mid one.</summary>
    private static float PaleHost(float luma) => Ramp(luma, 0.42f, 0.62f);

    private static Color BruiseFor(float luma) => Color.Lerp(BruiseStain, PaleBruise, PaleHost(luma));

    /// <summary>The infected tissue's share over the drained material (it darkens it, smoky; it never repaints it).</summary>
    private const float TissueShare = 0.6f;

    /// <summary>The tissue's stain on this host (on a dark host it only crusts the ash: multiplied whole it was black
    /// again, "a hole"); the same alive and dying.</summary>
    private static float TissueOn(HostLook look) => TissueShare * (1f - 0.45f * Ramp(look.Luma, 0.4f, 0.12f));

    /// <summary>The light's restraint on this host: on a host rich in violet, or dark (a caster's own colours), the
    /// drained material carries the state, never more violet ("its own aura"); the same alive and dying. A dark but
    /// COLOURFUL host (a cyan, a lava-orange one) is not restrained for its darkness: violet contrasts with its colours
    /// (held back, the crystal caster's violet read as "a pattern on its waist cloth").</summary>
    private static float Restraint(HostLook look)
    {
        var darkish = Ramp(look.Luma, 0.42f, 0.15f) * (1f - Ramp(look.Chroma, 0.1f, 0.25f));
        return (1f - 0.3f * VioletNorm(look.Violet)) * (1f - 0.45f * darkish) * (1f - 0.35f * darkish * (1f - Ramp(look.Chroma, 0.05f, 0.3f)));
    }

    // the colour each creature was drawn with this frame (its tier tint, the ember flush of a bite's wind-up): the
    // drained material takes it too, or the territory stayed cold grey while the body warmed (code review, 2026-10-01)
    private Color[]? _tint;

    /// <summary>
    /// HOST-AWARE LIGHT (a rule of the host's brightness, never of its name): up to 1.4x on a near-black body, where the
    /// dark corruption cannot show; trimmed a little on a pale one, where the additive front was the loudest.
    /// </summary>
    private static float LightFor(float luma)
    {
        var p = Math.Clamp((luma - 0.35f) / 0.35f, 0f, 1f);
        var dark = Math.Clamp((0.2f - luma) / 0.12f, 0f, 1f);
        // (up to 1.15x: the ash now carries a near-black host's region; at 1.4x the light read as "a glowing ball")
        return (1f - 0.15f * p * p * (3f - 2f * p)) * (1f + 0.15f * dark * dark * (3f - 2f * dark));
    }

    /// <summary>The curse's violet light on this host: paler on a host already rich in violet, and restrained there.</summary>
    private static Color EmissionFor(HostLook host) => Color.Lerp(Emission, EmissionPale, 0.7f * VioletNorm(host.Violet));

    /// <summary>One territory's material: Shadow Violet blended into Ash-Burn by its <see cref="Burn"/>; the same alive and
    /// dying.</summary>
    private readonly struct Mat
    {
        public readonly float Burn, Light, Tissue, Glow;
        public readonly Color Emission, Accent, Hot, Vein, Smoke, Bruise;

        public Mat(HostLook look, float burn)
        {
            Burn = burn;
            Light = LightFor(look.Luma) * Lerp(Restraint(look), BurnRestraint, burn);
            // (the dark tissue would blacken the ash again: it only crusts it)
            Tissue = TissueOn(look) * (1f - 0.55f * burn);
            Glow = TissueGlowShare * DarkHost(look.Luma) * (1f - 0.6f * burn);
            Emission = Color.Lerp(EmissionFor(look), SpectralPale, 0.85f * burn);
            // one section at a time answers in a breath of the curse's violet: its only violet on an ash-burned host
            Accent = Color.Lerp(Emission, CursePrototype.Emission, 0.45f * burn);
            Hot = Color.Lerp(EmissionHot, SpectralHot, 0.55f * burn);
            Vein = Color.Lerp(VeinStain, CharVein, burn);
            Smoke = Color.Lerp(WispSmoke, AshSmoke, burn);
            Bruise = BruiseFor(look.Luma);
        }
    }

    // ── WHERE THE TERRITORIES SIT ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One host strip's territories, in the strip's SOURCE pixels, never the screen's: their centres from the strip's
    /// mean authored body point (unflipped) and their diameters, each territory's Ash-Burn share, seated once per strip and
    /// orientation from the host's own stable silhouette. Mapped through each drawn frame's own transform (stored on
    /// screen, a seat measured on a squashed frame kept that squash for the creature's whole life).
    /// </summary>
    internal sealed class Layout
    {
        public readonly Vector2[] Offset = new Vector2[MaxTerritories];
        public readonly float[] Diameter = new float[MaxTerritories];
        public readonly float[] Burn = new float[MaxTerritories];
        public int Available;
    }

    /// <summary>A drawn frame's source-to-screen scale and mirror.</summary>
    internal readonly struct FrameMap
    {
        public readonly float Kx, Ky, K;
        public readonly bool Flip;

        public FrameMap(SpriteFrame frame)
        {
            Kx = frame.Src.Width > 0 ? frame.Dest.Width / (float)frame.Src.Width : 1f;
            Ky = frame.Src.Height > 0 ? frame.Dest.Height / (float)frame.Src.Height : 1f;
            K = MathF.Sqrt(Kx * Ky);
            Flip = (frame.Effects & SpriteEffects.FlipHorizontally) != 0;
        }

        /// <summary>Territory <paramref name="k"/>'s centre on screen, from this frame's body point.</summary>
        public Vector2 At(Layout lay, int k, Vector2 anchor)
            => anchor + new Vector2((Flip ? -lay.Offset[k].X : lay.Offset[k].X) * Kx, lay.Offset[k].Y * Ky);

        /// <summary>Territory <paramref name="k"/>'s diameter on screen.</summary>
        public float Diameter(Layout lay, int k) => lay.Diameter[k] * K;
    }

    private static readonly Dictionary<(Texture2D, bool), Layout> LayoutCache = new();
    private Layout?[]? _layout;
    private HostEntry?[]? _hostOf, _restOf;

    /// <summary>Each territory's share of the first one's diameter (later infections are a little smaller).</summary>
    private static readonly float[] TerritoryShare = { 1f, 0.82f, 0.72f, 0.64f };

    /// <summary>The first territory's diameter on screen: a share of the whole body, never a buckle-sized patch.</summary>
    private static float FirstDiameter(Rectangle body) => Math.Clamp(0.4f * MathF.Sqrt(body.Width * (float)body.Height), 56f, 210f);

    /// <summary>
    /// The first territory's diameter in the resting strip's SOURCE pixels: the creature's canonical body on screen (its
    /// idle's figure, never squashed) against that strip's own figure (<paramref name="content"/>, source pixels), so no
    /// drawn frame enters it (taken through the frame drawn at the first sight, a buckle or a lunge's canvas was kept).
    /// </summary>
    internal static float SourceDiameter(Rectangle body, Point content)
        => FirstDiameter(body) / MathF.Sqrt(body.Width / (float)Math.Max(1, content.X) * (body.Height / (float)Math.Max(1, content.Y)));

    /// <summary>
    /// Readies this slot's host and territories for the frame (called before the curse draws): the territories are seated
    /// on the creature's RESTING strip (<paramref name="rest"/>, its idle; the drawn strip when none is given) and the
    /// drawn strip's drained copy takes that strip's look.
    /// </summary>
    private void PrepareCorruption(GraphicsDevice device, int slot, Vector2 anchor, Rectangle body, SpriteFrame frame, MarkPoints? points,
                                   Texture2D? rest, MarkPoints? restPoints)
    {
        _layout ??= new Layout?[_kind.Length];
        _hostOf ??= new HostEntry?[_kind.Length];
        _restOf ??= new HostEntry?[_kind.Length];
        var seatOn = rest ?? frame.Texture;
        var restHost = Host(device, seatOn);
        _restOf[slot] = restHost;
        _hostOf[slot] = seatOn == frame.Texture ? restHost : Host(device, frame.Texture, restHost);
        // each creature's territories are LOCKED where they were first seated (seated per strip, they jumped from the
        // belly to the shoulder when the creature lunged: "the patch moves", "a band that does not follow the body")
        if (_layout[slot] is not null) return;
        var odd = slot % 2 == 1;
        var key = (seatOn, odd);
        if (!LayoutCache.TryGetValue(key, out var lay))
        {
            lay = Seat(anchor, body, frame, restHost, odd, rest is null ? points : restPoints, seatOn);
            LayoutCache[key] = lay;
        }
        _layout[slot] = lay;
    }

    private static Layout Seat(Vector2 anchor, Rectangle body, SpriteFrame frame, HostEntry host, bool odd, MarkPoints? points, Texture2D seatOn)
    {
        var tex = seatOn;
        // (the resting strip's first frame stands in for the drawn one: the whole strip is read anyway)
        var src = seatOn == frame.Texture ? frame.Src : new Rectangle(0, 0, Math.Min(seatOn.Width, seatOn.Height), seatOn.Height);
        // the whole strip (one row of square frames) seats the territories, never the one pose drawn now (a lunge, a
        // squash, a claw raised): a lone texture seats on its drawn rectangle
        var strip = tex.Height > 0 && tex.Width % tex.Height == 0 && src.Width <= tex.Height && src.Height <= tex.Height;
        var fw = strip ? tex.Height : src.Width;
        var fh = strip ? tex.Height : src.Height;
        var frames = strip ? tex.Width / tex.Height : 1;
        var ox = strip ? src.X / fw * fw : src.X;
        var oy = strip ? 0 : src.Y;
        var alpha = new byte[fw * fh * frames];
        var change = new byte[alpha.Length];
        for (var f = 0; f < frames; f++)
            for (var y = 0; y < fh; y++)
            {
                var from = (oy + y) * host.Width + (strip ? f * fw : ox);
                Array.Copy(host.Alpha, from, alpha, (f * fh + y) * fw, fw);
                Array.Copy(host.Change, from, change, (f * fh + y) * fw, fw);
            }
        var map = new FrameMap(frame);
        Vector2 ToLocal(Vector2 p)
        {
            // (in the drawn frame; a resting strip without authored points takes it as an approximation)
            var lx = (p.X - frame.Dest.X) / map.Kx;
            var fx = frame.Texture.Height > 0 && frame.Src.Width <= frame.Texture.Height ? frame.Src.X / frame.Texture.Height * frame.Texture.Height : frame.Src.X;
            return new Vector2((map.Flip ? frame.Src.Width - lx : lx) + frame.Src.X - fx, (p.Y - frame.Dest.Y) / map.Ky + frame.Src.Y);
        }
        // the territories hang from the strip's MEAN authored body point (each frame's own point carries them with it)
        var hasPoints = strip && points is { Points.Count: > 0 };
        var anchorLocal = hasPoints ? points!.MeanPoint * fw : ToLocal(anchor);
        // the resting strip's own figure (its opaque bounds over every frame)
        int cx0 = fw, cx1 = -1, cy0 = fh, cy1 = -1;
        for (var f = 0; f < frames; f++)
            for (var y = 0; y < fh; y++)
                for (var x = 0; x < fw; x++)
                {
                    if (alpha[(f * fh + y) * fw + x] < 128) continue;
                    cx0 = Math.Min(cx0, x); cx1 = Math.Max(cx1, x); cy0 = Math.Min(cy0, y); cy1 = Math.Max(cy1, y);
                }
        var content = cx1 < 0 ? new Point(fw, fh) : new Point(cx1 - cx0 + 1, cy1 - cy0 + 1);
        var bodySrc = new Vector2(content.X, content.Y);
        var d0 = SourceDiameter(body, content);
        // the first territory's pull: a little off the body's centre line toward its back (away from the face)
        var wideBody = body.Width > body.Height * 1.05f;
        var back = map.Flip ? -1f : 1f;
        var target = anchorLocal + new Vector2((wideBody ? 0f : odd ? -0.5f : 1f) * 0.08f * bodySrc.X * back, -0.02f * bodySrc.Y);
        var sizes = new float[MaxTerritories];
        for (var k = 0; k < MaxTerritories; k++) sizes[k] = d0 * TerritoryShare[k];
        List<Vector4>? heads = null;
        if (hasPoints && points!.Heads.Count > 0)
        {
            heads = new List<Vector4>(points.Heads.Count);
            foreach (var hb in points.Heads) heads.Add(hb * fw);
        }
        // enemies face the hunter on their left: the head is at the screen's left, src-left unless the strip is flipped
        var seats = TerritorySeats(alpha, fw, fh, target, sizes, !map.Flip, change, frames, heads);
        var lay = new Layout { Available = seats.Count };
        var paleOff = 1f - PaleHost(host.Look.Luma);
        var trace = PresentTrace.Enabled ? new System.Text.StringBuilder() : null;
        for (var k = 0; k < seats.Count; k++)
        {
            lay.Offset[k] = seats[k] - anchorLocal;
            lay.Diameter[k] = sizes[k];
            // where the host's OWN colour under this territory is blue-violet, the curse's violet competes: it burns there
            var compete = CompeteShare(host, strip, fw, fh, frames, ox, oy, seats[k], 0.5f * sizes[k]);
            lay.Burn[k] = Math.Max(host.Burn, paleOff * 0.8f * Ramp(compete, 0.25f, 0.55f));
            trace?.Append($"{seats[k].X:0},{seats[k].Y:0}/{sizes[k]:0}/b{lay.Burn[k]:0.00}/c{compete:0.00} ");
        }
        if (trace is not null)
            PresentTrace.Log("curse-seat", $"territories={seats.Count}	{trace}	anchor={anchorLocal.X:0},{anchorLocal.Y:0}	frame={fw}x{fh}x{frames}	heads={(heads?.Count ?? 0)}");
        return lay;
    }

    /// <summary>The share of the host's opaque pixels within <paramref name="r"/> of <paramref name="c"/> (frame-local, over
    /// every frame of the strip) whose own colour is blue-violet.</summary>
    private static float CompeteShare(HostEntry host, bool strip, int fw, int fh, int frames, int ox, int oy, Vector2 c, float r)
    {
        long opaque = 0, compete = 0;
        int x0 = Math.Max(0, (int)(c.X - r)), x1 = Math.Min(fw - 1, (int)(c.X + r));
        int y0 = Math.Max(0, (int)(c.Y - r)), y1 = Math.Min(fh - 1, (int)(c.Y + r));
        var r2 = r * r;
        for (var f = 0; f < frames; f++)
            for (var y = y0; y <= y1; y++)
                for (var x = x0; x <= x1; x++)
                {
                    if ((x - c.X) * (x - c.X) + (y - c.Y) * (y - c.Y) > r2) continue;
                    var i = (oy + y) * host.Width + (strip ? f * fw : ox) + x;
                    if (host.Alpha[i] < 128) continue;
                    opaque++;
                    compete += host.Compete[i];
                }
        return opaque == 0 ? 0f : compete / (float)opaque;
    }

    /// <summary>The thickness of a silhouette on a coarse grid: each cell's chamfer distance to the outline (0 outside).</summary>
    private static float[] Thickness(byte[] alpha, int w, int h, out int gw, out int gh, out int cell, out float max)
    {
        cell = Math.Max(2, Math.Max(w, h) / 96);
        gw = (w + cell - 1) / cell;
        gh = (h + cell - 1) / cell;
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
        max = Chamfer(d, gw, gh);
        return d;
    }

    /// <summary>Turns a grid of inside (MaxValue) and outside (0) cells into each cell's chamfer distance to the outline;
    /// returns the largest.</summary>
    private static float Chamfer(float[] d, int gw, int gh)
    {
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
        return max;
    }

    /// <summary>
    /// The point nearest <paramref name="p"/> (source pixels) where the silhouette is at least <paramref name="share"/>
    /// as thick as at its thickest; <paramref name="p"/> itself when it is already that deep in the body (on a wrist, a
    /// hand or a weapon the curse read as the creature's own magic, "a bracelet").
    /// </summary>
    internal static Vector2 ThickNear(byte[] alpha, int w, int h, Vector2 p, float share)
    {
        var d = Thickness(alpha, w, h, out var gw, out var gh, out var cell, out var max);
        if (max <= 0f) return p;
        var need = share * max;
        int px = (int)(p.X / cell), py = (int)(p.Y / cell);
        if (px >= 0 && py >= 0 && px < gw && py < gh && d[py * gw + px] >= need) return p;
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

    /// <summary>
    /// THE TERRITORIES' SEATS on a silhouette (source pixels of one frame; <paramref name="alpha"/> and
    /// <paramref name="change"/> hold <paramref name="frames"/> frames of <paramref name="w"/> x <paramref name="h"/>, one
    /// after another). Only the STABLE body counts: the cells solid in three quarters of the frames (a claw, a sleeve, a
    /// weapon or a wing that moves never holds a territory). The first, the anchor of the whole state, in the body's
    /// central MASS (thick, far from the extremities, where the host visibly changes, a weak pull toward
    /// <paramref name="first"/>); each next one ELSEWHERE on the body (in its thick parts, a gap clear of every earlier
    /// territory, about a territory and a third away, never far from its centre), on an upright body the third never in
    /// a row with the first two (three in a line drew a band again: "a sash"); none on the head (the authored
    /// <paramref name="heads"/> boxes, else the leading fifth of a wide body or the top of a tall one's mass) or on the
    /// feet. A later territory the body has no room for at its full size takes a smaller diameter (written back into
    /// <paramref name="diameters"/>); fewer seats when the body has no room even then.
    /// </summary>
    /// <summary>The rules (0 strict, 1 gentle, 2 loose) and sizes a later territory tries, in order, when the body has no
    /// room for it whole.</summary>
    private static readonly (int Level, float Scale)[] Attempts =
    {
        (0, 1f), (0, 0.82f), (0, 0.67f), (0, 0.55f), (0, 0.45f),
        (1, 0.82f), (1, 0.67f), (1, 0.55f), (1, 0.45f),
        (2, 0.67f), (2, 0.55f), (2, 0.45f),
    };

    internal static List<Vector2> TerritorySeats(byte[] alpha, int w, int h, Vector2 first, float[] diameters, bool headAtLeft,
                                                 byte[]? change = null, int frames = 1, IReadOnlyList<Vector4>? heads = null)
    {
        var seats = new List<Vector2>();
        var cell = Math.Max(2, Math.Max(w, h) / 96);
        int gw = (w + cell - 1) / cell, gh = (h + cell - 1) / cell;
        // THE STABLE BODY: each cell's count of frames it is solid in
        var solid = new int[gw * gh];
        var visSum = new float[gw * gh];
        var visCount = new int[gw * gh];
        var used = 0;
        for (var f = 0; f < frames; f++)
        {
            var any = false;
            var o = f * w * h;
            for (var cy = 0; cy < gh; cy++)
                for (var cx = 0; cx < gw; cx++)
                {
                    int opaque = 0, all = 0;
                    for (var y = cy * cell; y < Math.Min(h, cy * cell + cell); y++)
                        for (var x = cx * cell; x < Math.Min(w, cx * cell + cell); x++)
                        {
                            all++;
                            var i = o + y * w + x;
                            if (alpha[i] < 128) continue;
                            opaque++;
                            if (change is null) continue;
                            visSum[cy * gw + cx] += change[i];
                            visCount[cy * gw + cx]++;
                        }
                    if (opaque * 2 <= all) continue;
                    solid[cy * gw + cx]++;
                    any = true;
                }
            if (any) used++;
        }
        if (used == 0) return seats;
        var d = new float[gw * gh];
        for (var i = 0; i < d.Length; i++) d[i] = solid[i] >= 0.75f * used ? float.MaxValue : 0f;
        var max = Chamfer(d, gw, gh);
        if (max <= 0f) return seats;
        // WHERE THE HOST VISIBLY CHANGES: each cell's mean change under the drain (0..1 of the host's most). A territory
        // on a dark sash, an inner tunic or trousers barely changed and read as part of the costume ("a pattern on the
        // robe", "a sash"); where the host loses its colour or brightness it reads as damage
        var vis = new float[gw * gh];
        if (change is not null)
        {
            for (var i = 0; i < vis.Length; i++) vis[i] = visCount[i] == 0 ? 0f : visSum[i] / visCount[i];
            // (a territory's worth of neighbourhood, so one bright pixel never wins)
            var smooth = new float[vis.Length];
            var r = Math.Max(1, (int)(diameters[0] * 0.25f / cell));
            for (var cy = 0; cy < gh; cy++)
                for (var cx = 0; cx < gw; cx++)
                {
                    float sum = 0f;
                    var cnt = 0;
                    for (var y = Math.Max(0, cy - r); y <= Math.Min(gh - 1, cy + r); y++)
                        for (var x = Math.Max(0, cx - r); x <= Math.Min(gw - 1, cx + r); x++)
                        {
                            if (d[y * gw + x] <= 0f) continue;
                            sum += vis[y * gw + x];
                            cnt++;
                        }
                    smooth[cy * gw + cx] = cnt == 0 ? 0f : sum / cnt;
                }
            var visMax = 0f;
            foreach (var v in smooth) visMax = Math.Max(visMax, v);
            for (var i = 0; i < vis.Length; i++) vis[i] = visMax > 0f ? smooth[i] / visMax : 0f;
        }
        // the whole stable body (its hem and feet), and its MASS (the cells a fifth as thick as the thickest: no thin
        // sleeve, wing tip, antler or tail stretches it)
        int minX = gw, maxX = -1, minY = gh, maxY = -1, mX0 = gw, mX1 = -1, mY0 = gh, mY1 = -1;
        for (var i = 0; i < d.Length; i++)
        {
            if (d[i] <= 0f) continue;
            int x = i % gw, y = i / gw;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            if (d[i] < 0.2f * max) continue;
            mX0 = Math.Min(mX0, x); mX1 = Math.Max(mX1, x); mY0 = Math.Min(mY0, y); mY1 = Math.Max(mY1, y);
        }
        float bw = maxX - minX + 1, bh = maxY - minY + 1;
        float mh = mY1 - mY0 + 1;
        var wide = bw > bh * 1.05f;
        var column = 0.22f;
        var authored = heads is { Count: > 0 };
        Vector2 At(int i) => new((i % gw + 0.5f) * cell, (i / gw + 0.5f) * cell);
        bool OnHead(int i, float margin)
        {
            var c = At(i);
            foreach (var hb in heads!)
                if (c.X > hb.X - margin && c.X < hb.Z + margin && c.Y > hb.Y - margin && c.Y < hb.W + margin) return true;
            return false;
        }
        // (an upright body's chin: nothing beside the head either, a raised wing, antlers or a shoulder spike)
        var chin = 0f;
        if (authored)
        {
            foreach (var hb in heads!) chin += hb.W;
            chin /= heads.Count;
        }
        // the body's centre (its thickest core, the head left out: a whelp's round head is its thickest part, an angel's
        // wing roots pulled a plain centroid sideways) and its reach (the radius of a disc as large as its mass)
        var centre = Vector2.Zero;
        var reach = 1f;
        {
            double sx = 0, sy = 0, sw = 0;
            var mass = 0;
            for (var i = 0; i < d.Length; i++)
            {
                if (d[i] < 0.2f * max || (authored && OnHead(i, 0f))) continue;
                mass++;
                if (d[i] < 0.5f * max) continue;
                var c = At(i);
                var wgt = (double)d[i] * d[i] * d[i];
                sx += c.X * wgt; sy += c.Y * wgt; sw += wgt;
            }
            centre = sw > 0 ? new Vector2((float)(sx / sw), (float)(sy / sw)) : At(Array.IndexOf(d, max));
            // (never under the first territory's own size: a small body is mostly that territory)
            reach = Math.Max(MathF.Sqrt(Math.Max(1, mass) / MathF.PI) * cell, 0.6f * diameters[0]);
        }
        // an upright body's torso height (from its chin, when it is authored): its column is a share of it
        var torso = authored ? Math.Max(0.5f * mh * cell, (mY1 + 1) * cell - Math.Max(mY0 * cell, chin)) : mh * cell;
        bool Excluded(int x, int y)
        {
            // the hem, the feet, a beast's legs (a territory there read as "a stain on the hem")
            if (y > maxY - (wide ? 0.25f : 0.2f) * bh) return true;
            // the authored HEAD of every frame, and a territory's reach around it (on the face it read as face paint, on
            // a beast's head as its markings)
            if (authored && OnHead(y * gw + x, 0.15f * diameters[0])) return true;
            if (wide) return !authored && (headAtLeft ? x < minX + 0.22f * bw : x > maxX - 0.22f * bw);
            // an upright body: below the head and hood (a territory landed on the Matron's face), and inside its torso's
            // column (on an outstretched sleeve, a wing or a weapon it read as the creature's own magic); the column is
            // a share of the torso's HEIGHT round the thick core's centre (wings, fists and a held staff widened the
            // mass itself, and a column of its width held them)
            return y < mY0 + (authored ? 0.15f : 0.28f) * mh || (authored && (y + 0.5f) * cell < chin)
                   || (column > 0f && MathF.Abs((x + 0.5f) * cell - centre.X) > Math.Max(column * torso, 0.5f * reach));
        }
        // THE FIRST: the anchor of the whole state, so the body's stable central MASS; a hand, a claw, a weapon, a wing,
        // a sleeve or antlers never hold it (on the Matron's claw it read as "a spell it is charging")
        {
            var far = 1f;
            for (var i = 0; i < d.Length; i++)
                if (d[i] >= 0.5f * max && !Excluded(i % gw, i / gw)) far = Math.Max(far, Vector2.Distance(At(i), centre));
            var best = -1;
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < d.Length; i++)
            {
                if (d[i] < 0.5f * max || Excluded(i % gw, i / gw)) continue;
                var c = At(i);
                // (an upright body's first leans to its CHEST: at the hip it took the hand hanging in front of it, the
                // Matron's claw, the Reaper's grip on its scythe)
                var score = 0.45f * d[i] / max + 0.35f * (1f - Vector2.Distance(c, centre) / far)
                            + (change is null ? 0f : 0.5f * vis[i]) - 0.12f * Vector2.Distance(c, first) / diameters[0]
                            + (wide ? 0f : 0.2f * Math.Clamp((centre.Y - c.Y) / reach, -1f, 1f));
                if (score > bestScore) { bestScore = score; best = i; }
            }
            if (best >= 0) seats.Add(At(best));
        }
        if (seats.Count == 0)
        {
            for (var pass = 0; pass < 2 && seats.Count == 0; pass++)
            {
                var need = (pass == 0 ? 0.6f : 0.35f) * max;
                var best = -1;
                var bestD = float.MaxValue;
                for (var i = 0; i < d.Length; i++)
                {
                    if (d[i] < need || Excluded(i % gw, i / gw)) continue;
                    var dd = Vector2.DistanceSquared(At(i), first);
                    if (dd < bestD) { bestD = dd; best = i; }
                }
                if (best >= 0) seats.Add(At(best));
            }
            if (seats.Count == 0) seats.Add(first);
        }
        // THE OTHERS: elsewhere on the body, in its mass (reaching for the gap alone, they landed on a wing, a fist, a
        // hand on a staff). A body too small or too slim for a territory of its full size holds a smaller one (a slim
        // angel's second territory went to its wing, a small whelp held only one); a THIN body (a skeleton's ribs and
        // pelvis) that still cannot hold three gets a gentler pass (thinner parts, a smaller gap, a wider column), so
        // depth 3 always shows more of the body than depth 2. The second is chosen so that a third still fits (a second
        // straight above the first left no room for a third off their line)
        for (var k = 1; k < Math.Min(diameters.Length, MaxTerritories) && seats.Count == k; k++)
            if (!Place(k, k + 1 < Math.Min(diameters.Length, 3))) break;
        return seats;

        // the best cell for territory k under the strict (0), gentle (1) or loose (2: anywhere thick enough on the body,
        // a small or sideways body's last resort) rules (-1: none); every candidate when asked
        int Search(int k, int level, List<(int I, float Score)>? all)
        {
            var minThick = level == 0 ? 0.4f : 0.22f;
            var minGap = level == 0 ? 0.95f : 0.8f;
            var minArea = level == 0 ? 0.1f : 0.06f;
            var maxOut = level == 0 ? 1.3f : level == 1 ? 1.45f : 1.7f;
            column = level == 0 ? 0.22f : level == 1 ? 0.26f : 0f;
            var best = -1;
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < d.Length; i++)
            {
                if (d[i] < minThick * max || Excluded(i % gw, i / gw)) continue;
                var c = At(i);
                var out_ = Vector2.Distance(c, centre) / reach;
                if (out_ > maxOut) continue;
                var ok = true;
                var near = float.MaxValue;
                for (var j = 0; j < seats.Count; j++)
                {
                    var gap = Vector2.Distance(c, seats[j]) / (0.5f * (diameters[j] + diameters[k]));
                    if (gap < minGap) { ok = false; break; }
                    near = Math.Min(near, gap);
                }
                if (!ok) continue;
                if (seats.Count == 2 && !wide)
                {
                    // never three in a row on an upright body: the triangle's area against its longest side, squared
                    // (on a wide beast the body itself is the row; three in a line down a torso drew "a sash")
                    var a0 = seats[0];
                    var b0 = seats[1];
                    var area = MathF.Abs((b0.X - a0.X) * (c.Y - a0.Y) - (b0.Y - a0.Y) * (c.X - a0.X)) * 0.5f;
                    var side = MathF.Max(Vector2.DistanceSquared(a0, b0), MathF.Max(Vector2.DistanceSquared(a0, c), Vector2.DistanceSquared(b0, c)));
                    if (area / side < minArea) continue;
                }
                var score = 0.5f * d[i] / max - 0.5f * MathF.Abs(near - 1.3f) + 0.6f * vis[i] - 0.9f * out_;
                all?.Add((i, score));
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        // seats territory k (smaller when it must; the gentle and loose rules only up to the third, and smaller); with a
        // look-ahead, only where the next one still fits, by the strictest rules it can
        bool Place(int k, bool lookahead, int maxLevel = 2)
        {
            var full = diameters[k];
            int fallback = -1;
            float fallbackSize = full;
            foreach (var (level, scale) in Attempts)
            {
                if (level > maxLevel || (level > 0 && k >= 3)) break;
                diameters[k] = full * scale;
                if (!lookahead)
                {
                    var b = Search(k, level, null);
                    if (b < 0) continue;
                    seats.Add(At(b));
                    return true;
                }
                var all = new List<(int I, float Score)>();
                Search(k, level, all);
                if (all.Count == 0) continue;
                all.Sort((x, y) => y.Score.CompareTo(x.Score));
                if (fallback < 0) { fallback = all[0].I; fallbackSize = diameters[k]; }
                var next = diameters[k + 1];
                for (var nextLevel = 1; nextLevel <= 2; nextLevel++)
                    for (var n = 0; n < Math.Min(30, all.Count); n++)
                    {
                        seats.Add(At(all[n].I));
                        var fits = Place(k + 1, false, nextLevel);
                        diameters[k + 1] = next;
                        if (fits) seats.RemoveAt(seats.Count - 1);
                        if (fits)
                        {
                            return true;
                        }
                        seats.RemoveAt(seats.Count - 1);
                    }
            }
            if (fallback < 0)
            {
                diameters[k] = full;
                return false;
            }
            diameters[k] = fallbackSize;
            seats.Add(At(fallback));
            return true;
        }
    }

    // ── WHEN EACH TERRITORY BLOOMS ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A step of the drawn stage on one carried segment: at <c>At</c> it went from <c>From</c> to <c>To</c>.</summary>
    internal readonly record struct StageStep(float At, int From, int To);

    /// <summary>
    /// Each territory's own bloom at one moment: when it was born, how long it grows, how (1 apply, 2 arrival, 3 deepen);
    /// the last deepen (its old territories react, a shadow travels to the new) and the latest bloom's start.
    /// </summary>
    internal sealed class TerritoryClock
    {
        public readonly float[] Born = new float[MaxTerritories];
        public readonly float[] Grow = new float[MaxTerritories];
        public readonly byte[] Kind = new byte[MaxTerritories];
        /// <summary>The event (apply, arrival, deepen step) each territory was born of.</summary>
        public readonly float[] EventOf = new float[MaxTerritories];
        public int Count;
        public float DeepenAt = float.NegativeInfinity;
        public int DeepenBefore, DeepenAfter;
        public float EventAt = float.NegativeInfinity;

        /// <summary>How far territory <paramref name="k"/> has bloomed at <paramref name="t"/> (0..1).</summary>
        public float Reveal(int k, float t) => Smooth(Math.Clamp((t - Born[k]) / Grow[k], 0f, 1f));

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
    /// THE TERRITORIES' TIMELINE, replayed to <paramref name="t"/>: the segment's first bloom (an apply's territory, an
    /// arrival's every territory one after another), then each stage step in order. A step up blooms EVERY newly shown
    /// territory, one after another (a two-step catch-up popped one in without its bloom); a step down hides the ones no
    /// longer shown. Each territory keeps its own clock, so blooms that overlap never fight (a deepen landing during an
    /// arrival made the arrival's territories flicker between the two flares).
    /// </summary>
    internal static void Replay(float start, byte kind, int stage0, IReadOnlyList<StageStep> steps, float t, int available, TerritoryClock c)
    {
        var count = Math.Min(available, TerritoriesAt(stage0));
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
        for (var s = 0; s < steps.Count; s++)
        {
            var step = steps[s];
            if (step.At > t) break;
            var next = Math.Min(available, TerritoriesAt(step.To));
            if (next > count)
            {
                for (var k = count; k < next; k++)
                {
                    c.Born[k] = step.At + TravelMs + (k - count) * DeepenStaggerMs;
                    c.Grow[k] = DeepenGrowMs;
                    c.Kind[k] = 3;
                    c.EventOf[k] = step.At;
                }
                c.DeepenAt = step.At;
                c.DeepenBefore = count;
                c.DeepenAfter = next;
                c.EventAt = step.At;
            }
            count = next;
        }
        c.Count = count;
    }

    /// <summary>One slot's scanned segment: where it began, how, and every stage step found so far.</summary>
    private sealed class Timeline
    {
        public float Start = float.NaN;
        public byte Kind;
        public int Stage0, StageAt;
        public long Scanned;
        public readonly List<StageStep> Steps = new();
    }

    private Timeline?[]? _timeline;
    private TerritoryClock?[]? _clock;

    /// <summary>The steps' lattice: a stage step is found to this, from the segment's own start (never the frame's time).</summary>
    private const float LatticeMs = 0.25f;

    /// <summary>The scan's stride on the lattice (50 ms).</summary>
    private const long ScanStride = 200;

    private int StageOn(int slot, float t) => Math.Max(1, Mark.DrawnStage(slot, t));

    /// <summary>
    /// This slot's territories at <paramref name="t"/> (Count 0 while it carries no bloom: waiting, or nothing). The
    /// segment's stage steps are scanned once, forward, on a lattice anchored at the segment's start, each step bisected to
    /// its first lattice point: the same steps whatever the frame times or the order of the frames asked.
    /// </summary>
    private TerritoryClock ClockAt(int slot, float t, int available)
    {
        var c = (_clock ??= new TerritoryClock?[_kind.Length])[slot] ??= new TerritoryClock();
        var phase = Mark.TryPhase(slot, t, out var u);
        if (phase is not (MarkPhase.Apply or MarkPhase.Reform))
        {
            c.Count = 0;
            return c;
        }
        var tl = (_timeline ??= new Timeline?[_kind.Length])[slot] ??= new Timeline();
        var start = t - u;
        var kind = phase == MarkPhase.Apply ? (byte)1 : (byte)2;
        if (float.IsNaN(tl.Start) || MathF.Abs(tl.Start - start) > 1f || tl.Kind != kind)
        {
            tl.Start = start;
            tl.Kind = kind;
            tl.Stage0 = tl.StageAt = StageOn(slot, start);
            tl.Scanned = 0;
            tl.Steps.Clear();
        }
        var n = (long)MathF.Floor((t - tl.Start) / LatticeMs);
        var x = tl.Scanned;
        while (x < n)
        {
            var next = Math.Min(x + ScanStride, n);
            if (StageOn(slot, tl.Start + next * LatticeMs) == tl.StageAt)
            {
                x = next;
                continue;
            }
            long lo = x, hi = next;
            while (hi - lo > 1)
            {
                var mid = (lo + hi) / 2;
                if (StageOn(slot, tl.Start + mid * LatticeMs) == tl.StageAt) lo = mid;
                else hi = mid;
            }
            var to = StageOn(slot, tl.Start + hi * LatticeMs);
            tl.Steps.Add(new StageStep(tl.Start + hi * LatticeMs, tl.StageAt, to));
            tl.StageAt = to;
            x = hi;
        }
        if (n > tl.Scanned) tl.Scanned = n;
        Replay(tl.Start, tl.Kind, tl.Stage0, tl.Steps, t, available, c);
        return c;
    }

    // ── DRAWING ────────────────────────────────────────────────────────────────────────────────────────────────────────

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

    /// <summary>A territory's variant, turn and mirror on this slot (a row of one creature is never a stamp).</summary>
    private static (int Variant, float Turn, bool Flip) Look(int slot, int k)
        => ((k + slot) % TerritoryAtlas.Variants, (Hash(slot * 31 + k * 7) - 0.5f) * 1.6f, Hash(slot * 17 + k * 13) > 0.5f);

    private void T(SpriteBatch b, Texture2D tex, Vector2 centre, float side, float turn, bool flip, Color c)
    {
        if (c.A == 0 && c.R == 0 && c.G == 0 && c.B == 0) return;
        var half = TerritoryAtlas.N * 0.5f;
        b.Draw(tex, centre, null, c, turn, new Vector2(half, half), side / TerritoryAtlas.N, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        SpriteCount++;
    }

    /// <summary>The territory's field side on screen: its texture is wider than the region it holds.</summary>
    private static float FieldSide(float diameter) => diameter / TerritoryAtlas.Fill;

    private void DrawCorruptionGlow(SpriteBatch b, int slot, float playheadMs, float t, Rectangle body, Vector2 anchor, RasterizerState raster,
                                    SpriteFrame frame)
    {
        var device = b.GraphicsDevice;
        var atlas = Territories(device);
        var lay = _layout?[slot];
        var host = _hostOf?[slot];
        var inside = _inside[slot];
        if (_kind[slot] == 1 || lay is null || host is null)
        {
            b.Begin(SpriteSortMode.Deferred, Stain, SamplerState.LinearClamp, inside, raster);
            Fill(b, body, Color.Black * 0.05f);   // waiting: the curse is on its way, a faint shadow only
            b.End();
            return;
        }
        var look = host.Look;
        var map = new FrameMap(frame);
        var clock = ClockAt(slot, playheadMs, lay.Available);
        var count = clock.Count;
        if (count == 0) return;
        var light = LightFor(look.Luma);
        var deep = 0.5f * PaleHost(look.Luma);
        var stage = _stage[slot];
        // (beside an action the flare's extras are quieter until the LAST bloom has settled: ended at a fixed 700 ms, a
        // late territory's bloom and afterglow jumped up in one frame)
        var sinceEvent = playheadMs - clock.EventAt;
        var quietUntil = Math.Max(clock.EventAt + FlareMs, clock.GrownAt + SettleMs);
        var quiet = sinceEvent >= 0f && playheadMs < quietUntil && Mark.TickQuietNear(clock.EventAt, 450f) ? QuietShare : 1f;
        // (each territory's bloom keeps the quiet of the event that made it: one shared value re-scaled an arrival's
        // blooming fronts in one frame when a deepen with another quiet landed on it)
        Span<float> quietOf = stackalloc float[MaxTerritories];
        for (var k = 0; k < count; k++)
            quietOf[k] = playheadMs < clock.Born[k] + clock.Grow[k] + SettleMs && Mark.TickQuietNear(clock.EventOf[k], 450f) ? QuietShare : 1f;
        var deepenQuiet = float.IsNegativeInfinity(clock.DeepenAt) || !Mark.TickQuietNear(clock.DeepenAt, 450f) ? 1f : QuietShare;
        Span<float> reveal = stackalloc float[MaxTerritories];
        Span<Vector2> at = stackalloc Vector2[MaxTerritories];
        Span<float> side = stackalloc float[MaxTerritories];
        Span<Mat> mat = stackalloc Mat[MaxTerritories];
        for (var k = 0; k < count; k++)
        {
            reveal[k] = clock.Reveal(k, playheadMs);
            at[k] = map.At(lay, k, anchor);
            side[k] = FieldSide(map.Diameter(lay, k));
            mat[k] = new Mat(look, lay.Burn[k]);
        }

        // ── THE HOST'S OWN MATERIAL CHANGES: the drained copy of its frame, through each territory's soft mask (burned:
        // its hard-edged burn) ──
        for (var k = 0; k < count; k++)
        {
            if (reveal[k] <= 0f) continue;
            var (v, turn, flip) = Look(slot, k);
            DrainTerritory(device, atlas, frame, host, v, reveal[k], at[k], side[k], turn, flip,
                           DrainStrength(look, mat[k].Burn) * Math.Min(1f, reveal[k] * 3f), mat[k].Burn, _tint?[slot] ?? Color.White, raster);
        }

        // ── THE INFECTED TISSUE, stained into the host; on an ash-burned host its burnt rim ──
        b.Begin(SpriteSortMode.Deferred, Stain, SamplerState.LinearClamp, inside, raster);
        // a faint violet-grey loss over the whole host, a little more with each depth (the territories lead)
        Fill(b, body, AfflictedShade * ((0.08f + 0.04f * stage) * (1f - 0.7f * host.Burn)));
        for (var k = 0; k < count; k++)
        {
            if (reveal[k] <= 0f) continue;
            var (v, turn, flip) = Look(slot, k);
            // the contaminated tissue moves very gently (a slow swell, each territory at its own moment)
            var swell = 1f + 0.022f * MathF.Sin(t * MathF.Tau / 3400f + k * 1.9f);
            var s = side[k] * swell;
            var grow = reveal[k] < 1f;
            var f = GrowFrame(reveal[k]);
            var tissue = grow ? atlas.TissueGrow[v * TerritoryAtlas.Frames + f] : atlas.Tissue[v];
            // (on a dark host the tissue only crusts the ash, mottled: multiplied whole it was black again, left out
            // the ash was smooth, "a hole")
            T(b, tissue, at[k], s, turn, flip, mat[k].Bruise * mat[k].Tissue);
            if (deep > 0f) T(b, tissue, at[k], s, turn, flip, mat[k].Bruise * (TissueShare * deep));
            T(b, grow ? atlas.DarkGrow[v * TerritoryAtlas.Frames + f] : atlas.Dark[v], at[k], s, turn, flip, mat[k].Vein);
            if (mat[k].Burn > 0f)
                T(b, grow ? atlas.EdgeGrow[v * TerritoryAtlas.Frames + f] : atlas.Edge[v], at[k], s, turn, flip,
                  BurnEdge * (BurnEdgeShare * mat[k].Burn));
        }
        b.End();

        // ── THE LIGHT FROM INSIDE, screened into the host ──
        b.Begin(SpriteSortMode.Deferred, Screen, SamplerState.LinearClamp, inside, raster);
        var settleAge = playheadMs - clock.GrownAt;
        var settle = settleAge < 0f ? 0f : settleAge >= SettleMs ? 1f : settleAge / SettleMs;
        var afterglow = settleAge >= 0f && settleAge < SettleMs ? 0.5f * (1f - settle) * quiet : 0f;
        var source = SourceGlow(slot, playheadMs);
        var reactAge = playheadMs - clock.DeepenAt;
        for (var k = 0; k < count; k++)
        {
            if (reveal[k] <= 0f) continue;
            var (v, turn, flip) = Look(slot, k);
            var m = mat[k];
            // each territory breathes at its own pace; the old ones REACT to a deepen
            var breath = 1f + 0.14f * MathF.Sin(t * MathF.Tau / (4300f + 700f * k) + k * 2.3f);
            var react = k < clock.DeepenBefore ? 0.7f * ReactEnvelope(reactAge) * deepenQuiet : 0f;
            var lit = reveal[k] * reveal[k];
            // (a host already rich in violet, or dark, gets a RESTRAINED light; an ash-burned one a pale spectral leak)
            var level = IdleEmission * m.Light * breath * (1f + afterglow + 0.9f * source + react) * lit;
            if (m.Glow > 0f) T(b, atlas.Tissue[v], at[k], side[k], turn, flip, TissueGlow * (m.Glow * breath * lit));
            // (above 1 the light is split in two draws: Color x 1.3 clamped per channel and turned the violet pink)
            // (burned, the soft light gives way to the fissures' own, added below: screened onto the ash, a pale soft
            // core read as fog and the fissures were no brighter than the ash round them)
            var soft = 1f - m.Burn;
            T(b, atlas.Emit[v], at[k], side[k], turn, flip, m.Emission * (Math.Min(1f, level) * soft));
            if (level > 1f) T(b, atlas.Emit[v], at[k], side[k], turn, flip, m.Emission * (Math.Min(1f, level - 1f) * soft));
            // one section of the territory at a time gently answers (a slow internal change)
            var cycle = 3900f + 500f * k;
            var n = (int)MathF.Floor((t + k * 1300f) / cycle);
            var ph = (t + k * 1300f - n * cycle) / cycle;
            var answer = MathF.Sin(ph * MathF.PI);
            var g = ((n % TerritoryAtlas.IdleGroups) + TerritoryAtlas.IdleGroups) % TerritoryAtlas.IdleGroups;
            T(b, atlas.Idle[v * TerritoryAtlas.IdleGroups + g], at[k], side[k], turn, flip,
              m.Accent * (0.3f * m.Light * answer * answer * lit * settle));
        }
        b.End();

        // ── ASH-BURN: the PALE SPECTRAL FISSURES, added, so they glow out of the ash and its charred cracks ──
        var burned = false;
        for (var k = 0; k < count; k++) burned |= mat[k].Burn > 0f && reveal[k] > 0f;
        if (burned)
        {
            b.Begin(SpriteSortMode.Deferred, VfxBlend.PremultipliedAdditive, SamplerState.LinearClamp, inside, raster);
            for (var k = 0; k < count; k++)
            {
                var m = mat[k];
                if (m.Burn <= 0f || reveal[k] <= 0f) continue;
                var (v, turn, flip) = Look(slot, k);
                var breath = 1f + 0.14f * MathF.Sin(t * MathF.Tau / (4300f + 700f * k) + k * 2.3f);
                var react = k < clock.DeepenBefore ? 0.7f * ReactEnvelope(reactAge) * deepenQuiet : 0f;
                var lit = reveal[k] * reveal[k];
                var level = FissureLight * m.Light * breath * (1f + afterglow + 0.9f * source + react) * lit * m.Burn;
                T(b, atlas.Fissure[v], at[k], side[k], turn, flip, m.Emission * Math.Min(1f, level));
            }
            b.End();
        }

        // ── THE BLOOM: a growth phrase through each blooming territory; a deepen's shadow under the skin ──
        var blooming = false;
        for (var k = 0; k < count; k++) blooming |= reveal[k] > 0f && reveal[k] < 1f;
        var newOnes = clock.DeepenAfter - clock.DeepenBefore;
        var travel = clock.DeepenBefore > 0 && newOnes > 0 && reactAge >= 0f && reactAge < TravelMs * 1.5f + (newOnes - 1) * DeepenStaggerMs + 20f;
        if (!blooming && !travel) return;
        b.Begin(SpriteSortMode.Deferred, VfxBlend.PremultipliedAdditive, SamplerState.LinearClamp, inside, raster);
        for (var k = 0; k < count; k++)
        {
            if (reveal[k] <= 0f || reveal[k] >= 1f) continue;
            var (v, turn, flip) = Look(slot, k);
            var peak = FrontPeak * light * quietOf[k] * (clock.Kind[k] == 2 ? ArrivalShare : 1f);
            var kindle = Math.Min(1f, reveal[k] * 5f);
            T(b, atlas.FrontGrow[v * TerritoryAtlas.Frames + GrowFrame(reveal[k])], at[k], side[k], turn, flip,
              mat[k].Hot * (peak * kindle * (1f - 0.6f * reveal[k])));
        }
        if (travel)
        {
            // the curse TRAVELS under the skin from the nearest infected territory to the new one, then is gone
            for (var k = clock.DeepenBefore; k < Math.Min(count, clock.DeepenAfter); k++)
            {
                var from = at[0];
                for (var j = 1; j < clock.DeepenBefore; j++)
                    if (Vector2.DistanceSquared(at[j], at[k]) < Vector2.DistanceSquared(from, at[k])) from = at[j];
                for (var i = 0; i < 4; i++)
                {
                    var q = (reactAge - (k - clock.DeepenBefore) * DeepenStaggerMs) / TravelMs - i * 0.14f;
                    if (q < 0f || q > 1f) continue;
                    var p = Vector2.Lerp(from, at[k], Smooth(q));
                    var size = map.Diameter(lay, k) * (0.22f - 0.03f * i);
                    Puff(b, p, size, mat[k].Hot * (0.32f * deepenQuiet * MathF.Sin(q * MathF.PI) * (1f - 0.2f * i)));
                }
            }
        }
        b.End();
    }

    /// <summary>
    /// One territory's drained material, its own Ash-Burn share <paramref name="burn"/> of each copy: the Shadow Violet
    /// copy through the soft drain mask, the Ash-Burn copy through the hard-edged burn mask. Two "over" draws do not add
    /// their coverage, so the first is raised to s(1-b)/(1-sb): where the masks meet, the host keeps exactly 1-s and the
    /// copies take s(1-b) and sb (drawn at s(1-b) and sb, a half-burned territory drained far less than either mode).
    /// </summary>
    private void DrainTerritory(GraphicsDevice device, TerritoryAtlas atlas, SpriteFrame frame, HostEntry host, int v, float reveal,
                                Vector2 at, float side, float turn, bool flip, float strength, float burn, Color tint, RasterizerState raster)
    {
        var whole = reveal >= 1f;
        var f = v * TerritoryAtlas.Frames + GrowFrame(reveal);
        var s = Math.Clamp(strength, 0f, 1f);
        if (burn < 1f)
            DrainQuad(device, frame, host.Drained, whole ? atlas.Drain[v] : atlas.DrainGrow[f], at, side, turn, flip,
                      s * (1f - burn) / Math.Max(1e-3f, 1f - s * burn), tint, raster);
        if (burn > 0f)
            DrainQuad(device, frame, host.Burned, whole ? atlas.Burn[v] : atlas.BurnGrow[f], at, side, turn, flip, s * burn, tint, raster);
    }

    /// <summary>A deepen's reaction in the territories already infected: a quick swell of their light, then back.</summary>
    private static float ReactEnvelope(float age)
    {
        if (age < 0f) return 0f;
        if (age < 80f) return age / 80f;
        var f = 1f - (age - 80f) / 420f;
        return f <= 0f ? 0f : f * f;
    }

    private static int GrowFrame(float reveal)
        => Math.Clamp((int)MathF.Ceiling(reveal * TerritoryAtlas.Frames) - 1, 0, TerritoryAtlas.Frames - 1);

    /// <summary>
    /// A dying host's curse: its territories flare once, then collapse, drain away and smoke out, in the SAME material
    /// it wore alive (the tissue's share, the body's shade, the restrained light, the ash-burn) and from what it WORE (a
    /// host that fell mid-bloom collapses from its bloom).
    /// </summary>
    private void DrawCorruptionLeaving(SpriteBatch b, int slot, int stage, float age, float died, Vector2 anchor, Rectangle body,
                                       DepthStencilState inside, RasterizerState raster, SpriteFrame frame, Color tint)
    {
        var lay = _layout?[slot];
        if (lay is null) return;
        var device = b.GraphicsDevice;
        var atlas = Territories(device);
        var host = Host(device, frame.Texture, _restOf?[slot]);
        var look = _hostOf?[slot]?.Look ?? host.Look;
        var burn = _hostOf?[slot]?.Burn ?? host.Burn;
        var map = new FrameMap(frame);
        var deep = 0.5f * PaleHost(look.Luma);
        const float flash = 150f, collapse = 600f;
        var p = age < flash ? 0f : Math.Min(1f, (age - flash) / collapse);
        var fall = 1f - Smooth(p);
        if (fall <= 0.01f) return;
        // what it wore the moment before it fell, on each territory's own clock
        var clock = ClockAt(slot, died - 1f, lay.Available);
        var count = clock.Count;
        Span<float> wore = stackalloc float[MaxTerritories];
        Span<Vector2> at = stackalloc Vector2[MaxTerritories];
        Span<float> side = stackalloc float[MaxTerritories];
        Span<Mat> mat = stackalloc Mat[MaxTerritories];
        for (var k = 0; k < count; k++)
        {
            wore[k] = clock.Reveal(k, died - 1f);
            at[k] = map.At(lay, k, anchor);
            side[k] = FieldSide(map.Diameter(lay, k));
            mat[k] = new Mat(look, lay.Burn[k]);
        }
        for (var k = 0; k < count; k++)
        {
            var left = wore[k] * fall;
            if (left <= 0.01f) continue;
            var (v, turn, flip) = Look(slot, k);
            DrainTerritory(device, atlas, frame, host, v, left, at[k], side[k], turn, flip, DrainStrength(look, mat[k].Burn) * left,
                           mat[k].Burn, tint, raster);
        }
        b.Begin(SpriteSortMode.Deferred, Stain, SamplerState.LinearClamp, inside, raster);
        Fill(b, body, AfflictedShade * ((0.08f + 0.04f * stage) * fall * (1f - 0.7f * burn)));
        for (var k = 0; k < count; k++)
        {
            var left = wore[k] * fall;
            if (left <= 0.01f) continue;
            var (v, turn, flip) = Look(slot, k);
            var f = GrowFrame(left);
            var whole = left >= 1f;
            var tissue = whole ? atlas.Tissue[v] : atlas.TissueGrow[v * TerritoryAtlas.Frames + f];
            T(b, tissue, at[k], side[k], turn, flip, mat[k].Bruise * mat[k].Tissue);
            if (deep > 0f) T(b, tissue, at[k], side[k], turn, flip, mat[k].Bruise * (TissueShare * deep));
            T(b, whole ? atlas.Dark[v] : atlas.DarkGrow[v * TerritoryAtlas.Frames + f], at[k], side[k], turn, flip, mat[k].Vein);
            if (mat[k].Burn > 0f)
                T(b, whole ? atlas.Edge[v] : atlas.EdgeGrow[v * TerritoryAtlas.Frames + f], at[k], side[k], turn, flip,
                  BurnEdge * (BurnEdgeShare * mat[k].Burn));
        }
        b.End();
        b.Begin(SpriteSortMode.Deferred, VfxBlend.PremultipliedAdditive, SamplerState.LinearClamp, inside, raster);
        for (var k = 0; k < count; k++)
        {
            var left = wore[k] * fall;
            if (left <= 0.01f) continue;
            var (v, turn, flip) = Look(slot, k);
            if (age < flash)
                T(b, mat[k].Burn >= 0.5f ? atlas.Fissure[v] : atlas.Emit[v], at[k], side[k], turn, flip,
                  mat[k].Emission * (0.6f * mat[k].Light * wore[k] * MathF.Sin(age / flash * MathF.PI)));
            else
                T(b, atlas.FrontGrow[v * TerritoryAtlas.Frames + GrowFrame(left)], at[k], side[k], turn, flip,
                  mat[k].Hot * (FrontPeak * mat[k].Light * 0.7f * left));
        }
        b.End();
    }

    /// <summary>A falling host's territories smoking out (drawn outside the body): a few wisps from each it WORE (a
    /// territory still on its way when it fell never smokes), rising, gone.</summary>
    private void DrawCorruptionSmokeOut(SpriteBatch b, int slot, float died, float p, Vector2 anchor, Rectangle body, float fade, SpriteFrame frame)
    {
        var lay = _layout?[slot];
        if (lay is null) return;
        var map = new FrameMap(frame);
        var look = _hostOf?[slot]?.Look ?? default;
        var clock = ClockAt(slot, died - 1f, lay.Available);
        for (var k = 0; k < clock.Count; k++)
        {
            var wore = clock.Reveal(k, died - 1f);
            if (wore <= 0.01f) continue;
            var smoke = new Mat(look, lay.Burn[k]).Smoke * wore;
            for (var i = 0; i < 2; i++)
            {
                var q = Math.Clamp(p * 1.4f - i * 0.15f - k * 0.05f, 0f, 1f);
                if (q <= 0f) continue;
                var from = map.At(lay, k, anchor);
                var to = from + new Vector2(MathF.Sin(k * 1.7f + i) * 10f, -q * body.Height * 0.3f);
                var size = map.Diameter(lay, k) * (0.3f + 0.25f * q);
                Streak(b, to, size * 0.6f, size * 1.3f, smoke * (0.55f * (1f - q) * fade));
            }
        }
    }

    private void Streak(SpriteBatch b, Vector2 at, float w, float h, Color c)
    {
        if (c.A == 0) return;
        b.Draw(_puff, new Rectangle((int)(at.X - w / 2), (int)(at.Y - h / 2), (int)w, (int)h), c);
        SpriteCount++;
    }

    /// <summary>
    /// THE CURSE LEAKS OUT OF ITS TERRITORIES: now and then a slow wisp of Shadow leaves one infected territory, rises
    /// past the silhouette and is gone in about a second and a half (cloth, armour, tattoos and a creature's own trim
    /// cannot leak smoke); two more burst from each territory as it blooms, on that territory's own clock. Never an aura:
    /// one territory at a time, mostly.
    /// </summary>
    private void DrawCorruptionWisps(SpriteBatch b, int slot, float playheadMs, float t, Rectangle body, Vector2 anchor, RasterizerState raster,
                                     SpriteFrame frame)
    {
        if (_kind[slot] != 2) return;
        var lay = _layout?[slot];
        var host = _hostOf?[slot];
        if (lay is null || host is null) return;
        var map = new FrameMap(frame);
        var clock = ClockAt(slot, playheadMs, lay.Available);
        var count = clock.Count;
        if (count == 0) return;
        var rise = Math.Clamp(body.Height * 0.17f, 30f, 80f);
        var drift = Math.Clamp(body.Width * 0.18f, 16f, 60f);
        const float period = 2800f, life = 1500f, burstLife = 950f;
        for (var pass = 0; pass < 2; pass++)
        {
            b.Begin(SpriteSortMode.Deferred, pass == 0 ? BlendState.AlphaBlend : Screen, SamplerState.LinearClamp, DepthStencilState.None, raster);
            for (var k = 0; k < count; k++)
            {
                var m = new Mat(host.Look, lay.Burn[k]);
                var reveal = clock.Reveal(k, playheadMs);
                var from = map.At(lay, k, anchor);
                var size = Math.Clamp(map.Diameter(lay, k) * 0.36f, 20f, 58f);
                var outward = from.X >= anchor.X ? 1f : -1f;
                // the resting leak: one wisp from this territory every few seconds, each territory at its own moment;
                // it fades in with its territory's bloom (switched on at the bloom's end, it popped in mid-flight)
                var w0 = ((t + Hash(slot * 13 + k * 29) * period) % period + period) % period;
                if (reveal > 0f && w0 < life) Wisp(b, pass, from, w0 / life, size, rise, drift * outward, k, m.Smoke, m.Emission, reveal);
                // the bloom's burst: two wisps escape as the territory takes hold (its own clock: never cut mid-flight)
                var start = clock.Born[k] + 0.45f * clock.Grow[k];
                for (var i = 0; i < 2; i++)
                {
                    var ba = playheadMs - start - i * 160f;
                    if (ba < 0f || ba > burstLife) continue;
                    Wisp(b, pass, from, ba / burstLife, size * 0.9f, rise * 1.1f, drift * (i == 0 ? outward : -outward) * 0.8f, k + 5 * i, m.Smoke, m.Emission,
                         clock.Kind[k] == 2 ? ArrivalShare : 1f);
                }
            }
            b.End();
        }
    }

    private void Wisp(SpriteBatch b, int pass, Vector2 from, float q, float size, float rise, float drift, int seed, Color smoke, Color emission, float share)
    {
        var env = MathF.Pow(MathF.Sin(q * MathF.PI), 1.2f) * (1f - 0.4f * q) * share;
        var sway = MathF.Sin(q * 3.1f + seed * 1.7f) * size * 0.3f;
        var p = from + new Vector2(sway + drift * q * q, -rise * q);
        var sz = size * (0.6f + 0.7f * q);
        // a RISING STREAK of Shadow smoke with a faint core of the territory's light (round puffs read as "orbs"; black
        // smoke vanished)
        if (pass == 0)
            Streak(b, p, sz * 0.62f, sz * 1.5f, smoke * (0.62f * env));
        else
            Streak(b, p + new Vector2(0f, sz * 0.12f), sz * 0.36f, sz * 0.95f, emission * (0.5f * env * (1f - 0.5f * q)));
    }

    // ── THE HOST MATERIAL PASS (a stock DualTextureEffect: the drained copy through a territory's mask) ────────────────

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly struct VertexDual : IVertexType
    {
        public readonly Vector3 Position;
        public readonly Vector2 Uv;
        public readonly Vector2 Uv2;

        public VertexDual(Vector3 position, Vector2 uv, Vector2 uv2)
        {
            Position = position;
            Uv = uv;
            Uv2 = uv2;
        }

        public static readonly VertexDeclaration Declaration = new(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(20, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1));

        VertexDeclaration IVertexType.VertexDeclaration => Declaration;
    }

    private DualTextureEffect? _dual;
    private readonly VertexDual[] _quad = new VertexDual[4];
    private static readonly RasterizerState QuadRaster = new() { CullMode = CullMode.None };
    private static readonly RasterizerState QuadRasterScissor = new() { CullMode = CullMode.None, ScissorTestEnable = true };

    /// <summary>
    /// The host's DRAINED material over one territory: the drained copy of the host's current frame, its alpha the
    /// territory's soft mask (DualTextureEffect: rgb = 2 x drained x mask x 0.5, alpha = drained.a x mask.a x strength,
    /// premultiplied), over the host. The quad is the territory's bounds within the frame, so the strip's neighbouring
    /// frames are never sampled.
    /// </summary>
    private void DrainQuad(GraphicsDevice d, SpriteFrame frame, Texture2D drained, Texture2D mask, Vector2 centre, float side, float turn, bool flipMask,
                           float strength, Color tint, RasterizerState raster)
    {
        if (strength <= 0.004f || tint.A == 0) return;
        var ext = side * 0.5f * (MathF.Abs(MathF.Cos(turn)) + MathF.Abs(MathF.Sin(turn)));
        var box = new Rectangle((int)(centre.X - ext), (int)(centre.Y - ext), (int)(2f * ext) + 2, (int)(2f * ext) + 2);
        var r = Rectangle.Intersect(box, frame.Dest);
        if (r.Width <= 0 || r.Height <= 0) return;
        var dual = _dual ??= new DualTextureEffect(d);
        var vp = d.Viewport;
        dual.World = Matrix.Identity;
        dual.View = Matrix.Identity;
        dual.Projection = Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0, 1);
        dual.Texture = drained;
        dual.Texture2 = mask;
        // (the creature's draw colour, premultiplied: unpremultiplied into the diffuse, its alpha into the strength)
        var ta = tint.A / 255f;
        dual.DiffuseColor = new Vector3(tint.R / 255f / ta, tint.G / 255f / ta, tint.B / 255f / ta) * 0.5f;
        dual.Alpha = Math.Min(1f, strength * ta);
        var flipX = (frame.Effects & SpriteEffects.FlipHorizontally) != 0;
        var flipY = (frame.Effects & SpriteEffects.FlipVertically) != 0;
        var cos = MathF.Cos(-turn);
        var sin = MathF.Sin(-turn);
        var dest = frame.Dest;
        var srcR = frame.Src;
        float tw = drained.Width, th = drained.Height;
        VertexDual Corner(float x, float y)
        {
            var lx = (x - dest.X) / dest.Width;
            var ly = (y - dest.Y) / dest.Height;
            if (flipX) lx = 1f - lx;
            if (flipY) ly = 1f - ly;
            var uv = new Vector2((srcR.X + lx * srcR.Width) / tw, (srcR.Y + ly * srcR.Height) / th);
            var dx = x - centre.X;
            var dy = y - centre.Y;
            var mx = (dx * cos - dy * sin) / side + 0.5f;
            var my = (dx * sin + dy * cos) / side + 0.5f;
            if (flipMask) mx = 1f - mx;
            return new VertexDual(new Vector3(x, y, 0f), uv, new Vector2(mx, my));
        }
        _quad[0] = Corner(r.Left, r.Top);
        _quad[1] = Corner(r.Right, r.Top);
        _quad[2] = Corner(r.Left, r.Bottom);
        _quad[3] = Corner(r.Right, r.Bottom);
        d.BlendState = BlendState.AlphaBlend;
        d.DepthStencilState = DepthStencilState.None;
        d.RasterizerState = raster.ScissorTestEnable ? QuadRasterScissor : QuadRaster;
        d.SamplerStates[0] = SamplerState.LinearClamp;
        d.SamplerStates[1] = SamplerState.LinearClamp;
        foreach (var pass in dual.CurrentTechnique.Passes)
        {
            pass.Apply();
            d.DrawUserPrimitives(PrimitiveType.TriangleStrip, _quad, 0, 2);
        }
        SpriteCount++;
    }

    /// <summary>
    /// The infected TERRITORIES' textures, grown once per device: white premultiplied masks (the tint is the colour), N x
    /// N, a few variants. Each variant: the infected <see cref="Tissue"/> (an irregular smoky region with satellite
    /// blotches, never a disc), the softer, wider <see cref="Drain"/> (where the host's material changes), the
    /// <see cref="Dark"/> vein fragments (short, tapered, inside the region) and mottled darkening, the <see cref="Emit"/>
    /// light from inside (a soft mottled core, tapered runs along the fragments, a broken bright contaminated edge on one
    /// side), and its bloom in <see cref="Frames"/> steps from the infection's centre outward.
    /// </summary>
    internal sealed class TerritoryAtlas
    {
        public const int N = 160;
        public const int Variants = 4;
        public const int Frames = 6;
        public const int IdleGroups = 3;

        /// <summary>The share of the field's side a territory's region spans (its diameter is this x the side).</summary>
        public const float Fill = 0.68f;

        /// <summary>The bloom's last frame reaches this far in birth (every texel of the region is born by 1.2), so the
        /// grown frames end on the whole region and the settled masks follow without a pop.</summary>
        internal const float BloomReach = 1.36f;

        /// <summary>A texel's reveal in a bloom frame whose front stands at <paramref name="at"/>.</summary>
        internal static float RevealAt(float at, float birth) => Math.Clamp((at - birth) / 0.14f, 0f, 1f);

        private static TerritoryAtlas? _built;
        private static GraphicsDevice? _device;

        public Texture2D[] Tissue = null!, Drain = null!, Dark = null!, Emit = null!, Idle = null!, Edge = null!, Burn = null!, Fissure = null!;
        public Texture2D[] TissueGrow = null!, DarkGrow = null!, DrainGrow = null!, FrontGrow = null!, EdgeGrow = null!, BurnGrow = null!;

        // the grown fields, texel by texel (kept for the tests)
        internal readonly float[][] TissueF = new float[Variants][], DrainF = new float[Variants][], DarkF = new float[Variants][],
                                    VeinF = new float[Variants][], EmitF = new float[Variants][], RunF = new float[Variants][],
                                    BirthF = new float[Variants][], EdgeF = new float[Variants][], BurnF = new float[Variants][],
                                    FissureF = new float[Variants][];
        internal readonly int[][] GroupF = new int[Variants][];

        /// <summary>Each variant's vein fragments' lengths (texels).</summary>
        internal readonly List<float>[] Fragments = new List<float>[Variants];

        public static TerritoryAtlas For(GraphicsDevice device)
        {
            if (_built is not null && _device == device) return _built;
            _device = device;
            var a = Grow_(20261002);
            a.Upload(device);
            _built = a;
            return _built;
        }

        /// <summary>Grows every variant (no device: the tests read the fields).</summary>
        internal static TerritoryAtlas Grow_(int seed)
        {
            var a = new TerritoryAtlas();
            var rng = new Random(seed);
            for (var v = 0; v < Variants; v++) a.GrowVariant(v, rng);
            return a;
        }

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

        /// <summary>A mottling of smoke (0..1).</summary>
        private static float Smoke(int i)
        {
            int x = i % N, y = i / N;
            // (two octaves turned off the noise's grid: axis-aligned, the mottling read as "a woven", "a crosshatched"
            // pattern, a garment's)
            float u1 = x * 0.83f - y * 0.56f, v1 = x * 0.56f + y * 0.83f;
            float u2 = x * 0.47f + y * 0.88f, v2 = -x * 0.88f + y * 0.47f;
            var n = 0.65f * Noise2(u1 / 6.1f, v1 / 6.1f) + 0.35f * Noise2(u2 / 2.7f + 40f, v2 / 2.7f + 17f);
            return Math.Clamp((n - 0.32f) / 0.42f, 0f, 1f);
        }

        private void Upload(GraphicsDevice d)
        {
            Tissue = new Texture2D[Variants];
            Drain = new Texture2D[Variants];
            Dark = new Texture2D[Variants];
            Emit = new Texture2D[Variants];
            Idle = new Texture2D[Variants * IdleGroups];
            TissueGrow = new Texture2D[Variants * Frames];
            DarkGrow = new Texture2D[Variants * Frames];
            DrainGrow = new Texture2D[Variants * Frames];
            FrontGrow = new Texture2D[Variants * Frames];
            Edge = new Texture2D[Variants];
            EdgeGrow = new Texture2D[Variants * Frames];
            Burn = new Texture2D[Variants];
            BurnGrow = new Texture2D[Variants * Frames];
            Fissure = new Texture2D[Variants];
            for (var v = 0; v < Variants; v++)
            {
                Edge[v] = Mask(d, EdgeF[v]);
                Burn[v] = Mask(d, BurnF[v]);
                Fissure[v] = Mask(d, FissureF[v]);
                Tissue[v] = Mask(d, TissueF[v]);
                Drain[v] = Mask(d, DrainF[v]);
                Dark[v] = Mask(d, DarkF[v]);
                Emit[v] = Mask(d, EmitF[v]);
                for (var g = 0; g < IdleGroups; g++)
                {
                    var e = new float[N * N];
                    for (var i = 0; i < e.Length; i++) if (GroupF[v][i] == g) e[i] = EmitF[v][i];
                    Idle[v * IdleGroups + g] = Mask(d, Blur(e, 1));
                }
                for (var f = 0; f < Frames; f++)
                {
                    var at = (f + 1) / (float)Frames * BloomReach;
                    var tg = new float[N * N];
                    var dg = new float[N * N];
                    var rg = new float[N * N];
                    var fg = new float[N * N];
                    var eg = new float[N * N];
                    var bg = new float[N * N];
                    for (var i = 0; i < tg.Length; i++)
                    {
                        var b = BirthF[v][i];
                        var reveal = RevealAt(at, b);
                        eg[i] = EdgeF[v][i] * reveal;
                        bg[i] = BurnF[v][i] * reveal;
                        tg[i] = TissueF[v][i] * reveal;
                        dg[i] = DarkF[v][i] * reveal;
                        rg[i] = DrainF[v][i] * reveal;
                        var band = 1f - MathF.Abs(at - b) / 0.1f;
                        fg[i] = band > 0f && TissueF[v][i] > 0.1f ? band * (0.35f + 0.65f * Math.Max(VeinF[v][i], Smoke(i))) * Math.Min(1f, TissueF[v][i] * 2f) : 0f;
                    }
                    TissueGrow[v * Frames + f] = Mask(d, tg);
                    DarkGrow[v * Frames + f] = Mask(d, dg);
                    DrainGrow[v * Frames + f] = Mask(d, rg);
                    EdgeGrow[v * Frames + f] = Mask(d, eg);
                    BurnGrow[v * Frames + f] = Mask(d, bg);
                    var bl = Blur(fg, 2);
                    for (var i = 0; i < fg.Length; i++) fg[i] = Math.Min(1f, fg[i] + bl[i] * 0.6f);
                    FrontGrow[v * Frames + f] = Mask(d, fg);
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

        private static float Sq(float x) => x * x;
    }
}
