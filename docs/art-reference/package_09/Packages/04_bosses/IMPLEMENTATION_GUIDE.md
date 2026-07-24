# IMPLEMENTATION_GUIDE

## Package 04 — Bosses
This package contains six boss families prepared as importable PNG assets for **Resonance Hunter**:
- Thorn Regent
- Crystal Lich
- Forge Colossus
- Void Reaper
- Lumen Angel
- Spirit Matron

## Included per boss
For each boss, the package includes:
- 5 gameplay pose PNGs: `idle`, `attack`, `special`, `hurt`, `binding`
- 5 companion assets: `portrait_square`, `portrait_bust`, `silhouette`, `glyph`, `shadow`
- 5 normalized gameplay canvases at `1024x1024`

This gives a total of **90 PNG files**.

## Folder layout
- `assets/bosses/<boss>/` — native cropped gameplay poses
- `assets/bosses/<boss>/normalized/` — normalized `1024x1024` pose canvases with bottom-center grounding
- `assets/portraits/` — square and bust portraits for UI and codex use
- `assets/glyphs/` — boss sigils / glyphs
- `assets/silhouettes/` — silhouette readability references
- `assets/shadows/` — ground shadow assets
- `preview/` — source reference sheets used to extract the assets

## Recommended MonoGame import settings
- format: PNG
- alpha workflow: premultiplied alpha friendly
- `SamplerState.PointClamp` for crisp UI / pixel-aligned composition, or `LinearClamp` if your rendering style is smoother
- for gameplay sprites, use `SpriteSortMode.Deferred`

## Pose usage suggestions
### idle
Default standing pose while the boss is active but not attacking.

### attack
Use for basic strike / lunge / primary attack telegraph.

### special
Use for phase skill, summon, cast, slam, or ultimate attack.

### hurt
Use on stagger, break, or heavy-hit response.

### binding
Use on defeat, banishment, end-of-phase bind, or codex / victory scenes.

## Native vs normalized assets
### Native assets
These are tightly cropped around the art and are best for UI previews, codex entries, or custom placement.

### Normalized assets (`1024x1024`)
These are better for runtime animation/state swapping.
All normalized sprites are:
- centered horizontally
- grounded to a shared bottom line
- exported on transparent background

Recommended runtime use:
- swap `idle` / `attack` / `special` / `hurt` / `binding` on the same draw anchor
- use the same `position` for all states of the same boss

## Recommended pivot / anchor
For gameplay, anchor each normalized boss pose at:
- **pivot:** bottom-center
- `origin = new Vector2(texture.Width / 2f, texture.Height)`

This keeps feet / base contact aligned during state changes.

For portraits and glyphs, use center origin.

## Suggested scale on 1920x1080
These bosses are designed as major screen presences.
Suggested starting scale (normalized assets):
- Thorn Regent: `0.65f – 0.85f`
- Crystal Lich: `0.65f – 0.85f`
- Forge Colossus: `0.80f – 1.00f`
- Void Reaper: `0.70f – 0.90f`
- Lumen Angel: `0.70f – 0.90f`
- Spirit Matron: `0.70f – 0.90f`

Tune relative to arena layout and player size.

## Layering recommendations
1. arena background
2. ambient FX behind boss
3. boss shadow asset
4. boss sprite
5. boss front FX / spell overlays
6. health bar / UI / name

## Boss shadow usage
Each boss includes a dedicated shadow PNG. Draw it first under the boss body with slight opacity (for example `0.35f` to `0.60f`) and modest X/Y offset if needed.

## Portrait usage
- `portrait_square`: boss select, codex grid, UI cards
- `portrait_bust`: dialogue panel, boss intro, codex detail page

## Glyph usage
Use glyphs for:
- boss title card
- health bar badge
- codex section header
- defeat screen
- map marker / altar marker

## Naming convention
Examples:
- `assets/bosses/void_reaper/void_reaper_idle.png`
- `assets/bosses/void_reaper/normalized/void_reaper_idle_1024.png`
- `assets/portraits/void_reaper_portrait_square.png`
- `assets/glyphs/void_reaper_glyph.png`

## Implementation note
These assets are single-pose state illustrations rather than frame-by-frame animations. In MonoGame, treat them as state sprites or key-art boss states. You can later add VFX layers, tweening, breathing motion, or cutout animation on top.
