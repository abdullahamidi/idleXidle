# IMPLEMENTATION_GUIDE

## Package 02A — Hunter Animation Expansion
This package expands the Hunter into frame-by-frame flipbook animations.

### Included animations
- `hunter_idle` (8 frames, loop)
- `hunter_attack` (8 frames)
- `hunter_cast` (8 frames)
- `hunter_hurt_recover` (8 frames)
- `hunter_death` (8 frames)

Each animation includes native cropped frames, normalized `512x512` frames, a horizontal strip, and JSON metadata.

## Use with MonoGame
This package is designed to plug into the runtime provided in **Package 06 — VFX + Animation Runtime**. Load the strip texture and the matching JSON metadata, then animate by source rectangle.

## Layout / pivot
All normalized frames are grounded to a shared baseline and use a bottom-center origin. Recommended origin: `new Vector2(256, 450)`.

## Suggested state machine
- `Idle` → default loop
- `Attack` → play on basic weapon attack; return to idle when finished
- `Cast` → use for skills / channel opener
- `HurtRecover` → use on hit / stagger
- `Death` → play once, then remain on final frame

## Suggested FPS
- Idle: 12
- Attack: 16
- Cast: 14
- HurtRecover: 14
- Death: 12

## Notes
The sheet was generated as a clean frame-by-frame sequence rather than a concept board. For ultimate polish, you can later add equipment-specific attack variants, but this package is fully usable as-is.
