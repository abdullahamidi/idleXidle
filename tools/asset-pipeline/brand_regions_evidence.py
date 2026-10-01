#!/usr/bin/env python3
"""brand_regions_evidence.py -- BRAND, Concept A LIVING SHADOW CORRUPTION, the TERRITORY pass (ADR-011, owner's brief
from 9aa407fd): the curse as separate regions of the body becoming corrupted, the host's own material drained, depth as
more of the body infected. The owner's decision-focused list, from the takes films_brand.sh filmed with
RH_BRAND_CONCEPT=A (this pass) beside 9aa407fd's (the polish pass).

    PYTHONUTF8=1 python tools/asset-pipeline/brand_regions_evidence.py <new tag> <9aa407fd tag> <scale shots dir> <out dir>

THE MEASURES pair every frame with the same frame of the same seeded fight with the mark drawn off (the *_off twins,
filmed from the same build) and sort every changed pixel into four kinds:
  CURSE LIGHT     brightened (luma +12) and VIOLET (blue over green by 30, red over green by 10): the curse's own light
  HOST DRAINED    the host's own material changed by a territory: its colour drained (chroma down by 18 or more), or a
                  dark host's flesh turned to ash (brightened, grey, below luma 150)
  HOST DARKENED   darkened (luma -12): the infected tissue and the vein fragments
  UNRELATED       brightened, not violet and not ash: bright edges the curse's shudder moved, smoke lifted by light
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from brand_cut_evidence import CLOSE, TRANSFER, anchor_at, frame_at  # noqa: E402
from brand_evidence import ROW, other_light, trace  # noqa: E402
from brand_fix_evidence import bodies, context_crop, quiet, tiles, whole_creature  # noqa: E402
from foundation_evidence import REPO, film, read  # noqa: E402

BOSSES = [("void_reaper", "VOID REAPER"), ("crystal_lich", "CRYSTAL LICH"), ("spirit_matron", "SPIRIT MATRON"),
          ("forge_colossus", "FORGE COLOSSUS"), ("lumen_angel", "LUMEN ANGEL"), ("thorn_regent", "THORN REGENT")]
REFERENCE_PEAKS = "SPRAY 2761 / 805, HARD HANDS 1382 / 612, JAWS 6957 / 5327, PRESS 749-867 (px above luma 170 / 210, ADR-011)"
SMALL = {"etch_clean": (400, 260), "light_etch": (400, 260), "fam_nature_bruiser": (420, 380)}
SMALL_TIMES = {1: 3300, 3: 6450}


def luma(a):
    return 0.299 * a[..., 0] + 0.587 * a[..., 1] + 0.114 * a[..., 2]


def chroma(a):
    return a.max(axis=-1) - a.min(axis=-1)


def sort_pixels(A, B):
    """The four kinds of change between a frame with the curse (A) and without (B), as boolean masks, and A's luma."""
    la, lb = luma(A), luma(B)
    brighter = (la - lb) > 12
    violet = (A[..., 2] > A[..., 1] + 30) & (A[..., 0] > A[..., 1] + 10)
    light = brighter & violet
    ash = brighter & ~violet & (chroma(A) < 32) & (la < 150)
    drained = ((chroma(B) - chroma(A)) >= 18) & ~light | ash
    darker = (lb - la) > 12
    other = brighter & ~violet & ~ash
    return light, drained, darker, other, la


def pairs(after, before, lo, hi, box_of=None, row=ROW):
    """Per matched frame: playhead; in the creature ROW the curse light px > 170 / > 210 and unrelated px > 170; in the
    host's body box the curse light, drained, darkened and unrelated px, the union the curse changed, the box area."""
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
        light, drained, darker, other, la = sort_pixels(A[y0:y1, x0:x1], B[y0:y1, x0:x1])
        rec = [ph, int((light & (la > 170)).sum()), int((light & (la > 210)).sum()), int((other & (la > 170)).sum())]
        box = box_of(after, ph) if box_of else None
        if box:
            bx, by, bw, bh = box
            l2, d2, k2, o2, _ = sort_pixels(A[by:by + bh, bx:bx + bw], B[by:by + bh, bx:bx + bw])
            rec += [int(l2.sum()), int(d2.sum()), int(k2.sum()), int(o2.sum()), int((l2 | d2 | k2).sum()), bw * bh]
        else:
            rec += [0, 0, 0, 0, 0, 0]
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


HEAD = ("| moment | window (ms) | frames | CURSE LIGHT peak px > 170 / 210 (row) | UNRELATED peak px > 170 | curse light px "
        "(mean) | host DRAINED px (mean) | host DARKENED px (mean) | share of the host's box the curse changed (mean) | other light in the window |")


def row_of(name, lo, hi, rows, other):
    share = [r[8] / r[9] for r in rows if r[9]]
    m = lambda k: sum(r[k] for r in rows) / len(rows)   # noqa: E731
    return (f"| {name} | {lo}-{hi} | {len(rows)} | {max(r[1] for r in rows)} / {max(r[2] for r in rows)} | {max(r[3] for r in rows)} | "
            f"{m(4):.0f} | {m(5):.0f} | {m(6):.0f} | {(sum(share) / len(share) if share else 0):.1%} | {other} |")


def measures(n, scale_dir, path):
    dump = lambda name: os.path.join(scale_dir, f"{name}.png.actors.txt")   # noqa: E731
    L = ["# BRAND, Concept A territory pass, in numbers", "", __doc__.split("THE MEASURES", 1)[1].strip(), "",
         "## The black whelps (ETCH), the reference fight", "", HEAD, "|" + "---|" * 10]
    box = host_box(dump("default"))
    for after, before, name, lo, hi in (("apply", "apply_off", "APPLY (the first territory blooms)", 1750, 2400),
                                        ("clean_tick", "clean_tick_off", "IDLE, depth 1 (settled)", 3600, 3990),
                                        ("clean_tick", "clean_tick_off", "DEEPEN 1 -> 2 (a second territory)", 3990, 4700),
                                        ("clean_tick", "clean_tick_off", "IDLE, depth 2", 4700, 4850),
                                        ("clean_tick6", "clean_tick6_off", "DEEPEN 2 -> 3 (more territories)", 5990, 6700),
                                        ("clean_tick6", "clean_tick6_off", "depth 3, then its host FALLS at 6717", 6700, 6850)):
        if os.path.exists(n(after) + ".log") and os.path.exists(n(before) + ".log"):
            rows = pairs(n(after), n(before), lo, hi, box)
            if rows:
                L.append(row_of(name, lo, hi, rows, other_light(n(after), lo, hi)))
    for key, label in BOSSES:
        pre, off = n(f"boss_{key}"), n(f"boss_{key}_off")
        if not (os.path.exists(pre + ".log") and os.path.exists(off + ".log")):
            continue
        L += ["", f"## {label} (one host that never falls, ETCH)", "", HEAD, "|" + "---|" * 10]
        box = host_box(dump(f"boss_{key}"))
        for name, lo, hi in (("APPLY", 1750, 2450), ("IDLE, depth 1 (settled)", 3000, 3950), ("DEEPEN 1 -> 2", 3990, 4800),
                             ("IDLE, depth 2", 4850, 5950), ("DEEPEN 2 -> 3", 5990, 6800), ("IDLE, depth 3", 6850, 7900)):
            rows = pairs(pre, off, lo, hi, box)
            if rows:
                L.append(row_of(name, lo, hi, rows, other_light(pre, lo, hi)))
    L += ["", f"The accepted references' peaks: {REFERENCE_PEAKS}.", "",
          "## Draw counts and allocation (mark-draw trace, every frame the curse draws)", ""]
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
                 f"{sum(1 for a in alloc if a == 0)} of {len(alloc)} frames allocate 0 bytes (the prototype's read-backs "
                 f"build each host's drained copy and seats once per strip)")
    open(path, "w", encoding="utf-8").write("\n".join(L) + "\n")
    print(os.path.basename(path))


def depth_one_table(n, scale_dir, path):
    """DEPTH 1 AT REST on every host: how much of the host the one infected territory changes (its light, the host's
    drained and darkened material), at play size, paired with the same frames of the fight with the curse off."""
    dump = lambda name: os.path.join(scale_dir, f"{name}.png.actors.txt")   # noqa: E731
    L = ["| host | body box (px) | curse light px | host drained px | host darkened px | share of the box the curse changed | frames |",
         "|---|---|---|---|---|---|---|"]
    hosts = [("black whelp", n("etch_clean"), n("etch_clean_off"), "default", 3000, 3990),
             ("pale Choir wisp", n("light_etch"), n("light_etch_off"), "Spirit_Swarm", 3000, 3990),
             ("Verdant bruiser", n("fam_nature_bruiser"), n("fam_nature_bruiser_off"), "Body_Bruiser", 3000, 3990)]
    hosts += [(label, n(f"boss_{k}"), n(f"boss_{k}_off"), f"boss_{k}", 3000, 3950) for k, label in BOSSES]
    for label, pre, off, d, lo, hi in hosts:
        if not (os.path.exists(pre + ".log") and os.path.exists(off + ".log")):
            continue
        rows = [r for r in pairs(pre, off, lo, hi, host_box(dump(d))) if r[9]]
        if not rows:
            continue
        m = lambda k: sum(r[k] for r in rows) / len(rows)   # noqa: E731
        L.append(f"| {label} | {rows[0][9]} | {m(4):.0f} | {m(5):.0f} | {m(6):.0f} | {m(8) / rows[0][9]:.1%} | {len(rows)} |")
    open(path, "w", encoding="utf-8").write("\n".join(L) + "\n")
    print(os.path.basename(path))


def same_crop(pre, off, ms, dump, pad=24):
    """The twin (no curse) at the same playhead, cropped to the SAME rectangle as the cursed frame's whole creature."""
    a = anchor_at(pre, frame_at(pre, ms)[1])
    rects = bodies(dump)
    hold = [r for r in rects if a and r[0] <= a[0] <= r[0] + r[2] and r[1] <= a[1] <= r[1] + r[3]]
    x, y, w, h = min(hold, key=lambda r: abs(r[0] + r[2] / 2 - a[0])) if hold else (1000, 560, 400, 400)
    i, _ = frame_at(off, ms)
    return Image.open(f"{off}_{i:02d}.png").convert("RGB").crop((x - pad, y - pad, x + w + pad, y + h + pad))


def off_d1_d3(n, key, label, dump):
    """OFF / depth 1 / depth 3 of one host, whole, at play size: the uncursed twin at depth 1's very moment."""
    pre, off = n(key), n(key + "_off")
    pics = []
    if key in SMALL:
        w, h = SMALL[key]
        t1, t3 = SMALL_TIMES[1], SMALL_TIMES[3]
        if os.path.exists(off + ".log"):
            pics.append((f"{label}, NO curse ({t1} ms)", context_crop(off, t1, w, h)[0]))
        for k, t in ((1, t1), (3, t3)):
            im, ph = context_crop(pre, t, w, h)
            pics.append((f"{label}, depth {k} ({ph:.0f} ms)", im))
        return pics
    s1, m1 = quiet(pre, (1,), dump(key), after=1300)
    s3, m3 = quiet(pre, (3,), dump(key), after=1300)
    if m1 is not None and os.path.exists(off + ".log"):
        pics.append((f"{label}, NO curse ({m1:.0f} ms)", same_crop(pre, off, m1, dump(key))))
    for k, ms in ((1, m1), (3, m3)):
        if ms is not None:
            im, ph, _ = whole_creature(pre, ms, dump(key))
            pics.append((f"{label}, depth {k} ({ph:.0f} ms)", im))
    return pics


def main():
    tag, before, scale_dir, out = sys.argv[1], sys.argv[2], sys.argv[3], os.path.abspath(sys.argv[4])
    os.makedirs(out, exist_ok=True)
    stills_only = os.environ.get("BRAND_STILLS_ONLY") == "1"
    for f in os.listdir(out):
        if f[:2].isdigit() and f[:2] != "14" and f.endswith((".png", ".md") if stills_only else (".mp4", ".png", ".md")):
            os.remove(os.path.join(out, f))
    if stills_only:
        global film
        film = lambda *a, **k: None   # noqa: E731
    shots = os.path.join(REPO, "build", "shots", "brand")
    n = lambda take: os.path.join(shots, tag, take)       # noqa: E731
    o = lambda take: os.path.join(shots, before, take)    # noqa: E731
    dump = lambda name: os.path.join(scale_dir, f"{name}.png.actors.txt")   # noqa: E731
    # 1 the Crystal Lich at depth 3: 9aa407fd (the path along its collar) vs this pass
    pics = []
    for label, pre in (("9aa407fd", o("boss_crystal_lich")), ("this pass", n("boss_crystal_lich"))):
        s, ms = quiet(pre, (3,), dump("boss_crystal_lich"), after=1300)
        if ms is not None:
            im, ph, _ = whole_creature(pre, ms, dump("boss_crystal_lich"))
            pics.append((f"CRYSTAL LICH depth 3, {label} ({ph:.0f} ms)", im))
    tiles(pics, f"{out}/01_crystal_lich_depth3_before_after.png", "CRYSTAL LICH AT DEPTH 3: 9aa407fd (left) vs this pass (right), whole, play size", per_row=2, cap=700)
    # 2-6 the hard hosts, OFF / depth 1 / depth 3
    for num, key, label in (("02", "boss_crystal_lich", "CRYSTAL LICH"), ("03", "boss_void_reaper", "VOID REAPER"),
                            ("04", "boss_spirit_matron", "SPIRIT MATRON"), ("05", "etch_clean", "black whelp"),
                            ("06", "light_etch", "pale Choir wisp")):
        pics = off_d1_d3(n, key, label, dump)
        if pics:
            tiles(pics, f"{out}/{num}_{label.lower().replace(' ', '_')}_off_d1_d3.png",
                  f"{label}: NO curse / depth 1 / depth 3, play size", per_row=3, cap=700)
    # 7 the other hosts, OFF / depth 1 / depth 3
    pics = []
    for key, label in (("boss_forge_colossus", "FORGE COLOSSUS"), ("boss_lumen_angel", "LUMEN ANGEL"),
                       ("boss_thorn_regent", "THORN REGENT"), ("fam_nature_bruiser", "Verdant bruiser")):
        pics += off_d1_d3(n, key, label, dump)
    tiles(pics, f"{out}/07_other_hosts_off_d1_d3.png", "THE OTHER HOSTS: NO curse / depth 1 / depth 3, play size", per_row=3, cap=700)
    # 8-13 at true speed
    film(n("apply"), f"{out}/08_apply.mp4", "APPLY: the first territory blooms, true speed (close)", crop=CLOSE)
    film(n("apply"), f"{out}/08b_apply_arena.mp4", "APPLY, true speed (the whole arena)")
    film(n("apply"), f"{out}/08c_apply_slow.mp4", "APPLY, x4 slower (close)", "--mute", "--slow", "4", crop=CLOSE)
    film(n("etch_clean"), f"{out}/09_deepen.mp4", "DEEPEN 1 -> 2 (4 s) -> 3 (6 s): a new territory each time, true speed")
    film(n("etch_clean"), f"{out}/09b_deepen_close.mp4", "the deepens, close, true speed", lo=3600, hi=7000, crop=CLOSE)
    film(n("boss_crystal_lich"), f"{out}/09c_deepen_lich.mp4", "the deepens on the Crystal Lich, true speed", lo=1500, hi=8200)
    film(n("etch_clean"), f"{out}/09d_deepen_slow.mp4", "the deepens, close, x3 slower", "--mute", "--slow", "3", lo=3900, hi=7000, crop=CLOSE)
    film(n("long"), f"{out}/10_idle.mp4", "IDLE, depth 1, true speed (close): wisps leave the territory", "--mute", lo=2400, hi=6400, crop=CLOSE)
    film(n("boss_void_reaper"), f"{out}/10b_idle_reaper.mp4", "IDLE on the Void Reaper (depth 1, then deeper), true speed", "--mute", lo=2300, hi=7900)
    film(n("boss_spirit_matron"), f"{out}/10c_idle_matron.mp4", "IDLE on the Spirit Matron, true speed", "--mute", lo=2300, hi=7900)
    film(n("sprawl"), f"{out}/11_sprawl.mp4", "SPRAWL: the source's territories wake, the row is infected in turn, true speed")
    if os.path.exists(n("sprawl_winnow_clean") + ".log"):
        film(n("sprawl_winnow_clean"), f"{out}/11b_sprawl_deepen.mp4", "SPRAWL + WINNOW: the row deepens, true speed")
    film(n("long"), f"{out}/12_transfer.mp4", "TRANSFER: the front falls at 6.7 s and 8.2 s, true speed", lo=6000, hi=9400)
    film(n("long"), f"{out}/12b_transfer_close.mp4", "the transfer, close on the new front, true speed", lo=6600, hi=9300, crop=TRANSFER)
    film(n("press"), f"{out}/13a_with_press.mp4", "with PRESS (and SPRAY), true speed")
    film(n("fast"), f"{out}/13b_with_spray_hard_hands.mp4", "with SPRAY and HARD HANDS (fast TEMPO), true speed")
    film(n("etch"), f"{out}/13c_with_jaws.mp4", "with JAWS (and SPRAY), true speed", lo=3500, hi=6600)
    if os.path.exists(n("fam_nature_bruiser") + ".log"):
        film(n("fam_nature_bruiser"), f"{out}/13d_bruiser.mp4", "the Verdant bruiser, true speed")
    measures(n, scale_dir, f"{out}/15_measures.md")
    depth_one_table(n, scale_dir, f"{out}/16_depth1_every_host.md")
    print("done ->", out)


if __name__ == "__main__":
    main()
