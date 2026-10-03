# Phase 0 / P0.2: the rig (RH_SHOT_BUILD, RH_SHOT_SEEK, RH_SHOT_KEYSTONES)

Traces only, no pictures (every frame was deleted right after its take). Filmed with
`tools/asset-pipeline/films_sweep.sh` (RH_SHOT_SEED=7, RH_PRESENT_TRACE=1).

## The five references, now posed through RH_SHOT_BUILD

| Take | Build | vs baseline `_a` | vs baseline `_b` |
|---|---|---|---|
| ref_seeker | `sig_seeker_hard_hands,volley_spray@Mind,hammer_press@Body,snare_jaws@Shadow` | IDENTICAL | IDENTICAL |
| ref_fast | the same, + `RH_SHOT_TAKE` (fastest TEMPO) | IDENTICAL | IDENTICAL |
| ref_brand | `sig_seeker_hard_hands,volley_spray@Body,sign_brand@Shadow,snare_jaws@Shadow` | IDENTICAL | IDENTICAL |
| ref_brand_press | `sig_seeker_hard_hands,volley_spray@Body,hammer_press@Body,sign_brand@Shadow` | IDENTICAL | IDENTICAL |
| ref_jaws_kill | the default build, `RH_SHOT_ENEMY=20,420` | IDENTICAL | IDENTICAL |

    python tools/asset-pipeline/action_regression.py --refs production/qa/evidence/vfx-sweep/phase0-baseline/<take>_a.log production/qa/evidence/vfx-sweep/phase0-rig/<take>.log

Each log carries `RH_SHOT_BUILD verified at the shutter: slot 0 ... slot 3 ...` (the live run checked against the
asked build). The fight (`event` lines) is identical event for event in every take. `--refs` was refined in this package
for three cold-start / frozen-playhead SAMPLING effects (see notes.md, P0.2); the A/A baseline pairs stay IDENTICAL and
the negative controls are still caught (ref_seeker vs ref_brand: 132 differences; a field-draw body y edit and a lost
alloc jump are each 1 difference).

## Refusals and the legal Quiver

| Log | Asked | Result |
|---|---|---|
| refuse_three_passives.log | `RH_SHOT_HUNTER=quiver RH_SHOT_BUILD=sig_quiver_backdraw,hammer_press@Body,snare_jaws@Shadow,volley_spray@Mind` | exit 1: "refused by the build rule (PassivesFull): it holds 1 Active (volley_spray) and 3 passives (sig_quiver_backdraw, hammer_press, snare_jaws)" |
| refuse_build_beside_swap.log | RH_SHOT_BUILD and RH_SHOT_SWAP together | exit 1: "both set ... Use one." |
| seek_absent.log | `RH_SHOT_SEEK=hit:Reflect` on the default fixture | exit 1: "found no such event in this wave (116 events): nothing to film." |
| quiver_legal_backdraw.log | `RH_SHOT_HUNTER=quiver RH_SHOT_BUILD=sig_quiver_backdraw,volley_spray@Mind,hammer_press@Body,hammer_blow@Body RH_SHOT_SEEK=skill:sig_quiver_backdraw RH_SHOT_ENEMY=300,9` | filmed; BACKDRAW in slot 0 (`event Skill slot=0 at=3300`), the shutter on it |
| keystones.log | the default build + `RH_SHOT_KEYSTONES=weaver,venomancer,rend` | filmed; verified with the three keystones composed |
