# BRAND final visual correction: a torn DEPTH 3 and a capped size (ADR-011, the MARK reference)

The owner's brief (2026-10-01), from 97efe379: exactly two fixes, nothing else moves. Not accepted; the owner decides.

Review page: https://claude.ai/artifact/7MGguQQXQfwXE4vRGx5PkS

| Fix | What changed |
|---|---|
| DEPTH 3 read as a letter / number / rune / eye | `tools/asset-pipeline/v2/seeker_brand_cut.py`, DESIGN B, depth 3 only: new breaks, torn tapered ends and a wandering width (`ragged`, `gap_taper`); the curl's four pieces each SHIFTED on their own (`torn`), so they no longer continue one curve; the branch that met a gash in a 'Y' healed (`healed`); no incision scrap under six texels (`least_core`); the edge beat lights only a torn piece's outer side if its outline would close; the deepen into depth 3 (the tear) covers the moved pieces. Depths 1 and 2 byte-identical to 97efe379 (its atlas kept in `keypose_sources/seeker_brand_history/`). |
| The mark too large on large hosts | `MarkRecipe.MaxScale` 3 -> 1, MEASURED: `tools/asset-pipeline/brand_scale_shots.sh` (one single-shot fight per host with `RH_SHOT_DUMP`, all 24 family cells and the six bosses; new dial `RH_SHOT_BOSS=<art key>`), `tools/asset-pipeline/brand_scale_table.py` (the table, `09_scale_table.md`). |

Unprimed reads (fresh agents, shown only the picture, asked "What does this mark look like?" first): four rounds, the
last two on the torn depth 3 ("bruise / stain", "torn smear", "splatter", "irregular damage"; no eye, face, spiral or
rune; two of seven reached for a letter only when asked to name something). The full table is on the page.

Takes: `build/shots/brand/c02` (this pass) and `build/shots/brand/c01` (97efe379);
`PYTHONUTF8=1 python tools/asset-pipeline/brand_fix_evidence.py c02 c01 build/shots/brand/scale <out>` builds this folder
(01-08 stills, 09 the scale table, 10-14 films at true speed, 15 the measures and allocation). Tests: Game 845 / Core 1901.
