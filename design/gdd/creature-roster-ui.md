# GDD: Creature Roster UI

> **SUPERSEDED 2026-09-01 by nothing — the subsystem was retired 2026-08-24.** Describes the creature-roster UI model, none of which is in the runtime. Kept for vocabulary and history — do not treat its rules as current.

## Document Status

- **Status**: Complete — all 8 sections authored
- **Created**: 2026-07-14
- **Owned By**: ux-designer
- **Review Mode**: `lean` — authored autonomously this session (no user available to approve
  section-by-section; matching the precedent set by `loot-filter-ui.md`, `forge-ui.md`,
  `accessibility-settings-system.md`, and `creature-jobs-evolution-system.md` this same session).
  Every ambiguity the source material left open is resolved with an explicit, flagged design call,
  never a placeholder. Validate at `/ux-review` and `/gate-check` before Production.
- **Priority**: MVP, Presentation tier — 19th in the recommended design order
  (`design/gdd/systems-index.md`), depending on `creature-jobs-evolution-system` (13th) and
  `creature-data-schema` (1st). This document owns the **interface** exclusively — every rule,
  formula, and state machine it renders (evolution gating, work-state causes, production math) is
  cited by exact name from those two documents, never re-derived.

## Source Material Read

`design/gdd/creature-jobs-evolution-system.md` (full — the five evolution influence vectors, the
work-state machine's Healthy/Blocked/Starved causes and fixes, Core Hatching, `equipped_charm_id`,
the five Role production profiles, all cited by exact field/formula name throughout),
`design/gdd/creature-data-schema.md` (full — `CreatureTemplate`/`CreatureInstance`/`EvolutionTree`/
`EvolutionNode` shape, `discovered_node_ids`, all schema invariants this screen must never violate),
`design/art/art-bible.md` (§2 Creature Management mood — "stewardship," the glyph-ledger signature
visual; §3.1 Role mass-distribution/Source edge-quality silhouette grammar; §3.2 glyph container/mark
grammar; §3.4/§7.2 chrome-vs-content split; §4.2/§4.3 Source colors and rarity ring grammar; §5.3 the
Healthy/Blocked/Starved work-loop spec — pose, glyph pulse, and player-fix language; §5.4
hostile/bound/rare distinguishing rules; §5.6 the separate roster-portrait asset; §7.2 iconography;
§7.3 UI animation — work-loop pulses must animate in the Roster; §7.6 ceremony tier None for this
screen; §7.7 binding accessibility requirements), `design/gdd/loot-filter-ui.md` (full — the sibling
maximum-density Presentation-tier screen this document reuses precedent from: the collapsed/expanded
accordion row pattern, the persistent-header-plus-attention-banner pattern, the two-tier
comprehension problem this screen has its own version of, and the general rule that reusable
precedent should be matched, not re-derived), `design/gdd/item-data-schema.md` (full — the `charm`
`base_type`, `creature_core` `base_type`, `vow_binding_log`, `source`/`modifiers`/`enchantments`
fields read for equip-gate evaluation and charm-picker display), `design/gdd/the-forge-system.md`
(§3.5.3 Feed to Creature, §3.6 Vow-bound exclusion — read specifically to resolve the charm
ownership-conflict edge case), `design/gdd/accessibility-settings-system.md` (full — every binding
requirement in §3.7 is a non-negotiable constraint here; §3.6's `simplified_ui_density`
primary/advanced delegation to "that screen's own future GDD," which explicitly names
`creature-roster-ui`, is resolved by this document), `design/gdd/game-concept.md` (Pillar 5, the
Discovery retention hook), `design/gdd/systems-index.md` (this system's entry and dependency map).

## Assumptions Log (resolved this session, no placeholders left below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | **Core Hatching lives on this screen, not the Forge.** | Hatching consumes a `creature_core` Item Instance but produces a `CreatureInstance` — its entire output and consequence is creature-side, and `creature-jobs-evolution-system` §3.2 already frames it as this document's concern ("`creature-roster-ui` is expected to surface a 'fully matured' status," §5 Edge Case 8, treating this screen as the creature's home). The task brief explicitly authorizes this call. `the-forge-system`'s own Feed action (a *different* thing a core can do) stays exactly where it is — Hatching and Feed are two mutually exclusive, per-core player choices (`creature-jobs-evolution-system` §3.7), not two halves of one flow that need to live together. |
| A2 | **The Evolution Tree View is an in-place accordion expansion of a roster row (a "Creature Detail" state), not a separate screen or modal.** The main content region otherwise toggles between exactly two top-level states — **Roster List** (default whenever ≥1 creature is bound) and **Hatch** (opened via a persistent header button, and the default view when the roster is empty of bound creatures) — matching `loot-filter-ui` §3.1's Rule List / Preset Gallery precedent by name. | Direct reuse of `loot-filter-ui` §3.2's "the row grows in place (accordion, never a separate modal)... this screen never leaves its own single dense list" pattern, itself grounded in art-bible §7.4 ("abstracted planning interfaces... without introducing a second navigation layer"). An accordion has ample room for the tree diagram and branch cards (§3.6) without inventing a third top-level state this project's established pattern doesn't use elsewhere. |
| A3 | **A branch node's `discovered_node_ids` membership is populated automatically for every direct child the instant a creature reaches the parent node** — never a separate discovery action, currency, or delay. Deeper (grandchild-and-beyond) nodes remain unrendered until reached in turn. | `creature-data-schema` §3.8 defines `discovered_node_ids` as a field but never specifies its population trigger — that ambiguity blocks this screen from being buildable. Pillar 5 ("what it becomes is your doing") is explicit that a hidden path reads as random, which the brief's own Design Task 3 restates as this screen's most important obligation — so the *current* decision point can never be hidden. Deferring discovery of nodes *beyond* the immediate children preserves the Discovery retention hook (`game-concept.md`) for deeper future tree content without ever hiding the branch the player is actually choosing between right now. MVP trees are only 1 root + branches (no grandchildren, `creature-data-schema` §7's tuning note), so this rule has no visible effect in MVP content — it is defined for Full-Vision forward-compatibility. |
| A4 | **The roster-row thumbnail animates only the glyph-pulse channel of the Healthy/Blocked/Starved state (art-bible §5.3's glyph-behavior column), not the full pose/verb work-loop** (strike/brace/orbit/fidget/breathe). The full pose/verb performance is reserved for the Detail accordion's larger portrait. | Art-bible §5.3 defines the three states as legible through *two independent channels* (pose/motion and glyph pulse) and §7.3 mandates that "work-loop pulses must animate in the Region View and Roster." §5.6's floor rule ("complexity scales down with canvas, never the reverse") is extended here from part-count to motion: a list-row thumbnail is well below the canvas a full body-verb performance needs to read, but the glyph-pulse channel is a compact, single-point signal that scales down cleanly and is independently sufficient per §5.3's own two-channel redundancy. |
| A5 | **The Charm Picker excludes any item with a non-empty `vow_binding_log`** from being offered as equippable to a creature. | `creature-jobs-evolution-system` §3.3 Vector 2 defines the creature-charm-equip slot but is silent on Vow-bound exclusion; that document's own Edge Case 7 already applies exactly this exclusion, defensively, to the Feed action ("never trusts the caller"). This document extends the same defense-in-depth reasoning by direct analogy to Equip, a second action on the same item category that document's rules left unaddressed — an item permanently bound to the Hunter's own build should not simultaneously be offered as a creature's charm. |
| A6 | **Cross-creature charm exclusivity (one item, one equipped creature) is fully resolved within this document's own scope**: the Charm Picker cross-references every creature's `equipped_charm_id` (the extension record this document is the sole renderer of) and excludes/annotates any item already equipped elsewhere ("Already equipped to [Creature]"). **Cross-creature-to-Forge exclusivity remains an open, flagged gap** — `the-forge-system`/`item-data-schema` have no field marking an item "equipped to a creature," so nothing prevents that item from independently being sold, dismantled, merged, or fed at the Forge. This document adds a defensive UI-level mitigation (re-validate `equipped_charm_id`'s target item status on every screen open/focus; if the item was consumed elsewhere, clear the slot to empty, non-destructively, with a one-time "Charm lost — re-equip" notice) rather than claiming the gap is closed. | `creature-jobs-evolution-system` A3 already flagged this exact gap forward as "a small `item-data-schema` follow-up" needed once both systems existed. This document is the first to actually hit the gap in a concrete interaction and resolves the half of it fully within its own file discipline, while being explicit that the other half needs that follow-up, not a workaround pretending it isn't there. |
| A7 | **Region/slot assignment uses a picker-first interaction (region list → slot grid) with no drag path anywhere on this screen**, satisfying art-bible §7.7's non-drag-keyboard-equivalent requirement by never having a drag-only path to retrofit in the first place. | Region tiles are not rendered on this screen at all (owned by `region-view-world-map-ui`) — there is no natural drag *target* here, unlike `loot-filter-ui`'s own-list drag-to-reorder. A click/keyboard-identical two-step picker is exactly as fast as a drag would be and requires no fallback path. |
| A8 | **Hatching requires exactly one explicit confirm click, not `forge-ui`'s full destructive-ceremony gate and not `loot-filter-ui`'s zero-ceremony inline-undo pattern.** | Hatching consumes a real Item Instance irreversibly, unlike a loot-filter rule (configuration, freely recreatable) — so `loot-filter-ui` A4's zero-ceremony pattern is too light. But unlike a Forge dismantle/sell (which can destroy value for a purely scalar or lesser return), Hatching converts a core 1:1 into a definite, positive, permanently-safe asset (`creature-jobs-evolution-system` A7: no permadeath, a bound creature is never lost) — so `forge-ui`'s heaviest multi-step gate would misrepresent the actual (low) risk. A single explicit confirm, matching the weight already used elsewhere in this session for "turn off Epic protection" (`loot-filter-ui` §3.6), is the correctly-calibrated middle tier. |
| A9 | **Branch Readiness is a new, UI-only display formula (§4 Formula 1)**: a continuous percentage per branch, computed as the minimum (bottleneck) of each of Formula 4's independently-thresholded vectors, expressed as a percentage. It never redefines Formula 4's boolean AND-gate semantics — it is a display-layer lens on the same four inputs. | Design Task 3 explicitly asks "how close am I" for a branch; `creature-jobs-evolution-system` Formula 4 only returns a boolean. A continuous, bottleneck-driven percentage is the natural, honest display of an AND-gate (the whole gate is only as close as its worst vector) and lets this document name the specific blocking vector in plain language, which a single aggregate score would hide. |
| A10 | **Production Contribution for Defender/Support renders only a qualitative "Team Boost: Active/Inactive" badge, never a fabricated percentage.** Attacker/Crafter/Producer render `role_output_per_tick` (Formula 5) directly, unmodified. | Formula 5's own field table states Defender/Support's output "is consumed as a multiplier fed to *other* teammates' formula, not stored directly" — but never defines that multiplier's magnitude; that number belongs to `region-mastery-automation-system`, which actually combines team members. Inventing a percentage here would be exactly the kind of hand-waving `.claude/docs/coding-standards.md` forbids. Flagged as genuinely thin, not padded. |
| A11 | **`simplified_ui_density`'s primary/advanced split for this screen** (delegated by `accessibility-settings-system` §3.6, which explicitly names `creature-roster-ui`) is resolved here: primary tier = portrait, Source medallion, Role badge, display name, work-state badge (state + cause + fix, already a single concise line), region assigned, Branch Readiness percentage + bottleneck vector name, power tier, charm-equipped indicator. Advanced tier (hidden by default under this setting, inside the Detail accordion) = the per-vector progress breakdown (individual Material/Job/Combat/Equip bars and their raw X/Y numbers) and the Charm Picker's source/modifiers/enchantments detail. | Fulfills the exact obligation `accessibility-settings-system` left open for this screen, matching `loot-filter-ui` A7's own resolution of the identical delegation for its screen — the primary tier is always sufficient to make an informed decision; the advanced tier is diagnostic depth, not required information. |

---

## 1. Overview

The Creature Roster UI is the player-facing home for every bound creature in IDLExIDLE — the
single screen where a player reviews their whole team, diagnoses and fixes a stalled worker,
understands and pursues a creature's next evolution branch, places a creature into a region and job
slot, equips its one charm, and mints brand-new creatures from held `creature_core` items via Core
Hatching. Per art-bible §2/§7.6, this is the game's **"glyph ledger"** — a dense, tabular, zero-
ceremony screen (matching `loot-filter-ui`'s own density discipline) whose richness lives entirely in
per-row glyph content (Source medallion, Role badge, roster portrait), never in frame ornament. This
document owns five things: the **roster list** (dense, scannable, glyph-first, filterable, sortable,
searchable, built for a roster that may hold dozens of creatures); the **work-state display** that
lets a player spot the one creature in trouble, understand *why*, and know *what to do*, without
opening anything; the **evolution tree view**, the single most important surface on this screen,
which makes Pillar 5 legible by showing exactly where a creature stands and exactly what action
closes the gap to any visible branch; the **region/slot assignment** and **charm equip** interfaces,
both fully keyboard-operable with no drag-only path; and **Core Hatching**, the screen's entry point
for turning a defeated creature's core into a new colleague.

---

## 2. Player Fantasy

> **A keeper reading a ledger of living things — not a spreadsheet of stats, a team you shaped.**

Art-bible §2 names this screen's mood exactly: "the calm attentiveness of a keeper reading a ledger
of living things." This document's entire job is to make that attentiveness *rewarding* rather than
merely calm — a keeper who reads carefully should always be able to act on what they read. Three
concrete promises follow directly from `creature-jobs-evolution-system`'s own Player Fantasy (§2
there: "nothing is a trophy, nothing is a roll, nothing is silently punished") and this document
exists to make each one *legible on this exact screen*, not just true somewhere in the simulation:

- **Nothing is a trophy** — every row shows a creature doing a real, named job (or plainly not yet
  assigned to one), never a static collection-only entry. The role-glyph roster (art-bible §2's own
  signature visual) reads as a working team, not a gallery.
- **Nothing is a roll** — the Evolution Tree View is where "what it becomes is your doing" either
  becomes a plan the player can execute or stays a mystery the player resents. This document treats
  that legibility as non-negotiable: a player looking at any discovered branch must be able to name
  the exact action that gets them closer, in the game's own words, without a wiki.
- **Nothing is silently punished** — a Blocked or Starved creature is never just a red icon. It is a
  named cause and a named fix, visible in the same glance that spots the problem, so "my creature is
  in trouble" and "I now know what to do about it" happen in the same beat, not two separate ones.

A player who has never opened the Evolution Tree accordion and only ever glances at the roster to
confirm everyone is Hearth-Gold-healthy should still feel the screen is quietly, legibly working *for*
them. A player who opens it deliberately to plan a specific creature's next branch should find every
fact they need already there, in the ledger, never behind an external guide.

---

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This document owns: screen layout and ceremony tier, the roster row pattern (collapsed and
Detail-accordion-expanded), sort/filter/search/grouping at scale, the work-state display language,
the Evolution Tree View's layout and the branch-readiness comprehension problem, the region/slot
assignment interface, the charm equip interface, and Core Hatching's interface. It explicitly does
**not** own, and defers to the cited document in every case: evolution eligibility rules and the
Healthy/Blocked/Starved cause logic (`creature-jobs-evolution-system`, cited by exact formula/section
name, never redefined), the `CreatureTemplate`/`CreatureInstance`/`EvolutionTree` data shape
(`creature-data-schema`), item fields and enums (`item-data-schema`), Region/slot capacity and the
Mastered-gate check itself (`region-mastery-automation-system` — this document
renders that system's output, never invents it), and the settings surface and binding requirements
this screen must honor (`accessibility-settings-system`).

### 3.1 Screen Architecture & Persistent Elements

**Ceremony tier: None**, cited directly from art-bible §7.6's table ("Creature roster / management —
None (chrome) — Dense tabular content... Richness lives entirely in content — the Source medallions
and role badges rendered per-row — never in frame ornament"). No row, no branch card, no Hatch tile
ever earns heavier frame ornament than any other — importance is carried by glyph content and by the
Attention banner, never by decoration.

**Always visible, in every state of this screen** (a fixed-height header, matching `loot-filter-ui`
§3.1's persistent-header precedent):

- **Roster count summary** — Data-face tabular text: `"N creatures, M assigned, K need attention"`.
- **Attention banner** — present only when `K > 0` (§3.3): a chrome banner naming the count plus a
  **"View"** shortcut that applies the Work-State filter (`blocked` + `starved`) and sorts those rows
  to the top, without discarding the player's own prior sort/filter selection (restorable via a
  **"Clear"** control that reappears alongside it).
- **Hatch button** — a chrome button, always reachable, switching the main content region to the
  **Hatch** state (§3.9) regardless of how many bound creatures already exist.
- **Search field** — chrome text input, matching against `display_name` (nickname or template name),
  Source, Role, and region name.
- **Sort control** — chrome dropdown (§3.4).
- **Filter controls** — chrome multi-select chips: Source, Role, Work-State, Region, "Unassigned
  only," "Fully Matured" (§3.4).
- **Group-by toggle** — chrome control: None / By Region / By Role (§3.4).

**Main content region** toggles between exactly two states (A2): **Roster List** (the dense table,
§3.2–§3.6) and **Hatch** (§3.9). Roster List is the default whenever at least one creature is bound;
Hatch is the default the instant the roster has zero bound creatures (Edge Case 2), matching
`loot-filter-ui` §3.1's identical empty-state-default pattern. Switching between the two is instant,
no transition motion, per art-bible §7.3's utility-screen motion rule.

### 3.2 The Roster Row — Shared Pattern (Collapsed and Detail-Expanded States)

Every creature renders from one shared row pattern (art-bible's "role-glyph roster" signature visual,
§2), so a player learns it once and reads a dozens-long roster the same way they read one entry.

**Collapsed (default) state**, left to right:

1. **Roster portrait thumbnail** — the separate, cropped close-up asset from art-bible §5.6 (never a
   shrunk copy of the combat sprite), sized for row density.
2. **Source medallion** — the closed circular medallion glyph (art-bible §3.2/§4.2), fixed for the
   creature's lifetime (`creature-data-schema` A1).
3. **Role badge** — the shield/quatrefoil container (art-bible §3.2), reflecting the creature's
   *current* Role (its current `EvolutionNode`'s `resulting_template_id.role`,
   `creature-jobs-evolution-system` §3.1 — never independently settable).
4. **Display name** — nickname override if set, else the current template's `display_name`
   (`creature-data-schema` §3.5).
5. **Work-state badge** (§3.3) — a single-line, always-text-plus-glyph badge combining state and
   cause: `"Healthy"` / `"Blocked — output has nowhere to go"` / `"Starved — no input"` /
   `"Unassigned"` (a fourth, non-work-state row per `creature-data-schema` invariant 8, distinct from
   the three enum values). The thumbnail's glyph-pulse channel (A4) animates in sync.
6. **Region assignment** — the assigned region's name, or `"Unassigned"` if `assigned_region_id` is
   null, each with an inline **Assign** / **Change** action (§3.7).
7. **Evolution** — the current node's `branch_label` (or the template `display_name` if at the tree
   root) plus a compact Branch Readiness bar (Formula 1) toward the *nearest* (highest-percentage)
   currently-discovered branch, or a `"Fully Matured"` badge if the current node has zero children
   (Edge Case 5).
8. **Power tier** — Data-face tabular number (`creature-data-schema` §3.5).
9. **Charm** — a small equipped-charm icon + tooltip name if `equipped_charm_id` is set, else an
   empty-slot `"+"` icon (§3.8).
10. **Expand toggle** — click or Enter on any row (or a dedicated chevron control) opens the Detail
    accordion below it.

**Interaction states** (identical five-state matrix to `loot-filter-ui`/`forge-ui`, reused verbatim):
Available, Hover, Keyboard-Focus (mandatory visible indicator, WCAG 2.4.7), Selected, Disabled — all
built from value-step and border-weight within the three-color chrome budget (art-bible §4.5,
`accessibility-settings-system` §3.7 item 1).

**Detail-expanded state**, opened via the Expand toggle: the row grows in place (accordion, never a
separate modal or screen — A2) to show, top to bottom:

- **Header strip**: a larger roster portrait, an editable nickname field (defaults to the template
  `display_name`), Source medallion + name, Role badge + name, power tier.
- **Work-State detail** (§3.3) — the full diagnosis and fix text, expanded from the collapsed badge's
  one-liner.
- **Evolution Tree View** (§3.6) — the tree diagram and branch cards. This is the largest section of
  the accordion by design (Design Task 3's "most important thing on this screen").
- **Region / Slot Assignment** (§3.7) — current assignment plus a **Change Assignment** action.
- **Charm** (§3.8) — current charm tile plus **Change Charm** / **Unequip** actions.
- **Collapse** — returns the row to its collapsed state; any nickname edit is saved on collapse or on
  blur of the nickname field, whichever comes first (no separate save step).

### 3.3 The Work-State Display — Cause and Fix, Not Just Status

Per Design Task 2, the roster must communicate not just *that* something is wrong but *what* is wrong
and *what to do* — reusing `creature-jobs-evolution-system` §3.5's own cause/fix table verbatim, never
paraphrased into vaguer language:

| `work_state` | Row Badge Text | Thumbnail Glyph Behavior (art-bible §5.3, A4) | Detail-Accordion Fix Text |
|---|---|---|---|
| `healthy` | `"Healthy"` | Pulses on schedule, Hearth Gold | `"Working normally."` (no fix needed) |
| `blocked` | `"Blocked — output has nowhere to go"` | Holds steady, non-pulsing, desaturated toward Cold Slate | `"Expand storage, or process the backlog (sell/craft/collect)."` (exact language from `creature-jobs-evolution-system` §3.5) |
| `starved` | `"Starved — no input"` | Slow, irregular distress pulse, Hearth Gold with Ember Threat edge-flicker | `"Route or produce the missing input (assign a generator role, or restock manually)."` (exact language from `creature-jobs-evolution-system` §3.5) |
| `null` (unassigned) | `"Unassigned"` | No work-loop animation at all — a static rest pose, distinct from all three working states per art-bible §5.4's hostile/bound/rare distinguishing rules extended to this fourth UI-only case | `"Assign this creature to a Mastered region to put it to work."` |

**All four states are distinguishable by text alone** — the badge text is never color-only, satisfying
`accessibility-settings-system` §3.7 item 4 directly and Acceptance Criterion 2 (§8). The Attention
banner (§3.1) aggregates the count of `blocked` + `starved` rows specifically (never `null`
unassigned rows, which are a planning state, not a fault state) — an unassigned creature is not "in
trouble," it is simply waiting on a player decision.

### 3.4 Sort, Filter, Search, Grouping — Scale

- **Search** (chrome field, §3.1) matches case-insensitively against display name, Source, Role, and
  assigned region name — a live, instant view filter only.
- **Sort** options: Recently Hatched (default — newest first, so a just-Hatched creature is
  immediately visible without hunting, and stays consistent with §3.9's auto-scroll-to-new-row
  behavior), Alphabetical, Role, Source, Work-State Severity (Starved → Blocked → Unassigned →
  Healthy), Region, Power Tier.
- **Filter** chips (multi-select, AND-combined): Source (6 values), Role (5 values), Work-State
  (Healthy / Blocked / Starved / Unassigned), Region (populated from the player's own assigned
  regions), "Unassigned only," "Fully Matured."
- **Grouping**: an optional "Group by Region" or "Group by Role" display toggle reorganizes the
  visual list into chrome-divided sections while preserving the active sort order *within* each
  group — a strictly visual convenience, matching `loot-filter-ui` §3.8's identical "Group by Action"
  precedent and its explicit warning that grouping never changes underlying state.
- **`simplified_ui_density`** (A11): collapsed rows already show only primary-tier fields; the Detail
  accordion's Evolution Tree branch cards hide their per-vector breakdown behind an in-card "Show
  details" toggle, surfacing only the overall Branch Readiness percentage and the bottleneck vector's
  name plus its single fix-action line.
- **Scale**: the Roster List renders via a virtualized/paginated list (§7 `roster_page_size`), so a
  roster of dozens of creatures never degrades scroll performance or keyboard-navigation latency.

### 3.5 Creature Detail — Overview Fields

Beyond the sections specified in §3.2, §3.6–§3.8, the Detail accordion's header strip is the one place
a player edits a creature's nickname (`CreatureInstance.display_name`, `creature-data-schema` §3.5) —
a simple text field, 1–24 characters, optional (empty reverts to the current template's
`display_name`). No other overview field is editable here; Source, Role, power tier, and instance
identity are all read-only displays of upstream schema fields, never mutated by this screen.

### 3.6 The Evolution Tree View

**This is the most important thing on this screen** — the concrete, testable proof of Pillar 5. It
answers three questions in one glance: where is this creature now, what branches exist, and what do I
need to *do* to reach the one I want.

**Layout**: a small tree diagram — the creature's **current node** (its portrait, Source medallion,
Role badge, wrapped in the closed double-ring Ward-seal container from art-bible §5.4's "Bound /
working" glyph, since this creature *is* bound and working) connected by simple lines to one branch
card per currently-discovered child node (A3 — every direct child of the current node, always).

**Each branch card** shows:

- The destination Role badge + `branch_label` (`creature-data-schema` §3.4) — the Source medallion is
  omitted on branch cards, since every node in one tree shares the same Source
  (`creature-data-schema` A1) and repeating it on every card would be pure redundancy, not
  information.
- **Four per-vector rows**, each a progress bar plus an `"X / Y"` label, reading `creature-jobs-
  evolution-system`'s live `EvolutionProgressRecord` fields directly (§3.3 there):
  - **Material**: `evolution_progress` vs. this node's `material:<int>` gate. Shared, identical value
    across every sibling card (one scalar meter feeds every branch simultaneously) — the UI states
    this explicitly ("Material progress is shared across every branch") so a player never mistakes it
    for a per-branch number.
  - **Job Diligence**: `healthy_work_ticks[current_role]` vs. this node's `job:<role>:<int>` gate.
    Also shared across siblings (the role gate is always the *parent* node's Role, per
    `creature-jobs-evolution-system` §3.4's own authoring-time validation rule).
  - **Combat Behavior**: `combat_behavior_tally[tag]` vs. this node's `combat:<tag>:<int>` gate.
    **Differs per branch** — this and Equip are the two vectors that actually distinguish sibling
    cards from each other.
  - **Equip** (only rendered if this node has an `equip:<trait>` gate): a met/unmet chip naming the
    required trait, checked live against the creature's *currently equipped* charm
    (`creature-jobs-evolution-system` §3.3 Vector 2 — evaluated live, not accumulated).
- **Branch Readiness bar** (Formula 1, §4) — the bottleneck percentage across all present vectors,
  with the blocking vector named directly: `"62% ready — bottleneck: Combat Behavior (1/3 Clean
  Kills)"`.
- **One fix-action line**, generated from the bottleneck vector alone (never all four at once, to keep
  the single most useful next step unambiguous):
  - Material bottleneck → `"Feed this creature more materials — matching its [Source] source gives a
    1.5× bonus."`
  - Job bottleneck → `"Keep this creature assigned and Healthy at its current job — N more work
    cycles needed."`
  - Combat bottleneck → `"Feed N more [tag display name] creature cores."`
  - Equip bottleneck → `"Equip a charm with the [trait] trait."` — if the player's inventory currently
    holds a qualifying charm, the fix line links directly into the Charm Picker (§3.8) with that charm
    pre-highlighted.
- **Simultaneous-eligibility tie-break, surfaced** (Edge Case 1): if two or more sibling cards are
  simultaneously at 100% Branch Readiness, the card with the lowest `branch_priority`
  (`creature-jobs-evolution-system` §3.3/§3.4) shows a `"Will trigger next"` badge; every other
  simultaneously-ready sibling shows `"Also ready — resolves after [winning branch's label]"` — never
  silent, matching Player Fantasy's "nothing is a roll" promise even at the moment of the tie itself.

**Fully Matured state** (Edge Case 5): if the current node has zero children — either a genuine leaf
reached through normal evolution, or a hatched creature whose template has no `evolution_tree_id` at
all (`creature-data-schema` Edge Case 12 / `creature-jobs-evolution-system` Edge Case 10, treated
identically) — the tree diagram renders a single card in place of the branch list: `"Fully Matured —
this creature has reached the end of its evolution path."` No further per-vector tracking is shown,
though `creature-jobs-evolution-system` Edge Case 8 confirms the underlying counters keep
accumulating harmlessly in the background.

### 3.7 Region / Slot Assignment

Per `creature-jobs-evolution-system` §3.1, this assigns **where** a creature works, never **what** it
does (Role is evolution-gated, never freely reassignable) — the interface reflects that framing
directly: the picker is titled "Assign to Region," never "Change Job."

**Interaction** (A7 — no drag path anywhere in this flow):

1. **Assign** / **Change Assignment** (row or Detail accordion) opens the **Region Picker**: a list of
   every region the player has encountered, each row showing its name and Mastery status. Regions
   that do not report Mastered render grayed with an inline reason, `"Not yet Mastered"`, and are not
   selectable — enforcing `creature-jobs-evolution-system` §3.2 step 3's deployment gate at the UI
   layer (the actual gate check itself is `region-mastery-automation-system`'s; this
   screen only renders its output).
2. Selecting a Mastered region opens that region's **Slot Grid** — a small grid of slot tiles, whose
   count and any per-slot role restriction are supplied entirely by `region-mastery-automation-system`
   (this document invents neither). Each tile shows empty, or the currently assigned creature's
   thumbnail + name if occupied.
3. Selecting an **empty** slot assigns the creature being configured immediately — no confirm needed
   (a non-destructive, freely reversible placement).
4. Selecting an **occupied** slot prompts a lightweight swap confirm: `"Replace [Creature B] in this
   slot with [Creature A]? [Creature B] becomes unassigned."` — Creature B is never deleted or harmed,
   only returned to the unassigned pool, consistent with `creature-jobs-evolution-system` A7's no-loss
   guarantee.
5. **Unassign** (available directly from the Detail accordion, no picker needed) sets
   `assigned_region_id = null`, which per `creature-data-schema` invariant 8 also nulls `work_state` —
   the row immediately re-renders as `"Unassigned"` (§3.3).

Every step above is a standard click-or-keyboard-Enter selection through an ordinary list/grid — no
drag gesture exists on this screen to retrofit a keyboard equivalent onto, satisfying art-bible
§7.7's binding requirement by construction rather than by exception-handling.

### 3.8 Charm Equipping

One slot per bound creature (`creature-jobs-evolution-system` §3.3 Vector 2, locked at 1 for MVP).

**Interaction**:

1. **Change Charm** (Detail accordion, or the row's Charm icon) opens the **Charm Picker**: every
   `active` Item Instance in the player's inventory with `base_type = charm`, **excluding** any item
   with a non-empty `vow_binding_log` (A5) and **excluding** any item already equipped to a *different*
   creature, shown instead as a disabled tile with the reason `"Already equipped to [Creature]"` (A6)
   — never silently hidden, since a player should be able to see *why* a charm isn't offered, matching
   this session's established "never a leap of faith" transparency standard.
2. Each offered tile shows the charm's name, rarity ring (art-bible §4.3), and its `source`/
   `modifiers`/`enchantments` summary. If the Detail accordion's Evolution Tree View (§3.6) currently
   has an equip-gated branch card visible, any charm in the picker that would satisfy that branch's
   `equip:<trait>` gate is marked `"Matches [Branch Label]'s requirement."` — directly connecting the
   charm-selection decision to the evolution-planning decision that likely motivated it.
3. Selecting a charm equips it **immediately** — free, instant, non-destructive
   (`creature-jobs-evolution-system` §3.3 Vector 2: "equipping is free, instant, and non-destructive —
   the charm is worn, not consumed, and can be swapped at will"). No confirm step, matching that
   exact framing.
4. **Unequip** (a direct action, always available when a charm is equipped) clears
   `equipped_charm_id` to null, instantly, with no confirm — the charm returns to being an ordinary,
   fully available inventory item.

**Defensive re-validation** (A6): every time this screen opens or a row's Detail accordion regains
focus, the currently equipped charm's underlying item status is re-checked. If that item is no longer
`active` (consumed by a Forge operation the current cross-system gap did not prevent), the slot clears
to empty automatically and a one-time, non-blocking notice appears: `"[Creature]'s charm was
consumed elsewhere and has been unequipped."`

### 3.9 Core Hatching

**Placement** (A1): reachable at all times via the persistent **Hatch** header button (§3.1), and the
default main-content-region view whenever the roster has zero bound creatures (Edge Case 2).

**Hatch panel contents**: every `active` `creature_core` Item Instance in the player's inventory,
rendered as a grid of tiles. Each tile shows: a preview icon of the template it will hatch into
(derived from `definition_id` per `creature-jobs-evolution-system` §3.2 step 2's reversed-derivation
rule), the item's own rarity ring (item rarity — a separate concept from the creature's future
evolution progress), and a **Hatch** action. The panel supports the same Source/rarity filter chips as
the main roster header, reused verbatim for consistency.

**Hatching a core** (A8 — single explicit confirm, no Undo):

1. Selecting **Hatch** on a tile shows one confirm dialog: `"Hatch this [preview name] core into a new
   creature? This cannot be undone."` — **Confirm** / **Cancel**.
2. Confirming consumes the core (`item-data-schema` §3.5: `status` transitions `active` → `archived`)
   and mints a new `CreatureInstance` exactly per `creature-jobs-evolution-system` §3.2 step 2:
   `template_id` from the core's origin template, `evolution_state.current_node_id` at the tree's
   `root_node_id`, `bind_state = bound`, `bound_state.assigned_region_id = null`, a zeroed
   `EvolutionProgressRecord`, and (per `creature-data-schema` Edge Case 5's own default, which nothing
   in the Hatching flow overrides) `power_tier = 1`.
3. The main content region switches back to **Roster List**, sorted by the default Recently Hatched
   order (§3.4) so the new creature appears first, with its row's Detail accordion **auto-expanded** —
   directly reusing `loot-filter-ui` §3.3's "new row opens directly into its expanded edit state"
   precedent, so the player can immediately nickname, inspect the tree, or assign the creature with no
   extra navigation step.

### 3.10 Keyboard & Gamepad Navigation

- **Full keyboard/gamepad operability, no exceptions** (`accessibility-settings-system` §3.7 item 3):
  every interactive element — header controls, every row's expand/collapse and inline actions, the
  Evolution Tree View's branch cards and their fix-action links, the Region Picker and Slot Grid, the
  Charm Picker, and the Hatch panel — is reachable and operable without a mouse or pointer.
- **Visible keyboard-focus indicator** (WCAG 2.4.7) on every one of the above, built from
  value-step/border-weight within the chrome budget (`accessibility-settings-system` §3.7 item 1).
- **Standard menu-navigation model, not combat's cycle-and-confirm**: this is a planning screen, not a
  combat-targeting surface, so gamepad navigation uses ordinary D-pad/left-stick focus movement plus a
  confirm face button — art-bible §7.7's cycle-and-confirm decision is specific to combat part
  targeting and does not apply here.
- **Fixed logical navigation order**: header (Attention banner if present → Hatch button → Search →
  Sort → Filters → Group toggle) → Roster List content (each row, in current sort order: portrait/
  medallion/badge — non-interactive → name → work-state badge → region assign action → evolution
  summary/expand trigger → power tier — non-interactive → charm icon → expand toggle) → within an
  expanded row: nickname field → Evolution Tree branch cards (in `branch_priority` order) → Change
  Assignment → Change Charm → Collapse.
- **No simultaneous multi-button input anywhere** (`accessibility-settings-system` §3.7 item 8).

---

## 4. Formulas

Two formulas are original to this document — both UI-owned display-layer derivations over
`creature-jobs-evolution-system`'s own rules, cited by exact name and never redefined, matching
`loot-filter-ui` §4's own precedent for what a Presentation-tier document is allowed to add.

### Formula 1 — Branch Readiness Percentage

```
vector_pct(v, N) = min(100, round(100 × current(v) / threshold(v, N)))      for material/job/combat
equip_pct(N)     = 100  if no equip:<trait> gate on N, or the currently equipped charm satisfies it
                 = 0    if N has an equip:<trait> gate and it is unsatisfied

branch_readiness(N) = min( vector_pct(material, N), vector_pct(job, N), vector_pct(combat, N), equip_pct(N) )
bottleneck(N) = the vector(s) achieving that minimum
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `N` | `EvolutionNode` | one discovered child of the current node | Candidate branch card. |
| `current(v)` | number | ≥ 0 | The live value of vector `v` — `evolution_progress`, `healthy_work_ticks[current_role]`, or `combat_behavior_tally[tag]`, read directly from `creature-jobs-evolution-system` §3.3's `EvolutionProgressRecord`. |
| `threshold(v, N)` | number | > 0 (or absent) | The `material:`/`job:`/`combat:` gate value parsed from `N.unlock_condition_tags`, per `creature-jobs-evolution-system` §3.4's tag grammar. A vector absent from `N`'s tags (e.g. no `combat:` gate on this branch) is treated as `vector_pct = 100` — an ungated vector can never be a bottleneck. |
| `vector_pct(v, N)` | int | [0, 100] | Per-vector display percentage, clamped at 100 (overshoot never displays above full). |
| `equip_pct(N)` | int | {0, 100} | Binary — the equip vector has no partial-credit concept (`creature-jobs-evolution-system` Formula 4 checks it as a live boolean). |
| `branch_readiness(N)` | int | [0, 100] | The displayed Branch Readiness percentage — the bottleneck of all present vectors. |
| `bottleneck(N)` | set of vector names | 1+ | Which vector(s) tie for the minimum; drives the single fix-action line (§3.6). |

**Relationship to Formula 4**: `branch_readiness(N) = 100` for every present vector if and only if
`creature-jobs-evolution-system` Formula 4's `node_eligible(N) = true` — this formula is a strictly
finer-grained, continuous view of the exact same four inputs, never a competing definition.

**Worked example 1 (full eligibility — reusing Formula 4's own worked example exactly)**: Verdant
Whelp → Verdant Brawler, gated `material:600` / `job:producer:300` / `combat:clean_kill:3`, no equip
gate. Given `evolution_progress = 640`, `healthy_work_ticks[producer] = 310`,
`combat_behavior_tally[clean_kill] = 4`:

```
vector_pct(material) = min(100, round(100 × 640/600)) = 100
vector_pct(job)       = min(100, round(100 × 310/300)) = 100
vector_pct(combat)    = min(100, round(100 × 4/3))     = 100
equip_pct              = 100   (no equip gate on this branch)
branch_readiness = min(100, 100, 100, 100) = 100
```

Matches `creature-jobs-evolution-system` Formula 4's `node_eligible = true` for this exact fixture.

**Worked example 2 (partial progress, bottleneck named)**: same branch, given
`evolution_progress = 300`, `healthy_work_ticks[producer] = 150`,
`combat_behavior_tally[clean_kill] = 1`:

```
vector_pct(material) = min(100, round(100 × 300/600)) = 50
vector_pct(job)       = min(100, round(100 × 150/300)) = 50
vector_pct(combat)    = min(100, round(100 × 1/3))     = 33
branch_readiness = min(50, 50, 33) = 33   →   bottleneck = {combat}
```

Displayed: `"33% ready — bottleneck: Combat Behavior (1/3 Clean Kills)."`

### Formula 2 — Production Contribution Display

```
For Attacker / Crafter / Producer:
  displayed_contribution = role_output_per_tick        (creature-jobs-evolution-system Formula 5, unmodified)

For Defender / Support:
  displayed_contribution = "Team Boost: Active"   if work_state = healthy
                          = "Team Boost: Inactive" otherwise
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `role_output_per_tick` | number | ≥ 0 | `creature-jobs-evolution-system` Formula 5's output, rendered as-is — this document performs no transformation on it. |
| `displayed_contribution` | number \| enum{Active, Inactive} | — | What the row/Detail accordion actually shows, branching entirely on Role. |

**Genuinely thin, stated explicitly (A10)**: no numeric magnitude exists anywhere in the currently
locked design for what a Defender's stability boost or a Support's yield multiplier is actually
*worth* to a teammate — that number belongs to `region-mastery-automation-system`.
This document declines to invent one and renders only the true underlying signal it does have
(whether the multiplier is currently being applied at all, per `work_state`).

### Non-Formulaic Mechanisms (stated explicitly, not padded)

**The Attention count** (§3.1 header) is a plain count of rows where `work_state ∈ {blocked,
starved}` — no weighting, no severity score. **Search matching** (§3.4) is a case-insensitive
substring match with no ranking, identical in kind to `loot-filter-ui` §3.8's own search. **Sort** and
**grouping** are stable, deterministic array orderings with no derived math.

---

## 5. Edge Cases

1. **A creature simultaneously meets the full gate-set for two sibling branches.** Resolved
   identically to `creature-jobs-evolution-system` §5 Edge Case 1: ascending `branch_priority`
   resolves the tie. **Surfaced explicitly** (§3.6): the winning card shows `"Will trigger next"`; every
   other simultaneously-ready sibling shows `"Also ready — resolves after [winning branch]."` Never
   silent — this is the concrete UI answer to the brief's explicit instruction to align and surface
   this rule.
2. **The roster is empty.** Two distinct sub-cases, never conflated:
   - **Zero bound creatures AND zero held `creature_core` items** (only reachable before the player's
     first kill, since `loot-drop-system` guarantees a 100%-drop-rate core on every defeat). The Hatch
     panel (default view, §3.9) shows an onboarding message instead of an empty grid: `"No creatures
     yet. Defeat a creature to receive a Creature Core, then return here to Hatch it."` No Hatch action
     is offered because none is possible.
   - **Zero bound creatures but at least one held core.** The Hatch panel shows the real, hatchable
     core tiles immediately — no separate onboarding copy needed, since the action is already
     available.
3. **A creature is assigned to a region that has since become un-mastered.** No such mechanic exists
   in any currently locked design document — Mastery is treated as a one-way, permanent state
   everywhere it is described (art-bible §2/§6.5, `creature-jobs-evolution-system` §3.2 step 3). This
   is defined defensively, for forward-compatibility only, in case a future design introduces one: the
   row would render a `"Region Unavailable"` badge in place of the normal work-state badge, freeze
   `work_state` rendering at its last-known value rather than guessing, and offer only a **Reassign**
   action — never silently reassigning or dropping the creature. The actual trigger and semantics of
   "un-mastering," if it is ever designed, belong entirely to `region-mastery-automation-system`; this
   document only commits to rendering its output gracefully.
4. **A charm is equipped to a creature and is also targeted by a Forge merge/sell/dismantle/feed
   action.** **Ruling: structurally impossible. RESOLVED 2026-07-14 (`/review-all-gdds` Scenario 3).**

   > **This edge case previously described an OPEN gap. It is now closed at the data model.**
   > `item-data-schema` field 16 (`equipped_to_creature_id`) + **Invariant 9** make a creature-equipped
   > item ineligible for **every** Forge operation, and `the-forge-system` **§3.5.0 (Universal Input
   > Gate)** enforces it across merge, sell, dismantle, and feed. `forge-ui` **A8b** excludes such items
   > from all four pickers entirely — impossible to attempt, not merely rejected.

   **This screen's remaining obligations:**
   - On **equip**, write `equipped_to_creature_id = <this creature's instance_id>` on the item, and
     `equipped_charm_id = <item_instance_id>` on the creature. These are a **reciprocal pair and must
     never disagree** — exactly one creature may reference a given charm at a time.
   - On **unequip**, clear both sides. **Unequipping is a Roster action only** — the Forge never
     silently unequips an item in order to consume it.
   - The defensive re-validation on screen open/focus (§3.8) is **retained as a belt-and-braces
     safeguard** against a bug that desynchronizes the reciprocal pair — but it is no longer the
     primary defense, and it should never fire in correct operation. If it fires, that is a bug in the
     equip/unequip transaction, not an expected race.
   - The Charm Picker continues to prevent double-equip across two creatures (self-contained, unchanged).
5. **A creature is at a leaf node (fully matured) or was hatched from a template with no
   `evolution_tree_id`.** Both treated identically (`creature-data-schema` Edge Case 12 /
   `creature-jobs-evolution-system` Edge Case 8/10): the Evolution Tree View renders the single
   `"Fully Matured"` card (§3.6) in place of branch cards. Continued feeding remains legal and harmless
   (counters keep accumulating with no further gate to consume them) — this document does not block
   the Feed action itself, only reflects that no branch is currently reachable.
6. **A creature has never been assigned to a region** (`assigned_region_id = null`, `work_state =
   null`). Rendered as the fourth, non-enum `"Unassigned"` badge (§3.3) — distinct from Healthy/
   Blocked/Starved, never miscounted into the Attention total (§4's Non-Formulaic Mechanisms), and
   never silently defaulted to any of the three real work states.
7. **A player attempts to equip an item already equipped to a different creature.** Prevented
   structurally by the Charm Picker (§3.8, A6) — the item appears as a disabled tile with an explicit
   `"Already equipped to [Creature]"` reason, never simply omitted and never allowed to silently
   double-equip.
8. **A player attempts to equip a Vow-bound item.** Excluded from the Charm Picker entirely (A5),
   mirroring `creature-jobs-evolution-system` Edge Case 7's defensive Feed-path exclusion by direct
   analogy — the item never appears as a selectable option, so there is no rejection state to render.
9. **Two evolution branches present the identical bottleneck vector.** No special handling — each
   card's fix-action line is generated independently per branch (§3.6); if both name the same action
   (e.g. "feed more materials"), that is accurate and expected, not a bug to suppress.
10. **A very large roster (dozens of creatures) is opened.** Handled by virtualized/paginated
    rendering (§3.4, §7 `roster_page_size`) — sort, filter, search, and the Attention banner all
    operate over the *full* roster regardless of what is currently rendered on-screen, so a Starved
    creature past the first page is never invisible to the Attention count even if it isn't currently
    scrolled into view.
11. **A player Hatches a core while the Roster List is mid-filter/sort/search.** The new creature is
    appended to the full underlying roster immediately; the view then re-sorts to the default Recently
    Hatched order and auto-expands the new row (§3.9) — any active filter that would exclude the new
    creature (e.g. a Work-State filter, since the new creature starts `"Unassigned"`) is explicitly
    **cleared**, not silently hiding the creature the player just created, with a brief inline notice
    (`"Filters cleared to show your new creature."`).
12. **`ui_scale_percent` is set to 200% on this dense screen.** Resolved identically to
    `loot-filter-ui`'s own equivalent case: the Roster List and the Evolution Tree View's branch-card
    grid reflow to vertical (row count) and, where needed, horizontal (branch-card row) scrolling
    rather than overlapping or silently truncating any field — per `accessibility-settings-system` §5
    Edge Case 1's explicit data-table/reflow exception.

---

## 6. Dependencies

**Depends on**:

- **`creature-jobs-evolution-system.md`** — the entire foundation this screen renders: the five
  evolution influence vectors and `EvolutionProgressRecord` shape (§3.3), the tag grammar and Formula
  4 AND-gate this document's own Formula 1 extends (§3.4, §4), the work-state machine and its exact
  cause/fix language (§3.5, reused verbatim in §3.3 above), the five Role production profiles
  (§3.6, Formula 5, this document's Formula 2), Core Hatching's exact field-population contract
  (§3.2, this document's §3.9), and `equipped_charm_id` (§3.3 Vector 2, this document's §3.8). That
  document's own Dependencies section already names `creature-roster-ui` as depended-on-by, citing
  exactly the fields this document reads — fulfilled in full above.
- **`creature-data-schema.md`** — `CreatureTemplate`/`CreatureInstance` field shape throughout,
  `EvolutionTree`/`EvolutionNode` and `discovered_node_ids` (§3.4/§3.8, this document's A3 resolves
  that field's population trigger), and every schema invariant this screen must never violate (most
  directly invariant 8, `work_state` nullability, §3.3/§3.7 above).
- **`item-data-schema.md`** — the `charm` and `creature_core` `base_type`s, `vow_binding_log`
  (§3.8/A5), and `source`/`modifiers`/`enchantments` (read for equip-gate evaluation and Charm Picker
  display, §3.8).
- **`accessibility-settings-system.md`** — every binding requirement in its §3.7 is non-negotiable
  here (§3.10 restates the ones most load-bearing for this screen); §3.6's `simplified_ui_density`
  delegation, which explicitly names `creature-roster-ui`, is resolved by this document (A11, §3.4).
- **`design/art/art-bible.md`** — §2 (Creature Management mood, the role-glyph roster signature
  visual), §3.1/§3.2 (silhouette and glyph grammar reused unmodified), §3.4/§7.6 (zero-ceremony,
  content-not-chrome), §4.2/§4.3 (Source colors, rarity ring grammar for the Hatch panel), §5.3/§7.3
  (the Healthy/Blocked/Starved pose-and-glyph-pulse spec this screen's work-state display and A4 are
  built on), §5.4 (bound/working glyph-container reuse for the current-evolution-node marker), §5.6
  (the separate roster-portrait asset), §7.7 (the non-drag-keyboard-equivalent binding requirement
  this screen's assignment interface is directly named under).

**Adjacent (informational, not a dependency in either direction)**: **`loot-filter-ui.md`** — this
document reuses its collapsed/expanded accordion row pattern, persistent-header-plus-banner
structure, and empty-state-default-view precedent as an established sibling-UI precedent, but neither
document depends on the other's gameplay data. **`the-forge-system.md`** — this document's Feed action
is that document's, not this one's (§3.9's own scope note); read here only to resolve the charm
ownership-conflict edge case (A6, Edge Case 4). **`region-mastery-automation-system`** (not yet
written) — this document renders that system's Mastery-gate output and slot-grid contents (§3.7) but
invents neither; that system's own future GDD must reference this document back for the assignment
interface it must supply data to.

**Depended on by**: none. Per `systems-index.md`, no currently-enumerated system consumes this
screen's own output — it is a leaf, Presentation-tier document.

**`systems-index.md` / cross-document gaps flagged, not self-edited** (per this session's established
file discipline):

1. `systems-index.md` entry #19 lists `creature-roster-ui` as depending only on
   `creature-jobs-evolution-system`. This document establishes real, direct dependencies on
   `creature-data-schema`, `item-data-schema`, and `accessibility-settings-system` as well, none of
   which are currently reflected there.
2. `creature-data-schema.md`'s own "Depended On By" list (§6 there) does not include
   `creature-roster-ui`, even though this document reads that schema's fields extensively. Flagged for
   a future centralized pass, matching the identical gap `combat-hud.md` already flagged for the same
   document.
3. Neither `item-data-schema.md` nor `the-forge-system.md` mentions `creature-roster-ui` anywhere —
   confirmed by direct search of both files this session. Both are genuine, currently-undocumented
   consuming relationships this document establishes.

---

## 7. Tuning Knobs

| Knob | Default | Safe Range | Affects | Notes |
|---|---|---|---|---|
| `roster_page_size` | 50 | 20–200 | Virtualized/paginated batch size for the Roster List (§3.4, Edge Case 10) | Bounds render and keyboard-navigation cost for a roster of "dozens" of creatures per the brief's own framing; too low fragments scanning, too high risks list-scroll jank on lower-end hardware. |
| `default_sort` | Recently Hatched | one of: Recently Hatched / Alphabetical / Role / Source / Work-State Severity / Region / Power Tier | Initial Roster List ordering (§3.4) | Locked as Recently Hatched for the continuity argument in §3.9 (a freshly Hatched creature is always visible without hunting); the other six remain always available as one-click alternates, not competing defaults. |
| `evolution_tree_branches_per_row` | 3 | 2–5 | Layout wrap threshold for the Evolution Tree View's branch-card row (§3.6) before it wraps to a second row or scrolls | MVP content never exceeds 3 sibling branches per node (`creature-data-schema` §7's tuning note), so the default renders every MVP branch on one row; Full-Vision content with more siblings is handled by the wrap, not a redesign. |
| `attention_banner_threshold` | 1 | locked (not a numeric range — any `K ≥ 1` shows the banner) | When the Attention banner (§3.1) appears | Not a balance lever — a single Blocked or Starved creature is exactly the case this banner exists to catch; raising this to "only show at 3+" would contradict Design Task 2's "spot the *one* creature in trouble" framing. |
| `hatch_confirm_required` | on | locked, not tunable | Whether Hatching requires the single explicit confirm click (§3.9, A8) | A correctness/consequence-weight rule (irreversible core consumption), not a feel lever — never exposed as a skippable setting. |
| `charm_picker_match_badge_enabled` | on | on / off | Whether the Charm Picker's "Matches [Branch]'s requirement" badge (§3.8) computes and displays | Recommended to stay on — directly serves Pillar 5's legibility test — but exposed in case a future performance pass finds the live cross-reference against an open Evolution Tree View measurably costly on a very large charm inventory. |

---

## 8. Acceptance Criteria

| # | Criterion | Verification Method |
|---|---|---|
| AC1 (required — Pillar 5 legibility) | For any currently-discovered evolution branch at any readiness percentage below 100%, a player can name the exact next action to increase progress (which vector, and what to do about it) using only this screen's own displayed text — no external documentation, wiki, or GDD consultation required. | Scripted walkthrough: present a fixture creature with a partial-progress branch card, ask a tester (or scripted assertion against the fix-action line, §3.6) to state the next required action; assert the on-screen fix-action text alone is sufficient and correct. |
| AC2 (required — color-disabled legibility) | With `colorblind_preview_mode = grayscale` enabled, all four work-states (Healthy/Blocked/Starved/Unassigned) remain fully distinguishable across the Roster List by text and glyph-pulse-cadence alone, with no reliance on color. | QA pass through a fixture roster containing all four states with grayscale preview active, matching `loot-filter-ui` AC12's own verification method. |
| AC3 (required — keyboard-only) | Every interaction on this screen — row expand/collapse, region assignment (both Region Picker and Slot Grid steps), charm equip/unequip, Core Hatching (confirm included), sort/filter/search/group, and nickname editing — is completable using keyboard only. | Scripted or manual walkthrough completing one full Hatch → assign → equip → evolution-branch-check cycle using keyboard input exclusively. |
| AC4 (gamepad-only) | Identical scope to AC3, substituting a single connected gamepad with no keyboard or mouse connected. | Identical walkthrough to AC3, gamepad-only. |
| AC5 (required — no drag-only) | No functionality on this screen — region/slot assignment or charm equipping specifically — is reachable exclusively via a drag gesture; both flows are fully operable via click-or-keyboard selection alone (A7). | Structural review confirming no drag-only control exists anywhere in this document's specified interactions (a stronger bar than "has a keyboard equivalent," since none is designed at all). |
| AC6 | Given the worked examples in Formula 1 (§4), `branch_readiness` computes to exactly `100` for the full-eligibility fixture and exactly `33` (bottleneck = combat) for the partial-progress fixture, matching both worked examples precisely. | Unit test against both fixtures, asserting exact percentage and bottleneck-vector-name output. |
| AC7 | Simulating two sibling branch nodes both reaching 100% Branch Readiness on the same tick renders the lower-`branch_priority` card with `"Will trigger next"` and every other simultaneously-ready sibling with `"Also ready — resolves after [winner]"` — never silent, never both cards claiming "next." | Fixture test with two siblings deliberately satisfied simultaneously, asserting badge text on both. |
| AC8 | A creature at a leaf node, or hatched from a template with a null `evolution_tree_id`, renders the single `"Fully Matured"` card and zero branch cards. | Fixture test for both origin cases (reached-leaf vs. hatched-leaf), asserting identical rendered output per Edge Case 5. |
| AC9 | With zero bound creatures and zero held `creature_core` items, the Hatch panel shows the onboarding message and no Hatch action; with zero bound creatures and at least one held core, the Hatch panel shows real, actionable core tiles instead. | Fixture test for both empty-roster sub-cases (Edge Case 2), asserting the correct one of the two renders. |
| AC10 | An item already equipped to Creature A never appears as a selectable (non-disabled) option in Creature B's Charm Picker; a Vow-bound item never appears in any Charm Picker at all. | Fixture test with a pre-equipped charm and a separate Vow-bound charm, asserting both exclusion behaviors (Edge Cases 7–8) independently. |
| AC11 | If a creature's equipped charm's underlying item transitions to `archived` status outside this screen's own control, the next time this screen (or that creature's Detail accordion) gains focus, `equipped_charm_id` clears to null and the one-time notice is shown — never a dangling reference, never a crash. | Fixture test that archives an equipped item out-of-band, then opens the affected creature's Detail accordion, asserting the slot clears and the notice fires exactly once. |
| AC12 | Attempting to assign a creature to a region that does not report Mastered is rejected at the Region Picker (the region renders non-selectable with its reason); assigning to a Mastered region with an empty slot succeeds immediately with no confirm. | Fixture test against one Mastered and one non-Mastered region, asserting the picker's selectable/non-selectable state matches exactly. |
| AC13 | Unassigning a creature (`assigned_region_id → null`) always results in `work_state = null` in the same operation, immediately reflected as the `"Unassigned"` badge — never a stale Healthy/Blocked/Starved badge surviving the unassign action. | Fixture test performing Unassign on a `healthy` creature, asserting both fields update atomically and the row re-renders correctly (`creature-data-schema` invariant 8). |
| AC14 | With `ui_scale_percent = 200`, the Roster List and the Evolution Tree View's branch-card row reflow to scroll (vertical and/or horizontal as needed) rather than overlapping or silently truncating any field's content. | Layout test at maximum scale, per `accessibility-settings-system` §5 Edge Case 1's data-table reflow exception, matching `loot-filter-ui` AC13's identical verification method. |
| AC15 | With `simplified_ui_density` enabled, every collapsed row and every evolution branch card still displays enough information (work-state badge with cause+fix, Branch Readiness percentage, bottleneck vector name and its fix-action line) to make an informed assign/equip/feed decision without expanding the "Show details" per-vector breakdown. | Structural review of the primary-tier field set against §3.4/A11's documented split. |

---

## Cross-System Facts Proposed for Registration

None as new gameplay facts — every enum, formula, and rule this document renders (the five evolution
vectors, the work-state cause/fix table, the five Role production profiles, every item/creature
schema field) is cited from `creature-jobs-evolution-system.md`, `creature-data-schema.md`, or
`item-data-schema.md` by exact name, never redefined, matching this session's established precedent.
This document's two original formulas (Branch Readiness Percentage, Production Contribution Display)
are UI-presentation derivations with no consumer outside this screen.

Several **coordination gaps** are flagged for a future centralized pass, not self-edited per this
session's file discipline (full detail in §6 above):

1. `systems-index.md` entry #19's dependency list for `creature-roster-ui` is incomplete (missing
   `creature-data-schema`, `item-data-schema`, `accessibility-settings-system`).
2. `creature-data-schema.md`'s "Depended On By" list does not include `creature-roster-ui`.
3. Neither `item-data-schema.md` nor `the-forge-system.md` mentions `creature-roster-ui` anywhere,
   despite this document's real dependency on both.
4. ~~**The equipped-to-creature ownership gap** remains open.~~ **CLOSED 2026-07-14
   (`/review-all-gdds` Scenario 3).** `item-data-schema` field 16 (`equipped_to_creature_id`) +
   Invariant 9; enforced by `the-forge-system` §3.5.0 (Universal Input Gate) across all four
   operations; `forge-ui` A8b excludes equipped items from every picker. See the revised Edge Case 4.
   The detect-and-clear mitigation is retained only as a belt-and-braces safeguard and should never
   fire in correct operation.
5. **The Vow-bound charm-equip exclusion (A5)** is a UI-level defensive ruling this document makes by
   analogy to `creature-jobs-evolution-system` Edge Case 7's Feed-path exclusion — that document's own
   Vector 2 rules-layer spec never states this exclusion explicitly. Worth restating at the rules layer
   in a future revision of that document, rather than living only in this UI document's own ruling.
6. **The `discovered_node_ids` population trigger (A3)** is this document's own resolution to an
   ambiguity `creature-data-schema` §3.8 left open (the field is defined but its trigger is not).
   Since this is arguably a rules-layer decision forced into a UI document only because this screen
   needed it to be buildable, it is worth promoting into `creature-data-schema` or
   `creature-jobs-evolution-system`'s own text in a future centralized reconciliation pass.
