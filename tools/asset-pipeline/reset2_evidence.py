#!/usr/bin/env python3
"""reset2_evidence.py -- the bite reset's SECOND PASS review package (ADR-012, 2026-09-27): the authored whelp lunge
(puppet-posed, root motion off and on, the lunge travel series), the bite motif candidates and the selected motif at
true speed, the full routine hit, and rough JAWS A2 in the motif's own shape family. From foundation_films.sh's takes
in build/shots/foundation/ (SECOND PASS), the first reset's copies in build/shots/foundation_warp/ (the WARPED strip)
and the animatics in build/shots/reset2/a2_*/.

    PYTHONUTF8=1 python tools/asset-pipeline/reset2_evidence.py <out dir> [<blind dir>]
"""
import os
import shutil
import sys

from foundation_evidence import REPO, ARENA, GRID, CLOSE_WHELP, CLOSE_SEEKER, film, grid, sheet, flipbook, run, TL

WARP = os.path.join(REPO, "build", "shots", "foundation_warp")
NEW = os.path.join(REPO, "build", "shots", "foundation")
A2 = os.path.join(REPO, "build", "shots", "reset2")
BITE = os.path.join(REPO, "build", "shots", "bite")
V2 = os.path.join(REPO, "tools", "asset-pipeline", "v2", "keypose_sources")
CONTACT = "480,500,360,300"      # the Seeker's chest and the leader's head at contact
PACK = "880,560,420,340"         # the front of the pack, close


def main():
    out = os.path.abspath(sys.argv[1])
    blind = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else None
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png", ".md")) and f[0].isdigit():
            os.remove(os.path.join(out, f))
    w = lambda n: os.path.join(WARP, n)     # noqa: E731
    n = lambda k: os.path.join(NEW, k)      # noqa: E731
    a2 = lambda k: os.path.join(A2, "a2_" + k, k)     # noqa: E731

    # ── WHELP ──
    # 1 the current warped strip (the first reset), root OFF and as played bare
    film(w("new_noroot"), f"{out}/01_warped_strip_root_OFF_MUTED.mp4", "WARPED strip (first reset), root motion OFF, everything off", "--mute")
    film(w("new_bare"), f"{out}/01b_warped_strip_root_ON_bare_MUTED.mp4", "WARPED strip, root motion 30 %, everything off", "--mute")
    # 2-3 the newly authored strip, root OFF (the acceptance test) and its every-frame sheet beside the warp
    film(n("new_noroot"), f"{out}/02_authored_strip_root_OFF_the_poses_alone_MUTED.mp4", "AUTHORED strip (joint puppet), ROOT MOTION OFF, everything off: the acceptance test", "--mute")
    grid([w("new_noroot"), n("new_noroot")], ["WARPED, root OFF", "AUTHORED, root OFF"], f"{out}/02b_root_OFF_warped_vs_authored_close_MUTED.mp4", PACK, shrink=1.0)
    grid([w("new_noroot"), n("new_noroot")], ["WARPED, root OFF, 4x slower", "AUTHORED, root OFF, 4x slower"], f"{out}/02c_root_OFF_warped_vs_authored_close_slow_4x_MUTED.mp4", PACK, fps=15, shrink=1.0, lo=6650, hi=7300)
    sheet([w("new_noroot"), n("new_noroot")], ["WARPED, root OFF", "AUTHORED, root OFF"], f"{out}/03_root_OFF_every_frame_1to1.png",
          7017, -300, 150, CLOSE_WHELP, per_row=14,
          note="Every frame at play size from -300 ms, root motion OFF: the warped strip (rejected) above, the authored puppet strip below: coil -> commit -> contact -> follow-through.")
    # 4 the authored strip with the lunge travel series 20 / 25 / 30 %
    grid([n("bare_l20"), n("new_bare"), n("bare_l30")], ["20 %", "25 % (chosen)", "30 %"], f"{out}/04_lunge_travel_20_25_30_bare_MUTED.mp4", PACK, shrink=1.0)
    grid([n("bare_l20"), n("new_bare"), n("bare_l30")], ["20 %, 4x slower", "25 %, 4x slower", "30 %, 4x slower"], f"{out}/04b_lunge_travel_close_slow_4x_MUTED.mp4", PACK, fps=15, shrink=1.0, lo=6700, hi=7300)
    grid([n("new_l20"), n("new"), n("new_l30")], ["20 %", "25 % (chosen)", "30 %"], f"{out}/04c_lunge_travel_20_25_30_everything_on_MUTED.mp4", GRID)
    film(n("new_bare"), f"{out}/04d_authored_strip_25pct_bare_MUTED.mp4", "AUTHORED strip, the chosen 25 % lunge, everything off", "--mute")
    film(n("new_sil"), f"{out}/04e_authored_strip_silhouette_MUTED.mp4", "AUTHORED strip, silhouette", "--mute")
    # 5 silhouette / key-pose sheets: the puppet's key poses, the root-off row, the PixelLab pose concepts (references only)
    shutil.copy(os.path.join(V2, "umbral_swarm_lunge_sheet.png"), f"{out}/05_key_poses_crouch_vs_authored.png")
    shutil.copy(os.path.join(BITE, "seq_c.png"), f"{out}/05b_root_OFF_key_pose_row_play_size.png")
    shutil.copy(os.path.join(BITE, "seq_a.png"), f"{out}/05c_WARPED_key_pose_row_play_size.png")
    refs = os.path.join(BITE, "pixellab_refs")
    from PIL import Image, ImageDraw
    r1 = Image.open(os.path.join(refs, "contact_concept_seed0.png")).convert("RGBA")
    r2 = Image.open(os.path.join(refs, "commit_concept_seed11.png")).convert("RGBA")
    sh = Image.new("RGB", (2 * 520 + 30, 560), (40, 36, 48))
    d = ImageDraw.Draw(sh)
    for i, (im, label) in enumerate(((r2, "PixelLab pose concept: COMMIT (reference only, not used as art)"), (r1, "PixelLab pose concept: CONTACT (reference only, not used as art)"))):
        bg = Image.new("RGBA", (512, 512), (40, 36, 48, 255))
        bg.alpha_composite(im.resize((512, 512), Image.NEAREST))
        sh.paste(bg.convert("RGB"), (10 + i * 530, 40))
        d.text((10 + i * 530, 14), label, fill=(255, 220, 120))
    sh.save(f"{out}/05d_pixellab_pose_concepts_reference_only.png")

    # ── BITE MOTIF ──
    shutil.copy(os.path.join(BITE, "motifs", "motif_candidates_sheet.png"), f"{out}/06_motif_candidates_M1_M2_M3_M4_open_closed.png")
    shutil.copy(os.path.join(BITE, "motifs", "M4_pair.png"), f"{out}/07_selected_motif_open_closed_3x.png")
    sheet([n("new")], ["the selected motif, every frame"], f"{out}/09_selected_motif_every_frame_1to1.png", 7017, -50, 100, CLOSE_SEEKER, per_row=10,
          note="Every frame at play size at his torso edge: jaws OPEN at -33 and -17, SNAP shut on the contact frame, fang tips crossed to +17, residue, gone by ~+70.")
    grid([n("new")], ["the contact, 4x slower"], f"{out}/09b_selected_motif_close_slow_4x_MUTED.mp4", CONTACT, fps=15, shrink=1.0, lo=6900, hi=7150)
    grid([w("new"), n("new")], ["FIRST RESET: crescent claw shape, on his blade", "SECOND PASS: jaws + fangs, on his torso edge"], f"{out}/09c_motif_first_vs_second_close_MUTED.mp4", CONTACT, shrink=1.0, lo=6900, hi=7150)
    open(f"{out}/09d_trace.md", "w", encoding="utf-8").write(
        "# The bite's sentence on the playhead (bite_timeline.py), the bite at 7000\n\n## SECOND PASS\n\n" + run(TL, n("new") + ".log", "--bite", "7017")
        + "\n\n## FIRST RESET (warped strip)\n\n" + run(TL, w("new") + ".log", "--bite", "7017") + "\n")

    # ── FULL HIT ──
    film(n("new"), f"{out}/10_full_hit_lunge_motif_flash_number_true_speed_sound.mp4", "the routine bite: authored lunge + selected motif + the usual flash + sound + number")
    film(n("recv_only"), f"{out}/10b_receiver_alone_no_translation_MUTED.mp4", "the receiver alone, effects and flash OFF: nothing moves", "--mute")
    film(n("rep"), f"{out}/11_repeated_pack_bites_fast_tempo_sound.mp4", "repeated pack bites at fast TEMPO (1000 in SPRAY's wind-up, 2000 in flight)")
    film(n("kill"), f"{out}/11b_regression_killing_hit_sound.mp4", "regression: the killing hit at 5017")
    film(n("boss"), f"{out}/11c_regression_boss_sound.mp4", "regression: the boss at half share")

    # ── JAWS A2, the same shape family ──
    grid([n("new"), a2("new")], ["the second pass, no concept", "rough A2 (mirrored jaws, Shadow) over it"], f"{out}/12_A2_mirrored_over_second_pass_grid_MUTED.mp4", GRID)
    film(a2("rep"), f"{out}/13_A2_muted_true_speed_MUTED.mp4", "rough A2, fast TEMPO, MUTED (no trap sound; audio is a later pass)", "--mute")
    film(a2("rep"), f"{out}/14_A2_during_spray_fight_sound.mp4", "rough A2 with the fight's own sound (no snap cue): bites at 1000 and 2000, SPRAY in flight")
    film(a2("hh"), f"{out}/15_A2_during_hard_hands_fight_sound.mp4", "rough A2 as HARD HANDS leaps at 13083 (the bite at 13000)")
    grid([n("rep"), a2("rep")], ["fast TEMPO, no concept", "fast TEMPO, rough A2"], f"{out}/16_repeated_bite_bite_back_grid_MUTED.mp4", GRID)
    sheet([a2("rep")], ["A2, the bite at 2000"], f"{out}/16b_A2_every_frame_1to1.png", 2017, -34, 136, (560, 540, 1180, 860), per_row=11, scale=0.8,
          note="Every frame: the red jaws snap on the Seeker (0), the Shadow jaws open on the biter (+17), snap (+34), rebound, fade; no ownership line.")
    grid([a2("rep")], ["A2, 4x slower"], f"{out}/16c_A2_close_slow_4x_MUTED.mp4", "560,520,640,340", fps=15, shrink=0.9, lo=1950, hi=2200)
    film(a2("new"), f"{out}/16d_A2_normal_tempo_fight_sound.mp4", "rough A2 at normal TEMPO (the front whelp dies of the reflection on this bite)")
    if blind:
        os.makedirs(blind, exist_ok=True)
        for code, pre, t0 in (("p1", n("new_noroot"), 7017), ("p2", n("new"), 7017), ("p3", a2("rep"), 2017)):
            print("blind", code, flipbook(pre, f"{blind}/clip_{code}.png", t0, crop=(440, 522, 1340, 900), scale=0.5))
    print("done ->", out)


if __name__ == "__main__":
    main()
