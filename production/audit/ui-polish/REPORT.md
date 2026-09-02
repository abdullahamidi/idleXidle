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
10. **150 % fixes** — every 150 % capture was viewed, not just produced. Screens (`build/shots/m150_*.png`):
    ten passed; TRAITS printed its camera hint, tally and buttons over the tree's deepest node names, so
    the tree's clip now ends where a foot band begins, the band's height follows the rows the foot needs,
    and the canvas starts under the point plate (`4e4cb16`). Chrome (`000e8bd`, `d01581a`): the settings
    panel overflowed at 125 and 150 and now scrolls under a fixed header; the nav rail's eleven tiles fit
    1080 with ROSTER unclipped; the help sheet flows and scrolls; every toast, tour card and WELCOME BACK
    height is derived from its lines. The hint slot needed two fixes — its anchor was pinned to the page
    literal 80, which is the 100 % value of a band the screens derive (`24 + Pitch(ScreenTitle) + 1 +
    Space(6)`), so at 150 % it sat 27 px too high and across VAULT's, TRAITS' and MASTERY's subtitles;
    and being the one plate that sits on a screen's own content, `UiInk.Plate`'s 0xE0 alpha let that
    content read through it. Both fixed; 100 % is unchanged to the pixel.
11. **Settings escape behavior** — verified at the worst case: UI SCALE 150 % in a real 1280×720 window
    (`build/shots/c6_settings_150_720.png`). MODE, WINDOW SIZE and UI SCALE sit at the top of the panel
    and are clickable, the close icon and QUIT TO DESKTOP are reachable, and the rows scroll under a
    fixed header. A player who picks 150 % on a small screen can always get back to 100 %. The two
    modal scrolls reset when the modal closes, so a scrolled-away UI SCALE row cannot persist.
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
34. SETTINGS — reflowed to scroll at 125/150 with a fixed header; escape verified (item 11). `000e8bd`.
35. Expedition Log — modal reflowed with the chrome pass (`000e8bd`); `[states and copy pending the host pass]`.

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

The brief's order is reuse, then derive, then generate (§88–§101). Commit `e654d84` (C5) holds all of it;
`tools/asset-pipeline/file_generated.py` filed every generated image through the same knockout / trim /
centre gate the manifest assets went through.

42. **Existing assets reused** — `ui_bar_mana_frame` and `ui_bar_mana_fill` shipped with the UI pack and
    were never drawn (the game has no mana). They are the SHIELD bar, reached through the AssetLibrary
    aliases `ui_bar_shield_frame` / `ui_bar_shield_fill`: the health bar's own ornate frame in a cold
    blue, so the shield reads as the same kind of object as health without a second art style. The
    Source glyphs (`source_*`) were audited and kept as they are — all six exist and the vault's
    per-element chest glyph resolves them; nothing was regenerated.
43. **Assets derived from existing art** — none. Nothing in the owed set could be produced by recolouring
    or recomposing a shipped file: the shield family needed a shape the tree does not contain, and the
    set capstones needed six distinct motifs.
44. **Assets generated through PixelLab MCP**

    | File | Purpose | Where used | Source px | Drawn at (100 % / 150 %) | Why existing art was insufficient |
    |---|---|---|---|---|---|
    | `assets/art/UI/icons/icon_shield.png` | The SHIELD glyph — a heater shield in cold steel | HUNT shield bar label, the expedition log's shield row, the help sheet | 64² | `UiMetrics.IconSize` = 40 / 60 px | No shield glyph existed. The nearest shipped icon was `icon_status_defense`, which was an EMPTY gold ring (see below), and the armour item icons are equipment art, not a status glyph. |
    | `assets/art/Characters/Hunter/icons/status/icon_status_defense.png` | The TRAINING screen's DEFENSE row medallion | TRAINING stat row, stat inspector | 256² | ~40 / 60 px | The shipped file was an empty gold ring — the status family's frame with no motif inside it. Regenerated as a blue heater shield inside that same ring, so the row matches its five siblings. |
    | `assets/art/VFX/shield_break/fx_shield_break_strip8_512.png` | The shield breaking — a cracked dome bursting into shards | HUNT, played once on `ShieldBroken` | 8 × 512² | 512 px at the champion | `fx_shield` (the shipped aura) is a shield HOLDING, not a shield failing; playing it backwards reads as a shield forming. The break is the loudest non-boss moment in the brief's hierarchy (§65–§70) and needed its own strip. Whitened and feathered for additive tinting. |
    | `assets/art/UI/icons/sets/icon_set_momentum.png` | MOMENTUM set capstone emblem | GEAR set ladder, capstone rung | 192² | ladder rung, ~28 / 42 px | The set ladder's capstone rung had no art at all — the rung that ends a five-piece climb looked the same as the four below it. Authored in the skill icons' engraved bone-white style so all six tint by Source at draw time. |
    | `assets/art/UI/icons/sets/icon_set_plating.png` | PLATING (Machine) set capstone | same | 192² | same | same |
    | `assets/art/UI/icons/sets/icon_set_certainty.png` | CERTAINTY (Mind) set capstone | same | 192² | same | same |
    | `assets/art/UI/icons/sets/icon_set_overgrowth.png` | OVERGROWTH (Nature) set capstone | same | 192² | same | same |
    | `assets/art/UI/icons/sets/icon_set_afterimage.png` | AFTERIMAGE (Shadow) set capstone | same | 192² | same | same |
    | `assets/art/UI/icons/sets/icon_set_harmony.png` | HARMONY (Spirit) set capstone | same | 192² | same | same |

    Every source image is authored well above its draw size and rendered with `SamplerState.LinearClamp`
    (non-integer scaling is the house path since the 2026-07-29 pivot), so the profile ladder scales them
    without resampling artefacts. `[at-real-size verification pending — the round-two captures]`
45. **Assets regenerated/refined after in-game review** — `icon_status_defense` is itself a regeneration:
    the shipped file was reviewed in a TRAINING capture, found to be an empty ring, and replaced.
    `[further refinements pending the round-two captures]`
46. **Unused generation attempts removed** — no generation attempt was kept: `file_generated.py` writes
    exactly one file per job and staging lives in a gitignored `.staging/mcp`. The rule is now enforced
    rather than asserted: `tools/check_asset_consumers.py` (added to `tools/check_all.sh`) is the mirror
    of the asset-key gate — every art file and cue on disk must be reached by a literal, an alias target,
    an interpolated family, a strip / direction / frame stem, or a content list. It found 122 orphans,
    all pre-pivot debt (squad-era creature icons, nav tabs for dropped features, an unused button kit,
    boss animation strips nothing plays); those are carried in `tools/asset_orphans_baseline.txt` so the
    gate fails on any NEW orphan. The seven C5 files whose consumers round two owes (`icon_shield`, the
    six `icon_set_*`) sit on that list under their own heading and come off it when the gate reports them
    healed. `[healed-check pending round two]`

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
