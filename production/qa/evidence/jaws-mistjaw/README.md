# JAWS, Shadow fangs made of mist, that close: the review package (ADR-011, 2026-09-28)

Review page: https://claude.ai/artifact/8trdfMEnhTAoZTtUgg1dFD

**The owner's review** of the Shadow Fangs + floor mist pass, in Turkish: "Dişler kapanmıyor, ayrıca söylemek istediğim
dişlerin sis gibi olmasıydı, anlık görünmek yerine sis gibi gelip ısırıp yok olacak bir efekt." In English: the teeth
don't close; and what I meant was that the teeth themselves are like mist: instead of appearing at once, an effect
that comes like mist, bites, and vanishes.

**What was wrong.** At the snap the upper points stopped 16 % inside the creature's top and the lower points 16 %
inside its bottom, a gap of 68 % of its height, so the teeth never met. They were hard sprites that faded in, with a
separate fog under the creature.

**What is in the build.**
- **The teeth are the mist.** One procedural strip, `fxp_seeker_mistfang` (`tools/asset-pipeline/v2/seeker_mist_fangs.py`,
  from the approved fang's outline, one fixed noise field, no PixelLab), holds a fang in 16 condensation states. State 0
  is a loose drift of violet smoke with wisps. State 15 is a condensed dark-violet tooth with a violet rim and a
  pale-violet point, still smoky at its root. One state is drawn per frame. The separate floor mist is removed.
- **A jaw that closes.** Two upper teeth and two lower teeth, the lower 0.8 of the upper's size. The rows interlock like
  a shut mouth: the upper pair is a little wider apart than the lower, and each point leans 8° inward. The open jaw forms
  on the creature's body, below its health bar and above the skill dock. At the snap the upper points pass below the
  lower points across the creature's lower middle.
- **The phrase**, in ms after the first frame that shows the bite:

| ms | what happens |
|---|---|
| 0–65 | the mist grows out of nothing (opacity 0.10 → ~0.3 → ~0.6 → ~0.9) as four violet plumes that come in from the sides (2.4× wider apart, 1.25× larger) and condense slowly |
| 65–130 | the jaws close, accelerating; three frames show them part-way, each step larger than the last |
| 130 (133 on screen) | SNAP: shut, interlocked; the bite cue; a kill's fall |
| 130–190 | the bite holds |
| 170 (183) | the number, above the creature |
| 190–300 | the jaws let go; over four frames the teeth come apart back into smoke, which rises off the creature, spreads and fades evenly; nothing at 300 |

- **Unchanged:** the skill-id recipe, the real reflected target, `ReactionArmed`, no champion-body ownership, REPAY
  isolation, death ordering, the SPRAY and HARD HANDS overlaps, the one fang cue, no JAWS flash, and the number after
  the snap. Each target draws 4 sprites (8 with two targets), and the trace records 0 allocations. 803 game tests pass.
  The unused JAWS parts moved to `tools/asset-pipeline/v2/keypose_sources/history/`, so they are not loaded at boot and
  the asset-consumer gate passes.

**Measured on the filmed bite** (the bite at 7000, the whelp about 187 px tall):

| | before | now |
|---|---|---|
| gap between upper and lower points at the snap | 131 px apart | upper points about 26 px past the lower ones |
| frames showing the jaws part-way closed | 1 | 3 |
| mist visible before the teeth form | none (hard sprites faded in) | about 4 frames of violet smoke gathering |
| frames of teeth coming apart into smoke | 0 (a fade) | 3–4 |

**Known on this recorded bite:** the pack's own attack animation ends on the same frame as the snap (+133), so the
whole pack switches to its idle art as the jaws shut. The fangs-only take does the same at that moment, and it is not
caused by JAWS. In the dissolve the rising smoke passes over the bitten whelp's head.

| # | File | What |
|---|---|---|
| 1 | `01_*` | PREVIOUS: hard fangs that never met, over a floor mist, true speed, sound |
| 2 | `02_*` | NEW: Shadow teeth made of mist, true speed, sound |
| 3 | `03_*` | new, muted |
| 4 | `04_*` | close crop, true speed |
| 5 | `05_*` | frame sheet: MIST APPEARS, GATHERS, CLOSING, SNAP, BITE, LETTING GO, DISSOLVING, SMOKE FADES (new row over previous row) |
| 6 | `06_*` | repeated JAWS at fast tempo |
| 7 | `07_*` | during SPRAY |
| 8 | `08_*` | during HARD HANDS |
| — | `09_trace.md` | the playhead trace: the snap and cue at +133, the number at +183, 4 sprites a target, alloc 0 |

**Acceptance.** The first true-speed viewing of film 02 reads as Shadow mist that comes in, becomes teeth, bites that
creature with the teeth closing on it, and turns back into smoke.
