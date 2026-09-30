# BRAND, Concept A LIVING SHADOW CORRUPTION: the glow pass (ADR-011; owner's choice and brief, 2026-10-01)

The owner chose Concept A and asked for one fix: the corruption was too hard to see, above all on dark enemies. Keep
its identity; make it more glowy and readable (dark corruption first, violet glow second), with depth still told by
geometry, a quiet idle, the new growth lit on apply / deepen, and no net / web / sigil read. Not accepted; no sound;
still the prototype pipeline.

Review page: https://claude.ai/artifact/DMw4pyqkKtUXQPBo6DvYdJ

| Part | What |
|---|---|
| Structure | `CursePrototype.Corruption.cs` (`CorruptionAtlas`): veins GROW along paths (birth = path length) from an irregular smoky stain with short uneven veinlets; two bent trunks run along the body's long axis (the field is turned a quarter on a wide body) and break into bushy forks; branches fork forward at shallow angles and many die early; breaks; smoky infected patches, more far out; a vein that meets another stops (no loop). Depth = reach (0.24 / 0.5 / 0.86 of the longest path). Mirrored on alternate slots. |
| Material | dark veins (22, 10, 38) and a faint body darkening; a violet-magenta emission (168, 70, 236), additive, clipped to the silhouette: only some stretches lit, in beads, junction nodes brighter, patches smouldering; idle at 0.85 with a 5.2 s breath and one section at a time answering (4.2 s). |
| Motion | apply / arrival grow the paths with a hot front (236, 150, 255) through the new growth only, then settle; a deepen lights only its new paths; SPRAWL lights the source before each victim ignites; a dying host brightens once and collapses back along its paths. Peaks x0.55 beside an action / a presented reaction / the field's crush. |
| Fresh reads | five rounds of four unprimed readers ("What is happening to this creature?"); revised after cross / dagger, belt / sash reads; final: "a curse / corruption spreading under the skin". |
| Tests | `brand_curse_test` (depth is reach; no depth closes a loop; 15-60 % of the veins lit). |

Takes: `build/shots/brand/kA2` (this pass) and `build/shots/brand/kA` (8a10dae1's Concept A);
`PYTHONUTF8=1 python tools/asset-pipeline/brand_glow_evidence.py kA2 kA build/shots/brand/scale <out>` builds this folder
(the `_off` twins are filmed with `RH_MARK_RECIPES=0` from the same build).
