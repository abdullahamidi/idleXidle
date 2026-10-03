# Phase 0 / P0.4 evidence: the generic Skill path (2026-10-03)

Traces (RH_PRESENT_TRACE=1, RH_SHOT_SEED=7) filmed by `tools/asset-pipeline/films_sweep.sh p04 ...`; every frame deleted.

| take | what it shows |
|------|---------------|
| ref_seeker, ref_fast, ref_brand, ref_brand_press, ref_jaws_kill | `action_regression.py --refs` vs BOTH phase0-baseline films (`_a`, `_b`): IDENTICAL x 10. Callouts now say SPRAY / HARD HANDS (excluded from the reference regression). |
| p04_quiver | quiver, BUILD `sig_quiver_backdraw,hammer_press@Body,volley_spray@Mind,hammer_blow@Body`, seek `skill:sig_quiver_backdraw+unstruck`: at 12400 the wave's last kill, then BACKDRAW's Skill (slot 0) -> `skill-skip`, no callout / vfx-spawn / sfx_cast for it (SPRAY's callout and cast breath at that ms belong to slot 2). |
| p04_oathbound | oathbound, BUILD `sig_oathbound_oathmark,hammer_press@Body,volley_spray@Mind,hammer_blow@Body`, seek `skill:sig_oathbound_oathmark`: 4 OATHMARK answers, 0 `clip-start trap`, 0 pitched `sfx_hit` (vol 0.40 pitch +0.25). |
| p04_weaver | seeker fixture + RH_SHOT_KEYSTONES=weaver: HARD HANDS at 11200 woven into SPRAY -> `echo slot=1`, one callout (HARD HANDS, at its release), no second sfx_cast, the echo's effect at x0.6. |
