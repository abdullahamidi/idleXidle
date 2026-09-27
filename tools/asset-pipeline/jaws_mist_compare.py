#!/usr/bin/env python3
"""jaws_mist_compare.py -- one bite, two takes, the same frames side by side at 1.5x (the Shadow Fangs mist polish).

    PYTHONUTF8=1 python tools/asset-pipeline/jaws_mist_compare.py <out.png> <t0> <take_a> <label_a> <take_b> <label_b> [box]

Each take is a capture prefix (build/shots/jaws/<tag>/<name>); t0 is the playhead of the first frame that shows the
bite; box is x0,y0,x1,y1 in the 1920 x 1080 frame. Rows: the takes; columns: every frame from -17 to +220 ms.
"""
import os
import sys

from PIL import Image, ImageDraw

from foundation_evidence import read, SMALL

LABELS = [(0, "FADE-IN"), (17, "OPEN"), (33, "CLOSING"), (50, "CLOSING"), (67, "SNAP"), (83, "HOLD"), (100, "HOLD"),
          (117, "HOLD"), (133, "HOLD"), (150, "RELEASE"), (167, "RELEASE"), (183, "RELEASE"), (200, "MIST TAIL"), (217, "gone")]


def main():
    out, t0 = sys.argv[1], float(sys.argv[2])
    takes = [(sys.argv[3], sys.argv[4]), (sys.argv[5], sys.argv[6])] if len(sys.argv) > 6 else [(sys.argv[3], sys.argv[4])]
    box = tuple(map(int, sys.argv[7].split(","))) if len(sys.argv) > 7 else (1000, 620, 1340, 960)
    scale = 1.5
    bw, bh = int((box[2] - box[0]) * scale), int((box[3] - box[1]) * scale)
    cols = [(-17, "before")] + LABELS
    img = Image.new("RGB", (len(cols) * (bw + 4) + 4, len(takes) * (bh + 24) + 8), (20, 17, 14))
    d = ImageDraw.Draw(img)
    for r, (pre, label) in enumerate(takes):
        shots = read(pre + ".log")
        for c, (dt, name) in enumerate(cols):
            i = min(shots, key=lambda q: abs(shots[q][1] - (t0 + dt)))
            im = Image.open(f"{pre}_{i:02d}.png").convert("RGB").crop(box).resize((bw, bh), Image.LANCZOS)
            x, y = 4 + c * (bw + 4), 4 + r * (bh + 24)
            d.text((x, y), f"{shots[i][1] - t0:+.0f} {name}" + (f"  [{label}]" if c == 0 else ""), fill=(235, 200, 120), font=SMALL)
            img.paste(im, (x, y + 20))
    img.save(out)
    print(os.path.basename(out), img.size)


if __name__ == "__main__":
    main()
