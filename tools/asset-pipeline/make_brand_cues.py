#!/usr/bin/env python3
"""make_brand_cues.py -- BRAND's cue family (the Living Shadow Corruption), built from REAL recorded foley (ADR-011 / ADR-013,
the brief design/audio/seeker-brand-audio-brief.md, 2026-10-02).

BRAND is a curse INSIDE the victim: it enters, takes hold, spreads deeper through the same body, and leaves or propagates.
Seven one-shot cues (no loop, no ambience, no voice), every one quieter than SPRAY, HARD HANDS, JAWS and PRESS's crush:

  key                            moment                                  length      take-hold transient at
  sfx_seeker_brand_apply         the first infection                     ~650-800    180 ms
  sfx_seeker_brand_deepen        depth 1 -> 2                            ~600-750    230 ms (wake 0-80, travel 0-170)
  sfx_seeker_brand_deepen_deep   any deepen reaching depth 3             ~700-850    230 ms
  sfx_seeker_brand_infect        SPRAWL: one victim takes the curse      ~200-300     15 ms
  sfx_seeker_brand_leave         a cursed host falls                     ~500-700     10 ms (the flare on the fall)
  sfx_seeker_brand_awaken        transfer: the curse wakes in the host   ~450-600    120 ms
  sfx_seeker_brand_ash           the Ash-Burn accent                     ~200          5 ms

ONE source family: every cue of a candidate is made of the same three things --
  TEXTURE    the curse's breath/air (a bellows' air, a suction pipe; B: a steam hiss; C: a fire's steam, sand),
  RESONANCE  one low INTERNAL body resonance (an underwater drum hit, pitched down; a muffled body thud on the take-hold),
  DRY LAYER  the dry corruption (a leather creak's grain, burning paper's crackle, a twig's snap, dust),
and only timing and intensity change between cues. Across candidates the family is shared and re-weighted:
  A  SUBTLE / INTERNAL  breath and suction, an internal muffled body, dry corruption kept low; little overt magic.
  B  SUPERNATURAL       reversed textures (a reversed hiss and a reversed resonance swelling INTO the take-hold), a spectral
                        fluttering hiss, a corrupted (two-voice, beating) resonance; still dark and restrained.
  C  ORGANIC / ASH      dry internal crackle building into the hold, a brittle snap, ash and sand crumbling and pouring,
                        a creaking tissue, the low body resonance; supernatural, not gore.

Every excerpt is a REAL recording, CC0 1.0 (Freesound; the licence checked on each sound's own page, 2026-10-02; see
foley/brand_curse/SOURCES.md). Nothing is synthesised: the low internal resonance is a recorded underwater drum. Each cue
is mastered to its brief loudness (the K-weighted loudest 50 ms of the file x its MarkRecipe volume x the SFX master 0.8)
with the true peak <= PEAK, so the candidates differ in character, never in level.

NOTHING HERE IS APPROVED YET (awaiting the owner's choice of A/B/C). Candidate A ships provisionally in
assets/audio/combat/; A, B and C all live in tools/asset-pipeline/audio_history/brand_curse/<letter>/ for the review films
(film_audio.py --cue KEY=PATH). After the owner chooses, the chosen set's bytes get pinned by SHA-256; from then on a
different file is a new approval, never a regeneration. The shared DSP is IMPORTED from make_jaws_bite (whose JAWS bytes,
like PRESS's, are pinned): never edit it from here.

    PYTHONUTF8=1 python tools/asset-pipeline/make_brand_cues.py --extract <dir of the downloaded sources>   # once
    PYTHONUTF8=1 python tools/asset-pipeline/make_brand_cues.py [A|B|C ...]                                # build
"""
from __future__ import annotations

import functools
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from make_jaws_bite import SR, band, lpnoise, norm, place, read_wav, sat_os, true_peak, write_wav  # noqa: E402  (the shared DSP)
from make_press_tick import k_weight, max1  # noqa: E402  (PRESS's K-weighting and loudest-window RMS, read only)

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
FOLEY = os.path.join(HERE, "foley", "brand_curse")
COMBAT = os.path.join(REPO, "assets", "audio", "combat")
HISTORY = os.path.join(HERE, "audio_history", "brand_curse")    # <letter>/<key>.wav: every candidate, for the review
SHIPPED = "A"                   # provisional: the game plays A until the owner chooses (NOT approved)
PEAK = 0.37                     # the house true peak for a cue (~-8.6 dBFS)
MASTER_SFX = 0.8                # SoundBank's default SFX master
SEED = 20261002

KEYS = ["apply", "deepen", "deepen_deep", "infect", "leave", "awaken", "ash"]
PREFIX = "sfx_seeker_brand_"
# MarkRecipe's volumes (mirrored; the file is mastered to them) and the brief's K-weighted targets, dB.
VOLUME = {"apply": 0.183, "deepen": 0.163, "deepen_deep": 0.173, "infect": 0.116, "leave": 0.145, "awaken": 0.163, "ash": 0.092}
# the brief's targets lowered 1 dB alike by the integration's mix check (2026-10-02): the loudest, an apply on a host
# with no Ash-Burn, >= 3 dB under PRESS's tick in the fight; the files are (to within rounding) the same as before
TARGET = {"apply": -35.0, "deepen": -36.0, "deepen_deep": -35.5, "infect": -39.0, "leave": -37.0, "awaken": -36.0, "ash": -41.0}
# Where the TAKE-HOLD transient sits in each file (ms): MarkRecipe.<Kind>CueStartMs, the brief's table.
ONSET_MS = {"apply": 180.0, "deepen": 230.0, "deepen_deep": 230.0, "infect": 15.0, "leave": 10.0, "awaken": 120.0, "ash": 5.0}

# The excerpts: (name, downloaded file, start s, end s, title, author, url). Every source is CC0 1.0.
FS = "https://freesound.org/people/{}/sounds/{}/"
EXCERPTS = [
    # TEXTURE: air, suction, hiss
    ("air_draw", "breath/fs368498_bellows_lensson.wav", 0.300, 1.350, "bellows.wav", "lensson", FS.format("lensson", 368498)),
    ("air_exhale", "breath/fs368498_bellows_lensson.wav", 3.450, 5.050, "bellows.wav", "lensson", FS.format("lensson", 368498)),
    ("suck_pipe", "suction/fs707322_dental_suction_pipe_mht23SD.wav", 0.620, 1.950, "Dental tube sucking in mouthwash, suction pipe.wav", "mht23SD", FS.format("mht23SD", 707322)),
    ("steam", "hiss/fs234782_steam_hiss_wubitog.wav", 0.000, 1.450, "Steam/hiss", "wubitog", FS.format("wubitog", 234782)),
    ("fire_steam", "hiss/fs870180_water_on_fire_steam_embers_bassimat.wav", 2.000, 3.000, "Water Poured onto a Burning Fireplace - Hissing Steam and Sizzling Embers", "bassimat", FS.format("bassimat", 870180)),
    ("gas_puff", "hiss/fs477846_short_gas_leak_Astounded.wav", 0.660, 1.300, "short-gas-leak.wav", "Astounded", FS.format("Astounded", 477846)),
    # DRY: crackle, creak, scrape, ash
    ("paper_burn", "crackle/fs461075_burning_paper_15GKovacovaJulie.wav", 3.300, 4.700, "1.10_burning-paper.wav", "15GKovacovaJulie", FS.format("15GKovacovaJulie", 461075)),
    ("twig_snap", "crackle/fs354103_small_branches_snapping_Bini_trns.wav", 1.060, 1.300, "few small branches snapping, close up, high frequency,H6.wav", "Bini_trns", FS.format("Bini_trns", 354103)),
    ("twig_snap_b", "crackle/fs354103_small_branches_snapping_Bini_trns.wav", 0.670, 0.900, "few small branches snapping, close up, high frequency,H6.wav", "Bini_trns", FS.format("Bini_trns", 354103)),
    ("leaf_crush", "crackle/fs179338_dry_leaves_rustling_lolamadeus.wav", 1.000, 1.800, "Dry Leaves Rustling.wav", "lolamadeus", FS.format("lolamadeus", 179338)),
    ("leather_crawl", "creak/fs536187_leather_creak_stretching_IENBA.wav", 1.200, 3.000, "Leather Creak / Stretching", "IENBA", FS.format("IENBA", 536187)),
    ("cloth_scrape", "scrape/fs474724_rough_cloth_ablekirby.wav", 0.050, 1.050, "Rough Cloth.wav", "ablekirby", FS.format("ablekirby", 474724)),
    ("stone_scrape", "scrape/fs667284_stone_scrape_alegemaate.wav", 0.000, 1.200, "Stone Scrape", "alegemaate", FS.format("alegemaate", 667284)),
    ("dust_fall", "ash/fs109746_hit_with_dust_fall_SoundCollectah.wav", 0.000, 1.100, "hit with dust fall.aiff", "SoundCollectah", FS.format("SoundCollectah", 109746)),
    ("sand_pour", "ash/fs326304_sandfall5_Wagna.wav", 1.000, 2.200, "sandfall5.wav", "Wagna", FS.format("Wagna", 326304)),
    ("salt_grain", "ash/fs137259_salt_pour_xenognosis.wav", 2.400, 3.200, "Salt pour.wav", "xenognosis", FS.format("xenognosis", 137259)),
    # RESONANCE: the low internal body
    ("drum_res", "thump/fs631945_underwater_drum_hit_MathewHenry.wav", 0.000, 1.300, "Underwater Drum Hit E1", "MathewHenry", FS.format("MathewHenry", 631945)),
    ("body_thud", "thump/fs442344_deep_thud_punch_toddcircle.wav", 3.300, 3.900, "Deep thud punch.wav", "toddcircle", FS.format("toddcircle", 442344)),
    ("body_thud_b", "thump/fs442344_deep_thud_punch_toddcircle.wav", 1.800, 2.400, "Deep thud punch.wav", "toddcircle", FS.format("toddcircle", 442344)),
    ("tube_hit", "thump/fs442350_cardboard_tube_hits_toddcircle.wav", 3.800, 4.400, "cardboard tube hits.wav", "toddcircle", FS.format("toddcircle", 442350)),
    ("body_hit", "thump/fs276600_body_hit_insanity54.wav", 0.200, 0.620, "body_hit.wav", "insanity54", FS.format("insanity54", 276600)),
]


def extract(src_dir: str) -> None:
    """Cut each excerpt (DC removed, 2 ms fade in, 8 ms fade out) into foley/brand_curse/ and write SOURCES.md."""
    os.makedirs(FOLEY, exist_ok=True)
    lines = ["# BRAND curse foley sources (CC0 1.0)", "",
             "Excerpts of REAL recordings, cut by `make_brand_cues.py --extract`. Every source is **CC0 1.0** "
             "(https://creativecommons.org/publicdomain/zero/1.0/): public domain, no attribution required, commercial use "
             "allowed. The licence was checked on each sound's own page (2026-10-02: the page's licence link is "
             "creativecommons.org/publicdomain/zero/1.0/). Downloaded as Freesound's preview, converted to mono 16-bit "
             "44.1 kHz. Credited anyway, with thanks.", "",
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


@functools.lru_cache(maxsize=None)
def _src(name: str) -> np.ndarray:
    return norm(read_wav(os.path.join(FOLEY, name + ".wav")))


def src(name: str) -> np.ndarray:
    return _src(name).copy()


# ── helpers (local; the shared DSP is imported, never edited) ────────────────────────────────────

def ms(v: float) -> int:
    return int(round(v / 1000 * SR))


def seg(x: np.ndarray, t0: float = 0.0, t1: float | None = None, fin: float = 0.004, fout: float = 0.015) -> np.ndarray:
    """A slice (s) with raised-cosine fades in and out: a bare slice clicks."""
    y = x[int(t0 * SR):None if t1 is None else int(t1 * SR)].copy()
    a, b = min(len(y) // 2, int(fin * SR)), min(len(y) // 2, int(fout * SR))
    if a:
        y[:a] *= 0.5 - 0.5 * np.cos(np.pi * np.arange(a) / a)
    if b:
        y[-b:] *= 0.5 + 0.5 * np.cos(np.pi * np.arange(b) / b)
    return y


def env(n: int, pts: list[tuple[float, float]]) -> np.ndarray:
    """An envelope through (time ms, gain) points, smoothstep between them, flat outside: every layer's shape (its last
    point at 0 is its release)."""
    t = np.arange(n) / SR * 1000
    e = np.full(n, pts[0][1], dtype=float)
    for (t0, g0), (t1, g1) in zip(pts, pts[1:]):
        m = (t >= t0) & (t < t1)
        u = (t[m] - t0) / max(1e-9, t1 - t0)
        e[m] = g0 + (g1 - g0) * u * u * (3 - 2 * u)
    e[t >= pts[-1][0]] = pts[-1][1]
    return e


def stretch(x: np.ndarray, k: float, rng: np.random.Generator, grain: float = 0.05) -> np.ndarray:
    """Granular overlap-add time-stretch by k (pitch kept): for noisy textures (air, hiss, sand), never tones."""
    g = int(grain * SR)
    hop = g // 4
    w = np.hanning(g)
    n = int(len(x) * k)
    out, wsum = np.zeros(n + g), np.zeros(n + g)
    for p in range(0, n, hop):
        q = int(p / k + rng.uniform(-0.5, 0.5) * hop)
        q = min(max(q, 0), len(x) - g - 1)
        out[p:p + g] += x[q:q + g] * w
        wsum[p:p + g] += w
    return (out / np.maximum(wsum, 1e-3))[:n]


def repitch(x: np.ndarray, semis: float) -> np.ndarray:
    """Resample by 2^(semis/12) (tape-style: lower and longer, or higher and shorter, band-limited first)."""
    r = 2 ** (semis / 12)
    if r > 1:
        x = band(x, None, SR / 2 / r * 0.9)
    return np.interp(np.arange(0, len(x) - 1, r), np.arange(len(x)), x)


def sweep(x: np.ndarray, f0: float, f1: float, width: float, steps: int = 12) -> np.ndarray:
    """A band (width octaves) whose centre glides f0 -> f1 over x: the crawl moving through the body."""
    n = len(x)
    out = np.zeros(n)
    t = np.linspace(0, 1, n)
    cs = np.geomspace(f0, f1, steps)
    for i, c in enumerate(cs):
        w = np.clip(1 - np.abs(t * (steps - 1) - i), 0, 1)
        out += band(x, c / 2 ** (width / 2), c * 2 ** (width / 2), 0.4) * w
    return out


def flutter(n: int, rate: float, depth: float, rng: np.random.Generator) -> np.ndarray:
    """A slow unsteady amplitude (a spectral flicker), 1 +- depth."""
    return np.clip(1 + depth * lpnoise(n, rate, rng), 0.1, 2.0)


def grains(name: str, lo: float, hi: float, count: int) -> list[np.ndarray]:
    """The strongest isolated transients of a dry source (its lo..hi band), each cut alone with a fast rise and a 7 ms
    fall: the corruption's dry grains (a crackle, a creak's tick)."""
    h = band(src(name), lo, hi)
    k = max(1, int(0.0015 * SR))
    e = np.sqrt(np.convolve(h * h, np.ones(k) / k, "same"))
    rise, fall = int(0.001 * SR), int(0.03 * SR)
    picks: list[int] = []
    for i in np.argsort(e)[::-1]:
        if rise <= i < len(h) - fall and all(abs(i - j) > rise + fall for j in picks):
            picks.append(int(i))
        if len(picks) >= count:
            break
    w = np.concatenate([0.5 - 0.5 * np.cos(np.pi * np.arange(rise) / rise), np.exp(-np.arange(fall) / (0.007 * SR))])
    w[-int(0.004 * SR):] *= np.linspace(1, 0, int(0.004 * SR))
    return [norm(h[i - rise:i + fall] * w) for i in picks]


def scatter(n: int, gs: list[np.ndarray], times: list[float], gains: list[float], reverse: bool = False) -> np.ndarray:
    """Grains pasted at times (ms), cycling through the set; reversed grains swell INTO their tick."""
    out = np.zeros(n)
    for i, (t, g) in enumerate(zip(times, gains)):
        x = gs[i % len(gs)]
        x = x[::-1] if reverse else x
        place(out, t / 1000 - (len(x) / SR if reverse else 0.001), x, g)
    return out


def times_between(t0: float, t1: float, count: int, rng: np.random.Generator, accel: float = 1.0) -> list[float]:
    """`count` moments (ms) between t0 and t1, never a grid; accel > 1 crowds them toward t1 (a build), < 1 toward t0."""
    u = np.sort(rng.uniform(0, 1, count)) ** (1 / accel)
    return list(t0 + (t1 - t0) * u)


def onset(x: np.ndarray, share: float = 0.5, lo: float | None = None, hi: float | None = None) -> float:
    """Where x first reaches `share` of its peak (s), on a 1 ms envelope of its lo..hi band: an excerpt's hit."""
    y = band(x, lo, hi) if (lo or hi) else x
    k = max(1, int(0.001 * SR))
    e = np.convolve(np.abs(y), np.ones(k) / k, mode="same")
    return float(np.argmax(e >= share * e.max()) / SR)


def hit_at(buf: np.ndarray, t_ms: float, x: np.ndarray, gain: float) -> None:
    """Place x so its hit (onset) lands at t_ms."""
    place(buf, t_ms / 1000 - onset(x, 0.5), x, gain)


# ── the family: one source family, three weightings (TEXTURE, RESONANCE, DRY) ────────────────────

class Family:
    """One candidate's family. The same layers recur in every cue it builds; only timing and intensity change."""

    def __init__(self, name: str, seed: int):
        self.name = name
        self.rng = np.random.default_rng(seed)

    # RESONANCE: the low internal body (a recorded underwater drum, pitched down)
    def res(self, n: int, at_ms: float, decay_ms: float, gain: float, end_ms: float | None = None, reverse_from: float | None = None) -> np.ndarray:
        """The low internal resonance from at_ms (its attack soft: the body answers, it is not struck), decaying over
        decay_ms and released to ZERO by end_ms; reverse_from: a REVERSED copy swelling from that ms into at_ms (B)."""
        d = src("drum_res")
        d = seg(d, 0.035, None, 0.010, 0.05)                          # the body, not the stick
        if self.name == "A":
            r = band(repitch(d, -4.0), 40, 320)
        elif self.name == "B":                                          # corrupted: two voices a quarter-tone apart beat
            r1, r2 = band(repitch(d, -5.0), 45, 520), band(repitch(d, -4.55), 45, 520)
            r = r1[:len(r2)] + 0.8 * r2
        else:
            r = band(repitch(d, -3.0), 45, 650)
        r = norm(r)
        out = np.zeros(n)
        end = end_ms if end_ms is not None else n / SR * 1000
        place(out, at_ms / 1000, r, gain)
        out *= env(n, [(at_ms - 1, 0.0), (at_ms + 22, 1.0), (at_ms + 22 + decay_ms, 0.06), (end, 0.0)]) if at_ms > 1 else \
            env(n, [(0, 0.0), (at_ms + 22, 1.0), (at_ms + 22 + decay_ms, 0.06), (end, 0.0)])
        if reverse_from is not None:                                   # the swell INTO the hold, reversed
            span = (at_ms - reverse_from) / 1000
            rv = r[:int(span * SR)][::-1].copy()
            rv *= env(len(rv), [(0, 0.0), (span * 1000 * 0.5, 0.25), (span * 1000 - 12, 1.0), (span * 1000 - 3, 0.0)])
            place(out, reverse_from / 1000, rv, gain * 0.7)
        return out

    def thud(self, n: int, at_ms: float, gain: float) -> np.ndarray:
        """The TAKE-HOLD: a muffled body thud under the skin (never a punch: band-limited, its tail cut to a body)."""
        out = np.zeros(n)
        t = np.arange(n) / SR
        if self.name == "A":
            hit_at(out, at_ms, band(src("body_thud"), 80, 700), gain)
            hit_at(out, at_ms, band(src("body_hit"), 160, 1400), gain * 0.30)       # the body's own give
        elif self.name == "B":
            hit_at(out, at_ms, band(src("body_thud_b"), 75, 520), gain)
            hit_at(out, at_ms + 1, band(src("tube_hit"), 220, 1600), gain * 0.40)   # the hollow of the body
        else:
            hit_at(out, at_ms, band(src("body_thud"), 85, 900), gain * 0.80)
            hit_at(out, at_ms, band(src("body_hit"), 110, 2200), gain * 0.55)          # tissue, close
        decay = {"A": 0.075, "B": 0.10, "C": 0.06}[self.name]
        return out * np.where(t < at_ms / 1000 + 0.03, 1.0, np.exp(-(t - at_ms / 1000 - 0.03) / decay))

    # TEXTURE: the curse's breath (in = drawn inward toward the hold, out = released, bed = under the tail)
    def tex_in(self, n: int, t0: float, t1: float, gain: float) -> np.ndarray:
        """A swell from t0 culminating at t1 (ms) and handing over by t1+25: the suction / reversed hiss / dry build."""
        span = (t1 - t0) / 1000 + 0.03
        k = int(span * SR)
        if self.name == "A":                                            # a suction drawn inward, and the air with it
            x = 0.75 * band(stretch(seg(src("suck_pipe"), 0.1), 1.0, self.rng)[:k], 250, 3200) \
                + 0.6 * band(seg(src("air_draw"), 0.0)[:k], 180, 2400)
        elif self.name == "B":                                          # a REVERSED hiss and air: the classic inward pull
            x = band(src("steam")[::-1][-k:] if len(src("steam")) > k else src("steam")[::-1], 1200, 9000) \
                + 0.5 * band(src("air_exhale")[::-1][-k:], 600, 5000)
        else:                                                           # a fire's steam tightening, crackle crowding in
            x = 0.55 * band(seg(src("fire_steam"), 0.2)[:k], 700, 9000)
        x = np.pad(x, (0, max(0, k - len(x))))[:k]
        x = norm(x) * env(k, [(0, 0.0), (span * 1000 * 0.55, 0.22), (span * 1000 - 30, 1.0), (span * 1000 - 5, 0.25), (span * 1000, 0.0)])
        out = np.zeros(n)
        place(out, t0 / 1000, x, gain)
        if self.name == "C":                                            # the crackle crowds toward the hold
            g = grains("paper_burn", 1200, 11000, 12)
            ts = times_between(t0 + 10, t1 - 6, 9, self.rng, accel=2.2)
            out += scatter(n, g, ts, [gain * (0.35 + 0.55 * i / 8) for i in range(9)])
        return out

    def tex_out(self, n: int, t0: float, dur: float, gain: float) -> np.ndarray:
        """Released from t0 (ms) over dur ms, to zero: the exhale after the hold."""
        k = int(dur / 1000 * SR)
        if self.name == "A":
            x = band(stretch(seg(src("air_exhale"), 0.15), 1.25, self.rng), 150, 2000)
        elif self.name == "B":                                          # a spectral flicker: high hiss, fluttering
            x = band(stretch(seg(src("steam"), 0.35), 1.5, self.rng), 2200, 8000)
            x = x[:k] * flutter(min(k, len(x)), 14.0, 0.55, self.rng) if len(x) >= k else x
            x = np.pad(x, (0, max(0, k - len(x))))[:k] + 0.6 * band(seg(src("air_exhale"), 0.2)[:k], 900, 4500)
        else:                                                           # ash and sand pouring out
            x = band(seg(src("sand_pour"), 0.3)[:k], 700, 10000) + 0.6 * band(seg(src("dust_fall"), 0.18)[:k], 400, 9000)
        x = np.pad(x, (0, max(0, k - len(x))))[:k]
        x = norm(x) * env(k, [(0, 0.0), (35, 1.0), (dur * 0.35, 0.55), (dur, 0.0)])
        out = np.zeros(n)
        place(out, t0 / 1000, x, gain)
        return out

    # DRY: the corruption's grain
    def dry(self, n: int, t0: float, t1: float, count: int, gain: float, accel: float = 0.6) -> np.ndarray:
        """Sparse dry corruption grains from t0 to t1 (ms), thinning out (accel < 1): A a leather creak's grain, B
        reversed crackle (each grain swells into its tick), C burning paper's crackle and a leaf's crush."""
        t1 = min(t1, n / SR * 1000 - 150)                               # never a grain into the release
        ts = times_between(t0, max(t0 + 10, t1), count, self.rng, accel)
        gs_ = [gain * (1 - 0.6 * i / max(1, count - 1)) for i in range(count)]
        if self.name == "A":
            return scatter(n, grains("leather_crawl", 500, 6000, 10), ts, gs_)
        if self.name == "B":
            return scatter(n, grains("paper_burn", 1500, 10000, 12), ts, gs_, reverse=True)
        return scatter(n, grains("paper_burn", 900, 11000, 14), ts, gs_) + \
            scatter(n, grains("leaf_crush", 1500, 10000, 8), [t + 9 for t in ts[::2]], [g * 0.5 for g in gs_[::2]])

    def snap(self, n: int, at_ms: float, gain: float) -> np.ndarray:
        """A brittle internal crack (a small twig), at its hit."""
        out = np.zeros(n)
        hit_at(out, at_ms, band(src("twig_snap"), 1200, 11000), gain)
        hit_at(out, at_ms + 2, band(src("twig_snap_b"), 1800, 11000), gain * 0.4)
        t = np.arange(n) / SR
        return out * np.where(t < at_ms / 1000 + 0.02, 1.0, np.exp(-(t - at_ms / 1000 - 0.02) / 0.03))

    def crawl(self, n: int, t0: float, t1: float, gain: float) -> np.ndarray:
        """Under-skin travel (deepen, t0..t1 ms): A a muffled cloth scrape climbing, B a hiss band gliding up (spectral),
        C a leather creak crawling (tissue)."""
        k = int((t1 - t0 + 40) / 1000 * SR)
        if self.name == "A":
            x = sweep(np.pad(band(seg(src("cloth_scrape"), 0.05), 120, 4000), (0, k))[:k], 260, 900, 1.4)
            x += 0.35 * sweep(np.pad(seg(src("stone_scrape"), 0.1), (0, k))[:k], 400, 1500, 1.2)
        elif self.name == "B":
            base = stretch(seg(src("air_exhale"), 0.3), 1.0, self.rng)
            x = sweep(np.pad(base, (0, k))[:k], 900, 3600, 1.0) * flutter(k, 18.0, 0.45, self.rng)
        else:
            x = band(np.pad(seg(src("leather_crawl"), 0.4), (0, k))[:k], 300, 7000)
            x += 0.4 * sweep(np.pad(band(seg(src("cloth_scrape"), 0.05), 120, 4000), (0, k))[:k], 300, 1200, 1.4)
        span = t1 - t0 + 40
        x = norm(x) * env(k, [(0, 0.0), (span * 0.25, 0.5), (span - 60, 1.0), (span - 20, 0.5), (span, 0.0)])
        out = np.zeros(n)
        place(out, t0 / 1000, x, gain)
        return out


# ── the seven cues ───────────────────────────────────────────────────────────────────────────────

LENGTH = {  # ms, per candidate (inside the brief's ranges)
    "A": {"apply": 740, "deepen": 680, "deepen_deep": 800, "infect": 250, "leave": 620, "awaken": 520, "ash": 200},
    "B": {"apply": 780, "deepen": 700, "deepen_deep": 830, "infect": 270, "leave": 660, "awaken": 560, "ash": 210},
    "C": {"apply": 720, "deepen": 670, "deepen_deep": 790, "infect": 240, "leave": 600, "awaken": 500, "ash": 200},
}

# per-candidate weights of the family's three parts (TEXTURE, RESONANCE, DRY) and of the take-hold
MIX = {
    "A": {"tex": 0.60, "res": 0.20, "dry": 0.22, "thud": 0.80, "snap": 0.10},
    "B": {"tex": 0.55, "res": 0.14, "dry": 0.30, "thud": 0.70, "snap": 0.08},
    "C": {"tex": 0.45, "res": 0.16, "dry": 0.32, "thud": 0.85, "snap": 0.35},
}


def apply_cue(F: Family) -> np.ndarray:
    """reverse / suction lead-in -> a dark internal take-hold (180 ms) -> a short spectral / corruption tail."""
    L, T, M = LENGTH[F.name]["apply"], ONSET_MS["apply"], MIX[F.name]
    n = ms(L)
    y = F.tex_in(n, 0, T, M["tex"] * 0.9)
    y += F.thud(n, T, M["thud"])
    y += F.res(n, T - 4, 260, M["res"], L - 20, reverse_from=20 if F.name == "B" else None)
    y += F.snap(n, T + 3, M["snap"])
    y += F.tex_out(n, T + 60, L - T - 80, M["tex"] * 0.45)
    y += F.dry(n, T + 70, L - 120, 7, M["dry"] * 0.7)
    return y


def deepen_cue(F: Family, deep: bool = False) -> np.ndarray:
    """the existing corruption wakes (0-80) -> a low under-skin crawl (0-170+) -> a short take-hold at 230; deep: one
    extra corruption layer, a little more low-mid body, a longer tail -- never louder (the master sets the level)."""
    key = "deepen_deep" if deep else "deepen"
    L, T, M = LENGTH[F.name][key], ONSET_MS[key], MIX[F.name]
    n = ms(L)
    y = F.dry(n, 8, 80, 4, M["dry"] * 0.35, accel=1.0)                 # the wake: the old corruption stirs
    y += F.crawl(n, 0, 200, M["tex"] * 0.8)
    y += F.thud(n, T, M["thud"] * 0.85)
    y += F.res(n, T - 4, 220 if not deep else 300, M["res"] * (0.85 if not deep else 1.0), L - 20,
               reverse_from=T - 120 if F.name == "B" else None)
    y += F.snap(n, T + 3, M["snap"] * 0.8)
    y += F.tex_out(n, T + 60, L - T - 80, M["tex"] * 0.32)
    y += F.dry(n, T + 30, L - 110, 5, M["dry"] * 0.8)
    if deep:                                                          # the extra corruption layer, and low-mid body
        y += F.dry(n, T + 25, L - 60, 9, M["dry"] * 0.9, accel=0.8)
        b = np.zeros(n)
        hit_at(b, T + 4, band(src("body_thud_b"), 120, 700), 1.0)
        t = np.arange(n) / SR
        y += 0.30 * M["thud"] * b * np.where(t < T / 1000 + 0.05, 1.0, np.exp(-(t - T / 1000 - 0.05) / 0.12))
    return y


def infect_cue(F: Family) -> np.ndarray:
    """a short, quieter relative of the apply's take-hold, no lead-in (15 ms)."""
    L, T, M = LENGTH[F.name]["infect"], ONSET_MS["infect"], MIX[F.name]
    n = ms(L)
    y = F.thud(n, T, M["thud"] * 0.75)
    y += F.res(n, T - 2, 90, M["res"] * 0.45, L - 12)
    y += F.snap(n, T + 3, M["snap"] * 1.3)
    y += F.tex_out(n, T + 10, L - T - 30, M["tex"] * 0.55)
    y += F.dry(n, T + 25, L - 60, 3, M["dry"] * 0.8)
    return y


def leave_cue(F: Family) -> np.ndarray:
    """a brief curse flare on the fall (10 ms) -> an inward collapse / suction -> a soft ash / shadow exhale."""
    L, T, M = LENGTH[F.name]["leave"], ONSET_MS["leave"], MIX[F.name]
    n = ms(L)
    y = np.zeros(n)
    if F.name == "A":                                                   # a soft air flare
        place(y, T / 1000 - 0.004, band(seg(src("gas_puff"), 0.0, 0.12, 0.003, 0.06), 300, 3500), M["tex"] * 0.9)
    elif F.name == "B":                                                 # a bright spectral flash
        place(y, T / 1000 - 0.003, band(seg(src("steam"), 0.40, 0.52, 0.003, 0.08), 2000, 9000), M["tex"] * 1.0)
    else:                                                               # a crackle burst
        y += F.dry(n, T, T + 70, 6, M["dry"] * 1.2, accel=0.5)
    y += F.thud(n, T, M["thud"] * 0.55)
    y += F.snap(n, T + 2, M["snap"])
    y += F.res(n, T, 140, M["res"] * 0.6, 420)
    y += F.tex_in(n, 110, 380, M["tex"] * 0.65)                         # the inward collapse
    y += F.res(n, 380, 160, M["res"] * 0.35, L - 30, reverse_from=200 if F.name == "B" else None)
    y += F.tex_out(n, 360, L - 380, M["tex"] * 0.55)                    # the exhale
    y += F.dry(n, 400, L - 80, 4, M["dry"] * 0.6)
    return y


def awaken_cue(F: Family) -> np.ndarray:
    """a delayed internal inhale (0-120) -> a short take-hold as the territories bloom (120 ms)."""
    L, T, M = LENGTH[F.name]["awaken"], ONSET_MS["awaken"], MIX[F.name]
    n = ms(L)
    y = F.tex_in(n, 0, T, M["tex"] * 0.8)
    y += F.thud(n, T, M["thud"] * 0.95)
    y += F.res(n, T - 4, 200, M["res"] * 0.9, L - 20, reverse_from=10 if F.name == "B" else None)
    y += F.snap(n, T + 3, M["snap"])
    y += F.tex_out(n, T + 60, L - T - 80, M["tex"] * 0.35)
    y += F.dry(n, T + 30, L - 100, 5, M["dry"])
    return y


def ash_cue(F: Family) -> np.ndarray:
    """a dry crumble: a brittle internal crack (5 ms), a crumble of ash, a dusty exhale; ~200 ms."""
    L, T = LENGTH[F.name]["ash"], ONSET_MS["ash"]
    n = ms(L)
    crack = {"A": 0.70, "B": 0.80, "C": 1.0}[F.name]
    y = F.snap(n, T, crack)
    d = band(seg(src("dust_fall"), 0.22, 0.22 + L / 1000, 0.006, 0.05), 500, 10000)
    place(y, (T + 6) / 1000, norm(d) * env(len(d), [(0, 0.0), (45, 1.0), (L * 0.5, 0.5), (L - 25, 0.0)]),
          {"A": 0.30, "B": 0.25, "C": 0.40}[F.name])
    g = grains("salt_grain", 1500, 11000, 8)
    y += scatter(n, g, times_between(T + 35, L - 50, 6, F.rng, 0.6), [0.09 * crack * (1 - 0.12 * i) for i in range(6)])
    ex = band(seg(src("air_exhale"), 0.3), 300, 2200) if F.name != "B" else band(seg(src("steam"), 0.5), 1800, 7000)
    ex = ex[:ms(L - 40)]
    place(y, (T + 25) / 1000, norm(ex) * env(len(ex), [(0, 0.0), (40, 1.0), (len(ex) / SR * 1000, 0.0)]),
          {"A": 0.22, "B": 0.20, "C": 0.16}[F.name])
    b = np.zeros(n)
    hit_at(b, T + 1, band(src("body_thud"), 120, 900), 1.0)            # the crack happens INSIDE: a small low knock
    t = np.arange(n) / SR
    y += 0.22 * b * np.exp(-np.maximum(0, t - T / 1000 - 0.01) / 0.03)
    return y


CUES = {"apply": apply_cue, "deepen": deepen_cue, "deepen_deep": lambda F: deepen_cue(F, True), "infect": infect_cue,
        "leave": leave_cue, "awaken": awaken_cue, "ash": ash_cue}
NAMES = {"A": "subtle", "B": "supernatural", "C": "organic"}


# ── master: one loudness per cue, a release on everything, the true peak under PEAK ──────────────

def k50_of(y: np.ndarray, volume: float) -> float:
    """The loudest 50 ms, K-weighted, of y played at `volume` x the SFX master, dB."""
    return 20 * np.log10(max(1e-12, max1(k_weight(y * volume * MASTER_SFX), 0.05)))


def master(y: np.ndarray, key: str, length_ms: float) -> tuple[np.ndarray, float]:
    """A light 4x-oversampled saturation; nothing under 28 Hz or over 15 kHz; a 1 ms fade in (the take-hold sits >= 5 ms
    in) and a 45 ms squared release to zero; the K-weighted loudest 50 ms at the brief's target for its volume; the true
    peak kept under PEAK by driving the saturation further (never a hard clip). Returns (cue, drive)."""
    n = ms(length_ms)
    y = np.pad(y, (0, max(0, n - len(y))))[:n]
    y = band(y - y.mean(), 28, 15000)
    for drive in (1.2, 1.6, 2.0, 2.6, 3.2, 4.0):
        z = norm(sat_os(norm(y), drive))
        z[-ms(45):] *= np.linspace(1, 0, ms(45)) ** 2
        z[:ms(1)] *= np.linspace(0, 1, ms(1))
        z *= 10 ** ((TARGET[key] - k50_of(z, VOLUME[key])) / 20)
        if true_peak(z) <= PEAK:
            return z, drive
    return z / true_peak(z) * PEAK, drive                             # the target missed (reported by the QA)


def build(letter: str, key: str) -> tuple[np.ndarray, float]:
    F = Family(letter, SEED + "ABC".index(letter) * 101 + KEYS.index(key))
    return master(CUES[key](F), key, LENGTH[letter][key])


# ── QA ───────────────────────────────────────────────────────────────────────────────────────────

def transient(y: np.ndarray) -> float:
    """The take-hold transient (ms): the cue's LOUDEST moment (the K-weighted 4 ms envelope's peak) traced back to where
    that event first reached half its level (-6 dB), within the 80 ms before it -- the hit's onset."""
    k = ms(4)
    e = np.sqrt(np.convolve(k_weight(y) ** 2, np.ones(k) / k, "same"))
    p = int(np.argmax(e))
    a = max(0, p - ms(80))
    below = np.nonzero(e[a:p + 1] < 0.5 * e[p])[0]
    i = a + (below[-1] + 1 if len(below) else 0)
    return float(i / SR * 1000)


def measure(y: np.ndarray, key: str) -> dict:
    S = np.abs(np.fft.rfft(y)) ** 2
    f = np.fft.rfftfreq(len(y), 1 / SR)
    tot = S.sum()
    bands = [S[(f >= a) & (f < b)].sum() / tot for a, b in ((0, 150), (150, 1000), (1000, 4000), (4000, 30000))]
    edge = (float(np.abs(y[:ms(5)]).max()), float(np.abs(y[-ms(5):]).max()))
    return {"length_ms": len(y) / SR * 1000, "transient_ms": transient(y), "brief_ms": ONSET_MS[key], "bands": bands,
            "max50_db": 20 * np.log10(max1(y, 0.05)), "k50_played": k50_of(y, VOLUME[key]),
            "true_peak": true_peak(y), "edge": edge}


def path_of(letter: str, key: str) -> str:
    return os.path.join(HISTORY, letter, PREFIX + key + ".wav")


def main() -> int:
    if len(sys.argv) > 2 and sys.argv[1] == "--extract":
        extract(sys.argv[2])
        return 0
    which = sys.argv[1:] or ["A", "B", "C"]
    for letter in which:
        print(f"== {letter} {NAMES[letter]}")
        for key in KEYS:
            y, drive = build(letter, key)
            write_wav(path_of(letter, key), y)
            if letter == SHIPPED:
                write_wav(os.path.join(COMBAT, PREFIX + key + ".wav"), y)
            m = measure(y, key)
            print(f"  {key:12s} {m['length_ms']:4.0f} ms  transient {m['transient_ms']:5.1f} (brief {m['brief_ms']:3.0f})  "
                  f"bands <150 {m['bands'][0]:.2f} 150-1k {m['bands'][1]:.2f} 1-4k {m['bands'][2]:.2f} >4k {m['bands'][3]:.2f}  "
                  f"max50 {m['max50_db']:5.1f}  K50@{VOLUME[key]:.3f} {m['k50_played']:5.1f} (target {TARGET[key]:5.1f})  "
                  f"TP {m['true_peak']:.3f}  drive {drive}  edges {m['edge'][0]:.4f}/{m['edge'][1]:.4f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
