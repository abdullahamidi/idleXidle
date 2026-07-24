---
name: reference-registry-and-state-files
description: Where cross-system facts and session state live on the Resonance Hunter project
metadata:
  type: reference
---

On the Resonance Hunter project:

- `design/registry/entities.yaml` — single source of truth for cross-GDD facts (entities, items,
  formulas, constants). Register any formula whose output feeds another system's input, or any
  numeric constant another GDD must agree with. Format is strict YAML with required fields
  (name, status, source, referenced_by, added) — see the file's own header comments and existing
  entries for the exact shape before adding new ones.
- `design/gdd/systems-index.md` — the dependency map, priority tiers, and Recommended Design
  Order for all 24 identified systems. Has a Progress Tracker table (design docs started/
  reviewed/approved counts) that should be updated whenever a GDD's status changes.
- `production/session-state/active.md` — living checkpoint of current authoring task, progress
  checklist (by dependency wave), key decisions, and next step. Read first after any session
  interruption. See [[feedback-rush-mode-authoring]] for the current authoring mode recorded here.
- `.claude/rules/design-docs.md` — the 8-required-sections rule and formula/edge-case/dependency
  rigor rules that govern every file in `design/gdd/**`.
