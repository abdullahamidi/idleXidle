# GDD: Resonance Weaving System

## Document Status

- **Status**: Complete — all 8 sections authored
- **Created**: 2026-07-14
- **Owned By**: systems-designer
- **Review Mode**: `lean` — authored autonomously this session (no user available to approve
  section-by-section; matching the precedent set this session by `item-data-schema.md` and
  `the-forge-system.md`; see `production/session-state/active.md`, "rush mode"). Every ambiguity
  the source material left open has been resolved with an explicit, flagged design call rather
  than a placeholder. Validate at `/design-review` and `/gate-check` before Production.
- **Priority**: MVP, Core tier — 6th in the recommended design order (`design/gdd/systems-index.md`),
  ahead of `the-forge-system` in that order. This document was completed **after**
  `the-forge-system.md` in this session's actual authoring sequence, not before — it deliberately
  aligns with, and does not redefine, that document's already-locked Vow-binding **action** rules
  (`vow_binding_cost`, over-binding/scar behavior, the eligibility/cost/irreversibility checks in
  its §3.6 and Edge Cases 12–14). This document owns the other half of the split: what a Vow **is**
  and what its condition **means**.

## Source Material Read

`design/gdd/item-data-schema.md` (full — this document's primary dependency; every field name
quoted below, `ability_focus`, `vow_binding_log`, `VowBindingEntry`, is reused exactly, never
redefined), `design/gdd/the-forge-system.md` (full — §3.6 "Vow Binding at the Forge" and its Edge
Cases 1, 12–14 are load-bearing; this document's Vow schema and power formula slot directly into
that already-locked binding action without contradicting it), `design/gdd/game-concept.md` (full —
Pillar 1, Pillar 4, the Core Mechanics list, the MVP Definition, and the Combat Loop Diagram
section are the most load-bearing), `design/art/art-bible.md` §5.2 (Vow & Enchantment Silhouette
Standard — the 4-scar cap and the scar/inlay permanence distinction are locked here) and §7.5
(Combat HUD Specification — confirms the diegetic belt-charm placement for ability/Vow state and
that this document does not own HUD rendering), `design/gdd/systems-index.md` (this system's entry,
its dependents, and its MVP-scope-reduction note), `design/registry/entities.yaml` (checked for
existing Vow/Source/Form facts — none found; this document is the first to touch this space).

## Open Item Resolved From item-data-schema.md

| Item | item-data-schema's Provisional Default | This Document's Ruling |
|---|---|---|
| Does `ability_focus` restrict to `charm` items only, or any equippable? | Schema allows `weapon`/`armor`/`charm`; anticipated usage is `charm` per art-bible §5.2's belt/bandolier framing | **Confirmed: `charm` only.** Every ability-focus Item Definition this document creates or references has `base_type = charm`. This is not a schema-level restriction (item-data-schema's field itself stays open to `weapon`/`armor`/`charm`) — it is this system's own content-authoring policy, chosen because art-bible §5.2 depicts Vow scars exclusively as "worn charms" on a belt/bandolier, one per equipped ability/ultimate slot. A weapon or armor piece is never an ability-focus item in this system. |
| Authoritative Vow-ID catalog for `VowBindingEntry.vow_id` | Open string key | **Resolved**: this document's §3.3 defines the `VowDefinition` schema and authors a starting catalog of 10 concrete `vow_id`s (§3.3.4). Future Vows are added as content, not code — the catalog is open-ended by design. |

## New Open Items This Document Carries Forward

| Item | This Document's Default | Owning System | Confirm At |
|---|---|---|---|
| Authoritative combat stat catalog (`attack_power`, `focus`, `vitality`, `engineering`, `guile`, `resonance_affinity`) | Proposed as the 6 Source scaling stats (§3.1); illustrative, open string keys, matching item-data-schema §3.6's own stance | `combat-encounter-system` | `/design-system combat-encounter-system` |
| Resonance resource generation/spend economy | Referenced qualitatively only (a Vow condition parameter, an ability activation cost) — not designed here | `combat-encounter-system` | `/design-system combat-encounter-system` |
| Live per-frame Vow condition evaluation + HUD signal | This document defines what each `condition_type` *means*; it does not implement live detection | `vow-condition-tracking` | `/design-system vow-condition-tracking` |
| VS Weaving material/currency cost to create a new ability-focus item | Not specified — VS-deferred content-authoring pass | `resonance-weaving-system` (this doc, VS expansion) or `the-forge-system` | VS design pass |

---

## 1. Overview

Resonance Weaving is the system that builds every combat ability in the game and the direct
mechanical expression of Pillar 4 ("Builds Are Trade-offs, Not Stat Stacks"). Every Woven Ability
the Hunter can cast is defined by exactly three ingredients — a **Source** (one of 6 elemental
origins: body, mind, nature, machine, shadow, spirit — the same locked enum used by
`creature-data-schema` and `item-data-schema`), a **Form** (one of 6 mechanical delivery shapes:
projectile, aura, transformation, summon, trap, mark — each redesigned in this document to work
inside a single-screen, no-free-roam, weak-point-targeted arena), and an optional **Vow** (a
structured restriction that trades a quantified, formula-governed amount of combat power for a
quantified, formula-governed cost — either a live condition the ability's power scales against, or
a permanent stat sacrifice). Vows bind permanently to ability-focus items (always `charm`-type,
per this document's ruling on item-data-schema's open item) via `the-forge-system`'s binding
action, which this document does not redefine; this document owns what a Vow *is*, what its
condition *means*, and the formula converting a condition's rarity into the power it grants — the
single most important balance formula in the game, because without a rigorous answer to "how much
power does a restriction buy," Pillar 4 collapses into a vibe. At MVP, Source×Form combinatorics
are curated (a small, hand-authored ability list) while Vow-binding is fully live; full
player-facing Weaving (free Source×Form composition) ships at Vertical Slice.

## 2. Player Fantasy

Every other build system in this game hands the player a number to make bigger. Resonance Weaving
hands the player a **choice with teeth**. The fantasy is not "I found a better sword" — it's "I
decided that my Ember Lance only works when I'm one mistake from death, and I built my whole combat
rhythm around courting that edge on purpose." A Vow is not a debuff the game imposed; it's a scar
the Hunter chose to carry, seared into a belt-charm at the Forge, visible on their own silhouette
for the rest of the run (art-bible §5.2) — and the game's own math backs up that the choice was
real: a Vow that's almost always active is worth almost nothing, and a Vow that's brutally hard to
satisfy is worth a spike no safe build can match. That asymmetry is the whole point. Players should
be able to look at another Hunter's belt — four scars, a mix of pale old lines and one fresh
glowing brand — and read an entire philosophy of how that person plays, the same way art-bible's
Master Visual Rule promises for every other system in the game. This is also where the game's
Discovery and Expression pillars (game-concept.md's MDA table) become concrete: theorycrafting a
Source/Form/Vow combo that lets a brutal Vow's condition hold *safely*, because of a specific Form
choice or a specific creature matchup, is the build-identity payoff Path of Exile's itemization
promises and Resonance Hunter is explicitly built to deliver without a passive skill tree — through
three deliberate choices instead of a hundred small ones.

## 3. Detailed Rules

### 3.1 Sources (6, Locked)

The exact same enum as `creature-data-schema` and `item-data-schema`'s `source` field
(art-bible §4.2) — reused verbatim, never extended with a 7th ability-only value, so cross-schema
consistency checks can diff the lists directly (matching item-data-schema §3.5's own precedent).

| Source | Damage/Effect Flavor | Primary Scaling Stat (illustrative, open key) | Mechanical Read |
|---|---|---|---|
| `body` | Physical / kinetic | `attack_power` | Raw, direct, up-front damage |
| `mind` | Psychic / control | `focus` | Debuff precision, Mark/Trap reliability |
| `nature` | Organic / toxin / growth | `vitality` | Damage-over-time, drain, sustain |
| `machine` | Kinetic-construct / energy | `engineering` | Summon/Trap magnitude, structural effects |
| `shadow` | Corruption / void | `guile` | Burst spikes, ambush/ crit-flavored payoff |
| `spirit` | Ethereal / resonant energy | `resonance_affinity` | Aura/buff magnitude, Resonance generation |

**A Source contributes exactly three things mechanically**: (1) the ability's damage/effect flavor
tag (cosmetic/glyph identity, consumed by `animation-rig-system` and combat VFX, not scored by this
document), (2) which Hunter stat the ability's base power scales against (Formula 1, §4.1) — the
authoritative stat catalog itself belongs to `combat-encounter-system`, not yet designed, so these
6 names are illustrative open keys per item-data-schema §3.6's own precedent, and (3) which row of
the **Source Effectiveness Matrix** (§3.4, Formula 4) the ability's damage multiplies against, keyed
off the target creature's own `source` field (`creature-data-schema`, identical enum).

### 3.2 Forms (6, Locked) — Redesigned for a Contained, No-Free-Roam Arena

Combat is a single-screen contained encounter with weak-point/part targeting and no spatial player
movement (game-concept.md Pillar 1, Anti-Pillars; art-bible §7.5). Every Form below is defined so
its entire mechanical identity resolves through **target selection** (the existing
cycle-and-confirm / click part-targeting flow owned by `input-targeting-system`) and **time**
(durations, intervals, trigger windows) — never through player position in space.

| Form | Delivery Mechanic | What "using space" would have meant (rejected) | What it means here instead |
|---|---|---|---|
| **Projectile** | A single, instant, high-value hit resolved against the player's currently-targeted creature part. | Aimed spatial travel, dodgeable by repositioning | A targeted burst delivered the instant it's cast — travel is a cosmetic VFX beat only, never a hit/miss spatial check |
| **Aura** | A repeating tick effect active for a fixed duration, applied either to the Hunter (self-buff) or to the currently-targeted part (sustained damage) once per `tick_interval_seconds`. | A ground-anchored radius the player must stand inside | No radius exists — "aura" is a *time-based* repeating effect anchored to the Hunter's own state, not a place |
| **Transformation** | A temporary stance change: for `duration_seconds`, the Hunter gains a stat modifier and/or an enhanced basic-attack behavior. | N/A — inherently non-spatial already | Unchanged in spirit; specified here as a pure buff-window, no movement implication to strip out |
| **Summon** | An autonomous ally that performs its own attack against the player's current target every `summon_attack_interval_seconds`, for `duration_seconds` or until the summon is destroyed by the creature's AI. | The player directing a companion around the arena | The summon has no independent position to command — it just fires on a timer against whatever part the Hunter has targeted, fully automatic |
| **Trap** | An **armed, conditional** ability: casting it does not deal damage immediately — it enters an "armed" state and resolves automatically the instant its `trigger_condition` (a Form-level trigger, distinct from a bound Vow's condition) becomes true, or is discarded, unresolved, if the encounter ends first. | A physical device placed on the ground, waiting for the player/creature to walk over it | No ground exists to place it on — "trap" becomes a **temporal/conditional** ability instead of a spatial one: you arm it, then wait for a game-state event (a telegraph, a part-break, a cooldown) to detonate it |
| **Mark** | Applies a debuff glyph directly to the targeted part for `mark_duration_seconds`, amplifying subsequent damage taken by that part (Formula 6, §4.6) rather than dealing meaningful direct damage itself. | A spatial zone the creature must be lured into | The "zone" collapses to the single targeted part — Mark is purely a setup tool other hits (basic attacks, other abilities) capitalize on |

All six Forms resolve through the identical input path already locked by `input-targeting-system`
(cycle-and-confirm or click-to-target a specific part, then a confirm/cast action) — no Form
introduces a new input primitive.

### 3.3 Vows

#### 3.3.1 The Vow Data Object

A Vow is content, not code. Every Vow in the game — the 10 authored below and any added later — is
an instance of this schema:

| Field | Type | Description |
|---|---|---|
| `vow_id` | string | Unique content key. This is the exact value stored in item-data-schema's `VowBindingEntry.vow_id` (§3.8 there) — no translation layer between the two documents. |
| `display_name` | string | Player-facing name (e.g. "Vow of the Bloodied"). |
| `category` | enum | `conditional` \| `static_cost` (§3.3.2–3.3.3). |
| `effect_mode` | enum | `gate` \| `scale` \| `n/a`. Only meaningful for `category = conditional` (§3.3.2); `n/a` for `static_cost` Vows, which have no live condition to gate or scale against. |
| `condition_type` | enum (open, illustrative) \| null | Only set for `category = conditional`. See the catalog in §3.3.4. |
| `condition_params` | map\<string, any\> | Open parameter bag, shape defined per `condition_type` — same open-map pattern item-data-schema §3.7 uses for enchantment triggers, for the identical reason: this document defines the condition *taxonomy*, not every future condition's exact parameter shape. |
| `expected_uptime` | float ∈ (0, 1), exclusive | Only set for `category = conditional`. Author-estimated fraction of combat time/opportunities the condition holds true under typical play with a build that can viably satisfy it. **Flagged, per this project's own precedent** (see `tier_health_scalar` in `creature-data-schema.md`): these are unbalanced-but-principled placeholders pending Vertical Slice playtesting, not final numbers. |
| `static_cost_stat` | string \| null | Only set for `category = static_cost`. Which Hunter stat is permanently reduced (open key, same catalog as §3.1). |
| `static_cost_magnitude` | float ∈ (0, 1) \| null | Only set for `category = static_cost`. Normalized fraction the stat is reduced by (e.g. `0.20` = −20%). |
| `power_multiplier` | float | **Derived**, not hand-authored — computed once at content-authoring time via Formula 1 (conditional) or Formula 2 (static cost), §4, then stored as fixed content data. Runtime never recomputes it; only a balance retune (changing the source formula's tuning knobs and re-deriving) changes it. |
| `restriction_description` | string | Player-facing flavor/rules text. |
| `slot_type` | enum | `ability` \| `ultimate` \| `any` (default `any`). Reserves the option for a future Vow to be Ultimate-exclusive; none of the 10 starting Vows use this restriction. |

#### 3.3.2 Category A — Conditional Vows (`effect_mode: gate` vs `scale`)

The majority of Vows, and the heart of Pillar 4. A conditional Vow's `power_multiplier` (Formula 1,
§4.1) applies to its bound ability's output **only while `vow-condition-tracking` reports the
condition as currently true** (that system owns live per-frame evaluation and the HUD signal per
art-bible §7.5's diegetic belt-ring; this document owns only what the condition *means*). Two
sub-flavors, chosen per-Vow at authoring time based on which reads better for that specific
restriction:

- **`gate`** — the ability **cannot be cast at all** unless the condition currently holds. Matches
  game-concept.md's own phrasing for two of its four named examples ("working only against
  bosses," "activating below a certain health threshold") — the ability doesn't exist in a weaker
  form outside the condition, it simply isn't available.
- **`scale`** — the ability is **always castable**, but only receives its `power_multiplier` bonus
  while the condition holds; outside the condition it functions at unmultiplied baseline power
  (`power_multiplier` effectively `1.0`). Used for Vows where a hard lockout would feel punishing
  for a build that occasionally needs the ability regardless (e.g. a "haven't taken damage yet"
  Vow shouldn't make the ability unusable forever the instant the streak breaks).

#### 3.3.3 Category B — Static-Cost Vows

A smaller, structurally different class, directly matching game-concept.md's fourth named example
("reducing defense"). There is no fluctuating condition — the Hunter pays `static_cost_magnitude`
of `static_cost_stat`, permanently, for as long as the item is equipped, in exchange for a
permanent `power_multiplier` (Formula 2, §4.2) on the bound ability. This is **not** governed by
the uptime formula (§4.1) — a permanent cost held 100% of the time would resolve to
`expected_uptime = 1.0`, which Formula 1 correctly treats as "no real restriction" (§4.1's own
design intent). A static, always-paid cost is a different, simpler kind of trade: a direct
magnitude-for-magnitude exchange, deliberately priced more conservatively than a conditional Vow's
peak reward, because it carries zero risk and zero skill expression — see §4.2's rationale.

#### 3.3.4 Starting Vow Catalog (10)

| `vow_id` | `display_name` | `category` | `effect_mode` | Condition / Cost | `expected_uptime` (conditional) or cost | `power_multiplier` (computed, §4) |
|---|---|---|---|---|---|---|
| `vow_bloodied` | Vow of the Bloodied | conditional | gate | `health_below_percent_threshold` `{threshold_percent: 30}` | 0.15 | **2.28** |
| `vow_ten_blows` | Vow of the Ten Blows | conditional | gate | `cyclic_attack_trigger` `{trigger_every_n_attacks: 10}` | 0.10 (exact: 1/10) | **2.35** |
| `vow_boss_bound` | Vow of the Boss-Bound | conditional | gate | `encounter_type_restriction` `{restricted_to: "boss"}` | 0.15 | **2.28** |
| `vow_overextended` | Vow of the Overextended | conditional | gate | `resonance_above_threshold` `{threshold_percent: 80}` | 0.20 | **2.20** |
| `vow_single_mark` | Vow of the Single Mark | conditional | gate | `target_part_lock` `{must_match_last_marked_part: true}` | 0.40 | **1.90** |
| `vow_slow_bloom` | Vow of the Slow Bloom | conditional | gate | `encounter_time_elapsed_above` `{threshold_seconds: 60}` | 0.35 | **1.98** |
| `vow_unshaken` | Vow of the Unshaken | conditional | scale | `no_dodge_or_block_used` `{scope: "this_encounter"}` | 0.55 | **1.68** |
| `vow_untouchable` | Vow of the Untouchable | conditional | scale | `no_damage_taken` `{scope: "this_encounter"}` | 0.05 | **2.43** |
| `vow_fragility` | Vow of Fragility | static_cost | n/a | `static_cost_stat: "damage_taken"`, `fragility_damage_taken_increase: 0.125` (+12.5% `final_damage`, applied at `combat-encounter-system` Formula 3b Step 5), `static_cost_magnitude: 0.111` (the eHP fraction — invariant across all `defense` values; see §4.2) | — | **1.33** |
| `vow_reckless_offering` | Vow of the Reckless Offering | static_cost | n/a | `static_cost_stat: "max_health"`, `static_cost_magnitude: 0.15` | — | **1.45** |

All ten `power_multiplier` values are fully derived in §4.1–§4.2's worked examples — none is
hand-tuned independently of the formula. Note `vow_bloodied` and `vow_boss_bound` share
`expected_uptime = 0.15` and, correctly, share an identical `power_multiplier` — the formula does
not care *why* a condition is rare, only *how* rare it is (Acceptance Criterion 1, §8).

**Illustrative `condition_type` catalog** (open, non-exhaustive — new values are content, not
schema changes, exactly matching item-data-schema §3.7's pattern for enchantment triggers):
`health_below_percent_threshold`, `health_above_percent_threshold`, `resonance_above_threshold`,
`resonance_below_threshold`, `cyclic_attack_trigger`, `encounter_type_restriction`,
`target_part_lock`, `encounter_time_elapsed_above`, `no_dodge_or_block_used`, `no_damage_taken`.
Each `condition_type`'s live detection logic belongs to `vow-condition-tracking`; this document
fixes only the name and the parameter shape.

### 3.4 The Source × Form Validity Matrix (36 Combos)

**Design call**: all 36 `(source, form)` combinations are valid — no pairing is structurally
forbidden. Source and Form are deliberately orthogonal axes (game-concept.md frames them as
independent ingredients, not a paired taxonomy), and forbidding specific combinations would narrow
the combinatorial space Vertical Slice's player-facing Weaving is explicitly meant to open up
(game-concept.md MDA table: "Expression... Resonance Weaving (Source × Form × Vow) builds";
"Discovery... hybrid forge recipes, creature evolution branches" — the same discovery logic applies
to Weaving's own combo space). A `machine`-Source `transformation` (a Hunter briefly assuming a
constructed battle-form) is exactly as thematically valid as a `spirit`-Source `trap` (a
consecrated ambush glyph) — every cell reads as flavor-different, not mechanically broken.

| Source \ Form | Projectile | Aura | Transformation | Summon | Trap | Mark |
|---|---|---|---|---|---|---|
| `body` | Valid | Valid | Valid | Valid | Valid | Valid |
| `mind` | Valid | Valid | Valid | Valid | Valid | Valid |
| `nature` | Valid | Valid | Valid | Valid | Valid | Valid |
| `machine` | Valid | Valid | Valid | Valid | Valid | Valid |
| `shadow` | Valid | Valid | Valid | Valid | Valid | Valid |
| `spirit` | Valid | Valid | Valid | Valid | Valid | Valid |

**What actually curates the MVP list is content volume, not structural validity** — MVP simply
hand-authors a small subset of these 36 cells as real, named abilities (below), not all 36.

**Curated MVP Ability Examples** (illustrative content, spanning distinct cells, demonstrating the
system end-to-end — the full MVP list is a future content-authoring pass, matching item-data-schema
and the-forge-system's own restraint about not authoring full content catalogs in a GDD):

| Example Ability | Source | Form | Suggested Starting Vow |
|---|---|---|---|
| Ember Lance | `body` | projectile | none (unbound) |
| Thornveil | `nature` | aura | `vow_unshaken` |
| Iron Bulwark | `machine` | transformation | `vow_fragility` |
| Wraithcall | `spirit` | summon | none (unbound) |
| Snare of Whispers | `mind` | trap | `vow_ten_blows` |
| Blightmark | `shadow` | mark | `vow_bloodied` |

### 3.5 The Loadout (3 Abilities + 1 Ultimate, Locked)

Locked by game-concept.md's Core Mechanics list and confirmed by art-bible §5.2/§7.5 (the belt-charm
cap and the Combat HUD's ability-readiness cluster).

- **4 total slots**: 3 `ability` slots + 1 `ultimate` slot. Each slot holds exactly one equipped
  ability-focus item (`base_type: charm`, per this document's ruling in the Open Items table).
- **A slot's Woven Ability is determined by the equipped item's `WovenAbilityDefinition`** (§3.6) —
  equipping a charm makes that charm's fixed `(source, form)` pair castable from that slot.
- **Swapping**: fully free outside of combat — no cost, no cooldown, no encounter-lock. A player may
  re-slot any owned ability-focus item into any compatible slot at any time between encounters,
  identical in spirit to any other equipment swap. `slot_type` (§3.3.1) constrains which
  `WovenAbilityDefinition`s are eligible for the `ultimate` slot if any are authored as
  Ultimate-exclusive; none of the MVP examples above are.
- **Re-weavable or permanent?** A `WovenAbilityDefinition`'s `(source, form)` pair is **permanent**
  for the life of that specific ability-focus item instance — matching the Forge's own "forged, not
  logged in a stat line" ethos (art-bible §2) and mirroring Vow permanence's tone deliberately. A
  player who wants a different Source/Form combination crafts or acquires a **different**
  ability-focus item; they never re-weave an existing one's Source or Form. **A bound Vow, by
  contrast, is independently replaceable** (leaving the old scar, per the-forge-system §3.6 rule 4)
  — the item's Source/Form identity and its Vow history are two separate permanence axes, and only
  the Vow axis is ever mutable.
- **Max 4 Vows equipped at once**: a direct consequence of the 4-slot cap, not a separately
  enforced rule — since a Vow binds 1:1 to an ability-focus item (item-data-schema Invariant 8: at
  most one active `VowBindingEntry` per item) and at most 4 ability-focus items can be equipped at
  once, at most 4 Vows can ever be simultaneously active. Art-bible §5.2 confirms this cap
  visually ("there is nowhere to hang a fifth [scar]").

### 3.6 WovenAbilityDefinition — Representing a Source×Form Combo in Data

item-data-schema's Item Definition schema deliberately does not include a `form` field (full
Definition content is explicitly out of that document's scope, per its §3.2). This document adds a
small, separate content table that any ability-focus Item Definition may be linked to by
`definition_id`:

| Field | Type | Description |
|---|---|---|
| `definition_id` | string | Foreign key to an Item Definition (item-data-schema §3.2); 1:1 — every ability-focus Item Definition has exactly one `WovenAbilityDefinition`. |
| `source` | enum | One of the 6 locked Source values (§3.1). |
| `form` | enum | One of the 6 locked Form values (§3.2). |
| `form_params` | map\<string, any\> | Per-Form parameters (e.g. `{tick_interval_seconds, duration_seconds}` for `aura`) — open bag, shape fixed per `form` by this document's Formula 5 defaults (§4.5), overridable per ability at content-authoring time within the Tuning Knob safe ranges (§7). |
| `slot_type` | enum | `ability` \| `ultimate` \| `any` — which loadout slot(s) this ability may occupy. |

**MVP**: a small, hand-authored set of `WovenAbilityDefinition`s exists as content (§3.4's example
table is illustrative of the shape, not the final list). **Vertical Slice**: a player-facing
Weaving UI (not yet in the systems index; out of this document's scope) lets the player choose
`source` + `form` directly and consume VS-defined materials to mint a **new** ability-focus Item
Instance from a small, closed family of generic charm Definitions (`fixed_source: null` per
item-data-schema §3.2, `ability_focus_eligible: true`) — reusing the-forge-system §3.2.2's exact
"generic output Definition family" pattern rather than inventing a new one, and setting `source`
explicitly on the new Instance at creation time (the same pattern item-data-schema already uses for
`creature_core`'s `fixed_source: null` case).

### 3.7 Vow-Binding Permanence — Restated From the Owning Document

This document does not redefine the binding action (the-forge-system §3.6 owns cost, ceremony, and
the `vow_binding_log` write path); it restates the permanence contract exactly so both documents
stay legible independently:

1. Binding requires `ability_focus = true` on the target item (item-data-schema Invariant 2).
2. Binding costs `vow_binding_cost` (**200 Gleam + 5 `enchantment_material`**, the-forge-system §7
   — cited, not redefined here).
3. Once written, a `VowBindingEntry` is never deleted or reverted to unbound (item-data-schema §3.8).
4. Binding over an existing Vow is allowed: the prior entry flips to `superseded` with
   `replaced_by` set, and a new `active` entry is appended — the old scar remains visible
   (art-bible §5.2), the new Vow's `power_multiplier` is what actually applies to the ability going
   forward.
5. **This document's own addition**: when a Vow is replaced, the ability's effective power
   immediately begins using the **new** Vow's `power_multiplier` and `effect_mode`/condition — there
   is no transition grace period and no blending between old and new Vow effects. The replacement
   takes effect at the same moment the-forge-system's binding transaction commits.

---

## 4. Formulas

Six formulas. Formula 1 is the load-bearing one — the uptime→power conversion the brief calls "the
single most important balance formula in the game." Formulas 3–6 govern how much raw power a Woven
Ability produces before any Vow multiplier is applied. All six compose into one worked pipeline at
the end of this section.

### 4.1 Formula 1 — Vow Power Curve (Conditional Vows) — THE KEY FORMULA

```
vow_power_multiplier = 1 + max_power_bonus × (1 − expected_uptime) ^ curve_exponent
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `expected_uptime` | float | (0, 1), exclusive | Fraction of combat time/opportunities the Vow's condition holds true under typical play (§3.3.1). Author-estimated, playtesting-pending. |
| `max_power_bonus` | float, tuning knob | 0.5–3.0 (default `1.5`) | The maximum extra multiplier attainable as `expected_uptime` → 0. |
| `curve_exponent` | float, tuning knob | 0.5–2.0 (default `1.0`) | Controls the curve's shape: `1.0` is linear; `<1.0` front-loads reward toward mid-range uptimes; `>1.0` reserves most of the reward for only the rarest conditions. |
| `vow_power_multiplier` | float | (1.0, 1 + `max_power_bonus`), open interval | Multiplies the bound ability's `ability_effective_power` (Formula 3) whenever the condition currently holds (or always, for `gate`-mode abilities, since they cannot be cast otherwise). |

**Output range**: strictly bounded and strictly monotonically decreasing in `expected_uptime` — a
condition that holds 100% of the time (`expected_uptime → 1`) approaches `vow_power_multiplier → 1`
(no real bonus, correctly treating an always-true "condition" as not a restriction at all); a
condition that almost never holds (`expected_uptime → 0`) approaches the ceiling
`1 + max_power_bonus`, but never reaches either bound exactly since `expected_uptime` is
open-interval by construction (§8 Edge Case / Acceptance Criterion 2). **This monotonicity is the
formula's entire purpose**: it is mathematically impossible, at any single value of
`max_power_bonus`/`curve_exponent`, for a higher-`expected_uptime` Vow to yield a
greater-or-equal `vow_power_multiplier` than a lower-`expected_uptime` Vow — no Vow can ever
dominate a strictly-rarer one (Acceptance Criterion 1).

**Worked examples** (default tuning, `max_power_bonus = 1.5`, `curve_exponent = 1.0`):

| Vow | `expected_uptime` | Calculation | `vow_power_multiplier` |
|---|---|---|---|
| `vow_untouchable` | 0.05 | `1 + 1.5 × (0.95)^1` | **2.43** |
| `vow_ten_blows` | 0.10 (exact — literally 1 fire per 10 attacks) | `1 + 1.5 × (0.90)^1` | **2.35** |
| `vow_bloodied` | 0.15 | `1 + 1.5 × (0.85)^1` | **2.28** |
| `vow_boss_bound` | 0.15 | `1 + 1.5 × (0.85)^1` | **2.28** |
| `vow_overextended` | 0.20 | `1 + 1.5 × (0.80)^1` | **2.20** |
| `vow_slow_bloom` | 0.35 | `1 + 1.5 × (0.65)^1` | **1.98** |
| `vow_single_mark` | 0.40 | `1 + 1.5 × (0.60)^1` | **1.90** |
| `vow_unshaken` | 0.55 | `1 + 1.5 × (0.45)^1` | **1.68** |

Note that `vow_bloodied` and `vow_boss_bound` — two entirely different restrictions — land on the
**identical** multiplier because they share the identical `expected_uptime`. This is the formula
working as intended: it is blind to *why* a condition is rare, and prices purely on *how rare*.

### 4.2 Formula 2 — Static-Cost Vow Exchange

```
static_power_multiplier = 1 + static_cost_conversion_rate × static_cost_magnitude
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `static_cost_magnitude` | float | (0, 1), exclusive | Normalized fraction of `static_cost_stat` permanently sacrificed (§3.3.1). |
| `static_cost_conversion_rate` | float, tuning knob | 1.5–5.0 (default `3.0`) | Exchange rate between sacrificed magnitude and granted power. |
| `static_power_multiplier` | float | (1.0, 1 + 5.0×1.0) at the widest safe-range extreme; **(1.0, 4.0)** at default tuning's practical ceiling | Multiplies the bound ability's `ability_effective_power` unconditionally, at all times, for as long as the item is equipped. |

**Output range**: unbounded above only in principle (as `static_cost_magnitude → 1`, i.e.
sacrificing nearly all of a stat, which no authored Vow currently does); in practice bounded by
what magnitude values content-authoring actually assigns. **Deliberately priced below a
conditional Vow's peak** (§4.1's ceiling of `2.5` at default tuning) for any `static_cost_magnitude`
below roughly `0.5` — a permanent, zero-risk, zero-skill cost should never out-reward the best
conditional Vows, which demand genuine play-pattern commitment to realize.

#### `static_cost_magnitude` is an EFFECTIVE-HP fraction, not a raw stat fraction (re-priced 2026-07-14, Blocker B3)

**The bug.** `static_cost_magnitude` was originally read as *"the fraction of the named stat you give
up"* — so `vow_fragility` (−20% `defense`) was priced at `0.20` and returned **1.60**. That is wrong
for two compounding reasons, and it made `vow_fragility` a **strictly dominant Vow** — which breaks
Pillar 4 (Builds Are Trade-offs, Not Stat Stacks), the entire reason Vows exist.

1. **Until 2026-07-14, `defense` did nothing at all.** No formula in the project read it (Blocker B3 —
   incoming damage was mistakenly routed through the *player-to-creature* pipeline, which mitigates
   using the *creature's* defense). `vow_fragility` was therefore **+60% ability power for literally
   zero cost.** `combat-encounter-system` Formula 3b now makes `defense` live, which is what allows it
   to be priced honestly for the first time.

2. **Even with `defense` live, 20% of it is not a 20% cost.** Mitigation is hyperbolic —
   `100 / (100 + effective_hunter_defense)` — so sacrificing a *fraction of the stat* costs far less
   than that fraction of *survivability*. Two Vows that sacrifice "20%" of two different stats are not
   paying comparable prices, and pricing them off the raw fraction silently rewards whichever stat has
   the flatter curve.

**The fix: `static_cost_magnitude` is defined as the fraction of _effective HP_ (eHP) sacrificed**, at
a reference `hunter_defense`. This is the only unit in which two different sacrifices are commensurable.

```
eHP = max_health × (100 + hunter_defense) / 100        — derived from Formula 3b's mitigation term
```

#### Why `vow_fragility` cannot be a *percentage of `defense`* at all (the second, deeper bug)

The obvious repair — "keep −20% `defense`, just price it in eHP" — **does not work**, and it is worth
recording why, because it looks correct until you check the boundary.

`hunter-progression-system` sets the Hunter's `defense` at **base 0**, rising to a maximum of **120**
only through purchased Training ranks. So the eHP cost of sacrificing *a percentage of defense* depends
entirely on how much defense the player bought — and at `defense = 0` it is:

```
eHP retained = (100 + 0.8 × 0) / (100 + 0) = 100 / 100 = 1.0     →  cost = ZERO
```

**A player who simply never trains `defense` pays literally nothing for the Vow.** It would still be a
free power multiplier — the exact defect we set out to remove, re-entering through a different door.
Any cost expressed as a *fraction of an investable stat* is avoidable by declining the investment.

**The fix: `vow_fragility`'s cost is a direct damage-taken penalty, not a stat sacrifice.** It applies
to the *output* of `combat-encounter-system` Formula 3b (a multiplier on `final_damage` at Step 5),
which means it cannot be zeroed by any allocation choice. You cannot decline to take damage.

```
vow_fragility:  final_damage × (1 + fragility_damage_taken_increase)      — default 0.125 (+12.5%)
```

Converting a damage-taken increase `d` into the eHP fraction it costs:

```
static_cost_magnitude = 1 − 1 / (1 + d) = 1 − 1/1.125 = 0.111
```

This is **constant** — identical for a 0-defense glass cannon and a 120-defense bulwark. It is,
finally, a price.

**Worked examples**:

| Vow | Cost mechanism | `static_cost_magnitude` (eHP fraction) | Calculation | `static_power_multiplier` |
|---|---|---|---|---|
| `vow_reckless_offering` | −15% `max_health` | **0.15** — `max_health` scales eHP linearly, so the raw fraction *is* the eHP fraction, and base `max_health` is 100 (never 0), so it can never be dodged | `1 + 3.0 × 0.15` | **1.45** (unchanged) |
| `vow_fragility` | **+12.5% damage taken** (was: −20% `defense`) | **0.111** — invariant across all `defense` values (was incorrectly `0.20`) | `1 + 3.0 × 0.111` | **1.33** (was 1.60) |

**The trade is now genuinely two-sided, and unavoidable.** `vow_fragility` grants +33% ability power
for an 11.1% eHP cost; `vow_reckless_offering` grants +45% for a 15% eHP cost. Both sit on the *same*
line — **3.0 power per 1.0 eHP** — which is precisely what `static_cost_conversion_rate` was always
supposed to mean. Neither dominates; they differ only in how far along that line the player chooses to
walk. Under the old numbers `vow_fragility` offered **more** power (1.60 vs 1.45) for a **smaller**
real cost (0% to 10% eHP vs a flat 15%) — the textbook shape of a dominant option, and a direct
Pillar 4 violation.

**One honest caveat, stated rather than hidden.** Both static Vows' costs are reducible by skilled
dodging, since Formula 3b Step 3 zeroes a dodged hit entirely. This creates no *dominance* problem —
it applies equally to both, so their relative pricing is untouched — and it is Pillar 1 working as
intended: skill should mitigate a build's downside. It never reaches zero in practice, because hazard
ticks (`combat-encounter-system` §3.10) bypass block and interrupt entirely and are dodgeable only
within a narrow grace window.

### 4.3 Formula 3 — Ability Base Power by Source

```
ability_base_power = form_base_value[form] × (1 + source_scaling_coefficient × scaling_stat_value)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `form_base_value[form]` | float, tuning knob per Form | 10–100 (§7 for the default table) | Baseline magnitude before stat scaling. |
| `source_scaling_coefficient` | float, tuning knob | 0.003–0.02 (default `0.008`) | How strongly the Hunter's Source-linked stat feeds into ability power. |
| `scaling_stat_value` | float, external input | ≥ 0, unbounded | The Hunter's current value of the ability Source's primary scaling stat (§3.1) — sourced from base stats + equipment modifiers, owned by `combat-encounter-system`'s stat catalog; treated as an opaque input here, matching item-data-schema §3.6's own stance on stat authority. |
| `ability_base_power` | float | ≥ 0, unbounded above (grows with player progression) | The ability's power before any Vow multiplier or Source-effectiveness multiplier is applied. |

**Output range**: floor `0` (a `scaling_stat_value` of `0` still yields `form_base_value[form]` — an
ability is never fully zeroed by low stats), unbounded above by design, since idle-game power
curves are expected to compound over long play sessions (matching item-data-schema Formula 3's own
"geometric, uncapped" rationale for the same genre-fit reason).

**Worked example**: a `body`-Source `projectile` (`form_base_value = 40`, §7), Hunter
`attack_power = 120`, default `source_scaling_coefficient = 0.008`:
`ability_base_power = 40 × (1 + 0.008 × 120) = 40 × 1.96 = 78.4`.

### 4.4 Formula 4 — Source Effectiveness Multiplier

A 6×6 lookup, not a continuous function, applied against the **target creature's** `source` field
(identical enum, `creature-data-schema`). Structured as a **circulant hex-cycle**: fix the order
`body → nature → spirit → shadow → machine → mind → (back to body)`; each Source is **strong**
against the next 2 Sources clockwise in that order, **weak** against the previous 2, and
**neutral** against the one directly opposite (3 steps away) and against itself. This produces a
mathematically self-consistent, fully antisymmetric table (if A is strong vs. B, B is guaranteed
weak vs. A — verified by construction, Acceptance Criterion 4) using only 3 distinct multiplier
values.

```
source_effectiveness_multiplier(ability_source, target_source) = table_lookup(ability_source, target_source)
   where the table entry is one of: strong_multiplier, weak_multiplier, or 1.0 (neutral)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `ability_source` | enum | 1 of 6 | The Woven Ability's own Source. |
| `target_source` | enum | 1 of 6 | The targeted creature's `source` field. |
| `strong_multiplier` | float, tuning knob | 1.2–2.0 (default `1.5`) | Applied when `ability_source` is favored against `target_source`. |
| `weak_multiplier` | float, tuning knob | 0.4–0.85 (default `0.667`) | Applied when `ability_source` is disfavored against `target_source`. |
| `source_effectiveness_multiplier` | float | `{0.667, 1.0, 1.5}` at default tuning | Multiplies `ability_effective_power` (post-Vow) as one factor of the creature's total damage taken; final combination with weak-point/part-crit multipliers is `combat-encounter-system`'s domain, out of scope here. |

**Full table** (row = `ability_source`, column = `target_source`; S = strong `1.5`, W = weak
`0.667`, N = neutral `1.0`):

| ability \ target | `body` | `nature` | `spirit` | `shadow` | `machine` | `mind` |
|---|---|---|---|---|---|---|
| `body` | N (self) | S | S | N (opp.) | W | W |
| `nature` | W | N (self) | S | S | N (opp.) | W |
| `spirit` | W | W | N (self) | S | S | N (opp.) |
| `shadow` | N (opp.) | W | W | N (self) | S | S |
| `machine` | S | N (opp.) | W | W | N (self) | S |
| `mind` | S | S | N (opp.) | W | W | N (self) |

**Thematic read (flagged design call, not exhaustively justified)**: Body overpowers Nature (raw
force cuts through growth) and Spirit (a physical anchor resists possession); Nature overwhelms
Spirit (roots snare ethereal forms) and Shadow (life outcompetes decay); Spirit banishes Shadow
(light dispels corruption) and destabilizes Machine (ghostly interference shorts circuits); Shadow
subverts Machine (corrodes/hacks) and preys on Mind (madness, manipulation); Machine outclasses Mind
(no consciousness to manipulate) and Body (armor beats flesh); Mind dominates Body (compels
action) and Nature (redirects instinct). Every "weak" and "neutral" cell is the forced mirror of
some other row's "strong" cell — none is independently authored, which is what keeps the table
internally consistent.

**Output range**: exactly one of `{0.667, 1.0, 1.5}` at default tuning for every one of the 36
ordered pairs; genuinely bounded, discrete, and fully enumerable (unlike a continuous formula, this
one is exhaustively testable — Acceptance Criterion 4).

**Worked example**: a `body`-Source ability targeting a `nature`-Source creature part →
`source_effectiveness_multiplier = 1.5` (strong).

### 4.5 Formula 5 — Form Output Value

Unifies Projectile/Aura/Summon/Trap into one payout structure — each Form pays out some fraction of
`ability_effective_power` (Formula 1/2 applied to Formula 3's output) per discrete event (a single
hit, one tick, one summon attack, one trap trigger).

```
form_output_value = ability_effective_power × form_output_fraction[form]
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `ability_effective_power` | float | ≥ 0 | `ability_base_power` (Formula 3) × the applicable Vow multiplier (Formula 1 or 2, or `1.0` if unbound). |
| `form_output_fraction[form]` | float, tuning knob per Form | 0.20–1.50 (§7 for the default table) | Per-event payout fraction. Deliberately **not** equal across Forms — see rationale below. |
| `form_output_value` | float | ≥ 0, unbounded above | The damage/effect magnitude of one discrete event of that Form. |

**Default `form_output_fraction` table and rationale**:

| Form | `form_output_fraction` | Default event cadence | Total payout over full duration | Rationale |
|---|---|---|---|---|
| Projectile | `1.00` (locked reference value) | Instant, single event | `1.00×` | The baseline every other Form is priced relative to. |
| Aura | `0.20` per tick | Every `2s`, for `10s` (5 ticks, §7) | `5 × 0.20 = 1.00×` | Same total payout as Projectile, spread across time — a DPS-vs-burst trade, not a power discount. |
| Summon | `0.35` per attack | Every `3s`, for `12s` (4 attacks, §7) | `4 × 0.35 = 1.40×` | Pays out *more* in total because the summon can be destroyed by the creature's AI before completing its full run (a real risk a Projectile never carries). |
| Trap | `1.50` per trigger, one-shot | Waits for `trigger_condition` | `1.50×`, or **`0×` if the trigger never fires before the encounter ends** | Priced highest because it carries the largest risk of all: a fully wasted cast if its condition never resolves (§5 Edge Case 9). |

**Output range**: `≥ 0`, unbounded above (inherits `ability_effective_power`'s own unbounded range).

**Worked example**: `ability_effective_power = 178.75` (continuing §4.6's pipeline below), Form =
`aura`: `form_output_value = 178.75 × 0.20 = 35.75` per tick, 5 ticks over the aura's 10-second
duration.

### 4.6 Formula 6 — Mark Damage-Taken Amplification

Mark does not deal direct damage in the same sense as the other Forms (§3.2) — it produces a
bounded percentage amplification applied to *other* damage sources hitting the same part.

```
mark_damage_taken_bonus_percent = min(mark_amplification_cap_percent, mark_base_percent_per_power_unit × ability_effective_power)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `ability_effective_power` | float | ≥ 0 | Same as Formula 5. |
| `mark_base_percent_per_power_unit` | float, tuning knob | 0.1–0.5 (default `0.3`) | Percent amplification granted per point of `ability_effective_power`. |
| `mark_amplification_cap_percent` | float, tuning knob | 20–60 (default `40`) | Hard ceiling — prevents a heavily-scaled Mark from trivializing part-break pacing. |
| `mark_damage_taken_bonus_percent` | float | `[0, mark_amplification_cap_percent]`, hard-clamped | Percentage bonus damage the marked part takes from all subsequent hits for `mark_duration_seconds`. |

**Output range**: hard-clamped, `[0, 40]` at default tuning — this ceiling is load-bearing, not
decorative: an uncapped Mark would let late-game power scaling turn every other Form's damage
irrelevant by comparison, breaking the "6 genuinely different tools" premise of §3.2.

**Worked examples**: `ability_effective_power = 100` → `min(40, 0.3 × 100) = min(40, 30) = 30%`.
`ability_effective_power = 150` → `min(40, 0.3 × 150) = min(40, 45) = 40%` (clamp engaged).

### 4.7 Full Pipeline — Worked Example

Threading Formulas 3 → 1 → 4 end-to-end for one concrete cast: a `body`-Source `projectile` ability
(`form_base_value = 40`), Hunter `attack_power = 120`, bound with `vow_bloodied`
(`power_multiplier = 2.28`, currently active — Hunter is below 30% health), targeting a part on a
`nature`-Source creature.

1. **Formula 3** (base power): `40 × (1 + 0.008 × 120) = 78.4`.
2. **Formula 1** (Vow multiplier, condition currently true): `78.4 × 2.28 = 178.75` →
   `ability_effective_power = 178.75`.
3. **Formula 4** (Source effectiveness, `body` vs. `nature` = strong): `178.75 × 1.5 = 268.13`.
4. **Formula 5** (Form output, `projectile` = `1.00`): `268.13 × 1.00 = 268.13`.

`268.13` is the pre-weak-point/pre-crit damage value handed to `combat-encounter-system`, which owns
any further part-vulnerability or crit multipliers on top of this number — out of scope here.

---

## 5. Edge Cases

1. **A Vow's condition can never be satisfied given the player's other choices** (e.g.
   `vow_ten_blows`'s attack-cycle condition bound to an ability whose own Form never counts as a
   "basic attack" for that counter, or `vow_boss_bound` on a build that never fights bosses).
   **Ruling: not runtime-blocked.** Matching item-data-schema §5 Edge Case 3's "fail inert, not
   fail loud" precedent: the-forge-system's binding validation only checks `ability_focus`
   eligibility and cost (§3.6 there) — it does not, and should not, simulate future combat to
   pre-validate condition-satisfiability. If a structurally-unsatisfiable pairing ships anyway, the
   bound ability simply never receives its bonus (`scale` mode) or is never castable (`gate`
   mode) — it degrades to inert/unused, never crashes, never blocks the item from otherwise
   functioning. **Content-authoring-time validation** (a future `/consistency-check`-style lint,
   not a runtime concern) should flag obviously-incompatible `condition_type`/`form` pairings
   before they ship as curated MVP content.
2. **Two equipped Vows have contradictory conditions** (e.g. one slot's Vow requires health below
   30%, another slot's requires Resonance above 80%). **Ruling: not a real conflict.** Vows bind
   1:1 to individual ability-focus items (item-data-schema §3.8), and each slot's condition is
   evaluated fully independently against the same live game state — there is no shared "loadout
   Vow state" to contradict. Both conditions can be simultaneously true, simultaneously false, or
   split, and each slot's ability simply reflects its own condition's current value. The only
   structural constraint is item-data-schema Invariant 8 (at most one *active* `VowBindingEntry*
   per single item) — which prevents the same item from ever holding two Vows at once, not two
   different items from holding different Vows.
3. **A Vow's condition flips mid-cast** — most acutely relevant to Forms with a nonzero active
   duration (`aura`, `transformation`, `summon`, and a `trap`'s armed period). **Ruling: the
   `vow_power_multiplier` (or `1.0` baseline) is snapshotted at the moment of cast and held fixed
   for that cast's entire active duration.** A health threshold crossed mid-Aura does not
   retroactively upgrade or downgrade an already-ticking Aura — it only affects the *next* cast.
   **Exception**: a Trap's own `trigger_condition` (a Form-level mechanic, §3.2, distinct from a
   bound Vow's condition) is evaluated live and continuously while armed, since reacting to a
   later game-state change is the entire point of that Form — but if the Trap's bound
   ability-focus item also carries a Vow, that Vow's `power_multiplier` is still snapshotted at
   arm-time (cast moment), not re-evaluated at trigger-resolution moment. Instant Forms
   (`projectile`, `mark`) have no meaningful "mid-cast" window at all — resolution is atomic.
4. **A Vow-bound ability-focus item is destroyed, sold, dismantled, merged, or fed to a
   creature.** **Ruling: blocked at the source, not this document's problem to re-solve.**
   the-forge-system §5 Edge Case 1 already rejects any of its four operations (sell, dismantle,
   feed, merge) on an item with a non-empty `vow_binding_log`; item-data-schema §5 Edge Case 1
   independently blocks the same items from `creature-jobs-evolution-system`'s feed-material pool.
   This document's own obligation is narrower and purely defensive: **if that block were ever
   bypassed by a bug**, the loadout slot that held the destroyed item simply becomes empty (the
   ability disappears from that slot) — the Vow's full history remains permanently resolvable in
   the now-`archived` item's `vow_binding_log` (item-data-schema §3.9), never lost. **Distinct,
   mundane case**: simply *unequipping* a Vow-bound item (moving it to inventory, not destroying
   it) is always allowed and is not a form of Vow removal — the item keeps its full
   `vow_binding_log` and may be re-equipped later with the Vow still active.
5. **A loadout slot has no ability-focus item equipped.** **Ruling: valid, non-error state.** An
   empty slot simply has no castable ability. Whether combat may begin with one or more empty
   slots is a `combat-encounter-system`/UI precondition, not an invariant this document enforces.
6. **A conditional Vow is authored with `expected_uptime` at or beyond the (0, 1) open-interval
   boundary.** **Ruling: rejected at content-authoring validation**, not shipped. `0` (a condition
   that can literally never hold) and `1` (a condition that always holds, i.e. not a restriction at
   all — should be authored as a `static_cost` Vow instead, describing the real trade directly)
   are both content-authoring errors, not runtime states. If one somehow ships anyway, runtime
   behavior is well-defined and inert per Edge Case 1's precedent — never a crash.
7. **The same `vow_id` is bound to two different ability-focus items in the loadout
   simultaneously** (e.g. `vow_bloodied` on both the weapon-charm and the armor-charm... noting all
   ability-focus items are `charm`-typed per §3.4, so concretely: two different charms). **Ruling:
   allowed.** Nothing prevents duplicate Vow types across independent slots; each binding and each
   ability's power computation is fully independent. Stacking two abilities that both spike under
   the same trigger window is a deliberate, valid "glass cannon" build choice, not a bug.
8. **The Ultimate slot's Vow rules differ from the 3 ability slots?** **Ruling: no — identical
   rules.** The Ultimate slot is mechanically just the 4th ability-focus item slot (art-bible §5.2's
   own framing: "one per equipped ability/ultimate slot"). No Vow behavior is special-cased for it;
   only `slot_type` eligibility (§3.3.1, §3.6) differs, and only if a future Vow or
   `WovenAbilityDefinition` explicitly restricts to `ultimate`.
9. **An armed Trap's `trigger_condition` never fires before the encounter ends** (e.g. the creature
   dies before telegraphing the specific attack the Trap was waiting for). **Ruling: the Trap's
   payload never resolves, and its cast is fully wasted for that encounter** — a deliberate,
   valid outcome, not an error. This is precisely the risk `form_output_fraction[trap] = 1.50`
   (§4.5) is priced to compensate for. Any still-armed Trap is discarded at encounter end with no
   carry-over into the next encounter (matching Pillar 1's single-screen, contained-arena framing —
   no persistent world state between fights).
10. **A `static_cost` Vow's item is unequipped mid-encounter (if a future feature ever allows
    combat-time re-slotting; none does at MVP).** **Ruling: out of scope — not designed here.**
    MVP and VS both assume loadout changes only happen between encounters (§3.5); this document
    takes no position on a hypothetical future mid-combat re-slot feature, and flags it should such
    a feature ever be proposed, since a static cost's stat reduction would need an explicit
    apply/remove transition rule at that point.

## 6. Dependencies

**Depends on**: `item-data-schema` (Foundation tier). Every field this document writes to or reads
from is item-data-schema's exact name: `ability_focus`, `ability_focus_eligible`, `vow_binding_log`
and its `VowBindingEntry` fields (`binding_id`, `vow_id`, `bound_at`, `status`, `replaced_by`),
`base_type`, `definition_id`, `source`/`fixed_source`. Nothing here redefines that schema; §3.6
adds a new, separate content table (`WovenAbilityDefinition`) that references `definition_id` as a
foreign key rather than modifying item-data-schema's own shape — the same pattern the-forge-system
used for its Hybrid Recipe schema.

**Coordinates with** (non-overlapping split, explicit both directions):

- **`the-forge-system`** — owns the Vow-binding **action**: cost (`vow_binding_cost`, §3.7 here,
  cited not redefined), ceremony, irreversibility enforcement, and the `vow_binding_log` write path
  (the-forge-system §3.6). This document owns the Vow **catalog**: what each `vow_id` restricts and
  grants, and the formula converting a condition's rarity into power (§3.3, §4.1–4.2). Neither
  document should redefine the other's half — if this document's Vow catalog grows or changes
  shape, the-forge-system's binding action does not need to change, since it only ever writes
  whatever `vow_id` is supplied to it (the-forge-system §6, stated from that document's side
  already).

**Depended on by** (bidirectional — each system below must reference this document's field names
and formulas directly, not redefine its own Vow/Source/Form shape, when it is authored):

- **`combat-encounter-system`** (not yet authored) — the primary consumer. Reads
  `WovenAbilityDefinition`s to know what abilities exist per loadout slot, calls Formula 3
  (ability base power) against its own stat catalog, applies Formula 4 (Source effectiveness) and
  Formula 5/6 (Form output/Mark amplification) as inputs to its own damage-resolution pipeline
  (weak-point/part-crit multipliers are entirely that system's domain, not this one's). Also owns
  the Resonance resource's generation/spend economy, referenced only qualitatively here (as a Vow
  condition parameter, e.g. `resonance_above_threshold`).
- **`vow-condition-tracking`** (not yet authored) — owns **live per-frame evaluation** of every
  `condition_type` this document defines, and the HUD signal surfacing "satisfied"/"violated"
  state via the diegetic belt-ring (art-bible §7.5). **The split is explicit**: this document
  defines what a condition *is* and *means* (the `condition_type` catalog, §3.3.4, and each Vow's
  `effect_mode`); `vow-condition-tracking` defines *how* to detect that condition is true or false
  at runtime, tick by tick, and how it flips the HUD's closed-ring/open-ring state. Neither
  document should re-implement the other's half.
- **`memory-dust-prestige-system`** (Full Vision tier, not yet authored) — per `systems-index.md`'s
  dependency map, depends on this document directly. When authored, it must state explicitly
  whether a prestige reset wipes bound Vows, unlocks new `condition_type`s / Sources / Forms, or
  both — matching item-data-schema's own explicit non-position on prestige persistence. Flagged as
  the natural home for "knowledge (weak-point patterns, Vow timing...)" progression named in
  game-concept.md's growth-axis description.
- **`combat-hud`** / **`forge-ui`** (not yet authored) — presentation layers. This document
  specifies data and rules only; belt-charm rendering, cooldown pip-tracks, and the Vow-condition
  ring/flash are entirely art-bible §7.5's and those UI systems' domain.

**Soft/informal forward references** (not blocking): `creature-data-schema`'s Source enum is not a
formal dependency in either direction — per item-data-schema §3.5's own precedent, both documents
cite art-bible §4.2 as the shared canonical source rather than depending on each other directly.

## 7. Tuning Knobs

| Knob | Default | Safe Range | Affects | MVP / VS | Notes |
|---|---|---|---|---|---|
| `max_power_bonus` (Formula 1) | `1.5` | 0.5–3.0 | Ceiling on conditional-Vow power bonus | MVP-locked default, retunable | Raising this raises the reward ceiling for extreme-uptime Vows without changing the monotonic ordering (Acceptance Criterion 1 holds at any value in range) |
| `curve_exponent` (Formula 1) | `1.0` | 0.5–2.0 | Shape of the uptime→power curve | MVP-locked default, retunable | `<1.0` = more generous to mid-range uptimes; `>1.0` = reserves reward for only the rarest conditions. Recommend holding at `1.0` until VS playtesting data exists |
| `static_cost_conversion_rate` (Formula 2) | `3.0` | 1.5–5.0 | Static-cost Vow exchange rate | MVP-locked default, retunable | Kept low enough that no static Vow at typical `static_cost_magnitude` values (≤0.3) out-rewards the weakest conditional Vow (§4.2's rationale) |
| `form_base_value[form]` (Formula 3) | Projectile `40`, Aura `35`, Transformation `30`, Summon `45`, Trap `50`, Mark `25` | 10–100 each | Per-Form baseline power | MVP-locked defaults, retunable | Trap and Summon start highest, matching their delayed/at-risk payout structure (Formula 5's rationale) |
| `source_scaling_coefficient` (Formula 3) | `0.008` | 0.003–0.02 | How hard Hunter stats scale ability power | MVP-locked default, retunable | Directly controls late-game power-curve steepness; coordinate with `combat-encounter-system`'s eventual stat-growth curve before final lock |
| `strong_multiplier` / `weak_multiplier` (Formula 4) | `1.5` / `0.667` | 1.2–2.0 / 0.4–0.85 | Source-matchup swing | MVP-locked defaults, retunable | Recommend keeping `weak_multiplier` close to `1/strong_multiplier` for a symmetric swing, though the two knobs are independently tunable |
| `form_output_fraction[form]` (Formula 5) | Projectile `1.00` (locked reference), Aura `0.20`/tick, Summon `0.35`/attack, Trap `1.50`/trigger | 0.20–1.50 | Per-event Form payout | MVP-locked defaults, retunable (Projectile's `1.00` is the fixed reference point, not independently tunable) | |
| `aura_tick_interval_seconds` / `aura_duration_seconds` (Form 5) | `2.0` / `10` | 1–4 / 6–20 | Aura tick cadence and total uptime | MVP-locked defaults, retunable | |
| `summon_attack_interval_seconds` / `summon_duration_seconds` (Form 5) | `3.0` / `12` | 2–6 / 8–25 | Summon attack cadence and lifespan | MVP-locked defaults, retunable | |
| `mark_base_percent_per_power_unit` (Formula 6) | `0.3` | 0.1–0.5 | Mark amplification scaling | MVP-locked default, retunable | |
| `mark_amplification_cap_percent` (Formula 6) | `40` | 20–60 | Mark amplification hard ceiling | MVP-locked, **do not remove the cap** | Removing this cap entirely would let Mark's amplification dominate all other Forms at high power — the cap's *existence* is a structural rule, only its *value* is a free knob |
| Loadout slot count (3 ability + 1 ultimate) | `4` total | **locked** | Max simultaneously equipped Woven Abilities | MVP-locked, not tunable | Sourced from game-concept.md's Core Mechanics list |
| Max simultaneously equipped Vows | `4` | **locked** | Derived from the loadout cap, not independently set | MVP-locked, not tunable | Art-bible §5.2's belt-cap confirms this visually |
| Ability-focus `base_type` restriction | `charm` only | **locked** | Which items may carry a Woven Ability / Vow | MVP-locked, this document's own ruling | Resolves item-data-schema's open item (front matter) |
| Source×Form validity | all 36 combos valid | **locked, not tunable** | Weaving combinatorial space | MVP: small curated subset authored as content; VS: full space player-accessible | Structural rule, not a numeric knob (§3.4) |
| `WovenAbilityDefinition.source`/`.form` permanence per item instance | permanent | **locked, not tunable** | Whether an ability's Source/Form can be changed after creation | MVP + VS locked | §3.5 — mirrors Vow-scar permanence tonally |
| `vow_binding_cost` | `200` Gleam + `5` `enchantment_material` | Gleam 100–500; materials 2–10 | Cost of binding a Vow | **Owned by `the-forge-system` §7 — cited here, not redefined** | Any retune must happen in that document, not this one |
| VS Weaving material/currency cost (new ability-focus item creation) | not specified | not committed | VS Weaving economy | **VS-deferred** | §3.6 — a future content/economy pass |

## 8. Acceptance Criteria

| # | Criterion | Verification Method |
|---|---|---|
| AC1 (primary — no dominant Vow) | For any two conditional Vows A and B with `expected_uptime_A < expected_uptime_B`, `vow_power_multiplier_A > vow_power_multiplier_B` always holds, at any `max_power_bonus`/`curve_exponent` combination within the safe tuning ranges (0.5–3.0 / 0.5–2.0). Equal `expected_uptime` values always yield equal multipliers, regardless of `vow_id`. | Property-based test: sample the safe tuning range and 100+ random `expected_uptime` pairs in (0,1); assert strict monotonic ordering holds in every case, and equal inputs yield byte-identical outputs. |
| AC2 | Every authored conditional Vow's `expected_uptime` is strictly within the open interval (0, 1) — never exactly 0 or 1. | Content-catalog validation test enumerating all 10 starting Vows (and any future additions) at build time; reject on load if violated. |
| AC3 | `static_power_multiplier` (Formula 2) is monotonically non-decreasing in `static_cost_magnitude`, and strictly less than the maximum achievable `vow_power_multiplier` (Formula 1, at default tuning) for any `static_cost_magnitude ≤ 0.5`. | Unit test sweeping `static_cost_magnitude` from 0 to 1 in 0.05 steps, asserting monotonic non-decrease and the stated inequality at the 0.5 boundary. |
| **AC3b (added 2026-07-14 — no dominant STATIC Vow, Blocker B3)** | **Every** authored `static_cost` Vow's `static_cost_magnitude` equals the fraction of **effective HP** it sacrifices (`eHP = max_health × (100 + hunter_defense) / 100`, from `combat-encounter-system` Formula 3b) at the reference `hunter_defense = 100` — **not** the raw fraction of its named stat. Consequently all static Vows lie on one line: `static_power_multiplier = 1 + 3.0 × (eHP fraction sacrificed)`. No static Vow may offer *more* power than another for *less* eHP. | Content-catalog validation test: for each static Vow, recompute its eHP loss from its raw stat sacrifice via Formula 3b's mitigation curve, assert it equals the authored `static_cost_magnitude` within tolerance, then assert the power/cost ratio is identical across all static Vows. **This test is the one that would have caught `vow_fragility`** — priced at raw `0.20` (→ 1.60) while actually costing only 10% eHP, it offered *more* power than `vow_reckless_offering` for a *smaller* real cost: a strictly dominant option, and a direct Pillar 4 violation. AC1 could never have caught it, because AC1 only ranges over *conditional* Vows. |
| **AC3c (added 2026-07-14 — no Vow is free)** | Every `static_cost` Vow's sacrificed stat is **read by at least one live formula**, such that increasing `static_cost_magnitude` produces a measurable, strictly worse outcome for the player somewhere in the game. | Integration test: for each static Vow, bind it, then assert the named stat's degradation changes a real output — e.g. `defense` → assert `final_damage` from `combat-encounter-system` Formula 3b strictly increases. **`vow_fragility` failed this for the entire design phase**: `defense` was in the Hunter Stat Catalog but consumed by *no formula anywhere in the project*, making the Vow +60% ability power for literally zero cost. A stat that nothing reads is not a price. |
| AC4 | `source_effectiveness_multiplier` returns exactly one of `{strong_multiplier, weak_multiplier, 1.0}` for all 36 ordered `(ability_source, target_source)` pairs, and the table is fully antisymmetric — if `(A, B)` is strong, `(B, A)` is guaranteed weak. | Unit test enumerating all 36 pairs against the locked hex-cycle table; a second test asserting the antisymmetry property programmatically rather than by table inspection. |
| AC5 | An ability-focus item never carries more than 1 simultaneously active `VowBindingEntry`. | Inherited/re-asserted from item-data-schema AC7/Invariant 8 — integration test confirming this system's write path never violates it. |
| AC6 | The loadout never exceeds 4 total equipped ability-focus items (3 `ability` + 1 `ultimate`), and therefore never exceeds 4 simultaneously active Vows. | Unit test attempting a 5th equip; assert rejection. |
| AC7 | A `gate`-mode conditional ability is never castable while its bound Vow's condition is false; a `scale`-mode ability is always castable, applying `power_multiplier = 1.0` (baseline) whenever the condition is false. | Unit test toggling a mocked condition state true/false for one `gate` and one `scale` Vow, asserting cast-eligibility and applied multiplier match the expected mode behavior in both states. |
| AC8 | `mark_damage_taken_bonus_percent` never exceeds `mark_amplification_cap_percent`, regardless of how large `ability_effective_power` is. | Unit test at `ability_effective_power` values far exceeding the cap threshold (e.g. 10,000); assert output is clamped exactly at the cap. |
| AC9 | An Aura's total payout across its full default duration (`aura_duration_seconds / aura_tick_interval_seconds` ticks × `form_output_fraction[aura]`) equals exactly `form_output_fraction[projectile]` (`1.00`) at default tuning, within floating-point tolerance. | Computed test at default tuning values (5 ticks × 0.20 = 1.00); assert equality within 1e-6. |
| AC10 | A Vow's stored `power_multiplier` is deterministic content data — re-deriving it from the same `expected_uptime`/`static_cost_magnitude` and the same tuning-knob values always yields a byte-identical result; it never drifts at runtime absent an explicit balance retune. | Unit test computing each of the 10 starting Vows' `power_multiplier` twice from stored inputs; assert identical output both times. |
| AC11 | Every one of the 36 `(source, form)` combinations is accepted by `WovenAbilityDefinition` content validation — none is rejected purely for its Source/Form pairing. | Unit test constructing one `WovenAbilityDefinition` per combination; assert zero rejections attributable to Source×Form incompatibility. |
| AC12 | Once created, a `WovenAbilityDefinition` instance's `source` and `form` fields are immutable — no code path modifies them after creation. | Unit test attempting a post-creation mutation; assert rejection or absence of a mutation API entirely. |
| AC13 | A Trap-Form ability whose `trigger_condition` never resolves before encounter end produces zero `form_output_value` and leaves no persistent state into the next encounter. | Integration test (authored alongside `combat-encounter-system`) simulating an encounter ending with an armed, untriggered Trap; assert no damage/effect was applied and no carry-over state exists at the next encounter's start. |

---

## Cross-System Facts Proposed for Registration

This document does not write to `design/registry/entities.yaml` directly (registry updates are
coordinated centrally this session to avoid write conflicts between parallel agents, per this
project's established pattern — see `item-data-schema.md` and `the-forge-system.md`'s own closing
sections). The following are proposed for central registration:

**Formulas**:
- `vow_power_curve` (Formula 1, §4.1) — the primary cross-system formula; `vow-condition-tracking`
  and `combat-encounter-system` are both expected consumers.
- `static_cost_vow_exchange` (Formula 2, §4.2).
- `ability_base_power_by_source` (Formula 3, §4.3) — expected consumer: `combat-encounter-system`.
- `source_effectiveness_multiplier` (Formula 4, §4.4), including the full 6×6 table — expected
  consumer: `combat-encounter-system`.
- `form_output_value` (Formula 5, §4.5) and `mark_damage_taken_bonus_percent` (Formula 6, §4.6).

**Constants**:
- `max_power_bonus` (`1.5`), `curve_exponent` (`1.0`) — Formula 1.
- `static_cost_conversion_rate` (`3.0`) — Formula 2.
- `source_scaling_coefficient` (`0.008`) — Formula 3.
- `strong_multiplier` (`1.5`), `weak_multiplier` (`0.667`) — Formula 4.
- `mark_amplification_cap_percent` (`40`) — Formula 6.
- Loadout cap (`3` abilities + `1` ultimate = `4` total slots) and max simultaneously equipped Vows
  (`4`) — §3.5, likely referenced by `combat-hud`, `creature-roster-ui`-adjacent loadout UI, and
  `memory-dust-prestige-system`.

**Content catalogs** (not registry-typed facts, but flagged since other systems will reference
them by name): the `vow_id` catalog (10 starting entries, §3.3.4) and the Source/Form enums (§3.1,
§3.2 — Source is already the shared `creature-data-schema`/`item-data-schema` enum via art-bible
§4.2; Form is new, defined here for the first time, canonical source is this document).

**Not proposed for registration** (deliberately): the Source×Form validity matrix's "all 36 valid"
ruling and the ability-focus-is-charm-only ruling are locked, structural rules rather than tunable
numeric facts — cited from this document's §3.4/§7 by name, matching the precedent item-data-schema
and the-forge-system both set for their own locked structural rules.
