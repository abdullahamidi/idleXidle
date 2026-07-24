# IMPLEMENTATION_GUIDE — Package 02: Hunter Character

## Package scope
This package contains **108 separate PNG assets** for the approved Hunter direction:
- seven gameplay poses
- six cosmetic/material variants
- four weapon families with five designs each
- three armor sets split into pieces
- helmets, shoulders, capes, gloves, boots, and accessories
- portrait and UI/category icons
- normalized sprite canvases, silhouette, and ground shadow

Target game resolution: **1920x1080 (16:9)**.

## Runtime-ready pose files
Use the files under `assets/poses/normalized/` for direct gameplay drawing.
They use a common **512x512 canvas** and a recommended origin of **(256, 492)**.

Main state mapping:
- `hunter_idle.png`
- `hunter_move_01.png`
- `hunter_move_02.png`
- `hunter_attack_01.png`
- `hunter_attack_02.png`
- `hunter_hurt.png`
- `hunter_defeated.png`

## Recommended state timing
- idle breathing loop: idle → move_01 → idle → move_02, 0.18–0.28 s each
- attack: attack_01 for 0.08–0.12 s, attack_02 for 0.10–0.16 s, then idle
- hurt: 0.12–0.20 s
- defeated: terminal pose

These are key poses. Add position/scale/rotation tweening between poses instead of treating them as a high-frame-count cinematic animation.

## Render size at 1920x1080
- combat character height: **260–340 px**
- character/equipment screen: **480–680 px**
- portrait: use `assets/portraits/hunter_portrait.png`

## Draw order
1. `hunter_ground_shadow.png`
2. selected Hunter pose
3. separately attached weapon/equipment
4. attack trails / additive glow
5. hit flash
6. foreground particles

## MonoGame example
```csharp
Texture2D hunter = assets.Get("hunter_idle");
Vector2 origin = new(256f, 492f);

spriteBatch.Begin(
    SpriteSortMode.Deferred,
    BlendState.AlphaBlend,
    SamplerState.LinearClamp);

spriteBatch.Draw(
    hunter,
    worldPosition,
    sourceRectangle: null,
    color: Color.White,
    rotation: 0f,
    origin: origin,
    scale: scale,
    effects: facingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
    layerDepth: 0.5f);

spriteBatch.End();
```

## Variants
`assets/variants/normalized` contains full-body skin/material variants with the same 512x512 canvas and origin.
Suggested use:
- default — base Hunter
- ember — Body/fire state
- forest — Nature
- frost — Mind
- void — Shadow empowered
- golden — Spirit/legendary

## Weapons
Weapon textures are tightly cropped and separated by family:
- spear
- scythe
- bow
- blade

Store a grip point and rotation offset in data rather than assuming all weapons share one pivot:
```csharp
public sealed record WeaponVisual(
    string TextureKey,
    Vector2 GripPoint,
    float RotationOffset,
    float Scale);
```

## Equipment pieces
Equipment PNGs are ready to import for:
- inventory and item cards
- paper-doll customization
- equipment previews
- manually attached sprite layers

Because the source designs are individually cropped, attachment transforms must be tuned per pose:
```csharp
public sealed record AttachmentTransform(
    Vector2 Offset,
    float Rotation,
    float Scale,
    SpriteEffects Effects);
```

Recommended layer order:
1. cape
2. legs/boots
3. chest
4. shoulders
5. gloves
6. helmet
7. accessory
8. weapon

## Shadow
`hunter_ground_shadow.png` is a reusable soft ellipse.
Render it at 55–75% of the character width and lower its opacity during lunges or airborne motion.

## Texture settings
- PNG with transparency
- premultiplied-alpha-friendly
- `LinearClamp` recommended for the hand-painted style
- mipmaps usually off for fixed-resolution 2D UI/gameplay sprites

## Folder import suggestion
```text
Content/Art/Hunter/
  Poses/
  Variants/
  Weapons/
  Equipment/
  Portraits/
  Icons/
```

## Important limitation
The normalized full-body poses are the primary directly drawable gameplay assets. Individual armor/equipment pieces are importable modular assets, but require per-pose attachment tuning; they are not guaranteed to align as zero-configuration overlays.

The original reference board is included only in `preview/` and should not be loaded at runtime.
