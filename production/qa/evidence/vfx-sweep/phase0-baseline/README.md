# Phase 0 reference baseline (remaining-skill presentation sweep, P0.1)

The Seeker's five CLOSED references (SPRAY, HARD HANDS, JAWS, PRESS, BRAND) traced at the clean HEAD **ae20a7fd**,
before any sweep presentation code moved (`production/vfx-sweep/design.md` section 8). Every take was filmed TWICE
(`_a` / `_b`, the A/A pair) by `tools/asset-pipeline/films_sweep.sh`; logs only, no pictures (the frames were deleted
right after each take).

Every later phase re-films a take and holds it against `<take>_a.log`:

    bash tools/asset-pipeline/films_sweep.sh <tag> ref_seeker
    python tools/asset-pipeline/action_regression.py --refs production/qa/evidence/vfx-sweep/phase0-baseline/ref_seeker_a.log build/shots/sweep/<tag>/ref_seeker.log

`--refs` compares the fight (every `event`, in order), the actions (clip-start / release / contact / contact-tick /
handoff / yield / clip-end / root), every `reaction-*`, `field-wave` / `field-draw` / `field-cue`, `mark-wave` /
`mark-draw` / `mark-cue` line and every `sfx_seeker_spray` / `_hard_hands` / `_jaws` / `_press` / `_brand` sound, keyed
by the playhead. Not compared: `draws=` / `batches=` (upper bounds that include the generic effects pass Phase 0
changes), callouts, flashes, numbers, vfx-spawn, generic sounds.

## Takes (all: `RH_SHOT_SEED=7`, `RH_PRESENT_TRACE=1`, `RH_SHOT_HUNTER=seeker`, mode `fight`)

| Take | Env | Frames | Playhead covered | Events | Reference lines a / b | Moments both traced | Draw samples in one film only | A/A |
|---|---|---|---|---|---|---|---|---|
| ref_seeker | default fixture (HARD HANDS, SPRAY@Mind, PRESS@Body, JAWS@Shadow), `RH_SHOT_T=0.5` | 40 x 15 | 0-11.6 s | 62 | 529 / 533 | 522 | 6 | IDENTICAL |
| ref_fast | default fixture, `RH_SHOT_TAKE=bite,swift,quick,road_sign_2,brisk,rhythm,blitz,volley`, `RH_SHOT_T=3.4` | 40 x 6 | 3.4-8.7 s | 44 | 319 / 321 | 305 | 14 | IDENTICAL |
| ref_brand | `RH_SHOT_SWAP=volley_spray:volley_spray@Body,hammer_press:sign_brand@Shadow`, `RH_SHOT_T=0.5` | 40 x 15 | 0-11.6 s | 57 | 870 / 870 | 867 | 3 | IDENTICAL |
| ref_brand_press | `RH_SHOT_SWAP=volley_spray:volley_spray@Body,snare_jaws:sign_brand@Shadow`, `RH_SHOT_T=0.5` | 40 x 15 | 0-11.6 s | 62 | 956 / 965 | 953 | 1 | IDENTICAL |
| ref_jaws_kill | default fixture, `RH_SHOT_ENEMY=20,420` (JAWS' killing answer), `RH_SHOT_T=0.5` | 40 x 10 | 0-2.0 s (the wave ends) | 24 | 357 / 352 | 87 | 0 | IDENTICAL |

"Reference lines" counts raw lines; Draw-sampled lines repeated at one playhead (the playhead freezes once the killing
answer ends ref_jaws_kill's fight) count once per moment.

## Fields excluded because they differ A/A (named in `action_regression.py`)

| Line | Field | Why |
|---|---|---|
| mark-draw | `ticks=`, `flush=` | Stopwatch ticks of the curse's draw and flush: wall-clock CPU time |
| mark-draw | `alloc=` held as 0 / non-zero | the first curse frame's bytes include the runtime's lazy work (25752 vs 1128 A/A); 0 on every steady frame |
| field-draw | `shape=` | PRESS's front rectangle is latched in Draw from the target's pose on the first drawn frame after launch; a catch-up tick that skips that Draw latches it one frame later (ref_fast: 1056,700,340,184 vs 1109,688,225,196) |
| field-draw | `alloc=` held as 0 / non-zero | the wave's running total jumps 8-14 MB on a few frames (a first-use load inside the measured window) on whichever frame Draw sampled (ref_fast at 5717: 13107864 vs 48) |

Draw-sampled lines (`root`, every `*-draw`) at a moment only one film has are reported, not counted (a catch-up tick
skips a Draw). Negative controls: ref_seeker vs ref_brand reports 131 differences; a one-sprite edit to one
`reaction-draw` line and a volume edit to one JAWS cue are each caught; a `draws=` edit and a generic `sfx_hit` edit are
not (by design).
