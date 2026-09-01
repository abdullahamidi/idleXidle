# UX V2 audit — BUILD (loadout) screen

Audited 2026-09-01 against `production/audit/ux-v2/BRIEF.md` §0–15, §66–71, §82–102, and the
current runtime (STYLE → SKILL → VARIATION → REINFORCEMENTS, SkillId identity; Form / WovenAbility /
Aptitude / Source×Form composition deleted). Source read in full: `src/IdleXIdle.Game/WeaveScreen.cs`
(1811 lines), `src/IdleXIdle.Game/FormHexDiagram.cs` (345), `src/IdleXIdle.Core/Builds/PlayerLoadout.cs`,
`SkillCatalogue.cs`, `SkillProgress.cs`, `Vows.cs`, `Build.cs`. Pixels: `baseline/weave.png` (1920×1080) and
`baseline/720/weave.png` (1280×720), plus 3× crops of the 720p file (slots, keystones/readout, middle
column, vow column, header).

Screen identity: the rail's **BUILD** tile opens `WeaveScreen` (`Game1.cs:477 Activity.Build => "weave"`,
`:1318`, `:4097`). The class called `BuildScreen` is the **MASTERY** tree (`Game1.cs:1604`, `BuildScreen.cs:18–20`).

---

## 0. Does the screen answer its §100 question today?

> BUILD — "What skills am I running and how are they configured?"

**Partially.** The four left-hand rows do name each skill, its kind (ACTIVE/PASSIVE), its Source word,
its variation and reinforcement count (`WeaveScreen.cs:1022–1061`). But (a) the Style is never named
anywhere on the screen, (b) the level number lives only on the hidden tree tab, (c) the configuration is
split across three columns plus a tab the player must switch, and (d) at 1280×720 the configuration lines
are ~11 px tall — the answer has to be *read* rather than *seen*. The Vow half of "how configured" is
present (MET / UNMET) but never says *why* a demand fails.

---

## 1. Effective type sizes — the numbers behind every readability finding

Menu screens are drawn through Game1's **overlay inset**: `OverlayScale = (1920 − 180 − 20) / 1920 =
0.8958` (`Game1.cs:5323`, `UiKit.cs:717–729`). Every rung on this screen is therefore already 10.4 %
smaller than the ladder says *before* the bilinear downscale to 720p.

| Rung (authored) | On this screen at 1080p | At 1280×720 | Used here for |
|---|---|---|---|
| ScreenTitle 36 | 32.2 | **21.5** | "BUILD" (`:858`) |
| PanelTitle 26 | 23.3 | **15.5** | WHAT YOU ARE WEAVING / YOUR SKILLS / VOWS / skill name in tree header (`:926, :1427, :1658, :1536`) |
| Body 19 (= `UiTypography.Label`, the default for every `_ui.Text/TextCenter/TextRight`) | 17.0 | **11.3** | practically everything else: skill names, Source words, state lines, MET/UNMET, keystone chips, vow names and demands, library tile names, variation/reinforcement names, RESPEC, hover body, tab labels, toasts |
| Secondary 16 | 14.3 | **9.6** | the two header sentences (`:860, :866/:869`), NOW / WITH THIS labels (`:1590, :1238`), "hover a skill to compare" (`:1250`), the delta % (`:1241`) |
| Caption 14 | — | — | not used on this screen (good) |

Font em is not cap height: at Body the capitals are ~8 px tall at 720p; at Secondary ~6.7 px.

---

## 2. Findings

### P0 — readability / hierarchy

**P0-1. The whole screen is pre-shrunk by 10.4 %, then shrunk again to 720p.** Evidence: `Game1.cs:5323`;
in `weave.png` the left panel's frame ends at y≈885 although `SlotsPanel` is authored to y=994
(`WeaveScreen.cs:334`); a 112 px dead band runs across the bottom (`UiKit.cs:719–723` admits it). Every
finding below is aggravated by this. Brief §3–5: body must be comfortably readable at 720p; here Body
renders at 11.3 px.

**P0-2. Two sentences at Secondary in the header, permanently.** `:860` "A SKILL IS LEARNED ON THE
MASTERY TREE, THEN WOVEN INTO A SLOT" and `:866/:869` the DISCIPLINE line, both `UiTypography.Secondary`
→ 9.6 px at 720p. `720_header` crop: both lines are a grey smear; the "(E)" hotkey is unreadable.
Violates §6 (Caption/Secondary must not carry sentences), §11/§99-law-4 (do not explain the system on
every visit), §66 (legacy "WOVEN").

**P0-3. The one line the player must act on is drawn as disabled.** "EMPTY VOW SOCKET" is `Dim`
(#3A3A44) on a 0.7-alpha #0E0B16 plate (`:1098–1100`). In `720_slots` (3×) it is barely
distinguishable from the plate; at native 720p it is invisible. Same Dim used for "HOVER ANYTHING TO
READ IT." (`:1379`), "NO VOW SWORN" (`:1760`), the untaken variation's name and Source (`:1579–1580`),
and unaffordable reinforcement names (`:1608`). §7: secondary must not look disabled; the untaken
fork is *a road not taken*, not an unavailable control.

**P0-4. No hierarchy between the three columns; the eye lands on chips, not on skills.** In
`weave.png` the brightest objects are the two gold ACTIVE chips (`:1033–1036`), the cyan-outlined
SPLAY card (`:1354, :1365`) and the gold VOW OF COMPLETION medallion (`:1705`). The skill *names* — the
answer to the screen's question — are 17 px Bone Body text (`:1022`), quieter than their own kind
chips. Three 850 px panels of equal height, all framed (ornate `Panel` at `:925`; `PanelQuiet` at
`:1392`, `:1657` — which in the capture still reads as a full gold scrollwork frame), plus a framed
`PanelQuiet` card *inside* the middle frame (`:1354`, the variation cards) plus ornate tab art
(`:1409–1411`). §9: too many surfaces at PRIMARY weight. §99-law-1: the current decision is not obvious.

**P0-5. Style is never shown.** Rows show the Source word (`:1023`) and library tiles carry the style
"by ORDER" only (`:447` comment). Brief §69 requires Style on the skill card; §8 forbids meaning by
position/colour alone. The player cannot tell SPRAY is VOLLEY from this screen.

**P0-6. Configuration line and Source are 11 px at 720p and the Source shown can be stale.** State
line "SPRAY · SPLAY 0/3  +1" (`:1052–1060`) is Body (11.3 px at 720p, `720_slots` crop). For slot 1 (BLOW,
no variation chosen) the row prints "BODY" from the per-slot fallback `SkillChoice.Source`
(`PlayerLoadout.cs:36–38`, `WeaveScreen.cs:1023`) — a legacy per-slot Source the brief (§70) says the
player must never think they equip separately.

**P0-7. Text drawn through the frame and off the panel.** `DrawReadout` floors the NOW row
(`:1212`) but the region-matchup loop (`:1285–1294`) has no floor: in `weave.png` "NATURE STRONG x1.15"
is printed across the bottom ornament (y≈873) and "BODY WEAK x0.87" / "MIND even x1.00" sit *below*
the panel on the scrim (y≈895–917). At 4 slots — the normal build — this always happens. §94 clipping.

### P1 — layout / UX

**P1-1. Not master-detail; no inspector (§14, §68).** "Details" are scattered: DPS readout in the
LEFT column under keystones (`:1177`), the only sentence box at the FOOT of the middle (`:1369–1384`),
level on the middle header (`:1547`), vow prose at the foot of the RIGHT (`:1762–1772`). Library and
tree are two *faces* of one panel (`:466, :1401–1414`), so "what could go here" and "what is this
becoming" are never visible together.

**P1-2. Vow is not a validator (§71).** Rows give name, demand, MET/UNMET, multiplier (`:1704–1719`)
but never *what breaks it* ("SKILL RATE 1.25x vs MAX 1.00x", "YOUR BUILD HAS BODY, MIND"). Binding needs
three clicks across two panels (select slot → click seal → click BIND row, `:809–841`) and the bind
target is a cross-panel dependency the eye cannot see. Right column shows 2 known vows and ~230 px of
black reserved for 5 (`VowRows=5`, `:500`).

**P1-3. The ACTIVE/PASSIVE chip is a hidden swap control.** Clicking it replaces the slot's skill with
the style's *other* skill (`:651–675`), changing the row's name under the click; it is the brightest
element of every row (gold outline, `:1033–1035`) though gold there means neither selected nor earned
(§10). Redundant with the library, where both skills are one click away. Candidate for removal;
keep ACTIVE/PASSIVE as a quiet tag.

**P1-4. The skill tree is a grid, not a tree (§70).** Variation fork (`:1558–1588`) and reinforcements
(`:1599–1616`) are unconnected cards; a 130 px gap sits between the LEVEL line (TreeTop+62) and the
fork (TreeTop+150); reinforcement cards are 150 px tall for one word (`720_middle` crop: TWIN / NOCK /
FLIGHT floating in dark boxes). While no variation is chosen the reinforcements are not drawn at all
(`:1592–1594`), so a level-0/1 skill shows nothing of what each fork buys next. Locked reinforcements
never say why ("LEVEL 2 — 11 MORE WAVES"); the level line is 300 px away (§83).

**P1-5. Keystones are unexplained.** Chips are name-only (`:1150`), no glyph, no effect line, and the
hover box only covers the middle column — `Keystone.Blurb` (`Build.cs:189`) never reaches the player
here. §99-law-15.

**P1-6. "WHAT THIS BUILD DOES" is honest but mislabelled and mis-scoped.** Number comes from
`DamageBench.Measure` against a reference dummy (`:1327`) — a bench, not the live fight; the label
should say so. Region matchup judges only the *selected slot's* Source (`:1281–1283`) with no label
saying which skill it is about ("NATURE STRONG x1.15" is about SPRAY's MIND).

**P1-7. Redundant / stale labels.** Tab "THIS SLOT" (`:1412`) names a slot, not the skill tree it
shows; on the library face the tab "YOUR SKILLS" sits directly above a PanelTitle "YOUR SKILLS"
(`:1412` vs `:1427`). "COPY BUILD CODE" — a screen-level action — floats inside the VOW panel
(`CopyCodeBtn` `:52`, `:880`). Hotkey letters "(E)", "(P)" inside prose (`:869, :701, :1128, :1668`).

**P1-8. No keyboard or gamepad path.** `Update` reads only mouse (`:581`). `PlayerLoadout.CycleSource /
CycleVow` exist for cycle-and-confirm and have zero callers in the Game project. §89 keyboard
interaction; technical-preferences gamepad requirement.

**P1-9. Wasted vertical space.** 112 px dead band (P0-1); ~230 px black in the vow column; 146 px hover
box that mostly reads "HOVER ANYTHING TO READ IT."; ~110 px empty under the vow prose; 190 px below all
three panels at 1080p in `weave.png`.

### P2 — polish

- Vow multiplier text spills past its 48 px diamond (`:1708`; visible in `720_vows` crop and in
  `weave.png` "x1.30" crossing the outline).
- The "×" remove glyph (`:1085`) is a ~4 px speck at 720p (`720_slots` crop); its 30×30 target
  (`DropX`, `:341`) is under the Fitts padding the project mandates.
- Gold means five things on one screen: panel titles, available variation/reinforcement (`:1579,
  :1610`), active kind chip, live vow, free level "+1". §10.
- Dead code found during the read (verify before deleting): `PickCell` (`:1478`), `Titled` (`:1640`),
  `FitBig` (`:1647`), `LibStyleW` (`:457`), `LibRowH` (`:439`) each appear once (definition only);
  `lifted` (`:1069`) assigned, never read; duplicated `<summary>` blocks at `:343–352` and
  `:1487–1510`.
- `DrawReadout` mutates `Loadout` inside `Draw` for the hover preview (`:1228–1230`). Works
  (single-threaded) but is the kind of UI→model write §93 warns about; a `ToBuild` on a copied
  skill list would be safer.
- `capture.sh` header (lines 11–13) does not list the `weave` mode although `Game1.cs:1490/:2048`
  accept it.

---

## 3. Legacy vocabulary reaching the player, or naming the classes (§66, §67, §101)

Player-facing strings:
- `WeaveScreen.cs:926` panel title **"WHAT YOU ARE WEAVING"**.
- `:860` **"…THEN WOVEN INTO A SLOT"** (permanent header line).
- `:866` **"A VOW ON A FAR-STYLE SKILL PULLS IT ONE RING CLOSER"** — hexagon/"far Form" vocabulary
  re-skinned as style; also "DISCIPLINE" is a fourth name for Style (code says Affinity,
  Specialisation, Discipline, Style).
- `:691` toast **"SLOT WOVEN."**, `:647` **"SLOT UNWOVEN."**
- `Unlocks.cs:255` slot-4 banner body **"The full weave."** (shown on BUILD via
  `Onboarding.BannerFor`, `Onboarding.cs:505–510`).

Class / member / fixture names:
- `WeaveScreen` (`WeaveScreen.cs:37`) is the BUILD screen; `BuildScreen` (`BuildScreen.cs:28`) is the
  MASTERY screen and its summary still says "the Source/Form/Vow composition" (`:18`).
- `FormHexDiagram` (`FormHexDiagram.cs:41`) draws six **Styles** from `StyleAffinity.Factor`
  (`:155, :171`) — it truly represents Style, so per §67 rename to `StyleHexDiagram`; its remarks
  call them Forms (`:32, :70, :108, :112, :118`) and `IconKey` maps to `icon_form_*` assets
  (`:319–327`). Called only from the Mastery screen (`BuildScreen.cs:1320, :1394`).
- `Game1.cs:477` fixture id `"weave"`, `_showWeave`, `_weave`; `sfx_weave` (`WeaveScreen.cs:622, :805`).
- Core names not shown to the player but stale: `VowDemand.EveryWeaveFilled` (`Vows.cs:70`),
  `WeaveContext` (`Vows.cs:187`), `PlayerLoadout` remarks "which Forms the player wove" (`:17`),
  "a build is a CHOICE of Forms" (`:66`).
- Comments in `WeaveScreen.cs` that still reason in Form terms: `:28–29, :252, :367, :375, :417,
  :575, :986–987, :994, :1254, :1301–1308, :1474–1475, :1493`.
- Stale UX standard: `assets/art/idlexidle_ux_screen_guide_standard.md:68` "Build: fixed Source + Form
  + Vow setup", `:123` Form glyph fallback (§88).

Current-vocabulary copy that is **fine** and should be kept: Build tour cards (`Onboarding.cs:252–263`),
library lock text "…LEARNED ON HAMMER'S ROAD, ON THE MASTERY TREE" (`:798, :1447`), Vow catalogue copy
(`Vows.cs:253–358`), `DemandText` (`:546–566`).

---

## 4. What is GOOD and must be kept

- **Real data everywhere.** Vow validity via `Vows.IsActive(v, SoloBattle.DescribeBuild(...))`
  rebuilt every frame (`:578–579`); DPS via `DamageBench.Measure` (`:1327`) with a cached swap-and-measure
  hover preview (`:1219–1232`); region matchup via `SourceMatchup.Effectiveness` +
  `BandCycles.RosterFor` (`:1273–1293`); gear-enchant wants via `EnchantNeed.MetBySkills` (`:1310–1323`).
- **Unique skill glyphs** on rows, tiles and the tree header (`icon_skill_{id}`, `:1010, :1455, :1534`)
  — §69 satisfied on this point.
- **Cast-order spine** with the slot number (`:972–975`) and drag-to-reorder with a lit drop target
  and hollow source row (`:948–966`); the message names the consequence, not the gesture (`:625`).
- **Source carried three ways** — gem, colour, word (`:988–989, :1023`) — §8 done right.
- **MET/UNMET as word + colour** (`:1109, :1719`), Vow multiplier visible (`:1708`).
- **Locked library tiles explain why** with the road that teaches them (`:798, :1445–1447`) — §83.
- **Empty states exist**: "PICK A SLOT ON THE LEFT." (`:1396`), "THIS SLOT IS EMPTY / PICK ONE OF YOUR
  SKILLS…" (`:1518–1521`), "YOU KNOW NO VOWS YET. LEARN THEM IN TRAITS" (`:1667–1668`) — §82.
- **Safe reversible actions have no confirm**: pick, swap, respec (free, says so `:1627`), bind/break —
  §85 correct.
- **Flourishes**: pick flash and bind-chain strip (`:977–984`, `:179–206`) — §86/§87 correct in kind.
- **Fit/ellipsis and bounded wrap** so long strings cannot invade neighbours (`:1776–1802`).
- The Build tour copy (`Onboarding.cs:252–263`) and the "A NEW SKILL SLOT" banner appear only on BUILD
  (`Onboarding.cs:507`) — §12 already satisfied for this screen.

---

## 5. Proposed layout (authored 1920×1080; the content must not be pre-shrunk — see note)

**Note on the inset.** Either author this screen to the real free area (x 180…1900, y 0…1080) and draw
it at scale 1.0, or keep the inset but raise rungs so that *rendered* Body ≥ 19 px at 1080p. The
proposal below is written for scale 1.0 inside x 200–1880.

```
y 0–96    HEADER STRIP  "BUILD" (ScreenTitle, centred at x 1040). No sentences. Right end: small
          COPY BUILD CODE icon-button (44x44) + toast line. The contextual ScreenBanner ("A NEW SKILL
          SLOT...") is the ONLY teaching allowed here, and only while owed.

x 200–700 (w 500), y 110–1000   YOUR LOADOUT — the one PRIMARY (ornate) surface.
  Title "YOUR LOADOUT" PanelTitle. Group label "ACTIVE — take a turn" (Secondary, readable muted),
  rows 96 px each; divider; group label "PASSIVE — always on"; rows. Row anatomy:
    spine 26 px with cast-order number | skill glyph 56 px | NAME Headline 24 (Bone)
    line 2 Secondary: "VOLLEY · LV 1" (style word + level)
    line 3 Body: Source gem 22 px + "SPLAY · MIND · 0/3"
    right: vow pill (quiet): "DELIBERATE X" ember / "COMPLETION OK" gold / "— no vow" slate.
  Selected row: gold edge (gold = selected). Empty row: "EMPTY — pick a skill" in readable Slate.
  Ghost row for the next slot: "OPENS AT WAVE N" from Unlocks.SkillSlots.
  Below rows: KEYSTONES — two 44 px quiet chips (glyph + name), click selects -> inspector.
  Bottom 120 px: BUILD DAMAGE (BENCH)  "379 / s" PrimaryValue 30; hover delta "+12 %" beside it.

x 720–1260 (w 540), y 110–1000   SKILLS — QUIET surface (dark translucent, 1 px border, no scrollwork).
  Top 420 px: SKILL LIBRARY grouped by style: six 64 px rows, each = style glyph + "HAMMER" label +
  two 200x56 tiles (glyph, name, ACTIVE/PASSIVE tag). Learned = Bone; equipped = gold edge; not
  learned = Slate with "LEARN ON HAMMER'S ROAD" on hover/inspector (never Dim text).
  Bottom 470 px: SKILL TREE of the selected skill, drawn as a tree: header glyph+NAME+"LEVEL 1 · 5/16
  WAVES" -> two variation cards 250x120 (Source gem, VARIATION NAME, "MIND" label, one-line effect)
  connected by 2 px rails -> three 160x84 reinforcement chips under the taken variation, locked ones
  reading "LEVEL 2 — 11 MORE WAVES". Untaken fork stays readable (Slate), not Dim.

x 1280–1880 (w 600), y 110–1000   INSPECTOR — SECONDARY frame (thin bronze). §14 order:
  CATEGORY  "SKILL · VOLLEY · ACTIVE"          (Secondary)
  NAME      "SPRAY" + glyph 64 px             (Headline)
  IDENTITY  SkillDef.Line                      (Body)
  WHAT IT DOES  variation line + owned reinforcement lines (Body)
  REQUIREMENTS  "Learned on VOLLEY's road" / "Level 2 in 11 waves" (Body)
  VOW (validator block, 600x150):
      "VOW OF THE DELIBERATE  x1.75"          (Body, gold if valid)
      "DEMAND   skill rate max 1.00x"
      "YOUR BUILD   skill rate 1.25x"          <- WeaveContext.SkillRate
      "BROKEN X — Your build has BODY, MIND"  <- Build.Skills sources for SingleSource
      [CHANGE VOW v] opens the known-vow list in place (rows: name · demand · MET/UNMET · x mult)
  KEYSTONE block when a keystone is selected: name, Keystone.Blurb, sockets "1 / 2".
  REGION line: "SPRAY'S MIND IN VERDANT HOLLOW: NATURE strong x1.15 · BODY weak x0.87".
  PRIMARY ACTION (600x56, gold): EQUIP TO SLOT 2 / CHOOSE SPLAY / TAKE TWIN / BIND VOW — one per state.
  Secondary text actions under it: RESPEC (free) · REMOVE FROM SLOT.
```

**REMOVED**: both header sentences (`:860, :866/:869` → tour card / inspector CATEGORY line); the
ACTIVE/PASSIVE swap toggle (`:651–675, :1030–1036`); the two middle tabs (`:466, :1401–1414`); the
"HOVER ANYTHING TO READ IT" box (`:1369–1384`); the "NO VOW SWORN / BREAK THE VOW" bar (`:1757–1760`);
the free-standing VOWS column (its list becomes the CHANGE VOW picker).
**MOVED**: COPY BUILD CODE → header; DPS readout → foot of YOUR LOADOUT with a PrimaryValue number;
region matchup and gear-wants → inspector; keystone chips → under the loadout rows; vow prose →
inspector validator; the per-row "×" → inspector REMOVE FROM SLOT.
**RENAMED**: "WHAT YOU ARE WEAVING" → "YOUR LOADOUT"; "THIS SLOT" → "SKILL TREE"; "WHAT THIS BUILD DOES"
→ "BUILD DAMAGE (BENCH)"; "SLOT WOVEN/UNWOVEN" → "SLOT ADDED/CLEARED"; "DISCIPLINE" → the style word
+ "x2.0 YOURS" tag on the row/inspector; `WeaveScreen` → `LoadoutScreen`; `BuildScreen` →
`MasteryScreen`; `FormHexDiagram` → `StyleHexDiagram` (keep — it draws Styles); fixture mode `weave` →
`build` (and the current `build` mode → `mastery`).

---

## 6. Data honesty — what the brief wants shown, and whether Core already has it

| Wanted on BUILD | Status | Where |
|---|---|---|
| Skill name, glyph, kind (ACTIVE/PASSIVE) | EXISTS | `SkillDef.Name/Id/Kind`, `TakesABeat` (`SkillCatalogue.cs:151–236`) |
| Style on the card | EXISTS (not drawn today) | `SkillDef.Style` |
| Level + waves to next | EXISTS | `SkillProgress.LevelOf/UsesOf/UsesForLevel/FreeOn` (`SkillProgress.cs:53–70`) |
| Selected variation + its Source | EXISTS | `SkillProgress.VariationOf` → `SkillVariation.Source` (`:80–86`, `SkillCatalogue.cs:120–125`) |
| Reinforcement progress | EXISTS | `SkillProgress.HasReinforcement`, `SkillVariation.Reinforcements` |
| Effective per-slot Source after variation | EXISTS | `Loadout.ToBuild(...)` → `Build.Skills[i].Source` (`Build.cs:217, :385`) |
| Vow validity | EXISTS | `Vows.IsActive(v, SoloBattle.DescribeBuild(build, hunter))` (`Vows.cs:369`, `SoloBattle.cs:2047`) |
| Vow reward multiplier | EXISTS | `Vows.Multiplier` (`Vows.cs:236`) |
| "What breaks it" — numbers | EXISTS | `WeaveContext` fields: DistinctStyles/Sources, SkillsWoven/SkillSlots, CritPercent/BaseCritPercent, SkillRate, Defence, KeystonesWorn, WornSlots (`Vows.cs:187–197`) |
| "What breaks it" — names ("BODY, MIND") | EXISTS, different read | enumerate `Build.Skills` Sources / `SkillDef.Style`; no telemetry |
| Cast priority | EXISTS as rule | slot order → `SoloBattle.ResolveWave` (per `PlayerLoadout.cs:109–115`) |
| Build damage now / with hovered skill | EXISTS | `DamageBench.Measure(...).Dps` (`WeaveScreen.cs:1327`) — label as BENCH |
| Region matchup | EXISTS | `SourceMatchup.Effectiveness`, `BandCycles.RosterFor` |
| Style affinity factor / vow lift | EXISTS | `StyleAffinity.Factor(aff, style, vowSworn)` (`StyleAffinity.cs:25–34`), `MasteryTree.Affinity()` |
| Next slot unlock requirement | EXISTS | `Unlocks.SkillSlots(f)`, `Unlocks.SkillSlotNote` (`Unlocks.cs:248`) |
| Which mastery node teaches a not-learned skill | EXISTS | `MasteryCatalog` SkillRoad nodes, `MasteryTree.LearnedSkills()` (`MasteryTree.cs:308`) |
| Keystone effect text | EXISTS (not drawn) | `Keystone.Blurb` (`Build.cs:189`) |
| Gear enchant waiting on a style | EXISTS | `Hunter.WornEnchantments`, `EnchantNeed.Label/MetBySkills` (`Enchantments.cs:45–61`) |
| Per-skill damage share / casts last run | NEEDS telemetry | `SkillProgress` stores only waves cleared; not required by §66–71 — omit |

---

## 7. Fixtures

**Exists** — `RH_SHOT_MODE=weave` (`Game1.cs:2048–2132`): 22 trait points, all regions conquered, keystones
learned (`ks_glass_cannon/ironclad/echo/greed`), every SkillRoad node taken, capacity ≥ 4, four skills
(starter `hammer_blow` + `volley_spray`, `field_mire`, `snare_jaws`), skill progress posed per slot —
unchosen / chosen 0/3 / chosen with a spare level / 3/3 — tree tab opened (`DevOpenSkillTree`), slot 2
selected (`DevPose(1, …)`), optional `RH_SHOT_POSE=<vowId>[,t]` to open a BIND row and hold the chain.
`tour … Build` reuses it (`Game1.cs:477`). `RH_SHOT_EXPLAIN=SkillSlot2..4` poses the "A NEW SKILL SLOT"
banner. `capture.sh` header does not list `weave` (doc gap).

**Gaps against §95 "BUILD (4 skills, variation, reinforcement, valid/invalid Vow)"**:
1. No vow is **sworn on a slot** by default (`DevPose(1, null)`), so the row socket in MET and UNMET
   states, the SWORN tag and the BREAK THE VOW bar are unphotographed. Needs two slots: one with a
   valid vow (e.g. `vow_complete`), one with a broken vow (e.g. `vow_pure` on a 4-Source build).
2. No Discipline is set (header reads NO DISCIPLINE YET), so the style-factor tag (`:1065–1074`) never
   appears. Take one Specialisation in the fixture.
3. The comment claims a fifth slot (`weave_5`) and a fourth keystone below the fold; the capture shows
   4 rows, no "+ ADD A SKILL" button and 3 chips with no scroll hint — neither state is actually posed.
4. Only the tree face is captured; the library face (12 tiles, learned vs not-learned) needs its own
   shot (a `weavelib` variant or `RH_SHOT_TAB=0`).
5. The hover comparison ("WITH THIS … +12 %") cannot be posed — add `RH_SHOT_HOVER=<skillId>`.
6. An empty-slot state and the "YOU KNOW NO VOWS YET" empty state are not posed.
7. Every capture needs a 1280×720 counterpart reviewed as a P0 gate (§94/§96).
