# UX V2 audit — TRAINING (today: the STATS screen)

Audited 2026-09-01 against `production/audit/ux-v2/BRIEF.md` §0–15, §38–41, §82–102 and the current runtime
(STYLE → SKILL → VARIATION → REINFORCEMENTS; Form / WovenAbility / Aptitude / Source×Form deleted from Core;
mastery discovery permanent). Source read in full: `src/IdleXIdle.Game/StatsScreen.cs` (637 lines) and
`src/IdleXIdle.Core/Economy/HunterProgression.cs` (546 lines); plus the host wiring, fixture, nav, pills and
guide banner in `src/IdleXIdle.Game/Game1.cs`, the tour copy in `src/IdleXIdle.Core/Progression/Onboarding.cs`,
`Tutorial.cs`, `Unlocks.cs`, and the formulas the screen reads in `src/IdleXIdle.Core/Builds/SoloBattle.cs` /
`Build.cs`. Pixels: `baseline/stats.png` (1920×1080) and `baseline/720/stats.png` (1280×720), plus native-pixel
crops of the 720 file (rows, hunter card, reset bar, title band).

All coordinates are 1920×1080 logical. 720p sizes are rung × 0.667 (bilinear downscale, `DisplaySettings.PresentFit`):
36→24 · 30→20 · 26→17.3 · 24→16 · 21→14 · 19→12.7 · 16→10.7 · 14→9.3 px (cap height ≈ 0.7 of that).

---

## 0. Does the screen answer its question today?

Brief §100: TRAINING = **"What permanent/basic stat should I improve?"**

Half. It tells you what each stat IS RIGHT NOW (honestly — every number comes from the fight's own formula) and
what one rank COSTS, but it never says what one rank would DO, so the decision the screen exists for is made blind.
Nine identical rows in catalogue order give no grouping to reason with, the live effect is the smallest and
palest text in each row, and a third of the screen (HUNTER card + PROGRESS panel) answers questions that belong to
GEAR, MASTERY and MAP. The screen is also named for a by-product (STATS) rather than its verb (TRAINING).

---

## 1. Layout today (measured from code)

| Zone | Rect (1080) | Frame | Code |
|---|---|---|---|
| Nav rail (shared) | (0,0,180,1080) | — | Game1 `DrawHexNav`; tile `("STATS",'V',"state_resonance_128")` Game1.cs:5151 |
| Title + rule + subtitle | title y 24 (ScreenTitle 36 display); rule (700,74,520,3); subtitle y 80 Secondary | — | StatsScreen.cs:170-172 |
| HUNTER card | (56,150,372,480) | PanelQuiet | :85, :189-240 |
| PROGRESS panel | (56,670,372,290) | PanelQuiet (square frame, +28 drop) | :86, :524-555 |
| TRAINING panel | (472,150,1318,700) | **ornate `Panel`** (gold) | :87, :246 |
| 9 training rows | x 512..1750, 54 tall, pitch 62, first row y ≈ 234 | flat `RowBg` fill | :266-287 |
| Row columns | icon (524,·,40,40) · NAME x 580 Body · RANK x 772 Secondary · EFFECT x 942 Secondary Sky · TRAIN (1574,·,176,44) | — | :273-283 |
| RESET bar | (472,874,1318,106); button (1330,899,420,56) | flat fill + 2px top rule | :88, :465-476 |
| Hover card | 640 wide, flips at right edge, clamped y 150..1060−h | flat ground, gold top rule | :587-623 |
| Currency pills (shared) | Gleam / Dust / Scrap, x ≈ 1440–1826, y 16–76; Crystal only in the Scrap pill's hover | — | Game1.cs:4847-4894 |
| Guide banner (shared) | (470, 1080−h−26 ≈ 952, 980, ~102) | flat + gold left rule | Game1.cs:3109-3147 |

Ink budget: the TRAINING panel is 1318 wide but the longest effect string ends at x ≈ 1190 (`stats.png`) and
the button starts at 1574 — **≥ 380 px of dead space in every one of the nine rows**, ~3,400 px² × 9.
The left column is 372 wide and 810 tall for six values, two of which are printed twice (see P0-5).

---

## 2. P0 — readability / hierarchy

**P0-1. The row's most important value is its smallest, palest text.**
Each row prints NAME at Body 19 in Bone (:277) but the live effect — "YOUR BASIC ATTACK HITS FOR 96", the
number the player is deciding about — at Secondary 16 in `Sky` #7A9AC0 (:281), and the rank at Secondary 16 in
Slate (:278). `720/stats.png` crop: effect and rank ≈ 10.7 px font, ~7 px cap height; the label is legible, the
number is not. Brief §6 (Headline = the most important value inside a panel) is inverted.

**P0-2. Secondary 16 carries sentences everywhere on this screen, and at 720p that is 10.7 px.**
Subtitle "SPEND GLEAM TO GET STRONGER — YOU HAVE 18.7K" (:172); panel caption "EVERY ROW SHOWS THE REAL NUMBER
THE FIGHT USES · REST THE POINTER ON A ROW TO SEE ITS RULE" (:248-249); the reset explanation (:505/:510/:515);
every hover-card paragraph (:619, `lineH` 26); card lines "TEMPO 1.06x ACTION SPEED" (:227) and the HP figure
centred on the bar (:226). `720/stats.png`: all of these read as grey texture, not text. Brief §3/§6.

**P0-3. The permanent-loss warning is the least legible line on the screen.**
"COSTS 1 CRYSTAL — YOU HAVE 0. THE GLEAM YOU SPENT DOES NOT COME BACK." is Secondary 16 in Slate (:510, :515),
under a Body 19 label. `720/stats.png` reset crop: ~7 px caps. Brief §41 / §99-7: a destructive action explains
what will be lost — it does, in the smallest type on the page. (The armed state, :483-488, gets Body/Ember — good,
but the player reads the price BEFORE arming.)

**P0-4. The eye lands on chrome, not on a decision.**
`stats.png` first read: the gold ornate frame around TRAINING and the gold "TRAINING" / "HUNTER" / "PROGRESS" /
"STATS" titles (:170, :192, :247, :532). Nothing distinguishes one row from another; no row is selected; gold is
spent on frame and titles, not on a state (§10). The TRAINING panel wears the ornate `Panel` (:246), which the
project's own frame rule reserves for modals (UiKit.cs:557-561 "Every panel that lives IN a screen wears this
quiet brown"); and the content inside it is a stats list, which §9 classes QUIET.

**P0-5. The left column repeats itself and shows a bar that cannot move.**
HUNTER LEVEL is printed on the card (:215) and again in PROGRESS (:536); MASTERY POINTS on the card (:239) and
again in PROGRESS (:539). The life bar is drawn at `1f` with "180 / 180" (:219-226) — a full bar with the same
number twice, on a screen where life never changes. Brief §99-4 / §99-14.

**P0-6. The explanation lives in a 640 px hover document (§15).**
Every row's rule is three paragraphs in a hover card (:591-622; copy at :310-425), so understanding a stat means
holding the pointer still over it, and the card covers the rows beside it. There is no inspector and no click
selection. Brief §14/§15: hover = short glance, inspector = full explanation.

---

## 3. P1 — layout / UX

**P1-1. The screen is named for the by-product (§38).** Runtime responsibility is one purchase (`Hunter.Train`,
HunterProgression.cs:455-462) plus one respec; nothing on it is a read-only stat sheet that the HUNT hunter card
does not already show. Rename to TRAINING (Option A). Rename sites: StatsScreen.cs:170 (title); Game1.cs:5151
(nav label); Unlocks.cs:200 ("STATS — TRAIN YOUR CHAMPION"); Tutorial.cs:255 ("Press V for STATS"); Onboarding.cs:202
("Spend Gleam on the STATS screen"); Game1.cs:4882 (Gleam pill hover "BUYS UPGRADES ON STATS (V)"); Game1.cs:5059
and :5100 (help screen); class `StatsScreen` (:37) → `TrainingScreen`; `Activity.Stats` (Unlocks.cs:102/176/200,
Tutorial.cs:314, Onboarding.cs:220); fixture mode `"stats"` (Game1.cs:475, :1702; capture.sh:11). Hotkey: V is
free to keep; T is taken by the Weave (Game1.cs:2516-2520), so TRAINING keeps V or the help copy says so.

**P1-2. No grouping (§39).** Rows are in catalogue order MIGHT · RESONANCE · TEMPO · VITALITY · HEALTH · DEFENSE ·
CRITICAL · FOCUS · GUILE (:253-264). All four brief groups exist in the model, one to one:
OFFENSE = AttackPower/ResonanceAffinity/CriticalChance/Focus · SURVIVAL = Vitality/MaxHealth/Defense ·
TEMPO = Engineering · REWARDS = Guile (`HunterStat`, HunterProgression.cs:9-13). No category is invented.

**P1-3. No result before purchase (§40, §99-8).** A row states NOW and COST but never AFTER. Core has no preview
member: `ValueOf` (HunterProgression.cs:198-199) reads the live rank only; the constants that turn a rank into a
fight number (0.010 at :388, 0.006 at :434, 0.010 at :440) are private to the properties, so a screen cannot
compute AFTER without copying a formula — which is the drift the file's own comments forbid (StatsScreen.cs:26-28).
See §7 below for what exists and the one Core addition that closes the gap honestly.

**P1-4. Two TEMPO numbers on one screen can disagree.** The card prints `hunter.SquadSkillRate` (:227) — gear ×
training only. The TEMPO row prints `mods.SkillRate × shape.SkillRate` (:335) — the same term multiplied by
keystones (`Build.Resolve`, Build.cs:453-473) and the mastery shape. They agree in the fixture (1.06×) only
because no keystone or shape is seeded. Same shape as the health disagreement the file already fixed (:221-224).

**P1-5. Dead space per row.** Effect column starts at x 942 and the button at x 1574 (:281, :283); every row leaves
≥ 380 px empty (`stats.png`). The 1318-wide panel is wide for its content while the effect text is small for its
importance — the layout has room for a NOW → AFTER headline and a rank bar without growing the panel.

**P1-6. A VAULT lesson is drawn over the TRAINING screen (§12).** `stats.png`: "CHESTS ARE WHERE ITEMS COME FROM…
press K for the VAULT" (Tutorial.cs:240) sits at y ≈ 952–1054 over the reset bar's shadow. `DrawGuideBanner`
(Game1.cs:3109-3114) is screen-agnostic; only `_tourActive` and the title/help/settings modals suppress it. The
lesson that IS about this screen — `TutorialStep.SpendGleam` (Tutorial.cs:142, :255, :314) — is also drawn as this
global strip rather than as a contextual hint here. Any footer proposal at y > 950 collides with this strip until
§11 lands.

**P1-7. The reset's currency is invisible in the chrome.** The bar says "YOU HAVE 0" Crystal (:510); no pill shows
Crystal — it is listed only in the Scrap pill's hover tooltip (Game1.cs:4874-4877). Keep the exact count on the
bar (it is the only place the player sees it); do not also rely on the pill hover.

**P1-8. Three panels answer other screens' questions (§100).** GEAR POWER (:234-235) is GEAR's number; MASTERY POINTS
(:238-239, :539) is MASTERY's; CHESTS OPENED and HIGHEST WAVE (:537-538) are Vault/Map/Expedition Log facts
(HUNT already prints DEEPEST WAVE). None changes when you train. Remove from this screen or fold into a one-line
career strip; the Onboarding card "Your champion's level, life, gear power and mastery points" (Onboarding.cs:226-228)
changes with it.

**P1-9. Disabled reads like secondary (§7) — and the fixture never shows it.** Disabled button label is #7C7688
(UiKit.cs:892); the screen's secondary text is Slate #8A96A8 (:42). A row you cannot afford ("NEED 25 GLEAM",
:445) would read as "less important", not "unavailable". The `stats` fixture seeds 20,000 Gleam (Game1.cs:1708),
so no NEED or MAXED row has ever been photographed.

**P1-10. The reset needs the same words at two sizes.** The rest state explains the price at 10.7 px (P0-3); the
armed state repeats it at Body in Ember (:483-488) — correct per §85 (permanent cost → confirm) and §99-9, keep the
two-click arm and the 4 s auto-disarm (:135, :479). Fix the size, not the grammar.

**P1-11. No contextual teaching or NEW marker for this screen (§12).** `Hunter.CanTrain(stat)` (HunterProgression.cs:
451-452) already answers "can you afford a rank"; nothing surfaces it as a rail badge or a one-line hint on the
screen. The Vault badge pattern exists (Game1.cs:5441-5450) and could carry an "affordable" dot honestly.

---

## 4. P2 — polish

- **Stale doc comments contradict the runtime.** StatsScreen.cs:31-32 and :452-457 say the reset "returns 100% of
  the Gleam" via `Hunter.TrainingRefund`; that member does not exist (grep: only those two `<see cref>`s) and
  `ResetTraining` returns nothing (HunterProgression.cs:488-506). `ProgressionTuning` remarks :31-34 repeat the
  100% claim. Player-facing copy is correct; the comments are not.
- **Vocabulary drift:** the HEALTH card says "your promises" (:374); BUILD/WEAVE call them VOW (13 hits in
  WeaveScreen.cs). One word.
- **Per-frame allocations (§93):** the rows tuple array (:253-264), nine `string[]` cards with interpolation
  (:298-428) and the hover wrap lists (:593-594) are rebuilt every `Draw`. Cache per (stat, rank, gleam) change.
- **One glyph, two meanings:** the nav tile for STATS and the card's GEAR POWER both use `state_resonance_128`
  (Game1.cs:5151; StatsScreen.cs:233).
- **Internal names are pre-refactor but never reach the player:** `HunterStat.Engineering` (= TEMPO),
  `AttackPower` (= MIGHT), `ResonanceAffinity`; `SquadSkillRate` / `SquadDamageMultiplier` / `SquadHealthMultiplier`
  and the "the squad fights" remarks (HunterProgression.cs:295-298, :375, :398, :434) describe the deleted squad model.
- **Rank has no non-text form.** "RANK 12 OF 60" is a string; a 60-segment hairline bar under it would make
  progress readable at a glance (§8 is about state, but the same principle).
- **The subtitle repeats the pill** ("YOU HAVE 18.7K", :172, vs the Gleam pill at the same abbreviation, :79-81).
- **`ADEPT` line** (:228-229) prints the Style specialisation — current vocabulary, but a BUILD/MASTERY fact.

---

## 5. Keep (good, must survive the pass)

- ONE list of nine identical rows: icon · name · rank · live effect · priced TRAIN (:242-288). The row model is
  right; only its hierarchy and grouping are wrong.
- **Every number is the fight's own.** `Build.Resolve` once (:176-178), `SoloBattle.ChampionHealth` (:225, :350,
  :370), `SoloBattle.CritChance/CritMultiplier` (:393, :404), `SkillCatalogue.ResonancePerPoint` (:322),
  `SoloBattle.DefenseMitigationConstant` (:381), `mods.Haul` (:418). Never recomputed here. Extend, don't replace.
- The cost is on the button, changes every purchase (:430-449); MAXED comes from `Hunter.StatRankCap`
  (HunterProgression.cs:172-178), not inferred.
- Reset: three honest states (nothing trained / missing Crystal / ready, :503-521), two-click arm with red ground
  and auto-disarm (:479-499), validate-then-spend in Core (`ResetTraining`, :499-506). Matches §85 for a permanent cost.
- Request/consume — the screen never mutates the Hunter; the host trains, plays a cue and saves (StatsScreen.cs:
  105-131; Game1.cs:4104-4106). Immediate feedback exists (§86): click sound + the row's number changes.
- The plain-words copy in `Describe` with the player's own numbers filled in (:310-425), including the honest zero
  case for VITALITY (:357-363) and the LEVEL "a medal, not a stat" card (:566-571). Keep the text; move it.
- Fail-soft icons (:273-275); mouse-space conversion `Game1.ToOverlay` (:164); wide buttons 3-sliced (UiKit.cs:880-884).
- One health formula across three screens (:221-225).
- `Spotlights()` tour cut-outs keyed to the layout rects (:94-100) — keep the pattern when the rects move.

---

## 6. Proposed layout (1920×1080)

Assumes the global P0s land first (raised type ladder / UI scale; guide banner no longer persistent). No ornate
frame on this screen: the list is QUIET, the inspector is SECONDARY (PanelQuiet), gold marks the selected row,
the AFTER delta, and the hovered primary button only.

| Zone | Rect | Content |
|---|---|---|
| Title band (shared) | y 0–110 | "TRAINING" ScreenTitle; currency pills. **Remove** the subtitle sentence. |
| A. Champion strip | (200,118,1164,92) quiet translucent | Portrait 64 · `SEEKER · WANDERER` Headline · "LEVEL 7 · 31 RANKS TRAINED" Secondary. Right end: `GLEAM 18,742` Headline — the exact wallet (pills abbreviate, Game1.cs:4855). Hover on LEVEL keeps the medal card. |
| B. Grouped list | (200,226,1164,736) | 4 group captions (Secondary + hairline, 36 px each): OFFENSE · SURVIVAL · TEMPO · REWARDS. 9 rows × 64 px. Row: icon 44 @216 · NAME Body @276 · RANK `12/60` Secondary + 200×6 rank bar @476 · **NOW → AFTER** Headline @700 (`BASIC HIT 96 → 98`, arrow + delta in gold) · TRAIN 200×48 @1148 (`TRAIN · 108 GLEAM`; `NEED 108 GLEAM` disabled; `MAXED`). Click selects (gold 4 px left edge + lighter fill); hover = highlight only. |
| C. Inspector | (1384,118,512,844) PanelQuiet | §14 order: `OFFENSE · TRAINED STAT` (Secondary) → `MIGHT` (PanelTitle) → one-line identity (Body) → WHAT IT DOES (the three `Describe` paragraphs, Body) → NOW → AFTER block (PrimaryValue for AFTER) → contributions that are non-zero only: `+N FROM MASTERY` (`Hunter.MasteryBonus`), `+N FROM GEAR` (Defense/Crit affix totals) → `NEXT RANK · 108 GLEAM · RANK 13 OF 60` → `[TRAIN · 108 GLEAM]` 440×56. Default selection = first affordable stat, so the panel is never empty (§82). |
| D. Reset footer | (200,978,1696,78) quiet bar | Body: `RESET ALL TRAINING — 31 ranks to zero · costs 1 CRYSTAL (you have 0) · the Gleam does not come back`; button 320×52 right `RESET · 1 CRYSTAL`. Same two-click arm. **Depends on §11**: the guide strip occupies y ≈ 952–1054 (Game1.cs:3142-3147); until it stops being persistent, put the reset as the inspector's last block instead. |

Typography roles: NAME Body · RANK Secondary · NOW→AFTER Headline (row) / PrimaryValue (inspector) · group
captions Secondary · warnings Body, never Secondary. Nothing on this screen is Caption except the `MAXED` badge.

REMOVED: PROGRESS panel (4 rows) · GEAR POWER · duplicate MASTERY POINTS · the static 180/180 bar · the
subtitle · the panel caption ("EVERY ROW SHOWS…REST THE POINTER…") · the 640 px hover document · the ornate frame.
MOVED: stat explanation → inspector (click) · LEVEL medal card → hover on the strip's LEVEL · reset → footer (or
inspector foot) · champion identity → strip · the Stats tour cut-outs re-keyed to zones B/C/D.
RENAMED: STATS → TRAINING (nav, title, `Unlocks.Headline`, tutorial/help/pill copy, class, `Activity`, fixture mode) ·
"RANK n OF 60" → `n/60` + bar · effect strings become `LABEL now → after` (e.g. `BASIC HIT 96 → 98`,
`SKILL POWER +18% → +22%`, `ACTION SPEED 1.06× → 1.07×`, `REGEN 1/s → 2/s`, `LIFE 180 → 185`,
`DAMAGE TAKEN −9.1% → −10.7%`, `CRIT CHANCE 6.5% → 6.75%`, `CRIT DAMAGE 160% → 162%`, `LOOT +10% → +12%`).

Interaction (§15/§85): TRAIN is a costly-but-reversible-by-design purchase whose cost and result are on the row →
one click, no confirm. RESET is permanent-cost → arm + confirm (unchanged). Keyboard: ↑/↓ moves selection,
Enter trains selected, Esc disarms.

---

## 7. Data honesty (§92) — what the Core computes today

| Wanted | Status | Where |
|---|---|---|
| Current value of each stat | **EXISTS** | `Hunter.ValueOf` HunterProgression.cs:198-199 |
| Live fight number per row (NOW) | **EXISTS** | `Hunter.AutoDamageMultiplier` :388; `SkillCatalogue.ResonancePerPoint`; `Build.Resolve(...).SkillRate × SkillShape.SkillRate`; `Hunter.RegenPerSecond` :401; `SoloBattle.ChampionHealth` :1967; `SoloBattle.DefenseMitigationConstant` :286 + `Hunter.Defense` :234; `SoloBattle.CritChance` :1931; `SoloBattle.CritMultiplier` :1941; `Build.Resolve(...).Haul` |
| Cost of next rank / cap / MAXED | **EXISTS** | `Hunter.NextRankCost` :444, `CostOfRank` :446-449, `StatRankCap` :178, `CanTrain` :451 |
| Gain per rank (for "+2 MIGHT") | **EXISTS** | `Hunter.GainPerRank` :202 |
| **AFTER one rank (before → after)** | **NEEDS a Core preview member — pure arithmetic, no telemetry.** Today only RESONANCE, VITALITY and DEFENSE can be previewed from public constants (`ResonancePerPoint`, `RegenPerVitalityPoint` :404, `DefenseMitigationConstant`); MIGHT, TEMPO, HEALTH, CRITICAL, FOCUS, GUILE need the hunter at rank+1 because their constants are private (:388, :434, :440) or the formula takes a `Hunter` (`ChampionHealth`, `CritChance`, `CritMultiplier`). Proposal: `Hunter.WithRankTrained(HunterStat)` returning a copy with that rank +1 (share worn items and mastery dict; copy ranks), or a swap-measure-restore `Hunter.Preview(stat, Func<Hunter,T>)` exactly like `PowerContribution` does for gear (:283-293). Then AFTER = the same SoloBattle/Build calls on the preview hunter — one formula, no copy. Compute on selection/rank change, not per frame (§93). | HunterProgression.cs |
| Mastery's share of a stat | **EXISTS** | `Hunter.MasteryBonus` :196 |
| Gear's share (Defense, Crit) | **EXISTS** | `Hunter.AffixTotal` :354, `Gear.CharmDefenseBonus`/`CharmHealthBonus` via `Defense`/`MaxHealth` :234-236 |
| Total ranks / level | **EXISTS** | `TotalTrainedRanks` :477, `HunterLevel` :168 (cosmetic, documented) |
| Reset price, affordability, Crystal held | **EXISTS** | `TrainingResetCrystalCost` :480, `CanResetTraining` :485, `MaterialOf(Material.Crystal)` :114 |
| Gleam refunded by reset | **NONE by design** — say "does not come back" (already does) | `ResetTraining` :499-506 |
| "You can afford a rank" hint / badge | **EXISTS (derivable)** | any `CanTrain(stat)` true |
| Screen unlock progress | **EXISTS** | `Unlocks.IsOpen(Activity.Stats)` = `WavesCleared >= 1` Unlocks.cs:102; requirement copy :176 |
| Diagnosis / offline summary | N/A to this screen | — |

Nothing on TRAINING needs telemetry. The one gap is a preview member in `Hunter`.

---

## 8. Legacy vocabulary

In the TRAINING code itself: **no** Form / Weave / WovenAbility / Aptitude / Source×Form hits (grep of
StatsScreen.cs: 0). What is stale here is the screen's own name and the pre-solo internals:

- `"STATS"` as a screen name — StatsScreen.cs:170; Game1.cs:5151, :4882, :5059, :5100; Unlocks.cs:200;
  Tutorial.cs:255; Onboarding.cs:202. Class `StatsScreen` (:37), `Activity.Stats`, fixture `"stats"` (Game1.cs:475, :1702).
- `Hunter.TrainingRefund` cited but nonexistent; "returns 100% of the Gleam" — StatsScreen.cs:31-32, :452-457;
  HunterProgression.cs:31-34. Runtime refunds nothing (:490-492).
- "Squad" model in Core names/remarks — HunterProgression.cs:295-298, :375, :398, :434 (internal only).
- "your promises" for Vows — StatsScreen.cs:374.

Out of area, seen while reading the shared host (for the BUILD auditor): Game1.cs:478 `Activity.Build => "weave"`
fixture name; Game1.cs:2516 "T — THE WEAVE"; Game1.cs:2838-2839 comment "which Forms the build runs"; files
`src/IdleXIdle.Game/WeaveScreen.cs` and `FormHexDiagram.cs` still carry the deleted concepts in their class names.

---

## 9. Fixtures

Existing:
- `RH_SHOT_MODE=stats` (Game1.cs:1702-1715): opens the screen, seeds 20,000 Gleam, MIGHT 12, CRITICAL 6,
  DEFENSE 5, VITALITY 8, `_deepestEver = 23`, `_mastery.SetEarned(77400)`. No Crystal, no worn gear, no keystones,
  no mastery stat nodes.
- `RH_SHOT_ARM=reset` (StatsScreen.cs:148-150, :463) poses the armed red confirm.
- `capture.sh tour <out> Stats <1..3>` poses the three Stats tour cards over the same fixture (Onboarding.cs:220-232).
- `capture.sh stats` (capture.sh:11) drives the above; `baseline/720/stats.png` is an offline downscale of the
  1080 canvas.

Gaps against brief §95 "TRAINING (ranks + currency)" and this proposal:
- **No unaffordable row** — 20,000 Gleam covers every next rank, so `NEED n GLEAM` (disabled) is never posed. Seed
  ~120 Gleam in a variant, or train one stat to rank 30 (≈ 855 next) so one row reads NEED.
- **No MAXED row** — seed one stat at `StatRankCap` (60).
- **No RESET-enabled state** — the fixture holds 0 Crystal; add `AddMaterial(Material.Crystal, 1)` in a variant so
  the enabled `RESET — 1 CRYSTAL` button is photographed (only the dev-armed state exists today).
- **No divergence case** — a keystone or passive with a SkillRate/Health term, so the strip and the row show
  different TEMPO/LIFE numbers and the fix is checkable (P1-4).
- **No mastery stat bonus** — `SetMasteryStats` with a non-zero entry, so `+N FROM MASTERY` is posed.
- **No worn gear** — a Defense/Crit affix item so the gear share appears; the `character` fixture (Game1.cs:1690-1693)
  already builds such a set and could be reused.
- **No selected row / inspector state** — needs `RH_SHOT_SELECT=<stat>` (like Forge's `RH_SHOT_HOVER`).
- **No hover state** — the current hover card has never been captured; moot once it becomes an inspector.
- **VITALITY zero case** ("NOTHING YET — TRAIN IT TO REGAIN LIFE", :357) is never posed (fixture trains 8).
- **720p**: keep the downscale copy; add `RH_SHOT_SCALE=125` once UI Scale exists (§94).
- **Banner suppression**: the capture shows a Vault lesson over the screen; a `stats` capture should either pose
  the SpendGleam lesson (this screen's) or none.

---

## 10. Summary of REMOVED / MOVED / RENAMED

REMOVED: PROGRESS panel · GEAR POWER · duplicate HUNTER LEVEL / MASTERY POINTS · static full life bar · subtitle
"SPEND GLEAM…YOU HAVE" · panel caption manual text · 640 px hover document · ornate frame on the list.
MOVED: stat explanation → right inspector on click · reset → footer (or inspector foot while the banner persists)
· identity → champion strip · LEVEL card → strip hover · tour cut-outs → new zones.
RENAMED: STATS → TRAINING everywhere · rows gain `NOW → AFTER` headlines · `RANK n OF 60` → `n/60` + bar.
