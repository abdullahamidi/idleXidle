# The bite reset, SECOND PASS: review package (ADR-012, 2026-09-27)

Review page: https://claude.ai/artifact/JXrqFPAbbbMsJczyknnjcm

**The owner's brief.** The reset direction is approved; the warped strip and the claw-shaped motif are rejected as
final art. Two art problems only: (1) a genuinely authored whelp BITE attack strip, judged with root motion OFF;
(2) a contact motif that reads BITE SNAP, not claw / slash, judged statically on the target alone, then at true speed.
Then re-evaluate A2 in the same shape family. Keep: F2 flash, front-led pack timing, root-motion capability, recoil
default 0, no hit-stop, SPRAY and HARD HANDS, JAWS as a layered Reaction. Never again: a whelp mouth, bear traps,
mechanical JAWS, whole-body normal-hit recoil, Concept D.

**Evidence built by** `tools/asset-pipeline/reset2_evidence.py` from `foundation_films.sh`'s takes (the same seeded
fight as every battery, the reaction layer off). The WARPED side is the first reset's takes (`build/shots/foundation_warp/`).

| # | File | What |
|---|---|---|
| 1 | `01_*`, `01b` | the current WARPED strip (first reset): root motion OFF; root motion ON, bare |
| 2 | `02_*`, `02b`, `02c` | the newly AUTHORED strip with ROOT MOTION OFF (the acceptance test); warped vs authored close, true speed and 4× slow |
| 3 | `03_*` | every frame at play size, root OFF: warped above, authored below |
| 4 | `04_*`–`04e` | the authored strip with the lunge travel series 20 / 25 / 30 % (bare and everything on); the chosen 25 % bare; silhouette |
| 5 | `05_*`–`05d` | key-pose sheets: crouch strip vs authored; the root-OFF key-pose row at play size; the warped row for comparison; the two PixelLab pose concepts (references only) |
| 6 | `06_*` | the four motif candidates M1 / M2 / M3 / M4, open and closed, 1:1 and 2× |
| 7 | `07_*` | the selected motif (M4) open / closed at 3× |
| 8 | `17_blind_reads.md` | blind shape reads of the candidates on the target alone; blind flipbook reads |
| 9 | `09_*`–`09d` | the selected motif at true speed: every frame, 4× slow, first reset vs second pass, the playhead trace |
| 10 | `10_*`, `10b` | the full routine hit: authored lunge + motif + usual flash + sound + number; the receiver alone (nothing moves) |
| 11 | `11_*`–`11c` | repeated pack bites at fast tempo; regression: the killing hit, the boss |
| 12 | `12_*` | rough A2 (mirrored jaws, Shadow) over the second pass, beside it without |
| 13 | `13_*` | A2 muted, true speed (no trap sound; audio is a later pass) |
| 14 | `14_*` | A2 during SPRAY (bites at 1000 and 2000) with the fight's own sound |
| 15 | `15_*` | A2 during HARD HANDS |
| 16 | `16_*`–`16d` | repeated bite → bite-back: grid, every frame, 4× slow, normal tempo |

**What changed in the build** (branch `fix/vfx-fade`):
- `tools/asset-pipeline/v2/umbral_swarm_lunge.py` (new): the whelp's COIL, COIL deep, COMMIT, COMMIT fast, CONTACT,
  FOLLOW-THROUGH and RECOVER authored as a JOINT PUPPET of its own rest frame: 18 handles (head, crown, neck,
  shoulder, back, hip, hands, elbow, knee, feet, six on the tail) placed per pose, the image deformed between them by
  rigid moving-least-squares. Identity, palette, eyes, line style untouched; no mouth; nothing generated. The column
  warp is retired (its strip kept under `keypose_sources/` for the comparison).
- PixelLab: two 256 px pose-concept edits (COMMIT, CONTACT) with a strict identity prompt; identity held this time;
  used as references for the puppet targets only (`05d`).
- `HuntScreen.Jaw` / `DrawBiteContact`: the M4 motif (a broad crescent jaw + one fang point per half, converging on
  the contact centre) at his TORSO's enemy-facing edge under the hood (`body.X + 0.57 w, body.Y + 0.39 h`). Measured
  from a champion-free plate (`recv_nochamp`): his visible rect's right edge is his sword tip and belt height is his
  sword hand, which is why the first motif sat on the blade. Open gap smaller (0.08–0.32 of the span); residue gone by
  70 ms.
- `BitePresentation.LeaderLunge` 0.25 (was 0.30); `Lunge(..., travel)`; `CaptureViews.LungeOverride`
  (`RH_SHOT_LUNGE`) films 20 / 25 / 30 from one build.
- `prototypes/jaws-concepts/animatic.py` A2: the motif's own shape family in Shadow, 1.25×, no ownership line, no
  dry-steel cue (skipped for A2).
- `foundation_films.sh`: `new_l20`, `new_l25`, `new_l30`, `bare_l20`, `bare_l30`, `recv_nochamp`.
- Tests: Game 810 green. `action_regression.py` against the warp pass: SPRAY / HARD HANDS timings unchanged; the
  HARD HANDS launch and reach x differ by 8 px (the target's new silhouette).

**Timing (`09d_trace.md`).** Contact = the pump frame (7017 for the 7000 bite). Commit from −234 ms; the leader's
25 % lunge crosses ~67 px over the last 220 ms; the motif is OPEN at −33 (0.70) and −17 (0.35), SHUT at +0 with the
fang tips crossed, strength 0.98 at +17, 0.46 at +33, 0.14 at +50, gone by +67.

**Decision gates, as filmed.**
- Whelp, root OFF + VFX OFF (`02`, `03`, `05b`): the poses progress coil (gathered, low, head withdrawn) → commit
  (head forward and DOWN, body extending) → contact (the longest, lowest silhouette, head leading) → follow-through
  (compressed, the tail whipping forward). Three key-pose readers at play size named the verb "lunging" and the
  contact frame "impact / lunge, not rearing" every time; the SEQUENCE scored 2–2.5/5 on paper because the
  anticipation is under-read there, as motion always is in these flipbooks. The owner's true-speed eye decides.
- Motif (`06`, `07`, `17_blind_reads.md`): all four candidates read as TWO THINGS CLOSED ON HIM; the first words
  were clamp / pinch / clamp / clamp with bite as an alternative every time; never slash, scratch or claw. No
  candidate reached "bite" above 2/5 without drawing teeth, which the brief forbids. M4 (jaws + fang points) is
  selected: the broadest shapes at play size, the strongest open → closed change, and the family A2 can rhyme with.
