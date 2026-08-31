#!/usr/bin/env python3
"""Derive every static sprite the runtime asks for out of the animation strips.

`SoloExpeditionScreen` falls back to a static when a strip is missing or still
loading, and `DrawHunter` asks for statics directly:

    staticKey = attacking ? $"{en}_attack_01" : $"{en}_idle_01"   (:505)
    key = dead ? "hunter_defeated" : attacking ? "hunter_attack_01" : "hunter_idle"  (:855)

Slicing frame 0 out of the matching strip costs nothing and guarantees the
static matches the animation exactly — a separately generated one would drift.

Also emits the portrait / silhouette / ground-shadow variants, which are pure
transforms of the same frame.

Run after animate.py.
"""

from __future__ import annotations

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from derive import fit_square, ground_shadow, portrait, silhouette, slice_strip  # noqa: E402
from pixelpng import read, write  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))

# (strip key, strip dir)  ->  statics to cut from it.
# Frame 0 is the rest pose; a mid-swing frame reads better for "attacking".
ENEMIES = {
    "bonecrawler": "attack", "soul_leech": "attack", "wisp": "attack",
    "stone_sentinel": "slam", "shadeling": "attack", "rift_guardian": "attack",
}
BOSSES = ["thorn_regent", "forge_colossus", "void_reaper", "crystal_lich", "lumen_angel", "spirit_matron"]


def strip_path(dest: str, key: str) -> str:
    return os.path.join(REPO, dest, f"{key}.png")


def cut(strip_file: str, frame: int):
    img = read(strip_file)
    frames = slice_strip(img)
    return frames[min(frame, len(frames) - 1)]


def emit(img, path: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    write(path, img)


def main() -> int:
    made = 0
    missing = []

    def need(p: str) -> bool:
        if os.path.exists(p):
            return True
        missing.append(os.path.relpath(p, REPO))
        return False

    # --- Enemies: <en>_idle_01, <en>_attack_01, portrait, silhouette, ground_shadow
    for en, act in ENEMIES.items():
        idle = strip_path(f"assets/art/Animations/Enemies/{en}_idle", f"{en}_idle_strip8_512")
        atk = strip_path(f"assets/art/Animations/Enemies/{en}_{act}", f"{en}_{act}_strip8_512")
        base = os.path.join(REPO, "assets/art/Enemies/enemies", en)
        if need(idle):
            f0 = cut(idle, 0)
            emit(f0, os.path.join(base, f"{en}_idle_01.png"))
            emit(f0, os.path.join(base, f"{en}_idle_02.png"))
            emit(portrait(f0, 256, 256), os.path.join(base, f"{en}_portrait.png"))
            emit(silhouette(f0), os.path.join(base, f"{en}_silhouette.png"))
            emit(ground_shadow(f0, 256, 96), os.path.join(base, f"{en}_ground_shadow.png"))
            made += 5
        if need(atk):
            # Mid-swing reads as "attacking" far better than the wind-up.
            fa = cut(atk, 4)
            emit(fa, os.path.join(base, f"{en}_attack_01.png"))
            emit(fa, os.path.join(base, f"{en}_attack_02.png"))
            made += 2

    # --- Bosses: idle/attack statics + portraits + silhouette + shadow
    for boss in BOSSES:
        idle = strip_path(f"assets/art/Animations/Bosses/{boss}_idle", f"{boss}_idle_strip8_1024")
        atk = strip_path(f"assets/art/Animations/Bosses/{boss}_attack", f"{boss}_attack_strip8_1024")
        base = os.path.join(REPO, "assets/art/Bosses/bosses", boss)
        if need(idle):
            f0 = cut(idle, 0)
            emit(f0, os.path.join(base, f"{boss}_idle.png"))
            emit(fit_square(f0, 1024), os.path.join(base, "normalized", f"{boss}_idle_1024.png"))
            emit(portrait(f0, 256, 256), os.path.join(base, f"{boss}_portrait_square.png"))
            emit(portrait(f0, 256, 384), os.path.join(base, f"{boss}_portrait_bust.png"))
            emit(silhouette(f0, 512), os.path.join(base, f"{boss}_silhouette.png"))
            emit(ground_shadow(f0, 512, 160), os.path.join(base, f"{boss}_shadow.png"))
            made += 6
        if need(atk):
            fa = cut(atk, 4)
            emit(fa, os.path.join(base, f"{boss}_attack.png"))
            emit(fit_square(fa, 1024), os.path.join(base, "normalized", f"{boss}_attack_1024.png"))
            made += 2

    # --- Hunter: the three keys SoloExpeditionScreen asks for by name
    H = "assets/art/Animations/Hunter"
    C = os.path.join(REPO, "assets/art/Characters/Hunter/poses")
    idle = strip_path(f"{H}/hunter_idle", "hunter_idle_strip8_512")
    atk = strip_path(f"{H}/hunter_attack", "hunter_attack_strip8_512")
    death = strip_path(f"{H}/hunter_death", "hunter_death_strip8_512")
    if need(idle):
        emit(cut(idle, 0), os.path.join(C, "hunter_idle.png"))
        made += 1
    if need(atk):
        emit(cut(atk, 4), os.path.join(C, "hunter_attack_01.png"))
        made += 1
    if need(death):
        emit(cut(death, 7), os.path.join(C, "hunter_defeated.png"))
        made += 1

    print(f"{made} static(s) derived from strips.")
    if missing:
        print(f"\n{len(missing)} strip(s) not generated yet — statics skipped:", file=sys.stderr)
        for m in missing:
            print(f"  {m}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
