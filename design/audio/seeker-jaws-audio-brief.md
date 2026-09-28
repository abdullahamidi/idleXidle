# Audio brief: the Seeker's JAWS (the reaction's one cue)

> **Status (2026-09-28): the picture is APPROVED by the owner; the cue is `sfx_seeker_jaws_bite`, a BITE** (the owner:
> "make the sound effect more of a bite / being-bitten sound, and we approve this skill completely"). Candidate A
> (CHOMP) is in the game; B (CRUNCH) and C (HEAVY) wait in `production/qa/evidence/jaws-bite-sound/candidates/` for the
> owner's ear (review page https://claude.ai/artifact/UyNseCpfkpYrHioX4P5ucX). The thorn impact `sfx_seeker_jaws_fangs`, the trap's clack and the piranha's
> chomp are history and no longer played.

## The bite (`_bite`, `jaws_bite`), in a mouth's order

| Layer | What | Why |
|---|---|---|
| the teeth MEET | two enamel clacks 4 ms apart (a 1.4 ms click at 3 kHz, a resonance at 2.6 kHz ×1, 1.52, 2.31 dying inside ~4 ms, a small 1.3 kHz body) | the upper row, then the lower: two hard small contacts, never steel |
| they SINK IN | five saturated crunch grains over ~40 ms (380–2400 Hz), each quieter than the last | teeth going through hide |
| the wet give | a band falling 1500 → 420 Hz, ~25 ms | flesh, not wood |
| the JAW'S WEIGHT | a short thud 165 → 88 Hz, ~30 ms | the force of a jaw, not a skull |
| the Shadow | a dark tone squeezed 700 → 120 Hz, far under everything | the supernatural, a whisper |

One bite: every layer starts inside the first ~50 ms. 180 ms, peak −9.4 dB, RMS −23.3 dB, centroid 1476 Hz, decay20
74 ms: fuller than the thorn impact (−30.3 dB RMS), lighter than the HARD HANDS blow (−21.1 dB).

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
- 2026-09-28: the frontal smoke-teeth picture APPROVED; the owner asked for "more of a bite sound": the cue is
  `sfx_seeker_jaws_bite` (teeth meet, sink in, the jaw's weight), three candidates.
