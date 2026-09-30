# BRAND's size on every host, measured

The front creature's visible body from the game's geometry dump (RH_SHOT_DUMP, one fight per host, tools/asset-pipeline/brand_scale_shots.sh); the mark's visible extent (cut and stain, alpha > 0.1) from the atlas's idle cell of each depth, at the scale `ScaleFor` snaps to. The whelp is the reference: 1.00 before and after.

## Now (MaxScale 1)

| host | visible w | visible h | scale before | scale | D1 w x h | D3 w x h | D3 w share | D3 h share | D1 h share |
|---|---|---|---|---|---|---|---|---|---|
| Marrow Wastes Armoured | 260 | 386 | 1.67 | 1.00 | 66 x 51 | 72 x 63 | 28% | 16% | 13% |
| Marrow Wastes Bruiser | 415 | 469 | 2.33 | 1.00 | 66 x 51 | 72 x 63 | 17% | 13% | 11% |
| Marrow Wastes Caster | 200 | 331 | 1.67 | 1.00 | 66 x 51 | 72 x 63 | 36% | 19% | 15% |
| Marrow Wastes Swarm | 386 | 241 | 1.00 | 1.00 | 66 x 51 | 72 x 63 | 19% | 26% | 21% |
| Cinderworks Armoured | 346 | 390 | 1.67 | 1.00 | 66 x 51 | 72 x 63 | 21% | 16% | 13% |
| Cinderworks Bruiser | 431 | 474 | 2.33 | 1.00 | 66 x 51 | 72 x 63 | 17% | 13% | 11% |
| Cinderworks Caster | 243 | 331 | 1.67 | 1.00 | 66 x 51 | 72 x 63 | 30% | 19% | 15% |
| Cinderworks Swarm | 239 | 246 | 1.00 | 1.00 | 66 x 51 | 72 x 63 | 30% | 26% | 21% |
| The Still Archive Armoured | 286 | 390 | 1.67 | 1.00 | 66 x 51 | 72 x 63 | 25% | 16% | 13% |
| The Still Archive Bruiser | 392 | 474 | 2.33 | 1.00 | 66 x 51 | 72 x 63 | 18% | 13% | 11% |
| The Still Archive Caster | 216 | 331 | 1.67 | 1.00 | 66 x 51 | 72 x 63 | 33% | 19% | 15% |
| The Still Archive Swarm | 272 | 244 | 1.00 | 1.00 | 66 x 51 | 72 x 63 | 26% | 26% | 21% |
| Verdant Hollow Armoured | 330 | 390 | 1.67 | 1.00 | 66 x 51 | 72 x 63 | 22% | 16% | 13% |
| Verdant Hollow Bruiser | 417 | 474 | 2.33 | 1.00 | 66 x 51 | 72 x 63 | 17% | 13% | 11% |
| Verdant Hollow Caster | 201 | 331 | 1.67 | 1.00 | 66 x 51 | 72 x 63 | 36% | 19% | 15% |
| Verdant Hollow Swarm | 342 | 242 | 1.00 | 1.00 | 66 x 51 | 72 x 63 | 21% | 26% | 21% |
| Umbral Reach Armoured | 274 | 390 | 1.67 | 1.00 | 66 x 51 | 72 x 63 | 26% | 16% | 13% |
| Umbral Reach Bruiser | 442 | 469 | 2.33 | 1.00 | 66 x 51 | 72 x 63 | 16% | 13% | 11% |
| Umbral Reach Caster | 197 | 331 | 1.67 | 1.00 | 66 x 51 | 72 x 63 | 37% | 19% | 15% |
| Umbral Reach Swarm | 268 | 245 | 1.00 | 1.00 | 66 x 51 | 72 x 63 | 27% | 26% | 21% |
| The Pale Choir Armoured | 399 | 386 | 1.67 | 1.00 | 66 x 51 | 72 x 63 | 18% | 16% | 13% |
| The Pale Choir Bruiser | 345 | 474 | 2.33 | 1.00 | 66 x 51 | 72 x 63 | 21% | 13% | 11% |
| The Pale Choir Caster | 201 | 331 | 1.67 | 1.00 | 66 x 51 | 72 x 63 | 36% | 19% | 15% |
| The Pale Choir Swarm (pale host) | 250 | 246 | 1.00 | 1.00 | 66 x 51 | 72 x 63 | 29% | 26% | 21% |
| boss: CRYSTAL LICH | 349 | 521 | 2.33 | 1.00 | 66 x 51 | 72 x 63 | 21% | 12% | 10% |
| boss: FORGE COLOSSUS | 566 | 519 | 2.33 | 1.00 | 66 x 51 | 72 x 63 | 13% | 12% | 10% |
| boss: LUMEN ANGEL | 534 | 520 | 2.33 | 1.00 | 66 x 51 | 72 x 63 | 13% | 12% | 10% |
| boss: SPIRIT MATRON | 386 | 520 | 2.33 | 1.00 | 66 x 51 | 72 x 63 | 19% | 12% | 10% |
| boss: THORN REGENT | 410 | 521 | 2.33 | 1.00 | 66 x 51 | 72 x 63 | 18% | 12% | 10% |
| boss: VOID REAPER | 497 | 521 | 2.33 | 1.00 | 66 x 51 | 72 x 63 | 14% | 12% | 10% |
| dark whelp (the reference, Umbral Reach Swarm) | 268 | 245 | 1.00 | 1.00 | 66 x 51 | 72 x 63 | 27% | 26% | 21% |

## Before (97efe379, MaxScale 3)

| host | visible w | visible h | scale before | scale | D1 w x h | D3 w x h | D3 w share | D3 h share | D1 h share |
|---|---|---|---|---|---|---|---|---|---|
| Marrow Wastes Armoured | 260 | 386 | 1.67 | 1.67 | 110 x 85 | 120 x 105 | 46% | 27% | 22% |
| Marrow Wastes Bruiser | 415 | 469 | 2.33 | 2.33 | 154 x 119 | 168 x 147 | 40% | 31% | 25% |
| Marrow Wastes Caster | 200 | 331 | 1.67 | 1.67 | 110 x 85 | 120 x 105 | 60% | 32% | 26% |
| Marrow Wastes Swarm | 386 | 241 | 1.00 | 1.00 | 66 x 51 | 72 x 63 | 19% | 26% | 21% |
| Cinderworks Armoured | 346 | 390 | 1.67 | 1.67 | 110 x 85 | 120 x 105 | 35% | 27% | 22% |
| Cinderworks Bruiser | 431 | 474 | 2.33 | 2.33 | 154 x 119 | 168 x 147 | 39% | 31% | 25% |
| Cinderworks Caster | 243 | 331 | 1.67 | 1.67 | 110 x 85 | 120 x 105 | 49% | 32% | 26% |
| Cinderworks Swarm | 239 | 246 | 1.00 | 1.00 | 66 x 51 | 72 x 63 | 30% | 26% | 21% |
| The Still Archive Armoured | 286 | 390 | 1.67 | 1.67 | 110 x 85 | 120 x 105 | 42% | 27% | 22% |
| The Still Archive Bruiser | 392 | 474 | 2.33 | 2.33 | 154 x 119 | 168 x 147 | 43% | 31% | 25% |
| The Still Archive Caster | 216 | 331 | 1.67 | 1.67 | 110 x 85 | 120 x 105 | 56% | 32% | 26% |
| The Still Archive Swarm | 272 | 244 | 1.00 | 1.00 | 66 x 51 | 72 x 63 | 26% | 26% | 21% |
| Verdant Hollow Armoured | 330 | 390 | 1.67 | 1.67 | 110 x 85 | 120 x 105 | 36% | 27% | 22% |
| Verdant Hollow Bruiser | 417 | 474 | 2.33 | 2.33 | 154 x 119 | 168 x 147 | 40% | 31% | 25% |
| Verdant Hollow Caster | 201 | 331 | 1.67 | 1.67 | 110 x 85 | 120 x 105 | 60% | 32% | 26% |
| Verdant Hollow Swarm | 342 | 242 | 1.00 | 1.00 | 66 x 51 | 72 x 63 | 21% | 26% | 21% |
| Umbral Reach Armoured | 274 | 390 | 1.67 | 1.67 | 110 x 85 | 120 x 105 | 44% | 27% | 22% |
| Umbral Reach Bruiser | 442 | 469 | 2.33 | 2.33 | 154 x 119 | 168 x 147 | 38% | 31% | 25% |
| Umbral Reach Caster | 197 | 331 | 1.67 | 1.67 | 110 x 85 | 120 x 105 | 61% | 32% | 26% |
| Umbral Reach Swarm | 268 | 245 | 1.00 | 1.00 | 66 x 51 | 72 x 63 | 27% | 26% | 21% |
| The Pale Choir Armoured | 399 | 386 | 1.67 | 1.67 | 110 x 85 | 120 x 105 | 30% | 27% | 22% |
| The Pale Choir Bruiser | 345 | 474 | 2.33 | 2.33 | 154 x 119 | 168 x 147 | 49% | 31% | 25% |
| The Pale Choir Caster | 201 | 331 | 1.67 | 1.67 | 110 x 85 | 120 x 105 | 60% | 32% | 26% |
| The Pale Choir Swarm (pale host) | 250 | 246 | 1.00 | 1.00 | 66 x 51 | 72 x 63 | 29% | 26% | 21% |
| boss: CRYSTAL LICH | 349 | 521 | 2.33 | 2.33 | 154 x 119 | 168 x 147 | 48% | 28% | 23% |
| boss: FORGE COLOSSUS | 566 | 519 | 2.33 | 2.33 | 154 x 119 | 168 x 147 | 30% | 28% | 23% |
| boss: LUMEN ANGEL | 534 | 520 | 2.33 | 2.33 | 154 x 119 | 168 x 147 | 31% | 28% | 23% |
| boss: SPIRIT MATRON | 386 | 520 | 2.33 | 2.33 | 154 x 119 | 168 x 147 | 44% | 28% | 23% |
| boss: THORN REGENT | 410 | 521 | 2.33 | 2.33 | 154 x 119 | 168 x 147 | 41% | 28% | 23% |
| boss: VOID REAPER | 497 | 521 | 2.33 | 2.33 | 154 x 119 | 168 x 147 | 34% | 28% | 23% |
| dark whelp (the reference, Umbral Reach Swarm) | 268 | 245 | 1.00 | 1.00 | 66 x 51 | 72 x 63 | 27% | 26% | 21% |
