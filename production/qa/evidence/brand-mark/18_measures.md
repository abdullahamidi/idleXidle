# BRAND in numbers

## The light the mark ADDS, exactly (the same frames of the same seeded fight, with and without the mark)

Two takes filmed every frame at 60 Hz align frame for frame; the only difference is the mark (without it BRAND
falls back to the old reticle behind the hunter, outside the creature row measured here). Pixels of the creature
row brighter than luma 170 / 210 WITH the mark and at least 12 brighter than without it.

| moment | window (ms) | frames | peak px > 170 | peak px > 210 | mean px > 170 | peak px changed (any) | other light in the window (the take's trace) |
|---|---|---|---|---|---|---|---|
| APPLY: the gather and the cut ON the first tick | 1750-2280 | 21 | 765 | 0 | 46 | 2337 | hit flash 2200 (slot 0) |
| IDLE after the apply (base stage) | 2300-2790 | 30 | 0 | 0 | 0 | 2179 | none |
| IDLE before ETCH's tick (stage deep1) | 3580-3940 | 12 | 250 | 197 | 40 | 2353 | hit flash 3700 (slot 0) |
| DEEPEN: ETCH's tick at 4000 (120 -> 170 %) | 3950-4500 | 34 | 244 | 45 | 31 | 2615 | JAWS snap 4317 |
| IDLE after it (stage deep2) | 4300-4800 | 30 | 244 | 45 | 35 | 2489 | JAWS snap 4317 |
| CLEAN DEEPEN at 4000 (120 -> 170 %): the tick, the settle, the chisel | 3990-4700 | 43 | 135 | 0 | 25 | 2609 | none |
| the 5200 fall's white death smoke drifting over the NEW host (not the mark's light) | 5800-6140 | 11 | 1329 | 1124 | 811 | 2606 | fall 5217 (its death smoke) |
| CLEAN DEEPEN at 6000 (170 -> 220 %): the settle, the chisel | 6150-6700 | 34 | 63 | 0 | 14 | 2604 | none |
| SPRAWL + WINNOW beside JAWS: the row deepening at 4000 (a quiet tick) | 3990-4700 | 43 | 118 | 15 | 11 | 8504 | JAWS snap 4317 |
| SPRAWL + WINNOW at FULL strength (WILT in JAWS's slot): the row's ripple at 4000 | 3990-4700 | 39 | 347 | 0 | 137 | 8504 | none |

## Each ETCH step marks more of the body, in game (the clean ETCH take, settled frames of each stage)

| stage | frames | violet lit px (median) | the step |
|---|---|---|---|
| deep1 (120 %) | 40 | 1170 |  |
| deep2 (170 %) | 12 | 1395 | +19 % |
| deep3 (220 %) | 15 | 1485 | +6 % |
| deep4 (240 %) | 20 | 1575 | +6 % |

Settled frames only: none within 0.7 s of a tick or 1 s of a fall, none with a hit flash in the box.

The mark's own beat is bounded by construction (the table below gives the bound). A count above 210 is another
layer's light landing on the lit coil; the last column names every such layer the take's trace records in the
window: JAWS's snap (its reaction cue), the game's hit flash (a hit whitens body and brand together) and a fall
(whose white death smoke drifts over the next host ~450-750 ms later). A row beside JAWS is never the mark's alone.

## By construction (the atlas x the recipe's tints, over a near-black body, at a whelp's scale 1)

| stage | depth | cut px | brightest at rest (luma) | px > 170 at rest | lit core at rest (px > 100) | smoke halo px | the apply beat (whole cut-line px) | the deepen chisel (px per step) | beat luma full / quiet |
|---|---|---|---|---|---|---|---|---|---|
| spread | < 50 % | 864 | 53 | 0 | 0 | 2070 | 720 | 0 / 0 / 0 | 186 / 119 |
| base | 50-99 % | 1224 | 116 | 0 | 288 | 2466 | 765 | 261 / 252 / 252 | 198 / 157 |
| deep1 | 100-149 % | 1503 | 116 | 0 | 288 | 2763 | 765 | 261 / 252 / 252 | 198 / 157 |
| deep2 | 150-199 % | 1728 | 116 | 0 | 288 | 2853 | 765 | 144 / 279 / 414 | 198 / 157 |
| deep3 | 200-239 % | 1818 | 116 | 0 | 288 | 2979 | 774 | 63 / 117 / 171 | 198 / 157 |
| deep4 | >= 240 % | 1908 | 116 | 0 | 288 | 3006 | 918 | 63 / 72 / 153 | 198 / 157 |

At rest nothing passes luma 170 at any depth, and only a travelling third of the ring is lit (the inner hook never);
the whole coil lights only on the apply beat. Deeper stages cut MORE of the body (a wider groove, the ring's wall cut
into the hollow, then the thin hook cut further along the spiral, inward), never bigger (deep1 to deep4 share one
outline box) and never more light: the brightest texel is the same band. The deepen beat is a chisel: the new cut
only, in three stretches travelling round the ring (deep3 / deep4: setting in at the old hook's end and running on
inward). The beat luma above is an upper bound (the hot tint at full alpha over the rest band); the render peaks
near 183.

## The mark's own light across the long takes (violet-lit pixels; frames where JAWS or PRESS draw left out)

An upper bound: the whelps' pale pink eyes pass the same colour test (luma ~235), so the idle rows below count them
too; the exact table above is the mark alone.

The accepted references' own peaks (ADR-011): SPRAY 2761 / 805, HARD HANDS 1382 / 612, JAWS 6957 / 5327, PRESS
749-867 above 170 (~27 above 210 at its contact).

| take | moment | window (ms) | peak px > 170 | peak px > 210 | px > 90 (the lit coil, peak) | brightest luma |
|---|---|---|---|---|---|---|
| apply | APPLY: the cut on the first tick | 1950-2250 | 765 | 0 | 874 | 183 |
| long | IDLE (the settled coil) | 2400-3900 | 433 | 192 | 1422 | 233 |
| long | IDLE | 4300-5900 | 57 | 37 | 948 | 240 |
| long | MIGRATE (the front falls at 6700) | 6650-7300 | 0 | 0 | 169 | 116 |
| long | MIGRATE (the next falls at 8200) | 8150-8800 | 183 | 49 | 749 | 240 |
| etch | DEEPEN (ETCH ticks 4000 / 6000 / 8000) | 3950-8300 | 624 | 436 | 825 | 239 |
| sprawl | SPREAD (SPRAWL's first tick) | 1950-2600 | 0 | 0 | 728 | 155 |
| sprawl | SPRAWL idle (four coils) | 2700-3900 | 337 | 216 | 720 | 234 |

BEFORE, for comparison: the reticle held behind the hunter (518 px frame, white art tinted by the slot's Source,
flaring once a second) -- see 10_before_after.mp4.

## The trace (mark-draw, every frame the mark draws)

- **long**: 652 frames drawn; sprites per frame 3-11; bodies carrying it 0-1; stages drawn [1]; 652 of 652 frames allocate 0 bytes
  - the body point on one host moves 1 px (median) between drawn frames; 65 of 649 steps exceed 12 px; scale 1.00
- **etch**: 547 frames drawn; sprites per frame 3-11; bodies carrying it 0-1; stages drawn [2, 3, 4, 5]; 547 of 547 frames allocate 0 bytes
  - the body point on one host moves 2 px (median) between drawn frames; 59 of 529 steps exceed 12 px; scale 1.00
- **etch_clean**: 565 frames drawn; sprites per frame 3-11; bodies carrying it 0-1; stages drawn [2, 3, 4, 5]; 565 of 565 frames allocate 0 bytes
  - the body point on one host moves 1 px (median) between drawn frames; 59 of 547 steps exceed 12 px; scale 1.00
- **sprawl**: 665 frames drawn; sprites per frame 3-19; bodies carrying it 1-4; stages drawn [0]; 665 of 665 frames allocate 0 bytes
  - the body point on one host moves 1 px (median) between drawn frames; 68 of 662 steps exceed 12 px; scale 1.00
- **sprawl_winnow**: 664 frames drawn; sprites per frame 3-19; bodies carrying it 1-4; stages drawn [0, 1]; 664 of 664 frames allocate 0 bytes
  - the body point on one host moves 1 px (median) between drawn frames; 74 of 660 steps exceed 12 px; scale 1.00
- **sprawl_winnow_clean**: 649 frames drawn; sprites per frame 3-19; bodies carrying it 1-4; stages drawn [0, 1]; 649 of 649 frames allocate 0 bytes
  - the body point on one host moves 1 px (median) between drawn frames; 65 of 645 steps exceed 12 px; scale 1.00
- **press**: 660 frames drawn; sprites per frame 3-11; bodies carrying it 0-1; stages drawn [1]; 660 of 660 frames allocate 0 bytes
  - the body point on one host moves 1 px (median) between drawn frames; 79 of 656 steps exceed 12 px; scale 1.00
- **fast**: 823 frames drawn; sprites per frame 3-11; bodies carrying it 0-1; stages drawn [1]; 823 of 823 frames allocate 0 bytes
  - the body point on one host moves 1 px (median) between drawn frames; 87 of 802 steps exceed 12 px; scale 1.00
- **light**: 665 frames drawn; sprites per frame 3-11; bodies carrying it 0-1; stages drawn [1]; 665 of 665 frames allocate 0 bytes
  - the body point on one host moves 1 px (median) between drawn frames; 2 of 661 steps exceed 12 px; scale 1.00
- **light_etch**: 567 frames drawn; sprites per frame 3-11; bodies carrying it 0-1; stages drawn [2, 3, 4, 5]; 567 of 567 frames allocate 0 bytes
  - the body point on one host moves 1 px (median) between drawn frames; 1 of 549 steps exceed 12 px; scale 1.00
- **order**: 302 frames drawn; sprites per frame 3-10; bodies carrying it 0-1; stages drawn [1]; 302 of 302 frames allocate 0 bytes
  - the body point on one host moves 2 px (median) between drawn frames; 40 of 300 steps exceed 12 px; scale 1.00
- **fam_machine_armoured**: 302 frames drawn; sprites per frame 3-11; bodies carrying it 0-1; stages drawn [2, 3, 4]; 302 of 302 frames allocate 0 bytes
  - the body point on one host moves 2 px (median) between drawn frames; 32 of 299 steps exceed 12 px; scale 1.67
- **fam_shadow_caster**: 288 frames drawn; sprites per frame 3-11; bodies carrying it 0-1; stages drawn [2, 3, 4]; 288 of 288 frames allocate 0 bytes
  - the body point on one host moves 1 px (median) between drawn frames; 0 of 286 steps exceed 12 px; scale 1.67
- **fam_mind_swarm**: 293 frames drawn; sprites per frame 3-11; bodies carrying it 0-1; stages drawn [2, 3, 4]; 293 of 293 frames allocate 0 bytes
  - the body point on one host moves 1 px (median) between drawn frames; 15 of 291 steps exceed 12 px; scale 1.00
- **fam_nature_bruiser**: 289 frames drawn; sprites per frame 3-11; bodies carrying it 0-1; stages drawn [2, 3, 4]; 289 of 289 frames allocate 0 bytes
  - the body point on one host moves 2 px (median) between drawn frames; 29 of 287 steps exceed 12 px; scale 2.33
