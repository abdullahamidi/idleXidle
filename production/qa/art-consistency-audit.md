# Art Consistency Audit — MARKED FOR A DEDICATED PASS

**Raised:** playtest 2026-08-19, item 8. **Status:** blocked on asset budget, NOT forgotten.

> "Karakter ve düşmanların genel olarak perspektif, animasyon ve boyut tutarsızlıkları var.
> PixelLab'da asset üretme sınırım doldu sanırım ama bunu da mutlaka incelemek üzere işaretleyelim."

## What to audit when asset generation is available again

- **Perspective**: the champion, the enemy bands, and the boss renders do not share one camera
  assumption (some read side-on, some 3/4). Pick one and re-render the outliers.
- **Scale**: enemy sprite heights vary in ways that do not track their band's role (a Swarm creature
  can render larger than a Bruiser). Establish a size chart (champion = reference height) before
  regenerating anything.
- **Animation**: the champion has idle/walk frames; most enemies are static or two-frame. Decide the
  floor (breathing idle for everything?) and generate to it.

- **Weapon family art (added 2026-08-19, item-system redesign)**: every weapon picture on disk is a
  SWORD variant (`item_weapon_<trait>.png` x10). The name/stat system now has four families — BLADE,
  BOW, SPEAR, SCYTHE — so a "SHADOW BOW" renders as a sword. When credits return: one icon set per
  family (the id-stable picker in ForgeScreen.ItemArt is ready to key on family instead of the trait
  pool the moment the files exist). Same for the other slots if variety is wanted; they at least match
  their slot today.

## Constraints

- PixelLab credits were exhausted as of 2026-08-19 (17 generations used earlier; balance $0).
- Do not regenerate piecemeal: a one-off replacement will match nothing. The size chart and camera
  call come FIRST, then a single batched generation pass.
- The art bible (design/art/) is the authority for style; this audit is about geometry, not style.
