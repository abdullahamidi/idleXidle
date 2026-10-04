#!/usr/bin/env python3
"""chorus_charm -- the Chorus's MISSILE: one bone charm (vfx sweep Phase 1 / P1.4; design.md 5.25, section 6).

    python tools/asset-pipeline/v2/chorus_charm.py            # write the two files
    python tools/asset-pipeline/v2/chorus_charm.py --check    # prove the files on disk are this script's

Writes, under assets/art/Props/:
  prop_chorus_charm.png       the MATERIAL: a short carved bone with knobbed ends, a cord bound round its middle, the
                              cord's small loop above it; ivory, untinted
  prop_chorus_charm_edge.png  the EMISSIVE mask: a faint light along the bone's upper edge, white, soft (drawn at a low
                              brightness: the charm is bone in the air, never glowing baked art)

THE SILHOUETTE IS GENERATED, THE ART IS DRAWN (design.md section 6: the charm is one of the three organic silhouettes).
  source:   PixelLab create_image_pixen, 64 x 64, no background, side view, single colour black outline, medium detail,
            seed 1404; ONE generation (no retry), 2026-10-04
  job id:   befc30a0-7b75-4953-9ef6-7d03dcfe2bc2   (Creator asset 6f8698ed-ff5e-5885-8f8e-828e7424b502)
  prompt:   "a small bone charm: a short carved bone with knobbed ends, a leather cord wrapped around its middle, side
            view, horizontal, pale ivory bone, no glow"
  result:   a dog-bone charm ~60 px long: two lobes per end (each lobe ~0.43 of the end's height), a shaft 0.17 of the
            length tall with a slight waist, a red cord wound round the middle third, and the cord's loop rising over it
            (~0.33 of the length wide). The generation itself is NOT shipped and not committed (it lived in build/tmp):
            this script redraws those proportions on the game's grid, in the hand-drawn smooth style (props_smooth.py),
            at ~36 texels (the charm in the Chorus's fingers on frame 4 of her attack strip), and in her palette (the
            bone of the charms at her belt, a dull red-brown cord, not the generation's saturated red).
"""
from __future__ import annotations

import sys

import props_smooth as ps

SIZE = 48
CY = 27.0
BONE_L, BONE, BONE_D = (232, 222, 194, 255), (204, 188, 156, 255), (150, 132, 104, 255)
CORD, CORD_D = (118, 60, 46, 255), (78, 38, 32, 255)
WHITE = (255, 255, 255, 255)
LEFT, RIGHT = 9.5, 38.5          # the knobs' centres along the bone (length ~35 texels)


def draw():
    c = ps.Canvas(SIZE)
    # the cord's loop over the middle (drawn first: the bone and the wrap sit in front of its foot)
    c.d.ellipse([(24.0 - 4.6) * ps.SS, (19.5 - 4.4) * ps.SS, (24.0 + 4.6) * ps.SS, (19.5 + 4.4) * ps.SS],
                outline=CORD_D, width=int(1.4 * ps.SS))
    # the shaft, a slight waist, and the four lobes (two per end), shaded: light above, shade below
    c.poly([(LEFT, CY - 2.7), (24.0, CY - 2.2), (RIGHT, CY - 2.7), (RIGHT, CY + 2.7), (24.0, CY + 2.2), (LEFT, CY + 2.7)], BONE)
    for x in (LEFT, RIGHT):
        c.ellipse(x, CY - 3.0, 3.4, 3.2, BONE)
        c.ellipse(x, CY + 3.0, 3.4, 3.2, BONE)
        c.ellipse(x - 0.4, CY - 3.6, 2.3, 2.0, BONE_L)
        c.ellipse(x + 0.3, CY + 4.0, 2.4, 1.6, BONE_D)
    c.poly([(LEFT + 2.0, CY - 2.4), (24.0, CY - 2.0), (RIGHT - 2.0, CY - 2.4), (RIGHT - 2.0, CY - 1.1), (LEFT + 2.0, CY - 1.1)], BONE_L)
    c.poly([(LEFT + 2.0, CY + 1.4), (RIGHT - 2.0, CY + 1.4), (RIGHT - 2.0, CY + 2.5), (24.0, CY + 2.0), (LEFT + 2.0, CY + 2.5)], BONE_D)
    # the wrap: four turns of cord round the middle
    for i in range(4):
        x = 20.4 + i * 2.0
        c.poly([(x, CY - 2.7), (x + 1.6, CY - 2.9), (x + 1.9, CY + 2.9), (x + 0.3, CY + 2.7)], CORD if i % 2 == 0 else CORD_D)
    material = ps.feathered(ps.down(ps.outlined(c)))
    e = ps.Canvas(SIZE)
    e.line([(LEFT - 1.5, CY - 5.6), (LEFT + 1.5, CY - 5.9)], WHITE, 0.9)
    e.line([(LEFT + 3.0, CY - 2.2), (19.5, CY - 2.0)], WHITE, 0.8)
    e.line([(28.5, CY - 2.0), (RIGHT - 3.0, CY - 2.2)], WHITE, 0.8)
    e.line([(RIGHT - 1.5, CY - 5.9), (RIGHT + 1.5, CY - 5.6)], WHITE, 0.9)
    edge = ps.down(e.im, blur=1.0)
    return material, edge


if __name__ == "__main__":
    sys.exit(ps.main_for("prop_chorus_charm", draw))
