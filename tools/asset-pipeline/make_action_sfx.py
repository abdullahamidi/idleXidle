#!/usr/bin/env python3
"""Synthesise the AUTHORED action cues (ADR-011) into assets/audio/combat — the Seeker's SPRAY, HARD HANDS and JAWS.

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

    sfx_seeker_hard_hands_commit ~0.2 s  THE LEAP, 120 ms before the blow: a scuffed push-off, then the whole body
                                       and an overhand arm cutting the air, a DARK swell (a body, not a blade) that
                                       builds into the contact and is cut by it. No steel, nothing bright.
    sfx_seeker_hard_hands_hit    ~0.26 s THE BLOW: a knuckle slap on hide, a short dry knock (bone under it), a
                                       heavy dull thud with a pitch drop and a small hard room. No ring, no tail.
                                       Heavier than the basic swing's thud, but tighter, so ten in a fight do not tire.

    sfx_seeker_jaws_snap         ~0.17 s THE ANSWER to a bite (a Reaction, off the beat), timed to the picture: at 0 ms
                                       the tether fires (a small spring tick and a quiet chain rasp, a lead-in); at 17 ms,
                                       the frame the rigid jaws hit their stop, the CLAMP (a short hard iron clack with a
                                       little weight under it, teeth into hide): the cue's strongest transient; after it
                                       the chain taking the strain (three short link ticks). One cue for the whole
                                       reaction, right after the enemy's bite thud: BITE -> CLACK. No ring, no tail.

The SPRAY and HARD HANDS cues are HUMAN-APPROVED (the owner, 2026-09-25). A measurement says a cue is bright, short and
quiet enough; only an ear says it sounds like a knife (or a fist), so every new cue here is a candidate until it is
listened to (design/audio/seeker-spray-audio-brief.md is the contract the approved ones keep). The JAWS snap is a
CANDIDATE until the owner has heard it.
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


def hard_hands_commit(buf):
    rng = S.Noise(0x4A2D)
    # the push-off: a scuff of boot on ground and a little low weight leaving it
    S.add(buf, 0.0, S.envelope(S.lp2(S.noise(rng, 0.05), 700.0), 0.002, 0.014), 0.45)
    S.add(buf, 0.0, S.thump(0.08, 95.0, 62.0, 0.015, 0.022, sat=1.6), 0.28)
    # the cloak/sleeve as the arm goes over: a short dry flap
    S.add(buf, 0.018, S.envelope(S.biquad(S.noise(rng, 0.05), "bp", 1250.0, 1.1), 0.003, 0.014), 0.30)
    # the body and the arm through the air: a dark band SWELLING toward the blow (it peaks just before the contact,
    # 120 ms after the commit, and the hit's own transient cuts it)
    swing = S.sweep_bp(S.noise(rng, 0.17), 360.0, 980.0, 1.1, 0.07)
    S.add(buf, 0.02, [v * min(1.0, (i / (0.085 * S.RATE)) ** 1.6) * (1.0 if i < 0.09 * S.RATE else
                                                                         2.718 ** (-(i - 0.09 * S.RATE) / (0.018 * S.RATE)))
                      for i, v in enumerate(swing)], 1.0)


def hard_hands_hit(buf):
    rng = S.Noise(0x4A2E)
    S.add(buf, 0.0, S.click(rng, 0.003, 2600.0), 0.26)                        # the knuckles arriving
    S.add(buf, 0.0, S.body(rng, 0.03, 1450.0, 0.9, 0.006), 0.55)               # the slap of a fist on hide
    S.add(buf, 0.001, S.body(rng, 0.06, 620.0, 3.2, 0.013), 0.60)              # the knock: dry, bone under it
    S.add(buf, 0.0, S.crunch(rng, 0.020, 300.0, 2000.0, 0.005, sat=3.0), 0.30)  # hide / leather texture
    # the weight: a dull thud with a pitch drop, shorter than the basic swing's so a string of them stays tight
    # every layer runs to the end of the cue and decays there on its own: cut at 220 ms, the thud stopped at
    # -42 dBFS, a 22 dB step in 10 ms (the chop the SPRAY hit's first candidate had)
    S.add(buf, 0.0, S.body(rng, 0.26, 150.0, 1.0, 0.045), 1.00)
    S.add(buf, 0.0, S.thump(0.26, 124.0, 58.0, 0.022, 0.05, sat=2.2), 0.78)
    S.add(buf, 0.004, S.tail(S.Noise(0x4A2F), 0.256, 520.0, 0.036), 0.16)     # a small hard room, no more


JAWS_CLAMP_S = 0.017   # the jaws hit their stop on the frame after the first one (16.7 ms): the clack lives there


def jaws_snap(buf):
    rng = S.Noise(0x7A55)
    # 0 ms, THE TETHER FIRES: the spring's catch letting go (a small dry tick, well under the clack) and the chain
    # starting to run (a short, quiet, bright rasp). Heard as the lead-in, not as a second hit.
    S.add(buf, 0.0, S.click(rng, 0.0015, 3900.0), 0.16)
    S.bell(buf, 0.0005, 4600.0, 0.03, 0.008, partials=(1.0, 1.73))
    S.add(buf, 0.001, S.envelope(S.biquad(S.noise(rng, 0.02), "bp", 3200.0, 1.6), 0.002, 0.006), 0.12)
    # 17 ms, THE CLAMP, on the frame the jaws hit their stop: two iron jaws meeting. The strongest transient of the
    # cue: a hard clack (a tight body, high Q), a metallic knock that dies inside ~40 ms (inharmonic, never a ring),
    # teeth into hide, and a little weight under it
    c = JAWS_CLAMP_S
    S.add(buf, c, S.click(rng, 0.003, 2400.0), 0.46)
    S.add(buf, c, S.body(rng, 0.05, 1150.0, 3.4, 0.010), 0.80)
    S.bell(buf, c + 0.001, 1650.0, 0.17, 0.022, partials=(1.0, 2.76, 5.40))
    S.add(buf, c + 0.001, S.crunch(rng, 0.022, 420.0, 2400.0, 0.006, sat=3.0), 0.30)
    S.add(buf, c, S.thump(0.15, 150.0, 92.0, 0.012, 0.026, sat=1.8), 0.44)
    # after: the chain TAKING THE STRAIN as it jerks the creature, three short falling link ticks
    for at, f, g in ((0.052, 2650.0, 0.17), (0.078, 3050.0, 0.12), (0.108, 2450.0, 0.08)):
        S.add(buf, at, S.click(rng, 0.0015, f), g)
        S.bell(buf, at + 0.0005, f * 1.12, g * 0.5, 0.012, partials=(1.0, 1.61))


def main() -> int:
    S.make("sfx_seeker_spray_release", S.COMBAT_DIR, 0.20, spray_release, target_peak=0.26, max_ms=220.0, darken=9500.0)
    S.make("sfx_seeker_spray_hit", S.COMBAT_DIR, 0.28, spray_hit, target_peak=0.38, max_ms=300.0, darken=7500.0)
    S.make("sfx_seeker_spray_tick", S.COMBAT_DIR, 0.10, spray_tick, target_peak=0.30, max_ms=120.0, darken=8000.0)
    S.make("sfx_seeker_hard_hands_commit", S.COMBAT_DIR, 0.20, hard_hands_commit, target_peak=0.24, max_ms=220.0, darken=5000.0)
    S.make("sfx_seeker_hard_hands_hit", S.COMBAT_DIR, 0.26, hard_hands_hit, target_peak=0.39, max_ms=280.0, darken=6500.0)
    S.make("sfx_seeker_jaws_snap", S.COMBAT_DIR, 0.17, jaws_snap, target_peak=0.34, max_ms=190.0, darken=8000.0)
    print(f"{'cue':30} {'ms':>5} {'peak dB':>8} {'rms dB':>7} {'centroid':>9} {'decay20':>8}")
    for name in ("sfx_seeker_spray_release", "sfx_seeker_spray_hit", "sfx_seeker_spray_tick",
                 "sfx_seeker_hard_hands_commit", "sfx_seeker_hard_hands_hit", "sfx_seeker_jaws_snap", "sfx_cast", "sfx_hit"):
        m = S.measure(os.path.join(S.COMBAT_DIR, name + ".wav"))
        print(f"{name:30} {m['ms']:5.0f} {m['peak_db']:8.1f} {m['rms_db']:7.1f} {m['centroid']:9.0f} {m['decay20_ms']:8.0f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
