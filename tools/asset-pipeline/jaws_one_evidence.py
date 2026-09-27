#!/usr/bin/env python3
"""jaws_one_evidence.py -- the JAWS one-piranha pass (ADR-011, 2026-09-27): eight items and the flash comparison, no
research battery. From build/shots/jaws/piranha/ (films_jaws.sh piranha normal flashA flashoff fast_rearm during_spray
during_hh) and build/shots/jaws/piranha_hero/ (the three-jaw hero chomp, the previous pass).

    PYTHONUTF8=1 python tools/asset-pipeline/jaws_one_evidence.py <out dir>
"""
import os
import sys

from PIL import Image, ImageDraw

from foundation_evidence import REPO, film, grid, sheet, run, TL, read

HERO = os.path.join(REPO, "build", "shots", "jaws", "piranha_hero")
NEW = os.path.join(REPO, "build", "shots", "jaws", "piranha")
PARTS = os.path.join(REPO, "assets", "art", "VFX", "parts")
CLOSE = "820,540,460,340"        # the empty space in front of the pack and its front creature, close


def state_sheet(pre, out, t0):
    """OPEN in negative space, approaching, HALF, SHUT, HOLD, exit: the piranha's display states off the take at 3x."""
    shots = read(pre + ".log")
    picks = [(-17, "before"), (0, "OPEN, staged outside the creature"), (17, "OPEN"), (34, "OPEN, approaching"), (50, "HALF, near contact"),
             (67, "SHUT: the chomp, the number"), (84, "HOLD"), (100, "HOLD"), (117, "HOLD"), (134, "HOLD ends"), (167, "exit, fading"), (200, "gone")]
    cells = []
    for dt, label in picks:
        i = min(shots, key=lambda q: abs(shots[q][1] - (t0 + dt)))
        im = Image.open(f"{pre}_{i:02d}.png").convert("RGB").crop((900, 600, 1120, 820)).resize((440, 440), Image.NEAREST)
        cells.append((f"{dt:+d} ms  {label}", im))
    w = 440
    img = Image.new("RGB", (len(cells) * (w + 6) + 6, w + 30 + 270), (20, 17, 14))
    d = ImageDraw.Draw(img)
    for k, (label, im) in enumerate(cells):
        d.text((6 + k * (w + 6), 6), label, fill=(235, 200, 120))
        img.paste(im, (6 + k * (w + 6), 24))
    for k, name in enumerate(("open", "half", "shut")):
        sp = Image.open(os.path.join(PARTS, f"fxp_seeker_jaws_{name}.png")).convert("RGBA").resize((64 * 6, 40 * 6), Image.NEAREST)
        bg = Image.new("RGBA", sp.size, (40, 36, 48, 255))
        bg.alpha_composite(sp)
        img.paste(bg.convert("RGB"), (6 + k * (64 * 6 + 10), w + 44))
        d.text((6 + k * (64 * 6 + 10), w + 30), f"source: {name.upper()}", fill=(235, 200, 120))
    img.save(out)


def main():
    out = os.path.abspath(sys.argv[1])
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png", ".md")) and f[0].isdigit():
            os.remove(os.path.join(out, f))
    h = lambda k: os.path.join(HERO, k)    # noqa: E731
    n = lambda k: os.path.join(NEW, k)     # noqa: E731
    film(h("normal"), f"{out}/01_current_three_jaw_hero_chomp_true_speed_sound.mp4", "CURRENT: the three-jaw hero chomp (the previous pass; the bite at 7000, which kills)")
    film(n("normal"), f"{out}/02b_one_piranha_the_killing_bite_true_speed_sound.mp4", "NEW: one piranha on the bite at 7000, which the answer kills")
    # items 2-5 use the bite at 5000, which the answer does NOT kill (at 7000 the front whelp dies of the reflection and
    # its death plume competes with the read); item 1 and the flash comparison stay on the 7000 bite
    film(n("normal_early"), f"{out}/02_one_piranha_true_speed_sound.mp4", "NEW: ONE Shadow piranha, staged in the empty space, reduced flash (the bite at 5000)")
    film(n("normal_early"), f"{out}/03_one_piranha_MUTED.mp4", "NEW: muted", "--mute")
    grid([n("normal_early")], ["the answer, close, true speed"], f"{out}/04_close_crop_true_speed_MUTED.mp4", CLOSE, shrink=1.0, lo=4900, hi=5400)
    state_sheet(n("normal_early"), f"{out}/05_OPEN_HALF_SHUT_HOLD_frame_sheet.png", 5017)
    sheet([n("normal_early")], ["one piranha, every frame"], f"{out}/05b_every_frame_1to1.png", 5017, -17, 220, (820, 560, 1300, 900), per_row=15,
          note="Every frame at play size: the open head staged outside the creature (0), moving in (17-50), half closed near contact (~50), SHUT on the front edge at 65 with the number and the reduced flash, held to ~130, a small recoil and fade, gone by 200.")
    film(n("fast_rearm"), f"{out}/06_repeated_triggers_fast_tempo_sound.mp4", "repeated triggers at fast TEMPO (one picture per 3 frames)")
    film(n("during_spray"), f"{out}/07_during_spray_sound.mp4", "JAWS answers at 1000 in SPRAY's wind-up, the knives out at 1150")
    film(n("during_hh"), f"{out}/08_during_hard_hands_sound.mp4", "JAWS answers at 13000 as HARD HANDS leaps at 13083")
    # the flash comparison: B (the build: 0.28 on the chomp) / A (the full F2 one frame after) / OFF (the control)
    grid([n("normal"), n("flashA"), n("flashoff")], ["B: 0.28 on the chomp (the build)", "A: F2 0.45 one frame after", "OFF: no flash"],
         f"{out}/09_flash_B_vs_A_vs_off_close_MUTED.mp4", CLOSE, shrink=0.8, lo=6980, hi=7250)
    sheet([n("normal"), n("flashA"), n("flashoff")], ["B: 0.28 on the chomp", "A: F2 a frame after", "OFF"], f"{out}/09b_flash_every_frame_1to1.png",
          7017, 50, 134, (880, 580, 1180, 840), per_row=6,
          note="The closed frames under each flash option: does the piranha stay visible when it closes?")
    open(f"{out}/10_trace.md", "w", encoding="utf-8").write(
        "# The bite and the answer on the playhead (bite_timeline.py), the bite at 7000\n\n" + run(TL, n("normal") + ".log", "--bite", "7017") + "\n")
    print("done ->", out)


if __name__ == "__main__":
    main()
