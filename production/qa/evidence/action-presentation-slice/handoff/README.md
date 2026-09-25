# The action handoff: no more one-frame snap into SPRAY (2026-09-25)

| Field | Value |
|---|---|
| **Branch** | `fix/vfx-fade` (not merged) |
| **Decision record** | `docs/architecture/ADR-011-authored-actions-contact-on-the-beat.md` (Accepted 2026-09-25: SPRAY is the reference) |
| **Films** | `build/shots/slice/films_handoff.sh` (AFTER); the BEFORE films are the polish pass's (`build/shots/slice/handoff/before/`) |
| **Evidence builder** | this folder was assembled from those films and their `RH_PRESENT_TRACE` logs |

## The fault, and its root cause

On a fast TEMPO build the Seeker jumped in one frame from the basic swing's low lunge to SPRAY's upright ready pose.

- **Every committed clip played to its natural end**, including its settle hold (up to 300 ms on the exit pose). At a fast TEMPO the actions come faster than clip + settle, so every clip started late and caught up by playing faster.
- **The swing before SPRAY therefore started late.** Its visual contact landed ~140 ms after its beat, and it was still in frame 6, its low lunge (the follow-through), when SPRAY had to start.
- **The rule added to save SPRAY's wind-up** ("a recovering clip gives the figure to a performed cast at its latest start") cut the swing right there. Frame 7, the swing's exit pose, was never shown: lunge → ready pose in one frame.

## The fix: a generic handoff (no Seeker case, no new art)

| Piece | What it does |
|---|---|
| `ActionClipTiming.Plain` | Every plain strip now carries its phases. Contact 5, follow-through 6, **recovery + exit pose 7**, and the settle held on 7. The pictures are identical to the old continuous clock. |
| The reservation (`HuntScreen.Reservation`) | From the replay's next action: its **ideal** start (whole wind-up) and its **latest** start (tightest fit its own timing allows). |
| `ActionHandoff.Plan` | Once the outgoing blow has landed, it picks the exit time. The settle gives way first, then the recovery's pace. The readable floor is 2 display frames per pose and at most 3× faster. The exit is never before the recovery starts, so protected phases are never shortened. |
| `ActionClipTiming.FitRecovery` | Retimes ONLY the frames from the recovery marker. The exit pose is always shown, and the first recovery pose is kept before any intermediate. |
| The yield (`HuntScreen.NextAnimatedAction`) | A plain clip that could not reach its exit before the next PERFORMED action's latest start is not begun. Its hit still lands. This only happens on the fastest build. |
| Removed | The recovery cut (`PerformedCastNeedsTheFigure`, the `clip-cut` trace). |

Nothing about the fight changed: the beat, the release (beat − 250 ms), the flight, the targets, the feedback and the audio are all as before.

## The views

| File | View |
|---|---|
| `01_fast_tempo_true_speed_before_vs_after.mp4` | **The acceptance view.** The same fast-TEMPO seconds, BEFORE above and AFTER below, at true speed. |
| `01b_after_true_speed_audio.mp4`, `01c_before_true_speed_audio.mp4`, `01d_after_true_speed_silent.mp4` | Each on its own, with sound (and AFTER silent). |
| `02_fast_tempo_slow_motion_4x_before_vs_after.mp4` | The same, 4× slower. |
| `03_a_handoff_every_frame_after.png`, `03_b_…before.png` | Every frame from the swing's contact to SPRAY's release. |
| `04a_timeline_after.png`, `04b_timeline_before.png` | Lanes: the swing's frames, the handoff (recovery → exit), SPRAY. |
| `05_fast_tempo_8s_after_audio.mp4` | 8 s of the fast build: three SPRAYs, every handoff. |
| `06_fastest_build_true_speed_audio.mp4`, `06b_…slow_motion_4x.mp4`, `06c_timeline_fastest_build.png`, `06d_…every_frame_in/out.png` | The fastest build (TEMPO trained to 60 as well): SPRAY whole, the yield, SPRAY → HARD HANDS. |
| `07_normal_tempo_after_audio.mp4` | Normal TEMPO after the fix: no handoff happens. |
| `08a_join_swing_to_spray_full_size.png` | The swing's exit pose → SPRAY's first pose, full size. |
| `08b_join_hard_hands_to_spray_full_size.png`, `08c_…every_frame.png` | **The join that still pops**: HARD HANDS' exit pose is a crouch. |
| `10_timelines.md` | Every cast's timeline, the handoff rows first (previous contact, recovery start, exit, compression). |

## Measured

| Sequence | Handoffs | Worst recovery compression | Squeezed | SPRAY began on f0 |
|---|---|---|---|---|
| Normal TEMPO | 0 (the champion's frames are identical) | – | 0 | yes |
| Fast fixture (RHYTHM + VOLLEY), 33 s | 25: 15 settle only, 9 at the floor, 1 compressed to the ideal start | **2.60×** (a HARD HANDS recovery frame, 87 ms → 33 ms) | 0 | 10 of 10 |
| Fastest build (+ TEMPO 60), 33 s | 38 + 17 yields (11 swings, 6 of 11 HARD HANDS) | 1.74× (SPRAY's own recovery into HARD HANDS) | 0 | 17 of 17 (+1 at the rig's start-up seek) |
| BEFORE, fast fixture | – (the cut) | – | – | yes, but the swing was cut in its lunge |

For the swing → SPRAY handoff that snapped (AFTER, fast fixture):

| Moment | Playhead | vs SPRAY's beat |
|---|---:|---:|
| swing contact (its beat 5100) | 5106 | −694 |
| swing recovery starts (frames 0–6 whole) | 5214 | −586 |
| swing exit pose reached (54 ms of recovery in 33 ms: 1.63×, Floor) | 5248 | −552 |
| SPRAY clip start, frame 0 (anticipation) | 5250 | −550 |
| SPRAY release (pose, sound, knives on one frame) | 5567 | −233 |
| SPRAY contact (knives, health, flash, numbers, sound on one frame) | 5817 | +17 |

The swing now starts on time as well: its contact is on its beat (+6 ms) where it used to land ~140 ms late.

## Protected phases were never shortened

- **By construction.** `FitRecovery` copies every frame before the recovery marker unchanged, and `ActionHandoff.Plan` never places an exit before the recovery starts.
- **By test.** `action_handoff_test.cs` covers the plain phases, settle before pace, compression, pose dropping, reservation, floor, squeeze and yield.
- **By trace.** Every `handoff` line prints `protected=` (the recovery marker's ms) beside `recovery=`. No realistic run produced a `Squeezed` fit.

## What is still not continuous

- **HARD HANDS → SPRAY pops.** HARD HANDS' last frame is a low fighting crouch. After its (correct) handoff, the head rises ~40 px into SPRAY's standing pose in one frame. The same pop ends every HARD HANDS cast into idle. It is HARD HANDS' exit-pose art, out of scope for this pass.
- **The basic swing's blade disappears at the join.** Its exit pose is a half-rise with the blade still out. SPRAY's first pose (like idle) shows no blade. This already happens after every swing.
- **The fastest build does not animate the action just before a SPRAY** (above). Its hit lands during SPRAY's wind-up. The alternative is SPRAY entering at its third frame every cast. This is a decision for the owner.
