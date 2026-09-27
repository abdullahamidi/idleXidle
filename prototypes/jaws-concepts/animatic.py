#!/usr/bin/env python3
"""animatic.py -- JAWS CONCEPT STUDY (2026-09-25): three rough world-presentation concepts drawn over REAL fight plates.

    python prototypes/jaws-concepts/animatic.py <plate prefix> <out dir> <concept A|B|C> <trigger> [<trigger> ...]
        <trigger> = <contact ms>:<anchor x>,<anchor y>      (the bite's ms and the attacker's front-lower point)

THROWAWAY. This is a concept animatic, not production: nothing here is read by the game. The plates are the seeded
fight filmed with the reaction layer OFF (film_plates.sh, beside this file), so the reflected number and flash are on
the bite's own frame and nothing of JAWS is drawn. This script draws each concept over those frames with rough
monochrome shapes and one Shadow emissive colour, at the size the game shows them, and writes a copy of the plate's
trace whose sound list carries the TEMP cue (the dry-steel sfx_seeker_jaws_snap) timed so its clack lands on the
concept's SLAM frame (film_audio.py renders it). The legacy JAWS cues (the cast breath, the pitched reaction thud, the
answer's generic thud) are removed; the enemy's bite thud and the critical cue stay.

TIME. t = 0 is the frame that first shows the bite (the one the fight's number and flash land on). The replay knows the
bite ~900 ms early (the creatures' wind-up), so a concept may begin BEFORE t = 0: this is presentation choreography on
known replay truth; the fight is untouched.

  A  SHADOW JAWS    two dark serrated crescents FORM round the attacker's front (-40 .. -17 ms), SNAP shut on the bite
                    (0), rebound once (+17), hold, and BREAK into shadow (+100 .. +180); one brief Shadow thread from
                    the Seeker's belt (-20 .. +60) says whose they are.
  B  SHADOW TETHER  the stored tether stirs at the belt (-80), a Shadow line LASHES out (-50 .. -17) with its catch open,
                    CATCHES and snaps taut on the bite (0), recoils (+17 .. +67) and reels back and fades (+70 .. +160).
  C  TRAP SIGIL     a thin dashed ring TELEGRAPHS round the attacker (-80), two flat serrated arcs appear outside it
                    (-40), CONVERGE on the bite (0) with a contact flash, hold, and FRACTURE into shards (+60 .. +150).
"""
from __future__ import annotations

import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

DARK = (20, 16, 32)               # the manifestation's body: shadow-iron, near black with a violet cast
EDGE = (70, 60, 104)              # its lit edge (still dark)
GLOW = (155, 123, 255)            # the Shadow source's world glow (HuntScreen.SourceGlow)
CORE = (225, 212, 255)            # the hottest core of a flash
BELT = (647.0, 700.0)             # the Seeker's belt at rest (the production reaction's anchor); + the trace's root motion
FRAME = 1000.0 / 60.0


def ease_out(v):
    v = min(1.0, max(0.0, v))
    return 1 - (1 - v) * (1 - v)


def smooth(v):
    v = min(1.0, max(0.0, v))
    return v * v * (3 - 2 * v)


def lerp(a, b, v):
    return a + (b - a) * v


def key(t, keys):
    """Piecewise-linear value over (ms, value) keys, held at both ends."""
    if t <= keys[0][0]:
        return keys[0][1]
    for (t0, v0), (t1, v1) in zip(keys, keys[1:]):
        if t <= t1:
            return lerp(v0, v1, (t - t0) / max(1e-6, t1 - t0))
    return keys[-1][1]


def rot(p, c, deg):
    a = math.radians(deg)
    x, y = p[0] - c[0], p[1] - c[1]
    return (c[0] + x * math.cos(a) - y * math.sin(a), c[1] + x * math.sin(a) + y * math.cos(a))


# ── the trace: shots, root motion, the frame each bite first shows on ───────────────────────────
def read_log(path):
    shots, roots, lines, bite_frame = {}, {}, [], {}
    for raw in open(path, encoding="utf-8", errors="replace"):
        lines.append(raw)
        p = raw.rstrip("\n").split("\t")
        if len(p) < 5 or p[0] != "present":
            continue
        ph = float(p[2])
        if p[3] == "shot":
            shots[int(p[4])] = (float(p[1]), ph)
        elif p[3] == "root":
            kv = dict(x.split("=", 1) for x in p[4:] if "=" in x)
            roots[round(ph)] = (float(kv.get("x", 0)), float(kv.get("y", 0)))
        elif p[3] == "event" and p[4] == "EnemyStrike":
            kv = dict(x.split("=", 1) for x in p[5:] if "=" in x)
            bite_frame.setdefault(int(kv["at"]), []).append((float(p[1]), ph))
    # a bite's millisecond repeats wave to wave: keep the occurrence inside this film (by the wall clock)
    lo = min(w for w, _ in shots.values()) - 200
    hi = max(w for w, _ in shots.values()) + 200
    bite_frame = {ms: next((o for o in occ if lo <= o[0] <= hi), None) for ms, occ in bite_frame.items()}
    return shots, roots, lines, {ms: o for ms, o in bite_frame.items() if o is not None}


def belt_at(roots, ph):
    """The Seeker's belt this frame: rest + the last root motion traced at or before the frame (a leap carries it)."""
    best = None
    for k in roots:
        if k <= ph + 0.5 and (best is None or k > best):
            best = k
    if best is None or ph - best > 40:      # the root is traced every frame while it moves; stale means at rest
        return BELT
    rx, ry = roots[best]
    return (BELT[0] + rx, BELT[1] + ry)


# ── drawing helpers: a dark layer (alpha) and a light layer (additive) per frame ───────────────
class Layers:
    def __init__(self, size):
        self.dark = Image.new("RGBA", size, (0, 0, 0, 0))
        self.light = Image.new("RGB", size, (0, 0, 0))
        self.dd = ImageDraw.Draw(self.dark)
        self.dl = ImageDraw.Draw(self.light)

    def poly_dark(self, pts, alpha, edge=True):
        a = int(255 * max(0.0, min(1.0, alpha)))
        if a <= 0:
            return
        self.dd.polygon(pts, fill=DARK + (a,), outline=(EDGE + (a,)) if edge else None)

    def poly_light(self, pts, intensity, colour=GLOW, width=0):
        c = tuple(int(v * max(0.0, min(1.5, intensity))) for v in colour)
        if width:
            self.dl.line(pts + [pts[0]], fill=c, width=width)
        else:
            self.dl.polygon(pts, fill=c)

    def line_light(self, pts, intensity, width, colour=GLOW):
        c = tuple(int(v * max(0.0, min(1.5, intensity))) for v in colour)
        self.dl.line(pts, fill=c, width=width, joint="curve")

    def line_dark(self, pts, alpha, width):
        a = int(255 * max(0.0, min(1.0, alpha)))
        if a > 0:
            self.dd.line(pts, fill=DARK + (a,), width=width, joint="curve")


def composite(frame: Image.Image, layers: Layers, bloom=4.0) -> Image.Image:
    """The dark layer over the frame, then the light layer added with a soft bloom -- only inside what was drawn."""
    boxes = [b for b in (layers.dark.getbbox(), layers.light.getbbox()) if b]
    if not boxes:
        return frame
    pad = int(bloom * 4)
    x0 = max(0, min(b[0] for b in boxes) - pad)
    y0 = max(0, min(b[1] for b in boxes) - pad)
    x1 = min(frame.width, max(b[2] for b in boxes) + pad)
    y1 = min(frame.height, max(b[3] for b in boxes) + pad)
    box = (x0, y0, x1, y1)
    base = np.asarray(frame.crop(box).convert("RGB")).astype(np.float32)
    d = np.asarray(layers.dark.crop(box)).astype(np.float32)
    a = d[:, :, 3:4] / 255.0
    out = base * (1 - a) + d[:, :, :3] * a
    light = layers.light.crop(box)
    if light.getbbox():
        sharp = np.asarray(light).astype(np.float32)
        soft = np.asarray(light.filter(ImageFilter.GaussianBlur(bloom))).astype(np.float32)
        out = out + sharp * 0.85 + soft * 1.3
    frame = frame.copy()
    frame.paste(Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)), (x0, y0))
    return frame


# ── the crescent jaw: thick in the middle, pointed at the ends, teeth on the inner (biting) edge ──
def crescent(w, h_out, h_in, teeth, depth, offset=0.0, n=28):
    """An UPPER jaw in local coords (x across, y up is negative): its ends at (+-w/2, 0), the outer arch h_out high,
    the inner arch h_in high, and `teeth` triangles hanging below the inner arch (pointing +y, at the bite)."""
    outer = [(-w / 2 + w * i / n, -h_out * (1 - (2 * i / n - 1) ** 2)) for i in range(n + 1)]
    inner = [(w / 2 - w * i / n, -h_in * (1 - (1 - 2 * i / n) ** 2)) for i in range(n + 1)]
    body = outer + inner
    tri = []
    span = 0.78 * w
    step = span / teeth
    for k in range(teeth):
        cx = -span / 2 + step * (k + 0.5) + offset * step
        if abs(cx) > 0.42 * w:
            continue
        base_y = -h_in * (1 - (2 * cx / w) ** 2)
        half = step * 0.42
        tri.append([(cx - half, base_y), (cx, base_y + depth), (cx + half, base_y)])
    return body, tri


def place(pts, centre, scale=1.0, deg=0.0, flip=False, pivot=(0.0, 0.0), shift=(0.0, 0.0)):
    out = []
    for x, y in pts:
        if flip:
            y = -y
        x, y = rot((x, y), pivot, deg)
        out.append((centre[0] + (x + shift[0]) * scale, centre[1] + (y + shift[1]) * scale))
    return out


# ── A: SHADOW JAWS ───────────────────────────────────────────────────────────────────────────
def draw_a(L: Layers, t, anchor, size, belt, seed):
    if t < -45 or t > 200:
        return
    c = (anchor[0] + 0.10 * size, anchor[1] - 0.36 * size)          # the attacker's front: the part that bit
    w, h_out, h_in, depth = 1.35 * size, 0.62 * size, 0.30 * size, 0.24 * size
    up_body, up_teeth = crescent(w, h_out, h_in, 5, depth)
    lo_body, lo_teeth = crescent(w, h_out, h_in, 5, depth, offset=0.5)
    gap = key(t, [(-60, 0.42), (-17, 0.36), (0, -0.07), (17, 0.07), (33, 0.0)]) * size     # half-gap: open, SLAM, rebound
    tilt = key(t, [(-60, 22), (-17, 20), (0, 0), (17, 5), (33, 1), (60, 0)])                  # hinged at the back
    alpha = key(t, [(-45, 0.0), (-33, 0.7), (-17, 1.0), (100, 1.0), (180, 0.0)])            # forms from -40
    glow = key(t, [(-45, 0.0), (-33, 0.35), (-17, 0.55), (0, 1.25), (17, 0.8), (60, 0.35), (100, 0.2), (180, 0.0)])
    grow = key(t, [(-45, 1.12), (-17, 1.0)])
    hinge = (-w / 2, 0.0)
    brk = smooth((t - 100) / 80) if t > 100 else 0.0                  # the break: the jaws split and drift apart
    for body, teeth, flip, sign in ((up_body, up_teeth, False, -1), (lo_body, lo_teeth, True, 1)):
        for part, (lo, hi) in enumerate(((0.0, 0.34), (0.34, 0.67), (0.67, 1.0))):
            # the crescent is drawn whole until it breaks; then in three fragments that drift and fade
            if brk <= 0 and part > 0:
                continue
            seg = body if brk <= 0 else body_segment(body, lo, hi)
            drift = (brk * (part - 1) * 0.18 * size, sign * brk * 0.22 * size)
            # opening, the front of each jaw swings away from the bite about the hinge at its back (the Seeker's side)
            pts = place(seg, (c[0] + drift[0], c[1] + sign * gap + drift[1]), grow, tilt if flip else -tilt,
                        flip=flip, pivot=hinge)
            L.poly_dark(pts, alpha * (1 - brk))
            L.poly_light(pts, glow * 0.55 * (1 - brk), width=2)
        if brk < 0.5:
            for tri in teeth:
                pts = place(tri, (c[0], c[1] + sign * gap), grow, tilt if flip else -tilt, flip=flip, pivot=hinge)
                L.poly_dark(pts, alpha * (1 - 2 * brk), edge=False)
                L.poly_light(pts, glow * 0.45 * (1 - 2 * brk), width=1)
    if -1 <= t <= 40:                                                  # the SNAP: a hot seam along the bite
        s = key(t, [(-1, 0.0), (0, 1.3), (17, 0.7), (40, 0.0)])
        L.line_light([(c[0] - 0.62 * w, c[1]), (c[0] + 0.62 * w, c[1])], s, max(2, int(0.06 * size)), CORE)
    if -20 <= t <= 60:                                                 # ownership: one brief Shadow thread from the belt
        s = key(t, [(-20, 0.0), (0, 0.7), (60, 0.0)])
        L.line_light(thread(belt, (c[0] - 0.55 * w, c[1]), seed, 0.04), s, 2)


def body_segment(body, lo, hi):
    """The part of a crescent between two shares of its width (for the break)."""
    xs = [p[0] for p in body]
    x0, x1 = min(xs), max(xs)
    a, b = x0 + (x1 - x0) * lo, x0 + (x1 - x0) * hi
    return [(min(max(x, a), b), y) for x, y in body]


def thread(a, b, seed, sag, n=18, wobble=0.0, phase=0.0):
    pts = []
    for i in range(n + 1):
        u = i / n
        x = a[0] + (b[0] - a[0]) * u
        y = a[1] + (b[1] - a[1]) * u - sag * math.hypot(b[0] - a[0], b[1] - a[1]) * math.sin(math.pi * u)
        if wobble:
            y += wobble * math.sin(math.pi * u) * math.sin(6 * math.pi * u + phase)
        pts.append((x, y))
    return pts


# ── B: SHADOW TETHER COUNTER ─────────────────────────────────────────────────────────────────
def draw_b(L: Layers, t, anchor, size, belt, seed):
    if t < -80 or t > 170:
        return
    target = (anchor[0] + 0.05 * size, anchor[1] - 0.30 * size)      # the limb it catches
    if t < -50:                                                       # the stored tether stirs at the belt
        s = key(t, [(-80, 0.0), (-60, 0.5)])
        L.poly_light(circle(belt, 0.12 * size), s * 0.6, width=2)
        return
    reach = key(t, [(-50, 0.30), (-33, 0.68), (-17, 0.94), (0, 1.0)])   # the lash: three frames of travel
    retract = smooth((t - 70) / 80) if t > 70 else 0.0
    head = (lerp(belt[0], target[0], reach * (1 - retract)), lerp(belt[1], target[1], reach * (1 - retract)))
    sag = key(t, [(-50, 0.20), (-33, 0.14), (-17, 0.07), (0, 0.0)])
    wob = key(t, [(0, 0.0), (8, 0.22), (40, 0.08), (70, 0.0)]) * size
    pts = thread(belt, head, seed, sag, wobble=wob, phase=t * 0.35)
    width = int(key(t, [(-50, 0.08), (0, 0.11), (70, 0.07), (150, 0.02)]) * size) + 1
    alpha = key(t, [(-50, 0.9), (100, 0.9), (160, 0.0)])
    L.line_dark(pts, alpha, width + 2)
    glow = key(t, [(-50, 0.5), (-1, 0.6), (0, 1.35), (17, 0.9), (60, 0.4), (150, 0.0)])
    L.line_light(pts, glow, max(1, width // 2))
    # the CATCH: open hook before the bite, a ring clamped round the limb on it, loosening as it lets go
    r = key(t, [(-50, 0.30), (-17, 0.30), (0, 0.22), (17, 0.19), (70, 0.21), (100, 0.30)]) * size
    opening = key(t, [(-50, 120), (-17, 90), (0, 0), (70, 0), (100, 140)])
    arc = arc_pts(head if retract > 0 else target if t >= 0 else head, r, 200 + opening / 2, 520 - opening / 2)
    if t < 100:
        L.line_dark(arc, alpha, max(3, int(0.09 * size)))
        L.line_light(arc, glow, max(1, int(0.04 * size)))
    if -1 <= t <= 33:                                                 # the snap: a flash at the catch
        s = key(t, [(-1, 0.0), (0, 1.4), (33, 0.0)])
        L.poly_light(circle(target, 0.10 * size), s, CORE)


def circle(c, r, n=24):
    return [(c[0] + r * math.cos(2 * math.pi * i / n), c[1] + r * math.sin(2 * math.pi * i / n)) for i in range(n)]


def arc_pts(c, r, a0, a1, n=24):
    return [(c[0] + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), c[1] + r * math.sin(math.radians(a0 + (a1 - a0) * i / n)))
            for i in range(n + 1)]


# ── C: TRAP SIGIL ────────────────────────────────────────────────────────────────────────────
def draw_c(L: Layers, t, anchor, size, belt, seed):
    if t < -80 or t > 170:
        return
    c = (anchor[0] + 0.10 * size, anchor[1] - 0.45 * size)
    ring_r = 0.72 * size * key(t, [(0, 1.0), (150, 1.25)])
    ring_a = key(t, [(-80, 0.0), (-67, 0.35), (-33, 0.6), (0, 1.3), (33, 0.5), (150, 0.0)])
    spin = t * 0.08
    for k in range(6):                                                # the telegraph: a thin dashed ring
        a0 = spin + k * 60
        L.line_light(arc_pts(c, ring_r, a0, a0 + 38, 8), ring_a * 0.7, 2)
    arc_alpha = key(t, [(-40, 0.0), (-33, 0.6), (-17, 1.0), (60, 1.0), (140, 0.0)])
    radius = key(t, [(-33, 1.05), (-17, 0.85), (0, 0.30), (17, 0.36), (33, 0.30)]) * size   # the arcs close in
    thick = 0.16 * size
    frac = smooth((t - 60) / 90) if t > 60 else 0.0
    light = key(t, [(-40, 0.0), (-33, 0.35), (-1, 0.6), (0, 1.2), (40, 0.6), (140, 0.0)])   # the arcs exist from -40
    for a_start in (120.0, -60.0):                                    # two flat serrated arcs, left "(" and right ")"
        for piece in range(3):                                        # drawn as three pieces: they fracture apart
            a0, a1 = a_start + piece * 40, a_start + piece * 40 + 40
            mid = math.radians((a0 + a1) / 2)
            fly = frac * 0.6 * size
            pc = (c[0] + math.cos(mid) * fly, c[1] + math.sin(mid) * fly)
            outer = arc_pts(pc, radius + thick, a0, a1, 6)
            inner = arc_pts(pc, radius, a1, a0, 6)
            # the serration: every other inner point pulled toward the centre (a zigzag of teeth: a graphic, no mechanism)
            zig = [(lerp(p[0], pc[0], 0.28 * size / max(1.0, radius)) if i % 2 else p[0],
                    lerp(p[1], pc[1], 0.28 * size / max(1.0, radius)) if i % 2 else p[1]) for i, p in enumerate(inner)]
            pts = outer + zig
            L.poly_dark(pts, arc_alpha * (1 - frac) * 0.9)
            L.poly_light(pts, light * (1 - frac), width=2)
    if -1 <= t <= 40:                                                 # the contact flash where they meet
        s = key(t, [(-1, 0.0), (0, 1.4), (40, 0.0)])
        L.line_light([(c[0], c[1] - 0.55 * size), (c[0], c[1] + 0.55 * size)], s, max(2, int(0.06 * size)), CORE)
        L.line_light([(c[0] - 0.25 * size, c[1]), (c[0] + 0.25 * size, c[1])], s * 0.8, max(2, int(0.04 * size)), CORE)


# ── A2: MIRRORED SHADOW SNAP (the reset, 2026-09-27) ────────────────────────────────────────────
# JAWS is a magical Reaction, not a machine: the enemy's bite closes its small hostile-red fangs on the Seeker (the
# production contact motif, already in the plate), and the Shadow ANSWERS with the same sentence mirrored on the
# creature that bit: a slightly larger Shadow-purple snap, rooted on the Seeker's side and hooked into the attacker.
# Nothing is drawn before the bite lands (t < 0). 0..16 the answer begins (the fangs appear, open); 16..33 they close
# and peak; 33..80 rebound and residue; gone by 160. ONE extremely short ownership trace, from the fangs' root back
# toward the Seeker, and only AFTER the snap has begun: the line never precedes the reaction.
def jaw_polys(at, s, gap, side, n=12):
    """One half of the bite motif's SHAPE FAMILY (HuntScreen.Jaw, the M4 hybrid selected 2026-09-27): a broad crescent
    jaw convex away from the bite line with open horns, and one strong fang point from its middle to the bite line.
    Returns (crescent polygon, fang polygon, tip point). `side` -1 the upper jaw, +1 the lower."""
    c = (at[0], at[1] + side * (gap + s * 0.10))
    a0, a1 = (222.0, 318.0) if side < 0 else (42.0, 138.0)
    r_out, r_in = s * 0.66, s * 0.40
    outer = [(c[0] + r_out * math.cos(math.radians(a0 + (a1 - a0) * i / n)), c[1] + r_out * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]
    inner = [(c[0] + r_in * math.cos(math.radians(a1 - (a1 - a0) * i / n)), c[1] + r_in * math.sin(math.radians(a1 - (a1 - a0) * i / n))) for i in range(n + 1)]
    by = c[1] - side * s * 0.40
    tip = (at[0], at[1] + side * gap)
    fang = [(at[0] - s * 0.16, by), (at[0] + s * 0.16, by), tip]
    return outer + inner, fang, tip


def draw_a2(L: Layers, t, anchor, size, belt, seed):
    """A2 MIRRORED SHADOW SNAP, second pass: the SAME shape family as the incoming motif (crescent jaws + fang points),
    Shadow-purple, ~1.25x the incoming footprint, on the creature that bit. t = 0 is the incoming motif's snap on
    the Seeker. 0..16 the answer begins (jaws appear open); 16..33 they SNAP; a short rebound; gone by ~120.
    NO ownership trace by default: the visual rhyme (red on him -> purple on the biter) is the test."""
    if t < 0 or t > 130:
        return
    c = (anchor[0], anchor[1])                                        # the biter's head: the part that bit
    s = 50.0                                                          # ~1.25x the incoming motif's ~40 px span
    alpha = key(t, [(0, 0.0), (8, 0.7), (16, 0.95), (60, 0.95), (90, 0.5), (130, 0.0)])
    gap = key(t, [(0, 0.34), (16, 0.30), (24, -0.05), (33, -0.05), (48, 0.03), (80, 0.0)]) * s      # open, SNAP, rebound
    glow = key(t, [(0, 0.0), (16, 0.45), (24, 1.3), (33, 1.0), (80, 0.4), (130, 0.0)])
    for side in (-1, 1):
        cres, fang, tip = jaw_polys(c, s, gap, side)
        L.poly_dark(cres, alpha)
        L.poly_light(cres, glow * 0.55, width=2)
        L.poly_dark(fang, alpha)
        L.poly_light(fang, glow * 0.55, width=1)
        L.poly_light(circle(tip, max(1.5, 0.05 * size), 8), glow * 0.9, CORE)
    if 23 <= t <= 50:                                                 # the snap: a short hot seam along the bite line
        k = key(t, [(23, 0.0), (24, 1.2), (36, 0.5), (50, 0.0)])
        L.line_light([(c[0] - 0.36 * s, c[1]), (c[0] + 0.36 * s, c[1])], k, max(2, int(0.05 * size)), CORE)


DRAW = {"A": draw_a, "A2": draw_a2, "B": draw_b, "C": draw_c}
TEMP_CUE = "sfx_seeker_jaws_snap"
CLACK_MS = 51.0          # the temp cue's strongest transient, measured (design/audio/seeker-jaws-audio-brief.md)
LEGACY_DROP = {"sfx_cast"}


def main():
    prefix, out_dir, concept = sys.argv[1], sys.argv[2], sys.argv[3].upper()
    triggers = []
    for spec in sys.argv[4:]:
        ms, xy = spec.split(":")
        ax, ay = xy.split(",")
        triggers.append((int(ms), (float(ax), float(ay))))
    shots, roots, lines, bite = read_log(prefix + ".log")
    os.makedirs(out_dir, exist_ok=True)
    base = os.path.basename(prefix)
    size = 72.0                       # the manifestation's scale: ~0.38 of the imps' ~190 px height
    t0 = {ms: bite[ms] for ms, _ in triggers if ms in bite}
    for idx, (wall, ph) in sorted(shots.items()):
        frame = Image.open(f"{prefix}_{idx:02d}.png").convert("RGB")
        L = Layers(frame.size)
        belt = belt_at(roots, ph)
        for ms, anchor in triggers:
            if ms not in t0:
                continue
            t = ph - t0[ms][1]
            DRAW[concept](L, t, anchor, size, belt, ms)
        composite(frame, L).save(os.path.join(out_dir, f"{base}_{idx:02d}.png"), compress_level=1)
    # the sound: the temp cue so its clack lands on the SLAM frame; the legacy JAWS cues out
    out = []
    drop_walls = {round(w) for w, _ in t0.values()}
    for raw in lines:
        p = raw.rstrip("\n").split("\t")
        if len(p) > 5 and p[0] == "present" and p[3] == "sound" and round(float(p[1])) in drop_walls:
            kv = dict(x.split("=", 1) for x in p[5:] if "=" in x)
            if p[4] in LEGACY_DROP or (p[4] == "sfx_hit" and kv.get("pitch") == "0.25") or (p[4] == "sfx_hit" and kv.get("vol") == "0.22"):
                continue
        out.append(raw)
    # A2 is a magical reaction, not a machine: the dry-steel trap cue belongs to the rejected physical-trap fantasy and
    # is NOT added for it (the brief, 2026-09-27: temporary audio or a muted comparison; its sound is a later pass)
    if concept != "A2":
        for ms, (wall, ph) in t0.items():
            out.append(f"present\t{wall - CLACK_MS:.0f}\t{ph - CLACK_MS:.0f}\tsound\t{TEMP_CUE}\tvol=0.46\tpitch=0.00\tpan=0.09\n")
    open(os.path.join(out_dir, base + ".log"), "w", encoding="utf-8").writelines(out)
    print(f"{concept} {base}: {len(shots)} frames, triggers {sorted(t0)}")


if __name__ == "__main__":
    main()
