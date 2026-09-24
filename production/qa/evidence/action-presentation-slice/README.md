# Seeker SPRAY gold-standard slice (2026-09-24)

| Field | Value |
|---|---|
| **Branch** | `fix/vfx-fade` (not merged) |
| **Decision record** | `docs/architecture/ADR-011-authored-actions-contact-on-the-beat.md` (Proposed) |
| **Scope** | the Seeker's SPRAY only |
| **Status** | awaiting the owner's verdict; no other action was touched |
| **Review page** | https://claude.ai/artifact/N6pqMSkQ97zwJLdQt6nrvM |

## How it was filmed

- **Fight.** The HUNT fixture, with the Seeker's SPRAY cast in Body (`RH_SHOT_SWAP=volley_spray:volley_spray@Body`). It hits all **four** enemies of the wave.
- **Frames.** One picture per game step (16.7 ms), with the trace on (`RH_PRESENT_TRACE=1`).
- **OLD** is the branch just before this slice. **NEW** is this slice.
- **t = 0** is the fight's cast event:
  - OLD: the moment everything happened at once;
  - NEW: the contact.

| File | View |
|---|---|
| `01_true_speed_old_vs_new.gif` | **The approval view.** Old on top, new below, true speed. |
| `02_slow_motion_new.gif` | Every frame, 4× slower, for continuity. |
| `03_frame_sheet_new.png`, `03b_frame_sheet_old.png` | Key moments with their markers. |
| `04_vfx_off.gif`, `04b_vfx_off_sheet.png` | Effects off: the character alone. |
| `05_character_hidden.gif`, `05b_character_hidden_sheet.png` | Champion hidden: the force path alone. |
| `06_release_socket_overlay.png` | The hand socket (green) and each blade's first position (yellow). |
| `08_contact_every_frame.png` | Every frame from −50 to +183 ms at the targets. |
| `09_repeated_casts.png`, `09b_second_cast_true_speed.gif` | Three casts in 16 s of fight; the second at true speed. |
| `10_key_poses_silhouette_and_colour.png` | The eight frames as silhouettes (with the knife bundle) and in colour, with their durations. |
| `11_pixellab_passes.png` | The rejected and the accepted key-pose pass. |

## The contact timeline (NEW, measured)

| t (ms) | Channel | Event |
|---|---|---|
| −600 | animation | anticipation begins (f0: the hand goes to the belt) |
| −517 | animation | f1: the knife bundle drawn and cocked by the shoulder |
| −400 | animation | f2: extreme anticipation, held 150 ms |
| **−250** | animation | **release** (f3), 50 ms |
| **−250** | projectile | 4 blades launch from the hand socket (one per struck enemy) |
| **−250** | UI | callout VOLLEY |
| **−250** | sound | release cue |
| −200 | animation | f4 follow-through; the cloak flares after the arm |
| −83 | animation | f5 overshoot |
| **0** | projectile | all 4 blades contact their enemies, on one frame |
| **0** | fight | health presented (the fight's own event) |
| **0** | enemy | 4 target flashes (at 0.38 strength) |
| **0** | UI | 4 damage numbers |
| **0** | effects | impact effects (contact slash, slivers, flash) |
| **0** | sound | contact cue |
| +50 | animation | f6 recovery begins |
| +200 | animation | f7 = the idle's first pose (settle) |
| +317 | animation | recovery ends; the idle restarts from its frame 0 |

Every piece of hit feedback falls on the same frame as the knives' contact. The OLD version put all of it
683 ms **before** the knife arrived.

## Audio cues (NEW)

| Cue | t (ms) | Plays | Volume | Pitch | Pan | Resolution |
|---|---|---|---|---|---|---|
| release | −250 | `sfx_cast` | 0.24 | +0.45 | −0.12 | `sfx_seeker_spray_release` → `sfx_throw_release` → `sfx_cast` |
| contact | 0 | `sfx_hit` | 0.36 | +0.18 | +0.12 | `sfx_seeker_spray_hit` → `sfx_blade_hit` → `sfx_hit` (once for the fan) |
| bite (unrelated) | −200 | `sfx_hit` | 0.30 | −0.25 | 0 | unchanged |

There is no `sfx_cast` at the beat any more, and no generic `sfx_hit` per struck enemy. **No bespoke
throw or blade sound exists yet**; the chain falls back to the existing cues, tuned. A file with the
specific name, added to `assets/audio`, replaces each one with no code change.

## Object scale

The knife in the hand and the knife in the air are the **same texture drawn at the same scale**:

| | Measured |
|---|---|
| Hand knife : flying knife | **1 : 1** |
| Length on screen | 30 source px × 3 × the hunter's draw scale (~1.10) = **~99 px**, 0.24 of his 412 px visible height |
| Before this slice | 55 px in the hand, 280 px in the air |

## PixelLab

6 generations (4,352 → 4,346), all through `animate_with_skeleton_v3`:

| Job | Verdict | Why |
|---|---|---|
| `f66bc55f` | **rejected** | The generator's own background key removed the dark cloak (fragments in ready, anticipation and return) and left a stray blob on the release |
| `e7d49c06` | **accepted** | Opaque ground, keyed by `keyposes.py`. Frames 0–6 are used; frame 7 is the original idle frame 0 |

The knife was drawn pixel by pixel (`seeker_knife.py`), not generated.

## Cost

| Measure | Value |
|---|---|
| Sprites (four-knife fan) | ≤ 60 |
| Effects-pass draw calls | 16–17 → ≤ 39 at the contact frames |
| Extra batch | one extra Begin/End (the light pass) while a throw is live |
| Clip timing file | loaded once |

Batching per layer across the blades would cut the draw calls; it is not needed yet.

## What remains weak (reported, not hidden)

- **Audio is the weakest layer.** The timing and the chain are right, but the sounds are tuned generic cues, not a blade.
- **The release hand is a fist.** A real release would show an open hand. At combat size the blades leaving it carry the read, but it is the weakest pose of the eight.
- **The held field behind the Seeker still draws a hard-edged pink rectangle** (the old press strip). This is outside this slice.
- **Not measured here:**
  - a five-enemy wave (the fixture wave has four);
  - the fastest TEMPO builds. The clip compresses its elastic frames, and a unit test covers a late start.
