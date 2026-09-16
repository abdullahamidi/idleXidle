#!/usr/bin/env python3
"""matrixclips — assemble the region x archetype enemy clips recorded in spec.json's
`enemy_matrix.done`, gate them, and lay them out on one review sheet.

    python tools/asset-pipeline/v2/matrixclips.py [<key> ...] [--sheet <out.png>]

With no keys, every `done` entry is assembled. Each key lands in
tools/asset-pipeline/.staging/v2/matrix/<key>_<clip>_strip8_512.png (gitignored) and is filed by
`file_assets.py --apply --only matrix`, which copies whatever the staging dir holds for the keys the
matrix names (never rmtree's the enemy tree wholesale, so a half-finished region ships nothing new
and loses nothing old).

`done` shapes (see spec.json enemy_matrix.$comment):
    character route: {"character_id": ..., "idle": <animId>, "attack": <animId>, "death": <animId>}
    image route:     {"image_job": ..., "idle_job": ..., "attack_job": ..., "death_job": ...}

The gate is rhart.gate through clip.py — loose for attack, loose+fallen for death (a collapse ends
at half height on purpose), tight for idle — and its verdict is echoed per clip. The sheet is what a human looks at;
the gate cannot tell whether a clip READS (arena-art-contract.md §3.4).
"""
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys

# Git Bash on Windows hands us a cp1254 console; the child processes print UTF-8.
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SPEC = os.path.join(HERE, "spec.json")
STAGING = os.path.join(REPO, "tools", "asset-pipeline", ".staging", "v2", "matrix")
DIRECTION = "south-west"   # enemies face LEFT natively (arena-art-contract.md §1)
CLIPS = ("idle", "attack", "death")


def assemble(key: str, done: dict, only: tuple[str, ...] = CLIPS) -> list[tuple[str, str, bool, str]]:
    os.makedirs(STAGING, exist_ok=True)
    rows = []
    for clip in only:
        out = os.path.join(STAGING, f"{key}_{clip}_strip8_512.png")
        if "character_id" in done:
            anim = done.get(clip)
            if not anim:
                rows.append((key, clip, False, "no animation id recorded"))
                continue
            cmd = [sys.executable, os.path.join(HERE, "clip.py"), "char",
                   "--char", done["character_id"], "--anim", anim, "--dir", DIRECTION, "--out", out]
        else:
            job = done.get(f"{clip}_job")
            if not job:
                rows.append((key, clip, False, "no job id recorded"))
                continue
            cmd = [sys.executable, os.path.join(HERE, "clip.py"), "job", "--job", job, "--out", out]
        if clip != "idle":
            cmd.append("--loose")
        if clip == "death":
            cmd.append("--fallen")   # a body that ends on the ground is half its standing height
        if done.get("thrown"):
            cmd.append("--thrown")   # sparks / smoke / shed shadow are a second blob on purpose (rhart.gate `thrown`)
        p = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
        tail = (p.stdout or p.stderr).strip().splitlines()
        rows.append((key, clip, p.returncode == 0, tail[-1] if tail else "(no output)"))
    return rows


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("keys", nargs="*")
    ap.add_argument("--sheet", default=None, help="review sheet path (default build/shots/matrix_sheet.png)")
    ap.add_argument("--clips", default=",".join(CLIPS))
    a = ap.parse_args(argv)
    spec = json.load(open(SPEC, encoding="utf-8"))
    matrix = spec["enemy_matrix"]
    keys = a.keys or list(matrix["done"].keys())
    clips = tuple(c for c in a.clips.split(",") if c)
    strips = []
    failed = 0
    for key in keys:
        done = matrix["done"].get(key)
        if not done:
            print(f"{key}: nothing recorded in enemy_matrix.done")
            failed += 1
            continue
        for k, clip, ok, line in assemble(key, done, clips):
            print(f"{'PASS' if ok else 'FAIL'} {k}_{clip}: {line}")
            failed += 0 if ok else 1
            path = os.path.join(STAGING, f"{k}_{clip}_strip8_512.png")
            if os.path.exists(path):
                strips.append(path)
    if strips:
        sheet = a.sheet or os.path.join(REPO, "build", "shots", "matrix_sheet.png")
        os.makedirs(os.path.dirname(sheet), exist_ok=True)
        subprocess.run([sys.executable, os.path.join(HERE, "rhart.py"), "sheet", sheet] + strips, check=False)
        print(f"sheet -> {os.path.relpath(sheet, REPO)}")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
