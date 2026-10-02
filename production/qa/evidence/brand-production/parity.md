# BRAND curse: visual parity, production vs the approved prototype (39b59aaa)

Production = the working tree on `fix/vfx-fade` with stage 4's timeline fixes. Each pair is ONE frame of the seeded fight
taken by `proto/shoot.sh` (same env, same target playhead, start frame solved per build, within 60 ms of the target).
The frames are in `proto/` (prototype) and `prod/` (production). Each production row's mark-draw line is in
`prod/traces.txt`, and `prod/shots.log` holds the commands and landings. I compared each pair at play size (the full
1920x1080 frame) and in side-by-side crops of the creature, which were deleted after use.

Landing jitter is +-50 ms and the idle breath animates, so pixel-exact comparison is not meaningful. The comparison is by eye.

| Host / shot | Prototype -> production | Verdict |
|---|---|---|
| **Void Reaper** d1 / d2 / d3 (Ash-Burn) | Same cold-ash drained material with the thin violet fissures, and the same wisps. Depth 1 now sits on the chest/belly (the stage-1 seating fix) instead of the belt/apron. Depth 2 adds the lower robe, and depth 3 adds a third territory (chest, belly, lower robe hem). The ash in production reads very slightly softer and greyer at its edge (coverage ramp instead of the stencil's hard cut). | **PASS**. It still reads as an external affliction: grey ash patches on a blue-black robe, and Ash-Burn reads apart from the native dark. Coverage grows 1 -> 2 -> 3. |
| **Crystal Lich** d1 / d3 (Shadow violet) | Prototype d1 sat on the waist cloth / apron (the defect). In production d1 sits on the chest above the belt: a violet-grey drained patch with violet fissures. It does not run along the sash/stole and does not reach the skull or crown. At d3 both builds show chest, apron and side territories in the same violet-bruise material. | **PASS**. The defect is fixed with no change to the material. |
| **Spirit Matron** d1 / d3 | Same smoky drained ribcage region and lighter wisps. Production d1 is centred a little lower (ribs to belly, still off the face/hood). At d3 production covers ribs, apron and lower legs/robe, like the prototype (seats shifted slightly). | **PASS** |
| **Forge Colossus** d1 / d3 | The violet fissure strokes on the chest plate (d1) and the extra ones on the thigh/hip (d3) are present in both builds, in violet against the orange lava cracks. Production's d1 strokes are marginally fainter at this landing (the frame also differs by 100 ms of breath). | **PASS**. The corruption stays distinct from the lava (violet against orange). |
| **Black whelp row** d1 / d3 / d3_late | d1 is near-identical (an ash-grey territory on the front whelp's chest/flank). At d3 the frames caught different poses: the prototype frame is mid-lunge, the production frame idle, 53 ms apart. The host's ash territories read in both. d3_late is near-identical (the ash patch on the host's shoulder). | **PASS**. It reads on a near-black host. |
| **Pale Wisp row** d1 / d3 / d3_late | d1: the same dark bruise with a violet fissure on the front wisp's torso. d3 / d3_late: the same bruised patches on the host's robe and tail in both builds. | **PASS**. The wisp is clearly afflicted. |
| Depth 1 / 2 / 3 coverage | Void Reaper and Lich: one patch, then two, then three distinct territories. On the rows the depth-3 host shows its patches across body and tail. | **PASS** |

**Fixes made from this comparison: none.** Production keeps the readability of every prototype frame. The differences
I could see are the intended seating fix (the Lich and the Reaper moved up onto the chest), slightly softer
silhouette-edge coverage (the smooth coverage ramp that replaced the 90/255 stencil), and pose/timing jitter. None of
them needed a shader or parameter change.

Not re-checked here: the uncursed `_off` twins (unchanged by the curse; the prototype set keeps them), and Lumen Angel /
Thorn Regent (no prototype reference frame).
