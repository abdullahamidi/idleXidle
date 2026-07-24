# MASTER IMPLEMENTATION GUIDE

## 1. What this package contains

Package 09 is the consolidated delivery for Resonance Hunter. It contains UI, Hunter character art, normal enemies, bosses, items, frame-by-frame animation expansions, VFX, environments, branding, and glyphs.

The generated asset registry currently contains **1,817 valid PNG files**.

## 2. Recommended project layout

Copy these folders into or beside your MonoGame project:

```text
YourGame/
├── Content/
│   ├── ResonanceHunterAssets.mgcb
│   ├── UI/
│   ├── Characters/
│   ├── Animations/
│   ├── Enemies/
│   ├── Bosses/
│   ├── ItemsLoot/
│   ├── VFX/
│   ├── Environments/
│   └── BrandingSymbols/
├── Metadata/
└── Runtime/
```

Open `Content/ResonanceHunterAssets.mgcb` with MGCB Editor and build it. The file contains one TextureProcessor entry for every runtime PNG.

## 3. Content loading

MGCB content names are the file paths below `Content`, without the `.png` extension.

```csharp
Texture2D panel = Content.Load<Texture2D>("UI/panels/ui_panel_large");
Texture2D hunter = Content.Load<Texture2D>("Characters/Hunter/poses/normalized/hunter_idle");
Texture2D slashStrip = Content.Load<Texture2D>("VFX/slash/void/slash_void_strip8_512");
```

Use forward slashes in content names.

## 4. Frame-by-frame animation

The animation strips are under:

- `Animations/Hunter`
- `Animations/Enemies`
- `Animations/Bosses`
- `VFX`

Matching definitions are under `Metadata`. Package 06 runtime helpers are copied to `Runtime/AnimationVFX`.

Normalized animations share consistent canvas sizes and origins:

- Hunter and regular enemies: usually 512×512, bottom-center grounding
- Bosses: usually 1024×1024, bottom-center grounding
- VFX: usually 512×512, centered or grounded according to metadata

## 5. Draw passes

Use at least two SpriteBatch passes:

```csharp
spriteBatch.Begin(
    blendState: BlendState.AlphaBlend,
    samplerState: SamplerState.LinearClamp);

// Environment, characters, UI, smoke, solid sprites.

spriteBatch.End();

spriteBatch.Begin(
    blendState: BlendState.Additive,
    samplerState: SamplerState.LinearClamp);

// Glows, magic, light rays, additive VFX.

spriteBatch.End();
```

The VFX metadata identifies the recommended blend mode.

## 6. Environment rendering

Environment helpers are under `Runtime/Environments`. Draw parallax layers back-to-front, then corruption/mastery overlays, then characters and foreground layers.

Recommended order:

1. far layer
2. mid layer
3. near layer
4. environment overlays behind gameplay
5. characters, enemies, bosses
6. foreground environment layer
7. front VFX
8. UI

## 7. UI

Panel and button textures should be drawn with 9-slicing. The UI package guide under `Docs/Packages/01_ui` includes recommended border insets.

Render text in-engine rather than baking labels into textures. No font files are included in this bundle.

## 8. Asset registry

`Metadata/asset_registry.json` records:

- MGCB content path
- source file path
- category
- dimensions
- alpha availability
- file size
- SHA-256 checksum

`Runtime/AssetRegistryLoader.cs` is included for optional runtime/editor tooling.

## 9. Production notes

- Keep a stable virtual resolution of 1920×1080 and scale the final render target to the window.
- Use `LinearClamp` for painted assets and effects. `PointClamp` is suitable only when you intentionally want hard pixel edges.
- Do not load every texture at startup. Split content by screen/region or implement an asset cache.
- Large 1024×1024 boss strips can consume substantial VRAM. Load them only for the active boss encounter and unload when leaving.
- The `Reference` directory is not needed in the shipped build.

## 10. Verification

See:

- `Docs/QA_REPORT.md`
- `Build/CHECKSUMS.sha256`
- `Metadata/package_index.json`
