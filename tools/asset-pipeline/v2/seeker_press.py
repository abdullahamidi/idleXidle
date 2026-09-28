#!/usr/bin/env python3
"""seeker_press.py -- PRESS, the PERSISTENT FIELD reference (ADR-011, 2026-09-28): three procedural parts, white or grey
(premultiplied at load) and TINTED at runtime along the Seeker's Shadow palette. Every random draw derives from SEED.

    fxp_seeker_press_field   the quiet pressure field around the Seeker: a soft smoky shell and two faint broken pressure
                             lines on its enemy-facing side (never a ring buff: no hard circle, no glow disc)
    fxp_seeker_press_wave    the travelling pressure FRONT (after Syndra E's motion grammar: one broad force arc from the
                             caster toward the target side): a crescent, thick in the middle and tapering to its horns, a
                             bright leading rim, a soft wake behind it with two faint compression striations; two cells,
                             crisp and softened (the softened one trails it as its afterimage)
    fxp_seeker_press_clamp   the CRUSH: a shallow arc bowing onto the target, its pressing edge bright, a wake behind it;
                             drawn above the creature pressing down and, flipped, below it pressing up

    PYTHONUTF8=1 python tools/asset-pipeline/v2/seeker_press.py
"""
import json
import os

import numpy as np
from PIL import Image

from seeker_bite import blur, smoothstep, value_noise, white, sheet

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "assets", "art", "VFX", "parts")
SRC = os.path.join(HERE, "keypose_sources")
SEED = 20260928          # (value_noise derives every draw from seeker_bite's SEED plus a salt)

FW, FH = 320, 448        # the field
WW, WH = 192, 512        # the wave, per cell
CW, CH = 512, 160        # the clamp


def field():
    """NOT a ring (a closed ring read as a buff): a soft pressure HAZE that leans toward the enemy side, strongest in
    front of the body and low, fading round to nothing behind and above it, broken by smoke; two faint broken pressure
    lines just outside it on the enemy side. The middle is empty: the Seeker stands in it."""
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


def wave_cell(soft):
    """The pressure front, facing RIGHT: an arc of a circle centred far to its left, spanning +-68 degrees; the band's
    thickness follows cos(theta) (thick middle, tapering horns); across the band the alpha rises sharply at the leading
    rim and falls away behind it (the wake), with two faint striations parallel to the front."""
    ys, xs = np.mgrid[0:WH, 0:WW].astype(np.float32)
    R = WH * 0.60
    cx, cy = WW * 0.80 - R, WH / 2
    d = np.hypot(xs - cx, ys - cy)
    th = np.arctan2(ys - cy, xs - cx)
    span = np.radians(52)
    along = np.clip(1 - (np.abs(th) / span) ** 2.2, 0, 1)        # 1 in the middle, 0 at the horns
    depth = (R - d)                                              # >0 behind the leading rim
    thick = 100 * along ** 0.8 + 2                               # a thick wake: pressure, not a thin slash beam
    band = np.where((depth > -3) & (depth < thick), 1.0, 0.0)
    wake = np.exp(-np.clip(depth, 0, None) / (0.42 * thick + 1e-3)) * smoothstep(-3, 1.5, depth)
    rim = np.exp(-((depth - 1.0) / 2.2) ** 2)
    n = value_noise(WW, WH, 9, 411) * 0.6 + value_noise(WW, WH, 4, 412) * 0.4
    broken = smoothstep(0.45, 0.7, value_noise(WW, WH, 6, 413))
    striae = sum(np.exp(-((depth - k * thick) / 2.2) ** 2) * 0.12 for k in (0.35, 0.62)) * broken
    a = (np.maximum(rim * 0.9, wake * 0.8 * (0.35 + 0.65 * n)) + striae) * band * along ** 0.6
    lum = np.clip(0.72 + 0.28 * rim + 0.1 * striae, 0, 1)
    if soft:
        a = blur(a, 5.0) * 0.8
        lum = np.full_like(lum, 0.8)
    edge = smoothstep(0.0, 6.0, np.minimum(np.minimum(xs, WW - 1 - xs), np.minimum(ys, WH - 1 - ys)))
    return white(np.clip(a, 0, 1) * edge, lum)


def clamp():
    """The crush, pressing DOWN: a shallow arc bowing down in the middle, its pressing (lower) edge bright, a wake above,
    thick in the middle and tapering to the ends."""
    ys, xs = np.mgrid[0:CH, 0:CW].astype(np.float32)
    u = (xs - CW / 2) / (CW * 0.46)                              # -1..1 across
    along = np.clip(1 - np.abs(u) ** 2.4, 0, 1)
    edge_y = CH * 0.52 + 30 * np.clip(1 - u ** 2, 0, 1)          # the pressing edge, lowest in the middle (y grows down)
    depth = edge_y - ys                                          # >0 above the pressing edge (the wake)
    thick = 66 * along ** 0.8 + 2
    band = np.where((depth > -3) & (depth < thick), 1.0, 0.0)
    wake = np.exp(-np.clip(depth, 0, None) / (0.45 * thick + 1e-3)) * smoothstep(-3, 1.5, depth)
    rim = np.exp(-((depth - 1.0) / 2.0) ** 2)
    n = value_noise(CW, CH, 9, 421) * 0.5 + 0.5
    a = np.maximum(rim, wake * 0.7 * n) * band * along ** 0.5
    lum = np.clip(0.75 + 0.25 * rim, 0, 1)
    edge = smoothstep(0.0, 6.0, np.minimum(np.minimum(xs, CW - 1 - xs), np.minimum(ys, CH - 1 - ys)))
    return white(np.clip(a, 0, 1) * edge, lum)


def main():
    os.makedirs(OUT, exist_ok=True)
    f = field()
    w = Image.new("RGBA", (WW * 2, WH), (0, 0, 0, 0))
    w.alpha_composite(wave_cell(False), (0, 0))
    w.alpha_composite(wave_cell(True), (WW, 0))
    c = clamp()
    f.save(os.path.join(OUT, "fxp_seeker_press_field.png"))
    w.save(os.path.join(OUT, "fxp_seeker_press_wave.png"))
    c.save(os.path.join(OUT, "fxp_seeker_press_clamp.png"))
    spans = {
        "field_cell": [FW, FH], "wave_cell": [WW, WH], "wave_cells": 2, "clamp_cell": [CW, CH],
        "wave_lead_x": round(WW * 0.80, 1),                      # the leading rim's x at the middle of a wave cell
        "clamp_press_y": round(CH * 0.52 + 30, 1),               # the pressing edge's y at the middle of the clamp
    }
    with open(os.path.join(SRC, "seeker_press_spans.json"), "w", encoding="utf-8") as fh:
        json.dump(spans, fh, indent=2)
    sheet([("field", f), ("wave", w), ("clamp", c)], os.path.join(SRC, "seeker_press_sheet.png"))
    print("wrote the PRESS parts and", os.path.join(SRC, "seeker_press_spans.json"), spans)


if __name__ == "__main__":
    main()
