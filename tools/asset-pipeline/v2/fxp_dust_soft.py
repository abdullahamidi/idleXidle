#!/usr/bin/env python3
"""fxp_dust_soft -- the untinted grey-brown DUST PUFF part (remaining-skill sweep, design.md section 6, P1.3).

    python tools/asset-pipeline/v2/fxp_dust_soft.py            (writes assets/art/VFX/parts/fxp_dust_soft.png)
    python tools/asset-pipeline/v2/fxp_dust_soft.py --check    (rebuilds in memory, exits 1 if the file differs)

A MATERIAL, not light: it is drawn alpha-blended and untinted (the Tower's hammer slam now; stone drops, fizzles and
UPSET's skirt later), so its colour is in the texture: a warm grey-brown, a little darker and browner low in the cloud
(the earth it was kicked from), paler at its lit top. Straight alpha; AssetLibrary premultiplies it at load.

DETERMINISTIC, NO PIXELLAB (design.md section 6: every new part is drawn on the game's material by a script): a cloud of
eleven soft lobes at fixed positions, broken by a fixed low-frequency value noise, under a radial envelope that reaches
zero 24 px inside the canvas, so check_fx_edges.py's EDGE (no alpha on the border ring) and SOFT (partial alpha, the
puff has no hard edge anywhere) both hold. Re-running it writes the same bytes.
"""
from __future__ import annotations

import math
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "assets", "art", "VFX", "parts", "fxp_dust_soft.png")

SIZE = 256
MARGIN = 24          # the envelope is zero this far inside the canvas (EDGE)
PEAK_ALPHA = 0.82    # dust is never a solid wall: the creature reads through its thinnest parts

# (x, y, radius, weight) in units of the canvas: a low, wide heap, its crown a little left of centre
LOBES = [
    (0.50, 0.56, 0.20, 1.00),
    (0.36, 0.60, 0.16, 0.85),
    (0.64, 0.60, 0.16, 0.85),
    (0.44, 0.44, 0.15, 0.80),
    (0.58, 0.46, 0.13, 0.70),
    (0.27, 0.66, 0.11, 0.60),
    (0.73, 0.66, 0.11, 0.60),
    (0.50, 0.34, 0.10, 0.55),
    (0.38, 0.36, 0.08, 0.45),
    (0.20, 0.58, 0.07, 0.40),
    (0.80, 0.56, 0.07, 0.40),
]

# the colour: warm grey-brown, darker low (kicked earth), paler on the lit top
TOP = np.array([176.0, 160.0, 140.0])
LOW = np.array([118.0, 98.0, 78.0])


def value_noise(size: int, cells: int, seed: int) -> np.ndarray:
    """A smooth 0..1 value noise: a fixed lattice of random values, bicubically smoothed (smoothstep) between them."""
    rng = np.random.RandomState(seed)
    lattice = rng.rand(cells + 1, cells + 1)
    t = np.linspace(0.0, cells, size, endpoint=False)
    i = np.floor(t).astype(int)
    f = t - i
    f = f * f * (3.0 - 2.0 * f)
    y0, x0 = np.meshgrid(i, i, indexing="ij")
    fy, fx = np.meshgrid(f, f, indexing="ij")
    a = lattice[y0, x0]
    b = lattice[y0, x0 + 1]
    c = lattice[y0 + 1, x0]
    d = lattice[y0 + 1, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def build() -> Image.Image:
    yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(np.float64)
    u = (xx + 0.5) / SIZE
    v = (yy + 0.5) / SIZE

    # the lobes: soft gaussians, combined as a soft union (1 - prod(1 - a)) so overlaps thicken without clipping
    cloud = np.ones((SIZE, SIZE))
    for (cx, cy, r, w) in LOBES:
        d2 = ((u - cx) ** 2 + (v - cy) ** 2) / (r * r)
        cloud *= 1.0 - w * np.exp(-1.6 * d2)
    cloud = 1.0 - cloud

    # broken by two octaves of fixed value noise: billows, not a disc
    noise = 0.65 * value_noise(SIZE, 6, 11) + 0.35 * value_noise(SIZE, 13, 23)
    cloud *= 0.55 + 0.75 * noise

    # the envelope: an ellipse (wider than tall, the puff hugs the ground) that is exactly zero MARGIN px from the border
    half = SIZE / 2.0
    ex = (xx + 0.5 - half) / (half - MARGIN)
    ey = (yy + 0.5 - half * 1.04) / ((half - MARGIN) * 0.86)
    r = np.sqrt(ex * ex + ey * ey)
    env = np.clip(1.0 - r, 0.0, 1.0)
    env = env * env * (3.0 - 2.0 * env)
    alpha = np.clip(cloud * env * 1.35, 0.0, 1.0) * PEAK_ALPHA

    # the colour, by height in the cloud and a touch of the noise (no flat fill)
    k = np.clip((v - 0.30) / 0.45, 0.0, 1.0)[..., None]
    rgb = TOP * (1.0 - k) + LOW * k
    rgb = rgb * (0.92 + 0.16 * noise[..., None])
    rgb = np.clip(rgb, 0.0, 255.0)

    out = np.zeros((SIZE, SIZE, 4), dtype=np.uint8)
    out[..., :3] = np.round(rgb).astype(np.uint8)
    out[..., 3] = np.round(alpha * 255.0).astype(np.uint8)
    out[out[..., 3] == 0, :3] = 0   # a clear texel carries no colour (clean premultiplication, no fringe)
    return Image.fromarray(out, "RGBA")


def main(argv: list[str]) -> int:
    im = build()
    if "--check" in argv:
        if not os.path.exists(OUT):
            print(f"fxp_dust_soft: {OUT} is missing")
            return 1
        same = np.array_equal(np.asarray(Image.open(OUT).convert("RGBA")), np.asarray(im))
        print("fxp_dust_soft: " + ("the file is the script's" if same else "the file DIFFERS from the script's"))
        return 0 if same else 1
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    im.save(OUT, optimize=True)
    a = np.asarray(im)[..., 3]
    border = max(int(a[0].max()), int(a[-1].max()), int(a[:, 0].max()), int(a[:, -1].max()))
    soft = float(((a > 8) & (a < 248)).mean())
    print(f"fxp_dust_soft: wrote {os.path.relpath(OUT, REPO)} {SIZE}x{SIZE}, peak alpha {int(a.max())}, "
          f"border alpha {border}, partial alpha {soft * 100:.1f}%")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
