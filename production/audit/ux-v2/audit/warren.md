# WARREN screen — UX V2 audit

Audited 2026-09-01 against `production/audit/ux-v2/BRIEF.md` (sections 0-15, 77-81, 82-102) and the
current runtime (`src/IdleXIdle.Game/WarrenScreen.cs`, `src/IdleXIdle.Core/Warrens/Warren.cs`, host wiring in
`src/IdleXIdle.Game/Game1.cs`). Screenshots: `baseline/warren.png` (1920x1080) and `baseline/720/warren.png`
(1280x720). Read-only audit; no source was changed.

All px in this report are the screen's OWN 1920x1080 authoring space unless marked "physical".

---

## 0. Does the screen answer its question?

Brief section 100: WARREN = "What is my organization doing while I am away?"

Partly. The overview's TOTAL PRODUCTION rows (WarrenScreen.cs:221-234) do answer "what does it make per
minute", and the inspector's NEXT / cost / depth lines answer "can I upgrade this". But the loudest content
on the page is a WARREN BONUSES strip of five derived percentages and a one-line formula lecture
(WarrenScreen.cs:296-335), which answers a question nobody asked, while the two things the player actually
came for — "which facility can I upgrade right now" and "what stopped this one" — are either absent (no
per-card upgrade-ready marker) or set at the smallest rung on the panel (the REACH DEPTH line, lines 378-383,
Secondary 16).

**Honesty on the brief's vocabulary.** Sections 77, 82 and 95 speak of facility SERVICES, TARGETING and
AUTOMATION. None of that exists in Core. `Warren.cs` is, in full: eight authored facilities that each
produce ONE fixed `WarrenResource` at `BaseRatePerMin x Level x MilestoneMultiplier` (Facility, lines
107-155); a Warren level/XP; three bonus tracks + a conquest bonus (lines 223-238); a conquest unlock ramp
(lines 199-221); a depth-derived level cap (lines 250-301); `Tick` (325-337). There is no member that lets
the player choose what a facility produces, no service, no automation, no schedule. `Core/Automation/
RegionAutomation.cs` is region MASTERY (kills -> RMP) and explicitly says its creature-farm half was retired
2026-08-24 (lines 6-11, 38-47). **Do not build UI for targeting/automation/services; there is nothing to
bind it to.** The only honest "SERVICE" a card can state is which wallet the facility feeds and what that
wallet buys — data the screen already carries in prose (`Facilities.All` descriptions, Warren.cs:54-61; the
overview footer, WarrenScreen.cs:243-245).

---

## 1. What the player physically sees

### 1.1 Effective text sizes (the P0)

Menu screens are drawn through the overlay inset: `Game1.OverlayScale = (1920-180-20)/1920 = 0.896`
(Game1.cs:5323), then the 1920x1080 canvas is bilinear-downscaled to the window. So on a 1280x720 window
every rung on this screen is `rung x 0.896 x 0.667 = rung x 0.597` physical pixels:

| Rung | authored | physical @1080p | physical @720p | used on WARREN for |
|---|---|---|---|---|
| ScreenTitle 36 | 36 | 32 | 21.5 | "WARREN" |
| PanelTitle 26 | 26 | 23 | 15.5 | WARREN OVERVIEW / WARREN BONUSES / NURSERY |
| Headline 24 | 24 | 21.5 | 14.3 | 4 total-rate rows, 5 bonus %, inspector OUTPUT |
| ButtonText 21 | 21 | 19 | 12.5 | UPGRADE / DEPTH LOCKED |
| Body 19 | 19 | 17 | 11.3 | descriptions, WARREN LEVEL 23, LEVEL n on cards, OUTPUT, COST TO UPGRADE, GLEAM/DUST |
| Secondary 16 | 16 | 14.3 | **9.6** | ~40 strings — see below |

Secondary-rung strings on this screen (from code): subtitle (111); Warren name caption (197); XP readout
(216); NEXT LEVEL (218); (IDLE RATE) (223); 8 card names (267/280); 8 card rates (291); locked "TAKE ANOTHER
REGION" (270); the bonus formula sentence (307-312); 5 bonus labels + 5 "why" lines (332-333); inspector
NEXT (351); both milestone texts (356-357); both cost ratios and both OK/X verdicts (401-402); the REACH
DEPTH / INSTANT UPGRADE line (378-383). That is roughly forty strings, including every number the player
must compare, rendered at ~9.6 px on a 720p screen.

**Pixel evidence, 720/warren.png (crops made with PIL, viewed 1:1):**
- Facility card "NURSERY", "+783 /min": cap height ~7 px; the card names are Slate-on-dark and read as
  greyed-out labels, not as names.
- Inspector "131.9M / 266K  OK", "12.6K / 1.2K  OK": ~7 px cap height, green-on-black, the smallest text
  on the panel — and it is the cost ledger.
- "REACH DEPTH 95 ON AN EXPEDITION TO UPGRADE": gold, ~7 px cap height, sitting alone above a 72 px button.
  This is the one line that tells the player why the primary action is dead, and it is the least legible
  thing on the panel.
- Bonus strip "EACH IS 1 PLUS ALL THREE PLUS CONQUEST PLUS ITS OWN CHIP", "3% A LEVEL AFTER 1": ~7 px,
  Slate; a 50-character sentence at caption scale (brief section 6: captions do not carry sentences).
- Overview footer paragraph (three lines, Slate, Body): legible but only just (~9 px cap).
- Body-rung text (descriptions, WARREN LEVEL 23) is readable at 720p with effort; Headline numbers are fine.

### 1.2 Where the eye lands

1080/warren.png, in order: (1) the ornate gold-railed GRID frame — `UiKit.Panel` (line 251), the only
ornate frame on the screen, wrapped around a QUIET-class object (a grid; brief section 9); (2) the selected
Nursery card's gold border; (3) "WARREN BONUSES" — a third gold PanelTitle at the same 26 px as WARREN
OVERVIEW and NURSERY, so three panel identities compete at equal weight, plus the screen title above; (4)
the five bonus percentages at Headline; only then (5) the TOTAL PRODUCTION rows and (6) the inspector.

Gold appears on: 3 panel titles, the grid frame rails, the selected card border, "LEVEL n" on all 8 cards
(line 289 — gold on unselected cards too), up to 40 milestone pips, the TOTAL PRODUCTION heading, the
MILESTONES row, the REACH DEPTH line, the XP arc, the screen title rule. Brief section 10: gold must answer
selected/earned/available/primary. Here it is mostly decoration.

### 1.3 Space

- Content occupies y 154-862 (708 of 1080). Below the panels is a 218 px dead band containing only the
  global guide banner (y ~980-1054). Above them, the subtitle ends at ~100 and the panels start at 154.
  720/warren.png: the bottom third of the frame is background art plus the tutorial strip.
- The grid frame is 920x500 for eight 206x200 cards (lines 65-72); each card is ~30% icon plate, with two
  data lines. The inspector is 480 wide with ~180 px of empty space between the depth line (y+518) and the
  button (y+612).
- The guide banner on this capture is `TutorialStep.Watch` — "YOUR CHAMPION FIGHTS ON ITS OWN" — HUNT
  teaching drawn over the WARREN (Tutorial.cs:237; drawn as chrome by Game1.DrawGuideBanner, 3109-3115).
  Brief sections 11-12 name exactly this. Global, not Warren code, but it is the reason this screen
  reserves its bottom fifth.

---

## 2. Findings

### P0 — readability / hierarchy

| # | Finding | Evidence |
|---|---|---|
| P0-1 | ~40 strings, including every comparable number and the lock reason, render at Secondary = 9.6 px physical at 720p (16 x 0.896 inset x 0.667). | WarrenScreen.cs lines listed in 1.1; 720/warren.png crops: 7 px cap height. |
| P0-2 | Hierarchy inverted: five derived bonus percentages sit at Headline (24) while the cost ledger and REACH DEPTH line sit at Secondary (16). The decision data is quieter than the explanation. | WarrenScreen.cs:331 vs 378-383, 401-402. |
| P0-3 | Gold is not meaning: three equal gold PanelTitles, gold LEVEL on every card, gold pips, an ornate gold frame around a grid. The selected card's gold border has to compete with all of it. | WarrenScreen.cs:187, 251, 289, 298, 347; 1080/warren.png. |
| P0-4 | Secondary reads as disabled: unselected card names are Slate at 9.6 px; locked cards use Dim/Slate for the same job. A player cannot tell "not selected" from "locked" by the name colour. | WarrenScreen.cs:267 (Dim), 280 (Slate unless selected). Brief section 7. |
| P0-5 | HUNT tutorial banner ("YOUR CHAMPION FIGHTS ON ITS OWN") drawn over the Warren. | Tutorial.cs:237; Game1.cs:3109-3150; both screenshots. Brief 11-12. Global fix, cross-reference. |
| P0-6 | 218 px dead band under the panels (y 862-1080) and a 54 px gap above them; the layout is sized for a bottom banner that must go. | WarrenScreen.cs:49-56 rects; both screenshots. |
| P0-7 | The overlay inset costs every menu screen a further 10.4% of text size before the window downscale. Removing it (draw menu screens 1:1 in x 180-1920) is the cheapest readability win available and is orthogonal to the brief's UI Scale setting. | Game1.cs:5309-5337 (OverlayScale/ToOverlay). Global. |

### P1 — layout / UX

| # | Finding | Evidence |
|---|---|---|
| P1-1 | Numbers do not add up: cards and the inspector show RAW `BaseOutputPerMin` (before Warren/conquest bonuses), the overview shows boosted `ProductionPerMinute`. Nursery 783 + Tunnels 715 = 1,498, overview says +3.8K. Neither is labelled. | WarrenScreen.cs:291, 350 vs 228; Warren.cs:135 vs 241-246. |
| P1-2 | No per-card "can upgrade now" state. The player must click each of eight cards to find the affordable one. `Warren.CanUpgrade / CanAfford / IsAtLevelCap` already exist. | WarrenScreen.cs:253-292 draws no such marker; Warren.cs:290-301. Brief 1, 81, law 5. |
| P1-3 | Locked card says "TAKE ANOTHER REGION" for every locked facility; the 2nd, 3rd... need 2, 3... more regions. Derivable from the catalog index and `UnlockedFacilityCount = 2 + ConqueredRegions`. | WarrenScreen.cs:270; Warren.cs:207. Brief 83. |
| P1-4 | WARREN BONUSES is a formula lecture ("EACH IS 1 PLUS ALL THREE PLUS CONQUEST PLUS ITS OWN CHIP", "3% A LEVEL AFTER 1", "0.9% A LEVEL") in its own 920x196 titled panel. Brief 79 asks for ONE summary region; law 14 says do not display because the model has it. | WarrenScreen.cs:296-335. |
| P1-5 | The same lesson three times: subtitle (111), overview footer paragraph (243-245), tour card 3 (Onboarding.cs:330-332) all say "the warren earns while you are away". Law 4. | Cited lines. |
| P1-6 | Inspector order is description -> OUTPUT -> milestone -> COST -> requirement -> button. Brief 14 order is category / name / identity / what it does / REQUIREMENTS / cost-state / action; the blocker (depth) belongs above the cost, not under it at the smallest rung. | WarrenScreen.cs:337-393. |
| P1-7 | "INSTANT UPGRADE  ·  NO WAIT" occupies the requirement slot whenever the facility is not capped. It is not information the player can act on. Law 14. | WarrenScreen.cs:382. |
| P1-8 | Vocabulary: the Warren says "REACH DEPTH 95 ON AN EXPEDITION"; the rest of the game and the brief say WAVE and HUNT ("Reach wave 5", Unlocks.cs:183-184; brief 83 `REACH WAVE 8`). One word for one thing. | WarrenScreen.cs:380. |
| P1-9 | Cost verdict is "OK" / "X" at Secondary in green/red. "X" is not a word; the useful fact is the shortfall (cost minus owned, derivable). | WarrenScreen.cs:401-402. Brief 8, 83. |
| P1-10 | Cards have no hover state (click only). Brief 15: hover = highlight. | WarrenScreen.cs:253-292 (no hover branch). |
| P1-11 | Warren name caption shows the ACTIVE region, not "the deepest conquered region" the model documents; the fixture prints THE PALE CHOIR, which is not conquered. Either rename the caption to what it is or drop it. | Game1.cs:1117 vs Warren.cs:184-185; fixture Game1.cs:1974. |
| P1-12 | No designed fresh state: a first-conquest Warren shows 3 open cards and 5 LOCKED placeholders inside the same 920x500 ornate frame. No fixture exists to look at it (see section 5). Brief 82. | Warren.cs:207 (ramp starts at 3); WarrenScreen.cs:260-272. |
| P1-13 | The summary never states the cap or the champion's depth, so "why can't I upgrade anything" is only discoverable one card at a time. `FacilityLevelCap` and `Career.DeepestAnywhere` exist; depth is not passed to the screen. | WarrenScreen.cs:36-40 (host-set props: Warren, GleamOwned, DustOwned only); Game1.cs:1124, 5221. Brief 79. |

### P2 — polish

| # | Finding | Evidence |
|---|---|---|
| P2-1 | `Violet` is documented as the Insight-era accent and still paints the Warren-name caption and the crest fallback. Insight was cut 2026-08-31 (P12). | WarrenScreen.cs:28, 145, 197; Warren.cs:12-14. |
| P2-2 | Milestone pips stop at 5 (level 25+); the card then shows no further progress. State the multiplier as text instead. | WarrenScreen.cs:277. |
| P2-3 | Descriptions lead with flavour and end with the resource ("Hatch and raise young. More paws at work, more Gleam."). For a card whose only job is the resource, lead with the resource. | Warren.cs:54-61. |
| P2-4 | XP readout "18,540 / 25,000" at Secondary inside a 34 px bar, on a violet fill: 9.6 px physical at 720p. | WarrenScreen.cs:215-216. |
| P2-5 | The subtitle slogan spends the biggest centred slot under the title on copy (the comment at 105-110 argues for it; brief 6 says the screen title is only the name). | WarrenScreen.cs:111. |
| P2-6 | `farm` shot mode is a deprecated alias for the Warren. | Game1.cs:1988-1991. |
| P2-7 | UX standard says "Warren: fixed producers and automation levels" — automation levels never shipped. | assets/art/idlexidle_ux_screen_guide_standard.md:70. Brief 88. |

---

## 3. Keep (good, must survive the refactor)

- The three-zone skeleton — summary column / facility grid / right inspector — is already the brief-14
  MAIN CONTENT + RIGHT INSPECTOR pattern, and `WarrenScreen.Spotlights(TourTarget)` (lines 40-46) binds the
  three tour cards to those rects. Keep the rect names so the tour keeps working.
- The cap is EXPLAINED, not just enforced, and names the actionable number: `Warren.DepthForNextLevel`
  (Warren.cs:277-287) instead of the cap, with the reasoning written down (250-263). Brief 80 and 83 are
  met in substance; only the size, position and the word DEPTH need work.
- Before -> after is real: `Facility.NextLevelOutput` includes the milestone jump (Warren.cs:137-146), so
  "NEXT +826 /min" (WarrenScreen.cs:351) is honest. Law 8 met.
- Milestones as the long-term goal (`MilestoneTier / NextMilestoneLevel / MilestoneMultiplier`, Warren.cs
  122-132) and the pips + ladder row that show them.
- The facility art on resource-tinted hex plates (lines 281-287) and real currency icons via `ResGlyph`
  (90-103) with an explicit fallback. Eight distinguishable pictures, not eight coloured hexagons.
- The crest with a progress arc (`LevelBadge`, 143-160) and the five-sliced `BarArt` XP bar (211-215) —
  both fixed on playtest evidence and both good.
- One-click upgrade with cost and result visible first, no confirmation modal (brief 85: costly action
  shows cost/result first). Host performs spend + `Upgrade` + `Save()` (Game1.cs:1130-1137).
- Locked cards take no clicks and give a reason (lines 260-272). Fix the count, keep the behaviour.
- Model purity: the screen reads `Warren.Multiplier`, never re-derives it (comment 302-305); host sets
  state each frame, consumes one request (36-45). Brief 92-93 met.
- `Slate` secondary colour measures ~6.9:1 on the dark panels (UiKit.cs comment above line 863) — the
  colour is fine; the problem is size, not ink.
- Locked-selection fallback (337-343) — a reset can never pose a locked facility in the inspector.

---

## 4. Proposal

Same inset, same three-zone skeleton (tour spotlights survive), one summary region, no bonuses panel, cards
that say what they feed, an inspector in the brief-14 order, room reclaimed from the banner band.

### 4.1 Zones (screen space, 1920x1080 before the 0.896 inset)

```
y   0- 110   TITLE BAND.  "WARREN" ScreenTitle at y24 + rule. Subtitle REMOVED.
y 120- 240   SUMMARY STRIP  x 28-1880 (1852 x 120), QUIET surface, no title.
             left -> right, five cells:
             [crest 84 + "WARREN LEVEL 23" Headline, XP bar 260x28 + "18,540 / 25,000" Body]        320 w
             [DEEPEST WAVE 40  ->  FACILITY CAP LEVEL 8       Headline value / Body label]          320 w
             [EARNS FOR UP TO 24 H AWAY  ·  LAST TRIP +1.2K GLEAM (if retained, see 4.3)]           360 w
             [4 rate chips: icon 38 + "+3.8K /min" Headline + "GLEAM" Body; x2.52 etc. Body under]   4 x 190
             [CONQUEST 4 REGIONS  +40%  Headline / Body]                                            ~90 w spare
y 260-1040   FACILITY GRID  x 28-1330 (1302 x 780), QUIET surface (no ornate frame).
             8 cards 4x2, 306 x 372, gutters 26 (4*306 + 3*26 = 1250; 2*372 + 26 = 770).
             Card:  NAME Headline 24 Bone (always Bone; never Slate)                          y+16
                    resource pill "GLEAM -> TRAINING" Body, resource colour + icon + word     y+50
                    icon plate 132 x 114 centred                                             y+86
                    "LEVEL 18" Body Bone (gold ONLY on the selected card)                    y+214
                    "+783 /min BASE" Headline                                                y+244
                    "NEXT MILESTONE L20  x1.60" Body Slate                                   y+280
                    status chip Body (full width, y+316, 40 h):
                       UPGRADE READY  (gold chip)             <- Warren.CanUpgrade
                       NEEDS 134K GLEAM / NEEDS 300 DUST      <- cost - owned
                       REACH WAVE 95                          <- IsAtLevelCap, DepthForNextLevel
             Locked card: name Bone-dim, plate 40% alpha, "LOCKED" Body,
                    "CONQUER 2 MORE REGIONS" Body            <- catalog index vs UnlockedFacilityCount
             Hover: 1px Bone outline; Selected: 4px gold rails (as today).
y 260-1040   INSPECTOR  x 1360-1880 (520 x 780), SECONDARY frame (thin bronze; the only framed panel).
             FACILITY · GLEAM                     category role, set at Body 19 (not Secondary)
             NURSERY                              PanelTitle 26
             LEVEL 18  ·  3 MILESTONES x1.45      Body
             description                          Body, Bone
             -- WHAT IT DOES
             +783 /min  ->  +826 /min             Headline, arrow, Met for the gain
             (x2.52 with Warren bonuses = +1.97K -> +2.08K /min)   Body Slate   <- Multiplier(r)
             NEXT MILESTONE: LEVEL 20  (+15% permanent)            Body
             -- REQUIREMENT   (only when capped; else omitted — no filler line)
             REACH WAVE 95 ON A HUNT               Headline 24, gold
             -- COST
             GLEAM   266K     have 131.9M          Body; shortfall in Ember text if short
             DUST    1.2K     have 12.6K           Body
             [ UPGRADE ]  full width, 72 h, gold state when enabled; label "REACH WAVE 95" when capped
y 1040-1080  margin. No guide banner on this screen.
```

Why 306x372 cards: at 0.597 physical scale a 24 px name becomes 14.3 px and a 19 px row 11.3 px — the
same physical size Body has TODAY at 720p, with the difference that nothing on the card is below Body any
more. If the global typography pass raises Body to ~22-24 (brief 5), the card has the height for it (six
rows x ~40 = 240 + plate 114 + padding).

### 4.2 Removed / moved / renamed

REMOVED
- WARREN BONUSES panel (WarrenScreen.cs:296-335) as a panel. Its five percentages become one Body line under
  the rate chips ("GLEAM x2.52 · DUST x2.27 · MATERIALS x2.36") plus the CONQUEST cell; the per-level
  "why" lines and the formula sentence go away entirely (law 14; available on the Help screen if wanted).
- Subtitle slogan (111). Screen title only.
- Overview footer paragraph (243-245). The card's resource pill says what each output is for.
- "INSTANT UPGRADE · NO WAIT" (382).
- "(IDLE RATE)" (223) — the strip IS the idle rate.
- "OK"/"X" verdicts (402) — replaced by the have/shortfall value.
- Ornate `UiKit.Panel` around the grid (251) — quiet surface instead.
- Gold "LEVEL n" on unselected cards (289) — Bone.

MOVED
- Warren level / XP / conquest / totals: from the 388-wide left column to the full-width summary strip.
- Depth requirement: from a Secondary line above the button to a Headline REQUIREMENT block above COST.
- Milestone ladder: from a bar under OUTPUT to one Body line under the rate, and onto the card.

RENAMED
- "REACH DEPTH 95 ON AN EXPEDITION TO UPGRADE" -> "REACH WAVE 95 ON A HUNT" (matches Unlocks captions
  and brief 83).
- "DEPTH LOCKED" button label -> "REACH WAVE 95" (the label IS the reason).
- Card "+783 /min" -> "+783 /min BASE" (or show the boosted figure and label the strip's total as the sum;
  either way both numbers get a word).
- "TAKE ANOTHER REGION" -> "CONQUER 1 MORE REGION" / "CONQUER k MORE REGIONS".
- The name caption "THE PALE CHOIR" -> dropped, or "ACTIVE REGION: THE PALE CHOIR" if kept.

### 4.3 Data honesty — what already exists vs what would need new state

| Wanted on screen | Status |
|---|---|
| Warren level, XP, XP to next | EXISTS: `Warren.Level`, `Warren.Xp`, `Warren.XpToNext` (Warren.cs:190-194) |
| Total rate per currency (boosted) | EXISTS: `Warren.ProductionPerMinute(WarrenResource)` (241-246) |
| Per-currency multiplier | EXISTS: `Warren.Multiplier(r)`, `ResourceBonus`, `AllProductionBonus`, `ConquestBonus` (223-238) |
| Facility cap | EXISTS: `Warren.FacilityLevelCap`, set by host from `Warren.CapForDepth(Career.DeepestAnywhere(...))` (Game1.cs:1124) |
| Champion depth ("DEEPEST WAVE 40") | EXISTS in host: `Game1.DeepestAnywhere()` -> `Career.DeepestAnywhere(world, deepestEver)` (Game1.cs:5221; Career.cs:68-74). NOT passed to WarrenScreen today — add one host-set int property. No new telemetry. |
| Requirement for a capped facility | EXISTS: `Warren.IsAtLevelCap(kind)`, `Warren.DepthForNextLevel(kind)` (286-290) |
| Before -> after output | EXISTS: `Facility.BaseOutputPerMin`, `Facility.NextLevelOutput` (135-146); boosted variant = these x `Multiplier(r)` |
| Next milestone, current multiplier | EXISTS: `Facility.NextMilestoneLevel`, `MilestoneMultiplier`, `MilestoneTier` (126-132) |
| Upgrade-ready / affordable per card | EXISTS: `Warren.CanUpgrade`, `CanAfford`, `UpgradeCost` (248, 293-301) |
| Shortfall ("NEEDS 134K GLEAM") | DERIVABLE: `UpgradeCost(kind).Gleam - GleamOwned` — arithmetic on existing members |
| Locked-facility requirement ("CONQUER k MORE REGIONS") | DERIVABLE: `Facilities.All` index i is open when `i < UnlockedFacilityCount = 2 + ConqueredRegions` (207), so k = i - 1 - ConqueredRegions. Naming WHICH region: host has `Regions.Next` / the chain (Regions.cs:138-143). No new telemetry. |
| Offline coverage ("earns for up to 24 h away") | EXISTS as a constant: `SaveGame.MaxOfflineSeconds = 24*3600` (SaveGame.cs:779), applied via `CreditedOfflineSeconds` (781-782) |
| Last-trip Warren earnings ("LAST TRIP +1.2K GLEAM") | COMPUTED BUT NOT KEPT: `wOffline` is a local in Game1's load path (Game1.cs:751-757), reported once in `_bootMessage` (795-798) and discarded. Showing it here needs the host to retain `(credited seconds, WarrenYield)` in a field — host-side, no Core change, no new simulation. |
| Facility SERVICE / TARGETING / AUTOMATION | DOES NOT EXIST. No Core member. The only honest "service" line is static copy keyed by `FacilityInfo.Produces` (what the wallet buys). Do not invent a targeting UI or its empty state. |
| "Organization" name | EXISTS but mis-sourced: `Warren.Name` is set to the ACTIVE region (Game1.cs:1117), not "deepest conquered" as documented (Warren.cs:184). |

---

## 5. Fixtures

**Existing.** `RH_SHOT_MODE=warren` (Game1.cs:1971-1986): `_activeRegion = "pale_choir"`; conquers
verdant_hollow, cinderworks, umbral_reach, marrow_wastes (CONQUEST +40%); `Warren.Restore(23, 18_540, {...})`
with facility levels Nursery 18 / Tunnels 17 / ForagingPits 16 / ScavengerRuns 15 / BreedingChamber 16 /
RitualNest 14 / HoardVaults 13 / SentryBurrows 12; +131.9M Gleam, +12.6K Dust. Captured by
`tools/asset-pipeline/capture.sh warren` (mode list, line 11); the three tour cards by
`capture.sh tour <out> Warren 1..3` (line 34); `farm` is a deprecated alias (Game1.cs:1988-1991).
Unit coverage: `tests/unit/IdleXIdle.Core.Tests/Warrens/WarrenTests.cs` (21 tests: ramp, cap, milestones,
tick carry, restore, material facilities).

**Gaps against brief section 95 ("WARREN: levels + targeting + capped").**
1. Every facility is capped in the fixture, so the UPGRADE-enabled state is never photographed.
   `World.Conquer` does not record a depth (Regions.cs:138-143) and `_deepestEver` is 0, so
   `DeepestAnywhere() = 0`, `CapForDepth(0) = 1`, and all eight facilities (levels 12-18) read DEPTH LOCKED.
   Fix: set `_deepestEver` (or a region `BestDepth`) to 80 -> cap 16: Nursery 18, Tunnels 17,
   ForagingPits 16, BreedingChamber 16 stay capped; the other four become upgradeable. Both states in one shot.
2. The LOCKED card state is never photographed: all eight are past level 1, so the grandfather rule
   (Warren.cs:215-221) opens them regardless of the ramp. Fix: a second mode `warrenfresh` — 1 conquest,
   all facilities level 1, depth 20 -> 3 open cards, 5 locked, cap 4 — which is also the brief-82 empty/fresh
   state nobody has looked at.
3. The unaffordable state is never photographed: 131.9M Gleam covers every cost. Fix: seed ~200K Gleam so
   Nursery (266K) reads NEEDS 66K while Sentry Burrows (23K) reads UPGRADE READY.
4. Materials are not seeded: the SCRAP pill shows "3" while the overview claims +234 Scrap/min. Seed
   Scrap/Essence for coherence.
5. "targeting" cannot be posed — no mechanic (section 0). Report the fixture as levels + capped + locked +
   affordable/unaffordable, and say so in the fixture contract.
6. Tour capture: `tour ... Warren` inherits the same fixture, so its spotlights also only ever frame the
   all-capped state.

---

## 6. Legacy vocabulary check

No Form / Weave / WovenAbility / Aptitude / Source x Form text reaches the player from this screen
(grep of WarrenScreen.cs for form|weave|woven|aptitude|source: only `WarrenResource` matches). Residuals:

- WarrenScreen.cs:28 `Violet` "(crest fallback, caption)" — the Insight-era purple; still drawn at 145 and
  197. Insight was cut in P12 (Warren.cs:12-14). Colour only, not a word.
- WarrenScreen.cs:137, 230 — comments mention Insight. Comments only.
- Game1.cs:1988-1991 — `farm` shot mode alias, self-described deprecated.
- assets/art/idlexidle_ux_screen_guide_standard.md:70 — "Warren: fixed producers and automation levels".
- Cross-screen vocabulary drift, not legacy: DEPTH (Warren) vs WAVE (Map, Unlocks, brief 83); EXPEDITION
  (Warren line 380) vs HUNT (nav, brief).
