# IDLExIDLE

> Formerly **Resonance Hunter** — renamed 2026-08-24. The C# identity (folders, namespaces,
> projects) is `IdleXIdle`. The save folder followed on 2026-09-08 and adopts the old one once, so
> nothing was lost.

An **offline idle auto-battler**. Your champion fights wave after wave on its own — on every
screen, and while the game is closed. You never swing the sword: you read what the fight is telling
you, and you decide what the champion becomes.

**MonoGame 3.8.4.1 · C# / .NET 8 · Windows (DesktopGL)**

```bash
dotnet run --project src/IdleXIdle.Game
```

---

## What the game is

- **Twelve shared skills across six styles** — HAMMER, VOLLEY, SNARE, SIGN, FIELD, DRAIN; one
  active and one passive per style, each deepened by variations and reinforcements it earns from
  use. Each champion also has one SIGNATURE skill, and it always fills one of the build's slots. The
  build is two active and two passive slots, three keystones, and Vows —
  restrictions accepted for power. Most pay only while the build keeps the rule. Two always pay,
  and always cost something.
- **The wave is a question.** Depth is divided into bands; each band leans on an enemy archetype
  (Swarm / Armoured / Caster / Bruiser) and carries an affix that pressures a named answer —
  PLATED thickens armour, WARDED resists yesterday's favourite style, LEGION splits on death. The
  post-run report diagnoses the wall; the loop is build → run → read → build.
- **Nothing resets.** Six regions conquered in a chain, then the Corruption ratchets. Mastery points
  come from depth, and taking them back is free. Traits are awakened by play and never bought. A
  skill keeps its levels for good, even when you take it off or give its road back.
- **Idle buys gear, play buys identity.** The Warren's facilities produce Gleam, Memory Dust and
  forge materials while you are away — unlocked by conquest, level-capped by your own deepest
  descent, so the idle layer multiplies progress and never substitutes for it.
- **Offline is the real simulation.** Coming back runs the same deterministic wave sim your live
  game runs (capped, with a haircut), not a rate approximation.

Ten champions in five item classes, one shared progression. A Forge that refines, reforges,
sockets and salvages. No account, no ads, no purchases; multiplayer and leaderboards are
deliberately out (offline-only is a design decision).

---

## Architecture

```
src/IdleXIdle.Core     all game logic — ZERO MonoGame references
src/IdleXIdle.Game     rendering, input, the game loop (DesktopGL)
tests/unit             1,200+ headless tests
tests/integration      cross-system tests
```

**Core cannot reference MonoGame, and the compiler enforces it** — see
[ADR-001](docs/architecture/ADR-001-pure-logic-core-separation.md). The fight resolves a whole wave
synchronously in Core (`SoloBattle.ResolveWave`) and the screen replays the event stream; that split
is what makes the sim provable, the replay honest, and offline progress a real simulation
(`Expeditions/OfflineHunt.cs`).

Balance numbers are data (`ExpeditionTuning`, `WarrenTuning`, `LootTuning`, `SkillCatalogue`…),
never inline literals, and the suite leans hard on **liveness and economy probes**: tests that drive
the real wave pipeline and assert direction (“the affix must bite”, “the economy prices in hours of
play”) rather than pinning magic numbers. The house failure mode this guards against is the dormant
feature — built, tested green, and never actually reached by a player.

## Where the truth lives

- `design/gdd/skill-slots-and-skill-trees.md` — the skill system (CURRENT).
- `design/gdd/game-flow.md` — the loop, bands, report, checkpoints.
- `design/gdd/systems-index.md` — which design doc is current, mixed, or superseded.
- `production/audit/refactor-2026-08-31/` — the 2026-08-31 architectural refactor: fourteen area
  audits, adversarial verifications, and the phase log (P1–P16).

**Status: alpha.** The whole loop runs — fight, loot, forge, train, weave, conquer, warren,
offline — and ships on itch. Balance past the mid-game is unproven; numbers move between builds.
