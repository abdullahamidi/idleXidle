# FORGE — UX V2 audit

Audited 2026-09-01 against `production/audit/ux-v2/BRIEF.md` (sections 0-15, 53-59, 82-102) and the
runtime truth on `feat/hunter-cutout-rig` (post 2026-08-31 refactor). Read-only on source.

Evidence base:
- `src/IdleXIdle.Game/ForgeScreen.cs` (3071 lines, read in full), `src/IdleXIdle.Core/Forge/Forge.cs`,
  `Reforge.cs`, `MergeRecipe.cs`, `src/IdleXIdle.Core/Economy/GemCraft.cs`, `Charters.cs`,
  `Enchantments.cs`, `ItemAffixes.cs`, `HunterProgression.cs`, `Onboarding.cs`, `Tutorial.cs`,
  `Game1.cs` (chrome, inset, fixtures), `UiKit.cs`, `UiTypography.cs`, `tools/asset-pipeline/capture.sh`.
- Screenshots `production/audit/ux-v2/baseline/forge.png` (1920x1080) and `baseline/720/forge.png`
  (1280x720), plus 3x-zoomed crops of the 720p file and a luminance scan of rendered glyph heights.

Brief section-100 question for this screen: **"What will happen if I modify this item?"**

---

## 0. Verdict in one paragraph

The Forge is the most *complete* screen in the game in terms of logic — every price, risk, payout and
"who pays" line reads from Core, the confirmations are in place and proportionate, and the
before -> after compare exists. It fails the brief on **presentation**: four equal columns of one frame
weight, the answer to the screen's question split across two columns 400 px apart (numbers on the ITEM
card, the button on the FORGE column), a permanent manual in the fourth column (YOUR CHARTS prose + THE
FOUR TABS legend + a header sentence — the same system explained three times on one screen), and text
that is physically unreadable at 720p because the whole screen is drawn through a 0.896 inset *and then*
downscaled to 2/3: Secondary (16) lands at 4-5 px cap height, Body (19) at 6 px. The layout also wastes
roughly a third of every column's height. The fix is a hierarchy/layout pass, not a redesign: the code
already has every number the V2 layout needs.

---

## 1. What is on screen today (runtime truth)

### 1.1 Rendering path — a fact the rest of the audit depends on

Every Forge rectangle is authored in 1920x1080 (`ForgeScreen.cs:204, 253-262`), but the screen is
drawn through Game1's **overlay inset**: `Matrix.CreateScale(OverlayScale) * Translation(180, 0)` with
`OverlayScale = (1920 - 180 - 20) / 1920 = 0.8958` (`Game1.cs:4029, 5323-5326, 4091-4092`). So at
1080p every Forge rung is already 10.4 % smaller than the ladder says (Body 19 -> 17.0 px), and at 720p
the effective factor is 0.8958 x 0.6667 = **0.597** (Body 19 -> 11.3 px em, Secondary 16 -> 9.6 px em,
Caption 14 -> 8.4 px em). The fight screen is deliberately NOT inset (`Game1.cs:5319-5320`).

Measured on `baseline/720/forge.png` (uppercase cap height, luminance scan):

| Rung | Authored px | Measured cap height at 720p | Example |
|---|---|---|---|
| ScreenTitle 36 | 36 | 11 px | THE FORGE |
| PanelTitle 26 | 26 | 8 px | YOUR BAG |
| Headline 24 | 24 | 8 px | wallet `131.9M` |
| ButtonText 21 | 21 | 7 px | UPGRADE +1 LEVEL |
| Body 19 | 19 | 6 px | bag rows, CRITICAL CHANCE, tab labels, GLEAM |
| Secondary 16 | 16 | **4-5 px** | header hint, `for merging — not the fight's Source`, `YOU HOLD 12,600 SCRAP`, `pays for upgrades`, YOUR CHARTS prose, THE FOUR TABS legend |

A 4-5 px cap height is below any readable threshold; these lines are texture, not text, at 720p.

### 1.2 Zones (ForgeScreen authoring space, 1920x1080, before the inset)

| Zone | Rect (x, y, w, h) | Frame | Source |
|---|---|---|---|
| Screen title + rule + hint sentence | title at (960, 24); rule (720, 74, 480, 3); hint at (960, 80) | none | `ForgeScreen.cs:1227-1229` |
| BAG panel | (24, 140, 366, 762) | `PanelQuiet` | `:204, :1259` |
| ITEM panel | (406, 140, 384, 890) | **`Panel` (ornate, the one Primary)** | `:253, :1501` |
| ACTION panel ("WHAT TO DO WITH IT") | (806, 140, 560, 890) | `PanelQuiet` | `:254, :1502` |
| WALLET panel ("YOUR MATERIALS" + "YOUR CHARTS" + "THE FOUR TABS") | (1382, 140, 514, 890) | `PanelQuiet` | `:255, :2142` |
| Item card inside ITEM | (ContentLeft, TitleTop-6, 384-2*pad, 790) | — | `:258` |
| Tab strip | 4 x (Action.Width/4 - 4) x 46 at Action.Y | flat fills, gold underline on active | `:263, :287-292, :1534-1548` |
| Feedback strip | (ContentLeft(Action), Action.Bottom-100, w, 60) | fill + 5 px colour bar | `:1636-1639` |
| Guide banner (Game1 chrome, true canvas) | (470, 1080-h-26, 980, 58+22n) | fill + gold bar | `Game1.cs:3142-3148` |
| Currency pills (Game1 chrome, true canvas) | right-aligned from x 1826, y 16: Scrap, Dust, Gleam | `UiKit.Pill` | `Game1.cs:4860-4865` |

Note `PanelQuiet` is the SAME ornate art tinted dark bronze (`UiKit.cs:567-570`), so in the screenshot
all four columns still read as four ornate frames; only the ITEM column is brighter gold.

### 1.3 What each tab does (all Core-backed)

| Tab | Button(s) | Preview | Risk | Payment | Core |
|---|---|---|---|---|---|
| UPGRADE | UPGRADE +1 LEVEL; GREATER UPGRADE +5 — NEVER SLIPS | before -> after on the ITEM card (level, power, each affix, one decimal) | `UPGRADE n OF 15` bar; SAFE for 5 rungs then `x% SUCCESS. A SLIP DROPS ONE LEVEL AND ONE STEP.` | Scrap + Gleam, or a REFINE CHART; Crystal + Gleam for Greater | `Forge.Refine/RefineFailChance/AtRefineCap/GreaterRefine/TryRefine` (`Forge.cs:246-310`) |
| RE-ROLL | RE-ROLL THE ENCHANT | current enchant name + blurb + build verdict (WORKS WITH YOUR BUILD / NEEDS X IN YOUR BUILD) | "never the same one again" prose; no candidate list | Core (Rare/Epic) or Crystal (Legendary), or a REFORGE CHART | `Reforge.ReforgeEnchant/EnchantMaterial/CanPay/PayWith` (`Reforge.cs:84-161`) |
| SOCKET | (no button — click a gem row in the bag, which swaps to YOUR GEMS) | socket boxes n OF m; price line; FIRST GEM IS FREE | SET question warns "CANNOT COME BACK OUT"; CRUSH question | Essence | `GemCraft.SocketCount/SocketCost/Socket/Crush` |
| SALVAGE | SELL FOR n GLEAM; SALVAGE FOR n TIER | gain lines under each button | in-place question, KEEP first, worn variant never suppressible | — | `Forge.Dismantle`, `MaterialTiers.ForRarity`, `ItemInstance.SellValue` |
| (bag foot) | MERGE THREES INTO BETTER; SALVAGE ALL THE JUNK | — | junk question quotes the real payout incl. chart doubling | SALVAGE CHART doubles | `Forge.Merge`, `MergeRecipe`, `JunkOf` (`ForgeScreen.cs:773-863`) |

---

## 2. Does the screen answer "What will happen if I modify this item?" today

**Partly, and only for UPGRADE.** For UPGRADE the answer exists (level, power and every affix before ->
after, plus the slip chance) but it is split: the numbers live on the ITEM card at x 406-790 and the
button, cost and risk live on the ACTION panel at x 806-1366, so the eye must travel ~400 px and back
to connect "9" with "UPGRADE +1 LEVEL". For RE-ROLL the answer is "something random" — the tab shows the
current enchant but never the candidate pool, so the player cannot judge the gamble (`ForgeScreen.cs:
1708-1747`). For SOCKET there is no before -> after at all (`:1760-1857`); the gem's grant is in the bag
row, the item's power is on the card, and the sum is never shown. For SALVAGE the payout is shown on the
button itself, which is good. For MERGE (bag foot) nothing says what the three items will become even
though `MergeRecipe.TypeOf/ElementOf` decide it deterministically.

---

## 3. Findings

### P0 — readability and hierarchy

**P0-1 · Text is physically illegible at 720p because of inset x downscale.**
Evidence: table in 1.1; `720/forge.png` — `for merging — not the fight's Source`, `pays for upgrades`,
`YOU HOLD 12,600 SCRAP · 131.9M GLEAM`, the YOUR CHARTS paragraph and the THE FOUR TABS legend are 4-5 px
strokes. Two causes stack: `Game1.OverlayScale` (`Game1.cs:5323`) takes 10.4 % off every rung on every
menu screen before the presentation downscale, and Secondary (16) is used for 23 of the screen's ~40
text lines (`ForgeScreen.cs:1229, 1280, 1380, 1431, 1566, 1580-1581, 1652, 1666, 1676, 1713, 1726,
1734, 1746, 1765, 1772, 1807, 1855, 1972, 2007, 2202-2205, 2214, 2218, 2233, 2250, 2288, 2309`) —
including full sentences. The brief's rule (§6) is that Caption never carries a sentence and Secondary is
for metadata; here Secondary carries the whole manual.
Fix direction: (a) stop insetting the Forge — lay it out against the rail like the fight screen; (b) with
the global UI-scale/ladder work, promote every explanatory line to Body and every price/hold line to Body;
(c) delete the lines that should not exist (P0-3, P0-4) rather than shrinking anything.

**P0-2 · Four equal frames flatten hierarchy; the ornate frame is on the wrong column.**
Evidence: `forge.png` — four ornate-cornered columns of near-equal width (366/384/560/514) and identical
height; `PanelQuiet` is the same filigree tinted bronze (`UiKit.cs:567-570`), so the "quiet" panels are
not visually quiet at 1080p. The ONLY gold frame is the ITEM column (`ForgeScreen.cs:1501`), but the
screen's primary action lives in the ACTION column, which is quiet. The eye lands first on the item art
(the largest bright object), second on the gold `THE FORGE`/panel titles, and only third on the button.
Brief §9/§84: the focal interactive surface (operation + before -> after + [APPLY]) is the one that may be
ornate.

**P0-3 · The screen explains itself three times, permanently.**
- Header sentence `PICK AN ITEM IN YOUR BAG, THEN CHOOSE A TAB — UPGRADE, RE-ROLL, SOCKET OR SALVAGE`
  (`ForgeScreen.cs:1229`, Secondary, permanent).
- `THE FOUR TABS` legend with four lines (`:2237-2252`, permanent, in the wallet).
- The Forge tour card "FOUR JOBS" says the same four lines (`Onboarding.cs:311-313`).
- Plus `YOUR CHARTS` explanatory paragraph (`:2212-2214`) and the empty-state line `NONE RIGHT NOW —
  WAVES DROP ONE NOW AND THEN.` (`:2218`) — a 130 px block describing a mechanic the player does not
  currently have — exactly the case the brief names in §55.
- Plus each tab opens with a title AND a two-line paragraph restating it (`:1665-1666`, `:1711-1713`,
  `:1763-1765`, `:1970-1972`).
Design law 4 ("Do not explain the same system on every screen") is violated within one screen.

**P0-4 · The before -> after compare is not the centre of the screen.**
Evidence: `DrawTransition`/affix arrows are on the ITEM card (`ForgeScreen.cs:1585-1615`) at Body size
with a ~10 px triangle; the button, cost and risk are on a different panel (`:1662-1701`). At 720p the
arrows are ~6 px and the after-values are Met green on black at 6 px cap height (`720/forge.png`,
crop `card_stats`). The most important interaction on the screen (§56) is the smallest thing on it.

**P0-5 · Secondary looks disabled.** `Slate` (0x8A96A8) is used for metadata (`:1566, :1581, :1666`
etc.) and `Dim` (0x3A3A44) for empty states (`:1485, :1507-1509, :1721, :1772, :2000`), while disabled
buttons draw their label at 0x7C7688 (`UiKit.cs:892`). `MERGE THREES INTO BETTER` (disabled, greyed
frame) and the Slate hint lines are the same visual weight in `720/forge.png`. The empty-bag copy
`GO AND HUNT — BOSSES DROP CHESTS,` is drawn in Dim — an empty state that looks switched off (§7, §82).

**P0-6 · Contextual teaching violation on this screen.** The bottom guide banner reads
`YOU HAVE GLEAM TO SPEND — Every wave pays Gleam. Press V for STATS and train…` (`forge.png` y 975-1050;
`Tutorial.cs:238, 254-256`; drawn by `Game1.DrawGuideBanner` on every screen `Game1.cs:3109-3114,
4131`). It teaches STATS over the FORGE (§11, §12) and occupies the 980x80 band the Forge's own
feedback should own.

### P1 — layout and UX

**P1-1 · Wasted vertical space in every column.** In `forge.png`: ITEM content ends ~y 640 and the panel
runs to 920 (COPY ITEM CODE floats alone at the foot, `ForgeScreen.cs:1618`); UPGRADE tab content ends
~y 660 of 920; WALLET content ends ~y 720; BAG has 8 rows in a 14-row list (`BagRows` = (762-152-92)/40
= 12, `:229-230`) and two 52 px buttons pinned to the foot. Roughly 30 % of each column is empty while
the text is too small to read (§28 spirit, §102).

**P1-2 · The bag silently changes meaning under one tab.** Under SOCKET the left panel becomes
`YOUR GEMS` and gear disappears (`:357, :1263-1265`); a four-line help block explains how to get gear
back (`:1422-1431`). This is a mode switch disguised as a list, and it is why the SOCKET tab needs its
own manual. A filter chip row (ALL · GEAR · GEMS) on the inventory would make the state visible and
remove the help text.

**P1-3 · Two co-equal primary buttons on UPGRADE.** `UPGRADE +1 LEVEL` (72 px) and `GREATER UPGRADE +5
LEVELS — NEVER SLIPS` (60 px) are both ornate `UiKit.Button`s (`:1682-1697`). §84: one primary decision.
Greater upgrade is a payment option (Crystal for certainty), not a second verb.

**P1-4 · RE-ROLL is not RNG-honest (§58).** The tab never lists the candidate enchants although
`Enchantments.PoolFor(slot)` is public (`Enchantments.cs:254-260`) and `Reforge.Roll` draws from exactly
that pool minus the current kind (`Reforge.cs:107-112`). It never states what is LOCKED (level, stats,
gems, prefix — true by construction: `Reforge.cs:101`, product is `item with { EnchantOverride }`).

**P1-5 · SOCKET has no before -> after.** `GemCraft.Socket(host, gem).Product` is pure and
`Hunter.PowerContribution` reads gems (`HunterProgression.cs:283-293, 347-348`), so "POWER 251 -> 263 ·
+18% DAMAGE" is one call away; today the player reads the gem grant in the bag row and guesses.

**P1-6 · MERGE gives no preview.** `MERGE THREES INTO BETTER` (`:1435-1437`) merges everything mergeable
in one press with no statement of what will be produced, even though `MergeRecipe.TypeOf/ElementOf` and
the rarity/level rule (`Forge.cs:130-208`) are deterministic. The button label is also jargon-shaped
("threes into better"); the disabled reason is not shown (§83 spirit: a locked action explains why).

**P1-7 · Wallet duplicates chrome.** Gleam and Scrap are already pills at top-right (`Game1.cs:4856-
4865`) and appear again as the first two wallet rows 100 px below (`:2187-2188`); the Scrap pill's hover
already lists all four tiers (`Game1.cs:4871-4877`). The brief (§54) wants ONE compact shared strip.

**P1-8 · No item protection (§59).** `ItemInstance` (`LootSystem.cs:38-138`) has no lock/favourite
field; there is no `Locked`/`Protected` anywhere in Core. Existing partial protections: worn gear is
excluded from junk salvage and auto-merge (`ForgeScreen.cs:796-797, 839`) and always asks before
sell/salvage (`:879, :907`); gemmed items are excluded from auto-merge (`:797`). A NEW model field +
save field is needed before the UI can offer a lock.

**P1-9 · Tab labels below the control rung.** Tabs draw at `UiTypography.Body` (19) (`:1543-1544`);
§6 and the ladder say "a thing you click" is NavigationLabel (21).

**P1-10 · Two panel titles inside one panel.** `YOUR MATERIALS` and `YOUR CHARTS` are both PanelTitle
(26) inside the wallet (`:2144, :2212`), so the wallet has two heads; `THE FOUR TABS` adds a third at
Body. One panel, one title.

**P1-11 · Element line is a negative definition.** `SET PIECE · SHADOW / for merging — not the fight's
Source` (`:1580-1581`) tells the player what the element is NOT, in a 4 px line. The GEAR screen's set
ladder (§64) is the place the set matters; here one chip `SHADOW · MERGE ELEMENT` with the glyph suffices.

**P1-12 · Feedback strip is far from the compare.** Placed at `ActionPanel.Bottom-100` (`:1636`); with the
tab content ending at y ~660 there is a ~260 px gap between the button and the message that reports it
(the code comment at `:1630-1631` describes fixing this exact problem once already).

### P2 — polish

- P2-1 · `ItemNames` still carries `CreatureCore => "CORE"` and `Material => "MATERIAL"` (`:63-64`);
  neither type can reach a bag (`MergeRecipe.cs:581-583, 601-603`). "CORE" as an item word collides with
  CORE the material. Delete the two rows.
- P2-2 · Dead helpers: `Pct` (`:867`), `DrawWrapped` (`:2368`), `SourceTint` (`:3062`) have no callers.
- P2-3 · `IneligibleReason`/`CheckEligible`/`Explain` are degenerate (`Forge.cs:11-20, 91-99`) and still
  called at `:877, :905, :2790`; `Explain` returns "" so a refusal would be silent — remove the seam.
- P2-4 · The scroll counter `3-8 / 8` sits under the bag title at Secondary (`:1278-1280`) — at 720p it
  is 4 px; a scrollbar already exists (`:1448-1456`), so the counter can go.
- P2-5 · `COPY ITEM CODE` is an ornate button (`:1618-1619`) for a tertiary share utility; §49's
  hierarchy (primary / secondary / tertiary-overflow) applies.
- P2-6 · The button label `MERGE THREES INTO BETTER` and `SALVAGE ALL THE JUNK` disable with no reason
  text; `JunkOf` = non-worn Common/Uncommon (`:838-839`) — say `SALVAGE COMMON + UNCOMMON (n)`.
- P2-7 · `DrawTabStrip` fills use hardcoded colours (`:1541`) rather than UiKit tokens; fine today, but
  the V2 tab component should be shared with any other tabbed screen.

---

## 4. Keep — what is good and must survive the pass

1. **Every number is Core-computed and the UI cannot disagree with the payment.** `Reforge.CanPay` /
   `PayWith` drive the enable check, the price line and the spend (`ForgeScreen.cs:1743-1745, 975-990`);
   the refine preview is `Forge.Refine` (`:1668`); the wallet "NEEDS n" marks use the same calls
   (`:2156-2181`). This is the brief's §92 done right.
2. **Before -> after with one-decimal precision** so growth never reads as a lie (`:1601-1606, 2386-2391`).
3. **Risk stated before the click**: `UPGRADE n OF 15`, the bar, `SAFE — THE FIRST 5 NEVER FAIL` /
   `x% SUCCESS. A SLIP DROPS ONE LEVEL AND ONE STEP.` (`:1671-1676`), and the button label carries the
   odds (`:1683-1685`). Keep the words; grow the type.
4. **"Who pays" lines**: `FREE — YOU HOLD 2 REFINE CHARTS / ONE IS USED UP. WITHOUT A CHART IT COSTS…`
   and `YOU HOLD … — NOT ENOUGH CORE` in Ember (`:2281-2310`).
5. **Confirmation severity is proportionate (§85)**: routine sell/salvage asks once with a real "don't ask
   again" that commits with the confirming click (`:2056-2078`); worn gear ALWAYS asks and has no box
   (`:2046-2053`); CRUSH always asks (`:1887-1896`); SET asks because it is one-way (`:577-579`);
   KEEP sits first (`:1927, 2069`); the destructive verb is named, never "CONFIRM".
6. **In-place questions, not modals** (`:1220-1222, 2105-2132`); switching tab/item withdraws.
7. **Gems return before an item dies** (`:895-900`) and the question says so (`:2040-2044`).
8. **Bag rows: rarity bar + name in rarity ink + LEVEL/WORN right column, pixel-measured truncation**
   (`:1385-1396`), rarest-first sort (`:327-331`), wheel + scrollbar (`:743-749, 1448-1456`).
9. **Item art path** (`ItemArt`, `DrawItemIcon`, rarity-tinted frames, gem medallions `:2972-3059`).
10. **Cross-screen routes** from GEAR's item menu: `RequestUpgrade/RequestReroll/RequestSalvage`
    (`:469-484`; `Game1.cs:2630-2632`) arrive focused on the item with the right tab open and a
    `— READY.` flash (`:464`). This is the shared interaction grammar §15 asks for.
11. **The reveal** (drawn as host chrome, `:2406-2939`) — pointer-hold, per-item SELL/SALVAGE, KEEP
    first, cascade cap + mandatory summary. Out of this screen's layout scope but must not regress.
12. **Deterministic posing hooks**: `DevFocus`, `RH_SHOT_TAB`, `RH_SHOT_ASK`, `RH_SHOT_CHARTS`
    (`:377-447`).

---

## 5. Legacy vocabulary hits (Form / Weave / WovenAbility / Aptitude / Source x Form / stale names)

Player-facing on the Forge: **none.** Every label the player reads is current vocabulary (UPGRADE,
RE-ROLL, SOCKET, SALVAGE, ENCHANT, GEM, SCRAP/ESSENCE/CORE/CRYSTAL, CHART, STYLE-based needs such as
`NEEDS VOLLEY IN YOUR BUILD`, `Enchantments.cs:189-201`).

Code/comment-level hits that should be cleaned so the next reader does not reintroduce the model:
- `src/IdleXIdle.Game/ForgeScreen.cs:959` — doc comment: "the ENCHANTMENT — the Form-combo".
- `src/IdleXIdle.Core/Forge/Reforge.cs:22` — remarks: "the enchantment is the Form-combo".
- `src/IdleXIdle.Core/Economy/Enchantments.cs:172, 221-223, 296, 306` — comments: "The Form it needs",
  "Form-combo enchantments", "the Form combos below".
- `src/IdleXIdle.Game/Game1.cs:1950-1951` — forge fixture comment: "asks about something other than a
  Form… a Form combo"; `:1523` lootforge comment "no longer only asks about Forms".
- `Onboarding.cs:296-318` Forge tour copy is clean. `Tutorial.cs:242, 271-273` step is named
  `WeaveBuild` and its title is `THE BUILD IS THE GAME` (not this screen's copy, but the enum name is
  stale).
- Stale item-type words still in the screen's table: `ItemBaseType.CreatureCore => "CORE"`,
  `ItemBaseType.Material => "MATERIAL"` (`ForgeScreen.cs:63-64`) — creature-era types no live bag can hold.
- The shared UX standard `assets/art/idlexidle_ux_screen_guide_standard.md:68` still specifies the
  Build fixture as "fixed Source + Form + Vow setup" (the Forge line at `:69` is fine).

---

## 6. Data honesty — what the brief wants displayed vs. what Core already computes

| Wanted display (brief) | Status | Core member |
|---|---|---|
| UPGRADE before -> after: item level, power, every stat (§56) | **EXISTS** | `Forge.Refine(item, tuning).Product` (`Forge.cs:263-275`); `ItemAffixes.Of(product)`; `Hunter.PowerContribution(product)` (`HunterProgression.cs:283`) |
| UPGRADE risk: can it be worse, by how much (§57/58) | **EXISTS** | `RefineResult.FailChance`, `Forge.RefineFailChance` (`Forge.cs:246-253`); slip = -1 level -1 rung (`TryRefine`, `:281-293`) |
| UPGRADE cost + what you hold + chart | **EXISTS** | `RefineResult.Scrap/Gold`, `Hunter.MaterialOf`, `Hunter.Gleam`, `Hunter.CharterCount(Charter.Refine)` |
| GREATER UPGRADE steps actually bought at the cap | **EXISTS** | `RefineResult.Steps` (`Forge.cs:299-310`) |
| RE-ROLL: affected affix (the enchant), current value | **EXISTS** | `Enchantments.Of(item)` -> `Name`, `Blurb`, `Needs.Label` |
| RE-ROLL: potential range (candidate enchants) | **EXISTS** | `Enchantments.PoolFor(Gear.SlotFor(item.BaseType))` minus current kind (`Enchantments.cs:254-260`, mirrors `Reforge.Roll` `Reforge.cs:107-112`); magnitude per candidate via `Enchantments.MagnitudeFor(kind, rarity)` |
| RE-ROLL: locked affixes (level, stats, gems, prefix unchanged) | **EXISTS by construction** | product is `item with { EnchantOverride = picked }` (`Reforge.cs:101`); prefix immutable since trait re-roll removal (`Reforge.cs:28-30`) |
| RE-ROLL: "can the result be worse" | **NEEDS a rule** — no ranking of enchants exists; the honest display is the unordered candidate list plus the build verdict per candidate (`EnchantNeed.MetBy`, `Enchantments.cs:134-143`), never a "better/worse" verdict |
| SOCKET: before -> after (power, the added stat) | **EXISTS** | `GemCraft.Socket(host, gem).Product` (pure, `GemCraft.cs:143-155`) -> `Hunter.PowerContribution`; gem line `GemCraft.Describe` (`:94-98`) |
| SOCKET: cost incl. first-gem-free | **EXISTS** | `GemCraft.SocketCost(rarity, freeSocketUsed)` (`:120-125`) |
| SALVAGE / SELL payout and tier | **EXISTS** | `Forge.Dismantle`, `MaterialTiers.ForRarity/Name`, `ItemInstance.SellValue`; chart doubling `Charter.Salvage` |
| MERGE preview: what three items become | **EXISTS (type, rarity, element, level, class)** | `MergeRecipe.TypeOf/ElementOf/IsHybrid` (`MergeRecipe.cs:547-636`), rarity+1, `ItemLevel = max` (`Forge.cs:196-202`), class majority (`:119-128`). **Random by design:** the prefix (`GearTraits.RollPrefix`, `:181`) — say "a new random prefix", never preview one |
| Item protection / lock (§59) | **NEEDS new model** — no field on `ItemInstance` (`LootSystem.cs:38-138`), nothing in `SaveGame`; needs `Locked` + persistence + respect in `JunkOf`, `AutoMergeAll`, `Sell`, `Dismantle`, reveal buttons |
| Chart meaning on hover | **EXISTS** | `Charters.Blurb`, `Charters.Plain` (`Charters.cs:230-262`) |
| Disabled-reason for MERGE / SALVAGE JUNK | **EXISTS** | `anyTrio` predicate (`ForgeScreen.cs:1402-1404`), `JunkOf(hunter).Count`; `Forge.Merge` rejection strings (`Forge.cs:135-159`) |
| Diagnosis / offline summary / unlock progress | Not this screen's question — not applicable |

---

## 7. Proposed layout

Coordinates are **true canvas 1920x1080** with the nav rail at x 0-180 — the Forge should stop drawing
through `OverlayScale` (P0-1). If the inset must stay for this checkpoint, divide x by 0.8958 after
subtracting 180 to get the authoring-space equivalents; the proportions are what matter.

```
y   0- 110  HEADER (chrome): THE FORGE (ScreenTitle) centred; currency pills top-right as today.
            REMOVED: the hint sentence (ForgeScreen.cs:1229).
y 118- 178  MATERIALS STRIP (quiet, x 200-1900, 60 tall): five chips [icon 40 | name Secondary |
            value Headline] for GLEAM, SCRAP, ESSENCE, CORE, CRYSTAL, each ~260 wide; under the
            value a one-word mark "NEEDS 12" / "GIVES +32" in Met/Ember when the open tab touches it
            (the existing marks dictionary, :2148-2183). Held charts render as chips at the right end
            ("REFINE CHART x2", Met) with Charters.Blurb on hover. REMOVED: YOUR CHARTS paragraph,
            NONE RIGHT NOW line, THE FOUR TABS legend, MatUse captions (become hover text).
y 190-1040  THREE COLUMNS, 20 px gutters:
  A INVENTORY  x  200- 640 (440 w)  quiet.   Title "BAG" PanelTitle; filter chips ALL·GEAR·GEMS
                                             (Caption-as-badge) at y 246; rows 48 px from y 300,
                                             14 visible (to y 972): 6 px rarity bar, 36 px icon,
                                             name (Body), right column LEVEL n / WORN / [lock glyph];
                                             scrollbar; foot y 980-1032: two QUIET text buttons
                                             "MERGE ALL SETS OF 3 (n)" and "SALVAGE COMMON+UNCOMMON (n)"
                                             with the disabled reason in the label. The in-place junk
                                             question stays here. REMOVED: scroll counter, gem-mode
                                             help block (:1422-1431).
  B ITEM       x  660-1120 (460 w)  secondary (thin bronze). Inspector order (§14):
                                             LEGENDARY · WEAPON · WORN  (Secondary)
                                             SHADOW BOW                (Headline, rarity ink)
                                             [element chip: glyph + "SHADOW · MERGE ELEMENT"]
                                             picture 160x160
                                             WHAT IT DOES: enchant name (Headline) + blurb (Body) +
                                               build verdict line (MOVED here from RE-ROLL tab)
                                             STATS: 4 affix rows, current values only (Body)
                                             SOCKETS: n boxes 56 px, filled/empty, click = crush
                                               (MOVED here from the SOCKET tab)
                                             foot: "COPY ITEM CODE" as a quiet text link (P2-5).
                                             REMOVED from this column: before->after arrows.
  C FORGE      x 1140-1900 (760 w)  PRIMARY — the only ornate frame on the screen.
                                             Tab strip y 240-292 (NavigationLabel 21): UPGRADE ·
                                             RE-ROLL · SOCKET · SALVAGE (kept, all live).
                                             y 310-360 one-line intent (Body) — no paragraph.
                                             y 380-640 BEFORE -> AFTER TABLE (the centre of the
                                             screen): rows ITEM LEVEL 8 -> 9, POWER 251 -> 254, then
                                             each affix, label Body, values Headline, after-value
                                             Met, arrow 16 px. RE-ROLL variant: "NOW: FERVOUR" ->
                                             "ONE OF: SPLINTER · VENOM · HARVEST · REVERB" with the
                                             build verdict per candidate; a locked line "LEVEL,
                                             STATS, GEMS, NAME DO NOT CHANGE". SOCKET variant: the
                                             selected gem's grant and POWER 251 -> 263. SALVAGE
                                             variant: "+82 GLEAM" or "+32 CORE · gems return".
                                             y 660-700 RISK line (Body): SAFE / x% SUCCESS —
                                             A SLIP DROPS ONE LEVEL / "random among these" /
                                             "cannot be undone".
                                             y 720-780 COST line with icons + YOU HOLD (Body) and
                                             the chart line when one pays.
                                             y 800-880 ONE PRIMARY BUTTON, 80 tall, full width.
                                             y 896-944 secondary option row (quiet button):
                                             "GREATER: +5 FOR 1 CRYSTAL, NEVER SLIPS" (UPGRADE),
                                             or nothing.
                                             y 960-1032 FEEDBACK strip directly under the button.
```

Primary action per tab: UPGRADE +1 LEVEL / RE-ROLL THE ENCHANT / SET [gem name] (the button appears
once a gem row is selected in the GEMS filter; the SET question stays) / SALVAGE FOR n TIER with SELL as
the secondary row. The inspector is column B; column C is the operation surface the brief §54 asks for.

Removed / moved / renamed summary:
- REMOVED: header hint; YOUR CHARTS paragraph + empty line; THE FOUR TABS legend; per-tab paragraphs;
  gem-mode help block; scroll counter; wallet panel as a column; "for merging — not the fight's Source".
- MOVED: before -> after from ITEM card to FORGE column; enchant identity + verdict from RE-ROLL tab to
  ITEM inspector; socket boxes from SOCKET tab to ITEM inspector; gems from a tab-driven list swap to a
  filter chip; materials from a column to a strip; feedback strip to directly under the button.
- RENAMED: "WHAT TO DO WITH IT" -> "FORGE"; "YOUR BAG" -> "BAG"; "SET PIECE · SHADOW" -> "SHADOW ·
  MERGE ELEMENT"; "MERGE THREES INTO BETTER" -> "MERGE ALL SETS OF 3 (n)"; "SALVAGE ALL THE JUNK" ->
  "SALVAGE COMMON + UNCOMMON (n)"; "GREATER UPGRADE +5 LEVELS — NEVER SLIPS" -> secondary option row.
- ADDED (all Core-backed, see §6): RE-ROLL candidate list + locked line; SOCKET power before -> after;
  MERGE result preview line under the merge button ("3 RARE WEAPONS -> 1 EPIC WEAPON, SHADOW, LEVEL 8").
- NEEDS NEW MODEL before it can be added: item lock (§59).

Empty / locked states: bag empty -> "NO GEAR IN THE BAG — bosses drop chests, chests drop gear.
[GO TO HUNT]" in Slate (not Dim) with a real button; sub-Rare item on RE-ROLL/SOCKET -> "COMMON AND
UNCOMMON ITEMS HAVE NO ENCHANT / NO SOCKETS — MERGE THREE TO REACH RARE" (the rule is
`Enchantments.MinimumRarity`, `GemCraft.SocketCount`); at cap -> "FULLY UPGRADED 15/15" in Gold with
the button removed, not greyed.

Framing hierarchy after the pass: ONE ornate frame (column C), one thin bronze (column B), quiet fills
for A and the strip. Gold only on: active tab underline, the FORGE frame, after-values that are earned,
selected bag row, held-chart chips.

---

## 8. Fixtures

Existing deterministic surface:
- `RH_SHOT_MODE=forge` (`Game1.cs:1928-1970`): opens the Forge, seeds 9 `dev0..dev8` items cycling
  Weapon/Charm/**Material**/Focus x 5 rarities (the Material items are invisible — `Bag()` filters
  `Gear.IsWearable`, `ForgeScreen.cs:328`), plus `dev_hero` (Legendary Shadow weapon, iL 8, Fervour,
  Upgrades 0), focuses it, stocks 131.9M Gleam / 12,600 Scrap / 3,400 Essence / 820 Core / 400 Crystal
  / 77,400 Dust. This is the baseline `forge.png`.
- `RH_SHOT_MODE=reforge` — same fixture (`:1928`); the tab is chosen by `RH_SHOT_TAB`.
- `RH_SHOT_MODE=gemtour` — forge fixture + two minted gems + the first-gem lesson owed (`:1931-1935`).
- `RH_SHOT_MODE=lootforge` (`:1504-1557`) — a different bag (SIPHON charm, Fervour weapon, two gems,
  five chests, opens one, `RH_SHOT_T` poses the reveal).
- `RH_SHOT_MODE=tour` with `RH_SHOT_TAB=Forge RH_SHOT_STEP=n` poses the four Forge tour cards
  (`Game1.cs:472-487`; `capture.sh:34-40`).
- In-screen posing via `ForgeScreen.ApplyDevPose` (`:392-447`): `RH_SHOT_TAB=upgrade|reroll|socket|
  breakdown`, `RH_SHOT_ASK=sell,salvage,junk,gems,crush,revealsell,revealsold,haul,say`,
  `RH_SHOT_CHARTS=n` (adds n Refine/Reforge/Salvage charts — never Merge).
- `capture.sh` modes list (`tools/asset-pipeline/capture.sh:11-13`) names `forge lootforge reforge`;
  `gemtour` is documented at `:41-45`.

Gaps against brief §95 "FORGE (item with all operation states)":
1. No way to pose `Upgrades > 0` — the slip-chance branch (`x% SUCCESS`, Ember note) and the at-cap
   state (`FULLY UPGRADED +15`) are never photographed. Needs a fixture item with `Upgrades = 7` and one
   with `Upgrades = 15` (`ItemInstance.Upgrades` is init-settable, `LootSystem.cs:61`).
2. No worn item in the bag — the `WORN` column tag, the worn subtitle, and the always-ask worn salvage
   variant are unposed. Needs `_hunter.Equip(...)` of a fixture item in the `forge` branch.
3. No Rare/Epic host with a set gem in the default `forge` fixture (only via `RH_SHOT_ASK=gems`, which
   is a separate capture); no sub-Rare item focused (the "NONE — BELOW RARE" and "NO SOCKETS" states).
4. No held charts in the baseline (`RH_SHOT_CHARTS` exists but Merge charts cannot be added; the
   `PAYS THE NEXT UPGRADE` tag and the FREE price line are unposed by default).
5. No empty-bag state (`NO GEAR IN THE BAG`) — needs a `forgeempty` mode or `RH_SHOT_BAG=empty`.
6. The Material-type seeds are dead weight; replace with Helm/Ring/Boots so the bag shows the eight
   wearable slots and a class-locked piece.
7. The lootforge fixture comment still speaks of "Forms" (`Game1.cs:1523`).
8. A composite `forgeall` fixture is the cheapest way to satisfy §95: bag of ~14 wearables including
   one worn, one gemmed Epic with an open socket, one Rare at rung 7, one Legendary at 15/15, one
   Uncommon, two loose gems, one of each chart, all four tiers stocked — and `RH_SHOT_TAB` still picks
   the tab. Capture at 1920x1080 and 1280x720 per tab (§94/§96).

---

## 9. Open questions for the owner

1. Drop the overlay inset for menu screens (P0-1) in this pass, or defer to the global UI-scale work?
   The Forge alone cannot fix it; it is a Game1 decision affecting every menu screen.
2. Item lock (§59): add `ItemInstance.Locked` + save field now, or defer? Without it the salvage/merge
   bulk verbs stay "worn and gemmed only" protected, which is what ships today.
3. Should the Gleam/Scrap pills remain as global chrome when the Forge shows a full materials strip 60
   px below them (P1-7)? The brief wants one strip; the pills are the shared HUNT-relevant subset.
