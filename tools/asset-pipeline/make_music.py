#!/usr/bin/env python3
"""Synthesise the looping music beds into assets/audio/music.

    python3 tools/asset-pipeline/make_music.py

SoundBank has had working looping music since it was written — a SoundEffectInstance with
IsLooped, a per-screen track, even a per-theme arena variant with a fallback. It has never
had a FILE. All six tracks the game asks for resolved to nothing, and PlayMusic no-ops on a
missing key, so the game has been silent on every screen for the whole of development.

WHY THESE ARE DRONES AND NOT TUNES. A bad melody is worse than silence — it draws attention
to itself and then repeats every twenty seconds for the hundreds of hours an idle game is
open. A slow evolving pad is the honest thing to synthesise: it sets a room's temperature,
it survives repetition, and it is reachable with the same arithmetic as the bell in
make_sfx.py. When the right asset is a formula, write the formula.

THE ONE HARD CONSTRAINT — SEAMLESS LOOPS. IsLooped restarts the buffer with no crossfade, so
any discontinuity at the boundary is an audible tick, once every loop, forever. Every
frequency here is therefore locked to a whole number of cycles across the loop:

    f = cycles / DURATION      for integer `cycles`

which puts every partial, and every amplitude LFO, at exactly the same phase at the end as
at the start. The check at the bottom measures the actual discontinuity and fails loudly if
a track ever stops being seamless.
"""

from __future__ import annotations

import math
import os
import struct
import wave

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(REPO, "assets", "audio", "music")

RATE = 22050          # half rate: a drone has nothing above 8 kHz, and it halves the memory
DURATION = 20.0       # seconds per loop
N = int(RATE * DURATION)


def lock(freq: float) -> float:
    """Snap a frequency to the nearest whole number of cycles per loop. See the module docstring."""
    return max(1.0, round(freq * DURATION)) / DURATION


class Noise:
    """Deterministic LCG, so a rebuild is byte-identical and never a silent diff."""

    def __init__(self, seed: int = 0x5EED) -> None:
        self.state = seed & 0xFFFFFFFF

    def next(self) -> float:
        self.state = (1103515245 * self.state + 12345) & 0x7FFFFFFF
        return self.state / 0x3FFFFFFF - 1.0


def pad(buf: list[float], freq: float, gain: float, partials: int = 5,
        lfo_cycles: int = 3, depth: float = 0.35, detune: float = 0.0) -> None:
    """A sustained voice: harmonics at 1/n, breathing under a loop-locked LFO.

    `detune` shifts a second copy by a few cents to give the voice width. It is locked too,
    so the beat between the pair also lines up at the loop point.
    """
    f0 = lock(freq)
    f1 = lock(freq * (1.0 + detune)) if detune else None
    w_lfo = 2.0 * math.pi * lfo_cycles / DURATION

    for k in range(partials):
        amp = gain / (k + 1) ** 1.4
        if amp < 0.002:
            break
        for f in (f0, f1) if f1 else (f0,):
            w = 2.0 * math.pi * lock(f * (k + 1))
            phase = 0.7 * k                        # a fixed offset, so partials do not all peak together
            for i in range(N):
                t = i / RATE
                env = 1.0 - depth + depth * (0.5 + 0.5 * math.sin(w_lfo * t + k))
                buf[i] += amp * env * math.sin(w * t + phase)


def wind(buf: list[float], gain: float, rng: Noise, cycles: int = 2) -> None:
    """A filtered noise bed. One-pole low-pass over white noise, swelling on a locked LFO.

    The noise itself cannot be seamless — it is random — so it is faded at both ends of the
    loop into silence, which IS continuous. A drone can carry the boundary; hiss cannot.
    """
    w_lfo = 2.0 * math.pi * cycles / DURATION
    prev = 0.0
    edge = int(0.35 * RATE)
    for i in range(N):
        prev += 0.02 * (rng.next() - prev)          # one-pole low-pass, ~70 Hz corner
        env = 0.5 + 0.5 * math.sin(w_lfo * i / RATE)
        taper = min(1.0, i / edge, (N - 1 - i) / edge)
        buf[i] += gain * prev * env * taper


def write(path: str, buf: list[float]) -> float:
    peak = max((abs(v) for v in buf), default=1.0) or 1.0
    norm = 0.55 / peak                              # a bed sits UNDER the game, never over it
    frames = bytearray()
    for v in buf:
        v = math.tanh(v * norm * 1.1) * 0.9
        frames += struct.pack("<h", int(max(-1.0, min(1.0, v)) * 32767))

    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as fh:
        fh.setnchannels(1)
        fh.setsampwidth(2)
        fh.setframerate(RATE)
        fh.writeframes(bytes(frames))

    # THE SEAM, measured AGAINST WHAT THE SIGNAL NORMALLY DOES. The first version compared the
    # last sample to the first and called the difference the seam — but consecutive samples of
    # any signal with high content differ by a lot, so a perfectly seamless bright track scored
    # worse than a dull one. It flagged music_constellation, which was fine, and would have
    # missed a real tick on a bass drone. The step across the loop point is only a defect if it
    # is bigger than the steps either side of it.
    # AGAINST THE TWO STEPS IT SITS BETWEEN, and nothing else. Every frequency here is locked
    # to a whole number of cycles, so s(0) == s(DURATION) exactly and the join is by
    # construction just another consecutive step — but a waveform that is STEEP at t=0 makes
    # that step large in absolute terms and large against any average, while being perfectly
    # continuous.
    #
    # Two metrics were wrong before this one, and both failed the same way — they measured the
    # signal instead of the defect. |first - last| flagged bright tracks. The same figure over
    # the buffer's MEAN step flagged music_constellation at 4.2x while its join (1880 LSB) sat
    # neatly between the steps either side of it (2112 and 1574). The only honest ruler is the
    # immediate neighbourhood: a continuous join reads ~0.9 of its adjacent steps, and a real
    # discontinuity reads far above 1.
    pcm = struct.unpack(f"<{len(frames) // 2}h", bytes(frames))
    adjacent = max(abs(pcm[1] - pcm[0]), abs(pcm[-1] - pcm[-2]), 1)
    seam = abs(pcm[0] - pcm[-1]) / adjacent
    print(f"{os.path.basename(path):26s} {DURATION:.0f}s  join x{seam:.2f} of the steps it sits between"
          + ("   ** AUDIBLE TICK" if seam > 1.8 else ""))
    return seam


# ── The rooms. Each is a chord and a temperature, not a tune. ─────────────────────────────

def title() -> float:
    """Wide, slow, unresolved — the game has not started yet."""
    buf = [0.0] * N
    pad(buf, 55.00, 0.50, partials=6, lfo_cycles=1, depth=0.30, detune=0.004)   # A1
    pad(buf, 82.41, 0.34, partials=5, lfo_cycles=2, depth=0.40, detune=0.003)   # E2, a fifth
    pad(buf, 130.81, 0.20, partials=4, lfo_cycles=3, depth=0.55)                # C3 — minor third
    wind(buf, 0.30, Noise(0x7171), cycles=1)
    return write(os.path.join(OUT_DIR, "music_title.wav"), buf)


def combat() -> float:
    """The fight. Lower, closer, with a slow pulse under it — pressure, not urgency: this
    plays for hours and the champion fights on its own."""
    buf = [0.0] * N
    pad(buf, 49.00, 0.55, partials=6, lfo_cycles=4, depth=0.45, detune=0.005)   # G1
    pad(buf, 73.42, 0.30, partials=4, lfo_cycles=6, depth=0.50)                 # D2
    pad(buf, 116.54, 0.16, partials=3, lfo_cycles=8, depth=0.60)                # A#2 — tension
    wind(buf, 0.26, Noise(0xC0B7), cycles=4)
    return write(os.path.join(OUT_DIR, "music_combat.wav"), buf)


def forge() -> float:
    """Warm and metallic. Low body, a ringing upper voice, and nothing hurrying."""
    buf = [0.0] * N
    pad(buf, 65.41, 0.50, partials=6, lfo_cycles=2, depth=0.30, detune=0.004)   # C2
    pad(buf, 98.00, 0.30, partials=5, lfo_cycles=3, depth=0.35)                 # G2
    pad(buf, 261.63, 0.10, partials=3, lfo_cycles=5, depth=0.70)                # C4, the ring
    wind(buf, 0.22, Noise(0xF0F0), cycles=2)
    return write(os.path.join(OUT_DIR, "music_forge.wav"), buf)


def warren() -> float:
    """Cosy. Higher, gentler, major — the one place in the game that is simply yours."""
    buf = [0.0] * N
    pad(buf, 87.31, 0.44, partials=5, lfo_cycles=2, depth=0.30, detune=0.003)   # F2
    pad(buf, 130.81, 0.30, partials=4, lfo_cycles=3, depth=0.35)                # C3
    pad(buf, 174.61, 0.20, partials=4, lfo_cycles=5, depth=0.45)                # F3
    pad(buf, 220.00, 0.12, partials=3, lfo_cycles=7, depth=0.55)                # A3 — major third
    wind(buf, 0.16, Noise(0xBEE5), cycles=3)
    return write(os.path.join(OUT_DIR, "music_warren.wav"), buf)


def constellation() -> float:
    """The trait spine. Airy and sparse — high voices over almost nothing."""
    buf = [0.0] * N
    pad(buf, 110.00, 0.30, partials=3, lfo_cycles=1, depth=0.35)                # A2, far below
    pad(buf, 329.63, 0.20, partials=3, lfo_cycles=4, depth=0.65, detune=0.002)  # E4
    pad(buf, 493.88, 0.13, partials=2, lfo_cycles=6, depth=0.75)                # B4
    pad(buf, 659.25, 0.08, partials=2, lfo_cycles=9, depth=0.80)                # E5, a glint
    wind(buf, 0.14, Noise(0x5721), cycles=2)
    return write(os.path.join(OUT_DIR, "music_constellation.wav"), buf)


def world_map() -> float:
    """Open. Bare fifths and a lot of air — somewhere to look at, not to be in."""
    buf = [0.0] * N
    pad(buf, 73.42, 0.46, partials=5, lfo_cycles=1, depth=0.30, detune=0.004)   # D2
    pad(buf, 110.00, 0.32, partials=4, lfo_cycles=2, depth=0.40)                # A2
    pad(buf, 220.00, 0.14, partials=3, lfo_cycles=4, depth=0.60)                # A3
    wind(buf, 0.28, Noise(0x3A9F), cycles=1)
    return write(os.path.join(OUT_DIR, "music_map.wav"), buf)


# ── The six arenas, one per region Source. ───────────────────────────────────────────────
#
# Game1.UpdateMusic has always asked for `music_arena_{theme}` and fallen back to music_combat
# when the file was absent — which it always was, so all six regions shared one bed and the
# lookup was a branch that had never once been taken.
#
# Every one of these is still THE COMBAT BED first and a colour second: a low fundamental with
# real weight, a mid voice, and a pulse slow enough to live under a fight that runs for hours.
# What changes between them is temperature and interval, not activity. The brief from combat()
# holds — pressure, not urgency — because the champion fights on its own and the player is
# usually looking at another screen entirely.

def arena_nature() -> float:
    """VERDANT HOLLOW — E minor, warm and breathing, the most air of the six."""
    buf = [0.0] * N
    pad(buf, 41.20, 0.52, partials=6, lfo_cycles=3, depth=0.40, detune=0.005)   # E1
    pad(buf, 61.74, 0.32, partials=5, lfo_cycles=4, depth=0.45)                 # B1, the fifth
    pad(buf, 98.00, 0.18, partials=4, lfo_cycles=6, depth=0.55)                 # G2, minor third
    wind(buf, 0.34, Noise(0x4EAF), cycles=3)                                    # leaves
    return write(os.path.join(OUT_DIR, "music_arena_nature.wav"), buf)


def arena_machine() -> float:
    """CINDERWORKS — bare octaves and a ringing top. Hard, and the fastest pulse of the six."""
    buf = [0.0] * N
    pad(buf, 32.70, 0.55, partials=7, lfo_cycles=6, depth=0.50, detune=0.006)   # C1
    pad(buf, 65.41, 0.34, partials=5, lfo_cycles=8, depth=0.50)                 # C2 — an OCTAVE,
    pad(buf, 98.00, 0.20, partials=4, lfo_cycles=12, depth=0.60)                # G2   no third at
    pad(buf, 261.63, 0.09, partials=3, lfo_cycles=16, depth=0.75)               # C4, the hammer
    wind(buf, 0.18, Noise(0xC0A1), cycles=6)
    return write(os.path.join(OUT_DIR, "music_arena_machine.wav"), buf)


def arena_shadow() -> float:
    """UMBRAL REACH — a tritone that never resolves, and almost nothing above it."""
    buf = [0.0] * N
    pad(buf, 55.00, 0.55, partials=6, lfo_cycles=2, depth=0.45, detune=0.004)   # A1
    pad(buf, 77.78, 0.30, partials=4, lfo_cycles=3, depth=0.55)                 # D#2 — the tritone
    pad(buf, 110.00, 0.14, partials=3, lfo_cycles=5, depth=0.65)                # A2
    wind(buf, 0.32, Noise(0x5AD0), cycles=2)
    return write(os.path.join(OUT_DIR, "music_arena_shadow.wav"), buf)


def arena_body() -> float:
    """MARROW WASTES — the lowest and thickest. A slow deep swell, felt more than heard."""
    buf = [0.0] * N
    pad(buf, 36.71, 0.60, partials=7, lfo_cycles=2, depth=0.55, detune=0.007)   # D1
    pad(buf, 55.00, 0.34, partials=5, lfo_cycles=3, depth=0.50)                 # A1
    pad(buf, 87.31, 0.16, partials=3, lfo_cycles=4, depth=0.60)                 # F2, minor third
    wind(buf, 0.24, Noise(0xB0D1), cycles=2)
    return write(os.path.join(OUT_DIR, "music_arena_body.wav"), buf)


def arena_mind() -> float:
    """THE STILL ARCHIVE — cold and exact. A high glint over a nearly empty floor."""
    buf = [0.0] * N
    pad(buf, 61.74, 0.48, partials=5, lfo_cycles=2, depth=0.35, detune=0.002)   # B1
    pad(buf, 92.50, 0.28, partials=4, lfo_cycles=4, depth=0.45)                 # F#2
    pad(buf, 311.13, 0.09, partials=2, lfo_cycles=9, depth=0.80)                # D#4, the glint
    wind(buf, 0.12, Noise(0x11D5), cycles=3)
    return write(os.path.join(OUT_DIR, "music_arena_mind.wav"), buf)


def arena_spirit() -> float:
    """THE PALE CHOIR — voices. Every partial detuned into a pair, so the whole bed shimmers."""
    buf = [0.0] * N
    pad(buf, 55.00, 0.48, partials=6, lfo_cycles=2, depth=0.35, detune=0.008)   # A1
    pad(buf, 82.41, 0.30, partials=5, lfo_cycles=3, depth=0.45, detune=0.007)   # E2
    pad(buf, 110.00, 0.20, partials=4, lfo_cycles=4, depth=0.50, detune=0.006)  # A2
    pad(buf, 138.59, 0.12, partials=3, lfo_cycles=6, depth=0.65, detune=0.005)  # C#3 — major third
    wind(buf, 0.20, Noise(0x5717), cycles=3)
    return write(os.path.join(OUT_DIR, "music_arena_spirit.wav"), buf)


SCREEN_BEDS = (title, combat, forge, warren, constellation, world_map)
ARENA_BEDS = (arena_nature, arena_machine, arena_shadow, arena_body, arena_mind, arena_spirit)

if __name__ == "__main__":
    worst = max(t() for t in SCREEN_BEDS + ARENA_BEDS)
    print()
    if worst > 1.8:
        raise SystemExit(f"a join {worst:.1f}x the steps around it will tick once per loop")
    total = len(SCREEN_BEDS) + len(ARENA_BEDS)
    print(f"{total} beds, worst join {worst:.2f}x the steps it sits between — "
          "every loop closes silently.")
