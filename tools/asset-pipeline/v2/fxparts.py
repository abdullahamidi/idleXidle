#!/usr/bin/env python3
"""fxparts -- build the part textures a composite projectile is drawn from (ADR-010).

    python tools/asset-pipeline/v2/fxparts.py              (every part)
    python tools/asset-pipeline/v2/fxparts.py fxp_ring_soft (only the named parts; the drawn ones re-fetch)

Writes assets/art/VFX/parts/fxp_*.png. Every part is WHITE with honest straight alpha: the Source tint
colours it at play time and AssetLibrary premultiplies it at load, exactly like an effect strip.

TWO KINDS OF PART, AND WHO MAKES EACH.
  Drawn art comes from PixelLab through the same post-pass as every strip (whiten -> glow -> soften at ~1
  source pixel -> feather): the knife head (the approved cohort knife, pixen cd704393) and the glint
  (pixen a3b66867).
  Pure gradients are written here, because they are arithmetic, not art: the trail streak, the spark dot,
  the contact flash, the impact shard and the compression ring. pixen returns one-bit shapes; a soft falloff is what these parts
  exist to be. A pixen shard (566fab7e) was tried and rejected: it scattered opaque dither over its whole canvas.
"""
from __future__ import annotations

import io
import math
import os
import subprocess
import sys
import urllib.request

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "assets", "art", "VFX", "parts")
IMG = "https://api.pixellab.ai/mcp/images/{job}/download"

# key -> (pixen job, output size, soften radius in output px (~1.1 source px), feather band px)
DRAWN = {
    "fxp_seeker_knife_head": ("cd704393-43c4-4829-b752-a1e4767700ed", 512, 3, 48),
    "fxp_glint_star": ("a3b66867-ab11-4b70-a148-06f2605250f3", 128, 2, 12),
}


def smoothstep(x: float) -> float:
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


def white(size: tuple[int, int], alpha) -> Image.Image:
    """A white image whose alpha is alpha(x, y) in 0..1."""
    w, h = size
    im = Image.new("RGBA", size, (255, 255, 255, 0))
    px = im.load()
    for y in range(h):
        for x in range(w):
            a = max(0.0, min(1.0, alpha(x, y)))
            px[x, y] = (255, 255, 255, int(round(a * 255)))
    return im


def feather(im: Image.Image, band: int) -> Image.Image:
    """Ramp alpha to zero over the outer `band` pixels (what feather_fx.py does for a 512 frame)."""
    w, h = im.size
    px = im.load()
    for y in range(h):
        for x in range(w):
            d = min(x, y, w - 1 - x, h - 1 - y)
            if d < band:
                r, g, b, a = px[x, y]
                px[x, y] = (r, g, b, int(a * smoothstep(d / band)))
    return im


def drawn(key: str, job: str, size: int, radius: int, band: int) -> None:
    src = Image.open(io.BytesIO(urllib.request.urlopen(IMG.format(job=job), timeout=90).read())).convert("RGBA")
    im = src.resize((size, size), Image.NEAREST)          # the whole canvas, centred: its own framing
    out = os.path.join(OUT, f"{key}.png")
    im.save(out)
    for step in (["whiten", out], ["glow", out], ["soften", "--radius", str(radius), out]):
        subprocess.run([sys.executable, os.path.join(HERE, "rhart.py"), *step], check=True, capture_output=True)
    feather(Image.open(out).convert("RGBA"), band).save(out)


def procedural() -> dict:
    """The arithmetic parts: key -> a function that builds the image."""
    # THE TRAIL STREAK: soft across its height, uniform along its length, so segments laid end to end
    # between the projectile's recorded positions join without seams. The core and the wake share it.
    parts = {"fxp_trail_soft": lambda: white((64, 32), lambda x, y: math.exp(-((y - 15.5) / 6.5) ** 2)),
             # THE SPARK: a soft dot.
             "fxp_spark_dot": lambda: white((32, 32), lambda x, y: math.exp(-(((x - 15.5) ** 2 + (y - 15.5) ** 2) / 30.0))),
             # THE CONTACT FLASH: a round glow that falls off from a hot centre.
             "fxp_flash_soft": lambda: white((128, 128), lambda x, y: (1 - min(1.0, math.hypot(x - 63.5, y - 63.5) / 63.5)) ** 2.2)}
    # THE IMPACT SHARD: a sliver pointing +X, blunt at the rear and sharp at the tip, soft-edged.

    def sliver(x: float, y: float) -> float:
        t = x / 63.0                                   # 0 rear .. 1 tip
        half = 6.5 * (1.0 - t) ** 0.8 + 0.4            # the half-thickness narrows toward the tip
        edge = half - abs(y - 7.5)
        return smoothstep(edge / 1.6) * smoothstep(x / 4.0) * smoothstep((63 - x) / 1.5)
    parts["fxp_shard_sliver"] = lambda: white((64, 16), sliver)

    # THE COMPRESSION RING (HARD HANDS, 2026-09-25): a soft band, the air a blow shoves out of its way. The
    # performance squashes it along the force and grows it fast, so it reads as a shock, not a halo. A thin line
    # (the first cut, 3.6 px) read at play size as a hoop around the wrist; the band is wide and brightest on
    # its outer edge, with a faint fill inside so its centre never reads as a hole.
    def ring(x: float, y: float) -> float:
        r = math.hypot(x - 63.5, y - 63.5)
        band = math.exp(-((r - 50.0) / 8.5) ** 2) * (0.55 + 0.45 * smoothstep((r - 38.0) / 16.0))
        return min(1.0, band + 0.14 * smoothstep((50.0 - r) / 30.0) * smoothstep((r - 4.0) / 46.0)) * smoothstep((63.0 - r) / 5.0)
    parts["fxp_ring_soft"] = lambda: white((128, 128), ring)
    return parts


def main() -> int:
    os.makedirs(OUT, exist_ok=True)
    only = set(sys.argv[1:])
    for key, (job, size, radius, band) in DRAWN.items():
        if only and key not in only:
            continue
        drawn(key, job, size, radius, band)
        print(f"  {key:24} from pixen {job[:8]}  {size}x{size}")
    for key, build in procedural().items():
        if only and key not in only:
            continue
        build().save(os.path.join(OUT, f"{key}.png"))
        print(f"  {key:24} procedural")
    return 0


if __name__ == "__main__":
    sys.exit(main())
