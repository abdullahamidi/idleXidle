#!/usr/bin/env python3
"""Collapse duplicate basenames down to one canonical file per key.

    python3 tools/asset-pipeline/dedupe_keys.py          # dry run
    python3 tools/asset-pipeline/dedupe_keys.py --apply

`AssetLibrary` keys on bare filename, so a key living at two or three paths is a
collision the loader has to break arbitrarily. Worse, after regeneration the
generators only overwrite ONE of those paths — the others keep serving the old
painted art, and whichever one the loader picks decides which style the player
sees.

Keeps the path the generators actually write to (so the surviving file is the
regenerated one), and moves the rest to assets/_legacy_art/.
"""

from __future__ import annotations

import argparse
import os
import shutil

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
ART = os.path.join(REPO, "assets", "art")
LEGACY = os.path.join(REPO, "assets", "_legacy_art")

# Lower rank wins. These mirror where derive_statics.py and generate.py write.
def rank(rel: str) -> tuple[int, int, str]:
    p = rel.replace("\\", "/")
    # Never prefer an exploded animation frame or a crop-variant folder — those
    # are byproducts, not the canonical asset.
    penalty = 0
    for token, cost in (
        ("/frames_native/", 90), ("/frames_512/", 90), ("/frames_", 90),
        ("/normalized/", 40), ("/shadows/", 30), ("/portraits/", 30),
        ("/silhouettes/", 30), ("/thumbnails/", 50),
    ):
        if token in p:
            penalty += cost
    # A `_1024` normalized boss frame legitimately lives under normalized/.
    if p.endswith("_1024.png") and "/normalized/" in p:
        penalty -= 40
    return (penalty, p.count("/"), p)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    by_key: dict[str, list[str]] = {}
    for dirpath, _dirs, files in os.walk(ART):
        for fname in files:
            if fname.lower().endswith(".png"):
                by_key.setdefault(os.path.splitext(fname)[0], []).append(os.path.join(dirpath, fname))

    moves: list[tuple[str, str]] = []
    for key, paths in sorted(by_key.items()):
        if len(paths) < 2:
            continue
        ordered = sorted(paths, key=lambda p: rank(os.path.relpath(p, ART)))
        keep = ordered[0]
        for loser in ordered[1:]:
            moves.append((loser, os.path.join(LEGACY, os.path.relpath(loser, ART))))
        if not args.apply:
            print(f"  {key}")
            print(f"      KEEP   {os.path.relpath(keep, REPO)}")
            for loser in ordered[1:]:
                print(f"      retire {os.path.relpath(loser, REPO)}")

    print(f"\n{len(moves)} duplicate file(s) to retire")
    if not args.apply:
        print("(dry run — pass --apply)")
        return 0

    for src, dst in moves:
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.move(src, dst)
    for dirpath, _dirs, _files in sorted(os.walk(ART), key=lambda t: -len(t[0])):
        if dirpath != ART and not os.listdir(dirpath):
            os.rmdir(dirpath)
    print(f"moved {len(moves)} file(s) -> assets/_legacy_art/")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
