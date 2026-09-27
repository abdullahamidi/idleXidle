#!/usr/bin/env python3
"""bite_motif_candidates.py -- three rough BITE CONTACT MOTIF candidates at combat scale, drawn on a real plate of the
Seeker at the contact frame (effects and flash off, no creature in the crop), OPEN and CLOSED (ADR-012 reset, second
pass, 2026-09-27).

    python tools/asset-pipeline/bite_motif_candidates.py <plate png> <out dir>

    M1  OPPOSING CRESCENTS   two broad curved jaws closing top / bottom (the most abstract)
    M2  TWO-FANG SNAP        one strong fang point from above, one from below, short curved supports (the most minimal)
    M3  SERRATED CLAMP MARK  short upper and lower serrated arcs with a clear open gap (the most trap-like, no prop)

Grammar shared by all three: an UPPER half and a LOWER half converging on one contact centre at the Seeker's
enemy-facing chest edge; ember on a dark body with pale tips; the negative space between the halves is the read.
"""
import math
import os
import sys

from PIL import Image, ImageDraw

EMBER = (214, 66, 38)
DARK = (28, 14, 12)
PALE = (245, 214, 176)
AT = (634.0, 652.0)          # his TORSO's enemy-facing edge at chest height in the 1080 plate (measured from a NOCHAMP diff: the visible rect's right edge is his sword tip, 812, and belt height is his sword hand)
S = 40.0                     # the fang span at play size (0.13 of his visible height)


def arc_poly(c, r_out, r_in, a0, a1, n=14):
    """A thick arc (a crescent) between two radii, angles in degrees, as a polygon."""
    outer = [(c[0] + r_out * math.cos(math.radians(a0 + (a1 - a0) * i / n)), c[1] + r_out * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]
    inner = [(c[0] + r_in * math.cos(math.radians(a1 - (a1 - a0) * i / n)), c[1] + r_in * math.sin(math.radians(a1 - (a1 - a0) * i / n))) for i in range(n + 1)]
    return outer + inner


def draw_shape(d, poly, fill=EMBER, edge=DARK, w=2):
    d.polygon(poly, fill=fill, outline=edge)
    if w > 1:
        d.line(poly + [poly[0]], fill=edge, width=w, joint="curve")
        d.polygon(poly, fill=fill)


def m1(d, at, s, gap):
    """Opposing crescents: two broad curved jaws, convex away from the bite line, their horns reaching toward the
    contact centre without closing into a ring (each spans ~110 degrees, so the sides stay open)."""
    for side in (-1, 1):
        a0, a1 = (215, 325) if side < 0 else (35, 145)
        poly = arc_poly((at[0], at[1] + side * gap), s * 0.64, s * 0.36, a0, a1)
        draw_shape(d, poly)
        for ang in (a0 + 6, a1 - 6):
            hx, hy = at[0] + s * 0.50 * math.cos(math.radians(ang)), at[1] + side * gap + s * 0.50 * math.sin(math.radians(ang))
            d.ellipse((hx - 2.5, hy - 2.5, hx + 2.5, hy + 2.5), fill=PALE)


def m2(d, at, s, gap):
    """Two-fang snap: one strong fang from above, one from below, each on a short curved support."""
    for side in (-1, 1):
        base_y = at[1] + side * (gap + s * 0.55)
        tip = (at[0] - s * 0.06, at[1] + side * gap)
        fang = [(at[0] - s * 0.30, base_y), (at[0] + s * 0.22, base_y), tip]
        draw_shape(d, fang)
        # the support: a short thick arc behind the fang's base, bowed away from the bite
        sup = arc_poly((at[0], at[1] + side * (gap + s * 0.30)), s * 0.60, s * 0.42, 215, 325) if side < 0 else \
            arc_poly((at[0], at[1] + side * (gap + s * 0.30)), s * 0.60, s * 0.42, 35, 145)
        draw_shape(d, sup)
        d.ellipse((tip[0] - 2.5, tip[1] - 2.5, tip[0] + 2.5, tip[1] + 2.5), fill=PALE)


def m3(d, at, s, gap):
    """Serrated clamp mark: short upper and lower arcs with two triangular teeth each on the inner edge."""
    for side in (-1, 1):
        c = (at[0], at[1] + side * (gap + s * 0.70))
        a0, a1 = (235, 305) if side < 0 else (55, 125)
        r_out, r_in = s * 0.78, s * 0.56
        poly = arc_poly(c, r_out, r_in, a0, a1, 10)
        draw_shape(d, poly)
        # two teeth on the inner edge, pointing at the bite line
        for ang in (a0 + 20, a1 - 20):
            bx, by = c[0] + r_in * math.cos(math.radians(ang)), c[1] + r_in * math.sin(math.radians(ang))
            tx, ty = c[0] + (r_in - s * 0.26) * math.cos(math.radians(ang)), c[1] + (r_in - s * 0.26) * math.sin(math.radians(ang))
            tooth = [(bx - s * 0.09, by), (bx + s * 0.09, by), (tx, ty)]
            draw_shape(d, tooth)
            d.ellipse((tx - 2, ty - 2, tx + 2, ty + 2), fill=PALE)


def m4(d, at, s, gap):
    """The hybrid (M1 + M2): two broad crescent jaws, each with ONE strong fang point at its middle aimed at the
    contact centre; the horns stay open at the sides so the closed state is a bite line, not a ring."""
    for side in (-1, 1):
        a0, a1 = (222, 318) if side < 0 else (42, 138)
        c = (at[0], at[1] + side * (gap + s * 0.10))
        poly = arc_poly(c, s * 0.66, s * 0.40, a0, a1)
        draw_shape(d, poly)
        # the fang: from the crescent's inner middle to the bite line
        bx, by = c[0], c[1] + side * (-s * 0.40)
        tip = (at[0], at[1] + side * gap)
        fang = [(bx - s * 0.16, by + side * s * 0.02), (bx + s * 0.16, by + side * s * 0.02), tip]
        draw_shape(d, fang)
        d.ellipse((tip[0] - 2.5, tip[1] - 2.5, tip[0] + 2.5, tip[1] + 2.5), fill=PALE)


DRAW = {"M1": m1, "M2": m2, "M3": m3, "M4": m4}
GAP = {"M1": {"open": S * 0.50, "closed": -S * 0.02}, "M2": {"open": S * 0.55, "closed": -S * 0.04}, "M3": {"open": S * 0.16, "closed": -S * 0.40}, "M4": {"open": S * 0.50, "closed": -S * 0.03}}


def main():
    plate, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    base = Image.open(plate).convert("RGB")
    crop = (520, 490, 780, 730)
    cells = []
    for name, fn in DRAW.items():
        for state, gap in GAP[name].items():
            im = base.copy()
            d = ImageDraw.Draw(im)
            fn(d, AT, S, gap)
            cell = im.crop(crop)
            cell.save(os.path.join(out, f"{name}_{state}_1to1.png"))
            cell.resize((cell.width * 3, cell.height * 3), Image.NEAREST).save(os.path.join(out, f"{name}_{state}_3x.png"))
            cells.append((name, state, cell))
    # the review sheet: three candidates, open and closed, at 1:1 and 2x
    cw, ch = crop[2] - crop[0], crop[3] - crop[1]
    sheet = Image.new("RGB", (8 * (cw + 8) + 8, 2 * (ch * 2 + 30) + 40), (30, 26, 36))
    d = ImageDraw.Draw(sheet)
    for i, (name, state, cell) in enumerate(cells):
        x = 8 + i * (cw + 8)
        d.text((x, 6), f"{name} {state}", fill=(255, 220, 120))
        sheet.paste(cell, (x, 24))
    for i, (name, state, cell) in enumerate(cells):
        x = 8 + i * (cw + 8)
        d.text((x, 24 + ch + 14), f"{name} {state} 2x", fill=(255, 220, 120))
        sheet.paste(cell.resize((cw * 2, ch * 2), Image.NEAREST).crop((cw // 2, ch // 2, cw // 2 + cw, ch // 2 + ch)), (x, 24 + ch + 30))
    sheet.save(os.path.join(out, "motif_candidates_sheet.png"))
    # blind cards: the CLOSED state alone on the target, 2x, neutral names
    for k, name in enumerate(DRAW):
        cell = [c for n, s, c in cells if n == name and s == "closed"][0]
        cell.resize((cw * 2, ch * 2), Image.LANCZOS).save(os.path.join(out, f"card_{'xyzw'[k]}.png"))
    print("wrote", out)


if __name__ == "__main__":
    main()
