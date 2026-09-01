# UX V2 audit — MASTERY screen

Audited 2026-09-01 against `production/audit/ux-v2/BRIEF.md` (sections 0–15, 72–76, 82–102) and the
runtime truth on `feat/hunter-cutout-rig` (post 2026-08-31 refactor: STYLE → SKILL → VARIATION →
REINFORCEMENTS, Form/WovenAbility/Aptitude/Source×Form deleted, discovery permanent).

Sources read in full: `src/IdleXIdle.Game/BuildScreen.cs` (1922 lines — the MASTERY tree screen; the
class name is stale), `src/IdleXIdle.Game/FormHexDiagram.cs`, `src/IdleXIdle.Core/Builds/MasteryCatalog.cs`,
`MasteryTree.cs`, `MasteryLayout.cs`, `MasteryPoints.cs`, `StyleAffinity.cs` (rule lines), the Game1 wiring
(rail tiles, fixture handlers, guide banner), `Onboarding.cs` (Mastery tour cards), `Unlocks.cs`,
`UiTypography.cs`, `tools/asset-pipeline/capture.sh`, and the four baselines
`baseline/buildtree.png`, `baseline/buildzoom.png`, `baseline/720/buildtree.png`, `baseline/720/buildzoom.png`.

Read-only: no `.cs` file was modified.

---

## 0. Arithmetic the findings rest on

Everything is in the 1920×1080 logical space; a 1280×720 player sees it at ×2/3.

| Rung | 1080p px | 720p px |
|---|---|---|
| ScreenTitle 36 | 36 | 24 |
| PrimaryValue 30 | 30 | 20 |
| PanelTitle 26 | 26 | 17.3 |
| Headline 24 | 24 | 16 |
| NavigationLabel 21 | 21 | 14 |
| Body / Label 19 | 19 | 12.7 |
| Secondary 16 | 16 | **10.7** |
| Caption 14 | 14 | **9.3** |

Camera (from `BuildScreen.cs:295-331`, `MasteryLayout.cs`): `TreeView = (180,112,1260,952)`;
`WholeTreeZoom = (952/2 − 24) / (2000 × 1.30) = 0.174` (the HOME / `buildtree` framing);
`FirstOpenZoom = (min(1260,952)/2 − 48) / (740 + 58.9) = 0.536` (the first-visit / `buildzoom` framing).
Node diameter on screen = `2 × NodeRadius × zoom × 1.9`:

| Kind (world radius ÷1.9) | whole-tree 1080p | whole-tree 720p | first-open 1080p | first-open 720p |
|---|---|---|---|---|
| Minor 31 | 20 px | 14 px | 63 px | 42 px |
| Notable 45 | 30 | 20 | 92 | 61 |
| Bridge 49 | 32 | 22 | 100 | 67 |
| Greater 54 | 36 | 24 | 110 | 73 |
| SkillRoad 60 | 40 | 26 | 122 | 81 |
| Specialisation 68 | 45 | 30 | 138 | 92 |
| Capstone 76 | 50 | 33 | 155 | 103 |

The branch glyph inside a node is `0.46 × box` (`BuildScreen.cs:1804`) and drops out under 20 px
(`:1799`): a Minor at whole-tree framing is a 20 px ring with a 9 px glyph (6 px at 720p).

---

## 1. Does the screen answer its question today?

Brief §100 — MASTERY: **"How am I specializing this build?"**

Partly. At the whole-tree framing the gold fan of walked Resonance nodes and the four coloured
capstone rings answer *which direction I have walked* at a glance (buildtree.png — the eye lands on
the gold fan first, then the four branch headers, then the two brown panels). Nothing on the page
answers *what a node does*, *what I can take next*, *which skills I have discovered*, or *how I get
more points* without hovering nodes one at a time. At the first-visit framing (buildzoom.png) the
player sees ~20 identical unlabelled glyph rings around "YOU" and a "0" in the corner.

---

## 2. What is good and must be kept

- **World + camera tree with derived framings.** `WholeTreeZoom`, `FirstOpenZoom`, `LabelZoom` are
  formulas over the layout (`BuildScreen.cs:295-324`), zoom is about the pointer (`:387-395`), drag
  pans (`:512-528`), arrows / + / − / Home work (`:531-544`), and the camera is saved
  (`SaveGame.MasteryZoom/PanX/PanY`, `BuildScreen.RestoreCamera :164`). Keep all of it.
- **Layout owned by Core and tested.** `MasteryLayout` (constant sibling arc, ring curve, spur/bridge/
  road placement) with `MasteryLayoutTests` asserting no two nodes overlap and that the tree still
  separates at twice the depth. Keep; the V2 pass should not move a node.
- **The cross geometry states the design.** Opposed branches sit opposite (`MasteryLayout.AngleOf :1021`);
  branch headers with a one-line promise are planted outside the rim (`DrawBranchHeader :1245-1264`).
  Keep the headers and the promise lines — but see P0-1 for their size.
- **Colour = branch, brightness = state, gold = taken.** `DrawNode :1689-1690`; wires go gold only
  when both ends are taken and prefer the taken prerequisite (`:1069-1090`). The walked path reads
  instantly in both baselines. Keep.
- **Size = price, plus the capstone halo and the breathing halo on an unchosen Specialisation**
  (`:1704-1730`). Keep the intent; see P1-2 for the kinds that still share a frame.
- **Hover shows, click pins — including a node you cannot afford** (`:637-644`, `:1426`). Right-click
  refunds one node (`:552-569`), refusal to strand a dependent node (`MasteryTree.Refund :811`). Keep.
- **Refusals named exactly**: "NEEDS 3 POINTS — YOU HAVE 0. GO DEEPER.", "ONE CAPSTONE PER HUNTER",
  "ONE DISCIPLINE PER HUNTER" (`:660-672`, `:1544-1550`). Keep the copy; move where it appears (P1-5).
- **Two-click free respec** with the button relabelling itself "PRESS AGAIN TO CONFIRM" (`:614-626`,
  `:1156-1157`), arming cleared on navigation (`ShowTree :119`). Right severity for a free, reversible
  action (brief §85). Keep.
- **The screen-title pattern** is the house one (title at 960/24, rule at 74, caption at 80 —
  `DrawTreeChrome :1130-1138`). Keep.
- **The attunement ceremony** (`DrawAttunement :1385-1410`) makes the one identity choice feel like a
  moment, and its UNDO is a real refund. Keep the ceremony; fix its copy (Legacy L4).
- **Canvas scissor-clipped to `TreeView`** (`DrawTreeWorld :1035-1052`) and the dock gates input
  (`OverDock :102`). Keep.
- **Mastery tour cards** (`Onboarding.cs:269-281`) already speak the current model (STYLE, discipline,
  learned for good). Keep.
- **Points plate reduced to glyph + number** per the 2026-08-30 playtest (`:228-237`). Keep the idea;
  fix the number's colour (P0-4) and the plate/button widths (P1-3).

---

## 3. P0 — readability and hierarchy

**P0-1. Every caption on the canvas and in the right column is Secondary 16 → 10.7 px at 720p.**
Evidence: `DrawBranchHeader :1263` (promise lines), `DrawNode :1789-1794` ("HAMMER" / "SPECIALISATION"),
`:1834/:1839` ("CAPSTONE"), `DrawTreeChrome :1138` (the top caption and every refusal message),
`DrawNodeDetail :1435-1470` ("DRAG · WHEEL · HOME", "WHAT EACH KIND COSTS IN POINTS", the one-branch
rule), `DrawHexPanel :1316` and `FormHexDiagram.Draw` labelPx=Secondary (the six style names),
factorPx=Caption 14 → 9.3 px. In `720/buildtree.png` the promise lines under RESONANCE/LOOT/TEMPO/
ENDURE, the six hexagon names, "SPECIALISATION", "CAPSTONE", the top caption and the whole lower half
of the node card are grey smears; only the branch names (PanelTitle), "MASTERY TREE", the currency
pills and "TAKE EVERY POINT BACK" are comfortably legible. Brief §3/§5: fix by promoting rungs and by
the global UI-scale work, not by adding text.

**P0-2. Nodes carry no name on the canvas at any zoom.** `DrawNode` prints text for exactly three
kinds: "YOU" (`:1779`), the Style word inside a Specialisation (`:1789`), and a capstone's name
(`:1838/:1843`). Minors, Notables, Greaters, Bridges and all twelve SkillRoad nodes are anonymous at
every zoom. `buildzoom.png` (the framing every first visit gets, `FrameFirstOpen :334`) is ~20 identical
shield / lightning / sack / tuning-fork rings at 63 px with zero words; `720/buildzoom.png` is the same
at 42 px. The brief's §74 "detail readable zoomed in" fails outright, and §15's grammar (hover = glance,
inspector = full) is inverted: hover is the *only* way to learn a node's name.

**P0-3. Two identical ornate frames of equal weight hold the right column, and the top one is an
empty state most of the game.** `HexPanel = (1408,112,496,460)` and `NodePanel = (1408,588,496,460)`
(`:463-471`), both `PanelQuiet`. In `buildtree.png` YOUR ATTUNEMENT is 25 % of the screen's width and
43 % of its height showing six dim seals and "UNATTUNED / TAKE A SPECIALISATION NODE" — a diagram of
nothing yet — while the inspector (the decision surface, brief §73) gets the lower half and has no
control in it: "AVAILABLE — CLICK THE NODE" (`:1544`) is a Label-size sentence, not a [TAKE] button.
Brief §9 (frame reduction), §84 (one primary action), §99.1 (the decision must be visually obvious).

**P0-4. Secondary reads as disabled (brief §7).** "DRAG · WHEEL · HOME" is drawn in `Dim`
(0x2C2C36) on a 0x14111A panel (`:1435-1436`) — invisible in both baselines. The points number
turns `Slate` at zero (`:1154`), so the most important figure on the page reads as switched off in
the exact state where the player most needs to read it. Untaken-unaffordable and untaken-unreachable
nodes are drawn identically (`fill`/`edge` at `:1689-1690` only distinguish `canTake`, which folds
"cannot afford" into "locked"); in `buildzoom.png` every non-Resonance node is the same near-black ring.

**P0-5. The HUNT guide banner sits on the MASTERY canvas.** `Game1.DrawGuideBanner :3109` draws
`TutorialStep.Watch` ("YOUR CHAMPION FIGHTS ON ITS OWN") on every screen; `GuideBannerRect :3142`
puts a 980×~100 strip at y≈975–1050, over the bottom of `TreeView` (which runs to 1064) and 30 px
under the LOOT header in `buildtree.png`. It is HUNT teaching (brief §11/§12) and it costs the
canvas ~10 % of its height on the screen whose whole content is the canvas. Shared chrome — flagged
here because the pixels are this screen's.

---

## 4. P1 — layout and UX

**P1-1. "SPECIALISATION" captions are overdrawn by their own road nodes on the south and west arms.**
The caption is always drawn at `box.Bottom + 5` (`:1792`), i.e. toward the rim, and the SkillRoad
node sits on the same spoke 260 world units further out (`MasteryLayout.PositionOf :1214-1229`).
`buildtree.png`: DRAIN reads "S ECIALISATION" (~673,690), FIELD "SP CIALISATION" (~770,789), VOLLEY
"SPECIALIS TION" (~1038,789). The capstone already flips its captions by arm (`:1836-1845`); the
Specialisation caption must do the same or sit inside the diamond.

**P1-2. Node kinds still share frames, and skill discovery is invisible (brief §74/§75).**
`KindFrame :1625-1635`: SkillRoad wears `ui_node_greater`, Bridge wears `ui_node_minor`, Capstone
wears `ui_node_greater`. A 6-point bridge is framed like a 1-point minor; the twelve skill nodes — the
kind the code itself calls the tree's most consequential (`MasteryTree.cs:591-600`) — are Greaters
with a branch glyph. They never show the skill they teach (the twelve skill glyphs exist since commit
3aaed9f and `SkillDef.Name` is on `def` at `MasteryCatalog.Teaches :477-485`). DISCOVERED has no
on-canvas mark: after a respec a learned road node draws exactly like an untaken one; the word
"LEARNED — FOR GOOD" appears only in the inspector while that node is hovered (`:1520-1522`).

**P1-3. Corner plate and its button do not share a width.** `PointsPanel = (40,112,200,96)` and
`ResetBtn = (40, 220, 300, 52)` (`:237-247`): the button is 100 px wider than the plate above it
(visible top-left in both baselines). It is also the only button on the page, so a respec — the
screen's most sweeping action — carries the page's only primary-button weight while the actual
primary action (take a node) has none.

**P1-4. The inspector does not follow the §14 shape and repeats the Core's rule logic.** Order today:
kind + branch (Secondary) → the whole catalogue label as uppercase wrapped prose at Label 19
(`DrawWrapped :1561`, 30 px leading) → Specialisation / road sentence → COST / YOU HAVE → state line.
Missing against §14 and TRAITS' proven WHAT IT DOES / WHAT IT COSTS / YOU NEED FIRST
(`PrestigeScreen.cs:1295-1354`): NAME as a headline (`Head(label)` exists, `:67`); WHAT IT DOES as
lines (`MasteryNode.Stats` is a dictionary the UI never prints — a stat minor's "+6 RESONANCE" is only
inside its label string); YOU NEED FIRST with the prerequisite *names* (`MasteryNode.Prereqs` /
`SecondPrereqs` exist; the UI prints "LOCKED — WALK TO IT FIRST"); a [TAKE] button. The state line at
`:1544-1550` re-derives `MasteryTree.CanTake`'s five branches by hand.

**P1-5. The refusal message speaks 500 px from the click.** `_msg` replaces the caption at (960,80)
in Ember at Secondary 16 (`:1137-1138`) — 10.7 px at 720p, at the opposite end of the screen from the
node and from the inspector. Brief §86: feedback belongs where the action was (inspector state line
and/or a toast).

**P1-6. At the first-open framing the docked panels cover the east ring.** `TreeView` runs to
x=1440 while the dock starts at x=1408 (`:369`, `:463`); the ring-1 TEMPO minors at x≈1395–1440 are
cut by the frame edge in `buildzoom.png` (nodes at ≈(1400,225) and ≈(1400,820)). The canvas should
end where the inspector begins.

**P1-7. The whole-tree framing wastes ~220 px each side.** The tree spans ≈x 450–1270 of a 180–1440
canvas in `buildtree.png` because the zoom is bound by the header ring (`HeaderRing = 1.30`, `:428`)
and the view's height. Headers pinned to screen edges (or a smaller ring) buy ~15 % zoom for every
node and caption on the page.

**P1-8. Zero-points empty state says nothing (brief §82).** With `Available == 0` the page shows a
Slate "0" and a HOVER card. The first hint of how to earn a point is the refusal after a failed click
("GO DEEPER.", `:667`). `MasteryPoints.FromDepth` is a closed formula (`MasteryPoints.cs:41`), so "NEXT
POINT: REACH DEPTH n IN <REGION>" is computable — see Data honesty D7.

**P1-9. Two names for one node.** The canvas prints the Style (`HAMMER`, `:1789`), the hexagon and
the ceremony print `HAMMER`, but the inspector's label is "STRIKE SPECIALIST — EXECUTE WEAKENED FOES"
(`MasteryCatalog.cs:433`). Same for TRAP/SNARE, AURA/FIELD, MARK/SIGN, MORPH/DRAIN. See Legacy L5.

**P1-10. Undiscoverable input.** Right-click refund (`:552`) is not mentioned anywhere on screen; the
only hint is "DRAG · WHEEL · HOME" in Dim. Keyboard can pan and zoom but cannot select or take a
node — no cycle-and-confirm path (technical-preferences: every interaction must be completable without
a pointer; the tree is not combat, so P1 not P0).

---

## 5. P2 — polish and hygiene

**P2-1. ~300 lines of the class are the retired BUILD overview.** The rail's BUILD tile opens the weave
(`Game1.cs:5291-5292`), so `_editMode == false` is reachable only from the boot check
(`Game1.cs:1317`) and `RH_SHOT_MODE=build`. `DrawSummary/DrawCore/DrawAuraCards/DrawPassives`
(`:709-937`), `WeaveBtn`, `AuraCard`, `Big`, `Row` serve that dead view; `DrawPointsStrip :1179`,
`DrawBranchTable :1210`, `DrawCell :1884`, `Sidebar :476`, `SbX :459`, `MedallionTop..ResetRowTop
:1163-1167`, `TreeUnlocked :130`, `Power :187` are referenced by nothing. Delete with the rename.

**P2-2. Comment drift.** `:126-128` says the tree opens at wave 8 (Unlocks: wave 25,
`Unlocks.cs:136`); the class doc `:17-27` describes the overview; `Game1.cs:1565-1569` says points are
"BestDepth/5 ... 3 + 21 = 24" (the curve is √depth × 0.9 — see Fixtures F1).

**P2-3. Rail emblem swap (shared chrome).** The BUILD tile draws `state_mastery_128`
(`Game1.cs:5151`) — the same glyph this screen's points plate uses (`:1150`) — while MASTERY draws
`nav_build` (`:5156`). Flag to the Game1 auditor.

**P2-4. Data at Caption size.** The docked hexagon's factors ("x2.0 — YOURS", "x0.45") are
`factorPx: UiTypography.Caption` (`:1329`) — 9.3 px at 720p, and a value, not a tag (brief §6).

**P2-5. Uppercase prose.** Every node effect and inspector sentence is ALL CAPS at Label 19
(`DrawWrapped`), which reads slower than sentence case at any size. A house-style question; flag only.

**P2-6. Breathing halo ignores Reduced Motion** (`:1721-1730`, brief §87). Cheap to gate once the
setting exists.

**P2-7. The one-branch rule sentence** ("RESONANCE OR LOOT. TEMPO OR ENDURE. YOU HAVE POINTS FOR ONE
BRANCH, NOT TWO.", `:1465`) is only true as a budget statement (`MasteryTreeTests.test_two_complete_
branches_are_out_of_reach`); a player who has taken nodes in two branches will read it as a rule they
broke. Reword ("Points buy about one full branch.") or drop.

---

## 6. Legacy vocabulary reaching the player or the class names

| # | Where | Text | Reach |
|---|---|---|---|
| L1 | `src/IdleXIdle.Game/BuildScreen.cs:28` | class `BuildScreen` is the MASTERY screen | class name (brief §67 → `MasteryScreen`) |
| L2 | `src/IdleXIdle.Game/FormHexDiagram.cs:41` | class `FormHexDiagram`; doc "The Form's own art" `:70`, "Form's SEAL" `:108`, "far Form" `:190` | class name; it iterates `Enum.GetValues<Style>()` `:126`, so it IS a Style diagram → rename `StyleHexDiagram`, keep |
| L3 | `BuildScreen.cs:690`, `:763`, `:768`, `:784`, `:827` | "EVERY SKILL IS A SOURCE AND A FORM — YOU CHOOSE BOTH", row "FORMS", "Every skill is a SOURCE and a FORM." | retired overview: unreachable from the rail, but shipped and posed by `RH_SHOT_MODE=build` |
| L4 | `BuildScreen.cs:1403-1406` | "THE FAR FORMS HIT SOFTER — A VOW ON A FAR-FORM SKILL PULLS IT ONE RING CLOSER." | **player-facing** — the attunement ceremony (mechanic is true: `StyleAffinity.Factor(…, vowSworn) :34-38`; the word is wrong) |
| L5 | `src/IdleXIdle.Core/Builds/MasteryCatalog.cs:433-446` | "STRIKE SPECIALIST", "TRAP SPECIALIST", "AURA SPECIALIST", "MARK SPECIALIST", "MORPH SPECIALIST" — old Form names for HAMMER/SNARE/FIELD/SIGN/DRAIN | **player-facing** via `n.Label` in the inspector (`BuildScreen.cs:1483`); only "VOLLEY SPECIALIST" matches its Style |
| L6 | `MasteryCatalog.cs:179` | "NARROW — YOUR DISCIPLINE'S FORM +20%, EVERY OTHER FORM -10%" | **player-facing** node label |
| L7 | `MasteryCatalog.cs:181` | "BROAD — HALF OF THE OPPOSITE FORM'S PENALTY IS GIVEN BACK" | **player-facing** node label |
| L8 | `MasteryCatalog.cs:189` | "PURE — WHILE EVERY WOVEN SKILL SHARES ONE SOURCE…" | **player-facing**; "WOVEN" is Weave vocabulary (brief §101) |
| L9 | `BuildScreen.cs:1524` | "WEAVE IT ON THE BUILD SCREEN (B)." | **player-facing** road-node sentence in the inspector |
| L10 | `BuildScreen.cs:1392` | "SIX STYLES ON THE LOOM — ONE IS YOURS." | player-facing; "loom" is the weave metaphor — acceptable art copy if Weave survives as flavour, else reword |
| L11 | `MasteryTree.cs:608` "A Form sub-branch"; `MasteryCatalog.cs:419` "FORM SPECIALISATIONS"; `BuildScreen.cs:18` "Source/Form/Vow composition"; `_attuneForm :89`; `DevAttune(Style form) :174`; `MasteryCatalog.cs:58-63,73` "six Form arms / Form specialisations" | code identifiers and comments | not player-facing; clean with the rename |
| L12 | `FormHexDiagram.cs:319-327` | asset keys `icon_form_*` | asset names, accepted by the file's own note; no player text |

Not legacy but stale: `BuildScreen.cs:126` "wave 8" (Unlocks says wave 25); `Game1.cs:1565` fixture
comment "BestDepth/5".

---

## 7. Data honesty — what the brief wants shown vs what Core computes

| # | Display the brief wants | Status | Evidence |
|---|---|---|---|
| D1 | "18 SPENT · 6 AVAILABLE" | **EXISTS** | `MasteryTree.Spent :732`, `Available :734`, `Earned :729` |
| D2 | WHAT IT DOES per node | **EXISTS, partially structured** | `MasteryNode.Label` (name + effect in one string, split on "—" by `Head :67`), `MasteryNode.Stats` (stat lines, unused by UI), `SkillShape` (fields, no text) — no separate description member; a "lines" view is catalogue authoring, not telemetry |
| D3 | COST / can I take it / why not | **EXISTS** | `MasteryNode.Cost`, `MasteryTree.CanTake :758`; the *reason* is re-derived in UI (`:1544-1550`) — a `MasteryTree.WhyNot(id)` would remove the duplicate; no telemetry |
| D4 | YOU NEED FIRST (prerequisite names, tick/cross) | **EXISTS** | `MasteryNode.Prereqs` (any-of), `SecondPrereqs` (bridge), `Unlocked :687`, `IsTaken`; UI never prints the names |
| D5 | DISCOVERED, visible after respec (§75) | **EXISTS** | `MasteryTree.LearnedSkills :876`, latch in `Take :778`, `Respec :827` leaves it, persisted `SaveGame.LearnedSkills :177`; UI shows it only in the hovered road node's text |
| D6 | Path preview "This path costs 4 points." (§76) | **NEEDS a new Core function (pure, not telemetry)** | pathing is deterministic (any-of prereqs, bridge two-group); cheapest path from `Taken` over `MasteryCatalog.Nodes` by `Cost` is a small Dijkstra; nothing exists (`grep PathCost|PathTo|CostToReach` → none). Needs a test alongside `MasteryTreeTests` |
| D7 | How to earn the next point (empty state) | **EXISTS as formula, needs a helper + host feed** | `MasteryPoints.FromDepth :41` is √depth×0.9 floor; per-region `RegionFarm.BestDepth` is host-side (`Game1.SkillPointsEarned :5175`); a `MasteryPoints.NextDepth(bestDepth)` and the host passing per-region depths would make "NEXT POINT AT DEPTH n" honest |
| D8 | Attunement factors (×2.0 / ×1.15 / ×0.75 / ×0.45) | **EXISTS** | `StyleAffinity.Factor :25`, `FactorAtDistance :44-49`; the diagram reads them live |
| D9 | Branch spend tally (per-direction) | **EXISTS** | derivable from `Taken` × `Cost` (`DrawBranchTable :1210` did it; dead) |
| D10 | Lock requirement "REACH WAVE 25" | **EXISTS** | `Unlocks.IsOpen/Requirement(Activity.Mastery)` (`Unlocks.cs:136,181`); the rail already shows it |
| D11 | Unlock progress toward wave 25 | **EXISTS** | `TutorialFacts.DeepestWave` (`Unlocks.cs:136`) vs the constant — a "wave 17 / 25" bar is honest |

Nothing on this screen requires new combat telemetry. D6 and D7 are pure functions over existing state.

---

## 8. Proposed layout (1920×1080, one sentence per zone)

- **Header strip (0,0)–(1920,104)**: keep title / rule / caption exactly (`:1130-1138`); the caption
  is static copy again (the message row moves — see below).
- **Status plate (40,112)–(340,208)**: one `PanelQuiet` 300×96: medallion + `AVAILABLE` figure at
  PrimaryValue in Gold (Bone, never Slate, at zero) + "18 SPENT" at Body; a one-line
  "DISCIPLINE · HAMMER ×2" chip (or "NO DISCIPLINE YET") at Body under it. This replaces the
  460 px attunement panel as the standing answer to "who am I".
- **Reset (40,220)–(340,272)**: `TAKE EVERY POINT BACK` as a *quiet* button of the plate's width
  (300), same two-click guard.
- **Canvas (180,112)–(1400,1064)**: `TreeView` ends at the inspector, no overlap (fixes P1-6);
  headers pinned to the canvas edges rather than at HeaderRing 1.30 so the whole-tree zoom rises
  (P1-7); on-canvas names at Body for every Notable / Greater / Bridge / Road / Specialisation /
  Capstone when `zoom > LabelZoom`; stat minors show glyph + "+6" at Secondary when
  `zoom ≥ FirstOpenZoom × 0.8`; road nodes draw the *skill's* glyph and name, with a small gold tick
  badge when `LearnedSkills()` contains the skill (DISCOVERED survives respec); kind tags flip by arm.
- **Inspector (1408,112)–(1904,1064)**, one `PanelQuiet` 496×952 (TRAITS uses 500 at x=1368, MAP 474
  at x=1412 — same column): `KIND · BRANCH` (Secondary, branch colour) / `NAME` (Headline 24) /
  identity line (Body) / `WHAT IT DOES` (Body rows: `Stats` as "+6 RESONANCE", effect from the label
  tail, Specialisation and road sentences here) / `YOU NEED FIRST` (prerequisite names with ✓/✕ from
  `Prereqs`+`IsTaken`) / `COST 3 · YOU HAVE 6` (Headline figures) / **[TAKE · 3 POINTS]** primary
  `UiKit.Button` 52 px at the foot, or **[GIVE BACK]** when taken, disabled-with-reason otherwise
  ("NEEDS 3 — YOU HAVE 0"). The refusal/feedback line lives directly above the button. Empty state:
  "PICK A NODE TO READ IT" at Body, the cost ladder at Body, "HOW POINTS ARE EARNED — first-time depth,
  per region" + (D7) "NEXT POINT: DEPTH n IN <REGION>", and the controls hint at Body in Slate (never
  Dim), including "RIGHT-CLICK — GIVE ONE NODE BACK".
- **Specialisation state in the inspector**: when a Specialisation is pinned, the hexagon (the
  `StyleHexDiagram`, radius ~78) draws *inside* the inspector's WHAT IT DOES block with the six names
  at Body and factors at Secondary — the chart appears where the decision is made, not as a standing
  panel.
- **Guide banner**: suppressed on this screen; replace with a contextual toast "YOU HAVE n MASTERY
  POINTS" when `Available > 0` on entry (brief §12).
- **720p check**: with canvas names at Body (12.7 px) and Secondary reserved for kind tags, the tree
  reads; the global UI-scale/rung work (brief §4/§5) is still required for Body itself.

### Removed / moved / renamed on this screen

- **REMOVED**: the YOUR ATTUNEMENT dock (496×460); "DRAG · WHEEL · HOME" in Dim; the one-branch rule
  sentence (or reworded, P2-7); the retired overview and its dead members (P2-1); the HUNT banner on
  this screen.
- **MOVED**: the hexagon chart → inspector (Specialisation state) and the ceremony; the discipline
  readout → status-plate chip; refusal messages → inspector feedback line (+ toast); the cost ladder
  and "how points are earned" → inspector empty state; "SPECIALISATION"/"CAPSTONE" tags → the
  inward side on south/west arms.
- **RENAMED**: `BuildScreen` → `MasteryScreen`; `FormHexDiagram` → `StyleHexDiagram`;
  "STRIKE/TRAP/AURA/MARK/MORPH SPECIALIST" → "HAMMER/SNARE/FIELD/SIGN/DRAIN SPECIALISATION" (label
  head equal to the canvas word); "FORM" in NARROW/BROAD → "STYLE"; "WOVEN SKILL" → "EQUIPPED SKILL";
  "FAR FORMS / FAR-FORM" → "FAR STYLES / FAR-STYLE"; "WEAVE IT ON THE BUILD SCREEN (B)" → "EQUIP IT
  ON THE BUILD SCREEN (B)"; "MASTERY TREE" caption stays.

---

## 9. Deterministic fixtures

**What exists** (`Game1.cs:1559-1637`, `tools/asset-pipeline/capture.sh:11-24`):

- `RH_SHOT_MODE=buildtree` — whole-tree framing (`DevOpenTree()`); three regions at best depth 35,
  every Resonance Minor and Notable taken; with `RH_SHOT_MODE=tour` it opens on the first-visit framing.
- `RH_SHOT_MODE=buildzoom` — first-visit framing, or `RH_SHOT_ZOOM=<z>` for any zoom (`:1629-1634`).
- `RH_SHOT_MODE=attune` — the ceremony posed on HAMMER (`DevAttune`), `SetEarned(80)`, Resonance
  minors+notables, `spec_strike`, `road_hammer` taken (one learned skill).
- `RH_SHOT_MODE=attuned` — the same tree after sealing (discipline live, hex gold).
- `RH_SHOT_MODE=build` — poses the **retired overview**, a page no player can reach (`Game1.cs:5291`).
- Tour: `RH_SHOT_MODE=tour` with `ShotTab=Mastery` and `RH_SHOT_STEP` (generic, `Game1.cs:1256`).
- `capture.sh` lists `build buildtree buildzoom` (`:11-13`) but not `attune`/`attuned`.

**Gaps against brief §95 — "MASTERY (points + discovery node + notable/capstone)":**

- **F1 — the points pose is wrong, and the baseline shows it.** The fixture calls `SetEarned(24)`
  but `Game1.cs:3723` re-derives earned every frame from `MasteryPoints.Total`; three regions at depth
  35 pay 3 × ⌊√35 × 0.9⌋ = 3 × 5 = **15**, while the taken set costs 6 × 1 + 5 × 3 = **21**. Available
  = 0 — the "0" in both baselines. Consequence: no node is ever in the AVAILABLE state in any capture,
  so the screen's takeable look (branch-tint fill, `:1689`) has never been photographed, and the pose
  is a state the game cannot produce (spent > earned). Fix: `RestoreBestDepth(100)` on three regions
  (3 × 9 = 27) or take fewer notables; update the `:1565-1569` comment.
- **F2 — no capstone/greater state.** No fixture takes a Greater or a ring-4 node, so the mastered
  halo (`:1708-1713`), the gold capstone name (`:1834`) and "CLOSED — YOU ALREADY TOOK A CAPSTONE"
  are unphotographed. Add a `masterycap` pose: `SetEarned` high, walk Endure to `endless`.
- **F3 — no discovered-after-respec state (§75's exact case).** `attuned` takes `road_hammer` and
  keeps it taken; nothing poses `Take(road) → Respec()` with `LearnedSkills()` still holding the skill.
  Add it, and assert it in the same pose (`mastery_learned_permanence_test` covers Core; the UI has
  no capture).
- **F4 — the inspector is never photographed with content.** Both baselines show the HOVER empty
  state. Needs a `RH_SHOT_NODE=<id>` dial that sets `_pinnedNodeId` (a `DevPin(string)` on the screen)
  so a Notable, a Specialisation, a road node and a locked node each get a capture.
- **F5 — no 720p capture.** `capture.sh` shoots the 1920×1080 canvas; the `720/` copies are downscales
  of the baseline. That matches the game's own `PresentFit` downscale, so it is an honest proxy, but
  the fixture should emit both automatically (brief §96).
- **F6 — `attune`/`attuned` undocumented in `capture.sh`; `build` documented but poses a dead page.**
- **F7 — fixture comment drift** (`Game1.cs:1565`, "BestDepth/5") — see F1.

---

## 10. Open questions for the owner

1. Is "weave/woven/loom" retired as *vocabulary* (brief §101 lists Weave) or kept as the game's
   flavour metaphor? L8/L9/L10 depend on the answer.
2. Should the attunement ceremony survive as a modal? It is the one place the game makes a
   reversible choice look permanent (brief §99.9–10); the undo button and "TAKE EVERY POINT BACK IF YOU
   WANT TO CHOOSE AGAIN" (`:1510`) keep it honest, but a lighter inspector-only confirmation would
   match §85.
3. Path preview (§76): worth a Core `MasteryTree.CheapestPathCost(id)` + test, or skip for V2?
