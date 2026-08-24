#!/usr/bin/env python3
"""Synthesise the FIGHT and FORGE one-shots into assets/audio.

Usage:
    python3 tools/asset-pipeline/make_battle_sfx.py

The 2026-08-24 audio audit found the entire fight silent: no hit, no critical, no
enemy death, no champion death, no boss arrival, no skill cast. The chest reveal
cascade — the game's designated payoff beat — was silent per item, and the forge's
UPGRADE and SOCKET verbs had no sound of their own. This writes the missing cues.

Same reasoning as make_sfx.py, which owns the big ceremony cues (trait, conquer,
levelup...): when the right asset is a formula, write the formula. Deterministic
noise, stdlib only, exactly reproducible.

TWO RULES SPECIFIC TO THIS SET, both enforced by asserts at the bottom:

1. SHORT. These cues fire inside combat playback, sometimes several per second at
   raised battle speed. Everything but the two "somebody fell" beats and the boss
   horn is under 250 ms.
2. QUIET. Peaks are normalised to ~0.40 of full scale, NOT the 0.92 the ceremony
   cues use — combat cues STACK (a swarm wave fires hit after hit), and headroom
   here is what keeps five overlapping thuds from clipping. SoundBank additionally
   rate-limits and duplicate-scales at play time; this is the second belt.

16-bit mono PCM at 44.1 kHz, which is what SoundEffect.FromStream wants.
"""

from __future__ import annotations

import math
import os
import struct
import wave

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
COMBAT_DIR = os.path.join(REPO, "assets", "audio", "combat")
UI_DIR = os.path.join(REPO, "assets", "audio", "ui")

RATE = 44100


class Noise:
    """Tiny deterministic LCG — a fixed stream means the same files come out of
    every run, so a rebuild is never a silent diff. Same as make_sfx.py."""

    def __init__(self, seed: int = 0x5EED) -> None:
        self._s = seed

    def next(self) -> float:
        self._s = (self._s * 1103515245 + 12345) & 0x7FFFFFFF
        return self._s / 0x3FFFFFFF - 1.0


def sweep(buf: list[float], start: float, dur: float, f_from: float, f_to: float,
          gain: float, shape: str = "sine", curve: float = 1.0) -> None:
    """A tone gliding between two frequencies. Phase is integrated, not sampled,
    or the pitch jumps at block boundaries.

    shape: "sine" for soft bodies, "square" for the chiptune voice (tanh-softened
    so it reads retro without alias fizz). curve bends the glide (>1 = falls fast
    then settles — the shape of a thing dropping).
    """
    i0 = int(start * RATE)
    n = min(len(buf) - i0, int(dur * RATE))
    phase = 0.0
    for i in range(max(0, n)):
        p = i / n
        f = f_from + (f_to - f_from) * (p ** curve)
        phase += 2.0 * math.pi * f / RATE
        s = math.sin(phase)
        if shape == "square":
            s = math.tanh(s * 6.0)      # squared-off but band-limited-ish
        env = min(1.0, p * 14.0) * (1.0 - p) ** 1.6
        buf[i0 + i] += gain * env * s


def blip(buf: list[float], start: float, f0: float, gain: float, tau: float,
         partials=(1.0, 2.0), shape: str = "square") -> None:
    """A chip blip: a fixed pitch with an exponential decay — the retro voice."""
    i0 = int(start * RATE)
    for k, ratio in enumerate(partials):
        amp = gain / (k + 1)
        w = 2.0 * math.pi * f0 * ratio
        length = min(len(buf) - i0, int(tau * 6 * RATE))
        for i in range(max(0, length)):
            t = i / RATE
            s = math.sin(w * t)
            if shape == "square":
                s = math.tanh(s * 5.0)
            buf[i0 + i] += amp * math.exp(-t / tau) * s


def bell(buf: list[float], start: float, f0: float, gain: float, tau: float,
         partials=(1.0, 2.76, 5.40, 8.93)) -> None:
    """Struck metal / crystal: inharmonic partials, the top dying first."""
    i0 = int(start * RATE)
    for k, ratio in enumerate(partials):
        amp = gain / (k + 1)
        decay = tau / (1.0 + k * 0.55)
        w = 2.0 * math.pi * f0 * ratio
        length = min(len(buf) - i0, int(decay * 5 * RATE))
        for i in range(max(0, length)):
            t = i / RATE
            buf[i0 + i] += amp * math.exp(-t / decay) * math.sin(w * t)


def air(buf: list[float], start: float, dur: float, gain: float, rng: Noise,
        rise: bool = False, lp: float = 0.28) -> None:
    """Filtered noise: the breath of a strike, or the intake before one."""
    i0 = int(start * RATE)
    n = min(len(buf) - i0, int(dur * RATE))
    acc = 0.0
    for i in range(max(0, n)):
        p = i / n
        acc += (rng.next() - acc) * lp
        env = (p ** 2 if rise else (1.0 - p) ** 3) * (1.0 - abs(2 * p - 1)) ** 0.3
        buf[i0 + i] += gain * env * acc


def write(path: str, buf: list[float], target_peak: float = 0.40) -> tuple[float, float]:
    """Normalise to a QUIET target peak, fade the tail, write. Returns (dur, peak).

    Unlike make_sfx.py's write (0.92 + soft limiter), these cues are normalised
    DOWN to ~0.40: they stack in combat, and headroom is the anti-clip budget.
    """
    tail = int(0.008 * RATE)
    for i in range(min(tail, len(buf))):
        buf[len(buf) - 1 - i] *= i / tail

    peak = max((abs(v) for v in buf), default=1.0) or 1.0
    norm = target_peak / peak
    frames = bytearray()
    out_peak = 0.0
    for v in buf:
        v = max(-1.0, min(1.0, v * norm))
        out_peak = max(out_peak, abs(v))
        frames += struct.pack("<h", int(v * 32767))

    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as fh:
        fh.setnchannels(1)
        fh.setsampwidth(2)
        fh.setframerate(RATE)
        fh.writeframes(bytes(frames))
    dur = len(buf) / RATE
    print(f"{os.path.relpath(path, REPO):<38} {dur * 1000:6.0f} ms   peak {out_peak:.2f}")
    return dur, out_peak


REPORT: list[tuple[str, float, float, float]] = []   # (name, dur, peak, max allowed dur)


def make(name: str, out_dir: str, seconds: float, build, target_peak: float = 0.40,
         max_ms: float = 250.0) -> None:
    buf = [0.0] * int(seconds * RATE)
    build(buf)
    dur, peak = write(os.path.join(out_dir, name + ".wav"), buf, target_peak)
    REPORT.append((name, dur, peak, max_ms))


# ── The fight ─────────────────────────────────────────────────────────────────────

def hit(buf):
    """Soft thud — the auto-attack landing. The cue heard most; it has to be felt
    more than heard."""
    rng = Noise(0x0417)
    air(buf, 0.0, 0.035, 0.6, rng, lp=0.16)
    sweep(buf, 0.0, 0.08, 150.0, 92.0, 1.0, shape="sine", curve=1.4)


def crit(buf):
    """Brighter snap — a critical. Same family as the hit, an octave of extra bite."""
    rng = Noise(0xC217)
    air(buf, 0.0, 0.025, 0.5, rng, lp=0.5)
    sweep(buf, 0.0, 0.06, 240.0, 130.0, 0.8, shape="sine", curve=1.4)
    blip(buf, 0.005, 880.0, 0.55, 0.030, partials=(1.0, 1.5))
    blip(buf, 0.02, 1318.5, 0.30, 0.035, partials=(1.0,))


def enemy_down(buf):
    """Short down-chirp — a creature falls. The classic chiptune defeat shape."""
    sweep(buf, 0.0, 0.16, 660.0, 190.0, 0.9, shape="square", curve=1.3)
    rng = Noise(0xED04)
    air(buf, 0.06, 0.10, 0.30, rng, lp=0.2)


def champ_down(buf):
    """Low descending — the champion falls. Longer and darker than an enemy's,
    because this one costs the run."""
    sweep(buf, 0.0, 0.40, 220.0, 55.0, 0.9, shape="square", curve=1.15)
    sweep(buf, 0.06, 0.34, 165.0, 44.0, 0.5, shape="sine", curve=1.15)
    rng = Noise(0xCD04)
    air(buf, 0.18, 0.26, 0.35, rng, lp=0.12)


def boss(buf):
    """Low horn over rumble — a boss walks in. Slow attack so it swells rather
    than pops; two detuned lows so it beats like something breathing."""
    rng = Noise(0xB055)
    i0 = 0
    n = min(len(buf), int(0.52 * RATE))
    ph1 = ph2 = 0.0
    for i in range(n):
        p = i / n
        ph1 += 2.0 * math.pi * 65.4 / RATE          # C2
        ph2 += 2.0 * math.pi * 98.0 / RATE          # G2, a fifth up
        env = min(1.0, p * 3.2) * (1.0 - p) ** 1.2
        s = math.tanh(math.sin(ph1) * 3.0) * 0.7 + math.sin(ph2) * 0.35
        buf[i0 + i] += env * s * 0.9
    air(buf, 0.0, 0.5, 0.30, rng, rise=True, lp=0.06)


def cast(buf):
    """Airy whoosh — a skill leaves the hand. Mostly breath, a faint rising tone
    inside it so it reads as INTENT, not wind."""
    rng = Noise(0xCA57)
    air(buf, 0.0, 0.19, 0.9, rng, rise=True, lp=0.45)
    sweep(buf, 0.02, 0.16, 480.0, 950.0, 0.35, shape="sine")


# ── The forge ─────────────────────────────────────────────────────────────────────

def reveal_tick(buf):
    """Bright tick — one item landing on the chest-reveal card. Fires once per
    item in a stagger, so it must be tiny and clean."""
    rng = Noise(0x71CC)
    air(buf, 0.0, 0.012, 0.4, rng, lp=0.6)
    blip(buf, 0.0, 1760.0, 0.6, 0.022, partials=(1.0, 2.0), shape="sine")


def equip(buf):
    """Click-clack — gear going on. Two transients, the second lower: the latch
    and the seat. (For the GEAR screen — generated here, wired by its owner.)"""
    rng = Noise(0xE001)
    air(buf, 0.0, 0.018, 0.5, rng, lp=0.5)
    blip(buf, 0.0, 950.0, 0.55, 0.018, partials=(1.0, 2.0))
    air(buf, 0.065, 0.02, 0.45, rng, lp=0.4)
    blip(buf, 0.065, 620.0, 0.6, 0.024, partials=(1.0, 2.0))


def gem(buf):
    """Crystalline ping — a gem set into a socket. Glassy inharmonic partials."""
    rng = Noise(0x63E6)
    air(buf, 0.0, 0.015, 0.3, rng, lp=0.6)
    bell(buf, 0.0, 1567.98, 0.8, 0.075, partials=(1.0, 2.32, 4.25, 6.63))
    bell(buf, 0.03, 2093.0, 0.3, 0.055, partials=(1.0, 2.32))


def upgrade(buf):
    """Anvil-ish tap — a refine takes. Smaller cousin of sfx_forge (which keeps
    the big verbs); this one can fire on every rung of the ladder."""
    rng = Noise(0x06A4)
    air(buf, 0.0, 0.03, 0.55, rng, lp=0.3)
    sweep(buf, 0.0, 0.05, 170.0, 95.0, 0.6, shape="sine", curve=1.3)
    bell(buf, 0.008, 1046.5, 0.55, 0.045, partials=(1.0, 2.76, 5.40))


if __name__ == "__main__":
    make("sfx_hit", COMBAT_DIR, 0.10, hit, target_peak=0.34)
    make("sfx_crit", COMBAT_DIR, 0.14, crit, target_peak=0.40)
    make("sfx_enemy_down", COMBAT_DIR, 0.19, enemy_down, target_peak=0.36)
    make("sfx_champ_down", COMBAT_DIR, 0.48, champ_down, target_peak=0.40, max_ms=500)
    make("sfx_boss", COMBAT_DIR, 0.56, boss, target_peak=0.40, max_ms=600)
    make("sfx_cast", COMBAT_DIR, 0.21, cast, target_peak=0.30)
    make("sfx_reveal_tick", UI_DIR, 0.08, reveal_tick, target_peak=0.32)
    make("sfx_equip", UI_DIR, 0.13, equip, target_peak=0.36)
    make("sfx_gem", UI_DIR, 0.25, gem, target_peak=0.36)
    make("sfx_upgrade", UI_DIR, 0.17, upgrade, target_peak=0.38)

    # The numeric ear: nobody listens in CI, so the two rules are ASSERTED.
    bad = []
    for name, dur, peak, max_ms in REPORT:
        if dur * 1000 > max_ms + 1:
            bad.append(f"{name}: {dur * 1000:.0f} ms exceeds its {max_ms:.0f} ms budget")
        if peak > 0.45:
            bad.append(f"{name}: peak {peak:.2f} above the 0.45 stacking ceiling")
        if peak < 0.05:
            bad.append(f"{name}: peak {peak:.2f} — effectively silent, the synth broke")
    if bad:
        raise SystemExit("SFX SANITY FAILED:\n  " + "\n  ".join(bad))
    print(f"all {len(REPORT)} cues within budget (every peak <= 0.45, durations as declared).")
