# GLOBAL — Legacy terminology reaching the player, and the stale names on the classes

**Audit date:** 2026-09-01 · **Against:** `main` @ `255b07d` (working tree `feat/hunter-cutout-rig`, clean) · **Brief sections applied:** 0, 66, 67, 101 (plus 8, 12, 88, 92, 95 where they bear on vocabulary).
**Method:** whole-word and case-insensitive grep of `src/IdleXIdle.Game/*.cs` (25,334 lines) and of the player-facing Core copy files (`Onboarding`, `Tutorial`, `Unlocks`, `CharacterRoster`, `Vows`, `MemoryDust*`, `DustEffects`, `Enchantments`, `ItemClasses`, `Quests`, plus `BuildGlossary`, `Keystones`, `MasteryCatalog`, `ElementSets` because their strings are drawn verbatim). Every hit was opened and classified. Screenshots read at 1080p and at the 720p copy.
**Runtime truth assumed (verified in code):** `enum Form` does not exist; `SkillCatalogue`/`SkillId` is identity; `StyleAffinity.Factor(Style, Style, vowSworn)` is the affinity ring; `SavedSkill.Form` is read-only migration data (`SaveGame.cs:316-317`); `LegacySkillForm.cs` is the frozen seam.

---

## 0. Verdict in one paragraph

The runtime is clean — no `Form` type survives, and the refactor report's claim (`REPORT.md:43-44`) holds. The **UI is not clean**. Eleven live, player-reachable strings still say FORM or teach Source × Form (F1 help panel, the ATTUNEMENT ceremony, two mastery node labels, one keystone blurb); a further ~20 live strings use the retired *weave / woven* verb; and one concept — the Style you specialised in — is spoken to the player under **four names** (SPECIALISATION, DISCIPLINE, ATTUNEMENT/ATTUNED/UNATTUNED, and in code AFFINITY) while the word *attune* itself carries **four unrelated meanings** across screens. The retired BUILD overview (`BuildScreen.DrawSummary/DrawCore/DrawAuraCards`, ~330 lines) still compiles with its "EVERY SKILL IS A SOURCE AND A FORM" headline and is reached by the `build` capture mode and the boot check, but by no player. Six of the nine screen classes carry names that no longer match the screen title they draw. All of this is copy and identifiers; none of it needs new telemetry.

---

## 1. Does the UI speak the current model today? (brief §100 / §66)

**No.** The BUILD screen (`WeaveScreen`) itself is mostly right — skill rows show the skill's own glyph and name, the variation fork and reinforcement strip exist, Vows show MET/UNMET — but its frame still says "WHAT YOU ARE **WEAVING**" and "THEN **WOVEN** INTO A SLOT", and the moment a player leaves it the old model returns: F1 says "SOURCE x FORM x VOW / FORM IS HOW YOU FIGHT", the first specialisation ceremony says "THE FAR FORMS HIT SOFTER", the mastery tree's NARROW/BROAD notables price "YOUR DISCIPLINE'S FORM", and the TRAITS detail for KEYSTONE — WEAVER says "FIRES AS THE NEXT FORM YOU CARRY". A new player is told three different vocabularies for one system inside five minutes.

---

## 2. P0 — stale words a player can read right now

Each row: where, what the player sees, why it is stale, the replacement. "Derived?" says whether the number in the replacement already exists in Core (brief §92).

| # | Evidence | Player sees | Why stale | Replace with | Derived? |
|---|---|---|---|---|---|
| P0-1 | `Game1.cs:5042` (F1 help, "YOUR BUILD" column) — `720/help.png` row 2 | `SOURCE x FORM x VOW` | Teaches the deleted Source×Form composition on the one screen a new player opens for orientation | `STYLE → SKILL → VARIATION → REINFORCEMENTS` (brief §66 wording), or drop the row and let the help name the BUILD screen | n/a (copy) |
| P0-2 | `Game1.cs:5046` — `720/help.png` row 6 | `FORM IS HOW YOU FIGHT` | "Form" no longer exists; the answer to "how you fight" is the SKILL (and its Style) | `A SKILL IS HOW YOU FIGHT · ITS SOURCE IS WHAT IT IS MADE OF` | n/a |
| P0-3 | `BuildScreen.cs:1398` (THE ATTUNEMENT modal, live on first Specialisation take) | `YOUR HAMMER SKILLS HIT TWICE AS HARD. THE FAR FORMS HIT SOFTER —` | "FORMS" for what the ring actually indexes: STYLES (`StyleAffinity.Factor`) | `...THE FAR STYLES HIT SOFTER —` | Yes — `StyleAffinity.Factor` dist 0 = 2.00, off 0.75, opposite 0.45 (`StyleAffinity.cs:11`) |
| P0-4 | `BuildScreen.cs:1400` (same modal) | `A VOW ON A FAR-FORM SKILL PULLS IT ONE RING CLOSER.` | as above | `A VOW ON A FAR-STYLE SKILL PULLS IT ONE RING CLOSER.` (WeaveScreen.cs:866 already says FAR-STYLE — the two screens disagree) | Yes — `Factor(..., vowSworn: true)` |
| P0-5 | `MasteryCatalog.cs:179` — node label, drawn at `BuildScreen.cs:881/1478/1833` on hover and pinned | `NARROW — YOUR DISCIPLINE'S FORM +20%, EVERY OTHER FORM -10%` | Form = Style here; fields are `AffinityStyleBonus`/`OffStylePenalty` | `NARROW — YOUR OWN STYLE +20%, EVERY OTHER STYLE -10%` | Yes — `SkillShape.AffinityStyleBonus = 0.20`, `OffStylePenalty = 0.10` |
| P0-6 | `MasteryCatalog.cs:181` | `BROAD — HALF OF THE OPPOSITE FORM'S PENALTY IS GIVEN BACK` | as above | `BROAD — HALF OF THE OPPOSITE STYLE'S PENALTY IS GIVEN BACK` | Yes — `OppositePenaltyRelief = 0.5` |
| P0-7 | `Keystones.cs:184` — surfaces in TRAITS detail via `MemoryDustText.cs:114` (the "WHAT IT DOES" sentence under KEYSTONE — WEAVER, cf. BLOODLUST in `720/dust.png`) | `EVERY SKILL ALSO FIRES AS THE NEXT FORM YOU CARRY, AT 45%. SKILLS RETURN 30% SLOWER.` | "FORM" for "skill in the next slot" (`SoloBattle.cs:1636-1654` echoes `skills[(i+1) % count]`) | `EVERY SKILL ALSO FIRES THE NEXT SKILL IN YOUR BUILD, AT 45%. SKILLS RETURN 30% SLOWER.` | Yes — `SoloBattle.WeaverEchoFraction = 0.45f`, `BuildMods(SkillRate: 0.70f)` |
| P0-8 | `BuildScreen.cs:690, 763, 779, 826` — the retired BUILD overview | `EVERY SKILL IS A SOURCE AND A FORM — YOU CHOOSE BOTH` · row `FORMS` · `Add a skill to begin. Every skill is a SOURCE and a FORM.` · card title `{SOURCE} {SKILL}` | The whole Source×Form composer page. **Player-unreachable** (`Game1.cs:5293` always sets `ShowTree = true`; nothing ever sets it false) but still compiled, still posed by `RH_SHOT_MODE=build` (`Game1.cs:1561`) and by `BootCheckScreens` "build" (`Game1.cs:1317`) | **Delete** `DrawSummary`, `DrawCore`, `Sentences`, `DrawAuraCards`, `DrawPassives`, `WeaveBtn`, `AuraCard`, `WantsWeave`, the `!_editMode` branches (`:593-603`, `:683-701`) and the `build` capture mode. Brief §67: "otherwise delete Form-only presentation" | n/a |

P0-8 is P0 on hygiene grounds rather than readability: it is the last complete Source×Form presentation in the assembly, it is one `ShowTree = false` away from returning, and its capture mode produces a screenshot of a screen no player has.

---

## 3. P1 — retired verbs and a concept with four names

### 3a. "Weave / woven" as the verb for equipping a skill (brief §66 "Source/Form composer language")

The loom metaphor was the Source×Form composer's ("six Forms crossed with six Sources", `BuildGlossary.cs:13`). With skills as catalogue items you *equip* or *slot* them. Live strings:

| Evidence | Player sees | Replace with |
|---|---|---|
| `WeaveScreen.cs:926` (panel title, `weave.png` left column) | `WHAT YOU ARE WEAVING` | `YOUR LOADOUT` (brief §68 wording) |
| `WeaveScreen.cs:860` (screen subtitle) | `A SKILL IS LEARNED ON THE MASTERY TREE, THEN WOVEN INTO A SLOT` | `LEARN A SKILL ON THE MASTERY TREE, THEN EQUIP IT IN A SLOT` |
| `WeaveScreen.cs:647` / `:691` (status line) | `SLOT UNWOVEN.` / `SLOT WOVEN.` | `SLOT CLEARED.` / `SKILL EQUIPPED.` |
| `Game1.cs:5041` (F1 help) | `{n} WOVEN SKILLS` | `{n} SKILL SLOTS` (n = `PlayerLoadout.SkillCapacity`, already live) |
| `BuildScreen.cs:1519` (mastery node card, skill road) | `...WEAVE IT ON THE BUILD SCREEN (B).` | `...EQUIP IT ON THE BUILD SCREEN (B).` |
| `ChestScreen.cs:742` (A FRIEND'S BUILD modal) | `NONE WOVEN` | `NO SKILLS` |
| `Unlocks.cs:254` (slot-unlock note) | `A FOURTH SKILL. The full weave. ...` | `A FOURTH SKILL. A full build. ...` |
| `MasteryCatalog.cs:189` (node label PURE) | `PURE — WHILE EVERY WOVEN SKILL SHARES ONE SOURCE, ALL SKILLS HIT 35% HARDER` | `PURE — WHILE EVERY EQUIPPED SKILL SHARES ONE SOURCE, ...` |
| `CharacterRoster.cs:162` = `Quests.cs:203` (quest demand, ROSTER locked card) | `Clear 150 waves with VOLLEY skills woven` | `Clear 150 waves with a VOLLEY skill equipped` |

Keep as proper nouns (they are content names, not the verb): keystone **WEAVER** (`Keystones.cs:183`, trait `ks_weaver` "KEYSTONE — WEAVER", `art_terminal_weaver`), asset `sfx_weave`, trait id `weave_5` (its player name is already "FIFTH SKILL SLOT"). The WEAVER keystone's *blurb* is P0-7 above.

### 3b. One concept, four player words: SPECIALISATION · DISCIPLINE · ATTUNEMENT · (AFFINITY)

The chosen Style (`Build.Affinity`, `MasteryTree.Affinity()`, `StyleAffinity`) is presented as:

| Word | Live evidence |
|---|---|
| SPECIALISATION | node kind `BuildScreen.cs:1587` "SPECIALISATION — YOUR DISCIPLINE"; tree labels "HAMMER SPECIALISATION" (`buildtree.png`); `Onboarding.cs:275` |
| DISCIPLINE | `BuildScreen.cs:666, 1132, 1499-1504, 1542, 1587`; `WeaveScreen.cs:866, 869`; `ChestScreen.cs:735-736`; `Onboarding.cs:275-276` "ONE DISCIPLINE"; `MasteryCatalog.cs:179` |
| ATTUNEMENT / ATTUNED / UNATTUNED | `BuildScreen.cs:581, 588, 1295` "YOUR ATTUNEMENT", `:1310` "UNATTUNED", `:1386` "THE ATTUNEMENT"; `Onboarding.cs:64` |
| AFFINITY | code only (`Mastery.Affinity()`, `StyleAffinity`) — never shown, which is fine |

Brief §72 says the mastery screen is about "Style philosophy", §8 says Style must be named as `STYLE`. **Pick one player word and make it carry Style.** Recommendation: the node kind stays **SPECIALISATION** (it is already the label on six tree nodes and in the cost ladder); the state becomes **YOUR STYLE** — panel title `BuildScreen.cs:1295` "YOUR ATTUNEMENT" → `YOUR STYLE`; plate `:1310` "UNATTUNED / TAKE A SPECIALISATION NODE" → `NOT CHOSEN / TAKE A SPECIALISATION NODE`; ceremony title `:1386` "THE ATTUNEMENT" → `YOUR SPECIALISATION`; messages `:581` "ATTUNED. HAMMER IS YOURS —" → `HAMMER IS YOUR STYLE —`; `:588` "ATTUNE WHEN YOU ARE READY" → `SPECIALISE WHEN YOU ARE READY`; `:666`/`:1542` "ALREADY ATTUNED — ONE DISCIPLINE PER HUNTER" → `ALREADY SPECIALISED — ONE STYLE PER HUNTER`; `:1132` "FOUR DIRECTIONS · ONE DISCIPLINE · TWELVE SKILLS TO LEARN" → `FOUR DIRECTIONS · ONE STYLE · TWELVE SKILLS TO LEARN`; `:1499-1504` "{f} IS YOUR DISCIPLINE" → `{f} IS YOUR STYLE`; `:1587` → `SPECIALISATION — CHOOSES YOUR STYLE`; `WeaveScreen.cs:866` "DISCIPLINE: HAMMER — ..." → `YOUR STYLE: HAMMER — ITS SKILLS HIT TWICE AS HARD ...`; `:869` "NO DISCIPLINE YET — A SPECIALISATION NODE ..." → `NO STYLE CHOSEN YET — A SPECIALISATION NODE ON THE MASTERY TREE (E) CHOOSES ONE`; `ChestScreen.cs:735-736` → `STYLE: HAMMER` / `NO STYLE CHOSEN`; `Onboarding.cs:275-276` "ONE DISCIPLINE / A specialisation is your discipline: that STYLE's skills hit twice as hard" → `ONE STYLE / A specialisation chooses your Style: that Style's skills hit twice as hard`. Every "twice as hard" is `StyleAffinity.Factor` distance 0 = 2.00 — derived, keep.

### 3c. "Attune" already means four different things to the player

1. Style specialisation (above). 2. The TRAITS subtitle `PrestigeScreen.cs:538` prints `... · ATTUNED` when `DustEffects.TreeComplete` — i.e. when the trait node id `attunement`, player-named "A MARK — NO EFFECT" (`MemoryDust.cs:538`), is owned. 3. Gear trait `GearTrait.Attuned` = "Skills come faster. No downside." (`GearTraits.cs:242`), printed on item cards. 4. Items/chests "attuned to" a region's element (`ChestDossier.cs:42,102`, vault card copy). After 3b, meaning 1 disappears; rename the TRAITS subtitle word to `ALL FOUR ROADS WALKED` (what the node actually certifies, `MemoryDust.cs:540`) and leave 3 and 4, which are gear/loot words with their own sentences.

### 3d. MARK vs AMPLIFY (the SIGN style's effect)

The old Form "Mark" became Style **SIGN** with `SkillEffect.Amplify`. The player now meets three words for the same window: **MARK** (`BuildGlossary.cs:53` Mind signature "stretches an open MARK window"; `ElementSets.cs:90` "A MARK's window lasts 50% longer."; `CharacterRoster.cs:199` THE OATHBOUND "Your Marks last longer"; `MasteryCatalog.cs:441` "MARK SPECIALIST — THE WINDOW STRETCHES"), **AMPLIFY** (`Enchantments.cs:166` LINGER blurb "AMPLIFY LASTS LONGER"; `:193` need label "AN AMPLIFY WINDOW", printed on the Forge/Gear enchant rows), and **SIGN** (the Style name on every card, `spec_mark`'s own node sits under the SIGN specialisation diamond in `buildtree.png`). The catalogue's own variation prose uses lowercase *mark* as the thing CALL/BRAND put on an enemy (`SkillCatalogue.cs:429-449`), so the honest noun is the mark and the Style is SIGN. Recommendation: the *effect* is "a MARK"; never use MARK as a Style or specialisation name. `MasteryCatalog.cs:441` → `SIGN SPECIALIST — MARKS LAST LONGER`; `Enchantments.cs:166` → `MARKS LAST LONGER`; `:193` → `A SKILL THAT MARKS` (need is `AnyAmplify`, i.e. any def with `AmplifyPercent/AmplifyMs`); `BuildGlossary.cs:53`, `ElementSets.cs:90`, `CharacterRoster.cs:199` already say MARK — keep. The trait node "A MARK — NO EFFECT" (`MemoryDust.cs:538`) then collides with the effect noun (see P2-3).

### 3e. Stale screen names on the classes (brief §67) — see §7 for the full map

`WeaveScreen` draws the screen titled BUILD; `BuildScreen` draws MASTERY TREE (plus the dead overview); `FormHexDiagram` draws the Style affinity ring; `PrestigeScreen` → TRAITS; `CharacterScreen` → CHAMPION GEAR; `ChestScreen` → THE VAULT; `StatsScreen` → STATS (which brief §38 wants read as TRAINING); `SoloExpeditionScreen` → HUNT. `Game1` fields follow the old names (`_weave/_showWeave`, `_buildScreen/_showBuild`, `_prestige/_showPrestige`, `_character/_showCharacter`, `_chests/_showChests`, `_stats/_showStats`) and so do the fixture modes (`weave`, `build`, `dust`, `character`, `stats`, `vault`).

### 3f. Help panel duplicates and contradicts itself

`Game1.cs:5065` `("T", "BUILD, THE SAME PLACE AS B")` — a second key row for one screen. `Game1.cs:2517-2535` shows T is a *toggle* of the loadout screen while B opens it; the help should list B only and drop the T row (or document T as "toggle"). `Game1.cs:5044` `TRAITS (P) SELL MORE OF BOTH` refers back to two rows the player has to re-read to resolve "both".

---

## 4. P2 — polish

- **P2-1** `ChestScreen.cs:748-750`: a v1 share code with no `SkillId` prints `{SOURCE} {FORM}` words ("BODY STRIKE") to the player. `LegacySkillForm.cs:26-35` maps a Form name to the style's two skill ids — resolve through it (active skill for non-passive, passive otherwise) and print `SkillCatalogue.Find(id).Name`, falling back to `UNKNOWN SKILL`.
- **P2-2** `SoloExpeditionScreen.cs:1394,1397` and `Character.cs:187`, `SkillCatalogue.cs:396,425` (`ClipKey/FxKey: "mark"`, `"transformation"`), `AssetLibrary.cs:178` (`fx_mark`, `fx_transformation`), `FormHexDiagram.cs:321-326` (`icon_form_*`): asset keys still spell the six Form names. Not player-visible; acceptable as file names (renaming forty PNGs is churn), but the `IconKey` mapping should live beside the Style enum, not in a class called FormHexDiagram.
- **P2-3** `MemoryDust.cs:538` trait "A MARK — NO EFFECT" — after 3d "MARK" is the SIGN effect; rename the badge node (`A KEEPSAKE — NO EFFECT` or `ALL FOUR ROADS — NO EFFECT`). `TraitNamesTests.cs:45-60` pins only *old* names as forbidden, so this is safe.
- **P2-4** `SkillShape.cs:145-155` doc on `StylePower` still calls it "a character's APTITUDE"; `CharacterRoster.cs:174,201`, `ItemClasses.cs:107`, `BuildComposer.cs:18,105`, `GearShape.cs:12`, `PlayerLoadout.cs:225`, `Character.cs:66,124`, `RosterScreen.cs:296,304` — comment-only. `RosterScreen.cs:304` records the roster row's deletion correctly; the rest describe a live field by a retired name. Reword to "per-Style power".
- **P2-5** `assets/art/idlexidle_ux_screen_guide_standard.md:68` "Build: fixed Source + Form + Vow setup" and `:123` "no Form glyph → documented temporary label fallback" — the standard brief §88 already condemns; both lines go in V2.
- **P2-6** `design/gdd/skill-slots-and-skill-trees.md:83,91,916,923-928,944` and `docs/PROJECT-NOTES.md:92,126,256,392,1000,1011` speak of `WovenAbility`, `Build.Weave`, `WeaveScreen`. Historical docs; add a SUPERSEDED banner or a "names as of 2026-09" footnote when the classes are renamed.

---

## 5. KEEP — what is already right and must survive the cleanup

- **BUILD screen body** (`WeaveScreen.cs:986-1030`): the row is the skill's own glyph (`icon_skill_{id}`) beside the Source gem, the skill's own name, its Source in the Source colour, ACTIVE/PASSIVE as a switch, variation and reinforcement counts. This is the STYLE → SKILL → VARIATION → REINFORCEMENTS model on screen; only the frame copy is stale.
- **Fight rail** (`SoloExpeditionScreen.cs:3006-3080`): label is `railDef.Name`, rhythm read from `railDef.Kind/Beats`, headline from `railDef.Line` — no Form anywhere the player looks (`720/fight.png`: BLOW / SPRAY / PRESS / JAWS with their own lines).
- **Vow demand copy** (`WeaveScreen.cs:547-563`, `Vows.cs`): `VowDemand.SingleStyle` already reads "EVERY SKILL THE SAME STYLE" — the rename from SingleForm was done correctly and honestly (`Vows.cs:56-64`).
- **Onboarding BUILD tour** (`Onboarding.cs:255-269`): "Twelve skills exist, two per style. You learn them on the MASTERY tree ... pick any learned one for the chosen slot" — current model, plain words, no jargon.
- **Tutorial WeaveBuild body** (`Tutorial.cs:271-273`): "Pick a slot, then one of your learned skills from the library" — correct; only the enum name is stale.
- **Trait node names** (`MemoryDust.cs`): "FIFTH SKILL SLOT", "KEYSTONE SOCKET II", "LEARN VOWS I" — plain and pinned by `TraitNamesTests`.
- **FormHexDiagram's drawing** (`FormHexDiagram.cs:125-200`): the six seals iterate `Enum.GetValues<Style>()` and weight the threads by `StyleAffinity.Factor` — it *is* a Style ring (brief §67's "only if it truly represents Style" test passes). Keep the diagram, rename the class.
- **Save/migration seam**: `SavedSkill.Form` (`SaveGame.cs:316-317`), `LegacySkillForm.cs`, `ShareCodes.cs:244-245`, `Game1.cs:633` `s.Form` pass-through, `PlayerLoadout.Restore` rows. These are the only *code* uses of Form and every one is read-only migration, documented as such. Do not touch.
- **The refactor's own history comments** (BuildScreen 657-661, 743, 771, 1575-1578; SoloBattle 777, 1380; SkillCatalogue 14-22; StyleAffinity 10-13; Vows 9, 60-61): they say what *was* and why it changed. Acceptable historical remarks.

---

## 6. THE LEGACY TABLE (brief §101) — every hit, classified

Legend — **Class:** PV = player-visible string · ID = code identifier · CM = comment/doc only · AS = asset/save/fixture id. **Verdict:** STALE = retired concept spoken as present · OK = acceptable (historical remark, save-format member, proper noun, shape word, migration seam).

### 6a. FORM / FORMS

| File:line | Class | Text / identifier | Verdict | Action |
|---|---|---|---|---|
| Game1.cs:5042 | PV | `SOURCE x FORM x VOW` | STALE | P0-1 |
| Game1.cs:5046 | PV | `FORM IS HOW YOU FIGHT` | STALE | P0-2 |
| BuildScreen.cs:690 | PV (unreachable) | `EVERY SKILL IS A SOURCE AND A FORM — YOU CHOOSE BOTH` | STALE | P0-8 delete |
| BuildScreen.cs:763 | PV (unreachable) | row label `FORMS` | STALE | P0-8 delete |
| BuildScreen.cs:779 | PV (unreachable) | `Add a skill to begin. Every skill is a SOURCE and a FORM.` | STALE | P0-8 delete |
| BuildScreen.cs:826 | PV (unreachable) | card title `{Source} {SkillWord}` ("Source + Form" per its comment) | STALE | P0-8 delete |
| BuildScreen.cs:1398 | PV | `THE FAR FORMS HIT SOFTER` | STALE | P0-3 |
| BuildScreen.cs:1400 | PV | `A VOW ON A FAR-FORM SKILL` | STALE | P0-4 |
| MasteryCatalog.cs:179 | PV | NARROW label, `FORM` ×2 | STALE | P0-5 |
| MasteryCatalog.cs:181 | PV | BROAD label, `FORM` | STALE | P0-6 |
| Keystones.cs:184 | PV (via MemoryDustText.cs:114) | WEAVER blurb `NEXT FORM YOU CARRY` | STALE | P0-7 |
| ChestScreen.cs:750 | PV fallback + ID | `s.Form?.ToUpperInvariant()` on a v1 share code | STALE-ish | P2-1 |
| Game1.cs:633 | ID | `(string?)s.Form` from `save.WovenSkills` into `Restore` | OK — migration seam | keep |
| SaveGame.cs:316-317 | ID | `SavedSkill.Form` "WRITTEN NO MORE" | OK — save format | keep |
| ShareCodes.cs:244-245 | ID | v1 code validation reads `s.Form` | OK — migration | keep |
| LegacySkillForm.cs (whole file) | ID | frozen Form→SkillId map | OK — that is its job | keep |
| PlayerLoadout.cs:254,273,286-287 | ID/CM | `Form` tuple element in `Restore` rows | OK — migration | keep |
| FormHexDiagram.cs:41 | ID | `internal static class FormHexDiagram` | STALE name | rename → `StyleAffinityDiagram` (§7) |
| FormHexDiagram.cs:321-326 | AS | `icon_form_strike/trap/mark/projectile/aura/transformation` | OK — asset file names; mapping comment at :316 explains | P2-2 (move mapping) |
| BuildScreen.cs:84,178,650,1385,1394 | ID | `_attuneForm` (type is `Style`) | STALE name | rename `_specStyle` |
| BuildScreen.cs:18,53,657,726,743,771,826,1279,1282,1350,1575,1578,1647,1744,1778,1779,1782 | CM | "Source/Form/Vow", "the Form discipline", "six Form nodes" … | 657-661/743/771/1575-1578 OK (history); **18, 1279, 1282, 1350, 1744, 1778-1782 STALE** (present tense: "the attuned Form's name", "six Form names", "carries the name of its Form") | reword to Style when the class is renamed |
| ChestScreen.cs:748 | CM | "a v1 code still speaks the legacy Source x Form pair" | OK | — |
| ForgeScreen.cs:959 | CM | "the Form-combo, the build-defining roll" | STALE (enchant needs are Style/Kind/Amplify now, `Enchantments.cs:187-199`) | reword "style-combo" |
| Game1.cs:1523,1950,1951,2839,2886 | CM | "asks about Forms", "which Forms the build runs" | 1523/1950-1951 OK (history); **2839, 2886 STALE** present tense | reword |
| SoloExpeditionScreen.cs:314,850,1246,1355,1365,1368,2290,2842,2846,2862,2883,2888,2990,2991,3006,3014,3037,3072,3252,3317,3328,3354,3638 | CM | 23 remarks: "each Form's medallion", "its Form", "the Form's rule", "Source+Form label", "EACH FORM THROWS ITS OWN SHAPE", "The effect key for a Form" | 850, 1246, 2888, 3014, 3072, 3328, 3354 OK (history); **the other 16 STALE** — doc comments on live members (`:314` `_skillFlash`, `:2842-2846` `DrawSkillDock` summary, `:3317` effect-key doc, `:3252` clip naming) describe them in Form terms | reword "skill" |
| VfxPlayer.cs:14 | CM | "a burst on a Form's cast" | STALE | reword |
| WeaveScreen.cs:29,252,575,986,987,994,1019,1164,1254,1301-1303,1308,1475,1493 | CM | "one Form", "a Source/Form was set", "the Form cell", "Form combos", "SOURCE and FORM grids" | 986-994, 1019 OK (history); **29, 252, 575, 1164, 1254, 1301-1308, 1475, 1493 STALE** — describe live methods (`GearWants`, `PickCell`) in Form terms | reword |
| Core CM: Build.cs:117,156,159,206-207,242-243; BuildComposer.cs:150; BuildGlossary.cs:9,13; GearShape.cs:7,13; MasteryCatalog.cs:58-59,73,92,109,419; MasteryLayout.cs:224; MasteryTree.cs:40,198; PlayerLoadout.cs:17,34-35,66; SkillCatalogue.cs:14,21-22,81,116,165-173,397,513,643; SkillShape.cs:71,75,138,145-155; SoloBattle.cs:346-347,403,471-472,714-715,723,777,1194,1229,1345,1380,1417,1424,1475,1616,1628-1629,1829,1844; StyleAffinity.cs:10,13; Vows.cs:9,60-61; Character.cs:157,162,165,181; CharacterRoster.cs:174; Enchantments.cs:49,51,73,98,222,296,306; ItemFamilies.cs:33,43; BandCycles.cs:89,98; WaveModel.cs:301; WaveReplay.cs:165,174,185; Reforge.cs:22; MemoryDust.cs:454; TraitRoads.cs:28; Unlocks.cs:130,224 | CM | — | Mostly OK (they narrate the port). **Present-tense STALE worth fixing when touched:** Build.cs:117,156,242-243; GearShape.cs:7,13; MasteryCatalog.cs:109,419 ("FORM SPECIALISATIONS"); MasteryTree.cs:40; PlayerLoadout.cs:17,66; SkillShape.cs:71,75,138,145-155; SoloBattle.cs:346-347,403,471-472,714-715,723,1829; Character.cs:157,165,181; Enchantments.cs:49,51,73,222,306; ItemFamilies.cs:33,43; BandCycles.cs:89,98 | reword to Style/skill |

### 6b. WEAVE / WOVEN / WEAVING

| File:line | Class | Text / identifier | Verdict | Action |
|---|---|---|---|---|
| WeaveScreen.cs:926 | PV | `WHAT YOU ARE WEAVING` | STALE | 3a |
| WeaveScreen.cs:860 | PV | `...THEN WOVEN INTO A SLOT` | STALE | 3a |
| WeaveScreen.cs:647, 691 | PV | `SLOT UNWOVEN.` / `SLOT WOVEN.` | STALE | 3a |
| Game1.cs:5041 | PV | `{n} WOVEN SKILLS` | STALE | 3a |
| BuildScreen.cs:1519 | PV | `WEAVE IT ON THE BUILD SCREEN (B)` | STALE | 3a |
| ChestScreen.cs:742 | PV | `NONE WOVEN` | STALE | 3a |
| Unlocks.cs:254 | PV | `The full weave.` | STALE | 3a |
| MasteryCatalog.cs:189 | PV | `EVERY WOVEN SKILL` | STALE | 3a |
| CharacterRoster.cs:162 / Quests.cs:203 | PV | `VOLLEY skills woven` | STALE | 3a |
| Keystones.cs:183; MemoryDust.cs:468-470 ("KEYSTONE — WEAVER", "the keystone WEAVER"); PrestigeScreen.cs:333,851 (`art_terminal_weaver`, `ks_weaver`) | PV/AS | keystone proper noun | OK — content name | keep (blurb is P0-7) |
| WeaveScreen.cs:37 | ID | `public sealed class WeaveScreen` | STALE name | → `LoadoutScreen` |
| WeaveScreen.cs:263 | ID | ctor | — | follows class |
| Game1.cs:317-318 | ID | `_weave`, `_showWeave` | STALE | → `_loadout` conflicts with `PlayerLoadout _loadout`; use `_loadoutScreen`, `_showLoadout` |
| Game1.cs:1063,1181-1182,1318-1319,2048-2050,2098,2129-2131,2479-2533,2571-2573,2772-2787,3317,4041,4097,5224-5234,5271,5292 | ID | uses of the two fields | — | follow rename |
| Game1.cs:477, 1490, 2048 | AS | fixture mode `"weave"` | STALE | → `"build"` once the dead overview mode is deleted; keep `"weave"` as alias for one release so `capture.sh`/baseline names still work |
| Game1.cs:1318 | AS | `BootCheckScreens` `("weave", ...)`; `:1317` `("build", ...)` opens the dead overview | STALE | rename entries `build`/`mastery`; the old `build` entry should set `ShowTree = true` |
| BuildScreen.cs:99, 595, 600, 2571-2573 (Game1) | ID | `WantsWeave` | STALE (dead overview's door) | delete with P0-8 |
| BuildScreen.cs:206, 595, 700 | ID | `WeaveBtn` | STALE | delete |
| Game1.cs:625,631,633,882 / SaveGame.cs:152-153 | ID + save JSON | `WovenSkills` | OK — **save-format property name**; renaming changes the JSON key | keep, or rename with `[JsonPropertyName("WovenSkills")]` |
| Game1.cs:2932 / Tutorial.cs:57,147,182 | ID | `TutorialFacts.SkillsWoven` | STALE name | → `SkillsChanged` (it is `BuildDiffersFromStarter() ? 1 : 0`, `Game1.cs:2930-2932`) |
| Tutorial.cs:39,128,147,172,242,271,324 | ID | `TutorialStep.WeaveBuild` | STALE name; **persisted as a NAME** in `DismissedGuideRungs` (`SaveGame.cs:197`) | rename → `ChooseBuild` only with a read-alias for the old name |
| Vows.cs:70,275,378 / WeaveScreen.cs:550 | ID | `VowDemand.EveryWeaveFilled` | STALE name | → `EverySlotFilled` (not persisted — vows persist by `VowId`) |
| Vows.cs:187-200,369; SoloBattle.cs:477,1365,1499,1856,2033,2047,2064; Career.cs:83; WeaveScreen.cs:578-579; Game1.cs:3717 | ID | `WeaveContext`, `SkillsWoven` | STALE name | → `BuildContext`, `SkillsEquipped` |
| Build.cs:389,408; BuildComposer.cs:171 | ID | `Build.Weave()`, `Unweave()` | STALE verb | → `Equip()`, `Unequip()` |
| PlayerLoadout.cs:335; WeaveScreen.cs:1317; Enchantments.cs:113-140 (`woven` params) | ID | `WovenDefs()` | STALE | → `EquippedDefs()` |
| Build.cs:163; Keystones.cs:186; SoloBattle.cs:403-404,1636-1654 | ID | `BuildTrigger.Weaver`, `WeaverEchoFraction`, `woven`/`wovenIdx` locals | OK — keystone proper noun | keep (locals → `echo`) |
| DustEffects.cs:194,215; MemoryDust.cs:314-318; TraitTreeLayout.cs:107; Game1.cs:1583-1584,2052,2063; SoloExpeditionScreen.cs:2855 | AS | trait id `weave_5` | OK — save id, player name is FIFTH SKILL SLOT | keep |
| WeaveScreen.cs:622,805; make_sfx.py:205-216 | AS | `sfx_weave` | OK | keep |
| Onboarding.cs:51 | CM | "BUILD (the weave)" | STALE | reword |
| BuildScreen.cs:18,53,98,247,472,605,671,726,734-735,811,1376,1576,1647; CharacterScreen.cs:65,380,404,414; FormHexDiagram.cs:14; Game1.cs:249,629,637,2486,2503,2517,2786,2911,2925,2930,2936,2983,2994,3612,4452,5035,5055-5056,5226-5227,5233,5288; SoloExpeditionScreen.cs:789,834,848,3037; WeaveScreen.cs:18,51,135,212,315,344,1063,1307,1329 | CM | "the woven loadout", "the weave editor", "this game's language is the weave", "the Weave screen" | STALE in the sense that they name a screen/verb the player no longer meets | reword in the rename pass; no urgency |
| Core CM: Build.cs:206-271,294,316,345,348,396; BuildComposer.cs:13,25,33,37,54,136,145,153,170; BuildGlossary.cs:13; GearShape.cs:12,14; MasteryLayout.cs:222; MasteryTree.cs:226; PlayerLoadout.cs:13,31,49,61-63,106,113,154,238,331; SkillCatalogue.cs:133,255; SkillShape.cs:81; SoloBattle.cs:153,167,586,603,609,616,747,802,1197,1211; SoloExpedition.cs:155,202; Descent.cs:69; HunterProgression.cs:249; LootSystem.cs:34; SaveGame.cs:24,152,163,303,313; ShareCodes.cs:48,92; Unlocks.cs:221; Quests.cs:22,101; Character.cs:121 | CM | — | OK / low-value | reword opportunistically |

### 6c. APTITUDE · DISCIPLINE · ATTUNE

| File:line | Class | Text | Verdict | Action |
|---|---|---|---|---|
| RosterScreen.cs:296, 304 | CM | "Lean and aptitude", "the BEST AT row died with the aptitude" | :304 OK (history); :296 STALE (row is gone) | reword |
| Character.cs:66,124; BuildComposer.cs:18,105; GearShape.cs:12; SkillShape.cs:145-155; PlayerLoadout.cs:225; CharacterRoster.cs:174,201; ItemClasses.cs:107 | CM | "a character's APTITUDE lives here", "the Mark aptitude" | STALE on live field `StylePower` | P2-4 |
| BuildScreen.cs:666,1132,1499-1504,1542,1587; WeaveScreen.cs:40-43 (`Discipline` property),866,869; ChestScreen.cs:730,735-736; Game1.cs:2782; Onboarding.cs:64,275-276; MasteryCatalog.cs:179 | PV/ID | DISCIPLINE | STALE as *one of four* synonyms | 3b — one word |
| Core CM Build.cs:377; MasteryTree.cs:89,197,277; MasteryLayout.cs:221; MasteryPoints.cs:15; SkillShape.cs:71; SoloBattle.cs:1504; StyleAffinity.cs:11,12,30,42; MasteryCatalog.cs:456 | CM | "discipline" (the Style) | OK | follow 3b when touched |
| SkillCatalogue.cs:63; ChestDossier.cs:24; ShareCodes.cs:134; Unlocks.cs:81 | CM | "discipline" in its ordinary English sense | OK — not the concept | — |
| BuildScreen.cs:581,588,1295,1310,1386; :83-87,173-178,467-469,501-503,547,574-588,647-650,1004,1372-1404 (`_attuneNodeId`, `CeremonyOpen`, `DevAttune`, `AttunePanel/SealBtn/UndoBtn`, `DrawAttunement`); Game1.cs:1490,1559,1608-1622,2459-2465,2481,2497,2520 (`attunementHolds`, fixtures `attune`/`attuned`) | PV/ID/AS | ATTUNEMENT (Style specialisation) | STALE synonym | 3b; identifiers → `_specNodeId`, `SpecialisationOpen`, `DevSpecialise`, `SpecPanel`, `DrawSpecialisation`, fixtures `specialise`/`specialised` (alias old) |
| PrestigeScreen.cs:534-538 | PV | subtitle `· ATTUNED` when tree complete | STALE (third meaning) | 3c |
| DustEffects.cs:176,193; MemoryDust.cs:538; TraitTreeLayout.cs:109 | AS | trait id `attunement` (name "A MARK — NO EFFECT") | OK — save id | keep id; rename display (P2-3) |
| FormHexDiagram.cs:14,33,60 (`NativeBed` "attuned seal") | CM | — | STALE | reword with class rename |
| Onboarding.cs:64 | CM | "The attunement hex: six specialisations, one discipline" | STALE | reword |
| GearTraits.cs:8,97,242,283 (`GearTrait.Attuned` "Skills come faster") | PV/ID | — | OK — gear trait proper noun (different concept) | keep |
| HunterProgression.cs:434-436 `FocusAttunement`; Gear.cs:137-138 | ID/CM | — | OK — Focus-slot skill rate | keep |
| Chest.cs:36; ChestDossier.cs:42,102; GiftChests.cs:153; LootSystem.cs:68,170,283; MergeRecipe.cs:68,72 | PV/CM | items "attuned" to an element | OK — loot vocabulary | keep |

### 6d. HEX · MARK/AMPLIFY · stale screen names

| File:line | Class | Text | Verdict | Action |
|---|---|---|---|---|
| Game1 `DrawHexNav`; UiKit.Hex; `ui_slot_skill_hex`; BuildScreen `HexPanel/HexRadius/HexLabelGap/HexSealPx`; WarrenScreen.cs:262-287; PrestigeScreen.cs:1124-1129; FormHexDiagram.cs:183,312 | ID/AS | hexagon shape words | OK — geometry, not the Form concept | only `FormHexDiagram` renames |
| MasteryCatalog.cs:441 `"spec_mark"` id + label `MARK SPECIALIST` | AS + PV | — | id OK (persisted in `MasteryTaken`), **label STALE** | 3d |
| BuildGlossary.cs:53; ElementSets.cs:90; CharacterRoster.cs:199 | PV | MARK as the effect noun | OK | keep (3d) |
| Enchantments.cs:166, 193 | PV | `AMPLIFY LASTS LONGER`, `AN AMPLIFY WINDOW` | STALE (engine word; brief §3e "no jargon") | 3d |
| MemoryDust.cs:538 | PV | `A MARK — NO EFFECT` | collides after 3d | P2-3 |
| SkillCatalogue.cs:86,199-205; SkillShape.cs:224-226; SoloBattle.cs:776-789,1334-1356,1421-1447; Enchantments.cs:108,122 | ID | `SkillEffect.Amplify`, `Amplify*` dials | OK — code word | keep |
| PrestigeScreen.cs:291-297,359-400 (`SocketKind.Mark`, `UnlockEffect.Amplifier` used only to classify) | ID/CM | — | OK — never shown (`:291` says so) | keep |
| Onboarding.cs:213 "a gold NEW mark"; Game1.cs:5415; CharacterScreen.cs:784-830 "MARKS" (badges) | PV/CM | ordinary English | OK | — |
| WeaveScreen (BUILD), BuildScreen (MASTERY TREE + dead overview), FormHexDiagram, PrestigeScreen (TRAITS), CharacterScreen (CHAMPION GEAR), ChestScreen (THE VAULT), StatsScreen (STATS), SoloExpeditionScreen (HUNT) | ID | class names vs `ScreenTitle` at WeaveScreen.cs:858, BuildScreen.cs:685/1125, PrestigeScreen.cs:530, CharacterScreen.cs:445, ChestScreen.cs:365, StatsScreen.cs:170 | STALE | §7 |
| Activity.Stats (Unlocks.cs:19), Nav label "STATS" (Game1.cs:5151), Unlocks.Headline "STATS — TRAIN YOUR CHAMPION" (:200), Tutorial.cs:254 "Press V for STATS", help "STATS — SPEND GLEAM" (Game1.cs:5061) | PV/ID | STATS | brief §38: semantic problem — screen is TRAINING (its panel title already says TRAINING, `720/stats.png`) | rename to TRAINING; `Activity` names are **persisted** in `ExplainedScreens` (`SaveGame.cs:215`) — needs a read-alias |

**Totals (Game assembly):** Form/Forms 8 player-visible stale (4 unreachable) + 3 identifiers OK + 69 comments (≈35 present-tense stale) · Weave/Woven 7 player-visible stale + ~14 identifiers + ~60 comments · Aptitude 2 comments · Discipline 15 player-visible · Attune 6 player-visible + 12 identifiers · stale class names 8.

---

## 7. RENAME MAP (brief §67)

### 7a. Classes and files

| Today | Draws / owns | Proposed | Notes |
|---|---|---|---|
| `WeaveScreen.cs` | screen titled BUILD (loadout + skill tree + vows) | `LoadoutScreen.cs` | brief §67's word; screen title stays BUILD, rail tile BUILD, `Activity.Build` |
| `BuildScreen.cs` | MASTERY TREE + dead overview | `MasteryScreen.cs` | delete the overview first (P0-8) so the file is one screen; `DevOpenTree`, `ShowTree` become plain state |
| `FormHexDiagram.cs` | Style affinity ring (`StyleAffinity.Factor`) | `StyleAffinityDiagram.cs` (or `StyleRingDiagram`) | passes §67's "truly represents Style" test; move `IconKey(Style)` mapping into it under a Style-named method |
| `PrestigeScreen.cs` | TRAITS | `TraitsScreen.cs` | |
| `CharacterScreen.cs` | CHAMPION GEAR | `GearScreen.cs` | brief §60-65 calls it GEAR; consider screen title `GEAR` to match the rail |
| `ChestScreen.cs` | THE VAULT | `VaultScreen.cs` | |
| `StatsScreen.cs` | STATS (brief §38 → TRAINING) | `TrainingScreen.cs` | title, rail label, `Activity.Stats`→`Training` (save alias), `Unlocks.Headline`, `Tutorial.cs:254`, help row |
| `SoloExpeditionScreen.cs` | HUNT | `HuntScreen.cs` | 3,803 lines; rename only, no split in this pass |
| `MapScreen`, `RosterScreen`, `WarrenScreen`, `ForgeScreen` | match their titles | keep | |
| Core `MemoryDustTree`, `MemoryDust.cs`, `MemoryDustText.cs`, `DustEffects.cs`, `MemoryDustUnlock` | the TRAITS tree | `TraitTree`, `Traits.cs`, `TraitText.cs`, `TraitEffects.cs`, `TraitUnlock` | brief §88 "old Dust terminology"; **the currency Memory Dust is still real** (`Game1.cs:4881`, Warren facilities) — only the *tree* is misnamed. `TraitRoads.cs`/`TraitTreeLayout.cs` already use the right word |

### 7b. Game1 fields, enums, identifiers

| Today | Proposed | Persisted? |
|---|---|---|
| `_weave`, `_showWeave` | `_loadoutScreen`, `_showLoadout` | no |
| `_buildScreen`, `_showBuild` | `_masteryScreen`, `_showMastery` | no |
| `_prestige`, `_showPrestige` | `_traits`, `_showTraits` | no |
| `_character`, `_showCharacter` | `_gear`, `_showGear` | no |
| `_chests`, `_showChests` | `_vault`, `_showVault` | no |
| `_stats`, `_showStats` | `_training`, `_showTraining` | no |
| `BuildScreen.WantsWeave`, `WeaveBtn`, `AuraCard`, `DrawSummary/DrawCore/DrawAuraCards/DrawPassives/Sentences` | delete | — |
| `BuildScreen._attuneNodeId/_attuneForm/CeremonyOpen/DevAttune/AttunePanel/AttuneSealBtn/AttuneUndoBtn/DrawAttunement`; `Game1.attunementHolds` | `_specNodeId/_specStyle/SpecialisationOpen/DevSpecialise/SpecPanel/.../DrawSpecialisation`; `ceremonyHolds` | no |
| `WeaveScreen.Discipline` (property, `Game1.cs:2782`) | `ChosenStyle` | no |
| `TutorialStep.WeaveBuild` | `ChooseBuild` | **yes** — `DismissedGuideRungs` stores names (`SaveGame.cs:197`); add `"WeaveBuild"` → new name on read |
| `TutorialFacts.SkillsWoven` | `SkillsChanged` | no |
| `Activity.Stats` | `Activity.Training` | **yes** — `ExplainedScreens` stores names (`SaveGame.cs:215`); alias on read. Also `tools/check_nav_gates.py:57` parses `Activity.<name>` from `Game1.NavActivity` — update the tool or it will fail the build |
| `VowDemand.EveryWeaveFilled` | `EverySlotFilled` | no (vows persist by id) |
| `WeaveContext` / `.SkillsWoven` | `BuildContext` / `.SkillsEquipped` | no |
| `Build.Weave()/Unweave()`, `PlayerLoadout.WovenDefs()` | `Equip()/Unequip()`, `EquippedDefs()` | no |
| `SaveGame.WovenSkills` | keep, or `EquippedSkills` + `[JsonPropertyName("WovenSkills")]` | **yes** — JSON key |
| `SavedSkill.Form`, `LegacySkillForm` | keep | migration seam |

### 7c. Fixtures, tools, tests that name the old things

- `Game1.cs` `RH_SHOT_MODE` handlers: `weave` → `build` (P0-8 frees the name); `build` (dead overview) → delete; `buildtree`/`buildzoom` → `mastery`/`masteryzoom`; `attune`/`attuned` → `specialise`/`specialised`; `dust`/`traitlit`/`traitterm` → `traits`/...; `character` → `gear`; `stats` → `training`. Keep the old strings as aliases in the `sm is ...` switches for one release; `capture.sh:11-13` mode list and the baseline PNG names (`weave.png`, `buildtree.png`, `dust.png`, `character.png`, `stats.png`) follow.
- `Game1.cs:470-484` `ShotMode` tour map (`Activity.Build => "weave"`, `Activity.Traits => "dust"`, `Activity.Gear => "character"`) and `BootCheckScreens` (`:1305-1320`) — same renames.
- `tools/check_mouse_space.py:255` resolves `GAME / (type_name + ".cs")` from the class name — file and class must rename together (they do today).
- `tools/check_nav_gates.py:17-18,57` reads `Game1.cs` + `Unlocks.cs` and validates `Activity.<name>` — update if `Activity.Stats` renames.
- `tools/asset-pipeline/build_spec.py:243,624,714,736,788,803,820,840-841`, `tools/check_asset_keys.py:126-134`, `tools/asset-pipeline/make_sfx.py:17`, `tests/.../MasteryLayoutTests.cs:15` — comment mentions of `WeaveScreen`/`BuildScreen`/`PrestigeScreen`/`ChestScreen`; `check_asset_keys.py:134` prints "ChestScreen.cs" in a diagnostic string. Update text.
- Tests that pin copy: `TraitNamesTests.cs:45-60` (old metaphors forbidden — no collision with any proposal), `TraitDescriptionsTests.cs:24`, `MemoryDustTextTests.cs:142` (forbids " PT ", "AMP ", "CDR", "DPS"… — "MARKS LAST LONGER" passes; "AMPLIFY" was never forbidden but "AMP " is, so the direction agrees). `TriggerLivenessTests.cs:473` message text mentions WEAVER (proper noun, fine).

---

## 8. EXACT COPY REPLACEMENTS (one table to hand to the implementer)

| File:line | Before | After | Data honesty |
|---|---|---|---|
| Game1.cs:5041 | `WOVEN SKILLS` | `SKILL SLOTS` | `PlayerLoadout.SkillCapacity` (live) |
| Game1.cs:5042 | `SOURCE x FORM x VOW` | `STYLE → SKILL → VARIATION → REINFORCEMENTS` | model, not a number |
| Game1.cs:5046 | `FORM IS HOW YOU FIGHT` | `A SKILL IS HOW YOU FIGHT` | — |
| Game1.cs:5065 | `("T", "BUILD, THE SAME PLACE AS B")` | delete row (or `("T", "BUILD — TOGGLE")`) | `Game1.cs:2520-2535` |
| BuildScreen.cs:1398 | `THE FAR FORMS HIT SOFTER —` | `THE FAR STYLES HIT SOFTER —` | `StyleAffinity.Factor` |
| BuildScreen.cs:1400 | `A VOW ON A FAR-FORM SKILL PULLS IT ONE RING CLOSER.` | `A VOW ON A FAR-STYLE SKILL PULLS IT ONE RING CLOSER.` | `Factor(vowSworn:true)` |
| BuildScreen.cs:1386 | `THE ATTUNEMENT` | `YOUR SPECIALISATION` | — |
| BuildScreen.cs:1387 | `SIX STYLES ON THE LOOM — ONE IS YOURS.` | `SIX STYLES — ONE IS YOURS.` | drops the loom metaphor |
| BuildScreen.cs:1403 | `SEAL IT — HAMMER IS MINE` | `CHOOSE HAMMER` | — |
| BuildScreen.cs:581 | `ATTUNED. {S} IS YOURS — ITS SKILLS HIT TWICE AS HARD.` | `{S} IS YOUR STYLE — ITS SKILLS HIT TWICE AS HARD.` | Factor = 2.00 |
| BuildScreen.cs:588 | `THE POINTS ARE BACK. ATTUNE WHEN YOU ARE READY.` | `THE POINTS ARE BACK. CHOOSE A STYLE WHEN YOU ARE READY.` | — |
| BuildScreen.cs:666, 1542 | `YOU ARE ALREADY ATTUNED — ONE DISCIPLINE PER HUNTER.` / `CLOSED — YOU ALREADY HAVE A DISCIPLINE` | `YOU ALREADY CHOSE A STYLE — ONE STYLE PER HUNTER.` / `CLOSED — YOU ALREADY CHOSE A STYLE` | `MasteryTree.CanTake` rule |
| BuildScreen.cs:1132 | `FOUR DIRECTIONS · ONE DISCIPLINE · TWELVE SKILLS TO LEARN` | `FOUR DIRECTIONS · ONE STYLE · TWELVE SKILLS TO LEARN` | — |
| BuildScreen.cs:1295 | `YOUR ATTUNEMENT` | `YOUR STYLE` | — |
| BuildScreen.cs:1310-1311 | `UNATTUNED` / `TAKE A SPECIALISATION NODE` | `NOT CHOSEN` / `TAKE A SPECIALISATION NODE` | — |
| BuildScreen.cs:1499-1504 | `{f} IS YOUR DISCIPLINE...` / `TAKING IT MAKES {f} YOUR DISCIPLINE...ONE DISCIPLINE PER HUNTER.` / `YOUR DISCIPLINE IS ALREADY {m}. ONE DISCIPLINE PER HUNTER —` | `{f} IS YOUR STYLE...` / `TAKING IT MAKES {f} YOUR STYLE... ONE STYLE PER HUNTER.` / `YOUR STYLE IS ALREADY {m}. ONE STYLE PER HUNTER —` | — |
| BuildScreen.cs:1519 | `WEAVE IT ON THE BUILD SCREEN (B).` | `EQUIP IT ON THE BUILD SCREEN (B).` | — |
| BuildScreen.cs:1587 | `SPECIALISATION — YOUR DISCIPLINE` | `SPECIALISATION — CHOOSES YOUR STYLE` | — |
| WeaveScreen.cs:860 | `A SKILL IS LEARNED ON THE MASTERY TREE, THEN WOVEN INTO A SLOT` | `LEARN A SKILL ON THE MASTERY TREE, THEN EQUIP IT IN A SLOT` | — |
| WeaveScreen.cs:866 | `DISCIPLINE: {S} — ITS SKILLS HIT TWICE AS HARD · A VOW ON A FAR-STYLE SKILL PULLS IT ONE RING CLOSER` | `YOUR STYLE: {S} — ITS SKILLS HIT TWICE AS HARD · A VOW ON A FAR-STYLE SKILL PULLS IT ONE RING CLOSER` | Factor |
| WeaveScreen.cs:869 | `NO DISCIPLINE YET — A SPECIALISATION NODE ON THE MASTERY TREE (E) GIVES YOU ONE` | `NO STYLE CHOSEN YET — A SPECIALISATION NODE ON THE MASTERY TREE (E) CHOOSES ONE` | — |
| WeaveScreen.cs:926 | `WHAT YOU ARE WEAVING` | `YOUR LOADOUT` | — |
| WeaveScreen.cs:647 / 691 | `SLOT UNWOVEN.` / `SLOT WOVEN.` | `SLOT CLEARED.` / `SKILL EQUIPPED.` | — |
| ChestScreen.cs:735-736 | `DISCIPLINE: {S}` / `NO DISCIPLINE CHOSEN` | `STYLE: {S}` / `NO STYLE CHOSEN` | — |
| ChestScreen.cs:742 | `NONE WOVEN` | `NO SKILLS` | — |
| ChestScreen.cs:750 | `{SOURCE} {FORM}` | skill name via `LegacySkillForm` map, else `UNKNOWN SKILL` | `LegacySkillForm.cs:26-35` |
| PrestigeScreen.cs:538 | `· ATTUNED` | `· ALL FOUR ROADS WALKED` | `DustEffects.TreeComplete` |
| Keystones.cs:184 | `EVERY SKILL ALSO FIRES AS THE NEXT FORM YOU CARRY, AT 45%. SKILLS RETURN 30% SLOWER.` | `EVERY SKILL ALSO FIRES THE NEXT SKILL IN YOUR BUILD, AT 45%. SKILLS RETURN 30% SLOWER.` | `WeaverEchoFraction`, `SkillRate 0.70` |
| MasteryCatalog.cs:179 | `NARROW — YOUR DISCIPLINE'S FORM +20%, EVERY OTHER FORM -10%` | `NARROW — YOUR OWN STYLE +20%, EVERY OTHER STYLE -10%` | `AffinityStyleBonus`, `OffStylePenalty` |
| MasteryCatalog.cs:181 | `BROAD — HALF OF THE OPPOSITE FORM'S PENALTY IS GIVEN BACK` | `BROAD — HALF OF THE OPPOSITE STYLE'S PENALTY IS GIVEN BACK` | `OppositePenaltyRelief` |
| MasteryCatalog.cs:189 | `PURE — WHILE EVERY WOVEN SKILL SHARES ONE SOURCE, ...` | `PURE — WHILE EVERY EQUIPPED SKILL SHARES ONE SOURCE, ...` | `OneSourceBonus` |
| MasteryCatalog.cs:441 | `MARK SPECIALIST — THE WINDOW STRETCHES` | `SIGN SPECIALIST — MARKS LAST LONGER` | `AmplifyWindowMultiplier 1.3` |
| Enchantments.cs:166 | `AMPLIFY LASTS LONGER` | `MARKS LAST LONGER` | Linger |
| Enchantments.cs:193 | `AN AMPLIFY WINDOW` | `A SKILL THAT MARKS` | `AnyAmplify` |
| Unlocks.cs:254 | `A FOURTH SKILL. The full weave. ...` | `A FOURTH SKILL. A full build. ...` | — |
| CharacterRoster.cs:162 / Quests.cs:203 | `Clear 150 waves with VOLLEY skills woven` | `Clear 150 waves with a VOLLEY skill equipped` | `Quest.Style` tally (live) |
| Onboarding.cs:275-276 | `ONE DISCIPLINE` / `A specialisation is your discipline: that STYLE's skills hit twice as hard, and its road teaches the style's two skills — learned for good.` | `ONE STYLE` / `A specialisation chooses your Style: that Style's skills hit twice as hard, and its road teaches the Style's two skills — learned for good.` | Factor; `MasteryKind.SkillRoad` |
| MemoryDust.cs:538 | `A MARK — NO EFFECT` | `A KEEPSAKE — NO EFFECT` | id unchanged |

Nothing in this table needs a value the Core does not already compute.

---

## 9. FIXTURES (brief §95) — what exists, what this area needs

**Exists (Game1 `RH_SHOT_MODE`, `capture.sh:11-13`):** `weave` (BUILD: 4 slots incl. a fifth-slot purchase, all four skill-level states, MET + UNMET vow — `Game1.cs:2048-2131`; this already satisfies §95 "BUILD (4 skills, variation, reinforcement, valid/invalid Vow)"), `buildtree`/`buildzoom` (MASTERY: 24 points, Resonance minors+notables, no specialisation — `:1559-1630`), `attune` (THE ATTUNEMENT ceremony posed on Hammer) and `attuned` (spec_strike + road_hammer taken), `help` (F1 panel), `dust` (TRAITS), `vault`/`vaultfirst`, `character`, `stats`, `fight`, `map`, `roster`/`rosterlocked`, `forge`, `warren`, `runlog`, `fightreport`, `settings`. Baseline PNGs exist for all of these **except `attune` and `attuned`** — the two poses that carry P0-3/P0-4 and the four-synonym problem.

**Gaps for this area:**
1. Add `attune` and `attuned` (renamed `specialise`/`specialised`) to the baseline set — the ceremony modal and the "chosen" state of the right-hand Style panel are where DISCIPLINE/ATTUNEMENT/FORM copy lives.
2. A `mastery` fixture that satisfies §95 in one shot: points available **and** a SkillRoad node taken (permanent discovery, `MasteryTree.LearnedSkills()`) **and** a notable **and** a capstone. Today `buildtree` has no road and no capstone; `attuned` has a road but is posed at 80 points.
3. A fixture that opens the **A FRIEND'S BUILD** modal (`ChestScreen.cs:720-760`) with a v2 code and with a v1 (Form-era) code — P2-1 is otherwise unphotographable. No handler exists (`Game1.cs:1490` list has `trader` but no `code`/`paste`).
4. A `help` fixture posed on a save with `SkillCapacity = 5` and `KeystoneCapacity = 3`, since the two derived numbers are the only live data on that panel.
5. After renaming, keep the old mode strings as aliases so `tools/asset-pipeline/capture.sh` and the baseline filenames keep working for one release.

---

## 10. SCREENSHOT OBSERVATIONS (vocabulary surfaces, 720 vs 1080)

- `720/help.png`: the "YOUR BUILD" column is seven Secondary-rung lines at ~11 px cap height; `SOURCE x FORM x VOW` and `FORM IS HOW YOU FIGHT` are legible enough to mislead and too small to read comfortably. The column occupies x≈115-720 with ~60% empty vertical space beneath; a three-line model strip (`STYLE → SKILL → VARIATION → REINFORCEMENTS`) at Body would fit without shrinking anything.
- `720/weave.png`: the eye lands first on the gold panel titles — `WHAT YOU ARE WEAVING` is the largest stale word on any live screen. The two subtitle lines at y≈52/69 (`...WOVEN INTO A SLOT`, `NO DISCIPLINE YET...`) are ~9 px tall at 720p and unreadable; at 1080p (`weave.png`) they are readable but sit in Slate on near-black, i.e. they look disabled (brief §7).
- `720/buildtree.png`: `YOUR ATTUNEMENT` panel title is the only place the word appears as a heading; the six ring labels read HAMMER/SNARE/SIGN/VOLLEY/FIELD/DRAIN — i.e. the diagram already speaks Style while its title speaks attunement. The subtitle `FOUR DIRECTIONS · ONE DISCIPLINE · TWELVE SKILLS TO LEARN` is illegible at 720p (~8 px). The "SPECIALISATION" captions under the six diamonds are readable at 1080p, marginal at 720p.
- `720/dust.png`: KEYSTONE — WEAVER is a node name (fine); its detail sentence (P0-7) only appears when hovered — not in the baseline capture. The subtitle line `ONE SPINE · FOUR ROADS · NO TAKING BACK` (which grows `· ATTUNED`) is ~8 px at 720p.
- `720/fight.png`: the rail is clean of legacy words; skill names + rhythm + line. Nothing to fix in this area.
- `720/character.png`, `720/forge.png`: enchant rows show `HARVEST — ON KILL: 38% CORE`; the `AMPLIFY LASTS LONGER` / `AN AMPLIFY WINDOW` strings appear only on LINGER items (not in these captures) — data honesty of the finding rests on `Enchantments.cs:166,193` and `ForgeScreen.cs:1726`/`CharacterScreen.cs:1019` drawing `ench.Blurb`.

---

## 11. PROPOSED SURFACES for teaching the current model (1920×1080 logical px)

This area owns no screen, so the "layout" is where the vocabulary is spoken:

- **BUILD header strip** (`WeaveScreen`, y 74-120, x 640-1280): one line, Body rung, Secondary colour that is *not* the disabled grey — `STYLE → SKILL → VARIATION → REINFORCEMENTS` on first visits (Onboarding `explained` gate), replaced after the BUILD tour by the live state line `YOUR STYLE: HAMMER — ITS SKILLS HIT TWICE AS HARD`. REMOVED: the two stacked Secondary lines at y 80 and 108. RENAMED: `WHAT YOU ARE WEAVING` → `YOUR LOADOUT` (panel title, x 220-676, y 130-170).
- **MASTERY right column** (`BuildScreen.HexPanel` 1408,112,496×460): title `YOUR STYLE`; plate reads `NOT CHOSEN` / `HAMMER`; diagram unchanged. RENAMED only.
- **THE SPECIALISATION modal** (`AttunePanel` 480,180,960×720): title `YOUR SPECIALISATION`; body lines at y 784/806 read STYLES not FORMS; buttons `CHOOSE HAMMER` / `NOT YET — TAKE THE POINTS BACK`. RENAMED only; brief §46-style permanent-decision look is *not* wanted here (respec is free — `BuildScreen.cs:607-612`), so no red framing.
- **F1 help** (panel 112,92,1696×840): left column becomes three zones — MODEL (y 200-300: `STYLE → SKILL → VARIATION → REINFORCEMENTS`, `{n} SKILL SLOTS · {k} KEYSTONE SOCKETS`, `A VOW IS A PROMISE ON ONE SKILL`), THE IDEA (existing prose, y 420-540), KEYS (right column unchanged minus the T row). REMOVED: `SOURCE x FORM x VOW`, `FORM IS HOW YOU FIGHT`, `TRAITS (P) SELL MORE OF BOTH`, `T — BUILD, THE SAME PLACE AS B`.
- **TRAITS detail** (`720/dust.png` right panel 940-1235 × 95-555): no layout change; the WEAVER sentence rewrites via `Keystones.cs:184`.
