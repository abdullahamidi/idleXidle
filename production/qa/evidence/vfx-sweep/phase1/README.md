# Phase 1 acceptance evidence (P1.7, 2026-10-04)

design.md section 8, Phase 1: the ten basic attacks, the first champion missiles, the archetype and identity cues, the
champion-agnostic tier. The film itself is `production/vfx-sweep/review/phase1/` (INDEX.md); this folder holds the
traces, the regression and the gates.

## Files
- `p17_*.log`: the eleven film takes' traces (A + chapters 01-10), as filmed by `films_sweep.sh` (`p17_*` lines).
- `ref_*.log`: the five Seeker reference takes at the final Phase 1 code; `regression.txt`: `action_regression.py --refs`
  against BOTH ae20a7fd baselines (`../phase0-baseline/ref_*_{a,b}.log`): **IDENTICAL x 10**.
- `measure_phase1.py` -> `measured.md`: the per-take table below.
- `fx_edges.txt`: `check_fx_edges.py` on the Phase 1 part and the four props (+ their `_edge` masks).
- `gates_assets.txt`: `check_asset_consumers`, the audio diff and the 21 new cue files.
- `leftover_selectors.txt`: no new RH_* name, no candidate words, the legacy swing's reachability.

## Measured per take (over each film's window)

| take | window ms | swings | contact == Strike ms | step-in peak px / row gap px (max traced step) | flashes on swing ms | sound asks per swing ms (max) / cues | generic 0.38 thud / puff | releases | swing-draw sprites max / frames / alloc>0 |
|---|---|---|---|---|---|---|---|---|---|
| p17_a_tempo | 4483-6967 | 2 | 2 of 2 (dup 0) | 90/360, 124/497 (124) | 2 (0.30/110) | 3 / sfx_enemy_down 0.36; sfx_seeker_swing_hit 0.36; sfx_trait_lit 0.16 | 0 / 0 | 0 | 3 / 18 / 0 |
| p17_c01_seeker | 1633-4117 | 2 | 2 of 2 (dup 0) | 92/368, 89/357 (92) | 2 (0.30/110) | 1 / sfx_seeker_swing_hit 0.36 | 0 / 0 | 0 | 3 / 20 / 0 |
| p17_c02_anvil | 1633-6600 | 3 | 3 of 3 (dup 0) | 72/359, 70/348 (72) | 3 (0.30/110) | 1 / sfx_anvil_swing_hit 0.36 | 0 / 0 | 0 | 2 / 33 / 0 |
| p17_c03_metronome | 1633-6600 | 2 | 2 of 2 (dup 0) | 127/507, 130/518 (130) | 2 (0.30/110) | 1 / sfx_metronome_swing_hit 0.36 | 0 / 0 | 0 | 3 / 20 / 0 |
| p17_c04_tower | 1633-6600 | 3 | 3 of 3 (dup 0) | 79/395, 77/384 (79) | 3 (0.30/110) | 1 / sfx_tower_swing_hit 0.36 | 0 / 0 | 0 | 2 / 45 / 0 |
| p17_c05_thornwall | 1633-6600 | 3 | 3 of 3 (dup 0) | 65/326, 63/315 (65) | 3 (0.30/110) | 1 / sfx_thornwall_swing_hit 0.36 | 0 / 0 | 0 | 1 / 6 / 0 |
| p17_c06_magpie | 1633-6600 | 3 | 3 of 3 (dup 0) | 88/351, 85/340 (88) | 3 (0.30/110) | 1 / sfx_magpie_swing_hit 0.36 | 0 / 0 | 0 | 2 / 30 / 0 |
| p17_c07_quiver | 1433-6633 | 4 | 4 of 4 (dup 0) | 0 (gap 298-313) | 4 (0.30/110) | 2 / sfx_hit 0.30 (the pack's bite); sfx_quiver_swing_hit 0.36; sfx_trait_lit 0.16 | 0 / 0 | 4 x sfx_quiver_loose 0.18 | 11 / 111 / 0 |
| p17_c08_chorus | 1633-6600 | 3 | 3 of 3 (dup 0) | 0 (gap 390-402) | 3 (0.30/110) | 1 / sfx_chorus_swing_hit 0.36 | 0 / 0 | 4 x sfx_chorus_toss 0.16 | 9 / 90 / 0 |
| p17_c09_unbroken | 1633-6600 | 3 | 3 of 3 (dup 0) | 0 (gap 306-318) | 3 (0.30/110) | 1 / sfx_unbroken_swing_hit 0.36 | 0 / 0 | 4 x sfx_unbroken_toss 0.16 | 10 / 95 / 0 |
| p17_c10_oathbound | 1633-6600 | 3 | 3 of 3 (dup 0) | 0 (reach; strand taut=1 on 3 of 3) | 3 (0.30/110) | 1 / sfx_oathbound_swing_hit 0.36 | 0 / 0 | 0 | 16 / 73 / 0 |

- Step-in shares: seeker / metronome / magpie 25 %, anvil / tower / thornwall 20 %, as design.md 5.18-5.27. The step is
  0 at every attack clip-end in all eleven takes (`../phase1-p3/check_swing.py`: 0 non-zero of 39).
- Every other field / reaction / mark layer: `reaction-draw` and `mark-draw` alloc 0; `field-draw` (PRESS) shows its
  known first-use allocation (max ~13 MB, carried issue, notes.md).
- Window = trace-clock >= shot 0, i.e. what the film shows; the rig's pre-seek live run is excluded.

## Gates (final code)
- `dotnet build IdleXIdle.sln -c Release -warnaserror --no-incremental`: 0 warnings, 0 errors.
- Game.Tests 1213 / 1213, Core.Tests 1901 / 1901. The JAWS / PRESS / BRAND pin tests (174 incl. the SHA-256 pins) green.
- `bash tools/check_all.sh`: all gates green.
- `check_fx_edges.py`: SOFT and EDGE pass on all nine files (edge 0; soft 2.25-25.56 %). LIVE fails on all nine by
  nature: they are alpha-blended materials, not light strips (recorded in P1.3 / P1.4); LIVE is not a required rule here.
- `check_asset_consumers`: no new orphans; all 9 art files and all 21 cues are reached.
- `git diff ae20a7fd -- assets/audio`: empty for tracked files; `git status assets/audio`: exactly the 21 new cues.

## The director's review film (p1_* plan, 2026-10-04, supersedes the p17 film)
- `p1_*.log`: the ten film takes (A + 02-10), `measured_p1.md`: the per-take table (`measure_phase1.py p1_*.log`).
- `ref_*.log` and `regression.txt` were REPLACED by the re-filmed refs at the final code (incl. the seek-rig fixes
  SwingPerformance.ForgetVoiced and the FIRST BEAT rebuild): IDENTICAL x 10 against both ae20a7fd baselines.
- The p17_*.log files above stay as the superseded film's traces.
