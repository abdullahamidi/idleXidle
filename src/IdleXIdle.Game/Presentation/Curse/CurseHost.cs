using System;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation.Curse;

/// <summary>
/// What a host is made of, read once per creature from its idle strip: its mean brightness, its colourfulness (mean
/// chroma of its opaque pixels, 0..1), its native violet (the share of its coloured pixels that are blue-violet to
/// magenta) and its SHADOW share (of its opaque pixels, the blue-to-magenta tinted and the near-black: a look the
/// curse's own Shadow colours belong to).
/// </summary>
/// <param name="Luma">Mean brightness of the opaque pixels, 0..1.</param>
/// <param name="Chroma">Mean chroma (max - min channel) of the opaque pixels, 0..1.</param>
/// <param name="Violet">Share of the coloured pixels whose hue is blue-violet to magenta.</param>
/// <param name="Shadow">Share of the opaque pixels that are near-black or blue-to-magenta tinted.</param>
public readonly record struct HostLook(float Luma, float Chroma, float Violet, float Shadow = 0f);

/// <summary>
/// BRAND's HOST MATERIAL RULES (ADR-013 §3), pure and device-free: how a host's look is measured, how much it is
/// ASH-BURNED rather than drained to Shadow violet, and what one host pixel becomes inside an infected territory. The
/// offline bake (<c>brand_host_bake_test</c>) and the runtime share this code, so the baked numbers are the drawn ones.
/// </summary>
/// <remarks>
/// <para>
/// THE HOST DECIDES THE CONTRAST, by rule, never by name, as a continuous blend of two material responses. SHADOW
/// VIOLET, where violet contrasts with the host naturally: its colour drained to violet-grey. ASH-BURN, where the host's
/// own look is already dark, shadowy or violet and the curse's violet read as "its own aura": the colour burned out to
/// a cold ash pushed far from the host's own value.
/// </para>
/// <para>
/// Every pixel input here is PREMULTIPLIED (as <c>AssetLibrary</c> premultiplies at load) and every reader
/// unpremultiplies; the formulas are the approved prototype's (<c>39b59aaa</c>), moved here unchanged.
/// </para>
/// </remarks>
internal static class CurseHost
{
    /// <summary>Local contrast the drained material keeps.</summary>
    internal const float DrainFlatten = 0.72f;

    /// <summary>How far the ash keeps the host's folds (kept at 0.9 the ash read as "a see-through haze laid over the robe").</summary>
    internal const float AshFold = 1.3f;

    /// <summary>A cold grey, barely violet: the drained material's tint on a host without native violet.</summary>
    internal static readonly Vector3 VioletGrey = new(0.97f, 0.94f, 1.0f);

    /// <summary>The drained material's tint on a host rich in violet (a violet tint there would hand the colour back).</summary>
    internal static readonly Vector3 AshGrey = new(0.96f, 0.96f, 0.97f);

    /// <summary>A near-black host's faint violet-grey contamination (a neutral grey there read as "a see-through hole").</summary>
    internal static readonly Vector3 AshContamination = new(0.9f, 0.84f, 1.06f);

    /// <summary>Cold ash: a breath of blue in the grey, never cyan ice, never warm.</summary>
    internal static readonly Vector3 AshTint = new(0.95f, 0.97f, 1.0f);

    /// <summary>Mean luma, mean chroma, native violet share and shadow share of the opaque pixels (premultiplied input).</summary>
    internal static HostLook Measure(ReadOnlySpan<Color> data)
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

    /// <summary>
    /// 0..1, the host's ASH-BURN share: a look already dark and Shadow-coloured (most of its pixels blue-to-magenta or
    /// near-black, little other colour) or rich in native violet; never on a pale host (ash there would read as white).
    /// A continuous rule of the host's look, never of its name.
    /// </summary>
    internal static float AshBurn(HostLook h)
        => (1f - PaleHost(h.Luma)) * Math.Max(Ramp(h.Shadow, 0.42f, 0.7f) * (1f - 0.6f * Ramp(h.Chroma, 0.14f, 0.28f)), VioletNorm(h.Violet));

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

    /// <summary>The drained material's tint on this host (violet-grey, ash on a violet host, contaminated on a black one).</summary>
    internal static Vector3 DrainTint(HostLook host)
    {
        var tint = Vector3.Lerp(VioletGrey, AshGrey, VioletNorm(host.Violet));
        // (on a near-black host the ash takes a faint violet-grey contamination: a smooth neutral grey read as "a
        // see-through hole", the floor showing through; never on a host already rich in violet: its own colour back)
        return Vector3.Lerp(tint, AshContamination, DarkHost(host.Luma) * (1f - VioletNorm(host.Violet)));
    }

    /// <summary>One premultiplied pixel of the drained material (alpha kept), at the host's own Ash-Burn share.</summary>
    internal static Color DrainPixel(Color c, HostLook host) => DrainPixel(c, host, AshBurn(host));

    /// <summary>One premultiplied pixel of the drained material (alpha kept), Shadow Violet blended into Ash-Burn by
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
        var v = Vector3.Clamp(DrainTint(host) * level, Vector3.Zero, Vector3.One);
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

    /// <inheritdoc cref="DrainStrength(HostLook)"/>
    internal static float DrainStrength(HostLook host, float burn)
        => Math.Min(0.95f, Math.Clamp(0.55f + 0.3f * Ramp(host.Chroma, 0.05f, 0.35f) + 0.15f * VioletNorm(host.Violet) + 0.3f * Ramp(host.Luma, 0.4f, 0.12f), 0f, 0.92f)
                           + 0.3f * burn);

    /// <summary>1 on a near-black host (its territories are lit from inside), 0 from a mid-dark one.</summary>
    internal static float DarkHost(float luma) => Ramp(luma, 0.32f, 0.12f);

    /// <summary>1 on a pale host (its bruise is a deep saturated violet), 0 below a mid one.</summary>
    internal static float PaleHost(float luma) => Ramp(luma, 0.42f, 0.62f);

    /// <summary>Smoothstep of <paramref name="x"/> from <paramref name="a"/> to <paramref name="b"/> (inverted when a &gt; b).</summary>
    internal static float Ramp(float x, float a, float b)
    {
        var t = Math.Clamp((x - a) / (b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>Standard HSV hue in degrees.</summary>
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
}
