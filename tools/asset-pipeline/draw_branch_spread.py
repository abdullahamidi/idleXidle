#!/usr/bin/env python3
"""Author `icon_branch_spread` — the one branch glyph a model would not draw.

Usage:
    python3 tools/asset-pipeline/draw_branch_spread.py

Weight, Tempo and Endure each came back right on the first generation. Spread took
six and never arrived: a cluster of shafts meeting at a point is a TRIDENT, splaying
them wide is a BIRD, nested arcs fill in solid and become a MOON, and the last two
attempts returned a trident embossed on a SHIELD — which is Endure's glyph, the worst
collision available — and then a bare corner bracket. The shape "one line that
becomes three" has no name of its own in the model's vocabulary, so every description
of it landed on the nearest thing that does.

Six rolls is the signal that the asset is a FORMULA, not a picture. A fork is four
line segments and three triangles; drawing it directly is exact, free, and repeatable,
and it is the same call the shockwave ring made in PrestigeScreen.Ring.

The key is deliberately ABSENT from manifest.json for the reason the cut rig parts
are: a `--force` run would otherwise overwrite this with a seventh invented shape.
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from pixelpng import Image, write  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(REPO, "assets", "art", "UI", "icons", "branches", "icon_branch_spread.png")

SIZE = 192
SS = 4          # supersample factor — coverage at 4x downsamples to clean edges
# The same pale bone the generated glyphs came back in, so the four sit as one set.
# They are all tinted at the draw site anyway; what has to match is the VALUE.
INK = (0xED, 0xE6, 0xD2)

# One shaft rising from the bottom, splitting into three forks. Coordinates are in
# 192-space and describe the finished silhouette, not a canvas: nothing re-centres the
# glyph afterwards, so it must already be centred here.
#
# The outer forks lean 32 degrees off vertical, not 45. At 45 the glyph read as a stick
# figure with its arms out — the fan has to open UPWARD to say "one thing became three"
# rather than sideways, where the eye finds a torso.
SPLIT = (96.0, 118.0)          # where the shaft divides
SHAFT_FOOT = (96.0, 178.0)
FORK_TIPS = [(40.0, 30.0), (96.0, 14.0), (152.0, 30.0)]
STROKE = 13.0                  # half-width is what the distance test uses
HEAD = 25.0                    # arrowhead half-width across its base
HEAD_LEN = 34.0                # how far back from the tip the base sits


def seg_distance(px: float, py: float, ax: float, ay: float, bx: float, by: float) -> float:
    """Distance from a point to a line SEGMENT (not the infinite line)."""
    dx, dy = bx - ax, by - ay
    span = dx * dx + dy * dy
    t = 0.0 if span == 0 else ((px - ax) * dx + (py - ay) * dy) / span
    t = max(0.0, min(1.0, t))
    cx, cy = ax + t * dx, ay + t * dy
    return ((px - cx) ** 2 + (py - cy) ** 2) ** 0.5


def in_triangle(px, py, a, b, c) -> bool:
    def side(p, q, r):
        return (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])
    d1, d2, d3 = side((px, py), a, b), side((px, py), b, c), side((px, py), c, a)
    return not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0))


def unit(tip: tuple[float, float]) -> tuple[float, float]:
    dx, dy = tip[0] - SPLIT[0], tip[1] - SPLIT[1]
    length = (dx * dx + dy * dy) ** 0.5
    return dx / length, dy / length


def arrowhead(tip: tuple[float, float]) -> tuple:
    """A solid triangle at the tip, pointing along the fork it caps."""
    ux, uy = unit(tip)
    bx, by = tip[0] - ux * HEAD_LEN, tip[1] - uy * HEAD_LEN
    return tip, (bx - uy * HEAD, by + ux * HEAD), (bx + uy * HEAD, by - ux * HEAD)


def covered(px: float, py: float) -> bool:
    if seg_distance(px, py, *SHAFT_FOOT, *SPLIT) <= STROKE:
        return True
    for tip in FORK_TIPS:
        # The stroke stops SHORT of the tip. Run it all the way and its round cap bulges
        # a stroke-radius past the arrowhead's apex, so every fork ends in a blunt nose
        # with the triangle's corners showing as barbs behind it.
        ux, uy = unit(tip)
        stop = (tip[0] - ux * HEAD_LEN * 0.75, tip[1] - uy * HEAD_LEN * 0.75)
        if seg_distance(px, py, *SPLIT, *stop) <= STROKE:
            return True
        if in_triangle(px, py, *arrowhead(tip)):
            return True
    return False


def main() -> None:
    img = Image(SIZE, SIZE)
    step = 1.0 / SS
    for y in range(SIZE):
        for x in range(SIZE):
            hits = 0
            for sy in range(SS):
                for sx in range(SS):
                    if covered(x + (sx + 0.5) * step, y + (sy + 0.5) * step):
                        hits += 1
            if hits:
                img.set(x, y, INK[0], INK[1], INK[2], round(255 * hits / (SS * SS)))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    write(OUT, img)

    opaque = sum(1 for y in range(SIZE) for x in range(SIZE) if img.get(x, y)[3] > 0)
    print(f"{OUT}  {SIZE}x{SIZE}  {100 * (1 - opaque / (SIZE * SIZE)):.1f}% clear")


if __name__ == "__main__":
    main()
