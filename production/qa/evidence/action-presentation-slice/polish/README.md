# Seeker SPRAY: the art, animation and audio polish pass (2026-09-24)

| Field | Value |
|---|---|
| **Branch** | `fix/vfx-fade` (not merged) |
| **Decision record** | `docs/architecture/ADR-011-authored-actions-contact-on-the-beat.md`: still **Proposed** |
| **Scope** | the Seeker's SPRAY only. HARD HANDS, the other Seeker actions, other projectiles, the VFX backlog and Forge/items were not touched. One crash outside the scope was fixed (below). |
| **Audio status** | **HUMAN-APPROVED** (the owner listened, 2026-09-25). SPRAY is the first gold-standard audiovisual combat action (ADR-011 Accepted). |
| **Films** | `build/shots/slice/films_final.sh` (re-films every view) |
| **Review page** | https://claude.ai/artifact/1nteFcv9TaNyLq4tEwTywN |

## How the films were made

- **Fight.** The HUNT fixture with SPRAY woven in Body (`RH_SHOT_SWAP=volley_spray:volley_spray@Body`), one picture per game step (16.7 ms), trace on (`RH_PRESENT_TRACE=1`).
- **Sound.** The capture rig has no audio device, but the trace logs every sound the game asks for: the cue, volume, pitch, pan and the mix duck. `tools/asset-pipeline/film_audio.py` renders that list with the real WAV files into each MP4.
  - It is a faithful render of the game's mix decisions.
  - It is not a recording of a sound card.
  - The music bed is Umbral Reach's (`music_arena_shadow`) at the game's music level, from an arbitrary loop point.
- **Five targets.** This is a real five-creature wave about 67 s into the same fight. The film starts itself one second before the first SPRAY that strikes five (`RH_SHOT_SEQ=count,stride,@5`). A frame offset could not hold the moment, because crit rolls differ per run and the fight drifts by seconds. `RH_SHOT_SHOWCASE=1` hides the "IT FELL" guidance scrim, which otherwise dims the arena after the hunter's first fall. It hides guidance and toasts only.
- **Fast TEMPO.** The RHYTHM route plus VOLLEY: `RH_SHOT_TAKE=bite,swift,quick,road_sign_2,brisk,rhythm,blitz,volley`.
- **OLD** is the branch before the slice (`build/shots/slice/old/spray1`).

## The views

| File | View |
|---|---|
| `01a_final_true_speed_audio_with_music.mp4` | **The approval view.** True speed, with the SPRAY cues and the fight's music. |
| `01b_final_true_speed_audio_sfx_only.mp4` | The same, with effects sound only (to judge the cues). |
| `01c_final_true_speed_audio_off.mp4` | The same, silent. |
| `01d_old_true_speed_audio_sfx_only.mp4` | The OLD version with its own sound. |
| `01e_old_vs_final_true_speed.mp4` | OLD above, FINAL below, aligned on the fight's playhead. |
| `02_final_slow_motion_4x_audio.mp4` | 4× slower; the sound is slowed with it. |
| `03_animation_only_vfx_off.mp4`, `03b_…sheet.png` | Effects off: the character alone. |
| `04_vfx_only_character_hidden.mp4`, `04b_…sheet.png` | Champion hidden: blades, trails and contacts alone. |
| `05_release_socket_overlay.png` | The hand socket (green) and each blade's first position (yellow), around the release. |
| `06_five_targets_true_speed_audio_with_music.mp4`, `06b_…slow_motion…`, `06c_…contact_sheet.png`, `06d_…release_sockets.png` | A genuine five-target SPRAY. |
| `07_fast_tempo_first_cast_audio.mp4`, `07b_fast_tempo_8s_repeated_casts_audio.mp4`, `07c_fast_tempo_the_recovery_cut.png` | The fast TEMPO build: its first cast every frame, 8 s of casts, and the one-frame cut. |
| `08_*_body_vs_mind.png` | The same frames woven Body and Mind: the steel stays steel, and only the light changes. |
| `09_fatigue_16s_repeated_casts_audio.mp4` | 16.5 s of the plain build (one picture per 10 steps): every cast in it. |
| `10a–d_timeline_*.png`, `10_timelines.md` | The measured timeline as lanes, and as tables. |
| `11_audio_cues_spectrograms.png` | The three new cues and the phrase as scheduled. |
| `12_release_hand_before_after.png` | Frames 2–4 before and after, in colour and as silhouettes. |
| `13_follow_through_recovery_into_idle.png` | The follow-through and recovery into idle, with each frame's duration. |
| `14_press_field_before_after.png` | The pink rectangle (PRESS), before and after. |

## What changed in this pass

1. **The release hand.** Frames 3 and 4 now show an OPEN hand, flicked along the fan, where the approved slice showed a fist.
   - Source: a PixelLab `edit_image_pixen` of each approved key pose (2 generations). Only the hand's box is taken, because the edits also redrew part of the body, painted three knives and redrew the other arm.
   - The edit drew bare skin, so the whole hand is recoloured to his glove (`keyposes.py`). He is gloved in every other frame, idle included.
   - The knives leave from that hand (`05`).
2. **Frame timing** (`90 110 150 50 230 90 90 110` ms, still 920 ms in total).
   - The follow-through now holds until two frames after the contact, so the open hand still points at the pack when the knives land.
   - Before, it ended 67 ms before the contact. A first fix (exactly the flight) dropped the arm on the contact frame itself.
   - The recovery took back the 30 ms.
3. **The release accent.** Only the last 18 % of the hand's path is drawn, lifted over the head, from the release frame, fading over 90 ms. The first smear ran from the coil through the face and read as a beam from the eye.
4. **Audio.** Three new cues: `sfx_seeker_spray_release`, `…_hit` and `…_tick` (`tools/asset-pipeline/make_action_sfx.py`, the same layered kit as the shipped cues).
   - ONE contact sound per fan. A fan of three or more adds at most two quiet ticks, panned to its outermost blades (+18 / +36 ms).
   - Every other sound is ducked to 45 % from the release until 160 ms after the contact.
   - The contact's first candidate RANG: from 80 ms on, over 90 % of it was one pure 2.25 kHz tone, which says "metal on metal". It now has a 30 ms glint.
   - Its thump was cut off at −20 dB (a 15 dB step in 10 ms). It now decays on its own.
   - `design/audio/seeker-spray-audio-brief.md` is the contract for a replacement.
5. **The enemy flash** (still 0.38 strength) now peaks ON the contact frame and is gone in 130 ms. Before, the fight's usual swell put the pack's peak 40 ms after the knives and kept it grey for 200 ms.
6. **Fast TEMPO.** A plain clip whose blow has landed now gives the figure to a performed cast at the last moment the cast can still wind up.
   - Before, SPRAY on the fast build started AT its release: no wind-up, and the knives crossed the arena in ~100 ms.
   - Now every cast winds up (from −483 ms) and flies 250 ms.
7. **The pink rectangle** was `fx_press`: a white weight slab that the field aura tinted pink and held behind him. PRESS is now line art (`tools/asset-pipeline/v2/press_field.py`): a ring, an arrow and chevrons pressing onto a bar. The motion is deterministic. Edge check: edge 0.
8. **A crash (outside the scope, fixed).** The fall loop's build snapshot added string hash codes with `Enumerable.Sum`, which is checked. String hashes are random per process, so the hunter's first fall threw an `OverflowException` in a large share of processes (about a quarter with two woven skills). It has been in the game since 0.1.0-alpha. `Game1.WrappingSum` fixes it, and a regression test covers it. The released alpha still has it.

## The timeline (FINAL, measured; t = 0 is the fight's beat)

Everything on the beat is presented on the first 60 fps frame after it (+0 or +17 ms), together.

| moment | 4 targets | 5 targets | fast TEMPO |
|---|---:|---:|---:|
| clip start (ready) | −583 | −600 | −483 (after a cut) |
| anticipation | −500 | −500 | −433 |
| extreme hold (150 ms) | −383 | −400 | −383 |
| **release**: pose, sound, blades on one frame | **−233** | **−250** | **−233** |
| **contact**: knives, health, flash, numbers, contact sound | **+17** | **+0** | **+17** |
| ticks (outer blades) | +33, +50 | +33, +50 | +33, +50 |
| follow-through ends | +33 | +33 | +33 |
| idle restarts | +333 | +333 | +333 |

- The flight is 250 ms in every case.
- No generic hit puff and no generic hit sound play for SPRAY's hits.
- The only other sound inside the phrase in the four-target film is an enemy's bite at −183 ms, ducked to 45 %.
- OLD (the audit, `../../action-presentation-audit/`): every piece of hit feedback came 683 ms BEFORE the knife arrived.

## Cost (re-measured)

| | Sprites | Effects-pass draw calls (upper bound) |
|---|---:|---:|
| 4 knives | ≤ 60 | ≤ 39 |
| 5 knives | ≤ 85 | ≤ 50 |

These are unchanged from the approved slice for four knives, and far inside the "low hundreds" budget. Nothing was optimised.

## What is still weak

- **The audio has not been heard.** The only proof of a sound is a listening test, and it has not happened. See the brief.
- **Fast TEMPO snaps once.** At the recovery cut, the figure goes from the swing's low lunge to the upright ready pose in one frame (`07c`). It replaces a missing wind-up, but it is visible.
- **The hand is an edit.** At combat size it reads as an open gloved hand. At 3× the seam where the edit meets the pose shows.
- **PRESS awaits your look.** It is line art now, and it pulses behind him in the Source colour.
- **Pre-existing, not touched:** `tools/check_boot.sh` stops at its `pre-v8` snapshot check. The save version became 9 in 8314b03 (2026-09-21), so the snapshot is now `pre-v9`. Its three boot lanes and the v4 seeding lane pass.
