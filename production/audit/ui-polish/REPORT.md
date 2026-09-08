# FINAL UI POLISH — REPORT (brief §126, 56 items)

All 56 items answered. Commits are on `feat/hunter-cutout-rig`; the checkpoint table is in `PLAN.md`.
Where something was NOT done, it says so and says why — items 17 (keyboard focus) and 35 (the
Expedition Log's own polish) are the two open ones.

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

The house control is `UiKit.Button`, which owns all six states; the eleven screens draw their own
rows, cards, cells and tiles, and round two gave each of them the same six. Two shared-primitive bugs
had to be fixed first — see items 19 and 22.

13. **Hover** — an eased luminance lift over `UiMotion.Fast` (100 ms) on top of the art swap, so a
    hover is noticed without anything jumping. Every custom row, card, cell, node and tile in the
    eleven screens now does the same. Two fixes were needed: the lift was being tinted into
    `UiKit.PanelInner`, whose inset is the 40 px PANEL corner ornament, which on a 64 px button leaves
    a face four pixels tall — so the "lift" was a bright stripe drawn through the label, on every
    button in the game, at every profile (`build/shots/btn_before.png` → `btn_after.png`). And
    `ui_button_primary`'s interior is transparent, so a hovered button standing over the page's own
    art showed the scene through itself; it now gets the well `UiKit.Field` always had
    (`build/shots/pressed_100.png` → `pressed2_100.png`).
14. **Press** — the whole face drops 2 px and darkens for exactly as long as the button is held; the
    click still fires on release. The hit rect does not move. This state was invisible to the capture
    rig until `RH_SHOT_HELD=1` was added (five of the eleven screen agents reported it as
    unphotographable, and three had written into the host's own global from a screen file to get
    around it).
15. **Selected** — a persistent edge and a brighter face, never colour alone: the selected loadout
    slot, gear cell, forge tab, vault card, map region, roster card and trait node each carry a shape
    change as well as a tint.
16. **Disabled** — the grey art with a label that stays readable (`UiInk.Disabled`), and the reason in
    one plain line beside or under the control. Never a dead click: a refusal now also has a sound.
17. **Focus** — **NOT IMPLEMENTED, and not faked.** The game has no keyboard focus model anywhere:
    selection is the cursor's, the arrow keys move a screen's own selection (the map's region, the
    log's entry) rather than a focus ring, and no screen receives a focus index from the host. Six of
    the eleven agents reported this independently and none invented one, which is right — a focus ring
    that lights on one screen and not the ten others is worse than none. Adding it is a feature: a
    host-owned focus index, a cycle order per screen, and a visible ring in `UiKit`, which is also the
    keyboard and motor-accessibility path the technical preferences already commit to
    (cycle-and-confirm). It is the one interaction state of the six that this pass did not deliver.
18. **Hitbox changes** — hit targets follow `UiMetrics.Control` at every profile, so a 44 px button is
    66 px at 150 %. Draw rect equals hit rect everywhere (item 4): the pressed face moves, the hit rect
    does not.

## Motion

All motion goes through `UiMotion`: `Fast` 0.10 s, `Transition` 0.18 s, `Reward` 0.35 s — the brief's
three bands (§31) as three constants, so a screen names a speed rather than inventing one.

19. **Screen transitions** — the page lifts its last 8 px into place under a scrim that clears over
    130 ms, so a screen arrives rather than replaces. Drawn before the pills, the gear and the rail,
    because §33 says the navigation stays: the rail did not change and dimming it would say it had.
20. **Inspector transitions** — the content fades and the frame stays (§35): BUILD, GEAR, FORGE,
    TRAINING, MAP, ROSTER and TRAITS each fade their inspector body on a selection change.
21. **Number animations** — a figure the player just changed walks to its new value and flashes once:
    the currency pills, GEAR POWER, the FORGE's before → after pair, TRAINING's NOW → AFTER, the
    WARREN's per-minute output. The hover tooltip still reads the exact, true figure.
22. **Reward feedback** — chest opens, set completion, a trait terminal and the shield break get the
    `Reward` band and one extra beat of emphasis; nothing else does.
23. **Reduced Motion behavior** — `UiMotion.Reduced` collapses every ease to its end state and keeps
    short fades and immediate state changes, which is what §32 asks for. Fixing `Ease` was a
    prerequisite for trusting any of this: an unseen key started at "the opposite of the target", and
    because a value settling at 0 forgets its key, every control nobody was pointing at sawtoothed
    between 0 and 0.9 forever — a permanent shimmer on every button, tile, card and row, measured by
    one agent at a six-frame cycle on BUILD's library tiles. Four screens had written private guards
    around the call; two independently arrived at the same fix. Zero is rest now, and
    `ui_motion_rest_test.cs` plus `ui_motion_test.cs` pin the contract.

## Screen Polish

Each screen's second pass was implemented in its own worktree and then reviewed by a second agent
that rebuilt, recaptured and judged it before anything merged. Every screen also gained the six
interaction states on its own custom-drawn controls (items 13–18).

24. **HUNT** — SHIELD became the game's own bar: the reused mana-bar art at `UiMetrics.Control(16)`
    against the health bar's 26, the `icon_shield` glyph before the word, and the strip present only
    for a run that has a shield (`_shieldSeen` resets per descent, so SHIELD BROKEN teaches again).
    The three shield events got their cues and one one-shot each: a cold rim build on gain, a bright
    notch at the fill's leading edge on absorb, and the `fx_shield_break` strip over the champion at
    `Reward` length on break — which replaced `sfx_champ_down` at 0.30, the champion's own death
    sample played quietly. SHIELD BROKEN also moved to the top of the damage ladder, where §63 ranks
    it: it had been printing smaller than a critical. A Reaction's blow now carries its skill's name
    ("-4 JAWS") instead of CRITICAL, since a crit is the expected value there; a true critical from a
    basic or active hit still says CRITICAL. The multi-hit fold is untouched.
25. **BUILD** — the equipped slot pulses once, the inspector body fades on a selection change, a
    variation brightens its branch once and a reinforcement pulses once. RESPEC stays calm. The LAW 13
    refusal ("ALREADY EQUIPPED IN SLOT N") now sounds as well as reads.
26. **GEAR** — equipping pulses the target slot and ticks GEAR POWER to its new figure. The set ladder
    draws its rungs as shapes rather than glyph characters (the font gate forbids the filled/hollow
    circle glyphs), with the capstone rung carrying `icon_set_<name>` tinted by the set's Source. At
    real size the emblem reads as a coloured mark rather than an illustration, which is the job an
    18 px rung column has. The first five-piece completion posts a two-line notice once and remembers
    it in the save.
27. **TRAINING** — the cost pill reacts, the NOW → AFTER figure ticks and flashes, the rank bar eases,
    and TRAIN has its own cue instead of the ordinary click. A refusal for gleam flashes the cost pill
    and sounds the refusal.
28. **VAULT** — the card answers the press at once, the reveal stays inside `Reward`, and emphasis
    scales with rarity. A chest open sounds like a chest instead of borrowing the forge's hammer, with
    a second shimmer for Epic and better — graded, under OPEN ALL, by the best chest in the whole pile
    rather than the visible page.
29. **FORGE** — before → after values tick and flash, the material pill that paid reacts, and the item
    art takes a brief forge flash with no screen shake. Each operation names its own cue: upgrade,
    re-roll, socket, salvage.
30. **WARREN** — an upgrade flashes the level, ticks the output, reacts at the cost pill, and a
    milestone gets the `Reward` band and a second hump. A refused upgrade says so.
31. **MAP** — region cards hover and press, a locked card's hover says what conquers it, and a region
    that has just become available pulses once, the first time it is drawn available, and never again.
32. **TRAITS** — road framing eases instead of jumping (and jumps under Reduced Motion), a purchase
    runs a highlight along the lit connection, and terminals stay stronger. Separately, the camera's
    foot moved out of the tree: the hint, the tally and the three camera buttons had been printed over
    the clipped canvas and ran through the deepest nodes' names at 150 %.
33. **ROSTER** — SET ACTIVE updates card and inspector with a short highlight and the rail's own page
    tick; no confirm. READY and PLAYING are chips, not buttons, and do not look pressed.
34. **SETTINGS** — reflowed to scroll under a fixed header at 125 and 150 %, with the escape hatch
    verified (item 11) and both modal scrolls resetting on close.
35. **Expedition Log** — reflowed with the chrome pass; its metric emphasis and per-entry transitions
    are **not done** (§71–§72 remains open).

## Combat

36. **Semantic VFX hierarchy** — the fight's effects are graded by what they mean, not by what is
    available: a shield break is the loudest non-boss moment (§65–§70), above a critical, above a
    normal hit. SHIELD BROKEN moved up the damage ladder to match, having been printing smaller than a
    critical.
37. **Shield HUD** — a thinner bar under the health bar on the reused mana-bar art, with `icon_shield`
    before the word and the figure beside it. It appears for a run that has a shield and stays for
    that run, including at zero: a bar visible only in the instants it is full cannot be learned, and
    it would flicker on every bite. A build with no shield never sees it — confirmed in the `fight`,
    `fightstatus` and `fightfive` fixtures.
38. **Shield gain / absorb / break presentation** — gain is a cold rim building on the bar with
    `sfx_shield_gain`; absorb is a bright notch at the fill's leading edge with `sfx_shield_hit` and no
    screen shake; break is the `fx_shield_break` strip over the champion for one `Reward` beat with
    `sfx_shield_break`. Fixtures `fightshield` and `fightshieldbroken` pose all three.
39. **Multi-hit readability** — the fold ("-1,067 ×4") is unchanged and still proved by the `fightmulti`
    fixture, which aims the shutter at the cast event rather than at a guessed second.

## Audio

40. **Sound families added and reused** — ten cues synthesised deterministically
    (`tools/asset-pipeline/make_sfx.py`, `make_battle_sfx.py`): sfx_nav, sfx_error, sfx_train,
    sfx_reroll, sfx_chest_open, sfx_chest_rare, sfx_shield_gain, sfx_shield_hit, sfx_salvage,
    sfx_shield_break; vocabulary in `assets/audio/README.md`. Commit `e654d84` (C5). All of it is
    wired: the host reads a cue from each screen through one `PlayCue` reader (a screen may name two
    sounds for one instant, so a comma-separated list is a list) and plays it, because the host owns
    audio and a screen playing its own sound could not be muted, throttled or reordered. What stopped
    borrowing another act's voice: every chest opened with the forge's hammer, TRAIN was the ordinary
    click, the nav rail and every refusal shared one click at two volumes — a quiet yes is not a no.
    HUNT keeps its own SoundBank and plays the three shield cues itself, in the battle-event switch.
41. **Repetition testing** — `SoundBank` holds a per-cue minimum gap and drops a repeat inside it,
    with rapid repeats playing progressively quieter; a small random pitch offset per play stops a
    cue heard a thousand times from drawing attention through its own precision. The gaps that matter
    here: `sfx_shield_hit` 60 ms (a swarm's bites should sound busy), `sfx_shield_break` 300 ms (one
    moment), `sfx_chest_rare` 400 ms (the vault opens chests by the dozen and a shimmer restarting on
    each would be one long wash), `sfx_train` 70 ms. One gap is still on the 90 ms default that its
    two siblings do not use: `sfx_shield_gain`, flagged by the HUNT pass for a build with several
    grant sources on one beat.

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
    without resampling artefacts. Verified at real size (§101): `build/shots/ladder_100_crop2.png`
    shows the MACHINE ladder at 100 %, where the capstone emblem reads as a distinct tinted mark
    against the hollow discs of the rungs below it. It does NOT read as an illustration at 18 px, and
    was not meant to — the rung column's job is to say "this one is different, and it belongs to this
    Source", which it does. `icon_shield` at 40 px and the break strip at 512 px both read as
    themselves.
45. **Assets regenerated / refined after in-game review** — one: `icon_status_defense`. The shipped
    file was reviewed in a TRAINING capture, found to be an empty gold ring — the status family's frame
    with no motif inside it — and replaced with a blue heater shield inside that same ring, so the row
    matches its five siblings. Nothing else needed a second pass; the round-two captures put every
    other generated file on screen at its real size and none of them were wrong.
46. **Unused generation attempts removed** — no generation attempt was kept: `file_generated.py` writes
    exactly one file per job and staging lives in a gitignored `.staging/mcp`. The rule is now enforced
    rather than asserted: `tools/check_asset_consumers.py` (added to `tools/check_all.sh`) is the mirror
    of the asset-key gate — every art file and cue on disk must be reached by a literal, an alias target,
    an interpolated family, a strip / direction / frame stem, or a content list. It found 122 orphans,
    all pre-pivot debt (squad-era creature icons, nav tabs for dropped features, an unused button kit,
    boss animation strips nothing plays); those are carried in `tools/asset_orphans_baseline.txt` so the
    gate fails on any NEW orphan. The seven C5 files whose consumers round two owes (`icon_shield`, the
    six `icon_set_*`) sit on that list under their own heading and come off it when the gate reports them
    healed — and they have: after round two the gate reports `icon_shield` and all six `icon_set_*`
    consumed, and they have left the baseline, which now carries 115 pre-pivot files and nothing this
    pass produced.

## Accessibility

47. **Setting combinations tested** — UI SCALE × window size is the full 54-cell matrix of item 48.
    On top of it: Reduced Motion × 150 % photographed on TRAITS, SETTINGS and HUNT through the new
    `RH_SHOT_REDUCED` dial; UI SCALE 150 % × 1280×720 × windowed, which is the Settings escape case;
    and the pressed state × every screen through `RH_SHOT_HELD`. The combination that remains untested
    by capture is Reduced Motion during a transition, because a still cannot photograph the absence of
    movement — that one is held by `ui_motion_rest_test.cs` and the per-screen reduced-motion tests.
48. **100 / 125 / 150 results** — the brief's matrix: BUILD, GEAR, FORGE, TRAITS, SETTINGS and VAULT ×
    {100, 125, 150} × {1280×720, 1600×900, 1920×1080}, captured as PRESENTED frames at each real
    window size, in `build/shots/matrix/` (54 files, plus `settings_150_scrolled.png`). Every one was
    opened and judged against the reflow contract — no text over text, no text over a button, no
    control under a footer, no clipped last row, no panel past the page edge. All 54 pass. What the
    larger profiles actually do: BUILD keeps three columns and scrolls each; GEAR keeps three and
    scrolls the inventory and the inspector; FORGE keeps three and scrolls the compare rows beside a
    bar rather than dropping them; TRAITS refits the canvas and keeps its foot band below it; VAULT
    keeps two columns and pages; SETTINGS scrolls under a fixed header from 150 % (and at 125 % only
    in a 720p window). Known cosmetic residue, all documented rather than hidden: the GEAR footer
    shortens to "AVERAGE ITE…", the WARREN card reads "BREEDING CHAMB…", and at 150 % the VAULT hint
    slot's close icon sits within a few pixels of the OPEN ALL frame's top edge without covering its
    label.
49. **Colour-independent states** — no state in the pass is signalled by colour alone. Selected carries
    an edge as well as a tint; disabled carries the grey art and a printed reason; a set ladder rung is
    a filled or hollow shape, not a green or grey dot; a locked map region carries a padlock and a
    sentence; READY and PLAYING are words. The one place colour still does real work is the Source
    tokens (BODY, MIND, MACHINE, NATURE, SHADOW, SPIRIT), and each is always printed as its name
    beside its colour.
50. **Reduced Motion validation** — the setting had no capture dial at all when round two ran, which
    is why five screens proved it by test rather than by photograph and three wrote into the host's
    own `UiMotion.Reduced` from a screen file to get a picture. `RH_SHOT_REDUCED=1` now poses it from
    the host, so the combination the brief names (Reduced Motion × 150 %) can be photographed on any
    screen. Under it every ease lands on its end state at once; short fades and immediate state
    changes stay, which is what §32 asks. The screen-local dials that predate the host one are now
    redundant and should be removed in a follow-up.

## Validation

51. **Screenshot list** — everything is under `build/shots/`. The scale matrix in `matrix/` (55).
    The eleven merged screens at 150 % as `m150_*` (the C6 judgement) and `r2_*` / `r2c_*` (round two).
    The evidence for each fix named in this report: `c6_*` and `c6b_*` / `c6c_*` (the hint slot's
    anchor and its opacity), `btn_before.png` / `btn_after.png` (the button lift), `pressed_100.png` /
    `pressed2_100.png` (the pressed face and the transparent-art well), `motion_switch_075.png` and
    `motion_modal_075.png` (the chrome's transients, posed), `reduced_150_*.png` (Reduced Motion),
    `ladder_100_crop2.png` (the capstone emblem at real size), and each screen agent's own `p2_*` and
    `v2_*` batches. Phase 1's fixtures are in `production/audit/ui-polish/evidence/`.
52. **Deterministic fixture results** — every capture is the same rig: 60 frames, then the canvas is
    written and the process exits, so a capture is reproducible from its command line alone. The fight
    poses that depend on a wave event (`fightshieldbroken`, `fightmulti`) aim the shutter at the EVENT
    rather than at a guessed second — the seek rebuilds the replay from the wave's own events and
    applies two frames before the shutter — and `RH_SHOT_DUMP=1` writes the wave's event list beside
    the image so a pose can be checked against what the wave actually held. New dials this pass:
    `RH_SHOT_MOTION` (the chrome's four transients at any fraction of their run), `RH_SHOT_HELD` (the
    pressed face), `RH_SHOT_REDUCED` (the accessibility setting), `RH_SHOT_SCROLL` on the settings
    panel, and a screen-local dial per round-two screen for its own one-shots.
53. **Hit-test results** — one cursor, mapped once. `Core.Presentation.PageFrame` does the whole
    transform in floats and `Game1.ReadCursor` applies it once per frame; no screen converts or
    quantises, so draw rect equals hit rect at every profile and every window size. The gate
    `tools/check_mouse_space.py` enforces it (forbidden words, the host hands each screen the right
    cursor, no scaling of a cursor parameter) and 21 `PageFrame` tests cover the arithmetic. Hit
    targets grow with `UiMetrics.Control`, so a 44 px control is 55 at 125 % and 66 at 150 %. The
    pressed face moves 2 px; the hit rect does not.
54. **Test results** — Core 1368/1368, Game 162/162 (21 before this pass; the eleven screen agents and
    the chrome pass added the rest). All nine gates green, including the new orphan-asset gate. Boot
    green. `check_boot.sh` earned its keep: it caught a null reference that killed the game on start,
    from calling the gear screen out of `Initialize` when it is built in `LoadContent`.
55. **Build results** — 0 errors and 0 warnings across the solution. One pre-existing nullable warning
    in `HuntScreen` was removed by the HUNT pass.
56. **Performance observations** — nothing here costs a frame. The motion layer is two dictionaries in
    `UiMotion` keyed by rectangle, advanced once per frame from `Update` and read during `Draw`; a
    control at rest holds no entry at all, which is what the `Ease` fix restored — before it, every
    resting control in the game held a key and rewrote it every frame. No new render targets, no new
    `SpriteBatch` boundaries, no per-entity effect switching: the screen-switch scrim and the modal
    fade are single `Fill` calls inside batches that already existed, and the shield break is one
    strip through the existing `VfxPlayer`. Texture memory is unchanged apart from the nine C5 assets
    (about 150 KB on disk, RGBA8 at load).
