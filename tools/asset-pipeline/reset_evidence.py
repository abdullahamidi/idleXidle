#!/usr/bin/env python3
"""reset_evidence.py -- the bite RESET / PROTOTYPE battery (ADR-012, 2026-09-27): the maw version beside the mouthless
predatory lunge, the bite contact motif, the receiver with no translation (and the optional micro-impulse), and the
rough JAWS A2 MIRRORED SHADOW SNAP over three fights. From the takes foundation_films.sh filmed into
build/shots/foundation/ (RESET), the maw pass's copies in build/shots/foundation_maw/ (the REJECTED version), and the
A2 animatics prototypes/jaws-concepts/animatic.py drew into build/shots/reset/a2_*/.

    PYTHONUTF8=1 python tools/asset-pipeline/reset_evidence.py <out dir> [<blind dir>]
"""
import os
import shutil
import sys

from foundation_evidence import REPO, ARENA, GRID, CLOSE_WHELP, CLOSE_SEEKER, film, grid, sheet, flipbook, run, TL

MAW = os.path.join(REPO, "build", "shots", "foundation_maw")
NEW = os.path.join(REPO, "build", "shots", "foundation")
A2 = os.path.join(REPO, "build", "shots", "reset")
CONTACT = "560,520,360,300"      # the Seeker's chest edge and the leader's head at contact
ATTACKER = "820,480,560,380"     # the front of the pack


def main():
    out = os.path.abspath(sys.argv[1])
    blind = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else None
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png", ".md")) and f[0].isdigit():
            os.remove(os.path.join(out, f))
    m = lambda n: os.path.join(MAW, n)     # noqa: E731
    n = lambda k: os.path.join(NEW, k)     # noqa: E731
    a2 = lambda k: os.path.join(A2, "a2_" + k, k)     # noqa: E731
    MAWL, NEWL = "REJECTED: painted maw, 10 % recoil, edge wedge", "RESET: mouthless lunge, bite motif, no recoil"

    # 1 the current (rejected) maw version, true speed
    film(m("new"), f"{out}/01_rejected_maw_version_true_speed_sound.mp4", MAWL)
    grid([m("new"), n("new")], [MAWL, NEWL], f"{out}/01b_rejected_vs_reset_true_speed_MUTED.mp4", GRID)
    # 2 the mouthless body lunge, effects OFF: as played bare, without root motion (the poses alone), silhouette
    film(n("new_bare"), f"{out}/02_lunge_tint_vfx_flash_OFF_MUTED.mp4", "RESET: mouthless lunge, tint/effects/flash OFF", "--mute")
    film(n("new_noroot"), f"{out}/02b_lunge_ROOT_MOTION_OFF_the_poses_alone_MUTED.mp4", "RESET: root motion OFF (the strip's poses alone), everything else off", "--mute")
    film(n("new_sil"), f"{out}/02c_lunge_silhouette_MUTED.mp4", "RESET: silhouette", "--mute")
    shutil.copy(os.path.join(REPO, "tools", "asset-pipeline", "v2", "keypose_sources", "umbral_swarm_bite_sheet.png"), f"{out}/02d_key_poses_crouch_vs_lunge.png")
    sheet([n("new_noroot"), n("new_bare"), m("new_bare")], ["RESET, root OFF", "RESET, root ON", "REJECTED (maw)"], f"{out}/02e_poses_every_frame_1to1.png",
          7017, -300, 120, CLOSE_WHELP, per_row=13,
          note="Every frame at play size from -300 ms: the mouthless poses without root motion (do they read coil -> rise -> dive -> compress?), the same with the 30 % lunge, and the rejected maw version.")
    grid([n("new_noroot"), n("new_bare")], ["root motion OFF, 4x slower", "root motion ON, 4x slower"], f"{out}/02f_poses_close_slow_4x_MUTED.mp4", "900,560,420,340", fps=15, shrink=1.0, lo=6650, hi=7250)
    # 3 the lunge with the bite contact motif (effects on, the usual flash on the whelp, the number)
    film(n("new"), f"{out}/03_lunge_and_bite_motif_true_speed_sound.mp4", NEWL)
    sheet([n("new")], ["RESET: the motif at his chest edge"], f"{out}/03b_motif_every_frame_1to1.png", 7017, -50, 120, CLOSE_SEEKER, per_row=11,
          note="Every frame at play size: the fangs faintly OPEN at -33 and -17, SHUT on the contact frame (0) with the snap streak, tips crossed to +17, residue to ~+80, gone.")
    grid([n("new")], ["the contact, 4x slower"], f"{out}/03c_motif_close_slow_4x_MUTED.mp4", CONTACT, fps=15, shrink=1.0, lo=6900, hi=7200)
    open(f"{out}/03d_trace.md", "w", encoding="utf-8").write(
        "# The bite's sentence on the playhead (bite_timeline.py), the bite at 7000\n\n## RESET\n\n" + run(TL, n("new") + ".log", "--bite", "7017")
        + "\n\n## REJECTED (maw pass)\n\n" + run(TL, m("new") + ".log", "--bite", "7017") + "\n")
    # 4 no receiver translation: the receiver alone (nothing should move), beside the rejected 10 % recoil
    film(n("recv_only"), f"{out}/04_receiver_alone_no_translation_MUTED.mp4", "RESET: the receiver alone, effects and flash OFF: no recoil", "--mute")
    grid([m("recv10"), n("recv_only")], ["REJECTED: 10 % recoil (27 px)", "RESET: no translation"], f"{out}/04b_recoil_rejected_vs_none_close_MUTED.mp4", "430,540,360,360", shrink=1.0, lo=6900, hi=7300)
    # 5 the optional micro-impulse: 0 (default) / 1 % / 2 % of his visible width, effects on
    grid([n("new"), n("rec01"), n("rec02")], ["0 % (the reset's default)", "1 % micro-impulse (~3 px)", "2 % (~5 px)"], f"{out}/05_micro_impulse_0_1_2_close_MUTED.mp4", CONTACT, shrink=1.0, lo=6900, hi=7300)
    grid([n("new"), n("rec01"), n("rec02")], ["0 %, 4x slower", "1 %, 4x slower", "2 %, 4x slower"], f"{out}/05b_micro_impulse_close_slow_4x_MUTED.mp4", CONTACT, fps=15, shrink=1.0, lo=6950, hi=7200)
    # 6-8 rough JAWS A2 MIRRORED SHADOW SNAP over the reset: normal tempo, SPRAY's window, HARD HANDS
    grid([n("new"), a2("new")], ["the reset, no concept", "rough A2 over it"], f"{out}/06_A2_over_reset_grid_MUTED.mp4", GRID)
    film(a2("new"), f"{out}/06b_A2_normal_tempo_sound.mp4", "rough A2, normal TEMPO, the bite at 7000 (the front whelp dies of the reflection)")
    film(a2("rep"), f"{out}/07_A2_fast_tempo_during_spray_sound.mp4", "rough A2, fast TEMPO: bites at 1000 (SPRAY winding up) and 2000 (SPRAY in flight)")
    film(a2("hh"), f"{out}/08_A2_during_hard_hands_sound.mp4", "rough A2, the bite at 13000 as HARD HANDS leaps")
    # 9 repeated bite -> retaliation: the two bites of the fast take, the motif on him and the answer on the biter
    grid([n("rep"), a2("rep")], ["fast TEMPO, no concept", "fast TEMPO, rough A2"], f"{out}/09_repeated_bite_retaliation_grid_MUTED.mp4", GRID)
    sheet([a2("rep")], ["A2, the bite at 2000"], f"{out}/09b_A2_every_frame_1to1.png", 2017, -50, 170, (600, 500, 1300, 900), per_row=8, scale=0.8,
          note="Every frame: the red motif shuts on the Seeker (0), the Shadow answer opens on the biter (+17), closes (+33), rebounds and fades; the short ownership trace runs +17..+67, after the snap began.")
    grid([a2("rep")], ["A2, 4x slower"], f"{out}/09c_A2_close_slow_4x_MUTED.mp4", "560,480,820,380", fps=15, shrink=0.8, lo=1900, hi=2250)
    # regression: the killing hit, the boss
    film(n("kill"), f"{out}/10_regression_killing_hit_fast_tempo_sound.mp4", "regression: the killing hit at 5017, fast TEMPO")
    film(n("boss"), f"{out}/10b_regression_boss_sound.mp4", "regression: the boss performs the same curve at half share")
    if blind:
        os.makedirs(blind, exist_ok=True)
        for code, pre, t0 in (("k1", n("new_bare"), 7017), ("k2", n("new"), 7017), ("k3", a2("rep"), 2017), ("k4", n("new_noroot"), 7017)):
            print("blind", code, flipbook(pre, f"{blind}/clip_{code}.png", t0, crop=(440, 522, 1340, 900), scale=0.5))
    print("done ->", out)


if __name__ == "__main__":
    main()
