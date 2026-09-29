# BRAND, the Etched Shadow Brand: the MARK / PERSISTENT TARGET-ATTACHED STATE reference, first slice (ADR-011, 2026-09-29)

Review page: https://claude.ai/artifact/AddEshDzufkCDuBmyJitpL

**Status: a first slice for the owner's review, NOT accepted.**

**The brief** (the owner): the last gold-standard archetype, MARK, with BRAND as its reference. A hybrid: an etched /
branded sigil in shadow smoke / ink. "That enemy has been branded. The mark is living shadow, etched into its body. It
quietly persists there. When the mark deepens, it bites further inward. If it spreads or transfers, the same mark migrates
cleanly to the next target." Not a projectile, a field, a trap or a champion performance. Discovery first; one skill,
one direction, a first slice and a review page; Core untouched but for a proven information-only hook.

**Discovery:** `DISCOVERY.md` (what BRAND really does in Core, probe-verified; what was on screen; what was wrong; the
sentence; why BRAND; the presentation contract).

## What was built

| Part | What |
|---|---|
| Core (information only) | the field tick's `Marked` is emitted AFTER the depth update, so it reports the mark IN FORCE (it said 0 % on every wave's first tick and ETCH one step late). Proof: `brand_marked_report_test` pins a fingerprint of every other event, every wave outcome and the champion's end state, taken on the code BEFORE the move, for ten builds (base, ETCH, ETCH+SINK+GRAVEN, ETCH+PACE, SPRAWL+ANCHOR+WINNOW, SPRAWL+EVEN, CALL, SPEND, the Seeker with PRESS, BRAND in slot 0): byte-identical after it; and the reported depth is what the front enemy pays |
| The art | `tools/asset-pipeline/v2/seeker_brand.py`: the SHAPE from PixelLab (ten `create_image_pixen` silhouettes, one used: a lopsided hand-burned coil, 1bf52462, cached in `keypose_sources/seeker_brand_src/`), its path extracted and REDRAWN at the game's pixel material (3 px a logical pixel, value bands, nearest, no fragment under 6 texels); one atlas `assets/art/VFX/parts/fxp_seeker_brand.png` (6 depth stages x 18 cells: gather, idle x6, edge, carve x3, form x3, loosen x2; cut and halo layers; the thread puffs) |
| Where it sits | an AUTHORED body point per frame for all 60 creature idle / attack strips (`<strip>.mark.json` beside each strip, read by `MarkPoints`), proposed by `tools/asset-pipeline/v2/mark_points.py` (a reviewed seed per strip, tracked frame to frame, the coil's footprint kept on the body; the whelp's lunge, the Choir wisp, the Cinder armoured and the umbral caster authored by hand; the forge colossus and the verdant armoured hold ONE spot that follows its own patch of the body; a shield counts as the body, being what faces the hunter) and reviewed on its contact sheets (`22_body_points_*.png`). Checked by the test on every frame: 80 % of the coil on the body at full resolution (the tool keeps 84 %, moving a whole strip before any single frame), no jump over 0.16 of the frame between frames or on the loop's wrap, one point for two identical frames, and the mark's whole smoky halo at its largest size clear of a reviewed HEAD box per frame. The torso probe is only a fallback for a strip with no file |
| The layer | `MarkRecipe` / `MarkPerformance`: the brand drawn in the creature loop ON the host's body point (after the creature, before its hit flash), riding its breath, lunge and PRESS's buckle; the loose pass for the coil coming apart and the smoke in flight. One SHOWN stage per coil: it cuts deeper only under a chisel, so a deepen is never lost to a fall and never skipped past |
| The screen | `HuntScreen`: the fields chosen BY RECIPE (a BRAND woven before PRESS no longer takes PRESS's picture away), the mark built from BRAND's ticks and the falls the SCREEN shows (a JAWS kill falls on the snap), a chisel waits for a bite close to its tick to settle, the reticle behind the hunter retired for BRAND |
| Capture | `RH_SHOT_VARIATION` (capture only) poses a variation earned the game's way; `films_brand.sh`; `brand_evidence.py` |

## The moments

| Moment | What the player sees |
|---|---|
| APPLY (the wave's first tick, 2 s in) | smoke clumps gather on the body and condense into the coil; the whole cut lights once ON the tick (the moment Core amplifies), then settles |
| IDLE | a translucent lavender coil (a ring and a thin inner hook) in its smoky roots; on a light body the roots are a dark ink char round the cut; only a travelling third of the ring is lit, stepping every 800 ms, each creature at its own moment; the cut and the roots never change shape; no pulse, no ring, no cloud |
| REFRESH | a tick that changes nothing (base BRAND) shows nothing |
| DEEPEN (ETCH / WINNOW) | the same coil cut deeper: a wider groove (deep1), the ring's wall cut into the hollow (deep2), then the thin hook cut further ALONG THE SPIRAL, inward (deep3, deep4), so the cut literally goes deeper into the coil while its outline never grows; the spiral stays open and the hook stays a thin unlit line at every depth (design D, chosen unanimously by a blind read of four deep ladders). The old cut holds while a bite near the tick plays out (no chisel over its wind-up or its clip), then a CHISEL lights only the new cut in three stretches (round the ring; for the hook, setting in at the old cut's end and running on inward), and the new stage appears under it; never brighter, never a lit knot |
| MIGRATE (the host falls) | the coil comes apart into smoke on the falling body (220 ms), pinned from the death clip's own authored point on the falling torso; the next creature, which Core marks at once, carries a thickening smoke; a strand of overlapping smoke puffs (a mid body in a ragged ink rim, each puff its own density, the trailing half thinner; no lit centre) slides under the heads to its coil's tip and the coil is drawn in from there at the depth the old host SHOWED; once the fall's white death smoke has cleared and no bite is in the way, the coil is re-cut once faintly, or, if the mark deepened meanwhile, the chisel cuts the deeper stage there |
| SPREAD (SPRAWL) | every creature carries the mark from the first tick (a faint smoke until its hop lands); the coil hops down the row, each hop leaving as the one before lands (the row is formed within ~0.6 s), never from or to a creature that has fallen; a WINNOW deepen ripples down the row, one coil after another, and so does a hop's re-cut (never the whole row on one frame) |
| CONSUME | none: BRAND has none |

## The numbers (18_measures.md, from the b08 takes)

- Exact with / without pairs: the apply adds 765 px above luma 170 (0 above 210); at rest 0; a clean deepen 135 (4000)
  and 63 (6000); SPRAWL + WINNOW's full-strength ripple 347. The accepted references: PRESS 749-867, HARD HANDS 1382,
  SPRAY 2761, JAWS 6957. BRAND is the quietest thing on screen.
- Each ETCH step in game (violet lit groove, settled frames): deep1 1170, deep2 1395 (+19 %), deep3 1485 (+6 %), deep4
  1575 (+6 %): the last two steps are small (the owner's decision, below).
- 0 bytes allocated on every drawn frame of all 16 on-mark takes; 3 sprites a marked body at rest (up to 19 with
  SPRAWL's four coils).

## Review

Six rounds of independent read-only reviewers (placement, deepen, migration, the true-speed read, code, a critic):
rounds 1-5 REJECT, round 6 a verification of round 5's must-fixes. Resolved over the rounds: marks on eyes and on the
next creature's face; letter / eye reads of the deep stages; lost deepens; SPRAWL from the dead; a 22 MB read on PRESS's
crush; a circular head test; clipped smoke; a row-wide flash; a flat strand; the Choir mark on corpses. After round 6 the
whelp's and the Cinder armoured's death spots were moved (lower; off the furnace core) and `long` / `fast` re-filmed; the
other takes show the previous whelp death spot (0.026 of the frame higher).

**Verdict: a strong first slice, not yet the gold standard.** Open for the owner: (1) the deepest ETCH steps are small
(+6 % each; this coil has no inward room left without closing its hollow into an eye): accept, show three depths, or a
new shape pass; (2) the material reads as a lit lavender line more than dark incised shadow on the black whelps:
recommended, an incision (near-black cut, a lit edge on one side, darker smoke) behind its own blind read. Also open:
for ~100 ms at a fall the loosening smoke passes close to the next whelp's eye; a short hop's strand is a vertical wisp;
no BRAND sound yet; ETCH's card +170 vs Core +240; a JAWS-kill fall is tested, not filmed.

## Files

| file | what |
|---|---|
| `DISCOVERY.md` | the discovery report and the presentation contract |
| `01_true_speed_sound.mp4`, `02_true_speed_muted.mp4` | the seeded fight at true speed (the fight's own sound; BRAND has no cue yet) and muted: apply, idle, two falls |
| `03_apply_slow_x4.mp4`, `04_migrate_slow_x4.mp4` | the apply and the second migration (8.2 s), four times slower |
| `05_etch_deepen_sound.mp4`, `05b_etch_deepen_slow_x3.mp4`, `05c_etch_with_jaws_sound.mp4` | ETCH deepening on every tick (WILT in JAWS's slot, so no bite reaction covers the host), slowed, and beside JAWS |
| `06_sprawl_spread_sound.mp4`, `06b_sprawl_spread_slow_x3.mp4`, `06c_sprawl_winnow_sound.mp4`, `06d_sprawl_winnow_deepen_slow_x3.mp4` | SPRAWL spreading down the row; SPRAWL + WINNOW deepening every coil, one after another |
| `07_with_press_sound.mp4`, `08_with_spray_hard_hands_sound.mp4`, `09_with_jaws_sound.mp4` | beside the accepted references |
| `09b_light_body_sound.mp4`, `09c_light_body_etch_sound.mp4`, `15b_light_body_sheet.png` | on a light body (the Pale Choir): the ink half of the material |
| `10_before_after.mp4` | the reticle held behind the hunter, and the brand on the enemy, side by side |
| `11_apply_sheet.png` .. `15_idle_sheet.png`, `12b_stages_same_host.png` | frame sheets: apply, the two clean deepens (close crops on the host), the four depths at 1x / 2x / 4x, migrate, spread, idle |
| `16_parts_sheet.png`, `17_pixellab_sources.png` | the atlas over grey, black and a light body (a '-405' beside each row), and the PixelLab silhouettes |
| `18_measures.md` | the light the mark adds (exact with / without pairs), each ETCH step in game, by construction, the trace's sprites / bytes / body point |
| `19_trace.md`, `20_order_trace.md` | the mark's trace; BRAND before PRESS, both presented |
| `21_other_families.png`, `22_body_points_*.png` | BRAND on four other regions' creatures in game; the body point on every frame of all 60 creature strips |

Regenerate: `PYTHONUTF8=1 python tools/asset-pipeline/v2/seeker_brand.py` (the atlas), `PYTHONUTF8=1 python
tools/asset-pipeline/v2/mark_points.py` (the body points), `bash tools/asset-pipeline/films_brand.sh <tag>`,
`PYTHONUTF8=1 python tools/asset-pipeline/brand_evidence.py <tag> production/qa/evidence/brand-mark`.
