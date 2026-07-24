# IMPLEMENTATION_GUIDE

## Package 03A — Enemy Animation Expansion
This package expands a core enemy set into frame-by-frame flipbook animations.

### Included enemies and animations
- Bonecrawler: `idle`, `attack`
- Void Spitter: `idle`, `attack`
- Nightstalker: `idle`, `attack`
- Stone Sentinel: `idle`, `slam`

Each animation contains 8 native frames, 8 normalized `512x512` frames, a strip texture, and JSON metadata.

## Use with MonoGame
Use the runtime from Package 06. Each strip texture is intended for source-rectangle animation. For all enemies, use bottom-center origin with normalized frames.

## Suggested combat usage
- Idle loops continuously
- Attack / Slam plays once, then returns to idle
- Void Spitter attack can pair with `projectile_arcane` from Package 06
- Stone Sentinel slam can pair with `impact_gold` or smoke effects for impact emphasis

## Notes
This is the first enemy animation expansion batch. It covers four representative enemies and establishes the final frame-by-frame format for future enemy packs.
