#!/usr/bin/env python3
"""jaws_real_bite_evidence.py -- JAWS' bite built from REAL recorded foley (ADR-011, 2026-09-28). The owner rejected the
three synthesised bites ("all similar; I want a real bite, like Trundle's Q (Chomp) in League of Legends"). The same film
heard with the rejected synthesised bite and with each real-foley candidate (A CHOMP, in the game; B BONE; C JUICY), and
A in context (SPRAY, HARD HANDS, repeated at fast TEMPO). Every sound is rendered into the same film from the trace
(film_audio.py --cue), so the picture is identical and only the sound differs.
From build/shots/jaws/bitesfx/ (films_jaws.sh bitesfx normal fast_rearm during_spray during_hh).

    PYTHONUTF8=1 python tools/asset-pipeline/make_jaws_bite.py
    PYTHONUTF8=1 python tools/asset-pipeline/jaws_real_bite_evidence.py production/qa/evidence/jaws-real-bite
"""
import os
import shutil
import sys

from foundation_evidence import REPO, film

SHOTS = os.path.join(REPO, "build", "shots", "jaws", "bitesfx")
COMBAT = os.path.join(REPO, "assets", "audio", "combat")
CUE = "sfx_seeker_jaws_bite"
SYNTH = os.path.join(REPO, "production", "qa", "evidence", "jaws-bite-sound", "candidates", "sfx_seeker_jaws_bite_synth.wav")


def main() -> int:
    out = os.path.abspath(sys.argv[1])
    cands = os.path.join(out, "candidates")
    os.makedirs(out, exist_ok=True)
    n = lambda k: os.path.join(SHOTS, k)   # noqa: E731
    film(n("normal"), f"{out}/01_before_synth_bite_sound.mp4", "BEFORE: the rejected synthesised bite", "--cue", f"{CUE}={SYNTH}")
    film(n("normal"), f"{out}/02_A_chomp_real_sound.mp4", "A CHOMP (in the game): real teeth + crunch tearing in, the weight lands, wet, cartilage")
    film(n("normal"), f"{out}/03_B_bone_real_sound.mp4", "B BONE: tight and hard, the weight early",
         "--cue", f"{CUE}={os.path.join(cands, 'sfx_seeker_jaws_bite_bone.wav')}")
    film(n("normal"), f"{out}/04_C_juicy_real_sound.mp4", "C JUICY: a soft ramp, the wettest",
         "--cue", f"{CUE}={os.path.join(cands, 'sfx_seeker_jaws_bite_juicy.wav')}")
    film(n("during_spray"), f"{out}/05_A_during_spray_sound.mp4", "A during SPRAY")
    film(n("during_hh"), f"{out}/06_A_during_hard_hands_sound.mp4", "A during HARD HANDS")
    film(n("fast_rearm"), f"{out}/07_A_repeated_fast_tempo_sound.mp4", "A repeated at fast TEMPO (one picture per 3 frames)")
    shutil.copy(os.path.join(COMBAT, CUE + ".wav"), os.path.join(out, "sfx_seeker_jaws_bite_A_chomp.wav"))
    shutil.copy(SYNTH, os.path.join(out, "sfx_seeker_jaws_bite_before_synth.wav"))
    print("done ->", out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
