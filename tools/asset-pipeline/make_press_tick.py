#!/usr/bin/env python3
"""make_press_tick.py -- PRESS's tick cue, built from REAL recorded foley (ADR-011, the owner's brief 2026-09-28).

The PRESS picture is APPROVED (commit fab25919): a pressure front arrives at the front enemy, it is compressed between two
arcs, its DEFENCE BREAKS. The cue says exactly that, as ONE short composite event (~180 ms) of three semantic layers:
  1 PRESSURE RELEASE (0-35 ms): a restrained low air/force pulse -- pressure being discharged, never an object flying
    (no whoosh, no travel, no whistle): a real gust of air thrown by a snapped canvas, the air a pillow or a dropped stack
    of paper squeezes out (AIR: with the gust and a sub alone the first 35 ms were a pure ~60 Hz tone, an 808 swell),
    a compressed air cushion, a small sub;
  2 COMPRESSION THUMP (at CUE_THUMP_MS = 40 ms, the strongest layer): short, heavy, dry, low-mid -- "something was
    suddenly pressed under weight" (real padded cushion hits, a book slammed flat, a flour sack's squeezed give), smaller
    than HARD HANDS' hit, never a hammer, an explosion or a punch;
  3 DEFENCE BREAK (+6..+12 ms after the thump): a clear, brittle, MATERIAL-NEUTRAL crack -- a rigid protective layer
    cracking (a ceramic tile, a hard shell, a stone fracture; no sword-on-metal, no glass tinkle), then a very short
    fragment decay. No reverb tail: PRESS repeats every 2 s and faster with upgrades.
The game starts the file FieldRecipe.CueStartMs (-20 ms) before the tick so the thump lands on the crush (~+20 ms).

Every excerpt is a REAL recording, CC0 1.0 (Freesound; licence checked on each sound's own page, 2026-09-28; see
foley/press_tick/SOURCES.md); only a small sub under the pressure is synthesised. Three candidates that differ in
STRUCTURE: A BALANCED (pressure + thump + tile crack), B HEAVY (a heavier, earlier pressure and a squeezed sack under the
thump; the crack later and smaller), C BREAK (a lighter pressure, the clearest defence break: a stone fracture with a
brittle leading tick). Each is mastered to the same loudness (so the owner compares character, not level) and its
placement is measured (the QA below).

A IS HUMAN-APPROVED (the owner, 2026-09-29: "PRESS is APPROVED. Use Candidate A - BALANCED as the final human-approved
PRESS tick cue"): it is written to assets/audio/combat/sfx_seeker_press_tick.wav and its approved bytes are pinned by
SHA-256 in press_field_test.cs. Do not change balanced(), its sources or the shared DSP without the owner's ear: a
different file is a new approval, not a regeneration. B and C were not chosen; they are review history in
tools/asset-pipeline/audio_history/press_tick/.

    PYTHONUTF8=1 python tools/asset-pipeline/make_press_tick.py --extract <dir of the downloaded sources>   # once
    PYTHONUTF8=1 python tools/asset-pipeline/make_press_tick.py [A|B|C]                                     # build
"""
from __future__ import annotations

import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from make_jaws_bite import SR, band, norm, place, read_wav, sat_os, true_peak, weight, write_wav  # noqa: E402  (the shared DSP)

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
FOLEY = os.path.join(HERE, "foley", "press_tick")
COMBAT = os.path.join(REPO, "assets", "audio", "combat")
HISTORY = os.path.join(HERE, "audio_history", "press_tick")   # B and C: reviewed, not chosen
KEY = "sfx_seeker_press_tick"
APPROVED = "A"                  # HUMAN-APPROVED 2026-09-29: the game's cue, its bytes pinned in press_field_test.cs
CUE_THUMP_MS = 40.0             # FieldRecipe.CueThumpMs: the file is authored to it
LENGTH = 0.19                   # s: ~140-220 ms, no tail
PEAK = 0.37                     # the house true peak for a cue (~-8.6 dBFS, as the other Seeker cues)
MAX50_DB = -18.5                # the loudest 50 ms, dBFS: played at FieldRecipe.TickVolume (0.28) x the SFX master it sits
                                # ~-31.5 dB raw, and >= 3 dB under SPRAY's contact K-WEIGHTED (at 0.34 the QA measured only
                                # 1.6 dB K-weighted under SPRAY: a cue heard every 2 s at the level of an authored contact)
TICK_VOLUME = 0.28              # FieldRecipe.TickVolume (the QA's mix check plays the cue at it)
SEED = 20260928

# The excerpts: (name, downloaded file, start s, end s, title, author, url). Every source is CC0 1.0.
EXCERPTS = [
    ("air_gust", "pressure/fs428337_canvas_dropcloth_snap_1_zembacraftworks.wav", 0.200, 0.300, "Canvas Dropcloth Snap 1", "zembacraftworks", "https://freesound.org/people/zembacraftworks/sounds/428337/"),
    ("air_cushion", "pressure/fs532130_air_mattress_bump_j1987.wav", 0.010, 0.100, "air_mattress_bump.wav", "j1987", "https://freesound.org/people/j1987/sounds/532130/"),
    ("pillow_air", "pressure/fs728541_pillow_flump_trinitrotoluenedev.wav", 0.090, 0.175, "Pillow Flump", "trinitrotoluenedev", "https://freesound.org/people/trinitrotoluenedev/sounds/728541/"),
    ("paper_air", "pressure/fs416416_drop_stack_of_paper_whoomp_DAVESTALKER.wav", 0.055, 0.145, "Drop Stack of Paper-whoomp.wav", "DAVESTALKER", "https://freesound.org/people/DAVESTALKER/sounds/416416/"),
    ("kick_air", "pressure/fs104258_yamaha_custom_birch_bass_dru_minorr.wav", 0.000, 0.090, "Yamaha Custom Birch BASS DRUM 22'' PP.wav", "minorr", "https://freesound.org/people/minorr/sounds/104258/"),
    ("cushion_hit", "thump/fs593948_cushionimpacts_mincedbeats.wav", 0.155, 0.265, "Cushion Impacts.wav", "mincedbeats", "https://freesound.org/people/mincedbeats/sounds/593948/"),
    ("cushion_heavy", "thump/fs593948_cushionimpacts_mincedbeats.wav", 3.736, 3.846, "Cushion Impacts.wav", "mincedbeats", "https://freesound.org/people/mincedbeats/sounds/593948/"),
    ("book_flat", "thump/fs198960_bookslam_Mydo1.wav", 0.000, 0.115, "book slam", "Mydo1", "https://freesound.org/people/Mydo1/sounds/198960/"),
    ("flour_sack", "thump/fs382642_flourbag_bbrocer.wav", 0.248, 0.368, "Bag Of Flour Dropped On Counter Top.wav", "bbrocer", "https://freesound.org/people/bbrocer/sounds/382642/"),
    ("tile_crack", "crack/fs711734_tilebreak_group15_MulisaNdou.wav", 4.316, 4.400, "SFXTile_EXT_TileBreak_Group15_OWSFX", "MulisaNdou", "https://freesound.org/people/MulisaNdou/sounds/711734/"),
    ("shell_crack", "crack/fs379012_walnut_13FPanska_Cerny_Jan.wav", 0.958, 1.040, "Walnut.wav", "13FPanska_Cerny_Jan", "https://freesound.org/people/13FPanska_Cerny_Jan/sounds/379012/"),
    ("stone_break", "crack/fs843339_concrete_breaks_loganzsound.wav", 3.216, 3.300, "Concrete Breaks Several Denoised", "loganzsound", "https://freesound.org/people/loganzsound/sounds/843339/"),
    ("brittle_tick", "crack/fs119452_ice_cube_tray_crack_lmbubec.wav", 0.692, 0.735, "Ice Cube Tray Crack.wav", "lmbubec", "https://freesound.org/people/lmbubec/sounds/119452/"),
]


def extract(src_dir: str) -> None:
    """Cut each excerpt (DC removed, 2 ms fade in, 8 ms fade out) into foley/press_tick/ and write SOURCES.md."""
    os.makedirs(FOLEY, exist_ok=True)
    lines = ["# PRESS tick foley sources (CC0 1.0)", "",
             "Short excerpts of REAL recordings, cut by `make_press_tick.py --extract`. Every source is **CC0 1.0** "
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
    x = read_wav(os.path.join(FOLEY, name + ".wav"))
    return notch(x, 11080, 250) if name == "shell_crack" else x      # the walnut carries a faint 11.08 kHz line


def notch(x: np.ndarray, f0: float, width: float) -> np.ndarray:
    """Remove a narrow line (f0 +- width/2 Hz), zero-phase."""
    n = 1 << int(np.ceil(np.log2(len(x) + SR // 10)))
    X = np.fft.rfft(x, n)
    f = np.fft.rfftfreq(n, 1.0 / SR)
    X[np.abs(f - f0) < width / 2] = 0
    return np.fft.irfft(X, n)[:len(x)]


# ── helpers ──────────────────────────────────────────────────────────────────────────────────────

def onset(x: np.ndarray, share: float = 0.5, lo: float | None = None, hi: float | None = None) -> float:
    """Where x first reaches `share` of its peak (s), on a 1 ms envelope of its lo..hi band: the excerpt's hit."""
    y = band(x, lo, hi) if (lo or hi) else x
    k = max(1, int(0.001 * SR))
    e = np.convolve(np.abs(y), np.ones(k) / k, mode="same")
    return float(np.argmax(e >= share * e.max()) / SR)


def at_hit(buf: np.ndarray, t: float, x: np.ndarray, gain: float, lo: float | None = None, hi: float | None = None) -> None:
    """Place x so its HIT (onset) lands at t."""
    place(buf, t - onset(x, 0.5, lo, hi), x, gain)


def tail(x: np.ndarray, from_s: float, tau: float) -> np.ndarray:
    """Shorten a layer: an exponential fade from `from_s` (s into x) -- a body, not a ring."""
    t = np.arange(len(x)) / SR
    return x * np.where(t < from_s, 1.0, np.exp(-(t - from_s) / tau))


def sub(dur: float, f_hi: float, f_end: float, attack: float, decay: float, rng: np.random.Generator) -> np.ndarray:
    """The only synthesised part: a small pressure sub (a pitch falling f_hi -> f_end), alive (jitter), released to zero."""
    return weight(dur, f_hi, 0.5 * (f_hi + f_end), f_end, 0.03, attack, 0.004, decay, rng, jit=0.06, shim=0.15, sat=1.4)


def master(low: np.ndarray, crack: np.ndarray, crack_share: float) -> np.ndarray:
    """Two buses (the low weight never squashes the crack): the crack bus at `crack_share` of the low bus' peak; a light
    4x-oversampled saturation to tame the crest; nothing above 15 kHz; the loudest 50 ms at MAX50_DB, the true peak
    never above PEAK; a release to zero at the end."""
    n = int(LENGTH * SR)
    lo_b, cr_b = np.zeros(n), np.zeros(n)
    lo_b[:min(n, len(low))] = low[:n]
    cr_b[:min(n, len(crack))] = crack[:n]
    y = norm(lo_b) + crack_share * norm(cr_b)
    y = norm(sat_os(norm(y), 1.6))
    y = band(y, 28, 15000)
    y[-int(0.03 * SR):] *= np.linspace(1, 0, int(0.03 * SR)) ** 2
    y[:int(0.0005 * SR)] *= np.linspace(0, 1, int(0.0005 * SR))
    y = y / max1(y, 0.05) * 10 ** (MAX50_DB / 20)
    tp = true_peak(y)
    if tp > PEAK:
        y = y / tp * PEAK
    return y


def max1(y: np.ndarray, win: float) -> float:
    """The loudest `win` s RMS."""
    w = int(win * SR)
    c = np.concatenate([[0.0], np.cumsum(y * y)])
    s = np.arange(0, max(1, len(y) - w), int(0.002 * SR))
    return float(np.sqrt((c[s + w] - c[s]).max() / w))


# ── The candidates (T = the thump's moment in the file) ──────────────────────────────────────────

T = CUE_THUMP_MS / 1000


def balanced() -> np.ndarray:
    """A  BALANCED: a real gust of air swells into the contact (pressure discharged), a padded cushion and a book slammed
    flat land together on T (pressed under weight), a ceramic tile cracks 8 ms later (the defence breaks) with a
    walnut-shell grain under it, and the fragments decay inside ~60 ms."""
    rng = np.random.default_rng(SEED)
    n = int(LENGTH * SR)
    low, crack = np.zeros(n), np.zeros(n)
    gust = band(src("air_gust"), 35, 320)
    at_hit(low, T - 0.004, gust, 0.30)                                   # peaks just before the thump
    at_hit(low, T - 0.006, tail(band(src("pillow_air"), 80, 1400), 0.030, 0.020), 0.30)   # AIR, not a tone
    place(low, T - 0.030, sub(0.10, 78, 48, 0.022, 0.035, rng), 0.10)    # the pressure's sub, into the thump
    at_hit(low, T, tail(band(src("cushion_hit"), 35, 2500), 0.045, 0.030), 1.0)
    at_hit(low, T, tail(band(src("book_flat"), 60, 3000), 0.040, 0.025), 0.75)
    at_hit(crack, T + 0.008, tail(band(src("tile_crack"), 700, 14000), 0.030, 0.022), 1.0, 700, None)
    at_hit(crack, T + 0.010, tail(band(src("shell_crack"), 900, 12000), 0.020, 0.015), 0.35, 900, None)
    return master(low, crack, 0.50)                   # at 0.70 its crack was the sharpest thing on the most-repeated cue


def heavy() -> np.ndarray:
    """B  HEAVY: the pressure starts earlier and weighs more (the gust, a compressed air cushion and a lower sub swell
    through the first 35 ms), the thump is a heavy cushion over a flour sack's squeezed give, and the crack comes later
    (+12 ms) and smaller: a hard shell giving way."""
    rng = np.random.default_rng(SEED + 1)
    n = int(LENGTH * SR)
    low, crack = np.zeros(n), np.zeros(n)
    at_hit(low, T - 0.008, band(src("air_gust"), 35, 300), 0.45)
    at_hit(low, T - 0.002, tail(band(src("air_cushion"), 90, 900), 0.050, 0.030), 0.40)   # above the ~75 Hz tone
    at_hit(low, T - 0.010, tail(band(src("paper_air"), 60, 1600), 0.040, 0.025), 0.40)     # air expelled
    place(low, T - 0.036, sub(0.13, 70, 42, 0.030, 0.045, rng), 0.07)
    at_hit(low, T, tail(band(src("cushion_heavy"), 35, 2000), 0.050, 0.035), 0.80)
    at_hit(low, T, tail(band(src("book_flat"), 60, 3000), 0.040, 0.025), 0.50)
    at_hit(low, T + 0.002, tail(band(src("flour_sack"), 80, 5000), 0.045, 0.030), 0.60)
    at_hit(crack, T + 0.012, tail(band(src("shell_crack"), 800, 12000), 0.025, 0.018), 1.0, 800, None)
    at_hit(crack, T + 0.013, tail(band(src("tile_crack"), 1200, 14000), 0.018, 0.014), 0.30, 1200, None)
    return master(low, crack, 0.55)


def breaks() -> np.ndarray:
    """C  BREAK: the clearest defence break -- a lighter pressure and thump, then (+6 ms) a stone fracture with a brittle
    leading tick on its edge and a short crumble: the rigid layer audibly gives way."""
    rng = np.random.default_rng(SEED + 2)
    n = int(LENGTH * SR)
    low, crack = np.zeros(n), np.zeros(n)
    at_hit(low, T - 0.004, band(src("air_gust"), 35, 320), 0.25)
    at_hit(low, T - 0.005, tail(band(src("pillow_air"), 80, 1400), 0.025, 0.018), 0.20)
    place(low, T - 0.026, sub(0.09, 80, 50, 0.020, 0.030, rng), 0.08)
    at_hit(low, T, tail(band(src("cushion_hit"), 35, 2500), 0.040, 0.028), 1.0)
    at_hit(crack, T + 0.006, tail(band(src("brittle_tick"), 1500, 15000), 0.012, 0.010), 0.80, 1500, None)
    at_hit(crack, T + 0.007, tail(band(src("stone_break"), 1200, 12000), 0.025, 0.018), 1.0, 1200, None)
    at_hit(crack, T + 0.007, tail(band(src("tile_crack"), 900, 14000), 0.022, 0.016), 0.70, 900, None)
    return master(low, crack, 0.85)                   # mid-heavy and long, the stone read as rubble crumbling and outweighed the thump


BUILDS = {"A": ("balanced", balanced), "B": ("heavy", heavy), "C": ("break", breaks)}


# ── QA: the placement, the length, the balance ───────────────────────────────────────────────────

def measure(y: np.ndarray) -> dict:
    """The thump's onset (its low-mid band, 100-800 Hz, where the pressure does not live), the crack's onset (above 1.5 kHz)
    and their gap; the PRESSURE's level before the thump and the CRACK's level over its first 30 ms, both against the
    loudest 50 ms (dB); the length to -40 dB of the peak; the band split; the loudest 50 ms; the true peak."""
    thump = onset(y, 0.5, 100, 800)
    crack = onset(y, 0.5, 1500, None)
    ref = max1(y, 0.05)
    pre = band(y, None, 250)[:max(1, int((thump - 0.004) * SR))]
    cr = band(y, 1200, None)[int(crack * SR):int((crack + 0.03) * SR)]
    k = int(0.002 * SR)
    e = np.convolve(np.abs(y), np.ones(k) / k, mode="same")
    last = np.nonzero(e >= e.max() * 10 ** (-40 / 20))[0]
    S = np.abs(np.fft.rfft(y)) ** 2
    f = np.fft.rfftfreq(len(y), 1 / SR)
    tot = S.sum()
    shares = [S[(f >= a) & (f < b)].sum() / tot for a, b in ((0, 150), (150, 1000), (1000, 4000), (4000, 30000))]
    rms = lambda v: float(np.sqrt((v ** 2).mean())) if len(v) else 0.0   # noqa: E731
    return {"thump_ms": thump * 1000, "crack_ms": crack * 1000, "gap_ms": (crack - thump) * 1000,
            "pre_db": 20 * np.log10(max(rms(pre), 1e-9) / ref), "crack_db": 20 * np.log10(max(rms(cr), 1e-9) / ref),
            "length_ms": (last[-1] if len(last) else 0) / SR * 1000, "bands": shares,
            "max50_db": 20 * np.log10(ref), "true_peak_db": 20 * np.log10(true_peak(y))}


def k_weight(x: np.ndarray) -> np.ndarray:
    """ITU-R BS.1770 K-weighting at 44.1 kHz (the shelf, then the high-pass), a plain biquad pair."""
    def biquad(v, b, a):
        y = np.zeros_like(v)
        x1 = x2 = y1 = y2 = 0.0
        for i, s in enumerate(v):
            o = b[0] * s + b[1] * x1 + b[2] * x2 - a[1] * y1 - a[2] * y2
            x2, x1, y2, y1 = x1, s, y1, o
            y[i] = o
        return y
    shelf = ([1.53090959, -2.65116903, 1.16916686], [1.0, -1.66375011, 0.71265753])
    hp = ([1.0, -2.0, 1.0], [1.0, -1.98916967, 0.98919016])
    return biquad(biquad(x, *shelf), *hp)


def k50(path: str, volume: float) -> float:
    """The loudest 50 ms, K-weighted, of a cue played at `volume` x the SFX master (0.8), dB."""
    return 20 * np.log10(max1(k_weight(read_wav(path) * volume * 0.8), 0.05))


def candidate(key: str) -> str:
    """Where candidate `key` lives: the approved one IS the game's cue; the others are review history."""
    if key == APPROVED:
        return os.path.join(COMBAT, KEY + ".wav")
    return os.path.join(HISTORY, f"{KEY}_{key}_{BUILDS[key][0]}.wav")


def main() -> int:
    if len(sys.argv) > 2 and sys.argv[1] == "--extract":
        extract(sys.argv[2])
        return 0
    which = sys.argv[1:] or list(BUILDS)
    for key in which:
        name, build = BUILDS[key]
        y = build()
        path = candidate(key)
        write_wav(path, y)
        m = measure(y)
        print(f"{key} {name:9s} thump {m['thump_ms']:5.1f} ms  crack {m['crack_ms']:5.1f} ms (+{m['gap_ms']:4.1f})  "
              f"pressure {m['pre_db']:5.1f} dB  crack {m['crack_db']:5.1f} dB  length {m['length_ms']:5.1f} ms  "
              f"bands <150 {m['bands'][0]:.2f} 150-1k {m['bands'][1]:.2f} 1-4k {m['bands'][2]:.2f} >4k {m['bands'][3]:.2f}  "
              f"max50 {m['max50_db']:5.1f}  TP {m['true_peak_db']:5.1f}")
        kp = k50(path, TICK_VOLUME)
        ks = k50(os.path.join(COMBAT, "sfx_seeker_spray_hit.wav"), 0.50)
        print(f"   as played: K-weighted loudest 50 ms {kp:5.1f} dB, SPRAY's contact {ks:5.1f} ({ks - kp:+.1f} dB under it)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
