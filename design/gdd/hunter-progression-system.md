# GDD: Hunter Progression System

## Document Status

- **Status**: Complete — all 8 sections authored
- **Created**: 2026-07-14
- **Owned By**: systems-designer
- **Review Mode**: rush — authored autonomously this session per the project's standing "run all
  commands step by step, don't wait for approval" instruction recorded in
  `production/session-state/active.md`. Every ambiguity the source material left open is resolved
  below with an explicit, flagged design call rather than a placeholder. Validate at
  `/design-review` and `/gate-check` before Production.
- **Priority**: MVP, Core tier — system #26, created reactively by `gdd-cross-review-2026-07-14.md`
  to close **Blocker B4** (no system wrote the Hunter's combat stats) and **Warning W1** (Gleam had
  only one, mid-game-saturating sink). Depends on `combat-encounter-system`, `the-forge-system`,
  `loot-drop-system`, `region-mastery-automation-system` (`systems-index.md` entry #26).

## Source Material Read

`design/gdd/combat-encounter-system.md` (§3.1 Hunter Stat Catalog in full, Formulas 1–3 and the
newly-authored Formula 3b — Damage Pipeline, Creature-to-Player), `design/gdd/the-forge-system.md`
(Gleam faucets/sinks in full — §3.5–3.7, §4 Formulas 6–8, §7), `design/gdd/game-concept.md` (full),
`design/gdd/memory-dust-prestige-system.md` (the "nothing resets" contract, the 6 unlock
categories, the 19-unlock/660-MD tree structure), `design/gdd/systems-index.md` (entry #26 and
dependency map), `design/gdd/region-mastery-automation-system.md` (Automation Capability Stages
1–4, `region_auto_sell_gleam_cap_per_hour` and its Formula 7 proof), `design/gdd/loot-drop-system.md`
(Formula 7/8 faucet-rate worked tables — active vs. automated Gleam-equivalent value per hour),
`design/gdd/creature-data-schema.md` (`power_tier` 1–20 scaling formulas), `design/gdd/item-data-schema.md`
(§3.6 `Modifier.stat` convention), `design/gdd/resonance-weaving-system.md` (Source→stat mapping,
Formula 3 ability power), `design/gdd/gdd-cross-review-2026-07-14.md` (B4/W1/B3 full text — the
brief this document exists to satisfy).

## Assumptions Log

Design calls made without a live approval loop this session; flagged here rather than hidden in
prose, per this project's rush-mode convention.

| # | Assumption | Why |
|---|---|---|
| A1 | All 9 canonical Hunter stats are trainable from MVP day one, even though MVP's curated Weaving list won't exercise every Source. | `combat-encounter-system` §3.1 fixes the catalog at 9 stats, not phased by MVP/VS; trimming it here would create a second, competing scope cut. A player simply won't rationally train a stat their current build doesn't use (§3.7, Edge Case 12). |
| A2 | Training Rank purchases are **permanent — no respec, no refund.** | Matches the project's own established pattern for high-ceremony currency spends (`the-forge-system` Vow-binding, `memory-dust-prestige-system` A5) rather than inventing a third, inconsistent convention. |
| A3 | Training purchases are only legal outside an active encounter (Hub/Forge only). | Closes a mid-combat `max_health`-purchase heal exploit at the design level (Edge Case 3/4), mirroring `resonance-weaving-system`'s own "re-slot only between encounters" precedent for loadout changes. |
| A4 | Training purchases are **never automated** — no future Automation Capability Stage ever auto-spends Gleam on Training. | Preserves Pillar 2: automation handles combat/loot/sell/craft, never irreversible build/progression *spending decisions* (the same line `the-forge-system` already draws around Vow-binding). |
| A5 | `hunter_base_max_health`'s starting value (100) and `hunter_base_defense`'s mitigation-curve calibration are this document's own authority, cross-checked against the now-live `combat-encounter-system` Formula 3b (`100 / (100 + effective_hunter_defense)`, hard `min_incoming_damage_floor`). | Nothing else in the project defines a player HP/defense baseline — `combat-encounter-system` §3.1 explicitly deferred `hunter_base_[stat]` to "whichever future progression system levels the Hunter." That is this document. |
| A6 | The active-vs-idle Gleam **realization** rate assumes a player converts roughly half of acquired item value to Gleam via Sell (the rest is equipped, merged, or dismantled for materials), applied identically to both active and idle scenarios for a fair comparison. | No other GDD specifies this fraction; item value ≠ realized currency is `loot-drop-system` Formula 8's own load-bearing distinction, and applying the same discount to both modes keeps the resulting active/idle ratio honestly comparable rather than an artifact of asymmetric assumptions. |
| A7 | The Idle-Can-Progress Proof (§4.6) computes against `region_auto_sell_gleam_cap_per_hour = 24` as an explicit **pessimistic floor**, even though Stage 3 (auto-sell) is itself VS-deferred and does not exist at MVP. | Directive from the brief. At true MVP scope, manual Sell is uncapped and idle Gleam realization is bounded only by how often the player visits the Forge — strictly faster than this floor. Both numbers are shown (§4.6) so neither overstates nor understates the real MVP economy. |

---

## 1. Overview

Hunter Progression is the system that makes the player character actually get stronger. Every one
of the 9 stats `combat-encounter-system` reads (`attack_power`, `focus`, `vitality`, `engineering`,
`guile`, `resonance_affinity`, `max_health`, `defense`, `critical_chance`) has, until this document,
had no writer — 25 other GDDs read a value nothing ever set. This system closes that gap with a
single mechanic: **Training**. Gleam — the Forge's currency, previously a faucet with almost nothing
to spend it on — buys permanent Training Ranks in each of the 9 stats, one rank at a time, at a cost
that rises geometrically per rank within that stat and hard-caps at a fixed ceiling per stat. The
same Gleam balance a player would otherwise hoard toward the Forge's `vow_binding_cost` is now in
constant, meaningful tension with Training — one currency, two competing, permanent, unrefundable
spends. Training is deliberately not the game's build-identity layer (Resonance Weaving's Source ×
Form × Vow system owns that, per game-concept.md's own inspiration notes); it is the *floor* that
rises to keep pace with `power_tier` scaling, so gear, Vows, and precision execution keep doing the
work of actually winning fights.

## 2. Player Fantasy

Every other currency decision in this game buys you a specific, chosen *thing* — a Vow seared onto
a charm, a merge that might roll better, a creature nudged toward a new evolution branch. Training
is the one Gleam sink that buys nothing specific at all: it just makes the Hunter underneath all of
that gear and all of those Vows a little more capable, permanently, in a direction you choose. The
fantasy is quiet competence compounding — the sense that even the fights you're not winning cleanly
yet are becoming winnable, one rank at a time, through nothing more dramatic than banking loot and
choosing to spend it here instead of at the Forge's altar. That's also exactly the tension the
system is built to create: every time a player stands at the Forge with a healthy Gleam balance,
they are making a real, felt choice between *hardening the floor* (Training) and *committing to the
next build* (a Vow-binding) — and because both are permanent and both draw from the same account,
neither ever feels free. Crucially, the fantasy this system explicitly does **not** deliver is "buy
your way past a hard fight." A maxed-out Training investment is a sturdier foundation, not a
shortcut — the telegraph still has to be read, the weak point still has to be hit. Precision remains
the thing that wins encounters; Training just makes sure the player is never structurally locked out
of trying.

## 3. Detailed Rules

### 3.1 Ownership and the Hunter Stat Catalog (Recap)

This document is the sole **writer** of `hunter_base_[stat]` for all 9 stats in
`combat-encounter-system` §3.1's Hunter Stat Catalog. It does not redefine, rename, or extend that
catalog — the 9 keys (`attack_power`, `focus`, `vitality`, `engineering`, `guile`,
`resonance_affinity`, `max_health`, `defense`, `critical_chance`) are locked exactly as that document
states them (A1). Every other document's existing formula for combining `hunter_base_[stat]` with
gear modifiers (`hunter_base_[stat] + Σ flat gear modifiers`, item-data-schema §3.6 convention) is
unchanged — this document supplies only the left-hand term of that sum.

### 3.2 The Core Mechanic — Training Ranks

Each of the 9 stats has its own independent **Training Rank** counter, `ranks_purchased[stat]`,
starting at `0` and capped at `stat_rank_cap` (default `60`, uniform across all 9 stats, §7). A
purchase always buys exactly the *next* rank in one stat — ranks within a stat are strictly
sequential (you cannot buy rank 12 before rank 11), matching the same "ranks within a line are
sequential" convention `memory-dust-prestige-system` already established for its own unlock tree.
Every purchase is **permanent** (A2) — there is no sell-back, refund, or reallocation mechanic
anywhere in this system.

### 3.3 Purchase Rule and Encounter-Gating

A Training purchase is legal only when `combat-encounter-system`'s encounter state machine (§3.2
there) is **not** `ACTIVE` or `RESOLVING` (A3) — i.e., Training happens at the Forge/hub between
encounters, identically to how `resonance-weaving-system` already gates loadout changes. This is a
structural rule, not a UI convenience: it exists specifically so a `max_health` purchase can never
be used as an emergency mid-fight heal (Edge Case 3/4 make the resulting HP-delta rule explicit).

### 3.4 The Nine Stat Tracks

| Stat | Starting Value | Yield / Rank | Rank Cap | Max Contribution (this doc alone) |
|---|---|---|---|---|
| `attack_power` | 0 | +1.0 | 60 | +60 |
| `focus` | 0 | +1.0 | 60 | +60 |
| `vitality` | 0 | +1.0 | 60 | +60 |
| `engineering` | 0 | +1.0 | 60 | +60 |
| `guile` | 0 | +1.0 | 60 | +60 |
| `resonance_affinity` | 0 | +1.0 | 60 | +60 |
| `max_health` | 100 | +10 | 60 | +600 |
| `defense` | 0 | +2.0 | 60 | +120 |
| `critical_chance` | 0.0 | +0.005 (0.5pp) | 60 | +0.30 (30pp) |

The 6 Source-linked stats (`attack_power` through `resonance_affinity`) share an identical
yield/cap shape by design — they feed the same coefficient (`source_scaling_coefficient = 0.008`,
`combat-encounter-system` Formula 1 / `resonance-weaving-system` Formula 3) and there is no reason
for one Source to structurally out-scale another. `max_health`, `defense`, and `critical_chance` are
each a different unit of account and are calibrated individually (§4.2, §4.7).

### 3.5 Hunter Level (Derived Readout)

`hunter_level` (§4.4) is a **cosmetic, read-only** number derived from total ranks purchased across
all 9 stats combined. It gates nothing, unlocks nothing, and is never itself stored as authoritative
state — it exists purely to give players the "explicit, legible mastery curve" game-concept.md's
Player Motivation Profile calls for, without adding a second mechanical resource to reason about.

### 3.6 Base vs. Gear — Division of Responsibility

`item-data-schema` Formula 3 already establishes gear's own rarity-value scaling as "geometric,
uncapped... idle-game power curves are expected to compound over long play sessions." This document
deliberately does **not** compete with that: `hunter_base_[stat]` (this document, capped, §4.7's
non-dominance proof) is the floor; gear modifiers (item-data-schema, uncapped) are the long tail.
Splitting responsibility this way is what lets this document impose a hard structural ceiling
(required by Pillar 4) without also having to cap gear itself, which would contradict every other
document's existing "gear power compounds" design.

### 3.7 Relationship to Resonance Weaving and Vows

game-concept.md's own Inspiration table is explicit that this game's build-identity trade-offs are
"explicit Vows/enchantment behaviors rather than passive tree nodes" — a direct caution against
treating a point-allocation system like this one as a second, competing build-identity layer. This
document takes that caution at face value: **Training is not a build choice on par with a Vow.** It
does not restrict, gate, or unlock any ability, Source, Form, or Vow. Its only structural link to
Pillar 4 is economic — because Training and Vow-binding draw from the same finite, permanent-spend
Gleam balance, a player is always weighing "grow my numbers" against "commit to a build," which is a
real, felt trade-off even though Training itself is not the thing carrying build identity. That
identity work stays entirely owned by `resonance-weaving-system`.

### 3.8 Relationship to Memory Dust / Prestige

None of Memory Dust's 6 unlock categories (Automation Rules, Loot Filters, Starting Bonuses,
Evolution Branches, Forge Upgrades, Resonance Weaving Components) touch Hunter stats — there is no
overlap to reconcile. Per `memory-dust-prestige-system` §3.1's "nothing resets" contract, this
document explicitly states its own position (that document leaves dependents to declare their own
stance): **no Memory Dust prestige event ever reads, writes, or resets any `ranks_purchased[stat]`
or derived `hunter_base_[stat]` value.** Training progress is permanent for the life of the save,
exactly like every other system that document already audits (Edge Case 8).

### 3.9 Relationship to Region Mastery Automation and the Gleam Faucet

This document does not model the Gleam faucet itself — `the-forge-system` (Sell/Dismantle/Feed) and
`region-mastery-automation-system` (the Stage-3 `region_auto_sell_gleam_cap_per_hour` throttle,
VS-deferred) own that. This document only consumes their published rates to prove its own cost
curve is well-calibrated against both active and idle play (§4.5, §4.6).

## 4. Formulas

All formulas share the project's round-half-up convention for integer outputs.

### 4.1 Formula 1 — Training Rank Cost

```
gleam_cost(n) = round(base_training_cost × training_growth_rate ^ n)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `n` | int | 0–59 | Ranks already purchased in *this* stat before the purchase being priced (0-indexed: `n=0` prices the 1st rank). |
| `base_training_cost` | float, tuning knob | 10–50 (default 25) | Cost of the very first rank in any stat. |
| `training_growth_rate` | float, tuning knob | 1.03–1.08 (default 1.05) | Per-rank cost escalation within a single stat. |
| `gleam_cost(n)` | int (rounded) | 25–445 at default tuning across `n = 0..59` | Gleam required for one specific rank purchase. |

**Output range**: strictly increasing in `n`, unbounded in principle but evaluated only over
`n ∈ [0, 59]` since `stat_rank_cap = 60` forbids any purchase beyond that.

**Worked examples** (default tuning): rank 1 (`n=0`) = 25 Gleam; rank 10 (`n=9`) = 39 Gleam; rank 30
(`n=29`) = 103 Gleam; rank 60 (`n=59`, the single most expensive Training purchase possible in one
stat) = 445 Gleam — a little over twice `the-forge-system`'s own `vow_binding_cost` (200 Gleam) for
what is deliberately this document's single most expensive individual transaction.

### 4.2 Formula 2 — Effective Hunter Base Stat

```
hunter_base_[stat] = stat_starting_value[stat] + (ranks_purchased[stat] × stat_yield_per_rank[stat])
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `stat_starting_value[stat]` | float, per-stat constant | §3.4 table (0 for 8 stats, 100 for `max_health`) | Value at `ranks_purchased = 0`. |
| `ranks_purchased[stat]` | int | 0–60 | Current Training Rank in that stat. |
| `stat_yield_per_rank[stat]` | float, per-stat constant | §3.4 table | Contribution per rank. |
| `hunter_base_[stat]` | float | §3.4's "Max Contribution" column, offset by starting value | The exact opaque non-negative float `combat-encounter-system` §3.1 expects. |

**Output range**: monotonically non-decreasing, hard-capped per stat at the §3.4 maximum — this is
the literal mechanism of the required "hard structural ceiling."

**Worked example**: `ranks_purchased[attack_power] = 60` (fully capped) →
`hunter_base_attack_power = 0 + (60 × 1.0) = 60`. Fed into `combat-encounter-system` Formula 1:
`basic_attack_damage = 12 × (1 + 0.008 × 60) = 12 × 1.48 = 17.76`.

**Defense's diminishing-returns shape (against the now-live Formula 3b)**: `combat-encounter-system`
Formula 3b mitigates incoming damage via `100 / (100 + effective_hunter_defense)`, a hyperbola with
a hard `min_incoming_damage_floor` so `defense` can never reach immunity. Because that curve is
asymptotic, each successive Training rank in `defense` buys *less* mitigation than the last, even
though every rank costs *more* Gleam than the last (Formula 1) — the two curves compound against
over-investment in the same direction:

| `ranks_purchased[defense]` | `hunter_base_defense` | Mitigation Factor | Damage Reduction |
|---|---|---|---|
| 0 | 0 | 1.000 | 0% |
| 15 | 30 | 0.769 | 23.1% |
| 30 | 60 | 0.625 | 37.5% |
| 45 | 90 | 0.526 | 47.4% |
| 60 (cap) | 120 | 0.4545 | 54.5% |

The first 15 ranks buy 23.1 percentage points of reduction; the last 15 ranks (45→60) buy only 7.1
points — under a third as much, for the most expensive Gleam on the curve. This is by design (§4.7).

### 4.3 Formula 3 — Cumulative Cost to Rank N

```
total_cost(N) = base_training_cost × (training_growth_rate ^ N − 1) / (training_growth_rate − 1)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `N` | int | 0–60 | Number of ranks purchased in one stat, from 0. |
| `total_cost(N)` | float (Gleam) | 0–8,839 at default tuning, `N = 0..60` | Cumulative Gleam spent reaching rank `N` in that stat. |

**Output range**: `total_cost(60)` is the maximum any single stat can ever cost —
`25 × (1.05^60 − 1) / 0.05 ≈ 8,839` Gleam. Summed across all 9 independent stat tracks (identical
curve, per §3.4), **full completion of this entire system costs exactly `9 × 8,839 = 79,551` Gleam.**

**Worked example**: `total_cost(20) = 25 × (1.05^20 − 1) / 0.05 = 25 × 33.066 ≈ 827` Gleam — the
cumulative cost to reach rank 20 (a plausible MVP-relevant target, given `power_tier` is locked 1–3)
in one stat.

### 4.4 Formula 4 — Hunter Level (Derived, Cosmetic)

```
hunter_level = floor(total_ranks_purchased_lifetime / hunter_level_ranks_per_level) + 1
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `total_ranks_purchased_lifetime` | int | 0–540 | Sum of `ranks_purchased[stat]` across all 9 stats. |
| `hunter_level_ranks_per_level` | int, tuning knob | (default 10) | Ranks per displayed level. |
| `hunter_level` | int | 1–55 | Display-only; gates nothing (§3.5). |

**Output range**: `1` (fresh save) to `55` (full completion, `540 / 10 + 1`).

**Worked example**: 23 ranks purchased across all stats combined → `hunter_level = floor(23/10) + 1
= 3`.

### 4.5 Gleam Sink Math (Required Proof)

**Faucet inputs** (from `loot-drop-system` Formula 7's own worked table, item-value-equivalent per
hour): Automation Stage 1–2, unattended ≈ 7,488–7,608/hr; Active, focused (CTR≈1.175) ≈ 13,245/hr;
Active, peak skill (CTR≈1.35) ≈ 19,395/hr. Applying A6's 50%-realized-as-Gleam discount uniformly:

| Mode | Item value/hr | Realized Gleam/hr (×0.5, A6) |
|---|---|---|
| Idle (Stage 1–2, cashed in via manual Sell) | ≈7,500 | **≈3,750** |
| Active, focused | ≈13,245 | **≈6,620** |
| Active, peak skill | ≈19,395 | **≈9,700** |

**Time to fully absorb the entire sink (79,551 Gleam, all 9 stats maxed)**:

| Mode | Time to full completion |
|---|---|
| Active, peak skill | 79,551 / 9,700 ≈ **8.2 hours** |
| Active, focused | 79,551 / 6,620 ≈ **12.0 hours** |
| Idle, cashed in periodically | 79,551 / 3,750 ≈ **21.2 hours of elapsed real time** |

**Conclusion**: unlike `vow_binding_cost` alone (≈200 Gleam × a realistic lifetime cadence of "low
tens of bindings" ≈ 4,000–6,000 Gleam total — reachable in under an hour of focused active play,
the exact W1 defect), this document's total sink (79,551 Gleam) requires **8–12 hours of dedicated
active play**, or day-scale idle accrual, to exhaust — and Training and Vow-binding *compete for the
same balance simultaneously* (§3.7), so realistic full-completion timelines stretch further still.
At MVP scope (one region, `power_tier` locked 1–3), a player does not need anywhere near full
completion to keep pace with content — reaching rank ~20 in 2–3 relevant stats (≈827 Gleam each,
§4.3) is a far more typical early-game target, leaving the steep tail of the curve as genuine
late-game/completionist engagement. W1 is closed: Gleam now has a sink an order of magnitude larger
than the one that was saturating mid-game, and it never fully saturates within a normal MVP session.

### 4.6 Idle-Can-Progress Proof (Required)

Two scenarios, both computed explicitly because they answer different questions.

**Scenario A — Realistic MVP idle** (Automation Stages 1–2 only; Stage 3 auto-sell is VS-deferred
and does not exist yet). Item value accrues uncapped in inventory at ≈7,500/hr (`loot-drop-system`
Formula 7); nothing is lost, matching Pillar 3. The player realizes it as Gleam via a single,
uncapped, manual Forge visit whenever they choose. Applying A6's 50% realization assumption
(≈3,750 realized Gleam per hour of elapsed real time, cashed in whenever convenient):

- First Training rank (25 Gleam) affordable within ≈0.4 minutes of elapsed idle time, realized at
  next Forge visit.
- Full completion of all 9 stats (79,551 Gleam) within ≈21.2 hours of elapsed real time — i.e., a
  player who never actively fights at all can still fully complete Hunter Training in under a day
  of leaving the game running, provided they eventually visit the Forge to cash in.

**Scenario B — Pessimistic worst-case floor** (A7): using `region_auto_sell_gleam_cap_per_hour = 24`
literally, assuming the player *never* visits the Forge manually, ever, and relies solely on a
future, VS-deferred Stage-3 auto-sell trickle:

| Milestone | Cumulative cost | Time at 24 Gleam/hr |
|---|---|---|
| 1st rank, any stat | 25 | ≈1.04 hours |
| Rank 10, one stat | 314 | ≈13.1 hours |
| Rank 20, one stat | 827 | ≈34.5 hours |
| One stat fully maxed (60) | 8,839 | ≈368.3 hours (≈15.3 days) |
| All 9 stats fully maxed | 79,551 | ≈3,314.6 hours (≈138.1 days) |

**Conclusion**: under the single most pessimistic assumption stated in the brief — the Stage-3 cap,
applied as if it were the *only* path to Gleam, which it is not even at MVP — progress is **slower
than active by roughly 1.7–2.8×** in Scenario A (matching the project's own established 170–260%
active-premium band from `loot-drop-system` Formula 7) and dramatically slower in Scenario B, but in
neither scenario is progress ever **blocked**: every rank is eventually affordable, monotonically,
with no floor, ceiling, or gate that a purely idle player cannot eventually cross. Pillar 3 ("idle is
never worthless") holds under the worst case the brief asked for; the realistic MVP case (Scenario
A) is, if anything, generous — which is expected, since Gleam-realization-when-cashed-in is not the
same axis `region-mastery-automation-system`'s own idle-efficiency-percent formula measures and
enforces the ~2× target against (§3.9) — this document's sink math is a different, additional check,
not a restatement of that one.

### 4.7 Non-Dominance Proof (Required)

**Worst case for "out-stat everything"**: a player dumps their entire lifetime Training budget into
exactly the two stats that most directly increase raw damage output — `attack_power` and
`critical_chance` — both maxed to `stat_rank_cap = 60`:

```
attack_power term  = 1 + (source_scaling_coefficient × hunter_base_attack_power)
                    = 1 + (0.008 × 60) = 1.48
critical_chance term (expected-value multiplier)
                    = 1 + (hunter_base_critical_chance × (crit_multiplier − 1))
                    = 1 + (0.30 × (2.0 − 1)) = 1.30
combined multiplier = 1.48 × 1.30 = 1.924
```

Using `combat-encounter-system`'s own `crit_multiplier = 2.0` default (§4 Formula 2/3 there). **This
is the absolute ceiling of what pure Hunter-progression stat purchases can ever contribute to
damage output, at any Gleam balance, by construction** — `stat_rank_cap` makes it impossible to
exceed, regardless of how much Gleam a player has.

Compare against two numbers already established elsewhere in the project:

1. **Skill execution, available for free, from the very first encounter**: landing a hit inside a
   vulnerability window (`creature-ai-telegraph-system` Formula 4, ×1.5) *and* scoring a critical
   hit on that same swing (`combat-encounter-system` Formula 2/3, ×2.0) yields a single-hit
   multiplier of **×3.0** — larger than the entire lifetime-capped Training investment's *average*
   multiplier (×1.924), achievable with zero Gleam spent and zero ranks purchased. Precision remains
   the higher-leverage lever, exactly as Pillar 1 requires.
2. **The scaling gap Training exists to close**: `gdd-cross-review-2026-07-14.md`'s own figure — a
   `power_tier` 20 boss is ≈7× harder to kill than a `power_tier` 1 boss of the same template. Even
   the absolute maximum Hunter-progression investment (×1.924) covers under 28% of that 7× gap. The
   remainder is, and must be, covered by gear (item-data-schema's uncapped rarity scaling), Vow
   multipliers (`resonance-weaving-system`), and skill execution — never by Training alone.

**Conclusion**: a fully-maxed, single-focus Training investment roughly doubles raw damage output at
best, cannot reach even a third of the tier 1→20 difficulty gap on its own, and is smaller than a
single well-executed hit's multiplier. A player cannot "buy" their way past precision combat through
this system — Pillar 1 survives by construction, not by policy.

## 5. Edge Cases

1. **Insufficient Gleam at purchase time.** Rejected outright — zero Gleam deducted, zero rank-state
   change. No partial payment (mirrors `the-forge-system` AC12's Vow-binding precedent).
2. **Target stat already at `stat_rank_cap` (60/60).** Rejected outright as "stat maxed" — a
   distinct, legible UI state from "insufficient funds," never silently charges Gleam.
3. **Purchase attempted while the encounter state machine is `ACTIVE` or `RESOLVING`.** Rejected —
   Training is a Hub/Forge-only action (§3.3, A3).
4. **A `max_health` rank purchase resolves between encounters while `current_hp < previous
   max_health`.** `current_hp` increases by exactly the same delta added to `max_health` (e.g. +10)
   — never snaps to the new max outright. Not a free heal; existing damage persists proportionally
   unchanged.
5. **Invalid/unrecognized stat key submitted.** Rejected, no state change — only the 9 canonical
   keys (§3.1) are legal.
6. **Fresh save, zero ranks purchased anywhere (Day 1).** `hunter_base_[stat]` reads exactly each
   stat's `stat_starting_value` (§3.4) for all 9 stats — never null/undefined. `combat-encounter-system`
   always has a defined, non-negative float to read from the very first encounter.
7. **All 540 ranks across all 9 stats purchased (full completion).** Matches
   `memory-dust-prestige-system`'s own Edge Case 5 precedent: the sink is exhausted, further Gleam
   has no further Training purchase available and simply accumulates for other sinks (Vow-binding,
   future VS/Full-Vision Forge content). Displayed as "Hunter Training: Complete," not an error.
8. **A Memory Dust prestige reset event occurs.** Per §3.8: every `ranks_purchased[stat]` and every
   derived `hunter_base_[stat]` value is explicitly **not** read, written, or reset by that system —
   byte-identical before and after.
9. **Concurrent/duplicate purchase submission.** Not applicable — single-player, strictly sequential
   local state mutation; no concurrency case exists (named explicitly per this project's convention
   of stating even "not applicable" cases rather than omitting them).
10. **Gleam balance is simultaneously eligible for a Training purchase and a pending Vow-binding.**
    No reservation system — Gleam is a single shared account-wide balance read fresh at the moment of
    each purchase's confirmation, exactly as `the-forge-system` already treats it. First confirmed
    action wins; there is no earmarking.
11. **A hypothetical 10th stat is added to the Hunter Stat Catalog in the future.** Out of this
    document's unilateral authority — `combat-encounter-system` §3.1 remains the sole owner of which
    keys exist. This document's §3.4 table would need a new row, authored as an explicit follow-up
    patch, never assumed here.
12. **A player trains a stat their current build doesn't use** (e.g. ranks up `engineering` with no
    `machine`-Source ability equipped). **Allowed — not an error.** A suboptimal-for-current-build
    purchase is still a legitimate player choice (§3.7); this system does not gate purchases by
    loadout relevance.

## 6. Dependencies

**Depends on**:

- **`combat-encounter-system`** — the canonical Hunter Stat Catalog (§3.1), Formula 1 (basic attack
  scaling), Formula 2 (crit roll, `crit_chance_cap = 0.75`), Formula 3 (vulnerability ×1.5, crit
  ×2.0), and the now-live **Formula 3b** (creature-to-player damage, `100 / (100 +
  effective_hunter_defense)` with a hard `min_incoming_damage_floor`) — this document's §4.2/§4.7
  are computed directly against these values.
- **`the-forge-system`** — Gleam is this document's sole cost resource; `sell_conversion_rate`/
  `vow_binding_cost` (200 Gleam) are the comparison points §4.5's sink math is calibrated against.
- **`region-mastery-automation-system`** — `region_auto_sell_gleam_cap_per_hour` (24, Formula 7) is
  reused directly as the pessimistic floor in §4.6 Scenario B.
- **`loot-drop-system`** — Formula 7's worked per-hour item-value figures are the faucet-rate inputs
  for §4.5/§4.6.
- **`creature-data-schema`** — `power_tier` 1–20 scaling is the motivating curve this document exists
  to counterbalance; the "≈7× harder at tier 20" figure is reused directly in §4.7.
- **`memory-dust-prestige-system`** — confirms no unlock-category overlap and the "nothing resets"
  contract this document explicitly opts into (§3.8, Edge Case 8).
- **`item-data-schema`** — the `Modifier.stat` string-key convention this document's 9 keys match
  exactly, and the base-vs-gear division of responsibility (§3.6).
- **`resonance-weaving-system`** — the Source→stat mapping and Formula 3 (`ability_base_power`),
  both of which consume `hunter_base_[stat]` as an external input this document now supplies.
- **`save-load-persistence`** (not yet authored) — must persist `ranks_purchased[stat]` (9 integers)
  per save; this document assumes but does not specify the persistence mechanism.

**Depended on by** (bidirectional):

- **`combat-encounter-system`** — now has a writer for the `hunter_base_[stat]` field its own §3.1
  explicitly deferred to "whichever future progression system levels the Hunter."
- **`resonance-weaving-system`** — Formula 3's `scaling_stat_value` input is now backed by a real,
  boundedly-growing source.
- **A future `hunter-progression-ui`** (not yet named in `systems-index.md`) — the presentation
  layer for the Training panel this document specifies mechanically only, not visually.

## 7. Tuning Knobs

| Knob | Default | Safe Range | Affects | MVP / VS | Notes |
|---|---|---|---|---|---|
| `base_training_cost` | 25 Gleam | 10–50 | Formula 1 | MVP-locked default, retunable | ≈1 Common weapon's `sell_value` (18 Gleam) so the first purchase is affordable in a single early encounter |
| `training_growth_rate` | 1.05 (5%/rank) | 1.03–1.08 | Formula 1, sink absorption | MVP-locked default, retunable | Primary sink-absorption lever — re-run §4.5's sink math on any change |
| `stat_rank_cap` | 60 ranks, uniform across all 9 stats | 40–80 | Formula 2, the hard non-dominance ceiling | MVP-locked, **structural (Pillar 4)** | Raising this loosens §4.7's proof — re-verify the combined-multiplier ceiling stays comfortably under the ~7× `power_tier` 1–20 gap on any change |
| `stat_yield_per_rank` (per stat) | 1.0 / 10 / 2.0 / 0.005 (§3.4) | see §3.4 | Formula 2 | MVP-locked defaults, retunable per stat independently | `critical_chance`'s yield is deliberately smallest — shares the same global 0.75 cap `combat-encounter-system` already enforces |
| `stat_starting_value[max_health]` | 100 | 50–200 | Formula 2 floor | MVP-locked, coordinate with Formula 3b | The only non-zero starting base among the 9 stats |
| Purchase gating (encounter state) | Hub/Forge only | **locked** | §3.3, Edge Case 3 | MVP-locked | Structural anti-exploit rule, not a numeric knob |
| Respec availability | none (permanent) | **locked** | §3.2 | MVP-locked | Considered and rejected — see §3.2/A2 |
| Automation of Training purchases | never | **locked** | A4 | MVP-locked, applies to all future Automation Stages | Preserves Pillar 2 — spending decisions stay manual |
| `hunter_level_ranks_per_level` | 10 | not balance-critical | Formula 4 (cosmetic) | MVP-locked, freely retunable | Never gates anything; safe to retune without re-verifying any other formula |

## 8. Acceptance Criteria

| # | Criterion | Verification Method |
|---|---|---|
| AC1 | A fresh save with zero ranks purchased returns exactly the §3.4 starting values for all 9 stats (`0, 0, 0, 0, 0, 0, 100, 0, 0.0`) — never null/undefined. | Unit test reading `hunter_base_[stat]` for all 9 keys on a freshly initialized save. |
| AC2 | A Training purchase is rejected, with zero Gleam deducted and zero rank-state change, whenever: (a) Gleam balance < Formula 1's cost, (b) the target stat is already at `stat_rank_cap`, (c) the stat key is not one of the 9 canonical keys, or (d) encounter state is `ACTIVE`/`RESOLVING`. | Unit test enumerating all 4 rejection categories independently; assert no state mutation in each. |
| AC3 | Formula 1's worked examples reproduce exactly at default tuning: rank 1 = 25, rank 10 = 39, rank 30 = 103, rank 60 = 445 Gleam. | Unit test asserting `gleam_cost(n)` for `n ∈ {0, 9, 29, 59}`. |
| AC4 | Formula 3's cumulative worked examples reproduce exactly at default tuning: one stat fully ranked = 8,839 Gleam; all 9 stats fully ranked = 79,551 Gleam. | Unit test asserting `total_cost(60)` and its ×9 sum. |
| AC5 | A `max_health` rank purchase between encounters increases `current_hp` by exactly the delta added to `max_health` — never snaps to full, never leaves `current_hp` unchanged. | Unit test purchasing a `max_health` rank at partial HP; assert `current_hp_after == current_hp_before + stat_yield_per_rank[max_health]`. |
| AC6 | After a simulated Memory Dust prestige reset, every `ranks_purchased[stat]` and derived `hunter_base_[stat]` value is byte-identical before and after. | Integration test invoking the prestige-reset path with non-zero Training state pre-set; assert no field changes. |
| AC7 | The Non-Dominance combined multiplier (maxing `attack_power` + `critical_chance` simultaneously, both to `stat_rank_cap`) never exceeds 2.0× baseline damage output at default tuning. | Computed regression test asserting `(1 + source_scaling_coefficient × stat_rank_cap) × (1 + stat_rank_cap × stat_yield_per_rank[critical_chance] × (crit_multiplier − 1)) < 2.0`. |
| AC8 | The Idle-Can-Progress worst-case floor (§4.6 Scenario B) reaches rank 1 of any stat within `base_training_cost / region_auto_sell_gleam_cap_per_hour` hours (≈1.04 hr default) and full 9-stat completion within a finite, computable number of hours (≈3,314.6 hr default) — never infinite, never blocked. | Computed test asserting `total_cost(540 total ranks) / region_auto_sell_gleam_cap_per_hour` is finite and matches the documented figure. |
| AC9 | No code path allows any stat's Training-purchased contribution to exceed its §3.4 "Max Contribution" ceiling, regardless of Gleam balance. | Unit test attempting a 61st rank purchase on each of the 9 stats independently; assert rejection in all 9 cases. |
| AC10 | Purchasing all 540 ranks across all 9 stats, in any order, deducts exactly 79,551 Gleam total. | Property-based test over randomized purchase orderings; assert final cumulative Gleam spent is order-independent and matches Formula 3's ×9 sum. |

---

## Cross-System Facts Proposed for Registration

- The 9-stat Training Rank table (§3.4): `stat_starting_value`, `stat_yield_per_rank`, and the
  uniform `stat_rank_cap = 60` per stat — first-time authority; no prior document defined
  `hunter_base_[stat]` values.
- `base_training_cost` (`25`), `training_growth_rate` (`1.05`), `stat_rank_cap` (`60`) — **BALANCE-CRITICAL**,
  the non-dominance ceiling (§4.7) and sink-absorption curve (§4.5) both depend on these exact values.
- Full-completion Gleam sink total: `79,551` — relevant to any future economy-designer audit of
  Gleam faucet/sink closure alongside `vow_binding_cost` (200) and Memory Dust's own 660-MD total
  (a separate currency, not directly comparable, but useful context).
- `hunter_level_ranks_per_level` (`10`) — cosmetic only, safe to retune freely.
