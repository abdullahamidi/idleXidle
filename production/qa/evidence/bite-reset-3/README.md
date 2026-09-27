# The bite reset, THIRD PASS: review package (ADR-012, 2026-09-27)

Review page: https://claude.ai/artifact/ChyaL2RkT8PWsSbdqywbey

**The owner's brief.** The second pass's puppet strip and the M4 motif are not production reference art. A2
MIRRORED SHADOW SNAP is APPROVED as the JAWS design direction (its M4-derived art is not). Two root problems: the attack
art was still constrained by the idle art's bounds; M4 read as a glyph. Keep every accepted contract; never a
trap, a maw, a whole-body recoil, or Concept D. This is the last structural art pass before deciding whether the
foundation can close.

**What was done.**
1. **Art-space rule** (ADR-012 decision 9). An authored action clip may occupy more art space than its idle: larger
   square frames, the canonical idle frame in each frame's bottom-right corner. `UiKit.ResolveFrame` places such a
   strip on the idle's scale, ground and anchor, so the actor is never rescaled, its feet never move, the row's
   grounding is untouched; the REST frame draws pixel-identically to the 512 draw (diffed). The 512 canvas genuinely
   could not hold the pose: the rest head sat at x 39 of 512; the strip is now 8 × 640 × 640.
2. **Genuinely redrawn poses.** COIL, COMMIT, CONTACT, FOLLOW-THROUGH are PixelLab pose edits made in the wider art
   space with a strict same-creature prompt, then repaired deterministically (`umbral_swarm_attack_640.py`: re-inked
   from the original's fill and rim colours, eyes rebuilt at the head's front, every pose grounded on the rest sole,
   COMMIT set behind CONTACT). REST / RECOVER are the idle pose. No deformation of the rest image; nothing generated
   used raw. The CONTACT head reaches x 8 of 640: 159 texels ahead of the rest head.
3. **Root motion** 15 / 20 / 25 % filmed; 20 % chosen (15 twitches, 25 floats). FACT: at contact the leader's front
   edge is still ~330 px from the Seeker's torso edge; the row's home distance, not the travel, decides whether a
   bite ever "connects". A layout decision for the owner.
4. **Motif N2, the offset fang snap.** Three directional asymmetric candidates (N1 asymmetric crescents, N2 offset
   fang, N3 broken serrated arcs), no bilateral symmetry, no central dot, roots on the enemy side. Static reads
   asking what impact the shape suggests: all "pinch / clamp / bite", none slash, claw, spark or icon; N2 4/5 for
   closing from the right and "an event on his body, not an icon". N2 is in the build (`HuntScreen.FangSnap`) with
   M4's approved timing.
5. **A2 in the N2 family.** Mirrored (from the Seeker's side), ~1.2×, a longer serrated fang, a dark internal fill
   under the violet rim, a shard residue; no ownership line; no trap sound. Anchored on the biter's head from
   silhouette takes.

| # | File | What |
|---|---|---|
| 1 | `01_*` | the second pass's puppet strip, root motion OFF |
| 2 | `02_*` | the redrawn strip on the extended canvas, ROOT MOTION OFF, VFX / tint / flash OFF: the gate |
| 3 | `03_*`, `03b`, `03c` | puppet vs redrawn at play size, root OFF; 4× slow; every frame 1:1 |
| 4 | `04_*` | silhouette only |
| 5 | `05_*`, `05b`, `05c` | the key-pose strip sheet; the root-OFF key-pose row at play size; the PixelLab references |
| 6 | `06_*`, `06b` | root motion 15 / 20 / 25 % over the redrawn strip, bare; 4× slow |
| 7 | `07_*`, `07b` | the chosen full attack, bare; as played with sound |
| 8 | `08_*` | N1 / N2 / N3 open and closed, 1:1 and 2× |
| 9 | `18_blind_reads.md` | static impact reads of N1 / N2 / N3; flipbook reads |
| 10 | `10_*`–`10d` | N2 at true speed: every frame, 4× slow, M4 vs N2, the playhead trace |
| 11 | `11_*`–`11d` | repeated pack attack; the receiver alone; regression: the killing hit, the boss |
| 12 | `12_*` | the old M4-derived A2 (not approved), muted |
| 13 | `13_*` | A2 in the N2 family, muted, true speed |
| 14 | `14_*` | A2 beside the fight without it |
| 15 | `15_*` | A2 during SPRAY |
| 16 | `16_*` | A2 during HARD HANDS |
| 17 | `17_*`–`17c` | repeated bite → Shadow bite-back: every frame, 4× slow, normal tempo |

**Build changes** (branch `fix/vfx-fade`): `UiKit.ResolveFrame` extended-canvas branch; `HuntScreen` passes
`placeAs: look.IdleStrip` for creature and boss attacks (and the silhouette overlay); `HuntScreen.FangSnap` replaces
`Jaw`; `BitePresentation.LeaderLunge` 0.20; `umbral_swarm_attack_640.py` (new) with the four pose references under
`keypose_sources/umbral_swarm_lunge_poses/`; `bite_motif_candidates_n.py`; `animatic.py` `fang_polys` / `draw_a2`;
`foundation_films.sh` `new_l15`, `bare_l15/20/25`, `rep_sil`, `hh_sil`; `reset3_evidence.py`. Tests: 810 green
(`enemy_presentation_test` now allows attack strips of 8 square frames ≥ 512; the lunge share at −120 ms ≥ 0.25).
`action_regression.py` against the second pass: SPRAY / HARD HANDS timings unchanged; the HARD HANDS launch x differs
by 7 px (the target's new silhouette).
