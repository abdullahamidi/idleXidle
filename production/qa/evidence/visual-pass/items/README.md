# ITEM CELLS — slot well, rarity frame, glyph: three layers with three jobs (2026-09-16)

Captured from the running game on 2026-09-16 with the OLD rarity frames still on disk (the five
`ui_frame_rarity_*.png` have an opaque near-black centre, so `ItemArtMetrics.FrameInterior` reports
the 15 % fallback and the renderer keeps tinting them). **These captures judge the LAYOUT.** The
verdicts marked *waits on the new family* cannot be given until the regenerated transparent-centred
frames land; nothing in the code changes for them — the renderer measures the hole from the texture.

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

| check | 100 | 125 | 150 | note |
|---|---|---|---|---|
| frame fills the slot | pass | pass | pass | 94 % of the cell, a 3 px rim of the plate shows on every side |
| glyph fills the frame | pass | pass | pass | content-aware: the ring, the sword and the chest reach the hole's edges; before, each sat in a 15 % inset of its own padding |
| slot boundaries obvious | pass | pass | pass | the plate's hairline and the rim read as the cell; the frame reads as the item |
| rarity obvious from the frame | *waits on the new family* | — | — | the legacy set is one gold ring under five tints; Legendary (white) and Uncommon (green) can be told apart, Rare and Epic only side by side |
| no white squares | pass | pass | pass | every glyph draws from its content bounds; no canvas edge shows |
| no double rarity encoding in square cells | pass | pass | pass | the strips are gone from the grid and the doll; the Forge ROW keeps its strip on purpose |
| selected ≠ Legendary | pass | pass | pass | bone ring + corner tick on the Legendary bow; the hover is a grey Slate ring on the Common ring cell; gold appears only in the frame art |
| dark items visible | **fail on the legacy frame** | fail | fail | `item_weapon_bow_heavy` is a thin near-black bow on the legacy frame's near-black interior — visible only by its highlight. The new family's transparent hole sits on `WellInk` (Black × 0.35 over the plate), a step lighter; re-judge then. If it still fails, the fix is in the art (the ~10 glyphs under 30/255 luminance) or a lighter well, not the layout |
| no clipping at 150 % | — | — | pass | the 3-column grid, the doll's 48 px wells, the 96 px inspector icon, the 144 px trader offers and the 120 px reveal all draw whole |

Row context (the Forge bag): the 30 px row icon draws the frame and the glyph, and at that size a
dark glyph is a smudge — the row's name and strip carry the identity there, as they always have.
