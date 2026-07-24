---
name: feedback-rush-mode-authoring
description: When production/session-state/active.md or the task says "rush mode"/no user available, write full GDDs directly instead of the normal per-section approval loop
metadata:
  type: feedback
---

On the Resonance Hunter project, GDD authoring has been explicitly run in "rush mode" — the user
instructed the team to proceed continuously through the systems-index Recommended Design Order
"without per-step approval," recorded in `production/session-state/active.md` under Current Task.

**Why:** stated time constraint (see game-concept.md's Scope Risks — full vision is 12-24+ months
against a much shorter stated dev timeline). The user chose speed over the standard
question-first/section-by-section approval workflow for this GDD-authoring phase specifically.

**How to apply:** before starting a GDD-authoring task on this project, check
`production/session-state/active.md`'s "Current Task" line. If it says rush mode / continuous
authoring, write the complete file directly in one pass (per the normal 8-section
design-docs.md structure) rather than drafting section-by-section with approval gates — resolve
ambiguities by making the reasonable call and logging it as a flagged assumption inline (an
"Assumptions Log" table near the top of the doc worked well for creature-data-schema.md), not by
stopping to ask. Still update `active.md`'s progress checklist and the systems-index.md progress
tracker after each completed doc — that bookkeeping is not part of what got skipped. If
`active.md` does NOT mention rush mode, default back to the standard collaborative
question→options→decision→draft→approval protocol.
