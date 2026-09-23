using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game.Vfx;

/// <summary>
/// THE VFX BLEND CONTRACT: an effect texel adds its own light, ONCE (ADR-009).
/// </summary>
/// <remarks>
/// <para>
/// Effect strips carry honest straight alpha — the fading post-pass (whiten → glow → soften → feather)
/// exists to give them soft falloff — and <c>AssetLibrary</c> premultiplies every texture at load, so
/// a texel reaches the GPU as <c>rgb·α</c>. The light it should add is therefore exactly that:
/// source factor ONE, destination factor ONE.
/// </para>
/// <para>
/// DO NOT "SIMPLIFY" THIS BACK TO <see cref="BlendState.Additive"/>. That stock state multiplies the
/// source by its alpha (SourceAlpha, One), which is right for STRAIGHT alpha and wrong here: over
/// premultiplied data it applies α a second time, so a texel at α 0.5 adds a quarter of its light and one
/// at α 0.2 adds a twenty-fifth. One-bit art hid that for months (0² = 0, 1² = 1). The first strip with
/// real partial alpha — the regenerated Seeker strike, 2026-09-23 — drew as three faint streaks.
/// </para>
/// <para>
/// The fix lives here and not in the assets: a strip never encodes √α to cancel a renderer quirk.
/// </para>
/// </remarks>
public static class VfxBlend
{
    /// <summary>
    /// Additive for PREMULTIPLIED sources: <c>dst + src</c> on every channel. The alpha factors cannot
    /// change a picture (the canvas is cleared opaque) and are set to the same rule so the state reads
    /// as one contract.
    /// </summary>
    public static readonly BlendState PremultipliedAdditive = new()
    {
        Name = "VfxBlend.PremultipliedAdditive",
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };

    /// <summary>
    /// The draw colour an effect is submitted with: <paramref name="tint"/> scaled by its own opacity,
    /// so a tint at opacity <c>s</c> adds <c>s²</c> of its light.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The old blend squared the TINT's opacity as well as the texel's, and every opacity the spawn sites
    /// hand in was chosen by eye under that response: the standing barrier breathing 0.40–0.60, the field
    /// aura resting at 0.38, the shield break at 0.8, and the cubed tail fade (<see cref="VfxPlayer"/>).
    /// Keeping the response here makes <see cref="PremultipliedAdditive"/> change exactly ONE factor — the
    /// texel's second α — so one-bit art draws identically at every opacity, fade included.
    /// </para>
    /// <para>
    /// Making this linear is a legitimate later decision, but it is a retune of those four numbers, and
    /// it belongs in the same change as them.
    /// </para>
    /// </remarks>
    public static Color Light(Color tint) => tint * (tint.A / 255f);
}
