# ADR-013: BRAND's curse is one shader pass per afflicted creature over baked host data

| Field | Value |
|-------|-------|
| **Status** | **Accepted** (2026-10-02, the owner: "The BRAND visual direction and production renderer are approved. Lock them."). The production renderer is ACCEPTED and LOCKED; no further visual redesign unless audio integration exposes a genuine presentation defect. BRAND as the overall gold-standard MARK / PERSISTENT TARGET AFFLICTION reference is **NOT closed** until its final audio is chosen (page https://claude.ai/artifact/SBJLCVwBV4ztdiPvFQanFD, evidence `production/qa/evidence/brand-production/`). |
| **Date** | 2026-10-02 |
| **Deciders** | owner; lead-programmer, technical-artist (presentation), tools-programmer (bake) |
| **Supersedes** | the BRAND curse prototype (`CursePrototype*.cs`, `RH_BRAND_CONCEPT`), the etched-cut mark draw (ADR-011's first BRAND slices) |
| **Related** | ADR-009 (premultiplied blends), ADR-011 (BRAND sections), ADR-012 (extended attack canvas) |
| **Enforced by** | `brand_curse_test.cs`, `brand_curse_shader_test.cs`, `brand_curse_presentation_test.cs`, `brand_host_bake_test.cs`, `brand_mark_test.cs` (run in CI) |

## Context

The owner approved BRAND's look (Living Shadow Corruption: separate infected territories, the host's own material
drained, depth = more of the body; Shadow-violet on contrasting hosts, Ash-Burn on hosts whose palette conflicts).
That look was built as a prototype, and the prototype is not shippable:

- **GPU read-backs in combat.** Each strip a cursed creature first shows (idle, then its first lunge, then its death
  strip *on the frame it dies*) did a whole-strip `Texture2D.GetData` (~2 M px), a per-pixel CPU drain, and
  uploaded two 8 MB drained / burned copies, kept forever in a static cache.
- **Batch cost.** Every cursed creature broke the arena batch and opened 5-7 more SpriteBatch passes (stencil
  write, stain, screen, fissure, bloom, two wisp passes) plus 1-8 `DrawUserPrimitives`. A SPRAWL row of five was
  30-45 batch boundaries against a 10-20 frame budget.
- **Unstable seats.** The territory layout was computed on the first frame seen and cached forever, so a squashed,
  flipped or extended-canvas first frame could become the creature's permanent seat.
- **It only ran behind `RH_BRAND_CONCEPT`.** Without it, the game still drew the rejected etched mark, and the
  canvas had no stencil buffer.
- **Leaks.** One `AlphaTestEffect`, 96 `DepthStencilState`s and a `DualTextureEffect` per wave, never disposed.

## Decision

### 1. One custom effect, one draw per afflicted creature

`Content/Shaders/BrandCurse.fx` (`BrandCurseEffect`) is the project's first custom `Effect`. At each cursed
creature the arena batch is ended and the creature's CURRENT frame (`SpriteFrame`: texture, src, dest, flip, the
same squash and tint) is drawn through `SpriteBatch.Begin(..., effect: BrandCurseEffect)`. **The pass is the
creature's only draw**: before drawing it, the arena asks the curse whether its pass will run this frame
(`CursePresentation.PassDue` / `LeavingPassDue`, exactly the composition's own answer) and if so draws that frame
with a transparent tint (it keeps the frame the curse and the focus light read). Should no pass run after all,
`DrawOn` / `DrawLeaving` draw the plain frame, so the creature never vanishes. The pixel shader samples the host
texel under it and computes the whole corrupted result in the prototype's exact pass order:

1. drain / burn per territory (the old Drained / Burned copies, now computed from the host texel and the host's
   baked look: `level = DrainedMean + (l - Luma) * 0.72`, the tint lerps, `ash = AshMean + (l - Luma) * 1.3`);
2. the body shade, then the stain multiplies (tissue, pale bruise, dark, burnt edge);
3. the screen pass (tissue glow, emission, idle accent);
4. the additive pass (Ash-Burn fissures, bloom front).

It outputs premultiplied `(lerp(S, T, cov) * A, A)` with `A` = the host texel's alpha × the draw tint's alpha (the
plain sprite's own coverage), `S` the plain sprite's colour and `cov` the old stencil's threshold (90/255) as a narrow
smooth ramp: above it the corrupted host, under it the creature exactly as the arena draws it. Every texel is
composited once, so the result over the background `B` is `A * lerp(S, T, cov) + (1 - A) * B`. (The first
production build drew the pass OVER the arena's draw: a soft texel ended at `1 - (1 - a)^2` coverage, the
anti-aliased rim hardened and popped as the pass began and ended; pinned by
`test_the_curse_pass_composites_every_texel_once_like_the_plain_sprite`.) The canvas needs no depth/stencil buffer
at all.

Per-creature data are effect parameters (the host look, up to four territories' host-UV-to-mask affines, and their
reveal / light / burn / accent levels). Parameters change only between draws, and the arena batch is broken for
each afflicted creature anyway (the curse sits over its own creature and under later-slot creatures), so this is
not per-entity effect switching inside a batch.

**Cost:** one extra End/Begin pair + one draw per afflicted creature (from ~6-8 pairs + 1-8 quads); the arena's
own quad for that creature is drawn transparent.

### 2. The territory masks are one packed texture

The procedural territory atlas (same generator, same seed `20261002`) is packed into ONE RGBA texture: per
variant three pages (`drain, burn, tissue, dark` / `edge, emit, fissure, birth` / `idle0, idle1, idle2, front`)
plus the wisp puff. It is generated on the CPU and uploaded once at load time (`SetData` only, never `GetData`),
not on a combat frame. The six stepped bloom frames become analytic: `reveal = saturate((at - birth) / 0.14)` in
the shader, so the bloom grows continuously instead of in ~80 ms steps.

### 3. Host data is baked offline, never read back

`<idle strip>.brand.json` sits beside each creature's idle strip (and is shipped like `.mark.json`). It holds the
host look (luma, chroma, native violet share, shadow share, Ash-Burn weight) measured from the idle strip, and the
creature's two territory layouts (slot parity 0 / 1): each seat's offset from the strip's mean authored body point
and its diameter in source pixels, its own burn, and how many seats the body has room for. The bake reuses the
same C# measuring and seating code (pure functions, no device) reading the PNGs with a CPU decoder:
`brand_host_bake_test.cs` recomputes every file and fails if a committed one is stale; with `RH_BRAND_BAKE=1` it
writes them instead. At runtime the game only reads JSON at load (`CurseHostData.Warm`, like `MarkPoints.Warm`).
Attack and death strips use their creature's idle-strip data.

### 4. Seating from stable data, with a better first seat

The seat is measured once, offline, from the idle strip's stable mask (cells solid in >= 75 % of frames) and the
mean authored body point: never from whatever frame was drawn first. At runtime each territory hangs from the
CURRENT frame's authored body point (idle, attack, death all have one), so the layout is the same on every
animation. The depth-1 score gains generic terms (no names): distance from the silhouette boundary, local
thickness, persistent overlap across frames, distance from narrow appendages, and the visible change the
corruption makes there (see §Seating fix below for what actually moved the Crystal Lich off its waist cloth).
On the 30 shipped creatures `brand_host_bake_test` pins every seat, not only the first: each later seat (depth 2 and
3, both slot layouts) keeps within 20 % of the authored head boxes (the verdant scarab's second, on the front of its
shell inside its head rectangle, 25 %) with its centre on the body in at least 7 of 8 idle frames; and the host mode
the owner approved on the parity hosts (Ash-Burn on the Void Reaper, the black whelp and every umbral host; Shadow
violet on the Crystal Lich, the Forge Colossus and the pale wisp; the Spirit Matron's blend) is read from the
committed files, so a re-bake that flips one fails.

### 5. Presentation-state corrections

- Old territories' idle accent and afterglow are continuous through a deepen (no one-frame drop to 0 / jump of +50 %).
- A multi-depth jump blooms every new territory, staggered (90 ms), never popped.
- A deepen during an arrival composes deterministically: per-territory clocks; the arrival is never restarted.
- The boss now plays the death story (flare, collapse, smoke-out) like the pack; the single-creature path shudders
  like the others.
- Waiting creatures (SPRAWL hops in flight) take their faint shade through their own draw tint: no extra batch.
- Nothing the shader or the streaks are handed steps between frames (pinned by 1 ms sampling in
  `brand_curse_presentation_test`). Settle and afterglow work per territory, and the afterglow takes over from the front
  over the bloom's last 120 ms. Each old territory reacts to every deepen (not only the last). The travel puffs keep the
  four brightest, each less the fifth. The body shade eases across a stage step, rises through an apply's gather and
  hands over from the waiting shade on an arrival, and the waiting shade itself rises in. A step down fades (400 ms). A
  dying host's living light, drain and front carry into the flare and collapse, its in-flight wisps and travel fade, and
  the smoke-out eases in.
- The leaving is timed on the playhead, so the screen keeps the playhead running through the wave-clear break while
  any host's curse is still leaving (`CursePresentation.StillLeaving` in `ActionStillPlaying`, as PRESS's crush):
  a wave-ending kill's leaving (always the boss's) froze mid-flash on the corpse for the whole break. Wisps of a dying
  host only go on from what it wore at the fall: none starts after it, none rises from a territory born after it.
- The shader's first draw (the driver's program link) and the composition's JIT are paid at load
  (`BrandCurseEffect.Warm`, `CursePresentation.Rehearse`): before this, the first cursed combat frame cost 8-12 ms.
  `mark-draw` also carries `batches=` and `flush=` (the part of `ticks` spent flushing the arena early).

### 6. Wisps and smoke-out stay sprites

Wisps and the death smoke-out are unclipped puff streaks drawn in the reopened arena batch (premultiplied alpha-over;
the emission core as premultiplied additive, alpha 0, which approximates the old screen blend for these dim cores).

### 7. The shader binary is committed

CI builds on Ubuntu, where `mgfxc` needs Wine. The shader is compiled on Windows by `tools/shaders/build_shaders.sh`
(`mgfxc /Profile:OpenGL`) to `assets/shaders/brand_curse.mgfxo`, which is committed and loaded with
`new Effect(device, bytes)`. A test pins the `.fx` source hash recorded beside the binary, so an edited shader that
was not rebuilt fails. CI runs the device-free BRAND guards of the Game test project (`brand_curse_shader_test`,
`brand_host_bake_test`, `brand_curse_presentation_test`, `brand_curse_test`, `brand_mark_test`) in its own step
(`.github/workflows/ci.yml`, "BRAND curse guards"); the rest of the Game suite is still local-only.

### 8. What is removed

`RH_BRAND_CONCEPT`, Concepts B / C, `RH_CURSE_NOCLIP`, the stencil machinery, the drained / burned copies and host
cache, the etched-cut draw (`MarkPerformance` drawing, the dead `MarkRecipe` fields, `fxp_seeker_brand.png`).
`MarkPerformance` keeps the mark's truth (host chain, phases, stages, SPRAWL schedule, quiet windows, pins).
`RH_MARK_RECIPES=0` stays as the QA "uncursed twin" switch; `RH_PRESENT_TRACE` keeps `mark-draw` (now with
`draws=`, `ticks=`) and `curse-seat`.

## Alternatives rejected

- **Keep the stencil + SpriteBatch passes, just merge them.** Still several passes per creature, still a stencil
  buffer, and the drained copies would still need the host texels on the CPU or extra render targets.
- **Draw every curse after all creatures in one batch.** A territory computed from its host texel would paint over a
  later-slot creature that overlaps it; fixing that needs a depth or stencil buffer again.
- **Custom vertex format with all parameters per vertex.** One draw for all creatures sharing a strip, but the order
  problem above remains, and the vertex would carry ~16 floats; not worth it for <= 5 cursed creatures.
- **Bake the drained copies as PNGs.** 8 MB each per strip resident, and still two more textures per creature.
- **Seat in Python.** It would duplicate the C# seating the tests pin; the bake reuses the C# code instead.

## Consequences

- The project gains its first custom effect and a committed shader binary (rebuilt on Windows).
- Combat presentation reads no texture back from the GPU.
- Visual differences from `39b59aaa` are intended to be invisible at play size; any found are listed in the
  production evidence (`production/qa/evidence/brand-production/`).

## Validation

Parity stills against `39b59aaa` (Void Reaper, Crystal Lich, black whelp), production stills (Pale Wisp, depths
1/2/3), true-speed clips (apply/deepen, SPRAWL, transfer, mixed combat), and a draw / allocation table from the
`mark-draw` trace. Game and Core suites green.
