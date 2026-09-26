# The bite foundation: review battery (ADR-012, 2026-09-26)

**What this is.** The shared combat-language pass the JAWS studies asked for: the Gloom Whelp's attack re-authored as a
bite, the pack's spaced wind-up and front-led presentation lunge, the Seeker's layered hit recoil with a directional
contact accent, and the quiet default flash. Core outcomes and timing are unchanged; SPRAY and HARD HANDS keep their
clips and beats (`06_root_motion_trace.md`, and the regression note below). JAWS stays paused; Concept D is not built.

**The fight.** One seeded fight (`RH_SHOT_SEED=7`): the Seeker against four GLOOM WHELPS in Umbral Reach, the
reaction layer OFF (JAWS' answer is its reflected number and flash on the bite's own frame), the legacy row ring held
out of the build output for the takes (restored). OLD is the foundation study's own takes from the same seed (the
crouch strip, the contact-time shove, the full-white flash); NEW is `tools/asset-pipeline/foundation_films.sh`.

| # | File | What |
|---|---|---|
| 1 | `01_*`, `01a` | the enemy attack, OLD beside NEW at true speed; OLD alone with sound |
| 2 | `02_*` | NEW at true speed with the fight's own sound (the bite thud unchanged) |
| 3 | `03_*`, `03b` | NEW with tint and effects OFF (the acceptance view); the bare views side by side (OLD bare, NEW bare, NEW without the lunge, silhouette) |
| 4 | `04_*` | NEW as a silhouette |
| 5 | `05_*`, `05b` | the key poses (crouch strip above, bite strip below); every frame at play size |
| 6 | `06_root_motion_trace.md` | the bite's sentence on the playhead: anticipation, commit, the leader's root offset by frame, contact, the recoil by frame, the flash, follow-through end, home |
| 7 | `07_*` | the four-whelp pack attack, front-led lockstep |
| 8 | `08_*` | the receiver: a hit while idle (the boss fixture's bite at 3000, the Seeker between swings) |
| 9 | `09_*` | a hit 300 ms after the basic swing's beat |
| 10 | `10_*` | hits during SPRAY (in its wind-up and in its flight) |
| 11 | `11_*` | a hit as HARD HANDS leaps |
| 12 | `12_*`, `12b`, `12c` | recoil on beside off, 1:1; repeated bites at fast tempo; every frame of the recoil |
| 13 | `13_*` | the flash: OLD (1.0, 200 ms) beside NEW (0.45, 80 ms) on the same foundation, dark whelp |
| 14 | `14_*` | the same at 1:1, 4x slower |
| 15 | `15_*` | a light body (the Choir swarm's strips standing in) |
| 16 | `16_*` | the boss, pinned alive (the fixture fix), NEW flash |
| 17 | `17_*` | a critical (JAWS' answer, `-7 JAWS CRITICAL`, the crit cue and number, the quiet flash) |
| 18 | `18_*`, `18b` | the killing hit: contact, the quiet flash, the fall |
| 19 | `19_*` | the full sentence, enemy attack → Seeker reaction, true speed with sound |
| 20 | `20_*` | the same, muted |
| 21 | `21_*`, `21b` | repeated normal combat at fast tempo with the music; OLD beside NEW |
| 22–24 | `22_*`, `22A`, `23B`, `24C` | the JAWS concepts A, B, C (unchanged prototypes) over the new foundation |
| — | `25_blind_reads.md` | the blind reads on the new foundation and the concepts over it |

**Regression.** `tools/asset-pipeline/action_regression.py` on the study's takes against these: every release,
contact, clip start, hand-off and champion frame lands on the same playhead; the differences it lists are creature
x positions (the pack now moves, so a knife's contact tick and a melee launch's target x follow it) and the champion's
root x under the recoil. Tests: `bite_presentation_test` (9) with the Game suite (808) green.

**Review page:** https://claude.ai/artifact/2kS886KRf84XjHRw2EwmqJ

**Reproduce:** build once, then `bash tools/asset-pipeline/foundation_films.sh`, the concepts with
`python prototypes/jaws-concepts/animatic.py build/shots/foundation/new build/shots/foundation/concepts/<A|B|C> <A|B|C> 7000:1042,810`,
then `PYTHONUTF8=1 python tools/asset-pipeline/foundation_evidence.py production/qa/evidence/bite-foundation <blind dir>`.
