# Creature Data Schema: IDLExIDLE

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | systems-designer |
| **Status** | Complete — authored autonomously, no user available this session (rush mode, see `production/session-state/active.md`). Ambiguities resolved using `design/gdd/game-concept.md` and `design/art/art-bible.md` as authority; every resolution is flagged inline as an assumption. |
| **Priority / Tier** | MVP — Foundation layer (`design/gdd/systems-index.md` #1) |
| **Depends On** | None (Foundation tier) |
| **Depended On By** | combat-encounter-system, animation-rig-system, creature-ai-telegraph-system, creature-jobs-evolution-system, loot-drop-system (see §6) |

## Assumptions Log (resolved this session, no placeholders left in the schema below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | A creature's **Source is fixed for the lifetime of the instance**; only **Role** changes via evolution. | game-concept.md describes evolution changing what job/Role a creature fills, never its element. Source is treated everywhere else (art-bible 3.1, 4.2, 6.3) as a stable identity axis. |
| A2 | Boss-tier creatures have **5–8 targetable parts** (standard is locked at 3–5 by art-bible 5.6; "more on a boss" was not given a number). | Needed a concrete range for schema validation and the Formulas/Acceptance Criteria sections. |
| A3 | A `power_tier` field is introduced on `CreatureInstance` to drive HP/break-threshold scaling. | Neither game-concept.md nor art-bible.md name an explicit level/tier concept, but the task brief asks for a scaling formula and the Full Vision (many regions of escalating difficulty) requires one. |
| A4 | On the hostile→bound transition (Mastery Transition), `current_health` resets to full and all `PartState`s reset to `intact`. | art-bible §2 frames the transition as "coronation, not shutdown" and §5.3's bound work-loops show no persistent damage/crack decals — a working creature is not depicted as wounded. |
| A5 | A bound creature's job **is** its current Role (from its current evolution node) — there is no separate, independently-settable "job" field. | Prevents two sources of truth for the same fact; game-concept.md and art-bible both treat Role and job as the same concept once bound. |
| A6 | `crack_stage_count` (1 or 2, per art-bible 8.4) has no fleet-wide default mandated by any source doc; each part authors its own value. | Avoids inventing an unstated default while still satisfying the schema's need for a concrete field. |
| A7 | A `break_priority` field is added to `PartDefinition` to make simultaneous multi-part breaks deterministic. | Required to give Edge Case #1 (all parts break at once) an explicit, non-hand-wavy resolution. |
| A8 | Creature templates carry **no rarity/capture field**. Rare-creature-capture-system (Vertical Slice tier) is expected to extend this schema with its own field(s) when it is designed. | Avoids speculative scope creep into a system this GDD is explicitly not designing, while confirming (Edge Case #12) that nothing here blocks that extension. |
| A9 | `template_id` is locked to the pattern `[source]_[role_code]_[variant]_[tier]`, a 1:1 match with the art asset naming convention already locked in art-bible §8.1 (`crea_[source]_[role]_[variant]_[tier].png`). | Turns "art assets are keyed off this enum" from a note into an enforceable, unambiguous rule. |

---

## 1. Overview

The creature data schema is the single authoritative data model for every creature entity in
IDLExIDLE — both the authored, static definition of a creature species/variant (a
**CreatureTemplate**: its Source, Role, targetable parts, base stats, and evolution tree shape)
and the live, per-individual runtime record of a specific creature the player encounters or owns
(a **CreatureInstance**: current health, part-break progress, vulnerability state, bind state,
and evolution progress). Every other creature-facing system in the project —
combat resolution, animation rigging, AI telegraphs, evolution rules, and loot generation — reads
or writes this schema rather than inventing its own creature representation. This document
defines the shape of that data precisely enough that those five dependent systems can be
designed against a stable contract; it does not define combat math, telegraph timing, evolution
transition rules, or loot tables themselves — each of those is explicitly the job of a
different, not-yet-written GDD (see §6).

## 2. Player Fantasy

This is a foundation/data system, not a moment-to-moment mechanic, so its player fantasy is
what it *makes possible* rather than what it directly delivers:

> **Every creature you fight is a specific, persistent individual with a legible, mechanically
> real identity — not a random stat roll wearing a reskinned sprite.**

Concretely, this schema is what guarantees:

- A creature's Source and Role are never cosmetic flavor text — they are the same fields that
  determine its silhouette (art-bible 3.1), its part layout, and (once
  `creature-jobs-evolution-system` is designed) its evolution options. What the player *sees* on
  a creature's body is what the game's data *is*, not a decorative layer painted over generic
  numbers.
- A body part the player breaks is the same part that stays broken, changes the fight, and shifts
  the loot table — the schema makes "I broke its left claw" a persistent fact the rest of the
  game can react to, not a visual-only hit-reaction.
- A creature the player binds is not deleted and replaced with a "captured" flag — it is the
  *same instance*, carrying the same identity, now doing a job. Ownership feels earned because
  the data itself never breaks continuity between "the thing I fought" and "the thing I now run."
- The same schema scales from one MVP creature line up to the full 6-Source × 5-Role matrix
  without a rework, so a player's early mental model of "Source tells me what it looks like, Role
  tells me what it does" keeps paying off for the entire life of the game, not just the tutorial
  region.

## 3. Detailed Rules

### 3.0 Two-Tier Data Model

Every creature is represented by two related records:

1. **CreatureTemplate** — authored, static, one record per species/variant (e.g. "the standard
   Nature Defender" or "the Shadow Attacker boss Hollowmaw"). Defines Source, Role, tier, base
   stats, the list of targetable parts, and a reference to an evolution tree. Templates are
   content, not save data — they ship with the game and do not change per-player.
2. **CreatureInstance** — runtime, one record per specific creature that exists in a player's
   game (an encounter spawn, or a bound roster member). References a template and layers
   individual state on top of it: current health, part-break progress, vulnerability window,
   bind state, work state, and evolution progress.

This split exists because many instances share one template (every wild Nature Defender the
player fights is a separate instance of the same template), and because evolution moves an
instance from pointing at one template to pointing at another (§3.4) without losing the
instance's own identity (`instance_id`, nickname, capture history).

*Note on field naming*: field names below are logical identifiers for design purposes. When this
schema is implemented as C# data classes, field names should follow the project's eventual C#
naming convention (PascalCase properties) rather than the snake_case used here for readability.

### 3.1 Locked Enumerations

| Enum | Values | Source of truth |
|---|---|---|
| `Source` | `body` \| `mind` \| `nature` \| `machine` \| `shadow` \| `spirit` | art-bible §3.1/4.2 — locked, six values only, no additions without an art-bible revision |
| `Role` | `attacker` \| `defender` \| `support` \| `crafter` \| `producer` | art-bible §3.1 — locked, five values only |
| `Role` asset-key code | `attacker`→`atk`, `defender`→`def`, `support`→`sup`, `crafter`→`crf`, `producer`→`prd` | art-bible §8.1 naming convention — used only inside `template_id`/`variant_key`, not a separate field |
| `Tier` | `standard` \| `boss` | art-bible §5.6 (canvas tiers) |
| `BindState` | `hostile` \| `bound` | Derived from art-bible §5.4 (hostile vs. bound creature states) |
| `WorkState` | `healthy` \| `blocked` \| `starved` | art-bible §5.3 |
| `BreakThresholdType` | `cumulative_damage` \| `hit_count` | Task brief: "break-threshold (damage or hit-count)" |

A `Source` value is never inferred or defaulted — every `CreatureTemplate` must declare exactly
one. Same for `Role`. A creature is never sourceless or roleless; these are fixed identity axes,
not stats (per game-concept.md and art-bible §3.1).

### 3.2 CreatureTemplate

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `template_id` | string | Pattern `[source]_[role_code]_[variant]_[tier]`, e.g. `nature_def_01_std`, `shadow_atk_hollowmaw_boss` | Canonical unique ID. 1:1 with the art asset naming convention (art-bible §8.1) once prefixed `crea_` and suffixed `.png` by the art pipeline (A9). |
| `display_name` | string | Free text, 1–48 chars | Species/variant display name shown in the roster (art-bible §2, Creature Management). |
| `source` | enum `Source` | 6 values (§3.1) | Fixed elemental identity. Drives the creature's edge-quality silhouette and Source-glyph asset (art-bible §3.1/4.2) — this schema's `source` field is the single value both the renderer and the evolution/loot systems read. |
| `role` | enum `Role` | 5 values (§3.1) | Fixed-at-this-node functional identity. Drives mass-distribution silhouette and role badge (art-bible §3.1). Changes only by the instance moving to a different template via evolution (§3.4) — never mutated in place on a template. |
| `tier` | enum `Tier` | `standard` \| `boss` | Governs canvas size (art-bible §5.6: 96–160px standard / 192–320px boss) and, via `parts`, minimum/maximum part count (§3.9). |
| `variant_key` | string | 2-digit index `"01"`–`"99"` for `standard`; name-slug (e.g. `"hollowmaw"`) for `boss` | The `[variant]` component of `template_id` / asset filenames (art-bible §8.1). |
| `home_region_id` | string, nullable | Opaque reference to a Region entity | The region whose dominant Source this creature's `source` is expected to align with "in most cases" (art-bible §6.3). Region itself is not defined by this schema — owned by `region-mastery-automation-system`. Null is valid (a creature with no fixed home region, e.g. an early roaming variant). |
| `base_stats.base_health` | int | 1–999,999 (placeholder authoring range: 20–150 standard / 600–3,000 boss — unbalanced, pending `combat-encounter-system` and vertical-slice playtest per systems-index.md's flagged risk) | Base health at `power_tier = 1` (see Formula 1, §4). |
| `base_stats.base_attack_power` | int | 1–999,999 (placeholder: 3–40 standard / 40–200 boss) | Raw output value; how it becomes actual damage is `combat-encounter-system`'s resolution math, not defined here. |
| `base_stats.base_defense` | int | 0–999,999 (placeholder: 0–20 standard / 10–80 boss) | Raw mitigation value; resolution math owned by `combat-encounter-system`. |
| `parts` | list of `PartDefinition` (§3.3) | Length 3–5 if `tier = standard`; 5–8 if `tier = boss` (A2) | The creature's full targetable-part set. Exactly one entry must have `is_core = true` (§3.9). |
| `evolution_tree_id` | string, nullable | Reference to an `EvolutionTree` (§3.4) | Null if this template has no evolution branches defined (e.g. a template that only ever appears as an evolution *destination*, never a root). |

**Deliberately excluded from `CreatureTemplate`** (scope boundaries, not oversights — see §6):
crit chance, elemental resistances, action speed, equipment slots, rarity tier, capture
eligibility. Each belongs to a different not-yet-written system and would either duplicate or
pre-empt that system's design.

### 3.3 PartDefinition

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `part_id` | string | Unique within the owning template, snake_case, e.g. `left_claw`, `core` | Stable identifier referenced by `PartState` (§3.6), `creature-ai-telegraph-system`, and `loot-drop-system`. |
| `display_name` | string | Free text, e.g. "Left Claw", "Core" | Human-readable concept shown wherever parts are named in UI/tooltips. |
| `is_core` | bool | — | Marks the creature's main body / core part. Exactly one part per template must be `true` (§3.9, Edge Case #2). |
| `break_threshold_type` | enum `BreakThresholdType` | `cumulative_damage` \| `hit_count` | Whether this part's break progress accumulates raw damage or counts discrete successful hits. |
| `base_break_threshold` | number | > 0 (placeholder authoring range: 10–60 standard non-core parts, 40–150 standard core, scaled up for boss parts) | Threshold at `power_tier = 1` before scaling (Formula 2, §4). |
| `crack_stage_count` | int | 1–2 (art-bible §8.4: "1–2 overlay stages") | Number of intermediate "cracking" visual/mechanical stages between `intact` and `broken` (A6 — no fleet-wide default; authored per part). |
| `break_priority` | int | Unique within the owning template, ≥ 0 | Deterministic resolution order when multiple parts cross their break threshold in the same tick (A7; Edge Case #1). Lower resolves first. |
| `can_be_vulnerable` | bool | — | Default `true`. If `false`, this part can never appear in `vulnerable_parts` (§3.6) — reserved for future structural-only parts; every MVP part is expected to be `true`. |
| `loot_modifier_tag` | string, nullable | Free-form tag, e.g. `"claw_break_bonus"` | Consumed by `loot-drop-system` to shift the loot table when this part breaks, per game-concept.md's "breaking specific body parts... affects loot." This schema stores the tag only; the loot rule it triggers is that system's to define. |
| `fight_state_change_tag` | string, nullable | Free-form tag, e.g. `"exposes_core"`, `"disables_charge_attack"` | Consumed by `combat-encounter-system` / `creature-ai-telegraph-system` to change fight behavior when this part breaks, per game-concept.md's "breaking specific body parts changes the fight." Stored only, not interpreted here. |

### 3.4 EvolutionTree and EvolutionNode (shape only — not transition rules)

This system defines the *shape* an evolution tree takes. **`creature-jobs-evolution-system`
owns the rules for when and why an instance moves between nodes** — element
alignment, equipped items, job assignment, combat behavior, consumed materials (per
game-concept.md). That split is intentional and is restated in §6.

| Field (`EvolutionTree`) | Type | Range / Values | Description |
|---|---|---|---|
| `tree_id` | string | Unique | Identifier referenced by `CreatureTemplate.evolution_tree_id` and `CreatureInstance.evolution_state.tree_id`. |
| `root_node_id` | string | Must match a `node_id` in `nodes` with `parent_node_id = null` | The tree's starting node — every instance on this tree begins here. |
| `nodes` | list of `EvolutionNode` | MVP: 3–4 nodes total (1 root + 2–3 branches, per game-concept.md's MVP scope). No hard ceiling for Full Vision. | The full set of evolution destinations reachable from this tree. |

| Field (`EvolutionNode`) | Type | Range / Values | Description |
|---|---|---|---|
| `node_id` | string | Unique within the owning tree | Identifier referenced by `CreatureInstance.evolution_state.current_node_id`. |
| `resulting_template_id` | string | Must reference an existing `CreatureTemplate` | The template an instance takes on once it reaches this node. Per A1, `resulting_template_id`'s `source` must equal the tree's root template's `source`; its `role` may differ. |
| `parent_node_id` | string, nullable | Must reference another `node_id` in the same tree, or `null` | `null` only for the root node. Every non-root node has exactly one parent (a tree, not a general graph — no merging branches in MVP scope). |
| `branch_label` | string | Free text, e.g. "Vanguard Branch" | Display name for the branch this node represents, shown in roster/evolution UI. |
| `unlock_condition_tags` | list of string | Free-form tags, e.g. `["job:attacker", "material:iron_core"]` | Semantic references to the conditions `creature-jobs-evolution-system` will evaluate to permit this transition. This schema stores the tags only — evaluating them, resolving ties between simultaneously-eligible nodes, and deciding transition timing are explicitly that system's job (§6, Edge Case #4). |

### 3.5 CreatureInstance

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `instance_id` | string (GUID) | Globally unique, never reused | Identifies one specific creature for its entire lifetime, across the hostile→bound transition and across evolution. |
| `template_id` | string | Must reference an existing `CreatureTemplate` | The instance's current template. Changes when evolution moves the instance to a new node (§3.4) — `instance_id` does not change when this does. |
| `display_name` | string, nullable | Free text, 1–24 chars | Player-assigned nickname override. Null means "use the current template's `display_name`." Nicknaming is only meaningful once `bind_state = bound` but the field is legal to set at any time. |
| `power_tier` | int | 1–20 (MVP effectively fixed at 1–3, one region) | Scaling context this instance was spawned/exists at. Drives Formulas 1–3 (§4). Defaults to 1 if unset. |
| `bind_state` | enum `BindState` | `hostile` \| `bound` | Top-level state. Determines which of `hostile_state` / `bound_state` is populated (§3.9). |
| `hostile_state` | struct, nullable (§3.6) | Non-null **iff** `bind_state = hostile` | Combat-relevant live state: health, part-break progress, current vulnerability. |
| `bound_state` | struct, nullable (§3.7) | Non-null **iff** `bind_state = bound` | Automation-relevant live state: work state, region assignment. |
| `evolution_state` | struct (§3.8) | Always present | Current position in the creature's evolution tree. |

### 3.6 hostile_state and PartState

| Field (`hostile_state`) | Type | Range / Values | Description |
|---|---|---|---|
| `current_health` | int | 0 ≤ value ≤ `effective_max_health` | Live health. At 0, the instance transitions toward defeat/capture handling (owned by `combat-encounter-system`), not directly by this schema. |
| `effective_max_health` | int | ≥ 1 (Formula 1, §4) | Cached derived value, recomputed whenever `power_tier` or `template_id` changes. |
| `part_states` | list of `PartState` | Exactly one entry per `part_id` in the current template's `parts` | Live break-progress per part. |
| `vulnerable_parts` | list of string | Subset of `part_ids` from `part_states` where `can_be_vulnerable = true` and `current_stage < crack_stage_count + 1` (not broken) | Which part(s) are currently exploitable. Populated/cleared by `creature-ai-telegraph-system` — this schema only defines the field it writes into. Empty list = no current opening. |
| `vulnerability_window_ms` | int, nullable | ≥ 0; `null` iff `vulnerable_parts` is empty | Remaining milliseconds in the current vulnerability window. Timing/duration rules belong to `creature-ai-telegraph-system`; this is only the exposed data shape (per task brief). |

| Field (`PartState`) | Type | Range / Values | Description |
|---|---|---|---|
| `part_id` | string | Must reference a `part_id` on the current template | Which part this state belongs to. |
| `accumulated_value` | number | 0 ≤ value ≤ `effective_break_threshold` | Cumulative damage or hit count, matching the part's `break_threshold_type`. Source of truth for this part's break progress. |
| `current_stage` | int | 0 to `crack_stage_count + 1` | Derived/cached from `accumulated_value` via Formula 3 (§4). `0` = intact, `1..crack_stage_count` = cracking stage N, `crack_stage_count + 1` = broken. Recomputed whenever `accumulated_value` changes; never authored directly. |

### 3.7 bound_state

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `work_state` | enum `WorkState`, nullable | `healthy` \| `blocked` \| `starved`; `null` iff `assigned_region_id` is `null` | A creature with no region assignment has no work to be healthy/blocked/starved *at* — `null` is the explicit "not yet assigned" state, not a fourth enum value (Edge Case #7). |
| `assigned_region_id` | string, nullable | Opaque reference to a Region entity | The region this bound creature currently works in. May differ from the template's `home_region_id` (a bound creature can in principle be reassigned) — owned/enforced by `region-mastery-automation-system`. |

Per A5, there is no separate "job" field: the job a bound creature performs is its current
`Role`, read from the `CreatureTemplate` its `evolution_state.current_node_id` resolves to.

### 3.8 evolution_state (instance)

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `tree_id` | string | Must match the current template's `evolution_tree_id` | Which tree this instance is progressing through. |
| `current_node_id` | string | Must reference a `node_id` in that tree | The instance's current position. For a `hostile` instance, this is always the tree's `root_node_id` and is read-only — wild/hostile creatures do not carry evolution progress; only bound creatures do (evolution is tied to job assignment per game-concept.md, which only exists once bound). |
| `discovered_node_ids` | list of string | Subset of the tree's `node_id`s | Branches the player has revealed/unlocked visibility into, independent of whether they've been reached — supports the Discovery retention hook named in game-concept.md ("undiscovered evolution branches"). |

### 3.9 Schema Invariants (validation rules)

These must hold for any valid `CreatureTemplate` / `CreatureInstance` and are the basis for the
Acceptance Criteria in §8:

1. Exactly one `PartDefinition` per `CreatureTemplate` has `is_core = true`.
2. `parts.length` is 3–5 for `tier = standard`, 5–8 for `tier = boss`.
3. All `break_priority` values within one template's `parts` are unique.
4. `bind_state = hostile` ⟺ `hostile_state` is non-null AND `bound_state` is null.
5. `bind_state = bound` ⟺ `bound_state` is non-null AND `hostile_state` is null.
6. `vulnerable_parts` never contains a `part_id` whose `PartState.current_stage = crack_stage_count + 1` (broken).
7. `vulnerability_window_ms` is null if and only if `vulnerable_parts` is empty.
8. `bound_state.work_state` is null if and only if `bound_state.assigned_region_id` is null.
9. For a `hostile` instance, `evolution_state.current_node_id` always equals that tree's `root_node_id`.
10. `source` and `role` values are drawn only from the enumerations in §3.1 — no other strings are valid.

## 4. Formulas

All three formulas below share one input, `power_tier`, and one rounding rule: **all derived
numeric outputs round to the nearest integer using round-half-up**, unless the field they feed
(`PartState.accumulated_value` under `break_threshold_type = cumulative_damage`) is explicitly
allowed to remain fractional.

### Formula 1 — Effective Max Health by Power Tier

```
effective_max_health = max(1, round(base_health × (1 + tier_health_scalar × (power_tier − 1))))
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `base_health` | int | 1–999,999 | `CreatureTemplate.base_stats.base_health`, authored per template. |
| `tier_health_scalar` | float | 0.05–0.50 (default 0.15 — placeholder, unbalanced) | Global tuning constant (§7). Controls how steeply health grows per power tier. |
| `power_tier` | int | 1–20 | `CreatureInstance.power_tier`. |
| `effective_max_health` | int | ≥ 1, unbounded above | Output. Feeds `hostile_state.effective_max_health`. |

**Output range**: unbounded above (grows with `power_tier`), floor-clamped to a minimum of 1 so
no creature can ever spawn with 0 or negative health.

**Worked example**: a standard Nature Defender with `base_health = 80`, `tier_health_scalar =
0.15` (tuning default), spawned at `power_tier = 3`:

```
effective_max_health = max(1, round(80 × (1 + 0.15 × (3 − 1))))
                      = max(1, round(80 × 1.30))
                      = max(1, round(104))
                      = 104
```

### Formula 2 — Effective Break Threshold by Power Tier

```
effective_break_threshold = max(1, round(base_break_threshold × (1 + tier_break_scalar × (power_tier − 1))))
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `base_break_threshold` | number | > 0 | `PartDefinition.base_break_threshold`, authored per part. |
| `tier_break_scalar` | float | 0.05–0.50 (default 0.15 — placeholder, tuned independently of `tier_health_scalar` so part-break pacing can be adjusted without touching overall HP bloat) | Global tuning constant (§7). |
| `power_tier` | int | 1–20 | `CreatureInstance.power_tier`. |
| `effective_break_threshold` | int | ≥ 1, unbounded above | Output. The value `PartState.accumulated_value` must reach to fully break the part. |

**Output range**: unbounded above, floor-clamped to 1 so a part can never require zero or
negative effort to break.

**Worked example**: the "Left Claw" part of that same standard Nature Defender, with
`base_break_threshold = 25` (cumulative damage), `tier_break_scalar = 0.15`, `power_tier = 3`:

```
effective_break_threshold = max(1, round(25 × (1 + 0.15 × (3 − 1))))
                           = max(1, round(25 × 1.30))
                           = max(1, round(32.5))
                           = 33
```

### Formula 3 — Crack Escalation Stage Trigger

```
stage_trigger(n) = round(effective_break_threshold × (n ÷ (crack_stage_count + 1)))    for n = 1..crack_stage_count
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `effective_break_threshold` | int | ≥ 1 (Formula 2 output) | Full break threshold for this part at its current `power_tier`. |
| `crack_stage_count` | int | 1–2 | `PartDefinition.crack_stage_count` (art-bible §8.4). |
| `n` | int | 1–`crack_stage_count` | Which intermediate cracking stage. |
| `stage_trigger(n)` | int | 0 < value < `effective_break_threshold` | The `accumulated_value` at which `PartState.current_stage` advances to `n`. Evenly spaced between 0 and full break. |

**Output range**: strictly between 0 and `effective_break_threshold`, monotonically increasing in
`n`. `current_stage` reaches `crack_stage_count + 1` (broken) only once `accumulated_value ≥
effective_break_threshold` itself — not produced by this formula, but the natural next step past
the last `stage_trigger`.

**Worked example**: continuing the Left Claw part above (`effective_break_threshold = 33`,
`crack_stage_count = 2`):

```
stage_trigger(1) = round(33 × (1 ÷ 3)) = round(11.0) = 11   → part enters "cracking, stage 1" at 11 accumulated damage
stage_trigger(2) = round(33 × (2 ÷ 3)) = round(22.0) = 22   → part enters "cracking, stage 2" at 22 accumulated damage
(broken)                                          = 33   → part enters "broken" at 33 accumulated damage (full effective_break_threshold)
```

## 5. Edge Cases

1. **Multiple parts cross their break threshold in the same combat tick (e.g. an AoE hit).**
   Resolve breaks in ascending `break_priority` order (§3.3), applying each part's
   `fight_state_change_tag` and `loot_modifier_tag` in that order within the same tick. If the
   core part (see #2) is among them, its break is still processed in priority order, but once
   processed the encounter-end rule in #2 fires immediately — any parts with lower priority than
   the core simply never get processed that tick (the encounter has already ended).
2. **The core part (`is_core = true`) breaks.** This immediately ends the encounter — the
   creature is defeated — regardless of the condition of any other part. This is a hard rule,
   not a suggestion: `combat-encounter-system` must treat a core break as equivalent to
   `current_health` reaching 0, whichever occurs first.
3. **A creature is captured (or killed) mid-vulnerability-window.** The instant `bind_state`
   transitions `hostile` → `bound` (by kill, or by a future capture mechanic from
   `rare-creature-capture-system`), `hostile_state` is atomically cleared to `null` in the same
   write that populates `bound_state`. No dangling `vulnerability_window_ms` or `vulnerable_parts`
   can survive the transition — there is no partial state where both are populated.
4. **Evolution data references a branch that doesn't exist, or whose `unlock_condition_tags` are
   currently unmet, for the instance's current items/job/materials.** The system fails closed:
   `current_node_id` does not change, the attempted transition is rejected, and the rejection is
   logged as a data-integrity or unmet-condition event (the two are distinguishable —
   nonexistent node = integrity bug; unmet condition = normal, expected outcome). It is never
   silently accepted and never falls back to a random or default branch.
5. **A `power_tier` value is missing on a newly-spawned instance.** Defaults to `1`. There is no
   schema-level upper enforcement of `power_tier` against the template's intended difficulty band
   — `combat-encounter-system` is responsible for clamping it to sane values at spawn time.
6. **A part with `break_threshold_type = hit_count` receives damage-over-time or fractional
   damage that isn't a discrete "hit."** This schema only stores the resulting counter
   (`accumulated_value`); the definition of what constitutes one countable "hit" (e.g. does a DoT
   tick count?) is owned entirely by `combat-encounter-system`. This schema takes no position on
   it and must not be read as implying one.
7. **A bound creature has no `assigned_region_id` yet.** `work_state` is `null`, not one of
   `healthy`/`blocked`/`starved` — there is no fourth enum value for "unassigned"; absence of a
   region assignment is represented by the nullability of `work_state` itself (§3.9, invariant 8).
8. **`vulnerable_parts` is asked to include a `part_id` that is already `broken`.** Rejected —
   invariant 6 (§3.9) forbids it. `creature-ai-telegraph-system` must filter out broken parts
   before selecting vulnerability targets; this schema enforces the invariant but does not select
   targets itself.
9. **A `CreatureTemplate`'s `home_region_id` points to a region whose dominant Source differs
   from the template's own `source`.** This is permitted, not an error — art-bible §6.3 explicitly
   allows exceptions ("in most cases"), representing an intentionally out-of-place creature (a
   rare sighting, a narrative anomaly). No validation failure is raised.
10. **Two `EvolutionNode`s in the same tree have overlapping/identical `unlock_condition_tags`,
    making them simultaneously eligible.** Out of scope for this schema — tie-breaking between
    simultaneously-eligible nodes is `creature-jobs-evolution-system`'s rule to define. This
    schema guarantees only that `unlock_condition_tags` are stored per node; it does not resolve
    conflicts between them.
11. **A future system (e.g. `rare-creature-capture-system`) needs to mark an instance or template
    as rare/capturable, and this schema has no field for it.** This is intentional (A8) — the
    schema does not block that extension. The expected pattern is an additive field or a separate
    lookup table keyed by `template_id`/`instance_id`, added by that system's own GDD when it is
    written, without modifying any field defined here.
12. **A boss's evolution tree, or a template with no `evolution_tree_id` at all.** Valid.
    `evolution_tree_id` is nullable specifically for templates that are pure evolution
    destinations or that never branch (most boss templates, and any standard template authored
    as a dead-end). `evolution_state` is still present on the instance in this case, but
    `current_node_id`/`tree_id` reference nothing meaningful and are ignored by any system that
    reads them — a template with a null `evolution_tree_id` should never be assigned a non-null
    `evolution_state.tree_id` on its instances.

## 6. Dependencies

### Depends On

None. This is a Foundation-tier system per `design/gdd/systems-index.md` — it has no
prerequisites and is intentionally the first GDD authored.

### Depended On By

Per `systems-index.md`'s dependency map, thirteen systems depend directly on this
schema (five from the original enumeration, plus eight identified as the rest of the project was
authored). Each references this document (`design/gdd/creature-data-schema.md`) in its own
Dependencies section:

- **`combat-encounter-system`** — will read/write `hostile_state` every combat tick (health,
  `part_states`, `vulnerable_parts`) and read `base_stats` as raw inputs to its own resolution
  math. This schema explicitly does not define damage formulas, hit resolution, or what counts
  as a "hit" for `hit_count` parts (Edge Case #6) — that is combat-encounter-system's job.
- **`animation-rig-system`** — will read `CreatureTemplate.parts` (count, `part_id`, `is_core`)
  and `tier` (canvas size class) to know what to rig. Anchor conventions and per-part sprite
  assets are owned by the art pipeline (art-bible §8.6), not this schema — this schema supplies
  only the part list and identity fields (`source`, `role`) the rig needs to select the correct
  asset set.
- **`creature-ai-telegraph-system`** — will populate and clear `hostile_state.vulnerable_parts`
  and `hostile_state.vulnerability_window_ms`. This schema defines only the data shape those
  fields expose (per the task brief); telegraph timing, selection logic, and the
  slow-build/hard-cutoff curve (art-bible §2) are entirely that system's design.
- **`creature-jobs-evolution-system`** — owns evolution **transition rules**: when
  `unlock_condition_tags` are considered met, how Source/equipped-items/job/combat-behavior/
  materials factor into eligibility, and how ties between nodes resolve (Edge Case #10). This
  schema owns only the tree/node **shape** (`EvolutionTree`, `EvolutionNode`,
  `evolution_state`) that those rules operate on. This split is deliberate and should be
  restated explicitly in that system's own GDD.
- **`loot-drop-system`** — will mint loot (including "creature core" items, per the task brief)
  keyed off a defeated instance's `template_id`, `source`, `role`, and any broken parts'
  `loot_modifier_tag`. This schema does not define loot tables, drop rates, or item rarity —
  including creature-core item rarity, which belongs entirely to `item-data-schema` (not yet
  written), not to any field on `CreatureTemplate` (see Assumption A8 and the "deliberately
  excluded" note in §3.2).
- **`input-targeting-system`** — reads `PartDefinition.part_id`/`.can_be_vulnerable`/
  `.break_priority`/`.crack_stage_count`, `PartState.part_id`/`.current_stage`,
  `hostile_state.part_states`/`.vulnerable_parts`, and `CreatureInstance.bind_state`/
  `.hostile_state` for its hit-testing and cycle-order sort (that document's own §6).
- **`vow-condition-tracking`** — reads `CreatureTemplate.tier` (`standard`/`boss`) indirectly, via
  `combat-encounter-system`'s `hostile_state`, for `encounter_type_restriction` gating (that
  document's own §6).
- **`combat-hud`** (boundary dependency only) — reads `PartState.current_stage`/
  `accumulated_value` to confirm the creature's own crack-decal rendering is
  `animation-rig-system`'s job, never this HUD's (that document's own §6). A confirmed absence of a
  rendering dependency, not a data dependency, but the read itself is real.
- **`creature-roster-ui`** — reads `CreatureTemplate`/`CreatureInstance` field shape throughout,
  `EvolutionTree`/`EvolutionNode` and `discovered_node_ids`, and every schema invariant this screen
  must never violate, most directly invariant 8 (`work_state` nullability) (that document's own §6).
- **`automation-config-ui`** — reads `CreatureInstance`, `power_tier`, `bind_state`, and the `Role`
  enum underlying every team-assignment badge it renders (that document's own §6).
- **`encounter-spawn-system`** — reads `CreatureTemplate.template_id`/`.tier`/
  `.base_stats.base_health` and the `CreatureInstance.power_tier` field shape, and reuses Formula 1
  (`effective_max_health_by_power_tier`) by exact name in its own Formula 4 (that document's own §6,
  Assumption A1). This schema is `encounter-spawn-system`'s **only** dependency.
- **`hunter-progression-system`** — reads `power_tier` 1–20 scaling as the motivating curve this
  document exists to counterbalance; the "≈7× harder at tier 20" figure is reused directly in its
  §4.7 (that document's own §6).
- **`rare-creature-capture-system`** — reads `bind_state`, the atomic hostile→bound transition rule
  (Edge Case #3), `power_tier`, `CreatureTemplate.tier`, and `PartState`/`part_id`/`is_core` for the
  Capture Ward and `parts_broken_count`; adds its own extension record (`RareCreatureCaptureRecord`)
  without modifying any field defined here (that document's own §6, pre-authorized by this schema's
  own A8/Edge Case #11).

### Adjacent Systems (informational, not a dependency in either direction)

- **`item-data-schema`** — game-concept.md lists "equipped items" as an input
  to evolution. This schema deliberately does not define equipment slots on `CreatureTemplate`
  or `CreatureInstance`; reconciling "which items are equipped to this creature" is a joint
  concern for `creature-jobs-evolution-system` and `item-data-schema` once both exist, not this
  document.
- **`region-mastery-automation-system`** — will own the actual `Region` entity
  that `home_region_id` and `bound_state.assigned_region_id` reference. This schema treats both
  fields as opaque string references and does not define Region itself.
- **Art asset pipeline** (art-bible §8.1) — a one-way relationship: `template_id`'s
  `[source]_[role_code]_[variant]_[tier]` pattern is the authoring key the art pipeline's
  `crea_[source]_[role]_[variant]_[tier].png` naming convention is built from (A9). Changing this
  schema's ID pattern would require a corresponding art-bible naming update, not the reverse.

## 7. Tuning Knobs

| Knob | Field(s) | Safe Range | Gameplay Effect |
|---|---|---|---|
| Health tier scaling | `tier_health_scalar` (Formula 1) | 0.05–0.50 (default 0.15, unbalanced placeholder) | Higher = each power tier grows creature health faster, steepening the difficulty ramp across regions/encounters at the same power tier. |
| Break-threshold tier scaling | `tier_break_scalar` (Formula 2) | 0.05–0.50 (default 0.15, unbalanced placeholder) | Tuned independently of health scaling so part-break pacing (a Pillar 1 execution-skill lever) can be adjusted without also changing raw HP bloat (a difficulty lever). |
| Standard part count | `CreatureTemplate.parts.length`, `tier = standard` | 3–5 (locked by art-bible §5.6 canvas legibility — not safely retunable without an art-bible revision) | More parts = more distinct targeting decisions per encounter; fewer = simpler, faster reads. |
| Boss part count | `CreatureTemplate.parts.length`, `tier = boss` | 5–8 (A2 — not locked by any source doc; safe to retune within this band, revisit if boss canvas sizing changes) | Same lever at boss scale; more parts also supports more phase-transition fight-state changes. |
| Crack escalation stages | `PartDefinition.crack_stage_count` | 1–2 (locked by art-bible §8.4's authored overlay-stage budget) | 2 stages gives players an earlier "this part is about to break" read; 1 stage is a snappier, more binary break. Raising above 2 requires new art asset budget, not just a data change. |
| Base combat stats | `base_stats.base_health` / `base_attack_power` / `base_defense` | Placeholder authoring ranges only (§3.2) — genuinely unbalanced pending `combat-encounter-system` design and vertical-slice playtesting (flagged as a High-Risk System in systems-index.md) | Direct difficulty/pacing lever per creature. Must be data-driven per `.claude/docs/coding-standards.md`, never hardcoded. |
| Part break thresholds | `PartDefinition.base_break_threshold` | > 0, no upper bound; author relative to `base_health` (a part threshold far above total HP is a de facto "unbreakable in one encounter" part) | Controls how much sustained accurate targeting a given part demands before it breaks. |
| Evolution branch count per tree | `EvolutionTree.nodes.length` | MVP: 3–4 total nodes (1 root + 2–3 branches, locked by game-concept.md's MVP scope). No schema-level ceiling for Full Vision. | More branches = more build/collection depth (Discovery aesthetic); this is a content-authoring decision per tree, not a schema constraint. |

**MVP scope note (required by the task brief)**: MVP populates a narrow slice of this schema —
one `EvolutionTree` (one creature line) with one root `CreatureTemplate` and 2–3 branch
templates, all sharing one `source`, spanning at most 4 distinct `role` values, in a single
region (`power_tier` effectively 1–3), plus one boss template for that region. The schema itself
places no obstacle in the way of populating the full 6-Source × 5-Role matrix (30 base standard
templates) plus an arbitrary number of boss templates for Full Vision — every field above is
already general enough to support that without a rework; only content authoring volume changes.

## 8. Acceptance Criteria

1. Loading a `CreatureTemplate` with zero or with two-or-more `is_core = true` parts is rejected
   by validation (§3.9, invariant 1). A fixture with exactly one `is_core` part loads
   successfully.
2. Loading a `standard`-tier template with 2 parts is rejected; with 3, 4, or 5 parts it is
   accepted. Loading a `boss`-tier template with 4 parts is rejected; with 5 or 8 parts it is
   accepted (§3.9, invariant 2).
3. Two `PartDefinition`s on the same template sharing a `break_priority` value is rejected at
   load time (§3.9, invariant 3).
4. Given a `CreatureInstance` with `bind_state = hostile`, `bound_state` is asserted `null` and
   `hostile_state` is asserted non-null (§3.9, invariants 4–5).
5. Programmatically transitioning an instance's `bind_state` from `hostile` to `bound` results,
   in the same operation, in `hostile_state = null` and `bound_state` populated with a non-null
   struct — verified with no intermediate state where both are non-null (§3.9, invariants 4–5;
   Edge Case #3).
6. Attempting to add a `part_id` whose `PartState.current_stage = crack_stage_count + 1` to
   `vulnerable_parts` is rejected (§3.9, invariant 6; Edge Case #8).
7. Setting `vulnerable_parts` to a non-empty list without also setting a non-null
   `vulnerability_window_ms` (or vice versa) is rejected (§3.9, invariant 7).
8. Given `base_health = 80`, `tier_health_scalar = 0.15`, `power_tier = 3`, Formula 1 produces
   `effective_max_health = 104`, matching §4's worked example exactly.
9. Given `base_break_threshold = 25`, `tier_break_scalar = 0.15`, `power_tier = 3`, Formula 2
   produces `effective_break_threshold = 33`, matching §4's worked example exactly.
10. Given `effective_break_threshold = 33`, `crack_stage_count = 2`, Formula 3 produces
    `stage_trigger(1) = 11` and `stage_trigger(2) = 22`, matching §4's worked example exactly.
11. Setting `bound_state.assigned_region_id = null` while `bound_state.work_state` is non-null
    is rejected; setting `assigned_region_id` to a non-null value requires `work_state` to also
    become non-null (§3.9, invariant 8; Edge Case #7).
12. Setting `evolution_state.current_node_id` to a `node_id` not present in the tree referenced
    by `evolution_state.tree_id` is rejected at write time, never silently accepted (Edge Case
    #4).
13. Attempting to advance a `hostile` instance's `evolution_state.current_node_id` away from its
    tree's `root_node_id` is rejected — hostile instances are read-only with respect to
    evolution progress (§3.9, invariant 9).
14. Loading a `CreatureTemplate` with a `source` or `role` value outside the enumerations in
    §3.1 (e.g. `source = "fire"`) is rejected at load time (§3.9, invariant 10).
15. Simulating three parts crossing their break threshold in the same tick, with distinct
    `break_priority` values, results in `fight_state_change_tag`/`loot_modifier_tag` application
    in ascending `break_priority` order, verified by an ordered log of applied tags (Edge Case
    #1).
