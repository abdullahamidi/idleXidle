#!/usr/bin/env python3
"""quiver_arrow -- the Quiver's MISSILE: one hunting arrow (vfx sweep Phase 1 / P1.4; design.md section 6).

    python tools/asset-pipeline/v2/quiver_arrow.py            # write the two files
    python tools/asset-pipeline/v2/quiver_arrow.py --check    # prove the files on disk are this script's

Writes, under assets/art/Props/:
  prop_quiver_arrow.png       the MATERIAL: an ash shaft, a forged leaf head, grey goose vanes, bindings
  prop_quiver_arrow_edge.png  the EMISSIVE mask: the head's upper bevel and its tip, white, soft

DRAWN, NOT GENERATED (design.md section 6: procedural like seeker_knife.py): the arrow is a straight shaft, a head and
vanes, and every one of them is a decision about where light falls. Drawn in the hand-drawn smooth style
(props_smooth.py): polygons at 8 x, box-filtered, a soft near-black outline. Its length is ~68 texels at the Quiver's
draw scale, the arrow her strip nocks on the bow (frames 2-5).
"""
from __future__ import annotations

import sys

import props_smooth as ps

SIZE = 80
CY = 40.0
WOOD, WOOD_LIGHT, WOOD_DARK = (122, 84, 52, 255), (166, 124, 82, 255), (84, 56, 38, 255)
STEEL_D, STEEL, STEEL_L = (70, 76, 92, 255), (122, 134, 152, 255), (198, 210, 224, 255)
VANE, VANE_D, QUILL = (198, 190, 172, 255), (150, 142, 126, 255), (232, 226, 210, 255)
BIND = (62, 40, 34, 255)
WHITE = (255, 255, 255, 255)


def draw():
    c = ps.Canvas(SIZE)
    # the nock and the shaft (a light line along its top: the round wood catching light)
    c.poly([(6.5, CY - 1.6), (9.5, CY - 1.3), (9.5, CY + 1.3), (6.5, CY + 1.6)], WOOD_DARK)
    c.poly([(9, CY - 1.15), (62, CY - 1.15), (62, CY + 1.15), (9, CY + 1.15)], WOOD)
    c.poly([(9, CY - 1.15), (62, CY - 1.15), (62, CY - 0.35), (9, CY - 0.35)], WOOD_LIGHT)
    c.poly([(9, CY + 0.5), (62, CY + 0.5), (62, CY + 1.15), (9, CY + 1.15)], WOOD_DARK)
    # the vanes (one seen above the shaft, one below), a pale quill line on the near one
    c.poly([(10, CY - 0.6), (12.5, CY - 5.2), (21.5, CY - 3.0), (23.5, CY - 0.6)], VANE)
    c.poly([(10, CY + 0.6), (12.5, CY + 5.2), (21.5, CY + 3.0), (23.5, CY + 0.6)], VANE_D)
    c.poly([(12.5, CY - 5.2), (14.5, CY - 4.9), (22.5, CY - 1.2), (21.5, CY - 3.0)], QUILL)
    # bindings behind the vanes and behind the head
    for x in (9.2, 24.0, 58.2):
        c.poly([(x, CY - 1.5), (x + 1.6, CY - 1.5), (x + 1.6, CY + 1.5), (x, CY + 1.5)], BIND)
    # the head: a forged leaf, light above its spine, shade below
    head = [(59.5, CY - 1.5), (63.5, CY - 3.6), (69.0, CY - 2.2), (74.0, CY), (69.0, CY + 2.2), (63.5, CY + 3.6), (59.5, CY + 1.5)]
    c.poly(head, STEEL)
    c.poly([(60.5, CY - 1.3), (63.5, CY - 3.2), (69.0, CY - 1.9), (73.2, CY - 0.1), (60.5, CY - 0.2)], STEEL_L)
    c.poly([(60.5, CY + 0.6), (73.0, CY + 0.2), (69.0, CY + 2.0), (63.5, CY + 3.3), (60.5, CY + 1.4)], STEEL_D)
    material = ps.feathered(ps.down(ps.outlined(c)))
    e = ps.Canvas(SIZE)
    e.line([(62.0, CY - 2.7), (68.5, CY - 1.7), (73.4, CY - 0.1)], WHITE, 1.1)
    e.ellipse(73.2, CY, 0.9, 0.9, WHITE)
    # a faint sheen along the shaft's top, fading toward the vanes: the speed of the shaft, never a lit stick
    for i in range(6):
        x = 56.0 - i * 5.0
        e.line([(x - 5.0, CY - 1.0), (x, CY - 1.0)], (255, 255, 255, 150 - i * 22), 0.8)
    edge = ps.down(e.im, blur=1.0)
    return material, edge


if __name__ == "__main__":
    sys.exit(ps.main_for("prop_quiver_arrow", draw))
