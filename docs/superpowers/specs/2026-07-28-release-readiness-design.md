# Release Readiness — v1.0 content, progression & Warren

> **Date**: 2026-07-28
> **Goal**: Bring the game to a releasable v1.0 **using existing art only**. Add variety through data and
> systems, solidify the progression curve end-to-end, and finish the Warren.
> **Approved sequence**: E → D → A → B → C → F.

## Constraints (apply to every milestone)

- **No new art.** All variety comes from data/systems reusing the current asset set.
- **Data honesty.** Every visible value comes from the real model (no invented numbers).
- **Verified.** Each milestone lands with unit tests and, where visual, a 1920×1080 screenshot, and is
  committed on its own.
- **Non-destructive.** Existing saves must keep loading (new fields default).

---

## E. Warren → release-ready (first)

The facility economy exists (8 facilities, level/XP, instant upgrades, +% production). To finish it:

- **Balance pass** on the curves: facility base rates, upgrade-cost growth, XP-to-level, and the
  per-level/global production bonuses — so early upgrades are affordable, late ones meaningful, and Warren
  income *supports* progression without dominating combat rewards.
- **Depth: facility milestones.** At facility levels 5/10/15/20/25 a facility crosses a **milestone** that
  grants a permanent step-bump to its output and a small global nudge — long-term goals beyond "+1 level".
  Surfaced on the card and in the detail panel.
- **Robustness**: extend save/round-trip + offline-accrual tests to cover post-upgrade and milestone state.
- **Acceptance**: coherent idle economy; milestones visible; all Warren tests green; screenshot verified.

## D. Progression solidification

Make the spine coherent from wave 1 to endgame: **wave depth → mastery points → Dust; gear power → deeper
waves; conquest → next region; Warren → passive income**.

- Define and tune the intended curve across the 6 regions (tiers, boss health, loot-rarity-by-depth,
  mastery/dust rates) + the corruption endgame, so power gained ≈ difficulty faced in early/mid/late.
- Close any incoherent links (reward scaling on conquest, dust cadence).
- **Acceptance**: a documented progression curve; `balance-check` clean; sensible fresh→endgame ramp.

## A. Item variety & depth

- **Prefix system**: the dominant rolled affix picks a prefix — e.g. Damage→"Furious", Health→"Immortal",
  Crit→"Vicious", Defense→"Warded", SkillRate→"Nimble", Haul→"Greedy". Item name reads
  `PREFIX · ELEMENT · TYPE` (e.g. "FURIOUS SHADOW BLADE"), all on existing art.
- **Deeper rolls**: widen the affix pool ranges and ensure Rare+ items roll richer/more affixes so
  same-base items genuinely differ.
- **Fix**: enchant *triggers* currently fire from only 3 of 8 gear slots — extend to all 8.
- **Acceptance**: varied names + attributes per item; enchants work on all slots; tests.

## B. Skill-tree variety

- Expand the Dust mastery tree with new branches (crit, resonance, haul, defense lines) and a few new
  **keystones**; add Vow/Form options where they wire cheaply. Uses existing glyphs.
- **Acceptance**: more meaningful build choices; tree-validation tests (reachability, keystone teachability,
  cost totals) still pass.

## C. Map variety

- **Region modifiers**: a pool of modifiers (enemy +damage, +health, faster attacks, etc.). Each region
  carries a small themed set; the corruption endgame applies escalating modifier sets per tier. Shown on
  the Map detail panel; applied in combat.
- **Acceptance**: regions play differently; modifiers are real and affect the fight; tests.

## F. Release gate

- Run `content-audit`, `balance-check`, `smoke-check`; fix blockers; produce changelog / patch-notes.
- **Acceptance**: gate PASS; build + full test suite green.

---

## Out of scope (explicitly)

- New art of any kind (weapons, enemies, regions, VFX). Tracked separately in
  `design/art/asset-requests.md`.
- Deleting the dormant Core creature systems (kept for save/test continuity).
- Multiplayer, store integration, platform certification.
