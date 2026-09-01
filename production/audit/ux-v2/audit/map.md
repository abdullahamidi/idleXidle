# UX V2 audit — MAP screen

Auditor scope: `src/IdleXIdle.Game/MapScreen.cs` (hosted by `Game1.DrawWorld` / `PushMapState` /
`ConsumeMapRequests`, Game1.cs:4957-4987, 5016-5021) with the Core it renders: `Encounters/Regions.cs`,
`RegionLadder.cs`, `RegionModifiers.cs`, `RegionDrops.cs`, `Checkpoints.cs`, `Automation/RegionAutomation.cs`
(`Region`). Brief sections applied: 0-15, 26-29, 82-102 (esp. 100). Baselines:
`production/audit/ux-v2/baseline/map.png` (1920x1080) and `baseline/720/map.png` (1280x720, what a
720p player sees). Read-only on source; no `.cs` touched.

Runtime truth this is judged against: STYLE -> SKILL -> VARIATION -> REINFORCEMENTS with SkillId identity;
Form / WovenAbility / Aptitude / Source x Form deleted; Warren pays Gleam/Dust/Scrap/Essence; mastery
discovery permanent. The MAP itself carries none of the deleted vocabulary (see section 7).

---

## 0. Verdict in one line

**Does the screen answer "Where should I hunt next?" today?** Partly. The chart answers "where am I / what
is conquered / what is locked" at a glance and the inspector holds every fact a player needs (power gap,
enemy element + tempo, modifier, goal, drops) — but at 720p almost every one of those facts is drawn at a
5-7 px cap height, the inspector reserves ~160 px of empty space for two conditional sections, the
"available but not entered" state has no words at all, and a locked region says "conquer the previous
region" without naming it. The foundation is right (brief 26); the problem is size, hierarchy and the
three state gaps.

---

## 1. Measured readability (the P0)

### 1.1 The map draws SMALLER than its rungs say

Every menu screen is drawn through the overlay inset: `Game1.OverlayScale = (1920 - 180 - 20) / 1920 =
0.8958` (Game1.cs:5323, `NavRailWidth` Game1.cs:5305). `MapScreen` positions text in its own 1920-space
(`_ui.TextCenterBig(b, "MAP", 960, 24, ...)` MapScreen.cs:279) and the whole batch is scaled by 0.896 and
shifted right of the rail (Game1.cs:4029-4036). So on this screen the ladder actually lands at:

| Rung | nominal | on the 1080 canvas | at 1280x720 (x0.667) | measured cap height, 720/map.png |
|---|---|---|---|---|
| ScreenTitle 36 | 36 | 32.3 | 21.5 | "MAP" 12 px |
| PanelTitle 26 | 26 | 23.3 | 15.5 | "MARROW WASTES" 9 px |
| Headline 24 | 24 | 21.5 | 14.3 | "210" / "845" 8 px |
| Body 19 | 19 | 17.0 | 11.3 | "BODY ENEMIES" 7 px, modifier blurb 7 px |
| Secondary 16 | 16 | 14.3 | 9.6 | "YOUR POWER" 6 px, "RECOMMENDED POWER" 5 px, node name 5 px, "HELMS" 5 px |
| Caption 14 | 14 | 12.5 | 8.4 | (checkpoint chips — not in baseline) |

Measured with a local-contrast row scan of `baseline/720/map.png` (1080 equivalents: node name 9 px,
"YOUR POWER" 8 px, Body rows 10-11 px). For comparison the chrome, which is NOT inset, lands where its
rung says: the nav tile "MAP" (NavigationLabel 21) measures 12 px cap at 720p — twice the inspector's
labels.

**Brief 5 asks for an effective Body of ~24-26 and Secondary ~20-21 (1080 authoring px).** On this screen
Body is effectively 17 and Secondary 14.3 — both would need to grow ~45% just to reach the brief's
floor, before any UI-Scale option. This is the single most important finding: whatever the global
typography fix is, it must include the 0.896 overlay penalty or the menu screens will still miss the
target after the ladder is raised.

### 1.2 What is illegible at 720p (evidence: `720/map.png` and 3x crops of it)

- **Region node names** — `_ui.TextCenterBig(b, def.Name, ... UiTypography.Secondary)` MapScreen.cs:377.
  5 px cap height. The name of the place is the smallest rung on a 196x146 card (117x87 px at 720p).
- **Element label on the node** ("NATURE") — MapScreen.cs:389, Secondary, 5 px.
- **State band words** ("CONQUERED", "LOCKED", "YOU ARE HERE") — MapScreen.cs:407/416/424, Secondary in a
  24 px band. Readable only by their colour + the tick / padlock glyph.
- **"POWER 1,370" under a locked node** — MapScreen.cs:395-396 draws it at `Vellum * 0.55f`. In the 720
  crop it is a faint smear on the parchment (3 rows of contrast detected). The power ask of a locked
  region is exactly the number a player planning ahead wants; dimming it to 55% is brief 7's "secondary
  looks disabled" in its purest form.
- **Locked node name in Slate** ("THE PALE CHOIR", MapScreen.cs:377 `unlocked ? Bone : Slate`) — the same
  Slate the inspector uses for its ordinary labels (MapScreen.cs:479).
- **Subtitle** "EACH REGION IS ABOUT 62% TOUGHER THAN THE LAST, AND DROPS ITS OWN THINGS" —
  MapScreen.cs:289-290, Secondary Slate, 5 px cap at 720p. A sentence at the second-smallest rung under
  the biggest text on the page.
- **Inspector labels** "YOUR POWER" / "RECOMMENDED POWER" — MapScreen.cs:479/482, Secondary Slate, 5-6 px.
- **Tempo line** "IT HITS SLOWLY AND HARD" — MapScreen.cs:500-505, Secondary Slate, right-aligned on the
  same row as "BODY ENEMIES". 5 px. This is the one line that tells a build whether it faces a few heavy
  hits or a flurry, and it is the quietest text in the panel.
- **Modifier blurb** "Enemies have +25% health and +10% damage." — MapScreen.cs:512-516, Secondary
  Slate, 7 px. The most decision-relevant sentence on the screen is at footnote size.
- **DROPS rows** "HELMS / CHEST ARMOUR / BOOTS" — MapScreen.cs:562, Secondary at a 21 px pitch with
  19 px glyphs (MapScreen.cs:542, 561). 5 px cap at 720p.
- **Checkpoint chips** (conquered regions only, not in baseline) — labels at `UiTypography.Caption`
  inside 26 px chips (MapScreen.cs:150-160, the `DrawCheckpoints` chip loop). At 720p that is an 8.4 px
  em: a "110" chip you cannot read, and it is a control the player must click.
- **Corruption ladder blurb** (all-conquered only) — `_ui.TextCenter(b, look.Blurb ...)` MapScreen.cs:585
  is the unsized Label = Body 19 -> 11 px em at 720p.

### 1.3 What IS readable and must be kept as the anchor

- "MAP" title (12 px cap at 720p), the region title in its Source colour (9 px), the two power figures
  (8 px), the CTA label (7 px, `TextFace.Strong`). These sit at the right rungs; everything under them is
  one or two rungs too small. Build the hierarchy down from these, not up from the small text.

---

## 2. Hierarchy — where the eye lands (both baselines)

1. The ornate gold map frame and the gold **YOU ARE HERE** node — correct: the chart is the primary
   surface (brief 9 PRIMARY, brief 26).
2. The red-tinted identity plate with the 80 px Body gem in the inspector (MapScreen.cs:459-470) — a
   decorative element wins over every fact under it.
3. The **210 / 845** pair — the right second read, but the colours (Ember red vs Violet) carry the
   verdict alone (brief 8) and Violet on RECOMMENDED means nothing (MapScreen.cs:483).
4. Everything else is a uniform wall of Slate/Bone Secondary lines with gold sub-headings; nothing in
   the ENEMY / GOAL / DROPS blocks is louder than anything else.
5. **RESUME HERE** sits at the very bottom, separated from the last content by ~110 px of void
   (`720/map.png`: DROPS list ends y~485, button starts y~560).

Frames compete only mildly: the chart is `_ui.Panel` (ornate, MapScreen.cs:306) and the inspector is
`_ui.PanelQuiet` (MapScreen.cs:449) — that is the right PRIMARY/SECONDARY split and must be kept. The
six node frames add a third frame family (3-4 px flat rectangles in five colours, MapScreen.cs:361-369);
acceptable because they are state carriers, but "available" (unlocked, unconquered, not active) has no
band at all — the frame is the Source colour and nothing says AVAILABLE (MapScreen.cs:404-425 has
branches for active / conquered / locked only).

---

## 3. Space use (brief 28)

Inspector `DetailPanel = (1412, 146, 474, 880)` (MapScreen.cs:81). Content today, in panel-relative Y
(1920-space):

| Y | Section | Notes |
|---|---|---|
| 22 / 56 | title / description | fine |
| 92-192 | identity plate 100 px | 80 px gem; duplicates the gem on the node, in ENEMY THEME (34 px, :490) and in DROPS (22 px, :554) — the gem appears **four** times per region |
| 210-270 | YOUR POWER / RECOMMENDED | ok |
| 292 | divider | |
| 308-430 | ENEMY THEME + tempo + modifier + blurb | tempo crammed onto the enemy row |
| 470-520 | GOAL | |
| **526-576** | START AT WAVE chips — **only when conquered** (:524) | in the baseline this 50 px is blank |
| 590-675 | DROPS (3 rows max, :545) | |
| **675-788** | **empty** in the baseline — reserved for the corruption ladder, drawn only when `World.AllConquered` (:580-591, Bottom-204..-104) | 110 px blank for the whole non-endgame game |
| 788-856 | CTA 68 px | |

Two conditional sections are laid out as fixed holes (MapScreen.cs:139 comment "the chips must clear
the DROPS heading at Y+590"; :544 `dropsFloor = Bottom - (AllConquered ? 204 : 108)`). In the ordinary
state (region in progress) ~160 px of the 880 are blank while the text that is present is one rung too
small — exactly the pattern brief 28 names. The chart has slack too: nodes are 196x146 on a 1342x880
field with ~200 px gaps between columns (`NodeFrac` 0.20 / 0.50 / 0.80, MapScreen.cs:85-87); a 240x176
node fits without any connector rework.

---

## 4. Findings by priority

### P0 — readability / hierarchy

- **P0-1 Overlay inset shrinks every rung 10.4%.** Game1.cs:5323 `OverlayScale`; MapScreen draws in
  1920-space (MapScreen.cs:279). At 720p Secondary = 9.6 px em, Body = 11.3 px. The fix belongs to the
  global typography / UI-scale work but must be counted for every inset menu screen.
- **P0-2 Node text at Secondary** (name :377, element :389, band :407/416/424, POWER :395). 5 px cap at
  720p. Raise the name to Body/Headline, element and band to Body; grow nodes to ~240x176.
- **P0-3 Locked POWER at `Vellum * 0.55`** (:396) and locked name in Slate (:377) — unreadable, and
  indistinguishable from "secondary" (brief 7). Keep full Vellum; say LOCKED with the band and padlock,
  not by dimming the number.
- **P0-4 Inspector body at Secondary/Slate**: labels :479/:482, tempo :500-505, blurb :512-516, DROPS
  rows :562, checkpoint cost :143-144, chips :160 (Caption). All should be Body or above; only true tags
  (a "BODY" pill, "+1 LOOT TIER") may stay at Caption (brief 6).
- **P0-5 Power verdict is colour-only** (:480-481 `HunterPower >= RegionPower(def) ? Met : Ember`).
  Brief 8: add the word. Derivable today from two existing numbers (see section 6).
- **P0-6 Two reserved holes leave ~160 px empty** while text is tiny (:524, :544, :580). Let blocks
  flow; give the reclaimed height to the rungs.

### P1 — layout / UX

- **P1-1 Locked state does not name the requirement.** Node shows only "LOCKED" (:424); the inspector
  shows a disabled grey "ENTER THIS REGION" plus a red unsized caption "CONQUER THE PREVIOUS REGION TO
  UNLOCK" (:572-574); the click-through message is the same generic text (Game1.cs:4983). Brief 27 / 29 /
  83: `CONQUER UMBRAL REACH FIRST`, and no disabled mystery button. The prerequisite's name is one lookup
  away: `def.PrereqId` -> `Regions.Get(...).Name` (Regions.cs:18, 90). Its progress exists too:
  `World.RegionFarm(prereq).BestDepth` vs `Checkpoints.ConquestWave` (RegionAutomation.cs:77,
  Checkpoints.cs:189).
- **P1-2 No "available" state.** An unlocked, unconquered, non-active region gets a Source-coloured
  frame and no band (:404-425). This is the "A NEW REGION IS AVAILABLE" moment (brief 12) and it is
  silent on the chart. Add a band ("AVAILABLE" / "NEW") and the CTA word `HUNT HERE` (brief 29) — today
  the CTA says `ENTER THIS REGION` (:572). The tour card text hard-codes "ENTER THIS REGION"
  (Onboarding.cs:346) and must move with it.
- **P1-3 Corruption ladder lives inside the region inspector** (:580-591) though it is a WORLD setting
  (`World.CorruptionTier`, Regions.cs:155). When all is conquered the inspector has three buttons
  (SHALLOWER, DEEPER, RESUME/ENTER) — brief 84 wants one primary action. Move it to a world strip along
  the chart's top edge (zone B' in section 5); the tour card that says "a corruption ladder appears above
  this button" (Onboarding.cs:346-347) changes accordingly.
- **P1-4 Guide banner teaches STATS over MAP.** `720/map.png` bottom: "YOU HAVE GLEAM TO SPEND — Press V
  for STATS...". `DrawGuideBanner` (Game1.cs:3109-3115) draws whichever `Tutorial.Showing` step is live on
  every screen; nothing in `Tutorial.cs` is MAP-scoped. Brief 11 / 12 violation (global P0 #4 in brief 97).
  MAP's own contextual line already exists as data: `_conquerMsg` "... UNLOCKED — MAP (W)." (Game1.cs:3733).
- **P1-5 Message band and guide banner overlap.** The map's message band is at `MapCanvas.Bottom + 22`,
  60 px tall (MapScreen.cs:438) -> canvas y 939-993 after the inset; `GuideBannerRect` for a two-line
  step is y 974-1054 (Game1.cs:3142-3148). A player who just conquered a region and still has a guide
  step sees the two strips print through each other. Also the live text says "MAP (W)." while the player
  is already on the map (Game1.cs:3733; the `world` fixture text at 1895 says "OPEN THE MAP (W)").
- **P1-6 Tempo fact hidden on the enemy row** (:500-505). Give it its own Body row; `AttackBias`
  (Regions.cs:21) is a real per-region fact and currently the least visible one.
- **P1-7 Gem drawn four times per region** (node :387, plate :469, enemy row :489, drops :553). Keep one
  large one (the plate) plus the node; drop the two small repeats and merge "BODY ITEMS" into the DROPS
  heading. Brief 99 laws 4 and 15.
- **P1-8 Subtitle is a permanent lesson** (:289-290) that the Map tour's first card already teaches
  verbatim (Onboarding.cs:337-339). Brief 6 says the title band is only the screen's name; brief 99 law 4.
  Remove.
- **P1-9 Checkpoint affordability by colour only** (chip text `afford ? Bone : Dim` :160; cost in Ember
  :144). Add "NOT ENOUGH DUST" in words or strike the chip; and the chip row is a Caption-sized control
  the player must click — brief 6 says Caption is for tags.
- **P1-10 Recommended-power colour Violet** (:483) has no meaning in the palette (brief 10 applies to
  every accent). Use Bone/Vellum; let the verdict word carry the judgement.

### P2 — polish

- **P2-1 Hover has no state on nodes** (frame colour ignores `hit`; only chip edges react, :155).
  Brief 15 wants a hover highlight; a 2 px Bone edge or a brightened scrim is enough.
- **P2-2 Second click travels** (:340-347). A hidden shortcut; keep, but never let it be the only way (it
  is not — the CTA remains). Document it in the UX standard as MAP-specific grammar.
- **P2-3 Gold section headings** ("ENEMY THEME", "GOAL", "DROPS", "START AT WAVE" :488/:519/:548/:140)
  use Hearth Gold for labels that are neither selected, earned nor actionable. This is the house
  convention (`UiTypography.cs` line 40 "gold sub-headings"); decide globally, but on this screen gold
  already means YOU ARE HERE and conquered connectors, so bronze/Vellum headings would sharpen it.
- **P2-4 Modifier name in Ember** (:508) reads as a warning although half the modifiers are also "better
  loot" (RegionModifiers.cs:25-30). Use the Source colour or Bone with a "+1 LOOT TIER" tag.
- **P2-5 Stale doc comments**: MapScreen.cs:17-25 still describes "a campaign-progress column", "per-
  region mastery and idle efficiency" and "the boss power tier", all deleted (:71-77 and :472-476 explain
  the deletions). Regions.cs:110 "(mastery, team, automation stage)" — "team" is squad-era. Game1.cs:142
  "(spec rev 1)". None reach the player.
- **P2-6 Message band is a hand-built strip** (`_ui.TextCenter` :443, fills :439-442) — should be the
  shared `NotificationToast` (brief 90) once that component exists.
- **P2-7 Region mastery is invisible.** `Region.MasteryLevel` (RegionAutomation.cs:107-116) exists, the
  fixture even sets it (Game1.cs:1905), and it drives the loot tier via `RegionProgressionOf`
  (Game1.cs:3740). If it changes rewards it deserves one Body line on a conquered region; if the design
  no longer wants it surfaced, law 14 says leave it out. Design call, flagged.

---

## 5. Proposed layout

All zones in 1920x1080 logical px in MapScreen's own authored space (they land at x0.896 right of the
rail until P0-1 is resolved globally).

**Zone A — title band (y 0-100).** "MAP" ScreenTitle at (960, 24). Subtitle REMOVED. Nothing else.

**Zone B — the chart, unchanged rect `(34, 146, 1342, 880)`, ornate `Panel`.** Keep backdrop, scrim,
serpentine, dotted connectors (gold once conquered). Nodes grow to **240x176** centred on the same
`NodeFrac` points: emblem 56 px at top; NAME at Body (Bone — locked is Bone too, not Slate); element row
(30 px gem + name in Source colour, Body); a **32 px state band** at Body: `YOU ARE HERE` (gold),
`CONQUERED` (green + tick), `AVAILABLE` (Bone, new), `LOCKED` (padlock, Vellum). Under the node two
lines in Vellum Body: `POWER 1,370` (full alpha always) and, for a locked node only, `CONQUER UMBRAL
REACH`.

**Zone B' — world strip inside the chart's top edge `(58, 168, 1294, 48)`, quiet translucent row, drawn
only when it has content.** Holds what is about the WORLD, not a region: the conquest / unlock message
(MOVED from the band under the chart, so it never meets the guide banner) and, once
`World.AllConquered`, the corruption ladder MOVED out of the inspector: `CORRUPTION 3 / 5 · FEVERED`
(Body) with 40 px `SHALLOWER` / `DEEPER` at its right end. Nodes start at y=284 so nothing is covered.

**Zone C — inspector, unchanged rect `(1412, 146, 474, 880)`, `PanelQuiet`, blocks FLOW (no reserved
holes), following the brief 14 skeleton:**
- C1 `Y+22` CATEGORY line, Secondary: `REGION 4 OF 6 · BODY · HEAVY` (Source pill and tempo word as
  tags — Caption allowed here).
- C2 `Y+48` NAME, PanelTitle in the Source colour (as today).
- C3 `Y+84..164` IDENTITY plate 80 px (arena crop + ONE 64 px gem) with the one-line description moved
  beneath it at Body.
- C4 `Y+190..290` POWER: `YOUR POWER 210` / `RECOMMENDED 845` — labels Body Vellum, values Headline;
  third row **verdict word** at Headline (`UNDERPOWERED — 635 SHORT` / `MATCHED` / `OVERPOWERED`) once a
  threshold table is agreed (see section 6). RENAME "RECOMMENDED POWER" -> "RECOMMENDED".
- C5 `Y+306..420` ENEMIES: `BODY ENEMIES` Body Bone; `HEAVY — slow, hard hits` Body on its own row;
  modifier name Body in Source colour + `+1 LOOT TIER` tag when `LootTierBonus > 0`; blurb Body, 2 lines.
- C6 `Y+436..520` GOAL: `HOLD 20 WAVES TO CONQUER` Body + progress `BEST WAVE 14 OF 20` (Body,
  `Region.BestDepth`); when conquered: `CONQUERED` tick + the checkpoint row as **34 px chips at Body**
  with the cost line (`FREE` / `250 DUST PER DESCENT`, plus `NOT ENOUGH DUST` in words).
- C7 `Y+536..660` DROPS: heading `DROPS · BODY ITEMS` (one 22 px gem at left); rows at 32 px pitch,
  Body, 24 px glyphs; `+8% RARITY` only if the design wants `RarityTilt` shown (it exists; brief 92
  says never invent — this would not be invented).
- C8 `Y+676..770` REQUIREMENT (locked only): `LOCKED` Headline; `CONQUER UMBRAL REACH FIRST` Body;
  `UMBRAL REACH — BEST WAVE 12 OF 20` Body. For unlocked regions this block is absent and C7 simply
  ends earlier.
- C9 `Y+788..856` PRIMARY ACTION 68 px: `RESUME HERE` (active) / `HUNT HERE` (available or conquered) —
  ONE button. Locked: **no button**; C8 is the answer (brief 29). The corruption buttons are gone from
  here.

**REMOVED**: subtitle sentence; three of four gems; disabled locked CTA and its red unsized caption;
Violet on the recommended figure; the 0.55-alpha dim on locked POWER; the empty reserved holes.
**MOVED**: corruption ladder and the conquest message -> Zone B'; tempo -> its own row; START AT WAVE
-> inside GOAL; identity sentence -> under the plate.
**RENAMED**: `ENTER THIS REGION` -> `HUNT HERE`; `CONQUER THE PREVIOUS REGION TO UNLOCK` -> `CONQUER
<NAME> FIRST`; `BODY ITEMS` -> `DROPS · BODY ITEMS`; `IT HITS SLOWLY AND HARD` -> `HEAVY — slow, hard
hits`; `RECOMMENDED POWER` -> `RECOMMENDED`.
**Interaction**: click selects, CTA commits (one click, no confirm — travel is reversible, brief 85);
Left / Right / Enter keyboard kept (MapScreen.cs:256-264); D / S for the ladder kept but bound to Zone B'.

---

## 6. Data honesty (brief 92) — what the Core already computes

| Display the brief wants | Status | Source |
|---|---|---|
| YOUR POWER | EXISTS | `HunterProgression.PowerRating` (HunterProgression.cs:239), pushed at Game1.cs:4964 |
| RECOMMENDED power | EXISTS | `Regions.RecommendedPower` (Regions.cs:44-57), same ruler as PowerRating |
| Difficulty WORD | **NOT computed** — derivable from the two numbers with a threshold table (new small pure helper in Core, e.g. `PowerVerdict.For(have, need)`); no telemetry. A design decision, not something the screen should invent. | — |
| ENEMY THEME (element) | EXISTS | `RegionDefinition.Theme` (Regions.cs:15) |
| Tempo (heavy / fast / even) | EXISTS | `RegionDefinition.CombatBias` (Regions.cs:21) |
| REGION MODIFIER name / effect | EXISTS | `RegionModifiers.For(id)` -> `Name, Blurb, EnemyHealthMult, EnemyDamageMult, LootTierBonus` (RegionModifiers.cs:14, 34) |
| GOAL (waves to conquer) | EXISTS | `Checkpoints.ConquestWave` (Checkpoints.cs:189), mirrored by `Game1.ConquerWaveDepth` -> `MapScreen.ConquerWaves` |
| GOAL progress (best wave here) | EXISTS, not shown | `Region.BestDepth` (RegionAutomation.cs:77) via `World.RegionFarm(id)` |
| Conquered / unlocked state | EXISTS | `World.IsConquered`, `World.IsUnlocked` (Regions.cs:126-135) |
| Locked requirement naming the region | EXISTS, not shown | `RegionDefinition.PrereqId` -> `Regions.Get(prereq).Name` (Regions.cs:18, 90) |
| Unlock progress for a locked region | EXISTS, not shown | prereq farm's `BestDepth` vs `ConquestWave` |
| DROPS (favoured slots) | EXISTS | `RegionDrops.For(id).Favoured`, `PlainName` (RegionDrops.cs:65, 148) |
| Loot bonus | EXISTS, not shown | `RegionDropProfile.RarityTilt` (RegionDrops.cs:84), `RegionModifier.LootTierBonus` |
| Checkpoint options and Dust cost | EXISTS | `Checkpoints.Options / DustCost / Clamp` (Checkpoints.cs:198-215); `DustOwned` pushed Game1.cs:4966 |
| "A NEW REGION IS AVAILABLE" hint | EXISTS as state | `IsUnlocked && !IsConquered && id != ActiveRegion`; the event text is `_conquerMsg` (Game1.cs:3733) |
| Corruption tier / can deepen / ease | EXISTS | `World.CorruptionTier, CanDeepenCorruption, CanEaseCorruption` (Regions.cs:155-171), `CorruptionLook.Label` (CorruptionLook.cs:33) |
| Region mastery level | EXISTS, not shown | `Region.MasteryLevel` (RegionAutomation.cs:107) |
| Recommended / "best" region | **DO NOT SHOW** — nothing computes it; brief 92 names it explicitly. The only honest marker is structural: "NEXT ON THE CHAIN" = first unlocked, unconquered region in `Regions.All` order. | — |
| Diagnosis / before->after / offline summary | not this screen's question | — |

---

## 7. Legacy vocabulary

Grep of `Form|Weave|Woven|Aptitude|Source x Form` over MapScreen.cs and the six Encounters files: **no
player-facing hits**. The screen already speaks the current model (Source as element, regions, waves,
Dust checkpoints). Notes for the global cleanup, with lines:

- MapScreen.cs:17-25 — doc remark describes deleted panels ("campaign-progress column", "idle
  efficiency", "boss power tier"). Comment only.
- Regions.cs:110 — "(mastery, team, automation stage)": squad-era "team". Comment only.
- HunterProgression.cs:239 — `PowerRating` is built from `SquadDamageMultiplier / SquadSkillRate /
  SquadHealthMultiplier`: squad-era MEMBER NAMES feeding the map's YOUR POWER. Internal, but these are
  the class/member names the brief asks to list.
- Reachable FROM the map via F1 (out of area, flagged because a map player will read it): Game1.cs
  `DrawHelp` prints "WOVEN SKILLS" (Game1.cs:5041), "SOURCE x FORM x VOW" (5042), "FORM IS HOW YOU FIGHT"
  (5046), and its comment names the "WEAVE" screen (5055-5056). These are live player-facing Form/Weave
  strings.
- Fixture names `weave` for Activity.Build and `dust` for Activity.Traits (Game1.cs:477, 483;
  capture.sh mode list) — stale screen names in the fixture vocabulary (brief 101).
- `TutorialStep.WeaveBuild` / `TutorialFacts.SkillsWoven` (Tutorial.cs:182, 242) — internal names; the
  shown title "THE BUILD IS THE GAME" is fine.

---

## 8. Keep (good, preserve)

- The chart as the primary surface: illustrated `bg_mapfield` backdrop, scrim, six-node serpentine,
  dotted connectors that turn gold when conquered (MapScreen.cs:306-330). Brief 26 exactly.
- Nodes wear the region's own arena crop (`ArenaKey`, `CentreCrop`, :354-356) and a per-region emblem
  (:371-375); the state is said in WORDS on a band plus a glyph (:398-425) — brief 8 done right for three
  of the four states.
- Element shown as gem + name in the Source colour (:379-389) — colour, glyph and text together.
- Right-side inspector on `PanelQuiet` against an ornate chart — the PRIMARY/SECONDARY split (brief 9).
- YOUR POWER placed on the row above the number it is compared with (:477-483 rationale).
- The inspector's section ORDER already matches brief 28 (power, enemy, modifier, goal, drops, CTA).
- Travel is one safe click that returns to the hunt (`_showWorld = false`, Game1.cs:4982) — the
  OBSERVE -> CHANGE ONE THING -> RETURN loop (brief 1, law 5). Keyboard path complete (:256-264).
- Every value is real (World / Regions / RegionLadder / Checkpoints) and the subtitle's figure reads the
  ladder rather than a literal (:289) — the data-honesty discipline is already here.
- Contrast notes baked into the code (Vellum on parchment, not Slate — :326-328, :393-394).
- Deterministic fixtures exist and are used (`map`, `world`, `conquered`, `corrupted`, `tour ... Map`).
- Core tests pin the model the screen shows: `WorldTests` (chain, unlock, conquer, corruption),
  `checkpoints_test`, `RegionModifiersTests` (tests/unit/IdleXIdle.Core.Tests/Encounters/).

---

## 9. Fixtures

**Exist today** (Game1.cs, `RH_SHOT_MODE`; `tools/asset-pipeline/capture.sh` modes):
- `map` (Game1.cs:1898-1912): Verdant, Cinderworks, Umbral conquered; Marrow Wastes active AND
  selected; Still Archive + Pale Choir locked; 131.9M Gleam, 77.4K Dust; Marrow mastery 650 points.
  This is the baseline. Because Gleam is huge the STATS guide banner is forced onto the shot (P1-4).
- `world` (1891-1897): Verdant conquered, Cinderworks unlocked, conquest message band shown.
- `conquered` (1863-1869): all six conquered -> corruption ladder in the inspector.
- `corrupted` (1870-1877): all conquered at tier 3 (FEVERED) -> SHALLOWER / DEEPER both live.
- `region2` / `region3` (1913-1925): travel fixtures for the HUNT, not map shots.
- `tour <shot> Map <n>` (ShotMode Game1.cs:482, Onboarding.cs:335-348): the three Map tour cards over
  the `map` fixture.

**Gaps against brief 95 "MAP (selected + locked)" and the proposal above:**
1. No fixture poses the inspector on a **locked** region — `_selected` is private and only
   `SelectActive()` exists (MapScreen.cs:267-271). Needs a dev hook (`DevSelect(regionId)` like
   `_roster.DevSelect`, Game1.cs:2144) and a `maplocked` mode selecting `still_archive`.
2. No fixture poses a **conquered** region's inspector (checkpoint chips, cost, unaffordable chip):
   `mapconquered` = `map` + select `cinderworks` + `RestoreBestDepth(40)` + Dust low enough that the 40
   chip is unaffordable.
3. No fixture poses an **available** (unlocked, unconquered, not active) region — needed for the new
   band and `HUNT HERE`: `mapavailable` = conquer Verdant, stay active in Verdant, select Cinderworks.
4. `map` sets `_deepestEver = 24` but never `RestoreBestDepth` on `marrow_wastes`, so a GOAL progress
   line would read 0 of 20. Add `RestoreBestDepth(14)`.
5. The message-band + guide-banner collision (P1-5) has no fixture: `world` shows the band, but the rig
   treats tours as explained; a flag to force a guide step alongside `world` would photograph it.
6. 720p: `capture.sh` renders the 1920 canvas; the `baseline/720/*.png` are 1280x720 RGBA copies. Whether
   they came through the game's own `PresentFit` bilinear path or an image resize is not recorded — the
   fixture should capture through the presenter at a 1280x720 window so the review sees the real filter
   (brief 94, 96).
7. Brief 95 wants real domain data — it has it; keep the Gleam figure but drop it below the SpendGleam
   threshold (or dismiss the rung) so the MAP shot is not wearing the STATS lesson.

---

## 10. Cross-screen consequences to schedule with this pass

- Onboarding.cs:341-347 (Map tour cards 2 and 3) quote "ENTER THIS REGION" and "a corruption ladder
  appears above this button"; both change with P1-2 / P1-3.
- Game1.cs:3733 / 4983 / 1895 message strings say "MAP (W)" / "CONQUER THE PREVIOUS REGION" — rename with
  P1-1 and stop saying "open the map" when the map is the screen showing it.
- The guide banner (Game1.cs:3109-3148) needs the per-screen relevance rule from brief 12 before any MAP
  screenshot can be signed off at 720p.
