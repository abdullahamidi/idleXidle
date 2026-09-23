# VFX regeneration pilot — `fx_seeker_strike`

**Date** 2026-09-23 · **Branch** `fix/vfx-fade` · **Type** Visual/Feel (advisory) + pipeline proof
**Scope** ONE strip. The other 66 were not touched and are not approved.
**Verdict** Pipeline mechanically PROVEN. Candidate visually **NOT ACCEPTABLE AS IS**: the rectangle is
gone, but under the current renderer the Seeker's signature has lost most of its light. That comes from
the VFX blend state, not from the asset, and it has to be decided before any batch.

---

## What was run (the existing path, nothing parallel)

PixelLab MCP (subscription, Tier 2): `create_image_pixen(192², no_background, view=side, direction=west)`
→ `animate_image(first_frame_url=<pixen download>, frame_count=8)` →
`python tools/asset-pipeline/v2/fxclips.py seeker strike=bc6dcbd0-43d5-4b84-b4d3-bde8f715380e`
(fetch → `clip.py job --effect` → whiten → glow → soften → feather). The installed file is
**byte-identical** (SHA-1 `0cf83738…`) to a step-by-step run of the same four stages in scratch, so the
tool, not a hand edit, produced it. No runtime clipping or scissoring was added.

Identity kept: commit 8596685's "a puncture burst for the knife stab". The shape prompt was
*"a solid white burst of about twelve thick sharp shards flying outward from one point in the centre,
each shard a long narrow white triangle pointing outward…"*. The animation was *"the white shards split
into short sharp splinters that drift slowly outward from the centre…"*.

## Measurements

| | OLD (production) | NEW (installed) |
|---|---|---|
| `check_fx_edges` EDGE: worst border alpha | **255** | **0** |
| `check_fx_edges` SOFT: partial alpha, strided sample | **0.00 %** | **22.73 %** |
| border ring with alpha > 8 | 12.41 % | 0.00 % |
| border ring with alpha > 200 | 12.41 % | 0.00 % |
| distinct alpha values | 2 | 256 |
| partial share of lit pixels | 0.0 % | 99.91 % |
| lit share of the strip | 21.32 % | 22.69 % |
| minimum margin to frame edge (px of 512) | 0 in every frame | 11–109 |
| mean alpha of lit pixels | 255 | 41–64 |
| `rhart.py gate --effect` | PASS (the old gate never saw the defect) | PASS |

Library-wide, `check_fx_edges.py` (still run by hand, **not** in `check_all.sh`) goes from 61 to 60 of 67
failing. Only this strip changed.

## Runtime proof

- **Lookup.** The game's own `RH_VFX_DUMP` at the signature cast (8200 ms):
  `cast.strike key=fx_seeker_strike_strip8_512 subj=Creature/0 frame=1175,685,141,141 content=1178,689,136,135 ratio=0.276`.
  HuntScreen draws the Seeker's strip, not the `fx_strike` fallback. It resolves the same way at UI 100,
  125 and 150 %.
- **Geometry.** 4096×512 RGBA, 8 square frames, same path and key. Source rect is unchanged
  (`CurrentFrame × 512, 0, 512, 512`). The destination is content-driven: the visible size held at
  135→136 px on a swarm creature and 287→289 px on a boss. The frame grew 135→141 px and 287→301 px
  because the content box is 0.96 of the frame now, not 1.00. The ratio went 0.263→0.276 and 0.560→0.587,
  both still `Under`. Every other line of the `RH_VFX_BUDGET` ledger is byte-identical.
- **Timing.** No code touched. `CastStrike` is still 12 fps × 8 frames = 0.667 s with the 35 % cubed
  tail fade.
- **Gates.** `-warnaserror` 0/0; Core 1,850 · Game 707 · Integration 2 passed, 0 failed;
  `tools/check_all.sh` all green.

## Visual review (02_ingame_before_after.png, 03_runtime_simulation.png)

| Question | Answer |
|---|---|
| Rectangle impression gone? | **Yes.** The old strip reads in game as a square block of red lines; the new one is round. |
| Rays / shards fade naturally? | **Yes.** The tips taper into the feather band, and nothing is cut. |
| Readable at combat size? | **No, not as installed.** In game it is three or four faint red streaks at 135 px. With the blend corrected it reads as a soft shard burst. |
| Softening muddy? | On thin art, yes (the B2 needle candidate turned to haze). On the bold shards it is acceptable. |
| Glow too bright? | The opposite: far too dim (see the cause below). |
| Still the Seeker / the game's language? | Yes: white shards, Body tint, the same burst-then-splinter arc as the old strip. |
| Motion flaw | Frames 1–4 are the puncture. At frame 5 it jumps to a small cluster and re-bursts (see *Prompt drift*). |

## THE CAUSE OF THE DIMNESS — the blend, which one-bit art had been hiding

`VfxPlayer` draws with `BlendState.Additive` (`SourceAlpha, One`), and `AssetLibrary` **premultiplies**
every texture at load. So each texel adds `rgb·α·α`, and light goes as **α²**. The `glow` step had
already set α := α × luminance, which makes it roughly α²·lum³.

With one-bit art α² = α, so this never showed. The new post-pass exists to produce partial alpha, and
the renderer squares every partial value it makes: a softened pixel at α = 0.5 gives 25 % light, and a
blurred needle at α = 0.2 gives 4 %. The pipeline change in `0bdb9fe` was written without accounting
for this.

The bottom row of 02 is an **experiment that was not installed**: the same strip with α' = √α baked in,
which cancels the square exactly for this path. It shows what the fix buys.

## Prompt drift — yes, again, in four ways

1. **Containment.** pixen ignores "small / halfway to the edge / nothing touches the edges" whenever
   the subject is BOLD: all 5 bold prompts filled the canvas to 0–11 px of the edge. Of the 7 shapes,
   only the thin, sparse needle burst (B) stayed contained, and thin art cannot be read at a 0.28× draw.
2. **Literalism.** "a bright round white core" became a sun disc. "Nothing in the middle" became an
   opaque transparency **checkerboard** (whiten would lift it to grey). "No disc" produced a black disc.
3. **animate_image does not end.** "Keep a clear visible remnant in every frame" still returned two
   empty final frames in one clip of five, and that clip also collapsed halfway. B2 and E2 collapse and
   re-burst in their second half, C1 grows back to full size, and E1 cuts to an unrelated X glyph. The
   model pulls an open-ended clip back toward a cycle.
4. **spec.json is stale.** `effects.items` still says "fades out completely by the last frame" and
   "at the right edge", both of which the contract forbids. The batch must not use them as written.

First-try rates here: 2 of 7 shapes usable (B, E), and **0 of 5 animations clean** (B2 and E2 usable
with a flaw).

## `check_fx_edges.py` thresholds

- **EDGE_MAX = 24 is right.** Under the α² blend a border alpha of 24 is 0.9 % light, which is invisible.
  It is not too strict.
- **SOFT_MIN = 2 % is too weak as a quality signal.** The thin B2 candidate, which was nearly invisible
  in the simulation, passes at 8.9 %. The rule proves "not one-bit". It cannot prove "still has a core".
  A missing rule is a **CORE** check, for example that every frame keeps some pixels at α ≥ 224, or a
  minimum share of the pre-soften light. E2's frames 4–6 peak at α 161–168 and would be flagged.
  Not implemented; this is a recommendation.

## Cost

**32 generations** (balance 4,460 → 4,428): 7 pixen shapes × 1 plus 5 animate_image × 5.
Every job id is in `04_all_generations.png`.

## Rig note (not a game bug)

`capture_seq.sh` counts **Draws**, and each full-canvas PNG save makes the next Draw late, so MonoGame's
fixed step runs several catch-up Updates. The saved frames are therefore spaced by save latency, not by
stride × 16.7 ms: effects and callouts that live about 0.6 s show on 2–3 saved frames. Its "x100ms"
label is a static default. That is why frames beyond the first two are evidenced by
`03_runtime_simulation.png` (bilinear minification without mips, the α² blend, the Body tint, over the
real arena crop), which reproduces the in-game first frames.
