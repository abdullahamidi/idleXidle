# Audio brief: the Seeker's JAWS (the reaction's one cue)

> **Status (2026-09-28): APPROVED, the GOLD STANDARD.** The owner: "A is perfect, exactly what I wanted. We can close JAWS with this sound effect and mark it as a gold standard." The cue is
> `sfx_seeker_jaws_bite`, candidate A (CHOMP), built by `tools/asset-pipeline/make_jaws_bite.py` from CC0 recordings
> (`tools/asset-pipeline/foley/jaws_bite/SOURCES.md`), shaped to the measured structure of the owner's reference
> (Trundle's Q in League of Legends; analysed locally only, never sampled or shipped). HUMAN-APPROVED: its bytes are
> pinned by SHA-256 in `jaws_reaction_test.cs`; a change is a new approval. B (BONE) and C (JUICY) were not chosen and
> stay in `production/qa/evidence/jaws-real-bite/candidates/` as history; the rejected cues are in
> `tools/asset-pipeline/audio_history/` and `production/qa/evidence/jaws-bite-sound/candidates/`. Review page https://claude.ai/artifact/6tP43HjDMCtfKGWeHzHCRu.

## The real bite (`make_jaws_bite.py`, candidate A), in a mouth's order

| Phase | ms | What (real = a CC0 recording) |
|---|---|---|
| teeth + TEAR | 0-200 | a real enamel snap; real cabbage and flesh crunch in five chewing bursts (un-gridded), a crescendo into the weight |
| WEIGHT | ~150-400 | a sub 170 -> 60 -> 35 Hz, alive (jitter, shimmer, one re-strike of the same oscillator); a real meat thud; a real gore and heart low body |
| grind | 150-430 | real pepper and cabbage crunch still grinding under the weight in sparser bursts |
| WET | 35, 140, 235 | noise-excited resonances at 1000, 950, 840 Hz, each falling ~7 %; a real heart squelch |
| TAIL | 300-560 | a real crunch bed (2-16 kHz) decaying under four cartilage ticks |

~580 ms; 47 % of the energy below 150 Hz, 31 % above 2 kHz, peak-to-RMS over the loudest 300 ms 8.8 dB (the reference:
47 %, ~32 %, 8-9.3 dB). Volume 0.42: about the loudness of the HARD HANDS blow.

## What JAWS is, to the ear

A REACTION in the family of thorns-style damage: the enemy bites the Seeker, and large Shadow fangs snap onto the
attacker. Not a machine (no latch, no steel, no chain), not a monster (no roar), not a swarm (no ticks), not a spell (no
long whoosh). The target character is **dark, dry, sharp, short**: energy compression plus a short organic/dry impact,
over in about an eighth of a second. No bone crunch.

## What the player hears (the phrase)

```
BITE (the enemy's thud, t 0) ── +55 ms, the SNAP frame: ONE Shadow bite / thorn impact ── +95 ms: the number (silent)
```

One cue, played on the frame the fangs snap shut (never on the spawn), so the sound lands where the shapes meet. Nothing
after it: no secondary ticks, no tail. It is ~130 ms long and peaks at −10 dB: a frequent Reaction, quiet enough to
repeat every few seconds without tiring.

## The layers (`jaws_fangs`)

| Layer | What | Why |
|---|---|---|
| dry sharp transient | a 1.6 ms click at 2.7 kHz and three inharmonic partials (1.9 kHz ×1, 1.47, 2.23) dying inside ~7 ms | points meeting hide; nothing rings, so it is never steel or a bell |
| dry organic impact | low-passed noise (800 Hz), ~12 ms, and a short 1.1 kHz body | the bite took something: hide, not bone |
| dark body | a damped 300 Hz body, gone in ~25 ms, no pitch drop | weight without a skull thump |
| energy compression | a dark sine squeezed from 900 to 130 Hz over ~45 ms, quiet, under the impact | the fangs closing on what they caught: the Shadow in it |
| a whisper of crackle | a narrow band at 4.2 kHz, ~20 ms, far under everything | the supernatural, kept quiet |

Avoided on purpose: bone crack, metal trap, piranha-swarm ticks, monster roar, long magical whoosh.

## Measurements (make_action_sfx.py)

| cue | ms | peak dB | rms dB | centroid Hz | decay20 ms |
|---|---|---|---|---|---|
| `sfx_seeker_jaws_fangs` | 130 | −9.9 | −30.3 | 2039 | 29 |

Shorter and quieter than the approved SPRAY hit (280 ms, −8.4 dB) and HARD HANDS blow (260 ms, −8.2 dB): a small
gameplay event, a small sound.

## History

- 2026-09-25, identity pass: the crunch-and-thump bite cue was replaced by a dry-steel CLACK (the owner: "better; keep
  it as the baseline" for the mechanical trap).
- 2026-09-25, readable-clamp pass: the clack retimed to the jaws' stop (17 → 51 ms).
- 2026-09-27: the mechanical fantasy rejected; the cue became a small supernatural chomp (three bites with two baked
  ticks, then one bite).
- 2026-09-27: the piranha rejected; the cue is one Shadow bite / thorn impact on the fangs' snap.
- 2026-09-28: the frontal smoke-teeth picture APPROVED; the owner asked for "more of a bite sound": three
  synthesised bites (teeth meet, sink in, the jaw's weight), all rejected ("all similar; a real bite, like Trundle's Q").
- 2026-09-28: the cue is a REAL bite from CC0 foley, shaped to the measured reference; candidates A CHOMP, B BONE, C JUICY.
- 2026-09-28: the owner chose A ("perfect, exactly what I wanted"): APPROVED, the gold-standard reaction sound; JAWS closed.
