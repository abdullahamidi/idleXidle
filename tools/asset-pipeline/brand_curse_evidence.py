#!/usr/bin/env python3
"""brand_curse_evidence.py -- BRAND as a CURSE, three direction concepts (ADR-011, owner's brief 2026-10-01): the review
battery from the takes films_brand.sh filmed with RH_BRAND_CONCEPT=A|B|C (tags <prefix>A, <prefix>B, <prefix>C).

    PYTHONUTF8=1 python tools/asset-pipeline/brand_curse_evidence.py <tag prefix> <scale shots dir> <out dir> [ABC]

Per concept X: X_apply (true speed, close) + X_apply_slow, X_idle, X_deepen + X_deepen_slow, X_sprawl, X_transfer,
X_mixed_press, X_mixed_jaws, X_hosts.png (dark and pale at each depth), X_large.png (the Verdant bruiser and the Void
Reaper, whole creatures). And 00_compare.png: the three side by side at the same moments.
"""
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from brand_cut_evidence import CLOSE, TRANSFER, settled  # noqa: E402
from brand_fix_evidence import context_crop, quiet, tiles, whole_creature  # noqa: E402
from foundation_evidence import REPO, SMALL, film  # noqa: E402

NAMES = {"A": "LIVING SHADOW CORRUPTION", "B": "WITHERING CURSE", "C": "SHADOW POSSESSION"}


def main():
    prefix, scale_dir, out = sys.argv[1], sys.argv[2], os.path.abspath(sys.argv[3])
    which = sys.argv[4] if len(sys.argv) > 4 else "ABC"
    os.makedirs(out, exist_ok=True)
    shots = os.path.join(REPO, "build", "shots", "brand")
    dump = lambda name: os.path.join(scale_dir, f"{name}.png.actors.txt")   # noqa: E731
    compare_rows = []
    for c in which:
        n = lambda take: os.path.join(shots, prefix + c, take)   # noqa: E731
        if not os.path.exists(n("apply") + ".log"):
            continue
        film(n("apply"), f"{out}/{c}_apply.mp4", f"{c} {NAMES[c]}: APPLY, true speed", crop=CLOSE)
        film(n("apply"), f"{out}/{c}_apply_slow.mp4", f"{c}: APPLY, x4 slower", "--mute", "--slow", "4", crop=CLOSE)
        film(n("long"), f"{out}/{c}_idle.mp4", f"{c}: IDLE, depth 1, true speed (2.4-5.9 s)", "--mute", lo=2400, hi=5900, crop=CLOSE)
        film(n("etch_clean"), f"{out}/{c}_deepen.mp4", f"{c}: DEEPEN 1 -> 2 (4 s) -> 3 (6 s), true speed")
        film(n("etch_clean"), f"{out}/{c}_deepen_slow.mp4", f"{c}: the deepens, close, x3 slower", "--mute", "--slow", "3",
             lo=3900, hi=7000, crop=CLOSE)
        film(n("sprawl"), f"{out}/{c}_sprawl.mp4", f"{c}: SPRAWL, the row cursed from its source, true speed")
        film(n("long"), f"{out}/{c}_transfer.mp4", f"{c}: TRANSFER, the front falls at 6.7 s and 8.2 s, true speed", lo=6000, hi=9400)
        film(n("long"), f"{out}/{c}_transfer_close.mp4", f"{c}: the transfer, close on the new front", lo=6600, hi=9300, crop=TRANSFER)
        film(n("press"), f"{out}/{c}_mixed_press.mp4", f"{c}: beside PRESS and SPRAY, true speed")
        film(n("etch"), f"{out}/{c}_mixed_jaws.mp4", f"{c}: beside JAWS and SPRAY, true speed", lo=3500, hi=6600)
        # the hosts: dark and pale at each depth (play size)
        pics = []
        for label, pre, times in (("dark", n("etch_clean"), (3300, 4900, 9900)), ("pale", n("light_etch"), None)):
            for k in (1, 2, 3):
                ms = times[k - 1] if times else settled(pre, k, after=400)
                if ms is None:
                    continue
                im, ph = context_crop(pre, ms, 360, 300)
                pics.append((f"{label}, depth {k} ({ph:.0f} ms)", im))
        tiles(pics, f"{out}/{c}_hosts.png", f"{c} {NAMES[c]}: DARK AND PALE HOSTS AT EACH DEPTH (play size)", per_row=3, cap=360)
        # the large hosts: whole creatures at the deepest depth
        pics = []
        for label, pre, d in (("Verdant bruiser", n("fam_nature_bruiser"), dump("Nature_Bruiser")),
                              ("Void Reaper (boss)", n("boss_void_reaper"), dump("boss_void_reaper"))):
            for want in ((1,), (3, 2)):
                s, ms = quiet(pre, want, d, after=750)   # at rest: clear of any flare
                if ms is not None:
                    im, ph, _ = whole_creature(pre, ms, d)
                    pics.append((f"{label}, depth {s} ({ph:.0f} ms)", im))
        tiles(pics, f"{out}/{c}_large.png", f"{c} {NAMES[c]}: LARGE HOSTS, WHOLE CREATURES (play size)", per_row=2, cap=560)
        # the comparison row
        row = []
        for label, pre, ms in (("apply (flare)", n("apply"), 1950), ("idle, depth 1", n("long"), 3300),
                               ("depth 3", n("etch_clean"), 9900), ("pale, depth 3", n("light_etch"), settled(n("light_etch"), 3, after=400))):
            if ms is not None:
                row.append((label, context_crop(pre, ms, 300, 260)[0]))
        s, ms = quiet(n("boss_void_reaper"), (3, 2), dump("boss_void_reaper"), after=750)
        if ms is not None:
            im, _, _ = whole_creature(n("boss_void_reaper"), ms, dump("boss_void_reaper"))
            k = 260 / im.height
            row.append(("boss, depth 3", im.resize((int(im.width * k), 260), Image.LANCZOS)))
        compare_rows.append((c, row))
    # 00: the three side by side
    if compare_rows:
        cols = max(len(r) for _, r in compare_rows)
        widths = [max(r[i][1].width for _, r in compare_rows if i < len(r)) for i in range(cols)]
        img = Image.new("RGB", (40 + sum(w + 8 for w in widths), 30 + len(compare_rows) * 288), (20, 17, 14))
        d = ImageDraw.Draw(img)
        d.text((8, 6), "THE THREE CONCEPTS AT THE SAME MOMENTS (play size; the boss shown whole, scaled to the row)", fill=(235, 200, 120), font=SMALL)
        for r, (c, row) in enumerate(compare_rows):
            y = 26 + r * 288
            d.text((8, y + 130), c, fill=(235, 200, 120), font=SMALL)
            x = 40
            for i, (label, im) in enumerate(row):
                d.text((x, y), label, fill=(200, 180, 150), font=SMALL)
                img.paste(im, (x, y + 16))
                x += widths[i] + 8
        img.save(f"{out}/00_compare.png")
        print("00_compare.png")
    print("done ->", out)


if __name__ == "__main__":
    main()
