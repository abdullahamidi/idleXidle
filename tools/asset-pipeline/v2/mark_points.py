#!/usr/bin/env python3
"""mark_points.py -- the MARK's BODY POINT on every creature strip frame (ADR-011, the MARK reference, BRAND): where a
brand sits on that creature, authored once and reviewed on a contact sheet, never guessed at runtime.

A luma heuristic (the eyes are the brightest texels in a dark face) put the coil on the head, the legs, a staff or a
spike tip on the families it was not tuned on, and pressed it against the NEXT creature's face in the pack's round-head
pose. So each strip gets a SEED on its first frame (a reviewed fraction of the frame, below), and the point is TRACKED
frame to frame by matching the body patch round it (alpha and luma), so it stays on the same anatomical spot through
the breath and the lunge; then it is KEPT ON THE BODY: if the coil's footprint (its box at the runtime size, a share of
the creature's visible height) is not 88 % over the creature's own opaque pixels, it moves to the nearest point that is.

    PYTHONUTF8=1 python tools/asset-pipeline/v2/mark_points.py            # writes <strip>.mark.json beside every strip
    PYTHONUTF8=1 python tools/asset-pipeline/v2/mark_points.py <out dir>   # contact sheets only (review)

The runtime reads the files (MarkPoints.cs); brand_mark_test.cs checks every strip has one, every point is on the body.
"""
import glob
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ANIM = os.path.join(REPO, "assets", "art", "Animations")

SIZE_SHARE = 0.28        # MarkRecipe.SizeShare: the coil's box as a share of the creature's visible height
COIL_W, COIL_H = 1.0, 0.80   # the coil's own box is wider than tall (seeker_brand.py SQUASH), its footprint an ellipse
ON_BODY = 0.88           # the share of the footprint that must lie on the creature's own opaque pixels (sought)
# THE DRAWN MARK with its smoky roots, at the largest scale the snap to thirds can give (in game on a whelp the halo
# spans ~0.31 x 0.26 of the visible height): this whole footprint must stay clear of the creature's HEAD box
HALO_W, HALO_H = 0.42, 0.35   # MarkRecipe.HaloWidthShare / HaloHeightShare (the tests measure them on the atlas)

# THE HEAD (the face, its eyes, a helmet, a visor, a bell): a box per frame, reviewed on the contact sheet. By default
# the top 28 % of an upright creature's visible body; the creatures whose head is at the FRONT get a box in the frame
# (the same on every frame, or one per frame where the pose moves it); none for the crystal glyph.
HEAD_TOP = 0.28
HEADS = {
    "umbral_swarm_idle_strip8_512": [(0.08, 0.33, 0.35, 0.62)] * 8,
    "umbral_swarm_attack_strip8_512": [(0.28, 0.52, 0.48, 0.71), (0.22, 0.57, 0.43, 0.76), (0.22, 0.57, 0.43, 0.76),
                                       (0.07, 0.55, 0.27, 0.71), (0.04, 0.55, 0.23, 0.71), (0.10, 0.51, 0.31, 0.69),
                                       (0.08, 0.66, 0.31, 0.84), (0.28, 0.52, 0.48, 0.71)],
    "choir_swarm_idle_strip8_512": [(0.12, 0.10, 0.40, 0.47)] * 8,
    "choir_swarm_attack_strip8_512": [(0.12, 0.12, 0.42, 0.48)] * 8,
    "verdant_swarm_idle_strip8_512": [(0.16, 0.56, 0.42, 0.84)] * 8,
    "verdant_swarm_attack_strip8_512": [(0.14, 0.56, 0.40, 0.84)] * 8,
    "marrow_swarm_idle_strip8_512": [(0.16, 0.58, 0.40, 0.84)] * 8,
    "marrow_swarm_attack_strip8_512": [(0.14, 0.60, 0.38, 0.86)] * 8,
    "cinder_swarm_idle_strip8_512": [(0.26, 0.36, 0.43, 0.55)] * 8,
    "cinder_swarm_attack_strip8_512": [(0.26, 0.36, 0.44, 0.55)] * 8,
    "archive_swarm_idle_strip8_512": [],
    "archive_swarm_attack_strip8_512": [],
    "thorn_regent_idle_strip8_512": [(0.37, 0.24, 0.56, 0.45)] * 8,
    "thorn_regent_attack_strip8_512": [(0.37, 0.24, 0.56, 0.45)] * 8,
    "lumen_angel_idle_strip8_512": [(0.37, 0.18, 0.53, 0.40)] * 8,
    "lumen_angel_attack_strip8_512": [(0.36, 0.18, 0.53, 0.40)] * 8,
    "umbral_swarm_death_strip8_512": [(0.10, 0.34, 0.37, 0.60)] * 8,
    # the forge colossus is wider than tall: its head is only the middle of its top band (the shoulders are body)
    "forge_colossus_idle_strip8_512": [(0.33, 0.17, 0.53, 0.41)] * 8,
    "forge_colossus_attack_strip8_512": [(0.33, 0.17, 0.53, 0.41)] * 8,
}

# THE SEEDS: (x, y) on frame 0 as fractions of the frame, reviewed on the contact sheet. The torso: below the head and
# its eyes, forward (toward the champion, left) of the body's middle, where the creature behind never presses a face;
# never over a glowing core or emblem (lit through the coil's hollow it reads as an iris: the Cinder armoured's core, the
# Cinder wasp's abdomen, so those sit on the belly below the core and on the thorax).
SEEDS = {
    # death clips: the whelp's on its mid-back at the live coil's height (higher, the loosen sat level with the NEXT
    # whelp's eye as the row closed up); the Cinder armoured's below its furnace core (as its attack's points are)
    "umbral_swarm_death_strip8_512": (0.51, 0.645),
    "cinder_armoured_death_strip8_512": (0.46, 0.77),
    "umbral_armoured_attack_strip8_512": (0.47, 0.58),
    "umbral_armoured_idle_strip8_512": (0.49, 0.58),
    "umbral_bruiser_attack_strip8_512": (0.53, 0.55),
    "umbral_bruiser_idle_strip8_512": (0.50, 0.56),
    "umbral_caster_attack_strip8_512": (0.47, 0.47),
    "umbral_caster_idle_strip8_512": (0.48, 0.47),
    "umbral_swarm_attack_strip8_512": (0.44, 0.62),
    "umbral_swarm_idle_strip8_512": (0.46, 0.54),
    "choir_armoured_attack_strip8_512": (0.44, 0.60),
    "choir_armoured_idle_strip8_512": (0.45, 0.60),
    "choir_bruiser_attack_strip8_512": (0.52, 0.57),
    "choir_bruiser_idle_strip8_512": (0.47, 0.47),
    "choir_caster_attack_strip8_512": (0.45, 0.60),
    "choir_caster_idle_strip8_512": (0.47, 0.55),
    "choir_swarm_attack_strip8_512": (0.34, 0.63),
    "choir_swarm_idle_strip8_512": (0.33, 0.62),
    "archive_armoured_attack_strip8_512": (0.48, 0.58),
    "archive_armoured_idle_strip8_512": (0.48, 0.47),
    "archive_bruiser_attack_strip8_512": (0.46, 0.47),
    "archive_bruiser_idle_strip8_512": (0.47, 0.45),
    "archive_caster_attack_strip8_512": (0.48, 0.47),
    "archive_caster_idle_strip8_512": (0.50, 0.45),
    "archive_swarm_attack_strip8_512": (0.56, 0.66),
    "archive_swarm_idle_strip8_512": (0.52, 0.55),
    "cinder_armoured_attack_strip8_512": (0.53, 0.71),
    "cinder_armoured_idle_strip8_512": (0.46, 0.68),
    "cinder_bruiser_attack_strip8_512": (0.47, 0.50),
    "cinder_bruiser_idle_strip8_512": (0.45, 0.45),
    "cinder_caster_attack_strip8_512": (0.47, 0.64),
    "cinder_caster_idle_strip8_512": (0.47, 0.62),
    "cinder_swarm_attack_strip8_512": (0.50, 0.46),
    "cinder_swarm_idle_strip8_512": (0.48, 0.45),
    "marrow_armoured_attack_strip8_512": (0.45, 0.63),
    "marrow_armoured_idle_strip8_512": (0.46, 0.62),
    "marrow_bruiser_attack_strip8_512": (0.47, 0.48),
    "marrow_bruiser_idle_strip8_512": (0.48, 0.47),
    "marrow_caster_attack_strip8_512": (0.55, 0.57),
    "marrow_caster_idle_strip8_512": (0.53, 0.53),
    "marrow_swarm_attack_strip8_512": (0.58, 0.55),
    "marrow_swarm_idle_strip8_512": (0.62, 0.55),
    "verdant_armoured_attack_strip8_512": (0.38, 0.62),
    "verdant_armoured_idle_strip8_512": (0.40, 0.62),
    "verdant_bruiser_attack_strip8_512": (0.52, 0.46),
    "verdant_bruiser_idle_strip8_512": (0.50, 0.44),
    "verdant_caster_attack_strip8_512": (0.45, 0.60),
    "verdant_caster_idle_strip8_512": (0.48, 0.58),
    "verdant_swarm_attack_strip8_512": (0.58, 0.50),
    "verdant_swarm_idle_strip8_512": (0.62, 0.50),
    "crystal_lich_attack_strip8_512": (0.47, 0.45),
    "crystal_lich_idle_strip8_512": (0.45, 0.43),
    "forge_colossus_attack_strip8_512": (0.42, 0.55),
    "forge_colossus_idle_strip8_512": (0.43, 0.52),
    "lumen_angel_attack_strip8_512": (0.45, 0.55),
    "lumen_angel_idle_strip8_512": (0.47, 0.55),
    "spirit_matron_attack_strip8_512": (0.50, 0.55),
    "spirit_matron_idle_strip8_512": (0.50, 0.55),
    "thorn_regent_attack_strip8_512": (0.47, 0.55),
    "thorn_regent_idle_strip8_512": (0.47, 0.55),
    "void_reaper_attack_strip8_512": (0.52, 0.58),
    "void_reaper_idle_strip8_512": (0.52, 0.55),
}


WORK = 128              # the working frame size: every frame is measured at this resolution, the points scaled back


# PER-FRAME OVERRIDES, reviewed on the sheet: a point authored by hand (the whelp's lunge, the reference creature: its
# torso behind the head is too thin for the coil, so the on-body rule pulled it onto the head), or "interp" where the
# strip's own attack effect dragged the tracker (the point is interpolated between the frames round it).
OVERRIDES = {
    # the reference whelp: on the torso behind the head in every pose, the identical round-head frames 0 / 7 (and the
    # crouch 1 / 2) on the same spot (a point on frame 0 sat on its right eye: round 4), low on the torso so the halo
    # clears the back of the head and keeps away from the next whelp's face behind it
    "umbral_swarm_attack_strip8_512": {0: (0.596, 0.767), 1: (0.566, 0.737), 2: (0.566, 0.737), 3: (0.405, 0.67),
                                       4: (0.365, 0.66), 5: (0.445, 0.67), 6: (0.48, 0.70), 7: (0.596, 0.767)},
    # the Choir wisp: on the skirt below both hands (the tracker followed the hand into the coil's hollow: round 4)
    # (round 5: off the hem, whose ring lay on a fallen wisp's face when the next host stood over the corpse; onto the
    # trailing robe behind the hand, scored for the fewest hand pixels in the coil)
    "choir_swarm_idle_strip8_512": {k: (0.57, 0.68) for k in range(8)},
    "choir_swarm_attack_strip8_512": {k: (0.57, 0.68) for k in range(8)},
    # the Choir armoured at rest: the lower shield face (its dark gauntlet sat in the hollow on the pale armour)
    "choir_armoured_idle_strip8_512": {k: (0.36, 0.74) for k in range(8)},
    "choir_bruiser_attack_strip8_512": {5: "interp", 6: "interp"},
    "lumen_angel_attack_strip8_512": {5: "interp", 6: "interp"},
    "spirit_matron_attack_strip8_512": {5: "hold", 6: "hold"},
    # the Cinder armoured shifts and shrinks through its attack and the tracker slid onto its glowing core: each frame
    # authored on the belly just below the core (measured per frame), so the core never shows through the hollow
    "cinder_armoured_attack_strip8_512": {0: (0.48, 0.76), 1: (0.47, 0.77), 2: (0.44, 0.78), 3: (0.44, 0.79),
                                          4: (0.43, 0.77), 5: (0.46, 0.75), 6: (0.54, 0.76), 7: (0.50, 0.75)},
    # the umbral caster stands still while its scythe sweeps: one spot on the robe below the chest, idle and attack
    # alike (the tracker followed the sweep 0.07 down the chest and back on every bite)
    "umbral_caster_idle_strip8_512": {k: (0.495, 0.60) for k in range(8)},
    "umbral_caster_attack_strip8_512": {k: (0.495, 0.60) for k in range(8)},
    # the verdant armoured's swing: its shield's patch is hidden under the baked slash on frame 3
    "verdant_armoured_attack_strip8_512": {3: "interp"},
    # the marrow armoured's crouch: its back plates' patch is lost on frames 3 and 6
    "marrow_armoured_attack_strip8_512": {3: "interp", 6: "interp"},
}

# ONE SPOT THAT FOLLOWS ITS OWN PATCH: (the point on frame 0, the patch round it (a box in the frame) that carries it).
# Every frame's point is frame 0's moved by where that patch went (sum of squared differences, alpha weighted), so the
# coil stays on the same spot of the body through the loop and across its wrap (the frame-to-frame tracker slid off it:
# round 4, 0.18 of the frame on the wrap). A SHIELD COUNTS AS THE BODY: on a shield-bearer it is what faces the hunter.
PATCHES = {
    # the forge colossus: its right chest plate, off the molten core and the glowing crack junction (the tracker sat on
    # the core, then slid 0.18 of the frame across the chest while the chest itself moves under 0.04; a hollow over the
    # glow read as an eye)
    "forge_colossus_idle_strip8_512": ((0.62, 0.555), (0.46, 0.36, 0.72, 0.62)),
    "forge_colossus_attack_strip8_512": ((0.62, 0.555), (0.46, 0.36, 0.72, 0.62)),
    # the verdant armoured: the shield's wooden face below and left of its boss (the tracker went shield, belly by the
    # fist, shield again inside one bite); the shield itself swings up to a quarter of the frame
    "verdant_armoured_attack_strip8_512": ((0.39, 0.62), (0.30, 0.36, 0.62, 0.70)),
    "verdant_armoured_idle_strip8_512": ((0.40, 0.62), (0.30, 0.36, 0.62, 0.70)),
    # the cinder wasp: the upper abdomen behind the glowing thorax orb (a fixed x let the lunge put the orb in the hollow)
    "cinder_swarm_attack_strip8_512": ((0.64, 0.63), (0.52, 0.50, 0.80, 0.76)),
    "cinder_swarm_idle_strip8_512": ((0.60, 0.62), (0.48, 0.50, 0.76, 0.76)),
    # the marrow armoured: its plated back side (the belly skull's face sat in the hollow in every frame)
    "marrow_armoured_idle_strip8_512": ((0.60, 0.66), (0.48, 0.52, 0.74, 0.80)),
    "marrow_armoured_attack_strip8_512": ((0.60, 0.66), (0.48, 0.52, 0.74, 0.80)),
}


def follow_patch(fr, box):
    """Where frame 0's patch `box` (fractions) went in every frame: (dx, dy) in WORK px, alpha-weighted RGBA SSD."""
    x0, y0, x1, y1 = (int(round(v * WORK)) for v in box)
    tpl = fr[0][y0:y1, x0:x1].astype(np.float32)
    w = tpl[..., 3:4] / 255.0
    ph, pw = tpl.shape[:2]
    out = []
    for f in fr:
        f = f.astype(np.float32)
        best = None
        for dy in range(-int(0.25 * WORK), int(0.25 * WORK) + 1):
            ya = y0 + dy
            if ya < 0 or ya + ph > WORK:
                continue
            for dx in range(-int(0.35 * WORK), int(0.35 * WORK) + 1):
                xa = x0 + dx
                if xa < 0 or xa + pw > WORK:
                    continue
                e = float((((f[ya:ya + ph, xa:xa + pw] - tpl) ** 2) * w).mean())
                if best is None or e < best[0]:
                    best = (e, dx, dy)
        out.append(best[1:])
    return out


def frames(path):
    """The strip's frames at the working size (a box filter), and the frame's true size."""
    img = Image.open(path).convert("RGBA")
    h = img.height
    out = []
    for k in range(img.width // h):
        f = img.crop((k * h, 0, (k + 1) * h, h)).resize((WORK, WORK), Image.BOX)
        out.append(np.asarray(f).astype(np.float32))
    return out, h


def opaque(f):
    return f[..., 3] > 100


def luma(f):
    return f[..., 0] * 0.299 + f[..., 1] * 0.587 + f[..., 2] * 0.114


def visible_height(f):
    ys, _ = np.nonzero(opaque(f))
    return (ys.max() - ys.min() + 1) if len(ys) else f.shape[0]


def footprint(h, rx, ry):
    yy, xx = np.mgrid[-int(ry):int(ry) + 1, -int(rx):int(rx) + 1]
    return (xx / max(rx, 1)) ** 2 + (yy / max(ry, 1)) ** 2 <= 1.0


def coverage(mask, x, y, fp):
    fh, fw = fp.shape
    x0, y0 = int(round(x)) - fw // 2, int(round(y)) - fh // 2
    h, w = mask.shape
    if x0 < 0 or y0 < 0 or x0 + fw > w or y0 + fh > h:
        return 0.0
    return float(mask[y0:y0 + fh, x0:x0 + fw][fp].mean())


def propose(f, fp):
    """A first guess for frame 0 (reviewed on the sheet, then fixed in SEEDS): the best-covered, deepest-inside point of
    the torso band (40-80 % of the visible height from the top, 20-65 % of the width from the front)."""
    m = opaque(f)
    ys, xs = np.nonzero(m)
    top, bottom, left, right = ys.min(), ys.max(), xs.min(), xs.max()
    hgt, wid = bottom - top + 1, right - left + 1
    best, at = -1.0, (f.shape[1] / 2, f.shape[0] / 2)
    for y in range(int(top + 0.40 * hgt), int(top + 0.80 * hgt)):
        for x in range(int(left + 0.20 * wid), int(left + 0.65 * wid)):
            c = coverage(m, x, y, fp)
            if c > best + 1e-6:
                best, at = c, (x, y)
    return at


def track(prev, cur, x, y, patch, search):
    """The point in `cur` that best matches `prev`'s patch round (x, y): alpha and luma, sum of squared differences."""
    h, w = prev.shape[:2]
    p, sr = int(patch), int(search)
    x, y = int(round(x)), int(round(y))
    y0, y1, x0, x1 = max(0, y - p), min(h, y + p + 1), max(0, x - p), min(w, x + p + 1)
    ref_a = prev[y0:y1, x0:x1, 3] / 255.0
    ref_l = luma(prev[y0:y1, x0:x1]) / 255.0 * ref_a
    ph, pw = ref_a.shape
    cur_a = cur[..., 3] / 255.0
    cur_l = luma(cur) / 255.0 * cur_a
    best, at = 1e18, (x, y)
    for dy in range(-sr, sr + 1):
        yy0 = y0 + dy
        if yy0 < 0 or yy0 + ph > h:
            continue
        lo, hi = max(0, x0 - sr), min(w - pw, x0 + sr)
        if hi < lo:
            continue
        wa = np.lib.stride_tricks.sliding_window_view(cur_a[yy0:yy0 + ph, lo:hi + pw], (ph, pw))[0]
        wl = np.lib.stride_tricks.sliding_window_view(cur_l[yy0:yy0 + ph, lo:hi + pw], (ph, pw))[0]
        e = ((wa - ref_a) ** 2).sum(axis=(1, 2)) + ((wl - ref_l) ** 2).sum(axis=(1, 2))
        i = int(np.argmin(e))
        if e[i] < best:
            best, at = float(e[i]), (x + (lo + i - x0), y + dy)
    return at


def keep_on_body(f, x, y, fp, reach):
    """If the footprint at (x, y) is under ON_BODY on the creature, the nearest point within `reach` that is (deepest
    inside first); else the best there is."""
    m = opaque(f)
    if coverage(m, x, y, fp) >= ON_BODY:
        return x, y
    best, at = (-1.0, 0.0), (x, y)
    for dy in range(-int(reach), int(reach) + 1):
        for dx in range(-int(reach), int(reach) + 1):
            c = coverage(m, x + dx, y + dy, fp)
            key = (min(c, ON_BODY), -(dx * dx + dy * dy))
            if key > best:
                best, at = key, (x + dx, y + dy)
    return at


def strips():
    out = []
    for folder in ("Enemies", "Bosses"):
        for path in sorted(glob.glob(os.path.join(ANIM, folder, "*", "*_strip8_512.png"))):
            name = os.path.basename(path)[:-4]
            out.append((folder, name, path))
    # a death strip is seeded from its creature's idle point: the idle strips first
    return sorted(out, key=lambda t: "_death_" in t[1])


IDLE_P0 = {}             # the idle strips' frame-0 points of this run (WORK px), for the death strips' seeds


def head_boxes(name, fr):
    """The head box of every frame (WORK px): the reviewed table, else the top HEAD_TOP of the visible body."""
    if name in HEADS:
        return [tuple(v * WORK for v in b) for b in HEADS[name]]
    out = []
    for f in fr:
        ys, xs = np.nonzero(opaque(f))
        top, bottom, left, right = ys.min(), ys.max(), xs.min(), xs.max()
        out.append((left, top, right, top + HEAD_TOP * (bottom - top)))
    return out


def clear_of_head(x, y, hx, hy, head):
    """No texel of the halo-size footprint at (x, y) inside the head box."""
    if head is None:
        return True
    x0, y0, x1, y1 = head
    nx = min(max(x, x0), x1)
    ny = min(max(y, y0), y1)
    return ((nx - x) / hx) ** 2 + ((ny - y) / hy) ** 2 > 1.0


def clear_head(f, x, y, fp, hx, hy, head, reach):
    """Move the point off the head: the nearest spot whose halo clears the head box and whose coil is 80 % on the body."""
    if clear_of_head(x, y, hx, hy, head):
        return x, y
    m = opaque(f)
    best, at = None, (x, y)
    for dy in range(-int(reach), int(reach) + 1):
        for dx in range(-int(reach), int(reach) + 1):
            if not clear_of_head(x + dx, y + dy, hx, hy, head) or coverage(m, x + dx, y + dy, fp) < 0.8:
                continue
            d = dx * dx + dy * dy
            if best is None or d < best:
                best, at = d, (x + dx, y + dy)
    return at


FULL_FLOOR = 0.84        # the least share on the body at FULL resolution (the test asserts 0.80: a margin)


def full_coverage(op, k, s, x, y, rx, ry):
    """The body-point test's own measure (brand_mark_test.cs): full resolution, alpha > 100, every 2nd px of the coil's
    ellipse round (x, y) on frame k (px of the frame)."""
    xs = np.arange(int(x - rx), int(x + rx) + 1, 2)
    ys = np.arange(int(y - ry), int(y + ry) + 1, 2)
    gx, gy = np.meshgrid(xs, ys)
    inside = ((gx - x) / rx) ** 2 + ((gy - y) / ry) ** 2 <= 1
    ok = inside & (gx >= 0) & (gy >= 0) & (gx < s) & (gy < s)
    on = np.zeros(inside.shape, bool)
    on[ok] = op[gy[ok], k * s + gx[ok]]
    return on.sum() / max(1, inside.sum())


def points_for(name, path):
    """The strip's points at the WORKING size (the caller scales them to the frame), its footprint radii and coverage."""
    fr, size = frames(path)
    vis = visible_height(fr[0])
    rx, ry = SIZE_SHARE * vis * COIL_W / 2, SIZE_SHARE * vis * COIL_H / 2
    fp = footprint(WORK, rx, ry)
    if "_death_" in name:
        # A DEATH CLIP: the mark only comes apart on its first frames (the loosen, ~220 ms) and the strand leaves at once,
        # so ONE point on frame 0's torso (the lower torso band, the halo off the head), held: the falling host's pin
        # (the idle's point sat level with the eyes and the strand left at eye height into the next face: round 5)
        heads = head_boxes(name, fr)
        hx, hy = HALO_W * vis / 2, HALO_H * vis / 2
        # seeded from the creature's IDLE point (a death clip starts from the standing pose on the same canvas), else
        # the authored seed (the whelp: its death pose raises the head where the idle's torso was)
        idle = name.replace("_death", "_idle")
        if name in SEEDS:
            x, y = SEEDS[name][0] * WORK, SEEDS[name][1] * WORK
        elif idle in IDLE_P0:
            x, y = IDLE_P0[idle]
        else:
            x, y = propose(fr[0], fp)
        if name not in SEEDS:                        # an authored death seed is kept as reviewed
            x, y = keep_on_body(fr[0], x, y, fp, 0.10 * WORK)
        x, y = clear_head(fr[0], x, y, fp, hx, hy, heads[0] if heads else None, 0.18 * WORK)
        pts = [(x, y)] * len(fr)
        heads = [heads[0]] * len(fr) if heads else heads
        cov = [coverage(opaque(fr[k]), x, y, fp) for k in range(len(fr))]
        return fr, WORK, (rx, ry), pts, cov, heads, (hx, hy)
    if name in SEEDS:
        x, y = SEEDS[name][0] * WORK, SEEDS[name][1] * WORK
    else:
        x, y = propose(fr[0], fp)
    pts = [keep_on_body(fr[0], x, y, fp, 0.10 * WORK)]
    for k in range(1, len(fr)):
        tx, ty = track(fr[k - 1], fr[k], pts[-1][0], pts[-1][1], 0.16 * WORK, 0.16 * WORK)
        pts.append(keep_on_body(fr[k], tx, ty, fp, 0.10 * WORK))
    if name in PATCHES:
        (px, py), box = PATCHES[name]
        pts = [(px * WORK + dx, py * WORK + dy) for dx, dy in follow_patch(fr, box)]
    heads = head_boxes(name, fr)
    hx, hy = HALO_W * vis / 2, HALO_H * vis / 2
    over = OVERRIDES.get(name, {})
    for k in range(len(fr)):
        v = over.get(k)
        if isinstance(v, tuple):
            pts[k] = (v[0] * WORK, v[1] * WORK)
        elif v is None and name not in PATCHES:
            pts[k] = clear_head(fr[k], pts[k][0], pts[k][1], fp, hx, hy, heads[k] if heads else None, 0.18 * WORK)
    for k in range(len(fr)):
        v = over.get(k)
        if v == "hold":
            pts[k] = pts[k - 1]
        elif v == "interp":
            lo = max(j for j in range(k) if over.get(j) != "interp")
            hi = min(j for j in range(k + 1, len(fr)) if over.get(j) != "interp")
            t = (k - lo) / (hi - lo)
            pts[k] = (pts[lo][0] + (pts[hi][0] - pts[lo][0]) * t, pts[lo][1] + (pts[hi][1] - pts[lo][1]) * t)
    # THE FLOOR AT FULL RESOLUTION: the working size reads a ragged cloak or tail a few per cent fuller than it is. A strip
    # with a frame below the floor moves AS A WHOLE by the smallest shift that lifts every frame clearly above it and
    # keeps every halo off the head (a nudge per frame bobbed the coil up and down the body on every bite), stepping the
    # margin down to the test's own floor plus 0.02 before a frame alone moves to the nearest such spot
    full = np.array(Image.open(path).convert("RGBA"))
    s = full.shape[0]
    op = full[..., 3] > 100
    rows = np.nonzero(op[:, :s].any(1))[0]
    frx = SIZE_SHARE * (rows.max() - rows.min() + 1) / 2
    fry = frx * 0.8

    def fine(k, x, y, floor):
        return (full_coverage(op, k, s, x, y, frx, fry) >= floor
                and clear_of_head(x * WORK / s, y * WORK / s, hx, hy, heads[k] if heads else None))

    at = [(pts[k][0] * s / WORK, pts[k][1] * s / WORK) for k in range(len(fr))]
    floors = (FULL_FLOOR + 0.02, FULL_FLOOR, FULL_FLOOR - 0.02)
    for floor in floors if not all(fine(k, x, y, FULL_FLOOR) for k, (x, y) in enumerate(at)) else ():
        best = None
        for dy in range(-24, 25, 2):
            for dx in range(-24, 25, 2):
                if best is not None and dx * dx + dy * dy >= best[0]:
                    continue
                if all(fine(k, x + dx, y + dy, floor) for k, (x, y) in enumerate(at)):
                    best = (dx * dx + dy * dy, dx, dy)
        if best is not None:
            pts = [((x + best[1]) * WORK / s, (y + best[2]) * WORK / s) for x, y in at]
            break
    for k in range(len(fr)):
        x, y = pts[k][0] * s / WORK, pts[k][1] * s / WORK
        if full_coverage(op, k, s, x, y, frx, fry) >= floors[-1]:
            continue
        best = None
        for dy in range(-24, 25, 2):
            for dx in range(-24, 25, 2):
                if best is not None and dx * dx + dy * dy >= best[0]:
                    continue
                if (full_coverage(op, k, s, x + dx, y + dy, frx, fry) >= FULL_FLOOR + 0.02
                        and clear_of_head((x + dx) * WORK / s, (y + dy) * WORK / s, hx, hy, heads[k] if heads else None)):
                    best = (dx * dx + dy * dy, x + dx, y + dy)
        if best is not None:
            pts[k] = (best[1] * WORK / s, best[2] * WORK / s)
    # IDENTICAL FRAMES CARRY IDENTICAL POINTS (the whelp's round-head frames 0 and 7 are one picture: two points slid the
    # coil across a still body once a second)
    seen = {}
    for k, f in enumerate(fr):
        key = f.tobytes()
        if key in seen:
            pts[k] = pts[seen[key]]
            if heads:
                heads[k] = heads[seen[key]]
        else:
            seen[key] = k
    cov = [coverage(opaque(fr[k]), pts[k][0], pts[k][1], fp) for k in range(len(fr))]
    if "_idle_" in name:
        IDLE_P0[name] = pts[0]
    return fr, WORK, (rx, ry), pts, cov, heads, (hx, hy)


def sheet(rows, path):
    """Each strip as a row of its frames, the point and the coil's footprint drawn; a family's idle and attack together."""
    th = 150
    img = Image.new("RGB", (8 * (th + 4) + 260, len(rows) * (th + 6) + 6), (40, 38, 36))
    d = ImageDraw.Draw(img)
    for r, (name, fr, size, (rx, ry), pts, cov, heads, (hx, hy)) in enumerate(rows):
        y0 = 6 + r * (th + 6)
        d.text((4, y0 + 4), name.replace("_strip8_512", ""), fill=(235, 200, 120))
        d.text((4, y0 + 20), f"min on body {min(cov) * 100:.0f} %", fill=(200, 180, 150))
        for k, f in enumerate(fr):
            bg = np.zeros(f.shape[:2] + (3,), np.float32) + np.array([110, 104, 96], np.float32)
            a = f[..., 3:4] / 255.0
            comp = (f[..., :3] * a + bg * (1 - a)).astype(np.uint8)
            t = Image.fromarray(comp).resize((th, th), Image.LANCZOS)
            td = ImageDraw.Draw(t)
            s = th / size
            x, y = pts[k][0] * s, pts[k][1] * s
            td.ellipse([x - hx * s, y - hy * s, x + hx * s, y + hy * s], outline=(120, 90, 190), width=1)
            td.ellipse([x - rx * s, y - ry * s, x + rx * s, y + ry * s], outline=(170, 120, 255), width=2)
            if heads:
                hb = heads[k]
                td.rectangle([hb[0] * s, hb[1] * s, hb[2] * s, hb[3] * s], outline=(0, 230, 230))
            td.line([x - 4, y, x + 4, y], fill=(255, 230, 120))
            td.line([x, y - 4, x, y + 4], fill=(255, 230, 120))
            if cov[k] < ON_BODY:
                td.rectangle([0, 0, th - 1, th - 1], outline=(255, 60, 60), width=2)
            img.paste(t, (260 + k * (th + 4), y0))
    img.save(path)
    print(os.path.basename(path), img.size)


def main():
    review = sys.argv[1] if len(sys.argv) > 1 else None
    groups = {}
    for folder, name, path in strips():
        fr, size, radii, pts, cov, heads, halo = points_for(name, path)
        key = name.split("_")[0] if folder == "Enemies" else "bosses"
        groups.setdefault(key, []).append((name, fr, size, radii, pts, cov, heads, halo))
        if not review:
            data = {"points": [[round(float(x) / size, 4), round(float(y) / size, 4)] for x, y in pts],
                    "head": [[round(float(v) / size, 4) for v in b] for b in heads],
                    "halo": [HALO_W, HALO_H],
                    "source": "mark_points.py: " + ("one spot following its patch %.2f,%.2f" % PATCHES[name][0]
                                                     if name in PATCHES else
                                                     ("seed %.2f,%.2f" % SEEDS[name] if name in SEEDS else "proposed")
                                                     + ", tracked, kept on the body")
                              + (", frames overridden by hand" if name in OVERRIDES else "")}
            with open(path[:-4] + ".mark.json", "w", encoding="utf-8") as f:
                json.dump(data, f, indent=1)
    if review:
        os.makedirs(review, exist_ok=True)
        for key, rows in groups.items():
            sheet(rows, os.path.join(review, f"mark_points_{key}.png"))


if __name__ == "__main__":
    main()
