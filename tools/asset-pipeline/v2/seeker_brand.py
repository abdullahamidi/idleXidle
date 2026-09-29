#!/usr/bin/env python3
"""seeker_brand.py -- BRAND, the MARK / PERSISTENT TARGET-ATTACHED STATE reference (ADR-011, 2026-09-29): the Seeker's
ETCHED SHADOW BRAND, a lopsided coil cut into the marked enemy's body, made of living shadow smoke.

The one-sentence read: that enemy has been branded; the mark is living shadow etched into its body; it quietly persists
there; when it deepens it bites further inward; when its host falls it migrates cleanly to the next one.

THE SHAPE comes from PixelLab (a `create_image_pixen` silhouette, job 1bf52462, cached in
keypose_sources/seeker_brand_src/ because PixelLab's links expire): an uneven hand-burned coil of one and a half turns.
Nothing generated is used raw. Its PATH is extracted (the in-mask geodesic walk from the outer tip, averaged into a
centre line), turned so it enters from the lower left (at the source's own angle it read as the digit 6 / the letter
G), squashed lopsided, wobbled, and REDRAWN at the game's pixel material: a logical pixel of 1/3 of the runtime cell (~3 screen px on a whelp), value bands, a
nearest upscale, no soft edges, and a clean-up pass so no orphan or diagonal-only texel can read as a glyph.

THREE LAYERS per cell, tinted at runtime (two alpha maps):
  CUT   the groove: shadow smoke condensed into the cut. AT REST it is translucent MID with its LIGHT core lit only
        along a travelling third of the coil (the internal swirl, six phases); the whole coil is solid and lit only on
        a BEAT (the EDGE / CARVE / FORM cells, drawn hot). It is what reads on a near-black creature.
  HALO  the smoky roots and the smoke filling the coil: ONE map drawn twice, in near-black ink (it reads on a light
        creature) and in a lit lavender-grey smoke (it reads on a dark one), seeping 1-2 px out of the cut, broken by
        holes, drifting with the swirl.
THE DEPTH LADDER bites INWARD, never brighter or bigger: every stage is the whole coil (a coil cut only part of the way
read as the letter G), and every step CUTS more: deep1 a wider groove; deep2 the RING's wall cut in toward the coil's
hollow, with a ragged edge; deep3 and deep4 the thin inner hook cut further ALONG THE SPIRAL, inward (the path runs on
past the source's inner end at its own curvature, and each stage reveals more of it): the cut literally goes deeper into
the coil, and the outline never grows (a burn spreading out of the ring, and a hollow shrunk round the hook, read as
"just bigger", an O, an eye). The SPIRAL STAYS OPEN at every depth (the medial line between the outer tip and the turn
it runs under is never cut: fused, every deep stage was a closed ring round a pupil). The hook stays a thin line and is
never lit at rest: thickened into a club or lit as a knot, it read as an S / 5 / 9 / @ or a pupil. Chosen by a blind
read of four deep ladders (three readers, unanimous: "etched further inward at every step"). The DEEPEN beat is a CHISEL: the NEW cut only (this
stage less the one before), lit in three stretches travelling round the ring toward the hook, each keeping what the
steps before it cut; the old cut stays on screen until the last stretch. SPREAD (SPRAWL, half strength) is the base coil
thinned, with no lit core.

Every cell per stage (COLUMNS): GATHER x3 (smoke puffs drawn in along the coil's own path, condensing), IDLE x6 (the
travelling light), EDGE (the whole coil's lit line: the APPLY beat), CARVE x3 (only what this stage adds to the one
before, in three stretches travelling round the ring, each keeping the ones before: the DEEPEN beat), FORM x3 (the coil drawn in along its path from its outer tip: the arrival of a
migrating mark), LOOSEN x2 (the coil coming apart into smoke). Plus the THREAD puffs that carry a migrating mark.
Every random draw derives from SEED.

    PYTHONUTF8=1 python tools/asset-pipeline/v2/seeker_brand.py [preview dir]
"""
import json
import math
import os
import sys
from collections import deque

import numpy as np
from PIL import Image, ImageDraw

from seeker_bite import value_noise, white

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "assets", "art", "VFX", "parts")
SRC = os.path.join(HERE, "keypose_sources")
SOURCE = os.path.join(SRC, "seeker_brand_src", "pixen_1bf52462.png")   # PixelLab pixen, seed 20260941

SEED = 20260929
LOW = 3                  # the logical pixel: 1/3 of the runtime cell (~3 screen px at a whelp's brand size)
G = 28                   # the cell, logical px
BOX = 20.0               # the coil's box, logical px (the rest is room for the smoky roots)
SQUASH = (1.0, 0.80)     # lopsided: pressed flatter vertically (after the turn, so it lies wide on a flank)
TILT = 1.874             # radians: turned so it enters from the lower left
CELL = G * LOW           # the runtime cell (84 px)
PHASES = 6               # the idle swirl's phases

# THE VALUE BANDS (luminance, opacity), multiplied by the runtime tint: they ARE the value read. At rest the groove is
# the translucent MID; LIGHT is the travelling core, and the whole coil lights only on a beat (drawn hot).
DARK, MID, LIGHT = (0.42, 0.55), (0.72, 0.80), (0.92, 1.0)
# THE LADDER: (name, how far along the path the cut reaches (past 1.0 it runs on along the spiral, inward), the
# stroke's width factor, how far the ring's wall is cut in toward the hollow (logical px, never within CHANNEL of the
# inner hook), how far a burn has spread out of the ring's lower-left arc (logical px; none: it read as "just bigger")).
# Every stage is the whole coil (a partial one read as the letter G), the spiral open and the hook a thin line at every
# depth: the groove widens (deep1), bites into the hollow's wall (deep2), and the hook is cut further along the spiral,
# inward (deep3, deep4).
STAGES = [("spread", 0.95, 0.72, 0.0, 0.0), ("base", 0.95, 1.00, 0.0, 0.0),
          ("deep1", 0.95, 1.24, 0.0, 0.0), ("deep2", 0.95, 1.24, 1.2, 0.0),
          ("deep3", 1.07, 1.24, 1.2, 0.0), ("deep4", 1.18, 1.24, 1.2, 0.0)]
TIP_GAP = 1.2            # the half width (logical px) of the medial line kept open between the outer tip and its turn
CHANNEL = 2.9            # the dark channel kept round the inner hook (logical px from its centre line)
RING = (0.04, 0.80)      # the path's outer turn (the ring); past it the inner HOOK
HOOK_RUN_HW = 0.8        # the half width (logical px) of the hook's run-on past the source's inner end (deep3, deep4)
HOOK_LIP = 0.14          # a hook-cutting chisel sets in this far (path t) back along the old cut's end
COLUMNS = (["gather0", "gather1", "gather2"] + [f"idle{i}" for i in range(PHASES)] + ["edge"]
           + ["carve0", "carve1", "carve2"] + ["form0", "form1", "form2"] + ["loosen0", "loosen1"])
C_GATHER, C_IDLE, C_EDGE, C_CARVE, C_FORM, C_LOOSEN = 0, 3, 3 + PHASES, 4 + PHASES, 7 + PHASES, 10 + PHASES
THREAD_CELLS = 3
TG = 8                   # the thread puff's cell, logical px


# ── the coil's path, from the PixelLab silhouette ────────────────────────────────────────────────────────────────────

def largest(mask):
    """The largest 8-connected component."""
    h, w = mask.shape
    seen = np.zeros_like(mask)
    best = []
    for y in range(h):
        for x in range(w):
            if mask[y, x] and not seen[y, x]:
                comp, q = [], deque([(y, x)])
                seen[y, x] = True
                while q:
                    cy, cx = q.popleft()
                    comp.append((cy, cx))
                    for dy in (-1, 0, 1):
                        for dx in (-1, 0, 1):
                            ny, nx = cy + dy, cx + dx
                            if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                                seen[ny, nx] = True
                                q.append((ny, nx))
                if len(comp) > len(best):
                    best = comp
    out = np.zeros_like(mask)
    for y, x in best:
        out[y, x] = True
    return out


def geodesic(mask, start):
    """In-mask 4-neighbour walking distance from `start`; -1 outside."""
    d = np.full(mask.shape, -1.0)
    q = deque([start])
    d[start] = 0
    while q:
        cy, cx = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = cy + dy, cx + dx
            if 0 <= ny < mask.shape[0] and 0 <= nx < mask.shape[1] and mask[ny, nx] and d[ny, nx] < 0:
                d[ny, nx] = d[cy, cx] + 1
                q.append((ny, nx))
    return d


def coil_path(n=320):
    """The coil's centre line from its OUTER tip (t = 0) to the source's inner end (t = 1), in the logical cell.
    Returns (points, t per point)."""
    m = largest(np.asarray(Image.open(SOURCE).convert("RGBA"))[..., 3] > 128)
    ys, xs = np.nonzero(m)
    d0 = geodesic(m, (ys[0], xs[0]))
    a = np.unravel_index(np.argmax(d0), d0.shape)
    da = geodesic(m, a)
    b = np.unravel_index(np.argmax(da), da.shape)
    cy, cx = ys.mean(), xs.mean()
    outer = a if math.hypot(a[0] - cy, a[1] - cx) > math.hypot(b[0] - cy, b[1] - cx) else b
    d = geodesic(m, outer)
    pts, k = [], 0.0
    while k <= d.max():
        sel = (d >= k) & (d < k + 1.5)
        if sel.any():
            yy, xx = np.nonzero(sel)
            pts.append((xx.mean(), yy.mean()))
        k += 1.5
    p = np.array(pts)
    for _ in range(3):
        p[1:-1] = (p[:-2] + 2 * p[1:-1] + p[2:]) / 4
    s = np.concatenate([[0], np.cumsum(np.hypot(*np.diff(p, axis=0).T))])
    u = np.linspace(0, s[-1], n)
    p = np.stack([np.interp(u, s, p[:, 0]), np.interp(u, s, p[:, 1])], axis=1)
    # the path runs on past the source's inner end at its own (constant) curvature: the deep stages cut further along it
    step = s[-1] / (n - 1)
    head = math.atan2(p[-1][1] - p[-6][1], p[-1][0] - p[-6][0])
    tail = np.diff(p[-30:], axis=0)
    turn = float(np.mean(np.diff(np.unwrap(np.arctan2(tail[:, 1], tail[:, 0])))))
    ext, q = [], p[-1].copy()
    for i in range(int(0.22 * n)):
        head += turn
        q = q + step * 0.9 * np.array([math.cos(head), math.sin(head)])
        ext.append(q.copy())
    p = np.vstack([p, np.array(ext)])
    t = np.concatenate([np.linspace(0, 1, n), 1 + np.arange(1, len(ext) + 1) / n])
    # lopsided and leaning, with a slow wobble: a hand-burned coil, never a clean spiral
    c = (p[:n].min(0) + p[:n].max(0)) / 2
    rot = np.array([[math.cos(TILT), -math.sin(TILT)], [math.sin(TILT), math.cos(TILT)]])
    q = ((p - c) @ rot.T) * SQUASH
    rng = np.random.default_rng(SEED)
    phase = rng.random(2) * 6.283
    q *= (1 + 0.05 * np.sin(t * 11.0 + phase[0]) + 0.03 * np.sin(t * 23.0 + phase[1]))[:, None]
    mn, mx = q[:n].min(0), q[:n].max(0)
    q = (q - (mn + mx) / 2) * ((BOX - 2.0) / max(mx - mn)) + G / 2
    return q, t


PATH, TP = coil_path()
N = len(PATH)
GY, GX = np.mgrid[0:G, 0:G] + 0.5
DIST = np.full((G, G), 1e9)
TPAR = np.zeros((G, G))
for i, (px, py) in enumerate(PATH):
    dd = np.hypot(GX - px, GY - py)
    better = dd < DIST
    DIST = np.where(better, dd, DIST)
    TPAR = np.where(better, TP[i], TPAR)
CENTRE = PATH[:int(np.searchsorted(TP, 1.0))].mean(0)  # the coil's visual centre (the source's path, as pinned)


def dist_to(sel):
    """Distance to the path's points in `sel`, and the index of the nearest one, per texel."""
    d = np.full((G, G), 1e9)
    near = np.zeros((G, G), np.int32)
    for i in np.nonzero(sel)[0]:
        dd = np.hypot(GX - PATH[i][0], GY - PATH[i][1])
        better = dd < d
        d = np.where(better, dd, d)
        near = np.where(better, i, near)
    return d, near


DIST_RING, NEAR_RING = dist_to((TP >= RING[0]) & (TP <= RING[1]))
RING_T = TP[NEAR_RING]                                   # the ring's own t at every texel: the chisel travels by it
DIST_HOOK, _ = dist_to(TP > RING[1])
# the HOLLOW side of the ring: a texel nearer the coil's centre than the ring point it lies beside
INWARD = (np.hypot(GX - CENTRE[0], GY - CENTRE[1])
          < np.hypot(PATH[NEAR_RING][..., 0] - CENTRE[0], PATH[NEAR_RING][..., 1] - CENTRE[1]))
ROUGH = value_noise(G, G, 3, salt=91) * 1.2 - 0.3        # the ragged edge of the wall's inward cut
DIST_TIPARM, _ = dist_to(TP <= 0.14)
DIST_UNDER, _ = dist_to((TP >= 0.62) & (TP <= 0.86))
# THE SPIRAL STAYS OPEN: the medial line between the outer tip's arm and the turn it runs under is never cut (widened, the
# two fused and every deep stage was a closed ring round a pupil: an eye, an 'O')
OPEN = (np.abs(DIST_TIPARM - DIST_UNDER) <= TIP_GAP) & (DIST_TIPARM <= 3.2) & (DIST_UNDER <= 3.2)


def dilate(mask, steps=1):
    m = mask.copy()
    for _ in range(steps):
        n = m.copy()
        n[1:, :] |= m[:-1, :]
        n[:-1, :] |= m[1:, :]
        n[:, 1:] |= m[:, :-1]
        n[:, :-1] |= m[:, 1:]
        m = n
    return m


def erode(mask):
    m = mask.copy()
    m[1:, :] &= mask[:-1, :]
    m[:-1, :] &= mask[1:, :]
    m[:, 1:] &= mask[:, :-1]
    m[:, :-1] &= mask[:, 1:]
    return m


def neighbours4(mask):
    n = np.zeros(mask.shape, np.int32)
    n[1:, :] += mask[:-1, :]
    n[:-1, :] += mask[1:, :]
    n[:, 1:] += mask[:, :-1]
    n[:, :-1] += mask[:, 1:]
    return n


def fill_pinholes(mask, most=6):
    """Close every enclosed background speck of at most `most` texels: a dark dot inside the cut read as a pupil."""
    out = mask.copy()
    seen = np.zeros(mask.shape, bool)
    for y in range(G):
        for x in range(G):
            if mask[y, x] or seen[y, x]:
                continue
            comp, q, edge = [], deque([(y, x)]), False
            seen[y, x] = True
            while q:
                cy, cx = q.popleft()
                comp.append((cy, cx))
                edge |= cy in (0, G - 1) or cx in (0, G - 1)
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < G and 0 <= nx < G and not mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((ny, nx))
            if not edge and len(comp) <= most:
                for cy, cx in comp:
                    out[cy, cx] = True
    return out


def clean(mask):
    """No orphan texel (no 4-neighbour) and no diagonal-only pair: a lone pixel or a diagonal step reads as a glyph's
    serif at the game's scale ('a 1', 'a 9'), never as a cut."""
    m = mask.copy()
    for _ in range(2):
        m &= neighbours4(m) > 0
    return drop_specks(m)


def drop_specks(mask, least=6):
    """Every 4-connected piece under `least` texels removed: a detached dash inside the ring read as a nub or a glyph."""
    out = mask.copy()
    seen = np.zeros(mask.shape, bool)
    for y in range(G):
        for x in range(G):
            if not mask[y, x] or seen[y, x]:
                continue
            comp, q = [], deque([(y, x)])
            seen[y, x] = True
            while q:
                cy, cx = q.popleft()
                comp.append((cy, cx))
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < G and 0 <= nx < G and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((ny, nx))
            if len(comp) < least:
                for cy, cx in comp:
                    out[cy, cx] = False
    return out


def half_width(mul, reveal):
    """The stroke's half width along the coil: pressed hardest where it entered, a chisel front where the cut stops."""
    w = (1.45 - 0.45 * np.minimum(TPAR, 1.0)) * mul
    w = np.where(TPAR > 1.0, np.minimum(w, HOOK_RUN_HW), w)   # the hook's run-on stays a thin line: at the groove's width
                                                              # it swelled into a knot at the coil's centre (round 5)
    w = w * np.clip((reveal - TPAR) / 0.07, 0.35, 1.0)
    return w * np.clip(TPAR / 0.05, 0.55, 1.0)


def stroke(k, reveal=None):
    """The cut's pixels at stage k (optionally cut short at `reveal` of the path). The deep stages cut the RING's wall in
    toward the hollow (ragged, never within CHANNEL of the inner hook) and run the hook further along the spiral: the
    spiral stays open (OPEN is never cut) and the hook a thin line of its own."""
    name, full, mul, inward, outward = STAGES[k]
    rv = full if reveal is None else min(full, reveal)
    inside = (TPAR <= rv) & (DIST <= half_width(mul, rv))
    if (inward > 0 or outward > 0) and rv >= full:
        ring_hw = (1.45 - 0.45 * 0.5) * mul
        if inward > 0:
            inside |= INWARD & (DIST_RING <= ring_hw + inward + ROUGH) & (DIST_HOOK > CHANNEL)
        if outward > 0:
            arc = np.clip((RING_T - 0.42) / 0.08, 0, 1) * np.clip((0.80 - RING_T) / 0.08, 0, 1)
            inside |= ~INWARD & (arc > 0) & (DIST_RING <= ring_hw + (outward + ROUGH) * arc)
        inside = fill_pinholes(inside)
    inside &= ~OPEN
    return clean(inside)


# ── the cells ────────────────────────────────────────────────────────────────────────────────────────────────────────

def cut_rest(k, phase):
    """The groove at rest, swirl `phase` (0..1): a band map (0 none, 1 dark, 2 mid, 3 light). Translucent MID, a DARK
    rim, and the LIGHT core only along a travelling third of the coil (never the spread coil)."""
    name = STAGES[k][0]
    inside = stroke(k)
    band = np.where(inside, 2, 0).astype(np.uint8)
    rim = inside & ~erode(inside)
    band[rim & (DIST > 0.9) & ~INWARD] = 1              # the outer edge only: a dark line round the hollow drew a counter
    core = inside & (DIST <= 0.75) & (TPAR <= RING[1])  # the RING's core: the hook is never lit at rest (a pupil)
    if name != "spread":
        along = (TPAR * 1.4 - phase) % 1.0          # a travelling third, wrapping round the coil
        band[core & (along < 0.34)] = 3
    else:
        band[inside] = 1                            # one dark band: in a stroke this thin, its few interior texels lit up
                                                    # as lone lavender specks along an otherwise dim coil
    return band


def halo(k, phase):
    """The smoky roots (alpha 0..1): 1-2 px out of the cut, broken by holes, HELD STILL across the idle phases (a root
    pattern redrawn every step was the loudest motion the idle made); never filling the hollow (drawn as ink on a light
    body, a filled hollow closed the host's own outlines into a dark disc: an eye)."""
    name = STAGES[k][0]
    inside = stroke(k)
    grain = value_noise(G, G, 4, salt=40)
    near = dilate(inside, 1) & ~inside
    far = dilate(inside, 2) & ~dilate(inside, 1)
    a = np.zeros((G, G), np.float32)
    a[inside] = 0.7
    a[near] = np.where(grain[near] > 0.28, 0.55, 0.0)
    a[far] = np.where(grain[far] > 0.52, 0.30, 0.0)
    if name == "spread":
        a *= 0.7
    return np.clip(a, 0, 1).astype(np.float32)


def puff(rng, size):
    """A clumped smoke puff: an irregular 2x2 .. 3x3 knot of logical pixels, never a round bead or a lone texel."""
    k = np.ones((size, size), bool)
    if size == 3:
        for (y, x) in ((0, 0), (0, 2), (2, 0), (2, 2)):
            if rng.random() < 0.6:
                k[y, x] = False
    return k


def gather(k, step):
    """Smoke drawn IN: an IRREGULAR cluster of clumped puffs around and over the coil (step 0 loose and wide, step 1
    closer, over the path), never evenly spaced round it (a dotted ring read as a lock-on); then the coil condensed but
    not yet cut (2)."""
    rng = np.random.default_rng(SEED + 100 + 10 * k + step)
    rv = STAGES[k][1]
    inside = stroke(k)
    cut = np.zeros((G, G), np.uint8)
    smoke = np.zeros((G, G), np.float32)
    if step < 2:
        count = (5, 8)[step]
        for j in range(count):
            # a random point of the coil, pushed out from its centre by a random amount (step 0 further), jittered
            t = rng.random() * rv
            px, py = PATH[int(np.searchsorted(TP, t))]
            dx, dy = px - CENTRE[0], py - CENTRE[1]
            push = (0.5 + rng.random() * 0.9) if step == 0 else (0.1 + rng.random() * 0.35)
            x = int(round(CENTRE[0] + dx * (1 + push) + rng.normal(0, 1.2) - 1))
            y = int(round(CENTRE[1] + dy * (1 + push) + rng.normal(0, 1.2) - 1))
            size = 3 if (step == 0 or rng.random() < 0.6) else 2
            # inside the resting halo's reach (the head clearance is measured on it) and never clipped by the cell's
            # border (a puff cut there drew a straight edge)
            x = min(max(x, int(CENTRE[0] - 11)), int(CENTRE[0] + 11) - size)
            y = min(max(y, int(CENTRE[1] - 9)), int(CENTRE[1] + 9) - size)
            p = puff(rng, size)
            for yy in range(size):
                for xx in range(size):
                    if p[yy, xx] and 0 <= y + yy < G and 0 <= x + xx < G:
                        cut[y + yy, x + xx] = 2 if step == 1 else 1
                        smoke[y + yy, x + xx] = 0.55
        smoke = np.maximum(smoke, np.where(dilate(cut > 0, 1), 0.35, 0.0))
    else:
        cut = np.where(inside, 1, 0).astype(np.uint8)
        cut[inside & (DIST <= 0.75)] = 2
        smoke = halo(k, 0.0) * 0.9
    return cut, np.clip(smoke, 0, 1).astype(np.float32)


def edge(k):
    """The whole coil's lit line (the APPLY beat, drawn hot): its core, one continuous line (never the wall's inner contour)."""
    inside = stroke(k)
    line = inside & (DIST <= 0.75) & (TPAR <= STAGES[k][1])
    return np.where(clean(line), 3, 0).astype(np.uint8)


def carve(k, step):
    """The DEEPEN beat's CHISEL (step 0, 1, 2), drawn hot: ONLY the new cut (stage k less stage k - 1), in three
    stretches of equal size travelling round the ring toward the hook (by the ring's own t) and on along the hook,
    inward (by the path's t: deep3 / deep4 cut the hook further along the spiral); each step keeps what the steps
    before it cut (MID) and lights its own stretch (LIGHT). The runtime keeps the old cut on screen until the last
    step, so the new cut appears under the chisel as it travels; it never relights old groove and never lights a knot."""
    band = np.zeros((G, G), np.uint8)
    if k == 0:
        return band                                   # nothing deepens INTO the spread coil
    now = stroke(k)
    if STAGES[k][1] > STAGES[k - 1][1]:
        # a stage that runs the HOOK further along the spiral (deep3, deep4): the chisel sets in at the end of the old
        # cut and runs on inward into the new one (the new cut alone is a dozen texels: split three ways, a speck)
        new = now & (TPAR > RING[1]) & (TPAR >= STAGES[k - 1][1] - HOOK_LIP)
    elif STAGES[k][3] == 0 and STAGES[k][4] == 0:
        # a stage that only WIDENS the groove (spread -> base -> deep1) adds a 1-px rim all round, which alone drew a
        # dotted ring: its chisel is the new groove's own core line, travelling round the ring
        new = now & (DIST <= 0.75)
    else:
        # the NEW cut and a 1-px lip of the groove it bites into: a coherent band, never a scatter of single texels
        new = clean(now & dilate(now & ~stroke(k - 1), 1))
    ys, xs = np.nonzero(new)
    if len(ys) == 0:
        return band
    along = np.where(TPAR > RING[1], TPAR, RING_T)     # round the ring, then on along the hook
    order = np.argsort(along[ys, xs], kind="stable")
    thirds = np.array_split(order, 3)
    widening = STAGES[k][3] == 0 and STAGES[k][4] == 0
    for j in range(0 if not widening else step, step + 1):
        for i in thirds[j]:
            band[ys[i], xs[i]] = 3 if j == step else 2     # a cut the steps before made stays (MID); a widening's line travels
    band[~drop_specks(band > 0, 4)] = 0            # no lone speck in a chisel stretch
    return band


def form(k, step):
    """The coil DRAWN IN along its path from its outer tip, where the migrating smoke lands (step 0, 1, 2 of 3)."""
    rv = STAGES[k][1]
    inside = stroke(k, reveal=rv * (step + 1) / 3.0) if step < 2 else stroke(k)
    band = np.where(inside, 2, 0).astype(np.uint8)
    band[inside & (DIST <= 0.75)] = 3
    smoke = np.where(dilate(inside, 1) & ~inside, 0.45, 0.0).astype(np.float32)
    return band, smoke


def loosen(k, step):
    """The coil coming apart into smoke (step 0, 1): its pieces clumping into puffs and lifting, the smoke rising."""
    rng = np.random.default_rng(SEED + 200 + 10 * k + step)
    inside = stroke(k)
    lift = step + 1
    cut = np.zeros((G, G), np.uint8)
    keep = (0.55, 0.28)[step]
    pieces = np.argwhere(inside)
    for idx in range(0, len(pieces), 5):
        y, x = pieces[idx]
        if rng.random() <= keep:
            size = 3 if rng.random() < 0.5 else 2
            p = puff(rng, size)
            oy, ox = int(rng.integers(-1, 1)) - lift, int(rng.integers(-1, 2))
            for yy in range(size):
                for xx in range(size):
                    ty, tx = y + oy + yy - 1, x + ox + xx - 1
                    if p[yy, xx] and 0 <= ty < G and 0 <= tx < G:
                        cut[ty, tx] = 2          # both steps MID: a DARK second half vanished inside the flash
    grain = value_noise(G, G, 4, salt=220 + step)
    wisps = np.roll(dilate(inside, step + 1), -lift - 1, axis=0) & (grain > (0.42, 0.58)[step])
    smoke = np.where(wisps, (0.45, 0.28)[step], 0.0).astype(np.float32)
    return cut, smoke


def thread(step):
    """A migrating mark's smoke: clumped puffs (a strand is drawn from several, overlapping), in the groove's own bands:
    a MID body inside a ragged DARK ink rim (one flat tint read as a violet slab, a dash thrown between creatures), never
    a lit centre (that made the strand a row of beads)."""
    rng = np.random.default_rng(SEED + 300 + step)
    b = np.zeros((TG, TG), np.uint8)
    size = (5, 4, 3)[step]
    o = (TG - size) // 2
    p = np.ones((size, size), bool)
    for (y, x) in ((0, 0), (0, size - 1), (size - 1, 0), (size - 1, size - 1)):
        p[y, x] = rng.random() < 0.3
    if size >= 4:
        for _ in range(2):                              # a ragged rim: a bite out of an edge
            y, x = (int(rng.integers(1, size - 1)), int(rng.choice([0, size - 1])))
            p[y if rng.random() < 0.5 else x, x if rng.random() < 0.5 else y] = False
    core = p & erode(p)
    b[o:o + size, o:o + size] = np.where(core, 2, np.where(p, 1, 0))
    return b


# ── images ───────────────────────────────────────────────────────────────────────────────────────────────────────────

def cut_image(bands):
    lum = np.select([bands == 1, bands == 2, bands == 3], [DARK[0], MID[0], LIGHT[0]], 0.8).astype(np.float32)
    a = np.select([bands == 1, bands == 2, bands == 3], [DARK[1], MID[1], LIGHT[1]], 0.0).astype(np.float32)
    k = np.ones((LOW, LOW), np.float32)
    return white(np.kron(a, k), np.kron(lum, k))


def halo_image(alpha):
    q = np.round(np.clip(alpha, 0, 1) * 8) / 8        # a few alpha steps, never a gradient
    k = np.ones((LOW, LOW), np.float32)
    return white(np.kron(q, k))


def cells(k):
    """Stage k's cells, in COLUMNS order: (cut bands, halo alpha)."""
    z = np.zeros((G, G), np.float32)
    out = [gather(k, s) for s in range(3)]
    out += [(cut_rest(k, ph / PHASES), halo(k, ph / PHASES)) for ph in range(PHASES)]
    out.append((edge(k), z))
    out += [(carve(k, s), z) for s in range(3)]
    out += [form(k, s) for s in range(3)]
    out += [loosen(k, s) for s in range(2)]
    return out


def build():
    """ONE atlas, so a brand costs no texture switch: the CUT cells (a row per stage, a column per COLUMNS entry), the
    HALO cells in the same layout below them, then the THREAD puffs in the last row."""
    rows = len(STAGES)
    atlas = Image.new("RGBA", (CELL * len(COLUMNS), CELL * rows * 2 + TG * LOW), (0, 0, 0, 0))
    for k in range(rows):
        for c, (cut, hal) in enumerate(cells(k)):
            atlas.alpha_composite(cut_image(cut), (c * CELL, k * CELL))
            atlas.alpha_composite(halo_image(hal), (c * CELL, (rows + k) * CELL))
    for t in range(THREAD_CELLS):
        atlas.alpha_composite(cut_image(thread(t)), (t * TG * LOW, rows * 2 * CELL))
    return atlas


def spans():
    tip = PATH[0]
    return {
        "cell": CELL, "low": LOW, "grid": G, "stages": [s[0] for s in STAGES], "columns": COLUMNS,
        "thread_cell": TG * LOW, "thread_cells": THREAD_CELLS, "halo_row0": len(STAGES),
        "thread_y": len(STAGES) * 2 * CELL, "phases": PHASES,
        # the coil's visual centre in the cell (runtime px), the point laid on the body's torso
        "centre": [round(float(CENTRE[0]) * LOW, 1), round(float(CENTRE[1]) * LOW, 1)],
        # the coil's outer tip (runtime px): where a migrating mark lands and the coil is drawn in from
        "tip": [round(float(tip[0]) * LOW, 1), round(float(tip[1]) * LOW, 1)],
        "box": BOX * LOW,
    }


def preview(atlas, path):
    """Every stage over the floor grey, over the whelp's near-black and over a light body, as the runtime draws them:
    the halo in ink, the halo again in lit smoke, the cut in the groove tint; a '-405' beside each row, so no cell can
    pass for a digit unnoticed."""
    rows = len(STAGES)
    cut = np.asarray(atlas.crop((0, 0, atlas.width, rows * CELL))).astype(np.float32) / 255
    hal = np.asarray(atlas.crop((0, rows * CELL, atlas.width, rows * 2 * CELL))).astype(np.float32)[..., 3] / 255
    tiles = []
    for bg in [(58, 56, 54), (10, 8, 12), (196, 190, 184)]:
        img = np.zeros(cut.shape[:2] + (3,), np.float32) + np.array(bg, np.float32) / 255
        for tint, amt in (((12, 7, 20), 0.85), ((110, 92, 160), 0.6)):
            a = hal[..., None] * amt
            img = img * (1 - a) + np.array(tint, np.float32) / 255 * a
        g = np.array([136, 108, 196], np.float32) / 255
        a = cut[..., 3:4]
        img = img * (1 - a) + cut[..., :3] * g * a
        t = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
        d = ImageDraw.Draw(t)
        for r in range(rows):
            d.text((atlas.width - 60, r * CELL + 36), "-405", fill=(236, 226, 206))
        tiles.append(t)
    out = Image.new("RGB", (atlas.width, rows * CELL * 3 + 20), (0, 0, 0))
    for i, t in enumerate(tiles):
        out.paste(t, (0, i * (rows * CELL + 10)))
    out.save(path)


def main():
    preview_dir = sys.argv[1] if len(sys.argv) > 1 else None
    out = preview_dir or OUT
    os.makedirs(out, exist_ok=True)
    atlas = build()
    atlas.save(os.path.join(out, "fxp_seeker_brand.png"))
    sheet_path = os.path.join(preview_dir or SRC, "seeker_brand_sheet.png")
    preview(atlas, sheet_path)
    if not preview_dir:
        with open(os.path.join(SRC, "seeker_brand_spans.json"), "w", encoding="utf-8") as f:
            json.dump(spans(), f, indent=2)
    print("atlas", atlas.size, "->", out, "| sheet", sheet_path)
    print(json.dumps(spans()))


if __name__ == "__main__":
    main()
