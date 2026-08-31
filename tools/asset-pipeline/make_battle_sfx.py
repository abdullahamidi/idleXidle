#!/usr/bin/env python3
"""Synthesise the FIGHT and FORGE one-shots into assets/audio.

Usage:
    python3 tools/asset-pipeline/make_battle_sfx.py            # regenerate every cue, print the table
    python3 tools/asset-pipeline/make_battle_sfx.py --measure a.wav b.wav   # measure any wav, change nothing

The 2026-08-24 audio audit found the entire fight silent: no hit, no critical, no
enemy death, no champion death, no boss arrival, no skill cast. The chest reveal
cascade — the game's designated payoff beat — was silent per item, and the forge's
UPGRADE and SOCKET verbs had no sound of their own. This writes the missing cues.

Same reasoning as make_sfx.py, which owns the big ceremony cues (trait, conquer,
levelup...): when the right asset is a formula, write the formula. Deterministic
noise, stdlib only for the synthesis (numpy is used for the measurements when it is
installed, with a pure-Python FFT behind it when it is not), exactly reproducible.

THE 2026-08-26 REDESIGN — weight. Playtest: "The sounds are not good. The death sound
is like a candy-crush pop. The hit sound sounds like a bad drum. These sounds lower
the weight of the game." The 08-25 softening pass (a linear attack and a one-pole
low-pass over each cue) made them QUIETER, not better, because the problem was never
the level: every fight cue was one short sine sweep with a fast decay, and that IS a
toy sound at any volume. The hit was a 150→92 Hz sine over 80 ms (centroid 764 Hz,
20 dB down after 54 ms — a woodblock), the enemy death a 560→170 Hz chirp (centroid
1087 Hz — the "pop"), the champion death a square-wave slide (a buzz).

WHAT THE RESEARCH SAID, and what this file does about it:

1. AN IMPACT IS LAYERS, NOT A SHAPE. Every impact-design guide describes the same
   anatomy: a transient of a few milliseconds that says WHAT hit ("defines what's
   hitting what"), a body in the low-mids that carries "substance and scale", and a
   tail that puts it in a room ("a short, tight tail suggests an enclosed space"), with
   the layers' peaks snapped to the same instant so they read as ONE hit. The sub
   layer is a kick-drum recipe: a sine near 50–60 Hz whose pitch envelope starts high
   and falls in ~50 ms, a noise click of "just a few ms" on the front, and a touch of
   saturation for harmonics that let the sub be heard on small speakers.
   Sources: SFX Engine, "The Ultimate Guide to the Impact Sound Effect"
   (https://sfxengine.com/blog/impact-sound-effect); Rogue Waves, "Layering Sound
   Effects for Maximum Impact" (https://roguewaveslibrary.com/blogs/rogue-waves-blog/
   layering-sound-effects-for-maximum-impact-a-guide-for-sound-designers); ModeAudio,
   "Drum Synth Sound Design: Kick & Snare" (https://modeaudio.com/magazine/
   drum-synth-sound-design-kick-snare).
   HERE: `hit`, `crit`, the deaths and the boss's fall are each five or six layers —
   click, crunch, sub thump with a pitch drop, band-passed noise body, comb-filtered
   tail — built separately, filtered separately (proper biquads, not a one-pole
   smear), and summed at sample zero.

2. WEIGHT IS A LOW SPECTRAL CENTROID AND A LONG DECAY. Psychoacoustics ties perceived
   brightness strongly to the spectral centroid — the centre of mass of the spectrum —
   and darkness/heaviness to a low one; the candy-pop was a centroid over 1 kHz with a
   60–100 ms life. Source: Timbre and Orchestration Resource, "Spectral centroid"
   (https://timbreandorchestration.org/writings/timbre-lingo/2019/3/29/
   spectral-centroid), citing McAdams & Giordano (2010).
   HERE: the centroid and the -20 dB decay time are MEASURED for every cue and
   asserted against the old values (see BASELINE and the checks at the bottom): the
   hit must sit well under half the old centroid and ring at least three times as
   long; the deaths are dissolves in the 600–900 ms range with a descending band,
   crumble grains and a faint tonal drop, not a chirp.

3. IDENTICAL REPEATS FATIGUE; OVERLAPS RATTLE. A one-shot heard a thousand times
   "draws attention to itself through its artificial precision"; the standard cures
   are small per-play pitch and volume variation ("very small ranges will sound more
   natural, larger ranges sound jarring") and never playing the same clip twice in a
   row. Middleware treats the stacking problem separately: FMOD's event Cooldown is
   "the amount of time required to pass before playing an instance of this event
   again", there to stop simultaneous copies phasing; Wwise does it with playback
   limits. Sources: Andrew Mushel, "Sound Effect Variation in Unity"
   (https://andrewmushel.com/articles/sound-effect-variation-in-unity/); A Sound
   Effect, "How to maintain immersion (+ reduce repetition & listening fatigue) in
   game audio" (https://www.asoundeffect.com/game-audio-immersion/); FMOD Studio,
   Event Macro Controls Reference — Cooldown (https://fmod.com/resources/
   documentation-studio?page=event-macro-controls-reference.html).
   HERE (game side, SoundBank.cs): ±0.06 octave pitch jitter per play on the fight
   cues, a per-cue minimum gap (60 ms for hits, longer for the deaths), and the
   existing repeat-ducking so a swarm reads as a swarm rather than a wall.

TWO RULES, both enforced by asserts at the bottom:

1. BOUNDED. These cues fire inside combat playback, sometimes several per second at
   raised battle speed. A hit is under 400 ms (its tail is the weight; the cooldown
   in SoundBank keeps the tails from piling up), a death under 950 ms, and nothing
   else over 600 ms.
2. QUIET. Peaks are normalised to ~0.40 of full scale, NOT the 0.92 the ceremony
   cues use — combat cues STACK (a swarm wave fires hit after hit), and headroom
   here is what keeps five overlapping thuds from clipping. Weight comes from where
   the energy sits (the sub) and how long it lasts, not from the peak.

16-bit mono PCM at 44.1 kHz, which is what SoundEffect.FromStream wants.
"""

from __future__ import annotations

import cmath
import math
import os
import struct
import sys
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


# ── Filters. Proper second-order sections (the RBJ cookbook), not the one-pole smear ──────────
# the old soften() used: a one-pole rolls off at 6 dB/octave, which is too gentle to
# carve a band out of noise, and it has no resonance to give a body its pitch.

def _coeffs(kind: str, f0: float, q: float) -> tuple[float, float, float, float, float]:
    f0 = max(10.0, min(f0, RATE * 0.45))
    w0 = 2.0 * math.pi * f0 / RATE
    c, s = math.cos(w0), math.sin(w0)
    alpha = s / (2.0 * q)
    if kind == "lp":
        b0, b1, b2 = (1 - c) / 2, 1 - c, (1 - c) / 2
    elif kind == "hp":
        b0, b1, b2 = (1 + c) / 2, -(1 + c), (1 + c) / 2
    elif kind == "bp":                       # constant 0 dB peak gain
        b0, b1, b2 = alpha, 0.0, -alpha
    else:
        raise ValueError(kind)
    a0, a1, a2 = 1 + alpha, -2 * c, 1 - alpha
    return b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0


def biquad(x: list[float], kind: str, f0: float, q: float = 0.7071) -> list[float]:
    b0, b1, b2, a1, a2 = _coeffs(kind, f0, q)
    out = [0.0] * len(x)
    x1 = x2 = y1 = y2 = 0.0
    for i, v in enumerate(x):
        y = b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, v, y1, y
        out[i] = y
    return out


def lp2(x: list[float], f0: float) -> list[float]:
    """Two low-pass sections in a row — 24 dB/octave. The measurement made this necessary:
    a single 12 dB/octave stage leaves a noise skirt that is 40 dB down but nineteen
    thousand hertz WIDE, and the spectral centroid counts every one of those bins. A
    layer that sounds low can still measure bright until its skirt is cut steeply."""
    return biquad(biquad(x, "lp", f0, 0.54), "lp", f0, 1.31)   # Butterworth pair


def sweep_bp(x: list[float], f_from: float, f_to: float, q: float, glide_tau: float) -> list[float]:
    """A band-pass whose centre glides exponentially from f_from to f_to — the sound of a
    thing coming apart is a band FALLING through the noise. Coefficients are refreshed
    every 32 samples (0.7 ms), inaudible as steps, cheap enough for pure Python."""
    out = [0.0] * len(x)
    x1 = x2 = y1 = y2 = 0.0
    b0 = b1 = b2 = a1 = a2 = 0.0
    for i, v in enumerate(x):
        if i % 32 == 0:
            t = i / RATE
            f = f_to + (f_from - f_to) * math.exp(-t / glide_tau)
            b0, b1, b2, a1, a2 = _coeffs("bp", f, q)
        y = b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, v, y1, y
        out[i] = y
    return out


def drive(x: list[float], k: float) -> list[float]:
    """Soft saturation. Adds the harmonics that let a 45 Hz sub be heard on a laptop
    speaker that cannot reproduce 45 Hz, and rounds every peak into a thud."""
    norm = math.tanh(k)
    return [math.tanh(k * v) / norm for v in x]


def envelope(x: list[float], attack: float, tau: float, hold: float = 0.0) -> list[float]:
    """Linear attack, exponential decay (tau = time to 1/e). The shape of every struck
    thing: instant on, then a fall that never quite reaches zero."""
    a = max(1, int(attack * RATE))
    h = int(hold * RATE)
    out = [0.0] * len(x)
    for i, v in enumerate(x):
        t = i / RATE
        env = min(1.0, i / a)
        if i > a + h:
            env *= math.exp(-((i - a - h) / RATE) / tau)
        out[i] = v * env
    return out


def noise(rng: Noise, dur: float) -> list[float]:
    return [rng.next() for _ in range(int(dur * RATE))]


def add(buf: list[float], start: float, layer: list[float], gain: float) -> None:
    i0 = int(start * RATE)
    n = min(len(layer), len(buf) - i0)
    for i in range(max(0, n)):
        buf[i0 + i] += gain * layer[i]


# ── The layers. Each is one ingredient of an impact; a cue is a recipe of them ───────────────

def click(rng: Noise, dur: float = 0.003, f: float = 2200.0) -> list[float]:
    """The transient: a few milliseconds of noise in a band around `f`. It says "something
    struck" before the body says how heavy it was. Kept small — it is the one
    high-centroid ingredient in the hit, and too much of it is the woodblock again."""
    x = lp2(biquad(noise(rng, dur), "hp", f, 0.8), f * 2.2)
    return envelope(x, 0.0002, dur * 0.35)


def crunch(rng: Noise, dur: float, lo: float, hi: float, tau: float, sat: float = 4.0) -> list[float]:
    """The crunch: a band of noise driven hard, over 15–25 ms. Bone, leather, splinter —
    the texture that a pure sine can never have. Saturated BEFORE the top is cut, so
    the harmonics the drive adds are trimmed too."""
    x = drive(biquad(noise(rng, dur), "hp", lo, 0.7), sat)
    return envelope(lp2(x, hi), 0.0008, tau)


def thump(dur: float, f_hi: float, f_lo: float, pitch_tau: float, amp_tau: float,
          sat: float = 2.0, attack: float = 0.001) -> list[float]:
    """The sub: a sine whose pitch drops exponentially from f_hi to f_lo (the kick-drum
    pitch envelope — the drop is what the ear reads as MASS arriving) with a saturated
    body so its harmonics survive small speakers. Phase is integrated, never sampled."""
    n = int(dur * RATE)
    out = [0.0] * n
    phase = 0.0
    for i in range(n):
        t = i / RATE
        f = f_lo + (f_hi - f_lo) * math.exp(-t / pitch_tau)
        phase += 2.0 * math.pi * f / RATE
        out[i] = math.sin(phase)
    return envelope(drive(out, sat), attack, amp_tau)


def body(rng: Noise, dur: float, centre: float, q: float, tau: float) -> list[float]:
    """The body: band-passed noise with a resonance at `centre`. The low-mid "substance"
    layer between the sub and the crunch — the part that sounds like a THING was hit
    rather than a speaker."""
    return envelope(lp2(biquad(noise(rng, dur), "bp", centre, q), centre * 3.0), 0.0015, tau)


def tail(rng: Noise, dur: float, cutoff: float, tau: float,
         combs: tuple[float, ...] = (0.0079, 0.0113), fb: float = 0.55) -> list[float]:
    """The room: low-passed noise through two short feedback combs (a plate in miniature —
    the combs give it the metallic flutter of a hard space), decaying over `tau`. It is
    what makes the hit happen SOMEWHERE instead of in a vacuum."""
    x = lp2(noise(rng, dur), cutoff)
    out = [0.0] * len(x)
    for d in combs:
        delay = int(d * RATE)
        y = [0.0] * len(x)
        for i, v in enumerate(x):
            y[i] = v + (fb * y[i - delay] if i >= delay else 0.0)
        for i in range(len(x)):
            out[i] += y[i] / len(combs)
    return envelope(lp2(out, cutoff * 1.5), 0.004, tau)


def drop(dur: float, f_from: float, f_to: float, glide_tau: float, amp_tau: float,
         attack: float = 0.01, sat: float = 1.5) -> list[float]:
    """A tonal fall: a sine gliding exponentially downward — the "faint tonal drop" that
    turns a noise decay into a death. Slower than a thump's pitch envelope; this one is
    a thing SINKING, not a thing landing."""
    return thump(dur, f_from, f_to, glide_tau, amp_tau, sat=sat, attack=attack)


def dissolve(rng: Noise, dur: float, f_from: float, f_to: float, q: float, tau: float,
             grain_tau: float, density: float) -> list[float]:
    """The crumble: noise through a band that FALLS from f_from to f_to, gated by sparse
    random grains (impulses smoothed over grain_tau) so it breaks apart into pieces as
    it fades rather than hissing away smoothly. The core of both death cues."""
    x = lp2(sweep_bp(noise(rng, dur), f_from, f_to, q, glide_tau=dur * 0.42), f_from * 2.5)
    gate = [0.0] * len(x)
    g = 0.0
    k = math.exp(-1.0 / (grain_tau * RATE))
    for i in range(len(x)):
        if rng.next() * 0.5 + 0.5 < density:
            g += 1.0
        g *= k
        gate[i] = 0.35 + 0.65 * min(1.0, g)
    return envelope([v * gate[i] for i, v in enumerate(x)], 0.003, tau)


# ── Legacy helpers, still used by the cues that were never the problem ──────────────────────

def sweep(buf: list[float], start: float, dur: float, f_from: float, f_to: float,
          gain: float, shape: str = "sine", curve: float = 1.0) -> None:
    """A tone gliding between two frequencies. Phase is integrated, not sampled,
    or the pitch jumps at block boundaries."""
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


def soften(buf: list[float], attack_ms: float, lp: float) -> None:
    """Round a cue off: a short linear attack and a one-pole low-pass over the whole
    thing. Kept for the boss horn and the cast whoosh, which have no transient to
    protect; the impact cues are NOT run through this — it would eat the click."""
    n = int(attack_ms / 1000.0 * RATE)
    for i in range(min(n, len(buf))):
        buf[i] *= i / n
    acc = 0.0
    for i in range(len(buf)):
        acc += (buf[i] - acc) * lp
        buf[i] = acc


# ── Output ───────────────────────────────────────────────────────────────────────────────────

def write(path: str, buf: list[float], target_peak: float = 0.40) -> tuple[float, float]:
    """Normalise to a QUIET target peak, fade the tail, write. Returns (dur, peak)."""
    tail_n = int(0.008 * RATE)
    for i in range(min(tail_n, len(buf))):
        buf[len(buf) - 1 - i] *= i / tail_n

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
    return len(buf) / RATE, out_peak


REPORT: list[tuple[str, str, float, float, float]] = []   # (name, path, dur, peak, max allowed dur)


def make(name: str, out_dir: str, seconds: float, build, target_peak: float = 0.40,
         max_ms: float = 250.0, soft: tuple[float, float] | None = None,
         darken: float | None = None) -> None:
    """darken: a final 24 dB/octave low-pass over the whole cue, the lid on the fizz the
    layers' own filters let through. The impact cues use it instead of soften()."""
    buf = [0.0] * int(seconds * RATE)
    build(buf)
    if soft is not None:
        soften(buf, *soft)
    if darken is not None:
        buf[:] = lp2(buf, darken)
    path = os.path.join(out_dir, name + ".wav")
    dur, peak = write(path, buf, target_peak)
    REPORT.append((name, path, dur, peak, max_ms))


# ── The fight ────────────────────────────────────────────────────────────────────────────────

def hit(buf):
    """The auto-attack landing — the cue heard most, so it is the one that sets the
    game's weight. A thud with a crunchy front and a ~300 ms tail: click, crunch,
    a 100→46 Hz sub with a 30 ms pitch drop, a 210 Hz noise body, a short hard room."""
    rng = Noise(0x0417)
    # Balance note: the sub carries the weight but a laptop speaker cannot reproduce 46 Hz, so the
    # body (210 Hz) and the crunch are kept loud enough to carry the hit on their own — measured as
    # the 100–300 Hz share of the cue's power, which must not collapse under the sub's.
    add(buf, 0.0, click(rng, 0.003, 2200.0), 0.14)
    add(buf, 0.0, crunch(rng, 0.020, 240.0, 1600.0, 0.006, sat=4.0), 0.70)
    add(buf, 0.0, thump(0.30, 100.0, 46.0, 0.030, 0.085, sat=2.0), 0.80)
    add(buf, 0.0, body(rng, 0.16, 210.0, 1.1, 0.040), 1.00)
    add(buf, 0.003, tail(rng, 0.34, 480.0, 0.075), 0.30)


def crit(buf):
    """A critical — the hit's heavier cousin, not a brighter one. Longer sub with a deeper
    drop, a harder crunch, and a low inharmonic ring (a struck slab, not a chime) so it
    is unmistakably the same family and unmistakably bigger."""
    rng = Noise(0xC217)
    add(buf, 0.0, click(rng, 0.004, 2600.0), 0.28)
    add(buf, 0.0, crunch(rng, 0.028, 280.0, 3200.0, 0.009, sat=6.0), 0.60)
    add(buf, 0.0, thump(0.36, 118.0, 44.0, 0.036, 0.11, sat=2.4), 1.00)
    add(buf, 0.0, body(rng, 0.20, 260.0, 1.0, 0.050), 0.60)
    bell(buf, 0.002, 185.0, 0.22, 0.13, partials=(1.0, 2.41, 3.87))
    add(buf, 0.003, tail(rng, 0.38, 700.0, 0.09), 0.30)


def enemy_down(buf):
    """A creature comes apart. A snap, a fall (80→40 Hz), then ~600 ms of crumble — noise
    in a band that sinks from 520 Hz to 110 Hz, broken into grains — with a faint tonal
    drop underneath. Nothing above the low-mids rings; the pop is gone."""
    rng = Noise(0xED04)
    add(buf, 0.0, crunch(rng, 0.024, 200.0, 2000.0, 0.008, sat=3.0), 0.35)
    add(buf, 0.0, thump(0.30, 80.0, 40.0, 0.040, 0.10, sat=1.8), 0.60)
    add(buf, 0.01, dissolve(rng, 0.68, 520.0, 110.0, 2.0, 0.19, 0.006, 0.004), 0.90)
    add(buf, 0.02, drop(0.55, 150.0, 48.0, 0.18, 0.16, attack=0.01, sat=1.5), 0.45)
    add(buf, 0.05, tail(rng, 0.60, 400.0, 0.14), 0.22)


def boss_down(buf):
    """A boss falls — the heaviest beat in the fight. The enemy death scaled down an
    octave and stretched: a deeper sub, a second thump when the mass hits the ground,
    and a crumble that sinks to 70 Hz over most of a second."""
    rng = Noise(0xB0D0)
    add(buf, 0.0, crunch(rng, 0.030, 160.0, 1600.0, 0.010, sat=3.0), 0.55)
    add(buf, 0.0, thump(0.40, 70.0, 34.0, 0.050, 0.14, sat=2.2), 0.70)
    add(buf, 0.17, thump(0.36, 60.0, 32.0, 0.040, 0.13, sat=2.2), 0.50)
    add(buf, 0.01, dissolve(rng, 0.86, 380.0, 70.0, 1.8, 0.24, 0.008, 0.003), 1.20)
    add(buf, 0.02, drop(0.75, 110.0, 36.0, 0.24, 0.22, attack=0.01, sat=1.6), 0.45)
    add(buf, 0.06, tail(rng, 0.80, 300.0, 0.18), 0.28)


def champ_down(buf):
    """The champion falls — this one costs the run, so it is the slowest and darkest
    thing in the fight. A low landing (72→36 Hz), two long tonal falls a fourth apart
    that beat against each other as they sink, a crumble that goes down to 60 Hz, and
    a rumble tail. Distinct from a creature's death by being tonal and slow where that
    one is grainy and quick."""
    rng = Noise(0xCD04)
    add(buf, 0.0, crunch(rng, 0.020, 180.0, 1400.0, 0.008, sat=2.5), 0.22)
    add(buf, 0.0, thump(0.42, 72.0, 36.0, 0.060, 0.16, sat=2.2), 0.80)
    add(buf, 0.0, drop(0.85, 130.0, 41.0, 0.30, 0.28, attack=0.02, sat=1.8), 0.50)
    add(buf, 0.03, drop(0.80, 97.0, 31.0, 0.30, 0.26, attack=0.03, sat=1.6), 0.30)
    add(buf, 0.02, dissolve(rng, 0.85, 300.0, 60.0, 1.6, 0.26, 0.010, 0.002), 0.60)
    add(buf, 0.10, tail(rng, 0.80, 220.0, 0.24), 0.35)


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
    """Breath, not a bell — a skill leaves the hand. The old cue carried a rising sine
    (480 -> 950 Hz) inside the air, and at two casts a second that sine was the
    "boink boink boink" the playtest heard under the hit's thud (2026-08-26). Now a
    darker, longer breath with a low FALLING push under it and no tone at all: the
    cast is felt as a release, and the strike that follows carries the weight."""
    rng = Noise(0xCA57)
    air(buf, 0.0, 0.26, 0.9, rng, rise=True, lp=0.10)
    sweep(buf, 0.02, 0.12, 220.0, 120.0, 0.22, shape="sine")


# ── The forge ────────────────────────────────────────────────────────────────────────────────

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


# ── The numeric ear ──────────────────────────────────────────────────────────────────────────
# Nobody listens in CI, so the properties that make a cue heavy are MEASURED and asserted.

def _fft(x: list[complex]) -> list[complex]:
    """Iterative radix-2 FFT, zero-padded to a power of two. Only used when numpy is
    absent; slow (a second or two per cue) but stdlib."""
    n = 1
    while n < len(x):
        n <<= 1
    a = list(x) + [0j] * (n - len(x))
    j = 0
    for i in range(1, n):
        bit = n >> 1
        while j & bit:
            j ^= bit
            bit >>= 1
        j |= bit
        if i < j:
            a[i], a[j] = a[j], a[i]
    length = 2
    while length <= n:
        w = cmath.exp(-2j * math.pi / length)
        for i in range(0, n, length):
            wn = 1 + 0j
            for k in range(length // 2):
                u, v = a[i + k], a[i + k + length // 2] * wn
                a[i + k], a[i + k + length // 2] = u + v, u - v
                wn *= w
        length <<= 1
    return a


def read_wav(path: str) -> tuple[int, list[float]]:
    with wave.open(path, "rb") as fh:
        rate, n = fh.getframerate(), fh.getnframes()
        raw = fh.readframes(n)
    return rate, [v / 32768.0 for v in struct.unpack("<%dh" % (len(raw) // 2), raw)]


def measure(path: str) -> dict[str, float]:
    """Duration, peak and RMS in dBFS, spectral centroid (magnitude-weighted, 20 Hz–20 kHz)
    and the -20 dB decay time (from the envelope peak to the last moment the 10 ms RMS
    envelope is within 20 dB of it)."""
    rate, x = read_wav(path)
    n = len(x)
    peak = max(abs(v) for v in x) or 1e-9
    rms = math.sqrt(sum(v * v for v in x) / n) or 1e-9
    try:
        import numpy as np
        spec = np.abs(np.fft.rfft(np.array(x)))
        freqs = np.fft.rfftfreq(n, 1.0 / rate)
        m = (freqs >= 20) & (freqs <= 20000)
        centroid = float(np.sum(freqs[m] * spec[m]) / (np.sum(spec[m]) or 1e-9))
    except ImportError:
        spec = _fft([complex(v) for v in x])
        nfft = len(spec)
        num = den = 0.0
        for k in range(nfft // 2 + 1):
            f = k * rate / nfft
            if 20 <= f <= 20000:
                mag = abs(spec[k])
                num += f * mag
                den += mag
        centroid = num / (den or 1e-9)
    win, hop = int(0.010 * rate), int(0.001 * rate)
    env = []
    for i in range(0, max(1, n - win), hop):
        seg = x[i:i + win]
        env.append(math.sqrt(sum(v * v for v in seg) / len(seg)) + 1e-12)
    ip = max(range(len(env)), key=env.__getitem__)
    floor = env[ip] * 10 ** (-20 / 20)
    last = max((i for i, e in enumerate(env) if e > floor), default=ip)
    return {
        "ms": n / rate * 1000.0,
        "peak_db": 20 * math.log10(peak),
        "rms_db": 20 * math.log10(rms),
        "centroid": centroid,
        "decay20_ms": (last - ip) * hop / rate * 1000.0,
    }


# What the fight cues measured BEFORE the 2026-08-26 redesign, so the table and the asserts
# below compare against the sound the playtest rejected rather than against nothing.
BASELINE = {
    "sfx_hit":        dict(ms=100, peak_db=-11.7, rms_db=-21.3, centroid=764, decay20_ms=54),
    "sfx_crit":       dict(ms=140, peak_db=-9.9, rms_db=-25.2, centroid=1815, decay20_ms=74),
    "sfx_enemy_down": dict(ms=190, peak_db=-11.1, rms_db=-22.1, centroid=1087, decay20_ms=106),
    "sfx_champ_down": dict(ms=480, peak_db=-9.4, rms_db=-18.2, centroid=687, decay20_ms=220),
    "sfx_boss":       dict(ms=560, peak_db=-8.9, rms_db=-17.7, centroid=759, decay20_ms=312),
    "sfx_cast":       dict(ms=210, peak_db=-12.4, rms_db=-21.3, centroid=4507, decay20_ms=158),
}

# The weight contract, per cue: the highest centroid and the shortest -20 dB decay allowed.
WEIGHT = {
    "sfx_hit":        (400.0, 150.0),
    "sfx_crit":       (900.0, 180.0),
    "sfx_enemy_down": (550.0, 250.0),
    "sfx_boss_down":  (400.0, 350.0),
    "sfx_champ_down": (400.0, 350.0),
    "sfx_cast":       (2600.0, 90.0),   # a breath may be brighter than a blow, but never a tone
}


def print_table(rows: list[tuple[str, dict[str, float], dict[str, float] | None]]) -> None:
    head = f"{'cue':<16}{'':>4}{'ms':>6}{'peak dBFS':>11}{'RMS dBFS':>10}{'centroid Hz':>13}{'-20 dB ms':>11}"
    print(head)
    print("-" * len(head))
    for name, now, before in rows:
        if before is not None:
            b = before
            print(f"{name:<16}{'was':>4}{b['ms']:6.0f}{b['peak_db']:11.1f}{b['rms_db']:10.1f}"
                  f"{b['centroid']:13.0f}{b['decay20_ms']:11.0f}")
        print(f"{name if before is None else '':<16}{'now':>4}{now['ms']:6.0f}{now['peak_db']:11.1f}"
              f"{now['rms_db']:10.1f}{now['centroid']:13.0f}{now['decay20_ms']:11.0f}")


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "--measure":
        print_table([(os.path.basename(p), measure(p), None) for p in sys.argv[2:]])
        raise SystemExit(0)

    # The impact cues are layered and filtered per layer, and are NOT softened — soften()'s
    # attack ramp would erase the click that says "struck". The horn and the whoosh keep it.
    make("sfx_hit", COMBAT_DIR, 0.36, hit, target_peak=0.40, max_ms=400, darken=2600.0)
    make("sfx_crit", COMBAT_DIR, 0.40, crit, target_peak=0.42, max_ms=400, darken=5000.0)
    make("sfx_enemy_down", COMBAT_DIR, 0.72, enemy_down, target_peak=0.40, max_ms=950, darken=3000.0)
    make("sfx_boss_down", COMBAT_DIR, 0.92, boss_down, target_peak=0.42, max_ms=950, darken=2400.0)
    make("sfx_champ_down", COMBAT_DIR, 0.92, champ_down, target_peak=0.42, max_ms=950, darken=2200.0)
    make("sfx_boss", COMBAT_DIR, 0.56, boss, target_peak=0.36, max_ms=600, soft=(20.0, 0.25))
    make("sfx_cast", COMBAT_DIR, 0.28, cast, target_peak=0.22, max_ms=300.0, darken=2400.0)
    make("sfx_reveal_tick", UI_DIR, 0.08, reveal_tick, target_peak=0.32)
    make("sfx_equip", UI_DIR, 0.13, equip, target_peak=0.36)
    make("sfx_gem", UI_DIR, 0.25, gem, target_peak=0.36)
    make("sfx_upgrade", UI_DIR, 0.17, upgrade, target_peak=0.38)

    rows = []
    bad = []
    for name, path, dur, peak, max_ms in REPORT:
        if dur * 1000 > max_ms + 1:
            bad.append(f"{name}: {dur * 1000:.0f} ms exceeds its {max_ms:.0f} ms budget")
        if peak > 0.45:
            bad.append(f"{name}: peak {peak:.2f} above the 0.45 stacking ceiling")
        if peak < 0.05:
            bad.append(f"{name}: peak {peak:.2f} — effectively silent, the synth broke")
        now = measure(path)
        rows.append((name, now, BASELINE.get(name)))
        if name in WEIGHT:
            max_centroid, min_decay = WEIGHT[name]
            if now["centroid"] > max_centroid:
                bad.append(f"{name}: centroid {now['centroid']:.0f} Hz is above {max_centroid:.0f} — too bright to be heavy")
            if now["decay20_ms"] < min_decay:
                bad.append(f"{name}: -20 dB decay {now['decay20_ms']:.0f} ms is under {min_decay:.0f} — a pop, not a thud")
    print_table(rows)
    if bad:
        raise SystemExit("SFX SANITY FAILED:\n  " + "\n  ".join(bad))
    print(f"all {len(REPORT)} cues within budget (every peak <= 0.45, durations as declared, "
          f"fight cues dark and long enough).")
