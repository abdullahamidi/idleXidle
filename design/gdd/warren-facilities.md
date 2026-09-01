# Warren Facility Economy

> **Status**: Current · **Rewritten**: 2026-09-01 from the P12 Warren (commit 5f15730)
> · **System**: `Core/Warrens/Warren.cs` · **Screen**: `WarrenScreen` · **Host**: `Game1` (tick,
> offline credit, spend)

## 1. Overview

The Warren is the idle economy: eight facilities that produce while the player is away — on every
screen, and offline through the same tick. It pays the game's REAL wallets (Gleam, Memory Dust, and
the Forge's Scrap and Essence), never points: idle buys gear, play buys identity. Facilities open by
conquest, and every facility's level is capped by the champion's own deepest descent — the Warren
multiplies progress and can never substitute for it.

## 2. Player Fantasy

“My colony works for me while I’m away.” Leaving the game running is worth something; playing is
worth more; and the base visibly grows because of what the CHAMPION did — conquests open doors,
depth raises ceilings.

## 3. Detailed Rules

- **Eight facilities, four outputs**, in unlock order: NURSERY (Gleam 30/min), FORAGING PITS
  (Dust 15), TUNNELS (Gleam 29), SCAVENGER RUNS (Scrap 3), RITUAL NEST (Essence 1.5), SENTRY
  BURROWS (Dust 6), HOARD VAULTS (Scrap 2), BREEDING CHAMBER (Essence 1). Base rates at level 1.
- **Unlock ramp**: `UnlockedFacilityCount = min(8, 2 + regions conquered)`. The Warren itself opens
  on the first conquest, so a fresh Warren shows three cards; all eight at six conquests. A facility
  already raised past level 1 stays open under any ramp (the grandfather rule). Locked facilities
  produce nothing and cannot upgrade; their cards say what opens them.
- **Depth cap**: `FacilityLevelCap = max(1, deepestWaveAnywhere / 5)` (`Warren.CapForDepth`). A
  blocked card names the depth its NEXT level needs (`DepthForNextLevel`), never the cap.
- **Upgrades** cost Gleam + Dust (instant; no build timer), grant Warren XP = newLevel × 100;
  Warren levels need `1000 × (level + 2)` XP each.
- **Milestones**: every 5th facility level is a permanent +15% output step.
- **Offline**: the same `Tick`, over the credited absence (capped 24 h), gated on the Warren being
  unlocked — a player who has not conquered is not paid by a screen they cannot open.

## 4. Formulas

- Facility output/min = `round(BaseRate × Level × (1 + 0.15 × floor(Level/5)))`.
- Production multiplier per resource = `1 + (WarrenLevel−1)×0.03 + conquests×0.10 + track`,
  where track = WarrenLevel × {Gleam 0.02, Dust 0.009, materials 0.013}.
- Upgrade cost L→L+1 = `round(180 × 1.50^L)` Gleam + `round(22 × 1.25^L)` Dust.
- Calibration: the two Gleam facilities keep the pre-P12 four-facility total (59/min at L1 ≈ half
  an active champion); Dust was rebased ~27× down (a wave-40 checkpoint ≈ 45 min of idling);
  Scrap/Essence are sized against the one-material-per-wave trickle.

## 5. Edge Cases

- A save whose facilities out-level a fallen depth record: the card answers with the depth the next
  level needs, which can never contradict the level shown beside it.
- Fractional production carries across ticks (`_carry`), so many small ticks equal one big tick; the
  carry is not persisted — at most one unit per currency is lost per load (accepted).
- Facility save keys are enum NAMES — never rename a `FacilityKind` member.
- The retired INSIGHT pool (`WarrenMasteryPool`) is skipped on load; nothing to migrate.

## 6. Dependencies

`Hunter` (Gleam + material wallets) · `MemoryDustTree` (Dust wallet) · `World.ConqueredIds` (ramp +
conquest bonus) · `Career.DeepestAnywhere` (depth cap) · `Unlocks` (the Warren gate) ·
`SaveSystem.CreditedOfflineSeconds` (offline window).

## 7. Tuning Knobs

Everything on `WarrenTuning` (rates’ bonuses per level, cost bases/growth, XP per upgrade, conquest
bonus, milestone cadence/step) plus the catalog base rates and `Warren.DepthPerFacilityLevel`.

## 8. Acceptance Criteria

Pinned by `tests/unit/.../Warrens/WarrenTests.cs` and `Progression/PointIncomeTests.cs`: linear
output inside a band and the milestone jump; the unlock ramp (3 cards at one conquest, all 8 at
six); locked facilities produce nothing and cannot upgrade; the grandfather rule; the depth-cap
derivation (floor 1, /5); both material channels pay; determinism (3600 one-second ticks equal one
hour tick); restore round-trips; the cap binds every facility. Economy ratios live in
`gleam_economy_test.cs`.
