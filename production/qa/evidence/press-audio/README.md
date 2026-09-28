# PRESS, the final audio (ADR-011, 2026-09-28/29)

Review page: https://claude.ai/artifact/13QvWjjw4rQvxSMgyjh2c1

**The brief** (the owner): the PRESS picture is APPROVED (commit `fab25919`, the field-propagation version) and is not
reopened. Complete PRESS with its audio. Its gameplay meaning is PRESSURE ARRIVES -> TARGET COMPRESSED -> DEFENCE BREAKS,
and the cue says exactly that: ONE composite tick cue of three layers (a pressure release, a compression THUMP, a
material-neutral defence CRACK a few ms after it), synced to the crush, never on the launch, no travel audio, under
SPRAY's contact, HARD HANDS' impact and JAWS' bite, not fatiguing on repeated ticks. Up to three candidates; the owner
approves one by ear. Stop after the audio review.

## The cue

`sfx_seeker_press_tick` (~190 ms file, audible ~120-135 ms), built by `tools/asset-pipeline/make_press_tick.py` from
REAL recorded foley (CC0 1.0, the licence checked on each sound's own page; `tools/asset-pipeline/foley/press_tick/SOURCES.md`);
only a small sub under the pressure is synthesised.

| phase | ms in the file | what | sources |
|---|---|---|---|
| pressure release | 0-35 | a restrained low air pulse, noise-like (air, not a tone) | a canvas snap's air gust, the air a pillow or a stack of paper squeezes out, a small sub |
| compression thump | 40 | the strongest layer: short, heavy, dry, low-mid | padded cushion hits, a book slammed flat, a flour sack's squeezed give |
| defence crack | +6..+13 | brittle, material-neutral, short | a ceramic tile, a walnut shell, a stone break, a brittle ice-tray tick |
| decay | to ~130 | a very short fragment decay; no reverb tail | |

Three candidates, differing in STRUCTURE, mastered to the same loudness:

| | pressure (vs the loudest 50 ms) | crack after the thump | crack (vs the loudest 50 ms) | character |
|---|---|---|---|---|
| **A balanced** (recommended) | -14 dB | +8 ms | -7 dB | pressure, thump and tile crack in proportion; the thump leads |
| B heavier pressure | -11 dB | +13 ms | -7 dB | an earlier, heavier air push and a squeezed sack; the crack later, a shell giving way |
| C clearer defence break | -14 dB | +6 ms | -5 dB | the clearest break: a brittle tick on a stone fracture and a tile, short |

## In the game

`FieldRecipe.TickCues` (`sfx_seeker_press_tick`), `TickVolume` 0.28, started `CueStartMs` = -20 ms before the tick so the
file's thump (`CueThumpMs` 40) lands at ~+20..+37 ms (the first 60 Hz frame past the moment): between the fold frame and
the shut arcs, the crack on the shut frame. Asked once per tick (`FieldPerformance.CueDue`; a rewind re-arms it, a seek
more than 30 ms past skips it). Never lead: an authored action's duck applies; x0.6 on a quiet tick (an action's contact
close by, or the champion performing one: the picture's own rule; the duck alone if it already applies) and x0.5 on a
tick that gives way to JAWS. A tick that ends the wave now plays its crush out on the break's clock (it froze on its
arrival pose while its cue sounded).

## The mix (`08_mix_in_the_fight.md`)

### Each voice alone, as the game plays it

The loudest 50 ms (dBFS) of each cue file x its play volume x the SFX master (0.8), raw and K-weighted (ITU-R
BS.1770: how loud it is heard; the raw figure under-reads bright cues and over-reads sub-heavy ones).

| voice | volume | raw | K-weighted |
|---|---|---|---|
| PRESS A balanced | 0.28 | -31.5 dB | -31.3 dB |
| PRESS B heavier pressure | 0.28 | -31.5 dB | -31.5 dB |
| PRESS C clearer defence break | 0.28 | -31.5 dB | -31.1 dB |
| SPRAY contact | 0.5 | -27.3 dB | -27.5 dB |
| HARD HANDS contact | 0.55 | -21.8 dB | -23.5 dB |
| JAWS bite | 0.42 | -25.3 dB | -26.0 dB |
| the pack's own bite on the champion (a generic hit, on every PRESS tick in this seed) | 0.3 | -26.0 dB | -28.9 dB |

### In the fight (film_audio's render of the game's sound asks, recommended cue)

PRESS's OWN level is the difference between the soundtrack with its cue and with it silenced; 'the moment' is
the whole mix at that instant (PRESS with whatever else sounds). Raw loudest 50 ms, dBFS.

| take | moment | voice | alone | the moment |
|---|---|---|---|---|
| normal | 6000 | PRESS tick | -31.8 dB | -26.1 dB |
| repeated | 8000 | PRESS QUIET tick (an action's contact close by: x0.6) | -36.2 dB | -26.5 dB |
| fast | 1400 | SPRAY contact | -27.2 dB | -27.2 dB |
| fast | 2000 | PRESS tick (600 ms after SPRAY) | -31.8 dB | -25.9 dB |
| fast | 3600 | HARD HANDS contact | -21.0 dB | -21.0 dB |
| fast | 4000 | PRESS QUIET tick (400 ms after HARD HANDS, the champion still performing it: x0.6) | -36.2 dB | -26.1 dB |
| yield | 10000 | PRESS tick giving way to JAWS (x0.5) | -38.1 dB | -27.1 dB |
| yield | 10333 | JAWS bite | -26.0 dB | -26.0 dB |

PRESS A sits 3.8 / 7.8 / 5.2 dB under SPRAY's contact / HARD HANDS' impact / JAWS' bite, K-weighted. 'The moment'
is loud because the pack's bite (`sfx_hit`) lands on every PRESS tick in this seed.

## Checked

Nobody here can listen, so the cues were MEASURED by independent QA lenses, twice.

- **First round** (design, mix, sync): all three recommend A. Found and fixed: A only 1.6 dB under SPRAY (now `TickVolume`
  0.28 and a crack bus of 0.50: 3.8 dB); a wave-ending tick froze on its arrival pose while its cue sounded (now
  `FieldPerformance.Crushing` holds the break); the films drifted up to ~60 ms against the sound (now on the trace
  clock); a quiet tick under an action's duck took both (now the duck alone); a late window of 80 ms put a seek's thump
  past the hold (now 30); B's pressure was a ~77 Hz tone (sub halved, the air cushion banded from 90 Hz); C's stone
  crumbled (banded, shorter); the walnut rang at 11 kHz (notched).
- **Re-check** (design-mix, sync): both recommend A; everything above resolved. Two must-fixes, both fixed here: the
  films were still off the clock (ffmpeg's concat listing rounds durations to a 1/25 s timebase and dropped half the
  frames, the fold and shut among them; now `option framerate 1000` per entry, every fold and shut frame shown in p26)
  and the cue ignored the picture's own quiet rule while the champion performs (2 of 18 asks at the full 0.28; now
  `CueVolume(tick, ducked, performing)`, and the tick 400 ms after HARD HANDS plays at 0.17).
- **Sync on the trace clock** (every tick of the four takes): the cue asked at -16.7 ms, the thump at +23.3 (between the
  fold frame, +16.7, and the arcs shut, +33.3), A's crack at +31.3. Nothing on the launch or the travel.
- **Why A**: its crack is the most focused (most of the crack window at 2-6 kHz), and the crack is what tells PRESS from
  the pack's bite fused with the thump on every tick; its thump keeps the best margin over that bite (+10.7 / +6.2 /
  +4.6 dB on a full / quiet / yield tick; B +9.5 / +5.1 / +3.5, C +7.8 / +3.3 / +1.7); its pressure is the least tonal
  (periodicity 0.03; B 0.31, C 0.14).
- **Open**: the pack-bite fusion (an option, not built: duck the enemy strike's thud when it shares the tick's
  millisecond); B's residual ~86 Hz periodicity; C's crack only ~2 dB under its thump; a quieter cue at an upgraded 0.7 s
  cadence (not built); a yield tick under a duck stacks both (x0.063, a deliberate give-way); device and display latency
  are not modelled.
- Tests: 819 game + 1888 Core; every asset gate green.

**Awaiting the owner's ear.** Once a cue is approved: PRESS is ACCEPTED as the FIELD / AURA gold-standard presentation,
the approved cue is recorded as human-approved and its bytes pinned by SHA-256 in `press_field_test.cs`, and the visual
contract of `fab25919` is kept.

## Files

| file | what |
|---|---|
| `01_{A,B,C}_true_speed_sound.mp4` | PRESS at true speed with each candidate |
| `02_muted.mp4` | the same, muted |
| `03_{A,B,C}_cue_alone.wav`, `03_cue_layers_A_B_C.png` | the cue alone; its waveform and spectrogram with the layers marked |
| `03_as_played_A_B_C_spray_hard_hands_jaws.wav` | A, B, C, then SPRAY's contact, HARD HANDS' impact and JAWS' bite, 1.2 s apart, each at the level the game plays it |
| `04_{A,B,C}_repeated_ticks_sound.mp4` | repeated ticks in the fight (a normal one, a quiet one, one giving way to JAWS) |
| `04_{A,B,C}_alone_every_2s.wav`, `04_{A,B,C}_alone_every_0.7s.wav` | the cue alone every 2 s and at an upgraded 0.7 s (the game's small pitch variation) |
| `05_with_spray_sound.mp4`, `06_with_hard_hands_sound.mp4`, `07_with_jaws_sound.mp4` | PRESS beside the other three (cue A) |
| `08_mix_in_the_fight.md` | the levels: each voice alone as played (raw and K-weighted) and in the fight |
| `09_trace_sound.md` | the normal take's sound asks and PRESS's cue on the playhead |

The films are timed on the trace clock (`film_audio.py` now lays each captured frame for its real duration, with a
millisecond timebase, and resamples to 60 fps; it used the shots' mean rate and drifted up to ~60 ms against the sound).
Films: `bash tools/asset-pipeline/films_press.sh p26 normal repeated fast yield`; cues: `PYTHONUTF8=1 python tools/asset-pipeline/make_press_tick.py`; this folder:
`PYTHONUTF8=1 python tools/asset-pipeline/press_audio_evidence.py p26 production/qa/evidence/press-audio`.
