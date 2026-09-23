# Action presentation audit — the Seeker's thrown knife (SPRAY) and HARD HANDS (2026-09-23)

| Field | Value |
|---|---|
| **Branch** | `fix/vfx-fade` (not merged) |
| **Status** | discovery only; the implementation waits for the owner's approval |
| **Review page** | https://claude.ai/artifact/8yYNtYgjHAiDMPPw3XVpgT |

## How it was measured

- **Trace.** `RH_PRESENT_TRACE=1`, new and diagnostic only (`src/IdleXIdle.Game/PresentTrace.cs`), writes one line
  for every:
  - champion clip start, frame drawn and clip end;
  - fight event crossed;
  - effect spawned;
  - flash and damage number;
  - sound requested;
  - projectile position each frame and the projectile's contact;
  - capture picture.

  Each line carries the hunt screen's clock and the replay playhead.
- **Films.** The HUNT fixture, with the SPRAY pose `RH_SHOT_SWAP=volley_spray:volley_spray@Body` and `RH_SHOT_HUNTER=seeker`:
  - `RH_SHOT_T=3.1`, `capture_seq fight 99 2` for SPRAY;
  - `RH_SHOT_T=6.1` for HARD HANDS.
- **Clock.** t = 0 is the frame on which the Core cast event is crossed.

| File | What it is |
|---|---|
| `spray.trace.tsv`, `hard_hands.trace.tsv` | the raw trace, filtered to presentation lines |
| `03_spray_lanes.svg`, `04_hard_hands_lanes.svg` | millisecond lane charts |
| `01_spray_frames_ms.png`, `02_hard_hands_frames_ms.png` | frames stamped with ms and what happened on them |
| `05_throw_clip_frames.png`, `06_strike_clip_frames.png`, `07_other_clips_props.png` | the authored clips |

## SPRAY, as measured (rate 1, beat 1,500 ms)

| t (ms) | Event |
|---|---|
| −800 | `projectile` clip commits, frame 0. Speed 0.798 = 157 ms per frame, contact lead 796 ms |
| −633 / −483 / −317 / −167 | frames 1–4 |
| −200 | unrelated creature bite: `fx_hit` on the champion + `sfx_hit` (pitch −0.25) |
| 0 | Core `Skill` + 4 `Strike`. On the same frame: clip frame 5, callout VOLLEY, `vfx-spawn fx_seeker_projectile` from the champion's centre + 0.42 w, `sfx_cast`, `sfx_hit` × 4 requested, 4 numbers, 4 flashes, `fx_weakhit` on creatures 1 and 3 |
| +150 / +300 | frames 6, 7; the knife is still in the rear hand |
| +617 | `clip-end projectile` → idle frame 4 (hard cut) |
| +683 | `proj-contact` on creature 0 (x 1194); no flash, number or sound |
| +800 | next bite on the champion |

**Knife speed:** 1,079 px/s at +33 ms → 884 → 658 → 474 → 358 px/s at contact.

**Knife size:**

| | On-screen length |
|---|---|
| Knife in the hand (throw clip) | ~50 px in a 374 px character, about 55 px on screen |
| Flying knife (composite head) | 280 px |

## HARD HANDS, as measured

| t (ms) | Event |
|---|---|
| −800 | `strike` clip, frame 0 |
| −200 | a bite on the champion during the wind-up |
| 0 | frame 5, callout HAMMER, `fx_seeker_strike` on creature 0 (about 450 px from the champion, which does not move), `sfx_cast` + `sfx_hit`, number, flash, `fx_weakhit`, `EnemyDown` → death clip + `sfx_enemy_down`, `fx_death` (delayed 0.45 s) |
| +617 | snap to idle |

## Problems

The review page lists all of them with evidence. In short:

- **Target feedback plays before the knife arrives.** It fires at release, 683 ms before contact.
- **Nothing happens at contact.** There is no feedback and no sound there.
- **SPRAY hits 4 enemies but throws 1 knife.**
- **The knife never leaves the hand.** The clip punches with the empty hand.
- **The projectile appears from nowhere**, and two knives are on screen for 617 ms.
- **The projectile is about 5× the hand knife.**
- **It arrives at a third of its speed**, from the ease-out curve.
- **Every clip uses a fixed 5-of-8 contact frame** with even frame lengths.
- **The clip ends before the contact**, with a hard cut to idle.
- **Generic hit feedback stacks on top of the skill's own impact.**
- **An unrelated bite burst lands on the throwing arm.**
- **Presentation runs on five clocks.**
- **Audio uses three generic cues and never pans.**
- **The whole knife is tinted by its Source.**
- **The prop changes between clips.**
- **HARD HANDS lands from a distance.**
- **The held field draws a rectangle.**
