#!/usr/bin/env python3
"""make_swing_cues.py -- the basic attacks' IDENTITY cue family (design.md section 7, Phase 1 / P1.6).

"Archetypes first, identity second": P1.5 gave every basic attack an ARCHETYPE (make_archetypes.py); this builds the ten
swing hits and the three releases the design lists under "Basic-attack hit family", each in its champion's OWN material,
so a champion's basic attack is known by ear. Every SwingRecipe's chain already names its key first
(`sfx_<champ>_swing_hit` -> archetype -> `sfx_hit`; `sfx_<champ>_loose|toss` -> `sfx_throw_release`), so the file's
existence is the whole switch. ONE build each, built from REAL recorded foley (CC0 1.0, Freesound; the licence checked on
each sound's own page, see foley/swing_hits/SOURCES.md); synthesis only for a small sub under the Anvil's thud.

  sfx_seeker_swing_hit     a short blade cut into leather, a faint steel edge
  sfx_anvil_swing_hit      a padded heavy fist (a boxing bag), a low thud
  sfx_metronome_swing_hit  a dry knuckle hit on wood, a sharp click
  sfx_tower_swing_hit      a mallet on packed earth, a short gravel tick
  sfx_thornwall_swing_hit  a wooden shield thump with a steel rim rattle
  sfx_magpie_swing_hit     a quick knife nick, a short cloth tear
  sfx_quiver_swing_hit     an arrow into hide + a wooden clack
  sfx_quiver_loose         a bowstring twang
  sfx_chorus_swing_hit     bone clatter on hide
  sfx_chorus_toss          a light toss whip
  sfx_unbroken_swing_hit   a stone chip on rock
  sfx_unbroken_toss        a short heavy toss
  sfx_oathbound_swing_hit  a chain whip crack + two link rattles

None of the recordings is one an archetype is made of: an identity cue earns its place by its material (the --evidence
table also gives each hit's 1/3-octave distance from its archetype). Every cue repeats on every beat, so each is SHORT
(a hit <= 0.22 s, a release 0.17 s) and dry: no reverb tail, a release to zero.

Each cue is MASTERED in the fight's own mix by the PRESS method (make_press_tick.k50: the loudest 50 ms, ITU-R BS.1770
K-weighted, of the file x its play volume x the SFX master 0.8), exactly as the archetypes were: a hit at the T1 0.36 sits
3.8 dB under SPRAY's contact (0.50) and further under HARD HANDS' hit (0.55); a release at its recipe's own volume (0.16 /
0.18) on the basic release's level, ~7 dB under SPRAY's release and under its own hit. The measurements are written
beside this script (swing_cue_levels.json) and, with --evidence <dir>, as a table for the QA evidence.

The files are SHIPPED (design.md section 7's lower pin level), never human-approved: swing_cue_test.cs pins each one's
SHA-256; a change is a deliberate commit, never a regeneration. The build is byte-reproducible (fixed seeds, the excerpts
committed). It imports the shared DSP from make_jaws_bite and the P1.5 helpers from make_archetypes (neither edited) and
changes no approved cue.

    PYTHONUTF8=1 python tools/asset-pipeline/make_swing_cues.py --extract <dir of the downloaded sources>   # once
    PYTHONUTF8=1 python tools/asset-pipeline/make_swing_cues.py [--evidence <dir>]                         # build + measure
"""
from __future__ import annotations

import json
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from make_jaws_bite import SR, band, norm, place, read_wav, sat_os, write_wav  # noqa: E402  (the shared DSP)
from make_archetypes import (at_hit, k50, master, sha256, shape, shaped, sub, tail)  # noqa: E402  (P1.5's helpers, read only)
from make_press_tick import max1  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
FOLEY = os.path.join(HERE, "foley", "swing_hits")
ARCH_MANIFEST = os.path.join(HERE, "foley", "archetypes", "archetype_manifest.json")
COMBAT = os.path.join(REPO, "assets", "audio", "combat")
LEVELS = os.path.join(HERE, "swing_cue_levels.json")
MANIFEST = os.path.join(FOLEY, "swing_manifest.json")
LICENCE = "CC0 1.0"
LICENCE_CHECKED = "2026-10-04"  # every source's own Freesound page links creativecommons.org/publicdomain/zero/1.0/
SEED = 20261005

# The excerpts: (name, downloaded file, start s, end s, title, author, url). Every source is CC0 1.0.
FS = "https://freesound.org/people/{}/sounds/{}/"
EXCERPTS = [
    # SEEKER: a short blade cut into leather, a faint steel edge
    ("blade_slice", "seeker/fs637641_knife_slice_cut_saw_flesh_butc_kyles.wav", 1.050, 1.180, "knife slice cut saw flesh butcher.flac", "kyles", FS.format("kyles", 637641)),
    ("leather_thud", "seeker/fs543698_leather_impacts_wav_by_200221_200221_WeanBekker.wav", 3.355, 3.480, "Leather impacts .wav", "200221-WeanBekker", FS.format("200221-WeanBekker", 543698)),
    ("belt_snap", "seeker/fs621939_belt_snap_by_clothespeg_clothespeg.wav", 0.050, 0.150, "belt snap", "clothespeg", FS.format("clothespeg", 621939)),
    ("steel_edge", "seeker/fs577619_sword_drawing_1_wav_by_paulfab_paulfabb.wav", 0.060, 0.240, "Sword Drawing 1.wav", "paulfabb", FS.format("paulfabb", 577619)),
    # ANVIL: a padded heavy fist (a boxing bag), a low thud
    ("heavy_bag", "anvil/fs813405_punch_on_a_heavy_bag_by_luisa_Luisa_Sanchez.wav", 1.870, 2.090, "Punch on a heavy bag", "Luisa_Sanchez", FS.format("Luisa_Sanchez", 813405)),
    ("low_thud", "anvil/fs434781_luggage_drop_1_wav_by_stephenb_stephenbist.wav", 0.138, 0.300, "Luggage Drop 1.wav", "stephenbist", FS.format("stephenbist", 434781)),
    # METRONOME: a dry knuckle hit on wood, a sharp click
    ("knuckle_wood", "metronome/fs812363_knocking_once_on_wood_single_f_CuboRodante.wav", 0.366, 0.460, "Knocking once on wood, single footstep", "CuboRodante", FS.format("CuboRodante", 812363)),
    ("wood_knock", "metronome/fs369710_wood_knock_by_mrguff_Mrguff.wav", 0.002, 0.110, "Wood Knock", "Mrguff", FS.format("Mrguff", 369710)),
    ("block_click", "metronome/fs218460_wood_block_hit_by_thomasjaunis_thomasjaunism.wav", 0.000, 0.070, "Wood block hit", "thomasjaunism", FS.format("thomasjaunism", 218460)),
    # TOWER: a mallet on packed earth, a short gravel tick
    ("ground_thud", "tower/fs640204_heavy_metal_thud_on_ground_by_7of9Designs.wav", 0.178, 0.262, "Heavy Metal Thud on Ground", "7of9Designs", FS.format("7of9Designs", 640204)),
    ("dirt_hit", "tower/fs319222_single_rock_hit_dirt_wav_by_wo_worthahep88.wav", 0.277, 0.400, "Single Rock hit Dirt.wav", "worthahep88", FS.format("worthahep88", 319222)),
    ("gravel_tick", "tower/fs521588_hiking_boot_footsteps_on_grave_Fission9.wav", 2.372, 2.420, "Hiking Boot Footsteps on Gravel", "Fission9", FS.format("Fission9", 521588)),
    # THORNWALL: a wooden shield thump with a steel rim rattle
    ("shield_thump", "thornwall/fs370203_shield_guard_by_nekoninja_nekoninja.wav", 0.000, 0.090, "shield guard", "nekoninja", FS.format("nekoninja", 370203)),
    ("wood_face", "thornwall/fs319227_single_rock_hitting_wood_4_wav_worthahep88.wav", 0.060, 0.140, "Single Rock hitting wood 4.wav", "worthahep88", FS.format("worthahep88", 319227)),
    ("rim_rattle", "thornwall/fs827502_metal_rattle_jingle_recording_heidicolorado.wav", 0.110, 0.260, "Metal Rattle/Jingle recording", "heidicolorado", FS.format("heidicolorado", 827502)),
    # MAGPIE: a quick knife nick, a short cloth tear
    ("knife_nick", "magpie/fs344404_knife_stab_melon_wav_by_jawbut_jawbutch.wav", 0.350, 0.420, "Knife Stab Melon.wav", "jawbutch", FS.format("jawbutch", 344404)),
    ("stab_pull", "magpie/fs411742_knife_stab_pull_wav_by_neilshe_neilsher.wav", 0.376, 0.450, "Knife Stab Pull.wav", "neilsher", FS.format("neilsher", 411742)),
    ("cloth_tear", "magpie/fs764888_clothrip_rip_quick_jvz_owsfx_b_Joshua_van_Zyl.wav", 2.500, 2.640, "CLOTHRip-Rip quick_JvZ_OwSFX", "Joshua_van_Zyl", FS.format("Joshua_van_Zyl", 764888)),
    # QUIVER: an arrow into hide + a wooden clack / a bowstring twang
    ("arrow_thwack", "quiver/fs179996_arrow_release_and_hit_wav_by_c_calvarychurchatlanta.wav", 0.444, 0.560, "Arrow Release and Hit.wav", "calvarychurchatlanta", FS.format("calvarychurchatlanta", 179996)),
    ("arrow_shaft", "quiver/fs521552_arrow_impact_by_omerbhatti34_omerbhatti34.wav", 0.020, 0.140, "Arrow Impact", "omerbhatti34", FS.format("omerbhatti34", 521552)),
    ("wood_clack", "quiver/fs822567_small_wood_piece_sound_by_qubo_qubodup.wav", 0.000, 0.070, "Small Wood Piece Sound", "qubodup", FS.format("qubodup", 822567)),
    ("bow_thrum", "quiver/fs542559_owi_bow_string_thwang_wav_by_m_matthewHoldenSound.wav", 0.438, 0.640, "OWI_Bow string thwang.wav", "matthewHoldenSound", FS.format("matthewHoldenSound", 542559)),
    ("string_snap", "quiver/fs179996_arrow_release_and_hit_wav_by_c_calvarychurchatlanta.wav", 0.253, 0.330, "Arrow Release and Hit.wav", "calvarychurchatlanta", FS.format("calvarychurchatlanta", 179996)),
    # CHORUS: bone clatter on hide / a light toss whip
    ("bone_a", "chorus/fs202102_rattling_bones_wav_by_spookymo_spookymodem.wav", 2.556, 2.620, "Rattling Bones.wav", "spookymodem", FS.format("spookymodem", 202102)),
    ("bone_b", "chorus/fs202102_rattling_bones_wav_by_spookymo_spookymodem.wav", 4.158, 4.220, "Rattling Bones.wav", "spookymodem", FS.format("spookymodem", 202102)),
    ("bone_c", "chorus/fs473526_bones_mp3_by_kneeling_Kneeling.wav", 2.701, 2.760, "bones.mp3", "Kneeling", FS.format("Kneeling", 473526)),
    ("hide_skin", "chorus/fs509528_alfaia_hit_by_sassaby_Sassaby.wav", 0.000, 0.120, "Alfaia Hit", "Sassaby", FS.format("Sassaby", 509528)),
    ("toss_whip", "chorus/fs346373_throwing_whip_effect_by_denao2_denao270.wav", 0.000, 0.300, "Throwing / Whip Effect", "denao270", FS.format("denao270", 346373)),
    ("toss_air", "chorus/fs423799_little_whoosh_2_by_ch_ase_ch_ase.wav", 0.960, 1.120, "Little Whoosh 2", "ch_ase", FS.format("ch_ase", 423799)),
    # UNBROKEN: a stone chip on rock / a short heavy toss
    ("rock_knock", "unbroken/fs350750_stonehit1_wav_by_aerror_aerror.wav", 0.902, 0.980, "stonehit1.wav", "aerror", FS.format("aerror", 350750)),
    ("stone_chip", "unbroken/fs414853_rock_hit_mp3_by_link_boy_Link_Boy.wav", 0.014, 0.090, "rock hit.mp3", "Link-Boy", FS.format("Link-Boy", 414853)),
    ("pebble_scatter", "unbroken/fs843547_rock_impact_galets_outdoors_26_kevinklang.wav", 0.187, 0.330, "ROCK_IMPACT_GALETS_OUTDOORS_260128 48", "kevinklang", FS.format("kevinklang", 843547)),
    ("heavy_swing", "unbroken/fs523230_hard_swing_1_by_magnuswaker_magnuswaker.wav", 0.040, 0.330, "Hard Swing 1", "magnuswaker", FS.format("magnuswaker", 523230)),
    ("whoosh_low", "unbroken/fs684459_whoosh_wav_by_gbnelso_gbnelso.wav", 0.700, 0.960, "Whoosh.wav", "gbnelso", FS.format("gbnelso", 684459)),
    # OATHBOUND: a chain whip crack + two link rattles
    ("chain_crack", "oathbound/fs791469_real_steel_chain_whip_multiple_modusmogulus.wav", 6.114, 6.200, "Real Steel Chain-whip  (Multiple cracks)", "modusmogulus", FS.format("modusmogulus", 791469)),
    ("link_a", "oathbound/fs347820_chain_rustle_by_mafon2_Mafon2.wav", 0.129, 0.180, "Chain rustle", "Mafon2", FS.format("Mafon2", 347820)),
    ("link_b", "oathbound/fs347820_chain_rustle_by_mafon2_Mafon2.wav", 3.450, 3.500, "Chain rustle", "Mafon2", FS.format("Mafon2", 347820)),
]


def extract(src_dir: str) -> None:
    """Cut each excerpt (DC removed, 2 ms fade in, 8 ms fade out, normalised to 0.9) into foley/swing_hits/."""
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


# ── The cues (T = the contact / release moment in the file) ──────────────────────────────────────

T_HIT = 0.004                   # a hit lands at once: the swing plays it on the contact frame
HIT_LEN = 0.22                  # a hit's file length (s): it repeats every beat, so it is short and dry
REL_LEN = 0.17                  # a release's file length (s): the task's 0.16-0.18 s


def _hit(key: str, low: np.ndarray, top: np.ndarray, top_share: float, hi: float = 15000.0) -> np.ndarray:
    return master(low, top, top_share, HIT_LEN, VOLUME[key], TARGET_K[key], hi=hi)


def dense(x: np.ndarray, drive: float = 3.0) -> np.ndarray:
    """A very sharp knock given body: an oversampled tanh (make_jaws_bite.sat_os) lowers its crest factor, so the true-peak
    cap does not leave it quieter than its family."""
    return sat_os(norm(x), drive)


def _bufs(length: float) -> tuple[np.ndarray, np.ndarray]:
    n = int(length * SR)
    return np.zeros(n), np.zeros(n)


def seeker() -> np.ndarray:
    """A SHORT BLADE CUT INTO LEATHER: the slice of a knife through flesh on a leather thud, a belt's leather snap under
    it, a faint steel edge drawn on top."""
    low, top = _bufs(HIT_LEN)
    T = T_HIT
    at_hit(low, T, tail(band(src("blade_slice"), 1200, 12000), 0.030, 0.020), 1.0)
    at_hit(low, T + 0.001, tail(band(src("leather_thud"), 90, 1500), 0.025, 0.020), 0.70)
    at_hit(low, T + 0.003, tail(band(src("belt_snap"), 600, 9000), 0.020, 0.015), 0.45)
    at_hit(top, T + 0.002, shaped(band(src("steel_edge"), 3000, 14000), 0.004, 0.030), 1.0)
    return _hit("sfx_seeker_swing_hit", low, top, 0.22)


def anvil() -> np.ndarray:
    """A PADDED HEAVY FIST: a boxing bag's heavy give, a low thud of a dropped weight under it and a small sub; no snap,
    no crack (the fist archetype has a knuckle snap; the Anvil does not)."""
    rng = np.random.default_rng(SEED + 1)
    low, top = _bufs(HIT_LEN)
    T = T_HIT
    at_hit(low, T, tail(band(src("heavy_bag"), 45, 2500), 0.055, 0.040), 1.0)
    at_hit(low, T + 0.002, tail(band(src("low_thud"), 50, 1800), 0.040, 0.030), 0.75)
    place(low, T - 0.002, sub(0.11, 78, 46, 0.004, 0.045, rng), 0.16)
    return _hit("sfx_anvil_swing_hit", low, top, 0.0, hi=6000.0)


def metronome() -> np.ndarray:
    """A DRY KNUCKLE HIT ON WOOD, A SHARP CLICK: a single knock and a wood knock land together, a wood block's click on
    top, all inside 60 ms."""
    low, top = _bufs(HIT_LEN)
    T = T_HIT
    at_hit(low, T, tail(dense(band(src("knuckle_wood"), 150, 8000)), 0.030, 0.025), 1.0)
    at_hit(low, T + 0.001, tail(band(src("wood_knock"), 200, 6000), 0.035, 0.030), 0.80)
    at_hit(top, T, tail(band(src("block_click"), 1500, 14000), 0.006, 0.008), 1.0)
    return _hit("sfx_metronome_swing_hit", low, top, 0.55)


def tower() -> np.ndarray:
    """A MALLET ON PACKED EARTH, A SHORT GRAVEL TICK: a heavy thud on the ground and a rock into dirt, a gravel tick
    30 ms after."""
    low, top = _bufs(HIT_LEN)
    T = T_HIT
    at_hit(low, T, tail(band(src("ground_thud"), 40, 2500), 0.035, 0.030), 1.0)
    at_hit(low, T + 0.001, tail(band(src("dirt_hit"), 80, 5000), 0.025, 0.020), 0.80)
    at_hit(top, T + 0.030, tail(band(src("gravel_tick"), 800, 10000), 0.010, 0.010), 1.0)
    return _hit("sfx_tower_swing_hit", low, top, 0.40)


def thornwall() -> np.ndarray:
    """A WOODEN SHIELD THUMP WITH A STEEL RIM RATTLE: a shield's thump, a wooden face struck on it, the rim's steel
    rattling 10 ms after, short."""
    low, top = _bufs(HIT_LEN)
    T = T_HIT
    at_hit(low, T, tail(band(src("shield_thump"), 60, 4000), 0.035, 0.030), 1.0)
    at_hit(low, T + 0.001, tail(band(src("wood_face"), 300, 8000), 0.015, 0.012), 0.55)
    at_hit(top, T + 0.010, tail(band(src("rim_rattle"), 1200, 12000), 0.050, 0.035), 1.0)
    return _hit("sfx_thornwall_swing_hit", low, top, 0.40)


def magpie() -> np.ndarray:
    """A QUICK KNIFE NICK, A SHORT CLOTH TEAR: the tip of a knife in and out, a short tear of cloth on it (60 ms)."""
    low, top = _bufs(HIT_LEN)
    T = T_HIT
    at_hit(low, T, tail(band(src("stab_pull"), 300, 10000), 0.015, 0.012), 1.0)
    at_hit(low, T + 0.001, tail(band(src("knife_nick"), 1500, 14000), 0.012, 0.010), 0.60)
    place(top, T + 0.004, shaped(band(src("cloth_tear"), 800, 12000), 0.006, 0.030), 1.0)
    return _hit("sfx_magpie_swing_hit", low, top, 0.55)


def quiver_hit() -> np.ndarray:
    """AN ARROW INTO HIDE + A WOODEN CLACK: the arrow's thwack into a target and its shaft's knock, a small wooden clack
    10 ms after (the nock and the shaft)."""
    low, top = _bufs(HIT_LEN)
    T = T_HIT
    at_hit(low, T, tail(band(src("arrow_thwack"), 120, 9000), 0.025, 0.020), 1.0)
    at_hit(low, T + 0.001, tail(band(src("arrow_shaft"), 100, 6000), 0.020, 0.018), 0.55)
    at_hit(top, T + 0.010, tail(band(src("wood_clack"), 1000, 12000), 0.015, 0.012), 1.0)
    return _hit("sfx_quiver_swing_hit", low, top, 0.45)


def quiver_loose() -> np.ndarray:
    """A BOWSTRING TWANG: the string's low thrum released on T, its snap on top; no arrow flight (the arrow is seen)."""
    low, top = _bufs(REL_LEN)
    T = 0.006
    at_hit(low, T, tail(band(src("bow_thrum"), 60, 3000), 0.050, 0.040), 1.0)
    at_hit(top, T, tail(band(src("string_snap"), 1500, 12000), 0.020, 0.015), 1.0)
    return master(low, top, 0.40, REL_LEN, VOLUME["sfx_quiver_loose"], TARGET_K["sfx_quiver_loose"])


def chorus_hit() -> np.ndarray:
    """BONE CLATTER ON HIDE: a drum skin's slap (hide) under three bones knocking 0 / 16 / 38 ms apart."""
    low, top = _bufs(HIT_LEN)
    T = T_HIT
    at_hit(low, T, tail(band(src("hide_skin"), 80, 5000), 0.030, 0.025), 1.0)
    at_hit(top, T, tail(band(src("bone_a"), 600, 12000), 0.020, 0.015), 1.0)
    at_hit(top, T + 0.016, tail(band(src("bone_b"), 600, 12000), 0.015, 0.012), 0.70)
    at_hit(top, T + 0.038, tail(band(src("bone_c"), 600, 12000), 0.012, 0.010), 0.45)
    return _hit("sfx_chorus_swing_hit", low, top, 0.85)


def chorus_toss() -> np.ndarray:
    """A LIGHT TOSS WHIP: a small throwing whip with a little air around it, no weight."""
    low, top = _bufs(REL_LEN)
    T = 0.045
    at_hit(low, T, shaped(band(src("toss_whip"), 250, 10000), 0.020, 0.030), 1.0)
    at_hit(top, T - 0.004, shaped(band(src("toss_air"), 800, 10000), 0.015, 0.030), 1.0)
    return master(low, top, 0.35, REL_LEN, VOLUME["sfx_chorus_toss"], TARGET_K["sfx_chorus_toss"])


def unbroken_hit() -> np.ndarray:
    """A STONE CHIP ON ROCK: a dry rock knock and a chip's crack on it, a few pebbles scattering 15 ms after."""
    low, top = _bufs(HIT_LEN)
    T = T_HIT
    at_hit(low, T, tail(dense(band(src("rock_knock"), 150, 9000)), 0.030, 0.030), 1.0)
    at_hit(top, T, tail(band(src("stone_chip"), 1500, 14000), 0.015, 0.012), 1.0)
    at_hit(top, T + 0.015, tail(band(src("pebble_scatter"), 1200, 12000), 0.040, 0.030), 0.60)
    return _hit("sfx_unbroken_swing_hit", low, top, 0.60)


def unbroken_toss() -> np.ndarray:
    """A SHORT HEAVY TOSS: a hard swing's low push and a dark whoosh, nothing bright (a stone, not a cloth)."""
    low, top = _bufs(REL_LEN)
    T = 0.045
    at_hit(low, T, tail(shaped(band(src("heavy_swing"), 60, 2000), 0.020, 0.040), 0.060, 0.022), 1.0)
    at_hit(low, T, tail(shaped(band(src("whoosh_low"), 120, 3500), 0.020, 0.035), 0.060, 0.022), 0.80)
    return master(low, top, 0.0, REL_LEN, VOLUME["sfx_unbroken_toss"], TARGET_K["sfx_unbroken_toss"], hi=5000.0)


def oathbound() -> np.ndarray:
    """A CHAIN WHIP CRACK + TWO LINK RATTLES: a steel chain's crack on T, two links rattling 30 and 70 ms after."""
    low, top = _bufs(HIT_LEN)
    T = T_HIT
    at_hit(low, T, tail(band(src("chain_crack"), 200, 12000), 0.020, 0.015), 1.0)
    at_hit(top, T + 0.030, tail(band(src("link_a"), 2000, 12000), 0.020, 0.015), 1.0)
    at_hit(top, T + 0.070, tail(band(src("link_b"), 2000, 12000), 0.015, 0.012), 0.70)
    return _hit("sfx_oathbound_swing_hit", low, top, 0.40)


# key -> (build, what it is, the archetype it replaces first in its chain, is a release). The design's order.
CUES = {
    "sfx_seeker_swing_hit": (seeker, "a short blade cut into leather, a faint steel edge", "sfx_blade_hit", False),
    "sfx_anvil_swing_hit": (anvil, "a padded heavy fist (a boxing bag), a low thud", "sfx_fist_hit", False),
    "sfx_metronome_swing_hit": (metronome, "a dry knuckle hit on wood, a sharp click", "sfx_fist_hit", False),
    "sfx_tower_swing_hit": (tower, "a mallet on packed earth, a short gravel tick", "sfx_stone_hit", False),
    "sfx_thornwall_swing_hit": (thornwall, "a wooden shield thump with a steel rim rattle", "sfx_wood_hit", False),
    "sfx_magpie_swing_hit": (magpie, "a quick knife nick, a short cloth tear", "sfx_blade_hit", False),
    "sfx_quiver_swing_hit": (quiver_hit, "an arrow into hide + a wooden clack", "sfx_blade_hit", False),
    "sfx_quiver_loose": (quiver_loose, "a bowstring twang", "sfx_throw_release", True),
    "sfx_chorus_swing_hit": (chorus_hit, "bone clatter on hide", "sfx_wood_hit", False),
    "sfx_chorus_toss": (chorus_toss, "a light toss whip", "sfx_throw_release", True),
    "sfx_unbroken_swing_hit": (unbroken_hit, "a stone chip on rock", "sfx_stone_hit", False),
    "sfx_unbroken_toss": (unbroken_toss, "a short heavy toss", "sfx_throw_release", True),
    "sfx_oathbound_swing_hit": (oathbound, "a chain whip crack + two link rattles", "sfx_fist_hit", False),
}
# The hit each release must stay under (its own recipe's contact).
OWN_HIT = {"sfx_quiver_loose": "sfx_quiver_swing_hit", "sfx_chorus_toss": "sfx_chorus_swing_hit",
           "sfx_unbroken_toss": "sfx_unbroken_swing_hit"}

# The volume each is played at (SwingRecipe: every contact 0.36; the releases 0.18 / 0.16 / 0.16) and MASTERED at.
VOLUME = {k: 0.36 for k in CUES}
VOLUME.update({"sfx_quiver_loose": 0.18, "sfx_chorus_toss": 0.16, "sfx_unbroken_toss": 0.16})
# The K-weighted loudest 50 ms each is mastered TO (dB, as played): the archetypes' own levels, so swapping an archetype
# for an identity cue moves the mix by nothing: every hit 3.8 dB under SPRAY's contact, every release on the basic
# release's -37.0 (~7 dB under SPRAY's release). A true-peak-limited file sits lower, never higher.
TARGET_K = {k: -31.3 for k in CUES}
TARGET_K.update({"sfx_quiver_loose": -37.0, "sfx_chorus_toss": -37.0, "sfx_unbroken_toss": -37.0})

HIT_CEILINGS = [("sfx_seeker_spray_hit", 0.50, 3.0), ("sfx_seeker_hard_hands_hit", 0.55, 3.0)]
RELEASE_CEILINGS = [("sfx_seeker_spray_release", 0.40, 3.0)]
MAX_HIT_S, MIN_REL_S, MAX_REL_S = 0.25, 0.16, 0.18
MAX_TAIL_DB = -30.0             # the last 30 ms against the loudest 50 ms: no reverb tail
MIN_DISTANCE_DB = 3.0           # an identity hit's 1/3-octave distance from its archetype: it earns its place by its material


def trace() -> dict[str, list[str]]:
    """Which excerpts each cue's build READS (excerpt -> the cue keys), read-only: nothing is written."""
    global _READS
    out: dict[str, list[str]] = {}
    try:
        for key, (build, *_) in CUES.items():
            _READS = set()
            build()
            for name in _READS:
                out.setdefault(name, []).append(key)
    finally:
        _READS = None
    return out


def write_sources(reads: dict[str, list[str]]) -> None:
    """SOURCES.md and the manifest: which recordings produced which shipped swing cue, under what licence."""
    unused = [e[0] for e in EXCERPTS if e[0] not in reads]
    if unused:
        raise SystemExit(f"excerpts read by no cue (delete their files and EXCERPTS entries): {unused}")
    with open(ARCH_MANIFEST, encoding="utf-8") as f:
        archetype_pages = {e["freesound_page"] for e in json.load(f)["excerpts"]}
    shared = sorted({url for *_, url in EXCERPTS} & archetype_pages)
    if shared:
        raise SystemExit(f"recordings an archetype is made of (an identity cue must differ in material): {shared}")
    recordings: dict[str, tuple[str, str, list[str]]] = {}
    for name, _, _, _, title, author, url in EXCERPTS:
        recordings.setdefault(url, (title, author, []))[2].append(name)
    lines = [
        "# Swing identity cue foley sources", "",
        "Generated by `make_swing_cues.py`; do not edit by hand. It answers one question: **which source recordings "
        "produced the thirteen SHIPPED basic-attack identity cues (design.md section 7, \"Basic-attack hit family\"), and "
        "under what licence.**", "",
        f"- **Shipped:** {', '.join(f'`{k}`' for k in CUES)} in `assets/audio/combat/`, pinned by SHA-256 as SHIPPED "
        "(`swing_manifest.json`, `tests/unit/IdleXIdle.Game.Tests/swing_cue_test.cs`).",
        f"- **Licence:** every source below is **{LICENCE}** (https://creativecommons.org/publicdomain/zero/1.0/): public "
        "domain dedication, no attribution required, commercial use allowed. Credited anyway, with thanks.",
        f"- **Licence check:** {LICENCE_CHECKED}, on each sound's own Freesound page (the page's licence link is "
        "creativecommons.org/publicdomain/zero/1.0/).",
        "- **Processing:** downloaded as Freesound's preview into `build/tmp` (never committed), converted to mono 16-bit "
        "44.1 kHz; each excerpt is the range below, DC removed, a 2 ms fade in, an 8 ms fade out, normalised to 0.9. Only "
        "a small sub under the Anvil's thud is synthesised.",
        "- **Material:** no recording here is one an archetype (`foley/archetypes/`) is made of.", "",
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
        "_generated_by": "tools/asset-pipeline/make_swing_cues.py - do not edit",
        "pin_level": "SHIPPED (design.md section 7: never human-approved before the owner has heard the film)",
        "licence": {"name": LICENCE, "url": "https://creativecommons.org/publicdomain/zero/1.0/", "checked": LICENCE_CHECKED,
                    "how": "each source's own Freesound page links the CC0 1.0 deed"},
        "cues": {k: {"what": CUES[k][1], "file": f"assets/audio/combat/{k}.wav", "sha256": sha256(os.path.join(COMBAT, k + ".wav")),
                     "archetype": CUES[k][2], "release": CUES[k][3], "mastered_volume": VOLUME[k], "target_k50_db": TARGET_K[k],
                     "excerpts": sorted(n for n in reads if k in reads[n])} for k in CUES},
        "excerpts": [{"name": n, "file": f"tools/asset-pipeline/foley/swing_hits/{n}.wav", "source_title": title, "author": author,
                      "freesound_page": url, "licence": LICENCE, "range_s": [t0, t1], "download": rel,
                      "cues": [k for k in CUES if k in reads[n]]} for n, rel, t0, t1, title, author, url in EXCERPTS],
    }
    with open(MANIFEST, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(manifest, indent=2) + "\n")
    print(f"SOURCES.md + manifest: {len(EXCERPTS)} excerpts from {len(recordings)} recordings")


# ── QA: the shape, the level and the material, against the references and the archetypes ────────

def third_octaves(y: np.ndarray) -> np.ndarray:
    """The level (dB) of each 1/3 octave from 100 Hz to 12.5 kHz, normalised to the cue's own mean."""
    S = np.abs(np.fft.rfft(y, 1 << 15)) ** 2
    f = np.fft.rfftfreq(1 << 15, 1 / SR)
    centres = 1000.0 * 2 ** (np.arange(-10, 12) / 3)
    lv = np.array([10 * np.log10(max(1e-30, S[(f >= c * 2 ** -(1 / 6)) & (f < c * 2 ** (1 / 6))].sum())) for c in centres])
    return lv - lv.mean()


def distance(a: np.ndarray, b: np.ndarray) -> float:
    """The RMS difference of two cues' 1/3-octave profiles, dB: how far apart their materials sit."""
    return float(np.sqrt(np.mean((third_octaves(a) - third_octaves(b)) ** 2)))


def tail_db(y: np.ndarray) -> float:
    """The last 30 ms RMS against the loudest 50 ms RMS, dB: a reverb tail reads high."""
    return float(20 * np.log10(max(1e-12, np.sqrt(np.mean(y[-int(0.03 * SR):] ** 2))) / max(1e-12, max1(y, 0.05))))


def measure() -> dict:
    """Every identity cue as played (K-weighted loudest 50 ms at its volume x the SFX master) against its ceilings, its
    length, its tail, a release against its own hit, a hit's distance from its archetype."""
    out = {"_generated_by": "tools/asset-pipeline/make_swing_cues.py - do not edit",
           "method": "loudest 50 ms RMS, ITU-R BS.1770 K-weighted (make_press_tick.k_weight), of the file x its play volume "
                     "x the SFX master 0.8, dB; 'effective' = the raw loudest 50 ms RMS x the volume, dBFS (the hierarchy "
                     "test's figure); 'distance' = the RMS difference of the 1/3-octave profiles (100 Hz-12.5 kHz) from the "
                     "archetype the cue replaces, dB", "cues": {}}
    refs: dict[tuple[str, float], tuple[float, float]] = {}
    files = {k: read_wav(os.path.join(COMBAT, k + ".wav")) for k in CUES}
    for key, (_, what, arch, is_release) in CUES.items():
        y, v = files[key], VOLUME[key]
        dur = len(y) / SR
        row = {"volume": v, "k50_db": round(k50(y, v), 2), "effective_dbfs": round(20 * np.log10(max1(y * v, 0.05)), 2),
               "sha256": sha256(os.path.join(COMBAT, key + ".wav")), "duration_s": round(dur, 4),
               "tail_db": round(tail_db(y), 1), **shape(y), "archetype": arch,
               "distance_db": round(distance(y, read_wav(os.path.join(COMBAT, arch + ".wav"))), 1),
               "ceilings": [], "pass": True}
        ok_len = (MIN_REL_S <= dur <= MAX_REL_S) if is_release else dur <= MAX_HIT_S
        row["pass"] = ok_len and row["tail_db"] <= MAX_TAIL_DB
        if not is_release:
            row["pass"] = row["pass"] and row["distance_db"] >= MIN_DISTANCE_DB
            row["k50_db_at_0.38"] = round(k50(y, 0.38), 2)
        else:
            hit = OWN_HIT[key]
            hk, he = k50(files[hit], VOLUME[hit]), 20 * np.log10(max1(files[hit] * VOLUME[hit], 0.05))
            row["own_hit"] = {"key": hit, "k50_db": round(hk, 2), "under_db": round(hk - row["k50_db"], 2),
                              "pass": bool(row["k50_db"] < hk and row["effective_dbfs"] < he)}
            row["pass"] = row["pass"] and row["own_hit"]["pass"]
        for ref, rv, margin in (RELEASE_CEILINGS if is_release else HIT_CEILINGS):
            if (ref, rv) not in refs:
                ry = read_wav(os.path.join(COMBAT, ref + ".wav"))
                refs[(ref, rv)] = (k50(ry, rv), 20 * np.log10(max1(ry * rv, 0.05)))
            rk, reff = refs[(ref, rv)]
            under = rk - row["k50_db"]
            ok = under >= margin and row["effective_dbfs"] < reff
            row["ceilings"].append({"ref": ref, "ref_volume": rv, "ref_k50_db": round(rk, 2), "ref_effective_dbfs": round(reff, 2),
                                    "under_db": round(under, 2), "required_db": margin, "pass": bool(ok)})
            row["pass"] = row["pass"] and bool(ok)
        row["pass"] = bool(row["pass"])
        out["cues"][key] = row
    out["all_pass"] = all(r["pass"] for r in out["cues"].values())
    return out


def table(m: dict) -> str:
    lines = ["# The basic attacks' identity cues in the fight's mix (P1.6, the PRESS method)", "",
             m["method"] + ".", "",
             "| cue | what | volume | K50 dB | under the reference contact / release | under its own hit | effective dBFS | "
             "length ms (file) | tail dB | <150 / 150-1k / 1-4k / >4k | from its archetype dB | pass |",
             "|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for key, r in m["cues"].items():
        ceil = "; ".join(f"{c['ref']} @{c['ref_volume']:.2f} ({c['ref_k50_db']:.1f}): {c['under_db']:+.1f} (>= {c['required_db']:.0f})"
                         for c in r["ceilings"])
        own = f"{r['own_hit']['key']}: {r['own_hit']['under_db']:+.1f}" if "own_hit" in r else "-"
        lines.append(f"| `{key}` | {CUES[key][1]} | {r['volume']:.2f} | {r['k50_db']:.1f} | {ceil} | {own} | "
                     f"{r['effective_dbfs']:.1f} | {r['length_ms']:.0f} ({r['duration_s'] * 1000:.0f}) | {r['tail_db']:.0f} | "
                     f"{' / '.join(f'{b:.2f}' for b in r['bands'])} | `{r['archetype']}` {r['distance_db']:.1f} | "
                     f"{'yes' if r['pass'] else 'NO'} |")
    lines += ["", f"All pass: **{'yes' if m['all_pass'] else 'NO'}**."]
    return "\n".join(lines) + "\n"


def main() -> int:
    args = sys.argv[1:]
    if len(args) == 2 and args[0] == "--extract":
        extract(args[1])
        return 0
    for key, (build, *_) in CUES.items():
        write_wav(os.path.join(COMBAT, key + ".wav"), build())
    write_sources(trace())
    m = measure()
    with open(LEVELS, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(m, indent=2) + "\n")
    for key, r in m["cues"].items():
        print(f"{key:24s} vol {r['volume']:.2f}  K50 {r['k50_db']:6.1f}  eff {r['effective_dbfs']:6.1f}  len {r['length_ms']:4.0f}  "
              f"tail {r['tail_db']:5.0f}  TP {r['true_peak_dbfs']:5.1f}  dist {r['distance_db']:4.1f}  bands {r['bands']}  "
              + "  ".join(f"{c['ref']} {c['under_db']:+.1f}" for c in r["ceilings"])
              + (f"  own {r['own_hit']['under_db']:+.1f}" if "own_hit" in r else "") + ("" if r["pass"] else "  FAIL"))
    if len(args) == 2 and args[0] == "--evidence":
        os.makedirs(args[1], exist_ok=True)
        with open(os.path.join(args[1], "swing_cue_levels.md"), "w", encoding="utf-8", newline="\n") as f:
            f.write(table(m))
    return 0 if m["all_pass"] else 1


if __name__ == "__main__":
    sys.exit(main())
