#!/usr/bin/env python3
"""Synthesise the AUTHORED action cues (ADR-011) into assets/audio/combat — the Seeker's SPRAY first.

    python tools/asset-pipeline/make_action_sfx.py            # write the cues and print their measurements

The fight's generic cues are a breath (`sfx_cast`) and a thud (`sfx_hit`). An authored action is heard at
its OWN moments — the release, 250 ms before the beat, and the contact, on the beat — and it must sound like
what the picture shows: throwing knives leaving a hand, and blades going into bodies. Built with the SAME
layered primitives as make_battle_sfx.py (click, crunch, body, thump, bell, filtered air), so the new
cues sit in the family the shipped ones do: deterministic noise, measured, bounded.

    sfx_seeker_spray_release  ~0.2 s   SPEED, not weight: a dry cloth flick, a rising air displacement as the fan
                                       leaves, two tiny steel ticks (blades brushing as they separate). No low end.
    sfx_seeker_spray_hit      ~0.28 s  ONE contact for the whole fan: a sharp tip transient, a short dull puncture
                                       body, a little leather crunch, a light weight, a steel glint (not a ring). Brighter
                                       and shorter than the basic swing's thud, so the skill is not heard as a punch.
    sfx_seeker_spray_tick     ~0.1 s   the quiet secondary: one or two of these, a few ms after the hit and panned
                                       to the outer targets, give the fan its width without five equal impacts.

These are CANDIDATES until someone has listened to them in the game: a measurement says a cue is bright,
short and quiet enough; only an ear says it sounds like a knife.
"""
from __future__ import annotations

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import make_battle_sfx as S  # noqa: E402  (the shared synthesis kit)


def spray_release(buf):
    rng = S.Noise(0x5E1E)
    # the cloth/wrist flick: a dry band of noise, a few ms of attack, gone in ~40 ms
    S.add(buf, 0.0, S.envelope(S.biquad(S.noise(rng, 0.06), "bp", 1900.0, 0.9), 0.0015, 0.016), 0.55)
    # the air the fan displaces: a band RISING through the noise as the blades leave
    air = S.sweep_bp(S.noise(rng, 0.19), 950.0, 3300.0, 1.3, 0.055)
    S.add(buf, 0.004, S.envelope(air, 0.009, 0.05), 1.0)
    # the steel: two tiny inharmonic ticks as the blades separate (not a ring, a brush)
    S.bell(buf, 0.013, 3150.0, 0.10, 0.030, partials=(1.0, 1.51, 2.37))
    S.bell(buf, 0.027, 3650.0, 0.07, 0.024, partials=(1.0, 1.51, 2.37))


def spray_hit(buf):
    rng = S.Noise(0x5E17)
    S.add(buf, 0.0, S.click(rng, 0.003, 3200.0), 0.32)                       # the tip arriving
    S.add(buf, 0.0, S.body(rng, 0.10, 720.0, 1.4, 0.022), 0.95)               # the puncture: short, dull
    S.add(buf, 0.0, S.crunch(rng, 0.030, 480.0, 2600.0, 0.006, sat=3.0), 0.34)  # leather / flesh texture
    # a little weight, never bass. Long enough to decay to -41 dB on its own: cut at 80 ms it stopped at -20 dB,
    # a 15 dB step in 10 ms (heard as a chop once the ring stopped covering it)
    S.add(buf, 0.0, S.thump(0.16, 190.0, 118.0, 0.018, 0.034, sat=1.5), 0.36)
    # the blade's steel: a GLINT inside the first ~60 ms, never a ring. The first candidate rang for 250 ms
    # (from 80 ms on, 90%+ of the cue was one pure 2.25 kHz tone at -17..-30 dB): a blade striking metal,
    # not a blade going into a body.
    S.bell(buf, 0.004, 2250.0, 0.10, 0.028, partials=(1.0, 2.41, 3.93))
    S.add(buf, 0.006, S.tail(S.Noise(0x5E18), 0.16, 1900.0, 0.035), 0.10)      # a hint of room, no more


def spray_tick(buf):
    rng = S.Noise(0x5E19)
    S.add(buf, 0.0, S.click(rng, 0.002, 3800.0), 0.40)
    S.add(buf, 0.0, S.body(rng, 0.05, 1150.0, 1.3, 0.011), 0.55)
    S.bell(buf, 0.002, 2900.0, 0.10, 0.035, partials=(1.0, 2.41))


def main() -> int:
    S.make("sfx_seeker_spray_release", S.COMBAT_DIR, 0.20, spray_release, target_peak=0.26, max_ms=220.0, darken=9500.0)
    S.make("sfx_seeker_spray_hit", S.COMBAT_DIR, 0.28, spray_hit, target_peak=0.38, max_ms=300.0, darken=7500.0)
    S.make("sfx_seeker_spray_tick", S.COMBAT_DIR, 0.10, spray_tick, target_peak=0.30, max_ms=120.0, darken=8000.0)
    print(f"{'cue':28} {'ms':>5} {'peak dB':>8} {'rms dB':>7} {'centroid':>9} {'decay20':>8}")
    for name in ("sfx_seeker_spray_release", "sfx_seeker_spray_hit", "sfx_seeker_spray_tick", "sfx_cast", "sfx_hit"):
        m = S.measure(os.path.join(S.COMBAT_DIR, name + ".wav"))
        print(f"{name:28} {m['ms']:5.0f} {m['peak_db']:8.1f} {m['rms_db']:7.1f} {m['centroid']:9.0f} {m['decay20_ms']:8.0f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
