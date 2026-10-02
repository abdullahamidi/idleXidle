# BRAND audio: final live verification (Candidate A canonical), 2026-10-02

**Verdict: PASS.** The production BRAND (renderer 68a970d7 + approved cue set A) behaves as specified in every live
take. No defect found, no source changed.

## How

- Branch `fix/vfx-fade` at d6d74088. The working tree held another agent's uncommitted approval edits, and they were
  included in this build. In `MarkRecipe.cs` those edits change only comments.
- Eight trace-only takes from `films_brand.sh fv <take>`, seed 7, `RH_PRESENT_TRACE=1`: `audio_long`, `audio_sprawl`,
  `audio_etch`, `audio_etch_clean`, `audio_sprawl_winnow_clean`, `audio_press`, `audio_before` (`RH_MARK_RECIPES=0`)
  and `ba_death`.
- The frames were deleted after each take (`find -delete`). `build/shots` ends at its starting size, 39 472 498 B.
- The logs were moved to `build/tmp/brandaudio/fv/`, together with the parser `fv.py`.
- The parser splits each take into waves when the playhead resets. It then checks the `mark-cue`, `sound`,
  `mark-draw`, `mark-wave`, `curse-seat` and `event` rows against each other.
- Installed cues: all seven `assets/audio/combat/sfx_seeker_brand_*.wav` files are SHA-256 identical to
  `audio_history/brand_curse/A/`. So are the copies the build put in `bin/Debug/.../assets/audio/combat/`. None was
  rebuilt.
- Unit tests: the BRAND / curse / mark filter of `IdleXIdle.Game.Tests` passed (111 of 111, 0 failed).

## Checks

| check | result | evidence (playhead ms) |
|---|---|---|
| apply fires once | PASS | 1 Apply per wave in every take with BRAND. It is asked at 1883 for the 2000 tick. |
| deepen once per presented deepen, on the drawn step | PASS | `etch` / `etch_clean`: Deepen at 4383, on the same frame as the `mark-draw` stage step 1>2. DeepenDeep at 6383, on the same frame as 2>3. 2 deepen cues for 2 shown steps in each wave (4 waves). |
| multi-depth = one cue | PASS | A host that lands already deep (stage 0>2 at 5533, 0>3 at 7533 / 8533) gets one Awaken and no added deepen. |
| SPRAWL infects = one stepped phrase | PASS | One Infect per victim: 2200 / 2250 / 2283 (a stagger of 50 and 33 ms). vol 0.087 / 0.077 / 0.067 (x0.88 each). pitch 0 / -0.04 / -0.08. Identical in `sprawl` and `sprawl_winnow_clean`, in both waves. |
| ripple deepen (SPRAWL+WINNOW) | not exercisable live | No SPRAWL host crosses a stage floor: no `mark-draw` stage step and no deepen cue. This is correct, because no deepen is due. The ripple grouping is covered by `brand_audio_test` (passing). |
| leave, then silence, then awaken | PASS | Leave → next Awaken gap: 333 ms, or 833 ms when the next host lands a tick later (`long` 833/333/833; `etch` 333/833/333; `press` 833/333/333). No BRAND row in the gap. |
| multiple deaths → no multiplied leaves | PASS | 0 frames with more than one Leave. 1 Leave per `EnemyDown`, in every take. No take had two cursed hosts fall on one frame, so the merge path is covered by the unit tests. |
| no cue twice in a wave | PASS | 0 repeated (kind, moment, slot) in 15 waves. Every cue asked has exactly one `sound` row on its frame. Every `sfx_seeker_brand_*` sound has its cue. |
| wave-end leave in the break | PASS | The wave-ending kill's Leave is asked on its fall: `long` / `ba_death` 17200, `etch*` 11200, `press` 14200, `sprawl` 18700, `sprawl_winnow_clean` 12700. Nothing BRAND follows except its Ash. The playhead keeps running about 900 ms into the break (to 18100 / 12100 / 15100 / 19600 / 13600) before the next wave starts. |
| recipes off (`audio_before`) | PASS | 0 `mark-cue`, 0 `mark-wave`, 0 `mark-draw`, 0 `curse-seat` and 0 `sfx_seeker_brand_*` rows. |
| ash never without its parent | PASS | Every Ash has its main cue (same slot and take-hold) and was asked after it. When the main cue yields, its Ash is silent (`cue=-`, vol 0): `long` 7650 / 13650, `etch` 4617 / 5650 / 7650 / 8217 / 8650. |
| ash never louder than its cue | PASS | The heard share is vol x the bank's duck, compared with full. Ash ≤ its cue in every row. For example, `etch` wave 2 at 6383 / 6617: DeepenDeep is quiet x0.6, and its Ash is asked at 0.092 with duck=0.45, so it is heard at x0.45. Shares on a whole cue: Infect's Ash 0.75, Leave's Ash 0.95. |
| mixed combat: yield / quiet / duck | PASS | Yield beside a JAWS snap: Awaken 0.082 = 0.163 x 0.5, with its Ash silent (`long`, `ba_death`, `etch`). Quiet beside PRESS: every `press` Apply is 0.082 = 0.137 x 0.6, with Ash 0.055. Duck on a contact kill: Leave is duck=0.45 with vol unchanged at 0.109. |
| shipped keys only | PASS | Keys heard: apply, deepen, deepen_deep, infect, leave, awaken, ash. 0 foreign keys. 0 `brand_curse/B`, `/C` or `audio_history` strings in any log. No B/C path in `src`. |
| zero allocation (steady state) | PASS | `voice=0` on all 7 289 `mark-draw` rows. `alloc=1128` appears only on frames that carry a `curse-seat` row: the trace-only seat string, once per new host (the pre-existing finding in brand-production/10). 0 other non-zero rows. |
| draws / batches (ADR-013) | PASS | 2 boundaries per cursed body: 1 host = 2. 4 = a living host plus a dying one. 4 SPRAWL bodies = 8, the maximum. See the table below. |

## Draws / batches vs ADR-013 baseline (`brand-production/10_perf_allocation.md`)

| take | draw calls mean / max | batches mean / max | baseline (draws / batches) |
|---|---|---|---|
| `long` (w1) | 1.72 / 4 | 2.02 / 4 | 1.75 / 4 · 2.05 / 4 |
| `etch_clean` (w1) | 2.01 / 4 | 2.13 / 4 | 2.02 / 4 · 2.13 / 4 |
| `etch` (w1) | 2.01 / 4 | 2.13 / 4 | 2.02 / 4 · 2.12 / 4 |
| `press` (w1) | 1.77 / 4 | 2.10 / 4 | 1.81 / 4 · 2.12 / 4 |
| `sprawl` (w1) | 4.58 / 8 | 5.80 / 8 | 5.58 / 8 · 7.04 / 8 |

The SPRAWL means are lower only because this take covers the whole wave, including its four falls. The baseline take
covered a window with all four bodies alive. The maximum (8 / 8) and the cost per body (2) are unchanged.

## The BRAND cues heard in one wave (A, `long` wave 1; vol is the game's ask)

| ms | cue | vol | note |
|---|---|---|---|
| 1883 / 2067 | apply / ash | 0.137 / 0.092 | |
| 6700 / 6717 | leave / ash | 0.109 / 0.087 | |
| 7533 / 7650 | awaken / ash | 0.082 / 0 | yields to the JAWS snap |
| 8200 / 8217 | leave / ash | 0.109 / 0.087 | duck 0.45 |
| 8533 / 8650 | awaken / ash | 0.073 / 0.055 | quiet |
| 12700 / 12717 | leave / ash | 0.109 / 0.087 | |
| 13533 / 13650 | awaken / ash | 0.082 / 0 | yields |
| 17200 / 17217 | leave / ash | 0.109 / 0.087 | the wave-ending kill, in the break; duck 0.45 |
