#!/usr/bin/env python3
"""magpie_dagger -- the Magpie's steel dagger painted back into her basic attack's hand (a hand repaint).

    python tools/asset-pipeline/v2/magpie_dagger.py            # rewrite frames 2-7 of the attack strip
    python tools/asset-pipeline/v2/magpie_dagger.py --sheet D  # also write a before / after sheet to D

THE PROBLEM (the Phase 1 review of the real footage, item 06, identity). The generated strip drew her dagger as a
glowing green blade fused into a baked slash arc. strip_bake.py took the baked arc out (design.md 6, CLEAN) and the
blade went with it, so on the contact frames her hand was a bare fist while the knife cue and the recipe's thin slash
said "dagger": the verb had no object for the frames that matter.

THE FIX: one small steel dagger, drawn pixel by pixel in the strip's own grid (one source pixel = 3 strip pixels, the
same density as the Seeker's throwing knife, seeker_knife.py), its guard and blade painted OVER the strip at her fist on
every frame she holds it (2-7): forward on the thrust (2, 3: the contact), carried back on the back-swing (4-6) and
low on the settle (7). The grip is inside her fist, so it is not drawn. The blade is the strip's material (dark outline,
steel shades, one bevel highlight), not a light: the recipe's slash and sliver at the creature are its light. One object
in the hand (ADR-011): the dagger is never thrown.

Each frame's guard point and blade direction were placed by eye on the strip's fist (frame-local px; screen degrees,
y down: 0 = toward the row). The script always starts from the cleaned strip at BASE_REV, so it is idempotent.
Runs under the Python that has numpy (`python`, not the `py` launcher here).
"""
from __future__ import annotations

import argparse
import io
import math
import os
import subprocess

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
REL = "assets/art/Animations/Roster/magpie_attack/char_magpie_attack_strip8_512.png"
BASE = os.path.join(HERE, "keypose_sources", "magpie_attack_cleaned_strip8_512.png")   # strip_bake's output, cached
F = 512
SCALE = 3

PAL = {
    "O": (14, 9, 16, 255),       # outline
    "D": (86, 96, 122, 255),     # steel shade
    "M": (138, 152, 172, 255),   # steel
    "L": (192, 208, 222, 255),   # steel light
    "H": (232, 240, 246, 255),   # bevel highlight
    "G": (150, 112, 54, 255),    # brass guard
    "g": (204, 162, 82, 255),    # brass light
    ".": (0, 0, 0, 0),
}
# guard + blade, pointing RIGHT; the guard's column is x = 1 (the outline is x = 0). 16 x 7 source px.
DAGGER = [
    ".OOO............",
    "OgGO.OOOOOO.....",
    "OGGOOHHHHLLOOO..",
    "OGGOLMMMMMMMMLOO",
    "OGGODDDDDDDDOO..",
    "OGGOOOOOOOOO....",
    ".OOO............",
]
GUARD = (2.0, 3.5)   # the guard's centre in source px (where the fist's front edge sits)

# per frame: the fist's front edge (frame-local px) and the blade's direction (screen degrees, y down)
POSES = {
    2: ((402, 343), 0.0),      # the thrust, reaching
    3: ((438, 316), -10.0),    # the contact: fullest forward
    4: ((86, 305), 180.0),     # the back-swing: carried behind
    5: ((88, 268), -168.0),
    6: ((101, 236), -112.0),   # raised behind her
    7: ((90, 306), 180.0),     # the settle, low
}


def _sprite() -> Image.Image:
    h, w = len(DAGGER), len(DAGGER[0])
    img = Image.new("RGBA", (w, h))
    for y, row in enumerate(DAGGER):
        for x, c in enumerate(row):
            img.putpixel((x, y), PAL[c])
    return img.resize((w * SCALE, h * SCALE), Image.NEAREST)


def _rotate_smooth(img: Image.Image, degrees: float, centre: tuple[float, float]) -> Image.Image:
    """Turn a sprite smoothly (the art is filtered LinearClamp): premultiplied, bicubic, then back."""
    a = np.array(img).astype(np.float32) / 255.0
    a[..., :3] *= a[..., 3:4]
    chans = [Image.fromarray((a[..., c] * 255).round().astype(np.uint8)).rotate(degrees, resample=Image.BICUBIC, center=centre)
             for c in range(4)]
    r = np.stack([np.array(ch).astype(np.float32) / 255.0 for ch in chans], axis=-1)
    al = r[..., 3:4]
    r[..., :3] = np.where(al > 1e-4, r[..., :3] / np.maximum(al, 1e-4), 0)
    return Image.fromarray((np.clip(r, 0, 1) * 255).round().astype(np.uint8))


def _base() -> Image.Image:
    if not os.path.exists(BASE):
        # first run: cache the cleaned strip (strip_bake's output) before it is painted over
        Image.open(os.path.join(REPO, REL)).convert("RGBA").save(BASE, optimize=True)
    return Image.open(BASE).convert("RGBA")


def build(strip: Image.Image) -> Image.Image:
    dagger = _sprite()
    w, h = dagger.size
    gx, gy = GUARD[0] * SCALE, GUARD[1] * SCALE
    out = strip.copy()
    for i, ((fx, fy), deg) in POSES.items():
        pad = Image.new("RGBA", (w * 3, h * 3 + w * 2), (0, 0, 0, 0))
        ox, oy = w, h + w
        pad.paste(dagger, (ox, oy))
        # PIL turns counter-clockwise on screen; the pose's degrees are clockwise (y down)
        rot = _rotate_smooth(pad, -deg, (ox + gx, oy + gy))
        frame = out.crop((i * F, 0, (i + 1) * F, F))
        frame.alpha_composite(rot, (int(round(fx - ox - gx)), int(round(fy - oy - gy))))
        out.paste(frame, (i * F, 0))
    return out


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--sheet")
    args = ap.parse_args()
    base = _base()
    out = build(base)
    out.save(os.path.join(REPO, REL), optimize=True)
    if args.sheet:
        os.makedirs(args.sheet, exist_ok=True)
        sheet = Image.new("RGBA", (8 * 256, 2 * 256), (90, 110, 90, 255))
        for row, img in enumerate((base, out)):
            sheet.alpha_composite(img.resize((8 * 256, 256), Image.LANCZOS), (0, row * 256))
        sheet.convert("RGB").save(os.path.join(args.sheet, "magpie_dagger_sheet.jpg"), quality=88)


if __name__ == "__main__":
    main()
