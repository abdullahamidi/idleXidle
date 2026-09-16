# ITEM CELLS — slot well, rarity frame, glyph: three layers with three jobs (2026-09-16)

Captured from the running game on 2026-09-16 with the REGENERATED rarity frames (five pixel-art
frames with see-through centres, one band, progressive ornament: plain iron · green inlay · blue steel
brackets · violet enamel with gold corners · gold filigree with a crest). `ItemArtMetrics.FrameInterior`
measures each hole (10-20 % per side) and the renderer draws every frame untinted. Re-run with
`bash tools/asset-pipeline/item_fixtures.sh [filter]`.

## What was built

- `ItemCellLayout` (pure): `FrameRect` = 94 % of the cell, square, centred; `GlyphRect` from a
  measured interior; `Fit` = uniform, centred, never past 1:1; gem / pip / corner-overlay rects;
  **the one rarity ramp** `RarityInk` (Common bone, Uncommon Good, Rare 4A90D9, Epic B06AC8,
  Legendary gold). The four private ramps in Forge, Vault, Gear and the tooltip are deleted.
- `ItemArtMetrics`: `ContentBounds` (alpha > 8 bbox) and `FrameInterior` (centre-out scan along the
  middle row and column; opaque centre = fallback), one `GetData` per texture, cached.
- `ForgeScreen.DrawItemIcon(b, item, CELL)`: well (only under a see-through frame) → frame (untinted
  when see-through, legacy tint when painted) → glyph drawn from its content bounds into the hole.
- Gear: the 5 px rarity strips on the inventory cell and the doll well are gone; every caller passes
  its cell; selected = 2 px bone ring + dark seam + bone corner tick (`SelectedMark`), never gold.
- `RH_SHOT_GEAR_POSE=showcase` and `tools/check_item_art.py` (in `check_all.sh`).

## The captures

| file | what it poses |
|---|---|
| `gear_selected_<100,125,150>.png` | `character` + showcase: the first Legendary SELECTED, no pointer |
| `gear_showcase_<100,125,150>.png` | the same with `RH_SHOT_PAGE_MOUSE` on the Common ring cell (893,458 / 858,567 / 918,662) |
| `forge_<s>.png` | `forge`: the bag ROW icons (30 px, the row keeps its strip and its gold SelectedEdge), the selected art |
| `reveal_<s>.png` | `lootforge 1.2`: the chest reveal's 120 px cells — a glove and a gem medallion |
| `trader_<s>.png` | `trader`: four Control(96) offers, the card's Plate accent in the one ramp |

The showcase bag (`Game1.GearShowcaseBag`): Legendary bow wearing `item_weapon_bow_heavy` (the darkest
glyph on disk, mean luminance 22/255), Epic WARDEN chest (locked), Rare ring wearing `item_ring_swift`
(bright), Uncommon blade wearing `item_weapon_keen` (the brightest weapon), Common ring, Common chest.

## Verdicts

Every picture was looked at whole and the Gear grid again at 2x (`gear_selected_100` 925-1335 x 255-465).

| check | 100 | 125 | 150 | what the pictures show |
|---|---|---|---|---|
| frame fills the slot | PASS | PASS | PASS | the frame is 94 % of the cell; a thin plate rim is the slot well |
| glyph fills the frame | PASS | PASS | PASS | content-aware: the ring, sword and chest reach the hole's edges; nothing floats as a thumbnail |
| slot boundaries obvious | PASS | PASS | PASS | plate hairline + rim read as the cell; empty cells stay half-strength plates |
| rarity obvious from the frame | PASS | PASS | PASS | iron / green inlay / blue brackets / violet enamel / gold filigree — shape and ornament differ, not only hue |
| no white or black squares | PASS | PASS | PASS | every hole is see-through onto the warm well |
| no double rarity encoding in square cells | PASS | PASS | PASS | no strip in the grid, the doll, the dragged piece or the flight (source-pinned) |
| selected is not Legendary | PASS | PASS | PASS | the selected Legendary bow wears a bone ring and a bone corner tick round its gold frame |
| hovered is its own look | PASS | PASS | PASS | `gear_showcase_*`: the Common ring's cell lifts and takes a slate ring; the hover card hangs off it |
| dark glyphs visible | PASS (noted) | PASS (noted) | PASS (noted) | the darkest file (`item_weapon_bow_heavy`, mean luminance 22) reads as a thin dark bow on the warm well; it is thin art, not a hidden one |
| no clipping at 150 % | PASS | PASS | PASS | three-column grid, 48 px doll wells, 96 px inspector icon, 120 px reveal cell all whole |
| same object everywhere | PASS | PASS | PASS | Forge bag rows, the reveal card, the trader's cards and the inspector draw the same frame grammar at their own sizes |

## Notes

- The well is the branch's old "shelf" ink (`UiInk.Rule * 0.9`), now painted under the see-through
  frame: that is what keeps near-black iron glyphs from vanishing on a dark cell.
- `tools/check_item_art.py` refuses a rarity frame whose centre is painted — the 2026-08 family would
  have failed it (centre alpha 255).
- The Forge bag ROW keeps its 5 px strip and its gold `SelectedEdge`: a row is not a square cell.
