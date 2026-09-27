# JAWS hero chomp: the readability pass (ADR-011, 2026-09-27)

Review page: https://claude.ai/artifact/BKJLHRTob95gkw5wnUr6Rj

**The problem.** The Shadow-piranha concept is approved. At true speed the first build read as "a purple effect
happened": three near-equal jaws whose state changes fell one display frame apart, with the flash, the number and the
sound landing at once. At 4× it read. A temporal readability / visual hierarchy problem, nothing else.

**What changed.**
- **ONE HERO, two small secondaries.** The hero is ~35 % of the creature's visible height (26–60 px), always
  foreground, on the front of the body above its middle. Secondary A is 0.70 of it (lower/front, start +30, chomp
  +70), secondary B 0.62 (behind-and-above, start +50, chomp +90, drawn behind the body). Before: 1.0 / 0.85 / 0.9 at
  0 / 15 / 25 ms.
- **A HALF-closed state** (`fxp_seeker_jaws_half`), made deterministically from the accepted OPEN and SHUT: the two
  silhouettes' signed distance fields averaged and re-thresholded, colours from the nearer source. Same creature, no
  pumping. The hero now closes over three display states: OPEN (0–20), HALF (~32), SHUT (48).
- **A closed HOLD.** The hero stays shut ~45 ms (48 → ~95) before recoiling and dissolving (gone by 150). The
  phrase is ~180 ms (before: 140). No global or actor pause.
- **The peak on the closed frame.** The reflected number, the F2 flash, the main glint AND the chomp cue's transient
  land on the hero's closed frame (+48, which is +50 on the 60 fps boundary). The cue used to fire at the spawn; it
  now fires when the mouth closes. Its two quiet ticks moved to +22 / +42 to follow the secondaries. Core untouched.
- **Contrast.** The violet rim, the eye and the pale teeth are lifted ~18 % in the source; the body stays dark. No
  bloom, glow or outline.
- **Residue** reduced to 0.35 (secondary to the mouth), gone by 170.

| # | File | What |
|---|---|---|
| 1 | `01_*` | CURRENT: the first Shadow piranha, true speed, sound |
| 2 | `02_*` | POLISHED: the hero chomp, true speed, sound |
| 3 | `03_*` | the same, muted |
| 4 | `04_*` | close crop, true speed |
| 5 | `05_*` | close crop, 4× slower |
| 6 | `06_*`, `06b` | OPEN → HALF → SHUT → HOLD frame sheet off the take at 3×, with the three source states; every frame current vs polished |
| 7 | `07_*` | repeated triggers at fast tempo |
| 8 | `08_*` | during SPRAY |
| 9 | `09_*` | during HARD HANDS |
| — | `10_trace.md` | the playhead trace: spawn on the bite's frame, the cue and the answer at +50 |

**Acceptance.** At first true-speed viewing (film 02, then 04): "something like a small Shadow piranha bit that enemy."
If it still reads as a purple flash, the report says which of the three dials is short: hero size, closed hold,
contrast. No new effects.

**Build.** `ReactionRecipe` (per-jaw `ChompAtMs` / `HoldMs` / `GoneMs`, `HalfKey`, `HalfAtShare`, `AnswerAtMs` = the
hero's closed frame), `ReactionPerformance` (`Mouth` 0/1/2, the hold in `Recoil` / `Opacity`), `HuntScreen` (the cue on
`Clamped`, trace `reaction-cue`), `seeker_piranha.py` (the half state, the lift), `make_action_sfx.py` (the ticks at
+22 / +42). Tests updated: 802 green.
