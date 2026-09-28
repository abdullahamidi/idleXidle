#!/usr/bin/env python3
"""seeker_maw.py -- the Seeker's JAWS source art: a SHADOW MAW made of mist, two jaw pieces in sixteen condensation states
(the owner, 2026-09-28: "the teeth should not be four triangles coming to the middle: a jaw that really opens and
closes, a piranha jaw, a bite").

    python tools/asset-pipeline/v2/seeker_maw.py [variant]          # writes the runtime strip (default variant: piranha)
    python tools/asset-pipeline/v2/seeker_maw.py --compare <frame.png> <x,y,w,h>   # the variants composited on a real frame

The maw is seen from the SIDE, like a fish's jaws: an UPPER jaw (a thick, arched jaw whose biting edge carries a row of
sharp triangular teeth pointing down) and a LOWER jaw (the same, mirrored, teeth pointing up, offset by half a tooth so
the rows INTERLOCK when shut, and a little longer: a piranha's underbite). Each piece turns about its HINGE, the back
corner of the mouth, so the runtime opens the mouth by turning the upper jaw up and the lower jaw down, and bites by
turning both shut.

Each piece is not a hard sprite: it is a smoky density field that CONDENSES, like the misty fang before it (the same
approach, seeker_mist_fangs.py): state 0 is a loose drift of violet smoke where the jaw will be; each later state
tightens the field to the jaw's silhouette, sharpens its edge and brings up a soft lavender rim along the jaw and its
smoky lavender teeth; only the last states HARDEN (a crisper, brighter rim and teeth: the runtime shows them on the bite
frames); state 15 is the hardened jaw, still smoky along its back (the mist it came from). The fill is a DARKENING
Shadow veil with smoke moving inside it, never a flat lightening pane (that read as glass). The
noise field is FIXED across the states (one seed), so stepping through them reads as the same mist condensing. No
PixelLab.

    assets/art/VFX/parts/fxp_seeker_maw.png   16 states x (W x H), the upper jaw in the top row and the lower jaw in the
                                              bottom row; each piece's HINGE at (HINGE_X, EDGE_Y) of its cell; straight
                                              alpha (the asset library premultiplies)
    tools/asset-pipeline/v2/keypose_sources/seeker_maw_sheet.png    the states of both pieces, on grey and on black
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "tools", "asset-pipeline", "v2", "keypose_sources", "history")   # RETIRED (2026-09-28): the side-view maw was rejected for a FRONTAL bite (seeker_bite.py); this strip is history and never played
SRC = os.path.join(HERE, "keypose_sources")

STATES = 16
SS = 3
W, H = 320, 176            # one piece's cell
HINGE_X, EDGE_Y = 44.0, 104.0   # the hinge (the back corner of the mouth) and the biting edge's height, in each cell

BODY = np.array([42.0, 25.0, 68.0])        # the condensed jaw's fill: a dark violet Shadow MASS (a lightening lavender read as glass, a thin veil vanished)
MIST_BODY = np.array([70.0, 50.0, 110.0])  # the loose smoke it gathers from: a violet smoke that is seen
EDGE_SOFT = np.array([120.0, 106.0, 170.0])  # the outline while the jaw is still smoky
EDGE = np.array([150.0, 134.0, 206.0])     # the outline of the hardened jaw (the bite frames only)
TOOTH_SOFT = np.array([104.0, 92.0, 140.0])  # the teeth while the jaw is still smoky
TOOTH = np.array([168.0, 152.0, 218.0])    # the teeth of the hardened jaw: they catch the light on the bite (still under a creature's eyes; no white)
WISP = np.array([96.0, 72.0, 150.0])
LIP = np.array([16.0, 8.0, 28.0])          # the dark lip along each gum: the shut mouth shows TWO jaws meeting (one zigzag strip read as a zipper)
LOWER_TEETH_SHADE = 0.84                   # the lower row a shade darker than the upper, so the two rows read apart

VARIANTS = {
    # L: jaw length (hinge to snout); d0: depth behind the teeth; back: the rounded cheek behind the hinge; n: teeth;
    # th: the front tooth's height; tx0/tx1: the toothed span (shares of L); under: the lower jaw's extra length (the
    # underbite); lower_th: the lower teeth against the upper; hook: how far the upper snout curls down (the lower chin
    # turns up by the same measure); taper: how the jaw thins toward the snout; brow: the upper jaw's convex brow (share of L);
    # snout: the blunt snout's depth (share of d0; 0 = a needle point, which read as a beak); grade0: the back tooth's size
    # against the front one (0.5: the front fangs twice the back ones, so the shut rows never read as a zipper)
    "piranha": dict(L=232, d0=70, back=26, n=5, th=38, tx0=0.40, tx1=0.97, under=1.13, lower_th=1.15, hook=16, taper=1.3, brow=0.08,
                    snout=0.26, grade0=0.5),
    "croc":    dict(L=250, d0=48, back=26, n=8, th=24, tx0=0.26, tx1=0.95, under=1.03, lower_th=1.0, hook=8, taper=1.2, brow=0.03),
    "beast":   dict(L=220, d0=76, back=34, n=4, th=42, tx0=0.36, tx1=0.92, under=1.00, lower_th=1.0, hook=22, taper=1.8, brow=0.06),
}


def jaw_polygon(v, lower):
    """The jaw piece's outline and its teeth, in cell px: a BEAK, thick at its rounded cheek where it hinges, a convex
    brow, tapering to a snout; its biting edge carries a row of sharp teeth on its FRONT part only, largest at the snout
    and shrinking toward the hinge, raked back, never longer than ~70 % of the other jaw's depth (so every tooth sits
    inside the shut silhouette). Drawn as the UPPER jaw (body above EDGE_Y, teeth hanging below); the lower jaw is built
    the same way, longer (the underbite), with bigger teeth half a pitch along (the rows interlock), and mirrored."""
    L = v["L"] * (v["under"] if lower else 1.0)
    d0 = v["d0"] * (0.85 if lower else 1.0)
    other_depth = v["d0"] * (1.0 if lower else 0.85)
    th = min(v["th"] * (v["lower_th"] if lower else 1.0), 0.7 * other_depth)
    hook = v["hook"] * (0.6 if lower else 1.0)
    brow = v["brow"] * v["L"] * (0.5 if lower else 1.0)

    def edge_y(t):
        return EDGE_Y + hook * t ** 3

    pts = []
    # the cheek: a round behind the hinge, from the biting line up to the top of the jaw
    for k in range(13):
        a = k / 12.0 * math.pi / 2
        pts.append((HINGE_X - v["back"] * math.cos(a), EDGE_Y - d0 * math.sin(a)))
    # the top profile: a convex brow, tapering to the snout
    for k in range(1, 49):
        t = k / 48.0
        blunt = v.get("snout", 0.0)
        depth = d0 * ((1.0 - blunt) * (1.0 - t) ** (1.0 / v["taper"]) + blunt) + brow * math.sin(math.pi * min(1.0, t / 0.85)) * (1.0 - t) ** 0.3
        pts.append((HINGE_X + t * L, edge_y(t) - depth))
    if v.get("snout", 0.0) > 0.0:
        pts.append((HINGE_X + L, edge_y(1.0)))              # the blunt snout's front face, down to the biting edge
    # the teeth along the front of the biting edge, snout to hinge; graded, raked back. Both rows are laid out in ABSOLUTE
    # px along the UPPER jaw's length, the lower row half a tooth along: the rows interlock whatever the underbite (a
    # share of each jaw's own length slid the lower row back into step with the upper), six teeth each
    Lu = v["L"]
    t0, t1, n = v["tx0"], v["tx1"], v["n"]
    pitch = (t1 - t0) * Lu / n                              # px
    start = t0 * Lu + (0.5 * pitch if lower else 0.0)
    teeth, tips, edge = [], [], []
    for i in reversed(range(n)):
        a_px = start + i * pitch
        b_px = a_px + pitch
        if b_px > L - 4.0:
            continue
        a, b = a_px / L, b_px / L                            # shares of THIS jaw's length (the edge's curve)
        m = 0.5 * (a + b)
        g0 = v.get("grade0", 0.6)
        grade = g0 + (1.0 - g0) * (0.5 * (a_px + b_px) - t0 * Lu) / max(1e-6, (t1 - t0) * Lu)   # grade0 at the back, 1.0 at the snout
        size = th * min(1.0, grade)
        tip = (HINGE_X + m * L - 0.18 * pitch, edge_y(m) + size)
        tri = [(HINGE_X + b * L, edge_y(b)), tip, (HINGE_X + a * L, edge_y(a))]
        edge += tri
        teeth.append(tri)
        tips.append(tip)
    pts += edge
    pts.append((HINGE_X, EDGE_Y))
    if lower:
        pts = [(x, 2 * EDGE_Y - y) for x, y in pts]
        teeth = [[(x, 2 * EDGE_Y - y) for x, y in tri] for tri in teeth]
        tips = [(x, 2 * EDGE_Y - y) for x, y in tips]
    return pts, tips, teeth


def mask_of(poly):
    im = Image.new("L", (W * SS, H * SS), 0)
    ImageDraw.Draw(im).polygon([(x * SS, y * SS) for x, y in poly], fill=255)
    return np.asarray(im.resize((W, H), Image.BOX)).astype(np.float32) / 255.0


def signed_distance(mask):
    inside = mask >= 0.5
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    pad = np.pad(inside, 1, constant_values=False)
    edge = inside & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
    ey, ex = np.nonzero(edge)
    d = np.full((H, W), 1e9, np.float32)
    for k in range(0, len(ey), 48):
        dy = ys[..., None] - ey[None, None, k:k + 48]
        dx = xs[..., None] - ex[None, None, k:k + 48]
        d = np.minimum(d, np.sqrt(dx * dx + dy * dy).min(axis=2))
    return np.where(inside, -d, d)


def value_noise(rng, cells):
    ch, cw = max(2, int(H / cells)), max(2, int(W / cells))
    lattice = (rng.random((ch + 1, cw + 1)) * 255).astype(np.uint8)
    return np.asarray(Image.fromarray(lattice, "L").resize((W, H), Image.BICUBIC)).astype(np.float32) / 255.0


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def state(c, sd, back, rear, up, teeth, n_body, n_wisp, n_fine, lower=False):
    """One condensation state of a piece. `back` is 0 at the biting edge and 1 at the jaw's back (the smoky side);
    `rear` is 0 over the toothed front half and 1 at the cheek behind the hinge (where the outline fades into smoke);
    `up` is 0 at the biting edge and 1 across the jaw's outer side (which stays smoke: soft and ragged, never a pane's
    straight edge); `teeth` is the teeth's own coverage (0..1)."""
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    inflate = 10.0 * (1.0 - c) ** 1.2                    # the gathering smoke stays near the jaw it becomes
    smoky_side = 1.0 + 1.6 * up                              # the outer side stays smoke; the biting edge is the crisp part
    ragged = (13.0 * (1.0 - c) ** 1.1 + 1.5) * (n_body - 0.5) * 2.0 * smoky_side
    soft = (7.0 * (1.0 - c) ** 1.4 + 0.8) * smoky_side
    d = sd - inflate + ragged
    core = 1.0 / (1.0 + np.exp(d / soft))
    core = core * (1.0 - 0.35 * back * (0.6 + 0.4 * n_fine) * (1.0 - 0.6 * c))   # the back is smoke, less so once condensed
    density = core * (0.62 + 0.38 * c ** 0.8)             # the loose smoke dense enough to be SEEN
    around = np.exp(-np.maximum(sd, 0.0) / 14.0) * (sd > -2.0)
    wisps = np.clip((n_wisp - 0.5) * 3.0, 0.0, 1.0) * around * (1.0 - c) ** 1.4 * 0.7
    hard = smoothstep(0.85, 1.0, c)                                  # the jaw hardens only in its last states
    rim_band = smoothstep(-7.0 + 3.0 * hard, -1.0, d) * (d < 1.5) * (0.45 + 0.35 * hard)   # feathered and dim, crisp only when hard
    rim = rim_band * smoothstep(0.30, 1.0, c) * (1.0 - 0.5 * back) * (1.0 - 0.8 * rear)   # no closed outline: the back is smoke
    rim = rim * (0.35 + 0.65 * smoothstep(0.30, 0.70, n_wisp))       # broken by the smoke, never a clean glass edge
    rim = rim * (1.0 - smoothstep(0.0, 0.35, up) * 0.0) * (1.0 - (1.0 - smoothstep(0.0, 0.35, up)) * 0.85)   # no light line on the gum: the lip is there
    tooth = teeth * smoothstep(0.30, 1.0, c)
    # the fill: see-through (~0.45 at the bite) all along the jaw; the outline and the teeth solid
    fill = density * 0.52 * (0.7 + 0.6 * n_fine)              # the mass, with smoke moving inside it (never a flat pane)
    fill = fill * (1.0 - rear * (0.25 + 0.55 * n_body))        # the back comes apart into a ragged plume (never a blurred box)
    alpha = np.maximum(fill, np.maximum(rim * density * 1.2, tooth * 0.85))
    alpha = np.clip(np.maximum(alpha, wisps), 0.0, 1.0)
    body = MIST_BODY * (1.0 - c ** 0.7) + BODY * c ** 0.7
    rgb = body[None, None] * np.ones((H, W, 1))
    wisp_share = np.clip(wisps / np.maximum(alpha, 1e-4), 0.0, 1.0)[..., None]
    rgb = rgb * (1.0 - wisp_share) + WISP * wisp_share
    tooth_rgb = (TOOTH_SOFT * (1.0 - hard) + TOOTH * hard) * (LOWER_TEETH_SHADE if lower else 1.0)
    lip = smoothstep(-11.0, -3.0, sd) * (sd < 0.0) * (1.0 - smoothstep(0.0, 0.35, up)) * (1.0 - np.clip(teeth * 4.0, 0.0, 1.0))
    lip = lip * 0.75 * smoothstep(0.45, 1.0, c) * (1.0 - rear)
    edge_rgb = EDGE_SOFT * (1.0 - hard) + EDGE * hard
    rgb = rgb * (1.0 - lip[..., None]) + LIP * lip[..., None]
    alpha = np.maximum(alpha, lip * density)
    rgb = rgb * (1.0 - tooth[..., None]) + tooth_rgb * tooth[..., None]
    rgb = rgb * (1.0 - rim[..., None]) + edge_rgb * rim[..., None]
    edge = np.minimum.reduce([xs, W - 1 - xs, ys, H - 1 - ys]) / 16.0
    alpha = alpha * smoothstep(0.0, 1.0, edge)
    return np.dstack([rgb / 255.0, alpha[..., None]])


def piece_states(v, lower, seed):
    poly, tips, teeth = jaw_polygon(v, lower)
    sd = signed_distance(mask_of(poly))
    tm = Image.new("L", (W * SS, H * SS), 0)
    dr = ImageDraw.Draw(tm)
    for tri in teeth:
        dr.polygon([(x * SS, y * SS) for x, y in tri], fill=255)
    teeth_mask = np.asarray(tm.resize((W, H), Image.BOX)).astype(np.float32) / 255.0
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    # the jaw's BACK is smoke: up the jaw away from the biting edge, and the cheek behind the hinge
    up = smoothstep(0.0, 1.0, ((EDGE_Y - ys) if not lower else (ys - EDGE_Y)) / max(1.0, v["d0"]))
    heel = smoothstep(HINGE_X, HINGE_X - v["back"], xs)
    back = np.maximum(up, heel)
    rear = smoothstep(HINGE_X + 0.55 * v["L"], HINGE_X - v["back"], xs)   # 0 over the toothed front, 1 at the cheek
    rng = np.random.default_rng(seed)
    n_body = 0.6 * value_noise(rng, 14) + 0.4 * value_noise(rng, 6)
    n_wisp = 0.65 * value_noise(rng, 18) + 0.35 * value_noise(rng, 7)
    n_fine = value_noise(rng, 5)
    out = []
    for k in range(STATES):
        c = k / (STATES - 1)
        im = Image.fromarray(np.clip(state(c, sd, back, rear, up, teeth_mask, n_body, n_wisp, n_fine, lower) * 255.0, 0, 255).astype(np.uint8), "RGBA")
        radius = 2.0 * (1.0 - c) ** 1.5
        if radius > 0.2:
            im = im.filter(ImageFilter.GaussianBlur(radius))
        out.append(im)
    return out


def build(variant):
    v = VARIANTS[variant]
    upper = piece_states(v, False, 0x3A11)
    lower = piece_states(v, True, 0x3A12)
    strip = Image.new("RGBA", (W * STATES, H * 2), (0, 0, 0, 0))
    for k in range(STATES):
        strip.alpha_composite(upper[k], (k * W, 0))
        strip.alpha_composite(lower[k], (k * W, H))
    return strip


def piece(strip, k, lower):
    return strip.crop((k * W, H if lower else 0, (k + 1) * W, (2 if lower else 1) * H))


def place(canvas, im, hinge, angle_deg, scale):
    """Composite a piece onto `canvas` with its hinge at `hinge`, turned by angle (degrees, screen: + is clockwise) and scaled."""
    im = im.resize((int(W * scale), int(H * scale)), Image.LANCZOS)
    hx, hy = HINGE_X * scale, EDGE_Y * scale
    big = Image.new("RGBA", (im.width * 3, im.height * 3), (0, 0, 0, 0))
    big.alpha_composite(im, (int(im.width * 1.5 - hx), int(im.height * 1.5 - hy)))
    rot = big.rotate(-angle_deg, resample=Image.BICUBIC, center=(im.width * 1.5, im.height * 1.5))
    canvas.alpha_composite(rot, (int(hinge[0] - im.width * 1.5), int(hinge[1] - im.height * 1.5)))


def compare(frame_path, box):
    """Each variant, condensed, composited on a real frame at three poses: OPEN, CLOSING, SHUT; plus a loose-mist pose."""
    x, y, bw, bh = box
    base = Image.open(frame_path).convert("RGBA")
    rows = []
    for name in VARIANTS:
        strip = build(name)
        v = VARIANTS[name]
        length = 0.58 * bw
        scale = length / v["L"]
        hinge = (x - 0.45 * length, y + 0.45 * bh)
        tilt = 10
        cells = []
        for label, k, up, lo in (("MIST, gathering", 3, -16, 20), ("OPEN (wide)", 11, -26, 32), ("CLOSING", 14, -12, 14), ("SHUT (bite)", 15, -4, 4)):
            frame = base.copy()
            place(frame, piece(strip, k, False), hinge, up + tilt, scale)
            place(frame, piece(strip, k, True), hinge, lo + tilt, scale)
            crop = frame.crop((x - int(0.45 * bw), y - int(0.55 * bh), x + int(1.35 * bw), y + int(1.45 * bh))).convert("RGB")
            ImageDraw.Draw(crop).text((6, 4), f"{name}: {label}", fill=(255, 210, 120))
            cells.append(crop)
        row = Image.new("RGB", (sum(c.width + 6 for c in cells), cells[0].height), (20, 17, 14))
        xx = 0
        for c in cells:
            row.paste(c, (xx, 0))
            xx += c.width + 6
        rows.append(row)
    sheet = Image.new("RGB", (rows[0].width, sum(r.height + 6 for r in rows)), (20, 17, 14))
    yy = 0
    for r in rows:
        sheet.paste(r, (0, yy))
        yy += r.height + 6
    out = os.path.join(SRC, "seeker_maw_variants.png")
    sheet.save(out)
    print("wrote", out, sheet.size)


def main():
    if len(sys.argv) > 1 and sys.argv[1] == "--compare":
        compare(sys.argv[2], tuple(int(t) for t in sys.argv[3].split(",")))
        return
    variant = sys.argv[1] if len(sys.argv) > 1 else "piranha"
    os.makedirs(OUT, exist_ok=True)
    strip = build(variant)
    path = os.path.join(OUT, "fxp_seeker_maw.png")
    strip.save(path)
    print("wrote", path, strip.size, "variant", variant)
    v = VARIANTS[variant]
    _, up_tips, _ = jaw_polygon(v, False)
    _, lo_tips, _ = jaw_polygon(v, True)
    spans = {"states": STATES, "cell": [W, H], "hinge": [HINGE_X, EDGE_Y], "jaw_length": v["L"],
             "lower_jaw_length": round(v["L"] * v["under"], 1), "gum_hook": [v["hook"], round(v["hook"] * 0.6, 1)],
             "upper_tip_x": sorted(round(x, 1) for x, _ in up_tips), "lower_tip_x": sorted(round(x, 1) for x, _ in lo_tips),
             "upper_tip_y": [round(y, 1) for _, y in sorted(up_tips)], "lower_tip_y": [round(y, 1) for _, y in sorted(lo_tips)]}
    with open(os.path.join(SRC, "seeker_maw_spans.json"), "w", encoding="utf-8") as f:
        json.dump(spans, f, indent=2)
    print("wrote seeker_maw_spans.json", spans)
    big = strip.resize((strip.width // 2, strip.height // 2), Image.LANCZOS)
    sheet = Image.new("RGB", (big.width + 20, big.height * 2 + 30), (40, 36, 44))
    for row, bg in enumerate(((70, 67, 72), (10, 8, 12))):
        b = Image.new("RGBA", big.size, bg + (255,))
        b.alpha_composite(big)
        sheet.paste(b.convert("RGB"), (10, 10 + row * (big.height + 10)))
    sheet.save(os.path.join(SRC, "seeker_maw_sheet.png"))
    print("wrote", os.path.join(SRC, "seeker_maw_sheet.png"))


if __name__ == "__main__":
    main()
