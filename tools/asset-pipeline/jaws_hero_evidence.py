#!/usr/bin/env python3
"""jaws_hero_evidence.py -- the JAWS hero-chomp readability pass (ADR-011, 2026-09-27): nine items, no research battery.
From build/shots/jaws/piranha/ (the polished hero chomp: films_jaws.sh piranha normal fast_rearm during_spray during_hh)
and build/shots/jaws/piranha_v1/ (the first Shadow piranha, three near-equal jaws in ~140 ms).

    PYTHONUTF8=1 python tools/asset-pipeline/jaws_hero_evidence.py <out dir>
"""
import os
import shutil
import sys

from PIL import Image, ImageDraw

from foundation_evidence import REPO, ARENA, GRID, film, grid, sheet, run, TL, read

V1 = os.path.join(REPO, "build", "shots", "jaws", "piranha_v1")
NEW = os.path.join(REPO, "build", "shots", "jaws", "piranha")
PARTS = os.path.join(REPO, "assets", "art", "VFX", "parts")
CLOSE = "860,540,420,340"        # the front of the pack, close


def state_sheet(pre, out, t0):
    """OPEN -> HALF -> SHUT -> HOLD: the hero's display states off the take at 3x, with the three source states beside."""
    shots = read(pre + ".log")
    picks = [(-17, "before"), (0, "OPEN, arriving"), (17, "OPEN"), (34, "HALF"), (50, "SHUT: the chomp, the number, the flash"), (67, "HOLD"), (84, "HOLD, secondary A"), (100, "secondary B, dissolving"), (134, "gone")]
    cells = []
    for dt, label in picks:
        i = min(shots, key=lambda q: abs(shots[q][1] - (t0 + dt)))
        im = Image.open(f"{pre}_{i:02d}.png").convert("RGB").crop((930, 600, 1130, 800)).resize((400, 400), Image.NEAREST)
        cells.append((f"{dt:+d} ms  {label}", im))
    w = 400
    img = Image.new("RGB", (len(cells) * (w + 6) + 6, w + 30 + 260), (20, 17, 14))
    d = ImageDraw.Draw(img)
    for k, (label, im) in enumerate(cells):
        d.text((6 + k * (w + 6), 6), label, fill=(235, 200, 120))
        img.paste(im, (6 + k * (w + 6), 24))
    for k, name in enumerate(("open", "half", "shut")):
        sp = Image.open(os.path.join(PARTS, f"fxp_seeker_jaws_{name}.png")).convert("RGBA").resize((64 * 6, 40 * 6), Image.NEAREST)
        bg = Image.new("RGBA", sp.size, (40, 36, 48, 255))
        bg.alpha_composite(sp)
        img.paste(bg.convert("RGB"), (6 + k * (64 * 6 + 10), w + 40))
        d.text((6 + k * (64 * 6 + 10), w + 28), f"source: {name.upper()}", fill=(235, 200, 120))
    img.save(out)


def main():
    out = os.path.abspath(sys.argv[1])
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png", ".md")) and f[0].isdigit():
            os.remove(os.path.join(out, f))
    v1 = lambda k: os.path.join(V1, k)     # noqa: E731
    n = lambda k: os.path.join(NEW, k)     # noqa: E731
    film(v1("normal"), f"{out}/01_current_piranha_true_speed_sound.mp4", "CURRENT: the first Shadow piranha (three near-equal jaws, ~140 ms)")
    film(n("normal"), f"{out}/02_polished_hero_chomp_true_speed_sound.mp4", "POLISHED: the hero chomp (one hero, two small bites, the hold, ~180 ms)")
    film(n("normal"), f"{out}/03_polished_hero_chomp_MUTED.mp4", "POLISHED: muted", "--mute")
    grid([n("normal")], ["the answer, close, true speed"], f"{out}/04_close_crop_true_speed_MUTED.mp4", CLOSE, shrink=1.0, lo=6900, hi=7350)
    grid([n("normal")], ["the answer, close, 4x slower"], f"{out}/05_close_crop_slow_4x_MUTED.mp4", CLOSE, fps=15, shrink=1.0, lo=6950, hi=7250)
    state_sheet(n("normal"), f"{out}/06_OPEN_HALF_SHUT_HOLD_frame_sheet.png", 7017)
    sheet([v1("normal"), n("normal")], ["CURRENT", "POLISHED"], f"{out}/06b_every_frame_current_vs_polished_1to1.png", 7017, -17, 200, (860, 560, 1300, 900), per_row=14,
          note="Every frame at play size: the first piranha above (three near-equal jaws, over by 140), the hero chomp below (OPEN, HALF, SHUT at 48, HOLD to ~95, the secondaries at 70 and 90, gone by ~180).")
    film(n("fast_rearm"), f"{out}/07_repeated_triggers_fast_tempo_sound.mp4", "repeated triggers at fast TEMPO (one picture per 3 frames)")
    film(n("during_spray"), f"{out}/08_during_spray_sound.mp4", "JAWS answers at 1000 in SPRAY's wind-up, the knives out at 1150")
    film(n("during_hh"), f"{out}/09_during_hard_hands_sound.mp4", "JAWS answers at 13000 as HARD HANDS leaps at 13083")
    open(f"{out}/10_trace.md", "w", encoding="utf-8").write(
        "# The bite and the answer on the playhead (bite_timeline.py), the bite at 7000\n\n" + run(TL, n("normal") + ".log", "--bite", "7017") + "\n")
    print("done ->", out)


if __name__ == "__main__":
    main()
