# IDLExIDLE

> Formerly **Resonance Hunter** — renamed to IDLExIDLE on 2026-08-24; the C# identity (folders, namespaces, projects) is `IdleXIdle` since 2026-08-31. The save folder alone keeps the old name so existing saves stay intact.

A 2D pixel-art **idle action RPG**. You hunt corrupted spirit creatures by reading their attacks and
striking the openings that reading them creates. Then you teach your creatures to do it for you.

**MonoGame 3.8.4.1 · C# / .NET 8 · Windows, Linux, macOS**

```bash
dotnet run --project src/IdleXIdle.Game
```

> **Status: playable prototype.** The full loop runs — hunt, loot, forge, train, hatch, farm, save,
> come back to offline progress. The art is flat greybox rectangles on purpose: this build exists to
> answer whether the *loop* is fun, which no amount of design documentation can settle.

---

## What makes it different

**Defence is how you create offence.** There is no cooldown telling you when to use an ability. A
creature winds up an attack; if you dodge or interrupt it, a **weak point** opens. Hitting weak points
is the only way to do real damage. Skilled defence is the *source* of offensive opportunity — that
inversion is the whole design.

**Clicking faster never helps.** Your basic attacks fire on a fixed 1.2-second timer. Clicking a body
part *steers* the next tick; it never adds one. DPS is mathematically independent of click rate, so the
game has a clicker's tactility without being a test of how fast you can move a mouse.

**Automation is earned, and idle is never worthless.** Every kill drops a creature core. Hatch it,
assign it to a farm team, and the region starts hunting without you. A *balanced* team of four roles
beats a stack of your strongest creatures by nearly 3× — composition is a real puzzle, not a UI. But
active play always beats the best possible farm, and never by more than ~3×.

**Builds are trade-offs, not stat stacks.** Every ability carries a **Vow** — a restriction accepted in
exchange for power. *Reckless Offering* costs 15% of your maximum health, permanently. *Vow of the
Bloodied* only works below 40% health. A rarer condition is always worth more, so no Vow can dominate
another.

---

## Controls

| Key | |
|---|---|
| **Click** | Aim at a body part |
| **Space** | Dodge — zero damage, opens a weak point |
| **F** | Interrupt — zero damage, opens a weak point. The best answer. |
| **Shift** | Block — always available, but it still hurts |
| **1 / 2 / 3** | Cast an ability (costs Resonance, earned by defending) |
| **R** / **B** | Next hunt / fight the boss |
| **F** *(results screen)* | The Forge — sell, merge, dismantle |
| **A** | Automation — hatch creatures, build an idle farm |
| **Tab** | Animation-rig spike (tech demo) |
| **F1** | Help |

---

## Architecture

```
src/IdleXIdle.Core     all game logic — ZERO MonoGame references
src/IdleXIdle.Game     rendering, input, the game loop (DesktopGL)
tests/unit                   176 tests, headless, ~70 ms
tests/integration            3 cross-system tests
```

**Core cannot reference MonoGame, and the compiler enforces it.** `Game`, `GraphicsDevice` and
`SpriteBatch` cannot be constructed without a window, so any formula living near them is untestable —
and this project's hardest bugs are all in the balance math. See
[ADR-001](docs/architecture/ADR-001-pure-logic-core-separation.md).

Every balance number is data (`CombatTuning`, `LootTuning`, `SpawnTuning`…), never an inline literal.

---

## Why the balance math is tested this hard

Four claims in the design documents were confidently wrong, survived multiple review passes, and died
on first contact with something executable.

1. **The active/idle contract was inverted.** Active efficiency was measured against automation's *live*
   clear time, which improves with mastery — so identical play scored **216% → 72%** as your own farm
   got better, and a skilled player ended up scoring *below* the automation baseline they were being
   compared against. The loot premium for playing actively silently clamped to zero. Both existing
   "proofs" in the docs compared numbers from different mastery levels.
   Fixed by anchoring both halves to a fixed authored par time.

2. **The review report's own numbers didn't reproduce.** It published the inversion as `166 / 92.5 /
   63.2 / 50`. Implementing it produced `216.7 / 123.8 / 86.7 / 72.2`. The bug was real; the numbers
   were fiction. Balance figures now live in `BlockerB1RegressionTests.cs`, not in prose.

3. **The art bible's rotation premise was false.** It claimed rotating pixel art would make edges crawl,
   and the animation rig was built to defend against that. It doesn't crawl — not at output resolution,
   not even in pixel-perfect mode where theory says it should be worst. Angle-snapping is kept purely
   because the stepped look was *preferred*, and the per-part angle-variant textures it mandated came
   straight back out of the budget. See
   [ADR-002](docs/architecture/ADR-002-homebrew-cutout-animation-rig.md).

4. **`vow_fragility` was free — twice.** First because the player's `defense` stat was read by *no
   formula anywhere in the project*. Then, once defense went live, because it starts at **zero** — so
   "−20% of your defense" still cost nothing to anyone who never trained it. A cost expressed as a
   fraction of an investable stat is always avoidable by declining the investment. It is now a flat
   post-mitigation damage penalty.

And one that only an integration test could catch: **the idle farm produced nothing.** Kills were
`(int)(seconds / clearTime)`, which truncates — and the game ticks automation once per second against a
clear time of tens of seconds. Every live tick computed `(int)0.025 == 0`. It only *looked* correct
because every test used one large offline catch-up tick.

---

## What is built

Every major system from the 27-document design is implemented and tested: precision combat (both
damage pipelines), encounter spawning, loot, the Forge, Hunter progression, region mastery and
automation, Resonance Weaving, **branching creature evolution**, **rare-creature capture** with the
Capture Ward, **Memory Dust prestige** (where nothing resets), save/load with offline progression, and
the animation rig (spiked and validated). 215 unit + 3 integration tests.

## What is not built yet

- **Art, animation, audio.** The rig is spiked and validated, but no creature is authored against it —
  everything is flat greybox shapes with a hand-built pixel font.
- **A second region.** Only Verdant Hollow exists; the systems are region-agnostic but the content is
  one region deep.
- **The balance numbers are placeholders.** They are internally consistent and pillar-compliant. That is
  not the same as being *fun*, and only playing will tell.

Design lives in [`design/gdd/`](design/gdd/) — 27 system documents, with a
[cross-review](design/gdd/gdd-cross-review-2026-07-14.md) and its remediation record.

---

*Built with the [Claude Code Game Studios](docs/TEMPLATE-README.md) template.*
