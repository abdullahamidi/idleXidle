# tests.md — adversarial verification

Verifier pass over `production/audit/refactor-2026-08-31/tests.md` (branch `feat/hunter-cutout-rig`, HEAD `ae30f2a`).
Source was read-only; the only file written is this one. Every line reference below was re-read at HEAD.

Legend: **CONFIRMED** = claim holds as written · **PARTIAL** = direction right, a detail is wrong or the
claim omits a live consumer · **REFUTED** = concrete contrary evidence.

Totals: 40 claims — 20 CONFIRMED, 20 PARTIAL, 0 REFUTED outright (one sub-claim, "check_boot.sh is half-renamed",
is refuted inside claims 15 and 25).

## The one correction that governs the OBSOLETE rows

Every Form-side API the audit marks OBSOLETE[A-delete] is **live in the fight and on a screen today**:

| API the deleted tests pin | Live reader |
|---|---|
| `Build.Affinity` / `FormBehaviour.AffinityFactor` | `SoloBattle.cs:709-711` (Amp), `:787-788` (mark), `:1584-1586`, `:1711-1713` (vow buy-back); `BuildComposer.cs:128` sets it; `FormHexDiagram.cs:156,172`; `WeaveScreen.cs:1108-1109`; `Mastery.Affinity()` in `BuildScreen.cs:644,667,715,803,978,1496,1529`, `CharacterScreen.cs:553`, `SoloExpeditionScreen.cs:2339`, `StatsScreen.cs:226`, `Game1.cs:2769` |
| `ItemFamilies.FavouredForms` → `GearShape.Of` → `SkillShape.FormPowerFor` | `GearShape.cs:25-29` → `SoloBattle.cs:448` → `:811 m *= shape.FormPowerFor(skillForm.Value)` |
| `Character.Aptitude` | `CharacterRoster.cs:73-212` (9 of 10 characters), `Character.cs:207-208` → same `SoloBattle.cs:811`; displayed `RosterScreen.cs:306-309` |
| `Enchantment.NeedsForm` / `EnchantNeed.MetBy` | `ForgeScreen.cs:551` (FITS badge), `WeaveScreen.cs:1362-1363` |
| `BuildGlossary.FormHeadline/FormRule` | `WeaveScreen.cs:1749-1750, 1773-1774` |
| `VowDemand.SingleForm` / `WeaveContext.DistinctForms` | `SoloBattle.cs:2133`, `ResonanceWeaving.cs:505`, `WeaveScreen.cs:579` |
| `BuildTrigger.{Overdraw,Linger,Radiance,Execute,Coiled,Siphon}` | `SoloBattle.cs:639,1261,1382,1438,1507,1551,1613,1730,1909` |
| `SkillCatalogue.Resolve(Form, passive)` (the "bridge") | `Build.cs:256`, `BuildComposer.cs:173`, `FormBehaviour.cs:73`, **13 Game sites**: `Game1.cs:2097`, `SoloExpeditionScreen.cs:866,3010,3356`, `WeaveScreen.cs:691,711,777,1037,1573` |

So the OBSOLETE verdicts are *conditional on a source rewrite that has not happened*. Deleting these tests before (or
separately from) the source change leaves live behaviour unpinned — the exact failure mode this codebase's own headers
warn about. Each such row is PARTIAL below with the rule: **delete in the same commit that removes the reader.**

## Verdict table

| # | Claim | Verdict | Evidence |
|---|---|---|---|
| 1 | `affinity_test.cs` obsolete (Form affinity) | **PARTIAL** | Tests at `:57,:76,:100` use `new Build { Affinity = … }` (`:34,:45`). `Build.Affinity` is read by `SoloBattle.cs:709,787,1584,1711` and eleven Game sites (table above). Live, not dead; delete only with the reader. |
| 2 | `affinity_vow_buyback_test.cs` obsolete | **PARTIAL** | `AffinityFactor(…, vowSworn: true)` is live at `SoloBattle.cs:1585,1712` and shown at `WeaveScreen.cs:1109`. Test `:46 …reaches_the_fight_for_a_sworn_opposite_skill` is a chain test on live code. `SkillCatalogue.RingDistance` exists (`SkillCatalogue.cs:661`), so the re-spec target is real. |
| 3 | `family_form_affinity_test.cs` obsolete | **PARTIAL** | `ItemFamilies.FavouredFormsOf` (`ItemFamilies.cs:61-63`) → `GearShape.Of` (`GearShape.cs:25-29`) → `SoloBattle.cs:448,811`. Live. Test `:39` (each family favours two) is catalogue completeness and survives a Style re-point. |
| 4 | `one_discipline_test.cs` obsolete | **PARTIAL** | The rule under test — refuse a second Specialisation — is `MasteryTree.cs:196` and is read by `BuildScreen.cs:644,667,1529`. Only the `Form.Strike` equality (`:26,:31`) is Form-bound; the refuse/refund assertions pin a live rule. |
| 5 | SoloBattleTests Form-combo + affinity block (~12 of 47) | **PARTIAL** | Affinity `:405,:425` and combo tests `:437,:457,:468,:481,:553,:570,:585,:755` confirmed (10, not ~12). The cited range `:437-600` also contains `test_venom_bleeds_the_enemy_for_extra_damage` (`:504`) and `test_the_venomancer_keystone_earns_its_downside` (`:522`) — Venom keystone tests that must stay. Every combo trigger is live in `SoloBattle.cs` (table above). |
| 6 | WeavingTests power-formula block (4 of 19) | **PARTIAL** | Stronger than claimed for 3 of 4: `Weaving.AbilityPower` has **zero callers in src** (only its definition `ResonanceWeaving.cs:344`; the seven callers are `WeavingTests.cs:207,208,226,227,257,258,265`). But `:272 test_mark_deals_no_direct_damage` pins `BasePower(Form.Mark)==0`, and `BasePower` is live via `FormBehaviour.BaseDamage` (`FormBehaviour.cs:296`) at `SoloBattle.cs:1420,1573,1707,1926`. Detail wrong: `StatsScreen.cs:317-318` mentions BasePower in a **comment**, not a call. Keep-half confirmed: `SoloBattle.cs` uses `Weaving.IsActive/SourceEffectiveness/VowMultiplier` once each. |
| 7 | characters_roster Form-aptitude tests (2 of 16) | **PARTIAL** | `:123` and `:135` confirmed. Aptitude is live (`SoloBattle.cs:811`, `RosterScreen.cs:306`). `:135 test_aptitudes_compound…` never touches a Character — it is a pure `SkillShape.Combine` multiplicativity test and survives whatever the dictionary key becomes. |
| 8 | EnchantmentsTests Form-requirement tests (3 of 15) → replace | **PARTIAL** | Lines `:116,:185,:207` confirmed; category C is right. "Legacy" is wrong: `EnchantNeed.MetBy` (`Enchantments.cs:119-129`) is live at `ForgeScreen.cs:551` and `WeaveScreen.cs:1362-1363`; the six Form needs are `Enchantments.cs:170-175`. |
| 9 | MasteryTreeTests Form rows + total-cost pin | **CONFIRMED** | `:79-88` pins `4*49 + 4*6 + 6*6 + 12*SkillRoadCost == 316`; `:470-482` whitelist; `:484-494` `n.Form`. `MasteryCatalog.cs:542-545 Spec()` sets `{ Form = form }` and a grant. Note road nodes use spec ids as prerequisites (`:460-466`). |
| 10 | Form-bridge migration tests → move to migration fixtures, delete when no save carries a Form | **PARTIAL** | `skill_catalogue_test:167,:184` and `skill_slot_kinds:58,:203` are bridge tests. But `:284` is a slot-budget test, `:446` is a liveness test of the spill rule (WILT breaks attack), and `:597` pins `FormBehaviour.CooldownBeats == def.Beats` — a live Core/Game seam (Game reads `FormBehaviour.CooldownBeats` at 3 sites, `AuraTickMs` 2, `BaseCooldownMs` 2). And the bridge is not save-only: `SkillCatalogue.Resolve(Form,…)` has 13 Game call sites. "Delete once no save can carry a Form" undercounts what moves first. |
| 11 | WeaveInBattle/WeavingTests demand tests key on `VowDemand.SingleForm` | **PARTIAL** | `vow_singular` is `SingleForm` (`ResonanceWeaving.cs:387-388`) — but `vow_pure` is `VowDemand.SingleSource` (`:394-395`), and Source is live. Half of each assertion block (`WeaveInBattleTests:88-89`) is not Form-bound. |
| 12 | build_glossary Form tests (3 of 6) delete | **PARTIAL** | `:56,:79,:90` confirmed. `FormHeadline/FormRule` are live copy at `WeaveScreen.cs:1749-1774`; the tests check that copy against `FormBehaviour` numbers. `skill_catalogue_test:250 test_every_skill_names_its_art_and_says_what_it_does` exists. Delete with the WeaveScreen explanation path, not before. |
| 13 | slot_split_balance_test delete; wave_length/skill_slot_kinds already guard it | **PARTIAL** | Not print-only: `:122 Assert.InRange(Ratio(mixedBefore, mixedAfter), 0.85f, 1.25f)` is a run-depth floor for a 2+2 loadout through the real composer over 40 waves. No beat-demand test measures run depth. Uses shared `Taught.Everything()`. |
| 14 | LootSystemTests AutomationStage tests (2) + FullLoop `AutomationStage = 1` | **CONFIRMED** (extended) | `Kill` fixture `:22`; `:93`, `:134`; `FullLoopTests.cs:111`. Only prod constructor `ExpeditionLoot.cs:93-103` never sets it. Also touched: Theory rows `:37,:56` take `int? automationStage`; `LootTuning.AutomationRarityParityPercent` (`LootSystem.cs:173-178`) and `KillContext.IsAutomated` (`:222`, zero readers anywhere) go with the field. `SaveSystemTests:80,:160` "AutomationStage" is the retired SAVE JSON key — unrelated, keep. |
| 15 | `ResonanceHunter.*` identity in tests/gates; check_boot.sh half-renamed | **PARTIAL** | Counts confirmed: 120 namespace / 424 using. csproj `:24` both. ci.yml lines are `:33` and `:39` (not 29-36). Python paths confirmed at cited lines. **Refuted detail:** check_boot.sh is not half-renamed — `ResonanceHunter.Game.csproj:8 <AssemblyName>IDLExIDLE</AssemblyName>`, so killing `IDLExIDLE` is correct, and `SaveFile.cs:34-35` writes to `LocalApplicationData/ResonanceHunter`, so `:32` reads the real save. |
| 16 | `BuildComposer.SkillPick.SkillId` dead — no test passes it | **CONFIRMED** (extended) | `grep 'SkillId:' src tests` → 0. Composer reads it at `BuildComposer.cs:87,171` but **no production caller passes it either**: `PlayerLoadout.cs:268` builds `SkillPick(s.Source, s.Form, s.VowId, NameOf(s), s.Passive)` and drops `SkillChoice.SkillId` (set at `:197`); `SoloExpeditionScreen.cs:862,2862`, `WeaveScreen.cs:689,704,775,978,1571` all pass 5 args. The id path is dead in prod and tests. |
| 17 | `KillContext.AutomationStage` dead | **CONFIRMED** | `LootSystem.cs:220-222`, read `:386-388`; writers only `LootSystemTests.cs:22`, `FullLoopTests.cs:111`; `SaveGame.cs:53` retired note. |
| 18 | `FullLoopTests.Rng` shared static | **CONFIRMED** | Declared `:30`, used `:44` (audit said 45); second fact `new Random(7)` at `:101` (audit said 102). Off by one, same material. |
| 19 | TriggerLivenessTests hand-listed proof set | **CONFIRMED** | `:115-158`; six combo triggers name SoloBattleTests proofs at `:130-135`. |
| 20 | EnchantmentsTests hand-listed proof set | **CONFIRMED** | `:150-184`; six Form kinds at `:167-172`. |
| 21 | `SkillCasts` decodes Form from `BattleEvent.Amount` | **CONFIRMED** (extended) | `SoloBattleTests.cs:55-56`. Emitted at `SoloBattle.cs:1543,1558,1714,1942`; decoded by the **Game** at `SoloExpeditionScreen.cs:1254 (Form)e.Amount`, `:3277`, `:3280`, and by `beat_cadence_test.cs:79`. A Core→Game contract, not only a test coupling. |
| 22 | MasteryTreeTests whitelist branch | **CONFIRMED** | `:470-482`. |
| 23 | Liveness tests reach skills via `LegacyForm` door | **CONFIRMED** | `variation_liveness:78-93`, `reinforcement_liveness:73-77`. `PlayerLoadout.cs:194-196` does the identical walk in prod — the tests mirror the real door. |
| 24 | hunt_screen_feedback_test literal path | **CONFIRMED** | `:35 Path.Combine(dir, "src", "ResonanceHunter.Game", "SoloExpeditionScreen.cs")`. |
| 25 | Seven gates + check_boot.sh hardcode old dirs; check_boot.sh half-renamed | **PARTIAL** | Paths confirmed: `check_init_order.py:37`, `check_ui_type.py:50`, `check_asset_keys.py:28,118,143`, `check_nav_gates.py:17-18`, `check_mouse_space.py:25`, `check_font_coverage.py:37`, `check_boot.sh:32,34,54,65`. "Half-renamed" refuted — see 15. |
| 26 | ci.yml literal test paths | **CONFIRMED** | `.github/workflows/ci.yml:33` (unit), `:39` (integration). Cited 29-36 misses the second. |
| 27 | Core test reads Game source as text | **CONFIRMED** | `:30-40` walk; `Assert.Contains("ChestCount > 0 && VaultOpen", src)` `:59`; further substrings `:60-95`. |
| 28 | Python gates: "no test project for the Game assembly" | **CONFIRMED** | `check_nav_gates.py:10`, `check_ui_type.py:32`, `check_mouse_space.py:18`, `check_font_coverage.py:28`; run by `check_all.sh:13`. |
| 29 | Event stream shaped by Form ordinal | **CONFIRMED** | Same as 21. |
| 30 | beat_cadence header `:74-76` pins one counter | **PARTIAL** | The pin is the class header `:15-34` ("pinned TOGETHER", `BattleEventKind.Beat`). `:74-76` is the `ActionTimes` helper, which **deliberately re-derives** actions from raw events "so this file can catch the replay drifting" (`:69-72`) — the opposite of "one counter" at that line. |
| 31 | `SavedSkill` save risk — nothing composes the restored build | **CONFIRMED** (extended) | `SaveGame.cs:294-313` (no SkillId field); `SaveSystemTests:317-357` asserts strings only. `Game1.cs:635-637` restores via `(Source, Form, VowId, Passive)`; `PlayerLoadout.SaveSkills` (`:277-278`) drops `SkillId` — the "id-based persistence" comment at `:275` is untrue today. |
| 32 | ShareCodes carry Form; version the prefix | **PARTIAL** | Risk real: `SharedBuild.Skills` is `List<SavedSkill>` (`ShareCodes.cs:48`). But the code is **already versioned** — `BuildPrefix "RHB"` + `Version = 1` + `.` (`:35,:37,:246`), so the work is a v1-tolerant decoder, not a new prefix. Third format missed: `SharedFeedback.Loadout` (`RHF`, `:134`) carries the same list. |
| 33 | Renamed spec ids silently un-take on load | **CONFIRMED** | `MasteryTree.RestoreTaken` (`MasteryTree.cs:172-179`) skips unknown ids; spec ids at `MasteryCatalog.cs:434-446`; also road-node prerequisites `:460-466`; fixtures at 8 test sites incl. `share_codes_test:67`, `SaveSystemTests:325`. |
| 34 | SkillProgress keyed by variation/reinforcement NAME | **CONFIRMED** | `SkillProgress.cs:146-151` (`def.Variations.Any(v => v.Name == variation)`, reinforcements by `r.Name`); `skill_progress_test:113`. |
| 35 | Legacy JSON fixtures are hand-written, keep verbatim | **PARTIAL** | `SaveSystemTests:69-168` and `unlocked_characters_test:23-50` are hand-written JSON. `:537 test_a_legacy_single_region_save_still_loads` is **not** — it serialises `new SaveGame { … }` through `SaveSystem.Serialize` (`:538-541`) and follows any record rename automatically. |
| 36 | Four copies of "restore every SkillRoad node with 9999 points" | **PARTIAL** | Exact copies: `taught_tree.cs:31-39`, `variation_liveness:49-63`, `reinforcement_liveness:49-63`; `skill_slot_kinds:214-217` inline. `mastery_node_liveness TreeWith` (`:65-77`) is a **superset** — `SetEarned(999)` plus caller-supplied ids on top — not replaceable by `Taught.Everything()` without an overload. |
| 37 | `MidCareerHunter()` identical in two files | **PARTIAL** | `heal_balance_test:58` builds `new Hunter(new ProgressionTuning { RegenPerVitalityPoint = 0f })` on purpose (comment `:54-57`); `BalanceSweepTests:91` uses `new Hunter()`. Merging changes the heal probe. |
| 38 | Twelve `Sk(Form…)` factories | **CONFIRMED** | 12 definitions at the cited lines. Signatures vary: `element_sets:55 Sk(Source, Form)`, `SoloBattleTests:25` adds `Vow?`. |
| 39 | Same "2 strengths, 2 weaknesses" assertion in three files | **PARTIAL** | `WeavingTests:20` and `WeaveInBattleTests:30` are the same count. `build_glossary_test:29` asserts `BuildGlossary.StrongAgainst/WeakAgainst` agree with `SourceEffectiveness` — glossary-vs-table, a different assertion. |
| 40 | roster link check vs parity | **CONFIRMED** | `roster_parity_test.cs:23-27` header records THE OATHBOUND passing the link check with half its passive dead. |

## What the auditor missed

1. **Every OBSOLETE row pins live code.** `Build.Affinity`, `AffinityFactor`, `FavouredForms`, `Aptitude`, `NeedsForm`,
   `FormHeadline`, `SingleForm`, the six combo triggers and `SkillCatalogue.Resolve(Form)` are all read by
   `SoloBattle.cs` and/or a Game screen today (table at top). The report's §0/§9 hedges this in prose, but the per-file
   DELETE labels are unconditional. Each needs a "same commit as the reader's removal" gate.
2. **`SkillPick.SkillId` has no production writer.** `PlayerLoadout.ToBuild` (`PlayerLoadout.cs:268`) drops
   `SkillChoice.SkillId`; the composer's id branch (`BuildComposer.cs:87,171`) is dead in prod AND tests. The header
   comment "the id is what actually decides the skill" (`PlayerLoadout.cs:42-50,187-189`) is not true — Form still
   decides. The rewrite's foundation is unproven at both ends.
3. **`SaveSkills` drops `SkillId`** (`PlayerLoadout.cs:277-278`); the "Persistence (id-based…)" header at `:275` is wrong.
   Saves are Form-string based end to end (`Game1.cs:637` restore tuple).
4. **Save directory is a rename hazard the tests do not guard.** `SaveFile.cs:34-35` and `DisplaySettings.cs:146-147`
   write under `LocalApplicationData/ResonanceHunter`. A product-identity sweep that touches that string orphans every
   player's save; only `check_boot.sh:32` pins it, externally.
5. **`vow_pure` is `SingleSource`, not `SingleForm`** (`ResonanceWeaving.cs:394-395`). §9 Q3 lumps it with `vow_singular`.
6. **`BattleEvent.Amount == (int)Form` is a Core→Game contract.** `SoloExpeditionScreen.cs:1254,3277,3280` decode it for
   skill callouts and clip choice. Changing the encoding breaks the hunt screen, not just `SkillCasts()`.
7. **`Weaving.AbilityPower` is already dead in src** (zero callers outside `WeavingTests.cs`). The report calls the
   block "the free Source×Form formula" as if it were live.
8. **`FormBehaviour` is a Game dependency too**: `CooldownBeats` (3 sites), `AffinityFactor` (5), `AuraTickMs` (2),
   `BaseCooldownMs` (2), `ClipShareOfBeat` (2), `SkillClipShareOfBeat` (2), `BaseDamage` (1), `FactorAtDistance` (1).
   `skill_slot_kinds_test:597` pins a live seam, not a bridge.
9. **`KillContext.IsAutomated` (`LootSystem.cs:222`) has zero readers**, and `LootTuning.AutomationRarityParityPercent`
   (`:173-178`) is tuning for the dead branch; Theory rows `LootSystemTests:37,:56` also feed `automationStage`.
10. **Share codes are already versioned** (`RHB1.`, `ShareCodes.cs:37,246`), and `SharedFeedback` (`RHF`) is a third
    Form-carrying wire format (`:134`).
11. **Spec ids are road-node prerequisites** (`MasteryCatalog.cs:460-466`) — renaming `spec_*` also changes which roads
    an old save can hold, on top of the silent un-take.
12. **`SoloBattleTests:504,:522` (Venom / venomancer) sit inside the cited delete range `:437-600`.**
13. **`MasteryTree.RestoreTaken` accepts two specialisations from an old save** (`MasteryTree.cs:250-252` comment;
    `:172-179` validates ids only) — a save-shape edge with no test.
14. **`test_aptitudes_compound…` (`characters_roster_test:135`) has no Character in it** — it is a `SkillShape.Combine`
    test and should be kept under whatever key replaces `FormPower`.
