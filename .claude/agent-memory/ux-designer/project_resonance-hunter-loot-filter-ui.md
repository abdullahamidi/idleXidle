---
name: resonance-hunter-loot-filter-ui
description: loot-filter-ui GDD authored 2026-07-14, closing systems-index.md #21; introduces a new UI-owned shadow-detection algorithm and one UI-only schema field not in loot-drop-system's engine schema
metadata:
  type: project
---

`design/gdd/loot-filter-ui.md` was authored 2026-07-14 (autonomous session, no user
available) as the interface layer for `loot-drop-system.md`'s Loot Filter Rule Engine
(that document's §3.9 — `LootFilterRule`/`FilterCondition` schema, first-match-wins/
ascending-priority/AND-within-a-rule evaluation, default `keep`, the hard Legendary +
togglable-Epic safety net). This screen is art-bible's own named maximum-density,
zero-ceremony test case (§3.4, §7.6, §9.6's explicit PoE-density-without-memorization
brief).

**Key design calls made, worth knowing before touching either document again**:

- **Two-tier shadow detection is this document's one genuinely new formula** (§4
  Formula 1): Tier 1 (exact literal condition-set containment — always-on, provably
  sound) plus Tier 2 (same-field comparator-aware range subsumption, e.g. `rarity >=
  epic` shadows `rarity >= legendary` — best-effort, no false positives, documented
  false negatives on cross-field logic). Deliberately **not** a general arbitrary-logic
  solver — flagged as Assumption A1, scoped to catch the mistakes players actually make
  (duplicate rules, an over-broad earlier rule, a misplaced catch-all), not to prove
  full unreachability in the general case.
- **Priority reordering has three redundant, always-converging paths**: an editable
  numeric "move to position" field (UI-maintained, strictly contiguous 1..N — this
  document owns tie/gap prevention since `loot-drop-system` explicitly defers it),
  per-row move-up/move-down buttons, and a drag handle as a strictly additive,
  mouse-only third option. This is the concrete implementation of art-bible §7.7's
  binding non-drag-reorder requirement, for which this screen is the section's own
  named example.
- **Deletion/preset-replace use a lightweight inline-confirm-plus-Undo-toast pattern,
  not `forge-ui`'s full ceremony gate** (Assumption A4) — because a filter rule is
  configuration, not a destroyed item; the two screens' confirmation weight
  deliberately diverges even though both are Presentation-tier siblings authored the
  same session.
- **One new UI-only schema field, not registered anywhere else**: `rule_label`
  (Assumption A8) — optional player-authored string for search/recognition, stored as
  UI-layer metadata keyed by `rule_id`, never read by `loot-drop-system`'s evaluation
  engine, and deliberately not proposed for registration in that document's own schema
  (file discipline — `loot-drop-system.md` was not edited). Flagged as a forward
  coordination note for whoever implements persistence.
- **Resolved an open item `accessibility-settings-system.md` explicitly left for
  this screen**: §3.6 of that document delegates the `simplified_ui_density`
  primary/advanced column split to "that screen's own future GDD" — this document's
  Assumption A7 is that resolution (primary tier: priority, enabled, one-line
  condition summary, action, unreachable badge; advanced tier: full condition editor,
  exact items-affected count, shadow-detail breakdown, drag handle).

**Flagged, not yet actioned**: `systems-index.md`'s Dependency Map lists
`loot-filter-ui` as depending only on `loot-drop-system`; this document also has real
dependencies on `item-data-schema` and `accessibility-settings-system` not yet
reflected there. Not self-edited, per this session's file discipline (same pattern
`forge-ui.md` and `accessibility-settings-system.md` already flagged for their own
equivalent gaps — `systems-index.md` has an accumulating backlog of these
dependency-map gaps across this session's autonomous GDD authoring, worth a
centralized reconciliation pass before Production).

**How to apply**: before extending or reviewing the loot filter screen, or before
designing `automation-config-ui`/`creature-roster-ui` (the other two screens
`accessibility-settings-system` §3.6 names under `simplified_ui_density`, still not
yet authored as of this write), re-read `design/gdd/loot-filter-ui.md` directly — this
is a snapshot of the design calls made at authoring time, not a substitute for the
current file. The two-tier shadow-detection pattern and the three-redundant-path
reorder pattern are both good precedent to reuse rather than re-derive if either of
those two future screens turns out to need a similar comprehension or reorder problem
solved.
