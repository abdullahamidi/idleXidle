# Verification — warren-economy audit (adversarial pass)

**Date**: 2026-08-31 · **Branch**: `feat/hunter-cutout-rig` (clean) · **Mode**: read-only on source; `bin/ obj/ .git/` excluded.
**Method**: every destructive claim in `warren-economy.md` was attacked with `grep -rn --include=*.cs` over `src` AND `tests`,
then the cited lines were read. The auditor scoped most of its greps to `src`; the main correction below is that
**tests reference nearly every symbol proposed for A-delete**, so several "delete" items are rewrite-the-tests items.

Verdict scale: **CONFIRMED** = claim and detail hold · **PARTIAL** = direction right, a detail or the scope is wrong ·
**REFUTED** = concrete contrary evidence. No claim was refuted outright.

**Tally**: 39 claims — 26 CONFIRMED · 13 PARTIAL · 0 REFUTED.

---

## Verdict table

| # | Claim | Verdict | Evidence |
|---|---|---|---|
| 1 | INSIGHT (`WarrenResource.Mastery`, `_warrenMasteryPool`, `WarrenMasteryPool`, `MasteryCostBase/Growth`, `MasteryBonusPerLevel`) is a closed loop, non-binding, no Core owner → replace | **CONFIRMED** | `grep -rn "_warrenMasteryPool\|MasteryOwned\|WarrenMasteryPool" src tests` → `SaveGame.cs:267,517`; `Game1.cs:306,651,754(comment),768,876(Capture arg),993,1091,1114,1119,1123,1971(fixture)`; `WarrenScreen.cs:37,373,376`. Produced `Game1.cs:768/1091`, spent `1123` only. Derived 267/min vs 54 first cost checks out: `(122+118)×(1+0.10+0.013)=267`, `round(43×1.25)=54` (`Warren.cs:52-53,87-88,96,155`). `game-flow.md:229` and `:302-303` (auditor wrote 301) say Gleam + materials only. **Deletion detail the report omits**: `Tick` hard-codes three slots — `_carry = new double[3]` (`Warren.cs:179`), `new long[3]`/`i < 3` (`:304-305`), positional `WarrenYield(Gleam, Mastery, Dust)` (`:65,:312`), `WarrenCost` (`:62`); `WarrenTests.cs:88` and `WarrenScreen.cs:244` iterate all three. |
| 2 | `Charter.Merge` migration-only; keep enum + migration, delete its Name/Blurb/Short/Plain rows | **PARTIAL** | Migration-only is right: `WaveSpoils.cs:71` pool = Refine/Reforge/Salvage; `Game1.cs:613` converts to Salvage; `charters_test.cs:102` asserts Merge never drops; `ForgeScreen.cs:397-399` dev fixture adds only the three. **But the "rows" are the `_ =>` default arms** (`Charters.cs:53,62,71,88`), not Merge-specific cases — removing them leaves four switch expressions non-exhaustive for an enum that still contains `Merge` (runtime `SwitchExpressionException`), and `charters_test.cs:28-41` iterates `Enum.GetValues<Charter>()` asserting every member has non-empty Name/Blurb/Short/Plain and a Blurb containing "One". `charters_test.cs:126-143` also uses `Charter.Merge` as its restore fixture. Keep the text while the member exists, or delete both and rewrite the tests. |
| 3 | `SaveGame.RegionMasteryPoints` legacy: stop writing, keep fallback read one version | **PARTIAL** | Write `SaveGame.cs:552`; read only when `save.RegionFarms.Count == 0` (`Game1.cs:731-743`) — confirmed. **Tests pin the write**: `SaveSystemTests.cs:52` `Assert.Equal(region.RegionMasteryPoints, save.RegionMasteryPoints)` on a fresh Capture; `FullLoopTests.cs:88` same. `SaveSystem.Capture`'s `Region region` parameter (`SaveGame.cs:504`) exists chiefly to feed this field. "Stop writing" = change Capture's signature/contract and two tests, not a one-line drop. |
| 4 | Delete `Region.ParClearTimeSeconds`, `Region.IdleEfficiencyPercent`, `EfficiencyContract.IdleBand/IdleEfficiencyPercent/MaxIdleEfficiencyPercent`, `MasteryLevel.OptimizedTeam` | **PARTIAL** | Dead in `src` confirmed: sole runtime reader is the Map label `MapScreen.cs:529`; offline pay `Game1.cs:773` has no region term; `MasteryLevel` getter (`RegionAutomation.cs:101-109`) never returns `OptimizedTeam`. **Not dead in tests**: `RegionFarmTests.cs:73-79` (IdleBand/IdleEfficiencyPercent), `:94` (ParClearTimeSeconds); `EfficiencyContractTests.cs` (entire file — IdleBand, IdleEfficiencyPercent, OptimizedTeam, `AutomationClearTimeSeconds`); `MemoryDustTests.cs:238` `(int)MasteryLevel.OptimizedTeam`. **Constructor chain**: `Region(string, int parClearTimeSeconds, …)` (`RegionAutomation.cs:47-50`) is fed by `World` via `FarmParSeconds` (`Regions.cs:175,186-190`) — deleting the property orphans that helper. See Missed #1 for why the `OptimizedTeam` delete is the one to do carefully. |
| 5 | `LootContext.ActiveEfficiencyPercent` never assigned; term always 0 → delete field + term | **PARTIAL** | The type is `KillContext` (`LootSystem.cs:182`), not `LootContext`. Never assigned in `src` confirmed — the only construction `ExpeditionLoot.cs:93-103` omits it. **Three tests exercise it**: `LootSystemTests.cs:14-23` (`eff` param), `:114` and `:123` (`eff: 300f`), `:137` (`eff: 290f`). Deleting the field/term deletes or rewrites those tests. |
| 6 | `WarrenScreen.Draw(SpriteBatch)` overload has no caller → delete | **CONFIRMED** | `grep -rn "_warrenScreen\.Draw"` → `Game1.cs:1116` (3-arg) only; `WarrenScreen` is `sealed`, no interface (`WarrenScreen.cs:19`). Same dead overload exists on `MapScreen.cs:272` (only call `Game1.cs:4986`, 3-arg) — see Missed #3. |
| 7 | `Warren.OnMilestone`, `WarrenYield.Any` no consumer → delete | **CONFIRMED** | `grep -rn OnMilestone src tests` → `Warren.cs:133` only. `grep -rn "\.Any\b" src tests \| grep -v "\.Any("` → nothing (no property read anywhere). Host checks `> 0` per field `Game1.cs:1089-1090`. |
| 8 | `Haul.Cores` naming is a remnant → rename `CoreMaterial` / route through WaveSpoils | **PARTIAL** | Channel is live as a Core-material faucet: `Game1.cs:3646-3649`; written by HARVEST/LODESTONE only (`SoloExpedition.cs:489-490`, `WaveModel.cs:324-327`). **Rename reaches tests**: `charge_keystone_test.cs:118-119,167` and `TriggerLivenessTests.cs:404` assert on `Bonus.Cores`/`bonus.Cores`. **And two comments are false today**: `WaveModel.cs:317-318` "nothing in the game consumes this channel any more — the host stopped reading Haul.Cores" and `SoloExpedition.cs:488` "nothing consumes it" — contradicted by `Game1.cs:3646`. Not in the report's false-comment list (claim 10). |
| 9 | Warren GDD / ADR-004 / progression.md / automation GDD are stale → rewrite / supersede / archive | **CONFIRMED** | `warren-facilities.md:6` (missing spec file), `:13-14,:71` (CREATURES/AutomationScreen), `:28` (Mastery Points), `:32-34,:57-58,:68` (SetEarned), `:44` (Nursery 520 vs code 17), `:50` (`18700×1.40^L` vs code `180×1.50^L`), `:86`. `ADR-004:5` Status **Accepted**, `:29-33` pool→SetEarned, `:35-38` creature den preserved. `progression.md:27,31-35,59-62`. `game-flow.md:229,296-305,475`. Automation GDD: 956 lines, 71 case-insensitive "creature" hits (auditor said 62), 0 "warren". |
| 10 | False comments at `Warren.cs:9-11,35`; `SoloExpedition.cs:492`; `SaveGame.cs:259-266`; `MemoryDust.cs:150-154`; `WarrenScreen.cs:16-17`; `HunterProgression.cs:105-110,519-524` | **CONFIRMED** | Each read. `Warren.cs:9-11` says Mastery funds the build tree (it does not — only `Game1.cs:3714` feeds `SetEarned`, from depth). `Warren.cs:35` / `SoloExpedition.cs:492` cite 79,578; `HunterProgression.cs:17-25` constants (25 × 1.13^r, cap 60, nine stats) give ≈2.65M and `:20-21` says so in its own comment. `SaveGame.cs:263-264` "host adds it into SetEarned" — false. `MemoryDust.cs:152` "ONLY faucet" — five call sites. `WarrenScreen.cs:16-17` — `grep CREATURES WarrenScreen.cs` → that comment only. `HunterProgression.cs:104` "creature's evolution", `:522` "equipped to a creature". **Caveat on :519-524**: the gate is live code, not just prose — see Missed #6. **Add** `WaveModel.cs:317-318`, `SoloExpedition.cs:486-488` (claim 8). |
| 11 | DEAD `Region.ParClearTimeSeconds` | **CONFIRMED** (in src) | Only reader `RegionFarmTests.cs:94`; set via ctor from `Regions.cs:175`/`FarmParSeconds` (`:186-190`). `WorldTests.cs:116` reads `RegionDefinition.Boss.ParClearTimeSeconds` — a different type, not a `Region` reader. |
| 12 | DEAD `Region.IdleEfficiencyPercent()` / `EfficiencyContract.IdleBand` | **CONFIRMED** (in src) | `MapScreen.cs:529` label only; `Game1.cs:773` pays `ChampionGleamRate × 0.5` regardless of region. Test readers: `RegionFarmTests.cs:73-79`, `EfficiencyContractTests.cs:24-27,44`. |
| 13 | DEAD `MasteryLevel.OptimizedTeam` — "grep OptimizedTeam src → only comments + enum + IdleBand branch" | **PARTIAL** | Never returned at runtime — confirmed (`RegionAutomation.cs:105-107`; `RegionFarmTests.cs:52-62` pins it). **But the value is load-bearing in a test**: `MemoryDustTests.cs:237-238` computes the trait-point budget as `Regions.All.Count × (int)MasteryLevel.OptimizedTeam` (= 3/region → 34), a ceiling `Game1.TraitPointsEarned` (`Game1.cs:5197-5198`) can never reach because the live getter tops at `FullyMastered` (= 2/region → 28). `EfficiencyContractTests.cs:27,39` also use it. Deleting the member is a compile break in tests and exposes a false invariant — Missed #1. |
| 14 | DEAD `LootContext.ActiveEfficiencyPercent` | **PARTIAL** | Same as #5: `KillContext`, never assigned in src, but exercised by `LootSystemTests.cs:21,114,123,137`. |
| 15 | DEAD `Warren.OnMilestone` | **CONFIRMED** | Declaration only (`Warren.cs:133`). |
| 16 | DEAD `WarrenYield.Any` | **CONFIRMED** | No `.Any` property read in src or tests. |
| 17 | DEAD `WarrenScreen.Draw(SpriteBatch)` | **CONFIRMED** | Only `Game1.cs:1116` (3-arg). |
| 18 | `SaveGame.WarrenMasteryPool` live-but-closed; no test round-trips it | **CONFIRMED** | `grep WarrenMasteryPool tests` → nothing. All 16 `SaveSystem.Capture(` sites in tests omit the `warrenMasteryPool` argument (defaults 0, `SaveGame.cs:508`). `SaveSystemTests.cs:112-113,142-143` round-trip only `WarrenLevel`/`WarrenXp`. Field born `3d976a4` 2026-07-28. |
| 19 | BRANCH `Warren.cs:207-212` on `WarrenResource`; Tick indexes by `(int)` so enum order is load-bearing | **CONFIRMED** | `ResourceBonus` switch `:207-212`; `Tick` `:305-312` `(WarrenResource)i` → `outv[i]` → positional `new WarrenYield(outv[0], outv[1], outv[2])`. Tuning only. |
| 20 | BRANCH `WarrenScreen.cs:79-118` presentation switches; `ResFromLabel` string→enum round trip | **CONFIRMED** | `ResColor :79-82`, `ResGlyph :88-99`, `ResFromLabel :101-107`, `ResName :109-118`. `ResFromLabel` is called only from `DrawReq` (`:401`), which is called with literals `"GLEAM"/"INSIGHT"/"DUST"` at `:372-374`. |
| 21 | BRANCH `WarrenScreen.cs:291` icon key from `FacilityKind` name — the only runtime use of the kind | **CONFIRMED** | `icon_facility_{f.Kind.ToString().ToLowerInvariant()}`. No `switch` on `FacilityKind` anywhere in src. The other name-derived use is the save key (`SaveGame.cs:516` ToString / `:678` TryParse) — a key, not a branch. |
| 22 | Three Dust faucets hardcoded in host (`Game1.cs:2745-2750, :3721, :4976`); no Core owner, no test pins amounts | **PARTIAL** | `:2749` `15 * reward` and `:3721` `40 * RewardMultiplier` are inline host literals with no test — confirmed. **`:4976` calls `CorruptionScaling.DeepeningDustAward(tier)`** — a Core owner (`CorruptionScaling.cs:30-31`, `60 × clamp(tier)`) with tests (`CorruptionScalingTests.cs:37-38,58`: positive, monotonic, clamped; the literal 60 is not pinned). Two of three, not three of three. |
| 23 | COUPLING `DrawWarren` spends/upgrades/saves inside the Draw pass | **CONFIRMED** | `Game1.cs:4065` → `DrawWarren :1102-1127`; spend `:1122-1124`, `Upgrade :1125`, `Save() :1126`. Forge draws the same way (`:4059`). |
| 24 | COUPLING `_warrenMasteryPool` is a currency balance owned by the host | **CONFIRMED** | `Game1.cs:306`; passed to Core as a bare `long` (`SaveGame.cs:508`, `Game1.cs:876`). |
| 25 | COUPLING `FacilityLevelCap` derived only inside `DrawWarren` | **CONFIRMED** | `grep FacilityLevelCap src` → `Game1.cs:1111` only; default `int.MaxValue` `Warren.cs:251`. The offline path (`:764`) ticks but never upgrades, so no live bug today. |
| 26 | COUPLING `WarrenScreen.cs:129` calls static `Game1.ToOverlay` | **PARTIAL** | The call exists. **It is the house convention, not a Warren coupling**: `grep -l "Game1\.ToOverlay" src` → BuildScreen, CharacterScreen, ChestScreen, ForgeScreen, MapScreen, PrestigeScreen, RosterScreen, StatsScreen, WarrenScreen, WeaveScreen (10 files). Fixing one screen alone would be inconsistent; the finding belongs to the screens/host area. |
| 27 | COUPLING `MapScreen.cs:529` states a mechanic the sim does not have | **CONFIRMED** | Label reads `farm.IdleEfficiencyPercent()`; payout `Game1.cs:773` has no per-region term. **Worse than stated**: the line is drawn `if (unlocked)` (`MapScreen.cs:523`), so an unlocked-but-unconquered region — which has no Warren at all (`Unlocks.cs:151`) — reads "EARNS 25% WHILE YOU ARE AWAY". |
| 28 | SAVE-RISK renaming `WarrenMasteryPool` loads as 0 | **CONFIRMED** | `grep JsonPropertyName src` → none anywhere in `SaveGame.cs`; `JsonSerializerOptions` (`SaveGame.cs:444-448`) leaves `UnmappedMemberHandling` at its Skip default; `:53-56` documents reliance on that. |
| 29 | SAVE-RISK `WarrenFacilities` keys are `FacilityKind` names | **CONFIRMED** | `SaveGame.cs:516` `kv.Key.ToString()`; `:678` `Enum.TryParse` drops unknown; `Warren` ctor seeds every facility at 1 (`Warren.cs:184-185`) and `Restore` only overwrites present keys (`:325-326`). |
| 30 | SAVE-RISK `WarrenResource` enum order (runtime, not save) | **CONFIRMED** | `Warren.cs:305-312`: `_carry[i]` and `outv[i]` by index; yield constructed positionally. Reorder → wrong slot. Enum name never serialised (`WarrenYield`/`WarrenCost` positional records `:62,:65`). |
| 31 | SAVE-RISK `MasteryEarned` holds deepest-ever; renaming "breaks every save" | **PARTIAL** | Holds depth: `Game1.cs:641` restore, `:913` capture, `:252` field doc; feeds `DeepestAnywhere` `:5205`. **"Breaks every save" overstates** — an unpinned rename loads `_deepestEver = 0` silently, not a load failure; the Warren cap self-heals from per-region `BestDepth` (`:5206`) and `_deepestEver = Math.Max(_deepestEver, _expedition.Deepest)` (`:3683`) on the next run. What regresses until then: trader level (`:2663`), Stats HighestWave (`:2729`), Unlocks `DeepestWave` (`:938`), quest snapshots (`:2902,2920,2995-2996`). `SaveSystemTests.cs:329,346` round-trips the JSON name, so a pin is testable. |
| 32 | SAVE-RISK `RegionMasteryPoints` legacy still written | **PARTIAL** | As #3: correct, but `SaveSystemTests.cs:52` and `FullLoopTests.cs:88` assert the write. |
| 33 | SAVE-RISK `Warren._carry` not persisted | **CONFIRMED** | `Warren.cs:179`; `grep -n carry SaveGame.cs` → no field. ≤1 unit/currency per load. |
| 34 | SAVE-RISK `SavedAtMs` — none | **CONFIRMED** | `SaveGame.cs:26,484` (floor at 0), `:691-694` (24 h clamp); `SaveSystemTests.cs:442-458`. |
| 35 | DUPLICATE bonus-strip literals vs `WarrenTuning` | **CONFIRMED** | `WarrenScreen.cs:323-326` `"2% A LEVEL"`, `"1.3% A LEVEL"`, `"0.9% A LEVEL"`, `"3% A LEVEL AFTER 1"` vs `Warren.cs:74-77` `0.03/0.02/0.013/0.009`. |
| 36 | DUPLICATE three affordability checks | **CONFIRMED** | `WarrenScreen.cs:376` literal; `Warren.CanAfford :269-273`; host `CanUpgrade` `Game1.cs:1119`. The host's check is the authoritative gate and must stay; the removable duplicate is the screen literal (pass the model's `CanAfford`). |
| 37 | DUPLICATE `DeepestAnywhere()` vs `SkillPointsEarned()` depth walks + `_deepestEver` shadow | **CONFIRMED** | `Game1.cs:5203-5208`, `:5142-5148`; `TraitPointsEarned :5198` and `QuestSnapshot :5171` also walk `Regions.All`. |
| 38 | DUPLICATE live vs offline credit of `WarrenYield` | **CONFIRMED** | `Game1.cs:1088-1091` vs `:764-768`. Only difference: live guards `> 0`, offline does not. |
| 39 | DUPLICATE Dust faucets — one in Core, two inline in host | **CONFIRMED** | `:2749`, `:3721` inline; `:4976` → `CorruptionScaling.DeepeningDustAward`. Wording here is accurate (contrast #22). |

---

## Things the auditor missed

1. **The trait-tree budget is asserted against an unreachable tier — and the live budget cannot buy a single terminal.**
   `MemoryDustTests.cs:237-238` computes "earnable at full content" as `6 + 2×5 + 6×(int)MasteryLevel.OptimizedTeam = 34`
   and asserts one keystone path (30, per its own remark `:227`) is affordable. But `Region.MasteryLevel`
   (`RegionAutomation.cs:101-109`) never returns `OptimizedTeam`; the live `Game1.TraitPointsEarned` (`:5197-5198`) sums
   `(int)MasteryLevel` per region, topping at `FullyMastered = 2` → **28**, below both the test's 30 and `Game1.cs:5194`'s
   "cheapest terminal path of 31". `Game1.cs:5159,5194-5196` repeats the 34 figure. The test passes only because it reads
   a dead enum member. Derived from the code, not measured by running the game. This is the house bug species inside the
   report's own §5 "Trait points" row, and it is what the proposed `OptimizedTeam` delete (claims 4/13) would surface.
2. **Two more false comments, on the very channel the report wants renamed**: `WaveModel.cs:317-318` ("nothing in the game
   consumes this channel any more — the host stopped reading `Haul.Cores`") and `SoloExpedition.cs:488` ("nothing consumes
   it") are contradicted by `Game1.cs:3646-3649`, which pays `Haul.Cores` out as `Material.Core`. `WaveModel.cs:320-321`
   even says the opposite two lines later.
3. **`MapScreen.cs:272` carries the identical dead `Draw(SpriteBatch)` overload** as `WarrenScreen.cs:124`; the only call is
   the 3-arg one at `Game1.cs:4986`. Same fix, same commit.
4. **Tests that break on the report's A-deletes were not inventoried.** The report says "tests that would catch a botched
   migration: none" (true for the Warren save) but does not list the tests that pin what it wants removed:
   `RegionFarmTests.cs:73-79,94`; `EfficiencyContractTests.cs` (whole file); `MemoryDustTests.cs:238`;
   `LootSystemTests.cs:21,114,123,137`; `SaveSystemTests.cs:52` + `FullLoopTests.cs:88` (legacy `RegionMasteryPoints`
   write); `charters_test.cs:28-41,126-143` (`Charter.Merge` text and fixture); `charge_keystone_test.cs:118-119,167` +
   `TriggerLivenessTests.cs:404` (`Bonus.Cores`).
5. **Deleting `WarrenResource.Mastery` is not the mechanical rename fallout the report lists.** The report enumerates
   compile sites for a *rename*; a *delete* also hits the hard-coded three-slot `Tick`/`_carry` (`Warren.cs:179,304-312`),
   the positional `WarrenYield`/`WarrenCost` records (`:62,:65`), `Capture`'s `long warrenMasteryPool` parameter
   (`SaveGame.cs:508,517`), and the three-resource loops at `WarrenScreen.cs:244` / `WarrenTests.cs:88`.
6. **`EquippedToCreatureId` is a dead field with two live gates, and a legacy-save trap.** `HunterProgression.cs:531` and
   `Forge.cs:101` still refuse Sell and every Forge operation for an item whose `EquippedToCreatureId` is non-null. Nothing
   in `src` ever sets it except the save round-trip (`SaveGame.cs:567` out, `:587` back in), and no creature exists to
   unequip from — so an item in a pre-2026-08-24 save that was "equipped to a creature" (exactly the shape of the legacy
   fixture at `SaveSystemTests.cs:122`) loads permanently un-sellable, un-mergeable, un-salvageable. The report filed
   `HunterProgression.cs:519-524` as a stale *comment*; it is a live branch on a field that should be dropped from the
   schema with a clearing migration.
7. **`World.FarmParSeconds` (`Regions.cs:186-190`) and the `Region(string, int parClearTimeSeconds, …)` constructor
   parameter exist only to feed `ParClearTimeSeconds`.** The delete in claim 4 orphans a `World` helper and changes a
   constructor that `WorldTests`, `RegionFarmTests` (`NewRegion()`), and `SaveSystemTests.cs:470` (`new Region("r", 15)`)
   all call.
8. **The "EARNS X% WHILE YOU ARE AWAY" line is shown for unconquered regions** (`MapScreen.cs:523` gates on `unlocked`,
   not `conq`), while the Warren — the only away-earner — requires a conquest (`Unlocks.cs:151`). The report frames the
   label as stating a non-mechanic; it also states it for regions that could not earn under any mechanic.
9. **`Game1.ToOverlay` is a ten-screen convention** (see #26), so the coupling finding is misfiled under the Warren.

---

## Corrections to the report's proposed actions

| Report action | Correction |
|---|---|
| Delete `Charter.Merge` text rows | Keep the `_ =>` arms while `Merge` is in the enum; or remove `Merge`, the arms, the `Game1.cs:613` migration (after one version) and rewrite `charters_test.cs:126-143` on another charter. |
| Stop writing `RegionMasteryPoints` | Also drop the `Region region` parameter from `SaveSystem.Capture` (or keep it for nothing) and update `SaveSystemTests.cs:52`, `FullLoopTests.cs:88`. |
| A-delete `OptimizedTeam` | Do it — but rewrite `MemoryDustTests.cs:237-238` against `FullyMastered` first, watch it fail at 28 < 30, and take the trait-budget question to the owner. That is a design decision, not a cleanup. |
| A-delete `KillContext.ActiveEfficiencyPercent` | Also delete `LootSystemTests.cs:114-137`'s efficiency cases and the `eff` parameter of its `Kill()` factory; fix `EfficiencyContract.cs:74` cross-reference. |
| A-delete `Region.ParClearTimeSeconds` / `IdleEfficiencyPercent` / `IdleBand` | Also delete `World.FarmParSeconds`, the ctor parameter, `RegionFarmTests.cs:64-95`, `EfficiencyContractTests.cs`; then `EfficiencyContract.cs` is empty and goes too. |
| Rename `Haul.Cores` | Also fix `WaveModel.cs:317-318`, `SoloExpedition.cs:486-488` comments and the two test files reading `.Cores`. |
| "Three Dust faucets… no Core owner, no test" | Two faucets (`15×`, `40×`); `DeepeningDustAward` already lives in Core and is tested. Move the two, don't duplicate the third. |
| `_deepestEver`/`MasteryEarned` "breaks every save" | Silent zero, self-healing for the Warren cap; pin with `[JsonPropertyName("MasteryEarned")]` and add a round-trip assertion beside `SaveSystemTests.cs:346`. |
