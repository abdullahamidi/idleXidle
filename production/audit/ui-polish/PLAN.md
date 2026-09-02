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
| C3 | The owed fixtures: fightstatus · fightfive · fightmulti · fightshieldbroken · fightreport×3 limits; rig: event-aimed seek, dump, pinned baseline, page-space posed cursor | `1fda3a8` |
| C4 | UiMetrics + scale-aware ladder + Button states + UiMotion + ScrollBar; 150 % offered; Game test project (21 tests) | `645db62` |
| C5 | Ten synthesised cues (the §86 vocabulary); Shield art (icon_shield, the defence medallion, fx_shield_break, the mana bar reused as the shield bar); six capstone emblems; ADR-005 | `e654d84` |
| C6 | Per-screen density reflow — twelve branches `polish/<screen>` merged | — |

## Asset decisions (brief §89, §96–§97)

- **Source glyphs — kept.** The six are one generated family (the same ornate gold ring, six different
  gem shapes: BODY a red heater gem, MACHINE an orange spiked shield, MIND a cyan faceted crystal, NATURE
  a green crystal with antlers, SHADOW a violet crystal face, SPIRIT a pale crystal with tendrils). At the
  20–28 px they are drawn at, colour and the printed Source word beside them carry identity; the shapes
  differ but not enough to stand alone. Regenerating six good medallions for a distinction the label
  already makes is the "generate because PixelLab exists" the brief forbids.
- **Set capstone emblems — generated.** A fifth rung with a name and no picture had nothing to pulse and
  nothing a HUNT chip could show; six engraved bone-white emblems (the skill icons' style, tinted by
  Source at draw time) give the ladder's top rung, the completion toast and the "capstone live" chip one
  mark each. MOMENTUM's wrecking ball is the weakest read and is on the re-roll list if it fails in context.
- **Shield — one glyph, one medallion, one strip, one reuse.** See C5.

## Phase 3–5 — interaction, screen polish, motion — the per-screen brief (after C6 merges)

Shared already (C4): Button hover/pressed/disabled, UiMotion (Fast/Transition/Reward, Ease/Flash/Pulse),
ScrollBar. Each screen's second pass wires FEEDBACK, not information (§22):

- **HUNT** — Shield HUD on the reused bar art (`BarArt(…, "shield")`, thinner than health, `icon_shield`
  before the word); ShieldGained → sfx_shield_gain + a quick rim build; ShieldAbsorbed → sfx_shield_hit +
  a small impact at the barrier; ShieldBroken → `fx_shield_break` + sfx_shield_break, the loudest
  non-boss moment; keep the shell subtle. A Reaction's blow stops being labelled CRITICAL (crit is an
  expected value here) — it carries the skill's own name. Presentation priority §63; multi-hit fold
  stays; `_shieldSeen` resets per run. Skill strip states in words (already) — no constant flashing.
- **BUILD** — equip pulse on the slot (UiMotion.Flash Transition), inspector content fade on selection
  change, variation choice brightens its branch once, reinforcement pulses once, EQUIPPED · SLOT N badge
  (done), respec stays calm; sfx_error on ALREADY EQUIPPED; sfx_weave/sfx_bind as today.
- **GEAR** — equip: target slot pulses + Gear Power ticks (UiMotion) + ladder rung reveal; set ladder
  `● / ○` drawn as shapes (font gate), capstone rung with `icon_set_<capstone>` tinted by Source; first
  five-piece completion → host toast "MACHINE SET COMPLETE · PLATING ACTIVE" + sfx_levelup (once, via
  SaveGame.CompletedSets); empty cells quiet; ItemTooltip already reflowed.
- **FORGE** — before → after values tick to the new number and flash once (UiMotion), materials strip
  reacts to the spend, a brief forge flash on the item art; sfx_upgrade / sfx_reroll / sfx_gem /
  sfx_salvage per operation; no screen shake.
- **VAULT** — open: card responds at once, reveal stays short, rarity-scaled emphasis (the reveal's ring
  count already scales); sfx_chest_open (+ sfx_chest_rare Epic+); OPEN ALL aggregates (already);
  PASTE A CODE demoted to a plate at the far left (already) — confirm it never outranks OPEN.
- **TRAINING** — hover/selected rows (already distinct), on TRAIN: cost pill reacts, NOW → AFTER value
  ticks and flashes, rank bar eases, sfx_train; inspector updates without a modal.
- **TRAITS** — smooth road framing (UiMotion.Transition; Reduced Motion jumps), purchase pulse along the
  lit connection, terminal stronger; first-open framing kept.
- **MASTERY** — take: node pulses once, connection lights; no continuous tree animation (the
  unchosen-specialisation breath honours Reduced Motion).
- **MAP** — card hover, selected bar (already), a one-time reveal pulse on a newly available region.
- **ROSTER** — switch: card and inspector update, short highlight, no confirm (already), sfx_nav.
- **WARREN** — upgrade: level flashes, output ticks, cost pill reacts, milestone stronger; sfx_upgrade.
- **CHROME** — nav switch: 120 ms content fade-in + 8 px settle through OverlayTransform's kick (Reduced
  Motion: fade only), sfx_nav; modals (settings, log): backdrop fade + short panel fade; resource pills
  tick and flash on spend, `+N` on substantial gain (thresholded); locked tile → sfx_error.

## Host pass (Game1) — after polish/chrome and the eleven polish2/<screen> branches merge

The screens hand the host cues and notices; the host owns audio, toasts, transitions and the pills.
One branch `polish2/host`, Game1.cs (+ capture.sh docs), in this order:

1. Cues: read every screen's `ConsumeCue()` once per frame next to `_traits.ConsumeCue()` and play it;
   swap the host's own sounds where a screen now says what it means — TRAIN sfx_click → sfx_train,
   FORGE sfx_forge → the screen's cue (upgrade / reroll / gem / salvage), VAULT sfx_forge → chest cues,
   ROSTER switch sfx_click → sfx_nav, HUNT keeps its own SoundBank. Nav rail → sfx_nav; locked tile and
   every refusal → sfx_error.
2. GEAR set completion: `ConsumeNotice()` / `ConsumeCompletedSet()` → `SaveGame.CompletedSets` +
   the notice toast + sfx_levelup, once per set.
3. Motion: nav switch 120 ms content fade-in + 8 px settle through OverlayTransform's kick (Reduced:
   fade only); settings / log / help modals: backdrop fade + short panel fade; resource pills tick on
   spend and flash, `+N` on a substantial gain (thresholded); the notice / boot toasts use UiMotion.
4. Expedition Log (§71–§72) and Settings (§83–§85) per the brief — states, copy, scroll at 150.
5. `RH_SHOT_REDUCED=1` documented in capture.sh; Phase 8 combinations: {100,125,150} × {Reduced on/off}
   × {1280×720, 1920×1080} × {windowed, borderless} on BUILD and HUNT; Settings escape at each scale.
6. Re-run `tools/check_asset_consumers.py`: icon_shield and icon_set_* must report healed → drop them
   from the baseline.

## Phase 6 — audio — cues done, wiring with the screen polish

Ten cues exist (README lists the vocabulary). Wiring plan: nav rail → sfx_nav; refusals (locked tile,
BUILD locked, ALREADY EQUIPPED) → sfx_error; TRAIN → sfx_train (replaces sfx_click 0.8); RE-ROLL →
sfx_reroll (replaces sfx_forge); SALVAGE/SELL → sfx_salvage; VAULT open → sfx_chest_open (+ sfx_chest_rare
for Epic and up; replaces sfx_forge); HUNT ShieldGained → sfx_shield_gain, ShieldAbsorbed → sfx_shield_hit,
ShieldBroken → sfx_shield_break (replaces sfx_champ_down 0.30). UiKit.Button keeps no sound of its own —
each site says what it means.

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
