# ADR-004 — Warren facility economy as a pure Core model with a preserved creature den

| Field | Value |
|---|---|
| **Status** | Superseded 2026-09-01 — P12 (commit 5f15730) cut the INSIGHT currency (a closed loop), re-pointed two facilities at Forge materials (Scrap/Essence), added the conquest unlock ramp and the depth cap, and rebased the Dust rates. The creature den this ADR preserves was retired 2026-08-24. Current spec: design/gdd/warren-facilities.md |
| **Date** | 2026-07-28 |
| **Deciders** | user (chose "build facilities for real" + "3 real currencies") + Claude (implementation) |
| **Supersedes** | — |

## Context

The Warren production spec (rev 1) and its reference depict a facility base-builder: a Warren level +
XP, eight named facilities with per-minute output, three production currencies, facility upgrade costs,
and a bonuses strip. The game's real Warren (`AutomationScreen` + `RegionAutomation`) is a different
system entirely — creature-team automation with role stations, a creature pen, and per-region mastery.
Almost none of the reference's specifics had a backing model, and the UX standard (§11) forbids
inventing currencies, upgrade costs, and production timers.

The user chose to build the facility system for real, with facilities producing three **real**
currencies (Gleam, Mastery Points, Memory Dust) into the player's actual balances.

## Decision

1. **A new pure Core model** `IdleXIdle.Core.Warrens.Warren` owns facilities, levels/XP, production
   formulas, and upgrade costs. It holds **no balances and no rendering**: `Tick(seconds)` returns what
   was produced and the host credits the real currencies (ADR-001 — logic stays MonoGame-free and
   unit-testable). Upgrades are instant in v1 (build timers are a documented follow-up).

2. **Production feeds the game's existing currencies**, not new ones. Gleam and Dust credit their real
   accumulators. Mastery Points are *derived* each frame (not stored), so facility-produced mastery banks
   into a persisted **pool** that the host folds into `MasteryTree.SetEarned` — making the Warren a real
   cross-system loop (it funds the build tree and prestige) while spending the pool on upgrades correctly
   lowers available tree points.

3. **The creature den is preserved, not replaced.** The Warren nav slot now shows the facility dashboard;
   the old creature automation becomes the Warren's CREATURES sub-view (a `_warrenShowDen` sub-mode of the
   existing `_showAutomation` flag). This keeps every dependent system intact — the Forge feed-target,
   Cores, evolution, region farming, and their saves and tests — with zero churn to `AutomationScreen`.

4. **Save compatibility without a version bump.** All Warren fields default (level 1, empty facility map,
   pool 0), so pre-Warren saves load a fresh level-1 Warren — the project's established default-empty rule.

## Consequences

- **Positive**: the reference is literally accurate; the balance math is fully unit-tested (11 tests,
  including tick determinism and save round-trip); no existing system was disturbed; the two Warren faces
  share one nav slot cleanly.
- **Negative / follow-ups**: passive Mastery/Dust income touches those economies (rates start
  conservative and are tunable in `WarrenTuning`); facilities render color-coded hexes rather than bespoke
  per-facility art; upgrades are instant (no timers yet). Reconciliation and follow-ups are recorded in
  `design/gdd/warren-facilities.md`.
