#!/usr/bin/env python3
"""fxclips -- file a character's per-Form EFFECT strips where the renderer looks for them.

    python tools/asset-pipeline/v2/fxclips.py <charId> <form>=<animate_image jobId> [...]

Each pair becomes `assets/art/VFX/<char>_<form>/fx_<char>_<form>_strip8_512.png`, which is the key
`HuntScreen.FxFor` asks for first, before it falls back to the shared `fx_<form>`.

This is skillclips.py's twin and exists for the same reason: 45 effects arrive in nine batches of
five, each needing fetch -> strip -> gate -> file in that order, and a loop typed fresh per character
is a loop that drifts. The difference is the `--effect` flag on clip.py, which centres the strip
instead of grounding it and relaxes the silhouette bars -- an effect has no feet and no baseline.

Effects are authored white / pale (arena-art-contract.md §3.3): the Source tint multiplies over them
at play time, so the SHAPE says who cast it and the COLOUR says what it is made of.
"""

from __future__ import annotations

import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
VFX = os.path.join(REPO, "assets", "art", "VFX")

FORMS = ("strike", "projectile", "mark", "trap", "transformation", "aura")


def run(char: str, form: str, job: str) -> tuple[str, bool, str]:
    out_dir = os.path.join(VFX, f"{char}_{form}")
    out = os.path.join(out_dir, f"fx_{char}_{form}_strip8_512.png")
    os.makedirs(out_dir, exist_ok=True)
    p = subprocess.run(
        [sys.executable, os.path.join(HERE, "clip.py"), "job",
         "--job", job, "--out", out, "--effect"],
        capture_output=True, text=True)
    tail = (p.stdout or p.stderr).strip().splitlines()
    ok = p.returncode == 0
    if ok:
        # Enforce the white/pale rule mechanically rather than hoping the generator obeys it — see
        # rhart.whiten. Doing it here means every filed effect is tint-ready by construction.
        subprocess.run([sys.executable, os.path.join(HERE, "rhart.py"), "whiten", out],
                       capture_output=True, text=True)
    return form, ok, tail[-1] if tail else "(no output)"


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print(__doc__)
        return 2
    char = argv[0]
    pairs = []
    for a in argv[1:]:
        if "=" not in a:
            print(f"expected <form>=<jobId>, got {a!r}")
            return 2
        form, job = a.split("=", 1)
        if form not in FORMS:
            print(f"unknown form {form!r}; expected one of {', '.join(FORMS)}")
            return 2
        pairs.append((form, job))

    bad = 0
    print(f"-- {char} effects --")
    for form, job in pairs:
        name, ok, line = run(char, form, job)
        print(f"  {name:16} {'PASS' if ok else 'FAIL'}  {line}")
        bad += 0 if ok else 1
    print(f"  {len(pairs) - bad}/{len(pairs)} filed")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
