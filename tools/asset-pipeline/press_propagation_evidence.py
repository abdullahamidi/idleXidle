#!/usr/bin/env python3
"""press_propagation_evidence.py -- PRESS, the FIELD-vs-PROJECTILE polish (ADR-011, the owner's brief 2026-09-28: the
pixel-hard direction is approved; the one open question is whether the travelling front reads as a PROJECTILE). The
review's items: A the current pixel-hard PRESS, B the field-propagation polish, C both muted, D the five-frame sequence
(field release, wave travel, arrival, vertical crush, hold) and the contact frame by frame, E repeated ticks, F SPRAY in
the same fight, side by side with PRESS's travel.

    bash tools/asset-pipeline/films_press.sh <tag> normal repeated fast yield
    PYTHONUTF8=1 python tools/asset-pipeline/press_propagation_evidence.py <current tag> <tag> <out dir> [<contours tag>]

The launch contours were tried (a take with them and one without, RH_PRESS_CONTOURS=0, under <contours tag>) and removed on
the judges' word: invisible at play speed, speed dashes on a still; items 08-09 keep that record.
"""
import os
import sys

from PIL import Image, ImageDraw

from foundation_evidence import REPO, SMALL, film, grid, read

TICK = 6000
SEQUENCE = [(-267, "FIELD RELEASE"), (-133, "WAVE TRAVEL"), (0, "ARRIVAL"), (33, "VERTICAL CRUSH"), (67, "HOLD")]
CONTACT = [(-17, ""), (0, "ARRIVAL"), (17, "THE FOLD"), (33, "SHUT"), (50, "")]
LAUNCH = [(-217, ""), (-200, ""), (-183, "CONTOURS"), (-167, ""), (-150, ""), (-133, "")]
WIDE = (560, 470, 1440, 990)
CLOSE = (880, 560, 1360, 960)


def frame_at(pre, t):
    shots = read(pre + ".log")
    i = min(shots, key=lambda q: abs(shots[q][1] - t))
    return Image.open(f"{pre}_{i:02d}.png").convert("RGB"), shots[i][1]


def strip(rows, cols, out, box, tick, scale, title=""):
    """rows: [(prefix, label)]; cols: [(ms from the tick, name)]."""
    bw, bh = int((box[2] - box[0]) * scale), int((box[3] - box[1]) * scale)
    top = 24 if title else 0
    img = Image.new("RGB", (len(cols) * (bw + 4) + 4, top + len(rows) * (bh + 44) + 8), (20, 17, 14))
    d = ImageDraw.Draw(img)
    if title:
        d.text((6, 4), title, fill=(245, 220, 150), font=SMALL)
    for r, (pre, label) in enumerate(rows):
        y0 = top + 4 + r * (bh + 44)
        d.text((6, y0), label, fill=(245, 220, 150), font=SMALL)
        for c, (dt, name) in enumerate(cols):
            im, t = frame_at(pre, tick + dt)
            x = 4 + c * (bw + 4)
            d.text((x + 2, y0 + 20), f"{t - tick:+.0f} ms  {name}", fill=(235, 200, 120), font=SMALL)
            img.paste(im.crop(box).resize((bw, bh), Image.LANCZOS if scale < 1 else Image.NEAREST), (x, y0 + 40))
    img.save(out)
    print(os.path.basename(out), img.size)


def spray_vs_press(pre, out):
    """In the SAME fight (the fast take): SPRAY's volley in flight against PRESS's front on its way, the same crop, frames
    a similar time apart -- a thrown object against a wall propagating through the arena."""
    box = (520, 420, 1460, 980)
    rows = [("SPRAY: thrown daggers (a projectile volley), fast TEMPO 1183-1400", 1400, [-217, -167, -117, -83, -50, -17, 0]),
            ("PRESS: the field's front (a pressure wall), the tick at 2000", 2000, [-283, -217, -150, -100, -50, -17, 0])]
    scale = 0.42
    bw, bh = int((box[2] - box[0]) * scale), int((box[3] - box[1]) * scale)
    img = Image.new("RGB", (7 * (bw + 4) + 4, len(rows) * (bh + 44) + 8), (20, 17, 14))
    d = ImageDraw.Draw(img)
    for r, (label, tick, dts) in enumerate(rows):
        y0 = 4 + r * (bh + 44)
        d.text((6, y0), label, fill=(245, 220, 150), font=SMALL)
        for c, dt in enumerate(dts):
            im, t = frame_at(pre, tick + dt)
            x = 4 + c * (bw + 4)
            d.text((x + 2, y0 + 20), f"t={t:.0f}", fill=(235, 200, 120), font=SMALL)
            img.paste(im.crop(box).resize((bw, bh), Image.LANCZOS), (x, y0 + 40))
    img.save(out)
    print(os.path.basename(out), img.size)


def main():
    cur, tag, out = sys.argv[1], sys.argv[2], os.path.abspath(sys.argv[3])
    tried = sys.argv[4] if len(sys.argv) > 4 else None
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png", ".md")) and f[0].isdigit():
            os.remove(os.path.join(out, f))
    c = lambda k: os.path.join(REPO, "build", "shots", "press", cur, k)   # noqa: E731
    n = lambda k: os.path.join(REPO, "build", "shots", "press", tag, k)   # noqa: E731
    film(c("normal"), f"{out}/01_A_current_pixel_hard_true_speed_sound.mp4", "A: the current pixel-hard PRESS (approved)")
    film(n("normal"), f"{out}/02_B_field_propagation_true_speed_sound.mp4", "B: the field-propagation polish")
    film(c("normal"), f"{out}/03_C_current_MUTED.mp4", "A, muted", "--mute")
    film(n("normal"), f"{out}/04_C_field_propagation_MUTED.mp4", "B, muted", "--mute")
    grid([c("normal"), n("normal")], ["A: current", "B: field propagation"], f"{out}/05_C_side_by_side_close_MUTED.mp4",
         "560,470,880,500", shrink=0.8, lo=5600, hi=6450)
    strip([(c("normal"), "A: the current pixel-hard PRESS"), (n("normal"), "B: the field-propagation polish")], SEQUENCE,
          f"{out}/06_D_sequence_RELEASE_TRAVEL_ARRIVAL_VERTICAL_CRUSH_HOLD.png", WIDE, TICK, 0.5)
    strip([(c("normal"), "A: current (the front fades at the target while two arcs appear)"),
           (n("normal"), "B: the front stops, folds over and under the whelp, and becomes the arcs")], CONTACT,
          f"{out}/07_D_contact_frame_by_frame_2x.png", CLOSE, TICK, 1.0)
    if tried:
        t = lambda k: os.path.join(REPO, "build", "shots", "press", tried, k)   # noqa: E731
        strip([(t("normal"), "TRIED, REMOVED: the launch contours (the field's boundary ringing as the front departs, ~70 ms)"),
               (t("normal_nc"), "without them (the pass)")], LAUNCH, f"{out}/08_launch_contours_tried_and_removed.png", (440, 470, 1140, 990), TICK, 0.6)
        grid([t("normal"), t("normal_nc")], ["tried: the launch contours", "without (the pass)"], f"{out}/09_launch_contours_tried_and_removed_MUTED.mp4",
             "440,470,700,500", shrink=0.8, lo=5550, hi=5950)
    film(n("repeated"), f"{out}/10_E_repeated_ticks_sound.mp4", "B: repeated ticks (one normal, one quiet near an action, one giving way to JAWS)")
    spray_vs_press(n("fast"), f"{out}/11_F_spray_vs_press_same_fight.png")
    film(n("fast"), f"{out}/12_F_with_spray_sound.mp4", "B: fast TEMPO, SPRAY at 1400, then the PRESS tick at 2000", lo=880, hi=2500)
    film(n("fast"), f"{out}/13_with_hard_hands_sound.mp4", "B: HARD HANDS at 3600, then the quiet tick at 4000", lo=3100, hi=4700)
    film(n("yield"), f"{out}/14_with_jaws_true_speed_sound.mp4", "B: the tick at 10000 while JAWS bites the same creature: PRESS gives way")
    lines = [l.rstrip("\n") for l in open(n("normal") + ".log", encoding="utf-8", errors="replace") if "\tfield-" in l]
    draws = [l for l in lines if "field-draw" in l]
    open(f"{out}/15_trace.md", "w", encoding="utf-8").write(
        "# PRESS on the playhead (RH_PRESENT_TRACE), the field-propagation normal take\n\n```\n" + "\n".join(lines) + "\n```\n\n"
        + f"field-draw lines: {len(draws)}; allocation fields: " + ", ".join(sorted({l.split(chr(9))[-1] for l in draws})) + "\n")
    print("done ->", out)


if __name__ == "__main__":
    main()
