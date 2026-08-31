# GDD: Item Data Schema

## Document Status

- **Status**: Complete — all 8 sections authored
- **Created**: 2026-07-14
- **Owned By**: systems-designer
- **Review Mode**: `lean` — authored autonomously this session (no user available to approve
  section-by-section; see `production/session-state/active.md`, "rush mode"). Every ambiguity
  the source material left open has been resolved with an explicit, flagged design call rather
  than a placeholder. Validate at `/design-review` and `/gate-check` before Production.
- **Priority**: MVP, Foundation tier — second-biggest dependency bottleneck in the systems index
  (`design/gdd/systems-index.md`), behind only `creature-data-schema`.

## Source Material Read

`design/gdd/game-concept.md` (full), `design/art/art-bible.md` (full — all 9 sections; §3, §4,
§5.2 are the most load-bearing for this document), `design/gdd/systems-index.md` (this system's
entry and its dependents), `design/registry/entities.yaml` (empty at time of writing — no prior
registered facts to reconcile against).

## Open Items / Decisions Carried Forward for Dependent Systems

This schema resolves everything needed to be internally complete, but a few decisions are
explicitly flagged as provisional defaults that the systems which consume this schema should
confirm or override when they are authored. None of these block implementation of this schema
itself.

| Item | Provisional Default (this doc) | Owning System | Confirm At |
|---|---|---|---|
| Can a Vow-bound item be a Forge merge input? | No — blocked at validation (Edge Case 2) | `the-forge-system` | `/design-system the-forge-system` |
| Does `ability_focus` restrict to `charm` items only, or any equippable? | Schema allows `weapon`/`armor`/`charm`; anticipated usage is `charm` per art-bible §5.2's belt/bandolier framing | `resonance-weaving-system` | `/design-system resonance-weaving-system` |
| Authoritative stat name catalog for `Modifier.stat` | Open string key, illustrative examples only | `combat-encounter-system` | `/design-system combat-encounter-system` |
| Authoritative species-key catalog for `creature_core` items | Open string key, illustrative examples only | `creature-data-schema` | `/design-system creature-data-schema` |
| Authoritative Vow-ID catalog for `VowBindingEntry.vow_id` | Open string key | `resonance-weaving-system` | `/design-system resonance-weaving-system` |
| Authoritative enchantment/trigger/effect catalog | Open string keys + illustrative trigger/effect/condition type lists (Section 3.7) sufficient to represent game-concept.md's 4 named examples | Future content-authoring pass | Post-MVP content pass |

---

## 1. Overview

The item data schema is the single, canonical data model every item in IDLExIDLE is built
from — every weapon, piece of armor, charm, enchantment material, and creature core that exists
in the game, whether sitting in a player's inventory, mid-Forge-merge, or freshly dropped by a
broken creature part. It defines two related but distinct shapes: the **Item Definition** (the
static, authored template — "an Ember Blade exists and looks like this") and the **Item
Instance** (the specific, owned object with its own rolled rarity, modifiers, enchantments, Vow
history, and forge lineage — "this specific Ember Blade, which I pulled off a Rare Body-aligned
Shard-Hound, has taken two Vows and one enchantment"). This document is Foundation-tier: it
depends on no other system, and four MVP systems (`resonance-weaving-system`, `the-forge-system`,
`loot-drop-system`, `creature-jobs-evolution-system`) plus one Full Vision system
(`memory-dust-prestige-system`, transitively) are blocked on it existing and staying stable. Every
field below exists because a named dependent system needs it — nothing here is speculative.

## 2. Player Fantasy

A game with no narrative focus (per game-concept.md, narrative is explicitly N/A for MVP) still
needs its objects to feel like they matter, and in IDLExIDLE that weight is carried
entirely by data made visible, not by story text. This schema is what makes that possible: every
item you carry has a story, and that story is legible because it's structured, not decorative —
where it came from (a specific rarity roll off a specific creature, or three items fused at the
Forge), what it's been through (a permanent, append-only log of every Vow it has ever carried,
scars that never disappear even when replaced), and what it costs you (a behavior-changing
enchantment that reshapes how you play, not just a bigger number). This is the direct data-layer
expression of the art bible's Master Visual Rule — "read the world, don't just watch it" — applied
to inventory: a player should be able to look at an item's rarity ring, its Source medallion, its
Vow-scar inlay, and its forge-lineage tooltip, and reconstruct its entire history without reading
a word of lore. Pillar 4 (Builds Are Trade-offs, Not Stat Stacks) lives or dies on whether the
schema can actually hold a restriction and a behavior change as first-class citizens next to a
stat bonus — this document is where that pillar either becomes real or stays a slogan.

## 3. Detailed Rules

### 3.1 Two-Table Model: Item Definition vs. Item Instance

Every item in the game exists at two layers, kept deliberately separate:

- **Item Definition** — static, authored content (one row per distinct item design, e.g. "Ember
  Blade"). Definitions are content data, not save data. Full definition content (display name,
  icon reference, flavor text, drop-table membership, base stat roll ranges per modifier) is
  **out of scope for this document** — that is a future content-authoring pass this GDD
  deliberately does not perform, matching the brief's instruction to define the *shape* an
  enchantment/item takes, not author its content list. This document defines only the fields of
  a Definition that an Instance must be able to reference or denormalize from (Section 3.2).
- **Item Instance** — the actual owned object, one row per item a player (or an
  archived/consumed record) holds. This is save data, and it is where rarity is rolled,
  modifiers and enchantments are populated, Vow history accrues, and forge lineage is recorded.
  This is the layer every dependent system actually manipulates, and the majority of this
  document's rigor is spent here (Section 3.3 onward).

This split exists because rolled/instance-specific data (a Rare item's specific modifier values,
its Vow-binding log) cannot live on the shared Definition without either duplicating the
Definition per roll (wasteful) or losing per-instance history entirely (impossible, given Vow
scars are explicitly required to be permanent and per-object). Any real item/loot system needs
this split; it is not optional complexity.

### 3.2 Item Definition — Minimal Schema

| Field | Type | Valid Values | Description |
|---|---|---|---|
| `definition_id` | string | unique content key | Stable identifier for this authored template. Loosely-typed (not an enforced foreign key) since the content catalog itself is authored post-GDD. |
| `base_type` | enum | `weapon`, `armor`, `charm`, `enchantment_material`, `creature_core` | Fixed per definition; every instance of this definition denormalizes this value (3.3, field 3). |
| `fixed_source` | enum \| null | `body`, `mind`, `nature`, `machine`, `shadow`, `spirit`, or `null` | If set, every instance of this definition is created with this Source. If `null`, the creating system (loot-drop-system for a drop, creature capture for a `creature_core`) must supply `source` explicitly at instance-creation time — this is how a `creature_core`'s Source varies by which creature dropped it, even though `creature_core` as a base type is shared across all creatures. |
| `stackable` | bool | `true` / `false` | Whether instances of this definition are permitted to merge into a shared `quantity` count (3.10). Typically `true` for generic `enchantment_material`, `false` for equippable gear. |
| `ability_focus_eligible` | bool | `true` / `false` | Coarse pre-filter: whether `resonance-weaving-system` is permitted to consider instances of this definition as Vow-bindable at all. The per-instance `ability_focus` flag (3.3, field 11) still governs the actual instance and must be consistent with this value at creation time. |

### 3.3 Item Instance — Full Field Reference

This is the schema every dependent system reads and writes against.

| # | Field | Type | Valid Values / Range | Mutability | Description |
|---|---|---|---|---|---|
| 1 | `item_instance_id` | string (UUID) | unique | Immutable | Unique identifier for this specific owned object. |
| 2 | `definition_id` | string | must resolve against the Item Definition catalog | Immutable | The authored template this instance was created from. |
| 3 | `base_type` | enum | `weapon`, `armor`, `charm`, `enchantment_material`, `creature_core` | Immutable | Denormalized copy of the Definition's `base_type` at creation, for fast filtering without a Definition lookup. Gates which other fields may be non-empty (3.11). |
| 4 | `rarity` | enum | `common`, `uncommon`, `rare`, `epic`, `legendary` | Immutable after creation | The 5 tiers locked by art-bible §4.3 (bone→gold value ramp, countable ring ornament). Drives Formulas 1–3. No rarity-upgrade mechanic exists in this schema — the-forge-system's merge output rolls or derives its own rarity per its own rules; it does not mutate an input's rarity in place. |
| 5 | `source` | enum \| null | `body`, `mind`, `nature`, `machine`, `shadow`, `spirit`, or `null` | Immutable after creation | Elemental Source alignment. `null` = elementally unaligned (a plain stat-stick or generic material). The 6 non-null values are the **exact same enum** as `creature-data-schema`'s Source enum (art-bible §4.2) — same names, same count, deliberately kept identical rather than extended with an item-only 7th value, so cross-schema consistency checks (`/consistency-check`) can diff the two lists directly. `creature_core` instances must never be `null` (3.11). |
| 6 | `status` | enum | `active`, `archived` | Mutable (one-way: `active` → `archived` only) | `active` = owned/usable, appears in inventory. `archived` = consumed by a Forge merge or dismantle; the full record is retained permanently for lineage and Vow-history resolution but never surfaces in player-facing inventory queries (Section 3.9). Items are never hard-deleted from save data. |
| 7 | `stackable` | bool | `true` / `false` | Immutable | Denormalized copy of the Definition's `stackable` flag at creation. |
| 8 | `quantity` | int | ≥ 1 | Mutable only if `stackable = true` | Stack count. Constrained by the uniqueness rule in 3.10. |
| 9 | `modifiers` | array of `Modifier` | length ∈ [0, `modifier_slot_count(rarity)`] (Formula 1) | Populated at creation (rolled by loot-drop-system / the-forge-system); immutable thereafter | Flat stat bonuses (3.6). Only valid on `weapon`/`armor`/`charm`; must be empty on `enchantment_material`/`creature_core` (3.11). |
| 10 | `enchantments` | array of `Enchantment` | length ∈ [0, `enchantment_slot_count(rarity)`] (Formula 2) | Populated at creation, or by a future Forge enchant-step (Vertical Slice+, MVP is merge-only); immutable outside that step | Structured, behavior-changing effects (3.7). Only valid on `weapon`/`armor`/`charm`; must be empty on `enchantment_material`/`creature_core` (3.11). |
| 11 | `ability_focus` | bool | `true` / `false` | Immutable after creation | Whether this instance is eligible to carry a Vow binding. Valid `true` only when `base_type` ∈ {`weapon`, `armor`, `charm`} — a consumable material can never be an "ability focus" since it is never equipped (3.11). |
| 12 | `vow_binding_log` | array of `VowBindingEntry` | append-only | Append-only — entries are never edited or removed | Full chronological history of every Vow ever bound to this instance (3.8). Only ever non-empty when `ability_focus = true`. |
| 13 | `merged_from` | array of exactly 3 `item_instance_id`, or `null` | length ∈ {0 items (via `null`), 3} — no other length is valid | Immutable, set only at creation | Forge lineage: the 3 (now-`archived`) parent instances this item was merged from, or `null` if this instance was not produced by a merge (3.9). |
| 14 | `source_event` | enum | `loot_drop`, `forge_merge`, `forge_dismantle`, `initial_grant` | Immutable | How this instance came to exist. Governs whether `merged_from` may be non-null (`forge_merge` only). |
| 15 | `created_at` | timestamp | — (format owned by `save-load-persistence`) | Immutable | When this instance was created. |
| 16 | `equipped_to_creature_id` | string (UUID) \| null | a `CreatureInstance.instance_id`, or `null` | Mutable (set on equip, cleared on unequip) | **Exclusive ownership lock.** Non-null = this item is currently equipped to a creature (`creature-jobs-evolution-system`'s `equipped_charm_id` slot) and is therefore **not available to any Forge operation**. Valid non-null only when `base_type = charm` and `status = active`. See Invariant 9 and Edge Case 9 below. |

### 3.4 Rarity

Locked by art-bible §4.3 at exactly 5 tiers, on a bone→gold value ramp with a countable ring-frame
ornament — this schema does not own the visual treatment, only the enum and its ordinal index
(used by Formulas 1–3):

| `rarity` value | `rarity_tier_index` | Ring Ornament (art-bible §4.3, for cross-reference only) |
|---|---|---|
| `common` | 0 | Plain border, no ring |
| `uncommon` | 1 | Single ring |
| `rare` | 2 | Double concentric ring |
| `epic` | 3 | Double ring + four corner flourishes |
| `legendary` | 4 | Full Ward-seal ornament |

Rarity drives two things this schema exposes but does not itself implement: **drop-weight**
(`loot-drop-system` decides how often each tier rolls) and **Forge-hybrid eligibility**
(`the-forge-system` decides which tiers may hybridize, post-MVP). This schema's job stops at
exposing `rarity` as a stable, ordered field those systems can key off of.

### 3.5 Source (Element)

Confirmed reading: art-bible §4.2's 6 Source color assignments (Body, Mind, Nature, Machine,
Shadow, Spirit) apply to items exactly as they do to creatures — this is the same taxonomy, not a
parallel one, per the brief's instruction to use an identical enum for cross-system consistency.
See field 5 in 3.3 for the full rule, including the `null`-vs-7th-value decision and the
`creature_core`-is-never-null invariant.

### 3.6 Modifiers — Flat Stat Bonuses

Per Pillar 4 (Builds Are Trade-offs, Not Stat Stacks), flat modifiers are explicitly the **less
interesting half** of itemization — the pillar's own design test says a pure `+X%` stat modifier
loses to a behavior-altering effect whenever the two are in tension. This schema supports
modifiers fully (they are not cut), but deliberately keeps their shape minimal:

| Field | Type | Valid Values | Description |
|---|---|---|---|
| `stat` | string | open key — illustrative examples: `attack_power`, `defense`, `max_health`, `critical_chance`, `move_speed`, `resonance_generation`, `production_rate` | Not a closed enum in this document. The authoritative stat catalog belongs to `combat-encounter-system`, not yet designed. Treating this as an open string key (rather than inventing a false-precision closed enum now) avoids this Foundation-tier document taking on a soft dependency it can't yet resolve. |
| `value` | float | unbounded (loot-drop-system/the-forge-system apply Formula 3's multiplier and their own base ranges) | The flat bonus amount. |
| `value_type` | enum | `flat` (exactly one legal value today) | Deliberately an enum-of-one rather than a bare implied-flat field: this leaves room for a future `percent` value if a later revision needs it, without a schema shape change, while not pre-building capability the current pillar language explicitly deprioritizes. |

### 3.7 Enchantments — Structured, Behavior-Changing Effects

Enchantments are the half of itemization Pillar 4 prioritizes, and game-concept.md is explicit
that they must be representable as genuine behavior changes, not stat lines. This schema
represents an enchantment as three parts — a **trigger** (when it fires), an optional
**condition** (a gating state, for effects that aren't event-triggered), and an **effect** (what
happens) — matching the brief's own `trigger: on_critical_hit, effect: convert_to_poison, params:
{...}` shape directly.

| Field | Type | Description |
|---|---|---|
| `enchantment_instance_id` | string | Unique within the item. |
| `enchantment_definition_id` | string | Loose content-key reference to a future Enchantment Definition catalog (out of scope here — content authoring, not schema design). |
| `trigger.trigger_type` | enum (open, illustrative) | See table below. |
| `trigger.trigger_params` | map\<string, any\> | Open parameter bag; shape is defined per `trigger_type` by the future content catalog, not by this schema. |
| `condition.condition_type` | enum (open, illustrative) \| null | `null` for purely event-triggered enchantments. Present only for state-gated effects (e.g. "while at low health"). |
| `condition.condition_params` | map\<string, any\> \| null | Same open-bag pattern as `trigger_params`. |
| `effect.effect_type` | enum (open, illustrative) | See table below. |
| `effect.effect_params` | map\<string, any\> | Same open-bag pattern. |

**Why the params are an open map, not a fully closed schema**: the brief asks for a schema
"sufficient to represent the 4 examples given, without designing the full enchantment content
list." A closed, fully-typed param schema per effect type would require designing every future
enchantment's parameter shape now, which is explicitly out of scope. The trigger/condition/effect
**structure** is closed and rigorous (three named parts, each independently gated by `_type`); the
**parameters inside each part** are intentionally open, validated by the future content catalog at
authoring time and by `combat-encounter-system` at runtime — this is the standard, correct
boundary between "schema" and "content."

**Illustrative `trigger_type` values** (non-exhaustive, pending the future content catalog):
`on_hit`, `on_critical_hit`, `on_kill`, `on_ability_cast`, `on_block`, `on_dodge`,
`state_condition` (paired with a non-null `condition`, evaluated continuously rather than once).

**Illustrative `effect_type` values**: `split_projectile`, `spawn_minion`,
`convert_damage_type`, `apply_status_effect`, `modify_stat`.

**Illustrative `condition_type` values**: `health_below_percent`, `health_above_percent`.

**Worked mapping — the 4 examples from game-concept.md**, proving the schema is sufficient:

| Example (game-concept.md) | `trigger_type` | `condition` | `effect_type` | `effect_params` (illustrative) |
|---|---|---|---|---|
| Splitting projectiles | `on_ability_cast` (`{ability_tag: "projectile"}`) | none | `split_projectile` | `{additional_projectiles: 2, spread_angle_degrees: 15}` |
| Spawning a minion after a kill | `on_kill` | none | `spawn_minion` | `{minion_definition_id: "spirit_wisp_minion", duration_seconds: 20, count: 1}` |
| Converting critical hits into poison | `on_critical_hit` | none | `apply_status_effect` | `{status_id: "poison", damage_conversion_percent: 100, duration_seconds: 4}` |
| Increasing production while at low health | `state_condition` | `health_below_percent: {threshold_percent: 30}` | `modify_stat` | `{stat: "production_rate", value: 25, value_type: "percent"}` |

Note the last row's `effect_params.value_type = "percent"` is **not** a contradiction of 3.6's
`Modifier.value_type` being flat-only — `Modifier` and `Enchantment.effect_params` are different
objects with different design intents. `Modifier` is deliberately minimal because it's the
deprioritized half of Pillar 4; `Enchantment.effect_params` is deliberately open because it's the
prioritized half. The schema's own shape reinforces the pillar.

**Worked full instance example** (grounding the abstract schema in one concrete object, for
readability — not a formula worked example, just an illustration):

```
item_instance_id: "itm_7f3a..."
definition_id: "ember_charm_t2"
base_type: charm
rarity: rare                 (rarity_tier_index = 2)
source: body
status: active
stackable: false
quantity: 1
modifiers: [ {stat: "max_health", value: 18, value_type: flat} ]
enchantments: [
  {
    enchantment_instance_id: "ench_01",
    trigger: {trigger_type: on_critical_hit, trigger_params: {}},
    condition: null,
    effect: {effect_type: apply_status_effect,
             effect_params: {status_id: "poison", damage_conversion_percent: 100, duration_seconds: 4}}
  }
]
ability_focus: true
vow_binding_log: [
  {binding_id: "vb_01", vow_id: "vow_no_dodge", bound_at: "...", status: active, replaced_by: null}
]
merged_from: null
source_event: loot_drop
created_at: "..."
```

This instance has 1 of 3 available modifier slots filled and 1 of 2 available enchantment slots
filled (Rare = `modifier_slot_count` 3, `enchantment_slot_count` 2 — Formulas 1–2) — the schema
does not require slots to be full, only that they never exceed the cap.

### 3.8 Ability-Focus & Vow Binding

Per art-bible §5.2, Vows bind to "a small, fixed number of ability-focus items" and leave a
permanent, visible mark; if a Vow is later replaced, "the old scar remains... never erased." This
schema implements that as an **append-only log**, never an overwritable field.

**`VowBindingEntry` fields:**

| Field | Type | Description |
|---|---|---|
| `binding_id` | string (UUID) | Unique per entry. |
| `vow_id` | string | Loose content-key reference to `resonance-weaving-system`'s future Vow catalog. |
| `bound_at` | timestamp | When this binding was created. |
| `status` | enum | `active` or `superseded`. |
| `replaced_by` | string (`binding_id`) \| null | Set on a `superseded` entry once a later entry replaces it; `null` on the current `active` entry. |

**Rules:**

- At most one entry in `vow_binding_log` may have `status = active` at any time.
- Binding a new Vow to an already-bound item appends a new `active` entry and flips the previous
  `active` entry to `superseded` (setting its `replaced_by`) — the old entry is never deleted or
  mutated beyond that one status flip and `replaced_by` write. This is the data-level
  implementation of "the old scar remains."
- **Design call**: this schema does not model an "unbind to fully unbound" operation — the source
  material only describes replacement ("if the Vow is later replaced"), never removal-to-none. An
  item that has ever been bound stays permanently in a bound-or-superseded state; it can never
  return to a pre-binding state. This is a one-directional lifecycle by design, matching the
  "permanent mark" framing.
- `vow_binding_log` may only be non-empty when `ability_focus = true` (3.11).

### 3.9 Forge Lineage & Item Status Lifecycle

Per game-concept.md, "three compatible items can be merged into a stronger version," and unwanted
loot can be "dismantled into materials." This schema carries the resulting **data shape**, not the
merge/dismantle rules themselves (owned by `the-forge-system`).

- **Merge**: consumes exactly 3 input instances (each flips `status` → `archived`) and produces 1
  new output instance with `source_event = forge_merge` and `merged_from` set to the 3 archived
  parents' `item_instance_id`s. Archived parents are never hard-deleted, specifically so
  `merged_from` references never dangle and a merged item's lineage tooltip can always resolve.
- **Dismantle**: consumes exactly 1 input instance (flips `status` → `archived`) and produces one
  or more `enchantment_material` instances with `source_event = forge_dismantle`. **Design call**:
  dismantle output does **not** carry a per-unit parent reference (no `dismantled_from` field
  exists in this schema). Materials are typically `stackable`, and once dismantle output from
  multiple different parents blends into a shared stack, per-unit provenance stops being a
  meaningful or trackable concept — attempting to preserve it would force every dismantle output
  to be non-stackable, which contradicts materials' normal fungibility. Merge output is
  lineage-tracked because it produces one significant, unique item worth remembering the history
  of; dismantle output is not, because it produces fungible bulk material. This asymmetry is
  intentional, not an oversight.
- `status = archived` items are excluded from all player-facing inventory, loot-filter, and
  roster queries, but remain resolvable by `item_instance_id` for lineage-history and
  Vow-history display purposes only.

### 3.10 Stacking

- `stackable` (denormalized from the Item Definition) governs whether instances of the same
  `definition_id` may share a `quantity` count instead of existing as separate records.
- **Uniqueness rule**: `quantity` must equal exactly `1` if **any** of the following are true for
  the instance: `modifiers` is non-empty, `enchantments` is non-empty, `vow_binding_log` is
  non-empty, or `merged_from` is non-null. An item that has accrued any unique history is
  permanently quantity-locked to 1 and can never join or re-join a shared stack, even if its
  `definition_id` matches an existing stack. This prevents a Vow-bound or enchanted item from ever
  being silently merged into (and indistinguishable from) a generic stack.
- A `max_stack_size` ceiling exists as a Tuning Knob (Section 7); it is a data cap this schema
  exposes, not a mechanic this document designs beyond that cap.

### 3.11 Invariants (Cross-Field Validation Summary)

Collected here for scannability; each is independently stated above and each maps to an
Acceptance Criterion in Section 8.

1. `modifiers` and `enchantments` are non-empty only when `base_type` ∈ {`weapon`, `armor`,
   `charm`}; always empty on `enchantment_material` and `creature_core`.
2. `ability_focus = true` is valid only when `base_type` ∈ {`weapon`, `armor`, `charm`}.
3. `vow_binding_log` is non-empty only when `ability_focus = true`.
4. `creature_core` instances never have `source = null`.
5. `merged_from` is non-null only when `source_event = forge_merge`, and when non-null it
   contains exactly 3 entries — no other length is valid.
6. `quantity > 1` is valid only when `modifiers`, `enchantments`, and `vow_binding_log` are all
   empty and `merged_from` is `null` (3.10).
7. `len(modifiers) <= modifier_slot_count(rarity)` and `len(enchantments) <=
   enchantment_slot_count(rarity)` at all times (Formulas 1–2).
8. At most one `VowBindingEntry` in a given `vow_binding_log` has `status = active`.
9. `equipped_to_creature_id` is non-null only when `base_type = charm` and `status = active`.
   **A non-null `equipped_to_creature_id` makes the instance ineligible for every Forge operation
   (merge input, sell, dismantle, creature-feed) and for equipping to any second creature.** Exactly
   one creature may reference a given charm at a time; the reference is bidirectional with
   `creature-jobs-evolution-system`'s `CreatureInstance.equipped_charm_id` and the two must never
   disagree.

> **Added 2026-07-14 (cross-system gap closure).** Field 16 and Invariant 9 were added after
> `creature-jobs-evolution-system` (A3) and `creature-roster-ui` (Edge Case 4 / AC11) independently
> flagged that this schema had no ownership lock — meaning a charm could be simultaneously equipped
> to a creature *and* consumed by a Forge merge, sell, dismantle, or feed. `creature-roster-ui` added
> a defensive detect-and-clear mitigation, but the fix belongs here, in the data model. This mirrors
> the exclusion pattern already used for Vow-bound items (Edge Case 1): the Forge's input validators
> must reject any candidate where `equipped_to_creature_id != null`, exactly as they already reject
> `len(vow_binding_log) > 0`.

## 4. Formulas

Three derived values exist in this schema: how many modifier slots a rarity tier grants, how many
enchantment slots it grants, and how much a rarity tier multiplies a rolled stat's value. All
three are deliberately simple, discrete functions of `rarity_tier_index` (0–4) — this is a
Foundation-tier schema, and the actual *feel* tuning (base roll ranges per stat, drop weights)
belongs to `loot-drop-system`; this document only fixes the shape those systems must build on top
of, so every dependent system derives rarity-scaling the same way instead of inventing its own.

### Formula 1 — Modifier Slot Count

```
modifier_slot_count = base_modifier_slots + rarity_tier_index × slots_per_tier
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `rarity_tier_index` | int | 0–4 | Rarity ordinal (Common=0 ... Legendary=4). |
| `base_modifier_slots` | int | tuning knob, default 1 | Modifier slots granted to a Common item. |
| `slots_per_tier` | int | tuning knob, default 1 | Additional modifier slots per tier above Common. |
| `modifier_slot_count` | int | 1–5 at default tuning | Maximum length of an instance's `modifiers` array (Invariant 7). |

**Output range**: non-negative integer, effectively bounded to 1–5 given the locked 5-tier rarity
system and default tuning values (Section 7). This is a hard ceiling on array length, not a
guarantee that slots are filled — actual roll count is `loot-drop-system`'s decision.

**Worked example**: a Rare item (`rarity_tier_index = 2`) at default tuning:
`modifier_slot_count = 1 + (2 × 1) = 3`.

### Formula 2 — Enchantment Slot Count

```
enchantment_slot_count = enchantment_floor + floor(rarity_tier_index / tier_divisor)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `rarity_tier_index` | int | 0–4 | Same as Formula 1. |
| `enchantment_floor` | int | tuning knob, default 1, hard minimum 1 | Minimum enchantment slots every rarity tier must expose. The floor of 1 (never 0) is a deliberate, non-negotiable Anti-Pillar compliance value: game-concept.md's Anti-Pillars explicitly reject "flat stat-only gear dominating the loot pool" — every rarity tier, including Common, must structurally be capable of carrying at least one behavior-changing enchantment. |
| `tier_divisor` | int | tuning knob, default 2 | How many rarity tiers must pass before an additional enchantment slot unlocks. |
| `enchantment_slot_count` | int | 1–3 at default tuning | Maximum length of an instance's `enchantments` array (Invariant 7). |

**Output range**: non-negative integer, minimum 1 by construction (`enchantment_floor` ≥ 1),
effectively bounded to 1–3 at default tuning across the 5 locked tiers.

**Worked example**: a Legendary item (`rarity_tier_index = 4`) at default tuning:
`enchantment_slot_count = 1 + floor(4 / 2) = 1 + 2 = 3`. A Common item (`rarity_tier_index = 0`):
`1 + floor(0 / 2) = 1` — confirming Common items always carry at least one enchantment slot.

### Formula 3 — Rarity Value Multiplier

```
rarity_value_multiplier = (1 + growth_rate) ^ rarity_tier_index
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `rarity_tier_index` | int | 0–4 | Same as Formula 1. |
| `growth_rate` | float | tuning knob, safe range 0.15–0.40, default 0.25 | Per-tier geometric growth rate. |
| `rarity_value_multiplier` | float | ≥ 1.0, monotonically increasing | The multiplier `loot-drop-system` applies on top of a stat's own base min/max roll range (owned by `loot-drop-system` / future content, not this schema) to produce a rarity-scaled final value. |

**Output range**: `[1.0, (1 + growth_rate)^4]`. At default tuning: `[1.0, 2.441]`. Uncapped above
by the formula's own shape, but effectively bounded because rarity is locked to exactly 5 tiers —
there is no tier 5 to plug in. Geometric (not linear) growth is a deliberate genre-fit choice:
the idle-game comparable titles this project cites (Melvor Idle) lean on compounding power curves,
and a flat/linear rarity multiplier would make Legendary feel only marginally better than Common,
undermining the rarity ramp the art bible already commits to visually.

**Worked example**: `growth_rate = 0.25`. Tier multipliers: Common `1.000`, Uncommon `1.250`, Rare
`1.5625`, Epic `1.953`, Legendary `2.441`. A stat with a base roll range of `[4, 6]` on a Rare
item becomes `[4 × 1.5625, 6 × 1.5625] = [6.25, 9.375]` before `loot-drop-system`'s own rounding
rule (out of scope here) is applied.

**Why no fourth formula (e.g. an aggregate "item power score")**: no dependent system in the
current systems index consumes a single aggregate power number — `loot-drop-system` needs
per-tier slot counts and a multiplier, `the-forge-system` needs lineage and rarity, `combat`
consumes individual stat/effect values directly. Inventing an aggregate score now would be
speculative complexity with no consumer; if a future UI (`loot-filter-ui`, item comparison
tooltips) needs a sort key, it should be added then, against a real requirement.

## 5. Edge Cases

1. **A Vow-bound item is fed to a creature as evolution material.** Per game-concept.md,
   creature evolution is "driven by... consumed materials." **Ruling: blocked.** Any item with a
   non-empty `vow_binding_log` cannot be selected as valid input to
   `creature-jobs-evolution-system`'s feed-material pool — that system's input validator must
   reject any candidate where `len(vow_binding_log) > 0`, regardless of `ability_focus` or
   `base_type`. Rationale: a Vow scar is explicitly permanent and player-facing ("never erased");
   letting it be casually consumed as creature food would both contradict that permanence framing
   and create a punishing accidental-loss failure mode inconsistent with Pillar 4's build-identity
   intent. This is a validation rule this schema's data makes checkable (`len(vow_binding_log) >
   0` is a trivial predicate); the actual enforcement point lives in
   `creature-jobs-evolution-system` when it is authored.
2. **A Forge merge input includes a Vow-bound item.** **MVP-safe default ruling: blocked.**
   `the-forge-system`'s merge-input validation should reject any candidate with a non-empty
   `vow_binding_log`, mirroring Edge Case 1. **If `the-forge-system` later chooses to allow this**
   (flagged as an open item at the top of this document, to be confirmed when that GDD is
   authored), the schema already supports the correct, safe fallback behavior without a schema
   change: the merge output instance starts with an **empty** `vow_binding_log` (Vows do not
   carry forward across a merge — a merge produces a functionally new object, and inheriting a
   Vow automatically would let a player launder away an inconvenient restriction by merging it
   away, or duplicate a Vow's benefit across a merge chain). The consumed input's own
   `vow_binding_log` is preserved forever in its now-`archived` record, so the Vow's history is
   never lost — it is simply no longer active on any equippable item.
3. **An enchantment's trigger condition can never fire given the item's other properties** (e.g.
   an `on_critical_hit` trigger authored onto a definition that structurally cannot land critical
   hits, or a `state_condition: health_below_percent` enchantment authored onto a
   `creature_core`, which is never equipped and thus never has player-health context).
   **Ruling: fail inert, not fail loud.** This schema does not runtime-validate that a
   `trigger_type`/`effect_type`/`condition_type` combination is sensible for a given `base_type`
   — that validation is a content-authoring-time lint against the future content catalog, not a
   schema-level runtime constraint (the fields are open enums specifically so this document isn't
   required to enumerate every valid combination now). If an unfireable enchantment somehow ships
   anyway (a content bug), the runtime behavior is well-defined: the enchantment's trigger simply
   never fires — it contributes nothing, silently. It must never crash, block the item from
   otherwise functioning, or produce an error state visible to the player. Live-game content bugs
   must degrade to inert, never to a hard failure.
4. **`modifiers` or `enchantments` array length exceeds the rarity-derived cap** (Formulas 1–2),
   e.g. from a save-migration bug or a future content change that lowers `enchantment_floor`.
   **Ruling**: on load, truncate the array to the current cap (keep the first N entries in
   existing order) and log a warning in development/debug builds. Never crash on load; never
   silently grant the player extra effective slots by leaving the overflow in place.
5. **`merged_from` references an `item_instance_id` that fails to resolve** (e.g. a save-file
   corruption or a data-migration gap that dropped an archived record). **Ruling**: lineage
   display for that slot gracefully degrades to an "Unknown" placeholder in any lineage-tooltip
   UI. This must never block the merged item itself from being fully functional — a broken
   lineage pointer is a cosmetic/historical data-quality issue, never a gameplay-blocking one.
6. **An item instance somehow has `ability_focus = true` on a `base_type` other than
   `weapon`/`armor`/`charm`, or a non-empty `vow_binding_log` on `ability_focus = false`**
   (an integration bug in a system that writes to this schema, e.g. `resonance-weaving-system`
   attempting to bind a Vow to a `creature_core`). **Ruling**: this is an invariant violation
   (Invariants 2–3, Section 3.11), not a case this schema's runtime needs to gracefully handle —
   enforcement belongs to the write path in the system attempting the write
   (`resonance-weaving-system`), which must reject the operation before it ever reaches this
   schema's data. This document defines the invariant and its Acceptance Criteria (Section 8);
   it does not itself execute validation code.
7. **A stackable material instance would need to become unique** (e.g. a hypothetical future
   feature lets a player enchant a single unit out of a stack of 5 raw materials). Under the
   current schema this cannot happen for `enchantment_material`/`creature_core`, because
   Invariant 1 already forbids `modifiers`/`enchantments` on those `base_type`s — so the
   trigger condition for this edge case cannot currently occur. Flagged here only so a future
   revision that relaxes Invariant 1 knows to re-examine 3.10's uniqueness rule at the same time.

## 6. Dependencies

**Depends on**: none. This is a Foundation-tier document per `design/gdd/systems-index.md`, and
this schema requires no other GDD to exist first. Every forward reference to a not-yet-authored
system (the Item/Enchantment Definition content catalogs, `resonance-weaving-system`'s Vow
catalog, `combat-encounter-system`'s stat catalog, `creature-data-schema`'s species-key catalog)
is modeled as a **loosely-typed string key**, not an enforced foreign key or an imported type —
this is what lets item-data-schema stay genuinely independent while still carrying the data those
future systems will need once they exist. This loose-coupling pattern is itself a design decision
worth naming: a Foundation-tier schema that hard-depended on any of its five downstream consumers
would create a circular dependency the systems index explicitly confirms does not exist.

**Depended on by** (bidirectional — each system below must reference this schema's field names
directly, not redefine its own item shape, when it is authored):

- `resonance-weaving-system` — reads/writes `ability_focus` and `vow_binding_log` (Section 3.8);
  Vows bind to items via this schema's append-only log.
- `the-forge-system` — reads/writes `merged_from` and `status` (Section 3.9) for merges; produces
  `enchantment_material` instances for dismantle output; MVP scope is merge-only (Section 3.9's
  merge path), with hybrid/enchant-creation deferred to Vertical Slice per the systems index —
  this schema already supports the full `enchantments` array shape needed for that expansion
  without a rework (see MVP Scope Note below).
- `loot-drop-system` — creates new Item Instances on drop; reads `rarity` for drop-weight tables
  and Formulas 1–3 to know how many modifier/enchantment slots to roll and how to scale values.
- `creature-jobs-evolution-system` — consumes `creature_core`/`enchantment_material` instances as
  evolution feed material; must implement Edge Case 1's `vow_binding_log` exclusion filter.
- `memory-dust-prestige-system` (Full Vision tier) — depends transitively via `the-forge-system`
  and `creature-jobs-evolution-system`; when authored, it must state explicitly which Item
  Instance fields (if any) survive a prestige reset versus are wiped, since this schema itself
  takes no position on prestige persistence.
- `combat-encounter-system` — reads equipped Item Instances' `modifiers` array (its own §3.1 stat
  catalog resolution) and a weapon's `source` field (its own §4 Formula 3 Step 1) every encounter;
  confirms this schema's own A8 stat-catalog open item (that document's own §6).
- `forge-ui` — renders every field an item tile or confirmation panel needs (`base_type`, `rarity`,
  `source`, `modifiers`, `enchantments`, `ability_focus`, `vow_binding_log`, `merged_from`,
  `quantity`/`stackable`, `status`) by exact field name, never redefined (that document's own §6).
- `creature-roster-ui` — reads the `charm` and `creature_core` `base_type`s, `vow_binding_log`, and
  `source`/`modifiers`/`enchantments` for equip-gate evaluation and Charm Picker display (that
  document's own §6).
- `loot-filter-ui` — reads every filterable field (rarity/source/`base_type` enums) and the
  `max_stack_size`/quantity concept its `quantity_gte` filter condition operates on (that document's
  own §6).
- `hunter-progression-system` — reads the `Modifier.stat` string-key convention its own 9 Hunter
  stat keys match exactly, and relies on this schema's base-vs-gear division of responsibility (that
  document's own §6).

**Soft/informal forward references** (not blocking, not formal schema dependencies — see the Open
Items table at the top of this document): `combat-encounter-system` (stat catalog),
`creature-data-schema` (species-key catalog), `resonance-weaving-system` (Vow-ID catalog), and a
future content-authoring pass (Item Definition and Enchantment Definition catalogs).

**MVP Scope Note**: per `systems-index.md`, MVP's Forge is merge-only (no hybrids, no
enchantment-creation via Forge yet), and Resonance Weaving ships with a curated, non-combinatorial
ability list rather than full Source × Form × Vow combinatorics. This schema does **not** reduce
its scope to match — `ability_focus`, `vow_binding_log`, and the full `enchantments` structure
(Section 3.7) are all present and load-bearing starting at MVP, because `resonance-weaving-system`
needs Vow-binding to function at all (even against a curated list) and Vertical Slice needs the
full enchantment shape without a schema rework. The only thing genuinely deferred past MVP is
*content volume* (how many enchantment/Vow definitions exist) and *the Forge UI path that creates
enchantments* — not this schema's shape.

## 7. Tuning Knobs

| Knob | Default | Safe Range | Affects | Notes |
|---|---|---|---|---|
| `base_modifier_slots` (Formula 1) | 1 | 0–2 | How many modifier slots a Common item gets | 0 would make Common items unable to carry any stat bonus at all — valid but aggressive; untested. |
| `slots_per_tier` (Formula 1) | 1 | 1–2 | Modifier slot growth rate across rarity tiers | At 2, Legendary reaches 9 modifier slots (1 + 4×2) — high enough to risk itemization feeling like a stat-stacking game again, in tension with Pillar 4. Recommend staying at 1 unless playtesting shows rarity feels too flat. |
| `enchantment_floor` (Formula 2) | 1 | **hard floor: 1** (do not set to 0) | Minimum enchantment slots at Common | This is not a free tuning parameter — setting it below 1 directly violates the Anti-Pillar ("every rarity tier must include behavior-changing options"). Listed here as a knob only in the sense that raising it above 1 (e.g. to 2) is a legitimate power-curve decision; lowering it is not. |
| `tier_divisor` (Formula 2) | 2 | 1–3 | How fast enchantment slots grow with rarity | At 1, every tier adds a slot (Legendary reaches 5) — makes enchantments scale as fast as modifiers, diluting the "enchantments are rarer/more precious" read the current default creates. At 3, Legendary only reaches 2 — flatter, safer, less distinct top-end. |
| `growth_rate` (Formula 3) | 0.25 | 0.15–0.40 | How much rarity multiplies a rolled stat's value | Below 0.15, rarity risks feeling cosmetic (Legendary barely outperforms Common on raw stats, undermining the visual rarity ramp). Above 0.40, low-rarity gear becomes obsolete the instant a higher tier drops, risking a degenerate "only Legendary matters" economy — a direct tension with the idle-loop's need for a broad, continuously-relevant loot pool. |
| `max_stack_size` | 999 | 99–9999 | Ceiling on `quantity` for `stackable` instances (Section 3.10) | Owned by this schema since materials are this schema's domain; consuming UI (`loot-filter-ui`) and systems (`the-forge-system`) read this value rather than each defining their own cap. |
| Rarity tier count | 5 (**locked**) | not tunable | — | Locked by art-bible §4.3's ring-ornament grammar; changing this requires an art-bible revision first, not just a data change here. |
| `merged_from` cardinality | exactly 3 (**locked**) | not tunable at MVP | — | Locked by game-concept.md's explicit "three compatible items." Flag for future consideration: if `the-forge-system` ever wants variable-arity merges (2-way or 4-way), `merged_from` would need to become a variable-length array rather than a fixed 3-slot one — a deliberate, controlled schema-rework point, not an accidental one, should that ever come up. |

## 8. Acceptance Criteria

| # | Criterion | Verification Method |
|---|---|---|
| AC1 | `base_type` is always exactly one of `weapon`, `armor`, `charm`, `enchantment_material`, `creature_core` — no other value is ever valid. | Unit test: constructing an instance with any other value is rejected. |
| AC2 | `rarity` is always exactly one of the 5 locked values, and each value's ring-ornament mapping (Section 3.4) matches art-bible §4.3's table 1:1. | Unit test diffing this schema's rarity-to-ornament-tier lookup against a copy of art-bible §4.3's table. |
| AC3 | `source` is always `null` or exactly one of the 6 Source values, and the 6 non-null values are identical (same names, same count) to `creature-data-schema`'s Source enum. `creature_core` instances never have `source = null`. | Unit test enumerating both schemas' Source lists and asserting zero divergence; a second test asserting `creature_core` construction with `source = null` is rejected. |
| AC4 | `len(modifiers) <= modifier_slot_count(rarity)` for every instance, at every rarity tier. | Unit test constructing one instance per rarity tier at the cap and one attempt one-over the cap; the over-cap attempt is rejected. |
| AC5 | `len(enchantments) <= enchantment_slot_count(rarity)`, and `enchantment_slot_count(common) >= 1`. | Unit test as AC4, plus an explicit assertion that the Common-tier result is never 0. |
| AC6 | An instance with `ability_focus = false` always has an empty `vow_binding_log`. | Unit test: attempting to append a `VowBindingEntry` to an instance with `ability_focus = false` is rejected. |
| AC7 | `vow_binding_log` entries are never removed or mutated after creation except the single `status`/`replaced_by` flip performed when a new entry supersedes them. | Unit test: append entry A, assert its fields; append entry B; assert entry A's `binding_id`, `vow_id`, `bound_at` are byte-identical to before, only `status` (→ `superseded`) and `replaced_by` (→ B's `binding_id`) changed. |
| AC8 | `merged_from` is either exactly `null` or an array of exactly 3 `item_instance_id` strings — lengths of 1, 2, or 4+ are always invalid. | Unit test asserting rejection of every non-`null`, non-3 length. |
| AC9 | Any item instance with a non-empty `vow_binding_log` is rejected by `creature-jobs-evolution-system`'s feed-material input validator (Edge Case 1). | Integration test (authored alongside `creature-jobs-evolution-system`) using a mock feed-input validator against this schema's data. |
| AC10 | `quantity > 1` is valid only when `modifiers`, `enchantments`, and `vow_binding_log` are all empty and `merged_from` is `null`. | Unit test: construct an instance with any one of those four populated and `quantity = 2`; assert rejection. |
| AC11 | `status = archived` items never appear in a player-facing inventory/roster/loot-filter query, but resolve successfully by `item_instance_id` for a lineage-lookup call. | Integration test: archive an instance, assert it is absent from an inventory-listing query and present in a direct lineage-lookup call. |
| AC12 | `rarity_value_multiplier(rarity_tier_index)` is strictly increasing across all 5 tiers at any `growth_rate` within the safe tuning range (0.15–0.40). | Unit test computing all 5 tier values at the default and at both safe-range boundaries, asserting strict monotonic ordering in each case. |

---

## Cross-System Facts Registered

`design/registry/entities.yaml` has been updated with this document's cross-system facts,
following the same pattern already established this session by `creature-data-schema.md` and
`save-load-persistence.md`:

- **Registered** — Formulas 1–3 (`modifier_slot_count`, `enchantment_slot_count`,
  `rarity_value_multiplier`) and their 6 backing tuning constants (`base_modifier_slots`,
  `slots_per_tier`, `enchantment_floor`, `tier_divisor`, `growth_rate`, `max_stack_size`), so
  `loot-drop-system` can build its roll logic directly on these rather than re-deriving them.
- **Deliberately not registered** — the Source enum, rarity enum, and `base_type` enum. Per the
  precedent set by `creature-data-schema.md` (which locked its own `Source`/`Role` enums by
  citing art-bible §3.1/4.2 directly rather than duplicating them into the registry), enums that
  already have a canonical source-of-truth document are cited from that document, not
  re-registered — registering them a second place would create exactly the divergence risk the
  registry exists to prevent. The Source enum's canonical source is art-bible §4.2; the rarity
  enum's is art-bible §4.3. The `base_type` enum (`weapon`, `armor`, `charm`,
  `enchantment_material`, `creature_core`) has no other canonical document — it is defined here,
  in Section 3.3, and other GDDs should cite this document (not the registry) for it.
