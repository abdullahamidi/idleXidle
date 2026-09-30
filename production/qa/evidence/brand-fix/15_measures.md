# BRAND second pass, in numbers

## The art: dark first, light second

| mark | depth | cut px | lit px (> luma 200 in the art) | lit share | mean grey of the cut |
|---|---|---|---|---|---|
| first slice | base | 1224 | 288 | 24% | 186 |
| first slice | deep2 | 1728 | 288 | 17% | 177 |
| first slice | deep4 | 1908 | 288 | 15% | 177 |
| second pass | DEPTH 1 | 603 | 54 | 9% | 166 |
| second pass | DEPTH 2 | 963 | 135 | 14% | 137 |
| second pass | DEPTH 3 | 1251 | 99 | 8% | 136 |

The art's grey is multiplied by the runtime tints (the cut by the groove tint, so a grey of 255 draws ~(136, 108, 196)).
The first slice's rest cell was a translucent lit line; the new cut is a near-black incision with dark-violet walls and
one lit rim on its light-facing edge.

## The light the mark ADDS, exactly (the same frames with and without the mark)

| moment | window (ms) | frames | peak px > 170 | peak px > 210 | mean px > 170 | peak px changed (any) | other light in the window |
|---|---|---|---|---|---|---|---|
| APPLY: the ink gathers, the cut appears, its edge catches | 1750-2280 | 32 | 108 | 0 | 5 | 695 | hit flash 2200 (slot 0) |
| IDLE after the apply (DEPTH 1) | 2300-2790 | 30 | 0 | 0 | 0 | 683 | none |

The accepted references' peaks (ADR-011): SPRAY 2761 / 805, HARD HANDS 1382 / 612, JAWS 6957 / 5327, PRESS 749-867.
The 'peak px changed (any)' column counts DARKENED pixels too: the new mark's main work is darkening the body.

## The trace (mark-draw, every frame the mark draws)

- **long**: 648 frames drawn; sprites per frame 1-4; depths drawn [1]; 648 of 648 frames allocate 0 bytes
- **etch**: 579 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 579 of 579 frames allocate 0 bytes
- **etch_clean**: 578 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 578 of 578 frames allocate 0 bytes
- **sprawl**: 648 frames drawn; sprites per frame 3-28; depths drawn [0]; 648 of 648 frames allocate 0 bytes
- **sprawl_winnow_clean**: 654 frames drawn; sprites per frame 3-28; depths drawn [0, 1]; 654 of 654 frames allocate 0 bytes
- **press**: 648 frames drawn; sprites per frame 1-4; depths drawn [1]; 648 of 648 frames allocate 0 bytes
- **fast**: 776 frames drawn; sprites per frame 1-4; depths drawn [1]; 776 of 776 frames allocate 0 bytes
- **light**: 646 frames drawn; sprites per frame 1-4; depths drawn [1]; 646 of 646 frames allocate 0 bytes
- **light_etch**: 579 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 579 of 579 frames allocate 0 bytes
- **fam_machine_armoured**: 578 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 578 of 578 frames allocate 0 bytes
- **fam_shadow_caster**: 579 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 579 of 579 frames allocate 0 bytes
- **fam_mind_swarm**: 578 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 578 of 578 frames allocate 0 bytes
- **fam_nature_bruiser**: 579 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 579 of 579 frames allocate 0 bytes
- **fam_body_armoured**: 579 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 579 of 579 frames allocate 0 bytes
- **fam_spirit_caster**: 579 frames drawn; sprites per frame 1-4; depths drawn [1, 2, 3]; 579 of 579 frames allocate 0 bytes
- **boss_thorn_regent**: 579 frames drawn; sprites per frame 3-4; depths drawn [1, 2, 3]; 579 of 579 frames allocate 0 bytes
- **boss_forge_colossus**: 579 frames drawn; sprites per frame 3-4; depths drawn [1, 2, 3]; 579 of 579 frames allocate 0 bytes
- **boss_void_reaper**: 579 frames drawn; sprites per frame 3-4; depths drawn [1, 2, 3]; 579 of 579 frames allocate 0 bytes
- **boss_spirit_matron**: 579 frames drawn; sprites per frame 3-4; depths drawn [1, 2, 3]; 579 of 579 frames allocate 0 bytes
- **boss_crystal_lich**: 579 frames drawn; sprites per frame 3-4; depths drawn [1, 2, 3]; 579 of 579 frames allocate 0 bytes
- **boss_lumen_angel**: 579 frames drawn; sprites per frame 3-4; depths drawn [1, 2, 3]; 579 of 579 frames allocate 0 bytes
