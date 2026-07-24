# IMPLEMENTATION_GUIDE

## Package 06 — VFX + Animation Runtime
This package provides **frame-by-frame VFX** prepared for MonoGame.

### Included effects
- `slash_nature`
- `slash_void`
- `impact_gold`
- `projectile_arcane`
- `aura_arcane_ring`
- `heal_holy_burst`
- `binding_void_bind`
- `levelup_gold_purple`
- `smoke_puff`
- `loot_pop`

Each effect contains:
- 8 native cropped frames
- 8 normalized `512x512` frames
- one normalized horizontal strip (`8 x 512`)
- one metadata JSON file

## Why this format works in MonoGame
MonoGame does not require Unity-style Animator assets. The most common options are:
1. load individual PNG frames and swap by index, or
2. load one strip texture and draw different `sourceRectangle`s.

This package supports **both**.

## Folder structure
- `assets/vfx/.../frames_native` → original cropped frame-by-frame PNGs
- `assets/vfx/.../frames_512` → runtime-friendly normalized canvases
- `assets/vfx/.../*_strip8_512.png` → single strip texture for source-rectangle animation
- `metadata/*.json` → frame count, fps, loop, blend mode, origin
- `runtime/` → ready-to-use C# helper files for MonoGame
- `preview/` → source sheets and overview preview

## Recommended import/use flow
### Option A — strip animation (recommended)
1. Add the strip PNG to your Content project.
2. Load it with `Content.Load<Texture2D>()`.
3. Load or mirror the matching JSON metadata.
4. Use `SpriteAnimation` + `AnimationPlayer` from the `runtime/` folder.

### Option B — individual frame animation
1. Load each frame from `frames_512`.
2. Store them in a `List<Texture2D>`.
3. Advance the frame index manually.

## Blend mode guidance
Use the `blend` value from each metadata file.

### AlphaBlend
- `slash_nature`
- `smoke_puff`

### Additive
- `slash_void`
- `impact_gold`
- `projectile_arcane`
- `aura_arcane_ring`
- `heal_holy_burst`
- `binding_void_bind`
- `levelup_gold_purple`
- `loot_pop`

Suggested draw passes:
- regular sprites / smoke with `BlendState.AlphaBlend`
- glowing magic / bursts with `BlendState.Additive`

## Origin / pivot guidance
Metadata JSON includes an `origin`.

### Grounded effects
These are intended to sit on the floor / spawn point:
- `aura_arcane_ring`
- `heal_holy_burst`
- `binding_void_bind`
- `levelup_gold_purple`
- `smoke_puff`
- `loot_pop`

They use a **bottom-center** style origin.

### Free-floating effects
These use a centered origin:
- `slash_nature`
- `slash_void`
- `impact_gold`
- `projectile_arcane`

## Suggested gameplay usage
- `slash_nature` → melee / thorn sweep / nature skill
- `slash_void` → dark slash / cursed strike / void skill
- `impact_gold` → hit confirmation / crit impact / reward burst accent
- `projectile_arcane` → arcane bolt travel / mage shot / enemy projectile
- `aura_arcane_ring` → buff circle / cast zone / summon pre-roll
- `heal_holy_burst` → heal cast / revive / blessing pulse
- `binding_void_bind` → boss defeat bind / snare / prison spell
- `levelup_gold_purple` → level-up / mutation choose / mastery gain
- `smoke_puff` → spawn / despawn / hit smoke / environment dust
- `loot_pop` → chest reward / item drop / pickup burst

## Recommended FPS
The package already includes a recommended `fps` in JSON.
Good defaults:
- 12 FPS → looping aura circles
- 14–16 FPS → bursts, projectile effects
- 18–20 FPS → sharp attacks / impacts

## Example MonoGame loading
```csharp
var texture = Content.Load<Texture2D>("assets/vfx/slash/void/slash_void_strip8_512");
var json = TitleContainer.OpenStream("Content/metadata/slash_void.json");
```

You can either parse the JSON yourself or hardcode definitions using the supplied runtime classes.

## Important note about existing character / enemy / boss packages
The earlier gameplay art packages mostly provided **state/key-pose assets**, not full frame-by-frame animation clips. This VFX package is the first package intentionally produced as full frame-by-frame strips.

If you want every moving subject to use this same approach, the next best follow-up packages are:
- `Package 02A — Hunter Animation Expansion`
- `Package 03A — Enemy Animation Expansion`
- `Package 04A — Boss Animation Expansion`
