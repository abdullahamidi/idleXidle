#!/usr/bin/env python3
"""seeker_press.py -- PRESS, the PERSISTENT FIELD reference (ADR-011): its parts, white or grey (premultiplied at load) and
TINTED at runtime along the Seeker's Shadow palette. Every random draw derives from a fixed seed plus a salt.

PIXEL-HARD PASS (the owner, 2026-09-28: the direction is approved, the craft was "too smooth, too soft and too
vector-like for the game's pixel-art presentation"). The tick's parts are AUTHORED AT A LOW LOGICAL RESOLUTION, in a few
VALUE BANDS (dark body, violet mass, lavender edge, a tiny pale-hot accent), their edges broken into a few meaningful
segments and notches, given one source pixel of soften at most, then upscaled NEAREST to the runtime cell. Each part's
logical pixel is chosen so that, at the size it is DRAWN, it lands near the world's pixel (~3 screen px: the Seeker's
strips are authored at 3 px a pixel): the wave at 1/WAVE_LOW (it grows in flight; 1/5 matches on arrival), the clamp at
1/CLAMP_LOW (drawn at about a third of its cell; at 1/4 its pixels were half the world's and read as a thin HD blade).
Holes and dissolves are AUTHORED, not a uniform random mask (that read as grain / static): the dissolve ERODES from the
edges inward, keeps a few large fragments and lets them fall a pixel a step. Only the FIELD stays soft: at rest it is
quiet on purpose, and the tick is the hard beat.

    fxp_seeker_press_field   the quiet field around the Seeker (soft): a haze leaning toward the enemy side
    fxp_seeker_press_wave    the pressure FRONT, four cells: BODY (every band), EDGE (the edge and the accent only, for the
                             light pass), ECHO (one solid darker copy of the shape, no mask), DISSOLVE (eroded into a few
                             fragments as it collapses into the crush)
    fxp_seeker_press_clamp   the CRUSH arc pressing down (flipped below the target), five cells: whole, three erosion
                             states for its release, and EDGE (the pressing edge and the contact accent only, for the
                             light pass: the tick's heat lights a line, never the arc's body -- the whole arc lit pale
                             made the tick as bright as SPRAY's hit)

    PYTHONUTF8=1 python tools/asset-pipeline/v2/seeker_press.py [preview dir]
"""
import json
import os
import sys

import numpy as np
from PIL import Image

from seeker_bite import blur, smoothstep, value_noise, white, sheet

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "assets", "art", "VFX", "parts")
SRC = os.path.join(HERE, "keypose_sources")

WAVE_LOW = 5             # the wave's logical pixel: 1/5 of its runtime cell
CLAMP_LOW = 8            # the clamp's logical pixel: 1/8 of its runtime cell
FW, FH = 320, 448        # the field (soft)
WW, WH = 192, 512        # the wave, per cell (runtime)
CW, CH = 512, 160        # the clamp, per cell (runtime)

# THE VALUE BANDS (luminance, opacity): the runtime tint is multiplied by these, so they ARE the value read. The edge is a
# lavender, not a near-white: a white rim for the whole travel pushed the front toward a thrown sickle blade.
DARK, MID, LIGHT, PEAK = (0.36, 0.60), (0.60, 0.82), (0.82, 1.0), (1.0, 1.0)
EDGE_LIGHT = 0.4         # the clamp's EDGE cell: its lit line's opacity (the PEAK accent at the contact stays whole)
CLAMP_BOW = 11.0         # the arc's bow toward the body at its middle, in cell px per logical px of `s` (7.5 read flat)


def lw(n, low):
    """A runtime length in logical pixels (rounded up; the upscale is cropped back to the cell)."""
    return -(-n // low)


def banded(bands):
    """(lum, alpha) arrays from a band-index map (0 none, 1 dark, 2 mid, 3 light, 4 peak)."""
    lum = np.zeros(bands.shape, np.float32)
    a = np.zeros(bands.shape, np.float32)
    for k, (l, al) in enumerate((DARK, MID, LIGHT, PEAK), start=1):
        lum = np.where(bands == k, l, lum)
        a = np.where(bands == k, al, a)
    return lum, a


def to_image(bands, low, size, light=1.0):
    """At most one SOURCE pixel of soften (at the logical resolution), then NEAREST upscale to the runtime cell. `light`
    scales the LIGHT band's opacity (the clamp's edge cell: a lit line, not a slab)."""
    lum, a = banded(bands)
    a = np.where(bands == 3, a * light, a)
    a = np.clip(blur(a, 0.4), 0, 1)
    lum = np.where(a > 0.02, lum, 0.8)
    W, H = size
    k = np.ones((low, low), np.float32)
    return white(np.kron(a, k)[:H, :W], np.kron(np.clip(lum, 0, 1), k)[:H, :W])


def erode(mask):
    """One step of 4-neighbour erosion of a boolean mask."""
    m = mask.copy()
    m[1:, :] &= mask[:-1, :]
    m[:-1, :] &= mask[1:, :]
    m[:, 1:] &= mask[:, :-1]
    m[:, :-1] &= mask[:, 1:]
    return m


def dissolve(bands, steps, salt, holes=0.12, fall=1):
    """AUTHORED loss of cohesion: the shape erodes `steps` pixels from its edges inward, a few LARGE holes (3x3) open in
    what is left, and the remaining fragments fall `fall` pixels (downward in the cell)."""
    solid = bands > 0
    core = solid
    for _ in range(steps):
        core = erode(core)
    rng = np.random.default_rng(20260928 + salt)
    h, w = bands.shape
    gone = np.zeros((h, w), bool)
    for _ in range(int(holes * h * w / 9)):
        y, x = rng.integers(0, h), rng.integers(0, w)
        gone[max(0, y - 1):y + 2, max(0, x - 1):x + 2] = True
    kept = np.where(core & ~gone, bands, 0)
    if fall:
        kept = np.roll(kept, fall, axis=0)
        kept[:fall, :] = 0
    return kept


def field():
    """NOT a ring (a closed ring read as a buff): a soft pressure HAZE that leans toward the enemy side, strongest in
    front of the body and low, fading round to nothing behind and above it, broken by smoke; two faint broken pressure
    lines just outside it on the enemy side. The middle is empty: the Seeker stands in it. It stays SOFT on purpose."""
    ys, xs = np.mgrid[0:FH, 0:FW].astype(np.float32)
    nx, ny = (xs - FW / 2) / (FW * 0.46), (ys - FH * 0.52) / (FH * 0.46)
    r = np.hypot(nx, ny)
    ang = np.arctan2(ny, nx)                                     # 0 = the enemy side
    n = value_noise(FW, FH, 10, 401) * 0.6 + value_noise(FW, FH, 5, 402) * 0.4
    front = np.clip(np.cos(ang), 0, 1) ** 1.5                    # the enemy-facing side
    lean = np.clip(0.5 + 0.5 * np.cos(ang), 0, 1) ** 2.2          # 1 in front, 0 behind: the pressure leans forward
    shell = np.exp(-((r - 0.78) / 0.16) ** 2) * lean * (0.45 + 0.55 * n)
    lines = np.zeros_like(r)
    for rr, w, g in ((0.90, 0.018, 0.55), (0.98, 0.014, 0.35)):
        broken = smoothstep(0.25, 0.55, value_noise(FW, FH, 7, 403 + int(rr * 100)))
        lines = np.maximum(lines, np.exp(-((r - rr) / w) ** 2) * front * broken * g)
    edge = smoothstep(0.0, 14.0, np.minimum(np.minimum(xs, FW - 1 - xs), np.minimum(ys, FH - 1 - ys)))
    a = np.clip(np.maximum(blur(shell, 3.0) * 0.8, blur(lines, 1.6)), 0, 1) * edge
    lum = np.where(lines > shell * 0.8, 1.0, 0.8)
    return white(a, lum)


def wave_bands():
    """The pressure front at its logical resolution, facing RIGHT: an arc of a circle centred far to its left; its
    leading edge UNDER STRESS (five segments stepped a pixel apart, two notches); behind it the bands: a 1-2 px lavender
    edge, the violet mass, the dark body (solid, its back edge cut by two deliberate notches, never a random grain);
    the pale-hot accent a few pixels at the middle of the edge."""
    low = WAVE_LOW
    w, h = lw(WW, low), lw(WH, low)
    s = 4.0 / low                                                   # a 1/4-design pixel in logical pixels
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32) + 0.5
    R = h * 0.60
    cx, cy = w * 0.80 - R, h / 2
    d = np.hypot(xs - cx, ys - cy)
    th = np.arctan2(ys - cy, xs - cx)
    span = np.radians(52)
    q = np.clip(th / span, -1, 1)                                   # -1 top horn, 1 bottom horn
    along = np.clip(1 - np.abs(q) ** 2.2, 0, 1)
    seg = np.clip(((q + 1) / 2 * 5).astype(int), 0, 4)
    step = np.array([0.0, -1.4, 0.4, -0.9, 0.6], np.float32)[seg] * s
    notch = ((np.abs(q - 0.38) < 0.05) | (np.abs(q + 0.46) < 0.04)).astype(np.float32) * 1.6 * s
    depth = (R + step - notch) - d                                  # >0 behind the leading edge
    thick = (25 * along ** 0.8 + 1) * s
    back_cut = ((np.abs(q - 0.12) < 0.07) | (np.abs(q + 0.62) < 0.06)) & (depth > 0.72 * thick)   # two notches in the back
    inside = (depth >= 0) & (depth < thick) & (along > 0.02) & (np.abs(th) <= span) & ~back_cut
    bands = np.zeros((h, w), np.int32)
    bands = np.where(inside & (depth >= 0.5 * thick), 1, bands)                        # DARK body (solid)
    bands = np.where(inside & (depth >= 1.2 * s) & (depth < 0.5 * thick), 2, bands)     # MID violet mass
    bands = np.where(inside & (depth < 1.2 * s), 3, bands)                             # LIGHT edge
    bands = np.where(inside & (depth < 0.9 * s) & (np.abs(q) < 0.12), 4, bands)         # PEAK accent
    return bands


def wave_cells():
    bands = wave_bands()
    edge = np.where(bands >= 3, bands, 0)
    echo = np.where(bands > 0, 1, 0)                                # ONE solid darker copy of the shape, no mask
    dis = dissolve(bands, steps=1, salt=13, holes=0.10, fall=1)
    return [to_image(x, WAVE_LOW, (WW, WH)) for x in (bands, edge, echo, dis)]


def clamp_bands():
    """The crush arc at its logical resolution, pressing DOWN: a shallow arc bowing down in the middle; its pressing
    (lower) edge LIGHT, a notch and three stepped segments; the MID mass; the DARK wake above (solid)."""
    low = CLAMP_LOW
    w, h = lw(CW, low), lw(CH, low)
    s = 4.0 / low
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32) + 0.5
    u = (xs - w / 2) / (w * 0.46)
    along = np.clip(1 - np.abs(u) ** 2.4, 0, 1)
    seg = np.clip(((np.clip(u, -1, 1) + 1) / 2 * 3).astype(int), 0, 2)
    step = np.array([0.5, 0.0, -0.7], np.float32)[seg] * s
    edge_y = h * 0.52 + CLAMP_BOW * s * np.clip(1 - u ** 2, 0, 1) + step   # bowed toward the body (a flat slab read as a bar)
    notch = (np.abs(u - 0.3) < 0.06).astype(np.float32) * 1.2 * s
    depth = edge_y - notch - ys                                    # >0 above the pressing edge
    thick = max(2.5, 12 * s) * along ** 0.8 + 1
    inside = (depth >= 0) & (depth < thick) & (along > 0.03)
    bands = np.zeros((h, w), np.int32)
    bands = np.where(inside & (depth >= 0.55 * thick), 1, bands)
    bands = np.where(inside & (depth >= 1.0) & (depth < 0.55 * thick), 2, bands)
    bands = np.where(inside & (depth < 1.0), 3, bands)
    bands = np.where(inside & (depth < 1.0) & (np.abs(u) < 0.08), 4, bands)   # the PEAK: a short accent at the contact
    return bands


def clamp_cells():
    bands = clamp_bands()
    cells = [bands]
    for k, (steps, holes, fall) in enumerate(((0, 0.10, 0), (1, 0.14, 1), (1, 0.30, 1))):   # the release: loses cohesion
        cells.append(dissolve(bands, steps=steps, salt=30 + k, holes=holes, fall=fall))
    out = [to_image(c, CLAMP_LOW, (CW, CH)) for c in cells]
    out.append(to_image(np.where(bands >= 3, bands, 0), CLAMP_LOW, (CW, CH), light=EDGE_LIGHT))   # EDGE: the light pass
    return out


def strip(cells):
    out = Image.new("RGBA", (sum(c.width for c in cells), cells[0].height), (0, 0, 0, 0))
    x = 0
    for c in cells:
        out.alpha_composite(c, (x, 0))
        x += c.width
    return out


def main():
    f = field()
    w = strip(wave_cells())
    c = strip(clamp_cells())
    assert w.size == (WW * 4, WH) and c.size == (CW * 5, CH), (w.size, c.size)
    out = sys.argv[1] if len(sys.argv) > 1 else OUT
    os.makedirs(out, exist_ok=True)
    f.save(os.path.join(out, "fxp_seeker_press_field.png"))
    w.save(os.path.join(out, "fxp_seeker_press_wave.png"))
    c.save(os.path.join(out, "fxp_seeker_press_clamp.png"))
    if out != OUT:
        print("wrote the PRESS parts (preview) to", out)
        return
    spans = {
        "wave_logical_resolution": WAVE_LOW, "clamp_logical_resolution": CLAMP_LOW,
        "field_cell": [FW, FH], "wave_cell": [WW, WH], "wave_cells": 4, "clamp_cell": [CW, CH], "clamp_cells": 5,
        "wave_lead_x": round(WW * 0.80, 1),                      # the leading edge's x at the middle of a wave cell
        "clamp_press_y": round(CH * 0.52 + CLAMP_BOW * 4.0, 1),  # the pressing edge's y at the middle of the clamp
    }
    with open(os.path.join(SRC, "seeker_press_spans.json"), "w", encoding="utf-8") as fh:
        json.dump(spans, fh, indent=2)
    sheet([("field", f), ("wave", w), ("clamp", c)], os.path.join(SRC, "seeker_press_sheet.png"))
    print("wrote the PRESS parts and", os.path.join(SRC, "seeker_press_spans.json"), spans)


if __name__ == "__main__":
    main()
