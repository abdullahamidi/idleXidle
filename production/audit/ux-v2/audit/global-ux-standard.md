# UX V2 audit — GLOBAL: the UX standard vs the runtime, and the shared-component opportunity

Audited 2026-09-01 against `production/audit/ux-v2/BRIEF.md` (sections 0–15, 82–102; area sections 9–10,
14–15, 88–91) and the runtime on `feat/hunter-cutout-rig` (post-2026-08-31 refactor: STYLE → SKILL →
VARIATION → REINFORCEMENTS, `SkillId` identity; Form / WovenAbility / Aptitude / Source×Form deleted from
Core). Read-only on source. Pixels checked in `production/audit/ux-v2/baseline/*.png` (1080p) and
`baseline/720/*.png` (what a 1280×720 player sees).

Paths are relative to the repo root. `Game1.cs`, `UiKit.cs` etc. live in `src/IdleXIdle.Game/`.

---

## 0. Verdict in one paragraph

The standard at `assets/art/idlexidle_ux_screen_guide_standard.md` was written for a game with eight
screens, a Source+Form build, a "Dust" prestige screen and no Vault, Mastery, Roster or Trader; it
describes a process (reference image → spec → fixture → screenshot) that has already been run once for
every screen and says nothing about the two things the brief calls P0 — physical readability after the
downscale, and a real QUIET surface tier. The runtime has quietly grown the answers the standard lacks
(one typography ladder, one panel grid, a tinted "quiet" frame, per-screen tours, deterministic
fixtures for 40+ modes) but has ALSO grown eleven copies of the same header, eleven copies of the same
colour palette, six hand-rolled word-wrappers, eight toast dialects and five inspectors that agree on
the idea and disagree on every constant. The screens do not answer the brief's §100 questions today
because (a) at 720p most Secondary/Caption text is 8–10 px tall and blurred, (b) `PanelQuiet` is the
same filigree at 70 % brightness so nothing on a screen is actually quiet, and (c) the onboarding strip
sits over every screen's foot.

---

## 1. Facts the rest of this report relies on (verified)

| Fact | Evidence |
|---|---|
| Canvas is a fixed 1920×1080 render target, presented with `SamplerState.LinearClamp` into `_present` (aspect-fit) | `Game1.cs:4053` (`SetRenderTarget(_canvas)`), `:4076-4077`, `:3970-3971`; `DisplaySettings.cs:122` → `CanvasFit.PresentFit` |
| Text is rasterised at `logicalPx × Scale` INTO the canvas, never at device resolution | `SmoothFont.cs:96-99`, `:186` (`GetFont(Math.Max(6, logicalPx) * Scale)`), `:196` (drawn at `1/Scale`) |
| Every menu screen is drawn through an additional inset transform: `OverlayScale = (1920-180-20)/1920 = 0.8958`, translated by 180 | `Game1.cs:5323-5326`, `:4028-4036`, `:4091` (`BeginOverlayCanvas()`); HUNT and chrome draw at scale 1 (`:4086`, `:4110`, `:4118`) |
| The ladder: 36/30/26/24/21/19/16/14; weight follows size (≥21 SemiBold, ≥30 Bold) | `UiTypography.cs:52-182`; `SmoothFont.cs:85,92` |
| `check_ui_type.py` gates bare numeric sizes; opt-out `// ui-size-ok:` | `tools/check_ui_type.py:17-43`; wired in `tools/check_all.sh` |
| `Panel` (ornate, white or gold tint) and `PanelQuiet` are THE SAME nine-slice art; quiet = multiply by `#585262` | `UiKit.cs:533-534` (`Panel`), `:567` (`PanelQuiet`), `:570` (`QuietFrame`) |
| No dark-plate / thin-border primitive exists in `UiKit`; the only flat surfaces are `Fill`, `HoverTip` and `Scrim` | `UiKit.cs:479` (`Fill`), `:1168` (`HoverTip`), `:518` (`Scrim`) |
| Panel grid constants exist and are the house rule (TitleTop 22, CaptionTop 56, BodyTop 92, PadX 40 / 28, PadBottom 32, ModalTitleTop 44, SquareFrameDrop 28) | `UiTypography.cs:211-305`; applied via `UiKit.TitleTop/CaptionTop/BodyTop/ContentLeft/ContentRight` `UiKit.cs:603-634` |
| Nav rail: 11 tiles, 180 px wide, tile height 1080/11 ≈ 98 px, label at `NavigationLabel` | `Game1.cs:5142-5160` (table), `:5246-5250`, `:5305`, `:5341-5406` |
| Nav locked tile: hover shows `Unlocks.Requirement`, click toasts headline + requirement | `Game1.cs:5410-5412`, `:5260-5264` |
| Guide strip (bottom-centre, 980 wide) draws on EVERY screen while a `TutorialStep` is live | `Game1.cs:3109-3148`; copy `Tutorial.cs:235-275` |
| Screen tours exist per Activity (11), plus intro and the first-gem lesson | `Onboarding.cs:186-380`, `:420` |
| Deterministic fixtures: ~45 `RH_SHOT_MODE` values; `tour` resolves to the screen's own fixture | `Game1.cs:470-488`, `:1486-1491`; `tools/asset-pipeline/capture.sh` header |

### 1.1 Effective physical text height (the P0 the brief opens with)

Logical rung × overlay inset (menu screens only) × present scale. Values are em-box heights; x-height is
roughly half.

| Rung | logical | menu screen @1080 (×0.896) | menu screen @720 (×0.597) | HUNT / chrome @720 (×0.667) |
|---|---|---|---|---|
| ScreenTitle 36 | 36 | 32.3 | **21.5** | 24.0 |
| PrimaryValue 30 | 30 | 26.9 | **17.9** | 20.0 |
| PanelTitle 26 | 26 | 23.3 | **15.5** | 17.3 |
| Headline 24 | 24 | 21.5 | **14.3** | 16.0 |
| NavigationLabel 21 | 21 | 18.8 | **12.5** | 14.0 |
| Body 19 | 19 | 17.0 | **11.3** | 12.7 |
| Secondary 16 | 16 | 14.3 | **9.6** | 10.7 |
| Caption 14 | 14 | 12.5 | **8.4** | 9.3 |

Two consequences the standard does not mention:

1. **Menu screens are already 10.4 % smaller than the ladder says at 1080p** — `OverlayScale` is applied
   before any downscale (`Game1.cs:5323`). The ladder is honest only for HUNT and the chrome.
2. **At 720p no text is re-rasterised** — a 1080p glyph atlas is bilinear-shrunk by 0.667 (or 0.597), so
   the 9–11 px lines are also soft. A UI-Scale setting that only multiplies the rungs will still be blurred
   unless the atlas size (or the render target) follows the present scale. This is the architecture fact
   §4 of the brief asks about: the canvas architecture supports UI scale, but `SmoothFont.Scale`
   (`SmoothFont.cs:99`) has to learn the present ratio, or `_canvas` has to be allocated at backbuffer size.

Pixel evidence (720p captures):

- `720/forge.png` — bag rows "SHADOW BLADE · LEVEL 1" ≈ 9 px, material use lines ("upgrades · stats · the shop") ≈ 8 px, "YOU HOLD 12,600 SCRAP · 131.9M GLEAM" ≈ 8 px: all illegible without leaning in. The only lines that read are the four panel titles and the two big buttons.
- `720/character.png` — set ladder rungs in ITEM DETAIL ("Your MACHINE skills hit 8% harder.") ≈ 8 px; inventory footer "24 ITEMS / SORT: RARITY" ≈ 8 px; the header caption "THE SEEKER · WANDERER — WEARS WANDERER GEAR…" ≈ 8 px.
- `720/stats.png` — the row rule lines ("YOUR BASIC ATTACK HITS FOR 96") ≈ 9 px, the very thing §40 wants prominent.
- `720/warren.png` — facility card rates "+783 /min" ≈ 9 px; bonus strip footers "2% A LEVEL" ≈ 8 px.
- `720/map.png` — region inspector labels ("YOUR POWER", "ENEMY THEME", "DROPS") ≈ 8–9 px, while the panel has ~250 px of empty space above the button (brief §28 confirmed).
- `720/fight.png` — skill rail rule text ("FIRES 5 ARROWS AT RANDOM ENEMIES, WITH FEWER ENEMIES THEY ARE SPLIT BETWEEN") ≈ 8 px and wraps to two lines; the rail is 326 px of the 1230 px arena.
- `720/roster.png` — card tier line ("FIRST OF THE WANDERERS", Caption 14) ≈ 8 px.
- `720/dust.png` — every node label in the whole-tree view ≈ 7–8 px (world units, opted out of the gate).
- `720/runlog.png` — the diff rows under SINCE YOUR LAST RUN HERE ≈ 8 px; the headline "FELL AT WAVE 13" reads well.

What DOES read at 720p, and should be the model: screen titles, panel titles, PrimaryValue numbers
(GEAR POWER 397, +783 /min), button labels, the guide strip's title line, the nav rail labels, the
Expedition Log headline band.

---

## 2. The current standard: stale-assumption list

Line numbers refer to `assets/art/idlexidle_ux_screen_guide_standard.md`.

| § / line | What the standard says | Runtime truth | Action for V2 |
|---|---|---|---|
| Header, l.7-15 and §19 l.595-605 | Applies to 7 screens: Gear, Stats, Build, Forge, Warren, Map, **Dust** | 11 rail screens (`Game1.cs:5150-5159`): HUNT, GEAR, STATS, BUILD, MASTERY, VAULT, FORGE, WARREN, MAP, TRAITS, ROSTER — plus EXPEDITION LOG (modal, `SoloExpeditionScreen.cs:2072`), SETTINGS (`Game1.cs:4214`), HELP (`:5028`), TITLE (`:4913`), TRADER and PASTE-CODE popovers (`ChestScreen.cs:578,723`), WELCOME BACK toast (`Game1.cs:3826`) | Enumerate all 11 + 6 overlays. HUNT is "the screen the lessons came from" but has no section — it needs the most. |
| §1.3 l.68 | "Build: fixed Source + Form + Vow setup" | Build = 4 skill slots each with a chosen VARIATION (which carries the Source) and REINFORCEMENTS; Vow per slot; keystones. `WeaveScreen.cs`, `SkillProgress.cs:80-111` | Fixture text becomes "4 skills, one at a chosen variation, one reinforcement bought, one Vow met, one unmet" (brief §95). |
| §1.3 l.72 | "Dust: fixed Dust balance and upgrade list" | The screen is TRAITS, paid in TRAIT POINTS, not Dust (`PrestigeScreen.cs:13-22`; `Game1.cs:5183-5190` explicitly rejects Dust). Memory Dust is a Warren/Map currency (`WarrenScreen.cs:371`, `MapScreen.cs:143`). | Rename the screen entry; keep "Dust" only as a currency name. Capture mode is still `dust` (`Game1.cs:483`). |
| §3.2 l.123 | Approved fallback: "no Form glyph → documented temporary label fallback" | Form is deleted; every skill has its own glyph `icon_skill_{id}` (`WeaveScreen.cs:1534`; commit 3aaed9f "twelve skill glyphs"). | Delete. Replace with "no SKILL glyph → blocking" since the set is complete. |
| §2 l.86-94 | "Do not render into a lower-resolution intermediate target" | The 1920×1080 target is then downscaled to 720p with bilinear filtering (`Game1.cs:4076`). The standard has no readability target for the downscaled image. | Add a physical-readability section: minimum x-height at 720p, and the UI-scale contract. |
| §7 l.255-285 | PRIMARY / SECONDARY / QUIET panel hierarchy | Only two tiers exist in `UiKit` and both are the same ornate art (`UiKit.cs:533,567`). The QUIET description ("dark translucent surface, subtle border") matches nothing in the kit; screens hand-draw it with `Fill` (see §4 below). | Define QUIET as a real primitive with a name and a padding rule. |
| §8 l.289-304 | Provisional paddings 28/24, 22/20, 14/10 | Superseded: `PanelPadX 40`, `PanelPadNarrow 28`, `PanelPadBottom 32`, `WidePanelFrom 512` (`UiTypography.cs:275-305`) | Replace with the live constants and the aspect-chosen frame rule (`UiKit.PanelArtKey`, `UiKit.cs:581-587`). |
| §9 l.314-343 | The ladder (already revised 2026-08-28) and "body text at least 18 px" | Correct as LOGICAL sizes; silent on the 0.8958 overlay inset and on physical size. | Keep the ladder; add the physical table (§1.1) and a rule that Caption never carries a sentence (brief §6) — today it does on Roster cards (`RosterScreen.cs:246-247`) and Weave's variation cards. |
| §9.1 l.345-362 | "Every panel is the same nine-sliced frame" stated as the design | That IS the hierarchy problem the brief names (§9). | Reword: the ornate frame is for PRIMARY only; the grid constants apply to every tier. |
| §10.2 l.389-400 | Approved equipment slots: Head, Shoulder, Chest, Weapon, Neck, Ring, Trinket, Cloak | Real slots: Weapon, Charm, Focus, Helm, Chest, Gloves, Boots, Ring (`CharacterScreen.cs:33`) | Replace. |
| §14 l.477-497 | Canonical nav order: Hunt Gear Stats Build Forge Warren Map Dust (8) | Rail order: HUNT GEAR STATS BUILD MASTERY VAULT FORGE WARREN MAP TRAITS ROSTER (11), keys H C V B E K F A W P R (`Game1.cs:5150-5159`). Nav is a vertical LEFT rail, not a bottom bar. | Replace; document the locked-tile behaviour and the NEW / count badges (`Game1.cs:5415-5451`). |
| §16 l.522-536 | Every screen supports a debug bounds mode | Only `PrestigeScreen.DrawDebug` (`PrestigeScreen.cs:1379`) survives. | Either drop the requirement or make it one shared `UiKit.DebugBounds`. Not a V2 priority. |
| §18 l.557-591 | Completion response format | Fine as process; keep. | Keep, add "720p screenshot" as a required line (brief §96). |
| §19-20 l.595-642 | "Production order … Next screen — Gear" | Every screen has shipped. | Delete §19–20. |
| Whole document | No section on: empty states per screen, locked-state copy, selected/disabled states, overflow, keyboard, onboarding behaviour, accessibility, primary action, inspector structure | The brief's §89 list | V2 is organised per screen with that checklist (see §8 below). |
| Whole document | Vocabulary "Weave" as a noun for the loadout; "Form" | Runtime: STYLE / SKILL / VARIATION / REINFORCEMENT / VOW / KEYSTONE; the verb "weave" survives only in copy (see §6). | Add a vocabulary table to V2 and make it the reference for `ui-copy` review. |

What the standard gets RIGHT and V2 should carry forward verbatim or nearly: §3 dependency
classification (blocking / approved fallback / cosmetic); §4 runtime asset whitelist; §5 component
contract fields; §6 rendering modes (add `Plate` under PrimitiveSurface); §11 data honesty; §12 dynamic
layout rules (zero / one / many / long names / large numbers); §13 overlay policy; §15 fixture contract;
§17 acceptance metrics.

---

## 3. Global P0 findings

### P0-1 Readability collapses at 720p, and the overlay inset makes it worse than the ladder implies
Evidence: §1.1 table and the pixel list. Root causes: `OverlayScale` (`Game1.cs:5323`); rasterisation
inside the canvas (`SmoothFont.cs:186,196`); Secondary (16) used for prose at ≥ 40 sites (e.g.
`WarrenScreen.cs:241`, `MapScreen.cs:512-515`, `CharacterScreen.cs:1051`, `PrestigeScreen.cs:1268`,
`ForgeScreen.cs:2213`, guide strip body `Game1.cs:3131`). Fix shape (not a redesign): remove the 0.8958
inset by authoring menu screens in true 1920 space with the rail as a reserved 180 px column; make the
atlas follow present scale; move prose from Secondary to Body; introduce the UI-scale setting.

### P0-2 There is no QUIET tier — `PanelQuiet` is the ornate frame at 70 %
`UiKit.cs:567-570`. In `720/forge.png` the four columns read as four equal filigree boxes; in
`720/character.png` the ornate EQUIPPED panel is barely distinguishable from its three quiet neighbours;
`720/warren.png` shows four frames + eight cards + one strip = thirteen bordered rectangles on one screen.
The brief's QUIET ("dark translucent, subtle border, thin divider, hover highlight") exists only as ad-hoc
`Fill` plates: `Game1.cs:3124` (guide strip), `WarrenScreen.cs:258-260` (locked card),
`MapScreen.cs:440-442` (message band), `SoloExpeditionScreen.cs:2757` area (chest filter), Weave slot rows,
Vow rows, Stats train rows, Forge tab strip, Gear inventory cells. Fix: name it (`UiKit.Plate`) and demote
every non-focal `PanelQuiet` to it (see §4 for the per-screen list).

### P0-3 The onboarding strip sits on every screen
`Game1.cs:3109-3114` draws the live `TutorialStep` on any screen (only tours, title, help and settings
suppress it). Captures: `720/forge.png` carries "YOU HAVE GLEAM TO SPEND"; `720/character.png` "EVERY
FIFTH WAVE IS A BOSS"; `720/buildtree.png`, `720/warren.png`, `720/dust.png` all carry "YOUR CHAMPION
FIGHTS ON ITS OWN"; `720/map.png` "YOU HAVE GLEAM TO SPEND"; `720/stats.png` "CHESTS ARE WHERE ITEMS COME
FROM". None describes the screen it is on (brief §12). It occupies 980×(58+22n) px at the canvas foot and
overlaps the Weave screen's own status line (`WeaveScreen.cs:890` draws `_msg` at y=1016, under the strip).
Fix: the strip is HUNT-only; other screens get their `Onboarding.BannerFor` line (`Onboarding.cs:505`)
or nothing; the NEW tile mark already exists (`Game1.cs:5427-5434`).

### P0-4 Secondary text and disabled text share a colour family
`Slate #8A96A8` is the secondary ink on every screen (11 local copies, see §7.1) and is ALSO the colour of
"LOCKED" on Roster cards (`RosterScreen.cs:262`), Warren locked cards (`WarrenScreen.cs:263-264`), the Map
locked band (`MapScreen.cs:424`) and the disabled-button label is `#7C7688` (`UiKit.cs:892`) — a value the
eye cannot separate from Slate at 720p (`720/rosterlocked.png`: locked "LOCKED" and unlocked road labels
are the same grey). Fix: one `UiKit.Ink` set — Primary (Bone), Secondary (Slate), Disabled (a distinct
desaturated value + reduced alpha) — and locked states use text + glyph, not colour alone (brief §8).

### P0-5 Legacy vocabulary still reaches the player
Full list in §6. The live ones: the HELP sheet's "WOVEN SKILLS / SOURCE x FORM x VOW / FORM IS HOW YOU
FIGHT" (`Game1.cs:5041-5046`, visible in `720/help.png`), the ATTUNEMENT ceremony's "THE FAR FORMS HIT
SOFTER" (`BuildScreen.cs:1398-1400`, live via `:650`), the Mastery node card's "WEAVE IT ON THE BUILD
SCREEN" (`BuildScreen.cs:1519`), and the WEAVE screen's whole "weaving" register.

---

## 4. Frame inventory (the §9 evidence)

Call sites, not surfaces (a loop draws many surfaces from one site). Verified by grep over
`src/IdleXIdle.Game/*.cs`; the screenshot column is what the eye actually meets.

| Screen (class) | `Panel` ornate | `Panel(gold)` | `PanelQuiet` | Hand-drawn plates / cells inside | Bordered rectangles visible in the 720p capture |
|---|---|---|---|---|---|
| HUNT (`SoloExpeditionScreen`) | 1 — EXPEDITION LOG modal `:2111` (correct) | 0 | 5 — hunter card `:2307`, stage header `:2475`, right cards `:2585`, chest-filter popover `:2753`, skills rail `:3103` | enemy strip, chest-filter plate, welcome toast (`Game1.cs:3845`) | 6 framed + 2 plates + 4 skill medallions; the arena is boxed on three sides (`720/fight.png`) |
| GEAR (`CharacterScreen`) | 1 — EQUIPPED `:709` | 0 | 3 — LOADOUT `:544`, INVENTORY `:756`, ITEM DETAIL `:900` | 8 slot medallions, 16 inventory cells with rarity edge + lock, 4 tabs, 4 buttons | 4 frames + 8 + 16 + 8 ≈ 36 bordered shapes |
| STATS (`StatsScreen`) | 1 — TRAINING `:246` | 0 | 2 — HUNTER `:191`, PROGRESS `:526` | 9 train rows (plates) each with an ornate TRAIN button; reset bar plate + button | 3 frames + 10 ornate buttons |
| BUILD (`WeaveScreen`) | 1 — WHAT YOU ARE WEAVING `:925` | 0 | 3 — PickPanel `:1392`, VOWS `:1657`, and **one per variation card** `:1354` | 4 slot rows, 2 tabs, 3 reinforcement cells, keystone rows, vow rows, hover card | 3 frames + 2 card-frames + ~14 plates |
| MASTERY (`BuildScreen`, tree view) | 0 (overview `:808` is dormant — see §6) | 1 — ATTUNEMENT ceremony `:1383` (modal, correct) | 3 — points plate `:1136`, YOUR ATTUNEMENT hex `:1294`, node card `:1419` | tree nodes (medallion art) | 3 frames; the tree is the focal — good |
| VAULT (`ChestScreen`) | 1 — trader popover `:690` | 2 — trader `:578`, paste preview `:723` | 1 — grid frame `:379` | chest cards (plates with rarity bar) | 1 frame + up to 6 cards; 60 % of the panel is empty (`720/vault.png`) |
| FORGE (`ForgeScreen`) | 1 — SELECTED ITEM `:1501` | 0 | 5 — BAG `:1259`, ACTION `:1502`, MATERIALS `:2142`, reveal card `:2646`, haul panel `:2872` | 4 tabs (Field art), 5 ornate buttons, item medallion, feedback strip | 4 frames + 4 tabs + 5 buttons + medallion |
| WARREN (`WarrenScreen`) | 1 — facility grid `:248` | 0 | 3 — OVERVIEW `:183`, BONUSES `:296`, detail `:339` | 8 facility cards (plates with accent), milestone bar | 4 frames + 8 cards = 12 |
| MAP (`MapScreen`) | 1 — map canvas `:306` | 0 | 1 — region inspector `:449` | 6 region cards (plates), message band | 2 frames + 6 cards — the best hierarchy in the game |
| TRAITS (`PrestigeScreen`) | 0 | 0 | 2 — points plate `:580`, TRAIT DETAIL `:1242` | node name plates | 2 frames; tree focal — good |
| ROSTER (`RosterScreen`) | 1 — grid `:177` | 0 | 1 — WHO THEY ARE `:268` | 10 cards (plates with class header) | 2 frames + 10 cards — good |
| Chrome (`Game1`) | 2 — SETTINGS `:4361`, HELP `:5029` (modals, correct) | 1 — title menu selected `:4920` | 4 — notice toast `:3283`, boot toast `:3845`, settings dropdown list `:4651`, title menu `:4920` | guide strip, locked toast, currency pills (whole-texture stretch `UiKit.cs:836`), nav tiles | 3 pills + gear + nav + strip on every screen |
| **Total** | **12 live + 1 dormant** | **4** | **36** | | |

Reading of the table:

- The ornate `Panel` is already reserved for one focal surface per screen almost everywhere — the codebase
  did the §9 PRIMARY decision. What it did NOT do is make the other tier quiet: 36 `PanelQuiet` sites still
  wear filigree. The demotion list is therefore mechanical: every `PanelQuiet` that is a LIST, GRID,
  RESOURCE or METADATA surface becomes a plate — Forge BAG/MATERIALS, Gear LOADOUT/INVENTORY, Stats
  HUNTER/PROGRESS, Warren OVERVIEW/BONUSES, Hunt right cards/rail/hunter card, Weave variation cards, Vault
  grid frame. Inspectors (Map, Roster, Traits, Warren detail, Gear detail, Mastery node) become the
  SECONDARY tier: a thin bronze rule, not filigree.
- Two screens use the ornate frame on a CONTAINER of many cards (Roster grid `:177`, Warren grid `:248`,
  Vault trader). A frame around a grid says "look here" about nothing in particular; the card hover /
  selected state is the real emphasis. Candidates for plate.
- Every `Button` is ornate art with scrollwork ends (`UiKit.cs:873-912`). Nine of them in one STATS
  column (`720/stats.png`) are nine gold claims. The brief's §84 (one primary action) implies a
  secondary/ghost button style; today `Button` has only rest / hover / disabled.
- Gold as MEANING (brief §10) is mostly respected: `gold: true` is used for modals and the selected title
  item only. The exception is the label colour — panel titles are Gold on every panel (all 11 screens),
  which spends the "earned" colour on furniture.

---

## 5. Repeated presentation patterns (the §90 evidence)

### 5.1 Screen header (title + rule + caption sentence)
Same three lines hand-copied on ten live screens, two rule widths, one deviant:

| Screen | Title | Rule | Caption |
|---|---|---|---|
| MASTERY | `BuildScreen.cs:1125` | `:1126` (700,74,520,3) | `:1133` — doubles as the message row |
| BUILD | `WeaveScreen.cs:858` | `:859` (720,74,480,3) | `:860` + a SECOND caption at y=108 `:866/869` |
| GEAR | `CharacterScreen.cs:445` (uses `RegionTitle` alias) | — | left-aligned caption ≈ y 52 + a right-aligned one under the pills (`character.png`) |
| VAULT | `ChestScreen.cs:365` | (screenshot) | `:373` — the tally |
| FORGE | `ForgeScreen.cs:1227` | (screenshot) | "PICK AN ITEM IN YOUR BAG, THEN CHOOSE A TAB…" (permanent manual text, brief §55) |
| MAP | `MapScreen.cs:279` | | "EACH REGION IS ABOUT 62% TOUGHER…" |
| TRAITS | `PrestigeScreen.cs:530` | | `:541` |
| ROSTER | `RosterScreen.cs:158` | | "SWITCH FREELY — YOU KEEP…" |
| STATS | `StatsScreen.cs:170` | | `:172` |
| WARREN | `WarrenScreen.cs:114` | | "THE WARREN EARNS WHILE YOU ARE AWAY…" |
| (dormant) BUILD overview | `BuildScreen.cs:685` | `:686` | `:690` legacy copy |

Every caption is an explanatory sentence at Secondary (9.6 px at 720p) — the brief's §6 says Caption/
Secondary must not carry sentences and §55 says manuals belong in Help. A `ScreenHeader(title, hint?)`
with ONE geometry and a hint slot reserved for the screen's live status (Mastery already does this) has
10 real users.

### 5.2 Right-side inspector panels and their section order
| Screen | Rect (1920 space) | Order today | CTA |
|---|---|---|---|
| MAP `MapScreen.cs:447-585` | (1412,146,474,880) | name · one-line · art plate · YOUR POWER / RECOMMENDED · rule · ENEMY THEME + bias · modifier + blurb · GOAL · DROPS · locked line · button | Bottom-92, h68: RESUME HERE / ENTER THIS REGION (`:572`) |
| ROSTER `RosterScreen.cs:266-344` | (1250,144,630,718) | "WHO THEY ARE" · name · blurb · rule · ROAD · class + tier · class text · rule · ALWAYS ON + passive · rule · EARNED/LOCKED + quest progress · unlock text · `_msg` · button | Bottom-96, h68: BECOME THEM / PLAYING / LOCKED (`:337-339`) |
| TRAITS `PrestigeScreen.cs:1242-1371` | (1368,144,500,790) | "TRAIT DETAIL" · road badge + sentence · icon · name · state · rule · WHAT IT DOES · rule · WHAT IT COSTS · rule · YOU NEED FIRST (ticks) · permanence · reason · button | Bottom-92, h68: LEARN THIS TRAIT (`:1369`) |
| MASTERY node `BuildScreen.cs:1419-1547` | (1408,588,496,460) | colour bar · KIND + branch · label · special note · rule · COST · YOU HAVE · state line | **none** — click the node (`:1539-1546`) |
| WARREN `WarrenScreen.cs:339-394` | (1400,154,480,708) | name · LEVEL · rule · description · OUTPUT now/next · milestone bar · rule · COST TO UPGRADE rows · rule · cap line · button | Bottom-96, h72: UPGRADE / DEPTH LOCKED (`:390-392`) |
| GEAR `CharacterScreen.cs:900-1060` | (1442,120,454,890) | "ITEM DETAIL" · art plate · name · source·level / class · stat rows · comparison chip · trait / enchant · set ladder · buttons | EQUIP + LOCK side by side (`:1057-1059`); LOCK is a permanently disabled fallback |
| BUILD (Weave) `WeaveScreen.cs:1512-1600` | middle column (578,144,640,850) | tabs YOUR SKILLS / THIS SLOT · glyph + name + ACTIVE/PASSIVE · LEVEL n uses/next · 2 variation cards · n/3 · 3 reinforcement cells · RESPEC · hover card | none; hover card `:1379-1382` is the explanation surface |
| FORGE | column 2 (406,140,384,890) ornate | item card with before→after (`ForgeScreen.cs:1558`) | actions live in column 3 |
| STATS | hover card `StatsScreen.cs:573-585` | title + body lines under the pointer | TRAIN buttons per row |
| HUNT, VAULT | none | — | — |

Six panels agree on: PanelQuiet, `UiKit.TitleTop` header, `Dim` 2-px rules between sections, a Gold or
Slate Secondary section label, a full-width button at Bottom-92/96 of height 68/72. They disagree on
which label is gold, whether the name is a Headline or a PanelTitle, whether state is a line or a badge,
and where the refusal reason sits. `InspectorPanel` per brief §14 has **six** real users today (Map,
Roster, Traits, Warren, Gear, Mastery) — Mastery gains the missing TAKE button and Gear loses the dead
LOCK. The Weave hover-card and the Stats hover-card are the two places where the inspector grammar is
hover-driven (brief §15 violation): Weave's variation/reinforcement detail should live in the panel
under the cards, Stats' rule text should live in the row.

### 5.3 Requirement / locked lines (`RequirementView` candidates)
- Nav tile hover + click toast: `Game1.cs:5410-5412`, `:5263` — source `Unlocks.Requirement` (`Unlocks.cs:173-190`). Good; the model for everyone else.
- MAP: `MapScreen.cs:574` "CONQUER THE PREVIOUS REGION TO UNLOCK" — generic; the region name is available (`Regions`), brief §29 wants "CONQUER VERDANT HOLLOW FIRST". Card band `:421-424` bare "LOCKED".
- ROSTER: card `RosterScreen.cs:258-262` uses `Quest.ProgressLine` — the best locked state in the game (`720/rosterlocked.png`: "23 / 50 WAVES"); inspector `:328-335`; toast `:145`; text `:120-121`.
- WARREN: locked card `:263-264` "LOCKED / TAKE ANOTHER REGION" — the model knows the count (`Warren.UnlockedFacilityCount = 2 + ConqueredRegions`, `Warren.cs:207`) so "CONQUER 1 MORE REGION" is derivable; cap line `:386` "REACH DEPTH n ON AN EXPEDITION TO UPGRADE" — exactly brief §80.
- TRAITS: `PrestigeScreen.cs:1331-1352` YOU NEED FIRST with ticks; reasons `:1362-1366`.
- MASTERY: `BuildScreen.cs:1539-1546` state line; "LOCKED — WALK TO IT FIRST" does not name the missing node (`MasteryNode.Unlocked(isTaken)` `MasteryTree.cs:119` knows).
- BUILD: `WeaveScreen.cs:665` "IS LEARNED ON THE MASTERY TREE, ON X'S ROAD" (good), `:701`, `:749`.
- FORGE: `:1733` "NEEDS X IN YOUR BUILD — UNTIL THEN IT DOES NOTHING"; wallet marks `:2150` "UPGRADE NEEDS 79".
- STATS reset: "NEEDS 1 CRYSTAL" button label + `:505`.
Eight screens, one idea, six phrasings. A `RequirementView(text, met: bool, progress?: (cur,max))` drawn
as glyph + text + optional bar has ≥ 6 users.

### 5.4 Set-bonus ladders
- `CharacterScreen.cs:638-653` — active rungs only, in LOADOUT.
- `CharacterScreen.cs:1039-1054` — full 2/3/4/5 ladder with reached/unreached ink in ITEM DETAIL (already the brief §64 shape, at Secondary/Faint = 8 px at 720p).
- `ItemTooltip.cs:266-269` — one-line progress.
Core: `ElementSets.TiersOf / WornCount / Progress / Active` (`ElementSets.cs:42-132`). Three users → `SetBonusLadder` is justified; the Forge item card should be the fourth.

### 5.5 Item grids / cells
- Gear inventory: `CharacterScreen.DrawInventory` `:754` (4-col rarity-edged cells with lock glyph, selected outline).
- Forge reveal haul: `ForgeScreen.cs:2937` hover cells; open-chest card `:2646`.
- Trader offers: `ChestScreen.cs:645` (hover → `ItemTooltip`).
- Forge BAG is a LIST (`ForgeScreen.DrawBag` `:1257`) — the brief §54 INVENTORY column would adopt the grid.
- Non-item card grids with a shared shape: Roster `Card(col,row)` `:87`, Warren `Card(i)` `:70`, Vault `Card(visible)` `:212` — three local layout helpers doing the same arithmetic.
`ItemGrid` (cell art + rarity edge + lock + selected + hover→`ItemTooltip`) has 3 real users; a `CardGrid` layout helper has 3.

### 5.6 Resource strips / cost lines
- Chrome pills `Game1.cs:4847-4890` (Gleam · Dust · Scrap, whole-texture-stretched capsule `UiKit.cs:822-836`, hover tip with exact digits).
- Forge wallet `ForgeScreen.cs:2140-2207` (5 rows: icon, name, use line, have, NEEDS/GIVES mark).
- Warren TOTAL PRODUCTION (4 rows, `warren.png`), cost rows `WarrenScreen.cs:396-400` (owned / required, OK mark), bonus chips `:318-320`.
- Forge COSTS line "12 SCRAP + 79 GLEAM / YOU HOLD…" (`forge.png`), Mastery COST / YOU HAVE `BuildScreen.cs:1526-1533`, Traits WHAT IT COSTS / You have `PrestigeScreen.cs:1314-1324`, Stats TRAIN button labels carry the price.
Core: `Hunter.Gleam / MaterialOf(Material)` (`HunterProgression.cs:101,114`), `MemoryDust`, `WarrenCost`, `WarrenYield` (`Warren.cs:68-71`). A `CostLine(icon, need, have)` has ≥ 5 users; a `ResourceStrip` (icon + value rows) has 3.

### 5.7 Toasts and transient messages — eight dialects
| Site | Shape | Position |
|---|---|---|
| `Game1.DrawLockedToast` `:3184-3195` | flat fill + 3-px top accent, 900 wide | y 96 centred |
| `Game1.DrawNoticeToast` `:3271-3289` | `PanelQuiet` 800×96, two lines | `ToastTop` (176 or under the hunt header) |
| `Game1.DrawBootToast` `:3826-3848` | `PanelQuiet` 560×88 | same |
| `Game1` feedback toast `:4375` | bare gold text | inside settings |
| Guide strip `:3118-3139` | flat fill + 5-px gold left bar + close | bottom centre, 980 wide |
| `ForgeScreen.DrawFeedback` `:1633-1654` | plate + 5-px colour bar, 1–2 lines | foot of the ACTION column |
| `RosterScreen` `_msg` `:342-343` | bare centred gold text | above the CTA |
| `PrestigeScreen` reason `:1362-1366` | bare Ember text | above the CTA |
| `BuildScreen` `_msg` `:1132-1133` | replaces the header caption | header row |
| `WeaveScreen` `_msg` `:890` | bare gold text at y 1016 | UNDER the guide strip |
| `WeaveScreen` copy toast `:721-724` | frames counter | button |
| `MapScreen` message band `:440-443` | plate + gold rules | over the map |
The guide strip's plate (dark fill, accent bar, Body title, close icon) is the one that reads at 720p.
`NotificationToast(title, body?, tone)` with ONE anchor (under the header row, 960-centred, 900×64–96) has
≥ 8 users. Brief §86 wants short toasts for every meaningful action; today Gear equip, Warren upgrade, Map
enter, Trait learn give no toast at all.

### 5.8 Empty states — present and missing
Present: Forge bag `ForgeScreen.cs:1284` ("EMPTY — GO AND HUNT." / "NO GEMS YET — CHESTS CARRY THEM."),
Forge charts `:2218`, Vault `ChestScreen.cs:407-418` (two Dim Body lines inside an otherwise empty 1842×712
frame — `720/vaultfirst.png` shows the shape with one card: the frame is 90 % black), Weave slot
`WeaveScreen.cs:1518-1521` ("THIS SLOT IS EMPTY / PICK ONE OF YOUR SKILLS…"), Weave rows `:1014,1100`,
Stats reset `:505`, Mastery keystones `BuildScreen.cs:930`, Hunt rewards `SoloExpeditionScreen.cs:2650`,
Expedition Log `:2030` ("NO EXPEDITIONS YET"), Gear slot `CharacterScreen.cs:966` ("SLOT EMPTY"), Warren
locked card.
Missing: Gear INVENTORY with zero items (no branch in `DrawInventory`; grep finds none), Traits with zero
points (the inspector still says LOCKED / NOT ENOUGH TRAIT POINTS but nothing says "earn one by conquering"
at the tree level), Mastery with zero points (`buildtree.png` shows "0" and a full tree with no hint),
Warren before targeting/Essence facilities unlock, Roster (cannot be empty), Map (cannot be empty), Hunt
with an empty skill slot (draws an empty medallion, `720/fightreport.png` shows three blank rings).
`EmptyStateView(title, line, action?)` has 4 users today and 4 more places it is owed.

### 5.9 Hover-only explanation surfaces (brief §15)
`UiKit.HoverTip` `:1168` (settings rows `Game1.cs:4794`, hunt log button `:2389`, chest filter `:2743`,
hunt tips `:2811`); `ItemTooltip` (Gear `:472`, Vault ×2, Forge ×3); Stats hover card; Weave hover card;
Vault "?" dossier; currency pill tips `Game1.cs:4884-4890`; nav tile requirement on hover only `:5410`.
`ItemTooltip` is the ONE genuine shared component that already exists and works (`ItemTooltip.cs:12-36`).
The Stats and Weave hover cards are inspectors in disguise and should become panel content.

---

## 6. Legacy vocabulary — every hit that reaches the player, or names a class

### 6.1 Live, player-facing (must change)
| File:line | Text | Screen | Note |
|---|---|---|---|
| `Game1.cs:5041` | `"WOVEN SKILLS"` | HELP (F1) | visible in `help.png` |
| `Game1.cs:5042` | `"SOURCE x FORM x VOW"` | HELP | the deleted composition, stated as the rule of the game |
| `Game1.cs:5046` | `"FORM IS HOW YOU FIGHT"` | HELP | |
| `Game1.cs:5062` | `("T", "BUILD, THE SAME PLACE AS B")` | HELP | T key duplicate; stale routing note |
| `BuildScreen.cs:1398` | `"YOUR {name} SKILLS HIT TWICE AS HARD. THE FAR FORMS HIT SOFTER —"` | MASTERY attunement ceremony | live: `:650` opens it on the first Specialisation |
| `BuildScreen.cs:1400` | `"A VOW ON A FAR-FORM SKILL PULLS IT ONE RING CLOSER."` | same | |
| `BuildScreen.cs:1519` | `"…WEAVE IT ON THE BUILD SCREEN (B)."` | MASTERY node card (SkillRoad) | |
| `WeaveScreen.cs:866` | `"…A VOW ON A FAR-STYLE SKILL PULLS IT ONE RING CLOSER"` | BUILD header | "ring" is the hex-diagram metaphor; Style is right, "ring" is Form-era |
| `WeaveScreen.cs:860` | `"A SKILL IS LEARNED ON THE MASTERY TREE, THEN WOVEN INTO A SLOT"` | BUILD header | weave register |
| `WeaveScreen.cs:926` | `"WHAT YOU ARE WEAVING"` | BUILD panel title | brief §68 wants YOUR LOADOUT (ACTIVE / PASSIVE) |
| `WeaveScreen.cs:691`, `:647` | `"SLOT WOVEN."`, `"SLOT UNWOVEN."` | BUILD feedback | |
| `WeaveScreen.cs:1748` | `"BIND TO SLOT n"` | BUILD | |
| `ChestScreen.cs:742` | `"NONE WOVEN"` | VAULT paste-code preview | |
| `ChestScreen.cs:750` | `$"{s.Source} {s.Form}"` fallback | VAULT paste-code preview | v1 share codes; `ShareCodes.cs:244-245` still accepts Form. Keep the decoder, change the printed fallback to the resolved `SkillId` name or "OLD CODE". |
| `PrestigeScreen.cs:468` | `"…GO DEEPER INTO THE CORRUPTION, OR RAISE A REGION'S MASTERY."` | TRAITS | fine, listed only because "MASTERY" here means region mastery, colliding with the MASTERY screen name |

"Weave/woven" is the game's own equip verb, not a deleted mechanic — the brief lists it under legacy
cleanup (§101) and §66-68 rename the screen to LOADOUT. Recommendation: EQUIP / UNEQUIP / SLOT as verbs,
LOADOUT as the noun, and one decision recorded in V2's vocabulary table.

### 6.2 Dormant (unreachable by a player, still compiled and photographable)
`BuildScreen.Draw` overview branch (`:683-700`): `:690` `"EVERY SKILL IS A SOURCE AND A FORM — YOU CHOOSE
BOTH"`, `:763` `Big(b, "FORMS", …)`, `:779` `"Add a skill to begin. Every skill is a SOURCE and a FORM."`,
`:753` "CORE COMPOSITION". Reachability: the rail sets `ShowTree = true` (`Game1.cs:5293`) and nothing
ever sets `_editMode` back to false (no assignment in `BuildScreen.cs`), so a player never sees it; only
the `build` / `attune` fixtures open it (`Game1.cs:1317`, `:1559-1561`). Per the project's own memory
("dormant features are the failure mode") this is a delete, not a rewrite — and the `build` capture mode
should go with it or point at the tree.

### 6.3 Class / file / fixture names that lie about the screen they draw
| Name | Draws | Brief |
|---|---|---|
| `WeaveScreen` | BUILD (loadout) | §67 → `LoadoutScreen` |
| `BuildScreen` | MASTERY tree (+ dead overview) | §67 → `MasteryScreen` |
| `FormHexDiagram` (`FormHexDiagram.cs:41`; doc `:32` still says "each Form's icon") | the six-STYLE affinity hexagon | §67 → `StyleHexDiagram` only if the attunement chart stays; otherwise delete with the ceremony copy |
| `PrestigeScreen` | TRAITS | rename or record why not |
| `CharacterScreen` | GEAR | |
| `ChestScreen` | VAULT | |
| `StatsScreen` | STATS = TRAINING (§38) | |
| Core `MemoryDustTree`, `MemoryDustText` | the TRAIT tree (paid in trait points) | Core naming; out of UI scope but V2's vocabulary table should note it |
| Capture modes `character`, `dust`, `weave`, `buildtree`, `build` | GEAR, TRAITS, BUILD, MASTERY, dead overview | `Game1.cs:474-485` map them; rename alongside |
| Comments only, no player text: `SoloExpeditionScreen.cs:314, 850, 1246, 1355-1368, 2290, 2842-3072, 3252-3354`; `ForgeScreen.cs:959`; `Game1.cs:1523, 1950-1951, 2839, 2886` | | comment drift; a sweep, not a finding |

### 6.4 "Dust" terminology
Player-facing "DUST" today always means the Memory Dust currency (`Game1.cs:4881`, `MapScreen.cs:143`,
`WarrenScreen.cs:241,309,318,371`) — correct. The stale use is the STANDARD's "Dust" screen and the `dust`
fixture name for TRAITS.

---

## 7. Shared components — which have ≥ 2 real users today (brief §90), and what not to build (§91)

### 7.1 The strongest evidence: the palette is copied 12 times
`private static readonly Color Bone/Gold/Ember/Slate/Dim` appear in `BuildScreen.cs:30-34`,
`CharacterScreen.cs:39-43`, `ChestScreen.cs:42-45,76`, `ForgeScreen.cs:38-42`, `MapScreen.cs:29-34`,
`PrestigeScreen.cs:37-50`, `RosterScreen.cs:33-38`, `SoloExpeditionScreen.cs:40-44`, `StatsScreen.cs:39-44`,
`WarrenScreen.cs:21-26`, `WeaveScreen.cs:56-61`, `Game1.cs:100-104`, `UiKit.cs:857-863` — and they already
disagree: `Dim` is `#2C2C36` in Build/Hunt, `#3A3A44` in seven others, `#5E5A6E` in Traits; `Gold` is
`#F0A830` in screens but `#F0B24A` in the nav, hex diagram and screen titles; `Met` is `#6EC87A` in five
files and `#5FC88F` in the hex diagram. `ItemTooltip` has its own five. Total local `Color` constants:
Game1 18, Hunt 14, Gear 13, Mastery 12, Forge 11, Warren 11, Traits 10, Stats 8, Map 7, Weave 7, Vault 6,
Roster 6, Tooltip 6. **`UiKit.Ink` (a static palette) is the first extraction and costs nothing in layout.**

### 7.2 Components with ≥ 2 real users (build these)
| Component | Real users today | Where the geometry already agrees |
|---|---|---|
| `ScreenHeader(title, hint?)` | 10 screens (§5.1) | title (960,24) ScreenTitle Display; rule (700..720,74,480..520,3) |
| `InspectorPanel` (sections + CTA slot) | 6 (§5.2) | `UiKit.TitleTop` header, `Dim` 2-px rules, CTA at Bottom-92 h68 full content width |
| `RequirementView(text, met, progress?)` | 8 (§5.3) | Roster's `Quest.ProgressLine` row is the reference |
| `SetBonusLadder(element, worn)` | 3 (§5.4) | `CharacterScreen.cs:1039-1054` is the reference |
| `ItemGrid` (cell + rarity + lock + select + hover→`ItemTooltip`) | 3 (§5.5) + Forge BAG on adoption | `CharacterScreen.DrawInventory` |
| `CostLine(icon, need, have)` / `ResourceStrip(rows)` | 5 / 3 (§5.6) | Forge wallet row `:2196-2205` |
| `NotificationToast(title, body?, tone)` | 8+ (§5.7) | guide strip plate style, one anchor |
| `EmptyStateView(title, line, action?)` | 4 + 4 owed (§5.8) | Weave slot empty state `:1518-1521` is the best copy |
| `UiKit.Plate(rect, accent?)` — the QUIET tier | ≥ 8 hand-drawn today (§3 P0-2) | `Game1.cs:3124-3125` guide strip; `WarrenScreen.cs:258-260` |
| `UiKit.Button(style: Primary/Secondary)` | every screen with 2+ buttons (Forge 22 sites, Vault 8, Gear 4, Stats 4, Hunt 4) | `UiKit.cs:873`; the art has `primary/secondary/disabled` variants already (`:877`) |
| `NodeInspector` | 2 (Traits `:1242`, Mastery `:1419`) — identical section idea (WHAT IT DOES / COST / NEED FIRST / state) | brief §45 says formalise Traits' and reuse on Mastery |
| Word-wrap | six private copies: `Game1.WrapText` `:5464`, `RosterScreen.DrawWrapped` `:347`, `BuildScreen.DrawWrapped` `:1556`, `CharacterScreen.Wrapped` `:1063`, `WarrenScreen.DrawWrapped`, `ForgeScreen.DrawWrappedBig` — while `UiKit.WrapBig` `:1138` exists | delete the six; they also disagree on line pitch (36 / 28 / 30 / 26 / 22) |

### 7.3 What NOT to build (brief §91)
No layout DSL, no data-bound screen base class, no reflection. The eleven screen classes keep their
`static readonly Rectangle` zones and their `Draw`; the components above are plain methods on `UiKit` or
small `static` classes like `ItemTooltip` — the pattern the codebase already trusts. `CardGrid` arithmetic
(Roster/Warren/Vault) is fine as three local helpers; do not generalise it.

---

## 8. Outline of the UX SCREEN GUIDE STANDARD V2 (brief §89) — outline only, not the standard

**Part A — Global contract (replaces V1 §2, §7–9, §14)**
1. Canvas, present, UI scale: 1920×1080 authoring; present fit; the UI-Scale setting (100/125/150/Auto) and how text stays crisp under it; menu screens author in TRUE 1920 space (no `OverlayScale`), the rail is a reserved 180-px column.
2. Typography: the ladder (unchanged names), the role rules (brief §6), the PHYSICAL table at 720/1080/1440 and the floor ("Secondary never carries a sentence; Caption is a tag").
3. Ink: Primary / Secondary / Disabled / Accent(Gold) / Danger / Source colours / Rarity ramp — one `UiKit.Ink`; the colour-alone rule (brief §8).
4. Surface tiers: PRIMARY (ornate `Panel`, ≤ 1 per screen + modals), SECONDARY (thin bronze frame — a new tint or a nine-slice with the ornament suppressed), QUIET (`Plate`); paddings from `UiTypography`; the frame-by-aspect rule.
5. Controls: `Button` Primary/Secondary/Danger/Disabled; `Field`; `Pill`; `Bar/BarArt`; close icon rule (`CloseRect`).
6. Navigation: the 11-tile rail, keys, locked/NEW/count badges, requirement on hover + click.
7. Shared components (§7.2 list) with their contracts.
8. Interaction grammar (brief §15) and action severity (brief §85).
9. Notification policy: one toast anchor, one guide-strip owner (HUNT), per-screen banner line, tours.
10. Empty / locked / selected / disabled / overflow — the generic rules, with the V1 §12 list.
11. Data honesty (V1 §11 + brief §92) and the "Core member or telemetry" test.
12. Fixtures (V1 §15 + brief §95) and the screenshot gate (720 + 1080).
13. Vocabulary table: STYLE, SKILL, VARIATION, REINFORCEMENT, VOW, KEYSTONE, LOADOUT, MASTERY, TRAIT, SOURCE (of a variation / region / item set), GLEAM, MEMORY DUST, SCRAP/ESSENCE/CORE/CRYSTAL, CHART; banned: Form, Woven Ability, Aptitude, Source×Form, weave (as a noun).

**Part B — Per screen (13 entries: HUNT, EXPEDITION LOG, MAP, BUILD/LOADOUT, MASTERY, TRAITS, ROSTER,
GEAR, VAULT, FORGE, TRAINING, WARREN, SETTINGS/HELP)**, each with exactly the brief §89 fields: purpose ·
primary player question (§100) · primary action (§84) · layout zones in px · inspector behaviour ·
typography roles · tier map (which rect is PRIMARY/SECONDARY/QUIET) · empty · locked · selected ·
disabled · overflow · keyboard · mouse · onboarding · accessibility · deterministic fixture (mode name +
what it must contain) · visual acceptance criteria (720p legibility line included).

**Part C — Process (from V1 §3, §4, §17, §18)** dependency classes, asset whitelist, acceptance metrics,
completion format (+ 720p screenshot line).

Delete from V1: §1.1 reference-image deliverable (fixture screenshots replace it), §16 debug mode (or
one shared helper), §19–20.

---

## 9. Data honesty — what the brief wants shown vs what Core computes today

| Brief wants | Core today | Status |
|---|---|---|
| Defeat headline + "main pressure" (§22) | `RunReport.Verdict()` `RunReport.cs:68-83` picks one of stalled / armour / reach / health-loss / out-scaled from `AbsorbedFraction`, `TargetsPerActivation`, `CreaturesPerWave`, `HealthLostPerWaveFraction`, `WallWave` | **EXISTS** (single sentence; a structured `(Kind, Line)` would let the UI headline it) |
| "LIMITING FACTOR — REACH — 1 of 3" (§22/24) | same fields; `RunReport.DiffEntries(previous)` `:96` for the SINCE YOUR LAST RUN block | **EXISTS** |
| "BEST PERFORMER — BLOW — 43 % of your damage" (§22) | no per-skill damage aggregate anywhere in Core (grep: only `SkillProgress.UsesBySkill`, `QuestProgress.WavesBySkill` — wave counts, not damage). `WaveReplay` carries per-cast Skill events (`SoloExpeditionScreen.cs:2883` comment) | **NEEDS telemetry** — a damage-by-`SkillId` tally in `WaveMetrics` → `RunReport` |
| Before → after on Forge UPGRADE (§56) | `Forge.Refine(item, tuning)` returns `RefineResult(Product, Scrap, Gold, Crystal){FailChance}` `Forge.cs:53-59,263`; `GreaterRefine` `:299`; `AtRefineCap` `:256` | **EXISTS** (Forge card already draws 8→9 / 251→254) |
| RNG honesty on RE-ROLL (§58) | `Enchantments.PoolFor(slot)` `Enchantments.cs:254`, `MagnitudeFor(kind, rarity)` `:283`, `Reforge.EnchantMaterial` / `EnchantCostFor` `Reforge.cs:126,40`; result is `ReforgeResult` after the roll | **EXISTS** for pool + range + cost; "can it be worse" is a rule to state, not data |
| Training "99 → 103" (§40) | `Hunter.ValueOf(stat)`, `GainPerRank(stat)`, `RankOf` `HunterProgression.cs:198-202`; the shown figure goes through `Build.Resolve` / `SoloBattle` formulas (`StatsScreen.cs:26`) | **EXISTS as formulas**; needs a pure "evaluate at rank+1" call, no new telemetry |
| Roster unlock progress "80 / 100" (§32) | `Quest.Current / IsDone / ProgressText / ProgressLine` `Quests.cs:76-93` | **EXISTS** (already on cards) |
| Locked screen requirement (§83) | `Unlocks.Requirement(activity)` `Unlocks.cs:173` (text only); `UnlockFacts` `:60` has the facts | **EXISTS** as text; a numeric progress would need a small pure helper, no telemetry |
| Warren capped facility (§80) | `Warren.IsAtLevelCap`, `DepthForNextLevel` `Warren.cs:286-290` | **EXISTS** (already shown) |
| Warren shared summary (§79): level, cap, facility cap, total rates | `Warren.Level/Xp` `:190-191`, `FacilityLevelCap` `:275`, `UnlockedFacilityCount` `:207`, yields; offline coverage = `SaveSystem.MaxOfflineSeconds` (24 h, `OfflineHunt.cs:22`) | **EXISTS** |
| Offline welcome-back (§25): away time, HUNT +, WARREN + | `OfflineHunt.Result(Gleam, WavesCleared, Falls, DeepestWave, …)` `OfflineHunt.cs:54-56`; Warren yield; boot toast already prints "+140 GLEAM (90 WARREN · 50 HUNT)" | **EXISTS** for currency and waves |
| "NOTABLE — BLOW reached Level 14 · set reached 5 pieces · Rare found · 37 auto-salvaged" (§25) | `SkillProgress.LevelOf` and `ElementSets.Active` are queryable, but nothing snapshots them before/after the offline tick; offline waves pay Gleam only (`OfflineHunt.cs:75` "unwatched waves pay gleam, not build depth") so no items/levels change offline | **NEEDS** a before/after snapshot at boot for skill levels and sets if wanted; items-found offline do not exist in the model — do not show |
| Vow validity, what breaks it (§71) | `Vows.IsActive(vow, ctx)` `Vows.cs:369`, `SoloBattle.DescribeBuild` `SoloBattle.cs:2047`, `VowDemand` text `WeaveScreen.cs:550-564` | **EXISTS** |
| Skill discovery permanent (§75) | `MasteryTree.LearnedSkills / LearnSkill / RestoreLearned` `MasteryTree.cs:215-230,308` | **EXISTS** |
| Mastery path preview "this path costs 4" (§76) | `MasteryNode.Unlocked(isTaken)`, `SecondPrereqs`, `Cost` `MasteryTree.cs:47-119`; no path search | **NEEDS** a pure BFS helper (deterministic, no telemetry) — optional per §76 |
| Hunt "2 CHESTS READY", idle rate (§20) | `_forge.UnopenedChests.Count` (`Game1.cs:5443-5445`); idle rate already drawn | **EXISTS** |
| "EQUIP BEST" honesty (§65) | `CharacterScreen.EquipBest` `:371-391` picks max `Hunter.PowerContribution` per slot | It is EQUIP HIGHEST POWER — rename |
| Chest card facts (§48) | `ChestDossier` `ChestDossier.cs:34-160` floors / ranges / favoured slots | **EXISTS** |

---

## 10. Fixtures

Existing deterministic modes (`Game1.cs:1486-1491`, `capture.sh` header): `fight boss expedition forge
build character stats warren map dust world region2 region3 conquered lootforge reforge vow hybrid rig vfx
help buildtree buildzoom itemmenu runlog fightgear fightfilter traitlit traitterm intro tour vaultfirst
gemtour settingsopen roster rosterlocked weave vault fightreport fightswing fightfall fightaura fightflash
attune attuned trader banked corrupted corruptedboss settings settingsfull bossdebug`. `tour <Activity> <card>`
dresses each screen with its own fixture. The baseline set used here: fight, fightreport, runlog, map,
roster, rosterlocked, stats, character, forge, vault, vaultfirst, weave, buildtree, buildzoom, warren,
dust, settings, help.

What brief §95 additionally needs, globally:
- A **720p capture path**: `capture.sh` saves the canvas at 1920×1080 (`Game1.cs:4069`); the 720 copies here are offline resamples. Add a `RH_SHOT_SIZE=1280x720` (present-fit render, not a resample) so the gate photographs the real bilinear output — and later the real UI-scale output.
- A **UI-scale dimension** (`RH_SHOT_UISCALE=125`) once the setting exists.
- **Rename** `character→gear`, `dust→traits`, `weave→build`, `buildtree→mastery`; retire `build` (dormant overview) and `attune/attuned` if the ceremony goes.
- **One fixture per §95 line that is missing today**: BUILD with a chosen variation AND a bought reinforcement AND one met + one unmet Vow in the same shot (`720/weave.png` has the Vows but 0/3 reinforcements); MASTERY with points to spend + a discovery node + a notable + a capstone visible (`buildtree` has 0 points); TRAITS with a point AND a blocked node AND a permanent path in one frame (`dust` has 3 points and a locked node — add the path); VAULT with all tiers (present) AND the empty state (`vaultfirst` is one gift chest, not empty); GEAR with an item comparison hovered (`character` selects but does not compare); WARREN with a targeting facility and a capped one (`warren` has capped); TRAINING with a hovered row so the rule text is photographed.
- A **nav-locked** fixture (a fresh save at wave 0: only HUNT, MAP, ROSTER open) to photograph the locked-tile hover and toast.
- A **toast** fixture (`RH_SHOT_TOAST=<kind>`) so the eight dialects can be compared in one place before they are unified.

---

## 11. Keep (good today — preserve through V2)

- `UiTypography` as the single ladder + the panel grid constants, and `tools/check_ui_type.py` as the gate. Only the physical target is missing.
- `UiKit.TitleTop/CaptionTop/BodyTop/ContentLeft/ContentRight/PadX` — every inspector already uses them; `FrameDrop` handles the square frame.
- The nav rail: stable, icon + label, locked dim, NEW mark, VAULT count, requirement on hover and on click (`Game1.cs:5351-5453`). Brief §13 says keep it; the evidence agrees.
- `ItemTooltip` — the one shared component that exists; the model for the rest.
- MAP's hierarchy (ornate canvas, quiet inspector, plate cards) and its CTA states; ROSTER's 2×5 grid with `Quest.ProgressLine` on locked cards; TRAITS' WHAT IT DOES / WHAT IT COSTS / YOU NEED FIRST inspector and `MemoryDustText.Permanence` line.
- The Expedition Log's headline band, measure table and SINCE YOUR LAST RUN HERE diff (`runlog.png`).
- Warren's "REACH DEPTH n ON AN EXPEDITION TO UPGRADE" and milestone bar; the facility card plate style.
- The Forge before→after arrows on the item card and the tab strip.
- The guide strip's plate style (dark plate + accent bar + close) as the toast/plate reference — just not its placement policy.
- Per-screen tours (`Onboarding.TourFor`) and the `tour` fixture that photographs them over real content.
- `SoloExpeditionScreen`'s arena composition; the champion art; the source colours; the ornate frame art itself (as PRIMARY).
- Data discipline: every screen header comment says "every value is real" and it is — nothing here invents.

---

## 12. P1 and P2 (global)

P1 layout / UX
- Menu screens through `OverlayScale` also draw their scrims to 2144×1206 to cover the inset (`UiKit.cs:830`); removing the inset removes that hack.
- Header caption sentences on 10 screens (§5.1) → contextual hint or nothing.
- Inspector CTA geometry: unify Bottom-92 / h68; Mastery gains TAKE; Gear loses the dead LOCK (`CharacterScreen.cs:1059`).
- Hover-driven inspectors on STATS (`StatsScreen.cs:573`) and BUILD (`WeaveScreen.cs:1379`) → panel content.
- Toast unification (§5.7); feedback for equip / upgrade / enter / learn (§86).
- Empty states owed: Gear inventory, Mastery zero points, Traits zero points, Warren pre-unlock, Hunt empty slot (§5.8).
- Requirement copy that names the thing: Map (`:574`), Warren locked card (`:264`), Mastery "WALK TO IT FIRST" (`:1545`).
- Locked-state colour-only encodings (§P0-4) get a glyph + word.
- `Button` gains a Secondary style so one primary action per screen is possible (brief §84).
- Six private word-wrappers → `UiKit.WrapBig`.

P2 polish
- Rule width 480 vs 520 under the title (`WeaveScreen.cs:859` vs `BuildScreen.cs:1126`).
- `CharacterScreen.cs:445` uses `RegionTitle` for a screen title (alias drift).
- Currency pills stretch the whole 256-px frame into 60 px (`UiKit.cs:836`) — acceptable, but the pill hover tip is a flat rectangle while the nav toast is another; fold into the toast style.
- `Dim` has three values across screens; `Gold` two; `Met` two (§7.1).
- Comment drift naming Form in `SoloExpeditionScreen.cs` and `Game1.cs` (§6.3).
- `PrestigeScreen.DrawDebug` is the only survivor of V1 §16 — decide keep-one-shared or drop.

---

## 13. Proposed global layout template (1920×1080, no inset)

- **Nav rail** x 0–180, full height (unchanged).
- **Header band** x 180–1920, y 0–100: screen title centred at x 1050 (the centre of the content column, not 960 — today titles sit 10 % left of the content's centre because of the inset), ScreenTitle Display; NO caption sentence; the row at y 70–96 is the screen's HINT slot (status line / message / onboarding banner-for-this-screen), Body size, one line.
- **Currency pills + gear** y 16–76, right-aligned to 1900 (unchanged).
- **Content column** x 200–1400 (1200 wide) for the main surface: map canvas, tree canvas, grid, arena. PRIMARY tier lives here and only here.
- **Inspector** x 1420–1900 (480 wide), y 120–1040: SECONDARY tier; header at `TitleTop`; sections separated by 2-px rules; primary action at y 948, h 68, full content width; refusal / requirement line at y 916 (Secondary is fine here because it is one clause, not a sentence — or Body when the hint is longer).
- **Toast anchor** (960 → 1050 centred), y 112, 900×64–96, one at a time, queue behind.
- **Guide strip** HUNT only, y 1080-84-26, 980 wide — or moved into the HUNT right column per brief §20.
- Screens whose main surface is a horizontal strip (VAULT, FORGE three-column) keep their own zones but honour the header band, the inspector width and the toast anchor.

---

*End of report.*
