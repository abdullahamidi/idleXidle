# GDD: Automation Config UI

> **SUPERSEDED 2026-09-01 by nothing — the subsystem was retired 2026-08-24.** Describes the creature-roster automation model, none of which is in the runtime. Kept for vocabulary and history — do not treat its rules as current.

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | ux-designer |
| **Status** | Complete — authored in rush mode per `production/session-state/active.md` (no user available this session, Auto Mode active). Every ambiguity is resolved with an explicit, flagged design call in the Assumptions Log below, never a placeholder. Validate at `/ux-review` and `/gate-check` before Production. |
| **Priority / Tier** | MVP, Presentation tier — 20th in the recommended design order (`design/gdd/systems-index.md` #20). Per this document's own scope, it owns the **interface** exclusively — every mechanic, formula, and state machine it renders belongs to `region-mastery-automation-system` or `creature-jobs-evolution-system` and is cited by exact name throughout, never redefined. |

## Source Material Read

`design/gdd/region-mastery-automation-system.md` (full — the region state machine, the four-tier
mastery ladder, Region Mastery Points, the Composition Completeness Score / Power Quality Score /
`team_quality_score` triad, `idle_efficiency_percent` and its locked bands, the four Automation
Capability Stages and their mastery gates, `region_storage_capacity`, the Stage-3 Gleam cap, and the
two work-state booleans this document's host system supplies — this document's entire mechanical
foundation, cited by exact field name throughout and never redefined), `design/gdd/creature-jobs-
evolution-system.md` (full — the five Role production/consumption profiles, the Healthy/Blocked/
Starved priority-ordered decision table and its per-role failure-mode asymmetry, `healthy_work_ticks`,
`bound_state.assigned_region_id`), `design/gdd/loot-filter-ui.md` (full — the collapsed/expanded
accordion row pattern, the sound-vs-heuristic shadow-detection philosophy reused here for a genuinely
new predictive-warning problem, the non-drag-primary/drag-additive reordering philosophy, the
`simplified_ui_density` primary/advanced resolution precedent), `design/gdd/creature-roster-ui.md`
(full — the bottleneck-percentage-over-an-AND-gate technique this document's Team Quality panel
adapts for a weighted-sum formula instead of an AND-gate, the accordion-row-plus-attention-banner
structure, the `equipped_charm_id`/Role-badge rendering precedent), `design/art/art-bible.md` (§3.4
UI Shape Grammar, §4.5 UI Palette, §4.6 Colorblind Safety, §6.7 Automation Infrastructure Visuals —
the exact ward-post/cairn/forge-stall/beacon silhouette-per-stage mapping this document's Stage
display reuses unmodified, §7.2 Iconography Style — the chrome/glyph/world-object-silhouette
three-way split, §7.3 UI Animation Language — utility-screen motion discipline, §7.4 Diegetic vs.
Screen-Space Information — this screen is explicitly named a chrome-heavy pure-management screen with
no diegetic surface of its own, §7.6 Ornamentation Tier by Screen — this screen is named directly at
**None**, §7.7 Interaction States and Accessibility Requirements — the non-drag-equivalent binding
requirement this screen is explicitly enforced under), `design/gdd/accessibility-settings-system.md`
(full — §3.7's binding requirements, all of which name `automation-config-ui` explicitly by ID, and
§3.6's `simplified_ui_density` delegation to this screen's own future GDD), `design/gdd/onboarding-
tutorial-system.md` (§3.4 Stage 8, §3.6 — this document's forward recommendation that the
automation-configuration screen be the direct, contiguous next screen after the Mastery Transition,
and that `active_efficiency_percent` first render here), `design/gdd/game-concept.md` (Pillars 2, 3,
5; the Retention Hooks section; the exact "explicit idle-efficiency percentage as a legible mastery
readout" line, §"Feedback clarity"), `design/gdd/systems-index.md` (this system's entry and
dependency map).

## Assumptions Log (resolved this session, no placeholders left unresolved below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | **The screen is a single accordion list of region rows**, not a master-detail rail-plus-panel layout. A collapsed row's own content — six slot mini-chips rendered inline — *is* the literal region-×-slot-×-creature matrix cell-by-cell; expanding a row reveals full per-region detail. | Directly reuses the proven collapsed/expanded row pattern from `loot-filter-ui`/`creature-roster-ui` rather than inventing a third navigation shape, and answers the brief's "region × slot × creature assignment matrix" requirement literally: region is the row axis, slot is the inline-chip axis, creature is the chip content — visible at a glance across every mastered region without opening anything. |
| A2 | **Creature-to-slot assignment uses exactly two paths: an always-available assign-via-picker control (keyboard/gamepad primary) and drag-and-drop (mouse-only, strictly additive)** — not `loot-filter-ui`'s three-redundant-path structure. | `loot-filter-ui`'s numeric-priority-field and move-up/move-down paths both exist because rule *order* is itself mechanically meaningful there. A team slot carries no priority/order semantics — `region-mastery-automation-system` §3.3 states role, power tier, and source are all valid in any slot — so a third "move" path would have nothing real to move. Two paths (one non-drag, one strictly additive) fully satisfy `accessibility-settings-system` §3.7 item 2's binding requirement without inventing a redundant mechanism the underlying rules don't support. |
| A3 | **This document adds one UI-only field not present in any upstream schema: `region_slot_index`** (int, 0 to `region_team_slot_count − 1`, stable per occupied position). | `creature-data-schema`'s `bound_state.assigned_region_id` records *which region* a creature works in but no position within it, and `region-mastery-automation-system` §3.3 confirms slot position carries no mechanical weight. Without a stable index, removing a creature from slot 3 would reflow the remaining chips and silently relabel every slot after it — a confusing, unnecessary visual churn for a screen revisited constantly. Kept UI-only, additive, never read by any evaluation logic — the same pattern `loot-filter-ui`'s `rule_label` (Assumption A8 there) established. |
| A4 | **The Marginal-Impact Preview (Formula 1) is scoped to a single hypothetical change at a time** — add, remove, or swap one creature in one slot — never a chained multi-step "what if I did both of these" preview. When the candidate creature is currently assigned to a *different* mastered region, the preview shows both the target region's projected gain and the donor region's resulting loss, since `creature-data-schema`'s single `assigned_region_id` field makes reassignment a strict move, never a copy. | A chained preview (simultaneously swapping two creatures across two regions, say) is a real Full Vision enhancement but adds meaningful UI and compute complexity for a case MVP's single-region scope cannot even exercise. Locking single-change scope now keeps the preview's meaning unambiguous: the number shown is always "if I commit exactly this one action," never a hypothetical sequence. The donor-region loss display is not optional, though — showing only the gain would misrepresent the true cost of reassigning a creature already doing useful work elsewhere. |
| A5 | **The Predicted Structural Starvation Warning (Formula 2) is sound but deliberately narrow** — it only ever fires for the two cases `creature-jobs-evolution-system` §3.6 makes *structurally guaranteed* regardless of any tuning value: a Crafter assigned with zero generator (Attacker/Producer) present anywhere in the same region, and a Support assigned as the region's only occupied slot. It is never a general work-state simulator and never predicts Blocked (a live, storage-fill-dependent condition this document cannot know ahead of any ticks). | Direct methodological reuse of `loot-filter-ui`'s Tier-1 shadow-detection philosophy (Assumption A1 there): a narrow, provably sound, zero-false-positive check that catches the mistakes players actually make, stated explicitly as a known-scope limitation rather than implied as a complete solver. This is also the concrete mechanism that answers the brief's "don't let them grind toward an impossible goal" requirement *before* a single tick has run, not after the player discovers it the hard way. |
| A6 | **The Roster Composition Gap / Optimized-Reachability check (Formula 3) is account-wide**, checking the player's *entire* bound roster (assigned anywhere or not) for at least one creature of each of the four chain roles — never limited to what is currently assigned to the region being viewed. | A creature sitting unassigned, or working a different region, could in principle be reassigned into the region under review. Scoping the check to "currently assigned here only" would produce false "impossible" warnings for a player who owns the missing role but simply hasn't moved it yet — the opposite of the legibility this screen exists to provide. |
| A7 | **All four Automation Capability Stages render on every region's expanded detail, always** — including Stage 3 (Auto-Craft/Sell) and Stage 4 (Offline Progression), which MVP does not ship functionally. Stage 3/4 render in a visually distinct **"Designed — arrives in a future update"** locked state, separate from the ordinary mastery-gated-locked state Stage 1/2 use before a region is even mastered. | `region-mastery-automation-system` fully designs all four stages and their mastery gates (§3.5); only the *capability* is Vertical-Slice-deferred, not the design. Hiding Stage 3/4 outright would make the mastery ladder's upper half look like a dead end even though `mastery_level` 2 and 3 (Fully Mastered, Optimized) remain fully reachable and meaningful in MVP (they still drive `idle_efficiency_percent`'s upper bands and `loot-drop-system`'s rarity-parity extension). Showing them as "coming" rather than absent keeps the mastery ladder legible as a real four-rung climb. |
| A8 | **The idle-vs-active teaching moment (§3.6) is gated by a UI-owned, one-time boolean flag** — not proposed as an addition to `region-mastery-automation-system`'s or `accessibility-settings-system`'s schema. | Matches the precedent `settings-menu-ui.md` already set for its own first-run prompt: a first-run/first-time-seen flag is UI-presentation state, not gameplay state, and belongs in this document's own persistence layer. Flagged as a forward coordination note for whichever system implements UI-layer persistence, per this session's file-discipline constraint against editing either spine document directly. |
| A9 | **A minor cross-document citation discrepancy is noted, not resolved by editing either source file.** `game-concept.md`'s own "Feedback clarity" line attributes "the explicit idle-efficiency percentage as a legible mastery readout" specifically to `idle_efficiency_percent`; `onboarding-tutorial-system.md` §3.6 attributes the identical phrase to `active_efficiency_percent` when justifying this screen's teaching-moment placement. This document treats the two readings as complementary, not contradictory: this screen is the one place both numbers are legitimately shown side by side (§3.6), so satisfying onboarding's placement recommendation for `active_efficiency_percent` does not require picking a side on which document's citation is more precise. | Neither source file is edited, per this session's file discipline. Flagged explicitly so a future reconciliation pass (already accumulating a backlog across this session's autonomously-authored documents) has a concrete pointer rather than a silent divergence. |
| A10 | **`simplified_ui_density`'s primary/advanced column split for this screen — delegated by `accessibility-settings-system` §3.6 to "that screen's own future GDD" — is resolved here**: primary tier = region name, mastery-level badge, the six slot chips (Role badge + power tier + work-state icon per chip), `idle_efficiency_percent`, and the row's Needs-Attention flag. Advanced tier (hidden by default under this setting, reachable via the row's existing expand action) = the full Team Quality breakdown panel, the Marginal-Impact Preview, the Automation Stage detail cards, exact storage-fill numbers, and the Predicted Structural Starvation Warning's diagnostic detail (the badge itself stays primary-tier; only its expanded explanation is advanced). | Fulfills the exact obligation `accessibility-settings-system` left open for this screen, matching the resolution pattern `loot-filter-ui` Assumption A7 and `creature-roster-ui` Assumption A11 already established for their own equivalent delegations. |
| A11 | **The Support role's single "designated teammate" target is chosen via an explicit in-slot picker this document defines** — no upstream document specifies the selection mechanism. `creature-jobs-evolution-system` §3.6 states Support applies its multiplier "to exactly one other designated teammate... requires a valid target to orbit" but never names who does the designating or how. | This document cannot render a Support slot without *some* concrete interaction for choosing its target, so it defines one directly: a dropdown listing every other creature currently assigned to the same region, defaulting to unset (Starved) until chosen. Flagged forward as an assumption pending confirmation by `region-mastery-automation-system`, matching this session's established pattern of resolving UI-blocking gaps explicitly rather than leaving them undefined. |
| A12 | **Work-state renders as a static chrome badge (icon + text) on this screen, never as an animated diegetic pulse.** | Art-bible §7.4 explicitly classifies the pure-management screens (Forge, Roster, Automation Config, Loot Filters) as chrome-heavy "abstracted planning interfaces with no 'world' to be diegetic in," reserving animated work-loop pulses for the Region View and Roster's own diegetic/semi-diegetic surfaces. Applying an animated pulse here would blur a distinction the art bible draws deliberately, and would violate §7.3's utility-screen motion discipline (instant/near-instant state changes only) for no legibility gain — the badge's icon shape and text already carry the same information without motion. |

---

## 1. Overview

The Automation Config UI is the player-facing command surface for `region-mastery-automation-system`
— the single screen where every mastered region's assigned team, mastery progress, Automation
Capability Stage status, and idle-efficiency output are built, read, and diagnosed. Per art-bible
§3.4/§7.6, this is one of the game's named **zero-ceremony, maximum-density utility screens**: it
never earns heavier frame ornament regardless of how consequential the decision made here is, and its
richness lives entirely in glyph content and legible structure, never in decoration. This document
owns five things: the **region × slot × creature assignment matrix** (an accordion list of region
rows, each rendering its up-to-six slots as literal matrix cells, with a fully keyboard-operable
assign/unassign/reassign flow); the **Team Quality comprehension system** — the single most important
job this screen has — which breaks `team_quality_score` into its Composition Completeness and Power
Quality components, surfaces the structural-completeness gate that guards the Optimized mastery tier
explicitly and *before* it becomes a wasted grind, and previews the marginal effect of any hypothetical
roster change before it is committed; the **Automation Capability Stage display**, reusing art-bible
§6.7's locked world-object silhouettes (ward-post, collection cairn, forge-stall, ward-lantern) as
its iconography; the **work-state surface**, which makes a Healthy/Blocked/Starved problem anywhere
in the player's account impossible to miss and, per creature, states both cause and fix in plain text;
and the **idle-efficiency readout**, including the one-time teaching moment `onboarding-tutorial-
system` relies on this screen to deliver. It consumes `region-mastery-automation-system`'s and
`creature-jobs-evolution-system`'s formulas and state machines directly, never redefining either.

---

## 2. Player Fantasy

> **The foreman's satisfaction: a well-composed team, humming. And the quiet pride of a region that
> runs without you.**

`region-mastery-automation-system`'s own Player Fantasy names the region-level feeling — *"you fought
for this, and now it works for you."* `creature-jobs-evolution-system`'s names the creature-level
feeling — *"the creature you fought becomes the creature that works for you."* This screen is the one
place both fantasies become a single, literal, manageable picture at once: not an abstract stat on a
menu, but a legible roster of small working colleagues, arranged into a chain that either functions or
visibly doesn't, and — critically — a screen honest enough to show *why*.

Concretely, this screen delivers on that fantasy by making three things always true:

- **A well-run team is legible as a well-run team, not just a high number.** A player who assembles a
  complete four-role chain should be able to *see* that completeness — every chain role checked off,
  the structural gate satisfied, the efficiency band climbing — rather than infer it from a single
  opaque percentage. This is the direct answer to the composition puzzle `region-mastery-automation-
  system` built: the puzzle is worthless if it can't be read, and reading it correctly is this
  screen's entire reason to exist (§3.4).
- **A problem is never a mystery.** A Starved Crafter or a Blocked Producer states its cause and its
  fix in the same breath it announces itself (§3.5) — the foreman never has to guess what's wrong with
  the region, only decide what to do about it.
- **Automation earns real pride, not just idle numbers.** The idle-efficiency readout (§3.6) is framed
  as a genuine accomplishment climbing toward a real ceiling, and the first time a player sees it
  alongside their own recent active performance, the comparison teaches — without a single line of
  tutorial text — that walking away is never wasted and coming back is always worth it (Pillar 3).

---

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This document owns: screen layout and ceremony tier, the region-row accordion pattern and its six
slot-chip matrix cells, the assignment interaction (assign/unassign/reassign, the Support-target
picker), the Team Quality breakdown display and its Marginal-Impact Preview, the Automation Stage
display, the work-state surface and its Predicted Structural Starvation Warning, the idle-efficiency
readout and its onboarding teaching moment, cross-region navigation and scale affordances, and
keyboard/gamepad navigation order. It explicitly does **not** own, and defers to the cited document in
every case: the region state machine, `mastery_level`/RMP thresholds, `CCS`/`PQS`/`team_quality_score`
themselves, `idle_efficiency_percent`'s locked bands, the four Automation Capability Stages' actual
unlock gates, `region_storage_capacity`, and the Stage-3 Gleam cap (all `region-mastery-automation-
system`); a creature's Role, the Healthy/Blocked/Starved transition rule itself, and per-role
production/consumption behavior (`creature-jobs-evolution-system`); `CreatureInstance`, `power_tier`,
and `bind_state` field shape (`creature-data-schema`); the settings surface and binding requirements
this screen must honor (`accessibility-settings-system`); and pixel layout, exact colors, or
implementation (`art-director`/`ui-programmer` territory).

### 3.1 Screen Architecture & Persistent Elements

**Ceremony tier: None** — art-bible §7.6 names this screen directly ("Automation config — None —
Named in 3.4 as high-density utility that never flexes, regardless of how important the decision
being made there is"). Motion is purely functional throughout (§7.3): expand/collapse, sort, and
filter are instant or near-instant state changes, never a lingering transition. Work-state renders as
a static chrome badge, never an animated diegetic pulse (Assumption A12) — this screen's richness
lives entirely in content legibility, never in frame ornament or motion.

**Always visible, in every state of this screen** (a fixed-height header, matching the persistent-
header precedent `loot-filter-ui` §3.1 and `forge-ui` §3.2 both establish):

- **Needs-Attention banner** — a chrome callout listing every region with at least one non-Healthy
  assigned creature, capped at `needs_attention_banner_max_regions_listed` (§7) inline entries before
  collapsing to `"+N more"`. Absent entirely (not merely hidden) when every assigned creature
  account-wide is Healthy — no empty banner shell for the common good-state case. Clicking a listed
  region jumps to and auto-expands that row, scrolled to the specific problem slot (§3.5).
- **Search field** — chrome text input, filtering the region list by name; a live view filter only,
  never reorders or mutates state (§3.7).
- **Region count summary** — Data-face tabular text: `"N regions mastered, M slots filled of
  N×region_team_slot_count"` — the account-wide staffing picture at a glance (directly answers the
  "more regions mastered than creatures to staff" edge case, §5).

**Main content region**: the region-row accordion list (§3.2). Exactly one behavior differs from
`loot-filter-ui`'s equivalent list: **the most recently mastered region, on the first automation-
config visit immediately following its own Mastery Transition, opens already expanded** — every other
region (existing or subsequently mastered) defaults collapsed. This is the direct implementation of
`onboarding-tutorial-system` Stage 8's forward recommendation that this screen be "the direct,
contiguous next screen after the transition resolves... so the causal link between 'I just won this'
and 'this is now working for me' stays unbroken" (§6).

### 3.2 The Region × Slot × Creature Assignment Matrix

**The collapsed region row**, left to right — this row's own content is the literal matrix (region =
row, slot = chip, creature = chip content):

1. **Region name** (Display-face).
2. **Mastery-level badge** — chrome text + Data-face tabular RMP progress, e.g. `"Fully Mastered —
   2,340 / 5,000 RMP toward Optimized"`, or `"Optimized"` with no progress fraction once reached (the
   ratchet, `region-mastery-automation-system` §3.2 — never demotes, never shows a countdown to
   losing it).
3. **Six slot chips** (`region_team_slot_count`, default 6, §7 there) — the matrix's slot axis, each
   independently focusable and keyboard/gamepad-operable:
   - **Empty chip**: a neutral chrome outline + `"Empty"` text + an always-visible `"Assign"` action.
   - **Occupied chip**: the creature's Role badge (glyph-grammar, reused unmodified from combat per
     art-bible §3.4), a Data-face tabular power-tier number, and a work-state icon (§3.5) — three
     independent, non-color-only signals packed into one small cell.
   - Chip order is stable across additions/removals within a region, keyed by `region_slot_index`
     (Assumption A3) — never silently reflows.
4. **`idle_efficiency_percent`** — Data-face tabular number, this region's current live value
   (`region-mastery-automation-system` §4 Formula 4, cited by name, never recomputed here).
5. **Row-level Needs-Attention flag** — present only if any of this row's slots is non-Healthy; a
   compact chrome icon + `"N issues"` text.
6. **Automation Stage mini-icons** — up to four small world-object silhouettes (§3.3), rendered at a
   reduced scale, giving an at-a-glance stage-progress read without expanding the row.
7. **Expand/collapse action.**

**Interaction states** (five-state matrix, identical to `loot-filter-ui`/`forge-ui`'s own): Available,
Hover, Keyboard-Focus (mandatory visible indicator, WCAG 2.4.7), Selected, Disabled — built entirely
from value-step and border-weight within the three-color chrome budget (art-bible §4.5,
`accessibility-settings-system` §3.7 item 1).

**Expanded region detail** (accordion, in place — never a modal, matching the utility-screen
precedent both sibling documents establish): six full slot cards (one per `region_team_slot_count`
position, in stable `region_slot_index` order), the Team Quality breakdown panel (§3.4), the four
Automation Stage detail cards (§3.3), the idle-efficiency readout (§3.6), and per-resource storage
meters with a **Collect Backlog** action (§3.5, `region-mastery-automation-system` §3.4/Edge Case 6 —
"the player collects/processes the backlog... via `automation-config-ui` or a manual Forge visit").

#### 3.2.1 The Full Slot Card (Expanded State)

Each of the six slot cards shows, whether empty or occupied:

- **Empty**: `"Empty — Assign"` action, opening the Assign Picker (§3.2.2) directly into this slot.
  On a region with zero creatures assigned anywhere (§5 Edge Case 1), the first slot card additionally
  carries a one-time hint: `"Start with an Attacker — without one, this region produces nothing"`
  (`creature-jobs-evolution-system` §3.6, cited directly), matching `onboarding-tutorial-system` Stage
  10's recommendation to narrow a five-way decision to the one that currently matters.
- **Occupied**: Role badge, creature name, power tier, work-state badge with cause+fix text (§3.5),
  and two actions — **Reassign** (opens the Assign Picker pre-filtered to exclude the current
  occupant) and **Unassign** (immediate, no confirm — this is a placement decision, not a destructive
  one; matches `loot-filter-ui` Assumption A4's stakes-proportional confirmation philosophy).
  - If the occupant's Role is Support, an additional **Boosting: [dropdown]** control (Assumption
    A11) lists every other creature currently assigned to the same region; selecting `"— none —"`
    (the default, pre-selection) leaves the Support Starved per `creature-jobs-evolution-system`
    §3.6's own "requires a valid target" rule, surfaced honestly rather than hidden.

#### 3.2.2 The Assign Picker

A searchable, keyboard-navigable list of every bound, unassigned `CreatureInstance` the player owns,
plus every bound creature currently assigned to *any* region (shown, not hidden — §5 Edge Case 7),
each row showing: Role badge, power tier, current assignment status (`"Unassigned"` or
`"Currently assigned to [Region], Slot [N] — assigning here will unassign it there"`), and a live
**Marginal-Impact Preview** badge (§4 Formula 1) for the first `marginal_preview_live_candidate_cap`
(§7) visible rows, computed on-demand for any row beyond that cap the instant it receives keyboard
focus.

**Assignment paths** (Assumption A2 — two paths, both always available, both converging on identical
resulting state):

1. **Assign-via-picker** (primary, keyboard/gamepad-first) — opening a slot's Assign action, selecting
   a candidate row, and confirming. This is the only path required to complete any assignment flow
   with no mouse present.
2. **Drag-and-drop** (secondary, mouse-only, strictly additive) — dragging a creature chip from the
   Assign Picker directly onto a target slot card. Dropping triggers the identical assignment logic
   as path 1. Never the only way to perform any assignment — per art-bible §7.7/`accessibility-
   settings-system` §3.7 item 2's binding requirement.

### 3.3 Automation Stage Display and Gating

Each region's expanded detail shows all **four** Automation Capability Stages, always, as a row of
four cards reusing art-bible §6.7's locked world-object silhouettes unmodified (the "established
world-object silhouette" iconography category, art-bible §7.2) — never a generic gear-cog icon:

| Stage | Silhouette (art-bible §6.7) | Gate (mastery level ≥) | MVP Card State |
|---|---|---|---|
| **1 — Auto-Attack** | Standing ward-post | `newly_conquered` (0) | Active the instant the region is mastered |
| **2 — Auto-Loot** | Collection cairn/hopper beside the ward-post | `newly_conquered` (0) | Active alongside Stage 1 |
| **3 — Auto-Craft/Sell** | Forge-stall | `fully_mastered` (2) | **"Designed — arrives in a future update"** (Assumption A7) |
| **4 — Offline Progression** | Ward-lantern/beacon | `optimized` (3) | **"Designed — arrives in a future update"** (Assumption A7) |

Each card shows: the silhouette, the stage name, and one of three states — **Active** (chrome
checkmark + text, this region's `mastery_level` already clears the gate and the capability is MVP-
shipped), **Locked — [N] more mastery levels required** (mastery gate not yet cleared; a Data-face
progress fraction toward the *next* mastery-level threshold is shown, e.g. `"1,660 RMP to Fully
Mastered"`, `region-mastery-automation-system` §4 Formula 2, cited directly), or **Designed — arrives
in a future update** (Stage 3/4 specifically, in MVP builds, regardless of whether the mastery gate is
already cleared — the capability itself is not yet shippable, distinct from a mastery-gate lock and
never confused with one visually).

### 3.4 Team Quality Breakdown and Marginal-Impact Preview — THE CENTRAL DESIGN TASK

This is the single most important comprehension surface on this screen. A player who sees only
`"Team Quality: 0.62"` and nothing else will do exactly what the brief warns against: stack the
highest-power creatures available and conclude the game is broken when a complete-but-modest team
outperforms them. This panel exists to make that impossible.

**Layout, top to bottom, inside the expanded region detail:**

1. **The Chain-Role Checklist** — four rows, one per chain role, each showing: the role name (with
   its glyph-grammar Role badge), a filled/missing state (`"Present"` with a checkmark glyph, or
   `"Missing"` with a distinct outline glyph — shape-differentiated, never color-only), and, if
   present, which assigned creature(s) satisfy it. The **generator** row explicitly reads
   `"Generator (Attacker or Producer)"` and is satisfied by either or both — never double-counted if
   both are present, matching `region-mastery-automation-system` §3.3's own CCS definition exactly.
   This checklist **is** `CCS` made legible: `4/4 present = CCS 1.0`, and the fraction is shown as a
   running total (`"3 / 4 chain roles present"`) directly above the four rows.
2. **The Structural Completeness Gate banner** — a persistent, unconditional statement, present on
   every region regardless of current CCS: `"Reaching Optimized requires all 4 chain roles present at
   the same time, in addition to enough Region Mastery Points. Missing: [named roles]"` (or
   `"Structural requirement met"` once CCS = 1.0). This is shown **before** the RMP threshold check
   ever runs — a region at zero RMP with an incomplete team already states the gate exists, so no
   player grinds RMP toward `optimized` believing power alone will get them there
   (`region-mastery-automation-system` §3.2/A2, cited directly).
3. **The Power Quality bar** — a horizontal Data-face segmented bar showing average assigned
   `power_tier` against `power_tier_reference_ceiling` (`PQS`, cited directly, never recomputed), with
   the exact tabular value (`"PQS: 0.25 (avg power tier 5 / 20)"`) beside it.
4. **The Weighted Total** — a two-segment stacked bar directly visualizing
   `team_quality_score = ccs_weight × CCS + pqs_weight × PQS`: one segment sized to the CCS
   contribution, one to the PQS contribution, labeled with their exact weights (`"Composition ×0.65"`,
   `"Power ×0.35"`) so the dominance of composition over raw power (`region-mastery-automation-
   system` A2/§3.3) is visible as a proportion, not just implied by a single opaque number. The total
   `team_quality_score` renders as a Data-face tabular figure, rounded to 2 decimal places for
   comparison-column legibility (art-bible §7.1) — full unrounded precision is retained for every
   underlying computation.
5. **The Marginal-Impact Preview** — live within the Assign Picker (§3.2.2): every candidate row shows
   a delta badge computed by Formula 1, e.g. `"Team Quality: 0.58 → 0.74 (+0.16)"`, and — if the
   candidate would newly satisfy an unmet chain role — an explicit callout: `"Would satisfy the
   Optimized structural gate"`. This is the concrete mechanism that lets a player evaluate a decision
   *before* committing it, directly answering the brief's "show the marginal impact of a change before
   it's committed" requirement.

### 3.5 The Work-State Surface

Every occupied slot chip (collapsed) and slot card (expanded) carries a work-state badge — icon +
text, never color-only, per `accessibility-settings-system` §3.7 item 4 — reading `Healthy`,
`Blocked`, or `Starved` (`creature-jobs-evolution-system` §3.5's exact three-state machine, cited by
name, never recomputed here). The expanded card additionally states cause and fix, drawn directly from
that document's own priority-ordered table:

| State | Cause (as shown) | Fix (as shown) |
|---|---|---|
| `Starved` (Crafter) | `"No raw material available — nothing upstream is producing input."` | `"Assign a generator (Attacker or Producer) to this region."` |
| `Starved` (Support) | `"No valid teammate to boost."` | `"Assign at least one other creature to this region, then designate it above."` |
| `Blocked` (any storable-output role) | `"[Resource] storage is full — output has nowhere to go."` | `"Collect the backlog below, or expand storage."` |
| `Healthy` | — | — |

**Predicted Structural Starvation Warning** (Formula 2, Assumption A5) — a proactive, pre-tick warning
shown the instant an assignment would create one of the two structurally-guaranteed Starved
conditions, *before* the player leaves the Assign Picker: `"Assigning this Crafter here will starve
it — this region has no generator."` This never predicts Blocked (a live, storage-fill-dependent
condition), and is explicitly scoped as narrow-but-sound (§4 Formula 2), matching `loot-filter-ui`'s
own shadow-detection discipline.

**Storage meters and Collect Backlog** — each region's expanded detail shows a Data-face fill bar per
storable resource (`current / region_storage_capacity`, cited directly), with a **Collect** action
always available, emphasized (a distinct chrome accent, never a new hue) once a resource nears or
reaches capacity. Pressing Collect frees capacity immediately, per `region-mastery-automation-system`
§3.4/Edge Case 6 — this screen is one of the two named collection paths (the other being a manual
Forge visit).

**Cross-region visibility**: the header's Needs-Attention banner (§3.1) and every collapsed row's
Needs-Attention flag (§3.2) together guarantee a problem region is never more than one glance away —
directly answering the brief's "the player must spot a problem region instantly" requirement.

### 3.6 The Idle-Efficiency Readout and the First-Mastery-Transition Teaching Moment

Every region's expanded detail carries a **two-segment comparison bar**, drawn on one shared scale
(Formula 4, §4) so the two figures are honestly proportional to each other, never independently
scaled:

- **Idle** — this region's current `idle_efficiency_percent` (`region-mastery-automation-system` §4
  Formula 4, cited directly), labeled with its current mastery-level band (e.g. `"94.75% — Fully
  Mastered band: 80–100%"`).
- **Active (recent)** — the player's most recent `active_efficiency_percent` reading in this region
  (`combat-encounter-system` §4 Formula 7, cited directly, never recomputed here), labeled `"Your last
  active run: 210%"`.

**The teaching moment** (Assumption A8): the *very first* time this comparison bar renders for the
player — which, per `onboarding-tutorial-system` §3.6's forward recommendation, is immediately
following their first-ever Mastery Transition, on the freshly-expanded region row (§3.1) — a one-time,
dismissible callout appears beside it: `"Even with no team assigned yet, this region already produces
at [band_min]% — idle is never worthless. Your active run just now reached [active%] — active play is
always the bigger number, but walking away is never a loss."` This single readout teaches both halves
of Pillar 3 from one number, without a lecture: the floor is a real, nonzero percentage, and the
ceiling is visibly, meaningfully higher. The callout is gated by a UI-owned, one-time flag (Assumption
A8) and never reappears once dismissed or once naturally read past (a single interaction with either
segment of the bar counts as "read").

### 3.7 Cross-Region Management

- **At exactly one mastered region** (MVP's actual scope), the region list renders as a single row —
  no rail, no pagination controls, nothing to scroll. This is not a separate build; it is the same
  accordion-list component simply rendering its natural single-item state.
- **At two or more mastered regions**, the list scrolls vertically past `region_list_page_size` (§7)
  visible rows, and the header's search field (§3.1) becomes the primary way to locate a specific
  region by name at scale.
- **The account-wide region-count summary** (§3.1) and the Needs-Attention banner are the two
  always-visible cross-region signals — a player never needs to scroll through every region to know
  whether something, somewhere, needs a decision.

### 3.8 Keyboard & Gamepad Navigation

- **Full keyboard/gamepad operability, no exceptions** (`accessibility-settings-system` §3.7 item 3):
  every interactive element — header controls, every region row's collapse/expand and six slot chips,
  every slot card's Assign/Reassign/Unassign/Boosting-target controls, the Assign Picker, every
  Automation Stage card, the Collect Backlog action, search — is reachable and operable without a
  mouse or pointer.
- **Visible keyboard-focus indicator** (WCAG 2.4.7) on every one of the above, built from value-step/
  border-weight within the chrome budget (`accessibility-settings-system` §3.7 item 1).
- **No drag-only interaction anywhere** (art-bible §7.7, `accessibility-settings-system` §3.7 item 2):
  every drag-capable action (creature-to-slot assignment, §3.2.2) has a fully equivalent non-drag path
  that performs the identical state change.
- **Fixed logical navigation order**: header (search → Needs-Attention banner entries, if present) →
  region rows in list order (row expand/collapse → six slot chips, `region_slot_index` order → when
  expanded: slot cards in the same order, each card's Assign/Reassign/Unassign/Boosting-target
  controls → Team Quality panel (read-only, still focusable for screen-reader-equivalent traversal) →
  four Stage cards → efficiency readout → storage meters and Collect actions) → next region row.
- **No simultaneous multi-button input anywhere** (`accessibility-settings-system` §3.7 item 8): every
  action is a single discrete input at a time.

---

## 4. Formulas

Four formulas are original to this document — display-layer derivations over `region-mastery-
automation-system`'s and `creature-jobs-evolution-system`'s own formulas, cited and never redefined,
matching the precedent `loot-filter-ui` §4 and `creature-roster-ui` §4 both already established for
their own UI-owned math.

### Formula 1 — Marginal-Impact Preview (Team Quality Score Delta)

```
Given: current assigned team T for region R, and a single hypothetical change ΔT
       (add candidate C to an empty slot, remove C from a slot, or swap C_old → C_new in a slot)

T' = T with ΔT applied (hypothetical only, never committed until confirmed)

CCS'(T') = (count of the 4 chain roles — generator[Attacker∨Producer], Crafter, Defender, Support —
            present anywhere in T') / 4                    [identical method to Formula 3,
                                                              region-mastery-automation-system]

PQS'(T') = clamp(mean(power_tier of T') / power_tier_reference_ceiling, 0, 1)

team_quality_score'(T') = clamp(ccs_weight × CCS'(T') + pqs_weight × PQS'(T'), 0, 1)

Δ team_quality_score = team_quality_score'(T') − team_quality_score(T)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `T`, `T'` | set of `CreatureInstance` | 0..`region_team_slot_count` members | Current and hypothetical assigned team. |
| `ccs_weight`, `pqs_weight`, `power_tier_reference_ceiling` | float/int, external input | per `region-mastery-automation-system` §7 | Reused directly, never independently retuned here. |
| `team_quality_score'(T')` | float | `[0, 1]` | Displayed preview value. |
| `Δ team_quality_score` | float | `[-1, 1]` | Displayed as `"current → hypothetical (Δ)"`. |

This adds no new gameplay logic — it is `region-mastery-automation-system`'s own Formula 3, evaluated
twice (once for `T`, once for `T'`) and diffed. **Output range**: identical bounds to the source
formula by construction, since it is the same formula.

**Worked example** (deliberately reusing that document's own Formula 3 worked-example numbers for
direct cross-document consistency): a 5-creature team missing its Defender (`CCS = 0.75`), average
`power_tier = 5` (`PQS = 5/20 = 0.25`): `team_quality_score(T) = 0.65×0.75 + 0.35×0.25 = 0.4875 +
0.0875 = 0.575` → displayed `"0.58"`. The candidate under evaluation is a Defender at `power_tier = 5`
(matching the existing average, so the mean is unchanged): `CCS'(T') = 4/4 = 1.0`, `PQS'(T') =
5/20 = 0.25` (unchanged) → `team_quality_score'(T') = 0.65×1.0 + 0.35×0.25 = 0.65 + 0.0875 = 0.7375` →
displayed `"0.74"`. The picker row shows: `"Team Quality: 0.58 → 0.74 (+0.16)"` plus `"Would satisfy
the Optimized structural gate"` (since `CCS` crosses from `0.75` to `1.0`). This exactly reproduces
`region-mastery-automation-system` §4 Formula 3's own second worked-example value (`0.7375`,
"a complete 6-role team, average `power_tier = 5`") as the hypothetical result — a genuine, not
forced, cross-document consistency check.

### Formula 2 — Predicted Structural Starvation Warning

```
predicted_starvation(c, T') = TRUE  if role(c) = Crafter
                                     AND no member of T' has role ∈ {Attacker, Producer}

predicted_starvation(c, T') = TRUE  if role(c) = Support
                                     AND |T'| = 1   (c is the only assigned member of T')

predicted_starvation(c, T') = "no prediction" otherwise
                               [the live work_state machine, creature-jobs-evolution-system
                                §3.5 Formula 6, is the sole authority once ticks actually run]
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `c` | `CreatureInstance` | — | The candidate creature being evaluated for a hypothetical slot. |
| `T'` | set of `CreatureInstance` | — | The hypothetical resulting team (Formula 1's `T'`). |
| `predicted_starvation(c, T')` | bool / sentinel | `{TRUE, "no prediction"}` | Never `FALSE` — this check only ever asserts a positive, sound prediction or declines to predict; it never claims a creature will *not* starve, since Blocked and every other live condition remain unknown ahead of ticking. |

**Why this is sound, not a heuristic**: `creature-jobs-evolution-system` §3.6 fixes the Crafter's input
source to the region's own generator output and the Support's target to another assigned teammate —
both structurally, not by tuning value. If neither condition can ever be met by the hypothetical team
as constructed, `input_resource_available`/a valid orbit target can never become `true` regardless of
any other state, so `starved` is not merely likely, it is guaranteed by construction until the
composition itself changes. This is a proof for exactly these two cases, mirroring `loot-filter-ui`
Formula 1's own Tier-1 soundness argument.

**Worked example**: a hypothetical team consisting of one Crafter and one Defender (no Attacker, no
Producer) — `role(Crafter) = Crafter`, and `T'` contains no member with role Attacker or Producer →
`predicted_starvation(Crafter, T') = TRUE`, surfaced in the Assign Picker before the assignment is
confirmed. The same team's Defender: `predicted_starvation(Defender, T') = "no prediction"` (Defender
is structurally always-on per `creature-jobs-evolution-system` A10, never a candidate for this check
at all).

### Formula 3 — Roster Composition Gap / Optimized-Reachability Check

```
role_ever_owned(role) = TRUE  if the player's full bound roster
                                 (every CreatureInstance with bind_state = bound,
                                  assigned to any region or unassigned)
                               contains at least one creature whose current Role = role,
                               for role ∈ {generator[Attacker∨Producer], Crafter, Defender, Support}

optimized_reachable = role_ever_owned(generator) AND role_ever_owned(Crafter)
                       AND role_ever_owned(Defender) AND role_ever_owned(Support)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `role_ever_owned(role)` | bool | — | Account-wide, not per-region (Assumption A6). |
| `optimized_reachable` | bool | — | `FALSE` drives a persistent advisory on every region's Team Quality panel: `"This region cannot reach Optimized yet — you don't currently own a [Role] creature anywhere. Hatch or evolve one to unlock the possibility."` |

This is a pure existence check over `creature-jobs-evolution-system`'s own `role` field — no new
gameplay data, purely a display-layer read across the account. **Worked example**: a player owns
creatures with roles `{Producer, Producer, Crafter}` and no Attacker, Defender, or Support anywhere.
`role_ever_owned(generator) = TRUE` (Producer satisfies it), `role_ever_owned(Crafter) = TRUE`,
`role_ever_owned(Defender) = FALSE`, `role_ever_owned(Support) = FALSE` →
`optimized_reachable = FALSE`, surfaced with the missing roles named explicitly: `"Missing anywhere in
your roster: Defender, Support."`

### Formula 4 — Efficiency Comparison Bar Scaling

```
bar_length_ratio(value) = clamp(value / active_efficiency_cap_percent, 0, 1)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `value` | float, external input | — | Either `idle_efficiency_percent` (`[25, 120]`) or `active_efficiency_percent` (external input, `combat-encounter-system` §4 Formula 7). |
| `active_efficiency_cap_percent` | float, external input | `300` (fixed, `combat-encounter-system` §7, hard-locked, cited by exact name) | The single shared normalization ceiling both bars are drawn against. |
| `bar_length_ratio(value)` | float | `[0, 1]` | Rendered bar length as a fraction of the segment's maximum width. |

Both bars in §3.6's comparison share this one scale specifically so their *relative* lengths are
honestly comparable — two independently-scaled bars would visually misrepresent the true ratio between
idle and active output. **Worked example**: `idle_efficiency_percent = 94.75` →
`bar_length_ratio = 94.75/300 = 0.316` (about a third of the segment's max width);
`active_efficiency_percent = 210` → `bar_length_ratio = 210/300 = 0.70` — visibly, honestly, more than
double the idle bar's length, matching the actual ~2.2x ratio between the two numbers.

---

## 5. Edge Cases

1. **A region with zero creatures assigned.** All six slot chips render `"Empty — Assign"`; the Chain-
   Role Checklist (§3.4) shows all four roles `"Missing"`; `CCS = 0`, `PQS = 0`,
   `team_quality_score = 0`; `idle_efficiency_percent` computes to that mastery level's `band_min`
   exactly (`region-mastery-automation-system` §5 Edge Case 5, cited directly) — a real, nonzero
   number, not a blank field. The first slot card carries the Attacker-first hint (§3.2.1).
2. **A team that can never reach Optimized with the player's current roster.** Formula 3's
   `optimized_reachable = FALSE` state (§4) is surfaced as a persistent advisory on the Team Quality
   panel the instant it's true, independent of the region's current RMP — the player is told this
   *before* grinding toward a threshold that structural absence alone already makes unreachable, per
   the brief's explicit requirement. The advisory never blocks play or automation — the region keeps
   producing at whatever tier it can legitimately reach.
3. **A creature evolves and changes Role while assigned.** Per `creature-jobs-evolution-system` §5
   Edge Case 3, the creature keeps `assigned_region_id` across the transition; this screen reflects the
   new Role on the slot chip/card immediately and fires a one-time, non-blocking toast the next time
   this screen is viewed: `"[Creature] evolved into a [new Role]"`, plus, if the Chain-Role Checklist's
   completeness state changed as a direct result (a role newly covered, or a role that lost its only
   coverage), an explicit follow-up line: `"Your team's composition improved"` or `"...creating a gap:
   you no longer have a [Role]."` This document only displays the result — the composition-validity
   re-check itself is `region-mastery-automation-system`'s own stated integration expectation on that
   not-yet-fully-implemented path, cited, not performed here.
4. **All creatures Starved (an entire region).** Every occupied slot chip and card shows the Starved
   badge with its per-role cause/fix; `region_output_per_tick = 0` for every resource
   (`region-mastery-automation-system` §4 Formula 5, cited directly) is stated plainly in the
   region's efficiency readout area (`"Producing nothing this tick — see slots above"`) rather than
   implying malfunction; the region's row carries the Needs-Attention flag and appears in the header
   banner. No timer, no decay, no destructive consequence — matches that document's own Edge Case 1.
5. **More regions mastered than the player has creatures to staff.** Empty slots persist indefinitely
   across however many regions lack enough bound creatures to fill them — no forced auto-fill, no
   error state. The header's account-wide summary (§3.1) makes the gap between total slots and total
   bound creatures visible at the account level without requiring the player to open every region.
6. **This screen is reached for a region that is not yet Mastered** (a stale link, a UI navigation
   edge). **Ruling**: render a neutral, non-error state — `"Not yet mastered — defeat this region's
   boss to unlock automation"` — with no slot grid, no Team Quality panel, and no crash. Automation
   config is meaningless before the region state machine's own `mastered` transition
   (`region-mastery-automation-system` §3.1), and this screen never implies otherwise.
7. **A candidate selected in the Assign Picker is already assigned to a different mastered region.**
   The picker row states this plainly (`"Currently assigned to [Region], Slot [N]"`, §3.2.2) and the
   Marginal-Impact Preview (Formula 1) shows both sides of the trade: the target region's projected
   gain and, beneath it, the donor region's resulting `team_quality_score` after the loss —
   `"[Donor Region] would drop: 0.74 → 0.58"` — so the decision is never made blind to its own cost.
8. **The Support's designated teammate is later unassigned or reassigned elsewhere.** The Support's
   `Boosting:` selection (§3.2.1) reverts to `"— none —"` the next time this screen is viewed, and the
   Support's work-state badge shows Starved with its standard cause/fix text — no error, no silent
   retained reference to a teammate no longer present.
9. **Collect Backlog is pressed on a resource well below capacity.** Legal, not an error — it simply
   moves whatever backlog currently exists (even a small amount) into inventory/processing early.
   Blocked slots caused by that specific resource's cap clear on the next tick display once capacity
   is freed; slots Blocked for an unrelated reason are unaffected.

---

## 6. Dependencies

### Depends On

- **`encounter-spawn-system.md`** — supplies **`par_clear_time_seconds`** (the encounter template's
  fixed authored anchor) and **`power_tier`**, which together let this screen show the player what
  their farm is *actually* achieving: `automation_clear_time_seconds` (`region-mastery-automation-
  system` Formula 6) is derived from par and the team's `idle_efficiency_percent`.
  **This screen displays that derived clear time; it must never feed it back into any formula that
  scores the player.** Doing so is precisely Blocker B1 — it inverted Pillar 3 by making the active
  player's efficiency fall as their own automation improved. Automation's clear time is a *readout*,
  not a *reference*.
- **`region-mastery-automation-system.md`** — every field and formula this screen renders without
  redefining: the region state machine, `mastery_level`, `region_mastery_points`, `CCS`, `PQS`,
  `team_quality_score` (Formula 3, extended for display by this document's own Formula 1), `idle_
  efficiency_percent` (Formula 4), `region_team_slot_count`, `region_storage_capacity`, the four
  Automation Capability Stages and their mastery gates (§3.5), and the Collect action's mechanical
  effect (§3.4/Edge Case 6, cited directly). That document's own Dependencies section already names
  `automation-config-ui` as the presentation layer for exactly this set of fields (§6 there) —
  fulfilled in full above.
- **`creature-jobs-evolution-system.md`** — the five-Role production/consumption profile table (§3.6),
  the Healthy/Blocked/Starved priority-ordered decision table and its per-role failure-mode asymmetry
  (§3.5/A10, the direct basis for this document's own cause/fix text and Formula 2's soundness proof),
  `healthy_work_ticks`, `bound_state.assigned_region_id`, and Role itself.
- **`creature-data-schema.md`** — `CreatureInstance`, `power_tier`, `bind_state`, and the `Role` enum
  underlying every badge this screen renders; read directly, never redefined.
- **`accessibility-settings-system.md`** — every binding requirement in its §3.7 is treated as
  non-negotiable here (§3.8 restates the ones most load-bearing for this screen, all of which name
  `automation-config-ui` explicitly by ID in that document's own table); §3.6's `simplified_ui_
  density` delegation to "that screen's own future GDD" is resolved by this document (Assumption A10).
- **`design/art/art-bible.md`** — §3.4/§7.6 (zero-ceremony, density-never-flexes), §4.5 (chrome
  palette), §6.7 (the exact ward-post/cairn/forge-stall/beacon silhouette-per-stage mapping, reused
  unmodified), §7.2 (the chrome/glyph/world-object-silhouette iconography split), §7.3 (utility-screen
  motion discipline), §7.4 (this screen's explicit classification as chrome-heavy, non-diegetic), §7.7
  (the non-drag-equivalent binding requirement this screen is directly enforced under).

### Depended On By

None — this is a leaf system (per this task's own explicit scope instruction). No other GDD's owned
mechanics consume any formula or field this document originates (Formulas 1–4 are pure display-layer
derivations with no consumer outside this screen, matching `loot-filter-ui` §6's own equivalent
finding for its own three original formulas).

### Adjacent Systems (informational, not a dependency in either direction)

- **`combat-encounter-system.md`** — `active_efficiency_percent` and `active_efficiency_cap_percent`
  (`= 300`) are read directly for the §3.6 comparison readout and this document's own Formula 4, but
  this document does not depend on that system's own mechanics beyond those two already-external,
  already-cited values, and that document's own Dependencies section does not name this screen as a
  dependent — an informational adjacency, not a mechanical one, matching the precedent `loot-filter-
  ui` set for its own `forge-ui` adjacency.
- **`onboarding-tutorial-system.md`** — recommends (its own §3.6, flagged forward from that document,
  not a binding contract this document must satisfy) that this screen be the direct next screen after
  the first Mastery Transition and the first surface where `active_efficiency_percent` renders; §3.1
  and §3.6 of this document honor that recommendation, but the relationship is advisory, not a hard
  dependency in either direction — that document's own §6 lists itself as a leaf with nothing
  depending on it, consistent with this document also not listing it as a hard dependency.

**`systems-index.md` gap flagged, not self-edited** (matching the accumulating pattern already noted
in [[resonance-hunter-loot-filter-ui]] and [[resonance-hunter-creature-roster-ui]]): entry #20's
dependency list currently names only `region-mastery-automation-system`; this document establishes
real, additional dependencies on `creature-jobs-evolution-system`, `creature-data-schema`, and
`accessibility-settings-system`, none of which are yet reflected there.

---

## 7. Tuning Knobs

| Knob | Default | Safe Range | Affects | Notes |
|---|---|---|---|---|
| `region_list_page_size` | 10 | 6–20 | Number of region rows rendered before the list requires scrolling | MVP ships exactly one region; this bounds render/compute cost once more regions accumulate (Full Vision). |
| `creature_picker_page_size` | 20 | 10–50 | Number of candidate rows shown in the Assign Picker before requiring search/scroll narrowing | Keeps the picker navigable at large-roster scale. |
| `marginal_preview_live_candidate_cap` | 20 | 10–30 | How many picker rows get simultaneously live-computed Formula 1 delta badges, versus on-focus/on-demand computation beyond that | Direct compute-cost bound, matching `loot-filter-ui`'s `items_affected_sweep_sample_size` precedent — Formula 1 is cheap per-row but a large roster times a large slot count is not free at scale. |
| `marginal_preview_scope` | Single hypothetical change only (**locked**, not a numeric range) | N/A | Whether the preview chains multiple hypothetical changes at once | Locked at single-change scope for MVP legibility (Assumption A4). Chained "what-if I swapped two creatures across two regions" previews are a legitimate Full Vision enhancement, not attempted here. |
| `needs_attention_banner_max_regions_listed` | 5 | 3–8 | Number of regions named inline in the header's Needs-Attention banner before collapsing to `"+N more"` | Keeps the persistent header bounded as more regions accumulate problems simultaneously. |
| `structural_warning_recompute_debounce_ms` | 150 | 50–300 | Delay before Formula 2's Predicted Structural Starvation Warning updates while a player is actively browsing the Assign Picker | Avoids visual flicker/noise while rapidly scrolling candidate rows; short enough that the warning still feels immediate on settle. |

---

## 8. Acceptance Criteria

| # | Criterion | Verification Method |
|---|---|---|
| AC1 (required — keyboard-only) | Every interaction on this screen — region expand/collapse, all six slot chips, Assign/Reassign/Unassign, the Boosting-target picker, the Assign Picker's search and selection, every Automation Stage card, Collect Backlog, header search — is completable using keyboard only. | Scripted or manual walkthrough completing one full team assembly (4+ chain roles across 3+ slots), one reassignment across two regions, one Support-target designation, and one Collect action, using keyboard input exclusively. |
| AC2 (gamepad-only) | Identical scope to AC1, substituting a single connected gamepad with no keyboard or mouse connected. | Identical walkthrough to AC1, gamepad-only. |
| AC3 (required — no drag-only) | Disabling drag-and-drop entirely does not remove any capability from this screen — every drag-capable action remains fully completable via the assign-via-picker path alone. | Walkthrough identical in scope to AC1, performed with the drag input path explicitly disabled in a test harness. |
| AC4 (required — comprehension without external documentation) | Given any fixture team, a player can identify, from the Chain-Role Checklist and Weighted Total panel alone, exactly which chain roles are missing and what fraction of `team_quality_score` composition versus power each contributes — matching Formula 3's (`region-mastery-automation-system`) actual `CCS`/`PQS` component values exactly, with no value invented or approximated by this screen. | Cross-check the panel's displayed component values against a reference computation of that document's own Formula 3, across a fixture sweep of team compositions. |
| AC5 (required — structural gate surfaced before wasted effort) | For a region fixture with `region_mastery_points = 0` and an incomplete team (`CCS < 1.0`), the Structural Completeness Gate banner already states the requirement and names the missing role(s) — it does not wait for the RMP threshold to be approached or reached before appearing. | Fixture test at `RMP = 0`, asserting the gate banner and named-missing-roles text render identically to the same fixture at `RMP = rmp_threshold_3 − 1`. |
| AC6 | Formula 1's worked example reproduces exactly: a 5-creature team missing Defender (`CCS = 0.75`, `PQS = 0.25`, `team_quality_score = 0.575`) previewing the addition of a `power_tier = 5` Defender candidate displays `"0.58 → 0.74 (+0.16)"` and the `"Would satisfy the Optimized structural gate"` callout. | Unit test against the fixture, asserting both the exact displayed delta and the callout's presence. |
| AC7 | The Marginal-Impact Preview's hypothetical `team_quality_score'` value, for any fixture add/remove/swap operation, exactly matches an independent recomputation of `region-mastery-automation-system` Formula 3 over the resulting hypothetical team — no divergence at any team-size or composition combination. | Cross-check sweep across randomized (but seeded/deterministic) fixture teams and single-change operations. |
| AC8 | Formula 2's Predicted Structural Starvation Warning fires on 100% of fixtures matching its two defined sound cases (Crafter with no generator present; Support as the sole assigned member) and on 0% of fixtures outside those two cases, including fixtures that would actually Starve or Block for a *different*, non-predicted reason. | Test matrix covering both sound cases plus at least three non-predicted Starved/Blocked causes (storage-capacity Blocked, Support with a valid target present, a Crafter with a generator present but temporarily storage-Blocked), asserting the warning fires only for the two sound cases. |
| AC9 | Formula 3's Roster Composition Gap check correctly reports `optimized_reachable = FALSE` and names every missing role whenever the account-wide bound roster (assigned to any region or unassigned) lacks at least one creature of a given chain role, and reports `TRUE` whenever at least one of each role exists anywhere in the roster, even if none are currently assigned to the region being viewed. | Fixture sweep including a roster with the missing role present but unassigned elsewhere, asserting `optimized_reachable = TRUE` in that case specifically (proving the check is account-wide, not assigned-only, per Assumption A6). |
| AC10 | The Needs-Attention banner (header) and every region row's own Needs-Attention flag together list every region with at least one non-Healthy assigned creature, and neither lists any region whose entire assigned team is Healthy. | State-matrix fixture covering all-Healthy, one-Starved, one-Blocked, and mixed-state region fixtures, asserting banner/flag presence exactly matches expected state. |
| AC11 | `idle_efficiency_percent`, as displayed for any region fixture, always falls within `region-mastery-automation-system` Formula 4's exact locked band for that region's current `mastery_level` — this document never computes or displays a value outside that source formula's own output. | Cross-check displayed value against a direct call to that document's own Formula 4 across a sweep of `mastery_level`/`team_quality_score` combinations. |
| AC12 | The idle-vs-active teaching callout (§3.6) appears exactly once per playthrough — on the first region-detail view immediately following the player's first-ever Mastery Transition — and never reappears on any subsequent screen view, region, or session, once dismissed or naturally read past. | Scripted flow test: trigger a first Mastery Transition, assert the callout renders; navigate away and back, and trigger a second Mastery Transition on a different region, asserting the callout does not render a second time. |
| AC13 (colorblind) | With `colorblind_preview_mode = grayscale` enabled, every mechanically important state on this screen — work-state (Healthy/Blocked/Starved), Chain-Role Checklist present/missing, Automation Stage locked/active/future-update, and the Structural Completeness Gate met/unmet — remains fully distinguishable by shape, icon, or text alone. | QA pass through a fully-populated region fixture with the grayscale preview active, matching `loot-filter-ui` AC12's own verification method. |
| AC14 (scale) | With `ui_scale_percent = 200`, every region row, the slot-chip matrix, and the expanded detail panel reflow (scroll, never overlap or silently truncate) at maximum scale. | Layout test at maximum scale, per `accessibility-settings-system` §5 Edge Case #1's data-table reflow exception. |
| AC15 (`simplified_ui_density`) | With the setting enabled, every collapsed region row still displays enough information (region name, mastery badge, six slot chips with work-state icon, `idle_efficiency_percent`, Needs-Attention flag) to make an informed assign/collect decision without expanding the row. | Structural review of the collapsed-row field set against §3.1/Assumption A10's documented primary/advanced split. |

---

## Cross-System Facts Proposed for Registration

None of this document's cited gameplay formulas, enums, or constants require registration — every one
(`region-mastery-automation-system`'s `CCS`/`PQS`/`team_quality_score`/`idle_efficiency_percent`/RMP
formulas, `creature-jobs-evolution-system`'s work-state machine and Role production table) is already
registered by its owning document and is cited here by exact name, never redefined, matching the
precedent `loot-filter-ui` and `creature-roster-ui` both set for their own equivalent sections.

This document's own four originals (Marginal-Impact Preview, Predicted Structural Starvation Warning,
Roster Composition Gap check, Efficiency Comparison Bar Scaling) are UI-presentation derivations with
no consumer outside this screen — no other system in `systems-index.md` renders a region assignment
matrix — so none are proposed for central registration.

**One UI-only schema addition, flagged, not proposed for registration in either spine document**:
`region_slot_index` (Assumption A3) — a stable, cosmetic per-slot ordering field, additive to and
never a modification of `creature-data-schema`'s or `region-mastery-automation-system`'s own fields,
never read by any evaluation logic. Flagged as a forward coordination note for whichever system
implements persistence, matching the exact precedent `loot-filter-ui`'s `rule_label` (Assumption A8
there) already established.

**One open UI-blocking gap resolved locally, flagged forward for confirmation**: the Support-target
designation mechanism (Assumption A11) — no upstream document specifies how a Support's single
"designated teammate" is chosen; this document defines an explicit in-slot picker to make the screen
buildable, but the underlying rules-layer decision belongs to `region-mastery-automation-system` or
`creature-jobs-evolution-system`, neither of which currently states it. Matches the exact pattern
`creature-roster-ui`'s `discovered_node_ids` trigger resolution (its own §6/memory note) already set
for a UI document resolving a rules-layer gap it needed to be buildable.
