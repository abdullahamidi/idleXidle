# Tutorial interaction model — one acknowledgement grammar, presentation-only spotlights

**Date** 2026-09-21 · **Story** onboarding/tutorial interaction revision (playtest-driven)
**Verdict** PASS — six end-to-end opening runs, 2,538 unit tests, all static gates green.

---

## What the playtest said

The tutorial used visually identical spotlight-and-card treatments for several different demands.
Sometimes the player had to press CONTINUE, sometimes wait, sometimes click the highlighted
production control — and sometimes that highlighted control **glowed under the mouse while the
tutorial's own input swallow made it unpressable**. A control that lights up under the pointer looks
actionable whether or not it is.

## What changed

**EXPLAIN** — spotlight, short card, `PRESS ANY KEY OR CLICK TO CONTINUE`. No CONTINUE button. The
production UI underneath is presentation-only: no hover wash, no button glow, no press animation, no
tooltip, no hit-testing.

**DO** — the explanation is gone first; the card names the task and only the real production control
the deed needs is live. The real state mutation advances the step.

The acknowledging press is **spent**, so it can never also perform the deed underneath it.

---

## Evidence

### 1. The spotlight is no longer a lie — measured, at every density

`production/qa/evidence/visual-pass/focus/vault_tile_{explained,forced}_{100,125,150}.png`

The same lit VAULT rail tile, the cursor parked dead centre on it (canvas 90,539) in both frames.
`IntroduceChest` explains the tile; `ForceVault` makes it the one live control. Mean RGB inside the
tile:

| UI scale | explained (lit, inert) | forced (lit, live) | delta |
|---|---|---|---|
| 100 % | 22.4, 16.6, 14.6 | 34.1, 24.7, 16.4 | **+11.8 / +8.2 / +1.8** |
| 125 % | 25.7, 19.2, 16.5 | 36.9, 26.9, 18.2 | **+11.2 / +7.7 / +1.7** |
| 150 % | 27.0, 20.6, 17.7 | 38.0, 28.2, 19.5 | **+11.0 / +7.6 / +1.7** |

The gold hover wash appears **only** where pressing would do something. Flat where it would not.

### 2. The opening plays end to end — three densities, both input paths

`bash tools/check_opening_flow.sh <100|125|150> 90 "" <mouse|keys>`

A fresh career driven through all thirty beats on the real input path, then `check_opening_trace.py`
asserts the orders a screenshot cannot prove (GLEAM waits for the last fall; the Signature card
precedes its own cast; the gear chain picks, explains, equips).

| | mouse | keys |
|---|---|---|
| 100 % | PASS | PASS |
| 125 % | PASS | PASS |
| 150 % | PASS | PASS |

`every answered beat advanced on its first press` holds on all six — no dropped press, no double-fire.

The keyboard lane is new (`RH_OPENING_KEYS=1`). It answers explanations with **K**, a key bound to
nothing, so it proves the *any*-key rule rather than a key that already worked.

### 3. The new Training chapter

`build/shots/opening_flow/{100,125,150}_{mouse,keys}/` — every beat filmed.

`AwaitTraining` (live, waits for the screen to open **and** the purse to afford a rank) →
`IntroduceTraining` (explain, lights the TRAINING tile) → `ForceTraining` (navigate) →
`ForceTrainStat` (the rows lit, a real TRAIN purchase) → `ShowTrained` → back to the HUNT.

The lit region is the whole rows panel, deliberately: the deed is *make a choice*, and lighting one
row would be making it. The panel excludes RESET ALL TRAINING, so no destructive control is inside
the light.

### 4. Demoted lessons go quiet, not silent

`production/qa/evidence/visual-pass/focus/demoted_slot_{training,mastery}_{100,125,150}.png`

A demoted lesson keeps its title, its line and its × in the screen's own quiet slot, with the page
readable and fully interactive underneath — no scrim.

---

## Two defects found by this work

**The acknowledging key leaked into the game.** Found by the keyboard lane: the rig pressed `K` to
answer BACK TO THE HUNT and the game opened the VAULT on the same edge (`K` is the Vault's hotkey).
The cursor advances in the same `Update` the card is answered in, so by the time `_swallowInput` is
recomputed the opening can be over and the press is still live. `_openingAckSpent` now spends the
edge for **every** consumer — `Pressed`, `MouseClicked`, `ScreenKeys`, `HandleNavClick`,
`ForcedScreenClick` — not just the two forced mouse paths. Invisible on the mouse lane.

**The coach claimed the frame for a light it had stopped painting.** Found by photographing the
demotion instead of reasoning about it. The attention owner asked whether a lesson *resolved a
rectangle*, which a demoted lesson still does — so it took the frame, and `SlotShowing` stands the
slot down for anything above `Feedback`. The channel each lesson had just been moved into was closed
by the lesson itself: **seven lessons would have been silent rather than quiet**, with nothing on
screen to show for them. The owner now asks the same question the paint asks.

---

## Suites

- `dotnet build IdleXIdle.sln -warnaserror` — 0 warnings, 0 errors
- Core 1,838 passed · Game 700 passed · 0 failed
- `bash tools/check_all.sh` — all gates green, including `check_draw_purity` (ADR-006) and
  `check_mouse_space`

## Known limitation (pre-existing, not introduced)

Forced *deeds* remain mouse-only: the screens' keyboard is held while the opening owns their input,
so there is no keyboard path to a `ForceAction`/`ForceNavigate` control. The **acknowledgement** is
now fully input-equivalent, and the CONTINUE button this pass removed was itself a mouse-only target,
so the change strictly reduces mouse-only progression. Closing the forced-deed half needs per-screen
keyboard activation and is its own pass.
