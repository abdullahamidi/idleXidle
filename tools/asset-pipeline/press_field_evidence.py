#!/usr/bin/env python3
"""press_field_evidence.py -- PRESS, the first slice of the PERSISTENT FIELD / AURA reference (ADR-011, the owner's brief
2026-09-28, Concept A): "the Seeker's field pulses -> a wave goes out -> that enemy gets crushed". From
build/shots/press/<tag>/ (tools/asset-pipeline/films_press.sh <tag> normal repeated fast normal_off yield).

    PYTHONUTF8=1 python tools/asset-pipeline/press_field_evidence.py <tag> <out dir>
"""
import os
import sys

from PIL import Image, ImageDraw

from foundation_evidence import REPO, film, grid, read, SMALL

TICK = 6000                        # the PRESS tick filmed in `normal` (Core's Aura event; the front enemy crushed)
PHASES = [(-700, "A  REST"), (-560, "A  REST"), (-450, "B  COMPRESS"), (-360, "B  COMPRESS"), (-300, "C  EMIT"),
          (-233, "C  TRAVEL"), (-150, "C  TRAVEL"), (-67, "C  TRAVEL"), (0, "D  ARRIVE = TICK"), (33, "D  CRUSH"),
          (67, "D  CRUSH"), (117, "D  HOLD"), (183, "E  SETTLE"), (250, "E  SETTLE"), (330, "E  SETTLE"), (450, "A  REST")]
SHEET_BOX = (400, 420, 1440, 960)


def phase_sheet(rows, out, scale=0.62, per_row=8):
    bw, bh = int((SHEET_BOX[2] - SHEET_BOX[0]) * scale), int((SHEET_BOX[3] - SHEET_BOX[1]) * scale)
    lines = (len(PHASES) + per_row - 1) // per_row
    img = Image.new("RGB", (per_row * (bw + 4) + 4, len(rows) * lines * (bh + 22) + len(rows) * 24 + 8), (20, 17, 14))
    d = ImageDraw.Draw(img)
    y0 = 4
    for pre, label in rows:
        shots = read(pre + ".log")
        d.text((6, y0), label, fill=(245, 220, 150), font=SMALL)
        y0 += 22
        for k, (dt, name) in enumerate(PHASES):
            i = min(shots, key=lambda q: abs(shots[q][1] - (TICK + dt)))
            r, c = divmod(k, per_row)
            x, y = 4 + c * (bw + 4), y0 + r * (bh + 22)
            im = Image.open(f"{pre}_{i:02d}.png").convert("RGB").crop(SHEET_BOX).resize((bw, bh), Image.LANCZOS)
            d.text((x + 2, y), f"{shots[i][1] - TICK:+.0f} ms  {name}", fill=(235, 200, 120), font=SMALL)
            img.paste(im, (x, y + 20))
        y0 += lines * (bh + 22) + 2
    img.save(out)
    print(os.path.basename(out), img.size)


def main():
    tag, out = sys.argv[1], os.path.abspath(sys.argv[2])
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png", ".md")) and f[0].isdigit():
            os.remove(os.path.join(out, f))
    n = lambda k: os.path.join(REPO, "build", "shots", "press", tag, k)   # noqa: E731
    film(n("normal"), f"{out}/01_press_true_speed_sound.mp4", "PRESS: the field pulses, a wave goes out, the front enemy is crushed (tick at 6000)")
    film(n("normal"), f"{out}/02_press_MUTED.mp4", "PRESS, muted", "--mute")
    grid([n("normal")], ["PRESS, close: the Seeker, the front, the crush"], f"{out}/03_close_crop_true_speed_MUTED.mp4",
         "560,480,900,440", shrink=1.0, lo=5250, hi=6600)
    film(n("repeated"), f"{out}/04_repeated_ticks_sound.mp4", "repeated ticks (every 2 s; one picture per 2 frames): the target moves as creatures fall")
    film(n("yield"), f"{out}/04b_gives_way_to_jaws_true_speed_sound.mp4", "the tick at 10000 while JAWS bites the same creature: PRESS gives way (no arcs; the front flattens)")
    film(n("fast"), f"{out}/05_with_spray_sound.mp4", "fast TEMPO: SPRAY at 1400, then the PRESS tick at 2000", lo=880, hi=2500)
    film(n("fast"), f"{out}/06_with_hard_hands_sound.mp4", "fast TEMPO: HARD HANDS at 3600, then the PRESS tick at 4000 (SPRAY right after: quiet)", lo=3100, hi=4700)
    film(n("normal_off"), f"{out}/07_before_generic_aura_sound.mp4", "BEFORE: PRESS as the generic held aura (RH_FIELD_RECIPES=0)")
    phase_sheet([(n("normal"), "PRESS, the first slice: every phase around the tick at 6000"),
                 (n("normal_off"), "BEFORE: the generic held aura")],
                f"{out}/08_phase_sheet_REST_COMPRESS_EMIT_TRAVEL_CRUSH_SETTLE.png")
    lines = [l.rstrip("\n") for l in open(n("normal") + ".log", encoding="utf-8", errors="replace")
             if "\tfield-" in l or ("\tevent\t" in l and ("\tAura\t" in l or "\tBreak\t" in l))]
    draws = [l for l in lines if "field-draw" in l]
    open(f"{out}/09_trace.md", "w", encoding="utf-8").write(
        "# PRESS on the playhead (RH_PRESENT_TRACE), the normal take\n\n"
        "`field-wave` lists the wave's ticks (ms > the creature its Break names); `field-draw` is one line per frame of a "
        "phrase: `u` = ms from the tick, the target, the sprites of both passes, the target's squash (height), its drawn "
        "shape and layout body, and the bytes the field's draw allocated.\n\n```\n" + "\n".join(lines) + "\n```\n\n"
        + f"field-draw lines: {len(draws)}; allocation fields: " + ", ".join(sorted({l.split(chr(9))[-1] for l in draws})) + "\n")
    print("done ->", out)


if __name__ == "__main__":
    main()
