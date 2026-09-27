#!/usr/bin/env python3
"""seeker_fangs.py -- the Seeker's JAWS source art: ONE Shadow fang, authored by hand (the FINAL direction, 2026-09-27:
SHADOW FANGS. No creature, no eye, no mouth, no teeth-in-a-mouth, no tail: four large simple shapes are the effect).

    python tools/asset-pipeline/v2/seeker_fangs.py

The piranha (a head with an eye, a mouth, teeth and a tail on a ~45 px sprite) was below the useful perceptual budget
at gameplay scale: at true speed it read as coloured motion. What survives reduction is a STRONG SIMPLE TRIANGLE, so
that is all the art is: one broad, slightly curved fang, a near-black Shadow body, a strong violet edge, a small pale
tip. The runtime draws it four times (two from above, two from below: the same sprite flipped, tilted and scaled),
so the geometry is controlled and every fang is the same material. No PixelLab: this is a graphic shape, drawn
deterministically at 4x and downsampled, so its edge is clean at the LinearClamp render.

    assets/art/VFX/parts/fxp_seeker_fang.png      40 x 72, the TIP at (22, 71) pointing DOWN, the base along the top
    tools/asset-pipeline/v2/keypose_sources/seeker_fangs_sheet.png   the fang, and the four-fang OPEN and SNAPPED
                                                                     compositions round a body-sized box (a preview)
"""
import os

from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "assets", "art", "VFX", "parts")
SRC = os.path.join(HERE, "keypose_sources")

W, H = 40, 72            # the canvas (texture px)
TIP = (22, 71)           # the tip: the sprite's origin at runtime (ReactionRecipe.TipPoint)
S = 4                    # supersample

BODY = (22, 13, 38)      # near-black violet: the Shadow body
EDGE = (152, 82, 255)    # strong violet: the edge
EDGE_HI = (196, 150, 255)   # the edge where it thins toward the tip
TIP_PALE = (236, 226, 255)  # the small pale highlight at the point


def bezier(p0, p1, p2, n=64):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
             (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]) for t in (k / (n - 1) for k in range(n))]


def outline():
    """The fang's outline in canvas units: a broad base along the top, two gently curved flanks, a point at the tip.
    The left flank is the fang's INNER side (it faces the target's centre at runtime): a touch concave, so the shape
    reads as a curved tooth / thorn rather than a plain wedge; the right flank is convex."""
    base_l, base_r = (4.0, 1.5), (36.0, 1.5)
    tip = (TIP[0], TIP[1] + 0.4)
    left = bezier(base_l, (9.5, 38.0), (tip[0] - 1.0, tip[1] - 1.0))
    right = bezier(base_r, (34.5, 30.0), (tip[0] + 1.0, tip[1] - 1.0))
    pts = left + [tip] + right[::-1]
    return [(x * S, y * S) for x, y in pts]


def render():
    mask = Image.new("L", (W * S, H * S), 0)
    ImageDraw.Draw(mask).polygon(outline(), fill=255)
    # the edge band: the mask minus its erosion (3 px in canvas units at the base, thinning toward the tip)
    inner = mask.filter(ImageFilter.MinFilter(2 * 3 * S + 1))
    inner_thin = mask.filter(ImageFilter.MinFilter(2 * 2 * S + 1))
    rgba = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0))
    px = rgba.load()
    m, mi, mt = mask.load(), inner.load(), inner_thin.load()
    for y in range(H * S):
        v = y / (H * S)   # 0 at the base, 1 at the tip
        for x in range(W * S):
            if not m[x, y]:
                continue
            in_body = mi[x, y] if v < 0.55 else mt[x, y]
            if v > 0.86:
                # the pale point: a short blend from the edge violet into the pale tip
                k = min(1.0, (v - 0.86) / 0.12)
                c = tuple(int(EDGE_HI[i] + (TIP_PALE[i] - EDGE_HI[i]) * k) for i in range(3))
            elif not in_body:
                k = min(1.0, max(0.0, (v - 0.35) / 0.5))
                c = tuple(int(EDGE[i] + (EDGE_HI[i] - EDGE[i]) * k) for i in range(3))
            else:
                # the body: near-black, with a whisper of violet toward the inner flank so it is not a flat cut-out
                k = max(0.0, 0.10 - x / (W * S) * 0.10)
                c = tuple(int(BODY[i] + (EDGE[i] - BODY[i]) * k) for i in range(3))
            px[x, y] = (*c, 255)
    return rgba.resize((W, H), Image.LANCZOS)


def preview(fang):
    """The four-fang compositions round a 160 x 130 body box (the whelp's rough visible body), OPEN and SNAPPED,
    at the recipe's shares (FangHeightShare 0.30, OpenGapShare 0.12, BiteDepthShare 0.16, spread 0.55 of the fang)."""
    bw, bh = 160, 130
    fh = int(0.30 * bh)
    fw = int(fh * W / H)
    f = fang.resize((fw, fh), Image.LANCZOS)
    tip_x, tip_y = TIP[0] * fw / W, TIP[1] * fh / H
    cells = []
    for name, up_tip, lo_tip in (("OPEN", -0.12 * bh, bh + 0.12 * bh), ("SNAPPED", 0.16 * bh, bh - 0.16 * bh)):
        cell = Image.new("RGBA", (bw + 120, bh + 140), (60, 54, 70, 255))
        ox, oy = 60, 70
        ImageDraw.Draw(cell).rectangle((ox, oy, ox + bw, oy + bh), fill=(90, 84, 100, 255), outline=(140, 130, 150, 255))
        cx = ox + bw / 2
        for side, tilt in ((-1, 12), (1, -12)):
            x = cx + side * 0.55 * fh
            for lower in (False, True):
                im = f.transpose(Image.FLIP_TOP_BOTTOM) if lower else f
                im = im.rotate(tilt if not lower else -tilt, resample=Image.BICUBIC, expand=True)
                ty = oy + (lo_tip if lower else up_tip)
                # place by the tip (the rotated image's tip is ~its bottom (or top) centre)
                px_ = int(x - im.width / 2)
                py_ = int(ty - (im.height - 1 if not lower else 0))
                cell.alpha_composite(im, (px_, py_))
        ImageDraw.Draw(cell).text((8, 6), name, fill=(235, 200, 120, 255))
        cells.append(cell)
    big = fang.resize((W * 4, H * 4), Image.NEAREST)
    sheet = Image.new("RGB", (W * 4 + 20 + sum(c.width + 10 for c in cells), max(H * 4, cells[0].height) + 20), (60, 54, 70))
    bg = Image.new("RGBA", big.size, (60, 54, 70, 255))
    bg.alpha_composite(big)
    sheet.paste(bg.convert("RGB"), (10, 10))
    x = W * 4 + 20
    for c in cells:
        sheet.paste(c.convert("RGB"), (x, 10))
        x += c.width + 10
    return sheet


def main():
    os.makedirs(OUT, exist_ok=True)
    fang = render()
    path = os.path.join(OUT, "fxp_seeker_fang.png")
    fang.save(path)
    print("wrote", path, fang.size, "bbox", fang.getbbox())
    preview(fang).save(os.path.join(SRC, "seeker_fangs_sheet.png"))
    print("wrote", os.path.join(SRC, "seeker_fangs_sheet.png"))


if __name__ == "__main__":
    main()
