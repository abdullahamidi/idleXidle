# TRAITS screen — UX V2 audit (2026-09-01)

**Area**: TRAITS (nav tile TRAITS, key P). Class `src/IdleXIdle.Game/PrestigeScreen.cs` (1395 lines) over
`src/IdleXIdle.Core/Prestige/{MemoryDust,TraitRoads,DustEffects,TraitTreeLayout,MemoryDustText}.cs`.
**Brief sections applied**: 0-15, 42-46, 82-102 (esp. 100: *"What permanent Hunter identity am I creating?"*).
**Pixels inspected**: `production/audit/ux-v2/baseline/dust.png` (1920x1080) and `baseline/720/dust.png` (1280x720).
**Runtime truth assumed**: post-2026-08-31 refactor — Form / WovenAbility / Aptitude / Source x Form are deleted;
the tree spends TRAIT POINTS derived from world progress (`Career.TraitPointsEarned`, `Career.cs:56-62`), not Dust.
**Source was not modified.** This file is the only artefact written.

---

## 0. Does the screen answer its question today?

**Partly.** It answers *"what can I buy and what does it do"* very well — the inspector's WHAT IT DOES / WHAT IT COSTS /
YOU NEED FIRST / `Permanent. Never resets.` sheet is the best-structured inspector in the game, and the four roads with
their one-line identities (`TraitRoads.cs:36-48`) are the right framing for "identity". It does **not** yet answer
*"what identity am I creating"* because (a) the default framing is the whole 51-node tree at zoom about 0.556, where every
name is 9 px and every cost tag 6 px at 720p, so the player sees a constellation rather than a decision; (b) the
screen's only progress readouts are totals a player can never reach (`226 FOR EVERYTHING`, `45 POINTS` per road,
against a hard cap of 34 earnable points — `MemoryDustTests.cs:247-249`); (c) the two irreversible identity choices —
a 12-point terminal and Enter-to-buy — are one unconfirmed click, the same grammar as a 1-point spine node (§46, §85).

---

## 1. What is GOOD and must be kept

| Keep | Evidence |
|---|---|
| Starfield + free camera + world-unit tree ("permanent progression atmosphere", §42) | `Game1.cs:354` (`bg_constellation`), `PrestigeScreen.cs:25-33`, `TraitTreeLayout.cs` (Core, engine-free, tested for overlap) |
| Road grouping with effect-first identity sentences owned by Core and pinned by test | `TraitRoads.cs:36-48`; `TraitNamesTests.test_a_road_identity_agrees_with_what_its_nodes_do` |
| Three-channel node dress — frame = kind, glyph = road, tint = state — shared by tree and inspector so a node "looks like itself" in both | `PrestigeScreen.cs:337-402`, `DrawFace` 1101-1154 |
| Inspector sheet order WHAT IT DOES, WHAT IT COSTS, YOU NEED FIRST, `Permanent. Never resets.`; prerequisite rows with tick + `learned / not yet`; honest truncation `and N more — hover them on the tree` | `PrestigeScreen.cs:1287-1357`; words come from `MemoryDustText.Sheet` so tests hold them (`MemoryDustTextTests.cs:105`) |
| Names are the effect (`HARDER HITS I`, `AUTO-SELL COMMON DROPS`), descriptions at most 2 plain sentences with the real number, banned-word list | `MemoryDust.cs:294-310`; `TraitDescriptionsTests.cs:25,33,47,64` |
| One wire per node to its nearest prerequisite, gold once both ends are lit, dashed when it leaves the neighbourhood | `PrestigeScreen.cs:786-825`, `1188-1217` |
| Name plates on a dark plate with balanced (not greedy) line breaking; hit box = frame plus plate | `PrestigeScreen.cs:936-1021`, `1044-1059` |
| Zoom about the pointer, drag-to-pan, Home resets, pan clamped to the world | `PrestigeScreen.cs:274-285`, `430-457` |
| Point plate: `nav_prestige` glyph + number only, greys when nothing to spend | `PrestigeScreen.cs:578-599` |
| Refusals printed on the page that produced them, above the button | `PrestigeScreen.cs:1359-1366` (fix truncation, see P1-6) |
| Deterministic, non-blocking unlock flourish; camera kick owned by the screen and never applied to the mouse | `PrestigeScreen.cs:64-94`, `494-509`, `617-709` |
| World-unit type constants are named and marked `ui-size-ok` so `check_ui_type.py` can tell them apart | `PrestigeScreen.cs:910-933` |
| Fixture hooks `DevSelect / DevCamera / DevPoseLit`, capture modes `dust` (+zoom), `traitlit`, `traitterm`, `tour Traits N` | `PrestigeScreen.cs:131-160`; `Game1.cs:2160-2212`; `capture.sh:11-13, 59-60` |
| The tour cards for TRAITS are correct about the runtime (points come from conquests / mastery levels / corruption tiers) | `Onboarding.cs:350-363` vs `Career.cs:56-62` |

---

## 2. P0 — readability / hierarchy

### P0-1  Two compounding scale factors put every rung below the 720p floor
The screen is drawn through the overlay inset (`Game1.OverlayScale = 1720/1920 = 0.896`, `Game1.cs:5323`) and the
player's 1280x720 window downscales by 0.667 — a **0.597 combined factor** on every UiTypography rung this screen uses.

| Text (nominal rung) | on 1080 canvas | **at 720p** | Verdict |
|---|---|---|---|
| Screen title `TRAITS` 36 | 32 px | 21.5 px | fine |
| Point plate number PrimaryValue 30 | 27 px | 18 px | fine |
| Inspector title `TRAIT DETAIL` PanelTitle 26 | 23 px | 15.5 px | ok |
| Node name in inspector Headline 24 | 21.5 px | 14.3 px | ok |
| `6 TRAIT POINTS` Headline 24 | 21.5 px | 14.3 px | ok |
| Button `LEARN THIS TRAIT` NavigationLabel 21 | 18.8 px | 12.5 px | borderline |
| WHAT IT DOES body Body 19, lineH 23 | 17 px | **11.3 px** | too small for prose (brief target about 24-26 effective) |
| Road sentence, `You have 3 trait points.`, state word, prerequisite `learned / not yet`, reason line, subtitle mantra, tally line, camera hints — all Secondary 16 | 14.3 px | **9.6 px** | illegible — and several of these are SENTENCES at a caption-class size (§6) |

**720/dust.png**: the subtitle `ONE SPINE · FOUR ROADS · NO TAKING BACK` and the tally `7 OF 51 TRAITS LEARNED · 13 POINTS SPENT · 226 FOR EVERYTHING` are a grey smear under the title; `Hit harder. Live closer to death.` under RUIN in the inspector is unreadable; `You have 3 trait points.` (red, Secondary) is unreadable — and it is the one line that says why the button is grey.

### P0-2  The tree layer is unreadable at the default (home) zoom, even at 1080
World-unit type is multiplied by the camera zoom. Home zoom = `FitZoom()` about **0.556** (`PrestigeScreen.cs:259-264`; world extent 2298x1656 into View 1300x934; `capture.sh:59` says "about 0.55").

| World text | world px | x0.556 x0.896 = 1080 canvas | **720p** |
|---|---|---|---|
| Road name `RoadNameWorldPx` 32 | 17.8 | 15.9 px | 10.6 px |
| Node name `NamePx` 28 | 15.6 | 13.9 px | **9.3 px** |
| Spine caption 26 | 14.4 | 12.9 px | 8.6 px |
| Road identity sentence / tally `RoadLineWorldPx` 24 | 13.3 | 11.9 px | **8 px** |
| Cost tag `NodeCostWorldPx` 19 | 10.6 | 9.5 px | **6.3 px** |
| Minor node diameter 52 world | 28.9 | 25.9 px | 17 px (its road glyph about 9.6 px) |

**dust.png (1080)**: cost tags are 7-px digits in 12-px boxes; the road identity lines (`Skills act differently. Vows pay more.`) are readable only because they were memorised. **720/dust.png**: cost tags are dots; identity lines are noise; node names are guessable, not readable. The brief (§43) says explicitly *not* to require reading the whole tree at minimum zoom — but the constructor sets `_zoom = _homeZoom` (`PrestigeScreen.cs:127`), so the whole tree at minimum-legible zoom **is** the first thing every player sees, and the fixture photographs exactly that.

### P0-3  Secondary and disabled share one colour (§7)
`Slate 0x8A96A8` is the ink for supporting text (road sentence 1268-1269, `You have N points` 1322, camera hints 782-783, tally 552-553) **and** for the LOCKED state: a locked node's name in the inspector (1279), the word `LOCKED` (1284), unowned prerequisites (1349), cost tag digits on unaffordable nodes (1093), locked node names on the tree (947). `LockedInk 0x847E96` (glyph of a locked node) and the disabled button label `0x7C7688` (`UiKit.cs:892`) are within a few units of Slate. On the tree, 40 of 51 nodes are locked in a normal career, so most of the diagram is painted in the "less important" colour, and the inspector cannot show a locked node without making it look unimportant.

### P0-4  State and road are colour-only on the tree (§8)
A node's state (learned / affordable / locked) is carried by tint + brightness alone (`DrawFace` 1109-1112, name ink 947); its road by a glyph that is about 10 px on a minor node at 720p (P0-2) — so in practice by colour alone. No lock, tick, or shape channel exists on the diagram. The inspector does say the state in a word — good — but only for one node at a time.

### P0-5  The persistent guide strip sits on this screen (§11 global)
**dust.png**: `YOUR CHAMPION FIGHTS ON ITS OWN` (TutorialStep.Watch, `Tutorial.cs:237,250`) occupies canvas x 470-1450, y 978-1050, drawn as chrome on every screen (`Game1.cs:3109-3147`, 980 px wide, bottom-anchored). It has nothing to do with traits and lands directly under the spine caption and the camera hints. (Global finding; noted here because it is in the baseline of this screen.)

---

## 3. P1 — layout / UX

### P1-1  The header band shows totals the player can never reach (§92, §99-14)
`{learned} OF 51 TRAITS LEARNED · {spent} POINTS SPENT · {TotalTreeCost} FOR EVERYTHING` (`PrestigeScreen.cs:552-553`) prints **226** — the sum of all costs. The maximum a career can ever earn is **34** (6 conquests + 2x5 corruption tiers + 6 regions x 3 mastery levels; `Career.cs:59-60`, `CorruptionScaling.MaxTier = 5`, `MemoryDustTests.cs:247-249`). Every road header likewise prints `{lit} OF 8 · 45 POINTS` (`889-891`) — 45 > 34, so no player can ever finish even one road including its side strand. These numbers read as goals and are false goals; the tree's real design is "one terminal, most of a second road" (`MemoryDust.cs:371-377`). **Remove** `226 FOR EVERYTHING` and the per-road `45 POINTS`; **replace** with the reachable facts (P1-2).

### P1-2  The screen never says how to get the next point
The point plate says `3`; nothing on the screen says how the number grows. The only explanation is buried in `Buy()`'s refusal string (`466-469`, shown only after a failed click and then truncated — P1-6) and in tour card 2 (`Onboarding.cs:356-358`, shown once). The data is live in Core: `World.ConqueredIds`, `World.PeakCorruptionTier` (`Regions.cs:145,162`), `RegionAutomation.RegionMasteryPoints` vs `RmpThreshold1/2/3 = 500/2000/5000` (`RegionAutomation.cs:17-26,64,107-113`). A standing line — `NEXT TRAIT POINT: mastery in VERDANT HOLLOW 1,240 / 2,000` or `CONQUER ASHEN REACH` — is a Core derivation (no telemetry) and is the honest replacement for `226 FOR EVERYTHING`.

### P1-3  Default framing is the macro view; no road focus (§43, §44)
`_zoom = _homeZoom` (whole tree). There is no click / double-click / key that frames a road; road headers are not interactive (`DrawRoadHeaders` 846-893 draws only). Wheel zoom step is 1.16 (`431`) so reaching a legible 1.2 from 0.556 takes five notches with the pointer parked on the right node. Recommend: first open frames the spine head + road starts (or the road containing the cheapest affordable node), and clicking a road header / pressing 1-4 frames that road (5 rows x 190 + 150 header = about 1100 world tall, so zoom about 0.85 in a 934-px view; names then 28 x 0.85 x 0.896 = about 21 px canvas = about 14 px at 720p — raise `NamePx` to 32 for about 16 px, which still fits `NodeWidth 176` as the assert at `120-122` guards).

### P1-4  Hover swaps the whole inspector (§15 grammar)
`DrawDetail` reads `_hoverId ?? Selected` (`1245-1246`), so crossing nodes on the way to the button replaces the pinned trait's sheet mid-read, and the button's `enabled` flips with it. Brief grammar: hover = highlight / short tooltip, click = select, inspector = full explanation. Keep hover brightening on the tree (947, 953-954) and add a one-line tooltip (name · cost · state); the inspector follows the click only.

### P1-5  Terminal purchases and Enter have no confirmation (§46, §85, §99-7/9)
`Button(...)` then `Buy()` (`1369-1370`) and `Enter` then `Buy()` (`459`) are identical for a 1-point `LEARN VOWS I` and a 12-point `KEYSTONE — REAPER` whose description says *"After this you cannot afford to finish another branch"* (`MemoryDust.cs:401`). The screen already knows which nodes are terminals (`TerminalArt`, `328-335`). Minor nodes: keep one click. Terminals: two-step on the same button — first click arms it (`THIS IS PERMANENT — CLICK AGAIN TO COMMIT TO RUIN`, gold, 5 s) or a small modal `[COMMIT TO RUIN] [CANCEL]`; Enter must honour the same arming.

### P1-6  The one actionable refusal is truncated
Reason line width = panel 500 - 2 x 28 pad = **444 px** at Secondary 16 (`1366`, `ShortenBig`). `NOT ENOUGH TRAIT POINTS. EARN MORE: CONQUER A REGION, GO DEEPER INTO THE CORRUPTION, OR RAISE A REGION'S MASTERY.` (`468`) is about 110 characters, roughly 800 px, so the *how to earn more* half is cut to an ellipsis. It is also an ALL-CAPS sentence at a caption-class rung in Ember (§6). Solution is P1-2's standing line at Body.

### P1-7  Wasted vertical space under the inspector; a void inside it
`DetailPanel = (1368,144,500,790)` ends at overlay y 934 while the tree canvas runs to 1070 — **136 px** of empty column on the right (**dust.png**: nothing below y about 830). Inside, `YOU NEED FIRST` with one prerequisite ends at about y 560 canvas and the floor-anchored `Permanent.` rule starts at about 708 — a **roughly 150-px hole** on every short node, because cost/prereqs flow from the top and permanence/button hang from `Bottom-176` (`1292, 1356-1369`). Extend the panel to `View.Bottom` and fill the flow honestly (P1-2 next-point line, before/after for attribute nodes, road progress for gates).

### P1-8  The top-left is the densest 300 px of the screen while the bottom-left is empty
**dust.png**: point plate (canvas 216-356 x 68-143), then the RUIN header glyph starts 8 px to its right at y about 134; the tally line ends at y about 110 directly above the AEGIS/ARTIFICE headers; the RUIN crown `KEYSTONE — REAPER` sits at y about 200 under all of it. Meanwhile canvas x 220-470 x y 560-920 is blank starfield (the tree has no roots under RUIN) and the bottom 112 canvas px (overlay y > 1080 x 0.896) belong to chrome. The header should be one band (title, plate, next-point line) and the canopy should start below it with air.

### P1-9  No keyboard / gamepad node selection
Arrows pan (`451-454`), Enter buys the *mouse-pinned* node. There is no Tab / D-pad cycle through affordable nodes, so the technical-preferences cycle-and-confirm requirement is unmet on this screen, and Enter with no visible focus is a footgun combined with P1-5.

### P1-10  The nav rail never says "you have a point to spend" (§12, §83)
`Onboarding.IsNew` (`Onboarding.cs:515-517`) lights the TRAITS tile only for the first-visit tour; `BannerFor` has no TRAITS case; `tree.Available > 0` is never surfaced outside this screen. The brief's contextual line `YOU HAVE 1 TRAIT POINT` has its datum (`MemoryDustTree.Available`) but no wiring.

---

## 4. P2 — polish

- **P2-1** `TRAIT DETAIL` is a generic panel title (§6); the road line already identifies the panel. Drop the title, promote the node name to the panel's headline and add the kind as the CATEGORY line: `RUIN · KEYSTONE GATE` / `THE SPINE · CAPACITY` / `AEGIS · SMALL BONUS` (from `KindOf`, `368-377` — derivable, no new data).
- **P2-2** §14 order puts REQUIREMENTS before COST / STATE; the sheet prints COST before YOU NEED FIRST (`1309-1352`). Reorder once, then reuse the exact block on MASTERY (§45 asks for this).
- **P2-3** Success message `LEARNED: {name}.` is drawn in Ember red like every refusal (`1362-1366`) and persists until the next node click (`1062`). Use Met/Gold for success and clear on a timer (§86).
- **P2-4** The flourish's flash + 22-px camera kick have no Reduced Motion gate (`494-509`, `636-645`; §87).
- **P2-5** Camera hints (`782-783`) are two permanent Secondary lines in the canvas corner (§99-4). Replace with a compact zoom control at the canvas's bottom-right — three 44-px buttons `-  home  +` (also the discoverable home for road focus) — and show the drag hint once.
- **P2-6** Subtitle mantra `ONE SPINE · FOUR ROADS · NO TAKING BACK` (`538-541`) duplicates tour card 1 and the inspector's permanence line; at 9.6 px (720p) it is decoration. Either promote to Body under the title or remove; keep the `ATTUNED` variant as a gold pill on the plate instead.
- **P2-7** `You have 3 trait points.` is a sentence at Secondary (§6 caption rule); it belongs at Body beside the cost.
- **P2-8** Learned prerequisites print `learned` in Met green, unlearned `not yet` in Slate — Met is used for both AVAILABLE NOW (1284) and "prerequisite satisfied" (1350); two meanings, one colour.
- **P2-9** Attribute nodes could show before/after honestly: `Damage from traits x1.05 -> x1.10` via `DustEffects.TreeMods(tree)` and `u.Mods` (`DustEffects.cs:102-106`, `MemoryDust.cs:74`) — data exists.
- **P2-10** Keystone gates could say what they open against what you can wear: `You have 1 keystone socket, 2 keystones learned` via `DustEffects.KeystoneSockets / LearnedKeystones` (`DustEffects.cs:115-123, 265-269`) — data exists.

---

## 5. Legacy vocabulary / stale names

| # | Where | What reaches whom | Action |
|---|---|---|---|
| L1 | `src/IdleXIdle.Core/Builds/Keystones.cs:184` — WEAVER `Blurb = "EVERY SKILL ALSO FIRES AS THE NEXT FORM YOU CARRY, AT 45%. ..."` | **Player-facing on TRAITS**: `MemoryDustText.KeystoneSentence` (`MemoryDustText.cs:679-687`) appends it to `KEYSTONE — WEAVER`'s WHAT IT DOES (`PrestigeScreen.cs:1299`); also wherever BUILD prints keystone blurbs. Runtime now echoes the **next skill in the loadout** at 45 % (`SoloBattle.cs:1636-1650`) | Rename to `EVERY SKILL ALSO FIRES THE NEXT SKILL IN YOUR BUILD, AT 45%.` No test pins the old string (grep: only the source line) |
| L2 | `src/IdleXIdle.Core/Builds/SoloBattle.cs:1628-1629` comment "lands as the next Form in the loadout ... Source matchup" | code comment only | fix with L1 |
| L3 | `src/IdleXIdle.Core/Prestige/TraitRoads.cs:28` comment "fire as another Form" | code comment only | fix with L1 |
| L4 | `src/IdleXIdle.Game/Game1.cs:5040-5046` HELP panel: `WOVEN SKILLS`, `SOURCE x FORM x VOW`, `TRAITS (P) SELL MORE OF BOTH`, `FORM IS HOW YOU FIGHT`, `SOURCE VS REGION` | **Player-facing** (shared chrome, outside this screen but names TRAITS) | rewrite under §97-5 |
| L5 | Class/type names: `PrestigeScreen` (screen is TRAITS), `MemoryDustTree / MemoryDustUnlock / MemoryDustText / DustEffects` (tree spends TRAIT POINTS, not Dust — `MemoryDust.cs:122-140`), `Game1._dust`, `_showPrestige`, `DevDustDebug`, RH_SHOT_MODE `"dust"` (`Game1.cs:483, 2160`; `capture.sh:11, 59`) | developers / fixtures; the doc remark at `PrestigeScreen.cs:21-22` says the class keeps its name for host wiring | rename when the screen is touched; at minimum add `traits` as an alias mode |
| L6 | `assets/art/idlexidle_ux_screen_guide_standard.md:15, 489, 605` — screen listed as "Dust" | UX standard (§88) | rename to TRAITS in V2 |
| L7 | `Game1.cs:298` "Memory Dust prestige ... Dust accrues from mastery", `Game1.cs:1170` "the trait screen holds the Dust tree", `DustEffects.cs:92` "Resonance Weaving components" | comments | tidy |
| — | Node id `weave_5` (`MemoryDust.cs:314`) | save key, pinned by `test_ids_never_change_because_saves_store_them` | **not a finding** — ids must not change; the player sees `FIFTH SKILL SLOT` |

No Form / WovenAbility / Aptitude / Source x Form text is drawn by `PrestigeScreen.cs` itself; the `TRAITS` title, `TRAIT POINTS` wording and the Dust pill tooltip (`Game1.cs:4881`, Dust = checkpoints + Warren) match the runtime.

---

## 6. Data honesty (§92)

| Brief wants | Status | Source |
|---|---|---|
| Points to spend | EXISTS | `MemoryDustTree.Available` (`MemoryDust.cs:144`) |
| How the next point is earned / progress to it | EXISTS (Core derivation, no telemetry) | `Career.TraitPointsEarned` (`Career.cs:56-62`); `World.ConqueredIds`, `PeakCorruptionTier` (`Regions.cs:145,162`); `RegionAutomation.RegionMasteryPoints` + `AutomationTuning.RmpThreshold1/2/3` (`RegionAutomation.cs:17-26,64,107`) — needs a small `Career.NextTraitPoint(World)` helper |
| Maximum earnable (34) to replace `226 FOR EVERYTHING` | DERIVABLE, needs a Core helper (evaluate `TraitPointsEarned` on a full-content World exactly as `MemoryDustTests.cs:240-249` does) | no telemetry |
| Road progress `1 OF 8` | EXISTS | `tree.Owns` per road (`PrestigeScreen.cs:871-873`) |
| Locked reason (§83) | EXISTS | `CanUnlock` parts (`MemoryDust.cs:171-177`) + `Requires` names — already drawn |
| Before/after for attribute nodes | EXISTS | `u.Mods`, `DustEffects.TreeMods(tree)` (`DustEffects.cs:102-106`) |
| What a keystone gate opens vs. sockets owned | EXISTS | `DustEffects.LearnedKeystones / KeystoneSockets` (`DustEffects.cs:115-123, 265-269`) |
| Node kind for the CATEGORY line | EXISTS (derivable) | `KindOf` (`PrestigeScreen.cs:368-377`) |
| "You cannot afford a second terminal" warning | DERIVABLE | path cost walk as in `MemoryDustTests.cs:255-266`, moved to a Core helper |
| Diagnosis / offline summary | n/a for this screen | — |
| Contextual `YOU HAVE N TRAIT POINTS` on the rail (§12) | datum EXISTS, wiring MISSING | `Onboarding.IsNew` ignores it (`Onboarding.cs:515-517`) |

---

## 7. Proposed layout

Coordinates are in **the screen's own 1920x1080 logical space** (the space `View`, `DetailPanel`, `PointPlate` are written in). Game1 maps it to canvas by x0.896 + 180 px (`Game1.cs:4028-4036`); the canvas band y > 967 is chrome.

```
y   0- 70   TITLE BAND        TRAITS (ScreenTitle, Display, x=960) - rule 720-1200 @ y 74 (keep)
y  76-136   STATUS BAND       [PointPlate 40,76,156,84: glyph + N]  then  x 212-1000 "NEXT TRAIT POINT: ..." Body, readable Slate
                              right-aligned to 1340: "7 OF 51 LEARNED" Secondary.
                              REMOVED: "13 POINTS SPENT", "226 FOR EVERYTHING", subtitle mantra (ATTUNED becomes a gold pill at 212,96).
y 136-1070  TREE CANVAS       View 40,136,1300,934 (keep). Default camera: zoom about 0.85 framed on the road that holds the cheapest
                              affordable node (else the spine head). Road headers CLICKABLE = frame that road; keys 1-4 same; Home = whole tree.
                              NamePx 28 -> 32, NodeCostWorldPx 19 -> 24, RoadLineWorldPx 24 -> 28 (all still inside NodeWidth 176 /
                              NodeHeight 184 - re-check the ctor assert at 120-122). Locked nodes get a 12-world-px lock chip on the frame;
                              affordable keep the halo; learned get a tick chip - state stops being colour-only.
                              Zoom control: three 44x44 buttons  -  home  +  at 1252..1340 x 1014..1058; hints shown once.
y 136-1070  INSPECTOR         DetailPanel 1368,136,500,934 (extend to canvas bottom; PadX 28).
                              CATEGORY   y+56   RUIN glyph 26 + "RUIN - KEYSTONE GATE" Body road-colour; sentence Body Slate (raise from Secondary)
                              FACE       y+120  80/100 px face (keep)
                              NAME       y+210  Headline 24 -> PanelTitle 26 (the panel's headline; "TRAIT DETAIL" REMOVED)
                              STATE      y+244  Pill with glyph: LEARNED (tick) gold - AVAILABLE NOW (diamond) Met - LOCKED (lock) Bone-on-dark, never Slate
                              WHAT IT DOES  Body 19 (wrap to 444) + optional before/after line
                              YOU NEED FIRST  rows Body; tick/dash + learned/not yet at Body
                              WHAT IT COSTS   "6 TRAIT POINTS" Headline, right; "You have 3." Body beside it; if short: "NEXT POINT: ..." (same helper)
                              PERMANENT  panel.Bottom-166 rule; "Permanent. Never resets." Body gold (keep)
                              REASON     Bottom-120 Body (not Secondary), success in Met, refusal in Ember, cleared on timer
                              [LEARN THIS TRAIT]  1396..1840 x Bottom-92, h 68 (keep). Terminal: label "COMMIT TO RUIN", two-step arm.
```

**REMOVED**: `226 FOR EVERYTHING`, `13 POINTS SPENT`, per-road `· 45 POINTS`, `TRAIT DETAIL` title, subtitle mantra, permanent camera hints, hover-driven inspector swap.
**MOVED**: the "how to earn" sentence from `Buy()`'s refusal to a standing status-band line; road sentence from Secondary to Body; permanence stays where it is.
**RENAMED**: WEAVER blurb (L1); `TRAIT DETAIL` becomes the node's own name as headline; state word becomes a state pill with glyph; button label on terminals becomes `COMMIT TO <ROAD>`.
**NEW (data already in Core)**: next-point line, before/after for attribute nodes, sockets-vs-learned for gates, lock/tick chips, road focus.

---

## 8. Fixtures

**Existing (deterministic):**
- `RH_SHOT_MODE=dust` (`Game1.cs:2160-2197`): conquers all regions, `RestoreCorruption(16)`, buys `socket_2, ledger, vow_study_1, forge_insight, filter_common, recall_1, ks_glass_cannon`, `DevSelect("ks_bloodlust")`, optional `RH_SHOT_ZOOM` via `capture.sh dust out.png 1.2` (`capture.sh:59-60, 81-83`).
- `traitlit` (`RH_SHOT_T`, default 0.30 s) and `traitterm`/`traitterminal` pose the flourish frozen (`Game1.cs:2198-2212`; `PrestigeScreen.DevPoseLit` 149-160).
- `tour Traits N` poses the three tour cards over the same fixture (`capture.sh:32-40`; `Onboarding.cs:350-363`; `PrestigeScreen.Spotlights` 211-217).
- Baseline in this audit = plain `dust` at home zoom.

**Gaps against §95 "TRAITS (point + blocked node + permanent path)":**
1. **The fixture does not pose what its comment claims.** `RestoreCorruption(16)` clamps to `MaxTier = 5` (`Regions.cs:200-204`, `CorruptionScaling.cs:17`), so `Career.TraitPointsEarned` = 6 + 10 + 0 = **16**, not the "reachable 22" in the comment (`Game1.cs:2177`); 13 spent, so **3 available**, and `ks_bloodlust` (cost 6) is **LOCKED / NOT ENOUGH TRAIT POINTS**, not "AVAILABLE" (`Game1.cs:2186`). The baseline therefore shows a grey button and never an affordable node in the inspector. Fix: `RestoreMasteryPoints(RmpThreshold3)` on two regions (+6) or select an affordable node.
2. No capture of a **prerequisite-blocked** node (`LEARN WHAT IT NEEDS FIRST`) — e.g. `DevSelect("ks_blood_magic")`.
3. No **resting** capture of a walked road (gold header, gold wires to a terminal, `ATTUNED` subtitle); `traitterm` shows only the flourish over a darkened screen.
4. No **road-focus / zoomed** capture in the baseline set (the `1.2` third argument exists but was not used for `baseline/dust.png`); §94 wants the zoomed state at 720p too.
5. No capture of the terminal **confirmation** state (does not exist yet — P1-5).
6. For P1-2 the fixture must carry partial mastery points (`RegionAutomation.RestoreMasteryPoints`) so the next-point line has real data.
7. `capture.sh:11-13` mode list omits `traitterm`; add `traits` as an alias for `dust` (L5).

---

## 9. Summary of priorities for the TRAITS pass (brief §97 item 12)

1. Global P0s first (UI scale / rung sizes, Slate-vs-locked split, guide strip) — this screen is the worst case because of the 0.896 overlay factor on top of 720p.
2. Default camera to a road framing + clickable road headers; raise world type sizes.
3. Replace unreachable totals with the next-point line; extend the inspector to the canvas bottom and fill it with the data Core already has.
4. Two-step commit on terminals (and Enter); inspector follows click, hover tooltips.
5. Fix the WEAVER blurb; fix the fixture so it photographs an AVAILABLE node; add blocked / walked-road / zoomed captures at 1080 and 720.
