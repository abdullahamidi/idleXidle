"""Feather the frame edges of every effect strip so nothing cuts hard at the frame border.

Playtest 2026-08-23 ("efekt bir dikdörtgen şeklinde çıkıp kaybolduğu çok belli oluyor"): several
effect frames carry light right up to the 512-px frame edge (a burst's rays, a pillar's base, a
beam's tail). Under additive blending that edge is a visible straight line on the dark floor. This
multiplies each frame's alpha by a smoothstep ramp over the outer FEATHER pixels of all four sides,
so every effect fades into nothing before the frame ends. Idempotent enough to re-run (a second pass
only softens the ramp band again by the same curve — run it once per generated strip).

Usage:  python tools/asset-pipeline/v2/feather_fx.py [--feather 48] [paths...]
        (no paths = every assets/art/VFX/*/fx_*_strip8_512.png)
"""
import argparse, glob, os, sys
from PIL import Image

FRAME = 512


def ramp(d: int, width: int) -> float:
    """0 at the very edge → 1 at `width` px inside, smoothstep."""
    if d >= width:
        return 1.0
    t = max(0.0, d / width)
    return t * t * (3 - 2 * t)


def feather(path: str, width: int, out: str | None = None) -> str:
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    frames = w // FRAME
    a = im.getchannel("A")
    px = a.load()
    # Precompute the per-pixel factor for one frame (symmetric), then apply to every frame.
    fac = [[0.0] * FRAME for _ in range(FRAME)]
    for y in range(FRAME):
        ry = ramp(min(y, FRAME - 1 - y), width)
        row = fac[y]
        for x in range(FRAME):
            row[x] = ry * ramp(min(x, FRAME - 1 - x), width)
    for f in range(frames):
        ox = f * FRAME
        for y in range(min(FRAME, h)):
            row = fac[y]
            for x in range(FRAME):
                v = px[ox + x, y]
                if v:
                    px[ox + x, y] = int(v * row[x] + 0.5)
    im.putalpha(a)
    dest = out or path
    im.save(dest)
    return dest


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--feather", type=int, default=48)
    ap.add_argument("paths", nargs="*")
    a = ap.parse_args()
    paths = a.paths or sorted(glob.glob("assets/art/VFX/*/fx_*_strip8_512.png"))
    if not paths:
        print("no strips found", file=sys.stderr)
        return 1
    for p in paths:
        print("feathered", feather(p, a.feather))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
