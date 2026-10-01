# BRAND, Concept A LIVING SHADOW CORRUPTION: the polish pass (ADR-011; owner's brief 2026-10-01)

Three visual problems from d2885245, fixed without changing gameplay, timing, Core or the prototype pipeline: the
"string of beads" read, the "zipper / seam / sash" read, and depth 1 readable on every host. Not accepted; no sound.

Review page: https://claude.ai/artifact/AEfLGCYB5anLk7WujnvK5n

| Part | What |
|---|---|
| Light | short tapered irregular RUNS inside the veins (rise, hold, long taper; a little off each vein's centre; dim violet between brighter runs); no gated dots, no round junction discs; the steady light SCREENED into the host (`CursePrototype.Screen`), the growing front additive |
| Structure | `CorruptionAtlas.Grow_`: an off-centre entry pocket (smoky patch, violet leaking from inside, an elongated hot core); ONE crooked, twice-broken path to an upper territory; a side and a lower territory reached by smoke only; the three ~120 degrees apart round the pocket (region 1.3 : 1, was 3.7 : 1); veins stop BEFORE touching another (`WouldMeet`); hairlines die |
| Material | the stain MULTIPLIED into the host (`CursePrototype.Stain`: veins a bruised violet, a broad mottled infected-tissue layer); a violet-grey shadow over the WHOLE silhouette, deeper with depth |
| Host rule | the host's own mean luma (read once per strip): the violet up to 1.4x on a near-black body, the additive front trimmed a little on a pale one; never a name |
| Depth 1's region | suited to the host: on a dark body the infected tissue LIT from inside by a dim violet haze (screened); on a pale one a deep SATURATED violet bruise laid twice (the grey lavender read as "dirt", "a dye stain"); a wider, looser pocket |
| Seat in the mass | `CursePrototype.ThickNear`: the pocket moves off a thin limb, a wrist, a hand or a weapon into the nearest part of the silhouette at least 60 % as thick as its thickest (on the Matron's wrist, beside the Reaper's hand, on the Lich's sash it read as "a bracelet", "its own magic", "a belt ornament"); measured once per strip |
| Leaks | two slow shadow wisps per infected territory rising out of the body, drifting outward |
| Size | the field sized by the whole body, 240-660 px (a boss's curse is a region, not a buckle-sized patch) |
| Tests | `brand_curse_test`: reach; no loop; lit share; light in elongated runs; no vein spans the body; depth 1 holds a dark patch and a violet leak; a region, never a band; the pocket moves off a thin limb into the body's mass |

Takes: `build/shots/brand/kA5` (this pass) and `build/shots/brand/kA2` (d2885245);
`PYTHONUTF8=1 python tools/asset-pipeline/brand_polish_evidence.py kA5 kA2 build/shots/brand/scale <out>` builds this folder.
`17_depth1_fresh_read_sheet.png` is the sheet the depth-1 fresh readers saw (every host at depth 1, whole, at 100 %; the
readers opened one file per frame so no viewer shrank it).

OUTCOME (fresh reads on the final build, rounds V and Y): the bead read is gone (0 on the curse), the zipper / seam
read is gone (0 since round C), no symbol or rune read; NOT fixed: the Crystal Lich at depth 3 reads as "a sash"
(6 of 6: its depth-3 path lies along the robe's crossed collar). Depth 1 is visible on every host and reads as
something done to the creature on the Forge Colossus, Lumen Angel, Thorn Regent and Verdant bruiser; on the Void
Reaper, Crystal Lich and Spirit Matron most readers called it the creature's own dark aura; the black whelp blends at
actual size; the pale wisp at depth 1 reads as a stain. `18_final_fresh_read_sheet.png` is round Y's sheet.
