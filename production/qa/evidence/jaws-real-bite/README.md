# JAWS real bite: the review package (ADR-011, 2026-09-28)

Review page (Turkish): https://claude.ai/artifact/6tP43HjDMCtfKGWeHzHCRu

**APPROVED (2026-09-28).** The owner: "A is perfect, exactly what I wanted. We can close JAWS with this sound effect and mark it as a gold standard." Candidate A is the game cue and the gold-standard reaction
sound; JAWS is ACCEPTED and closed. B and C were not chosen.

**The owner** on the three synthesised bites (https://claude.ai/artifact/UyNseCpfkpYrHioX4P5ucX), in Turkish: "Hayır, hiçbirini beğenmedim hepsi benzer
sesler. Benim dediğim tam bir ısırma. LoL oyunundaki Trundle karakterinin Q skilli ile rakibi ısırdığında çıkan ses
efekti benim isteğimin tam karşılığı. Buna benzer bir ses olması lazım. Gerçek bir ısırık." In English: no, I liked none
of them, they are all similar. I meant a real bite: the sound when Trundle (League of Legends) bites an enemy with his Q
is exactly what I want. It must be similar. A real bite.

**The reference, measured (analysis only).** Riot's Trundle Q OnHit clips were downloaded to the session scratchpad and
analysed there; they are NOT in the repository, were never sampled, resampled or used as a template source, and are not
on the review page. What they are: one dense block of ~480-560 ms. (A) a tear/crunch, 0-150 ms: broadband, hard onset,
chopped into 4-5 chewing bursts 28-40 ms apart, centroid ~5.5 -> 3.7 kHz; (B) the weight, ~150-330 ms: a sub dropping
~170 -> 60 Hz in 40-60 ms and settling at 35-45 Hz, the loudest band, with the crunch still grinding under it; (C) 1-3
wet squelch resonances at 800-1100 Hz; (D) a tail of isolated cartilage ticks to ~550 ms. ~47 % of the energy below
150 Hz, ~32 % above 2 kHz, peak-to-RMS over the loudest 300 ms 8-9.3 dB. The rejected synthesised bite had 77 % below
150 Hz, 1.6 % above 2 kHz, 180 ms and 14 dB: a thud, not a bite.

**What is in the build.** `assets/audio/combat/sfx_seeker_jaws_bite.wav` is now candidate A, built by
`tools/asset-pipeline/make_jaws_bite.py` from REAL recordings: 14 short excerpts of CC0 1.0 foley (Freesound,
OpenGameArt; each licence checked on the sound's own page) in `tools/asset-pipeline/foley/jaws_bite/` with
`SOURCES.md`: teeth snaps, cabbage / pepper / rice-cake / nut / bone crunches, flesh bites, a gore crunch, a heart
squelch, a meat thud, a watermelon bite. Only the weight (a pitch-dropping sub, alive: jitter, shimmer, one re-strike
of the same oscillator) and the wet resonances (a noise-excited formant) are synthesised. Two buses (crunch and weight)
so the weight never squashes the crunch; the weight's gain is solved on the finished cue for 47 % below 150 Hz; a 4x
oversampled tanh for the density; nothing above 16.5 kHz; the house peak on the TRUE peak.

| | reference | A CHOMP (game) | B BONE | C JUICY | rejected synth |
|---|---|---|---|---|---|
| length (ms) | 480-560 | 580 | 540 | 600 | 180 |
| below 150 Hz | 47 % | 47 % | 47 % | 47 % | 77 % |
| above 2 kHz | ~32 % | 31 % | 31 % | 29 % | 1.6 % |
| peak-to-RMS 300 ms | 8-9.3 dB | 8.8 | 9.8 | 9.6 | 14 |

A sits within ±5 dB of the reference per band and per 50 ms window almost everywhere to 350 ms, and its tail within
±1 dB in total. B is the tight variation (the weight at ~65 ms, shorter); C the soft-ramp, wettest one.

**Checked by measurement** (nobody can listen): a two-lens QA workflow (technical; does it read as a meaty bite) found
and the build fixed: a 1x tanh making 17-21 kHz fizz, aliasing and +2.5..+3.9 dB true-peak overshoot; subs cut dead at
their end; splice clicks; the same bone transient played three times in B; a pure sine glide (an "808"); an empty
150 Hz-2 kHz after ~330 ms; ticks on digital silence; sine blips; a 30 ms dead onset in C; a teeth snap 22 ms late.

**Also.** The rejected cues `sfx_seeker_jaws_snap` / `_chomp` / `_fangs` moved from `assets/audio/combat/` to
`tools/asset-pipeline/audio_history/` (kept, never played; the asset gate flagged them as orphans). The synthesised
candidates stay in `production/qa/evidence/jaws-bite-sound/candidates/`. Unchanged: the picture, the cue on the snap, the
volume 0.42 (about the HARD HANDS blow's loudness), the 0.45 duck under an action. 808 game + 1888 Core tests; the asset
gate is clean.

| file | what |
|---|---|
| 01_before_synth_bite_sound.mp4 | the rejected synthesised bite |
| 02_A_chomp_real_sound.mp4 | A, in the game |
| 03_B_bone_real_sound.mp4 | B |
| 04_C_juicy_real_sound.mp4 | C |
| 05/06/07 | A during SPRAY, during HARD HANDS, repeated at fast TEMPO |
| *.wav, candidates/*.wav | the cues on their own |

Regenerate: `PYTHONUTF8=1 python tools/asset-pipeline/make_jaws_bite.py`, then
`PYTHONUTF8=1 python tools/asset-pipeline/jaws_real_bite_evidence.py production/qa/evidence/jaws-real-bite` (films from
`build/shots/jaws/bitesfx`, the sound mixed from the trace).
