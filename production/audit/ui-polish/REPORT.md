# FINAL UI POLISH — REPORT (brief §126, 56 items)

Status: DRAFT — filled as each phase closes. Sections marked `[pending]` are not yet done.
Commits are on `feat/hunter-cutout-rig`; checkpoints C1–C6 are listed in `PLAN.md`.

## Correctness

1. **Duplicate-skill rule implementation** — Core rule (LAW 13) in `PlayerLoadout`: `SetSkill(slot, id)`
   returns `bool` and refuses an id already in another slot (`IndexOfSkill` / `HasSkill`); `Build.Equip`
   refuses an id already worn; `ShareCodes.TryDecodeBuild` keeps the first copy of a duplicated id.
   UI copy: `ALREADY EQUIPPED IN SLOT N` on BUILD. Commit `5b0623d` (C1).
2. **Save migration** — `PlayerLoadout.Restore` resolves the saved slots, then clears any later slot whose
   id already resolved (the FIRST copy survives, the later slot becomes empty). Additive; no save
   version bump; tested in `tests/unit/IdleXIdle.Core.Tests/Builds/loadout_duplicate_skill_test.cs`.
3. **Duplicate progression guarantees** — SkillProgress is keyed by SkillId, so one id cannot level
   twice; with at most one slot per id there is no second progression path. Tests cover equip-refusal,
   restore-dedup and share-code dedup.
4. **Mouse transform changes** — one float transform in Core: `Core.Presentation.PageFrame`
   (Present / CanvasToScreen / ScreenToCanvas / PageToScreen / ScreenToPage, `Floor` toward −∞).
   `Game1.ReadCursor` maps once per frame into `PageCursor` / `PageMouseF` / `ChromeMouse` /
   `ChromeMouseF`; no screen converts or quantises; draw rect == hit rect. Gate
   `tools/check_mouse_space.py` rewritten (forbidden words, host hands the right cursor, no scaling of the
   cursor parameter). 21 PageFrame tests. Commit `dc261ae` (C2).
5. **Warren.Name decision** — deleted (orphaned; nothing read it). Commit `5b0623d`.
6. **Completed fight fixtures** — `fightstatus`, `fightfive`, `fightmulti`, `fightshieldbroken` plus
   `fightreport` ×3 limits (`RH_SHOT_LIMIT`), and the standard `fight` / `fightshield`. The rig gained an
   event-aimed seek (`HuntScreen.DevSeekBefore`, applied two frames before the shutter), `RH_SHOT_DUMP`
   event dumps, a pinned enemy baseline. Evidence `production/audit/ui-polish/evidence/p1-*.png`.
   Commit `1fda3a8` (C3).

## UI Scale

7. **How 100/125/150 differ** — a DENSITY profile, not a zoom: `UiMetrics` scales type and controls at
   the full factor and spacing at half rate; the page stays viewport-driven (`UiKit.Page` 1920×1080).
   ADR-005 `docs/architecture/ADR-005-ui-density-profile-and-one-cursor.md`.
8. **Central metrics/context introduced** — `UiMetrics` (Text / Control / Space / Gap / IconSize /
   InspectorWidth …), scale-aware `UiTypography` ladder, `UiKit.ScrollBar` / `Scrolled`,
   `UiKit.Button` states, `UiMotion`. Commit `645db62` (C4). Game test project
   `tests/unit/IdleXIdle.Game.Tests` (21 tests).
9. **Reflow/scroll changes** — eleven screens reflowed in parallel worktrees (`polish/<screen>`), merged
   `--no-ff` (HEAD `76724a3`): BUILD three scroll regions; GEAR three-column inventory + inspector
   scroll; FORGE bag/inspector scroll; TRAINING row scroll; VAULT two-column pages; MAP six cards
   refit; ROSTER two rows; WARREN eight cards; MASTERY / TRAITS canvases refit; HUNT card/strip/log.
10. **150 % fixes** — all eleven 150 % captures viewed (`build/shots/m150_*.png`); TRAITS foot printed
    over node names → foot band below the view (`4e4cb16`); chrome (hint slot, toasts, settings modal,
    nav rail) `[pending — chrome branch]`.
11. **Settings escape behavior** — `[pending — matrix run]`.
12. **Remaining scale limitations** — GEAR footer count shortens at 150 (`AVERAGE ITE…`); WARREN
    `BREEDING CHAMB…`; `[to complete after the matrix]`.

## Interaction

13. **hover** — `[pending — round two]`
14. **press** — `[pending — round two]`
15. **selected** — `[pending — round two]`
16. **disabled** — `[pending — round two]`
17. **focus** — `[pending — round two]`
18. **hitbox changes** — hit targets follow `UiMetrics.Control` at every profile; draw rect == hit rect
    (item 4); `[details pending]`.

## Motion

19. **screen transitions** — `[pending — host pass]`
20. **inspector transitions** — `[pending — round two]`
21. **number animations** — `[pending — round two]`
22. **reward feedback** — `[pending — round two]`
23. **Reduced Motion behavior** — `UiMotion.Reduced` collapses every Ease / Flash to the end state;
    `[validation pending]`.

## Screen Polish

24. HUNT — `[pending]`
25. BUILD — `[pending]`
26. GEAR — `[pending]`
27. TRAINING — `[pending]`
28. VAULT — `[pending]`
29. FORGE — `[pending]`
30. WARREN — `[pending]`
31. MAP — `[pending]`
32. TRAITS — foot band + point plate (`4e4cb16`); `[round two pending]`
33. ROSTER — `[pending]`
34. SETTINGS — `[pending — chrome]`
35. Expedition Log — `[pending — chrome]`

## Combat

36. **semantic VFX hierarchy** — `[pending — HUNT round two]`
37. **Shield HUD** — `[pending — HUNT round two]`
38. **Shield gain/absorb/break presentation** — `[pending — HUNT round two]`
39. **multi-hit readability** — fold `-N ×5` kept (fixture `fightmulti`); `[pending]`

## Audio

40. **sound families added/reused** — ten cues synthesised deterministically
    (`tools/asset-pipeline/make_sfx.py`, `make_battle_sfx.py`): sfx_nav, sfx_error, sfx_train,
    sfx_reroll, sfx_chest_open, sfx_chest_rare, sfx_shield_gain, sfx_shield_hit, sfx_salvage,
    sfx_shield_break; vocabulary in `assets/audio/README.md`. Commit `e654d84` (C5). Wiring
    `[pending — host pass]`.
41. **repetition testing** — SoundBank `MinGapMs` throttle per cue; `[validation pending]`.

## PixelLab / Assets

42. **existing assets reused** — the mana bar art as the shield bar (`ui_bar_shield_frame/fill` aliases);
    Source glyphs kept after audit.
43. **assets derived from existing art** — `[to list]`
44. **assets generated through PixelLab MCP** — `icon_shield`, six `icon_set_*` capstone emblems,
    `fx_shield_break_strip8_512`, `icon_status_defense` (replaced). Per-asset table `[to complete: path,
    purpose, where used, render size, why existing art was insufficient]`.
45. **assets regenerated/refined after in-game review** — `[to list]`
46. **unused generation attempts removed** — `tools/check_asset_consumers.py` (orphan audit)
    `[result pending]`.

## Accessibility

47. **setting combinations tested** — `[pending — Phase 8]`
48. **100/125/150 results** — `[pending — matrix]`
49. **color-independent states** — `[pending]`
50. **Reduced Motion validation** — `[pending]`

## Validation

51. **screenshot list** — `[pending]`
52. **deterministic fixture results** — `[pending]`
53. **hit-test results** — `[pending]`
54. **test results** — at `4e4cb16`: Core 1368/1368, Game 21/21; gates green; boot green.
55. **build results** — 0 errors at `4e4cb16`.
56. **performance observations** — `[pending]`
