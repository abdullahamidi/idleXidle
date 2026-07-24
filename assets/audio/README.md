# Audio assets

Raw **WAV** files, loaded at runtime by `SoundBank` (no MGCB pipeline), keyed by filename
without extension. The game runs silently with none present — each file drops in and starts
playing with no code change, exactly like the art.

- **SFX**: `sfx_*.wav` — short one-shots (hit, weakhit, break, block, dodge, interrupt, hurt,
  death, capture, train, conquer, deepen, ability[_emberlance/_ruinstrike/_lastbreath]).
- **Music**: `music_*.wav` — looping beds (title, combat, forge, warren, constellation, map;
  optional per-region `music_arena_machine` / `music_arena_shadow`).

See `design/audio/asset-generation-audio.md` for the full spec.
