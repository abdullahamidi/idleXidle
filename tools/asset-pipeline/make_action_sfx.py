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

    sfx_seeker_jaws_snap         ~0.2 s  THE ANSWER to a bite (a Reaction, off the beat), timed to the picture: at 0 ms
                                       the trap's latch lets go (a tiny dry tick and a quiet chain rasp, a lead-in); at
                                       50 ms, the frame the two steel jaws hit their stops, the CLACK: dry forged steel,
                                       a short hard metal contact whose partials die inside ~12 ms (never a clang, a bell
                                       or an anvil), the second jaw's stop a few ms behind the first, and far beneath it
                                       a muted leather contact (the trap caught something). After it the chain taking the
                                       strain (three small link ticks). One cue for the whole reaction, right after the
                                       enemy's bite thud: BITE -> tiny release -> CLACK. No crunch, no bone, no thump.

    sfx_seeker_jaws_bite_synth   ~0.18 s  history: the SYNTHESISED bite (2026-09-28): two enamel clacks, a granular crunch,
                                       a wet squish, a short jaw thud. The owner rejected it and its two siblings ("all
                                       similar; I want a REAL bite, like Trundle's Q"): the live `sfx_seeker_jaws_bite` is
                                       built from REAL foley by make_jaws_bite.py. Written to the candidates folder only.

    sfx_seeker_jaws_fangs        ~0.13 s  history: SHADOW FANGS (2026-09-27): ONE dark, dry, sharp, short
                                       Shadow bite / thorn impact, its transient exactly on the fangs' SNAP: a dry click and
                                       a few inharmonic partials dying inside ~7 ms, a short dry organic impact, a damped
                                       dark body with no pitch drop, and under it an energy COMPRESSION (a dark tone
                                       squeezed down over ~35 ms). No ticks, no metal, no swarm, no bone crunch. The
                                       `_snap` (the trap's clack) and `_chomp` (the piranha's) cues are history.

The SPRAY and HARD HANDS cues are HUMAN-APPROVED (the owner, 2026-09-25). A measurement says a cue is bright, short and
quiet enough; only an ear says it sounds like a knife (or a fist), so every new cue here is a candidate until it is
listened to (design/audio/seeker-spray-audio-brief.md is the contract the approved ones keep). The JAWS snap is a
CANDIDATE until the owner has heard it (design/audio/seeker-jaws-audio-brief.md).
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


JAWS_CLAMP_S = 0.050   # the jaws hit their stops 50 ms after the first frame (the third frame after it): the clack lives there
JAWS_SECOND_STOP_S = 0.0035   # the other arm's stop, just behind: two arms, two stops, one CLACK with a mechanical edge


def jaws_snap(buf):
    """A spring-loaded steel trap, never a bite: the identity pass (2026-09-25) took out the polish cue's crunch (a
    saturated noise band, heard as teeth or bone) and its pitch-dropping thump (heard as a skull or a body), which made
    the clamp sound like a mouth closing. What is left is a mechanism: a latch, two steel jaws hitting their stops, a
    chain. The readable-clamp pass kept that material and moved the CLACK from 17 ms to the jaws' new stop at 50 ms: the
    ear hears the bite, a tiny release, then the clack, and the gap helps the eye find the close."""
    rng = S.Noise(0x7A57)
    # 0 ms, THE LATCH LETS GO: a tiny dry tick of the pawl and the chain starting to run (a short, quiet, bright
    # rasp). The lead-in, well under the clack, never a second hit.
    S.add(buf, 0.0, S.click(rng, 0.0012, 4200.0), 0.12)
    S.bell(buf, 0.0004, 5200.0, 0.025, 0.004, partials=(1.0, 1.41))
    S.add(buf, 0.001, S.envelope(S.biquad(S.noise(rng, 0.016), "bp", 3600.0, 1.8), 0.002, 0.005), 0.08)
    # 17 ms, THE CLACK, on the frame the arms hit their stops: dry forged steel. A hard broadband contact, and the
    # struck steel's own voice as a few inharmonic partials that die inside ~12 ms (a plate hitting its stop, not a
    # blade ringing: no partial outlives 20 ms). The second arm lands 3.5 ms behind the first, a touch quieter.
    c = JAWS_CLAMP_S
    for at, g in ((c, 1.0), (c + JAWS_SECOND_STOP_S, 0.62)):
        S.add(buf, at, S.click(rng, 0.0022, 3000.0), 0.55 * g)
        S.bell(buf, at + 0.0002, 2180.0, 0.34 * g, 0.009, partials=(1.0, 1.47, 2.09, 2.95))
        S.add(buf, at, S.body(rng, 0.03, 1850.0, 4.5, 0.004), 0.42 * g)
    # the housing's own heft: a short, damped knock of the spring box (a small steel body, mid-range, gone in ~15 ms)
    S.bell(buf, c + 0.0006, 610.0, 0.16, 0.007, partials=(1.0, 1.58))
    # far beneath the steel, the contact that says it CAUGHT something: muted hide under the arms, low-passed, dull,
    # ~20 dB under the clack. Never saturated (that was the crunch), never a thump.
    S.add(buf, c + 0.001, S.envelope(S.lp2(S.noise(rng, 0.03), 650.0), 0.002, 0.007), 0.10)
    # after: the chain TAKING THE STRAIN as it jerks the creature, three small falling link ticks
    for at, f, g in ((c + 0.033, 3300.0, 0.11), (c + 0.057, 3700.0, 0.08), (c + 0.085, 3050.0, 0.055)):
        S.add(buf, at, S.click(rng, 0.0012, f), g)
        S.bell(buf, at + 0.0004, f * 1.08, g * 0.45, 0.006, partials=(1.0, 1.52))


def jaws_chomp(buf):
    """SHADOW PIRANHA (the production direction, 2026-09-27): the dry-steel trap CLACK belonged to the rejected mechanical
    fantasy. This is a SMALL SUPERNATURAL CHOMP for the whole three-bite phrase: at 0 ms a dry short bite/snap (a tiny
    click and a few inharmonic partials that die inside ~8 ms: teeth meeting, not steel), under it a subtle leathery
    transient (low-passed noise, a dozen ms) and a very short Shadow energy crackle (a bright narrow band, 25 ms, quiet);
    and nothing after it: ONE bite (the one-piranha direction: no tick-tick swarm). No bone
    crack, no metal, no roar, no long whoosh. A frequent Reaction: short, and quiet enough to repeat."""
    rng = S.Noise(0x9A75)
    # 0 ms, THE MAIN CHOMP: the bite's snap
    S.add(buf, 0.0, S.click(rng, 0.0018, 3100.0), 0.50)
    S.bell(buf, 0.0003, 2350.0, 0.26, 0.008, partials=(1.0, 1.39, 2.11))
    S.add(buf, 0.0, S.body(rng, 0.03, 1400.0, 3.5, 0.005), 0.30)
    # the leathery contact beneath it: dull, low-passed, ~14 ms
    S.add(buf, 0.0008, S.envelope(S.lp2(S.noise(rng, 0.03), 900.0), 0.0015, 0.006), 0.22)
    # the Shadow's crackle: a narrow bright band that fizzes for ~25 ms, well under the snap
    S.add(buf, 0.002, S.envelope(S.biquad(S.noise(rng, 0.04), "bp", 4600.0, 1.4), 0.002, 0.009), 0.09)
    # a little weight so it is a bite and not a tick: a short damped body, gone in ~20 ms, never a thump with a pitch drop
    S.bell(buf, 0.0006, 420.0, 0.14, 0.009, partials=(1.0, 1.62))
    # ONE bite: no secondary ticks (the one-piranha direction, 2026-09-27: the visual is one strong Shadow bite)


def jaws_fangs(buf):
    """SHADOW FANGS (the FINAL direction, 2026-09-27): ONE short Shadow bite / thorn impact, dark, dry, sharp, short, its
    main transient exactly on the SNAP (t 0). Energy compression + a short organic/dry impact, nothing more: at 0 a dry
    sharp transient (a tiny click and a few inharmonic partials that die inside ~7 ms: points meeting hide, not steel),
    a short dry organic impact under it (low-passed noise, ~12 ms) and a damped dark body (no pitch drop, so it is
    never a skull thump); then the COMPRESSION, a dark tone squeezed down (900 -> 130 Hz in ~35 ms, quiet, under the
    impact) as the fangs close on what they caught, and a whisper of Shadow crackle. No secondary ticks, no metal, no
    swarm, no bone crunch, no roar. A frequent Reaction: ~130 ms, quiet enough to repeat every few seconds."""
    rng = S.Noise(0xFA96)
    # 0 ms, THE SNAP: the dry sharp transient
    S.add(buf, 0.0, S.click(rng, 0.0016, 2700.0), 0.52)
    S.bell(buf, 0.0002, 1900.0, 0.22, 0.007, partials=(1.0, 1.47, 2.23))
    # the dry organic impact beneath it: low-passed, ~12 ms
    S.add(buf, 0.0006, S.envelope(S.lp2(S.noise(rng, 0.03), 800.0), 0.0012, 0.005), 0.30)
    S.add(buf, 0.0, S.body(rng, 0.03, 1100.0, 3.0, 0.006), 0.26)
    # the dark body: damped, no pitch drop (a bite, not a skull)
    S.bell(buf, 0.0004, 300.0, 0.16, 0.011, partials=(1.0, 1.58))
    # THE COMPRESSION: a dark tone squeezed down under the impact as the fangs close on what they caught
    S.sweep(buf, 0.002, 0.045, 900.0, 130.0, 0.13, shape="sine", curve=2.2)
    # a whisper of Shadow crackle: a narrow bright band, ~20 ms, well under everything
    S.add(buf, 0.002, S.envelope(S.biquad(S.noise(rng, 0.03), "bp", 4200.0, 1.6), 0.002, 0.007), 0.06)


BITE_CANDIDATES = os.path.join(S.REPO, "production", "qa", "evidence", "jaws-bite-sound", "candidates")
AUDIO_HISTORY = os.path.join(HERE, "audio_history")   # JAWS cues the owner rejected: kept, never played, not game assets


def _bite(buf, seed, clack_hz, clack_gain, grains, crunch_hi, squish, weight, whisper=0.05):
    """A BITE, in the order a mouth makes one, its first transient exactly on the SNAP (t 0), after the owner approved
    the smoke-teeth picture (2026-09-28): "make the sound more of a bite / being-bitten sound". The thorn impact before
    it (`jaws_fangs`) was dark and dry but read as a hit, not a mouth: it had no teeth meeting, no give, no jaw.
      1. THE TEETH MEET: two short enamel clacks, the upper row and, 4 ms behind it, the lower (a hard small contact whose
         resonance dies inside ~4 ms: enamel, never steel);
      2. THEY SINK IN: a granular crunch, a few saturated micro-bursts over ~40 ms, each quieter than the last (the teeth
         going through hide), and a short WET squish (a band falling through the noise);
      3. THE JAW'S WEIGHT: a short low thud with a small pitch drop (the force of the bite, not a skull);
      4. a whisper of the Shadow (a dark tone squeezed down, far under everything).
    ONE bite: nothing after ~60 ms but the decays. No metal, no roar, no swarm, no whoosh."""
    rng = S.Noise(seed)
    # 1. THE TEETH MEET
    for at, g in ((0.0, 1.0), (0.004, 0.72)):
        S.add(buf, at, S.click(rng, 0.0014, clack_hz), 0.55 * clack_gain * g)
        S.bell(buf, at + 0.0002, clack_hz * 0.86, 0.17 * clack_gain * g, 0.0035, partials=(1.0, 1.52, 2.31))
        S.add(buf, at, S.body(rng, 0.02, clack_hz * 0.42, 4.0, 0.003), 0.30 * clack_gain * g)
    # 2. THEY SINK IN: the crunch grains, and the wet give
    for at, g in grains:
        S.add(buf, at, S.crunch(rng, 0.03, 380.0, crunch_hi, 0.006, sat=4.0), g)
    wet = S.sweep_bp(S.noise(rng, 0.09), 1500.0, 420.0, 2.2, 0.025)
    S.add(buf, 0.003, S.envelope(wet, 0.003, 0.022), squish)
    # 3. THE JAW'S WEIGHT
    f_hi, f_lo, amp_tau, g = weight
    S.add(buf, 0.0, S.thump(0.16, f_hi, f_lo, 0.014, amp_tau, sat=2.0), g)
    # 4. the Shadow, a whisper
    S.sweep(buf, 0.002, 0.05, 700.0, 120.0, whisper, shape="sine", curve=2.0)


def jaws_bite(buf):
    """Candidate A, CHOMP (the default): the teeth, the give and the weight in balance."""
    _bite(buf, 0xB17E, 3000.0, 1.0,
          grains=((0.002, 0.42), (0.009, 0.30), (0.016, 0.22), (0.026, 0.14), (0.038, 0.08)),
          crunch_hi=2400.0, squish=0.34, weight=(165.0, 88.0, 0.032, 0.55))


def jaws_bite_crunch(buf):
    """Candidate B, CRUNCH: biting THROUGH something: more and brighter crunch grains, a lighter jaw."""
    _bite(buf, 0xB17F, 3300.0, 1.0,
          grains=((0.002, 0.50), (0.007, 0.40), (0.012, 0.34), (0.019, 0.28), (0.027, 0.20), (0.036, 0.13), (0.047, 0.07)),
          crunch_hi=3200.0, squish=0.24, weight=(170.0, 95.0, 0.026, 0.36))


def jaws_bite_heavy(buf):
    """Candidate C, HEAVY: a beast's jaws: a lower clack, fewer grains, a deeper and longer jaw weight."""
    _bite(buf, 0xB180, 2300.0, 0.9,
          grains=((0.002, 0.34), (0.011, 0.22), (0.022, 0.12)),
          crunch_hi=1900.0, squish=0.38, weight=(150.0, 68.0, 0.046, 0.85), whisper=0.07)


def main() -> int:
    S.make("sfx_seeker_spray_release", S.COMBAT_DIR, 0.20, spray_release, target_peak=0.26, max_ms=220.0, darken=9500.0)
    S.make("sfx_seeker_spray_hit", S.COMBAT_DIR, 0.28, spray_hit, target_peak=0.38, max_ms=300.0, darken=7500.0)
    S.make("sfx_seeker_spray_tick", S.COMBAT_DIR, 0.10, spray_tick, target_peak=0.30, max_ms=120.0, darken=8000.0)
    S.make("sfx_seeker_hard_hands_commit", S.COMBAT_DIR, 0.20, hard_hands_commit, target_peak=0.24, max_ms=220.0, darken=5000.0)
    S.make("sfx_seeker_hard_hands_hit", S.COMBAT_DIR, 0.26, hard_hands_hit, target_peak=0.39, max_ms=280.0, darken=6500.0)
    S.make("sfx_seeker_jaws_snap", AUDIO_HISTORY, 0.20, jaws_snap, target_peak=0.34, max_ms=220.0, darken=8000.0)   # history: the rejected trap's clack
    S.make("sfx_seeker_jaws_chomp", AUDIO_HISTORY, 0.14, jaws_chomp, target_peak=0.30, max_ms=160.0, darken=7000.0)   # history: the rejected piranha's chomp
    S.make("sfx_seeker_jaws_fangs", AUDIO_HISTORY, 0.13, jaws_fangs, target_peak=0.32, max_ms=150.0, darken=6500.0)   # history: the thorn impact
    S.make("sfx_seeker_jaws_bite_synth", BITE_CANDIDATES, 0.18, jaws_bite, target_peak=0.34, max_ms=200.0, darken=7000.0)   # history
    S.make("sfx_seeker_jaws_bite_crunch", BITE_CANDIDATES, 0.18, jaws_bite_crunch, target_peak=0.34, max_ms=200.0, darken=8000.0)
    S.make("sfx_seeker_jaws_bite_heavy", BITE_CANDIDATES, 0.20, jaws_bite_heavy, target_peak=0.34, max_ms=220.0, darken=6000.0)
    print(f"{'cue':30} {'ms':>5} {'peak dB':>8} {'rms dB':>7} {'centroid':>9} {'decay20':>8}")
    for name in ("sfx_seeker_spray_release", "sfx_seeker_spray_hit", "sfx_seeker_spray_tick",
                 "sfx_seeker_hard_hands_commit", "sfx_seeker_hard_hands_hit", "sfx_cast", "sfx_hit"):
        m = S.measure(os.path.join(S.COMBAT_DIR, name + ".wav"))
        print(f"{name:30} {m['ms']:5.0f} {m['peak_db']:8.1f} {m['rms_db']:7.1f} {m['centroid']:9.0f} {m['decay20_ms']:8.0f}")
    history = [(AUDIO_HISTORY, k) for k in ("sfx_seeker_jaws_fangs", "sfx_seeker_jaws_chomp", "sfx_seeker_jaws_snap")]
    rejected = [(BITE_CANDIDATES, k) for k in ("sfx_seeker_jaws_bite_synth", "sfx_seeker_jaws_bite_crunch", "sfx_seeker_jaws_bite_heavy")]
    for d, name in history + rejected:
        m = S.measure(os.path.join(d, name + ".wav"))
        print(f"{name:30} {m['ms']:5.0f} {m['peak_db']:8.1f} {m['rms_db']:7.1f} {m['centroid']:9.0f} {m['decay20_ms']:8.0f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
