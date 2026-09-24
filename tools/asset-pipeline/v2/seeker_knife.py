#!/usr/bin/env python3
"""seeker_knife -- the Seeker's canonical THROWING KNIFE, drawn pixel by pixel (the SPRAY slice, ADR-011).

    python tools/asset-pipeline/v2/seeker_knife.py

Writes, under assets/art/Props/:
  prop_seeker_throwing_knife.png       the MATERIAL: steel blade, leather-wrapped grip, ring pommel
  prop_seeker_throwing_knife_edge.png  the EMISSIVE mask: the blade's bevel and tip, white, for Source light

WHY DRAWN, NOT GENERATED. The knife is 30 source pixels long. At that size every pixel is a decision (where
the bevel catches light, where the wrap bands fall), and a generator returns a different knife each time.
It is authored in the Seeker's own palette (sampled from his idle frame: the outline, the belt-buckle steel,
the vest leather) and upscaled x3 NEAREST, the same pixel grid his strips are built on, so the knife in his
hand and the knife in the air are the same object at the same pixel density. Its length on screen is
30 x 3 x the hunter's draw scale (~1.10) = ~99 px, 0.24 of his visible height (the approved 0.20-0.25).
"""
from __future__ import annotations

import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "assets", "art", "Props"))
SCALE = 3          # the strip grid: one Seeker source pixel = 3 strip pixels

PAL = {
    "O": (12, 7, 15, 255),       # outline (his)
    "D": (97, 106, 142, 255),    # steel shade (belt buckle)
    "M": (131, 146, 168, 255),   # steel
    "L": (186, 205, 220, 255),   # steel light (buckle highlight)
    "H": (228, 238, 245, 255),   # bevel highlight
    "b": (63, 33, 34, 255),      # leather band
    "B": (97, 58, 42, 255),      # leather
    "W": (128, 86, 55, 255),     # leather light
    ".": (0, 0, 0, 0),
}
W, H = 30, 7
# THE SILHOUETTE, column by column: (half-height of the blade around row 3). A kunai-like throwing knife:
# a ring pommel, a thin wrapped grip, then a leaf blade that swells to five rows and runs to a point.
BLADE = {11: 1, 12: 1, 13: 1, 14: 2, 15: 2, 16: 2, 17: 2, 18: 2, 19: 2, 20: 2, 21: 2, 22: 1, 23: 1, 24: 1, 25: 1, 26: 1, 27: 1, 28: 0}
GRIP_CENTRE = (6.5, 3.0)   # where the hand holds it, in source px: the prop's pivot


def draw() -> tuple[list[list[str]], list[list[bool]]]:
    g = [["." for _ in range(W)] for _ in range(H)]
    edge = [[False] * W for _ in range(H)]
    # ring pommel: a 4x5 ring with a 2x1 hole
    for r in (2, 3, 4): g[r][0] = "O"; g[r][3] = "O"
    for c in (1, 2): g[1][c] = "O"; g[5][c] = "O"
    g[2][1], g[2][2], g[4][1], g[4][2] = "L", "M", "M", "D"
    # grip: one row of leather wrap between two outline rows, banded
    for c in range(4, 10):
        g[2][c] = "O"; g[4][c] = "O"
        g[3][c] = "b" if c % 2 == 0 else "W"
    g[3][10] = "O"; g[2][10] = "O"; g[4][10] = "O"      # the collar where the blade seats
    # blade: outline, a bright bevel under the top edge, a light spine, steel below, shade at the bottom
    for c, w in BLADE.items():
        top, bot = 3 - w, 3 + w
        if w == 0:
            g[3][c] = "H"; edge[3][c] = True
            g[3][c + 1] = "O"
            continue
        g[top][c] = "O"; g[bot][c] = "O"
        for r in range(top + 1, bot):
            if w == 2:
                g[r][c] = {2: "H", 3: "L", 4: "M"}[r]
            else:
                g[r][c] = "L" if c < 22 else "H"
            if (w == 2 and r == 2) or (w == 1 and c >= 22):
                edge[r][c] = True
    return g, edge


def main() -> int:
    os.makedirs(OUT, exist_ok=True)
    g, edge = draw()
    mat = Image.new("RGBA", (W, H))
    msk = Image.new("RGBA", (W, H))
    for r in range(H):
        for c in range(W):
            mat.putpixel((c, r), PAL[g[r][c]])
            if edge[r][c]:
                msk.putpixel((c, r), (255, 255, 255, 255))
    # one transparent source pixel of margin all round, so linear sampling of a rotated sprite never
    # smears the outline into its neighbour texel
    pad = lambda im: (lambda p: (p.paste(im, (1, 1)), p)[1])(Image.new("RGBA", (W + 2, H + 2)))
    mat, msk = pad(mat), pad(msk)
    mat.resize((mat.width * SCALE, mat.height * SCALE), Image.NEAREST).save(os.path.join(OUT, "prop_seeker_throwing_knife.png"))
    msk.resize((msk.width * SCALE, msk.height * SCALE), Image.NEAREST).save(os.path.join(OUT, "prop_seeker_throwing_knife_edge.png"))
    print(f"knife {W}x{H} source px -> {(W + 2) * SCALE}x{(H + 2) * SCALE} px; grip pivot at "
          f"({(GRIP_CENTRE[0] + 1) * SCALE:.1f}, {(GRIP_CENTRE[1] + 1) * SCALE:.1f}); length {W * SCALE} px before the hunter's draw scale")
    return 0


if __name__ == "__main__":
    sys.exit(main())
