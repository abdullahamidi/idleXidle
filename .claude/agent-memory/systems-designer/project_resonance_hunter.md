---
name: project-resonance-hunter
description: Resonance Hunter project context — idle action RPG, MonoGame/C#, current authoring stage and dependency order
metadata:
  type: project
---

**Resonance Hunter** is a 2D pixel-art idle action RPG (Monster Hunter weak-point combat +
Melvor Idle automation-as-endgame), single-player, MonoGame 3.8.4.1 / C#/.NET 8+, 60 FPS.
`design/gdd/game-concept.md` and `design/art/art-bible.md` (9 sections, complete) are the two
locked authority documents — read both fully before designing any creature/combat/UI system.

**Why:** the concept prototype step was explicitly skipped this project (see systems-index.md's
High-Risk Systems table) — `combat-encounter-system`'s fun-factor is the single most load-bearing
unproven assumption in the project. Flag this when designing anything combat-adjacent.

**How to apply:** `design/gdd/systems-index.md` is the authoritative dependency map and
Recommended Design Order (24 systems: 22 MVP / 1 Vertical Slice / 1 Full Vision). Foundation tier
(no dependencies) is `creature-data-schema` → `item-data-schema` → `save-load-persistence`, in
that order — design in this sequence, not alphabetically or by intuition. `creature-data-schema`
(design/gdd/creature-data-schema.md) is done; it is the single biggest dependency bottleneck (5
not-yet-written systems depend on it directly: combat-encounter-system, animation-rig-system,
creature-ai-telegraph-system, creature-jobs-evolution-system, loot-drop-system — each must
reference it back when authored). See [[feedback-rush-mode-authoring]] for how this session is
being run, and [[reference-registry-and-state-files]] for where cross-doc facts live.
