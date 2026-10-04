# The archetype cues in the fight's mix (P1.5, the PRESS method)

loudest 50 ms RMS, ITU-R BS.1770 K-weighted (make_press_tick.k_weight), of the file x its play volume x the SFX master 0.8, dB; 'effective' = the raw loudest 50 ms RMS x the volume, dBFS (the hierarchy test's figure).

| cue | what | volume | K50 dB | under its ceiling | effective dBFS | length ms | <150 / 150-1k / 1-4k / >4k | tonality dB | pass |
|---|---|---|---|---|---|---|---|---|---|
| `sfx_fist_hit` | a padded fist | 0.36 | -31.3 | sfx_seeker_spray_hit @0.50 (-27.5): +3.9 (>= 3); sfx_seeker_hard_hands_hit @0.55 (-23.5): +7.8 (>= 3) | -28.9 | 117 | 0.35 / 0.60 / 0.05 / 0.00 | 9.4 | yes |
| `sfx_blade_hit` | a short cut into leather | 0.36 | -31.3 | sfx_seeker_spray_hit @0.50 (-27.5): +3.8 (>= 3); sfx_seeker_hard_hands_hit @0.55 (-23.5): +7.8 (>= 3) | -31.6 | 75 | 0.00 / 0.25 / 0.60 / 0.14 | 11.3 | yes |
| `sfx_stone_hit` | a stone on packed earth | 0.36 | -31.3 | sfx_seeker_spray_hit @0.50 (-27.5): +3.8 (>= 3); sfx_seeker_hard_hands_hit @0.55 (-23.5): +7.8 (>= 3) | -29.5 | 90 | 0.20 / 0.64 / 0.16 / 0.00 | 6.6 | yes |
| `sfx_wood_hit` | a wooden thump with a rim | 0.36 | -31.3 | sfx_seeker_spray_hit @0.50 (-27.5): +3.8 (>= 3); sfx_seeker_hard_hands_hit @0.55 (-23.5): +7.8 (>= 3) | -30.4 | 157 | 0.29 / 0.43 / 0.13 / 0.14 | 11.6 | yes |
| `sfx_throw_release` | a cloth whip + air | 0.17 | -37.0 | sfx_seeker_spray_release @0.40 (-30.1): +6.9 (>= 3) | -36.3 | 194 | 0.00 / 0.37 / 0.63 / 0.01 | 10.8 | yes |
| `sfx_air_release` | an air push, no tone | 0.24 | -35.0 | sfx_seeker_spray_release @0.40 (-30.1): +4.9 (>= 3) | -33.0 | 191 | 0.42 / 0.41 / 0.15 / 0.02 | 11.2 | yes |
| `sfx_field_tick` | a quiet material settle | 0.34 | -33.0 | sfx_seeker_press_tick @0.28 (-31.2): +1.8 (>= 0) | -33.9 | 178 | 0.00 / 0.19 / 0.63 / 0.19 | 8.3 | yes |
| `sfx_cloth_commit` | a boot plant + cloth | 0.30 | -35.0 | sfx_seeker_hard_hands_commit @0.34 (-33.3): +1.7 (>= 0) | -33.2 | 142 | 0.24 / 0.62 / 0.13 / 0.00 | 7.5 | yes |

All under their ceilings: **yes**.
