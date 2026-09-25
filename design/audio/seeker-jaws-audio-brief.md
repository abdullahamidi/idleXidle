# Audio brief: the Seeker's JAWS (the reaction's one cue)

> **Status (2026-09-25, readable-clamp pass): the dry-steel MATERIAL is the owner's accepted direction ("better; keep it
> as the baseline"). The clack was RETIMED to the jaws' new stop (17 ms → 51 ms). The retimed cue is not yet heard.**
> - `sfx_seeker_jaws_snap` is built by `tools/asset-pipeline/make_action_sfx.py`, with the same layered kit as the
>   approved SPRAY and HARD HANDS cues and the fight's shipped ones.
> - It is measured, and rendered into the review films from the trace (`film_audio.py`).
> - A measurement says the cue is short, bright, and lands on the shut frame. Only an ear says it sounds like a steel
>   trap and not a bite. It is a candidate until the owner has listened to it.

## What JAWS is, to the ear

A SPRING-LOADED BEAR TRAP, a mechanism: a latch lets go, two steel jaws hit their stops, and a chain takes the strain.
It is never a mouth. The polish-pass cue read as bone because of two layers:

- a saturated noise crunch, heard as teeth or bone;
- a pitch-dropping thump, heard as a skull or a body. 81 % of the cue's energy was under 300 Hz, ringing for ~70 ms.

The identity pass removed both. Think **CLACK**: not CRUNCH, and not CLANGGG.

## What the player hears (the phrase)

JAWS is a REACTION: the enemy's bite is its cause, and it answers on the same frame. The ear should read

```
BITE (the enemy's thud) ── first frame: the latch lets go (a tiny release) ── ~50 ms: CLACK (the jaws hit their stops)
```

The separation is deliberate. In the identity pass the clack sat 17 ms after the bite, on top of it. Now the ear hears
bite → a small release → CLACK, and the ~50 ms gap is the jaws' visible close: the sound helps the eye find it.

It should not be five sounds piled on one frame. The discovery measured five on the bite's frame: the bite thud, a
cast breath, a reaction thud, the answer's thud and a puff.

| Moment | Cue | Game volume | Pan | Plays when |
|---|---|---|---|---|
| Contact | the enemy's `sfx_hit` (pitched down, 0.30) | unchanged | centre | the bite: the CAUSE stays |
| Snap | `sfx_seeker_jaws_snap` | 0.46 | ±0.35 × the caught creature's x | the first frame that shows the bite (JAWS' `Skill` event). Its CLACK sits 51 ms in, on the third frame after it: the one where the rigid jaws hit their stop, and the frame on which the reflected number and flash appear |

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

`sfx_seeker_jaws_snap`, 200 ms, TIMED TO THE PICTURE (the jaws shut at 50 ms, the third 60 fps frame after the bite):

- **0 ms, THE LATCH LETS GO.** A tiny dry pawl tick and a short bright chain rasp, −23 dB under the clack. It is a
  lead-in, never a second hit.
- **50 ms, THE CLACK (the strongest transient, measured at 51.2 ms).** Dry forged steel:
  - a hard broadband contact;
  - the struck steel's own voice: four inharmonic partials around 2.2–6.4 kHz that die inside ~12 ms, with no partial
    outliving 20 ms (a plate hitting its stop: never a sword clang, a bell or an anvil);
  - the second jaw's stop 3.5 ms behind the first, a touch quieter (two jaws, two stops, one CLACK with a mechanical
    edge);
  - a small damped knock of the housing (~610 Hz, gone in ~15 ms) for heft.
- **The contact layer.** Far beneath the clack, ~20 dB under it: a muted, low-passed leather contact that says the
  clamp CAUGHT something. It is never saturated (that was the crunch) and never a thump.
- **After.** The chain takes the strain in three small falling link ticks (83, 107 and 135 ms).

## Measured

| | polish pass (bone) | identity pass | READABLE (this cue) |
|---|---|---|---|
| Strongest transient | 18.8 ms | 17.3 ms (the stop at 16 ms) | **51.2 ms** (the stop at 50 ms) |
| Lead-in (0–10 ms) under the peak | −21.6 dB | −22.8 dB | −22.7 dB |
| Energy under 300 Hz | **80.8 %** (the bone/body thump) | 0.6 % | 1.3 % |
| Energy 300–1200 Hz | 1.1 % | 8.9 % | 11.5 % (the housing's knock, the leather) |
| Energy above 1.2 kHz | 18.1 % | 90.5 % | **87.2 %** (steel) |
| −30 dB after the peak | 72 ms | 24 ms | 26 ms |
| Length | 170 ms | 170 ms | 200 ms |

Waveforms with the shut frame and the peak marked: `production/qa/evidence/jaws-readable/25_snap_waveform_identity_vs_readable.png`.
The cues themselves: `jaws-readable/25a_identity_snap.wav` and `25b_readable_snap.wav`.
The approved SPRAY, HARD HANDS, cast and hit cues are byte-identical (the generator was re-run).

## What to listen for (the review)

- Does it read as a STEEL MECHANISM hitting its stop, with no bone, teeth, chewing or skull?
- Is it SHORT? A clack, not a clang, not a bell.
- If it sounds sterile: the leather contact under it is a dial (its gain, 0.10 in `jaws_snap`).
- At true speed, BITE → CLACK: the two are one moment, with the thud first in the ear.
- Sixteen seconds of repeated triggers with music (`jaws-readable/07b`, and 16 s CURRENT then READABLE with sound: `07d`): does it tire?
- Under SPRAY (`jaws-readable/04b`): does SPRAY's release and hit phrase stay whole?
- In slow motion (`jaws-readable/02d`, the close crop 4x slower with sound): does the clack land on the frame the jaws shut?
- Eyes away: does bite → release → CLACK say "a trap sprang" on its own?
