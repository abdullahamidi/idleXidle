# UX V2 audit — THE VAULT

Audited 2026-09-01 against `production/audit/ux-v2/BRIEF.md` (sections 0–15, 47–52, 82–102, 100) and the
runtime on `feat/hunter-cutout-rig` (post 2026-08-31 refactor). Read-only: no `.cs` was touched.

Code read in full: `src/IdleXIdle.Game/ChestScreen.cs` (the VAULT), `src/IdleXIdle.Core/Expeditions/Chest.cs`,
`ChestDossier.cs`, `GiftChests.cs`; the chest keep-filter in `Game1.cs` (258–276, 697–706, 1216–1219,
3524–3532, 3786–3798) and its editor on HUNT (`SoloExpeditionScreen.cs` 2655–2825); the openers and the
reveal in `ForgeScreen.cs` (1079–1200, 2518–2560, 2859–2939); the inset transform (`Game1.cs` 5304–5337);
the VAULT fixtures (`Game1.cs` 2012–2046) and `tools/asset-pipeline/capture.sh`.

Pixels read: `baseline/vault.png`, `baseline/vaultfirst.png`, `baseline/720/vault.png`, `baseline/720/vaultfirst.png`.

---

## 0. Does the screen answer its question today?

Brief §100 — VAULT: **"What rewards are waiting for me?"**

Partly. The screen does say *how many* and *of what grade* (the card row + the tally) and the Core already
knows everything truthfully sayable about each chest (`ChestDossier`). But the answer is delivered at the
smallest rung on the screen, behind a 32 px hover glyph with a 0.4 s delay, in a panel that occupies the top
third of the canvas and leaves 58 % of the screen to the swamp backdrop. At 1280×720 the two lines that
carry the actual promise ("EPIC OR BETTER", "WEAPON · FOCUS") are ~9.6 px tall and cannot be read without
leaning in. So: the *data* answers the question; the *layout* does not.

---

## 1. Measured geometry (the numbers every finding below rests on)

**The VAULT is drawn through the overlay inset.** Every menu screen is uniformly scaled by
`Game1.OverlayScale = (1920 − 180 − 20) / 1920 = 0.8958` and pushed right of the 180 px nav rail
(`Game1.cs:5322–5326`; the vault's hit-test inverts it at `ChestScreen.cs:294`). The rail and the HUNT screen are
not inset. So a UiTypography rung on the VAULT never reaches the canvas at its nominal size:

| Rung (authored) | On the 1080 canvas (×0.896) | Seen at 1280×720 (×0.597) |
|---|---|---|
| ScreenTitle 36 | 32.2 px | 21.5 px |
| PanelTitle 26 | 23.3 px | 15.5 px |
| Headline 24 ("TIER 6") | 21.5 px | 14.3 px |
| NavigationLabel 21 (buttons) | 18.8 px | 12.5 px |
| Body 19 (dossier tooltip, empty state) | 17.0 px | 11.3 px |
| Secondary 16 (floor line, slots line, tally) | 14.3 px | **9.6 px** |
| Caption 14 (HUNT filter row — not inset) | 14 px | 9.3 px |

The brief's §5 target for Body at 720p is ~24–26 px *effective*; the VAULT's most important line is at 9.6.

**Panel and card geometry** (`ChestScreen.cs:156–214`), authored → canvas → 720p:

- `GridPanel = (38, 150, 1842, h)`; with ≤ 6 stacks `h = CardTop 106 + CardH 210 + PanelCorner 40 = 356`
  (`GridPanelFor`, :167–173). Canvas: x 214–1864, y 134–453. **Below y = 453 nothing is drawn: 627 of 1080 px
  (58 %) is scrim + backdrop.** Confirmed in `vault.png` (panel bottom ≈ 447) and `720/vault.png` (≈ 298 of 720).
- Cards `270 × 210`, 6 columns, pitch 298 (`Cols`, `CardW`, `CardPitchX` :199–214) → 242×188 on canvas → 161×125 at 720p.
- Chest art `110 × 110` (:467) → 98 px → 66 px at 720p.
- Rarity pips `14 × 14` at 22 pitch (:476) → 12.5 px → 8.4 px.
- Peek glyph `32 × 32`, hit box 46 (:222–228) → 28.7 px → 19 px. Element glyph 32 (:234) likewise.
- Header buttons `56` tall: OPEN ALL 300, TRADER 200, PASTE A CODE 240 (:176–197), all the same
  `UiKit.Button` art (dark at rest → green on hover, grey when disabled; `UiKit.cs:873–912`).
- Dossier tooltip `470` wide, Body 19 lines, appears after `_qT ≥ 0.4 s` (:789–846).

---

## 2. What the eye does (screenshot observations)

**`vault.png` (1080):** first landing point is the three bright ornate buttons in the black header band, then the
row of six identical chest sprites (one texture, `chest_loot`, tinted toward the grade — :469–470). The grade
strip on each card top (5 px) and the pips read; "TIER n" reads; the two Secondary lines under the pips are
legible but small. The tally under the title ("1 LEGENDARY 3 EPIC 2 RARE 1 UNCOMMON 1 COMMON") is a 14 px
slate line the eye skips. Everything below the panel is decorative swamp — a room that is 42 % furniture.

**`720/vault.png`:** "EPIC OR BETTER" / "WEAPON · FOCUS" ≈ 9–10 px, anti-aliased to mush; the tally ≈ 9 px,
effectively unreadable; "TIER 6" ≈ 14 px is the only readable fact per card; buttons ≈ 12 px readable but
equal; pips 8 px (colour + count still discernible). The ×3 badge and "3 THE SAME" read because they are gold
on black, not because they are big.

**`vaultfirst.png` / `720/vaultfirst.png` (a new game's first visit):** one card in a six-wide row — 5/6 of the
panel interior is empty black; the panel still spans the full 1842 width. `OPEN ALL (1)` is drawn **disabled**
(grey art, `enabled: chests.Count > 1`, :421) — on the very first visit the biggest button on the screen says
"can't". The card's two lines ("A WELCOME GIFT", "ONE PLAIN BLADE") are ≈ 9 px at 720p. The only way to
discover that the card *is* the button is the tour (`Onboarding.cs:288–290`) or hovering, which fades in an
"OPEN" chip (:550–559) — a hover-only affordance.

---

## 3. Findings

### P0 — readability / hierarchy

**P0-1. The promise is the smallest text on the screen.** `ChestDossier.cs:48–56` calls `GuaranteedFloor` "the
single most valuable thing to show". On the card it is drawn at `UiTypography.Secondary` (16 → 9.6 px at 720p,
`ChestScreen.cs:522–524`), the same rung as the favoured-slot line (:525–526) and the header tally (:373).
Evidence: `720/vault.png`, both card lines ≈ 9–10 px. Fix: promote to Body/Headline in the card, and give the
full dossier a real inspector (§14) instead of a tooltip.

**P0-2. The overlay inset silently shaves 10.4 % off every rung on this screen** (`Game1.cs:5323`), so a design
that reads on paper at Body 19 ships at 17 px on 1080 and 11.3 px at 720p. This is global to all inset menu
screens but the VAULT suffers most because its content sits at the two lowest rungs. Any 720p acceptance pass on
this screen must measure the *canvas* size, not the authored constant.

**P0-3. 58 % of the canvas is empty backdrop while the cards are 242×188 px.** `GridPanelFor` sizes the
panel to the pile (:167–173) and never grows the cards; at one row the panel ends at canvas y 453. Brief §47:
"very large panel, very small chest cards, large empty areas" — measured true. At `vaultfirst` the waste is
also horizontal: one 242 px card in a 1650 px panel.

**P0-4. Three equal ornate buttons, no primary.** OPEN ALL, TRADER and PASTE A CODE share height, art and
rung (:176–197, :390–395, :421). Brief §49/§84 want OPEN ALL primary, TRADER secondary, PASTE tertiary.
In `vaultfirst.png` the largest button is the *disabled* one — a first-visit screen whose loudest element is a
grey "no".

**P0-5. The empty state is written in the disabled colour.** With zero chests the only content is two Body
lines in `Dim` (0x3A3A44) (:409–413) — the colour the same file uses for *unlit* pips (:478) and the
"ALREADY BOUGHT" dead label (:637). Brief §7 (secondary must not look disabled) and §50 (explicit "THE VAULT IS
EMPTY" + RETURN TO HUNT) both fail. There is no fixture for this state (see §7 below) so it has never been
photographed.

### P1 — layout / UX

**P1-1. The whole explanation lives behind a hover glyph with a 0.4 s delay.** `DrawDossierTip` (:789–846)
is the "retired detail column" reborn as a 470 px tooltip. Brief §14 asks for a shared right-side inspector;
§15 says hover = glance, click = select, inspector = explanation, and "avoid huge hover tooltips containing full
documentation". Also, the "?" hit box (46 px) sits inside the card's own click area, so a click that misses the
glyph by a few pixels *opens the chest* (:333–341) — inspect-vs-commit separated by 7 px of padding.

**P1-2. OPEN is hover-only.** The commit label fades in over the lid only while hovered (:550–559). Nothing at
rest says the card is clickable; the tour says it once. Brief §84 wants one obvious primary decision.
(Technical-preferences "no hover-only interactions" is stated for combat, but the same reasoning applies.)

**P1-3. OPEN ALL states no consequence.** Brief §51. `LandChest` (`ForgeScreen.cs:1190–1196`) sells every
wearable at or below `AutoSellFloor` for Gleam *silently* and keeps no count; the summary afterwards mentions
the filter only when *everything* was sold (`ForgeScreen.cs:2881`). TIRELESS FORGE auto-merge is reported
only after the fact (`:2925`). Before the click the VAULT could honestly say what the filter *is* and how many
of the waiting chests *can* produce items below it (see §6 Data honesty). Correctly, there is no confirmation
(§51/§85) — keep that.

**P1-4. Reward summary has no priority beyond rarity.** Brief §52. `DrawRevealSummary` orders rarest-first
(`ForgeScreen.cs:2905`), tallies by rarity (:2879–2882), caps at 10 shown (:2865), adds "+N MATERIALS" and
"AUTO-MERGE FUSED Nx". Nothing surfaces "a gem" (a distinct channel, `ChestReward.Gems`, `Chest.cs:89–92`),
a class-locked piece for *your* champion, or an upgrade over the worn piece. See §6 for what is computable.

**P1-5. Caption carries sentences on the HUNT chest-filter.** `SoloExpeditionScreen.cs:2739–2740` prints the
filter's value ("TIER 3 AND UP · HELM, BOOTS") at Caption 14, and :2809–2810 prints "OTHER CHESTS TURN INTO A
LITTLE SCRAP" at Caption — a sentence at the tag rung (brief §6). HUNT is not inset, so this is 14 px on 1080
and 9.3 px at 720p.

**P1-6. Trader card name shrinks below the type floor.** `ChestScreen.cs:613–614`:
`var px = UiTypography.Body; while (px > 13 && …) px--;` — a variable, so `tools/check_ui_type.py` cannot see
it, and it bottoms out at 13, under the Caption floor of 14 (`UiTypography.cs:173–182`). Brief §6: fix the
layout (wrap or widen the card), do not shrink.

**P1-7. Stack count is said twice.** The gold ×N badge (:486–491) *and* "N THE SAME" (:494) on a 270 px card.
One of them (the badge, made larger) is enough once the inspector exists; the gold on "3 THE SAME" is not
selection/earned/primary (§10).

**P1-8. Disabled primary on a one-chest vault.** `OPEN ALL` is `enabled: chests.Count > 1` (:421). With one
chest, hide it or relabel ("OPEN") rather than showing a grey primary; the card already opens the chest.

**P1-9. Scrolling is a text instruction.** "ROWS 1 / 3 · WHEEL SCROLLS" (:425–427) at Secondary, on a screen
with no scrollbar. Wheel scrolling is the only way to reach row 3 (`MaxScroll`, :279; wheel at :306–307).

### P2 — polish / vocabulary / structure

**P2-1. "MATERIALS" is the pre-split word for what is now SCRAP.** `ChestDossier.MaterialsLine` "12–18
materials." (`ChestDossier.cs:100`), the reveal's "+N MATERIALS" (`ForgeScreen.cs:1129, 1159, 2694, 2924`), and the
trader caption "YOU PAY IN MATERIALS" (`ChestScreen.cs:581`) — while the payout is literally
`hunter.AddMaterials(reward.Materials)` → `AddMaterial(Material.Scrap, …)` (`HunterProgression.cs:130`) and the
pill on every screen says SCRAP (`Game1.cs:4856–4861`). `Material.cs:9` records that "materials" was the old
undifferentiated count. Rename to SCRAP where the number *is* scrap; the trader's per-line prices already name
the material (:630).

**P2-2. Stale screen/class name.** The screen is THE VAULT in every player-facing string and the nav; the class
is `ChestScreen` and the host fields are `_chests` / `_showChests` (`Game1.cs:2686–2690, 4096`). Brief §101
lists stale screen names as legacy cleanup. Rename to `VaultScreen` / `_vault` / `_showVault`.

**P2-3. The VAULT's model is owned by the FORGE screen class.** `_forge.UnopenedChests`, `_forge.AddChest`,
`_forge.OpenOneChest`, `_forge.OpenEveryChest`, `_forge.AutoSellFloor`, `_forge.AutoMergeOnOpen`
(`ForgeScreen.cs:496–502, 558, 600, 1079–1090`). The brief (§90) wants screen classes to own their screen; a
chest pile + opener that lives in a *different* screen class is a responsibility smell and the reason the vault
cannot read the filter it needs for §51. (The comment at `ForgeScreen.cs:1075–1077` explains why it happened —
the reveal and the loot filter live there — which argues for a small `ChestVault` host-owned component, not
for the FORGE keeping it.)

**P2-4. Element glyph is colour + glyph but the word appears only on hover** (:502–511). Brief §8 asks for
colour + glyph + label where necessary. On a 32 px glyph at 19 px (720p) the six Source glyphs are hard to
tell apart; the inspector should print the word.

**P2-5. Tour card 3 describes chrome.** `Onboarding.cs:296–298` "THE HEADER BUTTONS — OPEN ALL opens every
chest at once. TRADER … PASTE A CODE …" will be wrong the moment the toolbar hierarchy changes; keep the two
content cards, rewrite the third around the primary action.

**P2-6. `ELEMENT` vs `SOURCE`.** The chest speaks of its `Element` (`Chest.cs:37`, `ChestDossier.ElementLine`),
which matches GEAR's item vocabulary; the brief speaks of Source colours. Not a legacy hit — flag for the
glossary pass so the two words are one.

**P2-7. `DISCIPLINE` on the A FRIEND'S BUILD card** (`ChestScreen.cs:735–736`) is the current player word for the
Style specialisation (`BuildScreen.cs:1587`, `WeaveScreen.cs:40`), consistent with BUILD — defer to the BUILD
audit; if BUILD renames to STYLE, this card follows.

---

## 4. Legacy vocabulary reaching the player (or the class names) — file:line

| Where | Text / identifier | Why it is legacy |
|---|---|---|
| `ChestScreen.cs:742` | `"NONE WOVEN"` (A FRIEND'S BUILD card, empty skills) | Weave vocabulary; the runtime has no Weave. |
| `ChestScreen.cs:748–750` | prints `$"{s.Source} {s.Form}"` for a v1 share code | Source × Form pair drawn as the skill's name when a pre-v2 code is pasted (`ShareCodes.cs:37–41` still decodes v1). |
| `ChestScreen.cs:730` (comment) | "the Nen identity is the headline" | Comment only, does not reach the player; stale framing. |
| `ChestScreen.cs` (class), `Game1.cs:2686–2690, 4096` | `ChestScreen`, `_chests`, `_showChests` | Screen is THE VAULT everywhere the player sees it. |
| `ChestDossier.cs:100`, `ForgeScreen.cs:1129, 1159, 2694, 2924`, `ChestScreen.cs:581` | "materials" / "+N MATERIALS" / "YOU PAY IN MATERIALS" | Pre-split currency word; the value is SCRAP (`HunterProgression.cs:130`). |
| `SoloExpeditionScreen.cs:2743` | "The rest turn into a little Scrap." | Correct vocabulary — listed as the *right* precedent for P2-1. |

No Form / Aptitude / WovenAbility identifiers exist in `ChestScreen.cs` beyond the two rows above.

---

## 5. What is GOOD and must be kept

- **Truth discipline.** Every card line is derived from `ChestTuning` via `ChestDossier` (`ChestDossier.cs:23–27`)
  and tested (`tests/unit/IdleXIdle.Core.Tests/Expeditions/chest_dossier_test.cs`: floor never broken, ranges hold,
  "no floor" says so, tilt silent at neutral, best-first order, tally, stacking). The screen "states facts, never
  outcomes" (`ChestScreen.cs:35–37`). Keep this as the model for every inspector line.
- **Stacks.** `ChestDossiers.Stacked` (`ChestDossier.cs:233–242`) groups identical chests; the card-back pile
  (:439–450) is a good visual. Keep the grouping; enlarge the badge.
- **Grade as count.** The 5-pip row (:473–479) makes rarity readable without colour (§8). Keep, enlarge.
- **Quiet frame.** The grid panel already wears `PanelQuiet` (:375–379) — the house rule the brief §9 wants.
  Modals (trader, share cards) keep the gold `Panel` (:578, :723), correctly.
- **Single safe click opens.** No confirmation on OPEN or OPEN ALL (§85/§99.5). Keep.
- **Best-first order and "chest is the button" model** were explicit playtest requests (:22–25).
- **Hover lift is a value step, not a hue** (:452–462), per the art bible.
- **Fitts padding** on the peek glyph (46 px hit for 32 px art, :224–228).
- **Screen title in the shared strip + gold rule** (:365–366) matches every other screen.
- **The reveal cascade** (`ForgeScreen.cs:1134–1178`: worst-first, best last, capped at 8, summary never skipped) is
  right; §52 is about *what the summary ranks*, not about replacing the ceremony.
- **HUNT-side keep-filter as a closed row with a popover** (`SoloExpeditionScreen.cs:2723–2744`): the
  setting-in-words on the closed row and "OTHER CHESTS TURN INTO A LITTLE SCRAP" are exactly the plain-language
  consequence line the brief asks for — only the rung is wrong.
- **Trader / share codes** live in the right room (:74–75).

---

## 6. Data honesty — what the brief wants shown vs what Core already computes

| Brief wants | Status | Source |
|---|---|---|
| Chest grade, tier, element, region, quantity | EXISTS | `Chest` record; `ChestDossier.Grade/Tier/Element/Region`; `ChestDossiers.Stacked` count |
| Guaranteed rarity floor ("EPIC or better" / "a gamble") | EXISTS | `ChestDossier.GuaranteedFloor`, `FloorLine`, `FloorShort` |
| Item count range, scrap range | EXISTS | `ChestDossier.MinItems/MaxItems/MinMaterials/MaxMaterials`, `ContentsLine`, `MaterialsLine` |
| Favoured gear slots ("drop constraints") | EXISTS | `ChestDossier.Favoured`, `RegionLine`, `RegionShort` (via `RegionDrops.For`) |
| Run-quality tilt | EXISTS | `ChestDossier.RunTilt`, `RunLine` (silent at neutral) |
| Gift contents (fixed) | EXISTS | `GiftChestDef.Lines/Items`, `ChestDossier.IsGift` |
| Tally by grade for the header | EXISTS | `ChestDossiers.Tally` |
| Empty-state acquisition facts | EXISTS but hand-written | `ChestTuning.DropChance = 0.20` / `Chests.DropChance` — the string "about one boss in five" at `ChestScreen.cs:410` is a literal; derive it. Sources: boss kills (`Game1.cs:3786`), one welcome gift (`GiftChests.NewGameChests`). The Warren pays Gleam/Dust/Scrap/Essence, never chests; the trader sells items, never chests — so "and through progression" would be dishonest today. |
| Keep-filter turning chests away (for the empty state) | EXISTS, not fed to the screen | `Game1._chestKeepMinTier/_chestKeepSlots` (258–276), `SoloExpeditionScreen.KeepMinTier/KeepSlots`; `Chests.PassesKeepFilter`, `FilterCompensation` |
| §51 "N low-tier items may be salvaged" (exact count before opening) | **NOT computable** | Contents are rolled at open (`Chests.Open`, `Chest.cs:305–355`) — a count would be invented. |
| §51 honest alternative: the filter floor + how many waiting chests *can* drop at/below it | EXISTS (needs plumbing) | `ForgeScreen.AutoSellFloor` ← `DustEffects.AutoSellAtOrBelow(_dust)` (`Game1.cs:3546`); per chest `ChestDossiers.For(c).GuaranteedFloor <= floor`. `Gear.IsWearable` excludes gems (`ForgeScreen.cs:1194`). |
| §51 post-open "N items were sold on sight" | **NEEDS a counter** | `LandChest` sells silently (`ForgeScreen.cs:1190–1196`); no count kept. Trivial new state, no telemetry. |
| Auto-merge consequence | EXISTS | `ForgeScreen.AutoMergeOnOpen` (before), `_revealMergedCount` (after, `:1158`) |
| §52 rarity tally / rarest first | EXISTS | `DrawRevealSummary` `:2879–2882, :2905` |
| §52 "new mechanic" — a gem | EXISTS (channel), not surfaced | `ChestReward.Gems` (`Chest.cs:89–92`); the reveal concatenates gems into items (`:1188`) and loses the distinction. `Onboarding.GemTourKey` exists for the first-gem lesson. |
| §52 "useful rare item" — class-locked for *your* champion | EXISTS | `ItemInstance.Class` vs `ForgeScreen.FavouredClass` (`:597`) |
| §52 "major upgrade" over the worn piece | Unverified in the reveal | A Forge-bag hover verdict is referenced at `Game1.cs:1667` ("UPGRADE +N PWR"); I did not find it in `DrawRevealCell`. Treat as "exists in GEAR/FORGE tooltip, needs reuse", not as new telemetry. |
| §52 "set completed" | **Not applicable to a chest open** | `ElementSets` (`Economy/ElementSets.cs`) counts *worn* pieces; a drop completes nothing until equipped. Do not show. |
| Trader stock, prices, affordability | EXISTS | `WanderingTrader.Stock/PriceOf/TryBuy`, `Hunter.MaterialOf` |

---

## 7. Fixtures

**Existing (`Game1.cs` RH_SHOT_MODE handlers; `tools/asset-pipeline/capture.sh`):**

- `vault` (`Game1.cs:2027–2046`): 8 chests → 6 stacks: Legendary t6 (RunTilt 1.35), Epic t13 ×3, Rare t20, Rare t27,
  Uncommon t34, Common t41; six regions, six Sources. Poses the stack pile + ×3 badge. Trader enabled because
  stock mints in Update (`:2680–2685`).
- `vaultfirst` (`:2014–2025`): exactly `GiftChests.NewGameChests()` — the welcome gift. `RH_SHOT_OPEN` opens it;
  `RH_SHOT_T` poses the reveal instant (`:1500–1502`).
- `trader` (`:~2000–2010`): `DevOpenTrader()` with 2 000 scrap / 400 essence / 20 core, deepest 18.
- `tour Vault N` (`capture.sh:31–37`): the three tour cards over the `vault` dressing (spotlights at
  `ChestScreen.Spotlights`, :248–271).
- `fightfilter` (`:1841–1845`): HUNT with the CHEST FILTER popover open, `minTier 3 + Helm + Boots`.
- `lootforge` + `RH_SHOT_T`: a single reveal burst; not a vault capture.

**Gaps against brief §95 "VAULT (tiers)" and the findings above:**

1. **Empty vault** — no mode poses 0 chests, so P0-5 has never been photographed. Needs `vaultempty`.
2. **Multi-row pile** (> 6 stacks) — nothing poses the scroll state / "ROWS x / y" (P1-9). Needs ~14 stacks.
3. **Gift + boss chests together** — `vault` and `vaultfirst` are disjoint; the gift-vs-rolled card contrast
   the tour describes (`Onboarding.cs:292–294`) is never in one frame.
4. **Filter consequence state** — a vault with `filter_common` owned (`DustEffects.AutoSellAtOrBelow`) and a
   keep-filter set, so the §51 consequence line has content. Also a chest with `Region = null` (no lean,
   `ChestDossier.RegionShort` → "no lean") and `Element = null` ("Plain — no element").
5. **OPEN ALL summary** — no mode poses `DrawRevealSummary` with ≥ 2 chests (the §52 surface). Needs
   `vaultopenall` = `vault` + `OpenEveryChest` + `RH_SHOT_T` past the cascade.
6. **Paste-code inspect cards** — `A FRIEND'S ITEM` / `A FRIEND'S BUILD` / error card have no fixture;
   the build card is where the legacy `NONE WOVEN` and v1 `Source Form` strings live.
7. **720p capture** — `RH_SHOT` saves the 1920×1080 canvas; the `baseline/720/*` files are downscales. That
   matches `DisplaySettings.PresentFit` (bilinear), so it is an honest stand-in, but the standard should say so.

---

## 8. Proposed layout (authored 1920×1080 space — the space `ChestScreen` lays out in; on screen it lands
at ×0.896 right of the rail until the inset question is settled globally)

**Zone A — screen strip, y 0–110.** Unchanged shared chrome: "THE VAULT" ScreenTitle at (960, 24), gold rule.
Subtitle at y 80 becomes the *count in words* at Body 19 Bone, not Secondary Slate: "8 CHESTS WAITING ·
1 LEGENDARY · 3 EPIC · 2 RARE · 1 UNCOMMON · 1 COMMON" (`ChestDossiers.Tally`).

**Zone B — toolbar, (38, 130) 1842 × 72.**
- Right end: **OPEN ALL (8)** — the one primary: 340 × 60, lit style (gold label / the `ui_button_primary` art at
  rest, which today is hover-only — `UiKit.Button` needs a `primary:` flag or a `PrimaryButton` sibling). With one
  chest: hidden, or relabelled "OPEN" and enabled (P1-8).
- Left of it: **TRADER** secondary: 200 × 60, quiet plate (dark fill + bronze `Outline`, the HUNT `CHEST FILTER`
  row style, `SoloExpeditionScreen.cs:2734–2735`), Bone label at NavigationLabel.
- Far left of the toolbar: **PASTE A CODE** tertiary: text-only Secondary Slate label with a 44 × 44 clipboard
  glyph, hover tip "READ AN ITEM OR BUILD CODE SOMEONE SHARED". (Brief §49: overflow-grade utility.)
- Under the toolbar, one Body 19 Slate **consequence line**, shown only when true: "YOUR LOOT FILTER SELLS
  COMMON DROPS ON SIGHT — 3 OF THESE CHESTS CAN DROP THEM · TIRELESS FORGE WILL MERGE THE HAUL" — every clause
  from an existing member (§6). Never shown when neither filter is owned.

**Zone C — chest grid, (38, 214) 1290 × 840, `PanelQuiet`.** 3 columns × 3 rows of **400 × 250** cards
(pitch 420 × 270), 9 per page (was 18 of 270 × 210). Card anatomy:
- Grade strip 6 px top; chest art **160 × 160** at left (x+16, y+24), tinted toward grade as today.
- Right column x+196: grade word "EPIC" at Headline 24 in grade colour + 5 pips at 20 px; **"TIER 13"** at
  PrimaryValue 30 Bone (the headline number of the card); floor promise at Body 19 Gem/Slate ("RARE OR BETTER" /
  "ANY RARITY — A GAMBLE"); favoured slots at Body 19 Slate ("CHARM · RING" / "NO LEAN").
- Bottom row: Source glyph 40 px **with its word** at Secondary ("MIND"), region name at Secondary; a gold
  **×3** pill 64 × 32 top-left over the art when stacked.
- **An always-visible OPEN** 120 × 44 at bottom-right in gold text (the commit colour, the one place it belongs);
  the whole card still opens on click (playtest request, `ChestScreen.cs:22–25`; §99.5). Hover = lift + edge
  as today. **The peek glyph and its tooltip are removed.**
- Empty state fills Zone C: dim chest art 200 px centred; "THE VAULT IS EMPTY" PanelTitle 26 Bone; two Body 19
  **Slate** lines derived from tuning: "A boss drops a chest one time in five (20 %). Your first was a welcome
  gift." and, only when a keep-filter is set, "Your CHEST FILTER turns away chests below TIER 3 — they arrive as
  Scrap."; **[RETURN TO HUNT]** 300 × 56 primary (new intent on the screen, host maps to `OpenNav(0)`, the same
  pattern as `SoloExpeditionScreen.WantsVault`). TRADER stays reachable (already so, :383–384).
- Overflow: a 6 px quiet scrollbar on the panel's right edge plus wheel; the "ROWS x / y" text is removed.

**Zone D — inspector, (1348, 214) 532 × 840, `PanelQuiet`** — the §14 structure, fed by hover (immediate, no
0.4 s) and pinned by click on a card's art-free area if the owner wants select-then-open; defaults to
`sorted[0]` (the best chest) so it is never blank:
```
CATEGORY   EPIC CHEST · 3 WAITING                 Secondary, grade colour
NAME       TIER 13                                PrimaryValue 30 Bone
IDENTITY   Cinderworks — <RegionBlurb>            Body 19 Slate
WHAT IT PROMISES                                  SectionLabel gold
  • Always RARE or better.                        Body 19 Bone  (FloorLine)
  • 1–2 items at tier 13.                         (ContentsLine)
  • 15–21 Scrap.                                  (MaterialsLine, renamed)
  • Element: MIND.  glyph                         (ElementLine + glyph)
  • Favours CHARM and RING.                       (RegionLine)
  • Won on a good hunt — 35% better odds.         (RunLine, when present)
REQUIREMENTS / FILTER                             only when AutoSellFloor applies
  Your loot filter sells COMMON drops on sight.   Body 19 Slate
[ OPEN ONE ]  primary 240×56       [ OPEN ALL 3 ] secondary
```
A gift shows `GiftChestDef.Lines` in the same slots and a single [OPEN].

**Modals** (trader 1320 × 724, share cards) keep their gold `Panel`; the trader card name wraps to two lines at
Body instead of shrinking (P1-6).

**REMOVED:** peek "?" glyph + 0.4 s dossier tooltip; hover-only OPEN chip; "N THE SAME" line; "ROWS x / y ·
WHEEL SCROLLS"; the two Dim empty-state sentences; disabled OPEN ALL on a one-chest vault.
**MOVED:** full dossier → inspector (Zone D); tally → subtitle in words; PASTE A CODE → tertiary; OPEN ALL →
sole primary; element word → on the card and in the inspector (was hover-only).
**RENAMED:** "MATERIALS" → "SCRAP" (dossier, reveal, trader caption); "NONE WOVEN" → "NO SKILLS"; v1 share-code
`Source Form` fallback → "AN OLDER CODE — SKILL NAMES NOT RECORDED"; class `ChestScreen` → `VaultScreen`,
`_chests`/`_showChests` → `_vault`/`_showVault`; tour card 3 rewritten around OPEN ALL.

**Resolution check for the proposal (authored → canvas → 720p):** card promise at Body 19 → 17 → 11.3 px;
card TIER at PrimaryValue 30 → 26.9 → 17.9 px; inspector lines Body 19 → 11.3 px at 720p. **Still under the
brief's ~24 px effective Body target at 720p** — which is why the P0 typography/UI-scale foundation (§97 items
1–2) must land first or in the same checkpoint; this layout removes the *hierarchy* faults and stops using
Secondary for the primary fact, but no VAULT-local layout can make 19 px authored text read at 720p through a
0.896 inset and a 0.667 downscale.

---

## 9. Remaining questions for the owner

1. **Click grammar.** The 2026-08 playtest asked for click-to-open; the brief's §15 grammar is click-to-select.
   Proposal above keeps click-to-open (safe, §99.5) with hover feeding the inspector. Confirm.
2. **Where should the chest pile live?** Moving `UnopenedChests`/openers out of `ForgeScreen` into a host-owned
   component is the clean fix for P2-3 and for feeding the filter facts to the vault, but it touches the reveal.
3. **"materials" → "SCRAP"** is a vocabulary change across three files (`ChestDossier`, `ForgeScreen`, `ChestScreen`).
   The dossier range test (`chest_dossier_test.cs:61–75`) asserts the *numbers* (`d.MinMaterials..d.MaxMaterials`),
   not the word, so the rename is test-safe as far as I read; confirm before the sweep.
