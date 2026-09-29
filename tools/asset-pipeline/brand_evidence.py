#!/usr/bin/env python3
"""brand_evidence.py -- BRAND, the MARK / PERSISTENT TARGET-ATTACHED STATE reference (ADR-011, 2026-09-29): the review
battery for the Etched Shadow Brand, from the takes films_brand.sh filmed.

    bash tools/asset-pipeline/films_brand.sh <tag>
    PYTHONUTF8=1 python tools/asset-pipeline/brand_evidence.py <tag> <out dir>

True speed first (with the fight's sound and muted), then slowed, the variations (ETCH deepening, SPRAWL spreading), the
overlaps with the accepted references (PRESS, SPRAY / HARD HANDS, JAWS), BEFORE / AFTER side by side, frame sheets of
every moment, and the MEASURES: the light the mark ADDS over the same seeded frames without it (px above luma 170 and
210, the method the SPRAY / HARD HANDS / JAWS / PRESS reviews used, committed here as a script), the sprites and bytes
the trace reports, and how still the brand sits on its body.
"""
import os
import shutil
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from foundation_evidence import FONT, REPO, SMALL, film, grid, read, sheet  # noqa: E402

ROW = (880, 520, 1900, 950)          # the creature row: where the mark lives (the champion's old halo is outside it)
WHELP = (940, 600, 1420, 900)        # the front creatures, close
ARENA = "380,410,1500,670"


def luma(a):
    return 0.299 * a[..., 0] + 0.587 * a[..., 1] + 0.114 * a[..., 2]


def added_light(after_pre, before_pre, lo=None, hi=None, box=ROW, tol=9):
    """Per matched frame (same playhead, same seed): pixels the mark ADDS above luma 170 and 210 in the creature row."""
    a_shots, b_shots = read(after_pre + ".log"), read(before_pre + ".log")
    out = []
    b_items = sorted((ph, i) for i, (w, ph) in b_shots.items())
    for i, (w, ph) in sorted(a_shots.items()):
        if (lo is not None and ph < lo) or (hi is not None and ph > hi):
            continue
        j = min(b_items, key=lambda q: abs(q[0] - ph))
        if abs(j[0] - ph) > tol:
            continue
        A = np.asarray(Image.open(f"{after_pre}_{i:02d}.png").convert("RGB").crop(box)).astype(np.float32)
        B = np.asarray(Image.open(f"{before_pre}_{j[1]:02d}.png").convert("RGB").crop(box)).astype(np.float32)
        la, lb = luma(A), luma(B)
        add = (la - lb) > 12
        out.append((ph, int(((la > 170) & add).sum()), int(((la > 210) & add).sum()), int(add.sum())))
    return out


def trace(pre, kind):
    rows = []
    for line in open(pre + ".log", encoding="utf-8", errors="replace"):
        parts = line.rstrip("\n").split("\t")
        if len(parts) > 3 and parts[3] == kind:
            rows.append((float(parts[2]), dict(p.split("=", 1) for p in parts[4:] if "=" in p), parts))
    return rows


def main():
    tag, out = sys.argv[1], os.path.abspath(sys.argv[2])
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f[:2].isdigit() and f.endswith((".mp4", ".png", ".md")):
            os.remove(os.path.join(out, f))
    n = lambda k: os.path.join(REPO, "build", "shots", "brand", tag, k)   # noqa: E731

    # ── true speed, then slowed ─────────────────────────────────────────────────────────────────────────────────────
    film(n("long"), f"{out}/01_true_speed_sound.mp4", "BRAND: apply, idle, the front falls twice (true speed)")
    film(n("long"), f"{out}/02_true_speed_muted.mp4", "BRAND, muted", "--mute")
    film(n("apply"), f"{out}/03_apply_slow_x4.mp4", "the apply, x4 slower", "--mute", "--slow", "4")
    film(n("long"), f"{out}/04_migrate_slow_x4.mp4", "the migration on the second fall (8.2 s), x4 slower", "--mute", "--slow", "4",
         lo=7950, hi=8850)
    # ── the variations ───────────────────────────────────────────────────────────────────────────────────────────────
    film(n("etch_clean"), f"{out}/05_etch_deepen_sound.mp4", "ETCH: the mark deepens on every tick (120 / 170 / 220 / 240 %)")
    film(n("etch_clean"), f"{out}/05b_etch_deepen_slow_x3.mp4", "ETCH, the deepening ticks, x3 slower", "--mute", "--slow", "3",
         lo=3900, hi=9800)
    film(n("etch"), f"{out}/05c_etch_with_jaws_sound.mp4", "ETCH beside JAWS (the fixture's own build)")
    film(n("sprawl"), f"{out}/06_sprawl_spread_sound.mp4", "SPRAWL: every creature marked at half strength")
    film(n("sprawl"), f"{out}/06b_sprawl_spread_slow_x3.mp4", "SPRAWL's spread, x3 slower (to the last coil's re-form)", "--mute",
         "--slow", "3", lo=1700, hi=3000)
    if os.path.exists(n("sprawl_winnow") + ".log"):
        film(n("sprawl_winnow"), f"{out}/06c_sprawl_winnow_sound.mp4", "SPRAWL + WINNOW: every coil deepens, one after another")
        film(n("sprawl_winnow"), f"{out}/06d_sprawl_winnow_deepen_slow_x3.mp4", "SPRAWL + WINNOW's deepen at 4 s, x3 slower",
             "--mute", "--slow", "3", lo=3900, hi=4700)
    # ── beside the accepted references ─────────────────────────────────────────────────────────────────────────────
    if os.path.exists(n("sprawl_winnow_clean") + ".log"):
        film(n("sprawl_winnow_clean"), f"{out}/06e_sprawl_winnow_full_strength_sound.mp4",
             "SPRAWL + WINNOW at full strength (WILT in JAWS's slot): the row's ripple, unquieted")
    film(n("press"), f"{out}/07_with_press_sound.mp4", "with PRESS: the crush and the brand on one body")
    film(n("fast"), f"{out}/08_with_spray_hard_hands_sound.mp4", "with SPRAY and HARD HANDS (fast TEMPO)")
    film(n("long"), f"{out}/09_with_jaws_sound.mp4", "with JAWS: the bite on the branded attacker", lo=3500, hi=4800)
    # ── before / after ─────────────────────────────────────────────────────────────────────────────────────────────
    grid([n("before"), n("long")], ["BEFORE: the reticle held behind the hunter", "AFTER: the Etched Shadow Brand"],
         f"{out}/10_before_after.mp4", ARENA, fps=30, shrink=0.8)

    film(n("light"), f"{out}/09b_light_body_sound.mp4", "on a LIGHT body (the Pale Choir): the ink half of the material")
    if os.path.exists(n("light_etch") + ".log"):
        film(n("light_etch"), f"{out}/09c_light_body_etch_sound.mp4", "ETCH on a LIGHT body: the deep stages' ink on the Pale Choir")
    # ── frame sheets ───────────────────────────────────────────────────────────────────────────────────────────────
    sheet([n("apply")], ["APPLY (u from the first tick)"], f"{out}/11_apply_sheet.png", 2000, -260, 330, WHELP, per_row=9,
          scale=0.62, note="smoke motes gather on the flank, condense into the coil; the cut lights ON the tick; it settles")
    deepen_rows(n("etch_clean"), f"{out}/12_deepen_sheet.png")
    stages_side_by_side(n("etch_clean"), f"{out}/12b_stages_same_host.png")
    sheet([n("long")], ["MIGRATE (u from the second fall at 8200)"], f"{out}/13_migrate_sheet.png", 8200, -70, 560,
          (1000, 560, 1700, 900), per_row=7, scale=0.7,
          note="the coil comes apart on the falling body; the next creature (Core marks it at once) carries a thickening smoke; "
               "a strand of overlapping smoke puffs slides under the heads to its coil's tip and the coil is drawn in from there")
    sheet([n("sprawl")], ["SPREAD (SPRAWL, u from the first tick)"], f"{out}/14_spread_sheet.png", 2000, -60, 900,
          (880, 560, 1900, 900), per_row=6, scale=0.5)
    idle_sheet(n("long"), f"{out}/15_idle_sheet.png")
    sheet([n("light")], ["LIGHT BODY (the Pale Choir): the char and roots in ink"], f"{out}/15b_light_body_sheet.png", 2000, -260, 2400,
          (880, 480, 1900, 950), per_row=6, scale=0.5)
    shutil.copy(os.path.join(REPO, "tools", "asset-pipeline", "v2", "keypose_sources", "seeker_brand_sheet.png"),
                f"{out}/16_parts_sheet.png")
    families_sheet(n, f"{out}/21_other_families.png")
    body_points(f"{out}")
    sources_sheet(f"{out}/17_pixellab_sources.png")

    # ── measures ───────────────────────────────────────────────────────────────────────────────────────────────────
    measures(n, f"{out}/18_measures.md")
    lines = [l.rstrip("\n") for l in open(n("long") + ".log", encoding="utf-8", errors="replace")
             if "\tmark-wave\t" in l or "\tmark-draw\t" in l or "\tevent\tEnemyDown" in l or "\tevent\tMarked" in l]
    order = [l.rstrip("\n") for l in open(n("order") + ".log", encoding="utf-8", errors="replace")
             if "\tfield-wave\t" in l or "\tmark-wave\t" in l][:6] if os.path.exists(n("order") + ".log") else []
    open(f"{out}/20_order_trace.md", "w", encoding="utf-8").write(
        "# BRAND woven BEFORE PRESS: both presented (PRESS's field-wave and BRAND's mark-wave in one wave)\n\n"
        "The first Field used to win: BRAND in the earlier slot drew a reticle behind the hunter and PRESS vanished.\n\n```\n"
        + "\n".join(order) + "\n```\n")
    open(f"{out}/19_trace.md", "w", encoding="utf-8").write(
        "# The long take's mark trace (mark-wave, mark-draw, Marked, EnemyDown)\n\n```\n" + "\n".join(lines[:400]) + "\n```\n")
    print("done ->", out)


def deepen_rows(pre, path):
    """One row per full-strength ETCH tick (4000: deep1 -> deep2; 6000: deep2 -> deep3 on the second host), every
    captured frame from just before the tick to the chisel cooling, cropped CLOSE round the host's brand (the trace's
    anchor) at 1.5x. The 8000 tick's host falls 200 ms later: that step (deep3 -> deep4) is carried to the next host and
    cut there by the catch-up chisel once the fall's smoke has cleared (the third row)."""
    shots = read(pre + ".log")
    draws = trace(pre, "mark-draw")
    at = [(r[0], tuple(int(v) for v in r[1]["at"].split(","))) for r in draws if "at" in r[1]]
    cw, ch, z = 170, 130, 1.5
    bw, bh = int(cw * z), int(ch * z)
    rows = []
    for tick, name in ((4000, "tick 4000: deep1 (120 %) -> deep2 (170 %)"), (6000, "tick 6000: deep2 (170 %) -> deep3 (220 %)"),
                       (9200, "tick 8000's step, carried: deep3 (220 %) -> deep4 (240 %) cut on the next host after the 8200 fall (ms from 9200)")):
        rows.append((name, [(i, ph - tick, ph) for i, (w, ph) in sorted(shots.items()) if -40 <= ph - tick <= 520]))
    per = max(len(r) for _, r in rows)
    img = Image.new("RGB", (per * (bw + 4) + 8, len(rows) * (bh + 44) + 44), (20, 17, 14))
    d = ImageDraw.Draw(img)
    for k, (name, fr) in enumerate(rows):
        y0 = 6 + k * (bh + 44)
        d.text((8, y0), name, fill=(235, 200, 120), font=SMALL)
        for c, (i, u, ph) in enumerate(fr):
            x = 6 + c * (bw + 4)
            ax, ay = min(at, key=lambda q: abs(q[0] - ph))[1]
            d.text((x, y0 + 18), f"{u:+.0f}", fill=(200, 180, 150), font=SMALL)
            crop = Image.open(f"{pre}_{i:02d}.png").convert("RGB").crop((ax - cw // 2, ay - ch // 2, ax + cw // 2, ay + ch // 2))
            img.paste(crop.resize((bw, bh), Image.NEAREST), (x, y0 + 36))
    d.text((8, img.height - 30), "ETCH, each step: the old cut holds while the host's own bite plays out, then a chisel of light travels "
           "along the new cut in three 60 ms steps (deep2: into the hollow's wall; deep3 / deep4: on along the hook, inward) and the "
           "new stage appears under it; never brighter, never bigger. Crops follow the host (the trace's anchor).",
           fill=(200, 180, 150), font=SMALL)
    img.save(path)
    print(os.path.basename(path))


def stages_side_by_side(pre, path):
    """ETCH's four depths, settled, in ONE pose of the pack's one-second bite cycle (the same phase of it for every stage),
    at 1x, 2x and 4x. Never inside the second after a fall (the fallen creature's white death smoke drifts over the next
    host there), never on a beat."""
    shots = read(pre + ".log")
    rows = trace(pre, "mark-draw")
    waves = trace(pre, "mark-wave")
    falls = [float(f.split(">")[0]) for f in waves[-1][1].get("falls", "").split(",") if ">" in f] if waves else []
    picks = []
    for stage in (2, 3, 4, 5):
        cand = [(r[0], r[1]) for r in rows if int(r[1]["stage"]) == stage and "at" in r[1] and int(r[1]["hosts"]) == 1
                and int(r[1]["sprites"]) == 3 and not any(f <= r[0] <= f + 1000 for f in falls)]
        if not cand:
            continue
        ph, d = min(cand, key=lambda c: abs((c[0] % 1000) - 650))      # the same moment of the bite cycle
        i = min(shots, key=lambda q: abs(shots[q][1] - ph))
        x, y = (int(v) for v in d["at"].split(","))
        picks.append((stage, ph, i, x, y, int(d["front"])))
    if not picks:
        return
    img = Image.new("RGB", (len(picks) * 340 + 10, 640), (20, 17, 14))
    dr = ImageDraw.Draw(img)
    names = {2: "deep1 (120 %)", 3: "deep2 (170 %)", 4: "deep3 (220 %)", 5: "deep4 (240 %)"}
    for k, (stage, ph, i, x, y, host) in enumerate(picks):
        im = Image.open(f"{pre}_{i:02d}.png").convert("RGB")
        one = im.crop((x - 160, y - 60, x + 160, y + 60))
        two = im.crop((x - 80, y - 60, x + 80, y + 60)).resize((320, 240), Image.NEAREST)
        four = im.crop((x - 40, y - 26, x + 40, y + 26)).resize((320, 208), Image.NEAREST)
        dr.text((10 + k * 340, 4), f"{names[stage]}  creature {host}  {ph:.0f} ms", fill=(235, 200, 120), font=SMALL)
        img.paste(one, (10 + k * 340, 22))
        img.paste(two, (10 + k * 340, 150))
        img.paste(four, (10 + k * 340, 398))
    dr.text((10, 616), "each depth on its host at one moment of the bite cycle, settled: 1x, 2x, 4x; every step cuts more, "
            "the brightest band never rises", fill=(200, 180, 150), font=SMALL)
    img.save(path)
    print(os.path.basename(path))


def families_sheet(n, path):
    """BRAND (ETCH) on four other regions' creatures, filmed in game: a frame before the first tick's apply has formed,
    one settled, one deeper, and the last."""
    takes = [t for t in ("fam_machine_armoured", "fam_shadow_caster", "fam_mind_swarm", "fam_nature_bruiser")
             if os.path.exists(n(t) + ".log")]
    if not takes:
        return
    tiles = []
    for t in takes:
        shots = read(n(t) + ".log")
        rows = [r for r in trace(n(t), "mark-draw") if "at" in r[1] and int(r[1]["hosts"]) >= 1]
        if not rows:
            continue
        want = [2300, 3500, 4700, 6200]
        picks = []
        for w in want:
            r = min(rows, key=lambda q: abs(q[0] - w))
            i = min(shots, key=lambda q: abs(shots[q][1] - r[0]))
            picks.append((i, r[0], tuple(int(v) for v in r[1]["at"].split(","))))
        tiles.append((t, picks))
    img = Image.new("RGB", (4 * 330 + 10, len(tiles) * 262 + 40), (20, 17, 14))
    dr = ImageDraw.Draw(img)
    for r, (t, picks) in enumerate(tiles):
        for c, (i, ph, (x, y)) in enumerate(picks):
            im = Image.open(f"{n(t)}_{i:02d}.png").convert("RGB")
            crop = im.crop((x - 150, y - 110, x + 150, y + 110)).resize((320, 234), Image.LANCZOS)
            img.paste(crop, (10 + c * 330, 22 + r * 262))
            dr.text((10 + c * 330, 6 + r * 262), f"{t.replace('fam_', '')}  {ph:.0f} ms", fill=(235, 200, 120), font=SMALL)
    dr.text((10, img.height - 16), "the authored body point on creatures the reference fight never shows (in game, ETCH)",
            fill=(200, 180, 150), font=SMALL)
    img.save(path)
    print(os.path.basename(path))


def body_points(out):
    """The authoring tool's contact sheets: every idle and attack frame of all 60 creature strips with its body point and
    the coil's footprint (a red frame: under 88 % of the footprint on the body)."""
    import subprocess
    tmp = os.path.join(REPO, "build", "shots", "brand", "_body_points")
    subprocess.run([sys.executable, os.path.join(REPO, "tools", "asset-pipeline", "v2", "mark_points.py"), tmp], check=True,
                   stdout=subprocess.DEVNULL)
    for f in sorted(os.listdir(tmp)):
        if f.startswith("mark_points_") and f.endswith(".png"):
            shutil.copy(os.path.join(tmp, f), os.path.join(out, "22_body_points_" + f[len("mark_points_"):]))
    print("22_body_points_*")


def idle_sheet(pre, path):
    """The quiet idle: one frame every ~300 ms, 2.4 .. 6.3 s (no beat in between but the creatures' own life)."""
    shots = read(pre + ".log")
    fr = [(i, ph) for i, (w, ph) in sorted(shots.items()) if 2400 <= ph <= 6300]
    fr = fr[::9]
    bw, bh = int((WHELP[2] - WHELP[0]) * 0.55), int((WHELP[3] - WHELP[1]) * 0.55)
    img = Image.new("RGB", (7 * (bw + 6) + 6, ((len(fr) + 6) // 7) * (bh + 24) + 36), (20, 17, 14))
    d = ImageDraw.Draw(img)
    for k, (i, ph) in enumerate(fr):
        r, c = divmod(k, 7)
        x, y = 6 + c * (bw + 6), 6 + r * (bh + 24)
        d.text((x, y), f"{ph:.0f}", fill=(235, 200, 120), font=SMALL)
        img.paste(Image.open(f"{pre}_{i:02d}.png").convert("RGB").crop(WHELP).resize((bw, bh), Image.LANCZOS), (x, y + 18))
    d.text((6, img.height - 26), "IDLE: the settled coil, a slow stepped swirl inside it; no pulse, no ring, no light between beats",
           fill=(200, 180, 150), font=SMALL)
    img.save(path)
    print(os.path.basename(path))


def sources_sheet(path):
    """The PixelLab pixen silhouettes: the ten generated, the one used (1bf52462), each 64 px shown x3."""
    src = os.path.join(REPO, "tools", "asset-pipeline", "v2", "keypose_sources", "seeker_brand_src")
    fs = sorted(f for f in os.listdir(src) if f.endswith(".png"))
    img = Image.new("RGB", (len(fs) * 200 + 10, 236), (58, 56, 54))
    d = ImageDraw.Draw(img)
    for k, f in enumerate(fs):
        im = Image.open(os.path.join(src, f)).convert("RGBA").resize((192, 192), Image.NEAREST)
        tile = Image.new("RGBA", (192, 192), (58, 56, 54, 255))
        tile.alpha_composite(im)
        img.paste(tile.convert("RGB"), (10 + k * 200, 30))
        used = "1bf52462" in f
        d.text((10 + k * 200, 8), f[6:14] + ("  USED" if used else ""), fill=(255, 220, 140) if used else (200, 190, 170), font=SMALL)
    img.save(path)
    print(os.path.basename(path))


def mark_light(pre, lo=None, hi=None, box=ROW):
    """Per shot: the mark's OWN light in the creature row -- VIOLET-lit pixels above luma 170 and 210 (the brand is the
    only violet-lit thing on the row but JAWS and PRESS, whose frames are left out by their own trace lines). Two takes
    of one seed start a frame apart, so a with / without difference would count the creatures' motion instead."""
    busy = set()
    for line in open(pre + ".log", encoding="utf-8", errors="replace"):
        parts = line.split("\t")
        if len(parts) > 3 and parts[3] in ("reaction-draw", "field-draw"):
            busy.add(round(float(parts[2])))
    rows = []
    for i, (w, ph) in sorted(read(pre + ".log").items()):
        if (lo is not None and ph < lo) or (hi is not None and ph > hi):
            continue
        if any(abs(ph - b) <= 17 for b in busy):
            continue
        A = np.asarray(Image.open(f"{pre}_{i:02d}.png").convert("RGB").crop(box)).astype(np.float32)
        L = luma(A)
        violet = (A[..., 2] > A[..., 1] + 25) & (A[..., 0] > A[..., 1] - 5)
        rows.append((ph, int(((L > 170) & violet).sum()), int(((L > 210) & violet).sum()), int(((L > 90) & violet).sum()),
                     float(L[violet].max()) if violet.any() else 0.0))
    return rows


def construction():
    """By construction, from the atlas and the recipe's tints: what each stage can light, at scale 1 (a whelp)."""
    atlas = np.asarray(Image.open(os.path.join(REPO, "assets", "art", "VFX", "parts", "fxp_seeker_brand.png")).convert("RGBA")).astype(np.float32) / 255
    cell, stages = 84, ["spread", "base", "deep1", "deep2", "deep3", "deep4"]
    groove, hot, smoke = np.array([136, 108, 196]) / 255, np.array([226, 208, 255]) / 255, np.array([110, 92, 160]) / 255
    idle, edge_c, carve0 = 3, 9, 10

    def luma(c):
        return (c[..., 0] * 0.299 + c[..., 1] * 0.587 + c[..., 2] * 0.114) * 255

    out = ["## By construction (the atlas x the recipe's tints, over a near-black body, at a whelp's scale 1)", "",
           "| stage | depth | cut px | brightest at rest (luma) | px > 170 at rest | lit core at rest (px > 100) | smoke halo px "
           "| the apply beat (whole cut-line px) | the deepen chisel (px per step) | beat luma full / quiet |",
           "|---|---|---|---|---|---|---|---|---|---|"]
    depth = ["< 50 %", "50-99 %", "100-149 %", "150-199 %", "200-239 %", ">= 240 %"]
    hot_l = float(luma(hot[None, :])[0])
    for k, name in enumerate(stages):
        c = atlas[k * cell:(k + 1) * cell, idle * cell:(idle + 1) * cell]
        a = c[..., 3]
        rest = luma(c[..., :3] * groove) * (a > 0.01)
        top = float(rest.max())
        h = atlas[(6 + k) * cell:(7 + k) * cell, idle * cell:(idle + 1) * cell, 3]
        e = int((atlas[k * cell:(k + 1) * cell, edge_c * cell:(edge_c + 1) * cell, 3] > 0.01).sum())
        carve = [int((atlas[k * cell:(k + 1) * cell, (carve0 + j) * cell:(carve0 + j + 1) * cell, 3] > 0.01).sum()) for j in range(3)]
        full, quiet = top + (hot_l - top) * 0.8, top + (hot_l - top) * 0.8 * 0.5     # an upper bound: rendered ~183
        out.append(f"| {name} | {depth[k]} | {int((a > 0.01).sum())} | {top:.0f} | {int((rest > 170).sum())} | {int((rest > 100).sum())} | "
                   f"{int((h > 0.01).sum())} | {e} | {' / '.join(str(v) for v in carve)} | {full:.0f} / {quiet:.0f} |")
    out += ["", "At rest nothing passes luma 170 at any depth, and only a travelling third of the ring is lit (the inner hook never);",
            "the whole coil lights only on the apply beat. Deeper stages cut MORE of the body (a wider groove, the ring's wall cut",
            "into the hollow, then the thin hook cut further along the spiral, inward), never bigger (deep1 to deep4 share one",
            "outline box) and never more light: the brightest texel is the same band. The deepen beat is a chisel: the new cut",
            "only, in three stretches travelling round the ring (deep3 / deep4: setting in at the old hook's end and running on",
            "inward). The beat luma above is an upper bound (the hot tint at full alpha over the rest band); the render peaks",
            "near 183."]
    return out


def stage_sizes(n):
    """How much of the body each stage marks IN GAME: the violet lit px of the groove (B - G above 45, luma 55-175) in a box
    round the host's anchor, median over settled frames of each stage in the clean ETCH take (no beat, no fall's smoke,
    no hit flash). The count is the cut's, whatever the creature's pose (the round-3 deepen lens measured it the same way)."""
    pre = n("etch_clean")
    if not os.path.exists(pre + ".log"):
        return []
    shots = read(pre + ".log")
    rows = trace(pre, "mark-draw")
    waves = trace(pre, "mark-wave")
    falls = [float(f.split(">")[0]) for f in waves[-1][1].get("falls", "").split(",") if ">" in f] if waves else []
    ticks = [float(t.split(":")[0]) for t in waves[-1][1].get("ticks", "").split(",") if ":" in t] if waves else []
    per = {}
    for ph, d, _ in rows:
        if "at" not in d or int(d["hosts"]) != 1 or int(d["sprites"]) != 3:
            continue
        if any(f <= ph <= f + 1000 for f in falls) or any(t <= ph <= t + 700 for t in ticks):
            continue
        i = min(shots, key=lambda q: abs(shots[q][1] - ph))
        if abs(shots[i][1] - ph) > 9:
            continue
        x, y = (int(v) for v in d["at"].split(","))
        A = np.asarray(Image.open(f"{pre}_{i:02d}.png").convert("RGB").crop((x - 50, y - 50, x + 50, y + 50))).astype(np.float32)
        L = luma(A)
        if (L > 200).sum() > 30:
            continue                                    # a hit flash or a fall's smoke
        violet = (A[..., 2] - A[..., 1] > 45) & (L >= 55) & (L <= 175)
        per.setdefault(int(d["stage"]), []).append(int(violet.sum()))
    names = {2: "deep1 (120 %)", 3: "deep2 (170 %)", 4: "deep3 (220 %)", 5: "deep4 (240 %)"}
    out = ["", "## Each ETCH step marks more of the body, in game (the clean ETCH take, settled frames of each stage)", "",
           "| stage | frames | violet lit px (median) | the step |", "|---|---|---|---|"]
    last = None
    for st in sorted(per):
        med = float(np.median(per[st]))
        out.append(f"| {names.get(st, st)} | {len(per[st])} | {med:.0f} | " + (f"{100 * (med - last) / last:+.0f} %" if last else "") + " |")
        last = med
    return out + ["", "Settled frames only: none within 0.7 s of a tick or 1 s of a fall, none with a hit flash in the box."]


def other_light(pre, lo, hi):
    """Every other layer's light the take's trace records inside [lo, hi): JAWS's snaps, hit flashes, falls."""
    out = []
    for at, _, parts in trace(pre, "reaction-cue"):
        if lo <= at < hi:
            out.append(f"JAWS snap {at:.0f}")
    for at, kv, _ in trace(pre, "flash"):
        if lo <= at < hi:
            out.append(f"hit flash {at:.0f} (slot {kv.get('slot', '?')})")
    for at, _, parts in trace(pre, "vfx-spawn"):
        if len(parts) > 4 and parts[4] == "fx_death" and lo - 750 <= at < hi:
            out.append(f"fall {at:.0f} (its death smoke)")
    return ", ".join(dict.fromkeys(out)) if out else "none"


def measures(n, path):
    L = ["# BRAND in numbers", ""]
    L += ["## The light the mark ADDS, exactly (the same frames of the same seeded fight, with and without the mark)", "",
          "Two takes filmed every frame at 60 Hz align frame for frame; the only difference is the mark (without it BRAND",
          "falls back to the old reticle behind the hunter, outside the creature row measured here). Pixels of the creature",
          "row brighter than luma 170 / 210 WITH the mark and at least 12 brighter than without it.", "",
          "| moment | window (ms) | frames | peak px > 170 | peak px > 210 | mean px > 170 | peak px changed (any) | other light in the window (the take's trace) |",
          "|---|---|---|---|---|---|---|---|"]
    for after, before, name, lo, hi in (("apply", "apply_off", "APPLY: the gather and the cut ON the first tick", 1750, 2280),
                                        ("apply", "apply_off", "IDLE after the apply (base stage)", 2300, 2790),
                                        ("etch_tick", "etch_tick_off", "IDLE before ETCH's tick (stage deep1)", 3580, 3940),
                                        ("etch_tick", "etch_tick_off", "DEEPEN: ETCH's tick at 4000 (120 -> 170 %)", 3950, 4500),
                                        ("etch_tick", "etch_tick_off", "IDLE after it (stage deep2)", 4300, 4800),
                                        ("clean_tick", "clean_tick_off", "CLEAN DEEPEN at 4000 (120 -> 170 %): the tick, the settle, the chisel", 3990, 4700),
                                        ("clean_tick6", "clean_tick6_off", "the 5200 fall's white death smoke drifting over the NEW host (not the mark's light)", 5800, 6140),
                                        ("clean_tick6", "clean_tick6_off", "CLEAN DEEPEN at 6000 (170 -> 220 %): the settle, the chisel", 6150, 6700),
                                        ("sprawl_winnow_tick", "sprawl_winnow_tick_off", "SPRAWL + WINNOW beside JAWS: the row deepening at 4000 (a quiet tick)", 3990, 4700),
                                        ("sprawl_winnow_clean_tick", "sprawl_winnow_clean_tick_off", "SPRAWL + WINNOW at FULL strength (WILT in JAWS's slot): the row's ripple at 4000", 3990, 4700)):
        if not (os.path.exists(n(after) + ".log") and os.path.exists(n(before) + ".log")):
            continue
        rows = added_light(n(after), n(before), lo, hi)
        if not rows:
            continue
        L.append(f"| {name} | {lo}-{hi} | {len(rows)} | {max(r[1] for r in rows)} | {max(r[2] for r in rows)} | "
                 f"{sum(r[1] for r in rows) / len(rows):.0f} | {max(r[3] for r in rows)} | {other_light(n(after), lo, hi)} |")
    L += stage_sizes(n)
    L += ["", "The mark's own beat is bounded by construction (the table below gives the bound). A count above 210 is another",
          "layer's light landing on the lit coil; the last column names every such layer the take's trace records in the",
          "window: JAWS's snap (its reaction cue), the game's hit flash (a hit whitens body and brand together) and a fall",
          "(whose white death smoke drifts over the next host ~450-750 ms later). A row beside JAWS is never the mark's alone.", ""]
    L += construction()
    L += ["", "## The mark's own light across the long takes (violet-lit pixels; frames where JAWS or PRESS draw left out)", "",
          "An upper bound: the whelps' pale pink eyes pass the same colour test (luma ~235), so the idle rows below count them",
          "too; the exact table above is the mark alone.", "",
          "The accepted references' own peaks (ADR-011): SPRAY 2761 / 805, HARD HANDS 1382 / 612, JAWS 6957 / 5327, PRESS",
          "749-867 above 170 (~27 above 210 at its contact).", "",
          "| take | moment | window (ms) | peak px > 170 | peak px > 210 | px > 90 (the lit coil, peak) | brightest luma |",
          "|---|---|---|---|---|---|---|"]
    for take, name, lo, hi in (("apply", "APPLY: the cut on the first tick", 1950, 2250),
                               ("long", "IDLE (the settled coil)", 2400, 3900),
                               ("long", "IDLE", 4300, 5900),
                               ("long", "MIGRATE (the front falls at 6700)", 6650, 7300),
                               ("long", "MIGRATE (the next falls at 8200)", 8150, 8800),
                               ("etch", "DEEPEN (ETCH ticks 4000 / 6000 / 8000)", 3950, 8300),
                               ("sprawl", "SPREAD (SPRAWL's first tick)", 1950, 2600),
                               ("sprawl", "SPRAWL idle (four coils)", 2700, 3900)):
        rows = mark_light(n(take), lo, hi)
        if not rows:
            continue
        L.append(f"| {take} | {name} | {lo}-{hi} | {max(r[1] for r in rows)} | {max(r[2] for r in rows)} | "
                 f"{max(r[3] for r in rows)} | {max(r[4] for r in rows):.0f} |")
    L += ["", "BEFORE, for comparison: the reticle held behind the hunter (518 px frame, white art tinted by the slot's Source,",
          "flaring once a second) -- see 10_before_after.mp4.", ""]
    # sprites, allocation, attachment
    L += ["## The trace (mark-draw, every frame the mark draws)", ""]
    for take in ("long", "etch", "etch_clean", "sprawl", "sprawl_winnow", "sprawl_winnow_clean", "press", "fast", "light", "light_etch", "order",
                 "fam_machine_armoured", "fam_shadow_caster", "fam_mind_swarm", "fam_nature_bruiser"):
        rows = trace(n(take), "mark-draw")
        if not rows:
            continue
        sprites = [int(r[1]["sprites"]) for r in rows]
        alloc = [int(r[1]["alloc"]) for r in rows]
        hosts = [int(r[1]["hosts"]) for r in rows]
        stages = sorted({int(r[1]["stage"]) for r in rows if int(r[1]["stage"]) >= 0})
        big = [a for a in alloc if a > 0]
        where = [f"{r[0]:.0f} ms ({int(r[1]['alloc']) / 1e6:.1f} MB)" for r in rows if int(r[1]["alloc"]) > 0]
        L.append(f"- **{take}**: {len(rows)} frames drawn; sprites per frame {min(sprites)}-{max(sprites)}; bodies carrying it "
                 f"{min(hosts)}-{max(hosts)}; stages drawn {stages}; {sum(1 for a in alloc if a == 0)} of {len(alloc)} frames "
                 f"allocate 0 bytes" + (f"; the rest at {', '.join(where[:6])}" if where else ""))
        ats = [(r[0], tuple(int(v) for v in r[1]["at"].split(",")), r[1].get("scale"), int(r[1]["front"])) for r in rows if "at" in r[1]]
        if ats:
            steps = [abs(ats[k][1][0] - ats[k - 1][1][0]) + abs(ats[k][1][1] - ats[k - 1][1][1])
                     for k in range(1, len(ats)) if ats[k][3] == ats[k - 1][3]]
            if steps:
                big_steps = sum(1 for d in steps if d > 12)
                L.append(f"  - the body point on one host moves {np.median(steps):.0f} px (median) between drawn frames; "
                         f"{big_steps} of {len(steps)} steps exceed 12 px; scale {ats[0][2]}")
    open(path, "w", encoding="utf-8").write("\n".join(L) + "\n")
    print(os.path.basename(path))


if __name__ == "__main__":
    main()
