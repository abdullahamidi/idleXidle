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


def fetch_all(urls: list[str], into: str, key: str = "") -> list[str]:
    """Download `urls` into `cache_dir` as f0..fN, reusing whatever is already there.

    The cache is keyed by OUTPUT PATH, which is right for a retry after a network stall and wrong for
    a re-roll: a second take of the same clip files to the same path, so the stale frames of the take
    being replaced would be reused and the re-roll would change nothing. `key` (the animation id)
    is stamped in the directory; a different one empties it first.
    """
    os.makedirs(into, exist_ok=True)
    if key:
        stamp = os.path.join(into, "SOURCE")
        was = open(stamp).read().strip() if os.path.exists(stamp) else ""
        if was != key:
            for f in os.listdir(into):
                os.remove(os.path.join(into, f))
            with open(stamp, "w") as fh:
                fh.write(key)
    paths = []
    for i, u in enumerate(urls):
        p = os.path.join(into, f"f{i}.png")
        if not os.path.exists(p) or os.path.getsize(p) < 100:
            rhart.fetch(u, p)
        paths.append(p)
    return paths




REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
STAGING = os.path.join(REPO, "tools", "asset-pipeline", ".staging", "frames")


def cache_dir(out: str) -> str:
    """Where the downloaded frames are cached — OUTSIDE the shipped asset tree.

    This used to be `<out>.frames`, i.e. a directory sitting next to the strip inside `assets/art`.
    The game's csproj copies `assets/art/**/*.png` to the output directory and AssetLibrary keys every
    PNG it finds by BASENAME, so 808 cache frames — all of them named f0.png .. f7.png — were being
    copied and loaded at every boot, eight hundred textures fighting over eight keys. Gitignoring them
    hid it from CI and from other machines and did nothing for the machine doing the work.

    A build INPUT does not belong in the shipped asset tree. The cache now lives under
    tools/asset-pipeline/.staging/frames/<strip name>/, keyed by the output's basename.
    """
    return os.path.join(STAGING, os.path.basename(out))


def exists(url: str) -> bool:
    """True if the URL serves a PNG. One try, short timeout — this is a probe, not a fetch."""
    import urllib.error
    import urllib.request
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "rh-art-pipeline/2"}, method="HEAD")
        return urllib.request.urlopen(req, timeout=15).status < 400
    except Exception:  # noqa: BLE001 — 404, 403 and a stalled socket all mean "not there"
        return False


def char_range(a) -> range:
    """Which frame indices to pull for a character animation.

    animate_character stores 8 frames when it was called with `keep_first_frame=false` and NINE when
    it was not: index 0 is then the character's own rotation image, reused as the starting pose, and
    1..8 are the generated ones. Hard-coding range(8) silently mixed the two — for a nine-frame group
    it kept the static reference and threw away the LAST generated frame, which is the frame every
    action prompt spends its final clause describing ("holds that point steady", "the arrow still
    visible"). So probe for index 8 and take 1..8 when it is there.
    """
    if getattr(a, "keep_ref", False):
        return range(8)
    ninth = f"{CHAR_BASE}/{a.char}/animations/{a.anim}/{a.dir}/8.png"
    return range(1, 9) if exists(ninth) else range(8)


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(prog="clip")
    sub = ap.add_subparsers(dest="cmd", required=True)

    c = sub.add_parser("char")
    c.add_argument("--char", required=True); c.add_argument("--anim", required=True)
    c.add_argument("--dir", required=True); c.add_argument("--out", required=True)
    c.add_argument("--fit", type=float, default=0.90); c.add_argument("--frame", type=int, default=512)
    c.add_argument("--loose", action="store_true"); c.add_argument("--flip", action="store_true")
    c.add_argument("--keep-ref", dest="keep_ref", action="store_true",
                   help="take frames 0..7 even for a nine-frame group (keeps the rotation reference)")
    c.add_argument("--thrown", action="store_true",
                   help="a projectile clip: allow the thrown object to be a separate blob")
    c.add_argument("--fallen", action="store_true",
                   help="a death clip: the body ends fully fallen, so frames may lie as flat as 22 %% of the standing one")

    j = sub.add_parser("job")
    j.add_argument("--job", required=True); j.add_argument("--out", required=True)
    j.add_argument("--fit", type=float, default=0.90); j.add_argument("--frame", type=int, default=512)
    j.add_argument("--effect", action="store_true"); j.add_argument("--loose", action="store_true")
    j.add_argument("--thrown", action="store_true",
                   help="sparks, smoke or a shed part are a second blob on purpose: skip the stray-blob check")
    j.add_argument("--fallen", action="store_true",
                   help="a death clip: the body ends fully fallen, so frames may lie as flat as 22 %% of the standing one")
    j.add_argument("--flip", action="store_true")

    s = sub.add_parser("still")
    s.add_argument("--url", required=True); s.add_argument("--out", required=True)
    s.add_argument("--fit", type=float, default=0.90); s.add_argument("--frame", type=int, default=512)
    s.add_argument("--flip", action="store_true")

    a = ap.parse_args(argv)
    cache = cache_dir(a.out)

    if a.cmd == "char":
        urls = [f"{CHAR_BASE}/{a.char}/animations/{a.anim}/{a.dir}/{i}.png" for i in char_range(a)]
        frames = fetch_all(urls, cache, a.anim)
        rhart.build_strip(frames, a.out, a.frame, a.fit, 0.035, a.flip, 8, False, False)
    elif a.cmd == "job":
        urls = [f"{IMG_BASE}/{a.job}/download?index={i}" for i in range(1, 9)]
        frames = fetch_all(urls, cache, a.job)
        rhart.build_strip(frames, a.out, a.frame, a.fit, 0.035, a.flip, 8, False, a.effect)
    else:
        frames = fetch_all([a.url], cache)
        rhart.build_strip(frames, a.out, a.frame, a.fit, 0.035, a.flip, 1, False, False)
        print(f"WROTE {a.out}")
        return 0

    probs = rhart.gate(a.out, effect=getattr(a, "effect", False), loose=a.loose,
                       thrown=getattr(a, "thrown", False), fallen=getattr(a, "fallen", False))
    print(("PASS " if not probs else "FAIL ") + a.out)
    for pr in probs:
        print("   - " + pr)
    return 1 if probs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
