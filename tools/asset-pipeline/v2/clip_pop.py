#!/usr/bin/env python3
"""clip_pop — how much a figure changes size when the renderer switches between its clips.

    python tools/asset-pipeline/v2/clip_pop.py <strip_dir> <key> [<key> ...]
    python tools/asset-pipeline/v2/clip_pop.py --shipped

UiKit.AnimSprite scales EVERY STRIP so that strip's own headroom-trimmed height fills the box
(ResolveCrop measures the strip's top pad; DrawScale = box / (frame - crop)). A clip whose motion
reaches higher than the idle's therefore draws the whole body smaller, and the figure jumps in size
when the clip changes. This prints, per clip, the height frame 0 is DRAWN at relative to the idle's
frame 0 — 1.00 is no jump. Frame 0 of every clip is the same pose (the animations start from the
reference), so any difference is the renderer's per-strip scale and nothing else.
"""
from __future__ import annotations

import glob
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rhart  # noqa: E402

ALPHA = 8


def top_pad(strip) -> int:
    """The strip's measured headroom in pixels: the first row with any alpha > 8 (UiKit.TopPadFraction)."""
    b = strip.getchannel("A").point(lambda v: 255 if v > ALPHA else 0).getbbox()
    return 0 if b is None else b[1]


def drawn_frame0(path: str) -> float:
    strip = rhart.load_rgba(path)
    fh = strip.height
    src_h = fh - top_pad(strip)
    f0 = rhart.frames_of(strip)[0]
    b = rhart.bbox(f0)
    body = 0 if b is None else b[3] - b[1]
    return body / max(1, src_h)          # the share of the box the frame-0 body fills


def report(paths_by_clip: dict[str, str], label: str) -> float:
    idle = paths_by_clip.get("idle")
    if not idle:
        return 1.0
    base = drawn_frame0(idle)
    worst = 1.0
    parts = []
    for clip in ("attack", "death"):
        p = paths_by_clip.get(clip)
        if not p:
            continue
        r = drawn_frame0(p) / base
        parts.append(f"{clip} {r:.2f}")
        if abs(r - 1) > abs(worst - 1):
            worst = r
    print(f"{label:<24} " + "  ".join(parts))
    return worst


def main(argv: list[str]) -> int:
    if argv and argv[0] == "--shipped":
        root = os.path.join(os.path.dirname(__file__), "..", "..", "..", "assets", "art", "Animations")
        keys = sorted({os.path.basename(d).rsplit("_", 1)[0] for d in glob.glob(os.path.join(root, "*", "*_idle"))})
        for key in keys:
            clips = {}
            for clip in ("idle", "attack", "death"):
                hits = glob.glob(os.path.join(root, "*", f"{key}_{clip}", f"{key}_{clip}_strip8_512.png"))
                if hits:
                    clips[clip] = hits[0]
            report(clips, key)
        return 0
    if len(argv) < 2:
        print(__doc__)
        return 2
    d = argv[0]
    for key in argv[1:]:
        report({c: os.path.join(d, f"{key}_{c}_strip8_512.png") for c in ("idle", "attack", "death")
                if os.path.exists(os.path.join(d, f"{key}_{c}_strip8_512.png"))}, key)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
