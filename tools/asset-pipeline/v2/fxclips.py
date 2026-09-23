#!/usr/bin/env python3
"""fxclips -- file a character's per-Form EFFECT strips where the renderer looks for them.

    python tools/asset-pipeline/v2/fxclips.py <charId> <form>=<animate_image jobId> [...]
    python tools/asset-pipeline/v2/fxclips.py shared <key>=<animate_image jobId> [...]

Each pair becomes `assets/art/VFX/<char>_<form>/fx_<char>_<form>_strip8_512.png`, which is the key
`HuntScreen.FxFor` asks for first, before it falls back to the shared `fx_<form>`.

`shared` files the strips that are not a champion's: `shield=<job>` becomes
`assets/art/VFX/shield/fx_shield_strip8_512.png`. Only a key that already exists there is accepted, so a
typo cannot invent an effect nothing asks for. It is the same fetch and the same post-pass; before
2026-09-23 the shared strips were filed by hand with clip.py and whiten alone.

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

# SOFTEN BY ONE SOURCE PIXEL, NOT BY A NUMBER OF STRIP PIXELS. The strip is the generated canvas scaled
# up nearest-neighbour to 512 (x2.67 for the recipe's 192), and soften exists to take the one-bit STEP
# off that upscale. It was a flat radius 5 — two source pixels — which erased every splinter one source
# pixel wide: the 2026-09-23 Seeker candidate kept 1,008 bright cells at combat size before the post-pass
# and 50 after it. At 1.1 source pixels it kept 294 and still has no step (tools/fx_energy.py LIVE).
SOFTEN_SOURCE_PX = 1.1
FRAME = 512


def soften_radius(out: str) -> int:
    """The blur radius, in strip pixels, that is SOFTEN_SOURCE_PX of the generated canvas."""
    from PIL import Image
    sys.path.insert(0, HERE)
    import clip  # noqa: E402  (the frame cache clip.py just filled)
    with Image.open(os.path.join(clip.cache_dir(out), "f0.png")) as f0:
        return max(1, round(SOFTEN_SOURCE_PX * FRAME / max(f0.size)))


def run(char: str, form: str, job: str) -> tuple[str, bool, str]:
    stem = form if char == "shared" else f"{char}_{form}"
    out_dir = os.path.join(VFX, stem)
    out = os.path.join(out_dir, f"fx_{stem}_strip8_512.png")
    os.makedirs(out_dir, exist_ok=True)
    p = subprocess.run(
        [sys.executable, os.path.join(HERE, "clip.py"), "job",
         "--job", job, "--out", out, "--effect"],
        capture_output=True, text=True)
    tail = (p.stdout or p.stderr).strip().splitlines()
    ok = p.returncode == 0
    if ok:
        # ── THE POST-PASS, AND IT IS FOUR STEPS, NOT ONE. ────────────────────────────────────────
        #
        # This ran `whiten` alone and stopped, which is how fifty-odd strips shipped as ONE-BIT alpha:
        # 55 of 67 had exactly two alpha values, 0 and 255, and 1.0% of all effect pixels carried
        # anything in between. Nothing could fade, so every ray ended on a step and the rays that ran
        # off the canvas ended on a straight line — the rectangle players kept reporting (2026-08-23,
        # and again 2026-09-22 on THE SEEKER's own signature, whose strip had 12.4% of its border ring
        # at full opacity).
        #
        # The order is the whole point:
        #   whiten  - enforce the white/pale rule mechanically rather than hoping the generator obeys
        #   glow    - alpha := alpha x luminance, the additive contract: black becomes nothing
        #   soften  - blur the ALPHA so the silhouette itself gains a falloff, wherever it sits
        #   feather - ramp the outer frame band to zero LAST, because soften pushes alpha outward
        #
        # feather_fx alone was never enough: it only touches the frame's outer band, and a hard edge
        # 77-112 px inside the frame (measured on the trap strips) never reaches it.
        for step in (["rhart.py", "whiten", out],
                     ["rhart.py", "glow", out],
                     ["rhart.py", "soften", "--radius", str(soften_radius(out)), out],
                     ["feather_fx.py", out]):
            subprocess.run([sys.executable, os.path.join(HERE, step[0]), *step[1:]],
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
        if char == "shared":
            if not os.path.exists(os.path.join(VFX, form, f"fx_{form}_strip8_512.png")):
                print(f"no shared strip fx_{form}; only an existing shared key can be refiled")
                return 2
        elif form not in FORMS:
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
