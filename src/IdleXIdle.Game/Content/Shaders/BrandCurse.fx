// BRAND's curse (ADR-013 §1): one pass that DRAWS an afflicted creature's CURRENT frame through
// SpriteBatch.Begin(effect: this), in place of its arena draw (never over it: a second draw would composite every
// soft texel twice). Texture 0 is the host strip (premultiplied), input.Color the creature's draw tint (premultiplied).
// It reproduces the approved prototype's (39b59aaa) composite in its exact pass order and returns premultiplied
// (lerp(S, T, cov) * A, A): the sprite's own coverage A, the corrupted host T inside the old stencil's threshold, the
// plain sprite S under it.
//
// Every line follows CurseMaterial.Composite (src/IdleXIdle.Game/Presentation/Curse/CurseMaterial.cs) line by line; the
// [tags] name the matching lines there. Edit both together: brand_curse_shader_test pins the C# mirror against the
// prototype's CPU functions, and the shader check pins this file against the mirror.
//
// Compiled on Windows by tools/shaders/build_shaders.sh (mgfxc /Profile:OpenGL) to assets/shaders/brand_curse.mgfxo,
// which is committed; brand_curse_shader_test fails when this file changes and the binary is not rebuilt.

#if OPENGL
    #define SV_POSITION POSITION
    #define PS_SHADERMODEL ps_3_0
#else
    #define PS_SHADERMODEL ps_4_0_level_9_3
#endif

// ── TEXTURES ──────────────────────────────────────────────────────────────────────────────────────────────────────
Texture2D SpriteTexture;
sampler2D SpriteTextureSampler : register(s0) = sampler_state { Texture = <SpriteTexture>; };

// the packed territory atlas (CurseAtlas.Pack): per variant row, page 0 = (drain, burn, tissue, dark),
// page 1 = (edge, emit, fissure, birth / 1.2), page 2 = (idle 0, idle 1, idle 2, front / 1.6)
Texture2D CurseAtlas;
sampler2D AtlasSampler : register(s1) = sampler_state
{
    Texture = <CurseAtlas>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

// ── THE MATERIAL'S CONSTANTS (CurseMaterial / CurseHost, byte colours / 255) ─────────────────────────────────────
static const float3 Luma3 = float3(0.299, 0.587, 0.114);
static const float DrainFlatten = 0.72;
static const float AshFold = 1.3;
static const float3 AshTint = float3(0.95, 0.97, 1.0);
static const float3 VeinStain = float3(46.0, 18.0, 78.0) / 255.0;
static const float3 CharVein = float3(40.0, 26.0, 54.0) / 255.0;
static const float3 BurnEdge = float3(24.0, 17.0, 28.0) / 255.0;
static const float3 TissueGlow = float3(92.0, 84.0, 112.0) / 255.0;
static const float3 Emission = float3(168.0, 70.0, 236.0) / 255.0;
static const float3 SpectralPale = float3(198.0, 192.0, 226.0) / 255.0;
static const float3 EmissionHot = float3(236.0, 150.0, 255.0) / 255.0;
static const float3 SpectralHot = float3(226.0, 214.0, 252.0) / 255.0;

// the atlas's geometry (CurseAtlas: N 160, 640 x 640, 3 pages + the puff's column)
static const float CellU = 160.0 / 640.0;       // one cell's width in atlas u
static const float CellV = 160.0 / 640.0;       // one cell's height in atlas v
static const float HalfTexel = 0.5 / 160.0;     // half a texel in mask-local uv
static const float BirthScale = 1.2;
static const float FrontScale = 1.6;
static const float RevealBand = 0.14;
static const float FrontBand = 0.1;

// the old stencil's threshold (alpha > 90/255) as a narrow smooth ramp
static const float CoverageLo = (90.0 - 16.0) / 255.0;
static const float CoverageHi = (90.0 + 16.0) / 255.0;

// ── PER CREATURE (BrandCurseEffect; set between draws, never inside one) ────────────────────────────────────────────
float4 HostLevels;      // (luma, drained mean, ash mean, 0)
float3 DrainTint;       // the drained material's tint
float3 Bruise;          // the tissue's stain colour
float3 HostEmission;    // the curse's light on this host before Ash-Burn
float3 Shade;           // the body's faint shade, as its multiply factor
float4 UvToPx;          // host uv -> screen px: (scale u, scale v, offset x, offset y)

// per territory (CurseMaterial.TerritoryParams); an all-zero territory draws nothing
float4 MaskU[4];        // mask u = dot(xyz, (U, V, 1)); w = the variant's cell origin, atlas u
float4 MaskV[4];        // mask v = dot(xyz, (U, V, 1)); w = the variant's cell origin, atlas v
float4 Grow[4];         // (at, drain alpha, burn alpha, 1 / swell)
float4 Stain[4];        // (tissue, pale, vein, edge)
float4 Light[4];        // (glow, emit, burn, accent)
float4 Add[4];          // (fissure, front, emit add, accent group)

// a deepen's travelling puffs (CurseMaterial.PuffParams); colour 0 = none
float4 PuffAt[4];       // (screen x, screen y, 1 / radius, 0)
float4 PuffColour[4];   // (premultiplied rgb, 0)

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

// [F1] one page's texel at mask-local uv: zero outside the field, clamped half a texel inside the cell
float4 Page(float2 origin, float page, float2 mu)
{
    float inside = step(0.0, mu.x) * step(0.0, mu.y) * step(mu.x, 1.0) * step(mu.y, 1.0);
    float2 c = clamp(mu, HalfTexel, 1.0 - HalfTexel);
    return tex2Dlod(AtlasSampler, float4(origin + float2((page + c.x) * CellU, c.y * CellV), 0.0, 0.0)) * inside;
}

// [R] a mask texel's reveal at the bloom's front, from its stored birth
float Reveal(float at, float storedBirth)
{
    return saturate((at - storedBirth * BirthScale) / RevealBand);
}

// screen: dst + src x (1 - dst)
float3 ScreenOver(float3 dst, float3 src)
{
    return dst + src * (1.0 - dst);
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TextureCoordinates;
    float4 host = tex2D(SpriteTextureSampler, uv);
    float4 tint = input.Color;

    // [H1] the host and the tint, unpremultiplied: T is computed on the host as if opaque
    float3 hu = host.rgb / max(host.a, 1e-4);
    float3 tu = tint.rgb / max(tint.a, 1e-4);
    // [H2] the drained (Shadow Violet) and burned (Ash-Burn) material: CurseHost.DrainPixel at burn 0 and 1
    float l = dot(hu, Luma3);
    float3 drained = saturate(DrainTint * (HostLevels.y + (l - HostLevels.x) * DrainFlatten));
    float3 burned = saturate(AshTint * (HostLevels.z + (l - HostLevels.x) * AshFold));
    // [H3] the creature as drawn
    float3 d = hu * tu;

    // [F2] the five texels each territory takes: pages 0 / 1 / 2 at the mask's uv, pages 0 / 1 at the stain's swelled uv
    float4 p0[4], p1[4], p2[4], s0[4], s1[4];
    [unroll] for (int i = 0; i < 4; i++)
    {
        float3 u3 = float3(uv, 1.0);
        float2 mu = float2(dot(MaskU[i].xyz, u3), dot(MaskV[i].xyz, u3));
        float2 ms = (mu - 0.5) * Grow[i].w + 0.5;
        float2 origin = float2(MaskU[i].w, MaskV[i].w);
        p0[i] = Page(origin, 0.0, mu);
        p1[i] = Page(origin, 1.0, mu);
        p2[i] = Page(origin, 2.0, mu);
        s0[i] = Page(origin, 0.0, ms);
        s1[i] = Page(origin, 1.0, ms);
    }

    // [P1] DRAIN / BURN, territory by territory: each copy (x the tint, as the DualTextureEffect did) "over" the host
    [unroll] for (int a = 0; a < 4; a++)
    {
        float rev = Reveal(Grow[a].x, p1[a].a);
        d = lerp(d, drained * tu, p0[a].r * rev * Grow[a].y);
        d = lerp(d, burned * tu, p0[a].g * rev * Grow[a].z);
    }

    // [P2] STAIN: multiply for premultiplied sources, dst x lerp(1, colour, amount x mask)
    d *= Shade;
    [unroll] for (int b = 0; b < 4; b++)
    {
        float rev = Reveal(Grow[b].x, s1[b].a);
        float3 vein = lerp(VeinStain, CharVein, Light[b].z);
        d *= 1.0 - s0[b].b * rev * Stain[b].x * (1.0 - Bruise);
        d *= 1.0 - s0[b].b * rev * Stain[b].y * (1.0 - Bruise);
        d *= 1.0 - s0[b].a * rev * Stain[b].z * (1.0 - vein);
        d *= 1.0 - s1[b].r * rev * Stain[b].w * (1.0 - BurnEdge);
    }

    // [P3] SCREEN: the tissue's glow, the emission (its part above 1 a second draw), the idle accent
    [unroll] for (int c = 0; c < 4; c++)
    {
        float3 emission = lerp(HostEmission, SpectralPale, 0.85 * Light[c].z);
        float3 accent = lerp(emission, Emission, 0.45 * Light[c].z);
        float soft = 1.0 - Light[c].z;
        float3 group = saturate(1.0 - abs(Add[c].w - float3(0.0, 1.0, 2.0)));
        d = ScreenOver(d, TissueGlow * (Light[c].x * p0[c].b));
        d = ScreenOver(d, emission * (min(1.0, Light[c].y) * soft * p1[c].g));
        d = ScreenOver(d, emission * (saturate(Light[c].y - 1.0) * soft * p1[c].g));
        d = ScreenOver(d, accent * (Light[c].w * dot(p2[c].rgb, group)));
    }

    // [P4] ADDITIVE: the fissures, a dying host's flash, the bloom front, the travelling puffs
    [unroll] for (int e = 0; e < 4; e++)
    {
        float3 emission = lerp(HostEmission, SpectralPale, 0.85 * Light[e].z);
        float3 hot = lerp(EmissionHot, SpectralHot, 0.55 * Light[e].z);
        float band = max(0.0, 1.0 - abs(Grow[e].x - p1[e].a * BirthScale) / FrontBand);
        float front = min(1.0, band * p2[e].a * FrontScale);
        d += emission * (Add[e].x * p1[e].b + Add[e].z * p1[e].g) + hot * (Add[e].y * front);
    }
    float2 px = uv * UvToPx.xy + UvToPx.zw;
    [unroll] for (int f = 0; f < 4; f++)
    {
        float2 off = (px - PuffAt[f].xy) * PuffAt[f].z;
        float q = max(0.0, 1.0 - dot(off, off));
        d += PuffColour[f].rgb * (q * q);
    }
    d = saturate(d);

    // [K] THE PASS IS THE CREATURE'S ONLY DRAW (the arena skips it): every texel is composited once, at the sprite's own
    // alpha A = host.a x tint.a. The corrupted host replaces the plain one by the old stencil's threshold as a narrow
    // smooth ramp: under it the creature exactly as the arena draws it (host x tint), above it the curse
    float cov = smoothstep(CoverageLo, CoverageHi, host.a);
    float A = host.a * tint.a;
    return float4(lerp(hu * tu, d, cov) * A, A);
}

technique BrandCurse
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
