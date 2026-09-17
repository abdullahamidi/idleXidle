# One surface at a time, after the visual pass — 2026-09-17

Phase 5 of the visual identity pass. The pass added a shape-aware light, larger item frames, a new
enemy cast and new prologue plates, and any of those could have set two things in front of the player
at once. Each pair below is decided by the one attention owner (ADR-007): it is assigned once per
Update, highest tier wins (Opening > Modal > Reveal > Death > Report > Coach > Feedback > None), and
every surface asks `AttentionOwnedAbove(tier)`. Nothing about that model changed in this pass. This
folder is the pictures and the one defect they found.

Re-run: `bash tools/asset-pipeline/integration_fixtures.sh [filter]` and
`bash tools/check_opening_flow.sh <100|125|150>`.

## The nine pairs

| # | Pair | What decides it | Evidence | Verdict |
|---|---|---|---|---|
| 1 | The coach under a dispatch arrival | The arrival pulse is held until nothing owns the frame (`AttentionOwnedAbove(None)`), and spending it now hushes the coach (see the defect below) | `dispatch_lesson_*`; opening trace rule "no lesson began on top of a letter's arrival" | PASS after the fix |
| 2 | The coach under a chest reveal | Reveal outranks Coach; the director is told `RewardUp` and holds its lesson | `reveal_notice_during_*`, `chest_letter_during_*`: the reveal alone, no card, no light | PASS |
| 3 | The dispatch panel over item art | The panel is a Modal; its scrim owns the page | `letters_panel_*`: the reading panel over a dimmed page, nothing else lit | PASS |
| 4 | The death transition with the focus light | Death outranks Coach; the director is told `DeathUp`; a fall's end hushes it | `fight_lesson_*` (control) against `fade_lesson_*`, `fade_lesson_black_*`; `death_sweep_*` (below) | PASS |
| 5 | The opening's light against the chrome | Opening owns the frame; the ? and the envelope ask the owner and are not offered; the rail's chain breaks wait for the last authored click | `../focus/*_intro_*`; the opening trace: no notice, no dispatch, no lesson and no rail ceremony over the opening, at 100 / 125 / 150 | PASS |
| 6 | The enemy introduction against a notice | Opening outranks Feedback; the notice's clock holds | `enemy_intro_notice_*`: the Bog Weaver lit, the card beside it, no toast | PASS |
| 7 | The Gear lesson with the enlarged item frames | The light is the cell's rectangle (unchanged); the frame is 94 % of it | `gear_lesson_*`, `../focus/gear_cell_*`: the whole frame inside the light, the card clear of it | PASS |
| 8 | The first chest reveal against a dispatch arrival | Reveal owns; the arrival is held (no pulse, no cue) and the envelope is not offered | `chest_letter_during_*` (no envelope, no toast) and `chest_letter_after_*` (the envelope with its unread mark once the reveal is gone) | PASS |
| 9 | The first dispatch lesson once attention frees | `FirstDispatchOpened` is an ordinary coach lesson aimed at the envelope; it can only light when nothing above the coach owns the frame | `dispatch_lesson_*`: A DISPATCH ARRIVED beside the lit envelope, the rest of the page dark | PASS |

## The defect the pictures found — and fixed

**The first letter and the first lesson landed on the same frame.** The owner is decided near the top
of Update, before the coach chooses its lesson for that frame. On the frame the opening's last CONTINUE
lets go, the owner reads None, so the held arrival was spent (its halo and its cue), and a few lines
later the director picked `FirstTrainingPurchase` and lit the TRAINING tile. The autoplayed opening
recorded both at frame 5713.

The fix gives the arrival its moment the way a fall's end already does: spending the pulse hushes the
coach for `OnboardingDirector.QuietAfterReward` (2.5 s), and only when no lesson already stands, so a
standing card never blinks (`Game1.cs`, the arrival block). The owner model is untouched: one owner, one
assignment, no new tier. After the fix the same run records the pulse at 5713 and the lesson at 5862.

Guards: `attention_owner_test` pins the hush inside the arrival block and counts the host's hushes
(the fall's end and the arrival, nothing else). `tools/check_opening_trace.py` gained the rule "no
lesson began on top of a letter's arrival", which fails on the pre-fix trace and passes on the new one
at 100 / 125 / 150.

## The death transition

The fall → fade → restart, posed on `fightfade` (`RH_SHOT_T` sweeps the transition: 0 the last readable
instant, 0.5 the black, 1 the stage back): `death_sweep_{0,0.25,0.5,0.75,1}_100`, `death_sweep_0.5_150`,
`death_sweep_1_125`, and `death_sweep_0.5_100_reduced` (Reduced Motion cuts).

| Picture | What it shows | Verdict |
|---|---|---|
| `death_sweep_0_100` | The Hunter collapsed on the floor, the wave that killed it (two Night Carapaces, the save region's own family) still standing, the HUD at full value. | PASS |
| `death_sweep_0.25_100` | The page darkening over the fall. | PASS |
| `death_sweep_0.5_100`, `_150`, `_100_reduced` | Black, with only the rail and the currency chain above it. No plate, no card, no button. | PASS |
| `death_sweep_0.75_100` | The next descent already under the fade: WAVE 1, the Hunter standing, a new pack arriving. | PASS |
| `death_sweep_1_100`, `_125` | Back in play at WAVE 1 against Gloom Whelps. | PASS |
| every frame | **No fall plate and no GO AGAIN** anywhere in the transition. | PASS |

The lesson against the fall (pair 4):

| Picture | What it shows | Verdict |
|---|---|---|
| `fight_lesson_{100,125,150}` | The control. YOUR HUNTER FIGHTS FOR YOU, the Hunter cut out of the scrim by its own outline, the card beside it. | PASS |
| `fade_lesson_{100,125,150}` | The same lesson posed at 0.1, the collapse: no scrim, no light, no card. The death owns the frame. | PASS |
| `fade_lesson_black_{100,125,150}` | The same at 0.5: black, no card over it. | PASS |

## Other captures in this folder

| Files | What they pose | Verdict |
|---|---|---|
| `reveal_notice_during_*` | A notice queued under the chest reveal: the reveal alone; the toast does not paint. | PASS |
| `reveal_notice_after_*` | Past the reveal's hold: the notice (YOU ARE THE ANVIL) lands on the Forge. | PASS |
