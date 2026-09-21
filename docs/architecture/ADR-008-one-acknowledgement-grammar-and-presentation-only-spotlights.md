# ADR-008 — One acknowledgement grammar, and a spotlight that is never a lie

| Field | Value |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-21 |
| **Deciders** | user (product decision, three explicit choices) + ux-designer, technical-director |
| **Related** | ADR-006 (Update owns every edge; one edge, at most one action), ADR-007 (one surface at a time: the attention owner), ADR-005 (UI SCALE is a density profile, the cursor is mapped once) |
| **Enforced by** | `tests/unit/IdleXIdle.Game.Tests/opening_acknowledgement_test.cs`, `opening_training_chapter_test.cs`, `lesson_demotion_test.cs`, `opening_flow_seams_test.cs`, `attention_owner_test.cs`, `host_input_gates_test.cs`; `tests/unit/IdleXIdle.Core.Tests/Progression/opening_script_test.cs`, `onboarding_director_test.cs`; `tools/check_opening_flow.sh` (3 densities × 2 input lanes), `tools/check_opening_trace.py`, `tools/asset-pipeline/focus_fixtures.sh` |
| **Scope** | the authored opening, the contextual coach's presentation, and the host's pointer/edge ownership while either holds the frame. No gameplay arithmetic moves; `Unlocks` thresholds are unchanged. |

## Summary

The tutorial drew **one picture** — the same scrim, the same halo, the same card — for four
different demands, and distinguished them only by a line of footer text. Worse, while a card held
the frame the production control underneath went on hovering, glowing and lifting under a mouse that
could not press it. This ADR makes the grammar uniform (**any key or any click** ends every
explanation), makes the underlying UI **presentation-only** while an explanation is up, and spends
the acknowledging edge so it can never also perform the deed underneath.

## Context

### The problem

Playtest: a highlighted control that reacts to the pointer *looks* actionable. The player could not
tell, from the picture, whether a spotlight was a thing to press or a thing to read — and on an
explanatory beat it was neither, because the tutorial had already swallowed the click.

Three interaction paths shared one visual treatment:

| Beat | Looked like | Actually wanted |
|---|---|---|
| `PauseExplain` | spotlight + card + CONTINUE plate | a click *inside the plate*, or SPACE/ENTER |
| `ForceNavigate` / `ForceAction` | **the same** spotlight + card | a real click on the lit control |
| `LiveExplain` | the same card | nothing; it leaves on its own |

### The trap this walks past

**This exact design was shipped here and deliberately reverted.** The optional tour once advanced on
any click and any key, anywhere; the 2026-09-09 playtest was *"while in the tutorial, clicking
outside the screen also fast-forwards it"* — and, in the host's own words, *"every gold ring in the
intro was a button that did the wrong thing."*

That reversion was correct **for a design without the other half**. The failure was not
click-anywhere; it was click-anywhere *while the lit control was still live*, so pressing the thing
being explained skipped the explanation instead of using it. The two halves below are therefore one
decision, and shipping either alone re-opens the defect.

## Decision

### 1. One grammar for every explanation

An explanatory beat ends on **any key or any click**, and says so: `PRESS ANY KEY OR CLICK TO
CONTINUE`. The CONTINUE button is gone — it was the only thing distinguishing the two identical
pictures, and finding and parsing a footer is exactly the reading task this removes.

ESCAPE and the settings gear are taken first and unconditionally: they are the way *out*, and a
tutorial you cannot leave is a worse product than no tutorial. Bare modifiers are not an
acknowledgement — a hand reaching for Alt-Tab has read nothing.

### 2. EXPLAIN and DO are separate phases

The explanation is **gone** before the player is asked to act. A forced beat then presents the task
and makes exactly the real production control live. There is no tutorial-only button anywhere in
this design: the deed the player performs is the deed the game records.

### 3. Presentation-only underneath

While an explanation owns the frame, the screen gets **no pointer**. The mechanism already existed —
`ReadCursor` blanks `PageCursor` under a tour, described there as *"the mirror of the keyboard mute
(ScreenKeys)"* — and was simply never extended past tours. One term added to a gate that was already
there, so every hit-test each screen already does answers "not here" without a single screen learning
that a tutorial exists. `UiKit.MouseHeld` is gated with it, because the pressed face reads a global
the blanked cursor cannot reach; the rail and the envelope, which are chrome and keep a live
`ChromeMouse`, get their own explicit answer.

A `ForceAction` is the one beat that keeps the pointer, and keeps it **only inside the rectangle the
light is cut from and the click is tested against**. Same rectangle for the light, the hover and the
click, resolved once — so a control that hovers is a control that would answer.

### 4. The acknowledging edge is spent

ADR-006 §3 already binds this: *one edge, at most one action*. The cursor advances in the same
`Update` the card is answered in, and the beat it advances to may be a forced one whose control is
under the pointer right now — the VAULT tile beneath "A CHEST DROPPED", the EQUIP button beneath
"ITEM STATS". `_openingAckSpent` is the opening's `_modalOpenedNow`, and it is read by **every**
consumer of the frame's input, not only the forced paths.

### 5. Only a HardGuide darkens the page

`LessonMode` was dead classification: the host asked a purely geometric question, so Observe,
SoftGuide and HardGuide all rendered as the same full-page scrim. Twenty-four lessons declared three
loudnesses and the game had one. The presentation now reads `Mode`, and full focus is reserved for
critical first-use teaching — which leaves the two fall lessons and nothing else.

A demoted lesson goes **quiet, never silent**: it falls to the screen's own top-of-screen slot
(`SlotShowing`), or — for the one lesson about the chrome rather than a screen — to the HUNT's lesson
card. Both channels already existed. Three of the demotions were darkening the page to repeat what
`Onboarding.HintFor` already prints as a quiet banner.

### 6. Training is taught in the authored opening

Reverses the earlier decision to leave it out. The opening reached its first boss without ever asking
the player to make a decision, in a game whose premise is that you choose how your hunter grows — and
the chest and item that follow are things the game *gives*. A rank is the first thing the player
*spends*. The chapter forces the deed and never the choice: **TRAIN ANY STAT**, never TRAIN MIGHT,
and no step in the opening names a trainable stat.

### 7. Lesson eligibility asks the production unlock

`FirstMasterySpend` was eligible on `MasteryPointsFree >= 1` while `Unlocks` opens the tree on
`MasteryPointsEarned >= MasteryOpensAtPoints` — two different facts wearing similar names — so the
card pointed at a rail tile that was still chained shut. Eligibility now calls `Unlocks.IsOpen`, the
predicate navigation itself obeys. **The threshold is not repeated in onboarding code**, and a test
fails any future comparison of `MasteryPointsEarned` against a literal.

## Consequences

### Good

- The player never has to work out whether a spotlight is clickable: on an explanation nothing is,
  and the card says which hand to use.
- Keyboard and mouse are equivalent for every acknowledgement, and the rig proves both.
- The coach stops blacking out the screen for information that needs no decision.
- `LessonMode` became load-bearing, so demoting a lesson is a one-line data decision in Core.

### Costs and risks

- **Save migration.** `OpeningStage` persists by ordinal; the Training chapter inserts five stages
  before the tutorial boss, so `SaveGame.CurrentVersion` is 9 and `OpeningScript.StageOf(int, int)`
  composes two shifts, oldest first. This is the second mid-sequence insertion (the first earned v7)
  and the riskiest part of the change.
- **Forced deeds remain mouse-only.** Pre-existing: the screens' keyboard is held while the opening
  owns their input. The acknowledgement is now fully input-equivalent and the removed CONTINUE button
  was itself a mouse-only target, so mouse-only progression strictly decreased — but closing the
  other half needs per-screen keyboard activation and is its own pass.
- Source-substring tests pin several of these seams, so reformatting those lines fails tests on
  formatting rather than behaviour. Two were re-pinned in halves to reduce that.

## Two defects this pass exposed

Both were invisible to reasoning and found by exercising the thing:

1. **The acknowledging key leaked into the game.** The keyboard lane pressed `K` to answer the last
   card and the game opened the VAULT on the same edge. Guarding only the two forced mouse paths was
   not enough; the latch is now read by `Pressed`, `MouseClicked` and `ScreenKeys` as well.
2. **The coach claimed the frame for a light it had stopped painting.** The attention owner asked
   whether a lesson *resolved* a rectangle, not whether it would *paint* one — so a demoted lesson
   took the frame and `SlotShowing` stood the slot down for it. Seven lessons would have been silent
   rather than quiet. The owner now asks the same question the paint asks.

## Alternatives considered

- **Keep an explicit CONTINUE target, made uniform.** Closest to the old code and safest against the
  2026-09-09 reversion, but it keeps the reading task: the player still has to find a plate to learn
  whether the spotlight is live. Rejected by the user in favour of the uniform grammar, *with* the
  presentation-only half landing in the same changeset.
- **Demote lessons to DISPATCHES.** Rejected: there is no `DispatchKind` for a lesson (a schema
  change, not a copy change); a dispatch is permanently one-shot while a lesson's eligibility is
  live; and the DISPATCHES panel is itself a full-screen scrim, which defers the darkening rather
  than removing it. The slot is quiet, persistent-while-true, and already wired.
- **Append the Training stages after the enum to avoid the migration.** Rejected: it breaks the
  enum-order-equals-script-order invariant and makes the persisted cursor misleading.
