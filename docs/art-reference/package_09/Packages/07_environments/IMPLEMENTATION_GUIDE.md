# IMPLEMENTATION_GUIDE

## Package 07 — Backgrounds & Environments
Target resolution: **1920×1080 (16:9)**.

## Regions
- Body — Blood Moors
- Mind — Crystal Caverns
- Nature — Verdant Hollow
- Machine — Forge Wastes
- Shadow — Void Wastes
- Spirit — Twilight Sanctum

Each region includes clean, unconquered and mastered full-screen backgrounds; four transparent parallax layers; fog, corruption, mastery, particle, light-ray and vignette overlays; and JSON metadata.

## Render order
1. far layer
2. mid layer
3. near layer
4. actors and gameplay effects
5. foreground layer
6. fog and vignette using `BlendState.AlphaBlend`
7. mastery glyphs, light rays and particles using `BlendState.Additive`
8. HUD

## Recommended parallax factors
- far: 0.12
- mid: 0.32
- near: 0.62
- foreground: 1.00

The layers use overlapping soft vertical masks; keep camera displacement subtle.

## Ground and combat safe area
- Ground baseline: `Y = 900`
- Combat area: `Rectangle(160, 560, 1600, 360)`

## Mastery transition
Cross-fade `unconquered` to `mastered` over 1.2–1.8 seconds. Fade out the corruption overlay and fade in mastery/light-ray overlays. Combine with the binding and level-up effects from Package 06.

## Scaling
Render gameplay to a fixed 1920×1080 `RenderTarget2D`, then scale or letterbox the target for other display resolutions. Use `SamplerState.LinearClamp`.
