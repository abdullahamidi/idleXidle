# JAWS concept study: evidence (2026-09-25)

**A concept / reference / animatic study only. No production code, asset, sound or Core change.**
- JAWS stays NOT accepted.
- The upright bear trap (readable-clamp pass, `2f5486a`) is still what the game draws. This study does not replace it.
- It asks whether JAWS wants a literal trap at all, or a stylised reaction manifestation.

**How it was made.**
- **Plates.** The seeded fight (`RH_SHOT_SEED=7`) was filmed with the reaction layer OFF (`RH_REACTION_RECIPES=0`).
  The fight is identical either way. The reflected number and the flash sit on the bite's own frame, and no JAWS is
  drawn. Filmed by `prototypes/jaws-concepts/film_plates.sh`.
- **Plate clean-up.** The legacy row ring was moved out of the BUILD OUTPUT for the takes and has been put back,
  byte-identical. Nothing in the repo changed.
- **Concepts.** Three rough concepts were drawn over those frames by `prototypes/jaws-concepts/animatic.py`: monochrome
  shapes plus one Shadow glow, at the size the game would show them.
- **Timing.** t = 0 is the frame that first shows the bite. A concept may begin before it: the replay knows the bite
  ~900 ms early.
- **Media.** Built by `prototypes/jaws-concepts/build_evidence.py`.

**Sound.**
- The TEMP cue is the approved dry-steel `sfx_seeker_jaws_snap`, used as a reference only. Its clack is timed onto
  each concept's SLAM frame.
- The legacy JAWS cues are removed: the cast breath and the two reflected thuds.
- The enemy's bite thud and the critical cue stay.
- Muted films come first. Judge the motion before the sound.

**Common to every concept, and NOT part of any concept:**
- the fight's own white hit flash on the answered creature (+17..+170 ms);
- the pale hit puff;
- the red spark and ring on the Seeker (the enemy's bite landing);
- the reflected number "-7 JAWS CRITICAL".

Owner films crop off the legacy "SNARE" callout (y < 410), which belongs to the old presentation.

| File | What |
|---|---|
| `01_true_speed_grid_MUTED.mp4` | TRUE SPEED, muted: no JAWS / A / B / C, the same frames of the same fight (normal TEMPO, the bite at 7000) |
| `01A` / `01B` / `01C_true_speed_MUTED.mp4` | each concept alone, the whole stage, muted |
| `02A` / `02B` / `02C_true_speed_temp_sound.mp4` | the same with the temp cue; `02N` is the fight with no JAWS, for its sound |
| `03_close_grid_slow_4x_MUTED.mp4`, `03A/B/C_close_slow_4x.mp4` | 4x SLOW, the belt-to-target close view, 1:1 |
| `04A/B/C_frame_sheet.png` | every frame from -67 to +183 ms, 1:1 and 2x, each named by its key pose |
| `05_repeated_loop_grid_MUTED.mp4`, `05A/B/C_repeated_loop_temp_sound_music.mp4` | REPEATED TRIGGER: about 4.8 s of fast TEMPO with JAWS at 1000 / 3000 / 5000, three consecutive takes joined by playhead (one 150 ms cut at 4050 → 4200), with the arena music |
| `06_during_spray_grid_MUTED.mp4`, `06A/B/C_during_spray_temp_sound.mp4` | JAWS at 1000 inside SPRAY's wind-up (knives out at 1150, landing at 1400) |
| `07_during_hard_hands_grid_MUTED.mp4`, `07A/B/C_during_hard_hands_temp_sound.mp4` | JAWS at 13000, the HARD HANDS leap at 13083, its hit at 13200 |
| `08_blind_checks.md`, `08_blind_clip_*.png` | the blind reads: 7 fresh readers, one strip each; the key is in the record |

**Reproduce.** The plates are gitignored build output. Build the game once, then run the three steps below.
Re-filming shifts frame-to-playhead by the startup jitter, so re-render after every re-film.

```
bash prototypes/jaws-concepts/film_plates.sh        # the plates (moves no repo file)
bash prototypes/jaws-concepts/render_concepts.sh    # A/B/C over every plate
PYTHONUTF8=1 python prototypes/jaws-concepts/build_evidence.py production/qa/evidence/jaws-concepts <blind dir>
```
