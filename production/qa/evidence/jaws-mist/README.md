# JAWS, Shadow Fangs + SHADOW MIST: the polish review package (ADR-011, 2026-09-27)

Review page: https://claude.ai/artifact/99CnwwXM34Uo4Txogdssgr

**The brief.** SHADOW FANGS is approved; do not redesign it. Artistic polish only: ONE restrained supportive layer,
SHADOW MIST, so the phrase reads "Shadow gathers around the creature, jaws snap onto it, then the darkness
evaporates" instead of "four geometric fang sprites move inward". The fangs stay the semantic impact. If readability
gets worse, reduce the mist; never compensate with more effects.

**What is in the build.**
- **Exactly two layers**, mist and fangs. No particles, glints, trails, flash, bloom or additive light.
- **The mist art** is procedural, with no PixelLab (`tools/asset-pipeline/v2/seeker_mist.py`). It is two wisps: near-black, darker than the
  creatures' own black, with a very faint violet fringe, a compact core and a long feathered fringe, and zero alpha on every
  edge. The script records what it measured (`keypose_sources/seeker_mist_spans.json`), and the tests read that.
- **The mist lies UNDER the creatures.** It is a Shadow pool the bitten whelp stands in, never a veil over it. This was decided by
  measurement and judge panels: drawn over the creatures, the same fog greyed the whelp's white eyes by about 40 %,
  and a thin veil over them by about 11 %. Drawn under, the eyes and the violet rim are exactly as they are without the mist.
  Three adversarial verification rounds then REDUCED it, because the darkened floor beside a near-black creature costs its
  silhouette a little separation. The stacked density is capped near 0.6, the pocket is raised off the feet and sized to
  one creature, and the release is a third lighter.
- **Size and place.** The pocket is centred a little above the bitten creature's drawn silhouette, on its torso and back and
  toward the upper fangs, off the floor between its feet. It is sized to one creature, never to the lunge's wider
  canvas; its height is the body's own, since the fangs sit on its edges.
- **The curves** below are the layer's opacity; the main pocket's densest point is 0.72 of it on screen:

| ms | fangs | mist | what happens |
|---|---|---|---|
| 0 | 0.30 | 0.06 | FADE-IN: a breath of Shadow; the fangs emerge, open, outside the silhouette |
| 17 | ~0.55 | ~0.30 | OPEN: the mist is ~16 % wider and looser |
| 33 | ~0.80 | ~0.51 | CLOSING: the mist contracts as the fangs close |
| 55 | 1.00 | 0.70 | SNAP: fangs whole and crisp; the mist at its densest (~0.50 on screen, ~0.59 where the wisps overlap); the cue |
| 55–90 | 1.00 | 0.70 | HOLD: pressure, the mist compressed |
| 90–140 | 1.00 | 0.70 → 0.42 | HOLD: the mist loosens outward and thins |
| 95 | | | the number, above the creature |
| 140–185 | 1 → 0 | 0.42 → ~0.12 | RELEASE: the fangs retract 6 px and fade over 45 ms; the mist keeps growing (about 11 % more from the release to its last visible frame) and evaporates evenly |
| 183 | 0 | ~0.12 | MIST-ONLY TAIL: faint Shadow alone (~0.08 on screen) |
| 200 | | 0 | gone (the phrase is not lengthened) |

- **Cost and tests.** Each target draws 4 fang sprites and 2 mist sprites, with no fang quads once they have faded. The trace records 0
  allocations over both passes. The tests were rewritten for the mist: 806 pass.

**Measured against the fangs-only take** (same seed, same bite):

| | fangs only | with mist |
|---|---|---|
| the bitten whelp's eye brightness (peak) | 219–230 | 219–230 (unchanged) |
| its silhouette separation from the floor at the snap | 1.00 | 0.89–0.90 |
| the same through the release | 1.00 | 0.95–0.98 |
| the pocket's width on screen | — | ~200 px (one creature) |

**Known, and left alone because placement is the approved foundation:** on this layout the lower fang pair's OPEN pose
sits under the skill-dock band at the bottom of the arena, so it rises into view only at about 50 ms. The fangs-only take
does the same.

| # | File | What |
|---|---|---|
| 1 | `01_*` | CURRENT: Shadow Fangs, no mist, true speed, sound |
| 2 | `02_*` | POLISHED: Shadow mist + Shadow fangs, true speed, sound |
| 3 | `03_*` | polished, muted |
| 4 | `04_*` | close crop, true speed |
| 5 | `05_*` | frame sheet: FADE-IN, OPEN, CLOSING, SNAP, HOLD, RELEASE, MIST-ONLY TAIL (polished row over current row) |
| 6 | `06_*` | repeated JAWS at fast tempo |
| 7 | `07_*` | during SPRAY |
| 8 | `08_*` | during HARD HANDS |
| — | `09_trace.md` | the playhead trace: the snap and cue at +66, the number at +100, 6 → 2 sprites in the tail, alloc 0 |

Items 1–5 show the bite at 7000 in the seeded normal-tempo fight, the JAWS trigger a film can hold. The answer kills
that whelp, so its fall follows the snap.

**Acceptance.** The first true-speed viewing of film 02 reads as "Shadow gathers around the creature, jaws snap onto it,
then the darkness evaporates". It should feel organic and deliberately animated, not like four sprites popping, a
smoke explosion or purple particles. The mist must not reduce fang readability; if it does, the mist is reduced.
