# BRAND, the final audio review (ADR-011 / ADR-013, 2026-10-02)

**Status: three candidates, awaiting the owner's choice. Nothing is approved and no cue bytes are pinned.** BRAND's
picture and its production renderer are APPROVED and LOCKED. These films change nothing in them: each candidate is heard
over the same frames. The brief is `design/audio/seeker-brand-audio-brief.md`.

## The films (true speed, ~12 s each, the same seven segments, the same crop, the segment's name burned in)

| file | what |
|---|---|
| `01_candidate_A.mp4` | **A SUBTLE / INTERNAL** (recommended, currently in `assets/audio/combat/`) |
| `02_candidate_B.mp4` | B SUPERNATURAL |
| `03_candidate_C.mp4` | C ORGANIC / ASH |
| `04_visual_reference_muted.mp4` | the same picture with no audio track |
| `05_answers.md` | the seven review questions answered per candidate, and the recommendation |
| `06_mix.md` | levels in the fight: every BRAND cue against SPRAY, HARD HANDS, JAWS and PRESS |
| `07_timing.md` | the live timing check, plus each segment's cues on the film's own clock |

Segments (playhead = the fight's clock; seeded fight, seed 7, the Seeker):

| # | segment | take | window (ms) | film time (s) |
|---|---|---|---|---|
| 1 | BRAND apply (the first tick, 2.0 s) | `ba_apply` | 1750-3050 | 0.00-1.30 |
| 2 | ETCH deepen 1 -> 2 (shown 4.38 s) | `ba_deepen12` (WILT in JAWS's slot) | 3800-5150 | 1.30-2.66 |
| 3 | ETCH deepen 2 -> 3 (shown 6.38 s), then the host falls | `ba_deepen23` | 6100-7480 | 2.66-4.04 |
| 4 | SPRAWL: the source's apply, then the row infects | `ba_sprawl` | 1750-3050 | 4.04-5.34 |
| 5 | transfer: leave 6.7 s, the silent gap, awaken 7.52 s | `ba_transfer` | 6450-8185 | 5.34-7.04 |
| 6 | cursed death with no transfer (the wave-ending kill, 17.2 s) | `ba_death` | 16900-18300 | 7.04-8.52 |
| 7 | mixed: SPRAY, PRESS's ticks, a BRAND leave and awaken | `ba_mixed` (BRAND in JAWS's slot) | 4850-8170 | 8.52-11.82 |

How the films were made:

- **The audio comes from the game's trace.** `film_audio.py` places every sound the game asked for (key, volume, duck,
  pan, pitch) on the trace clock. The candidates differ only in the seven BRAND files, which are passed in with
  `--cue sfx_seeker_brand_<key>=tools/asset-pipeline/audio_history/brand_curse/<X>/...`. JAWS, PRESS and every other
  sound are the shipped files, untouched.
- **What this does not reproduce:** the per-play random pitch `vary` and the audio device's voice limit. This is a
  render of the game's mix decisions; the ear test on real hardware is still the final proof.
- **The window edges.** Each window closes after its last BRAND cue's tail, or just before the next BRAND event
  (segment 2 stops before the leave at 5200, segment 5 before the leave at 8200, segment 7 before HARD HANDS at 8200).
- **A HUD lag in segment 1.** The slot bar still reads PRESS / JAWS. This is a known quirk of the capture rig's skill
  swap: the bar is a label only. BRAND is in the build, as the curse on the front enemy and the trace's `mark-cue` rows
  show. Later segments show BRAND in the bar.
- **Verified checks:**
  - Frames were extracted from all four films: each segment's label and picture are correct.
  - A, B and C each carry a stereo AAC track: peak -13.7 / -14.3 / -14.3 dBFS, RMS about -33.7 dBFS, every segment
    non-silent. The A-B and A-C differences are about -49 dB RMS: the BRAND share is the only thing that changes, and
    it is quiet by design.
  - The muted film has no audio stream.

## The exact commands

From a Bash shell at the repository root. Each take is filmed and then rendered before the next one starts. The
render deletes the take's frames, including 3-digit names and the strip sheet. Launched from Python, the game's trace
log comes out EMPTY, so `films_brand.sh` is run from Bash. `films_brand.sh` keeps its disk guard.

```bash
TAG=ba; OUT=production/qa/evidence/brand-audio
for n in 1 2 3 4 5 6 7; do
  take=$(PYTHONUTF8=1 python tools/asset-pipeline/brand_audio_evidence.py $TAG $OUT --take-of $n)
  bash tools/asset-pipeline/films_brand.sh $TAG $take          # <= 120 frames (~70-90 MB), the trace in $take.log
  PYTHONUTF8=1 python tools/asset-pipeline/brand_audio_evidence.py $TAG $OUT --only $n   # muted + A + B + C, frames deleted
done
PYTHONUTF8=1 python tools/asset-pipeline/brand_audio_evidence.py $TAG $OUT --concat-only   # 01-04 + the verify
```

Where things go:

- The four segment renders: `build/tmp/brandaudio/films/seg/`, about 18 MB.
- The trace logs: `build/shots/brand/ba/ba_*.log`, about 0.6 MB.
- The candidate files are built by `PYTHONUTF8=1 python tools/asset-pipeline/make_brand_cues.py [A|B|C]` (deterministic).
