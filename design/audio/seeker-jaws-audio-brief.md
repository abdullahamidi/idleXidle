# Audio brief: the Seeker's JAWS (the reaction's one cue)

> **Status (2026-09-25, identity pass): CANDIDATE, NOT YET HEARD.**
> - `sfx_seeker_jaws_snap` is built by `tools/asset-pipeline/make_action_sfx.py`, with the same layered kit as the
>   approved SPRAY and HARD HANDS cues and the fight's shipped ones.
> - It is measured, and rendered into the review films from the trace (`film_audio.py`).
> - A measurement says the cue is short, bright, and lands on the shut frame. Only an ear says it sounds like a steel
>   trap and not a bite. It is a candidate until the owner has listened to it.

## What JAWS is, to the ear

A SPRING-LOADED HUNTING CLAMP, a mechanism: a latch lets go, two steel arms hit their stops, and a chain takes the
strain. It is never a mouth. The polish-pass cue read as bone because of two layers:

- a saturated noise crunch, heard as teeth or bone;
- a pitch-dropping thump, heard as a skull or a body. 81 % of the cue's energy was under 300 Hz, ringing for ~70 ms.

The identity pass removed both. Think **CLACK**: not CRUNCH, and not CLANGGG.

## What the player hears (the phrase)

JAWS is a REACTION: the enemy's bite is its cause, and it answers on the same frame. The ear should read

```
BITE (the enemy's thud) ── first frame: the latch lets go (a tiny lead-in) ── ~17 ms: CLACK (the arms hit their stops)
```

It should not be five sounds piled on one frame. The discovery measured five on the bite's frame: the bite thud, a
cast breath, a reaction thud, the answer's thud and a puff.

| Moment | Cue | Game volume | Pan | Plays when |
|---|---|---|---|---|
| Contact | the enemy's `sfx_hit` (pitched down, 0.30) | unchanged | centre | the bite: the CAUSE stays |
| Snap | `sfx_seeker_jaws_snap` | 0.46 | ±0.35 × the caught creature's x | the first frame that shows the bite (JAWS' `Skill` event). Its CLACK sits 17.3 ms in, on the next frame, the one where the rigid arms hit their stop |

- **Removed for JAWS**, because they described the same event or a cast it is not:
  - the cast breath `sfx_cast`;
  - the pitched-up reaction thud;
  - the reflected blow's generic `sfx_hit` and its puff;
  - the "SNARE" callout.
- **Under an action** the snap is not `lead`: SPRAY's or HARD HANDS' duck plays it at 45 %, so their own phrase stays on
  top.
- **No rearm sound.** Readiness is read from the dock.
- The engine adds ±0.04 octave of random pitch per play, and the SFX master (0.8).
- Fallback chain: `sfx_seeker_jaws_snap` → `sfx_hit`. The game has no generic trap cue.

## The cue

`sfx_seeker_jaws_snap`, 170 ms, TIMED TO THE PICTURE (the arms shut on the second 60 fps frame, at 16 ms):

- **0 ms, THE LATCH LETS GO.** A tiny dry pawl tick and a short bright chain rasp, −23 dB under the clack. It is a
  lead-in, never a second hit.
- **17 ms, THE CLACK (the strongest transient, measured at 17.3 ms).** Dry forged steel:
  - a hard broadband contact;
  - the struck steel's own voice: four inharmonic partials around 2.2–6.4 kHz that die inside ~12 ms, with no partial
    outliving 20 ms (a plate hitting its stop: never a sword clang, a bell or an anvil);
  - the second arm's stop 3.5 ms behind the first, a touch quieter (two arms, two stops, one CLACK with a mechanical
    edge);
  - a small damped knock of the housing (~610 Hz, gone in ~15 ms) for heft.
- **The contact layer.** Far beneath the clack, ~20 dB under it: a muted, low-passed leather contact that says the
  clamp CAUGHT something. It is never saturated (that was the crunch) and never a thump.
- **After.** The chain takes the strain in three small falling link ticks (50, 74 and 102 ms).

## Measured

| | CURRENT (polish pass) | IDENTITY (this cue) |
|---|---|---|
| Strongest transient | 18.8 ms | **17.3 ms** (the arms reach their stop at 16 ms) |
| Lead-in (0–10 ms) under the peak | −21.6 dB | −22.8 dB |
| Energy under 300 Hz | **80.8 %** (the bone/body thump) | 0.6 % |
| Energy 300–1200 Hz | 1.1 % | 8.9 % (the housing's knock, the leather) |
| Energy above 1.2 kHz | 18.1 % | **90.5 %** (steel) |
| Spectral centroid | 1960 Hz | 3017 Hz |
| −20 dB after the peak | 65 ms | 34 ms |
| −30 dB after the peak | 72 ms | 24 ms |
| Peak / RMS | −9.4 / −27.5 dBFS | −9.4 / −31.9 dBFS |

Waveforms with the shut frame and the peak marked: `production/qa/evidence/jaws-identity/25_snap_waveform_current_vs_identity.png`. The two cues themselves: `25a_current_snap.wav` and `25b_identity_snap.wav`.
The approved SPRAY, HARD HANDS, cast and hit cues are byte-identical (the generator was re-run).

## What to listen for (the review)

- Does it read as a STEEL MECHANISM hitting its stop, with no bone, teeth, chewing or skull?
- Is it SHORT? A clack, not a clang, not a bell.
- If it sounds sterile: the leather contact under it is a dial (its gain, 0.10 in `jaws_snap`).
- At true speed, BITE → CLACK: the two are one moment, with the thud first in the ear.
- Sixteen seconds of repeated triggers with music (`jaws-identity/07b`, and 16 s CURRENT then IDENTITY with sound: `07d`): does it tire?
- Under SPRAY (`jaws-identity/04b`): does SPRAY's release and hit phrase stay whole?
- In slow motion (`jaws-identity/02d`, the close crop 4x slower with sound): does the clack land on the frame the arms shut?
