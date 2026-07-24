# GDD: Forge UI

## Document Status

- **Status**: Complete — all 8 sections authored
- **Created**: 2026-07-14
- **Owned By**: ux-designer
- **Review Mode**: `lean` — authored autonomously this session (no user available to approve
  section-by-section; matching the precedent set this session by `the-forge-system.md`,
  `item-data-schema.md`, `accessibility-settings-system.md`, and `resonance-weaving-system.md`).
  Every ambiguity the source material left open has been resolved with an explicit, flagged
  design call rather than a placeholder. Validate at `/ux-review` and `/gate-check` before
  Production.
- **Priority**: MVP, Presentation tier — 18th in the recommended design order
  (`design/gdd/systems-index.md`), depending on `the-forge-system` (7th). Per that system's own
  scope split, this document's MVP surface is **merge + sell + dismantle + feed + Vow-binding
  only**; the hybrid-recipe book and Forge-side enchantment application (`the-forge-system` §3.3–
  §3.4) are Vertical-Slice-deferred and are explicitly out of scope here — no UI is designed for
  them in this pass.

## Source Material Read

`design/gdd/the-forge-system.md` (full — every operation, formula, and edge case cited below by
exact name), `design/gdd/item-data-schema.md` (full — every rendered field cited by exact name),
`design/gdd/accessibility-settings-system.md` (full — its binding requirements, §3.7, are treated
as non-negotiable constraints on this screen), `design/gdd/resonance-weaving-system.md` (full —
its 10-entry Vow catalog and `VowDefinition` schema are what the Vow-binding flow's item/Vow
pickers actually render), `design/art/art-bible.md` (§2, §3.2, §3.4, §4.2, §4.3, §4.5, §5.2, §7.1,
§7.2, §7.3, §7.4, §7.5, §7.6, §7.7 — the binding visual/interaction constraints this document
specifies against, never re-litigates), `design/gdd/game-concept.md` (Pillar 4, Anti-Pillars),
`design/gdd/systems-index.md` (this system's entry and dependency map).

## Assumptions Log (resolved this session, no placeholders left below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | Item names, mode-tab labels, and Vow `display_name`s render in the pixel **Display face** even at dense grid/card scale, per this task's own binding directive ("pixel display face for titles/item names"). | Art-bible §7.1 explicitly lists this face for titles and short proper-noun labels; extending it to item-tile labels is the literal reading of the given constraint. **Flagged risk**: unlike the screen titles and ability names §7.1 names as examples, an inventory grid repeats this label dozens of times at small size, which is closer to the *dense tabular* case §7.1 built the scalable Data face to solve. If density playtesting shows label legibility failing at grid scale, the documented, pre-authorized fallback is to move item-tile names to the Data face — this is a fallback this document pre-approves, not a future redesign. |
| A2 | `expected_uptime` (`resonance-weaving-system` §3.3.1) is **not** shown to the player anywhere in this UI. Only `restriction_description` (qualitative) and `power_multiplier` (quantitative payoff) are player-facing on a Vow card. | `expected_uptime` is an author-estimated balancing input, not a promise made to the player. Displaying it as a literal percentage would read as the game grading the player's own condition ("this Vow triggers 15% of the time"), which undercuts the player's own read of how hard a restriction is to satisfy — a read Pillar 4 wants the player to develop through play, not receive as a spoiler. |
| A3 | Vow `category`/`effect_mode` (`gate` / `scale` / `static_cost`) is represented by new **chrome** iconography (a padlock, an up-arrow, and an anchor respectively) — not a `3.2` glyph-grammar container. | No entry in art-bible §3.2's locked glyph grammar (Source, Vulnerability, Ward, Role badge, Vow scar) covers this axis; it is a Resonance-Weaving-specific taxonomy invented after §3.2 was locked. Per §7.2's own test ("would this icon still mean the same thing bolted onto a completely different game?"), a padlock for "conditional/gated" and an anchor for "permanent cost" are pre-learned, generic symbols — chrome, not content. |
| A4 | The Vow-binding cost's `enchantment_material` component (5 units, `the-forge-system` §7) is displayed as a **single aggregate count**, with no material sub-type breakdown. | `the-forge-system`'s own material sub-type catalog is explicitly future content authoring (its §3.5.2). This document cannot display a breakdown that does not yet exist; a future content pass that sub-types materials will need to revisit this display, and is flagged to do so. |
| A5 | **Bulk multi-select applies to Sell and Dismantle only. Feed is always single-item, single-creature-target**, and is never offered as a multi-select destination. | `the-forge-system` §3.5.3 states the Feed action explicitly: "a single atomic step, no queueing, no batching, consistent with the room's 'no timers' mood." This is a locked rule from the owning system, not a UX preference — this document conforms to it rather than proposing batched Feed. |
| A6 | **Merge has no bulk or "auto-merge" mode.** Every merge is a deliberate, individually-composed 3-slot act, with no shortcut that runs multiple merges from one confirmation. | Directly required by `the-forge-system`'s own Player Fantasy (§2): "not a menu operation dressed up with a progress bar... watching three separate things actually become a fourth, different thing." A batch-merge feature would contradict the system's own stated intent. |
| A7 | **Any disposal action (single-item or bulk; Sell, Dismantle, or Feed) that includes at least one Legendary-rarity item requires one additional, explicit acknowledgment step** beyond the standard confirmation gate. | Not required by `the-forge-system`'s data layer, which explicitly permits Legendary disposal without restriction (its §5 Edge Case 6: "a confirmation prompt on high-rarity feeds, if desired, is a `forge-ui` concern, not a data-validation one"). This document exercises that explicit invitation as a UX safety net against high-value accidental loss — a design call, not a rule inherited from elsewhere. |
| A8 | Vow-bound items are **excluded entirely** (not merely styled as disabled) from the Merge, Sell, Dismantle, and Feed item pickers by default. A per-session, off-by-default "Show unavailable items" toggle reveals them in a disabled state with a reason tag, for discoverability. | Satisfies "impossible to attempt, not merely rejected" while avoiding the confusing UX of an item silently vanishing from every list with no explanation available on request. Default-off keeps the common-case grid uncluttered, matching art-bible §3.4's density discipline. |
| **A8b** | **Creature-equipped items (`equipped_to_creature_id != null`) are excluded from all four pickers by exactly the same mechanism as A8**, with the reason tag "Equipped to [creature name] — unequip in the Roster to use here." | **Added 2026-07-14, `/review-all-gdds` Scenario 3 fix.** `item-data-schema` field 16 / Invariant 9 and `the-forge-system` §3.5.0 forbid every Forge operation on a creature-equipped item, but no UI enforcement existed — an equipped charm was fully sellable, dismantlable, mergeable, and feedable-to-a-different-creature. **Naming caution for implementers**: this screen's existing "Equipped" status tag (§3.3) refers to the *ability-focus combat loadout* (`resonance-weaving-system`'s 4 slots), which is a **different, unrelated meaning of "equipped."** The two must not be conflated — an ability-focus item is Forge-eligible unless Vow-bound; a creature-equipped item is never Forge-eligible. Use distinct tag copy for each. |
| A9 | `forge-ui` does **not** consume `accessibility-settings-system`'s `simplified_ui_density` setting. | That setting is explicitly scoped by its owning document (§3.6) to Loot Filters, Automation Config, and Creature Roster — screens it justifies as "maximum-density, zero-ornament utility interfaces by design." The Forge carries Medium ceremony (art-bible §7.6), not zero ornament, so it does not structurally qualify under that setting's own stated rationale. If Forge density becomes a real cognitive-load problem in playtesting, extending `simplified_ui_density` to cover it is a future revision to `accessibility-settings-system` itself (a file this document does not edit), not a scope this document can unilaterally claim. |
| A10 | The Forge lineage tooltip (resolving `the-forge-system` §5 Edge Case 16, explicitly left open to `forge-ui`) shows **immediate parents only** by default, with a "View full lineage" expansion for deeper multi-generation ancestry. | Keeps the common case (checking what an item was merged from) lightweight and matches the density discipline (art-bible §3.4) that governs the rest of this screen; deep ancestry chains are the rare case, so they are opt-in detail, not default clutter. |

---

## 1. Overview

The Forge UI is the player-facing interface for every operation `the-forge-system` defines:
merging three compatible items into one, disposing of unwanted loot via Sell, Dismantle, or Feed,
and binding a Vow onto an ability-focus item. It is organized as three navigable modes — Merge,
Disposal, Vow-Binding — behind a single persistent header showing the player's Gleam balance and
material count, with every irreversible action gated behind one unified confirmation pattern that
scales in weight with what the art bible calls that action's ceremony tier: plain chrome for
routine disposal, Medium-tier frame richness for the Forge's general workspace (Merge and
Disposal), and the game's second-highest ceremony treatment — heavier ornament and a live
molten-to-cooled sear animation played directly on the confirm control — reserved for the single
highest-stakes action in the room, binding a Vow. This document specifies interaction flows,
validation and filtering rules, confirmation-gate mechanics, bulk-operation tooling, and
keyboard/gamepad/accessibility compliance; it does not specify pixel layout, exact colors, or
implementation (`art-director`/`ui-programmer` territory), and it does not redefine any formula,
cost, or rule already locked by `the-forge-system`, `item-data-schema`, or
`resonance-weaving-system` — every number and rule below is cited from its owning document.

---

## 2. Player Fantasy

Art-bible §2 sets the Forge's mood as **alchemical focus** — "a tinkerer's intimacy with material
and consequence... the mood should slow the player down to feel the weight of the decision." This
document's job is to make that mood survive contact with a UI, which is the place mood most often
dies quietly: a beautifully art-directed room can still feel like a spreadsheet if its buttons fire
instantly and its costs are a small gray number nobody reads. Concretely, that means the interface
itself has to *withhold* speed at the exact moments the fiction calls for weight — a merge, a
disposal, and above all a Vow-binding must never complete on a single accidental input, because the
friction of a confirmation gate is not a usability tax here, it is the mechanism that makes "the
trade-off is forged, not logged in a stat line" (art-bible §2) true of the interface and not just
the artwork behind it. A player should feel, at the moment they commit to a merge, that they are
choosing to let three specific things stop existing — not clearing a queue.

That said, this mood has a deliberate limit, and knowing where it stops is as important as knowing
where it applies. A player returning from a long expedition with forty items to clear out is not
performing alchemy forty times — they are doing housekeeping, and the interface should let them do
it briskly. Art-bible §3.4 and §7.6 are explicit that ornamentation flexes with *ceremony*, never
with *density*: the Forge's frame richness does not thicken because a list got longer, and by the
same logic, this document does not slow bulk disposal down to match the pace of a single
considered merge. The "weight of the decision" the mood targets is reserved for the decisions that
actually carry weight — which specific three items become a fourth thing, which specific Vow a
Hunter accepts — and is deliberately *not* asked of a player clearing forty Common weapons in one
pass. Getting this balance wrong in either direction breaks the fantasy: too much friction
everywhere turns the workshop into a chore; too little friction on the ceremonial moments turns the
Forge into a vending machine. Every interaction pattern in Section 3 is built to land on the
correct side of that line for its specific action, not to apply one uniform pace to the whole
screen.

---

## 3. Detailed Rules

### 3.1 Scope Summary

| Forge UI Feature | Scope | Section |
|---|---|---|
| Merge flow (3 slots → 1 output, preview, confirm) | **MVP** | §3.4 |
| Vow-binding flow (ceremony moment) | **MVP** | §3.5 |
| Disposal flow — Sell / Dismantle / Feed (single item) | **MVP** | §3.6 |
| Bulk operations (multi-select, rarity-threshold presets) | **MVP** | §3.7 |
| Hybrid-recipe book UI | **Vertical Slice — not designed here** | — |
| Forge-side enchantment-application UI | **Vertical Slice — not designed here** | — |

Per `the-forge-system` §3.1, hybrids and Forge-driven enchantment application are fully specified
at the data/rules level but deferred to Vertical Slice for content volume reasons. This document
follows suit: no screen, flow, or wireframe region is allocated to either feature at MVP. When
Vertical Slice adds them, the mode-tab rail (§3.2) is the natural extension point — two additional
tabs, not a redesign of the existing three.

### 3.2 Screen Architecture & Persistent Elements

**Always visible, in every mode** (a fixed-height header, never scrolling out of view):

- **Gleam balance** — glyph icon + a tabular-figure number (art-bible §7.1: any number appearing
  in a comparison context uses the Data face with tabular figures; the Gleam balance is compared
  against costs constantly, so it qualifies).
- **Material count** — a single aggregated `enchantment_material` count (Assumption A4), with a
  small expandable "View Materials" affordance reserved as a forward hook for the day
  `the-forge-system`'s material sub-type catalog exists; at MVP it expands to nothing beyond the
  same aggregate number, so it is inert but present, avoiding a later layout change.
- **Mode-tab rail** — three tabs: **Merge**, **Disposal**, **Vow-Binding**. A single row of chrome
  tab controls (art-bible §3.4/§7.2 chrome iconography: flat, single-weight, Void Ink/Bone
  Parchment/Cold Slate only). Keyboard/gamepad: a dedicated "switch mode" input (left/right
  bumper, or a keyboard modifier + arrow) cycles tabs without needing to tab through the entire
  header first — modes are a top-level navigation concern, not buried in general tab order.

**Ceremony tier by region** (art-bible §7.6, cited directly, not re-derived): the header and the
mode-tab rail are chrome-neutral at all times. The Merge and Disposal content areas render at the
Forge's **Medium** general-workspace tier. The Vow-Binding confirmation panel specifically escalates
to **High** the moment a pending Vow selection exists (§3.5) — everything in Vow-Binding mode
*before* that point (browsing the item picker, browsing the Vow catalog) stays at the same Medium
tier as the rest of the workspace, so the ceremony spike is reserved for the actual commitment
moment, not the whole mode.

**Item-picker sort/filter chrome** (shared across all three modes' grids): a search-by-name field,
and sort controls for rarity, `base_type`, and Source — standard chrome iconography per art-bible
§7.2 (sort arrows, search glyph, drop-down caret), never stained-glass ornament.

### 3.3 Shared Item-Tile & Item-Picker Pattern

Every item grid in every mode (Merge's inventory picker, Disposal's inventory picker, Vow-Binding's
item picker) renders tiles built from the same rules, so a player only learns this pattern once:

- **Rarity**: the countable ring-frame ornament (art-bible §4.3) — 0/1/2/2+flourish/full-seal — is
  the tile's frame. This is the mechanical rarity signal; a confirming rarity-tier color glow rides
  on top of it, never replacing it (art-bible §4.3's own design test, restated here as a binding
  rule for this screen: "the frame ring-count is the mechanical rarity signal; the color is a
  confirming glow only").
- **Source medallion** (`item-data-schema` field `source`): rendered in a tile corner using the
  exact §3.2/§4.2 glyph unmodified — never a simplified icon set (art-bible §3.4/§7.2). Absent
  entirely (no medallion, not an empty-medallion placeholder) when `source = null`.
- **`base_type` silhouette**: a small flat icon of the actual item category (weapon/armor/charm
  silhouette family, `enchantment_material` vial silhouette, `creature_core` shard silhouette) —
  art-bible §7.2's "established world-object silhouette" category, reused, not invented fresh for
  this screen.
- **Item name**: pixel Display face (Assumption A1).
- **Modifiers/enchantments row**: shown only for `weapon`/`armor`/`charm` tiles. Rendered as a
  compact fill-count readout (e.g., "3/4 mods, 1/2 ench") in the Data face, tabular figures.
  **Never rendered at all** for `enchantment_material` or `creature_core` tiles — per
  `item-data-schema` Invariant 1, those `base_type`s always have empty `modifiers`/`enchantments`,
  and showing a "0/0" row on every material tile would read as a broken or incomplete item rather
  than correctly-empty, so the row is omitted entirely rather than shown at zero.
- **Status tags** (chrome text/icon pairs, never color-only, per §3.7 item 4 below): "Vow Bound"
  (linear brand glyph, art-bible §3.2's Vow-scar container, reused as a tag icon), "Equipped,"
  "In Use" (currently occupying a merge slot or the pending Feed target).
- **Stack quantity** (`item-data-schema` field `quantity`, only when `stackable = true`): a Data-
  face tabular number in the tile's corner.

**Interaction states** (art-bible §7.7's five-state chrome matrix, applied to every tile):
Available, Hover, Keyboard-Focus (a mandatory visible focus indicator, WCAG 2.4.7, per
`accessibility-settings-system` §3.7 item 1), Selected, Disabled. All five are built from value-step
and border-weight differences within the three-color chrome budget — no additional hue is
introduced for tile state (art-bible §4.5).

**Selecting a tile** (no drag, anywhere): a single confirm input (click, or gamepad
confirm-button-on-focused-tile, or keyboard Enter-on-focused-tile) performs that mode's assignment
action — placing the item in the next open merge slot, adding it to the pending disposal
multi-select, or setting it as the Vow-binding target. There is no drag-to-slot interaction
anywhere on this screen; every assignment is a discrete confirm action, satisfying the
"no drag-only interaction" requirement by construction rather than by adding a keyboard fallback
after the fact.

### 3.4 The Merge Flow

**Layout**: a 3-slot tray at the top of the Merge content area, an output preview panel beneath it,
and the filtered inventory picker (§3.3) below that.

**Compatibility filtering — incompatible items are unselectable, not merely rejected**:

1. With **zero slots filled**, the picker shows every `active`, non-Vow-bound item with
   `base_type` ∈ {`weapon`, `armor`, `charm`} (`the-forge-system` §3.2.1). Vow-bound items are
   excluded per Assumption A8 (revealable via the "Show unavailable items" toggle, disabled with a
   "Vow-bound — cannot merge" reason tag).
2. The instant the **first** slot is filled, the picker re-filters live to only items sharing that
   item's exact `base_type` and `rarity`. Every item that fails either test is removed from the
   default view; with "Show unavailable items" on, they render Disabled with a reason tag ("Different
   rarity" / "Different item type"), never as a normally-selectable tile. This is what makes an
   incompatible merge **impossible to attempt** — there is no code path where the player selects an
   incompatible third item and only then learns it was rejected.
3. Slot compatibility does **not** filter on `source` (Formula 5's Source Inheritance Roll makes a
   mixed-Source merge a normal, common, legal case — `the-forge-system` §3.2.1's explicit "not
   required"). Tiles of every Source remain selectable once `base_type`/`rarity` match.
4. An item already occupying a slot is removed from the picker entirely (not shown disabled) until
   its slot is cleared, preventing the same `item_instance_id` from ever being reachable for a
   second slot (`the-forge-system` §5 Edge Case 7).

**Assigning and clearing a slot**: selecting a compatible tile fills the first open slot,
left-to-right, and automatically advances focus to the next open slot's picker context — a
three-item merge can be built with three consecutive confirm presses and no other navigation, for
players who already know what they're merging. Selecting a filled slot directly opens a small
"Remove" affordance (a second confirm clears it); there is no separate "swap" gesture — clear, then
re-fill.

**Merge output preview** (populates only once all 3 slots hold valid items): renders in the Data
face with tabular figures throughout, and communicates uncertainty honestly rather than promising
one outcome — see Formulas §4.2–§4.4 for exactly what is computed and how "guaranteed" vs.
"candidate" entries are distinguished. The preview panel shows, top to bottom: the deterministic
output rarity (a single glyph + name, e.g. "→ Epic," per Formula 4.1), the Source Inheritance
breakdown as a percentage list (Formula 4.2), the guaranteed modifier/enchantment entries with
computed values, and the candidate-pool list for any unique attributes competing for remaining
slots (Formula 4.3), each candidate tagged with its exact inclusion likelihood — never rounded to a
vague "maybe."

**Confirmation gate**: the Confirm control described in §3.8, at the Forge's Medium ceremony tier.
The confirmation panel explicitly restates, in plain text, which three items will be archived
("These 3 items will be consumed and cannot be recovered") alongside the preview — this is the
"cost unmistakable" requirement: the player sees exactly what is destroyed and what is (probably)
gained on the same screen, before the same commit action that does both.

**No bulk/auto-merge** (Assumption A6): the mode-tab rail has no secondary "repeat this merge"
shortcut. Wanting to merge nine more items into three more outputs means filling the tray three
more times.

### 3.5 The Vow-Binding Flow (the Ceremony Moment)

**Layout**: two pickers side by side — an item picker (left, filtered per below) and the Vow
catalog gallery (right) — collapsing into a single confirmation panel once both a target item and a
Vow are pending.

**Selecting the target item**: the item picker shows every `active` item with `ability_focus = true`
(per `resonance-weaving-system` §3.4, always `base_type: charm` at MVP). Unlike Merge/Disposal, an
existing Vow-bound item is **not** excluded here — it is exactly the expected case for a rebind
(`the-forge-system` §3.6 rule 4). A tile already carrying an active Vow shows its current
`VowDefinition.display_name` as a small tag; a tile with any `superseded` entries in its
`vow_binding_log` additionally shows a compact, pale "scar history" list on hover/inspect —
`resonance-weaving-system`'s own permanence framing, surfaced as inspectable UI data rather than
rendered on the Hunter's silhouette (that render is `art-director`/`ui-programmer` territory; this
document only guarantees the data is shown somewhere legible). An "Equipped" tag appears if the
item currently occupies one of the 4 loadout slots — equip state never blocks binding.

**Selecting a Vow**: the catalog gallery lists all 10 starting `VowDefinition`s
(`resonance-weaving-system` §3.3.4) as cards. Each card shows: `display_name` (Display face),
a category icon distinguishing `gate` / `scale` / `static_cost` (Assumption A3's chrome iconography
— padlock / up-arrow / anchor), `restriction_description` (body text), and `power_multiplier` as a
prominent Data-face tabular number (e.g., "×2.28"). `expected_uptime` is never shown (Assumption
A2). Selecting a card sets it as the pending Vow — freely reversible, no cost incurred yet.

**The confirmation panel — where ceremony lives**: once both an item and a Vow are pending, the
right-hand region transforms into the binding confirmation panel, at the **High** ceremony tier
(art-bible §7.6's own named example). It shows, unambiguously, before any commit input is possible:

1. The target item's current state, including any existing scar (per above).
2. The chosen Vow's full effect and `restriction_description`.
3. **Cost**: `vow_binding_cost` — 200 Gleam + 5 `enchantment_material` (`the-forge-system` §7,
   Assumption A4 on material display) — shown against the player's current balance; if either
   component is short, the cost line and the Confirm control both render in the Disabled
   interaction state (§3.3) with an explicit text delta ("Need 45 more Gleam"), never a bare
   grayed-out button with no explanation (satisfies `accessibility-settings-system` §3.7 item 4 —
   color is never the sole differentiator).
4. **Irreversibility copy, stated plainly, before any commit input exists**: "This cannot be
   undone. If this item already carries a Vow, that scar remains, visible, beneath the new one."
   If it's a rebind, the copy is specific: names the Vow being superseded.
5. The **Bind Vow** control (§3.8's Confirm control, at High ceremony). Per art-bible §4.5's one
   explicit chrome exception, this button — and only this button, in this document — may glow
   Hearth-Gold-adjacent on hover, because pressing it *is* the mastery-tier commitment the color is
   reserved for.

**The sear itself**: per art-bible §7.3 ("the Vow-binding button can play the item's own
molten-to-cooled material transition directly on the confirmation UI before commit, rather than
being a flat 'Confirm' button that cuts to a separate cinematic") and `the-forge-system` §3.6 rule
5, the commit click **is** the point of no return — the transaction is atomic and complete the
instant the click registers, and the molten→cooled animation that follows is feedback on an
already-committed action, not a cancelable preview. Concretely: the Bind Vow control cannot be
re-clicked, re-focused-and-cancelled, or interrupted once activated; no "cancel while glyph is
mid-sear" state exists, matching the source rule exactly. This animation is **never** suppressed by
`reduced_motion_enabled` (`accessibility-settings-system` §3.4 explicitly lists ceremony-tier
motion — named there by this exact example — as never-suppressed; only ambient/decorative motion
is gated by that setting).

**Before that click**, the player may freely change the pending item, the pending Vow, or back out
entirely with zero cost and zero state change — the confirmation panel is a planning surface right
up until the single irreversible input.

### 3.6 The Disposal Flow — Sell / Dismantle / Feed (Single Item)

**Layout**: a single item picker (filtered to `active`, non-Vow-bound items with an eligible
`base_type` per the union of all three paths' eligibility — `weapon`, `armor`, `charm`,
`enchantment_material`, `creature_core`), and three destination cards presented as **equal visual
weight, side by side** — never a ranked list, never a "best value" badge on any of the three.

**Making equivalence legible, not just true**: `the-forge-system` §3.5.4 deliberately prices Sell
and Feed identically so the choice is about *which resource the player needs*, not *which pays
more*. A UI that shows "Sell: 62 Gleam / Dismantle: 2 materials / Feed: 62" in a single ranked
numeric column would visually announce "Sell and Feed are the same, Dismantle is worse" — exactly
the misread this system exists to prevent. This document's rule: **the three destinations are never
rendered on one shared numeric axis.** Each card leads with its own resource icon and name first
("→ Gleam," "→ Materials," "→ Send to Creature"), with the numeric payout secondary and styled
identically across all three cards (same size, same weight, same position within the card) so a
player scans "what do I get" before "how much," not the reverse. A persistent caption beneath the
three cards states the relationship in words, not as a one-time dismissible toast: **"Sell and Feed
always return equal value — choose based on which resource you need."** This caption is present
every time the Disposal mode is open, not a tutorial hint that disappears after one viewing.

**Feed's target-selection step**: selecting the Feed card (only offered when exactly one item is
selected — see §3.7) opens a creature-target picker from the player's roster. If the roster is
empty, the Feed card itself renders Disabled with the reason tag "No creatures in your roster yet"
(`the-forge-system` §5 Edge Case 15) — unselectable, not selectable-then-rejected.

**Confirmation gate**: the standard §3.8 pattern, at the Forge's Medium ceremony tier — a compact
confirmation panel (not a full modal for a single item) restating exactly what will be consumed and
exactly what will be returned, with a distinct Confirm/Cancel pair.

**Legendary safety net** (Assumption A7): if the item being disposed of is Legendary rarity, an
additional acknowledgment line appears in the confirmation panel ("This is a Legendary item) before
the Confirm control activates — the Confirm control itself remains Disabled until that line has
been explicitly acknowledged (a second discrete input, not a timer or an auto-check).

### 3.7 Bulk Operations

**Multi-select**: every tile in the Disposal picker gains a selection-toggle affordance (a discrete
confirm input, per §3.3 — never drag-select). A persistent selection tray shows running totals for
whichever destinations remain valid for the current selection.

**Feed is excluded from multi-select** (Assumption A5): the moment 2+ items are selected, the Feed
destination card renders Disabled with the reason tag "Feed is one item at a time" — Sell and
Dismantle remain fully available for any selection size. Selecting exactly 1 item re-enables all
three destinations, matching the single-item flow in §3.6 exactly (bulk and single-item share one
underlying flow that simply degrades gracefully at `selection_count = 1`; there is no separate
code path a player could get confused by).

**"Dismantle/Sell all below rarity X"**: a bulk-select preset control above the picker — a rarity
threshold selector plus a "Select Matching" button. Activating it populates the multi-select state
with every currently eligible (`active`, non-Vow-bound, correct `base_type` for whichever
destination is eventually chosen) item at or below the chosen rarity tier. This **never** auto-
executes a disposal — it only populates the selection, landing the player on the same review-and-
confirm panel as any other multi-select, so a misclick on the preset can be reviewed and edited
before anything is destroyed.

**Bulk confirmation panel**: the same §3.8 pattern as everywhere else, scaled to a scrollable
itemized list (reflow, not overlap, per `accessibility-settings-system` §5 Edge Case #1) once the
selection exceeds the visible-row default (§7). Each row shows the item tile in miniature plus its
per-item payout; a running total sits at the top, always visible while scrolling the list.

**Legendary safety net, bulk version** (Assumption A7): if **any** item in the current selection is
Legendary rarity, the bulk confirmation panel lists those items in a separate, visually distinct
sub-section at the top ("N Legendary items included") before the standard list, and the Confirm
control stays Disabled until that sub-section has been explicitly acknowledged — identical
mechanism to the single-item case, scaled to however many Legendaries are present.

**No ornament flex with density** (art-bible §3.4, restated as a binding rule for this screen): a
selection of 40 items renders in exactly the same chrome frame, same border weight, same lack of
stained-glass ornament as a selection of 2. The bulk list is scrollable and reflow-only different
from the single-item confirmation panel — never more richly decorated because more is at stake in
aggregate.

**Merge has no bulk equivalent** (§3.4, restated): this section applies to Disposal only.

### 3.8 The Confirmation Gate Pattern (unified across the whole screen)

One pattern, reused everywhere an irreversible action exists, scaled only by ceremony tier
(art-bible §7.6) and by whether the action is single-item or multi-item — never by a different
underlying mechanism:

1. **Build the pending action** — fill merge slots, select a disposal destination and item(s),
   select a Vow-binding target and Vow. Nothing here costs anything or mutates any state; every
   step is freely revisable.
2. **A confirmation surface appears** — inline and compact for a single-item disposal, a larger
   panel for Merge and bulk Disposal, and the High-ceremony panel described in §3.5 for
   Vow-Binding. It always restates, in plain text, what will be consumed and what will be
   returned/created, and for Legendary-inclusive disposals and Vow-binding specifically, an
   explicit irreversibility statement.
3. **A dedicated Confirm control, distinct from any selection control**, commits the transaction. A
   dedicated Cancel/Back control aborts with zero state change. **The same control is never used to
   both initiate and commit** — this deliberately avoids an "arm on first press, commit on second
   press of the same button" pattern, which is vulnerable to a single accidental double-input
   (a real risk for players with tremor or imprecise input devices) committing an irreversible
   action the player only meant to inspect.
4. **No timers, no auto-commit, no countdown** anywhere in this pattern, matching the room's "no
   timers, no urgency" mood (art-bible §2) and avoiding any interaction that could punish a slower
   input method.

This is the single mechanism every Acceptance Criterion in §8 about "irreversible action always has
a confirmation gate" is checking for — there is exactly one confirmation architecture on this
screen, not a different one per action type.

### 3.9 Keyboard & Gamepad Navigation

- **Full keyboard/gamepad operability, no exceptions** (`accessibility-settings-system` §3.7 item
  3): every interactive element on this screen — mode tabs, item tiles, slot tray, Confirm/Cancel
  controls, bulk-select toggles, the rarity-threshold preset — is reachable and operable without a
  mouse or pointer.
- **Visible keyboard-focus indicator** (WCAG 2.4.7) on every one of the above, built from
  value-step/border-weight within the chrome budget (`accessibility-settings-system` §3.7 item 1) —
  never a fourth hue.
- **Fixed logical navigation order**: header (Gleam/materials, non-interactive) → mode tabs → mode
  content region (slot tray / picker / confirmation panel, in that reading order) → Confirm/Cancel.
  Entering a confirmation surface (§3.8 step 2) automatically moves focus to that surface's first
  element (typically the irreversibility copy or the first list row), never leaving focus stranded
  on a control that just triggered a full-panel change.
- **No simultaneous multi-button input anywhere** (`accessibility-settings-system` §3.7 item 8):
  every action on this screen is a single discrete input at a time — assign-to-slot, toggle-select,
  confirm, cancel.
- **No drag-to-reorder or drag-to-assign of any kind on this screen** — restated from §3.3, because
  it is the specific pattern `accessibility-settings-system` §3.7 item 2 calls out by name as
  requiring a keyboard equivalent; this document's answer is not to add a keyboard equivalent to a
  drag pattern, but to never have a drag pattern to begin with.

---

## 4. Formulas

Unlike most of this document's siblings, the Forge UI's formula section is not thin — the merge
preview's requirement to "honestly communicate uncertainty, not promise a specific result" needs
real, derived-for-display math beyond a simple restatement of `the-forge-system`'s formulas. Four
items are covered: two are direct citations (no new math), two are genuine display-layer
derivations this document originates. Nothing here recomputes or overrides a value
`the-forge-system` already owns — every display value is downstream of that document's Formulas
1–8, cited by exact name.

### 4.1 Merge Output Rarity — Direct Citation, No Uncertainty to Display

The preview's rarity line is `the-forge-system` Formula 2 (`output_rarity_tier_index = min(4,
input_rarity_tier_index + 1)`), evaluated the instant all 3 slots hold valid, same-rarity inputs.
This is fully deterministic given three valid slots — there is no roll, no probability, and no
range to display. The preview renders it as a single fixed value (e.g., "→ Epic"), never as a
probability distribution, because presenting a deterministic value with false uncertainty would be
as dishonest as the reverse.

### 4.2 Source Inheritance Display — Direct Citation

Directly displays `the-forge-system` Formula 5's distribution:

```
P(output_source = S) = count(inputs where source == S) / 3
```

Rendered as a descending-sorted percentage list, each row pairing the candidate Source's exact
§4.2 glyph medallion with a Data-face tabular percentage.

**Worked example** (reusing `the-forge-system` §4.5's own worked example, for direct
cross-reference): inputs' sources `[body, body, machine]` → the preview shows two rows: "Body —
66.7%" and "Machine — 33.3%." If all three inputs share one Source, the preview shows a single row
at "100%" — the general case, not special-cased in the UI any more than the underlying formula
special-cases it.

### 4.3 Unique-Attribute Candidate Inclusion Likelihood — New Display Formula

`the-forge-system` Formula 4 (§4.4) fills modifier/enchantment slots by ranking shared-stat entries
first, then drawing the remainder from a unique-attribute pool "without replacement... uniform
random order" whenever that pool exceeds the number of open slots remaining. This document adds the
one piece of math needed to display that fairly: since a uniform-random draw of a fixed-size subset
from a pool of distinct candidates gives every candidate in the pool an identical marginal
probability of inclusion, that probability is exactly computable and worth surfacing, rather than
hand-waved as "maybe."

```
candidate_inclusion_probability = open_slots_remaining_after_shared / unique_candidate_pool_size
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `open_slots_remaining_after_shared` | int | ≥ 0 | Output slot cap minus however many slots shared-stat/shared-enchantment entries already claim (`the-forge-system` Formula 4 step 2/4). |
| `unique_candidate_pool_size` | int | ≥ 0 | Count of distinct stats (or enchantments) present on exactly one of the 3 inputs. |
| `candidate_inclusion_probability` | float | [0, 1] | Exact marginal probability a given unique candidate appears in the final output, under a uniform random draw-without-replacement. |

**Why this is exact, not an approximation**: for a uniform random subset of size *k* drawn without
replacement from *n* symmetric candidates, each candidate's marginal inclusion probability is
exactly `k/n` — a standard, well-known combinatorial identity, not a heuristic. When
`unique_candidate_pool_size <= open_slots_remaining_after_shared`, every candidate is guaranteed
(`candidate_inclusion_probability = 1`, displayed as a plain guaranteed entry with no likelihood
tag at all, rather than a "100%" label that would visually clutter the common, non-uncertain case).

**Worked example** (constructed to exercise the genuinely-uncertain case, since
`the-forge-system`'s own worked example in §4.4 happens to land in the guaranteed case): three Epic
charms (`rarity_tier_index = 3`) merge into a Legendary output
(`modifier_slot_count(Legendary) = 5` at default tuning). Inputs' modifiers: A =
`{attack_power +8, crit_chance +0.05, move_speed +3}`, B = `{attack_power +10, max_health +20}`,
C = `{attack_power +9, defense +5, resonance_generation +2}`.

- `attack_power` is shared across all 3 → guaranteed, `avg(8,10,9) × 1.25 = 11.25`, claims 1 slot.
- Remaining unique candidates: `crit_chance`, `move_speed`, `max_health`, `defense`,
  `resonance_generation` — pool size 5.
- `open_slots_remaining_after_shared = 5 − 1 = 4`.
- `candidate_inclusion_probability = 4 / 5 = 80%` for **every** one of the 5 unique candidates.

The preview panel shows one guaranteed row (`attack_power: 11.25`) and five candidate rows, each
labeled "80% chance" — never arbitrarily picking 4 of the 5 to display as if they were the certain
outcome. This is the concrete mechanism satisfying the brief's "the preview must honestly
communicate uncertainty, not promise a specific result."

**Enchantments** use the identical formula, substituting `enchantment_slot_count(rarity)` (the
same Formula 2 import `the-forge-system` itself uses) and grouping by `enchantment_definition_id`
instead of `stat`.

### 4.4 Bulk Operation Value Totals — New Display Formula (Trivial, Stated for Completeness)

```
Total_display(op) = Σ over selected items i of [per-item formula for op](item_i)
```

Where `[per-item formula for op]` is `the-forge-system` Formula 6 (`sell_value`) for Sell, or
Formula 7 (`material_yield`) for Dismantle — Feed is never summed, since it is never a bulk
destination (§3.7). This is a plain summation with no independent logic of its own; stated here
only because §7 tuning and §8 acceptance criteria reference it by name.

**Worked example**: a 12-item Sell selection with individual `sell_value`s summing to 540 → the
selection tray and confirmation panel both show "Total: 540 Gleam," computed live as items are
added to or removed from the selection.

---

## 5. Edge Cases

1. **Fewer than 3 compatible items exist for a merge.** With 0 or 1 slots filled and no valid
   third item in the player's inventory, the Confirm control never activates (it requires 3 filled
   slots to exist at all) and the picker itself shows an inline message once a slot constrains it
   ("Need 2 more matching Rare Weapons — you have 0"). If the player's entire inventory has zero
   `weapon`/`armor`/`charm` items in any quantity, the picker shows a full empty state instead of an
   empty grid ("No compatible items yet — merge needs 3 matching items of the same rarity and
   type").
2. **An item in a merge slot is Vow-bound.** Cannot happen — Vow-bound items never appear in the
   Merge picker at all (§3.4, Assumption A8), so there is no slot state to reach where this could
   occur. This is the "impossible to attempt" requirement applied directly.
3. **The player's inventory is empty.** Every mode's content area shows its own empty state rather
   than an empty grid: Merge — "Your inventory is empty." Disposal — "Nothing to process yet."
   Vow-Binding's item picker — "You have no ability-focus items to bind a Vow to." The mode-tab
   rail itself remains fully navigable in every case; only the content region changes.
4. **A bulk-dismantle (or bulk-sell) selection would destroy a Legendary.** Handled by the bulk
   Legendary safety net (§3.7, Assumption A7) — the confirmation panel surfaces the Legendary items
   in their own acknowledgment sub-section and the Confirm control stays Disabled until
   acknowledged. This applies identically whether the Legendary was added via manual multi-select or
   via the "Select Matching" rarity-threshold preset.
5. **Gleam or materials are insufficient for a Vow binding.** The cost line and the Bind Vow control
   both render Disabled with an explicit shortfall amount (§3.5) — never a silent failure on click,
   and never color-only (a text delta accompanies the disabled state, satisfying
   `accessibility-settings-system` §3.7 item 4).
6. **A player attempts to select a fourth item after 3 merge slots are already filled.** The picker
   in Merge mode only ever exposes assignment to *open* slots (§3.4); once all 3 are filled, tile
   selection is repurposed to "inspect" (no assignment target exists) until a slot is cleared —
   there is no fourth-slot state to enter.
7. **The Feed destination is attempted with 2+ items selected.** Cannot occur — the Feed card
   renders Disabled the instant selection count exceeds 1 (§3.7, Assumption A5), before any
   destination click is possible.
8. **The player's creature roster is empty when attempting to Feed.** The Feed card renders
   Disabled with the reason "No creatures in your roster yet" (§3.6,
   `the-forge-system` §5 Edge Case 15) — unselectable at the destination-card level, never
   reachable to a target-picker that then has nothing to show.
9. **A merge input becomes invalid between selection and confirmation** (e.g., a hypothetical
   future concurrent-action scenario; not reachable in the current single-player, single-session
   design, but specified for forward robustness per `the-forge-system` §5 Edge Case 8's own atomic
   re-validation guarantee). **Ruling**: if the underlying transaction is rejected atomically at
   commit time, the confirmation panel closes, all 3 slots return to their prior filled state
   (nothing is silently emptied), and an inline message states "Nothing was lost — try again,"
   matching the owning system's guarantee that a failed merge never partially archives an input.
10. **`ui_scale_percent` is set to 200% on the bulk Disposal confirmation panel.** Per
    `accessibility-settings-system` §5 Edge Case #1, the itemized list reflows to vertical scroll
    rather than overlapping or clipping rows — the running total at the top of the panel remains
    pinned and visible regardless of scroll position.
11. **`reduced_motion_enabled` is on during a Vow-binding.** The molten-to-cooled sear is never
    suppressed (it is explicitly named as ceremony-tier, always-on motion in
    `accessibility-settings-system` §3.4); only the surrounding menu slide/fade transitions into and
    out of Vow-Binding mode are suppressed under that setting, per that document's own suppression
    list.
12. **A rebind targets an item that already has 2+ superseded Vow entries in its
    `vow_binding_log`.** The scar-history hover/inspect view (§3.5) lists all superseded entries,
    oldest to newest, with no depth limit — `item-data-schema`'s log is append-only with no
    cardinality cap, so this document imposes none on its own display either.
13. **The lineage tooltip on a merge-output item resolves a parent that was itself a prior merge
    output** (multi-generation lineage, `the-forge-system` §5 Edge Case 16). Resolved per
    Assumption A10: the default tooltip shows immediate parents only; "View full lineage" expands
    to the recursive chain, gracefully degrading to an "Unknown" placeholder for any link that fails
    to resolve (`item-data-schema` §5 Edge Case 5).

---

## 6. Dependencies

**Depends on** (bidirectional — each cited document either already lists `forge-ui` as a dependent
or is cited here by exact field/formula name, never redefined):

- **`the-forge-system`** — every operation (merge, sell, dismantle, feed, Vow-bind), every cost and
  conversion-rate constant, and Formulas 1–8 this document's own Formulas §4 build on top of. That
  document's own Dependencies section (§6) already lists `forge-ui` as depended-on-by, closing the
  bidirectional link from its side.
- **`item-data-schema`** — every field this document renders on an item tile or in a confirmation
  panel (`base_type`, `rarity`, `source`, `modifiers`, `enchantments`, `ability_focus`,
  `vow_binding_log`, `merged_from`, `quantity`/`stackable`, `status`), cited by exact name
  throughout §3.
- **`accessibility-settings-system`** — every binding requirement in its §3.7 is treated as
  non-negotiable here (§3.9 restates the ones most load-bearing for this specific screen); that
  document's own Dependencies section already names `forge-ui` as a depended-on-by system requiring
  it to "implement the keyboard-focus indicator, non-drag keyboard equivalents,
  `ui_scale_percent` responsiveness... `high_contrast_mode`'s value-step widening,
  `colorblind_preview_mode`'s compatibility." This document fulfills that obligation from its own
  side.
- **`resonance-weaving-system`** — the 10-entry `VowDefinition` catalog (§3.3.4 of that document)
  populates the Vow-Binding gallery (§3.5) directly; `power_multiplier`, `display_name`,
  `restriction_description`, and `category`/`effect_mode` are all rendered by exact field name,
  never re-derived. That document does not currently name `forge-ui` in its own Dependencies
  section — flagged below as a gap, not self-corrected (file discipline).
- **`design/art/art-bible.md`** — §2 (mood), §3.2/§3.4 (glyph and chrome shape grammar), §4.2/§4.3/
  §4.5 (color rules, including the one chrome exception this document exercises at §3.5), §5.2 (Vow
  scar permanence framing), §7.1–§7.7 (typography, iconography, motion, diegetic/screen-space split,
  ornamentation tier table, accessibility requirements) — the primary source of truth this document
  specifies against throughout.

**Depended on by**: none. Per `systems-index.md`'s dependency map, `forge-ui` is a Presentation-tier
leaf node — no other GDD in the current systems index lists `forge-ui` as a dependency.

**`systems-index.md` gap flagged, not self-edited** (per this session's file discipline, matching
the precedent set by `accessibility-settings-system.md` and `resonance-weaving-system.md` for their
own equivalent gaps): `systems-index.md`'s Dependency Map currently lists `forge-ui` as depending
only on `the-forge-system`. This document establishes three additional real dependencies —
`item-data-schema`, `accessibility-settings-system`, and `resonance-weaving-system` — none of which
are currently reflected there. Flagged for a future centralized `systems-index.md` update, not
corrected here.

---

## 7. Tuning Knobs

| Knob | Default | Safe Range | Affects | Notes |
|---|---|---|---|---|
| `confirmation_panel_visible_rows` | 6 | 4–10 | How many item rows show in a bulk confirmation panel before it becomes scrollable (§3.7) | Below 4, even small bulk batches scroll immediately, adding friction to routine cleanup; above 10, the panel risks pushing the Confirm control off-screen on smaller windows |
| `legendary_disposal_confirmation` | on (**locked**) | not tunable | Whether any Legendary-inclusive Sell/Dismantle/Feed (single or bulk) requires the extra acknowledgment step (§3.6, §3.7, Assumption A7) | Structural safety-net rule, not a balance value — turning it off is a UX regression, not a legitimate retune |
| `show_unavailable_items_default` | off | off / on | Whether Vow-bound and rarity/type-mismatched items are hidden or shown-disabled by default in item pickers (Assumption A8) | Recommend revisiting toward "on" if playtesting shows players are confused by items silently disappearing from Merge/Disposal pickers after a Vow bind |
| `merge_slot_auto_advance` | on | off / on | Whether filling a merge slot automatically advances picker focus to the next open slot (§3.4) | Off would require an extra explicit input per slot; kept on by default for the fast, deliberate 3-in-a-row flow the Player Fantasy (§2) wants for a considered action, without adding friction to the *composition* of the merge (friction is reserved for the confirmation gate itself, not for building the pending state) |
| `bulk_rarity_threshold_default` | none pre-selected | any of the 5 rarity tiers, or none | The rarity dropdown's starting value on the "Select Matching" preset (§3.7) | Defaulting to none-selected forces an explicit choice rather than risking a player accepting a pre-filled threshold they didn't examine — a small, deliberate friction point given the preset's bulk-destructive potential |

No numeric merge/disposal/Vow-binding cost or conversion-rate values are re-listed here — every
economic tuning knob (`sell_conversion_rate`, `dismantle_conversion_rate`, `feed_conversion_rate`,
`vow_binding_cost`, `merge_scaling_factor`, etc.) is owned entirely by `the-forge-system` §7 and
cited by name in this document's Formulas (§4) and Detailed Rules (§3) rather than duplicated —
duplicating them here would create exactly the drift risk `the-forge-system` itself warns against
for its own derived values.

---

## 8. Acceptance Criteria

| # | Criterion | Verification Method |
|---|---|---|
| AC1 | The entire Forge UI — all three modes, every picker, every confirmation panel — is fully operable with a keyboard only, with no functionality reachable exclusively via mouse hover or click. | Scripted or manual walkthrough completing one full merge, one Vow-binding, one single-item disposal, and one bulk disposal, using keyboard input exclusively. |
| AC2 | The entire Forge UI is fully operable with a single connected gamepad only, with no functionality reachable exclusively via keyboard or mouse. | Identical walkthrough to AC1, gamepad-only, no keyboard or mouse connected. |
| AC3 | No interaction anywhere on this screen requires drag-and-drop; every assignment (merge slot fill, multi-select toggle, Vow-binding target selection) is achievable via a single discrete confirm input. | Static review of every interactive element's input handler confirming no drag-start/drag-end event pair is the sole path to any state change. |
| AC4 | An incompatible merge combination (mismatched `base_type` or `rarity`, or a Vow-bound item) can never be attempted — the picker excludes or disables the incompatible item before selection, not after. | Test: fill slot 1 with a Rare weapon; assert every non-Rare and non-weapon item, and every Vow-bound item, is absent from the default picker view or rendered Disabled and non-actionable when "Show unavailable items" is on; assert no input sequence results in an incompatible item occupying slot 2 or 3. |
| AC5 | Every irreversible action (Merge, Vow-Bind, single-item Sell/Dismantle/Feed, bulk Sell/Dismantle) passes through the unified confirmation gate (§3.8) — a distinct Confirm control, separate from the initiating selection control, with a Cancel path available at zero cost up until that control is activated. | Per-action test asserting: (a) no state mutation occurs before the dedicated Confirm control is activated, (b) Cancel at any prior point results in zero state change, (c) the initiating control and the Confirm control are distinct UI elements. |
| AC6 | The Vow-binding commit is atomic and non-cancelable once activated — no input during the molten-to-cooled sear animation aborts or alters the already-committed transaction. | Test: activate Bind Vow, attempt to send a Cancel/Back input mid-animation; assert the `VowBindingEntry` write already occurred and is unaffected. |
| AC7 | Sell and Feed are never presented with a ranking indicator, "best value" badge, or shared numeric axis that visually implies one outpays the other; the equivalence caption is present every time Disposal mode with a valid selection is open. | Visual/structural review of the Disposal mode's destination-card layout and the persistent caption's presence across multiple sessions (not a dismissible one-time tooltip). |
| AC8 | Feed is never offered as a bulk-select destination; the Feed card renders Disabled the instant a second item is added to the current disposal selection, and re-enables when the selection returns to exactly 1. | Test: select 1 item, assert Feed enabled; select a 2nd item, assert Feed Disabled with a reason tag; deselect back to 1, assert Feed re-enabled. |
| AC9 | Any Sell, Dismantle, or Feed action (single-item or bulk) that includes at least one Legendary-rarity item requires an explicit additional acknowledgment before its Confirm control activates. | Test matrix: single Legendary item, bulk selection containing exactly 1 Legendary among others, bulk selection containing multiple Legendaries — assert Confirm stays Disabled in all three until the acknowledgment sub-section is explicitly interacted with. |
| AC10 | Every interactive element has a visible keyboard-focus indicator built from value-step/border-weight within the three-color chrome budget, with no additional hue introduced for focus state. | Static/visual review of every control's Keyboard-Focus interaction state against the three-color chrome budget (art-bible §4.5). |
| AC11 | The merge preview's modifier/enchantment display never shows more "guaranteed" entries than the true guaranteed count computed by `the-forge-system` Formula 4, and every non-guaranteed entry displays its exact `candidate_inclusion_probability` (§4.3), never an unlabeled or vague uncertainty indicator. | Test using the §4.3 worked example (three Epic charms → Legendary): assert exactly 1 guaranteed row (`attack_power`) and exactly 5 candidate rows each labeled "80%." |
| AC12 | The bulk confirmation panel's running total always equals the exact sum of the selected items' individual per-item formula results (Formula 4.4), recomputed live as items are added to or removed from the selection. | Test: build a selection incrementally, asserting the displayed total matches `Σ sell_value_i` (or `Σ material_yield_i`) after each add/remove. |
| AC13 | Given `ui_scale_percent = 200`, no item picker, confirmation panel, or bulk list overlaps, clips, or silently truncates content — dense lists reflow to vertical scroll instead. | Layout test at maximum `ui_scale_percent` across all three modes' primary content regions, per `accessibility-settings-system` §5 Edge Case #1's reflow rule. |
| AC14 | With `colorblind_preview_mode = grayscale` enabled, every mechanically important state on this screen (rarity tier, Vow-bound status, disabled/available item state, insufficient-cost state) remains fully distinguishable by shape, text, or icon alone. | QA pass through Merge, Disposal, and Vow-Binding modes with the grayscale preview active, confirming no state depends on hue to be understood. |
| AC15 | `reduced_motion_enabled` suppresses this screen's menu/mode-transition motion but never the Vow-binding molten-to-cooled sear. | Test: enable `reduced_motion_enabled`, switch between mode tabs (assert transition motion suppressed), then perform a Vow-binding (assert the sear animation plays in full). |
| AC16 | With inventory empty, fewer than 3 compatible merge candidates present, or an empty creature roster, the affected mode/control shows an explicit empty-state message rather than a blank or silently-disabled control with no explanation. | Fixture test for each of the three empty conditions (§5, Edge Cases 1, 3, 8), asserting a human-readable message is present in each case. |

---

## Cross-System Facts Proposed for Registration

None. This document introduces no new gameplay formula, currency, enum, or cross-system constant
of its own — every numeric value it displays (`sell_value`, `material_yield`,
`feed_contribution_value`, `vow_binding_cost`, `power_multiplier`, rarity ring-counts, Source
colors) is cited from `the-forge-system`, `item-data-schema`, `resonance-weaving-system`, or
`design/art/art-bible.md` by exact name, never redefined. The two genuinely new formulas this
document does originate (§4.3's `candidate_inclusion_probability`, §4.4's bulk total summation) are
UI-presentation derivations with no consumer outside this screen — no other system in
`systems-index.md` renders a merge preview or a bulk disposal total, so there is nothing here that
would benefit from central registration. This document's new Tuning Knobs (§7) are likewise
UI-only presentation/friction settings, not gameplay-balance constants any other system would need
to look up.
