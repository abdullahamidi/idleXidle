#!/usr/bin/env python3
"""seeker_piranha.py -- the Seeker's JAWS source art: ONE tiny Shadow piranha head in THREE states (the production
direction, 2026-09-27: SHADOW PIRANHA retaliation; the hero-chomp readability pass added the HALF state and the
contrast lift).

    python tools/asset-pipeline/v2/seeker_piranha.py

Source: two PixelLab Pro Flash images (96 x 64, `create_image_pro_flash` seed 7 for the OPEN head, then one
`edit_image_pro_flash` of it with the mouth SHUT), kept under keypose_sources/ as seeker_piranha_open_src.png and
seeker_piranha_shut_src.png. This script:

  1. cuts both to one 64 x 40 canvas at the same offset (OPEN and SHUT stay registered on the mouth);
  2. builds the HALF-CLOSED state DETERMINISTICALLY from the two: the silhouettes' signed distance fields are
     averaged and re-thresholded (a true mid-shape between open and shut, no pumping, no redraw), and each pixel's
     colour is taken from whichever source is nearer at that point (the teeth and the rim stay the same material);
  3. lifts the contrast of the INFORMATION-BEARING parts only, by ~18 %: the violet rim and eye, and the pale teeth.
     The body stays dark Shadow;
  4. writes the runtime parts:

    assets/art/VFX/parts/fxp_seeker_jaws_open.png     the head, mouth open, facing LEFT, the smear trailing right
    assets/art/VFX/parts/fxp_seeker_jaws_half.png     the same head, mouth half closed
    assets/art/VFX/parts/fxp_seeker_jaws_shut.png     the same head, teeth met

The runtime (ReactionPerformance) owns position, rotation about the mouth point, scale, timing and fade; the art owns
only the states. MouthPoint in ReactionRecipe is (7, 24) on this canvas: the front of the mouth on the seam.
"""
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SRC = os.path.join(HERE, "keypose_sources")
OUT = os.path.join(REPO, "assets", "art", "VFX", "parts")
CROP = (20, 12, 84, 52)      # the same window off both 96 x 64 sources: a 64 x 40 canvas, the head at x 5..54, y 4..35
LIFT = 1.18                  # the contrast lift on the rim, the eye and the teeth


def cut(name):
    im = np.asarray(Image.open(os.path.join(SRC, f"seeker_piranha_{name}_src.png")).convert("RGBA").crop(CROP)).astype(np.float32)
    im[im[:, :, 3] < 40] = 0          # nothing but the head: a stray speck outside the head is dropped
    return im


def sdf(mask):
    """A signed distance field of a boolean mask (negative inside), by brute force on this tiny canvas."""
    h, w = mask.shape
    ys, xs = np.mgrid[0:h, 0:w]
    inside = np.argwhere(mask)
    outside = np.argwhere(~mask)
    d_out = np.full((h, w), 99.0)
    d_in = np.full((h, w), 99.0)
    for (y, x) in inside:
        d_out = np.minimum(d_out, np.sqrt((ys - y) ** 2 + (xs - x) ** 2))
    for (y, x) in outside:
        d_in = np.minimum(d_in, np.sqrt((ys - y) ** 2 + (xs - x) ** 2))
    return np.where(mask, -d_in, d_out)


def half_state(open_, shut):
    """The mid-shape: the average of the two silhouettes' distance fields, thresholded; colours from the nearer source."""
    m_open = open_[:, :, 3] > 40
    m_shut = shut[:, :, 3] > 40
    mid = (sdf(m_open) + sdf(m_shut)) * 0.5 <= 0.0
    out = np.zeros_like(open_)
    # each mid pixel takes the colour of the source that has an opaque pixel there; both -> the average (the teeth and
    # the rim are the same material in both, so this keeps them); neither -> the nearest opaque pixel of the OPEN
    both = mid & m_open & m_shut
    only_o = mid & m_open & ~m_shut
    only_s = mid & m_shut & ~m_open
    neither = mid & ~m_open & ~m_shut
    out[both] = (open_[both] + shut[both]) * 0.5
    out[only_o] = open_[only_o]
    out[only_s] = shut[only_s]
    if neither.any():
        src_px = np.argwhere(m_open)
        for (y, x) in np.argwhere(neither):
            d = ((src_px[:, 0] - y) ** 2 + (src_px[:, 1] - x) ** 2)
            sy, sx = src_px[int(np.argmin(d))]
            out[y, x] = open_[sy, sx]
    out[:, :, 3] = np.where(mid, 255, 0)
    return out


def lift(im):
    """+18 % on the information-bearing parts: the violet rim / eye (blue-led, mid luminance) and the pale teeth."""
    a = im[:, :, 3] > 40
    rgb = im[:, :, :3]
    lum = rgb.mean(axis=2)
    pale = a & (lum > 150)
    violet = a & (rgb[:, :, 2] > rgb[:, :, 0] + 20) & (lum >= 60) & (lum <= 150)
    out = im.copy()
    for sel in (pale, violet):
        out[sel, :3] = np.clip(rgb[sel] * LIFT, 0, 255)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    open_, shut = cut("open"), cut("shut")
    half = half_state(open_, shut)
    states = {"open": lift(open_), "half": lift(half), "shut": lift(shut)}
    for name, im in states.items():
        img = Image.fromarray(np.clip(im, 0, 255).astype(np.uint8), "RGBA")
        path = os.path.join(OUT, f"fxp_seeker_jaws_{name}.png")
        img.save(path)
        print("wrote", path, img.size, "head bbox", img.getbbox(), "opaque", int((im[:, :, 3] > 40).sum()))
    sheet = Image.new("RGB", (3 * 64 * 6 + 40, 40 * 6 + 20), (60, 54, 70))
    for i, name in enumerate(("open", "half", "shut")):
        im = Image.open(os.path.join(OUT, f"fxp_seeker_jaws_{name}.png")).convert("RGBA").resize((64 * 6, 40 * 6), Image.NEAREST)
        bg = Image.new("RGBA", im.size, (60, 54, 70, 255))
        bg.alpha_composite(im)
        sheet.paste(bg.convert("RGB"), (10 + i * (64 * 6 + 10), 10))
    sheet.save(os.path.join(SRC, "seeker_piranha_sheet.png"))


if __name__ == "__main__":
    main()
