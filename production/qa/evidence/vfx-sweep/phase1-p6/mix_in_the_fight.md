# The archetype cues in the fight's own mix (P1.5)

film_audio.py's render of each take's sound asks (sound_throttle.json applied). 'own' = the voice's share (the mix minus
the mix with that voice silenced); 'moment' = the whole mix. Loudest 50 ms K-weighted over the 250 ms after the ask, dB.

| take | trace ms | voice | asked vol | own K50 | moment K50 |
|---|---|---|---|---|---|
| p16_seeker | 1013 | `sfx_hit` | 0.30 | -30.2 | -30.2 |
| p16_seeker | 1980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -28.0 |
| p16_seeker | 2013 | `sfx_hit` | 0.30 | -29.2 | -28.0 |
| p16_seeker | 2213 | `sfx_seeker_swing_hit` | 0.36 | -31.7 | -31.7 |
| p16_seeker | 3013 | `sfx_hit` | 0.30 | -29.2 | -29.2 |
| p16_seeker | 3713 | `sfx_seeker_swing_hit` | 0.36 | -31.7 | -31.7 |
| p16_seeker | 3980 | `sfx_seeker_press_tick` | 0.14 | -37.6 | -29.1 |
| p16_seeker | 4013 | `sfx_hit` | 0.30 | -29.2 | -29.1 |
| p16_seeker | 5013 | `sfx_hit` | 0.14 | -36.1 | -27.4 |
| p16_seeker | 5213 | `sfx_seeker_spray_hit` | 0.50 | -28.0 | -27.3 |
| p16_anvil | 1013 | `sfx_hit` | 0.30 | -30.3 | -30.3 |
| p16_anvil | 1980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -28.0 |
| p16_anvil | 2013 | `sfx_hit` | 0.30 | -29.2 | -28.0 |
| p16_anvil | 2213 | `sfx_anvil_swing_hit` | 0.36 | -31.7 | -31.8 |
| p16_anvil | 3013 | `sfx_hit` | 0.30 | -29.2 | -29.2 |
| p16_anvil | 3713 | `sfx_anvil_swing_hit` | 0.36 | -31.7 | -31.7 |
| p16_anvil | 3980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.9 |
| p16_anvil | 4013 | `sfx_hit` | 0.30 | -29.2 | -27.9 |
| p16_anvil | 5013 | `sfx_hit` | 0.30 | -29.2 | -28.0 |
| p16_anvil | 5213 | `sfx_hit` | 0.22 | -33.1 | -27.6 |
| p16_metronome | 1013 | `sfx_hit` | 0.30 | -30.7 | -30.7 |
| p16_metronome | 1980 | `sfx_seeker_press_tick` | 0.17 | -35.9 | -28.9 |
| p16_metronome | 2013 | `sfx_hit` | 0.30 | -29.3 | -28.1 |
| p16_metronome | 2213 | `sfx_hit` | 0.22 | -33.1 | -27.7 |
| p16_metronome | 3013 | `sfx_hit` | 0.30 | -29.6 | -29.6 |
| p16_metronome | 3713 | `sfx_metronome_swing_hit` | 0.36 | -31.8 | -31.8 |
| p16_metronome | 3980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p16_metronome | 4013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p16_metronome | 5013 | `sfx_hit` | 0.30 | -29.2 | -25.5 |
| p16_metronome | 5213 | `sfx_metronome_swing_hit` | 0.36 | -31.9 | -25.4 |
| p16_tower | 1013 | `sfx_hit` | 0.30 | -30.2 | -30.2 |
| p16_tower | 1980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p16_tower | 2013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p16_tower | 2213 | `sfx_tower_swing_hit` | 0.36 | -31.7 | -31.7 |
| p16_tower | 3013 | `sfx_hit` | 0.30 | -29.2 | -29.2 |
| p16_tower | 3713 | `sfx_tower_swing_hit` | 0.36 | -31.7 | -31.7 |
| p16_tower | 3980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p16_tower | 4013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p16_tower | 5013 | `sfx_hit` | 0.30 | -29.2 | -28.0 |
| p16_tower | 5213 | `sfx_hit` | 0.22 | -33.1 | -27.6 |
| p16_thornwall | 713 | `sfx_hit` | 0.38 | -34.7 | -34.7 |
| p16_thornwall | 1013 | `sfx_hit` | 0.30 | -30.2 | -30.2 |
| p16_thornwall | 1980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p16_thornwall | 2013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p16_thornwall | 2213 | `sfx_thornwall_swing_hit` | 0.36 | -31.7 | -31.4 |
| p16_thornwall | 3013 | `sfx_hit` | 0.30 | -29.2 | -29.2 |
| p16_thornwall | 3713 | `sfx_thornwall_swing_hit` | 0.36 | -31.7 | -31.7 |
| p16_thornwall | 3980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p16_thornwall | 4013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p16_thornwall | 5013 | `sfx_hit` | 0.30 | -29.2 | -24.3 |
| p16_thornwall | 5213 | `sfx_hit` | 0.22 | -33.1 | -24.1 |
| p16_magpie | 1013 | `sfx_hit` | 0.30 | -30.2 | -30.2 |
| p16_magpie | 1980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p16_magpie | 2013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p16_magpie | 2213 | `sfx_magpie_swing_hit` | 0.36 | -31.7 | -31.6 |
| p16_magpie | 3013 | `sfx_hit` | 0.30 | -29.2 | -29.2 |
| p16_magpie | 3713 | `sfx_magpie_swing_hit` | 0.36 | -31.7 | -31.7 |
| p16_magpie | 3980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p16_magpie | 4013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p16_magpie | 5013 | `sfx_hit` | 0.30 | -29.2 | -28.0 |
| p16_magpie | 5213 | `sfx_hit` | 0.22 | -33.1 | -27.6 |
| p16_quiver | 1013 | `sfx_hit` | 0.30 | -30.2 | -30.2 |
| p16_quiver | 1813 | `sfx_quiver_loose` | 0.18 | -37.6 | -26.3 |
| p16_quiver | 1980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -26.3 |
| p16_quiver | 2013 | `sfx_quiver_swing_hit` | 0.36 | -31.6 | -26.3 |
| p16_quiver | 2013 | `sfx_hit` | 0.30 | -29.2 | -26.3 |
| p16_quiver | 3013 | `sfx_hit` | 0.30 | -29.2 | -29.2 |
| p16_quiver | 3113 | `sfx_quiver_loose` | 0.18 | -37.7 | -31.8 |
| p16_quiver | 3313 | `sfx_quiver_swing_hit` | 0.36 | -31.8 | -31.8 |
| p16_quiver | 3980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p16_quiver | 4013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p16_quiver | 4613 | `sfx_hit` | 0.22 | -32.1 | -27.4 |
| p16_quiver | 5013 | `sfx_hit` | 0.30 | -30.3 | -30.3 |
| p16_chorus | 1013 | `sfx_hit` | 0.30 | -30.2 | -30.2 |
| p16_chorus | 1980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.6 |
| p16_chorus | 2013 | `sfx_chorus_toss` | 0.16 | -37.7 | -27.6 |
| p16_chorus | 2013 | `sfx_hit` | 0.30 | -29.2 | -27.6 |
| p16_chorus | 2213 | `sfx_chorus_swing_hit` | 0.36 | -31.7 | -31.6 |
| p16_chorus | 3013 | `sfx_hit` | 0.30 | -29.2 | -29.2 |
| p16_chorus | 3513 | `sfx_chorus_toss` | 0.16 | -37.7 | -31.7 |
| p16_chorus | 3713 | `sfx_chorus_swing_hit` | 0.36 | -31.7 | -31.7 |
| p16_chorus | 3980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p16_chorus | 4013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p16_chorus | 5013 | `sfx_hit` | 0.30 | -29.2 | -28.0 |
| p16_chorus | 5213 | `sfx_hit` | 0.22 | -33.1 | -27.6 |
| p16_unbroken | 1013 | `sfx_hit` | 0.30 | -30.2 | -30.2 |
| p16_unbroken | 1980 | `sfx_unbroken_toss` | 0.16 | -37.7 | -16.7 |
| p16_unbroken | 1980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -16.7 |
| p16_unbroken | 2013 | `sfx_hit` | 0.30 | -29.3 | -16.7 |
| p16_unbroken | 2213 | `sfx_unbroken_swing_hit` | 0.36 | -32.4 | -19.1 |
| p16_unbroken | 3013 | `sfx_hit` | 0.30 | -29.3 | -19.6 |
| p16_unbroken | 3480 | `sfx_unbroken_toss` | 0.16 | -37.7 | -33.1 |
| p16_unbroken | 3713 | `sfx_unbroken_swing_hit` | 0.36 | -32.4 | -32.4 |
| p16_unbroken | 3980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -16.7 |
| p16_unbroken | 4013 | `sfx_hit` | 0.30 | -29.2 | -16.7 |
| p16_unbroken | 5013 | `sfx_hit` | 0.30 | -29.3 | -19.6 |
| p16_unbroken | 5213 | `sfx_hit` | 0.22 | -33.1 | -27.6 |
| p16_oathbound | 1013 | `sfx_hit` | 0.30 | -30.2 | -30.2 |
| p16_oathbound | 1980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p16_oathbound | 2013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p16_oathbound | 2213 | `sfx_oathbound_swing_hit` | 0.36 | -31.7 | -31.6 |
| p16_oathbound | 3013 | `sfx_hit` | 0.30 | -29.2 | -29.2 |
| p16_oathbound | 3713 | `sfx_oathbound_swing_hit` | 0.36 | -31.7 | -31.7 |
| p16_oathbound | 3980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p16_oathbound | 4013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p16_oathbound | 5013 | `sfx_hit` | 0.30 | -29.2 | -28.0 |
| p16_oathbound | 5213 | `sfx_hit` | 0.22 | -33.1 | -27.6 |

## Per voice (own K50, dB: loudest / quietest ask)

| voice | asks | loudest | quietest |
|---|---|---|---|
| `sfx_seeker_swing_hit` | 2 | -31.7 | -31.7 |
| `sfx_anvil_swing_hit` | 2 | -31.7 | -31.7 |
| `sfx_metronome_swing_hit` | 2 | -31.8 | -31.9 |
| `sfx_tower_swing_hit` | 2 | -31.7 | -31.7 |
| `sfx_thornwall_swing_hit` | 2 | -31.7 | -31.7 |
| `sfx_magpie_swing_hit` | 2 | -31.7 | -31.7 |
| `sfx_quiver_swing_hit` | 2 | -31.6 | -31.8 |
| `sfx_chorus_swing_hit` | 2 | -31.7 | -31.7 |
| `sfx_unbroken_swing_hit` | 2 | -32.4 | -32.4 |
| `sfx_oathbound_swing_hit` | 2 | -31.7 | -31.7 |
| `sfx_quiver_loose` | 2 | -37.6 | -37.7 |
| `sfx_chorus_toss` | 2 | -37.7 | -37.7 |
| `sfx_unbroken_toss` | 2 | -37.7 | -37.7 |
| `sfx_seeker_spray_hit` | 1 | -28.0 | -28.0 |
| `sfx_seeker_press_tick` | 20 | -31.6 | -37.6 |
| `sfx_hit` | 60 | -29.2 | -36.1 |

The loudest identity swing hit in any take sits 3.6 dB under the quietest SPRAY / HARD HANDS contact heard in the Seeker's fight.
