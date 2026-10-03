# Phase 0 / P0.3: the generic Strike path (quiet derived hits, one cue per batch ms, the last-owner rule)

Trace logs only (no frames kept). All takes: `tools/asset-pipeline/films_sweep.sh`, RH_SHOT_SEED=7, RH_PRESENT_TRACE=1.

| log | take | what it shows |
|-----|------|---------------|
| ref_seeker.log, ref_fast.log, ref_brand.log, ref_brand_press.log, ref_jaws_kill.log | the table's ref takes | `action_regression.py --refs` IDENTICAL against BOTH `phase0-baseline/<take>_a.log` and `_b.log` |
| ref_seeker_run1.log | ref_seeker, first film | 1 difference: `reaction-clamp` clamp/body x 1194 vs 1195 at 7333 (see notes.md: the host's bite lunge advances on frame dt; ref_seeker holds no quiet hit) |
| p03_bleed.log | `seeker sig_seeker_hard_hands,volley_spray,hammer_press@Body,volley_weep@Shadow`, fightmulti, 20x30 | 20 `quiet-hit hit=Bleed` batches: 0 `flash`, 0 generic `sfx_hit`, each number `grade=Quiet`. The before film (`phase0-before/before_bleed.log`) flashed and thudded on all 10 of its bleed-only batches |
| p03_quiver_multi.log | `quiver sig_quiver_backdraw,volley_spray,hammer_press@Body,hammer_blow@Body`, fightmulti, 20x30 | 7 batches with 2-7 non-performed skill Strikes at one ms: exactly one generic `sfx_hit` per ms |

The Seeker's own fightmulti SPRAY batch (at 5200: three Strikes on slot 0) is all PERFORMED hits, so it asks no generic
`sfx_hit` before or after; the Quiver take (no recipe: the generic path) is the multi-creature proof.
