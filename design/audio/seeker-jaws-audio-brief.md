# Audio brief: the Seeker's JAWS (the reaction's one cue)

> **Status (2026-09-27, the SHADOW PIRANHA production direction): the dry-steel trap CLACK (`sfx_seeker_jaws_snap`)
> belonged to the rejected mechanical fantasy and is no longer played. The cue is now `sfx_seeker_jaws_chomp`, a SMALL
> SUPERNATURAL CHOMP for the whole three-bite phrase. It is a candidate until the owner has listened to it.**
> - Built by `tools/asset-pipeline/make_action_sfx.py` (`jaws_chomp`), with the same layered kit as the approved SPRAY
>   and HARD HANDS cues; measured; rendered into the review films from the trace (`film_audio.py`).
> - The old snap's wav stays on disk as history until final approval; nothing plays it.

## What JAWS is, to the ear

A REACTION in the family of thorns-style damage: the enemy bites the Seeker, and several tiny Shadow jaw-heads bite the
attacker back, at once. Not a machine (no latch, no steel, no chain), not a monster (no roar), not a spell (no long
whoosh). Think **CHOMP**: a small, dry, slightly fleshy bite with a hint of Shadow energy, over in a tenth of a second.

## What the player hears (the phrase)

```
BITE (the enemy's thud, t 0) ── the same frame: CHOMP (the main jaw) ── +15, +25 ms: two very quiet ticks (the other jaws)
```

One cue for the whole phrase, played on the first frame that shows the bite, right after the enemy's own thud. The two
secondary ticks are baked into the same file, ~10 dB under the main chomp: three loud identical chomps would read as
three separate hits, and the bank's repeat rule would drop two of them anyway. The chomp is ~140 ms long and peaks at
−10 dB: a frequent Reaction, quiet enough to repeat every few seconds without tiring.

## The layers (`jaws_chomp`)

| Layer | What | Why |
|---|---|---|
| dry short bite / snap | a 1.8 ms click at 3.1 kHz and three inharmonic partials (2.35 kHz ×1, 1.39, 2.11) dying inside ~8 ms | teeth meeting; nothing rings, so it is never steel or a bell |
| leathery transient | low-passed noise (900 Hz), 14 ms | the bite took something: hide, not bone |
| Shadow crackle | a narrow bright band at 4.6 kHz, ~25 ms, well under the snap | the supernatural in it, kept quiet |
| a little weight | a damped 420 Hz body, gone in ~20 ms, no pitch drop | a bite, not a tick; a pitch-dropping thump read as a skull in the old cue |
| two secondary ticks | the same snap material at +15 and +25 ms, at 34 % and 24 % | the second and third jaws, a swarm and not a stamp |

Avoided on purpose: bone crack, metal trap, monster roar, long magical whoosh.

## Measurements (make_action_sfx.py)

| cue | ms | peak dB | rms dB | centroid Hz | decay20 ms |
|---|---|---|---|---|---|
| `sfx_seeker_jaws_chomp` | 140 | −10.5 | −31.6 | 2819 | 30 |

Shorter and quieter than the approved SPRAY hit (280 ms, −8.4 dB) and HARD HANDS blow (260 ms, −8.2 dB): a small
gameplay event, a small sound.

## History

- 2026-09-25, identity pass: the crunch-and-thump bite cue was replaced by a dry-steel CLACK (the owner: "better; keep
  it as the baseline" for the mechanical trap).
- 2026-09-25, readable-clamp pass: the clack retimed to the jaws' stop (17 → 51 ms).
- 2026-09-27: the mechanical fantasy rejected; the cue is a small supernatural chomp.
