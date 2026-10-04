# The archetype cues in the fight's own mix (P1.5)

film_audio.py's render of each take's sound asks (sound_throttle.json applied). 'own' = the voice's share (the mix minus
the mix with that voice silenced); 'moment' = the whole mix. Loudest 50 ms K-weighted over the 250 ms after the ask, dB.

| take | trace ms | voice | asked vol | own K50 | moment K50 |
|---|---|---|---|---|---|
| p15_seeker | 1013 | `sfx_hit` | 0.30 | -30.2 | -30.2 |
| p15_seeker | 1980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p15_seeker | 2013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p15_seeker | 2213 | `sfx_blade_hit` | 0.36 | -31.7 | -31.6 |
| p15_seeker | 3013 | `sfx_hit` | 0.30 | -29.2 | -29.2 |
| p15_seeker | 3713 | `sfx_blade_hit` | 0.36 | -31.7 | -31.7 |
| p15_seeker | 3980 | `sfx_seeker_press_tick` | 0.14 | -37.6 | -29.0 |
| p15_seeker | 4013 | `sfx_hit` | 0.30 | -29.2 | -29.0 |
| p15_seeker | 5013 | `sfx_hit` | 0.14 | -36.1 | -27.4 |
| p15_seeker | 5213 | `sfx_seeker_spray_hit` | 0.50 | -28.0 | -27.4 |
| p15_quiver | 1013 | `sfx_hit` | 0.30 | -30.2 | -30.2 |
| p15_quiver | 1813 | `sfx_throw_release` | 0.18 | -37.1 | -26.1 |
| p15_quiver | 1980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -26.1 |
| p15_quiver | 2013 | `sfx_blade_hit` | 0.36 | -31.7 | -26.1 |
| p15_quiver | 2013 | `sfx_hit` | 0.30 | -29.3 | -26.1 |
| p15_quiver | 3013 | `sfx_hit` | 0.30 | -29.2 | -29.2 |
| p15_quiver | 3113 | `sfx_throw_release` | 0.18 | -37.2 | -31.8 |
| p15_quiver | 3313 | `sfx_blade_hit` | 0.36 | -31.8 | -31.8 |
| p15_quiver | 3980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p15_quiver | 4013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p15_quiver | 4613 | `sfx_hit` | 0.22 | -32.1 | -27.4 |
| p15_quiver | 5013 | `sfx_hit` | 0.30 | -30.3 | -30.3 |
| p15_anvil | 1013 | `sfx_hit` | 0.30 | -30.2 | -30.2 |
| p15_anvil | 1980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p15_anvil | 2013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p15_anvil | 2213 | `sfx_fist_hit` | 0.36 | -31.7 | -31.5 |
| p15_anvil | 3013 | `sfx_hit` | 0.30 | -29.2 | -29.2 |
| p15_anvil | 3713 | `sfx_fist_hit` | 0.36 | -31.8 | -31.8 |
| p15_anvil | 3980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p15_anvil | 4013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p15_anvil | 5013 | `sfx_hit` | 0.30 | -29.2 | -28.0 |
| p15_anvil | 5213 | `sfx_hit` | 0.22 | -33.1 | -27.6 |
| p15_tower | 1013 | `sfx_hit` | 0.30 | -30.2 | -30.2 |
| p15_tower | 1980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p15_tower | 2013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p15_tower | 2213 | `sfx_stone_hit` | 0.36 | -31.7 | -31.5 |
| p15_tower | 3013 | `sfx_hit` | 0.30 | -29.2 | -29.2 |
| p15_tower | 3713 | `sfx_stone_hit` | 0.36 | -31.7 | -31.7 |
| p15_tower | 3980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.8 |
| p15_tower | 4013 | `sfx_hit` | 0.30 | -29.2 | -27.8 |
| p15_tower | 5013 | `sfx_hit` | 0.30 | -29.2 | -28.0 |
| p15_tower | 5213 | `sfx_hit` | 0.22 | -33.1 | -27.6 |
| p15_thornwall | 713 | `sfx_hit` | 0.38 | -32.7 | -32.7 |
| p15_thornwall | 1013 | `sfx_hit` | 0.30 | -30.3 | -30.3 |
| p15_thornwall | 1980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -28.0 |
| p15_thornwall | 2013 | `sfx_hit` | 0.30 | -29.2 | -28.0 |
| p15_thornwall | 2213 | `sfx_wood_hit` | 0.36 | -31.7 | -31.7 |
| p15_thornwall | 3013 | `sfx_hit` | 0.30 | -29.2 | -29.2 |
| p15_thornwall | 3713 | `sfx_wood_hit` | 0.36 | -31.7 | -31.7 |
| p15_thornwall | 3980 | `sfx_seeker_press_tick` | 0.28 | -31.6 | -27.9 |
| p15_thornwall | 4013 | `sfx_hit` | 0.30 | -29.2 | -27.9 |
| p15_thornwall | 5013 | `sfx_hit` | 0.30 | -29.2 | -24.3 |
| p15_thornwall | 5213 | `sfx_hit` | 0.22 | -33.1 | -24.1 |

## Per voice (own K50, dB: loudest / quietest ask)

| voice | asks | loudest | quietest |
|---|---|---|---|
| `sfx_blade_hit` | 4 | -31.7 | -31.8 |
| `sfx_fist_hit` | 2 | -31.7 | -31.8 |
| `sfx_stone_hit` | 2 | -31.7 | -31.7 |
| `sfx_wood_hit` | 2 | -31.7 | -31.7 |
| `sfx_throw_release` | 2 | -37.1 | -37.2 |
| `sfx_seeker_spray_hit` | 1 | -28.0 | -28.0 |
| `sfx_seeker_press_tick` | 10 | -31.6 | -37.6 |
| `sfx_hit` | 30 | -29.2 | -36.1 |

The loudest archetype hit in any take sits 3.6 dB under the quietest SPRAY / HARD HANDS contact heard in the Seeker's fight.
