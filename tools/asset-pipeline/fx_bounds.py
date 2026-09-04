#!/usr/bin/env python3
"""Measure the CONTENT BOX of an effect strip — the fraction of the frame the art fills.

Usage:
    python3 tools/asset-pipeline/fx_bounds.py [key-or-path ...]      (default: every fx_ strip)

WHY THIS EXISTS. VfxPlayer draws the WHOLE padded frame, so a strip whose art fills half its
frame draws at half the size its scale number claims. The VFX placement contract
(production/audit/systems-refactor/DESIGN-catalogues.md, "The VFX Placement Contract") sizes
an effect by its CONTENT rather than by its frame, and the number that makes that possible is
the one printed here: the transparent margin on each side as a fraction of the frame, unioned
across every frame of the strip — exactly what UiKit.Content(key) measures at runtime
(TopPadFraction / SidePadFraction / BottomPadFraction).

Left/Right are SYMMETRIC and are the LEAST margin of any frame, because that is
SidePadFraction's own rule (trimming by the least can never cut the one frame that reaches
furthest), and it carries the same one-pixel of slack the runtime keeps.

Output is one line per strip:

    fx_shield  frame 512  L 0.123 T 0.215 R 0.123 B 0.293   content 0.754w x 0.492h  centre 0.500,0.461

(that line is the RETIRED fx_shield hemisphere, kept because it shows every field doing
something; the strip on disk today is a ring filling its frame and reads L/T/R/B 0.000.)

Slow on purpose (a pure-Python PNG decode of a 4096x512 image), so this is a MEASURING tool,
not a gate. tools/check_all.sh does not run it.
"""

from __future__ import annotations

import glob
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pixelpng  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ART = os.path.join(ROOT, "assets", "art")

ALPHA_FLOOR = 8   # UiKit's own threshold: alpha <= 8 is "transparent" for bounds purposes


def find(key: str) -> str | None:
    if os.path.isfile(key):
        return key
    for path in glob.glob(os.path.join(ART, "**", "*.png"), recursive=True):
        if os.path.splitext(os.path.basename(path))[0] == key:
            return path
    return None


def content_box(path: str):
    """(frameSize, left, top, right, bottom) as fractions of the frame, unioned over frames."""
    img = pixelpng.read(path)
    px, w, h = img.px, img.w, img.h
    fw = h                                  # square frames, as every consumer assumes
    frames = max(1, w // fw)

    # TOP / BOTTOM: scan the whole texture, exactly as TopPadFraction / BottomPadFraction do.
    top = h
    for y in range(h):
        row = px[y * w * 4: (y + 1) * w * 4]
        if max(row[3::4]) > ALPHA_FLOOR:
            top = y
            break
    bottom = -1
    for y in range(h - 1, -1, -1):
        row = px[y * w * 4: (y + 1) * w * 4]
        if max(row[3::4]) > ALPHA_FLOOR:
            bottom = y
            break
    if bottom < 0:
        return fw, 0.0, 0.0, 0.0, 0.0, frames

    # SIDES: the least margin any frame has, symmetric, with SidePadFraction's pixel of slack.
    least = fw // 2
    for f in range(frames):
        left, right = fw, fw
        base = f * fw
        for x in range(fw):
            col = px[(base + x) * 4 + 3:: w * 4]
            if max(col) > ALPHA_FLOOR:
                left = x
                break
        for x in range(fw - 1, -1, -1):
            col = px[(base + x) * 4 + 3:: w * 4]
            if max(col) > ALPHA_FLOOR:
                right = fw - 1 - x
                break
        if left == fw:
            continue
        least = min(least, left, right)
    side = max(0, least - 1) / fw

    return fw, side, top / h, side, (h - 1 - bottom) / h, frames


def main() -> int:
    keys = sys.argv[1:]
    if not keys:
        keys = sorted({os.path.splitext(os.path.basename(p))[0]
                       for p in glob.glob(os.path.join(ART, "VFX", "**", "fx_*.png"), recursive=True)})
    bad = 0
    for key in keys:
        path = find(key)
        if path is None:
            print(f"{key}: NOT FOUND")
            bad = 1
            continue
        fw, l, t, r, b, frames = content_box(path)
        cw, ch = 1 - l - r, 1 - t - b
        print(f"{key}  frame {fw} x{frames}  L {l:.3f} T {t:.3f} R {r:.3f} B {b:.3f}"
              f"   content {cw:.3f}w x {ch:.3f}h  centre {l + cw / 2:.3f},{t + ch / 2:.3f}")
    return bad


if __name__ == "__main__":
    raise SystemExit(main())
