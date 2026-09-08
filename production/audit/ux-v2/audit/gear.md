# UX V2 audit — GEAR screen

Audited 2026-09-01 against `production/audit/ux-v2/BRIEF.md` (sections 0-15, 60-65, 82-102) and the
current runtime (post 2026-08-31 refactor). Source read in full: `src/IdleXIdle.Game/CharacterScreen.cs`
(1191 lines), `src/IdleXIdle.Game/ItemTooltip.cs`; data: `Core/Economy/Gear.cs`, `ElementSets.cs`,
`ItemAffixes.cs`, `Enchantments.cs`, `HunterProgression.cs`, plus `ItemClasses.cs`, `ItemNaming.cs`,
`GearTraits.cs`, `Builds/GearShape.cs`, `Builds/MasteryTree.cs`, `Progression/Onboarding.cs`,
`Game1.cs` (fixture handler, guide banner, overlay inset, nav). Pixels: `baseline/character.png`
(1920x1080) and `baseline/720/character.png` (1280x720, bilinear downscale — what a 720p player sees),
plus 3x crops of the 720 image and a PIL row-scan of rendered glyph heights.

Source was not modified. This file is the only thing written.

---

## 0. Does the screen answer its question today?

Brief §100 — GEAR: **"What am I wearing and is this item useful?"**

**"What am I wearing"** — yes, well. The dressed doll flanked by eight slots is the strongest thing on
the page; the eye lands on it first at both resolutions (`character.png`: the Hunter is the only large
organic shape on a page of rectangles). Slot labels and rarity bars are correct.

**"Is this item useful"** — the answer exists and is real (the `+679% DAMAGE PER SECOND` strip,
`CharacterScreen.cs:952-981`), but it is a 44px Quiet fill buried in the fourth column at Body 19 /
Secondary 16, under a 150px hero picture and a five-row stat list, and it is drawn in the same Slate as the
metadata around it. At 720p the verdict line is ~11px em text competing with ~43 gold filigree frames. The
screen answers the question in data and fails to answer it in hierarchy.

---

## 1. Measured readability at 720p (P0 evidence)

Two compounding scales apply to this screen, and the second one is invisible in the typography table:

1. The canvas is 1920x1080 and a 1280x720 window shows it at **x0.6667** (`DisplaySettings.PresentFit`).
2. Every menu screen (GEAR included) is drawn through the **overlay inset**: `Game1.cs:5323
   OverlayScale = (1920 - NavRailWidth - 20) / 1920 = 1720/1920 = 0.8958` (`NavRailWidth = 180`,
   `Game1.cs:5305`). The screen's authored 1920x1080 is scaled to 1720x967 canvas px before the
   window scale ever applies. So an authored rung of 16 is **14.3 canvas px at 1080p** and **9.6 px at
   720p** — the rung table (`UiTypography.cs:52-182`) overstates every size on this screen by 10%.

Effective em size of each rung on GEAR (authored -> 1080p canvas -> 720p window):

| Rung | Authored | 1080p | 720p | Where it is used on GEAR |
|---|---|---|---|---|
| ScreenTitle 36 | 36 | 32.2 | 21.5 | "CHAMPION GEAR" |
| PrimaryValue 30 | 30 | 26.9 | 17.9 | "397" |
| PanelTitle 26 | 26 | 23.3 | 15.5 | LOADOUT / EQUIPPED / INVENTORY / ITEM DETAIL |
| Headline 24 | 24 | 21.5 | 14.3 | item name, "SEEKER" |
| Body 19 | 19 | 17.0 | 11.3 | stat rows, buttons, comparison verdict, 8 / 8 EQUIPPED |
| Secondary 16 | 16 | 14.3 | **9.6** | subtitle, header instruction, slot labels, tabs, set ladder, passive, footer hints, LEGENDARY row, element·level line |
| Caption 14 | 14 | 12.5 | 8.4 | (not used on GEAR) |

PIL row-scan of `720/character.png` (bright-pixel row extent of one word, luminance > 90):

- Secondary — subtitle "THE SEEKER": **5 px tall**; slot label "HELM": **5 px**.
- Secondary in Slate — "RIGHT-CLICK AN ITEM" and the set-rung lines: **no pixel above luminance 90**
  (below the scan threshold — the text is both tiny and dim).
- Body — "8 / 8 EQUIPPED": 8 px; "ITEM POWER" label (Slate): 3 px cleared the threshold.
- Headline "MACHINE BOW": 8 px cap. PanelTitle "LOADOUT": 8 px. PrimaryValue "397": 10 px. ScreenTitle: 11 px.

Brief §5 asks for Body effectively ~24-26 and Secondary ~20-21 (authored-equivalent) at 720p. GEAR's
Body is 19 x 0.896 = 17 authored-equivalent, Secondary 14.3. The whole ladder is short by roughly 1.4-1.5x,
and Secondary carries most of the words on this screen (it is used at 22 call sites in `CharacterScreen.cs`).

**Concrete illegible text at 720p** (`720/character.png`):
- Header left: "THE SEEKER · WANDERER — WEARS WANDERER GEAR AND ANY CHARM, RING OR FOCUS" (`:455-456`) — 5px caps, the sentence that explains every dimmed cell.
- Header right: "ONLY WORN GEAR COUNTS IN A FIGHT — RIGHT-CLICK AN ITEM FOR OPTIONS" (`:457-458`) — 5px, Slate, the ONLY place the right-click menu is taught besides the footer.
- Loadout: "EVEN HAND" + passive sentence (`:572-581`), "LEGENDARY" (`:598`), all four SET BONUSES rung sentences (`:645-652`) — Slate/Vellum at 9.6px, wrapped to two lines each.
- Inventory: tab labels ALL/WEAPONS/ARMOR/ACCESSORY (`:767`), "RIGHT-CLICK AN ITEM", "24 ITEMS", "SORT: RARITY" (`:884-895`).
- Item detail: "MACHINE · LEVEL 62" / "WANDERER GEAR" (`:931-937`), "EQUIPPED: NATURE WEAPON" (`:966`), the ENCHANT/SET tags, and the **four set-rung lines in Faint #6A7282** (`:1050-1053`) — the most decision-relevant text on the screen (what one more piece buys) is the smallest and dimmest.
- Equipped: eight slot labels (`:739`) and "GEAR LEVEL 29" (`:751`).

---

## 2. Findings

### P0 — readability / hierarchy

**P0-1 Secondary is the workhorse rung and it is 9.6px at 720p.** Evidence: table above; 22 Secondary
call sites; measured 5px caps. Compounded by the undocumented 0.896 overlay scale (`Game1.cs:5323`).
Fix belongs to the global typography/UI-scale pass, but GEAR must also stop using Secondary for sentences
(brief §6: "Caption/Secondary must not carry sentences" in spirit — the set rungs, passive and header
instruction are sentences at 16).

**P0-2 Everything wears the same frame.** `_ui.Panel` on EQUIPPED (`:709`) and `_ui.PanelQuiet` on the
other three (`:544, :756, :900`) are the same filigree art (`UiKit.cs:567 PanelQuiet => PanelAt(..., QuietFrame)`,
tint #585262); at 1080p the "quiet" frames read bronze-gold, not quiet (`1080_frames` crop: LOADOUT and
EQUIPPED corners are the same ornament). Add the eight ornate slot frames (`ui_slot_empty` /
`ui_slot_trinket_round`, `:727`), twenty ornate item-icon frames (`_forge.DrawItemIcon`, `:782`), four
ornate tabs (`ui_tab_*`, `:764`), four ornate buttons (`ui_button_*`, `:1095`), three currency pills and
the rail: roughly 43 gold-filigree frames in one view. Brief §9: nothing can be PRIMARY when everything is.

**P0-3 The primary action is not obvious.** Four buttons of equal weight and style: EQUIP (`:1057`,
`ui_button_secondary` until hovered), LOCK (`:1059`, permanently disabled — there is no lock system,
`:35`), EQUIP BEST and UNEQUIP ALL (`:616-617`). None is gold at rest. Brief §84 (one primary decision:
GEAR = EQUIP) and §10 (gold = available/primary).

**P0-4 Disabled and secondary share a colour.** `Button()` draws a disabled label in **Slate**
(`:1098 !enabled ? Slate`), the same Slate every Secondary label on the page uses (`:556, :645, :698,
:884, :932`). LOCK's dead label and the live "GEAR POWER" caption are the same colour. Brief §7.

**P0-5 A HUNT lesson sits under GEAR.** `720/character.png`: "EVERY FIFTH WAVE IS A BOSS — Bosses hit
far harder..." (`Tutorial.cs:239`, `TutorialStep.MeetABoss`) is drawn by `Game1.cs:4131 DrawGuideBanner`
as chrome over every screen (`Game1.cs:3109-3140`, rect `GuideBannerRect` = 980 wide, bottom-anchored).
Brief §11/§12: teaching must describe the current screen. (GEAR's own tour exists and is fine —
`Onboarding.cs:237-249`, three cards.)

**P0-6 Ten percent of the canvas is dead on this screen.** The overlay inset maps authored y=1080 to
canvas y=967; panels end at authored 1010 = canvas 905 (`:78-79` comment says so). `character.png`:
nothing but the guide banner below y≈905. Once the banner behaves (P0-5) the band is empty. Either the
inset should not shrink height (letterbox only the width) or GEAR's panels should run to authored 1040+.

### P1 — layout / UX

**P1-1 The LOADOUT column (300px, 16% of width) is spent on duplicates and a mastery title.**
Contents (`:542-625`): portrait; "SEEKER" — which is `Mastery.Affinity()`'s fallback word, not the name
(`:552`; the starter champion happens to be called "THE SEEKER", `CharacterRoster.cs:51`, so the bug
hides — on THE ANVIL this panel would print a portrait of the Anvil over the word SEEKER); "LEVEL 3"
(`Hunter.HunterLevel = 1 + ranks/5`, `HunterProgression.cs:168` — a training count, sitting 1000px from
"LEVEL 62" which is an item level); GEAR POWER 397 (real, `Hunter.PowerRating`); the passive (one line,
belongs to the champion header); a "LEGENDARY 2" row (`:598`) that answers no gear question; and the
SET BONUSES prose (`:638-653`) — four wrapped sentences that are the same four sentences the ITEM DETAIL
ladder prints when a NATURE piece is selected. Brief §61 asks for exactly this column to go.

**P1-2 The hover card is the inspector again, and covers the inspector.** `ItemTooltip` is 460 wide
(`ItemTooltip.cs:38`) and for a Legendary with trait+enchant+set runs ~560px tall (`HeightFor`, `:52-78`):
name, class line, rarity line, item power, verdict, affixes, gems, family, prefix + numbers, enchant + blurb,
set line. It flips left only when `at.X + 484 > canvas.Right` (`:97`), so for grid columns 1-3
(x 1026-1386 authored) it draws to the RIGHT — over the ITEM DETAIL panel (x 1442-1896) that shows the
same facts for the selected item. Brief §15: hover = glance; inspector = full explanation.

**P1-3 Three of four item verbs live behind an untaught right-click.** UPGRADE / REFORGE / SALVAGE are
reachable only from the context menu (`:217-223`, `:259-284`). Discoverability rests on the 9.6px header
sentence (`:457`) and footer hint (`:893`) — permanent manual text (brief §55's Forge rule applies here too,
and law 4). Also TAKE OFF for a worn piece is only in that menu or via the undiscoverable
click-selected-slot-again gesture (`:305`); the inspector's button reads a disabled "EQUIPPED" (`:1057`).

**P1-4 The grid's BETTER halo and the inspector's verdict use different rankings for weapons.** Halo:
`hunter.PowerContribution(item) > PowerContribution(worn)` for every slot (`:827-828`). Inspector and
EQUIP BEST rank weapons by `DamageBench` DPS (`:968-977`, `:383-385`). A bow can carry the green hairline
while the strip says -12% DPS, or vice versa. One ranking per fact.

**P1-5 Set ladder relies on colour alone.** Reached rungs Vellum, unreached Faint (`:1052`); the rung
number is the only other mark. `1080_setladder` crop: four grey lines. Brief §8 and §64 (● / ○ with explicit
active/inactive).

**P1-6 GEAR prints a combo enchant as live when the build cannot fire it.** `:1014-1021` prints
`ench.Name` + `ench.Blurb` only. The Forge, for the same item, asks `EnchantNeed.MetBy` and prints
"NEEDS VOLLEY IN YOUR BUILD — UNTIL THEN IT DOES NOTHING" (`ForgeScreen.cs:1729-1735`, `:552`). An OVERDRAW
bow on a build with no Volley skill reads as a working enchant on the screen where you decide to wear it.

**P1-7 "TRAIT" means two things.** The item prefix is tagged "TRAIT" in the inspector (`:995`) and "PREFIX"
in the hover card (`ItemTooltip.cs:243`); the nav has a TRAITS screen (`Game1.cs:5158`) that is the Dust
tree. Same word, two systems, two screens apart.

**P1-8 Dead vertical inside every panel.** Authored coordinates: grid rows end at y≈750, footer at
926-954 (`:884-895`) — ~170px void (visible in `character.png` as the blank under row 5). Equipped: slots
end ~740, strip at 904 — ~160px. Detail: content ends ~770 for the fixture item, buttons at 910 — ~130px.
Loadout: prose ends ~700, EQUIP BEST at 848. Meanwhile text is at 16.

**P1-9 Tabs are four more ornate frames with 9.6px labels** (`:759-768`, `ui_tab_active/inactive`). A
filter is QUIET furniture (brief §9).

**P1-10 Empty states are not designed.** Empty bag = twenty flat cells + "0 ITEMS"; no item selected =
"No item selected." centred (`:906`); empty slot = "—" (`:737`). Brief §82.

**P1-11 Purple has no meaning.** `Purple #8A5AC8` is the hover reticle (`:738`), the strip accent (`:748`),
the element·level line (`:932`), the enchant name (`:1016`), the scrollbar (`:876`), and the enchant diamond
(`:923`). Gold has a law; purple does not.

### P2 — polish

- P2-1 Hero block (`:917-924`) is a 150px dark box whose only text is "LEGENDARY WEAPON"; the source glyph and an unlabelled purple diamond (`:921-923`) are the only marks — an abstract glyph with no word (brief §8).
- P2-2 "SORT: RARITY" (`:895`) is a label with no control; either a control or fold into the footer sentence.
- P2-3 `Wrapped()` (`:1063-1080`) draws at Label/Body with a 26 pitch while the ladder and the loadout use Secondary at 20/22 — three text rhythms in one column.
- P2-4 Rarity: Common = Bone, Legendary = Gold (`:1188-1189`); with gold as the selection/primary colour, a Legendary's rarity bar and a selected cell compete (the code already fought this at `:789-797`). Consider an orange Legendary.
- P2-5 Comment rot: `:583-612` and `:619-624` describe rows and placements that no longer exist; `:599-605` references SOURCE/FORGE ELEM rows deleted long ago; `:380` "the WEAVE screen".
- P2-6 Doc header `:25` still calls this a "four-panel Champion Gear sheet" — it will be three.

---

## 3. Keep (good, must survive the pass)

- **The doll is the arena's own idle strip** (`:718`, `Character.StripKey("idle")`) — one identity across screens; large Hunter art (brief §62). Keep `HunterBox` size or grow it.
- **Eight slots flanking the doll**, rarity bar on top (`:735`), label under (`:739`), round frames for jewellery (`:726-727`) — shape encodes slot family (brief §8).
- **Grid state grammar**: rarity = left edge (`:799`), better = 1px inner halo (`:839`), selected = outer ring (`:861-862`), locked = scrim + padlock in the class colour (`:823-824`) — different shapes in different places, none competing for a hue. This is the brief's §8 done right; the comments at `:784-864` record the playtests that got it there.
- **Worn pieces leave the grid** (`:187-196`, playtest-driven) and the list is rarity-desc, level-desc.
- **Class-lock honesty**: dimmed not hidden (`:817-825`); "CANNOT WEAR" + who can (`:955-962`, `:990-991`; `ItemClasses.WhyNot`); the menu refuses the verb instead of failing silently (`:350-354`); Game1 re-checks on the only route that equips (`Game1.cs:2606-2616`).
- **Real comparison numbers**: `Hunter.PowerContribution` deltas (`HunterProgression.cs:283-293` — defined as PowerRating-with minus PowerRating-without, so the shown delta IS the change) and `DamageBench` DPS for weapons, swap-and-restore, cached on everything the bench reads (`:397-430`).
- **Effects, not adjectives**: `GearTraits.EffectOf` (`GearTraits.cs:192`) prints the computed trade; `Enchantment.Blurb` prints the trigger.
- **Set ladder shows every rung, reached or not** (`:1039-1054`), with `ElementSets.Progress` honest past 5 ("8 WORN · COMPLETE", `ElementSets.cs:48-52`); `DrawEntries` counts overflow ("AND 2 MORE") rather than clipping (`:661-681`).
- **The menu names the item before SALVAGE** (`:513-520`), measured to the menu width.
- **Selection survives tab switches and dies only when the item is gone** (`:240-249`); scroll clamped every frame (`:251-257`).
- **No fictional bag cap** (`:880-884`).
- **Request/consume routing** to the Forge with the click swallowed (`:199-211`, `Game1.cs:2589-2641`).
- **Fixtures**: `character`, `itemmenu`, `tour <Gear> 1..3`, `RH_SHOT_MOUSE` (`Game1.cs:1641-1699`, `capture.sh`), F7 layout debug (`:1101-1109`).

---

## 4. EQUIP BEST — what it really compares (brief §65)

`CharacterScreen.cs:371-394`. Greedy, one slot at a time, in `AllSlots` order:

- **Seven non-weapon slots**: pick the wearable bag item with the highest `hunter.PowerContribution(item)`
  and equip it if strictly greater than the worn piece's contribution. `PowerContribution` is the marginal
  `Hunter.PowerRating` (`HunterProgression.cs:239`), which reads `SquadDamageMultiplier`, `SquadSkillRate`,
  `SquadHealthMultiplier`, `Defense`, `MaxHealth`, `CritFactor` — i.e. weapon multiplier, charm/focus
  rarity curves, and `WornMods` (traits + affixes + gems + family built-ins, `:313-327`).
- **Weapon**: pick the wearable bag weapon with the highest `DamageBench.Measure(build).Dps` and equip it
  if it beats the worn weapon's DPS by more than 0.1% (`:383-386`). This IS build-aware (bow vs blade with
  the woven skills), and it is the same number the BUILD screen prints.

What it does **not** see:

- **Set bonuses.** `ElementSets` reach the fight via `GearShape.Of` (`Builds/GearShape.cs:19-29`), on the
  Builds side; `PowerRating` (Economy) never reads them (`HunterProgression.cs:239`, `:313`). So EQUIP BEST
  will break a 5-piece NATURE set for a +3 POWER MACHINE glove and never know. (Weapons only: the bench
  does see sets because `Loadout.ToBuild` folds `GearShape`, so the weapon path is set-aware; the other
  seven are not.)
- **Enchant triggers** (`WornEnchantments`, `:366`) — deliberately separate from `WornMods`.
- **Loot/Haul** — deliberately excluded from the rating (`Gear.cs:193-195`).

Verdict: the button is "equip the highest ITEM POWER in each slot, and the highest bench-DPS weapon".
It is not a build optimiser. Rename to **EQUIP HIGHEST POWER**, and give it a one-line caption or tooltip:
"BY ITEM POWER — IGNORES SET BONUSES". Do not add set-awareness by inventing a number; if set-awareness is
wanted, that is a Core change (rank by bench depth/DPS for every slot, which the weapon path already does).

---

## 5. Data honesty (brief §92) — what the model already computes

| Brief wants on GEAR | Status | Evidence |
|---|---|---|
| Before -> after on equip (non-weapon) | **EXISTS** | `Hunter.PowerContribution(item) - PowerContribution(worn)` (`HunterProgression.cs:283`), used at `CharacterScreen.cs:965` |
| Before -> after on equip (weapon) | **EXISTS** | `DamageBench.Measure(Loadout.ToBuild(...), hunter).Dps` swap-and-restore (`CharacterScreen.cs:408-430`) |
| Set ladder with active/inactive rungs | **EXISTS** | `ElementSets.TiersOf`, `WornCount`, `Active`, `Progress` (`ElementSets.cs:55, 120, 123, 48`) |
| "Wearing this reaches NATURE 4" (rung delta) | **EXISTS (derivable, not yet shown)** | compare `WornCount(hunter, el)` with and without the candidate against `ElementSets.Rungs`; the swap-and-restore pattern at `:422-426` is the template. Show the RUNG, never a power figure for it |
| Set contribution to POWER | **NOT COMPUTED — do not show** | `PowerRating` omits `GearShape`/sets (`HunterProgression.cs:239`, `GearShape.cs`) |
| Enchant works with this build | **EXISTS in Core, missing on GEAR** | `EnchantNeed.MetBy(woven, triggers, swornVows)` (`Enchantments.cs:133-142`); Forge wires it (`ForgeScreen.cs:552, 1729`); GEAR has `Loadout`/`Mastery`/`Tree` already (`:60-62`) so the same predicate is computable here — extract `CombosWithBuild` to Core rather than copy it |
| Why can't I wear it | **EXISTS** | `ItemClasses.WhyNot` (`ItemClasses.cs:264-270`) |
| Item power | **EXISTS** | `Hunter.PowerContribution` |
| Hunter "LEVEL" | **EXISTS but mislabelled** | `Hunter.HunterLevel = 1 + ranks/5` (`:168`) — a training count; label it or drop it from GEAR |
| "GEAR LEVEL" | **UI-derived** | average worn `ItemLevel` (`CharacterScreen.cs:750`), no Core member; if kept, say "AVERAGE ITEM LEVEL" |
| Trait / enchant effect text | **EXISTS** | `GearTraits.EffectOf` (`GearTraits.cs:192`), `Enchantment.Blurb` (`Enchantments.cs:159`) |
| Empty-bag guidance ("chests drop gear") | **Fact exists, no counter needed** | `Tutorial.OpenChest` copy; do not print chest counts unless read from the vault model |
| Diagnosis / offline summary / unlock progress | N/A on GEAR | — |

---

## 6. Legacy vocabulary reaching the player or the class names

No Form / WovenAbility / Aptitude / Source x Form composition reaches the player on GEAR. Hits:

- `CharacterScreen.cs:445` title **"CHAMPION GEAR"** vs nav label **"GEAR"** (`Game1.cs:5150`) — brief §6 "screen title = only the name of the screen"; ROSTER's title is "ROSTER" (`RosterScreen.cs:158`).
- Class name **`CharacterScreen`** and fixture mode **`"character"`** (`Game1.cs:476`, `capture.sh:11`) for the GEAR screen — stale screen name in code and tooling (brief §101).
- `CharacterScreen.cs:995` **"TRAIT"** tag on an item prefix; `ItemTooltip.cs:243` says **"PREFIX"** for the same fact; **TRAITS** is a nav screen (`Game1.cs:5158`).
- `CharacterScreen.cs:552` **"SEEKER" / "<STYLE> ADEPT"** — `MasteryTree.Affinity()` (`MasteryTree.cs:282`) printed as the champion's title where a name is expected; collides with the starter's name "THE SEEKER" (`CharacterRoster.cs:51`).
- `CharacterScreen.cs:380` comment: "the same one the **WEAVE** screen prints" — the screen is BUILD.
- `CharacterScreen.cs:65, 404, 414` comments: "**woven** skills" — note the verb is still live player copy elsewhere (`BuildScreen.cs:1519 "WEAVE IT ON THE BUILD SCREEN"`, `Game1.cs:5041 "WOVEN SKILLS"`), so this is a terminology-pass decision, not a GEAR defect.
- `Core/Economy/Enchantments.cs:170-172` comment: "The **Form** it needs ("TRANSFORMATION")" — stale; Siphon's need is now `Style.Drain` (`:187`). Comment only.
- `Core/Builds/MasteryTree.cs:198` comment: "its **Form** silently counted for nothing" — stale. Comment only.
- `CharacterScreen.cs:599-605` comment describes "SOURCE"/"FORGE ELEM" rows that no longer exist.

---

## 7. Proposed layout (authored 1920x1080; the overlay inset still applies)

Three panels, 16px gutters, 24px outer margins; brief §61's 42 / 27 / 31 split of the 1840px that remain
after gutters. LOADOUT is removed. Panels run y 100 -> 1040 (reclaiming 30px; more if the inset stops
shrinking height, see P0-6).

**Title band — y 0-88.** "GEAR" ScreenTitle centred (rename). Currency pills stay (shared chrome).
REMOVE the right-hand instruction sentence (`:457`). REMOVE the left identity sentence from here (it moves
into the EQUIPPED header).

**EQUIPPED — x 24, w 776, y 100-1040. PRIMARY: the one ornate frame on the page.**
- Header row y 150-240: portrait 72px at x 64; "THE SEEKER" Headline Bone; under it "WANDERER · WEARS
  WANDERER GEAR AND ANY CHARM, RING OR FOCUS" Body Slate-readable (not Secondary); right-aligned "GEAR
  POWER" caption + "397" PrimaryValue; "EVEN HAND — Every skill hits 8% harder" one Body line at y 250.
  (MOVED from LOADOUT: portrait, name, power, passive. REMOVED: "SEEKER/ADEPT" mastery title, "LEVEL 3",
  "LEGENDARY 2".)
- Doll y 290-880, x 200-600 (400x590, up from 278x588 — the Hunter grows). Slot columns at x 70 and
  x 630, 102px boxes, 130 pitch from y 300, labels at Body 19 in Bone/Slate.
- Footer strip y 895-945: left "8 / 8 EQUIPPED · AVERAGE ITEM LEVEL 29" Body; right: active-set chips
  "[glyph] NATURE 5 / 5" (one chip per element with a reached rung, glyph + word + count — no prose;
  the prose lives only in the inspector ladder). MOVED from LOADOUT's SET BONUSES; REMOVED the sentences.
- Utility row y 960-1010: "EQUIP HIGHEST POWER" and "UNEQUIP ALL" as secondary (bronze/thin) buttons,
  each 360 wide. RENAMED from EQUIP BEST; MOVED from LOADOUT; demoted from ornate.

**INVENTORY — x 816, w 496, y 100-1040. QUIET surface** (dark translucent fill, 1px subtle border, no
filigree).
- Title y 122; filter y 150-194 as a `UiKit.Pill` segmented row (ALL · WEAPONS · ARMOR · ACCESSORY) at
  Body — RENAMED from ornate tabs.
- Grid y 210-890: 4 cols x 104px cells, 8px gap (fills the 440 content width), 6 rows visible (24 cells,
  up from 20). Empty cells: flat #14111C, no border, no frame art (brief §63). Filled cells keep the
  current state grammar (rarity edge / halo / ring / scrim+lock). Scrollbar as today.
- Footer y 905-945: "24 ITEMS · SORTED BY RARITY" Body Slate. REMOVED "RIGHT-CLICK AN ITEM" and
  "SORT: RARITY" as separate hints; the right-click lesson moves into tour card 2 ("YOUR ITEMS",
  `Onboarding.cs:241`) and a small "⋯" corner mark appears on a hovered cell.

**ITEM DETAIL — x 1328, w 568, y 100-1040. SECONDARY (thin bronze / PanelQuiet at true quiet tint).**
Brief §14 inspector order:
- y 150 CATEGORY: "LEGENDARY WEAPON · MACHINE · LEVEL 62 · WANDERER GEAR" — one Body line, rarity colour on the first two words, class colour on the last.
- y 185 NAME: "MACHINE BOW" Headline in rarity colour.
- y 230-330 DECISION FIRST: icon 96px at left; right of it the verdict block — "+679% DAMAGE PER SECOND"
  Headline green / "-4 POWER" Ember / "SLOT EMPTY" / "EQUIPPED" / "THE SEEKER CANNOT WEAR THIS" + the who-can
  line; under it "REPLACES: NATURE BOW" Body Slate. MOVED up from `:952-981`; REMOVED the 150px hero box.
- y 350 WHAT IT DOES: ITEM POWER + affix rows at Body, 30 pitch; then PREFIX name + `EffectOf` numbers;
  then ENCHANT name + blurb + liveness line "WORKS WITH YOUR BUILD" / "NEEDS VOLLEY IN YOUR BUILD"
  (`EnchantNeed.MetBy`). RENAMED TRAIT -> PREFIX.
- y ~700 SET: "MACHINE SET · 0 OF 5 WORN" Body; four rungs as "■ 2  Your MACHINE skills hit 8% harder."
  (filled square = reached, Vellum) / "□ 3 …" (outlined square = not reached, Faint) with the word
  "ON" right-aligned on reached rungs — three encodings (mark, colour, word), brief §8/§64. If the
  candidate would reach a new rung, the rung line ends "← WEARING THIS REACHES IT" (rung delta is
  derivable; see §5). Body size, 26 pitch.
- y 960-1010 PRIMARY ACTION: one full-width gold EQUIP (ornate allowed — it is the screen's one decision);
  "TAKE OFF" (secondary style) when the item is worn; "CANNOT WEAR" disabled with the reason above.
  REMOVED: LOCK. A secondary text link "SEND TO FORGE ›" under the button opens the same UPGRADE/REFORGE/
  SALVAGE verbs the right-click menu offers.

**Hover card (shared `ItemTooltip`)** on GEAR shrinks to a glance: name, category line, class line,
verdict line, "CLICK TO INSPECT" — ~460x170. Full affix/prefix/enchant/set text lives in the inspector.
Position: below the cell when room, above otherwise; never to the right over the inspector.

Summary of REMOVED / MOVED / RENAMED:
- REMOVED: LOADOUT panel; "SEEKER/ADEPT" title; "LEVEL 3"; "LEGENDARY 2" row; SET BONUSES prose; LOCK
  button; header instruction sentence; "RIGHT-CLICK AN ITEM" and "SORT: RARITY" hints; 150px hero box;
  ornate frames on INVENTORY, ITEM DETAIL, tabs, utility buttons.
- MOVED: portrait/name/class sentence/power/passive -> EQUIPPED header; active sets -> EQUIPPED footer
  chips; EQUIP BEST/UNEQUIP ALL -> EQUIPPED utility row; comparison verdict -> top of inspector;
  right-click lesson -> tour card + hover mark.
- RENAMED: "CHAMPION GEAR" -> "GEAR"; "EQUIP BEST" -> "EQUIP HIGHEST POWER"; "TRAIT" -> "PREFIX";
  "GEAR LEVEL" -> "AVERAGE ITEM LEVEL"; "EQUIPPED" (disabled button) -> "TAKE OFF" (live).

---

## 8. Fixtures

**Exists today** (`Game1.cs:1641-1699`; `tools/asset-pipeline/capture.sh` modes `character`, `itemmenu`,
`tour <out> Gear <1..3>`; `RH_SHOT_MOUSE=x,y` parks the cursor, `Game1.cs:3872`):
- All eight slots worn with NATURE pieces of the starter's class (WANDERER), iL 8..50, rarity cycling —
  poses a COMPLETE set (8 WORN · COMPLETE) and every doll slot filled.
- Bag: a Legendary MACHINE bow (Wanderer, iL62, Family 1) as the UPGRADE candidate; an Epic WARDEN chest
  (locked cell); 14 extras rotating five classes and six elements.
- `itemmenu` opens the context menu on cell 0.
- 720 copies are made offline by bilinear downscale (scratch `downscale720.py`); capture.sh renders the
  1920 canvas only.

**Gaps against brief §95 "GEAR (set + inventory + comparison)" and §94/§96:**
1. **No inventory overflow.** 2 + 14 = 16 unworn items < 20 visible cells, so the scrollbar is never posed —
   the fixture comment (`Game1.cs:1683`) claims it overflows; `character.png` shows row 5 empty, no track.
   Need ≥ 21 unworn (≥ 25 for the proposed 6-row grid).
2. **No partial set ladder.** Worn = NATURE 8/8 (complete); default selection = MACHINE bow (0/5). The
   mixed reached/unreached state — the whole point of §64 — is never photographed. Need e.g. 3 NATURE worn
   plus a selected NATURE candidate that reaches rung 4 (also poses the "reaches it" line).
3. **No non-weapon comparison** in the default pose (bow -> DPS%). Need a `RH_SHOT_SELECT=<instanceId>`
   or a second mode selecting an armour piece so "+N POWER" / "-N POWER" / "SLOT EMPTY" are captured.
4. **No empty states**: empty doll slot, empty bag, "no item selected".
5. **No hover-card pose in the baseline** (RH_SHOT_MOUSE exists; not used for `character.png`).
6. **No dead-combo enchant**: the bow's HARVEST has no `Needs`; an OVERDRAW piece on the fixture build
   (which has no Volley skill) would pose the "NEEDS VOLLEY" line.
7. **No worn-item-selected pose** (TAKE OFF / "EQUIPPED" strip).
8. **No real 720p capture**: capture.sh cannot set the window size; brief §94 wants the presented 1280x720
   frame, not a resample. Add `RH_SHOT_WINDOW=1280x720` through `_windowSize` (`Game1.cs:3939`).
9. **No screen-level test**: Core has `PowerContributionTests.cs` and `element_sets_test.cs`; nothing
   asserts that the grid halo, inspector verdict and EQUIP HIGHEST POWER agree on a weapon (P1-4), or that
   `ItemTooltip.HeightFor` matches what `Draw` draws (the gem branch: 56 counted vs 90 drawn,
   `ItemTooltip.cs:61` vs `:190-193` — not reachable from GEAR's wearable filter, but the mismatch exists).
