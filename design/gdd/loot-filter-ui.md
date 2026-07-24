# GDD: Loot Filter UI

## Document Status

- **Status**: Complete — all 8 sections authored
- **Created**: 2026-07-14
- **Owned By**: ux-designer
- **Review Mode**: `lean` — authored autonomously this session (no user available to approve
  section-by-section; matching the precedent set this session by `loot-drop-system.md`,
  `item-data-schema.md`, `accessibility-settings-system.md`, and `forge-ui.md`). Every ambiguity
  the source material left open has been resolved with an explicit, flagged design call rather
  than a placeholder. Validate at `/ux-review` and `/gate-check` before Production.
- **Priority**: MVP, Presentation tier — 21st in the recommended design order
  (`design/gdd/systems-index.md`), depending on `loot-drop-system` (12th). Per that system's own
  scope split, this document owns the **interface** exclusively — the rule schema, evaluation
  semantics (first-match-wins, ascending priority, AND-within-a-rule, default `keep`, the
  Legendary/Epic safety net) belong entirely to `loot-drop-system` and are cited by exact name
  throughout, never re-derived or re-litigated.

## Source Material Read

`design/gdd/loot-drop-system.md` (full — §3.9's `LootFilterRule`/`FilterCondition` schema and
§3.9.2–§3.9.3's evaluation/safety-net semantics are this document's entire foundation, cited by
exact field name throughout), `design/gdd/item-data-schema.md` (full — every Item Instance field a
condition can test), `design/gdd/accessibility-settings-system.md` (full — its binding
requirements, §3.7, are non-negotiable constraints on this screen; §3.6's `simplified_ui_density`
delegation to "that screen's own future GDD" is resolved here), `design/gdd/forge-ui.md` (full —
the sibling UI whose item-tile pattern, confirmation-gate philosophy, and keyboard/gamepad
navigation conventions this document reuses wherever the two screens share a problem, and
deliberately diverges from where the two screens' stakes differ — see Assumption A4),
`design/art/art-bible.md` (§2, §3.2, §3.4, §4.2, §4.3, §4.5, §4.6, §7.1, §7.2, §7.3, §7.6, §7.7,
§9.6 — §3.4/§7.6 lock this screen at zero ceremony, §7.2 locks loot-filter logic icons to chrome
style even though they're game-specific, §7.7 binds the non-drag reorder requirement, §9.6 is a
direct design brief: prove density without PoE's memorization tax), `design/gdd/game-concept.md`
(Pillar 3, Pillar 4, Anti-Pillars), `design/gdd/systems-index.md` (this system's entry and
dependency map).

## Assumptions Log (resolved this session, no placeholders left below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | **Shadow detection is two-tier: Tier 1 (exact literal condition-set containment) is always-on and guaranteed sound; Tier 2 (same-field comparator-aware range subsumption) is a best-effort enhancement with no false positives but possible false negatives.** Neither is a general arbitrary-logic solver. | Fully general "will rule N ever fire" detection across arbitrary open-key fields and mixed comparators is a much harder problem than this task needs solved — it can shade into combinatorial/undecidable territory for pathological rule sets. A sound, cheap, real-time detector for the mistakes players actually make (duplicate rules, an over-broad earlier rule, a misplaced catch-all) delivers the task's actual value ("make silent non-firing visible") without over-scoping into a constraint solver this document has no mandate to design. Flagged explicitly as a known limitation, not hidden as a completeness claim. |
| A2 | **The visible "priority" field is a UI-maintained, contiguous `1..N` sequence** (a "move to position" control), never a raw pass-through of an arbitrary integer. | `loot-drop-system` §3.9.1 explicitly defers tie-breaking: "ties are a `loot-filter-ui` authoring-time validation concern, not resolved here." Enforcing strict contiguity at the UI layer makes ties and gaps structurally impossible to create, rather than validating against them after the fact. |
| A3 | **Action iconography (Keep / Auto-Sell / Auto-Dismantle) and all loot-filter logic controls (AND label, comparator symbols, move/reorder icons, the catch-all warning icon) render in chrome style**, per art-bible §7.2's explicit carve-out ("loot-filter logic icons... stay in chrome style" even though they're bespoke to this game). Where an action icon needs to reference a resource/destination (Auto-Sell → Gleam, Auto-Dismantle → materials), it reuses the exact established world-object silhouettes `forge-ui` §3.3 already defined for those resources rather than inventing new ones. | Direct application of §7.2's own design test ("would this icon still mean the same thing bolted onto a completely different game?") and §3.4's "content reuses grammar unmodified, chrome never borrows stained-glass ornament" split. |
| A4 | **Rule deletion and preset-replace use a lightweight inline-confirm-plus-Undo-toast pattern, not `forge-ui`'s full ceremony confirmation gate** (§3.8 there). | `forge-ui`'s gate exists because its actions destroy game items — economically real and fictionally irreversible. A loot-filter rule is configuration, not an item; deleting one is trivially reversible (recreate it, or Undo). Gating it behind the same weight as destroying a Legendary would misrepresent the actual stakes and add exactly the friction art-bible §3.4/§7.6 forbid on a zero-ceremony density screen. |
| A5 | **Value pickers for glyph-grammar condition fields (rarity, source) reuse the exact ring-frame and Source-medallion assets from art-bible §3.2/§4.2/§4.3**, matching `forge-ui` §3.3's item-tile precedent — never a re-drawn or simplified icon set. | Direct application of §3.4/§7.2's content-reuse rule, and this document's concrete answer to §9.6's explicit warning against PoE's "text-first, memorization-first route to density." A container-tells-you-category shape grammar is used for condition values, not stacked color-coded text tags. |
| A6 | **Preset content is specified here at the level of design intent and rule shape, not final numeric content.** Five presets are locked by name, purpose, and rule shape; their internal rarity/type thresholds are illustrative, not final tuning. | Matches `item-data-schema`/`loot-drop-system`'s own established precedent of deferring exact content-catalog values to a future content-authoring pass — this document is Presentation-tier, not a content-authoring pass. |
| A7 | **`simplified_ui_density`'s primary/advanced column split for this screen — delegated by `accessibility-settings-system` §3.6 to "that screen's own future GDD" — is resolved here**: primary tier = priority, enabled toggle, one-line condition summary, action, unreachable badge. Advanced tier (hidden by default under this setting) = the full condition editor, the exact items-affected count (replaced by a coarse bucket at primary tier — "few" / "some" / "many" / "most"), the shadow-detail breakdown, and the drag handle. | Fulfills the exact obligation `accessibility-settings-system` left open, and gives `ui-programmer` an unambiguous field-by-field spec instead of a vague "simplify it" instruction — matching this session's own no-hand-waving standard. |
| A8 | **This document adds one UI-only, optional field not present in `loot-drop-system`'s `LootFilterRule` schema: `rule_label`** (string, player-authored, optional, search/recognition only — never read by the evaluation engine). Stored as UI-layer metadata keyed by `rule_id`, additive to and never a modification of that document's schema. | A list of a dozen-plus rules becomes materially harder to scan by condition-summary alone once several rules are structurally similar (three different rarity-threshold sell rules, say). A short player-chosen label is a standard, low-cost density mitigation directly serving Design Task 6 (scale). Kept as UI-only metadata specifically to respect this session's file discipline against editing `loot-drop-system.md`. Flagged as a forward coordination note for whichever system implements persistence. |

---

## 1. Overview

The Loot Filter UI is the player-facing authoring and comprehension surface for
`loot-drop-system`'s Loot Filter Rule Engine (§3.9 there) — the interface where a player builds,
reorders, tests, and trusts the ordered list of `LootFilterRule`s that engine evaluates
first-match-wins against every item their kills (active or automated) produce. Per art-bible
§3.4/§7.6, this is the game's **maximum-density, zero-ceremony utility screen** — the direct
subject of both §3.4's "never flexes, regardless of how important the configuration decision is"
rule and §9.6's explicit brief to prove that density, done right, does not require Path of
Exile's tens-of-hours memorization tax. This document owns five things: the **rule builder**
(condition rows, action selection, enable/disable, and full non-drag-first reordering per art-bible
§7.7's binding requirement); a **structural shadow-detection system** that makes first-match-wins'
single most notorious failure mode — a rule that silently never fires because an earlier rule
already caught its items — visible automatically, without the player needing to notice it
themselves; a **live test panel** that lets a player trace exactly which rule would catch any real
or hypothetical item, and why; a **preset gallery** that lets a first-time player reach a working
filter without ever learning the rule vocabulary; and the **surfacing** (not the logic — that is
`loot-drop-system`'s) of the hard Legendary/togglable-Epic safety net, so that trust in this screen
is never a leap of faith.

---

## 2. Player Fantasy

`loot-drop-system`'s own Player Fantasy (§2 there) already named this feeling once: *"the quieter
satisfaction of a filter doing its job while you're away... a small act of delegated trust... the
player isn't passively watching numbers move, they authored the policy the numbers are now
obeying."* This document exists to make that trust earnable in under two minutes and unbreakable
afterward — because this screen's honest, stated job is to be **opened, configured once (or
never, if a preset already fits), and then largely forgotten.** Every design decision below is in
service of that: a player should be able to close this screen and go fight something, confident
that the small policy they wrote is quietly, correctly sorting their inventory in the background,
without a single moment of "wait, did I just tell it to sell my Legendary?"

That confidence is the entire point, and it is exactly the thing badly-designed loot-filter
tooling gets wrong. Art-bible §9.6 names Path of Exile as the industry's own cautionary tale here
— not because its density is wrong (density is, per that section, *achievable*), but because a
filter tool that only becomes legible after tens of hours of memorization is a tool players end
up fearing rather than trusting, reaching for third-party sites to translate what their own game
won't show them plainly. This document's answer to that fear has three concrete parts, each
directly addressed in Detailed Rules below: the fear of **losing an item you didn't mean to** is
answered by a safety net that is surfaced constantly, not just enforced silently (§3.6); the fear
of **a rule you wrote quietly doing nothing** is answered by shadow detection and a live test
panel that make first-match-wins' one real trap visible before it costs anything (§3.5); and the
fear of **not knowing where to even start** is answered by a preset gallery that is the very first
thing a new player sees (§3.7). A player who never authors a single custom rule and only ever
taps one preset button should still feel this screen worked *for* them, exactly once, and then
got out of the way.

---

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This document owns: screen layout and ceremony tier, the rule-row interaction pattern (build,
edit, enable/disable, duplicate, delete), the condition-editor's field/operator/value vocabulary
as *exposed to the player* (never redefining the underlying schema), all three reordering input
paths, the shadow-detection display and its computation (a new, UI-owned algorithm — §4 Formula
1), the live test panel, the preset gallery, and search/collapse/grouping affordances at scale. It
explicitly does **not** own, and defers to the cited document in every case: the `LootFilterRule`/
`FilterCondition` schema shape and the evaluation semantics themselves (first-match-wins, AND
within a rule, default `keep`, the Legendary/Epic safety net — all `loot-drop-system` §3.9, cited
by exact name, never redefined), what an Item Instance field means or is bounded by
(`item-data-schema`), the settings surface and binding requirements this screen must honor
(`accessibility-settings-system`), and pixel layout, exact colors, or implementation
(`art-director`/`ui-programmer` territory).

### 3.1 Screen Architecture & Persistent Elements

**Ceremony tier: None**, cited directly from art-bible §7.6's own table ("Loot filters — None —
Same [as automation config] — named directly in 3.4") and §3.4's "high-density utility screens...
never [flex], regardless of how important the decision being made there is." Nothing on this
screen — not the preset gallery, not the safety-net banner, not a rule that would sell a
Legendary if it could — ever earns heavier frame ornament than any other rule row. Importance here
is carried entirely by glyph content and by the shadow/safety-net badges, never by decoration
(§3.4's own design test, applied literally).

**Always visible, in every state of this screen** (a fixed-height header, never scrolling out of
view — matching `forge-ui` §3.2's persistent-header precedent):

- **Rule count summary** — Data-face tabular text: `"N rules, M enabled"`. When `N = 0` or
  `M = 0`, this reads `"0 rules — every item is kept automatically"` (Edge Case 4/6).
- **Safety-net statement** — a chrome lock-shield glyph + static text, **always present, never
  dismissible, never conditional on the current rule set**: *"Legendary items are never sold or
  dismantled — no rule can override this."* (§3.6.)
- **Epic-protection toggle** — a labeled chrome switch, `"Also protect Epic items"`, reflecting
  `loot-drop-system`'s `epic_auto_disposal_lock_default` (§3.6).
- **Presets** — a chrome button, always reachable, opening the Preset Gallery (§3.7) regardless of
  how many custom rules already exist.
- **Search field** — chrome text input (§3.8).
- **Test Panel toggle** — a chrome button opening/closing the collapsible Test Panel drawer (§3.5).

**Main content region** toggles between exactly two states, switched by a single chrome tab-like
control (never a mode requiring separate navigation depth, matching `forge-ui` §3.2's dedicated
mode-switch precedent): **Rule List** (the dense table, §3.2–§3.4) and **Preset Gallery** (§3.7).
The Preset Gallery is the **default** state whenever the player's rule set is empty (Edge Case 4);
Rule List is the default the instant at least one rule exists. Switching between the two is
instant, no transition motion, per art-bible §7.3's utility-screen motion rule ("instant or
near-instant state changes on scroll, sort, filter toggle... motion here is friction, not
delight").

### 3.2 The Rule Row — Shared Pattern (Collapsed and Expanded States)

Every rule renders from one shared row pattern, so a player learns it once and reads a dozen rules
the same way they read one.

**Collapsed (default) state**, left to right:

1. **Move-up / move-down** — two small chrome chevron-icon buttons, always visible (never
   hover-only), each independently focusable and activatable (§3.4).
2. **Priority** — a Data-face, tabular-figure number, itself an editable field (§3.4). This is the
   contiguous `1..N` position (Assumption A2), not a raw arbitrary integer.
3. **Enabled toggle** — a chrome checkbox/switch. Off renders the **entire row** at a dimmed
   value-step (Cold Slate-shifted, within the three-color chrome budget, art-bible §4.5) plus an
   explicit text tag `"Disabled"` — never color-only, satisfying `accessibility-settings-system`
   §3.7 item 4.
4. **Condition summary** — a single line combining glyph-grammar chips (a rarity ring-frame swatch
   + text for a rarity condition, a Source medallion + text for a source condition — Assumption
   A5) and chrome text for open-key fields, joined by an explicit, always-rendered **"AND"** chrome
   label between each condition (§3.8's PoE-avoidance rule: logic is always spelled out, never
   implied by mere adjacency). Truncates to a `"+N more"` chip past the row's available width;
   clicking/focusing it expands inline rather than truncating silently.
5. **Action badge** — chrome icon + text: a checkmark for **Keep**, the Gleam resource silhouette
   (reused from `forge-ui` §3.2, Assumption A3) + text for **Auto-Sell**, the material-vial
   silhouette (same reuse) + text for **Auto-Dismantle**.
6. **Items-affected count** — Data-face tabular number (§4 Formula 2), e.g. `"142 items"`. Under
   `simplified_ui_density`, this is replaced by a coarse bucket (`"few" / "some" / "many" /
   "most"`, Assumption A7).
7. **Unreachable badge** (§3.5) — present **only** when Formula 1 flags this rule as fully
   shadowed: a chrome broken-link glyph + text `"Unreachable"`. Absent entirely, not merely hidden,
   when the rule is reachable — no empty badge slot rendered for the common case.
8. **Row actions** — chrome icon buttons: **Edit** (expands the row, §3.2 below), **Duplicate**
   (clones this rule immediately after itself with a fresh `rule_id`, letting a player clone a
   working rule and tweak one field rather than rebuilding from scratch — a direct, low-cost
   authoring-friction reducer for a screen expected to host dozens of rules), **Delete** (§3.3).

**Interaction states** (identical five-state matrix to `forge-ui` §3.3, reused verbatim): Available,
Hover, Keyboard-Focus (mandatory visible indicator, WCAG 2.4.7), Selected, Disabled — all built
from value-step and border-weight within the three-color chrome budget, no additional hue
(art-bible §4.5, `accessibility-settings-system` §3.7 item 1).

**Expanded (edit) state**, opened via the Edit action or by adding a new rule (§3.3): the row grows
in place (accordion, never a separate modal — this screen never leaves its own single dense list,
matching art-bible §7.4's "abstracted planning interfaces... chrome-heavy UI is the right tool
there" without introducing a second navigation layer) to show:

- One **condition row** per `FilterCondition`, each a field picker → operator picker (only the
  operators valid for the selected field are offered — an invalid field/operator pairing is
  structurally impossible to construct, matching `forge-ui`'s "impossible to attempt, not merely
  rejected" philosophy) → a value picker whose shape depends on the field (§3.2.1 below). An
  explicit **"AND"** chrome label separates every condition row from the next, identical to the
  collapsed summary's own labeling — the logic reads the same whether collapsed or expanded.
- A **"+ Add Condition"** chrome button, disabled past `max_conditions_per_rule` (§7) with an
  inline reason (`"Maximum 8 conditions per rule"`).
- An **Action** selector — a three-way chrome segmented control: Keep / Auto-Sell / Auto-Dismantle.
- A **live preview strip** at the top of the accordion, recomputed on every edit while the rule is
  open: the in-progress shadow status and items-affected count, explicitly labeled `"Preview"` so
  it is never confused with the row's last-saved badge state (Edge Case 8).
- **Done** (commits the in-progress edits to the persisted rule set — recomputing shadow detection
  for the whole list, since any edit can create or resolve shadowing elsewhere) and **Cancel**
  (discards this edit session, reverting to the last-saved state) — a lightweight pair, not the
  full ceremony gate (Assumption A4), since nothing here is destroying an item.

#### 3.2.1 Condition Field Vocabulary and Value-Picker Shape

The field picker offers exactly the fields below — matching `loot-drop-system` §3.9.1's own field
examples by exact name where it names one, and preserving that document's own convention of baking
a comparator directly into a field's name where it already does so (`quantity_gte`), rather than
inventing a cleaner-looking but divergent abstraction:

| Field (exact `LootFilterRule.field` key) | Value-Picker Shape | Offered Operators |
|---|---|---|
| `base_type` | Enum picker — `base_type` silhouette icons (`forge-ui` §3.3 reuse) | `equals`, `not_equals`, `in` |
| `rarity` | Enum/ordinal picker — rarity ring-frame swatches (Assumption A5) | `equals`, `not_equals`, `in`, `greater_than_or_equal`, `less_than_or_equal` |
| `source` | Enum picker — Source medallions (Assumption A5), plus an explicit "Unaligned (no Source)" option for `null` | `equals`, `not_equals`, `in` |
| `ability_focus` | Boolean toggle | `equals` (fixed, not player-chosen — the toggle itself is the value) |
| `stackable` | Boolean toggle | `equals` (fixed, same as above) |
| `quantity_gte` | Numeric input, Data-face tabular | `greater_than_or_equal` (fixed — baked into the field name, matching `loot-drop-system`'s own illustrative naming) |
| `has_enchantment_type` | Multi-select autocomplete against the known `effect_type` catalog (`item-data-schema` §3.7) | `has_any`, `has_none` |
| `modifier_stat_present` | Multi-select autocomplete against the known `stat` catalog (`item-data-schema` §3.6) | `has_any`, `has_none` |
| `definition_id` | Search-by-name autocomplete picker (item Definition display names) | `equals`, `not_equals`, `in` |

**Deliberately excluded from the field picker** — `status`, `merged_from`, `vow_binding_log`,
`created_at`, `item_instance_id`. Every one of these is **invariant across every item the filter
engine ever evaluates**: the engine only ever evaluates freshly-minted drops
(`loot-drop-system` §3.3 step 8), and per `item-data-schema`'s own field rules, a freshly-minted
instance always has `status = active`, `merged_from = null`, an empty `vow_binding_log`, and a
fresh `created_at`/`item_instance_id` with no prior history to test. Offering a condition field
that can provably never distinguish one evaluated item from another would be pure UI clutter — a
structural application of "impossible to attempt" at the field-selection level itself, matching
Edge Case 3.

### 3.3 Adding, Duplicating, and Deleting a Rule

**Adding**: the persistent **"+ Add Rule"** chrome button (top of the Rule List) inserts a new rule
at the **last** position (highest priority number — evaluated last) with zero conditions and
`action = keep`. This is the deliberately safest possible starting state: a zero-condition `keep`
rule is functionally a no-op duplicate of the engine's own default behavior (`loot-drop-system`
§3.9.2), so it changes nothing until the player adds conditions, and appending at the end can never
shadow or be shadowed by anything already authored. The new row opens directly into its expanded
edit state (§3.2) so the player can start filling conditions immediately, with no extra navigation
step.

**Duplicating**: clones the source rule's full condition list, action, and enabled state into a new
rule inserted immediately after the source, with a fresh `rule_id` and (Assumption A8) a
`rule_label` of `"[original label] (copy)"` if the source had one. Opens collapsed, not expanded —
duplication is a starting point for a small tweak, not a fresh authoring session.

**Deleting**: a single click/press on the Delete action removes the rule immediately (no modal, no
ceremony gate — Assumption A4) and replaces it with a temporary, non-blocking toast:
`"Rule deleted. [Undo]"`, actionable for `undo_toast_duration_seconds` (§7). Activating Undo
restores the rule at its exact prior position with its exact prior content — a full round-trip,
not a re-creation from scratch. Deleting a rule always triggers a shadow-detection recompute for
the remaining list, since removing a shadowing rule can un-shadow rules below it.

### 3.4 Priority Reordering — Three Redundant Paths

Per art-bible §7.7's binding requirement — this screen is the section's own named example
(*"any drag-to-reorder (loot filter priority)... requires a non-drag keyboard equivalent"*) —
reordering is available through **three** input paths that always converge on identical resulting
state, so there is no divergent behavior between input methods:

1. **The numeric priority field** (primary, always visible, §3.2 item 2) — typing a target
   position and confirming (Enter, or blur) moves the rule to that position; every other rule's
   priority shifts to absorb the move, maintaining strict `1..N` contiguity automatically
   (Assumption A2). Any out-of-range or duplicate input (0, negative, past `N`, a number already
   held by another rule mid-edit) is clamped into range and resolved the same way — never an
   error state, never a tie or a gap (Edge Case 7).
2. **Move-up / move-down buttons** (secondary, always visible per row, §3.2 item 1) — a single
   press swaps the rule with its immediate neighbor in that direction; repeatable, and focus stays
   on the moved rule so repeated presses feel direct rather than requiring re-acquisition each
   time.
3. **A drag handle** (tertiary, mouse-only, strictly additive — never the only path) — a small
   grip-dot icon at the row's left edge. Dropping a dragged row triggers the identical reindex
   logic as the other two paths.

Any move via any path triggers an instant (no transition motion, art-bible §7.3) recompute of the
shadow-detection badge for **every** rule in the list, since reordering can create or resolve
shadowing anywhere, not just at the moved rule's old and new positions.

### 3.5 First-Match-Wins Comprehension — Shadow Detection and the Live Test Panel

This is the single most valuable thing this screen does, per the task brief, and it is answered by
two complementary, always-available systems — one static and automatic, one dynamic and
player-driven.

#### 3.5.1 Structural Shadow Detection (Always-On, Automatic)

Computed continuously as the rule set changes (§4 Formula 1) and surfaced two ways:

- **Per-row badge** (§3.2 item 7) — any rule Formula 1 flags as fully shadowed shows the
  `"Unreachable"` chrome badge. Focusing or clicking it reveals an inline callout naming the exact
  cause: `"Rule 2 (priority 2) already catches everything this rule would match."` for a
  containment case, or `"Identical to Rule 2."` for the exact-duplicate special case (Edge Case 1)
  — a distinct, more specific message for the single most common authoring mistake. The callout
  links directly to the shadowing rule; activating the link scrolls to and briefly highlights that
  rule with one discrete, non-looping flash (matching the HUD-adjacent "single beat, never a loop"
  motion discipline art-bible §7.3 establishes for state-transition feedback, extended here to a
  utility-screen cross-reference).
- **List-level catch-all banner** — if any **enabled** rule with **zero conditions** exists at any
  position other than the last, a banner (not just a row badge — this specific mistake is common
  and catastrophic enough to deserve visibility a player can't scroll past) reads:
  `"Rule 4 matches every item — every rule after it will never run."` The banner disappears the
  instant that rule moves to the last position or is disabled (Acceptance Criterion 9).

#### 3.5.2 The Live Test Panel (Dynamic, Player-Driven)

A collapsible drawer (toggled from the persistent header, §3.1), with two input modes for choosing
what to test:

- **Test a real item** — an item picker reusing `forge-ui` §3.3's item-tile pattern, drawn from the
  player's current inventory.
- **Build a sample item** — the identical field/value pickers used in the condition editor
  (§3.2.1), letting a player construct a hypothetical item they don't currently own — critical for
  planning ("what happens when I finally loot a Legendary Machine weapon") rather than only
  auditing what already exists.

The panel evaluates the chosen item against the current rule list (§4 Formula 3) and displays:

- The **matched rule** (its number, priority, and action) — or, if none matched,
  `"No rule matched — default action: Keep"` (directly answering Edge Case 4/6's onboarding
  concern with a concrete instance, not just the header's aggregate statement).
- A **runner-up list** — every other enabled rule that also matched this item but lost to the
  winner's earlier priority. This is the concrete, per-instance mechanism that makes
  first-match-wins legible experientially, complementing §3.5.1's structural, always-on proof.
- If the safety net downgraded the result, that is shown explicitly and separately from a normal
  match (§3.6).

### 3.6 The Safety Net, Surfaced

`loot-drop-system` §3.9.3 owns the rule; this document's entire job is making sure a player never
has to trust it blindly.

- The header's safety-net statement (§3.1) is **static, permanent, and unconditional** — it does
  not change based on the current rule set, because the guarantee itself does not change based on
  the current rule set.
- The Epic-protection toggle (§3.1) reflects `epic_auto_disposal_lock_default`. Turning it **off**
  (the risk-increasing direction) requires one explicit confirm click:
  `"Turn off Epic protection? Rules can then sell or dismantle Epic items."` Turning it back **on**
  is instant, no confirm — the asymmetry matches the asymmetry of the risk itself.
- Every rule whose action is Auto-Sell or Auto-Dismantle carries a standing, always-present
  informational badge (a small lock glyph, tooltip: *"Legendary items are always kept, regardless
  of this rule."*) — shown **proactively on every disposal-action row**, not conditionally on
  whether that row's current conditions happen to include Legendary. This is a deliberate design
  call: reassurance is cheapest and most useful *before* a player wonders, not only in response to
  a risky-looking rule.
- The Test Panel (§3.5.2) is the dynamic proof layered on top of this static reassurance: testing
  a Legendary or protected-Epic sample item against a disposal-action rule always displays the
  downgrade explicitly (`"Rule 3 matched (Auto-Dismantle) → downgraded to Keep — Legendary safety
  net applied"`), so the guarantee is not just stated, it is demonstrably observable on demand.

### 3.7 Presets and Onboarding

**The Preset Gallery is the default view whenever the player's rule set is empty** (§3.1) — the
concrete mechanism that lets a first-time player reach a working filter without ever opening the
rule-builder vocabulary. Preset cards render at this screen's own **None** ceremony tier (never a
`forge-ui`-style ceremony bump — richness lives in content, not frame decoration, art-bible §3.4)
and show: a Display-face name, a one-line body-text description, a compact glyph-chip summary of
what the preset does (e.g., `"Keeps: Epic+, ability-focus. Sells: Common, Uncommon."`), and an
**Apply** button.

**Five starter presets** (content shape locked, exact thresholds illustrative per Assumption A6):

| Preset | Intent | Rule Shape (illustrative) |
|---|---|---|
| **Keep Everything** | The safe, zero-risk baseline — matches doing nothing at all | One rule: no conditions, action `keep` (functionally identical to zero rules) |
| **Declutter Common Junk** | Low-risk starter cleanup | Auto-dismantle `rarity` ≤ Uncommon with no enchantments present; keep everything else |
| **Aggressive Cleanup** | Maximum automation for a player who trusts the safety net fully | Auto-sell `rarity` ≤ Rare; keep Epic and above (already safety-netted regardless) |
| **Materials Focus** | Favor material stockpiling over Gleam income | Auto-dismantle `rarity` < Epic; keep Epic and above |
| **Ability-Focus Only** | Keep only what could plausibly be equipped or Vow-bound | Keep where `ability_focus = true` OR `rarity` ≥ Rare; auto-sell the rest below Rare |

**Applying a preset**: if the player's rule set is currently empty, Apply installs the preset
immediately, no confirm needed — there is nothing to lose. If custom rules already exist, Apply
presents an explicit two-way choice:

- **"Add to my rules"** — the preset's rules are appended at the **end** of the existing list
  (lowest priority, evaluated last), so they can never shadow or override an existing custom rule
  by default. No confirm needed — non-destructive to prior work.
- **"Replace all my rules"** — destructive to the player's existing configuration; requires one
  explicit confirm (matching the asymmetric-risk pattern used for the Epic toggle, §3.6) and is
  followed by the same Undo-toast pattern rule deletion uses (§3.3) — `"Replaced 12 rules with
  'Aggressive Cleanup'. [Undo]"`.

The **"Presets"** header button (§3.1) remains reachable at all times, so a returning player who
has since authored custom rules can still browse and layer in a preset later.

### 3.8 Scale — Search, Collapse, and Grouping

- **Search** (chrome field, always visible above the Rule List) matches, case-insensitively,
  against a rule's `rule_label` (Assumption A8, if set), its condition field/value text, and its
  action name — a live, instant view filter only (never affects priority or evaluation order,
  never mutates state).
- **Collapse**: all rows default to the collapsed summary state (§3.2); a **"Collapse All / Expand
  All"** toggle is available for a player auditing many rules at once.
- **Grouping**: an optional **"Group by Action"** display toggle reorganizes the visual list into
  three chrome-divided sections (Keep / Auto-Sell / Auto-Dismantle) while preserving true priority
  order *within* each group, and continuing to show each rule's absolute priority number. Grouping
  is strictly a display convenience — it never changes, hides, or reorders the underlying
  evaluation sequence, and this document states that explicitly rather than leaving it implied,
  precisely to avoid the dangerous misconception that visual clustering equals evaluation order.
  Default display mode is priority order (matching actual evaluation semantics, the least
  misleading default), not grouped.
- **`simplified_ui_density`** (Assumption A7): when enabled, every rule row renders only its
  primary-tier fields (§3.2); the condition editor, exact items-affected count, and drag handle
  move behind the row's existing Edit/expand action rather than disappearing outright.

### 3.9 Keyboard & Gamepad Navigation

- **Full keyboard/gamepad operability, no exceptions** (`accessibility-settings-system` §3.7 item
  3): every interactive element — header controls, every rule-row control including both
  reordering paths that don't require a mouse, the condition editor, the Preset Gallery, and the
  Test Panel — is reachable and operable without a mouse or pointer. This screen is the literal,
  named subject of art-bible §7.7's non-drag-reorder requirement (§3.4), and satisfies it
  structurally (two independent non-drag paths exist by design) rather than retrofitting a
  keyboard equivalent onto a drag-only pattern after the fact.
- **Visible keyboard-focus indicator** (WCAG 2.4.7) on every one of the above, built from
  value-step/border-weight within the chrome budget (`accessibility-settings-system` §3.7 item 1).
- **Fixed logical navigation order**: header (safety-net text, non-interactive → Epic toggle →
  Presets button → Search field → Test Panel toggle) → Rule List content (each row, in priority
  order: move-up → move-down → priority field → enabled toggle → condition summary/expand trigger
  → action badge → Edit → Duplicate → Delete) → "+ Add Rule" → (if the Test Panel is open) its own
  controls, appended after the Rule List in tab order.
- **No simultaneous multi-button input anywhere** (`accessibility-settings-system` §3.7 item 8):
  every action is a single discrete input at a time.

---

## 4. Formulas

Three formulas are original to this document — a UI-owned shadow-detection algorithm and two
display-layer derivations over `loot-drop-system`'s own evaluation semantics, which this document
cites and never redefines (matching `forge-ui` §4's own precedent of adding only genuine
display-layer math on top of an owning system's rules).

### Formula 1 — Structural Shadow Detection

```
conditions(R) = the set of (field, operator, value) tuples on rule R

Tier 1 (exact literal containment, always-on, sound):
  shadows(R_i, R_j) = TRUE  if  conditions(R_i) ⊆ conditions(R_j)  as literal tuples
                              OR conditions(R_i) = ∅   (a catch-all)

Tier 2 (same-field comparator-aware subsumption, best-effort, non-blocking):
  for any field shared between R_i and R_j, extends Tier 1's subset test to also recognize
  a provable value-range superset relationship for that field under ordinal comparators
  (equals / in / greater_than_or_equal / less_than_or_equal) before the whole-set test runs

is_fully_shadowed(R_j) = TRUE  if  ∃ enabled R_i, i < j (evaluated earlier), such that
                                    shadows(R_i, R_j) under Tier 1 or Tier 2
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `R_1..R_n` | ordered list | — | The player's rule set in ascending `priority` order (`R_1` evaluated first, `loot-drop-system` §3.9.2). |
| `conditions(R)` | set of tuples | — | `R`'s `FilterCondition` list, read as an unordered set for containment purposes (AND semantics make order within a rule irrelevant to matching). |
| `shadows(R_i, R_j)` | bool | — | True if every item that could match `R_j` necessarily also matches `R_i`. |
| `is_fully_shadowed(R)` | bool | — | Drives the `"Unreachable"` badge (§3.2 item 7). Only computed over **enabled** rules — a disabled rule neither shadows nor can be shadowed. |

**Why Tier 1 is sound**: AND semantics mean an item matches `R_j` only if it satisfies *every*
condition in `conditions(R_j)`. If `conditions(R_i)` is a subset of that set, every condition in
`R_i` is also satisfied whenever `R_j`'s full set is — so any item that would match `R_j` already
matched `R_i` first, and `R_j` can never independently determine an outcome. This is a proof, not
a heuristic, for the exact-tuple case.

**Worked example 1 (the task's own "second rule with identical/broader conditions is dead" case,
generalized)**: `R1` (priority 1, `conditions = {rarity = common}`, `action = auto_dismantle`),
`R2` (priority 2, `conditions = {rarity = common, source = machine}`, `action = auto_sell`).
`conditions(R1) = {rarity=common} ⊆ conditions(R2) = {rarity=common, source=machine}` →
`shadows(R1, R2) = TRUE` → `R2` is flagged `"Unreachable — Rule 1 already catches everything this
rule would match."` Every Common Machine item was already dismantled by `R1` before `R2` is ever
consulted.

**Worked example 2 (catch-all)**: `R1` (priority 1, `conditions = ∅`, `action = keep`) placed
ahead of `R2`–`R5`. `conditions(R1) = ∅` satisfies the catch-all clause against every later rule →
`R2`–`R5` are all flagged, and the list-level banner (§3.5.1) fires because `R1` is a
zero-condition rule not in the last position.

**Worked example 3 (correctly NOT flagged — proving the algorithm doesn't over-flag)**: `R1`
(`conditions = {source = body}`), `R2` (`conditions = {source = nature}`). Neither condition set is
a subset of the other (different `equals` values on the same field are mutually exclusive, not
overlapping), so `shadows(R1, R2) = FALSE` in both directions — `R2` is correctly left unflagged.

**Worked example 4 (Tier 2, comparator-aware)**: `R1` (`conditions = {rarity ≥ epic}`), `R2`
(`conditions = {rarity ≥ legendary}`). Neither is a literal subset of the other under Tier 1 (the
tuples differ), but Tier 2 recognizes that every item satisfying `rarity ≥ legendary` also
satisfies `rarity ≥ epic` (Legendary is a superset of the Epic-or-above range) → `shadows(R1, R2) =
TRUE` under Tier 2, and `R2` is flagged.

**Known limitation, stated explicitly (Assumption A1)**: Tier 2 only reasons about a single shared
field at a time under ordinal comparators. It does not attempt cross-field logical implication
(e.g., a rule made redundant only by the *combination* of two different earlier rules' conditions
together). Such cases are not flagged — a documented false-negative, never a false positive.

### Formula 2 — Items-Affected Count

```
items_affected(R_k) = count of active Item Instances I, drawn from the current inventory
                       (or a bounded sample, see below), such that R_k is the first enabled
                       rule (ascending priority) whose conditions(R_k) all match I

If |inventory| > items_affected_sweep_sample_size (§7):
  the sweep runs over the most-recently-acquired `items_affected_sweep_sample_size` items
  (deterministic, reproducible — never a random sample), and the displayed count is labeled
  "~approximate" rather than exact
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `items_affected(R_k)` | int | ≥ 0 | The row's displayed count (§3.2 item 6). |
| `items_affected_sweep_sample_size` | int, tuning knob | see §7 | Bounds compute cost against `loot-drop-system` Assumption A5's uncapped inventory. |

This is a literal, deterministic run of `loot-drop-system` §3.9.2's own evaluation loop once per
swept item, tallying which rule "won" for each — not an independent probability estimate.
Recomputed synchronously whenever the rule set changes while this screen is open, and once on
screen open; **not** recomputed in response to background inventory changes while the screen is
closed, since resweeping for a screen the player isn't looking at would be pure wasted cost for a
planning tool, not a live HUD.

**Worked example**: a 5,000-item swept sample (under the default `sample_size`, so unsampled and
exact); a rule `rarity ≤ rare` positioned such that it is the first match for 340 of those items →
the row displays `"340 items."`

### Formula 3 — Live Test Panel Trace Evaluation

```
For a test item I (real, from inventory, or synthetic, constructed via §3.2.1's pickers):

  matched_rule = first enabled R_k (ascending priority) where conditions(R_k) all match I
  runner_up_rules = { R_m : index(R_m) > index(matched_rule), R_m enabled,
                       conditions(R_m) all match I }

  raw_action = matched_rule.action   (or "keep" if matched_rule is null — no rule matched)

  final_action = keep, with "safety net applied" flag,
                 if raw_action ∈ {auto_sell, auto_dismantle} AND
                    (I.rarity = legendary OR
                     (I.rarity = epic AND epic_auto_disposal_lock_default = on))
  final_action = raw_action, otherwise
```

This adds no new evaluation logic — it is a display-layer trace of exactly the pipeline
`loot-drop-system` §3.9.2 (matching) and §3.9.3 (the safety net) already specify, applied to one
chosen item instead of a live drop batch.

**Worked example**: a synthetic sample item (`rarity = legendary`, `source = shadow`,
`base_type = weapon`) tested against a rule list where `Rule 3` (priority 3,
`conditions = {base_type = weapon}`, `action = auto_dismantle`) would otherwise match first. Panel
output: `matched_rule = Rule 3`, `raw_action = auto_dismantle`, but since `I.rarity = legendary`,
`final_action = keep`, displayed as: *"Rule 3 matched (Auto-Dismantle) → downgraded to Keep —
Legendary safety net applied."* If `Rule 5` (`conditions = {base_type = weapon, rarity ≥ epic}`,
`action = keep`) also matches this item, it appears in the runner-up list even though it wasn't the
winner, so the player can see every rule that was "in the running," not only the one that fired.

### Non-Formulaic Mechanisms (Stated Explicitly, Not Padded)

**Priority reindexing** (§3.4) is a deterministic array-shift on move/insert/delete with no
independent math — every rule's `priority` is simply renumbered `1..N` in the new order, an
implementation detail with no ambiguity worth a symbol table. **Search matching** (§3.8) is a
plain case-insensitive substring match across `rule_label`, condition field/value text, and action
name, with no ranking or fuzzy logic — the filtered view is a strict inclusion test, not a scored
result.

---

## 5. Edge Cases

1. **Two rules have identical conditions.** The second is dead code — this is Formula 1's Tier-1
   exact-match case by construction (`conditions(R_i) = conditions(R_j)` trivially satisfies
   `conditions(R_i) ⊆ conditions(R_j)`). **Ruling**: flagged with the more specific message
   `"Identical to Rule N"` rather than the general containment message (§3.5.1), since this is the
   single most common real authoring mistake (copy-pasting a rule and forgetting to change it).
   The rule is never auto-deleted or blocked — always surfaced, never silently removed.
2. **A rule references an item field that no longer exists** (a future schema change, or a stale
   reference to a removed `effect_type`/`stat` value). **Ruling**: mirrors `loot-drop-system` Edge
   Case 10 exactly at the engine level — the condition evaluates to `false`, never an error
   ("fail inert, not fail loud"). This document adds a UI-layer enhancement on top, not a change to
   that semantics: the affected condition row renders a distinct `"Unknown field"` chrome warning
   tag (text, never color-only) in the expanded editor, so the player can see and fix a
   permanently-non-matching condition rather than have a rule that silently does nothing forever.
3. **A filter would auto-sell or auto-dismantle almost everything (a catastrophic
   misconfiguration).** **Ruling**: no hard block exists beyond the Legendary/Epic safety net —
   `loot-drop-system` deliberately does not protect ordinary items, since the entire point of this
   system is delegated player *autonomy* (§2, cited above), and overriding a deliberately
   aggressive filter would contradict that. This document adds a **non-blocking advisory** instead:
   if applying a rule edit or a preset would result in more than `catastrophic_disposal_threshold_
   percent` (§7) of the player's *current* inventory (computed via the same Formula 2 sweep,
   summed across every disposal-action rule) being caught by Auto-Sell/Auto-Dismantle combined, a
   one-time banner appears: `"This filter will affect 94% of your current inventory — review before
   it processes in the background."` It never blocks the action — a player who wants an aggressive
   filter on purpose is entitled to one.
4. **The player has zero rules.** **Ruling**: default action is `keep` (`loot-drop-system` §3.9.2)
   — communicated directly in the persistent header (`"0 rules — every item is kept
   automatically"`, §3.1) and by the Preset Gallery being the default view in this state (§3.7),
   so a first-time player is never left wondering what "doing nothing" means here.
5. **A rule's condition can never match** (e.g., a comparator/value combination outside any real
   item's possible range). **Ruling**: for every enum-constrained field (`base_type`, `rarity`,
   `source`, `ability_focus`, `stackable`), the value picker only ever offers real, valid values
   (§3.2.1) — a structurally-impossible condition on these fields cannot be constructed at all,
   matching the "impossible to attempt" philosophy at the value-selection level. For open numeric
   fields (`quantity_gte`), a player can type an unrealistic threshold; this is allowed (not
   blocked — it is not technically invalid, just unlikely to ever match) and is not specially
   flagged as "broken" — Formula 2's own `items_affected` count naturally reports `0` for such a
   rule without this document inventing a separate, potentially-wrong "impossible" detector for a
   genuinely open field.
6. **The player disables every rule** (`enabled = false` on all of them). **Ruling**: behaves
   identically to having zero rules (Edge Case 4) — every item defaults to `keep`, and the same
   header messaging and Preset Gallery default apply, reusing the exact same UI rather than a
   separate special case.
7. **The priority field is typed with a duplicate, zero, negative, or out-of-range value.**
   **Ruling** (§3.4, path 1): clamped into `[1, N]` and resolved via the same reindex logic used
   for every other move — never an error, and ties/gaps are structurally impossible to create
   through this control (Assumption A2).
8. **Shadow-detection and items-affected values while a rule is mid-edit, unsaved.** **Ruling**:
   the expanded row's live preview strip (§3.2) computes and shows these against the in-progress
   condition set, explicitly labeled `"Preview"` — never confused with, and never silently
   overwriting, the row's last-saved badge state until Done is pressed.
9. **A condition tests Vow-binding, forge-lineage, or archive/creation-timestamp state.**
   **Ruling** (§3.2.1): these fields are not offered in the field picker at all, because every item
   the filter engine ever evaluates is a freshly-minted drop, which per `item-data-schema` can
   never have Vow history, forge lineage, or an `archived` status yet — offering a condition that
   can provably never distinguish one evaluated item from another would be pure clutter on the
   game's densest screen, the opposite of this document's whole purpose.
10. **Applying "Replace all my rules" by accident.** **Ruling**: covered by the required explicit
    confirm (§3.7) plus, immediately after, the same Undo-toast pattern rule deletion uses (§3.3)
    — belt-and-suspenders, so a single misclick can never permanently cost a player's prior
    configuration work.

---

## 6. Dependencies

**Depends on**:

- **`loot-drop-system.md`** — the entire `LootFilterRule`/`FilterCondition` schema (§3.9.1) and
  evaluation semantics (§3.9.2 first-match-wins/ascending-priority/AND-within-a-rule/default
  `keep`, §3.9.3 the Legendary/Epic safety net) this document is the interface for, cited by exact
  field and rule name throughout and never redefined. That document's own Dependencies section
  already names `loot-filter-ui` as depended-on-by and explicitly delegates "layout, drag/
  reorder-vs-keyboard-equivalent interaction... and the Epic-lock toggle's UI" to this document —
  fulfilled in full by §3.4, §3.6, and §3.9 above.
- **`item-data-schema.md`** — every filterable field (§3.2.1), rarity/source/`base_type` enums, and
  the `max_stack_size`/quantity concept the `quantity_gte` field conditions on.
- **`accessibility-settings-system.md`** — every binding requirement in its §3.7 is treated as
  non-negotiable here (§3.9 restates the ones most load-bearing for this screen); §3.6's
  `simplified_ui_density` delegation to "that screen's own future GDD" is resolved by this
  document (Assumption A7, §3.8) — fulfilling that document's own open item.
- **`design/art/art-bible.md`** — §3.4/§7.6 (zero-ceremony, density-never-flexes), §4.5 (chrome
  palette), §4.3/§4.6 (rarity ring-frame ornament, colorblind-safety audit), §7.1 (Data-face
  tabular figures for every comparison-column number), §7.2 (loot-filter logic icons stay chrome
  even though bespoke), §7.3 (utility-screen motion is purely functional), §7.7 (the non-drag
  reorder binding requirement this screen is directly named under), §9.6 (the explicit
  density-without-memorization brief this whole document argues it satisfies).

**Adjacent (informational, not a dependency in either direction)**: **`forge-ui.md`** — this
document reuses its item-tile pattern, five-state interaction matrix, and resource-icon reuse
(Assumption A3) as an established sibling-UI precedent, but neither document depends on the
other's own gameplay data.

**Depended on by** (bidirectional):

- **`region-mastery-automation-system`** — per this task's own framing, loot
  filtering is expected to be an automation unlock; this document does not itself gate anything
  behind an unlock state (that gating logic belongs entirely to the not-yet-written automation
  system), but flags that whichever GDD introduces that unlock must reference this document's
  screen and `loot-drop-system`'s engine, not redefine either.

**`systems-index.md` gap flagged, not self-edited** (per this session's file discipline, matching
the precedent set by `forge-ui.md` and `accessibility-settings-system.md` for their own equivalent
gaps): `systems-index.md`'s Dependency Map currently lists `loot-filter-ui` as depending only on
`loot-drop-system`. This document establishes two additional real dependencies —
`item-data-schema` and `accessibility-settings-system` — neither of which are currently reflected
there. Flagged for a future centralized `systems-index.md` update, not corrected here.

---

## 7. Tuning Knobs

| Knob | Default | Safe Range | Affects | Notes |
|---|---|---|---|---|
| `rules_per_filter_cap` | 200 | 50–500 | Maximum total authored rules per player filter | Bounds Formula 1's `O(R²)` shadow-detection cost and Formula 2's per-rule sweep cost, and keeps the Rule List itself navigable at scale (Design Task 6). |
| `shadow_detection_tier2_enabled` | on | on / off | Whether comparator-aware same-field subsumption (Formula 1, Tier 2) runs, or only exact-literal Tier 1 | Recommended to stay on — no false positives at any rule count — but exposed as a knob in case a very large rule set makes its added cost measurable in practice. |
| `catastrophic_disposal_threshold_percent` | 90% | 70–95% | Edge Case 3's non-blocking advisory-banner trigger | Below 70%, the banner risks firing on ordinary aggressive-but-intentional filters (e.g. Aggressive Cleanup preset), becoming noise; above 95%, it risks never firing before real damage is done. |
| `items_affected_sweep_sample_size` | 5,000 | 1,000–20,000 | Formula 2's sampling fallback once inventory exceeds this size | Directly answers `loot-drop-system` Assumption A5's uncapped inventory — without this bound, a very long unattended automated session (that document's own named risk) could make every items-affected recompute on this screen arbitrarily expensive. |
| `max_conditions_per_rule` | 8 | 5–15 | The condition editor's `"+ Add Condition"` cap (§3.2) | An AND-chain beyond a handful of conditions has steeply diminishing authoring value and starts to fight the one-line condition-summary read (§3.2 item 4); this cap keeps both the editor and the summary legible. |
| `undo_toast_duration_seconds` | 8 | 4–15 | How long the Undo toast (§3.3, §3.7) remains actionable after a rule deletion or preset-replace | Short enough to not linger as visual clutter on a screen revisited constantly; long enough to catch an accidental click without requiring split-second reaction. |
| `preset_count` | 5 (**locked**, content-authored) | not tunable as a count | §3.7's starter preset gallery | Not a numeric balance knob — the five presets' names/purposes are locked by this document; their exact internal rarity/type thresholds are future content authoring (Assumption A6). |
| `epic_auto_disposal_lock_default` | — (owned elsewhere) | — | The Epic-protection toggle's starting state (§3.6) | Owned entirely by `loot-drop-system` §7; this screen's toggle is the exposed **control** for that value, not a second, independently-tunable copy of it. |

---

## 8. Acceptance Criteria

| # | Criterion | Verification Method |
|---|---|---|
| AC1 (required — keyboard-only) | Every interaction on this screen — rule add/edit/duplicate/delete, priority reorder via all three paths, enable toggle, preset apply, search, and the full Test Panel flow — is completable using keyboard only, with no functionality reachable exclusively via mouse hover or click. | Scripted or manual walkthrough completing one full rule authored from scratch, one reorder via each of the three paths, one preset apply, and one Test Panel trace, using keyboard input exclusively. |
| AC2 (gamepad-only) | Identical scope to AC1, substituting a single connected gamepad with no keyboard or mouse connected. | Identical walkthrough to AC1, gamepad-only. |
| AC3 (required — shadowed rules always surfaced) | For every rule-set configuration containing at least one fully-shadowed rule under Formula 1's Tier-1 definition, the affected rule(s) display the `"Unreachable"` badge with no exceptions. | Test matrix covering: identical-conditions duplication, a catch-all placed early, and a literal-subset containment case (Formula 1's three worked examples), asserting the badge appears in every case and does not appear in the fourth (non-shadowed) worked example. |
| AC4 (required — no filter can destroy a Legendary) | For every possible action/condition combination targeting a Legendary sample item in the Test Panel, `final_action` (Formula 3) always resolves to `keep` with the `"safety net applied"` flag set, never to `auto_sell` or `auto_dismantle`. | Test sweep constructing a Legendary sample item against every action type and a range of matching condition sets, asserting the trace output is `keep` in 100% of cases. |
| AC5 (required — first-time preset without authoring) | A first-time player (zero rules) can reach a fully-applied, working filter without ever opening the condition editor. | Scripted walkthrough: open screen with 0 rules → Preset Gallery renders as the default view (no navigation required to reach it) → select and Apply any preset → rules are active → assert the condition editor (§3.2's expanded state) was never opened during the walkthrough. |
| AC6 | Priority reordering produces byte-identical resulting rule order regardless of which of the three input paths (numeric field, move buttons, drag) performed an equivalent move. | Perform the same logical move via each of the three paths independently on identical starting fixtures; diff the resulting `priority` arrays and assert exact equality across all three. |
| AC7 | Typing any priority value — including duplicates, zero, negative, or a value past the current rule count — into the priority field never produces a tie or a gap in the underlying rule set. | Boundary-value test sweep (0, negative, `N+1`, an already-held value) against a fixture list, asserting the resulting `priority` sequence is always a contiguous `1..N` permutation. |
| AC8 | `items_affected` (Formula 2), computed against a fixed inventory fixture, exactly equals the count of items for which that rule is the true first-match winner under `loot-drop-system` §3.9.2's own evaluation semantics. | Cross-check this document's Formula 2 output against a reference implementation of that document's evaluation loop over the same fixture inventory and rule set. |
| AC9 | The catch-all banner (§3.5.1) appears whenever an enabled zero-condition rule exists at any position other than last, and disappears the instant that rule is moved to last position or disabled. | Fixture rule set exercised through a sequence of reorder/disable actions, asserting the banner's presence/absence at each step. |
| AC10 | The Legendary safety-net statement and the Epic-protection toggle are visible in the header in every screen state (Rule List, Preset Gallery, Test Panel open or closed). | State-matrix structural check across all four combinations of main-content-state × Test-Panel-open/closed. |
| AC11 | Deleting a rule, or replacing all rules via a preset, always offers an Undo action for at least `undo_toast_duration_seconds`, and activating Undo exactly restores the prior rule set (order, content, enabled-state) with no data loss. | Delete-then-undo and replace-then-undo round-trip tests, diffing the rule set before the destructive action and after Undo, asserting byte-identical equality. |
| AC12 | With `colorblind_preview_mode = grayscale` enabled, every mechanically important state on this screen (enabled/disabled rule, shadowed/unreachable status, action type, safety-net-applied status) remains fully distinguishable by shape, text, or icon alone. | QA pass through the Rule List, Preset Gallery, and Test Panel with the grayscale preview active, matching `forge-ui` AC14's own verification method. |
| AC13 | With `ui_scale_percent = 200`, the Rule List and Test Panel reflow to scroll (vertical for row count, horizontal for the condition-summary column where needed) rather than overlapping or silently truncating any row's content. | Layout test at maximum scale, per `accessibility-settings-system` §5 Edge Case #1's data-table reflow exception. |
| AC14 | With `simplified_ui_density` enabled, every rule row still displays enough information (priority, enabled state, one-line condition summary, action, unreachable badge) to make an informed enable/disable/reorder decision without opening the advanced editor. | Structural review of the collapsed-row field set against §3.8's documented primary/advanced split (Assumption A7). |
| AC15 | The Test Panel's synthetic "build a sample item" path can construct and correctly evaluate a hypothetical item the player does not currently own. | Fixture rule set plus a synthetic item chosen to be structurally absent from the fixture inventory, asserting the returned `matched_rule`/`final_action` matches the reference evaluation. |

---

## Cross-System Facts Proposed for Registration

None. Every gameplay formula, enum, and constant this document renders or reasons about (the
`LootFilterRule`/`FilterCondition` schema, the first-match-wins evaluation order, the Legendary/
Epic safety net, every Item Instance field and enum) is cited from `loot-drop-system.md` or
`item-data-schema.md` by exact name, never redefined — matching `forge-ui.md`'s own precedent for
its equivalent section. The three formulas this document does originate (shadow detection,
items-affected sweep, Test Panel trace) are UI-presentation derivations with no consumer outside
this screen; no other system in `systems-index.md` renders a loot-filter rule list, so there is
nothing here that would benefit from central registration. This document's one schema addition —
the UI-only `rule_label` field (Assumption A8) — is deliberately **not** proposed for registration
in `loot-drop-system.md`'s own schema, since it is never read by that document's evaluation engine;
it is flagged in this document's own Dependencies section (§6) as a forward coordination note for
whichever system implements persistence instead.
