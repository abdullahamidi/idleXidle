# The attention pass, photographed — 2026-09-16

Every state the attention-ownership pass created or changed, at 100 / 125 / 150 (and Reduced Motion
where the state has motion in it), looked at rather than reasoned about. The pictures live here and
in `production/qa/evidence/dispatches/`.

## Re-running the whole matrix

```
LOCALAPPDATA=/mnt/c/Users/<you>/AppData/Local bash tools/asset-pipeline/attention_fixtures.sh [filter]
```

One command, ~75 captures (~40 s each) plus two opening-rig runs. The filter is a substring of
`<dir>/<state>`, so one family re-takes on its own (`death`, `dispatches/dispatches`, `flourish`,
`opening`). The recipe IS the matrix: each row is `<dir>|<state>|<mode>|<scale>|<dials>` and lands at
`production/qa/evidence/<dir>/<state>_<scale>[_reduced].png`.

The end-of-the-opening frames are not captures — four rail tiles break on the single frame the last
authored card is answered, which no shutter can pose — so the recipe runs `tools/check_opening_flow.sh`
and lifts the middle frame of the rig's own `NAV_BREAK` film burst.

## The matrix

| State (product spec §13) | Fixture | Dials | Files |
|---|---|---|---|
| A the readable death | `fightfall` | `RH_SHOT_T=0.6` | `death/fightfall_060_{100,125,150}` (+`_hover_100`) |
| A the transition | `fightfade` | `RH_SHOT_T=0.25/0.5/0.75/1` | `death/fightfade_{025,050,075,100}_{100,125,150}` |
| A the transition, refusing | `fightfade` | `RH_SHOT_T=0.25` + cursor on the medallion | `death/fightfade_025_hover_100` |
| A Reduced Motion | `fightfade` | `RH_SHOT_REDUCED=1` | `death/fightfade_{025,075}_*_reduced` |
| B the first-failure lesson | `fightfade` | `RH_SHOT_T=1` + `RH_SHOT_LESSON=FirstFailureReport` | `owner/fightfade_100_lesson_*` |
| C the log open | `runlog` | `RH_SHOT_LESSON=FirstPostFailureChange` | `owner/runlog_lesson_*` |
| D the log shut, same lesson | `fight` | `RH_SHOT_LESSON=FirstPostFailureChange` | `owner/fight_changelesson_*` |
| E chest + waiting dispatch | `chestdispatch` | `RH_SHOT_T=1.2` / `5` | `dispatches/chestdispatch_{during,after}_*` |
| F the inbox | `dispatches` | — / `read` / `many;RH_SHOT_SCROLL=end` / `vow.<id>` | `dispatches/dispatches*`, `dispatch_vow_*` |
| F the empty state · G the migrated veteran | `dispatchesempty` | — | `dispatches/dispatchesempty_*` |
| F the mark, the hover, the lesson | `dispatchesunread` / `dispatcheshover` | cursor on the envelope · `RH_SHOT_LESSON=FirstDispatchOpened` | `dispatches/dispatches{unread,hover}_*`, `dispatchlesson_*` |
| one fixture per family | `fight` `lootforge` `warren` `forge` | — | `dispatches/producers/family_*` |
| the chrome overrun at 150 | `forge`, `buildtree` | — | `dispatches/producers/chain_*_150` |
| the coach-tier exception | `dispatchesunread` | `RH_SHOT_LESSON=FirstPostFailureChange` / `RH_SHOT_OPENING=IntroduceHealth` / `RH_SHOT_REDUCED=1` | `flourish/coachdot_*` |
| the end of the opening | the opening rig | `check_opening_flow.sh 100 \| 150` | `flourish/openingend_breaks_*` |

G's proof is the boot check's migration lanes, not the picture — `tools/check_boot.sh`:

```
v4 save's inbox seeded as already known — inbox	unread=0	known=4	seeded=True	unread_keys=
v5 save's inbox seeded as already known — inbox	unread=0	known=4	seeded=True	unread_keys=
v8 save believed as written — its letter stays unread, nothing seeded — inbox	unread=1	known=1	seeded=False	unread_keys=region.verdant_hollow.conquered
```

## What the pictures said

| Picture | What it shows | Verdict |
|---|---|---|
| `death/fightfall_060_*` | The collapse: the body on the floor, the red flash held over the whole canvas, the wave header as state. No plate, no BACK TO WAVE, no READ THE LOG plate, no black. The envelope and the `?` are gone — the death owns the frame. | PASS |
| `death/fightfall_060_hover_100` | The same with the cursor on the log medallion: the medallion lifts and its tip paints. The readable beat is live. | PASS |
| `death/fightfade_025_*` | Fade-out mid-way: the fallen body and the page dimmed under the host chrome. | PASS |
| `death/fightfade_050_*` | The hold: the page fully black, only the rail, the capsules and the gear. | PASS |
| `death/fightfade_075_*` | Fade-in mid-way: the Hunter standing, WAVE 1, the new pack still sliding in. | PASS |
| `death/fightfade_100_*` | The stage back, the envelope and `?` back — and NO coach card: the fall's end hushes the lesson. | PASS |
| `death/fightfade_025_hover_100` | The cursor on the medallion under the black: no lift, no whitening, no tip. The refused control does not paint as interactable. | PASS |
| `death/fightfade_025_*_reduced` | Reduced Motion at the fade-out's mid-point: fully black — the cut, not an alpha. | PASS |
| `death/fightfade_075_*_reduced` | Reduced Motion at the fade-in's mid-point: the stage already back, and the READ THE LOG card up. A pose artifact — the frozen phase never crosses the transition's end, which is the edge that hushes the coach; the un-reduced pose at the same t is still inside the Death owner. | PASS (noted) |
| `owner/fightfade_100_lesson_*` | The transition finished and the lesson allowed: gold brackets on the real EXPEDITION LOG medallion, the card IT FELL / READ THE LOG with its ×, no toast, no notice. | PASS |
| `owner/runlog_lesson_*` | The report being read with MAKE ONE CHANGE forced: no card, no brackets, no scrim over the log. The rail and the `?` are not painted under it. | PASS |
| `owner/fight_changelesson_*` | The log shut, the same lesson guiding: the card beside the rail, the page dimmed, the rail kept at full brightness with its bracket edge. | PASS (the rail's own light is faint — see the defects list) |
| `dispatches/chestdispatch_during_*` | A letter posted DURING the reveal: the cascade owns the frame, and the envelope is not painted at all — no mark, no halo, no toast. | PASS |
| `dispatches/chestdispatch_after_*` | Past the reveal's hold: the envelope is back with its unread mark, the letter waiting. | PASS |
| `dispatches/dispatches_*` | The panel over a fight, `6 UNREAD · 7 KEPT`, the cursor and the open letter on one row, the region kicker at the row's right end, the rule between the columns, MARK ALL READ live. | PASS |
| `dispatches/dispatches_allread_*` | `7 KEPT`, no dots anywhere, MARK ALL READ in its disabled face. | PASS |
| `dispatches/dispatchesempty_*` | `NO DISPATCHES YET` and its one sentence, centred; no counter, no footer. | PASS |
| `dispatches/dispatchesunread_*` | The fight with the chrome intact: gear, `?`, envelope with its red mark clearing the gap, the ROSTER tile carrying its own dot. | PASS |
| `dispatches/dispatcheshover_*` | The cursor on the envelope: the medallion lifts and whitens, `DISPATCHES — M` right-aligned under it. | PASS |
| `dispatches/dispatchlesson_*` | `FirstDispatchOpened`: brackets on the envelope, the card A DISPATCH ARRIVED beside it, clear of the chrome row. | PASS |
| `dispatches/dispatchesmany_scrolled_*` | 23 letters scrolled to the foot, the scrollbar thumb at the bottom of its track. Only the long WARREN headline and the kicker'd region rows shorten. | PASS |
| `dispatches/dispatch_vow_granted_*` | `A VOW IS OFFERED TO YOU — VOW OF COMPLETION` — the sentence a HANDED vow gets. | PASS |
| `dispatches/dispatch_vow_proved_*` | `A VOW HAS REVEALED ITSELF — VOW OF THE BLUNT EDGE`, wrapping to two lines at 150. | PASS |
| `producers/family_fight_100` | The hunt: the boot line on its own channel, the envelope's mark the only new thing. | PASS |
| `producers/family_lootforge_100` | The reveal owning the frame; no envelope, no `?`. | PASS |
| `producers/family_warren_100` | One surface: the coach card TRAINING / TRAIN ANY STAT ONCE with the rail lit. (Before the fix below, the warren's own hint plate stood over the same page.) | PASS |
| `producers/family_forge_100` | The bench with six capsules; no card, no plate, the envelope unmarked on this account's facts. | PASS |
| `producers/chain_sixpills_forge_150` · `chain_longtitle_mastery_150` | The known chrome overrun — see the defects list. | DEFECT (reported) |
| `flourish/coachdot_coach_*` | The coach lighting the rail while the ROSTER dot keeps its full brightness AND its breathing halo: the coach is not above its own tier. | PASS |
| `flourish/coachdot_modal_*` | The same dot under the authored opening: dimmed with the page, no halo. | PASS |
| `flourish/coachdot_100_reduced` | The control: same dot, same brightness, no breath at all. | PASS |
| `flourish/openingend_breaks_*` | The end of the opening: GEAR, TRAINING, VAULT and FORGE tear their chains on one frame, in one phase, while the six still-locked tiles between them keep theirs. It reads as one moment, not a burst. | PASS |

## Defects the pictures found

1. **The screen title is unreadable behind the currency chain** (pre-existing, escalated, NOT fixed).
   At 150 % with six ornate capsules the chain's left edge is canvas x 427 against a floor of 1210,
   and the FORGE title shows through the gap between two capsules as a legible gold "RO"
   (`chain_sixpills_forge_150`); the same happens at 100 % (`family_lootforge_100`, `chestdispatch_after_100`).
   With only three capsules at 150 % the longest title is still overdrawn — `chain_longtitle_mastery_150`
   reads "MASTERY TRE" with the gleam capsule on its last letter. The compact rule in `DrawCurrencyPills`
   is spent: the width is capsule chrome, not digits (the values there are "0", "515", "40"). Fixing it
   needs fewer capsules, a compact capsule, or a title that yields — a chrome decision, not a layout bug.
2. **A rail-targeted lesson's light is nearly invisible.** `DrawCoachSpotlight` paints its scrim in the
   chrome batch BEFORE `DrawHexNav`, so the rail is never dimmed and never brightened; a lesson whose
   target is the whole rail column reads only through a 4 px bracket edge (`fight_changelesson_*`).
   Pre-existing; a fix is paint-order work on the chrome.
3. **At 150 % the capsules and the gear overlap the EXPEDITION LOG panel's top border**
   (`runlog_lesson_150`). Nothing is obscured and the × is clear; cosmetic, pre-existing.

Four more the pictures found were fixed rather than listed — the hint slot standing beside a lit
coach card, `dispatchesempty` no longer being empty, `RH_SHOT_DISPATCH=read` showing an unread dot,
and the list's highlight sitting on a different row from the letter in the pane. See
`fix(chrome): one card at a time on a menu screen, and three poses that had stopped posing`.
