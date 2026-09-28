# JAWS Shadow-Mist Polish: the review package (ADR-011, 2026-09-28)

Review page: https://claude.ai/artifact/7UWmmu6T3gPhb59YZSnQ5H

**The brief** ("JAWS Frontal Bite Final Shadow-Mist Polish"): the frontal Shadow bite is APPROVED; do not redesign it.
Polish it into ONE Shadow phenomenon: mist gathers, the fangs condense, the Shadow compresses, snap, the Shadow bursts
outward, the teeth break back into mist, everything evaporates. Do not change the perspective, the fang geometry, the
head placement, the charge colours, the snap timing, the reflected target, ReactionArmed, skill-ID isolation, SPRAY,
HARD HANDS or Core. If the mist reduces readability, reduce or remove it; do not add compensating effects.

**What changed** (films `build/shots/jaws/mist4`; the approved version is `build/shots/jaws/bite4`):
- **One Shadow mist, one lifecycle.** Three soft irregular sprites of one lobe (`fxp_seeker_bite_smoke`), flipped and
  never tilted, drawn BEHIND the creatures: no emitter, no allocations. Each lobe is drawn at the share that stacks to
  the recipe's opacity. Opacity 0.07 at spawn, 0.38 once formed (100 ms), 0.45 at the snap; scale 1.12 to 1.0 while it
  forms, compressed to 0.92 at the snap with its lobes drawn in, stretched a little with the rows' own gap in the wind-up,
  then released slowly to 1.22 while it fades to 0. It never fades in twice. The core is near-black violet (12, 7, 20)
  with a subtle violet edge. Drawn behind the body it cannot veil the creature or the teeth; what shows is a dark haze
  around the bite, restrained on purpose (the brief's opacities, not more).
- **White-hot for one frame.** The teeth are white-hot only on the first frame drawn at the snap; the next frame is
  already magenta.
- **A shorter, cooling flash.** 140 ms with the two-frame peak kept, cooling white to magenta to violet; nothing moves
  toward brighter pink after the snap.
- **Hierarchy.** Fangs and the hot core first; the pressure ring second (its crisp edge hands over to a softened copy
  that dissolves into the mist); the splinters (pale magenta, then darker violet and softer) and the mist third; 13
  speed lines as an accent, gone by 70 ms.
- **The slash is removed.** A directional stroke inside a radial burst read as a blade attack; JAWS is a bite. The part
  moved to `tools/asset-pipeline/v2/keypose_sources/history/`.
- **Quiet mode.** While the champion performs an action (SPRAY, HARD HANDS), and on a second bitten creature, the snap
  is never white-hot, the flash is dimmer (x0.55) and smaller (x0.8), and there are no speed lines. A second creature
  also gets no ring.
- **Unchanged.** Frontal geometry, head placement, charge colours, snap at 316.6 ms, the single bite cue on the snap,
  the number 40 ms after it, a kill's fall on the snap, the reflected target, ReactionArmed, skill-ID isolation, SPRAY,
  HARD HANDS, Core. Duration ~677 ms. The tail is faint mist and the last splinters and does not persist, so no shorter
  tail variant was made.

**Checked** by three independent judges (readability; one phenomenon; hierarchy and context), every phase against
the current version: all three ACCEPT WITH NOTES, no defect. Their shared note: the mist is at the edge of perception
at play speed, and making it stronger would move toward a veil. Under HARD HANDS the flash and ring still paint magenta
over the champion's fist for ~130 ms (as before, but dimmer); the lever, if the owner minds, is to draw them under the
champion in quiet mode.

**Proof.** `reaction-draw` alloc=0 on all 809 traced frames; at most 20 sprites for a two-creature bite. 808 game tests
and 1888 Core tests pass. The asset gate reports no new orphans.

| file | what |
|---|---|
| 01_current_frontal_bite_true_speed_sound.mp4 | the approved frontal bite, true speed, sound |
| 02_polished_mist_bite_true_speed_sound.mp4 | the polish, true speed, sound |
| 03_polished_MUTED.mp4 | the polish, muted |
| 04_close_crop_true_speed_MUTED.mp4 | the bitten whelp, close, true speed |
| 05_phase_sheet_FORM_CONDENSE_CHARGE_WINDUP_SNAP_PRESSURE_RELEASE_DISSOLVE.png | every phase, polished over current |
| 06_repeated_fast_tempo_sound.mp4 | repeated triggers at fast TEMPO (one picture per 3 frames) |
| 07_during_spray_sound.mp4 | JAWS during SPRAY |
| 08_during_hard_hands_sound.mp4 | JAWS during HARD HANDS |
| 09_trace.md | the bite and the answer on the playhead |

Regenerate: `bash build/shots/jaws/films_jaws.sh mist4 normal fast_rearm during_spray during_hh`, then
`PYTHONUTF8=1 python tools/asset-pipeline/jaws_bite_mist_evidence.py production/qa/evidence/jaws-bite-mist`.
