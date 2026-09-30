# BRAND, Concept A glow pass, in numbers

Every row pairs a frame with the SAME frame of the same seeded fight with the mark drawn off (the `_off` twins).
`violet px > 170 / 210`: VIOLET pixels the curse pushes above that luma, its own light. The raw count (the measure every
ADR-011 reference was held to) is kept in its own column: it also counts bright edges the curse's shudder moves (a boss's
scythe) and white death smoke lifted by the glow under it.
`glow px`: pixels in the host's body box the curse brightens by more than 30 luma (a violet vein on a black body is
rarely above luma 170, so this is the dark-host readability measure). `coverage`: body-box pixels it changes at all
(|luma| > 12, darkening included) as a share of the box.

| moment | window (ms) | frames | peak violet px > 170 | peak violet px > 210 | mean glow px | peak glow px | mean coverage | raw peak px > 170 / 210 (incl. moved edges, smoke) | other light in the window |
|---|---|---|---|---|---|---|---|---|---|
| APPLY (the growth and its front) | 1750-2400 | 40 | 4 | 0 | 748 | 2137 | 4.9% | 89 / 78 | hit flash 2200 (slot 0) |
| IDLE, depth 1 (after the apply settles) | 2500-2800 | 19 | 0 | 0 | 494 | 734 | 2.0% | 10 / 0 | none |
| DEEPEN 1 -> 2 (the 4000 tick) | 3990-4700 | 43 | 0 | 0 | 955 | 2245 | 3.9% | 79 / 71 | none |
| IDLE, depth 2 (settled) | 4700-4850 | 4 | 0 | 0 | 1356 | 1365 | 5.9% | 18 / 0 | none |
| IDLE, depth 2 (before the 6000 tick) | 5600-5990 | 20 | 777 | 300 | 508 | 1217 | 4.1% | 1184 / 635 | fall 5217 (its death smoke) |
| DEEPEN 2 -> 3 (the 6000 tick) | 5990-6700 | 43 | 960 | 349 | 1223 | 2467 | 4.8% | 1243 / 556 | none |
| depth 3, then its host FALLS at 6717 (the curse brightens and collapses) | 6700-6850 | 10 | 535 | 197 | 624 | 1540 | 1.9% | 672 / 232 | hit flash 6717 (slot 1), fall 6717 (its death smoke) |

### One host that never falls: the Void Reaper (a dark boss), ETCH

| moment | window (ms) | frames | peak violet px > 170 | peak violet px > 210 | mean glow px | peak glow px | mean coverage | raw peak px > 170 / 210 (incl. moved edges, smoke) | other light in the window |
|---|---|---|---|---|---|---|---|---|---|
| APPLY | 1750-2450 | 21 | 6 | 0 | 5878 | 16630 | 10.0% | 4082 / 2910 | hit flash 2200 (slot 0) |
| IDLE, depth 1 | 2500-3950 | 44 | 0 | 0 | 621 | 3495 | 0.9% | 75 / 0 | hit flash 3700 (slot 0) |
| DEEPEN 1 -> 2 | 3990-4800 | 25 | 0 | 0 | 5522 | 18851 | 10.4% | 2676 / 2054 | none |
| IDLE, depth 2 | 4850-5950 | 33 | 4 | 0 | 1067 | 4011 | 6.6% | 1279 / 765 | hit flash 5200 (slot 0), hit flash 5200 (slot 1), hit flash 5200 (slot 2), hit flash 5200 (slot 3) |
| DEEPEN 2 -> 3 | 5990-6800 | 25 | 80 | 0 | 5575 | 18474 | 12.1% | 3035 / 2070 | hit flash 6717 (slot 0) |
| IDLE, depth 3 | 6850-7900 | 32 | 4 | 1 | 2146 | 4454 | 8.6% | 1153 / 650 | none |

The accepted references' peaks: SPRAY 2761 / 805, HARD HANDS 1382 / 612, JAWS 6957 / 5327, PRESS 749-867 (px above luma 170 / 210, ADR-011).

## Allocation and draw counts (mark-draw trace, every frame the curse draws)

- **long**: 653 frames drawn; sprites per frame 1-17; depths [0, 1]; 650 of 653 frames allocate 0 bytes
- **etch**: 565 frames drawn; sprites per frame 1-20; depths [0, 1, 2, 3]; 561 of 565 frames allocate 0 bytes
- **etch_clean**: 565 frames drawn; sprites per frame 1-20; depths [0, 1, 2, 3]; 561 of 565 frames allocate 0 bytes
- **sprawl**: 652 frames drawn; sprites per frame 5-44; depths [1]; 650 of 652 frames allocate 0 bytes
- **sprawl_winnow_clean**: 654 frames drawn; sprites per frame 5-44; depths [1]; 652 of 654 frames allocate 0 bytes
- **press**: 648 frames drawn; sprites per frame 1-17; depths [0, 1]; 644 of 648 frames allocate 0 bytes
- **fast**: 764 frames drawn; sprites per frame 1-17; depths [0, 1]; 757 of 764 frames allocate 0 bytes
- **light**: 651 frames drawn; sprites per frame 1-17; depths [0, 1]; 648 of 651 frames allocate 0 bytes
- **light_etch**: 565 frames drawn; sprites per frame 1-20; depths [0, 1, 2, 3]; 561 of 565 frames allocate 0 bytes
- **fam_nature_bruiser**: 565 frames drawn; sprites per frame 1-20; depths [0, 1, 2, 3]; 561 of 565 frames allocate 0 bytes
- **fam_shadow_caster**: 565 frames drawn; sprites per frame 1-20; depths [0, 1, 2, 3]; 561 of 565 frames allocate 0 bytes
- **boss_void_reaper**: 561 frames drawn; sprites per frame 5-14; depths [1, 2, 3]; 560 of 561 frames allocate 0 bytes
- **boss_forge_colossus**: 553 frames drawn; sprites per frame 5-14; depths [1, 2, 3]; 552 of 553 frames allocate 0 bytes
- **boss_thorn_regent**: 565 frames drawn; sprites per frame 5-14; depths [1, 2, 3]; 564 of 565 frames allocate 0 bytes
- **boss_lumen_angel**: 561 frames drawn; sprites per frame 5-14; depths [1, 2, 3]; 560 of 561 frames allocate 0 bytes
- **boss_crystal_lich**: 543 frames drawn; sprites per frame 5-14; depths [1, 2, 3]; 542 of 543 frames allocate 0 bytes
- **boss_spirit_matron**: 565 frames drawn; sprites per frame 5-14; depths [1, 2, 3]; 564 of 565 frames allocate 0 bytes
