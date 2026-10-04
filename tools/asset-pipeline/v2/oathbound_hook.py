#!/usr/bin/env python3
"""oathbound_hook -- the Oathbound's chain HOOK, at the end of his REACH (vfx sweep Phase 1 / P1.4; design.md 5.27).

    python tools/asset-pipeline/v2/oathbound_hook.py            # write the two files
    python tools/asset-pipeline/v2/oathbound_hook.py --check    # prove the files on disk are this script's

Writes, under assets/art/Props/:
  prop_oathbound_hook.png       the MATERIAL: a forged iron hook, an eye ring for the chain (left), a shank, a curve that
                                bends down and back to a barbed point (right); the chain body's iron (seeker_jaws.py)
  prop_oathbound_hook_edge.png  the EMISSIVE mask: the light along the curve's outer rim and the point, white, soft

DRAWN, NOT GENERATED (design.md section 6). The chain itself is `prop_seeker_chain_body` reused as a shared iron
material (design.md 5.27); the hook is the one new object, in the same iron, so the strand and its end are one thing.
Hand-drawn smooth (props_smooth.py): the curve is a swept, tapering stroke. ~34 x 20 texels.
"""
from __future__ import annotations

import math
import sys

import props_smooth as ps

SIZE = 48
DARK, IRON, LIGHT = (40, 42, 54, 255), (72, 78, 98, 255), (118, 128, 154, 255)
WHITE = (255, 255, 255, 255)
SHANK = 33                      # the spine's first SHANK points are the straight shank


def path():
    """The hook's spine: the shank (left to right), then the bend down and back, to the point."""
    pts = [(14.0 + i * 0.5, 22.0) for i in range(SHANK)]                    # the shank, 14 -> 30
    cx, cy, r = 30.0, 28.0, 6.0
    for k in range(1, 41):                                                  # -90 deg (top) -> +115 deg (down, back)
        a = math.radians(-90 + k * 205 / 40)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def sweep(c, pts, r0, r1, colour, dx=0.0, dy=0.0):
    """A stroke swept along `pts`: discs of radius r0, tapering to r1 along the bend only."""
    n = len(pts)
    for i, (x, y) in enumerate(pts):
        r = r0 + (r1 - r0) * max(0.0, (i - SHANK) / max(1, n - SHANK - 1))
        c.ellipse(x + dx, y + dy, r, r, colour)


def draw():
    c = ps.Canvas(SIZE)
    pts = path()
    # the eye ring the chain runs through
    c.ellipse(10.5, 22.0, 4.6, 4.6, IRON)
    c.ellipse(10.5, 22.0, 2.1, 2.1, (0, 0, 0, 0))
    c.d.arc([(10.5 - 4.2) * ps.SS, (22.0 - 4.2) * ps.SS, (10.5 + 4.2) * ps.SS, (22.0 + 4.2) * ps.SS], 190, 300,
            fill=LIGHT, width=int(1.1 * ps.SS))
    sweep(c, pts, 1.9, 0.55, IRON)
    sweep(c, pts, 1.1, 0.3, LIGHT, dx=-0.15, dy=-0.75)
    sweep(c, pts[:SHANK], 0.8, 0.8, DARK, dy=1.1)
    # the barb: a small spur inside the point
    tip = pts[-1]
    c.poly([(tip[0] + 0.4, tip[1] - 0.2), (tip[0] + 3.4, tip[1] - 2.6), (tip[0] + 2.4, tip[1] + 0.6)], IRON)
    material = ps.feathered(ps.down(ps.outlined(c)))
    e = ps.Canvas(SIZE)
    rim = [(30.0 + 6.8 * math.cos(math.radians(a)), 28.0 + 6.8 * math.sin(math.radians(a))) for a in range(-80, 50, 5)]
    e.line(rim, WHITE, 1.0)
    e.ellipse(tip[0], tip[1], 0.9, 0.9, WHITE)
    edge = ps.down(e.im, blur=1.0)
    return material, edge


if __name__ == "__main__":
    sys.exit(ps.main_for("prop_oathbound_hook", draw))
