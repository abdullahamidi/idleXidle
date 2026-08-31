# Audit — Encounters, Expeditions, Wave Model, Replay/Events, Run Reports (and the orphaned Core combat)

- **Date**: 2026-08-31 · **Branch**: `feat/hunter-cutout-rig` · **Mode**: read-only source audit
- **Scope**: `src/ResonanceHunter.Core/Encounters/*`, `Expeditions/*`, `Builds/SoloExpedition.cs`, `Combat/AttackBias.cs`, `Automation/RegionAutomation.cs`, their consumers in `src/ResonanceHunter.Game/*` and their tests under `tests/`.
- **Method**: every file in scope read in full; every claim of "dead"/"orphan" is backed by a `grep -rn` over `src` and `tests` (bin/obj excluded) whose result is quoted. Line numbers are from the working tree at audit time.

---

## 0. Headline

The area is in far better shape than the "squad expedition Core is test-only" memory suggests: `SoloExpedition`, `WaveReplay`, the band/archetype/affix content, run reports, chests and checkpoints are all **live** for the solo champion loop. The genuine orphan is the **encounter-template / par-time / efficiency-contract chain** (`EncounterTemplate`, `CreatureTemplate`, `SpawnTuning`, `EncounterSpawner`, `VerdantHollow.Templates`, `RegionDefinition.Templates/Boss`, `Regions.AllTemplates`, `World.FarmParSeconds`, `Region.ParClearTimeSeconds`, `EfficiencyContract`): nothing in the running game reads it except one map caption whose number no earnings path uses. The area's worst liveness defect is that **four of the ten band affixes (WARDED, LEGION, ENTRENCHED, HOLLOW) have no rule anywhere** while six authored bands carry them and the HUNT header/report print their names. The event model is live and well-consumed but is **keyed on the legacy `(Source, Form)` pair** — the Skill/Aura event's payload, `WaveReplay`'s cast queries, `ReplaceBuild`'s slot identity and the screen's rail all identify a skill by Form — so the STYLE→SKILL migration cannot finish without changing `BattleEvent`. There is **no headless fast-forward**: the descent state machine lives in `SoloExpeditionScreen` (MonoGame), offline progress is a gleam rate × seconds × 0.5 approximation, and loot uses unseeded host RNGs. Enemy health is the product of **eleven multipliers from seven files**, three of which (`RegionLadder`, `RegionModifiers`, `CorruptionScaling`) plus `RegionDrops.RarityTilt` are all "deeper region = harder + better loot" curves.

---

## 1. Live vs orphan inventory (Q1)

### 1.1 LIVE for the solo loop (keep)

| Type / file | Producer → consumer evidence |
|---|---|
| `RegionDefinition` (Id, Name, Theme, PrereqId, CombatBias) — `Encounters/Regions.cs:10-25` | `Game1.cs:3470-3477` (`Regions.Get(_activeRegion)`, `.Theme`, `.Id`, `.CombatBias`), `MapScreen.cs:350-560` (`.Theme`, `.Name`), `Regions.All/Get/Find/Next` throughout Game1 |
| `Regions.RecommendedPower` — `Regions.cs:48-61` | `MapScreen.cs:165` |
| `World` (conquest, unlock, corruption, farms) — `Regions.cs:167-273` | `Game1.cs:724,762,939-940,3718-3720,4952-4979`; `MapScreen.cs:589-590` |
| `Affix`, `BandDefinition`, `Bands.*` — `Encounters/Bands.cs` | `SoloExpedition.cs:276-332` (CycleIndex, AffixesOf, RepeatScale, Seed, Roll, Health/Damage/Defence/Extra/Interval/Sustain multipliers); `RunReport.cs:178-179` (BandOf) |
| `BandCycles.For/RosterFor/BandFor` — `BandCycles.cs:304-331` | `SoloExpedition.cs:276,296`; `WeaveScreen.cs:1319` (roster shown on the build screen) |
| `Archetype`, `ArchetypeShape`, `Archetypes.Compose` — `Archetypes.cs` | `SoloExpedition.cs:295-296`; `SoloBattle.cs:840` (SIEGE reads `WaveCreature.Archetype`); `SoloExpeditionScreen.cs:1515-1520,1645,2430` |
| `Checkpoints` — `Checkpoints.cs` | `Game1.cs:3499-3500,3781,4943`; `MapScreen.cs:50,133-151`; `SoloExpeditionScreen.cs:825,2281`; `Tutorial.cs:243,276`; `Quests.cs:165` |
| `CorruptionScaling`, `CorruptionLook` | `Game1.cs:373,2748,3602,3721,3744,4962-4979`; `MapScreen.cs:589-590`; `SoloExpeditionScreen.cs:1899,2496,3550` |
| `RegionLadder` | `Game1.cs:3614-3615`; `MapScreen.cs:290`; `Onboarding.cs:338` |
| `RegionDrops` / `RegionDropProfile` (Favoured, Blurb, RarityTilt) | `ExpeditionLoot.cs:91` (via `Chests.Open`), `ChestDossier.cs:178`, `MapScreen.cs:548-569` |
| `RegionModifiers` | `Game1.cs:3603,3745`; `MapScreen.cs:508` |
| `AttackBias` — `Combat/AttackBias.cs` | `Regions.cs:20,69-96` → `Game1.cs:3477` → `SoloExpedition.cs:60,104-109,264` |
| `Region` (progress record) `BestDepth/RecordDepth/StartWave/MasteryLevel/RecordActiveKill` — `Automation/RegionAutomation.cs` | `Game1.cs:737,2537,3486,3499,3652,3697,4943,5146,5171,5197,5206`; `MapScreen.cs:133-140` |
| `ExpeditionTuning`, `WaveOutcome`, `BattleEventKind`, `BattleEvent`, `WaveBonus`, `Haul`, `WaveScaling` — `Expeditions/WaveModel.cs` | `SoloExpedition.cs` (all), `SoloBattle.cs` (events, tuning), `SoloExpeditionScreen.cs:809,888,1195-1305,1333,1436,2845`, `Game1.cs:3739` |
| `WaveReplay` — `Expeditions/WaveReplay.cs` | **LIVE, not an orphan.** `SoloExpeditionScreen.cs:393,904,911` construct it every wave; 20+ member reads (`Advance` ×3, `CreatureAlive` ×5, `BeatAt` ×4, `NextEnemyStrikeAfter` ×3, `NextChampionStrikeAfter` ×3, `LastSkillBefore` ×2, `FirstBeat` ×2, `EnemyHealthFraction` ×2, `IsShielded`, `HealthOf`, `HealthFractionOf`, `NextSkillEventAfter`, `NextSkillAfter`, `LastBeat`, `LastTrapBefore`, `CreatureBreaks`, `CreatureHealthFraction`, `CreatureCount`, `Finished`). Pinned by `WaveReplayTests`, `beat_cadence_test`, `break_badge_test`. |
| `WaveSpoils` | `Game1.cs:3657` |
| `Chest`, `Chests`, `ChestTuning`, `ChestReward` | `Game1.cs:3746-3762` (drop), `ForgeScreen.cs:1182` (open) |
| `ChestDossier(s)` | `ChestScreen.cs:298,351,369,433,522-525,791-805`; `ForgeScreen.cs:1111`; `Game1.cs:2700` |
| `GiftChests` | `Chest.cs:316-317`; `Game1.cs:828,2003` (`NewGameChests`) |
| `ExpeditionLoot.RollBoss` | only via `Chest.cs:340` (contents rolled at open). Live, misnamed (see §9). |
| `RunReport`, `RunRecorder`, `DiffEntry`, `RunLog` | `SoloExpeditionScreen.cs:246,1344,2041,2141-2200,3748-3753`; `Game1.cs:660,891,958,1209`; `SaveGame.cs:90,318`; `ShareCodes.cs:95` |
| `SoloExpedition` — `Builds/SoloExpedition.cs` | `SoloExpeditionScreen.cs:808-815` (ctor), `823` (StartAtWave), `848` (ReplaceBuild), `852` (RefreshPool), `883` (PushWave), `898/911/1584/1817` (LastWaveCreatures), `1314` (LastWaveHaul/WasBoss), `1344/3753` (Report), `1645/2430/2444` (LastWaveArchetype/Affixes), `284` (Carried.Quality) |

### 1.2 ORPHAN — the encounter-template / par / efficiency chain

**Claim**: the authored encounter templates and everything derived from them are not read by the running game.

Evidence (grep over `src` and `tests`, definitions in `Encounters/EncounterTemplate.cs`, `VerdantHollow.cs`, `Regions.cs` excluded):

```
ParClearTimeSeconds → RegionAutomation.cs:50,57 (ctor store) ; EncounterSpawner.cs:13 (doc)
                      tests: RegionFarmTests.cs:94, EncounterSpawnerTests.cs:9,30, WorldTests.cs:116
PowerTierBase       → EncounterSpawner.cs:33 (doc) ; tests: FullLoopTests.cs:38, WorldTests.cs:115
CreaturePool        → tests only: WorldTests.cs:137,140
SelectionWeight     → (no hits)
TemplateId          → tests only: EncounterSpawnerTests.cs:29
CreatureTemplate    → (no hits outside definitions)
Regions.AllTemplates→ Regions.cs:110 (definition only)
.Templates / .Boss in src/ResonanceHunter.Game → (no hits)
BaseHealth outside Encounters/ → SoloExpedition.cs:41,126,140,290 (the ctor PARAMETER, fed by Game1.cs:3614 `110f * …`, not by a template)
```

The live enemy baseline is composed in `Game1.cs:3614-3615` from literals (`110f`, `9f`) — `EncounterTemplate.CreaturePool[i].BaseHealth` (80/65/110/1800 in `VerdantHollow.cs:241-256`; 120/95/160/3200… in `Regions.cs:73-95`) never reaches a fight. The template creature ids (`nature_atk_whelp_std`, `machine_atk_cinderworksboss_boss`…) never reach art either: `Game1.cs:2957-2965 EnemyArtFor` has its own region-id switch with different keys (`crea_nature_atk_whelp`). The region "boss" template (Thornmaw, 1800 HP) is never fought; the live boss is every 5th wave at ×2.2 health (`WaveScaling.IsBossWave`, `ExpeditionTuning.BossEvery/BossHealthScale`, `SoloExpedition.cs:269,296 forceSingle`).

The only *runtime* thread out of the chain is:
`EncounterSpawner.DerivePar` → `EncounterTemplate.ParClearTimeSeconds` → `World.FarmParSeconds` (`Regions.cs:186-190`) → `Region.ParClearTimeSeconds` (`RegionAutomation.cs:47-57`) → **read by nobody in the game** (only `RegionFarmTests.cs:94`).
And separately `Region.IdleEfficiencyPercent()` (`RegionAutomation.cs:119-120`) → `EfficiencyContract.IdleEfficiencyPercent(MasteryLevel, 0f)` → **`MapScreen.cs:529`** prints `"EARNS {n}% WHILE YOU ARE AWAY"`. That number is **not what offline earnings use**: `Game1.cs:751-774` credits `Warren.Tick(credited)` plus `credited * save.ChampionGleamRate * 0.5`. The caption advertises a rule (25/50/80 % of par by mastery band) that no payout implements — a costume with no number (the design's own phrase, `Bands.cs:11-14`).

**Tests that pin the orphan** (would need deleting/rewriting):
- `tests/unit/.../Encounters/EncounterSpawnerTests.cs` (par worked examples 15/13/24/591; `EffectiveMaxHealth`; `ReferenceTeamEffectiveDps`)
- `WorldTests.cs:110-116` `test_later_regions_are_tougher_than_earlier_ones` (PowerTierBase/Par), `:133-141` `test_verdant_hollow_offers_creature_variety` (CreaturePool), `:186-192` `test_each_region_declares_its_standard_pool_and_a_boss`
- `tests/integration/.../FullLoopTests.cs:36-38` (`new Region(…, parClearTimeSeconds: 15)`, `Templates.First(...).PowerTierBase`)
- `RegionFarmTests.cs:94` (`ParClearTimeSeconds`), `:73-79` (`IdleBand` mins)
- `Progression/EfficiencyContractTests.cs` (whole file)

**Recommendation**: A-delete `EncounterTemplate`, `CreatureTemplate`, `SpawnTuning`, `EncounterSpawner`, `VerdantHollow.Templates` (keep `VerdantHollow.RegionId` — 40+ consumers — ideally as `Regions.HomeId`), `RegionDefinition.Templates/Boss`, `Regions.AllTemplates`, `Regions.BuildRegion`'s template half (`Regions.cs:116-143,148-153`), `World.FarmParSeconds`, `Region.ParClearTimeSeconds` and the tests above. `EfficiencyContract` + `MasteryLevel.OptimizedTeam`: **owner decision** (open question §11) — either wire offline earnings to the idle band so the caption becomes true, or delete both the contract and the caption. `MasteryLevel` itself stays: it is live as a difficulty ramp and trait-point source (§1.3).

### 1.3 Other orphans / dead members in scope (grep-verified)

| Member | Declared | Grep result | Verdict |
|---|---|---|---|
| `SoloExpedition.LastWaveTopForm` (`Form?`) | `SoloExpedition.cs:81-86` | `grep -rn LastWaveTopForm src tests` → 1 hit (the declaration). Never assigned, never read. | A-delete (WARDED was never wired) |
| `SoloExpedition._enemySource` | `SoloExpedition.cs:43,129,142` | `grep -n _enemySource SoloExpedition.cs` → 43 (decl), 142 (assign). Never read; per-creature Source now comes from `BandCycles.RosterFor` (`:296`). `Game1.cs:3472` comment "region element → the Source matchup" is stale. | A-delete field + ctor param + `SoloExpeditionScreen.EnemySource` plumbing |
| `Affix.Warded / Legion / Entrenched / Hollow` | `Bands.cs:35-54` | No multiplier in `Bands.cs:155-180`; `grep -rn "Warded\|Legion\|Entrenched\|Hollow\b" src/ResonanceHunter.Core/Builds/*.cs src/ResonanceHunter.Game/*.cs` → only `VerdantHollow` matches. Authored into 6 bands: `BandCycles.cs:225` (VH b4 Legion), `:245` (UR b2 Legion), `:274,275,280` (SA b2 Warded, b3 Entrenched, b5 Warded), `:292` (PC b2 Warded). Printed to the player at `SoloExpeditionScreen.cs:2444` (HUNT header) and `:2148-2149` (report "wall"). `BandCycleTests.cs:117-123` only checks the affix is not None; `AffixLivenessTests` covers Plated/SIEGE, Swift, Endless. | **LIVENESS DEFECT.** Implement or remove (see §3.3). WARDED's spec is Form-keyed (`regions-and-rosters.md:102-108`) and must be re-specified per SKILL/STYLE first. |
| `WaveMetrics.CreaturesKilled` | `SoloBattle.cs:128` | written `SoloBattle.cs:1018`; no reader (`RunRecorder.Build` `RunReport.cs:182-189` sums Raw/Delivered/Hits/Activations/TargetsStruck/CreaturesPresent/HealthLost/DurationMs only). | A-delete |
| `WaveMetrics.AbsorbedFraction/AverageHitSize/TargetsPerActivation` (computed props) | `SoloBattle.cs:136-141` | no `metrics.X` reader; `RunRecorder.Build` recomputes the same formulas from band sums (`RunReport.cs:202-204`). Second copy of a formula. | A-delete the props (or make `RunRecorder` the only place) |
| `EncounterTemplate.SelectionWeight`, `CreatureTemplate.Weight` | `EncounterTemplate.cs:20,32` | `SelectionWeight` → 0 hits; `.Weight` → only band Composition tuples and `BandCycleTests.cs:32`. | A-delete with the chain |
| `RegionDropProfile.FavouredNames` | `RegionDrops.cs:59` | Game uses `RegionDrops.PlainName(slot)` directly (`MapScreen.cs:569`); `FavouredNames` read only by `region_drops_test.cs:31-40`. | A-delete (test reads `PlainName` directly) |
| `RunReport.DiffAgainst` (string lines) | `RunReport.cs:91-95,119-124` | Game uses `DiffEntries` (`SoloExpeditionScreen.cs:2200`); `DiffAgainst` only in `RunReportTests.cs:144,157`. | A-delete, move tests to `DiffEntries` |
| `RunLog.Newest`, `RunLog.Clear` | `RunLog.cs:38,85` | `Newest` → `RunLogTests.cs:46,58,133` only; `Clear` → no callers. | Minor; delete or keep as API |
| `WaveReplay.NextSkillAfter(ms, form)` / `LastSkillBefore(ms, form)` (Form-only overloads) | `WaveReplay.cs:189-196,234-244` | Game calls only the `(ms, source, form)` overloads (`SoloExpeditionScreen.cs:371,2918-2919`). | A-delete with the event re-key (§2.3) |
| `WaveReplay.MaxHealthOf` (public) | `WaveReplay.cs:106` | internal use only (`:110,355`); Game 0, tests 0. | make private |
| `Bands.DefenseMultiplier` + `DefenceMultiplierOrOne` | `Bands.cs:166-170` | alias pair; only `OrOne` called (`SoloExpedition.cs:301`); neither in tests. | collapse to one |
| `Haul.Cores` / `WaveBonus.Cores` | `WaveModel.cs:324,331` | **LIVE** — `Game1.cs:3646-3648` pays `Material.Core`. But `SoloExpedition.cs:486-488` says "nothing consumes it" → stale comment. | fix comment |
| `SoloExpedition.Carried` | `SoloExpedition.cs:159,392` | only `.Quality` read (`SoloExpeditionScreen.cs:284` → chest `RunTilt`); `.Gleam/.Cores` accumulate unread. | narrow to `CarriedQuality` |
| `MasteryLevel.OptimizedTeam` | `EfficiencyContract.cs:11` | unreachable by construction (`RegionAutomation.cs:38-41,101-108`); referenced only by `EfficiencyContractTests.cs:27`. | A-delete with contract decision |
| `WaveCreature.Single` | `SoloBattle.cs:86-87` | `SoloBattle.cs:413` (single-enemy `ResolveWave` overload used by `DamageBench` + 16 test sites). | keep (bench/test seam) |

---

## 2. The event model (Q2)

### 2.1 Members and emitters (`WaveModel.cs:241-256`; emitters in `Builds/SoloBattle.cs`)

| Kind | Emitted at | Slot | Amount | Notes |
|---|---|---|---|---|
| `Beat` | 683 (Kill), 1776 (tick) | 0 | `champ.BeatCount` (run-cumulative) | published rhythm counter |
| `Strike` | 1020 (`LandOn`) | creature index | delivered damage (post-armour) | `FromSkill: !swing` |
| `EnemyDown` | 1030 | creature index | 0 | once per creature |
| `Heal` | 1195 | 0 | **landed** (clamped) | `WaveReplay.cs:351-353` remark about unclamped amounts is stale |
| `Aura` | 1272 | `(int)sk.Source` | `(int)form` | passive tick marker, not an action |
| `Break` | 1323 | creature index | breaks carried (state, assigned) | PRESS/HAMMER |
| `Charge` | 1542, 1606, 1754, 1897 | 0 | pool after change | |
| `Skill` | 1543 (Mark), 1558 (cast), 1714 (variation), 1942 (Trap bite) | `(int)Source` | `(int)Form` | **legacy composition key** |
| `Shield` | 1677 (BANKED), 1962 (UNDYING) | 0 | **1677: shield magnitude; 1962: `UndyingShieldMs`** | units conflict — see 2.4 |
| `EnemyStrike` | 1864 | 0 (champion) | damage taken | **no attacker index** |
| `Down` | 1967 | 0 | 0 | |

### 2.2 Consumers

- `SoloExpeditionScreen.cs:1195-1305` switch: Aura, Strike, EnemyStrike, Skill (`(Form)e.Amount`, `(Source)e.Slot`), Heal, Shield, Down, EnemyDown, Charge. Break and Beat are consumed indirectly through `WaveReplay.CreatureBreaks/BeatAt`.
- `WaveReplay.Apply` (`WaveReplay.cs:330-373`): Strike, EnemyStrike, Break, Heal, Down, EnemyDown, Shield. Queries: Beat (`BeatAt/FirstBeat/LastBeat`), Skill/Aura (`IsCastOf` on `(Slot==source, Amount==form)`), Trap (`(Form)e.Amount == Form.Trap`, lines 172, 303).
- `SoloExpedition.HaulForWave` (`SoloExpedition.cs:425-441`): `EnemyStrike` timestamps → longest clean stretch for UNTOUCHED / SPLINTER haul nodes.
- `DamageBench.cs:54`: sums `Strike.Amount`.
- `RunReport` does **not** consume events — `RunRecorder` reads `WaveMetrics` populated inside `ResolveWave` (`SoloBattle.cs:641,673-674,948,960,995-996,1018,1092,1124,1147`).
- Quests do **not** consume events — `QuestSnapshot` (`Game1.cs:5170-5174`) reads `BestDepth`, conquests, chests opened, vows kept.

### 2.3 The Form leak — why the migration cannot finish without touching `BattleEvent`

The Skill and Aura events identify the acting skill as `(Slot=(int)Source, Amount=(int)Form)` (`WaveModel.cs:286-306` contract; emitters above). Everything downstream keys on that pair:

- `SoloExpeditionScreen.cs:1254-1270` — `var form = (Form)e.Amount; _skillFlash[SkillKey(e.Slot, e.Amount)]; PlayFormVfx(form, (Source)e.Slot, …); if (form == Form.Trap) …`
- `SoloExpeditionScreen.cs:2905-2919` — the skill rail keys its cooldown ring by `SkillKey((int)s.Source, (int)s.Form)` and asks `_replay.NextSkillAfter/LastSkillBefore(_playheadMs, (int)s.Source, formKey)`; `:2924` uses `FormBehaviour.CooldownBeats(s.Form)`.
- `WaveReplay.cs:172,303` — `(Abilities.Form)e.Amount != Abilities.Form.Trap` (a behaviour branch on a legacy content enum inside Core).
- `SoloExpedition.ReplaceBuild` (`:208-217`) decides whether a slot's cooldown survives a swap by comparing `Ability.Form` and `Ability.Source` — slot identity by Form.
- `EquippedSkill.Def` still resolves through `SkillCatalogue.Resolve(Form, Passive)` (`Build.cs`, remark: "the bridge is deliberate and temporary … once WovenAbility carries a SkillId this becomes a lookup by id and the Form column disappears").

Under the authoritative model a skill's Source is a *variation choice* (`EquippedSkill.Source => Variation?.Source ?? Ability.Source`), so the `(Source, Form)` key is neither stable across a variation pick nor unique across the two faces of one Form. **Recommendation**: the event should carry the **slot index** (and, if the screen needs it, the presentation resolves `build.Skills[slot].Def.Id` / style / kind); `WaveReplay` should answer cast queries by slot; the Trap exclusion becomes a `SkillDef` flag (e.g. `FiresOnBeingHit`) resolved by the caller, not a Form compare in Core.

### 2.4 Events the semantic contract wants vs what exists

| Wanted | Status |
|---|---|
| SkillCastStarted | **missing** — `Skill` is emitted at the moment of resolution (same ms as its `Strike`s, `SoloBattle.cs:1558` then `LandSpread`); the screen back-derives anticipation with `WaveReplay.NextSkillEventAfter` (`:167-174`) |
| SkillReleased | exists as `Skill` |
| Impact | exists as `Strike` (per creature, `FromSkill`) |
| DamageResolved (raw vs delivered) | **partial** — `Strike.Amount` is post-armour only; raw/absorbed exists only as the wave aggregate `WaveMetrics.RawDamage` |
| DefenceBroken | exists as `Break` (count, not the Defense value) |
| StatusApplied / StatusRemoved | **missing** — slow (`slowFactor` `SoloBattle.cs:626,1347,1798`), poison/bleed (`poison` `:495`, `VenomBleedPerHalfSecond`), MARK window (`champ.MarkUntilMs`), stagger (9 refs), wounds/bent armour (`:461-462`) are all sim-locals with no event; the presentation cannot show any of them. Only `Break` and `Shield` are published states. |
| ExecuteTriggered | **missing** — ASSASSINATE lands as a `Strike` with `fromSkill:false` (`WaveModel.cs:926-927` lists execution among the basic-attack-flagged blows) |
| HealResolved | exists as `Heal` (landed amount) |
| ShieldApplied | exists as `Shield`, but **the payload has two meanings**: `SoloBattle.cs:1962` emits `UndyingShieldMs` (a duration, as the contract at `WaveModel.cs:236-237` says) while `:1677` emits `(int)MathF.Round(bankedShield)` (a magnitude). `WaveReplay.Apply` treats Amount as ms (`:371 _shieldUntil = AtMs + Amount`), so BANKED's shield badge is open for e.g. 40 ms. Confirmed by reading both sites; not yet reproduced in a test. |
| SkillFinished | **missing** (casts are instantaneous on the beat; no duration concept) |
| WaveStarted / WaveEnded / CreatureSpawned | **missing as events** — the host learns via `PushWave`'s return and `LastWave*` properties; composition is handed to the replay out-of-band (`SetComposition`, `SoloExpeditionScreen.cs:911`) |
| EnemyStrike attacker | **missing** — `Slot` is the champion (0); which of up to five creatures bit is not in the stream (`SoloBattle.cs:1864`); the screen lunges "the enemy" generically (`SoloExpeditionScreen.cs:1246-1248`) |

Payload overloading: `Slot` means creature index / champion slot / `(int)Source` depending on kind; `Amount` means damage / heal / break count / beat count / charge pool / duration ms / `(int)Form` / shield magnitude. A typed event (discriminated record per kind, or at least named fields) would remove the casts at `SoloExpeditionScreen.cs:1254,1268` and `WaveReplay.cs:172,303`.

---

## 3. Enemy model (Q3)

### 3.1 `WaveCreature` (`SoloBattle.cs:60-88`)

`MaxHealth` (init), `Health` (set), `Damage` (init), `Defense` (**set** — mutated in-wave by SUNDER/PRESS/Machine signature at `SoloBattle.cs:918,1318`), `Source?` (init), `Archetype?` (init). No attack interval (wave-level `biteInterval`, `SoloExpedition.cs:329`, passed as an int), no slow/bleed/stun/broken fields.

What the presentation actually reads from `LastWaveCreatures` (the **end** state after `ResolveWave`): `Sum(MaxHealth)` for the bar (`SoloExpeditionScreen.cs:898,911`), `Count` (`:1584`), archetype (`:1645,2430`). Mid-wave per-creature health and break depth are reconstructed by `WaveReplay` from `Strike`/`EnemyDown`/`Break` events. Transient status (slow, poison, mark, stagger, wounds, bent armour) is invisible to the presentation because it lives in `ResolveWave` locals (§2.4).

### 3.2 Archetype → stats (`Archetypes.cs:71-78,120-154`)

Count / HealthMult / DamageMult / DefenseBase: Swarm 3-5 × 0.30/0.32/0; Armoured 1-2 × 1.25/0.85/25; Caster 1-2 × 0.50/1.00/0; Bruiser 1 × 2.40/1.35/10. Defense grows 2 %/wave (`DefenseGrowthPerWave`). The `Archetype` tag has exactly one combat reader: SIEGE (`SoloBattle.cs:840`, `VsArmouredBonus/VsOtherPenalty`) — which is why the PLATED re-mint bug (`SoloExpedition.cs:303-315` remark, `AffixLivenessTests.cs:51-111`) inverted it.

### 3.3 Affixes → dials

| Affix | Rule site | Interacts with |
|---|---|---|
| Plated ×1.5 Defense | `Bands.cs:166-170`; re-mint `SoloExpedition.cs:301-315` | flat-armour `MinHitFraction`, SIEGE (`SoloBattle.cs:840`), SUNDER/PRESS `DefenceBreakPerTick/Floor` (`:1318`) |
| Numbers +1 clone | `Bands.cs:172-173`; `SoloExpedition.cs:320-327` (never on a boss) | Spread / `TargetsBonus` |
| Swift interval ×0.7 | `Bands.cs:175-176`; `SoloExpedition.cs:329` | TRAP (`FiresOnBeingHit`), FORTIFY, PAYBACK, REPAY |
| Endless sustain ×0.5 | `Bands.cs:179-180`; `SoloExpedition.cs:332` → `SoloBattle.cs:1177` (all in-wave heals) and `SoloExpedition.cs:386` (between-wave regen) | ENDURE branch |
| Brittle health ×0.6 / dmg ×1.6 | `Bands.cs:155-164` | — |
| Ritual dmg +8 %/wave into band | `Bands.cs:158-164`; `wavesIntoBand` `SoloExpedition.cs:279` | — |
| **Warded, Legion, Entrenched, Hollow** | **none** | — (see §1.3). ENTRENCHED is cheap to add: `struckOnce` already exists at `SoloBattle.cs:456`. LEGION needs a split-on-death in `LandOn`. HOLLOW ("no chests, haul ×2") would be host-side today (`Game1.cs:3676,3746`). WARDED needs a per-skill (not per-Form) damage tally carried across waves — the one affix whose *design* must change for STYLE→SKILL before it is built. |

---

## 4. Fast-forward, offline, determinism (Q4)

- **No Core API to simulate N waves / T seconds.** The unit is `SoloExpedition.PushWave()`; `SoloBattle.ResolveWave` is already headless and instant (100 ms ticks to a 120 s ceiling: `SoloBattle.cs:1198`, `ExpeditionTuning.TickMs/TickCeilingMs` `WaveModel.cs:202-203`). Tests fast-forward by hand — `while (!run.Over && run.Wave < 100) run.PushWave();` (`SoloExpeditionTests.cs:43`; 20 such loops across `tests/`). The only in-game loop is dev-only: `SoloExpeditionScreen.DevRunToDeath` `while (_run is {Over:false} && guard++ < 400) _outcome = _run.PushWave();` (`:3745`).
- **Offline progress does not simulate** (`Game1.cs:748-802`): `champOffline = credited * save.ChampionGleamRate * 0.5` where `ChampionGleamRate` is a live-measured gleam/sec (`Game1.cs:3617-3632,3681`; persisted `SaveGame.cs:211`), capped at 24 h (`SaveGame.cs:691-694`). Offline pays **Gleam only**: no `WaveSpoils`, no charters, no chests, no `SkillProgress.RecordWave`, no depth records, no mastery points. `Bands.Seed`'s remark ("fast-forward can pay the haul a wave originally paid", `Bands.cs:99-103`) describes an unbuilt feature.
- **The descent state machine is in the MonoGame screen**: `StartRun` (`SoloExpeditionScreen.cs:792-830` — mints `Champion`, `SoloExpedition`, charges the checkpoint), `BeginWave` (`:832-916` — re-composes the build via a string `BuildStamp`, calls `ReplaceBuild`/`RefreshPool`, pushes, builds the replay), reward queue (`:1314`), `Deepest` (`:1315`), `Log.Add(Report)` (`:1344`), and `Game1` pays rewards off the screen's queue (`:3640-3680`). Core cannot run a descent by itself. This is the single biggest blocker to offline simulation, headless probes that model the *loop* (see memory "probes must model the loop"), and to keeping "simulation never waits for presentation" honest.
- **Determinism**: composition is deterministic — `new Random(Bands.Seed(RegionId, next, RunIndex))` (`SoloExpedition.cs:282`), FNV-1a stable across processes (`Bands.cs:107-124`; `BandCycleTests.cs:185`). The **fight** uses one `Random` stream across all waves of a run (`SoloExpedition.cs:44,143,334`) fed from the screen's unseeded `new Random()` (`SoloExpeditionScreen.cs:236`) → live fights are not reproducible (tests seed it). `RunIndex` is `++_runIndex` per `StartRun` and **not persisted** (`SoloExpeditionScreen.cs:765,813`) → after a reload the first descent repeats run 1's compositions. **Loot** rolls use unseeded host RNGs (`Game1.cs:135` for spoils `:3657` and chest drop `:3746,3753`; `ForgeScreen.cs:117` for open `:1182`). Net: deterministic composition, stochastic fight and loot — a fast-forward that must "pay what the wave originally paid" has no seed to replay from.

---

## 5. Region → Source, ladder, conquest, depth cap (Q5)

- **Region Source → enemy**: `RegionDefinition.Theme` (`Regions.cs:14,68-96`). Per-creature Source comes from `BandCycles.RosterFor(RegionId)` (own + two wheel neighbours, `BandCycles.cs:317-325`) at `SoloExpedition.cs:296`. `Game1.cs:3472 _expedition.EnemySource = def.Theme` flows into the dead `_enemySource` (§1.3).
- **Region Source → item Element**: `Game1.cs:3753 Chests.RollDrop(lootTier, def.Theme, …)` → `Chest.Element` (`Chest.cs:36,272`) → `Chests.Open` → `ExpeditionLoot.RollBoss(element: chest.Element)` (`Chest.cs:340-341`) → `KillContext.Element` (`ExpeditionLoot.cs:220`) → `ItemInstance.Element`. Gift path: `GiftChests.cs:114-121,133,153`.
- **Region ladder**: `RegionLadder.Health/Damage(index)` (`RegionLadder.cs:181,191,194-197`, geometric 1.62/1.34) applied at `Game1.cs:3614-3615` with `LadderIndex` (`Game1.cs:3445-3450`); `StepPercent` shown `MapScreen.cs:290`, `Onboarding.cs:338`; `Regions.RecommendedPower` (`Regions.cs:48-61`) at `MapScreen.cs:165`.
- **Conquest**: `Checkpoints.ConquestWave = 20` (`Checkpoints.cs:184`; was 7 → 20 per remark `:166-167`; **the brief's "wave 10" is out of date**). `Game1.cs:3718-3727`: `_expedition.Deepest >= ConquerWaveDepth && !_world.IsConquered(_activeRegion)` → `World.Conquer` (`Regions.cs:206-211`, returns the unlocked next) → Memory Dust award `40 * CorruptionScaling.RewardMultiplier` → banner. Unlock rule `World.IsUnlocked` (`Regions.cs:197-203`). Conquest also feeds `Warren.ConqueredRegions` (`Game1.cs:762,1087,1105`), character unlocks (`:3571`), trait points (`:5196`), quests (`:5172`), corruption gate `World.AllConquered` (`Regions.cs:233-236`).
- **Depth cap → Warren**: `Region.BestDepth` recorded `Game1.cs:3697 RecordDepth(_expedition.Deepest)`; `DeepestAnywhere()` (`Game1.cs:5203-5208`, max over farms + `_deepestEver`) → `Game1.cs:1111 _warren.FacilityLevelCap = Math.Max(1, DeepestAnywhere() / Warren.DepthPerFacilityLevel)` (`Warren.cs:248,251,262-266`). `BestDepth` also → skill points `MasteryPoints.Total` (`Game1.cs:5143-5148`), quests `DepthByRegion` (`:5171`), checkpoints (`Checkpoints.Options/Clamp`).
- **Hidden difficulty ramp**: `_regionProgression = Clamp((int)_region.MasteryLevel + (MasteryLevel>0 ? 1 : 0), 0, 4)` (`Game1.cs:2537`) multiplies enemy health by `1 + 0.35·p` and damage by `1 + 0.20·p` (`:3614-3615`) and adds `p` to the chest loot tier (`:3743`). Mastery points accrue automatically per cleared wave (`:3652 RecordActiveKill`). `grep MasteryLevel MapScreen.cs SoloExpeditionScreen.cs` → no hits: the player is never shown that farming a region makes it up to +140 % health. Flagged as an open question, not a verdict.

---

## 6. Loot per wave / boss (Q6)

- **Per wave (Gleam + material + charter)**: Gleam in `SoloExpedition.HaulForWave` (`:413-510`): `3 × HaulScale(wave) × build Haul × [UNTOUCHED, SPOTLESS/VEIN, BLOODPRICE, PROSPECT/LODE, CACHE, DESPERATION] × WaveHaulScale 1.44`; `Quality = Rarity + skill bonus + SPLINTER clean-stretch`. Material via `WaveSpoils.Roll(wave, rng)` (`WaveSpoils.cs:102-119`) at `Game1.cs:3657` — always 1 unit, tier by depth 8/25/60, 35 % tiered chance, 2.5 % charter from wave 5. Deterministic given its `Random`; the host passes an unseeded one.
- **Boss**: `Game1.cs:3676 if (r.IsBoss && DropBossChest(r, def))` → `lootTier = 1 + LadderIndex + _regionProgression + wave/5 + CorruptionScaling.TierBonus + RegionModifiers.LootTierBonus` (`:3739-3745`) → flat `DropChance 0.20` (`Chest.cs:175,260-261`) → `Chests.RollDrop` (grade by `RollRarity` weights + tier shift `Chest.cs:231-252`; `RunTilt = Carried.Quality`) → keep-filter (`Chests.PassesKeepFilter`, compensation `8 + tier` Scrap) → `_forge.AddChest`. **Contents are rolled at open**: `ForgeScreen.cs:1182 Chests.Open` → materials `12 + [0,6] + tier/4` → `ExpeditionLoot.RollBoss` (region favoured slots ×2, `RarityTilt`, quality + build tilt → `LootSystem.Roll`) → rarity floor by grade → 30 % gem. Gift chests short-circuit (`Chest.cs:316-317`).
- The region boss *template* is never fought; "boss" means every 5th wave (§1.2). The HOLLOW affix's "no chests, doubled haul" would have to live in `Game1.DropBossChest`/`HaulForWave` — neither knows the affix.

---

## 7. Form references in the area (Q7)

| Site | Reference |
|---|---|
| `SoloExpedition.cs:5` | `using ResonanceHunter.Core.Abilities;` |
| `SoloExpedition.cs:81-86` | `public Form? LastWaveTopForm` (dead) |
| `SoloExpedition.cs:211-213` | `ReplaceBuild` compares `old[i].Ability.Form != now[i].Ability.Form || Source` to decide cooldown reset |
| `WaveModel.cs:279-293` | `BattleEvent` contract: Skill/Aura `Slot=(int)Source`, `Amount=(int)Form` |
| `WaveReplay.cs:172,303` | `(Abilities.Form)e.Amount != Abilities.Form.Trap` |
| `WaveReplay.cs:189-244` | `NextSkillAfter/LastSkillBefore(ms, [source,] form)` keyed by Form int |
| `Bands.cs:41-42` | WARDED spec: "whichever Form dealt the most damage" |
| `BandCycles.cs:88,97` | "mono-Form build" remarks |
| `SoloBattle.cs:1543,1558,1714,1942` | emit `(int)Form.Mark`, `(int)form`, `(int)woven.Form`, `(int)Form.Trap` |
| `SoloExpeditionScreen.cs:1254-1270,2905-2924` | `(Form)e.Amount`, `CalloutFor(form)`, `PlayFormVfx(form, …)`, `FormBehaviour.CooldownBeats(s.Form)` |
| `design/gdd/regions-and-rosters.md:100-108,301-302,351` | WARDED specified per Form |

No archetype weakness is keyed on Form — `Archetypes` are pure stat shapes; the Source matchup is per creature (`WaveCreature.Source` → `Weaving.SourceEffectiveness` in the legacy `ResonanceWeaving.cs`, other area).

---

## 8. Numerical stacking

**Enemy health** (11 factors, 7 files):
`110` × `(1 + 0.35·_regionProgression)` × `RegionLadder.Health(i)` × `CorruptionScaling.HealthMultiplier(t)` × `RegionModifier.EnemyHealthMult` (all `Game1.cs:3614`) × `EnemyScaleBase^wave` × `BossHealthScale` (`WaveScaling.EnemyScale`) × `Bands.RepeatScale` (1.9^cycles) × `Bands.HealthMultiplier(affixes)` × `WaveLengthScale 2.5` (`SoloExpedition.cs:290-291`) × `ArchetypeShape.HealthMult` (`Archetypes.cs:144`).

**Enemy damage** (10 factors): `9` × `(1 + 0.20·p)` × `RegionLadder.Damage(i)` × corruption × modifier (`Game1.cs:3615`) × `EnemyDamageScaleBase^wave` × `BiasTempo.DamageMult` × `RepeatScale` × `Bands.DamageMultiplier` (`SoloExpedition.cs:292-293`) × `ArchetypeShape.DamageMult`.

**Chest loot tier** (6 additive terms, `Game1.cs:3743-3745`) then **item rarity** = `RollRarity` weights + tier shift (`Chest.cs:236-239`) → floor by grade → `RollBoss` tilt = `clamp(quality)` + `clamp(buildTilt)` + `RegionDrops.RarityTilt` (`ExpeditionLoot.cs:216-219`) where `buildTilt = rarityBonus × RunTilt` (`Chest.cs:342`).

Three separate "deeper region = harder" curves — `RegionLadder` (geometric), `RegionModifiers` (per-region hand values), `CorruptionScaling` (world ratchet) — and two "deeper region = better loot" curves — `RegionModifiers.LootTierBonus`, `RegionDrops.RarityTilt` — overlap in purpose. `RegionModifiers` in particular duplicates `RegionLadder` (`RegionModifiers.cs:229-239` vs `RegionLadder.cs:181-197`).

---

## 9. Stale terms and comments

| Term | Where | Replacement |
|---|---|---|
| `ResonanceHunter` | every namespace/csproj in scope | `IdleXIdle` (product identity audit) |
| "squad" / "Crafter" / "Producer" | `WaveReplay.cs:13` ("re-derives the squad's health"), `ExpeditionLoot.cs:135,165-176`, `SoloExpedition.cs:400-404`, `Game1.cs:2956` ("throws at your squad"), `SoloExpeditionTests.cs` header | champion / build |
| "Region farm", `RegionFarm()`, `Automation` namespace, `AutomationTuning`, `RmpPerActiveKillBonus` | `RegionAutomation.cs` (whole file), `Regions.cs:192 World.RegionFarm` | `RegionProgress` in `Encounters` (or `Progression`) |
| "par", "EfficiencyContract", "Pillar 3", "Blocker B1/B2", "fully-mastered automated team", `OptimizedTeam` | `EncounterTemplate.cs:33-46`, `EncounterSpawner.cs`, `EfficiencyContract.cs`, `RegionAutomation.cs:56` | delete with the chain |
| `RollBoss` | `ExpeditionLoot.cs:202` | `RollChestGear` (bosses drop chests; chests roll) |
| "nothing consumes it" (Cores) | `SoloExpedition.cs:486-488` | contradicted by `Game1.cs:3646-3648`; fix |
| "greybox fallback for new regions" | `Regions.cs:32-34` | stale vs `Game1.cs:2957-2965` art keys |
| "region element → the Source matchup" | `Game1.cs:3472` | stale; roster drives matchup |
| "emits 18 and delivers 2" (unclamped heal) | `WaveReplay.cs:351-353` | stale; `SoloBattle.cs:1195` emits landed |
| "mastery" (five meanings) | `Region.MasteryLevel/RegionMasteryPoints` (Automation), `Builds/MasteryTree` (skill tree), `MasteryPoints` (skill points from depth, `Game1.cs:5148`), `WarrenResource.Mastery` (shown as INSIGHT), `_dust.AwardFromMastery` (Memory Dust) | pick one word per concept |
| `Affix.Endless` vs mastery node "endless" | noted in code `SoloExpedition.cs:384-385` | rename one |
| Memory Dust vs Trait Points | `Checkpoints.cs:172-179` explains Dust no longer buys traits; `_dust.AwardFromMastery` (`Game1.cs:3721,4976`) | rename the award method |
| Superseded GDDs without banners | `design/gdd/encounter-spawn-system.md`, `creature-ai-telegraph-system.md`, `creature-data-schema.md`, `creature-jobs-evolution-system.md`, `creature-roster-ui.md`, `rare-creature-capture-system.md` — `grep -c SUPERSEDED\|RETIRED` → 0 each; `combat-encounter-system.md` and `expedition-auto-battle.md` do carry one | add banners |
| `regions-and-rosters.md` "Draft" | specifies WARDED/LEGION/ENTRENCHED/HOLLOW rules (`:100-104,300-305,351`) the code does not have | mark the four as unimplemented or implement |

---

## 10. Hardcoded branches, coupling, serialization

### 10.1 Branches keyed on content IDs / legacy enums
- `BandCycles.For` / `RosterFor` switch on region id string (`BandCycles.cs:304-325`); `RegionDrops.Profiles` (`:70-103`) and `RegionModifiers.ByRegion` (`:231-239`) dictionaries keyed by id; `Regions.All` (`:63-97`) — **four tables keyed by the same string**, each with its own coverage test. Fold cycle, roster, drop profile, modifier and art key into `RegionDefinition`.
- `Game1.EnemyArtFor` switch on region id (`Game1.cs:2957-2965`) — presentation branching on content id; belongs on `RegionDefinition` as data.
- `WaveReplay.cs:172,303` `(Form)e.Amount != Form.Trap` — Core behaviour keyed on a legacy content enum (§2.3).
- `SoloExpeditionScreen.cs:1254-1270` `(Form)e.Amount`, `form == Form.Trap` for sounds/vfx — same.
- Semantic (fine): `SoloBattle.cs:840` `Archetype == Armoured` (SIEGE); `SoloExpeditionScreen.cs:1515-1520 ArchetypeScale`.
- `RunReport.Verdict` thresholds 0.45 / 2.5 / 0.5 / 0.18 inline (`RunReport.cs:73-82`) — should be tuning.

### 10.2 Core/presentation coupling
- `Game1.cs:3614-3615` composes the enemy baseline in the host with literals `110f`, `9f`, `0.35f`, `0.20f`; the screen has its own defaults `120f`/`9f` (`SoloExpeditionScreen.cs:460`) — two sources of truth for a gameplay value.
- `Game1.cs:2537` `_regionProgression` (mastery → difficulty) is a gameplay rule in the host.
- `Game1.cs:3718-3721` conquest rule and Dust award `40` in the host.
- `Game1.cs:3739-3746` loot-tier formula and drop roll in the host; `:3657` spoils roll; `:3652` `RecordActiveKill` called once per **wave** (name says kill).
- `SoloExpeditionScreen` owns the descent (§4): `StartRun`/`BeginWave`/reward queue/`Deepest`/`Log`/checkpoint charge; `Game1` has 89 `_expedition.` reads/writes (`grep -c "_expedition\." Game1.cs`).
- `ExpeditionTuning` (`WaveModel.cs:14-226`) carries SoloBattle knobs (`AutoAttackDamage`, `BeatMs`, `WaveOpeningMs`, `Heal`, `TickMs`) alongside wave-scaling knobs — one record, two owners.
- `Checkpoints.ConquestWave` is mirrored under three names (`Game1.cs:3781 ConquerWaveDepth`, `SoloExpeditionScreen.cs:2281 ConquerAt`, `MapScreen.cs:50 ConquerWaves`) — all derived from the Core const, so consistent, but noise.

### 10.3 Serialization
- `RunReportSave.Outcome/WallArchetype/WallAffixes` are stored as **enum ints** (`SaveGame.cs:318-337`; `RunLog.cs:302-346`). Deleting `Warded/Legion/Entrenched/Hollow` from `Affix` (or reordering `Archetype`) re-labels every saved report. Migration: store names, or append-only with tombstones.
- `RegionFarmSave` keyed by region id (`SaveGame.cs:344-356`); `World.RestoreConquered` drops unknown ids (`Regions.cs:214-218`) — safe on rename but silently loses conquest/depth (`Warren.cs:257` already notes the FacilityLevelCap consequence).
- `SaveGame.ChampionGleamRate` (`:211`) is the sole offline input; becomes legacy if offline is ever simulated.
- `RunIndex` not persisted (§4) — determinism caveat only.
- `Chest.Region/Gift/RunTilt` are name-keyed, lenient, defaulted — the good pattern to copy.

---

## 11. Open questions for the owner

1. **EfficiencyContract / "EARNS N% WHILE YOU ARE AWAY"**: wire offline to the idle band (and then simulate waves offline), or delete the contract, `MasteryLevel.OptimizedTeam` and the caption? Today the caption is unbacked (`MapScreen.cs:529` vs `Game1.cs:751-774`).
2. **Region mastery as a hidden difficulty ramp** (`Game1.cs:2537,3614-3615`): intended and to be surfaced, or an inherited coupling to remove? It also feeds trait points (`:5197`) and chest tier (`:3743`).
3. **The four inert affixes**: implement (ENTRENCHED/LEGION/HOLLOW are mechanical; WARDED needs a per-skill re-spec) or cut them from `BandCycles` and the enum (then migrate `RunReportSave.WallAffixes`).
4. **Event re-key**: slot index vs `SkillDef.Id` in `BattleEvent`? Slot is cheapest and matches `ReadyAt`/`ReadyAtBeat` keying; id is what a run log/replay file would want.
5. **BANKED Shield payload** (`SoloBattle.cs:1677` magnitude vs `WaveReplay.cs:371` duration): confirm on screen; if confirmed, it is a dormant-feature bug of exactly the house species.
6. **Fast-forward ownership**: should a headless `Descent` (Core) own StartRun/BeginWave/rewards/log so the screen only observes it? This is the prerequisite for offline wave simulation, seeded loot, and loop-modelling probes.
7. Can two equipped skills share `(Source, Form)` (the two faces of one Form)? If yes, `SkillKey(source, form)` on the rail (`SoloExpeditionScreen.cs:2905`) already collides today.
8. Is `RegionModifiers` still wanted now that `RegionLadder` is geometric? It was added as "Map variety" (`Game1.cs:3603`) and its loot bonus overlaps `RegionDrops.RarityTilt`.
