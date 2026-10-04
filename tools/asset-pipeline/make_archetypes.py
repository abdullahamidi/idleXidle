#!/usr/bin/env python3
"""make_archetypes.py -- the eight ARCHETYPE cues of the remaining-skill sweep (design.md section 7, Phase 1 / P1.5).

"Archetypes first, identity second": every item of the sweep resolves its sound through a chain, most specific first
(`sfx_<champ>_<skill>_<moment>` -> `sfx_<skill>_<moment>` -> ARCHETYPE -> generic), so these eight files give every item a
non-generic sound from its first film, and a per-item identity cue replaces one only where the item's identity is in its
sound. ONE candidate each, built from REAL recorded foley (CC0 1.0, Freesound; the licence checked on each sound's own
page, see foley/archetypes/SOURCES.md); synthesis only for a small sub inside a foley cue (the fist and the stone).

  sfx_fist_hit       a padded fist             a punching bag + a body blow, a little knuckle snap, a small sub
  sfx_blade_hit      a short cut into leather  a knife into flesh + a leather strap on skin, a faint steel edge
  sfx_stone_hit      a stone on packed earth   a heavy stone impact + a brick on soil, a soil click, a gravel tick
  sfx_wood_hit       a wooden thump with a rim a crate thump + a barrel knock, a wooden shield's face, a bucket's rim
  sfx_throw_release  a cloth whip + air        a thrown swish + clothes whipping, a shirt's snap on the release
  sfx_air_release    an air push, no tone      a puff of smoke + a puff of air, a short air burst (no pitch)
  sfx_field_tick     a quiet material settle   two pebbles settling + a grain of poured gravel, dark, no crack
  sfx_cloth_commit   a boot plant + cloth      a boot on the floor + a stomp, a clothing ruffle around it

Each cue is MASTERED TO ITS CEILING in the fight's own mix, by the PRESS method (make_press_tick.k50: the loudest 50 ms,
ITU-R BS.1770 K-weighted, of the file x its play volume x the SFX master 0.8): a hit played at the T1 0.36 sits >= 3 dB
under SPRAY's contact (0.50) and HARD HANDS' hit (0.55); the field tick at the tick ceiling 0.34 sits under PRESS's tick
(0.28); a release, the commit, under SPRAY's release and HARD HANDS' commit. The measurements are written beside this
script (archetype_levels.json) and, with --evidence <dir>, as a table for the QA evidence.

The files are SHIPPED (design.md section 7's lower pin level), never human-approved: archetype_cue_test.cs pins each
one's SHA-256; a change is a deliberate commit, never a regeneration. The build is byte-reproducible (fixed seeds, the
excerpts committed). It imports the shared DSP from make_jaws_bite (never edited) and changes no approved cue.

    PYTHONUTF8=1 python tools/asset-pipeline/make_archetypes.py --extract <dir of the downloaded sources>   # once
    PYTHONUTF8=1 python tools/asset-pipeline/make_archetypes.py [--evidence <dir>]                         # build + measure
"""
from __future__ import annotations

import hashlib
import json
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from make_jaws_bite import SR, band, norm, place, read_wav, sat_os, true_peak, weight, write_wav  # noqa: E402  (the shared DSP)
from make_press_tick import k_weight, max1  # noqa: E402  (PRESS's K-weighting and loudest-window RMS, read only)

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
FOLEY = os.path.join(HERE, "foley", "archetypes")
COMBAT = os.path.join(REPO, "assets", "audio", "combat")
LEVELS = os.path.join(HERE, "archetype_levels.json")
MANIFEST = os.path.join(FOLEY, "archetype_manifest.json")
LICENCE = "CC0 1.0"
LICENCE_CHECKED = "2026-10-04"  # every source's own Freesound page links creativecommons.org/publicdomain/zero/1.0/
MASTER_SFX = 0.8                # SoundBank's default SFX master
PEAK = 0.37                     # the house true peak for a cue (~-8.6 dBFS)
SEED = 20261004

# The excerpts: (name, downloaded file, start s, end s, title, author, url). Every source is CC0 1.0.
FS = "https://freesound.org/people/{}/sounds/{}/"
EXCERPTS = [
    # FIST: a padded fist
    ("bag_hit", "fist/fs53985_punching_bag_sound_effects_qubodup.wav", 51.050, 51.300, "Punching Bag Sound Effects", "qubodup", FS.format("qubodup", 53985)),
    ("body_blow", "fist/fs369324_clarks_punch_hits_body_blows_aro_cryanrautha.wav", 16.540, 16.700, "CLARKS Punch Hits body blows around the room.wav", "cryanrautha", FS.format("cryanrautha", 369324)),
    ("punch_snap", "fist/fs517744_punch_danlucaz.wav", 0.455, 0.600, "Punch", "danlucaz", FS.format("danlucaz", 517744)),
    # BLADE: a short cut into leather
    ("knife_flesh", "blade/fs870739_knife_stabs_into_chicken_breasts_CHallSmith.wav", 12.050, 12.220, "Knife Stabs into Chicken Breasts #2", "CHallSmith", FS.format("CHallSmith", 870739)),
    ("leather_slap", "blade/fs744400_leather_strap_striking_skin_desiderium_audio.wav", 23.520, 23.700, "Leather Strap Striking Skin", "desiderium_audio", FS.format("desiderium_audio", 744400)),
    ("steel_stab", "blade/fs399617_knife_sword_stab_2_mp3_SoundsForHim.wav", 0.540, 0.660, "Knife/sword stab #2.MP3", "SoundsForHim", FS.format("SoundsForHim", 399617)),
    # STONE: a stone on packed earth
    ("stone_heavy", "stone/fs513694_impact_stone_heavy_wav_kasparsj.wav", 0.160, 0.360, "impact-stone-heavy.wav", "kasparsj", FS.format("kasparsj", 513694)),
    ("brick_soil", "stone/fs325268_brick_on_soil_wav_deleted_user_2104797.wav", 1.015, 1.150, "Brick on soil.wav", "deleted_user_2104797", FS.format("deleted_user_2104797", 325268)),
    ("soil_click", "stone/fs325268_brick_on_soil_wav_deleted_user_2104797.wav", 1.455, 1.540, "Brick on soil.wav", "deleted_user_2104797", FS.format("deleted_user_2104797", 325268)),
    ("pebble_tick", "settle/fs734669_stone_throwing_pebbles_Vrymaa.wav", 5.470, 5.530, "Stone - Throwing pebbles", "Vrymaa", FS.format("Vrymaa", 734669)),
    # WOOD: a wooden thump with a rim
    ("crate_thump", "wood/fs667654_wooden_crate_impact2_wav_DeltaCode.wav", 0.000, 0.200, "wooden-crate-impact2.wav", "DeltaCode", FS.format("DeltaCode", 667654)),
    ("barrel_knock", "wood/fs520146_wooden_barrel_one_hit_5_cwmcnutt.wav", 5.360, 5.480, "Wooden-Barrel_One-Hit_5", "cwmcnutt", FS.format("cwmcnutt", 520146)),
    ("shield_wood", "wood/fs138489_shield_hit_2_JustInvoke.wav", 0.000, 0.105, "Shield Hit 2", "JustInvoke", FS.format("JustInvoke", 138489)),
    ("bucket_rim", "wood/fs513800_hitting_metallic_bucket_with_woo_lartti.wav", 0.955, 1.120, "hitting metallic bucket with wooden stick.wav", "lartti", FS.format("lartti", 513800)),
    # THROW: a cloth whip + air
    ("cloth_whip", "throw/fs263454_clothes_whipping_AlecCorday.wav", 0.760, 0.960, "Clothes Whipping", "AlecCorday", FS.format("AlecCorday", 263454)),
    ("shirt_snap", "throw/fs742810_whipping_t_shirt_Sadiquecat.wav", 0.950, 1.050, "Whipping T-shirt", "Sadiquecat", FS.format("Sadiquecat", 742810)),
    ("air_swish", "throw/fs463763_throw_swish_x12_hza_07_03_2019_w_hz37.wav", 7.900, 8.150, "throw swish x12 HZA 07-03-2019.wav", "hz37", FS.format("hz37", 463763)),
    # AIR: an air push, no tone
    ("smoke_puff", "air/fs714257_puff_of_smoke_qubodup.wav", 0.000, 0.260, "Puff of Smoke", "qubodup", FS.format("qubodup", 714257)),
    ("air_puff", "air/fs817695_air_puff_waymonds.wav", 0.090, 0.420, "Air Puff", "waymonds", FS.format("waymonds", 817695)),
    ("air_burst", "air/fs138477_air_burst_JustInvoke.wav", 0.000, 0.165, "Air Burst", "JustInvoke", FS.format("JustInvoke", 138477)),
    # SETTLE: a quiet material settle
    ("pebble_settle", "settle/fs734669_stone_throwing_pebbles_Vrymaa.wav", 1.820, 1.900, "Stone - Throwing pebbles", "Vrymaa", FS.format("Vrymaa", 734669)),
    ("gravel_grain", "settle/fs588467_gravel_pouring_wav_cartoonrob.wav", 0.700, 0.900, "Gravel Pouring.wav", "cartoonrob", FS.format("cartoonrob", 588467)),
    ("gravel_push", "settle/fs450353_gravel_push_with_shoe_short_flac_kyles.wav", 0.190, 0.400, "gravel push with shoe short.flac", "kyles", FS.format("kyles", 450353)),
    # COMMIT: a boot plant + cloth
    ("boot_plant", "commit/fs392483_footsteps_boots_wav_gpag1.wav", 0.015, 0.130, "Footsteps boots.wav", "gpag1", FS.format("gpag1", 392483)),
    ("foot_stomp", "commit/fs733165_foot_stomp_3_locky_Y.wav", 0.000, 0.150, "Foot stomp 3", "locky_Y", FS.format("locky_Y", 733165)),
    ("cloth_ruffle", "commit/fs641383_clothing_ruffle_01_wav_WhiteFire43.wav", 6.330, 6.480, "Clothing Ruffle 01.wav", "WhiteFire43", FS.format("WhiteFire43", 641383)),
    ("cloth_ruffle_b", "commit/fs641383_clothing_ruffle_01_wav_WhiteFire43.wav", 4.780, 4.900, "Clothing Ruffle 01.wav", "WhiteFire43", FS.format("WhiteFire43", 641383)),
]


def extract(src_dir: str) -> None:
    """Cut each excerpt (DC removed, 2 ms fade in, 8 ms fade out, normalised to 0.9) into foley/archetypes/."""
    os.makedirs(FOLEY, exist_ok=True)
    for name, rel, t0, t1, *_ in EXCERPTS:
        x = read_wav(os.path.join(src_dir, rel))[int(t0 * SR):int(t1 * SR)]
        x = x - x.mean()
        x[:int(0.002 * SR)] *= np.linspace(0, 1, int(0.002 * SR))
        x[-int(0.008 * SR):] *= np.linspace(1, 0, int(0.008 * SR))
        write_wav(os.path.join(FOLEY, name + ".wav"), x / max(1e-9, np.abs(x).max()) * 0.9)
    print("extracted", len(EXCERPTS), "excerpts ->", FOLEY)


_READS: set[str] | None = None   # trace(): the excerpts a cue's build reads


def src(name: str) -> np.ndarray:
    if _READS is not None:
        _READS.add(name)
    return read_wav(os.path.join(FOLEY, name + ".wav"))


# ── helpers (local; the shared DSP is imported, never edited) ────────────────────────────────────

def onset(x: np.ndarray, share: float = 0.5) -> float:
    """Where x first reaches `share` of its peak (s), on a 1 ms envelope: the excerpt's hit."""
    k = max(1, int(0.001 * SR))
    e = np.convolve(np.abs(x), np.ones(k) / k, mode="same")
    return float(np.argmax(e >= share * e.max()) / SR)


def at_hit(buf: np.ndarray, t: float, x: np.ndarray, gain: float) -> None:
    """Place x so its HIT (onset) lands at t."""
    place(buf, t - onset(x), x, gain)


def tail(x: np.ndarray, from_s: float, tau: float) -> np.ndarray:
    """Shorten a layer: an exponential fade from `from_s` (s into x) -- a body, not a ring."""
    t = np.arange(len(x)) / SR
    return x * np.where(t < from_s, 1.0, np.exp(-(t - from_s) / tau))


def shaped(x: np.ndarray, rise: float, tau: float) -> np.ndarray:
    """A texture given an envelope of its own: a raised-cosine rise, then an exponential decay."""
    t = np.arange(len(x)) / SR
    up = np.where(t < rise, 0.5 - 0.5 * np.cos(np.pi * np.clip(t / rise, 0, 1)), 1.0)
    return x * up * np.exp(-np.maximum(0.0, t - rise) / tau)


def sub(dur: float, f_hi: float, f_end: float, attack: float, decay: float, rng: np.random.Generator) -> np.ndarray:
    """The only synthesised part: a small weight under a hit (pitch falling f_hi -> f_end), alive, released to zero."""
    return weight(dur, f_hi, 0.5 * (f_hi + f_end), f_end, 0.025, attack, 0.004, decay, rng, jit=0.06, shim=0.15, sat=1.3)


def k50(y: np.ndarray, volume: float) -> float:
    """The loudest 50 ms, K-weighted, of a cue played at `volume` x the SFX master, dB (make_press_tick.k50's method)."""
    return 20 * np.log10(max(1e-12, max1(k_weight(y * volume * MASTER_SFX), 0.05)))


def master(low: np.ndarray, top: np.ndarray, top_share: float, length: float, volume: float, target_k: float,
           hi: float = 15000.0) -> np.ndarray:
    """Two buses (the weight never squashes the detail): the top bus at `top_share` of the low bus' peak; a light 4x
    oversampled saturation; nothing above `hi`; a release to zero; then the gain that puts the loudest 50 ms, K-weighted
    as played at `volume`, exactly on `target_k` (the true peak never above PEAK: then it sits lower, never higher)."""
    n = int(length * SR)
    lo_b, tp_b = np.zeros(n), np.zeros(n)
    lo_b[:min(n, len(low))] = low[:n]
    tp_b[:min(n, len(top))] = top[:n]
    y = norm(lo_b) + (top_share * norm(tp_b) if top_share > 0 else 0.0)
    y = norm(sat_os(norm(y), 1.4))
    y = band(y, 28, hi)
    y[-int(0.03 * SR):] *= np.linspace(1, 0, int(0.03 * SR)) ** 2
    y[:int(0.0005 * SR)] *= np.linspace(0, 1, int(0.0005 * SR))
    y = y * 10 ** ((target_k - k50(y, volume)) / 20)
    tp = true_peak(y)
    if tp > PEAK:
        y = y / tp * PEAK
    return y


# ── The cues (T = the contact / release moment in the file) ──────────────────────────────────────

T_HIT = 0.004                   # a hit lands at once: the swing plays it on the contact frame


def fist() -> np.ndarray:
    """A PADDED FIST: a punching bag's heavy give and a body blow land together, a little knuckle snap on top, a small
    sub under them; no crack, no ring."""
    rng = np.random.default_rng(SEED + 1)
    n, T = int(0.24 * SR), T_HIT
    low, top = np.zeros(n), np.zeros(n)
    at_hit(low, T, tail(band(src("bag_hit"), 55, 3200), 0.050, 0.035), 1.0)
    at_hit(low, T + 0.001, tail(band(src("body_blow"), 70, 4000), 0.030, 0.025), 0.70)
    place(low, T - 0.002, sub(0.10, 96, 56, 0.004, 0.040, rng), 0.12)
    at_hit(top, T, tail(band(src("punch_snap"), 1500, 12000), 0.012, 0.010), 1.0)
    return master(low, top, 0.22, 0.24, VOLUME["sfx_fist_hit"], TARGET_K["sfx_fist_hit"])


def blade() -> np.ndarray:
    """A SHORT CUT INTO LEATHER: a knife into flesh and a leather strap on skin, a faint steel edge on the contact."""
    n, T = int(0.22 * SR), T_HIT
    low, top = np.zeros(n), np.zeros(n)
    at_hit(low, T, tail(band(src("knife_flesh"), 150, 9000), 0.030, 0.020), 1.0)
    at_hit(low, T + 0.002, tail(band(src("leather_slap"), 200, 8000), 0.035, 0.025), 0.80)
    at_hit(top, T, tail(band(src("steel_stab"), 3000, 14000), 0.020, 0.015), 1.0)
    return master(low, top, 0.30, 0.22, VOLUME["sfx_blade_hit"], TARGET_K["sfx_blade_hit"])


def stone() -> np.ndarray:
    """A STONE ON PACKED EARTH: a heavy stone impact and a brick landing on soil, a dry soil click, a short gravel tick
    after it, a small sub under the earth."""
    rng = np.random.default_rng(SEED + 3)
    n, T = int(0.24 * SR), T_HIT
    low, top = np.zeros(n), np.zeros(n)
    at_hit(low, T, tail(band(src("stone_heavy"), 60, 6000), 0.045, 0.030), 1.0)
    at_hit(low, T, tail(band(src("brick_soil"), 45, 2500), 0.040, 0.030), 0.80)
    place(low, T - 0.002, sub(0.09, 82, 50, 0.004, 0.035, rng), 0.10)
    at_hit(top, T + 0.002, tail(band(src("soil_click"), 1200, 12000), 0.015, 0.012), 0.60)
    at_hit(top, T + 0.028, tail(band(src("pebble_tick"), 1500, 12000), 0.012, 0.010), 0.50)
    return master(low, top, 0.38, 0.24, VOLUME["sfx_stone_hit"], TARGET_K["sfx_stone_hit"])


def wood() -> np.ndarray:
    """A WOODEN THUMP WITH A RIM: a crate's thump and a barrel's knock, a wooden shield's face on the contact, a bucket's
    rim rattling just after it, short."""
    n, T = int(0.24 * SR), T_HIT
    low, top = np.zeros(n), np.zeros(n)
    at_hit(low, T, tail(band(src("crate_thump"), 70, 5000), 0.045, 0.035), 1.0)
    at_hit(low, T, tail(band(src("barrel_knock"), 200, 6000), 0.040, 0.030), 0.60)
    at_hit(top, T + 0.001, tail(band(src("shield_wood"), 800, 12000), 0.030, 0.020), 0.80)
    at_hit(top, T + 0.012, tail(band(src("bucket_rim"), 600, 9000), 0.060, 0.045), 0.35)
    return master(low, top, 0.45, 0.24, VOLUME["sfx_wood_hit"], TARGET_K["sfx_wood_hit"])


def throw_release() -> np.ndarray:
    """A CLOTH WHIP + AIR: a thrown swish swells into the release (60 ms in), clothes whip on it, a shirt's snap on top."""
    n, T = int(0.20 * SR), 0.060
    low, top = np.zeros(n), np.zeros(n)
    at_hit(low, T, band(src("air_swish"), 150, 6000), 1.0)
    at_hit(low, T - 0.005, tail(band(src("cloth_whip"), 200, 10000), 0.060, 0.040), 0.90)
    at_hit(top, T, tail(band(src("shirt_snap"), 1000, 12000), 0.030, 0.020), 1.0)
    return master(low, top, 0.40, 0.20, VOLUME["sfx_throw_release"], TARGET_K["sfx_throw_release"])


def air_release() -> np.ndarray:
    """AN AIR PUSH, NO TONE: a puff of smoke and a puff of air pushed out together, a short air burst's hiss on top."""
    n, T = int(0.22 * SR), 0.030
    low, top = np.zeros(n), np.zeros(n)
    at_hit(low, T, tail(band(src("smoke_puff"), 70, 2500), 0.070, 0.050), 1.0)
    at_hit(low, T, tail(band(src("air_puff"), 300, 6000), 0.070, 0.045), 0.60)
    at_hit(top, T, tail(band(src("air_burst"), 1500, 10000), 0.030, 0.030), 0.50)
    return master(low, top, 0.30, 0.22, VOLUME["sfx_air_release"], TARGET_K["sfx_air_release"])


def field_tick() -> np.ndarray:
    """A QUIET MATERIAL SETTLE: two pebbles settle 45 ms apart over a grain of poured gravel and a gravel shift, dark
    (nothing above 7 kHz), no crack, no impact."""
    n, T = int(0.20 * SR), 0.012
    low, top = np.zeros(n), np.zeros(n)
    at_hit(low, T, tail(band(src("pebble_settle"), 300, 6000), 0.020, 0.020), 0.80)
    at_hit(low, T + 0.045, tail(band(src("pebble_tick"), 300, 6000), 0.015, 0.015), 0.50)
    place(low, T - 0.006, shaped(band(src("gravel_grain"), 400, 7000), 0.010, 0.060), 0.50)
    place(low, T + 0.020, shaped(band(src("gravel_push"), 300, 5000), 0.012, 0.050), 0.40)
    return master(low, top, 0.0, 0.20, VOLUME["sfx_field_tick"], TARGET_K["sfx_field_tick"], hi=7000.0)


def cloth_commit() -> np.ndarray:
    """A BOOT PLANT + CLOTH: the cloth moves first (12 ms before), the boot plants on T with a stomp under it, a second
    ruffle settles after."""
    n, T = int(0.22 * SR), 0.020
    low, top = np.zeros(n), np.zeros(n)
    at_hit(low, T, tail(band(src("boot_plant"), 60, 3000), 0.030, 0.025), 1.0)
    at_hit(low, T + 0.002, tail(band(src("foot_stomp"), 80, 5000), 0.030, 0.030), 0.60)
    at_hit(top, T - 0.012, tail(band(src("cloth_ruffle"), 300, 10000), 0.050, 0.040), 1.0)
    at_hit(top, T + 0.030, tail(band(src("cloth_ruffle_b"), 400, 10000), 0.040, 0.030), 0.50)
    return master(low, top, 0.60, 0.22, VOLUME["sfx_cloth_commit"], TARGET_K["sfx_cloth_commit"])


# key -> (build, what it is). The order is the design's.
CUES = {
    "sfx_fist_hit": (fist, "a padded fist"),
    "sfx_blade_hit": (blade, "a short cut into leather"),
    "sfx_stone_hit": (stone, "a stone on packed earth"),
    "sfx_wood_hit": (wood, "a wooden thump with a rim"),
    "sfx_throw_release": (throw_release, "a cloth whip + air"),
    "sfx_air_release": (air_release, "an air push, no tone"),
    "sfx_field_tick": (field_tick, "a quiet material settle"),
    "sfx_cloth_commit": (cloth_commit, "a boot plant + cloth"),
}
HITS = ["sfx_fist_hit", "sfx_blade_hit", "sfx_stone_hit", "sfx_wood_hit"]

# The volume each cue is MASTERED at (design.md sections 3 and 7): the T1 hit 0.36; the basic-attack release 0.17 (the
# 0.16-0.18 band's middle); the air release at PULSE's 0.24 (REPAY 0.26, DRINK's intake 0.22); the field tick at the tick
# ceiling 0.34 (PRESS 0.28 + 0.06); a commit at BLOW's 0.30 (0.28-0.34).
VOLUME = {"sfx_fist_hit": 0.36, "sfx_blade_hit": 0.36, "sfx_stone_hit": 0.36, "sfx_wood_hit": 0.36,
          "sfx_throw_release": 0.17, "sfx_air_release": 0.24, "sfx_field_tick": 0.34, "sfx_cloth_commit": 0.30}
# The K-weighted loudest 50 ms each is mastered TO (dB, as played): every hit 3.8 dB under SPRAY's contact (the lower
# ceiling; at the T1 band's top, 0.38, it is still 3.3 under, and a true-peak-limited file sits lower); the throw release ~7 dB under SPRAY's release (a basic's release is heard on every swing); the air release
# 5 dB under it; the field tick 1.7 dB under PRESS's tick (at the tick ceiling 0.34: at its consumers' 0.14-0.18 it is
# ~7 dB under); the commit ~1.7 dB under HARD HANDS' commit.
TARGET_K = {"sfx_fist_hit": -31.3, "sfx_blade_hit": -31.3, "sfx_stone_hit": -31.3, "sfx_wood_hit": -31.3,
            "sfx_throw_release": -37.0, "sfx_air_release": -35.0, "sfx_field_tick": -33.0, "sfx_cloth_commit": -35.0}

# The ceilings: (reference key, its play volume, the margin the archetype must keep under it, dB). A hit is checked
# against BOTH contact references.
CEILINGS = {
    "sfx_fist_hit": [("sfx_seeker_spray_hit", 0.50, 3.0), ("sfx_seeker_hard_hands_hit", 0.55, 3.0)],
    "sfx_blade_hit": [("sfx_seeker_spray_hit", 0.50, 3.0), ("sfx_seeker_hard_hands_hit", 0.55, 3.0)],
    "sfx_stone_hit": [("sfx_seeker_spray_hit", 0.50, 3.0), ("sfx_seeker_hard_hands_hit", 0.55, 3.0)],
    "sfx_wood_hit": [("sfx_seeker_spray_hit", 0.50, 3.0), ("sfx_seeker_hard_hands_hit", 0.55, 3.0)],
    "sfx_throw_release": [("sfx_seeker_spray_release", 0.40, 3.0)],
    "sfx_air_release": [("sfx_seeker_spray_release", 0.40, 3.0)],
    "sfx_field_tick": [("sfx_seeker_press_tick", 0.28, 0.0)],
    "sfx_cloth_commit": [("sfx_seeker_hard_hands_commit", 0.34, 0.0)],
}


def trace() -> dict[str, list[str]]:
    """Which excerpts each cue's build READS (excerpt -> the cue keys), read-only: nothing is written."""
    global _READS
    out: dict[str, list[str]] = {}
    try:
        for key, (build, _) in CUES.items():
            _READS = set()
            build()
            for name in _READS:
                out.setdefault(name, []).append(key)
    finally:
        _READS = None
    return out


def sha256(path: str) -> str:
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def write_sources(reads: dict[str, list[str]]) -> None:
    """SOURCES.md and the manifest: which recordings produced which shipped archetype cue, under what licence."""
    unused = [e[0] for e in EXCERPTS if e[0] not in reads]
    if unused:
        raise SystemExit(f"excerpts read by no cue (delete their files and EXCERPTS entries): {unused}")
    recordings: dict[str, tuple[str, str, list[str]]] = {}
    for name, _, _, _, title, author, url in EXCERPTS:
        recordings.setdefault(url, (title, author, []))[2].append(name)
    lines = [
        "# Archetype cue foley sources", "",
        "Generated by `make_archetypes.py`; do not edit by hand. It answers one question: **which source recordings "
        "produced the eight SHIPPED archetype cues (design.md section 7), and under what licence.**", "",
        f"- **Shipped:** {', '.join(f'`{k}`' for k in CUES)} in `assets/audio/combat/`, pinned by SHA-256 as SHIPPED "
        "(`archetype_manifest.json`, `tests/unit/IdleXIdle.Game.Tests/archetype_cue_test.cs`).",
        f"- **Licence:** every source below is **{LICENCE}** (https://creativecommons.org/publicdomain/zero/1.0/): public "
        "domain dedication, no attribution required, commercial use allowed. Credited anyway, with thanks.",
        f"- **Licence check:** {LICENCE_CHECKED}, on each sound's own Freesound page (the page's licence link is "
        "creativecommons.org/publicdomain/zero/1.0/).",
        "- **Processing:** downloaded as Freesound's preview into `build/tmp` (never committed), converted to mono 16-bit "
        "44.1 kHz; each excerpt is the range below, DC removed, a 2 ms fade in, an 8 ms fade out, normalised to 0.9. Only "
        "a small sub under the fist and the stone is synthesised.", "",
        "## Source recordings", "",
        "| source recording | author | Freesound page | licence | excerpts |", "|---|---|---|---|---|"]
    for url, (title, author, names) in recordings.items():
        lines.append(f"| {title} | {author} | {url} | {LICENCE} | {', '.join(f'`{n}`' for n in names)} |")
    lines += ["", "## Excerpts", "",
              "| excerpt | source recording | author | Freesound page | licence | range (s) | cues that use it |",
              "|---|---|---|---|---|---|---|"]
    for name, _, t0, t1, title, author, url in EXCERPTS:
        lines.append(f"| `{name}.wav` | {title} | {author} | {url} | {LICENCE} | {t0:.3f}-{t1:.3f} | "
                     f"{', '.join(f'`{k}`' for k in CUES if k in reads[name])} |")
    with open(os.path.join(FOLEY, "SOURCES.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    manifest = {
        "_generated_by": "tools/asset-pipeline/make_archetypes.py - do not edit",
        "pin_level": "SHIPPED (design.md section 7: never human-approved before the owner has heard the film)",
        "licence": {"name": LICENCE, "url": "https://creativecommons.org/publicdomain/zero/1.0/", "checked": LICENCE_CHECKED,
                    "how": "each source's own Freesound page links the CC0 1.0 deed"},
        "cues": {k: {"what": CUES[k][1], "file": f"assets/audio/combat/{k}.wav", "sha256": sha256(os.path.join(COMBAT, k + ".wav")),
                     "mastered_volume": VOLUME[k], "target_k50_db": TARGET_K[k],
                     "excerpts": sorted(n for n in reads if k in reads[n])} for k in CUES},
        "excerpts": [{"name": n, "file": f"tools/asset-pipeline/foley/archetypes/{n}.wav", "source_title": title, "author": author,
                      "freesound_page": url, "licence": LICENCE, "range_s": [t0, t1], "download": rel,
                      "cues": [k for k in CUES if k in reads[n]]} for n, rel, t0, t1, title, author, url in EXCERPTS],
    }
    with open(MANIFEST, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(manifest, indent=2) + "\n")
    print(f"SOURCES.md + manifest: {len(EXCERPTS)} excerpts from {len(recordings)} recordings")


# ── QA: the shape and the level, against the references ──────────────────────────────────────────

def tonality(y: np.ndarray) -> float:
    """How far the strongest line between 80 Hz and 4 kHz stands out of its own neighbourhood, dB: the power on a
    1/48-octave grid over the median of the grid within +-1/2 octave. Noise (air, grain) reads ~5-11 dB; PRESS's
    approved "air, not a tone" reads ~14; a held tone reads 20 and up."""
    S = np.abs(np.fft.rfft(y, 1 << 15)) ** 2
    f = np.fft.rfftfreq(1 << 15, 1 / SR)
    edges = 2 ** np.arange(np.log2(60.0), np.log2(5000.0), 1 / 48)
    idx = np.searchsorted(edges, f) - 1
    ok = (idx >= 0) & (idx < len(edges) - 1)
    grid = np.bincount(idx[ok], weights=S[ok], minlength=len(edges) - 1) / np.maximum(1, np.bincount(idx[ok], minlength=len(edges) - 1))
    centre = np.sqrt(edges[:-1] * edges[1:])
    best = 0.0
    for i in np.nonzero((centre >= 80) & (centre < 4000))[0]:
        nb = grid[max(0, i - 24):i + 25]
        best = max(best, 10 * np.log10(max(1e-30, grid[i]) / max(1e-30, float(np.median(nb)))))
    return best


def shape(y: np.ndarray) -> dict:
    """The length to -40 dB of the peak, the band split, the loudest 50 ms raw (dBFS), the true peak and the tonality."""
    k = int(0.002 * SR)
    e = np.convolve(np.abs(y), np.ones(k) / k, mode="same")
    last = np.nonzero(e >= e.max() * 10 ** (-40 / 20))[0]
    S = np.abs(np.fft.rfft(y, 1 << 15)) ** 2
    f = np.fft.rfftfreq(1 << 15, 1 / SR)
    tot = S.sum()
    shares = [float(S[(f >= a) & (f < b)].sum() / tot) for a, b in ((0, 150), (150, 1000), (1000, 4000), (4000, 30000))]
    return {"length_ms": round(float((last[-1] if len(last) else 0) / SR * 1000), 1), "bands": [round(s, 3) for s in shares],
            "max50_dbfs": round(20 * np.log10(max1(y, 0.05)), 2), "true_peak_dbfs": round(20 * np.log10(true_peak(y)), 2),
            "tonality_db": round(float(tonality(y)), 1)}


def measure() -> dict:
    """Every archetype as played (K-weighted loudest 50 ms at its mastered volume x the SFX master) against its
    ceilings, and the hierarchy test's EFFECTIVE level (the raw loudest 50 ms x the volume) beside it."""
    out = {"_generated_by": "tools/asset-pipeline/make_archetypes.py - do not edit",
           "method": "loudest 50 ms RMS, ITU-R BS.1770 K-weighted (make_press_tick.k_weight), of the file x its play volume "
                     "x the SFX master 0.8, dB; 'effective' = the raw loudest 50 ms RMS x the volume, dBFS (the hierarchy "
                     "test's figure)", "cues": {}}
    refs: dict[tuple[str, float], tuple[float, float]] = {}
    for key in CUES:
        y = read_wav(os.path.join(COMBAT, key + ".wav"))
        v = VOLUME[key]
        row = {"volume": v, "k50_db": round(k50(y, v), 2), "effective_dbfs": round(20 * np.log10(max1(y * v, 0.05)), 2),
               "sha256": sha256(os.path.join(COMBAT, key + ".wav")), **shape(y), "ceilings": [], "pass": True}
        if key in HITS:      # also at the T1 band's top, 0.38
            row["k50_db_at_0.38"] = round(k50(y, 0.38), 2)
        if key == "sfx_air_release":   # "an air push, no tone": no line stands out further than in PRESS's approved air
            press = tonality(read_wav(os.path.join(COMBAT, "sfx_seeker_press_tick.wav")))
            row["no_tone"] = {"tonality_db": row["tonality_db"], "press_tick_tonality_db": round(press, 1),
                              "pass": bool(row["tonality_db"] < press)}
            row["pass"] = row["pass"] and row["no_tone"]["pass"]
        for ref, rv, margin in CEILINGS[key]:
            if (ref, rv) not in refs:
                ry = read_wav(os.path.join(COMBAT, ref + ".wav"))
                refs[(ref, rv)] = (k50(ry, rv), 20 * np.log10(max1(ry * rv, 0.05)))
            rk, reff = refs[(ref, rv)]
            under = rk - row["k50_db"]
            ok = under >= margin and row["effective_dbfs"] < reff
            row["ceilings"].append({"ref": ref, "ref_volume": rv, "ref_k50_db": round(rk, 2), "ref_effective_dbfs": round(reff, 2),
                                    "under_db": round(under, 2), "required_db": margin, "pass": bool(ok)})
            row["pass"] = row["pass"] and bool(ok)
        out["cues"][key] = row
    out["all_pass"] = all(r["pass"] for r in out["cues"].values())
    return out


def table(m: dict) -> str:
    lines = ["# The archetype cues in the fight's mix (P1.5, the PRESS method)", "",
             m["method"] + ".", "",
             "| cue | what | volume | K50 dB | under its ceiling | effective dBFS | length ms | <150 / 150-1k / 1-4k / >4k | tonality dB | pass |",
             "|---|---|---|---|---|---|---|---|---|---|"]
    for key, r in m["cues"].items():
        ceil = "; ".join(f"{c['ref']} @{c['ref_volume']:.2f} ({c['ref_k50_db']:.1f}): {c['under_db']:+.1f} (>= {c['required_db']:.0f})"
                         for c in r["ceilings"])
        lines.append(f"| `{key}` | {CUES[key][1]} | {r['volume']:.2f} | {r['k50_db']:.1f} | {ceil} | {r['effective_dbfs']:.1f} | "
                     f"{r['length_ms']:.0f} | {' / '.join(f'{b:.2f}' for b in r['bands'])} | {r['tonality_db']:.1f} | "
                     f"{'yes' if r['pass'] else 'NO'} |")
    lines += ["", f"All under their ceilings: **{'yes' if m['all_pass'] else 'NO'}**."]
    return "\n".join(lines) + "\n"


def main() -> int:
    args = sys.argv[1:]
    if len(args) == 2 and args[0] == "--extract":
        extract(args[1])
        return 0
    for key, (build, what) in CUES.items():
        y = build()
        write_wav(os.path.join(COMBAT, key + ".wav"), y)
    write_sources(trace())
    m = measure()
    with open(LEVELS, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(m, indent=2) + "\n")
    for key, r in m["cues"].items():
        print(f"{key:18s} vol {r['volume']:.2f}  K50 {r['k50_db']:6.1f}  eff {r['effective_dbfs']:6.1f}  len {r['length_ms']:4.0f}  "
              f"TP {r['true_peak_dbfs']:5.1f}  tone {r['tonality_db']:4.1f}  bands {r['bands']}  "
              + "  ".join(f"{c['ref']} {c['under_db']:+.1f}" for c in r["ceilings"]) + ("" if r["pass"] else "  FAIL"))
    if len(args) == 2 and args[0] == "--evidence":
        os.makedirs(args[1], exist_ok=True)
        with open(os.path.join(args[1], "archetype_levels.md"), "w", encoding="utf-8", newline="\n") as f:
            f.write(table(m))
    return 0 if m["all_pass"] else 1


if __name__ == "__main__":
    sys.exit(main())
