# Audio brief: the Seeker's SPRAY (the authored action's three cues)

> **Status (2026-09-24): CANDIDATES IN, NOT APPROVED BY EAR.** The three cues below exist
> (`tools/asset-pipeline/make_action_sfx.py`, synthesized from the same layered kit as the fight's
> shipped cues), and are measured and wired. **Nobody has listened to them yet.** The capture rig is
> silent, and the model that built them cannot hear. Only a listening test on real speakers or headphones
> decides whether they are right. This brief is the contract any replacement must meet: from a sound
> designer, a licensed library, or a new synthesis pass. Evidence lives in
> `production/qa/evidence/action-presentation-slice/polish/`. It includes audio-on films rendered from
> the game's own trace (`tools/asset-pipeline/film_audio.py`).

## What the player hears (the phrase)

SPRAY is ONE action heard as ONE phrase:

```
release ──── 250 ms of flight (nothing of ours plays) ──── CONTACT  tick tick
beat−250 ms                                                beat     +18 +36 ms
```

| Moment | Cue | Game volume | Pan | Plays when |
|---|---|---|---|---|
| Release | `sfx_seeker_spray_release` | 0.40 | ±0.3 × the hand's x | the frame the knives leave the hand (beat − 250 ms) |
| Contact | `sfx_seeker_spray_hit` | 0.50 | ±0.3 × the fan's centre | ONCE, on the beat, however many knives land (1–5) |
| Width | `sfx_seeker_spray_tick` | 0.20 | ±0.3 × each outer target | fans of 3 or more only: at most 2, at +18 ms and +36 ms, at the outermost blades |

- **The mix:** from the release until 160 ms after the contact, every other one-shot in the fight plays
  at **45 %** of its volume (an enemy's bite, another skill's cast, a death). This is a duck, not a mute:
  those sounds still carry combat information. The generic `sfx_hit` and hit puff for the SPRAY's own
  hits are replaced outright, because the contact describes the same blow.
- The engine adds ±0.04 (release), ±0.05 (contact) and ±0.08 (tick) octave of random pitch per play, and
  applies the SFX master (0.8). A cue must survive both: no tuned pitch it depends on.
- Fallback chain (a missing file never breaks anything): release → `sfx_throw_release` → `sfx_cast`;
  contact → `sfx_blade_hit` → `sfx_hit`; tick → `sfx_blade_tick` → silence.

## The three cues

### `sfx_seeker_spray_release`: SPEED, not weight

What it depicts: a flick of the wrist and a fan of small knives leaving the fingers.

- **Must:** attack in 1–3 ms. Most of its energy in the first 60 ms. −20 dB by ~140 ms, gone by ~200 ms.
  Bright: spectral centroid roughly 3–4.5 kHz. A dry cloth/leather flick, a short air displacement that
  rises in pitch as the blades leave, and at most a faint brush of steel as the blades separate.
- **Must not:** have low end below ~200 Hz (no whoomp: nothing heavy is thrown). Must not swell INTO the
  contact (it must be over well before the knives land, or the flight stops being silent). No magic
  shimmer or reverse-cymbal riser: the Source colour is in the picture, not the sound. No sword-draw
  "schwing" either: nothing is drawn, the knives are already in the hand.
- Current candidate: 200 ms file, peak −11.7 dBFS, centroid 3932 Hz, −20 dB at 121 ms.

### `sfx_seeker_spray_hit`: blades going into BODIES

What it depicts: several thin blades punching into creatures at once. It is heard as one blow with width.

- **Must:** a sharp tip transient (2–5 ms), then a short dull puncture body (the substance: "it went IN"),
  a little leather/flesh texture, and a light weight. −20 dB by ~80 ms, done by ~160 ms. Clearly brighter
  and shorter than the basic swing's `sfx_hit` (a dull thud, centroid ~330 Hz, 360 ms), so the skill is
  never heard as a punch. Target centroid 1.2–2 kHz.
- **Must not:** RING. Any metallic content must be a glint inside the first ~60 ms, not a tone. The first
  candidate rang at 2.25 kHz for 250 ms: from 80 ms on, 90 %+ of it was one pure tone, which says
  "a blade hit metal". It must not be five hits flammed together (that is what the ticks are for, quietly).
  No gore squelch: the creatures are shadow/beast, the tone is a hunt, not a slasher. No bass boom.
- Current candidate: 280 ms file (sound ends ~160 ms), peak −8.4 dBFS, centroid 1530 Hz, −20 dB at 75 ms.

### `sfx_seeker_spray_tick`: the fan's width

What it depicts: the outermost blades landing a hair later than the centre.

- **Must:** a tiny, bright version of the contact's tip: ~50–100 ms, −20 dB by ~70 ms, centroid ~3–4 kHz.
  At 0.20 volume against the contact's 0.50 it must read as detail, not as a second hit.
- **Must not:** carry its own body or weight (two ticks plus the contact must never sound like three hits).
- Current candidate: 100 ms, peak −10.5 dBFS, centroid 3284 Hz, −20 dB at 68 ms.

## Delivery format (same as every cue in the game)

- 16-bit PCM WAV, mono, 44 100 Hz. `SoundEffect.FromStream` refuses float WAV, MP3 and OGG.
- Peak between −12 and −8 dBFS, not brick-walled: cues stack, and the engine applies 0.8 on top.
- No leading silence (the release and contact are frame-timed; 5 ms of pre-roll is 5 ms late). Trailing
  silence is harmless.
- Exact lowercase names above, dropped in `assets/audio/combat/`. They play on next launch, with no code change.
- **Sources:** original recordings, original synthesis, or a library whose licence covers a commercial
  game. Never lifted, resampled or "inspired by" a specific reference game's sound.

## Acceptance (the ear test, in this order)

1. **True speed, real fight, default volumes, music on** (Verdant Hollow bed): a four- and a five-target
   SPRAY. The release, the silence of the flight, and the contact must be heard as one phrase. The contact
   must be the loudest SPRAY moment.
2. **With the picture:** the contact sound must land on the frame the knives land (it does by construction:
   the same playhead crosses both). Judge whether it FEELS on the hit. A sound that feels early is usually a
   transient that is too soft.
3. **Against the fight:** blind, can a listener tell the SPRAY's contact from the enemy's bite
   (`sfx_hit` at −0.25 octave) and from the basic swing? They must be distinguishable.
4. **Fatigue:** 20 casts in a row on the fast-TEMPO build (the RHYTHM route plus VOLLEY). As measured, that
   build casts SPRAY every ~3 s (3.7 s, then 2.9 s as RHYTHM ramps), with a basic swing every ~0.7 s and a
   HARD HANDS between. Nothing should become grating. The tick in particular must not become a "tic".
5. **Small speakers / laptop:** the contact must still read (the weight layer sits at ~120–190 Hz for this reason).

If a cue fails 1–3, replace it against this brief. Do not retune volumes around a cue that is wrong.
