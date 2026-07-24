# ASSET_FAMILY_MAPPING

This file maps each reference screen to the previously generated asset packages.

| Package | Use |
|---|---|
| Package 01 | UI Kit: Panels, buttons, bars, medallions, slots, decorative chrome |
| Package 02 / 02A | Hunter + Hunter Animation: Hunter portrait, full body figure, animation strips |
| Package 03 / 03A | Enemy Creatures + Enemy Animation: Normal enemies and animation strips |
| Package 04 / 04A | Bosses + Boss Animation: Boss artwork and animation strips |
| Package 05 | Items, Loot & Glyphs: Item thumbnails, currencies, materials, loot objects |
| Package 06 | VFX & Animation Runtime: Slashes, impacts, auras, projectiles, smoke, runtime helpers |
| Package 07 | Backgrounds & Environments: Environment backgrounds, parallax layers, overlays |
| Package 08 | Branding, Glyphs & Symbols: Logos, source glyphs, role glyphs, utility symbols |

## 01_main_menu.png
- UI/panels/ui_panel_large, ui_panel_medium, ui_panel_small
- UI/buttons/ui_button_primary, ui_button_secondary
- UI/bars/ui_bar_progress_frame + fill
- Branding/Symbols logo and emblem assets from Package 08
- Environments/shadow_void_wastes_* or other background variant from Package 07

## 02_hunt_auto_combat.png
- Hunter character assets from Package 02 / 02A
- Enemy creature assets from Package 03 / 03A
- VFX strips from Package 06
- Environment layers from Package 07
- UI bars, medallions, slots, buttons from Package 01
- Loot item thumbnails and currencies from Package 05

## 03_gear_loadout.png
- Hunter portrait/full body assets from Package 02
- Item thumbnails and rarity frames from Package 05
- UI slots, panels, buttons from Package 01
- Symbol/glyph support from Package 08

## 04_forge.png
- Item thumbnails/materials/currencies from Package 05
- UI panels/buttons/bars from Package 01
- Forge environment background from Package 07
- VFX impact/glow accents from Package 06

## 05_warren.png
- Resource icons from Package 05
- UI panels/buttons/progress bars from Package 01
- Creature portraits / worker icons from Packages 03 and 08
- Background environment from Package 07

## 06_world_map.png
- Environment previews from Package 07
- Buttons/panels/bars from Package 01
- Loot and currency icons from Package 05
- Glyph/sigil assets from Package 08

## 07_memory_dust.png
- Memory Dust / currency icon from Package 05 or 08
- Panels/buttons from Package 01
- Meta symbols/glyphs from Package 08
- Environment backdrop from Package 07

## 08_build_mastery.png
- Glyphs, source symbols, mastery sigils from Package 08
- Panels/buttons/bars from Package 01
- Background environment from Package 07

## 09_settings.png
- Buttons/tabs/panels from Package 01
- Navigation glyphs and state symbols from Package 08