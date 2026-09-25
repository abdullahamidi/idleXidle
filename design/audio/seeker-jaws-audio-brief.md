# Audio brief: the Seeker's JAWS (the reaction's one cue)

> **Status (2026-09-25): CANDIDATE, NOT YET HEARD.** `sfx_seeker_jaws_snap` is built by
> `tools/asset-pipeline/make_action_sfx.py` with the same layered kit as the approved SPRAY and HARD HANDS cues and
> the fight's shipped ones, measured, and rendered into the review films from the trace (`film_audio.py`). A measurement
> says it is short, bright and quiet enough; only an ear says it sounds like an iron trap. It is a candidate until
> the owner has listened to it.

## What the player hears (the phrase)

JAWS is a REACTION: the enemy's bite is its cause, and it answers on the same frame. The ear should read

```
BITE (the enemy's thud) ── the same frame ── CLACK (the jaws)
```

not five sounds piled on one frame (the discovery measured: the bite thud, a cast breath, a reaction thud, the
answer's thud and a puff, all on the bite's frame).

| Moment | Cue | Game volume | Pan | Plays when |
|---|---|---|---|---|
| Contact | the enemy's `sfx_hit` (pitched down, 0.30) | unchanged | centre | the bite: the CAUSE stays |
| Snap | `sfx_seeker_jaws_snap` | 0.46 | ±0.35 × the caught creature's x | the same frame: JAWS' `Skill` event, on the bite's ms |

- **Removed for JAWS** (they described the same event, or a cast it is not): the cast breath `sfx_cast`, the pitched-up
  reaction thud, the reflected blow's generic `sfx_hit` and its puff. The "SNARE" callout is off too (a reaction every
  two or three seconds; the jaws, the number and the dock already say it).
- **Under an action** the snap is not `lead`: SPRAY's or HARD HANDS' duck plays it at 45 %, so their own phrase stays
  on top (filmed: the snap 150 ms before SPRAY's release, ducked to 0.45).
- **No rearm sound.** Readiness is read from the dock.
- The engine adds ±0.04 octave of random pitch per play and the SFX master (0.8).
- Fallback chain: `sfx_seeker_jaws_snap` → `sfx_hit`. The game has no generic trap cue.

## The cue

`sfx_seeker_jaws_snap`, ~180 ms: the spring letting go (a dry, bright mechanical click and a tiny inharmonic tick),
12 ms later the clamp (a short, hard iron clack with a little weight under it and teeth into hide: a tight body, a
metallic knock that dies inside ~40 ms, never a ring), then the chain taking the strain (four small link ticks,
falling, jittered). Measured: peak −9.4 dBFS, RMS −27.0 dB, spectral centroid 2118 Hz, −20 dB after 65 ms. Brighter
and much shorter than the enemy's bite thud (333 Hz, 194 ms), so the answer is told apart from its cause.

## What to listen for (the review)

- Does it read as IRON closing, not a click-track or a sword clang?
- At true speed, BITE → CLACK: the two are one moment, the thud first in the ear.
- Sixteen seconds of repeated triggers (`08a`, `08b`): does it tire?
- Under SPRAY (`05a`): does SPRAY's release and hit phrase stay whole?
