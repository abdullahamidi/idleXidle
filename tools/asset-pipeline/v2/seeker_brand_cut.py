#!/usr/bin/env python3
"""seeker_brand_cut.py -- BRAND's SECOND PASS, the ETCHED SHADOW CUT (ADR-011, 2026-09-30): the same coil gesture and the
same atlas layout as the first slice (seeker_brand.py: the PixelLab path, the helpers), redrawn as a BROKEN ASYMMETRIC
SCAR-SPIRAL in a new material.

THE MATERIAL, dark first and light second (the first slice's lit lavender line WAS the mark; it read as a glowing
spiral, an eye, a rune):
  CUT   the incision: near-black (a cut filled with living Shadow), with ONE lit rim on its light-facing (upper-left)
        edge, a minority of the mark; the unlit side dark.
  HALO  a dark ink stain round the cut (drawn in ink; the lit smoke is kept faint): on a light body the dark incision
        carries the mark, on a dark body the violet rim does.

THE SHAPE: the coil curls inward, but its outer contour is BROKEN (gaps), its weight lies on one side, its centre is
never a dot (the hook stops short of it, the spiral stays open), and 1-3 small scar branches / fractures leave it.

THREE VISIBLE DEPTHS (the gameplay values are untouched; the picture is quantized): TIER 1 marked (base BRAND, ETCH's
120 %): thin, incomplete, dark, a minimal rim. TIER 2 deeper (170 %): the cut widens a little, the inner curl runs on,
a scar branch appears, the stain gains density. TIER 3 fully branded (220 % and above): a thicker incision, a second
branch, a stronger stain, a slightly broader footprint, still open in the centre. Plus the SPREAD cut (SPRAWL's half
strength), the thinnest.

Cells per tier (the first slice's COLUMNS): GATHER x3 (ink motes, closing in, then the stain condensing: dark first),
IDLE x6 (the same cut; the stain drifts by a texel, one phase a short rim shimmer), EDGE (the apply's rim catching: the
cut's light-facing edge and its outer contour, drawn hot), CARVE x3 (the deepen: ink gathering where the new cut will
be, the new cut, its rim answering), FORM x3 (a migrated mark seeping in: stain, part of the cut, the whole cut),
LOOSEN x2 (the mark collapsing into its own dark centre), then the dark ink THREAD puffs a spread carries.

    PYTHONUTF8=1 python tools/asset-pipeline/v2/seeker_brand_cut.py [preview dir] [design]
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

import seeker_brand as sb
from seeker_bite import value_noise, white

G, LOW, CELL, PHASES, TG = sb.G, sb.LOW, sb.CELL, sb.PHASES, sb.TG
PATH, TP, GX, GY, DIST, TPAR, CENTRE = sb.PATH, sb.TP, sb.GX, sb.GY, sb.DIST, sb.TPAR, sb.CENTRE
dilate, erode, clean, drop_specks, puff, fill_pinholes = sb.dilate, sb.erode, sb.clean, sb.drop_specks, sb.puff, sb.fill_pinholes
INNER = np.zeros((G, G), bool)
INNER[1:G - 1, 1:G - 1] = True                           # nothing on a cell's outermost texel (smoke cut by the border)
COLUMNS, THREAD_CELLS, OPEN = sb.COLUMNS, sb.THREAD_CELLS, sb.OPEN
SEED = 20260930

# THE BANDS (luminance, opacity) the runtime tints: the INCISION near-black, the STAIN a dark violet (the unlit wall), the
# RIM lit; multiplied by the groove tint they are ~(11, 9, 16), ~(38, 30, 55) and ~(129, 103, 186)
CUT, STAIN, RIM = (0.07, 0.96), (0.55, 0.9), (0.95, 1.0)
STAIN1 = (0.66, 0.9)     # DEPTH 1's wall, one step lighter: on a black whelp its thin cut was lost at play scale

# THE DESIGNS (reviewed by a blind read): per tier (spread, 1, 2, 3) the cut's half width and how far along the path it
# reaches; the GAPS that break the outer contour (path t ranges, per tier); the SCAR BRANCHES (path t where it leaves the
# coil, the angle from the outward normal in degrees, the length and half width in logical px, the first tier that has
# it); the HEAVY side (degrees, the direction the cut is thickest toward) and how lopsided it is
DESIGNS = {
    "A": dict(hw=(0.6, 1.0, 1.25, 1.5), reveal=(0.84, 0.84, 0.93, 0.99),
              gaps=([(0.25, 0.30), (0.54, 0.59), (0.70, 0.74)], [(0.25, 0.30), (0.54, 0.59), (0.70, 0.74)],
                    [(0.26, 0.30), (0.55, 0.59)], [(0.265, 0.295), (0.555, 0.585)]),
              branches=[(0.42, -35, 4.0, 0.7, 2), (0.10, 30, 3.5, 0.7, 3), (0.66, 150, 2.2, 0.55, 3)],
              heavy=60, lopsided=0.45),
    # B, revised after the review of the takes: DEPTH 3's growth where a black body shows it (thicker, both walls), wide
    # breaks at every depth (the outer contour of DEPTH 2 / 3 wrapped 335-350 degrees: a ring), DEPTH 1 the thinnest
    "B": dict(hw=(0.6, 1.0, 1.35, 2.3), reveal=(0.82, 0.82, 0.92, 0.98),
              gaps=([(0.16, 0.26), (0.45, 0.54), (0.68, 0.75)], [(0.16, 0.26), (0.45, 0.54), (0.68, 0.75)],
                    [(0.16, 0.26), (0.46, 0.54), (0.68, 0.73)], [(0.16, 0.26), (0.45, 0.54), (0.65, 0.73)]),
              branches=[(0.35, 20, 4.5, 0.75, 2), (0.60, -25, 4.0, 0.9, 3), (0.08, -40, 3.0, 0.6, 3)],
              heavy=120, lopsided=0.4),
    "C": dict(hw=(0.6, 0.95, 1.2, 1.45), reveal=(0.80, 0.80, 0.90, 0.97),
              gaps=([(0.30, 0.36), (0.62, 0.66)], [(0.30, 0.36), (0.62, 0.66)], [(0.31, 0.35), (0.63, 0.66)],
                    [(0.315, 0.345), (0.63, 0.655)]),
              branches=[(0.50, 0, 5.0, 0.7, 2), (0.20, 45, 3.0, 0.65, 3), (0.74, -140, 2.5, 0.55, 3)],
              heavy=20, lopsided=0.35),
}
# B was chosen by a blind read (three readers, unanimous: "a sunken shadow wound"); A and C are kept for the record
DESIGN = DESIGNS[os.environ.get("BRAND_DESIGN", "B")]
TIERS = ["spread", "tier1", "tier2", "tier3"]

ANG = np.arctan2(GY - CENTRE[1], GX - CENTRE[0])          # each texel's angle round the coil's centre
LIGHT = np.array([-1.0, -1.0]) / math.sqrt(2.0)           # the stage's light comes from the upper left


def _index(t):
    return int(min(len(TP) - 1, np.searchsorted(TP, t)))


def branch_mask(k):
    """The scar branches tier k has: short fractures leaving the coil, tapering to a point."""
    m = np.zeros((G, G), bool)
    for (t, deg, length, hw, first) in DESIGN["branches"]:
        if k < first:
            continue
        i = _index(t)
        p = PATH[i]
        tang = PATH[min(i + 3, len(PATH) - 1)] - PATH[max(i - 3, 0)]
        tang = tang / (np.linalg.norm(tang) + 1e-6)
        normal = np.array([tang[1], -tang[0]])
        if np.dot(normal, p - CENTRE) < 0:
            normal = -normal                                # outward, away from the coil's centre
        a = math.radians(deg)
        d = np.array([normal[0] * math.cos(a) - normal[1] * math.sin(a), normal[0] * math.sin(a) + normal[1] * math.cos(a)])
        rx, ry = GX - p[0], GY - p[1]
        s = np.clip(rx * d[0] + ry * d[1], 0, length)
        dist = np.hypot(rx - s * d[0], ry - s * d[1])
        m |= dist <= hw * (1.0 - 0.6 * s / length)
    return m


def incision(k, reveal=None):
    """Tier k's cut: the coil's path to its reveal, a half width heavier toward the HEAVY side, broken by the GAPS, with
    the tier's scar branches; the spiral open (sb.OPEN) and no speck left alone."""
    rv = DESIGN["reveal"][k] if reveal is None else min(reveal, DESIGN["reveal"][k])
    hw = DESIGN["hw"][k] * (1.0 + DESIGN["lopsided"] * np.cos(ANG - math.radians(DESIGN["heavy"])))
    hw = hw * np.clip((rv - TPAR) / 0.07, 0.35, 1.0) * np.clip(TPAR / 0.05, 0.5, 1.0)
    inside = (TPAR <= rv) & (DIST <= hw)
    for lo, hi in DESIGN["gaps"][k]:
        inside &= ~((TPAR >= lo) & (TPAR <= hi))
    if reveal is None or reveal >= DESIGN["reveal"][k]:
        inside |= branch_mask(k)
    inside &= ~OPEN
    return drop_specks(clean(inside), 3)


RIM_ARC = (-178.0, -50.0)   # degrees round the coil's centre: the outer arc that faces the light (upper left)


def rim_of(inside):
    """The LIT edge: ONE side of the mark only -- the cut's texels on the coil's upper-left OUTER arc whose neighbour
    toward the light is outside it and whose neighbour away from it is still cut (a stroke two texels thick there: in a
    one-texel stroke every texel was an edge and the whole mark became a lit dashed line)."""
    up = np.zeros_like(inside)
    up[1:, :] = ~inside[:-1, :]
    left = np.zeros_like(inside)
    left[:, 1:] = ~inside[:, :-1]
    down_in = np.zeros_like(inside)
    down_in[:-1, :] = inside[1:, :]
    right_in = np.zeros_like(inside)
    right_in[:, :-1] = inside[:, 1:]
    deg = np.degrees(ANG)
    arc = (deg >= RIM_ARC[0]) & (deg <= RIM_ARC[1]) & ~sb.INWARD
    return inside & arc & ((up & down_in) | (left & right_in))


R = np.hypot(GX - CENTRE[0], GY - CENTRE[1])


def outer_edge(inside):
    """The cut's OUTER side: edge texels with an outside neighbour farther from the coil's centre than themselves."""
    res = np.zeros_like(inside)
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        nb_out = np.zeros_like(inside)
        nb_r = np.zeros_like(R)
        ys, yd = slice(max(0, dy), G + min(0, dy)), slice(max(0, -dy), G + min(0, -dy))
        xs, xd = slice(max(0, dx), G + min(0, dx)), slice(max(0, -dx), G + min(0, -dx))
        nb_out[yd, xd] = ~inside[ys, xs]
        nb_r[yd, xd] = R[ys, xs]
        res |= inside & nb_out & (nb_r > R)
    return res


def bands_of(inside, rim=True, k=2):
    """The cut's bands. Its CORE near-black (a cut filled with Shadow, darker than a light host); its WALL dark violet on
    the OUTER side only, two texels deep (drawn on both sides of a thin stroke it read as an outlined doodle, a
    hairline glyph); DEPTH 3 gets its inner wall too (its extra depth was all black core, invisible on a black whelp);
    DEPTH 1's wall a step lighter; the lit RIM on the light-facing side only."""
    b = np.where(inside, 1, 0).astype(np.uint8)
    wall = outer_edge(inside)
    wall |= inside & dilate(wall, 1) & ~erode(erode(inside))
    # (DEPTH 3's outer wall stays two texels deep: three covered its thick stroke, and its incision -- the dark fill the
    # brief asks DEPTH 3 to strengthen -- fell to 5 % of the mark)
    lit = rim_of(inside) if rim else np.zeros_like(inside)
    # (DEPTH 3 had an inner wall too: it ringed its dark core into pockets -- an eye -- and, filled, it left DEPTH 3 with
    # no incision at all, a printed violet glyph on a light host. The incision stays on the HOLLOW side, open to it.)
    # NEVER A PUPIL: a pocket of dark core the visible bands ring -- through a mouth under two texels wide too, which
    # the eye does not see at play scale -- is FILLED with wall: a solid dark-violet lobe, never a violet ring round a
    # dark centre (notched open by one texel, DEPTH 3's lobes still read as an 'o', a pupil in an iris)
    for _ in range(4):
        pocket = enclosed_dark(inside & ~(wall | lit), erode(dilate(wall | lit, 1)) | wall | lit)
        if not pocket.any():
            break
        wall |= pocket
    b[wall] = 4 if k == 1 else 2
    b[lit] = 3
    return b


def enclosed_dark(inside, barrier):
    """The cut's dark (core) texels no 4-connected path through non-barrier texels joins to the cell's border: a dark
    core ringed by the visible bands."""
    free = ~barrier
    reach = np.zeros_like(inside)
    reach[0, :] = free[0, :]
    reach[-1, :] = free[-1, :]
    reach[:, 0] |= free[:, 0]
    reach[:, -1] |= free[:, -1]
    while True:
        grown = dilate(reach, 1) & free
        if (grown == reach).all():
            break
        reach = grown
    return inside & ~reach


def stain(k, drift=(0, 0)):
    """The dark ink round the cut (alpha): denser with depth, broken by holes; never filling the coil's hollow."""
    inside = incision(k)
    grain = np.roll(value_noise(G, G, 4, salt=40 + k), drift, axis=(0, 1))
    near = dilate(inside, 1) & ~inside
    a = np.zeros((G, G), np.float32)
    a[inside] = 0.9
    a[near] = np.where(grain[near] > (0.62, 0.5, 0.38, 0.28)[k], (0.35, 0.45, 0.55, 0.65)[k], 0.0)
    if k >= 2:
        far = dilate(inside, 2) & ~dilate(inside, 1)
        a[far] = np.where(grain[far] > (0.7, 0.62)[k - 2], 0.3, 0.0)
    # the stain spreads OUT into the body, never into the coil's hollow (a dark-filled hollow read as a whirlpool, an eye)
    a[~inside & sb.INWARD] *= 0.35
    a[~INNER] = 0.0
    # inside the mark's envelope (MarkRecipe.HaloWidthShare: what the head is kept clear of); the heavy side's outer
    # ring of stain reached a cell's edge
    a[~inside & (np.abs(GX - CENTRE[0]) > 12.2)] = 0.0
    a[~inside & (np.abs(GY - CENTRE[1]) > 10.4)] = 0.0
    # and never a dark pool the visible bands ring (on a light body the stain reads as dark as the core: a bay of it
    # closed in by the wall read as a pupil)
    b = bands_of(inside, rim=k > 0, k=k)
    visible = b >= 2
    shut = enclosed_dark(~visible, erode(dilate(visible, 1)) | visible)
    a[shut & ~inside] = np.minimum(a[shut & ~inside], 0.25)
    return np.clip(a, 0, 1).astype(np.float32)


# ── the cells ────────────────────────────────────────────────────────────────────────────────────────────────────────

def idle(k, phase):
    """At rest: the same cut and the same stain in every phase; phase 3 lengthens one rim stretch by a texel (a single-edge
    shimmer, once in six steps). No travelling light, no pulse."""
    inside = incision(k)
    b = bands_of(inside, rim=k > 0, k=k)
    if k > 0 and phase == 3:
        rim = rim_of(inside)
        extra = inside & dilate(rim, 1) & ~rim & (TPAR > 0.08) & (TPAR < 0.22)
        b[extra & (b == 1)] = 3
    return b, stain(k)                              # the stain held still (redrawn roots were the loudest idle motion)


def gather(k, step):
    """Dark first: ink motes loosely round the coil (0), closing in (1), the stain condensed into the cut's shape (2)."""
    rng = np.random.default_rng(SEED + 100 + 10 * k + step)
    inside = incision(k)
    cut = np.zeros((G, G), np.uint8)
    ink = np.zeros((G, G), np.float32)
    if step < 2:
        for _ in range((6, 9)[step]):
            t = rng.random() * DESIGN["reveal"][k]
            px, py = PATH[_index(t)]
            dx, dy = px - CENTRE[0], py - CENTRE[1]
            push = (0.45 + rng.random() * 0.7) if step == 0 else (0.05 + rng.random() * 0.3)
            size = 2 if step == 1 and rng.random() < 0.5 else 3
            x = int(round(CENTRE[0] + dx * (1 + push) + rng.normal(0, 1.0) - 1))
            y = int(round(CENTRE[1] + dy * (1 + push) + rng.normal(0, 1.0) - 1))
            x = min(max(x, int(CENTRE[0] - 11)), int(CENTRE[0] + 11) - size)
            y = min(max(y, int(CENTRE[1] - 9)), int(CENTRE[1] + 9) - size)
            p = puff(rng, size)
            cut[y:y + size, x:x + size][p] = 1
            ink[y:y + size, x:x + size][p] = 0.7
        ink = np.maximum(ink, np.where(dilate(cut > 0, 1), 0.3, 0.0))
    else:
        cut = np.where(inside, 1, 0).astype(np.uint8)
        ink = stain(k) * 0.9
    return cut, np.clip(ink, 0, 1).astype(np.float32)


def edge(k):
    """The APPLY beat and a re-formed mark's arrival (drawn hot, cooling fast): the cut's light-facing rim and the outer
    contour of its LIGHT-FACING HALF catching, one-sided (the whole outer contour lit was the most ring-like cell)."""
    inside = incision(k)
    deg = np.degrees(ANG)
    outer = inside & ~erode(inside) & ~sb.INWARD & (deg <= -15.0) & (deg >= -195.0)
    return np.where(fill_pinholes(rim_of(inside) | outer), 3, 0).astype(np.uint8)


def carve(k, step):
    """The DEEPEN beat (drawn hot over the old cut): 0 ink gathering where the new cut will be (dark), 1 the new cut
    (dark, its rim lit), 2 the rim answering on the new cut. Only what tier k adds to tier k - 1."""
    b = np.zeros((G, G), np.uint8)
    if k == 0:
        return b                                        # nothing deepens into the spread cut
    now, before = incision(k), incision(k - 1)
    new = now & ~before
    new = now & dilate(new, 1) if new.sum() < 12 else new
    if step == 0:
        b[new & ~rim_of(now)] = 1                           # dark ink gathering (the hot tint x a dark band)
    elif step == 1:
        b[new] = 1
        b[new & rim_of(now)] = 3
    else:
        b[new & rim_of(now)] = 3
        b[new & ~rim_of(now) & dilate(rim_of(now) & new, 1)] = 2
    if step < 2 and not (b > 0).any():
        b[new] = 1
    if step == 2 and not (b > 0).any():
        b[new & ~erode(new)] = 2                        # a new cut with no light-facing edge: its wall answers
    filled = fill_pinholes(b > 0)
    b[filled & (b == 0)] = 1                            # no pinhole: a one-texel hole read as a pupil
    return b


def form(k, step):
    """A migrated mark SEEPING IN on its new host: the stain (0), part of the cut drawn in from its outer end (1), the
    whole cut, no rim yet (2)."""
    rv = DESIGN["reveal"][k]
    if step == 0:
        inside = incision(k, reveal=rv * 0.45)
        return np.zeros((G, G), np.uint8), np.where(dilate(inside, 1), 0.55, 0.0).astype(np.float32)
    inside = incision(k, reveal=rv * 0.75) if step == 1 else incision(k)
    return bands_of(inside, rim=False, k=k), stain(k) * (0.7 if step == 1 else 0.9)


def loosen(k, step):
    """The old mark COLLAPSING into its own dark centre (0 drawn in to ~65 %, 1 a small dark knot fading)."""
    inside = incision(k)
    f = (0.62, 0.3)[step]
    ys, xs = np.nonzero(inside)
    m = np.zeros((G, G), bool)
    ny = np.round(CENTRE[1] + (ys + 0.5 - CENTRE[1]) * f - 0.5).astype(int)
    nx = np.round(CENTRE[0] + (xs + 0.5 - CENTRE[0]) * f - 0.5).astype(int)
    m[np.clip(ny, 0, G - 1), np.clip(nx, 0, G - 1)] = True
    m = clean(m | (dilate(m, 1) & erode(dilate(m, 1)))) if step == 0 else dilate(m, 1) & erode(dilate(m, 2))
    # kept OPEN as it collapses (drawn in whole, the broken spiral closed into a ring round a pupil): a wedge through the
    # contour's first break stays empty, and no pinhole is left
    lo, hi = DESIGN["gaps"][k][0]
    gap = PATH[_index((lo + hi) / 2)] - CENTRE
    wedge = np.abs(np.angle(np.exp(1j * (ANG - math.atan2(gap[1], gap[0]))))) < math.radians(24)
    m = fill_pinholes(m & ~wedge) & ~wedge
    # its outer edge keeps the wall's violet while it draws in (step 0), so the collapse shows on a black host too; the
    # last step is a dark knot fading
    b = np.where(m, 1, 0).astype(np.uint8)
    if step == 0:
        b[m & outer_edge(m)] = 2
        b[fill_pinholes(b > 0) & (b == 0)] = 1
    return b, np.where(dilate(m, 1) & INNER, (0.5, 0.35)[step], 0.0).astype(np.float32)


def thread(step):
    """A spread's ink thread puff: a dark clump with a dark-violet core, ragged; no lit centre."""
    rng = np.random.default_rng(SEED + 300 + step)
    b = np.zeros((TG, TG), np.uint8)
    size = (4, 3, 3)[step]
    o = (TG - size) // 2
    p = np.ones((size, size), bool)
    for (y, x) in ((0, 0), (0, size - 1), (size - 1, 0), (size - 1, size - 1)):
        p[y, x] = rng.random() < 0.3
    b[o:o + size, o:o + size] = np.where(p & erode(p), 2, np.where(p, 1, 0))
    return b


def cells(k):
    out = [gather(k, s) for s in range(3)]
    out += [idle(k, ph) for ph in range(PHASES)]
    z = np.zeros((G, G), np.float32)
    out.append((edge(k), z))
    out += [(carve(k, s), z) for s in range(3)]
    out += [form(k, s) for s in range(3)]
    out += [loosen(k, s) for s in range(2)]
    return out


def cut_image(bands):
    lum = np.select([bands == 1, bands == 2, bands == 3, bands == 4], [CUT[0], STAIN[0], RIM[0], STAIN1[0]], 0.0).astype(np.float32)
    a = np.select([bands == 1, bands == 2, bands == 3, bands == 4], [CUT[1], STAIN[1], RIM[1], STAIN1[1]], 0.0).astype(np.float32)
    k = np.ones((LOW, LOW), np.float32)
    return white(np.kron(a, k), np.kron(lum, k))


def build():
    rows = len(TIERS)
    atlas = Image.new("RGBA", (CELL * len(COLUMNS), CELL * rows * 2 + TG * LOW), (0, 0, 0, 0))
    for k in range(rows):
        for c, (cut, hal) in enumerate(cells(k)):
            atlas.alpha_composite(cut_image(cut), (c * CELL, k * CELL))
            atlas.alpha_composite(sb.halo_image(hal), (c * CELL, (rows + k) * CELL))
    for t in range(THREAD_CELLS):
        atlas.alpha_composite(cut_image(thread(t)), (t * TG * LOW, rows * 2 * CELL))
    return atlas


def spans():
    tip = PATH[0]
    return {
        "cell": CELL, "low": LOW, "grid": G, "stages": TIERS, "columns": COLUMNS,
        "thread_cell": TG * LOW, "thread_cells": THREAD_CELLS, "halo_row0": len(TIERS),
        "thread_y": len(TIERS) * 2 * CELL, "phases": PHASES,
        "centre": [round(float(CENTRE[0]) * LOW, 1), round(float(CENTRE[1]) * LOW, 1)],
        "tip": [round(float(tip[0]) * LOW, 1), round(float(tip[1]) * LOW, 1)],
        "box": sb.BOX * LOW,
    }


def main():
    preview_dir = sys.argv[1] if len(sys.argv) > 1 else None
    out = preview_dir or sb.OUT
    os.makedirs(out, exist_ok=True)
    atlas = build()
    atlas.save(os.path.join(out, "fxp_seeker_brand.png"))
    if not preview_dir:
        with open(os.path.join(sb.SRC, "seeker_brand_spans.json"), "w", encoding="utf-8") as f:
            json.dump(spans(), f, indent=2)
    print("atlas", atlas.size, "->", out)


if __name__ == "__main__":
    main()
