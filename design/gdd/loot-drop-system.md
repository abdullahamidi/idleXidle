# GDD: Loot Drop System

## Document Status

- **Status**: Complete — all 8 sections authored
- **Created**: 2026-07-14
- **Owned By**: economy-designer
- **Review Mode**: `lean` — authored autonomously this session (no user available to approve
  section-by-section; matching the precedent already set this session by `item-data-schema.md`
  and `the-forge-system.md`). Every ambiguity the source material left open has been resolved
  with an explicit, flagged design call rather than a placeholder. Validate at `/design-review`
  and `/gate-check` before Production.
- **Priority**: MVP, Feature tier — 12th in the recommended design order
  (`design/gdd/systems-index.md`), immediately after `vow-condition-tracking`. This is
  **the economy's faucet** — the single source of new items into the game. `the-forge-system`
  (already designed) is the sink this document's output must stay balanced against.

## Source Material Read

`design/gdd/item-data-schema.md` (full — the Item Instance/Definition shape every drop mints
against), `design/gdd/combat-encounter-system.md` (full — the `CreatureDefeated` event, Formula 4
`part_break_loot_bonus_percent`, and Formula 7 `active_efficiency_percent`, all consumed by exact
name below), `design/gdd/the-forge-system.md` (full — the economic sink this document's faucet
rate is modeled against; its Formula 1 `item_potency_score`/IPS and Formula 6 `sell_value` are
reused directly, never re-derived), `design/gdd/creature-data-schema.md` (full —
`PartDefinition.loot_modifier_tag`, `break_priority`, `power_tier`, `tier`, `source`), `design/gdd/game-concept.md`
(full), `design/art/art-bible.md` §4.3 (the locked 5-tier rarity ramp and ring-ornament grammar),
`design/gdd/systems-index.md` (this system's entry and dependents), `design/registry/entities.yaml`
(read for existing formulas/constants to reuse — `growth_rate`, `modifier_slot_count`,
`enchantment_slot_count`, `rarity_value_multiplier` all confirmed present and reused unmodified).

## Assumptions Log (resolved this session, no placeholders left in the design below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | **`creature-data-schema.md`'s `CreatureTemplate` has no `loot_table_id` field.** This document adds its own extension table, `LootTableBinding`, mapping `template_id → loot_table_id`, rather than modifying a schema it does not own. | Matches the established project pattern (`combat-encounter-system` A7: "adds its own small reference tables instead of modifying a file it doesn't own"). File discipline for this task forbids editing `creature-data-schema.md`. |
| A2 | **A `creature_core` Item Definition is derived deterministically as `"core_" + template_id`**, not looked up in a separate content table. | Avoids a second extension table for a fact that's already deterministic from data this document already reads (the `CreatureDefeated` event's `template_id`), matching `creature-data-schema`'s own `template_id`-pattern and art-bible's asset-naming-convention precedent (§8.1). |
| A3 | **`region-mastery-automation-system` fires the same `CreatureDefeated` event for an automated kill as for an active one**, but with `part_break_loot_bonus_percent` defaulting to a low/zero baseline (automation does not make deliberate weak-point targeting decisions) and `active_efficiency_percent` treated as not-applicable (functionally 100%, automation's own reference point). | `combat-encounter-system` does not itself distinguish active-vs-automated encounters within its own state machine — the only defined loot-trigger point in the project is the Kill outcome's `CreatureDefeated` event (§3.9 there). This document needs an assumption to proceed, matching `combat-encounter-system`'s own A9 pattern of treating an unauthored automation system's inputs as opaque and conservatively defaulted. `region-mastery-automation-system`'s own GDD must confirm or refine this when authored. |
| A4 | **`automation_stage` is an opaque external int, owned by `region-mastery-automation-system`**, consumed here only to select `automation_rarity_parity_percent(automation_stage)` (Formula 2). Only Stage 1 and Stage 2 values are authored (MVP's own scope — `auto-attack` + `auto-loot`, per game-concept.md's MVP Definition); later stages are flagged, not authored. | Mirrors `combat-encounter-system`'s A9 treatment of an externally-owned input this document does not compute. Matches MVP's own explicit scope: "Automation Stages 1–2 (auto-attack + auto-loot)" — game-concept.md's MVP Definition, item 5. |
| A5 | **This game has no hard inventory capacity limit at MVP.** No other authored document (`item-data-schema`, `save-load-persistence`) defines an inventory-slot ceiling — only `max_stack_size` (999, governing per-stack *quantity*, not inventory *breadth*) exists. | A hard cap would silently block automation's output once full, directly violating Pillar 3 ("automation must always produce genuine core progression") for any sufficiently long unattended session — the worst possible failure mode for an idle game's core promise. Flagged explicitly as a design call other systems (`loot-filter-ui`, a future `inventory-ui`) should reference rather than re-litigate. See Edge Case 9. |
| A6 | **Capture and Retreat encounter outcomes never reach this document.** Only the Kill outcome fires `CreatureDefeated` (`combat-encounter-system` §3.9); this document has no logic path for the other two outcomes. | Direct reading of `combat-encounter-system` §3.9's reward table: "Capture hook rewards: [handled by rare-creature-capture-system]"; "Retreat rewards: none." Loot-drop-system's entire surface area is the Kill outcome. |
| A7 | **A bad-luck-protection ("pity") mechanism is added**, guaranteeing at least Epic rarity once every `pity_threshold_kills` kills without a qualifying roll. | Not required by any source document, but within this agent's stated responsibility ("pity timers, and bad luck protection") and directly serves reward-psychology fairness at the very low baseline Legendary/Epic odds this document's own math produces (Formula 2). Bounded, capped at Epic (not Legendary), so it reshapes *timing* only, never inflates the value ceiling — does not interact with any of this document's economic-closure proofs. |

---

## 1. Overview

The loot drop system is IDLExIDLE's **economic faucet** — the sole point in the game where
new Item Instances are created and enter play. Every weapon, piece of armor, charm, raw material,
and creature core a player will ever own originates from this document's drop resolution, which
fires exactly once per defeated creature (the Kill outcome's `CreatureDefeated` event, owned by
`combat-encounter-system`) and mints Item Instances against `item-data-schema`'s schema and
Formulas 1–3. This document owns four things: the **loot table** content schema (what a creature
can drop, and how breaking specific parts or fighting a boss changes that pool); the **rarity and
quantity distribution formulas** (how many items drop and at what tier, scaled by the creature's
`power_tier`, the kill's `part_break_loot_bonus_percent`, and — for active kills only — the kill's
`active_efficiency_percent`); the **faucet-rate model and its balance against `the-forge-system`'s
already-proven sink**, including an explicit, worked accounting of the "automation runs 24/7"
failure mode named in this document's brief; and the **loot filter rule engine** — the
condition-to-action evaluation semantics that let a player (or their automation) keep pace with a
faucet that, by design, never turns off. Where `the-forge-system` proved its own closure ("every
operation converts or destroys value, nothing here creates it from nothing"), this document is the
other half of that proof: it shows the one place value *is* created is rate-bounded, known, and
never outpaces what the rest of the economy can absorb.

## 2. Player Fantasy

> The corpse of the thing you just broke apart flickers, and for one half-second before the drop
> resolves, you already know — from the ring starting to draw itself around the falling item —
> whether this was just another kill or the one you'll remember.

This document is where two very different, deliberately co-existing rewards live, matching this
game's own thesis that active and idle are permanently co-existing modes, not a phase and its
graduation:

- **The drop moment, for the player who is there to see it.** Art-bible §4.3 locks rarity to a
  bone→gold value ramp with a countable ring-frame ornament — Common's plain border, up through
  Legendary's full Ward-seal. That ring is drawn by this document's Formula 2, and the moment it
  resolves to a double-ring, four-flourish, or full-seal frame is this game's slot-machine
  moment — a classic **variable-ratio reinforcement schedule** (the single most durable reward
  pattern in behavioral psychology, precisely because the *next* roll always might be the one).
  This document deliberately keeps the baseline Legendary odds low (Formula 2's worked examples)
  specifically so that ring is never routine — but never zero, and never *purely* luck: a player
  who broke three parts before the kill (Formula 4's `part_break_loot_bonus_percent`) and fought
  cleanly (Formula 7's `active_efficiency_percent`) has visibly, provably tilted that ring's odds
  in their favor before the drop even resolves. The reward feels earned, not just rolled — this is
  the **Competence** need (Self-Determination Theory) expressed at the exact instant of payoff.
- **The quieter satisfaction of a filter doing its job while you're away.** Every rule authored in
  the Loot Filter Rule Engine (§3.9) is a small act of delegated trust — the player deciding in
  advance what "junk" and "keep" mean for their build, then walking away and finding, hours later,
  that the automation held to that judgment exactly, converting a wall of undifferentiated drops
  into a pre-sorted inventory that respects the one line they drew (Legendary and, by default,
  Epic items are never silently sold, no matter what rule matches — §3.9's safety net). This is
  **Autonomy**, not Submission: the player isn't passively watching numbers move, they authored the
  policy the numbers are now obeying. It's the same emotional shape as the Mastery Transition
  (art-bible §2) at a smaller, ambient scale — a system you built continuing to prove it was worth
  building, every time you open the inventory and find it already sorted the way you wanted.

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This document owns: the loot table content schema, drop-count and rarity-roll resolution, the
part-break loot-shifting mechanic, boss and rare drop handling, creature core minting, the Item
Instance minting rules for drops specifically (stat/enchantment roll *count*, per
`item-data-schema`'s own explicit deferral), the faucet rate model, and the loot filter rule
engine's evaluation semantics. It does **not** own, and defers to the cited document in every
case: what an Item Instance *is* (`item-data-schema`), when and why a creature dies
(`combat-encounter-system`), a creature's identity/parts/`power_tier` (`creature-data-schema`),
what happens to a dropped item after it exists — merge, sell, dismantle, feed, Vow-bind
(`the-forge-system`), the specific stat name/value catalog and Item/Enchantment Definition content
catalog (a future content-authoring pass, matching `item-data-schema`'s own scope boundary), the
loot filter *interface* (`loot-filter-ui`), automation stage definitions, `automation_stage`
itself, and offline-progression cadence (`region-mastery-automation-system`), and creature core
*consumption* rules for evolution (`creature-jobs-evolution-system`).

### 3.1 Trigger: The `CreatureDefeated` Event, and Only That Event

Per `combat-encounter-system` §3.9, exactly one encounter outcome produces rewards: **Kill**. On a
Kill, that document fires a `CreatureDefeated` event —
`{instance_id, template_id, broken_part_ids, power_tier}` — and attaches
`part_break_loot_bonus_percent` (its own Formula 4). This document's entire drop-resolution
pipeline (§3.3) begins here and only here. **Capture** produces no loot from this document (the
captured creature itself is the reward, handled entirely by `rare-creature-capture-system`); a
manual or HP-triggered **Retreat** produces nothing (`combat-encounter-system` §3.8/§3.9). No other
trigger exists — see Edge Case 4 for why a delayed/DoT-driven kill after the player has left cannot
occur under `combat-encounter-system`'s own rules, and Edge Case 6 for the automated-kill case.

### 3.2 Loot Table Schema (Content, Not Code)

A `LootTable` is authored content — one per distinct creature line (or, per §3.2.4, a future
region-level table) — referenced by exactly one `CreatureTemplate` via the extension table in
§3.2.5 (A1).

#### 3.2.1 `LootTable`

| Field | Type | Description |
|---|---|---|
| `loot_table_id` | string | Unique content key. |
| `base_entries` | list of `LootEntry` | Always-eligible pool — every regular drop slot (§3.3 step 3) draws from here, filtered by the slot's rolled rarity (§3.2.3). |
| `part_break_entries` | map<`loot_modifier_tag`, list of `LootEntry`> | Keyed by the exact string values `creature-data-schema`'s `PartDefinition.loot_modifier_tag` uses. A bucket here is only eligible to drop if the matching tagged part was broken in this kill (§3.4). |
| `boss_entries` | list of `LootEntry` | Only ever rolled if the defeated `CreatureTemplate.tier == boss` (§3.5). |
| `rare_entries` | list of `LootEntry` | An independent, very-low-probability bonus roll (§3.6), layered on top of the regular pool — the "jackpot" bucket. |

#### 3.2.2 `LootEntry`

| Field | Type | Description |
|---|---|---|
| `entry_id` | string | Unique within the owning bucket. |
| `item_definition_id` | string | Loose content-key reference to an `item-data-schema` §3.2 Item Definition — the *content* catalog of specific definitions (e.g. "Ember Blade") is out of scope here, matching that document's own restraint. May reference any `base_type` except `creature_core`, which has its own dedicated path (§3.7) and never competes for pool weight. |
| `weight` | float, > 0 | Relative weight within this entry's bucket, for the weighted-random pick at §3.3 step 3b. |
| `min_rarity_tier_index` | int, 0–4 | Default 0. Restricts this entry to only be eligible when the slot's independently-rolled rarity (§3.2.3, Formula 2) is at or above this tier. |
| `max_rarity_tier_index` | int, 0–4 | Default 4. Same, as an upper bound. Together, `min`/`max` let content authors reserve a specific Definition for a narrow rarity band (e.g. a boss-exclusive charm authored `min=max=4`, Legendary-only) without inventing a second schema. |

#### 3.2.3 Rarity Is Rolled Independently of Entry Selection

`item-data-schema` §3.3 (field 4) locks `rarity` as an **Item Instance** field, never fixed by the
Definition — the same Definition can be instantiated at any rarity. This document's drop
resolution therefore always rolls rarity **first** (Formula 2), then filters the relevant
`LootEntry` pool down to entries whose `[min_rarity_tier_index, max_rarity_tier_index]` range
includes the rolled tier, then weighted-picks among the survivors. This is a deliberate ordering
choice, not an implementation detail: it means "what drops" (entry pool composition) and "how good
is it" (rarity) are two orthogonal authoring levers, matching `item-data-schema`'s own separation
of Definition (content) from Instance (rolled state).

#### 3.2.4 Region-Level Tables (Forward Compatibility, Not Authored Here)

`LootTable` is deliberately generic enough to be referenced by a future `Region` entity (owned by
`region-mastery-automation-system`) — e.g. for ambient/passive region-level
resource generation independent of any specific creature kill. This document does not author any
region-level table content, since `Region` itself has no schema yet; `region-mastery-automation-system`
should reference this section directly rather than redefining a table shape when it is authored.

#### 3.2.5 `LootTableBinding` (Extension Table — A1)

| Field | Type | Description |
|---|---|---|
| `template_id` | string | Foreign key into `creature-data-schema`'s `CreatureTemplate.template_id`. |
| `loot_table_id` | string | Foreign key into `LootTable.loot_table_id` (§3.2.1). |

Exactly one binding per `template_id`. This document owns and stores this table — it is additive,
not a modification to `creature-data-schema.md` (A1).

### 3.3 Drop Resolution — The Full Algorithm

On receiving a `CreatureDefeated` event (§3.1), in this exact order:

1. **Resolve `loot_table_id`** via `LootTableBinding[template_id]` (§3.2.5). If no binding exists
   or it resolves to nothing, see Edge Case 1 — steps 3–6 below are skipped entirely for this kill,
   but step 7 (creature core, §3.7) is **unaffected**, since it does not depend on `LootTableBinding`.
2. **Determine `drop_count`** via **Formula 1**, using `part_break_loot_bonus_percent` (from the
   event) and, for an active kill only, `active_efficiency_percent`
   (`combat-encounter-system` Formula 7). For an automated kill, `active_efficiency_percent` is
   treated as not-applicable (A3) — Formula 1's second factor resolves to exactly `1.0`.
3. **For each of `drop_count` slots**:
   a. Roll `rarity_tier_index` via **Formula 2**.
   b. Filter `LootTable.base_entries` to entries whose `[min_rarity_tier_index, max_rarity_tier_index]`
      range includes the rolled tier. If the filtered pool is non-empty, weighted-pick one entry
      (§3.2.2's `weight`). If empty, re-roll the *filter* downward to the next-lower tier with a
      non-empty pool (never re-roll the rarity itself — the item mints at the tier the filtered
      entry supports; see Edge Case 1 for the fully-empty case).
   c. Mint one Item Instance from the picked entry's `item_definition_id`, at the (possibly
      downward-adjusted) rarity, per §3.8.
4. **Resolve Part-Break Bucket bonus items** via **Formula 3**: for each `part_id` in
   `broken_part_ids` (event field), in ascending `break_priority` order (matching
   `creature-data-schema`'s own determinism precedent for simultaneous breaks), look up that
   part's `loot_modifier_tag`; if non-null and a matching key exists in
   `LootTable.part_break_entries`, mint one guaranteed bonus item from that bucket (rarity rolled
   identically to step 3a), up to `max_part_break_bonus_items_per_kill` total across the whole
   kill (§7 — this cap is what makes Edge Case 6 provably non-exploitable).
5. **If `CreatureTemplate.tier == boss`**: mint `guaranteed_boss_drop_count` (default 1) additional
   items from `LootTable.boss_entries` (rarity via Formula 2).
6. **Roll the Rare/Bonus Drop** via **Formula 5**. If successful, mint one additional item from
   `LootTable.rare_entries`.
7. **Roll the Creature Core** via **Formula 4** — always mints exactly one `creature_core` Item
   Instance (§3.7); does not consume a `drop_count` slot and does not depend on `LootTableBinding`.
8. **Evaluate the Loot Filter Rule Engine** (§3.9) against every instance minted in steps 3–7,
   attaching a `keep` / `auto_sell` / `auto_dismantle` disposition tag to each. This document does
   not itself invoke `the-forge-system`'s Sell/Dismantle operations — it only produces the tag;
   *acting* on it is owned by the consuming layer (§6 Dependencies).
9. **Hand off** the full batch of minted instances (with disposition tags) as a single
   `LootResult` for this kill. The consuming presentation (a player-facing drop popup for an
   active kill, an automation collection queue for an idle kill) is owned by `combat-hud` /
   `region-mastery-automation-system` respectively — this document defines the data, not its
   display.

### 3.4 Part-Break Loot Shifting — "More Control Over Loot Types"

This is the mechanical answer to the task's explicit callout: **active players choose which part
to break, and that choice — not a generic multiplier — determines which extra loot bucket
unlocks.** Concretely:

- `PartDefinition.loot_modifier_tag` (`creature-data-schema` §3.3) is authored per part, per
  creature template — e.g. a Shard-Hound's `left_claw` part might carry `"claw_break_bonus"`.
- A `LootTable.part_break_entries["claw_break_bonus"]` bucket, if authored, contains items
  thematically or mechanically tied to *that specific part* (a claw-derived material, a
  claw-shaped charm, etc.) — content-authored, out of this document's own scope, but the
  **mechanism** that makes breaking that exact part deterministically unlock that exact bucket is
  fully specified here (§3.3 step 4).
- Because different parts carry different tags, and an active player is the one who decides
  (`input-targeting-system`'s `selected_part_id`, `combat-encounter-system` §3.3) which part their
  basic attacks and abilities land on, **the active player is directly choosing which loot bucket
  gets unlocked this kill** — a real, legible decision with a visible consequence, not a
  probability nudge. An automated kill, per A3, defaults `part_break_loot_bonus_percent` to a low
  baseline and — absent deliberate targeting logic, which is `region-mastery-automation-system`'s
  design to make, not this document's to presume — is not expected to reliably trigger any
  specific `part_break_entries` bucket. This is the genuine, structural difference between active
  and idle loot *type* control the task asked this document to design, not merely restate.
- The bonus items from this bucket are **guaranteed** (not just weighted into the general pool),
  capped by `max_part_break_bonus_items_per_kill` (§7) — see Edge Case 6 for the exploit-safety
  proof.

### 3.5 Boss Drops

A `boss`-tier `CreatureTemplate` (`creature-data-schema` §3.1's `Tier` enum) rolls
`LootTable.boss_entries` for `guaranteed_boss_drop_count` (default 1) additional guaranteed items,
on top of its already-larger `drop_count_base` (Formula 1, `drop_count_base_boss` default 3 vs.
`drop_count_base_standard` default 1). Boss encounters are also the primary natural source of
higher `power_tier` values (creature-data-schema's own placeholder authoring convention scales
boss `base_stats` well above standard), which independently shifts Formula 2's rarity distribution
upward — boss drops are structurally better on two independent axes (guaranteed bucket + higher
`power_tier` rarity tilt) without needing a third, redundant "is boss" bonus term in Formula 2
itself.

### 3.6 Rare/Bonus Drops

A small, independent chance (Formula 5) to mint one additional item from `LootTable.rare_entries`
on any kill, standard or boss. This is the "jackpot" bucket — content-authored to contain unique or
otherwise-hard-to-source items — and its trigger chance is itself tilted by the same
`part_break_loot_bonus_percent` / `active_efficiency_percent` factors Formula 1 already computes
(reused directly, not re-derived), so a strong active kill is meaningfully more likely to trigger
it than a routine automated one, without introducing a third independent scalar.

### 3.7 Creature Cores

Per game-concept.md and `item-data-schema`'s Dependencies section, `creature_core` is its own
`base_type`, used as `creature-jobs-evolution-system` feed material. This document's rule:

1. **Every Kill mints exactly one `creature_core` Item Instance — unconditionally, 100% of the
   time, for both active and automated kills.** This is a deliberate, non-tunable design call
   (§7): creature cores are named by game-concept.md as core progression material that "should not
   permanently lock... behind manual play" (Pillar 3). A guaranteed drop is the simplest,
   strongest, and most defensible way to satisfy that requirement — its **rarity**, not its
   presence, is the variable that active play and part-break performance influence.
2. `source` is set to the defeated `CreatureTemplate.source` — read directly from
   `creature-data-schema`. Per that schema's Invariant 9, a hostile instance's `template_id`
   always resolves to its evolution tree's root template, so this is always the creature line's
   base elemental identity, never an evolved-branch value.
3. `definition_id` is derived deterministically as `"core_" + template_id` (A2) — no
   content-authored lookup table is needed; every root `CreatureTemplate` that can be defeated as
   `hostile` implicitly has exactly one corresponding core Definition, matching
   `item-data-schema`'s existing `fixed_source: null` convention (source is set explicitly at
   instance-creation time here, exactly as that document anticipated for `creature_core`).
4. `rarity` is rolled via the **same Formula 2** used for every other drop this kill (no separate
   creature-core rarity formula — reuse over reinvention, matching the project's own established
   convention of citing a formula rather than duplicating it). `part_break_loot_bonus_percent` and
   `active_efficiency_percent` apply identically, so a well-executed active kill also yields a
   higher-quality core, on average, than a routine automated one — consistent tilting, not a
   special case.
5. `creature_core`'s `modifiers`/`enchantments` arrays are always empty (`item-data-schema`
   Invariant 1) — only `rarity` (and, transitively, `the-forge-system`'s IPS via
   `base_type_weight[creature_core] = 1.5`) expresses a core's quality.

### 3.8 Item Instance Minting (Stat/Enchantment Roll Count)

`item-data-schema` explicitly delegates "how many of the available slots get filled" to this
document (its Formula 1 note: "actual roll count is loot-drop-system's decision"). This document's
rule, **Formula 6**: each of `modifier_slot_count(rarity)` and `enchantment_slot_count(rarity)`
(`item-data-schema` Formulas 1–2, reused unmodified) available slots is independently filled with
probability `slot_fill_chance(rarity_tier_index)`. For each filled modifier slot, a `stat` is
weighted-picked from the target Definition's eligible stat pool (falling back to
`combat-encounter-system`'s Hunter Stat Catalog, §3.1 there, for a generic/procedural Definition
with no narrower pool authored) and a `base_value` is rolled from that stat's authored
`[min, max]` range (content-authoring pass, out of scope here, exactly matching
`item-data-schema`'s own stated boundary) — the **final** value is
`base_value × rarity_value_multiplier(rarity_tier_index)` (`item-data-schema` Formula 3, reused
directly). Enchantment slots are filled identically, weighted-picking one
`enchantment_definition_id` from the target Definition's eligible enchantment pool, with **no
duplicate `enchantment_definition_id` within the same instance**. `merged_from = null`,
`source_event = loot_drop`, `status = active`, `ability_focus` denormalized from the Definition,
exactly as `item-data-schema` §3.2/§3.3 already specify for any newly-created instance.

### 3.9 The Loot Filter Rule Engine

Per the task's explicit assignment, this document owns the **rule schema and evaluation
semantics**; `loot-filter-ui` owns the authoring interface.

#### 3.9.1 Schema

| Field (`LootFilterRule`) | Type | Description |
|---|---|---|
| `rule_id` | string | Unique per player's rule set. |
| `priority` | int | Ascending = evaluated first. Unique per rule set (ties are a `loot-filter-ui` authoring-time validation concern, not resolved here). |
| `conditions` | list of `FilterCondition` | **AND** semantics — every condition in the list must match for the rule to match. To express OR, author multiple separate rules at adjacent priorities. |
| `action` | enum | `keep`, `auto_sell`, `auto_dismantle`. |
| `enabled` | bool | Player can disable a rule without deleting it. |

| Field (`FilterCondition`) | Type | Description |
|---|---|---|
| `field` | string (open key) | Any Item Instance field or derived predicate — e.g. `base_type`, `rarity`, `source`, `ability_focus`, `has_enchantment_type`, `modifier_stat_present`, `quantity_gte`. |
| `operator` | enum | `equals`, `not_equals`, `in`, `greater_than_or_equal`, `less_than_or_equal`, `has_any`, `has_none`. |
| `value` | any | Matches `field`'s expected type. |

#### 3.9.2 Evaluation Semantics (Task's Explicit Question, Answered)

**First-match-wins, evaluated in ascending `priority` order.** For a given Item Instance, walk the
player's `LootFilterRule` list in `priority` order; the first `enabled = true` rule whose
`conditions` **all** match (AND) is applied. If no rule matches, the default action is **`keep`**.
This is the standard, well-understood evaluation pattern (firewall/spam-filter rule ordering) —
chosen specifically because it is simple to reason about and never produces a conflicting
double-action on the same item.

#### 3.9.3 Safety Net (Edge Case, Answered Explicitly)

Before rule evaluation runs, a **hard, non-overridable exemption** applies:
**any item with `rarity = legendary` can never receive `auto_sell` or `auto_dismantle` as its
final action, regardless of which rule matches.** If a matching rule's action would violate this,
the engine silently downgrades that item's action to `keep` for this evaluation only (the rule
itself is not altered or disabled — it simply does not apply to this one item). A second,
**default-on but player-togglable** exemption applies the same protection to `rarity = epic`
(`epic_auto_disposal_lock_default`, §7) — the toggle itself is owned by `loot-filter-ui`, but the
rule-engine-level check (evaluated before, not instead of, rule matching) is this document's to
define. This directly answers the task's Edge Case: a player cannot accidentally auto-sell a
Legendary through an overly broad rule (e.g. "auto-sell all weapons below X modifiers") — the
protection is structural, not dependent on the player having authored their rules carefully.

### 3.10 What Active Play Gets, Concretely (Consolidated)

Restating the task's own list, each item mapped to the exact mechanism that delivers it in this
document:

| Task's promise | Mechanism in this document |
|---|---|
| Faster clear times | Not this document's mechanism (owned by `combat-encounter-system` Formula 7's `CTR` term) — but faster clears compound with every per-kill mechanism below, since more kills/hour multiplies all of them (Formula 7 here). |
| Better part-break rewards | `part_break_loot_bonus_percent` shifts both **rarity** (Formula 2) and **quantity** (Formula 1) upward, capped at the inherited 40% ceiling (`combat-encounter-system` Formula 4). |
| Higher capture chances | Not this document's mechanism (owned by `rare-creature-capture-system`) — orthogonal to loot entirely. |
| **More control over loot types** | §3.4's part-break bucket unlocking — genuine agency over *which* bonus bucket a kill draws from, not a magnitude change. |
| Occasional rare encounters | Formula 5's rare/bonus drop roll, itself tilted upward by the same part-break/efficiency factors. |
| Reduced resource waste | A direct consequence of Formula 1's quantity uplift and Formula 2's rarity uplift together — fewer kills are needed to accumulate a given amount of usable material/gear, meaning fewer "wasted" low-value encounters to reach the same progression point. |

### 3.11 Bad Luck Protection (Pity)

A per-player, per-`loot_table_id` counter, `kills_since_epic_or_higher`, increments on every Kill
that does **not** produce at least one item (of any kind — regular slot, part-break bonus, boss,
rare, or creature core) at `rarity_tier_index ≥ 3` (Epic). The instant this counter would exceed
`pity_threshold_kills` (default 150, §7), the **next** Kill's Formula 2 roll for its **first**
regular drop slot is forced to a floor of Epic-or-better (i.e., `Common`/`Uncommon`/`Rare` are
excluded from that one roll's eligible pool for that slot only), and the counter resets to 0. This
never raises the *ceiling* of any roll (Legendary is still capped at its normal, tiny probability
even on a pity-triggered roll) and never affects `drop_count` or any other slot in the same
kill — it only guarantees a *floor* on one slot, on a bounded worst-case cadence. This mechanism
does not interact with, and cannot break, any of this document's economic-closure math (§4
Formula 8) — it reshapes *when* a rare item is guaranteed to appear, never *how many* drop or
*how large* their stat rolls are.

## 4. Formulas

All formulas share the project's established **round-half-up** convention for any output that
must resolve to an integer. Every formula below either directly reuses an already-registered
formula (`item-data-schema` Formulas 1–3, `the-forge-system` Formula 1/6, `combat-encounter-system`
Formulas 4/7) or is new content this document owns and is proposing for registration (see the
closing section of this document).

### Formula 1 — Drop Count

```
effective_bonus_drop_chance_percent = clamp(
    bonus_drop_chance_percent
    × (1 + part_break_quantity_scalar × (part_break_loot_bonus_percent / 100))
    × (1 + efficiency_quantity_scalar × max(0, (active_efficiency_percent − 100) / 100)),
  0, 100)

drop_count = drop_count_base(tier) + successes among bonus_roll_attempts
             independent Bernoulli(effective_bonus_drop_chance_percent) trials
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `drop_count_base(tier)` | int, tuning knob | 1 (standard) / 3 (boss) | Guaranteed floor before any bonus rolls. |
| `bonus_drop_chance_percent` | float, tuning knob | 15–50% (default 35%) | Base per-attempt chance of one bonus item. |
| `bonus_roll_attempts` | int, tuning knob | 1–5 (default 3) | Number of independent bonus-item attempts. |
| `part_break_loot_bonus_percent` | float, external input | `[0, 40]` | `combat-encounter-system` Formula 4, consumed by exact name. |
| `active_efficiency_percent` | float, external input | `[50, 300]`, or not-applicable (≡100) for an automated kill (A3) | `combat-encounter-system` Formula 7, consumed by exact name. |
| `part_break_quantity_scalar` | float, tuning knob, **BALANCE-CRITICAL** | 0.2–1.0 (default 0.6) | How strongly part-break performance raises drop count. |
| `efficiency_quantity_scalar` | float, tuning knob, **BALANCE-CRITICAL** | 0.3–1.2 (default 0.8) | How strongly overall active efficiency raises drop count. |
| `drop_count` | int | `[drop_count_base(tier), drop_count_base(tier) + bonus_roll_attempts]` | Output. Hard-bounded — see Edge Case 6. |

**Output range**: strictly bounded — `[1, 4]` for a standard kill, `[3, 6]` for a boss kill, at
default tuning. The `clamp(..., 0, 100)` on the effective chance guarantees this ceiling can never
be exceeded regardless of how large the two input scalars grow.

**Worked examples** (standard creature, `drop_count_base = 1`, `bonus_roll_attempts = 3`):

| Case | `part_break_loot_bonus_percent` | `active_efficiency_percent` | `effective_bonus_drop_chance_percent` | `E[drop_count]` |
|---|---|---|---|---|
| Automated kill (A3: no part-break targeting, efficiency n/a) | 0% | n/a (≡100) | 35.0% | 2.05 |
| Active, focused play (`combat-encounter-system`'s own "Focused active play" example) | 24% | 205% | 73.7% | 3.21 |
| Active, peak skill (`combat-encounter-system`'s own "peak" example) | 40% | 291.6% | 109.9% → clamped to 100% | **4.00 (hard ceiling reached)** |

The peak-skill case deliberately reaches the hard ceiling — this is the proof, not a bug, that the
formula's bound is real and reachable, directly answering "what if every part is broken — is it
exploitable" (Edge Case 6): no, because `drop_count` cannot exceed `drop_count_base +
bonus_roll_attempts` under any input, however extreme.

#### Total-throughput re-verification (Blocker B1 fallout — required, and it passes)

**Why this needed rechecking.** `active_efficiency_percent` used to *decay* toward the 50% floor as the
player's automation improved, which meant this document's active bonus — `max(0, (active_eff − 100) /
100)` — **clamped to zero at Partially Mastered**, roughly 26 minutes of team work into a region. The
premium for playing actively silently vanished. With B1 fixed, `active_efficiency_percent` is now
stably in the **120–300** band *permanently*, at every mastery level. The bonus is therefore **larger
and no longer decays** — so the obvious question is whether active play now *over*-rewards and breaches
the brief's "~3×" ceiling.

**It does not.** The reason is that `active_efficiency_percent` appears in *two* places — kills/hour
(via Formula 7's `CTR` term) and loot/kill (via this formula's quantity uplift) — which looks like it
should compound multiplicatively. It cannot, because `effective_bonus_drop_chance_percent` is
`clamp(..., 0, 100)`: the drop-count ceiling **binds before the uplift can compound**. Peak skill
already overshoots to 109.9% and is clamped back to 100%.

Total reward throughput = `kills/hour × E[drop_count]`. Both anchored to par:

| Play mode | Clear-time ratio vs. par | `E[drop_count]` | Throughput | vs. Fully-Mastered idle | vs. Optimized idle |
|---|---|---|---|---|---|
| Idle, fully mastered (100%) | 1.00 | 2.05 | 2.05 | 1.00× | — |
| Idle, optimized team (120%) | 1.20 | 2.05 | 2.46 | 1.20× | 1.00× |
| Active, focused (sustained) | 1.20 | 3.21 | 3.85 | **1.88×** | **1.57×** |
| Active, peak skill (brief) | 1.35 | 4.00 (clamped) | 5.40 | **2.63×** | **2.20×** |

**Every cell is inside the brief's "avoid sustained active play being more than roughly 3× better."**
The worst case in the entire table — brief peak skill against a fully-mastered farm — is **2.63×**, and
*sustained* focused play is **1.88×**. Automated kills keep `E[drop_count] = 2.05` (A3: efficiency
n/a ≡ 100%, so the uplift term is exactly zero), which is the floor the whole comparison rests on.

**The load-bearing invariant.** The `clamp(..., 0, 100)` in `effective_bonus_drop_chance_percent` is
what converts an unbounded multiplicative interaction into a bounded one. It is the reason
`efficiency_quantity_scalar = 0.8` (BALANCE-CRITICAL, §7) is safe despite appearing in two
independently-multiplying places. **If that clamp is ever removed or raised, this proof collapses** and
peak active throughput grows without a ceiling. Treat it as structural, not cosmetic — AC2 and AC7
already assert the bound; they must never be relaxed.

### Formula 2 — Rarity Distribution (Weighted Tilt)

```
tilt_multiplier[i] = (1 + i × power_tier_rarity_scalar × (power_tier − 1))
                    × (1 + i × part_break_rarity_scalar × (part_break_loot_bonus_percent / 100))

applied_tilt[i] = 1 + (tilt_multiplier[i] − 1) × source_parity_percent

effective_weight[i] = base_rarity_weight[i] × applied_tilt[i]

P(rarity_tier_index = i) = effective_weight[i] / Σ_j effective_weight[j]      for i = 0..4
```

Where `source_parity_percent = 100%` always for an active kill, and
`= automation_rarity_parity_percent(automation_stage)` for an automated kill (A4).

| Symbol | Type | Range | Description |
|---|---|---|---|
| `i` | int | 0–4 | Rarity tier index (Common=0 … Legendary=4). |
| `base_rarity_weight` | array of 5 floats, tuning knob, **BALANCE-CRITICAL** | — | Default `[700, 250, 45, 4.5, 0.5]` (sums to 1000 exactly, resolving to a clean 70% / 25% / 4.5% / 0.45% / 0.05% baseline). |
| `power_tier` | int, external input | 1–20 | `creature-data-schema`'s `CreatureInstance.power_tier`, read from the defeated instance. |
| `power_tier_rarity_scalar` | float, tuning knob, **BALANCE-CRITICAL** | 0.05–0.30 (default 0.15) | How much higher `power_tier` tilts weight toward rarer tiers. |
| `part_break_rarity_scalar` | float, tuning knob, **BALANCE-CRITICAL** | 0.2–1.0 (default 0.6) | How much part-break performance tilts weight toward rarer tiers. |
| `automation_rarity_parity_percent(automation_stage)` | float, external function, **BALANCE-CRITICAL** | `[0, 100]` | Stage 1: 0%. Stage 2: 50%. Later stages (VS/Full Vision) expected to approach 100% — not authored here (A4). |
| `P(rarity_tier_index = i)` | float | `[0, 1]`, sums to 1 across `i` | Output probability distribution. |

**Critical property (never zero, satisfying Pillar 3)**: at `i = 0` (Common), `tilt_multiplier[0] =
1` always (the `i` factor zeroes both bonus terms), so `applied_tilt[0] = 1` unconditionally —
Common's weight is never suppressed. For every `i > 0`, `applied_tilt[i] ≥ 1` always (never below
the un-tilted `base_rarity_weight[i]`), because `automation_rarity_parity_percent` only ever
**dampens the bonus above the floor**, never subtracts below it. **Every rarity tier retains at
least its baseline, non-zero probability under every combination of `power_tier`, part-break
performance, and automation stage** — this is the direct, provable satisfaction of game-concept.md's
"should not permanently lock important creatures, evolution paths, or core crafting materials
behind manual play."

**Worked examples** (low/mid/high tier, per the task's explicit request):

| Case | `power_tier` | part-break | parity | Common | Uncommon | Rare | Epic | Legendary |
|---|---|---|---|---|---|---|---|---|
| **Automation, Stage 1** (baseline reference) | 1 | 0% | 0% | 70.00% | 25.00% | 4.50% | 0.45% | 0.05% |
| **Automation, Stage 2**, mid-tier region | 3 | 0% (A3) | 50% | 66.45% | 27.30% | 5.56% | 0.62% | 0.076% |
| **Active, focused**, mid-tier region | 3 | 24% | 100% | 59.40% | 31.55% | 7.87% | 1.04% | 0.147% |
| **Active, peak skill**, deep region | 6 | 40% | 100% | 48.68% | 37.72% | 11.58% | 1.75% | 0.273% |

**Reading this table**: automation's per-kill rarity odds barely move between Stage 1 (power tier
1) and Stage 2 (power tier 3, three regions deeper) — `automation_rarity_parity_percent` is doing
real, deliberate throttling work here, and it is the direct mechanism preventing "automation at a
deep region spamming Legendaries 24/7 from turn one." Active play, by contrast, climbs
meaningfully with both region depth and execution — Legendary odds roughly **5.5×** higher at
peak active play than at Stage-1 automation, while never being literally zero at either extreme.

### Formula 3 — Part-Break Bucket Guaranteed Items

```
bonus_items_from_part_breaks = min(
    max_part_break_bonus_items_per_kill,
    count(part_id in broken_part_ids where PartDefinition.loot_modifier_tag is non-null
          and that tag exists as a key in LootTable.part_break_entries)
)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `broken_part_ids` | list of string, external input | 0 to the template's non-core part count | From the `CreatureDefeated` event. |
| `max_part_break_bonus_items_per_kill` | int, tuning knob | 1–5 (default 3) | Hard ceiling — see Edge Case 6. |
| `bonus_items_from_part_breaks` | int | `[0, max_part_break_bonus_items_per_kill]` | Number of guaranteed bonus items minted this kill via §3.3 step 4. |

**Worked example**: a boss with 8 parts, 6 broken at kill time, 4 of which carry a
`loot_modifier_tag` matching an authored `part_break_entries` bucket. Even though 4 buckets are
eligible, `min(3, 4) = 3` — only the first 3 (in ascending `break_priority` order) mint a
guaranteed item; the 4th eligible bucket is not drawn from this kill. Bounded regardless of part
count.

### Formula 4 — Creature Core Drop & Rarity

Presence is unconditional (§3.7, rule 1) — not a probability formula. Rarity reuses **Formula 2**
directly, with the defeated creature's own `power_tier`, `part_break_loot_bonus_percent`, and
`automation_rarity_parity_percent` inputs (identical mechanism, no new formula needed). Its
resulting **value** (for faucet-rate modeling, Formula 7) uses `the-forge-system` Formula 1 (IPS)
with `base_type_weight[creature_core] = 1.5` and both fill-ratio terms forced to 0
(`item-data-schema` Invariant 1 — cores never carry modifiers/enchantments).

**Worked example**: at Automation Stage 1 baseline rarity odds (Formula 2's first row), expected
core `sell_value`-equivalent (via `the-forge-system` Formula 6, `sell_conversion_rate = 15`) is
**≈24.90 Gleam-equivalent per kill** (full computation in Formula 7).

### Formula 5 — Rare/Bonus Drop Roll

```
effective_rare_chance_percent = rare_drop_roll_chance_percent
    × (1 + part_break_quantity_scalar × (part_break_loot_bonus_percent / 100))
    × (1 + efficiency_quantity_scalar × max(0, (active_efficiency_percent − 100) / 100))
```

Reuses Formula 1's own `part_break_quantity_scalar`/`efficiency_quantity_scalar` directly (no new
scalars) — the same underlying "how well did this kill go" signal drives both regular quantity and
jackpot-roll odds.

| Symbol | Type | Range | Description |
|---|---|---|---|
| `rare_drop_roll_chance_percent` | float, tuning knob, **BALANCE-CRITICAL** | 0.5–5% (default 2%) | Base chance of one bonus roll from `rare_entries`. |
| `effective_rare_chance_percent` | float | `[0, 100]` (clamped) | Actual chance applied this kill. |

**Worked examples**: Automation Stage 1/2 (no part-break, efficiency n/a): 2.0%. Active, focused:
`2% × 1.144 × 1.84 ≈ 4.2%`. Active, peak: `2% × 1.24 × 2.533 ≈ 6.3%`.

### Formula 6 — Slot Fill Ratio (Stat/Enchantment Roll Count)

```
slot_fill_chance(rarity_tier_index) = clamp(base_fill_ratio + rarity_tier_index × fill_ratio_rarity_bonus, 0, 1)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `base_fill_ratio` | float, tuning knob | 0.3–0.7 (default 0.5) | Fill chance at Common. |
| `fill_ratio_rarity_bonus` | float, tuning knob | 0.05–0.15 (default 0.1) | Additional fill chance per rarity tier. |
| `slot_fill_chance` | float | `[0.5, 0.9]` at default tuning across the 5 tiers | Per-slot independent Bernoulli probability (§3.8). |

**Worked example**: a Rare drop (`rarity_tier_index = 2`): `slot_fill_chance = 0.5 + 2×0.1 = 0.7`.
`modifier_slot_count(rare) = 3`, `enchantment_slot_count(rare) = 2` (`item-data-schema` Formulas
1–2) → expected filled modifiers `3 × 0.7 = 2.1`, expected filled enchantments `2 × 0.7 = 1.4`.

### Formula 7 — Faucet Rate Model (Expected Value per Kill and per Hour)

```
E[value per kill] = E[drop_count] × E[sell_value | rarity distribution]
                   + E[core sell_value | rarity distribution]
                   + effective_rare_chance_percent × rare_entry_reference_value

faucet_value_per_hour = kills_per_hour × E[value per kill]
```

Where `E[sell_value | rarity distribution]` and `E[core sell_value | rarity distribution]` are
computed by weighting `the-forge-system` Formula 6's `sell_value` (itself built on Formula 1's
IPS) across Formula 2's rarity probabilities — this document does **not** invent a competing
valuation formula, per `the-forge-system` §3.7's own stated expectation ("this document's
Formula 1 (Item Potency Score) is not duplicated by an independent drop-value system").

**Worked `sell_value` table** (weapon, `base_type_weight = 1.2`; core, `base_type_weight = 1.5`;
both at `slot_fill_chance` per Formula 6; `sell_conversion_rate = 15`):

| `rarity_tier_index` | Weapon `sell_value` | Core `sell_value` |
|---|---|---|
| 0 (Common) | 28 | 23 |
| 1 (Uncommon) | 38 | 28 |
| 2 (Rare) | 51 | 35 |
| 3 (Epic) | 68 | 44 |
| 4 (Legendary) | 91 | 55 |

**Worked full per-kill and per-hour comparison** (`rare_entry_reference_value = 182`, illustrative —
roughly 2× a Legendary weapon's `sell_value`). **All rows are anchored to the same fixed
`par_clear_time_seconds = 15s`** (`encounter-spawn-system`'s Whelp Thicket template), which is
240 kills/hour at exactly 100% efficiency. Automation's kills/hour is derived from its
`idle_efficiency_percent`; active's from its `CTR`.

> **This table previously compared peak active play against `automation_baseline_clear_time_seconds
> = 45s` — automation at its *weakest* (a newly-conquered region, 80 kills/hour) — and reported the
> result as "255% vs. automation." That was one of the two invalid proofs `/review-all-gdds` (2026-07-14)
> caught: it flattered active play by silently benchmarking it against the worst possible farm, and it
> concealed the fact that against a *good* farm the old formula made active play score **worse**. Every
> row below is now benchmarked against the same fixed anchor, so the comparisons are real.**

| Case | Efficiency | E[drop_count] | E[value/kill] | Kills/hour | **Value/hour** |
|---|---|---|---|---|---|
| Automation — newly conquered | 33.3% | 2.05 | 93.6 | 80 | 7,488 |
| Automation — partially mastered | 60% | 2.05 | 93.6 | 144 | 13,478 |
| **Automation — fully mastered** | **100%** | 2.05 | 93.6 | **240** | **22,464** *(the reference)* |
| Automation — optimized team | 120% | 2.05 | 93.6 | 288 | 26,957 |
| **Active — focused (sustained)** | CTR ≈ 1.175 | 3.21 | 140.9 | ≈282 | **39,734** |
| **Active — peak skill (brief)** | CTR ≈ 1.35 | 4.00 (clamped) | 179.4 | ≈324 | **58,126** |

**The contract, verified against the strongest farm rather than the weakest:**

| Comparison | Sustained focused active | Brief peak active |
|---|---|---|
| vs. **fully-mastered** automation (the reference) | **1.77×** | **2.59×** |
| vs. **optimized** automation (the strongest possible farm) | **1.47×** | **2.16×** |

Every figure sits inside the brief's "avoid sustained active play being more than roughly 3× better" —
and, critically, **active still wins against the best farm in the game**, which is the half of Pillar 3
the old anchor destroyed. (Against a *newly conquered* region the ratio is 5.3–7.8×; that is Pillar 2
working as intended — fresh automation is *supposed* to be poor — and is not what the ~3× ceiling
describes. See `combat-encounter-system` §4 Formula 7.)

Note also that automation's **per-kill** value stays flat at 93.6 across every mastery level: mastery
buys automation *speed*, never *loot quality*. That is `automation_rarity_parity_percent` doing its job
deliberately, and it is why active play's advantage (better drops per kill: 140.9 and 179.4) cannot be
farmed away by simply owning a better team.

**Note on units**: `faucet_value_per_hour` is denominated in **item value** (Gleam-equivalent, via
IPS), not realized currency. See Formula 8 for why this distinction is the entire resolution of
the faucet-vs-sink question.

### Formula 8 — Faucet-vs-Sink Balance Check (The Critical Analysis)

**The question this document was asked to answer rigorously**: does 24/7 automation's lifetime
output eventually inflate the economy into meaninglessness?

**Step 1 — the rate is bounded and known.** Formula 7 proves `faucet_value_per_hour` is a finite,
calculable number at every automation stage and every active-play skill level — there is no
runaway term anywhere in Formulas 1–7; every multiplier is clamped, every distribution sums to 1,
every quantity has a hard ceiling. Over a **100-hour unattended session** at Automation Stage 2
(`≈7,600` value/hour): **≈760,000 Gleam-equivalent of item value is minted** — large, but finite
and fully accounted for.

**Step 2 — gross item value is not the same as realized currency.** This is the load-bearing
distinction. Per game-concept.md's own MVP Definition, MVP automation covers only **Stages 1–2
(auto-attack + auto-loot)** — "auto-craft/sell" is explicitly a *later*, not-yet-scoped automation
stage. At MVP, therefore, **items accumulate in inventory (A5: uncapped) but do not convert to
Gleam automatically.** Gleam only comes into existence when a player manually visits the Forge and
sells/dismantles/feeds an item (`the-forge-system` §3.5). Currency realization is
**player-paced**, not automation-paced — a 100-hour idle stretch produces a large item backlog,
never a large currency balance, until the player chooses to process it.

**Step 3 — the forward-looking finding, honestly flagged.** `the-forge-system` §3.7 already
identified, but explicitly declined to resolve, this exact tuning dependency: *"As long as
`vow_binding_cost` is tuned such that a player cannot indefinitely afford unlimited
Vow-bindings purely from routine Sell income... [this] depends on `resonance-weaving-system`'s
not-yet-defined Vow cadence."* This document now supplies the missing number: **if** a future
automation stage (Vertical Slice or later) grants continuous, unattended auto-sell, the same
100-hour session would realize the full ≈760,000 Gleam — against `vow_binding_cost = 200` Gleam
(`the-forge-system` §7), that is **enough to afford ≈3,800 Vow-bindings**, against a Vow cadence
bounded by at most 4 ability-focus equip slots (`resonance-weaving-system` §3.5) plus occasional
rebinding — realistically low tens of bindings across an entire playthrough. That is a **two-to-three
order of magnitude oversupply.**

**Conclusion, and the recommendation this document makes without overstepping its scope**: the
faucet's *rate* is not the defect — it is proven bounded (Step 1) and, at MVP, correctly firewalled
from currency realization by the absence of auto-sell (Step 2). The defect that **would** exist is
in an unscaled Gleam *sink* once a future automation stage removes that firewall — and that fix
belongs to whichever system introduces continuous auto-sell (`region-mastery-automation-system`)
in coordination with `the-forge-system` (which owns `vow_binding_cost`), not to this document. Two
concrete options are flagged for that future design pass: **(a)** scale `vow_binding_cost` with
the number of Vows already bound on the target item (thematically consistent with "the old scar
remains, the new one costs more"), or **(b)** cap auto-sell's Gleam realization rate independently
of drop rate, deferring full realization to a manual Forge visit. **This document does not
implement either fix** — implementing (a) would mean editing `the-forge-system.md`, outside this
task's file discipline; implementing (b) would mean designing `region-mastery-automation-system`,
a system this document explicitly does not own. What this document *does* provide is the numeric
proof that one of these fixes will be needed before (not after) an auto-sell automation stage
ships — the single most concrete, actionable output of this section.

## 5. Edge Cases

1. **A loot table references an item definition that doesn't exist**, or `LootTableBinding`
   itself has no entry for a defeated `template_id`. **Ruling: degrade to inert, never crash**,
   matching this project's established content-bug philosophy (`item-data-schema` Edge Case 3:
   "live-game content bugs must degrade to inert, never to a hard failure"). A single unresolvable
   `LootEntry` is simply excluded from its bucket's weighted pool for that roll (§3.3 step 3b's
   downward re-filter already handles a resulting empty pool at a given rarity). If **every**
   bucket a kill would draw from is entirely unresolvable (a missing `LootTableBinding`, or a
   `LootTable` whose every entry is broken), that kill simply produces **zero regular-slot
   items** — but the guaranteed creature core (§3.7) and the loot filter engine are entirely
   unaffected, since neither depends on `LootTableBinding` resolving. A dev/debug build logs a
   warning in either case; a shipped build never surfaces an error to the player.
2. **A filter rule would auto-sell or auto-dismantle a Legendary item.** **Ruling: blocked,
   unconditionally**, by the hard safety net (§3.9.3) — the matched rule's action is silently
   downgraded to `keep` for that one item. Epic items receive the same protection by default,
   player-togglable via `loot-filter-ui` (not this document's concern beyond exposing the toggle
   point).
3. **A `creature_core` Item Definition (`"core_" + template_id`) somehow fails to resolve** (a
   content-authoring gap — a creature template exists but its corresponding core Definition was
   never authored). **Ruling**: this is treated as a **content-authoring blocking bug**, not a
   live-game degrade-to-inert case, because a core is guaranteed, Pillar-3-critical progression
   material — silently dropping nothing would violate "should not... lock... core crafting
   materials." Content validation at build/authoring time should catch this before ship (every
   root `CreatureTemplate` must have a resolvable `"core_" + template_id` Definition, checked as
   part of content QA). As a defensive live-game fallback only, this document specifies one
   `fallback_creature_core_definition_id` per Source (6 total, §7) that mints at `Common` rarity
   if the derived ID is ever unresolvable in a shipped build — ensuring the guarantee holds even
   in a worst-case content gap, never silently skipping the core entirely.
4. **A creature dies to a DoT/hazard tick after the player has already left the encounter.**
   **Ruling: this cannot occur, by `combat-encounter-system`'s own design**, not a case this
   document needs to separately handle. That document's Edge Case #12 and §3.10's hazard
   lifecycle both confirm: a manual Retreat immediately discards all in-flight
   damage-over-time/hazard effects at the `ACTIVE → RESOLVING` transition, and hazards "never
   carry over between encounters." There is no asynchronous or delayed-kill path in this game's
   combat model — the `CreatureDefeated` event (this document's only trigger) always fires
   synchronously, within the same encounter, before the player can have "left."
5. **A part-break sequence produces the maximum possible bonus items and the maximum possible
   quantity uplift simultaneously (every part broken, peak `active_efficiency_percent`).**
   **Ruling: not exploitable**, by construction across three independent clamps: Formula 1's
   `effective_bonus_drop_chance_percent` is clamped to ≤100% (hard-bounding `drop_count` at
   `drop_count_base + bonus_roll_attempts`); Formula 3's `bonus_items_from_part_breaks` is
   hard-capped at `max_part_break_bonus_items_per_kill` regardless of how many parts a boss has;
   and the upstream `part_break_loot_bonus_percent` input itself is already capped at 40% by
   `combat-encounter-system` Formula 4, before it ever reaches any formula in this document. See
   Formula 1's worked "peak skill" example, which deliberately demonstrates the ceiling being
   reached.
6. **An automated kill occurs before `region-mastery-automation-system` exists to supply
   `automation_stage`.** (Current state — that system is not yet authored.) **Ruling, mirroring
   `combat-encounter-system` Edge Case 11's precedent for its own not-yet-available external
   input**: `automation_rarity_parity_percent(automation_stage)` defaults to its Stage-1 value
   (0%) whenever `automation_stage` is unavailable — the most conservative, active-favoring
   default — rather than blocking drop resolution. The moment that system supplies a real
   `automation_stage`, this formula becomes live with no further change to this document.
7. **A Kill occurs from an ability whose damage source is ambiguous between "active" and
   "automated"** (a hypothetical hybrid — not currently possible per A3's binary treatment, but
   flagged for completeness). **Ruling**: out of scope — this document's inputs
   (`part_break_loot_bonus_percent`, `active_efficiency_percent`) are supplied wholesale by
   `combat-encounter-system`/`region-mastery-automation-system` per kill; if a future hybrid
   input mode is designed, it is those systems' responsibility to supply a single, resolved value
   for each input, not this document's to disambiguate.
8. **Two or more items in the same `drop_count` batch roll the exact same `item_definition_id` at
   the exact same rarity, and the Definition is `stackable`.** **Ruling**: mint as separate Item
   Instances first (per §3.3 step 3c), then apply `item-data-schema` §3.10's own stacking rule
   normally — this document does not special-case same-kill stacking; it is identical to any two
   stackable instances of the same Definition meeting anywhere in inventory.
9. **The player's inventory would, after a very long unattended automated session, hold an
   extremely large number of unprocessed items.** **Ruling (A5)**: this game has no hard inventory
   capacity limit at MVP — there is no "inventory full" blocking condition for this document to
   handle, by explicit design call. The Loot Filter Rule Engine (§3.9) exists to let a player (or
   their future automation) keep the *decision* backlog manageable, but it is a convenience and
   economic-legibility tool, not a capacity safety valve — dropped items are never lost, rejected,
   or silently discarded due to volume. If a future document introduces an inventory cap, it must
   reference this ruling and design its own explicit overflow policy; this document guarantees
   only that it never blocks a mint due to capacity.
10. **A `LootFilterRule`'s `conditions` list references a `field` that does not exist on a given
    Item Instance's `base_type`** (e.g. a `has_enchantment_type` condition evaluated against a
    `creature_core`, which never has `enchantments`). **Ruling**: the condition simply evaluates
    to `false` for that field (an empty/absent field never matches a positive condition), never an
    error — consistent with `item-data-schema` Edge Case 3's "fail inert, not fail loud"
    precedent for content mismatches.

## 6. Dependencies

**Depends on**:

- **`encounter-spawn-system.md`** — supplies **`power_tier`**, which this document consumes to scale
  the rarity distribution (Formula 2): higher-tier creatures skew their drop rolls toward the upper
  rarity tiers. Before that system existed, `power_tier` had four consumers and **no producer**
  (Blocker B2), so this document's central input had no owner.
- **`combat-encounter-system.md`** — supplies **`active_efficiency_percent`** (its Formula 7), which
  drives this document's active loot premium (`max(0, (active_eff − 100) / 100)`, Formulas 1 and 5).
  **This premium is only meaningful because that formula is now anchored to a fixed
  `par_clear_time_seconds`.** Under the original moving anchor, `active_efficiency_percent` decayed as
  the player's automation improved, so the premium **clamped to exactly zero** once a region reached
  Fully Mastered — silently deleting the entire reward for playing actively (Blocker B1). Also supplies
  `part_break_loot_bonus_percent` and the `CreatureDefeated` event.
- **`item-data-schema.md`** — every minted drop is an Item Instance built against that schema's
  exact fields (`base_type`, `rarity`/`rarity_tier_index`, `source`, `modifiers`, `enchantments`,
  `ability_focus`, `stackable`, `quantity`, `source_event = loot_drop`, `merged_from = null`), and
  reuses Formulas 1–3 (`modifier_slot_count`, `enchantment_slot_count`, `rarity_value_multiplier`)
  directly rather than re-deriving them (§3.8, Formula 6).
- **`combat-encounter-system.md`** — the sole trigger (`CreatureDefeated` event, §3.1) and two
  directly-consumed inputs: Formula 4 (`part_break_loot_bonus_percent`) and Formula 7
  (`active_efficiency_percent`), both referenced by exact name throughout this document.
- **`creature-data-schema.md`** — reads `CreatureTemplate.tier`/`source`/`parts`,
  `PartDefinition.loot_modifier_tag`/`break_priority`, and `CreatureInstance.power_tier` directly;
  this document's own extension table (`LootTableBinding`, A1) is additive and does not modify
  that schema.
- **`the-forge-system.md`** — reused directly, not re-derived, for all value/currency modeling
  (Formula 1 IPS, Formula 6 `sell_value`) in this document's Formula 7–8. This document's own
  economic-closure analysis (Formula 8) is the explicit continuation of that document's §3.7,
  which named `loot-drop-system` as its one external faucet and flagged the exact
  `vow_binding_cost`-vs-Vow-cadence tuning question this document's Formula 8 now answers
  numerically.

**Depended on by** (bidirectional — each system below must reference this document's exact field
names/formulas when authored):

- **`hunter-progression-system.md`** — Hunter Training is bought with Gleam, and Gleam originates
  (via `the-forge-system`'s Sell) from **this document's drops**. That makes this document the
  ultimate faucet behind the player's own power curve, and it is why this document's active loot
  premium matters beyond loot itself: it is also the rate at which an active player can afford to
  level. The premium's survival across every mastery level (see Formula 1's total-throughput
  re-verification) is therefore load-bearing for **Pillar 3** in a second, less obvious way — if it
  had stayed clamped to zero (Blocker B1), active play would have stopped funding progression faster
  than idle, not merely stopped dropping better items.
- **`the-forge-system.md`** (already authored) — every item it operates on (merge, sell,
  dismantle, feed, Vow-bind inputs) originates from this document's drops; its own §3.7 already
  names this document as its sole external faucet and expects its Formula 1 (IPS) not to be
  duplicated here — confirmed: this document reuses that formula rather than inventing a
  competing one (Formula 7).
- **`creature-jobs-evolution-system`** — consumes `creature_core` (§3.7) and
  `enchantment_material` drops as evolution feed material; must independently implement
  `item-data-schema` Edge Case 1's `vow_binding_log` exclusion filter (moot for fresh drops, since
  a freshly-minted instance never has a non-empty `vow_binding_log`, but stated for consistency
  with that document's own defense-in-depth expectation).
- **`region-mastery-automation-system`** — **must supply `automation_stage`**
  (A4) as this document's Formula 2 input, and is the system whose eventual "auto-craft/sell"
  stage design must account for Formula 8's flagged Gleam-realization finding before it ships.
  Also the expected future owner of any region-level `LootTable` content (§3.2.4).
- **`loot-filter-ui`** — the presentation/authoring layer for `LootFilterRule`
  content (§3.9); this document owns the schema and evaluation semantics in full, that system owns
  layout, drag/reorder-vs-keyboard-equivalent interaction (art-bible §7.7's accessibility
  requirement), and the Epic-lock toggle's UI.
- **`combat-hud`** — the presentation layer for an active kill's `LootResult`
  (drop popup), reading this document's minted-instance batch (§3.3 step 9).
- **`rare-creature-capture-system`** (Vertical Slice tier) — informational only:
  a successful Capture bypasses this document entirely (A6); that system's own GDD should confirm
  this non-interaction explicitly rather than assume it.

**Adjacent systems (informational, not a dependency in either direction)**:

- **`design/art/art-bible.md`** §4.3 — the rarity ring-ornament grammar this document's Formula 2
  output is visually expressed through; this document owns the probability, not the render.

## 7. Tuning Knobs

| Knob | Default | Safe Range | Affects | Notes |
|---|---|---|---|---|
| `drop_count_base_standard` | 1 | 1–3 | Formula 1 floor, standard creature | |
| `drop_count_base_boss` | 3 | 2–5 | Formula 1 floor, boss creature | |
| `bonus_drop_chance_percent` | 35% | 15–50% | Formula 1 base bonus-roll chance | |
| `bonus_roll_attempts` | 3 | 1–5 | Formula 1 max bonus rolls; also the effective ceiling on `drop_count` above the base | |
| **`part_break_quantity_scalar`** | 0.6 | 0.2–1.0 | Formula 1 & 5 quantity uplift from part-break | **BALANCE-CRITICAL** |
| **`efficiency_quantity_scalar`** | 0.8 | 0.3–1.2 | Formula 1 & 5 quantity uplift from active efficiency | **BALANCE-CRITICAL** |
| **`base_rarity_weight`** (5-value array) | `[700,250,45,4.5,0.5]` | sum-preserving retuning only | Formula 2 baseline distribution | **BALANCE-CRITICAL** — the single most impactful knob in this document; retuning any one value should be checked against the full worked-example table (§4 Formula 2) for unintended tier collapse. |
| **`power_tier_rarity_scalar`** | 0.15 | 0.05–0.30 | Formula 2 tilt from region depth | **BALANCE-CRITICAL** |
| **`part_break_rarity_scalar`** | 0.6 | 0.2–1.0 | Formula 2 tilt from part-break | **BALANCE-CRITICAL** |
| **`automation_rarity_parity_percent`** (Stage 1 / Stage 2) | 0% / 50% | 0–100% per stage, monotonically non-decreasing across stages | Formula 2's automation dampening | **BALANCE-CRITICAL, NON-NEGOTIABLE FLOOR** — must never dampen below 0% (that would zero out a tier, violating Pillar 3) and the floor `base_rarity_weight` itself must never be scaled down by this knob (only the bonus tilt above it) — see Formula 2's "never zero" property. |
| `max_part_break_bonus_items_per_kill` | 3 | 1–5 | Formula 3 hard cap | Prevents high-part-count boss exploitation (Edge Case 5) |
| `guaranteed_boss_drop_count` | 1 | 1–3 | §3.5 boss-exclusive guaranteed items | |
| **`rare_drop_roll_chance_percent`** | 2% | 0.5–5% | Formula 5 jackpot-roll base chance | **BALANCE-CRITICAL** — governs reward-schedule pacing directly |
| `base_fill_ratio` | 0.5 | 0.3–0.7 | Formula 6, Common-tier slot fill chance | |
| `fill_ratio_rarity_bonus` | 0.1 | 0.05–0.15 | Formula 6, per-tier fill chance growth | |
| `creature_core_drop_chance_percent` | 100% (**locked**) | not tunable | §3.7 rule 1 | Not a free knob — a Pillar 3 compliance value, matching `item-data-schema`'s own framing of `enchantment_floor`'s hard floor. |
| `pity_threshold_kills` | 150 | 50–300 | §3.11 bad-luck-protection cadence | **BALANCE-CRITICAL** |
| `pity_minimum_rarity_tier_index` | 3 (Epic, **locked**) | not tunable above/below Epic without a design review | §3.11 — locked at Epic specifically to preserve Legendary's rarity-chase status; guaranteeing Legendary-on-a-timer would flatten the game's own rarest tier into a schedule. |
| `epic_auto_disposal_lock_default` | on | on/off, player-togglable via `loot-filter-ui` | §3.9.3 safety net | Legendary's own lock is **not** a knob — always on, never tunable. |
| `fallback_creature_core_definition_id` (×6, one per Source) | content-authored | — | Edge Case 3's defensive fallback | Not a numeric knob; a content-authoring requirement. |

### BALANCE-CRITICAL Flag Summary

Matching `combat-encounter-system`'s own convention: the following knobs directly govern this
document's faucet rate and must be the first values revisited during Vertical Slice playtesting,
in priority order: `base_rarity_weight`, `automation_rarity_parity_percent`,
`power_tier_rarity_scalar`, `part_break_rarity_scalar`, `part_break_quantity_scalar`,
`efficiency_quantity_scalar`, `rare_drop_roll_chance_percent`, `pity_threshold_kills`. Any
retuning of these must be re-checked against Formula 7's per-hour comparison table (target: active
play in the 170–260% band relative to automation, consistent with
`combat-encounter-system` Formula 7's own 128–292% band) and Formula 8's 100-hour session proof
(target: gross faucet value stays a known, finite, bounded quantity — it always will,
structurally, but the specific magnitude should be re-verified after any retune).

## 8. Acceptance Criteria

| # | Criterion | Verification Method |
|---|---|---|
| AC1 | The only event that can trigger a loot mint is `CreatureDefeated` (Kill outcome) — a Capture or Retreat outcome from `combat-encounter-system` never produces any Item Instance via this document. | Unit test: simulate all three encounter outcomes; assert zero minted instances for Capture/Retreat, ≥1 (the guaranteed core) for Kill. |
| AC2 | `drop_count` never falls outside `[drop_count_base(tier), drop_count_base(tier) + bonus_roll_attempts]` for any combination of `part_break_loot_bonus_percent` (0–40%) and `active_efficiency_percent` (50–300%), including pathological extremes. | Unit test sweeping both inputs to their full valid range and their theoretical extremes; assert the bound holds in every case (Formula 1). |
| AC3 | `Σ P(rarity_tier_index = i)` over `i = 0..4` equals exactly 1.0 (within floating-point tolerance) for any valid combination of `power_tier` (1–20), `part_break_loot_bonus_percent` (0–40%), and `automation_rarity_parity_percent` (0–100%). | Unit test sweeping the input space and asserting the probability distribution always sums to 1. |
| AC4 (Pillar 3 — required by task) | For every rarity tier `i = 0..4`, `P(rarity_tier_index = i) > 0` strictly, for any input combination including `automation_rarity_parity_percent = 0%` (worst-case idle throttling) and `power_tier = 1` (earliest possible kill). | Unit test at the most idle-unfavorable input combination, asserting all 5 tiers retain nonzero probability — the direct, testable proof that no rarity tier is ever locked away from idle play (Formula 2's "never zero" property). |
| AC5 (Pillar 3 — required by task) | Every Kill, active or automated, mints exactly one `creature_core` Item Instance with `source` matching the defeated `CreatureTemplate.source` and `definition_id = "core_" + template_id`. | Unit test across both active and automated kill simulations (`automation_stage` present and absent, per Edge Case 6), asserting a `creature_core` instance is present in the `LootResult` batch in every case. |
| AC6 (economic integrity — required by task) | Simulating a 100-hour continuous Automation Stage 2 session at default tuning produces a `faucet_value_per_hour` that never exceeds the Formula 7 worked value (≈7,608, ±10% for RNG variance across a statistically large sample) at any point in the simulated session — i.e., the rate does not drift upward over time (no hidden compounding term). | Statistical simulation: run 100 simulated hours of automated kills at default tuning, bucket `faucet_value_per_hour` into 10-hour windows, assert no window's realized value exceeds the modeled expectation by more than a normal-variance band. |
| AC7 (economic integrity — required by task) | No part-break/quantity/rarity combination produces an unbounded (non-finite, or growing without an asymptotic cap) expected value per kill — `E[value per kill]` at the theoretical maximum input (100% part-break-equivalent, 300% `active_efficiency_percent`, `power_tier = 20`) is a specific, computable, finite number. | Computed test: evaluate Formula 7's full pipeline at the stated extreme inputs and assert the result is finite and matches a hand-computed reference value within tolerance. |
| AC8 | A `LootFilterRule` matching a Legendary item's `auto_sell`/`auto_dismantle` action never results in that item's `status` changing or its removal from inventory — the action is downgraded to `keep` and the original rule is left unmodified. | Unit test: author a broad rule matching all rarities with `action = auto_sell`; evaluate against a Legendary instance; assert the returned disposition is `keep` and the rule's own stored `action` field is unchanged. |
| AC9 | Filter rule evaluation is deterministically first-match-wins in ascending `priority` order — given two rules that would both match the same item, only the lower-`priority`-value rule's action is ever applied. | Unit test: author two overlapping rules at priorities 1 and 2 with different actions against the same item; assert only priority 1's action is returned, across repeated evaluations. |
| AC10 | `bonus_items_from_part_breaks` never exceeds `max_part_break_bonus_items_per_kill`, even when `broken_part_ids` contains more matching-tag parts than the cap. | Unit test: an 8-part boss fixture with 5 parts carrying distinct valid `loot_modifier_tag` values, all broken; assert exactly `min(max_part_break_bonus_items_per_kill, 5)` bonus items mint (Formula 3). |
| AC11 | Every minted Item Instance from this document satisfies every `item-data-schema` invariant (Section 3.11 there) at creation — no drop ever produces an instance failing that schema's own validation. | Integration test: mint 10,000 instances across the full input space (all rarities, all `base_type`s this document can produce) and run `item-data-schema`'s own Acceptance Criteria validators against each. |
| AC12 | Formula 2's four worked-example rows (§4) reproduce exactly at default tuning: Automation Stage 1 → `[70.00, 25.00, 4.50, 0.45, 0.05]`%; Active peak → `[48.68, 37.72, 11.58, 1.75, 0.273]`% (±0.01 percentage points for rounding). | Unit test computing all four worked rows at default tuning and asserting exact match against the documented table. |
| AC13 | `kills_since_epic_or_higher` never exceeds `pity_threshold_kills` before a forced Epic-or-better floor applies to the next kill's first regular slot. | Unit test: simulate `pity_threshold_kills + 1` consecutive kills with rarity rolls forced to Common/Uncommon/Rare only (mocking Formula 2's RNG); assert the pity floor triggers at exactly the configured threshold and the counter resets to 0 immediately after. |
| AC14 | `LootTableBinding` resolution failure (Edge Case 1) never prevents the guaranteed `creature_core` (§3.7) from minting, and never raises an unhandled error — the `LootResult` batch is still returned, containing at minimum the core. | Integration test: simulate a `CreatureDefeated` event for a `template_id` with no `LootTableBinding` entry; assert the returned `LootResult` is non-null, contains exactly one `creature_core` instance, and zero regular-slot items. |

---

## Cross-System Facts Proposed for Registration

This document does not write to `design/registry/entities.yaml` directly, per this task's explicit
file-discipline instruction. The following are proposed for registration by whichever process
coordinates registry writes this session:

**Formulas**:
- `loot_drop_count` — source `design/gdd/loot-drop-system.md` §4 Formula 1. Consumes
  `part_break_loot_bonus_percent` (`combat-encounter-system` Formula 4) and
  `active_efficiency_percent` (`combat-encounter-system` Formula 7) by name.
- `loot_rarity_distribution` — §4 Formula 2. Flagged as the single most economically load-bearing
  formula in this document.
- `part_break_bucket_bonus_items` — §4 Formula 3.
- `creature_core_drop` — §4 Formula 4 (reuses `loot_rarity_distribution`; not an independent
  probability model).
- `rare_bonus_drop_roll` — §4 Formula 5.
- `loot_slot_fill_ratio` — §4 Formula 6.
- `faucet_value_per_hour` — §4 Formula 7. Reuses `the-forge-system`'s registered `item_potency_score`
  and `sell_value` formulas directly — flagged so the registry can note the derivation relationship
  rather than treating this as an independent valuation.

**Constants**:
- `base_rarity_weight` (`[700, 250, 45, 4.5, 0.5]`) — §7. **BALANCE-CRITICAL.**
- `power_tier_rarity_scalar` (`0.15`), `part_break_rarity_scalar` (`0.6`) — §7.
- `part_break_quantity_scalar` (`0.6`), `efficiency_quantity_scalar` (`0.8`) — §7.
- `automation_rarity_parity_percent` table (Stage 1: `0%`, Stage 2: `50%`) — §7. Flagged for
  `region-mastery-automation-system` to extend when later stages are authored.
- `rare_drop_roll_chance_percent` (`2%`) — §7.
- `pity_threshold_kills` (`150`), `pity_minimum_rarity_tier_index` (`3`, locked) — §7.
- `creature_core_drop_chance_percent` (`100%`, locked) — §7.

**Not proposed for registration** (deliberately): the `LootTable`/`LootEntry`/`LootFilterRule`
schemas themselves, and the filter engine's first-match-wins evaluation rule — these are
structural/content-shape facts with no other canonical document to diverge against, cited from
this document's §3 by name, matching the precedent `item-data-schema.md` set for its own
`base_type` enum.
