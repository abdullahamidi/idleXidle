# Persistence audit — adversarial verification

Verified 2026-08-31 against branch `feat/hunter-cutout-rig`, read-only. Every verdict below was checked by
grep/read of the cited file:line; where the auditor's line numbers had drifted the corrected line is given.
Verdicts: CONFIRMED = claim and its destructive consequence hold as written; PARTIAL = directionally right
but a detail that would change the edit is wrong or missing; REFUTED = concrete contrary evidence.

Headline correction: the report treats `Form` as "the save's legacy skill identity, otherwise retired". It
is also the **live fight's** skill identity. `EquippedSkill.Def` is `SkillCatalogue.Resolve(Form, Passive)`
(`src/ResonanceHunter.Core/Builds/Build.cs:255-256`); `BuildComposer.Compose` resolves the `SkillDef` only to
gate and to look up progress, then constructs `EquippedSkill(ability, cooldown, PassiveSlot)` **without the
id** (`BuildComposer.cs:189-193`). So the `SkillId` never reaches the sim today, and every Form-deletion
step in §6(b) has a prerequisite the report does not list: `WovenAbility`/`EquippedSkill` must carry a
`SkillId` before `Resolve`, `LegacyForm` or the `Form` column can move to a migration table.

---

## Verdict table

| # | Claim (short) | Verdict | Corrected detail |
|---|---|---|---|
| 1 | SavedSkill.Form = legacy identity; add SkillId, isolate Form→Style table | **PARTIAL** | Persistence half correct. "never used at runtime" is wrong: `Build.cs:255-256` resolves the sim's `SkillDef` from Form; `SoloBattle.cs` reads `form` for damage/affinity/heal at 709-717, 1412-1435, 1573, 1584-1586, 1705-1721, 1926. Table cannot be "isolated" in Persistence until EquippedSkill carries an id. |
| 2 | SaveGame.Affinity never written/read → delete | **CONFIRMED** | Only declaration `SaveGame.cs:159`; absent from Capture `:509-554` and the host `with` `Game1.cs:874-928`; zero test references. |
| 3 | SavedItem.EquippedToCreatureId always null → delete DTO field + two null-checks | **PARTIAL** | Runtime-dead: CONFIRMED (no `src` setter outside `SaveGame.cs:567,587`). Deletion scope undercounted: `ItemInstance.EquippedToCreatureId` (`LootSystem.cs:73`), `IneligibleReason.EquippedToCreature` + its Explain string (`Forge.cs:16,109`), and three tests that SET it (`ForgeTests.cs:22`, `SalvageTests.cs:58`, `LootSystemTests.cs:233`). |
| 4 | RegionMasteryPoints still written; stop writing, keep read fold | **PARTIAL** | Live-save branch unreachable: CONFIRMED (`Game1.cs:874-876` always passes `_world`). But `Capture(hunter, region, inventory, nowMs)` — the 4-arg form every test uses — writes **empty** `RegionFarms` (`SaveGame.cs:539`), so for those saves `RegionMasteryPoints` is the only region record; `SaveSystemTests.cs:52` and `tests/integration/.../FullLoopTests.cs:88` assert it; and `region` is a required Capture parameter used ONLY at `:552`. Dropping the write orphans the parameter and breaks two tests. |
| 5 | ChestKeepSlot dual-written; stop writing, keep read | **CONFIRMED** | Lines drifted: write `Game1.cs:911`, read `:697`; `:699` is a no-op duplicate parse. `chest_filter_test.cs:82-89` round-trips the DTO field directly (unaffected). |
| 6 | Charter.Merge→Salvage lives in host; move to RestoreHunter so Merge can leave the enum | **PARTIAL** | Host migration at `Game1.cs:613` (not 610-612), untested: CONFIRMED. Leaving the enum: `Charters.cs:48-72` use `_ =>` for Merge (compile-safe), but `charters_test.cs:132-142` uses `Charter.Merge` as its fixture and `:102` asserts spoils never roll it — both need edits. Ordering trap: `RestoreHunter` filters unknown names via `TryParse` at `SaveGame.cs:617-619`, so once Merge leaves the enum the rule must read the RAW `save.Charters["Merge"]` before that filter or the count is silently dropped. |
| 7 | GDD save-load-persistence.md describes a system that never shipped | **CONFIRMED** | GDD: per-section envelope `:127-181`; `ApplicationData`/`%AppData%` `:224`; `.incompatible-v<N>` `:372,480`; async `:109`; `I_autosave=120` `:291`; `creature_roster` `:146-148`. Code: flat record + single `Version` (`SaveGame.cs:17-19`), `LocalApplicationData` (`SaveFile.cs:33-35`), sync `File.WriteAllText` (`SaveStore.cs:67`), 10 s (`Game1.cs:512`), `save.corrupt-`/`save.newer-` (`SaveStore.cs:121,133`). |
| 8 | SharedBuild wire shape is Source/Form; RHB2 with SkillId | **CONFIRMED** | `ShareCodes.cs:46-51,224-227`; `WeaveScreen.cs:749-751` omits `Passive` (feedback code includes it, `Game1.cs:943-944`); `ChestScreen.cs:733-734` DISCIPLINE from `spec?.Form`; `:747` prints `{Source} {Form}` (not 748). |
| 9 | DEAD SaveGame.Affinity | **CONFIRMED** | As #2. |
| 10 | DEAD SavedItem.EquippedToCreatureId | **PARTIAL** | As #3 — runtime-dead, but test setters exist and the Core record field is part of the same deletion. |
| 11 | DEAD RegionMasteryPoints write side | **PARTIAL** | As #4 — dead for live saves, load-bearing for every test save and for the `region` parameter. |
| 12 | DEAD ChestKeepSlot write side | **CONFIRMED** | As #5, lines 911 / 697-699. |
| 13 | SkillChoice.SkillId never persisted | **CONFIRMED** | `PlayerLoadout.cs:278-279` tuple omits it; `:292` constructs without it. Additional live symptom not in report: `WeaveScreen.cs:1485` (`SkillId == def.Id`) — no library cell reads as selected after a reload; `:840` — re-picking the same skill after reload flashes/plays sfx as a change. |
| 14 | BRANCH SkillCatalogue.Resolve — "fine as migration table, wrong as runtime" | **PARTIAL** | Lines 653-654 correct. But Resolve IS the runtime path: `Build.cs:256` (`EquippedSkill.Def`), `FormBehaviour.cs:73` (`Native`), `Game1.cs:2097`, `BuildComposer.cs:173`. Moving it to `Persistence/LegacySkillForm.cs` as proposed breaks the sim. |
| 15 | BRANCH BuildComposer 102-103, 179-180 on Form | **CONFIRMED** | Plus a third site: `Build.cs:219-220` `PassiveSlot ?? (IsPassive(Form) \|\| FiresOnBeingHit(Form))`. |
| 16 | BRANCH LegacyUnlocks 72-78 — frozen, keep | **CONFIRMED** | `LegacyUnlocks.cs:72-78`; header `:18` "must never change again". |
| 17 | BRANCH Game1 on Charter.Merge | **CONFIRMED** | Line 613. |
| 18 | BRANCH ChestScreen Spec node .Form | **CONFIRMED** | `ChestScreen.cs:731-734`. |
| 19 | COUPLING migrations in host Initialize, untestable | **CONFIRMED** | Corrected lines: Merge 613, loadout 635-637, ChestKeepSlot 693-699, region fold 731-744. No `try`/`catch` anywhere in `LoadOrStartFresh` (551-800). `ResonanceHunter.slnx` lists only Core.Tests and Integration.Tests — no Game test project. |
| 20 | COUPLING PlayerLoadout save/restore tuple in Game | **CONFIRMED** | `PlayerLoadout.cs:21-26` (why it is in Game), `:278-295`; `BuildComposer.cs:16-19` (assembly moved to Core). |
| 21 | COUPLING half of SaveGame populated by host `with` | **CONFIRMED** | `Game1.cs:874-928`. |
| 22 | SAVE-RISK deleting required Form → Corrupt on older build; migration plan | **PARTIAL** | Every persistence fact CONFIRMED: `required` `SaveGame.cs:297`; `JsonException→Corrupt` `:470-475`; latch `SaveStore.cs:47-48`; no `Version <` branch (`:479` is `>` only); table matches catalogue (`SkillCatalogue.cs:289,373,401,456,538,566` → hammer_blow, snare_jaws, sign_call, volley_spray, field_mire, drain_drink); natural-passive {Aura,Trap} = `FormBehaviour.cs:140,160`. Caveat as #1/#14: the table cannot be "isolated" while `Build.cs:256` derives the fight's SkillDef from Form. |
| 23 | SAVE-RISK folder rename "ResonanceHunter" | **CONFIRMED** | `SaveFile.cs:33-35`, `DisplaySettings.cs:145-147`; `Missing` → `SeedNewGame()` (`Game1.cs:600`) and `LocksSaving(Missing)==false` (`SaveStore.cs:47-48`) → the 10 s autosave writes a fresh game to the new folder. |
| 24 | SAVE-RISK property renames — no [JsonPropertyName] | **CONFIRMED** | grep over `src` → only `JsonIgnoreCondition.WhenWritingNull` (`SaveGame.cs:447`). `WarrenResource` not persisted (Capture keys by `FacilityKind`, `:514-516`). |
| 25 | SAVE-RISK strict Enum.Parse BaseType + `_regions[id]` boot crash | **CONFIRMED** | `SaveGame.cs:582,589`; `Regions.cs:192`; call sites `Game1.cs:700` and `:735` (not 737), no try. Sharpening: `CreatureCore` is still MINTED on every kill (`LootSystem.cs:255`) and filtered before the bag (`ExpeditionLoot.cs:115`); `MergeRecipe.cs:75-80,97` still lists it — removal is a 6-file refactor plus the save crash. Answers open question 5. |
| 26 | SAVE-RISK MasteryTaken id changes silently unweave | **CONFIRMED** | `MasteryTree.cs:172-179` (drop at 178); gate `BuildComposer.cs:178` (not 175); spec ids `MasteryCatalog.cs:434-446`; road nodes `:460-471`. |
| 27 | SAVE-RISK Vow/trait ids and variation/reinforcement NAMES | **CONFIRMED** | `ResonanceWeaving.cs:489`; `MemoryDust.cs:197`; `SkillProgress.cs:146,148,151`; `SkillVariation` (`SkillCatalogue.cs:120`) and `Reinforcement(string Name, string Line, …)` (`:105`) carry no id — Name is the only key. |
| 28 | SAVE-RISK EnchantOverride hash fallback; FocusPool is Form-based | **CONFIRMED** | `Enchantments.cs:236-260`; `pool[(int)(hash % (uint)pool.Length)]` → `DivideByZeroException` on an empty pool; `EnchantKind` comment `:49-64` "do NOTHING unless your BUILD runs the Form they name"; `SaveGame.cs:591` TryParse→null→hash. |
| 29 | SAVE-RISK ordinals for Rarity/WaveOutcome/Archetype/Affix | **CONFIRMED** | `SaveGame.cs:359,273,323-326`; casts `:583`, `Game1.cs:704`, `RunLog.cs:122-125`. Nuance: `Rarity` is explicitly numbered 0-4 (`LootSystem.cs:9`), so only the other three are implicit-ordinal hazards. |
| 30 | SAVE-RISK HunterStat rename drops ranks, no refund | **CONFIRMED** | `SaveGame.cs:623-636`, `continue` at 625 precedes `AddGleam`. |
| 31 | SAVE-RISK no pre-migration backup | **CONFIRMED** | `SaveStore.cs:59-94` one generation; `SaveGame.cs:479` `>` only; `Game1.cs:512`. |
| 32 | DUPLICATE SaveSystem.Options vs ShareCodes default options | **CONFIRMED** | `SaveGame.cs:444-448` vs `ShareCodes.cs:101,108,122,151,164,210`. Today's difference: codes write `"Passive":null`/`"VowId":null` and are unindented — cosmetic; `required` enforcement identical. |
| 33 | DUPLICATE Resolve vs FormBehaviour fallback — "two places" | **PARTIAL** | Four, not two: `SkillCatalogue.Resolve` (:646-655), `BuildComposer.SlotKinds` (:102-103), `EquippedSkill.Passive` (`Build.cs:219-220`), `FormBehaviour.Native` (:72-73); plus the inverse skill→Form in `PlayerLoadout.SetSkill` (:194-196). |
| 34 | DUPLICATE Core Restore* vs Game1 inline migrations | **CONFIRMED** | Lines as #19. |

---

## Evidence detail for the PARTIAL verdicts

### #1 / #14 / #22 / #33 — Form is the sim's identity, not only the save's
```
src/ResonanceHunter.Core/Builds/Build.cs:253-256
    public SkillDef Def
    {
        get
        {
            var def = SkillCatalogue.Resolve(Form, Passive);
src/ResonanceHunter.Core/Builds/BuildComposer.cs:189-193
            build.Weave(new EquippedSkill(ability, cooldown, PassiveSlot: passive[i])
            {   Variation = variation, Reinforcements = taken,   });     // no SkillId passed
src/ResonanceHunter.Core/Builds/FormBehaviour.cs:72-73
    private static SkillDef Native(Form form)
        => SkillCatalogue.Resolve(form, IsPassive(form) || FiresOnBeingHit(form));
src/ResonanceHunter.Game/Game1.cs:2097
                        var rd = SkillCatalogue.Resolve(sk.Form, sk.Passive ?? false);
src/ResonanceHunter.Core/Builds/SoloBattle.cs:1573   raw = FormBehaviour.BaseDamage(form, resonance, wt);
src/ResonanceHunter.Core/Builds/SoloBattle.cs:1412   var castMs = FormBehaviour.BaseCooldownMs(form);
src/ResonanceHunter.Core/Builds/SoloBattle.cs:709-711 if (build.Affinity is { } aff && skillForm is { } f) … AffinityFactor(aff, f)
```
Consequence for the plan in §6(b): step 2 ("SkillDef.LegacyForm, SkillCatalogue.Resolve, SkillPick.Form,
SkillChoice.Form and the Form enum can leave runtime") needs a preceding step: give `WovenAbility` (or
`EquippedSkill`) a `SkillId`, make `Def` a `Find(id)`, and only then retire `Resolve`. The auditor's
cross-area list at §6(b).7 names SoloBattle lines but not `Build.cs:256`, which is the one that makes the
id-less design load-bearing.

### #3 / #10 — deletion scope
```
src/ResonanceHunter.Core/Loot/LootSystem.cs:73     public string? EquippedToCreatureId { get; init; }
src/ResonanceHunter.Core/Forge/Forge.cs:16         EquippedToCreature,
src/ResonanceHunter.Core/Forge/Forge.cs:109        IneligibleReason.EquippedToCreature => "EQUIPPED TO A CREATURE. UNEQUIP IT FIRST.",
tests/unit/ResonanceHunter.Core.Tests/Forge/ForgeTests.cs:22        EquippedToCreatureId = equippedTo,
tests/unit/ResonanceHunter.Core.Tests/Forge/SalvageTests.cs:58      var worn = Item() with { EquippedToCreatureId = "c1" };
tests/unit/ResonanceHunter.Core.Tests/Loot/LootSystemTests.cs:233   SellValue = 82, EquippedToCreatureId = "creature_1",
```

### #4 / #11 — the mirror is load-bearing for every test save
```
src/ResonanceHunter.Core/Persistence/SaveGame.cs:539   RegionFarms = world is null ? new List<RegionFarmSave>() : …
src/ResonanceHunter.Core/Persistence/SaveGame.cs:552   RegionMasteryPoints = region.RegionMasteryPoints,   // only use of `region`
tests/unit/ResonanceHunter.Core.Tests/Persistence/SaveSystemTests.cs:52
        Assert.Equal(region.RegionMasteryPoints, save.RegionMasteryPoints, precision: 1);
tests/integration/ResonanceHunter.Integration.Tests/FullLoopTests.cs:88
        Assert.Equal(region.RegionMasteryPoints, loaded.Save!.RegionMasteryPoints, precision: 1);
```
The report says `ls tests/unit → only ResonanceHunter.Core.Tests`; there is also
`tests/integration/ResonanceHunter.Integration.Tests` (`ResonanceHunter.slnx:8`).

### #6 — ordering of the Merge rule
```
src/ResonanceHunter.Core/Persistence/SaveGame.cs:617-619
        hunter.RestoreCharters(save.Charters
            .Where(kv => Enum.TryParse<Charter>(kv.Key, out _))          // "Merge" dropped here once the member is gone
            .Select(kv => new KeyValuePair<Charter, int>(Enum.Parse<Charter>(kv.Key), kv.Value)));
tests/unit/ResonanceHunter.Core.Tests/Economy/charters_test.cs:132-142   fixture uses Charter.Merge
tests/unit/ResonanceHunter.Core.Tests/Economy/charters_test.cs:102       Assert.DoesNotContain(Charter.Merge, seen);
```

---

## Missed by the auditor

1. **`EquippedSkill.Def` is Form-resolved at runtime** (`Build.cs:255-256`) and `BuildComposer` never hands
   the id to the sim (`:189-193`). This is the actual blocker for deleting `Form`, and the report's own
   comment trail (`Build.cs:247-251`: "once WovenAbility carries a SkillId this becomes a lookup by id")
   already names the prerequisite step.
2. **The 2026-08-30 "all twelve need unlock" change is already a silent migration loss for every pre-08-30
   save.** `SkillCatalogue.NeedsUnlock` returns true for all (`:627-640`); `BuildComposer.cs:174-178` drops
   any woven skill not in `mastery.LearnedSkills() ∪ StartingSkillId`. A Form-era save with four woven
   Forms and no `road_*` nodes keeps one skill in the fight; the loadout still holds and re-saves the
   other three, so it looks intact on disk. The `SlotKinds` comment at `:70-75` still cites the §11
   promise ("old four-active build keeps all four skills") that the gate below it now contradicts.
3. **Reload-visible UI symptoms of the missing SkillId**: `WeaveScreen.cs:1485` never marks a library cell
   selected after a reload; `:840` treats re-picking the same skill as a change (flash + sfx).
4. **`CreatureCore` is still minted every kill** (`LootSystem.cs:255`, doc comment `:241-246` still
   describes the creature-era guarantee) and filtered at `ExpeditionLoot.cs:115`; `MergeRecipe.cs:75-80,97`
   still recipes it. Open question 5 is answered: mintable, filtered, six-file removal.
5. **`Capture`'s 4-arg form writes empty `RegionFarms`**, so the "legacy" `RegionMasteryPoints` is the only
   region record in every unit/integration test save and `region` exists as a parameter solely to write it.
6. **The integration test project exists** (`tests/integration/ResonanceHunter.Integration.Tests`,
   `ResonanceHunter.slnx:8`) and pins `RegionMasteryPoints` (`FullLoopTests.cs:88`) and `RestoreHunter`.
7. **`Game1.cs:699` is a dead statement** — `_ = Enum.TryParse<ItemBaseType>(save.ChestKeepSlot ?? "", out _);`
   duplicates the parse two lines above and assigns nothing.
8. **Merge→Salvage rule ordering** (see #6): a name-keyed Core rule must run on the raw `save.Charters`
   dictionary before the `TryParse` filter at `SaveGame.cs:617-619`, or removing `Charter.Merge` from the
   enum silently drops the count the rule was meant to convert.
9. **Stale doc on `SavedSkill.Passive`** (`SaveGame.cs:305` "Defaults to false") — the member is `bool?`
   defaulting to null, and `BuildComposer.cs:23-29` explains why the three-state matters. Harmless, but
   the comment describes the pre-three-state behaviour.
10. **Deletion scope for `EquippedToCreatureId`** includes the Core record property, the `IneligibleReason`
    member and its Explain string, and three test fixtures (see #3).
