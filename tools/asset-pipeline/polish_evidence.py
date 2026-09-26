#!/usr/bin/env python3
"""polish_evidence.py -- the bite foundation's SECOND battery (ADR-012 polish: the maw, the spacing, the recoil series,
the directional contact accent, the JAWS A recheck), from the takes foundation_films.sh filmed into
build/shots/foundation/ (NEW) and the first pass's copies in build/shots/foundation_v1/ (the CURRENT foundation).

    PYTHONUTF8=1 python tools/asset-pipeline/polish_evidence.py <out dir> [<blind dir>]
"""
import os
import shutil
import sys

from foundation_evidence import REPO, ARENA, GRID, CLOSE_WHELP, CLOSE_SEEKER, film, grid, sheet, flipbook, run, TL

V1 = os.path.join(REPO, "build", "shots", "foundation_v1")
NEW = os.path.join(REPO, "build", "shots", "foundation")


def main():
    out = os.path.abspath(sys.argv[1])
    blind = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else None
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png")):
            os.remove(os.path.join(out, f))
    v1 = lambda n: os.path.join(V1, n)     # noqa: E731
    n = lambda m: os.path.join(NEW, m)     # noqa: E731
    CUR, NEWL = "CURRENT foundation (first pass)", "NEW: maw, spaced commit, 10 % recoil, chest-edge accent"

    # 1-2 current foundation attack vs the new bite
    grid([v1("new"), n("new")], [CUR, NEWL], f"{out}/01_current_vs_new_bite_true_speed_MUTED.mp4", GRID)
    film(v1("new"), f"{out}/01a_current_foundation_true_speed_sound.mp4", CUR)
    film(n("new"), f"{out}/02_new_bite_true_speed_sound.mp4", NEWL)
    # 3-4 everything off, silhouette
    film(n("new_bare"), f"{out}/03_new_bite_tint_vfx_flash_OFF_MUTED.mp4", "NEW: tint, effects and flash OFF (the acceptance view)", "--mute")
    grid([v1("new_bare"), n("new_bare")], ["CURRENT, everything off", "NEW, everything off"], f"{out}/03b_bare_current_vs_new_MUTED.mp4", GRID)
    film(n("new_sil"), f"{out}/04_new_bite_silhouette_MUTED.mp4", "NEW: silhouette", "--mute")
    # 5 the key poses; 6 the spacing comparison
    shutil.copy(os.path.join(REPO, "tools", "asset-pipeline", "v2", "keypose_sources", "umbral_swarm_bite_sheet.png"), f"{out}/05_key_poses_with_the_maw.png")
    sheet([v1("new_bare"), n("new_bare")], ["CURRENT (ease 2.4)", "NEW (ease 1.8, commit from -234 ms)"], f"{out}/06_spacing_every_frame_1to1.png",
          7017, -300, 120, CLOSE_WHELP, per_row=13, note="Every frame at play size from -300 ms: the first pass (the commit crammed into the last 100 ms) beside the polish (commit from -234 ms, the maw opening, the pre-contact thrust, the shut maw on the contact frame).")
    grid([v1("new_bare"), n("new_bare")], ["CURRENT spacing, 4x slower", "NEW spacing, 4x slower"], f"{out}/06b_spacing_close_slow_4x_MUTED.mp4", "960,600,340,300", fps=15, shrink=1.0, lo=6650, hi=7250)
    open(f"{out}/06c_trace.md", "w", encoding="utf-8").write(
        "# The bite's sentence on the playhead (bite_timeline.py)\n\n## NEW, the bite at 7000\n\n" + run(TL, n("new") + ".log", "--bite", "7017")
        + "\n\n## CURRENT (first pass), the bite at 7000\n\n" + run(TL, v1("new") + ".log", "--bite", "7017") + "\n")
    # 7 the pack
    film(n("new"), f"{out}/07_pack_four_whelps_true_speed_sound.mp4", "NEW: the pack, front-led", crop="900,410,980,670")
    # 8 the recoil series, effects and flash off; 9 the chosen recoil alone
    grid([n("recv06"), n("recv08"), n("recv10")], ["6 %", "8 %", "10 % (chosen)"], f"{out}/08_recoil_6_8_10_effects_off_MUTED.mp4", "430,540,360,360", shrink=1.0, lo=6900, hi=7300)
    grid([n("recv06"), n("recv08"), n("recv10")], ["6 %", "8 %", "10 % (chosen)"], f"{out}/08b_recoil_6_8_10_close_slow_4x_MUTED.mp4", "430,540,360,360", fps=15, shrink=1.0, lo=6950, hi=7200)
    sheet([n("recv06"), n("recv_only"), n("recv10")], ["6 %", "8 %", "10 %"], f"{out}/08c_recoil_every_frame_1to1.png", 7017, -34, 133, CLOSE_SEEKER, per_row=11,
          note="Every frame at play size round the bite, effects and flash off: the recoil at 6, 8 and 10 % of his visible width (16, 21, 27 px) with the 1.5 % dip; 10 % is the build's default.")
    film(n("recv_only"), f"{out}/09_receiver_alone_true_speed_MUTED.mp4", "the receiver alone: effects and flash OFF, the 10 % recoil", "--mute")
    # 10 old centred burst vs new directional accent
    grid([v1("new"), n("new")], ["CURRENT: centred burst (0.36, ~570 ms) + streaks", "NEW: edge mark (0.16, ~290 ms) + compression wedge"], f"{out}/10_contact_current_vs_new_close_MUTED.mp4", "430,540,360,360", shrink=1.0, lo=6950, hi=7350)
    sheet([v1("new"), n("new")], ["CURRENT", "NEW"], f"{out}/10b_contact_every_frame_1to1.png", 7017, -17, 150, CLOSE_SEEKER, per_row=11,
          note="Every frame at play size: the first pass's centred burst beside the polish's edge mark and wedge, with the recoil under both.")
    # 11-13 repeated combat, SPRAY, HARD HANDS
    film(n("rep"), f"{out}/11_repeated_combat_fast_tempo_music.mp4", "repeated combat, fast TEMPO", "--music", "music_arena_shadow")
    grid([v1("rep"), n("rep")], [CUR, NEWL], f"{out}/11b_repeated_current_vs_new_MUTED.mp4", GRID)
    film(n("rep"), f"{out}/12_hit_during_spray_sound.mp4", "hits during SPRAY (1000 in its wind-up, 2000 in flight)")
    film(n("hh"), f"{out}/13_hit_during_hard_hands_sound.mp4", "a hit at 13000 as HARD HANDS leaps at 13083")
    film(n("hh_bare"), f"{out}/13b_hit_during_hard_hands_everything_off_MUTED.mp4", "HARD HANDS window, tint/effects/flash OFF", "--mute")
    # 14 rough Concept A over the final foundation, three views
    A = os.path.join(NEW, "concepts", "A")
    grid([n("new"), os.path.join(A, "new")], ["the new foundation, no concept", "rough Concept A over it"], f"{out}/14_concept_A_over_new_foundation_grid_MUTED.mp4", GRID)
    film(os.path.join(A, "new"), f"{out}/14a_concept_A_normal_tempo_sound.mp4", "rough A, normal TEMPO, the bite at 7000")
    film(os.path.join(A, "rep"), f"{out}/14b_concept_A_fast_tempo_during_spray_sound.mp4", "rough A, fast TEMPO, the bite at 2000 with SPRAY in flight")
    film(os.path.join(A, "hh"), f"{out}/14c_concept_A_during_hard_hands_sound.mp4", "rough A, the bite at 13000 as HARD HANDS leaps")
    if blind:
        os.makedirs(blind, exist_ok=True)
        for code, pre, t0 in (("s2", n("new_bare"), 7017), ("s5", n("recv_only"), 7017), ("s7", os.path.join(A, "new"), 7017),
                              ("s3", os.path.join(A, "rep"), 2017), ("s8", os.path.join(A, "hh"), 13017)):
            crop = (440, 522, 1760, 900) if "hh" in pre else (440, 522, 1340, 900)
            print("blind", code, flipbook(pre, f"{blind}/clip_{code}.png", t0, crop=crop, scale=0.5 if "hh" not in pre else 0.42))
    print("done ->", out)


if __name__ == "__main__":
    main()
