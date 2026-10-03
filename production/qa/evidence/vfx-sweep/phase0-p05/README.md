# Phase 0 / P0.5 evidence: the generic heal receive and the ShieldGained claim (2026-10-03)

Traces (RH_PRESENT_TRACE=1, RH_SHOT_SEED=7) filmed by `tools/asset-pipeline/films_sweep.sh p05 ...`; every frame deleted.

| take | what it shows |
|------|---------------|
| ref_seeker, ref_fast, ref_brand, ref_brand_press, ref_jaws_kill | `action_regression.py --refs` vs BOTH phase0-baseline films (`_a`, `_b`): IDENTICAL x 10, filmed at the final code. None of the five fixtures holds a Heal or a ShieldGained event. |
| p05_drink | seeker, BUILD `sig_seeker_hard_hands,drain_drink@Nature,hammer_press@Body,snare_jaws@Shadow`, seek `skill:drain_drink`, RH_SHOT_ENEMY=1500,25: two Heals at 9700 (46 + 11) -> ONE callout `+57` at playhead 10113 (+400 ms); one Heal at 18700 -> `+15` at 19113. No `fx_heal` vfx-spawn, no heal sound. |
| p05_holdfast | unbroken, RH_SHOT_VARIATION=sig_unbroken_hold_fast:BREASTWORK+GROUNDWORK, 150 x 8: the wave-open ShieldGained (at=0, amount 5) has no `sfx_shield_gain`, no `shield.gain` spawn and no callout; the mid-wave gains (4000, 8000, ...; unclaimed in Phase 0) keep the cue 0.40, the one-shot and `+10 SHIELD`. Absorb / break unchanged. |
