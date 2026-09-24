#!/usr/bin/env python3
"""press_field -- the PRESS field as a line-art pressure sigil, animated deterministically (2026-09-24).

    python tools/asset-pipeline/v2/press_field.py

THE RECTANGLE IT REPLACES. `fx_press` was a white 3D slab with four feet — a literal weight — and 24 % of
every frame was opaque. The field aura tints it and holds it behind the champion at 1.10x his height
(over its size budget at 1.44x), so in every fight with PRESS woven it stood behind him as a pulsing pink
box: the loudest thing on screen that was not the action being judged. The owner had already ruled that
PRESS is an abstract pressure field in the line-art language of the other fields (fx_aura's flame ring,
fx_wilt's drip ring), never a weight, box or machine.

WHAT THIS DOES. One pixen shape (job 0f584123, 1 generation): a white ring with a downward arrow and
chevrons pressing onto a bar. The motion is ours, not a generator's: the ring and the bar hold still and
the arrow group presses down 4 source px and back over the 8 frames (0,1,2,3,4,3,2,1 — a loop with no
repeated frame at the seam). Then the approved post-pass, exactly as fxclips.py runs it: whiten -> glow ->
soften (1 source px) -> feather.
"""
from __future__ import annotations

import io
import os
import subprocess
import sys
from collections import deque

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "assets", "art", "VFX", "press", "fx_press_strip8_512.png")
SHAPE_JOB = "0f584123-0b7f-4d75-917a-832e09229ede"
PRESS = [0, 1, 2, 3, 4, 3, 2, 1]          # the arrow group's downward offset per frame, in source px


def fetch(url: str) -> Image.Image:
    data = subprocess.run(["curl", "-s", "--retry", "3", url], check=True, capture_output=True).stdout
    return Image.open(io.BytesIO(data)).convert("RGBA")


def components(im: Image.Image) -> list[list[tuple[int, int]]]:
    w, h = im.size
    a = im.getchannel("A").load()
    seen = [[False] * w for _ in range(h)]
    comps = []
    for y in range(h):
        for x in range(w):
            if a[x, y] <= 8 or seen[y][x]:
                continue
            comp, q = [], deque([(x, y)]); seen[y][x] = True
            while q:
                cx, cy = q.popleft(); comp.append((cx, cy))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and a[nx, ny] > 8:
                            seen[ny][nx] = True; q.append((nx, ny))
            comps.append(comp)
    return sorted(comps, key=len, reverse=True)


def main() -> int:
    shape = fetch(f"https://api.pixellab.ai/mcp/images/{SHAPE_JOB}/download")
    comps = components(shape)
    ring = set(comps[0])
    inner = [c for c in comps[1:] if len(c) >= 12]
    # the BAR: the widest, flattest inner shape in the lower half — what is pressed, so it holds still
    def flatness(c):
        xs, ys = [p[0] for p in c], [p[1] for p in c]
        return (max(xs) - min(xs) + 1) / (max(ys) - min(ys) + 1), sum(ys) / len(ys)
    bar = max((c for c in inner if flatness(c)[1] > shape.height * 0.55), key=lambda c: flatness(c)[0], default=[])
    moving = [c for c in inner if c is not bar]
    print(f"ring {len(ring)} px, bar {len(bar)} px, pressing group {sum(map(len, moving))} px in {len(moving)} shapes")
    px = shape.load()
    strip = Image.new("RGBA", (512 * len(PRESS), 512))
    for i, d in enumerate(PRESS):
        f = Image.new("RGBA", shape.size)
        fp = f.load()
        for x, y in list(ring) + bar:
            fp[x, y] = px[x, y]
        for c in moving:
            for x, y in c:
                if 0 <= y + d < shape.height:
                    fp[x, y + d] = px[x, y]
        strip.paste(f.resize((512, 512), Image.NEAREST), (i * 512, 0))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    strip.save(OUT)
    radius = round(1.1 * 512 / shape.width)
    for step in (["rhart.py", "whiten", OUT], ["rhart.py", "glow", OUT],
                 ["rhart.py", "soften", "--radius", str(radius), OUT], ["feather_fx.py", OUT]):
        subprocess.run([sys.executable, os.path.join(HERE, step[0]), *step[1:]], check=True, capture_output=True)
    print(f"wrote {os.path.relpath(OUT, REPO)} (soften radius {radius})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
