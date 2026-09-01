# Final report — the IDLExIDLE architectural refactor (2026-08-31 → 2026-09-01)

**Branch**: `feat/hunter-cutout-rig` · **Suite at close**: 1,242 unit + 2 integration, green · every
phase committed with gates + boot soak green. Method per phase: read the area audit + its
adversarial verification, re-grep the working tree (audits predate the middle phases), implement,
test, gate, commit.

## 1. What the refactor was

One mission (owner's brief, 2026-08-31): make STYLE → SKILL → VARIATION → REINFORCEMENTS the one
authoritative skill model, delete the Source×Form runtime, and walk every system — mastery, traits,
vows, characters, quests, gear, loot, warren, encounters, persistence, docs — until each mechanic
either reaches the player or leaves the codebase. The law throughout: **a mechanic without a
consumer does not exist.**

## 2. The phases, as committed

| Phase | Commit | What became true |
|---|---|---|
| P0-P1 | (audit corpus) | 14 area audits + adversarial verifications + PLAN with D1-D13 |
| P2 | `88d2830` | The project speaks its own name; the save folder alone stays `ResonanceHunter` |
| P3 (a-final) | `fb27590` `608a2e1` `5e6951e` `e82eaf4` `df52044` `4da40cc` | SkillId is the identity on disk and in the sim; Form leaves the runtime; the affinity ring is the styles'; variations/reinforcements actually run; save v3 with a one-time frozen migration |
| P5 | `179b91a` | The descent goes headless (Core `Descent`); offline is the real simulation (`OfflineHunt`); `Career` owns the account ledger |
| P6 | `a7cf35c` | D7: a learned skill is learned for good (`LearnedSkills` latch); 18 orphan dials + ~120 dead sim lines out |
| P7 | `bfa1eee` | Terminals reachable (34-point budget real, PERFECTED tier live); vow gates enforced at compose |
| P8-P9 | `258c24b` | Birth skills latch; every character card says everything it does; no Aptitude |
| P10 | `137f2bf` | Quests are measurable facts (BossesFelled, WavesWithStyle); id-retirement law applied |
| P11 | `c7e078d` `71bad85` | FITS badges tell the sim's truth; AMPLIFY named; creature-era forge/loot limbs off; the stacking ledger is law |
| P12 | `5f15730` | INSIGHT (closed loop) cut; facilities pay Forge materials, unlock by conquest, capped by depth |
| P13 | `ad32484` `f04933d` | The phantom template/par/efficiency economy out; WARDED/ENTRENCHED/LEGION become real rules, HOLLOW cut; run-report enums travel by name over a frozen table |
| P14 | `6d1e1e3` | The save stops writing its past; the Merge fold is testable Core; one fixture walks a Form-era file end to end |
| P15 | `524bcd8` | 25 SUPERSEDED banners; README/systems-index/warren-facilities rewritten; `sources.md` born; store copy off Source×Form |
| P16 | (this commit) | This report; last dormant fixture fixed; repo hygiene |

## 3. §101 — the final legacy grep, every hit justified

`grep -rn` over `src/ tests/ tools/` (bin/obj excluded), 2026-09-01. The survivors match PLAN §5's
declared target end-state exactly.

| Pattern | Hits | Justification |
|---|---|---|
| `ResonanceHunter` | `SaveFile.cs:25` (folder const), `tools/check_boot.sh:32` | **By design** — the owner's one hard constraint: existing saves must keep loading. |
| `Form` (as a type) | none — `enum Form` no longer exists | Runtime Form is zero. |
| `Form` (as data) | `SavedSkill.Form` (read-only legacy member), `Persistence/LegacySkillForm.cs` (frozen map), `PlayerLoadout.Restore` rows, `ShareCodes` v1 validation | **The migration seam** — reads a pre-v3 save once, writes nothing. Pinned by `legacy_full_save_migration_test`. |
| `WovenAbility` / `FormBehaviour` | comment mentions only (`Build.cs`, `SoloBattle.cs`, `TestBuilds.cs`) | Historical remarks explaining deletions — they describe the past as past. |
| `Aptitude` | test comments only | Deleted (P8-P9); comments record it. |
| `WEIGHT` / `SPREAD` (branches) | none in code or player copy after P15/P16 | Historical bug-story comments in BuildScreen name them as history, present-tense uses corrected. |
| `INSIGHT` | tombstone comments (`SaveGame`, `Warren`, `Game1`, `WarrenScreen`) | Explain the P12 cut and the skipped save key. |
| `WarrenMasteryPool` / `RegionMasteryPoints` / `ChestKeepSlot` | schema members marked WRITTEN NO MORE + their read-folds | Legacy read-only; folds pinned by tests. |
| `Charter.Merge` | enum member + Core fold + tests | **Accepted survivor** — the migration (Merge→Salvage) now stands beside it in `RestoreHunter`, so the member can leave in a later version with its fold. |
| `RH_*` env vars | `DevEnv` accepts `RH_*` and `IXI_*`; tools use `RH_SAVE_DIR`/`RH_BOOTCHECK`/`RH_SHOT_MODE` | Accepted alias reads — tooling keeps working. |
| `RHB`/`RHI` share-code prefixes | readers accept v1; writers emit v2 | Wire-format compatibility. |
| `Solo*` class names, `MasteryEarned` (holds deepest-ever), `Automation` namespace | live code | **Accepted stale terms** (PLAN §3/§5) — renames would be churn; each carries a truthful doc comment. |

## 4. §110 — what is now true that was not

- **One skill model.** A skill is its catalogue id; variation + reinforcement deltas are applied
  once at compose; the central loop branches on no skill or character id.
- **Deterministic, headless, fast-forwardable.** `SoloBattle.ResolveWave` + `Descent` +
  `OfflineHunt`; offline pay is the same sim.
- **Every advertised mechanic is real** — or gone. The four costume affixes got verdicts; the
  closed-loop currency, the phantom par economy, the never-run loot branches, the empty fixture
  poses are out. New liveness proofs ride each: affix-direction tests, the stacking ledger, the
  gleam-economy hours table, the end-to-end save-migration fixture.
- **Saves are safe.** Ids that change meaning are retired, never reused; enums travel by name
  (skills, quests, run-report walls); unknown names degrade, and the two boot-crash parse paths
  are gone; a Form-era file loads, migrates once, and reloads stable.
- **The docs tell the truth** about which game this is (P15).

## 5. Recorded, not changed — the playtest questions

Deliberately NOT retuned blind (per the owner's standing instruction): spine cost 46 vs career 34 ·
the mastery point-curve squeeze (~21%) · charm health double-dip · total DEFENCE as the one
unbounded additive sum · the three divergent item-level ceilings (2.0 / 2.8 / 4.0, pinned as-is) ·
Dust/checkpoint pacing after the P12 rebase · heal:damage 0.90-0.95 band under live LEGION ·
enemy curve + champion HP (inherited, unproven).

## 6. Deferred, with owners

- **Docs**: archive-folder moves; `skill-and-trait-trees` split; `game-concept`/`game-flow` content
  rewrites; `entities.yaml` rebuild; engine-reference template cleanup; GDDs for the undocumented
  live systems (list in `systems-index.md`).
- **Code**: LEGION spawn-reveal on screen (brood visible from wave start — polish);
  `Haul.Cores`→`CoreMaterial` rename (tests pin the name); region-keyed table consolidation onto
  `RegionDefinition`; `RunReport.Verdict` thresholds → tuning; Wallet abstraction (three owners
  today — recorded, deliberately not built).
- **Product**: the itch `forms.png` image still draws the six Forms (copy is fixed; art is not).

## 7. How to trust this report

Every phase's exact scope is in `production/session-state/active.md`'s task log and the commit
bodies (all carry `Task: refactor-2026-08-31 P<n>`). The audits and their adversarial
verifications — including where the audits were wrong and how the verify pass corrected them —
are beside this file.
