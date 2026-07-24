# IMPLEMENTATION_GUIDE — Package 03: Enemy Creatures

## Scope
This package contains **15 non-boss enemy archetypes** with separate transparent PNG animation states, portraits, silhouettes, and ground shadows. Bosses are intentionally excluded and belong to Package 04.

The package includes two versions of every gameplay frame:

- `native/`: tightly cropped source sprite for atlasing or custom scaling.
- `normalized/`: ready-to-use `512x512` canvas with a shared bottom-center ground anchor.

## Standard gameplay orientation
The enemies are authored for a side-view combat composition where the player is generally on the left and enemies occupy the right half of the arena. Enemy art should therefore face toward the player. Do not horizontally flip individual animation frames inside one animation; flip the entire entity consistently when required by encounter layout.

## Shared pivot and anchor
Use this pivot for every normalized gameplay sprite:

```text
Normalized canvas: 512 x 512
Pivot:             (0.5, 0.91796875)
Pivot in pixels:   (256, 470)
Meaning:           bottom-center / feet or contact point
```

Draw the corresponding ground shadow at the same world X and slightly below the sprite contact point.

## Recommended on-screen sizes at 1920x1080

| Category | Approximate display height |
|---|---:|
| Small common enemy | 90–145 px |
| Medium common enemy | 135–210 px |
| Large common / defender | 190–270 px |
| Elite enemy | 250–360 px |

Use the same world-to-screen scale for all normalized sprites. Their internal transparent padding preserves relative alignment.

## Suggested gameplay mapping
The package includes suggested `Source` and `Role` metadata in `docs/asset_manifest.json`. These mappings are recommendations rather than hard engine dependencies.

- Shape should communicate the primary combat role.
- Source color can be reinforced with VFX, UI glyphs, and arena lighting.
- Do not use color as the only gameplay signal; pair it with the role silhouette and a source badge.

## Animation state machine
Use the available frames as follows:

```text
Idle       -> loop
Move       -> loop
Attack     -> play once, then Idle
Slam       -> play once, then Idle
Drain      -> hold or loop during channel, then Idle
Cast       -> play once, trigger projectile/event on authored frame
Dash       -> play once while movement is applied
Channel    -> loop while channeling
Die        -> play once, freeze final frame or remove entity
Projectile -> spawn a projectile entity; do not draw as the creature itself
```

Common enemies contain two visual frames for each listed action. Elite enemies have one authored key pose per action. For elite motion, add small engine-side transforms—position easing, scale anticipation, hit-stop, and additive VFX—rather than inventing extra sprite substitutions.

## Timing recommendations

| State | Frame duration | Loop |
|---|---:|---|
| Idle | 0.32–0.46 s | Yes |
| Move | 0.14–0.22 s | Yes |
| Attack | 0.09–0.15 s | No |
| Cast / Drain / Channel | 0.12–0.20 s | Conditional |
| Die | 0.14–0.22 s | No |

For two-frame attacks, spend more time on anticipation and less on the impact frame. The actual damage event should be emitted close to the start of the second attack frame.

## MonoGame content setup
Recommended MGCB settings:

```text
Importer: TextureImporter
Processor: TextureProcessor
ColorKeyEnabled: False
GenerateMipmaps: False
PremultiplyAlpha: True
ResizeToPowerOfTwo: False
TextureFormat: Color
```

For normal rendering:

```csharp
_spriteBatch.Begin(
    SpriteSortMode.Deferred,
    BlendState.AlphaBlend,
    SamplerState.LinearClamp,
    DepthStencilState.None,
    RasterizerState.CullNone);
```

Use `PointClamp` only when deliberately targeting a harder pixel-like presentation. These assets are painted and generally look better with `LinearClamp`.

## Example sprite draw

```csharp
public static void DrawEnemy(
    SpriteBatch batch,
    Texture2D texture,
    Vector2 groundPosition,
    float scale,
    bool faceLeft,
    Color color)
{
    var origin = new Vector2(256f, 470f);
    var effects = faceLeft
        ? SpriteEffects.None
        : SpriteEffects.FlipHorizontally;

    batch.Draw(
        texture,
        groundPosition,
        sourceRectangle: null,
        color,
        rotation: 0f,
        origin,
        scale,
        effects,
        layerDepth: 0f);
}
```

## Ground shadow
Each enemy has a shared-size `256x96` shadow texture. Draw it first:

```csharp
var shadowOrigin = new Vector2(128f, 48f);
batch.Draw(
    shadowTexture,
    groundPosition + new Vector2(0f, 5f),
    null,
    Color.White * 0.75f,
    0f,
    shadowOrigin,
    enemyScale,
    SpriteEffects.None,
    0f);
```

Scale the shadow width per archetype when desired. Flying creatures should use lower opacity and a larger vertical gap.

## Draw order

1. arena background
2. distant environmental effects
3. enemy ground shadows
4. enemy bodies
5. enemy weapon/projectile attachment
6. source glyph / weak-point overlay
7. hit flashes and combat VFX
8. health bars and status icons
9. damage numbers

## Hit feedback
Avoid replacing the sprite with an unrelated image. Recommended feedback:

- 55–90 ms white or source-colored flash
- 2–6 px positional recoil
- brief scale compression on heavy hits
- impact VFX from the later effects package
- optional silhouette-only flash using `assets/silhouettes`

## Portraits
`assets/portraits` contains `256x256` transparent portraits derived from the same enemy art. They are intended for:

- enemy inspection panels
- health bar medallions
- bestiary / codex cards
- automation worker cards when an enemy becomes bound

Place the portrait inside the UI medallions delivered with Package 01.

## Texture atlasing
For production, atlas by enemy or tier rather than placing the entire package in one giant atlas:

```text
enemies_common_1_atlas
 enemies_common_2_atlas
 enemies_elite_atlas
 portraits_atlas
```

Use at least 4–8 px transparent padding around packed frames to avoid texture bleeding.

## Data-driven animation definition
The JSON manifest lists every action and filename. A compact runtime definition can look like:

```csharp
public sealed record EnemyAnimationDef(
    string Name,
    IReadOnlyList<string> Frames,
    float SecondsPerFrame,
    bool Loop);
```

Keep animation timing in data rather than hard-coding per enemy.

## Important limitation
This package provides the complete normal-enemy starter roster represented by the generated production set. It does not contain the six region bosses. It also does not force one unique creature for every possible `Source × Role × evolution stage` combination; use the supplied mapping as a starting roster and expand the same naming/pivot standard in later content passes.
