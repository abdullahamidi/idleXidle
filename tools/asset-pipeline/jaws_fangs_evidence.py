#!/usr/bin/env python3
"""jaws_fangs_evidence.py -- the JAWS SHADOW FANGS pass (ADR-011, the final direction, 2026-09-27): six films and the
trace, no research battery. From build/shots/jaws/fangs/ (films_jaws.sh fangs normal fast_rearm during_spray
during_hh) and build/shots/jaws/piranha/ (the rejected one-piranha pass, for the comparison).

    PYTHONUTF8=1 python tools/asset-pipeline/jaws_fangs_evidence.py <out dir>
"""
import os
import sys

from foundation_evidence import REPO, film, run, TL

PIRANHA = os.path.join(REPO, "build", "shots", "jaws", "piranha")
NEW = os.path.join(REPO, "build", "shots", "jaws", "fangs")


def main():
    out = os.path.abspath(sys.argv[1])
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png", ".md")) and f[0].isdigit():
            os.remove(os.path.join(out, f))
    p = lambda k: os.path.join(PIRANHA, k)   # noqa: E731
    n = lambda k: os.path.join(NEW, k)       # noqa: E731
    # the bite at 7000 in both: the whelp bites, JAWS answers, the answer kills the front whelp (its fall follows the snap)
    film(p("normal"), f"{out}/01_current_piranha_true_speed_sound.mp4", "CURRENT: the one Shadow piranha (rejected), true speed")
    film(n("normal"), f"{out}/02_shadow_fangs_true_speed_sound.mp4", "NEW: SHADOW FANGS, true speed")
    film(n("normal"), f"{out}/03_shadow_fangs_MUTED.mp4", "NEW: SHADOW FANGS, muted", "--mute")
    film(n("fast_rearm"), f"{out}/04_repeated_triggers_fast_tempo_sound.mp4", "repeated triggers at fast TEMPO (one picture per 3 frames)")
    film(n("during_spray"), f"{out}/05_during_spray_sound.mp4", "JAWS answers at 1000 in SPRAY's wind-up, the knives out at 1150")
    film(n("during_hh"), f"{out}/06_during_hard_hands_sound.mp4", "JAWS answers at 13000 as HARD HANDS leaps at 13083")
    # the reaction layer's own lines for that bite: the spawn on the bite's frame, the snap (reaction-clamp) and the cue,
    # the number's frame, the end; and the draw cost (four sprites, no allocation)
    lines = [l.rstrip("\n") for l in open(n("normal") + ".log", encoding="utf-8", errors="replace")
             if "\treaction-" in l and ("contact=7000" in l or "reaction-draw" in l)]
    draws = [l for l in lines if "reaction-draw" in l]
    own = [l for l in lines if "reaction-draw" not in l]
    open(f"{out}/07_trace.md", "w", encoding="utf-8").write(
        "# The bite and the answer on the playhead (bite_timeline.py), the bite at 7000\n\n" + run(TL, n("normal") + ".log", "--bite", "7017") + "\n"
        + "\n## The reaction layer (RH_PRESENT_TRACE): `at=` is ms after the bite's ms; the first frame that shows the bite is +17\n\n```\n"
        + "\n".join(own) + "\n```\n\n" + f"reaction-draw lines: {len(draws)}; distinct cost fields: "
        + ", ".join(sorted({l.split('\t')[-1] for l in draws})) + "; sprites per frame: "
        + ", ".join(sorted({l.split('\t')[5] for l in draws})) + "\n")
    print("done ->", out)


if __name__ == "__main__":
    main()
