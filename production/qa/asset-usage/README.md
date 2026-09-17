# Which textures does the game actually use? — 2026-09-17

Every PNG under `assets/art` ships in the build, and everything outside the deferred families
(`/Animations/`, `/Enemies/enemies/`, `/VFX/<champion>_*`) is decoded into GPU memory at boot. So an
unused texture costs download size, and usually memory in every session as well.

## The problem with asking the code

`tools/check_asset_consumers.py` asks "does something in `src/` name this file?" — and counts a whole
interpolated family as reached. `LoadoutScreen` builds `"icon_" + v.Id` for vow seals, so the gate
counted **every** `icon_*` file as consumed; `fx_` heads a key family, so `fx_levelup` — aliased to
nothing, played by nobody — passed the gate for weeks. The baseline's note that seventeen icons "gained
a consumer" on 2026-09-16 was that false positive, not a fix.

## What was done instead

1. **The game was asked.** `AssetLibrary` writes every key it is ASKED for to `RH_ASSET_TRACE`
   (Get / GetFirst / WhiteMask / an alias target, `?` for a `Has` existence check, `~` for a key a
   `Warm` prefix loaded). `tools/asset-pipeline/asset_usage.sh` runs the battery: 179 rig captures
   (every screen and tour, all ten champions, 150 %), then the enemy, effect, prologue, item, letter,
   Warren and geometry fixtures, the boot check and the opening played from a fresh save — about 500
   game launches. Result: **488 keys requested, 273 existence checks**, and 276 files never asked for
   (`never-requested.txt`).
2. **A trace is not a verdict.** The fixtures cannot pose every boss, death, item roll or trait, so each
   of the 276 was traced in the code by family — five analysts, then a skeptic per family who tried to
   find a consumer for every proposed delete (`verdicts.json`).

## The result

- **117 files deleted** (`deleted.txt`), plus **5 moved** to `docs/store/source-art` (read only by the
  itch page kit, so they no longer ship or load):
  - 66 item glyphs for trait words no slot pool can draw. `ForgeScreen.ItemArt` picks from
    `GearTraits.PoolFor`: weapons keen/heavy/swift/savage, charms warding/vital/greedy/wild, every other
    slot attuned/focused/swift/greedy. Exactly 44 of the 110 glyphs are drawable.
  - 27 UI icons: the nine `icon_equipment_*`, four `icon_role_*` (only `icon_role_attacker` is aliased),
    two blessings, nine old `nav_*`, `nav_forge_128`, `nav_warren_128`, `icon_peek`.
  - 23 old UI pieces: buttons, corners, dividers, scrollbar parts, keycaps, `ui_panel_large`,
    `ui_bar_fill/frame`, `ui_node_mastery`, `affix_healing`, `core_hatch`, two retired currencies.
  - `fx_levelup_strip8_512` — 8.4 MB of texture memory at every boot, played by nothing.
- **Kept, with the evidence in `verdicts.json`:** all enemy and boss art (every region × archetype ×
  clip, including deaths, is reachable), champion clips and base stills (fallbacks and shared skills),
  `fx_bind_chain` (swearing a vow), all 13 vow icons, all 14 trait icons, the set icons, `affix_defense`
  and `affix_timer` (gem stats), `hunter_portrait` (fallback). 155 files remain unrequested by the
  battery and reachable in code.

Measured on one fixture (`fight`, one armoured creature, dump at t=1.2): **457 textures / 679 MB
resident → 336 / 645 MB**. About 2 MB off the download.

## Code that went with the art

`AssetLibrary` aliases `item_glyph_core` and `ui_keycap`; `UiKit.KeyCap` (no callers).
`tools/asset-pipeline/retired_keys.txt` holds the 122 keys, and `build_spec.py` (manifest.json) and
`v2/file_assets.py` read it so a pipeline re-run cannot restore them. `scene_preview.py` now previews a
word the pools draw. The orphan baseline is empty.

## Still open

- The six prologue plates (49.8 MB) and the shared VFX strips load at boot though only one scene uses
  them; moving them into the deferred families is a memory change, not a deletion.
- `check_asset_consumers.py` still treats `icon_`/`fx_` as family heads. The trace is the honest check;
  re-run `asset_usage.sh` before trusting that gate about a family.
