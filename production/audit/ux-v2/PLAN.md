# UX V2 — implementation plan (from the audit of 2026-09-01)

Inputs: `BRIEF.md` · sixteen audit reports + `critic.md` in `audit/` · baseline captures in
`baseline/` (1080p) and `baseline/720/` (offline bilinear copies — estimates until P0.6 adds a real
backbuffer capture). Runtime truth: `main` at `210db97`.

The audit's headline: every screen fails 720p readability (page Body is 11.3 physical px; Caption
carries sentences), ornate frames flatten hierarchy (there is no QUIET tier — `PanelQuiet` is the
ornate frame at 70%), secondary and disabled inks share a luminance band, the onboarding strip sits on
every screen, and the BUILD/MASTERY screens still speak Form/Weave/Discipline. Per-screen auditors
each re-invented the shared grammar; the critic counted nine inspector rectangles, three section
vocabularies, four toast anchors. So the plan starts with the shared decisions, then the foundation,
then screens one checkpoint at a time.

## 1. Shared decisions (resolved here; the per-screen passes inherit them)

| # | Decision | Resolution |
|---|---|---|
| D1 | Overlay inset (`BaseOverlayScale` 0.8958) | KEEP at 100% UI scale; each screen pass re-authors against `UiKit.Page`; the inset disappears screen by screen, never globally. |
| D2 | Ladder + pitch sequencing | `UiTypography.Pitch(rung)` + a type-gate rule FIRST; convert hand-set pitches; THEN move the ladder to 38/32/28/26/24/22/19/16; weights derived (`SemiBoldFrom = NavigationLabel`, `BoldFrom = PrimaryValue`). |
| D3 | One inspector | Right column, overlay space: x = `Page.Right − 40 − 496`, y 112, w 496, h to `Page.Bottom − 40`. Sections in this order and these words: CATEGORY (Secondary) · NAME (Headline) · IDENTITY line (Body) · **WHAT IT DOES** · **YOU NEED FIRST** · **WHAT IT COSTS** / CURRENT STATE · refusal line · ONE primary CTA h68 at `Bottom − 92`, full content width. Inspector follows CLICK. |
| D4 | Hint slot + toast anchor | Menu screens: one hint line at canvas `(450, 86, 1200, 48)`; screens keep y 86–134 free. HUNT: the boot-toast slot under the header stack. ONE toast anchor per zone (menus: 960-centred under the title; HUNT: the boot slot). Mechanism: `Onboarding.HintFor(Activity, HintFacts)` in Core + the rail's gold NEW mark. The persistent guide strip is REMOVED; the three fight lessons stay on HUNT only. |
| D5 | Hover grammar | Hover = highlight + one-line tip. Click = select (feeds the inspector). Primary button = commit. **One exception, documented:** VAULT has no inspector — the owner's playtest asked for "only the boxes"; the chest card carries its own OPEN. |
| D6 | Defeat / log / welcome-back | Defeat plate = two lines, a click target into the log: `FELL AT WAVE n` / `MAIN LIMIT — <one Verdict sentence>`; no buttons over the arena (the fight restarts in 1.6 s). Doors (ADJUST BUILD · GEAR) live in the LOG footer. Welcome-back PANEL only when `credited ≥ 60 s`; the toast otherwise. ONE label: `MAIN LIMIT`. Core: `RunReport.MainLimit` (enum over the existing Verdict thresholds) — no new telemetry; BEST PERFORMER is omitted (needs telemetry). |
| D7 | Vocabulary | HUNTER (not champion) in copy · WAVE (never "depth") · POWER (one label) · STYLE (never discipline/attunement) · EQUIP/UNEQUIP (never weave) · INNATE for a character's passive · screen titles without the article: VAULT, FORGE, GEAR, TRAINING. |
| D8 | Class / fixture renames | Adopt `audit/global-legacy-terminology.md §7`: `WeaveScreen→LoadoutScreen`, `BuildScreen→MasteryScreen`, `FormHexDiagram→StyleAffinityDiagram`, `PrestigeScreen→TraitsScreen`, `CharacterScreen→GearScreen`, `ChestScreen→VaultScreen`, `StatsScreen→TrainingScreen`, `SoloExpeditionScreen→HuntScreen`; `Activity.Stats→Training` and `TutorialStep.WeaveBuild→ChooseBuild` with read aliases for the two persisted name lists; fixture modes renamed with old names kept as aliases one release. |
| D9 | Gold law | `Accent` = earned / selected / active / primary action. Never on requirement lines (those are Primary + a lock glyph) and never on section heads (Secondary). |
| D10 | Fixture contract | Add `RH_SHOT_WINDOW=WxH` (backbuffer capture after the present blit — the first real 720p photograph) and `RH_SHOT_UISCALE`; every fixture seeds facts that make the RAIL agree with the screen (`Unlocks` monotone on DeepestWave); new modes as each screen passes. |
| D11 | Brief corrections (recorded, applied) | The Warren has no services / targeting / automation in Core (retired 2026-08-24) — §77–81 are honoured as "what each facility PAYS and where that is spent", nothing invented. Vault empty-state copy says chests come from bosses and gifts (the true sources). §22/§24 collapse to one label. |
| D12 | Chest filter home | The keep-filter CONTROL moves to the VAULT toolbar (secondary); HUNT shows only `n CHESTS READY`. Chest-pile ownership (ForgeScreen) is not refactored in this pass. |

Palette (D-tokens): `UiKit.Ink` — `Primary E8DFC8` · `Secondary 8A96A8` (≥ 6:1, never lower) ·
`Accent F0A830` (fold `F0B24A`) · `Disabled 5A5664` (the only ink under 4:1, always with a second cue) ·
`Rule 3A3A44` · `Empty` = Secondary at 60% with an outlined shape. The fourteen private palettes are deleted.

## 2. Checkpoints (each: compile · tests · gates · captures at 1080 + 720 · inspect · commit)

### P0 — global foundation (brief §97 #1–6)
- **P0.1 Renames** (D8) — mechanical, zero visual change: classes, files, Game1 fields, fixture-mode aliases, tools (`check_mouse_space.py` class→file, `check_nav_gates.py` Activity names), persisted-name read aliases + tests. Screenshots must be pixel-identical to baseline.
- **P0.2 Legacy copy** — the exact-replacement table (`audit/global-legacy-terminology.md §8`, ~35 rows) + the help sheet; delete the dead MASTERY overview (`DrawSummary/DrawCore/DrawAuraCards`) so `MasteryScreen` is one screen.
- **P0.3 Ink tokens + QUIET tier** — `UiKit.Ink`; `UiKit.Plate(rect, accent?)` as the real quiet surface (dark translucent + 1px rule); `Button(style: Primary/Secondary)` using the existing art variants; delete private palettes and the six private word-wrap copies (use `WrapBig`).
- **P0.4 Pitch + gate** — `UiTypography.Pitch(rung) = rung × 13 / 10`; `check_ui_type.py` rule for `lineH/rowH/pitch = <n>` literals; convert the ~110 hand-set pitches.
- **P0.5 UI Scale mechanism** — `PageScale = BaseOverlayScale × s`, `UiKit.Page`/`PageRight/PageBottom`, `SmoothFont.Density`, `ToOverlay` from `ChromeMouse`, prefs `uiscale=`, `Auto = presentWidth < 1600 ? 125 : 100`, settings DISPLAY row — **shown only under `RH_DEV` until P1 screens pass their 125/150 captures** (no dropdown that lies). `check_mouse_space.py` entry-point detection updated in the same commit.
- **P0.6 The ladder moves** (D2) + real 720p capture (`RH_SHOT_WINDOW`) + `typespec` fixture; chrome ladder bump (nav 24, pills, title 38).
- **P0.7 Contextual teaching** (D4) — `Onboarding.HintFor` + `HintFacts` in Core with a table test; hint slot; rail NEW mark; guide strip removed; HUNT lesson card; intro card copy updated.
- **P0.8 UX Standard V2** — `assets/art/idlexidle_ux_screen_guide_standard.md` rewritten to §89's checklist from the decisions above (screens filled in as their passes land).

### P1 — main loop
- **P1.1 HUNT** — horizontal ACTIVE/PASSIVE skill strip at the bottom of the arena; compact hunter card (name · Lv · POWER · HP bar · status icons); right side reduced to IDLE / REWARDS / [OPEN VAULT]; HUD drawn in the page zone; damage aggregation for same-beat multi-hits; defeat plate (D6); frames demoted to plates.
- **P1.2 EXPEDITION LOG** — MAIN LIMIT diagnosis line above the numbers (`RunReport.MainLimit`); doors in the footer; keep SINCE YOUR LAST RUN HERE; fill the dead middle; 720-legible rows.
- **P1.3 WELCOME BACK** — panel when away ≥ 60 s: AWAY · HUNT (waves, falls, deepest, Gleam from `OfflineHunt.Result`) · WARREN (all four outputs from `WarrenYield`) · CONTINUE. Notables only where the model has them (none for offline skill levels/items — omitted honestly).

### P1 — build loop
- **P1.4 BUILD (LoadoutScreen)** — YOUR LOADOUT (ACTIVE/PASSIVE) | SKILLS by style with the variation fork (Source belongs to the variation) | inspector (D3) with the Vow validator block.
- **P1.5 MASTERY (MasteryScreen)** — canvas + compact points plate + inspector (D3); node kinds visually distinct; DISCOVERED persists after respec; path preview only if `MasteryTree` pathing supports it cleanly.
- **P1.6 TRAITS** — first-open camera on SPINE + road starts; road focus; inspector formalised (the D3 words came from here); capstone confirmation only for terminals.

### P1 — loot loop
- **P1.7 GEAR** — loadout column folded into the Equipped header; ~42/27/31 split (layout-tested); quiet empty cells; `SetBonusLadder`; EQUIP BEST → `EQUIP HIGHEST POWER` (the audit confirmed it compares `PowerRating` only).
- **P1.8 VAULT** — big chest cards with OPEN; OPEN ALL primary, TRADER secondary, PASTE CODE tertiary; keep-filter control in the toolbar (D12); real empty state; OPEN ALL consequence line.
- **P1.9 FORGE** — three columns + MATERIALS strip; YOUR CHARTS copy → tour/Help; BEFORE → AFTER prominent; per-tab honesty lines (what changes, cost, uncertainty, destruction).

### P2 — progression
- **P2.1 MAP** — inspector (D3) at readable rungs; locked regions discoverable with the requirement; CTA states RESUME HERE / HUNT HERE / requirement text; difficulty word only with a design-owned table + test.
- **P2.2 ROSTER** — simplified cards (class · portrait · name · status/progress); locked progress from `Quest.ProgressLine`; inspector shows STARTING SKILL and INNATE; SET ACTIVE with feedback, no confirmation.
- **P2.3 TRAINING (TrainingScreen)** — title/rail/Activity renamed; stats grouped by the real model (OFFENSE Might/Resonance/Critical/Focus · SURVIVAL Vitality/Health/Defense · TEMPO · REWARDS Guile); before → after per row via a pure `Hunter.Preview`; one primary per selected row.
- **P2.4 WARREN** — one summary plate (Warren level · facility cap · deepest wave · rates); facility cards say what they pay and where it is spent; capped cards say `REACH WAVE n`; locked cards say the conquest they need.

### P3 — polish
- **P3.1 Settings** — two-column regrouping (DISPLAY · AUDIO · GAMEPLAY · ACCESSIBILITY · CONTROLS) + DANGER ZONE; UI SCALE row un-gated; controls enlarged; Reduced Motion added WITH its first consumers.
- **P3.2 Transitions** — inspector crossfade, node pulse, equip movement, resource tick; all under Reduced Motion.
- **P3.3 Contextual help** — F1 sheet re-authored to the current model.
- **P3.4 Final responsive pass** — every screen at 720/1080/1440 through the real capture; the §101 deliverable written to `REPORT.md`.

## 3. Laws applied to every checkpoint
No per-frame allocations · Core never references MonoGame · every displayed value is computed by Core today
(the audits' data-honesty tables are the whitelist) · no generic widget engine — components are `UiKit`
methods or small static classes, extracted on their second real user · a screen is done when its 1080 and
real-720 captures have been looked at, not when the code compiles.

## 4. Defects register — found during P0, owned by a later checkpoint
Pre-existing (identical in the 2026-09-01 baselines), recorded here so the screen pass that owns them cannot miss them:
- **BUILD** — the `IN <REGION>` affinity block at the foot of YOUR LOADOUT draws three rows BELOW the panel's bottom frame (`LoadoutScreen` region block; baseline `weave.png` y≈870–920). → P1.4.
- **ItemTooltip** — `HeightFor` sums literal row heights that must mirror the draw's pitches by hand; the draw was left on its literals in P0.4 so the two could not drift apart before the pair is unified. → P1.7 (the inspector replaces the tooltip's measure/draw pair).
- **Page anchors** — every screen still centres titles at 960 and subtracts from 1920/1080; at UI SCALE 125%/150% the page overflows the canvas to the right and bottom (posed with `RH_SHOT_UISCALE=125`). → each P1/P2 screen pass adopts `UiKit.Page` / `PageRight` / `PageBottom` / `PageCenterX`; P3.4 verifies all three scales.
- **Mouse quantisation** — `ToOverlay` still takes the 480-space `CanvasMouse` (×4), so menu hit-tests land on a 4 px grid (audit P1-3). → fold into the first screen pass that rewrites its hit-testing; `ChromeMouse` is the full-resolution source.

Fixed in P0 and worth knowing: the GEAR compare plate's `EQUIPPED: <ELEMENT> <SLOT>` label collided with its right-aligned value when Secondary grew to 19 px (P0.6); it is a label beside a figure and now sits on Caption.
