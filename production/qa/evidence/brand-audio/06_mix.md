# BRAND audio: the mix in the fight (integrator, 2026-10-02)

## Method

`build/tmp/brandaudio/mix.py`: `film_audio.py`'s own renderer (imported: `parse` / `render`, with `CUE_FILES` = the
`--cue KEY=PATH` override) on the trimmed trace takes of `timing.md` (no frames needed: the soundtrack is placed on the
trace clock). For each take and candidate the soundtrack is rendered with the candidate's seven cues and with all seven
silenced (`_silent.wav`); the references are measured the same way, each silenced in turn. Every BRAND event is also
rendered ALONE (its own cue + the ash accent riding on it) so overlapping events do not add into each other's figure.
Loudness = K-weighted (BS.1770 pre-filter, `make_press_tick.k_weight`) loudest 50 ms of (L+R)/2, dBFS, at the game's
gain (vol x duck x 0.8, the throttle, pan and pitch as `film_audio` renders them). Wave 1 of each take.

## The tuning (one multiplier, all candidates, all seven cues)

The part-2 volumes put the loudest possible case, an apply on a host with no Ash-Burn (burn 0, no quiet), at -34.0
against PRESS's tick at -31.3: **2.7 dB, under the required 3**. Every `MarkRecipe` volume x0.891 (-1.0 dB):
apply 0.205 -> **0.183**, deepen 0.183 -> **0.163**, deepen_deep 0.194 -> **0.173**, infect 0.130 -> **0.116**, leave
0.163 -> **0.145**, awaken 0.183 -> **0.163**, ash 0.103 -> **0.092**. `make_brand_cues.py` mirrors it (VOLUME and TARGET
both moved 1 dB, so the files are unchanged to within 0.02 dB; rebuilt deterministically, A re-installed), and
`cues_qa.md` was regenerated. Candidates stay level-matched (same multiplier).

| cue at full (no burn, no quiet) | K50 played | under SPRAY -27.5 | HARD HANDS -23.5 | JAWS -26.0 | PRESS -31.3 |
|---|---|---|---|---|---|
| apply | -35.0 | 7.5 | 11.5 | 9.0 | **3.7** |
| deepen_deep | -35.5 | 8.0 | 12.0 | 9.5 | 4.2 |
| deepen / awaken | -36.0 | 8.5 | 12.5 | 10.0 | 4.7 |
| leave | -37.0 (sfx_enemy_down -26.4: 10.6 under) | 9.5 | 13.5 | 11.0 | 5.7 |
| infect | -39.0 | 11.5 | 15.5 | 13.0 | 7.7 |
| ash accent | -41.0 | 13.5 | 17.5 | 15.0 | 9.7 |

## In the fight (after the tuning)

References in the same takes (their own share, K50): SPRAY -28.0..-28.4, HARD HANDS -23.8..-24.5, JAWS -26.5..-27.4,
PRESS -31.6 (a full tick) .. -36.2 (its own quiet ticks beside an action). The quietest reference voice at full is
PRESS's tick, -31.6 in the fight.

**Loudest BRAND event in any take: A -37.2, B -36.6, C -37.1 dB** (the apply, on this pack's burn-1.0 host: main x0.75 +
the accent) -> **5.0..5.6 dB under PRESS's full tick**, 8.6+ under SPRAY, 10+ under JAWS, 12.6+ under HARD HANDS. The
worst host (burn 0) gives -35.2 for the apply in this render: 3.6 dB under PRESS's fight tick (-31.6), 3.9 under its file level (-31.3). Every other event is quieter
(deepen -37.7..-42.6, awaken -38.2..-44.1, leave -38.2..-46.5, infect -39.1..-41.9).

The BRAND events beside the big voices (the `moment` = whole mix): a leave on HARD HANDS' kill is ducked to x0.45
(-45.4..-46.5 own vs -23.7 moment); on a SPRAY kill the same; the awaken that lands 203 ms after JAWS' snap yields
(-42.8..-43.5 own).

## The SPRAWL row (`audio_sprawl`, `audio_sprawl_winnow_clean`)

The whole row rendered (apply + three infects + four accents, from the apply's start, 1.5 s), loudest 50 ms and the
loudest 50 ms in each 100 ms step:

| | row max | per 100 ms | the row vs its own apply (+accent) |
|---|---|---|---|
| A | -36.9 | -49.6, -37.2, -39.8, -36.9, -37.3, -44.8, -65.9 | +0.3 |
| B | -36.6 | -50.1, -36.6, -39.2, -36.9, -37.4, -43.9, -57.6 | 0.0 |
| C | -36.9 | -48.2, -37.1, -39.6, -36.9, -37.7, -45.8, -71.9 | +0.2 |

Offline re-weighting of the same row (`row.py`): on a burn-0 host the row is exactly the apply's level (-35.2, +0.0
for all three), on burn 0.5 -0.1..+0.2. **No build-up**: the infects (each alone -39.1..-41.9, stepping down 0.7-1.2
dB per victim) sit under the apply's tail, the row is gone 500 ms after the last landing. Brightness of the row (the
"noise" an ear hears): energy above 4 kHz A 2.7 %, B 12.9 %, C 11.6 %; >2 kHz onsets/s A 13.3 (quiet low-presence
grit), B 7.5, C 10.0.

## Every event

The per-event table (every BRAND event alone in every trace take, A / B / C) is in the scratch `build/tmp/brandaudio/mix.md` / `mix_v2.json`; the figures above are its extremes.
