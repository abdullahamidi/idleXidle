"""BRAND's mark size against every host, MEASURED: the front creature's visible body from the game's own geometry dump
(RH_SHOT_DUMP on one single-shot fight per host; tools/asset-pipeline/brand_scale_shots.sh takes them) and the mark's visible
extent from the atlas (cut and stain, alpha > 0.1, the idle cell of each depth), at the scale MarkPerformance.ScaleFor
snaps to.   python tools/asset-pipeline/brand_scale_table.py <shots dir> [max scale, e.g. 4/3] [--md]"""
import glob
import os
import sys
from fractions import Fraction

import numpy as np
from PIL import Image

ATLAS = "assets/art/VFX/parts/fxp_seeker_brand.png"
CELL, IDLE_COL, HALO_ROW0 = 84, 3, 4
SIZE_SHARE, COIL_BOX, MIN_SCALE = 0.28, 60.0, 2 / 3
NAMES = {"Nature": "Verdant Hollow", "Machine": "Cinderworks", "Shadow": "Umbral Reach", "Body": "Marrow Wastes",
         "Mind": "The Still Archive", "Spirit": "The Pale Choir"}


def extents():
    a = np.asarray(Image.open(ATLAS).convert("RGBA"))[..., 3] / 255.0
    out = {}
    for depth in (1, 2, 3):
        cells = [a[r * CELL:(r + 1) * CELL, IDLE_COL * CELL:(IDLE_COL + 1) * CELL] for r in (depth, HALO_ROW0 + depth)]
        ys, xs = np.nonzero(np.maximum(*cells) > 0.1)
        out[depth] = (xs.max() - xs.min() + 1, ys.max() - ys.min() + 1)
    return out


def scale_for(h, max_scale):
    s = round(SIZE_SHARE * h / COIL_BOX * 3) / 3
    return min(max(s, MIN_SCALE), max_scale)


def hosts(folder):
    for f in sorted(glob.glob(os.path.join(folder, "*.png.actors.txt"))):
        name = os.path.basename(f)[:-len(".png.actors.txt")]
        for line in open(f, encoding="utf-8"):
            if line.startswith("Creature/0\t"):
                x, y, w, h = map(int, line.split("\t")[1].split()[1].split(","))
                yield name, w, h


def label(name):
    if name == "default": return "dark whelp (the reference, Umbral Reach Swarm)"
    if name.startswith("boss_"): return "boss: " + name[5:].replace("_", " ").upper()
    src, arch = name.split("_")
    return f"{NAMES[src]} {arch}" + (" (pale host)" if src == "Spirit" and arch == "Swarm" else "")


if __name__ == "__main__":
    folder = sys.argv[1]
    cap = float(Fraction(sys.argv[2])) if len(sys.argv) > 2 and not sys.argv[2].startswith("--") else 3.0
    md = "--md" in sys.argv
    ext = extents()
    rows = []
    for name, w, h in hosts(folder):
        s, s_old = scale_for(h, cap), scale_for(h, 3.0)
        (w1, h1), (w3, h3) = ext[1], ext[3]
        rows.append((label(name), w, h, s_old, s, w1 * s, h1 * s, w3 * s, h3 * s))
    hdr = ("host", "visible w", "visible h", "scale before", "scale", "D1 w x h", "D3 w x h", "D3 w share", "D3 h share",
           "D1 h share")
    if md: print("| " + " | ".join(hdr) + " |\n|" + "---|" * len(hdr))
    for r in rows:
        n, w, h, so, s, w1, h1, w3, h3 = r
        cells = (n, w, h, f"{so:.2f}", f"{s:.2f}", f"{w1:.0f} x {h1:.0f}", f"{w3:.0f} x {h3:.0f}", f"{w3 / w:.0%}",
                 f"{h3 / h:.0%}", f"{h1 / h:.0%}")
        print(("| " + " | ".join(map(str, cells)) + " |") if md else "  ".join(f"{c!s:>12}" for c in cells))
