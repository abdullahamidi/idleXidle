#!/usr/bin/env python3
"""brand_polish_evidence.py -- BRAND, Concept A LIVING SHADOW CORRUPTION, the three-fix polish pass (ADR-011, owner's
brief 2026-10-01): no "string of beads", no "zipper / seam / sash", depth 1 readable on every host. The owner's focused
list, from the takes films_brand.sh filmed with RH_BRAND_CONCEPT=A (this pass) beside d2885245's (before).

    PYTHONUTF8=1 python tools/asset-pipeline/brand_polish_evidence.py <new tag> <before tag> <scale shots dir> <out dir>

THE MEASURES pair every frame with the same frame of the same seeded fight with the mark drawn off (the *_off twins,
filmed from the same build) and sort every changed pixel into three kinds:
  CURSE EMISSION  brightened (luma +12) and VIOLET (blue over green by 30, red over green by 10): the curse's own light
  HOST DARKENED   darkened (luma -12): the curse's dark body on the host
  UNRELATED       brightened and NOT violet: bright edges the curse's shudder moved, smoke lifted by light under it
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from brand_cut_evidence import CLOSE, TRANSFER, anchor_at, settled  # noqa: E402
from brand_evidence import ROW, other_light, trace  # noqa: E402
from brand_fix_evidence import bodies, context_crop, quiet, tiles, whole_creature  # noqa: E402
from foundation_evidence import REPO, film, read  # noqa: E402

BOSSES = [("void_reaper", "VOID REAPER"), ("forge_colossus", "FORGE COLOSSUS"), ("thorn_regent", "THORN REGENT"),
          ("lumen_angel", "LUMEN ANGEL"), ("crystal_lich", "CRYSTAL LICH"), ("spirit_matron", "SPIRIT MATRON")]
REFERENCE_PEAKS = "SPRAY 2761 / 805, HARD HANDS 1382 / 612, JAWS 6957 / 5327, PRESS 749-867 (px above luma 170 / 210, ADR-011)"


def luma(a):
    return 0.299 * a[..., 0] + 0.587 * a[..., 1] + 0.114 * a[..., 2]


def sort_pixels(A, B):
    """The three kinds of change between a frame with the curse (A) and without (B), as boolean masks."""
    la, lb = luma(A), luma(B)
    brighter = (la - lb) > 12
    violet = (A[..., 2] > A[..., 1] + 30) & (A[..., 0] > A[..., 1] + 10)
    return brighter & violet, (lb - la) > 12, brighter & ~violet, la


def pairs(after, before, lo, hi, box_of=None, row=ROW):
    """Per matched frame: playhead, then in the creature ROW (the references' measure) curse px > 170 / > 210 and
    unrelated px > 170, then in the host's body box: curse px, darkened px, unrelated px, box area."""
    a_shots, b_shots = read(after + ".log"), read(before + ".log")
    b_items = sorted((ph, i) for i, (w, ph) in b_shots.items())
    out = []
    for i, (w, ph) in sorted(a_shots.items()):
        if ph < lo or ph > hi:
            continue
        j = min(b_items, key=lambda q: abs(q[0] - ph))
        if abs(j[0] - ph) > 9:
            continue
        A = np.asarray(Image.open(f"{after}_{i:02d}.png").convert("RGB")).astype(np.float32)
        B = np.asarray(Image.open(f"{before}_{j[1]:02d}.png").convert("RGB")).astype(np.float32)
        x0, y0, x1, y1 = row
        curse, dark, other, la = sort_pixels(A[y0:y1, x0:x1], B[y0:y1, x0:x1])
        rec = [ph, int((curse & (la > 170)).sum()), int((curse & (la > 210)).sum()), int((other & (la > 170)).sum())]
        box = box_of(after, ph) if box_of else None
        if box:
            bx, by, bw, bh = box
            c2, d2, o2, _ = sort_pixels(A[by:by + bh, bx:bx + bw], B[by:by + bh, bx:bx + bw])
            rec += [int(c2.sum()), int(d2.sum()), int(o2.sum()), bw * bh]
        else:
            rec += [0, 0, 0, 0]
        out.append(tuple(rec))
    return out


def host_box(dump):
    rects = bodies(dump)

    def box_of(pre, ph):
        a = anchor_at(pre, ph)
        if not a:
            return None
        hold = [r for r in rects if r[0] <= a[0] <= r[0] + r[2] and r[1] <= a[1] <= r[1] + r[3]]
        return min(hold, key=lambda r: abs(r[0] + r[2] / 2 - a[0])) if hold else None
    return box_of


HEAD = ("| moment | window (ms) | frames | CURSE EMISSION peak px > 170 / 210 (row) | UNRELATED peak px > 170 (moved edges, smoke) | "
        "curse px in the host (mean / peak) | host px darkened (mean) | share of the host's box changed by the curse (mean) | other light in the window |")


def row_of(name, lo, hi, rows, other):
    share = [(r[4] + r[5]) / r[7] for r in rows if r[7]]
    return (f"| {name} | {lo}-{hi} | {len(rows)} | {max(r[1] for r in rows)} / {max(r[2] for r in rows)} | "
            f"{max(r[3] for r in rows)} | {sum(r[4] for r in rows) / len(rows):.0f} / {max(r[4] for r in rows)} | "
            f"{sum(r[5] for r in rows) / len(rows):.0f} | {(sum(share) / len(share) if share else 0):.1%} | {other} |")


def measures(n, scale_dir, path):
    dump = lambda name: os.path.join(scale_dir, f"{name}.png.actors.txt")   # noqa: E731
    L = ["# BRAND, Concept A polish pass, in numbers", "", __doc__.split("THE MEASURES", 1)[1].strip(), "",
         "## The black whelps (ETCH), the reference fight", "", HEAD, "|" + "---|" * 9]
    box = host_box(dump("default"))
    # (the apply's growth and settle run to ~2920 ms: a depth's IDLE is measured once it has settled)
    for after, before, name, lo, hi in (("apply", "apply_off", "APPLY (the growth and its front)", 1750, 2400),
                                        ("clean_tick", "clean_tick_off", "IDLE, depth 1 (settled)", 3600, 3990),
                                        ("clean_tick", "clean_tick_off", "DEEPEN 1 -> 2 (the 4000 tick)", 3990, 4700),
                                        ("clean_tick", "clean_tick_off", "IDLE, depth 2", 4700, 4850),
                                        ("clean_tick6", "clean_tick6_off", "DEEPEN 2 -> 3 (the 6000 tick)", 5990, 6700),
                                        ("clean_tick6", "clean_tick6_off", "depth 3, then its host FALLS at 6717", 6700, 6850)):
        if os.path.exists(n(after) + ".log") and os.path.exists(n(before) + ".log"):
            rows = pairs(n(after), n(before), lo, hi, box)
            if rows:
                L.append(row_of(name, lo, hi, rows, other_light(n(after), lo, hi)))
    for key, label in BOSSES:
        pre, off = n(f"boss_{key}"), n(f"boss_{key}_off")
        if not (os.path.exists(pre + ".log") and os.path.exists(off + ".log")):
            continue
        L += ["", f"## {label} (one host that never falls, ETCH)", "", HEAD, "|" + "---|" * 9]
        box = host_box(dump(f"boss_{key}"))
        for name, lo, hi in (("APPLY", 1750, 2450), ("IDLE, depth 1 (settled)", 3000, 3950), ("DEEPEN 1 -> 2", 3990, 4800),
                             ("IDLE, depth 2", 4850, 5950), ("DEEPEN 2 -> 3", 5990, 6800), ("IDLE, depth 3", 6850, 7900)):
            rows = pairs(pre, off, lo, hi, box)
            if rows:
                L.append(row_of(name, lo, hi, rows, other_light(pre, lo, hi)))
    L += ["", f"The accepted references' peaks: {REFERENCE_PEAKS}.", "",
          "## Allocation and draw counts (mark-draw trace, every frame the curse draws)", ""]
    for take in ("long", "etch", "etch_clean", "sprawl", "sprawl_winnow_clean", "press", "fast", "light", "light_etch",
                 "fam_nature_bruiser") + tuple("boss_" + b for b, _ in BOSSES):
        if not os.path.exists(n(take) + ".log"):
            continue
        rows = trace(n(take), "mark-draw")
        if not rows:
            continue
        sprites = [int(r[1]["sprites"]) for r in rows]
        alloc = [int(r[1]["alloc"]) for r in rows]
        L.append(f"- **{take}**: {len(rows)} frames drawn; sprites per frame {min(sprites)}-{max(sprites)}; "
                 f"{sum(1 for a in alloc if a == 0)} of {len(alloc)} frames allocate 0 bytes")
    open(path, "w", encoding="utf-8").write("\n".join(L) + "\n")
    print(os.path.basename(path))


def depth_one_table(n, scale_dir, path):
    """DEPTH 1 AT REST on every host: how much of the host the curse visibly marks (curse px + darkened px), and its
    brightest violet, at play size."""
    dump = lambda name: os.path.join(scale_dir, f"{name}.png.actors.txt")   # noqa: E731
    L = ["| host | body box (px) | curse px (violet, brightened) | darkened px | marked share of the box | frames |", "|---|---|---|---|---|---|"]
    # (settled: the apply grows and settles until ~2920 ms)
    hosts = [("black whelp", n("clean_tick"), n("clean_tick_off"), "default", 3600, 3990),
             ("pale Choir wisp", n("light"), n("light_off"), "Spirit_Swarm", 3000, 3990)]
    hosts += [(label, n(f"boss_{k}"), n(f"boss_{k}_off"), f"boss_{k}", 3000, 3950) for k, label in BOSSES]
    for label, pre, off, d, lo, hi in hosts:
        if not (os.path.exists(pre + ".log") and os.path.exists(off + ".log")):
            continue
        rows = pairs(pre, off, lo, hi, host_box(dump(d)))
        rows = [r for r in rows if r[7]]
        if not rows:
            continue
        c = sum(r[4] for r in rows) / len(rows)
        dk = sum(r[5] for r in rows) / len(rows)
        area = rows[0][7]
        L.append(f"| {label} | {area} | {c:.0f} | {dk:.0f} | {(c + dk) / area:.1%} | {len(rows)} |")
    open(path, "w", encoding="utf-8").write("\n".join(L) + "\n")
    print(os.path.basename(path))


def same_crop(pre, off, ms, dump, pad=24):
    """The twin (no curse) at the same playhead, cropped to the SAME rectangle as the cursed frame's whole creature."""
    from brand_cut_evidence import frame_at
    a = anchor_at(pre, frame_at(pre, ms)[1])
    rects = bodies(dump)
    hold = [r for r in rects if a and r[0] <= a[0] <= r[0] + r[2] and r[1] <= a[1] <= r[1] + r[3]]
    x, y, w, h = min(hold, key=lambda r: abs(r[0] + r[2] / 2 - a[0])) if hold else (1000, 560, 400, 400)
    i, _ = frame_at(off, ms)
    return Image.open(f"{off}_{i:02d}.png").convert("RGB").crop((x - pad, y - pad, x + w + pad, y + h + pad))


def main():
    tag, before, scale_dir, out = sys.argv[1], sys.argv[2], sys.argv[3], os.path.abspath(sys.argv[4])
    os.makedirs(out, exist_ok=True)
    stills_only = os.environ.get("BRAND_STILLS_ONLY") == "1"
    for f in os.listdir(out):
        if f[:2].isdigit() and f.endswith((".png", ".md") if stills_only else (".mp4", ".png", ".md")):
            os.remove(os.path.join(out, f))
    if stills_only:
        global film
        film = lambda *a, **k: None   # noqa: E731
    shots = os.path.join(REPO, "build", "shots", "brand")
    n = lambda take: os.path.join(shots, tag, take)       # noqa: E731
    o = lambda take: os.path.join(shots, before, take)    # noqa: E731
    dump = lambda name: os.path.join(scale_dir, f"{name}.png.actors.txt")   # noqa: E731
    # 1 before vs new, idle on the black whelp (play size): depth 1 and depth 2 at rest
    pics = []
    for label, pre in (("d2885245 (before)", o("etch_clean")), ("this pass", n("etch_clean"))):
        for k, ms in ((1, 3300), (2, 4883), (3, 9900)):
            im, ph = context_crop(pre, ms, 400, 300)
            pics.append((f"{label}, depth {k} ({ph:.0f} ms)", im))
    tiles(pics, f"{out}/01_before_after_whelp.png", "IDLE ON THE BLACK WHELP: d2885245 (top) vs this pass (bottom), the same moments, play size", per_row=3, cap=400)
    # 2 the emission, close: old beads vs new runs (x3, the same moment)
    pics = []
    for label, pre in (("d2885245: the beads", o("etch_clean")), ("this pass: runs in dark corruption", n("etch_clean"))):
        for ms in (4883, 9900):
            im, ph = context_crop(pre, ms, 150, 100, 3)
            pics.append((f"{label} ({ph:.0f} ms), x3", im))
    tiles(pics, f"{out}/02_emission_close.png", "THE EMISSION, CLOSE (x3): isolated dots before, short tapered runs inside dark corruption now", per_row=2, cap=450)
    # 3 / 4 the depths, dark and pale (play size above, x2 below)
    # (fixed moments, each depth settled: the "settled" picker took depth 1 inside the apply's growth)
    for num, pre, name in (("03", n("etch_clean"), "black whelp"), ("04", n("light_etch"), "pale Choir wisp")):
        times = [3300, 4967, 9133 if num == "03" else 9867]
        pics = []
        for k, ms in zip((1, 2, 3), times):
            if ms is not None:
                im, ph = context_crop(pre, ms, 400, 300)
                pics.append((f"{name}, depth {k} ({ph:.0f} ms), play size", im))
        for k, ms in zip((1, 2, 3), times):
            if ms is not None:
                im, ph = context_crop(pre, ms, 200, 150, 2)
                pics.append((f"depth {k}, x2", im))
        tiles(pics, f"{out}/{num}_depths_{'dark' if num == '03' else 'pale'}.png", f"DEPTH 1 / 2 / 3 ON THE {name.upper()}", per_row=3, cap=400)
    # 5 / 6 Void Reaper and Lumen Angel, old vs new, whole creatures, depth 1 and depth 3
    for num, key, name in (("05", "void_reaper", "VOID REAPER"), ("06", "lumen_angel", "LUMEN ANGEL")):
        pics = []
        for label, pre in (("d2885245", o(f"boss_{key}")), ("this pass", n(f"boss_{key}"))):
            for want in ((1,), (2,), (3,)):
                s, ms = quiet(pre, want, dump(f"boss_{key}"), after=1300)
                if ms is not None:
                    im, ph, _ = whole_creature(pre, ms, dump(f"boss_{key}"))
                    pics.append((f"{label}, depth {s}", im))
        tiles(pics, f"{out}/{num}_{key}_before_after.png", f"{name}, WHOLE CREATURE, PLAY SIZE: d2885245 (top) vs this pass (bottom)", per_row=3, cap=700)
    # 7 Forge Colossus, depths 1 / 2 / 3
    pics = []
    for want in ((1,), (2,), (3,)):
        s, ms = quiet(n("boss_forge_colossus"), want, dump("boss_forge_colossus"), after=1300)
        if ms is not None:
            im, ph, _ = whole_creature(n("boss_forge_colossus"), ms, dump("boss_forge_colossus"))
            pics.append((f"FORGE COLOSSUS, depth {s} ({ph:.0f} ms)", im))
    tiles(pics, f"{out}/07_forge_colossus.png", "FORGE COLOSSUS (native lava cracks), WHOLE CREATURE, PLAY SIZE, DEPTH 1 / 2 / 3", per_row=3, cap=700)
    # 8 every boss at depth 1, whole, play size, beside the same frame with no curse
    # (caps of 700: at 340 / 440 the tiler shrank a whole boss to 55-86 %, and the owner asked for play size)
    pics = []
    for key, name in BOSSES:
        pre, off = n(f"boss_{key}"), n(f"boss_{key}_off")
        s, ms = quiet(pre, (1,), dump(f"boss_{key}"), after=1300)   # settled, past the apply's growth
        if ms is None:
            continue
        im, ph, _ = whole_creature(pre, ms, dump(f"boss_{key}"))
        pics.append((f"{name}, depth 1 ({ph:.0f} ms)", im))
        if os.path.exists(off + ".log"):
            pics.append((f"{name}, the same moment, NO curse", same_crop(pre, off, ph, dump(f"boss_{key}"))))
    tiles(pics, f"{out}/08_bosses_depth1.png", "EVERY BOSS AT DEPTH 1, WHOLE CREATURE, PLAY SIZE (100 %), beside the same frame with no curse", per_row=2, cap=700)
    # 9-14 at true speed
    film(n("apply"), f"{out}/09_apply.mp4", "APPLY, true speed (close)", crop=CLOSE)
    film(n("apply"), f"{out}/09b_apply_arena.mp4", "APPLY, true speed (the whole arena)")
    film(n("apply"), f"{out}/09c_apply_slow.mp4", "APPLY, x4 slower (close)", "--mute", "--slow", "4", crop=CLOSE)
    film(n("etch_clean"), f"{out}/10_deepen.mp4", "DEEPEN 1 -> 2 (4 s) -> 3 (6 s), true speed")
    film(n("etch_clean"), f"{out}/10b_deepen_close.mp4", "the deepens, close, true speed", lo=3600, hi=7000, crop=CLOSE)
    film(n("etch_clean"), f"{out}/10c_deepen_slow.mp4", "the deepens, close, x3 slower", "--mute", "--slow", "3", lo=3900, hi=7000, crop=CLOSE)
    film(n("long"), f"{out}/11_idle.mp4", "LONG IDLE, depth 1, true speed (close)", "--mute", lo=2400, hi=6400, crop=CLOSE)
    film(n("boss_void_reaper"), f"{out}/11b_idle_boss.mp4", "LONG IDLE on the Void Reaper (depth 1, then deeper), true speed", "--mute", lo=2300, hi=7900)
    film(n("sprawl"), f"{out}/12_sprawl.mp4", "SPRAWL: the source lights, the row ignites in turn, true speed")
    if os.path.exists(n("sprawl_winnow_clean") + ".log"):
        film(n("sprawl_winnow_clean"), f"{out}/12b_sprawl_deepen.mp4", "SPRAWL + WINNOW: the row deepens, true speed")
    film(n("long"), f"{out}/13_transfer.mp4", "TRANSFER: the front falls at 6.7 s and 8.2 s, true speed", lo=6000, hi=9400)
    film(n("long"), f"{out}/13b_transfer_close.mp4", "the transfer, close on the new front, true speed", lo=6600, hi=9300, crop=TRANSFER)
    film(n("press"), f"{out}/14a_with_press.mp4", "with PRESS (and SPRAY), true speed")
    film(n("fast"), f"{out}/14b_with_spray_hard_hands.mp4", "with SPRAY and HARD HANDS (fast TEMPO), true speed")
    film(n("etch"), f"{out}/14c_with_jaws.mp4", "with JAWS (and SPRAY), true speed", lo=3500, hi=6600)
    if os.path.exists(n("fam_nature_bruiser") + ".log"):
        film(n("fam_nature_bruiser"), f"{out}/14d_bruiser.mp4", "the Verdant bruiser, true speed")
    measures(n, scale_dir, f"{out}/15_measures.md")
    depth_one_table(n, scale_dir, f"{out}/16_depth1_every_host.md")
    print("done ->", out)


if __name__ == "__main__":
    main()
