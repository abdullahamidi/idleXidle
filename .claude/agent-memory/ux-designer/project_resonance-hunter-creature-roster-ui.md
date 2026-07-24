---
name: resonance-hunter-creature-roster-ui
description: creature-roster-ui GDD authored 2026-07-14, closing systems-index.md #19; resolves the discovered_node_ids trigger ambiguity and the equipped-charm ownership-conflict gap creature-jobs-evolution-system A3 flagged forward
metadata:
  type: project
---

`design/gdd/creature-roster-ui.md` was authored 2026-07-14 (autonomous session, no user available)
as the interface layer for `creature-jobs-evolution-system.md` (evolution vectors, work-state
machine, Core Hatching, `equipped_charm_id`) and `creature-data-schema.md`. Reuses
`loot-filter-ui.md`'s established precedents directly: the collapsed/expanded accordion row pattern,
the persistent-header-plus-attention-banner structure, and the empty-state-default-view pattern (see
[[resonance-hunter-loot-filter-ui]]).

**Key design calls made, worth knowing before touching either document again**:

- **Core Hatching lives on the Roster screen, not the Forge** — a deliberate call authorized by the
  task brief, reasoned as: Hatching's entire output/consequence is creature-side (mints a
  `CreatureInstance`), while Feed (a different thing a `creature_core` can do) correctly stays owned
  by `the-forge-system`. The two are mutually exclusive per-core player choices, not two halves of one
  flow.
- **Resolved an ambiguity `creature-data-schema` left open**: `discovered_node_ids`' population
  trigger. Ruling: every direct child of a creature's current evolution node is auto-discovered the
  instant the creature reaches that parent node — never a separate discovery action/currency. Forced
  by Pillar 5 ("what it becomes is your doing" fails if the current decision's branches are hidden);
  deeper (grandchild+) nodes stay hidden for the Discovery hook, though MVP trees have no grandchildren
  so this has zero visible effect in MVP content. Flagged as worth promoting into
  `creature-data-schema` or `creature-jobs-evolution-system`'s own rules-layer text later, since it's
  a rules decision that only ended up in a UI document because this screen needed it to be buildable.
- **Branch Readiness is a new continuous display formula** (Formula 1): the bottleneck (min) of the
  AND-gate's per-vector percentages, over `creature-jobs-evolution-system` Formula 4's exact four
  inputs — never redefines that formula's boolean semantics, just gives it a continuous lens plus a
  named blocking vector and a generated one-line fix-action per branch card. This is the document's
  answer to "how close am I" and is what makes the Evolution Tree View (explicitly the most important
  surface on the screen, per the task brief) legible rather than a boolean pass/fail wall.
- **Production Contribution for Defender/Support is deliberately left as a qualitative Active/Inactive
  badge, not a fabricated percentage** (A10) — `creature-jobs-evolution-system` Formula 5 never
  defines the actual multiplier magnitude those two roles apply to teammates (that belongs to
  `region-mastery-automation-system`, not yet written); inventing a number would have been exactly the
  hand-waving the project's coding standards forbid. Flagged explicitly as thin, not padded.
- **The equipped-charm ownership-conflict gap is only half-resolved, and this is stated plainly, not
  glossed over**: this document's Charm Picker fully prevents one item from being equipped to two
  creatures at once (self-contained, since it owns rendering of `equipped_charm_id` across the whole
  roster). It **cannot** prevent the Forge from independently selling/dismantling/merging/feeding an
  equipped item, because `item-data-schema`/`the-forge-system` have no "equipped to creature X" field
  — the exact gap `creature-jobs-evolution-system` A3 already flagged forward. The mitigation shipped
  here is defensive-only: re-validate the equipped item's status on every screen focus, clear the slot
  gracefully with a one-time notice if it was consumed elsewhere. **How to apply**: if a future session
  is tempted to treat this as "solved," it isn't — the real fix is the `item-data-schema` follow-up
  field A3 already named, still not built.
- **Vow-bound items are excluded from the creature Charm Picker** (A5) by direct analogy to
  `creature-jobs-evolution-system` Edge Case 7's Feed-path exclusion — that document's own Vector 2
  spec never states this for Equip specifically. A UI-level defensive ruling, not a confirmed
  rules-layer decision; flagged for that document to state explicitly later.
- **Simultaneous-branch-eligibility tie-break (`branch_priority`) is surfaced directly in the UI**:
  the winning card shows "Will trigger next," losing siblings show "Also ready — resolves after
  [winner]." Direct, explicit application of `creature-jobs-evolution-system` Edge Case 1 — the task
  brief specifically asked this be aligned-and-surfaced, not just handled silently.

**Flagged, not yet actioned** (matching the accumulating pattern from
[[resonance-hunter-loot-filter-ui]] and [[resonance-hunter-combat-hud]]): `systems-index.md` entry
#19's dependency list for `creature-roster-ui` only names `creature-jobs-evolution-system`, missing
`creature-data-schema`, `item-data-schema`, and `accessibility-settings-system`.
`creature-data-schema.md`'s own "Depended On By" list omits `creature-roster-ui` despite this
document's heavy read dependency on it. Neither `item-data-schema.md` nor `the-forge-system.md`
mentions `creature-roster-ui` anywhere (confirmed by direct search this session). None self-edited,
per this session's file discipline — the centralized `systems-index.md`/dependency-map reconciliation
pass this session keeps flagging across every autonomous GDD is now overdue across at least four
documents.

**How to apply**: before extending or reviewing `creature-roster-ui.md`, or before designing
`automation-config-ui` (the last screen `accessibility-settings-system` §3.6 names under
`simplified_ui_density`, still not yet authored as of this write), re-read the file directly — this is
a snapshot of design calls made at authoring time, not a substitute for the current file. The
accordion-row-plus-attention-banner pattern and the bottleneck-percentage-over-an-AND-gate technique
are both good precedent to reuse if `automation-config-ui` turns out to have a similar
multi-vector-readiness comprehension problem.
