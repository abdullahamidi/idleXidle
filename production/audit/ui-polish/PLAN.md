# UI POLISH — working log

*The brief is `BRIEF.md`; the §126 report goes to `REPORT.md`. This file is the checkpoint trail and the
decisions that are not derivable from the diff.*

## Decisions

- **D1 — UI SCALE is a density profile, not a canvas zoom (brief §7–§11).** `UiKit.Page` stays 1920×1080
  and `Game1.OverlayScale` stays `BaseOverlayScale` at every profile. `UiMetrics` (Game assembly) owns
  TextScale / ControlScale / SpacingScale and the derived sizes; `UiTypography` rungs become scale-aware
  properties so every one of the ~930 rung uses scales without a call-site sweep. Spacing scales at half
  rate (1.125 / 1.25) so the page keeps room; text and controls at full rate (1.25 / 1.5).
- **D2 — One cursor (brief §12–§16).** `Core.Presentation.PageFrame` is the exact inverse of the draw
  matrices; `Game1.ReadCursor` maps the mouse once a frame; screens receive `PageCursor` (page space,
  floored once) or `ChromeMouse` (true 1920) and convert nothing. `check_mouse_space.py` enforces it.
- **D3 — LAW 13 lives in Core.** `PlayerLoadout.SetSkill` refuses a skill worn elsewhere; `Restore`
  clears a later duplicate to an EMPTY slot (positions kept); `Build.Equip` refuses a duplicate id;
  share codes show one copy. No save-version bump — the read path is lenient, like item dedup.
- **D4 — A fight pose aims at an event, not a second.** `HuntScreen.DevSeekBefore(pick)` rebuilds the
  wave's replay from its events and lands the seek two frames before the shutter, so the event is
  crossed live on the photographed frame. `RH_SHOT_DUMP=1` writes the wave beside the shot. Fight
  fixtures pin their enemy baseline through the host (`_shotEnemyBaseline`), because the host's
  per-frame push had been replacing every "deliberately beefy 1400" with the region's real baseline.
- **D5 — Probes before numbers.** Twice this pass a fixture number was "wrong" and the cause was the
  fixture (JAWS stopping the bite; the host's baseline push), found by measuring in Core first.

## Checkpoints

| | What | Commit |
|---|---|---|
| C1 | LAW 13: duplicate-skill invariant, restore migration, BUILD copy, Warren.Name deleted | `5b0623d` |
| C2 | One cursor: PageFrame (21 tests), ReadCursor, eleven screens swept, gate rewritten | `dc261ae` |
| C3 | The owed fixtures: fightstatus · fightfive · fightmulti · fightshieldbroken · fightreport×3 limits; rig: event-aimed seek, dump, pinned baseline, page-space posed cursor | — |

## Phase 1 — correctness — DONE at C3

- §2–§6 duplicate skills: Core rule + migration + UI copy + 13 tests.
- §12–§16 input: one float transform, floor once, 21 tests; posed-cursor capture at 1080 and a real
  1280×720 window lands on the same row.
- §19–§21 owed items: 150 % vertical pass → Phase 2; mouse quantisation → C2; three owed fight fixtures
  → C3 (`fightstatus`, `fightfive`, three `fightreport` limit seeds) plus the brief's §108 trio
  (`fight` standard, `fightshieldbroken`, `fightmulti`); `Warren.Name` → C1 (deleted).

## Phase 2 — UI scale — NEXT

1. `UiMetrics` + scale-aware `UiTypography` + `SmoothFont` weight thresholds + `UiKit` (Button label px,
   HoverTip width, Pill) + `DisplaySettings` offers 150.
2. Game1 chrome at density: nav rail tile, pills, settings modal (rows from metrics, scrolls when tall,
   close/escape always reachable), toasts, hint slot, tour card.
3. Per-screen density pass (parallel, one worktree per screen): vertical rhythm from available height,
   scroll where §18 allows, captures at 100/125/150.
4. Game test project for pure layout (`tests/unit/IdleXIdle.Game.Tests`): metrics, rung scaling, and
   every static page-anchored rect inside the page at all three profiles.
