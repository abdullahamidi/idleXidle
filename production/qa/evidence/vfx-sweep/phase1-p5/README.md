# Phase 1 / P1.5 evidence: the eight archetype cues

- `archetype_levels.md`: each cue as played (K-weighted loudest 50 ms at its mastered volume x the SFX master 0.8) against
  its ceiling (written by `make_archetypes.py --evidence`; the same figures are in `tools/asset-pipeline/archetype_levels.json`).
- `mix_in_the_fight.md` / `mix_in_the_fight.py`: the same cues in the fight's own soundtrack (film_audio.py's render of
  each take's trace, through `sound_throttle.json`), each voice's own share measured by rendering again with that voice
  silenced (the PRESS method).
- `p15_*.log` / `p15_*.mp4`: five swing trace takes (beat:1, 150 x 2), rendered with sound by film_audio.py and
  re-encoded at 720p / CRF 28. `swing_cues.txt`: each take's swing-contact / swing-release cue and volume.
  - p15_seeker: `sfx_blade_hit` 0.36 beside SPRAY's own contact 0.50 in the same fight.
  - p15_quiver: `sfx_blade_hit` 0.36 + `sfx_throw_release` 0.18.
  - p15_anvil: `sfx_fist_hit` 0.36. p15_tower: `sfx_stone_hit` 0.36. p15_thornwall: `sfx_wood_hit` 0.36.
- `ref_*.log` / `regression.txt`: the five reference takes re-filmed at this code, IDENTICAL x 10 under `--refs` against
  both ae20a7fd baselines.
