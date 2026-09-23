# VFX temporal taxonomy — every live strip, classified by its RUNTIME uses (2026-09-23)

Generated from `SkillCatalogue` (form → profile, `FxKey` → `FxFor` lookup, `SkillKind.Field` → the HELD
`field.aura` profile), `VfxProfiles` (the system effects), today's `check_fx_edges.py` run and the game's own
`RH_VFX_BUDGET` ledger (the worst scale ratio each strip can be asked for; the budget is 1.25).

| Archetype | Strips | Contract (what the motion must be) |
|---|---|---|
| IMPACT | 27 | one event: optional build-up → one peak → outward breakup → fade. No collapse and second burst, no loop, no late central hit |
| PROJECTILE | 11 | the RENDERER flies the strip; the art is an in-place flight pose (a spin, a flicker). Continuous, stable size, no reset, no second launch |
| TRAP / MARK | 22 | establish → read clearly → settle into a held form. Energy need not fall after the first peak; no repeated placement |
| FIELD / AURA | 4 (+12 duals) | a held LOOP: one coherent silhouette, breathing allowed, repeated peaks allowed, frame 7 flows into frame 0, no structural reset |
| SHIELD / BARRIER | 1 | stable main geometry, modest breath, no respawn, no size jump; also flares once as a one-shot |
| OTHER | 2 | `fx_weep` (a one-shot rain: its falling streaks are cyclic inside it); `fx_bind_chain` (BUILD screen, AlphaBlend, not HUNT) |

**Lifetime:** 45 one-shot · 2 held only (`fx_press`, `fx_wilt`) · **14 DUAL** (held AND one-shot: the ten
champion marks through BRAND, `fx_tower_strike` and `fx_unbroken_trap` through their signature fields,
`fx_aura`, `fx_shield`) · 5 fallback-only (every champion owns that form, so nothing reaches them) · 1 UI.

A DUAL strip must satisfy both contracts: it reads as a one-shot under the renderer's tail fade, and it
loops seamlessly when held.

| Archetype | Strip | Lifetime | Gate today | Worst scale ratio (budget ≤ 1.25) | Runtime uses |
|---|---|---|---|---|---|
| FIELD/AURA | `fx_aura` | DUAL (held + one-shot) | pass | 1.09 | HELD: GRAVE SONG field (signature); HELD: MIRE field (any hunter); ONE-SHOT: PULSE active -> cast.aura |
| FIELD/AURA | `fx_press` | HELD | pass | 1.44 OVER | HELD: PRESS field (any hunter) |
| FIELD/AURA | `fx_tower_strike` | DUAL (held + one-shot) | SOFT | 0.75 | HELD: SLOW FALL field (signature); ONE-SHOT: BLOW active -> cast.strike (on the creature) |
| FIELD/AURA | `fx_wilt` | HELD | pass | 0.99 | HELD: WILT field (any hunter) |
| IMPACT | `fx_anvil_strike` | ONE-SHOT | SOFT | 0.70 | ONE-SHOT: BLOW active -> cast.strike (on the creature); ONE-SHOT: HARDFACE active -> cast.strike (on the creature) (signature) |
| IMPACT | `fx_anvil_transformation` | ONE-SHOT | SOFT | 1.39 OVER | ONE-SHOT: DRINK active -> cast.transformation (on the hunter) |
| IMPACT | `fx_chorus_strike` | ONE-SHOT | SOFT | 0.70 | ONE-SHOT: BLOW active -> cast.strike (on the creature) |
| IMPACT | `fx_chorus_transformation` | ONE-SHOT | SOFT | 0.87 | ONE-SHOT: DRINK active -> cast.transformation (on the hunter) |
| IMPACT | `fx_crit` | ONE-SHOT | pass | 0.68 | ONE-SHOT: death.boss_burst |
| IMPACT | `fx_death` | ONE-SHOT | SOFT | 0.91 | ONE-SHOT: death.creature / death.champion |
| IMPACT | `fx_heal` | ONE-SHOT | SOFT | 0.78 | ONE-SHOT: heal.column |
| IMPACT | `fx_hit` | ONE-SHOT | SOFT | 0.38 | ONE-SHOT: impact.bite (a bite on the hunter) |
| IMPACT | `fx_magpie_strike` | ONE-SHOT | SOFT | 1.36 OVER | ONE-SHOT: BLOW active -> cast.strike (on the creature) |
| IMPACT | `fx_magpie_transformation` | ONE-SHOT | SOFT | 1.14 | ONE-SHOT: DRINK active -> cast.transformation (on the hunter); ONE-SHOT: PAYING WORK active -> cast.transformation (on the hunter) (signature) |
| IMPACT | `fx_metronome_strike` | ONE-SHOT | SOFT | 0.67 | ONE-SHOT: BLOW active -> cast.strike (on the creature) |
| IMPACT | `fx_metronome_transformation` | ONE-SHOT | SOFT | 0.93 | ONE-SHOT: DRINK active -> cast.transformation (on the hunter) |
| IMPACT | `fx_oathbound_strike` | ONE-SHOT | SOFT | 0.68 | ONE-SHOT: BLOW active -> cast.strike (on the creature) |
| IMPACT | `fx_oathbound_transformation` | ONE-SHOT | SOFT | 0.97 | ONE-SHOT: DRINK active -> cast.transformation (on the hunter) |
| IMPACT | `fx_quiver_strike` | ONE-SHOT | SOFT | 0.63 | ONE-SHOT: BLOW active -> cast.strike (on the creature) |
| IMPACT | `fx_quiver_transformation` | ONE-SHOT | SOFT | 1.10 | ONE-SHOT: DRINK active -> cast.transformation (on the hunter) |
| IMPACT | `fx_seeker_strike` | ONE-SHOT | pass | 0.59 | ONE-SHOT: BLOW active -> cast.strike (on the creature); ONE-SHOT: HARD HANDS active -> cast.strike (on the creature) (signature) |
| IMPACT | `fx_seeker_transformation` | ONE-SHOT | EDGE+SOFT | 0.84 | ONE-SHOT: DRINK active -> cast.transformation (on the hunter) |
| IMPACT | `fx_shield_break` | ONE-SHOT | SOFT | 1.05 | ONE-SHOT: shield.break |
| IMPACT | `fx_strike` | fallback | SOFT | 0.94 | FALLBACK ONLY (every champion owns this form) |
| IMPACT | `fx_thornwall_strike` | ONE-SHOT | SOFT | 0.75 | ONE-SHOT: BLOW active -> cast.strike (on the creature) |
| IMPACT | `fx_thornwall_transformation` | ONE-SHOT | SOFT | 1.04 | ONE-SHOT: DRINK active -> cast.transformation (on the hunter) |
| IMPACT | `fx_tower_transformation` | ONE-SHOT | SOFT | 0.91 | ONE-SHOT: DRINK active -> cast.transformation (on the hunter) |
| IMPACT | `fx_transformation` | fallback | pass | 0.86 | FALLBACK ONLY (every champion owns this form) |
| IMPACT | `fx_unbroken_strike` | ONE-SHOT | SOFT | 0.58 | ONE-SHOT: BLOW active -> cast.strike (on the creature) |
| IMPACT | `fx_unbroken_transformation` | ONE-SHOT | SOFT | 1.10 | ONE-SHOT: DRINK active -> cast.transformation (on the hunter) |
| IMPACT | `fx_weakhit` | ONE-SHOT | SOFT | 0.49 | ONE-SHOT: impact.weak (weak hit on a creature) |
| OTHER | `fx_bind_chain` | UI | SOFT | 0.00 | UI: BUILD screen chain animation (LoadoutScreen, AlphaBlend, not HUNT) |
| OTHER | `fx_weep` | ONE-SHOT | EDGE+SOFT | 0.94 | ONE-SHOT: WEEP reaction -> cast.rain (over the head) |
| PROJECTILE | `fx_anvil_projectile` | ONE-SHOT | SOFT | 0.58 | ONE-SHOT: SPRAY active -> cast.projectile (flies to target) |
| PROJECTILE | `fx_chorus_projectile` | ONE-SHOT | SOFT | 0.45 | ONE-SHOT: SPRAY active -> cast.projectile (flies to target) |
| PROJECTILE | `fx_magpie_projectile` | ONE-SHOT | SOFT | 0.66 | ONE-SHOT: SPRAY active -> cast.projectile (flies to target) |
| PROJECTILE | `fx_metronome_projectile` | ONE-SHOT | SOFT | 0.44 | ONE-SHOT: CLOCKWORK active -> cast.projectile (flies to target) (signature); ONE-SHOT: SPRAY active -> cast.projectile (flies to target) |
| PROJECTILE | `fx_oathbound_projectile` | ONE-SHOT | SOFT | 0.25 | ONE-SHOT: SPRAY active -> cast.projectile (flies to target) |
| PROJECTILE | `fx_projectile` | fallback | SOFT | 0.48 | FALLBACK ONLY (every champion owns this form) |
| PROJECTILE | `fx_quiver_projectile` | ONE-SHOT | EDGE+SOFT | 0.34 | ONE-SHOT: BACKDRAW reaction -> cast.projectile (flies to target) (signature); ONE-SHOT: SPRAY active -> cast.projectile (flies to target) |
| PROJECTILE | `fx_seeker_projectile` | ONE-SHOT | SOFT | 0.84 | ONE-SHOT: SPRAY active -> cast.projectile (flies to target) |
| PROJECTILE | `fx_thornwall_projectile` | ONE-SHOT | SOFT | 0.26 | ONE-SHOT: SPRAY active -> cast.projectile (flies to target) |
| PROJECTILE | `fx_tower_projectile` | ONE-SHOT | SOFT | 0.30 | ONE-SHOT: SPRAY active -> cast.projectile (flies to target) |
| PROJECTILE | `fx_unbroken_projectile` | ONE-SHOT | SOFT | 0.61 | ONE-SHOT: SPRAY active -> cast.projectile (flies to target) |
| SHIELD/BARRIER | `fx_shield` | DUAL (held + one-shot) | EDGE+SOFT | 0.93 | HELD: shield.barrier (while SHIELD stands); ONE-SHOT: shield.gain / absorb / undying flares |
| TRAP/MARK | `fx_anvil_mark` | DUAL (held + one-shot) | SOFT | 0.56 | HELD: BRAND field (any hunter); ONE-SHOT: CALL active -> cast.mark (over the head) |
| TRAP/MARK | `fx_anvil_trap` | ONE-SHOT | SOFT | 1.33 OVER | ONE-SHOT: JAWS reaction -> cast.trap (under the row); ONE-SHOT: REPAY active -> cast.trap (under the row) |
| TRAP/MARK | `fx_chorus_mark` | DUAL (held + one-shot) | SOFT | 0.55 | HELD: BRAND field (any hunter); ONE-SHOT: CALL active -> cast.mark (over the head) |
| TRAP/MARK | `fx_chorus_trap` | ONE-SHOT | SOFT | 1.52 OVER | ONE-SHOT: JAWS reaction -> cast.trap (under the row); ONE-SHOT: REPAY active -> cast.trap (under the row) |
| TRAP/MARK | `fx_magpie_mark` | DUAL (held + one-shot) | SOFT | 0.56 | HELD: BRAND field (any hunter); ONE-SHOT: CALL active -> cast.mark (over the head) |
| TRAP/MARK | `fx_magpie_trap` | ONE-SHOT | SOFT | 4.95 OVER | ONE-SHOT: JAWS reaction -> cast.trap (under the row); ONE-SHOT: REPAY active -> cast.trap (under the row) |
| TRAP/MARK | `fx_mark` | fallback | SOFT | 1.60 OVER | FALLBACK ONLY (every champion owns this form) |
| TRAP/MARK | `fx_metronome_mark` | DUAL (held + one-shot) | EDGE+SOFT | 0.62 | HELD: BRAND field (any hunter); ONE-SHOT: CALL active -> cast.mark (over the head) |
| TRAP/MARK | `fx_metronome_trap` | ONE-SHOT | SOFT | 1.31 OVER | ONE-SHOT: JAWS reaction -> cast.trap (under the row); ONE-SHOT: REPAY active -> cast.trap (under the row) |
| TRAP/MARK | `fx_oathbound_mark` | DUAL (held + one-shot) | SOFT | 0.56 | HELD: BRAND field (any hunter); ONE-SHOT: CALL active -> cast.mark (over the head); ONE-SHOT: OATHMARK reaction -> cast.mark (over the head) (signature) |
| TRAP/MARK | `fx_oathbound_trap` | ONE-SHOT | SOFT | 1.62 OVER | ONE-SHOT: JAWS reaction -> cast.trap (under the row); ONE-SHOT: REPAY active -> cast.trap (under the row) |
| TRAP/MARK | `fx_quiver_mark` | DUAL (held + one-shot) | SOFT | 0.68 | HELD: BRAND field (any hunter); ONE-SHOT: CALL active -> cast.mark (over the head) |
| TRAP/MARK | `fx_quiver_trap` | ONE-SHOT | SOFT | 1.40 OVER | ONE-SHOT: JAWS reaction -> cast.trap (under the row); ONE-SHOT: REPAY active -> cast.trap (under the row) |
| TRAP/MARK | `fx_seeker_mark` | DUAL (held + one-shot) | SOFT | 0.59 | HELD: BRAND field (any hunter); ONE-SHOT: CALL active -> cast.mark (over the head) |
| TRAP/MARK | `fx_seeker_trap` | ONE-SHOT | SOFT | 1.79 OVER | ONE-SHOT: JAWS reaction -> cast.trap (under the row); ONE-SHOT: REPAY active -> cast.trap (under the row) |
| TRAP/MARK | `fx_thornwall_mark` | DUAL (held + one-shot) | SOFT | 0.50 | HELD: BRAND field (any hunter); ONE-SHOT: CALL active -> cast.mark (over the head) |
| TRAP/MARK | `fx_thornwall_trap` | ONE-SHOT | SOFT | 1.41 OVER | ONE-SHOT: JAWS reaction -> cast.trap (under the row); ONE-SHOT: NARROWS reaction -> cast.trap (under the row) (signature); ONE-SHOT: REPAY active -> cast.trap (under the row) |
| TRAP/MARK | `fx_tower_mark` | DUAL (held + one-shot) | SOFT | 0.49 | HELD: BRAND field (any hunter); ONE-SHOT: CALL active -> cast.mark (over the head) |
| TRAP/MARK | `fx_tower_trap` | ONE-SHOT | SOFT | 1.45 OVER | ONE-SHOT: JAWS reaction -> cast.trap (under the row); ONE-SHOT: REPAY active -> cast.trap (under the row) |
| TRAP/MARK | `fx_trap` | fallback | pass | 1.28 OVER | FALLBACK ONLY (every champion owns this form) |
| TRAP/MARK | `fx_unbroken_mark` | DUAL (held + one-shot) | SOFT | 0.48 | HELD: BRAND field (any hunter); ONE-SHOT: CALL active -> cast.mark (over the head) |
| TRAP/MARK | `fx_unbroken_trap` | DUAL (held + one-shot) | SOFT | 1.48 OVER | HELD: HOLD FAST field (signature); ONE-SHOT: JAWS reaction -> cast.trap (under the row); ONE-SHOT: REPAY active -> cast.trap (under the row) |
