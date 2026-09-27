# JAWS, one Shadow piranha: the review package (ADR-011, 2026-09-27)

**The decision.** The three-jaw hero chomp was not accepted: too many things (three jaws, the flash, a glint, a smear,
the number) occupied the same small area at once and read as "purple activity". JAWS now uses ONE SINGLE HERO
SHADOW PIRANHA and nothing else. No swarm, no residue, no glint, no particles, no behind-the-target layer. Nothing
may be added back before the one object is accepted.

**What changed.**
- **One object.** The recipe holds one head (~35 % of the creature's visible height, 26–60 px), always foreground.
  The secondaries, the smear and the glint are removed from the recipe and the layer (not hidden: gone).
- **Staged in negative space.** The OPEN head appears about three-quarters of its own width OUTSIDE the creature's
  front edge, on the Seeker's side, so its whole silhouette is against the arena for the first frames. It then
  moves in over the close and bites the creature's outer front edge with its rear half still outside the body.
- **Slower close, longer hold.** OPEN at 0 and ~25 (approaching), HALF at ~45 near contact, SHUT at 65 (the main
  chomp), HELD shut ~65 ms, then a 6 px recoil and a quick fade, gone by 200. Before: shut at 48, held 45, gone 150.
- **The peak on the closed frame.** The reflected number and the chomp cue land on the closed frame (+65, which is
  +67 on the 60 fps boundary). The cue is one bite; the two baked ticks are gone.
- **The flash never erases the head.** A JAWS-only override: 0.28 on the closed frame (option B, the build). Option A
  (the full F2 0.45 one rendered frame after the chomp) and OFF (the control) are filmed for the comparison. The
  generic F2 default is untouched.
- **Contrast** as in the previous pass (the rim, eye and teeth lifted 18 %); the body dark; no bloom.

| # | File | What |
|---|---|---|
| 1 | `01_*` | CURRENT: the three-jaw hero chomp (the previous pass), true speed, sound |
| 2 | `02_*` | NEW: one piranha, true speed, sound |
| 3 | `03_*` | the same, muted |
| 4 | `04_*` | close crop, true speed |
| 5 | `05_*`, `05b` | OPEN in negative space → approaching → HALF → SHUT → HOLD → exit, off the take at 2×, with the three source states; every frame |
| 6 | `06_*` | repeated triggers at fast tempo |
| 7 | `07_*` | during SPRAY |
| 8 | `08_*` | during HARD HANDS |
| 9 | `09_*`, `09b` | the flash comparison: B 0.28 on the chomp (the build) / A the full F2 a frame after / OFF |
| — | `10_trace.md` | the playhead trace: spawn on the bite's frame, the cue and the answer at +65 |

**Acceptance.** First normal-speed viewing of film 02: "a small Shadow piranha came in and bit that creature." If it
still reads as purple flash or noise, the report names exactly which is true: A the OPEN piranha is not recognizable in
negative space; B the motion is still too fast; C the piranha is too small; D the target flash still obscures it.
Nothing is added.

**Build.** `ReactionRecipe` (one head; `SpawnWidthShare`, `ChompAtMs` 65, `HoldMs` 65, `GoneMs` 200; `TargetFlash`
0.28 + `TargetFlashDelayMs`; `ReactionRecipes.FlashDial`), `ReactionPerformance` (one sprite, no behind or light
pass), `HuntScreen` (the flash echo on its own frame, `keepFlash`), `make_action_sfx.py` (no ticks). Tests
rewritten: 801 green.
