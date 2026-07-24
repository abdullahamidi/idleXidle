# Asset Generation — Audio ("give it a voice")

The game has no sound yet. The **code is now ready for it**: a `SoundBank` loads raw WAVs at startup
and plays named cues, and every cue is already wired at its trigger point (combat hits, breaks,
defends, ability casts, kills, captures, training, boss conquest, corruption deepening) plus a looping
music bed per screen. Like the art, **the game runs perfectly silent** until the files exist — each WAV
you drop in starts playing with no code change.

> This is the audio twin of the art batches. Same drop-in philosophy, same "missing = graceful
> silence" guarantee. No MonoGame content pipeline: raw WAV only.

---

## Hard technical constraints (please follow exactly)

- **Format: WAV, PCM, 16-bit.** Mono for SFX, mono or stereo for music. Loaded via
  `SoundEffect.FromStream` — it does **not** accept MP3/OGG/FLOAT WAV. If in doubt, export
  "16-bit PCM WAV".
- **Sample rate: 44100 Hz** (22050 is fine for small SFX). Keep it consistent.
- **Filenames are the keys** — lowercase, underscores, exact. `sfx_weakhit.wav`, not `Weak Hit.wav`.
- **SFX are short** (50–400 ms) and should be **normalized but not brick-walled** — they stack
  (several can play at once), so leave headroom. The engine applies a master SFX volume (0.8) on top.
- **Music loops seamlessly.** It's played as a looped one-shot — the end must flow back into the start
  with no click or gap. Engine applies a master music volume (0.5) on top, so mix it to sit *under* the
  SFX. Keep files reasonable (a 30–90 s loop is plenty; these are uncompressed, so ~1–8 MB each).

Folder: **`assets/audio/`** (flat is fine; subfolders are cosmetic — the loader scans recursively).

---

## Priority order

1. **Core combat SFX** — hit / weakhit / break / hurt. This is what makes a fight *feel*.
2. **Defensive + resolution SFX** — block / dodge / interrupt / death / capture.
3. **One music bed for combat** — the single biggest "it has a soundtrack now" jump.
4. **Progression stingers** — train / conquer / deepen, and the ability casts.
5. **Per-screen music** — forge / warren / constellation / map / title.
6. *(Optional)* per-region combat beds.

---

## 1. Combat SFX  →  `assets/audio/`

One-shots. These fire from the combat event stream; several can overlap.

| File | When it fires | Character |
|---|---|---|
| `sfx_hit.wav` | A normal (non-weak-point) hit lands | A dull, meaty thud. Not sharp — this is the "chip" damage. |
| `sfx_weakhit.wav` | A **weak-point** hit — the payoff | Bright, satisfying *crack* / crystalline hit. Clearly better than `sfx_hit`. |
| `sfx_crit.wav` | A **critical** weak-point hit (on a FLOW streak) | The biggest, most satisfying hit — `sfx_weakhit` turned up to 11. |
| `sfx_parry.wav` | A **perfect read** (right defence for the attack) | A crisp, bright *ting/flash* — the "nailed it" reward. The signature sound. |
| `sfx_break.wav` | A body part **shatters** | A hard shatter/snap with debris tail. |
| `sfx_hurt.wav` | An enemy attack lands on **you** (unavoided) | A blunt, downbeat impact — the "you got hit" gut-punch. |
| `sfx_block.wav` | A **grazed** wrong read (partial defence) | A dull scrape/clip — you defended, but wrong. Less satisfying than `sfx_parry`. |

## 2. Defence & resolution SFX  →  `assets/audio/`

| File | When | Character |
|---|---|---|
| `sfx_block.wav` | A successful block | A metallic *clang* / guard-deflect. Still a little painful. |
| `sfx_dodge.wav` | A dodge (zero damage) | A quick air *whoosh* / cloth swish. |
| `sfx_interrupt.wav` | An interrupt (the best defence) | A crisp, authoritative *chnk* / stagger — the most satisfying defensive sound. |
| `sfx_death.wav` | The creature is defeated | A dissolving, downward *dissipation* — creature unmaking, not gore. |
| `sfx_capture.wav` | A rare creature is captured | A rising *ward-seal lock* — chime + click, hopeful. |

## 3. Progression & ability SFX  →  `assets/audio/`

| File | When | Character |
|---|---|---|
| `sfx_train.wav` | A Hunter training rank is bought | A short, warm confirming tone — a "power up" tick. |
| `sfx_conquer.wav` | A region boss is beaten / region conquered | A triumphant short fanfare stinger. |
| `sfx_deepen.wav` | The corruption is deepened (endgame) | A dark, ominous swell — power at a price. Falls back to `sfx_conquer` if absent. |
| `sfx_ability_emberlance.wav` | Ember Lance cast | A sharp fiery lance *fwoosh-crack*. |
| `sfx_ability_ruinstrike.wav` | Ruin Strike cast | A torn, shadowy *slash*. |
| `sfx_ability_lastbreath.wav` | Last Breath cast | A soft, ghostly *bloom* / glyph pulse. |

*Fallbacks:* each ability first tries `sfx_ability_<name>` then a generic `sfx_ability.wav` — so a single
`sfx_ability.wav` covers all three until you want distinct ones.

---

## 4. Music beds (looping)  →  `assets/audio/`

Seamless loops. One plays at a time; the engine cross-swaps as the player changes screens.

| File | Screen | Direction |
|---|---|---|
| `music_combat.wav` | Any fight (default) | The main hunt theme — tense, driving, but not exhausting to loop for an hour. |
| `music_title.wav` | Title / main menu | Moody, atmospheric, inviting. Sets the tone. |
| `music_forge.wav` | The Forge | Warm, low, workmanlike — a smithy hum. |
| `music_warren.wav` | Automation / farm | Calm, sleepy, organized — the idle den. |
| `music_constellation.wav` | Memory Dust | Sparse, wondrous, celestial. |
| `music_map.wav` | World map | Expansive, journeying — "where next?". |

**Optional per-region combat beds:** `music_arena_machine.wav` (Cinderworks — industrial, heavy) and
`music_arena_shadow.wav` (Umbral Reach — dark, creeping). If present they replace `music_combat` in that
region automatically; if absent, `music_combat` plays everywhere. Verdant Hollow always uses
`music_combat`.

---

## Naming recap

```
assets/audio/
├── sfx_hit.wav  sfx_weakhit.wav  sfx_break.wav  sfx_hurt.wav
├── sfx_block.wav  sfx_dodge.wav  sfx_interrupt.wav  sfx_death.wav  sfx_capture.wav
├── sfx_train.wav  sfx_conquer.wav  sfx_deepen.wav
├── sfx_ability.wav  (or the three sfx_ability_*.wav)
└── music_*.wav  (title / combat / forge / warren / constellation / map [+ arena_machine/shadow])
```

Exact lowercase filenames — the loader keys on them. Deliver in any order; a missing cue is simply
silent, never a crash. Drop a batch in `assets/audio/` and it plays next launch.

---

## What's already wired (so you know it's not just a wish-list)

- `SoundBank` (raw-WAV loader, master SFX/music volumes, graceful no-op, safe on machines with no audio
  device and during headless screenshot runs).
- Every SFX above is called at its real trigger (combat events, ability casts, training, conquest,
  deepening), panned by where the hit landed.
- `music_*` swaps automatically with the active screen, with the per-region combat fallback.

So: generate WAVs, drop them in `assets/audio/`, launch. That's the whole integration.
