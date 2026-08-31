# Persistence audit — save DTOs, migrations, share codes

Audit date: 2026-08-31. Branch: `feat/hunter-cutout-rig`. Read-only audit; every claim below carries the
file:line it was read from. Paths are relative to the repo root.

Files read in full: `src/ResonanceHunter.Core/Persistence/SaveGame.cs` (710 lines), `SaveStore.cs`,
`ShareCodes.cs`, `BuildStamp.cs`, `src/ResonanceHunter.Core/Characters/LegacyUnlocks.cs`,
`src/ResonanceHunter.Game/SaveFile.cs`, `src/ResonanceHunter.Game/PlayerLoadout.cs`, the load/save regions of
`src/ResonanceHunter.Game/Game1.cs` (551-800, 846-990, 1130, 2300-2310), all nine files under
`tests/unit/ResonanceHunter.Core.Tests/Persistence/`, and `design/gdd/save-load-persistence.md`.

---

## 0. Headline

The save layer is in better shape than most of the codebase: one `System.Text.Json` record (`SaveGame`),
atomic temp+`File.Replace` writes with a rolling `save.bak`, quarantine of unreadable files, a write latch
when the load failed, and a consistent "lenient by name, default-clean" field policy that has absorbed every
schema change since the creature subsystem was cut without a single migration registry. The one structural
debt is exactly the one the planned refactor touches: **the current skill model is not in the save at all**.
`SavedSkill` carries `Source`, `Form`, `VowId`, `Passive` and no `SkillId` (`SaveGame.cs:294-310`);
`PlayerLoadout.SaveSkills` drops the id on the way out (`PlayerLoadout.cs:278-279`) and `Restore` rebuilds
the slot without it (`PlayerLoadout.cs:291-292`), so skill identity survives a reload only through
`SkillCatalogue.Resolve(Form, passive)` (`SkillCatalogue.cs:646-655`). Deleting `Form` therefore is not
"remove a legacy column" — it is "give the save its first real skill identity, then migrate the old one".
The second debt is the on-disk folder `%LocalAppData%\ResonanceHunter\` (`SaveFile.cs:34-35`,
`DisplaySettings.cs:146-147`): the product is already `IDLExIDLE` in the assembly name
(`ResonanceHunter.Game.csproj:8`) but the folder still carries the old identity, and renaming it without a
move step deletes every player's progress from their point of view. Everything else the brief asks about
(Aptitude, WarrenResource.Mastery, vow ids, mastery node ids, a "learned skills" field) is either not
persisted, additive, or handled by an existing lenient-drop rule that only needs a rename table.

---

## 1. Serializer and format

| Question | Answer | Evidence |
|---|---|---|
| Serializer | `System.Text.Json`, `JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = WhenWritingNull }`, nothing else | `SaveGame.cs:444-448` |
| Property names | C# PascalCase, default case-sensitive matching; no `[JsonPropertyName]` anywhere | grep `JsonPropertyName\|JsonIgnore\|JsonInclude` over `src` → 0 hits outside `SaveGame.cs` usings |
| Enum encoding | **No `JsonStringEnumConverter`** (grep → 0 hits). Enums are never serialized as enums: the DTOs hold `string` (written via `.ToString()`) or `int` (written via cast). By NAME: `Source`, `Form`, `ItemBaseType`, `GearTrait`, `EnchantKind`, `ItemClass`, `Charter`, `HunterStat`, `FacilityKind`, `TutorialStep`, `Activity`. By ORDINAL: `Rarity` (`SavedItem.Rarity`, `SavedChest.Rarity`), `WaveOutcome`, `Archetype`, `Affix` (`RunReportSave.Outcome/WallArchetype/WallAffixes`) | `SaveGame.cs:559-576` (ToSavedItem), `:318-335` (RunReportSave), `Game1.cs:915-922` (SavedChest), `RunLog.cs:114-135` (int casts back) |
| Type / assembly names in JSON | **None.** No polymorphism, no `$type`, no `TypeNameHandling` (grep → 0). A namespace or assembly rename cannot affect the file | grep `JsonPolymorphic\|JsonDerivedType\|AssemblyQualifiedName\|GetType().FullName` → 0 |
| Version field | `SaveGame.Version` int, `CurrentVersion = 2`; only bump so far was the item redesign (`"Gem"` base type, nested `Gems`, `"NONE"` trait sentinel) | `SaveGame.cs:16-23` |
| How the version is used | **Forward refusal only.** `save.Version > CurrentVersion → LoadFailure.FromNewerVersion`. There is no `< CurrentVersion` branch, no upgrade chain, no per-version transform. Every backward-compat rule is a field-level default or a `TryParse`-and-drop at restore | `SaveGame.cs:479-480`; grep `Version <\|Migrate\|migration` in `SaveGame.cs` → only comments |
| Absent `Version` member | Defaults to `CurrentVersion` (init value), so a file with no `Version` is read as current — the field cannot be used to detect pre-versioning saves | `SaveGame.cs:19` |
| `required` members | `SavedItem.InstanceId/BaseType/Rarity/SellValue`, `SavedSkill.Source/Form`, `SavedChest.Rarity/Tier`, `RunReportSave.RegionId`, `RegionFarmSave.Id`, `SavedSkillProgress.SkillId`. STJ (.NET 7+) throws `JsonException` when a `required` member is absent → whole load reports `Corrupt`, file is quarantined, session locks. **Removing `SavedSkill.Form` without a version bump turns every new save into "Corrupt" on an older build.** | `SaveGame.cs:296-297, 357-360, 273-274, 320, 345, 700`; `:470-475` |
| Unknown JSON members | Skipped (STJ default; nothing overrides it). Relied on for every retired field | `SaveGame.cs:53-56`, test `SaveSystemTests.cs:69-166` |
| Save location | `Environment.SpecialFolder.LocalApplicationData` + `"ResonanceHunter"` + `save.json`, or `RH_SAVE_DIR` if set. Display prefs live beside it as `display.txt` (own hand-parsed format, separate file) | `SaveFile.cs:31-38`, `SaveStore.cs:30-39`, `DisplaySettings.cs:145-147` |
| Product name on disk | Folder = `ResonanceHunter` (obsolete identity). Assembly = `IDLExIDLE`. GDD says `%AppData%/ResonanceHunter/save.json` (Roaming); code uses Local — GDD is wrong | `SaveFile.cs:35`, `ResonanceHunter.Game.csproj:5,8`, `save-load-persistence.md:223-226` |
| Write cadence | Autosave every 10 s (`AutosaveIntervalSeconds = 10f`), on `OnExiting`, and on ~40 dirty-flag sites | `Game1.cs:512, 2303-2309, 1130-1134` |
| Write path | Synchronous `File.WriteAllText` on the game thread inside `Update` (GDD asked for async — not implemented, and at this file size not needed) | `SaveStore.cs:67`, `Game1.cs:2308` |

**Other serializer sites.** `ShareCodes` uses plain `JsonSerializer.Serialize/Deserialize` with default options
(not `SaveSystem.Options`) for `SavedItem`, `SharedBuild`, `SharedFeedback` (`ShareCodes.cs:101,108,122,151,164,210`).
Same DTOs, different options object — harmless today (defaults differ only in indentation/null-writing) but
two options objects for one wire format is a drift point.

---

## 2. Every persisted field

Legend: **LEGACY** = stores a retired concept; **DEAD** = declared but never written or never read by any
runtime consumer (grep evidence given); **MIRROR** = still written for old builds.

### 2.1 Envelope
| Field | Type | Line | Notes |
|---|---|---|---|
| `Version` | int | 19 | see §1 |
| `SavedAtMs` | long | 26 | offline-time basis; `OfflineSeconds = max(0, now - SavedAtMs)/1000` (`:484-490`), capped at 24 h (`:691-694`) |

### 2.2 Currencies and materials
| Field | Type | Line | Consumer | Notes |
|---|---|---|---|---|
| `Gleam` | int | 28 | `RestoreHunter :609` | |
| `Materials` | int | 31 | `:610` | the SCRAP tier under its legacy name |
| `Essence`, `Core`, `Crystal` | int | 34-36 | `:611-613` | |
| `Charters` | Dictionary<string,int> by `Charter` name | 46 | `:617-619` (TryParse, unknown dropped) | **LEGACY member inside**: `Charter.Merge` still in enum (`Charters.cs:36-37`); host converts held Merge charters to Salvage on load (`Game1.cs:612`) — a migration living in the presentation host |
| `MemoryDust` | int | 60 | `_dust.Restore` (`Game1.cs:614`); spent on Warren upgrades (`Game1.cs:1119`) and checkpoints (`Game1.cs:3500`) | This is the **Dust currency**, not trait points. Trait points (`MemoryDustTree.Earned`) are **not persisted** — derived each frame by `TraitPointsEarned()` = conquests + 2×peak corruption + Σ region mastery levels (`Game1.cs:3715, 5191-5200`). Reconstructable from persisted facts — good |
| `TrainingRanks` | Dictionary<string,int> by `HunterStat` name | 48 | `:623-636` re-buys each rank; unknown stat name → `continue` (ranks lost, Gleam not refunded) | `HunterStat.ResonanceAffinity` is a persisted key (`HunterProgression.cs:11`) — do not rename that member without a key map |
| `WarrenMasteryPool` | long | 267 | `Game1.cs:651` | the pool the UI calls INSIGHT (`WarrenScreen.cs:117`). `WarrenResource` enum itself is **not persisted** anywhere |

### 2.3 Characters, quests, unlocks
| Field | Type | Line | Consumer | Notes |
|---|---|---|---|---|
| `ActiveCharacterId` | string | 102 | `RestoreCharacters :659` → `CharacterState.Restore` (`CharacterState.cs:120-123`): unknown → starter | |
| `UnlockedCharacters` | List<string> | 116 | `:652-658`: empty ⇒ pre-tier save ⇒ `LegacyUnlocks.Seed` | "empty means old" works because a new save always banks the starter (`CharacterState.cs:129-130`, test `unlocked_characters_test.cs:207-211`) |
| `QuestsDone` | List<string> | 119 | `CharacterState.cs:109-112` keeps ids **verbatim, unknown included** | correct — `LegacyUnlocks` needs the retired ids `q_hollow_hunt`, `q_first_vow` (`LegacyUnlocks.cs:29,35`) |
| `RunsWithVowKept` | int | 129 | `Game1.cs:666`; `LegacyUnlocks.Seed` | the one event counter that must be latched |
| `ChestsOpened` | int | 139 | parked → Forge (`Game1.cs:685`) | |

### 2.4 Build (skills, keystones, progress)
| Field | Type | Line | Consumer | Notes |
|---|---|---|---|---|
| `WovenSkills` | List<`SavedSkill`> | 142 | `Game1.cs:635-637` → `PlayerLoadout.Restore` | **empty ⇒ keep Starter** (a pre-solo save) |
| `SavedSkill.Source` | required string (`Source` name) | 296 | `Restore :291` TryParse; fail ⇒ **slot silently dropped** | Source is still a live concept (variation Source), but here it is the legacy composition Source — its only runtime use after 2026-08-30 is the fallback element for a skill with no chosen variation (`PlayerLoadout.cs:47-49`) |
| `SavedSkill.Form` | required string (`Form` name) | 297 | `Restore :291`; `BuildComposer.cs:171-173` resolves `Form+passive → SkillDef` when `SkillId` is null (always, after a reload) | **LEGACY and load-bearing: the ONLY skill identity in the save.** Mapping (`SkillCatalogue.cs:264-593`): Strike→Hammer (`hammer_blow`/`hammer_press`), Trap→Snare (`snare_jaws`/`snare_repay`), Mark→Sign (`sign_call`/`sign_brand`), Projectile→Volley (`volley_spray`/`volley_weep`), Aura→Field (`field_mire`/`field_pulse`), Transformation→Drain (`drain_drink`/`drain_wilt`) |
| `SavedSkill.VowId` | string? | 298 | `Weaving.ById` (`ResonanceWeaving.cs:489`): unknown ⇒ null vow, silently | 13 vow ids `vow_singular … vow_reckless_offering` (`ResonanceWeaving.cs:387-482`) |
| `SavedSkill.Passive` | bool? | 309 | `BuildComposer.SlotKinds :93-99`; null ⇒ `FormBehaviour.IsPassive(Form)` (`:102-103`) | three-state on purpose; pre-rework saves are null |
| *(absent)* `SkillId` | — | — | `PlayerLoadout.SaveSkills` (`:278-279`) omits `SkillChoice.SkillId`; `Restore` (`:292`) never sets it | **Round trip is identity-preserving only while actives ≤ `Build.ActiveSlotsFor(cap)` = (cap+1)/2 (`Build.cs:402`).** In-session a *named* active never spills (`BuildComposer.cs:87-91`); after reload the same slot is unnamed, `Passive=false`, and if the active budget is spent it spills (`:96-98`) and `Resolve(form, true)` returns the style's **other** skill (`SkillCatalogue.cs:653-654`). Whether the weave screen can produce that state is outside this area — see open questions |
| `SocketedKeystoneIds` | List<string> | 145 | `PlayerLoadout.Restore :293-294` (no validation, `Take(3)`); `BuildComposer.cs:146-149` drops any not learned | |
| `SkillProgress` | List<`SavedSkillProgress`> | 156 | `SkillProgress.Restore` (`SkillProgress.cs:137-158`) | keyed by **real skill id** — the one place the current model is persisted |
| `SavedSkillProgress.SkillId` | required string | 700 | unknown ⇒ row dropped (`:146`) | |
| `.Uses` | int | 703 | | |
| `.Variation` | string? (variation NAME, e.g. "FINISH") | 706 | unknown ⇒ dropped with its reinforcements (`:148-153`) | variation names are content strings, not ids — renaming a variation's `Name` wipes the player's choice and paid reinforcements |
| `.Reinforcements` | List<string> (names) | 709 | filtered to the variation's set (`:151-152`) | same rename fragility |

### 2.5 Mastery tree (respeccable)
| Field | Type | Line | Consumer | Notes |
|---|---|---|---|---|
| `MasteryTaken` | List<string> node ids | 162 | `MasteryTree.RestoreTaken` (`MasteryTree.cs:172-179`): unknown id dropped, `start` always added | Spec node ids carry **Form names**: `spec_strike, spec_trap, spec_projectile, spec_aura, spec_mark, spec_transformation` (`MasteryCatalog.cs:434-446`); road nodes `road_hammer(_2) … road_drain(_2)` grant skill ids (`:460-471`) |
| `MasteryEarned` | int | 165 | `Game1.cs:641` → `_deepestEver`; `:913` | **misnamed**: stores the deepest wave ever, not mastery points. Skill points are derived (`SkillPointsEarned`, `Game1.cs:5142-5148`) |
| `MasteryZoom/PanX/PanY` | float | 177-179 | `Game1.cs:646` | camera; 0 = first-open framing |
| `Affinity` | string | 159 | **none** | **DEAD.** Not set in `Capture` (`:509-554`), not set in the host's `with` block (`Game1.cs:874-926`), not read anywhere: `grep -rn "save\.Affinity\|Affinity = \"" src tests` → 0 hits (the only `Affinity =` hits are `Build.Affinity`, `BuildComposer.cs:128` and a test on `Build`). Comment on it already says "Legacy — the Mastery tree owns it now" |
| `HighestMasteryAwarded` | int | 76 | `Game1.cs:631, 2746-2750` | live; gates one-time Dust milestones |

### 2.6 Items
`Inventory : List<SavedItem>` (`:58`), restored by `RestoreInventory` (`:599-605`, dedups by `InstanceId`).
| `SavedItem` field | Type | Line | Load rule | Notes |
|---|---|---|---|---|
| `InstanceId` | required string | 357 | | |
| `BaseType` | required string (`ItemBaseType` name) | 358 | **strict `Enum.Parse`** (`:582, 589`) — throws `ArgumentException` | `RestoreInventory` is called from `Game1.LoadOrStartFresh :700` **outside any try/catch** (awk for try/catch in 551-760 → none) ⇒ an unknown BaseType **crashes the game at boot**. `ItemBaseType.CreatureCore` (`LootSystem.cs:13`) is a retired creature concept still in the enum; removing it would crash any save holding one. ShareCodes guards this path (`ShareCodes.cs:172`), the save path does not |
| `Rarity` | required int (ordinal) | 359 | `(Rarity)s.Rarity` unguarded (`:583`) | `Rarity` is explicitly numbered 0-4 (`LootSystem.cs:9`) — safe as long as nobody renumbers; out-of-range from a hand-edited save reaches colour-table indexing (ShareCodes fixed this for codes only, `:189-202`) |
| `SellValue` | required int | 360 | | |
| `ItemLevel` | int = 1 | 363 | | |
| `Upgrades` | int | 366 | | |
| `EquippedToCreatureId` | string? | 368 | copied both ways (`:567, 587`); read by `Forge.cs:101`, `HunterProgression.cs:531` | **DEAD/LEGACY**: `grep -rn "EquippedToCreatureId *=" src` outside Persistence → 0 setters. Always null since the creature cut |
| `TraitOverride` | string? (`GearTrait` name or `"NONE"`) | 379 | null ⇒ `LegacyDerivedTrait` (pre-redesign); `"NONE"` ⇒ plain; unknown name ⇒ null ⇒ plain (`:588-590`, `GearTraits.cs:122-127`) | |
| `EnchantOverride` | string? (`EnchantKind` name) | 380 | unknown ⇒ null ⇒ **falls back to the id-hash roll** `pool[hash % pool.Length]` (`Enchantments.cs:255-260`) | **Form-based enchants are the entire Focus/armour pool**: `FocusPool = {Linger, Radiance, Overdraw, Execute, Coiled}` (`Enchantments.cs:236-237`), used for Focus, Helm, Chest, Gloves, Boots (`:239-245`). Deleting any member re-rolls every un-reforged item on that slot (positional hash); deleting all five gives `hash % 0` |
| `Gems` | List<SavedItem> | 383 | recursive | |
| `Element` | string? (`Source` name) | 394 | TryParse ⇒ null | Source is current; safe |
| `Class` | string? (`ItemClass` name) | 405 | TryParse ⇒ null ("anyone") | |
| `Family` | int? | 408 | bounds-checked against `WeaponFamilies.Length` (`:595`) | positional — reordering families re-labels weapons |

Worn: `WornWeaponId, WornCharmId, WornFocusId, WornHelmId, WornChestId, WornGlovesId, WornBootsId, WornRingId : string?` (`:64-73`) → `Game1.cs:718-723`.

### 2.7 World, regions, corruption
| Field | Type | Line | Consumer | Notes |
|---|---|---|---|---|
| `ConqueredRegions` | List<string> | 79 | `_world.RestoreConquered` (`Game1.cs:727`) | |
| `ActiveRegion` | string | 80 | `Game1.cs:729-730`, guarded by `IsUnlocked` (unknown ⇒ home) | |
| `CorruptionTier`, `CorruptionPeak` | int | 93, 97 | `Game1.cs:728` | |
| `RegionFarms[]` | `RegionFarmSave { Id req, MasteryPoints float, BestDepth int, StartWave int }` | 81, 343-353 | `Game1.cs:735-741` | **`_world.RegionFarm(rf.Id)` is `_regions[id]`** (`Regions.cs:192`) — a dictionary indexer ⇒ `KeyNotFoundException` **at boot** for a renamed/removed region id, outside any try. `IsUnlocked` has the lenient comment (`Regions.cs:199-200`) but `RegionFarm` does not |
| `RegionMasteryPoints` | float | 51 | read only when `RegionFarms` is empty (`Game1.cs:743`) | **MIRROR/LEGACY**: still written on every save (`Capture :552`) |

### 2.8 Run log
`RunLog : List<RunReportSave>` (`:90`), `RunReportSave` (`:318-335`): `RegionId` req, `Depth`, `IsRecord`,
`Outcome` int, `WallWave`, `WallArchetype` int, `WallAffixes` List<int>, `WallCreatures`, six floats,
`SampledWaves`. Restored by `RunLog.FromSave` (`RunLog.cs:114-135`) with unguarded enum casts from ordinals
(`WaveOutcome`, `Archetype`, `Affix` — `WaveModel.cs:229`, `Archetypes.cs:31-37`, `Bands.cs:15-`). Reordering any
of those enums silently relabels every saved report; nothing validates the ints.

### 2.9 Chests and vault
| Field | Type | Line | Notes |
|---|---|---|---|
| `UnopenedChests[]` | `SavedChest { Rarity req int, Tier req int, Element string?, Region string?, RunTilt float=1, Gift string? }` | 220, 271-291 | restored `Game1.cs:701-711`; unknown Element ⇒ inert; `RunTilt<=0 ⇒ 1`; unknown Gift key ⇒ `GiftChests.Get` null ⇒ rolls (`Chest.cs:316`) |
| `FreeSocketUsed` | bool | 231 | inferred from any socketed gem when absent (`:667-671`) |
| `ChestKeepMinTier` | int | 234 | |
| `ChestKeepSlot` | string? | 237 | **MIRROR**: still written when exactly one slot is wanted (`Game1.cs:909`), read only when `ChestKeepSlots` empty (`:707-708`) |
| `ChestKeepSlots` | List<string> (`ItemBaseType` names) | 241 | |
| `TraderWeekStamp` | int | 246 | |
| `TraderBoughtSlots` | List<int> | 249 | |

### 2.10 Warren
| Field | Type | Line | Notes |
|---|---|---|---|
| `WarrenLevel` | int = 1 | 253 | |
| `WarrenXp` | int | 254 | |
| `WarrenFacilities` | Dictionary<string,int> by `FacilityKind` name | 257 | `RestoreWarren :673-680`, unknown dropped. Names: `Nursery, Tunnels, ForagingPits, ScavengerRuns, BreedingChamber, RitualNest, HoardVaults, SentryBurrows` (`Warren.cs:16-20`) — the creature-era names are the persisted keys |
| `WarrenMasteryPool` | long | 267 | see §2.2 |

### 2.11 Onboarding
| Field | Type | Line | Notes |
|---|---|---|---|
| `DismissedGuideRungs` | List<string> (`TutorialStep` names) | 190 | |
| `IntroSeen` | bool | 197 | |
| `ExplainedScreens` | List<string> (`Activity` names + `SkillSlotN`) | 208 | |
| `ChampionGleamRate` | float | 211 | offline champion income at half rate (`Game1.cs:773-779`) |

### 2.12 Not persisted, derived (good — "state vs event" is respected)
Trait points earned (`Game1.cs:5191`), skill points earned (`:5142`), learned skills (from `MasteryTaken`
Teaches nodes + `Character.StartingSkillId`, `BuildComposer.cs:153-154`), unlock gates (`Unlocks.*`), champion
`Aptitude` (grep `Aptitude` in `Persistence/`, `Game1.cs`, `PlayerLoadout.cs` → 0), `PlayerLoadout.SkillId`
(unintentionally — see §2.4).

---

## 3. Existing migrations, and what covers them

There is **no migration registry, no version-keyed transform, and no old-save fixture file**
(`find tests -name '*.json' -o -name '*.sav' -o -name '*.bak'` → nothing). Every migration is a field default
or a restore-time fallback, and every test builds its input in memory — either a `SaveGame` object or a
hand-written JSON string literal.

| # | Migration | Where | Test | Fixture kind |
|---|---|---|---|---|
| 1 | Retired creature fields (`AutomationStage`, `UnhatchedCores`, `Roster`, `AssignedCreatureIds`, `ChestsCredited`, farm `Stage/AssignedIds`) skipped by STJ | `SaveGame.cs:53-56, 339-342` | `SaveSystemTests.cs:69-166` | hand-written JSON literal |
| 2 | Single-region `RegionMasteryPoints` folded into the home region when `RegionFarms` empty | `Game1.cs:735-744` | `SaveSystemTests.cs:537-548` (DTO only; the host fold itself is untested — Game has no test project, `ls tests/unit` → only `ResonanceHunter.Core.Tests`) | in-memory DTO |
| 3 | Pre-redesign items: `TraitOverride == null` ⇒ `LegacyDerivedTrait`; `"NONE"` sentinel; `Version` 1→2 bump for `"Gem"` | `SaveGame.cs:21-23, 570, 588-590` | `legacy_trait_migration_test.cs` (3 tests) | in-memory DTO |
| 4 | Pre-tier roster: empty `UnlockedCharacters` ⇒ `LegacyUnlocks.Seed(conquered, quests, depths, vowsKept)` | `SaveGame.cs:648-660`, `LegacyUnlocks.cs:60-90` | `unlocked_characters_test.cs` (8 tests; `:103-111` is a JSON literal) | JSON literal + DTO |
| 5 | Pre-gem saves: `FreeSocketUsed` inferred from socketed gems | `SaveGame.cs:667-671` | `Economy/first_gem_free_test.cs` (grep hit) | DTO |
| 6 | Retired `Charter.Merge` ⇒ `Salvage` | `Game1.cs:610-612` | none (host code) | — |
| 7 | `ChestKeepSlot` (one) ⇒ `ChestKeepSlots` (many) | `Game1.cs:704-709` | none (host code) | — |
| 8 | `SavedChest.RunTilt` 0 ⇒ 1 | `SaveGame.cs:287` default + `Game1.cs:707` | `Expeditions/chest_*` tests cover `RunTilt` semantics, not the save default specifically | — |
| 9 | **Form(+Passive) ⇒ SkillDef bridge** — `SkillDef.LegacyForm`, `SkillCatalogue.Resolve`, `SkillPick.SkillId` null path | `SkillCatalogue.cs:151-155, 646-655`; `BuildComposer.cs:87-104, 169-173` | `Builds/skill_slot_kinds_test.cs:36, 63-67` exercise `Resolve` directly; **no test loads a Form-era `SavedSkill` through `PlayerLoadout.Restore → ToBuild`** (Game layer, untestable) | in-memory |
| 10 | Duplicate `InstanceId` dedup | `SaveGame.cs:599-605` | `SaveSystemTests.cs:190-205` | DTO |
| 11 | Additive defaults (camera 0, `IntroSeen` false, empty lists, `WarrenLevel` 1, `ItemLevel` 1) | throughout `SaveGame.cs` | `dismissed_guide_test`, `onboarding_save_test`, `gift_chest_save_test`, `SaveSystemTests:358-367` | JSON literals |
| 12 | Mastery node renames (WEIGHT→RESONANCE, SPREAD→LOOT at the 2026-08-30 re-axe) | **none exists.** `Branch` is not persisted (only node ids are); `RestoreTaken` drops unknown ids (`MasteryTree.cs:178`). If node ids changed at the re-axe, those allocations were silently dropped and auto-refunded (points are derived) | — | — |
| 13 | Display prefs: volume 0-10→0-100 via marker line; window scale int → `WxH` | `DisplaySettings.cs:157-175` | none | separate file |

Git history cannot help reconstruct older shapes: `git log -S"CurrentVersion = "` returns only
`ec4f234 Recreate repository without Git LFS`.

---

## 4. Unknown-identifier policy on load

| Identifier | Behaviour | Evidence | Verdict |
|---|---|---|---|
| Skill `Form` / `Source` name in `SavedSkill` | `Enum.TryParse` fails ⇒ **slot silently dropped** | `PlayerLoadout.cs:291-292` | degrade, silent |
| `SavedSkillProgress.SkillId` | row dropped | `SkillProgress.cs:146` | degrade, silent |
| Variation / reinforcement **name** | dropped | `SkillProgress.cs:148-153` | degrade, silent |
| `VowId` | `Weaving.ById` ⇒ null ⇒ no vow | `ResonanceWeaving.cs:489`, `BuildComposer.cs:165` | degrade, silent |
| Keystone id | kept in loadout, dropped at compose if not learned | `PlayerLoadout.cs:294`, `BuildComposer.cs:146-149` | degrade, silent |
| Mastery node id | dropped; `start` re-added; points auto-refund (derived) | `MasteryTree.cs:172-179` | degrade, silent — **and `LearnedSkills()` shrinks ⇒ woven skill silently unwoven** (`BuildComposer.cs:175`) |
| Trait (dust) unlock id | dropped; Dust currency kept; points auto-refund | `MemoryDust.cs:192-198` | degrade, silent |
| Character id (active) | falls back to starter | `CharacterState.cs:120-123` | degrade |
| Character id (unlocked) | dropped | `CharacterState.cs:116-118` | degrade |
| Quest id | **kept verbatim** | `CharacterState.cs:109-112` | correct (LegacyUnlocks depends on it) |
| `Charter` name | dropped | `SaveGame.cs:617-619` | degrade |
| `HunterStat` name | rank dropped, **Gleam not refunded** | `SaveGame.cs:625` | degrade, lossy |
| `FacilityKind` name | dropped | `SaveGame.cs:677-678` | degrade |
| `ItemBaseType` name | **`Enum.Parse` throws ⇒ boot crash** (no try around `Game1.cs:700`) | `SaveGame.cs:582, 589` | **throw** |
| Region id in `RegionFarms` | **`_regions[id]` throws `KeyNotFoundException` ⇒ boot crash** | `Regions.cs:192`, `Game1.cs:737` | **throw** |
| `ActiveRegion` | `IsUnlocked` false ⇒ home | `Game1.cs:729`, `Regions.cs:197-200` | degrade |
| `GearTrait` / `EnchantKind` / `ItemClass` / `Source` names on items | `TryParse` ⇒ null; enchant then **re-rolls from id hash** | `SaveGame.cs:590-594`, `Enchantments.cs:255-260` | degrade; enchant fallback is a different roll, not "none" |
| `Rarity`, `Outcome`, `Archetype`, `Affix` ordinals | cast unguarded | `SaveGame.cs:583`, `RunLog.cs:122-125` | undefined enum value passes through |
| Chest `Element` / `Gift` | inert / rolls | `Game1.cs:706`, `Chest.cs:316` | degrade |

Two throw paths at boot (`BaseType`, region id) are the only places where a content rename can take a player's
game down before the window opens — the same failure shape the `_pending*` comments in `Game1.cs:652-684`
describe for a different cause.

---

## 5. Share codes

`ShareCodes.cs`. Format `<PREFIX><version>.<base64url(deflate(json))>.<fnv1a-hex8>` (`:240-247`).
Prefixes `RHI` item, `RHB` build, `RHF` feedback (`:34-36`) — "RH" = Resonance Hunter, product identity baked
into the wire format. `Version = 1` in the prefix digits; `version > Version` ⇒ "NEWER VERSION" (`:266-270`).

| Code | Payload | What is encoded | Form in it? |
|---|---|---|---|
| Item `RHI1` | `SavedItem` via `SaveSystem.ToSavedItem` (`:101`) | full item incl. gems, trait/enchant override names, element, class, family | no (items have no Form) |
| Build `RHB1` | `SharedBuild { List<SavedSkill> Skills, List<string> Keystones, List<string> Mastery }` (`:46-51`) | **Source, Form, VowId** per skill (WeaveScreen omits `Passive`, `WeaveScreen.cs:749-752`); keystone ids; mastery node ids. **No SkillId, no variation, no reinforcements.** | **Yes — Form is the skill identity, and without `Passive` the code cannot say which of a style's two skills was meant** |
| Feedback `RHF1` | `SharedFeedback` (`:72-96`) | build stamp, `SaveVersion`, progress numbers, `SharedBuild` (with `Passive`, `Game1.cs:943-949`), worn summary, `RunReportSave[]` | yes (via `SharedBuild`) |

Decoders: `TryDecodeBuild` validates `Source` and `Form` non-empty and ≤ 40 chars (`:224-227`) — deleting
`Form` from `SavedSkill` breaks this validation. The inspect card prints `"{Source} {Form}"` per skill
(`ChestScreen.cs:748`) and derives "DISCIPLINE" from the first Specialisation node's `.Form`
(`ChestScreen.cs:731-736`). Live consumers: encode in `ForgeScreen.cs:1620` (item), `WeaveScreen.cs:747`
(build), `Game1.cs:934` (feedback); decode/inspect in `ChestScreen.cs:666-673`. Tests: `share_codes_test.cs`
(round trips, hostile payloads, tamper, truncation, future version).

---

## 6. Risks of the planned changes, with a concrete migration each

### (a) Namespace / assembly rename `ResonanceHunter` → `IdleXIdle`
- **JSON**: zero risk. No type names, no assembly names, enum values written by member name via `ToString()`
  (§1). Renaming namespaces/assemblies does not change a single byte of `save.json`.
- **Disk folder**: `%LocalAppData%\ResonanceHunter\{save.json, save.bak, save.corrupt-*, save.newer-*, save.bak-pre-reset-*, display.txt}`
  (`SaveFile.cs:34-35`, `DisplaySettings.cs:146-147`). If the folder name changes, every existing player boots
  to "NO SAVE FOUND. STARTING FRESH." — and the very first autosave 10 s later writes a blank game into the new
  folder. Two acceptable approaches: (i) **keep the folder name** — it is invisible to the player and only a
  constant; or (ii) rename, and in `SaveFile.Dir` (Game layer, "WHERE") add a one-time adopt step: if
  `<new>/save.json` is absent and `<old>/save.json` exists, `Directory.Move(old, new)` (same volume, atomic
  enough) or copy `save.json`+`save.bak`, never delete the old folder. Put the adopt logic in Core
  (`SaveStore.AdoptLegacyDirectory(oldDir, newDir)`) so it can be tested against scratch folders like the rest
  of `save_store_test.cs`.
- **Env vars** `RH_SAVE_DIR`, `RH_SHOT`, `RH_BOOTCHECK` (`SaveFile.cs:29`, `Game1.cs:555, 859, 871-872`) —
  dev-only; rename freely but update `tools/` scripts (not audited here).
- **Share-code prefixes** `RHI/RHB/RHF` — codes already pasted in Discord must keep decoding. Either keep the
  prefixes (they are three letters nobody reads) or accept both old and new prefixes in `TryDecode`.
- `BuildStamp` reads the *Core* assembly's `AssemblyInformationalVersion` (`BuildStamp.cs:223`) — a rename does
  not break it, but feedback codes from before the rename will carry the old stamp; harmless.
- Tests: namespace `ResonanceHunter.Core.Tests` and the `.csproj`/`.slnx` names — mechanical.

### (b) Deleting `Form` from `SavedSkill`
This is the real migration. Today `Form` is the skill's only persisted identity (§2.4).
1. **Add `SkillId : string?` to `SavedSkill`** and to `PlayerLoadout.SaveSkills/Restore` (both currently drop
   it, `PlayerLoadout.cs:278-292`). New saves write `SkillId`; keep writing `Form` for one release if an
   old-build rollback matters (it is `required` on old builds).
2. **Make `Form` optional** (`string?`), and on restore: `SkillId ?? LegacySkillForm.Resolve(Form, Passive)`.
   Move the six-entry table out of `SkillDef.LegacyForm` into one isolated migration class (e.g.
   `Persistence/LegacySkillForm.cs`): `"Strike"→Hammer, "Trap"→Snare, "Mark"→Sign, "Projectile"→Volley,
   "Aura"→Field, "Transformation"→Drain`, plus the "naturally passive" set `{Aura, Trap}` that today lives in
   `FormBehaviour.IsPassive/FiresOnBeingHit` (`BuildComposer.cs:102-103`) so a `Passive == null` save resolves
   the way it does now. Then `SkillDef.LegacyForm`, `SkillCatalogue.Resolve`, `SkillPick.Form`,
   `SkillChoice.Form` and the `Form` enum can leave runtime.
3. **Bump `CurrentVersion` to 3.** Not because the read side needs it (it is lenient), but because an older
   build reading a file without `required Form` reports **Corrupt**, quarantines the player's file and locks
   saving (`SaveGame.cs:470-475`, `SaveStore.cs:47-48`, `Game1.cs:565`) — whereas `Version 3` makes it say
   "MADE BY A NEWER VERSION" and leave the file alone. This is exactly why version 2 exists (`SaveGame.cs:21-23`).
4. **Pre-migration snapshot** (see §7): copy `save.json` to `save.pre-v3-<time>.json` before the first write of
   a migrated file.
5. **Share codes**: bump to `RHB2` with `SkillId`; in `TryDecodeBuild` accept `RHB1` and map through the same
   `LegacySkillForm` table; change the validation at `ShareCodes.cs:224-227` to require `SkillId` *or* `Form`;
   change the inspect card (`ChestScreen.cs:744-751`) to print `SkillCatalogue.Find(id).Name` and the style.
   `WeaveScreen.cs:749-752` should also send `Passive`/`SkillId` (today a shared "BODY STRIKE" cannot tell
   BLOW from PRESS).
6. **Tests**: add a JSON-literal fixture of a 2026-08-29 save (`{"Source":"Shadow","Form":"Trap","VowId":…}`
   with and without `Passive`) and assert the resolved skill ids — this coverage does not exist today (§3 #9).
7. Cross-area (not persistence, but blocks deleting the enum): `Build.Affinity : Form?` and
   `MasteryNode.Form` on Spec nodes (`MasteryTree.cs:253-257`, `MasteryCatalog.cs:434-446`),
   `FormBehaviour.BaseCooldownMs(s.Form)` for `Beats == 0` skills (`BuildComposer.cs:179-180`),
   `SoloBattle.cs:709, 787, 1584, 1711`, `Character.Aptitude : Form?`.

### (c) Renaming `WarrenResource.Mastery` → `Insight`
- `WarrenResource` is **not persisted**: `WarrenFacilities` keys are `FacilityKind` names (`SaveGame.cs:516,
  677`); `FacilityInfo.Produces` is catalogue metadata. Renaming the enum member is save-safe.
- The only persisted trace of the word is the **JSON key** `WarrenMasteryPool` (`SaveGame.cs:267`). Renaming
  the C# property renames the key and silently zeroes every player's pool. Either keep the property name, or
  rename it with `[JsonPropertyName("WarrenMasteryPool")]`, or add a read-only shadow property that folds the
  old key into the new one. Same rule applies to any other property rename (`MemoryDust`, `MasteryEarned`,
  `WovenSkills`, …) — STJ matches names case-sensitively by default.

### (d) Removing `Character.Aptitude`
- Not persisted (grep `Aptitude` in `Persistence/`, `Game1.cs`, `PlayerLoadout.cs` → 0). Zero save risk.
  Roster already sets it null for the champion at `CharacterRoster.cs:56`.

### (e) Mastery node id changes
- `MasteryTaken` stores ids; unknown ids are dropped silently (`MasteryTree.cs:178`); points come back as
  Available because Earned is derived and Spent sums owned nodes. So a rename never *strands* points, but it
  **silently un-allocates** the tree and, because `LearnedSkills()` shrinks, **silently unweaves** the skills
  those road nodes taught (`BuildComposer.cs:175`).
- Migration: a `Dictionary<string,string> Renames` in `MasteryCatalog` (or `Persistence/LegacyMasteryIds.cs`)
  applied in `RestoreTaken` before validation; one JSON-literal test per rename. The Spec ids
  `spec_strike/spec_trap/spec_projectile/spec_aura/spec_mark/spec_transformation` are the obvious candidates if
  they become `spec_hammer/…`. Never reuse a retired id for a different node (same reason `SkillProgress.Restore`
  gives at `SkillProgress.cs:143-145`).
- The same lenient-drop applies to trait ids in `MemoryDustUnlocks` (`MemoryDust.cs:197`) — `recall_*`,
  `socket_*`, `weave_5`, `vow_study_*`, `ks_*` (`MemoryDust.cs:309-442`).

### (f) Making learned skills permanent (new field)
- Additive `List<string> LearnedSkills` on `SaveGame`, default empty. Copy the `UnlockedCharacters` pattern
  exactly (`SaveGame.cs:104-116`, `:648-660`): a new save always contains at least the champion's
  `StartingSkillId`, so **empty ⇒ old save ⇒ seed** from `MasteryTaken` Teaches nodes (`MasteryCatalog.cs:460-471`)
  + `StartingSkillId`. No version bump. Put the seed in Core (`SaveSystem.RestoreLearnedSkills`) so a JSON
  literal test can prove a road-walker keeps their skills after respec re-locking is removed.

### (g) Vow ids
- `SavedSkill.VowId` — unknown ⇒ null vow, silent (`ResonanceWeaving.cs:489`). Renames need a table applied in
  `PlayerLoadout.Restore` (or better, in a Core `SaveSystem.RestoreLoadout` that does not exist yet). Also in
  `SharedBuild` (codes in the wild) and `LegacyUnlocks.FirstVowId = "q_first_vow"` (a quest id, keep as is).
- Deleting a vow: the silent drop is acceptable but should surface a one-line boot note; today nothing tells
  the player their skill lost its vow (and its `RunsWithVowKept` future).

---

## 7. Atomicity and backups

- **Atomic write: yes.** `SaveStore.TryWrite` writes `save.json.tmp`, then `File.Replace(temp, path, save.bak)`
  in one call; `PlatformNotSupportedException` falls back to copy+move (`SaveStore.cs:59-94`). Temp is in the
  same directory (same volume). Tested (`save_store_test.cs:327-341, 403-419`).
- **Rolling backup: one generation.** Every write rotates the previous file into `save.bak`; corrupt load
  quarantines the corpse and promotes `save.bak` (`Game1.cs:562-575`); reset preserves the backup aside
  (`Game1.cs:977-979`); a newer-version file is set aside, never deleted (`SaveStore.cs:132-133`).
- **Backup on migration: no.** Because there is no migration step there is no pre-migration snapshot. After an
  upgrade, the first save rotates the old-format file into `save.bak` and the **second autosave, ten seconds
  later, rotates it out for good** (`Game1.cs:512`). A format change that mis-reads a field (e.g. a Form that
  resolves to the wrong sibling) is unrecoverable after ~20 s. Recommendation: in `SaveSystem.Deserialize`
  expose `save.Version < CurrentVersion` (or "a legacy fallback fired") and have `SaveStore` copy the original
  to `save.pre-v<N>-<time>.json` once before the first write of that session. Cheap, testable in
  `save_store_test.cs`, and the same shape as the existing quarantine names.
- **Write latch**: a session whose load failed (`Corrupt`/`FromNewerVersion`) never writes (`SaveStore.cs:47-48`,
  `Game1.cs:851`) — good.
- **Screenshot/boot-check guards** at the single write site (`Game1.cs:859-872`) — good.

---

## 8. Dead fields (with the grep)

| Field | Declared | Evidence |
|---|---|---|
| `SaveGame.Affinity` | `SaveGame.cs:159` | `grep -rn "save\.Affinity\|Affinity = \"" src tests` → 0; not in `Capture` (`:509-554`) nor the host `with` (`Game1.cs:874-926`) |
| `SavedItem.EquippedToCreatureId` | `SaveGame.cs:368` | `grep -rn "EquippedToCreatureId *=" src` outside Persistence → 0 setters; readers `Forge.cs:101`, `HunterProgression.cs:531` see null forever |
| `SaveGame.RegionMasteryPoints` (legacy single-region) | `SaveGame.cs:51` | still written (`:552`), read only if `RegionFarms.Count == 0` (`Game1.cs:743`) — a mirror no current save can trigger |
| `SaveGame.ChestKeepSlot` | `SaveGame.cs:237` | written as mirror (`Game1.cs:909`), read only if `ChestKeepSlots` empty (`:707`) |
| `ItemBaseType.CreatureCore` (persisted name) | `LootSystem.cs:13` | retired concept; removing it is a boot crash for any save holding one (§4) |

---

## 9. Stale terms in this area

| Term | Where | Replacement |
|---|---|---|
| `ResonanceHunter` save folder | `SaveFile.cs:35`, `DisplaySettings.cs:147` | keep or adopt-move (§6a) |
| `RH_SAVE_DIR`, `RH_SHOT`, `RH_BOOTCHECK` | `SaveFile.cs:29,42`, `Game1.cs:555,859,871` | `IXI_*` — dev knobs only |
| `RHI/RHB/RHF` code prefixes | `ShareCodes.cs:34-36` | keep, or accept both |
| "woven skill", "Source × Form × Vow" | `SaveGame.cs:141, 293`, `ShareCodes.cs:44`, `PlayerLoadout.cs:12-52` | "equipped skill", STYLE → SKILL |
| `MasteryEarned` (holds deepest wave) | `SaveGame.cs:165`, `Game1.cs:641, 913` | `DeepestWave` via `[JsonPropertyName("MasteryEarned")]` |
| `WarrenMasteryPool` | `SaveGame.cs:267` | `WarrenInsightPool` with `[JsonPropertyName]` |
| `Memory Dust` naming vs "Trait Points" | `SaveGame.cs:60-61`, `MemoryDust.cs:113, 139` | persisted `MemoryDust` is the Dust currency; `MemoryDustUnlocks` are trait ids — rename the list, keep the key |
| GDD `save-load-persistence.md` | whole file (dated 2026-07-14) | describes a per-section envelope, `%AppData%`, `.incompatible-v<N>`, async writes, creature roster — none of it is what ships |

---

## 10. Good to preserve

- Pure `Serialize`/`Deserialize` string functions with injected `nowMs` (`SaveGame.cs:450-491`).
- `LoadResult`/`LoadFailure` and the never-throw contract at the parse boundary (`:456-477`).
- Names-not-ordinals for every catalogue key with a comment saying why (`:43-45, 185-186`).
- "Empty means old, and a new save is never empty" as the version-less migration signal (`:104-116`).
- Flattened DTOs that never mirror a gameplay record (`:312-317`).
- `SaveStore` disk mechanics in Core with real-directory tests (`SaveStore.cs:10-25`).
- The write latch on failed load and the single write site with all guards on it.
- `LegacyUnlocks` as a frozen table with "must never change again" written on it (`LegacyUnlocks.cs:18`).
- `ShareCodes` reusing the save DTO so codes inherit the same lenient parsing (`ShareCodes.cs:24-26`).

## 11. Open questions

1. Can the weave screen actually produce a build with more *named* actives than `ActiveSlotsFor(cap)`? If yes,
   the reload re-resolves the overflow to the passive sibling (§2.4) — a real identity change on reload; if the
   screen prevents it, the lossy round trip is latent, not live. Not verifiable from the persistence files.
2. Did any mastery node ids change at the 2026-08-30 re-axe (WEIGHT→RESONANCE)? Branch enum renamed; node ids
   in `MasteryCatalog.cs` look stable, but with squashed git history it cannot be proven.
3. `SharedBuild` from `WeaveScreen` omits `Passive` while the feedback code includes it — intended?
4. Should `Charter.Merge` stay in the enum forever (like `LegacyUnlocks`) or be moved into a name-only
   migration table so the enum can lose it?
5. Is `ItemBaseType.CreatureCore` still mintable anywhere, or only present in old saves? Its removal is a boot
   crash either way until `FromSavedItem` uses `TryParse`.
6. Is the 10-second autosave + one-generation backup an accepted trade-off, given it also bounds how long a
   bad migration is recoverable?
