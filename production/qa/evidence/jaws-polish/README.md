# JAWS polish — a rigid tethered mechanism (ADR-011, 2026-09-25) — AWAITING THE OWNER'S REVIEW

The owner approved JAWS' architecture and asked for one pass on ART DIRECTION and PHYSICAL MOTION. The first build was
mechanically correct but read as composed effect pieces: a trap that rose from the floor, a ring that pumped in scale,
a procedural chain, and an object that dropped and faded. This pass keeps every approved system and rebuilds the look.
Approved and unchanged:

- the skill-only recipe;
- the reaction layer;
- `ReactionArmed` and the Core-driven dock;
- the real reflected targets;
- the SPRAY and HARD HANDS isolation;
- the death ordering;
- the removal of the row ring and the generic cues.

NET, IRON and REPAY are untouched.

Every film is the real game, SEEDED (`RH_SHOT_SEED=7`) so CURRENT and POLISHED show the same fight. CURRENT is
`build/shots/jaws/base`, the committed base slice; POLISHED is `build/shots/jaws/polish`. The recipe is
`build/shots/jaws/films_jaws.sh`.

## The new physical sentence, measured (normal TEMPO, the bite at 7000)

The reaction's clock starts on the first frame that shows the bite, so the open jaws are always seen once.

| On screen (ms after the bite) | What |
|---|---|
| +17 (first frame) | THE TETHER FIRES. The open head is at the creature's front-lower silhouette, along the chain's line. The chain whips out of his belt in a curve, with a Source streak brightest at the head. The sound starts with a quiet spring tick. |
| +33 (next frame) | THE JAWS CLAMP. Both jaws rotate about the hub's bolt, accelerating, to a hard stop 3 degrees apart (shut on the limb). The teeth glint, three small sparks come out of the mouth, and the CLACK peaks here (18.8 ms into the cue). |
| +33 → +80 | a 5 degree jaw recoil and lock; the chain goes taut with a small shiver |
| +33 → +180 | THE YANK: the chain jerks the creature TOWARD the Seeker, peaking at +65 and settled by +180 |
| +183 | THE RETRACT: the jaws unlock and the head is reeled back along its chain, accelerating, into his belt |
| +283 | gone: the last 35 % of the travel fades into the belt, and the dock reads REARMING |

## What changed, point by point

| The owner's point | Before (base) | Now |
|---|---|---|
| world art | a PixelLab U that shut into a toothed ring (a portal or a collar) | a side-view steel TRAP HEAD with a chain eye, a spring, a hub and pivot bolt, and TWO serrated jaws that interlock on a toothed seam. Drawn on the Seeker's grid as rigid parts on one canvas (`14a`, `14c`) |
| rigid motion | the whole sprite pumped 0.82 → 1.12 and rose from under the target | ONE scale; the jaws ROTATE about the bolt: open 30°, then an accelerating slam to a 3° stop in 16 ms, a 5° recoil, locked |
| origin | appeared from below the creature | fired from his belt: the head is at the creature on the first frame, the chain whips out of the belt, and a streak runs along it |
| contact timing | shut drawn from 38 ms (the third frame) | open on the first frame, shut on the next |
| sound | the clack at 12 ms, jaws looking shut at ~50 ms | a 0 ms lead-in at 8 % of the peak, then the CLACK at 18.8 ms (the shut frame), then three chain-strain ticks |
| chain | up to 64 equal link stamps (sprites up to 135) | a dark metal body along the curve and 11 link accents, dense at both ends (sprites 28–47) |
| force | the target pushed AWAY while the chain was taut to him | the target JERKED TOWARD him (a few px), with the chain taut: snap, tension, yank |
| ending | slacken, drop and fade in place | the jaws unlock and the head is REELED BACK along the chain into his belt |
| deaths | released after the snap | a creature that falls: the snap reads, a 50 ms hold, then let go and reeled home, never dragging the corpse. A champion felled by the bite: JAWS answers first, then the chain slackens and the head fades in place |
| size | a 0.48-body-height ring | a head 0.53 of the creature's height long, chosen at play size from 100 % / 85 % / 75 % (`13`) |
| particles | flash + 5 sparks + glint + chain accent | the tether streak (60 ms), the teeth glint (70 ms), 3 sparks from the mouth; no generic flash |
| dock ready | a wide gold ring opening at 1.6× the medallion | a thin gold pulse leaving the rim (to 1.14×) and a 6 % swell (`08a` at true speed, `08b`) |

## Answers to the owner's questions (true speed)

- **Does the trap appear to originate from the Seeker?** Yes. On the bite frame the chain runs from his belt and the
  streak brightens toward the head; at the end it is reeled back there (`01a`, `02a`).
- **Do the jaws physically CLOSE rather than sprite-swap?** Yes. Two rigid jaws rotate about one bolt from 30° to 3°,
  and the hub never changes (`02b` 4× slower, `02d`).
- **Does the sound happen at the close?** Its strongest transient sits 18.8 ms into the cue, on the frame the jaws
  shut (the cue starts on the frame before).
- **Does the chain tension match the target motion?** The chain is taut while the creature is jerked toward the
  Seeker (`02a`, `12a`).
- **Does the mechanism RETRACT rather than vanish?** It is reeled into his belt; only the last 35 % of the travel fades.
- **Readable without being oversized?** At 85 % (`13`) it clamps the front paw and does not surround the creature.
- **Secondary during SPRAY and HARD HANDS?** Yes (`04a`, `05a`). Its light plays at 0.55 under an action, its sound is
  ducked, and its silhouette is a small dark head and a thin chain. In the HARD HANDS film the chain shortens as he
  leaps and the head is still on the creature at his blow.
- **Does it resemble the icon's fantasy as world art?** It shows the icon's parts (serrated jaws, a central bolt and
  spring, a chain) from the arena's side, never the face-on ring.

## Evidence

Stacked pairs are CURRENT above, POLISHED below, and muted.

| File | What |
|---|---|
| `01a` | CURRENT vs POLISHED at true speed |
| `01b` / `01c` / `01d` / `01e` | polished with sound effects, polished with music, polished muted, current with sound effects |
| `02a` / `02b` | the CLOSE CROP (belt, chain, jaws, target), CURRENT vs POLISHED, at true speed and 4× slower |
| `02c` / `02d` / `02e` | polished 4× slower with sound; polished and current, every frame |
| `03a` / `03b` | effects only (champion hidden) during HARD HANDS, the pair and every frame |
| `03c` | champion only (effects hidden): HARD HANDS untouched |
| `04a` / `04b` | during SPRAY: the pair, and the polished film with sound |
| `05a` / `05b` / `05c` | during HARD HANDS: the pair, polished with sound, every frame |
| `06a` | on frame 5 of a basic swing |
| `07a` / `07b` / `07c` | repeated triggers: the 16 s pair; polished 16 s and 5 s at fast TEMPO with music |
| `08a` / `08b` | the dock's ready at true speed (the wide ring above, the chosen pulse below) and frame by frame |
| `09a` / `09b` | the killing answer |
| `10a` / `10b` | the champion falls on the triggering bite |
| `11a` | REPAY beside JAWS, 16 s: REPAY keeps its own |
| `12a` / `12b` | the anchor overlay: the belt (cyan), the clamp (magenta), the caught body, on the front creature and during HARD HANDS |
| `13` | the size at play size: the current ring, 100 %, 85 % (chosen), 75 % |
| `14a` / `14b` / `14c` | the head's poses from open to the stop; the two rejected PixelLab candidates beside the icon; the parts on their canvas with the pivot |
| `15_timelines.md` | every trigger's life (`reaction_timeline.py --report`) |
| `16_non_regression.md` | SPRAY and HARD HANDS identical with the reaction layer on and off, re-proved after the polish |

## Performance, tests and cost

- **Per frame (polished films, from `reaction-draw`):** 28–47 sprites, 11 link accents, and 0 bytes allocated. The
  base build drew up to 135 sprites and 64 links.
- **PixelLab:** 2 generations this pass (`3c45cc40`, `ec3db96a`), both rejected on sight. The trap head is drawn by
  `tools/asset-pipeline/v2/seeker_jaws.py`.
- **Sound:** `sfx_seeker_jaws_snap` is regenerated. The approved SPRAY and HARD HANDS cues are byte-identical.

## Still open

- **The snap is unheard.** See `design/audio/seeker-jaws-audio-brief.md`.
- **The closed silhouette** is a lens with a toothed seam: two jaws, but a simple shape. A hand-drawn refinement of the
  three parts would slot into the same canvas and pivots with no code change.
- **`tools/check_boot.sh`** still expects `save.pre-v8` (pre-existing since `8314b03`, not touched).
