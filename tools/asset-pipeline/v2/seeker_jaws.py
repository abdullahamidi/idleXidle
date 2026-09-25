#!/usr/bin/env python3
"""seeker_jaws -- the Seeker's JAWS as RIGID PARTS: a hinge hub and two toothed iron jaws (ADR-011, JAWS polish).

    python tools/asset-pipeline/v2/seeker_jaws.py            # write the parts and a preview

Writes, under assets/art/Props/ (every part on ONE canvas, so one pivot and one eye serve them all):
  prop_seeker_jaws_base.png        the HINGE HUB: chain eye, spring coils, the hub disk and its pivot bolt
  prop_seeker_jaws_upper.png       JAW A: the upper serrated jaw, drawn CLOSED (the runtime rotates it open)
  prop_seeker_jaws_lower.png       JAW B: the lower serrated jaw, drawn CLOSED
  prop_seeker_jaws_upper_edge.png  the upper jaw's teeth, white: the Source glint at the clamp
  prop_seeker_jaws_lower_edge.png  the lower jaw's teeth, white
  prop_seeker_chain_body.png       the chain's dark metal body, a cross-section stretched along the tether
  prop_seeker_chain_link.png       one chain link, face-on and edge-on (the link accents)

WHY PARTS, AND WHY DRAWN. The owner's polish brief (2026-09-25): the jaws are METAL, so they must close by ROTATING about
a hinge, never by swapping or scaling a sprite; and the world object must be the icon's mechanism (opposing serrated
jaws, a hinge, a central mechanism, a chain) seen from the arena's side, not the icon enlarged. The first world art was
a PixelLab U that shut into a nearly perfect toothed ring (a portal, a collar). Two PixelLab candidates for a side-view
trap head were rejected on sight: 3c45cc40 drew a robot crocodile with an EYE (a creature, not a trap), ec3db96a a flat
riveted vise with no hinged jaws and none of the icon's serrated arcs. A rigid part needs an exact pivot and a jaw that
tessellates with its twin, which is arithmetic, so the head is drawn here, pixel by pixel, in the Seeker's steel and on
his grid (one source pixel = 3 texture pixels), the way his throwing knife is (seeker_knife.py).

THE MECHANISM (source px, the head pointing RIGHT, toward the creature):
  the chain EYE at the back (left), a short neck wound with a SPRING, the HUB disk with its pivot BOLT, and the two
  JAWS reaching forward from the pivot. The jaws meet on a TOOTHED SEAM: the upper jaw's triangular teeth point down,
  the lower's up, half a tooth along, so shut they interlock pixel for pixel and read as TWO jaws with a row of teeth
  between them, never as a ring; open (each rotated about the bolt) each shows its own saw edge.
"""
from __future__ import annotations

import math
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "assets", "art", "Props"))
SCALE = 3

W, H = 44, 32
PIVOT = (10.0, 16.0)       # the bolt: both jaws rotate about it (source px; the runtime reads it x3)
EYE = (2.5, 16.0)          # where the chain hooks on
SEAM = 16.0                # the closing line: the upper jaw above it, the lower below
TIP_X = 36                 # the jaws' tips

O = (12, 7, 15, 255)       # outline (his)
D = (40, 42, 54, 255)      # iron shadow
M = (72, 78, 98, 255)      # iron
L = (112, 122, 150, 255)   # iron light
T = (190, 204, 220, 255)   # tooth steel
S = (232, 240, 247, 255)   # tooth glint
TD = (128, 140, 162, 255)  # tooth shade
CLEAR = (0, 0, 0, 0)


TEETH_UPPER = [13 + 4 * k for k in range(6)]   # tooth centres (source x): the upper jaw's point down...
TEETH_LOWER = [15 + 4 * k for k in range(6)]   # ...the lower jaw's point up, half a tooth along, so shut they interlock


def depth(x: float) -> float:
    """A jaw's depth behind its lip: an arch from the hub (deep root), a strong belly, curling down into the tip."""
    u = min(1.0, max(0.0, (x - 8.0) / (TIP_X - 8.0)))
    return 2.0 + 5.2 * math.sin(math.pi * u ** 0.85) * (1.0 - 0.35 * u)


def jaw_pixels(upper: bool) -> tuple[set, set, set]:
    """One CLOSED jaw as (body, teeth, tooth tips): the body behind a straight lip, the teeth hanging over the seam.

    The seam is the line between rows 15 and 16. The upper jaw's body ends at row 14 (its lip); a tooth centred on c
    covers row 15 at c-1..c+1 and row 16 at c. The lower jaw mirrors it (lip at row 17), centred half a tooth along,
    so rows 15 and 16 are shared by the two rows of teeth without a single overlapping pixel.
    """
    body, teeth, tips = set(), set(), set()
    for x in range(8, TIP_X + 1):
        cx = x + 0.5
        d = depth(cx)
        if upper:
            top = int(round(14 - d))
            for y in range(top, 15):
                body.add((x, y))
        else:
            bottom = int(round(17 + d))
            for y in range(17, bottom + 1):
                body.add((x, y))
    for c in (TEETH_UPPER if upper else TEETH_LOWER):
        if c > TIP_X - 1:
            continue
        base_row, tip_row = (15, 16) if upper else (16, 15)
        for x in (c - 1, c, c + 1):
            teeth.add((x, base_row))
        teeth.add((c, tip_row))
        tips.add((c, tip_row))
    # the tip: a heavier fang hooking over the seam, so the head reads as biting, not as a pair of tongs
    fx = TIP_X
    fang = [(fx - 1, 15), (fx, 15), (fx, 16)] if upper else [(fx - 1, 16), (fx, 16), (fx, 15)]
    # (the two fangs cross: the upper's at the tip column row 16 and the lower's at row 15)
    for p in fang:
        teeth.add(p)
    tips.add(fang[-1])
    return body, teeth, tips


def paint_jaw(upper: bool) -> tuple[Image.Image, Image.Image]:
    body, teeth, tips = jaw_pixels(upper)
    everything = body | teeth
    im = Image.new("RGBA", (W, H), CLEAR)
    glint = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    for (x, y) in body:
        cx = x + 0.5
        d = depth(cx)
        back = (14 - d) if upper else (17 + d)
        # its outer edge and its two ends are outlined; the lip (the row over the teeth) is a dark line
        exposed = any((x + dx, y + dy) not in everything for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        lip = (y == 14) if upper else (y == 17)
        if lip:
            c = D if not exposed else O
        elif exposed:
            c = O
        else:
            # lit on its back (the upper jaw's top, the lower jaw's belly shadowed): iron with a light rim
            rel = abs(y + 0.5 - back) / max(1.0, d)
            if upper:
                c = L if rel < 0.35 else M
            else:
                c = M if rel > 0.45 else D
        im.putpixel((x, y), c)
    for (x, y) in teeth:
        tip = (x, y) in tips
        # the two rows of teeth are two VALUES (the upper lit, the lower in its own shade), so shut the interlock
        # still reads as a zigzag of teeth rather than one bright stripe
        if upper:
            c = S if tip else T
        else:
            c = T if tip else TD
        im.putpixel((x, y), c)
        glint.putpixel((x, y), (255, 255, 255, 255 if tip else 150))
    return im, glint


def paint_base() -> Image.Image:
    im = Image.new("RGBA", (W, H), CLEAR)
    px = im.load()
    # the chain EYE: a ring, its hole showing through
    for x in range(0, 6):
        for y in range(12, 21):
            d = math.hypot((x + 0.5 - EYE[0]) / 2.6, (y + 0.5 - EYE[1]) / 3.6)
            if 0.55 <= d <= 1.0:
                px[x, y] = O if d > 0.85 or d < 0.66 else (L if y < 16 else M)
    # the NECK with its SPRING: coils across a short bar from the eye to the hub
    for x in range(5, 9):
        for y in range(14, 19):
            px[x, y] = O if y in (14, 18) else (T if (x % 2 == 0 and y == 15) else (M if x % 2 == 0 else D))
    # the HUB: a disk over the jaws' roots, lit from above-left, and its BOLT
    r = 4.6
    for x in range(W):
        for y in range(H):
            d = math.hypot(x + 0.5 - PIVOT[0], y + 0.5 - PIVOT[1])
            if d <= r:
                if d > r - 1.0:
                    px[x, y] = O
                else:
                    lit = (PIVOT[0] - (x + 0.5)) + (PIVOT[1] - (y + 0.5))
                    px[x, y] = L if lit > 2.2 else (M if lit > -1.5 else D)
    for x, y, c in ((9, 15, S), (10, 15, T), (9, 16, T), (10, 16, TD), (8, 15, O), (11, 16, O), (9, 14, O), (10, 17, O),
                    (8, 16, O), (11, 15, O), (10, 14, O), (9, 17, O)):
        px[x, y] = c
    return im


def chain_link() -> Image.Image:
    """One link, face-on (an oval ring with its hole) and edge-on (a short bar), each 12x7 source px, x2."""
    Dk, Md, Lt = (66, 70, 88, 255), (92, 100, 128, 255), (150, 164, 188, 255)
    face = ["..OOOOOOOO..", ".OMMLLLLMMO.", "OML.OOOO.DMO", "OM.O....O.DO", "OMD.OOOO.DDO", ".ODDDDDDDDO.", "..OOOOOOOO.."]
    bar = ["............", "..OOOOOOOO..", ".OMLLLLLLMO.", "OMMMMMMMMMDO", ".ODDDDDDDDO.", "..OOOOOOOO..", "............"]
    pal = {"O": O, "D": Dk, "M": Md, "L": Lt, ".": CLEAR}
    im = Image.new("RGBA", (24, 7))
    for cell, rows in enumerate((face, bar)):
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                im.putpixel((cell * 12 + x, y), pal[ch])
    return im.resize((48, 14), Image.NEAREST)


def chain_body() -> Image.Image:
    """The tether's dark metal body: a cross-section (outline, shadow, iron, a thin light, iron, outline), 4 px long."""
    rows = [O, (40, 42, 54, 255), (72, 78, 98, 255), (104, 114, 140, 255), (58, 62, 78, 255), O]
    im = Image.new("RGBA", (4, len(rows)))
    for y, c in enumerate(rows):
        for x in range(4):
            im.putpixel((x, y), c)
    return im


def up(im: Image.Image) -> Image.Image:
    return im.resize((im.width * SCALE, im.height * SCALE), Image.NEAREST)


def rotated_about_pivot(im: Image.Image, degrees: float) -> Image.Image:
    return im.rotate(degrees, resample=Image.NEAREST, center=(PIVOT[0] * SCALE, PIVOT[1] * SCALE))


def main() -> int:
    os.makedirs(OUT, exist_ok=True)
    upper, upper_glint = paint_jaw(True)
    lower, lower_glint = paint_jaw(False)
    base = paint_base()
    parts = {
        "prop_seeker_jaws_base": up(base), "prop_seeker_jaws_upper": up(upper), "prop_seeker_jaws_lower": up(lower),
        "prop_seeker_jaws_upper_edge": up(upper_glint), "prop_seeker_jaws_lower_edge": up(lower_glint),
        "prop_seeker_chain_body": chain_body(), "prop_seeker_chain_link": chain_link(),
    }
    for name, im in parts.items():
        im.save(os.path.join(OUT, name + ".png"))
        print(f"{name:30} {im.size} bbox={im.getbbox()}")
    print(f"pivot (texture px) = {PIVOT[0] * SCALE:.1f},{PIVOT[1] * SCALE:.1f}   eye = {EYE[0] * SCALE:.1f},{EYE[1] * SCALE:.1f}"
          f"   bite point = {(TIP_X - 8) * SCALE:.1f},{SEAM * SCALE:.1f}   length eye->tips = {(TIP_X + 1 - EYE[0]) * SCALE:.1f}")

    # a preview: open (28 deg each way), mid, shut, rebound; big and at play size, on the arena floor's tone
    preview = os.environ.get("JAWS_PREVIEW")
    if preview:
        def pose(a):
            canvas = Image.new("RGBA", (W * SCALE, H * SCALE), CLEAR)
            canvas.alpha_composite(rotated_about_pivot(up(lower), -a))
            canvas.alpha_composite(rotated_about_pivot(up(upper), a))
            canvas.alpha_composite(up(base))
            return canvas
        angles = (28, 14, 4, 0)
        sheet = Image.new("RGBA", (W * SCALE * 2 * len(angles) + 40, H * SCALE * 2 + H * 2 + 40), (58, 54, 52, 255))
        for i, a in enumerate(angles):
            p = pose(a)
            sheet.alpha_composite(p.resize((p.width * 2, p.height * 2), Image.NEAREST), (10 + i * W * SCALE * 2, 10))
            small = p.resize((int(p.width * 0.62), int(p.height * 0.62)), Image.LANCZOS)
            sheet.alpha_composite(small, (10 + i * W * SCALE * 2, H * SCALE * 2 + 20))
        sheet.save(preview)
        print("preview ->", preview)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
