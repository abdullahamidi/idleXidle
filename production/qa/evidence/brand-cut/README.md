# BRAND second pass: the Etched Shadow Cut (ADR-011, the MARK reference)

The owner's brief (2026-09-30): keep the first slice's architecture; fix the ART DIRECTION. The first slice read as "a
glowing lavender spiral / eye / rune"; the mark must read as "a dark Shadow brand CUT INTO the target". Three visible
depths; a broken asymmetric scar-spiral; dark cut first, a one-sided violet edge second; a quiet idle; a deepen that
changes geometry; a transfer that does not fight the death smoke; a spread that propagates; working on dark and light
bodies without raising brightness. No sound in this pass.

Review page: https://claude.ai/artifact/A4G3gEs7CpXXoGahBEjtZm

## What changed (the architecture is the first slice's: MarkRecipe / MarkPerformance, the authored body points)

| Part | What |
|---|---|
| The art | `tools/asset-pipeline/v2/seeker_brand_cut.py` (the first slice's PixelLab coil path, `seeker_brand.py`, redrawn): a BROKEN ASYMMETRIC SCAR-SPIRAL, design B of three candidates chosen by a blind read (three readers, unanimous: "a sunken shadow wound"), then revised over two review rounds. Three wide breaks in the outer contour at every depth (it wraps at most 290 degrees round the centre), the weight on one side, scar branches added with depth, the spiral open, no dark pocket ringed by the visible bands (tested). The first slice's atlas is kept in `keypose_sources/seeker_brand_history/`. |
| The material | the incision near-black; a dark-violet WALL on its OUTER side only, two texels deep (three at DEPTH 3, which also keeps an inner wall wherever that closes no pocket; DEPTH 1's wall a step lighter); ONE lit RIM on the light-facing (upper-left) outer arc, 9-14 % of the mark; a dark ink stain round it, the lit smoke kept faint (0.18). On a light body the incision carries the mark, on a dark body the wall and rim do. |
| Three visible depths | `MarkRecipe.Stages` 4 (the spread cut, DEPTH 1-3), `StageFloors` {50, 150, 200}: base BRAND and ETCH's 120 % are DEPTH 1, 170 % DEPTH 2, 220 % and above DEPTH 3 (240 % gets only the faint retrace a rise inside one depth gets; its picture never changes). |
| Apply | ink motes gather, the stain condenses into the cut, its rim and the light-facing half of its outer contour catch ON the tick, and it settles dark within ~200 ms (`EtchMs` 200). |
| Idle | the same cut and stain in every step; one step in six lengthens a rim stretch by a texel. No travelling light, no pulse. |
| Deepen | ink gathers where the new cut will be (dark), the new cut appears with its rim, the rim answers, settled by ~170 ms (`CarveStepMs` 40, `DeepenEdgeMs` 50), after the host's own bite; the new depth's growth is geometry in the visible wall band, never light. |
| Transfer | no bridge (`TransferThread` false): the old mark collapses into its own dark centre (kept open) on the falling body while the death presents; the new front, which Core marks at once, carries a dark ink stain; the same mark seeps in on it once its own bite AND the reaction it draws have played out (`TransferSettleMs` 520: JAWS snaps ~333 ms after the contact). |
| Spread | SPRAWL: short dark ink threads leave the front's mark for every other creature, `SpreadStaggerMs` 40 apart; the row etches near-simultaneously. |
| Tests | `brand_mark_test.cs` 26 (new or rewritten: three depths carved deeper, dark first, never brighter; never a pupil (no dark texel ringed by the visible bands); never a ring (outer contour under 300 degrees); the deepen phrase; the halo extent; no bridge on a transfer and the seep waits for the bite and its reaction; the spread from its source). Game 845 / Core 1901 green. |

## The review list (the files here)

| # | Item | File |
|---|---|---|
| 1 | the FIRST SLICE at true speed | `01_first_slice_true_speed_sound.mp4` |
| 2 | the new cut at true speed | `02_new_true_speed_sound.mp4`, `02b_new_true_speed_muted.mp4` |
| 3 | apply, close | `03_apply_close.mp4`, `03b_apply_close_slow_x4.mp4` |
| 4 | idle, close | `04_idle_close.mp4` |
| 5 | three depths side by side | `05_three_depths.png` |
| 6 | deepen at true speed | `06_deepen_true_speed_sound.mp4`, `06b_deepen_close_slow_x3.mp4`, `06c_deepen_sheet.png` |
| 7 | spread at true speed | `07_spread_true_speed_sound.mp4`, `07b_spread_deepen_true_speed_sound.mp4`, `07c_spread_slow_x4.mp4` |
| 8 | transfer after a death | `08_transfer_true_speed_sound.mp4`; framed on the new front: `08b_transfer_6700_slow_x4.mp4`, `08c_transfer_sheet.png` (the 6.7 s fall: a bite and JAWS on the new front, then the seep), `08d_transfer_8200_slow_x4.mp4`, `08e_transfer_8200_sheet.png`; on the Pale Choir (the collapse is seen): `08f_transfer_light_sheet.png` |
| 9 | dark body (first slice vs new) | `09_dark_body.png` |
| 10 | light body (first slice vs new) | `10_light_body_sound.mp4`, `10b_light_body_etch_sound.mp4`, `10c_light_body.png` |
| 11 | with PRESS | `11_with_press_sound.mp4` |
| 12 | with SPRAY / HARD HANDS | `12_with_spray_hard_hands_sound.mp4` |
| 13 | with JAWS | `13_with_jaws_sound.mp4` |
| 14 | material sheet (glowing line vs dark cut) | `14_material_sheet.png` |
| 15 | measures | `15_measures.md` |
| 16-17 | other families in game; the six bosses (a still: the capture rig cannot pose a boss wave) | `16_other_families.png`, `17_bosses.png` |

Takes: `build/shots/brand/c01` (the new cut) and `build/shots/brand/b08` (the first slice, the same seeded fights);
`tools/asset-pipeline/brand_cut_evidence.py c01 b08 <out>` builds this folder.

## Review

A blind read of three candidate shapes (three readers, unanimous for B), then four rounds of independent read-only
reviewers (the brief and all findings in the session's scratch: brand_cut_brief.md, brand_cut_review1-4.md). Round 1:
DEPTH 1 hard to see on black, DEPTH 2 / 3 alike, DEPTH 3 nearly a ring, the transfer / spread rules untested. Round 2:
DEPTH 3's inner wall ringed black (an eye), the seep under JAWS. Round 3: the eye only notched open by a texel, the
transfer sheet framed off the new front. Round 4 (ACCEPT WITH NOTES): no eye at any depth on any host; its two last
notes (DEPTH 3 had lost its incision; the collapse did not show on a black host) are fixed and verified by the tests
and the measures, not by another round.

**Verdict: the gold-standard candidate for the owner's review.** Notes: on a black host the dark incision cannot show
(the wall and rim carry the mark); DEPTH 3's middle curl is mildly letter-like ('∂' / '2', open, not a ring); the coil's
size on large hosts is the first slice's; the bosses are a still; no sound yet.
