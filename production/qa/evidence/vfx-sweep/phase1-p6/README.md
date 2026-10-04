# Phase 1 / P1.6 evidence: the basic attacks' identity cues

- `swing_cue_levels.md`: each of the 13 cues as played. Columns: K-weighted loudest 50 ms at its recipe's volume x the SFX
  master 0.8, the ceilings (SPRAY / HARD HANDS contact, SPRAY's release), a release against its own hit, the length, the
  tail, and the 1/3-octave distance from the archetype it replaces. Written by `make_swing_cues.py --evidence`; the same
  figures are in `tools/asset-pipeline/swing_cue_levels.json`.
- `mix_in_the_fight.md` / `mix_in_the_fight.py`: the cues in the fight's own soundtrack. film_audio.py renders each take's
  trace through `sound_throttle.json`. Each voice's own share is measured by rendering again with that voice silenced
  (the PRESS method, P1.5's script with the identity voices).
- `p16_*.log` / `p16_*.mp4`: one swing trace take per champion (beat:1, 150 x 2). Each was rendered with sound by
  film_audio.py and re-encoded at 720p / CRF 28. Nobody has listened to them yet; they are the review set.
- `swing_cues.txt` (`check_swing_cues.py`): every swing-contact / swing-release line names the champion's own cue at
  0.36 / 0.16-0.18, with exactly one `sound` line on the same ms.
- `ref_*.log` / `regression.txt`: the five reference takes, filmed twice at the final code (with LungePx on the playhead).
  Under `--refs`, both runs are IDENTICAL x 10 against both ae20a7fd baselines. The `ref_*.log` files here are run B.
- `lunge_playhead.txt` / `lunge_playhead.py`: the creature lunge's traced x at each playhead, compared between two films.
  - With the change (run A vs run B): 0 differing.
  - Before it (P1.4 vs P1.5): one differing playhead in ref_brand (600) and one in ref_jaws_kill (583), x=6 vs x=9.
