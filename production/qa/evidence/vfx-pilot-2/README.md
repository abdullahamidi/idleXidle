# VFX pilot, second regeneration — `fx_seeker_strike`

**Date** 2026-09-23 · **Branch** `fix/vfx-fade` (not merged) · **Renderer** unchanged (ADR-009, approved)
**Scope** ONE strip. The other 66 were not touched. **Verdict** production-worthy candidate installed; awaiting
the owner's approval.

## What was generated — 10 generations (balance 4,428 → 4,418)

Both clips animate the same approved shard shape (pixen `83b78398`, from the first pilot, so no new
shape was needed), at 192² × 8 frames, 5 generations each.

| Job | How | Raw temporal shape | Result |
|---|---|---|---|
| `1a76308a` | `last_frame_url` pinned to a splinter frame of the same shape family | shards → central star (frames 3–5) → **the full burst again** (6–7) | rejected raw, never post-processed |
| `3e04a7c8` | open-ended, single-event prompt (below) | shards → splinters → thinning → sparse remnant; lit pixels fall every frame | **installed** |

The prompt that worked (recorded in `spec.json` → `effects.characters.seeker.strike`):
*"one single impact that happens once: the white shards shoot outward from the centre and keep flying
outward, getting thinner and splitting into small splinters. the motion only goes outward, nothing moves
back toward the middle, no second burst, no new shapes appear, it does not repeat or restart. the
splinters stay clearly visible in every frame including the last one"*.

Pinning the ending did NOT help a burst. The interpolation routed back through the start, so pinning
stays right only for held loops.

## One pipeline calibration: soften radius = one SOURCE pixel

Through the unchanged post-pass (soften radius 5), the good animation **failed** the new liveness rule. Its
one-bit assembly kept 1,008 bright cells at combat size; after the post-pass it kept 50. Radius 5 is two
source pixels at the 192 canvas, and it erased every one-pixel splinter (see 02). Soften exists to remove the
nearest-neighbour stair-step, so its unit is the source pixel. `fxclips.py` now passes ~1.1 source pixels
(3 at 192, 2 at 256, 4 at 128), read from the frames it just fetched. The stages are unchanged
(assemble → whiten → glow → soften → feather), and no √alpha anywhere. Refiled through the tool,
SHA-1 `d6a094bf…`.

## Metrics

| | OLD (1-bit) | E2 (rejected pilot) | **NEW** |
|---|---|---|---|
| border alpha (EDGE, ≤ 24) | 255 | 0 | **0** |
| partial alpha, sampled (SOFT, ≥ 2 %) | 0.00 % | 22.73 % | **10.91 %** |
| distinct alpha values / partial share of lit | 2 / 0 % | 256 / 99.9 % | **256 / 99.98 %** |
| minimum margin to frame edge | 0 px | 11 px | **10 px** |
| LIVE: weakest impact-window frame (≥ 150 bright cells) | 3,205 | 300 | **294** |
| ONE PEAK (diagnostic) | — | **RE-FORM at frame 3: +84 core cells, 2.6×** | **none** |
| placement (swarm / boss) | frame 135 / 287 | 141 / 301 | **141 / 300**, ratio 0.276 / 0.586 |

Per-frame, from `tools/fx_energy.py`. Energy and core are fade-weighted (the renderer's tail starts at
433 ms). Chart: `07_energy_curves.png`.

| frame (ms) | E2 energy | E2 bright | E2 core | NEW energy | NEW bright | NEW core |
|---|---|---|---|---|---|---|
| 0 (0) | 0.0617 | 636 | 23 | 0.0519 | 840 | 71 |
| 1 (83) | 0.0441 | 399 | 24 | 0.0288 | 294 | 79 |
| 2 (167) | 0.0309 | 300 | 47 | 0.0294 | 446 | 112 |
| 3 (250) | 0.0221 | 156 | **107** | 0.0191 | 291 | 51 |
| 4 (333) | 0.0153 | 51 | 50 | 0.0160 | 258 | 114 |
| 5 (417) | 0.0083 | 53 | 22 | 0.0052 | 164 | 27 |
| 6 (500) | 0.0004 | 176 | 3 | 0.0001 | 60 | 0 |
| 7 (583) | 0.0000 | 492 | 0 | 0.0000 | 21 | 0 |

**Why the new candidate is better temporally.** The two energy curves are almost the same. The difference is
the CENTRE. E2's centre is nearly empty after the burst leaves (23 cells), then jumps to 107 at 250 ms:
a new object appears where nothing was, which the player reads as a second hit. The new strip's centre is
lit from the first frame and only wobbles (112 → 51 → 114) as splinters cross it, 0.89× of its own
peak core. Every game frame from 167 to 417 ms was inspected: continuous dissipation, no pulse, no pop.

## In HUNT, at real playback speed (03–06)

The films are real game frames, one 16.7 ms step per picture (the rig fix of 83ad5db). Each pair is aligned
on its own hit. The GIFs play at exact game speed.

| Check | Enemy survives (17200 ms) | Killing blow (8200 ms) |
|---|---|---|
| One hit | yes | yes |
| One expansion | yes: a full, even ring of shards at the first frame | yes |
| Continuous dissipation | yes: splinters thin and scatter | yes: until the death plume |
| Visible second hit | **no** (E2: a compact red starburst at +333–400 ms) | **no** (E2: at +266–333 ms) |
| Rectangle | none | none |
| Brightness | readable at 135 px, no bloom | readable |
| Frame popping | none at the 83 ms strip-frame steps | none |

Lookup at UI 100 / 125 / 150 %: `cast.strike key=fx_seeker_strike_strip8_512`, ratio 0.276, not the
fallback.

Overlap note: the separate pale `fx_weakhit` puff plays on the same hit and covers the centre for about the
first 300 ms. It is present in every capture, both old and new, and was not changed.

## The gate

- **LIVE is a hard rule** in `tools/check_fx_edges.py`. It is robust: every production strip passes
  (weakest `fx_magpie_trap` 192, then `fx_mark` 290 and this strip at 294, the third-weakest in the library), the invisible needle candidate fails (50), and so does this very animation at the old
  soften radius (50). Library count is unchanged: 60 of 67 fail, all on SOFT, as before.
- **ONE PEAK stays a diagnostic.** It prints a warning on impact forms only. With E2 installed, E2 was the
  only impact strip it warned on; with the new strip installed it warns on none. Run on every form
  (`py tools/fx_energy.py`), it also flags `fx_anvil_mark`, whose brackets close into an X on purpose (a
  mark's lock-on), and `fx_quiver_mark`, whose ring blinks out for one frame (energy climbs back 44 %).
  Three judged cases are too few for a CI rule, and a PNG cannot say which motion grammar it was drawn to.
- Still NOT wired into `check_all.sh`.

## Gates

`-warnaserror` 0/0 · Core 1,850 · Game 712 · Integration 2, 0 failed · `tools/check_all.sh` all green.

## Files

`01` the new strip: raw PixelLab frames, assembled, final, with E2 for reference. `02` rejected this
round. `03`/`04` HUNT, E2 vs new, surviving target and killing blow (2× zoom). `05`/`06` the same as GIFs:
`_realspeed` at exact play speed, `_slow` at about ¼ speed. `07` per-frame energy / bright / core curves.
