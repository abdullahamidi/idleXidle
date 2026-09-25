#!/usr/bin/env python3
"""action_joins -- how far the champion's FIGURE jumps where one clip hands it to the next (ADR-011).

    python tools/asset-pipeline/action_joins.py <film.log> [--character seeker] [--all]

A film run with RH_PRESENT_TRACE=1 logs every champion frame it shows (champ-frame: clip, frame, draw box). This
maps each of those frames onto the screen with the renderer's OWN placement arithmetic (UiKit.ResolveFrame: the
strip's measured headroom and side margin, the scale from the box height capped at RasterCeiling, the strip's
lowest row on the box floor; an AUTHORED clip, one with a .clip.json, PLACED AS the idle since HARD HANDS) and, at
every clip change, reports how far the silhouette's feet line, head and centre moved between the last frame drawn
before the join and the first frame after it.

A join is continuous when the feet do not move and the centre moves no more than a pose change inside a clip
does. The ~40 px HARD HANDS -> SPRAY pop the SPRAY handoff pass saw was a join the old strike's crouched exit
pose made; this makes it a number.
"""
from __future__ import annotations

import argparse
import json
import os
import sys

from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ROSTER = os.path.join(REPO, "assets", "art", "Animations", "Roster")
RASTER_CEILING = 1.25
CROP_CEILING = 0.6
ALPHA = 8


class Strip:
    def __init__(self, path: str):
        im = Image.open(path).convert("RGBA")
        self.fw = im.height
        self.frames = max(1, im.width // self.fw)
        a = im.getchannel("A").point(lambda v: 255 if v > ALPHA else 0)
        whole = a.getbbox() or (0, 0, im.width, im.height)
        self.top = whole[1]                                   # the strip's least headroom (TopPadFraction)
        self.bottom_pad = self.fw - 1 - (whole[3] - 1)          # rows under its lowest opaque row (BottomPadFraction)
        least = self.fw // 2
        self.boxes = []
        for f in range(self.frames):
            bb = a.crop((f * self.fw, 0, f * self.fw + self.fw, self.fw)).getbbox()
            self.boxes.append(bb)
            if bb:
                least = min(least, bb[0], self.fw - bb[2])
        self.side = max(0, least - 1)                          # SidePadFraction, a pixel of slack
        self.clip_json = os.path.exists(path[:-4] + ".clip.json")


def strip_for(character: str, clip: str, cache: dict) -> Strip | None:
    key = (character, clip)
    if key not in cache:
        path = os.path.join(ROSTER, f"{character}_{clip}", f"char_{character}_{clip}_strip8_512.png")
        cache[key] = Strip(path) if os.path.exists(path) else None
    return cache[key]


def place(s: Strip, box: tuple[int, int, int, int], placed_as: Strip | None):
    """UiKit.ResolveFrame + PlaceFrame: (dest x, dest y, scale, own crop x, own crop y)."""
    bx, by, bw, bh = box
    crop_y = int(s.fw * min(s.top / s.fw, CROP_CEILING))
    metric = placed_as or s
    metric_crop = int(s.fw * min(metric.top / metric.fw, CROP_CEILING))
    sc = min(bh / max(1, s.fw - metric_crop), RASTER_CEILING)
    crop_x = int(s.fw * min(s.side / s.fw, 0.4))
    src_w, src_h = s.fw - 2 * crop_x, s.fw - crop_y
    drawn_h, w = max(1, int(src_h * sc)), max(1, int(src_w * sc))
    drop = round(metric.bottom_pad * sc)
    return bx + bw // 2 - w // 2, by + bh - drawn_h + drop, sc, crop_x, crop_y


def silhouette(s: Strip, frame: int, box, placed_as):
    bb = s.boxes[min(frame, s.frames - 1)]
    if not bb:
        return None
    dx, dy, sc, cx, cy = place(s, box, placed_as)
    left, top = dx + (bb[0] - cx) * sc, dy + (bb[1] - cy) * sc
    right, bottom = dx + (bb[2] - cx) * sc, dy + (bb[3] - cy) * sc
    return left, top, right, bottom


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("log")
    ap.add_argument("--character", default="seeker")
    ap.add_argument("--all", action="store_true", help="every frame change, not only clip changes")
    a = ap.parse_args()
    cache: dict = {}
    idle = strip_for(a.character, "idle", cache)
    shown, roots, home = [], [], None
    with open(a.log, encoding="utf-8", errors="replace") as fh:
        for n, line in enumerate(fh):
            p = line.rstrip("\n").split("\t")
            if len(p) >= 7 and p[0] == "present" and p[3] == "champ-frame":
                clip = p[4]
                frame = int(p[5].split("=")[1])
                box = tuple(int(v) for v in p[6].split("=")[1].split(","))
                shown.append((float(p[2]), clip, frame, box, n, float(p[1])))
                if clip == "idle" and home is None:
                    home = box
            elif len(p) >= 6 and p[0] == "present" and p[3] == "root":
                roots.append((n, float(p[1]), int(p[4].split("=")[1]), int(p[5].split("=")[1])))

    def last_box(n0, box, n1, clock1):
        """The box the outgoing frame was LAST drawn at: champ-frame logs a frame once, when it first shows, but a
        melee lunge moves the box while one frame holds; the root trace has every move. By LOG ORDER (the playhead
        restarts every wave), and not the move logged by the incoming frame's own draw (same wall clock)."""
        moved = [r for r in roots if n0 < r[0] < n1 and r[1] < clock1]
        if not moved or home is None:
            return box
        _, _, x, y = moved[-1]
        return (home[0] + x, home[1] + y, box[2], box[3])

    print(f"{'playhead':>9} {'from':>16} {'to':>16} {'feet':>6} {'head':>6} {'centre x':>9}  box x")
    worst = {}
    for (t0, c0, f0, b0, n0, _), (t1, c1, f1, b1, n1, clock1) in zip(shown, shown[1:]):
        if c0 == c1 and not a.all:
            continue
        s0, s1 = strip_for(a.character, c0, cache), strip_for(a.character, c1, cache)
        if not s0 or not s1:
            continue
        b0 = last_box(n0, b0, n1, clock1)
        g0 = silhouette(s0, f0, b0, idle if s0.clip_json and s0 is not idle else None)
        g1 = silhouette(s1, f1, b1, idle if s1.clip_json and s1 is not idle else None)
        if not g0 or not g1:
            continue
        feet, head = g1[3] - g0[3], g1[1] - g0[1]
        centre = (g1[0] + g1[2]) / 2 - (g0[0] + g0[2]) / 2
        print(f"{t1:9.0f} {c0 + '/' + str(f0):>16} {c1 + '/' + str(f1):>16} {feet:+6.1f} {head:+6.1f} {centre:+9.1f}  {b0[0]}->{b1[0]}")
        k = f"{c0}->{c1}"
        if k not in worst or abs(centre) + abs(feet) > abs(worst[k][2]) + abs(worst[k][0]):
            worst[k] = (feet, head, centre, t1)
    print("\nworst join per pair (feet, head, centre x px):")
    for k, (feet, head, centre, t) in sorted(worst.items()):
        print(f"  {k:24} feet {feet:+5.1f}  head {head:+6.1f}  centre {centre:+6.1f}   at {t:.0f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
