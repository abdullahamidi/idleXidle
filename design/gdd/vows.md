# Vows

*Created: 2026-08-13*
*Status: Implemented*

> Depends on `game-flow.md` (no in-run decisions) and `skill-and-trait-trees.md`
> (the four branches a Vow must pull against). Read §3 of both first.

---

## 1. Overview

A Vow is a restriction the player accepts on one woven skill in exchange for a large
damage multiplier on it. Every Vow states a **demand the build must meet** — one
Form only, no crit training, no boots — and pays nothing at all if the build breaks
it. Two older Vows instead pay a **static cost** every wave and are always active.

A Vow is bound per skill slot, learned from the trait tree's spine, and can be
changed freely at the workbench like anything else in the Skill Tree layer.

---

## 2. Player Fantasy

Swearing something off. The fantasy is the ascetic's bargain: give up a thing you
could obviously have — your boots, your second Form, the crit you trained — and the
world pays you for the discipline. It should feel like a decision you can *say out
loud*: "I don't wear a helm." That sentence is only interesting because it is true
for the whole run, and true whether the run goes well or badly.

---

## 3. Detailed Rules

### 3.1 A Vow reads the build, never the fight

This is the rule the system exists to obey, and the reason it was rewritten.

The original Vows watched run state: *below 40% health*, *against a boss*, *after
ten seconds*. Combat is fully automatic (flow §3.1), so the player cannot cause any
of those conditions — whether a Vow paid was decided by the wave. That makes a Vow
a lottery ticket wearing the costume of a decision, and worse, it corrupts the
post-run report: a run that failed because a Vow never activated looks identical to
a run that failed because the build was too small.

A demand is checked once per wave against a description of the build. It is:

- **knowable before descending** — the player can see whether they satisfy it,
- **constant across the descent** — it cannot flicker mid-run,
- **reportable** — the report can state it as a fact rather than a probability.

### 3.2 The three families

| Family | Reads | Examples |
|---|---|---|
| **Build shape** | The woven skills | one Form, one Source, every weave filled |
| **Stat shape** | The character sheet | no crit training, cadence ≤ 1.00×, cadence ≥ 1.40×, no defence, no keystone |
| **Sacrifice** | The worn gear | a named slot left bare — boots, gloves, helm |

Sacrifice is the harshest family and priced accordingly: an empty slot costs its
stats **and** its enchantment **and** its rolled affixes at once, and all three are
visible to the player before they agree.

### 3.3 Every demand must pull against a branch

A Vow that costs nothing anyone wanted is not a Vow. Each demand is authored to
argue against a named branch of the Skill Tree, so taking one is a *second* vote in
the same argument the tree is already having:

| Vow | Argues against |
|---|---|
| THE SINGULAR (one Form) | SPREAD — its whole case is breadth |
| THE BLUNT EDGE (no crit) | TEMPO — the FOCUS road specifically |
| THE DELIBERATE (cadence ≤ 1.00×) | TEMPO — every rate node |
| THE FRANTIC (cadence ≥ 1.40×) | WEIGHT — HEAVY HAND and DELIBERATE cut rate |
| THE UNGUARDED (no defence) | ENDURE |
| THE UNBOUND (no keystone) | The whole Trait Tree's payoff |

### 3.4 Static-cost Vows

Two Vows have no demand: they are always active and always paying. FRAGILITY takes
12.5% more damage; RECKLESS OFFERING permanently gives up 15% of maximum health.
They exist as the floor of the system — something a player with no build identity
at all can still take — and their price is charged in effective HP so it is
invariant across every build.

**A Vow is charged once, however many skills carry it.** Both prices used to compound
per skill wearing the Vow, so a four-skill weave paid `1.125^4` (+60% damage taken)
against a card that said +12.5%, and `0.85^4` (−48% health) against a card that said
−15%. The benefit never compounded that way — a Vow multiplies each skill it is woven
into, so four skills at x1.45 is still x1.45 of damage — and the asymmetry made both
static-cost Vows strictly negative to swear: measured at −1 depth each, the only two
Vows in the game that cost more than they paid. Swearing is a promise about the build,
and a promise made on four skills is one promise. `SoloBattle.DistinctVows` is the one
place that decides this, so the two price sites cannot answer it differently.

### 3.5 Learning Vows

The Trait Tree spine teaches them, gentlest first:

| Node | Teaches |
|---|---|
| FIRST VOW | COMPLETION, THE DELIBERATE |
| SECOND VOW | THE PURE, THE FRANTIC |
| THIRD VOW | THE SINGULAR, THE BLUNT EDGE |
| BINDING VOWS | THE BAREFOOT, THE OPEN HAND, THE BARE SKULL |
| SACRIFICIAL VOWS | FRAGILITY, RECKLESS OFFERING, THE UNGUARDED, THE UNBOUND |

The gear-slot sacrifices are unreachable from the study chain alone — BINDING VOWS
is a separate purchase, so "I gave up a slot" is a choice made twice.

---

## 4. Formulas

### 4.1 Severity

    severity ∈ (0, 1) — how much of the build space the demand forbids

Severity replaced EXPECTED UPTIME, which is the same quantity asked honestly. Uptime
was meaningful while a Vow watched the fight ("how often does this hold?"); a build
demand is true for a whole descent or false for one, so the only remaining question
is what it costs to satisfy.

### 4.2 The multiplier

    multiplier = ConditionalMultiplier(1 − severity)

The pricing formula prices **rarity**: a lower argument means a condition that holds
less often and therefore pays more. Severity runs the other way by design, so it is
inverted at the call site rather than the catalogue being written backwards to suit
the formula.

### 4.3 Static cost

    multiplier = StaticMultiplier(effectiveHpFraction)

All static Vows lie on one line — 3.0 power per 1.0 of effective HP given up — so
none can dominate another. The cost is always expressed as a fraction of effective
HP, never of a raw stat, because a stat cost is avoidable by declining to invest in
that stat and a Vow that can be made free is not a Vow.

---

## 5. Edge Cases

- **A demand nothing can satisfy** is dead content — the old VOW OF THE OPENING
  waited on a weak-point window that manual combat took with it.
- **A demand everything satisfies** is a free multiplier — the old VOW OF THE
  UNBROKEN watched a squad slot the solo champion always occupies, and paid its full
  multiplier every fight for a restriction it never bore.
  Both are guarded by `test_every_demand_can_be_both_met_and_unmet`, which walks the
  catalogue against two extremes of the build space.
- **Cadence thresholds** compare against the *resolved* skill rate, which includes
  the Skill Tree's shape — so taking QUICK HANDS can silently break THE DELIBERATE.
  This is intended: it is the tree and the Vow having the argument out loud.
- **A build with no skills** satisfies SINGLE FORM and SINGLE SOURCE trivially, and
  has nothing to apply the multiplier to. Harmless.
- **Breaking a Vow mid-session** costs nothing beyond the lost multiplier. There is
  no punishment state; the Vow simply stops paying.

---

## 6. Dependencies

- **Quests** (`quests.md`) — THE FIRST VOW is completed by finishing a descent with a Vow's
  demand still MET, judged by the same `Weaving.IsActive` the simulation uses.

- `Weaving` — the Vow catalogue, severity pricing, and `IsActive`.
- `SoloBattle.DescribeBuild` — builds the `WeaveContext` from the Build and Hunter.
- `MemoryDustTree` spine — teaches them.
- `MasteryCatalog` — every demand is authored against one of its four branches.

---

## 7. Tuning Knobs

| Knob | Where | Effect |
|---|---|---|
| `Severity` per Vow | `Weaving.Catalog` | The multiplier it pays |
| `Threshold` | `Weaving.Catalog` | Cadence demands' cut-off |
| `Bare` | `Weaving.Catalog` | Which slot a sacrifice Vow forbids |
| `StaticCostMagnitude` | `Weaving.Catalog` | Effective-HP price of a static Vow |
| Which node teaches which | `DustEffects.VowGrants` | Learning order |

---

## 8. Acceptance Criteria

1. No field of `WeaveContext` names anything about the fight — health, elapsed time,
   boss, wave, damage. *(`test_a_demand_does_not_depend_on_how_the_fight_goes`)*
2. Every demand Vow is satisfied by at least one build and broken by at least one.
   *(`test_every_demand_can_be_both_met_and_unmet`)*
3. The sim's build description can actually satisfy each demand — the two halves are
   joined. *(`test_the_sim_describes_a_build_the_vows_can_read`)*
4. A Vow whose demand is broken grants exactly nothing.
   *(`test_a_broken_vow_grants_nothing`)*
5. A harsher Vow always pays more than a gentler one.
   *(`test_a_harsher_vow_is_worth_more_power`)*
6. No static Vow dominates another. *(`test_no_static_vow_dominates_another`)*
7. Every Vow in the catalogue is teachable by some trait-tree node.
   *(`test_the_whole_tree_teaches_every_vow_in_the_catalog`)*
