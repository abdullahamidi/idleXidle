#!/usr/bin/env python3
"""texture_budget — how much texture memory AssetLibrary holds once its PNGs are decoded.

    python tools/asset-pipeline/texture_budget.py [--top N] [--extra <dir>]

Every PNG under assets/art (skipping /native/, /preview/, /mask/, /medallion/) costs width x height x 4
once decoded as RGBA8, whatever its size on disk. technical-preferences.md sets a 512 MB ceiling.

Since 2026-09-16 AssetLibrary DEFERS the one-at-a-time families (AssetLibrary.IsDeferred: every path
under /Animations/ and /Enemies/enemies/, and each champion's VFX/<id>_* effects) to first use, and the host warms every champion's idle at
boot. So three figures are reported:

  ON DISK     everything, as if all were loaded (what the old eager loader held)
  AT BOOT     the eager set plus the ten champion idles
  IN A FIGHT  boot plus one champion's clips, one region's four creatures and its boss — the most the
              arena asks for at once; a session that visits more regions adds a family per region
              (nothing is evicted)

`--extra` adds a staging directory's PNGs to the on-disk total, to price a batch before it is filed.
Reads PNG headers only; no Pillow needed.
"""
from __future__ import annotations

import argparse
import glob
import os
import re
import struct
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SKIP = ("/native/", "/preview/", "/mask/", "/medallion/")
DEFERRED = ("/animations/", "/enemies/enemies/")   # AssetLibrary.IsDeferred, case-insensitive
CHAMPIONS = ("seeker", "anvil", "chorus", "metronome", "unbroken", "tower", "quiver", "thornwall", "oathbound", "magpie")
CEILING_MB = 512
MB = 2 ** 20


def dims(path: str) -> tuple[int, int]:
    with open(path, "rb") as f:
        head = f.read(24)
    return struct.unpack(">II", head[16:24])


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--top", type=int, default=12)
    ap.add_argument("--extra", default=None)
    a = ap.parse_args()
    groups: dict[str, int] = {}
    total = boot = 0
    per_key: dict[str, int] = {}
    for path in glob.glob(os.path.join(ROOT, "assets", "art", "**", "*.png"), recursive=True):
        rel = os.path.relpath(path, ROOT).replace(os.sep, "/")
        if any(s in "/" + rel for s in SKIP):
            continue
        w, h = dims(path)
        cost = w * h * 4
        total += cost
        key = os.path.splitext(os.path.basename(rel))[0]
        parts = rel.split("/")
        deferred = any(s in ("/" + rel).lower() for s in DEFERRED) or (
            parts[2] == "VFX" and any(parts[3].startswith(c + "_") for c in CHAMPIONS))
        if deferred:
            per_key[key] = max(per_key.get(key, 0), cost)
            if re.fullmatch(r"char_[a-z]+_idle_strip8_512", key):
                boot += cost   # Game1 warms every champion's idle at boot
        else:
            boot += cost
        group = "/".join(rel.split("/")[2:4])
        groups[group] = groups.get(group, 0) + cost
    for g, cost in sorted(groups.items(), key=lambda kv: -kv[1])[: a.top]:
        print(f"{cost / MB:8.1f} MB  {g}")

    champions = {k.split("_")[1] for k in per_key if k.startswith("char_")}
    champion = max((sum(c for k, c in per_key.items()
                        if (k.startswith(f"char_{cid}_") and not k.endswith("_idle_strip8_512")) or k.startswith(f"fx_{cid}_"))
                    for cid in champions), default=0)
    regions = {k.split("_")[0] for k in per_key if k.split("_")[0] in ("verdant", "cinder", "umbral", "marrow", "archive", "choir")}
    region = max((sum(c for k, c in per_key.items() if k.startswith(f"{r}_")) for r in regions), default=0)
    boss = max((sum(c for k, c in per_key.items() if k.startswith(f"{b}_"))
                for b in ("thorn_regent", "forge_colossus", "void_reaper", "spirit_matron", "crystal_lich", "lumen_angel")), default=0)
    print(f"ON DISK    {total / MB:6.0f} MB (every file decoded)")
    print(f"AT BOOT    {boot / MB:6.0f} MB resident (ceiling {CEILING_MB} MB)")
    print(f"IN A FIGHT {(boot + champion + region + boss) / MB:6.0f} MB  "
          f"(+ champion {champion / MB:.0f}, + region {region / MB:.0f}, + boss {boss / MB:.0f})")
    if a.extra:
        extra = sum(w * h * 4 for w, h in (dims(p) for p in glob.glob(os.path.join(a.extra, "*.png"))))
        print(f"+ {a.extra}: {extra / MB:.0f} MB on disk -> {(total + extra) / MB:.0f} MB")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
