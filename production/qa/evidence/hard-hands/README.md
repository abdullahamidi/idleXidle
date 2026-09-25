# HARD HANDS — the melee gold standard (ADR-011, 2026-09-25) — ACCEPTED (after the second pass, `pass2/`)

Review page: https://claude.ai/artifact/MUVvsDxsEZkdfcktNC8qEY

The Seeker's HARD HANDS rebuilt as the project's MELEE / DIRECT-CONTACT reference, to the standard of the approved
SPRAY. The verb: a leaping overhand HAMMER-FIST. He draws his fist up, bounds in, drives it down onto the creature
with his whole weight ON the beat, and hops back.

Every film is the real game, traced (`RH_PRESENT_TRACE=1`). The sound in an MP4 is rendered from the cues the game
asked for, on the trace's clock (`tools/asset-pipeline/film_audio.py`): it is the game's mix, not a device
recording. **The two HARD HANDS cues were human-listened and approved by the owner (2026-09-25).**

OLD = the committed build before this pass (b607e8e, filmed in a worktree). NEW = this pass. The fight is the same
seed and the same beats in both.

| File | What it shows |
|---|---|
| `01a_old_vs_new_true_speed.mp4` | the same fight, true speed: OLD (a knife slash 350 px from the creature, the effect on its own) above, NEW below |
| `01b_new_true_speed_audio_with_music.mp4` · `01c_…sfx_only` · `01d_…audio_off` | NEW with the region's music, with the effects only, and silent |
| `01e_old_true_speed_sfx_only.mp4` | OLD with its generic breath and thud |
| `02a_old_vs_new_slow_motion_4x.mp4` · `02b_new_slow_motion_4x_audio.mp4` | 4× slower, stacked; and NEW slowed with its sound slowed alike |
| `03a_animation_only_vfx_off.mp4` · `03b_…every_2nd_frame.png` | effects off: the poses and the lunge alone must read |
| `04a_effects_only_champion_hidden.mp4` · `04b_effects_only_sheet.png` | the champion hidden: the impact alone, starting AT the fist on the beat |
| `05_contact_socket_overlay.png` | `RH_SHOT_SOCKETS`: the StrikeHand socket riding the drawn fist; the impact begins at it |
| `06_repeated_casts_16s_audio.mp4` | 16 s of the plain build (one picture per 10 frames) for fatigue |
| `07a…07c` fast TEMPO (first cast, HARD HANDS → SPRAY, 33 s) · `07d` OLD vs NEW at fast TEMPO | |
| `08a_fast_tempo_hard_hands_to_spray_every_frame.png` · `08e_OLD_…` | the join into SPRAY, every frame, new and old |
| `08b_fastest_build_yield_every_frame.png` · `08c` true speed · `08d` 4× | the fastest build's HARD HANDS → SPRAY 500 ms later: the YIELD after the contact |
| `09a_new_hard_hands_to_idle_every_frame.png` · `09b_OLD_…` | the join into idle, every frame |
| `10_key_poses_silhouettes_and_old.png` | the eight key poses, their flat silhouettes, and the old strip |
| `11_timelines.md` | every moment against the beat (trace), new at three tempos and the old plain clip |
| `12_joins_and_yields.md` | the measured silhouette jump at every join, new and old; the fastest build's handoff census |

## Decisions recorded

- ADR-011 (§ The melee reference, Decision 1 and 10, Consequences, Known limits).
- `design/art/arena-art-contract.md` §3.8 (a melee blow is carried; key poses from a light ground are repaired).
- `design/audio/seeker-hard-hands-audio-brief.md` (the two cues, human-approved).
- PixelLab jobs, with the reason for each verdict: `tools/asset-pipeline/v2/keyposes.py` `ACTIONS["seeker_hard_hands"]`;
  skeletons in `tools/asset-pipeline/v2/keypose_skeletons/seeker_hard_hands.json`; sources cached in
  `tools/asset-pipeline/v2/keypose_sources/`.
