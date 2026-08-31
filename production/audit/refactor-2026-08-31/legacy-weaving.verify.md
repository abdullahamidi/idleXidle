# Verification — legacy-weaving audit (adversarial pass)

Verifier pass over `legacy-weaving.md`, 2026-08-31, branch `feat/hunter-cutout-rig` (clean). Every verdict
below was checked by grep/read against `src/` and `tests/` (bin/obj/.git excluded). Line numbers are the
ones read in this pass; where the auditor's cite was off, the corrected line is given.

Legend: CONFIRMED = claim and detail hold; PARTIAL = direction right, a detail that would change the edit
is wrong or missing; REFUTED = concrete contrary evidence.

Tally: 38 CONFIRMED, 12 PARTIAL, 0 REFUTED.

## Verdict table

| # | Claim (short) | Verdict | Evidence / correction |
|---|---|---|---|
| 1 | `enum Form` → B-migration-only, keep a private name→SkillId map | **PARTIAL** | Form IS the live runtime identity (Build.cs:223,256; SoloBattle.cs:1236) and the save carries the enum NAME (`PlayerLoadout.SaveSkills` :279 `s.Form.ToString()`, SaveGame.cs:297). But a *name→SkillId* map is the wrong key: a Form name resolves to a **Style** (`SkillCatalogue.Resolve` :653 `All.First(s => s.LegacyForm == form).Style`) and the slot kind picks which of the style's two skills it is (:654; `SavedSkill.Passive` SaveGame.cs:309 + `BuildComposer.SlotKinds` :80-107). `{"Form":"Strike","Passive":true}` must migrate to PRESS, not BLOW. The migration key is (Form, effective slot kind) → SkillId. |
| 2 | `WovenAbility` A-delete; built only at BuildComposer.cs:166; Name read only by Unweave | **CONFIRMED** | `grep -rn "new WovenAbility" src/` → BuildComposer.cs:166 only. `.Ability.*` readers: Build.cs:222,223,240,241; SoloExpedition.cs:212-213 (Form/Source compare on build swap). `EquippedSkill.Name` readers in src: Build.cs:468 only (GiftChests.cs:88 `starter.Name` is a Character). |
| 3 | FormBaseValue/BasePower/BaseDamage the only hit-size source; null-LegacyForm skills borrow via the spill formula (:1402-1420), which misprices PULSE | **PARTIAL** | BaseDamage reads at :1420,1573,1707,1926 confirmed; PULSE mispricing confirmed (active.md:26). **Mechanism wrong:** PULSE is `SkillKind.Active` (SkillCatalogue.cs:486-488) and is priced on the CAST path, `raw = FormBehaviour.BaseDamage(form, …)` at :1573 with `form == Form.Aura` (SetSkill writes sibling MIRE's LegacyForm, PlayerLoadout.cs:194-196). The spill formula (:1412-1420) lives on the FIELD path and is reached by no spilled skill today — every spilled active resolves to a Field with a dial that `continue`s first (PRESS :1327, BRAND :1399, WILT :1373) or to a Reaction skipped at :1245 (WEEP); only native MIRE reaches it with `castMs == IntervalMs` → `perTick = 1` (a no-op). REPAY (null LegacyForm) never touches the table (:1566-1570 `PaysBackDamageTaken`). Also see Missed #2: base JAWS runs on `FormBaseValue[Trap]`, not on a reflect. |
| 4 | `Weaving.AbilityPower` A-delete, test-only | **CONFIRMED** | `grep -rnE "AbilityPower" src/ tests/` → ResonanceWeaving.cs:344 (decl) + WeavingTests.cs:207,208,226,227,257,258,265. No src caller. |
| 5 | FormBehaviour predicates/tables A-delete → Def.* | **PARTIAL** | All callers confirmed (SoloBattle 528,1376,1412,1424,1435,1503,1705,1715,1721,1943; Build.cs:220; BuildComposer.cs:102-103,180; BuildGlossary.cs:49,54,62-63,68,71; SoloExpeditionScreen 2926,2963,2972,3039,3048; plus FormBehaviour.Native :73). **Two caveats the edit must respect:** (a) `Targets(Form)` (FormBehaviour.cs:132-137: Aura ∞, Projectile 2, else 1) is NOT a door onto the catalogue — it is its own table and disagrees with `SkillDef.Targets` for SPRAY (5, SkillCatalogue.cs:433). Swapping `shape.TargetsFor(woven.Form)` at :1715 for `Def.Targets` lifts WEAVER's echo into SPRAY from 2 to 5 targets — a balance change. (b) The Build.cs:219-220 Form fallback is unreachable in production: BuildComposer.cs:189 is the only src constructor and always passes `PassiveSlot`. |
| 6 | AffinityFactor/HexDistance + Build.Affinity + MasteryTree.Affinity + MasteryNode.Form → Style | **CONFIRMED** | FormBehaviour.cs:245-284; Build.cs:441; MasteryTree.cs:63,253-257; MasteryCatalog.cs:434-448,542-545; SoloBattle.cs:709-720,786-788,1584-1586,1712-1713; WeaveScreen.cs:43,1108-1109; FormHexDiagram.cs:156,172. Note `SkillCatalogue.RingDistance/Opposite` (:661-670) have **no src caller** (only skill_catalogue_test.cs:148-160) — the Style ring is dormant, and swapping to it is a balance change (Missed #1). |
| 7 | MarkWindowMs/MarkMultiplier are Form constants; AmplifyMs/AmplifyPercent are *added on top* (:1388-1394,1517) | **PARTIAL** | Depth: ✓ stacked — `MarkMultiplier + shape.MarkPowerBonus` at :786 AND `m *= 1f + markBonus` at :797-800 both apply while `MarkUntilMs > absMs` (SPEND's card "+200%" is effectively 1.6 × 3.0 = 4.8×). Window: ✗ **replaced, not added** — `if (sk.Def.AmplifyMs > 0) window = sk.Def.AmplifyMs;` at :1518. Base `sign_call` sets neither dial (SkillCatalogue.cs:376-401: only SPEND/STEADY do), so the base "+60% for 6s" line IS the two Form constants. |
| 8 | Clip constants (CastClipMs, ClipShareOfBeat, SkillClipShareOfBeat, AuraTickMs) → presentation tuning / DefaultFieldTickMs | **PARTIAL** | ClipShareOfBeat/SkillClipShareOfBeat read only by SoloExpeditionScreen.cs:3300,3316 ✓. AuraTickMs read by SoloBattle.cs:1260 and screen :1149,:3432 ✓. **`CastClipMs` (FormBehaviour.cs:194) has no reader anywhere** — `grep -rnE "CastClipMs|CastGapFor" src/` → only the declaration; the `SoloBattle.CastGapFor` its doc comment cites does not exist. It is dead (A), not tuning to move (C). |
| 9 | `SkillDef.LegacyForm` + `Resolve(Form,bool)` B-migration-only; live readers listed | **CONFIRMED** | PlayerLoadout.cs:194-197; WeaveScreen.cs:691,711,777,1037,1492,1573; SoloExpeditionScreen.cs:866,3010; Game1.cs:2097; Build.cs:256; BuildComposer.cs:173 all verified. Add FormBehaviour.Native (:73) and SoloExpeditionScreen.FxFor (:3356) as further `Resolve(Form,…)` readers. |
| 10 | SkillPick/SkillChoice carry Form; id dropped at ToBuild:268; delete CycleForm/SetForm + hover swap | **PARTIAL** | Shapes and :268 drop confirmed; hover swap at WeaveScreen.cs:1256-1278 confirmed. **`SetForm`/`SetSource` have three more callers** in Game1.cs dev fixtures (:1711, :1739-1740, :2081-2082) that must be rewritten to `SetSkill`. `CycleForm` and `CycleSource` (PlayerLoadout.cs:131-141) have **zero callers** in src or tests — already dead; the :166 "gamepad path" comment is stale. |
| 11 | SkillShape.FormPower/FormTargets/TargetsFor(Form) + Character.Aptitude + ItemFamilies.FavouredForms + GearShape.Of → Style | **CONFIRMED** | Character.cs:118,133,206-212; CharacterRoster.cs:56-212 (nine `Aptitude = Form.X`); ItemFamilies.cs:49-64; GearShape.cs:25-29; SkillShape.cs:187,202-205,425-444,461-467,509-510; SoloBattle.cs:811; MasteryCatalog.cs:439. |
| 12 | EnchantNeed.Form/NeedsForm + ForgeScreen.ActiveForms + WeaveScreen.GearWants → Style | **CONFIRMED** | Enchantments.cs:104-129,175-180,189; ForgeScreen.cs:524,551; Game1.cs:2878; WeaveScreen.cs:1356-1365. `SetSkill` writes the style's Form (PlayerLoadout.cs:194-197), so it is already a Style check. |
| 13 | BattleEvent Skill/Aura Amount=(int)Form + WaveReplay Form queries | **CONFIRMED** | WaveModel.cs:279-293. Emit sites: SoloBattle.cs:**1272** (auditor said 1288), 1543, 1558, 1714, 1942. WaveReplay.cs:172,189-218,221-234,303. Screen :348,370-371,1254-1270,2904-2905,2918-2919,3277-3280. |
| 14 | FormHexDiagram + icon_form_* glyphs → Style hexagon; retire PNGs + spec | **PARTIAL** | FormHexDiagram.cs (332 lines) AffinityFactor :156,172, icon_form_ :196; callers BuildScreen.cs:1322-1324,1395-1396; WeaveScreen.cs:1051,1705; six PNGs in assets/art/UI/icons/forms/; build_spec.py:714-734. **`tools/marketing/make_itch_page.py:419,440` loads `icons/forms/icon_form_*.png` from disk** — retiring the PNGs breaks the itch page generator unless it is repointed first. |
| 15 | BuildGlossary.FormHeadline/FormRule/SkillSummary → per-Style + SkillDef.Line | **CONFIRMED** | BuildGlossary.cs:29-79,166-167; callers WeaveScreen.cs:1749-1750,1773-1774; tests build_glossary_test.cs:61-62,84,94; heal_balance_test.cs:306-307. `SkillSummary` has no src caller. |
| 16 | resonance-weaving-system.md + vow-condition-tracking.md + systems-index entries → SUPERSEDED | **CONFIRMED** | `grep -rn Summon src/` → nothing. resonance-weaving-system.md 838 lines, "Status: Complete", 2026-07-14; vow-condition-tracking.md 688 lines; systems-index.md:39,43,44,51,146,166-184 as cited; vows.md "Status: Implemented", §3.1 "A Vow reads the build, never the fight". |
| 17 | DEAD `SkillPick.SkillId` + "THE ID WINS" branch; SkillChoice.SkillId read only at :65 and WeaveScreen:1485 | **PARTIAL** | Core confirmed: every `SkillPick(` construction (PlayerLoadout.cs:268; SoloExpeditionScreen.cs:862,2862; WeaveScreen.cs:689,704,775,978,1571; 20+ tests) passes 4-5 positional args; BuildComposer.cs:87-92 and :171-173 unreachable from the game. **Detail wrong:** `SkillChoice.SkillId` has a third reader, WeaveScreen.cs:840 (`skills[_slot].SkillId != def.Id`, the "changed" flourish), besides :65 and :1485. |
| 18 | DEAD `SoloExpedition.LastWaveTopForm` + WARDED affix | **CONFIRMED** | SoloExpedition.cs:86 `private set`, never assigned. Bands.cs:155-180 no Warded case; SoloExpedition.cs:290-332 consumes only Health/Damage/Defence/ExtraCreatures/Interval/Sustain multipliers. `grep -rn "Warded\|TopForm"` → Bands.cs:48, BandCycles.cs:93,99,111, SoloExpedition.cs:86 only. The chip IS shown to the player via `affixes[i].ToString().ToUpperInvariant()` at SoloExpeditionScreen.cs:2448. |
| 19 | DEAD `SaveGame.Affinity` | **CONFIRMED** | SaveGame.cs:159 only; no `Affinity =` on any SaveGame construction in Game (grep); Game1.cs:2769 is `_mastery.Affinity()`; no Persistence test references it. |
| 20 | DEAD `Weaving.AbilityPower` | **CONFIRMED** | Duplicate of #4. |
| 21 | DEAD `WovenAbility.Name` / `EquippedSkill.Name` except `Build.Unweave` | **CONFIRMED** | Build.cs:222 → readers Build.cs:468 (src) and BuildTests.cs:169 (tests). |
| 22 | DEAD `BareSlot.Ring` / `BareSlot.Charm` | **CONFIRMED** | Catalog `Bare =` only Boots/Gloves/Helm (ResonanceWeaving.cs:452,459,466); mapped at SoloBattle.cs:2126-2127; WeavingTests.cs:123 enumerates all five (test to trim). |
| 23 | BRANCH :1551 `form == Form.Projectile && Overdraw` | **CONFIRMED** | SoloBattle.cs:1551. |
| 24 | BRANCH :1601 `form == Form.Strike && Rend` | **CONFIRMED** | SoloBattle.cs:1601-1602. |
| 25 | BRANCH :1613 `form == Form.Strike && Execute` | **CONFIRMED** | SoloBattle.cs:1613-1616. |
| 26 | BRANCH :786-788 `AffinityFactor(markAff, Form.Mark)` | **CONFIRMED** | SoloBattle.cs:786-788. |
| 27 | BRANCH :1926,1942,1943 Form.Trap literals inside a Kind==Reaction block | **CONFIRMED** | SoloBattle.cs:1904 selects by `Kind != Reaction || On != Bitten`; :1926, :1942, :1943 use `Form.Trap`. |
| 28 | BRANCH :1376,1435,1503,1705,1721 IsAmplifier/Heals/FiresOnBeingHit | **CONFIRMED** | All five lines verified. |
| 29 | BRANCH WaveReplay :172,303 + screen :1268-1270,3277 `(Form)e.Amount != Form.Trap` | **CONFIRMED** | WaveReplay.cs:172,303; SoloExpeditionScreen.cs:1268,1270,3277. |
| 30 | BRANCH screen :682-688,1366-1405 `switch (Form)` | **CONFIRMED** | SoloExpeditionScreen.cs:682-690 CalloutFor; :1366-1407 PlayFormVfx. |
| 31 | BRANCH Game1.cs:2932 `Form != Form.Strike` starter check | **CONFIRMED** | Game1.cs:2932. Note the compare target should be the champion's `StartingSkillId`, not `hammer_blow` — see Missed #4. |
| 32 | BRANCH Character.cs:189-197 clip-name strings | **CONFIRMED** | Character.cs:189-197; keys are already opaque strings on `SkillDef.ClipKey`. |
| 33 | COUPLING WaveModel.cs:279-293 + SoloBattle emits shaped for the screen decode | **CONFIRMED** | As #13; emit at :1272 not :1288. |
| 34 | COUPLING CastClipMs/ClipShareOfBeat/SkillClipShareOfBeat read only by screen :3300,3316 | **PARTIAL** | ClipShareOfBeat/SkillClipShareOfBeat ✓ (:3300,3316). `CastClipMs` is read by **nothing** (the screen uses its own `ClipMs`); it is dead, not coupled. |
| 35 | COUPLING BuildGlossary.cs:28-76 player copy with sim numbers | **CONFIRMED** | BuildGlossary.cs:47-79 reads BaseCooldownMs/AuraTickMs/MarkWindowMs/MarkMultiplier/Targets/TransformationLeech. |
| 36 | COUPLING WeaveScreen.cs:1256-1278 hover-DPS mutates live loadout via SetSource/SetForm | **CONFIRMED** | WeaveScreen.cs:1270-1276; reachable because `hoverForm = def.LegacyForm` at :1492. |
| 37 | COUPLING screens re-run SlotKinds + Resolve(Form, passive) | **CONFIRMED** | SoloExpeditionScreen.cs:861-866,2862,3010; WeaveScreen.cs:688-691,703-711,774-777,1037,1570-1573; Game1.cs:2097. |
| 38 | SAVE-RISK `SavedSkill.Form` + `Passive` identity; TryParse drops unknown | **CONFIRMED** | SaveGame.cs:294-309; PlayerLoadout.cs:290-292 `Enum.TryParse<Form>` else-drops the slot; `Resolve(Form, passive)` (:646-655) is the (Style × slot kind) bijection that makes it lossless today. |
| 39 | SAVE-RISK ShareCodes RHB1 carries `"Form"`, validator requires non-empty ≤40 | **CONFIRMED** | ShareCodes.cs:48 `List<SavedSkill>`; :224-227 validator. `JsonSerializer.Deserialize<SharedBuild>(json)` at :210 uses default options → unknown members skipped, so adding `SkillId` is read-compatible for existing codes. |
| 40 | SAVE-RISK `SaveGame.Affinity` — safe only if unknown members tolerated (not verified) | **PARTIAL** | Dead ✓. **The hedge is resolved:** `SaveSystem.Options` (SaveGame.cs:444-448) sets only `WriteIndented` and `DefaultIgnoreCondition = WhenWritingNull`; `grep -rn UnmappedMember src/` → nothing, so System.Text.Json's default `Skip` applies and an old save's `"Affinity": ""` is ignored on load. One-step deletion is safe; no keep-for-one-release needed. |
| 41 | SAVE-RISK `BattleEvent.Amount=(int)Form` in-memory contract; ordinal reuse changes the rail | **CONFIRMED** | SoloExpeditionScreen.cs:348 `SkillKey = source*16 + form`; :370-371; :2904-2905,2918-2919. |
| 42 | SAVE-RISK MasteryTaken ids `spec_strike…`; RestoreTaken validates ids only | **CONFIRMED** | MasteryCatalog.cs:434-448; MasteryTree.cs:172-179 `if (MasteryCatalog.ById(id) is not null) _taken.Add(id)`. |
| 43 | DUPLICATE three cadence sources (:528 / :1453 / :1484,1908) | **CONFIRMED** | SoloBattle.cs:528, 1453, 1483-1484, 1908; BuildComposer.cs:179-180; skill_slot_kinds_test.cs:597-610. |
| 44 | DUPLICATE MarkWindowMs/MarkMultiplier vs AmplifyMs/AmplifyPercent — Form constant is base, dials on top | **PARTIAL** | Same as #7: depth stacks (:786 × :797-800), window is **replaced** by `AmplifyMs` when > 0 (:1518). |
| 45 | DUPLICATE Heals(form)→TransformationLeech vs Lifesteal dial; GLUT cannot switch it off | **CONFIRMED** | SoloBattle.cs:1691-1692 (`vdef.Lifesteal`) then :1721-1731 unconditional on `Heals(form)`; field path :1435-1440. GLUT `Lifesteal = 0f` (SkillCatalogue.cs:556-558) leaves :1721 untouched. THIRST sets `Lifesteal = TransformationLeech` (:549) so "doubles" is literally the stack. |
| 46 | DUPLICATE Targets(form) via TargetsFor(Form) at :1424,1715,1943 vs TargetsFor(form, vdef.Targets) at :1654 | **CONFIRMED** | Lines verified. Note the Form table's Projectile=2 vs SPRAY's Def.Targets=5 at the Weaver path (see #5). |
| 47 | DUPLICATE two hexagons with different ring orders; sim uses the Form one | **CONFIRMED** | FormBehaviour.cs:267-272 (Strike,Projectile,Aura,Trap,Mark,Transformation) vs SkillCatalogue.cs:34-53,661-666 (Hammer,Snare,Sign,Volley,Field,Drain). Opposites differ: Strike↔Trap / Projectile↔Mark / Aura↔Transformation vs Hammer↔Volley / Snare↔Field / Sign↔Drain. Style ring has no src caller. |
| 48 | DUPLICATE composer resolves by SkillId-or-Form; EquippedSkill.Def re-resolves by (Form, Passive) | **CONFIRMED** | BuildComposer.cs:171-173 vs Build.cs:252-263. |
| 49 | DUPLICATE EnchantNeed.MetBy vs SoloBattle trigger branches | **CONFIRMED** | ForgeScreen.cs:551, Game1.cs:2878, WeaveScreen.cs:1360-1362 vs SoloBattle.cs:1551 (Overdraw), 1613 (Execute), 1261 (Radiance), 1507 (Linger), **1909** (Coiled; auditor said 1911), 1723/1437 (Siphon). |
| 50 | DUPLICATE FormHeadline/FormRule vs SkillDef.Line + Style summaries | **CONFIRMED** | BuildGlossary.cs:29-79; SkillCatalogue.cs:34-53 (Style doc comments), per-skill `Line` on every SkillDef. |

## Missed by the auditor (important for this area)

1. **Switching affinity from the Form hexagon to the Style ring is a balance change, not a rename.** The two
   rings disagree on which pairs are opposite (FormBehaviour.cs:267-272 vs SkillCatalogue.cs:661-666). Mapping
   Form→Style (Strike→Hammer, Trap→Snare, Mark→Sign, Projectile→Volley, Aura→Field, Transformation→Drain): a
   Hammer specialist's SPRAY moves from adjacent ×1.15 to opposite ×0.45; JAWS moves from opposite ×0.45 to
   adjacent ×1.15; every off-discipline multiplier in `FactorAtDistance` (FormBehaviour.cs:278-284) lands on a
   different skill. `RingDistance`/`Opposite` have never been exercised by the sim (test-only), so
   `affinity_test`, `affinity_vow_buyback_test`, `roster_parity_test` and the WeaveScreen affinity tags need
   re-measurement, not just a `Style.Hammer` rename. The ItemFamilies spear pairing (Strike+Trap,
   ItemFamilies.cs:53) is opposite on the Form ring and adjacent on the Style ring.
2. **Base JAWS runs on the Form table, not on a reflect.** `snare_jaws` sets no `ReflectFraction` at the base
   line (SkillCatalogue.cs:348-373; only NET/IRON variations do, :356,:365-367), so
   `reflect > 0f ? taken * reflect : FormBehaviour.BaseDamage(Form.Trap, …)` (SoloBattle.cs:1924-1926) takes
   the 290 fallback for every unlevelled JAWS while its card says "Every bite returns 50% of it" (:349).
   Deleting `FormBaseValue[Form.Trap]` silently zeroes base JAWS unless a `BasePower` or base
   `ReflectFraction` is authored first — the same card-vs-code species as DRINK's "50%" (§7.2).
3. **WEAVER's echo target count changes under the proposed `Def.Targets` swap** (2 → 5 for SPRAY), see #5/#46.
   The "mechanical" replacement is only behaviour-preserving for BLOW/DRINK/JAWS/fields.
4. **The starter slot is Form-encoded AND stale against `Character.StartingSkillId`.** `PlayerLoadout.Starter()`
   (:316-321) and `AddSkill()` (:100) create `SkillChoice(Body, Strike, null)` with no `SkillId`;
   `Game1.cs:250,987` use it; `grep -rn StartingSkillId src/ResonanceHunter.Game/` → only WeaveScreen.cs:1376
   (adds it to the known set) — nothing ever calls `SetSkill(StartingSkillId)`. BuildComposer.cs:154-155,178
   gates on `LearnedSkills() + StartingSkillId`, so for the eight champions whose starting skill is not
   `hammer_blow` (CharacterRoster.cs:70-209) the starter slot resolves to BLOW and is refused by the taught
   gate — the champion fights with no skill until the player picks one. *Inferred from code, not run* — flag
   for a test. It also means Game1.cs:2932's "differs from starter" must compare against
   `Character.StartingSkillId`, and the target `SkillChoice(SkillId, …)` needs an explicit empty-slot
   representation (today "empty" and "legacy BODY STRIKE" are the same tuple; WeaveScreen.cs:1038-1040
   already special-cases the ambiguity).
5. **`CastClipMs` is dead** (no reader; the `SoloBattle.CastGapFor` its remark cites does not exist) — delete
   rather than move. `CycleForm`/`CycleSource` (PlayerLoadout.cs:131-141) are likewise callerless.
6. **`EquippedSkill.Passive`'s Form fallback (Build.cs:219-220) is unreachable in production** — the only src
   constructor (BuildComposer.cs:189) always passes `PassiveSlot`; only tests hit it.
7. **Extra callers the destructive edits must catch:** `SetForm`/`SetSource` in Game1.cs:1711,1739-1740,
   2081-2082; `SkillChoice.SkillId` read at WeaveScreen.cs:840; `icon_form_*.png` loaded by
   `tools/marketing/make_itch_page.py:419,440`.
8. **Serializer options resolved:** `SaveSystem.Options` (SaveGame.cs:444-448) and `ShareCodes` (default
   options, :122,164,210) both skip unknown members. `SaveGame.Affinity` can go in one release; `SavedSkill`
   can gain `SkillId` without breaking old saves or old RHB1 codes.
9. **SIGN's stacking has a player-facing text consequence** the refactor must settle: SPEND's card
   (SkillCatalogue.cs:382) says "+200%" but the loop multiplies `1.6 × (1 + 2.0) = 4.8×`; OVERSPEND
   "+300%" is `1.6 × 4.0 = 6.4×`. Moving the base onto the SkillDef must decide whether the base "+60%" is
   the floor or a multiplier — today's behaviour is the latter.
10. Line-cite drift in the report that a scripted edit would trip on: Aura event emit is SoloBattle.cs:1272
    (not 1288); the Skill emit is :1558 (not 1559); Coiled is :1909 (not 1911).
