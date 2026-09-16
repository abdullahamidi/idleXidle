# Audio assets

Raw **WAV** files (16-bit mono PCM, 44.1 kHz for cues, 22.05 kHz for the beds), loaded at runtime by
`SoundBank` (no MGCB pipeline), keyed by filename without extension. The game runs silently with none
present — each file drops in and starts playing with no code change, exactly like the art.

Every cue is **synthesised** by a script in `tools/asset-pipeline/`, deterministically (a fixed LCG,
no random module), so a rebuild is never a silent diff: `make_sfx.py` writes the UI cues, `make_battle_sfx.py`
the fight and forge cues (and measures them — a fight cue that comes out too bright or too short fails the
build), `make_music.py` the beds. Re-tuning a cue is editing a number there, not rolling a generator.

## The vocabulary (UI polish, 2026-09-01 — brief §86)

One family per MEANING, reused across screens; never one click for everything, never fifty sounds.

| Family | Cue | Where |
|---|---|---|
| Navigation | `sfx_nav` (soft page tick) | nav rail, tabs, screen switches |
| Dispatch arrived | `sfx_dispatch` (paper and a wax seal) | a letter reaches the inbox, once, and only when attention is free |
| Generic button | `sfx_click` (short dry click) | any button with no richer meaning |
| Refusal | `sfx_error` (dull, dead) | a locked tile, a button that cannot take the click |
| Equip | `sfx_equip` (latch and seat) | GEAR equip / take off |
| Training | `sfx_train` (two notes up, short) | TRAIN a rank |
| Skill / build | `sfx_weave` (a soft took), `sfx_bind` (a vow: the one cue with weight) | BUILD |
| Trait | `sfx_trait_lit`, `sfx_trait_terminal` | TRAITS |
| Forge | `sfx_upgrade` (anvil tap), `sfx_forge` (the big anvil: merge), `sfx_reroll` (a shuffle), `sfx_gem` (a socket), `sfx_salvage` (a dry break) | FORGE |
| Chest | `sfx_chest_open` (wood, latch, air) + `sfx_chest_rare` (a glassy shimmer, Epic and up) + `sfx_reveal_tick` (each item landing) | VAULT |
| Reward | `sfx_levelup`, `sfx_conquer`, `sfx_deepen` | rank, conquest, corruption |
| Shield | `sfx_shield_gain` (a cold shimmer forming), `sfx_shield_hit` (a cold tick), `sfx_shield_break` (a crack) | HUNT |
| Fight | `sfx_hit`, `sfx_crit`, `sfx_cast`, `sfx_enemy_down`, `sfx_boss_down`, `sfx_champ_down`, `sfx_boss` | HUNT |

`SoundBank.Play` throttles every cue (a minimum gap per cue, and repeats inside a quarter second play
quieter), so a swarm of bites or ten TRAIN clicks in a row read as many events, not as a wall.

- **Music**: `music_*.wav` — looping beds (title, combat, forge, warren, constellation, map; per-Source
  `music_arena_<source>`).

See `design/audio/asset-generation-audio.md` for the direction.
