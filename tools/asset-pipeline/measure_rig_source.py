#!/usr/bin/env python3
"""Measure a rig SOURCE figure and report whether a rectangle decomposition can cut it.

Usage:
    python3 tools/asset-pipeline/measure_rig_source.py <key-or-path> [...]

The cut boxes in cut_rig_parts.py were hand-measured against one image. Every new
roster body needs the same numbers, and eyeballing them is how a box ends up sawing
through a shoulder — so they are measured here instead, from the alpha channel.

What it reports, and why each one decides whether the body is usable:

  ARMPIT GAPS   two columns of background running down between each arm and the torso.
                Without them no rectangle separates arm from body, which is the single
                thing that made the original source uncuttable.
  LEG GAP       a column of background between the legs, same reason.
  SHOULDER/HIP  the rows where width changes sharply — where arms leave the torso and
                where the legs begin.
  DRAPE         opaque pixels OUTSIDE the arm columns below the shoulder line. A cloak
                behind the arms shows up here and nowhere else.

Exit status is non-zero if any figure fails a structural check, so this can gate.
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from pixelpng import read  # noqa: E402
from scene_preview import find  # noqa: E402

ALPHA = 24          # a pixel counts as "figure" above this alpha
# CALIBRATED AGAINST THE SHIPPING RIG, not chosen. hunter_rig_base cuts correctly and its two armpit
# gaps measure 7px and 6px, so a floor of 8 rejected the one body known to work — which is the only
# way to find out that a gate is wrong before it starts rejecting good art. 6 keeps that body and still
# rejects the 3-4px anti-aliasing seams that a broad-shouldered figure leaves when its arms are
# actually touching its ribs.
MIN_GAP = 6
# Two is correct. Five allows an arm with internal detail — a bracer or a pauldron reads as its own
# seam, and the ENDURE body scored a symmetric four that way and was perfectly fine. Nine meant two
# sets of arms, which is the case this ceiling exists for.
MAX_GAPS = 5
# A single figure cut out on an empty background does not reach the canvas edge. Reaching BOTH edges
# means the art is a scene, or a figure generated too large for its canvas, or — the case that started
# this — a duplicated pose overlapping itself. It is the cheapest signal for all three.
EDGE_MARGIN = 6


def columns(img) -> list[int]:
    """Opaque pixel count per column."""
    return [sum(1 for y in range(img.h) if img.get(x, y)[3] > ALPHA) for x in range(img.w)]


def rows(img) -> list[int]:
    return [sum(1 for x in range(img.w) if img.get(x, y)[3] > ALPHA) for y in range(img.h)]


def runs(values: list[int], lo: int, hi: int, threshold: int = 0) -> list[tuple[int, int]]:
    """Maximal [start, end) runs inside [lo, hi) whose value is <= threshold."""
    out, start = [], None
    for i in range(lo, hi):
        if values[i] <= threshold:
            if start is None:
                start = i
        elif start is not None:
            out.append((start, i))
            start = None
    if start is not None:
        out.append((start, hi))
    return out


def band_columns(img, top: int, bottom: int) -> list[int]:
    """Opaque count per column, restricted to a horizontal band — this is what finds an
    armpit gap, which exists at chest height and nowhere else."""
    return [sum(1 for y in range(top, bottom) if img.get(x, y)[3] > ALPHA) for x in range(img.w)]


def measure(path: str) -> dict:
    img = read(path)
    col, row = columns(img), rows(img)
    xs = [x for x, c in enumerate(col) if c > 0]
    ys = [y for y, r in enumerate(row) if r > 0]
    if not xs or not ys:
        return {"path": path, "ok": False, "why": "no opaque pixels"}

    l, r, t, b = xs[0], xs[-1] + 1, ys[0], ys[-1] + 1
    h = b - t

    # A SLIDING band, not a fixed one, because where the arm/torso gap exists depends on the pose. A
    # narrow band at 0.34-0.46 of the figure's height finds it on an arms-down A-pose and finds nothing
    # on an arms-raised one; widening the band to cover both finds nothing on EITHER, because a wide
    # band also contains the rows where the arm joins the shoulder and no column there is empty. The
    # gap is real but it is thin in y, so the band has to go looking for it.
    #
    # A gap qualifies when there is opaque content on BOTH SIDES of it within the band — that is what
    # makes it a separation between an arm and a torso rather than the empty margin beyond the figure.
    band_h = max(6, int(h * 0.09))
    chest_top, chest_bot, chest, gaps = 0, 0, [], []
    for top in range(t + int(h * 0.26), t + int(h * 0.52) - band_h, 4):
        bot = top + band_h
        counts = band_columns(img, top, bot)
        found = [g for g in runs(counts, l, r)
                 if g[1] - g[0] >= MIN_GAP
                 and any(counts[x] > 0 for x in range(l, g[0]))
                 and any(counts[x] > 0 for x in range(g[1], r))]
        # Keep the band that separates the figure into the most pieces, and among equals the one whose
        # gaps are widest — that is the most comfortable place to put a cut boundary.
        score = (len(found), sum(g[1] - g[0] for g in found))
        if score > (len(gaps), sum(g[1] - g[0] for g in gaps)):
            chest_top, chest_bot, chest, gaps = top, bot, counts, found

    # The leg band: below the hip, above the ankles.
    leg_top, leg_bot = t + int(h * 0.66), t + int(h * 0.88)
    legs = band_columns(img, leg_top, leg_bot)
    # Interior again, and it has to be near the MIDDLE — the gap outside a bow-legged stance is not the
    # gap between the legs, and picking "nearest the centre" out of a list that includes edge runs
    # happily returned a run starting at the left bound.
    mid = (l + r) // 2
    leg_gaps = [g for g in runs(legs, l, r)
                if g[1] - g[0] >= MIN_GAP and g[0] > l and g[1] < r
                and abs((g[0] + g[1]) // 2 - mid) < (r - l) * 0.22]
    leg_gap = min(leg_gaps, key=lambda g: abs((g[0] + g[1]) // 2 - mid), default=None)

    return {
        "path": path, "ok": True,
        "bounds": (l, t, r, b), "size": (img.w, img.h),
        "chest_band": (chest_top, chest_bot),
        "armpit_gaps": gaps,
        "leg_band": (leg_top, leg_bot),
        "leg_gap": leg_gap,
        "widest_row": max(range(t, b), key=lambda y: row[y]),
    }


def report(m: dict) -> bool:
    name = os.path.basename(m["path"])
    if not m["ok"]:
        print(f"{name:26s} FAIL  {m['why']}")
        return False

    l, t, r, b = m["bounds"]
    gaps = m["armpit_gaps"]
    leg = m["leg_gap"]
    print(f"{name:26s} bounds x{l}..{r} y{t}..{b}  ({r - l}x{b - t})")
    print(f"{'':26s} chest band y{m['chest_band'][0]}..{m['chest_band'][1]}  "
          f"armpit gaps: {gaps if gaps else 'NONE'}")
    print(f"{'':26s} leg band   y{m['leg_band'][0]}..{m['leg_band'][1]}  "
          f"leg gap: {leg if leg else 'NONE'}")

    ok = True
    if len(gaps) < 2:
        print(f"{'':26s} ** UNCUTTABLE: needs TWO armpit gaps, found {len(gaps)}. The arms are "
              f"against the body, or something is draped behind them.")
        ok = False
    if l <= EDGE_MARGIN and r >= m["size"][0] - EDGE_MARGIN:
        print(f"{'':26s} ** REJECTED: the figure spans the whole canvas (x{l}..{r} of {m['size'][0]}). "
              f"A cut-out figure has margin; this is a scene, or a pose overlapping itself.")
        ok = False
    if len(gaps) > MAX_GAPS:
        # TOO MANY is a failure too, and this one is not obvious until it bites: a body scoring nine
        # gaps at chest height passed every other check and turned out to have TWO SETS OF ARMS. One
        # figure standing in an A-pose separates into exactly three pieces across its chest — arm,
        # torso, arm — so anything past a little slack means the silhouette is not one person.
        print(f"{'':26s} ** REJECTED: {len(gaps)} armpit gaps. One figure gives two; this many means "
              f"duplicated limbs or a silhouette broken into pieces.")
        ok = False
    if leg is None:
        print(f"{'':26s} ** UNCUTTABLE: no gap between the legs.")
        ok = False
    return ok


def main(args: list[str]) -> int:
    bad = 0
    for a in args:
        path = a if os.path.exists(a) else find(a)
        if path is None:
            print(f"{a}: not found", file=sys.stderr)
            bad += 1
            continue
        if not report(measure(path)):
            bad += 1
        print()
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
