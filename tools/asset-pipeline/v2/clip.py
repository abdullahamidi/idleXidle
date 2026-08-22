#!/usr/bin/env python3
"""clip — fetch one generated clip, assemble the runtime strip, run the gate, print the verdict.

    # a character animation (frames 0..7 at .../animations/<anim>/<dir>/<i>.png)
    python tools/asset-pipeline/v2/clip.py char --char <characterId> --anim <animId> --dir south-east \
        --out <dir>/<key>.png [--loose] [--fit 0.90] [--frame 512]

    # an animate_image job (frames 1..8 at .../images/<job>/download?index=<i>; 0 is the input)
    python tools/asset-pipeline/v2/clip.py job --job <jobId> --out <dir>/<key>.png [--effect] [--loose]

    # a single still (a rotation URL or any PNG URL) into a 512 square, bottom-anchored
    python tools/asset-pipeline/v2/clip.py still --url <url> --out <path.png> [--fit 0.90]

Exit status is the gate's: 0 pass, 1 fail (the strip is still written so a human can look).
Frames are cached beside the output (`<out>.frames/`) so a re-run after a network stall does not
pay for the fetches again.
"""

from __future__ import annotations

import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rhart  # noqa: E402

CHAR_BASE = "https://backblaze.pixellab.ai/file/pixellab-characters/a5e233f5-b48a-4bf3-8ed5-ea649a71f38d"
IMG_BASE = "https://api.pixellab.ai/mcp/images"


def fetch_all(urls: list[str], cache_dir: str) -> list[str]:
    os.makedirs(cache_dir, exist_ok=True)
    paths = []
    for i, u in enumerate(urls):
        p = os.path.join(cache_dir, f"f{i}.png")
        if not os.path.exists(p) or os.path.getsize(p) < 100:
            rhart.fetch(u, p)
        paths.append(p)
    return paths


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(prog="clip")
    sub = ap.add_subparsers(dest="cmd", required=True)

    c = sub.add_parser("char")
    c.add_argument("--char", required=True); c.add_argument("--anim", required=True)
    c.add_argument("--dir", required=True); c.add_argument("--out", required=True)
    c.add_argument("--fit", type=float, default=0.90); c.add_argument("--frame", type=int, default=512)
    c.add_argument("--loose", action="store_true"); c.add_argument("--flip", action="store_true")

    j = sub.add_parser("job")
    j.add_argument("--job", required=True); j.add_argument("--out", required=True)
    j.add_argument("--fit", type=float, default=0.90); j.add_argument("--frame", type=int, default=512)
    j.add_argument("--effect", action="store_true"); j.add_argument("--loose", action="store_true")
    j.add_argument("--flip", action="store_true")

    s = sub.add_parser("still")
    s.add_argument("--url", required=True); s.add_argument("--out", required=True)
    s.add_argument("--fit", type=float, default=0.90); s.add_argument("--frame", type=int, default=512)
    s.add_argument("--flip", action="store_true")

    a = ap.parse_args(argv)
    cache = a.out + ".frames"

    if a.cmd == "char":
        urls = [f"{CHAR_BASE}/{a.char}/animations/{a.anim}/{a.dir}/{i}.png" for i in range(8)]
        frames = fetch_all(urls, cache)
        rhart.build_strip(frames, a.out, a.frame, a.fit, 0.035, a.flip, 8, False, False)
    elif a.cmd == "job":
        urls = [f"{IMG_BASE}/{a.job}/download?index={i}" for i in range(1, 9)]
        frames = fetch_all(urls, cache)
        rhart.build_strip(frames, a.out, a.frame, a.fit, 0.035, a.flip, 8, False, a.effect)
    else:
        frames = fetch_all([a.url], cache)
        rhart.build_strip(frames, a.out, a.frame, a.fit, 0.035, a.flip, 1, False, False)
        print(f"WROTE {a.out}")
        return 0

    probs = rhart.gate(a.out, effect=getattr(a, "effect", False), loose=a.loose)
    print(("PASS " if not probs else "FAIL ") + a.out)
    for pr in probs:
        print("   - " + pr)
    return 1 if probs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
