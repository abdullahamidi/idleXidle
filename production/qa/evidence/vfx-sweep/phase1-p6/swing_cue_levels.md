# The basic attacks' identity cues in the fight's mix (P1.6, the PRESS method)

loudest 50 ms RMS, ITU-R BS.1770 K-weighted (make_press_tick.k_weight), of the file x its play volume x the SFX master 0.8, dB; 'effective' = the raw loudest 50 ms RMS x the volume, dBFS (the hierarchy test's figure); 'distance' = the RMS difference of the 1/3-octave profiles (100 Hz-12.5 kHz) from the archetype the cue replaces, dB.

| cue | what | volume | K50 dB | under the reference contact / release | under its own hit | effective dBFS | length ms (file) | tail dB | <150 / 150-1k / 1-4k / >4k | from its archetype dB | pass |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `sfx_seeker_swing_hit` | a short blade cut into leather, a faint steel edge | 0.36 | -31.3 | sfx_seeker_spray_hit @0.50 (-27.5): +3.8 (>= 3); sfx_seeker_hard_hands_hit @0.55 (-23.5): +7.8 (>= 3) | - | -31.6 | 131 (220) | -78 | 0.45 / 0.02 / 0.06 / 0.47 | `sfx_blade_hit` 13.4 | yes |
| `sfx_anvil_swing_hit` | a padded heavy fist (a boxing bag), a low thud | 0.36 | -31.3 | sfx_seeker_spray_hit @0.50 (-27.5): +3.8 (>= 3); sfx_seeker_hard_hands_hit @0.55 (-23.5): +7.8 (>= 3) | - | -28.5 | 104 (220) | -71 | 0.56 / 0.41 / 0.02 / 0.00 | `sfx_fist_hit` 15.7 | yes |
| `sfx_metronome_swing_hit` | a dry knuckle hit on wood, a sharp click | 0.36 | -31.4 | sfx_seeker_spray_hit @0.50 (-27.5): +4.0 (>= 3); sfx_seeker_hard_hands_hit @0.55 (-23.5): +7.9 (>= 3) | - | -30.3 | 71 (220) | -78 | 0.00 / 0.79 / 0.19 / 0.01 | `sfx_fist_hit` 13.9 | yes |
| `sfx_tower_swing_hit` | a mallet on packed earth, a short gravel tick | 0.36 | -31.3 | sfx_seeker_spray_hit @0.50 (-27.5): +3.8 (>= 3); sfx_seeker_hard_hands_hit @0.55 (-23.5): +7.8 (>= 3) | - | -28.1 | 76 (220) | -72 | 0.71 / 0.27 / 0.02 / 0.00 | `sfx_stone_hit` 6.8 | yes |
| `sfx_thornwall_swing_hit` | a wooden shield thump with a steel rim rattle | 0.36 | -31.3 | sfx_seeker_spray_hit @0.50 (-27.5): +3.8 (>= 3); sfx_seeker_hard_hands_hit @0.55 (-23.5): +7.8 (>= 3) | - | -29.3 | 144 (220) | -220 | 0.67 / 0.14 / 0.13 / 0.06 | `sfx_wood_hit` 3.6 | yes |
| `sfx_magpie_swing_hit` | a quick knife nick, a short cloth tear | 0.36 | -31.3 | sfx_seeker_spray_hit @0.50 (-27.5): +3.8 (>= 3); sfx_seeker_hard_hands_hit @0.55 (-23.5): +7.8 (>= 3) | - | -32.0 | 136 (220) | -217 | 0.00 / 0.20 / 0.62 / 0.18 | `sfx_blade_hit` 9.0 | yes |
| `sfx_quiver_swing_hit` | an arrow into hide + a wooden clack | 0.36 | -31.3 | sfx_seeker_spray_hit @0.50 (-27.5): +3.8 (>= 3); sfx_seeker_hard_hands_hit @0.55 (-23.5): +7.8 (>= 3) | - | -32.3 | 81 (220) | -217 | 0.01 / 0.21 / 0.54 / 0.25 | `sfx_blade_hit` 6.1 | yes |
| `sfx_quiver_loose` | a bowstring twang | 0.18 | -37.0 | sfx_seeker_spray_release @0.40 (-30.1): +6.9 (>= 3) | sfx_quiver_swing_hit: +5.7 | -34.6 | 138 (170) | -46 | 0.75 / 0.10 / 0.11 / 0.04 | `sfx_throw_release` 14.4 | yes |
| `sfx_chorus_swing_hit` | bone clatter on hide | 0.36 | -31.3 | sfx_seeker_spray_hit @0.50 (-27.5): +3.8 (>= 3); sfx_seeker_hard_hands_hit @0.55 (-23.5): +7.8 (>= 3) | - | -30.4 | 114 (220) | -78 | 0.13 / 0.53 / 0.28 / 0.06 | `sfx_wood_hit` 6.1 | yes |
| `sfx_chorus_toss` | a light toss whip | 0.16 | -37.0 | sfx_seeker_spray_release @0.40 (-30.1): +6.9 (>= 3) | sfx_chorus_swing_hit: +5.7 | -35.4 | 148 (170) | -35 | 0.00 / 0.90 / 0.10 / 0.00 | `sfx_throw_release` 6.9 | yes |
| `sfx_unbroken_swing_hit` | a stone chip on rock | 0.36 | -32.0 | sfx_seeker_spray_hit @0.50 (-27.5): +4.5 (>= 3); sfx_seeker_hard_hands_hit @0.55 (-23.5): +8.5 (>= 3) | - | -30.7 | 121 (220) | -77 | 0.00 / 0.85 / 0.12 / 0.02 | `sfx_stone_hit` 11.9 | yes |
| `sfx_unbroken_toss` | a short heavy toss | 0.16 | -37.0 | sfx_seeker_spray_release @0.40 (-30.1): +6.9 (>= 3) | sfx_unbroken_swing_hit: +5.0 | -35.2 | 146 (170) | -36 | 0.16 / 0.70 / 0.14 / 0.00 | `sfx_throw_release` 25.9 | yes |
| `sfx_oathbound_swing_hit` | a chain whip crack + two link rattles | 0.36 | -31.3 | sfx_seeker_spray_hit @0.50 (-27.5): +3.8 (>= 3); sfx_seeker_hard_hands_hit @0.55 (-23.5): +7.8 (>= 3) | - | -32.9 | 98 (220) | -216 | 0.00 / 0.07 / 0.48 / 0.45 | `sfx_fist_hit` 22.1 | yes |

All pass: **yes**.
