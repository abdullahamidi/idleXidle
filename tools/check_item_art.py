#!/usr/bin/env python3
"""Every item PNG under assets/art/ItemsLoot must be a 256 x 256 canvas with art in the middle of it.

    python3 tools/check_item_art.py

The item renderer (ForgeScreen.DrawItemIcon, via ItemArtMetrics) measures each glyph's CONTENT
BOUNDS and each rarity frame's INTERIOR from the pixels, then fits the content into the frame's
hole. That fit only means something if the files keep their contract: one canvas size (so a
frame's safe area is the same fraction of every file), no opaque corners and no fully opaque
canvas (a filled canvas has no content bounds to find and would draw as a square), and content
that is neither a speck nor bleeding to the edge.

The bbox is measured on the shorter side, alpha over 8 — the same floor ItemArtMetrics uses —
so a thin charm on its chain (item_charm_swift, 0.22 wide) is a sliver and not a failure, and a
round medallion that fills its canvas (0.97) is not a bleed. A LONGER side that reaches the edge
is reported as a WARN column (item_weapon_spear_greedy's tip sits one pixel from the top) rather
than a failure: it is a real spear, and the renderer clips nothing.

RARITY FRAMES (ItemsLoot/frames) have their own contract, because a frame is MEANT to reach its
canvas edge and sit flush in the slot: its CENTRE must be see-through (alpha under the floor) and
its band — measured from the centre outward along the middle row and column, the scan
ItemArtMetrics.FrameInterior runs — must be between 5 % and 25 % of the edge on every side. The
2026-08 family had opaque near-black centres, so every glyph sat on a black square and the frame
could only be tinted, never read; this is the check that would have refused it.

Pure python: tools/asset-pipeline/pixelpng.py is the dependency-free PNG reader every gate
shares, because `py` on this machine has no Pillow.
"""
import glob
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "tools", "asset-pipeline"))
import pixelpng  # noqa: E402

ITEMS = os.path.join(ROOT, "assets", "art", "ItemsLoot")
CANVAS = 256
ALPHA_FLOOR = 8
MIN_FRACTION = 0.18
MAX_FRACTION = 0.98
FRAME_BAND = (0.05, 0.25)


def frame_band(path):
    """(centre alpha, [left, top, right, bottom] band as a share of the edge) — ItemArtMetrics' scan."""
    img = pixelpng.read(path)
    w, h, px = img.w, img.h, img.px
    alpha = lambda x, y: px[(y * w + x) * 4 + 3]
    cx, cy = w // 2, h // 2
    centre = alpha(cx, cy)
    def run(xs, ys):
        n = 0
        for x, y in zip(xs, ys):
            if alpha(x, y) > ALPHA_FLOOR:
                return n
            n += 1
        return n
    left = cx - run(range(cx, -1, -1), [cy] * (cx + 1))
    right = w - 1 - (cx + run(range(cx, w), [cy] * (w - cx)))
    top = cy - run([cx] * (cy + 1), range(cy, -1, -1))
    bottom = h - 1 - (cy + run([cx] * (h - cy), range(cy, h)))
    return centre, [left / w, top / h, right / w, bottom / h]


def measure(path):
    img = pixelpng.read(path)
    w, h, px = img.w, img.h, img.px
    corners = [px[3], px[(w - 1) * 4 + 3], px[((h - 1) * w) * 4 + 3], px[((h - 1) * w + w - 1) * 4 + 3]]
    x0, y0, x1, y1 = w, h, -1, -1
    opaque = 0
    for y in range(h):
        row = y * w * 4
        for x in range(w):
            a = px[row + x * 4 + 3]
            if a == 255:
                opaque += 1
            if a > ALPHA_FLOOR:
                if x < x0: x0 = x
                if x > x1: x1 = x
                if y < y0: y0 = y
                if y > y1: y1 = y
    bbox = None if x1 < 0 else (x0, y0, x1 + 1, y1 + 1)
    return w, h, corners, opaque == w * h, bbox


def main():
    files = sorted(glob.glob(os.path.join(ITEMS, "**", "*.png"), recursive=True))
    if not files:
        print("no item art found under assets/art/ItemsLoot")
        return 1
    failures = []
    rows = []
    for path in files:
        rel = os.path.relpath(path, ROOT).replace("\\", "/")
        w, h, corners, full, bbox = measure(path)
        problems = []
        if (w, h) != (CANVAS, CANVAS):
            problems.append(f"canvas {w}x{h}")
        if "/frames/" in rel:
            centre, band = frame_band(path)
            if centre > ALPHA_FLOOR:
                problems.append(f"frame centre is painted (alpha {centre})")
            elif not all(FRAME_BAND[0] <= b <= FRAME_BAND[1] for b in band):
                problems.append("frame band " + "/".join(f"{b:.2f}" for b in band) + f" outside {FRAME_BAND}")
            if full:
                problems.append("fully opaque")
            rows.append((rel, w, h, min(band), max(band), "frm", ", ".join(problems)))
            if problems:
                failures.append((rel, problems))
            continue
        if any(a > ALPHA_FLOOR for a in corners):
            problems.append("opaque corner")
        if full:
            problems.append("fully opaque")
        short = long_ = 0.0
        warn = ""
        if bbox is None:
            problems.append("no content")
        else:
            fw = (bbox[2] - bbox[0]) / w
            fh = (bbox[3] - bbox[1]) / h
            short, long_ = min(fw, fh), max(fw, fh)
            if short < MIN_FRACTION:
                problems.append(f"content {short:.2f} < {MIN_FRACTION}")
            if short > MAX_FRACTION:
                problems.append(f"content {short:.2f} > {MAX_FRACTION}")
            if long_ > MAX_FRACTION:
                warn = "edge"
        rows.append((rel, w, h, short, long_, warn, ", ".join(problems)))
        if problems:
            failures.append((rel, problems))

    name_w = max(len(r[0]) for r in rows)
    print(f"{'file':<{name_w}}  size     short  long   warn  problems")
    for rel, w, h, short, long_, warn, problems in rows:
        print(f"{rel:<{name_w}}  {w}x{h}  {short:5.2f}  {long_:5.2f}  {warn:<4}  {problems}")
    print(f"{len(files)} files, {len(failures)} failing, {sum(1 for r in rows if r[5] == 'edge')} edge warnings")
    if failures:
        print("ITEM ART GATE FAILED:")
        for rel, problems in failures:
            print(f"  {rel}: {'; '.join(problems)}")
        return 1
    print("item art gate green")
    return 0


if __name__ == "__main__":
    sys.exit(main())
