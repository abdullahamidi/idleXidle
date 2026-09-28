#!/usr/bin/env python3
"""seeker_bite.py -- the Seeker's JAWS source art: a FRONTAL Shadow bite (the owner, 2026-09-28, pointing at Roni
Kangaskorte's "Bite VFX" on ArtStation as the reference: "not a bad jaw biting from the side: the earlier perspective,
biting from the FRONT, but a more effective and beautiful jaw").

Eight parts, all white/grey luminance with alpha (the runtime tints them along the Shadow palette), composed at
runtime (ADR-010's rule: effects are composed from parts, never generated as motion):

    fxp_seeker_bite_upper.png    the UPPER CROWN of fangs, seen from the front: two long canines at its corners that bow
                                 OUT and hook their points IN, "( )", and five leaf-shaped teeth between them, packed so
                                 each overlaps its neighbour, hanging from an arched gum; their points on one flat line;
                                 STATES condensation states left to right (0: a loose drift of mist, last: the crisp teeth)
    fxp_seeker_bite_lower.png    the LOWER ROW: four leaf-shaped teeth whose points rise between the upper points, and a
                                 taller one at each end, that the canines close outside of; the same STATES
    fxp_seeker_bite_star.png     the impact's FLASH: a small hot core, six fat rays, a soft bloom
    fxp_seeker_bite_ring.png     the impact's RING, two cells: the crisp pressure wave (four tapered arcs with gaps), and
                                 the same ring SOFTENED into torn smoke (it dissolves into the mist)
    fxp_seeker_bite_streaks.png  the impact's SPEED LINES: a dozen meaningful radial streaks of mixed length around an
                                 empty centre (an accent: thirty read as a pattern to inspect)
    fxp_seeker_bite_shards.png   the impact's SHARDS, two cells: torn splinters of the broken teeth in six clusters, and the
                                 same splinters softened (late in their life they are Shadow fragments dissolving)
    fxp_seeker_bite_smoke.png    the Shadow MIST: a soft, irregular smoke lobe; three of them, turned apart, make the one
                                 mist volume the fangs condense from, compress into and burst out of

The teeth are drawn as LEAVES (widest a third of the way from the root, tapering to a sharp point), with feathered
edges, a soft halo and a thin dark seam where one overlaps the next; a crisp tooth is a flat glow brightening toward
its point (a centre stripe read as a crystal spike). It writes keypose_sources/seeker_bite_spans.json (the cells, the
rows' bite lines, every tooth's point), which the tests hold the recipe to, and keypose_sources/seeker_bite_sheet.png
(every part over grey and over black). Every random draw derives from SEED; no PixelLab.

    PYTHONUTF8=1 python tools/asset-pipeline/v2/seeker_bite.py
"""
import json
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "assets", "art", "VFX", "parts")
SRC = os.path.join(HERE, "keypose_sources")

SEED = 20260928
STREAK_COUNT = 13              # the speed lines: a dozen meaningful streaks (an accent; thirty read as a pattern)
HISTORY = os.path.join(SRC, "history")
STATES = 6                     # condensation states per row: mist -> crisp teeth
SS = 4                         # supersampling for the shapes
UW, UH = 256, 200              # the upper crown's cell
LW, LH = 224, 120              # the lower row's cell
CX = 128.0                     # the upper crown's centre line
LCX = LW / 2.0                 # the lower row's centre line
UPPER_TIP_Y = 116.0            # the upper inner points, one flat line: the crown's bite line
LOWER_TIP_Y = 40.0             # the lower middle points, one flat line: the row's bite line


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def value_noise(w, h, cells, salt):
    r = np.random.default_rng(SEED + salt)
    ch, cw = max(2, int(h / cells)), max(2, int(w / cells))
    lattice = (r.random((ch + 1, cw + 1)) * 255).astype(np.uint8)
    return np.asarray(Image.fromarray(lattice, "L").resize((w, h), Image.BICUBIC)).astype(np.float32) / 255.0


def blur(a, radius):
    if radius <= 0.05:
        return a
    return np.asarray(Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(radius))).astype(np.float32) / 255.0


def leaf(root, tip, width, bow=0.0, widest=0.36, steps=48):
    """A tooth's outline as a LEAF: from a rounded root to a sharp point, widest `widest` of the way along; its spine a
    quadratic curve from `root` to `tip` bowed sideways by `bow` px at its middle (+ is toward the spine's right-hand
    normal, which for a tooth pointing down is screen +x), so a canine can bow out and hook its point back in."""
    rx, ry = root
    tx, ty = tip
    dx, dy = tx - rx, ty - ry
    ln = math.hypot(dx, dy)
    ux, uy = dx / ln, dy / ln
    nx, ny = uy, -ux
    cx, cy = (rx + tx) / 2 + nx * bow * 2.0, (ry + ty) / 2 + ny * bow * 2.0
    left, right = [], []
    for k in range(steps + 1):
        t = k / steps
        px = (1 - t) ** 2 * rx + 2 * (1 - t) * t * cx + t * t * tx
        py = (1 - t) ** 2 * ry + 2 * (1 - t) * t * cy + t * t * ty
        gx = 2 * (1 - t) * (cx - rx) + 2 * t * (tx - cx)
        gy = 2 * (1 - t) * (cy - ry) + 2 * t * (ty - cy)
        gl = math.hypot(gx, gy) or 1.0
        mx, my = gy / gl, -gx / gl
        if t < widest:
            prof = 0.62 + 0.38 * math.sin(0.5 * math.pi * t / widest)
        else:
            prof = math.cos(0.5 * math.pi * (t - widest) / (1 - widest)) ** 0.85
        hw = 0.5 * width * prof
        left.append((px - mx * hw, py - my * hw))
        right.append((px + mx * hw, py + my * hw))
    # a rounded root: a half-circle cap behind the root
    cap = []
    hw0 = 0.5 * width * 0.62
    a0 = math.atan2(-uy, -ux)
    for k in range(1, 12):
        a = a0 - math.pi / 2 + math.pi * k / 12
        cap.append((rx + math.cos(a) * hw0, ry + math.sin(a) * hw0))
    return right[::-1] + cap[::-1] + left + [(tx, ty)]


def upper_teeth():
    """The crown: [(outline, point, root, kind)], drawn outer first (the middle teeth overlap their neighbours)."""
    def root_y(dx):
        return 38.0 + 0.0042 * dx * dx                      # the gum arches: higher at the middle

    teeth = []
    for side in (-1, 1):                                     # the canines: bow OUT, hook the point IN
        root = (CX + side * 81.0, root_y(81.0) - 8.0)
        tip = (CX + side * 80.0, 172.0)
        teeth.append((leaf(root, tip, 36.0, bow=side * 7.0, widest=0.30), tip, root, "canine"))
    for dx in (-48, 48, -24, 24, 0):                         # the inner teeth, outer to middle
        root = (CX + dx, root_y(dx))
        tip = (CX + dx - 0.06 * dx, UPPER_TIP_Y)
        teeth.append((leaf(root, tip, 30.0, widest=0.36), tip, root, "inner"))
    return teeth


def lower_teeth():
    teeth = []
    for side in (-1, 1):                                     # the tall end teeth
        root = (LCX + side * 58.0, 98.0)
        a = math.radians(0.0)
        tip = (root[0] + side * 64.0 * math.sin(a), root[1] - 64.0 * math.cos(a))
        teeth.append((leaf(root, tip, 26.0, widest=0.36), tip, root, "end"))
    for dx in (-36, 36, -12, 12):                            # the middle four, their points between the upper points
        root = (LCX + dx, 98.0)
        tip = (LCX + dx + 0.04 * dx, LOWER_TIP_Y)
        teeth.append((leaf(root, tip, 27.0, widest=0.36), tip, root, "middle"))
    return teeth


def mask(poly, w, h):
    im = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(im).polygon([(x * SS, y * SS) for x, y in poly], fill=255)
    return np.asarray(im.resize((w, h), Image.BOX)).astype(np.float32) / 255.0


def crisp_row(teeth, w, h):
    """The crisp row: each tooth a flat glow brightening toward its point, a thin dark seam where it overlaps the teeth
    already drawn, feathered edges, a soft halo; the roots dissolve into a faint gum haze."""
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    lum = np.zeros((h, w), np.float32)
    alpha = np.zeros((h, w), np.float32)
    for poly, tip, root, _ in teeth:
        m = mask(poly, w, h)
        rx, ry = root
        tx, ty = tip
        vx, vy = tx - rx, ty - ry
        t = np.clip(((xs - rx) * vx + (ys - ry) * vy) / (vx * vx + vy * vy), 0.0, 1.0)
        l = 0.80 + 0.20 * t ** 0.8                           # brighter toward the point
        seam = np.clip(blur(m, 1.4) * 1.6 - m, 0.0, 1.0) * alpha   # a dark seam only over the teeth behind
        lum = lum * (1.0 - 0.55 * seam)
        lum = lum * (1.0 - m) + l * m
        alpha = np.maximum(alpha, m)
    union = alpha.copy()
    edge = np.clip(union - blur(union, 1.2), 0.0, 1.0)
    lum = np.clip(lum + 0.10 * edge, 0.0, 1.0)             # a faint bright rim
    soft = blur(union, 0.7)                                  # feathered edges
    halo = blur(union, 6.0) * 0.32
    roots = np.zeros((h, w), np.float32)
    for poly, tip, root, _ in teeth:
        roots = np.maximum(roots, np.exp(-(((xs - root[0]) / 16.0) ** 2 + ((ys - root[1]) / 9.0) ** 2)))
    gum = blur(roots, 5.0) * 0.28
    a = np.clip(np.maximum(soft, np.maximum(halo, gum)), 0.0, 1.0)
    l = np.where(soft > 0.02, (lum * soft + 0.85 * np.maximum(halo, gum) * (1 - soft)) / np.maximum(a, 1e-3), 0.85)
    return np.clip(l, 0.0, 1.0), a, union


def row_states(teeth, w, h, salt):
    lum, alpha, union = crisp_row(teeth, w, h)
    wisp = value_noise(w, h, 9, salt) * 0.6 + value_noise(w, h, 4, salt + 1) * 0.4
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    edge_fade = smoothstep(0.0, 12.0, np.minimum(np.minimum(ys, h - 1 - ys), np.minimum(xs, w - 1 - xs)))
    out = []
    for k in range(STATES):
        c = k / (STATES - 1)
        r = 10.0 * (1.0 - c) ** 1.2
        a = blur(alpha, r)
        a = np.clip(a * (1.0 + 0.9 * (1.0 - c)), 0.0, 1.0) * (0.30 + 0.70 * c ** 1.1)
        spread = blur(union, 6.0 + 12.0 * (1.0 - c))                      # the mist the teeth condense from, wider early
        mist = np.clip((wisp - 0.38) * 2.4, 0.0, 1.0) * spread * (1.0 - c) ** 0.8 * 0.85
        aa = np.clip(np.maximum(a, mist), 0.0, 1.0) * edge_fade   # never cut by the cell's edge
        l = np.where(a >= mist, blur(lum, r * 0.6), 0.70)
        out.append(Image.fromarray((np.dstack([l, l, l, aa]) * 255).astype(np.uint8), "RGBA"))
    return out


def white(a, lum=None):
    l = np.ones_like(a) if lum is None else lum
    return Image.fromarray((np.dstack([l, l, l, np.clip(a, 0, 1)]) * 255).astype(np.uint8), "RGBA")


def star(size=256):
    """The flash: a small hot core, six fat rays (long and short in turn), a soft bloom that is mostly the rays'."""
    S = size * SS
    im = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(im)
    c = S / 2
    for k in range(6):
        ang = math.radians(20 + k * 60)
        long_ = k % 2 == 0
        ln = (0.47 if long_ else 0.31) * S
        w = (0.075 if long_ else 0.058) * S
        ux, uy = math.cos(ang), math.sin(ang)
        px, py = -uy, ux
        d.polygon([(c - ux * w * 0.6, c - uy * w * 0.6), (c + px * w, c + py * w), (c + ux * ln, c + uy * ln), (c - px * w, c - py * w)], fill=255)
    m = np.asarray(im.resize((size, size), Image.BOX)).astype(np.float32) / 255.0
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float32)
    r = np.hypot(xs - size / 2, ys - size / 2) / (size / 2)
    core = np.clip(1.0 - r / 0.18, 0.0, 1.0) ** 1.4
    bloom = np.clip(1.0 - r / 0.40, 0.0, 1.0) ** 2.6 * 0.30
    rays = blur(m, 1.0) * (1.0 - 0.45 * r)
    return white(np.maximum(np.maximum(rays, core), bloom))


def slash(w=512, h=96):
    """One tapered stroke, thickest at its middle, soft-edged, with a faint glow."""
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    t = xs / (w - 1)
    half = 0.5 * h * 0.34 * np.clip(np.sin(np.pi * t), 0.0, 1.0) ** 0.75
    dist = np.abs(ys - h / 2)
    core = smoothstep(half + 1.5, half - 1.5, dist)
    glow = blur(core, 5.0) * 0.45
    return white(np.maximum(core, glow))


def ring(size=256):
    """Two cells: the crisp ring (four tapered arcs with gaps, soft-edged, a faint glow) and the same ring SOFTENED into
    torn smoke, wider and broken by noise, for its outer edge to dissolve into the mist."""
    S = size * SS
    im = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(im)
    c = S / 2
    R = 0.40 * S
    for a0, span in zip([8, 104, 196, 282], [78, 70, 66, 64]):
        po, pi = [], []
        for k in range(33):
            t = k / 32
            a = math.radians(a0 + span * t)
            th = (0.030 * S) * math.sin(math.pi * t) ** 0.6
            po.append((c + (R + th) * math.cos(a), c + (R + th) * math.sin(a)))
            pi.append((c + (R - th) * math.cos(a), c + (R - th) * math.sin(a)))
        d.polygon(po + pi[::-1], fill=255)
    m = blur(np.asarray(im.resize((size, size), Image.BOX)).astype(np.float32) / 255.0, 1.0)
    crisp = np.maximum(m, blur(m, 6.0) * 0.55)
    n = value_noise(size, size, 8, 21) * 0.6 + value_noise(size, size, 3, 22) * 0.4
    soft = np.clip(blur(m, 7.0) * 3.2, 0.0, 1.0) * np.clip((n - 0.22) * 1.9, 0.0, 1.0)
    return white(np.hstack([crisp, np.clip(soft, 0.0, 1.0)]))


def streaks(size=512, n=STREAK_COUNT):
    """A dozen meaningful radial speed lines of mixed length (long and short in turn, a little jittered), starting out
    at the ring, tapered at both ends: an accent of force, never a pattern to count."""
    S = size * SS
    im = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(im)
    c = S / 2
    R = 0.5 * S
    r = np.random.default_rng(SEED + 7)
    for k in range(n):
        a = (k + 0.25 + r.random() * 0.5) / n * 2 * math.pi
        long_ = k % 2 == 0
        r0 = R * (0.42 + 0.10 * r.random())
        ln = R * ((0.30 + 0.16 * r.random()) if long_ else (0.13 + 0.10 * r.random()))
        w = SS * (1.3 + 1.0 * r.random())
        ux, uy = math.cos(a), math.sin(a)
        px, py = -uy, ux
        p0 = (c + ux * r0, c + uy * r0)
        p1 = (c + ux * (r0 + ln), c + uy * (r0 + ln))
        mid = (c + ux * (r0 + 0.4 * ln), c + uy * (r0 + 0.4 * ln))
        d.polygon([p0, (mid[0] + px * w, mid[1] + py * w), p1, (mid[0] - px * w, mid[1] - py * w)], fill=int(190 + 65 * r.random()))
    return white(np.asarray(im.resize((size, size), Image.BOX)).astype(np.float32) / 255.0)


def shards(size=256):
    """Torn splinters of the broken teeth in six clusters around a ring: curled, jagged, a few px wide."""
    S = size * SS
    im = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(im)
    c = S / 2
    r = np.random.default_rng(SEED + 11)
    for k in range(6):
        a = (k + 0.35 * r.random()) / 6 * 2 * math.pi
        rad = S * (0.37 + 0.05 * r.random())
        cx, cy = c + rad * math.cos(a), c + rad * math.sin(a)
        tang = a + math.pi / 2
        for j in range(3):
            ln = S * (0.075 + 0.05 * r.random())
            off = (j - 1) * S * 0.028
            ox, oy = cx + off * math.cos(a), cy + off * math.sin(a)
            ang = tang + (r.random() - 0.5) * 0.9
            ux, uy = math.cos(ang), math.sin(ang)
            px, py = -uy, ux
            w = S * (0.010 + 0.010 * r.random())
            pts_l, pts_r = [], []
            for q in range(7):                               # a curled, jagged splinter thinning to both ends
                t = q / 6
                bend = math.sin(math.pi * t) * S * 0.018 * (1 if j % 2 else -1)
                zig = (S * 0.006) * (1 if q % 2 else -1)
                x = ox + ux * ln * (t - 0.5) + px * (bend + zig)
                y = oy + uy * ln * (t - 0.5) + py * (bend + zig)
                hw = w * math.sin(math.pi * t) ** 0.8
                pts_l.append((x - px * hw, y - py * hw))
                pts_r.append((x + px * hw, y + py * hw))
            d.polygon(pts_l + pts_r[::-1], fill=int(185 + 70 * r.random()))
    m = blur(np.asarray(im.resize((size, size), Image.BOX)).astype(np.float32) / 255.0, 0.6)
    crisp = np.maximum(m, blur(m, 3.0) * 0.4)
    soft = np.clip(blur(m, 2.6) * 1.5, 0.0, 1.0) * 0.8                     # the same splinters, softened: Shadow fragments
    lum = np.clip(0.6 + 0.4 * m, 0, 1)
    return white(np.hstack([crisp, soft]), np.hstack([lum, np.full_like(lum, 0.8)]))


def smoke(size=256):
    """A soft, irregular smoke lobe: densest a little off centre, its edge torn by two octaves of noise and feathered
    wide (three of these, turned apart, make the one mist volume; a disc with a ragged rim read as a puff)."""
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float32)
    cx, cy = size * 0.47, size * 0.52
    r = np.hypot((xs - cx) / (size * 0.5), (ys - cy) / (size * 0.44))
    n = value_noise(size, size, 9, 3) * 0.55 + value_noise(size, size, 4, 4) * 0.30 + value_noise(size, size, 18, 5) * 0.15
    edge = 0.84 + 0.22 * (n - 0.5) * 2.0
    a = smoothstep(edge, edge - 0.45, r) * (0.62 + 0.38 * n)          # a broad soft body (a tiny core was hidden by the creature)
    return white(blur(a, 2.0))


def strip(states, w, h):
    out = Image.new("RGBA", (w * len(states), h), (0, 0, 0, 0))
    for k, s in enumerate(states):
        out.alpha_composite(s, (k * w, 0))
    return out


def sheet(parts, path):
    """Every part over grey (the dungeon floor's value) and over black."""
    width = max(im.width for _, im in parts) + 20
    height = sum(im.height + 16 for _, im in parts) + 10
    out = Image.new("RGB", (width * 2, height), (0, 0, 0))
    for side, bg in enumerate([(58, 56, 54), (10, 8, 14)]):
        y = 5
        for _, im in parts:
            tile = Image.new("RGBA", (width, im.height + 16), bg + (255,))
            tile.alpha_composite(im, (10, 8))
            out.paste(tile.convert("RGB"), (side * width, y))
            y += im.height + 16
    out.save(path)


def main():
    os.makedirs(OUT, exist_ok=True)
    up = upper_teeth()
    lo = lower_teeth()
    upper_states = row_states(up, UW, UH, 101)
    lower_states = row_states(lo, LW, LH, 202)
    strip(upper_states, UW, UH).save(os.path.join(OUT, "fxp_seeker_bite_upper.png"))
    strip(lower_states, LW, LH).save(os.path.join(OUT, "fxp_seeker_bite_lower.png"))
    parts = {"star": star(), "ring": ring(), "streaks": streaks(n=STREAK_COUNT), "shards": shards(), "smoke": smoke()}
    # the SLASH was removed in the shadow-mist polish (a directional stroke in a radial burst read as a blade: JAWS is a
    # bite); it is written to history only
    slash().save(os.path.join(HISTORY, "fxp_seeker_bite_slash.png"))
    for name, im in parts.items():
        im.save(os.path.join(OUT, f"fxp_seeker_bite_{name}.png"))
    inner = [t for _, t, _, kind in up if kind == "inner"]
    canines = [t for _, t, _, kind in up if kind == "canine"]
    middle = [t for _, t, _, kind in lo if kind == "middle"]
    ends = [t for _, t, _, kind in lo if kind == "end"]

    def width_of(teeth):
        xs = [p[0] for poly, _, _, _ in teeth for p in poly]
        return round(max(xs) - min(xs), 1)

    spans = {
        "states": STATES,
        "upper_cell": [UW, UH], "lower_cell": [LW, LH],
        "upper_centre_x": CX, "lower_centre_x": LCX,
        "upper_inner_tip_y": round(max(t[1] for t in inner), 1),     # the inner points' line: the crown's bite line
        "upper_inner_tip_y_all": [round(t[1], 1) for t in sorted(inner)],
        "upper_canine_tip_y": round(max(t[1] for t in canines), 1),
        "upper_inner_tip_x": [round(t[0], 1) for t in sorted(inner)],
        "upper_canine_tip_x": [round(t[0], 1) for t in sorted(canines)],
        "lower_tip_y": round(min(t[1] for t in middle), 1),           # the middle points' line: the row's bite line
        "lower_tip_y_all": [round(t[1], 1) for t in sorted(middle)],
        "lower_tip_x": [round(t[0], 1) for t in sorted(middle)],
        "lower_end_tip_x": [round(t[0], 1) for t in sorted(ends)],
        "lower_end_tip_y": [round(t[1], 1) for t in sorted(ends)],
        "upper_width": width_of(up),
        "lower_width": width_of(lo),
        "part_sizes": {name: [im.width, im.height] for name, im in parts.items()},
        "streak_count": STREAK_COUNT,
        "two_cell_parts": ["ring", "shards"],
    }
    with open(os.path.join(SRC, "seeker_bite_spans.json"), "w", encoding="utf-8") as f:
        json.dump(spans, f, indent=2)
    print("wrote seeker_bite_spans.json", spans)
    sheet([("upper", strip(upper_states, UW, UH)), ("lower", strip(lower_states, LW, LH))]
          + [(k, v) for k, v in parts.items()], os.path.join(SRC, "seeker_bite_sheet.png"))
    print("wrote", os.path.join(SRC, "seeker_bite_sheet.png"))


if __name__ == "__main__":
    main()
