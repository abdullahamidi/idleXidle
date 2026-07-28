# Warren Facility Economy

> **Status**: Implemented (v1)
> **System**: `ResonanceHunter.Core.Warrens.Warren`
> **Screen**: `WarrenScreen` (nav: WARREN)
> **Reference**: `warren_management_dashboard_in_dark_fantasy.png`, `warren_screen_production_spec_revision_1.md`

## 1. Overview

The Warren is a global idle base of eight facilities that passively produce the game's real
currencies. Each facility has a level and a per-minute output; the player spends currency to upgrade
facilities, which raises their output and grants Warren XP; Warren levels grant a global production
bonus. The creature-team automation that previously occupied the Warren nav slot is preserved as the
Warren's **CREATURES** sub-view.

## 2. Player Fantasy

"My colony works for me while I'm away." The Warren is the visible, growing engine of passive
progression — you watch numbers climb, choose which facility to pour resources into next, and feel the
whole base get faster each time the Warren levels. It funds the rest of the game (build tree, prestige,
gold), so investing in it is never a dead end.

## 3. Detailed Rules

- Eight facilities, fixed set: Nursery, Tunnels, Foraging Pits, Scavenger Runs, Breeding Chamber,
  Ritual Nest, Hoard Vaults, Sentry Burrows. Each produces exactly one currency:
  - **Gleam** (gold): Nursery, Tunnels, Scavenger Runs, Hoard Vaults
  - **Mastery Points**: Breeding Chamber, Ritual Nest
  - **Memory Dust** (the "Nature" track): Foraging Pits, Sentry Burrows
- Production accrues every real second (on every screen) and during offline catch-up, using the same
  24-hour offline cap the region farms use.
- Gleam and Dust credit the player's real shared balances. Mastery credits a **produced-mastery pool**
  that is added to the derived Mastery-Earned total — so a Breeding facility genuinely funds the build
  tree, and spending that pool on an upgrade correctly lowers available tree points.
- Upgrading a facility is **instant** (no build timer in v1). It costs Gleam + Mastery + Dust, raises the
  facility's level by one, and grants Warren XP. Enough XP levels the Warren, which raises the global
  "+% All Production" bonus and the per-currency bonus tracks.
- Upgrades are gated honestly: the button disables when any of the three costs is unaffordable.

## 4. Formulas

Let `L` = facility level, `WL` = Warren level.

- **Facility raw output/min** = `round(BaseRate × L)` (BaseRate per facility; e.g. Nursery 520).
- **All-production bonus** = `(WL − 1) × 0.03`.
- **Per-currency bonus** = `WL × k`, `k` = 0.02 Gleam / 0.013 Mastery / 0.009 Dust.
- **Production multiplier(currency)** = `1 + AllProductionBonus + PerCurrencyBonus`.
- **Total production/min(currency)** = `sum(raw outputs of that currency's facilities) × multiplier`.
- **Upgrade cost** (to go `L → L+1`): `round(Base × Growth^L)` per currency —
  Gleam `18700 × 1.40^L`, Mastery `43 × 1.25^L`, Dust `22 × 1.25^L`.
- **XP per upgrade** = `newLevel × 100`. **XP to next Warren level** = `1000 × (WL + 2)`.

## 5. Edge Cases

- **Tick determinism**: production carries the un-credited `rate × seconds` and divides by 60 only at the
  floor step, so 3600 one-second ticks credit exactly what one 3600-second tick does (unit-tested).
- **Mastery is derived**: it cannot be stored on the tree, so the produced-mastery pool is persisted
  separately and folded into `SetEarned` each frame.
- **Save compatibility**: all Warren fields default (level 1, empty facilities, pool 0), so pre-Warren
  saves load a fresh level-1 Warren with no version bump.
- **Unknown facility key on load**: ignored (crash-safe), never throws.
- **Warren name**: derived from the active region; a missing/invalid region id falls back to "THE WARREN".

## 6. Dependencies

- `Hunter` (Gleam balance; `AddGleam`/`SpendGleam`).
- `MemoryDustTree` (Dust balance; `AwardFromMastery`/`Spend`).
- `MasteryTree` (Earned is derived; the pool is added in the host's `SetEarned`).
- `SaveGame` / `SaveSystem` (persisted level, XP, facility levels, mastery pool).
- `Regions` (Warren display name).
- `AutomationScreen` (the CREATURES sub-view — unchanged).

## 7. Tuning Knobs

All in `WarrenTuning`: `AllProductionPerLevel`, `GleamBonusPerLevel`, `MasteryBonusPerLevel`,
`DustBonusPerLevel`, the three `*CostBase` / `*CostGrowth` pairs, and `XpPerUpgradeLevel`. Per-facility
`BaseRatePerMin` lives in the `Facilities` catalog.

## 8. Acceptance Criteria

- New Warren: level 1, eight facilities at level 1, all three currencies produced. ✓ (unit)
- Output scales with facility level; higher Warren level multiplies production. ✓ (unit)
- Upgrade cost rises with level; `CanAfford` gates on all three currencies. ✓ (unit)
- Upgrade raises level + grants XP; enough XP levels the Warren. ✓ (unit)
- Many small ticks equal one big tick (determinism). ✓ (unit)
- Save round-trips level, XP, and facility levels. ✓ (unit)
- Screen renders 1920×1080 with the 4-panel layout, all real values, no forbidden assets. ✓ (screenshot)
