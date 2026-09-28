#!/usr/bin/env python3
"""jaws_bite_evidence.py -- JAWS as a FRONTAL SHADOW BITE (ADR-011, the owner, 2026-09-28: "not a bad jaw biting from
the side: the earlier perspective, biting from the FRONT, but a more effective and beautiful jaw", with Roni
Kangaskorte's "Bite VFX" on ArtStation as the reference): the eight review items. From build/shots/jaws/bite4/
(films_jaws.sh bite4 normal fast_rearm during_spray during_hh) and build/shots/jaws/maw5/ (the rejected side-view maw,
for the comparison).

    PYTHONUTF8=1 python tools/asset-pipeline/jaws_bite_evidence.py <out dir>
"""
import os
import sys

from PIL import Image, ImageDraw

from foundation_evidence import REPO, film, grid, read, run, TL, SMALL

CURRENT = os.path.join(REPO, "build", "shots", "jaws", "maw5")
NEW = os.path.join(REPO, "build", "shots", "jaws", "bite4")
CLOSE = "940,540,480,420"       # the bitten whelp and the maw closing on it, close
T0 = 7017                        # the first frame that shows the bite at 7000 (the filmable JAWS trigger)

STAGES = [(-17, "before"), (0, "MIST APPEARS"), (33, "CONDENSING"), (67, "CONDENSING"), (100, "FANGS FORMED"),
          (167, "CHARGING"), (200, "WIND-UP"), (233, "WIND-UP"), (267, "CLOSING"), (300, "CLOSING"),
          (317, "SNAP"), (333, "IMPACT"), (350, "IMPACT"), (383, "BURST"), (433, "BURST"),
          (500, "FADING"), (567, "FADING"), (617, "SPLINTERS LAST"), (650, "SPLINTERS LAST"), (700, "gone")]


def stage_sheet(rows, out, box=(960, 540, 1400, 960), scale=1.25):
    """The phrase's stages, one row per take, labelled: MIST APPEARS, FANGS FORMED, CHARGING, WIND-UP, CLOSING, SNAP, IMPACT, BURST, FADING."""
    bw, bh = int((box[2] - box[0]) * scale), int((box[3] - box[1]) * scale)
    per_row = 10
    lines = (len(STAGES) + per_row - 1) // per_row
    img = Image.new("RGB", (per_row * (bw + 4) + 4, len(rows) * lines * (bh + 22) + len(rows) * 24 + 8), (20, 17, 14))
    d = ImageDraw.Draw(img)
    y0 = 4
    for pre, label in rows:
        shots = read(pre + ".log")
        d.text((6, y0), label, fill=(245, 220, 150), font=SMALL)
        y0 += 22
        for k, (dt, name) in enumerate(STAGES):
            i = min(shots, key=lambda q: abs(shots[q][1] - (T0 + dt)))
            r, c = divmod(k, per_row)
            x, y = 4 + c * (bw + 4), y0 + r * (bh + 22)
            im = Image.open(f"{pre}_{i:02d}.png").convert("RGB").crop(box).resize((bw, bh), Image.LANCZOS)
            d.text((x + 2, y), f"{shots[i][1] - T0:+.0f} ms  {name}", fill=(235, 200, 120), font=SMALL)
            img.paste(im, (x, y + 20))
        y0 += lines * (bh + 22) + 2
    img.save(out)
    print(os.path.basename(out), img.size)


def main():
    out = os.path.abspath(sys.argv[1])
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png", ".md")) and f[0].isdigit():
            os.remove(os.path.join(out, f))
    c = lambda k: os.path.join(CURRENT, k)   # noqa: E731
    n = lambda k: os.path.join(NEW, k)       # noqa: E731
    # the bite at 7000 in both: the whelp bites, JAWS answers, the answer kills the front whelp (its fall follows the snap)
    film(c("normal"), f"{out}/01_previous_side_view_maw_true_speed_sound.mp4", "PREVIOUS: the side-view maw (rejected)")
    film(n("normal"), f"{out}/02_frontal_bite_true_speed_sound.mp4", "NEW: a frontal Shadow bite: the fangs form, charge, slam shut and burst")
    film(n("normal"), f"{out}/03_frontal_bite_MUTED.mp4", "NEW: muted", "--mute")
    grid([n("normal")], ["frontal bite, close, true speed"], f"{out}/04_close_crop_true_speed_MUTED.mp4", CLOSE, shrink=1.0, lo=6900, hi=7750)
    stage_sheet([(n("normal"), "NEW: a frontal Shadow bite"), (c("normal"), "PREVIOUS: the side-view maw")],
                f"{out}/05_frame_sheet_FORM_CHARGE_WINDUP_CLOSE_SNAP_IMPACT_FADE.png")
    film(n("fast_rearm"), f"{out}/06_repeated_jaws_fast_tempo_sound.mp4", "repeated JAWS at fast TEMPO (one picture per 3 frames)")
    film(n("during_spray"), f"{out}/07_during_spray_sound.mp4", "JAWS answers at 1000 in SPRAY's wind-up, the knives out at 1150")
    film(n("during_hh"), f"{out}/08_during_hard_hands_sound.mp4", "JAWS answers at 13000 as HARD HANDS leaps at 13083")
    lines = [l.rstrip("\n") for l in open(n("normal") + ".log", encoding="utf-8", errors="replace")
             if "\treaction-" in l and ("contact=7000" in l or "reaction-draw" in l)]
    draws = [l for l in lines if "reaction-draw" in l]
    own = [l for l in lines if "reaction-draw" not in l]
    open(f"{out}/09_trace.md", "w", encoding="utf-8").write(
        "# The bite and the answer on the playhead (bite_timeline.py), the bite at 7000\n\n" + run(TL, n("normal") + ".log", "--bite", "7017") + "\n"
        + "\n## The reaction layer (RH_PRESENT_TRACE): `at=` is ms after the bite's ms; the first frame that shows the bite is +17\n\n```\n"
        + "\n".join(own) + "\n```\n\n" + f"reaction-draw lines: {len(draws)}; distinct cost fields: "
        + ", ".join(sorted({l.split(chr(9))[-1] for l in draws})) + "; sprites per frame (both passes: the rows, their glow and the impact's parts): "
        + ", ".join(sorted({l.split(chr(9))[5] for l in draws})) + "\n")
    print("done ->", out)


if __name__ == "__main__":
    main()
