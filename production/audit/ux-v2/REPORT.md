# UI/UX V2 — final report

*Brief §101. Written 2026-09-01, at the end of the pass, against what the game actually does — every
figure here was read out of the code or off a capture, not remembered.*

> **The instruction this pass was measured against (§102):** *the final game should look simpler than
> before while communicating more.* Where the two pulled against each other, the pass deleted the
> louder thing and kept the fact.

---

## 1. Global

### 1.1 The UX problems this pass found

Nine faults recurred on nearly every screen. They are listed in the order they cost the player the
most.

1. **The decision was quieter than the explanation.** Screen after screen set its explanatory prose at
   the readable rungs and its deciding numbers at the small ones: the FORGE's before → after lived four
   hundred pixels from the button that produced it at the smallest values on the screen; the ROSTER's
   hunter name was drawn at 11 physical pixels under a phrase at 9; the TRAINING row's live effect was
   the palest text in the row. The player was being told what a system *is* far more loudly than what
   their next click *does*.
2. **Screens answered other screens' questions.** TRAINING spent a third of its width on GEAR POWER,
   MASTERY POINTS, CHESTS OPENED and HIGHEST WAVE — four numbers owned by four other screens, none of
   which move when you train. The WARREN's loudest panel was five derived percentages and the formula
   behind them.
3. **The ornament was on the list.** VAULT, ROSTER, WARREN and FORGE all wrapped a grid of things to
   pick between in the ornate gold frame while the column that explains them wore the quiet brown, so
   the eye landed on filigree and found the explanation last.
4. **Facts were hidden behind hover.** The VAULT's whole chest dossier lived behind a 32 px glass on a
   0.4 s delay; TRAINING's stat rules lived in a 640 px card that covered the rows beside the one it
   explained. Both were the "huge hover tooltip containing full documentation" §15 rejects.
5. **A card could not answer its own question.** No WARREN card said whether it could be upgraded, so
   the only way to find out was to click all eight. No ROSTER card said how close a locked hunter was,
   though the progress was already in the screen's own hands.
6. **Fixed pixels against a page that moves.** Every screen subtracted from 1920×1080. At UI SCALE 125 %
   GEAR's inspector fell to 204 px, TRAINING drew two whole groups over its footer, and the VAULT's
   panel — a `static readonly Rectangle`, frozen at class load — hung 330 px off the edge of the page.
7. **Empty and locked states written in the disabled ink.** The VAULT's empty state was drawn in
   `UiInk.Rule`, a hairline colour, and had never been photographed because no fixture could empty the
   pile; the FORGE's empty bench and the RE-ROLL tab's below-Rare state were the same.
8. **Copy that named a subject without saying anything.** "SOURCE VS REGION", "EACH HAS A COST",
   "INSTANT UPGRADE · NO WAIT" — the last of which sat in the slot reserved for the reason you cannot
   act.
9. **States that shipped unlooked-at.** The slip branch, the at-cap state, the below-Rare locks, the
   worn tag, the empty vault, the first-conquest Warren: all had shipped copy and none had a fixture
   that could pose them. Every one of them was wrong in some way the first time it was photographed.

### 1.2 Typography

One ladder, in `UiTypography`, and a gate that refuses a bare number in a text-size or line-pitch slot.

| Rung | px | Used for |
|---|---|---|
| ScreenTitle | 38 | the screen's name |
| PrimaryValue | 32 | the loudest number on a panel (AFTER ONE RANK, a locked hunter's count) |
| PanelTitle | 28 | a panel's own name, a modal's title |
| Headline | 26 | the deciding value in a row or card; a card's name |
| NavigationLabel / ButtonText | 24 | anything you click |
| Body | 22 | sentences |
| Secondary | 19 | labels, category lines, captions |
| Caption | 16 | badges only — never a sentence |

`UiTypography.Pitch(rung)` = `rung × 13 ÷ 10` is the only legal line step. Combat callouts (34 / 40 / 46)
are a deliberate family beside the ladder, not on it.

**Physical floor.** At 1280×720 the canvas presents at ×0.5972, so Secondary reaches 11.3 px and Caption
9.6 px. Every sentence is therefore Body or larger, and Caption carries badges only — the rule that
removed the ROSTER's tier line, the VAULT's fragments and the FORGE's shrink-to-13-px item name.

### 1.3 UI Scale

`Game1.OverlayScale = BaseOverlayScale × UiScaleFactor`, applied from `RecomputePresent`, with
`UiKit.Page` as the one rectangle every screen lays out against: 1920×1080 at 100 %, 1536×864 at 125 %.
`SmoothFont.Density` rasterises glyphs at the page's scale, so text is sharp rather than stretched.

- **The setting shipped in P3.1.** It had worked since P0.5 and its only door was the F8 dev key.
- **AUTO** picks 125 % under a 1600 px present width.
- **150 % is deliberately not offered.** `Display.UiScaleSteps` is `{100, 125, 0}` and a saved 150 loads
  as 125. The evidence is `p34-150-stats.png` and `p34-150-gear.png`: the page is 1280×720 logical, and
  screens whose vertical rhythm assumes 1080 still overflow. A preference must never resolve to a state
  the game cannot draw. Re-offering it needs every vertical rhythm derived from the height that is
  there — the same work GEAR's slot column got in P3.4, applied to the rest.
- **`tools/check_page_anchors.py`** refuses canvas literals (1920/1080/960, near-right edges) in a
  converted screen's layout, and its whitelist grew with each pass: BUILD · MASTERY · TRAITS · GEAR ·
  VAULT · FORGE · ROSTER · TRAINING · WARREN. It caught four live instances the eye had missed,
  including a MASTERY ceremony pinned to 960.

### 1.4 Panel hierarchy

Three surfaces, three calls, so the tier is visible in the code:

- **PRIMARY** — `UiKit.Panel` (ornate). One per screen at most, on the column that carries the decision.
  Gold nine-slice is modals only.
- **SECONDARY** — `UiKit.PanelQuiet`. Containers and inspectors.
- **QUIET** — `UiKit.Plate`. Every list, grid, row, chip and strip.

What changed: the ornate frame moved off the ITEM column onto the FORGE column, off the VAULT's grid,
off the ROSTER's grid and off the WARREN's grid. Gold now means one of three things and nothing else —
selected, earned, or the primary action.

### 1.5 Inks

`UiInk` is the single palette. Primary E8DFC8 (14.6:1) · Secondary 8A96A8 (6.45:1, never below 6) ·
Accent F0A830 · Disabled 5A5664 (the only ink under 4:1, and never alone — always with a second cue) ·
Rule 3A3A44 (hairlines, never text) · Good 6EC87A · Danger D8483A · Empty (Secondary × 0.6, placeholders).
Private `new Color(...)` constants used as text were deleted from the VAULT (`Gem`), the FORGE (`Bloom`,
the Insight-era violet) and the WARREN (`Violet`).

### 1.6 Onboarding

The persistent guide strip is gone. Teaching happens in three channels: a one-line hint slot under the
screen's title, a lesson card that appears once beside the thing it names, and the per-screen tour.
Tour cards were re-authored wherever they described furniture that no longer exists — the VAULT's peek
glass, TRAINING's HUNTER card, the ROSTER's aptitude row, MAP's "depth", and every card that said
*champion*.

### 1.7 Accessibility

- **REDUCED MOTION** (P3.1) ships with its consumers: `UiKit.AnimSprite` — the one call every looping
  sprite in the game goes through — holds its strip on frame one, and the vault card stops growing under
  the pointer. A setting that changes nothing teaches the player that the options screen lies.
- Colour is never the only channel: the MAP's power gap is stated in words, the ROSTER's road is a word
  and a colour, rarity is a countable pip row, and every disabled control has a sentence saying why.
- Settings regrouped by purpose with a bordered DANGER ZONE (§36, §37); controls enlarged (§35).
- Contrast: every ink above 6:1 except `Disabled`, which never appears without a second cue.

### 1.8 Shared components

- `UiKit.Plate(rect, accent?, alpha)` — the QUIET tier, and the accent rule that marks selection.
- `UiKit.Page` / `PageRight` / `PageBottom` / `PageCenterX` — the page every screen anchors to.
- `UiInk` / `UiTypography.Pitch` — the palette and the line step.
- `UiKit.HoverTip` — one hover grammar, flipping inside the page by itself.
- `ButtonStyle.Primary` — exactly one lit button per screen state.
- `Hunter.Preview<T>` (new, Core) — the before → after for any trained stat, measured with the real
  formula rather than a copy of it. Four tests compare it against actually training the stat.
- `ToggleRow` / `SliderRow` now take their own geometry instead of a hidden fixed offset.

---

## 2. Per screen

### HUNT — "How is my build doing right now?"

- **Old:** a vertical skill rail, a status card of raw numbers, a fall banner that named no cause.
- **New:** a horizontal skill strip grouped ACTIVE / PASSIVE with each slot's readiness in words; a
  hunter card; a utility column; a fall plate that names the limit that stopped the run.
- **Main improvement:** the run's *cause of death* is now a stated fact (`RunReport.Limit` →
  ARMOUR / REACH / SUSTAIN / PACE), not something to infer from a health bar.
- **Removed/renamed:** the Form-era slot vocabulary; the persistent guide strip.
- **New states:** the fall plate; the welcome-back summary after a real absence.
- **Screens:** `p11-hunt-1080.png`, `p11-hunt-720real.png`, `p11-hunt-fall-1080.png`, `p32-fight-1080.png`.
- **Remaining:** the damage-callout grouping now folds same-instant blows on one creature (§21); a
  `fightstatus` fixture for the SHIELDED / UNDYING / CHARGE chips is still owed.

### EXPEDITION LOG — "Why did I stop progressing?"

- **Old:** a list of wave lines.
- **New:** a diagnosis layer above the log — the limit, the affix words that produced it, and the two
  doors (ADJUST BUILD · GEAR) in the footer.
- **Screens:** `p12-log-1080.png`, `p12-log-720real.png`.

### MAP — "Where should I hunt next?"

- **Old:** small nodes with a coloured state, an inspector with two reserved holes (~160 px kept empty
  for a checkpoint row and a corruption ladder that are usually absent), the conquest message drawn in
  the canvas's dead band, and a chart whose ladder put a second and third button on a panel allowed one.
- **New:** large node cards with the state in words (YOU ARE HERE / CONQUERED / AVAILABLE / LOCKED); a
  world strip inside the chart carrying the corruption ladder and the conquest message, which costs
  nothing when it has nothing to say; an inspector that flows.
- **Main improvement:** the power gap is stated — `635 BELOW THE RECOMMENDED POWER` — instead of being
  left to a colour.
- **Removed:** a difficulty word (no Core member maps a power gap to EASY or DEADLY), the retired word
  *depth*, and the ENTER THIS REGION button's name.
- **New states:** locked regions name their prerequisite ("CONQUER THE STILL ARCHIVE"); `mapdeep` and
  `maplocked` fixtures.
- **Screens:** `p21-map-1080.png`, `p21-mapdeep-1080.png`, `p21-maplocked-1080.png`, `p21-map-720real.png`.

### BUILD · MASTERY · TRAITS

- **Old:** BUILD composed a skill out of a Source and a Form; MASTERY and TRAITS centred their titles
  and ceremonies on the canvas's middle.
- **New:** BUILD offers the skills you know; MASTERY and TRAITS use the shared inspector, page-anchored,
  with a first-open camera on TRAITS and a two-step commit.
- **Screens:** `p14-build-*.png`, `p15-mastery-*.png`, `p16-traits-*.png`, `p17c-ceremony-*.png`.

### GEAR — "What am I wearing and is this item useful?"

- **Old:** four columns; the inspector declared with one fixed edge and one page-relative edge.
- **New:** three columns (~42 / 27 / 31); the decision first; quiet empty cells; `EQUIP HIGHEST POWER`
  named for what it compares.
- **Main improvement (P3.4):** the paper-doll's slot column derives its pitch from the room between the
  header and the footer. Written as a fixed 102 px box on a 130 px pitch it needed 520 px, which 1080 has
  and 864 does not — at 125 % the last two slots drew through the footer and out of the panel.
- **Screens:** `p17-gear-1080.png`, `p17b-gear-125.png`, `p34-125-gear.png`, `p34-720-gear.png`.

### VAULT — "What rewards are waiting for me?"

- **Old:** eighteen 270×210 cards; the dossier behind a 32 px glass on a 0.4 s delay in a 470 px tooltip;
  four identical ornate buttons with no primary; a first visit whose loudest element was a greyed-out
  `OPEN ALL (1)`.
- **New:** four 884×349 cards carrying the whole dossier; OPEN ALL is the one Primary and is never
  disabled (one chest reads OPEN THE CHEST); CHEST FILTER and TRADER secondary; PASTE A CODE a plate.
- **Main improvement:** the promise is readable without hovering, and a reserved consequence row states
  the AUTO-SELL / AUTO-MERGE traits *before* the click that would consume the drops.
- **Removed:** the peek glass, `DrawDossierTip`, `ChestDossier.FloorShort` / `RegionShort`,
  `GiftChestDef.CardPromise` / `CardContents`, and one tour card.
- **New states:** a real empty state with its drop rate derived from `ChestTuning.DropChance`, plus
  `vaultempty`, `vaultemptyfilter`, `vaultsell`, `vaultmany`.
- **Screens:** `p18-vault-1080.png`, `p18-vaultempty-1080.png`, `p18-vaultfirst-1080.png`,
  `p18-vaultmany-1080.png`, `p18-vaultsell-1080.png`, `p18-vault-125.png`, `p18-vault-720real.png`.

### FORGE — "What will happen if I modify this item?"

- **Old:** four near-equal ornate columns, one of them a 514 px WALLET holding five balances, a charts
  essay and a THE FOUR TABS legend; the before → after on the ITEM card, 400 px from its button, at the
  smallest values on the screen; RE-ROLL said only "a new random enchant".
- **New:** three columns plus a QUIET materials strip; the compare is the first thing in the forge
  column at Headline, directly above its button; RE-ROLL lists the four real candidates from
  `Enchantments.PoolFor` with each one's build verdict; SOCKET selects then commits.
- **Main improvement:** the numbers that decide the press sit beside it.
- **Removed:** `DrawWallet`, `DrawTransition`, `DrawGain`, `DrawWrapped`, `SourceTint`, the `Bloom`
  accent, and the CreatureCore / Material item names that no bag can hold.
- **New states:** at the cap both buttons are removed rather than greyed; `forgeempty`; `RH_SHOT_ITEM` /
  `RH_SHOT_FILTER` dials; `dev_rung` / `dev_cap` / `dev_low` and one worn piece.
- **Screens:** `p19-forge.png`, `p19-forge-reroll.png`, `p19-forge-socket.png`, `p19-forge-breakdown.png`,
  `p19-forge-cap.png`, `p19-forge-rung.png`, `p19-forgeempty.png`, `p19-forge-125.png`, `p19-forge-720.png`.

### TRAINING — "What permanent stat should I improve?"

- **Old:** called STATS; nine identical rows in catalogue order; no AFTER anywhere; a HUNTER card and a
  PROGRESS panel answering other screens' questions; a 640 px hover document.
- **New:** four groups named for what they change; NOW → AFTER on every row; the §6 inspector; the reset
  footer's warning at Body.
- **Main improvement:** `Hunter.Preview<T>` — Core measures one more rank with the real formula and
  restores it, so the screen can promise an AFTER it cannot get wrong. Four tests check the preview
  against actually training the stat.
- **Renamed:** STATS → TRAINING at every player-facing site; the host's hint stopped printing enum names
  ("YOU CAN TRAIN ATTACKPOWER") via the new `TrainingScreen.WordFor`.
- **New states:** `trainingpoor` (a MAXED row and a row you cannot afford), `trainingreset` (the enabled
  reset button, never photographed before), `RH_SHOT_SELECT`.
- **Screens:** `p23-training-1080.png`, `p23-trainingpoor.png`, `p23-trainingreset.png`,
  `p23-training-125.png`, `p23-training-720.png`.

### ROSTER — "Who should I play?"

- **Old:** the grid ornate, the inspector quiet; the hunter's name at 11 physical px under a phrase at
  9.6; conquest-gated cards said the single word LOCKED; the header promised "YOU KEEP … GEAR" on every
  visit, which over-promised.
- **New:** quiet grid, §6 inspector, cards carrying class badge · portrait · short name · innate · a
  status band that counts.
- **Main improvement:** the two facts that separate two hunters — the STARTING SKILL and the INNATE —
  are on the screen at all, for the first time; and every gate has a bar and a live count, conquest
  gates included.
- **Removed:** the SWITCH FREELY banner (its honest half moved beside the button, with the count of worn
  pieces a switch would shed), the disabled PLAYING / LOCKED button, `BECOME THEM`.
- **New states:** `rosterswitch` (the post-switch toast); three counters seeded in `rosterlocked`.
- **Screens:** `p22-roster.png`, `p22-rosterlocked.png`, `p22-rosterswitch.png`, `p22-roster-125.png`,
  `p22-roster-720.png`.

### WARREN — "What is my organization doing while I am away?"

- **Old:** a 388 px overview column and a 920 px WARREN BONUSES panel of five derived percentages plus
  the formula behind them; no card said whether it could be upgraded; the away earnings — computed by
  the host since the welcome panel shipped — were never shown.
- **New:** one QUIET summary strip (rank · the ceiling explained once · the four rates with bonuses in
  them · the away line), a quiet grid whose every card carries a chip, and the §6 inspector.
- **Main improvement:** "can I upgrade this one?" is answered without a click, three ways: UPGRADE
  READY, REACH WAVE n with a lock, or NEEDS n GLEAM · n DUST.
- **Removed:** `DrawBonuses`, the `Warren.Name` caption (leaving the Core property orphaned — registered),
  the milestone pips (replaced by the multiplier itself), "TAKE ANOTHER REGION" on all five locked cards.
- **New states:** `warrenready`, `warrenfresh` (the first-conquest Warren, never looked at).
- **Screens:** `p24-warren.png`, `p24-warrenready.png`, `p24-warrenfresh.png`, `p24-warren-125.png`,
  `p24-warren-720.png`.

### SETTINGS — "How should the game behave and present itself?"

- **Old:** nine unrelated rows in one column; half the panel empty; START A NEW GAME beside COPY
  FEEDBACK CODE as one of a pair of identical buttons; UI SCALE reachable only by a dev key.
- **New:** two columns grouped DISPLAY · AUDIO · CONTROLS | GAMEPLAY · ACCESSIBILITY, a UI SCALE row,
  REDUCED MOTION with real consumers, and a bordered DANGER ZONE.
- **Screens:** `p31-settings.png`, `p31-settingsopen.png`.

---

## 3. Legacy cleanup

| Term | State | Justification |
|---|---|---|
| `Form` (player copy) | **gone** | The Form/Source composition was deleted in the 2026-08-31 refactor; BUILD offers named skills. |
| `WovenAbility` | **gone** | Deleted with the same refactor. |
| `weave` / `woven` (player copy) | **gone** | The screen is BUILD; the verb is "choose". |
| `Source × Form` UI | **gone** | A skill's Source is its element; the FORGE says so in one chip. |
| `champion` (player copy) | **gone (P3.4)** | Swept from TRAINING's three stat paragraphs, the boot message, two settings tips, the item tooltip, the ROSTER unlock headline, the tutorial rung and four HUNT tour cards. The pinned test copy moved with it. |
| `STATS` (screen name) | **gone** | Renamed TRAINING at the rail, the title, the help sheet, the Gleam pill's hover, the unlock headline, the tutorial and the intro. |
| `depth` (player copy, MAP) | **gone** | Replaced by "wave", which is the unit the game counts in. |
| `WEAVER` (keystone), `ks_weaver` (trait) | **kept** | Proper nouns of live content, not architecture words: a keystone and the trait that unlocks it. Renaming them would rename content, not clean up terminology. |
| `Warren.Name` | **orphaned, registered** | Its writer and reader both went in P2.4. Left in Core rather than editing Core in a UI checkpoint; queued for the next Core sweep. |
| `ItemClasses.ChampionNames` | **kept (identifier only)** | An internal method name; the string it builds says "{names} CAN WEAR IT" and never prints the word. Renaming is a Core sweep. |
| `Activity.Stats` fixture mode `"stats"` | **kept** | The capture mode's name is the key for the baseline before-shots; `"training"` already aliases to it. |

---

## 4. Validation

| Check | Status |
|---|---|
| **Build** | `dotnet build src/IdleXIdle.Game` — 0 errors. |
| **Tests** | `dotnet test tests/unit/IdleXIdle.Core.Tests` — **1260 passed, 0 failed**. Four are new (`hunter_preview_test.cs`). |
| **Gates** (`tools/check_all.sh`) | all green: font coverage · font digits · **ui type** · **page anchors** · asset keys · init order · nav gates · mouse space. |
| **Boot** (`tools/check_boot.sh`) | green — 14 screens drawn, save read back. |
| **1080 captures** | every screen, plus every new state. `production/audit/ux-v2/evidence/` — 95 files. |
| **Real 720p** (`RH_SHOT_WINDOW=1280x720`) | HUNT · LOG · BUILD · GEAR · VAULT · FORGE · TRAINING · WARREN · ROSTER · MAP, all legible; no sentence below Secondary. |
| **125 %** | every passed screen verified; GEAR's slot column fixed in P3.4. |
| **150 %** | **still overflows — the step remains withdrawn.** Evidence `p34-150-stats.png`, `p34-150-gear.png`. |
| **Fixtures** | 13 added this pass: `vaultempty` · `vaultemptyfilter` · `vaultsell` · `vaultmany` · `mapdeep` · `maplocked` · `forgeempty` · `trainingpoor` · `trainingreset` · `rosterswitch` · `warrenready` · `warrenfresh` · `welcome`, plus the dials `RH_SHOT_ITEM` · `RH_SHOT_FILTER` · `RH_SHOT_SELECT` · `RH_SHOT_UISCALE` · `RH_SHOT_WINDOW`. |

### Still open, and owned

1. **150 % vertical pass** — every vertical rhythm must derive from the height that is there, as GEAR's
   slot column now does. Then the step can be re-offered.
2. **Mouse quantisation** — menu hit-tests still go through `ToOverlay(CanvasMouse)`, a 4 px grid. The
   fix is `ChromeMouse` at every entry point: one commit across eleven screens, not a screen pass.
3. **Fixtures owed** — `fightstatus`; a five-slot skill strip; three `fightreport` seeds for the
   ARMOUR / REACH / SUSTAIN limits.
4. **`Warren.Name`** — dead Core property, queued for a Core sweep.
5. **Decorative transitions** (crossfade, node pulse, equip movement) — deliberately not shipped. Reduced
   Motion landed with real consumers instead; adding motion for its own sake to a screen set this pass
   spent its length calming is the wrong trade. Recorded as not done rather than quietly dropped.

---

## 5. What the pass actually changed, in one line each

- The **decision** is now the loudest thing on every screen, and it is next to the button that commits it.
- Facts that were **behind a hover** are on the surface; explanations that were on the surface are behind
  a click.
- Every **list is quiet** and every screen has **one lit button**.
- Every screen **lays out to the page**, and a gate keeps it that way.
- Every state a screen can be in now has a **fixture that poses it** — which is how six of them were
  found to be wrong.
