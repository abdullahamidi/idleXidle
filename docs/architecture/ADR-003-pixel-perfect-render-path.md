# ADR-003 — Render to a 480×270 virtual canvas, then integer-upscale

| Field | Value |
|---|---|
| **Status** | **Superseded** (2026-07-29) — the project pivoted to hand-drawn "vector" art; the live renderer now uses `SamplerState.LinearClamp` with non-integer scaling. See the note below. |
| **Date** | 2026-07-14 |
| **Deciders** | technical-director + user (both paths compared in-game) |
| **Related** | ADR-002 (animation rig) |

> **SUPERSEDED 2026-07-29.** This ADR chose a 480×270 pixel-perfect, integer-upscale, PointClamp render
> path for flat-fill pixel art. The project has since pivoted to **hand-drawn "vector" art**: the live
> renderer draws with `SamplerState.LinearClamp` (smooth) at a 1920×1080 target, with **non-integer**
> scaling and premultiplied-at-load alpha (see `Game1.cs`, `SoloExpeditionScreen.cs`). The decision
> below is retained for history only. Pipeline of record: `design/art/asset-integration-spec.md` +
> `.claude/docs/technical-preferences.md`.

## Context

`design/art/art-bible.md` §8.2 specifies a **480×270 virtual canvas**. That spec implies a rendering
model but never states one, and two mutually exclusive implementations satisfy the words:

**A — Rotate at output resolution.** Draw straight to the backbuffer through a `CreateScale(3)`
transform. Positions are authored in virtual-canvas units, but rasterization happens at the real
output resolution (1440×810).

**B — Pixel-perfect.** Render the scene into an actual 480×270 `RenderTarget2D`, then upscale it by an
integer factor with `PointClamp`.

The difference is invisible for axis-aligned sprites and decisive for rotated ones:

- Under **A**, a rotated sprite is rasterized at output density, so its pixels end up *finer* than the
  background's. The scene contains **mixed pixel sizes**. This is not "pixel-perfect" in the purist
  sense, though many shipped pixel-art games do exactly this.
- Under **B**, every pixel on screen is identical in size and locked to the virtual grid. But rotation
  is **re-quantized onto that coarse grid every frame**, which is the textbook cause of pixel-art edge
  crawl — and was the stated reason `animation-rig-system` was the project's highest technical risk.

Nobody had ever chosen between these. The codebase was silently doing **A** because that is what the
MonoGame template does, and the art bible was describing **B**.

## Decision

**Adopt B — the pixel-perfect path.** Render into the 480×270 canvas, integer-upscale with point
sampling.

**The expected cost of B does not materialize.** Both paths were built, made toggleable (`P`), and
compared in-game on the Attacker strike arc — the widest motion arc in the game — using real pixel-art
limbs (flat fill, unbroken 1px contour, internal seam lines) at **4× zoom**:

| Path | Continuous rotation | Snapped rotation |
|---|---|---|
| A — output-resolution | Clean, no crawl | Clean |
| **B — pixel-perfect** | **Clean, no crawl** | **Clean; preferred on feel** |

B was expected to shimmer and did not. At this game's sprite scale (~20×6 px limbs) and rotation speed
(~170° in 0.15 s), point-sampled rotation onto the coarse grid simply looks correct.

So B is chosen on its own merits — it is what the art bible actually specifies, and it guarantees a
uniform pixel size across the entire screen — rather than as a trade against an artifact that turned
out to be imaginary.

## Consequences

**Positive**

- Every pixel on screen is the same size. The art direction's clean-pixel mandate holds literally, not
  approximately.
- A single, cheap post-processing seam: everything the game draws lands in one 480×270 target, so any
  future full-screen effect (palette swap, screen shake, damage flash, colourblind LUT) has an obvious
  place to live, and costs one pass rather than N.
- Resolution independence is trivial: the upscale factor is the only thing that changes across window
  sizes and displays.

**Negative / residual**

- One extra render-target bind and one full-screen blit per frame. Negligible at this resolution, and
  far inside the 16.6 ms frame budget.
- **The upscale factor must remain an integer.** Non-integer scaling reintroduces exactly the blur that
  point sampling exists to prevent, and is already a Forbidden Pattern in
  `.claude/docs/technical-preferences.md`. At non-16:9 window aspect ratios this means letterboxing,
  not stretching. **Not yet implemented** — the window is currently fixed at 3×.
- Validated at the current sprite scale only. Much larger rotating sprites could still reveal
  artifacting; re-run the spike (`Tab`) rather than assuming.

## Alternatives considered

- **A — rotate at output resolution.** Also artifact-free, and it is what the codebase was doing by
  accident. Rejected because it produces mixed pixel sizes, which contradicts §8.2's canvas spec and
  the clean-pixel mandate. Kept behind the `P` toggle so the comparison stays reproducible.
- **Render at native resolution with no virtual canvas.** Rejected: it discards the art bible's entire
  layout grid and makes the pixel-art scale dependent on the player's monitor.
