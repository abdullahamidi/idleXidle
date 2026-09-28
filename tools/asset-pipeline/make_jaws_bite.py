#!/usr/bin/env python3
"""make_jaws_bite.py -- JAWS' bite, built from REAL recorded foley (ADR-011, 2026-09-28).

The owner rejected three synthesised bites ("all similar; I want a REAL bite, like Trundle's Q (Chomp) in League of
Legends"). A reference analysis of that sound (done locally, for measurement only; Riot's audio is never sampled,
shipped or used as a template source) found ONE dense block of ~500 ms in overlapping phases:
  A  TEAR / CRUNCH (0-150 ms): broadband crunch, hard onset, chopped into 4-5 chewing bursts 28-40 ms apart;
  B  WEIGHT (~120-330 ms): a sub whose pitch drops ~170 -> 60 Hz in 40-60 ms and settles at 35-45 Hz, the loudest band;
     the crunch keeps going under it in sparser bursts;
  C  WET: 1-3 short resonant squelch blips at 800-1100 Hz, each falling 5-10 % in pitch;
  D  TAIL (350-550 ms): a few isolated cartilage/debris ticks at 2-9 kHz over the sub's decay.
Dense (peak-to-RMS over the loudest 300 ms ~8-9.5 dB), ~30 % of its energy above 2 kHz and ~45 % below 150 Hz.
The synthesised cues had 1.6 % above 2 kHz, no chewing texture and no pitch-dropping sub: they read as a thud.

Here the crunch, the teeth, the wet and the ticks are REAL recordings (CC0 1.0, Freesound and OpenGameArt; see
foley/jaws_bite/SOURCES.md); only the sub drop and the resonant blips are synthesised. Three candidates that differ in
STRUCTURE, not just pitch: A CHOMP (the reference's shape), B BONE (tight: crunch peaks early, weight at ~65 ms),
C JUICY (a soft ~90 ms ramp, the wettest).

A IS HUMAN-APPROVED (the owner, 2026-09-28: "A is perfect, exactly what I wanted ... a gold standard"): the approved
bytes of assets/audio/combat/sfx_seeker_jaws_bite.wav are pinned by SHA-256 in jaws_reaction_test.cs. Do not change
chomp(), its sources or the shared DSP without the owner's ear: a different file is a new approval, not a regeneration.

    PYTHONUTF8=1 python tools/asset-pipeline/make_jaws_bite.py --extract <dir of the downloaded sources>   # once
    PYTHONUTF8=1 python tools/asset-pipeline/make_jaws_bite.py                                            # build
"""
from __future__ import annotations

import os
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
FOLEY = os.path.join(HERE, "foley", "jaws_bite")
COMBAT = os.path.join(REPO, "assets", "audio", "combat")
CANDIDATES = os.path.join(REPO, "production", "qa", "evidence", "jaws-real-bite", "candidates")
SR = 44100
PEAK = 0.34                     # the house peak for this cue (-9.4 dBFS), as the other JAWS cues
SEED = 20260928

# The excerpts: (name, downloaded file, start s, end s, title, author, url). Every source is CC0 1.0.
EXCERPTS = [
    ("teeth_snap", "freesound/fs527489_teeth_snapping_lucasduff.wav", 3.699, 3.780, "Teeth Snapping.mp3", "LucasDuff", "https://freesound.org/people/LucasDuff/sounds/527489/"),
    ("teeth_snap_b", "freesound/fs527489_teeth_snapping_lucasduff.wav", 3.225, 3.315, "Teeth Snapping.mp3", "LucasDuff", "https://freesound.org/people/LucasDuff/sounds/527489/"),
    ("cabbage_bite", "freesound/fs404691_crunchy_cabbage_bites_chestnutjam.wav", 1.490, 1.810, "crunchy cabbage bites, chewing", "chestnutjam", "https://freesound.org/people/chestnutjam/sounds/404691/"),
    ("cabbage_dense", "freesound/fs404691_crunchy_cabbage_bites_chestnutjam.wav", 0.290, 0.950, "crunchy cabbage bites, chewing", "chestnutjam", "https://freesound.org/people/chestnutjam/sounds/404691/"),
    ("flesh_bite", "freesound/fs155973_zombie_flesh_bites_mrpokephile.wav", 9.300, 9.640, "Zombie Flesh Bites", "MrPokephile", "https://freesound.org/people/MrPokephile/sounds/155973/"),
    ("flesh_bite_body", "freesound/fs155973_zombie_flesh_bites_mrpokephile.wav", 4.790, 5.320, "Zombie Flesh Bites", "MrPokephile", "https://freesound.org/people/MrPokephile/sounds/155973/"),
    ("pepper_cracks", "freesound/fs831735_zombie_apple_pepper_bite_crimsonblaze.wav", 0.025, 0.460, "Zombie Bite/Apple Bite/Bell Pepper Bite", "Crimsonblaze", "https://freesound.org/people/Crimsonblaze/sounds/831735/"),
    ("bone_bite", "freesound/fs445987_zombie_biting_on_bones_breviceps.wav", 0.130, 0.570, "Horror / Zombie - Biting on bones", "Breviceps", "https://freesound.org/people/Breviceps/sounds/445987/"),
    ("nut_crack", "freesound/fs272239_nut_crunch_chomp_spanrucker.wav", 0.240, 0.500, "nut crunch chomp", "spanrucker", "https://freesound.org/people/spanrucker/sounds/272239/"),
    ("gore_crunch", "freesound/fs784768_deathcrunch_gore_akkingstudio.wav", 0.000, 0.470, "DeathCrunch Gore SFX Blood and Bone", "AKkingStudio", "https://freesound.org/people/AKkingStudio/sounds/784768/"),
    ("heart_squelch", "freesound/fs641046_gore_impact_heart_magnuswaker.wav", 0.000, 0.600, "Gore Impact - LOT OF HEART", "magnuswaker", "https://freesound.org/people/magnuswaker/sounds/641046/"),
    ("juicy_bite", "freesound/fs761205_juicy_bites_mafon2.wav", 3.323, 3.720, "Juicy bites", "Mafon2", "https://freesound.org/people/Mafon2/sounds/761205/"),
    ("meat_thud", "freesound/fs626405_heavy_wet_impact_puppetmaster68.wav", 0.100, 0.460, "Heavy, wet impact.wav", "puppetmaster685719", "https://freesound.org/people/puppetmaster685719/sounds/626405/"),
    ("wet_break", "other/oga_wet-break-3_zanelittle.wav", 0.110, 0.630, "Fleshy Bone Break/Snap SFX - Wet Break 3", "Zane Little Music", "https://opengameart.org/content/fleshy-bone-breaksnap-sfx"),
]


# ── I/O ──────────────────────────────────────────────────────────────────────────────────────────

def read_wav(path: str) -> np.ndarray:
    with wave.open(path, "rb") as w:
        n, ch, sw, sr = w.getnframes(), w.getnchannels(), w.getsampwidth(), w.getframerate()
        raw = w.readframes(n)
    assert sw == 2 and sr == SR, (path, sw, sr)
    x = np.frombuffer(raw, dtype="<i2").astype(np.float64) / 32768.0
    return x.reshape(-1, ch).mean(axis=1) if ch > 1 else x


def write_wav(path: str, x: np.ndarray) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(np.round(np.clip(x, -1.0, 1.0) * 32767.0).astype("<i2").tobytes())


def extract(src_dir: str) -> None:
    """Cut each excerpt (DC removed, 2 ms fade in, 8 ms fade out) into foley/jaws_bite/ and write SOURCES.md."""
    os.makedirs(FOLEY, exist_ok=True)
    lines = ["# JAWS bite foley sources (CC0 1.0)", "",
             "Short excerpts of REAL recordings, cut by `make_jaws_bite.py --extract`. Every source is **CC0 1.0** "
             "(https://creativecommons.org/publicdomain/zero/1.0/): public domain, no attribution required, commercial use "
             "allowed. The licence was checked on each sound's own page (2026-09-28). Credited anyway, with thanks.", "",
             "| excerpt | source | author | range (s) | page |", "|---|---|---|---|---|"]
    for name, rel, t0, t1, title, author, url in EXCERPTS:
        x = read_wav(os.path.join(src_dir, rel))[int(t0 * SR):int(t1 * SR)]
        x = x - x.mean()
        x[:int(0.002 * SR)] *= np.linspace(0, 1, int(0.002 * SR))
        x[-int(0.008 * SR):] *= np.linspace(1, 0, int(0.008 * SR))
        write_wav(os.path.join(FOLEY, name + ".wav"), x / max(1e-9, np.abs(x).max()) * 0.9)
        lines.append(f"| `{name}.wav` | {title} | {author} | {t0:.3f}-{t1:.3f} | {url} |")
    with open(os.path.join(FOLEY, "SOURCES.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print("extracted", len(EXCERPTS), "excerpts ->", FOLEY)


def src(name: str) -> np.ndarray:
    return read_wav(os.path.join(FOLEY, name + ".wav"))


# ── DSP (numpy only) ─────────────────────────────────────────────────────────────────────────────

def band(x: np.ndarray, lo: float | None = None, hi: float | None = None, width: float = 0.35) -> np.ndarray:
    """Zero-phase band limit with raised-cosine edges `width` octaves wide."""
    n = 1 << int(np.ceil(np.log2(len(x) + SR // 10)))
    X = np.fft.rfft(x, n)
    f = np.fft.rfftfreq(n, 1.0 / SR)
    g = np.ones_like(f)
    lf = np.log2(np.maximum(f, 1.0))
    if lo:
        a = np.clip((lf - (np.log2(lo) - width / 2)) / width, 0, 1)
        g *= 0.5 - 0.5 * np.cos(np.pi * a)
    if hi:
        a = np.clip((lf - (np.log2(hi) - width / 2)) / width, 0, 1)
        g *= 0.5 + 0.5 * np.cos(np.pi * a)
    return np.fft.irfft(X * g, n)[:len(x)]


def place(buf: np.ndarray, at: float, x: np.ndarray, gain: float) -> None:
    i = int(round(at * SR))
    if i < 0:                                   # starts before the cue: its head is cut
        x, i = x[-i:], 0
    if i >= len(buf):
        return
    n = min(len(x), len(buf) - i)
    buf[i:i + n] += gain * x[:n]


def norm(x: np.ndarray) -> np.ndarray:
    return x / max(1e-9, np.abs(x).max())


def rms_env(x: np.ndarray, ms: float) -> np.ndarray:
    k = max(1, int(ms / 1000 * SR))
    return np.sqrt(np.convolve(x * x, np.ones(k) / k, mode="same") + 1e-12)


def chew(n: int, times: list[float], width: float, floor_db: float) -> np.ndarray:
    """An amplitude envelope of BURSTS: 1 at each time (a smooth bump `width` s wide), `floor_db` between them. It chops
    a continuous crunch into the chewing tear the reference has (4-5 bursts 28-40 ms apart, never on a strict grid)."""
    t = np.arange(n) / SR
    floor = 10 ** (floor_db / 20)
    e = np.zeros(n)
    for c in times:
        e = np.maximum(e, np.exp(-0.5 * ((t - c) / (width / 2.355)) ** 2))
    return floor + (1 - floor) * e


def gate_io(n: int, t0: float, t1: float, fin: float = 0.002, fout: float = 0.06) -> np.ndarray:
    """Open at t0 over `fin`, close by t1 over a raised-cosine `fout`: layers hand over, never cut dead."""
    t = np.arange(n) / SR
    a = np.clip((t - t0) / fin, 0, 1)
    r = np.clip((t1 - t) / fout, 0, 1)
    return a * (0.5 - 0.5 * np.cos(np.pi * r))


def cut(name: str, t0: float, t1: float | None = None, fade: float = 0.002) -> np.ndarray:
    """A source sliced mid-signal, with a 2 ms raised-cosine fade-in (a bare slice clicks)."""
    x = norm(src(name))[int(t0 * SR):None if t1 is None else int(t1 * SR)].copy()
    f = int(fade * SR)
    x[:f] *= 0.5 - 0.5 * np.cos(np.pi * np.arange(f) / f)
    if t1 is not None:
        x[-f:] *= 0.5 + 0.5 * np.cos(np.pi * np.arange(f) / f)
    return x


def lpnoise(n: int, fc: float, rng: np.random.Generator) -> np.ndarray:
    v = band(rng.standard_normal(n), None, fc, 0.5)
    return v / (v.std() + 1e-12)


def sub_env(dur: float, attack: float, hold: float, decay: float) -> np.ndarray:
    """Attack, hold, exponential decay, and a 20 ms release to ZERO at the end (a cut sub steps into silence)."""
    t = np.arange(int(dur * SR)) / SR
    env = np.minimum(1.0, t / attack) * np.exp(-np.maximum(0.0, t - attack - hold) / decay)
    return env * np.clip((dur - t) / 0.02, 0, 1) ** 2


def weight(dur: float, f_hi: float, f_mid: float, f_end: float, tau: float, attack: float, hold: float, decay: float,
           rng: np.random.Generator, jit: float = 0.10, shim: float = 0.20, sat: float = 1.0,
           restrike: tuple[float, float, float] | None = None) -> np.ndarray:
    """The WEIGHT: a sine whose pitch drops f_hi -> f_mid (tau) and drifts to f_end, but ALIVE (a pure glide is an 808):
    slow pitch jitter and amplitude shimmer from low-passed noise, and an optional RE-STRIKE (t, f, amp) that kicks the
    SAME oscillator up in pitch and level (a second weight() call phase-cancels into a hole)."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = f_mid + (f_hi - f_mid) * np.exp(-t / tau) + (f_end - f_mid) * (1 - np.exp(-t / 0.08))
    kick = np.zeros(n)
    if restrike:
        tr, fr, ar = restrike
        k = np.where(t >= tr, np.exp(-(t - tr) / 0.022), 0.0)
        f = f + (fr - f_end) * k
        kick = ar * np.where(t >= tr, np.minimum(1, (t - tr) / 0.004) * np.exp(-(t - tr) / 0.06), 0.0)
    f = f * np.exp(jit * lpnoise(n, 25.0, rng))
    ph = 2 * np.pi * np.cumsum(f) / SR
    env = (sub_env(dur, attack, hold, decay) + kick * np.clip((dur - t) / 0.02, 0, 1) ** 2)
    env *= np.clip(1 + shim * lpnoise(n, 30.0, rng), 0.2, 2.0)
    return np.tanh(sat * np.sin(ph) * env) / np.tanh(sat)


def real_low(n: int, at_gore: float, at_heart: float) -> np.ndarray:
    """The weight's RECORDED body: a real gore crunch and a real heart squelch, 30-450 Hz (noisy, lumpy)."""
    b = np.zeros(n)
    place(b, at_gore, band(norm(src("gore_crunch")), 30, 450), 1.0)
    place(b, at_heart, band(norm(src("heart_squelch")), 30, 450), 0.8)
    return b


def blip(f0: float, fall: float, dur: float, bw: float, rng: np.random.Generator) -> np.ndarray:
    """A WET squelch: a noise-excited resonance (a formant) at f0 falling by `fall`, `bw` Hz wide; an oscillator here
    read as a synth bloop."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    x = rng.standard_normal(n) * np.sin(np.pi * np.clip(t / dur, 0, 1)) ** 1.5
    f = f0 * (1 - fall * (t / dur))
    r = np.exp(-np.pi * bw / SR)
    y = np.zeros(n)
    y1 = y2 = 0.0
    for i in range(n):
        v = x[i] + 2 * r * np.cos(2 * np.pi * f[i] / SR) * y1 - r * r * y2
        y[i] = v
        y2, y1 = y1, v
    return norm(y)


def bed(n: int, name: str, skip: float, t0: float, t1: float, tau: float) -> np.ndarray:
    """The TAIL BED: the rest of a real crunch, 2-16 kHz, decaying from t0: what the ticks sit on (ticks on digital
    silence read as clicks)."""
    b = np.zeros(n)
    place(b, t0, band(cut(name, skip), 2000, 16000), 1.0)
    t = np.arange(n) / SR
    return b * np.exp(-np.maximum(0, t - t0) / tau) * gate_io(n, t0, t1, 0.02, 0.05)


def ticks(x: np.ndarray, times: list[float], gains: list[float], lo: float = 0.0, hi: float | None = None) -> np.ndarray:
    """The TAIL: the strongest isolated transients of a real crunch (only from lo..hi s of it: material no other layer
    plays), 2-16 kHz, each pasted alone at a time with a fast rise and a 5 ms fall, as debris decays."""
    h = band(x, 2000, 16000)
    e = rms_env(h, 1.5)
    a, b = int(lo * SR), len(h) if hi is None else int(hi * SR)
    rise, fall = int(0.0008 * SR), int(0.025 * SR)
    picks: list[int] = []
    for i in np.argsort(e[a:b])[::-1] + a:
        if all(abs(i - j) > 4 * rise + fall for j in picks) and rise <= i < len(h) - fall:
            picks.append(int(i))
        if len(picks) >= len(times):
            break
    w = np.concatenate([0.5 - 0.5 * np.cos(np.pi * np.arange(rise) / rise), np.exp(-np.arange(fall) / (0.005 * SR))])
    out = np.zeros(int((max(times) + 0.05) * SR))
    for i, at, g in zip(picks, times, gains):
        place(out, at, norm(h[i - rise:i + fall] * w), g)
    return out


def energy_split(y: np.ndarray) -> tuple[float, float]:
    """(share below 150 Hz, share above 2 kHz) of the whole cue's energy."""
    S = np.abs(np.fft.rfft(y)) ** 2
    f = np.fft.rfftfreq(len(y), 1.0 / SR)
    tot = S.sum()
    return S[f < 150].sum() / tot, S[f >= 2000].sum() / tot


def p2r300(y: np.ndarray) -> float:
    """Peak over the loudest 300 ms RMS, in dB (the reference: 8-9.5)."""
    w = int(0.3 * SR)
    c = np.concatenate([[0.0], np.cumsum(y * y)])
    s = np.arange(0, max(1, len(y) - w), int(0.005 * SR))
    return 20 * np.log10(np.abs(y).max() / np.sqrt((c[s + w] - c[s]).max() / w))


def sat_os(x: np.ndarray, drive: float) -> np.ndarray:
    """tanh at 4x (FFT zero-pad), band-limited back: density without aliasing."""
    X = np.fft.rfft(x)
    n = len(x)
    xo = np.fft.irfft(np.concatenate([X, np.zeros(len(X) * 3)]), n * 4) * 4
    yo = np.tanh(drive * xo) / np.tanh(drive)
    return np.fft.irfft(np.fft.rfft(yo)[:len(X)], n) / 4


def true_peak(y: np.ndarray) -> float:
    X = np.fft.rfft(y)
    return float(np.abs(np.fft.irfft(np.concatenate([X, np.zeros(len(X) * 7)]), len(y) * 8) * 8).max())


def mix_master(crunch: np.ndarray, weight_bus: np.ndarray, low_share: float, target: float, length: float) -> np.ndarray:
    """Two buses, so the loud weight never squashes the crunch. The weight's gain is solved on the FINISHED cue (after
    the saturation) so it has `low_share` of its energy below 150 Hz (the reference: ~47 %); the saturation is a 4x
    oversampled tanh driven until the loudest 300 ms sits `target` dB under the peak (the reference: 8-9.5); nothing
    above 16.5 kHz (the sources stop there; the reference is silent there); the house peak on the TRUE peak."""
    n = int(length * SR)
    c = np.zeros(n)
    w = np.zeros(n)
    c[:min(n, len(crunch))] = crunch[:n]
    w[:min(n, len(weight_bus))] = weight_bus[:n]
    c, w = norm(c), norm(w)

    def render(g: float) -> tuple[np.ndarray, float]:
        x = norm(c + g * w)
        if p2r300(x) <= target:
            return x, 1.0
        lo_d, hi_d = 1.0, 5.0
        for _ in range(14):
            d = 0.5 * (lo_d + hi_d)
            lo_d, hi_d = (lo_d, d) if p2r300(norm(sat_os(x, d))) <= target else (d, hi_d)
        return norm(sat_os(x, hi_d)), hi_d

    lo, hi = 0.05, 20.0
    for _ in range(22):
        g = (lo * hi) ** 0.5
        y, _ = render(g)
        lo, hi = (g, hi) if energy_split(y)[0] < low_share else (lo, g)
    y, drive = render(g)
    y = band(y, None, 16500)
    y[-int(0.04 * SR):] *= np.linspace(1, 0, int(0.04 * SR)) ** 2
    y[:int(0.0005 * SR)] *= np.linspace(0, 1, int(0.0005 * SR))
    if os.environ.get("BITE_DEBUG"):
        print(f"   weight gain {g:.2f}, drive {drive:.2f}, below 150 Hz {energy_split(y)[0]:.3f}")
    return y / true_peak(y) * PEAK


# ── The candidates ───────────────────────────────────────────────────────────────────────────────

def chomp() -> np.ndarray:
    """A  CHOMP: the reference's shape. The teeth meet (a real enamel snap) and TEAR in (real cabbage and flesh crunch in
    five chewing bursts, a crescendo INTO the weight); the weight lands at ~150 ms (a living sub 170 -> 60 -> 35 Hz that
    re-strikes once, a real meat thud and a recorded gore/heart low body) with a real crunch still grinding under it;
    three wet squelches (resonances at 1000, 950, 840 Hz, each falling ~7 %) and a real squelch; a debris bed and
    cartilage ticks to ~530 ms."""
    rng = np.random.default_rng(SEED)
    n = int(0.62 * SR)
    t = np.arange(n) / SR
    c = np.zeros(n)
    place(c, 0.000, band(norm(src("teeth_snap")), 1800, 12000), 0.40)
    tear = np.zeros(n)
    place(tear, 0.002, band(norm(src("cabbage_bite")), 1000, 16000), 1.0)
    place(tear, 0.030, band(norm(src("flesh_bite")), 700, 16000), 0.6)
    place(tear, 0.070, band(norm(src("cabbage_dense")), 1000, 16000), 0.8)
    tear += 0.25 * band(tear, 6000, 16000) + 0.45 * band(tear, 2000, 6000)
    rise = np.interp(t, [0.0, 0.05, 0.12, 0.16], [0.40, 0.65, 1.0, 1.0])
    c += tear * chew(n, [0.012, 0.041, 0.078, 0.104, 0.143], 0.024, -8.0) * rise * gate_io(n, 0.0, 0.20, 0.001, 0.045)
    grind = np.zeros(n)
    place(grind, 0.150, band(cut("pepper_cracks", 0.0, 0.28), 1500, 16000), 1.0)
    place(grind, 0.160, band(cut("cabbage_dense", 0.25), 1500, 16000), 0.7)
    grind += 0.4 * band(grind, 2000, 6000)
    c += grind * chew(n, [0.163, 0.201, 0.247, 0.268, 0.309, 0.352], 0.022, -9.0) * gate_io(n, 0.15, 0.43, 0.01, 0.09) * 0.75
    c += bed(n, "cabbage_dense", 0.30, 0.30, 0.56, 0.075) * 0.32
    squelch = np.zeros(n)
    place(squelch, 0.012, band(norm(src("heart_squelch")), 600, 2600), 0.20)
    c += squelch * gate_io(n, 0.0, 0.44, 0.002, 0.10)
    meat = np.zeros(n)
    place(meat, 0.110, band(norm(src("flesh_bite_body")), 150, 1200), 1.0)
    c += meat * gate_io(n, 0.12, 0.40, 0.03, 0.10) * 0.40
    for at, f0 in ((0.035, 1000.0), (0.140, 950.0), (0.235, 840.0)):
        place(c, at, blip(f0, 0.07, 0.026, 90.0, rng), 0.13)
    place(c, 0.0, ticks(src("pepper_cracks"), [0.366, 0.401, 0.423, 0.470], [0.030, 0.018, 0.024, 0.014], lo=0.30), 1.0)
    w = np.zeros(n)
    place(w, 0.150, weight(0.36, 170.0, 60.0, 35.0, 0.018, 0.008, 0.12, 0.060, rng, restrike=(0.098, 110.0, 0.6)), 0.42)
    place(w, 0.150 - 0.110, band(norm(src("meat_thud")), 25, 300), 0.95)
    w += real_low(n, 0.140, 0.150) * 0.8 * gate_io(n, 0.13, 0.40, 0.005, 0.12)
    return mix_master(c, w, 0.47, 8.8, 0.58)


def bone() -> np.ndarray:
    """B  BONE: tight and hard, the reference's early variation. A real nut CRACK and teeth snap on the onset, a real
    bone crunch peaking inside 50 ms in un-gridded chewing bursts, the weight arriving early (~65 ms, living, one
    re-strike, a real meat thud and a recorded low body), bone grit grinding under it (material the tear never plays);
    one wet squelch; a debris bed and ticks to ~470 ms."""
    rng = np.random.default_rng(SEED + 1)
    n = int(0.56 * SR)
    c = np.zeros(n)
    place(c, 0.000, band(cut("nut_crack", 0.040), 600, 12000), 0.40)
    place(c, 0.001, band(norm(src("teeth_snap_b")), 1500, 12000), 0.35)
    tear = np.zeros(n)
    place(tear, 0.000, band(cut("wet_break", 0.0, 0.20), 800, 16000), 1.0)
    place(tear, 0.004, band(cut("bone_bite", 0.245), 800, 16000), 0.6)
    tear += 0.3 * band(tear, 6000, 16000) + 0.3 * band(tear, 2000, 6000)
    c += tear * chew(n, [0.008, 0.033, 0.066, 0.089, 0.124], 0.020, -6.0) * gate_io(n, 0.0, 0.16, 0.001, 0.04)
    grit = np.zeros(n)
    place(grit, 0.120, band(cut("bone_bite", 0.34), 800, 15000, width=1.0), 1.0)
    place(grit, 0.150, band(cut("wet_break", 0.20, 0.30), 800, 15000, width=1.0), 0.8)
    c += grit * chew(n, [0.140, 0.179, 0.228, 0.263, 0.314, 0.35], 0.020, -9.0) * gate_io(n, 0.12, 0.42, 0.01, 0.09)
    c += bed(n, "cabbage_dense", 0.30, 0.27, 0.50, 0.07) * 0.30
    meat = np.zeros(n)
    place(meat, 0.040, band(norm(src("flesh_bite_body")), 150, 1200), 1.0)
    c += meat * gate_io(n, 0.05, 0.30, 0.03, 0.08) * 0.35
    place(c, 0.030, blip(930.0, 0.08, 0.022, 90.0, rng), 0.15)
    place(c, 0.0, ticks(src("nut_crack"), [0.338, 0.371, 0.392, 0.437], [0.030, 0.022, 0.018, 0.012]), 1.0)
    w = np.zeros(n)
    place(w, 0.065, weight(0.44, 180.0, 62.0, 40.0, 0.014, 0.005, 0.15, 0.065, rng, restrike=(0.12, 115.0, 0.5)), 0.40)
    place(w, 0.065 - 0.115, band(norm(src("meat_thud")), 25, 300), 0.90)
    w += real_low(n, 0.060, 0.070) * 0.8 * gate_io(n, 0.05, 0.34, 0.005, 0.12)
    return mix_master(c, w, 0.47, 9.2, 0.54)


def juicy() -> np.ndarray:
    """C  JUICY: the wettest, the reference's soft variation. A real teeth snap on the onset, then the bite RAMPS in over
    ~90 ms (a real watermelon bite and a real flesh bite, gathering), a real gore crunch on top, the loud body at ~110-300
    ms with a denser crunch under the weight; the weight late (~160 ms, living, a recorded low body); three wet
    squelches and a long real squelch; a debris bed and ticks."""
    rng = np.random.default_rng(SEED + 2)
    n = int(0.62 * SR)
    t = np.arange(n) / SR
    c = np.zeros(n)
    place(c, 0.000, band(norm(src("teeth_snap")), 1500, 12000), 0.40)
    body = np.zeros(n)
    place(body, 0.000, band(norm(src("juicy_bite")), 700, 16000), 1.0)
    place(body, -0.005, band(norm(src("flesh_bite_body")), 600, 16000), 0.8)
    body += 0.5 * band(body, 6000, 16000) + 0.3 * band(body, 2000, 6000)
    ramp = np.interp(t, [0.0, 0.09, 1.0], [0.3, 1.0, 1.0])
    c += body * ramp * chew(n, [0.006, 0.041, 0.075, 0.111, 0.147], 0.030, -6.0) * gate_io(n, 0.0, 0.22, 0.001, 0.05)
    gore = np.zeros(n)
    place(gore, 0.070, band(norm(src("gore_crunch")), 600, 12000), 0.5)
    c += gore * gate_io(n, 0.0, 0.40, 0.001, 0.08)
    under = np.zeros(n)
    place(under, 0.160, band(cut("cabbage_dense", 0.25), 1500, 16000), 1.0)
    c += under * chew(n, [0.18, 0.215, 0.26, 0.29, 0.335], 0.022, -9.0) * gate_io(n, 0.16, 0.42, 0.01, 0.09) * 0.5
    squelch = np.zeros(n)
    place(squelch, 0.045, band(norm(src("heart_squelch")), 500, 3000), 0.40)
    c += squelch * gate_io(n, 0.0, 0.40, 0.002, 0.10)
    c += bed(n, "pepper_cracks", 0.30, 0.34, 0.58, 0.07) * 0.30
    for at, f0 in ((0.050, 1080.0), (0.150, 980.0), (0.225, 860.0)):
        place(c, at, blip(f0, 0.09, 0.026, 100.0, rng), 0.18)
    place(c, 0.0, ticks(src("nut_crack"), [0.395, 0.420, 0.452, 0.500], [0.030, 0.022, 0.016, 0.012]), 1.0)
    w = np.zeros(n)
    place(w, 0.160, weight(0.40, 160.0, 58.0, 34.0, 0.020, 0.010, 0.12, 0.060, rng, restrike=(0.10, 105.0, 0.5)), 0.42)
    place(w, 0.160 - 0.110, band(norm(src("meat_thud")), 25, 300), 0.95)
    w += real_low(n, 0.150, 0.160) * 0.8 * gate_io(n, 0.14, 0.42, 0.005, 0.12)
    return mix_master(c, w, 0.47, 9.2, 0.60)


def main() -> int:
    if len(sys.argv) > 2 and sys.argv[1] == "--extract":
        extract(sys.argv[2])
        return 0
    out = {"sfx_seeker_jaws_bite": (COMBAT, chomp),
           "sfx_seeker_jaws_bite_bone": (CANDIDATES, bone),
           "sfx_seeker_jaws_bite_juicy": (CANDIDATES, juicy)}
    for name, (d, build) in out.items():
        y = build()
        write_wav(os.path.join(d, name + ".wav"), y)
        print(f"{name:28} {len(y) / SR * 1000:5.0f} ms  peak {20 * np.log10(np.abs(y).max()):6.1f} dB  "
              f"p2r300 {p2r300(y):4.1f} dB")
    return 0


if __name__ == "__main__":
    sys.exit(main())
