#!/usr/bin/env python3
"""filmstrip — lay a capture sequence out as one sheet, cropped to the arena.

    python tools/asset-pipeline/v2/filmstrip.py <outprefix> <frame_00.png> <frame_01.png> ...
        [--crop x,y,w,h] [--cols 4] [--scale 0.5] [--label "100 ms"]

Default crop is the HUNT arena (HuntScreen.ArenaRect = 492,100,1062,940) plus the HUD
strip above it so the skill rail and the boss bar stay in frame. Each cell is numbered; with a
stride of 6 frames at 60 Hz the numbers read as tenths of a second.
"""

from __future__ import annotations

import argparse
import os
import sys

from PIL import Image, ImageDraw


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(prog="filmstrip")
    ap.add_argument("outprefix")
    ap.add_argument("frames", nargs="+")
    ap.add_argument("--crop", default="480,20,1090,1040")
    ap.add_argument("--cols", type=int, default=4)
    ap.add_argument("--scale", type=float, default=0.45)
    ap.add_argument("--label", default="x100ms")
    a = ap.parse_args(argv)

    x, y, w, h = (int(v) for v in a.crop.split(","))
    cw, ch = int(w * a.scale), int(h * a.scale)
    frames = sorted(a.frames)
    rows = (len(frames) + a.cols - 1) // a.cols
    sheet = Image.new("RGB", (a.cols * cw, rows * ch), (18, 14, 22))
    d = ImageDraw.Draw(sheet)
    for i, f in enumerate(frames):
        im = Image.open(f).convert("RGB").crop((x, y, x + w, y + h)).resize((cw, ch), Image.BILINEAR)
        ox, oy = (i % a.cols) * cw, (i // a.cols) * ch
        sheet.paste(im, (ox, oy))
        d.rectangle([ox, oy, ox + 44, oy + 20], fill=(0, 0, 0))
        d.text((ox + 4, oy + 4), f"{i:02d} {a.label}", fill=(240, 220, 160))
        d.line([(ox + cw - 1, oy), (ox + cw - 1, oy + ch)], fill=(60, 50, 70))
        d.line([(ox, oy + ch - 1), (ox + cw, oy + ch - 1)], fill=(60, 50, 70))
    out = a.outprefix + "_strip.png"
    sheet.save(out)
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
