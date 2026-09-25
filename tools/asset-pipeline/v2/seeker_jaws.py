#!/usr/bin/env python3
"""seeker_jaws -- the Seeker's JAWS as RIGID PARTS: a spring-loaded bear trap (ADR-011, JAWS readable-clamp pass).

    python tools/asset-pipeline/v2/seeker_jaws.py            # write the parts
    JAWS_PREVIEW=<png> python tools/asset-pipeline/v2/seeker_jaws.py   # and a poses preview

Writes, under assets/art/Props/ (every part on ONE canvas, so the pins, the chain point and the clamp point serve them
all):
  prop_seeker_jaws_base.png       the HOUSING, small: a short forged base bar carrying the two pins, the trigger spring
                                  (a coil) under it, and the chain eye at its rear end
  prop_seeker_jaws_near.png       the NEAR jaw "(" (the Seeker's side), drawn SHUT; the runtime turns it open about its pin
                                  and draws it BEHIND the caught creature (the limb crosses it)
  prop_seeker_jaws_far.png        the FAR jaw ")" (the creature's side), drawn shut, in front of the creature
  prop_seeker_jaws_near_edge.png  the near jaw's teeth, white: the Source glint at the stop
  prop_seeker_jaws_far_edge.png   the far jaw's teeth, white
  prop_seeker_chain_body.png      the chain's dark metal body, a cross-section stretched along the tether
  prop_seeker_chain_link.png      one chain link, face-on and edge-on (the link accents)

WHAT IT IS. A spring-loaded BEAR TRAP / mantrap: "JAWS" names its two big opposing jaws. The history, each rejected by
the owner:
  - the polish pass's long toothed head on a round hub read as a metal CROCODILE;
  - the identity pass's big riveted box with two thin quarter-arc arms read as a BOX WITH TWO HOOKS (a grabber), and
    its 16 ms close was one open frame and one shut frame: nobody SAW it close.
The readable-clamp brief (2026-09-25): the JAWS dominate the silhouette (the housing is small), each jaw is a broad forged
crescent with a few LARGE teeth, OPEN is unmistakably different from SHUT, and shut it reads "steel jaw, TARGET, steel
jaw". This is the classic bear trap: a short base, and two semicircular jaws that spring UP about two pins close
together at its middle and shut "( )" around the limb, their tips stopping apart so the limb passes out between them.
Open, the jaws lie spread in a wide toothed cup: the space something is about to be caught in.

Chosen from three silhouettes at play size (production/qa/evidence/jaws-readable/): A, this upright bear trap (read blind
as "bear trap", best words BEAR TRAP and CLAMP); B, an asymmetric mantrap along the chain (TRAP and CLAMP, but a
sideways toothed opening still reads as a mouth); C, an upright spring yoke (BEAR TRAP and CLAMP, "fang-like jaws on a
stand"). The jaws are about 75 % of the silhouette.

DRAWN, not generated: a rigid part needs an exact pin. The jaws' bands and teeth are polygons snapped to the Seeker's
grid (one source pixel = 3 texture pixels), shaded by hand rules in his steel.
"""
from __future__ import annotations

import math
import os

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "assets", "art", "Props"))
SCALE = 3
SS = 8                      # supersampling: shapes are drawn as polygons, then snapped to the source grid

W, H = 40, 40
CX = 20.0                   # the trap's middle
JAW_C = (CX, 21.0)          # the jaws' common centre, shut: the CLAMP POINT, placed on the caught limb
R_OUT = (11.2, 13.0)        # the jaws' outer ellipse (a little taller than wide: an upright "( )", never a ring)
R_IN = (6.8, 8.8)           # their inner edge: a forged band ~4.4 px thick
NEAR_SPAN = (106.0, 242.0)  # the near jaw "(": from its pin at the base, round the left, to its tip at the top
FAR_SPAN = (74.0, -62.0)    # the far jaw ")": mirrored; the tips stop ~6 px apart (the limb passes out between them)
NEAR_TEETH = (140.0, 178.0, 216.0)   # tooth centres (deg): the near jaw's...
FAR_TEETH = (54.0, 16.0, -24.0)      # ...and the far jaw's, a third of a pitch along: interleaved, never mirrored
TOOTH_DEPTH = 3.9
TOOTH_HALF_DEG = 15.0
PIVOT_NEAR = (17.5, 31.5)   # the near jaw's pin, on the base (source px; the runtime reads it x3)
PIVOT_FAR = (22.5, 31.5)    # the far jaw's pin: a compact pair, 5 px apart (never a tall bracket)
EYE = (6.5, 32.5)           # where the chain hooks on: the eye at the base's rear end

O = (12, 7, 15, 255)       # outline (his)
D = (40, 42, 54, 255)      # iron shadow
M = (72, 78, 98, 255)      # iron
L = (112, 122, 150, 255)   # iron light
TL = (150, 162, 184, 255)  # tooth steel (a step above the iron, well below white)
TD = (104, 114, 136, 255)  # tooth shade
CLEAR = (0, 0, 0, 0)


def _ept(c, r, deg):
    return (c[0] + r[0] * math.cos(math.radians(deg)), c[1] + r[1] * math.sin(math.radians(deg)))


def _coverage(polys: list) -> list:
    """The share of each source pixel the polygons cover (drawn at SS x, box-averaged)."""
    im = Image.new("L", (W * SS, H * SS), 0)
    d = ImageDraw.Draw(im)
    for poly in polys:
        d.polygon([(x * SS, y * SS) for x, y in poly], fill=255)
    small = im.resize((W, H), Image.BOX)
    return [[small.getpixel((x, y)) / 255.0 for y in range(H)] for x in range(W)]


def jaw_pixels(near: bool) -> tuple[set, set, set]:
    """One jaw, shut, as (body, teeth, tooth points)."""
    a0, a1 = NEAR_SPAN if near else FAR_SPAN
    n = 60
    angles = [a0 + (a1 - a0) * i / n for i in range(n + 1)]
    band = [_ept(JAW_C, R_OUT, a) for a in angles] + [_ept(JAW_C, R_IN, a) for a in reversed(angles)]
    teeth_polys, points = [], []
    for c in (NEAR_TEETH if near else FAR_TEETH):
        root_r = (R_IN[0] + 0.5, R_IN[1] + 0.5)
        tip_r = (R_IN[0] - TOOTH_DEPTH, R_IN[1] - TOOTH_DEPTH)
        tri = [_ept(JAW_C, root_r, c - TOOTH_HALF_DEG), _ept(JAW_C, tip_r, c), _ept(JAW_C, root_r, c + TOOTH_HALF_DEG)]
        teeth_polys.append(tri)
        points.append(tri[1])
    body_cov = _coverage([band])
    tooth_cov = _coverage(teeth_polys)
    body = {(x, y) for x in range(W) for y in range(H) if body_cov[x][y] >= 0.5}
    teeth = {(x, y) for x in range(W) for y in range(H) if tooth_cov[x][y] >= 0.36 and (x, y) not in body}
    tips = set()
    for pt in points:
        near_px = min(teeth, key=lambda q: (q[0] + 0.5 - pt[0]) ** 2 + (q[1] + 0.5 - pt[1]) ** 2, default=None)
        if near_px is not None:
            tips.add(near_px)
            # the pixel next to the point, toward the root, is steel too: a point reads as a point, not a speck
            for q in list(teeth):
                if abs(q[0] - near_px[0]) + abs(q[1] - near_px[1]) == 1 and \
                        math.hypot(q[0] + 0.5 - JAW_C[0], q[1] + 0.5 - JAW_C[1]) < math.hypot(near_px[0] + 0.5 - JAW_C[0], near_px[1] + 0.5 - JAW_C[1]) + 1.2:
                    tips.add(q)
    return body, teeth, tips


def paint_jaw(near: bool) -> tuple[Image.Image, Image.Image]:
    body, teeth, tips = jaw_pixels(near)
    px = {}
    for (x, y) in body:
        if any((x + dx, y + dy) not in body for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            px[(x, y)] = O                        # one outline round the forged band (a dark lip behind the teeth too)
            continue
        # how far out across the band this pixel is: 0 at the inner edge, 1 at the outer
        ex, ey = (x + 0.5 - JAW_C[0]), (y + 0.5 - JAW_C[1])
        r_out = 1.0 / math.hypot(ex / R_OUT[0], ey / R_OUT[1]) if (ex or ey) else 1.0
        r_in = 1.0 / math.hypot(ex / R_IN[0], ey / R_IN[1]) if (ex or ey) else 1.0
        band = (1.0 - r_in) / max(1e-6, r_out - r_in)   # ~0 inner .. ~1 outer
        lit = (ex < 0) and (ey < 2)                     # the light comes from the upper left
        if near:
            px[(x, y)] = L if (band > 0.62 and lit) else (M if band > 0.28 else D)
        else:
            px[(x, y)] = M if (band > 0.62 and ey < 0) else D
    for p in teeth:
        # bare steel, a step brighter than the jaw (the far jaw's in its own shade), never white: teeth, not a grin
        px[p] = (TL if p in tips else TD) if near else (TD if p in tips else M)
    im = Image.new("RGBA", (W, H), CLEAR)
    glint = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    for (x, y), c in px.items():
        im.putpixel((x, y), c)
    for (x, y) in teeth:
        glint.putpixel((x, y), (255, 255, 255, 255 if (x, y) in tips else 140))
    return im, glint


def outline(px: dict) -> dict:
    out = dict(px)
    for (x, y) in px:
        if any((x + dx, y + dy) not in px for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            out[(x, y)] = O
    return out


def paint_base() -> Image.Image:
    """The housing, small: the base bar with the two pins, the trigger spring under it, the chain eye at its rear."""
    px: dict = {}

    def fill(x0, y0, x1, y1, c):
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                px[(x, y)] = c

    # the BASE BAR: forged, lit along its top, the jaws' roots sit on it
    fill(11, 30, 28, 33, M)
    fill(11, 30, 28, 30, L)
    fill(11, 33, 28, 33, D)
    # the two PINS: one small dark rivet each, in the bar (a pair of bright dots would read as eyes)
    for (px_, py_) in (PIVOT_NEAR, PIVOT_FAR):
        px[(int(px_), int(py_))] = D
    # the TRIGGER SPRING under the bar: a short coil, slanted turns of lit wire with a dark gap between them
    for x in range(15, 25):
        for y in range(34, 37):
            turn = (x * 2 + (y - 34)) % 4
            px[(x, y)] = (L if y == 34 else M) if turn < 2 else D
    # the CHAIN EYE at the bar's rear end: a small rectangular loop, its hole open
    fill(6, 30, 10, 34, M)
    fill(6, 30, 10, 30, L)
    for p in ((7, 31), (8, 31), (9, 31), (7, 32), (8, 32), (9, 32), (7, 33), (8, 33), (9, 33)):
        px.pop(p, None)
    px = outline(px)
    im = Image.new("RGBA", (W, H), CLEAR)
    for (x, y), c in px.items():
        if 0 <= x < W and 0 <= y < H:
            im.putpixel((x, y), c)
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


def pose(near: Image.Image, far: Image.Image, base: Image.Image, jaw: float, pad: int = 12) -> Image.Image:
    """The trap with each jaw turned `jaw` degrees open about its own pin (the runtime's rotation), padded."""
    s = SCALE
    canvas = Image.new("RGBA", ((W + 2 * pad) * s, (H + 2 * pad) * s), CLEAR)

    def place(part, deg, pivot):
        big = Image.new("RGBA", canvas.size, CLEAR)
        big.alpha_composite(up(part), (pad * s, pad * s))
        return big.rotate(deg, resample=Image.BICUBIC, center=((pivot[0] + pad) * s, (pivot[1] + pad) * s))

    # PIL rotates counter-clockwise for a positive angle: the near jaw opens counter-clockwise (out to the left)
    canvas.alpha_composite(place(far, -jaw, PIVOT_FAR))
    canvas.alpha_composite(place(near, jaw, PIVOT_NEAR))
    canvas.alpha_composite(place(base, 0, PIVOT_NEAR))
    return canvas


def main() -> int:
    os.makedirs(OUT, exist_ok=True)
    near, near_glint = paint_jaw(True)
    far, far_glint = paint_jaw(False)
    base = paint_base()
    parts = {
        "prop_seeker_jaws_base": up(base), "prop_seeker_jaws_near": up(near), "prop_seeker_jaws_far": up(far),
        "prop_seeker_jaws_near_edge": up(near_glint), "prop_seeker_jaws_far_edge": up(far_glint),
        "prop_seeker_chain_body": chain_body(), "prop_seeker_chain_link": chain_link(),
    }
    for name, im in parts.items():
        im.save(os.path.join(OUT, name + ".png"))
        print(f"{name:30} {im.size} bbox={im.getbbox()}")
    top = JAW_C[1] - R_OUT[1]
    print(f"pins (texture px) near = {PIVOT_NEAR[0] * SCALE:.1f},{PIVOT_NEAR[1] * SCALE:.1f}"
          f"  far = {PIVOT_FAR[0] * SCALE:.1f},{PIVOT_FAR[1] * SCALE:.1f}   eye = {EYE[0] * SCALE:.1f},{EYE[1] * SCALE:.1f}"
          f"   clamp = {JAW_C[0] * SCALE:.1f},{JAW_C[1] * SCALE:.1f}   height jaw top -> spring = {(37 - top) * SCALE:.1f}")
    preview = os.environ.get("JAWS_PREVIEW")
    if preview:
        angles = (34, 31, 20, 3, 0)
        tiles = [pose(near, far, base, a) for a in angles]
        tw, th = tiles[0].size
        sheet = Image.new("RGBA", (tw * 2 * len(angles) + 40, th * 2 + th + 40), (58, 54, 52, 255))
        for i, p in enumerate(tiles):
            sheet.alpha_composite(p.resize((tw * 2, th * 2), Image.NEAREST), (10 + i * tw * 2, 10))
            small = p.resize((int(tw * 0.6), int(th * 0.6)), Image.LANCZOS)
            sheet.alpha_composite(small, (10 + i * tw * 2, th * 2 + 20))
        sheet.save(preview)
        print("preview ->", preview)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
