# Systems Index: Resonance Hunter

> **Status**: Approved
> **Created**: 2026-07-14
> **Last Updated**: 2026-07-14
> **Source Concept**: design/gdd/game-concept.md

---

## Overview

Resonance Hunter's mechanical scope splits into five domains that mirror the five game
pillars: a precision combat layer (weak-point targeting, telegraphs, part-breaks — Pillar 1),
an earned-automation layer (region mastery, staged unlock, offline production — Pillars 2 & 3),
an itemization/build layer (the Forge, Resonance Weaving, Vow-condition tracking — Pillar 4),
and a creature layer (capture, jobs, evolution — Pillar 5). All four gameplay domains sit on a
shared technical foundation (creature/item data schemas, the homebrew animation rig, and
save/load) and are wrapped in a presentation layer that the art bible has already substantially
specified. Review mode is `lean`; per user instruction this session, all internal decision
points are being resolved directly rather than raised for individual approval, given a stated
time constraint.

---

## Systems Enumeration

| # | System Name | Category | Priority | Status | Design Doc | Depends On |
|---|---|---|---|---|---|---|
| [Game Flow](game-flow.md) | **Foundation** | End-to-end loop: expedition, enemy archetypes, band rotation, the post-run report, the two trees. Supersedes combat-encounter-system and expedition-auto-battle. |
| 1 | creature-data-schema (inferred) | Core | MVP | Designed | design/gdd/creature-data-schema.md | — |
| 2 | item-data-schema (inferred) | Core | MVP | Designed | design/gdd/item-data-schema.md | — |
| 3 | save-load-persistence (inferred) | Persistence | MVP | Designed | design/gdd/save-load-persistence.md | — |
| 4 | animation-rig-system (inferred) | Core | MVP | Designed | design/gdd/animation-rig-system.md | creature-data-schema |
| 5 | input-targeting-system (inferred) | Core | MVP | Designed | design/gdd/input-targeting-system.md | creature-data-schema, animation-rig-system¹ |
| 6 | resonance-weaving-system | Gameplay | MVP | Designed | design/gdd/resonance-weaving-system.md | item-data-schema |
| 7 | the-forge-system | Economy | MVP | Designed | design/gdd/the-forge-system.md | item-data-schema |
| 8 | creature-ai-telegraph-system (inferred) | Gameplay | MVP | Designed | design/gdd/creature-ai-telegraph-system.md | creature-data-schema, animation-rig-system |
| 9 | accessibility-settings-system (inferred) | Meta | MVP | Designed | design/gdd/accessibility-settings-system.md | input-targeting-system, save-load-persistence |
| 10 | combat-encounter-system | Gameplay | MVP | Designed | design/gdd/combat-encounter-system.md | creature-data-schema, animation-rig-system, input-targeting-system, resonance-weaving-system, creature-ai-telegraph-system, item-data-schema |
| 11 | vow-condition-tracking (inferred) | Gameplay | MVP | Designed | design/gdd/vow-condition-tracking.md | resonance-weaving-system, combat-encounter-system |
| 12 | loot-drop-system (inferred) | Economy | MVP | Designed | design/gdd/loot-drop-system.md | item-data-schema, combat-encounter-system, creature-data-schema |
| 13 | creature-jobs-evolution-system | Progression | MVP | Designed | design/gdd/creature-jobs-evolution-system.md | creature-data-schema, item-data-schema, loot-drop-system, the-forge-system |
| 14 | region-mastery-automation-system | Progression | MVP | Designed | design/gdd/region-mastery-automation-system.md | combat-encounter-system, creature-jobs-evolution-system, save-load-persistence, loot-drop-system |
| 15 | rare-creature-capture-system | Gameplay | Vertical Slice | Designed | design/gdd/rare-creature-capture-system.md | combat-encounter-system, creature-jobs-evolution-system, creature-data-schema |
| 16 | memory-dust-prestige-system | Progression | Full Vision | Designed | design/gdd/memory-dust-prestige-system.md | region-mastery-automation-system, the-forge-system, creature-jobs-evolution-system, resonance-weaving-system, loot-drop-system, save-load-persistence |
| 17 | combat-hud | UI | MVP | Designed | design/gdd/combat-hud.md | combat-encounter-system, vow-condition-tracking, accessibility-settings-system, creature-ai-telegraph-system, creature-data-schema |
| 18 | forge-ui (inferred) | UI | MVP | Designed | design/gdd/forge-ui.md | the-forge-system, item-data-schema, accessibility-settings-system, resonance-weaving-system |
| 19 | creature-roster-ui | UI | MVP | Designed | design/gdd/creature-roster-ui.md | creature-jobs-evolution-system, creature-data-schema, item-data-schema, accessibility-settings-system |
| 20 | automation-config-ui | UI | MVP | Designed | design/gdd/automation-config-ui.md | region-mastery-automation-system, creature-jobs-evolution-system, accessibility-settings-system |
| 21 | loot-filter-ui | UI | MVP | Designed | design/gdd/loot-filter-ui.md | loot-drop-system, item-data-schema, accessibility-settings-system |
| 22 | region-view-world-map-ui | UI | MVP | Designed | design/gdd/region-view-world-map-ui.md | region-mastery-automation-system, creature-jobs-evolution-system, combat-encounter-system, accessibility-settings-system |
| 23 | audio-system | Audio | MVP | Designed | design/gdd/audio-system.md | combat-encounter-system, region-mastery-automation-system, creature-ai-telegraph-system, accessibility-settings-system, creature-jobs-evolution-system |
| 24 | onboarding-tutorial-system | Meta | MVP | Designed | design/gdd/onboarding-tutorial-system.md | combat-encounter-system, combat-hud, creature-ai-telegraph-system, input-targeting-system, accessibility-settings-system |
| 25 | settings-menu-ui (inferred) | UI | MVP | Designed | design/gdd/settings-menu-ui.md | accessibility-settings-system, input-targeting-system, save-load-persistence |
| 26 | hunter-progression-system (inferred) | Progression | MVP | Designed | design/gdd/hunter-progression-system.md | combat-encounter-system, the-forge-system, loot-drop-system, region-mastery-automation-system |
| 27 | encounter-spawn-system (inferred) | Gameplay | MVP | Designed | design/gdd/encounter-spawn-system.md | creature-data-schema⁴ |

⁴ **BLOCKING GAP found by `/review-all-gdds` (2026-07-14) — system #27 added.** Four systems
*consume* `power_tier` — it drives loot rarity distribution (`loot-drop-system`), telegraph windup
duration (`creature-ai-telegraph-system`), and creature health (`combat-encounter-system`) — but **no
system produced it.** Every GDD deferred the question upstream and the buck never stopped. There was
no owner for *what creature you fight, at what power, in what sequence*. `encounter-spawn-system` is
that owner: it defines the **encounter template** schema, the `power_tier` assignment rule, and the
per-template `par_clear_time_seconds` constant.

**Dependency direction is deliberate and must not be "corrected."** `encounter-spawn-system` depends
on `creature-data-schema` *only*. It consumes the region's current mastery level as an **input value**,
not as a system dependency. Wiring it to depend on `region-mastery-automation-system` would close a
cycle (`region-mastery` → `combat-encounter` → `encounter-spawn` → `region-mastery`) and break the
acyclic build order. Mastery level is data passed in, not a system edge.

**`par_clear_time_seconds` is the anchor that resolves Blocker B1** (see the cross-review report). It is
the hand-authored clear time of *a competent, fully-mastered automated team* for a given encounter
template, and it is FIXED — independent of the player's live mastery, team, or automation throughput.
Both efficiency tables are measured against it: active (120–140 / 180–220 / 250–300%) and idle
(25–40 / 50–70 / 80–100 / 100–120%). Previously `active_efficiency_percent` was measured against
automation's *live* clear time, which improves with mastery — so the active player's score **fell** as
their automation got better, inverting Pillar 3. A fixed anchor removes the circularity and makes both
of the user's locked tables hold simultaneously.

³ **BLOCKING GAP found by `/review-all-gdds` (2026-07-14) — system #26 added.** All 25 original systems
were authored and **none of them levels the Hunter.** `combat-encounter-system` §3.1 defines Hunter
stats as `hunter_base_[stat] + Σ(equipment modifiers)` and explicitly defers `hunter_base_[stat]` to
"whichever future progression system levels the Hunter" — a system that did not exist. Meanwhile
creature difficulty scales rigorously across `power_tier` 1–20: a tier-20 boss is **~7× harder to kill**
than a tier-1 boss of the same template (3.85× health; defense mitigation dropping from 71.4% → 39.4% of
damage landing), against **zero** documented player-side growth. Invisible at MVP (`power_tier` locked to
1–3, one region), but `region-mastery-automation-system`, `memory-dust-prestige-system`, and
`creature-data-schema` are all already fully designed against the full 1–20 range. Every other
progression axis in the game has a proven formula; the one number every other curve is implicitly
measured against had none.

This system is also tasked with closing a second review finding: **Gleam has effectively one sink.** Its
only faucet (manual Sell) is permanently unthrottled, while its only sink (`vow_binding_cost`, ~200
Gleam) saturates mid-game after "realistically low tens of bindings" (`the-forge-system` §3.7).
Hunter progression costing Gleam turns the missing progression system into the missing recurring sink.

**Total system count is now 27** (25 MVP / 1 Vertical Slice / 1 Full Vision).

² **Gap found during authoring (2026-07-14)**: `settings-menu-ui` was missing from the original
enumeration — `accessibility-settings-system.md` defines a full settings surface but no screen was
ever specified to host it. Added as system #25. Total system count is now **25** (23 MVP / 1 VS /
1 Full Vision).

¹ **Correction (added when `input-targeting-system.md` was authored)**: the original dependency
list for this row omitted `animation-rig-system`. `input-targeting-system.md`'s hit-testing and
cycle-order math both require each part's current-frame render transform (position, rotation,
scale), which only `animation-rig-system` produces — a runtime dependency this system cannot
function without, even though `animation-rig-system` hadn't been designed yet when this row was
first written. See `design/gdd/input-targeting-system.md`'s Assumption A1 and §6 for the full
contract this system assumes; `animation-rig-system`'s own GDD must confirm/refine it and
reference `input-targeting-system.md` back when authored.

---

## Categories

| Category | Description | Systems in this project |
|---|---|---|
| **Core** | Foundation systems everything depends on | creature-data-schema, item-data-schema, animation-rig-system, input-targeting-system |
| **Gameplay** | The systems that make the game fun | resonance-weaving-system, creature-ai-telegraph-system, combat-encounter-system, vow-condition-tracking, rare-creature-capture-system, encounter-spawn-system |
| **Progression** | How the player grows over time | creature-jobs-evolution-system, region-mastery-automation-system, memory-dust-prestige-system, hunter-progression-system |
| **Economy** | Resource creation and consumption | the-forge-system, loot-drop-system |
| **Persistence** | Save state and continuity | save-load-persistence |
| **UI** | Player-facing information displays | combat-hud, forge-ui, creature-roster-ui, automation-config-ui, loot-filter-ui, region-view-world-map-ui, settings-menu-ui |
| **Audio** | Sound and music systems | audio-system |
| **Meta** | Systems outside the core game loop | accessibility-settings-system, onboarding-tutorial-system |

*(Narrative category removed — narrative is explicitly N/A for MVP per game-concept.md.)*

---

## Priority Tiers

| Tier | Definition | System Count |
|---|---|---|
| **MVP** | Required for the core loop to function, per game-concept.md's MVP Definition | 25 |
| **Vertical Slice** | Rare-creature capture — deferred from MVP explicitly, but the art bible already specced its visual grammar to avoid retrofit | 1 |
| **Full Vision** | Memory Dust prestige — explicitly deferred in the Scope Tiers table | 1 |

Note: several MVP systems have a **reduced MVP scope** that expands at Vertical Slice —
`resonance-weaving-system` (curated list → full Source×Form×Vow combinatorics),
`the-forge-system` (merge-only → hybrid + enchant), and `region-mastery-automation-system`
(Stages 1–2 → full stack incl. offline progression). These are the same GDD, extended, not new
systems — each GDD's Tuning Knobs section should flag which rules are MVP-locked vs.
VS-deferred.

---

## Dependency Map

### Foundation Layer (no dependencies)

1. **creature-data-schema** — Source, Role, base stats, targetable parts, evolution-tree shape. The single biggest bottleneck in the graph.
2. **item-data-schema** — base type, rarity, element, modifiers, enchantment slots. Second-biggest bottleneck.
3. **save-load-persistence** — generic serialization framework; other systems integrate into it rather than the reverse.

### Core Layer (depends on Foundation)

1. **animation-rig-system** — depends on: creature-data-schema (needs part definitions to know what to rig)
2. **input-targeting-system** — depends on: creature-data-schema (needs part definitions to know what's targetable), animation-rig-system (needs each part's current-frame transform for hit-testing and cycle-order angular sort — see ¹ above; not originally listed here)
3. **resonance-weaving-system** — depends on: item-data-schema (Vows bind to ability-focus items)
4. **the-forge-system** — depends on: item-data-schema

### Feature Layer (depends on Core)

1. **encounter-spawn-system** — depends on: creature-data-schema. Produces `power_tier`,
   the encounter-template schema, and `par_clear_time_seconds`. Reads the region's mastery level as an
   **input value**, not a system edge — see ⁴ above; adding an edge to `region-mastery-automation-system`
   would create a cycle.
2. **creature-ai-telegraph-system** — depends on: creature-data-schema, animation-rig-system, encounter-spawn-system (`power_tier` scales windup)
3. **accessibility-settings-system** — depends on: input-targeting-system
4. **combat-encounter-system** — depends on: creature-data-schema, animation-rig-system, input-targeting-system, resonance-weaving-system, creature-ai-telegraph-system, encounter-spawn-system (`par_clear_time_seconds` anchors Formula 7; `power_tier` scales creature health)
5. **vow-condition-tracking** — depends on: resonance-weaving-system, combat-encounter-system
6. **loot-drop-system** — depends on: item-data-schema, combat-encounter-system, encounter-spawn-system (`power_tier` scales rarity)
7. **creature-jobs-evolution-system** — depends on: creature-data-schema, item-data-schema, loot-drop-system
8. **region-mastery-automation-system** — depends on: combat-encounter-system, creature-jobs-evolution-system, save-load-persistence
9. **hunter-progression-system** — depends on: combat-encounter-system (writes the Hunter Stat Catalog it defines), the-forge-system (Gleam is the cost currency), loot-drop-system, region-mastery-automation-system
10. **rare-creature-capture-system** — depends on: combat-encounter-system, creature-jobs-evolution-system
11. **memory-dust-prestige-system** — depends on: region-mastery-automation-system, the-forge-system, creature-jobs-evolution-system, resonance-weaving-system

### Presentation Layer (depends on Feature)

1. **combat-hud** — depends on: combat-encounter-system, vow-condition-tracking
2. **forge-ui** — depends on: the-forge-system
3. **creature-roster-ui** — depends on: creature-jobs-evolution-system
4. **automation-config-ui** — depends on: region-mastery-automation-system
5. **loot-filter-ui** — depends on: loot-drop-system
6. **region-view-world-map-ui** — depends on: region-mastery-automation-system, encounter-spawn-system (it renders the selectable encounters)
7. **settings-menu-ui** — depends on: accessibility-settings-system, input-targeting-system, save-load-persistence

### Polish Layer (depends on everything)

1. **audio-system** — depends on: combat-encounter-system, region-mastery-automation-system
2. **onboarding-tutorial-system** — depends on: combat-encounter-system, combat-hud

---

## Recommended Design Order

| Order | System | Priority | Layer | Agent(s) | Est. Effort |
|---|---|---|---|---|---|
| 1 | creature-data-schema | MVP | Foundation | systems-designer | M |
| 2 | item-data-schema | MVP | Foundation | systems-designer | M |
| 3 | save-load-persistence | MVP | Foundation | systems-designer | S |
| 4 | animation-rig-system | MVP | Core | systems-designer | L |
| 5 | input-targeting-system | MVP | Core | systems-designer | S |
| 6 | resonance-weaving-system | MVP | Core | systems-designer | M |
| 7 | the-forge-system | MVP | Core | systems-designer | M |
| 8 | creature-ai-telegraph-system | MVP | Feature | game-designer | M |
| 9 | accessibility-settings-system | MVP | Feature | accessibility-specialist | S |
| 10 | combat-encounter-system | MVP | Feature | game-designer | L |
| 11 | vow-condition-tracking | MVP | Feature | systems-designer | S |
| 12 | loot-drop-system | MVP | Feature | economy-designer | M |
| 13 | creature-jobs-evolution-system | MVP | Feature | systems-designer | M |
| 14 | region-mastery-automation-system | MVP | Feature | systems-designer | L |
| 15 | rare-creature-capture-system | Vertical Slice | Feature | game-designer | S |
| 16 | memory-dust-prestige-system | Full Vision | Feature | economy-designer | M |
| 17 | combat-hud | MVP | Presentation | ux-designer | M |
| 18 | forge-ui | MVP | Presentation | ux-designer | S |
| 19 | creature-roster-ui | MVP | Presentation | ux-designer | S |
| 20 | automation-config-ui | MVP | Presentation | ux-designer | S |
| 21 | loot-filter-ui | MVP | Presentation | ux-designer | S |
| 22 | region-view-world-map-ui | MVP | Presentation | ux-designer | S |
| 23 | audio-system | MVP | Polish | audio-director | M |
| 24 | onboarding-tutorial-system | MVP | Polish | ux-designer | S |
| 25 | settings-menu-ui | MVP | Presentation | ux-designer | S |
| 26 | hunter-progression-system | MVP | Feature | systems-designer | M |
| 27 | encounter-spawn-system | MVP | Feature | systems-designer | M |

Rows 25–27 were added *after* the original design order was executed — they are gaps the
cross-review caught, not a re-planned sequence. In a clean re-run, `encounter-spawn-system` would
sit at position 8 (immediately after `the-forge-system`, before `creature-ai-telegraph-system`),
since three Feature-layer systems consume its `power_tier` output.

---

## Circular Dependencies

None found — but one was **narrowly avoided** and must stay avoided.

`encounter-spawn-system` scales `power_tier` with the region's progression, which is state owned by
`region-mastery-automation-system`. Modelling that as a *system dependency* would close the cycle
`region-mastery` → `combat-encounter` → `encounter-spawn` → `region-mastery`. It is instead modelled
as an **input value**: the spawn system is a pure function of `(region_id, region_mastery_level)`, and
the caller supplies the mastery level. Keep it that way. See footnote ⁴.

Sequencing note: `vow-condition-tracking` depends on both `resonance-weaving-system` and
`combat-encounter-system`, so it cannot be finalized until both are stable — it is scheduled
immediately after `combat-encounter-system` for this reason.

---

## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
|---|---|---|---|
| ~~animation-rig-system~~ | ~~Technical~~ | **RESOLVED 2026-07-14 — risk retired.** The feared artifact does not exist. | The spike was built and reviewed in-game (`Tab`): continuous rotation of real pixel art shows **no blur, no stair-stepping, no edge crawl** — at 4× zoom, in *both* render paths, including the pixel-perfect path where it should be worst. The art bible's §8.5 rotation claim was **factually wrong** and has been corrected in place. Angle-snapping is retained purely as an **art-direction choice** (the stepped, stop-motion feel was preferred), not as a mitigation — so it is now a free knob, not a constraint. The mandated per-part angle-variant textures are **withdrawn from the budget**. See `docs/architecture/ADR-002`. |
| combat-encounter-system | Design | The core hunt loop's fun-factor is unproven — the concept prototype was explicitly skipped in favor of relying on prior game-dev experience (see prototype session log) | This is now the single most load-bearing unproven assumption in the project. Strongly recommend validating at `/vertical-slice` even though the concept prototype was skipped |
| region-mastery-automation-system | Design | The active-vs-idle efficiency curve (25–40% → 100–120%) is a delicate balance explicitly flagged as a risk in game-concept.md — too generous trivializes active play, too stingy makes idle feel punishing | Tuning Knobs section must expose the curve as data, not hardcoded values, so it can be iterated post-implementation without a code change |
| vow-condition-tracking | Design | The belt-charm HUD signal's legibility at 10–15% frame height was explicitly flagged as unresolved-on-paper in art-bible §7.5, with a pre-authorized fallback (mirror larger in the corner cluster) | Playtest at `/vertical-slice`; fallback is already designed, not blocking |

---

## Progress Tracker

| Metric | Count |
|---|---|
| Total systems identified | 27 |
| Design docs written | 27 |
| Design docs cross-reviewed | 25 (`/review-all-gdds`, 2026-07-14 — verdict **FAIL**; B1–B5 now closed, re-review pending) |
| Design docs approved | 0 |
| MVP systems designed | 25/25 |
| Vertical Slice systems designed | 1/1 |
| Full Vision systems designed | 1/1 |

---

## Next Steps

- [x] Review and approve this systems enumeration
- [x] Design MVP-tier systems in the order above
- [x] Run `/review-all-gdds` — **verdict: FAIL.** Report: `design/gdd/gdd-cross-review-2026-07-14.md`
- [x] Close all 5 blockers — B1 (efficiency anchor), B2 (`encounter-spawn-system`), B3 (Formula 3b +
      `vow_fragility` re-price), B4 (`hunter-progression-system`), B5 (equipped-charm Forge gate)
- [x] Consolidate `design/registry/entities.yaml` (W16) — 51 registered facts across all four sections,
      up from 21 covering 4 systems. Gleam, Memory Dust and `creature_core` now registered; the
      `par_clear_time_seconds` anchor and Formula 3b are registered with their failure histories.
- [x] Purge stale "(not yet written)" claims (W18) — 108 occurrences across 19 GDDs, all cleared
- [ ] Reconcile bidirectional dependencies — 31 gaps found (W17)
- [ ] Re-run `/review-all-gdds` to confirm FAIL → PASS
- [ ] Run `/gate-check pre-production`
- [ ] Validate `combat-encounter-system` and `animation-rig-system` (the two highest-risk systems) as early as possible — `/vertical-slice` before committing to Production
