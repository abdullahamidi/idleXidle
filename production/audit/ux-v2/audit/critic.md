# UX V2 audit — COMPLETENESS CRITIC (cross-report review)

Reviewed 2026-09-01: `production/audit/ux-v2/BRIEF.md` (all of §0–102) and the sixteen reports in
`production/audit/ux-v2/audit/` — `global-ux-standard.md`, `global-typography-scale.md`,
`global-legacy-terminology.md`, `global-chrome-onboarding.md`, `hunt.md`, `expedition-log.md`,
`build-loadout.md`, `mastery.md`, `forge.md`, `gear.md`, `vault.md`, `map.md`, `roster.md`,
`training.md`, `traits.md`, `warren.md`. Every claim below cites `report.md:line` (line numbers as
written in those files) or `src:line` where I verified a contested fact myself. Read-only on source.

Pixels I opened myself to arbitrate: `baseline/720/weave.png`, `baseline/buildtree.png`,
`baseline/720/vaultfirst.png`. Code I checked myself: `Game1.cs:5036-5048` (F1 rows),
`Game1.cs:3226-3236` (screen-banner rect), `Game1.cs:1486-1491, 1313, 1559-1622, 2000` (fixture
modes), `Unlocks.cs:96-145` (gate order), `Unlocks.cs:234-248` + `DustEffects.cs:212-215` (slot
gating), `BuildScreen.cs:1398/1400` (ceremony copy), `tools/asset-pipeline/capture.sh:9-16`,
`tools/asset-pipeline/*` (no 720p tool exists).

---

## 0. Verdict in one paragraph

Coverage of the brief is broad and unusually well-evidenced: every §100 screen has a report, every one
of the 18 baseline PNG pairs was opened by at least one auditor, and the data-honesty tables are the
strongest part of the set. The gaps are of two kinds. (1) **Surfaces outside the baseline set were
never looked at**: the title screen, the chest reveal + OPEN ALL summary, the trader and paste-code
cards, the specialisation ceremony, the tour cards, the boss/corruption HUD, three of the four Forge
tabs, every hover state and every empty state — reports reasoned about these from code only. (2) The
per-screen auditors, working alone, each **re-invented the shared grammar**: nine different inspector
rectangles in two coordinate spaces, six CTA heights, three section vocabularies, two hint-slot
positions, three toast anchors, two defeat-banner designs, two welcome-back panels, two Expedition
Log rectangles, two settings layouts, two class-rename schemes, and two positions on whether the
0.896 overlay inset goes now or later. None of these is wrong on its own; together they would produce
exactly the "collection of individually designed screens" the brief opens by rejecting. The owner
needs about twelve shared decisions (§5 below) before any screen pass starts.

---

## 1. MISSED — what got no coverage or shallow coverage

### 1.1 Brief sections

| § | Topic | Coverage | Gap |
|---|---|---|---|
| §0.2 "inspect every player-facing screen" | TITLE screen / new game / continue | **None.** `Game1.cs:4897 DrawTitle()`, fixture mode `"title"` exists (`Game1.cs:1313`) — no report, no baseline, not in `global-ux-standard.md:98`'s overlay list either | Audit the title menu (gold `Panel` on the selected item, `global-ux-standard.md:187`), the NEW GAME confirm, and what a returning player sees first |
| §21 damage feedback | Multi-hit flood, priority order | Code-only (`hunt.md:126-129`, `hunt.md:263-267`) | No capture of a Volley cast with 5 alive creatures; `hunt.md:365` asks for a `fightvolley` mode — nobody opened `fight.png` during a cast |
| §25 welcome back | Return panel | Two designs (`hunt.md:268-277`, `expedition-log.md:321-325`) — see §3.6 | Neither report checked how the panel interacts with the tour (`hunt.md:277` "queue it") vs the boot-toast queue rules (`global-chrome-onboarding.md:232-233`) |
| §36 accessibility "high-contrast combat states if feasible" | Feasibility | **None.** `global-chrome-onboarding.md:326-331` covers shake / reduced motion / speed / keys; the high-contrast question is not asked | One line from the HUNT owner: what would a high-contrast combat state toggle (arena scrim, outline on creatures, pips)? |
| §46 / §85 permanent confirmation | Terminal traits | `traits.md:105-106` | OK. But the *mastery* specialisation ceremony (`mastery.md:92-93, 385-388`) is a **reversible** choice drawn as a ceremony — `global-legacy-terminology.md:353` says "no red framing"; nobody reconciled this with §99-9/10 |
| §51 / §52 reveal | OPEN ALL consequence, reward priority | `vault.md:139-147`, `vault.md:253-262` (data only) | **No pixels.** The reveal (`ForgeScreen.cs:2406-2939`) appears on every chest open; `forge.md:257-258` declares it "out of this screen's layout scope"; `lootforge` mode exists (`Game1.cs:1504`) but was never captured |
| §57 operation tabs | RE-ROLL / SOCKET / SALVAGE | `forge.md` covers all four in code (`forge.md:77-83`) | **Only the UPGRADE tab is in the baseline** (`baseline/forge.png`); `forge.md:438` asks for per-tab captures — three of four operations were never seen |
| §86 feedback after action | Per-screen toasts | `global-ux-standard.md:308-310` lists the missing ones (equip, upgrade, enter, learn) | Only `roster.md:314-316` and `traits.md:129` design the post-action moment; GEAR, WARREN, MAP, FORGE proposals are silent on what the player sees after the click |
| §87 transitions / Reduced Motion | Inventory of animations | Reduced-Motion gate mentions only: `traits.md:130`, `mastery.md:236-237`, `hunt.md:175`, `global-chrome-onboarding.md:328-330` | No report inventories what animates today (inspector crossfade — none exists; equip movement; milestone glow; resource tick) or which of §87's list are owed |
| §89 keyboard / gamepad | Per-screen | BUILD `build-loadout.md:137-139`, TRAITS `traits.md:118-119`, MASTERY `mastery.md:207-210`, ROSTER `roster.md:204-206`, TRAINING `training.md:223-224`, MAP `map.md:285` | **Absent** for HUNT, GEAR, VAULT, FORGE, WARREN, SETTINGS, HELP. The technical-preferences cycle-and-confirm requirement is never checked against HUNT (which has no targeting — someone should say so) |
| §93 performance | Per-frame allocation | `training.md:161-162`, `global-typography-scale.md:349-351` | The other twelve proposals add wrapped prose, tooltips and per-row strings without a caching note; `build-loadout.md:155-157` is the only one that flags a Draw-time model write |
| §94 2560×1440 | Upscale | `global-typography-scale.md:443-444` (arithmetic only) | Nobody checked frame-art upscale, `HoverTip`'s literal 1912/1072 bounds (`global-typography-scale.md:207`) or the letterbox at non-16:9 |
| §94 / §96 720p evidence | Real presented output | Everyone measured `baseline/720/*` | **The real 1280×720 frame has never been photographed.** No tool in `tools/asset-pipeline/` produces the 720 copies (my check; `global-typography-scale.md:46-50` "method unknown", `gear.md:347-348` "scratch downscale720.py"). Reports disagree on fidelity — see §3.13 |
| §95 fixtures | Rail coherence | Every report lists fixture gaps for its own screen | Nobody checked that a fixture's **nav rail** is a state the game can produce — see §1.3 |
| §1 loop | Click-count of OBSERVE → CHANGE ONE THING → RETURN | `expedition-log.md:107-110` (doors), `map.md:349-350` (one-click travel) | No report counts clicks from a defeat to a BUILD change and back, which is the loop the brief says the whole UI serves |
| §77–78, §95 WARREN "services / targeting / automation" | Mechanic | `warren.md:24-34` correctly says none exists in Core | Correct, but it should be escalated as **a brief correction**: §77, §78, §82 ("no Warren targeting unlocked") and §95 ("levels + targeting + capped") describe a system that was retired 2026-08-24 (`warren.md:29-31`). Only warren says so; no global report carries it to the owner |

### 1.2 Player-facing surfaces nobody examined in pixels

1. **Title screen** — see above. Fixture `"title"` (`Game1.cs:1313`) is a boot-check entry, not a capture mode.
2. **Chest reveal cascade + OPEN ALL summary** (`ForgeScreen.cs:2406-2939`) — `lootforge` + `RH_SHOT_T` exists (`Game1.cs:1500-1504`); never captured for this audit.
3. **Trader popover and A FRIEND'S ITEM / A FRIEND'S BUILD cards** (`ChestScreen.cs:578-760`) — the only live `Source Form` and `NONE WOVEN` strings live there (`global-legacy-terminology.md:135, 304-305`); `trader` mode exists (`Game1.cs:2000`), no `paste` mode (`global-legacy-terminology.md:330`).
4. **Specialisation (attunement) ceremony** — carries P0 legacy copy (`global-legacy-terminology.md:29-30`); `attune`/`attuned` modes exist (`Game1.cs:1559, 1611-1622`) but are not in `capture.sh:11-13` and have no baseline (`global-legacy-terminology.md:325`, `mastery.md:376`).
5. **Tour cards and the 8-card intro** — `global-chrome-onboarding.md:183-185` computes 12.7 px body at 720p but no `tour`/`intro` capture was opened by anyone.
6. **Boss / corruption HUD** — `boss`, `corrupted`, `corruptedboss` modes exist (`Game1.cs:1488`); `hunt.md:366` lists it as a gap; unseen.
7. **Forge RE-ROLL / SOCKET / SALVAGE tabs** — see §1.1.
8. **Every hover state** — `ItemTooltip` (460 wide, ~560 tall for a Legendary, `gear.md:127-132`), Stats hover card, Weave hover card, nav locked-tile tip, settings tips, Vault dossier tip. `RH_SHOT_MOUSE` exists; no baseline uses it.
9. **Every empty state** — empty Vault (`vault.md:112-113` "never been photographed"), empty bag (`gear.md:353-354`), empty log (`expedition-log.md:347`), zero mastery points with the empty inspector is captured but nothing else. All reasoning is from code.
10. **Disabled/unaffordable states** — TRAINING `NEED n GLEAM` never posed (`training.md:280-281`); WARREN affordable/unaffordable never posed (`warren.md:292-302`); MASTERY AVAILABLE node never posed (`mastery.md:356-362`); TRAITS affordable node never posed (`traits.md:219`).
11. **Expedition Log variants** — STALLED band, NEW RECORD chip, cross-region empty diff, named diff row, armour/sustain verdicts (`expedition-log.md:339-348`): all six unseen.

### 1.3 A fixture-coherence problem nobody named (my own observation)

`Unlocks.IsOpen` is monotone on `DeepestWave`: BUILD opens at ≥ 5, MASTERY at ≥ 25
(`Unlocks.cs:124-136`). Yet:

- `baseline/buildtree.png`: the MASTERY tile is lit while **BUILD, GEAR, VAULT, FORGE are dimmed** — a
  rail no career can produce (MASTERY open implies BUILD open). The fixture forces the screen open
  (`DevOpenTree`) without seeding the facts the rail reads.
- `baseline/720/weave.png`: **MASTERY and TRAITS are locked** on a screen showing four mastery-learned
  skills and three learned keystones (`build-loadout.md:314-318` describes the seeding). Same cause.

Consequence: every per-screen proposal that places content near the rail, or that relies on the rail's
NEW marks (`global-chrome-onboarding.md:246-248`), has been judged against a rail that lies. §95 "real
domain data" should include "facts that make the rail agree with the screen". `tools/check_nav_gates.py`
checks array parallelism only (`global-chrome-onboarding.md:214`). No report flags this.

### 1.4 States nobody examined per screen (summary grid)

| Screen | Empty | Locked | Disabled | Selected | Hover |
|---|---|---|---|---|---|
| HUNT | empty slot seen in `fightreport.png` (`hunt.md:86`) | — | — | — | none |
| LOG | code only | — | — | — | none |
| MAP | n/a | nodes yes; **locked inspector never** (`map.md:374-376`) | locked CTA yes | yes | none (`map.md:208`) |
| BUILD | code only | library face never (`build-loadout.md:330-331`) | Dim placeholders yes | slot 2 yes | comparison never (`build-loadout.md:332`) |
| MASTERY | inspector empty yes | yes | — | **inspector never with content** (`mastery.md:367-370`) | none |
| TRAITS | zero-points never | yes | yes | yes | none |
| ROSTER | n/a | yes | `LOCKED`/`PLAYING` buttons yes | yes | none (`roster.md:203-204`) |
| GEAR | never | class-locked cell yes | LOCK yes | yes | never |
| VAULT | **never** | — | OPEN ALL yes | n/a | never |
| FORGE | never | — | MERGE yes | yes | never |
| TRAINING | n/a | — | **never** | **never** | never |
| WARREN | fresh state never (`warren.md:297-300`) | never | capped yes | yes | none (`warren.md:130`) |
| SETTINGS/HELP | — | — | WINDOW SIZE disabled yes | — | tips never |

---

## 2. CROSS-SCREEN INCONSISTENCIES the per-screen auditors could not see

### 2.1 Nine inspector rectangles, two coordinate spaces

| Report | Inspector rect | Space | CTA height |
|---|---|---|---|
| `map.md:252` | (1412,146,474,880) unchanged | overlay | 68 (`map.md:273`) |
| `roster.md:289-290` | (1250,120,630,880) | overlay | 68 (`roster.md:310`) |
| `traits.md:190` | (1368,136,500,934) | overlay | 68 (`traits.md:200`) |
| `mastery.md:304` | (1408,112,496,952) | overlay | 52 (`mastery.md:308-309`) |
| `warren.md:207` | (1360,260,520,780) | overlay | 72 (`warren.md:221`) |
| `gear.md:303` | (1328,100,568,940) | overlay | ~50 (`gear.md:318`) |
| `vault.md:336` | (1348,214,532,840) | overlay | 56 ×2 side-by-side (`vault.md:352`) |
| `training.md:207` | (1384,118,512,844) | overlay | 56 (`training.md:207`) |
| `build-loadout.md:253` | (1280,110,600,890) | **true canvas, scale 1.0** | 56 (`build-loadout.md:267`) |
| `forge.md:335-347` | column B (660-1120) + column C (1140-1900) | **true canvas** | 80 (`forge.md:365`) |
| `global-ux-standard.md:553` | (1420,120,480,920) | **true canvas, no inset** | 68 at y 948 |

Widths 474–630, tops 100–260, CTA 50–80 px. `global-ux-standard.md:528` asks to "unify Bottom-92 /
h68"; only MAP, ROSTER and TRAITS comply. The brief's §14 "one interaction model" cannot be learned
from nine geometries.

### 2.2 Section vocabulary — three dialects for the same block

- Requirements: `YOU NEED FIRST` (`traits.md:196`, `mastery.md:307`) vs `REQUIREMENTS`
  (`build-loadout.md:258`, `vault.md:350`) vs `REQUIREMENT` (`warren.md:215`, `map.md:270`) vs
  `LOCKED` headline (`map.md:270`).
- Cost: `WHAT IT COSTS` (`traits.md:197`) vs `COST` (`mastery.md:308`, `warren.md:218`) vs
  `NEXT RANK · 108 GLEAM` (`training.md:207`).
- Effect: `WHAT IT DOES` (six reports) vs `WHAT IT PROMISES` (`vault.md:343`) vs none (MAP uses
  POWER / ENEMIES / GOAL / DROPS, `map.md:259-269`; ROSTER uses STARTING SKILL / INNATE,
  `roster.md:296-299`).
- The brief itself carries both dialects (§14 `WHAT IT DOES / REQUIREMENTS / COST`; §45
  `WHAT IT DOES / WHAT IT COSTS / YOU NEED FIRST`). Nobody chose. `traits.md:128` notes the order
  clash (COST before NEED) but the reports split on the words.

### 2.3 Rung of the same inspector line differs per screen

- CATEGORY line: Secondary (`build-loadout.md:254`, `map.md:254`, `roster.md:292`, `mastery.md:305`,
  `vault.md:340`) vs Body (`warren.md:208`, `traits.md:191`, `gear.md:305`).
- NAME: Headline (`build-loadout.md:255`, `mastery.md:305`, `gear.md:306`) vs PanelTitle
  (`traits.md:193`, `roster.md:294`, `warren.md:209`, `map.md:255`, `training.md:207`) vs
  PrimaryValue (`vault.md:341`).
- Under `global-typography-scale.md:414-426` both rungs move, but the *choice* per line should be one
  rule (`global-typography-scale.md:459-464` states one; the per-screen proposals predate it).

### 2.4 Hover / click grammar

- TRAITS: inspector follows **click only**, hover = one-line tip (`traits.md:102-103`, brief §15).
- MASTERY: keep "hover shows, click pins" (`mastery.md:83-84`) — hover still drives the inspector.
- VAULT: inspector **fed by hover** "immediate, no 0.4 s", click **opens** the chest
  (`vault.md:318-327, 336-338`); flagged as an open question at `vault.md:378-380`.
- BUILD: hover-driven DPS comparison kept in the readout (`build-loadout.md:242`).
- MAP / ROSTER / GEAR / TRAINING / WARREN: hover = highlight, click = select.
Three grammars; §15 and §99-13 ask for one.

### 2.5 The shared hint slot collides with eight per-screen top zones

`global-chrome-onboarding.md:281-285` reserves canvas `(450,86,1200,48)` and says "Screens keep
y 86–134 free of interactive content". I confirmed the rect (`Game1.cs:3226-3230`: x = 180 + 270,
y = 86). Converting each proposal's top zone to canvas y (×0.896 for overlay-space proposals):

| Proposal | Top zone (authored) | Canvas y | Collides |
|---|---|---|---|
| `warren.md:185` summary strip y 120–240 | overlay | 107–215 | yes |
| `training.md:205` champion strip y 118 | overlay | 106 | yes |
| `gear.md:271-272` panels from y 100 | overlay | 90 | yes |
| `vault.md:306` toolbar y 130 | overlay | 116 | yes |
| `roster.md:272` grid y 120 | overlay | 107 | yes |
| `traits.md:181` status band y 76–136 | overlay | 68–122 | yes |
| `mastery.md:292` status plate y 112 | overlay | 100 | yes |
| `forge.md:319` materials strip y 118–178 | true canvas | 118–178 | yes |
| `map.md:238` chart y 146 | overlay | 131 | marginal |
| `build-loadout.md:232` panels y 110 | true canvas | 110 | marginal |

And `global-ux-standard.md:550` puts the hint slot somewhere else again (y 70–96, Body, one line).
Two hint-slot positions, eight collisions — this is the single most consequential unshared decision.

### 2.6 Toast anchors — four

`global-ux-standard.md:308-309, 554` (under the header, 960/1050-centred, y 112, 900×64–96);
`global-chrome-onboarding.md:376-377` ((560,176,800,96) on menus, boot slot on HUNT);
`hunt.md:34` boot slot (630, HeaderStackBottom+8 = 212, 560, 88); `map.md:246-250` a world strip
inside the chart at (58,168,1294,48) for the conquest message. `expedition-log.md:262-264` also
wants the rail/pills *under* the log scrim, which changes what a toast may overdraw.

### 2.7 Gold usage — the reports do not agree on what §10 means

- Gold on a **blocker**: `warren.md:216-217` "REACH WAVE 95 ON A HUNT — Headline 24, gold" (a
  requirement is neither earned nor available).
- Gold on a **section heading**: `vault.md:343` "WHAT IT PROMISES — SectionLabel gold";
  `expedition-log.md:294` "SINCE YOUR LAST RUN HERE header Headline 24 Gold"; `hunt.md:281`
  "MAIN LIMIT — REACH Headline 24 Gold".
- Gold **only** on state: `training.md:199-200` (selected row, AFTER delta, hovered primary);
  `map.md:212-215` wants bronze/Vellum section heads; `global-ux-standard.md:205-207` says gold panel
  titles are furniture.
`global-typography-scale.md:499` defines `Accent` as "earned / selected / active / primary action —
never decoration". Warren's and Vault's uses break it.

### 2.8 PRIMARY tier means different things

Brief §9: PRIMARY = "important focal **interactive** surface". Assigned to: the operation column
(`forge.md:347`, interactive), the EQUIPPED doll panel (`gear.md:278`, display), the hunter status card
(`hunt.md:224`, read-only), the map chart (`map.md:238`, interactive), the loadout column
(`build-loadout.md:232`), and to **nothing** on ROSTER, TRAITS, MASTERY, WARREN, TRAINING, VAULT
(where the primary *button* carries the weight). Two of six named PRIMARY surfaces are not
interactive. `global-ux-standard.md:434` ("≤ 1 per screen + modals") is the rule; it needs the word
"interactive" enforced or dropped.

### 2.9 Requirement phrasing and the DEPTH / WAVE split

- `warren.md:128, 250-252` renames DEPTH → WAVE ("one word for one thing").
- `mastery.md:199-200, 311-312` proposes "NEXT POINT: **DEPTH** n IN <REGION>"; `MasteryPoints.FromDepth`.
- `hunt.md:273` "deepest **wave** 41"; `expedition-log.md:276` "12 WAVES CLEARED".
- Brief §80 itself says "REACH DEPTH 70". Unresolved; three screens would say it three ways.
- Phrasing shapes: `CONQUER UMBRAL REACH FIRST` (`map.md:281-282`), `CONQUER 2 MORE REGIONS`
  (`warren.md:205`), `REACH WAVE 50 IN CINDERWORKS FIRST` (`roster.md:170`), `11 / 20 WAVES IN
  CINDERWORKS` (`roster.md:308-309`), `LEVEL 2 — 11 MORE WAVES` (`build-loadout.md:251`), `MORE SLOTS —
  TRAITS` (`hunt.md:247`), `MERGE THREE TO REACH RARE` (`forge.md:390-392`). `global-ux-standard.md:409`
  names `RequirementView(text, met, progress?)` as the component; no report drafted its copy template.

### 2.10 One number, three labels; one count, two verdicts

- `Hunter.PowerRating` is `POWER 222` on HUNT (`hunt.md:225-226`), `GEAR POWER 397` on GEAR
  (`gear.md:281`), `YOUR POWER 210` on MAP (`map.md:259`), removed from TRAINING (`training.md:213`).
- `Hunter.HunterLevel = 1 + ranks/5`: GEAR drops it as "a training count" (`gear.md:119-120, 328`);
  HUNT shows `Lv 12` (`hunt.md:225`, brief §19); TRAINING shows `LEVEL 7 · 31 RANKS TRAINED` and keeps
  a LEVEL medal card (`training.md:205, 215`). Same dubious number, three treatments.
- HUNTER vs CHAMPION: `roster.md:213-215` flags; `hunt.md:254` "YOUR CHAMPION FELL";
  `training.md:205` "Champion strip"; `global-chrome-onboarding.md:318` "champion, Warren, traits";
  brief uses Hunter (§19, §62, §12). Nobody owns the decision.

### 2.11 Screen titles

`THE VAULT` kept (`vault.md:302`), `THE FORGE` kept (`forge.md:317`), but `CHAMPION GEAR → GEAR`
(`gear.md:274`), `THE ROSTER` panel title removed (`roster.md:207-208`), `STATS → TRAINING`
(`training.md:217`). Brief §6: "only the name of the screen"; the rail says VAULT / FORGE. The
article should go or stay everywhere. `global-ux-standard.md:550` also moves the title to x 1050
(content centre) while every per-screen proposal keeps (960, 24).

### 2.12 Rarity and Source colour-alone

`global-typography-scale.md:513-516` states the global rule "(colour + glyph) or (colour + word);
never colour alone". `forge.md:328-329` keeps bag rows as "6 px rarity bar, 36 px icon, name (Body)"
— rarity by colour only; `gear.md:188, 297` keeps "rarity = left edge" in the grid; `vault.md:223`'s
five-pip count is the only rarity encoding that passes. Cross-screen: the same item shows rarity three
ways (bar / edge / pips).

### 2.13 Class-name and fixture-name renames — three schemes

| Thing | build-loadout | legacy | chrome | ux-standard | mastery |
|---|---|---|---|---|---|
| `WeaveScreen` → | `LoadoutScreen` (:281) | `LoadoutScreen` (:232) | **`BuildScreen`** (:473) | `LoadoutScreen` (:373) | — |
| `FormHexDiagram` → | `StyleHexDiagram` (:281) | **`StyleAffinityDiagram`** (:234) | — | `StyleHexDiagram` (:375) | `StyleHexDiagram` (:332) |
| fixture `weave` → | `build`; old `build` → `mastery` (:282) | `build`; `buildtree` → `mastery` (:267) | — | `weave→build`, `buildtree→mastery` (:499) | — |
| `TutorialStep.WeaveBuild` → | — | `ChooseBuild` + read alias (:256) | keep name, retarget (:293-297) | — | — |
Chrome's `WeaveScreen → BuildScreen` collides with the existing `BuildScreen` (mastery) unless the
mastery rename lands first; brief §67 names `LoadoutScreen`.

### 2.14 DISCIPLINE → STYLE is not applied by everyone

`global-legacy-terminology.md:60-71` picks one word (STYLE) and gives 25 exact replacements
(`:280-317`). `mastery.md:292-295` still proposes a status-plate chip reading "DISCIPLINE · HAMMER ×2 /
NO DISCIPLINE YET"; `build-loadout.md:279-280` proposes "the style word + x2.0 YOURS tag";
`vault.md:194-196` defers. Two of three screens that speak the word would keep it.

### 2.15 The "weave" verb replacement copy differs

`global-legacy-terminology.md:50, 302`: `SLOT CLEARED.` / `SKILL EQUIPPED.`;
`build-loadout.md:279`: `SLOT ADDED / CLEARED`; `mastery.md:381-384` asks the owner whether weave is
retired at all. `global-ux-standard.md:357-359` recommends EQUIP / UNEQUIP / SLOT / LOADOUT.

---

## 3. CONTRADICTIONS between reports

1. **Overlay inset — remove now vs keep.** `global-ux-standard.md:125-131, 431, 547` and
   `forge.md:105-116, 312-314` and `warren.md:115` want menu screens authored in true 1920 space now;
   `global-typography-scale.md:388-393` says explicitly "Do it per screen, not globally" and keeps
   `BaseOverlayScale = 0.8958` at 100 % so "no screen moves when the feature lands" (`:296`). MAP,
   ROSTER, VAULT, TRAITS, WARREN, TRAINING, GEAR author in overlay space "until settled" (`map.md:233`,
   `vault.md:299-300`, `traits.md:177`); BUILD and FORGE author in true canvas. Both sets of
   coordinates cannot be implemented in one checkpoint.
2. **Settings modal geometry.** `global-typography-scale.md:355-362`: UI SCALE as a third dropdown at
   (860,354,524,58), panel grown to (335,70,1250,940). `global-chrome-onboarding.md:310-319`: panel
   kept at (335,100,1250,880), two-column layout (left 380,190,620,690; right 1040,190,500,480;
   danger zone 1040,700,500,180). Two layouts for one modal. Chrome also says "do not ship a dropdown
   that says 125 % and does nothing" (`:339`) while typography ships it gated (`:397-400`) —
   reconcilable, but the rects are not.
3. **Defeat banner.** `hunt.md:252-261`: new (560,190,800,220) plate with MAIN PRESSURE + LIMITING
   FACTOR lines and three buttons [ADJUST BUILD] [GEAR] [READ THE LOG], held until dismissed.
   `expedition-log.md:315-319`: keep the two-line plate at (500,214,920,110), line 2 = `MAIN LIMIT —
   REACH · 1 of 3`, plate is a click target, no buttons; the doors go in the **log footer**
   (`:300-306`). Also the label: `LIMITING FACTOR` / `MAIN PRESSURE` (hunt, brief §22) vs
   `MAIN LIMIT` (log, brief §24). And `expedition-log.md:87-91` shows `Verdict()` *is* the reach
   sentence — so hunt's design (`:255-256`) prints the same fact twice under two labels.
4. **Expedition Log panel.** `hunt.md:279` "keep panel 502,130,1042,800" vs `expedition-log.md:266`
   move to (360,90,1200,900). (`LogPanel` today is (502,130,1042,800), `SoloExpeditionScreen.cs:2072`.)
5. **Log chrome.** `expedition-log.md:100-103, 262-264` wants nav/pills/gear under the scrim;
   `hunt.md` keeps the current draw order. Not both.
6. **Welcome-back panel.** `hunt.md:268-277`: (660,240,600,~380), PRIMARY frame, held until CONTINUE,
   always (toast replaced). `expedition-log.md:321-325`: (610,300,700,400), only when `credited ≥ 60 s`,
   short-trip toast kept. Same component, two rects, two policies.
7. **Chest filter destination.** `hunt.md:237-238, 379` MOVES the CHEST FILTER row + popover to the
   Vault toolbar. `vault.md:306-316` (Zone B toolbar) has no filter control — only a consequence line
   — and `vault.md:381-382` leaves the pile's ownership open. The editor would be orphaned (the
   project's "dormant features" failure mode).
8. **Class rename target for the BUILD screen** — see §2.13 (`LoadoutScreen` ×3 vs `BuildScreen` ×1).
9. **FormHexDiagram name and fate.** `StyleHexDiagram` (`build-loadout.md:281`, `mastery.md:332`,
   `global-ux-standard.md:375` "only if the chart stays") vs `StyleAffinityDiagram` /
   `StyleRingDiagram` (`global-legacy-terminology.md:234`); `mastery.md:314-317` moves the chart
   *into* the inspector, `global-legacy-terminology.md:352` keeps the (1408,112,496,460) dock
   "RENAMED only".
10. **F1 `SOURCE VS REGION` row.** `global-chrome-onboarding.md:360-362, 460-461` REMOVES it;
    `build-loadout.md:127-128, 302` shows `SourceMatchup.Effectiveness` is a live rule drawn in the
    readout; I confirmed the row at `Game1.cs:5047`. Removing a true rule from the one help sheet
    contradicts brief §66 "UI must teach it".
11. **Ceremony copy line numbers.** `mastery.md:253` cites `BuildScreen.cs:1403-1406`;
    `global-legacy-terminology.md:29-30, 284-285` and `global-ux-standard.md:345-346` cite
    `:1398/1400`. Verified: **1398 and 1400**. Trivial, but the implementer will grep the wrong line.
12. **Help T-row line.** `global-chrome-onboarding.md:180` cites `Game1.cs:5062`;
    `global-legacy-terminology.md:87, 283` cites `:5065`. One of them is off.
13. **Fidelity of the 720 baselines.** `roster.md:34-35` "the same path PresentFit takes — so the numbers
    are what a 720p player sees"; `vault.md:294-295`, `hunt.md:368` agree. `global-typography-scale.md:46-50`
    says the copies are "very likely *sharper* than what the game shows" because the game resamples
    twice; `map.md:386-389` "not recorded". No tool in the repo made them (my check). Until a
    backbuffer capture exists (`global-typography-scale.md:545-548`, `gear.md:365-366`,
    `global-ux-standard.md:497`) every 720p number in every report is an estimate — the reports
    should say so uniformly.
14. **Ladder assumption under the layouts.** `global-typography-scale.md:414-426` moves Body 19 → 22,
    Secondary 16 → 19 and adds `Pitch()` = rung × 1.3 (`:452`). Every per-screen zone table is sized on
    the old ladder: `training.md:206` 9 rows × 64 px, `roster.md:277-284` card anatomy,
    `vault.md:318-327` card rows, `warren.md:193-203` card rows (`warren.md:225-228` half-anticipates),
    `map.md:239-244` 240×176 nodes. Under Body 22 / pitch 28 several of these overflow.
15. **Guide-strip successor.** `global-ux-standard.md:149-151` "other screens get their
    `Onboarding.BannerFor` line or nothing"; `global-chrome-onboarding.md:238-297` a per-screen hint
    line from live state via a new `Onboarding.HintFor`; `mastery.md:318-319` a contextual *toast* on
    entry; `traits.md:120-121` a rail badge. Four mechanisms for §12.
16. **Vault empty-state acquisition copy.** Brief §50 says "earned while hunting and through
    progression"; `vault.md:251` proves "and through progression" would be dishonest today (Warren
    pays currencies, trader sells items, only bosses and the welcome gift give chests). The brief's
    own example copy is stale here; only vault notices.

---

## 4. RISKY PROPOSALS (brief or architecture violations)

1. **Global ladder bump in P0 = the big-bang §98 forbids.** `global-typography-scale.md:395-400`
   ships the new ladder (`:414-426`) in P0 while its own P1-2 (`:192`) counts ~110 hand-set line
   pitches that "silently overlap" when a rung moves, and its `Pitch()` gate (`:452-455, 363-366`) is
   proposed but not sequenced first. Order must be: `Pitch()` + gate → convert pitches → move the
   ladder → per-screen passes. Otherwise every screen breaks at once with no screenshot to blame.
2. **Removing the 0.8958 inset globally** (`global-ux-standard.md:431, 547-556`; `forge.md:105-116`
   as a Forge-local fix) moves every panel on every screen in one change (`global-typography-scale.md:388-393`
   says exactly this). Same big-bang risk; and it is orthogonal to UI Scale, which the brief §4 asks for.
3. **Defeat banner held over the arena until dismissed** (`hunt.md:259`) — the champion restarts after
   1.6 s (`hunt.md:122-123`), so a persistent (560,190,800,220) plate with three buttons sits over the
   next descent's fight: §99-11 (combat dominant), §86 (avoid blocking). Expedition-log's click-target
   plate (`expedition-log.md:315-319`) is the safer shape.
4. **Hunter card as the one ornate PRIMARY on HUNT** (`hunt.md:224`) — a read-only status card given
   the focal frame on the screen where §16 says the HUD must not surround combat;
   `global-ux-standard.md:176, 196-198` wants HUNT's frames demoted to plates. Risk of re-boxing the
   arena.
5. **"OPENS AT WAVE N from `Unlocks.SkillSlots`" ghost row** (`build-loadout.md:240`) — slots 2–4 are
   wave-gated (`Unlocks.cs:234`) but the fifth is the `weave_5` trait (`DustEffects.cs:212-215`:
   `4 + (Owns("weave_5") ? 1 : 0)`). At capacity 4 the row would print an invented wave requirement.
   §92. `hunt.md:339` has the right source ("the node name needs a Dust-tree lookup").
6. **Vault click-to-open + hover-fed inspector + two buttons at the inspector foot**
   (`vault.md:318-327, 336-352`) — breaks §15 (click = select) and §84 (one primary); the report knows
   (`vault.md:378-380`). Also a hover-driven inspector is what `traits.md:102-103` condemns.
7. **TRAINING keeps nine per-row TRAIN buttons and adds an inspector [TRAIN]** (`training.md:206-207`)
   — ten primary buttons on one screen; `global-ux-standard.md:202-204` already calls the nine "nine
   gold claims". §84.
8. **GEAR hint "AN UNWORN ITEM MAY BEAT WHAT YOU WEAR"** (`global-chrome-onboarding.md:269`) — `gear.md:144-147`
   shows two rankings (PowerContribution halo vs bench-DPS verdict) disagree for weapons; chrome
   correctly says "do not ship without it", but it is in the table. §92.
9. **Welcome-back panel on every launch** (`hunt.md:268-277`, no threshold) — a modal after a
   30-second alt-tab; §99-12. Expedition-log's `credited ≥ 60 s` rule (`:322`) is the honest one.
10. **`WeaveScreen → BuildScreen`** (`global-chrome-onboarding.md:473-475`) — collides with the live
    `BuildScreen` class and contradicts brief §67's `LoadoutScreen`. Rename churn with a hazard.
11. **Map difficulty verdict word with an invented threshold table** (`map.md:259-261, 295`) — the
    report flags "a design decision"; brief §28 asks for the word but §92 forbids inventing the rule.
    Ship only with a design-owned table and a test.
12. **Moving the chest pile / openers out of `ForgeScreen`** (`vault.md:174-180, 381-382`) — a
    Game-assembly refactor that touches the reveal `forge.md:257-258` says must not regress. Right
    direction, wrong pass; schedule separately.
13. **Eleven shared components before the screen passes** (`global-ux-standard.md:404-418`) plus a
    global template (`:547-556`) — building `InspectorPanel`, `NodeInspector`, `RequirementView`,
    `NotificationToast`, `EmptyStateView`, `ScreenHeader`, `Plate`, `Button(style)`, `ItemGrid`,
    `CostLine`, `SetBonusLadder` first is framework-first; §91 warns, §98 forbids the bang. Extract each
    on its second real user during the screen passes (which the report's own §7.3 implies).
14. **`Density` float in `SmoothFont` + `ChromeMouse` page mapping** (`global-typography-scale.md:335-351`)
    — sound, but it changes `tools/check_mouse_space.py`'s entry-point detection (`:337-339`) and the
    RH_SHOT path; land it with the gate change in the same commit or CI fails.
15. **Duplicate diagnosis lines** (`hunt.md:255-256`) — `MAIN PRESSURE — <Verdict()>` and
    `LIMITING FACTOR — REACH · 1 of 3` are one fact from one if-chain (`expedition-log.md:87-91, 219`);
    printing both invents a second signal. Use `RunReport.MainLimit` + one sentence.
16. **Mastery on-canvas names for every non-minor node + "+6" on minors** (`mastery.md:300-303`) —
    brief §74 wants detail zoomed-in, fine, but at the whole-tree framing this is 60+ new labels on a
    canvas the report itself says is 20 px per minor (`mastery.md:38-49`); gate strictly on zoom.

Not risky, noted as good: every Core addition proposed is a pure function over existing state
(`RunReport.MainLimit` `expedition-log.md:219`; `Hunter.Preview` `training.md:236`;
`Career.NextTraitPoint` `traits.md:162`; `MasteryTree.WhyNot` `mastery.md:274`;
`Onboarding.HintFor` `global-chrome-onboarding.md:273-277`; `PowerVerdict` `map.md:295`). None couples
Core to MonoGame; none needs telemetry except the one everyone agrees to omit (BEST PERFORMER —
`hunt.md:322`, `expedition-log.md:223`, `global-ux-standard.md:466`).

---

## 5. Decisions the owner must make before any screen pass (in dependency order)

1. Inset: keep `BaseOverlayScale` at 100 % and re-author per screen (typography's plan), or remove it
   for all menu screens in one go. Every coordinate in eleven reports depends on this.
2. Ladder + `Pitch()` sequencing (risk 1).
3. One inspector column: rect, section names (`WHAT IT DOES / YOU NEED FIRST / COST` or brief §14's),
   CATEGORY/NAME rungs, CTA height (68 is the majority), one primary button, refusal line position.
4. One hint slot and one toast anchor (§2.5, §2.6) — and which mechanism answers §12.
5. Hover grammar: inspector follows click everywhere (traits' rule); vault's card stays click-to-open
   only if the owner accepts the exception and documents it in the standard.
6. Defeat banner + log doors + welcome-back: pick hunt's or expedition-log's design; one label
   (`MAIN LIMIT`), one threshold (≥ 60 s).
7. Vocabulary: HUNTER or CHAMPION; WAVE or DEPTH; POWER label; STYLE not DISCIPLINE (legacy §3b);
   weave → EQUIP; INNATE vs ALWAYS ON; article on VAULT/FORGE titles.
8. Class and fixture renames: adopt `global-legacy-terminology.md:228-272` as the single map
   (`LoadoutScreen`, `MasteryScreen`, `StyleHexDiagram`/`StyleAffinityDiagram` — pick one).
9. Gold law wording: "earned / selected / available / primary action" — and strike gold from
   requirement lines and section heads in the proposals that use it.
10. Fixture contract additions: a real 1280×720 backbuffer capture; rail-coherent seeding (§1.3);
    baselines for title, reveal, trader/paste, ceremony, tours, boss HUD, Forge tabs, hover, empty.
11. Brief corrections to record: Warren has no services/targeting/automation (§77–78, §82, §95);
    Vault empty-state copy "through progression" is untrue today (§50); §22/§24 use two labels for
    one diagnosis.
12. Chest-filter home (hunt → vault) and chest-pile ownership — decide together or the filter goes dormant.

---

## 6. What the set got right (keep across the reconciliation)

- The data-honesty tables (every report §"Data honesty") — the best artefact of the audit; they
  agree with each other everywhere they overlap (BEST PERFORMER needs telemetry; offline NOTABLE is
  impossible by design; EQUIP BEST = highest power).
- Legacy inventory: `global-legacy-terminology.md:276-319` is implementable as written and is
  consistent with the per-screen legacy sections (only DISCIPLINE and the weave-verb copy diverge).
- The measured-physical-size discipline (0.8958 × 0.667) is applied identically in all eleven screen
  reports; the numbers agree.
- Fixture gap lists are specific and actionable in every report.
- `global-typography-scale.md §5` is the only complete architecture answer to brief §4 and should be
  the reference the others are re-based on.
