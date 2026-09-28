# PRESS, the field-vs-projectile polish (ADR-011, 2026-09-28)

Review page: https://claude.ai/artifact/2zZPnjMVQqSTtunKw8RxQw

**The brief** (the owner): the pixel-hard PRESS is approved; do not redesign it. One question remained: does the
travelling pressure front read too much like a PROJECTILE? PRESS should read as a FIELD DISTURBANCE PROPAGATING THROUGH
SPACE, not a magic projectile fired at a target. Keep the material, the palette, the persistent field, the ~30 ms crush,
the ~60 ms hold, the squash, no push, the brightness hierarchy and the JAWS / SPRAY / HARD HANDS rules. No added weight. No
sound.

**Core, unchanged.** A = the approved pixel-hard PRESS (films tag p13); B = this polish (tag p22).

## What changed

| | A (approved) | B (the polish) |
|---|---|---|
| the path | from the Seeker's chest down to the target's centre, ~12-16 degrees: it homed on the target | LEVEL along the combat axis, halfway between the field's height and the target's (clamped so the wall always holds its target); it never climbs or dips |
| the front | a crescent with a pale accent at the middle of its edge and a darker copy one frame behind it | only the wall: no accent (a glowing centre is a head), no echo (a trailing copy is a tail) |
| at the contact | the front faded at the target while two arcs appeared and slid on from the arrival point | the front STOPS; the next frame it is the FOLD (its wall collapsing at the creature's middle, its ends bent over and under it), then the arcs; one transformation in place |
| the crush | the arcs slid horizontally and drifted with the creature through the hold | where the front stopped, latched on the first frame after the contact: the arcs only close VERTICALLY (the upper presses down, the lower up) |
| the launch | the field's contraction and snap | the same: launch contours were tried and removed (below) |

The fold is a new part (`fxp_seeker_press_fold`): the front's own violet and weight, its inner edge lit at 0.72 of the
arrival rim's light (at the rim's full light its longer edge made it the phrase's brightest frame; at 0.55 its darkest),
over the arcs' own span. It shows exactly once, on the first frame drawn after the contact, whatever the frame phase.

## Three iterations

1. **p16**: the level path at the target's own height, no accent, the launch contours, a fold. Four judges (two accepted
   with notes, two rejected): the fold reached ~135 px past where the front stopped and the arcs snapped back; it was
   the dimmest frame of the contact; the arcs drifted forward through the hold; the front was born below the field
   (under the Seeker's belt); the contours were invisible at play speed and, on a still, speed dashes behind a moving
   body (three of four: remove). The echo was the one literal tail left.
2. **p21**: the fold over the arcs' span, lit like the front, anchored upright; the arcs latched; the axis halfway; the
   contours and the echo removed. Four re-checks confirmed those and all rejected one defect: when the first frame after
   the contact lands early (0 < u < 15 ms, most phases at 60 fps), the fold was placed from the launch pose (a lurch)
   and the next frame snapped back; an early phase also showed the fold twice.
3. **p22, the pass**: the crush forms WHERE THE FRONT STOPPED, latched on the first frame after the contact; the fold
   shows once; the axis is clamped to its target.

## Measured

Added brightness over a baseline (the judges' method), peak frame:

| | > luma 170 | > luma 210 |
|---|---|---|
| PRESS A (approved) | 749 | (the pack's art) |
| **PRESS B** | **867** (the arrival; the higher level wall clears the HUD strip that clipped A) | the pack's art |
| the fold frame | 726 (lit like the front, under the arrival) | |
| SPRAY | 2761 | 805 |
| HARD HANDS | 1382 | 612 |
| JAWS | 6957 | 5327 |

Quiet ticks ~180-230; the tick that gives way to JAWS 1151 / 134. The effect's horizontal extent through the contact
(violet mask), early phase (fast take, tick 2000) and on-tick phase (normal take, tick 6000) alike: front ~981-1083 ->
fold 1031-1200 -> arcs 1022-1194 held to +67 ms (centroid 1021 -> 1100 -> 1108: no lurch, no snap back).

## Checked

- **Iteration 1** (p16), four lenses (field vs projectile, the force transition, readability and hierarchy, an adversarial
  skeptic): two ACCEPT WITH NOTES, two REJECT; must-fixes: the fold's forward reach and the arcs' snap back, the fold as the
  contact's dimmest frame, the arcs drifting forward through the hold, the front born below the field; the contours: three
  of four REMOVE.
- **Iteration 2** (p21), four re-checks: every must-fix above RESOLVED but one, PARTLY by all four -- the phase-dependent
  lurch and snap back (and a double fold) when the first frame after the contact lands early.
- **The pass** (p22), two final checks (the force transition across frame phases; a skeptic): the phase fix RESOLVED at
  every contact (one fold frame, the fold at 1043-1200 and the arcs at 1022-1194 in both phases, the centre within 6 px
  from the fold through the hold); no must-fix; both ACCEPT WITH NOTES. Their answers: 1 still a projectile NO / PARTLY
  (what remains -- one crescent leaving the field and speeding into the hit -- is the approved travel); 2 propagation from
  the field PARTLY / PARTLY (released by the field more than rippling through it); 3 the horizontal -> vertical transition
  YES / YES; 4 as readable as A YES / PARTLY (the lunging snout 60-80 px ahead of the arcs at +17/+33). The skeptic found a
  frozen-playhead fault (at the end of a wave the fold's lit edge was drawn on every frozen frame), fixed with a frame
  count; a re-film of the normal take is pixel-identical.
- **Open** (their notes): the lunging creature's snout ahead of the arcs (the trade for a crush that forms where the front
  stopped; `FoldBackShare` is the lever); the lower arc's press weaker than the upper's (feet planted); the approved
  speed-up into the hit is the last projectile cue (a constant speed would read more like a wave but soften the approved
  hard arrival); the fold's one-frame curvature flip can look like a bracket on a still; a hitch over ~54 ms skips the
  fold; the creature sliding back under the still arcs; the capture tests two frame phases, not all.

## Files

| file | what |
|---|---|
| `01_A_current_pixel_hard_true_speed_sound.mp4` | A, the approved pixel-hard PRESS, true speed |
| `02_B_field_propagation_true_speed_sound.mp4` | B, true speed |
| `03_C_current_MUTED.mp4`, `04_C_field_propagation_MUTED.mp4` | A and B, muted |
| `05_C_side_by_side_close_MUTED.mp4` | A beside B, close |
| `06_D_sequence_RELEASE_TRAVEL_ARRIVAL_VERTICAL_CRUSH_HOLD.png` | the five frames, A over B |
| `07_D_contact_frame_by_frame_2x.png` | -17 / 0 / +17 (the fold) / +33 / +50 ms, A over B |
| `08_launch_contours_tried_and_removed.png`, `09_..._MUTED.mp4` | the tried launch contours against none (iteration 1's takes) |
| `10_E_repeated_ticks_sound.mp4` | B: three ticks (normal, quiet, giving way to JAWS) |
| `11_F_spray_vs_press_same_fight.png` | SPRAY's volley against PRESS's front, the same fight |
| `12_F_with_spray_sound.mp4`, `13_with_hard_hands_sound.mp4`, `14_with_jaws_true_speed_sound.mp4` | B beside the others |
| `15_trace.md` | B on the playhead |

Films: `bash tools/asset-pipeline/films_press.sh p22 normal repeated fast yield`; this folder:
`PYTHONUTF8=1 python tools/asset-pipeline/press_propagation_evidence.py p13 p22 production/qa/evidence/press-propagation p16`.
