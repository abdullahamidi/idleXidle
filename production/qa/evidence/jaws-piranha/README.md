# JAWS as SHADOW PIRANHA: the review package (ADR-011 production direction, 2026-09-27)

**The owner's direction.** JAWS is thorns-family reactive damage, presented intentionally simply: ENEMY HITS SEEKER →
SMALL SHADOW JAWS APPEAR ON THE ATTACKER → CHOMP → "−X JAWS" → GONE. A 100–160 ms VFX phrase. No trap body, chain,
housing, tether, bite glyphs, mirrored snaps or Concept D. The whelp attack work is closed (the redrawn 640-canvas
lunge kept, ~20 % travel; the row gap recorded as a layout limitation) and the bite contact motif is removed from
normal hits.

**Acceptance (the one question).** At true speed: when an enemy bites me, does it look like Shadow piranhas
immediately bite it back? Films 02 and 04 are the test.

| # | File | What |
|---|---|---|
| 1 | `01_*` | the old mechanical JAWS (the readable-clamp bear trap, rejected), true speed, sound |
| 2 | `02_*` | Shadow piranha JAWS, true speed, sound |
| 3 | `03_*` | the same, muted |
| 4 | `04_*`, `04b`, `04c` | close crop, true speed; 4× slow; every frame 1:1 |
| 5 | `05_*` | repeated triggers at fast tempo |
| 6 | `06_*` | during SPRAY (the answer at 1000 in its wind-up) |
| 7 | `07_*` | during HARD HANDS (the answer at 13000 as he leaps) |
| 8 | `08_*` | the answer kills the biter |
| 9 | `09_*` | the bite fells the Seeker as JAWS answers it |
| — | `10_trace.md` | the playhead trace of the normal take |

**What is in the build.**
- `ReactionRecipe` / `ReactionPerformance` (rewritten): three tiny jaw-heads from ONE source sprite in two states
  (`fxp_seeker_jaws_open` / `_shut`, a 64 × 40 canvas), each ~28 % of the bitten creature's visible height (22–48 px),
  art-directed: upper/front at 0 ms, lower/front at +15, behind-and-above at +25 (drawn behind the body). Each spawns
  ~10–25 px off its bite point, darts in open (16 ms), CHOMPS shut with a one-frame squash (20), recoils 4 px, dissolves
  55→100. The answer (number, F2-scale flash 0.45 / 80 ms, a kill's fall) lands on the main chomp at 25. A dark Shadow
  smear at each bite fades by 120; the phrase ends at 140. No yank, no chain, no line from the Seeker; the Seeker does
  nothing; the actual reflected target from Core is bitten, each on its own body.
- Art from PixelLab (`create_image_pro_flash` 96 × 64, the best of three concepts; the SHUT state an
  `edit_image_pro_flash` of it), cut to the runtime parts by `tools/asset-pipeline/v2/seeker_piranha.py`.
- Sound: `sfx_seeker_jaws_chomp` (`make_action_sfx.py jaws_chomp`): one small supernatural chomp with two very quiet
  ticks baked in; 140 ms, −10.5 dB peak. The dry-steel snap is no longer played. Brief: `design/audio/seeker-jaws-audio-brief.md`.
- Removed from playback: the mechanical clamp, chain, trap art and the N2 snap (their files stay as history).
- Untouched: the Core-driven `ReactionArmed` dock sweep; REPAY's legacy presentation; SPRAY and HARD HANDS.
- Tests: `jaws_reaction_test.cs` rewritten for the phrase; 802 Game tests green.

**Trace (`10_trace.md`).** The bite at 7000 shows on 7017; the reaction spawns on that frame with the chomp cue; the
main chomp (the number, the flash) at +33 on the playhead (the first frame at or past +25); the layer allocates 0 bytes
per frame; 1–9 sprites a frame.
