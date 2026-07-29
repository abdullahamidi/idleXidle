# Progression — the release curve

> **Status**: Documented for v1.0 (release-readiness milestone D).
> One-paragraph summary of how a run ramps, and how the systems feed each other, so the curve is
> intentional and guarded by tests rather than emergent.

## 1. Overview

The player runs one champion through an idle auto-battler. Fighting deeper waves drops gear (the power
axis) and grants mastery; conquering a region's boss unlocks the next region; the Warren produces the
currencies that fund the build tree and further upgrades. The whole thing keeps earning while away.

## 2. Player Fantasy

"Every session I'm visibly stronger, and the base I've built keeps paying me even when I'm not playing."
Growth you can *see* — gear power climbs, new regions open, the Warren's per-minute numbers rise.

## 3. The core loop (data flow)

```
fight waves ── drop ──▶ GEAR (power) ──▶ deeper waves ─┐
     │                                                  │
     ├── depth + conquest ──▶ MASTERY POINTS ──▶ DUST ──▶ BLESSING TREE (permanent power)
     │                                                  │
     └── conquer boss ──▶ unlock next REGION ───────────┘
                                  │
                                  └──▶ WARREN conquest bonus ──▶ idle Gleam/Mastery/Dust ──▶ upgrades
```

Every arrow is live in code. The two that milestone D tightened:
- **Conquest → Warren.** Each conquered region grants a standing `+ConquestBonusPerRegion` (10%) to all
  Warren production, so taking a region immediately raises idle income — the reward that region-creature
  farming used to give before it was scrapped.
- **Warren → mastery.** Facility-produced mastery banks into a pool that feeds the build tree, so the idle
  economy funds active progression.

## 4. The region ramp (6 regions + endgame)

| # | Region | Element | Std tier | Boss tier / HP |
|---|---|---|---|---|
| 1 | Verdant Hollow | Nature | 1 | 4 / — |
| 2 | Cinderworks | Machine | 4 | 8 / 3,200 |
| 3 | Umbral Reach | Shadow | 9 | 14 / 6,000 |
| 4 | Marrow Wastes | Body | 12 | 17 / 10,000 |
| 5 | The Still Archive | Mind | 15 | 19 / 15,000 |
| 6 | The Pale Choir | Spirit | 18 | 20 / 24,000 |

- **Difficulty** rises with power tier + boss HP; **loot** rises with it too (a kill's item level = its
  power tier, and rarity weight tilts up with tier), so pushing deeper is self-funding.
- **Conquer** = hold `ConquerWaveDepth` (7) waves in a region. Conquest unlocks the next region and adds
  the Warren conquest bonus.
- **Endgame**: once all six are conquered, the **corruption** ladder ratchets difficulty and Dust rewards
  up, indefinitely (map variety milestone C layers region modifiers onto this).

## 5. Currencies & their sinks (no dead ends)

| Currency | Faucet | Sink |
|---|---|---|
| Gleam | kills, selling loot, Warren gold facilities | gear Refine, facility upgrades |
| Materials (Scrap→Crystal) | salvaging loot | Refine / Reforge |
| Mastery Points | depth + conquest + Warren | the blessing (Dust) tree's mastery gate |
| Memory Dust | region mastery + conquest + Warren dust facilities | the blessing tree, facility upgrades |

## 6. Dependencies

Loot (`LootSystem`), Gear/PowerRating (`Gear`, `Hunter`), region world (`Regions`, `World`), mastery
(`MasteryTree`), Dust tree (`MemoryDustTree`), the Warren (`Warren`).

## 7. Tuning knobs

Region tiers/HP (`Regions.BuildRegion`), loot rarity/value curves (`LootTuning`), `ConquerWaveDepth`,
the mastery-earned formula (host `SetEarned`), Dust award rates, and all of `WarrenTuning`.

## 8. Acceptance criteria

- The loop has no dead currency and no dead-end reward (see §5).
- Deeper regions are both harder and more rewarding (tier drives difficulty *and* loot).
- Conquest raises idle income (Warren conquest bonus) — pinned by
  `test_conquering_regions_raises_production`.
- The Warren funds the tree (mastery pool → SetEarned) and is a real sink (facility costs climb).

## Known follow-up

- **CreatureCore drops are now vestigial.** Every kill still mints a `CreatureCore` (the old hatch
  currency); with creatures scrapped it is only sellable fodder. Repurposing it (e.g. a guaranteed
  material) touches the merge-recipe table + ~6 test files, so it's deferred to the release gate (F).
