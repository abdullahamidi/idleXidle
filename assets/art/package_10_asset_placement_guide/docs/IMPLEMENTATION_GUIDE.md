# IMPLEMENTATION_GUIDE

## Goal
Use the improved UX reference screens as the blueprint for implementing scene composition with the already generated asset library.

## Global layout rules
- Base reference canvas: **1672×941** in the supplied mock screens; scale proportionally to your actual 1920×1080 target.
- Keep a persistent top resource strip and a persistent bottom global navigation on most non-title screens.
- Use **Package 01** panel and button assets for all chrome.
- Use **Package 07** environments as background sources.
- Use **Package 02/03/04** (and their A expansions) for character, enemy, and boss presentation.
- Use **Package 05** for item thumbnails, loot, currencies, and materials.
- Use **Package 06** for VFX overlays and combat timing accents.
- Use **Package 08** for glyphs, symbols, source icons, and branding.

## Screen-by-screen guidance

### 01_main_menu.png — Main Menu

Annotated reference: `annotated/01_main_menu_annotated.png`

**Layout regions**
- **A — Player Summary**: `22,18,345,148` — Portrait, level, HP bar, combat power. Use UI panels + bars + portrait medallion.
- **B — Primary Navigation Rail**: `20,168,365,718` — Vertical main actions: Play, Hunter, Forge, Warren, Map, Settings.
- **C — Hero / Brand Focus**: `420,110,840,530` — Center logo / title focal zone over environment background.
- **D — Offline Rewards Panel**: `1230,95,400,245` — Idle rewards summary with currency icons and claim CTA.
- **E — Recent Progress Panel**: `1230,385,400,275` — Stage progress, activity summary, view details CTA.
- **F — Bottom Utility Bar**: `24,812,1600,110` — Mail, Relics, Shop, Community, Help shortcuts + flavor text.

**Primary asset families**
- UI/panels/ui_panel_large, ui_panel_medium, ui_panel_small
- UI/buttons/ui_button_primary, ui_button_secondary
- UI/bars/ui_bar_progress_frame + fill
- Branding/Symbols logo and emblem assets from Package 08
- Environments/shadow_void_wastes_* or other background variant from Package 07

**Implementation notes**
- Preserve a clean, high-contrast CTA stack on the left.
- Offline rewards and recent progress should be immediately visible without overwhelming the logo focal area.

### 02_hunt_auto_combat.png — Hunt Auto Combat

Annotated reference: `annotated/02_hunt_auto_combat_annotated.png`

**Layout regions**
- **A — Top-Left Hunter HUD**: `18,12,350,210` — Player portrait, HP, power, source icons.
- **B — Stage Header**: `560,20,555,125` — Region name, wave/stage, progress bar.
- **C — Top Resource Bar**: `1120,8,530,80` — Premium/resource currencies.
- **D — Combat Arena**: `180,120,980,540` — Hunter, enemies, damage numbers, VFX, parallax background.
- **E — Right Context Column**: `1235,90,390,620` — Idle rewards, objective, recent loot, expedition card.
- **F — Auto Skill Dock**: `280,646,920,130` — Auto-trigger skill icons with cooldown numerals, all set to AUTO.
- **G — Left Speed/Mode Controls**: `30,700,200,125` — Auto toggle and speed button.
- **H — Bottom Stage Footer**: `0,812,1672,129` — Chat/system line, auto-hunt state, stage progress, bottom nav.

**Primary asset families**
- Hunter character assets from Package 02 / 02A
- Enemy creature assets from Package 03 / 03A
- VFX strips from Package 06
- Environment layers from Package 07
- UI bars, medallions, slots, buttons from Package 01
- Loot item thumbnails and currencies from Package 05

**Implementation notes**
- The skill dock is intentionally presented as **auto-cast**: every skill tile shows cooldown timing and AUTO state rather than manual hotkeys.
- Build the arena with Package 07 parallax layers, then draw hunter/enemies, then VFX, then floating numbers, then HUD.
- The right column is a rotating context rail: objective, loot, expedition, idle rewards.

### 03_gear_loadout.png — Gear Loadout

Annotated reference: `annotated/03_gear_loadout_annotated.png`

**Layout regions**
- **A — Player Summary**: `16,12,340,190` — Top-left portrait, HP and currencies.
- **B — Character Stats Column**: `20,190,305,510` — Class identity and stat list.
- **C — Character Showcase**: `340,150,470,630` — Full body hunter preview with equipment slots around.
- **D — Loadout Pager**: `505,752,340,74` — Preset slots 1-5.
- **E — Inventory Grid & Tabs**: `845,90,450,675` — Arsenal, artifacts, materials and filters.
- **F — Item Detail Panel**: `1320,90,325,680` — Selected item stats, set bonuses, compare context.
- **G — Action CTA Row**: `1300,770,350,74` — Equip best / auto manage.
- **H — Bottom Nav**: `0,842,1672,99` — Hunter, Skills, Resonance, Talents, Inventory, Relics, Shop, Mail, Settings.

**Primary asset families**
- Hunter portrait/full body assets from Package 02
- Item thumbnails and rarity frames from Package 05
- UI slots, panels, buttons from Package 01
- Symbol/glyph support from Package 08

**Implementation notes**
- Center the hunter on a presentation pedestal area; place equipment slots symmetrically around the figure.
- The inventory grid should reuse the slot frames and rarity frames from Packages 01 and 05.

### 04_forge.png — Forge

Annotated reference: `annotated/04_forge_annotated.png`

**Layout regions**
- **A — Recipe List**: `20,192,375,572` — Left list of craftable/upgradable items with filters.
- **B — Forge Tabs**: `445,106,770,68` — Upgrade, Refine, Salvage, Craft.
- **C — Item Showcase**: `470,195,650,420` — Center pedestal with selected item.
- **D — Power Preview**: `570,616,390,150` — Current vs projected power, affix deltas.
- **E — Materials Panel**: `1236,150,395,265` — Required currencies and parts.
- **F — Chance / Helper Panel**: `1230,430,400,250` — Protection toggle, success rate.
- **G — Forge Actions**: `1200,700,430,110` — Upgrade and auto-upgrade CTAs.
- **H — Bottom Nav**: `0,840,1672,101` — Persistent global navigation.

**Primary asset families**
- Item thumbnails/materials/currencies from Package 05
- UI panels/buttons/bars from Package 01
- Forge environment background from Package 07
- VFX impact/glow accents from Package 06

**Implementation notes**
- The central showcase should feel ceremonial; animate VFX beneath the item with Package 06 aura/impact loops.
- Keep the success-rate and protection options visually subordinate to the primary Upgrade CTA.

### 05_warren.png — Warren

Annotated reference: `annotated/05_warren_annotated.png`

**Layout regions**
- **A — Warren Status**: `20,190,320,560` — Level, capacity, storage, upgrades, global boosts.
- **B — Tab Strip**: `485,102,640,60` — Overview / Producers / Automation / Upgrades / Stats.
- **C — Automation Chains**: `350,195,865,560` — Core producer rows and chain flow arrows.
- **D — Passive Income Panel**: `1250,72,380,110` — Hourly resource totals.
- **E — Storage Overview**: `1260,220,365,220` — Current stock and view storage CTA.
- **F — Next Unlocks**: `1260,485,365,220` — Upcoming Warren unlocks.
- **G — Claim / Bottom Nav**: `0,822,1672,119` — Bottom global nav plus claim all CTA.

**Primary asset families**
- Resource icons from Package 05
- UI panels/buttons/progress bars from Package 01
- Creature portraits / worker icons from Packages 03 and 08
- Background environment from Package 07

**Implementation notes**
- Warren is a management dashboard: emphasize rows and chain flow more than decorative splash art.
- Use progress bars and resource icons repeatedly with consistent spacing.

### 06_world_map.png — World Map

Annotated reference: `annotated/06_world_map_annotated.png`

**Layout regions**
- **A — Top HUD**: `12,10,360,180` — Portrait summary and source icons.
- **B — Map Field**: `120,100,960,700` — Region nodes / islands arranged around map plane.
- **C — Selected Region Detail**: `1118,92,515,700` — Preview, progress, recommended power, loot, enter CTA.
- **D — Bottom Nav**: `0,840,1672,101` — World, Hunter, Inventory, Relics, Codex, Shop, Mail, Settings.

**Primary asset families**
- Environment previews from Package 07
- Buttons/panels/bars from Package 01
- Loot and currency icons from Package 05
- Glyph/sigil assets from Package 08

**Implementation notes**
- Region cards should use cropped previews from Package 07 environments.
- The selected region detail panel acts as the conversion area with reward previews and the Enter Region CTA.

### 07_memory_dust.png — Memory Dust

Annotated reference: `annotated/07_memory_dust_annotated.png`

**Layout regions**
- **A — Summary Panel**: `20,220,472,260` — Level ring, next threshold, view rewards CTA.
- **B — Center Meta Focus**: `470,95,730,640` — Large Memory Dust emblem and total currency.
- **C — Income Panel**: `1240,76,392,150` — Passive generation and total/hour.
- **D — Permanent Upgrades**: `1130,240,500,540` — Upgrade list and spend costs.
- **E — Milestones**: `20,520,470,240` — Unlock track with progress.
- **F — Recent Acquisitions**: `590,600,420,160` — Recent sources/history CTA.
- **G — Bottom Nav**: `0,842,1672,99` — Hunter, Inventory, Relics, Memory Dust, Shop, Mail, Settings.

**Primary asset families**
- Memory Dust / currency icon from Package 05 or 08
- Panels/buttons from Package 01
- Meta symbols/glyphs from Package 08
- Environment backdrop from Package 07

**Implementation notes**
- Make the center emblem the focal point, with large numeric value directly below.
- Upgrade rows on the right should behave like a store/upgrade list with repeatable button slots.

### 08_build_mastery.png — Build Mastery

Annotated reference: `annotated/08_build_mastery_annotated.png`

**Layout regions**
- **A — Build Summary**: `20,180,390,610` — Power score, source alignment, active bonuses.
- **B — Top Subnav**: `495,78,670,74` — Hunt / Build / Stats / Mastery tabs.
- **C — Mastery Graph**: `430,150,820,610` — Central node graph and mastery bonuses.
- **D — Comparison Panel**: `1242,180,380,300` — Stat comparison vs previous.
- **E — Growth Overview**: `1242,505,380,240` — Progress stats and CTA.
- **F — Bottom Nav**: `0,842,1672,99` — Hunter, Inventory, Relics, Shop, Mail, Settings.

**Primary asset families**
- Glyphs, source symbols, mastery sigils from Package 08
- Panels/buttons/bars from Package 01
- Background environment from Package 07

**Implementation notes**
- The mastery graph needs the clearest focus on the screen; keep side summaries narrower.
- Source icons from Package 08 should repeat consistently across the left-side alignment strip and node states.

### 09_settings.png — Settings

Annotated reference: `annotated/09_settings_annotated.png`

**Layout regions**
- **A — Settings Category Rail**: `20,150,290,610` — System, Audio, Graphics, Accessibility, Notifications, Account, Language, Support.
- **B — Primary Settings Columns**: `345,148,870,620` — Audio, Graphics, Accessibility, Notification controls.
- **C — Account / Language / Help**: `1240,150,390,610` — Account binding, language, support tools.
- **D — Bottom Nav**: `0,840,1672,101` — Global nav with settings highlighted.

**Primary asset families**
- Buttons/tabs/panels from Package 01
- Navigation glyphs and state symbols from Package 08

**Implementation notes**
- Treat settings as a dense utility screen; panel clarity matters more than decorative illustration.
- The left rail is persistent category navigation; right column groups account/language/help settings.