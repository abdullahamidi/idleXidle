# ADR-009 — A VFX texel adds its own light once: the premultiplied additive blend

| Field | Value |
|---|---|
| **Status** | Accepted 2026-09-23 — the owner approved the renderer contract (`VfxBlend.PremultipliedAdditive` + `VfxBlend.Light`) as an accepted VFX contract, and the second Seeker strike built on it was approved for production. |
| **Date** | 2026-09-23 |
| **Deciders** | user (chose a renderer correction over a √alpha bake in the assets) + technical-artist, lead-programmer |
| **Related** | ADR-003 (Superseded pixel-perfect path; its successor is the LinearClamp + premultiplied-at-load path), `design/art/arena-art-contract.md` §3.6 |
| **Enforced by** | `tests/unit/IdleXIdle.Game.Tests/vfx_blend_contract_test.cs` |
| **Scope** | the VFX pass in `VfxPlayer.DrawPass` only. `FocusRenderer` also begins a `BlendState.Additive` batch; it is not an effect strip and is out of scope here. |

## Summary

Effect strips are authored with **honest straight alpha**. `AssetLibrary` premultiplies every texture at
load, and the VFX pass now blends with `VfxBlend.PremultipliedAdditive` (colour One + One, alpha One +
One). Each texel therefore adds `rgb·α` of light, **once**. The draw colour keeps the opacity curve the
spawn sites were tuned under, through `VfxBlend.Light`.

## Context

The VFX pass began its batch with MonoGame's stock `BlendState.Additive`. Read from the 3.8.4.1 assembly,
that is ColorSrc = **SourceAlpha**, ColorDst = One, AlphaSrc = SourceAlpha, AlphaDst = One, Add.
`Texture2D.FromStream` returns straight alpha (a probe texel stored as 200,100,50,128 loads unchanged),
and `AssetLibrary.Premultiply` turns it into 100,50,25,128. SpriteBatch multiplies the texel by the draw
colour, so the light each effect texel added was

    old   dst + (texel.rgb·α · tint.rgb) × (α · tint.a)   =  rgb · α² · tint.rgb · tint.a

The texture's alpha was applied **twice**. With one-bit art (55 of 67 strips) α² = α, and nothing showed.
The fading post-pass (whiten → glow → soften → feather) exists to produce partial alpha, and the first
strip made with it — the regenerated Seeker strike — was reduced in the arena to three or four faint
streaks. A texel at α 0.5 gave 25 % of its light; one at α 0.2 gave 4 %.

## Decision

    new   dst + texel.rgb·α · Light(tint).rgb
          Light(tint) = tint × tint.a                  (so Light(tint).rgb = tint.rgb · tint.a)
        = rgb · α · tint.rgb · tint.a

1. **`VfxBlend.PremultipliedAdditive`** — colour One/One/Add and alpha One/One/Add. This is the correct
   additive blend for premultiplied data: the source colour already carries its alpha. The alpha
   factors cannot change a picture here (the canvas is cleared opaque), but they are set to the same
   contract so the state reads as one rule.
2. **`VfxBlend.Light(tint)`** multiplies the draw colour by its own alpha. The tint's opacity had also
   been squared by the old blend, and the numbers that ride on it were chosen by eye under that
   response: the barrier's 0.40–0.60 breathing, the aura's 0.38 → 1 pulse, the break's × 0.8, and the
   cubed tail fade (whose light was really k⁶). Keeping that response makes the correction touch
   **exactly one factor**: the texel's second α. For α ∈ {0, 1} the new equation equals the old one at
   every opacity — one-bit art is unchanged in every frame, fade included.
3. **No √α in the assets.** Assets carry the alpha they mean. A strip never encodes a correction for
   the renderer.

## Consequences

- Partial-alpha texels brighten by 1/α: ×2 at α 0.5, ×5 at α 0.2. That is the intended effect on
  `fx_seeker_strike`, and a real change for the strips that already had soft alpha: `fx_aura` (91 % of lit
  pixels partial, mean α 83), `fx_press` (89 %, mean α 228) and `fx_wilt` (62 %). The held field auras are
  the compatibility risk to watch.
- Bilinear sampling makes partial alpha even from one-bit texels wherever a minified edge is sampled,
  so thin rays in old strips gain a little edge brightness. Their opaque interiors are unchanged.
- A future opacity knob should be written knowing that draw-colour opacity acts on light **squared**
  (`Light`). If that curve is ever made linear, retune the four spawn-site numbers above in the same
  change.

## Validation (2026-09-23)

`production/qa/evidence/vfx-blend/README.md`, summarised:
- **One-bit strips:** unchanged (traps +0.1 %, −0.7 %, +0.2 %).
- **Held soft auras:** `fx_press` +0.2 % in game, `fx_aura` ×1.36 mean light in simulation; nothing
  became too bright.
- **Seeker strike:** now reads.
- **Placement:** the placement dump is byte-identical, so no size or timing moved.

## Alternatives rejected

- **Bake √α into the strips** (a fifth post-pass step). It exactly cancels the square, but it makes every
  asset encode a renderer bug and doubles the fix's blast radius the day the blend is corrected. The
  owner rejected it.
- **A bare One/One swap with no `Light`.** Honest, but it also makes every draw-colour opacity linear:
  the standing barrier would be 2.5× brighter at rest (0.40 instead of 0.16) and the aura 2.6×, a
  retune of four approved presentations riding on a texture fix.
- **`BlendState.AlphaBlend`.** Its source factor is One, but the destination is InverseSourceAlpha, so it
  occludes. The effects are additive light, and the "reticles" bug is what drawing them in AlphaBlend
  looked like.
