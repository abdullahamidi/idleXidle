#!/usr/bin/env python3
"""unbroken_chip -- the Unbroken's MISSILE: a flung stone chip (vfx sweep Phase 1 / P1.4; design.md 5.24, section 6).

    python tools/asset-pipeline/v2/unbroken_chip.py            # write the two files
    python tools/asset-pipeline/v2/unbroken_chip.py --check    # prove the files on disk are this script's

Writes, under assets/art/Props/:
  prop_unbroken_chip.png       the MATERIAL: a split warm-grey wedge, a lit top face, a shaded underside, two cracks
  prop_unbroken_chip_edge.png  the EMISSIVE mask: the fracture ridge along its top, white, soft

DRAWN, NOT GENERATED (design.md section 6): three faces and two cracks. A wall champion does not lunge; he flings a
piece of the ground he holds (DECIDED in design.md 5.24), so the chip is stone and never glows (the orange glow of the
old strip was cleaned out in P1.1). Hand-drawn smooth (props_smooth.py), ~38 x 25 texels.
"""
from __future__ import annotations

import sys

import props_smooth as ps

SIZE = 56
GROW = 1.4                      # drawn on a 40-texel sketch, grown 1.4x about its middle (filmed at 1.0 it read as a pebble)


def g(pts):
    """The sketch's points, grown about (20, 20) and centred on the canvas."""
    return [(SIZE / 2 + (x - 20.0) * GROW, SIZE / 2 + (y - 20.0) * GROW) for x, y in pts]
TOP, SIDE, UNDER = (158, 150, 136, 255), (114, 106, 96, 255), (72, 66, 62, 255)
CRACK = (46, 40, 40, 255)
WHITE = (255, 255, 255, 255)


def draw():
    c = ps.Canvas(SIZE)
    outline = [(7.0, 21.5), (10.5, 15.0), (18.5, 12.5), (27.5, 13.5), (32.5, 18.0), (31.0, 24.5), (24.0, 28.0), (13.0, 27.5)]
    c.poly(g(outline), SIDE)
    # the lit top face (the fracture plane) and the shaded underside
    c.poly(g([(9.0, 19.5), (10.5, 15.0), (18.5, 12.5), (27.5, 13.5), (32.0, 17.8), (24.0, 19.0), (15.0, 20.5)]), TOP)
    c.poly(g([(9.5, 24.0), (16.0, 23.6), (24.5, 23.0), (31.2, 22.5), (31.0, 24.5), (24.0, 28.0), (13.0, 27.5)]), UNDER)
    # two cracks
    c.line(g([(16.0, 14.0), (17.5, 17.0), (16.5, 19.5)]), CRACK, 0.9)
    c.line(g([(26.0, 19.0), (27.5, 21.5), (26.5, 24.5)]), CRACK, 0.8)
    material = ps.feathered(ps.down(ps.outlined(c)))
    e = ps.Canvas(SIZE)
    e.line(g([(10.8, 15.2), (18.5, 12.9), (27.4, 13.9), (32.0, 17.9)]), WHITE, 1.3)
    edge = ps.down(e.im, blur=1.0)
    return material, edge


if __name__ == "__main__":
    sys.exit(ps.main_for("prop_unbroken_chip", draw))
