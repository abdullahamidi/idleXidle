# IMPLEMENTATION_GUIDE

## Package
`package_01_ui_kit` — dark mythic primal UI starter kit for **Resonance Hunter**.

## Included scope
This first UI package includes:
- panel frames
- buttons and tab buttons
- UI bars and matching fill strips
- rarity / slot frames
- generic medallions and decorative UI parts
- keycap frames, dividers, corners, and scrollbar parts

Total assets: **47**
Target reference resolution: **1920x1080 (16:9)**.

## Folder structure
- `assets/panels` — windows, cards, containers
- `assets/buttons` — action buttons and tab buttons
- `assets/bars` — bar frames and fill strips
- `assets/slots` — rarity frames and equipment/inventory slots
- `assets/icons` — generic medallion/icon holders
- `assets/decor` — dividers, corners, keycaps, scrollbar parts
- `preview` — source reference sheets used to build the package

## General import settings for MonoGame
- Texture format: PNG
- Alpha: premultiplied alpha friendly workflow
- Sampler state recommendation: `SamplerState.PointClamp` for crisp UI or `LinearClamp` if you prefer slightly smoother scaling
- Keep textures in a dedicated UI atlas later if you want fewer draw calls.

## Recommended draw order
1. world/background
2. dim overlays / vignette
3. panel frames
4. slot frames / medallions
5. bars and fills
6. button frames
7. icons / glyphs / text
8. tooltips / modal windows

## 9-slice guidance
The following assets are intended for sliced / scalable use:

### Panels
Use these as 9-slice with approximate border insets:
- `ui_panel_large`, `ui_panel_medium`, `ui_panel_small`, `ui_panel_square`, `ui_panel_vertical`, `ui_panel_modal_wide`
- Recommended border inset: **32 px** on each side for large/medium panels; **24 px** for small panel.
- Preserve corners; only stretch the center and edge bands.

### Buttons
Use these as horizontal-slice / 9-slice buttons:
- `ui_button_primary`, `ui_button_secondary`, `ui_button_disabled`, `ui_button_danger`, `ui_button_confirm`
- Recommended border inset: **20 px left/right**, **14 px top/bottom**.
- Render label text separately in code.

### Tabs
- `ui_tab_active`, `ui_tab_inactive`
- Use the same 20/14 inset rule.

## Bars
Each bar family has a **frame** asset and a **fill** asset.
Recommended use:
- draw frame first
- compute fill width as `current / max`
- draw the fill clipped inside the safe interior rect

Suggested pairs:
- `ui_bar_health_frame` + `ui_bar_health_fill`
- `ui_bar_mana_frame` + `ui_bar_mana_fill`
- `ui_bar_boss_frame` + `ui_bar_boss_fill`
- `ui_bar_progress_frame` + `ui_bar_progress_fill`
- `ui_bar_xp_frame` + `ui_bar_xp_fill`

Recommended fill padding inside frame:
- health/mana/progress/xp: **10 px left/right**, **8 px top/bottom**
- boss bar: **12 px left/right**, **10 px top/bottom**

## Slots and rarity frames
Use cases:
- `ui_frame_rarity_common` → item card / inventory rarity frame
- `ui_frame_rarity_uncommon`
- `ui_frame_rarity_rare`
- `ui_frame_rarity_epic`
- `ui_frame_rarity_legendary`
- `ui_slot_empty` → normal inventory / equipment slot
- `ui_slot_locked` → locked slot indicator
- `ui_slot_trinket_round` → trinket / relic / gem slot
- `ui_slot_skill_hex` → skill / rune / hex-node slot

Suggested slot sizes on 1920x1080 UI:
- inventory slot: 72–96 px
- equipped gear slot: 96–128 px
- large highlighted slot: 128–160 px

## Decorative assets
- `ui_keycap_square_blank` — render keyboard key text (Q, E, R, 1, 2, etc.) separately
- `ui_keycap_space_blank` — wide spacebar-like keycap
- `ui_medallion_round` — icon holder / category badge / portrait frame base
- `ui_medallion_hex` — skill icon or hex-tech slot base
- `ui_divider_long`, `ui_divider_short` — section separators
- `ui_corner_top_left`, `ui_corner_top_right`, `ui_corner_bottom_left`, `ui_corner_bottom_right` — build decorative modal corners or screen framing
- `ui_scrollbar_track`, `ui_scrollbar_handle` — list / inventory scrollbars

## Safe-area layout recommendations (1920x1080)
- outer action-safe margin: **64 px**
- text/title safe margin: **96–128 px**
- top-left player HUD cluster: inside 64 px safe margin
- bottom-center action bar: reserve roughly 540–760 px width depending on skill count
- right-side quest/log panels: 320–420 px width works well with the medium and vertical panel assets

## Example usage patterns
### Inventory
- modal base: `ui_panel_modal_wide`
- left grid: `ui_slot_empty` or rarity frames
- right tooltip: `ui_panel_vertical` or `ui_panel_medium`
- buttons: `ui_button_confirm`, `ui_button_secondary`

### Combat HUD
- player block: `ui_medallion_round` + health/mana bars
- boss area: `ui_bar_boss_frame` / fill
- abilities: `ui_slot_skill_hex`
- consumables: `ui_keycap_square_blank` or `ui_slot_empty`

### Warren / management screen
- root window: `ui_panel_large`
- side column: `ui_panel_vertical`
- section titles separated by `ui_divider_long`
- worker rows inside `ui_panel_medium` / `ui_panel_small`

## MonoGame implementation notes
### Drawing a 9-sliced panel
If you already have a 9-slice helper, use the panel textures directly. If not, divide the texture into:
- 4 corners
- 4 edges
- 1 center

Keep corner sizes fixed and stretch edges/center only.

### Drawing a clipped fill bar
1. draw frame
2. calculate inner rect
3. calculate `filledWidth = innerRect.Width * percent`
4. draw the fill texture clipped to `filledWidth`

### Suggested naming convention for code
- panels: `UiPanelLarge`, `UiPanelMedium`, etc.
- buttons: `UiButtonPrimary`, `UiButtonDanger`, etc.
- bars: `UiBarHealthFrame`, `UiBarHealthFill`, etc.

## Notes / limitations
- This package is a **starter UI kit** generated from the selected art direction.
- Text labels are intentionally excluded from most assets; render text in engine for flexibility and localization.
- Corners, keycaps, and dividers are best reused compositionally rather than stretched arbitrarily.
