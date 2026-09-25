#!/usr/bin/env python3
"""seeker_jaws -- the Seeker's JAWS as RIGID PARTS: a spring-loaded hunting clamp (ADR-011, JAWS identity pass).

    python tools/asset-pipeline/v2/seeker_jaws.py            # write the parts
    JAWS_PREVIEW=<png> python tools/asset-pipeline/v2/seeker_jaws.py   # and a poses preview

Writes, under assets/art/Props/ (every part on ONE canvas, so the pivots, the chain point and the clamp point serve
them all):
  prop_seeker_jaws_base.png        the HOUSING: the chain shackle, the riveted spring box (the coil showing through a
                                   slot, a latch on top), the reinforced front plate and its two hinge brackets
  prop_seeker_jaws_upper.png       CLAMP ARM A: the upper crescent arm, drawn at the stop (the runtime rotates it open)
  prop_seeker_jaws_lower.png       CLAMP ARM B: the lower crescent arm, drawn at the stop
  prop_seeker_jaws_upper_edge.png  arm A's teeth, white: the Source glint at the clamp
  prop_seeker_jaws_lower_edge.png  arm B's teeth, white
  prop_seeker_chain_body.png       the chain's dark metal body, a cross-section stretched along the tether
  prop_seeker_chain_link.png       one chain link, face-on and edge-on (the link accents)

WHAT IT IS. A spring-loaded HUNTING CLAMP, a mantrap mechanism: "JAWS" names its two opposing clamp arms, never a face.
The owner's identity brief (2026-09-25) rejected the polish pass's head: a long upper and a long lower toothed plate
tapering to one tip, with a round hub behind them, read at true speed as a metal crocodile (snout, jaws, an eye). This
drawing changes the SHAPE LANGUAGE, not the colour:
  - the HOUSING is the heavy mass and reads first: a rectangular shackle for the chain, a riveted box with a coil
    spring showing between its rails and a latch on top, and a tall reinforced front plate;
  - the two ARMS are short forged CRESCENTS of one thickness, each hinged on its OWN knuckle at a corner of the front
    plate (two pivots, the way a bear trap's jaws hinge at the two ends of its base), teeth on the inner edge only,
    the upper and lower teeth OFFSET (not a mirrored, biological row);
  - shut, the arms stop with a GAP between their tips: upper arm, the caught limb, lower arm. No seam, no lens, no
    nose, nothing round at the middle height (no eye).
Three silhouettes were compared at play size before this was drawn (A: this bear-trap clamp; B: a crossed-lever spring
clamp, read blind as "a throwing star"; C: a round spring drum with crescent arms on one pivot, read blind as "a crab
claw"). A was read blind as "bear trap jaws" and was chosen (production/qa/evidence/jaws-identity/).

WHY DRAWN. PixelLab was used for three CONCEPTS only (42696d59: a crescent-armed grabber on a sprung bar; 7e776e95: a
sprung rat-trap on a plate, top-down; 4513dcd7: a riveted box with a shackle). They informed the housing; none has the
exact pivots a rigid part needs, and none is used. Earlier candidates 3c45cc40 (a robot crocodile) and ec3db96a (a
vise) were rejected in the polish pass. The parts are drawn here pixel by pixel in the Seeker's steel, on his grid (one
source pixel = 3 texture pixels), the way his throwing knife is (seeker_knife.py).
"""
from __future__ import annotations

import math
import os

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "assets", "art", "Props"))
SCALE = 3

W, H = 33, 34
CY = 17.0                   # the clamp's centre line (the chain's line)
ARC_C = (18.5, CY)          # the arms' common arc centre (drawn at the stop)
R_OUT, R_IN = 12.6, 8.8     # the arms' outer and inner radius: one forged thickness
ARM_FROM, ARM_TO = 103.0, 26.0   # each arm's span in degrees off the centre line (root at the plate, tip at the gap)
PIVOT_U = (17.0, 7.5)       # arm A's knuckle: it rotates about this (source px; the runtime reads it x3)
PIVOT_L = (17.0, 26.5)      # arm B's knuckle
EYE = (0.5, CY)             # where the chain hooks on: the back of the shackle
BITE = (22.0, CY)           # the clamp point: the middle of the space the arms close around (the caught limb)
UPPER_TEETH = (((20, 8), (20, 9)), ((22, 9), (22, 10)), ((24, 11), (24, 12)))   # (root, point) source px
LOWER_TEETH = (((21, 24), (21, 23)), ((23, 23), (23, 22)), ((25, 21), (25, 20)))  # one px along: they interleave

O = (12, 7, 15, 255)       # outline (his)
D = (40, 42, 54, 255)      # iron shadow
M = (72, 78, 98, 255)      # iron
L = (112, 122, 150, 255)   # iron light
TL = (150, 162, 184, 255)  # tooth steel (a step above the iron, well below white)
TD = (104, 114, 136, 255)  # tooth shade
CLEAR = (0, 0, 0, 0)


def polar(x: float, y: float) -> tuple[float, float]:
    dx, dy = x - ARC_C[0], y - ARC_C[1]
    return math.hypot(dx, dy), math.degrees(math.atan2(dy, dx))


SS = 8                      # supersampling for the arms' shapes: drawn as polygons, then snapped to the source grid


def _coverage(polys: list) -> list:
    """The share of each source pixel the polygons cover (drawn at SS x, box-averaged)."""
    im = Image.new("L", (W * SS, H * SS), 0)
    d = ImageDraw.Draw(im)
    for poly in polys:
        d.polygon([(x * SS, y * SS) for x, y in poly], fill=255)
    small = im.resize((W, H), Image.BOX)
    return [[small.getpixel((x, y)) / 255.0 for y in range(H)] for x in range(W)]


def _arc_point(r: float, deg: float) -> tuple[float, float]:
    return (ARC_C[0] + r * math.cos(math.radians(deg)), ARC_C[1] + r * math.sin(math.radians(deg)))


def arm_pixels(upper: bool) -> tuple[set, set, set]:
    """One arm at the stop as (body, teeth, tooth points): a forged band snapped to the grid, hand-placed teeth."""
    sign = -1.0 if upper else 1.0
    n = 48
    angles = [sign * (ARM_FROM + (ARM_TO - ARM_FROM) * i / n) for i in range(n + 1)]
    band = [_arc_point(R_OUT, a) for a in angles] + [_arc_point(R_IN, a) for a in reversed(angles)]
    body_cov = _coverage([band])
    body = {(x, y) for x in range(W) for y in range(H) if body_cov[x][y] >= 0.5}
    # the TEETH are placed by hand on the snapped arm (a polygon tooth this small snaps to a smear): three spikes on
    # each arm's inner edge, a root and a point toward the caught limb, with a gap between them. The lower arm's sit
    # one pixel along from the upper's, so the two rows interleave and never mirror
    teeth, tips = set(), set()
    for root, point in (UPPER_TEETH if upper else LOWER_TEETH):
        teeth.update((root, point))
        tips.add(point)
    return body, teeth, tips


def outline(px: dict) -> dict:
    """Every painted pixel on the part's outer edge becomes the outline colour."""
    out = dict(px)
    for (x, y) in px:
        if any((x + dx, y + dy) not in px for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            out[(x, y)] = O
    return out


def paint_arm(upper: bool) -> tuple[Image.Image, Image.Image]:
    body, teeth, tips = arm_pixels(upper)
    mid = (R_IN + R_OUT) / 2.0
    px = {}
    for (x, y) in body:
        r, _ = polar(x + 0.5, y + 0.5)
        if any((x + dx, y + dy) not in body for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            px[(x, y)] = O                  # one outline round the forged band (a dark lip behind the teeth too)
        elif upper:
            px[(x, y)] = L if r > mid else M   # the upper arm is lit along its back
        else:
            px[(x, y)] = M if r > mid else D   # the lower is in its own shadow
    for p in teeth:
        # bare steel, a step brighter than the arm (the lower arm's in its own shade), never white: teeth, not a grin
        px[p] = (TL if p in tips else TD) if upper else (TD if p in tips else M)
    im = Image.new("RGBA", (W, H), CLEAR)
    glint = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    for (x, y), c in px.items():
        im.putpixel((x, y), c)
    for (x, y) in teeth:
        glint.putpixel((x, y), (255, 255, 255, 255 if (x, y) in tips else 140))
    return im, glint


def paint_base() -> Image.Image:
    """The housing, drawn over the arms' roots: shackle, spring box, latch, front plate and its two knuckles."""
    px: dict = {}

    def fill(x0, y0, x1, y1, c):
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                px[(x, y)] = c

    # the SHACKLE: a rectangular loop the chain runs through (a hole, never a round eye)
    fill(0, 14, 3, 20, M)
    fill(0, 14, 3, 14, L)
    for p in ((1, 16), (2, 16), (1, 17), (2, 17), (1, 18), (2, 18)):
        px.pop(p, None)
    # the SPRING BOX: a riveted casing, lit on top, dark below. Its face is plain iron with one riveted strap down its
    # back edge (a column of rivets, never two dots over a line: nothing on it can be read as a face)
    fill(4, 11, 13, 23, M)
    fill(4, 11, 13, 12, L)
    fill(4, 21, 13, 23, D)
    fill(5, 12, 6, 22, D)
    for y in (14, 19):
        px[(5, y)] = L                           # the strap's two rivets, one above the other
        px[(6, y)] = M
    # the COIL SPRING, OUTSIDE along the casing's top from the back to the plate: slanted turns of lit wire with a
    # dark gap between them, a spring on the silhouette's edge where it reads as a spring
    for x in range(5, 14):
        for y in range(8, 11):
            turn = (x * 2 + (y - 8)) % 4
            px[(x, y)] = (L if y < 10 else M) if turn < 2 else D
    # the FRONT PLATE: tall and reinforced (lit edge, iron, a dark back edge), the base the two arms hinge on
    fill(14, 5, 16, 29, M)
    fill(14, 5, 14, 29, L)
    fill(16, 5, 16, 29, D)
    # the HINGE FLANGES at the plate's two corners: the plate widens into a flange round each arm's pin, with the
    # pin one small dark rivet (a single pixel). A 2x2 dark square pin in a lit square frame read (blind) as an eye
    # socket over the arms' "mouth": nothing here is a hole, a ring or a bright round point
    for (kx, ky) in (PIVOT_U, PIVOT_L):
        x0, y0 = int(kx) - 2, int(ky) - 2
        fill(x0, y0, x0 + 4, y0 + 4, M)
        fill(x0, y0, x0 + 4, y0, L if ky < CY else M)
        fill(x0, y0 + 4, x0 + 4, y0 + 4, D)
        px[(int(kx), int(ky))] = D                         # the pin, one small dark rivet
    px = outline(px)
    # the shackle's hole and the spring's shadows stay open/dark inside the outline pass
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


def pose(upper: Image.Image, lower: Image.Image, base: Image.Image, jaw: float, pad: int = 12) -> Image.Image:
    """The clamp with each arm rotated `jaw` degrees open about its own knuckle (the runtime's rotation), padded."""
    s = SCALE
    canvas = Image.new("RGBA", ((W + 2 * pad) * s, (H + 2 * pad) * s), CLEAR)

    def place(part, deg, pivot):
        big = Image.new("RGBA", canvas.size, CLEAR)
        big.alpha_composite(up(part), (pad * s, pad * s))
        centre = ((pivot[0] + pad) * s, (pivot[1] + pad) * s)
        return big.rotate(deg, resample=Image.NEAREST, center=centre)

    canvas.alpha_composite(place(lower, -jaw, PIVOT_L))
    canvas.alpha_composite(place(upper, jaw, PIVOT_U))
    canvas.alpha_composite(place(base, 0, PIVOT_U))
    return canvas


def main() -> int:
    os.makedirs(OUT, exist_ok=True)
    upper, upper_glint = paint_arm(True)
    lower, lower_glint = paint_arm(False)
    base = paint_base()
    parts = {
        "prop_seeker_jaws_base": up(base), "prop_seeker_jaws_upper": up(upper), "prop_seeker_jaws_lower": up(lower),
        "prop_seeker_jaws_upper_edge": up(upper_glint), "prop_seeker_jaws_lower_edge": up(lower_glint),
        "prop_seeker_chain_body": chain_body(), "prop_seeker_chain_link": chain_link(),
    }
    for name, im in parts.items():
        im.save(os.path.join(OUT, name + ".png"))
        print(f"{name:30} {im.size} bbox={im.getbbox()}")
    tip_x = ARC_C[0] + R_OUT * math.cos(math.radians(ARM_TO))
    print(f"pivots (texture px) upper = {PIVOT_U[0] * SCALE:.1f},{PIVOT_U[1] * SCALE:.1f}"
          f"  lower = {PIVOT_L[0] * SCALE:.1f},{PIVOT_L[1] * SCALE:.1f}   eye = {EYE[0] * SCALE:.1f},{EYE[1] * SCALE:.1f}"
          f"   bite = {BITE[0] * SCALE:.1f},{BITE[1] * SCALE:.1f}   length eye->tips = {(tip_x - EYE[0]) * SCALE:.1f}")

    preview = os.environ.get("JAWS_PREVIEW")
    if preview:
        angles = (30, 14, 3, 0)
        tiles = [pose(upper, lower, base, a) for a in angles]
        tw, th = tiles[0].size
        sheet = Image.new("RGBA", (tw * 2 * len(angles) + 40, th * 2 + th + 40), (58, 54, 52, 255))
        for i, p in enumerate(tiles):
            sheet.alpha_composite(p.resize((tw * 2, th * 2), Image.NEAREST), (10 + i * tw * 2, 10))
            small = p.resize((int(tw * 0.62), int(th * 0.62)), Image.LANCZOS)
            sheet.alpha_composite(small, (10 + i * tw * 2, th * 2 + 20))
        sheet.save(preview)
        print("preview ->", preview)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
