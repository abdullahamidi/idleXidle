# IMPLEMENTATION_GUIDE

## Package 08 — Branding, Glyphs & Symbols

This package contains import-ready branding and symbolic UI assets for **IdleXIdle — Resonance Hunter**.

## Included groups
- Branding: emblem, wordmark, horizontal and stacked logo variants
- Source glyphs: Body, Mind, Nature, Machine, Shadow, Spirit
- Role glyphs: Attacker, Defender, Support, Crafter, Producer
- Vow sigils: six neutral sigils (`vow_01`–`vow_06`) ready to map to your final Vow definitions
- Navigation glyphs: Hunt, Warren, Forge, Evolve, Codex, Relics, Inventory, Shop, Mail, Settings
- State glyphs: Resonance, Corruption, Ward, Binding, Mastery, Automation, Prestige, Evolution

Every glyph is provided as:
- `color` — full-color version
- `mask` — white alpha mask for runtime tinting
- `medallion` — ready-made ornate circular UI version
- sizes: `512`, `256`, `128`, `64`

## Recommended MonoGame loading
Add PNG files to the MGCB content project and load with `Content.Load<Texture2D>()`.

Example:
```csharp
var sourceNature = Content.Load<Texture2D>(
    "assets/glyphs/source/color/source_nature_128");
```

## Color vs mask
Use the full-color asset when the visual identity is fixed. Use the mask variant when you want to tint based on state:
```csharp
spriteBatch.Draw(maskTexture, position, null, selected ? Color.Gold : Color.Gray);
```

## Medallion variants
Medallions are intended for:
- navigation buttons
- region/source selectors
- codex filters
- mastery and progression badges
- boss/creature metadata cards

Recommended display sizes at 1920x1080:
- compact HUD: 48–64 px
- navigation: 72–96 px
- large selection card: 128–160 px

## Logo usage
- `logo_horizontal_full.png` — splash screen, title screen, marketing header
- `logo_horizontal_compact.png` — menu header
- `logo_stacked.png` — square title card / store artwork base
- `logo_wordmark.png` — text-only placement
- `logo_emblem_*` — executable icon, loading indicator, save slot badge

Preserve logo aspect ratio. Do not stretch independently on X/Y.

## Atlas support
The `assets/atlases` folder contains optional 256px atlases. The order and frame layout are recorded in `metadata/symbol_catalog.json`. Individual PNG files remain the recommended first implementation path for a new MonoGame project.

## Vow sigils
The repository search did not expose stable Vow names, so the package deliberately uses neutral IDs `vow_01` through `vow_06`. Map them in your game data rather than hardcoding assumed names.

## Premultiplied alpha
All PNGs use transparent backgrounds and are suitable for MonoGame's normal `BlendState.AlphaBlend`. Additive blending is not required for these symbols.

## Suggested layering
1. button/panel background
2. medallion or slot frame
3. glyph
4. selection glow / hover overlay
5. label text
