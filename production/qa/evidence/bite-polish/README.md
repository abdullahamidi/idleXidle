# The bite foundation, polish pass: review battery (ADR-012, 2026-09-26)

**What changed since the first battery** (`production/qa/evidence/bite-foundation/`, the "CURRENT foundation" side of
every comparison here):
- **The Shadow maw.** The whelp's commit and contact poses carry a temporary mouth, painted into the head from its own
  eyes and clipped to its own silhouette (`tools/asset-pipeline/v2/umbral_swarm_bite.py`, `paint_maw`): opening on the
  commit, wide with three pale teeth on the pre-contact thrust, shut with the teeth meeting on the contact frame,
  gone by the follow-through. The idle creature is untouched.
- **The spacing.** The wind-up ease 2.4 → 1.8 (the commit poses start ~225 and ~105 ms before contact instead of the
  last ~100 ms); the lunge's commit runs from −220 ms with a near-linear ease (1.15): 33 % of the travel done 120 ms
  out, 71 % at 50 ms. The travel is unchanged (30 % of the leader's visible width, 80 px on the whelp).
- **The recoil.** 6 % → 10 % of the Seeker's visible width (16 → 27 px) with the 1.5 % dip, the same curve (peak +33,
  home +130 ms). 6, 8 and 10 were filmed from one build (`RH_SHOT_RECOIL`, a dial the rig turns).
- **The contact.** The bite burst is a small mark (0.16 of his height, ~290 ms) at his enemy-facing CHEST edge, with a
  compression wedge and two ember streaks along the force; the centred 0.36 / ~570 ms burst is gone. Chest, not belt:
  at belt height his forward edge is his sword hand, and a mark there read as "a burst at his hand, he fired".
- Approved contracts kept: front-led lockstep, the layered recoil (no hurt clip, no retiming, re-impulse), the
  quiet generic flash (0.45 / 0.15 / 80 ms) with recipe overrides, the generic capture views, no hit-stop.

**The fight.** The same seeded fight as every battery (`RH_SHOT_SEED=7`, the reaction layer off, the legacy ring held
out of the build output for the takes). SPRAY and HARD HANDS land on the same playheads as before
(`action_regression.py`: only creature x positions differ).

| # | File | What |
|---|---|---|
| 1 | `01_*`, `01a` | the CURRENT foundation (first pass) beside the NEW bite, true speed; CURRENT alone with sound |
| 2 | `02_*` | the new bite, true speed, with the fight's own sound |
| 3 | `03_*`, `03b` | the new bite with tint, effects AND flash off (the acceptance view); CURRENT vs NEW bare |
| 4 | `04_*` | silhouette only |
| 5 | `05_*` | the key poses: the crouch strip above, the bite strip with the maw below |
| 6 | `06_*`, `06b`, `06c` | the spacing: every frame from −300 ms at 1:1, CURRENT vs NEW; the same 4x slower; the trace |
| 7 | `07_*` | the four-whelp pack, front-led |
| 8 | `08_*`, `08b`, `08c` | the recoil at 6 / 8 / 10 %, effects and flash off: true speed, 4x slower, every frame |
| 9 | `09_*` | the receiver alone at the chosen 10 %: effects and flash off, muted |
| 10 | `10_*`, `10b` | the contact: CURRENT centred burst vs NEW edge mark + wedge, 1:1; every frame |
| 11 | `11_*`, `11b` | repeated combat at fast tempo with the music; CURRENT vs NEW |
| 12 | `12_*` | hits during SPRAY (in its wind-up and in its flight) |
| 13 | `13_*`, `13b` | a hit as HARD HANDS leaps; the same with everything off |
| 14 | `14_*`, `14a`–`14c` | rough Concept A over the final foundation: normal tempo, during SPRAY's flight, during HARD HANDS |
| — | `15_blind_reads.md` | the blind reads |

**Review page:** https://claude.ai/artifact/W1w3jG8XpHJLUdbvDCbWaE

**Reproduce:** build once; `bash tools/asset-pipeline/foundation_films.sh` (the first pass's takes copied to
`build/shots/foundation_v1/` stand as CURRENT); the A overlays with `prototypes/jaws-concepts/animatic.py` over
`new` (7000:1042,810), `rep` (2000:1042,805) and `hh` (13000:1450,812); then
`PYTHONUTF8=1 python tools/asset-pipeline/polish_evidence.py production/qa/evidence/bite-polish <blind dir>`.
