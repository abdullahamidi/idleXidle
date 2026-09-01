# Audit — The Warren (idle facilities) and progression/economy ownership

**Date**: 2026-08-31 · **Branch**: `feat/hunter-cutout-rig` (clean) · **Mode**: read-only on source
**Method**: every claim below cites a `file:line` I read. Every "dead" claim shows the grep that found
no consumer. Numbers marked *derived* were computed from constants in the code; numbers marked
*measured* come from running the test suite (`dotnet test … --filter GleamEconomy`, output quoted in §12).
Searches excluded `bin/`, `obj/`, `.git/`, `docs/art-reference/`.

Files read in full: `src/ResonanceHunter.Core/Warrens/Warren.cs` (328 lines),
`src/ResonanceHunter.Game/WarrenScreen.cs` (449), `Economy/Charters.cs`, `Economy/WanderingTrader.cs`,
`Automation/RegionAutomation.cs`, `Expeditions/GiftChests.cs`, `Builds/MasteryPoints.cs`, `Economy/Material.cs`,
`Game/SaveFile.cs`, `tests/…/Warrens/WarrenTests.cs`, `tests/…/Economy/gleam_economy_test.cs`,
`tests/…/Automation/RegionFarmTests.cs`, `tests/…/Progression/PointIncomeTests.cs`,
`design/gdd/warren-facilities.md`, `docs/architecture/ADR-004-warren-facility-economy.md`.
Read in relevant part: `Game1.cs` (all Warren / offline / currency sites), `Persistence/SaveGame.cs`,
`Economy/HunterProgression.cs`, `Prestige/MemoryDust.cs`, `Progression/Unlocks.cs`, `Progression/Onboarding.cs`,
`Progression/Tutorial.cs`, `Encounters/Checkpoints.cs`, `Progression/EfficiencyContract.cs`,
`design/gdd/game-flow.md`, `design/gdd/progression.md`, `design/gdd/region-mastery-automation-system.md`,
`design/gdd/hunter-progression-system.md`.

---

## 0. Headline

The Warren is a small, clean, pure Core model (`Warren.cs`, 328 lines, no MonoGame, no balances) with a
host in `Game1.cs` that credits its yield into three pools. Two of the three "currencies" it produces are
economically inert against the sinks that exist: **INSIGHT** (`WarrenResource.Mastery`) is produced only
by the Warren and spent only on the Warren — a closed loop the enum's own doc comment says the system was
built to avoid — and **Memory Dust** is minted at ~619/min at level 1 (*derived*) against sinks priced at
28 Dust (an L1 upgrade) and 25 Dust/wave (checkpoints). Only Gleam and the depth cap ever bind an upgrade.
The eight facilities are mechanically identical (one `Facility` class, no per-kind branch anywhere:
`grep FacilityKind` outside the catalog finds only save-key parsing and a dev fixture); their descriptions
("Store the warren's Gleam so it grows", "Keeps the Dust safe") describe functions that do not exist.
The design docs are badly stale: the GDD, ADR-004 and `progression.md` all still say the Warren's Mastery
pool feeds `MasteryTree.SetEarned` (it does not — `Game1.cs:294-306` documents the correction), reference a
retired CREATURES sub-view, and quote rates 30× off the code. `game-flow.md §3.8` states the design the
owner actually wants (unlock-by-conquest, Gleam + materials only, per-tier material facilities) and the code
implements exactly one of its four bullets (the depth cap). The persistence surface is four JSON fields; a
rename of `WarrenMasteryPool` without a JSON-name pin would silently zero every player's Insight on load.

---

## 1. The eight facilities (Q1)

Catalogue: `Warren.cs:44-59`. Every facility is the same class (`Facility`, `Warren.cs:108-160`) with
`Kind`, `Level`, and a lookup into `FacilityInfo(Kind, Name, Description, Produces, BaseRatePerMin)`.

| Kind | Name | Produces | Base/min (L1) | Description (verbatim, `Warren.cs:48-55`) | Any function beyond producing? |
|---|---|---|---|---|---|
| Nursery | NURSERY | Gleam | 17 | "Hatch and raise young. Makes more Gleam." | No |
| Tunnels | TUNNELS | Gleam | 16 | "Dig deeper veins. Steady Gleam from the digging." | No |
| ForagingPits | FORAGING PITS | Dust | 450 | "Search the deep soil for Memory Dust." | No |
| ScavengerRuns | SCAVENGER RUNS | Gleam | 14 | "Send runners out to bring back Gleam." | No |
| BreedingChamber | BREEDING CHAMBER | Mastery (INSIGHT) | 122 | "Breed sharper minds. Makes Insight." | No |
| RitualNest | RITUAL NEST | Mastery (INSIGHT) | 118 | "Listen to the resonance. Slow, deep Insight." | No |
| HoardVaults | HOARD VAULTS | Gleam | 12 | "Store the warren's Gleam so it grows." | **No** — implies storage/compounding; it is a flat producer |
| SentryBurrows | SENTRY BURROWS | Dust | 108 | "Guard the forage trails. Keeps the Dust safe." | **No** — implies protection; nothing can take Dust |

**Evidence there is no per-facility behaviour**: `grep -rn "FacilityKind" src tests` outside
`Warren.cs`/`WarrenScreen.cs`/`WarrenTests.cs` returns only `SaveGame.cs:256,676,678` (save-key parse),
`Game1.cs:1963-1967` (screenshot fixture), `PointIncomeTests.cs:69-74`. No `switch (kind)` exists anywhere;
`Facility` has no virtual behaviour. `WarrenScreen.cs:291` builds the icon key from the enum name — the only
place the kind matters at runtime.

**Formulas (all `Warren.cs`)**:
- Raw output/min = `round(BaseRate × Level × MilestoneMultiplier)` — `:139`.
- Milestones: every `MilestoneEvery = 5` levels, `+MilestoneBonusPerTier = 0.15` each — `:99-102, :127-136`.
  So ×1.15 at L5, ×1.30 at L10, ×1.45 at L15… Yes, "+15%" is correct.
- Warren multiplier per resource = `1 + (Level−1)×0.03 + ConqueredRegions×0.10 + Level×k` with
  k = 0.02 Gleam / 0.013 Mastery / 0.009 Dust — `:74-77, :96, :204-218`.
- Upgrade cost `L→L+1` = `round(180×1.5^L)` Gleam, `round(43×1.25^L)` Mastery, `round(22×1.25^L)` Dust — `:85-90, :153-156`.
- Warren XP per upgrade = `newLevel × 100` (`:93, :288`); XP to next = `1000 × (Level+2)` (`:198`).

**Level cap rule (depth-based)**:
- `Warren.DepthPerFacilityLevel = 5` — `Warren.cs:248`; `FacilityLevelCap` defaults to `int.MaxValue` — `:251`.
- Host derives it every Warren draw: `_warren.FacilityLevelCap = Math.Max(1, DeepestAnywhere() / Warren.DepthPerFacilityLevel)` — `Game1.cs:1111`;
  `DeepestAnywhere()` = max of `_deepestEver` and every region's `BestDepth` — `Game1.cs:5203-5208`.
- Enforced in `Warren.CanUpgrade` (`:276-277`) which the host checks before spending (`Game1.cs:1119`).
- Screen states the depth required: `DepthForNextLevel = (Level+1) × 5` — `Warren.cs:262-263`, shown at `WarrenScreen.cs:387-391`.
- Tested at the pure-model level: `PointIncomeTests.cs:64-87`, `WarrenTests.cs:193-221`. **Not tested**: the
  host's derivation (`/5`, the `_deepestEver` fallback) — it lives in `Game1.cs`, which has no tests.

**Which facilities are the same thing with a different label**: all four Gleam facilities
(Nursery 17 / Tunnels 16 / ScavengerRuns 14 / HoardVaults 12) are one producer split four ways with base
rates 12–17; likewise the two Insight facilities (122/118) and the two Dust facilities (450/108). Eight
cards, three real knobs. The only distinction is the base-rate literal.

---

## 2. `WarrenResource.Mastery` — what IS it? (Q2)

**It is not mastery points.** `MasteryTree.Earned` is set every frame from depth only:
`_mastery.SetEarned(SkillPointsEarned())` — `Game1.cs:3714`; `SkillPointsEarned` = `MasteryPoints.Total(bestDepths)`
— `Game1.cs:5142-5148`, curve at `MasteryPoints.cs:39-49`. Its doc says "Nothing else feeds this — not the
Warren, not conquest" (`Game1.cs:5139-5140`).
`grep -rn "SetEarned" src` → the only `MasteryTree.SetEarned` calls outside dev fixtures are `Game1.cs:3714`
(depth) and `:3715` (`_dust.SetEarned(TraitPointsEarned())`). Nothing adds `_warrenMasteryPool`.

**Trace of the pool**:
- Produced: `Warren.Tick` → `WarrenYield.Mastery` (`Warren.cs:301-313`).
- Credited: `_warrenMasteryPool += w.Mastery` live (`Game1.cs:1091`) and offline (`:768`).
- Held: `private long _warrenMasteryPool` (`Game1.cs:306`), persisted as `SaveGame.WarrenMasteryPool` (`SaveGame.cs:267`, capture `:517`, restore `Game1.cs:651`).
- Displayed: `_warrenScreen.MasteryOwned = _warrenMasteryPool` (`Game1.cs:1114`).
- Spent: `_warrenMasteryPool -= c.Mastery` on a facility upgrade (`Game1.cs:1123`) — **the only spend**.
  `grep -rn "_warrenMasteryPool\|MasteryOwned\|WarrenMasteryPool" src tests` returns only the lines above plus
  the screenshot fixture (`Game1.cs:1971`). No other consumer exists.

**Closed loop, confirmed**: produced only by BreedingChamber + RitualNest, spent only on facility upgrades.
The code itself says so: `Game1.cs:294-305` ("a closed loop, which is exactly what the design note on
WarrenResource says the Warren was built to avoid"), `WarrenScreen.cs:111-117, :256-263`.
The enum's own remark (`Warren.cs:9-11`, "Mastery is the build-tree currency … rather than a closed loop")
is now false.

**Is it even a binding constraint?** *Derived* at level 1 with one conquest: raw 240/min × (1 + 0.10 + 0.013)
= 267 Insight/min; the first upgrade costs `round(43 × 1.25) = 54` Insight (`Warren.cs:87-88, :155`) — twelve
seconds of production. Gleam for the same upgrade is 270 at 66/min ≈ 4 minutes. Insight never decides anything
the Gleam line has not already decided.

**Every INSIGHT label site** (`grep -rn -i insight src`):
- Core: `Warren.cs:52` ("Makes Insight."), `:53` ("Slow, deep Insight.") — the Core catalogue already uses the screen name while the enum member is `Mastery`.
- `WarrenScreen.cs:85-86` (doc), `:94` (`"ui_insight"` asset key), `:104` (`"INSIGHT" => WarrenResource.Mastery`, string→enum reverse map), `:117` (`ResName`), `:155` (doc), `:258-263` (overview paragraph "INSIGHT is used only here, on upgrades."), `:315` (bonus strip `INSIGHT x{…}`), `:324` (bonus chip label + "1.3% A LEVEL"), `:373` (`DrawReq … "INSIGHT"`).
- `Game1.cs:294, :302, :754, :1077` (comments only — no player-facing literal in Game1 says INSIGHT; the top pills do not show Insight at all: `Game1.cs:4842-4849` lists materials, Dust, Gleam).
- `AssetLibrary.cs:123` (`["ui_insight"] = "currency_insight"`), art at `assets/art/ItemsLoot/loot/currency/currency_insight.png`.
- `docs/store/store-page.md:43` ("Facilities produce Gleam, Insight and Memory Dust").
- Unrelated homonym: the trait node `forge_insight` (`MemoryDust.cs:346`, `DustEffects.cs:175,196`, `TraitTreeLayout.cs:131`) — "FORGE INSIGHT" is a trait, not this currency. A rename to `Insight` must not collide with it in search or copy.

---

## 3. Warren Dust — is it the same Dust traits/materials use? (Q3)

Yes, one pool: `MemoryDustTree.MemoryDust` (`MemoryDust.cs:113`). The Warren credits it via
`_dust.AwardFromMastery((int)w.Dust)` live (`Game1.cs:1090`) and offline (`:767`). But **Dust no longer buys
traits**: the trait tree spends `Earned` (trait points, `MemoryDust.cs:139-146`), derived from conquests +
2×corruption peak + region-mastery levels (`Game1.cs:5192-5200`). `MemoryDust.cs:120-133` says this outright.

Dust's faucets (all `AwardFromMastery`, `grep -rn AwardFromMastery src`):
1. Warren tick — `Game1.cs:1090`; offline — `:767`.
2. Region-mastery level milestones: `(Δlevels) × 15 × corruption reward` — `Game1.cs:2745-2750`.
3. Conquest: `40 × corruption reward` — `Game1.cs:3721`.
4. Corruption deepening, first time per tier — `Game1.cs:4976`.
5. Dev/screenshot fixtures — `Game1.cs:1896, :1952, :1972, :2150`.

Dust's sinks (`grep -rn "_dust.Spend" src`):
1. Facility upgrade — `Game1.cs:1124` (`Dust cost = round(22×1.25^L)`).
2. Checkpoint start — `Game1.cs:3495`, `Checkpoints.DustCost = startWave × 25` (`Checkpoints.cs:35, :47`).

`AwardFromMastery` is misnamed: its doc (`MemoryDust.cs:150-154`) says "This is the ONLY faucet: Dust comes
from mastering regions" — false on five counts above. The name predates the Warren.

*Derived* supply vs. sink: at level 1 with one conquest the Dust facilities pay `558 × 1.109 ≈ 619/min`;
the first Dust cost is 28; a wave-40 checkpoint costs 1,000 (≈ 100 s of L1 production). Over the 24 h
offline cap that is ≈ 891k Dust before a single facility is levelled. Not measured in play — arithmetic from
`Warren.cs:50,55,77,96` and `Checkpoints.cs:35`. Flagged in §11 rather than asserted as a balance verdict.

---

## 4. Offline production, cap, combat feed, "never outrun the champion" (Q4)

**Offline path exists and is deterministic given the clock**:
- Basis: `SaveGame.SavedAtMs` (UTC epoch ms, `SaveGame.cs:26`), written every save (`:511`), autosave every 10 s (`Game1.cs:512, :2305`).
- On load, `SaveSystem.Deserialize(json, nowMs)` computes `OfflineSeconds = max(0, now − SavedAtMs)/1000` — `SaveGame.cs:483-489` (clock rollback floors to 0, tested `SaveSystemTests.cs:447`).
- Capped: `CreditedOfflineSeconds = clamp(elapsed, 0, 24h)` — `SaveGame.cs:691-694`, tested `SaveSystemTests.cs:456-457`.
- Applied: `Game1.cs:748-802`. Warren: `_warren.Tick((float)credited)` gated on `Unlocks.IsOpen(Activity.Warren)` (`:762-765`), then credited (`:766-768`). Champion: `credited × save.ChampionGleamRate × 0.5` (`:773`) — a rate measured live at x1 (`:3617-3631`), saved as `ChampionGleamRate` (`SaveGame.cs:211`).
- Determinism: `Warren.Tick` is pure arithmetic with a persistent fractional carry (`Warren.cs:176-179, :301-313`); many-small == one-big tick is tested (`WarrenTests.cs:155-170`). The clock is injected into Core (`SaveFile.cs:41`, `SaveGame.cs:466`) — Core never reads `DateTime`.
- Caveat: the fractional carry `_carry[]` is **not persisted** (`grep -n carry SaveGame.cs` → only unrelated comments), so up to <1 unit per currency is lost per load. Negligible; noted for completeness.
- Caveat: the offline credit runs from `Initialize` (`LoadOrStartFresh`), before `DrawWarren` has ever set `FacilityLevelCap` — irrelevant to the tick (the cap only gates upgrades), but `ConqueredRegions` had the same ordering bug until fixed at `Game1.cs:756-762`.

**Does the Warren feed combat power?** No direct path. `grep -rn -i warren src/ResonanceHunter.Core` outside
`Warrens/`, `Persistence/`, `Progression/` returns only comments (`SoloExpedition.cs:493-496`, `WaveModel.cs:176`,
`MasteryTree.cs:136`, `MemoryDust.cs:125-158`, `Checkpoints.cs:21-23`, `CharacterState.cs:13`, `DustEffects.cs:32`).
`SoloBattle`, `Build`, `SoloExpedition` do not reference it. Indirect paths only: Warren Gleam → `Hunter.Train`
→ stats → combat; Warren Dust → checkpoints (skip waves). No multiplier into the fight.

**"The Warren must never outrun the champion" — enforcement**: the depth cap (§1). `Warren.cs:233-237`
states the rule; `Game1.cs:1108-1111` derives it; `Warren.CanUpgrade` enforces it (`:276-277`);
`PointIncomeTests.cs:64-87` pins it at the model level. What is **not** enforced: the rule caps *levels*, not
*production*. A level-1 Warren already pays 66 Gleam/min and 619 Dust/min (*derived*) the moment a region is
conquered (wave 20 held), and offline accrual runs at that rate for up to 24 h regardless of depth. The cap
stops idle from buying *upgrades* the descent did not earn; it does not stop idle from out-earning play when
the player is away (*measured*: champion trained = 133/min live; Warren L1 = 66/min awake or asleep, 0.5× champ
rate applied only to the champion's half, `Game1.cs:773`).

**Gate on unlock**: production is gated on the same rule as the nav tile (`Unlocks.cs:151`
`Activity.Warren => f.RegionsConquered >= 1`; `Game1.cs:1085`, `:763`). `gleam_economy_test.cs:182-194` asserts
the gate is *closed* for a new save but cannot assert the production is gated (that code is in Game1).

---

## 5. Economy map (Q5)

| Currency / material | Type & owner | Faucets (file:line) | Sinks (file:line) | Verdict |
|---|---|---|---|---|
| **Gleam** | `Hunter.Gleam` (`HunterProgression.cs:101`) | Wave haul `Game1.cs:3637`; Warren live `:1089` / offline `:766`; champion offline `:774`; selling items `ForgeScreen.cs:887, :1193`; save restore `SaveGame.cs:609` | Training `Hunter.Train` (`HunterProgression.cs:455-461`, 25×1.13^r, 60 ranks × 9 stats — *measured* lifetime 2,646,459); Forge refine `ForgeScreen.cs:1015, :1042`; Warren upgrade `Game1.cs:1122` | Live, shared, the one binding Warren cost |
| **INSIGHT** (`WarrenResource.Mastery`) | `long _warrenMasteryPool` in Game1 (`:306`) — **no Core owner** | Warren live `Game1.cs:1091` / offline `:768` | Warren upgrade `Game1.cs:1123` | **Closed loop; non-binding** (§2) |
| **Memory Dust** | `MemoryDustTree.MemoryDust` (`MemoryDust.cs:113`) | Warren `Game1.cs:1090/:767`; mastery milestones `:2749`; conquest `:3721`; corruption `:4976` | Warren upgrade `Game1.cs:1124`; checkpoints `:3495` (`Checkpoints.cs:47`) | Live; *derived* oversupply ~20×–200× vs. sinks (§3) |
| **Mastery points** (skill tree) | `MasteryTree.Earned` (`MasteryTree.cs:156`) | Derived each frame from per-region `BestDepth` via `MasteryPoints.Total` (`Game1.cs:3714, :5142-5148`) | Node cost (`MasteryTree.cs:159, :189`); respec free (`:230`) | Live; NOT a stockpile — cannot be "credited" |
| **Trait points** | `MemoryDustTree.Earned` (`MemoryDust.cs:139`) | Derived: conquests + 2×corruption peak + Σ region MasteryLevel (`Game1.cs:3715, :5192-5200`) | `Purchase` (`MemoryDust.cs:179-185`), permanent | Live; the class name `MemoryDustTree` is the stale term |
| **Region Mastery Points** (per region, float) | `Region.RegionMasteryPoints` (`RegionAutomation.cs:59`) | `RecordActiveKill × DustEffects.MasteryRate` (`Game1.cs:3652`, 5/kill `RegionAutomation.cs:15`) | None — thresholds 500/2000 → `MasteryLevel` (`:101-109`) read by: difficulty `_regionProgression` (`Game1.cs:2537`), Dust milestone (`:2745`), trait points (`:5198`), Map label (`MapScreen.cs:529`) | Live as a meter; never spent |
| **Scrap / Essence / Core / Crystal** | `Hunter._mats` (`HunterProgression.cs:112-131`) | Wave spoils `Game1.cs:3658` (`WaveSpoils`); Haul.Cores→Core `:3648` (HARVEST/LODESTONE only); salvage `ForgeScreen.cs:854, :917`; chest reward `:1184`; filter compensation `Game1.cs:3760` | Refine (Scrap+Gleam `ForgeScreen.cs:1015`; Crystal `:1041`); socket (Essence `:1876`); reforge (`Reforge.cs:159`); trader (`WanderingTrader.cs:171`, reachable via `Game1.cs:2684`); training reset (1 Crystal, `HunterProgression.cs:502`) | Live, each tier has a sink (`Material.cs:14-25`) |
| **Charters** (Refine/Reforge/Salvage/Merge) | `Hunter._charters` (`HunterProgression.cs:134-165`) | `WaveSpoils` 2.5 %/wave from wave 5 (`WaveSpoils.cs:61, :68`) → `Game1.cs:3665`; dev fixture `ForgeScreen.cs:397-399` | Refine `ForgeScreen.cs:1014`; Salvage `:848`; Reforge `Reforge.cs:158` | Live. **`Charter.Merge`** is not in the drop pool (`WaveSpoils.cs:71`) and is converted to Salvage on load (`Game1.cs:613`) — migration-only member |
| **"Cores"** (currency) | retired 2026-08-24 | `Haul.Cores` channel still written by HARVEST/LODESTONE (`WaveModel.cs:318-338`, `SoloExpedition.cs:486-490`) | Re-pointed to `Material.Core` (`Game1.cs:3646-3650`) | Name is a remnant; channel is live as a Core-material faucet |
| **Warren XP / Level** | `Warren.Xp/Level` (`Warren.cs:194-198`) | `Upgrade` (`:284-289`) | Nothing spends it; Level feeds the multiplier (`:204-212`) | Live meter |

**Closed loops**: INSIGHT (only). **Dead currencies**: none strictly dead; "Cores" is a dead *name*.
**Not-quite-currencies with no consumer**: `Region.IdleEfficiencyPercent()` — see §9.

---

## 6. What the WarrenScreen exposes (Q6)

Read in full (`WarrenScreen.cs:1-449`). Host-set inputs: `Warren`, `GleamOwned`, `MasteryOwned`, `DustOwned`,
`DevWarrenDebug` (`:35-39`). Host-consumed output: `ConsumeUpgrade()` (`:42-43`).

| Element | Lines | Does something? |
|---|---|---|
| Title + subtitle "THE WARREN EARNS WHILE YOU ARE AWAY — COME BACK AND SPEND" | `:132-140` | Copy only |
| Overview: name, level crest with XP arc, `+% ALL PRODUCTION`, XP bar, `TOTAL PRODUCTION` per currency, explainer paragraph | `:199-265` | All values real (`Warren.Name/Level/Xp/XpToNext/AllProductionBonus/ProductionPerMinute`) |
| 8 facility cards; click selects; milestone pips; per-card `+N /min` | `:267-298` | Click → `_selected` → detail panel. Real |
| Bonus strip: 5 chips (GLEAM / INSIGHT / DUST per-level, ALL THREE, CONQUEST) + formula caption | `:300-341` | Real (`ResourceBonus`, `AllProductionBonus`, `ConquestBonus`, `Multiplier`) |
| Detail: description, OUTPUT now → NEXT, MILESTONES row, COST TO UPGRADE ×3, cap line, UPGRADE / DEPTH LOCKED button | `:343-397` | Button sets `_upgradeRequest`; host spends and upgrades (`Game1.cs:1118-1126`). Real |
| "INSTANT UPGRADE · NO WAIT" | `:390` | Copy only; there is no timer to skip |
| Dev overlay (F7) | `:438-448`, `Game1.cs:2321` | Dev only |

**Nothing the player can click does nothing.** Findings anyway:
- `public void Draw(SpriteBatch b) => Draw(b, new Point(-1,-1), false)` (`:124`) has **no caller**:
  `grep -rn "_warrenScreen.Draw(" src` → only `Game1.cs:1116` (3-arg). Dead overload.
- `ResFromLabel(string)` (`:101-107`) parses a display string back to the enum because `DrawReq` takes a
  label string (`:399-402`). String-keyed round trip inside one class; pass the enum.
- Class doc (`:16-17`) still promises "The creature 'den' … reached via the CREATURES button" — no such
  button exists in the file. Stale.
- `:11` cites "the Warren production spec (rev 1)"; the GDD cites `warren_screen_production_spec_revision_1.md`
  (`warren-facilities.md:6`). `find . -iname "*warren_screen_production_spec*"` → nothing. The spec is not in the repo.
- The bonus-strip literals "2% A LEVEL", "1.3% A LEVEL", "0.9% A LEVEL", "3% A LEVEL AFTER 1" (`:323-326`)
  duplicate `WarrenTuning` constants (`Warren.cs:74-77`) as strings; retuning the record silently lies here.
- Overview copy "GLEAM and DUST are used all over the game" (`:262`) — Dust has exactly two sinks (§3).
- `AssetLibrary.cs:158` aliases `stat_guile → icon_facility_hoardvaults`: a facility icon doubles as a stat icon.
- The Warren tour (`Onboarding.cs:320-333`, spotlights `WarrenScreen.cs:61-67`) is live (`Game1.cs:3308`) and its copy is accurate.
- Mutation in the Draw pass: `DrawWarren` (`Game1.cs:1102-1127`) is called from `Draw` (`:4065`) and performs
  spend + `Warren.Upgrade` + `Save()` there (documented at `:1096-1101`, "the same place Forge does its Refine").
  The screen itself is request/consume (good); the host's consume sits on the wrong side of Update/Draw.

---

## 7. Save fields and the rename (Q7)

`SaveGame.cs:251-267` — the whole Warren surface:

| JSON property | Type | Default | Written | Read |
|---|---|---|---|---|
| `WarrenLevel` | int | 1 | `SaveGame.cs:512` | `RestoreWarren` `:679` |
| `WarrenXp` | int | 0 | `:513` | `:679` |
| `WarrenFacilities` | `Dictionary<string,int>` keyed by **`FacilityKind` enum NAME** (`Nursery`, `Tunnels`, `ForagingPits`, `ScavengerRuns`, `BreedingChamber`, `RitualNest`, `HoardVaults`, `SentryBurrows`) | empty → all L1 | `:514-516` | `:676-678` (`Enum.TryParse`, unknown keys silently dropped) |
| `WarrenMasteryPool` | long | 0 | `:517` | `Game1.cs:651` |
| `SavedAtMs` (shared) | long | — | `:511` | `:484` — the offline basis; there is **no per-facility last-tick** |

Not persisted: `Warren._carry` (fractional remainder), `FacilityLevelCap` (re-derived), `ConqueredRegions` (re-derived from `World`), `Name` (re-derived).

Doc comment on `WarrenMasteryPool` (`SaveGame.cs:259-266`) is false: "the host adds it into SetEarned".

**Renaming `WarrenResource.Mastery → Insight` — what breaks on load**:
- The enum **name** is never serialized. `WarrenYield`/`WarrenCost` are positional records (`Warren.cs:62-68`);
  `Tick` indexes `_carry`/`outv` by `(int)` cast (`Warren.cs:305-312`, `(WarrenResource)i` for `i` in 0..2).
  **Renaming is save-safe; reordering the enum is not** (Gleam=0, Mastery=1, Dust=2 is load-bearing).
- `SaveGame.WarrenMasteryPool` **is** a JSON name. Renaming the C# property (e.g. to `WarrenInsightPool`)
  without `[JsonPropertyName("WarrenMasteryPool")]` or a fallback read would load as 0 for every player —
  `System.Text.Json` skips unknown members silently (`SaveGame.cs:53-56` relies on exactly that). Migration:
  keep the JSON name, or read both and prefer the new.
- `WarrenFacilities` keys are `FacilityKind` names: renaming any *facility* enum member drops that facility to
  level 1 on load with no error (`SaveGame.cs:677-678`). No facility rename is proposed, but the risk is the same shape.
- Compile-time fallout of the resource rename (all mechanical): `Warren.cs:13, :52-53 (already say Insight), :62, :65, :76, :87-88, :210`;
  `WarrenScreen.cs:81, :94, :104, :117, :244, :315, :324, :373, :376`; `Game1.cs:306, :651, :768, :876, :993, :1091, :1114, :1119, :1123, :1971`;
  `WarrenTests.cs:88`; `gleam_economy_test.cs:9` (namespace only).
- Tests that would catch a botched migration: **none**. `grep -rn "WarrenFacilities\|WarrenMasteryPool\|RestoreWarren" tests` → nothing.
  `SaveSystemTests.cs:112-113, :142-143` round-trips only `WarrenLevel`/`WarrenXp` through JSON. `WarrenTests.cs:173-190`
  tests `Warren.Restore` in memory, not the JSON path. The GDD's "Save round-trips level, XP, and facility levels ✓ (unit)"
  (`warren-facilities.md:86`) overstates what is tested.

---

## 8. Adjacent files in scope

**`Economy/Charters.cs`** (read in full). Four-member enum + three text tables (`Name/Blurb/Short/Plain`,
`:48-89`). Live: drops via `WaveSpoils` (§5), spent in Forge/Reforge. `Charter.Merge` (`:36-37`) survives only
to be migrated (`Game1.cs:611-613`); `Blurb`/`Plain` still describe it. Classification B.

**`Economy/WanderingTrader.cs`** (read in full). Pure, hash-seeded stall; host owns the clock
(`Game1.cs:2649`); reachable (`Game1.cs:2669, :2684`, `ChestScreen.cs:625`). A materials sink by design
(`:22-25`). Uses `Source` for item element (`:90`) — the Source concept survives the Form deletion. `TraderTuning`
(`:178-189`) is data-driven. Nothing Warren-related; included because it is a material sink. Healthy.

**`Economy/HunterProgression.cs`** (training sink). `Train` (`:455-461`), `CostOfRank` (`:447-449`),
`ResetTraining` for 1 Crystal, no refund (`:480-506`), `TotalLifetimeSink` (`:539-543`). *Measured* lifetime sink
2,646,459 Gleam (`gleam_economy_test` output, §12). **Stale figures in comments**: `Warren.cs:35` and
`SoloExpedition.cs:492` cite "79,578 Gleam lifetime training sink" — 33× below the current constants
(`StatRankCap = 60`, `TrainingGrowthRate = 1.13`, `HunterProgression.cs:17-25`). `Hunter` doc at `:105-110` still
talks about "a creature's evolution". `Sell` doc at `:519-524` still gates on `EquippedToCreatureId`.

**`Automation/RegionAutomation.cs`** (read in full). Class `Region` in namespace `Automation` — the
creature-farm survivor. Live members: `RegionMasteryPoints`, `BestDepth`, `StartWave`, `MasteryLevel`,
`RecordActiveKill` (§5). Dead/unreachable:
- `ParClearTimeSeconds` (`:57`) — set by `World` (`Regions.cs:175`) and **read by nobody in src**:
  `grep -rn "\.ParClearTimeSeconds" src` → only `EncounterTemplate.ParClearTimeSeconds` (a different property).
  Only reader is `RegionFarmTests.cs:94`.
- `IdleEfficiencyPercent()` (`:119-120`) → `EfficiencyContract.IdleEfficiencyPercent(level, 0f)` — sole
  consumer is a **label**: `MapScreen.cs:529` "EARNS {…}% WHILE YOU ARE AWAY". No payment path reads it; offline
  champion pay is `ChampionGleamRate × 0.5` (`Game1.cs:773`) regardless of region. The Map is stating a
  mechanic that does not exist.
- `MasteryLevel.OptimizedTeam` (`EfficiencyContract.cs:11`) unreachable (`RegionAutomation.cs:38-40, :98-100`);
  `IdleBand(OptimizedTeam)` and `MaxIdleEfficiencyPercent = 120` (`EfficiencyContract.cs:46, :53`) are unreachable code.
- `LootContext.ActiveEfficiencyPercent` (`LootSystem.cs:201`) defaults to 100 and is **never assigned**:
  `grep -rn "ActiveEfficiencyPercent\s*=" src` → nothing. The term at `LootSystem.cs:348` is always 0. (Outside this
  area's files; recorded because it is the other half of `EfficiencyContract`.)
- `AutomationTuning` (`:12-21`) is fine but misnamed — nothing here automates.
- The namespace `ResonanceHunter.Core.Automation` also holds `Source.cs` (the element enum used everywhere) — a
  gameplay enum living in a retired subsystem's namespace.

**`Progression/Unlocks.cs`**: `Activity.Warren => f.RegionsConquered >= 1` (`:151`), price caption
"Conquer a region" (`:185`), headline (`:207`). Consistent with the production gate. Healthy.

**`Progression/Onboarding.cs`**: Warren tour, three steps (`:320-333`), copy accurate. `TourTarget.Facilities/
FacilityDetail/WarrenOverview` (`:89-95`) → spotlights (`WarrenScreen.cs:61-67`). Healthy.
**`Progression/Tutorial.cs`**: no Warren step; `SpendGleam` teaches Gleam → STATS (`:17-18, :254-256`). Healthy.

**`Expeditions/GiftChests.cs`** (read in full). One welcome chest, `Materials = 0` (`:96`), deterministic open
(`:139-162`). Not a currency source; no Warren interaction. Healthy; out of economic scope.

**`Persistence/SaveGame.cs` — legacy field still written**: `RegionMasteryPoints` (single-region legacy,
`:50-51`) is written on every save (`:552`) and read only when `RegionFarms` is empty (`Game1.cs:740-744`).
Migration-only data that is still being produced.

**`Game1.cs` — `_deepestEver` is restored from `save.MasteryEarned`** (`Game1.cs:641`, field doc `SaveGame.cs:165`).
A depth stored under a points name; it also feeds `DeepestAnywhere()` (`:5205`) and therefore the Warren cap.

---

## 9. Liveness summary (the house bug species)

| Mechanic | Consumer? | Evidence |
|---|---|---|
| INSIGHT production | Only its own upgrade line | §2 |
| `Region.IdleEfficiencyPercent` / `EfficiencyContract.IdleBand` | Label only (`MapScreen.cs:529`); no payout reads it | §8 |
| `Region.ParClearTimeSeconds` | None in src | `grep` §8 |
| `LootContext.ActiveEfficiencyPercent` | Never set; term always 0 | `grep` §8 |
| `MasteryLevel.OptimizedTeam` | Unreachable | `RegionAutomation.cs:101-109` never returns it |
| `WarrenScreen.Draw(SpriteBatch)` | No caller | §6 |
| `Charter.Merge` | Not dropped; migrated away on load | `WaveSpoils.cs:71`, `Game1.cs:613` |
| `Warren.OnMilestone` (`Warren.cs:133`) | `grep -rn OnMilestone src tests` → declaration only | dead property |
| `WarrenYield.Any` (`Warren.cs:67`) | `grep -rn "\.Any\b" WarrenScreen.cs Game1.cs` for yields → not used on a `WarrenYield` | dead property (host checks `> 0` per field, `Game1.cs:1089-1090`) |
| Facility descriptions promising storage/protection | No mechanic | §1 |
| Bonus-strip "% A LEVEL" strings | Duplicate constants as text | §6 |

---

## 10. Documentation drift (what the docs say vs. what runs)

| Doc | Claim | Reality |
|---|---|---|
| `design/gdd/warren-facilities.md:32-34, :57-58, :68` | Mastery pool "is added to the derived Mastery-Earned total … folded into `SetEarned` each frame" | False — `Game1.cs:294-305`; only `Game1.cs:3714` calls `SetEarned`, from depth |
| `warren-facilities.md:13-14, :71` | CREATURES sub-view, `AutomationScreen` | Retired 2026-08-24 (`Game1.cs:289-291`) |
| `warren-facilities.md:44, :50` | Nursery 520/min; Gleam cost `18700 × 1.40^L` | Code: 17/min; `180 × 1.50^L` (`Warren.cs:48, :85-86`) |
| `warren-facilities.md` (whole) | No depth cap, no milestones, no conquest bonus, no unlock gate | All four exist in code |
| `docs/architecture/ADR-004:29-33, :35-38` | Pool → `SetEarned`; creature den preserved; "Cores, evolution, region farming … intact" | All false today |
| `design/gdd/progression.md:27, :34-35, :61-62` | "Warren → mastery … feeds the build tree"; Mastery faucet "depth + conquest + Warren"; Dust sink "the blessing tree" | False on all three (§2, §3) |
| `design/gdd/game-flow.md:229, :287-305` | "The Warren pays neither kind of point. It produces Gleam and materials only"; facilities unlock one per conquest; facilities produce material tiers | Code produces Gleam + Insight + Dust, **no materials**; all 8 facilities exist from the start (`Warren.cs:181-186`); only the depth cap (`§4.5`) is implemented |
| `design/gdd/region-mastery-automation-system.md` (956 lines) | Creature-team automation, RMP from team work-ticks, Stages 1–4 | 62 "creature" mentions, 0 "Warren"; superseded per `game-flow.md:475`; what survives is `RecordActiveKill` + thresholds |
| `Warren.cs:9-11` (enum remark) | "Mastery is the build-tree currency … a real cross-system idle loop" | False; the closed loop it disclaims is what exists |
| `Warren.cs:35`, `SoloExpedition.cs:492` | "79,578 Gleam lifetime training sink" | *Measured* 2,646,459 |
| `MemoryDust.cs:150-154` | `AwardFromMastery` "is the ONLY faucet" | Five faucets (§3) |
| `SaveGame.cs:259-266` | pool "added into SetEarned" | False |

---

## 11. Numerical stacking / balance observations (derived, not measured in play)

- **Multiplier chain per resource** (`Warren.cs:218`): `1 + all-production + conquest + own-track`. Additive,
  not multiplicative — good. Then `× MilestoneMultiplier × Level` inside each facility (`:139`) — the level term
  is linear, milestones a +15 %-per-5 step. Modest.
- **Three cost currencies for one action** (`WarrenCost`, `:62`): two of them (Insight, Dust) are produced by the
  thing being upgraded at 5×–20× the rate they are consumed. *Derived* L1/1 conquest: Insight 267/min vs. 54 cost;
  Dust 619/min vs. 28 cost; Gleam 66/min vs. 270 cost. The three-currency cost is a one-currency cost wearing two hats.
- **Dust facilities' base rates** (450, 108) are 26× the Gleam facilities' (12–17) and 4× the Insight ones — the
  same "calibrated against an art mock-up" pattern `Warren.cs:29-37` describes for Gleam, apparently never
  re-based for Dust when Gleam was cut 30×. Dust's sinks: 28-then-×1.25 per upgrade; 25/wave for checkpoints.
- **24 h offline at level 1** (*derived*): ≈ 95k Gleam, ≈ 385k Insight, ≈ 891k Dust — before any facility levels.
  Against a lifetime Gleam sink of 2.65M this is 3.6 % per day of Gleam; fine. Against Dust's sinks it is
  effectively infinite.
- The `gleam_economy_test` guards Gleam only (`:197-250`, 100–500 h band); nothing guards Dust or Insight ratios.

---

## 12. Tests run

`dotnet test tests/unit/ResonanceHunter.Core.Tests --filter "FullyQualifiedName~Warren|~PointIncome|~GleamEconomy|~RegionFarm|~SaveSystem"`
→ **50 passed, 0 failed** (95 ms).
`GleamEconomyTest` printed (measured): `LIFETIME TRAINING SINK: 2,646,459 Gleam (9 stats x 60 ranks)`;
`Warren, level 1, 0 conquests 60 gleam/min`; `Warren, level 1, 1 conquest 66`; `champion, fresh, waves 1-8 52`;
`champion, trained, waves 1-40 133`; total `199 gleam/min → 221.3 hours`.
Note: `gleam_economy_test.cs:4, :85-87` builds its champion through the **legacy** `WovenAbility`/`Form.Strike`
path (`using ResonanceHunter.Core.Abilities` = `Weaving/ResonanceWeaving.cs:6`). Deleting Form breaks this test's
fixture; it should be rebuilt on `SkillCatalogue`.

---

## 13. Classification

### Authoritative (keep; the current truth)
- `Warrens/Warren.cs` — `Warren`, `Facility`, `Facilities`, `WarrenTuning`, `WarrenCost/Yield`. Pure, tested, injected clock. The `Mastery` member and the enum remark are wrong; the mechanics are right.
- `Game1.cs:1058-1092` `TickWarren` + `:748-768` offline credit + `:1102-1127` `DrawWarren` — the only host.
- `SaveGame.cs:251-267` + `RestoreWarren :673-680` + `CreditedOfflineSeconds :683-694`.
- `Unlocks.cs:151` gate; `Onboarding.cs:320-333` tour.
- `MasteryPoints.cs` (skill points from depth), `Game1.cs:5142-5148, :5192-5200` (both point derivations).
- `Checkpoints.cs:35, :47` — Dust's real sink.
- `Material.cs`, `Charters.cs` (minus `Merge`), `WanderingTrader.cs`, `HunterProgression.cs` training.
- `RegionAutomation.cs` `Region` — as a per-region progress record (`RegionMasteryPoints`, `BestDepth`, `StartWave`).

### Obsolete
| Name | Files | Class | Why | Replacement |
|---|---|---|---|---|
| INSIGHT as a currency (`WarrenResource.Mastery`, `_warrenMasteryPool`, `WarrenMasteryPool`, `MasteryCostBase/Growth`, `MasteryBonusPerLevel`) | `Warren.cs:13,76,87-88,210`; `Game1.cs:306,651,768,993,1091,1114,1119,1123`; `SaveGame.cs:259-267,517`; `WarrenScreen.cs` (§2 list) | **C** | Closed loop, non-binding, no Core owner; the design (`game-flow.md:229, :301-302`) says Gleam + materials only | Either delete the resource (2-currency Warren) or make BreedingChamber/RitualNest produce a material tier per `game-flow.md:304-305`. Save: keep `WarrenMasteryPool` readable for one version or drop with a note |
| `Charter.Merge` | `Charters.cs:36-37, :53, :62, :71, :88`; `Game1.cs:611-613` | **B** | Not dropped (`WaveSpoils.cs:71`); migrated to Salvage on load | Keep the enum member + the one-line migration; delete its text rows |
| `SaveGame.RegionMasteryPoints` (single-region legacy) | `SaveGame.cs:50-51, :552`; `Game1.cs:740-744` | **B** | Only read when `RegionFarms` is empty; still written every save | Stop writing; keep the read for one version |
| `Region.ParClearTimeSeconds`, `Region.IdleEfficiencyPercent`, `EfficiencyContract.IdleBand/IdleEfficiencyPercent/MaxIdleEfficiencyPercent`, `MasteryLevel.OptimizedTeam` | `RegionAutomation.cs:47-57, :111-120`; `EfficiencyContract.cs:6-12, :43-63`; `MapScreen.cs:529` | **A** | No payout reads them; the Map label states a non-mechanic | Delete; replace the Map line with what offline actually pays (champion rate × 0.5) or nothing |
| `LootContext.ActiveEfficiencyPercent` | `LootSystem.cs:201, :348` | **A** | Never assigned; term always 0 | Delete the field and the term |
| `WarrenScreen.Draw(SpriteBatch)` | `WarrenScreen.cs:124` | **A** | No caller | Delete |
| `Warren.OnMilestone`, `WarrenYield.Any` | `Warren.cs:133, :67` | **A** | No consumer | Delete |
| `Haul.Cores` naming | `WaveModel.cs:318-338`; `SoloExpedition.cs:486-490`; `Game1.cs:3646-3650` | **C** | "Cores" the currency is gone; the channel is a Core-material payout | Rename the channel `CoreMaterial` (or route HARVEST/LODESTONE through `WaveSpoils`) |
| `design/gdd/warren-facilities.md`, `docs/architecture/ADR-004`, `design/gdd/progression.md §3/§5`, `design/gdd/region-mastery-automation-system.md` | — | **C** (docs) | See §10 | Rewrite the GDD from `game-flow.md §3.8/§4.5` + code; mark ADR-004 Superseded; archive the automation GDD |
| Comments: `Warren.cs:9-11, :35`; `SoloExpedition.cs:492`; `SaveGame.cs:259-266`; `MemoryDust.cs:150-154`; `WarrenScreen.cs:16-17`; `HunterProgression.cs:105-110, :519-524` | — | **A** | Each states something false today | Fix in the same commit as the code they describe |

### Stale terms
| Term | Where | Replacement |
|---|---|---|
| `WarrenResource.Mastery` | `Warren.cs:13` and all users | `Insight` (if kept at all) — or remove |
| `MemoryDustTree` as the trait tree's class name; `AwardFromMastery` | `MemoryDust.cs:102, :155` | `TraitTree` / `AddDust` |
| `ResonanceHunter.Core.Automation` namespace, `AutomationTuning`, `Region` living in "Automation" | `RegionAutomation.cs:4, :12, :43`; `Source.cs` in the same folder | `Core.Regions.RegionProgress` / `RegionTuning`; move `Source` next to the skill model |
| `SaveGame.MasteryEarned` holding deepest-ever depth | `SaveGame.cs:165`; `Game1.cs:641` | `DeepestEver` (JSON-name pinned) |
| "Cores" | `WaveModel.cs:324-338` | `CoreMaterial` |
| "CREATURES button", "creature's evolution", `EquippedToCreatureId` | `WarrenScreen.cs:16`; `HunterProgression.cs:105-110, :519-524` | delete |
| "Mastery Points" for the Warren output | `warren-facilities.md:28`, `ADR-004:20`, `progression.md:61` | Insight / remove |

### Hardcoded branches on content IDs
None in the Warren. The only enum-keyed branches are the three-way `WarrenResource` switches (`Warren.cs:207-212`;
`WarrenScreen.cs:79-118`) and the icon-key-by-name at `WarrenScreen.cs:291` — all presentation or tuning, none in combat.

### Duplicated responsibilities
- `WarrenScreen.cs:323-326` re-states `WarrenTuning` percentages as literal strings ↔ `Warren.cs:74-77`.
- `WarrenScreen.cs:376` recomputes affordability ↔ `Warren.CanAfford` (`Warren.cs:269-273`); the host then checks `CanUpgrade` again (`Game1.cs:1119`). Three affordability checks for one button.
- `DeepestAnywhere()` (`Game1.cs:5203-5208`) ↔ `SkillPointsEarned()` (`:5142-5148`) both walk `Regions.All` for `BestDepth`; plus `_deepestEver` shadow copy (`:252`, restored from `MasteryEarned`).
- Dust award multipliers: `15 × reward` (`Game1.cs:2749`), `40 × reward` (`:3721`), `DeepeningDustAward` (`:4976`) — three ad-hoc Dust faucets in the host with no Core owner.

### Core/presentation coupling
- `Game1.cs:1102-1127` `DrawWarren` spends currencies, calls `Warren.Upgrade`, and `Save()` inside the Draw pass (`Game1.cs:4065`).
- `_warrenMasteryPool` (`Game1.cs:306`) — a currency balance owned by the MonoGame host, not Core. Every other balance has a Core class (`Hunter`, `MemoryDustTree`).
- `FacilityLevelCap` is set only in `DrawWarren` (`Game1.cs:1111`); if any non-screen path ever upgrades, the cap is `int.MaxValue`.
- `WarrenScreen.cs:129` calls `Game1.ToOverlay` (static) — screen reaches up to the host.

### Serialization risks
| Field | Risk | Migration |
|---|---|---|
| `WarrenMasteryPool` | C# rename without JSON pin → silently 0 for all players | `[JsonPropertyName]` or dual-read |
| `WarrenFacilities` keys = `FacilityKind` names | Any facility rename → that facility resets to L1 silently (`SaveGame.cs:677-678`) | name-map on read |
| `WarrenResource` order | `Tick` indexes by `(int)` (`Warren.cs:305-312`); reordering mis-credits currencies (not a save issue, a runtime one) | never reorder; or index by enum explicitly |
| `MasteryEarned` (= deepest-ever) | Renaming to what it is breaks every save | JSON-name pin |
| `RegionMasteryPoints` legacy | Still written; harmless but perpetuates the field | stop writing |
| `SavedAtMs` | Offline basis; a save written with a future clock floors to 0 (`SaveGame.cs:484`) — correct | none |
| `Warren._carry` | not saved; ≤1 unit/currency lost per load | accept |

### Closed-loop currencies
- **INSIGHT** (`WarrenResource.Mastery`): produced `Game1.cs:1091/:768`, spent `Game1.cs:1123`, nothing else (§2).

### Good to preserve
- `Warren.Tick` carry design (`Warren.cs:176-179, :301-313`) and its determinism test (`WarrenTests.cs:155-170`).
- Host-owns-the-clock, Core-takes-`nowMs` (`SaveFile.cs:41`, `SaveGame.cs:466`); 24 h clamp; rollback floor.
- Depth cap as the one rule that ties idle to play (`Warren.cs:233-248`, `Game1.cs:1111`, `PointIncomeTests.cs:64-87`).
- Request/consume screen pattern (`WarrenScreen.cs:42-43`, `Game1.cs:1118`).
- Production gated on the same `Unlocks` rule as the tile (`Unlocks.cs:146-151`).
- Additive multiplier chain (`Warren.cs:218`).
- Name-keyed save dictionaries with lenient parse (`SaveGame.cs:43-46, :676-678`).
- Both point currencies derived-not-banked (`Game1.cs:3714-3715`).
- `gleam_economy_test`'s "state the economy as hours of play" discipline (`:16-31`).

### Overengineering risks
- Eight facilities for three knobs. A facility per *material tier* (`game-flow.md:304-305`) would give eight cards eight reasons to exist; otherwise four Gleam cards are one card.
- Three cost currencies per upgrade when one binds.
- A Warren level/XP layer whose only effect is `+3 %/level` on top of per-currency `+k/level` on top of conquest `+10 %` on top of milestones `+15 %/5` — four bonus tracks on a 66/min faucet.
- Do **not** build a generic "producer/consumer resource engine" to fix this; `Warren` is already the right size.

### Abstraction opportunities (only after two real users)
- A `Wallet` in Core holding Gleam / Dust / materials / charters (today: `Hunter` holds Gleam+materials+charters, `MemoryDustTree` holds Dust, `Game1` holds Insight). Two real users exist (Forge and Warren both spend across three owners) — this one qualifies.
- One `Economy` host method for "credit a `WarrenYield`" used by both the live tick (`Game1.cs:1088-1091`) and the offline credit (`:764-768`) — they are copy-pasted today.

---

## 14. Open questions (for the owner, not asserted)

1. **Should INSIGHT exist at all?** `game-flow.md:229, :301` says the Warren produces "Gleam and materials only". The cheapest honest fix is a two-resource Warren (Gleam + Dust) with BreedingChamber/RitualNest re-pointed at a material tier. The alternative — wiring it into the mastery tree — is the balance decision `Game1.cs:302-305` flags as the designer's call.
2. **Dust supply**: 619/min at L1 (*derived*) against 28-Dust upgrades and 25-Dust/wave checkpoints. Is Dust meant to be abundant "fuel" for checkpoints (never a decision) or scarce? The Dust base rates (450/108) look un-rebased from the Gleam cut.
3. **Facilities unlock by conquest** (`game-flow.md:297-298`, "A new player has one facility, not eight") — was this dropped deliberately, or not yet built? `Warren.cs:181-186` constructs all eight at L1.
4. **Offline champion pay vs. the Map's "EARNS X% WHILE YOU ARE AWAY"**: which one is the design? Today the label reads `IdleEfficiencyPercent` (25/50/80 %) and the payout uses `ChampionGleamRate × 0.5`. One must go.
5. **`_deepestEver` restored from `save.MasteryEarned`** (`Game1.cs:641`): is the field ever written with anything other than deepest depth now? If not, rename with a JSON pin.
6. **Where should the three Dust faucets in Game1 live** (`:2745-2750`, `:3721`, `:4976`)? A `DustAwards` static in Core next to `CorruptionScaling` would let a test pin them; today no test covers any of the three amounts.
7. The GDD's reference spec `warren_screen_production_spec_revision_1.md` is not in the repo. Was it ever, or is the GDD citing a file that lived in a chat?
8. `gleam_economy_test.cs:85-87` fixtures on `WovenAbility`/`Form` — rebuild on `SkillCatalogue` before deleting Form, or the only economy-ratio test in the suite dies with it.
