# Quests

## 1. Overview

Two quests. Each gates one character, and neither can be finished by accident. A quest is an enum goal
plus a threshold, evaluated by a pure function over a flat progress snapshot — the same shape a Vow
demand takes over `WeaveContext`, for the same reasons: it is testable without a game, it can be
looked at, and it cannot quietly become a lambda nobody reads.

## 2. Player Fantasy

Not a checklist. Each quest is the game pointing at something the player has not tried yet and saying
*"do this once and you will understand it"* — and then handing them the character built around that
exact idea. THE FIRST VOW teaches the Vow system and pays out THE OATHBOUND, whose whole passive is
Vows. THE HOLLOW HUNT teaches that a conquered region is still worth descending, and pays out THE
QUIVER, who is built for long runs.

## 3. Detailed Rules

- `QuestCatalogue.All` is the catalogue. `QuestCatalogue.Satisfied(progress)` returns every quest the
  current snapshot completes; the host forwards them to `CharacterState.CompleteQuest`.
- **Progress is DERIVED**, per frame, exactly as the trees' points and the character unlocks are —
  with one exception, below. There is nothing to double-count across a reload.
- **THE FIRST VOW is LATCHED.** Every other goal reads a fact still true when you look at it; a run's
  Vow is gone the moment the run ends. The host increments `RunsWithVowKept` on the one frame the run
  ends, and that counter is saved.
- **Kept, not sworn.** The latch tests the Vow's demand against the same `WeaveContext` the simulation
  judged it by. A Vow pays nothing while its demand is unmet, and counting sworn-but-unmet Vows would
  hand THE OATHBOUND to a player who never engaged with the system it exists to reward.
- A completed quest is permanent.

### The quests

| Quest | Goal | Threshold | Unlocks |
|---|---|---|---|
| **THE HOLLOW HUNT** | depth in the Verdant Hollow | 20 | THE QUIVER |
| **THE FIRST VOW** | descents finished with a Vow's demand met | 1 | THE OATHBOUND |

Neither duplicates conquest, which already unlocks four characters on its own. THE HOLLOW HUNT asks
for depth 20 against the 7 a conquest needs.

## 4. Formulas

`Quest.Current(p)` selects one field of `QuestProgress` by `QuestGoal`; `IsDone` is
`Current >= Threshold`. `ProgressText` clamps at the threshold, so a finished quest reads "20 / 20"
rather than continuing to count.

## 5. Edge Cases

- **Depth in the wrong region** does not count — the goal names its region.
- **A quest already done** is not re-announced: the host checks `CharacterState.QuestDone` first.
- **A quest satisfied on the same frame its character is refreshed** unlocks that character
  immediately, because quests are evaluated before `CharacterState.Refresh`.
- **A save from before quests existed** loads with `RunsWithVowKept = 0`, which is correct: nothing
  was latched, so nothing is claimed.
- **A quest id on a character that no catalogue defines** is caught by test, not by a player finding
  an unobtainable character.

## 6. Dependencies

- `Characters.CharacterState` — the owner of which quests are done, and of the gate.
- `Encounters.World` — depth per region, conquest count.
- `Weaving.IsActive` / `SoloBattle.DescribeBuild` — the kept-Vow judgement.
- `ForgeScreen.ChestsOpened` — for the goal that exists but no quest currently uses.

## 7. Tuning Knobs

- Each quest's `Threshold`, and which region a depth quest names.
- Which character each quest gates (`CharacterUnlock.Quest`).

## 8. Acceptance Criteria

- [x] Every quest-gated character names a quest that exists.
- [x] Every quest gates something.
- [x] A depth quest counts only its own region.
- [x] THE FIRST VOW cannot be completed by any amount of derived progress.
- [x] Progress text clamps at the threshold.
- [x] Finishing a quest unlocks its character and only its character.
- [x] The roster shows live progress on a quest gate, not just its demand.
