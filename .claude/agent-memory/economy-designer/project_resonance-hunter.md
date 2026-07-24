---
name: project-resonance-hunter
description: Context on the Resonance Hunter idle-action-RPG project and its GDD-authoring conventions
metadata:
  type: project
---

The working directory `c:\Users\Admin\Desktop\idleXidle` is "Resonance Hunter" — a 2D pixel-art
idle Action RPG (MonoGame/C#, single-player), per `design/gdd/game-concept.md`. Core hook: active
weak-point/telegraph combat (Monster Hunter-flavored) that hands off to earned, staged automation
(auto-attack -> auto-loot -> auto-craft/sell -> offline progression), governed by a locked
25-120% idle-efficiency curve. Five pillars: Precision Over Reflexes, Automation Is Earned Not
Assumed, Active Is Better/Idle Never Worthless, Builds Are Trade-offs Not Stat Stacks, Creatures
Are Systems Not Trophies. Scope tiers: MVP (1 region) -> Vertical Slice -> Alpha -> Full Vision
(many regions, Memory Dust prestige, full Forge/Weaving).

**Why this matters**: many systems are designed far ahead of their implementation tier (Full
Vision content authored before MVP ships) specifically so dependent MVP/VS systems have stable
hooks to build against. See [[feedback-rush-authoring-convention]] for how these sessions actually
get authored without a live user.

Key already-authored GDDs I've read in depth: `region-mastery-automation-system.md` (the thesis
system — mastery ladder, idle efficiency Formula 4, the Stage-3 Gleam-realization cap of 24/hr,
BALANCE-CRITICAL), `the-forge-system.md` (closed-sink economy, Gleam currency, Vow-binding
permanence), `creature-jobs-evolution-system.md` (five evolution influence vectors, tag grammar),
`resonance-weaving-system.md` (Source x Form x Vow, Formula 1/2 power-multiplier derivation),
`loot-drop-system.md` (faucet/sink modeling, Loot Filter Rule Engine).

I authored `design/gdd/memory-dust-prestige-system.md` (Full Vision prestige layer) on
2026-07-14. Core design call: **nothing resets** — no region de-masters, no creature reverts, no
Vow-bound item is touched. Memory Dust is earned purely from region-mastery-level milestones
(3/7/20/50 per region, ratchet, once each) and spent on a finite 19-unlock tree (660 total cost)
across the six categories game-concept.md names. This was a deliberate rejection of the classic
"reset + multiplier" prestige pattern, argued from Pillars 2/5 and the Forge's explicit
irreversible-Vow design. Hard rule baked into the doc: no unlock may ever touch a LOCKED value in
another system's Tuning Knobs table, and `region_auto_sell_gleam_cap_per_hour` (24/hr,
BALANCE-CRITICAL) is excluded from Memory Dust's authority entirely, never merely "kept in range."
