#!/usr/bin/env python3
"""brand_cut_evidence.py -- BRAND's SECOND PASS, the ETCHED SHADOW CUT (ADR-011, 2026-09-30): the review battery the
owner asked for, from the takes films_brand.sh filmed (the new cut) beside the first slice's (the lit lavender spiral).

    PYTHONUTF8=1 python tools/asset-pipeline/brand_cut_evidence.py <new tag> <first-slice tag> <out dir>

1 the first slice at true speed, 2 the new cut at true speed, 3 the apply close, 4 the idle close, 5 the three depths
side by side, 6 the deepen at true speed, 7 the spread at true speed, 8 the transfer after a death at true speed, 9 a dark
body and 10 a light body (first slice vs new), 11-13 beside PRESS, SPRAY / HARD HANDS and JAWS, 14 the material sheet
(the first slice's glowing line vs the new dark cut with its one-sided rim), 15 the measures.
"""
import os
import shutil
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from brand_evidence import added_light, body_points, families_sheet, other_light, trace  # noqa: E402
from foundation_evidence import REPO, SMALL, film, read, sheet  # noqa: E402

CLOSE = "1000,600,500,300"                 # the front creatures, close (x, y, w, h)
TRANSFER = "1080,600,560,300"              # a transfer: the fallen front at the left edge, the NEW front where the mark seeps in
NEW_FRONT = (1080, 600, 1640, 900)
WHELP = (940, 600, 1420, 900)
C = 84
CX, CY = 44.0, 40.6
INK, SMOKE, GROOVE, HOT = (12, 7, 20), (110, 92, 160), (136, 108, 196), (226, 208, 255)


def frame_at(pre, ms):
    """The captured frame nearest a playhead (index, playhead)."""
    shots = read(pre + ".log")
    i, (w, ph) = min(shots.items(), key=lambda kv: abs(kv[1][1] - ms))
    return i, ph


def anchor_at(pre, ms):
    rows = [(r[0], r[1]) for r in trace(pre, "mark-draw") if "at" in r[1]]
    if not rows:
        return None
    t, kv = min(rows, key=lambda r: abs(r[0] - ms))
    x, y = (int(v) for v in kv["at"].split(","))
    return x, y, int(kv.get("stage", -1))


def settled(pre, stage, after=250, before_fall=True):
    """A playhead where the drawn stage has been `stage` for `after` ms and no beat or fall is near."""
    rows = [(r[0], int(r[1]["stage"])) for r in trace(pre, "mark-draw") if "stage" in r[1]]
    falls = [r[0] for r in trace(pre, "vfx-spawn") if len(r[2]) > 4 and r[2][4] == "fx_death"]
    flashes = [r[0] for r in trace(pre, "flash")]
    since = None
    for t, s in rows:
        if s != stage:
            since = None
            continue
        since = t if since is None else since
        if t - since >= after and all(t < f or t - f > 800 for f in falls) and all(abs(t - f) > 150 for f in flashes):
            return t
    return None


def crop_at(pre, ms, w=150, h=110, zoom=1):
    i, ph = frame_at(pre, ms)
    a = anchor_at(pre, ph)
    img = Image.open(f"{pre}_{i:02d}.png").convert("RGB")
    x, y = (a[0], a[1]) if a else (1200, 780)
    c = img.crop((x - w // 2, y - h // 2, x + w // 2, y + h // 2))
    return c.resize((w * zoom, h * zoom), Image.NEAREST), ph


def comparison(rows, cols, path, title, zoom=2):
    """rows: [(label, pre)], cols: [(label, ms)] -> a grid of close crops (1x and zoomed)."""
    w, h = 150, 110
    cw = w + w * zoom + 18
    img = Image.new("RGB", (8 + len(cols) * cw, 40 + len(rows) * (h * zoom + 34)), (20, 17, 14))
    d = ImageDraw.Draw(img)
    d.text((8, 6), title, fill=(235, 200, 120), font=SMALL)
    for r, (rl, pre) in enumerate(rows):
        y0 = 30 + r * (h * zoom + 34)
        d.text((8, y0), rl, fill=(235, 200, 120), font=SMALL)
        for c, (cl, ms) in enumerate(cols):
            if ms is None:
                continue
            x0 = 8 + c * cw
            one, ph = crop_at(pre, ms)
            two, _ = crop_at(pre, ms, zoom=zoom)
            d.text((x0, y0 + 14), f"{cl} ({ph:.0f} ms)  1x | {zoom}x", fill=(200, 180, 150), font=SMALL)
            img.paste(one, (x0, y0 + 30 + (h * zoom - h) // 2))
            img.paste(two, (x0 + w + 6, y0 + 30))
    img.save(path)
    print(os.path.basename(path))


def three_depths(pre, path, at=(3300, 4900, 9900)):
    # each depth settled on its host (etch_clean: depth 1 on the first whelp, depth 2 before its fall at 5.2 s, depth 3
    # on the fourth host, far from any fall's white smoke)
    cols = [(f"DEPTH {s}", t) for s, t in zip((1, 2, 3), at)]
    rows = [("the new cut, each depth settled on its host (etch_clean)", pre)]
    comparison(rows, cols, path, "THREE VISIBLE DEPTHS: marked, deeper, fully branded (ETCH 120 / 170 / 220+ %)", zoom=3)


# ── the material sheet: the atlases themselves, drawn as the runtime draws them ──────────────────────────────────────

def cell(atlas, row, col):
    return np.asarray(atlas.crop((col * C, row * C, (col + 1) * C, (row + 1) * C))).astype(np.float32) / 255


def over(img, x0, y0, rgba, tint, amt):
    h, w = rgba.shape[:2]
    a = rgba[..., 3:4] * amt
    img[y0:y0 + h, x0:x0 + w] = img[y0:y0 + h, x0:x0 + w] * (1 - a) + rgba[..., :3] * (np.array(tint, np.float32) / 255) * a


def draw(frame, atlas, halo_row0, stage, at, smoke_a):
    img = frame.copy()
    x0, y0 = int(round(at[0] - CX)), int(round(at[1] - CY))
    halo = cell(atlas, halo_row0 + stage, 3)
    ha = np.ones_like(halo)
    ha[..., 3] = halo[..., 3]
    over(img, x0, y0, ha, INK, 0.85)
    over(img, x0, y0, ha, SMOKE, smoke_a)
    over(img, x0, y0, cell(atlas, stage, 3), GROOVE, 1.0)
    return img


def material_sheet(dark_frame, dark_at, light_frame, light_at, path):
    old = Image.open(os.path.join(REPO, "tools", "asset-pipeline", "v2", "keypose_sources", "seeker_brand_history",
                                  "fxp_seeker_brand_first_slice_77d383ff.png")).convert("RGBA")
    new = Image.open(os.path.join(REPO, "tools", "asset-pipeline", "v2", "keypose_sources", "seeker_brand_history", "fxp_seeker_brand_etched_cut_39b59aaa.png")).convert("RGBA")
    # (label, atlas, halo row 0, stages to show, lit smoke alpha): the first slice's base / deep2 / deep4 vs DEPTH 1 / 2 / 3
    sets = [("FIRST SLICE: the glowing line", old, 6, (1, 3, 5), 0.6), ("SECOND PASS: the dark cut, one lit rim", new, 4, (1, 2, 3), 0.18)]
    bodies = [("dark body (whelp)", dark_frame, dark_at), ("light body (Pale Choir)", light_frame, light_at)]
    w, h, z = 130, 100, 3
    tw = w * z + 10
    img = Image.new("RGB", (8 + 3 * tw, 40 + len(sets) * len(bodies) * (h * z + 26)), (20, 17, 14))
    d = ImageDraw.Draw(img)
    d.text((8, 6), "MATERIAL: the first slice's glowing lavender line vs the new dark incision with its one-sided rim (3x, as drawn in game)",
           fill=(235, 200, 120), font=SMALL)
    y = 30
    for label, atlas, hr, stages, smoke_a in sets:
        for bl, fpath, at in bodies:
            frame = np.asarray(Image.open(fpath).convert("RGB")).astype(np.float32) / 255
            d.text((8, y), f"{label} -- {bl}", fill=(235, 200, 120), font=SMALL)
            for c, s in enumerate(stages):
                pic = draw(frame, atlas, hr, s, at, smoke_a)
                x, yy = int(at[0]), int(at[1])
                crop = Image.fromarray((np.clip(pic, 0, 1) * 255).astype(np.uint8)).crop((x - w // 2, yy - h // 2, x + w // 2, yy + h // 2))
                img.paste(crop.resize((w * z, h * z), Image.NEAREST), (8 + c * tw, y + 16))
            y += h * z + 26
    img.save(path)
    print(os.path.basename(path))


def boss_sheet(path):
    """The six bosses (no boss fight can be posed by the capture rig): each boss's idle frame 0 with the new mark at
    DEPTH 1 and DEPTH 3, at the size the runtime gives it (SizeShare of the visible height, snapped to thirds), on the
    authored body point, drawn as the runtime draws it."""
    import json
    atlas = Image.open(os.path.join(REPO, "tools", "asset-pipeline", "v2", "keypose_sources", "seeker_brand_history", "fxp_seeker_brand_etched_cut_39b59aaa.png")).convert("RGBA")
    bosses = sorted(d for d in os.listdir(os.path.join(REPO, "assets", "art", "Animations", "Bosses")) if d.endswith("_idle"))
    T = 300
    img = Image.new("RGB", (8 + 2 * (T + 8), 34 + len(bosses) * (T + 22)), (20, 17, 14))
    d = ImageDraw.Draw(img)
    d.text((8, 6), "BOSSES (a still, not a fight: the capture rig cannot pose a boss wave): DEPTH 1 and DEPTH 3 at their runtime size",
           fill=(235, 200, 120), font=SMALL)
    for r, name in enumerate(bosses):
        base = os.path.join(REPO, "assets", "art", "Animations", "Bosses", name, name + "_strip8_512")
        strip = Image.open(base + ".png").convert("RGBA")
        h = strip.height
        f = strip.crop((0, 0, h, h))
        a = np.asarray(f)[..., 3]
        rows = np.nonzero((a > 100).any(1))[0]
        vis = rows.max() - rows.min() + 1
        scale = max(2 / 3, round(0.28 * vis / 60.0 * 3) / 3)
        px, py = json.load(open(base + ".mark.json", encoding="utf-8"))["points"][0]
        for c, tier in enumerate((1, 3)):
            bg = Image.new("RGBA", (h, h), (96, 90, 84, 255))
            bg.alpha_composite(f)
            frame = np.asarray(bg.convert("RGB")).astype(np.float32) / 255
            size = int(round(C * scale))
            layers = []
            for row, tint, amt in ((4 + tier, INK, 0.85), (4 + tier, SMOKE, 0.18), (tier, GROOVE, 1.0)):
                cellimg = atlas.crop((3 * C, row * C, 4 * C, (row + 1) * C)).resize((size, size), Image.NEAREST)
                rgba = np.asarray(cellimg).astype(np.float32) / 255
                if row >= 4:
                    rgba = rgba.copy()
                    rgba[..., :3] = 1.0
                layers.append((rgba, tint, amt))
            x0 = int(round(px * h - CX * scale))
            y0 = int(round(py * h - CY * scale))
            pic = frame.copy()
            for rgba, tint, amt in layers:
                hh, ww = rgba.shape[:2]
                ys, xs = slice(max(0, y0), min(h, y0 + hh)), slice(max(0, x0), min(h, x0 + ww))
                sub_ = rgba[ys.start - y0:ys.stop - y0, xs.start - x0:xs.stop - x0]
                aa = sub_[..., 3:4] * amt
                pic[ys, xs] = pic[ys, xs] * (1 - aa) + sub_[..., :3] * (np.array(tint, np.float32) / 255) * aa
            tile = Image.fromarray((np.clip(pic, 0, 1) * 255).astype(np.uint8)).resize((T, T), Image.LANCZOS)
            img.paste(tile, (8 + c * (T + 8), 30 + r * (T + 22)))
            d.text((10 + c * (T + 8), 30 + r * (T + 22) + T + 2), f"{name.replace('_idle', '')}  DEPTH {tier}  scale {scale:.2f}",
                   fill=(200, 180, 150), font=SMALL)
    img.save(path)
    print(os.path.basename(path))


def atlas_numbers():
    a = np.asarray(Image.open(os.path.join(REPO, "tools", "asset-pipeline", "v2", "keypose_sources", "seeker_brand_history", "fxp_seeker_brand_etched_cut_39b59aaa.png")).convert("RGBA")).astype(np.float32)
    o = np.asarray(Image.open(os.path.join(REPO, "tools", "asset-pipeline", "v2", "keypose_sources", "seeker_brand_history",
                                           "fxp_seeker_brand_first_slice_77d383ff.png")).convert("RGBA")).astype(np.float32)
    out = ["| mark | depth | cut px | lit px (> luma 200 in the art) | lit share | mean grey of the cut |", "|---|---|---|---|---|---|"]
    for label, atl, rows in (("first slice", o, ((1, "base"), (3, "deep2"), (5, "deep4"))),
                             ("second pass", a, ((1, "DEPTH 1"), (2, "DEPTH 2"), (3, "DEPTH 3")))):
        for r, name in rows:
            c = atl[r * C:(r + 1) * C, 3 * C:4 * C]
            m = c[..., 3] > 20
            g = c[..., :3].mean(-1)
            lit = int((m & (g > 200)).sum())
            out.append(f"| {label} | {name} | {int(m.sum())} | {lit} | {lit / max(1, m.sum()):.0%} | {g[m].mean():.0f} |")
    return out


def measures(n, path):
    L = ["# BRAND second pass, in numbers", "", "## The art: dark first, light second", ""]
    L += atlas_numbers()
    L += ["", "The art's grey is multiplied by the runtime tints (the cut by the groove tint, so a grey of 255 draws ~(136, 108, 196)).",
          "The first slice's rest cell was a translucent lit line; the new cut is a near-black incision with dark-violet walls and",
          "one lit rim on its light-facing edge.", "",
          "## The light the mark ADDS, exactly (the same frames with and without the mark)", "",
          "| moment | window (ms) | frames | peak px > 170 | peak px > 210 | mean px > 170 | peak px changed (any) | other light in the window |",
          "|---|---|---|---|---|---|---|---|"]
    for after, before, name, lo, hi in (("apply", "apply_off", "APPLY: the ink gathers, the cut appears, its edge catches", 1750, 2280),
                                        ("apply", "apply_off", "IDLE after the apply (DEPTH 1)", 2300, 2790),
                                        ("clean_tick", "clean_tick_off", "DEEPEN at 4000 (DEPTH 1 -> 2)", 3990, 4700),
                                        ("clean_tick6", "clean_tick6_off", "DEEPEN at 6000 (DEPTH 2 -> 3)", 6150, 6700),
                                        ("sprawl_winnow_clean_tick", "sprawl_winnow_clean_tick_off", "SPRAWL + WINNOW, the row deepening", 3990, 4700)):
        if not (os.path.exists(n(after) + ".log") and os.path.exists(n(before) + ".log")):
            continue
        rows = added_light(n(after), n(before), lo, hi)
        if rows:
            L.append(f"| {name} | {lo}-{hi} | {len(rows)} | {max(r[1] for r in rows)} | {max(r[2] for r in rows)} | "
                     f"{sum(r[1] for r in rows) / len(rows):.0f} | {max(r[3] for r in rows)} | {other_light(n(after), lo, hi)} |")
    L += ["", "The accepted references' peaks (ADR-011): SPRAY 2761 / 805, HARD HANDS 1382 / 612, JAWS 6957 / 5327, PRESS 749-867.",
          "The 'peak px changed (any)' column counts DARKENED pixels too: the new mark's main work is darkening the body.", "",
          "## The trace (mark-draw, every frame the mark draws)", ""]
    for take in ("long", "etch", "etch_clean", "sprawl", "sprawl_winnow", "sprawl_winnow_clean", "press", "fast", "light",
                 "light_etch", "fam_machine_armoured", "fam_shadow_caster", "fam_mind_swarm", "fam_nature_bruiser",
                 "fam_body_armoured", "fam_spirit_caster", "boss_thorn_regent", "boss_forge_colossus", "boss_void_reaper",
                 "boss_spirit_matron", "boss_crystal_lich", "boss_lumen_angel"):
        if not os.path.exists(n(take) + ".log"):
            continue
        rows = trace(n(take), "mark-draw")
        if not rows:
            continue
        sprites = [int(r[1]["sprites"]) for r in rows]
        alloc = [int(r[1]["alloc"]) for r in rows]
        stages = sorted({int(r[1]["stage"]) for r in rows if int(r[1]["stage"]) >= 0})
        L.append(f"- **{take}**: {len(rows)} frames drawn; sprites per frame {min(sprites)}-{max(sprites)}; depths drawn {stages}; "
                 f"{sum(1 for a in alloc if a == 0)} of {len(alloc)} frames allocate 0 bytes")
    open(path, "w", encoding="utf-8").write("\n".join(L) + "\n")
    print(os.path.basename(path))


def main():
    tag, first, out = sys.argv[1], sys.argv[2], os.path.abspath(sys.argv[3])
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f[:2].isdigit() and f.endswith((".mp4", ".png", ".md")):
            os.remove(os.path.join(out, f))
    shots = os.path.join(REPO, "build", "shots", "brand")
    n = lambda take: os.path.join(shots, tag, take)          # noqa: E731
    o = lambda take: os.path.join(shots, first, take)        # noqa: E731
    first_films = os.path.join(REPO, "production", "qa", "evidence", "brand-mark")
    shutil.copy(os.path.join(first_films, "01_true_speed_sound.mp4"), f"{out}/01_first_slice_true_speed_sound.mp4")
    film(n("long"), f"{out}/02_new_true_speed_sound.mp4", "BRAND, the etched shadow cut: apply, idle, two transfers (true speed)")
    film(n("long"), f"{out}/02b_new_true_speed_muted.mp4", "the etched shadow cut, muted", "--mute")
    film(n("apply"), f"{out}/03_apply_close.mp4", "the apply, close, true speed", crop=CLOSE)
    film(n("apply"), f"{out}/03b_apply_close_slow_x4.mp4", "the apply, close, x4 slower", "--mute", "--slow", "4", crop=CLOSE)
    film(n("long"), f"{out}/04_idle_close.mp4", "the idle, close, true speed (2.4-5.9 s)", "--mute", lo=2400, hi=5900, crop=CLOSE)
    three_depths(n("etch_clean"), f"{out}/05_three_depths.png")
    film(n("etch_clean"), f"{out}/06_deepen_true_speed_sound.mp4", "ETCH: marked -> deeper (4 s) -> fully branded (6 s), true speed")
    film(n("etch_clean"), f"{out}/06b_deepen_close_slow_x3.mp4", "the deepens, close, x3 slower", "--mute", "--slow", "3",
         lo=3900, hi=7000, crop=CLOSE)
    sheet([n("clean_tick")], ["DEEPEN 1 -> 2, every frame (ms from the 4000 tick)"], f"{out}/06c_deepen_sheet.png", 4000, -40, 560, WHELP, per_row=12, scale=1.4)
    film(n("sprawl"), f"{out}/07_spread_true_speed_sound.mp4", "SPRAWL: the mark propagates from its source (true speed)")
    if os.path.exists(n("sprawl_winnow_clean") + ".log"):
        film(n("sprawl_winnow_clean"), f"{out}/07b_spread_deepen_true_speed_sound.mp4", "SPRAWL + WINNOW: the row deepens (true speed)")
    film(n("sprawl"), f"{out}/07c_spread_slow_x4.mp4", "the spread, x4 slower", "--mute", "--slow", "4", lo=1900, hi=2800)
    film(n("long"), f"{out}/08_transfer_true_speed_sound.mp4", "TRANSFER: the front dies at 6.7 s and 8.2 s (true speed)", lo=6000, hi=9400)
    # framed on the NEW front, where the same mark seeps in (the fallen whelp at the left edge), both falls
    film(n("long"), f"{out}/08b_transfer_6700_slow_x4.mp4", "the transfer at 6.7 s (a bite and JAWS on the new front first), x4 slower",
         "--mute", "--slow", "4", lo=6600, hi=7900, crop=TRANSFER)
    film(n("long"), f"{out}/08d_transfer_8200_slow_x4.mp4", "the transfer at 8.2 s, x4 slower", "--mute", "--slow", "4",
         lo=8100, hi=9300, crop=TRANSFER)
    sheet([n("long")], ["TRANSFER at 6.7 s: the collapse, the new front's bite and JAWS, then the seep (ms from the fall)"],
          f"{out}/08c_transfer_sheet.png", 6700, -70, 1100, NEW_FRONT, per_row=9, scale=1.2)
    sheet([n("long")], ["TRANSFER at 8.2 s (ms from the fall)"], f"{out}/08e_transfer_8200_sheet.png", 8200, -70, 1100, NEW_FRONT, per_row=9, scale=1.2)
    # on a LIGHT body (the Pale Choir) the old mark's collapse is seen: it draws into its own dark centre
    sheet([n("light")], ["TRANSFER on the Pale Choir at 6.7 s: the collapse on the falling body, then the seep (ms from the fall)"],
          f"{out}/08f_transfer_light_sheet.png", 6700, -70, 1100, NEW_FRONT, per_row=9, scale=1.2)
    t1, t3 = 3300, 9900
    comparison([("FIRST SLICE", o("etch_clean")), ("SECOND PASS", n("etch_clean"))],
               [("idle, base", 3000), ("ETCH, first depth", t1), ("ETCH, deepest", t3)], f"{out}/09_dark_body.png",
               "DARK BODY (the whelps): the first slice vs the new cut, same fight, same moments")
    film(n("light"), f"{out}/10_light_body_sound.mp4", "on a LIGHT body (the Pale Choir), true speed")
    film(n("light_etch"), f"{out}/10b_light_body_etch_sound.mp4", "ETCH on the Pale Choir, true speed")
    lt3 = settled(n("light_etch"), 3, after=600)
    comparison([("FIRST SLICE", o("light_etch")), ("SECOND PASS", n("light_etch"))],
               [("DEPTH 1", settled(n("light_etch"), 1, after=600)), ("DEPTH 3", lt3)], f"{out}/10c_light_body.png",
               "LIGHT BODY (the Pale Choir): the first slice vs the new cut, same fight, same moments")
    film(n("press"), f"{out}/11_with_press_sound.mp4", "with PRESS: the crush and the cut on one body")
    film(n("fast"), f"{out}/12_with_spray_hard_hands_sound.mp4", "with SPRAY and HARD HANDS (fast TEMPO)")
    film(n("etch"), f"{out}/13_with_jaws_sound.mp4", "with JAWS: the bite on the branded attacker", lo=3500, hi=6600)
    i, _ = frame_at(n("apply_off"), 2367)
    a = anchor_at(n("apply"), 2367)
    j, _ = frame_at(n("light_off"), 2367)
    b = anchor_at(n("light"), 2367)
    material_sheet(f"{n('apply_off')}_{i:02d}.png", a[:2], f"{n('light_off')}_{j:02d}.png", b[:2], f"{out}/14_material_sheet.png")
    measures(n, f"{out}/15_measures.md")
    families_sheet(n, f"{out}/16_other_families.png")
    boss_sheet(f"{out}/17_bosses.png")
    print("done ->", out)


if __name__ == "__main__":
    main()
