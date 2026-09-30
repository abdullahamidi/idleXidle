# BRAND second pass, in numbers

## The art: dark first, light second

| mark | depth | cut px | lit px (> luma 200 in the art) | lit share | mean grey of the cut |
|---|---|---|---|---|---|
| first slice | base | 1224 | 288 | 24% | 186 |
| first slice | deep2 | 1728 | 288 | 17% | 177 |
| first slice | deep4 | 1908 | 288 | 15% | 177 |
| second pass | DEPTH 1 | 603 | 54 | 9% | 166 |
| second pass | DEPTH 2 | 963 | 135 | 14% | 137 |
| second pass | DEPTH 3 | 1422 | 135 | 9% | 131 |

The art's grey is multiplied by the runtime tints (the cut by the groove tint, so a grey of 255 draws ~(136, 108, 196)).
The first slice's rest cell was a translucent lit line; the new cut is a near-black incision with dark-violet walls and
one lit rim on its light-facing edge.

## The light the mark ADDS, exactly (the same frames with and without the mark)

| moment | window (ms) | frames | peak px > 170 | peak px > 210 | mean px > 170 | peak px changed (any) | other light in the window |
|---|---|---|---|---|---|---|---|
| APPLY: the ink gathers, the cut appears, its edge catches | 1750-2280 | 32 | 108 | 0 | 5 | 695 | hit flash 2200 (slot 0) |
| IDLE after the apply (DEPTH 1) | 2300-2790 | 30 | 0 | 0 | 0 | 683 | none |
| DEEPEN at 4000 (DEPTH 1 -> 2) | 3990-4700 | 43 | 63 | 0 | 5 | 997 | none |
| DEEPEN at 6000 (DEPTH 2 -> 3) | 6150-6700 | 34 | 72 | 0 | 7 | 1348 | none |
| SPRAWL + WINNOW, the row deepening | 3990-4700 | 39 | 63 | 0 | 18 | 2154 | none |

The accepted references' peaks (ADR-011): SPRAY 2761 / 805, HARD HANDS 1382 / 612, JAWS 6957 / 5327, PRESS 749-867.
The 'peak px changed (any)' column counts DARKENED pixels too: the new mark's main work is darkening the body.

## The trace (mark-draw, every frame the mark draws)

- **long**: 646 frames drawn; sprites per frame 1-4; depths drawn [1]; 646 of 646 frames allocate 0 bytes
- **etch**: 579 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 579 of 579 frames allocate 0 bytes
- **etch_clean**: 579 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 579 of 579 frames allocate 0 bytes
- **sprawl**: 647 frames drawn; sprites per frame 3-28; depths drawn [0]; 647 of 647 frames allocate 0 bytes
- **sprawl_winnow**: 648 frames drawn; sprites per frame 3-28; depths drawn [0, 1]; 648 of 648 frames allocate 0 bytes
- **sprawl_winnow_clean**: 650 frames drawn; sprites per frame 3-28; depths drawn [0, 1]; 650 of 650 frames allocate 0 bytes
- **press**: 649 frames drawn; sprites per frame 1-4; depths drawn [1]; 649 of 649 frames allocate 0 bytes
- **fast**: 775 frames drawn; sprites per frame 1-4; depths drawn [1]; 775 of 775 frames allocate 0 bytes
- **light**: 645 frames drawn; sprites per frame 1-4; depths drawn [1]; 645 of 645 frames allocate 0 bytes
- **light_etch**: 579 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 579 of 579 frames allocate 0 bytes
- **fam_machine_armoured**: 285 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 285 of 285 frames allocate 0 bytes
- **fam_shadow_caster**: 285 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 285 of 285 frames allocate 0 bytes
- **fam_mind_swarm**: 287 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 287 of 287 frames allocate 0 bytes
- **fam_nature_bruiser**: 286 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 286 of 286 frames allocate 0 bytes
