#!/usr/bin/env python3
"""jaws_piranha_evidence.py -- the JAWS review package for the SHADOW PIRANHA production direction (ADR-011, 2026-09-27):
nine films, no concept battery. From build/shots/jaws/piranha/ (films_jaws.sh piranha: normal, fast_rearm,
during_spray, during_hh, kill, fatal) and the last mechanical version's takes in build/shots/jaws/polish/.

    PYTHONUTF8=1 python tools/asset-pipeline/jaws_piranha_evidence.py <out dir>
"""
import os
import sys

from foundation_evidence import REPO, ARENA, GRID, film, grid, sheet, run, TL

OLD = os.path.join(REPO, "build", "shots", "jaws", "polish")
NEW = os.path.join(REPO, "build", "shots", "jaws", "piranha")
CLOSE = "860,540,420,340"        # the front of the pack, close


def main():
    out = os.path.abspath(sys.argv[1])
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png", ".md")) and f[0].isdigit():
            os.remove(os.path.join(out, f))
    o = lambda n: os.path.join(OLD, n)     # noqa: E731
    n = lambda k: os.path.join(NEW, k)     # noqa: E731
    film(o("normal"), f"{out}/01_old_mechanical_jaws_true_speed_sound.mp4", "OLD: the mechanical bear trap (rejected), normal TEMPO, the bite at 7000")
    film(n("normal"), f"{out}/02_shadow_piranha_jaws_true_speed_sound.mp4", "NEW: Shadow piranha JAWS, normal TEMPO, the bite at 7000")
    film(n("normal"), f"{out}/03_shadow_piranha_jaws_MUTED.mp4", "NEW: muted", "--mute")
    grid([n("normal")], ["the answer, close"], f"{out}/04_close_crop_true_speed_MUTED.mp4", CLOSE, shrink=1.0, lo=6900, hi=7300)
    grid([n("normal")], ["the answer, close, 4x slower"], f"{out}/04b_close_crop_slow_4x_MUTED.mp4", CLOSE, fps=15, shrink=1.0, lo=6950, hi=7200)
    sheet([n("normal")], ["Shadow piranha, every frame"], f"{out}/04c_every_frame_1to1.png", 7017, -17, 150, (860, 560, 1300, 900), per_row=11,
          note="Every frame at play size: the bite lands (0); the first jaw appears on the biter and darts in; the main CHOMP at +25 with the number and the flash; the second and third jaws overlap; residue; gone by ~+140.")
    film(n("fast_rearm"), f"{out}/05_repeated_triggers_fast_tempo_sound.mp4", "repeated triggers at fast TEMPO (one picture per 3 frames)")
    film(n("during_spray"), f"{out}/06_during_spray_sound.mp4", "JAWS answers at 1000 in SPRAY's wind-up, the knives out at 1150")
    film(n("during_hh"), f"{out}/07_during_hard_hands_sound.mp4", "JAWS answers at 13000 as HARD HANDS leaps at 13083")
    film(n("kill"), f"{out}/08_target_kill_sound.mp4", "the answer KILLS the biter (a frail pack that bites hard)")
    film(n("fatal"), f"{out}/09_seeker_death_same_bite_sound.mp4", "the bite fells the Seeker as JAWS answers it")
    open(f"{out}/10_trace.md", "w", encoding="utf-8").write(
        "# The bite and the answer on the playhead (bite_timeline.py), the bite at 7000\n\n" + run(TL, n("normal") + ".log", "--bite", "7017") + "\n")
    print("done ->", out)


if __name__ == "__main__":
    main()
