#!/usr/bin/env python3
"""reset3_evidence.py -- the bite reset's THIRD PASS review package (ADR-012, 2026-09-27): the whelp's attack on an
extended art canvas with genuinely redrawn poses (root motion off, then 15 / 20 / 25 %), the directional asymmetric
snap motif candidates N1 / N2 / N3 and the selected N2 at true speed, the full routine hit, and JAWS A2 as the
approved design direction drawn in the selected motif's shape family. From foundation_films.sh's takes in
build/shots/foundation/ (THIRD PASS), the second pass's in build/shots/foundation_puppet/ (the PUPPET strip, M4), and
the animatics in build/shots/reset3/a2_*/ (new) and build/shots/reset2/a2_*/ (the old M4-derived A2).

    PYTHONUTF8=1 python tools/asset-pipeline/reset3_evidence.py <out dir> [<blind dir>]
"""
import os
import shutil
import sys

from foundation_evidence import REPO, ARENA, GRID, CLOSE_WHELP, CLOSE_SEEKER, film, grid, sheet, flipbook, run, TL

PUP = os.path.join(REPO, "build", "shots", "foundation_puppet")
NEW = os.path.join(REPO, "build", "shots", "foundation")
A2 = os.path.join(REPO, "build", "shots", "reset3")
A2OLD = os.path.join(REPO, "build", "shots", "reset2")
BITE = os.path.join(REPO, "build", "shots", "bite")
V2 = os.path.join(REPO, "tools", "asset-pipeline", "v2", "keypose_sources")
CONTACT = "480,500,360,300"
PACK = "760,520,620,380"         # the front of the pack, wide enough for the extended poses
WIDE_WHELP = (760, 560, 1300, 900)


def main():
    out = os.path.abspath(sys.argv[1])
    blind = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else None
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png", ".md")) and f[0].isdigit():
            os.remove(os.path.join(out, f))
    p = lambda n: os.path.join(PUP, n)      # noqa: E731
    n = lambda k: os.path.join(NEW, k)      # noqa: E731
    a2 = lambda k: os.path.join(A2, "a2_" + k, k)        # noqa: E731
    a2old = lambda k: os.path.join(A2OLD, "a2_" + k, k)  # noqa: E731

    # ── WHELP ART ──
    film(p("new_noroot"), f"{out}/01_puppet_strip_root_OFF_MUTED.mp4", "SECOND PASS: the puppet strip (inside the idle bounds), root motion OFF, everything off", "--mute")
    film(n("new_noroot"), f"{out}/02_redrawn_strip_root_OFF_MUTED.mp4", "THIRD PASS: the redrawn strip on the extended canvas, ROOT MOTION OFF, VFX / tint / flash OFF", "--mute")
    grid([p("new_noroot"), n("new_noroot")], ["puppet, root OFF", "redrawn, root OFF"], f"{out}/03_root_OFF_puppet_vs_redrawn_play_size_MUTED.mp4", PACK, shrink=1.0)
    grid([p("new_noroot"), n("new_noroot")], ["puppet, root OFF, 4x slower", "redrawn, root OFF, 4x slower"], f"{out}/03b_root_OFF_close_slow_4x_MUTED.mp4", PACK, fps=15, shrink=1.0, lo=6650, hi=7300)
    sheet([p("new_noroot"), n("new_noroot")], ["PUPPET, root OFF", "REDRAWN, root OFF"], f"{out}/03c_root_OFF_every_frame_1to1.png",
          7017, -300, 150, WIDE_WHELP, per_row=14,
          note="Every frame at play size from -300 ms, root motion OFF: the second pass's puppet strip above, the third pass's redrawn poses on the 640 canvas below.")
    film(n("new_sil"), f"{out}/04_redrawn_strip_silhouette_MUTED.mp4", "THIRD PASS: silhouette only", "--mute")
    shutil.copy(os.path.join(V2, "umbral_swarm_attack_640_sheet.png"), f"{out}/05_key_poses_COIL_COMMIT_CONTACT_FOLLOW.png")
    shutil.copy(os.path.join(BITE, "seq_d.png"), f"{out}/05b_root_OFF_key_pose_row_play_size.png")
    shutil.copy(os.path.join(BITE, "pixellab_640", "sheet.png"), f"{out}/05c_pixellab_pose_references_256px.png")
    grid([n("bare_l15"), n("bare_l20"), n("bare_l25")], ["15 %", "20 % (chosen)", "25 %"], f"{out}/06_lunge_travel_15_20_25_bare_MUTED.mp4", PACK, shrink=1.0)
    grid([n("bare_l15"), n("bare_l20"), n("bare_l25")], ["15 %, 4x slower", "20 %, 4x slower", "25 %, 4x slower"], f"{out}/06b_lunge_travel_close_slow_4x_MUTED.mp4", PACK, fps=15, shrink=1.0, lo=6700, hi=7300)
    film(n("new_bare"), f"{out}/07_chosen_full_attack_bare_true_speed_MUTED.mp4", "THIRD PASS: the chosen travel, everything off", "--mute")
    film(n("new"), f"{out}/07b_chosen_full_attack_true_speed_sound.mp4", "THIRD PASS: the routine bite as played, true speed, sound")

    # ── CONTACT MOTIF ──
    shutil.copy(os.path.join(BITE, "motifs_n", "motif_n_candidates_sheet.png"), f"{out}/08_motif_candidates_N1_N2_N3_open_closed.png")
    sheet([n("new")], ["N2 at true speed, every frame"], f"{out}/10_selected_motif_N2_every_frame_1to1.png", 7017, -50, 100, CLOSE_SEEKER, per_row=10,
          note="Every frame at play size: the fang lifted and the jaw dropped (OPEN) at -33 and -17, driven together on the contact frame (SNAP), held, gone by ~+70; roots on the enemy side.")
    grid([n("new")], ["the contact, 4x slower"], f"{out}/10b_selected_motif_close_slow_4x_MUTED.mp4", CONTACT, fps=15, shrink=1.0, lo=6900, hi=7150)
    grid([p("new"), n("new")], ["SECOND PASS: M4 symmetric jaws", "THIRD PASS: N2 offset fang snap"], f"{out}/10c_motif_M4_vs_N2_close_MUTED.mp4", CONTACT, shrink=1.0, lo=6900, hi=7150)
    open(f"{out}/10d_trace.md", "w", encoding="utf-8").write(
        "# The bite's sentence on the playhead (bite_timeline.py), the bite at 7000\n\n## THIRD PASS\n\n" + run(TL, n("new") + ".log", "--bite", "7017") + "\n")
    film(n("rep"), f"{out}/11_repeated_pack_attack_fast_tempo_sound.mp4", "repeated pack attack at fast TEMPO (1000 in SPRAY's wind-up, 2000 in flight)")
    film(n("recv_only"), f"{out}/11b_receiver_alone_no_translation_MUTED.mp4", "the receiver alone, effects and flash OFF: nothing moves", "--mute")
    film(n("kill"), f"{out}/11c_regression_killing_hit_sound.mp4", "regression: the killing hit at 5017")
    film(n("boss"), f"{out}/11d_regression_boss_sound.mp4", "regression: the boss at half share")

    # ── JAWS A2 ──
    film(a2old("rep"), f"{out}/12_old_M4_derived_A2_MUTED.mp4", "SECOND PASS: A2 in the M4 family (not approved), muted", "--mute")
    film(a2("rep"), f"{out}/13_A2_in_the_selected_N2_family_muted_true_speed_MUTED.mp4", "THIRD PASS: A2 in the N2 family, Shadow, mirrored, ~1.2x, no line, muted", "--mute")
    grid([n("rep"), a2("rep")], ["fast TEMPO, no concept", "fast TEMPO, A2 (N2 family)"], f"{out}/14_A2_grid_MUTED.mp4", GRID)
    film(a2("rep"), f"{out}/15_A2_during_spray_fight_sound.mp4", "A2 during SPRAY, with the fight's own sound (no trap cue)")
    film(a2("hh"), f"{out}/16_A2_during_hard_hands_fight_sound.mp4", "A2 as HARD HANDS leaps (the bite at 13000)")
    sheet([a2("rep")], ["A2, the bite at 2000"], f"{out}/17_repeated_bite_shadow_bite_back_every_frame_1to1.png", 2017, -34, 136, (560, 540, 1180, 860), per_row=11, scale=0.8,
          note="Every frame: the red fang snaps on the Seeker (0); the Shadow fang, mirrored and a little larger, opens on the biter (+17), snaps (+34), rebounds, breaks into shards; no ownership line.")
    grid([a2("rep")], ["A2, 4x slower"], f"{out}/17b_A2_close_slow_4x_MUTED.mp4", "560,520,640,340", fps=15, shrink=0.9, lo=1950, hi=2200)
    film(a2("new"), f"{out}/17c_A2_normal_tempo_fight_sound.mp4", "A2 at normal TEMPO (the front whelp dies of the reflection on this bite)")
    if blind:
        os.makedirs(blind, exist_ok=True)
        for code, pre, t0 in (("v1", n("new_noroot"), 7017), ("v2", n("new"), 7017), ("v3", a2("rep"), 2017)):
            print("blind", code, flipbook(pre, f"{blind}/clip_{code}.png", t0, crop=(440, 522, 1340, 900), scale=0.5))
    print("done ->", out)


if __name__ == "__main__":
    main()
