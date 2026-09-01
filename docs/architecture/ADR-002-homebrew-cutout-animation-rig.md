# ADR-002 — Homebrew cutout animation rig with angle-snapped rotation

| Field | Value |
|---|---|
| **Status** | **Accepted** — spike built and validated by playtest, 2026-07-14. |
| **Date** | 2026-07-14 |
| **Deciders** | technical-director + user (spike reviewed in-game) |
| **Related** | ADR-001 (pure-logic core), ADR-003 (pixel-perfect render path — now Superseded) |

> **UPDATED 2026-07-29.** Two things changed under this ADR without invalidating it. (1) The render
> pipeline pivoted to hand-drawn "vector" art (`SamplerState.LinearClamp`, non-integer scaling; ADR-003
> Superseded), so **angle-snapping's original anti-"pixel-edge-crawl" justification no longer applies** —
> the spike had already found that artifact didn't reproduce. Snapping is kept only as an *optional
> art-direction* (stop-motion) knob; `SnapSteps` stays a tuning value. (2) The cutout rig is now being
> **adopted for the player champion** (Option B), authored **front-facing and painterly** to match the
> shipped art — see `design/art/character-art-spec-batch1.md`. The core decision (a homebrew cutout rig
> over frame-by-frame sheets, to fit the texture budget) stands.

## Context

MonoGame ships **no animation system**. Anything that moves is built from scratch.

`design/art/art-bible.md` §8.5 committed to a homebrew *cutout* rig (sprites rotating about pivots)
rather than frame-by-frame sprite sheets, because frame-by-frame across the 6-Source × 5-Role creature
matrix at ~25–40 frames each does not fit the 512 MB texture ceiling. That reasoning is sound and
stands.

The art bible additionally asserted a **technical** justification for quantizing rotation angles:

> Rotating pixel art by an arbitrary angle resamples the source grid and destroys the hard 1px edges
> the legibility model depends on. A continuously varying angle makes those edges **crawl and shimmer**
> frame to frame.

`systems-index.md` accordingly named `animation-rig-system` the project's **highest technical risk**
and required the Attacker strike arc — the widest motion arc in the roster, the worst case for such
artifacts — be spiked before the creature roster was committed to the rig.

## What the spike actually found

**That assertion is false.** It was tested and it does not reproduce.

The spike renders the Attacker strike arc twice from identical pose data — angle-snapped vs
effectively-continuous — on real pixel-art limbs (flat fill, unbroken 1px dark contour, internal seam
lines: the art bible's own grammar, and precisely the hard edges the claim says rotation destroys).
Reviewed in-game at **4× zoom** under point sampling, in **both** render paths:

| Render path | Continuous rotation | Verdict |
|---|---|---|
| Rotate at output resolution (1440×810) | No crawl, no shimmer | Clean |
| **Pixel-perfect** (480×270 target → integer upscale) | **No crawl, no shimmer** | **Clean** |

The second row is the surprising one. Re-quantizing rotation onto a coarse virtual grid every frame is
the textbook cause of pixel-art edge crawl, and it is the path the art bible's canvas spec implies. It
still did not crawl at the sprite sizes and rotation speeds this game actually uses.

**Two false-premise defects have now been found this way** (the other being the inverted efficiency
contract, ADR-001 / Blocker B1). Both survived multiple passes of document review and died on first
contact with something executable.

## Decision

**Keep the cutout rig. Keep angle-snapping. But keep it for a completely different reason.**

1. **Angle-snapping is retained as an ART-DIRECTION choice, not a technical mitigation.** Reviewed
   side by side, the snapped arm reads as deliberate, stepped, stop-motion-like movement, and it was
   preferred on feel. That is a sufficient and honest reason to ship it.
2. **`SnapSteps = 16` (22.5° increments), locked as a default.** Chosen by eye against the widest arc
   in the game.
3. **It is now a knob, not a constraint.** Because nothing breaks without it, the step count may be
   tuned freely, varied per creature, or disabled entirely, with no correctness consequence. (An
   obvious future use: tie snap coarseness to the creature's **Source** — a `machine` creature moving
   in harder steps than a `spirit` one — turning a rendering parameter into characterisation. Not
   scoped now.)
4. **Snap at the render boundary only.** `Rig.Evaluate` composes the hierarchy on **unsnapped** angles
   and exposes `Angle` (true) and `SnappedAngle` (quantized). Snapping mid-hierarchy would compound
   quantization error down the chain and make a limb's tip drift. Pinned by
   `test_quantization_error_does_not_compound_down_the_chain`.

## Consequences

**Positive**

- **`animation-rig-system` is no longer the project's highest technical risk.** The unknown it was
  flagged for does not exist. The risk register in `systems-index.md` is updated accordingly.
- The rig is free to be simple. There is no artifact to engineer around, so no need for pre-rasterized
  angle variants, no per-angle texture budget, no special-casing of wide arcs.
- Snapping is now optional, which means it can also become an **accessibility / preference** toggle
  later at zero design cost.

**Negative / residual**

- The art bible §8.5 contains a **factually incorrect technical claim** which has been corrected in
  place. Anything else that was justified *by that claim* should be re-examined rather than inherited.
- Validated at the current sprite scale (limbs ~20×6 px) and rotation speed (~170° in 0.15 s). Much
  larger sprites, or much slower sweeps, could still reveal artifacts. If a creature is authored well
  outside those bounds, re-run the spike (**Tab** in-game) rather than assuming.
- `MonoGame.Extended` (Tweening) is listed in technical-preferences as the intended curve-evaluation
  dependency but is **not integrated**; `Clip` implements its own four easing curves and they are
  sufficient. The dependency should be dropped from the allowed-libraries list unless something else
  needs it.

## Alternatives considered

- **Frame-by-frame sprite sheets.** Rejected on texture budget. Unchanged.
- **Continuous rotation (no snapping).** Technically viable — this is the finding. Rejected on
  **aesthetics**, not on correctness, and that reason is recorded plainly so nobody later "fixes" the
  stepping as though it were a bug.

## How to re-verify

```
dotnet run --project src/IdleXIdle.Game
```
**Tab** → spike · **Z** 4× zoom · **H** hold the worst frame · **D** slow-mo ·
**Left/Right** change snap steps · **P** toggle render path.
