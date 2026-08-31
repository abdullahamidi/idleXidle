#!/usr/bin/env python3
"""Diff the asset keys the game asks for against the PNGs actually on disk.

    python3 tools/asset-pipeline/audit.py           # human-readable report
    python3 tools/asset-pipeline/audit.py --json    # machine-readable
    python3 tools/asset-pipeline/audit.py --strict  # exit 1 if anything is missing

Three classes of problem, all of which have really occurred in this repo:

  MISSING     a literal key the C# asks for that has no PNG and no working alias.
              `Assets.Get` returns null and the draw site silently falls back to a
              flat rectangle, so these never surface as errors at runtime.
  BROKEN ALIAS an entry in AssetLibrary.Aliases pointing at a file that does not
              exist — strictly worse than no alias, because it looks wired up.
  COLLISION   the same basename at two or more runtime paths. Keys are flat
              basenames, so whichever the directory walk reaches last wins,
              non-deterministically.

Dynamic keys built by interpolation (`$"{en}_{act}_strip8_512"`) cannot be
resolved statically; they are listed separately as FYI rather than guessed at.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(REPO, "src")
ART = os.path.join(REPO, "assets", "art")
ASSET_LIBRARY = os.path.join(SRC, "IdleXIdle.Game", "AssetLibrary.cs")

# Directories AssetLibrary skips when building its filename -> texture map.
SKIP_DIRS = ("/native/", "/preview/", "/mask/", "/medallion/")

# Prefixes that mark a snake_case string literal as an asset key rather than an
# id, a config name, or a save-file field.
KEY_PREFIXES = (
    "icon_", "ui_", "item_", "bg_", "vfx_", "crea_", "boss_", "frame_",
    "mat_", "currency_", "hunter_", "affix_", "core_", "nav_", "source_",
)

LITERAL = re.compile(r'"([a-z][a-z0-9]*(?:_[a-z0-9]+)+)"')
ACCESSOR = re.compile(r'\b(?:Get|Has|GetFirst)\s*\(([^)]*)\)')
INTERPOLATED = re.compile(r'\$"([^"]*\{[^"]*)"')
ALIAS_ENTRY = re.compile(r'\["([^"]+)"\]\s*=\s*"([^"]+)"')


def disk_assets() -> tuple[dict[str, str], dict[str, list[str]]]:
    """Runtime-visible basenames -> path, plus every basename with >1 path."""
    found: dict[str, str] = {}
    all_paths: dict[str, list[str]] = {}
    for dirpath, _dirs, files in os.walk(ART):
        norm = dirpath.replace("\\", "/") + "/"
        if any(skip in norm for skip in SKIP_DIRS):
            continue
        for fname in files:
            if not fname.lower().endswith(".png"):
                continue
            key = os.path.splitext(fname)[0]
            rel = os.path.relpath(os.path.join(dirpath, fname), REPO).replace("\\", "/")
            all_paths.setdefault(key, []).append(rel)
            found.setdefault(key, rel)
    collisions = {k: v for k, v in all_paths.items() if len(v) > 1}
    return found, collisions


def aliases() -> dict[str, str]:
    if not os.path.exists(ASSET_LIBRARY):
        return {}
    with open(ASSET_LIBRARY, encoding="utf-8") as fh:
        text = fh.read()
    start = text.find("Aliases")
    if start < 0:
        return {}
    return dict(ALIAS_ENTRY.findall(text[start:]))


def referenced() -> tuple[dict[str, list[str]], dict[str, list[str]]]:
    """Static asset keys -> source sites, and interpolated patterns -> sites."""
    static: dict[str, list[str]] = {}
    dynamic: dict[str, list[str]] = {}
    # Only the client project draws anything; Core is pure simulation, so a
    # snake_case literal there is a gameplay tag, not an asset key.
    client = os.path.join(SRC, "IdleXIdle.Game")
    for dirpath, dirs, files in os.walk(client):
        dirs[:] = [d for d in dirs if d not in ("obj", "bin")]
        for fname in files:
            if not fname.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fname)
            rel = os.path.relpath(path, REPO).replace("\\", "/")
            with open(path, encoding="utf-8", errors="replace") as fh:
                lines = fh.readlines()
            for lineno, line in enumerate(lines, 1):
                site = f"{rel}:{lineno}"
                for arglist in ACCESSOR.findall(line):
                    for key in LITERAL.findall(arglist):
                        static.setdefault(key, []).append(site)
                for key in LITERAL.findall(line):
                    if key.startswith(KEY_PREFIXES):
                        static.setdefault(key, []).append(site)
                for pattern in INTERPOLATED.findall(line):
                    if any(p in pattern for p in KEY_PREFIXES) or "strip8" in pattern:
                        dynamic.setdefault(pattern, []).append(site)
    return static, dynamic


def audit() -> dict:
    on_disk, collisions = disk_assets()
    alias_map = aliases()
    static, dynamic = referenced()

    def resolves(key: str) -> bool:
        if key in on_disk:
            return True
        target = alias_map.get(key)
        return bool(target and target in on_disk)

    # Alias targets are reported under broken_aliases; listing them again as
    # missing keys would double-count the same defect.
    alias_targets = set(alias_map.values())
    # The ValidateRuntimeAssetPath deny-list is path substrings, not asset keys.
    forbidden_tokens = {
        "source_reference", "source_sheet", "contact_sheet",
        "runtime_assets_preview", "mood_board", "pitchboard",
    }

    missing = {
        k: sorted(set(v))
        for k, v in sorted(static.items())
        if not resolves(k) and k not in alias_targets and k not in forbidden_tokens
    }
    broken_aliases = {
        k: v for k, v in sorted(alias_map.items()) if v not in on_disk
    }
    return {
        "counts": {
            "on_disk": len(on_disk),
            "referenced": len(static),
            "missing": len(missing),
            "broken_aliases": len(broken_aliases),
            "collisions": len(collisions),
        },
        "missing": missing,
        "broken_aliases": broken_aliases,
        "collisions": {k: v for k, v in sorted(collisions.items())},
        "dynamic": {k: sorted(set(v)) for k, v in sorted(dynamic.items())},
    }


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--json", action="store_true", help="emit JSON instead of a report")
    ap.add_argument("--strict", action="store_true", help="exit 1 when problems are found")
    ap.add_argument("--quiet", action="store_true", help="only print problems")
    args = ap.parse_args()

    result = audit()
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        c = result["counts"]
        if not args.quiet:
            print(
                f"assets on disk: {c['on_disk']}   referenced keys: {c['referenced']}\n"
            )
        if result["missing"]:
            print(f"MISSING ({c['missing']}) — referenced in code, no PNG and no working alias:")
            for key, sites in result["missing"].items():
                print(f"  {key:36s} {sites[0]}" + (f" (+{len(sites)-1} more)" if len(sites) > 1 else ""))
            print()
        if result["broken_aliases"]:
            print(f"BROKEN ALIASES ({c['broken_aliases']}) — alias target does not exist:")
            for key, target in result["broken_aliases"].items():
                print(f"  {key:36s} -> {target}")
            print()
        if result["collisions"]:
            print(f"COLLISIONS ({c['collisions']}) — same basename, multiple runtime paths:")
            for key, paths in list(result["collisions"].items())[:20]:
                print(f"  {key}")
                for p in paths:
                    print(f"      {p}")
            if len(result["collisions"]) > 20:
                print(f"  ... and {len(result['collisions']) - 20} more")
            print()
        if not any((result["missing"], result["broken_aliases"], result["collisions"])):
            print("No problems found.")

    problems = c["missing"] + c["broken_aliases"]
    return 1 if (args.strict and problems) else 0


if __name__ == "__main__":
    raise SystemExit(main())
