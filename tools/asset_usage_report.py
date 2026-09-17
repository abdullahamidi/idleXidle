#!/usr/bin/env python3
"""Which textures on disk did the capture battery never ask for?

    python tools/asset_usage_report.py build/asset_usage/requested.txt [--tsv out.tsv]

Reads the key trace AssetLibrary writes under RH_ASSET_TRACE (tools/asset-pipeline/asset_usage.sh) and
compares it with every PNG under assets/art the loader would load, keyed the way AssetLibrary keys them
(flat basename, case-insensitive; native/ preview/ mask/ medallion/ are never loaded). Prints the files
nobody asked for, heaviest first, with whether they load at boot (EAGER — they cost texture memory in
every session) or on first use (DEFERRED — they cost only download size until asked for).

A row here is a CANDIDATE. Before deleting one, trace its family in the code: the battery cannot pose
every boss, clip and beat. Files a tool outside the game reads (tools/marketing, tests) are flagged.
"""
import glob
import os
import re
import struct
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "assets", "art")
SKIPPED = ("/native/", "/preview/", "/mask/", "/medallion/")


def champion_ids():
    src = open(os.path.join(ROOT, "src", "IdleXIdle.Core", "Characters", "CharacterRoster.cs"), encoding="utf-8").read()
    return re.findall(r'Id = "([a-z_]+)", Name =', src)


CHAMPS = champion_ids()


def deferred(norm: str) -> bool:
    if "/Animations/" in norm or "/Enemies/enemies/" in norm:
        return True
    i = norm.find("/VFX/")
    return i >= 0 and any(norm[i + 5:].startswith(c + "_") for c in CHAMPS)


def rgba_bytes(path: str) -> int:
    with open(path, "rb") as f:
        head = f.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return 0
    w, h = struct.unpack(">II", head[16:24])
    return w * h * 4


def outside_consumers() -> str:
    parts = []
    for pattern in ("tools/marketing/**/*.py", "tests/**/*.cs", "tools/*.py"):
        for p in glob.glob(os.path.join(ROOT, pattern), recursive=True):
            if os.path.abspath(p) == os.path.abspath(__file__):
                continue
            try:
                parts.append(open(p, encoding="utf-8", errors="ignore").read())
            except OSError:
                pass
    return "\n".join(parts)


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    trace = sys.argv[1]
    lines = [line.strip().lower() for line in open(trace, encoding="utf-8") if line.strip()] if os.path.exists(trace) else []
    # Marks written by AssetLibrary.Trace: none = requested (Get/GetFirst/WhiteMask/alias target),
    # "?" = existence check (Has), "~" = loaded by a Warm prefix. A warmed-only file costs memory and
    # was never drawn, so it stays a candidate (flagged WARMED).
    drawn = {l for l in lines if l[0] not in "?~"}
    queried = {l[1:] for l in lines if l.startswith("?")}
    warmed = {l[1:] for l in lines if l.startswith("~")}
    others = outside_consumers()
    rows = []
    only_queried = []
    for path in glob.glob(os.path.join(ART, "**", "*.png"), recursive=True):
        norm = path.replace("\\", "/")
        if ".png.frames/" in norm or any(s in norm for s in SKIPPED):
            continue
        key = os.path.splitext(os.path.basename(path))[0]
        if key.lower() in drawn:
            continue
        if key.lower() in queried:
            only_queried.append(os.path.relpath(path, ROOT).replace("\\", "/"))
            continue
        rel = os.path.relpath(path, ROOT).replace("\\", "/")
        tools = "tools/tests" if re.search(r"(?<![A-Za-z0-9_])" + re.escape(key) + r"(?![A-Za-z0-9_])", others) else ""
        if key.lower() in warmed:
            tools = (tools + " WARMED").strip()
        rows.append((rel, key, "DEFERRED" if deferred(norm) else "EAGER", os.path.getsize(path), rgba_bytes(path), tools))
    rows.sort(key=lambda r: (r[2] != "EAGER", -r[4]))
    eager = [r for r in rows if r[2] == "EAGER"]
    print(f"trace: {len(drawn)} keys requested, {len(queried)} existence checks")
    print(f"never asked: {len(rows)} files, {sum(r[3] for r in rows) / 1e6:.1f} MB on disk; "
          f"{len(eager)} load at boot = {sum(r[4] for r in eager) / 1e6:.1f} MB of texture memory")
    for rel, key, cls, size, vram, tools in rows:
        print(f"{cls:8s} {vram / 1e6:7.1f} MB  {size / 1e3:7.0f} KB  {tools:11s} {rel}")
    print()
    print(f"only existence-checked (Has), never requested: {len(only_queried)} files (kept: the code branches on them)")
    for rel in sorted(only_queried):
        print(f"  {rel}")
    if "--tsv" in sys.argv:
        with open(sys.argv[sys.argv.index("--tsv") + 1], "w", encoding="utf-8") as f:
            for r in rows:
                f.write("\t".join(map(str, r)) + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
