#!/usr/bin/env python3
"""seeker_piranha.py -- the Seeker's JAWS source art: ONE tiny Shadow piranha head in two states (the production
direction, 2026-09-27: SHADOW PIRANHA retaliation, a ~140 ms reactive-damage phrase).

    python tools/asset-pipeline/v2/seeker_piranha.py

Source: two PixelLab Pro Flash images (96 x 64, `create_image_pro_flash` seed 7 for the OPEN head, then one
`edit_image_pro_flash` of it with the mouth SHUT), kept under keypose_sources/ as seeker_piranha_open_src.png and
seeker_piranha_shut_src.png. This script cuts both to one 64 x 40 canvas at the same offset (so OPEN and SHUT stay
registered on the mouth), drops any stray pixels off the head, and writes the runtime parts:

    assets/art/VFX/parts/fxp_seeker_jaws_open.png     the head, mouth open, facing LEFT, the smear trailing right
    assets/art/VFX/parts/fxp_seeker_jaws_shut.png     the same head, teeth met

The runtime (ReactionPerformance) owns position, rotation about the mouth point, scale, timing and fade; the art owns
only the two states. MouthPoint in ReactionRecipe is (7, 24) on this canvas: the front of the mouth on the seam.
"""
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SRC = os.path.join(HERE, "keypose_sources")
OUT = os.path.join(REPO, "assets", "art", "VFX", "parts")
CROP = (20, 12, 84, 52)      # the same window off both 96 x 64 sources: a 64 x 40 canvas, the head at x 5..54, y 4..35


def cut(name):
    im = Image.open(os.path.join(SRC, f"seeker_piranha_{name}_src.png")).convert("RGBA").crop(CROP)
    # nothing but the head: a stray speck the generation left outside the head's own bbox is dropped
    a = im.split()[3]
    bb = a.getbbox()
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            if px[x, y][3] < 40:
                px[x, y] = (0, 0, 0, 0)
    return im, bb


def main():
    os.makedirs(OUT, exist_ok=True)
    for name in ("open", "shut"):
        im, bb = cut(name)
        path = os.path.join(OUT, f"fxp_seeker_jaws_{name}.png")
        im.save(path)
        print("wrote", path, im.size, "head bbox", bb)
    sheet = Image.new("RGB", (2 * 64 * 6 + 30, 40 * 6 + 20), (60, 54, 70))
    for i, name in enumerate(("open", "shut")):
        im = Image.open(os.path.join(OUT, f"fxp_seeker_jaws_{name}.png")).convert("RGBA").resize((64 * 6, 40 * 6), Image.NEAREST)
        bg = Image.new("RGBA", im.size, (60, 54, 70, 255))
        bg.alpha_composite(im)
        sheet.paste(bg.convert("RGB"), (10 + i * (64 * 6 + 10), 10))
    sheet.save(os.path.join(SRC, "seeker_piranha_sheet.png"))


if __name__ == "__main__":
    main()
