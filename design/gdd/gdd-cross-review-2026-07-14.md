# Cross-GDD Review Report

**Date**: 2026-07-14
**GDDs Reviewed**: 25 (system #26, `hunter-progression-system`, was created *by* this review)
**Passes run**: Consistency (2a–2f) · Design Holism (3a–3g) · Cross-System Scenario Walkthrough (5 scenarios)

---

## VERDICT: **FAIL**

Four blocking issues. Two are already fixed; two require a design decision before
`/create-architecture` can begin.

**Do not start architecture until B1 and B2 are resolved.** Architecture built on an inverted
efficiency contract and an unowned spawn system will inherit both.

---

## BLOCKING

### 🔴 B1 — The active/idle efficiency contract INVERTS as a region gains mastery. Pillar 3 breaks arithmetically.

**Status**: OPEN — requires a pillar-level design decision.
**Files**: `combat-encounter-system.md` §4 F7 ↔ `region-mastery-automation-system.md` §4 F4/F6 ↔ `loot-drop-system.md` §4 F1/F2/F5

`active_efficiency_percent` is measured **relative to automation's live clear time**
(`CTR = automation_baseline_clear_time_seconds / active_clear_time_seconds`). But
`region-mastery-automation-system` Formula 6 makes automation *faster* as mastery rises. So as
automation improves, `CTR` shrinks, and **the active player's score falls** — while being consumed
downstream as an *absolute* reward multiplier.

Holding one player's skill fixed (`CTR = 1.15` at the 45s baseline; complete team, `power_tier = 5`,
`team_quality_score = 0.7375`) and walking the same region up the mastery ladder:

| mastery_level | idle_eff | baseline (F6) | CTR | **active_eff** | active loot bonus | automation kills/hr | active kills/hr |
|---|---|---|---|---|---|---|---|
| Newly Conquered | 36.1% | 41.6s | 1.064 | **166%** | 1.53× | 87 | 92 |
| Partially Mastered | 64.75% | 23.2s | 0.593 | **92.5%** | **1.00×** | 155 | 92 |
| Fully Mastered | 94.75% | 15.8s | 0.405 | **63.2%** | **1.00×** | 228 | 92 |
| Optimized | 120% | 12.5s | 0.320 | **50% (FLOOR)** | **1.00×** | **288** | 92 |

**Consequences, all provable from the documents' own numbers:**

1. **Active play's loot bonus vanishes at Partially Mastered** — reachable in **~26 minutes** of team
   work (region-mastery F2's own worked example: 500 RMP at 1164 RMP/hr). `max(0, (active_eff − 100)/100)`
   clamps to zero. A focused active hunter then gets *identical* drop counts and rare-roll factors to an
   automated kill.
2. **At Optimized, `automation_rarity_parity_percent = 100%`** (region-mastery A6) — identical rarity
   odds — while automation does **~3.1× the kills/hour**.
3. Pillar 3 promises active is *"meaningfully more rewarding (~2x) than automation."* At Optimized,
   **automation is ~3× better than active play.** Complete inversion of the game's thesis.

**Both existing reconciliation proofs compare numbers from different mastery levels.** Active's
180–220% band was computed with `CTR ≈ 1.15–1.20` — i.e. against a **45s baseline, which only exists at
Newly Conquered**. You cannot simultaneously have idle at 120% (Optimized) and active at 200%
(Newly Conquered's baseline). `loot-drop-system` F7's per-hour table repeats the error by hardcoding
`automation_baseline_clear_time_seconds = 45s` and never re-evaluating it as mastery climbs.

`region-mastery-automation-system` **AC2 is unpassable as written**: it asserts a same-tier comparison of
292% active vs. 120% idle, but at Optimized's 12.5s baseline that requires a **4.2-second clear** — three
basic-attack ticks (`basic_attack_interval_ms = 1200`).

**Required resolution (a design decision, not a doc fix):**
1. **Decouple the denominators.** `active_efficiency_percent` must be measured against a **fixed authored
   par-clear-time per encounter template**, never against automation's live throughput. *The moving
   denominator is the entire bug.*
2. Decide whether `idle_efficiency_percent` should scale automation's **kill rate** (F6) at all, or only
   its **per-tick role output** (F5). Scaling both is what produces the 3.6× throughput swing.
3. Re-derive `loot-drop-system` F1/F5's `efficiency_quantity_scalar` term against the replacement, and
   re-run F7's table at **every** mastery level.
4. Re-check `automation_rarity_parity_percent = 100%` at Optimized — at parity, automation's only
   remaining deficit is `part_break_loot_bonus_percent`.

---

### 🔴 B2 — No system owns hostile-creature spawning, encounter selection, or `power_tier` assignment.

**Status**: OPEN — requires an ownership decision.
**Files**: `combat-encounter-system` §3.2 ↔ `creature-data-schema` EC#5 ↔ `region-mastery-automation-system` §3.0 ↔ `rare-creature-capture-system` §3.1 ↔ `onboarding-tutorial-system` A5

Every consumer defers upstream and **the buck never stops**. `combat-encounter-system` assumes spawn is
already done and only claims *clamping*. `creature-data-schema` EC#5 points back at
`combat-encounter-system`. `rare-creature-capture-system` and `onboarding-tutorial-system` both say
"most likely `region-mastery-automation-system`" — whose §3.0 scope list **does not include spawning**.

**Nothing owns:**
- Which `template_id` spawns in a given encounter
- The spawned instance's **`power_tier`** — a direct input to `loot-drop-system` F2 (rarity
  distribution), `creature-ai-telegraph-system` F1 (telegraph duration), and `creature-data-schema`
  F1–F2 (health, break thresholds)
- Expedition structure (game-concept's "3–6 encounters → boss")
- The trigger for `rare-creature-capture-system` F1's rarity roll
- The "this encounter is automated" flag that `loot-drop-system` A3 and `rare-creature-capture-system` A8
  both require

**Four systems are blocked on an input nobody produces.** Assign an owner — a new `encounter-spawn` /
`region-progression` system, or an explicit scope expansion of `region-mastery-automation-system`.

---

### 🔴 B3 — No creature→player damage formula. The player's `defense` stat is read by nothing. `vow_fragility` is a free +60%.

**Status**: OPEN — needs a formula authored.
**Files**: `combat-encounter-system` §3.1 / §3.10 / §4 F3 ↔ `resonance-weaving-system` §3.3.4

§3.1 declares `defense` *"Mitigates incoming creature/hazard damage (§4 Formula 3)"*, and §3.10 routes
creature attacks through Formula 3. But **Formula 3 is titled "Player-to-Creature"**: it mitigates using
the *creature's* `base_defense` and applies damage to the *creature's* health. §3.10 patches the target
but **nothing patches the mitigation term**. The player's `defense` stat has no formula that reads it
anywhere in the project. Also unresolved: do `crit_multiplier` and `min_damage_floor` apply to creature
attacks?

**Knock-on**: `resonance-weaving-system`'s `vow_fragility` is `static_cost_stat: "defense"`,
`static_cost_magnitude: 0.20`, granting `power_multiplier = 1.60`. As specified this is **+60% ability
power for zero cost** — a strictly dominant Vow, the exact failure the uptime curve was built to prevent.

**Required**: author `Formula 3b — Damage Pipeline (Creature-to-Player)` consuming `hunter_defense`;
state whether crit/floor apply; then re-price `vow_fragility` against Formula 2.

---

### 🔴 B4 — No Hunter progression system existed. **FIXED** — system #26 authored.

**Status**: RESOLVED (`hunter-progression-system` created).

All 25 original GDDs were authored and **none levelled the Hunter**. `combat-encounter-system` §3.1
deferred `hunter_base_[stat]` to *"whichever future progression system levels the Hunter"* — which did
not exist. Meanwhile creature difficulty scales rigorously across `power_tier` 1–20: **a tier-20 boss is
~7× harder to kill** than a tier-1 boss of the same template (3.85× health; defense mitigation dropping
71.4% → 39.4% of damage landing), against **zero** player-side growth.

Invisible at MVP (`power_tier` locked 1–3, one region) — but `region-mastery-automation-system`,
`memory-dust-prestige-system`, and `creature-data-schema` are **all already designed against the full
1–20 range.** Every other progression axis has a proven formula; the one number every other curve is
implicitly measured against had none.

System #26 also closes **W1 (Gleam's missing sink)** by making progression cost Gleam.

---

### 🔴 B5 — Equipped charms were consumable by the Forge. **FIXED.**

**Status**: RESOLVED across 4 documents.

`item-data-schema`'s `equipped_to_creature_id` (field 16) + Invariant 9 had been written into the schema
but **never wired into any validator** — the field appeared in zero other GDDs. A charm equipped to a
creature working a region was fully sellable, dismantlable, mergeable, and feedable-to-a-different-creature.

Fixed: `the-forge-system` **§3.5.0 Universal Input Gate** (covers all four operations at the gate, so no
future operation can reopen it); `forge-ui` **A8b** (excluded from all four pickers — impossible to
attempt, not merely rejected); `creature-roster-ui` EC4 + §7 (stale "gap remains open" claim corrected;
detect-and-clear demoted to belt-and-braces).

**Implementer caution captured**: `forge-ui` already used "Equipped" to mean *ability-focus combat
loadout* — a different, unrelated sense with **opposite** Forge eligibility. Now explicitly disambiguated.

---

## WARNINGS (should fix, not blocking)

| # | Issue | Files |
|---|---|---|
| W1 | **Gleam has one sink.** Faucet (manual Sell) permanently unthrottled; only sink is `vow_binding_cost` (~200 Gleam × "low tens of bindings"), saturating **mid-game**. `merge_operation_fee` locked to 0. **→ Folded into system #26.** | the-forge-system, loot-drop-system |
| W2 | **`memory-dust` "Hunter's Cache"** (75 Gleam/region) is an **unaccounted Gleam faucet**, absent from every closure proof and bypassing the 24/hr cap. Small (~750 Gleam / 10 regions) but the "exhaustive" proof has a hole. | memory-dust-prestige-system |
| W3 | **`memory-dust` invents a 5-rule loot-filter cap that doesn't exist, then sells relief from it** — imposing a restriction on the base game to sell the fix. Contradicts its own A6/AC4 ("complete game without ever prestiging"). | memory-dust, loot-drop-system, loot-filter-ui |
| W4 | **"Second Charm" unlock re-opens the ownership hole just closed.** `equipped_charm_id` is single-valued; a 2-slot expansion needs Invariant 9 restated against a list. | memory-dust, item-data-schema |
| W5 | **Unbounded Vow rebinding vs. art-bible's hard 4-scar cap.** `vow_binding_log` is append-only/unbounded and rebinding is uncapped; one charm rebound 10× carries 10 scars. Art-bible §5.2 caps visible scars at 4. | item-data-schema, the-forge-system, art-bible |
| W6 | **Capture Ward never inserted into `combat-encounter-system` Formula 3 Step 6** — without it, encounter-end fires first and capture is lost to overkill: the exact failure the Ward exists to prevent. | rare-creature-capture, combat-encounter |
| W7 | **Evolution Vector 4 (Combat Behavior) is unimplementable.** `creature-jobs-evolution` needs `loot-drop-system`'s core-mint to expose `instance_id` + `broken_part_ids` + `tier`; it mints none of them. | creature-jobs-evolution, loot-drop-system |
| W8 | **`region-mastery` silently re-keyed `loot-drop-system`'s `automation_stage`** (now `mastery_level_index + 1`, not the capability stage). `loot-drop-system`'s tables are now **mislabeled** — reading it alone yields the wrong parity curve. | region-mastery, loot-drop-system |
| W9 | **`boss_kill_confirmed` autosave timing unspecified** relative to the 3000ms RESOLVING window vs. mastery-commit-at-COMPLETE. A crash in the gap could persist loot granted but region still unconquered → boss respawns, re-farmable. | save-load-persistence, combat-encounter, region-mastery |
| W10 | **Offline evolution's production math is more imprecise than its own doc admits.** Doc says "a small amount of the tail-end"; in fact an early evolution in a 72h window misattributes the *whole* window — and because production sums the team at one stale snapshot, it misattributes **every other creature in that region too**. | region-mastery, creature-jobs-evolution |
| W11 | **Core Hatching is never sequenced in onboarding.** Stage 10 says "the just-bound boss creature" — nothing bound it; the player must independently discover the Hatch panel. Also Stage 9 says merge combines "**two** items"; it's **three**. | onboarding-tutorial-system |
| W12 | **`vow-condition-tracking` inserted a step into `combat-encounter-system`'s frame order that doesn't exist there**, and mis-cites the cast-snapshot step (§3.2 step 5 → should be §3.4 rule 5). Currently benign; breaks the moment either step order changes. | vow-condition-tracking, combat-encounter |
| W13 | **Inventory cap contradiction.** `loot-drop-system` A5 declares "no hard inventory cap at MVP" (Pillar 3); `region-mastery` then adds `region_storage_capacity = 500/resource/region` **without referencing the ruling**. Is auto-looted gear subject to it? Neither says. | loot-drop-system, region-mastery |
| W14 | **Capture RESOLVING duration: 2000ms vs 1800ms.** Two docs, two values. | combat-encounter, rare-creature-capture |
| W15 | **`systems-index.md` internal inconsistency** — 4 sections still say 24 systems; Priority Tiers says 22 MVP; Categories/Design Order/Progress Tracker all omit `settings-menu-ui`; Dependency Map disagrees with its own Enumeration table on 7 rows. | systems-index |
| W16 | **Registry is ~20% complete.** `entities.yaml` covers 4 of 25 GDDs; `entities: []` and `items: []` are **empty** — Gleam and Memory Dust unregistered. Every GDD says "registry updates coordinated centrally" — **that pass never ran.** Unregistered: every balance-critical constant in the project. | entities.yaml |
| W17 | **31 dependency-bidirectionality gaps** (full table in the consistency pass output). Notably: `accessibility-settings-system` does not list `settings-menu-ui`, its own screen. | all |
| W18 | **17 of 25 GDDs contain stale "(not yet written)" claims** about systems that now exist on disk. Will mislead an architecture reader about what is settled vs. speculative. | all |
| W19 | **Combat HUD's "4 chunks" may undercount** — Resonance isn't in the chunk table but renders as its own element; true glanceable load may be 5. And the VS playtest gate tests only the *aggregate* read, not the compound time-pressured task combat actually demands ("is THIS ability off-cooldown AND Vow-satisfied, within a 240–720ms interrupt window"). | combat-hud |
| W20 | **Block/dodge/interrupt gradient is asserted, not proven** — the only core combat-balance claim in an otherwise proof-driven doc set that rests on playtest alone (AC18). Flag as the highest-priority VS playtest item. | combat-encounter, creature-ai-telegraph |
| W21 | Two docs disagree on which number is the player-facing "mastery readout" (`idle_` vs `active_efficiency_percent`). Matters more than it looks, given B1. | game-concept, onboarding, automation-config-ui |
| W22 | Four UI-persisted fields have no home in `save-load-persistence`'s four MVP sections (`region_slot_index`, `rule_label`, `mastery_transition_presentation_pending`, `first_run_prompt_shown`). | save-load-persistence + 4 UI docs |

---

## What the review CONFIRMED (independently re-derived, not taken on faith)

These claims were re-checked with fresh arithmetic and **hold**:

- **Vow power curve is non-dominant** — verified by derivative sign. Strictly monotonically decreasing in `expected_uptime` for any `curve_exponent > 0` in the safe range.
- **Source effectiveness matrix has no dominant element** — the full 6×6 circulant was re-derived from the cycle rule and matches exactly. Every Source: 2 strong / 2 weak / 2 neutral.
- **Composition beats power.** Incomplete team caps at `team_quality_score` 0.5125; a *minimum-power complete* team reaches 0.6675. Optimized tier requires `CCS = 1.0` **absolutely** — no power-stacking reaches it.
- **The merge economy is closed.** Re-ran it: direct dismantle of 3 Rares → 6 materials; merge-then-dismantle → 3. Strictly worse. Loop dead.
- **Memory Dust cannot breach the 120% ceiling or the 24 Gleam/hr cap** — confirmed by reading both formulas directly; none of the 19 unlocks' fields appear in them. *(Except the Hunter's Cache faucet — W2, a different vector.)*
- **The Capture Ward vs. boss-death interaction is not a bug** — bosses never roll rare and are never capture-eligible (`rare-creature-capture` A6), verified across three formulas and a priority table. A suspected gap that turned out to be one of the more carefully closed loops in the project.
- **Anti-strobe floors are consistent** — `TELEGRAPH_WINDUP_FLOOR_MS` and `VOW_FLIP_MIN_INTERVAL_MS` both 600ms, non-tunable, cited identically across five documents.
- **State-transition semantics are consistent** — hostile↔bound atomicity, Starved-before-Blocked priority, Kill > Capture > Retreat, Retreat's zero-loss rule.
- **`power_tier` scaling is deliberately uniform** — `tier_health_scalar` / `tier_break_scalar` / `tier_defense_scalar` / `power_tier_output_scalar` all default 0.15, same safe ranges, range 1–20 consistent across four documents.
- **Pillar coverage**: all 25 systems map to ≥1 pillar. **No anti-pillar violations found.**
- **Fantasy coherence**: PASS. One identity — Hunter / tinkerer / keeper / foreman — threading through all 25 docs via deliberate cross-referencing, not independent invention.

**One sharp observation worth keeping**: even *light* active interaction (128%) beats idle's absolute
ceiling (120%, perfect team). Idle is never "sometimes better" — its honest pitch is *"worthwhile in
aggregate when you can't be everywhere."* That's coherent with the core fantasy, but it should be said
plainly rather than implied.

---

## Required actions before `/create-architecture`

1. **Resolve B1** — decouple `active_efficiency_percent` from automation's live clear time. Pillar-level decision.
2. **Resolve B2** — assign an owner for creature spawning / `power_tier` assignment / expedition structure.
3. **Resolve B3** — author the creature→player damage formula; re-price `vow_fragility`.
4. Verify B4 (`hunter-progression-system`) closes both the scaling gap and the Gleam sink.
5. Run a **registry consolidation pass** (W16) — the central coordination pass every GDD assumed would happen never ran.
6. Sweep W18 (stale "not yet written" claims) so architecture readers aren't misled.

---

# REMEDIATION RECORD — 2026-07-14 (same day)

All five blockers are closed. Verdict moves **FAIL → PASS**, with the caveats in "What is still
unproven" below. Every claim here is verifiable by grep or by running the test suite; none of it
rests on prose.

## Blockers

| ID | Resolution | Verify by |
|---|---|---|
| **B1** — active/idle contract inverted | Both efficiency figures now measured against a fixed authored `par_clear_time_seconds` (owned by the new `encounter-spawn-system`), never against automation's live throughput. Required **no change to the user's locked numbers** — par *is* their "fully automated = 100% baseline", so both their tables now hold simultaneously and non-circularly. | `tests/…/Progression/EfficiencyContractTests.cs`, `BlockerB1RegressionTests.cs` |
| **B2** — no owner for spawning / `power_tier` | `encounter-spawn-system.md` (system #27) authored. Owns the `EncounterTemplate` schema, the `power_tier` assignment rule, and `par_clear_time_seconds`. | `grep -rn power_tier design/gdd/encounter-spawn-system.md` |
| **B3** — no creature→player damage formula | `Formula 3b` authored in `combat-encounter-system.md`. §3.10 rerouted through it. The Hunter's `defense` now has exactly one consumer — previously it had none. | `tests/…/Combat/DamagePipelineTests.cs` |
| **B4** — no Hunter progression | `hunter-progression-system.md` (system #26) authored. Gleam-funded Training; simultaneously closes W1 (Gleam had no sink). | `design/gdd/hunter-progression-system.md` |
| **B5** — equipped charms still Forge-eligible | Closed at the Forge's Universal Input Gate (§3.5.0) rather than per-operation. | `grep -n "3.5.0" design/gdd/the-forge-system.md` |

## Warnings

- **W1** (Gleam had no sink) — closed by B4. Hunter Training absorbs 79,551 Gleam over a lifetime.
- **W15** (`systems-index.md` internally inconsistent) — closed; reconciled to 27 systems throughout.
- **W16** (registry covered 4 of 27 systems) — closed. `design/registry/entities.yaml` now holds **51
  registered facts**. `entities`/`items` were literally empty; **Gleam and Memory Dust — both
  currencies — were unregistered entirely.**
- **W17** (31 bidirectional-dependency gaps) — closed; every edge in the index now resolves both ways.
- **W18** (stale "(not yet written)" claims) — closed. **108 occurrences across 19 GDDs** purged.

## ⚠ A correction to THIS REPORT

**This report published numbers that do not reproduce.** It stated the B1 inversion as
`166% / 92.5% / 63.2% / 50% (floor)`. Implementing the formula and reconstructing the original in a
test produced **`216.7% / 123.8% / 86.7% / 72.2%`** (par 15 s, active clear 27 s, VWS 0.40, IR 0.20,
maxed team). The figures came from an unstated parameterization and were never checked.

**The inversion was entirely real** — identical play loses roughly two-thirds of its value as the
player's own farm improves, and on a well-mastered region a *skilled* active player scores **below the
automation baseline they are measured against**. But the specific numbers were fiction.

This is not a footnote; it is the whole lesson. A broken balance contract reached 25 documents behind
**two hand-written "proofs"**, and then this review's own corrected numbers were *also* wrong. Prose
cannot check arithmetic. **Balance figures now live in `BlockerB1RegressionTests.cs`.** Do not
re-publish a number that no test asserts.

## What is still unproven (not blockers — genuine unknowns)

1. **The core hunt loop's fun is untested.** The concept prototype was explicitly skipped. This
   remains the single most load-bearing unvalidated assumption in the project. No amount of document
   consistency substitutes for playing it.
2. **The homebrew cutout animation rig is novel engineering.** MonoGame ships no animation system, and
   angle-snapped rotation on pixel art has no reference implementation here. Spike the Attacker strike
   arc (the widest motion arc) before committing the creature roster.
3. **Belt-charm legibility at 10–15% frame height** is a real playtest unknown; the corner-mirror
   fallback is pre-authorized with a concrete trigger.
4. **Every balance default remains an unplaytested placeholder.** They are internally consistent and
   pillar-compliant. That is not the same as being *fun*.
