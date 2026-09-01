# GDD: The Forge System

## Document Status

- **Status**: Complete — all 8 sections authored
- **Created**: 2026-07-14
- **Owned By**: economy-designer
- **Review Mode**: `lean` — authored autonomously this session (no user available to approve
  section-by-section; matching the precedent already set this session by `item-data-schema.md`).
  Every ambiguity the source material left open has been resolved with an explicit, flagged
  design call rather than a placeholder. Validate at `/design-review` and `/gate-check` before
  Production.
- **Priority**: MVP, Core tier — 7th in the recommended design order
  (`design/gdd/systems-index.md`), immediately after `resonance-weaving-system`. Per the systems
  index, this is one of the systems whose **MVP scope is intentionally reduced**: merge-only at
  MVP, with hybrid combinations and Forge-created enchantments deferred to Vertical Slice — both
  are fully designed in this document so no rework is needed later, but neither ships at MVP.

## Source Material Read

`design/gdd/item-data-schema.md` (full — this document's primary dependency; every field name,
formula, and invariant cited below is quoted from there, not re-derived), `design/gdd/game-concept.md`
(full), `design/art/art-bible.md` (full — §2's "The Forge" mood block and §5.2's Vow &
Enchantment Silhouette Standard are the most load-bearing; §7.6's Ornamentation Tier by Screen
table confirms Forge Vow-binding's ceremony ranking), `design/gdd/systems-index.md` (this
system's entry, its dependents, and its scope-reduction note), `design/registry/entities.yaml`
(checked for an existing currency name — none found anywhere in the project; a new currency is
proposed below and flagged for registration rather than assumed).

## Open Item Resolved From item-data-schema.md

`item-data-schema.md`'s own "Open Items / Decisions Carried Forward" table flagged exactly one
question for this document to confirm. It is resolved here, not deferred further:

| Item | item-data-schema's Provisional Default | This Document's Ruling |
|---|---|---|
| Can a Vow-bound item be a Forge merge input? | No — blocked (its Edge Case 2) | **Confirmed: no.** Extended in this document to all four Forge operations (merge, sell, dismantle, feed) — see §3.6 and Edge Case 1. |

## New Open Items This Document Carries Forward

| Item | This Document's Default | Owning System | Confirm At |
|---|---|---|---|
| Currency name "Gleam" | Proposed; no currency has been named anywhere else in the project as of this writing | economy-designer / cross-project | Registry confirmation (flagged at the end of this authoring session) |
| Exact hybrid recipe catalog and enchantment-application material costs | Schema/shape specified here (§3.3–3.4); no specific recipes or costs authored | the-forge-system (this doc), at its Vertical Slice expansion pass | VS design pass |
| Whether an equipped, Vow-bound item may still receive an *additional* enchantment at the Forge, and whether enchantment application is one-directional (fill-only) like Vow binding | Leaning fill-only for tonal consistency; not locked | the-forge-system, VS expansion | VS design pass |

---

## 1. Overview

The Forge is IDLExIDLE's central item-transformation and disposal hub — the single place
where the constant stream of loot produced by combat gets turned into deliberate, permanent
decisions. At MVP, the Forge performs exactly one creation operation (merging three compatible
items into one stronger item) and three disposal operations (selling, dismantling, and feeding
unwanted loot), plus the action of binding a Vow onto an ability-focus item; hybrid cross-family
combinations and Forge-created enchantments are fully designed in this document but deliberately
deferred to Vertical Slice. Mechanically, the Forge is this game's primary economic sink: every
operation it performs either destroys more value than it creates or converts one resource type
into a different, non-reconvertible one, which is what keeps loot volume from compounding into
runaway currency, material, or item-count inflation across an idle game's very long play
sessions. Thematically, per the art bible's mood direction, it is the one place in the game where
the pace deliberately drops to zero — no timers, no urgency — so that a merge, a dismantle, or
above all a Vow-binding reads as a considered commitment rather than another menu transaction.

---

## 2. Player Fantasy

Every other screen in IDLExIDLE moves at combat's pace or automation's calm hum; the Forge
is the one place that stops entirely. The art bible's mood direction for this room is explicit: a
single warm, localized light on the crafting surface, the periphery falling into a cozy, unlit
dark, no timers, no urgency cues — "time feels suspended." The player fantasy this system exists
to deliver is the feeling of being a tinkerer alone with material and consequence, not a hunter
under threat. That's a deliberate tonal contrast with everything else in the game, and it's
load-bearing: every operation available here — melting three items down into one, stripping a
weapon for parts, feeding a trophy to a creature you're shaping, or searing an oath onto a
charm — is irreversible, and the room's stillness is what gives the player room to feel that
weight before they commit to it, instead of clicking through it at combat speed.

Three items entering a merge and becoming one is not a menu operation dressed up with a progress
bar; it's meant to feel like watching three separate things actually become a fourth, different
thing, with a roll of the elemental dice deciding which of the three inputs' "nature" survives
into the result (§3.2, §4.5) — the player should feel present for a real transformation, not
confirming a batch process. Dismantling should feel like a deliberate act of salvage, not a trash
button. And feeding a piece of gear to a creature should feel like a small, sincere offering with
visible downstream consequence (a shift in what that creature becomes), not "sell to vendor #2."

Vow-binding is the ceremonial peak of the room, and the art bible is explicit that it is the
second-highest-ceremony screen in the entire game, behind only the Mastery Transition summary
(art-bible §7.6): a glyph mark sears into the item's silhouette in real time as the player
commits, and the item's material cools from molten orange to a fixed inlaid color — "the
trade-off is forged, not logged in a stat line" (art-bible §2). The player fantasy here is
specifically the fantasy of a *permanent, chosen* restriction: the moment is built to make a Vow
feel like something the Hunter did to themselves on purpose, in exchange for power, not a
checkbox toggled in an options menu. And because a new Vow binds over an old one rather than
replacing it cleanly — the old scar stays, pale, underneath the new one (art-bible §5.2) — every
subsequent visit to the Forge to rebind is also a small, quiet reminder of every trade-off that
came before it. The Forge is where IDLExIDLE's build-identity pillar (Pillar 4 — "Builds
Are Trade-offs, Not Stat Stacks") stops being an abstract design principle and becomes a room the
player physically stands in to make the trade.

---

## 3. Detailed Rules

### 3.1 Scope Summary: MVP vs. Vertical-Slice-Deferred

| Forge Feature | Scope | Section |
|---|---|---|
| Merge (3 compatible items → 1 stronger item) | **MVP** | §3.2 |
| Hybrid combinations (curated cross-family recipes) | **Vertical Slice — deferred** | §3.3 |
| Enchantment application at the Forge | **Vertical Slice — deferred** | §3.4 |
| Sell | **MVP** | §3.5.1 |
| Dismantle | **MVP** | §3.5.2 |
| Feed to creature | **MVP** | §3.5.3 |
| Vow binding (the action) | **MVP** | §3.6 |

Per `systems-index.md`: "MVP's Forge is merge-only (no hybrids, no enchantment-creation via Forge
yet)." Per `item-data-schema.md`'s MVP Scope Note: "the only thing genuinely deferred past MVP is
*content volume*... and *the Forge UI path that creates enchantments* — not this schema's shape."
This document follows the same split: §3.3 and §3.4 are fully designed (schema, rules,
discovery/application mechanics) so no rework is needed when Vertical Slice arrives, but neither
ships at MVP.

### 3.2 The Merge (MVP)

#### 3.2.1 Compatibility

Two properties, and only two, must match across all three inputs for a merge to be legal. This is
a deliberate design call: **"compatible" means same `base_type` and same `rarity` — nothing
else.**

- All three inputs share the same `base_type`, and that `base_type` must be one of `weapon`,
  `armor`, or `charm`. `enchantment_material` and `creature_core` are never merge-eligible —
  they're fungible/collection items respectively, not "gear that gets stronger," and merging them
  wouldn't produce anything equippable.
- All three inputs share the same `rarity` (equivalently, the same `rarity_tier_index`). This is
  what makes merge the classic "three-of-a-tier become one of the next tier" pattern, and it's
  what keeps the output formula (§4.2–§4.4) predictable enough to reason about even though merge
  is not literally deterministic end-to-end (§4.5).
- All three inputs are `status = active` and are three **distinct** `item_instance_id`s — the
  same instance cannot fill two of the three slots (Edge Case 7).
- None of the three inputs has a non-empty `vow_binding_log` (Edge Case 1) — this resolves
  item-data-schema's own open item on this exact question: **Vow-bound items can never be a merge
  input.**
- **None of the three inputs has a non-null `equipped_to_creature_id`** (item-data-schema field 16,
  Invariant 9) — an item currently equipped to a creature can never be a merge input. See §3.6a.

**Explicitly not required**: the three inputs do **not** need to share the same `source`
(element) or the same `definition_id`. This is deliberate. Requiring identical `definition_id`
(literally three copies of the exact same authored item) would be simpler, but a Definition's
`source` is either fixed per-Definition or rolled once per instance at creation (item-data-schema
§3.2, field `fixed_source`), so three copies of the *same* Definition would nearly always share
one element by construction anyway — that would make "three items of different elements get
merged together" impossible to happen naturally, when it's actually a common, expected case this
system needs a real rule for (§4.5), not a hypothetical edge case.

#### 3.2.2 Output Resolution — The Transaction

A successful merge is a single atomic transaction:

1. All three input instances flip `status: active → archived` (item-data-schema §3.9's lifecycle
   rule — never hard-deleted, forever resolvable for lineage).
2. Exactly one new Item Instance is created, with:
   - `source_event = forge_merge`.
   - `merged_from` = the three (now-archived) input `item_instance_id`s, in the order they were
     selected (item-data-schema field 13; always exactly 3 entries, Invariant 5).
   - `rarity` per **Formula 2** (§4.2).
   - `source` per **Formula 5**, the Source Inheritance Roll (§4.5).
   - `modifiers` and `enchantments` per **Formula 4** (§4.4).
   - `definition_id`: resolves to a small, generic, forge-native Item Definition family, one per
     `(base_type, output rarity)` pair — conceptually "Merged Weapon (Epic)" rather than a
     themed, loot-table Definition like "Ember Blade." **Design call, flagged**: because inputs
     can carry three different `definition_id`s, there is no single correct "which of the three
     input Definitions does the output become" answer, and inventing a lineage-mapping content
     table (Definition A + B + C → Definition D) is exactly the kind of content-authoring-volume
     decision item-data-schema deliberately keeps out of its own scope (its §3.2). Routing merge
     output to a small, closed family of generic Definitions — authored with `fixed_source: null`
     so the Forge can set `source` explicitly per Formula 5 — sidesteps that combinatorial
     content problem while staying fully schema-legal. The actual content (icon, flavor text,
     display name per tier) for this small Definition family is a future content-authoring pass,
     matching item-data-schema's own restraint.
   - `ability_focus`: denormalized from the resolved output Definition's `ability_focus_eligible`,
     exactly as any newly-created instance would be (item-data-schema §3.2/§3.3 field 11) — no
     Forge-specific special-casing.
   - `vow_binding_log = []` (empty) — a fresh merge output always starts unbound, matching
     item-data-schema Edge Case 2's fallback behavior even though that Edge Case is moot here
     (merge inputs can never be Vow-bound in the first place, per §3.2.1).
   - `quantity = 1`; `stackable` denormalized from the resolved Definition (expected `false` for
     gear).

A stackable input's entire record (its whole `quantity`) is consumed as one of the three inputs
if selected — merge operates on instance identity, not a fractional slice of a stack (Edge Case
17; in practice this rarely applies, since gear is normally non-stackable).

### 3.3 Hybrid Combinations (Vertical Slice — Deferred)

Per game-concept.md: "selected cross-family combinations can create predictable hybrid
equipment" — the operative word is *predictable*. Where merge (§3.2) is a general rule applied to
any items that satisfy a compatibility test, with a genuine roll in the output (§4.5), a
**hybrid is a specific, curated recipe**: an exact, authored list of input requirements mapped to
one deterministic output. This section specifies the recipe's shape and discovery flow now so the
eventual VS content-authoring pass has a stable schema to build on, matching item-data-schema's
own "schema now, content volume later" pattern. **No specific recipes are authored by this
document** — that is deliberately out of scope, the same content-authoring boundary
item-data-schema draws around its own Definition catalog.

**Hybrid Recipe schema**:

| Field | Type | Description |
|---|---|---|
| `recipe_id` | string | Unique content key. |
| `required_inputs` | array of Input Slot Spec, length 2–3 (exact arity a VS tuning decision) | Each slot spec constrains one input item: `{base_type, source (or "any"), rarity_min}`. Unlike merge, a hybrid input slot may require a **specific Source**, which is what makes it "cross-family": e.g. a two-slot recipe requiring `{base_type: weapon, source: body}` + `{base_type: charm, source: machine}`. |
| `output_definition_id` | string | The exact, deterministic output Item Definition — unlike merge's generic Definition family (§3.2.2), a hybrid output is a specific, themed, named item. |
| `output_rarity_rule` | enum | Default proposal: `max(input rarity tiers)` — deterministic, no roll, consistent with "predictable." The exact rule is confirmed at the VS design pass; this document only commits to it being a **deterministic** function of the inputs' rarities, never a random roll — a roll here would contradict "predictable" outright. |

**Compatibility and consumption**: identical in spirit to merge — all listed input slots must be
filled by distinct, `active`, non-Vow-bound instances matching that slot's spec; a successful
hybrid archives all consumed inputs and creates one new instance with `source_event = forge_merge`
(no new `source_event` value is needed — a hybrid is mechanically a merge variant with curated,
non-random inputs/outputs, reusing item-data-schema's existing enum rather than requesting a
schema change) and `merged_from` set to the consumed inputs' IDs, following the same lineage rule
as §3.2.2. If a future recipe ever needs more than 3 inputs, item-data-schema's `merged_from`
cardinality lock ("exactly 3, not tunable at MVP" — its §7) would need revisiting at that time;
this document does not propose that change now.

**Discovery**: recipes are not handed to the player as a fully legible list from the start. The
recommended approach (VS design pass to confirm): a **partial-hint recipe book** — an entry
becomes partially visible (a glyph-tier silhouette hint, not text, matching the game's
glyph-over-text visual language) once the player has acquired at least one of its required input
types, and fully legible once discovered, either by successfully assembling a valid combination
at the Forge for the first time or via an explicit unlock (quest/region-mastery reward, TBD). Once
discovered, a recipe is **permanently known and repeatable** — unlike a merge, which is a general
rule applied on demand, a discovered hybrid recipe is closer to an unlocked crafting formula:
knowing it doesn't consume anything, only executing it does. This serves the Discovery aesthetic
(game-concept.md's MDA priority 3 explicitly names "hybrid forge recipes" as a Discovery delivery
mechanism).

### 3.4 Enchantments (Vertical Slice — Deferred)

Enchantments already exist at MVP as loot-drop output — item-data-schema's `enchantments` field
and its Formula 2 are load-bearing from MVP onward (see that document's MVP Scope Note). What's
deferred to Vertical Slice is specifically **the Forge's ability to deliberately apply one to an
item**, as opposed to only receiving one as a random loot roll. Per Pillar 4, enchantments are the
*prioritized* half of itemization ("If we're debating adding a pure +X% stat modifier vs. a
build-altering effect... this pillar says we choose the behavior change") — a deliberate,
player-chosen application path is the natural complement to the random loot-roll path, and this
document specifies its shape now for the same reason §3.3 does.

**Application rule (VS design intent, schema-complete now)**:

1. The player selects one target item with at least one open enchantment slot
   (`len(enchantments) < enchantment_slot_count(rarity)`, item-data-schema Formula 2 / Invariant
   7), that is not `archived`. Unlike merge/dismantle/feed, an equipped, Vow-bound item is **not**
   automatically blocked from receiving an *additional* enchantment in this proposal — only the
   Vow scar's own permanence is protected elsewhere, not the item's ability to keep evolving
   otherwise — flagged explicitly as a VS-confirm decision, not locked here.
2. The player selects one content-authored **Enchantment Definition** (a specific
   `trigger`/`condition`/`effect` combination, item-data-schema §3.7's schema, reused unmodified)
   and pays its associated cost, expected to be denominated primarily in `enchantment_material` —
   this is the forward-looking payoff for the Dismantle disposal path (§3.5.2): dismantle
   materials become the direct fuel for this VS feature, not just a generic sell-adjacent
   resource.
3. The Forge appends one new `Enchantment` object to the target's `enchantments` array
   (item-data-schema §3.7 fields) — deterministic, player-chosen, never a roll, matching the
   "predictable" framing that already governs hybrids.
4. **Open question flagged for the VS pass, not resolved here**: whether an existing enchantment
   can ever be removed or replaced at the Forge, or whether — mirroring Vow permanence —
   enchantment application is also one-directional (fill empty slots only, never overwrite a
   filled one). This document leans toward one-directional, for tonal consistency with the room's
   "irreversible commitment" fantasy (§2), but does not lock it.

### 3.5 The Three Disposal Paths (MVP)

Per game-concept.md: "Unwanted loot can be sold, dismantled into materials, or fed to creatures to
influence their evolution." All three share one valuation core (§4.1's Item Potency Score) so
their outputs stay comparable, but each is restricted to a different subset of eligible
`base_type`s and converts into a different, non-interchangeable resource — this is what keeps any
one path from strictly dominating the other two (§3.5.4).

#### 3.5.1 Sell

**Eligible `base_type`**: `weapon`, `armor`, `charm`, `enchantment_material`. `creature_core` is
not sellable at the Forge — a captured creature's core is that system's asset, not liquidatable
loot; see §6 Dependencies.

Output: **Gleam** (this document's proposed currency name — flagged for registry at the end of
this authoring session; no currency has been named anywhere else in the project). Sell value is
Formula 6, §4.6.

Sell is the only path that supports selling a **sub-quantity** of a stackable instance (defaulting
to the full stack if unspecified) — the one disposal path expected to routinely operate on large
material stacks rather than single gear pieces.

#### 3.5.2 Dismantle

**Eligible `base_type`**: `weapon`, `armor`, `charm` only. `enchantment_material` cannot be
dismantled — a material is already the terminal output of a dismantle, and dismantling a material
into more material would be either a no-op or a value-generating loop; excluding it closes that
possibility structurally rather than relying only on a formula to make it unprofitable (§3.7).
`creature_core` is likewise excluded — its disposal, if any, belongs to
`creature-jobs-evolution-system`, not the Forge.

Output: `material_yield` units of an `enchantment_material` instance, keyed to the dismantled
item's `base_type` and `source` (e.g. a Body-aligned weapon yields a Body-keyed material) — the
exact material catalog is future content authoring, matching item-data-schema §3.2's own scope
boundary. Yield is Formula 7, §4.7.

#### 3.5.0 Universal Input Gate (applies to ALL Forge operations)

> **Added 2026-07-14 — cross-GDD review fix.** `/review-all-gdds` (Scenario 3) found that
> `item-data-schema`'s Invariant 9 (`equipped_to_creature_id`, field 16) had been written into the
> schema but **never wired into any Forge validator**. As specified before this fix, a charm equipped
> to a creature and actively working a region was fully sellable, dismantlable, mergeable, and
> feedable-to-a-different-creature — directly contradicting the schema's own stated invariant. This
> section closes that gap at the gate rather than per-operation, so no future operation can reopen it.

**Every Forge operation — merge, sell, dismantle, and feed — rejects any candidate item where
EITHER of the following holds:**

| Rejection predicate | Source | Rationale |
|---|---|---|
| `len(vow_binding_log) > 0` | item-data-schema Edge Case 1 | A Vow is a permanent, irreversible commitment; consuming its host item would erase a mark the design says is never erased (art-bible §5.2). |
| **`equipped_to_creature_id != null`** | **item-data-schema Invariant 9 (field 16)** | **The item is currently equipped to a creature (`creature-jobs-evolution-system`'s `equipped_charm_id` slot). It is not in the player's free inventory and is not the Forge's to consume.** |

**The item must be unequipped first.** Unequipping is a `creature-roster-ui` action, not a Forge
action — the Forge never silently unequips an item to consume it. Per the "impossible to attempt, not
merely rejected" principle (`forge-ui` AC), equipped items are **filtered out of every Forge picker**,
not merely disabled or rejected on submit.

**Bidirectional integrity**: `item-data-schema.equipped_to_creature_id` and
`creature-jobs-evolution-system.CreatureInstance.equipped_charm_id` are a reciprocal pair and must
never disagree. Exactly one creature may reference a given charm at a time.

#### 3.5.3 Feed to Creature

**Eligible `base_type`**: `weapon`, `armor`, `charm`, `enchantment_material`, `creature_core` —
the broadest of the three paths. This matches item-data-schema's own expectation: its Dependencies
section lists `creature_core`/`enchantment_material` as anticipated feed material, and its Edge
Case 1 explicitly gates *any* `base_type` via the `vow_binding_log` check, which only has teeth
for `weapon`/`armor`/`charm` — implying those are eligible too, or the check would be vacuous. The
Forge does not decide **what** a fed item does to a creature's evolution — that ruleset belongs
entirely to `creature-jobs-evolution-system` (not yet authored). This document defines only the
**feed action** and the **data it emits**.

**The Feed action**: the player selects one `active`, non-Vow-bound item and one target creature
from their roster, and confirms — a single atomic step, no queueing, no batching, consistent with
the room's "no timers" mood (§2). On confirmation:

1. The fed item's `status` flips `active → archived` — the same archival lifecycle pattern
   item-data-schema §3.9 already establishes for merge and dismantle, extended here to a third
   consuming action. No new Item Instance is created, and no new `source_event` value is required
   (feeding doesn't create anything; it only consumes).
2. The Forge emits a **Creature Feed Event** to `creature-jobs-evolution-system`, containing:

| Field | Source | Purpose |
|---|---|---|
| `fed_item_instance_id` | item-data-schema field 1 | Audit-trail traceability, mirroring `merged_from`'s role for merge |
| `base_type` | item-data-schema field 3 | Lets evolution rules key off item category |
| `rarity`, `rarity_tier_index` | item-data-schema field 4 | Magnitude signal |
| `source` | item-data-schema field 5 | Elemental theming signal — expected to be the primary lever evolution branches key off |
| `modifiers` (full array) | item-data-schema field 9 | Qualitative stat-theme signal |
| `enchantments` (full array) | item-data-schema field 10 | Qualitative behavior-theme signal |
| `feed_contribution_value` | Formula 8, §4.8 | A single scalar potency value, for evolution rules that want a magnitude without parsing the qualitative fields |
| `fed_at` | timestamp | When |
| `target_creature_id` | player selection | Which creature this feed applies to |

**What `merged_from`-style lineage is *not* doing here**: unlike merge, Feed produces no output
instance, so there's no lineage array to populate. The `fed_item_instance_id` field on the emitted
event serves an analogous audit-trail purpose but is a one-shot event field, not a persistent Item
Instance field — a deliberate, minimal design; this document proposes no changes to
item-data-schema's shape to accommodate it (no new field, no new enum value).

**Implementation note (not a design gap)**: `creature-jobs-evolution-system` does not exist yet
(`systems-index.md` — "Not Started"). This section fully specifies what the Forge emits; how that
payload is consumed is entirely that future document's responsibility, matching the same
"define the emitter, defer the consumer" pattern item-data-schema itself already used for this
exact system (its Edge Case 1 and Dependencies section).

#### 3.5.4 Why No Path Dominates

Design-docs.md requires an explicit answer, not an assertion, whenever multiple options exist that
could collapse into one obviously-correct choice. Two properties are deliberately engineered here:

**Baseline Value Parity**: `sell_conversion_rate` and `feed_conversion_rate` default to the same
value (`15`, §7). This is not a coincidence — the *numeric* value returned by Sell and Feed is
identical for the same item, by design, so a player's choice between them is never "which one pays
more," only "do I need Gleam right now, or do I want to shape a specific creature's evolution
using this specific item's profile." Dismantle's lower numeric rate (`0.6`) is denominated in a
different, more concentrated resource (materials), not directly comparable 1:1 to the other
two — §4.7's worked table shows it returns a smaller *count* of a more valuable-per-unit resource,
not "less value" in any absolute sense.

**Eligibility differentiates the paths as much as value does**: the three paths don't even accept
the same inputs. Dismantle is gear-only (§3.5.2 — closing off any material-to-material loop
structurally). Feed is the only path that accepts `creature_core`. Sell is the only universal
option (covers gear and materials, but not cores). A player holding a duplicate `creature_core`
genuinely only has Feed available; a player who needs materials specifically for a VS hybrid
recipe genuinely only has Dismantle. The choice is real because the *resource type* each path
produces is not fungible with the others inside this document — there is no Forge operation that
converts Gleam into materials, or materials into Gleam.

**Qualitative information loss differentiates further**: Sell and Dismantle destroy an item's
specific profile (Source, modifiers, enchantments) and return only a scalar. Feed is the only path
that preserves and forwards that full profile (§3.5.3's event payload) to a system that can act on
it. A player who has a specific Nature-aligned, poison-enchanted charm they want to nudge a
specific creature toward a specific branch has a reason to Feed that exact item that Sell/Dismantle
cannot replicate at any conversion rate.

### 3.6 Vow Binding at the Forge (MVP)

Per art-bible §7.6, the Forge's Vow-binding confirmation is the **second-highest ceremony-tier
screen in the game**, behind only the Mastery Transition summary — both are the only two "High" /
"ceiling case" ornamentation tiers in the game's entire UI. This document owns the **binding
action**; `resonance-weaving-system` (being authored in parallel this session, not yet complete)
owns **what a Vow is** — its catalog, its `vow_id` semantics, and what restriction/power it
grants. The split, stated precisely:

| | Owner |
|---|---|
| The Vow catalog (which `vow_id`s exist, what each restricts/grants) | `resonance-weaving-system` |
| Which items are Vow-*eligible* (`ability_focus = true`) | item-data-schema (schema/invariant) — `resonance-weaving-system` decides usage policy within that, per its own open item, not re-litigated here |
| The binding **action**: cost, ceremony, irreversibility enforcement, the `vow_binding_log` write | **the-forge-system** (this document) |

**The action's rules**, all consistent with item-data-schema §3.8's already-locked log
mechanics — this document does not redefine that log, only specifies the transaction that writes
to it:

1. **Eligibility**: the target item must have `ability_focus = true` (item-data-schema Invariant
   2). Binding is rejected outright otherwise (Edge Case 12).
2. **Cost**: binding a Vow costs `vow_binding_cost` — a combined Gleam + `enchantment_material`
   price (§7 for defaults), paid in full or not at all; no partial payment, no partial binding
   (Edge Case 13). This is deliberately the single most expensive named transaction in this
   document, by design — it should cost meaningfully more than a single Sell/Dismantle nets, so a
   player has to have actually accumulated resources (via the other three paths) to afford the
   game's highest-ceremony action. This is also the primary intended sink for Gleam at MVP
   (§3.7) — without it, Gleam would be a pure faucet with no MVP-defined sink at all.
3. **Irreversibility**: once written, a `VowBindingEntry` can never be deleted or reverted to an
   unbound state — item-data-schema's own design call ("this schema does not model an 'unbind to
   fully unbound' operation... a one-directional lifecycle by design"), enforced by the Forge's
   binding action by construction: the action only ever *appends*.
4. **Binding over an existing Vow is explicitly allowed.** Per art-bible §5.2, "if the Vow is
   later replaced, the old scar remains... never erased." Mechanically: the Forge appends a new
   `VowBindingEntry` with `status: active`, and flips the item's previous `active` entry to
   `status: superseded` with `replaced_by` set to the new entry's `binding_id` — exactly
   item-data-schema §3.8's rule, performed here as the write-path (Edge Case 14). No entry is ever
   mutated beyond that one status flip.
5. **The ceremony itself**: per art-bible §2 and §7.3, the confirmation UI plays the item's own
   molten-to-cooled material transition directly, in real time, as the player commits — this
   document does not further specify presentation (owned by `forge-ui`), only confirms that the
   binding is a single, irreversible, atomic confirm-step with no "preview and cancel after the
   glyph has started searing" state; the point of no return is the confirm click itself, not the
   end of the animation.

### 3.7 Economic Integrity: Faucets, Sinks, and Loop Closure (MVP)

Per game-concept.md's Risks section, the Forge is explicitly named alongside the automation
ceiling as one of the two places a "degenerate optimal strategy" risk lives. This section is the
rigorous accounting the brief asks for.

**Faucets and sinks this document owns**:

| Flow | Direction | Resource | Mechanism |
|---|---|---|---|
| Loot enters the economy | Faucet (**external** — owned by `loot-drop-system`, not this document) | Item instances | Not modeled here; the Forge only ever consumes what arrives from elsewhere |
| Merge | **Sink** (net −2 item count per operation) | Items → fewer, higher-tier items | 3 inputs archived, 1 output created (§3.2) |
| Sell | **Sink** (item → currency, one-directional) | Items → Gleam | Formula 6, §4.6 |
| Dismantle | **Sink** (item → materials, one-directional) | Items → `enchantment_material` | Formula 7, §4.7 |
| Feed | **Sink** (item → creature-evolution event, one-directional; no resource returned to the player's own economy at all) | Items → a data event | §3.5.3 |
| Vow binding | **Sink** (Gleam + materials consumed, nothing returned) | Gleam + materials → a permanent log entry | §3.6 |

Every row above is a **sink** or a pass-through, and the only faucet in the whole table is
external. This is the headline finding: **the Forge, taken as a closed system, cannot generate
items, currency, or materials from nothing — every operation converts an existing resource into a
smaller or differently-shaped one, or destroys it outright.** The rest of this section proves the
two specific loop risks the brief names.

**Is the 3:1 merge ratio a genuine sink?** Yes, on two independent axes:

- **Item count**: trivially, 3 → 1 is a net loss of 2 items per operation, always.
- **Aggregate stat value**: even the *best case* for the player — three fully-socketed
  same-rarity items merging into a fully-socketed output — is value-negative in a naive "sum of
  all modifier values" framing. Three Rare items' combined modifier pool feeds into one Epic
  item's smaller number of slots (§4.4's worked example fills only 3 of 4 output slots from a
  3-input pool that could have held up to 6 values total), and each surviving value is scaled by
  only `merge_scaling_factor = 1.25`, not `3×`. Merge is not a value-creation engine; it's a
  **concentration** mechanic — its real payoff is that IDLExIDLE's equip slots are limited
  (one weapon, one set of armor, a fixed charm count), so "one strictly stronger item" is worth
  more to a player *in practice* than "three weaker items I can't all equip anyway," even though
  the raw numbers say the operation lost value. This is the correct read of "stronger version"
  from game-concept.md: stronger *per equip slot*, not stronger in aggregate.

**Can a player loop merge → dismantle → merge for infinite value?** No — and this is provably
closed by two independent mechanisms, not one:

1. **Structural closure**: Dismantle's output `base_type` is `enchantment_material` (§3.5.2), and
   Merge's input requirement is `base_type ∈ {weapon, armor, charm}` (§3.2.1) —
   `enchantment_material` is explicitly excluded from both Merge inputs *and* Dismantle inputs
   (§3.5.2). Dismantle output can never re-enter either Merge or another Dismantle. The loop the
   brief asks about literally cannot be constructed with these two operations alone, because their
   input/output types don't chain.
2. **Value closure (the "merge first, then dismantle" variant)**: even the adjacent question —
   does merging three items *before* dismantling return more materials than dismantling them
   directly? — is provably no, including in the most favorable case for the player. Compare, at
   default tuning, three fully-socketed Rare weapons (`IPS = 4.14`, §4.1):

   | Path | Calculation | Materials |
   |---|---|---|
   | Dismantle all 3 directly | `3 × max(1, round(4.14 × 0.6)) = 3 × 2` | **6 materials** |
   | Merge the 3 into 1 Epic (assume maximally favorable — output ends up fully socketed), then dismantle that 1 Epic | `IPS(Epic, maxed) ≈ 5.18` → `max(1, round(5.18 × 0.6))` | **3 materials** |

   Merging first and dismantling second yields **half** the materials of dismantling directly,
   even under the most generous assumption for the merge outcome. There is no rational reason to
   route through a merge on the way to a dismantle — the operations compose to a *worse* outcome
   than either applied alone, which is exactly the property a healthy sink-chain needs.

**Currency (Gleam) closure**: Gleam's only faucet in this document is Sell (Formula 6, §4.6); its
only sink is the Vow-binding cost (§3.6). There is no Forge operation that converts Gleam back
into items — a player cannot buy loot with Gleam at the Forge. As long as `vow_binding_cost` is
tuned such that a player cannot indefinitely afford unlimited Vow-bindings purely from routine
Sell income relative to how often new ability-focus items enter play (a tuning relationship this
document flags but does not fully resolve, since it depends on `resonance-weaving-system`'s
not-yet-defined Vow cadence), Gleam does not have an internal path to runaway accumulation
*within this document's own operations* — accumulation is bounded by how much the player chooses
to spend, not by any generative loop.

**Boundary the Forge does not control**: if `creature-jobs-evolution-system` (consuming Feed
events) or `region-mastery-automation-system` (a likely future consumer of Gleam or materials)
later introduces their *own* faucet — e.g., an evolution reward that grants items or currency back
to the player — that is a faucet outside this document's authority to bound. This document's claim
is narrower and fully verifiable: **the Forge itself, as specified here, is a closed,
non-generative system with respect to items, Gleam, and materials.** Any future system that
reintroduces a faucet connected to Forge-owned resources should reference this section's closure
proof and explicitly account for how it preserves — or deliberately relaxes — it.

---

## 4. Formulas

Eight formulas exist in this document. Formulas 2–5 govern the merge output (§3.2); Formula 1 is
a shared valuation core consumed by Formulas 6–8, which govern the three disposal paths (§3.5).
Every formula either derives from or explicitly reuses an item-data-schema formula rather than
re-deriving one — this document adds no competing definition of rarity, slot counts, or the
rarity-value curve.

### 4.1 Formula 1 — Item Potency Score (IPS)

The Forge's own local valuation formula. **This is deliberately not the same thing as the
aggregate "power score" item-data-schema explicitly declined to define** (that document's §4,
closing note: "no dependent system in the current systems index consumes a single aggregate power
number... speculative complexity with no consumer"). That was correct *then* — no consumer
existed. It exists now: the three disposal paths below all need a single, comparable valuation,
and IPS is scoped narrowly to serve exactly that need. It is not proposed as a combat power
metric, a UI sort key, or anything else outside this document's own consumption of it.

```
IPS = base_type_weight[base_type] × rarity_value_multiplier(rarity_tier_index)
      × (1 + w_mod × modifier_fill_ratio) × (1 + w_ench × enchantment_fill_ratio)

modifier_fill_ratio     = len(modifiers) / modifier_slot_count(rarity)
enchantment_fill_ratio  = len(enchantments) / enchantment_slot_count(rarity)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `base_type_weight[base_type]` | float, tuning knob per `base_type` | 0.3–2.0 | Relative economic weight by item category (§7 for the default table) |
| `rarity_value_multiplier(rarity_tier_index)` | float | 1.0–2.441 (default tuning) | Imported directly, **item-data-schema Formula 3** — not redefined here |
| `modifier_fill_ratio` | float | [0, 1] | How full the item's modifier slots are, relative to its own rarity's cap (**item-data-schema Formula 1**) |
| `enchantment_fill_ratio` | float | [0, 1] | Same, for enchantment slots (**item-data-schema Formula 2**) |
| `w_mod` | float, tuning knob | 0.1–0.5 (default 0.3) | Weights how much filled modifier slots matter to potency |
| `w_ench` | float, tuning knob | 0.3–1.0 (default 0.7) | Weights how much filled enchantment slots matter to potency. `w_ench > w_mod` by design, mirroring Pillar 4's "enchantments are the prioritized half" |

**Output range**: `[0.3, ~6.5]` across the full `base_type` / rarity / fill-ratio space at default
tuning (floor: a Common, empty `enchantment_material` at `0.3 × 1.0 × 1 × 1 = 0.3`; ceiling: a
fully-socketed Legendary weapon, worked below).

**A direct, useful consequence of item-data-schema Invariant 1** ("`modifiers`/`enchantments` are
always empty on `enchantment_material` and `creature_core`"): for those two `base_type`s, both
fill ratios are always exactly `0`, so `IPS` collapses to `base_type_weight ×
rarity_value_multiplier` — a pure rarity-scaled base value, with no modifier/enchantment term to
evaluate. No special-casing was written for this; it falls out of the shared formula
automatically.

**Per-unit vs. stacks**: IPS is a **per-unit** value. For a `stackable` instance with `quantity >
1`, total transaction value in any of Formulas 6–8 is `IPS × quantity`.

**Worked values** (weapon, `base_type_weight = 1.2`, default tuning throughout):

| Item | `rarity_tier_index` | fill ratios | IPS |
|---|---|---|---|
| Common, empty | 0 | 0 / 0 | `1.2 × 1.0 × 1.0 × 1.0 = 1.2` |
| Rare, fully socketed | 2 | 1.0 / 1.0 | `1.2 × 1.5625 × 1.3 × 1.7 ≈ 4.14` |
| Epic, fully socketed | 3 | 1.0 / 1.0 | `1.2 × 1.953125 × 1.3 × 1.7 ≈ 5.18` |
| Legendary, fully socketed | 4 | 1.0 / 1.0 | `1.2 × 2.4414 × 1.3 × 1.7 ≈ 6.47` |

### 4.2 Formula 2 — Merge Output Rarity

```
output_rarity_tier_index = min(4, input_rarity_tier_index + 1)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `input_rarity_tier_index` | int | 0–4 | The shared rarity tier of all three inputs (§3.2.1 requires them identical) |
| `output_rarity_tier_index` | int | 0–4 | The new instance's rarity tier |

**Output range**: strictly `input_rarity_tier_index + 1`, except when `input_rarity_tier_index =
4` (Legendary), where the output stays at `4` — the "Legendary Refinement" case, Edge Case 5.

**Worked example**: three Rare (`tier_index = 2`) inputs produce an Epic (`tier_index = 3`)
output.

### 4.3 Formula 3 — Merge Scaling Factor

```
merge_scaling_factor = rarity_value_multiplier(output_rarity_tier_index)
                        / rarity_value_multiplier(input_rarity_tier_index)
```

Reuses **item-data-schema Formula 3** directly — this is a **derived** value, not an independent
tuning knob (§7). Because the rarity gap between input and output is always exactly 0 or 1 tier
(Formula 2), this simplifies to exactly two cases at any `growth_rate`:

| Case | `output_tier − input_tier` | `merge_scaling_factor` | At default `growth_rate = 0.25` |
|---|---|---|---|
| Normal merge (tier-up) | 1 | `1 + growth_rate` | `1.25` |
| Legendary Refinement (capped) | 0 | `1.0` | `1.0` |

If `growth_rate` is ever retuned within item-data-schema's safe range (0.15–0.40),
`merge_scaling_factor` moves with it automatically (e.g. `1.15` at the low end, `1.40` at the high
end) — this document does not maintain its own copy of that constant.

### 4.4 Formula 4 — Merge Attribute Inheritance

This is "the merge output formula" in the sense the brief means it: what the output's `modifiers`
and `enchantments` actually contain. Both follow the same two-phase **pool-and-fill** logic,
applied independently to the two arrays (they never mix — a modifier can never become an
enchantment or vice versa).

**Modifiers:**

1. Build the pooled multiset of every `{stat, value}` pair across all three inputs' `modifiers`
   arrays.
2. Group by `stat`. Any `stat` present in **two or three** of the inputs is a **shared stat**: its
   output value is `avg(values for that stat) × merge_scaling_factor`, guaranteed a slot (subject
   to step 4).
3. Any `stat` present in exactly **one** input is a **unique stat**: its output value is `value ×
   merge_scaling_factor`, and it goes into a random-fill pool for any output slots not claimed by
   shared stats.
4. The output's `modifiers` array is filled up to `modifier_slot_count(output_rarity)`
   (item-data-schema Formula 1): shared-stat entries fill first (highest post-scaling value first,
   if there are more shared stats than slots — ties broken randomly); any remaining slots are
   filled by drawing without replacement from the unique-stat pool (uniform random order); if the
   combined pool (shared + unique) is smaller than the slot count, the remaining slots are simply
   left empty. This is a valid outcome, not an error (Edge Case 10) — it's the mechanical
   incentive to feed the merge well-rolled inputs.

**Enchantments**: identical pool-and-fill logic, grouped by `enchantment_definition_id` instead of
`stat`, filled up to `enchantment_slot_count(output_rarity)` (item-data-schema Formula 2). There is
no "averaging" step for enchantments (a behavior-changing effect isn't a magnitude to average) — a
shared `enchantment_definition_id` (present on 2+ inputs) simply guarantees **one** copy carries
over; ties among shared enchantments beyond slot capacity are broken randomly rather than by
value, since enchantment "value" isn't a single comparable number at this schema's level.

| Symbol | Type | Range | Description |
|---|---|---|---|
| `modifier_slot_count(rarity)` | int | 1–5 (default tuning) | Imported directly, **item-data-schema Formula 1** |
| `enchantment_slot_count(rarity)` | int | 1–3 (default tuning) | Imported directly, **item-data-schema Formula 2** |
| `merge_scaling_factor` | float | 1.0 or `1+growth_rate` | Formula 3, §4.3 |

**Worked example** (combined with Formula 5, §4.5, below, since a merge always resolves all four
formulas together): three Rare (`tier_index = 2`) weapons are merged.

| Input | `source` | `modifiers` | `enchantments` |
|---|---|---|---|
| A | `body` | `[{attack_power: 8}, {crit_chance: 0.05}]` | `[on_kill→spawn_minion]` |
| B | `body` | `[{attack_power: 10}, {max_health: 15}]` | `[]` |
| C | `machine` | `[{attack_power: 9}]` | `[on_critical_hit→poison]` |

**Rarity (Formula 2)**: `output_rarity_tier_index = min(4, 2+1) = 3` → Epic. `modifier_slot_count
(Epic) = 4`, `enchantment_slot_count(Epic) = 2` (item-data-schema Formulas 1–2 at default tuning).

**Scaling (Formula 3)**: normal tier-up case → `merge_scaling_factor = 1.25`.

**Modifiers**: `attack_power` appears on all three inputs (shared) → `avg(8, 10, 9) × 1.25 = 9 ×
1.25 = 11.25`. `crit_chance` (A only) and `max_health` (B only) are unique, both go to the fill
pool; since only 1 slot is claimed by the shared stat and 3 remain, both unique entries are drawn:
`crit_chance = 0.05 × 1.25 = 0.0625`, `max_health = 15 × 1.25 = 18.75`. That fills 3 of 4 slots —
the 4th stays empty (pool exhausted, Edge Case 10). Output `modifiers = [{attack_power: 11.25},
{crit_chance: 0.0625}, {max_health: 18.75}]`.

**Enchantments**: `spawn_minion` (A only) and `poison` (C only) are both unique, pool size 2,
output slots 2 → both carry over, no randomness needed. Output `enchantments = [spawn_minion,
poison]`.

**Legendary Refinement variant**: if all three inputs had instead been Legendary (`tier_index =
4`), `output_rarity_tier_index` stays `4` (Formula 2's cap) and `merge_scaling_factor = 1.0`
instead of `1.25` — the pool-and-fill logic for modifiers/enchantments runs identically otherwise.
See Edge Case 5.

### 4.5 Formula 5 — Source Inheritance Roll

```
P(output_source = S) = count(inputs where source == S) / 3
   for each distinct value S present among the 3 inputs (including null)
```

A single weighted random draw over this distribution sets the output's `source`. This is the
formula that resolves the "three merged items have different elements" case explicitly (Edge Case
3): mixed-element merges are legal, and the result is a genuine roll, weighted toward whichever
element (or lack thereof) was better represented among the three inputs. If all three inputs
happen to share one element, the formula degenerates safely to that element at 100% weight — no
special-casing needed.

**Worked example** (continuing §4.4's scenario): inputs' sources were `[body, body, machine]` →
`P(body) = 2/3 ≈ 66.7%`, `P(machine) = 1/3 ≈ 33.3%`.

| Symbol | Type | Range | Description |
|---|---|---|---|
| `S` | enum \| null | any `source` value present among the 3 inputs | Candidate output source |
| `P(output_source = S)` | float | (0, 1] | Probability that candidate wins the roll |

**Output range**: always one of the (at most 3 distinct) values actually present among the
inputs — the roll can never produce a Source none of the three inputs had.

### 4.6 Formula 6 — Sell Value

```
sell_value = round(IPS × sell_conversion_rate)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `IPS` | float | 0.3–6.5 (default tuning) | Formula 1, §4.1 |
| `sell_conversion_rate` | float, tuning knob | 5–30 (default 15) | Gleam returned per IPS point |
| `sell_value` | int (rounded) | ~2–195 (default tuning bounds) | Gleam awarded |

**Worked examples**:

| Item | IPS | `sell_value` |
|---|---|---|
| Common weapon, empty | 1.2 | 18 Gleam |
| Rare weapon, maxed | 4.14 | 62 Gleam |
| Legendary weapon, maxed | 6.47 | 97 Gleam |

### 4.7 Formula 7 — Dismantle Material Yield

```
material_yield = max(1, round(IPS × dismantle_conversion_rate))
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `IPS` | float | 0.3–6.5 (default tuning) | Formula 1, §4.1 |
| `dismantle_conversion_rate` | float, tuning knob | 0.3–1.2 (default 0.6) | Materials returned per IPS point |
| `material_yield` | int (rounded, hard floor 1) | ≥1, ~1–8 in practice at default tuning | `enchantment_material` units awarded |

**Hard floor of 1**: dismantle never yields zero materials, by design — a dismantle that can
return nothing would make the option a trap relative to Sell, undermining §3.5.4's "no path
dominates" requirement in the other direction.

**Worked examples**:

| Item | IPS | `material_yield` |
|---|---|---|
| Common weapon, empty | 1.2 | 1 material |
| Rare weapon, maxed | 4.14 | 2 materials |
| Legendary weapon, maxed | 6.47 | 4 materials |

The lower numeric rate relative to Sell's `15` does not mean Dismantle "returns less" — materials
and Gleam are different units of account, and materials are the intentionally scarcer, more
concentrated resource (the sole fuel for the VS hybrid/enchant systems, §3.3–3.4, and a listed
feed material for `creature-jobs-evolution-system` per item-data-schema's Dependencies section).

### 4.8 Formula 8 — Creature-Feed Contribution Value

```
feed_contribution_value = round(IPS × feed_conversion_rate)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `IPS` | float | 0.3–6.5 (default tuning) | Formula 1, §4.1 |
| `feed_conversion_rate` | float, tuning knob | 5–30 (default 15) | Scalar potency units per IPS point |
| `feed_contribution_value` | int (rounded) | ~2–195 (default tuning bounds) | Emitted in the Creature Feed Event, §3.5.3 |

**Worked examples** (identical numbers to Formula 6 by design — see Baseline Value Parity,
§3.5.4):

| Item | IPS | `feed_contribution_value` |
|---|---|---|
| Common weapon, empty | 1.2 | 18 |
| Rare weapon, maxed | 4.14 | 62 |
| Legendary weapon, maxed | 6.47 | 97 |
| Rare `creature_core` (no modifier/enchantment term, Invariant 1) | `1.5 × 1.5625 ≈ 2.34` | 35 |

`feed_conversion_rate` defaults identically to `sell_conversion_rate` (`15`) deliberately, so the
scalar half of the Creature Feed Event carries the same "no path pays more" property as Sell — the
qualitative fields alongside it (§3.5.3's table) are what make Feed a genuinely different choice,
not the number.

---

## 5. Edge Cases

1. **A Forge merge input includes a Vow-bound item.** **Ruling: blocked.** Confirms
   item-data-schema's provisional default for this exact open item (this document's front matter,
   "Open Item Resolved"). Extended further: sell, dismantle, and feed also reject any item with a
   non-empty `vow_binding_log`, for the same rationale item-data-schema's own Edge Case 1 gives
   for feed specifically — a Vow scar is explicitly permanent and player-facing, and letting it be
   destroyed via *any* Forge disposal path would both contradict that permanence framing and
   create an accidental-loss failure mode inconsistent with Pillar 4's build-identity intent. A
   Vow-bound item can only be equipped, unequipped/stored, or have its Vow **replaced** (§3.6,
   rule 4) — never sold, dismantled, fed, or merged.
2. **Three merge inputs have different rarities.** **Ruling: blocked.** Rarity match is one of
   exactly two compatibility requirements (§3.2.1) — there is no partial-credit or
   nearest-rarity-wins behavior; the operation is simply not offered/rejected at selection time.
3. **Three merge inputs have different elements (Source).** **Ruling: allowed**, resolved via the
   Source Inheritance Roll (Formula 5, §4.5), weighted by how many of the three inputs shared each
   value. This is the expected, common case, not a degenerate one — §3.2.1 explicitly does not
   require Source to match.
4. **Three merge inputs have different `base_type`s** (e.g. two weapons and one charm).
   **Ruling: blocked.** `base_type` match is the other of the two compatibility requirements
   (§3.2.1) — mixing categories would leave the output's own `base_type` (and thus equip slot)
   undefined.
5. **The merge output would exceed the max rarity tier** (three Legendary inputs). **Ruling: not
   blocked — "Legendary Refinement" mode.** The merge is still accepted; `output_rarity_tier_index`
   stays at `4` (Formula 2's cap) and `merge_scaling_factor` becomes `1.0` instead of `1 +
   growth_rate` (Formula 3). The operation still runs the full pool-and-fill attribute-inheritance
   logic (Formula 4), so it functions as a **curation/refinement** tool at the rarity ceiling —
   letting a player consolidate the best modifiers/enchantments out of three Legendaries into one,
   without further power growth — rather than a dead end. This is a deliberate design choice: it
   keeps merge always available as a sink for excess Legendary loot (reinforcing §3.7's economic
   closure) without ever letting power inflate past the rarity system's own locked 5-tier ceiling.
6. **A Legendary item is fed to a creature.** **Ruling: allowed** (assuming it is not Vow-bound,
   per Edge Case 1). No special restriction — this is intentional, a genuine high-value sink for
   excess Legendary loot. The system does not attempt to protect the player from a valid
   strategic choice; a confirmation prompt on high-rarity feeds, if desired, is a `forge-ui`
   concern, not a data-validation one.
7. **The same `item_instance_id` is selected more than once to fill two or three of a merge's
   three input slots.** **Ruling: blocked.** Merge requires three *distinct* instance IDs
   (§3.2.1); the operation is rejected at selection/validation time, before any state mutation.
8. **One of three selected merge inputs becomes invalid between selection and confirmation**
   (e.g., consumed by a concurrent Forge action, or its `status` otherwise flips to `archived`
   before commit). **Ruling: abort atomically, no partial state change.** All three inputs are
   re-validated as `status = active` at the moment of commit, not only at selection time; if any
   has become invalid, the entire transaction fails and no input is archived and no output is
   created.
9. **Sell, dismantle, feed, or merge is attempted on an item with `status = archived`.**
   **Ruling: blocked.** Only `active` items are eligible for any Forge operation — this mirrors
   item-data-schema §3.9's rule that archived items never surface in player-facing queries.
10. **Merge inputs' combined modifier/enchantment pool is smaller than the output's slot count**
    (sparse inputs — e.g. three items with few or no rolled modifiers). **Ruling: the remaining
    output slots are left empty.** Not an error; a valid, if suboptimal, outcome (§4.4, step 4) —
    the mechanical incentive to feed the merge well-rolled items rather than the first three
    matching-rarity items on hand.
11. **Merge inputs' combined shared-stat or shared-enchantment count exceeds the output's slot
    cap** (e.g. three shared stats but only two output slots). **Ruling**: for modifiers, rank
    shared-stat entries by post-scaling value, descending, and keep the top N (ties broken
    randomly); for enchantments, since a single behavior-changing effect has no directly
    comparable "value," ties among shared enchantments beyond slot capacity are broken randomly
    rather than by any computed ranking. Discarded entries are lost from the output, but the
    archived parent instances retain their full original data permanently, so the discarded
    attribute is never truly destroyed from a lineage/audit standpoint (item-data-schema §3.9) —
    only from the new item's live stat block.
12. **A Vow-binding is attempted on an item whose `ability_focus != true`.** **Ruling: blocked.**
    Matches item-data-schema Invariant 2 directly; the Forge's write path rejects the action
    before ever touching `vow_binding_log`.
13. **A Vow-binding is attempted while the player's Gleam or material balance is below
    `vow_binding_cost`.** **Ruling: blocked, no partial payment.** The full cost is paid atomically
    or the action does not occur at all — there is no "pay what you can" partial-binding state.
14. **A new Vow is bound to an item that already has an active Vow.** **Ruling: allowed.** The
    prior `active` entry flips to `superseded` (with `replaced_by` set), and a new `active` entry
    is appended — matching art-bible §5.2's "the old scar remains" directly (§3.6, rule 4).
15. **The Feed action is initiated with no creature in the player's roster to target.**
    **Ruling**: the Feed action cannot be completed without a valid `target_creature_id`
    selection — this is a UI-level precondition (an empty roster simply cannot present a target to
    confirm against), not a Forge data-validation failure mode this document needs to special-case
    further.
16. **`merged_from` on a merge output resolves to an archived parent that is itself the product of
    an earlier merge** (multi-generation lineage — you merged an item that was already once a
    merge output). **Ruling: fully supported, no depth limit.** The data model natively supports
    arbitrarily deep lineage chains, since archived parents are never deleted (item-data-schema
    §3.9) and each one's own `merged_from` (if any) remains independently resolvable. Whether a
    lineage tooltip displays only immediate parents or recurses to show the full ancestry is a
    `forge-ui`/UX decision, not a data constraint this document imposes.
17. **A `stackable` merge input has `quantity > 1`** (an edge implementation possibility, though
    weapon/armor/charm are typically non-stackable per item-data-schema §3.2's own note).
    **Ruling**: merge consumes the **entire** `item_instance_id` record — the whole stack — as one
    of the three inputs, not a fractional quantity. Merge operates on instance identity, not
    partial stock.

---

## 6. Dependencies

**Depends on**: `item-data-schema` (Foundation tier — no other GDD). Every rule in this document
is expressed directly in terms of that schema's exact field names and formulas, never a
redefinition: `base_type`, `rarity` / `rarity_tier_index`, `source`, `status`, `modifiers`,
`enchantments`, `ability_focus`, `vow_binding_log` (and its `VowBindingEntry` fields), `merged_from`,
`source_event`, `quantity`, `stackable`, `definition_id` / `fixed_source` / `ability_focus_eligible`;
and Formulas 1–3 (`modifier_slot_count`, `enchantment_slot_count`, `rarity_value_multiplier`),
reused by name throughout §4 rather than re-derived.

**Depended on by**:

- **`hunter-progression-system`** — **Gleam, introduced by this document, is the currency that
  document's Training ranks are purchased with, and Hunter Training is now Gleam's PRIMARY sink**
  (79,551 Gleam over a full lifetime). This closes warning W1: before that system existed, Gleam's
  only sink was `vow_binding_cost` (~200 Gleam), which saturated after "realistically low tens of
  bindings" (§3.7) — the currency accumulated with almost nothing to spend it on. The two sinks now
  compete for the same finite balance, which is what gives Vow-binding a real opportunity cost.
- **`forge-ui`** — renders all four operations (merge, dismantle, sell, feed) and enforces §3.5.0's
  Universal Input Gate at the picker level (A8b), so an ineligible item is never offered as an input
  rather than being rejected after selection.
- **`loot-drop-system`** — mints the Item Instances this document consumes, and denominates its
  faucet model in this document's Gleam-equivalent item value.
- **`memory-dust-prestige-system`** (Full Vision) — reads Forge state for several of its unlocks.

**Depended on by** (bidirectional — each system below references this document's field names and
formulas directly when it is authored):

- **`loot-drop-system`** (not yet authored) — every item the Forge operates on originates from
  this system's drops. This document does not model that faucet (§3.7 explicitly treats it as
  external), but its own economic-closure proof depends on `loot-drop-system` remaining the *only*
  faucet feeding items into the Forge's inputs; when that GDD is authored, it should confirm this
  document's Formula 1 (Item Potency Score) is not duplicated by an independent drop-value system.
- **`creature-jobs-evolution-system`** (not yet authored) — consumes the Creature Feed Event this
  document's Feed action emits (§3.5.3's full field table), and is the intended interpreter of
  `feed_contribution_value` (Formula 8, §4.8). It must also independently implement
  item-data-schema Edge Case 1's `vow_binding_log` exclusion filter on its own consumption path,
  even though this document already blocks Vow-bound items from ever being fed at the source
  (Edge Case 1, §5) — defense in depth, matching item-data-schema's own stated expectation.
- **`memory-dust-prestige-system`** (Full Vision tier, not yet authored) — depends on this
  document per `systems-index.md`'s dependency map ("depends on: `region-mastery-automation-system`,
  `the-forge-system`, `creature-jobs-evolution-system`, `resonance-weaving-system`"). When
  authored, it must state explicitly which of this document's knobs (§7) or accumulated resources
  (Gleam, materials) survive a prestige reset versus are wiped; this document takes no position on
  prestige persistence, matching item-data-schema's own stance on the same question.
- **`forge-ui`** (not yet authored) — the presentation layer for every action this document
  defines (merge, sell, dismantle, feed, Vow-bind, and eventually hybrid/enchant). This document
  specifies action rules, costs, and outputs; it explicitly does not specify layout, motion, or
  ceremony-tier presentation beyond citing the art bible's own mood/ceremony direction (§2, §3.6) —
  that is `forge-ui`'s domain in full.

**Coordinates with**:

- **`resonance-weaving-system`** (being authored in parallel this session) — the Vow-binding
  ownership split (§3.6) is explicit and non-overlapping: `resonance-weaving-system` owns the Vow
  catalog and what each Vow restricts/grants; this document owns the binding action itself (cost,
  ceremony, irreversibility, the `vow_binding_log` write). Neither document should redefine the
  other's half — if `resonance-weaving-system` ships with a different `vow_id` catalog shape than
  the loosely-typed string key item-data-schema already assumes, this document's binding action
  does not need to change, since it only ever writes whatever `vow_id` is supplied to it.

**Soft/informal forward references** (not blocking): `combat-encounter-system`'s eventual stat
catalog is not depended on here — Formula 1 (IPS) was deliberately designed around modifier
*count* (fill ratio) rather than modifier *magnitude*, specifically to avoid needing a
cross-stat value function before that catalog exists (§4.1).

---

## 7. Tuning Knobs

| Knob | Default | Safe Range | Affects | MVP / VS | Notes |
|---|---|---|---|---|---|
| Merge `base_type` requirement | `{weapon, armor, charm}` | **locked** | Which items are merge-eligible | MVP-locked | Not a numeric knob — a fixed compatibility rule (§3.2.1) |
| Merge rarity-match requirement | required (exact match) | **locked** | Merge compatibility | MVP-locked | Loosening to "adjacent tiers allowed" is a real future option but is not proposed here — it would break the clean tier-up read of Formula 2 |
| `merge_output_rarity_step` | `+1` tier, capped at Legendary | **locked at MVP** | How much rarity a merge grants | MVP-locked | A probabilistic tier-up (e.g. 80% chance +1 / 20% chance +0) is flagged as a future exploration for added tension, not committed here |
| `merge_scaling_factor` | derived: `1 + growth_rate` (normal) / `1.0` (capped) | not independently tunable | Merge output attribute values | MVP-locked | **Not a free knob** — always derived from item-data-schema's registered `growth_rate` constant (safe range 0.15–0.40 there), so the two documents cannot drift apart |
| `merge_operation_fee` | `0` (free) | 0–50 Gleam | Additional currency cost per merge, beyond the 3-item consumption itself | MVP-locked to 0; VS-tunable | Kept at zero for MVP so the core loop's primary Forge action has no extra friction beyond the item cost itself, which §3.7 already proves is a sufficient sink |
| `base_type_weight` (IPS) | weapon `1.2`, armor `1.0`, charm `0.9`, `enchantment_material` `0.3`, `creature_core` `1.5` | 0.3–2.0 each | Relative economic weight per category in Formula 1 | MVP-locked defaults, retunable | Materials deliberately weighted lowest (bulk/fungible); `creature_core` second-highest (rare, roster-relevant) |
| `w_mod` (IPS) | `0.3` | 0.1–0.5 | How much filled modifier slots raise IPS | MVP-locked default, retunable | |
| `w_ench` (IPS) | `0.7` | 0.3–1.0 | How much filled enchantment slots raise IPS | MVP-locked default, retunable | Kept above `w_mod` by design (Pillar 4) |
| `sell_conversion_rate` | `15` (Gleam / IPS point) | 5–30 | Sell payout (Formula 6) | MVP-locked default, retunable | Kept equal to `feed_conversion_rate` for Baseline Value Parity (§3.5.4) — if retuned, retune both together or re-verify §3.5.4's parity claim |
| `dismantle_conversion_rate` | `0.6` (materials / IPS point) | 0.3–1.2 | Dismantle yield (Formula 7) | MVP-locked default, retunable | Hard floor of 1 material per dismantle is **not** part of this knob's range — it is a separate, non-tunable floor (§4.7) |
| `feed_conversion_rate` | `15` (scalar / IPS point) | 5–30 | Creature Feed Event's scalar value (Formula 8) | MVP-locked default, retunable | Recommend keeping within ±20% of `sell_conversion_rate` to preserve Baseline Value Parity; drifting far apart risks one disposal path dominating |
| `vow_binding_cost` | `200` Gleam + `5` `enchantment_material` units | Gleam: 100–500; materials: 2–10 | Cost of the Vow-binding action | MVP-locked default, retunable | Deliberately the single most expensive named transaction in this document — should always cost meaningfully more than one Sell/Dismantle nets |
| Sell-eligible `base_type`s | `{weapon, armor, charm, enchantment_material}` | **locked** | Sell path scope | MVP-locked | §3.5.1 |
| Dismantle-eligible `base_type`s | `{weapon, armor, charm}` | **locked** | Dismantle path scope | MVP-locked | §3.5.2 — deliberately excludes `enchantment_material` to structurally close the material-loop risk (§3.7) |
| Feed-eligible `base_type`s | `{weapon, armor, charm, enchantment_material, creature_core}` | **locked** | Feed path scope | MVP-locked | §3.5.3 |
| Vow-bound item lock (blocks merge/sell/dismantle/feed) | enforced | **locked, not tunable** | Protects permanent Vow history from accidental loss | MVP-locked | A permanence-protection rule, not a balance knob (Edge Case 1) |
| Hybrid recipe input arity | 2–3 slots | not committed | Hybrid recipe shape | **VS-deferred** | §3.3 — exact arity confirmed at the VS design pass |
| Hybrid `output_rarity_rule` | proposed `max(input tiers)` | not committed | Hybrid output rarity | **VS-deferred** | §3.3 — must remain deterministic (no roll), whatever the final rule |
| Enchantment-application material cost | not specified | not committed | VS enchant-application economy | **VS-deferred** | §3.4 — expected to consume Dismantle's material output |

---

## 8. Acceptance Criteria

| # | Criterion | Verification Method |
|---|---|---|
| AC1 | Merge accepts only exactly 3 distinct, `active` `item_instance_id`s sharing identical `base_type` ∈ {`weapon`, `armor`, `charm`} and identical `rarity`; any other combination (wrong count, duplicate ID, mismatched `base_type`, mismatched `rarity`, non-`active` status, or any Vow-bound input) is rejected before any state mutation occurs. | Unit test enumerating each violation category independently; assert rejection and zero state change in every case. |
| AC2 | A successful merge always: flips all 3 inputs' `status` to `archived`; creates exactly 1 new `active` instance with `merged_from` set to the 3 archived parents' IDs, `source_event = forge_merge`, and `rarity_tier_index = min(4, input_tier_index + 1)`. | Unit test across all 5 input rarity tiers, asserting the exact output tier and lineage array at each. |
| AC3 | Merge output's `source` is drawn according to the Source Inheritance Roll (Formula 5) with probability proportional to per-input occurrence count. | Statistical test: run a merge with sources `[body, body, machine]` over 10,000 trials; assert observed `body` frequency is `66.7% ± 2%` and `machine` is `33.3% ± 2%`. |
| AC4 | Merge output's `modifiers`/`enchantments` never exceed `modifier_slot_count(output_rarity)` / `enchantment_slot_count(output_rarity)` (item-data-schema Formulas 1–2), and shared-stat/shared-enchantment entries are prioritized over unique ones when the pool exceeds slot capacity. | Unit test constructing inputs with a pool larger than the output's slot cap; assert output length never exceeds the cap and all guaranteed shared entries are present before any unique entries. |
| AC5 | A Legendary + Legendary + Legendary merge is accepted, produces a Legendary (`tier_index = 4`) output — not rejected for "exceeding max rarity" — and applies `merge_scaling_factor = 1.0` (not `1.25`) to any carried-over modifier/enchantment values. | Unit test asserting acceptance, output tier, and the exact scaled value of a known input modifier. |
| AC6 | Sell, dismantle, feed, and merge all reject any item with a non-empty `vow_binding_log` or `status = archived`, at every entry point independently. | Unit test per operation (4 operations × 2 conditions = 8 cases) asserting rejection with no state change. |
| AC7 | Sell accepts only `base_type` ∈ {`weapon`, `armor`, `charm`, `enchantment_material`}; dismantle accepts only `base_type` ∈ {`weapon`, `armor`, `charm`}; feed accepts `base_type` ∈ {`weapon`, `armor`, `charm`, `enchantment_material`, `creature_core`} — any out-of-set `base_type` is rejected per path. | Unit test matrix: all 5 `base_type`s × 3 disposal paths, asserting accept/reject matches §3.5's eligibility table exactly. |
| AC8 | Dismantle output is always `≥ 1` unit of `enchantment_material`, regardless of input IPS, even at the lowest possible IPS value. | Unit test at the minimum-IPS input (Common, empty weapon) asserting `material_yield ≥ 1`. |
| AC9 (economic integrity — primary) | For any set of 3 same-rarity, same-`base_type` items at any rarity tier, the total material yield from directly dismantling all 3 is always ≥ the material yield obtained by merging the 3 into 1 output and then dismantling that single output, at default tuning and across the full safe tuning range of `growth_rate` (0.15–0.40) and `dismantle_conversion_rate` (0.3–1.2). | Computed test sweeping all 5 rarity tiers × the tuning ranges' boundaries and midpoints, comparing direct-dismantle-3 vs. merge-then-dismantle-1 under the most favorable (fully-socketed) assumption for the merge path; assert the direct path never yields less. |
| AC10 (economic integrity — general) | No sequence of Forge operations (merge, sell, dismantle, feed, Vow-bind, in any order or combination) applied to a fixed starting set of N items produces a net-positive increase in total item count, total Gleam-equivalent value, or total material count without new items entering from an external faucet (`loot-drop-system` drop, a `creature-jobs-evolution-system` reward, or `initial_grant`). | Property-based/fuzz test: generate random operation sequences over a fixed starting item set with no external faucet calls; assert final aggregate resource totals (item count, Gleam, materials) never exceed their starting-equivalent value under Formula 1's valuation. |
| AC11 | Binding a Vow at the Forge on an item with an existing active `VowBindingEntry` appends a new entry (`status = active`) and flips the previous entry to `superseded` with `replaced_by` set — the previous entry's `binding_id`/`vow_id`/`bound_at` remain byte-identical before and after. | Unit test: bind Vow A, capture its entry's fields; bind Vow B on the same item; assert entry A's `binding_id`/`vow_id`/`bound_at` unchanged, only `status` and `replaced_by` updated; assert entry B is the sole `active` entry (echoes item-data-schema AC7). |
| AC12 | Vow-binding is rejected outright if the target item's `ability_focus != true`, or if the acting player's Gleam or material balance is below `vow_binding_cost` at the moment of confirmation — with no partial payment or partial binding possible in either failure case. | Unit test for each rejection condition independently, asserting no currency/material deduction and no `vow_binding_log` mutation occurs on failure. |
| AC13 | A successful Feed action emits a Creature Feed Event containing at minimum: `fed_item_instance_id`, `base_type`, `rarity`, `rarity_tier_index`, `source`, `modifiers`, `enchantments`, `feed_contribution_value`, `fed_at`, and `target_creature_id`; the fed item's `status` flips to `archived`; and no new Item Instance is created as a result. | Integration test (authored alongside `creature-jobs-evolution-system`) asserting the emitted event's full field set matches §3.5.3's table and that the fed item is subsequently absent from active-inventory queries. |

---

## Cross-System Facts Proposed for Registration

This document does not write to `design/registry/entities.yaml` directly (registry updates are
being coordinated centrally this session to avoid write conflicts between parallel agents). The
following are proposed for central registration:

**New currency (item/entity)**:
- `Gleam` — proposed currency name, source `design/gdd/the-forge-system.md`, referenced by this
  document only so far. No currency has been named anywhere else in the project as of this
  writing (checked via `design/registry/entities.yaml` and a full-project grep before proposing
  this name). Expected future referencers: `forge-ui`, `region-mastery-automation-system`,
  `automation-config-ui`.

**Formulas**:
- `item_potency_score` (IPS) — source `design/gdd/the-forge-system.md` §4.1. Reused by Formulas
  6–8 within this same document; a plausible future consumer is `loot-filter-ui` if it ever needs
  a value-based sort key, though no such dependency exists yet.
- `merge_output_rarity` — §4.2.
- `merge_scaling_factor` — §4.3 (derived from item-data-schema's registered `growth_rate`; flagged
  so the registry can note the derivation relationship rather than treating it as independent).
- `merge_attribute_inheritance` (modifier/enchantment pool-and-fill logic) — §4.4.
- `source_inheritance_roll` — §4.5.
- `sell_value` — §4.6.
- `dismantle_material_yield` — §4.7.
- `creature_feed_contribution_value` — §4.8.

**Constants**:
- `base_type_weight` table (weapon `1.2`, armor `1.0`, charm `0.9`, `enchantment_material` `0.3`,
  `creature_core` `1.5`) — §7.
- `w_mod` (`0.3`), `w_ench` (`0.7`) — IPS weighting, §7.
- `sell_conversion_rate` (`15`), `dismantle_conversion_rate` (`0.6`), `feed_conversion_rate`
  (`15`) — §7.
- `vow_binding_cost` (`200` Gleam + `5` materials) — §7.
- `merge_operation_fee` (`0`, MVP-locked) — §7.

**Not proposed for registration** (deliberately): the merge `base_type`/rarity compatibility
rule, the per-path `base_type` eligibility sets, and the Vow-bound lock are all locked, structural
rules rather than tunable facts another document would need to look up numerically — they're
cited from this document's §3 by name, matching the precedent item-data-schema set for its own
locked enums.
