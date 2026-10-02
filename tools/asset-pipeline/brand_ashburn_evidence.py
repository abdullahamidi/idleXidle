#!/usr/bin/env python3
"""brand_ashburn_evidence.py -- BRAND, Concept A LIVING SHADOW CORRUPTION, the ASH-BURN pass (ADR-011, owner's brief from
91c1650b): the territory system is locked; on hosts whose own look is dark, violet or magical a second, host-adaptive
material response (ASH-BURN: the colour burned out to cold ash, value pushed away from the host's own, near-black burnt
edges, pale spectral fissures, a very restrained violet) is blended in; the first territory takes the stable central body
mass; three presentation bugs are fixed. From the takes films_brand.sh filmed with RH_BRAND_CONCEPT=A (this pass) beside
91c1650b's (the territory pass).

    PYTHONUTF8=1 python tools/asset-pipeline/brand_ashburn_evidence.py <tag> <scale shots dir> <out dir>

Every still of one host is cut from the SAME rectangle; a boss's depths are shown in the SAME POSE (the uncursed twin's
frame most like depth 1's picks each later moment), so the only difference is the curse. The measures are
brand_regions_evidence.py's (four kinds of changed pixel against the *_off twins).
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from brand_cut_evidence import CLOSE, TRANSFER, frame_at  # noqa: E402
from brand_evidence import trace  # noqa: E402
from brand_fix_evidence import bodies, context_crop, tiles  # noqa: E402
from brand_regions_evidence import SMALL, SMALL_TIMES, depth_one_table, measures  # noqa: E402
from foundation_evidence import REPO, film, read  # noqa: E402


def settled_times(pre, stage, after=1300):
    """Every traced playhead where the drawn stage has been `stage` for `after` ms."""
    rows = [(r[0], int(r[1]["stage"])) for r in trace(pre, "mark-draw") if "stage" in r[1]]
    since, out = None, []
    for t, st in rows:
        since = (t if since is None else since) if st == stage else None
        if since is not None and t - since >= after:
            out.append(t)
    return out


def boss_rect(dump, pad=24):
    rects = bodies(dump)
    x, y, w, h = max(rects, key=lambda r: r[2] * r[3]) if rects else (1000, 560, 400, 400)
    return x - pad, y - pad, x + w + pad, y + h + pad


def crop(pre, ms, rect):
    i, ph = frame_at(pre, ms)
    return Image.open(f"{pre}_{i:02d}.png").convert("RGB").crop(rect), ph


def bright(im):
    return int((np.asarray(im.convert("L")) > 200).sum())


def same_pose(n, o, key, dump, label, stages=(1, 2, 3), old=True):
    """A boss: NO curse / [old depth 1] / depth 1 / depth 2 / depth 3, all in the pose of depth 1's moment."""
    pre, off, before = n(key), n(key + "_off"), o(key)
    if not os.path.exists(off + ".log"):
        off = o(key + "_off")
    rect = boss_rect(dump(key))
    c1 = settled_times(pre, 1)
    if not c1:
        return []
    # depth 1's moment: the least foreign light (a swing's glint, a hit's flash) in the creature
    t1 = min(c1[::3], key=lambda t: bright(crop(pre, t, rect)[0]))
    ref = np.asarray(crop(off, t1, rect)[0].convert("L")).astype(np.float32)
    pics = [(f"{label}, NO curse ({t1:.0f} ms)", crop(off, t1, rect)[0])]
    if old and os.path.exists(before + ".log"):
        im, ph = crop(before, t1, rect)
        pics.append((f"{label}, OLD 91c1650b, depth 1 ({ph:.0f} ms)", im))
    for s in stages:
        cands = c1 if s == 1 else settled_times(pre, s)
        if not cands:
            continue
        # the later depths in the SAME POSE: the uncursed twin's frame most like depth 1's
        t = t1 if s == 1 else min(cands[::2], key=lambda q: float(np.abs(np.asarray(crop(off, q, rect)[0].convert("L")).astype(np.float32) - ref).mean()))
        im, ph = crop(pre, t, rect)
        pics.append((f"{label}, NEW depth {s} ({ph:.0f} ms)", im))
    return pics


def small(n, key, label, w, h):
    """A pack creature: NO curse / depth 1 / depth 3 round the curse's anchor."""
    pre, off = n(key), n(key + "_off")
    t1, t3 = SMALL_TIMES[1], SMALL_TIMES[3]
    pics = []
    if os.path.exists(off + ".log"):
        pics.append((f"{label}, NO curse ({t1} ms)", context_crop(off, t1, w, h)[0]))
    for k, t in ((1, t1), (3, t3)):
        im, ph = context_crop(pre, t, w, h)
        pics.append((f"{label}, depth {k} ({ph:.0f} ms)", im))
    return pics


def main():
    """A SMALL showing (the owner, 2026-10-02: a few stills and one clip, never a gallery): six takes, three sheets, one clip."""
    tag, scale_dir, out = sys.argv[1], sys.argv[2], os.path.abspath(sys.argv[3])
    os.makedirs(out, exist_ok=True)
    shots = os.path.join(REPO, "build", "shots", "brand")
    n = lambda take: os.path.join(shots, tag, take)       # noqa: E731
    dump = lambda name: os.path.join(scale_dir, f"{name}.png.actors.txt")   # noqa: E731
    for num, key, label, stages in (("01", "boss_void_reaper", "VOID REAPER", (1, 2, 3)),
                                    ("02", "boss_crystal_lich", "CRYSTAL LICH", (1, 3))):
        pics = same_pose(n, n, key, dump, label, stages, old=False)
        if pics:
            tiles(pics, f"{out}/{num}_{label.lower().replace(' ', '_')}.png",
                  f"{label}: NO curse / " + " / ".join(f"depth {s}" for s in stages) + " (one pose, play size)",
                  per_row=len(pics), cap=520)
    w, h = SMALL["etch_clean"]
    tiles(small(n, "etch_clean", "black whelp", w, h), f"{out}/03_black_whelp.png",
          "BLACK WHELP: NO curse / depth 1 / depth 3, play size", per_row=3, cap=700)
    film(n("boss_void_reaper"), f"{out}/04_deepen_reaper.mp4", "the deepens on the Void Reaper, true speed", "--mute", lo=3500, hi=8200)
    print("done ->", out)


if __name__ == "__main__":
    main()
