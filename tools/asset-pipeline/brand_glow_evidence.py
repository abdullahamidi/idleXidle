#!/usr/bin/env python3
"""brand_glow_evidence.py -- BRAND, Concept A LIVING SHADOW CORRUPTION, the glow pass (ADR-011, owner's brief 2026-10-01):
the owner's focused list from the takes films_brand.sh filmed with RH_BRAND_CONCEPT=A (this pass) beside 8a10dae1's
Concept A takes (before), and the measures (added light, dark-host readability, coverage).

    PYTHONUTF8=1 python tools/asset-pipeline/brand_glow_evidence.py <new tag> <before tag> <scale shots dir> <out dir>

The *_off twins (the same seeded fight with the mark drawn off) are copied into <new tag> from an earlier pass.
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from brand_cut_evidence import CLOSE, TRANSFER, anchor_at, frame_at, settled  # noqa: E402
from brand_evidence import ROW, other_light, trace  # noqa: E402
from brand_fix_evidence import bodies, context_crop, quiet, tiles, whole_creature  # noqa: E402
from foundation_evidence import REPO, film, read  # noqa: E402

DARK_BOSSES = [("void_reaper", "VOID REAPER"), ("forge_colossus", "FORGE COLOSSUS"), ("thorn_regent", "THORN REGENT")]
LIGHT_BOSSES = [("lumen_angel", "LUMEN ANGEL"), ("crystal_lich", "CRYSTAL LICH"), ("spirit_matron", "SPIRIT MATRON")]
REFERENCE_PEAKS = "SPRAY 2761 / 805, HARD HANDS 1382 / 612, JAWS 6957 / 5327, PRESS 749-867 (px above luma 170 / 210, ADR-011)"


def luma(a):
    return 0.299 * a[..., 0] + 0.587 * a[..., 1] + 0.114 * a[..., 2]


def pairs(after, before, lo, hi, box_of):
    """Per matched frame: (playhead, px > 170 added, px > 210 added, glow px (luma +30) in the host's body, changed px in
    the body (any |luma| > 12), body area)."""
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
        la, lb = luma(A), luma(B)
        add = (la - lb) > 12
        x0, y0, x1, y1 = ROW   # the creature row, as every ADR-011 reference was measured (the champion's poses excluded)
        ra, radd = la[y0:y1, x0:x1], add[y0:y1, x0:x1]
        # THE CURSE'S OWN LIGHT: added pixels that are VIOLET (blue over green, red over green). The raw count also takes
        # bright edges the curse's shudder MOVES (a boss's scythe) and smoke over the glow, which add no light of the curse's
        Ar = A[y0:y1, x0:x1]
        violet = (Ar[..., 2] > Ar[..., 1] + 30) & (Ar[..., 0] > Ar[..., 1] + 10)
        row = (int(((ra > 170) & radd).sum()), int(((ra > 210) & radd).sum()),
               int(((ra > 170) & radd & violet).sum()), int(((ra > 210) & radd & violet).sum()))
        box = box_of(after, ph)
        if box:
            x, y, w2, h2 = box
            d = (la - lb)[y:y + h2, x:x + w2]
            glow, changed, area = int((d > 30).sum()), int((np.abs(d) > 12).sum()), w2 * h2
        else:
            glow = changed = area = 0
        out.append((ph,) + row[2:] + (glow, changed, area) + row[:2])
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


def stage_at(pre, ph):
    a = anchor_at(pre, ph)
    return a[2] if a else -1


def measures(n, dump, path):
    box_of = host_box(dump)
    L = ["# BRAND, Concept A glow pass, in numbers", "",
         "Every row pairs a frame with the SAME frame of the same seeded fight with the mark drawn off (the `_off` twins).",
         "`violet px > 170 / 210`: VIOLET pixels the curse pushes above that luma, its own light. The raw count (the measure every",
         "ADR-011 reference was held to) is kept in its own column: it also counts bright edges the curse's shudder moves (a boss's",
         "scythe) and white death smoke lifted by the glow under it.",
         "`glow px`: pixels in the host's body box the curse brightens by more than 30 luma (a violet vein on a black body is",
         "rarely above luma 170, so this is the dark-host readability measure). `coverage`: body-box pixels it changes at all",
         "(|luma| > 12, darkening included) as a share of the box.", "",
         "| moment | window (ms) | frames | peak violet px > 170 | peak violet px > 210 | mean glow px | peak glow px | mean coverage | raw peak px > 170 / 210 (incl. moved edges, smoke) | other light in the window |",
         "|---|---|---|---|---|---|---|---|---|---|"]
    windows = (("apply", "apply_off", "APPLY (the growth and its front)", 1750, 2400),
               ("apply", "apply_off", "IDLE, depth 1 (after the apply settles)", 2500, 2800),
               ("clean_tick", "clean_tick_off", "DEEPEN 1 -> 2 (the 4000 tick)", 3990, 4700),
               ("clean_tick", "clean_tick_off", "IDLE, depth 2 (settled)", 4700, 4850),
               ("clean_tick6", "clean_tick6_off", "IDLE, depth 2 (before the 6000 tick)", 5600, 5990),
               ("clean_tick6", "clean_tick6_off", "DEEPEN 2 -> 3 (the 6000 tick)", 5990, 6700),
               ("clean_tick6", "clean_tick6_off", "depth 3, then its host FALLS at 6717 (the curse brightens and collapses)", 6700, 6850))
    for after, before, name, lo, hi in windows:
        if not (os.path.exists(n(after) + ".log") and os.path.exists(n(before) + ".log")):
            continue
        rows = pairs(n(after), n(before), lo, hi, box_of)
        if not rows:
            continue
        cov = [r[4] / r[5] for r in rows if r[5]]
        L.append(f"| {name} | {lo}-{hi} | {len(rows)} | {max(r[1] for r in rows)} | {max(r[2] for r in rows)} | "
                 f"{sum(r[3] for r in rows) / len(rows):.0f} | {max(r[3] for r in rows)} | "
                 f"{(sum(cov) / len(cov) if cov else 0):.1%} | {max(r[6] for r in rows)} / {max(r[7] for r in rows)} | {other_light(n(after), lo, hi)} |")
    # ONE HOST THAT NEVER FALLS (the Void Reaper, 6000 health): apply, both deepens and every idle depth with no transfer
    boss = n("boss_void_reaper")
    if os.path.exists(boss + ".log") and os.path.exists(n("boss_void_reaper_off") + ".log"):
        L += ["", "### One host that never falls: the Void Reaper (a dark boss), ETCH", "",
              "| moment | window (ms) | frames | peak violet px > 170 | peak violet px > 210 | mean glow px | peak glow px | mean coverage | raw peak px > 170 / 210 (incl. moved edges, smoke) | other light in the window |",
              "|---|---|---|---|---|---|---|---|---|---|"]
        box_boss = host_box(os.path.join(os.path.dirname(dump), "boss_void_reaper.png.actors.txt"))
        for name, lo, hi in (("APPLY", 1750, 2450), ("IDLE, depth 1", 2500, 3950), ("DEEPEN 1 -> 2", 3990, 4800),
                             ("IDLE, depth 2", 4850, 5950), ("DEEPEN 2 -> 3", 5990, 6800), ("IDLE, depth 3", 6850, 7900)):
            rows = pairs(boss, n("boss_void_reaper_off"), lo, hi, box_boss)
            if not rows:
                continue
            cov = [r[4] / r[5] for r in rows if r[5]]
            L.append(f"| {name} | {lo}-{hi} | {len(rows)} | {max(r[1] for r in rows)} | {max(r[2] for r in rows)} | "
                     f"{sum(r[3] for r in rows) / len(rows):.0f} | {max(r[3] for r in rows)} | "
                     f"{(sum(cov) / len(cov) if cov else 0):.1%} | {max(r[6] for r in rows)} / {max(r[7] for r in rows)} | {other_light(boss, lo, hi)} |")
    L += ["", f"The accepted references' peaks: {REFERENCE_PEAKS}.", "",
          "## Allocation and draw counts (mark-draw trace, every frame the curse draws)", ""]
    for take in ("long", "etch", "etch_clean", "sprawl", "sprawl_winnow_clean", "press", "fast", "light", "light_etch",
                 "fam_nature_bruiser", "fam_shadow_caster") + tuple("boss_" + b for b, _ in DARK_BOSSES + LIGHT_BOSSES):
        if not os.path.exists(n(take) + ".log"):
            continue
        rows = trace(n(take), "mark-draw")
        if not rows:
            continue
        sprites = [int(r[1]["sprites"]) for r in rows]
        alloc = [int(r[1]["alloc"]) for r in rows]
        stages = sorted({int(r[1]["stage"]) for r in rows if int(r[1]["stage"]) >= 0})
        L.append(f"- **{take}**: {len(rows)} frames drawn; sprites per frame {min(sprites)}-{max(sprites)}; depths {stages}; "
                 f"{sum(1 for a in alloc if a == 0)} of {len(alloc)} frames allocate 0 bytes")
    open(path, "w", encoding="utf-8").write("\n".join(L) + "\n")
    print(os.path.basename(path))


def depth_row(pre, times, label):
    pics = []
    for k, ms in zip((1, 2, 3), times):
        if ms is None:
            continue
        im, ph = context_crop(pre, ms, 400, 300)
        pics.append((f"{label}, depth {k} ({ph:.0f} ms), play size", im))
    for k, ms in zip((1, 2, 3), times):
        if ms is None:
            continue
        im, ph = context_crop(pre, ms, 200, 150, 2)
        pics.append((f"depth {k}, x2", im))
    return pics


def boss_sheet(n, dump, bosses, path, title):
    pics = []
    for key, name in bosses:
        pre = n(f"boss_{key}")
        if not os.path.exists(pre + ".log"):
            continue
        for want in ((1,), (2,), (3,)):
            s, ms = quiet(pre, want, dump(f"boss_{key}"), after=750)
            if ms is not None:
                im, ph, _ = whole_creature(pre, ms, dump(f"boss_{key}"))
                pics.append((f"{name}, depth {s} ({ph:.0f} ms)", im))
    tiles(pics, path, title, per_row=3, cap=460)


def main():
    tag, before, scale_dir, out = sys.argv[1], sys.argv[2], sys.argv[3], os.path.abspath(sys.argv[4])
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f[:2].isdigit() and f.endswith((".mp4", ".png", ".md")):
            os.remove(os.path.join(out, f))
    shots = os.path.join(REPO, "build", "shots", "brand")
    n = lambda take: os.path.join(shots, tag, take)       # noqa: E731
    o = lambda take: os.path.join(shots, before, take)    # noqa: E731
    dump = lambda name: os.path.join(scale_dir, f"{name}.png.actors.txt")   # noqa: E731
    dark = (3300, 4883, 9900)
    # 1 BEFORE vs NEW on the same dark enemy, the same moments, play size
    pics = []
    for label, pre in (("8a10dae1 (before)", o("etch_clean")), ("this pass", n("etch_clean"))):
        for k, ms in zip((1, 2, 3), dark):
            im, ph = context_crop(pre, ms, 400, 300)
            pics.append((f"{label}, depth {k} ({ph:.0f} ms)", im))
    tiles(pics, f"{out}/01_before_after_dark.png", "BEFORE (8a10dae1) vs NEW on the same black whelp, the same moments, play size", per_row=3, cap=400)
    # 2 / 3 the depths, dark and pale
    tiles(depth_row(n("etch_clean"), dark, "black whelp"), f"{out}/02_depths_dark.png",
          "NEW, DEPTH 1 / 2 / 3 on a DARK host (top: play size; bottom: x2)", per_row=3, cap=400)
    pale = tuple(settled(n("light_etch"), k, after=500) for k in (1, 2, 3))
    tiles(depth_row(n("light_etch"), pale, "pale Choir wisp"), f"{out}/03_depths_pale.png",
          "NEW, DEPTH 1 / 2 / 3 on a PALE host (top: play size; bottom: x2)", per_row=3, cap=400)
    # 4-11 at true speed
    film(n("apply"), f"{out}/04_apply.mp4", "APPLY, true speed (close)", crop=CLOSE)
    film(n("apply"), f"{out}/04b_apply_arena.mp4", "APPLY, true speed (the whole arena)")
    film(n("apply"), f"{out}/04c_apply_slow.mp4", "APPLY, x4 slower (close)", "--mute", "--slow", "4", crop=CLOSE)
    film(n("etch_clean"), f"{out}/05_deepen.mp4", "DEEPEN 1 -> 2 (4 s) -> 3 (6 s), true speed")
    film(n("etch_clean"), f"{out}/05b_deepen_close.mp4", "the deepens, close, true speed", lo=3600, hi=7000, crop=CLOSE)
    film(n("etch_clean"), f"{out}/05c_deepen_slow.mp4", "the deepens, close, x3 slower", "--mute", "--slow", "3", lo=3900, hi=7000, crop=CLOSE)
    film(n("long"), f"{out}/06_idle.mp4", "IDLE, depth 1, several seconds, true speed (close)", "--mute", lo=2400, hi=6200, crop=CLOSE)
    film(n("light"), f"{out}/06b_idle_pale.mp4", "IDLE on the pale Choir, true speed", "--mute", lo=2400, hi=6200)
    film(n("sprawl"), f"{out}/07_sprawl.mp4", "SPRAWL: the source lights, the row ignites in turn, true speed")
    if os.path.exists(n("sprawl_winnow_clean") + ".log"):
        film(n("sprawl_winnow_clean"), f"{out}/07b_sprawl_deepen.mp4", "SPRAWL + WINNOW: the row deepens, true speed")
    film(n("long"), f"{out}/08_transfer.mp4", "TRANSFER: the front falls at 6.7 s and 8.2 s, true speed", lo=6000, hi=9400)
    film(n("long"), f"{out}/08b_transfer_close.mp4", "the transfer, close on the new front, true speed", lo=6600, hi=9300, crop=TRANSFER)
    film(n("press"), f"{out}/09_with_press.mp4", "with PRESS (and SPRAY), true speed")
    film(n("fast"), f"{out}/10_with_spray_hard_hands.mp4", "with SPRAY and HARD HANDS (fast TEMPO), true speed")
    film(n("etch"), f"{out}/11_with_jaws.mp4", "with JAWS (and SPRAY), true speed", lo=3500, hi=6600)
    # 12 the large Verdant bruiser, 13 / 14 the bosses (whole creatures, play size)
    pics = []
    for want in ((1,), (2,), (3,)):
        s, ms = quiet(n("fam_nature_bruiser"), want, dump("Nature_Bruiser"), after=750)
        if ms is not None:
            im, ph, _ = whole_creature(n("fam_nature_bruiser"), ms, dump("Nature_Bruiser"))
            pics.append((f"Verdant bruiser, depth {s} ({ph:.0f} ms)", im))
    tiles(pics, f"{out}/12_bruiser.png", "THE LARGE VERDANT BRUISER, WHOLE CREATURE, PLAY SIZE", per_row=3, cap=460)
    film(n("fam_nature_bruiser"), f"{out}/12b_bruiser.mp4", "the Verdant bruiser, true speed")
    boss_sheet(n, dump, DARK_BOSSES, f"{out}/13_dark_bosses.png", "DARK BOSSES, WHOLE CREATURES, PLAY SIZE, DEPTH 1 / 2 / 3")
    boss_sheet(n, dump, LIGHT_BOSSES, f"{out}/14_light_bosses.png", "LIGHTER BOSSES, WHOLE CREATURES, PLAY SIZE, DEPTH 1 / 2 / 3")
    film(n("boss_void_reaper"), f"{out}/13b_void_reaper.mp4", "the Void Reaper (dark boss), true speed")
    film(n("boss_lumen_angel"), f"{out}/14b_lumen_angel.mp4", "the Lumen Angel (light boss), true speed")
    measures(n, dump("default"), f"{out}/15_measures.md")
    print("done ->", out)


if __name__ == "__main__":
    main()
