# Phase 0 acceptance evidence (remaining-skill presentation sweep, P0.6, 2026-10-03)

Phase 0 = the generic-path fixes (`production/vfx-sweep/design.md` section 8): quiet derived hits, one `sfx_hit` per
batch ms, the last-owner rule, callouts for Actives only, no pitched thud for a no-damage reaction, the phantom skip,
the WEAVER echo flag, the summed heal receive, the ShieldGained claim, no `trap` clip for reactions; plus the rig
(RH_SHOT_BUILD / RH_SHOT_SEEK / RH_SHOT_KEYSTONES, `films_sweep.sh`, `sound_throttle.json`).

Every take: `bash tools/asset-pipeline/films_sweep.sh [--mp4] p06 <take>` (RH_SHOT_SEED=7, RH_PRESENT_TRACE=1), one
take at a time, every frame deleted right after its mp4. The mp4s are rendered by `film_audio.py` with the traced
sound (throttled from `tools/asset-pipeline/sound_throttle.json`) and re-encoded here at 720p, CRF 28 (all < 2 MB, so
no contact sheets were needed). Captions: none; the take name is the file name.

## (a) The Seeker's five references: 0 differences

`regression.txt` holds the full `action_regression.py --refs` output of the five after-traces (`ref_*.log`, filmed
at the Phase 0 code) against BOTH baseline films in `../phase0-baseline/` (`_a`, `_b`, filmed at ae20a7fd):
**IDENTICAL x 10**, each on the first film. The fight (`event` lines) is identical in order in all ten comparisons
(ref_fast's after-film covers four events more than `_a`, the same four `_b` has: window coverage, not a change).

| take | vs _a | vs _b | events |
|---|---|---|---|
| ref_seeker | IDENTICAL | IDENTICAL | 62 = 62 |
| ref_fast | IDENTICAL | IDENTICAL | 48 (= `_b`; `_a` traced 44, a prefix) |
| ref_brand | IDENTICAL | IDENTICAL | 57 = 57 |
| ref_brand_press | IDENTICAL | IDENTICAL | 62 = 62 |
| ref_jaws_kill | IDENTICAL | IDENTICAL | 24 = 24 |

## (b) The films, measured from their traces

| take | pose | window (playhead ms) |
|---|---|---|
| after_multi | `fightmulti`, the default build (vs `../phase0-before/before_multi`) | 5263-10230 |
| after_bleed | `fightmulti`, WEEP for JAWS by RH_SHOT_SWAP (vs `../phase0-before/before_bleed`) | 5263-10230 |
| after_default | the default fixture, `RH_SHOT_T=0.5`, 150 x 4 (its before is the trace `../phase0-baseline/ref_seeker_a.log`) | 1867-11817 |
| after_backdraw | quiver, BUILD `sig_quiver_backdraw,hammer_press@Body,volley_spray@Mind,hammer_blow@Body`, seek `skill:sig_quiver_backdraw+unstruck` | 12413 -> next wave |
| after_weaver | default build + `RH_SHOT_KEYSTONES=weaver`, `RH_SHOT_T=9` | 10283-15817 |
| after_drink | seeker, BUILD `sig_seeker_hard_hands,drain_drink@Nature,hammer_press@Body,snare_jaws@Shadow`, seek `skill:drain_drink`, `RH_SHOT_ENEMY=1500,25` | 9730-14697 |
| after_holdfast | unbroken, `RH_SHOT_VARIATION=sig_unbroken_hold_fast:BREASTWORK+GROUNDWORK`, 150 x 8 | 1333-21750 |

Columns: `sfx_hit` all asks; `max/ms` the most Strike thuds (pitch 0) asked in one batch (one frame, one playhead);
`quiet-only` batches whose every Strike is a derived (Bleed / Reflect / Carry / Deadweight) hit, and the `flash` lines
in them; `heal +N` the summed heal callouts; `open cue` the `sfx_shield_gain` asks on the wave-open (at=0) gain;
alloc per layer as `lines / non-zero / max bytes` from the `*-draw` trace lines.

| take | Strikes | quiet hits (grade=Quiet) | sfx_hit | max/ms | quiet-only batches | flash on them | skill-skip | echo | heal +N | fx_heal | open cue / gain cues | field-draw alloc | reaction-draw alloc | callouts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| before_multi | 10 | 0 | 9 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 / 0 | 119/5/13107864 | 99/0/0 | HAMMER, VOLLEY |
| after_multi | 10 | 0 | 9 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 / 0 | 143/5/13107392 | 72/1/5032 (see note) | HARD HANDS, SPRAY |
| before_bleed | 17 | 0 | 19 | 1 | (10 bleed-only) | **10** | 0 | 0 | 0 | 0 | 0 / 0 | 142/6/14193328 | - | HAMMER, VOLLEY |
| after_bleed | 17 | 10 (10) | **9** | 1 | 10 | **0** | 0 | 0 | 0 | 0 | 0 / 0 | 143/6/14193328 | - | HARD HANDS, SPRAY |
| ref_seeker_a (before) | 17 | 0 | 16 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 / 0 | 254/10/13107392 | 163/0/0 | HAMMER, VOLLEY |
| after_default | 17 | 0 | 16 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 / 0 | 268/11/13107392 | 163/0/0 | HARD HANDS, SPRAY |
| after_backdraw | 7 | 0 | 8 | 1 | 0 | 0 | **1** | 0 | 0 | 0 | 0 / 0 | - | - | BLOW, SPRAY |
| after_weaver | 10 | 0 | 8 | 1 | 0 | 0 | 0 | **1** | 0 | 0 | 0 / 0 | 168/7/14193328 | 22/0/0 | HARD HANDS, SPRAY |
| after_drink | 8 | 0 | 11 | 1 | 0 | 0 | 0 | 0 | **1** (+57: 46 + 11 at 9700) | **0** | 0 / 0 | 136/6/13107392 | 99/0/0 | DRINK |
| after_holdfast | 23 | 0 | 36 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | **0** / 5 (mid-wave, unclaimed until Phase 3) | - | - | SHIELD BROKEN, SPRAY |

before_bleed's bleed-only batches are found at the ms after_bleed traced as `quiet-hit` (the fight is identical): each
of the 10 flashed and thudded before, none after. The other `sfx_hit` asks are the enemy bite's pitched thud (-0.25),
which is not a Strike. No take has a mark-draw layer.

Notes:
- **field-draw alloc** is PRESS's closed layer: the 13-14 MB first-use jump in the wave's first field frames, the same
  in every before and after (open since P0.1, left for Phase 2's FieldLayers).
- **after_multi's one reaction-draw frame of 5032 bytes** (playhead 1233) did NOT repeat: the same take re-filmed
  (trace only) has 73 reaction-draw lines, all `alloc=0`. Runtime lazy work on that frame, like the mark-draw first
  frame (P0.1); the closed ref takes hold reaction-draw `alloc=` exactly and are identical.

## (c) Gates (at the end of P0.6)
- `bash tools/check_all.sh`: all gates green.
- `dotnet build IdleXIdle.sln -c Release -warnaserror`: 0 warnings, 0 errors.
- Core.Tests 1901/1901, Game.Tests 980/980.

## (d) Approved cues untouched
`git diff ae20a7fd -- assets/audio` is empty (and `git status assets/audio` clean): the JAWS / PRESS / BRAND SHA-256
pins in jaws_reaction_test / press_field_test / brand_audio_test pass.
