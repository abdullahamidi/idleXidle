# Resonance Hunter — v1.0 Release Notes

> The release-readiness push (design: `docs/superpowers/specs/2026-07-28-release-readiness-design.md`).
> All content variety here is **data/systems over the existing art** — no new assets required.

## Player-facing highlights

- **The Warren is a finished idle base.** Eight facilities produce Gleam, Mastery, and Dust every second
  (even offline). Upgrades are instant, and every 5 levels a facility crosses a **milestone** that
  permanently steps up its output. Conquering regions now grants a standing **+production bonus**, so the
  world map and the base feed each other.
- **A far deeper item pool.** Gear now rolls a **name** from its strongest attribute —
  "FURIOUS SHADOW BLADE", "IMMORTAL MACHINE SPEAR", "WARDED NATURE HELM" — with wider attribute spreads so
  two of the same base really differ. Item power shown on the gear screen now exactly matches the power you
  gain on equip.
- **A bigger blessing tree.** The Dust tree grew from 36 to 44 nodes and gained two keystones — including a
  new **FORTUNE** road that trades combat power for loot rarity, and **TITAN** for the ultimate fortress build.
- **Regions play differently.** Each region carries a themed **modifier** (Umbral Veil, Molten Plating,
  Pale Requiem, …) that twists enemy health/damage and sweetens the loot, on top of the endless corruption
  ladder — deeper regions are both harder and richer.
- **The creature/den system was retired** from the Warren; the base economy replaces it.

## Progression (documented)

The full loop — fight → gear + mastery → deeper waves; conquer → next region + Warren bonus; Warren →
idle income → upgrades — is documented and guarded by tests in `design/gdd/progression.md`.

## Release gate — status

- **Build**: Core + Game compile clean (0 warnings, 0 errors).
- **Tests**: 474 unit + 2 integration — all green.
- **Combat smoke**: fight and boss encounters run with region modifiers applied.
- **Screens**: all eight (Hunt/Gear/Stats/Build/Forge/Warren/Map/Dust) build to their production specs and
  verified by screenshot this cycle.

## Known follow-ups (non-blocking)

- **Art** is the outstanding work, tracked in `design/art/asset-requests.md`: bespoke weapon/item art,
  standard-enemy gaps, three region emblems (marrow/still/pale), and a painted world-map illustration
  (the map currently uses a stone-texture backdrop).
- **Vestigial CreatureCore drops**: every kill still mints a core (the old hatch currency) that is now only
  sellable; repurposing it into a material touches the merge-recipe table + several tests.
- **Dormant Core**: the creature/automation/evolution systems remain in the codebase (unreached) to keep
  old saves loading and tests green; a full deletion is a larger follow-up.
