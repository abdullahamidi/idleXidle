#!/usr/bin/env python3
"""bite_motif_candidates_n.py -- three rough DIRECTIONAL ASYMMETRIC SNAP candidates for the bite contact motif, drawn on
a real plate of the Seeker at the contact frame (effects and flash off, no creature in the crop), OPEN and CLOSED
(ADR-012, the third pass, 2026-09-27). M4 (symmetric jaws with a central dot) is rejected: it read as an hourglass /
status icon. These are two parts of ONE directional action arriving from the enemy's side (the right), converging on
the Seeker; no bilateral symmetry, no central dot, roots and streaks on the enemy-facing side.

    python tools/asset-pipeline/bite_motif_candidates_n.py <plate png> <out dir>

    N1  ASYMMETRIC CRESCENT BITE   a dominant upper crescent and a smaller lower crescent, closing from the right
    N2  OFFSET FANG SNAP           one large upper fang and an offset short lower jaw; the contact point off-centre
    N3  BROKEN-JAW ARC             two partial serrated arcs sharing one sweep from the right; VFX-like
"""
import math
import os
import sys

from PIL import Image, ImageDraw

EMBER = (214, 66, 38)
DARK = (28, 14, 12)
PALE = (245, 214, 176)
AT = (634.0, 652.0)          # the Seeker's torso edge at chest height in the 1080 plate (from a NOCHAMP diff)
S = 40.0                     # the motif's span at play size


def arc_poly(c, r_out, r_in, a0, a1, n=14, taper=0.0):
    """A thick arc between two radii (a crescent); `taper` thins it toward a1 (0 = even, 1 = to a point)."""
    outer, inner = [], []
    for i in range(n + 1):
        t = i / n
        ang = math.radians(a0 + (a1 - a0) * t)
        ro = r_out - (r_out - r_in) * 0.5 * taper * t
        ri = r_in + (r_out - r_in) * 0.5 * taper * t
        outer.append((c[0] + ro * math.cos(ang), c[1] + ro * math.sin(ang)))
        inner.append((c[0] + ri * math.cos(ang), c[1] + ri * math.sin(ang)))
    return outer + inner[::-1]


def shape(d, poly, fill=EMBER, edge=DARK):
    d.polygon(poly, fill=fill, outline=edge)


def streaks(d, at, s, k, ys, lengths):
    """Short trailing roots on the enemy side (right), fading toward the enemy: the direction of arrival."""
    for y, ln in zip(ys, lengths):
        x0 = at[0] + s * 0.55
        d.line([(x0, at[1] + y), (x0 + ln * k, at[1] + y)], fill=EMBER, width=2)


def n1(d, at, s, gap):
    """A dominant upper crescent sweeping from the upper right over and down onto the contact; a smaller lower
    crescent from the lower right curling up under it. The contact point sits left of the crescents' centres."""
    cx, cy = at[0] + s * 0.18, at[1]
    up = arc_poly((cx, cy - gap * 0.6), s * 0.78, s * 0.46, 250, 372, taper=0.9)     # from the upper right, over the top, down to the left
    shape(d, up)
    lo = arc_poly((cx + s * 0.1, cy + gap * 0.6), s * 0.48, s * 0.30, 10, 140, taper=0.8)   # from the lower right, under, up to the left
    shape(d, lo)
    streaks(d, at, s, 1.0, (-s * 0.55, s * 0.30), (s * 0.35, s * 0.22))


def n2(d, at, s, gap):
    """One large upper fang from the upper right driving down-left to a point; a short lower jaw wedge from the
    lower right. The point is off-centre: low and left."""
    # the fang keeps its size; the gap only lifts it (open) or drives it onto the jaw (closed)
    oy = -gap * 0.7
    tip = (at[0] - s * 0.22, at[1] + s * 0.12 + oy)
    base_l = (at[0] + s * 0.14, at[1] - s * 0.66 + oy)
    base_r = (at[0] + s * 0.72, at[1] - s * 0.44 + oy)
    fang = [base_l, base_r, (at[0] + s * 0.30, at[1] - s * 0.18 + oy), tip, (at[0] + s * 0.02, at[1] - s * 0.22 + oy)]
    shape(d, fang)
    jy = gap * 0.3
    jaw = [(at[0] - s * 0.14, at[1] + s * 0.20 + jy), (at[0] + s * 0.70, at[1] + s * 0.26 + jy), (at[0] + s * 0.62, at[1] + s * 0.48 + jy), (at[0] + s * 0.06, at[1] + s * 0.36 + jy)]
    shape(d, jaw)
    streaks(d, at, s, 1.0, (-s * 0.50, s * 0.42), (s * 0.30, s * 0.28))


def n3(d, at, s, gap):
    """Two partial serrated arcs sharing one sweep from the right (an opening `(` broken into an upper and a lower
    piece), two teeth each on the inner edge; VFX-like, less literal."""
    c = (at[0] + s * 0.50, at[1])
    for (a0, a1, side, r_out, r_in) in ((200, 268, -1, s * 1.05, s * 0.72), (96, 160, 1, s * 0.86, s * 0.60)):
        cc = (c[0], c[1] + side * gap * 0.5)
        arc = arc_poly(cc, r_out, r_in, a0, a1, 10, taper=0.4)
        shape(d, arc)
        ang = (a0 + a1) / 2 + (8 if side < 0 else -8)          # one strong tooth per arc, on the inner edge, aimed at the bite
        bx, by = cc[0] + r_in * math.cos(math.radians(ang)), cc[1] + r_in * math.sin(math.radians(ang))
        tx, ty = cc[0] + (r_in - s * 0.34) * math.cos(math.radians(ang)), cc[1] + (r_in - s * 0.34) * math.sin(math.radians(ang))
        shape(d, [(bx - s * 0.11, by - side * s * 0.02), (bx + s * 0.11, by + side * s * 0.02), (tx, ty)])
    streaks(d, at, s, 1.0, (-s * 0.30, s * 0.30), (s * 0.30, s * 0.30))


DRAW = {"N1": n1, "N2": n2, "N3": n3}
GAP = {"open": S * 0.55, "closed": -S * 0.04}


def main():
    plate, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    base = Image.open(plate).convert("RGB")
    crop = (520, 490, 780, 730)
    cells = []
    for name, fn in DRAW.items():
        for state, gap in GAP.items():
            im = base.copy()
            fn(ImageDraw.Draw(im), AT, S, gap)
            cell = im.crop(crop)
            cell.save(os.path.join(out, f"{name}_{state}_1to1.png"))
            cells.append((name, state, cell))
    cw, ch = crop[2] - crop[0], crop[3] - crop[1]
    sheet = Image.new("RGB", (6 * (cw + 8) + 8, 2 * (ch * 2 + 30) + 40), (30, 26, 36))
    d = ImageDraw.Draw(sheet)
    for i, (name, state, cell) in enumerate(cells):
        x = 8 + i * (cw + 8)
        d.text((x, 6), f"{name} {state}", fill=(255, 220, 120))
        sheet.paste(cell, (x, 24))
        d.text((x, 24 + ch + 14), f"{name} {state} 2x", fill=(255, 220, 120))
        sheet.paste(cell.resize((cw * 2, ch * 2), Image.NEAREST).crop((cw // 2, ch // 2, cw // 2 + cw, ch // 2 + ch)), (x, 24 + ch + 30))
    sheet.save(os.path.join(out, "motif_n_candidates_sheet.png"))
    for k, name in enumerate(DRAW):
        cell = [c for n, s, c in cells if n == name and s == "closed"][0]
        cell.resize((cw * 2, ch * 2), Image.LANCZOS).save(os.path.join(out, f"card_{'qrt'[k]}.png"))
    print("wrote", out)


if __name__ == "__main__":
    main()
