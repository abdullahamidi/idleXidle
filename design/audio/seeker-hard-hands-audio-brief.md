# Audio brief: the Seeker's HARD HANDS (the melee action's two cues)

> **Status (2026-09-25): CANDIDATES, not yet heard by the owner.** Built by `tools/asset-pipeline/make_action_sfx.py`
> with the same layered kit as SPRAY's approved cues and the fight's shipped ones. They are measured and wired.
> Only an ear can say they sound like a fist, so they stay candidates until the owner listens. This brief is the
> contract a revision must keep: if a cue is reported as cheap, thin, boomy or tiring, the SOUND is revised
> against it, and the action's timing stays.

## What the player hears (the phrase)

HARD HANDS is ONE action heard as ONE phrase: the leap, then the blow.

```
COMMIT (the leap) ──── 120 ms: the body crosses to the creature ──── CONTACT
beat − 120 ms                                                        beat
```

| Moment | Cue | Game volume | Pan | Plays when |
|---|---|---|---|---|
| Commit | `sfx_seeker_hard_hands_commit` | 0.34 | ±0.3 × the fist's x | the frame the Seeker leaps (the commit marker, beat − 120 ms) |
| Contact | `sfx_seeker_hard_hands_hit` | 0.55 | ±0.3 × the fist's x | on the beat, the frame the fist lands |

- **The mix:** as SPRAY: from the commit until 160 ms after the contact every other one-shot plays at 45 %.
  The generic `sfx_hit` and hit puff for this blow are replaced outright (the same blow). The cast's generic
  `sfx_cast` breath does not play.
- The engine adds ±0.04 (commit) and ±0.05 (contact) octave of random pitch per play and the SFX master (0.8).
- Fallback chain: commit → `sfx_fist_commit` → `sfx_cast`; contact → `sfx_fist_hit` → `sfx_hit`. SPRAY's cues are
  never in it: a fist is not a knife.

## The two cues

### `sfx_seeker_hard_hands_commit`: a BODY leaving the ground

What it depicts: a boot scuffing off the ground, the cloak and sleeve going over, and a whole body and an
overhand arm cutting the air toward the creature.

- **Must:** a small scuff and a little low weight at 0 ms (the push-off). Then a DARK air band (centroid under
  ~2 kHz, well below SPRAY's 3.9 kHz release) that SWELLS toward the blow. It peaks ~10 ms before the contact,
  120 ms after the commit, and the hit's own transient cuts it.
- **Must not:** be a blade whoosh or carry steel (nothing is drawn or thrown). No magic riser: the Source colour is
  in the picture. Not louder than the hit: it is the lead-in.
- Candidate (2026-09-25): 200 ms file, peak −12.4 dBFS, centroid 1745 Hz, −20 dB at 137 ms. Its 10 ms RMS
  envelope: −17 dB at the scuff, dipping to −33, swelling to −21 at 100 ms, gone by 190 ms.

### `sfx_seeker_hard_hands_hit`: a FIST landing, with weight

What it depicts: knuckles slapping into hide, a dry knock of the bone under it, and the weight of a whole body
behind the blow.

- **Must:** a knuckle transient (a click and a skin-slap band around 1.4 kHz), a short dry knock (~620 Hz, high Q,
  13 ms: the bone), a little hide texture, then the weight: a dull thud with a pitch drop (124 → 58 Hz) and a
  ~150 Hz body. Heavier than SPRAY's hit (centroid 1530 Hz), and tighter than the basic swing's `sfx_hit` (a dull
  thud, ~330 Hz, −20 dB at ~190 ms): a string of them in a fight must not tire.
- **Must not:** ring (no tonal tail), boom (no sub held past ~100 ms), or chop: every layer runs to the file's end
  and decays there. The first candidate cut its thud at 220 ms, a 22 dB step in 10 ms at −42 dBFS.
- Candidate (2026-09-25): 260 ms file, peak −8.2 dBFS, centroid 655 Hz, −20 dB at 118 ms; its 10 ms RMS falls
  smoothly to −59 dB, no step.

## Delivery format

As SPRAY's brief (`seeker-spray-audio-brief.md`): 16-bit PCM WAV, mono, 44 100 Hz, peak between −12 and −8 dBFS
(the hit sits at −8.2), no leading silence,
exact lowercase names in `assets/audio/combat/`.
