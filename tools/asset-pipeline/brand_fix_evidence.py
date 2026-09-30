#!/usr/bin/env python3
"""brand_fix_evidence.py -- BRAND's final VISUAL CORRECTION (ADR-011, 2026-10-01): DEPTH 3 torn so it cannot read as a
letter / number / rune / eye, and the mark's scale capped (MarkRecipe.MaxScale 1, measured). The owner's focused list,
in the owner's order, from the takes films_brand.sh filmed (this pass) beside 97efe379's (the second pass).

    PYTHONUTF8=1 python tools/asset-pipeline/brand_fix_evidence.py <new tag> <97efe379 tag> <scale shots dir> <out dir>

 1 97efe379's DEPTH 3 vs the new one, same host, play size    2 the three depths at play size    3 the three depths close
 4 DEPTH 3 on a dark host    5 DEPTH 3 on a pale host    6 the large Verdant bruiser, 97efe379 vs now, whole creature
 7 every family    8 the six bosses, whole creatures (DEPTH 1 and DEPTH 3)    9 the scale table    10 apply    11 deepen
12 transfer    13 SPRAWL    14 beside PRESS / SPRAY / HARD HANDS / JAWS    15 tests and allocation
"""
import os
import subprocess
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from brand_cut_evidence import CLOSE, TRANSFER, anchor_at, comparison, frame_at, measures, settled  # noqa: E402
from brand_evidence import trace  # noqa: E402
from foundation_evidence import REPO, SMALL, film, read  # noqa: E402

GROUND = (20, 17, 14)
GOLD, NOTE = (235, 200, 120), (200, 180, 150)
FAMILIES = [("default", None, "Umbral Reach Swarm (the dark whelp)"), ("light", "Spirit_Swarm", "The Pale Choir (pale host)"),
            ("fam_mind_swarm", "Mind_Swarm", "The Still Archive Swarm"), ("fam_shadow_caster", "Shadow_Caster", "Umbral Reach Caster"),
            ("fam_spirit_caster", "Spirit_Caster", "The Pale Choir Caster"), ("fam_machine_armoured", "Machine_Armoured", "Cinderworks Armoured"),
            ("fam_body_armoured", "Body_Armoured", "Marrow Wastes Armoured"), ("fam_nature_bruiser", "Nature_Bruiser", "Verdant Hollow Bruiser")]
BOSSES = ["thorn_regent", "forge_colossus", "void_reaper", "spirit_matron", "crystal_lich", "lumen_angel"]


def bodies(dump):
    out = []
    for line in open(dump, encoding="utf-8"):
        if line.startswith("Creature/"):
            out.append(tuple(map(int, line.split("\t")[1].split()[1].split(","))))
    return out


def deepest(pre, prefer=(3, 2, 1), after=300):
    for s in prefer:
        t = settled(pre, s, after=after, before_fall=True)
        if t is not None:
            return s, t
    return None, None


def quiet(pre, prefer, dump, after=200):
    """The settled frame of the deepest wanted depth whose whole-creature crop carries the LEAST foreign light (a boss's
    own swing, a hit's flash): the mark itself must be seen. Returns (depth, playhead) or (None, None)."""
    import numpy as np
    rows = [(r[0], int(r[1]["stage"])) for r in trace(pre, "mark-draw") if "stage" in r[1]]
    shots = read(pre + ".log")
    for s in prefer:
        since, cands = None, []
        for t, st in rows:
            since = (t if since is None else since) if st == s else None
            if since is not None and t - since >= after:
                cands.append(t)
        if not cands:
            continue
        best = None
        for t in cands[::3]:
            im, _, a = whole_creature(pre, t, dump)
            if a is None or a[2] != s:
                continue
            lum = np.asarray(im.convert("L"))
            score = int((lum > 200).sum())
            if best is None or score < best[0]:
                best = (score, t)
        if best:
            return s, best[1]
    return None, None


def whole_creature(pre, ms, dump, pad=24):
    """The frame nearest ms, cropped to the WHOLE body of the creature the mark is on (the dump's body that holds the
    mark's anchor), at play size."""
    i, ph = frame_at(pre, ms)
    a = anchor_at(pre, ph)
    img = Image.open(f"{pre}_{i:02d}.png").convert("RGB")
    rects = bodies(dump) if dump and os.path.exists(dump) else []
    hold = [r for r in rects if a and r[0] <= a[0] <= r[0] + r[2] and r[1] <= a[1] <= r[1] + r[3]]
    if hold:
        x, y, w, h = min(hold, key=lambda r: abs(r[0] + r[2] / 2 - a[0]))
    else:
        x, y, w, h = (a[0] - 170, a[1] - 200, 340, 330) if a else (1000, 560, 400, 400)
    return img.crop((x - pad, y - pad, x + w + pad, y + h + pad)), ph, a


def tiles(items, path, title, per_row=4, cap=440):
    """items: [(label, PIL image)] -> one sheet at play size (only images wider than `cap` are shrunk, and then said so)."""
    ims = []
    for label, im in items:
        k = min(1.0, cap / im.width)
        ims.append((label + ("" if k == 1.0 else f"  (shown at {k:.0%})"), im if k == 1.0 else im.resize((int(im.width * k), int(im.height * k)), Image.LANCZOS)))
    rows = [ims[i:i + per_row] for i in range(0, len(ims), per_row)]
    cw = max(im.width for _, im in ims) + 12
    rh = [max(im.height for _, im in r) + 34 for r in rows]
    sheet = Image.new("RGB", (12 + per_row * cw, 36 + sum(rh)), GROUND)
    d = ImageDraw.Draw(sheet)
    d.text((12, 8), title, fill=GOLD, font=SMALL)
    y = 30
    for r, h in zip(rows, rh):
        for c, (label, im) in enumerate(r):
            d.text((12 + c * cw, y), label, fill=NOTE, font=SMALL)
            sheet.paste(im, (12 + c * cw, y + 18))
        y += h
    sheet.save(path)
    print(os.path.basename(path))


def context_crop(pre, ms, w=480, h=300, zoom=1):
    i, ph = frame_at(pre, ms)
    a = anchor_at(pre, ph)
    img = Image.open(f"{pre}_{i:02d}.png").convert("RGB")
    x, y = (a[0], a[1]) if a else (1200, 780)
    c = img.crop((x - w // 2, y - h // 2, x + w // 2, y + h // 2))
    return (c if zoom == 1 else c.resize((w * zoom, h * zoom), Image.NEAREST)), ph


def main():
    tag, old, scale_dir, out = sys.argv[1], sys.argv[2], sys.argv[3], os.path.abspath(sys.argv[4])
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f[:2].isdigit() and f.endswith((".mp4", ".png", ".md")):
            os.remove(os.path.join(out, f))
    shots = os.path.join(REPO, "build", "shots", "brand")
    n = lambda take: os.path.join(shots, tag, take)          # noqa: E731
    o = lambda take: os.path.join(shots, old, take)          # noqa: E731
    dump = lambda name: os.path.join(scale_dir, f"{name}.png.actors.txt")   # noqa: E731
    d1, d2, d3 = 3300, 4900, 9900                            # etch_clean: each depth settled on its host (brand_cut_evidence)

    # 1 the old DEPTH 3 vs the new, the same whelp at the same moment, play size (1x) beside a 3x
    comparison([("97efe379 (second pass)", o("etch_clean")), ("THIS PASS", n("etch_clean"))], [("DEPTH 3", d3)],
               f"{out}/01_depth3_before_after.png", "DEPTH 3: 97efe379 vs this pass, the same host, the same moment (1x = play size)", zoom=3)
    # 2 the three depths at play size, in context
    tiles([(f"DEPTH {k} ({context_crop(n('etch_clean'), t)[1]:.0f} ms)", context_crop(n("etch_clean"), t)[0]) for k, t in ((1, d1), (2, d2), (3, d3))],
          f"{out}/02_three_depths_play_size.png", "THE THREE DEPTHS AT PLAY SIZE (1x, the fight's own pixels; ETCH 120 / 170 / 220+ %)", per_row=3, cap=480)
    # 3 the three depths close
    comparison([("this pass", n("etch_clean"))], [(f"DEPTH {k}", t) for k, t in ((1, d1), (2, d2), (3, d3))],
               f"{out}/03_three_depths_close.png", "THE THREE DEPTHS, CLOSE (1x beside 4x)", zoom=4)
    # 4 / 5 DEPTH 3 on a dark and on a pale host
    for num, take, what in (("04", "etch_clean", "a DARK host (the whelp)"), ("05", "light_etch", "a PALE host (the Pale Choir)")):
        s, t = deepest(n(take), prefer=(3,))
        if t is None:
            s, t = deepest(n(take))
        big, ph = context_crop(n(take), t)
        tiles([(f"play size, DEPTH {s} ({ph:.0f} ms)", big), ("x3", context_crop(n(take), t, 160, 110, 3)[0])],
              f"{out}/{num}_depth3_{'dark' if num == '04' else 'pale'}_host.png", f"DEPTH {s} ON {what.upper()}", per_row=2, cap=480)
    # 6 the large Verdant bruiser, whole creature: 97efe379 (scale 2.33) vs now (capped at 1)
    pics = []
    for label, pre in (("97efe379: scale 2.33", o("fam_nature_bruiser")), ("THIS PASS: scale 1", n("fam_nature_bruiser"))):
        s, t = quiet(pre, (3, 2, 1), dump("Nature_Bruiser"))
        if t is not None:
            im, ph, _ = whole_creature(pre, t, dump("Nature_Bruiser"))
            pics.append((f"{label}, DEPTH {s} ({ph:.0f} ms)", im))
    tiles(pics, f"{out}/06_bruiser_before_after.png", "THE LARGE VERDANT BRUISER, WHOLE CREATURE, PLAY SIZE: 97efe379 vs this pass", per_row=2, cap=520)
    # 7 every family, whole creature, the deepest settled depth each take reached
    pics = []
    for take, name, label in FAMILIES:
        pre = n("etch_clean") if take == "default" else n("light_etch") if take == "light" else n(take)
        if not os.path.exists(pre + ".log"):
            continue
        s, t = quiet(pre, (3, 2, 1), dump(name or "default"))
        if t is None:
            continue
        im, ph, _ = whole_creature(pre, t, dump(name or "default"))
        pics.append((f"{label}, DEPTH {s}", im))
    tiles(pics, f"{out}/07_families.png", "THE MARK ON EVERY FAMILY, WHOLE CREATURES, PLAY SIZE (ETCH, the deepest depth each fight reached)", per_row=4, cap=420)
    # 8 the six bosses, whole creatures: DEPTH 1 (still readable at the cap?) and the deepest
    pics = []
    for b in BOSSES:
        pre = n(f"boss_{b}")
        if not os.path.exists(pre + ".log"):
            continue
        for want in ((1,), (3,)):
            s, t = quiet(pre, want, dump(f"boss_{b}"))
            if t is not None:
                im, ph, _ = whole_creature(pre, t, dump(f"boss_{b}"))
                pics.append((f"{b.replace('_', ' ').upper()}, DEPTH {s}", im))
    tiles(pics, f"{out}/08_bosses.png", "THE SIX BOSSES, WHOLE CREATURES, PLAY SIZE: DEPTH 1 and DEPTH 3", per_row=2, cap=600)
    # 9 the scale table (measured)
    table = subprocess.run([sys.executable, os.path.join(REPO, "tools", "asset-pipeline", "brand_scale_table.py"), scale_dir, "1", "--md"],
                           capture_output=True, text=True, cwd=REPO).stdout
    before = subprocess.run([sys.executable, os.path.join(REPO, "tools", "asset-pipeline", "brand_scale_table.py"), scale_dir, "3", "--md"],
                            capture_output=True, text=True, cwd=REPO).stdout
    open(f"{out}/09_scale_table.md", "w", encoding="utf-8").write(
        "# BRAND's size on every host, measured\n\nThe front creature's visible body from the game's geometry dump "
        "(RH_SHOT_DUMP, one fight per host, tools/asset-pipeline/brand_scale_shots.sh); the mark's visible extent (cut and "
        "stain, alpha > 0.1) from the atlas's idle cell of each depth, at the scale `ScaleFor` snaps to. The whelp is "
        "the reference: 1.00 before and after.\n\n## Now (MaxScale 1)\n\n" + table + "\n## Before (97efe379, MaxScale 3)\n\n" + before)
    print("09_scale_table.md")
    # 10-13 at true speed
    film(n("apply"), f"{out}/10_apply_true_speed.mp4", "APPLY, true speed", crop=CLOSE)
    film(n("etch_clean"), f"{out}/11_deepen_true_speed.mp4", "DEEPEN 1 -> 2 (4 s) -> 3 (6 s), ETCH, true speed")
    film(n("etch_clean"), f"{out}/11b_deepen_close_true_speed.mp4", "the deepens, close, true speed", lo=3600, hi=7000, crop=CLOSE)
    film(n("long"), f"{out}/12_transfer_true_speed.mp4", "TRANSFER: the front dies at 6.7 s and 8.2 s, true speed", lo=6000, hi=9400)
    film(n("long"), f"{out}/12b_transfer_close_true_speed.mp4", "the transfer, close on the new front, true speed", lo=6600, hi=9300, crop=TRANSFER)
    film(n("sprawl"), f"{out}/13_sprawl_true_speed.mp4", "SPRAWL: the mark propagates from its source, true speed")
    if os.path.exists(n("sprawl_winnow_clean") + ".log"):
        film(n("sprawl_winnow_clean"), f"{out}/13b_sprawl_deepen_true_speed.mp4", "SPRAWL + WINNOW: the row deepens, true speed")
    # 14 mixed combat
    film(n("press"), f"{out}/14a_with_press.mp4", "with PRESS: the crush and the cut on one body")
    film(n("fast"), f"{out}/14b_with_spray_hard_hands.mp4", "with SPRAY and HARD HANDS (fast TEMPO)")
    film(n("etch"), f"{out}/14c_with_jaws.mp4", "with JAWS: the bite on the branded attacker", lo=3500, hi=6600)
    # 15 the measures and the allocation (the tests are added by hand from the run)
    measures(n, f"{out}/15_measures.md")
    print("done ->", out)


if __name__ == "__main__":
    main()
