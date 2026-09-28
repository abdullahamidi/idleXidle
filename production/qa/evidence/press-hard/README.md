# PRESS, the pixel-hard / impact polish pass (ADR-011, 2026-09-28)

Review page: https://claude.ai/artifact/YZNuW29wsUsxXkzJMpN88i

**The brief** (the owner): the Concept A direction is approved, do not redesign it. Keep the sentence: a quiet persistent
field, a contraction, one broad pressure wave, the front enemy crushed, a settle. Two goals: bring the effect into the
game's pixel language (it was "too smooth, too soft and too vector-like"), and make the tick substantially harder and
more decisive, from timing rather than more VFX. PRESS stays subordinate to SPRAY, HARD HANDS and JAWS. No sound yet.

**Core, unchanged.** The presentation reads the fight's own `Aura` tick and its `Break` target, as in the first slice.

## What changed

| | first slice (p5) | pixel-hard (p13) |
|---|---|---|
| material | smooth high-resolution shapes, Gaussian soft edges, a soft afterimage | built at 1/5 (the front) and 1/8 (the arcs) of their cells and upscaled NEAREST: one art pixel is about 3 screen px, the stage's own; four value bands (dark wake, violet mass, lavender edge, a tiny pale accent); five stepped segments with notches; one solid darker echo a frame behind |
| the front on screen | turned along its path, growing from half the Seeker to 1.7x the target | drawn axis-aligned on whole pixels (turned, its pixel grid tilted and the rim read as a saw-tooth); it leaves the field at 0.87 of its arrival size, so its pixels stay one size in flight |
| the release | the field springs back gently; the front eases out into the target | the field draws in to 0.82, SNAPS to 1.10 within ~27 ms and settles; the front ACCELERATES into the contact (the largest step is the last) |
| the crush | closes in ~70 ms; buckle x0.85 / x1.07 | closes in 30 ms (two frames), holds 60 ms, lets go in 120; buckle x0.81 height / x1.09 width, feet planted |
| the heat | the whole arcs pale-hot on the tick | the arcs stay violet; only their pressing edge lights: a dim line and a four-art-pixel accent at the contact |
| which enemy | arcs on the silhouette pinned at launch | pinned again one frame into the crush, so a creature caught in its own lunge is pressed where it is drawn; the arcs sit on its head and shoulders and are placed on the body as it BUCKLES, so they press it down and touch it; their size comes from its pose at launch (their art pixel is the world's) |
| the arcs' shape | shallow, thin, smooth | bowed toward the body, stepped, a violet body under a lavender pressing edge |
| the settle | the arcs fade | the arcs and the arrived front erode in pixel chunks (3x3 holes, fragments falling a pixel); no emitter |

## Four iterations (the path to this build)

1. **p7**, a "> TARGET <" pair of chunky chevrons at the crushed creature and a 6 px micro push. The judges dropped both:
   the chevrons read as arrowheads / a lock-on reticle (the brief bans arrows), the far one landed on the next creature
   in a lunge, and pinching from the sides contradicted the top-down crush; the push could not be seen at true speed (the
   creature's own lunge moves it more).
2. **p9**, the arcs lit whole and pale-hot on the tick. The judges measured the contact frame as bright as SPRAY's hit and
   about twice HARD HANDS'; the front was drawn turned along its path (a tilted pixel grid) and its pixels grew in
   flight; in the lunge the arcs sat behind the front creature's head and on the next one.
3. **p12**: the heat is a line, the front is drawn on the grid and keeps its pixel size, the crush re-pins the creature
   where it is drawn. The re-check found the arcs sized from the lunge's long silhouette (an art pixel ~3.9 px, coarser
   than the world's and changing with the pose) and hovering 35-40 px off the buckling body, reading as two bars.
4. **p13, the pass**: the arcs are sized from the pose at launch and placed on the buckling body, bowed toward it and
   thinner, drawn on whole pixels.

## Measured

Added brightness over a no-effect baseline in the fight area (the judges' method, `luma > 170` / `> 210` pixels on the
peak frame):

| | > 170 | > 210 |
|---|---|---|
| PRESS, first slice (p5) | 604 | (the pack's art) |
| PRESS, second iteration (p9) | 3647 | 1135 |
| **PRESS, the pass (p13)** | **749** (the front in flight) | **27** on the contact frame; ~200 in the crush, about 120-130 of it the whelps' own eyes and claws |
| SPRAY | 2761 | 805 |
| HARD HANDS | 1382 | 612 |
| JAWS | 6957 | 5327 |

The quiet ticks (near SPRAY / HARD HANDS) add ~180-200 px; the tick that gives way to JAWS ~1050 px while the front
travels, 133 > 210 as it flattens, gone before JAWS' teeth form. (The ~500 px > 210 at +150 ms in every take, p5 included,
are the pack's own white-eyed lunge frame and the damage number, not PRESS.)

## Checked

- **Iteration 2** (p9), a four-lens panel (pixel craft, impact / timing, hierarchy / context, an adversarial skeptic):
  three ACCEPT WITH NOTES, one REJECT. Must-fixes: the whole arcs lit pale on the tick (as bright as SPRAY's hit), the
  front drawn turned along its path (a tilted pixel grid, a saw-tooth rim), its pixel size sliding in flight, the arcs
  behind the lunging whelp's head and on the next one.
- **Iteration 3** (p12), three re-checks: all four RESOLVED (two of three for the target, one PARTLY); all ACCEPT WITH
  NOTES; a new must-fix from two of them: the arcs sized from the lunge's long silhouette (coarser than the world's
  pixel, pose-dependent), and they hovered 35-40 px off the buckling body.
- **The pass** (p13), two final checks (the arcs and the pixel grid; a skeptic): the size fix RESOLVED, no must-fix, both
  ACCEPT WITH NOTES. Their answers: 1 native pixel art YES (arcs 2.98 px, front 2.9-3.4 px, stage 3 px); 2 one front
  YES; 3 substantially harder YES (a 30 ms close, a ~60 ms hold at 0.81, an accelerating front; 27 px above luma 210 at
  contact); 4 the affected enemy clearer YES (the top arc 0-7 px off its head, both tips before the next head); 5 less
  smoothing helped YES; 6 quieter YES at every peak (the skeptic: PARTLY, because summed over the phrase the travelling
  front is lit longer than HARD HANDS' two-frame hit); 7 the push NO.
- **Open** (their notes): the arcs' size has no cap for a much larger creature; the bottom arc rests on the floor 10-29 px
  under the lunging claws; placed once, the arcs float over a whelp whose lunge ends mid-hold and drift right with it;
  the buckle on a standing creature is still unseen in this seed; the front leaves at nearly full size (the "grows out of
  the field" read is weaker); the field haze stays smooth (soft at rest, as asked); the yield flatten's pixels are
  slightly non-square for two frames.

## Proof

817 game tests (press_field_test: the new edge cell, the heat never tinting the arcs' body, the front's growth within
1.15x and not sized by the Seeker, the axis-aligned draw, the crush re-pin, the arcs sized at launch and placed on the
buckled body) and 1888 Core tests pass; the asset gate is
clean. The field's draw allocates 0 bytes on every traced frame but the silhouette pins: the first measurement of a
texture (the launch pin ~14 MB and, the first time a crush lands on a lunge frame, ~13 MB for the lunge strip; cached
after) and 48-136 bytes of bookkeeping per pin.

## Files

| file | what |
|---|---|
| `01_current_first_slice_true_speed_sound.mp4` | the first slice (p5), true speed |
| `02_pixel_hard_true_speed_sound.mp4` | the pass (p13), true speed |
| `03_pixel_hard_MUTED.mp4` | the pass, muted |
| `04_close_crop_true_speed_MUTED.mp4` | the pass, close |
| `05_asset_sheet_smooth_vs_pixel_hard.png` | the parts: smooth against pixel-hard, over the floor's grey and over black, their states, 3x NEAREST |
| `06_impact_frames_PRE_ARRIVAL_DEEPEST_HOLD_RELEASE.png` | -33 / 0 / +33 / +67 / +117 ms: the first slice, iterations 1 and 2, the pass |
| `07_repeated_ticks_sound.mp4` | three ticks (one normal, one quiet, one giving way to JAWS) |
| `08_with_spray_sound.mp4` | SPRAY at 1400, the tick at 2000 |
| `09_with_hard_hands_sound.mp4` | HARD HANDS at 3600, the quiet tick at 4000 |
| `10_with_jaws_true_speed_sound.mp4` | the tick at 10000 while JAWS bites the same creature |
| `11_push_vs_no_push_close_MUTED.mp4` | the micro push against none (iteration 1's record; dropped) |
| `12_trace.md` | the pass on the playhead: u, target, sprites, squash, shape, allocation |

Films: `bash tools/asset-pipeline/films_press.sh p13 normal repeated fast yield`; this folder:
`PYTHONUTF8=1 python tools/asset-pipeline/press_hard_evidence.py p5 p7 p9 p13 production/qa/evidence/press-hard`.
