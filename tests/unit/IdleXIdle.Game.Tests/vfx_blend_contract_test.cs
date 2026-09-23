using System;
using System.IO;
using System.Linq;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE VFX BLEND CONTRACT — a premultiplied texel adds its own light ONCE (2026-09-23).
/// </summary>
/// <remarks>
/// <para>
/// The regression this file exists to stop: someone "simplifies" <see cref="VfxBlend.PremultipliedAdditive"/>
/// back to <see cref="BlendState.Additive"/>. That blend multiplies the source by its alpha, and
/// <c>AssetLibrary</c> has already multiplied every texel by its alpha at load, so a soft pixel's light
/// becomes alpha SQUARED. One-bit art hid it for months (0² = 0, 1² = 1); the first strip with honest
/// partial alpha — the regenerated Seeker strike — came out three or four faint streaks in the arena.
/// </para>
/// <para>
/// Nothing here needs a graphics device: a <see cref="BlendState"/> is plain configuration until it is
/// bound, so the factors are read, and the blend equation is evaluated in arithmetic.
/// </para>
/// </remarks>
public class VfxBlendContractTest
{
    /// <summary>What a fixed-function blend factor multiplies a source colour channel by.</summary>
    private static float Factor(Blend factor, float srcAlpha) => factor switch
    {
        Blend.One => 1f,
        Blend.Zero => 0f,
        Blend.SourceAlpha => srcAlpha,
        Blend.InverseSourceAlpha => 1f - srcAlpha,
        _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, "not a factor this test models"),
    };

    /// <summary>
    /// The light one texel adds over a black floor, exactly as the GPU composes it: the texture as
    /// <c>AssetLibrary</c> leaves it (premultiplied), times the vertex colour SpriteBatch passes, through
    /// the state's colour source factor.
    /// </summary>
    private static float AddedLight(BlendState state, float texelAlpha, Color drawColour)
    {
        var texelRgb = 1f * texelAlpha;                      // a WHITE texel, premultiplied at load
        var srcRgb = texelRgb * (drawColour.R / 255f);
        var srcA = texelAlpha * (drawColour.A / 255f);
        return srcRgb * Factor(state.ColorSourceBlend, srcA);
    }

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    [Fact]
    public void test_vfx_blend_factors_are_one_and_one()
    {
        var s = VfxBlend.PremultipliedAdditive;

        Assert.Equal(Blend.One, s.ColorSourceBlend);
        Assert.Equal(Blend.One, s.ColorDestinationBlend);
        Assert.Equal(BlendFunction.Add, s.ColorBlendFunction);
        Assert.Equal(Blend.One, s.AlphaSourceBlend);
        Assert.Equal(Blend.One, s.AlphaDestinationBlend);
        Assert.Equal(BlendFunction.Add, s.AlphaBlendFunction);
    }

    [Fact]
    public void test_a_half_alpha_texel_adds_half_its_light_not_a_quarter()
    {
        // THE BUG, AS ARITHMETIC. Under the stock additive blend a half-transparent white texel adds a
        // quarter of its light; under the contract it adds half. That factor of two at alpha 0.5 — and
        // of five at 0.2 — is the whole difference between a soft falloff and a vanished one.
        Assert.Equal(0.25f, AddedLight(BlendState.Additive, 0.5f, Color.White), 3);
        Assert.Equal(0.50f, AddedLight(VfxBlend.PremultipliedAdditive, 0.5f, Color.White), 3);
    }

    [Fact]
    public void test_opaque_texels_add_the_same_light_under_both_blends()
    {
        // ONE-BIT ART IS UNCHANGED. Most of the library is alpha 0 or 255, and for those texels the two
        // blends agree exactly — at full strength and at every opacity the draw colour carries, because
        // VfxBlend.Light keeps the draw colour's own response.
        foreach (var opacity in new[] { 1f, 0.8f, 0.6f, 0.4f, 0.1f })
        {
            var tint = Color.White * opacity;
            Assert.Equal(AddedLight(BlendState.Additive, 1f, tint),
                         AddedLight(VfxBlend.PremultipliedAdditive, 1f, VfxBlend.Light(tint)), 2);
            Assert.Equal(0f, AddedLight(VfxBlend.PremultipliedAdditive, 0f, VfxBlend.Light(tint)));
        }
    }

    [Fact]
    public void test_the_draw_opacity_keeps_the_curve_it_was_tuned_under()
    {
        // THE SHIELD BREATHES AT 0.40-0.60 AND THE TAIL FADE IS A CUBE — numbers chosen by eye while the
        // draw colour's opacity acted on light SQUARED. Light() keeps that response, so the correction
        // touches the texture's alpha and nothing the spawn sites tuned. Full strength is untouched.
        Assert.Equal(Color.White, VfxBlend.Light(Color.White));
        var half = VfxBlend.Light(Color.White * 0.5f);
        Assert.InRange(half.R, 62, 65);          // 0.5² of 255
        var tinted = VfxBlend.Light(new Color(0xE8, 0x4A, 0x5E));
        Assert.Equal(new Color(0xE8, 0x4A, 0x5E), tinted);   // an opaque Source tint passes unchanged
    }

    [Fact]
    public void test_the_vfx_pass_begins_with_the_contract_blend()
    {
        // THE WIRE. A correct BlendState that nothing binds is this project's oldest failure mode.
        var player = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "VfxPlayer.cs"));

        Assert.Contains("VfxBlend.PremultipliedAdditive", player, StringComparison.Ordinal);
        Assert.Contains("VfxBlend.Light(", player, StringComparison.Ordinal);
        Assert.DoesNotContain("BlendState.Additive", player, StringComparison.Ordinal);
    }
}
