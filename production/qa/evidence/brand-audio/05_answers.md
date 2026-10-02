# BRAND audio: the owner's seven review questions, per candidate (integrator, 2026-10-02)

Evidence: `07_timing.md` (the live trace: when each cue fires), `06_mix.md` (levels in the fight's own soundtrack, after the -1 dB tuning), the films 01-04 in this folder. All three candidates are the same timing, keys and loudness by construction (mastered to the same K-weighted targets, one volume multiplier): they differ in CHARACTER only, so the answers below turn on shape and spectrum, not level. (Analysis scripts: `build/tmp/brandaudio/listen.py`, `row.py`, scratch, not committed.)

Key measurements (attack = 10 -> 90 % of the take-hold's level; for contrast SPRAY 1.9 ms, HARD HANDS 2.8, PRESS 17.7,
JAWS 34.5):

| | A SUBTLE / INTERNAL | B SUPERNATURAL | C ORGANIC / ASH |
|---|---|---|---|
| apply attack / crest at the take-hold | 97 ms / 8.4 dB | 56 ms / 7.5 dB | 100 ms / 9.1 dB |
| apply centroid (10th..90th pct) | 770..1790 Hz | 1500..4990 Hz | 3140..6680 Hz |
| deepen: energy in the travel before the take-hold | 25 % | 34 % (a swell right up to it, -7 dB 50 ms before) | 5 % (the travel nearly silent) |
| deepen_deep vs deepen: grit / tail +300 ms | 13 -> 22.5 /s, -28 -> -24 dB | 21 -> 26.5 /s, -31 -> -21 dB | 21 -> 29 /s, -28 -> -21 dB |
| leave centroid over its length | 960 -> 1700 -> 820 Hz (falls: inward) | 4800-5600 -> 2100 -> 4300 Hz (air hiss, 22 % > 4 kHz) | 4100 -> 2500 -> 6400 Hz (rises: outward, 28 % > 4 kHz) |
| awaken centroid after the take-hold | 1940-2300 -> 820-940 Hz (settles) | 4000-5500 Hz (stays bright) | 5700-6650 Hz (keeps rising) |
| energy > 4 kHz (apply / leave / awaken) | 0.00 / 0.00 / 0.01 | 0.03 / 0.22 / 0.02 | 0.15 / 0.28 / 0.12 |
| SPRAWL row: max vs its apply; > 4 kHz share | +0.3 dB; 2.7 % | 0.0 dB; 12.9 % | +0.2 dB; 11.6 % |
| family: 1/3-octave spectrum correlation, six main cues (mean / min) | **0.94 / 0.82** | 0.81 / 0.50 | 0.88 / 0.60 |
| loudest BRAND event in the fight (K50) | -37.2 | -36.6 | -37.1 |

## A - SUBTLE / INTERNAL

1. **Apply = the curse entering, not an impact?** YES, most clearly. A 97 ms swell (suction / breath lead-in) into a
   low, dark take-hold whose centroid never leaves 0.8-1.8 kHz: it sounds *inside* the body. Nothing in it resembles
   the 2-3 ms attacks of SPRAY / HARD HANDS.
2. **Deepen = the curse spreading further?** YES. A quarter of its energy is the under-skin crawl BEFORE the take-hold
   (the picture's 170 ms travel is audible as a low scrape-hiss moving into the second hold); deepen_deep adds the extra
   grit layer and a 4 dB longer tail at the same level, so the third depth reads as "more of it", not "louder".
3. **SPRAWL = propagation?** YES. One apply, then three short sub-heavy pulses (71 % < 150 Hz, 250 ms, no tail at +300)
   stepping down -39.9 / -40.6 / -41.8 dB in the fight, 40 ms apart, panned along the row: a phrase moving away.
4. **Transfer = migration, not a projectile?** YES. Silence between the leave and the awaken (no travel cue at all, by
   design and verified live); the leave's centroid FALLS at its end (an inward collapse, not a whoosh), the awaken is an
   inhale that settles low after its hold. Nothing sweeps across the stereo field or in pitch.
5. **Subordinate to SPRAY / HARD HANDS / JAWS / PRESS?** YES, and the most so perceptually: -37.2 at its loudest in the
   fight (5.6 dB under PRESS's full tick, 9+ under SPRAY), and it lives below ~2 kHz, under the presence band where the
   action contacts, the JAWS crunch and the PRESS crack carry their detail, so it cannot mask them.
6. **A full cursed row not noisy?** YES. The row is +0.3 dB over its own apply and gone 500 ms after the last landing;
   only 2.7 % of its energy above 4 kHz (no hiss build-up); the grit it has is quiet low-presence texture.
7. **One coherent family?** YES, the most coherent: the six main cues' spectra correlate 0.94 on average (min 0.82):
   the same low internal resonance and dry corruption layer audibly recur in every file; the ash accent (a dry
   crumble in the mids) is the deliberate exception, an accent, not a member.

Risk: the most restrained of the three; on small speakers the low-mid take-holds lose some body (the in-game ear test
on real hardware should check the deepen is still noticed at -36).

## B - SUPERNATURAL

1. **Apply = entering, not impact?** MOSTLY. A reversed swell leads in, but the fastest attack of the three (56 ms) and a
   bright spectral tail (centroid climbing to ~5 kHz) make it read as a *magic event arriving* more than a curse
   seeping in.
2. **Deepen = spreading further?** PARTLY. 34 % of the energy is a swell that peaks just before the take-hold (-7 dB at
   50 ms before): a riser into a hit; it says "something is coming" more than "it crawls further". deepen_deep's beating
   resonance and longer tail are the best "deeper" of the three.
3. **SPRAWL = propagation?** YES: the most sub-heavy infects (83 % < 150 Hz) with a thin high shimmer; they step down
   cleanly.
4. **Transfer = migration, not a projectile?** RISK. The leave carries 22 % air above 4 kHz with a centroid that dips and
   climbs back to 4 kHz (an airy exhale that can read as a release / whoosh outward), and the awaken stays bright after
   its hold. No travel cue, so it is not a projectile, but the shimmer pair edges toward "spell cast and received".
5. **Subordinate?** YES by level (-36.6 loudest, 5.0 under PRESS), but its shimmer sits at 4-5 kHz, the band where
   SPRAY's hit and PRESS's crack live: it is the candidate most likely to draw the ear in a busy moment.
6. **A full row not noisy?** YES by level (0.0 dB over its apply), but 12.9 % of the row's energy is above 4 kHz: a
   faint hiss bed over the row.
7. **One coherent family?** LEAST: main cues correlate 0.81 (min 0.50); the bright leave/apply/awaken and the
   sub-heavy deepen/infect read as two layers rather than one material.

## C - ORGANIC / ASH

1. **Apply = entering, not impact?** YES in envelope (100 ms swell), but its tail is dry crackle and ash (centroid to
   6.7 kHz, 15 % > 4 kHz): it reads as the body's MATERIAL burning more than the curse entering.
2. **Deepen = spreading further?** WEAKEST: the travel is nearly silent (5 % of the energy before the take-hold), so the
   deepen is heard as a second hit, not as a spread; deepen_deep's added crackle does say "worse".
3. **SPRAWL = propagation?** YES: short crackling pulses stepping down; the most "physical" of the three.
4. **Transfer = migration?** PARTLY. No travel cue, but the leave's centroid RISES to 6.4 kHz over its tail (an ash
   exhale brightening outward) and the awaken keeps rising after its hold: the collapse is less "inward".
5. **Subordinate?** YES by level (-37.1 loudest), but its crackle (28 % > 4 kHz in the leave) shares the band of PRESS's
   brittle defence crack and JAWS' crunch: the two families could blur on a shared tick or a shared kill.
6. **A full row not noisy?** YES by level (+0.2 dB), 11.6 % above 4 kHz: a crackle bed, drier than B's hiss.
7. **One coherent family?** YES (0.88, min 0.60): the crackle / ash layer ties the cues together, but its accent (the
   ash file) is the same material as the main cues, so the Ash-Burn accent adds less contrast on top.

## Recommendation: **A - SUBTLE / INTERNAL**

A is the only candidate that answers all seven questions YES without a caveat. It is the most *internal* (everything
under ~2 kHz, an apply that swells in rather than arrives, a leave that collapses inward, an awaken that settles), the
most subordinate where it matters (it leaves the 2-6 kHz presence band to SPRAY, HARD HANDS, JAWS and PRESS, whose
transients carry the combat read), the quietest SPRAWL row, and by far the most coherent family. B is the most "magic"
and has the best deepen_deep, but its shimmer competes with SPRAY / PRESS and its transfer edges toward a cast; C is the
most physical but blurs with PRESS's crack and JAWS' crunch and its deepen does not spread. A's risk (restraint on
small speakers) is a hardware ear test, not a design fault.

**Installed:** A's seven files in `assets/audio/combat/sfx_seeker_brand_*.wav` (byte-identical to
`tools/asset-pipeline/audio_history/brand_curse/A/`); B and C only in `audio_history/brand_curse/`. NOT approved, no
bytes pinned: the owner chooses.
