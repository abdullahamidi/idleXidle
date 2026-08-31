# Audit — Traits / Prestige (permanent account identity) and its currency

**Date:** 2026-08-31 · **Branch:** `feat/hunter-cutout-rig` · **Scope:** `src/ResonanceHunter.Core/Prestige/*`, `Builds/Keystones.cs`, the earn/spend sites in `src/ResonanceHunter.Game/Game1.cs`, `PrestigeScreen.cs`, persistence, tests under `tests/unit/ResonanceHunter.Core.Tests/Prestige/`, and the two GDDs.
**Method:** every claim below carries a `file:line` that was read; every "dead" claim carries the grep that was run. Read-only on source. `dotnet test --filter "Prestige|PassiveTree|LegacyTrait"` → **93 passed, 0 failed** at time of audit.

---

## 0. Headline

The trait tree is the one system in this codebase whose *runtime* wires are almost all live — `DustEffectsTests` forces every node id to be named in `DustEffects`, and 47 of 51 nodes genuinely reach the sim, the loadout, the forge or the vow menu. The problems are one layer up:

1. **Two currencies live on one object, and the object is named for the wrong one.** `MemoryDustTree` carries the Memory Dust *wallet* (`MemoryDust`, `AwardFromMastery`, `Spend` — spent by the Warren and by Checkpoints) *and* the trait-point *ledger* (`Earned`/`Spent`/`Available`/`Purchase`). Traits are bought with **trait points only**; Dust never touches a node. The class, the namespace (`Prestige`), the screen class, the save field (`MemoryDustUnlocks`), the effects class (`DustEffects`), the GDD file and a dozen doc comments all say Dust.
2. **The tree's central promise is false in the live game.** The design (and `test_two_terminals_are_out_of_reach`) budgets 34 trait points at full content assuming 3 mastery levels per region. `RegionAutomation.MasteryLevel` caps at `FullyMastered` (2) — `OptimizedTeam` "is unreachable by design" (RegionAutomation.cs:38-40). Real ceiling: 6 + 2×5 + 6×2 = **28**. The cheapest terminal path costs **31**. **No player can ever reach any of the four capstones**, the 12-point "REAPER/TITAN/HOARDER/WEAVER" nodes are decoration, and the four notable minors behind them (`*_3`) are unreachable too. The test passes because it re-derives the budget by hand with `MasteryLevel.OptimizedTeam` instead of calling the code.
3. **The liveness guard tests the wrong end.** `DustEffects.WiredIds` counts a node as wired if a method *mentions* its id. Four such methods (`ShowExactNumbers`, `ShowMergePreview`, `TreeComplete`, `KnowsVow`) have **zero callers** in `src/`. The nodes behind them are honest ("Does nothing by itself"), but the guard would pass a lying node just as happily.
4. **The whole spine (46 pts) costs more than a career earns (28).** Spine nodes anchor every road (`socket_2`, `ledger`, `vow_study_1`), so usability and identity compete point-for-point — the "fake choice" the brief warned about is real and, at 28 points, acute.
5. **`design/gdd/memory-dust-prestige-system.md` describes a game that does not exist** (19 unlocks, 660 Dust, roster slots, creature evolution, "prestige"). The live design is `skill-and-trait-trees.md §3.4–3.5`, which itself contradicts its own §4.2 (34 pts/226 cost/15% vs 36 pts/137 cost/26%).
6. **"Trait" is two unrelated things in the player's face:** the permanent tree, and `GearTrait` (an item's prefix, shown as "TRAIT — KEEN" on the character screen). `legacy_trait_migration_test.cs` is about the *gear* one.

---

## 1. Q1 — What currency buys a trait node? Earn → spend trace

### 1.1 The rule in code

| Step | File:line | What happens |
|---|---|---|
| Earn (derived, every frame) | `Game1.cs:3714-3715` | `_mastery.SetEarned(SkillPointsEarned()); _dust.SetEarned(TraitPointsEarned());` |
| Income formula | `Game1.cs:5191-5199` | `total = ConqueredIds.Count + 2 * PeakCorruptionTier; foreach region: total += (int)RegionFarm(id).MasteryLevel` |
| Ledger | `MemoryDust.cs:139-146` | `Earned` (set), `Spent` = Σ cost of owned ids (`:141`), `Available = Earned − Spent` (`:143`) |
| Gate | `MemoryDust.cs:168-174` | `CanUnlock`: exists, not owned, `Available >= Cost`, all `Requires` owned |
| Spend | `MemoryDust.cs:183-189` | `Purchase` adds to `_owned`; "the cost is charged by Spent" — nothing decrements a balance |
| UI trigger | `PrestigeScreen.cs:464-469` | `Buy` → `tree.Purchase(u.Id)`; refusal copy says "NOT ENOUGH TRAIT POINTS" |
| Persist | `SaveGame.cs:533` → `Game1.cs:614` | only `OwnedIds` round-trips; points are not saved (reconstructable — good) |

**Trait points are the only thing that buys a node.** `MemoryDust` (the int wallet) is never read by `CanUnlock`/`Purchase`.

### 1.2 The *other* currency on the same object

| Memory Dust (wallet) | File:line |
|---|---|
| Field + faucet | `MemoryDust.cs:113` `MemoryDust`; `:155` `AwardFromMastery` |
| Faucet: region-mastery levels ×15 × corruption reward | `Game1.cs:2747-2751` |
| Faucet: conquest, `40 × RewardMultiplier` | `Game1.cs:3721` |
| Faucet: corruption deepening (first time) | `Game1.cs:4976` → `CorruptionScaling.DeepeningDustAward` |
| Faucet: Warren tick (live + offline) | `Game1.cs:1090`, `:767` |
| Sink: Warren facility upgrade | `Game1.cs:1119-1124` → `_dust.Spend(c.Dust)`; `Warren.cs:62,272` |
| Sink: checkpoint start | `Game1.cs:3491-3500` → `Checkpoints.DustCost` (`Checkpoints.cs:35,47`: 25 Dust/wave) |
| Display | `Game1.cs:4824` currency pill; `MapScreen.cs:52,142`; `WarrenScreen.cs:113` |

So `MemoryDustTree` is (a) the trait catalogue + ownership, (b) the trait-point ledger, and (c) the Memory Dust wallet for two unrelated systems. `AwardFromMastery`'s doc (`MemoryDust.cs:151-154`, "This is the ONLY faucet … never from grinding") is false on all four counts above. **One system, three responsibilities** — the wallet belongs beside `Hunter.Gleam`/`Hunter.Materials` (`HunterProgression.cs`: `AddGleam`/`SpendGleam`/`AddMaterial` already exist as the pattern).

### 1.3 Where the code *does* say the right thing

`PrestigeScreen.cs` is clean: title "TRAITS" (`:530`), "ONE SPINE · FOUR ROADS · NO TAKING BACK" (`:534`), "{n} TRAIT POINTS" (`:1309`), "You have {n} trait points." (`:1315`), refusal copy (`:468, :1357`). `MemoryDustText.CostSentence` (`:83`) says "trait points". `Onboarding.cs:356-358` tour step is correct. `Unlocks.cs:154` gates the screen on `TraitPointsEarned >= 1`. `game-flow.md:220-222, 549-552` and `skill-and-trait-trees.md:336-339` are correct. The itch `description.html:36` is correct.

### 1.4 Stale terminology (contradicts the actual rule)

| # | Where | Says | Should say |
|---|---|---|---|
| 1 | `MemoryDust.cs:88-97` class remarks | "Memory Dust — the prestige layer … buys from a FINITE, completable tree" | trait points; tree is not completable (§2.3) |
| 2 | `MemoryDust.cs:61-69` | "Dust is one of the three things the player loots … This is what it buys." | trait points |
| 3 | `MemoryDust.cs:148-155` `AwardFromMastery` | "ONLY faucet: Dust comes from mastering regions, never from … grinding" | Warren/conquest/deepening also mint it; Dust buys checkpoints + Warren |
| 4 | `MemoryDust.cs:251-253` | "no second engine and no second currency" | there are two currencies on this object |
| 5 | `MemoryDust.cs:102,47` / `DustEffects.cs:28` / namespace `Prestige` / `PrestigeScreen` | names say Dust/prestige | `TraitTree`, `TraitNode`, `TraitEffects`, `TraitsScreen`; `PrestigeScreen.cs:23` admits "keeps its name so the host wiring is unchanged" |
| 6 | `DustEffects.cs:10-11` | "What Memory Dust actually BUYS" | trait points |
| 7 | `DustEffects.cs:210-216` (`test_reaching_a_keystone_costs_more_than_dust`) | "Dust not spent walking toward IRONCLAD" | points |
| 8 | `Build.cs:475-476` `KeystoneSlots` remarks | "The Dust tree is COMPLETABLE by design — a player can buy every node" | not completable; sockets now start at 1 and are sold by the spine |
| 9 | `PlayerLoadout.cs:78` | "the tree teaches fifteen, you wear three" | 19 (`Keystones.cs` has 19 `Id =` entries) |
| 10 | `Warren.cs:9-10` | "Dust is the prestige currency" | Dust is the checkpoint/Warren currency |
| 11 | `Game1.cs:308` | "Memory Dust prestige (Full Vision). NOTHING RESETS — Dust accrues from mastery." | trait tree; points derived |
| 12 | `Game1.cs:3484` | "powered by the Dust tree's passive nodes" | trait tree |
| 13 | `Game1.cs:490` fixture key `Activity.Traits => "dust"`; `Game1.cs:2321` `_prestige.DevDustDebug`; `PrestigeScreen.cs:109` | dev names say dust | traits |
| 14 | `ForgeScreen.cs:509` "(EFFICIENT FORGE)", `:554` "Memory Dust auto-sell floor", `:598` "TIRELESS FORGE (Memory Dust)" | old node names (banned in `TraitNamesTests.cs:44-60`) + wrong currency | SALVAGE PAYS 15% MORE / AUTO-MERGE SPARE ITEMS; trait |
| 15 | `SaveGame.cs:61` `MemoryDustUnlocks` | field name says Dust unlocks | holds trait node ids (see §7 for why it must not be renamed blindly) |
| 16 | `MemoryDustTests.cs:14` "19 unlocks, 660 Dust"; `:66` "The ONLY faucet is region mastery"; `DustEffectsTests.cs:12` "What Memory Dust buys" | stale test prose | — |
| 17 | `UnlockEffect` docs `MemoryDust.cs:14` "more roster slots, more team room" | roster is gone | — |
| 18 | `design/gdd/memory-dust-prestige-system.md` (entire) | 19 unlocks / 660 MD / roster / creature evolution / "prestige" | superseded — see §10 |
| 19 | `design/gdd/systems-index.md:49,126,143,184,222` | lists memory-dust-prestige-system as Designed, Full Vision | point at skill-and-trait-trees §3.4 |
| 20 | `design/gdd/hunter-progression-system.md:163-169, 410, 487` (AC6 "simulated Memory Dust prestige reset") | assumes a reset event and 6 Dust unlock categories | no reset exists; no Dust tree |
| 21 | `design/gdd/progression.md:61-62` | "Mastery Points → the blessing (Dust) tree's mastery gate"; "Memory Dust → the blessing tree" | trait points → trait tree; Dust → checkpoints + Warren |
| 22 | `design/gdd/game-concept.md:161-162, 337, 348` | "Memory Dust prestige resets" | nothing resets; no prestige |
| 23 | `README.md:117` | "Memory Dust prestige (where nothing resets)" | traits |
| 24 | `production/qa/playtest-guide.md:38, 42, 132-139` | "P Memory Dust … X / 19 unlocks, Y / 660 Dust" | traits, 51 nodes, trait points |
| 25 | `docs/store/store-page.md:45` (older Steam draft) | "Memory Dust buys permanent traits" | flagged as old draft at `:31`, but still player-facing copy in the repo |
| 26 | `design/gdd/skill-and-trait-trees.md:465-467` §4.2 | "6 + ~10 + ~20 = 36. Tree cost 137 → 26.3%" | contradicts its own §3.4 (`:336-339, :367-371`: 34 pts, 226 cost, 15%); both wrong vs live 28 (§2.3) |
| 27 | `Game1.cs:5193-5196`, `MemoryDustTests.cs:227-229, 235-238` | "6 + 10 + 18 = 34" (3 mastery goals/region) | 6 + 10 + 12 = 28 (`RegionAutomation.cs:38-40, 101-107`) |
| 28 | `Economy/GearTraits.cs:8` `GearTrait`; `SaveGame.cs:379` `TraitOverride`; `CharacterScreen.cs:996` "TRAIT"; `tests/…/legacy_trait_migration_test.cs` | "trait" = an item prefix | collides with the trait tree in UI and in test names; pick one word for the item quirk (e.g. QUIRK / PREFIX) |

---

## 2. Q2 — Catalogue inventory per road

Source: `MemoryDust.cs:275-538`. 51 nodes, `TotalTreeCost` = 226 (pinned 215–240 by `MemoryDustTests.cs:34`).

### 2.1 SPINE — 19 nodes, 46 points (`Road` defaults to `Spine`, `MemoryDust.cs:50`)

| Id | Name | Cost | Requires | Effect enum | Real effect kind | Wire |
|---|---|---|---|---|---|---|
| socket_2 | KEYSTONE SOCKET II | 2 | — | Expansion | structural: +1 keystone socket | `DustEffects.KeystoneSockets` `:211-215` |
| weave_5 | FIFTH SKILL SLOT | 3 | socket_2 | Expansion | structural: +1 skill slot | `SkillSlots` `:218-222` |
| socket_3 | KEYSTONE SOCKET III | 5 | weave_5 | Expansion | structural | `KeystoneSockets` |
| vow_study_1 | LEARN VOWS I | 1 | — | Expansion | vow grant (complete, deliberate) | `KnownVows` `:103-127` |
| vow_study_2 | LEARN VOWS II | 2 | vow_study_1 | Expansion | vow grant (pure, frantic) | `KnownVows` |
| vow_study_3 | LEARN VOWS III | 2 | vow_study_2 | Expansion | vow grant (singular, bluntedge) | `KnownVows` |
| vow_binding | GEAR-SLOT VOWS | 3 | vow_study_3 | Expansion | vow grant (barefoot, openhand, bareskull) | `KnownVows` |
| vow_sacrifice | SACRIFICE VOWS | 3 | vow_study_3 | Expansion | vow grant (4 static-cost vows) | `KnownVows` |
| ledger | OPENS AUTO-SELL AND FORGE | 1 | — | Convenience | pure gate ("Does nothing by itself") | `ShowExactNumbers` `:165-169` — **0 callers** |
| filter_common | AUTO-SELL COMMON DROPS | 2 | ledger | Convenience | QoL: auto-sell floor | `AutoSellAtOrBelow` `:145-151` |
| filter_uncommon | AUTO-SELL UNCOMMON DROPS | 3 | filter_common | Convenience | QoL | `AutoSellAtOrBelow` |
| forge_insight | OPENS THE FORGE UPGRADES | 2 | ledger | Convenience | pure gate | `ShowMergePreview` `:172-176` — **0 callers** |
| efficient_forge | SALVAGE PAYS 15% MORE | 3 | forge_insight | Amplifier | economy multiplier ×1.15 | `DismantleRate` `:86-91` |
| auto_merge | AUTO-MERGE SPARE ITEMS | 2 | forge_insight | Convenience | QoL automation | `AutoMergeAfterRuns` `:156-160` |
| recall_1..4 | FASTER REGION MASTERY I–IV | 1/2/2/3 | chain | Amplifier | +5% RMP each | `MasteryRate` `:69-76` |
| attunement | A MARK — NO EFFECT | 4 | ks_glass_cannon, ks_ironclad, ks_greed, ks_echo, socket_3 | Convenience | nothing (honest) | `TreeComplete` `:179-183` — **0 callers** |

Sub-totals: sockets 10 · vows 11 · ledger group 13 · recall 8 · mark 4 = **46**.

### 2.2 The four roads — 8 nodes / 45 points each (`MemoryDust.cs:384-531`)

Every road: keystone chain **4 → 6 → 8 → 12** (= 30 for the terminal), a CHARGE-spur keystone (6) off rung 1, and three `BuildMods` minors (2 / 3 / 4) off rungs 2 / 3 / 4.

| Road | Anchor (spine) | Rung 1 (4) | Rung 2 (6) | Rung 3 (8) | Terminal (12) | Spur (6) | Minors (2/3/4) → `BuildMods` |
|---|---|---|---|---|---|---|---|
| RUIN | socket_2 | ks_glass_cannon | ks_bloodlust | ks_blood_magic | ks_reaper | ks_rend | ruin_edge_1..3: Damage 1.05 / 1.06 / 1.12 |
| AEGIS | socket_2 | ks_ironclad | ks_juggernaut | ks_undying | ks_titan | ks_dynamo | aegis_skin_1..3: Health 1.05 / 1.06 / 1.12 |
| AVARICE | ledger | ks_greed | ks_discerning_eye | ks_fortune | ks_hoarder | ks_lodestone | avarice_purse_1..3: Haul 1.05 / Rarity 1.06 / Haul 1.08 + Rarity 1.05 |
| ARTIFICE | vow_study_1 | ks_echo | ks_venomancer | **artifice_vows** (not a keystone: `SkillShape.VowPowerMultiplier` 1.25, `DustEffects.cs:232-244`) | ks_weaver | ks_capacitor | artifice_hands_1..3: SkillRate 1.05 / 1.06 / 1.12 |

Effect kinds present: **structural unlock** (3), **vow grant** (5), **QoL** (3), **pure gate** (2), **no-op mark** (1), **economy/rate multiplier** (5), **BuildMods multiplier** (12), **keystone grant** (19 — each keystone carries its own `BuildMods` and 0–2 `BuildTrigger`s, `Keystones.cs:29-188`), **SkillShape dial** (1). No trait node grants a `BuildTrigger` directly; triggers ride on keystones (`Build.cs:56-164`).

**Anomaly:** `artifice_vows` is tagged `UnlockEffect.Expansion` (`MemoryDust.cs:462`) though it is an amplifier; `UnlockEffect` is read exactly once in the whole game (`PrestigeScreen.cs:375`, Amplifier → small frame), so the mis-tag is cosmetic — and so is the enum.

### 2.3 Is the tree purchasable? Budget vs cost

**Terminal path costs** (`MemoryDustTests.cs:244-256` algorithm, recomputed by hand from the catalogue): RUIN 2+4+6+8+12 = **32**, AEGIS **32**, AVARICE 1+4+6+8+12 = **31**, ARTIFICE **31**. Cheapest two = 62.

**Budget the design assumes:** 34 = 6 conquests + 2×5 corruption tiers + 6 regions × 3 mastery levels (`Game1.cs:5193-5196`; `MemoryDustTests.cs:237-238` uses `(int)MasteryLevel.OptimizedTeam` = 3; `skill-and-trait-trees.md:336-339`).

**Budget the live code can produce:**
- `Regions.All` has 6 entries (`Regions.cs:63-98`: `new()` + 5 `BuildRegion`) → 6.
- `CorruptionScaling.MaxTier = 5` (`CorruptionScaling.cs:17`), `CanDeepenCorruption` stops at it (`Regions.cs:236`) → 2×5 = 10.
- `Region.MasteryLevel` returns at most `FullyMastered` = 2 (`RegionAutomation.cs:101-107`); the class remarks say `OptimizedTeam` "is unreachable by design here: it required a complete creature team, which no player could ever assemble" (`:38-40`, `:98-99`) → 6×2 = 12.
- **Total: 28.**

**Consequences at 28 points:**
- **No terminal is reachable** (31 > 28). `ks_reaper`, `ks_titan`, `ks_hoarder`, `ks_weaver` and the four notables behind them (`ruin_edge_3`, `aegis_skin_3`, `avarice_purse_3`, `artifice_hands_3`) are unreachable content. `TerminalArt` (`PrestigeScreen.cs:179-186`), the 2.2 s terminal flourish (`:83`), `sfx_trait_terminal.wav` and the four terminal emblems are for nodes nobody can buy.
- The deepest anyone gets is rung 3 of one road (RUIN/AEGIS 20 incl. anchor; AVARICE/ARTIFICE 19) with 8–9 points left over for the spine.
- `attunement` needs 32 (four rung-1 keystones 16 + socket chain 10 + ledger 1 + vow_study_1 1 + itself 4) → unreachable.
- The spine alone (46) is unaffordable; a player can own at most ~60% of it and nothing else.
- `test_two_terminals_are_out_of_reach` and `Game1.TraitPointsEarned`'s own comment ("one terminal reachable") are both wrong, and the test cannot notice because the budget is recomputed by hand in the test rather than read from the rule. This is the exact drift the Core/presentation split is meant to prevent: the income rule lives in `Game1.cs` (presentation), so Core tests cannot call it.

**Mutual exclusivity:** none is structural. `CanUnlock` (`MemoryDust.cs:168-174`) checks only cost and prerequisites; there is no "closes the other road" rule. Exclusivity is purely budgetary — and at 28 points it is total (nobody finishes *any* road), not "one of four". `DustEffectsTests.test_the_tree_is_still_completable` (`:337-348`) and `MemoryDustTests.test_the_entire_tree_can_be_completed` (`:45-61`) both buy everything with an artificial 5000/226-point budget, so "completable" in the test suite means "acyclic", not "reachable".

---

## 3. Q3 — SPINE: usability vs identity

Every road hangs off a spine node (`socket_2` for RUIN/AEGIS at `MemoryDust.cs:388,412`; `ledger` for AVARICE `:437`; `vow_study_1` for ARTIFICE `:454`), so the anchor cost is unavoidable and the remaining spine competes 1:1 with road rungs.

| Structural / QoL node | Cost | Compare to identity |
|---|---|---|
| socket_2 (wear a 2nd keystone) | 2 | = half a rung-1 keystone; **mandatory** to wear more than one of the keystones a road teaches (`DustEffects.cs:211-215`: sockets start at 1) |
| weave_5 (5th skill slot) | 3 | forced prerequisite of socket_3 (`:315`); also gated behind progression `Unlocks.SkillSlots` (`Game1.cs:2986-2990` takes the min) |
| socket_3 | 5 (10 with chain) | = a rung-2 keystone + a rung-1 |
| vow_study_1..3 + binding + sacrifice | 11 | = a whole rung-3 path minus the terminal |
| ledger + filters (auto-sell) | 6 | = a rung-2 keystone; auto-sell is an attention feature in an idle game (`MemoryDust.cs:336-337` says so) |
| forge_insight + efficient_forge + auto_merge | 7 | = a rung-3 keystone |
| recall_1..4 | 8 | +20% RMP, which is itself trait-point income (mastery levels) — a node that buys points, priced in points |
| attunement | 4 | nothing |

At the real 28-point budget a player who walks one road to rung 3 (20) has 8 left: `socket_2` is already in the 20, so 8 buys e.g. `vow_study_1`+`ledger`+`filter_common`+`recall_1`+`recall_2` (7). Auto-selling Uncommons, the fifth skill slot, the third socket, the forge nodes and the vow chain past rung 1 are all out of reach *for a player who wants any identity at all*. That is a **fake choice** in the brief's sense: the spine is "cheap" per node but is 46 points of one-time usability that idle players will want, and every point of it is a rung not walked. The design comment (`MemoryDust.cs:280-282`: "a spine that competed with the paths for points would turn 'what am I' into 'can I afford to be anything'") describes exactly the state the numbers now produce.

Note `vow_unbound` ("YOU MAY WEAR NO KEYSTONE", `ResonanceWeaving.cs:441-443`) is taught by `vow_sacrifice` (3 pts, behind 5 pts of vow chain) — the trait tree sells, for 8 points, permission to refuse what the trait tree sells.

---

## 4. Q4 — Respec / refund

- **Permanent in code.** `Purchase` only adds (`MemoryDust.cs:183-189`); there is no `Refund`/`Reset`/`Respec` member — `test_the_only_way_to_gain_dust_is_mastery_and_nothing_is_ever_reset` reflects over the type and asserts none exist (`MemoryDustTests.cs:87-94`). UI: "ONE SPINE · FOUR ROADS · NO TAKING BACK" (`PrestigeScreen.cs:534`), "Permanent. Never resets." (`MemoryDustText.cs:77`, drawn `PrestigeScreen.cs:1350`).
- **Contrast:** the mastery tree has `Refund(id)` and `Respec()` (`MasteryTree.cs:215-234`), free and instant; `skill-and-trait-trees.md:439-442` rule 4 makes this asymmetry the whole distinction.
- **One implicit refund path exists:** `Restore` drops any saved id not in the catalogue (`MemoryDust.cs:197`), and `Spent` sums only owned ids that exist (`:141`). Removing or renaming a node therefore *silently refunds its points* to every existing save. That is the correct behaviour for content removal, but it is also the only way a player ever gets points back, and it is invisible to them.

---

## 5. Q5 — Liveness, node by node

Method: `grep -rn --include=*.cs "DustEffects\.<Method>" src/ tests/` (run 2026-08-31; results in §5.2).

### 5.1 Live wires (node → DustEffects → consumer → effect)

| Nodes | Wire | Consumer(s) | Reaches |
|---|---|---|---|
| ruin_edge_*, aegis_skin_*, avarice_purse_*, artifice_hands_* (12) | `TreeMods` `DustEffects.cs:43-47` | `BuildComposer.cs:126` → `Build.PassiveMods` → `Build.Resolve` `Build.cs:533` | sim (`SoloBattle` via `Resolve`); `PassiveTreeTests.cs:248` |
| 19 `ks_*` gates | `LearnedKeystones` `:56-64` | `BuildComposer.cs:145` (`build.Take`), `BuildScreen.cs:921`, `WeaveScreen.cs:666,735,1162,1762` | sim; every granted trigger is read in `SoloBattle.cs` (Rend/Capacitor/Dynamo/Lodestone `:475-478,695,1601,1894`; Venom `:491`; Splinter `:697`; Bloodlust `:745`; Zeal `:755`; Echo `:763,1550`; Hoarder `:817`; NoHealing `:1169`; Weaver `:1702`; Undying `:1954`) |
| recall_1..4 | `MasteryRate` `:69-76` | `Game1.cs:3652` → `Region.RecordActiveKill(rate)` `RegionAutomation.cs:132-133` | RMP → mastery level → trait points |
| efficient_forge | `DismantleRate` `:86-91` | `Game1.cs:3529` → `ForgeTuning.DismantleReturnRate` | forge salvage yield |
| vow_study_1..3, vow_binding, vow_sacrifice | `KnownVows` `:121-127` | `WeaveScreen.cs:599` | vow menu → `Build.Skills[].Vow` → sim |
| filter_common / filter_uncommon | `AutoSellAtOrBelow` `:145-151` | `Game1.cs:3533` → `_forge.AutoSellFloor` (`ForgeScreen.cs:558`) | loot disposal |
| auto_merge | `AutoMergeAfterRuns` `:156-160` | `Game1.cs:3601` → `_forge.AutoMergeOnOpen` (`ForgeScreen.cs:599`) | forge automation |
| socket_2, socket_3 | `KeystoneSockets` `:211-215` | `Game1.cs:630, 3597` → `PlayerLoadout.KeystoneCapacity` → `PlayerLoadout.cs:227` | how many keystones can be worn |
| weave_5 | `SkillSlots` `:218-222` | `Game1.cs:629, 2988` (min with `Unlocks.SkillSlots`) → `SkillCapacity` | loadout |
| artifice_vows | `VowPowerMultiplier`/`TreeShape` `:232-244` | `BuildComposer.cs:132` → `Build.Shape` → `SoloBattle.cs:2111` | vow payout |

**47 of 51 nodes reach something real.** Sample greps for three "suspect" nodes:
- `recall_*` → `grep -rn "DustEffects.MasteryRate" src/` → `Game1.cs:3652` (1 hit) + `RegionAutomation.cs:124` doc. Live (was dead before 2026-08-25 per `RegionAutomation.cs:127-131`).
- `weave_5` → `grep -rn "DustEffects.SkillSlots" src/` → `Game1.cs:629, 2988`. Live but capped by `Unlocks.SkillSlots` (`Game1.cs:2986-2990`): the node buys nothing until the progression gate has already granted 4 (`gate >= Build.SkillSlots ? fromTree : gate`).
- `artifice_vows` → `grep -rn "DustEffects.TreeShape" src/` → `BuildComposer.cs:132`. Live; `DustEffectsTests.cs:351-376` pins it through the composer.

### 5.2 Dead wires (method exists, nothing in `src/` calls it)

| Method (`DustEffects.cs`) | Node it "wires" | `grep -rn "DustEffects\.<M>\|\b<M>(" src/` excl. definer | Note |
|---|---|---|---|
| `ShowExactNumbers` `:165-169` | ledger | **0** | node description is honest ("Does nothing by itself") |
| `ShowMergePreview` `:172-176` | forge_insight | **0** | same |
| `TreeComplete` `:179-183` | attunement | **0** | same ("A MARK — NO EFFECT") |
| `KnowsVow` `:129-133` | — (helper) | **0** | tests only |

Also dead in `src/`: `MemoryDustTree.IsComplete` (`MemoryDust.cs:203`; `grep "\.IsComplete\b" src/` → 0), `MemoryDustText.Sheet`/`CostSentence`/`RequiresSentence` (`MemoryDustText.cs:67-98`; 0 callers — `PrestigeScreen.DrawDetail` composes its own panel at `:1292-1350` and only cites `Sheet` in a comment `:1228`), `Keystones.IsATrade` (`Keystones.cs:199-208`; 0 callers — a declared test hook, acceptable), and 2 of the 3 `UnlockEffect` values (only `Amplifier` is read, `PrestigeScreen.cs:375`).

**The guard's shape is the finding.** `WiredIds` (`DustEffects.cs:193-201, 246-254`) is a hand-maintained list plus "has Mods or GrantsKeystone". `test_every_unlock_in_the_catalog_is_actually_wired_to_something` (`DustEffectsTests.cs:33-46`) therefore proves "some method in DustEffects names this id", not "some system reads that method". Three of the four gate nodes pass on methods with no callers. The correct guard is one hop further out: assert every public `DustEffects` member is referenced from `src/` outside `Prestige/` (a reflection-plus-grep test, or a `TriggerLivenessTests`-style consumer list).

---

## 6. Q6 — Form dependencies in trait nodes / keystones (migration list to Style/SkillKind)

| Node → keystone/vow | Form dependency | Evidence |
|---|---|---|
| `ks_rend` → REND (`BuildTrigger.Rend`) | fires only when `form == Form.Strike` | `SoloBattle.cs:1601-1602`; `Build.cs:85-92` "Dead without a Strike in the build, on purpose"; blurb "YOUR STRIKES SPEND IT ALL" `Keystones.cs:143` |
| `ks_capacitor` → CAPACITOR | inert without REND → transitively Strike-dependent | `Keystones.cs:149-153` "USELESS UNLESS A KEYSTONE USES THE POOL" |
| `ks_weaver` → WEAVER (`BuildTrigger.Weaver`) | echoes as `skills[(i+1)%n].Form`; uses `FormBehaviour.IsAmplifier/FiresOnBeingHit/BaseDamage/AffinityFactor(…, Form)` and `shape.TargetsFor(woven.Form)` | `SoloBattle.cs:1702-1716`; blurb "AS THE NEXT FORM YOU CARRY" `Keystones.cs:184`; `Build.cs:155-163` |
| `ks_juggernaut` (comment only) | "refuses every TRANSFORMATION/SIPHON sustain build" | `Keystones.cs:99-102` |
| `vow_study_3` → `vow_singular` | `VowDemand.SingleForm` → `ctx.DistinctForms <= 1` | `ResonanceWeaving.cs:387-388, 505`; `SoloBattle.cs:2133` builds `DistinctForms` from `build.Skills.Select(s => s.Form)` |
| road identity copy | "fire as another Form" | `TraitRoads.cs:28` (remarks; the player-facing `Sentence` at `:44-45` is Form-free) |
| catalogue comment | "the Form-combo triggers it used to hold now belong to the skill tree" | `MemoryDust.cs:451` |
| the path from tree to sim | `BuildComposer.Compose` is Form-shaped: `FormBehaviour.IsPassive(skills[i].Form)`, `new WovenAbility{Form}`, `SkillCatalogue.Resolve(s.Form, …)`, `FormBehaviour.BaseCooldownMs(s.Form)` | `BuildComposer.cs:102-103, 166, 171-173, 180` (out of area, but every keystone reaches the sim through it) |

Not Form-dependent: the 12 minors, all spine nodes except `vow_study_3`, and the other 16 keystones (their triggers are Form-agnostic — Bloodlust/Zeal/Echo/Venom/Undying/NoHealing/Splinter/Hoarder/Dynamo/Lodestone). `Overdraw/Linger/Radiance/Execute/Coiled/Siphon` are enchantment/mastery-specialisation triggers, not trait content (`Build.cs:117-134`, `MasteryCatalog.cs:420-434`).

**Migration shape:** REND → "your **Hammer**-style skills spend the pool" (or `SkillKind`/Style predicate on `SkillDef`); WEAVER → "the next *skill* you carry" resolved through `EquippedSkill.Def`, not `Form`; `vow_singular` → `SingleStyle`. Three keystones, one vow, one context field (`DistinctForms`).

---

## 7. Q7 — Save fields and the "legacy trait migration"

### 7.1 Fields

| Field | Type | File:line | Written from | Read into |
|---|---|---|---|---|
| `MemoryDust` | int | `SaveGame.cs:60` | `prestige?.MemoryDust` `:532` | `_dust.Restore(save.MemoryDust, …)` `Game1.cs:614` → `MemoryDust.cs:195` |
| `MemoryDustUnlocks` | `List<string>` (trait node ids) | `SaveGame.cs:61` | `prestige?.OwnedIds` `:533` | `Restore` `MemoryDust.cs:196-197` — unknown ids dropped silently |
| `HighestMasteryAwarded` | int (Dust award high-water) | `SaveGame.cs:75` | `Game1.cs:2750` | `Game1.cs:631` |
| `CorruptionPeak` | int (trait-point input) | `SaveGame.cs:95` | `world.PeakCorruptionTier` `:539` | `World` → `TraitPointsEarned` |
| `ConqueredRegions`, `RegionFarms[].MasteryPoints` | inputs | `SaveGame.cs:80, 82, 546` | — | `TraitPointsEarned` |
| *(trait points)* | **not persisted** | — | derived every frame `Game1.cs:3715` | — |

State is reconstructable (owned ids + world facts → points), which satisfies the "state vs event" law. Round-trip test: `SaveSystemTests.cs:462-485`.

### 7.2 The existing "legacy trait migration" is about **gear**, not the tree

`tests/unit/…/Persistence/legacy_trait_migration_test.cs:9-16` — "The prefix migration: pre-redesign saves keep the traits they HAD". It migrates `SavedItem.TraitOverride` (`SaveGame.cs:371-379`): a missing field (`null`) is re-derived via `GearTraits.LegacyDerivedTrait(instanceId, baseType)` (`SaveGame.cs:588-590`), the sentinel `"NONE"` means plain (`:570`). It never touches `MemoryDustUnlocks`. **There is no prestige-trait migration in the codebase**; retired node ids (`might_1`, `grit_1`, `hatchery_1`, "EXPANDED WARREN" — `MemoryDust.cs:246-248`, `Game1.cs:2153-2156`) are handled only by the silent drop in `Restore`. `TraitNamesTests.test_ids_never_change_because_saves_store_them` (`:66-70`) pins all 51 ids for exactly this reason.

---

## 8. Q8 — Duplication with the mastery tree

`MasteryTree.Mods()` returns `BuildMods.None` by design (`MasteryTree.cs:286-294`); mastery numbers travel as `HunterStat` additive points (`MasteryTree.Stats()` `:264-273` → `Hunter.SetMasteryStats` `Game1.cs:2757` → `HunterProgression.cs:192-199`) and as `SkillShape`. Trait minors travel as `BuildMods` multipliers. Same dials, two channels, multiplied together in `Build.Resolve` (`Build.cs:519-533`: `fromKeystones.Combine(fromStats).Combine(PassiveMods)`).

| Dial | Trait tree | Mastery tree | Also |
|---|---|---|---|
| Vow payout | `artifice_vows` ×1.25 (`DustEffects.cs:235`) | PLEDGE ×1.30 (`MasteryCatalog.cs:178-179`), ZEALOT ×1.60 (`:197-198`) | Oathbound ×1.5 (`CharacterRoster.cs:201`); all multiply via `SkillShape.Combine` (`SkillShape.cs:476`) → up to ×3.9 on the vow bonus (`SoloBattle.cs:2111`) — **same job, three systems** |
| Health | MORE HEALTH I–III ×1.05/1.06/1.12 (`BuildMods.Health`) | HIDE/BULK +12 MaxHealth, STEEP/COUNT +5 (`MasteryCatalog.cs:360-361,168,221`) additive `HunterStat.MaxHealth` | keystones ×0.5…×3.0 |
| Damage | HARDER HITS I–III (`BuildMods.Damage`, all hits) | BITE/FORCE +4/+5 AttackPower (`:282-283`) → `AutoDamageMultiplier` basic swing only (`HunterProgression.cs:378`) | partial overlap |
| Skill rate | FASTER SKILLS I–III (`BuildMods.SkillRate`) | SWIFT/QUICK +3 Engineering (`:284-285`) → `SquadSkillRate ×(1+0.006·Eng)` (`HunterProgression.cs:434`) | same dial, additive-inside-multiplicative |
| Haul | MORE LOOT / MORE AND RARER LOOT (`BuildMods.Haul`) | GLEAN/POCKETS/SCAVENGE/WEIGH +Guile → `HaulMultiplier` (`HunterProgression.cs:440`); notables UNTOUCHED/BLOODPRICE/CACHE/PROSPECT/GAMBLE/LODE/PROSPECTOR (`:228-252`) | GREED/HOARDER keystones — **three systems on one number** |
| Skill slots | `weave_5` | — | `Unlocks.SkillSlots` progression gate; `Game1.cs:2986-2990` takes the min of the two |
| Triggers | keystones grant general triggers | specialisations grant only Form-combo triggers (`MasteryCatalog.cs:420-430`) | clean split — no duplication |

The design law in `skill-and-trait-trees.md:427-433` ("No node may be a bare multiplier") is honoured by the mastery *minors* only nominally (+12 MaxHealth is a bare number) and is explicitly waived for the trait minors (`MemoryDust.cs:469-486`). The `test_the_trait_trees_multipliers_are_small…` cap (`DustEffectsTests.cs:118-150`, ≤1.35 per field for the whole tree) bounds the trait side only; nothing bounds the product across the three channels.

---

## 9. Numerical stacking paths (for the multiplicative-soup ledger)

| Path | Kind | Evidence |
|---|---|---|
| `Build.Resolve` = Π keystones × hunter(gear/charm/focus/stats) × `PassiveMods`(trait minors × character) | multiplicative across 3 channels | `Build.cs:519-533`; `BuildComposer.cs:126-127` |
| `SkillShape.VowPowerMultiplier` = trait × mastery × mastery × character | 4-source product on one dial | `SkillShape.cs:476`; sources §8 |
| Trait minors themselves | `BuildMods.Sum` multiplicative (by design, "additive stacking is how an idle game arrives at 400,000%") | `DustEffects.cs:39-46` |
| Memory Dust faucet | Warren `DustBonusPerLevel` × `AllProductionPerLevel` × `ConquestBonusPerRegion` (compounding) vs linear checkpoint sink (25/wave) and geometric Warren cost (×1.25) | `Warren.cs:70-79, 86-89`; `Checkpoints.cs:35` |
| Trait points | strictly additive, capped (28) — the one honest ledger | `Game1.cs:5197-5199` |

---

## 10. Authoritative vs obsolete

### 10.1 Authoritative (keep; rename)
- `Prestige/MemoryDust.cs` — `MemoryDustUnlock` record (`:47-86`), `Catalog` (`:275-538`), `MemoryDustTree` ledger/gate/validate (`:102-231`). **Rename** → `TraitNode` / `TraitTree`; **extract** the Dust wallet (`MemoryDust`, `AwardFromMastery`, `Spend`, `:113,155,161-166`) to `Hunter`.
- `Prestige/DustEffects.cs` — the one seam (`TreeMods`, `LearnedKeystones`, `KnownVows`, `KeystoneSockets`, `SkillSlots`, `MasteryRate`, `DismantleRate`, `AutoSellAtOrBelow`, `AutoMergeAfterRuns`, `TreeShape`). Rename → `TraitEffects`; delete the four uncalled methods.
- `Prestige/TraitRoads.cs`, `Prestige/MemoryDustText.cs` (`Describe`, `ModsSentence`, `KeystoneSentence`, `SentenceCase`, `Permanence`), `Prestige/TraitTreeLayout.cs`.
- `Builds/Keystones.cs` (19 keystones; every one a trade — `IsATrade` is asserted by `PassiveTreeTests`/`DustEffectsTests`).
- `Game/PrestigeScreen.cs` (copy already says TRAITS / trait points); rename class.
- `design/gdd/skill-and-trait-trees.md §3.4–3.6` — the live design; fix §4.2 and the 34→28 budget.
- Tests: `MemoryDustTests`, `DustEffectsTests`, `TraitNamesTests`, `TraitDescriptionsTests`, `MemoryDustTextTests`, `Builds/PassiveTreeTests`.

### 10.2 Obsolete
| Name | Files | Class | Why | Replacement |
|---|---|---|---|---|
| Memory Dust prestige GDD | `design/gdd/memory-dust-prestige-system.md` (350 lines) | A-delete (or mark Superseded with a one-paragraph pointer) | 19 unlocks/660 MD/roster slots/creature evolution/`vow_binding_cost`; every system it cites (creature jobs, region teams, loot filter rule engine) is gone; `systems-index.md:49` still lists it as the design of record | `skill-and-trait-trees.md §3.4–3.6`; `game-flow.md:540-556` for Dust's job |
| Dust wallet on the tree | `MemoryDust.cs:113,148-166`; `SaveGame.cs:532` | C-replace | wallet for Warren/Checkpoints riding on the trait catalogue | `Hunter.Dust` with `AddDust`/`SpendDust`, same save field `MemoryDust` |
| Uncalled effect methods | `DustEffects.cs:165-183` (`ShowExactNumbers`, `ShowMergePreview`, `TreeComplete`), `:129-133` (`KnowsVow`) | A-delete | 0 callers in `src/` | gate nodes carry `Mods = None, GrantsKeystone = null` and are recognised as gates by data; liveness test asserts *callers* |
| `MemoryDustTree.IsComplete`, `MemoryDustText.Sheet/CostSentence/RequiresSentence` | `MemoryDust.cs:203`; `MemoryDustText.cs:67-98` | A-delete (or keep `Sheet` only if a codex/share-code surface is actually built) | 0 callers in `src/` | `PrestigeScreen.DrawDetail` already prints the same facts |
| `UnlockEffect` enum | `MemoryDust.cs:9-19`, all 51 `Effect =` assignments | C-replace | read once (`PrestigeScreen.cs:375`) to pick a frame; docs describe roster slots; `artifice_vows` mis-tagged | derive "minor" from `Mods != None` (the screen already classifies from data at `:368-376`) |
| Stale test/fixture prose and dev keys | `MemoryDustTests.cs:14,66`; `DustEffectsTests.cs:12`; `Game1.cs:490 "dust"`, `:2321 DevDustDebug`; `Game1.cs:2152-2161`, `1565-1571` fixture comments (`RestoreCorruption(16)` → "reachable 22" arithmetic is also stale) | B-migration-only (rename in the same pass) | — | — |
| Stale design/production copy | list in §1.4 rows 19–26 | A-delete/rewrite | — | — |

---

## 11. Serialization risks

| Field | Risk | Migration |
|---|---|---|
| `SaveGame.MemoryDustUnlocks` (`SaveGame.cs:61`) | Name says Dust; renaming the JSON member drops every player's traits (they would silently load as none — and `Spent` would refund them, so it would even look fine). | Keep the JSON name via `[JsonPropertyName("MemoryDustUnlocks")]` on a renamed C# property, or read both names in `Restore`. `SaveSystemTests.cs:462-485` must be extended to load a fixture written with the old name. |
| Trait node ids (51) | Any id change = silent loss + silent refund (`MemoryDust.cs:197,141`). | Never rename; if a node is removed, keep an explicit `RetiredIds` map so the drop is a decision, not an accident (`TraitNamesTests.cs:66-70` pins them). |
| `SaveGame.MemoryDust` (`:60`) | Moving the wallet to `Hunter` changes the *capture* source (`:532`) but not the field — safe if the field name stays. | None if the JSON member is unchanged. |
| `CorruptionPeak` absent in old saves (`:95-96`) | "then the tier is the peak" — a player who eased shallower before 2026-08-23 loses trait points on load. | Accepted already; note only. |
| Trait points not persisted | Any change to `TraitPointsEarned` (e.g. fixing the 28 budget by counting something new) instantly re-prices every existing save — up or down. A downward change would leave `Spent > Earned` → `Available = 0` (clamped `MemoryDust.cs:143`) but owned nodes stay. | Fine by design; document that the formula is a save-compat surface. |

---

## 12. Core / presentation coupling

| File:line | Note |
|---|---|
| `Game1.cs:5191-5199` `TraitPointsEarned()` (and `:5142-5149` `SkillPointsEarned`) | The **income rule** — a Core rule cited by the GDD (`skill-and-trait-trees.md:336`) — lives in the MonoGame host. Core tests cannot call it, so `MemoryDustTests.cs:235-238` re-derives it by hand and drifted (34 vs 28). Move to Core (e.g. `Prestige/TraitPoints.Total(world)`), test the real function. |
| `Game1.cs:2747-2751`, `:3721`, `:1090`, `:767` | Dust faucet magnitudes (`*15*reward`, `40*reward`) are literals in the host; only `DeepeningDustAward` is in Core (`CorruptionScaling.cs:30`). |
| `Prestige/TraitTreeLayout.cs` | Pixel geometry (`LaneStep 190`, `NodeWidth 176`) in the headless Core, justified by the no-overlap test (`:54-55`). Acceptable, but it is presentation data. |
| `Prestige/MemoryDustText.cs` | UI copy in Core — by the project's own rule (testable, ESL-checked). Acceptable. |
| `PrestigeScreen.cs:179-186` `TerminalArt` and `:385-386` `DrawRoadHeaders` | Presentation switches on keystone/node ids. |
| `DustEffects.cs:73, 89, 108-117, 148-149, 159, 168, 175, 182, 214, 221, 235` | `Owns("literal_id")` — string-keyed gameplay in Core. The data-driven half (`Mods`, `GrantsKeystone`) is fine; the rest is an id switch. |

### Hardcoded branches on content ids
- `DustEffects.cs` as above (13 literal ids).
- `TraitTreeLayout.cs:106-151` — every node id positioned by hand (mitigated by `Derive` fallback `:173-198`).
- `PrestigeScreen.cs:179-186` (`"reaper"/"titan"/"hoarder"/"weaver"`), `:385-386` (terminal ids per road), `:222` (`Requires.Count >= 4` ⇒ the mark).
- `DustEffectsTests.cs:139` — `r == "artifice_vows"` special-cased in the "hangs off a rung" rule.
- `Game1.cs:1572-1573, 2049-2051, 2170-2172` — dev fixtures buy literal ids (fine, dev only).
- No `switch(skill.Id)` in the battle loop for trait content — the sim reads `BuildTrigger`s and `BuildMods`, which is the right abstraction.

---

## 13. Closed-loop currencies

- **Trait points:** faucet = conquest/corruption/mastery (derived), sink = nodes. Closed, finite, capped at 28. No leak. **Problem is the cap, not the loop.**
- **Memory Dust:** faucets = Warren tick (compounding), mastery levels, conquest, deepening; sinks = checkpoints (repeatable, linear), Warren upgrades (geometric). Closed, but its wallet is on the wrong object, and `MemoryDust.cs:151-154` lies about its faucets. `Checkpoints.cs:20-23` records that Dust "used to buy traits, then bought nothing but Warren upgrades" — the pill was read as a lie in playtest; the object name still tells that lie to the next programmer.

---

## 14. Over-engineering / abstraction notes

- `UnlockEffect` is a three-value enum that one line reads — a classification the data already expresses.
- `MemoryDustText.Sheet` + `RequiresSentence` + `CostSentence` exist "so a codex, a share code, a tooltip" can print the same sentence (`:28-30`) — no such second consumer exists; generalised before a second user.
- `TraitTreeLayout.Derive` auto-placement (`:173-225`) is a fallback for an authoring mistake that a one-line test (`Assert.Empty(Unauthored)`, `TraitNamesTests.cs:187`) already catches; 50 lines of placement logic guarding a to-do.
- Good restraint to preserve: keystones are data (`BuildMods` + `BuildTrigger` list), the sim reads triggers not ids, the tree *teaches* and the build *wears* (`Build.cs:470-489`), the description/`ModsSentence` equality test (`TraitDescriptionsTests.cs:64-70`) keeps copy and sim honest, `Restore` is order-independent and tolerant.

---

## 15. Open questions (not asserted)

1. Is the 28-point ceiling intended after the creature-team retirement, or an oversight? If intended, the terminals and their emblems/flourish/notables are dead content and the "one terminal, not two" design collapses; if not, the fix is either +1 mastery goal per region (e.g. a depth or conquest-overwave goal replacing `OptimizedTeam`) or cheaper terminals — and `TraitPointsEarned` should move to Core so the test reads the rule.
2. Should Memory Dust keep the *name* "Memory Dust" at all, now that it buys checkpoints and Warren levels and nothing permanent? The player-facing pill and the Warren facility copy ("Search the deep soil for Memory Dust", `Warren.cs:50`) are fine; the code identity is the problem.
3. Is `weave_5` meant to do anything while `Unlocks.SkillSlots` also gates slots? Today it only matters once the progression gate has already granted 4 (`Game1.cs:2986-2990`) — verify with the Unlocks audit which gate is authoritative.
4. Should `vow_unbound` (refuse all keystones) be taught by the tree that sells keystones — and should the spine's vow chain be moved to the mastery tree (respeccable "how am I specialising now") rather than the permanent tree, given vows are build-time choices?
5. Does anyone intend the four `Owns("…")` gate/QoL wires (`ledger`, `forge_insight`, `attunement`) to grow a real effect, or should the gate nodes be deleted and their children re-anchored (saves 7 spine points, which at a 28-point budget is a whole rung)?
6. Is `TraitTreeLayout` (pixels) acceptable in Core under the project's Core/Game contract, or should it move to the Game project with its test?
