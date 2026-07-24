# IMPLEMENTATION_GUIDE

## Package 04A — Boss Animation Expansion
This package expands all six bosses with frame-by-frame idle and attack sequences.

### Included bosses
- Void Reaper: `idle`, `attack`
- Crystal Lich: `idle`, `attack`
- Spirit Matron: `idle`, `attack`
- Thorn Regent: `idle`, `attack`
- Lumen Angel: `idle`, `attack`
- Forge Colossus: `idle`, `attack`

Each animation provides 8 native frames, 8 normalized `1024x1024` frames, a horizontal strip, and metadata JSON.

## Runtime usage
Use the Package 06 runtime helpers. Boss normalized frames are larger (`1024x1024`) and should be drawn with a bottom-center origin. Recommended origin: `new Vector2(512, 900)`.

## Suggested sequencing
- idle: loop continuously with subtle breathing / motion
- attack: play for the boss primary attack, then return to idle
- combine attack peaks with VFX from Package 06 (slash, aura, binding, impact, etc.)

## Scale guidance
Starting runtime scales for 1920x1080:
- Void Reaper / Crystal Lich / Spirit Matron / Thorn Regent / Lumen Angel: 0.55–0.8
- Forge Colossus: 0.7–0.95

## Notes
This package focuses on core flipbook boss states. Special attacks, hurt, and defeat expansions can be generated later as a follow-up `04B` if you want.
