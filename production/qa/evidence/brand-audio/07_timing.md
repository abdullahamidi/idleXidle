# BRAND audio: live timing check (integrator, 2026-10-02)

## How

Seven TRIMMED trace takes, added to `tools/asset-pipeline/films_brand.sh` as `audio_long`, `audio_sprawl`, `audio_etch`,
`audio_etch_clean`, `audio_sprawl_winnow_clean`, `audio_press` and `audio_before` (`RH_MARK_RECIPES=0`): seed 7, T=0.5,
**42 pictures at a stride of 30 frames** (one picture every 0.5 s, ~33 MB a take, deleted right after each run with
`find ... -name '*.png' -delete`; `build/shots` is back to 38 MB). Every frame is still updated and drawn at 60 Hz, so the
trace carries every `mark-cue`, `sound` and `mark-draw` row; the pictures are only the run's clock. Logs kept:
`build/shots/brand/ba/audio_*.log`. Analysis: `build/tmp/brandaudio/timing.py` (rows below are its output).

New live counter: `VoiceMark` now measures its own bytes under the trace (`CueDue`, `CueVolume`, the anchor / body pan
lookups; the bank's and the row's trace strings excluded) and the `mark-draw` row prints them as `voice=`.

Column meaning: *start asked (late)* = the cue's scheduled start (take-hold - file offset) and how far past it the 60 Hz
frame that asked it was; *transient A / B / C* = where each candidate's measured take-hold transient (make_brand_cues
QA) lands against the take-hold the schedule aims at; *picture* = the `mark-draw` front / stage change and the other
sounds asked on that same frame.

## Verdict

| check | result |
|---|---|
| each cue fires once (every wave of every take) | YES: no (kind, moment, slot) twice; cues asked = BRAND `sound` rows in every wave |
| transient on the picture's decisive frame, within one 60 Hz step | YES: worst +13.6 ms (an awaken's ash), mains -1.7..+11.3 ms; apply -1.3/+1.3/+1.2 (A/B/C), deepen +1.4..+1.6, leave -1.5..-1.7, infect -1.5..+8.7, awaken +11.0..+11.3 |
| apply | asked at 1883 (start 1880), take-hold 2060 = the flare's rise end (tick + 60) |
| deepen on the SHOWN step | the `mark-draw` stage steps 1->2 at 4383 and 2->3 at 6383 on the very frame the deepen / deepen_deep is asked (step At 4380 / 6380); take-hold At + 230 |
| leave on the fall | on the fall's frame, the same frame as `sfx_enemy_down` (and HARD HANDS' / SPRAY's contact when they kill); the wave-ending kill's leave (17200 in `audio_long`, 11200 / 14200 / 18700 elsewhere) is asked in the break |
| awaken on the landing | the first frame of the Reform (`mark-draw` front changes and the stage returns), 5533 / 7533 / 8533 / 11533 / 13533 |
| SPRAWL | apply, then ONE infect per victim at its landing 2200 / 2240 / 2280 (asked 2200 / 2250 / 2283), each x0.88 and -0.04 oct below the one before (0.087 / 0.077 / 0.067); no deepen (SPRAWL's depth never crosses a stage floor: 35 %, WINNOW 45..85 %, the picture's stage stays 1, so no deepen is due) |
| never twice on overlaps | arrival + catch-up deepen (etch: awaken 5533, deepen_deep 6383): two different cues; a deepen_deep on a host falling 320 ms later (6383 -> leave 6700): two cues, the deepen not cut; a SPRAWL row fall: one leave per fall frame; wave end: one leave, asked once on the frozen playhead |
| duck / quiet / yield | yield x0.5 near a JAWS snap (awaken 7533 in `audio_long`: 0.082 = 0.163 x 0.5, whole, no burn cut; its ash 0); quiet x0.6 beside an action / PRESS's crush (every `audio_press` apply 0.082 = 0.137 x 0.6); duck alone on a contact kill (leave `duck=0.45`, vol unchanged 0.109) |
| Ash accent | on every take-hold of this pack (its baked burn is 1.0: the main x0.75, the accent at 0.092); omitted when yielding; now FOLLOWS its cue's share (see below) |
| `RH_MARK_RECIPES=0` (`audio_before`) | zero `mark-cue`, zero `mark-wave`, zero `sfx_seeker_brand_*` rows |
| zero allocation | `voice=0` on every `mark-draw` row of every take. `alloc=1128` on ONE frame per new host (the wave's first drawn frame and each landing's first frame): PRE-EXISTING in the approved renderer's draw path (present in the kB9 logs before any audio work), not the voice; left alone (renderer locked) |

## Found and fixed

**The ash accent was louder than the cue it accents.** A deepen's accent starts ~225 ms after the deepen (on the
take-hold); `CueVolume`'s quiet share is decided per frame (`performing`), so in `audio_etch` wave 2 the deepen_deep was
asked quiet (0.078) and its accent full (0.103), and an accent could equally escape a duck its cue had. `MarkVoice` now
records the share x duck each main cue was asked at (`_heard`, re-armed with the cue) and the accent rides it
(divided by its own frame's duck, which the bank applies again; never above its full volume). `CueVolume` gained a
`duck` parameter (`VoiceMark` passes `Sound?.Duck ?? 1f`); test
`test_the_ash_accent_is_heard_at_the_share_its_cue_was_never_louder_than_the_layer_it_accents`.

## Not exercisable live (covered by the unit tests only)

- a multi-depth jump (1 -> 3 in one step): the seeded fight's catch-up is a single 2 -> 3 step;
- a SPRAWL deepen ripple (one cue per tick's ripple over the row's whole span, `MarkVoice.RippleGroups`; never two steps
  of one creature folded): no SPRAWL variation reaches stage 2 (85 % max);
- several CURSED hosts falling on one frame: front mode carries one host; `etch` wave 2 has two falls at 12700 but only one is cursed.

## The cues in the review films (01-04)

Each film is the same seven segments in the same order and crop; the time is the film's own clock (the A film; B, C and
the muted reference are frame-identical). Only the asked cues are listed (vol is the game's ask before the 0.8 SFX master;
`duck=` rows are in the trace). Other voices in the windows: segment 2 ends on SPRAY's release (4950) and the enemy's
strike (5000); segment 5 has JAWS' snap at 7333 (the awaken yields x0.5, its accent silent) and HARD HANDS' commit at
8083; segment 6 is HARD HANDS' kill (the leave ducked x0.45); segment 7 is SPRAY 4950-5250, PRESS's ticks 5983 / 7983.

| seg | take | window (playhead ms) | film time | BRAND cues asked (film time) |
|---|---|---|---|---|
| 1 | `ba_apply` | 1750-3050 | 0.00-1.30 | Apply 0.13 s (playhead 1883, vol 0.137); Ash 0.32 s (2067, 0.092) |
| 2 | `ba_deepen12` | 3800-5150 | 1.30-2.66 | Deepen 1.88 s (4383, 0.122); Ash 2.12 s (4617, 0.092) |
| 3 | `ba_deepen23` | 6100-7480 | 2.66-4.04 | DeepenDeep 2.94 s (6383, 0.130); Ash 3.18 s (6617, 0.092); Leave 3.26 s (6700, 0.109); Ash 3.28 s (6717, 0.092) |
| 4 | `ba_sprawl` | 1750-3050 | 4.04-5.34 | Apply 4.17 s (1883, 0.137); Ash 4.36 s (2067, 0.092); Infect 4.49 / 4.54 / 4.57 s (2200 / 2250 / 2283; 0.087 / 0.077 / 0.067), each with its Ash (0.092 / 0.081 / 0.071) |
| 5 | `ba_transfer` | 6450-8185 | 5.34-7.04 | Leave 5.56 s (6700, 0.109); Ash 5.57 s (6717, 0.092); Awaken 6.39 s (7533, 0.082 = yield x0.5 beside JAWS' snap; its Ash silent) |
| 6 | `ba_death` | 16900-18300 | 7.04-8.52 | Leave 7.17 s (17200, 0.109, duck 0.45 under HARD HANDS' kill); Ash 7.19 s (17217, 0.092, duck 0.45) |
| 7 | `ba_mixed` | 4850-8170 | 8.52-11.82 | Leave 10.35 s (6700, 0.109); Ash 10.37 s (6717, 0.092); Awaken 11.19 s (7533, 0.122); Ash 11.30 s (7650, 0.092) |
