# ADR-001 — Separate all game logic into a MonoGame-free core assembly

| Field | Value |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-07-14 |
| **Deciders** | technical-director (via autonomous session, per standing user instruction) |
| **Supersedes** | — |

## Context

MonoGame is a *framework*, not an engine. It ships no scene system, no entity model, no UI toolkit,
and — critically — no way to instantiate its types outside a live graphics context. `Game`,
`GraphicsDevice`, and `SpriteBatch` all require a window and a GL context to construct.

This collides directly with two of the project's own standards:

- `.claude/docs/coding-standards.md`: *"Balance formulas, gameplay systems... Required Tests"* and
  *"All public methods must be unit-testable (dependency injection over singletons)."*
- The testing standard: *"Unit tests do not call external APIs, databases, or file I/O"* and
  *"Determinism: tests must produce the same result every run."*

If the damage pipeline lives in a class that inherits from `Game`, or reads `GameTime`, or touches a
`Texture2D`, then **none of the balance math can be tested headlessly** — and this project's balance
math is where its hardest bugs live. The `/review-all-gdds` pass found that the game's central
active-vs-idle contract was *arithmetically inverted* and had survived two hand-written "proofs" in
the design docs. Numbers that only exist in prose do not get checked.

## Decision

**Three assemblies, with a strict one-way dependency.**

```
IdleXIdle.Core        (net8.0 classlib)   — ZERO MonoGame references
        ▲
        │ referenced by
        │
IdleXIdle.Game        (net8.0, DesktopGL) — rendering, input, content, the game loop
IdleXIdle.Core.Tests  (net8.0, xUnit)     — headless, deterministic
```

1. **`IdleXIdle.Core` must never reference MonoGame.** No `Microsoft.Xna.Framework` using
   directive may appear in it. All rules, formulas, state machines, and progression math live here as
   plain C#: `DamagePipeline`, `EfficiencyContract`, and everything that follows.
2. **`IdleXIdle.Game` owns everything MonoGame touches** — `SpriteBatch`, the content pipeline,
   input polling, the animation rig's rendering. It *calls into* Core and renders the result.
3. **Tuning is data, never literals.** Every formula takes a tuning record (e.g. `CombatTuning`)
   rather than reading constants inline. This satisfies *"Gameplay values must be data-driven
   (external config), never hardcoded"* and lets tests pin exact values without touching production
   defaults.

## Consequences

**Positive**

- The balance formulas are unit-testable with no window, no GL context, and no I/O. 34 tests already
  run in ~25 ms.
- The design docs' acceptance criteria become *executable*. AC21–27 of `combat-encounter-system.md`
  are now real xUnit tests that fail loudly if the contract regresses.
- This immediately paid for itself: writing the tests **caught a factual error in the review report
  itself** — the published inversion figures (166 / 92.5 / 63.2 / 50) did not reproduce; the true
  values are 216.7 / 123.8 / 86.7 / 72.2. The bug was real, the numbers were not. Prose cannot catch
  that; a test can.
- Swapping the rendering layer later (or adding a headless simulation harness for balance sweeps)
  requires no change to game logic.

**Negative**

- Some duplication of small value types. Core cannot use `Microsoft.Xna.Framework.Vector2`, so any
  geometry it needs must be its own type, converted at the boundary. Accepted deliberately: the cost
  is a handful of structs, and the alternative is untestable balance math.
- A discipline the compiler only partly enforces. `Core` has no MonoGame *package* reference, so an
  accidental `using Microsoft.Xna.Framework;` there fails to compile — which is the enforcement we
  want. Keep it that way; **do not add MonoGame as a Core dependency for convenience.**

## Alternatives considered

- **Single project, test what we can.** Rejected: it makes the highest-risk code (the balance
  contract) the *least* testable code, which is exactly backwards given this project's failure history.
- **Reference MonoGame from Core but avoid its runtime types.** Rejected: the boundary is only real if
  the compiler enforces it. A package reference makes the rule advisory, and advisory rules decay.

## Related

- `design/gdd/combat-encounter-system.md` §4 Formula 3, 3b, 7
- `design/gdd/region-mastery-automation-system.md` §4 Formula 4, 6
- `design/registry/entities.yaml` — the registered cross-system constants these formulas consume
- `tests/unit/IdleXIdle.Core.Tests/` — the executable form of the acceptance criteria
