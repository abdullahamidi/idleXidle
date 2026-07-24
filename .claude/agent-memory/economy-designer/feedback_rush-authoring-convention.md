---
name: feedback-rush-authoring-convention
description: How to author GDDs in this project when no live user is available for section-by-section approval
metadata:
  type: feedback
---

This project's normal collaboration protocol (question-first, one section at a time, approval
before writing) is the default — but orchestrator/parent-agent tasks in this project sometimes
explicitly override it with instructions like "WRITE THE FILE EARLY" and "Do not ask questions,"
usually because no live user is available this session and the task prompt has already made every
necessary creative decision (or explicitly delegates the remaining calls to me with "make a real
call and defend it rigorously").

**How to apply**: when a task prompt does this, don't fight it by pausing for approval anyway —
write the complete document in one pass, but preserve the *substance* of the collaboration
protocol by front-loading a "Document Status" + "Assumptions Log" block (established convention,
see `region-mastery-automation-system.md` and `the-forge-system.md`) that names every ambiguity
resolved, the call made, and why — one row per decision. This is what the sibling GDDs in
`design/gdd/` already do ("Status: Complete — authored in rush mode... no user available to
approve section-by-section... every ambiguity resolved with an explicit, flagged design call
rather than a placeholder"). Never leave a placeholder value; always make and defend a specific
call.

**File discipline is strict and literal in this project**: when a task says "write ONLY
[target-file]," that means never touching `systems-index.md`, `production/session-state/active.md`,
or `design/registry/entities.yaml` even though the work clearly produces registry-worthy facts —
those get listed in a "Cross-System Facts Proposed for Registration" closing section inside the
target file itself, plus restated in my final chat response, and left for a separate coordinating
process to actually write into the registry.

**Why**: confirmed by direct observation across multiple sibling GDDs in this codebase using the
identical pattern — this is an established, repeated project convention, not a one-off
instruction, so it should be the default posture for future rush-mode GDD tasks in this project
absent signals otherwise.
