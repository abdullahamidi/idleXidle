#!/usr/bin/env python3
"""jaws_bite_sound_evidence.py -- JAWS' cue made a BITE (ADR-011, 2026-09-28). The owner approved the smoke-teeth picture
and asked for the last thing: "if you make the sound effect more of a bite / being-bitten sound, we approve this skill
completely." The same film heard with the old thorn impact and with each bite candidate (A CHOMP, the default in the
game; B CRUNCH; C HEAVY), and A in context (SPRAY, HARD HANDS, repeated at fast TEMPO). The candidates B and C are
rendered into the same film from the trace (film_audio.py --cue), so the picture is identical and only the sound differs.
From build/shots/jaws/bitesfx/ (films_jaws.sh bitesfx normal fast_rearm during_spray during_hh) and
build/shots/jaws/smoke3/ (the approved picture with the old cue).

    PYTHONUTF8=1 python tools/asset-pipeline/make_action_sfx.py
    PYTHONUTF8=1 python tools/asset-pipeline/jaws_bite_sound_evidence.py production/qa/evidence/jaws-bite-sound
"""
import os
import shutil
import sys

from foundation_evidence import REPO, film

BEFORE = os.path.join(REPO, "build", "shots", "jaws", "smoke3")
NEW = os.path.join(REPO, "build", "shots", "jaws", "bitesfx")
COMBAT = os.path.join(REPO, "assets", "audio", "combat")
CUE = "sfx_seeker_jaws_bite"


def main() -> int:
    out = os.path.abspath(sys.argv[1])
    cands = os.path.join(out, "candidates")
    os.makedirs(out, exist_ok=True)
    b = lambda k: os.path.join(BEFORE, k)   # noqa: E731
    n = lambda k: os.path.join(NEW, k)      # noqa: E731
    crunch = os.path.join(cands, "sfx_seeker_jaws_bite_crunch.wav")
    heavy = os.path.join(cands, "sfx_seeker_jaws_bite_heavy.wav")
    film(b("normal"), f"{out}/01_before_thorn_impact_sound.mp4", "BEFORE: the old cue, a dark thorn impact")
    film(n("normal"), f"{out}/02_A_chomp_sound.mp4", "A CHOMP (in the game): teeth meet, sink in, the jaw's weight")
    film(n("normal"), f"{out}/03_B_crunch_sound.mp4", "B CRUNCH: biting through, crunchier, a lighter jaw", "--cue", f"{CUE}={crunch}")
    film(n("normal"), f"{out}/04_C_heavy_sound.mp4", "C HEAVY: a beast's jaws, deeper, fewer crunch grains", "--cue", f"{CUE}={heavy}")
    film(n("during_spray"), f"{out}/05_A_during_spray_sound.mp4", "A during SPRAY")
    film(n("during_hh"), f"{out}/06_A_during_hard_hands_sound.mp4", "A during HARD HANDS")
    film(n("fast_rearm"), f"{out}/07_A_repeated_fast_tempo_sound.mp4", "A repeated at fast TEMPO (one picture per 3 frames)")
    # the cues on their own, for listening without the fight
    for name in ("sfx_seeker_jaws_bite", "sfx_seeker_jaws_fangs"):
        shutil.copy(os.path.join(COMBAT, name + ".wav"), os.path.join(out, name + ".wav"))
    print("done ->", out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
