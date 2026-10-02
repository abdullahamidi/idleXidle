# BRAND curse: production cost, from the `mark-draw` trace

**Runs.** Each take is a trace-only run of the seeded fight from `tools/asset-pipeline/films_brand.sh`, with the same env,
`RH_PRESENT_TRACE=1` and 2 saved frames (a stride of 660; the frames were deleted). The script is
`build/tmp/brandprod/perf.sh <takes>`, the parser is `perf_parse.py`, and the logs are in `perf/<take>.log`.

**Machine and build.** The owner's Windows PC, DesktopGL, Debug build (as `capture_seq.sh` builds it), 60 Hz fixed step.

**Fields.**
- `ticks` are Stopwatch ticks at 10 MHz (100 ns) spent in the curse's own draw code on that frame (`DrawOn`, `DrawLeaving`,
  `Convulse`). They include the arena flush that each curse pass forces.
- `flush=` is new in stage 4. It is the part of `ticks` spent in that forced flush, which is the arena's own work done early.
- `draws=` and `batches=` are the curse's draw calls and batch boundaries.
- `alloc=` is the bytes of managed allocation around the curse's draw calls.

| take (what it holds) | rows | max cursed bodies | curse draw calls / frame (mean / max) | batch boundaries / frame (mean / max) | CPU µs / frame (mean / p95 / max) | max excluding the first cursed frame (µs) | arena-flush share (mean µs) | alloc, steady state | alloc at apply / deepen / SPRAWL / transfer / death |
|---|---|---|---|---|---|---|---|---|---|
| `long`: depth 1, apply, 4 falls, transfers, JAWS | 645 | 1 | 1.75 / 4 | 2.05 / 4 | 31.4 / 39.8 / 2140 | 123 | 5.3 | **0** | 1128 (trace) / - / - / 1128 (trace) / 0 |
| `etch_clean`: depth 1 -> 3, WILT, 1 fall | 631 | 1 | 2.02 / 4 | 2.13 / 4 | 32.3 / 46.0 / 2027 | 188 | 3.7 | **0** (one 504 B row, see below) | 1128 (trace) / 0 / - / 1128 (trace) / 0 |
| `sprawl`: SPRAWL, 4 bodies cursed | 648 | 4 | 5.58 / 8 | 7.04 / 8 | 67.8 / 81.6 / 2762 | 191 | 15.0 | **0** | 1128 (trace) / 0 / 1128 per hop (trace) / - / 0 |
| `press`: BRAND + PRESS on one body | 648 | 1 | 1.81 / 4 | 2.12 / 4 | 34.9 / 47.0 / 2791 | 100 | 7.3 | **0** | 1128 (trace) / 0 / - / 1128 (trace) / 0 |
| `fast`: BRAND + SPRAY / HARD HANDS, fast tempo | 809 | 1 | 1.86 / 4 | 2.01 / 4 | 28.5 / 37.9 / 1931 | 232 | 5.4 | **0** | 1128 (trace) / 0 / - / 1128 (trace) / 0 |
| `etch`: BRAND + JAWS, depth 1 -> 3 | 631 | 1 | 2.02 / 4 | 2.12 / 4 | 35.7 / 49.4 / 2445 | 121 | 5.6 | **0** | 1128 (trace) / 0 / - / 1128 (trace) / 0 |

**Reading the table.**

- **Draw calls.** Each cursed body costs 2 calls (the curse pass, and its wisps' run in the reopened arena batch) and
  2 batch boundaries. The max of 4 is a living host plus a dying one on the same frame. SPRAWL's 4 bodies give 8 / 8.
  The planning budget is 10-20 boundaries per frame, and SPRAWL stays inside it.
- **Allocation.** Every non-zero `alloc` row except one coincides with a `curse-seat` trace line (1128 B: the trace
  string, built once per creature per wave, only under `RH_PRESENT_TRACE`). With the trace off, steady state and every
  event moment allocate nothing. There is one unexplained 504 B row in `etch_clean`, at 9317 ms on the 4th host's first
  frames. It was seen once in 6 takes and never repeated.
- **First cursed frame.** The `max` column (1.9-2.8 ms) is the first frame a creature is cursed in the run (1783 ms,
  the gather). Of that, about 1.2 ms is the trace-only `curse-seat` string; with it disabled the frame measured 0.82 ms.
  - Before stage 4 the same frame cost **7.7-12 ms and allocated 47 KB**. That was the driver's shader-program link on the
    effect's first draw, plus the JIT of the whole composition path. The first bloom and the first fall added ~1 ms more each.
  - Both now happen at load: `BrandCurseEffect.Warm` draws one empty pass into a 4x4 target, and
    `CursePresentation.Rehearse` composes a made-up apply / deepen / fall once, device-free.
- **Steady state.** About 30-36 µs of CPU per frame for one cursed body and ~68 µs for four. Less than 6 % of that is the
  forced arena flush.

**Prototype (39b59aaa).** I did not re-run it. Its `mark-draw` has no `draws=`, `batches=` or `ticks=`. These figures are
counted from its code (map.md §5-§7):

| | prototype (counted) | production (measured) |
|---|---|---|
| batch boundaries per cursed body | 5-7 Begin/End pairs + 1 arena split (+1 fissure, +1 bloom) | 1 pair + 1 split |
| draw calls, 1 body, depth 1 | ~8-12 | 2 |
| draw calls, 1 body, depth 3 | ~23-37 (+ 1-8 `DrawUserPrimitives`) | 2 |
| SPRAWL row of 4-5 at depth 3 | ~30-45 boundaries, ~115-185 draws | 8 boundaries, 8 draws (4 bodies) |
| GPU read-back | whole-strip `GetData` (~2 M px) + 2 x 8 MB uploads on each new strip seen (first idle, first lunge, the death frame) | none (tested) |
| per-wave GPU objects | `AlphaTestEffect`, 96 `DepthStencilState`, `DualTextureEffect`, never disposed | none (effect + atlas once per device) |
| steady allocation | 0 B (`stackalloc`) | 0 B |
