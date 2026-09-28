# JAWS, a Shadow maw made of mist: the review package (ADR-011, 2026-09-28)

Review page: https://claude.ai/artifact/5cemkjDpzbxWn28P4YmS5Z

**The owner's review** of the misty teeth, in Turkish: "Kesinlikle daha iyi, ama hala biraz daha ısırılmanın
gösterimini netleştirmek lazım. Belki genel süre biraz uzayabilir veya ilk ısırılma anı daha belirginleştirilebilir.
Ayrıca dişler 4 tane üçgen ortaya geliyor şeklinde de olmasın. Gerçekten bir açılıp kapanan çene (pirana çenesi olabilir)
ısırma efekti olsun." In English: definitely better, but the bite still needs to read more clearly. Maybe the overall
duration can be a little longer, or the first bite moment more pronounced. And the teeth should not look like four
triangles coming to the middle: a real jaw that opens and closes (it can be a piranha jaw), a bite effect.

**What is in the build.**
- **A side-view jaw, made of mist.** One procedural strip, `fxp_seeker_maw` (`tools/asset-pipeline/v2/seeker_maw.py`,
  one fixed noise field per piece, no PixelLab), holds an upper and a lower jaw in 16 condensation states each, from a
  loose drift of violet smoke to the hardened jaw. Each jaw has a rounded cheek at the hinge, a blunt snout and five
  graded teeth, the front fang about 1.7 times the back tooth. The lower jaw is 13 % longer, a piranha's underbite, and
  its teeth sit half a tooth along, so the rows interlock. The body is a dark violet smoke mass. Its outline is broken by
  the smoke and fades out toward the hinge, so the back of the jaw is a ragged plume. The teeth harden and brighten only
  in the last states, which play only on the bite. The design was chosen from three (piranha, crocodile, beast) by a
  three-judge panel; the vote for piranha was unanimous (`10_three_jaw_designs_judged.png`). The jaw's outer side stays
  smoke, softer than its crisp biting edge. A dark lip runs along each gum, and the lower teeth are a shade darker than
  the upper ones, so the shut mouth shows two jaws meeting rather than one zigzag strip.
- **Where it bites.** The maw is anchored to the creature's own body, not to its lunge drawing. The hinge sits in front
  of the creature on the Seeker's side and about 70 % of the jaw lies over the creature. Open, the upper jaw rises clear
  of the creature's eyes, so its face sits inside the mouth. Shut, the seam crosses the creature at 0.60 of its drawn
  height, the throat under its head; just under the eyes, the band of teeth read as the creature's own grin. When one answer bites two creatures, the front one gets the full maw and the other
  its own maw, 0.85 the size, formed in place at its own front edge. The maw is drawn over the creatures and under the
  Seeker, so in HARD HANDS her fists land in front of it.
- **The phrase**, in ms after the first frame that shows the bite. It is about 590 ms long, against 300 ms for the misty
  teeth, as the owner allowed:

| ms | what happens |
|---|---|
| 0–83 | loose smoke grows out of nothing and gathers into a nearly shut jaw, coming in from the Seeker's side |
| 83–133 | the nearly shut jaw waits while the biting creature's own lunge drawing is still up; opened over it, the jaw was never seen to open |
| 133–200 | the jaw OPENS WIDE around the creature's head, its two halves turning apart about the hinge; its teeth become clearly visible |
| 200–233 | the mouth is held wide open, the wind-up before the bite |
| 233–300 | the jaws close, accelerating; the largest step, 58 % of the travel, is the snap frame itself |
| 300 (317 on screen) | SNAP: the jaws bite past their rest until the tooth points reach the other jaw's gums; the teeth harden and brighten; the bite sound; a kill's fall |
| 300–400 | the CLENCH: one 16 px push into the creature that peaks on the frame after the snap and eases back to nothing, and a 16 % swell for two frames, then easing evenly |
| 350 (367 on screen) | the number appears above the creature |
| 383–433 | the bite is held with the mouth slightly open, tightening over three frames |
| 433–593 | the jaw lets go and opens, comes apart into smoke, rises 28 px and fades evenly |

- **Unchanged:** the skill-id recipe, the real reflected target, `ReactionArmed`, no champion-body ownership, REPAY
  isolation, death ordering, the SPRAY and HARD HANDS overlaps, the one bite cue, no JAWS flash, and the number after
  the snap. A critical answer also plays the game's general critical cue with its number, as every skill's critical
  does. Each target draws 2 sprites (4 with two targets), and the trace records 0 allocations. 811 game tests and 1888
  Core tests pass. The asset-consumer gate passes; the misty-teeth strip moved to
  `tools/asset-pipeline/v2/keypose_sources/history/`.

**Measured on the filmed bite** (the bite at 7000, the whelp about 187 px tall):

| | misty teeth | the maw |
|---|---|---|
| the whole phrase | 300 ms | about 590 ms |
| what it is | four teeth that meet in the middle | an upper and a lower jaw turning about one hinge |
| frames the mouth is seen opening and wide open before the bite | 0 | about 6, over the creature's standing art |
| frames showing the jaws part-way closed | 3 | 2, the largest step on the snap frame |
| the first bite | the teeth stop | the snap past rest, the teeth brightening, one push, a swell, then the hold |
| a tooth passing through the other jaw's gum | n/a | never (a test turns every tooth tip by every frame's gape) |
| the largest condensation jump in one frame | not measured | 2 states (a test holds it) |

**Verified** by four judge rounds, each with vision judges on true-scale and gameplay-scale frames and an adversarial
code review. The second round found the tooth rows turning inside-out at the snap, a jolt that buzzed, an uneven
swell, a jaw mostly shut before the snap frame, the jaw over the eyes, a glassy fill, a six-state jump in the
condensation, and the rear maw reaching across the front creature. The third round found the open jaw reading as the
creature's own grin, the opening hidden behind the lunge drawing, a bite frame that was not the peak, a clench that
backed off at once, a zipper-like shut band, a glassy capsule, the maw drawn over the Seeker in HARD HANDS, the rear
maw arriving over the front creature, a one-frame tighten, and an untested front-target choice. The fourth round
found the shut band sitting just under the eyes, where it read as the creature's own grin, the opening still under the
lunge drawing, and the shut band still a zipper. Each was fixed, and each one a test can hold is pinned by a test.

**Known on this recorded bite:** the pack's own lunge art ends between +116 and +150, so the whole pack switches to
its standing art while the jaw forms. Near +500 the pack switches to its next pose. The previous build's take does the
same at those moments; JAWS does not cause it.

| # | File | What |
|---|---|---|
| 1 | `01_*` | PREVIOUS: four misty teeth that close, true speed, sound |
| 2 | `02_*` | NEW: the Shadow maw made of mist, true speed, sound |
| 3 | `03_*` | new, muted |
| 4 | `04_*` | close crop, true speed |
| 5 | `05_*` | frame sheet: MIST APPEARS, GATHERS, JAW FORMS, OPENING, OPEN WIDE, HELD WIDE, CLOSING, SNAP, CLENCH, BITE HELD, LETTING GO, DISSOLVING, SMOKE RISES, SMOKE FADES (new rows over previous rows) |
| 6 | `06_*` | repeated JAWS at fast tempo; some answers bite two creatures, one maw each |
| 7 | `07_*` | during SPRAY |
| 8 | `08_*` | during HARD HANDS |
| — | `09_trace.md` | the playhead trace: the snap and cue at +317, the number at +367, 2 sprites a target, alloc 0 |
| — | `10_*` | the three jaw designs the judge panel chose from (drawn in an earlier, lighter material) |

**Acceptance.** The first true-speed viewing of film 02 reads as a Shadow jaw made of mist that forms, opens wide,
bites that creature and dissolves back into smoke.
