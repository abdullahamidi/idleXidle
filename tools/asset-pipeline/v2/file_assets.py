#!/usr/bin/env python3
"""file_assets — move the approved 2026-08-22 arena art from staging into assets/art, derive the
statics the runtime still asks for, and retire the art it supersedes.

    python tools/asset-pipeline/v2/file_assets.py --plan          # print what would happen
    python tools/asset-pipeline/v2/file_assets.py --apply         # do it
    python tools/asset-pipeline/v2/file_assets.py --apply --only roster,enemies

Why a script and not a pile of cp: the runtime keys are flat basenames and the old art shares many of
them (char_<id>_idle_strip8_512 existed before this pass, in a different camera) — a half-done copy
would leave a stage where the champion faces right and the enemies still face the viewer. One command
files a GROUP atomically and removes what that group replaces, so the tree is always in one style.

What is retired, per group (every item below was either replaced by this pass or proven dormant —
no code key reaches it; see design/art/arena-art-contract.md and the 2026-08-22 session notes):
  roster   : the previous char_<id>_base + char_<id>_{idle,attack} strips (front-facing painterly set)
  enemies  : Animations/Enemies/* (incl. stone_sentinel_slam), Enemies/enemies/<key>/* extras
             (idle_02, attack_02, portrait, silhouette, ground_shadow — nothing reads them);
             idle_01 / attack_01 are RE-DERIVED from the new strips (the arena's static fallback)
  bosses   : Animations/Bosses/*_strip8_1024 and Bosses/bosses/*/normalized (the boss_<region> aliases
             that pointed at them were dead and are gone from AssetLibrary)
  vfx      : VFX/{aura,binding,death,heal,impact,interrupt,levelup,loot,projectile,slash,smoke}
             (superseded by fx_* — VFX/traits/vfx_trait_burst stays, the prestige screen reads it)
  hunter   : Animations/Hunter/* , Characters/Hunter/poses/* , Characters/Hunter/hunter_rig_base.png
             (the retired single-Hunter strips and cutout-rig base: no key in the game reaches them —
             the playable figure is the roster character; hunter_portrait + icons STAY)
"""

from __future__ import annotations

import argparse
import glob
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
STAGING = os.path.join(REPO, "tools", "asset-pipeline", ".staging", "v2")
ART = os.path.join(REPO, "assets", "art")

sys.path.insert(0, HERE)
import rhart  # noqa: E402

ROSTER = ["seeker", "anvil", "chorus", "metronome", "unbroken", "tower", "quiver", "thornwall", "oathbound", "magpie"]
ENEMIES = ["bonecrawler", "soul_leech", "wisp", "stone_sentinel", "shadeling", "rift_guardian"]
BOSSES = ["thorn_regent", "forge_colossus", "void_reaper", "crystal_lich", "lumen_angel", "spirit_matron"]
EFFECTS = ["strike", "projectile", "aura", "trap", "mark", "transformation",
           "hit", "weakhit", "crit", "death", "heal", "shield", "levelup"]


def plan_group(group: str) -> tuple[list[tuple[str, str]], list[str], list[tuple[str, str]]]:
    """Returns (copies [(src, dst)], removals [path or glob], derived statics [(strip, dst)])."""
    copies: list[tuple[str, str]] = []
    removes: list[str] = []
    statics: list[tuple[str, str]] = []
    if group == "roster":
        for cid in ROSTER:
            for clip in ("idle", "attack", "cast", "death"):
                src = os.path.join(STAGING, "roster", f"char_{cid}_{clip}_strip8_512.png")
                dst = os.path.join(ART, "Animations", "Roster", f"{cid}_{clip}", f"char_{cid}_{clip}_strip8_512.png")
                copies.append((src, dst))
            copies.append((os.path.join(STAGING, "roster", f"char_{cid}_base.png"),
                           os.path.join(ART, "Characters", "Roster", f"char_{cid}_base.png")))
    elif group == "enemies":
        removes += [os.path.join(ART, "Animations", "Enemies")]
        for key in ENEMIES:
            for clip in ("idle", "attack"):
                src = os.path.join(STAGING, "enemies", f"{key}_{clip}_strip8_512.png")
                dst = os.path.join(ART, "Animations", "Enemies", f"{key}_{clip}", f"{key}_{clip}_strip8_512.png")
                copies.append((src, dst))
                statics.append((dst, os.path.join(ART, "Enemies", "enemies", key, f"{key}_{clip}_01.png")))
            removes += [os.path.join(ART, "Enemies", "enemies", key)]
    elif group == "bosses":
        removes += [os.path.join(ART, "Animations", "Bosses"), os.path.join(ART, "Bosses")]
        for key in BOSSES:
            for clip in ("idle", "attack"):
                src = os.path.join(STAGING, "bosses", f"{key}_{clip}_strip8_512.png")
                dst = os.path.join(ART, "Animations", "Bosses", f"{key}_{clip}", f"{key}_{clip}_strip8_512.png")
                copies.append((src, dst))
    elif group == "vfx":
        for sub in ("aura", "binding", "death", "heal", "impact", "interrupt", "levelup", "loot", "projectile", "slash", "smoke"):
            removes.append(os.path.join(ART, "VFX", sub))
        for fx in EFFECTS:
            copies.append((os.path.join(STAGING, "vfx", f"fx_{fx}_strip8_512.png"),
                           os.path.join(ART, "VFX", fx, f"fx_{fx}_strip8_512.png")))
    elif group == "hunter":
        removes += [os.path.join(ART, "Animations", "Hunter"), os.path.join(ART, "Characters", "Hunter", "poses"),
                    os.path.join(ART, "Characters", "Hunter", "hunter_rig_base.png")]
    else:
        raise SystemExit(f"unknown group {group}")
    return copies, removes, statics


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--plan", action="store_true")
    ap.add_argument("--only", default="roster,enemies,bosses,vfx,hunter")
    a = ap.parse_args(argv)
    groups = [g.strip() for g in a.only.split(",") if g.strip()]

    missing: list[str] = []
    for g in groups:
        copies, removes, statics = plan_group(g)
        for src, _ in copies:
            if not os.path.exists(src):
                missing.append(os.path.relpath(src, REPO))
    if missing:
        print("STAGING IS INCOMPLETE — these approved files are missing, nothing was filed:")
        for m in missing:
            print("   " + m)
        return 2

    for g in groups:
        copies, removes, statics = plan_group(g)
        print(f"== {g}: {len(copies)} files in, {len(removes)} retire paths, {len(statics)} derived statics")
        if a.apply:
            for r in removes:
                if os.path.isdir(r):
                    shutil.rmtree(r)
                elif os.path.exists(r):
                    os.remove(r)
            for src, dst in copies:
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                shutil.copyfile(src, dst)
            for strip, dst in statics:
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                rhart.static_from(strip, dst, 0)
        else:
            for r in removes:
                print("   retire " + os.path.relpath(r, REPO))
            for src, dst in copies[:4]:
                print("   file   " + os.path.relpath(dst, REPO))
            if len(copies) > 4:
                print(f"   ... and {len(copies) - 4} more")
    if a.apply:
        # The strips must all be truthful 8x512 rows — the same gate the agents ran, once more, on the
        # files that actually landed (a copy that went wrong would fail soft at runtime and look like art).
        bad = 0
        for path in glob.glob(os.path.join(ART, "Animations", "**", "*_strip8_512.png"), recursive=True) \
                  + glob.glob(os.path.join(ART, "VFX", "**", "fx_*_strip8_512.png"), recursive=True):
            im = rhart.load_rgba(path)
            if im.height != 512 or im.width != 512 * 8:
                print(f"   BAD STRIP SHAPE {os.path.relpath(path, REPO)}: {im.width}x{im.height}")
                bad += 1
        print("filed." if not bad else f"filed, {bad} bad strips — FIX BEFORE COMMITTING")
        return 1 if bad else 0
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
