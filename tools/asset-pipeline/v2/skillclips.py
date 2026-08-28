#!/usr/bin/env python3
"""skillclips — assemble a champion's per-Form skill clips and file them where the game looks.

    python tools/asset-pipeline/v2/skillclips.py <charId> <clip>=<animId> [<clip>=<animId> ...]

Each pair becomes `assets/art/Animations/Roster/<char>_<clip>/char_<char>_<clip>_strip8_512.png`,
which is `Character.StripKey(clip)` — the key the renderer asks for first, before it falls back to
the generic attack/cast pair (see Character.StripKeys).

WHY THIS EXISTS RATHER THAN A SHELL LOOP. Fifty clips arrive as ten batches of five, each needing the
same four steps in the same order (fetch → strip → gate → file) and each capable of failing at any of
them. A loop typed fresh per character is a loop that drifts: the 2026-08-22 pass filed a strip under
the wrong key twice for exactly that reason. One entry point, one report, one place to fix.

The gate is `rhart.gate` through clip.py, and its verdict is echoed per clip rather than aggregated —
a batch where four of five passed is four clips to keep and one to re-roll, not a failure.
"""

from __future__ import annotations

import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ROSTER = os.path.join(REPO, "assets", "art", "Animations", "Roster")

# The champion's SE rotation is the contract's facing (arena-art-contract.md §1) — the figure faces
# right natively and the renderer never mirrors it.
DIRECTION = "south-east"

# attack/cast/idle/death keep the looser gate the 2026-08-22 pass gave them: an action's silhouette
# legitimately changes a lot. Every clip here is an action, so they all take it.
LOOSE = True


def run(char: str, clip: str, anim: str) -> tuple[str, bool, str]:
    out_dir = os.path.join(ROSTER, f"{char}_{clip}")
    out = os.path.join(out_dir, f"char_{char}_{clip}_strip8_512.png")
    os.makedirs(out_dir, exist_ok=True)
    cmd = [
        sys.executable, os.path.join(HERE, "clip.py"), "char",
        "--char", CHAR_IDS.get(char, char), "--anim", anim,
        "--dir", DIRECTION, "--out", out,
    ]
    if LOOSE:
        cmd.append("--loose")
    p = subprocess.run(cmd, capture_output=True, text=True)
    tail = (p.stdout or p.stderr).strip().splitlines()
    return clip, p.returncode == 0, tail[-1] if tail else "(no output)"


# Filled by the caller on the command line as <name>:<uuid>, or left empty when the first argument is
# already a uuid. Keeping the map here means a re-run months later does not need the ids re-found.
CHAR_IDS: dict[str, str] = {
    "seeker": "22e6d4d3-a470-462c-a39c-bfb67315e08a",
    "anvil": "4ef026d1-ecd2-44d1-acfe-b0ff4ffc01d2",
    "chorus": "32a9fdd9-bfe3-4538-84a0-8ea75abe8aa7",
    "metronome": "671a2fdf-1b18-40cd-baa3-b814a6935cc3",
    "unbroken": "62ac4546-930a-404c-808a-6175fe30d160",
    "tower": "55ff028c-6085-4d84-ae59-db8c8b1863d7",
    "quiver": "1478b963-d3c3-4f5d-a5de-47df0889ac9e",
    "thornwall": "cf73ce5f-77cc-4931-8b4b-51d70b10dcbb",
    "oathbound": "17c2e227-176f-4add-b5df-7ac08c0143dc",
    "magpie": "2134a661-b91d-4ee0-b2ba-f57bfab0e252",
}


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print(__doc__)
        return 2
    char = argv[0]
    pairs = []
    for a in argv[1:]:
        if "=" not in a:
            print(f"expected <clip>=<animId>, got {a!r}")
            return 2
        clip, anim = a.split("=", 1)
        pairs.append((clip, anim))

    bad = 0
    print(f"-- {char} --")
    for clip, anim in pairs:
        name, ok, line = run(char, clip, anim)
        print(f"  {name:16} {'PASS' if ok else 'FAIL'}  {line}")
        bad += 0 if ok else 1
    print(f"  {len(pairs) - bad}/{len(pairs)} filed")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
