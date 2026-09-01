# Creature Jobs & Evolution System: IDLExIDLE

> **SUPERSEDED 2026-09-01 by nothing — the subsystem was retired 2026-08-24.** Describes the creature jobs/evolution model, none of which is in the runtime. Kept for vocabulary and history — do not treat its rules as current.

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | systems-designer |
| **Status** | Complete — authored in rush mode per `production/session-state/active.md` (review mode `lean`, all internal decision points resolved directly). Every resolution to an open question is logged in the Assumptions table below and justified inline. |
| **Priority / Tier** | MVP — Feature layer (`design/gdd/systems-index.md` #13) |
| **Depends On** | `design/gdd/creature-data-schema.md`, `design/gdd/item-data-schema.md`, `design/gdd/the-forge-system.md`, `design/gdd/loot-drop-system.md` |
| **Depended On By** | `region-mastery-automation-system`, `rare-creature-capture-system`, `creature-roster-ui`, `memory-dust-prestige-system` (none yet written) |

## Assumptions Log (resolved this session, no placeholders left unresolved below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | **The central tension is resolved in favor of evolution-gated Role.** A bound creature's `role` (and therefore its silhouette and work-loop verb, art-bible §3.1/§5.3) never changes except by the instance moving to a new `EvolutionNode`/`CreatureTemplate` (already how `creature-data-schema` §3.4/A5 defines evolution). "Job assignment" is redefined as placing an already-roled bound creature into a region and slot — matching the schema's only assignment field, `bound_state.assigned_region_id` (no separate "job" field exists, per A5). See §3.1 for the full argument. |
| A2 | **Recruitment (the hostile→bound transition) is primarily driven by Core Hatching in MVP**, not by every Kill outcome auto-binding. `combat-encounter-system`'s outcome table reserves the ceremonial "Mastery Transition" beat only for a boss kill and treats standard kills as an ordinary defeat beat; `rare-creature-capture-system` (live capture-without-killing) is Vertical-Slice-deferred. Consuming `loot-drop-system`'s unconditional, 100%-drop-rate `creature_core` as a recruitment seed is the only mechanism consistent with (a) this document's own listed dependency on `loot-drop-system` in `systems-index.md`, (b) Pillar 3's requirement that automated/idle kills produce genuine progression, and (c) not inventing new `combat-encounter-system` outcome logic in a file this document cannot edit. Live capture (VS) is designed as a second, additive path into the same transition rules (§3.1, §5 Edge Case 12). |
| A3 | **A creature equipment slot is a new extension**, not present in `creature-data-schema` or `item-data-schema` today. Defined in §3.2 as `equipped_charm_id`, stored in this document's own extension record (never written into a file this document doesn't own), reusing the existing `charm` `base_type`. Flagged at the end of this document as a registry-worthy cross-system addition requiring a small `item-data-schema` follow-up (an "equipped-to-creature" ownership context). |
| A4 | **Evolution eligibility is a multi-vector AND-gate, not a single weighted score.** Four independently-tracked signals (material, job, combat, equip) must each independently clear their own threshold before a branch triggers. This keeps every one of the five influence vectors load-bearing — a player cannot "grind past" a missing vector by over-investing in another — which is the same design spirit as Pillar 4 (trade-offs over stat-stacking) applied to creatures. A single weighted sum was rejected specifically because it lets one dominant vector (usually raw material feeding, being the easiest to automate) silently make the other four cosmetic. |
| A5 | **The four dynamic gate values are encoded as `unlock_condition_tags` strings** (`material:`, `job:`, `combat:`, `equip:`, `branch_priority:` — §3.3), reusing `creature-data-schema`'s existing free-form tag field exactly as that document's own worked example (`["job:attacker", "material:iron_core"]`) anticipated. No new field is added to `EvolutionNode`. |
| A6 | **Combat Behavior is modeled as tags stamped onto the specific `creature_core` Item Instance at mint time** (from the `CreatureDefeated` event's `broken_part_ids`), read only when that core is later fed to a bound creature (§3.4). This requires `loot-drop-system`'s core-mint step (§3.7 of that document) to expose the newly-minted core's `instance_id` alongside the event it already processes — flagged as an integration requirement on that (already-Designed) document, not implemented here. |
| A7 | **No permadeath.** game-concept.md never mentions creature death or loss for bound creatures; Retention Hooks explicitly names "evolved creatures they've shaped" as an Investment hook, and Pillar 3 ("idle is never worthless") is directly violated by a mechanic that can destroy player investment through neglect. Indefinite Starved/Blocked is a pure, reversible opportunity-cost penalty — see §5 Edge Case 9. |
| A8 | **Evolution progress on the job and combat vectors accrues only while `work_state = healthy`.** A Blocked or Starved creature is not completing real work cycles, so it earns neither production nor evolution progress on those two vectors — this is what makes work-state management a genuine, stakes-bearing decision rather than a cosmetic status light. |
| A9 | **Blocked and Starved both yield zero net production** (§4, Formula 5) but are deliberately kept causally distinct (downstream capacity vs. upstream input missing) because they require different player fixes — matching art-bible §5.3's distinct pose/motion language for the two states exactly. |
| A10 | **Per-role failure-mode profile is asymmetric by design** (§3.6): only Crafter can Starve (it is the only role with a consumed material input in this document's production model); Attacker, Producer, and Crafter can Block (they are the only roles with storable output); Support can Starve for lack of a valid orbit target; Defender is structurally always-on (never Blocks or Starves once assigned) — five distinct reliability profiles, not five copies of one state machine. |

---

## 1. Overview

The creature jobs & evolution system is the mechanical proof of Pillar 5 — it is the reason a
bound creature is a colleague with a function, not a trophy on a shelf. It owns three things:
**recruitment** (how a defeated creature's `creature_core` becomes a new bound `CreatureInstance`,
via Core Hatching), **evolution** (the transition rules that move a bound instance along its
`EvolutionTree` — element, equipped items, job diligence, combat behavior, and consumed materials,
combined into one deterministic, player-legible eligibility gate), and **work** (the
Healthy/Blocked/Starved state machine and the five distinct per-role production behaviors that
make team composition a genuine design puzzle). It builds directly on `creature-data-schema`'s
locked tree **shape** and its locked, evolution-only-mutable `role`/`source` fields, and it hands
off a working, roled, placed creature to `region-mastery-automation-system` to
actually run inside a mastered region's economy.

## 2. Player Fantasy

> **The creature you fought becomes the creature that works for you — and what it becomes is your
> doing.** Not a checkbox on a collection screen, and not a random roll on a slot machine, but a
> colleague you shaped through the fights you picked, the job you kept it at, and the materials you
> chose to feed it.

Concretely, this system delivers on that fantasy by making three things always true:

- **Nothing is a trophy.** Every bound creature, from its first tick of work, has a visible job
  doing something the automation economy actually needs (§3.6) — Pillar 5's design test applied
  literally, not as an aspiration.
- **Nothing is a roll.** A creature's evolution path is a deterministic function of four things the
  player directly controls — which region/job they assigned it to and for how long, which
  encounters produced the cores they fed it, which materials they chose, and which charm they
  equipped it with (§4, Formula 4). Two players who make the same choices get the same creature.
  Two players who diverge get visibly, traceably different results.
- **Nothing is silently punished.** A neglected creature's work stalls, visibly, in a
  specific, diagnosable way (§3.5) — never destroyed, never lost, always recoverable the moment the
  player pays attention to it again (A7). The stakes are opportunity cost, not loss — consistent
  with Pillar 3's promise that idle is never worthless and never punishing.

## 3. Detailed Rules

### 3.1 Job Assignment vs. Evolution — Resolving the Central Tension

game-concept.md states a creature can "become an attacker, defender, support unit, crafter, or
resource producer," influenced by five factors including "assigned job." Read as "a bound creature
can be freely reassigned to any of the five jobs at will," this directly contradicts two other
locked facts: art-bible §3.1 locks each Role to a distinct **silhouette** (mass distribution) and
§5.3 locks each Role to a distinct, continuously-looping **work-loop verb** (Attacker *strikes*,
Defender *braces*, Support *orbits*, Crafter *fidgets*, Producer *breathes* — with per-role frame
budgets, not a shared rig), and `creature-data-schema` §3.2 locks `role` as fixed-per-template,
mutable only by the *instance* moving to a *different template* via evolution (§3.4). A body built
for a Defender's held brace-stance cannot instantaneously perform a Crafter's rapid manipulator
fidget without the game either lying about what's on screen or re-rendering the creature from
scratch on every reassignment — neither is acceptable.

**Ruling: job (Role) is evolution-gated, never freely reassignable.** A bound creature's current
Role is exactly the Role of its current `EvolutionNode`'s `resulting_template_id`. The only way a
creature's Role changes is by satisfying an evolution transition (§3.3–3.4) that moves it to a
node with a different Role — a deliberate, earned, player-caused event with its own ceremony beat,
not a menu toggle.

**What "job assignment" means instead**: assigning a bound creature to **a region** and **a slot**
within that region's farming team — i.e., setting `bound_state.assigned_region_id` (the only
assignment field `creature-data-schema` actually defines) and, within that region,
which of the team's slots it occupies. This is a placement decision, never a Role decision. It
answers "*where* does this creature work," not "*what* does this creature do" — the latter is
already answered, permanently until the next evolution, by its Role.

This resolution still satisfies Pillar 5's design test in full: every bound creature has exactly
one real, functional job at all times (never zero, never a collection-only state), and that job
plugs directly into the automation economy (§3.6). Pillar 5 requires a creature to *have* a job —
it never requires that job to be *reassignable on demand*. Locking Role to evolution is what makes
the job feel earned and consequential rather than a costless dropdown, which better serves the
"colleague you shaped" fantasy (§2) than free reassignment would.

Because Role is fixed between evolutions, "assigned job" earns real mechanical teeth a different
way: **how diligently and how long** a creature is kept working at its *current* job is one of the
four dynamic inputs that determine which future branch it evolves toward (§3.3's Job Diligence
vector) — "what a creature does shapes what it becomes" survives fully intact, just expressed as
*evolution direction* rather than *instant reassignment*.

### 3.2 Recruitment — Core Hatching (the Mastery Transition, MVP path)

`creature-data-schema` calls the hostile→bound transition the **Mastery Transition** ("coronation,
not shutdown," art-bible §2) but explicitly defers *when and how* it fires to this document. In
MVP, exactly one path exists:

1. Every defeated creature — active or automated kill, mastered region or not — unconditionally
   mints one `creature_core` Item Instance (`loot-drop-system` §3.7; 100% drop rate, only rarity
   varies).
2. The player (or, once Automation Stage 3+ exists, the automation layer — flagged as a forward
   compatibility note for `region-mastery-automation-system`) may **Hatch** any `creature_core` they
   hold. Hatching consumes the core and mints a brand-new `CreatureInstance` with:
   - `template_id` = the core's own deterministic origin template (`loot-drop-system` A2:
     `definition_id = "core_" + template_id` — hatching simply reverses that derivation).
   - `evolution_state.current_node_id` = that template's tree's `root_node_id`.
   - `bind_state = bound`, `bound_state.assigned_region_id = null` (unassigned until the player
     places it, §3.1), `bound_state.work_state = null` (per `creature-data-schema` invariant 8).
   - A fresh `EvolutionProgressRecord` (§3.3) at zero on every vector.
3. **Region Mastery still gates *deployment*, not *hatching*.** A hatched creature can sit
   unassigned indefinitely; it can only be assigned to `bound_state.assigned_region_id` for a
   region that reports Mastered — enforced by `region-mastery-automation-system` (external
   precondition this document checks against but does not itself implement). This is exactly how
   Pillar 2 ("automation is earned") is honored without this document inventing region mechanics
   it doesn't own.
4. A **second, additive path** — live Capture-without-killing, preserving the hostile instance's
   own `instance_id` and history instead of minting a fresh one from a core — is specified by
   `rare-creature-capture-system` (Vertical Slice). It must produce a valid post-transition
   `CreatureInstance` using the same fields as step 2 above; this document's transition-rule
   contract does not change based on which path produced the instance (§5 Edge Case 12).

### 3.3 The Five Evolution Influence Vectors

Every bound `CreatureInstance` carries one `EvolutionProgressRecord` (this document's own extension
record, keyed by `instance_id`, never written into `creature-data-schema`'s file):

| Field | Type | Description |
|---|---|---|
| `evolution_progress` | number, ≥ 0 | Scalar meter filled by fed materials (Formula 1). |
| `healthy_work_ticks` | map<`Role`, int> | Job-diligence tally, keyed by the Role it was earned under (preserved across evolutions for flavor/roster stats; only the *current* Role's entry is checked live). |
| `combat_behavior_tally` | map<string, int> | Count of fed `creature_core`s carrying each combat-behavior tag (Formula 3). |
| `equipped_charm_id` | string, nullable | This document's new extension field (A3) — see below. |

**Vector 1 — Element (Source): a hard gate, not a per-branch differentiator.** Per
`creature-data-schema` A1/§3.4, an instance's `source` is fixed for its lifetime and every node in
its tree shares that source — so Source never distinguishes *between* sibling branches (they're
all the same source by construction). Its real mechanical weight instead comes from **Formula 1's
Source-Match Bonus**: fed materials whose own `source` field matches the creature's `source` fill
the evolution meter faster, rewarding the intuitive, thematic choice ("feed a Nature creature
Nature materials") with a genuine mechanical edge, not just flavor text.

**Vector 2 — Equipped Items (new extension, flagged).** No creature equipment concept exists
anywhere in the project today (`creature-data-schema` §6 explicitly named this a joint concern for
this document and `item-data-schema` once both existed). This document defines the minimal version
needed for MVP: **one charm slot per bound creature**, `equipped_charm_id`, holding any Item
Instance whose `base_type = charm` (item-data-schema's existing catalog — no new item content
required). Equipping is free, instant, and non-destructive — the charm is worn, not consumed, and
can be swapped at will. A branch's `equip:<trait>` tag (§3.4) checks the *currently equipped*
charm's `source`/`modifiers`/`enchantments` fields live, at the moment of the eligibility check
(Formula 4) — it is not accumulated over time like the other three vectors. **This is a genuinely
new cross-system surface and is flagged at the end of this document for a registry entry and a
small required follow-up to `item-data-schema`** (an item needs to be able to report "currently
equipped to creature X" so it can't simultaneously be sold/fed/merged while worn — mirroring how
`resonance-weaving-system`'s Vow-bound items already carry a similar exclusivity concept).

**Vector 3 — Assigned Job (job diligence).** Per §3.1, this is not a Role choice — it is *how much
the creature has actually worked*. Every production tick (Formula 5's cadence, per role) resolved
while `work_state = healthy` increments `healthy_work_ticks[current_role]` by 1 (Formula 2). Ticks
earned under a Role are namespaced to that Role — evolving from Producer to Attacker does not let
old Producer-ticks satisfy an Attacker-branch job gate. This is deliberate: "prove yourself at your
current job before the game lets you transcend it."

**Vector 4 — Combat Behavior.** Modeled as tags stamped onto a `creature_core` at the moment it is
minted (A6), from the originating `CreatureDefeated` event's `broken_part_ids` and `tier`:

| Tag | Condition at mint time |
|---|---|
| `combat:core_broken` | The defeated creature's `is_core` part was the one that ended the fight. |
| `combat:clean_kill` | `broken_part_ids` is empty or contains exactly 1 non-core part. |
| `combat:precise_kill` | `broken_part_ids` contains 2 or more non-core parts. |
| `combat:boss_kill` | The defeated creature's `tier = boss`. |

These tags travel with the specific core Item Instance (`CreatureCoreOriginRecord`, this
document's own extension table, keyed by the core's `instance_id`). They only matter when that core
is later **fed** (not hatched — a hatched core's tags are consumed/discarded, since hatching spends
the core on recruitment, not on shaping an existing creature) to an *already-bound* creature —
feeding a core is the one feed action that updates both `evolution_progress` (Formula 1, every fed
item does this) **and** `combat_behavior_tally` (Formula 3, cores only).

**Vector 5 — Consumed Materials.** The scalar meter, `evolution_progress`, filled by every Feed
action (`the-forge-system` §3.5.3's Creature Feed Event) via Formula 1. This is the vector every
other feed material (weapons, armor, charms, raw materials) contributes to — only `creature_core`
additionally contributes to Vector 4.

### 3.4 Evolution Trigger and Branching

An `EvolutionNode`'s `unlock_condition_tags` (`creature-data-schema` §3.4) are authored using this
document's tag grammar — a direct, literal use of that field exactly as its own worked example
anticipated:

| Tag pattern | Meaning |
|---|---|
| `material:<int>` | `evolution_progress` must be ≥ `<int>` (Formula 1). |
| `job:<role>:<int>` | `healthy_work_ticks[<role>]` must be ≥ `<int>` (Formula 2). `<role>` must equal the *parent* node's Role — validated at content-authoring time. |
| `combat:<tag>:<int>` | `combat_behavior_tally[<tag>]` must be ≥ `<int>` (Formula 3). |
| `equip:<trait>` | The currently equipped charm (Vector 2) must carry `<trait>` among its `source`/`modifiers`/`enchantments`. Omit this tag entirely for a branch with no equip requirement. |
| `branch_priority:<int>` | Deterministic tie-break order among sibling nodes (§5 Edge Case 1) — unique among siblings, lower resolves first. Not itself a gate. |

A node becomes eligible the instant **all** of its gate tags (`material`/`job`/`combat`, plus
`equip` if present) are simultaneously satisfied (Formula 4) — an AND-gate across vectors (A4), not
a weighted score. The transition applies at the next work-tick boundary (§3.5, §5 Edge Case 5), at
which point `template_id`/`evolution_state.current_node_id` update, `evolution_progress` is
decremented (not zeroed — §5 Edge Case 2) by that node's `material` threshold, and
`healthy_work_ticks`/`combat_behavior_tally` continue accumulating uninterrupted (they are
per-tag/per-role counters, not consumed by the transition).

### 3.5 The Work-State Machine

`work_state` is non-null only once `assigned_region_id` is set (`creature-data-schema` invariant
8). Two externally-supplied boolean signals drive every transition — both owned and computed by
`region-mastery-automation-system`, consumed here as inputs this document does
not itself compute:

- `output_capacity_available` — is there room downstream for this role's storable output, this
  tick?
- `input_resource_available` — is there a unit of this role's required input material available,
  this tick? (Trivially `true`, always, for roles with no input requirement — §3.6.)

| Priority | Condition | Resulting `work_state` | Player fix |
|---|---|---|---|
| 1 | Role requires input AND `input_resource_available = false` | `starved` | Route/produce the missing input (assign a generator role, or restock manually) |
| 2 | Role emits storable output AND `output_capacity_available = false` | `blocked` | Expand storage, or process the backlog (sell/craft/collect) |
| 3 | Neither of the above | `healthy` | — |

Starved is checked before Blocked: a creature with genuinely nothing to work with is a more urgent
signal than one whose output has merely backed up (§7 justifies this as the priority default, not a
hardcoded assumption). **Only while `healthy`** does the creature produce its role's output
(Formula 5) and accrue Vector 3 job-diligence ticks (Formula 2, A8) — Blocked and Starved both yield
zero net production and zero evolution progress on those two vectors, for different underlying
reasons (A9), matching art-bible §5.3's distinct Blocked ("stalls mid-cycle, output has nowhere to
go") vs. Starved ("agitated searching pose, no input resource") language exactly.

### 3.6 Per-Role Production and Team Composition

Each Role emits (or consumes) a distinct thing, chosen specifically so the five roles form one
interdependent production chain rather than five parallel, interchangeable number-generators:

| Role | Emits / Consumes | Storable output? | Consumes input? | Failure modes |
|---|---|---|---|---|
| **Attacker** *(strikes)* | Triggers automated kills against the region's hostile population — the sole source of new `CreatureDefeated` events (loot + cores) in an automated region. Without a Healthy Attacker, an automated region produces nothing at all. | Yes (loot/cores) | No | Blocked only |
| **Defender** *(braces)* | A team-wide stability/uptime modifier, applied instantly to every other assigned teammate's effective yield — never itself stored or routed. | No | No | Always-on: never Blocked or Starved once assigned (A10) |
| **Support** *(orbits)* | A yield multiplier applied to exactly one other designated teammate (matching its "tethered satellite" silhouette) — requires a valid target to orbit. | No | Requires a valid teammate target | Starved only (no valid orbit target) |
| **Crafter** *(fidgets)* | Consumes the region's raw loot/material output (from Attacker/Producer) and converts it into refined, higher-value output — feeds `region-mastery-automation-system`'s auto-craft stage. | Yes (refined goods) | Yes (raw materials) | Both Blocked and Starved — the only role exposed to both |
| **Producer** *(breathes)* | A steady, passive, ambient raw-resource trickle, independent of the rest of the team. The most idle-safe role. | Yes (raw resource) | No | Blocked only |

**What makes a good team, concretely**: at least one generator (Attacker and/or Producer) to create
raw input, a Crafter to convert that input into higher sell/feed value, a Defender to raise the
whole team's reliability, and a Support boosting whichever role is the current bottleneck. **What
makes a bad team**: any composition missing a generator entirely (e.g., an all-Crafter team is
permanently Starved — nothing to convert) or a Crafter with no generator upstream (its refined
output ceiling is capped by raw input it will never receive). This is the design puzzle named in
the brief: role choice is a supply-chain decision, not a stat-stacking one, and a bad composition
fails *visibly and diagnosably* (§3.5) rather than just underperforming silently.

Per-role work-tick cadence (how often a Healthy creature completes one production cycle, and
therefore how fast it accrues Vector 3 job-diligence) follows art-bible §5.3's locked *ordering*
(Crafter fastest → Attacker/Support mid → Defender slow → Producer slowest); absolute seconds are
this document's own tuning responsibility (§7), since art-bible explicitly commits only to the
ordering.

### 3.7 Creature Cores in Evolution

Creature cores serve exactly two purposes in this system, and never a third:

1. **Recruitment fuel** (§3.2) — Hatching a core mints a brand-new bound instance, always at its
   tree's `root_node_id`, deterministically tied to the specific template that dropped it.
2. **Feed material with a bonus channel** (§3.3, Vector 4/5) — feeding (not hatching) a core to an
   *already-bound* creature contributes to `evolution_progress` exactly like any other feed
   material (Formula 1, at `base_type_weight[creature_core] = 1.5`, `the-forge-system` §7), **plus**
   its stamped `combat_behavior_tags` to `combat_behavior_tally` (Formula 3) — the one thing no
   other feed material can do.

A core is never both hatched and fed — hatching consumes it into a new instance; feeding consumes
it into an existing instance's progress. The player chooses which, per core, at the moment they
act on it.

## 4. Formulas

### Formula 1 — Fed-Material Evolution-Progress Accrual

```
progress_gain = round(feed_contribution_value × source_match_multiplier)
evolution_progress_new = evolution_progress_old + progress_gain
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `feed_contribution_value` | int | ~2–195 (`the-forge-system` Formula 8, default tuning) | Reused directly, not re-derived. |
| `source_match_multiplier` | float | {`1.0`, `source_match_bonus`} | `source_match_bonus` (tuning, default `1.5`, §7) if the fed item's `source` field equals the creature's own `source`; `1.0` otherwise (including items with no fixed source). |
| `progress_gain` | int | ≥ 0 | Added to `evolution_progress` this Feed action. |
| `evolution_progress` | number | ≥ 0, unbounded above | Never decreases except by Formula 4's evolution-trigger decrement. No cap — overshoot carries over (§5 Edge Case 2). |

**Worked example**: feeding a Rare `creature_core` (`feed_contribution_value = 35`, per
`the-forge-system`'s own worked example) whose `source = nature` to a bound Nature-source creature:

```
progress_gain = round(35 × 1.5) = round(52.5) = 53
```

Feeding the same core to a Shadow-source creature instead: `progress_gain = round(35 × 1.0) = 35`.

### Formula 2 — Job Diligence Accrual

```
healthy_work_ticks[role] += 1   per resolved production tick where work_state = healthy and current Role = role
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `role` | enum `Role` | 5 values | The creature's *current* Role at the moment the tick resolves. |
| `healthy_work_ticks[role]` | int | ≥ 0, unbounded above | Namespaced per Role — ticks earned under a prior Role are preserved but never checked against a different Role's `job:` gate. |

**Output range**: unbounded above, monotonically non-decreasing, frozen (no decrement, no decay)
while `work_state ≠ healthy`. **Worked example**: a Crafter with `work_tick_interval_seconds = 8`
(§7) left Healthy for 40 minutes (2400s) accrues `floor(2400 / 8) = 300` ticks toward
`healthy_work_ticks[crafter]`.

### Formula 3 — Combat Behavior Tally

```
combat_behavior_tally[tag] += 1   for each tag in CreatureCoreOriginRecord.combat_behavior_tags, once per Feed action on a creature_core
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `tag` | string | One of `combat:core_broken` / `combat:clean_kill` / `combat:precise_kill` / `combat:boss_kill` | Stamped at core-mint time (§3.3 Vector 4 table); a single core carries exactly one of `clean_kill`/`precise_kill` (mutually exclusive by definition) plus optionally `core_broken` and/or `boss_kill` (independent, can co-occur). |
| `combat_behavior_tally[tag]` | int | ≥ 0, unbounded above | Per-creature, per-tag counter. Never decremented by an evolution transition. |

**Worked example**: feeding three cores in sequence tagged `[clean_kill]`, `[precise_kill,
boss_kill]`, `[precise_kill]` results in `combat_behavior_tally = {clean_kill: 1, precise_kill: 2,
boss_kill: 1}`.

### Formula 4 — Evolution Trigger / Direction Check (combines all five vectors)

```
node_eligible(N) = (evolution_progress ≥ material(N))
                  AND (healthy_work_ticks[current_role] ≥ job(N))
                  AND (combat_behavior_tally[combat_tag(N)] ≥ combat_count(N))
                  AND (equip_trait(N) = null OR equipped_charm has equip_trait(N))
```

(Source is not a term in this formula — it is enforced structurally, once, at the moment the tree
itself was assigned to the creature, per `creature-data-schema` A1. It re-enters the system only as
Formula 1's multiplier.)

| Symbol | Type | Range | Description |
|---|---|---|---|
| `N` | `EvolutionNode` | One of the current node's children | Candidate branch. |
| `material(N)` | int | > 0, tuning per node (§7) | Parsed from `N.unlock_condition_tags`'s `material:` entry. |
| `job(N)` | int | ≥ 0, tuning per node | Parsed from the `job:` entry; `0` if the node has no job gate. |
| `combat_tag(N)`, `combat_count(N)` | string, int | one of Formula 3's 4 tags; ≥ 0 | Parsed from the `combat:` entry; `combat_count(N) = 0` (always true) if the node has no combat gate. |
| `equip_trait(N)` | string, nullable | free-form | Parsed from the `equip:` entry, or `null` if absent. |
| `node_eligible(N)` | bool | — | `true` only when every present gate clears simultaneously. |

**Output range**: boolean per candidate node, evaluated once per work-tick (never continuously in
real time — guarantees the transition can only ever land on a tick boundary, §5 Edge Case 5).

**Worked example** (Verdant Whelp → Verdant Brawler, §7's MVP content):
`unlock_condition_tags = ["material:600", "job:producer:300", "combat:clean_kill:3",
"branch_priority:1"]` (no `equip:` gate on this branch). Given
`evolution_progress = 640`, `healthy_work_ticks[producer] = 310`,
`combat_behavior_tally[clean_kill] = 4`: `640 ≥ 600` ✓, `310 ≥ 300` ✓, `4 ≥ 3` ✓, no equip term ⇒
`node_eligible = true`.

### Formula 5 — Per-Role Production Rate

```
role_output_per_tick = round(base_role_output × (1 + power_tier_output_scalar × (power_tier − 1)) × role_state_multiplier)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `base_role_output` | float | tuning, per role, §7 (placeholder, unbalanced) | Authored output at `power_tier = 1`, Healthy. |
| `power_tier_output_scalar` | float | 0.05–0.50 (default `0.15`, mirroring `creature-data-schema`'s own scaling convention for internal consistency) | Global tuning constant. |
| `power_tier` | int | 1–20 | `CreatureInstance.power_tier`. |
| `role_state_multiplier` | float | `{healthy: 1.0, blocked: 0.0, starved: 0.0}` | Both non-Healthy states yield zero net output (A9) — see §5 Edge Case rationale for why this is not "reduced," not "zero for a different, hidden reason." |
| `role_output_per_tick` | number | ≥ 0, unbounded above | For Defender/Support this is consumed as a multiplier fed to *other* teammates' formula, not stored directly (§3.6) — the numeric mechanism is identical, only the consumer differs. |

**Output range**: unbounded above with `power_tier`, floor-clamped implicitly to 0 (never negative)
via the multiplier. **Worked example**: a Producer, `base_role_output = 10`, `power_tier = 2`,
Healthy: `round(10 × (1 + 0.15 × 1) × 1.0) = round(11.5) = 12` units this tick. The same Producer
Blocked: `round(10 × 1.15 × 0.0) = 0`.

### Formula 6 — Work-State Transition Rule

Already stated in full as a priority-ordered rule table in §3.5 (not a numeric formula — a
deterministic decision table over two externally-supplied booleans and the role's own
input/output profile from §3.6). Restated compactly:

```
work_state = starved  if requires_input(role) and not input_resource_available
           = blocked  else if emits_storable_output(role) and not output_capacity_available
           = healthy  otherwise
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `requires_input(role)` | bool, per role (§3.6 table) | fixed | `true` only for Crafter (input) and Support (valid orbit target) in MVP. |
| `emits_storable_output(role)` | bool, per role | fixed | `true` for Attacker, Crafter, Producer. |
| `input_resource_available`, `output_capacity_available` | bool, external | supplied by `region-mastery-automation-system` | Not computed by this document. |

**Worked example**: a Crafter with no raw material queued (`input_resource_available = false`) and
full refined-goods storage (`output_capacity_available = false`) resolves to `starved` (priority 1
wins over priority 2, §3.5).

## 5. Edge Cases

1. **A creature simultaneously meets the full gate-set for two sibling branches.** Resolved by
   ascending `branch_priority:<int>` (§3.3/3.4) — lower resolves first. `branch_priority` values
   must be unique among siblings; this is validated the same way `creature-data-schema` validates
   `PartDefinition.break_priority` uniqueness (§3.9 of that document), and content authoring for
   this document's own trees must satisfy the same rule.
2. **A fed item massively overshoots the current branch's `material` threshold.** **Ruling:
   carryover, never waste.** `evolution_progress` has no cap (Formula 1) and is decremented by
   exactly the triggering node's `material` threshold at the moment of transition (§3.4) — any
   surplus above that threshold immediately begins counting toward the *next* node's threshold.
   This matches Pillar 3's "idle is never worthless" spirit extended to the feed economy: a
   generous, high-value feed (e.g., an automated Legendary drop fed while the player is away) is
   never discarded for arriving "too early."
3. **Evolving while assigned to an automated region.** Evolution checks (Formula 4) are evaluated
   once per work-tick, the exact same cadence production resolves at (§3.6) — a transition can
   therefore only ever land *between* ticks, never mid-cycle (directly answers Edge Case 5 below
   too). The creature keeps its existing `assigned_region_id` across the transition (it does not
   get un-assigned by evolving); if the new Role differs from the old one, `work_state` is
   immediately re-evaluated post-transition using the *new* Role's input/output profile (§3.6),
   and `region-mastery-automation-system` is expected to re-check that region's team-composition
   validity on the next tick (flagged as an integration expectation on that not-yet-written
   system, not implemented here).
4. **Indefinite starvation — does a creature die?** **Ruling: no, explicitly rejected.**
   game-concept.md never mentions creature death; Retention Hooks names shaped/evolved creatures as
   an Investment hook the game should protect, not threaten; and a mechanic that can destroy player
   investment through neglect directly contradicts Pillar 3's "idle is never worthless" promise —
   the opposite of "never worthless" is "actively punishing," which permadeath-via-neglect would
   be. Indefinite Starved or Blocked has no timer, no decay, and no destructive consequence — it is
   a pure, fully-reversible opportunity-cost penalty (zero production, zero evolution progress,
   §3.5/A8) that resolves the instant the underlying supply-chain or capacity problem is fixed.
5. **An evolution that would change Role mid-work-loop.** Cannot occur by construction — see Edge
   Case 3. Evolution trigger checks only run at tick boundaries, and a Role's animation work-loop
   (art-bible §5.3) only ever plays to completion within one tick before the next tick (and any
   pending transition) is evaluated. There is no code path that swaps `template_id` while a
   work-loop animation is mid-playback.
6. **A branch's `combat:` tag references a tag that can never be earned by this creature's tree**
   (e.g., a `combat:boss_kill` gate on a creature line whose cores can only ever originate from
   standard-tier kills). Treated identically to `creature-data-schema` Edge Case 4's
   unmet-vs-nonexistent distinction: a tag that is merely *hard to satisfy* is a normal, expected
   unmet-condition state (the branch stays locked, no error) — a tag that is *structurally
   impossible* (content-authoring bug) is a content-validation failure to be caught before ship,
   not a runtime case this document resolves silently.
7. **A `creature_core` is fed to a Vow-bound-adjacent context, or a Vow-bound item is fed
   directly.** `the-forge-system` already blocks any item carrying a non-empty `vow_binding_log`
   from ever reaching the Feed action at the source (that document's Edge Case 1), and that
   document's own Dependencies section explicitly requires this document to *independently*
   re-check the same exclusion on its own consumption path (defense in depth). **Ruling
   (satisfying that requirement)**: this document's Feed-action handler rejects any incoming item
   with a non-empty `vow_binding_log`, regardless of what upstream validation already occurred —
   never trusts the caller.
8. **A creature at a leaf node (no further children) keeps being fed.** Legal, not an error.
   `evolution_progress`/`healthy_work_ticks`/`combat_behavior_tally` keep accumulating with no
   further gate ever consuming them. `creature-roster-ui` is expected to surface
   a "fully matured" status once a creature's current node has zero children, so the player isn't
   left wondering why nothing is happening — a UI concern, not a rule this document enforces by
   blocking the feed action itself.
9. **A creature is Starved or Blocked for a very long time (e.g., an entire offline session) and
   then rescued.** No catch-up production is granted for the missed time (A7/A9's "pure opportunity
   cost" framing means the lost ticks are simply gone, not banked) — `work_state` re-evaluates to
   `healthy` on the next tick after the underlying condition is fixed, and normal production/
   diligence accrual resumes from that point forward only.
10. **A hatched creature's tree has no `evolution_tree_id`** (a template authored as a pure leaf,
    per `creature-data-schema` Edge Case 12). Legal — the creature is bound at `root_node_id` with
    a permanently empty set of eligible children; it behaves identically to Edge Case 8's "fully
    matured" state from the moment it is hatched.
11. **Two different fed items in the same Feed action both claim a `source_match_multiplier`
    bonus** (batch-feeding). Each item's `progress_gain` (Formula 1) is computed independently and
    summed — the multiplier is per-item, never a batch-level bonus, so batching for efficiency
    provides no additional advantage or penalty over feeding items one at a time.
12. **A creature reaches `bound_state` via the Vertical-Slice live-capture path
    (`rare-creature-capture-system`) instead of Core Hatching.** No special-case handling required
    — §3.2 step 4 already specifies that path must produce a `CreatureInstance` with the same
    field shape as a hatched one (fresh or preserved `EvolutionProgressRecord`, per that system's
    own design choice), and every rule from §3.3 onward operates identically regardless of
    origin path.

## 6. Dependencies

### Depends On

- **`creature-data-schema.md`** (full) — `EvolutionTree`/`EvolutionNode` shape and
  `unlock_condition_tags` field (this document supplies the grammar that fills it, §3.4);
  `CreatureTemplate.role`/`.source`; `CreatureInstance.bind_state`/`.bound_state`/`.evolution_state`
  and their invariants (esp. invariant 8's `work_state` nullability, invariant 9's hostile-instance
  read-only evolution rule). This document never writes new fields into that schema — all
  extensions (`EvolutionProgressRecord`, `CreatureCoreOriginRecord`, `equipped_charm_id`) live in
  this document's own side tables.
- **`item-data-schema.md`** — `charm` `base_type` (reused for the new equipment slot, §3.3 Vector
  2); `vow_binding_log` (checked defensively, §5 Edge Case 7); `source`/`modifiers`/`enchantments`
  fields (read for `source_match_multiplier` and `equip:<trait>` gate evaluation).
- **`the-forge-system.md`** — Formula 8 (`feed_contribution_value`) reused directly, unmodified, as
  the input to this document's Formula 1; the Creature Feed Event (§3.5.3 of that document) is the
  trigger for both Formula 1 and Formula 3.
- **`loot-drop-system.md`** — the guaranteed, 100%-drop-rate `creature_core` (§3.7 of that
  document) is the sole recruitment seed for Core Hatching (§3.2); the `CreatureDefeated` event's
  `broken_part_ids`/`tier` fields are the source of Vector 4's combat-behavior tags (A6) —
  **this document requires that system's core-mint step to additionally expose the minted core's
  `instance_id`, an integration point not yet present in that (already-Designed) document,
  flagged here rather than implemented silently.**

### Depended On By

- **`region-mastery-automation-system`** — owns Region itself, team-slot
  capacity, and the two boolean signals (`input_resource_available`,
  `output_capacity_available`) this document's work-state machine (§3.5, Formula 6) consumes as
  external inputs. Also enforces the Region-Mastered precondition for assignment (§3.2 step 3) and
  is expected to re-check team-composition validity after any Role-changing evolution (§5 Edge Case
  3). That system's own GDD must reference this document for the creature-side contract.
- **`rare-creature-capture-system`** (Vertical Slice) — supplies the second
  hostile→bound transition path (§3.2 step 4, §5 Edge Case 12); must produce `CreatureInstance`s
  compatible with this document's `EvolutionProgressRecord` shape.
- **`creature-roster-ui`** — reads `evolution_state`, `EvolutionProgressRecord`
  (progress toward each visible/discovered branch's gates), `bound_state.work_state`, and
  `equipped_charm_id` to render roster/evolution screens; surfaces the "fully matured" status named
  in §5 Edge Case 8.
- **`memory-dust-prestige-system`** (Full Vision) — expected to read evolved
  creatures' final Role/tree position as part of whatever it carries across a prestige reset; no
  further contract is specified here since that system does not exist yet.

### Adjacent Systems (informational, not a dependency in either direction)

- **`combat-encounter-system`** — this document does not depend on it directly (recruitment runs
  entirely through `loot-drop-system`'s `creature_core`, per A2) and is not listed among its
  dependents. The `CreatureDefeated` event this document reads for combat-behavior tags (A6)
  originates there but is consumed indirectly, via the same event `loot-drop-system` already
  processes.

## 7. Tuning Knobs

| Knob | Field(s) | Safe Range | Gameplay Effect | MVP / VS |
|---|---|---|---|---|
| Source-match feed bonus | `source_match_bonus` (Formula 1) | 1.2–2.0 (default `1.5`) | Higher = thematically-matched feeding meaningfully outpaces mismatched feeding, reinforcing Source identity without making mismatched feeding useless. | MVP |
| Evolution material threshold | `material:<int>` per node (Formula 4) | Author relative to expected feed cadence; placeholder default `600` for the MVP Attacker/Defender/Crafter branches (unbalanced, pending playtest) | Primary pacing lever for "how long until this creature's first evolution" — the single most player-visible knob in the system. | MVP |
| Job diligence threshold | `job:<role>:<int>` per node | Placeholder default `300` ticks | Controls how much sustained, correctly-managed work (not just feeding) a branch demands — raising this makes neglect-then-catch-up strategies less viable, reinforcing the work-state stakes (A8). | MVP |
| Combat tally threshold | `combat:<tag>:<int>` per node | Placeholder default `3` | Controls how many *matching* cores (not just any cores) must be fed — too low trivializes the vector into "feed cores," too high makes a branch feel locked behind a specific playstyle the player may not naturally produce. | MVP |
| Work-tick interval per role | `work_tick_interval_seconds` | Must preserve art-bible §5.3's locked *ordering* (Crafter < Attacker ≈ Support < Defender < Producer); placeholder absolute values Crafter `8`, Attacker/Support `15`, Defender `25`, Producer `40` (unbalanced) | Governs both production cadence (Formula 5) and job-diligence accrual rate (Formula 2) simultaneously — the single highest-leverage knob for how "busy" each role feels and how fast each role's branches unlock. | MVP |
| Power-tier output scaling | `power_tier_output_scalar` (Formula 5) | 0.05–0.50 (default `0.15`, matching `creature-data-schema`'s own scalar for cross-system consistency) | Higher = higher-power-tier creatures produce disproportionately more, widening the gap between an early and a late roster member. | MVP |
| Base role output | `base_role_output` per role (Formula 5) | Placeholder authoring range only, genuinely unbalanced pending `region-mastery-automation-system`'s economy model and Vertical Slice playtesting | Direct output-volume lever per role — must be data-driven, never hardcoded, per `.claude/docs/coding-standards.md`. | MVP |
| `branch_priority` uniqueness | `branch_priority:<int>` per sibling set | Must be unique among siblings (not itself a numeric range) | Deterministic tie-break for simultaneous eligibility (§5 Edge Case 1) — a correctness rule, not a feel lever. | MVP |
| Equipment slot count | Creature equipment (§3.3 Vector 2) | Locked at 1 charm slot for MVP | More slots would deepen the Equipped Items vector but multiply the `equip:` gate design space; 1 slot keeps MVP content authoring tractable. | MVP (1 slot); VS candidate for expansion, not committed |
| Support-role team synergy depth | Team composition (§3.6) | MVP: Support boosts exactly 1 designated teammate | A wider synergy radius (e.g., Support boosting the whole team at a smaller magnitude) is a legitimate VS-era rebalance, not attempted here — flagged as an open direction, not a committed roadmap item. | VS-deferred consideration |

**MVP scope note (required by the task brief)**: MVP populates one creature line — **Verdant
Whelp** (Nature source, Producer root Role) — branching into three evolution destinations:
**Verdant Brawler** (Attacker, gated `material:600` / `job:producer:300` /
`combat:clean_kill:3` / `branch_priority:1`), **Verdant Warden** (Defender, gated
`material:600` / `job:producer:300` / `combat:core_broken:3` / `branch_priority:2`), and
**Verdant Tinker** (Crafter, gated `material:600` / `job:producer:300` / `combat:precise_kill:3` /
`branch_priority:3`) — 1 root + 3 branches, 4 total nodes, spanning 4 distinct Role values
(Producer, Attacker, Defender, Crafter), matching both `creature-data-schema`'s MVP node-count cap
(3–4 nodes) and its "at most 4 distinct role values" scope note. **Support is explicitly
VS-deferred** — the mechanic (Vector 2's equip gate, in particular) supports a Support branch
without any rework; only the content (a fourth branch template) is missing. All specific creature
names, tag values, and thresholds above are placeholder content pending `game-designer`/content
authoring and Vertical Slice playtesting — the *mechanic* is complete and final; the *numbers* are
not.

## 8. Acceptance Criteria

1. A bound `CreatureInstance`'s `role` never changes except as a direct result of an
   `evolution_state.current_node_id` transition — verified by asserting no code path writes
   `template_id` other than the evolution-trigger handler (§3.1, §3.4).
2. **No creature template in the shipped roster has a Role with zero production behavior defined
   in §3.6's table** — Pillar 5's design test, made mechanically testable: every one of the 5 Role
   values has a non-empty `Emits / Consumes` entry; a content-validation pass over all authored
   `CreatureTemplate`s confirms every `role` value used resolves to a row in that table.
3. **A creature's evolution path is deterministically traceable to player actions.** Given an
   identical sequence of Feed/assignment/equip actions replayed against two fresh hatches of the
   same template, both end at the same `current_node_id` with identical
   `EvolutionProgressRecord` values — verified by a replay test with no random seed involved
   anywhere in Formulas 1–4 (directly satisfies the brief's explicit "not random" requirement).
4. Hatching a `creature_core` with `definition_id = "core_nature_def_01_std"` mints a
   `CreatureInstance` with `template_id = "nature_def_01_std"`, `bind_state = bound`,
   `evolution_state.current_node_id` = that tree's `root_node_id`, and a zeroed
   `EvolutionProgressRecord` (§3.2).
5. Assigning a hatched, unassigned creature to a region that does not report Mastered is rejected
   (§3.2 step 3); assigning it to a Mastered region succeeds and sets `bound_state.work_state` to a
   non-null value per `creature-data-schema` invariant 8.
6. Given the worked example in Formula 1 (`feed_contribution_value = 35`, matching source,
   `source_match_bonus = 1.5`), `progress_gain = 53` exactly, matching §4's worked example.
7. Given the worked example in Formula 4 (`evolution_progress = 640`, `healthy_work_ticks[producer]
   = 310`, `combat_behavior_tally[clean_kill] = 4` against a node gated `material:600` /
   `job:producer:300` / `combat:clean_kill:3`), `node_eligible = true`; reducing any single one of
   the three inputs below its threshold flips the result to `false` — verified independently for
   each of the three vectors (proves the AND-gate is genuinely load-bearing on all three, not
   dominated by one, per A4).
8. A creature whose `work_state = blocked` or `work_state = starved` produces
   `role_output_per_tick = 0` for that tick (Formula 5) and does not increment
   `healthy_work_ticks` for that tick (Formula 2) — verified across both non-Healthy states.
9. A creature left `starved` or `blocked` for an arbitrarily long simulated duration (e.g.,
   10,000 ticks) is never removed from the roster, never has `bind_state` change, and resumes
   normal production/diligence accrual the instant the underlying signal (§3.5) flips true —
   verified with no destructive side effect of any kind (§5 Edge Case 4/9, A7).
10. Feeding an item with a non-empty `vow_binding_log` to any bound creature is rejected by this
    document's own Feed-action handler, independent of whether `the-forge-system`'s upstream check
    is bypassed in a test harness (§5 Edge Case 7 — proves the defense-in-depth requirement is
    actually implemented, not just asserted).
11. Feeding a `creature_core` with `combat_behavior_tags = [precise_kill, boss_kill]` to a bound
    creature increments `combat_behavior_tally[precise_kill]` and `combat_behavior_tally[boss_kill]`
    each by exactly 1, and also increments `evolution_progress` by that core's
    `progress_gain` (Formula 1) — verified both counters update from a single Feed action (Formula
    3, §3.7).
12. Simulating two sibling nodes both reaching `node_eligible = true` on the same tick resolves to
    the node with the lower `branch_priority` value, verified with a fixture where both nodes'
    gates are deliberately satisfied simultaneously (§5 Edge Case 1).
13. Feeding an item whose value would take `evolution_progress` from `550` to `680` against a node
    gated `material:600` results in the transition firing and `evolution_progress` landing at
    `680 − 600 = 80` post-transition (not `0`) — verified against §5 Edge Case 2's carryover
    ruling.
14. A `CreatureTemplate` reachable only via a node whose parent's Role differs from a `job:<role>`
    tag's `<role>` component is rejected at content-validation time (§3.4's authoring-time
    validation note).
