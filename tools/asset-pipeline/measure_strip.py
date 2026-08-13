#!/usr/bin/env python3
"""Measure an animation strip and say whether it is fit to play.

Usage:
    python3 tools/asset-pipeline/measure_strip.py <key-or-path> [...]

Twenty character clips is past the number anyone checks by eye, and eyeing them is
exactly how a waist-up bust reached the battle screen and stayed there for a whole
session. Every check below exists because something got past the previous version of
this file or past a human:

  FRAMING     the figure must reach its own feet. A clip cropped at the belt draws a
              torso standing on the floor. Measured the way the base sprites are: the
              width of the bottom eighth of the content against the widest row — a body
              narrows at the legs, a bust ends wide.
  SCALE DRIFT the figure must be the same size in every frame. The animator returns
              frames at slightly different zooms, and played back that reads as the
              character pulsing rather than breathing.
  BASELINE    the feet must stay put. UiKit.AnimSprite grounds a strip by ONE offset
              measured across the whole clip, so a figure whose feet wander vertically
              slides instead of standing.
  STRAYS      no disconnected blobs. The animator hallucinates spare parts — one idle
              clip carried a black spike floating above the head on two of eight frames.
  EMPTY       no blank frames, which is what a failed generation looks like.

Exit status is non-zero if any strip fails, so this can gate a re-roll loop.
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from pixelpng import read  # noqa: E402
from scene_preview import find  # noqa: E402

ALPHA = 8

# RELATIVE TO THE CHARACTER'S OWN BASE SPRITE, not an absolute.
#
# An absolute threshold cannot work here and the first version proved it: a body narrows
# at the legs and a bust ends wide, so 0.55 separated them beautifully — and then failed
# THE OATHBOUND and THE THORNWALL, who wear robes whose hem is genuinely as wide as their
# shoulders. Their clips were fine; the ruler was wrong. The question is not "is this
# shape bottom-heavy" but "is this clip more bottom-heavy than the design it came from",
# which is answerable because the base sprite is right there.
MAX_FOOT_EXCESS = 0.20
# The animator's own frame-to-frame wobble is a few percent; 12% is the point where it
# stops reading as breathing and starts reading as a zoom.
MAX_SCALE_DRIFT = 0.12
# In frame heights. An idle bob is a handful of pixels; a quarter of the figure is a slide.
MAX_BASELINE_DRIFT = 0.06
MIN_FILL = 0.20         # a frame with almost nothing in it is a failed generation


def frames_of(img):
    fw = img.h
    for i in range(max(1, img.w // fw)):
        yield i, fw, i * fw


def bounds(img, ox: int, fw: int):
    """Content box of one frame, in frame-local coordinates."""
    rows, cols = {}, {}
    for y in range(img.h):
        n = 0
        for x in range(fw):
            if img.get(ox + x, y)[3] > ALPHA:
                n += 1
                cols[x] = cols.get(x, 0) + 1
        if n:
            rows[y] = n
    if not rows:
        return None
    ys, xs = sorted(rows), sorted(cols)
    return xs[0], ys[0], xs[-1] + 1, ys[-1] + 1, rows


def components(img, ox: int, fw: int) -> int:
    """How many disconnected opaque blobs the frame has (4-connected, iterative)."""
    seen = [[False] * fw for _ in range(img.h)]
    found = 0
    for sy in range(img.h):
        for sx in range(fw):
            if seen[sy][sx] or img.get(ox + sx, sy)[3] <= ALPHA:
                continue
            found += 1
            stack = [(sx, sy)]
            seen[sy][sx] = True
            size = 0
            while stack:
                x, y = stack.pop()
                size += 1
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= nx < fw and 0 <= ny < img.h and not seen[ny][nx] \
                            and img.get(ox + nx, ny)[3] > ALPHA:
                        seen[ny][nx] = True
                        stack.append((nx, ny))
            # Ignore specks: a handful of anti-aliased pixels is not a hallucinated limb.
            if size < 24:
                found -= 1
    return found


def base_foot_ratio(path: str) -> float | None:
    """The same measurement taken on the character's own base sprite, as the reference."""
    name = os.path.basename(path)
    if not name.startswith("char_"):
        return None
    cid = name[len("char_"):].split("_")[0]
    base = find(f"char_{cid}_base")
    if base is None:
        return None
    img = read(base)
    rows = {}
    for y in range(img.h):
        n = sum(1 for x in range(img.w) if img.get(x, y)[3] > ALPHA)
        if n:
            rows[y] = n
    if not rows:
        return None
    ys = sorted(rows)
    t, b = ys[0], ys[-1] + 1
    h = b - t
    widest = max(rows.values())
    foot = [rows[y] for y in range(max(t, b - max(1, h // 8)), b) if y in rows]
    return sum(foot) / len(foot) / widest if foot and widest else None


def measure(path: str) -> dict:
    img = read(path)
    per = []
    for i, fw, ox in frames_of(img):
        bb = bounds(img, ox, fw)
        if bb is None:
            per.append({"i": i, "empty": True})
            continue
        l, t, r, b, rows = bb
        h = b - t
        widest = max(rows.values())
        foot = [rows[y] for y in range(max(t, b - max(1, h // 8)), b) if y in rows]
        per.append({
            "i": i, "empty": False, "top": t, "bottom": b, "height": h,
            "fill": h / fw,
            "foot": (sum(foot) / len(foot) / widest) if foot and widest else 1.0,
            "blobs": components(img, ox, fw),
        })
    return {"path": path, "fw": img.h, "frames": per, "base_foot": base_foot_ratio(path)}


def report(m: dict) -> bool:
    name = os.path.basename(m["path"])
    per = m["frames"]
    fw = m["fw"]
    live = [f for f in per if not f["empty"]]
    ok = True

    if not live:
        print(f"{name:38s} FAIL  every frame is empty")
        return False

    heights = [f["height"] for f in live]
    bottoms = [f["bottom"] for f in live]
    foots = [f["foot"] for f in live]
    blobs = max(f["blobs"] for f in live)
    drift = (max(heights) - min(heights)) / max(1, max(heights))
    base = (max(bottoms) - min(bottoms)) / fw

    print(f"{name:38s} {len(per)}f  fill {min(f['fill'] for f in live):.2f}-{max(f['fill'] for f in live):.2f}"
          f"  foot {min(foots):.2f}-{max(foots):.2f}  scale-drift {drift:.1%}"
          f"  baseline {base:.1%}  blobs {blobs}"
          + (f"  [base foot {m['base_foot']:.2f}]" if m.get("base_foot") is not None else ""))

    empties = [f["i"] for f in per if f["empty"]]
    if empties:
        print(f"{'':38s} ** FAIL: frames {empties} are empty.")
        ok = False
    if min(f["fill"] for f in live) < MIN_FILL:
        print(f"{'':38s} ** FAIL: a frame is nearly blank.")
        ok = False
    ref = m.get("base_foot")
    if ref is not None and max(foots) > ref + MAX_FOOT_EXCESS:
        print(f"{'':38s} ** FAIL: cropped at the body — foot {max(foots):.2f} against the base "
              f"sprite's {ref:.2f}. The clip does not reach as far down as the design does.")
        ok = False
    if drift > MAX_SCALE_DRIFT:
        print(f"{'':38s} ** FAIL: the figure changes size by {drift:.1%} across the clip "
              f"(max {MAX_SCALE_DRIFT:.0%}). It will read as pulsing, not breathing.")
        ok = False
    if base > MAX_BASELINE_DRIFT:
        print(f"{'':38s} ** FAIL: the feet move {base:.1%} of a frame. The strip is grounded by one "
              f"offset, so this slides.")
        ok = False
    if blobs > 1:
        print(f"{'':38s} ** FAIL: {blobs} disconnected blobs — a hallucinated part is floating "
              f"beside the figure.")
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
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
