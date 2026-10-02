using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static IdleXIdle.Game.Presentation.Curse.CurseHost;

namespace IdleXIdle.Game.Presentation.Curse;

/// <summary>
/// BRAND's MATERIAL (ADR-013 §1): the approved prototype's colours and per-territory material rules (<see cref="Mat"/>,
/// <see cref="LightFor"/>, <see cref="Restraint"/>), the shader's per-creature parameters (<see cref="HostParams"/>,
/// <see cref="TerritoryParams"/>, <see cref="PuffParams"/>) built from them, and <see cref="Composite"/>: a pure C#
/// MIRROR of <c>Content/Shaders/BrandCurse.fx</c>, line by line, which the unit tests pin against the prototype's CPU
/// functions and the shader check pins against the GPU.
/// </summary>
/// <remarks>
/// <para>
/// THE COMPOSITE, in the prototype's exact pass order (each pass a SpriteBatch pass over the creature in 39b59aaa):
/// 1. per territory, in order, the host's material DRAINED (the Shadow Violet copy through the soft drain mask) and
/// BURNED (the Ash-Burn copy through the hard burn mask), each "over" the host, the copy multiplied by the creature's
/// draw tint as the prototype's DualTextureEffect did; 2. the STAIN multiplies (the body's faint shade, then per
/// territory tissue x Bruise, a pale host's second tissue, dark x Vein, burnt edge x BurnEdge); 3. the SCREEN pass
/// (tissue glow, emission, its second draw above 1, the idle accent); 4. the ADDITIVE pass (Ash-Burn fissures, a dying
/// host's flash, the bloom front, a deepen's travelling puffs). Multiplies, screens and adds each commute within their
/// pass, so only the passes' order and the drain's per-territory order matter.
/// </para>
/// <para>
/// THE STENCIL becomes coverage: the prototype clipped every pass but the drain to host alpha &gt; 90/255. The shader
/// draws the creature's frame once more and outputs premultiplied <c>(T x k, k)</c> with
/// <c>k = smoothstep(threshold +- 16/255, a) x a x tint.a</c>: inside the silhouette the corrupted host, a narrow smooth
/// ramp on the old threshold, nothing outside. T is computed on the host as if opaque (its colour unpremultiplied, the
/// tint's colour unpremultiplied); the host's own alpha and the tint's alpha are carried by k.
/// </para>
/// </remarks>
internal static class CurseMaterial
{
    // ── THE MATERIAL (moved unchanged from the approved prototype, 39b59aaa) ──────────────────────────────────────────
    // the stain MULTIPLIED into the host (it discolours the host's own material, keeping its folds and shading): the vein
    // fragments a deep bruised violet, the infected tissue a greyed lavender (on a pale host a deep saturated violet, laid
    // twice: once, grey lavender read as "dirt", "a dye stain")
    internal static readonly Color VeinStain = new(46, 18, 78);
    internal static readonly Color BruiseStain = new(92, 88, 100);
    internal static readonly Color PaleBruise = new(104, 96, 116);
    internal static readonly Color WispSmoke = new(62, 34, 92);
    internal static readonly Color AfflictedShade = new(150, 124, 176);

    // a dark host's territory faintly LIT FROM INSIDE (its flesh has already turned to ash: the drain carries it)
    internal static readonly Color TissueGlow = new(92, 84, 112);   // the ash's own faint light, barely violet
    internal const float TissueGlowShare = 0.35f;

    // the curse's light: violet-magenta; on a host already rich in violet it turns PALER, nearer white, so it never reads
    // as more of the creature's own violet
    internal static readonly Color Emission = new(168, 70, 236);
    internal static readonly Color EmissionPale = new(206, 178, 246);
    internal static readonly Color EmissionHot = new(236, 150, 255);

    // ASH-BURN: the colour burned out of the victim. Cold ash, the host's folds kept in it; a near-black burnt rim; the
    // vein fragments charred; the light a pale spectral leak from the fissures, barely lavender, restrained; the wisps
    // Shadow smoke, ash-dark
    internal static readonly Color BurnEdge = new(24, 17, 28);
    internal const float BurnEdgeShare = 0.85f;
    internal static readonly Color CharVein = new(40, 26, 54);
    internal static readonly Color SpectralPale = new(198, 192, 226);
    internal static readonly Color SpectralHot = new(226, 214, 252);
    internal static readonly Color AshSmoke = new(52, 46, 64);
    internal const float BurnRestraint = 0.62f;
    internal const float FissureLight = 0.95f;   // the fissures' light, added, against the ash (Light carries the restraint)

    /// <summary>The infected tissue's share over the drained material (it darkens it, smoky; it never repaints it).</summary>
    internal const float TissueShare = 0.6f;

    /// <summary>The resting emission.</summary>
    internal const float IdleEmission = 0.85f;

    /// <summary>A blooming front at its brightest.</summary>
    internal const float FrontPeak = 0.72f;

    /// <summary>The old stencil's threshold: host alpha above this was "inside".</summary>
    internal const float CoverageThreshold = 90f / 255f;

    /// <summary>Half the width of the smooth coverage ramp round <see cref="CoverageThreshold"/>.</summary>
    internal const float CoverageRamp = 16f / 255f;

    /// <summary>Territories the shader composes per creature (the body's most: <see cref="CurseSeating.MaxTerritories"/>).</summary>
    internal const int ShaderTerritories = 4;

    /// <summary>A deepen's travelling puffs the shader composes per creature (the brightest are kept).</summary>
    internal const int MaxPuffs = 4;

    private static readonly Vector3 Luma3 = new(0.299f, 0.587f, 0.114f);

    internal static Color BruiseFor(float luma) => Color.Lerp(BruiseStain, PaleBruise, PaleHost(luma));

    /// <summary>The tissue's stain on this host (on a dark host it only crusts the ash: multiplied whole it was black
    /// again, "a hole"); the same alive and dying.</summary>
    internal static float TissueOn(HostLook look) => TissueShare * (1f - 0.45f * Ramp(look.Luma, 0.4f, 0.12f));

    /// <summary>The light's restraint on this host: on a host rich in violet, or dark (a caster's own colours), the
    /// drained material carries the state, never more violet ("its own aura"); a dark but COLOURFUL host is not
    /// restrained for its darkness.</summary>
    internal static float Restraint(HostLook look)
    {
        var darkish = Ramp(look.Luma, 0.42f, 0.15f) * (1f - Ramp(look.Chroma, 0.1f, 0.25f));
        return (1f - 0.3f * VioletNorm(look.Violet)) * (1f - 0.45f * darkish) * (1f - 0.35f * darkish * (1f - Ramp(look.Chroma, 0.05f, 0.3f)));
    }

    /// <summary>
    /// HOST-AWARE LIGHT (a rule of the host's brightness, never of its name): up to 1.15x on a near-black body, where the
    /// dark corruption cannot show; trimmed a little on a pale one, where the additive front was the loudest.
    /// </summary>
    internal static float LightFor(float luma)
    {
        var p = Math.Clamp((luma - 0.35f) / 0.35f, 0f, 1f);
        var dark = Math.Clamp((0.2f - luma) / 0.12f, 0f, 1f);
        return (1f - 0.15f * p * p * (3f - 2f * p)) * (1f + 0.15f * dark * dark * (3f - 2f * dark));
    }

    /// <summary>The curse's violet light on this host: paler on a host already rich in violet, and restrained there.</summary>
    internal static Color EmissionFor(HostLook host) => Color.Lerp(Emission, EmissionPale, 0.7f * VioletNorm(host.Violet));

    /// <summary>One territory's material: Shadow Violet blended into Ash-Burn by its <see cref="Burn"/>; the same alive and
    /// dying.</summary>
    internal readonly struct Mat
    {
        public readonly float Burn, Light, Tissue, Glow;
        public readonly Color Emission, Accent, Hot, Vein, Smoke, Bruise;

        public Mat(HostLook look, float burn)
        {
            Burn = burn;
            Light = LightFor(look.Luma) * MathHelper.Lerp(Restraint(look), BurnRestraint, burn);
            // (the dark tissue would blacken the ash again: it only crusts it)
            Tissue = TissueOn(look) * (1f - 0.55f * burn);
            Glow = TissueGlowShare * DarkHost(look.Luma) * (1f - 0.6f * burn);
            Emission = Color.Lerp(EmissionFor(look), SpectralPale, 0.85f * burn);
            // one section at a time answers in a breath of the curse's violet: its only violet on an ash-burned host
            Accent = Color.Lerp(Emission, CurseMaterial.Emission, 0.45f * burn);
            Hot = Color.Lerp(EmissionHot, SpectralHot, 0.55f * burn);
            Vein = Color.Lerp(VeinStain, CharVein, burn);
            Smoke = Color.Lerp(WispSmoke, AshSmoke, burn);
            Bruise = BruiseFor(look.Luma);
        }
    }

    // ── THE SHADER'S PARAMETERS ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>One creature's host constants for the shader (all per-host, none per pixel).</summary>
    /// <param name="Luma">The host's mean brightness.</param>
    /// <param name="DrainedMean">The drained material's mean brightness (<see cref="CurseHost.DrainedMean"/>).</param>
    /// <param name="AshMean">The ash's mean brightness (<see cref="CurseHost.AshMean"/>).</param>
    /// <param name="DrainTint">The drained material's tint (<see cref="CurseHost.DrainTint"/>).</param>
    /// <param name="Bruise">The tissue's stain colour (<see cref="BruiseFor"/>).</param>
    /// <param name="Emission">The curse's light on this host before Ash-Burn (<see cref="EmissionFor"/>).</param>
    /// <param name="Shade">The body's faint shade, as the multiply factor it applies (1 = none).</param>
    internal readonly record struct HostParams(float Luma, float DrainedMean, float AshMean, Vector3 DrainTint, Vector3 Bruise,
                                               Vector3 Emission, Vector3 Shade);

    /// <summary>
    /// One territory's shader parameters: where its masks sit on the host (<see cref="MaskU"/>, <see cref="MaskV"/>: rows of
    /// the host-UV to mask-local affine, <c>w</c> = the variant's atlas cell origin), how far it has bloomed, and how much of
    /// each pass it draws. All zero is a territory that draws nothing.
    /// </summary>
    internal struct TerritoryParams
    {
        /// <summary>Mask-local u = dot(xyz, (U, V, 1)); w = the variant's page 0 cell origin, atlas u.</summary>
        public Vector4 MaskU;

        /// <summary>Mask-local v = dot(xyz, (U, V, 1)); w = the variant's page 0 cell origin, atlas v.</summary>
        public Vector4 MaskV;

        /// <summary>The bloom's front in birth (<c>reveal x </c><see cref="CurseAtlas.BloomReach"/>).</summary>
        public float At;

        /// <summary>The drained copy's "over" alpha (the prototype's <c>s(1 - b) / (1 - sb)</c>).</summary>
        public float DrainAlpha;

        /// <summary>The burned copy's "over" alpha (<c>s x b</c>).</summary>
        public float BurnAlpha;

        /// <summary>1 / the stain's slow swell (the stain's masks are drawn this much larger).</summary>
        public float InvSwell;

        /// <summary>Stain amounts: tissue x Bruise, the pale host's second tissue, dark x Vein, edge x BurnEdge.</summary>
        public float Tissue, Pale, Vein, Edge;

        /// <summary>Screen levels: the tissue's glow, the emission (its part above 1 is a second draw).</summary>
        public float Glow, Emit;

        /// <summary>This territory's Ash-Burn share (its colours are lerped by it in the shader).</summary>
        public float Burn;

        /// <summary>The idle accent's level and section group (0..2).</summary>
        public float Accent, AccentGroup;

        /// <summary>Additive levels: the fissures, the bloom front, the emission (a dying host's flash).</summary>
        public float Fissure, Front, EmitAdd;
    }

    /// <summary>A deepen's travelling puff: its centre in screen pixels, its radius, and its premultiplied colour.</summary>
    internal readonly record struct PuffParams(Vector2 Centre, float Radius, Vector3 Colour);

    /// <summary>The faint shade's level over the whole host: a little more each depth (<paramref name="stage"/> may be
    /// between two: the presentation eases it from one to the next), much less on an Ash-Burn host;
    /// <paramref name="fall"/> fades it as a dying host's curse collapses.</summary>
    internal static float ShadeLevel(float stage, float hostBurn, float fall = 1f) => (0.08f + 0.04f * stage) * fall * (1f - 0.7f * hostBurn);

    /// <summary>The host constants for <paramref name="look"/>, its shade at <paramref name="shadeLevel"/>.</summary>
    internal static HostParams Host(HostLook look, float shadeLevel)
        => new(look.Luma, DrainedMean(look.Luma), AshMean(look.Luma), DrainTint(look), Rgb(BruiseFor(look.Luma)), Rgb(EmissionFor(look)),
               Vector3.One - shadeLevel * (Vector3.One - Rgb(AfflictedShade)));

    /// <summary>
    /// A living territory's levels, from the prototype's draw (39b59aaa <c>DrawCorruptionGlow</c>): its bloom
    /// <paramref name="reveal"/>, its slow <paramref name="swell"/> and <paramref name="breath"/>, the light's
    /// <paramref name="boost"/> (1 + afterglow + 0.9 x source + react), the idle accent's <paramref name="answer"/>,
    /// <paramref name="group"/> and <paramref name="settle"/>, and the bloom front's <paramref name="peak"/> (FrontPeak x
    /// LightFor x its quiet x its arrival share). The masks' placement is set apart (<see cref="Place"/>).
    /// </summary>
    internal static TerritoryParams Living(HostLook look, float burn, float reveal, float swell, float breath, float boost,
                                           float answer, int group, float settle, float peak)
    {
        var t = new TerritoryParams();
        if (reveal <= 0f) return t;
        var m = new Mat(look, burn);
        Drains(ref t, look, burn, reveal, Math.Clamp(DrainStrength(look, burn) * Math.Min(1f, reveal * 3f), 0f, 1f));
        t.InvSwell = 1f / swell;
        var lit = reveal * reveal;
        t.Glow = m.Glow > 0f ? m.Glow * breath * lit : 0f;
        t.Emit = IdleEmission * m.Light * breath * boost * lit;
        t.Accent = 0.3f * m.Light * answer * answer * lit * settle;
        t.AccentGroup = group;
        t.Fissure = burn > 0f ? Math.Min(1f, FissureLight * m.Light * breath * boost * lit * burn) : 0f;
        t.Front = reveal < 1f ? peak * Math.Min(1f, reveal * 5f) * (1f - 0.6f * reveal) : 0f;
        return t;
    }

    /// <summary>
    /// A dying host's territory (39b59aaa <c>DrawCorruptionLeaving</c>): what it <paramref name="wore"/>, collapsed by
    /// <paramref name="fall"/>; a flash of its light for the first 150 ms of <paramref name="age"/>, then its front
    /// shrinking back toward the infection's centre. <paramref name="alive"/> is the territory's living levels the moment
    /// before the fall (<see cref="Living"/>): its drain strength, its light and its front carry on from there and go out
    /// with the collapse, so nothing steps on the frame the host falls (the prototype dropped the living light, and cut a
    /// mid-bloom drain from <c>min(1, 3r)</c> to <c>r</c>, in one frame). Default: a territory that showed no light.
    /// </summary>
    internal static TerritoryParams Leaving(HostLook look, float burn, float wore, float fall, float age, in TerritoryParams alive = default)
    {
        var t = new TerritoryParams();
        var left = wore * fall;
        if (left <= 0.01f) return t;
        var m = new Mat(look, burn);
        Drains(ref t, look, burn, left, Math.Clamp(DrainStrength(look, burn) * Math.Min(1f, wore * 3f) * fall, 0f, 1f));
        t.InvSwell = alive.InvSwell > 0f ? alive.InvSwell : 1f;
        t.Glow = alive.Glow * fall;
        t.Emit = alive.Emit * fall;
        t.Accent = alive.Accent * fall;
        t.AccentGroup = alive.AccentGroup;
        t.Fissure = alive.Fissure * fall;
        const float flash = 150f;
        var collapse = FrontPeak * m.Light * 0.7f * left;
        if (age < flash)
        {
            var f = 0.6f * m.Light * wore * MathF.Sin(age / flash * MathF.PI);
            if (burn >= 0.5f) t.Fissure += f;
            else t.EmitAdd = f;
            // the living front hands over to the collapsing one across the flash
            var h = age / flash;
            t.Front = MathHelper.Lerp(alive.Front, collapse, h * h * (3f - 2f * h));
        }
        else t.Front = collapse;
        return t;
    }

    private static void Drains(ref TerritoryParams t, HostLook look, float burn, float reveal, float s)
    {
        t.At = Math.Min(reveal, 1f) * CurseAtlas.BloomReach;
        t.Burn = burn;
        // two "over" draws do not add their coverage, so the first is raised to s(1-b)/(1-sb): where the masks meet the
        // host keeps exactly 1-s, the copies s(1-b) and sb (prototype DrainTerritory); a draw under 0.004 was skipped
        if (s > 0.004f)
        {
            t.DrainAlpha = burn < 1f ? Math.Min(1f, s * (1f - burn) / Math.Max(1e-3f, 1f - s * burn)) : 0f;
            t.BurnAlpha = burn > 0f ? Math.Min(1f, s * burn) : 0f;
        }
        var m = new Mat(look, burn);
        t.Tissue = m.Tissue;
        t.Pale = TissueShare * 0.5f * PaleHost(look.Luma);
        t.Vein = 1f;
        t.Edge = burn > 0f ? BurnEdgeShare * burn : 0f;
    }

    /// <summary>
    /// Places a territory's masks on the drawn frame: the affine from host UV (the strip texture, <paramref name="texW"/> x
    /// <paramref name="texH"/>) to the variant's mask-local uv, for a frame drawn from <paramref name="src"/> into
    /// <paramref name="dest"/> (squash included) with <paramref name="effects"/>, and a territory field centred at
    /// <paramref name="centre"/> on screen, <paramref name="side"/> across, turned by <paramref name="turn"/>, mirrored by
    /// <paramref name="flipMask"/> (exactly the prototype's <c>T()</c> sprite and its drain quad's mapping).
    /// </summary>
    internal static void Place(ref TerritoryParams t, Rectangle src, Rectangle dest, SpriteEffects effects, int texW, int texH,
                               Vector2 centre, float side, float turn, bool flipMask, int variant)
    {
        var p00 = MaskOf(ScreenOf(0f, 0f, src, dest, effects, texW, texH), centre, side, turn, flipMask);
        var p10 = MaskOf(ScreenOf(1f, 0f, src, dest, effects, texW, texH), centre, side, turn, flipMask);
        var p01 = MaskOf(ScreenOf(0f, 1f, src, dest, effects, texW, texH), centre, side, turn, flipMask);
        var origin = CurseAtlas.CellOrigin(variant);
        t.MaskU = new Vector4(p10.X - p00.X, p01.X - p00.X, p00.X, origin.X);
        t.MaskV = new Vector4(p10.Y - p00.Y, p01.Y - p00.Y, p00.Y, origin.Y);
    }

    /// <summary>The affine from host UV to screen pixels for the drawn frame: (scale u, scale v, offset x, offset y).</summary>
    internal static Vector4 UvToPx(Rectangle src, Rectangle dest, SpriteEffects effects, int texW, int texH)
    {
        var p00 = ScreenOf(0f, 0f, src, dest, effects, texW, texH);
        var p10 = ScreenOf(1f, 0f, src, dest, effects, texW, texH);
        var p01 = ScreenOf(0f, 1f, src, dest, effects, texW, texH);
        return new Vector4(p10.X - p00.X, p01.Y - p00.Y, p00.X, p00.Y);
    }

    /// <summary>Where host UV (<paramref name="u"/>, <paramref name="v"/>) lands on screen, for the drawn frame.</summary>
    internal static Vector2 ScreenOf(float u, float v, Rectangle src, Rectangle dest, SpriteEffects effects, int texW, int texH)
    {
        var lx = (u * texW - src.X) / src.Width;
        var ly = (v * texH - src.Y) / src.Height;
        if ((effects & SpriteEffects.FlipHorizontally) != 0) lx = 1f - lx;
        if ((effects & SpriteEffects.FlipVertically) != 0) ly = 1f - ly;
        return new Vector2(dest.X + lx * dest.Width, dest.Y + ly * dest.Height);
    }

    /// <summary>A screen point's mask-local uv in a territory field (rotated back by <paramref name="turn"/>, mirrored).</summary>
    internal static Vector2 MaskOf(Vector2 p, Vector2 centre, float side, float turn, bool flipMask)
    {
        var cos = MathF.Cos(-turn);
        var sin = MathF.Sin(-turn);
        var dx = p.X - centre.X;
        var dy = p.Y - centre.Y;
        var mx = (dx * cos - dy * sin) / side + 0.5f;
        var my = (dx * sin + dy * cos) / side + 0.5f;
        if (flipMask) mx = 1f - mx;
        return new Vector2(mx, my);
    }

    // ── THE MIRROR OF BrandCurse.fx ────────────────────────────────────────────────────────────────────────────────────
    // [Fn] tags name the shader's matching lines.

    /// <summary>The five atlas samples one territory takes at one pixel: page 0 / page 1 at the mask's uv and at the
    /// stain's swelled uv, page 2 at the mask's uv (each zero outside the field).</summary>
    internal struct TerritoryTexels
    {
        public Vector4 Page0, Page1, Page2, Page0Swell, Page1Swell;
    }

    /// <summary>[F2] Samples one territory's atlas texels at host UV (<paramref name="u"/>, <paramref name="v"/>), bilinear and
    /// clamped to the cell as the GPU samples them (<paramref name="atlas"/> = <see cref="CurseAtlas.Pack"/>).</summary>
    internal static TerritoryTexels Texels(Color[] atlas, in TerritoryParams t, float u, float v)
    {
        var mu = new Vector2(t.MaskU.X * u + t.MaskU.Y * v + t.MaskU.Z, t.MaskV.X * u + t.MaskV.Y * v + t.MaskV.Z);
        var ms = (mu - new Vector2(0.5f)) * t.InvSwell + new Vector2(0.5f);
        var origin = new Vector2(t.MaskU.W, t.MaskV.W);
        return new TerritoryTexels
        {
            Page0 = Page(atlas, origin, 0, mu),
            Page1 = Page(atlas, origin, 1, mu),
            Page2 = Page(atlas, origin, 2, mu),
            Page0Swell = Page(atlas, origin, 0, ms),
            Page1Swell = Page(atlas, origin, 1, ms),
        };
    }

    // [F1] one page's texel: zero outside the field, clamped half a texel inside the cell (never the neighbour's)
    private static Vector4 Page(Color[] atlas, Vector2 origin, int page, Vector2 mu)
    {
        if (mu.X < 0f || mu.Y < 0f || mu.X > 1f || mu.Y > 1f) return Vector4.Zero;
        const float half = 0.5f / CurseAtlas.N;
        var c = Vector2.Clamp(mu, new Vector2(half), new Vector2(1f - half));
        var uv = origin + new Vector2((page + c.X) * CurseAtlas.N / CurseAtlas.Width, c.Y * CurseAtlas.N / CurseAtlas.Height);
        return Bilinear(atlas, CurseAtlas.Width, CurseAtlas.Height, uv);
    }

    /// <summary>A linear, clamp-addressed sample of a <paramref name="w"/> x <paramref name="h"/> texture at <paramref name="uv"/>, 0..1.</summary>
    internal static Vector4 Bilinear(Color[] tex, int w, int h, Vector2 uv)
    {
        var x = uv.X * w - 0.5f;
        var y = uv.Y * h - 0.5f;
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        Vector4 At(int px, int py) => tex[Math.Clamp(py, 0, h - 1) * w + Math.Clamp(px, 0, w - 1)].ToVector4();
        var top = Vector4.Lerp(At(x0, y0), At(x0 + 1, y0), fx);
        var bottom = Vector4.Lerp(At(x0, y0 + 1), At(x0 + 1, y0 + 1), fx);
        return Vector4.Lerp(top, bottom, fy);
    }

    /// <summary>
    /// The curse over one host pixel, exactly as <c>BrandCurse.fx</c> computes it: the premultiplied host texel
    /// <paramref name="host"/>, the creature's premultiplied draw <paramref name="tint"/>, its host constants, its
    /// territories with their sampled <paramref name="texels"/>, its puffs, and the pixel's screen position
    /// <paramref name="px"/> (for the puffs). Returns the premultiplied output (lerp(S, T, cov) x A, A): the pass is the
    /// creature's only draw, at its own alpha A (S the plain sprite, T the corrupted host, cov the threshold's ramp).
    /// </summary>
    internal static Vector4 Composite(Vector4 host, Vector4 tint, in HostParams h, ReadOnlySpan<TerritoryParams> territories,
                                      ReadOnlySpan<TerritoryTexels> texels, ReadOnlySpan<PuffParams> puffs, Vector2 px)
    {
        // [H1] the host and the tint, unpremultiplied: T is computed on the host as if opaque
        var hu = new Vector3(host.X, host.Y, host.Z) / MathF.Max(host.W, 1e-4f);
        var tu = new Vector3(tint.X, tint.Y, tint.Z) / MathF.Max(tint.W, 1e-4f);
        // [H2] the drained (Shadow Violet, burn 0) and burned (Ash-Burn, burn 1) material: CurseHost.DrainPixel
        var l = Vector3.Dot(hu, Luma3);
        var drained = Saturate(h.DrainTint * (h.DrainedMean + (l - h.Luma) * DrainFlatten));
        var burned = Saturate(AshTint * (h.AshMean + (l - h.Luma) * AshFold));
        // [H3] the creature as drawn
        var d = hu * tu;

        // [P1] DRAIN / BURN, territory by territory: each copy (x the tint, as the DualTextureEffect did) "over" the host
        for (var k = 0; k < territories.Length; k++)
        {
            ref readonly var t = ref territories[k];
            var x = texels[k];
            var rev = Reveal(t.At, x.Page1.W);
            d = Vector3.Lerp(d, drained * tu, x.Page0.X * rev * t.DrainAlpha);
            d = Vector3.Lerp(d, burned * tu, x.Page0.Y * rev * t.BurnAlpha);
        }

        // [P2] STAIN: multiply for premultiplied sources, dst x lerp(1, colour, amount x mask)
        d *= h.Shade;
        for (var k = 0; k < territories.Length; k++)
        {
            ref readonly var t = ref territories[k];
            var x = texels[k];
            var rev = Reveal(t.At, x.Page1Swell.W);
            var vein = Vector3.Lerp(Rgb(VeinStain), Rgb(CharVein), t.Burn);
            d *= Vector3.One - x.Page0Swell.Z * rev * t.Tissue * (Vector3.One - h.Bruise);
            d *= Vector3.One - x.Page0Swell.Z * rev * t.Pale * (Vector3.One - h.Bruise);
            d *= Vector3.One - x.Page0Swell.W * rev * t.Vein * (Vector3.One - vein);
            d *= Vector3.One - x.Page1Swell.X * rev * t.Edge * (Vector3.One - Rgb(BurnEdge));
        }

        // [P3] SCREEN: dst + src x (1 - dst)
        for (var k = 0; k < territories.Length; k++)
        {
            ref readonly var t = ref territories[k];
            var x = texels[k];
            var emission = Vector3.Lerp(h.Emission, Rgb(SpectralPale), 0.85f * t.Burn);
            var accent = Vector3.Lerp(emission, Rgb(Emission), 0.45f * t.Burn);
            var soft = 1f - t.Burn;
            var group = Vector3.Clamp(Vector3.One - Abs(new Vector3(t.AccentGroup) - new Vector3(0f, 1f, 2f)), Vector3.Zero, Vector3.One);
            d = Screen(d, Rgb(TissueGlow) * (t.Glow * x.Page0.Z));
            d = Screen(d, emission * (MathF.Min(1f, t.Emit) * soft * x.Page1.Y));
            d = Screen(d, emission * (Math.Clamp(t.Emit - 1f, 0f, 1f) * soft * x.Page1.Y));
            d = Screen(d, accent * (t.Accent * Vector3.Dot(new Vector3(x.Page2.X, x.Page2.Y, x.Page2.Z), group)));
        }

        // [P4] ADDITIVE: the fissures, a dying host's flash, the bloom front, the travelling puffs
        for (var k = 0; k < territories.Length; k++)
        {
            ref readonly var t = ref territories[k];
            var x = texels[k];
            var emission = Vector3.Lerp(h.Emission, Rgb(SpectralPale), 0.85f * t.Burn);
            var hot = Vector3.Lerp(Rgb(EmissionHot), Rgb(SpectralHot), 0.55f * t.Burn);
            var band = MathF.Max(0f, 1f - MathF.Abs(t.At - x.Page1.W * CurseAtlas.BirthScale) / CurseAtlas.FrontBand);
            var front = MathF.Min(1f, band * x.Page2.W * CurseAtlas.FrontScale);
            d += emission * (t.Fissure * x.Page1.Z + t.EmitAdd * x.Page1.Y) + hot * (t.Front * front);
        }
        for (var i = 0; i < puffs.Length; i++)
        {
            var q = Vector2.DistanceSquared(px, puffs[i].Centre) / (puffs[i].Radius * puffs[i].Radius);
            var f = MathF.Max(0f, 1f - q);
            d += puffs[i].Colour * (f * f);
        }
        d = Saturate(d);

        // [K] THE PASS IS THE CREATURE'S ONLY DRAW (the arena skips it): every texel is composited once, at the sprite's
        // own alpha A = host.a x tint.a. The corrupted host replaces the plain one by the old stencil's threshold as a
        // narrow smooth ramp: under it the creature exactly as the arena draws it (host x tint), above it the curse
        var e = Math.Clamp((host.W - (CoverageThreshold - CoverageRamp)) / (2f * CoverageRamp), 0f, 1f);
        var cov = e * e * (3f - 2f * e);
        var a = host.W * tint.W;
        return new Vector4(Vector3.Lerp(hu * tu, d, cov) * a, a);
    }

    /// <summary>[R] A mask texel's reveal at the bloom's front <paramref name="at"/>, from its stored birth.</summary>
    internal static float Reveal(float at, float storedBirth) => CurseAtlas.RevealAt(at, storedBirth * CurseAtlas.BirthScale);

    private static Vector3 Screen(Vector3 dst, Vector3 src) => dst + src * (Vector3.One - dst);

    private static Vector3 Saturate(Vector3 v) => Vector3.Clamp(v, Vector3.Zero, Vector3.One);

    private static Vector3 Abs(Vector3 v) => new(MathF.Abs(v.X), MathF.Abs(v.Y), MathF.Abs(v.Z));

    /// <summary>A colour's rgb, 0..1.</summary>
    internal static Vector3 Rgb(Color c) => c.ToVector3();
}
