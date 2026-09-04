# PHASE 1 DESIGN — the tables the brief requires before any code

Produced by `design-workflow.js` (run `wf_22be5d4a-181`): two independent signature panels (a third
died on an output limit), three judges scoring every design of every panel through a different lens,
and five catalogue designs. The judges verified read sites line by line and rejected several designs
for naming dials the fight does not read; their verdicts are recorded in full so the synthesis can be
checked against them.

## STRUCTURAL UNLOCKS AND SAVE MIGRATION — where the tree's systems go, and how a save carries them across

### 0. What this section decides, and the two laws it answers to

LAW 12 — *structural functionality is never an exciting Trait choice* — and LAW 13 — *automation belongs to Warren* — are the whole of this section. Between them the 51-node tree is holding eleven structural entitlements hostage, and every one of them has to land somewhere that is already **monotone**, already **derived from a fact the save carries**, or already **a thing the player buys with the idle economy**.

The answer, in one line each:

| Capability | Was | Becomes | Costs the player |
|---|---|---|---|
| Skill slots | `Unlocks.SkillSlots` 1→4, then `weave_5` → 5 | `Unlocks.SkillSlots` alone, hard-capped at 4 | nothing; the fifth slot is **removed** (D8) |
| Keystone sockets 2 and 3 | `socket_2` (2 pts), `socket_3` (10 pts cumulative) | `Unlocks.KeystoneSockets` — 2nd at 2 conquests, 3rd at 4 conquests | nothing; automatic |
| Auto-sell Common / Uncommon | `ledger`+`filter_common` / `+filter_uncommon` | **SCAVENGER RUNS** facility, level 2 / level 4 | Gleam + Dust on the Warren's own ladder |
| Auto-merge after a chest crack | `ledger`+`forge_insight`+`auto_merge` | **HOARD VAULTS** facility, level 2 | Gleam + Dust on the Warren's own ladder |
| Vow knowledge (13 vows) | 5 `vow_study_*` nodes | keep-the-rule-first discovery (vow design owns the rules); this section owns the migration | nothing |
| Keystone knowledge (19) | 19 `ks_*` nodes | region conquest / region mastery (keystone design owns the map); this section owns the migration | nothing |
| Forge SCREEN access | *never gated on a node* (`Unlocks.cs:127`) | unchanged — only the node NAME lied | — |
| Vow SYSTEM access | implicit: knowing ≥1 vow | the BUILD screen's own gate (wave 5) plus one free tutorial vow | — |
| Salvage +15% (`efficient_forge`) | tree numeric | **deleted**, its multiplier folded into the base rate for everyone | — |
| Region-mastery rate +5…20% (`recall_1..4`) | tree numeric | **deleted**, uncompensated | — |
| Vow bonus ×1.25 (`artifice_vows`) | tree numeric | **deleted** — mastery and THE OATHBOUND already own that dial | — |

The brief's §55 premise does not hold and I am not acting on it: **`Activity.Forge` already opens on `ChestsEverHeld >= 1 || ItemsOwned >= 2`** (`Unlocks.cs:127`). No trait node has ever gated the Forge screen. What is behind a purchase is the Forge's *upgrades*, and the node whose NAME says otherwise (`ledger`, "OPENS AUTO-SELL AND FORGE", `MemoryDust.cs:341`) dies with its file. The one real defect in that gate is a caption that names only one of its two clauses (`Unlocks.cs:184`, "Earn a chest from a boss"); it is fixed here to **"Earn a chest, or find two items"** because §93 makes stale player-facing copy part of this job.

### 1. A row per structural node: what it is, what it gates, its new owner, the exact milestone

Every node in `MemoryDustTree.Catalog` that the audit classes STRUCTURAL, plus the four that gate a system by name. Milestones are stated as expressions over facts that already exist and already only grow.

| Old node (cost) | What it actually is | What it gates, with the live consumer | New owner | Exact milestone |
|---|---|---|---|---|
| `socket_2` (2) | +1 keystone socket, 1→2 | `DustEffects.KeystoneSockets` `:205-209` → `PlayerLoadout.KeystoneCapacity` → refusal at `PlayerLoadout.cs:225`, truncation at `:337` | `Unlocks.KeystoneSockets(UnlockFacts)` — a new sibling of `Unlocks.SkillSlots` | `f.RegionsConquered >= 2` |
| `socket_3` (5, req `weave_5`) | +1 socket, 2→3 | same wire | same | `f.RegionsConquered >= 4` |
| `weave_5` (3, req `socket_2`) | skill capacity 4→5 | `DustEffects.SkillSlots` `:212-216` → `Game1.ApplySkillCapacity` `:3501-3507` → `PlayerLoadout.SkillCapacity` → `Build.SlotCapacity`, and `SoloBattle.cs:2587` reads it as VOW OF COMPLETION's demand | **nobody — the fifth slot is deleted** | n/a. `Unlocks.SkillSlots` keeps its ladder (1 · wave 5 · wave 12 · first conquest) and 4 becomes the ceiling, not the hand-over point |
| `vow_study_1` (1) | teaches `vow_complete`, `vow_deliberate` | `DustEffects.KnownVows` `:120-126` → `BuildComposer.cs:148` (an untaught vow pays nothing), `Game1.cs:800-802` (stripped on load), `LoadoutScreen.cs:538` (the picker) | Vow discovery (sibling design) — **prove the rule before it pays** | per-vow; this section only guarantees the migration grants what a save already knew |
| `vow_study_2` (2) | `vow_pure`, `vow_frantic` | same | same | same |
| `vow_study_3` (2) | `vow_singular`, `vow_bluntedge` | same | same | same |
| `vow_binding` (3) | `vow_barefoot`, `vow_openhand`, `vow_bareskull` | same | same | same |
| `vow_sacrifice` (3) | `vow_fragility`, `vow_reckless_offering`, `vow_unguarded`, `vow_unbound` | same | same | same |
| `ledger` (1) | **pure gate, no reader.** Its name claims it opens the Forge; it does not | nothing — it is wired only because three other nodes require it (`DustEffects.cs:252`) | **deleted** | n/a |
| `filter_common` (2, req `ledger`) | auto-sell floor = Common | `DustEffects.AutoSellAtOrBelow` `:144-150` → `Game1.cs:4265` → `ForgeScreen.AutoSellFloor`, applied `ForgeScreen.cs:1748-1753`; mirrored to the Vault at `Game1.cs:3171` | **Warren — SCAVENGER RUNS** | facility level ≥ 2 |
| `filter_uncommon` (3, req `filter_common`) | floor = Uncommon (hard ceiling — no rule may ever eat a Rare) | same | **Warren — SCAVENGER RUNS** | facility level ≥ 4 |
| `forge_insight` (2, req `ledger`) | **pure gate, no reader** | nothing | **deleted** | n/a |
| `auto_merge` (2, req `forge_insight`) | merge the haul on chest open | `DustEffects.AutoMergeAfterRuns` `:155-159` → `Game1.cs:4338` → `ForgeScreen.AutoMergeOnOpen`, fires `ForgeScreen.cs:1716`; Vault mirror `Game1.cs:3172` | **Warren — HOARD VAULTS** | facility level ≥ 2 |
| `efficient_forge` (3, req `forge_insight`) | dismantle return ×1.15 (0.40 → 0.46) | `DustEffects.DismantleRate` `:85-90` → `Game1.cs:4261` → `ForgeTuning.DismantleReturnRate` → `Forge.cs:225` | **deleted**, and `ForgeTuning.DismantleReturnRate` is rebased 0.40 → 0.46 so no save loses value | n/a |
| `recall_1..4` (1/2/2/3) | region-mastery rate +5% each, to +20% | `DustEffects.MasteryRate` `:68-75` → `Game1.cs:4379` → `RegionAutomation.RecordActiveKill(rate)` | **deleted**, uncompensated; `RecordActiveKill`'s `rate` parameter goes with it rather than becoming a dial nothing turns | n/a |
| `artifice_vows` (8) | sworn-vow bonus ×1.25 | `DustEffects.VowPowerMultiplier` `:226-230` → `TreeShape` → `BuildComposer.cs:116` → `SoloBattle.cs:2559` | **deleted.** `SkillShape.VowPowerMultiplier` keeps three other owners (MasteryCatalog `:178` 1.30, `:197` 1.60, THE OATHBOUND 1.50) so the dial is not orphaned | n/a |
| `attunement` (4) | one subtitle string on the TRAITS screen | `DustEffects.TreeComplete` → `TraitsScreen.cs:943` | **deleted** with the screen it decorated | n/a |
| 19 × `ks_*` (4–12) | learn one keystone each | `DustEffects.LearnedKeystones` `:55-63` → `BuildComposer.cs:129-132`, the whole BUILD chip strip | Region conquest / region mastery (sibling design) | per-keystone |
| 12 attribute minors | ×1.05…×1.12 on one axis each | `DustEffects.TreeMods` `:42-46` → `BuildComposer.cs:111` | **deleted** (§59: no clean semantic equivalent; a trait that reads "+5% damage" is the catalogue §34 forbids) | n/a |

**Why `Unlocks` is the right owner for sockets and slots.** `Unlocks` is already *derived, never stored* and already refuses non-monotone facts — the file carries a scar (`Unlocks.cs:116-122`) from the day `ChestsHeld` re-locked the Vault and re-announced it on every chest. `RegionsConquered` and `DeepestWave` only grow. Hunter level cannot carry this: `HunterProgression.cs:168` says outright "Cosmetic only — never a gate", and it is derived from Gleam spend, a different pacing curve entirely. So the brief's suggestion of "early Hunter Level" (§43/§48/§52) is declined in favour of the two axes that are already gates and already tested.

**Two consts, so a test can pin the numbers without copying them:** `Unlocks.SecondSocketConquests = 2`, `Unlocks.ThirdSocketConquests = 4`. The hard ceiling stays `Build.KeystoneSlots = 3`.

```csharp
/// <summary>How many keystone sockets the champion has. One to start; the world sells the other two.</summary>
public static int KeystoneSockets(UnlockFacts f)
{
    var sockets = 1;
    if (f.RegionsConquered >= SecondSocketConquests) sockets++;
    if (f.RegionsConquered >= ThirdSocketConquests) sockets++;
    return sockets;   // never above Build.KeystoneSlots, which is 3
}
```

**Pacing against today.** Socket 2 cost 2 trait points ≈ 2 conquests — unchanged. Socket 3 cost 10 cumulative points (`socket_2` 2 + `weave_5` 3 + `socket_3` 5) ≈ 5 conquests plus mastery, and now arrives at 4 conquests. Earlier, deliberately: the third socket was hanging off the fifth skill slot, which is a cross-system prerequisite (`MemoryDust.cs:318`) that has no meaning once the slot is gone, and §52 explicitly refuses to make a player choose between an interesting trait and a basic socket.

### 2. The fifth skill slot is removed, and it is the one thing this migration takes away

D8 is the single place the plan overrides §58, and §54 is what authorises it: *"the authoritative build remains 2 Active + 2 Passive. Do not retain a legacy fifth Skill slot."* The arithmetic makes it worse than a cosmetic leftover — `Build.ActiveSlotsFor(5) = (5+1)/2 = 3`, so `weave_5` buys a **third active**, and beat demand climbs back toward the number the slot rework existed to cut (`Build.cs:330-343` says so in its own comment).

**The change, in three edits:**
- `PlayerLoadout.SkillCapacity` setter clamps to `MaxSkills` (4). Today `MaxSkills` is a const nothing reads as a ceiling (`PlayerLoadout.cs:66`) — it becomes the ceiling again.
- `Build.SlotCapacity` setter clamps to `Build.SkillSlots` (4). Its getter keeps `Math.Max(_slotCapacity, _skills.Count)` so a build can never report fewer slots than it holds.
- `Game1.ApplySkillCapacity` loses the tree hand-over entirely: `_loadout.SkillCapacity = Math.Max(Unlocks.SkillSlots(GuideUnlockFacts()), _loadout.Skills.Count);`

**At load, the fifth woven skill is dropped, in slot order, and the player is told.** `_loadout.SkillCapacity = Math.Min(Build.SkillSlots, Math.Max(save.WovenSkills.Count, 1))` before `Restore`, which truncates the row list at `PlayerLoadout.cs:286`. The dropped row is the last one, and its bound vow (vows live on the slot, `SavedSkill.VowId`) goes with the slot. Nothing else is touched: `SkillProgress` is keyed by skill id, not by slot (`SaveGame.cs:161-166`), so **every level, variation and reinforcement that skill earned is still there** and it can be woven in place of another skill at any time.

Toast, plain English, one idea per sentence:

> **THE FIFTH SKILL SLOT IS GONE**
> Every build now holds four skills: two that take an action, and two that do not.
> DRAIN was taken out of your build. It keeps its level — put it back any time in place of another skill.

**One frozen thing to leave alone.** `LegacySkillForm.SlotKinds` runs on *every* load, not only legacy ones (`PlayerLoadout.cs:294`), and carries a frozen copy of `activeBudget = (capacity + 1) / 2` (`LegacySkillForm.cs:71`). It is handed the row count, so a five-row pre-v4 save still walks the old split when it decides which rows were passive — which is correct, because that is how those rows were *written*. The truncation happens after, in `PlayerLoadout.Restore`. Do not "fix" the frozen walk to agree with the new cap; that would re-read old rows under a rule that was not true when they were saved.

### 3. Auto-sell and auto-merge become Warren facility levels

LAW 13 and §56. Both are pure automation, both fire on chest open, and the Warren is the account's automation layer, with its own currency (Gleam + Dust), its own upgrade ladder, and its own depth cap so idle can never outrun the champion.

### The assignment

| Automation | Facility | Level | What the level costs (from `WarrenTuning`) | Depth the cap needs |
|---|---|---|---|---|
| Sell Common drops on sight | **SCAVENGER RUNS** | 2 | 270 Gleam + 28 Dust | 10 |
| Sell Uncommon drops too (hard ceiling — never Rare) | **SCAVENGER RUNS** | 4 | +405 +34, then +608 +43 → **1,283 Gleam + 105 Dust** cumulative | 20 |
| Merge spare items after a chest crack | **HOARD VAULTS** | 2 | 270 Gleam + 28 Dust | 10 |

### Why SCAVENGER RUNS carries auto-sell and HOARD VAULTS carries auto-merge, and not the other way round

The brief names both facilities (§56) and leaves the assignment open. The literal-name reading puts auto-sell on HOARD VAULTS ("Sort and store the salvage") — and that reading is wrong here, for a reason that only shows up in the ramp:

`Warren.UnlockedFacilityCount = min(8, 2 + ConqueredRegions)` walks `Facilities.All` **in authored order**: Nursery(0), ForagingPits(1), Tunnels(2), **ScavengerRuns(3)**, RitualNest(4), SentryBurrows(5), **HoardVaults(6)**, BreedingChamber(7).

- SCAVENGER RUNS opens at **2 conquests**.
- HOARD VAULTS opens at **5 conquests**.

Auto-sell is the anti-tedium feature; the bag drowns around wave 30, not around region six. Today it costs 3 trait points ≈ 3 conquests. Putting it on HOARD VAULTS would push it to 5 — a real pacing regression — and the only fix would be reordering the facility catalogue, which is the ramp, and reordering closes a level-1 facility for every save sitting between the two indices. Putting it on SCAVENGER RUNS costs **no reorder, no production change, no risk**, and lands it slightly *earlier* than today.

Auto-merge on HOARD VAULTS at 5 conquests is exactly today's pacing (5 trait points), and the copy is better than the alternative anyway: the vault keepers are the ones who stack matching pieces together.

The facility descriptions already carry the fantasy without a word changing: *"Send runners out to bring back Scrap"* — the runners pick over the haul and sell what is not worth carrying. *"Sort and store the salvage"* — the vault stacks matching pieces into better ones.

### Where the thresholds live, and who reads them

House rule: gameplay values are data. `WarrenTuning` already exists for exactly this ("Pure data, so balance lives in one place and tests pin it") and gains three fields:

```csharp
/// <summary>SCAVENGER RUNS at this level sells Common drops for you.</summary>
public int AutoSellCommonLevel { get; init; } = 2;
/// <summary>SCAVENGER RUNS at this level sells Uncommon drops too. Rare and better are always kept.</summary>
public int AutoSellUncommonLevel { get; init; } = 4;
/// <summary>HOARD VAULTS at this level merges your spare items when a chest is opened.</summary>
public int AutoMergeLevel { get; init; } = 2;
```

A new pure-Core static, `IdleXIdle.Core.Warrens.WarrenAutomation`, is the single translation layer — the same job `DustEffects` did, and it inherits `DustEffects`' liveness discipline: every method it exposes must have a real consumer, and a test asserts it.

```csharp
public static Rarity? AutoSellAtOrBelow(Warren w)   // null = keep everything
{
    var lvl = w.Facility(FacilityKind.ScavengerRuns).Level;
    if (lvl >= w.Tuning.AutoSellUncommonLevel) return Rarity.Uncommon;
    if (lvl >= w.Tuning.AutoSellCommonLevel)   return Rarity.Common;
    return null;
}

public static bool AutoMergeOnChestOpen(Warren w)
    => w.Facility(FacilityKind.HoardVaults).Level >= w.Tuning.AutoMergeLevel;
```

(`Warren` gains a `public WarrenTuning Tuning => _t;` accessor — it holds one already, privately.)

Call sites, replaced one for one, so nothing is left dangling:
- `Game1.cs:4265` `_forge.AutoSellFloor = DustEffects.AutoSellAtOrBelow(_dust)` → `WarrenAutomation.AutoSellAtOrBelow(_warren)`
- `Game1.cs:4338` `_forge.AutoMergeOnOpen = DustEffects.AutoMergeAfterRuns(_dust)` → `WarrenAutomation.AutoMergeOnChestOpen(_warren)`
- `Game1.cs:3171-3172` (the Vault mirror) reads `_forge.AutoSellFloor` / `_forge.AutoMergeOnOpen` and needs no edit.

**No new save field.** The facility levels are already persisted (`SaveGame.WarrenFacilities`, a `Dictionary<string,int>` keyed by `FacilityKind` NAME), already written in `SaveSystem.Capture` (`:555-557`), and already monotone in practice — `Facility.SetLevel`/`LevelUp` never lower a level. This is the whole reason the Warren is the right owner: it has the storage, the currency and the ladder already.

### One deliberate placement note

`WarrenTuning.MilestoneEvery = 5`, so SCAVENGER RUNS crosses its first *production* milestone at level 5. The automation levels (2 and 4) sit deliberately below it, so the ladder reads as two separate legible rewards rather than one lump: level 2 and level 4 change what the game does for you, level 5 changes how much it makes.

### Copy

The facility card already draws a "next level" line from `NextLevelOutput`. It gains an authored automation note, plain English, abbreviation-free:

- SCAVENGER RUNS → LEVEL 2: *"Your runners will sell Common items for you the moment a chest is opened."*
- SCAVENGER RUNS → LEVEL 4: *"Your runners will sell Uncommon items too. Rare and better are always kept."*
- HOARD VAULTS → LEVEL 2: *"The vault will stack your spare items into better ones after a chest is opened."*

And the two descriptions on the old nodes were **lies the migration must not inherit**: `filter_common` said items sell "the moment they drop" and `auto_merge` said the forge merges "after every expedition" (`MemoryDust.cs:345`, `:357`). Both actually fire only when a chest is opened (`ForgeScreen.cs:1748`, `:1716`). The copy above says chest-open, because that is what the code does.

### 4. The numerics that die, and the argument for each

§59 permits removing an old node outright when there is no clean semantic equivalent, and forbids manufacturing one-to-one baggage. Three rate nodes and twelve attribute minors fall under it. Each gets its own argument, because §58's "do not take unlocked functionality away" deserves a real answer rather than a citation.

**`efficient_forge` — salvage +15%. Deleted, and compensated for everybody.** It is the *only* modifier on `ForgeTuning.DismantleReturnRate`; deleting it silently retunes salvage back to a flat 0.40 for its owners. So the base rate is rebased **0.40 → 0.46** — exactly the value the node produced — and the dial disappears. Nobody loses value; everybody who never bought it gains a little. It stays a loss against selling at every tier, which is the design intent stated at `DustEffects.cs:79-83` ("if Dust ever pushed it past 1.0 it would become the strictly-correct action for every item in the game"), and it is nowhere near the 0.95 clamp. This is data, in `ForgeTuning`, where salvage balance already lives.

**`recall_1..4` — region-mastery rate +5…20%. Deleted, uncompensated.** These accelerated a currency that is being abolished: 18 of the 34 trait points a full career earned came from region mastery (`Career.cs:60`). With trait points gone, the ladder's remaining payouts are Dust milestones and region difficulty/loot tier — and the *totals* of both are unchanged, only the pace, by at most 17% on a ladder measured in hundreds of waves per region (5,000 mastery points at 5 per cleared wave = 1,000 waves for PERFECTED). Compensating by lifting `RmpPerActiveKillBonus` for everyone would speed up enemy scaling and loot tier globally, which is a far larger change than the one being avoided. `RegionAutomation.RecordActiveKill(float rate)` loses its parameter rather than keeping a dial nothing turns.

**`artifice_vows` — sworn-vow bonus ×1.25. Deleted, uncompensated.** The clean semantic equivalent already exists and is not going anywhere: `SkillShape.VowPowerMultiplier` is sold by two mastery nodes (`MasteryCatalog.cs:178` = 1.30, `:197` = 1.60) and by THE OATHBOUND's innate (1.50), all multiplicative. This node was the redundant fourth owner of one dial, it cost 8 points behind `ks_venomancer` (19 cumulative of 34 earnable), and vanishingly few saves hold it. `DustEffects.TreeShape` and its fold at `BuildComposer.cs:115-116` go; the dial itself keeps three live producers, so nothing is orphaned.

**The twelve attribute minors — deleted.** Their entire combined ceiling is ×1.05·1.06·1.12 ≈ **×1.246** on one axis, and a test already caps the set. §34 forbids the new trait catalogue from being made of "+5% damage", so there is nowhere honest for them to land. What repays it is the same migration's keystone move: a player deep enough to have bought minors is deep enough that conquest-and-mastery discovery hands them far more keystone vocabulary than 34 points ever bought, and a single keystone (GLASS CANNON is ×2.0 damage) is twenty times any minor.

**`ledger`, `forge_insight`, `attunement` — deleted, nothing to preserve.** The first two have no reader at all; they exist only because other nodes require them. `attunement` gated one subtitle string on a screen being replaced wholesale.

**Trait points themselves — abolished, and §60's "evaluate a conversion" is answered NO, because there is no balance to convert.** `Career.TraitPointsEarned(World)` is recomputed from world state every frame (`Game1.cs:4444`) and never persisted. The number a player "has" is a function, not a stock. Deleting it therefore deletes: `Career.TraitPointsEarned` (`Career.cs:56-62`), `UnlockFacts.TraitPointsEarned` (`Unlocks.cs:71`), the `Activity.Traits` gate that read it (`:159`), the host feed (`Game1.cs:3521`, `:6841`), the tour step that explained the three faucets (`Onboarding.cs:376-382`), the free-points hint (`:588-589`), and `MemoryDustTests.cs:233-249` — the test that asserts the 34-point budget, which is *deleted, not fixed*, because its subject no longer exists. §60 asks for the decision to be documented; this paragraph is it.

**`MemoryDustTree.Validate()` throws from a field initialiser** (`Game1.cs:447`) on a missing prerequisite or a cycle — i.e. before the window opens. Do not edit the catalogue down node by node; delete the class in one commit, with `DustEffects`, `TraitTreeLayout`, `TraitRoads` and `MemoryDustText`.

### 5. The Dust WALLET survives the tree's deletion

`MemoryDustTree` is two systems wearing one class name, and the audit is emphatic that only one of them is the trait tree. `MemoryDust.cs:121-139` says it outright: *"TRAIT POINTS. What this tree spends — and it is NOT Memory Dust."*

The wallet has five faucets — offline Warren yield (`Game1.cs:922`), live Warren yield (`:1266`), region-mastery milestones (`:3250`), conquest (`:4450`), corruption deepening (`:6542`) — and two sinks: Warren facility upgrades (`:1302`) and expedition checkpoints (`:4221`). Delete the class without splitting it and the Warren and the checkpoint system lose their currency.

**The split:** a new `sealed class DustWallet` in `IdleXIdle.Core.Prestige`, carrying exactly the three members that were never about the tree — `Balance`, `Add(int)`, `Spend(int) → bool`, plus `Restore(int)`. No catalogue, no `Validate()`, no ownership set, no `Earned`/`Spent`/`Available`.

`SaveGame.MemoryDust` (`:64`) is **unchanged in name, type, meaning and write path**; `SaveSystem.Capture` (`:572`) changes only its source expression, `prestige?.MemoryDust ?? 0` → `wallet?.Balance ?? 0`. Renaming that field would be the single most destructive thing this migration could do, because an unknown JSON member is silently skipped on read — every player's Dust would read zero ten seconds after launch.

The five screens that hold a `MemoryDustTree` purely to call `ToBuild` or to build a cache signature from `OwnedIds` (`LoadoutScreen`, `GearScreen`, `HuntScreen`, `TrainingScreen`, and `MasteryScreen.cs:336`, which assigns it and never reads it — dead coupling, confirmed by grep) drop the field. `PlayerLoadout.ToBuild(MemoryDustTree, …)` and `BuildComposer.Compose(MemoryDustTree, …)` take the discovered-keystone list and the known-vow set instead, and the cache signature keys on those lists plus `_keystoneIds`.

### 6. The migration: one frozen read, once, before the first save

This codebase has a house pattern for exactly this and it is used twice already: **a frozen read-once table** — `LegacyUnlocks` ("It must never change again; that is the point of it") and `LegacySkillForm` ("These six rows are frozen history"). The trait-tree migration is the third instance and must look like the other two.

### `LegacyTraitTree` — new, frozen on the day it ships

```csharp
/// <summary>What a pre-v4 save's trait-tree purchases are worth in the systems that replaced it.</summary>
/// <remarks>FROZEN, like LegacyUnlocks and LegacySkillForm. Read once, on load, and never written.</remarks>
public sealed record LegacyTraitGrants(
    int KeystoneSockets,                    // 1..3, from socket_2 / socket_3
    IReadOnlyList<string> Keystones,        // keystone ids, from the 19 ks_* nodes
    IReadOnlyList<string> Vows,             // vow ids, from the 5 vow_study_* nodes
    int ScavengerRunsLevel,                 // 1, 2 or 4 — from filter_common / filter_uncommon
    int HoardVaultsLevel,                   // 1 or 2 — from auto_merge
    bool HadTheTraitScreen);                // any node owned at all — see the gate note below

public static class LegacyTraitTree
{
    /// <summary>Read a pre-v4 MemoryDustUnlocks list. Unknown ids are ignored; an empty list grants nothing.</summary>
    public static LegacyTraitGrants Read(IEnumerable<string>? ownedNodeIds);
}
```

It owns its own frozen copies of the two tables it needs — the 19 `ks_* → keystone id` pairs and the 5 `vow_study_* → vow ids` rows — copied out of `MemoryDust.cs` and `DustEffects.VowGrants` on the day the originals are deleted, exactly as `LegacySkillForm` copied its Form table rather than calling into `SkillCatalogue`. That is what lets `MemoryDustTree` and `DustEffects` be deleted outright instead of living forever as migration scaffolding.

### Where it runs, and in what order

Inside `Game1.LoadOrStartFresh`, which today runs `SnapshotBeforeUpgrade` → `RestoreHunter` → `_dust.Restore` → capacities → `_loadout.Restore` → vow strip → `_skillProgress` → mastery (`:767-810`), then `RestoreWarren` (`:822`) and `RestoreWorld` (`:897`).

The load-order constraint the existing code already documents at `Game1.cs:773-783` applies to the sockets too, and it is the trap: **the unlock facts are all zero at this point in the load** — conquest count is not restored until `:897`. Asking `Unlocks.KeystoneSockets` here would see a player with no conquests, return 1, and `PlayerLoadout.Restore` would truncate every existing player's worn keystones to one. They would then never see the others again, because the save written back would agree. The existing code solves the identical problem for skill slots by flooring on the save's own count; sockets use the same floor.

```
1.  SnapshotBeforeUpgrade(save.Version)          // NOW FIRES on every v3 file — this is the undo
2.  SaveSystem.RestoreHunter(save, _hunter)
3.  _wallet.Restore(save.MemoryDust)             // the wallet, unchanged
4.  var legacy = LegacyTraitTree.Read(save.MemoryDustUnlocks);   // ONCE. [] on a v4 file → all-empty
5.  _keystones.Restore(save.DiscoveredKeystones.Union(legacy.Keystones))
    _vows.Restore(save.DiscoveredVows.Union(legacy.Vows))
6.  _socketsEarned = Max(save.KeystoneSocketsEarned,
                        legacy.KeystoneSockets,
                        save.SocketedKeystoneIds.Count)      // the save's OWN count is the floor
    _loadout.KeystoneCapacity = _socketsEarned
7.  _loadout.SkillCapacity = Min(4, Max(save.WovenSkills.Count, 1))
8.  _loadout.Restore(...)                        // truncates a 5th row here; toast if count > 4
9.  vow strip against the NEW known-vow set (Game1.cs:800-802, unchanged in shape)
10. _skillProgress.Restore(...) ; _mastery.RestoreTaken/... (untouched by this section)
11. SaveSystem.RestoreWarren(save, _warren, legacy)   // facility floors applied here (see below)
12. SaveSystem.RestoreWorld(save, _world)        // conquest is real from here on
13. per-frame from now on: _socketsEarned = Max(_socketsEarned, Unlocks.KeystoneSockets(facts))
```

`RestoreWarren` gains one optional parameter and raises two floors after the dictionary is applied:

```csharp
public static void RestoreWarren(SaveGame save, Warren warren, LegacyTraitGrants? legacy = null)
{
    ... existing Enum.TryParse walk ...
    if (legacy is { } g)
    {
        levels[FacilityKind.ScavengerRuns] = Math.Max(levels.GetValueOrDefault(FacilityKind.ScavengerRuns, 1), g.ScavengerRunsLevel);
        levels[FacilityKind.HoardVaults]   = Math.Max(levels.GetValueOrDefault(FacilityKind.HoardVaults,   1), g.HoardVaultsLevel);
    }
    warren.Restore(save.WarrenLevel, save.WarrenXp, levels);
}
```

Three things this gets right by construction: `Warren.Restore` calls `Facility.SetLevel`, which **mints no XP** (`Upgrade` does, `Restore` does not) — so the migration cannot level the Warren sideways. `Warren.IsUnlocked` has a grandfather rule ("a facility past level 1 stays open under any ramp") — so a migrated SCAVENGER RUNS at level 2 is *open* even for a player with one conquest, which is precisely what §58 demands. And `FacilityLevelCap` is depth-derived and only gates further `Upgrade` calls, never `SetLevel` — a migrated level above the cap simply cannot be raised until depth catches up. It is never lowered.

### The `Activity.Traits` gate, and the one non-monotone hazard in the whole migration

D7 makes the screen open on the first discovered trait — monotone, because discovery is account-wide and permanent. `UnlockFacts.TraitPointsEarned` is replaced by `TraitsDiscovered`.

```csharp
Activity.Traits => f.TraitsDiscovered >= 1,
Activity.Traits => "Discover your first trait",          // Requirement
Activity.Traits => "TRAITS — WHAT YOU HAVE LIVED THROUGH", // Headline: the old one said
                                                          // "PERMANENT BONUSES THAT NEVER RESET",
                                                          // which is no longer what they are
```

**The hazard:** a returning player had the TRAITS screen open (they had at least one trait point). The new trait rules read lifetime counters that do not exist retroactively, so on the first v4 load they have **zero** discovered traits and the screen **re-locks**. That is a gate going backwards across the migration — the exact bug class `Unlocks.cs:116-122` was written to prevent, and it also violates §58.

**The fix, and it lives in this section because it is a migration fact:** `LegacyTraitGrants.HadTheTraitScreen` is true when the save owned any node at all, and the migration then seeds the account's discovered-trait list with the catalogue's designated first trait — the same role the tutorial vow plays for vows. One trait, granted once, with the ordinary reveal:

> **A TRAIT HAS AWAKENED**
> SCAR TISSUE
> *"What you have already lived through was enough."*

The screen stays open, the player has something to equip on arrival, and `ExplainedScreens` (which stores `Activity` NAMES) is untouched — **do not rename the `Activity.Traits` enum member**, or `Onboarding.SeedExplained` and `IsNew` will re-tour every returning player. Which trait it is belongs to the trait catalogue design; this section only fixes the slot it must fill.

### Idempotence is the load-bearing property

After the first v4 save, `MemoryDustUnlocks` serialises as `[]`, so `LegacyTraitTree.Read` returns an all-empty grant and every step above becomes a `Max` against zero or a `Union` with nothing. The migration is safe to run on every load forever, which is what stops it from being a one-shot with a failure mode nobody can test twice. **This gets its own test.**

### 7. The migration table — all 51 node ids, and what each becomes

Read once from `save.MemoryDustUnlocks`. An id not in this table is ignored (a forward-compat guard, and the behaviour `MemoryDustTree.Restore` already had at `MemoryDust.cs:200`). Nothing here consults trait points, because there are none.

| # | Old node id | What it bought | New state after migration | Where that state now lives |
|---|---|---|---|---|
| 1 | `socket_2` | 2nd keystone socket | `KeystoneSocketsEarned = max(…, 2)` | `SaveGame.KeystoneSocketsEarned` (new) |
| 2 | `socket_3` | 3rd keystone socket | `KeystoneSocketsEarned = max(…, 3)` | ″ |
| 3 | `weave_5` | 5th skill slot | **nothing.** Capacity truncates to 4; the 5th woven row is unwoven and toasted; its levels are kept | — (deliberate removal, D8/§54) |
| 4 | `vow_study_1` | learn 2 vows | `DiscoveredVows ∪ { vow_complete, vow_deliberate }` | `SaveGame.DiscoveredVows` (new) |
| 5 | `vow_study_2` | learn 2 vows | `∪ { vow_pure, vow_frantic }` | ″ |
| 6 | `vow_study_3` | learn 2 vows | `∪ { vow_singular, vow_bluntedge }` | ″ |
| 7 | `vow_binding` | learn 3 vows | `∪ { vow_barefoot, vow_openhand, vow_bareskull }` | ″ |
| 8 | `vow_sacrifice` | learn 4 vows | `∪ { vow_fragility, vow_reckless_offering, vow_unguarded, vow_unbound }` | ″ |
| 9 | `ledger` | nothing (pure gate) | nothing | — |
| 10 | `filter_common` | auto-sell Common | `WarrenFacilities[ScavengerRuns] = max(…, 2)` | `SaveGame.WarrenFacilities` (existing) |
| 11 | `filter_uncommon` | auto-sell Uncommon | `WarrenFacilities[ScavengerRuns] = max(…, 4)` | ″ |
| 12 | `forge_insight` | nothing (pure gate) | nothing | — |
| 13 | `efficient_forge` | salvage ×1.15 | nothing — the multiplier is folded into `ForgeTuning.DismantleReturnRate` (0.40 → 0.46) for every save | `ForgeTuning` (data) |
| 14 | `auto_merge` | auto-merge on chest open | `WarrenFacilities[HoardVaults] = max(…, 2)` | `SaveGame.WarrenFacilities` (existing) |
| 15 | `recall_1` | region mastery +5% | nothing (deleted, uncompensated) | — |
| 16 | `recall_2` | +5% | nothing | — |
| 17 | `recall_3` | +5% | nothing | — |
| 18 | `recall_4` | +5% | nothing | — |
| 19 | `attunement` | one subtitle string | nothing | — |
| 20 | `ks_glass_cannon` | learn GLASS CANNON | `DiscoveredKeystones ∪ { glass_cannon }` | `SaveGame.DiscoveredKeystones` (new) |
| 21 | `ks_bloodlust` | learn BLOODLUST | `∪ { bloodlust }` | ″ |
| 22 | `ks_blood_magic` | learn BLOOD MAGIC | `∪ { blood_magic }` | ″ |
| 23 | `ks_reaper` | learn REAPER | `∪ { reaper }` | ″ |
| 24 | `ks_rend` | learn REND | `∪ { rend }` | ″ |
| 25 | `ks_ironclad` | learn IRONCLAD | `∪ { ironclad }` | ″ |
| 26 | `ks_juggernaut` | learn JUGGERNAUT | `∪ { juggernaut }` | ″ |
| 27 | `ks_undying` | learn UNDYING | `∪ { undying }` | ″ |
| 28 | `ks_titan` | learn TITAN | `∪ { titan }` | ″ |
| 29 | `ks_dynamo` | learn DYNAMO | `∪ { dynamo }` | ″ |
| 30 | `ks_greed` | learn GREED | `∪ { greed }` | ″ |
| 31 | `ks_discerning_eye` | learn DISCERNING EYE | `∪ { discerning_eye }` | ″ |
| 32 | `ks_fortune` | learn FORTUNE | `∪ { fortune }` | ″ |
| 33 | `ks_hoarder` | learn HOARDER | `∪ { hoarder }` | ″ |
| 34 | `ks_lodestone` | learn LODESTONE | `∪ { lodestone }` | ″ |
| 35 | `ks_echo` | learn ECHO | `∪ { echo }` | ″ |
| 36 | `ks_venomancer` | learn VENOMANCER | `∪ { venomancer }` | ″ |
| 37 | `ks_capacitor` | learn CAPACITOR | `∪ { capacitor }` | ″ |
| 38 | `ks_weaver` | learn WEAVER | `∪ { weaver }` | ″ |
| 39 | `artifice_vows` | vow bonus ×1.25 | nothing — mastery and THE OATHBOUND still sell the dial | — |
| 40 | `ruin_edge_1` | Damage ×1.05 | nothing | — |
| 41 | `ruin_edge_2` | Damage ×1.06 | nothing | — |
| 42 | `ruin_edge_3` | Damage ×1.12 | nothing | — |
| 43 | `aegis_skin_1` | Health ×1.05 | nothing | — |
| 44 | `aegis_skin_2` | Health ×1.06 | nothing | — |
| 45 | `aegis_skin_3` | Health ×1.12 | nothing | — |
| 46 | `avarice_purse_1` | Haul ×1.05 | nothing | — |
| 47 | `avarice_purse_2` | Rarity ×1.06 | nothing | — |
| 48 | `avarice_purse_3` | Haul ×1.08, Rarity ×1.05 | nothing | — |
| 49 | `artifice_hands_1` | SkillRate ×1.05 | nothing | — |
| 50 | `artifice_hands_2` | SkillRate ×1.06 | nothing | — |
| 51 | `artifice_hands_3` | SkillRate ×1.12 | nothing | — |
| — | **any node owned at all** | — | `HadTheTraitScreen = true` → seed one discovered trait so the TRAITS screen does not re-lock | trait catalogue's discovered list |

**Sanity checks the migration must satisfy, from §97:** an already-unlocked vow stays unlocked (rows 4–8); an already-available keystone stays discovered (rows 20–38); an already-earned socket capacity is preserved (rows 1–2); a worn keystone is never truncated away, because `SocketedKeystoneIds.Count` is a floor on the capacity at load. **Row 3 is the only entitlement removed anywhere in this table, and it is removed loudly.**

### 8. The save surface: every field added, stopped, and kept

### Added

| Field | Type / default | Meaning | "Empty" means | Written where |
|---|---|---|---|---|
| `DiscoveredKeystones` | `List<string>` = empty | Account-wide keystone knowledge. A **stored latch**, unioned each frame with what world progression derives | brand-new player, or a pre-v4 save whose grants the migration writes on the same load, before the first save | `Game1.Save()`'s `with` block |
| `DiscoveredVows` | `List<string>` = empty | Account-wide vow knowledge, same shape | ″ | ″ |
| `KeystoneSocketsEarned` | `int` = 0 | The socket capacity high-water mark: `max(stored, Unlocks.KeystoneSockets(facts), legacy, worn count)` | 0 = never computed; the load-time max always produces ≥1 | ″ |

All three are **additive with a default that means what an old save meant** — the codebase's only migration pattern (eleven live instances, listed in the audit). All three are **monotone by construction**: a `Union` and a `Max` cannot go backwards, which is what the `Unlocks` layer requires of any new gate fact and what stops a reveal from re-firing.

Why `KeystoneSocketsEarned` is stored at all, given `Unlocks` is otherwise "derived, never stored": a player can hold `socket_2` on **one** conquest today (2 trait points is reachable as 1 conquest + 1 region-mastery level), and the new rule wants two. Pure derivation would take their second socket away. It is a capacity latch of exactly the same species as `HighestMasteryAwarded`, not an Activity gate, and the tension is named here rather than hidden.

The trait catalogue design adds its own fields (the discovered-trait list, and the per-character three-slot loadout, which is the one piece of genuinely per-character save state this refactor introduces). This section only seeds them.

### Stopped

| Field | What happens |
|---|---|
| `MemoryDustUnlocks` (`SaveGame.cs:65`) | **Stays declared. `SaveSystem.Capture` stops populating it** (`:573` drops), so from the first v4 write it serialises as `[]`. It is not removed from the record, because removing it makes the migration impossible to run against an old file. Its doc-comment joins `RegionMasteryPoints` and `ChestKeepSlot` in the house's "WRITTEN NO MORE — read only to migrate" club. `LegacyTraitTree.Read` is its only remaining reader |
| `LearnedSkills` (`:177`) | Owned by the mastery-access design (D4), listed here for completeness of the save surface. See the note under **Open question 7** — the version bump changes its stated rationale |

**Nothing is removed from the record.** `System.Text.Json` skips unknown members on read, so a deleted field is unrecoverable the instant the first autosave lands; the house rule is "never delete a field, stop writing it", and this migration follows it.

### Kept, and load-bearing

`MemoryDust` (`:64`) — the **wallet**, not trait points, and the field whose loss would silently zero every player's checkpoint and Warren currency. `SocketedKeystoneIds` (`:156`) — unchanged, and now doubles as the floor on socket capacity at load. `WarrenFacilities` (`:281`) — unchanged in shape, and now carries the auto-sell/auto-merge entitlement. `SavedSkill.VowId` (`:327`) — unchanged; vows stay bound per skill slot.

### The version bump is a data-safety requirement, not bookkeeping

`SaveGame.CurrentVersion` **3 → 4**, with the history comment extended:

```csharp
// 4 = the trait tree is deleted (2026-09-02). MemoryDustUnlocks stops carrying data; keystone
// knowledge, vow knowledge, keystone-socket capacity and the Warren's two automation levels are
// read out of it ONCE, on the first v4 load, and written to their new homes. The bump is what makes
// SaveStore.SnapshotBeforeUpgrade copy the pre-v4 file aside — its guard is
// `if (fileVersion >= CurrentVersion) return null;`, so WITHOUT the bump no snapshot is taken and
// the ten-second autosave destroys the tree state unrecoverably.
```

That guard (`SaveStore.cs:176`) is the whole argument. Autosave fires every ten seconds (`Game1.cs:655`, `:2792-2796`). The moment a build that stopped writing `MemoryDustUnlocks` saves, the field is gone from disk. The snapshot is the only undo, and the snapshot only happens on a version bump.

**Tests that break on the bump, deliberately:** `skill_identity_migration_test.cs:166 test_the_current_save_version_is_three` → renamed and re-pinned to four. `save_armour_test.cs`'s `SnapshotBeforeUpgrade` trio now exercises a v3→v4 file.

### How a save written by the OLD build still loads

1. The file carries `Version: 3`. `SaveSystem.Deserialize` refuses only `save.Version > CurrentVersion` (`:520`), and 3 ≤ 4, so it loads.
2. `SaveFile.SnapshotBeforeUpgrade(3)` fires (3 < 4) and writes `save.pre-v4-<time>.json` beside it, **once**. This is the recovery file if the migration is wrong.
3. Unknown members from the old file — the retired creature fields, `WarrenMasteryPool`, `Form` on a woven skill — are skipped as they always were.
4. New members are absent, so `DiscoveredKeystones`/`DiscoveredVows` default to empty and `KeystoneSocketsEarned` to 0 — each default meaning "this save predates the field", which is exactly what the migration then fills in.
5. `LegacyTraitTree.Read(save.MemoryDustUnlocks)` runs at step 4 of the load order, **before any `Save()` call**, and every entitlement in the 51-row table above is translated.
6. `RestoreWarren` raises the two facility floors; `Warren.IsUnlocked`'s grandfather rule opens SCAVENGER RUNS even at one conquest.
7. `PlayerLoadout.Restore` truncates a five-row build to four and the toast fires. Nothing else in the build changes: `SkillProgress` is keyed by skill id, `MasteryTaken` is untouched, worn gear and inventory are untouched.
8. The first autosave, ten seconds later, writes a v4 file with `MemoryDustUnlocks: []` and the new fields populated. The pre-v4 snapshot still sits on disk.

**And in the other direction, which is why the bump has teeth:** a v4 file read by a v3 build returns `LoadFailure.FromNewerVersion`, and `SaveStore.LocksSaving` (`:47-48`) then latches saving OFF for that session. This is correct — a rollback that silently loaded a v4 file would find no trait tree and no new fields and would write both away — but it means **the migration has one attempt against any given file**. That is the reason for the pre-v4 snapshot, for the idempotence test, and for running `tools/check_boot.sh` (the only test that exercises the real load path against a real binary, on a real save, twice) before this ships.

### 9. Player-facing copy that must change

§93 makes stale strings part of the work. Every string below currently tells the player that Traits unlock something Traits no longer own.

| Where | Today | Becomes |
|---|---|---|
| `LoadoutScreen.cs:848` | "ONLY {n} SOCKET(S) — MORE ON THE TRAITS SCREEN." | "ONLY {n} SOCKET{S}. YOU EARN THE NEXT ONE BY CONQUERING {N} REGIONS." |
| `LoadoutScreen.cs:1103` | "LEARN THEM ON THE TRAITS SCREEN" | "KEYSTONES ARE FOUND BY CONQUERING REGIONS." |
| `LoadoutScreen.cs:1578` | "NO VOW ON THIS SLOT — VOWS ARE LEARNED ON THE TRAITS SCREEN" | "NO VOW ON THIS SLOT. YOU FIND A VOW BY KEEPING ITS RULE ONCE WITHOUT IT." |
| `Onboarding.cs:286-288` | "Keystones are rules learned on the TRAITS screen." | "Keystones are rules the world gives you for conquering it." |
| `Onboarding.cs:376-382` | the three-card TRAITS tour, incl. "TRAIT POINTS … You earn one for each region you conquer…" | rewritten by the trait design; `TourTarget.TraitPoints` is deleted |
| `Onboarding.cs:588-589` | "YOU HAVE {n} TRAIT POINT{S}" hint | deleted; `HintFacts.TraitPointsFree` with it |
| `Unlocks.cs:159` / `:191` / `:213` | `TraitPointsEarned >= 1` / "Earn a trait point" / "TRAITS — PERMANENT BONUSES THAT NEVER RESET" | `TraitsDiscovered >= 1` / "Discover your first trait" / "TRAITS — WHAT YOU HAVE LIVED THROUGH" |
| `Unlocks.cs:184` | "Earn a chest from a boss" — names one of the gate's two clauses | "Earn a chest, or find two items" |
| `Vows.cs:315` | `vow_unbound`: "…YOU GIVE UP THE TRAIT TREE'S PRIZE." | "…YOU GIVE UP EVERY KEYSTONE." |
| `VaultScreen.cs:823`, the Forge's auto-sell/auto-merge captions | "Memory Dust traits" wording | name the facility and level: "YOUR SCAVENGER RUNS SELL COMMON DROPS", "YOUR HOARD VAULTS MERGE SPARES" |
| `Build.cs:246`, `:254-258`, `:417-435`; `PlayerLoadout.cs:66-67` | doc-comments naming `weave_5`, claiming the tree "is COMPLETABLE by design" and "teaches fifteen" keystones | rewritten. All three are already false: the tree costs 226 points against 34 earnable, and there are 19 keystones |

All new copy follows the house rules: plain English, no abbreviations, effects named as the game names them, one idea per sentence.

### 10. Tests, fixtures, and the things that will fail loudly

### New tests (Core, so they run without a host)

| Test | What it pins |
|---|---|
| `test_a_v3_save_with_filter_uncommon_still_sells_uncommon` | hand-written v3 JSON → load → `WarrenAutomation.AutoSellAtOrBelow` returns `Uncommon` |
| `test_a_v3_save_with_auto_merge_still_merges` | ″ for HOARD VAULTS level 2 |
| `test_a_v3_save_with_socket_3_and_one_conquest_keeps_three_sockets` | the §58 case the derived rule alone would break |
| `test_a_worn_keystone_is_never_truncated_away_on_load` | `SocketedKeystoneIds.Count` as the load-time floor |
| `test_a_v3_save_with_five_woven_skills_loads_four_and_keeps_every_level` | the one removal, and that `SkillProgress` is untouched |
| `test_the_trait_migration_is_idempotent` | load → save → load again: no double-raise, no second grant, no re-toast. **The load-bearing one** |
| `test_a_fresh_save_grants_nothing` | empty `MemoryDustUnlocks` → all-empty grants |
| `test_an_unknown_node_id_is_ignored_and_does_not_throw` | forward-compat, and the never-throw guarantee `save_armour_test.cs` exists for |
| `test_the_dust_wallet_survives_the_tree` | `SaveGame.MemoryDust` round-trips with no `MemoryDustTree` in the process |
| `test_keystone_sockets_only_ever_grow` | the monotone invariant, including against a conquest count that falls (a renamed region id can do it — the Warren's depth cap already carries a comment about exactly that) |
| `test_the_current_save_version_is_four` | replaces `…_is_three` |
| `test_scavenger_runs_level_two_actually_sells_a_common_at_chest_open` | **liveness** — the wire, through a real `LandChest`, not the number |
| `test_hoard_vaults_level_two_actually_merges_after_open_all` | ″ |
| `test_every_warren_automation_threshold_has_a_consumer` | the replacement for `DustEffectsTests`' two liveness tests, which die with `DustEffects` |

### Tests that are deleted rather than fixed

`MemoryDustTests.cs` (11 tests — the 34-point budget, the two-terminals invariant), `DustEffectsTests.cs` (24), `trait_gates_test.cs` (3), `TraitNamesTests.cs` (8), `TraitDescriptionsTests.cs` (8), `MemoryDustTextTests.cs` (8), `career_test.cs`'s trait-point arithmetic, `traits_feedback_test.cs` (13, Game-side motion tests for the tree screen). Their subjects no longer exist; "fixing" them would keep the deleted design alive in the suite.

### Fixtures — because a state no capture mode can pose has never been looked at

Roughly forty call sites hard-code trait node ids to pose a state: ~12 blocks in `Game1` (`:1776`, `:1981`, `:1992-1993`, `:2455-2458`, `:2511`, `:2654-2655`, `:2682`), `TraitsScreen.DevPoseLit` (`:356-357`), and a dozen Core tests. Every one needs a replacement or the corresponding UI state becomes unphotographable:

- **Warren automation** — `_warren.Restore(level, xp, new() { [ScavengerRuns] = 4, [HoardVaults] = 2 })`. `Warren.Restore` is already public and already used as a fixture at `Game1.cs:2356`.
- **Sockets** — set `KeystoneSocketsEarned` on the posed save, or push `_loadout.KeystoneCapacity` directly.
- **Keystone / vow knowledge** — push ids into the discovered lists.

### Things that fail loudly, by design

`tools/check_nav_gates.py` (Nav[]/NavActivity[] parallel) is unaffected because `Activity.Traits` keeps its name. `unlocked_characters_test.cs:150` is unaffected because the roster does not change here. `legacy_full_save_migration_test.cs` — the end-to-end hand-written legacy fixture — gains a v3 case carrying a populated `MemoryDustUnlocks`, and is the closest thing this repo has to an integration test for the whole path. And `tools/check_boot.sh` is mandatory before merge: the load path is unreachable by the screenshot rig (`Game1.cs:823-830` documents two shipped boot crashes that happened *before the window opened, on the second launch only*), and this change edits the load path in eight places.

**Open questions this design leaves to the implementer**

- SOCKET MILESTONES vs KEYSTONE DISCOVERY. Sockets 2 and 3 are set at 2 and 4 conquests. The keystone-acquisition design owns which keystone arrives on which conquest, and the menu must stay longer than the plate (DustEffects' own stated invariant). If that design front-loads discovery onto the first two conquests, sockets at 2/4 is right; if it spreads discovery across all six, sockets should move later. Options: (2, 4) as designed / (2, 5) / third socket on all six conquered. Whichever lands, the two consts Unlocks.SecondSocketConquests and Unlocks.ThirdSocketConquests are the only place the number appears.
- AUTO-SELL'S FACILITY. SCAVENGER RUNS is chosen over the more literally-named HOARD VAULTS purely because of the unlock ramp: index 3 opens at two conquests, index 6 at five, and auto-sell is a wave-30 need. The alternative is to keep the literal naming and reorder Facilities.All so HOARD VAULTS moves to index 3 — but the catalogue order IS the ramp, and swapping two entries closes a level-1 facility for every save sitting between the two indices (the grandfather rule only protects facilities already past level 1). Options: SCAVENGER RUNS with no reorder (designed) / HOARD VAULTS with a swap of the two Scrap facilities, accepting a −1 Scrap/min blip for saves at exactly 2–4 conquests.
- efficient_forge's COMPENSATION. Designed as: delete the node, rebase ForgeTuning.DismantleReturnRate 0.40 → 0.46 so no save loses value. That gives the +15% to every player who never bought it, which is a real if small economy change. Options: rebase for everyone (designed) / delete uncompensated (a 13% salvage cut for its owners) / hang it on a HOARD VAULTS level as a third automation perk (keeps the entitlement, but puts a Forge rate inside the Warren, which muddies LAW 13).
- recall_1..4 AND artifice_vows. Both deleted uncompensated, on the argument that recall accelerated a currency being abolished and artifice_vows was the redundant fourth owner of a dial mastery and THE OATHBOUND still sell. Neither loses a capability, only a rate. If the project would rather take nothing at all from anybody, the alternatives are: fold +20% region-mastery rate into RmpPerActiveKillBonus for everyone (which speeds enemy scaling and loot tier globally — a bigger change than the one avoided), and/or add a fourth VowPowerMultiplier source somewhere. My recommendation is to take neither.
- KeystoneSocketsEarned AS A STORED LATCH. It is stored so a save holding socket_2 on one conquest does not lose its second socket. That puts one capacity number in the save while the rest of the Unlocks layer is proudly derived-never-stored. Options: store the latch (designed — one field, monotone by construction, same species as HighestMasteryAwarded) / derive from conquest only and floor on SocketedKeystoneIds.Count (no new field, but a player who bought socket_3 and wore only two keystones silently loses the third) / carry a frozen LegacyKeystoneSockets grant list instead (baggage forever, which §59 warns against).
- THE SEEDED FIRST TRAIT. To stop the TRAITS screen re-locking for a returning player — a gate going backwards, which the Unlocks layer forbids — the migration grants one discovered trait to any save that owned any node. Which trait is the trait catalogue's call; SCAR TISSUE is used above only as a placeholder. Options: seed one designated entry trait (designed) / seed nothing and let the screen re-lock until the new counters fire (violates §58 and re-fires a reveal) / open the screen on a second fact such as RegionsConquered >= 1 (keeps it open but shows a player an empty collection, which is the 'lying screen' shape the Warren gate exists to avoid).
- D4's RATIONALE FOR STILL WRITING LearnedSkills. The mastery-access design keeps SaveGame.LearnedSkills written for one more version 'so a rollback still loads'. With CurrentVersion bumped to 4, a v3 build refuses a v4 file outright (FromNewerVersion, and SaveStore latches saving off), so a rollback cannot load it whatever the field says. Either the field should stop being written at v4 along with MemoryDustUnlocks, or the rationale should be restated as something the bump does not void. Flagging rather than overruling — it is that design's field, not this one's.
- ECHOING MemoryDustUnlocks FOR ONE VERSION. The design stops populating it immediately, on the grounds that the pre-v4 snapshot is the real undo. Writing it back verbatim for one version costs a few hundred bytes and buys a second chance if a migration bug is found within a session. Options: stop writing (designed) / echo verbatim for v4 only, stop at v5.
- WHICH SKILL THE FIFTH SLOT LOSES. The design drops the last row in slot order and names it in the toast. Options: drop the last row (designed — deterministic, testable, and the row order is the player's own) / hold the build at five until the player is shown a chooser on next opening the BUILD screen (kinder, but it means a build that is temporarily over capacity, which every truncation path in PlayerLoadout is written to prevent).
- SHARE CODES CARRYING UNDISCOVERED CONTENT. ShareCodes.SharedBuild carries skills, keystones and mastery, with its own version (2) independent of the save's. A code naming a keystone or vow the recipient has not discovered composes as nothing today (BuildComposer.cs:129, :148) — silently. Options: keep the silent drop / show 'YOU HAVE NOT FOUND THIS YET' on the inspect card so a shared build reads as a goal rather than as a bug. Out of scope here, but the discovery move is what makes it newly visible.

## VOW DISCOVERY — proof before reward, and what "vow capacity" should become

### 1. What the code actually is (the four premises this design stands on)

Read before designing: `BRIEF.md` §42–48 and LAW 10, `AUDIT.md`, `PLAN.md`, `Vows.cs`, `SkillCatalogue.cs`, `SoloBattle.cs`, `SkillShape.cs`, `CharacterRoster.cs`, `WaveModel.cs`, plus `Build.cs`, `PlayerLoadout.cs`, `Career.cs`, `Descent.cs`, `SoloExpedition.cs`, `RunReport.cs`, `Unlocks.cs`, `MasteryCatalog.cs`, `Forge.cs`, `ItemAffixes.cs`, `HunterProgression.cs`.

**P1 — `Vows.IsActive(vow, ctx)` already answers the discovery question, and it does not care whether the vow is worn.** `Vows.cs:363-384` tests a demand against a `BuildContext`; `SoloBattle.DescribeBuild` (`SoloBattle.cs:2563-2596`) builds that context out of the build and the hunter. Nothing in `IsActive` reads whether the vow is sworn. So *"did this build obey the restriction without the vow?"* is one existing call plus one `!sworn` test. **The discovery machine for eleven of the thirteen vows already exists.**

**P2 — `Career.VowWasKept` is the precedent, and it names the one frame this can be asked.** `Career.cs:85-91` composes the build, calls `DescribeBuild`, and asks `IsActive`. Its caller is `Game1.cs:4439`, inside the `_expedition.LogDirty` block, whose own comment reads: *"A RUN JUST ENDED. This is the only frame on which 'was a Vow kept for that descent?' can be answered."* Vow discovery belongs in that same block, four lines below the line that already does 90% of it.

**P3 — a demand is a property of the BUILD, static for a descent, and that is deliberate.** `Vows.cs:47-63`: demands used to watch the fight and were moved to the workbench precisely so a vow is *"known before the descent starts, visible on the workbench, holds for every wave equally, and the post-run report can state it as a fact."* A discovery rule written against `WaveMetrics` (shield absorbed, hits, kills) would drag vows back to the model that was deleted. **Vow discovery reads the BUILD; trait discovery reads `WaveMetrics`. That is the line between the two systems, and it should be the reason they never merge.**

**P4 — without a guard, ten of the eleven demand vows unlock on the player's first descent.** A brand-new hunter has 1 slot, 1 skill, Body source, no crit training, no defence, no keystones, no gear. That satisfies `SingleStyle`, `SingleSource`, `EverySlotFilled`, `NoCritInvestment`, `CadenceAtOrBelow 1.0`, `NoDefence`, `NoKeystone`, `SlotLeftBare` ×3 — ten of eleven, on wave one. Only `CadenceAtOrAbove 1.4` fails. So the design's whole weight is on the two guards below; the `IsActive` half is free.

### 2. The rule, stated once

> **A vow reveals itself when a descent ends in which its restriction held, for enough waves, on a build that had something to break it with — and the vow was not sworn.**

Four clauses, one function, one call site.

| Clause | What it is | Where it is read |
|---|---|---|
| **Unsworn** | the vow is on no slot of the build that ran | `build.Skills.All(s => s.Vow?.Id != vow.Id)` — or `!build.Vows.Contains(v)` after §7 |
| **Held** | `Vows.IsActive(vow, ctx)` for a demand vow; a *conduct* predicate for the two static-cost vows | `Vows.cs:363-384`, `SoloBattle.cs:2563` |
| **Proof waves** | it held for N **cleared** waves of *this* descent | one new per-descent counter (§5) |
| **Temptation** | the account owned the thing the restriction refuses | account facts, read once at run end (§4) |

**Temptation is the clause that makes this a restriction rather than a starting condition**, and it is what §44's own sentence ("the player must obey the restriction BEFORE receiving its reward") requires but does not say: you cannot obey a rule you were never able to break. Without it, P4 happens. Without proof waves, the reveal fires on wave one and the ceremony is a lie.

**Proof waves are not depth.** `RunReport.Depth` is `SoloExpedition.Wave` (`SoloExpedition.cs:186`), and `StartAtWave` sets it from a checkpoint (`Descent.cs:128-132`) — so a player could buy a checkpoint to wave 40, die on 41, and claim a 20-wave proof having cleared nothing. Counting cleared waves closes that by construction.

**Nothing here is RNG (§47, §30).** Every clause is a build property or an owned-item test. There is no drop, no proc, no missable event, and no one-time window: every vow can be proved again at any point in the career, in any region, for as long as the game runs.

### 3. The metadata the catalogue gains

Four values per vow, authored beside `Severity` in `Vows.Catalog`. Data, not constants in a loop.

```csharp
public enum VowProof { Granted, Demand, Conduct }

public enum VowTemptation
{
    None,                    // FRANTIC — cannot be met by accident
    AnAffixInHand,           // + TemptationAffix: you own the stat you refused
    GearForTheBareSlot,      // you own an item for the slot you left empty
    TwoStylesInReach,        // your mastery gives you skills of two or more styles
    EveryWovenSourceChosen,  // every woven skill has a chosen variation — the Source was a decision
    AKeystoneKnown,          // the world has already given you a doctrine
}

// on the Vow record:
public VowProof Proof { get; init; } = VowProof.Demand;
public int ProofWaves { get; init; }
public VowTemptation Temptation { get; init; } = VowTemptation.None;
public Economy.AffixStat? TemptationAffix { get; init; }
```

Six temptation values, each with **exactly one** read inside `Vows.Discoverable(...)`. `GearForTheBareSlot` reads the vow's own existing `Bare` field, so the three gear vows need no extra data.

`ProofWaves` scales with `Severity`, which is the same pricing logic the catalogue already runs on: a heavier restriction asks for a longer proof.

**One structural note on `ProofWaves`:** the proof descent may be run in **any** region. A late player who wants VOW OF THE SINGULAR goes back to Verdant Hollow and holds fifteen waves on two skills. That is not a loophole — it is the intended shape. The region ladder (`RegionLadder.HealthStep 1.62×`) is the difficulty dial the player already owns, and proving a vow becomes a deliberate expedition rather than an accident, which is exactly §44.

### 4. THE THIRTEEN — discovery rules

Payout column is `Vows.Multiplier` as it computes today (`Vows.cs:214-247`). Every condition is expressed in terms the game already sees; the *only* new state is the shared per-descent counter of §5.

| # | Vow (id) | Pays | Restriction as the code sees it | Proof waves | Temptation — what you owned and refused | New state? |
|---|---|---|---|---|---|---|
| 1 | VOW OF COMPLETION `vow_complete` | ×1.300 | `SkillsWoven >= SkillSlots` | **GRANTED** | — | none |
| 2 | VOW OF THE DELIBERATE `vow_deliberate` | ×1.750 | `ctx.SkillRate <= 1.001` | 10 | `AnAffixInHand(SkillRate)` — you own an item carrying a skill-rate bonus | counter |
| 3 | VOW OF THE FRANTIC `vow_frantic` | ×1.750 | `ctx.SkillRate >= 1.399` | 10 | `None` — 1.40× is unreachable by accident | counter |
| 4 | VOW OF THE BLUNT EDGE `vow_bluntedge` | ×1.825 | `ctx.CritPercent <= ctx.BaseCritPercent + 0.01` | 12 | `AnAffixInHand(Crit)` | counter |
| 5 | VOW OF THE PURE `vow_pure` | ×1.900 | `ctx.DistinctSources <= 1` **and** `ctx.SkillsWoven >= 2` | 12 | `EveryWovenSourceChosen` — every woven skill has a chosen variation, so the Source was picked, not defaulted | counter |
| 6 | VOW OF THE BAREFOOT `vow_barefoot` | ×1.975 | `!ctx.WornSlots.Contains(Boots)` | 15 | `GearForTheBareSlot` — you own boots | counter |
| 7 | VOW OF THE OPEN HAND `vow_openhand` | ×1.975 | `!ctx.WornSlots.Contains(Gloves)` | 15 | `GearForTheBareSlot` | counter |
| 8 | VOW OF THE BARE SKULL `vow_bareskull` | ×2.050 | `!ctx.WornSlots.Contains(Helm)` | 15 | `GearForTheBareSlot` | counter |
| 9 | VOW OF THE UNGUARDED `vow_unguarded` | ×2.050 | `ctx.Defence <= 0` | 15 | `AnAffixInHand(Defense)` | counter |
| 10 | VOW OF THE SINGULAR `vow_singular` | ×2.125 | `ctx.DistinctStyles <= 1` **and** `ctx.SkillsWoven >= 2` | 15 | `TwoStylesInReach` | counter |
| 11 | VOW OF THE UNBOUND `vow_unbound` | ×2.200 | `ctx.KeystonesWorn == 0` | 20 | `AKeystoneKnown` — the world has given you at least one | counter |
| 12 | VOW OF FRAGILITY `vow_fragility` | ×1.888 | **conduct:** `ctx.DamageTakenMultiplier > 1f` — you were already taking more damage than normal | 15 | `None` — the producers are all deliberate mastery investments | counter + 1 ctx field |
| 13 | RECKLESS OFFERING `vow_reckless_offering` | ×2.200 | **conduct:** `ctx.KeystoneHealthMultiplier < 1f` — a socketed keystone already cost you maximum health | 15 | `AKeystoneKnown` (implied by the condition) | counter + 1 ctx field |

### Why the two static-cost vows are different, and why that is honest

LAW 10 is written for demand vows and **cannot be obeyed literally by these two.** You cannot voluntarily take 12.5% more damage or lose 15% of your pool — there is no dial the player can turn. `VowDemand.None` is exactly this admission: `IsActive` returns `true` unconditionally for `StaticCost` (`Vows.cs:369`), so there is no restriction to satisfy.

Rather than invent a fake demand, the proof is **conduct — you had already accepted that cost from somewhere else:**

- **FRAGILITY** (+12.5% damage taken) → you ran a descent whose build already raised damage taken. Three producers exist, all deliberate mastery purchases: ZEALOT `DamageTaken = 1.15f` (`MasteryCatalog.cs:197`), and two more at `:241` (1.25) and `:326` (1.15). The card reads `build.Shape.DamageTaken > 1f` — a value `SkillShape.Combine` already resolves (`SkillShape.cs:530`).
- **RECKLESS OFFERING** (−15% max health) → you ran a descent with a keystone that already cost you health: GLASS CANNON 0.5, BLOODLUST 0.75, DYNAMO 0.85 (`Keystones.cs:33, 52, 162`). Reads `BuildMods.Sum(build.Keystones.Select(k => k.Mods)).Health < 1f` — the keystone contribution alone, so training can never mask it.

Both are **intentional build restrictions** (§47's own words), both are deterministic, both are reversible, and both naturally land mid-career, because keystones and mastery greaters arrive after conquest. The player's experience is the right one: *you had already agreed to be hurt; now the game offers to pay you for it.*

### The two new fields, and their single read sites

Added to `BuildContext` (`Vows.cs:186-201`) and written once in `SoloBattle.DescribeBuild` (`SoloBattle.cs:2580`), which is already "describe a build to the Vow layer":

| Field | Written | Read |
|---|---|---|
| `float DamageTakenMultiplier` | `DescribeBuild`, from `build.Shape.DamageTaken` | `Vows.ProofHolds`, FRAGILITY's clause — one site |
| `float KeystoneHealthMultiplier` | `DescribeBuild`, from `BuildMods.Sum(keystone mods).Health` | `Vows.ProofHolds`, RECKLESS OFFERING's clause — one site |

**The fight loop gains nothing.** No new `SkillDef` dial, no new `SkillRules` member, no new `SkillShape` field, no new `BattleEventKind`, no new `WaveMetrics` counter. Every effect a vow has on the fight — `VowFactor` at `SoloBattle.cs:1641/1820/2069/2375`, the fragility bill at `:675`, `VowHealthMultiplier` at `:2511`, TITHE at `:652/:954`, the affinity buy-back at `:1852/:2072` — already exists, is already read, and is untouched by discovery.

### 5. End-of-descent, or a running counter? — one honest answer for all thirteen

**The condition is checkable at end of descent. The proof is not, and the reason is one line of existing code.**

Every one of the thirteen conditions is a property of the BUILD (P3), and the build at run end is still assembled — `Game1.cs:4438` proves it by composing one there today. So a naive implementation needs no counter at all.

But the build **can change mid-descent**: `SoloExpedition.ReplaceBuild` (`:208-225`) swaps it, and the hunt compares `PlayerLoadout.Signature` at every wave boundary and re-composes when it moved (`PlayerLoadout.cs:53-56`). A player could clear fourteen waves on a four-skill, four-source, fully-armoured build, drop to one skill in the breath before wave fifteen, die, and the end-of-descent context would report `SingleSource`, `SingleStyle`, `NoDefence` and three bare slots — six vows from a build that never fought.

So all thirteen share **one** piece of new state:

```
SoloExpedition:  Dictionary<string,int> VowProofWaves
```

- **Cleared and reset per descent.** Never persisted, never crosses a run.
- **One write site:** `SoloExpedition.PushWave`, beside `Recorder.Record(next, metrics)` (`SoloExpedition.cs:346`), guarded on `outcome == WaveOutcome.Cleared`. The wave that killed you does not count. `_build` and `_hunter` are both in scope there.
- **One read site:** the `_expedition.LogDirty` block, `Game1.cs:4429-4441`, immediately after `if (VowWasKept()) _runsWithVowKept++;`.
- **Cost:** one `DescribeBuild` per *build epoch*, not per wave — the sim already caches exactly this context for the same reason (`SoloBattle.cs:576`: *"The Vow context is a property of the BUILD, so it is built once"*).

This counter also subsumes the depth proof, so `RunReport` needs no new field.

**No vow needs an account-lifetime counter.** That is the sharp contrast with the trait system, which by design accumulates history (`AUDIT` D6: shield absorbed, kills, hits, duration). Vows read a build; traits read a life. Keeping vow discovery out of `WaveMetrics` is what stops the two systems answering the same question — §118's actual test.

### 6. The tutorial vow, the reveal, and the copy

### The vow that is available openly (§46)

**VOW OF COMPLETION is granted, not discovered.** It is already authored for this job — its own catalogue comment reads: *"The one Vow most builds already satisfy, and priced accordingly — it exists so the catalogue has an entry a new player can take without giving anything up yet"* (`Vows.cs:272-273`). Severity 0.20, ×1.300.

It arrives with the BUILD screen, at `DeepestWave >= 5` (`Unlocks.cs:132`) — the gate that already exists, is already monotone, and is already where vows are configured. §43 asks for "an early account/world progression milestone"; this is the one the game already has, and PLAN D7's rule (never gate on a non-monotone fact) is satisfied by construction.

It teaches the whole grammar in one card: fill your slots, be paid; break the rule, be paid nothing. `LoadoutScreen.cs:1038` already draws the live `OK` / `BROKEN` pill, so the lesson is visible on the workbench with no new UI.

### Reveal ceremony

One overlay, same weight as the conquest message (`Game1._conquerMsg`, `Game1.cs:4452-4456`), fired from the run-end block, queued if several land at once. Three lines:

1. **The headline** — `A VOW HAS REVEALED ITSELF` (granted: `A VOW IS OFFERED TO YOU`)
2. **WHAT YOU DID** — one plain past-tense sentence. **This is the only place a discovery rule is ever stated in the game**, and it is stated after the fact. That is how §46's hidden conditions and §31's "naturally discoverable" coexist: the player never sees a checklist, but every reveal teaches the grammar and lets them guess the next one.
3. **The line** — one sentence of flavour.

### The copy (plain English, game terms, no abbreviations)

| Vow | WHAT YOU DID | The line |
|---|---|---|
| VOW OF COMPLETION | *Fill every skill slot you own, and every skill hits 30% harder. Leave one empty and the vow pays nothing.* | "The first promise is the easy one. That is why it is first." |
| VOW OF THE DELIBERATE | You cleared 10 waves at skill rate 1.00, though you owned gear that would have made your skills come back sooner. | "Slow hands. Heavy blows." |
| VOW OF THE FRANTIC | You cleared 10 waves with your skills sped up by 40% or more. | "You never stood still long enough to be found." |
| VOW OF THE BLUNT EDGE | You cleared 12 waves with your critical chance untouched, though you owned gear that would have raised it. | "No lucky hits. Only sure ones." |
| VOW OF THE PURE | You cleared 12 waves with every skill drawing one Source, and you chose each of those Sources yourself. | "You spoke in one voice long before anyone was listening." |
| VOW OF THE BAREFOOT | You cleared 15 waves with no boots, though you owned a pair. | "You walked the depths on your own feet." |
| VOW OF THE OPEN HAND | You cleared 15 waves with no gloves, though you owned a pair. | "Nothing between your hands and the work." |
| VOW OF THE BARE SKULL | You cleared 15 waves with no helm, though you owned one. | "You looked the depths in the face." |
| VOW OF THE UNGUARDED | You cleared 15 waves with no defence at all, though you owned gear that would have given you some. | "Nothing between you and the wave, and the wave noticed." |
| VOW OF THE SINGULAR | You cleared 15 waves carrying one style of skill only, and left slots empty rather than fill them with anything else. | "You went deep where the road was narrow." |
| VOW OF THE UNBOUND | You cleared 20 waves with no keystone socketed, though the world had already given you one. | "You were handed a doctrine, and you set it down." |
| VOW OF FRAGILITY | You cleared 15 waves carrying something that already made you take more damage. | "You had agreed to bleed. Now you will be paid for it." |
| RECKLESS OFFERING | You cleared 15 waves with a keystone that already cost you maximum health. | "You gave part of yourself away, and came back anyway." |

### Before discovery (§91)

The BUILD screen's vow list shows discovered vows exactly as it does today (`Short`, `Description`, the live `OK`/`BROKEN` pill). Undiscovered vows are one grouped row of `???` with a count — `4 OF 13 VOWS FOUND` — and **no** condition, no progress, no percentage. The count exists because a player who cannot tell "there is more here" from "this is all there is" will stop looking; it is a total, not a checklist.

### 7. §48 — vow capacity: it should become real, and here is the argument

### The finding: today's "capacity" is not a capacity, and the dominant play is degenerate

A vow rides a skill slot: `EquippedSkill(SkillDef, Source, Vow?)` (`Build.cs:217`), `SkillChoice(Source, string? VowId, …)` (`PlayerLoadout.cs:40`), `SavedSkill.VowId` (`SaveGame.cs:327`). `AUDIT` §1d: *"There is no Vow capacity constant anywhere."* The cap is `PlayerLoadout.SkillCapacity`.

But trace how the sim actually pays and charges:

| | Site | Scope |
|---|---|---|
| **Benefit** | `VowFactor(sk, ctx, shape)` — a skill reads **its own** vow only (`SoloBattle.cs:2549-2560`), applied at `:1641`, `:1820`, `:2069`, `:2375` | per skill |
| **Price** — fragility | `foreach (var v in DistinctVows(skills))` (`:675`) | **per build** |
| **Price** — health | `VowHealthMultiplier` over `DistinctVows` (`:2515`) | **per build** |
| TITHE | `DistinctVows(build.Skills).Count()` (`:652`, `:954`) | **per build** |

**Distinct vows never compound on a single skill.** Each skill takes exactly one vow's bonus. So four different vows give the same per-skill multiplier as one vow repeated — while charging four prices and demanding four restrictions hold simultaneously. **The rational play is: satisfy the single highest-severity restriction you can, and bind that one vow to every slot.** Wearing a second, weaker vow on a slot is strictly worse than wearing the first one there again.

The code already knows this and says so out loud (`SoloBattle.cs:668-670`): *"ONCE PER VOW, not once per skill wearing it. A Vow is sworn, not equipped."* **Half the system has already migrated to build scope. `VowFactor` is the leftover.**

The only thing in the entire game that rewards *distinct* vows is TITHE, an item enchantment.

### Four reasons it must become a real, build-level number

**1. There is nothing to grant.** §48 asks capacity to unlock through automatic account progression. A milestone cannot grant a number that is a side effect of a different system's cap. Worse, PLAN **D8 deletes `weave_5`** — so on the day the skill-slot decision lands, the emergent vow cap silently drops from 5 to 4. A capacity that moves because someone else's ceiling moved is precisely the coupling this project's memory calls its signature bug.

**2. The UI teaches a decision with one correct answer.** `BIND TO SLOT {n}` (`LoadoutScreen.cs:1637`) presents a per-slot choice whose optimum is always "the same as the last slot." That is a lie the screen tells four times.

**3. Three systems already multiply vow power, and there is nowhere to bound them.** `AUDIT`: PLEDGE ×1.30 (`MasteryCatalog.cs:178`), ZEALOT ×1.60 (`:197`), THE OATHBOUND ×1.50 (`CharacterRoster.cs:209`) — multiplicative in `SkillShape.Combine` (`:451`) — **×3.12 on the bonus**. (`artifice_vows` ×1.25 dies with the tree.) Under the per-slot model that ×3.12 rides the one replicated vow across every skill, and the only lever anyone has is skill slots, which belong elsewhere. A real capacity plus a combined ceiling gives the stack **exactly one place to be measured and one knob to turn.**

**4. Nothing is taken away.** At capacity 1 the game is numerically identical to today, because today's four "vows" were one vow four times. Capacity 2 and 3 are new power the player earns — §58 satisfied without an exception.

### The recommendation

**Make the vow a promise about the BUILD. Bound it with an explicit `VowCapacity` of 1 → 2 → 3, granted by conquest count. Compose multiple vows by adding their bonuses under one ceiling.**

```
VowFactor(build, ctx, shape)
    = 1 + min(CombinedBonusCeiling, Σ over sworn-and-active v of (Vows.Multiplier(v) - 1))
          * shape.VowPowerMultiplier
```

One new `VowTuning` knob (`CombinedBonusCeiling`, ~1.35 as a starting point — just above today's single-vow best of +1.20) with one read site. The clamp sits **before** `VowPowerMultiplier`, so PLEDGE / ZEALOT / THE OATHBOUND still pay for their investment, and the worst case in the game is one bounded number a sweep can print.

### Capacity milestones (§48 — automatic, monotone, already-tested facts)

| Sworn vows | Milestone | Fact that already exists |
|---|---|---|
| 1 | the BUILD screen opens, with VOW OF COMPLETION | `DeepestWave >= 5`, `Unlocks.cs:132` |
| 2 | two regions conquered | `RegionsConquered >= 2` |
| 3 | four regions conquered | `RegionsConquered >= 4` |

A `Unlocks.VowCapacity(UnlockFacts)`, written beside `Unlocks.SkillSlots` (`:239-246`), in the file whose law is *"Derived, never stored"*. Conquest is PLAN D9's faucet for keystone sockets too; put sockets on conquests 1 and 3 and vows on 2 and 4, and every conquest hands the build vocabulary exactly one new thing.

### The exact migration this implies in `SoloBattle`

Five sites, no new ones:

| Site | Today | Becomes |
|---|---|---|
| `:2549` `VowFactor` | per-skill, per-vow | per-build, computed once per build epoch beside the context at `:576` |
| `:1641`, `:1820`, `:2069`, `:2375` | `VowFactor(sk, …)` | `VowFactor(build, …)` — same four multiplications |
| `:652`, `:675`, `:2515` | `DistinctVows(build.Skills)` | `build.Vows` — three sites simplify and one helper is deleted |
| `:1852`, `:2072` affinity buy-back | `sk.Vow is not null` | `build.Vows.Count > 0` |
| `Vows.Multiplier` | one vow | `Vows.MultiplierFor(IReadOnlyList<Vow>, ctx)` with the ceiling |

**A live bug to fix in the same pass:** the affinity buy-back at `:1852` tests `sk.Vow is not null` and **never asks `IsActive`** — a *broken* vow currently buys back an off-discipline skill's affinity penalty. Restriction is meant to buy power; today merely *claiming* a restriction does.

**Storage:** keep `SkillChoice.VowId` as the save shape for one version and read the distinct set out of it (`PlayerLoadout.SwornVows`), so no save changes shape on day one. Move to a `SwornVowIds` list when the per-slot field has no readers left. The screen becomes the keystone chip strip, which already ships: `{n} / {cap} SOCKETS` (`LoadoutScreen.cs:1103`) → `{n} / {cap} VOWS SWORN`. §91 is satisfied — vows stay on the BUILD screen and do not become a second collection screen.

### 8. Persistence, migration, and the stale copy that must die

### The one new save field

`SaveGame.DiscoveredVowIds : List<string>`. Vow knowledge has **no field today** — it is derived from `MemoryDustUnlocks` through `DustEffects.VowGrants` (`DustEffects.cs:102-117`), and that producer dies with the tree (PLAN D6). A discovery is an *event*, so it must be latched, exactly like `QuestsDone`, `RunsWithVowKept` and `UnlockedCharacters`. Parsed with `Enum`/id tolerance and unknown ids dropped, per the load-path discipline at `AUDIT` §2.

### Migration (§97 — nobody re-proves what they already had)

On load, if `DiscoveredVowIds` is absent, seed it from `DustEffects.VowGrants` against the save's `MemoryDustUnlocks`: `vow_study_1` → COMPLETION + DELIBERATE, `vow_study_2` → PURE + FRANTIC, `vow_study_3` → SINGULAR + BLUNT EDGE, `vow_binding` → the three gear vows, `vow_sacrifice` → FRAGILITY, RECKLESS OFFERING, UNGUARDED, UNBOUND. Always add `vow_complete`. One frozen table read once, in the shape of `LegacySkillForm` / `LegacyUnlocks`.

### The four enforcement sites keep their shape and change their source

| Site | Today | Becomes |
|---|---|---|
| `LoadoutScreen.cs:538` | `DustEffects.KnownVows(Tree)` | the discovered list |
| `BuildComposer.cs:148` | `DustEffects.KnowsVow(tree, id)` | `discovered.Contains(id)` |
| `Game1.cs:800-802` | strips an untaught vow on load | unchanged logic, new source |
| `PlayerLoadout.cs:206-212` `SetVow` | refuses an unknown vow | unchanged — its `knownVows` argument is already the seam |

### Player-facing strings that become false (§93)

| Where | Now | Must become |
|---|---|---|
| `LoadoutScreen.cs:1578` | `"NO VOW ON THIS SLOT — VOWS ARE LEARNED ON THE TRAITS SCREEN"` | `"NO VOW SWORN — A VOW REVEALS ITSELF WHEN YOU OBEY IT WITHOUT IT"` |
| `Vows.cs:315` `vow_unbound.Description` | `"…YOU GIVE UP THE TRAIT TREE'S PRIZE."` | `"YOU MAY WEAR NO KEYSTONE. THE WORLD'S DOCTRINES ARE NOT FOR YOU."` |
| `TraitRoads.cs:38-39`, `:44-45` | road identity sentences naming vows | deleted with the tree |
| `LoadoutScreen.cs:1637` | `BIND TO SLOT {n}` | `SWEAR A VOW — {n} / {cap}` |

### What is not touched

`Career.VowWasKept` (`Career.cs:85-91`) and quest `q_three_vows` (`Quests.cs:175-178`), THE OATHBOUND's gate, keep working unchanged — "a descent finished with a vow's demand still met" is still exactly what it tests, and it now sits four lines above the discovery check that shares its context.

### 9. Tests (§102), and the guard against the project's signature bug

### Discovery, per vow (12 discoverable + 1 granted)

1. **The restriction can be satisfied with the vow unsworn** — build a compliant build, run `ProofWaves` waves, assert the counter reaches the bar.
2. **Satisfying it discovers the vow** — the run-end check adds the id.
3. **No reward before discovery** — with the vow undiscovered, `BuildComposer` composes `null` and `VowFactor` is 1.0. Damage-dealt must be identical to a no-vow control.
4. **Sworn does not count** — the same descent with the vow already equipped increments nothing. This is LAW 10's actual test.
5. **Temptation is required** — the same compliant descent on an account that owns no boots / no crit item / no keystone discovers nothing.
6. **Proof waves are required** — one wave short discovers nothing.
7. **Mid-run swap cannot fake it** — 14 waves on a non-compliant build, `ReplaceBuild` to a compliant one, die on wave 15: nothing is discovered. *This is the test that justifies the counter existing.*
8. **Checkpoints cannot fake it** — `StartAtWave(40)`, die on 41: nothing is discovered.
9. **Account-wide and permanent** — discovered under SEEKER, present under MAGPIE; survives mastery respec, character switch, save/load.
10. **Determinism** — the same descent twice, two different seeds, same discovery set. No RNG anywhere in the path.

### Capacity

11. `VowCapacity` is monotone in `UnlockFacts` (the `Unlocks.cs:116-122` invariant).
12. Swearing beyond capacity is refused below UI level.
13. Capacity 1 reproduces today's numbers exactly, on the vow sweep's mid-career fixture.
14. The combined ceiling binds: three maximal vows × the ×3.12 power stack lands on one asserted number.

### Vow liveness — the same bar the variation and reinforcement suites hold

**Every one of the thirteen, run through a real fight with its demand met, must move damage-dealt or health-kept against the same fight with the vow unsworn.** The vow catalogue has never had this suite; the variations and reinforcements do, and the reason is the same. `VowTuning.StaticCostConversionRate`'s own remarks record that FRAGILITY and RECKLESS OFFERING were once *net-negative to swear* — measurable only because someone ran the sweep. A vow that cannot move either number is a dial with no consumer.

### The dormant-feature guard

This design adds **no fight-loop dial**. Everything a vow does in combat already exists and is already read. The three things it adds are: two `BuildContext` fields (one read site each, both outside the fight), one per-descent counter (one write site, one read site), and one save list. If any of those four ends the refactor with no live reader, it is the bug this project keeps paying for — and each has been named with its site precisely so a reviewer can check.

**Open questions this design leaves to the implementer**

- The combined bonus ceiling (§7). 1.35 is a starting point, not a measurement. It must be set by the vow sweep on its mid-career fixture — the same bench that set StaticCostConversionRate to 8.0 — before capacity 2 or 3 ships. Alternative shapes if a flat clamp reads badly: diminishing per additional vow (full, half, quarter), or clamping the summed Severity at ~0.90 and feeding the existing ConditionalMultiplier curve once.
- Capacity milestones: conquest 2 and 4 (proposed), or wave depth (25 and 40), or one of each. Conquest interleaves cleanly with PLAN D9's keystone sockets and gives every conquest exactly one build-vocabulary reward; depth would let a player who farms one region deeply earn it without travelling. Pick one faucet and let the other system own the other.
- Whether to move EquippedSkill.Vow / SkillChoice.VowId out immediately or keep the per-slot field as storage for one version behind a build-level UI. The staged path costs one release of a field that no longer means what its name says; the immediate path costs a save migration during a refactor that already has several.
- Whether any Signature Skill should share a style with an existing shared skill. The catalogue has exactly two skills per style, so VOW OF THE SINGULAR can never be met at 4 slots with slots full — it always leaves at least one empty, and is therefore permanently mutually exclusive with VOW OF COMPLETION. That is a good tension, but it should be a decision, not an accident of D2's authoring.
- FRAGILITY's conduct clause. Chosen: build.Shape.DamageTaken > 1, which has exactly three producers, all mastery greaters (MasteryCatalog.cs:197, :241, :326). If PLAN D11's road rework moves or deletes any of them, the vow becomes harder or impossible to prove. The alternative — RunReport.HealthLostPerWaveFraction >= 0.125, i.e. 'you were already losing 12.5% of your pool a wave and held anyway' — needs no producers at all and reads a number the report already computes, but it is an outcome rather than an intentional build restriction, which is the letter of §47.
- Whether the AnAffixInHand temptation counts items in the VAULT and unopened chests, or only the loose inventory. Loose-only is the smaller read and the more honest 'you had it in your hand'; including the vault avoids a player being told they own nothing while a crit ring sits in storage.
- Whether VOW OF COMPLETION should keep paying once the player has three or four slots. It is satisfied by any full build forever, so at capacity 2 it is a permanent free +0.30 of bonus alongside a real vow. Options: leave it (it is bounded by the combined ceiling and it is the tutorial), let its Severity fall as SkillSlots rises, or retire it from the payable set once a second vow is sworn.
- Whether the undiscovered vows show a grouped '???' row with a found-count, or nothing at all. §91 permits '???' and §90's no-completion-percentage rule is written about Traits, not Vows — but the two screens will be read as one system by the player.

## Keystone Acquisition — the world teaches the build (BRIEF §49–§52, LAW 11)

### 1. What this design rests on (read, not assumed)

**The world's events, from the code.**

| Event | Where it lives | Cost to reach | Monotone? |
|---|---|---|---|
| Region conquered | `World.Conquer` (`Regions.cs`), fired at `Game1.cs:4447` when `_expedition.Deepest >= Checkpoints.ConquestWave` (**20**) | hold wave 20 once, in a chain of 6 | yes — `_conquered` is a `HashSet` that is only ever added to |
| PARTLY MASTERED | `Region.MasteryLevel` ≥ 1, `RmpThreshold1 = 500` | `RecordActiveKill` is called **once per cleared wave** (`Game1.cs:4379`) at `RmpPerActiveKillBonus = 5` → **100 waves in that region** | yes — points only ever added |
| FULLY MASTERED | `RmpThreshold2 = 2000` | **400 waves in that region** | yes |
| PERFECTED | `RmpThreshold3 = 5000` | **1,000 waves in that region** | yes |
| First corruption deepening | `World.DeepenCorruption`, needs `AllConquered` | conquer all six, then press DEEPEN | yes — `PeakCorruptionTier` never falls, by construction |

Six regions × (1 conquest + 3 mastery rungs) = **24 events**, plus the corruption ladder. Nineteen keystones to place.

**Three facts that shape everything.**

1. **Conquest is the fast axis; mastery is the slow one.** Twenty waves versus one hundred / four hundred / a thousand *in one place*. A player pushing forward walks all six conquests and may never fully master anything. Anything the rest of the game depends on must ride the conquest axis.
2. **Mastery and conquest are about to lose their only build-facing payout.** `Career.TraitPointsEarned` (`Career.cs:55-62`) is `conquests + 2 × PeakCorruptionTier + Σ mastery levels`, and PLAN D6 deletes it with the tree. Conquest, region mastery *and* the corruption ladder all stop paying. Keystones are the natural replacement, and that is the strongest argument for §50 that the brief did not make.
3. **Every dial this design needs is already read in the fight.** All thirteen `BuildTrigger`s the nineteen keystones grant have live read sites in `SoloBattle.cs` (`Splinter`, `NoHealing`, `Bloodlust`, `Echo`, `Undying`, `Venom`, `Zeal`, `Rend`, `Capacitor`, `Dynamo`, `Lodestone`, `Weaver`, `Hoarder` — grep count 1–2 each). **This design adds no new combat dial and touches no line of `SoloBattle`.** It changes only *who hands you the keystone*, which is exactly what LAW 11 asks. `Keystones.IsATrade` still passes on all nineteen, unchanged.

### 2. The event budget: why 6 + 6 + 6 + 1

| Rung | Keystones | Reason |
|---|---|---|
| The six conquests | 6 | The spine every player walks. Carries the doctrines the rest of the game depends on. |
| Six × PARTLY MASTERED (100 waves) | 6 | The first reason to farm a place you already own. |
| Six × FULLY MASTERED (400 waves) | 6 | The deep rung — where three of the four road terminals land. |
| First corruption deepening | 1 | The nineteenth. Repays the ladder that loses two trait points a tier under D6, and gives WEAVER — the strangest keystone in the catalogue — the strangest gate. |
| Six × PERFECTED (1,000 waves) | 0 | Deliberately empty. It already pays Memory Dust (`CorruptionScaling.MasteryLevelDust`) and pushes `RegionProgressionOf` to 4, the region's best loot tier and hardest enemies. A keystone there would be one almost nobody ever sees. |

The sentence a player can learn: **every region teaches you three — one for taking it, one for knowing it, one for mastering it.** Six regions, eighteen keystones, and the nineteenth waits past the end of the world.

### 3. The mapping — all nineteen

| # | Region (Source, bias) | CONQUEST — hold wave 20 | PARTLY MASTERED — 100 waves | FULLY MASTERED — 400 waves |
|---|---|---|---|---|
| 1 | VERDANT HOLLOW (Nature, Balanced) | **ECHO** — every skill fires twice, each at 60% | **IRONCLAD** — double health, skills come back half as often | **GREED** — double loot, you hit 30% softer |
| 2 | CINDERWORKS (Machine, Heavy) | **GLASS CANNON** — double damage, half health | **BLOOD MAGIC** — skills come back twice as fast, you cannot be healed | **LODESTONE** — clear a wave with a full charge pool for a spare core |
| 3 | UMBRAL REACH (Shadow, Fast) | **BLOODLUST** — the closer to death, the harder you hit | **VENOMANCER** — your skills poison, they hit 20% softer | **DISCERNING EYE** — far rarer finds, half the loot |
| 4 | MARROW WASTES (Body, Heavy) | **REND** — every skill use stores 1 charge; your strikes spend it all | **FORTUNE** — double loot rarity, you hit 25% softer | **REAPER** — every kill gives richer loot, skills 25% slower |
| 5 | THE STILL ARCHIVE (Mind, Fast) | **JUGGERNAUT** — the fuller your health the harder you hit, you cannot be healed | **DYNAMO** — every hit you take stores 2 charge, you take 15% more | **HOARDER** — more loot means harder hits, rare finds half as often |
| 6 | THE PALE CHOIR (Spirit, Balanced) | **UNDYING** — the first killing blow each run leaves you on 1 health | **CAPACITOR** — your charge pool holds 20, not 10 | **TITAN** — triple health, skills 60% slower |
| — | THE CORRUPTION — first deepening (tier 1) | **WEAVER** — every skill also fires the next skill in your build, at 45% | — | — |

Nineteen ids, each exactly once: `echo, ironclad, greed, glass_cannon, blood_magic, lodestone, bloodlust, venomancer, discerning_eye, rend, fortune, reaper, juggernaut, dynamo, hoarder, undying, capacitor, titan, weaver`. Identities, blurbs, `Mods` and `Grants` are untouched.

**Why each region's three belong together**

- **VERDANT HOLLOW** — the plain doctrines, the three cheapest things in the old tree (ECHO, IRONCLAD and GREED were all 4-point road heads). The Hollow is where you learn to fight, where you learn to survive, and where you farm. ECHO first because it *visibly* changes the fight — two casts, not a bigger number — which is what a first keystone has to teach.
- **CINDERWORKS** — Machine, slow heavy blows. GLASS CANNON is the region's own answer: kill it before it swings, and pay in the health it takes off you. (This is §51's own example, kept verbatim — see §4, the constraints leave it there anyway.) BLOOD MAGIC is the furnace's cadence: never resting, never mending. LODESTONE is a magnet in an ironworks, and it is the one CHARGE keystone that works with nothing else socketed.
- **UMBRAL REACH** — Shadow, fast creeping strikes. The region bleeds you a cut at a time, so it teaches BLOODLUST, the doctrine of fighting from the edge. VENOMANCER is the shadow's own weapon. DISCERNING EYE is what mastering the dark is: seeing what is worth taking.
- **MARROW WASTES** — Body, a slaughterhouse. REND is the body that gathers every blow and delivers one. FORTUNE is what is buried in a bone-field. REAPER — richer loot from every kill — is the terminal a slaughterhouse owes.
- **THE STILL ARCHIVE** — Mind, a thousand precise cuts. JUGGERNAUT is the composure that refuses the first one and never mends. DYNAMO records every blow instead of wasting it. HOARDER is what an archive *is*: the pile becomes the power.
- **THE PALE CHOIR** — Spirit, the end of the known world. UNDYING refuses the killing blow. CAPACITOR is the vessel that holds twice as much. TITAN is the thing that does not move.
- **THE CORRUPTION** — WEAVER, for the player who chose to make the world worse.

### 4. The two ordering rules the mapping obeys

These are not taste. Break either one and the game hands a player a keystone that does nothing, which is this project's named failure mode wearing a reward's clothes.

**Rule A — a keystone that needs another keystone is never discovered first.**

`SoloBattle.cs:599-602`: the charge pool is only tracked when `Rend`, `Capacitor`, `Dynamo` or `Lodestone` is socketed. Inside that:

- **LODESTONE** works alone — casts fill the pool, a full pool at the clear pays a core (`:887`).
- **REND** works alone — it spends the pool through a Hammer-style cast (`:1869`).
- **DYNAMO** alone is *pure downside*: it fills a pool nothing reads and charges 15% more damage taken. It needs REND or LODESTONE.
- **CAPACITOR** alone is dead and its own blurb says so — but a blurb is not a schedule.

The chain guarantees it: **a region's conquest is strictly before any event in a later region**, because regions unlock in a fixed chain (`RegionDefinition.PrereqId`). A region's *own* mastery rungs are **not** guaranteed to come after its conquest (100 waves accrue whether or not you ever hold wave 20), so the rule is stated across regions, never within one.

| Dependent | Discovered at | Its reader | Discovered at | Guaranteed? |
|---|---|---|---|---|
| DYNAMO | region 5, partly mastered | REND | region 4, **conquest** | yes — region 4 conquest gates region 5 |
| DYNAMO | region 5, partly mastered | LODESTONE | region 2, fully mastered | (second cover) |
| CAPACITOR | region 6, partly mastered | REND | region 4, **conquest** | yes |
| LODESTONE | region 2, fully mastered | — | works alone | n/a |

**Rule B — a keystone the rest of the game depends on is a CONQUEST keystone.** Four enchantments and (in future) any quest or item that names a trigger. See §8. This is what pins ECHO, BLOODLUST and JUGGERNAUT to the conquest row.

**The two rules leave almost no freedom, and that is the design's best evidence.** Rule B claims three of six conquest slots. Rule A forces REND onto the conquest row no later than region 4 — at region 5 the only rungs left after it are region 6's two, which must also hold a terminal. With ECHO first (see §8), BLOODLUST at 3 and REND at 4, only regions 2, 5 and 6 remain for GLASS CANNON, JUGGERNAUT and UNDYING; UNDYING is the finale, and GLASS CANNON is far more legible as a second keystone than "YOU CANNOT BE HEALED" is. **GLASS CANNON lands on CINDERWORKS by constraint — which is exactly where §51's example put it.**

### 5. What the player sees (§51)

**On a conquest.** The host already has the single site: `Game1.cs:4447-4458` fires once, plays `sfx_conquer`, and writes `_conquerMsg` into the map's strip. The reveal is added there and shown in the same panel, in the brief's own shape plus one line the brief's mock is missing:

```
UMBRAL REACH CONQUERED
MARROW WASTES IS OPEN.

NEW KEYSTONE DISCOVERED

BLOODLUST
THE CLOSER TO DEATH, THE HARDER YOU HIT. YOU TAKE 25% MORE.
```

The blurb line is not optional. `Keystone.Blurb` is already written in plain game terms, and a player who has never owned a keystone learns nothing from the word BLOODLUST alone — that is the second-language rule, and it is the difference between a relic and a menu entry.

On the **first** conquest only, one more line, because the socket arrives on the same event:

```
A KEYSTONE SOCKET OPENS. WEAR IT ON THE BUILD SCREEN.
```

**On a mastery rung.** This happens mid-farm, with the player very possibly not watching, so it must never be modal. It uses the existing two-line notice toast (`Game1.Notice`, "HEAD\nDETAIL", top of screen, queued):

```
NEW KEYSTONE — IRONCLAD
VERDANT HOLLOW IS PARTLY MASTERED. DOUBLE HEALTH, SKILLS COME BACK HALF AS OFTEN.
```

**On the map.** Each region card gains a three-rung strip — CONQUERED / PARTLY MASTERED / FULLY MASTERED — with the keystone named once discovered and shown as `???` before. That makes the world screen the place a player goes to ask "what else can my build become", which is what §50 means by *world progression expands build vocabulary*. It shows how much is left without spoiling what.

**On the corruption.** The DEEPEN button's confirmation gains one line, and tier 1 pays WEAVER through the same conquest-style panel.

### 6. Copy that must change (§92, §93)

| Where | Today | New |
|---|---|---|
| `LoadoutScreen.cs:1103` | `LEARN THEM ON THE TRAITS SCREEN` | `CONQUER A REGION TO FIND YOUR FIRST KEYSTONE` |
| `LoadoutScreen.cs:1113` | `NO KEYSTONES LEARNED YET` | `NO KEYSTONES YET — THE WORLD TEACHES THEM` |
| `LoadoutScreen.cs:1102` (header right) | `{worn} / {capacity} SOCKETS` | unchanged, plus a second line while capacity < 3: `NEXT SOCKET: CONQUER THREE REGIONS` / `NEXT SOCKET: REACH WAVE 80` |
| Keystone list footer (new) | — | `{n} MORE ARE WAITING IN THE WORLD.` |
| Locked keystone row (§92) | — | Not drawn as 12 grey rows. One count line (above) keeps the mystery §92 allows and keeps the list readable at every UI SCALE. |
| Forge, an unpaired combo enchantment | `NEEDS BLOODLUST IN YOUR BUILD` (greyed) | If undiscovered: `NEEDS BLOODLUST — CONQUER UMBRAL REACH TO FIND IT.` If discovered but not socketed: today's line, unchanged. |
| `TraitRoads.cs:39`, the `ks_*` node names, `KEYSTONE SOCKET II/III` | tree copy | deleted with the nodes (PLAN D9) |

The Forge line is the one genuinely new sentence, and it is the point of the whole change: an item that used to be a dead card now points at a place on the map.

### 7. Socket capacity — a separate system (§49, §52)

Discovery says *what you know*. Capacity says *how many you may wear*. They share no state, no screen and no gate. Capacity is a count derived from `UnlockFacts`, in the exact idiom `Unlocks.SkillSlots` already uses.

```csharp
/// <summary>How many keystone sockets are open. None until the world has taught you a keystone.</summary>
public static int KeystoneSockets(UnlockFacts f)
{
    var sockets = 0;
    if (f.RegionsConquered >= 1) sockets++;                          // with the first keystone
    if (f.RegionsConquered >= Regions.All.Count / 2) sockets++;      // 3 — halfway across the world
    if (f.DeepestWave >= Checkpoints.ConquestWave * 4) sockets++;    // 80 — four times the conquest line
    return sockets;
}
```

| Socket | Milestone | Fact | Why this one |
|---|---|---|---|
| 1 | **Your first conquest** | `RegionsConquered >= 1` | The same event that hands you your first keystone. The Build screen is never showing an empty socket with nothing that could fill it, nor a keystone with nowhere to put it. |
| 2 | **Your third conquest** | `RegionsConquered >= 3` (`Regions.All.Count / 2`) | Halfway across the world. You own at least ECHO, GLASS CANNON and BLOODLUST by then, so the second socket arrives at the first moment there is a real second choice to make — and it arrives *with* BLOODLUST, in one sentence: a third doctrine and the room to wear a second. |
| 3 | **Wave 80, anywhere** | `DeepestWave >= 80` | The other axis. Breadth bought sockets 1 and 2; depth buys the last one, so a player who has only walked the map wide still has two. 80 is `Checkpoints.ConquestWave * 4`, the number `q_marrow_hold` already calls deep. |

**Locked copy** (§93, plain English, no abbreviations):

- `CONQUER A REGION TO OPEN YOUR FIRST KEYSTONE SOCKET`
- `CONQUER THREE REGIONS FOR A SECOND SOCKET`
- `REACH WAVE 80 FOR A THIRD SOCKET`

**Not hunter level.** `HunterProgression.cs:168` is doc-commented *"Cosmetic only — never a gate"* with four display-only readers; AUDIT:2492 says that axis would have to be built from nothing, and it is derived from Gleam spend, a different pacing curve from depth or conquest. `RegionsConquered` and `DeepestWave` are already on `UnlockFacts` (`Game1.cs:3520, 3512`), already fed from `_deepestEver` (a `Math.Max` latch) and `World.ConqueredIds`, and already monotone — which is the hard-won invariant at `Unlocks.cs:116-122` that a re-firing reveal would otherwise break.

**§52 is satisfied literally**: nothing is spent, so the player never chooses between an interesting trait and a basic socket. The trade the design wants is *between keystones*, and it is preserved exactly — the menu of 19 stays far longer than the plate of 3.

### 8. The four combo enchantments — do they die?

**No. They get more reliable, not less. The premise that keystones become rarer is backwards.**

**What they need** (`Enchantments.cs:198-201`, read at `SoloBattle.cs:935, 945, 950, 954`):

| Enchantment | Pool | Needs | Where the partner now comes from | Alive from |
|---|---|---|---|---|
| FERVOUR — `BLOODLUST +x%`, caps at +50% | Weapon | `BuildTrigger.Bloodlust` | UMBRAL REACH conquest | conquest 3 of 6 |
| REVERB — `ECHO HITS +x%`, caps at +30% | Weapon | `BuildTrigger.Echo` | VERDANT HOLLOW conquest | **conquest 1 of 6** |
| BULWARK — `ZEAL +x%`, caps at +50% | Charm / Ring | `BuildTrigger.Zeal` (JUGGERNAUT) | THE STILL ARCHIVE conquest | conquest 5 of 6 |
| TITHE — `PER VOW: +x%`, caps at +10% each | Charm / Ring | any sworn Vow | the free tutorial Vow (PLAN D9) | immediately |

**Why they were more dead before.** A full career earns 34 trait points (`Career.TraitPointsEarned`, and its own doc comment: *"6 + 10 + 18 = 34 is the budget the tree was built around — one terminal reachable, two never"*). Keystones cost 4/6/8/12 and sit behind road prerequisites — `ks_echo` behind `vow_study_1`, `ks_greed` behind `ledger`, four of them behind `socket_2`. A player who spent every point on keystones alone could afford five or six, inside one or two roads. **A player could finish this game having never learned ECHO, and REVERB was then permanently dead for them, with nothing on any screen able to fix it.** Under this mapping, six keystones arrive on the conquest spine at no cost and with no choice, and all nineteen are reachable. Keystones become *common*. What stays scarce is the socket — still 1 → 3 — and that is the correct scarcity, because the enchantment's whole job is to be the argument for spending one of them.

**What the mapping must guarantee — five rules, all testable.**

1. **G1 — every partner is a CONQUEST keystone, never a mastery rung.** Conquest costs holding wave 20 in a chain every player walks; PARTLY MASTERED costs 100 waves of farming one place, which a pushing player may never do. Putting FERVOUR's partner behind a farm gate makes 20% of every rare weapon's headline hostage to a play style. *Held: ECHO, BLOODLUST and JUGGERNAUT are all conquest rewards.*
2. **G2 — the two WEAPON-pool partners come earliest.** `WeaponPool` is `{Splinter, Venom, Harvest, Fervour, Reverb}` — **two of five entries are keystone-gated**, so 40% of every Rare-or-better weapon depends on ECHO or BLOODLUST. `CharmPool` is `{Undying, Desperation, Siphon, Bulwark, Tithe}` — also two conditional, but TITHE is alive from the tutorial Vow, so a charm never has more than one dead entry. *Held: ECHO at conquest 1, BLOODLUST at conquest 3; JUGGERNAUT, the lower-pressure one, at conquest 5.* This is why ECHO and not IRONCLAD is the very first keystone in the game.
3. **G3 — the partner must be socketable, not merely known.** `EnchantNeed.MetBy` reads `triggers`, and triggers come from **socketed** keystones. So socket 1 can never arrive after the first partner — it arrives on the same event — and socket 2 arrives with the third conquest, which is BLOODLUST's own. At every moment a partner is discovered, there is a socket free or a socket arriving with it.
4. **G4 — no partner behind the corruption ladder or behind PERFECTED.** Those are opt-in endgame facts. The corruption pays exactly one keystone, WEAVER, and no enchantment points at WEAVER. PERFECTED pays none.
5. **G5 — the Forge names the source.** Today an unpaired combo greys itself and says `NEEDS BLOODLUST IN YOUR BUILD`, which is honest and useless: nothing tells the player where BLOODLUST is. The new line names the place (§6). One new read site, in a draw method, against a pure Core lookup — no new dial, nothing in the fight.

**The build enforces it.** `test_every_combo_enchantment_partner_is_a_conquest_keystone`: for every `EnchantKind` whose `Needs` names a `BuildTrigger`, find the keystone that grants it and assert its row's rung is `Conquest`, and that its region index is inside the world's first five. That is the same "wire it or the build fails" discipline as `DustEffects.WiredIds`, which is the guard that has caught this project's signature bug four times — and it is what stops someone moving BLOODLUST to a mastery rung in six months because the table looked tidier that way.

### 9. Code surface, save and migration

**Added — Core, beside the catalogue it describes.**

```csharp
public enum WorldRung { Conquest, PartlyMastered, FullyMastered, Corruption }

/// <summary>Which world event teaches this keystone. RegionId is null for the corruption row.</summary>
public sealed record KeystoneSource(string KeystoneId, string? RegionId, WorldRung Rung, int Tier = 0);
```

| Member | Shape | Read sites |
|---|---|---|
| `Keystones.Sources` | the 19-row table of §3 — pure data | the three below |
| `Keystones.DiscoveredBy(World, IEnumerable<string>? legacyIds)` | `IReadOnlyList<Keystone>` | the `learned` argument of `PlayerLoadout.ToggleKeystone` (`:220`); `BuildComposer.cs:131`; `LoadoutScreen`'s keystone list (`:1098`); the reveal edge-detector |
| `Keystones.SourceOf(string id)` | `KeystoneSource?` | the Forge's unpaired-combo line; the map's region card |
| `Unlocks.KeystoneSockets(UnlockFacts)` | `int` | the two existing sites that call `DustEffects.KeystoneSockets` today: `Game1.cs:786` (load) and `Game1.cs:4334` (per frame) |

The per-rung predicates are one line each: `world.IsConquered(id)`; `world.RegionFarm(id).MasteryLevel >= MasteryLevel.PartiallyMastered`; `>= FullyMastered`; `world.PeakCorruptionTier >= 1`. Every one of those facts only ever grows, so the reveal can never re-fire — the `Unlocks.cs:116-122` invariant, honoured.

**Deleted.** The 19 `ks_*` nodes, `socket_2`, `socket_3`, `DustEffects.LearnedKeystones` (`:48-63`), `DustEffects.KeystoneSockets` (`:205-209`). `MemoryDustTree`'s wallet, faucets and sinks stay (PLAN D6).

**Save: nothing is added, and nothing new is written.** Discovery derives entirely from fields the save already carries — `ConqueredRegions` (`SaveGame.cs:83`), `RegionFarms[].MasteryPoints` (`:389`), `CorruptionPeak` (`:101`). Socket capacity derives from `UnlockFacts`. That side-steps the plan's standing risk about the ten-second autosave window: this change stops writing nothing.

**Migration — nobody loses anything (§58).**

1. *Keystones already learned in the tree.* `MemoryDustUnlocks` stays in the save as a read-ignored field for one version (PLAN D4's rollback discipline). `DiscoveredBy` takes the `ks_*` ids still present there as `legacyIds` and unions them in — the exact shape of `Characters.LegacyUnlocks`, which already reads an old save's record *"so nobody loses a champion they had already earned"*. When the field goes, the bank goes with it, by which time the world has re-earned everything.
2. *Sockets already bought.* `KeystoneCapacity` on load is floored at what the save was wearing: `Math.Max(save.SocketedKeystoneIds.Count, Unlocks.KeystoneSockets(facts))` — the same line, one line up, that already reads `Math.Max(save.WovenSkills.Count, DustEffects.SkillSlots(_dust))` (`Game1.cs:785`). Without it, `PlayerLoadout.Restore`'s truncation (`:337`) would silently strip a returning player's worn keystones.
3. *The first frame after migration.* A deep save derives eleven or more keystones at once. Suppress the per-keystone reveal on that frame and show one notice: `THE WORLD HAS TAUGHT YOU 11 KEYSTONES.` / `THEY ARE ON THE BUILD SCREEN.`

**Tests.**

- `test_every_keystone_has_exactly_one_world_source` — 19 rows, 19 distinct ids, no orphan, no duplicate. The `WiredIds` guard, restated for the new producer.
- `test_a_keystone_that_needs_another_is_never_discovered_first` — for each dependent pair (DYNAMO→REND/LODESTONE, CAPACITOR→REND), assert the reader's region index is strictly lower.
- `test_every_combo_enchantment_partner_is_a_conquest_keystone` — §8's guard.
- `test_socket_capacity_never_falls` over an ascending sweep of `UnlockFacts`, and `test_the_first_socket_opens_no_later_than_the_first_keystone`.
- `test_a_migrated_save_keeps_every_keystone_and_every_worn_socket`.
- `Keystones.IsATrade` over the catalogue — unchanged, and still passing, because no identity moved.

### 10. Tuning knobs

| Knob | Where it lives now | Value | Note |
|---|---|---|---|
| Conquest line | `Checkpoints.ConquestWave` | 20 | Already data. Sockets 3 derives from it (`× 4`), so the two cannot drift apart. |
| Mastery thresholds | `AutomationTuning.RmpThreshold1/2/3` | 500 / 2000 / 5000 | Already a record, already injectable. These are the pacing of twelve of the nineteen keystones — the single biggest lever on this design. |
| Mastery per wave | `AutomationTuning.RmpPerActiveKillBonus` | 5 | With the thresholds, sets 100 / 400 / 1,000 waves. |
| Socket 1 gate | `Unlocks.KeystoneSockets` | `RegionsConquered >= 1` | |
| Socket 2 gate | `Unlocks.KeystoneSockets` | `RegionsConquered >= Regions.All.Count / 2` | derived from the region count, so adding a seventh region moves it honestly |
| Socket 3 gate | `Unlocks.KeystoneSockets` | `DeepestWave >= Checkpoints.ConquestWave * 4` | = 80 |
| Socket ceiling | `Build.KeystoneSlots` / `PlayerLoadout.MaxKeystones` | 3 | unchanged; the three milestones exactly fill it |
| Corruption rung for WEAVER | the table's `Tier` field | 1 | |
| The mapping itself | `Keystones.Sources` | 19 rows | content, not code — a row can be moved without touching a method |

**A pacing note the implementer must not miss.** PLAN D9 deletes `recall_1..4` (FASTER REGION MASTERY) along with the tree, which removes up to +20% from `Region.RecordActiveKill`'s rate. Region mastery therefore gets *slower* for anyone who had bought them, at the same moment it becomes the source of twelve keystones. `RmpThreshold1/2/3` should be re-checked against a real farm before P5 ships — they were tuned when the only prize was a trait point.

**Open questions this design leaves to the implementer**

- WEAVER's home. Recommended: the first corruption deepening (PeakCorruptionTier >= 1) — it is reachable, it is monotone, and it repays a ladder that loses two trait points per tier when Career.TraitPointsEarned is deleted. Alternative: THE PALE CHOIR at PERFECTED, which keeps every keystone inside §50's exact words ("region conquest / region mastery") at the price of putting one of the nineteen behind 1,000 waves in the last region — i.e. behind content most players will never finish. A third option: move it to THE PALE CHOIR's FULLY MASTERED and slide TITAN to the corruption instead.
- Socket 3's depth number: 80 (Checkpoints.ConquestWave × 4, the number q_marrow_hold already calls deep) or 60 (three times the conquest line, roughly where the fifth region's push lands). 80 makes the third socket a genuine endgame reward; 60 gets a full three-keystone build into more players' hands before the last two regions. This is a playtest question, not a reasoning one — and per the standing memory, the expedition balance is inherited and unproven, so it should be measured rather than argued.
- Whether §51's own example (GLASS CANNON at CINDERWORKS) is honoured, as this design does, or overruled. The alternative is JUGGERNAUT at CINDERWORKS — a Machine region that cannot be healed is the best single thematic pairing in the whole catalogue, and it would move BULWARK's partner from conquest 5 to conquest 2, strengthening rule G2. The cost: GLASS CANNON slides to conquest 5, which is very late for the most legible damage doctrine in the game and leaves the world's first two conquests with no plain 'hit harder' option.
- Whether PERFECTED needs a new payout at all. It currently keeps its Memory Dust milestone and the region's top loot tier, but it loses its trait point with Career.TraitPointsEarned. Options: leave it (recommended — three keystones per region is already a full ladder), give it a Memory Dust multiplier, or give it a checkpoint discount. Whatever is chosen, it must not be a keystone: there are only nineteen and all are placed.
- How undiscovered keystones are presented on the Build screen. This design specifies one count line ('7 MORE ARE WAITING IN THE WORLD') to keep the list readable at every UI SCALE profile and to keep §92's mystery. The alternative §92 also allows is twelve greyed 'UNDISCOVERED — found through region conquest' rows, which shows the player the full shape of what is missing at the cost of a wall of dead rows. A middle option: the count line on the Build screen, and the full per-rung strip on the map's region cards (which this design already proposes).
- Whether the map's region card names an undiscovered keystone or shows '???'. Naming it turns the map into a route planner for build vocabulary — genuinely useful, and it makes the Forge's 'CONQUER UMBRAL REACH TO FIND IT' line consistent with the map. Hiding it preserves the 'doctrine discovered from the world' feeling §51 asks for. Note these two must agree: if the Forge names the region, the map cannot pretend not to know it.

## THE TRAIT CATALOGUE — thirty characteristics the account discovers by living, not by buying

### 0. What this is, and the five decisions under it

This replaces `MemoryDustTree`'s 51-node catalogue (BRIEF §22, PLAN D6). The Dust **wallet** stays; only the node catalogue, `TraitTreeLayout`, `TraitRoads` and `Career.TraitPointsEarned` go.

**D-T1 — A trait is a `SkillShape` contribution, exactly like a set rung.** `ElementSets.Catalogue` already proves the pattern: a small authored record carrying a `SkillShape`, folded through `SkillShape.Combine`. Traits use the same seam (`BuildComposer.Compose`, `SkillCatalogue`-style authored list). No second engine.

**D-T2 — The 38 new dials live on ONE new member of `SkillShape`, not on `SkillShape` directly.** `SkillShape` is already ~90 fields; `SkillCatalogue.cs:130-141` records that `SkillDef` hit "the width where a reader stops reading" and answered it with `SkillRules`. Traits get the same answer: `SkillShape` gains **one** field, `TraitRules Traits { get; init; } = TraitRules.None;`, and every read site in `SoloBattle` says `shape.Traits.X`. `SkillShape.Combine` gains one line (`Traits = TraitRules.Combine(a.Traits, b.Traits)`). A build with no trait equipped composes byte-identically to today.

**D-T3 — World-conditional traits are resolved at COMPOSITION time, never inside the fight.** `TraitEffects.Compose(equippedTraitIds, TraitContext)` returns a `TraitOutcome { SkillShape Shape, Source? ForceSkillSource }`. `TraitContext` carries what the fight must never learn: the run's region, the conquered set, region mastery, completed element sets, and the archetype that ended the last run (already in `SaveGame.RunLog` → `RunReport.WallArchetype`). This is why HOMEGROUND and WHAT KILLED YOU need no world state threaded into `SoloBattle`. `BuildComposer.Compose` gains one parameter and one fold.

**D-T4 — Discovery is evaluated at exactly two call sites.** `TraitDiscovery.OnWaveCleared(ledger, waveFacts)` fires in `SoloExpedition.Advance` on the line after the existing `Recorder.Record(next, metrics)` (`SoloExpedition.cs:346`); `TraitDiscovery.OnRunEnded(ledger, runFacts)` fires where the run closes. Both return `IReadOnlyList<string>` of newly-awakened trait ids for the host to reveal (§32) and persist. Nothing else in the codebase may write the ledger.

**D-T5 — No rule reads a single rolled wave.** Creature COUNT and ARCHETYPE are rolled (`Archetypes.Compose` `count = rng.Next(MinCount, MaxCount+1)`; `Bands.Roll`). Band affixes and boss waves are NOT (`WaveScaling.IsBossWave`, every fifth; `forceSingle: isBoss`). So every rule below reads either (a) the player's own build, (b) an account-monotone total that the roll only PACES, or (c) a deterministic fact — a boss wave, a conquest, a completed set, a sworn vow. That is §30 satisfied by construction rather than by inspection.

### 1. New telemetry: seven fields on WaveMetrics, seven write sites

Everything else the rules need, `WaveMetrics` already keeps. These seven do not exist and cannot be derived. Each is one line; only `ReflectedDamage` has two producers.

| # | New field on `WaveMetrics` | Type | Write site (`SoloBattle.cs`) | Why it cannot be derived from what exists |
|---|---|---|---|---|
| M1 | `HeavyHits` | `int` | `LandOn`, beside `metrics.Hits++` (~:1189): `if (dmg >= target.MaxHealth * HeavyHitShare) metrics.HeavyHits++` | `AverageHitSize` is a wave MEAN. "How often did I hit something hard" is a count and the mean destroys it. |
| M2 | `Overkill` | `float` | `LandOn` death branch, at the existing `var spill = -target.Health;` (~:1257) | Nothing records waste. `DeliveredDamage` counts overkill as delivered, which is exactly the fact a waste trait must not read. |
| M3 | `ShieldGained` | `float` | `GrantShield` (:818-824), from the local `added` — the single funnel every producer already passes through | `ShieldAbsorbed` is what the shield SPENT. A shield that was granted and never bitten records zero. |
| M4 | `ShieldBreaks` | `int` | Beside the existing `ShieldBroken` event emission (~:2300) | The event exists; the count does not, and events are not persisted past the wave. |
| M5 | `Healed` | `int` | `Heal`, beside `champ.Health = (int)(champ.Health + landed)` (~:1407) | `HealthLost` is gross loss. In-wave healing has no counter at all today. (Deliberately does NOT count `FullHealBetweenWaves` or `BetweenWaveRegen`, which live in `SoloExpedition` — those are a reset and a regain, not healing.) |
| M6 | `ReflectedDamage` | `float` | **Two**: the THORNS loop (~:2330) and the trap-reflect payout (~:2371) | Inside `LandOn` a reflected hit is indistinguishable from any other `fromSkill:false` hit. |
| M7 | `LowestHealthFraction` | `float`, init `1f` | `champ.Health -= healthLost` (~:2317): `metrics.LowestHealthFraction = MathF.Min(…, champ.Health / (float)champ.MaxHealth)` | `HealthLost` is a total. "How close did I come" is a minimum, and a wave that lost 60% in one bite and a wave that lost 60% in six are the same number today. |

All seven are also honest additions to `RunRecorder`/`RunReport` — the run log gains "what your shield actually did" and "how close you came", which the report currently cannot say.

### 2. The account ledger: twenty bounded keys, three new save fields

§29: *"Do not store full combat history when a bounded counter is sufficient."* The ledger is **one dictionary with a closed key set declared by the catalogue**, not twenty save fields.

### New `SaveGame` fields (three)

| Field | Type | Meaning |
|---|---|---|
| `DiscoveredTraits` | `List<string>` | Account-wide (§25, LAW 6). Monotone: nothing ever removes an id. |
| `TraitLoadouts` | `List<SavedTraitLoadout>` | `{ CharacterId, List<string> TraitIds }` — max 3 ids (§26, LAW 7). The one piece of per-character state this refactor adds. |
| `TraitTally` | `Dictionary<string,double>` | The twenty accumulators below. Keys not in the catalogue are dropped on load. |

Optional fourth, cheap, for §33: `TraitProvenance : List<SavedTraitFirst>` = `{ TraitId, CharacterId, RegionId, Wave }`, written once per discovery. Enough for "FIRST AWAKENED BY THE SEEKER IN CINDERWORKS" and nothing more — not a historical database.

**Hazard the audit already named:** `Game1.Save()`'s `with` block attaches 38 of the 58 fields; a field added to `SaveGame` but not to that block serialises at its default forever, and autosave fires every 10 s. All three (four) fields must be added to the `with` block in the same commit.

### The twenty tally keys

| Key | Unit | Fed by | Traits that read it |
|---|---|---|---|
| `heavy_hits` | count | M1 | DEEP CUT |
| `overkill_pools` | ÷ `champ.MaxHealth` | M2 | THE SPILL (8), CARRION WEIGHT (25) — a ladder |
| `shield_gained_pools` | ÷ pool | M3 | THE ANSWERING WALL |
| `shield_breaks` | count | M4 | SCAR TISSUE |
| `shield_absorbed_pools` | ÷ pool | `ShieldAbsorbed` (exists) | STANDING PLATE |
| `healed_pools` | ÷ pool | M5 | PRACTISED FLESH (25), THE GIVEN HAND (50) — a ladder |
| `reflected_pools` | ÷ pool | M6 | THE MIRROR |
| `brink_waves` | count | M7 ≤ 0.20, wave cleared | LAST BREATH (25), THE THIN LINE (60) — a ladder |
| `untouched_waves` | count | `HealthLost == 0`, cleared | THE UNBROKEN THREAD |
| `hits` | count | `Hits` (exists) | DRUMBEAT |
| `sparse_clears` | count | cleared with `Hits <= 12` | THE LONG SWING |
| `wide_waves` | count | cleared carrying a skill whose `shape.TargetsFor(def) >= 3` | SPREADING FIRE |
| `high_crit_waves` | count | cleared with `BuildContext.CritPercent >= 35` | THE CERTAIN HAND (30), THE OPENED VEIN (100) — a ladder |
| `creatures_killed` | count | `CreaturesKilled` (exists) | SETTLING WEIGHT |
| `sign_casts` | count | `StyleActivations[Style.Sign]` (exists) | THE LINGERING MARK |
| `long_field_waves` | count | cleared, `DurationMs >= 15000`, a `SkillKind.Field` equipped | THE SETTLED GROUND |
| `ManyVowWaves` | count | a cleared wave with `Vows.KeptCount(build.Vows, ctx)` at two or more | THE WEIGHT OF VOWS |
| `pure_waves` | count | every woven skill one `Source` (the `oneSource` local, :745) | THE SINGLE NOTE |
| `motley_waves` | count | `BuildContext.DistinctSources >= 4` | MANY TONGUES |
| `shield_carried_waves` | count | cleared with `ShieldAbsorbed > HealthDamage` | THE RETURNED BLOW |

### Six traits need NO new accumulator — they read save fields that already exist

`LAST WORD` ← `BossesFelled`. `THE KEPT WORD` ← `RunsWithVowKept`. `THE MATCHED SUIT` ← `CompletedSets`. `HOMEGROUND` ← `ConqueredRegions`. `THE STUDIED PLACE` ← `RegionFarms` mastery. `WHAT KILLED YOU` ← `RunLog` → `RunReport.WallArchetype`.

That is a migration gift, not a coincidence: **those six, and only those six, can be discovered retroactively on the very first load of the new build** (§96 allows explicit mappings). Everything else starts at zero and is earned honestly, which is exactly "do not automatically unlock all new Traits because the old tree was progressed."

### 3. The catalogue — thirty traits, one sentence each, with its dial and its single read site

Every dial below is a field of the new `TraitRules` record unless marked. Neutral default on every one, so an unequipped trait is invisible (the `SkillShape` house rule). Line-number anchors are `src/IdleXIdle.Core/Builds/SoloBattle.cs` on `feat/hunter-cutout-rig`.

| # | id | Name | One-line effect (player copy) | Tag | Dial(s) | The single read site | Wave state it needs |
|---|---|---|---|---|---|---|---|
| 1 | `t_deep_cut` | DEEP CUT | "A hit that takes a fifth of an enemy's health also strips 12 armour from it for the rest of the wave." | heavy hits | `HeavyHitShare`, `HeavyHitArmourStrip` | `LandOn`, after `target.Health -= dmg` (~:1194), beside the existing MACHINE-signature strip | none — `WaveCreature.Defense` is already settable and wave-scoped |
| 2 | `t_long_swing` | THE LONG SWING | "The longer a skill's cooldown, the harder it lands: +4% for every beat it waits." | heavy hits | `PowerPerCooldownBeat` | `Amp`, inside the `skillDef is not null` region (~:1005), reading `skillDef.Beats` | none |
| 3 | `t_spill` | THE SPILL | "Damage wasted past a kill comes back to you as shield, a quarter of it." | overkill | `OverkillToShield` | `LandOn` death branch, at `var spill = -target.Health;` (~:1257) — calls the existing `GrantShield` | none |
| 4 | `t_carrion_weight` | CARRION WEIGHT | "Damage wasted past a kill bleeds into the rest of the wave." | overkill | `OverkillToBleed` | Same line as #3; adds to the standing `poison` pool | none — `poison` already exists |
| 5 | `t_drumbeat` | DRUMBEAT | "Every hit you land this wave makes you hit 0.5% harder, up to a fifth." | multi-hit | `PowerPerHitLanded`, `PowerPerHitCap` | `Amp` (~:1017, beside `PerCreatureBonus`) | one new wave-local `landedHits`, incremented in `LandOn` |
| 6 | `t_spreading_fire` | SPREADING FIRE | "A cast that already reaches three enemies reaches one more." | multi-hit | `WideCastAtTargets`, `WideCastExtraTargets` | `LandSpread`, first line after the `targets <= 0` guard (~:1288) | none |
| 7 | `t_certain_hand` | THE CERTAIN HAND | "Your first hit of each wave always critically strikes, and strikes twice as hard as a critical usually would." | crit | `FirstHitOfWaveCritMultiplier` | `LandOn`, inside the existing `if (fromSkill)` crit block (~:1116-1141) | one new wave-local `firstSkillHitSpent` |
| 8 | `t_opened_vein` | THE OPENED VEIN | "Your critical hits leave the enemy bleeding." | crit | `CritToBleed` | `LandOn`, one line after the crit block, using the wave-local `critChance` | none — feeds `poison` |
| 9 | `t_scar_tissue` | SCAR TISSUE | "When your shield breaks, the next shield you gain this wave is half again as large." | shield | `ShieldAfterBreakBonus` | `GrantShield` (:818-824) — the one funnel | one wave-local `shieldBrokenPending`, set beside the `ShieldBroken` event |
| 10 | `t_standing_plate` | STANDING PLATE | "Shield you are still holding when a wave ends is carried into the next one." | shield | `ShieldCarriesWaves` | `champ.ResetShield()` at the top of `ResolveWave` (:801) becomes conditional | none |
| 11 | `t_answering_wall` | THE ANSWERING WALL | "While you hold a shield, every enemy that bites you takes a tenth of its own bite back." | shield | `ShieldedReflectFraction` | The THORNS block in the bite path (~:2325) — one added term on the existing fraction | none |
| 12 | `t_last_breath` | LAST BREATH | "Below a quarter health your skills come back a third sooner." | low health | `LowHealthShare`, `LowHealthRateBonus` | `RateNow()` (:884-886), which is already re-read at every cast and swing | none |
| 13 | `t_thin_line` | THE THIN LINE | "The first bite that would kill you each wave deals half instead." | low health · survival | `LethalBiteShare` | The bite path, immediately before `champ.Health -= healthLost` (~:2317) | one wave-local `lethalSpared` |
| 14 | `t_practised_flesh` | PRACTISED FLESH | "Every heal widens this wave's healing limit a little." | healing | `HealCeilingPerHeal`, `HealCeilingPerHealCap` | `Heal`, after `healedThisWave += landed` (~:1406) | none — `healBudget` is already a wave-local |
| 15 | `t_given_hand` | THE GIVEN HAND | "Healing you cannot use at full health is thrown at the enemy instead." | healing | `OverhealToDamage` | `Heal`, at the exact line NATURE's `OverhealToShield` is already read (`amount > room`, ~:1400) | none |
| 16 | `t_mirror` | THE MIRROR | "Every bite you reflect makes the next reflection this wave stronger, up to double." | reflect | `ReflectRampPerBite`, `ReflectRampCap` | The THORNS block (~:2325) | one wave-local `bitesTaken` (the existing `bites` only counts trap arms) |
| 17 | `t_returned_blow` | THE RETURNED BLOW | "A bite your shield eats whole is returned to the enemy in full." | reflect · shield | `ReturnFullyAbsorbedBite` | Inside the existing `if (absorbed > 0f)` block (~:2292), placed BEFORE the THORNS block so its `if (alive == 0) return Kill(ms)` covers it | none |
| 18 | `t_settling_weight` | SETTLING WEIGHT | "Every enemy that has died this wave makes your hits 6% heavier, up to a third." | kill chains | `PowerPerDeadEnemy`, `PowerPerDeadCap` | `Amp` (~:1017), reading the existing wave-local `deadThisWave` | none |
| 19 | `t_last_word` | LAST WORD | "You deal 30% more to the last enemy left alive in a wave." | kill chains | `LastEnemyBonus` | `Amp` (~:1017), `alive == 1` — `alive` is already a wave-local | none |
| 20 | `t_lingering_mark` | THE LINGERING MARK | "Your amplify window lasts a second longer for every enemy still alive." | persistent | `AmplifyWindowPerEnemyMs` | The SIGN **cast** branch where `window` is computed (~:1727-1763). The standing field mark keeps its own clock — stated on the card. | none |
| 21 | `t_settled_ground` | THE SETTLED GROUND | "A field that has been running for ten seconds deals half again as much." | persistent | `FieldRampAfterMs`, `FieldRampBonus` | The Field damage line (~:1660), one factor on `aura` | none — `ms` is wave-relative already |
| 22 | `t_kept_word` | THE KEPT WORD | "If you have sworn no vows, the weakest vow you have found still holds you, and still pays." | vows | `UnswornBuildBorrowsWeakestVow` | `BuildComposer.Compose` — the only place that sees both what the account has FOUND and what the build has SWORN; the loan joins `Build.Vows` and every price and payment site bills it like a sworn one. | none |
| 23 | `t_weight_of_vows` | THE WEIGHT OF VOWS | "Each vow you are keeping after the first makes all of your vows pay 20% more." | vows | `VowPayPerExtraKeptVow` | `SoloBattle` — the line after `Vows.CombinedFactor`, scaling what the vows PAY (never the damage), outside the combined ceiling. | none |
| — | — | *(RE-AUTHORED 2026-09-04.* Both were written against the retired per-slot vow model. THE KEPT WORD survives as a build-level rule — it pays a hunter who has sworn nothing at all. THE PRICE PAID was DELETED: paying for a broken vow contradicts the law that restriction buys power, and grew worse with several build-level vows at once. `BrokenVowShare` and `Vows.CombinedFactor`'s `brokenShare` parameter went with it.*) | | | | | | |
| 24 | `t_single_note` | THE SINGLE NOTE | "If every skill you carry shares one source, your first cast each wave strikes twice." | source | `OneSourceRepeatsFirstCast` | The cast damage path, after `LandSpread` (~:1971), mirroring AFTERIMAGE exactly; reads the existing `oneSource` and `firstCast` | none |
| 25 | `t_many_tongues` | MANY TONGUES | "Carrying four different sources makes every matchup a strong one." | source | `AllMatchupsStrongAtSources` | `Amp`, the existing `shape.AllMatchupsStrong` line (~:918) gains one `||` | one wave-local `distinctSources`, computed beside `oneSource` (:745) |
| 26 | `t_matched_suit` | THE MATCHED SUIT | "The element of the set you wear becomes the element of every skill you carry." | sets | `ForceSkillSource` (`Source?`, on `TraitOutcome` — **composition time, not `TraitRules`**) | `BuildComposer.Compose`, at `build.Equip(new EquippedSkill(resolved, variation?.Source ?? s.Source, vow))` (~:196) | none — the fight is untouched |
| 27 | `t_homeground` | HOMEGROUND | "In a region you have conquered, your first cast each wave has no wait." | region | **Reuses `SkillShape.FreeOpeningCast`** — no new dial | `openingBeat` (~:1687), already live | none |
| 28 | `t_studied_place` | THE STUDIED PLACE | "For every region you have mastered, an enemy that dies leaves the wave biting 3% softer, up to a tenth." | region | `WeakenPerDeadEnemy`, `WeakenPerDeadCap` | Folded into the existing `weakenPerDead`/`weakenCap` wave-locals at their gather (:735-742); the bite path already reads them (~:2205) | none |
| 29 | `t_what_killed_you` | WHAT KILLED YOU | "The kind of enemy that ended your last descent takes 20% more from you." | failure | `GrudgeArchetype` (`Archetype?`), `GrudgeBonus` | `Amp`, in the `against is not null` block (~:1027) — `WaveCreature.Archetype` is already read there by SIEGE | none |
| 30 | `t_unbroken_thread` | THE UNBROKEN THREAD | "While you are at full health, the first bite of each wave is stopped outright." | survival | `PreventFirstBiteAtFullHealth` | The MACHINE-PLATING prevention block (~:2278), one added condition | one wave-local `fullHealthGuardSpent` (must NOT share `platingSpent`) |

**Tally: 38 new dials on `TraitRules`, one composition-time field, one reused dial. 28 new read sites in `SoloBattle`, one in `BuildComposer`, one already live.** Every one is a single line or a single added term inside a block that already exists. Seven traits need one new wave-local each; none needs new per-creature state.

**Two invariants this catalogue deliberately bends, and they must be written down where the rule lives:**

- **STANDING PLATE breaks `ShieldRules`' "wave-local" clause.** `SoloBattle.cs:24-29` says in as many words that an accumulating shield would make standing still the strongest defensive play. It cannot here: shield only enters through `GrantShield`, which is clamped to `ShieldRules.CapFor` = half the pool, and nothing grants outside a fight. `ShieldRules`' remarks must gain the sentence naming this trait as the single exception, or the next reader will "fix" it.
- **THE GIVEN HAND lands damage from inside `Heal()`.** It uses `fromSkill:false, ignoresArmour:true`, so it cannot crit, cannot feed VENOM, and cannot re-poison. It CAN empty the wave; the caller's next `if (alive == 0) return Kill(ms)` catches that, and `Kill()`'s only heal (`HealOnClear`) is reached after `Heal()` has returned, so there is no recursion. This must be a comment at the site.

### 4. The hidden discovery rules — what each reads, and why the rule is the trait

Hidden from the player (§28, §39, LAW 8), deterministic in the engine (§29, §30). No rule reads an RNG draw. No rule can be missed.

| # | Trait | The rule (never shown) | Counters it reads | Lifetime accumulator? | Why the rule IS the trait |
|---|---|---|---|---|---|
| 1 | DEEP CUT | Land 250 hits each worth a fifth or more of the struck creature's maximum health. | M1 `HeavyHits` | **New** — `heavy_hits` | You awaken it by hitting hard enough to matter; what awakens is hitting hard enough to break plate. |
| 2 | THE LONG SWING | Clear 40 waves using twelve landed hits or fewer. | `Hits` (exists) + outcome | **New** — `sparse_clears` | Winning with few enormous blows awakens payment for waiting between them. |
| 3 | THE SPILL | Waste 8 times your maximum health past kills. | M2 `Overkill` | **New** — `overkill_pools` | Waste teaches the body to keep something back. |
| 4 | CARRION WEIGHT | Waste 25 times your maximum health past kills. | M2 (same key, higher rung) | shares `overkill_pools` | The deeper rung of the same lesson: the waste stops falling on the floor and starts falling on the wave. |
| 5 | DRUMBEAT | Land 50,000 hits. | `Hits` (exists) | **New** — `hits` | Rhythm is the only thing 50,000 small hits can teach. |
| 6 | SPREADING FIRE | Clear 200 waves carrying a skill that reaches three or more creatures. | `shape.TargetsFor(def)` at wave start (build-side, no roll) | **New** — `wide_waves` | Committing to reach awakens one more creature of reach. |
| 7 | THE CERTAIN HAND | Clear 30 waves with critical chance at 35% or above. | `BuildContext.CritPercent` (exists) | **New** — `high_crit_waves` | Investing in chance awakens one hit a wave where chance does not apply. |
| 8 | THE OPENED VEIN | Clear 100 waves with critical chance at 35% or above. | same key, higher rung | shares `high_crit_waves` | Once criticals are your whole game, they stop being only damage. |
| 9 | SCAR TISSUE | Have your shield broken 40 times. | M4 `ShieldBreaks` | **New** — `shield_breaks` | The brief's own line: the body remembers what survives. Breaking teaches re-armouring. |
| 10 | STANDING PLATE | Absorb 20 times your maximum health with shield. | `ShieldAbsorbed` (exists) | **New** — `shield_absorbed_pools` | The brief's stated relation: absorbing a great deal awakens something shield-shaped. |
| 11 | THE ANSWERING WALL | Be granted 30 times your maximum health in shield. | M3 `ShieldGained` | **New** — `shield_gained_pools` | Wearing plate long enough awakens plate that answers. |
| 12 | LAST BREATH | Clear 25 waves in which you fell to a fifth of your health or below. | M7 `LowestHealthFraction` | **New** — `brink_waves` | Living at the brink teaches you to act faster there. |
| 13 | THE THIN LINE | Clear 60 such waves. | same key, higher rung | shares `brink_waves` | The deeper rung: the body stops merely acting faster and starts flinching from the blow. |
| 14 | PRACTISED FLESH | Be healed 25 times your maximum health in combat. | M5 `Healed` | **New** — `healed_pools` | Healing a great deal awakens room to be healed more. |
| 15 | THE GIVEN HAND | Be healed 50 times your maximum health in combat. | same key, higher rung | shares `healed_pools` | Past the point where more healing helps, healing has to go somewhere else. |
| 16 | THE MIRROR | Reflect 6 times your maximum health back at enemies. | M6 `ReflectedDamage` | **New** — `reflected_pools` | Reflecting a great deal awakens a deeper reflection. |
| 17 | THE RETURNED BLOW | Clear 30 waves in which the shield took more of the wave than your health did. | `ShieldAbsorbed` + `HealthDamage` (both exist) | **New** — `shield_carried_waves` | A shield that has done the work awakens a shield that hits back. |
| 18 | SETTLING WEIGHT | Kill 2,000 creatures. | `CreaturesKilled` (exists) | **New** — `creatures_killed` | Emptying waves awakens a reward that grows as a wave empties. |
| 19 | LAST WORD | Fell 60 bosses. | `SaveGame.BossesFelled` | **None** | A boss wave is always one creature (`forceSingle: isBoss`). Sixty fights that were only ever the last enemy alive awaken an edge against the last enemy alive. |
| 20 | THE LINGERING MARK | Cast 200 SIGN skills. | `StyleActivations[Style.Sign]` (exists) | **New** — `sign_casts` | Keeping a mark up awakens a mark that keeps itself up. |
| 21 | THE SETTLED GROUND | Clear 40 waves lasting fifteen seconds or more with a Field skill equipped. | `DurationMs` (exists) + equipped `SkillKind.Field` | **New** — `long_field_waves` | A field that has stood a long time awakens a field that pays for standing. |
| 22 | THE KEPT WORD | Finish 8 descents with a vow's demand still met. | `SaveGame.RunsWithVowKept` | **None** | Keeping your word awakens your word covering more of you. |
| 23 | THE WEIGHT OF VOWS | Clear 40 waves keeping two or more vows at once. | `Vows.KeptCount(build.Vows, ctx)` per cleared wave | **New** — `ManyVowWaves` | Carrying more than one promise at once awakens the reward for carrying them. |
| 24 | THE SINGLE NOTE | Clear 60 waves in which every woven skill shared one source. | the `oneSource` wave-local (:745) | **New** — `pure_waves` | Committing to one voice awakens the voice saying it twice. |
| 25 | MANY TONGUES | Clear 60 waves carrying four or more distinct sources. | `BuildContext.DistinctSources` (exists) | **New** — `motley_waves` | Speaking to every element awakens every element answering well. |
| 26 | THE MATCHED SUIT | Complete any element set at five worn pieces. | `SaveGame.CompletedSets` | **None** | The set already decided what you are; this makes your skills agree. |
| 27 | HOMEGROUND | Conquer two regions. | `SaveGame.ConqueredRegions` | **None** | You are not a visitor here any more; the wave does not get its free breath. |
| 28 | THE STUDIED PLACE | Bring one region to full mastery. | `SaveGame.RegionFarms` mastery | **None** | Knowing a place is knowing how its creatures come apart. |
| 29 | WHAT KILLED YOU | End three consecutive descents against the same wall archetype. | `SaveGame.RunLog` → `RunReport.WallArchetype` (last ten, saved) | **None** | The purest statement of the whole system: what beat you three times changed you. |
| 30 | THE UNBROKEN THREAD | Clear 30 waves without losing a point of health. | `HealthLost` (exists) | **New** — `untouched_waves` | Going untouched awakens staying untouched. |

**Naturalness check (§31).** Nine of the thirty are pure play-a-build-for-a-while (1, 2, 5, 6, 7, 8, 20, 21, 24). Seven are pure play-a-DEFENCE (9, 10, 11, 12, 13, 17, 30). Six are world progress a player is already chasing (19, 22, 26, 27, 28, 29). None requires a specific enemy, a specific drop, or a specific moment. Nothing is missable: every accumulator is monotone and every threshold is reachable from any point in the career.

### 5. Liveness scenarios — the same fight, with and without

Harness: the existing `mastery_new_nodes_liveness_test.cs` shape — a fixed `Champion`, a fixed `List<WaveCreature>`, `SoloBattle.ResolveWave` with a seeded `Random`, two `Build`s differing **only** by `Shape.Traits`. New file `tests/unit/IdleXIdle.Core.Tests/Builds/trait_liveness_test.cs`. The bar (LAW: no decorative trait, §101): **damage dealt or health kept must move, measurably, in the stated direction, when the condition is posed.**

| # | Trait | Fixture | How the condition is posed | The number that must move |
|---|---|---|---|---|
| 1 | DEEP CUT | 1 Bruiser, health 40,000, `Defense = 25`; BLOW | BLOW's first landing exceeds 20% of 40,000 | `DeliveredDamage` up ≥5%; `AbsorbedFraction` down; `creatures[0].Defense` lower after wave 1 |
| 2 | THE LONG SWING | BLOW (6 beats) + DRINK (6) vs. the same fixture; 3 waves | Nothing to pose — always on for a slow build | `DeliveredDamage` up ≈24%. Control: SPRAY (4) + PULSE (5) moves ≈16%, strictly less — the ordering is the assertion |
| 3 | THE SPILL | 5 Swarm creatures at 400 health; BLOW at 500 base | The very first blow overkills | `ShieldGained` > 0 (was 0); `HealthLost` down |
| 4 | CARRION WEIGHT | Same as #3 | Same | `DeliveredDamage` up; `DurationMs` down |
| 5 | DRUMBEAT | 5 creatures, SPRAY + MIRE, 20 s wave | ≥40 hits land | `DeliveredDamage` up ≥8%, and the second half of the wave's damage strictly greater than the first |
| 6 | SPREADING FIRE | 5 Swarm; PULSE (whole wave) + SPRAY (5 targets) | SPRAY already reaches 3+ | `TargetsStruck` up by exactly one per activation; `DeliveredDamage` up |
| 7 | THE CERTAIN HAND | 1 Bruiser; BLOW; hunter at 20% crit, `critMult` 2.0 | Wave 1's first skill hit | `DeliveredDamage` up by (`critMult`×2 − `critFactor`) × BLOW's first hit; assert the delta is that exact figure |
| 8 | THE OPENED VEIN | 5 Swarm; SPRAY; hunter at 40% crit | ≥20 hits land | `DeliveredDamage` up; `DurationMs` down; poison pool non-zero at wave end |
| 9 | SCAR TISSUE | MACHINE 3p (`WaveStartShieldFraction = 0.12`) + JAWS/PLATING; a Brittle-band bite sized to break the opening shield in one hit | The first bite exceeds the wave-start shield | `ShieldAbsorbed` up ≥15% across 6 waves; `HealthLost` down |
| 10 | STANDING PLATE | MACHINE 3p; 8 waves; bites too small to spend the shield | Shield survives wave 1 | `champ.CurrentShield` > 0 at wave 2's first tick (0 without); summed `ShieldAbsorbed` up; `HealthLost` down |
| 11 | THE ANSWERING WALL | MACHINE 3p + 5 Swarm biters | Shield held while bitten | `DeliveredDamage` up; `DurationMs` down |
| 12 | LAST BREATH | Brittle band, champion minted at 30% health, BLOW + SPRAY, 6 waves | Health starts below the quarter line | `Activations` up ≥20%; `DeliveredDamage` up |
| 13 | THE THIN LINE | Brittle band, thin champion, a bite sized above remaining health | The lethal bite | Champion survives the wave that otherwise wipes; `Depth` +1 or more |
| 14 | PRACTISED FLESH | DRINK + WILT/SUP, leech-heavy, long wave | The heal budget is exhausted mid-wave | `Healed` up; `HealthLost` net down ≥10% |
| 15 | THE GIVEN HAND | DRINK at full health vs. an easy wave | Healing arrives with no room | `DeliveredDamage` up; `DurationMs` down |
| 16 | THE MIRROR | THORNWALL (`ReflectFraction`) + JAWS, 5 Swarm biters, 5 waves | ≥8 bites taken in a wave | `ReflectedDamage` up ≥40%; `DeliveredDamage` up |
| 17 | THE RETURNED BLOW | MACHINE 3p, bites sized under the shield | A bite the shield eats whole | `DeliveredDamage` up; assert zero movement on a fixture where every bite pierces |
| 18 | SETTLING WEIGHT | 5 Swarm; PULSE | Creatures die one at a time | `DurationMs` down ≥10%; `DeliveredDamage` up |
| 19 | LAST WORD | 1 Bruiser (`alive == 1` from tick 1) | Always | `DeliveredDamage` up 30%; `DurationMs` down. Control: 5 Swarm moves only at the end |
| 20 | THE LINGERING MARK | CALL + BLOW vs. 4 creatures | A CALL cast with 4 alive | `DeliveredDamage` up ≥8%; assert no movement at `alive == 1` |
| 21 | THE SETTLED GROUND | MIRE vs. 1 Bruiser, wave > 20 s | The field passes ten seconds | `DeliveredDamage` up ≥12%; assert no movement in a 6-second wave |
| 22 | THE KEPT WORD | A build that swore NOTHING, lent the weakest found vow at compose time | The lent vow is active | `DeliveredDamage` up; assert zero movement when nothing was lent, and that a build which swore a vow is lent none |
| 23 | THE WEIGHT OF VOWS | THE SINGULAR + THE BAREFOOT, both kept by a two-HAMMER bare-handed build | Two vows kept at once | `DeliveredDamage` up; assert zero movement with one vow, and zero movement when one of the two is broken |
| 24 | THE SINGLE NOTE | Four MACHINE-source skills | `oneSource` true | `DeliveredDamage` up ≥15%; assert zero movement when one slot is swapped to Body |
| 25 | MANY TONGUES | Four skills, four sources, vs. a mixed-source wave | `DistinctSources == 4` | `DeliveredDamage` up (every matchup reads `SourceMatchup.Strong`); assert zero at three sources |
| 26 | THE MATCHED SUIT | 5 MACHINE pieces, four Body-source skills, vs. a wave whose creatures are weak to Machine | The set is complete | `DeliveredDamage` up via the matchup, and the composed `EquippedSkill.Source` is Machine on all four. **Fixture must fix the wave's sources**, or the direction is not determined |
| 27 | HOMEGROUND | Same wave twice: `TraitContext.RegionConquered` false, then true | The region is conquered | Wave 1 `DeliveredDamage` up; `DurationMs` down by the 700 ms opening |
| 28 | THE STUDIED PLACE | 5 Swarm, `TraitContext.RegionsMastered = 3` | The first creature dies | `HealthLost` down ≥8% after the first death; assert zero at `RegionsMastered = 0` |
| 29 | WHAT KILLED YOU | Armoured band; `TraitContext.LastWallArchetype = Armoured` | Always | `DeliveredDamage` up 20%; **and unchanged** on the identical fixture composed of Swarm creatures |
| 30 | THE UNBROKEN THREAD | Champion at full health, 5 waves, no leech | Full health at each wave's opening | `HealthLost` down by one bite per wave; assert zero movement when the champion opens hurt |

Six traits carry a **negative control** as well as a positive one (17, 19, 20, 21, 22, 23, 24, 25, 28, 29, 30 — eleven, in fact). That is on purpose: a conditional trait that fires unconditionally is the same failure as one that never fires, and the tally only catches the second.

### 6. The 51 old nodes — what maps, what dies (§59)

**Answer, stated plainly first: none of the 51 has a clean semantic equivalent among the thirty new traits.** Not one. That is not evasion — it is what the audit's own classification predicts. The 51 divide into 32 STRUCTURAL, 13 COMBAT and 6 OTHER; the 32 are system access (PLAN D9 moves them to owners that are not traits), and the 13 COMBAT nodes are, without exception, flat multipliers — the exact catalogue shape §34 forbids and LAW 9 names. There is nothing there to carry over.

Six nodes have a **spiritual successor** — a new trait that answers the same want by a different verb. The migration copy may say so; the code must not map them.

| # | id | What it did | Verdict | Where its function goes |
|---|---|---|---|---|
| 1 | `socket_2` | +1 keystone socket | **DIES as a trait** | Conquest-count milestone (PLAN D9, §52). Existing owners preserved (§58). |
| 2 | `weave_5` | 4→5 skill slots | **DIES outright** | Removed. §54 makes 2A+2P authoritative; PLAN D8. Migration toast, not silence. |
| 3 | `socket_3` | +1 keystone socket | **DIES as a trait** | As #1. |
| 4 | `vow_study_1` | teaches 2 vows | **DIES as a trait** | Vow discovery by proof (§44). One tutorial vow free (§46). |
| 5 | `vow_study_2` | teaches 2 vows | **DIES as a trait** | As #4. |
| 6 | `vow_study_3` | teaches 2 vows | **DIES as a trait** | As #4. |
| 7 | `vow_binding` | teaches 3 gear-slot vows | **DIES as a trait** | As #4. |
| 8 | `vow_sacrifice` | teaches 4 sacrifice vows | **DIES as a trait** | As #4. |
| 9 | `ledger` | pure gate; its NAME lies about Forge | **DIES outright** | Nothing to move — Forge already opens on chests or two owned items (`Unlocks.cs:127`). Delete the lying name. |
| 10 | `filter_common` | auto-sell floor = Common | **DIES as a trait** | Warren facility (§56, LAW 13). Owners preserved. |
| 11 | `filter_uncommon` | auto-sell floor = Uncommon | **DIES as a trait** | As #10. |
| 12 | `forge_insight` | pure gate | **DIES outright** | Nothing to move. |
| 13 | `efficient_forge` | salvage ×1.15 | **DIES** (PLAN D9) | An economy rate, not a characteristic. If it is missed, it belongs to a Warren facility — never a trait. |
| 14 | `auto_merge` | auto-merge on open | **DIES as a trait** | Warren automation (LAW 13). Owners preserved. |
| 15 | `recall_1` | +5% region mastery rate | **DIES** (PLAN D9) | A progression rate. No trait equivalent; LAW 12. |
| 16 | `recall_2` | +5% | **DIES** | As #15. |
| 17 | `recall_3` | +5% | **DIES** | As #15. |
| 18 | `recall_4` | +5% | **DIES** | As #15. |
| 19 | `ks_glass_cannon` | learns GLASS CANNON | **DIES as a trait** | Keystone discovery ← region conquest (§50, LAW 11). Keystone identity preserved. |
| 20 | `ks_bloodlust` | learns BLOODLUST | **DIES as a trait** | As #19. |
| 21 | `ks_blood_magic` | learns BLOOD MAGIC | **DIES as a trait** | As #19. |
| 22 | `ks_reaper` | learns REAPER | **DIES as a trait** | As #19 (region mastery tier). |
| 23 | `ks_rend` | learns REND | **DIES as a trait** | As #19. |
| 24 | `ks_ironclad` | learns IRONCLAD | **DIES as a trait** | As #19. |
| 25 | `ks_juggernaut` | learns JUGGERNAUT | **DIES as a trait** | As #19. |
| 26 | `ks_undying` | learns UNDYING | **DIES as a trait** | As #19. |
| 27 | `ks_titan` | learns TITAN | **DIES as a trait** | As #19. |
| 28 | `ks_dynamo` | learns DYNAMO | **DIES as a trait** | As #19. |
| 29 | `ks_lodestone` | learns LODESTONE | **DIES as a trait** | As #19. |
| 30 | `ks_capacitor` | learns CAPACITOR | **DIES as a trait** | As #19. |
| 31 | `ks_greed` | learns GREED | **DIES as a trait** | As #19. |
| 32 | `ks_discerning_eye` | learns DISCERNING EYE | **DIES as a trait** | As #19. |
| 33 | `ks_fortune` | learns FORTUNE | **DIES as a trait** | As #19. |
| 34 | `ks_hoarder` | learns HOARDER | **DIES as a trait** | As #19. |
| 35 | `ks_echo` | learns ECHO | **DIES as a trait** | As #19. |
| 36 | `ks_venomancer` | learns VENOMANCER | **DIES as a trait** | As #19. |
| 37 | `artifice_vows` | every sworn vow's bonus ×1.25 | **DIES** — no clean equivalent | *Successor:* **THE KEPT WORD** (#22) answers the same want — "I have committed to vows, pay me for it" — but as a rule, not a percentage, and it is discovered by the very behaviour this node's buyers were already performing. `SkillShape.VowPowerMultiplier` itself STAYS ALIVE — THE OATHBOUND reads it. |
| 38 | `ks_weaver` | learns WEAVER | **DIES as a trait** | As #19. |
| 39 | `ruin_edge_1` | Damage ×1.05 | **DIES** | *Successor:* the mastery tree already owns damage scaling. §34's named anti-pattern verbatim. |
| 40 | `ruin_edge_2` | Damage ×1.06 | **DIES** | As #39. |
| 41 | `ruin_edge_3` | Damage ×1.12 | **DIES** | As #39. |
| 42 | `aegis_skin_1` | Health ×1.05 | **DIES** | *Successor:* training ranks and gear own health. |
| 43 | `aegis_skin_2` | Health ×1.06 | **DIES** | As #42. |
| 44 | `aegis_skin_3` | Health ×1.12 | **DIES** | As #42. |
| 45 | `avarice_purse_1` | Haul ×1.05 | **DIES** | *Successor:* GREED / DISCERNING EYE / HOARDER keystones and gear own haul. |
| 46 | `avarice_purse_2` | Rarity ×1.06 | **DIES** | As #45. |
| 47 | `avarice_purse_3` | Haul ×1.08, Rarity ×1.05 | **DIES** | As #45. |
| 48 | `artifice_hands_1` | SkillRate ×1.05 | **DIES** | *Successor:* **LAST BREATH** (#12) is the only cadence trait, and it is conditional on where you are standing rather than a flat rate. |
| 49 | `artifice_hands_2` | SkillRate ×1.06 | **DIES** | As #48. |
| 50 | `artifice_hands_3` | SkillRate ×1.12 | **DIES** | As #48. |
| 51 | `attunement` | nothing (one subtitle string) | **DIES outright** | It already did nothing. |

**Summary: 0 clean equivalents. 32 structural nodes move to non-trait owners (their CAPABILITY is preserved per §58; their node is not). 19 nodes die outright as flat numbers, dead gates or decoration. 6 have a named spiritual successor the migration copy may cite and the code must not map.**

This is the §59 answer the brief expects: *"Because the new Trait system is fundamentally different, exact tree topology does not need to survive."* Nothing here is dropped to save work — every one of the 13 COMBAT nodes was checked against the thirty, and each is a percentage that LAW 9 forbids a trait to be.

### 7. Laws 5–9, audited against this catalogue

| Law | How this design satisfies it | Where it would fail if implemented carelessly |
|---|---|---|
| **LAW 5 — discovered, not bought** | There is no cost field on a trait, no currency reads it, and `TraitDiscovery` is the only writer of `DiscoveredTraits`. `Career.TraitPointsEarned` and its one gate consumer are deleted (PLAN D6). | If any screen offers a trait for Dust. The Traits screen must have no spend affordance at all. |
| **LAW 6 — account-wide discovery, per-character loadout** | `DiscoveredTraits` is a flat account list; `TraitLoadouts` is keyed by `CharacterId`. Switching character reads a different row and touches the discovery list not at all. | If the loadout were stored on the account, or the discovery on the character — the latter would be the ten-parallel-careers failure §25 names. |
| **LAW 7 — exactly three per character** | `SavedTraitLoadout.TraitIds` is clamped to 3 at restore and at every mutation, **below the UI**, exactly as the duplicate-skill rule is (`PlayerLoadout.cs`, §14's precedent). A duplicate trait id in one loadout is rejected on the same path. | If the cap lived only in the screen. Nothing in this design gives a trait a downside to price it (§35, and none of the thirty carries one) — the three slots ARE the cost, so the cap has to be real. |
| **LAW 8 — deterministic but hidden** | Every rule in §4 reads a counter or a build fact; none reads `rng`. Nothing in the trait UI model exposes a threshold, a progress figure or a counter — the view model for an undiscovered trait must literally be `{ Id: null, Known: false }`, so there is no field a screen COULD leak. Tested: `test_unknown_trait_view_model_carries_no_criteria`. | If the inspector or a tooltip carried the rule text "for debugging". §40 forbids developer hints; the way to guarantee it is to not put the rule in the view model at all. |
| **LAW 9 — traits are not Mastery 2** | Zero of the thirty is a bare multiplier on damage, health, rate, haul or rarity. Every one is a rule with a condition: a threshold, a wave position, a state (shield held, full health, one source), a world fact, or a per-hit relationship. The catalogue adds **no** `BuildMods` contribution — traits reach the fight only through `SkillShape.Traits` and one composition-time source override. | The two closest calls are THE LONG SWING (#2) and LAST WORD (#19): both are multipliers, but both are gated on a real build axis (cadence; last enemy alive), and LAST WORD is the brief's own worked example. If a later trait is added that reads no condition, it belongs on the mastery tree instead. |

### 8. What this costs, and the order to build it in

**Surface, counted honestly:**

- 7 new `WaveMetrics` fields, 8 write sites.
- 1 new `SkillShape` member (`TraitRules Traits`), 1 line in `SkillShape.Combine`.
- 1 new record `TraitRules` with 38 fields and its own `Combine` (multipliers multiply, additives add, flags OR, thresholds take the kinder value — the same fold rule `SkillShape.Combine` already documents).
- 28 new read sites in `SoloBattle`, one line each; 7 new wave-locals.
- 1 new read site in `BuildComposer` (the source override) plus one new parameter.
- 3 (or 4) new `SaveGame` fields, all of which must also be added to `Game1.Save()`'s `with` block.
- 1 new `TraitCatalogue` (30 authored records), 1 `TraitEffects` composer, 1 `TraitDiscovery` evaluator with 2 call sites.
- 30 liveness tests, 11 of which carry a negative control.

**Ship order inside PLAN Phase 4.** P4 is already "delete the tree, discovery engine, catalogue, per-character slots, new screen". Split the catalogue so the phase lands:

- **P4a — the spine.** `TraitRules`, `TraitCatalogue`, `TraitEffects`, `TraitDiscovery`, the save fields, the seven metrics, the screen, plus the **six no-accumulator traits** (19, 22, 26, 27, 28, 29). These need no new tally and are retroactively discoverable on first load, so P4a ships a system that is immediately non-empty for an existing save — which is the only way the new screen can be screenshot-tested (§109) at all.
- **P4b — the eleven counters.** The seven metrics feed twenty keys; add them and the remaining 24 traits in two commits grouped by read site (the `Amp`/`LandOn` group, then the shield/bite/heal group).

**Documentation debt this creates (§113):** `ShieldRules`' wave-local remark (STANDING PLATE), `Heal`'s remark (THE GIVEN HAND lands damage), `VowFactor`'s remark (its two early returns are now conditional), and `SkillShape`'s class remark (a field that stops being read must be deleted — now true of `TraitRules` too).

**Open questions this design leaves to the implementer**

- THIRTY OR TWENTY-TWO. The catalogue is at the top of the brief's 24-30 range and costs 38 dials and 28 read sites. Trimming to 24 means dropping one of each doubled pair; the cheapest six to cut, in order, are THE LONG SWING (#2, closest to a bare multiplier), DRUMBEAT (#5, needs a new wave-local for a ramp SETTLING WEIGHT already provides), THE RETURNED BLOW (#17, the third shield trait), THE SETTLED GROUND (#21), MANY TONGUES (#25) and THE STUDIED PLACE (#28). Cutting all six still covers all fifteen behaviours the brief lists, at one trait per behaviour instead of two. The case for keeping thirty is that three equipped slots out of twenty-four is a thin collection to choose from.
- TRAITRULES AS A SUB-RECORD, OR 38 FIELDS ON SKILLSHAPE DIRECTLY. This design puts them behind one member, citing SkillCatalogue.cs:130-141's own precedent for SkillRules. The cost is one extra hop at every read site (shape.Traits.X) and a second Combine to keep in step. The alternative is flat on SkillShape, which reads more directly at the site and takes the record past 125 fields.
- STANDING PLATE (#10) BREAKS A DOCUMENTED INVARIANT. ShieldRules' remark says a shield that accumulated while nothing was happening would make standing still the strongest defensive play. The trait is bounded (grants only happen mid-fight, and the cap is half the pool), but it is a real exception and needs the user's sign-off before the remark is edited. The alternative is to cap the carry — 'you carry HALF the shield you were still holding' — which is a weaker sentence but leaves the invariant nearly intact.
- THE MATCHED SUIT (#26) TAKES THE PLAYER'S SOURCE CHOICE AWAY. Forcing every skill to the set's element is the most build-changing trait here, and it interacts with PURE, THE SINGLE NOTE, the six Source signatures and every matchup at once. It may be too strong or too flattening. The narrower version is the one first drafted: '+20% to skills that share the element of your completed set', which is a plain conditional multiplier and duller.
- REFLECT NEEDS TWO WRITE SITES (M6). Every other new metric has exactly one. The alternative is a bool parameter on LandOn threaded from the THORNS loop and the trap payout, which is one write site but a wider signature on the fight's hottest method. Two write sites was chosen; the reverse is defensible.
- SIX TRAITS DISCOVERED RETROACTIVELY ON FIRST LOAD. LAST WORD, THE KEPT WORD, THE MATCHED SUIT, HOMEGROUND, THE STUDIED PLACE and WHAT KILLED YOU read save fields that already exist, so an established account awakens some or all of them the moment the build loads. That is a good welcome and a bad reveal: §32 asks for a meaningful awakening, not six at once. Either stagger them across the first six cleared waves, or show one combined 'SIX TRAITS HAVE AWAKENED' plate. Needs a call.
- WHAT KILLED YOU (#29) READS THE LAST THREE RUNS, AND RUNLOG HOLDS TEN. If the player switches region between deaths the three consecutive wall archetypes may span three different regions, which reads slightly oddly ('the Armoured of Cinderworks changed you, and you are angrier at the Armoured of Marrow Wastes'). Restricting the rule to three consecutive deaths in ONE region is more coherent and considerably slower to earn.
- THE TALLY IS DENOMINATED IN POOLS FOR FIVE KEYS (overkill, shield gained, shield absorbed, healed, reflected). A hunter whose pool grows tenfold over a career earns those thresholds at a constant rate, which is the intent. But the divisor is the pool AT THE TIME, so a build that deliberately shrinks its pool (RECKLESS OFFERING, GLASS CANNON) banks faster. That is arguably correct — the fantasy is proportional survival — but it is a lever a player could farm. The alternative is absolute damage figures, which stop meaning anything at depth.
- WHETHER TRAIT DISCOVERY SHOULD FIRE ON A FAILED WAVE. Every rule above requires a CLEARED wave, matching the skill-progression rule ('a run that dies teaches nothing on its way out', SoloExpedition.cs). WHAT KILLED YOU is the deliberate exception and is evaluated at run END. If failure is meant to teach — and the brief's §36 lists 'failure' as a behaviour to cover — a second failure-fed trait may be wanted, and it would need the wiped wave's metrics recorded, which today they are (Recorder.Record runs before the outcome check) but are never read.

## The VFX Placement Contract — semantic anchors, visual bounds, and one ratio that makes LAW 16 checkable

### 0. What this contract is, and the three measured facts it is built on

This is the design for BRIEF §61–73 and LAWS 14–16, inside PLAN **D10**. It replaces the 18 ad-hoc spawn expressions in `HuntScreen.cs` with a typed profile, a resolver, and a per-frame bounds registry.

**It touches no gameplay dial.** `SkillDef`, `SkillRules`, `SkillVariation`, `Reinforcement` and `SoloBattle.cs` are untouched. The contract reads exactly two fields that already exist and that `SoloBattle` already ignores as presentation: `SkillDef.ClipKey` (the *shape* — which profile) and `SkillDef.FxKey` (the *art* — which strip). `SkillDef.Targets` is read at one spawn site to choose between the single-target and row trap profile; it is already read by `SoloBattle`. **No new dial, no new read site, no liveness suite affected.**

Three facts, measured from the working tree, decide the shape of everything below.

**Fact 1 — the subject's visible height is already normalised; its width is not.** `UiKit.AnimSprite` crops the top pad and fills `box.Height`, so all ten champions render **412 px tall** in `ChampBox` (a 0.9 % spread). Drawn width runs **162 px (Oathbound) to 373 px (Quiver)** — a 2.30× spread. THE SEEKER (267) and THE MAGPIE (302) differ by 13 %. So §71's chosen pair cannot fail a height-based contract, and the real stress pair is Oathbound/Quiver. A scale model based on height is therefore *stable by construction*; a width-based one is not.

**Fact 2 — the effect strips are padded, off-centre, and `VfxPlayer` draws the whole padded frame.** Measured content boxes, union across all 8 frames, as fractions of the 512 frame:

| strip | content W × H (px) | H fraction | W fraction | content centre Y |
|---|---|---|---|---|
| `fx_shield` | 386 × 252 | **0.492** | 0.754 | 0.461 |
| `fx_aura` | 234 × 416 | 0.812 | 0.457 | 0.506 |
| `fx_shield_break` | 466 × 472 | 0.922 | 0.910 | 0.504 |
| `fx_hit` | 416 × 392 | 0.766 | 0.813 | 0.500 |
| `fx_weakhit` | 352 × 338 | 0.660 | 0.688 | 0.494 |
| `fx_death` | 420 × 401 | 0.783 | 0.820 | 0.438 |
| `fx_crit` | 500 × 500 | 0.977 | 0.977 | 0.500 |
| `fx_heal` | 398 × 500 | 0.977 | 0.777 | 0.500 |
| `fx_strike` | 448 × 480 | 0.938 | 0.875 | 0.500 |
| `fx_projectile` | 500 × 238 | 0.465 | 0.977 | 0.502 |
| `fx_mark` | 284 × 284 | 0.555 | 0.555 | 0.496 |
| `fx_transformation` | 452 × 500 | 0.977 | 0.883 | 0.500 |
| `fx_trap` | 500 × 431 | 0.842 | 0.977 | 0.424 |
| `fx_press` | 424 × 315 | 0.615 | 0.828 | 0.495 |
| `fx_weep` | 133 × 496 | 0.969 | 0.260 | 0.516 |
| `fx_wilt` | 448 × 458 | 0.895 | 0.875 | 0.516 |
| `fx_seeker_projectile` | 488 × 138 | 0.270 | 0.953 | 0.500 |
| `fx_magpie_projectile` | 304 × 174 | 0.340 | 0.594 | 0.460 |
| `fx_seeker_strike` | 512 × 512 | 1.000 | 1.000 | 0.500 |
| `fx_magpie_strike` | 442 × 210 | 0.410 | 0.863 | 0.492 |
| `fx_seeker_trap` | 328 × 347 | 0.678 | 0.641 | 0.521 |
| `fx_magpie_trap` | 109 × 75 | **0.146** | 0.213 | 0.511 |
| `fx_seeker_mark` | 398 × 395 | 0.771 | 0.777 | 0.495 |
| `fx_magpie_mark` | 413 × 416 | 0.812 | 0.807 | 0.500 |
| `fx_seeker_transformation` | 410 × 512 | 1.000 | 0.801 | 0.500 |
| `fx_magpie_transformation` | 235 × 378 | 0.738 | 0.459 | 0.500 |

So a `scale` number today does not describe what you see. `fx_shield` at scale 2.6 draws a 270-px frame whose visible dome is **132 px tall — 0.32× the champion**, where §65 asks for 1.10–1.20×. **Relative scale must measure the CONTENT, not the frame.** Once it does, the frame size becomes a *derived* number, and the ratio of that derived size to the strip's native size is exactly LAW 16's question, computed rather than argued.

**Fact 3 — the bounds are already measured; only the API is missing.** `TopPadFraction`, `SidePadFraction`, `BottomPadFraction`, `StripBottomPadFraction` all do a cached alpha scan and are private to the draw path. `ContentPad(Texture2D)` returns a full bounding box and has **zero consumers** — and is wrong for a strip anyway (its left/right come from frame 0 and frame 7 in *texture* coordinates, not the union in *frame* coordinates). Do not rewrite the measurement. Expose it, strip-aware.

### 1. The types — the exact C# shape

New folder `src/IdleXIdle.Game/Vfx/`. `Rectangle` is a MonoGame type, so none of this may live in Core (ADR-001). Every member below is named because a real site consumes it; the consumer census is §4.

```csharp
namespace IdleXIdle.Game.Vfx;

/// <summary>Which figure's visual bounds an effect is measured and placed against.</summary>
public enum VfxSubjectKind { Champion, Creature, EnemyRow }

/// <summary>Where on the subject the effect's CONTENT lands. Four members, edge- or centre-matching.</summary>
public enum VfxAnchor
{
    /// <summary>Content centre on the subject's visual centre.</summary>
    Center,
    /// <summary>Content centre on the subject's visible crown — an overhead sigil.</summary>
    Head,
    /// <summary>Content centre on the subject's visible sole — a flat ring lying on the ground.</summary>
    Underfoot,
    /// <summary>Content BOTTOM on the subject's visible sole — a column that stands on the ground.</summary>
    Standing,
}

/// <summary>Which dimension of the subject's visual bounds RelativeScale multiplies.</summary>
public enum VfxBasis { SubjectHeight, SubjectWidth }

/// <summary>Draw order. GroundUnder + BehindSubject run BEFORE the figures; the other two after.</summary>
public enum VfxLayer { GroundUnder, BehindSubject, OnSubject, Overhead }

/// <summary>Whether +OffsetX means screen-right or "toward the opponent".</summary>
public enum VfxFacing { Fixed, Forward }

/// <summary>Detached resolves once, on its first drawn frame; Pinned re-resolves every frame.</summary>
public enum VfxFollow { Detached, Pinned }

/// <summary>OneShot plays once, fades, dies. Held loops and lives only while re-asserted this frame.</summary>
public enum VfxLifetime { OneShot, Held }

/// <summary>Whether the effect crosses to a second subject across its life.</summary>
public enum VfxTravel { None, ToTarget }

/// <summary>A subject INSTANCE: the kind plus, for a creature, which slot.</summary>
public readonly record struct VfxSubject(VfxSubjectKind Kind, int Slot)
{
    public static readonly VfxSubject Champion = new(VfxSubjectKind.Champion, -1);
    public static readonly VfxSubject EnemyRow = new(VfxSubjectKind.EnemyRow, -1);
    public static VfxSubject Creature(int slot) => new(VfxSubjectKind.Creature, slot);
}

/// <summary>A figure's VISIBLE opaque rectangle this frame, plus which way it faces (+1 right, -1 left).</summary>
public readonly record struct VisualBounds(Rectangle Rect, int Facing);

/// <summary>A strip frame's transparent margins as fractions of the FRAME, union across all frames.</summary>
public readonly record struct ContentBox(float Left, float Top, float Right, float Bottom)
{
    public float Width   => 1f - Left - Right;
    public float Height  => 1f - Top  - Bottom;
    public float CenterX => Left + Width  * 0.5f;
    public float CenterY => Top  + Height * 0.5f;
}

/// <summary>One effect's placement rule. Authored data; five required members, the rest defaulted.</summary>
public sealed record VfxProfile(
    string        Id,               // stable, printed by the debug view and named by tests
    string        AssetKey,         // LITERAL base key; the per-character rule expands it (see §6)
    VfxSubjectKind Subject,
    VfxAnchor     Anchor,
    float         RelativeScale,    // multiple of the subject's VISUAL basis dimension, measured on CONTENT
    VfxLayer      Layer   = VfxLayer.OnSubject,
    float         Fps     = 12f,
    VfxBasis      Basis   = VfxBasis.SubjectHeight,
    float         OffsetX = 0f,     // subject WIDTHS,  + = right, or forward when Facing is Forward
    float         OffsetY = 0f,     // subject HEIGHTS, + = down
    VfxFacing     Facing  = VfxFacing.Fixed,
    VfxFollow     Follow  = VfxFollow.Detached,
    VfxLifetime   Lifetime = VfxLifetime.OneShot,
    VfxTravel     Travel  = VfxTravel.None,
    float         DelaySeconds = 0f,
    int           Frames  = 8);     // DECLARED, never inferred from the aspect — see §7 risk 5

/// <summary>What the resolver produced. Pure data, testable without a GraphicsDevice.</summary>
public readonly record struct VfxPlacement(
    Rectangle Frame,        // where the whole strip frame draws
    Rectangle Content,      // the visible part — what the player actually sees
    Point     Anchor,       // the resolved anchor point, before the offset
    float     NativeRatio); // Frame.Height / native frame height — the §73 / LAW 16 number

/// <summary>Published each frame by the screen so the player can resolve subjects to pixels.</summary>
public interface IVfxBoundsSource
{
    bool TryBounds(VfxSubject subject, out VisualBounds bounds);
}
```

No `Dictionary<string, object>`, no reflection, no scripting. The catalogue is `static readonly VfxProfile[]` plus a `FrozenDictionary<string, VfxProfile>` keyed by id — a dictionary of `VfxProfile`, not of `object`.

**Tint is not in the profile.** It is per-cast (`SourceGlow(source)`, `Steel * breathe`, `colour * level`) and stays a spawn-call argument. Putting it in authored data would need a Source→profile matrix for no gain.

**Rotation is deliberately absent.** Nothing in the repo draws with an angle, and the arena's projectile gap is nearly horizontal (origin y ≈ 683, target y ≈ 550–700 across 700 px ≈ 11°). A `Rotate` member would be a dial with one marginal consumer. Named here so the omission is a decision, not an oversight.

### 2. Resolution — the arithmetic, in order

One pure static function. Every input already exists; nothing here reads global state.

```csharp
public static VfxPlacement Resolve(
    VfxProfile p, VisualBounds s, ContentBox c, int nativeFrameW, int nativeFrameH);
```

1. `basis = p.Basis == SubjectHeight ? s.Rect.Height : s.Rect.Width`
2. `contentDim = p.RelativeScale * basis` — **the visible size, in pixels. This is the number the design argues about.**
3. `frameH = p.Basis == SubjectHeight ? contentDim / c.Height : (contentDim / c.Width) * (nativeFrameH / (float)nativeFrameW)`
4. `frameW = frameH * nativeFrameW / (float)nativeFrameH` — the strip's own aspect, preserved always.
5. `anchor = p.Anchor switch { Center → (s.CenterX, s.Center.Y), Head → (s.CenterX, s.Top), Underfoot → (s.CenterX, s.Bottom), Standing → (s.CenterX, s.Bottom) }`
6. `sign = p.Facing == Forward ? s.Facing : +1`
7. `contentCentre = ( anchor.X + sign * p.OffsetX * s.Rect.Width,`
   `anchor.Y + p.OffsetY * s.Rect.Height + (p.Anchor == Standing ? -frameH * c.Height * 0.5f : 0f) )`
   — the one anchor-specific term in the whole resolver, and it is what makes `Standing` mean "stands on the ground" without a content-dependent magic offset at every call site.
8. `frame = ( contentCentre.X - frameW * c.CenterX, contentCentre.Y - frameH * c.CenterY, frameW, frameH )`
9. `content = ( contentCentre.X - frameW * c.Width * 0.5f, contentCentre.Y - frameH * c.Height * 0.5f, frameW * c.Width, frameH * c.Height )`
10. `NativeRatio = frameH / nativeFrameH`

**The ratio is the enforcement mechanism for LAW 16.** §73's band is 0.75–1.25 and the two ends mean different things, so they get different verdicts:

| ratio | verdict | meaning | action |
|---|---|---|---|
| > 1.25 | **ERROR — build gate fails** | the strip is being magnified; this is `scale = 3.4` wearing a new coat | regenerate the asset (§72, LAW 16, LAW 20) |
| 0.75 – 1.25 | OK | drawn near native | none |
| < 0.75 | WARNING — listed, never fails | the strip is authored larger than it is ever drawn; wasteful, not ugly | batch re-export at 256 (§111) |

The asymmetry is the point: you cannot compensate for a bad asset with a number, because the number *is* the diagnosis.

**`VfxPlayer.BaseUnitPx = 104` and `VfxPlayer.Scale` are deleted.** Relative scale replaces the base unit entirely, and `Scale` has one caller which forces it to 1 while its field default of 4 is dead — a dormant dial by the repo's own definition. With them go the two duplicated sizing formulas at `HuntScreen.cs:3824` and `:3856`, and the hidden coupling the audit named ("correct only while `_vfx.Scale == 1`").

### 3. Visual bounds — where they come from, and who publishes them

**The measurement.** `UiKit` gains one public, strip-aware function assembled from the four scans that already exist and are already cached — no new `GetData` pass:

```csharp
public ContentBox Content(string key) => new(
    Left:   SidePadFraction(key),
    Top:    TopPadFraction(key),
    Right:  SidePadFraction(key),      // symmetric by construction — SidePadFraction's own rule
    Bottom: BottomPadFraction(key));
```

Two cleanups the contract requires because it consumes them:
- `StripBottomPadFraction` and `BottomPadFraction` are byte-identical and **share `_bottomPadCache`** (`UiKit.cs:91`, `:222`, `:336`). Collapse to one `BottomPadFraction(key)`; delete the twin. The latent cross-contamination the audit flagged goes with it.
- `ContentPad(Texture2D)` is **deleted**. Its left/right are wrong for a strip and it has no consumers. `Content(string)` is the real API it was shaped like.

**The subject's bounds.** For a figure drawn through `AnimSprite` into box `B` from strip `k`:

```
top    = B.Y + round(Content(k).Bottom * frameH * sc)     // AnimSprite's own `drop`
height = B.Height * (1 - Content(k).Bottom / (1 - Content(k).Top))
centreX= B.Center.X
width  = round((1 - 2*Content(k).Left) * frameH * sc)
sc     = B.Height / (frameH * (1 - Content(k).Top))
```

**Width and height are measured from ONE reference strip per subject — the idle strip — never from the live clip.** This is load-bearing. `char_seeker_idle` has side pad 132 px and `char_seeker_attack` has 30: the same hunter draws **267 px wide idling and 492 px wide swinging**. A width-derived offset read from the live clip would make every effect jump on every cast. Height is nearly clip-invariant anyway (412 vs 411), so taking it from the reference too costs nothing and buys a bounds value that is a pure function of (reference strip, live box) — computable in `Update` as well as `Draw`.

**The registry, and the fix for two audited defects at once.** `_creatureRect` is written inside `Draw` while every `Play` runs in `Update`, so positions are one frame stale; and every champion-side effect uses the un-pushed `ChampBox` while the champion is drawn at `ChampBox.X + push`, up to 40 px right. Both die from one change:

> **Spawn sites name a subject and a profile. They never compute a pixel. Resolution happens inside the effects pass, against bounds published earlier in the same frame.**

Mechanically: extract `LayoutActors()` and call it as the first statement of `DrawArena`, before any figure is drawn. It computes the champion box (`ChampBox.X + push`), every creature box (the existing row-compression pipeline at `:1873-1905`), and the row's union box, and publishes a `VisualBounds` for each. The three draw paths then *consume* the published boxes instead of computing them. `PublishCreature`, `EnemyPoint` and `EnemyScale` are deleted — every one of their consumers becomes a profile.

Consequences, all of them wanted:
- the lunge is included by construction (the published box *is* the drawn box);
- staleness is gone (published before both effect passes, in the same frame);
- `EnemyPoint`'s `(_rowCentreX, _rowTopY + 110)` magic fallback is gone — an unresolvable subject means the effect **holds** (its clock does not start, reusing the existing negative-`Elapsed` delay mechanism) and resolves on the first frame it can, rather than drawing 110 px below an imaginary row;
- `_champLunge` remains an `Update`-side value, still available.

**Constraint on the refactor:** `tests/unit/IdleXIdle.Core.Tests/Presentation/hunt_screen_feedback_test.cs:81-92` slices `HuntScreen.cs` between the literal strings `"private void DrawComposition"` and `"private void DrawNormalEnemy"`. Those two method names and their relative order must survive the extraction, or a pure rename fails the suite.

**Facing.** `VisualBounds.Facing` is +1 for the champion, −1 for creatures. It is *not* a draw mirror (`ArtFacesLeft` is `const false`; nothing is ever flipped, and that stays true). Its single job is the sign of `OffsetX` under `VfxFacing.Forward`.

### 4. The vocabulary and its consumer census — every member earns its place

The rule the repo enforces on itself: a dial with no consumer is the signature bug. Every enum member below has at least one site in the migration table of §6.

| Type | Member | Real consumers |
|---|---|---|
| `VfxSubjectKind` | `Champion` | 11 profiles: both persistent effects, all four shield transients, heal, death, enemy-bite, projectile origin, aura cast, transformation |
| | `Creature` | 5: weak impact, creature death, boss death burst, mark sigil, skill strike |
| | `EnemyRow` | 1: the trap ring — the only effect whose subject is the pack, not a body |
| `VfxAnchor` | `Center` | 13 profiles |
| | `Head` | 1 today (`cast.mark`, the sigil), +1 in-phase (`state.break`, see §9) |
| | `Underfoot` | 1: `cast.trap` — a flat ring lying on the ground line |
| | `Standing` | 2: `heal.column`, `field.aura` — both today reach for it with a hand-tuned constant |
| `VfxBasis` | `SubjectHeight` | 17 profiles |
| | `SubjectWidth` | 1: `cast.trap` — a row is 900 px wide and one creature tall; height is meaningless for it |
| `VfxLayer` | `GroundUnder` | 1: `cast.trap` |
| | `BehindSubject` | 1: `field.aura` — the audited fix for an opaque interior "hazing the champion it was meant to wrap" |
| | `OnSubject` | 15 profiles |
| | `Overhead` | 1 today (`cast.mark`), +1 in-phase (`state.break`) |
| `VfxFacing` | `Fixed` | 17 profiles |
| | `Forward` | 1: `cast.projectile` origin — and it fixes a live bug, see below |
| `VfxFollow` | `Detached` | 16 one-shots — a spark stays where the blow landed |
| | `Pinned` | 2: `field.aura`, `shield.barrier` — they follow the lunge |
| `VfxLifetime` | `OneShot` | 16 |
| | `Held` | 2: `field.aura`, `shield.barrier` |
| `VfxTravel` | `None` | 17 |
| | `ToTarget` | 1: `cast.projectile` |

**Anchors NOT created, and why.** §63 offers `ActorChest` and `Weapon / Hand`. `ActorChest` is `Center` with `OffsetY ≈ +0.08` — the normalized-offset model already says it, so a fifth enum member would be a synonym. `Weapon / Hand` is explicitly conditional in §63 ("if the current cutout rig supports a reliable semantic anchor"). **It does not.** The live rig is a horizontal flipbook through `AnimSprite`; the bone rig in `src/IdleXIdle.Core/Animation/` has zero production consumers and its justifying premise (angle snapping for hard 1-px edges) belongs to an art direction the project abandoned. Inventing a `Hand` anchor would be a lie about where the hand is. The projectile's origin is instead stated honestly as what it actually is: **the leading edge of the silhouette, at chest height** — `Center` + `OffsetX +0.42` widths, `Facing.Forward`.

That is not a cosmetic restatement. Today the origin is `ChampBox.Right - 40 = 780`, a constant 160 px right of centre for every hunter. THE SEEKER's visible silhouette is 267 px wide, so his right edge is at **x = 753** — today's bolt is born **27 px off his body, in empty air**. Under the contract it is born at `620 + 0.42 × 267 = 732` (inside him), and at `620 + 0.42 × 302 = 747` for THE MAGPIE, 68 px for Oathbound, 157 px for Quiver. One number, correct on ten silhouettes.

**Layers and the batch.** The additive pass splits in two, and only in two:

```
DrawArena:  LayoutActors();
            _vfx.DrawUnder(b);      // GroundUnder, then BehindSubject
            DrawBoss / DrawNormalEnemy / DrawComposition
            DrawChampion
            _vfx.DrawOver(b);       // OnSubject, then Overhead
            DrawCallouts / DrawArenaOverlay
```

Each pass keeps the existing `End() → Begin(Additive, Rasterizer) → … → End() → Begin(AlphaBlend, RestoreRasterizer ?? Rasterizer)` dance and each keeps the early-out when its own layers are empty, so the batch count rises by at most two per frame — well inside the 10–20 `Begin/End` planning budget. Within a pass, sort by `(Layer, Lifetime == Held ? 0 : 1, spawn index)` under `SpriteSortMode.Deferred`, which preserves today's "held sits under the blows".

**The unscissored pass stays unscissored.** `HuntScreen.cs:1659-1660` sets `_vfx.Rasterizer = null` deliberately — a clip edge cut bursts flat and "gave the arena away". Both passes inherit that, and both restore `ArenaRasterizer` so callouts and the overlay keep the arena's edge. The barrier at 424 × 474 px overhangs `ArenaClip` at the top by nothing (dome top y = 430, clip top y = 150) and is covered at the sides by the rail panels drawn after the arena. **Do not re-scissor while widening.**

**`_held` is re-keyed.** It is keyed by asset key today (`VfxPlayer.cs:152`), so two persistent effects sharing a strip collapse into one instance, one position, one tint — it works by luck because today's two keys differ. The new key is `readonly record struct VfxHoldId(string ProfileId, VfxSubject Subject)`. This is not hypothetical hygiene: a per-creature persistent Break sigil (§9) is five subjects on one strip and would collide on the first swarm wave.

### 5. The profile table — all 18, plus the two the contract makes possible

`AssetKey` is a literal base key. Per-character expansion keeps today's `FxFor` rule verbatim: try `fx_{characterId}_{key}_strip8_512`, else `fx_{key}`. **Profile selection**: `VfxProfiles.ForSkill(def)` looks up `def.FxKey` in a small explicit override table first, then falls back to `def.ClipKey`. The override table exists for exactly the skills whose art is a different shape from their clip (WEEP is authored as a falling column, not a bolt).

| # | Profile Id | Asset | Subject | Anchor | Scale | Basis | OffX | OffY | Layer | Facing | Follow | Life | Travel | Fps |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `impact.weak` | `fx_weakhit` | Creature | Center | 0.32 | H | 0 | 0 | OnSubject | Fixed | Detached | OneShot | None | 16 |
| 2 | `impact.bite` | `fx_hit` | Champion | Center | 0.36 | H | 0 | +0.08 | OnSubject | Fixed | Detached | OneShot | None | 14 |
| 3 | `heal.column` | `fx_heal` | Champion | **Standing** | 0.95 | H | 0 | 0 | OnSubject | Fixed | Detached | OneShot | None | 10 |
| 4 | `shield.undying` | `fx_shield` | Champion | Center | **1.15** | H | 0 | −0.02 | OnSubject | Fixed | Detached | OneShot | None | 12 |
| 5 | `death.champion` | `fx_death` | Champion | Center | 0.80 | H | 0 | 0 | OnSubject | Fixed | Detached | OneShot | None | 9 |
| 6 | `death.boss_burst` | `fx_crit` | Creature | Center | 0.65 | H | 0 | −0.06 | OnSubject | Fixed | Detached | OneShot | None | 10 |
| 7 | `death.creature` | `fx_death` | Creature | Center | 0.70 | H | 0 | 0 | OnSubject | Fixed | Detached | OneShot | None | 9 (delay 0.45 s) |
| 8 | `shield.gain` | `fx_shield` | Champion | Center | **1.15** | H | 0 | −0.02 | OnSubject | Fixed | Detached | OneShot | None | 12 |
| 9 | `shield.absorb` | `fx_shield` | Champion | Center | **1.15** | H | 0 | −0.02 | OnSubject | Fixed | Detached | OneShot | None | 16 |
| 10 | `cast.projectile` | `fx_projectile` | Champion | Center | 0.28 | H | **+0.42** | +0.02 | OnSubject | **Forward** | Detached | OneShot | **ToTarget** | 8 |
| 11 | `cast.aura` | `fx_aura` | Champion | Center | 1.05 | H | 0 | 0 | OnSubject | Fixed | Detached | OneShot | None | 12 |
| 12 | `cast.trap` | `fx_trap` | **EnemyRow** | **Underfoot** | 1.00 | **W** | 0 | 0 | **GroundUnder** | Fixed | Detached | OneShot | None | 12 |
| 13 | `cast.mark` | `fx_mark` | Creature | **Head** | 0.45 | H | 0 | −0.10 | **Overhead** | Fixed | Detached | OneShot | None | 12 |
| 14 | `cast.transformation` | `fx_transformation` | Champion | Center | 1.05 | H | 0 | 0 | OnSubject | Fixed | Detached | OneShot | None | 12 |
| 15 | `cast.strike` | `fx_strike` | Creature | Center | 0.55 | H | 0 | 0 | OnSubject | Fixed | Detached | OneShot | None | 12 |
| 16 | `shield.break` | `fx_shield_break` | Champion | Center | 1.20 | H | 0 | −0.02 | OnSubject | Fixed | Detached | OneShot | None | `ShieldBreakFrames / UiMotion.Reward` |
| 17 | `field.aura` | *skill `FxKey`* | Champion | **Standing** | 1.10 | H | 0 | 0 | **BehindSubject** | Fixed | **Pinned** | **Held** | None | 10 |
| 18 | `shield.barrier` | `fx_shield` | Champion | Center | **1.15** | H | 0 | −0.02 | OnSubject | Fixed | **Pinned** | **Held** | None | 8 |
| 19† | `cast.rain` | `fx_weep` | Creature | Head | 0.90 | H | 0 | −0.05 | Overhead | Fixed | Detached | OneShot | None | 12 |
| 20† | `state.break` | `fx_mark` | Creature | Head | 0.30 | H | 0 | −0.12 | Overhead | Fixed | **Pinned** | **Held** | None | 8 |

† §19 is the FxKey override that gives WEEP art that finally draws (its ClipKey is `projectile`, its art is a 133 × 496 falling column — a bolt profile would draw it sideways). §20 is the §69 / D10 persistent state the contract makes possible: `WaveReplay.CreatureBreaks(int)` is already accumulated state that survives a frame with no event and reconstructs exactly under a dev seek, so a held sigil over each broken creature needs **no new replay state** — and it is the second consumer of `Head`, `Overhead`, and the re-keyed `_held`. It is an open decision whether it ships in this phase (§Open Questions).

**Why `shield.gain` and `shield.absorb` are the barrier's geometry.** §106 requires "absorb impact lands on barrier". A ripple offset onto the dome's flank was considered and rejected: the offset is normalized to the *champion's* width (162–373 px) while the dome is a constant 424 px, so the same number lands in a different place on every hunter, and the smaller frame falls out of budget (ratio 0.40). One dome, taking the hit across its whole surface, at 16 fps and low alpha, is simpler, in budget, and correct on ten silhouettes.

**Why the projectile is sized to the caster.** Three candidates: the target (what it used to be — a swarm creature got a 208 px bolt and a boss a 520 px one for the same throw), the gap (what it is now — integer division with four reachable values), or the caster. A bolt is a thing the hunter throws; its size is a property of the throw, not of what it hits or how far it flies. Sizing by the caster gives one bolt, always, and the gap keeps its real job: the travel time.

**Why the field moves to `BehindSubject`.** This is the one deliberate visual change in the table. The audit records the designer seeing "ortasında küçük yanan alev" — the strip's opaque interior hazing the champion it was meant to wrap. Drawn additively behind the figure, the interior contributes nothing where the champion is opaque and glows everywhere he is not: the field reads as a halo and the hunter stays legible. Listed as an open question because it is a change the designer should see, not infer.

### 6. The migration table — every spawn site, its present arithmetic, and the profile that replaces it

All 18 sites are in `src/IdleXIdle.Game/HuntScreen.cs`. "Visible now" and "Visible after" are the *content* height in px at the 100 % density profile — what the player actually sees, computed from the content boxes in §0.

| # | file:line | Present ad-hoc arithmetic | Visible now | Profile | Visible after | What the change fixes |
|---|---|---|---|---|---|---|
| 1 | `:1375` | `EnemyPoint(slot, 0.45f)`; `EnemyScale(slot, 0.6f)` → integer bucket 1–2 → frame 104/208 | 69 or 137 px | `impact.weak` | swarm 78, boss 166, continuous | five buckets → a continuous ratio; stale rect → same-frame bounds |
| 2 | `:1384` | `(ChampBox.Center.X, ChampBox.Center.Y + 40)` = (620, 706); `scale: 2` | 159 px | `impact.bite` | 148 px at (620 + push, 708) | picks up the lunge; `+40` becomes `+0.08` heights |
| 3 | `:1419` | `(620, ChampBox.Bottom - 156)`; `scale: 3` — the `-156` is documented as hand-tuned "so the effect's FOOT sits on the ground line" | 305 px, foot at 877 | `heal.column` | 391 px, foot at **881** | the hand-tuned constant *is* `Standing`; the comment becomes the enum |
| 4 | `:1423` | `(620, ChampBox.Center.Y - 20)`; `scale: 4` | 205 px = **0.50×** | `shield.undying` | 474 px = **1.15×** | UNDYING was a half-height flash of a barrier it never matched |
| 5 | `:1427` | `(620, ChampBox.Center.Y)`; `scale: 4` | 326 px | `death.champion` | 330 px | ~unchanged; gains bounds-relative sizing |
| 6 | `:1441` | `EnemyPoint(slot, 0.55f)`, `dy - 40`; `EnemyScale(slot, 1.2f)` → bucket 5 on a boss | 508 px = 0.98× boss | `death.boss_burst` | 338 px = 0.65× | §65's execute band (0.50–0.70); `-40` becomes `−0.06` heights |
| 7 | `:1442` | same point; `EnemyScale(slot, 0.9f)` → 2–3; `delay: 0.45f` | 163 / 244 px | `death.creature` | swarm 172, boss 364 | delay preserved verbatim as `DelaySeconds` |
| 8 | `:1464` | `(620, 646)`; `scale: 3` | 150 px | `shield.gain` | 474 px | the gain now flares the dome that actually stands there |
| 9 | `:1476` | `(620, 646)`; `scale: 2` | 101 px | `shield.absorb` | 474 px, low alpha, 16 fps | §106: "absorb impact lands on barrier" |
| 10 | `:1594-1596` | origin `(ChampBox.Right - 40, Center.Y + 30)` = (780, 696); `scale: Math.Clamp((tx - ChampBox.Right) / 104, 2, 5)` — **integer division, four reachable values**; `toX/toY` | 97–242 px, origin 27 px off THE SEEKER's body | `cast.projectile` | 115 px always; origin (732, 683) Seeker / (747, 683) Magpie | step function → one size; origin lands *on* every silhouette; travel ease unchanged |
| 11 | `:1599` | `(620, ChampBox.Center.Y + 20)`; `scale: 3` | 253 px = 0.61× | `cast.aura` | 433 px = 1.05× | §65's body-aura band (1.0–1.2) |
| 12 | `:1602` | `(_rowCentreX, EnemyPoint(target, 0.7f).Y)`; `EnemyScale(target, 1.3f)` — positioned by the ROW, sized by one CREATURE | 175–438 px wide | `cast.trap` | 1.00 × row width, on the ground line, under the figures | one coherent statement; `GroundUnder` puts the ring under the pack instead of over it |
| 13 | `:1605` | `(tx, ty)` at 0.45 down the body; `EnemyScale(target, 0.9f)` | 115 / 173 px, in the chest | `cast.mark` | 0.45× height, floating over the crown | an Amplify sigil belongs over the head, not inside the ribcage |
| 14 | `:1608` | `(620, ChampBox.Center.Y)`; `scale: 3` | 305 px | `cast.transformation` | 433 px = 1.05× | §65 body-aura band |
| 15 | `:1611` | `(tx, ty)`; `EnemyScale(target, 1.25f)` → 2–5 | 195–488 px | `cast.strike` | swarm 135, boss 286 | continuous; §65 impact band |
| 16 | `:3877-3878` | `(620, 646)`; `scale: 4`; fps `ShieldBreakFrames / UiMotion.Reward` | 384 px = 0.93× | `shield.break` | 494 px = 1.20× | bursts *outward* from the dome it replaces; fps expression preserved |
| 17 | `:3825-3826` | `(620, (int)(ChampBox.Bottom + 18 - h/2f))` with `h = AuraScale(6.4) × BaseUnitPx` → frame 666 px, **ratio 1.30 OVER budget** | 302 × 541 px, floating from 170 px above the crown to **42 px above the feet** | `field.aura` | 453 px tall, **standing on y = 881**, frame 558, ratio **1.09** | the aura reaches the ground; ratio comes into budget; the `- h/2f` duplicate formula disappears |
| 18 | `:3857-3858` | `(620, (int)(ChampBox.Bottom - 40 - h/2f))` with `h = 2.6 × 104` → frame 270 px | **203 × 132 px — 0.32× the champion**, spanning 39 %–71 % down the torso | `shield.barrier` | **424 × 474 px, 1.15×**, enclosing the figure | the headline defect; see §7 for the numbers on both silhouettes |

**Sites that disappear entirely:** `EnemyPoint` (`:1777`), `EnemyScale` (`:1782`), `PublishCreature` (`:1776`), `AuraScale`, `ShieldShellScale`, `VfxPlayer.BaseUnitPx`, `VfxPlayer.Scale`, and the dead `growTo` / `frameW` parameters on `Play`.

**Sites that stay verbatim:** the travel ease (`Anim.At`), the additive fade tail (`Anim.Fade`), the negative-`Elapsed` delay, the wave-boundary continuity (`BeginWave` still does **not** call `_vfx.Clear()` — `HuntScreen.cs:912-914`), the reduced-motion freeze, and the two `Clear()` calls at `:1238` / `:1640`.

### 7. SHIELD on THE SEEKER and THE MAGPIE — the numbers (§106, §71)

**The subjects, measured from the shipped art.** `char_seeker_idle_strip8_512.png` and `char_magpie_idle_strip8_512.png` are both 4096 × 512 with a 17-row bottom pad; top pads 114 and 102; side pads 132 and 112.

| | THE SEEKER | THE MAGPIE |
|---|---|---|
| visual top y | **469** | **469** |
| visual bottom y | **881** | **881** |
| **visual height** | **412 px** | **412 px** |
| **visual width** | **267 px** | **302 px** |
| visual centre | (620 + push, 675) | (620 + push, 675) |
| silhouette spans x | 486 … 754 | 469 … 771 |

**The barrier profile, in full:**

```
shield.barrier   asset fx_shield   Subject Champion   Anchor Center
RelativeScale 1.15  Basis SubjectHeight
OffsetX 0.00        OffsetY -0.02
Layer OnSubject     Facing Fixed    Follow Pinned    Lifetime Held    Frames 8    Fps 8
```

`OffsetY = −0.02` (−8 px) gives the dome more clearance above the crown than below the sole, because the floor hides the bottom and the head does not.

**Resolved, with the CURRENT asset (`fx_shield`, content H fraction 0.492):**

| step | value |
|---|---|
| content height | 1.15 × 412 = **474 px** |
| frame height | 474 ÷ 0.492 = **963 px** |
| native ratio | 963 ÷ 512 = **1.881×** |
| verdict | **ERROR. The contract refuses to draw it.** |

This is the whole of LAW 16 in one line. The only way to make today's `fx_shield` reach §65's band is to magnify a 512-px frame to 963 — precisely the `scale = 3.4` the brief forbids. **The asset is regenerated, not the number.**

**Regeneration order — `fx_shield`, 8 frames of 512 × 512:**
- content occupies **≥ 0.91 of frame height** (top pad ≤ 0.045, bottom pad ≤ 0.045), vertically centred (content centre Y within 0.49–0.51);
- content aspect **0.85 – 0.95** (width ÷ height). At 0.894 the dome is 424 px wide at 474 tall, which clears the *widest* hunter (Quiver, 373 px) by 25 px per side. Below 0.86 Quiver pokes out of his own shield;
- authored so the ring reads at 16 % alpha additive (it rests at `ShieldShellRest = 0.16`);
- reference: `fx_shield_break` already fills 0.91 × 0.92 of its frame. It is the shape to match.

**Resolved, with the regenerated asset (content H fraction 0.91, W fraction 0.814, centre 0.50/0.50):**

| | THE SEEKER | THE MAGPIE |
|---|---|---|
| basis (visual height) | 412 | 412 |
| content height = 1.15 × 412 | **474 px** | **474 px** |
| frame height = 474 ÷ 0.91 | **521 px** | **521 px** |
| frame width (square frame) | 521 px | 521 px |
| **native ratio** = 521 ÷ 512 | **1.017×** ✅ in the 0.75–1.25 budget | **1.017×** ✅ |
| anchor point (Center, no lunge) | (620, 675) | (620, 675) |
| content centre = anchor + (0, −0.02 × 412) | (620, **667**) | (620, **667**) |
| **destination frame rect** | **(360, 406, 521, 521)** | **(360, 406, 521, 521)** |
| **visible dome** | **424 × 474 px** | **424 × 474 px** |
| dome spans | x 408…832, y 430…904 | x 408…832, y 430…904 |
| clearance above the crown | **39 px** | **39 px** |
| clearance below the sole | 23 px (dips into the floor — a bubble, not a hat) | 23 px |
| clearance per side | **78 px** | **61 px** |
| **layer** | OnSubject (over the figure, low alpha, additive) | OnSubject |
| follow | Pinned — tracks `ChampBox.X + push` through the full 40 px lunge | Pinned |

**The two resolve to the same rectangle. That is the contract working, and it is also §71's real answer:** `AnimSprite` already normalises visible height, so a height-based barrier is identical on THE SEEKER and THE MAGPIE by construction, and **testing only those two proves nothing**. The stress pair is width, and the width test is the enclosure check:

| hunter | visual width | dome width | margin per side |
|---|---|---|---|
| Oathbound | 162 | 424 | 131 px |
| Chorus | 200 | 424 | 112 px |
| Tower | 212 | 424 | 106 px |
| Metronome | 239 | 424 | 93 px |
| **THE SEEKER** | **267** | **424** | **78 px** |
| Anvil | 284 | 424 | 70 px |
| **THE MAGPIE** | **302** | **424** | **61 px** |
| Thornwall | 351 | 424 | 37 px |
| Unbroken | 366 | 424 | 29 px |
| **Quiver** | **373** | **424** | **25 px** — the binding case |

Every hunter is enclosed with margin, so **no runtime width-guard dial is created.** The constraint lives where it belongs: as a build-gate assertion (§8, test 4) and as a clause in the regeneration order. A dial that would never fire is a dormant dial.

**The three sibling shield effects, resolved on the same subject** (all Center, all −0.02, all against the regenerated asset):

| profile | scale | visible | frame | ratio |
|---|---|---|---|---|
| `shield.gain` | 1.15 | 474 px | 521 | 1.017 ✅ |
| `shield.absorb` | 1.15 | 474 px | 521 | 1.017 ✅ |
| `shield.undying` | 1.15 | 474 px | 521 | 1.017 ✅ |
| `shield.break` (`fx_shield_break`, 0.922) | 1.20 | 494 px | 536 | 1.047 ✅ |

All four share the barrier's geometry, so the gain flares it, the absorb shimmers across it, UNDYING flashes it gold, and the break bursts 20 px wider than it — one dome, four moments, and every §106 clause satisfied: *surrounds Seeker, surrounds Magpie, is not a tiny sprite in the torso centre, scales with visual bounds, follows movement (Pinned), the absorb lands on the barrier, the break is positioned on it.*

**Persistence is not touched.** `WaveReplay.CurrentShield` is accumulated state (`WaveReplay.cs:356-365`); `HasShield` survives a frame with no event and reconstructs exactly under a dev seek. §69 asks for this shape and it already exists. The contract changes only *where and how big* the barrier draws, never *whether it knows it should*.

### 8. Asset verdicts — the diagnostic the contract produces on its first run

Running §2's ratio over every profile × every asset it can resolve to × the smallest and largest subject. This is the output of the build gate, not a wish list.

**ERROR — over 1.25×, must be regenerated before the profile ships (LAW 16, §72, LAW 20):**

| asset | profile | worst ratio | why | order |
|---|---|---|---|---|
| `fx_shield` | `shield.barrier` / `.gain` / `.absorb` / `.undying` | **1.881** | content is 0.492 of the frame, 46 % down | 8 × 512², content ≥ 0.91 H, aspect 0.85–0.95, centred |
| `fx_magpie_trap` | `cast.trap` | **4.20 – 8.67** | ring occupies **0.146 × 0.213** of the frame | re-export with the ring ≥ 0.90 of frame width |
| `fx_seeker_trap` | `cast.trap` | **1.40 – 2.88** | ring 0.641 of frame width | as above |
| the other 8 per-character trap strips | `cast.trap` | assume similar | same generator batch | re-export as one family |
| `fx_magpie_strike` | `cast.strike` | **1.36** (boss subject) | content 0.410 of frame height | re-export ≥ 0.85 H |
| `fx_mark` used as a held field (BRAND) | `field.aura` | **1.595** | a sigil is not a field; content 0.555 | BRAND needs field art, or aliases to `fx_aura` |
| `fx_press` used as a held field (PRESS) | `field.aura` | **1.44** | content 0.615 | re-export ≥ 0.85 H |

`fx_wilt` as a held field resolves to **0.99** ✅ — it needs only the alias, not the art.

**WARNING — under 0.75×, listed, never fails.** Almost every creature-side profile: `impact.weak` 0.23–0.49, `cast.strike` 0.28–0.60, `cast.mark` 0.28–0.56, `death.creature` 0.43 on a swarm, `cast.projectile` 0.49 on the shared strip. The pattern is one finding, not thirty: **the fx set is authored at 512 for effects that draw at 80–300 px.** The correct response is §111's actual-size rule — a one-time re-export of the creature-side family at 256 — not a scale change. Downscaling is visually safe, so this never blocks a build.

**Three effects that resolve to no asset at all — fix these FIRST, before anything else is measured.** `PRESS` (`SkillCatalogue.cs:386`), `WEEP` (`:587`) and `WILT` (`:715`) name FxKeys `press` / `weep` / `wilt`. The art is on disk and loaded; `AssetLibrary`'s alias table (`:176-185`) has no entry; `VfxPlayer.Play`/`Hold` return silently on a null texture. PRESS and WILT are 2 of the 4 Field skills and `_auraFxKey` drives the held field, so **a build running PRESS or WILT has no field effect whatsoever.** Add three aliases:

```csharp
["fx_press"] = "fx_press_strip8_512", ["fx_weep"] = "fx_weep_strip8_512", ["fx_wilt"] = "fx_wilt_strip8_512",
```

and delete the orphan `["fx_levelup"]` (§112 — aliased, file present, played by nothing).

**The gate that missed this is fixed by construction.** `tools/check_asset_keys.py:33-39` matches `_vfx.Play` but extracts only string *literals*, so the six `FxFor(def)` sites are invisible to it. Under the contract every asset key is a literal in **one table** (`VfxProfiles.cs`), so the gate stops scanning call sites and starts scanning the table, plus the per-character expansion rule. Silence becomes impossible: a profile whose asset resolves to nothing fails test 3 below.

### 9. The debug view (§70) — what it draws and how it is turned on

**How it is turned on. Three doors, none of them open in a shipped build.**

1. **`F8`**, toggling `HuntScreen.DevVfxDebug`, live only under `RH_DEV=1` — the existing `DevKeysEnabled` gate at `Game1.cs:2806-2808`, beside F6 (force boss) and F7 (layout overlays). F8 is free.
2. **`RH_SHOT_MODE=vfxdebug`**, setting `_expedition.DevVfxDebug = true` at `Game1.cs:2177`, exactly the pattern `bossdebug` already uses — so the capture rig can photograph it.
3. **`RH_VFX_DUMP=1`**, a text dump, one line per resolved effect per frame, to stdout. This is the *primary* artifact: numbers first, picture second.

`DevVfxDebug` defaults to `false` and nothing else writes it. It draws in the **unclipped HUD pass**, after `DrawArena` and before the hover tip, so it is never scissored to the arena and never covers a tooltip.

**What it draws.** Reusing `HuntScreen.DebugRect` (`:2177-2183`), colour-coded by layer so the z-model is legible: GroundUnder verdant · BehindSubject steel · OnSubject gold · Overhead violet.

Per published subject:
- the **visual bounds rectangle**, 1 px steel, labelled `champion 267×412` / `creature 2 · 189×245` / `row 5 · 903×255`;
- a 2 px tick on each edge at the four anchor points, so `Center` / `Head` / `Underfoot` / `Standing` are visible without an effect being live.

Per live effect:
- the **frame rectangle**, 1 px dashed, dim — where the padded 512 frame lands;
- the **content rectangle**, 2 px solid, layer colour — what the player actually sees. Seeing these two apart is what makes Fact 2 obvious at a glance;
- the **anchor point**, a 9 px gold cross;
- a **line from the anchor to the content centre**, labelled with the offset in its own units: `+0.42w  +0.02h`. The normalized-offset model made visible;
- a one-line caption: `shield.barrier · Champion · Center · ×1.15h · ratio 1.02` — the ratio printed **green** in 0.75–1.25, **amber** below, **red** above. LAW 16 as a colour;
- for `Travel = ToTarget`, the from→to segment with the eased head marked.

Header, top-left of the arena: effect count per layer (`ground 1 · behind 1 · on 3 · over 0`) and, if any live effect is over budget, a red banner: `OVER BUDGET: cast.trap fx_magpie_trap 4.20×`.

**The dump format** (`RH_VFX_DUMP=1`), tab-separated so it diffs cleanly and a fixture can assert on it:

```
vfx  frame=412  id=shield.barrier  subj=Champion/-1  anchor=620,675  off=0.00w,-0.02h
     frame=360,406,521,521  content=408,430,424,474  ratio=1.017  layer=OnSubject  held=1
```

This is what makes the contract *checkable without eyeballing a screenshot*, which the capture rig's own history says is the only way these numbers get believed.

**Fixtures for §105 / §71.** Four captures plus their dumps, one per silhouette, with the barrier held:

```
RH_DEV=1 RH_SHOT_MODE=vfxdebug RH_VFX_DUMP=1 RH_SHOT_SHIELDFX=hold RH_SHOT_HUNTER=seeker|magpie|quiver|oathbound
```

Seeker and Magpie because §106 names them; **Quiver and Oathbound because the audit proved they are the pair that can actually fail** (373 px vs 162 px, a 2.30× width spread against Seeker/Magpie's 13 %). Plus two enemy-size captures (a swarm creature at 245 px and the Crystal Lich at 520 px) for the creature-side profiles.

**Baseline first.** PLAN P6 has no test coverage to regress against — nothing in the repo touches `VfxPlayer`, `AnimSprite`, a pad fraction or an actor rectangle. So the *first* commit of this phase adds `RH_VFX_DUMP` to the existing player and captures today's 18 resolved rectangles. Every number in §6's "Visible now" column becomes a recorded baseline before a single profile is written, and the change is then measured rather than asserted.

### 10. Tests — the contract as a build gate

New file `tests/unit/IdleXIdle.Game.Tests/vfx_placement_test.cs` and siblings. `Resolve` is a pure static over `VfxProfile` / `VisualBounds` / `ContentBox` / two ints, so none of this needs a `GraphicsDevice`.

**1. The resolver (`vfx_placement_test.cs`)** — deterministic, no random seeds, no time:
- `test_anchor_center_puts_content_centre_on_the_visual_centre`
- `test_anchor_standing_puts_the_content_bottom_on_the_sole`
- `test_anchor_underfoot_centres_the_content_on_the_sole`
- `test_anchor_head_centres_the_content_on_the_crown`
- `test_offset_x_is_measured_in_subject_widths` — same profile, two subjects 267 and 302 wide, offsets differ by exactly 0.42 × 35 px
- `test_offset_x_flips_when_facing_is_forward` — facing −1 mirrors the sign, facing +1 does not
- `test_relative_scale_measures_content_not_frame` — **the load-bearing case.** Two `ContentBox`es (H fraction 0.49 and 0.91), one profile, one subject: the resolved *content* heights must be equal and the resolved *frame* heights must differ by 0.91/0.49
- `test_native_ratio_is_frame_height_over_native_height`
- `test_an_unresolvable_subject_produces_no_placement_and_does_not_start_the_clock`

**2. The budget gate (`vfx_budget_test.cs`)** — every profile × every asset it can resolve to (including all ten per-character variants) × the smallest subject (swarm, 245 px) and the largest (boss, 520 px; champion, 412 px):
- **fails** if any ratio > 1.25 (LAW 16);
- **prints, never fails** if any ratio < 0.75, so §111's re-export can be scheduled rather than forced.

**3. The silence gate (`vfx_asset_test.cs`)**:
- every `VfxProfile.AssetKey`, and every per-character expansion of it, resolves through `AssetLibrary` — this is the test that would have caught `press` / `weep` / `wilt`;
- every `SkillDef` in `SkillCatalogue.All` maps to a profile through the FxKey-override-then-ClipKey rule;
- no profile id is unreferenced by a spawn site, and no spawn site names an id that is not in the table (the no-dormant-dial rule, mechanised).

**4. The shield gate (`vfx_shield_test.cs`, §106 / §71)** — over **all ten champions**, not two:
- content height ∈ [1.10, 1.20] × visual height;
- content width ≥ visual width × 1.05 — the enclosure clause that replaces the width dial;
- content top ≥ 20 px above the crown;
- native ratio ∈ [0.75, 1.25];
- with the lunge at 0 and at 1, the barrier's centre x equals the *drawn* champion's centre x (the Pinned assertion, and the audited lunge bug as a regression test).

**5. Layer ordering (`vfx_layer_test.cs`)** — the under-pass contains exactly the GroundUnder and BehindSubject effects, the over-pass exactly the other two, and within a pass `Held` precedes `OneShot`. Guards "the shield must not render behind the background or over every HUD element" (§68) as an ordering fact rather than a screenshot.

**6. Manual evidence** — the six captures of §9, filed under `production/qa/evidence/`, which is the ADVISORY gate the coding standards set for Visual/Feel work.

### 11. What the contract deliberately does not do

- **It does not touch Core.** No new `SkillDef` member, no `SkillRules` dial, no `SkillShape` change, no line of `SoloBattle`. The liveness suites cannot be affected, because nothing a variation or reinforcement turns is read here.
- **It does not replace the shield's persistence model.** `WaveReplay.CurrentShield` already does what §69 asks. D10 says copy its shape for Break; `state.break` (profile 20) does exactly that and needs no new replay state.
- **It does not re-scissor the additive pass.** `HuntScreen.cs:1659-1660` disables the arena clip for effects on purpose, after a documented regression. Both new passes inherit that, and both restore `ArenaRasterizer`.
- **It does not invent a Hand or Weapon anchor.** The rig cannot supply one; §63 made that anchor conditional and the condition is false.
- **It does not add rotation, `growTo` or `frameW`.** `growTo` and `frameW` are dead parameters today and are deleted rather than carried forward; rotation has no consumer worth an 11° correction.
- **It does not clear effects at a wave boundary.** `BeginWave` deliberately does not call `_vfx.Clear()` (`HuntScreen.cs:912-914`) because doing so erased the field mid-flight twice a cycle. That stays.
- **It does not resolve `src/IdleXIdle.Core/Animation/{Rig,Clip}.cs`.** That dead rig is a separate decision (ADR-002 still says Accepted while its premise — angle snapping for hard 1-px edges — belongs to the superseded pixel-art direction). The contract only records that it supplies no anchor and therefore cannot justify a `Hand` one.
- **It does not silently widen anything.** Aspect is preserved in every case. The one place the design wanted an aspect break — a width guard on the barrier — was removed once the arithmetic showed it would never fire.

**Open questions this design leaves to the implementer**

- Does the held field move BEHIND the champion (`field.aura` → `BehindSubject`)? The audit records the designer seeing the strip's opaque interior haze the figure it was meant to wrap, and drawing it behind is the fix — but it is a visible change the designer should see rather than infer. Alternative: keep it `OnSubject` and lower its rest alpha, which hides the interior less well.
- The barrier dips 23 px below the ground line (dome bottom y = 904, sole y = 881). Accept it as a bubble the hunter stands inside, or set `OffsetY = -0.075` so the dome bottom sits exactly on the sole — which puts 62 px of clearance above the crown and reads top-heavy.
- Are the ten per-character trap strips re-exported at 8 × 1024 × 512 with the ring filling ≥ 0.90 of frame width (preserving "hepsinin efektinin farklı olması"), or retired to the shared `fx_trap` ring? Re-exporting is ten PixelLab jobs; retiring loses per-character identity on one of five clips. The `Frames` member makes the non-square strip safe either way.
- Does BRAND's held field get its own field art, or alias to `fx_aura`? `fx_mark` as a held field resolves to ratio 1.595 — over budget — because a sigil is not a field. Aliasing is free and correct-shaped; new art is more distinct.
- Does the whole creature-side fx family get re-exported at 256 (§111)? Almost every creature-side profile resolves under 0.75× because the strips are authored at 512 for effects that draw at 80–300 px. It is a warning, never a failure, so this can be scheduled rather than forced — but it is real waste in the 512 MB texture ceiling.
- Does `state.break` (profile 20, the persistent defence-break sigil) ship in this phase? `WaveReplay.CreatureBreaks(int)` already supplies the state, and it is what gives `Head`, `Overhead` and the re-keyed `_held` their second consumer. Deferring it leaves those three members with exactly one consumer each — legal, but thin. Amplify cannot ship with it: `WaveReplay` carries no amplify state, so that one needs replay work first.
- Is the under-budget threshold a warning or a failure? This design makes < 0.75× a printed list and > 1.25× a build failure, on the reasoning that magnification is visible and downscaling is not. A stricter reading of §73 would fail both, which would block the phase on a 256-px re-export of thirty assets.
- Should `VfxTravel` gain a `FromTarget` member for enemy-side casts (a boss throwing at the champion)? No such spawn exists today, so it is deliberately absent — but `VfxFacing.Forward` and `VisualBounds.Facing` are already correct for it, so adding it later is one enum member and one branch.
