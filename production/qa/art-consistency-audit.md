# Art Consistency Audit — RESOLVED BY THE 2026-08-22 ARENA PASS

**Raised:** playtest 2026-08-19, item 8. **Resolved:** 2026-08-22 (PixelLab plan upgraded; one batched
generation pass). Contract: `design/art/arena-art-contract.md`. Pipeline: `tools/asset-pipeline/v2/`.

> "Karakter ve düşmanların genel olarak perspektif, animasyon ve boyut tutarsızlıkları var."
> "Düşmanlar sola bakacak, ana karakter sağa bakarak dövüşecek … idle ve dövüş animasyonları ikisinde de
> bakış yönleri ve perspektifler düzgün olmalı. Aynı şekilde boyutlar da."

## What was found, and what the pass did about it

| Finding (2026-08-19) | Resolution (2026-08-22) |
|---|---|
| **Perspective**: champion front-on painterly, enemies 3/4 pixel, bosses front-on symmetrical | One camera: PixelLab `view="side"`, three-quarter toward the viewer — champion on the **south-east** rotation (faces RIGHT natively), enemies + bosses on the **south-west** rotation (face LEFT natively). `ArtFacesLeft = false`; nothing is mirrored at draw time. |
| **Scale**: sprite heights did not track the role; bosses needed a hand-measured body table | One density (~3.4 screen px per source px): champions 128, enemies 128 (image-route creatures 192–256), bosses 160; every strip is 8 × 512 frames and the renderer's boxes (430 / 440×archetype / 540) are the size chart. The BossMeta table is gone. |
| **Animation**: roster idle+attack only, some enemies two-frame, no death/cast | Champions: idle, attack, **cast**, **death** (the fall plays the drawn clip; the collapse treatment is the fallback). Enemies and bosses: idle + attack, all eight frames, swing driven by the windup. |
| **Effects**: generic strips shared across semantics, per-Source slashes never played | Thirteen `fx_*` strips: one per FORM (tinted by the casting skill's SOURCE — the Skill event now carries it) plus hit / weakhit / crit / death / heal / shield / levelup, all authored white and drawn additively. |
| **Weapon family art** (BOW / SPEAR / SCYTHE icons are swords) | **Still open** — the item-icon layer was out of this pass's scope (see below). |

## Still owed

- **Weapon family icons** (`item_weapon_<trait>` are all swords; the name system has BLADE / BOW / SPEAR /
  SCYTHE). One icon set per family; `ForgeScreen.ItemArt` is ready to key on family the moment the files
  exist. Batch it with any other icon-layer work rather than one at a time.
- `hunter_portrait` (HUD, four screens) is still the generic Hunter face; a per-character portrait from the
  new south rotations would close the last seam between the roster and the HUD.

## Constraints that still hold

- Do not regenerate piecemeal: a one-off replacement matches nothing. Re-run through `v2/spec.json` +
  the contract, and review the sheet before filing.
- The art bible (design/art/) is the authority for style; the contract is the authority for geometry.
