#!/usr/bin/env python3
"""Move art the game never asks for out of the runtime load path.

    python3 tools/asset-pipeline/retire_legacy.py            # dry run (default)
    python3 tools/asset-pipeline/retire_legacy.py --apply

`AssetLibrary` loads EVERY png under `assets/art` into VRAM at startup. The
legacy tree holds ~1,500 of them, including 83 backgrounds at 1920x1080 — that
alone is ~660 MB of RGBA, well past the 512 MB texture ceiling in art-bible
§8.8. Anything nothing references is pure cost, and any leftover painted asset
sharing a basename with a new one is also a style regression waiting to happen.

Files are MOVED to `assets/_legacy_art/` (a sibling of `assets/art`, outside the
scan root and outside the .csproj copy glob), never deleted. Reversible, and git
records it as a rename.

The keep-set is computed from the game's actual contract, not from a list:
manifest keys, animation keys, statics derived from strips, every alias target
in AssetLibrary.cs, and every statically-referenced key the audit can see.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from audit import aliases, referenced  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
ART = os.path.join(REPO, "assets", "art")
LEGACY = os.path.join(REPO, "assets", "_legacy_art")

SOURCES = ["body", "mind", "nature", "machine", "shadow", "spirit"]
ENEMIES = {"bonecrawler": "attack", "soul_leech": "attack", "wisp": "attack",
           "stone_sentinel": "slam", "shadeling": "attack", "rift_guardian": "attack"}
BOSSES = ["thorn_regent", "forge_colossus", "void_reaper", "crystal_lich", "lumen_angel", "spirit_matron"]


def keep_set() -> set[str]:
    keep: set[str] = set()

    for spec, field in (("manifest.json", "assets"), ("animations.json", "animations")):
        with open(os.path.join(HERE, spec), encoding="utf-8") as fh:
            keep |= {a["key"] for a in json.load(fh)[field]}

    # Statics derive_statics.py cuts out of the strips.
    for en in ENEMIES:
        keep |= {f"{en}_idle_01", f"{en}_idle_02", f"{en}_attack_01", f"{en}_attack_02",
                 f"{en}_portrait", f"{en}_silhouette", f"{en}_ground_shadow"}
    for b in BOSSES:
        keep |= {f"{b}_idle", f"{b}_attack", f"{b}_idle_1024", f"{b}_attack_1024",
                 f"{b}_portrait_square", f"{b}_portrait_bust", f"{b}_silhouette", f"{b}_shadow"}
    keep |= {"hunter_idle", "hunter_attack_01", "hunter_defeated"}

    # Alias targets, and every key the code names outright.
    keep |= set(aliases().values())
    static, _dynamic = referenced()
    keep |= set(static)
    keep |= set(aliases())

    return keep


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="actually move files (default is a dry run)")
    args = ap.parse_args()

    keep = keep_set()
    moves: list[tuple[str, str]] = []
    kept = 0
    for dirpath, _dirs, files in os.walk(ART):
        for fname in files:
            src = os.path.join(dirpath, fname)
            if not fname.lower().endswith(".png"):
                # Non-PNG (docs, .mgcb) is never loaded; leave it alone.
                continue
            if os.path.splitext(fname)[0] in keep:
                kept += 1
                continue
            moves.append((src, os.path.join(LEGACY, os.path.relpath(src, ART))))

    print(f"keep {kept} file(s), retire {len(moves)} file(s)")
    by_area: dict[str, int] = {}
    for src, _ in moves:
        area = os.path.relpath(src, ART).replace("\\", "/").split("/")[0]
        by_area[area] = by_area.get(area, 0) + 1
    for area, n in sorted(by_area.items(), key=lambda kv: -kv[1]):
        print(f"  {area:28s} {n}")

    if not args.apply:
        print("\n(dry run — pass --apply to move them)")
        return 0

    for src, dst in moves:
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.move(src, dst)

    # Drop directories left empty by the move so the tree stays readable.
    for dirpath, _dirs, _files in sorted(os.walk(ART), key=lambda t: -len(t[0])):
        if dirpath != ART and not os.listdir(dirpath):
            os.rmdir(dirpath)

    print(f"\nmoved {len(moves)} file(s) -> assets/_legacy_art/")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
