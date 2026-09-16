#!/usr/bin/env python3
"""Synthesise the UI cues into assets/audio.

Usage:
    python3 tools/asset-pipeline/make_sfx.py

`assets/audio` held nothing but a README. SoundBank has always loaded every WAV it
finds there and no-ops on a missing cue, so the game referenced sfx_click, sfx_forge,
sfx_conquer and sfx_levelup for the whole of development and played silence for all
four — which is why "add a sound" for the trait flourish is not a one-line call. This
writes the two cues that flourish asks for, so the cue is real rather than another
name pointing at nothing.

Synthesised rather than generated or sourced: a bell is a sum of decaying partials,
which is nine lines of arithmetic and is exactly reproducible. There is no asset to
license, nothing to knock out, and re-tuning it is editing a number here rather than
rolling a generator again. Same call as TraitsScreen.Ring — when the right asset is
a formula, write the formula.

16-bit mono PCM at 44.1 kHz, which is what SoundEffect.FromStream wants.
"""

from __future__ import annotations

import math
import os
import struct
import wave

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(REPO, "assets", "audio", "ui")

RATE = 44100


class Noise:
    """A tiny deterministic LCG. `random` would work, but a fixed stream means the
    same two files come out of every run, so a rebuild is never a silent diff."""

    def __init__(self, seed: int = 0x5EED) -> None:
        self._s = seed

    def next(self) -> float:
        self._s = (self._s * 1103515245 + 12345) & 0x7FFFFFFF
        return self._s / 0x3FFFFFFF - 1.0


def bell(buf: list[float], start: float, f0: float, gain: float, tau: float,
         partials=(1.0, 2.0, 3.01, 4.17, 5.43)) -> None:
    """A struck-metal tone: partials at inharmonic ratios, the high ones dying first.

    Equal-amplitude partials give an organ; 1/n with a shortening decay gives a bell,
    because what makes a strike sound struck is the top of the spectrum leaving early.
    """
    i0 = int(start * RATE)
    for k, ratio in enumerate(partials):
        amp = gain / (k + 1)
        decay = tau / (1.0 + k * 0.55)
        w = 2.0 * math.pi * f0 * ratio
        length = min(len(buf) - i0, int(decay * 5 * RATE))
        for i in range(max(0, length)):
            t = i / RATE
            buf[i0 + i] += amp * math.exp(-t / decay) * math.sin(w * t)


def sweep(buf: list[float], start: float, dur: float, f_from: float, f_to: float,
          gain: float) -> None:
    """A sine gliding between two frequencies — phase is integrated, not sampled, or
    the pitch jumps at every block boundary."""
    i0 = int(start * RATE)
    n = min(len(buf) - i0, int(dur * RATE))
    phase = 0.0
    for i in range(max(0, n)):
        p = i / n
        f = f_from + (f_to - f_from) * p
        phase += 2.0 * math.pi * f / RATE
        # In at the start, out at the end, so neither edge clicks.
        env = min(1.0, p * 12.0) * (1.0 - p) ** 1.5
        buf[i0 + i] += gain * env * math.sin(phase)


def air(buf: list[float], start: float, dur: float, gain: float, rng: Noise,
        rise: bool = False) -> None:
    """Filtered noise: the breath of a strike, or the intake before one."""
    i0 = int(start * RATE)
    n = min(len(buf) - i0, int(dur * RATE))
    lp = 0.0
    for i in range(max(0, n)):
        p = i / n
        lp += (rng.next() - lp) * 0.28          # one-pole low-pass, takes the fizz off
        env = (p ** 2 if rise else (1.0 - p) ** 3) * (1.0 - abs(2 * p - 1)) ** 0.3
        buf[i0 + i] += gain * env * lp


def write(path: str, buf: list[float]) -> None:
    """Soft-limit, fade the tail, and write. A hard clip on a bell is audible as grit."""
    tail = int(0.02 * RATE)
    for i in range(min(tail, len(buf))):
        buf[len(buf) - 1 - i] *= i / tail

    peak = max((abs(v) for v in buf), default=1.0) or 1.0
    norm = 0.92 / peak
    frames = bytearray()
    for v in buf:
        v *= norm
        v = math.tanh(v * 1.15) * 0.87          # gentle knee rather than a cliff
        frames += struct.pack("<h", int(max(-1.0, min(1.0, v)) * 32767))

    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as fh:
        fh.setnchannels(1)
        fh.setsampwidth(2)
        fh.setframerate(RATE)
        fh.writeframes(bytes(frames))
    print(f"{path}  {len(buf) / RATE:.2f}s  peak {peak:.2f}")


def trait_lit() -> None:
    """An ordinary trait: a bright two-note chime, over in under a second."""
    rng = Noise(0x1234)
    buf = [0.0] * int(0.95 * RATE)
    air(buf, 0.0, 0.10, 0.30, rng)
    bell(buf, 0.005, 523.25, 0.55, 0.34)         # C5
    bell(buf, 0.085, 783.99, 0.40, 0.30)         # G5, a fifth above, just behind it
    sweep(buf, 0.0, 0.16, 220.0, 660.0, 0.22)    # the lift under the strike
    write(os.path.join(OUT_DIR, "sfx_trait_lit.wav"), buf)


def trait_terminal() -> None:
    """A terminal: a road ends. Low impact, rising swell, and a chord that hangs."""
    rng = Noise(0xBEEF)
    buf = [0.0] * int(2.1 * RATE)

    air(buf, 0.0, 0.34, 0.26, rng, rise=True)    # the intake before the hit
    sweep(buf, 0.30, 0.42, 55.0, 38.0, 0.85)     # the hit itself, falling
    air(buf, 0.32, 0.30, 0.34, rng)

    # A minor chord, struck as one: this is a cost, not a prize.
    for f, g in ((220.00, 0.42), (261.63, 0.34), (329.63, 0.30), (440.00, 0.24)):
        bell(buf, 0.33, f, g, 0.95)
    bell(buf, 0.62, 880.00, 0.16, 0.75)          # a late shimmer over the top
    write(os.path.join(OUT_DIR, "sfx_trait_terminal.wav"), buf)


def click() -> None:
    """Every button. Has to be SHORT and quiet — this one plays hundreds of times an hour,
    and the cue you hear most is the cue that has to stay out of the way."""
    rng = Noise(0x0C11)
    buf = [0.0] * int(0.11 * RATE)
    air(buf, 0.0, 0.03, 0.13, rng)
    bell(buf, 0.0, 1174.66, 0.20, 0.045, partials=(1.0, 2.0))   # D6, two partials only
    write(os.path.join(OUT_DIR, "sfx_click.wav"), buf)


def forge() -> None:
    """Refine, reforge, merge. A struck anvil: low body, bright ring, gone in half a beat."""
    rng = Noise(0xF0F0)
    buf = [0.0] * int(0.75 * RATE)
    air(buf, 0.0, 0.05, 0.30, rng)
    sweep(buf, 0.0, 0.09, 180.0, 90.0, 0.42)                     # the mass of the hammer
    bell(buf, 0.01, 1046.50, 0.34, 0.26, partials=(1.0, 2.76, 5.40, 8.93))  # metal, inharmonic
    bell(buf, 0.02, 1567.98, 0.18, 0.20, partials=(1.0, 2.76))
    write(os.path.join(OUT_DIR, "sfx_forge.wav"), buf)


def levelup() -> None:
    """A rank bought. Three notes UP — the one unambiguous "you gained something" shape."""
    rng = Noise(0x1EAF)
    buf = [0.0] * int(1.05 * RATE)
    air(buf, 0.0, 0.08, 0.18, rng)
    for i, f in enumerate((523.25, 659.25, 783.99)):             # C5 E5 G5, a major triad
        bell(buf, 0.02 + i * 0.075, f, 0.40 - i * 0.04, 0.40)
    sweep(buf, 0.0, 0.20, 260.0, 800.0, 0.16)
    write(os.path.join(OUT_DIR, "sfx_levelup.wav"), buf)


def conquer() -> None:
    """A region taken. The biggest cue the game has that is not a terminal: a swell, an
    impact, and a MAJOR chord that hangs — the mirror of trait_terminal's minor one."""
    rng = Noise(0xC0FE)
    buf = [0.0] * int(2.4 * RATE)
    air(buf, 0.0, 0.40, 0.28, rng, rise=True)
    sweep(buf, 0.34, 0.36, 70.0, 44.0, 0.80)
    air(buf, 0.36, 0.34, 0.30, rng)
    for f, g in ((261.63, 0.44), (329.63, 0.36), (392.00, 0.32), (523.25, 0.26)):
        bell(buf, 0.37, f, g, 1.05)                              # C major — a reward, not a price
    bell(buf, 0.70, 1046.50, 0.18, 0.85)
    write(os.path.join(OUT_DIR, "sfx_conquer.wav"), buf)


def deepen() -> None:
    """Corruption deepened. Descending, and it should NOT feel good — the player has just
    made the whole world harder in exchange for more."""
    rng = Noise(0xDEEB)
    buf = [0.0] * int(2.0 * RATE)
    air(buf, 0.0, 0.30, 0.24, rng, rise=True)
    sweep(buf, 0.24, 0.70, 120.0, 34.0, 0.66)                    # a long fall
    for f, g in ((196.00, 0.40), (233.08, 0.30), (293.66, 0.26)):  # G minor, low and close
        bell(buf, 0.26, f, g, 0.90)
    air(buf, 0.90, 0.50, 0.16, rng)
    write(os.path.join(OUT_DIR, "sfx_deepen.wav"), buf)


def weave() -> None:
    """A Source or a Form set on a slot. Deliberately SMALLER than sfx_click's cousins: this
    fires on every pick while a player is trying combinations, so it has to read as a soft
    'took' rather than an achievement. A short breath and one clean note, no low body — the
    weight in this screen is reserved for binding a Vow."""
    rng = Noise(0x3EA4)
    buf = [0.0] * int(0.34 * RATE)
    air(buf, 0.0, 0.04, 0.16, rng)
    bell(buf, 0.0, 880.00, 0.30, 0.13, partials=(1.0, 2.0, 3.0))   # A5, thin and quick
    bell(buf, 0.035, 1318.51, 0.16, 0.10, partials=(1.0, 2.0))     # E6 just behind it
    sweep(buf, 0.0, 0.07, 420.0, 900.0, 0.12)                      # a small lift, not a swell
    write(os.path.join(OUT_DIR, "sfx_weave.wav"), buf)


def bind() -> None:
    """A Vow bound to a skill. This is the screen's one COMMITMENT, so it is the one cue with
    weight: an intake, a low seal pressed down, and a fifth that hangs after it. A Vow is a
    restriction you accept for power, so the shape is a promise being closed rather than a
    prize being won — the chord is open (root and fifth, no third), which reads as solemn
    instead of happy."""
    rng = Noise(0xB17D)
    buf = [0.0] * int(1.35 * RATE)
    air(buf, 0.0, 0.16, 0.24, rng, rise=True)                      # the breath before it lands
    sweep(buf, 0.14, 0.26, 150.0, 62.0, 0.62)                      # the seal pressed into wax
    air(buf, 0.15, 0.10, 0.22, rng)
    bell(buf, 0.15, 293.66, 0.44, 0.62, partials=(1.0, 2.0, 3.0, 4.2))   # D4
    bell(buf, 0.15, 440.00, 0.32, 0.58, partials=(1.0, 2.0, 3.0))        # A4 — the open fifth
    bell(buf, 0.40, 880.00, 0.14, 0.55)                            # the ring that hangs after
    write(os.path.join(OUT_DIR, "sfx_bind.wav"), buf)


# ── The UI POLISH vocabulary (2026-09-01, brief §86): one family per meaning, not one click ────

def nav() -> None:
    """A nav-rail tile or a tab: the softest cue in the game. Lower and shorter than the button
    click — a page turning under a thumb, not a latch. It fires on every screen change, so it has
    to disappear into the act of looking somewhere else."""
    rng = Noise(0x0A71)
    buf = [0.0] * int(0.09 * RATE)
    air(buf, 0.0, 0.025, 0.10, rng)
    bell(buf, 0.0, 740.00, 0.14, 0.035, partials=(1.0, 2.0))   # F#5, two partials, gone in 35 ms
    write(os.path.join(OUT_DIR, "sfx_nav.wav"), buf)


def dispatch() -> None:
    """A DISPATCH ARRIVES: paper and a wax seal. A short breath of air and one low two-partial note,
    under 150 ms and quieter than everything else in the bank — this is BACKGROUND news, and the one
    cue it gets must not sound like a reward. Lower and softer than the nav tick it falls back to, so
    a letter landing while the player is reading something else is felt rather than announced."""
    rng = Noise(0xD15A)
    buf = [0.0] * int(0.15 * RATE)
    air(buf, 0.0, 0.05, 0.11, rng)                             # the paper
    bell(buf, 0.02, 392.00, 0.13, 0.075, partials=(1.0, 2.0))  # G4, two partials — the seal pressing
    write(os.path.join(OUT_DIR, "sfx_dispatch.wav"), buf)


def error() -> None:
    """A refusal: a locked tile, a button that cannot take the click, a code that will not paste.
    Dull on purpose — no ring, no rise — a knuckle on a closed door. Distinct from the click by
    being LOWER and DEADER, so "that did nothing" is heard rather than read."""
    rng = Noise(0xE770)
    buf = [0.0] * int(0.16 * RATE)
    air(buf, 0.0, 0.03, 0.14, rng)
    sweep(buf, 0.0, 0.07, 170.0, 105.0, 0.34)                  # a thud that sinks
    bell(buf, 0.0, 220.00, 0.16, 0.05, partials=(1.0, 1.5))    # a damped low note, no shimmer
    write(os.path.join(OUT_DIR, "sfx_error.wav"), buf)


def train() -> None:
    """A rank trained. Two quick notes UP a fourth, smaller and shorter than levelup's triad —
    this fires on every TRAIN click and a player trains ten ranks in a row; it has to satisfy
    without accumulating. The lift is the reward; the shortness is the mercy."""
    rng = Noise(0x7241)
    buf = [0.0] * int(0.30 * RATE)
    air(buf, 0.0, 0.04, 0.16, rng)
    bell(buf, 0.0, 659.25, 0.34, 0.12, partials=(1.0, 2.0, 3.0))    # E5
    bell(buf, 0.06, 880.00, 0.30, 0.14, partials=(1.0, 2.0, 3.0))   # A5, the fourth above
    sweep(buf, 0.0, 0.08, 330.0, 700.0, 0.10)
    write(os.path.join(OUT_DIR, "sfx_train.wav"), buf)


def reroll() -> None:
    """RE-ROLL: three quick blips falling then one clean note — a shuffle that settles. Lighter than
    the forge's anvil; this is chance, not craft."""
    rng = Noise(0x2E20)
    buf = [0.0] * int(0.36 * RATE)
    for i, f in enumerate((1318.51, 1174.66, 987.77)):          # E6 D6 B5, tumbling down
        bell(buf, i * 0.045, f, 0.16, 0.05, partials=(1.0, 2.0))
    air(buf, 0.13, 0.05, 0.14, rng)
    bell(buf, 0.15, 783.99, 0.30, 0.18, partials=(1.0, 2.0, 3.01))   # G5 — where it lands
    write(os.path.join(OUT_DIR, "sfx_reroll.wav"), buf)


def chest_open() -> None:
    """A chest opens: a wooden lid knock, a metal latch, a breath of air — over in under half a
    second because the VAULT opens chests by the dozen. The rarity layer (chest_rare) rides on
    top of it for Epic and Legendary; this one is the same for every chest."""
    rng = Noise(0xC4E5)
    buf = [0.0] * int(0.42 * RATE)
    sweep(buf, 0.0, 0.06, 260.0, 150.0, 0.36)                    # the lid's wooden knock
    air(buf, 0.0, 0.05, 0.22, rng)
    bell(buf, 0.07, 1046.50, 0.22, 0.06, partials=(1.0, 2.76))   # the latch, one bright tick
    air(buf, 0.10, 0.22, 0.14, rng, rise=False)                  # the air that comes out
    write(os.path.join(OUT_DIR, "sfx_chest_open.wav"), buf)


def chest_rare() -> None:
    """The rarity layer over chest_open for an Epic or Legendary chest: a slow glassy shimmer that
    hangs after the latch. Restrained — the dark-and-gold direction has no fireworks (brief §58)."""
    rng = Noise(0xC4A2)
    buf = [0.0] * int(0.90 * RATE)
    air(buf, 0.0, 0.20, 0.10, rng, rise=True)
    bell(buf, 0.06, 1567.98, 0.20, 0.55, partials=(1.0, 2.32, 4.25))   # G6, glass
    bell(buf, 0.14, 2093.00, 0.16, 0.50, partials=(1.0, 2.32))         # C7 behind it
    bell(buf, 0.26, 2637.02, 0.10, 0.45, partials=(1.0, 2.0))          # E7, the last spark
    write(os.path.join(OUT_DIR, "sfx_chest_rare.wav"), buf)


def shield_gain() -> None:
    """SHIELD gained: a cold shimmer — high glassy partials with a quick rise under them, and no
    low body at all, so it is felt as something forming around the hunter rather than something
    hitting them. The same glass as the gem cue, an octave up and colder."""
    rng = Noise(0x5A1E)
    buf = [0.0] * int(0.55 * RATE)
    sweep(buf, 0.0, 0.14, 600.0, 1800.0, 0.14)                   # the rise as it forms
    air(buf, 0.0, 0.12, 0.12, rng, rise=True)
    bell(buf, 0.08, 2093.00, 0.26, 0.30, partials=(1.0, 2.32, 4.25))   # C7, glass
    bell(buf, 0.12, 3135.96, 0.14, 0.26, partials=(1.0, 2.32))         # G7 above it
    write(os.path.join(OUT_DIR, "sfx_shield_gain.wav"), buf)


def shield_hit() -> None:
    """A bite absorbed by SHIELD: a small cold tick, over in a tenth of a second. The bar's movement
    does the telling (brief §69); this only says "that landed on the shield, not on you"."""
    rng = Noise(0x5A17)
    buf = [0.0] * int(0.12 * RATE)
    air(buf, 0.0, 0.02, 0.16, rng)
    bell(buf, 0.0, 1396.91, 0.22, 0.045, partials=(1.0, 2.32, 4.25))   # F6, glassy and short
    write(os.path.join(OUT_DIR, "sfx_shield_hit.wav"), buf)


if __name__ == "__main__":
    trait_lit()
    trait_terminal()
    click()
    forge()
    levelup()
    conquer()
    deepen()
    weave()
    bind()
    nav()
    dispatch()
    error()
    train()
    reroll()
    chest_open()
    chest_rare()
    shield_gain()
    shield_hit()
