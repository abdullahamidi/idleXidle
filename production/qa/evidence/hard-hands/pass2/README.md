# HARD HANDS — second pass: skill identity, the retreat, the cloak (2026-09-25) — ACCEPTED

The owner accepted HARD HANDS as the MELEE / DIRECT-CONTACT reference after this pass, and approved its two sounds.

The owner approved HARD HANDS' choreography (the leaping overhand hammer-fist, the held fist, the contact on the
beat, the root motion to the target, the impact, the exit pose, the handoff and the fast-TEMPO yield) and asked for
three fixes before it becomes the accepted melee reference. The two HARD HANDS sounds were unchanged by this pass
(the owner has since listened to and approved them).

1. **Skill identity.** HARD HANDS' recipe was found through the Seeker's Strike Form and its `strike` effect, and BLOW
   says the same two words, so BLOW was performed as HARD HANDS: the lunge, HARD HANDS' sounds, impact, flash, duck
   and callout timing. The recipe now belongs to the skill (`sig_seeker_hard_hands`) and plays its own strip
   (`seeker_hard_hands`); the Strike Form's strip is BLOW's knife swing again, byte for byte.
2. **The retreat.** The way home was a 136 ms spring with a 25 px hop. It is now a controlled retreat in a guarded
   backstep pose (a new key pose): about 234 ms, a 7 px low bound, leaving the target slowly and settling in.
3. **The cloak.** The flared cloak on the contact and follow-through was painted in the hood's grey-green. It is
   remapped, deterministically, to the idle cloak's own colours.

PREVIOUS = the committed build 94f20c2 (the first pass). BEFORE = the same commit with BLOW woven on the Seeker.
NOW = this pass.

| File | What it shows |
|---|---|
| `01a_now_true_speed_audio_with_music.mp4` · `01b_…sfx_only` · `01c_…audio_off` | NOW at true speed, with the music, with the effects only, silent |
| `01d_previous_hop_vs_now_retreat_true_speed.mp4` | the same fight, PREVIOUS above (the hop) and NOW below (the retreat) |
| `02a_previous_vs_now_return_slow_motion_4x.mp4` · `02b_now_slow_motion_4x_audio.mp4` | the return, 4× slower |
| `03a_animation_only_vfx_off.mp4` · `03b_…every_2nd_frame.png` | effects off: the poses and the root motion alone |
| `04a_now_hard_hands_to_idle_every_frame.png` · `04b_previous_…` | contact to idle, every frame |
| `05a_fast_tempo_hard_hands_to_spray_every_frame.png` · `05b_…audio.mp4` | fast TEMPO: the retreat compressed to its floor, then SPRAY from its first pose |
| `06a_fastest_build_yield_every_frame.png` · `06b` true speed · `06c` 4× | the fastest build: HARD HANDS yields after its contact to a SPRAY 500 ms later |
| `07a_blow_before_vs_after_isolation_true_speed.mp4` · `07b` / `07c` sound · `07d` sheet | BLOW before (performed as HARD HANDS) and after (its own legacy swing) |
| `08a_press_16s_audio.mp4` | PRESS (a field): 16 s of its ticks beside HARD HANDS |
| `09_key_poses_now_and_previous.png` | the eight key poses (the retreat on 6, the cloak on 4–5), their silhouettes, and the previous strip |
| `10_return_path.md` | the way home frame by frame: x and y from home, per 60 fps frame, NOW and PREVIOUS |
| `11_timelines_joins_and_census.md` | timelines against the beat, every join, the fastest build's handoffs, BLOW's and PRESS's census |

Review page: https://claude.ai/artifact/MUVvsDxsEZkdfcktNC8qEY
