# The bite RESET / PROTOTYPE: review battery (ADR-012, 2026-09-27)

**The owner's brief.** Stop polishing the maw: the painted mouth and the whole-body receiver recoil are REJECTED. Keep
the front-led synchronized pack contact, the authored root-motion capability, the capture tooling, the short generic
flash (0.45 / 80 ms) with recipe overrides, no hit-stop, SPRAY and HARD HANDS. New principle: the ATTACK VERB is the
body (a predatory lunge), the MATERIAL / CONTACT IDENTITY is a bite-shaped motif at the contact. No JAWS Concept D, no
VFX backlog, no Core change. A prototype, reviewed at true speed; JAWS is not accepted.

**What this battery contains** (from `tools/asset-pipeline/foundation_films.sh`, the same seeded fight as every
battery, the reaction layer off; evidence by `tools/asset-pipeline/reset_evidence.py`; the REJECTED side is the maw
pass's takes, copied to `build/shots/foundation_maw/`):

| # | File | What |
|---|---|---|
| 1 | `01_*`, `01b` | the REJECTED maw version (true speed, sound); the rejected beside the reset |
| 2 | `02_*` | the mouthless lunge with tint, effects and flash OFF (the acceptance view) |
| 2b | `02b_*` | the same with ROOT MOTION OFF: the strip's poses alone (the brief's test: crouch → crouch → crouch = the art fails) |
| 2c–f | `02c`–`02f` | silhouette; the key poses (crouch strip above, lunge strip below); every frame at play size root OFF / root ON / rejected; a close 4× slow grid |
| 3 | `03_*` | the lunge WITH the bite contact motif, true speed, sound |
| 3b–d | `03b`–`03d` | every frame of the motif at his chest edge; a 4× slow close-up; the playhead trace |
| 4 | `04_*`, `04b` | the receiver alone with effects and flash off: nothing moves; the rejected 10 % recoil beside no translation |
| 5 | `05_*`, `05b` | the optional micro-impulse: 0 % (default) / 1 % (~3 px) / 2 % (~5 px), true speed and 4× slow |
| 6 | `06_*`, `06b` | rough JAWS A2 MIRRORED SHADOW SNAP over the reset, normal tempo (the front whelp dies of the reflection in this fight) |
| 7 | `07_*` | A2 at fast tempo: bites at 1000 (SPRAY winding up) and 2000 (SPRAY in flight) |
| 8 | `08_*` | A2 as HARD HANDS leaps (the bite at 13000) |
| 9 | `09_*`–`09c` | repeated bite → retaliation: the fast take with and without A2; every frame of A2; a 4× slow close-up |
| 10 | `10_*`, `10b` | regression: the killing hit at fast tempo, the boss |
| — | `blind/` | four half-size every-frame flipbooks read by fresh readers (`11_blind_reads.md`) |

**What changed in the build** (branch `fix/vfx-fade`):
- `tools/asset-pipeline/v2/umbral_swarm_bite.py`: the maw code is gone; the strip is authored by column-wise warps of
  the creature's own frames (per-column shift, vertical scale, torso lift with the legs planted): COIL, COIL deep,
  COMMIT, COMMIT fast (a smear), CONTACT, FOLLOW-THROUGH, RECOVER. The whelp's face is untouched.
- `BitePresentation`: `RecoilShare` 0 (the curve, the dial and the tests stay); `MotifOpen` / `MotifStrength` (open
  ≤ 24 ms before the contact, full on it, hold 16 ms, gone by 90 ms).
- `HuntScreen.DrawBiteContact`: the two-fang motif at his enemy-facing chest edge (replaces the wedge); the
  `impact.bite` burst is no longer spawned and its profile is removed; traces `bite-motif-open`, `bite-motif-snap`,
  `bite-motif` per frame.
- `prototypes/jaws-concepts/animatic.py`: concept `A2`.
- `foundation_films.sh`: `recv_only`, `rec01`, `rec02` replace the 6/8/10 % series.

**Timing, from the trace (`03d_trace.md`).** The shown contact is the pump frame (+0 = 7017 for the 7000 bite). The
commit starts −234 ms; the leader's lunge crosses 80 px over the last 220 ms; the motif is OPEN at −33 (0.70) and −17
(0.35, nearly shut), SHUT at +0 with the streak, tips crossed to +17, strength 0.59 at +33, 0.29 at +50, 0.10 at +67,
gone by +83. SPRAY and HARD HANDS timings are unchanged (`action_regression.py`: only root-x samples differ, the
recoil being off).
