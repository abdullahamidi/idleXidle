#!/usr/bin/env python3
"""press_hard_evidence.py -- PRESS, the PIXEL-HARD / IMPACT polish (ADR-011, the owner's brief 2026-09-28: the Concept A
direction is approved; the craft was "too smooth, too soft and too vector-like", the tick lacked hardness). The review's
items: the CURRENT first slice against the PIXEL-HARD pass, true speed, muted, close, the asset sheet (smooth vs pixel-hard
parts), the impact frames, repeated ticks, the SPRAY / HARD HANDS / JAWS overlaps, and the micro push against none.

Three iterations: the FIRST (its tag kept for the record) crushed with a "> TARGET <" pair of chevrons and tried a micro push;
the judges read the chevrons as a lock-on reticle that pinched the next creature, and the push as invisible at true speed,
so the SECOND drops both (the push comparison is therefore filmed from the first iteration's takes); the second lit the
whole crush arcs pale on the tick (as bright as SPRAY's hit), so the THIRD, the pass, lights only their pressing edge.

    bash tools/asset-pipeline/films_press.sh <tag> normal repeated fast yield
    PYTHONUTF8=1 python tools/asset-pipeline/press_hard_evidence.py <current tag> <first iteration tag> <second iteration tag> <tag> <out dir>
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

from foundation_evidence import REPO, film, grid, read, SMALL

TICK = 6000
IMPACT = [(-33, "PRE-CONTACT"), (0, "ARRIVAL"), (33, "DEEPEST CRUSH"), (67, "HOLD"), (117, "RELEASE")]
BOX = (880, 560, 1420, 960)
PARTS = os.path.join(REPO, "assets", "art", "VFX", "parts")
HISTORY = os.path.join(REPO, "tools", "asset-pipeline", "v2", "keypose_sources", "history")


def frame_at(pre, t):
    shots = read(pre + ".log")
    i = min(shots, key=lambda q: abs(shots[q][1] - t))
    return Image.open(f"{pre}_{i:02d}.png").convert("RGB"), shots[i][1]


def impact_sheet(rows, out, scale=0.8):
    bw, bh = int((BOX[2] - BOX[0]) * scale), int((BOX[3] - BOX[1]) * scale)
    img = Image.new("RGB", (len(IMPACT) * (bw + 4) + 4, len(rows) * (bh + 44) + 8), (20, 17, 14))
    d = ImageDraw.Draw(img)
    for r, (pre, label) in enumerate(rows):
        y0 = 4 + r * (bh + 44)
        d.text((6, y0), label, fill=(245, 220, 150), font=SMALL)
        for c, (dt, name) in enumerate(IMPACT):
            im, t = frame_at(pre, TICK + dt)
            x = 4 + c * (bw + 4)
            d.text((x + 2, y0 + 20), f"{t - TICK:+.0f} ms  {name}", fill=(235, 200, 120), font=SMALL)
            img.paste(im.crop(BOX).resize((bw, bh), Image.LANCZOS), (x, y0 + 40))
    img.save(out)
    print(os.path.basename(out), img.size)


def tinted(path, cell, cells, colour, alpha=1.0):
    im = Image.open(path).convert("RGBA")
    w = im.width // cells
    x = np.asarray(im.crop((cell * w, 0, cell * w + w, im.height))).astype(np.float32) / 255
    a = x[..., 3:4] * alpha
    rgb = x[..., :3] * np.array(colour, np.float32) / 255
    return rgb, a


def over(bg, rgb, a, at):
    h, w = a.shape[:2]
    X, Y = at
    reg = bg[Y:Y + h, X:X + w]
    bg[Y:Y + h, X:X + w] = reg * (1 - a) + rgb * a


def add(bg, rgb, a, at):
    """ADDED as the light pass adds it (premultiplied additive)."""
    h, w = a.shape[:2]
    X, Y = at
    bg[Y:Y + h, X:X + w] = np.clip(bg[Y:Y + h, X:X + w] + rgb * a, 0, 1)


def asset_sheet(out):
    """The smooth first-slice parts over the pixel-hard ones, tinted as the runtime tints them (the wave's body in the
    material pass, its edge ADDED in the light pass), over the dungeon floor's grey and over black; then the pixel-hard
    states (the one-frame-late echo, the eroding dissolve, the clamp's three erosion steps; the arcs at the tick, their heat added); then 3x NEAREST (the pixel read)."""
    wave_s, clamp_s = os.path.join(HISTORY, "fxp_seeker_press_wave_smooth.png"), os.path.join(HISTORY, "fxp_seeker_press_clamp_smooth.png")
    wave_p, clamp_p = os.path.join(PARTS, "fxp_seeker_press_wave.png"), os.path.join(PARTS, "fxp_seeker_press_clamp.png")
    rows = [("CURRENT (the first slice, smooth)", wave_s, 2, clamp_s, 1), ("PIXEL-HARD", wave_p, 4, clamp_p, 5)]
    HALF, ROW = 1000, 580
    W, H = 2 * HALF, 40 + 3 * ROW
    img = np.zeros((H, W, 3), np.float32)
    img[:, :HALF] = np.array([58, 56, 54]) / 255
    img[:, HALF:] = np.array([12, 10, 16]) / 255
    wave_c, rim_c, clamp_c = (176, 146, 238), (236, 224, 255), (196, 138, 250)
    for r, (label, wave, wc, clamp, cc) in enumerate(rows):
        for side in (0, 1):
            ox, oy = side * HALF + 20, 40 + r * ROW
            over(img, *tinted(wave, 0, wc, wave_c, 0.9), (ox, oy))
            add(img, *tinted(wave, 1, wc, rim_c, 0.9 * (0.6 if wc == 4 else 0.8)), (ox, oy))   # the rim on arrival (WaveGlowShare)
            over(img, *tinted(clamp, 0, cc, clamp_c, 0.9), (ox + 230, oy + 40))
            # the tick's heat, ADDED: the first slice lit the whole arc hot; the pixel-hard one lights only its edge cell
            add(img, *tinted(clamp, 4 if cc == 5 else 0, cc, (226, 208, 255), 0.95 * (0.7 if cc == 5 else 1.0)), (ox + 230, oy + 40))
    for side in (0, 1):                                                  # the pixel-hard states
        ox, oy = side * HALF + 20, 40 + 2 * ROW
        over(img, *tinted(wave_p, 2, 4, wave_c, 0.25), (ox, oy))           # the echo, one frame late, at EchoAlpha
        over(img, *tinted(wave_p, 3, 4, wave_c, 0.9), (ox + 210, oy))      # the dissolve after arrival
        for k in (1, 2, 3):
            over(img, *tinted(clamp_p, k, 5, clamp_c, 0.9), (ox + 420, oy + (k - 1) * 170))
    pil = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    d = ImageDraw.Draw(pil)
    for r, text in enumerate([rows[0][0], rows[1][0], "PIXEL-HARD states: the echo (one frame behind) | the dissolve | the clamp's release, 3 erosion steps"]):
        d.text((24, 12 + r * ROW), text + ("   (left: over the floor's grey; right: over black)" if r < 2 else ""),
               fill=(245, 220, 150), font=SMALL)
    crop = lambda r: (20 + 100, 40 + r * ROW + 60, 20 + 380, 40 + r * ROW + 250)   # noqa: E731 - the wave's front and the clamp's tip
    zooms = [pil.crop(crop(r)).resize(((crop(r)[2] - crop(r)[0]) * 3, (crop(r)[3] - crop(r)[1]) * 3), Image.NEAREST) for r in (0, 1)]
    zw, zh = zooms[0].size
    out_img = Image.new("RGB", (W + zw + 20, max(H, 2 * zh + 70)), (20, 17, 14))
    out_img.paste(pil, (0, 0))
    od = ImageDraw.Draw(out_img)
    for r, z in enumerate(zooms):
        od.text((W + 16, 10 + r * (zh + 30)), ("CURRENT" if r == 0 else "PIXEL-HARD") + ", 3x NEAREST (the pixel read)", fill=(245, 220, 150), font=SMALL)
        out_img.paste(z, (W + 10, 30 + r * (zh + 30)))
    out_img.save(out)
    print(os.path.basename(out), out_img.size)


def main():
    cur, first, second, tag, out = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4], os.path.abspath(sys.argv[5])
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png", ".md")) and f[0].isdigit():
            os.remove(os.path.join(out, f))
    c = lambda k: os.path.join(REPO, "build", "shots", "press", cur, k)   # noqa: E731
    i1 = lambda k: os.path.join(REPO, "build", "shots", "press", first, k)   # noqa: E731
    i2 = lambda k: os.path.join(REPO, "build", "shots", "press", second, k)   # noqa: E731
    n = lambda k: os.path.join(REPO, "build", "shots", "press", tag, k)   # noqa: E731
    film(c("normal"), f"{out}/01_current_first_slice_true_speed_sound.mp4", "CURRENT: the first slice (smooth)")
    film(n("normal"), f"{out}/02_pixel_hard_true_speed_sound.mp4", "PIXEL-HARD: the same sentence, pixel material, a hard tick")
    film(n("normal"), f"{out}/03_pixel_hard_MUTED.mp4", "PIXEL-HARD, muted", "--mute")
    grid([n("normal")], ["PIXEL-HARD, close"], f"{out}/04_close_crop_true_speed_MUTED.mp4", "560,480,900,440", shrink=1.0, lo=5250, hi=6600)
    asset_sheet(f"{out}/05_asset_sheet_smooth_vs_pixel_hard.png")
    impact_sheet([(c("normal"), "CURRENT (the first slice)"),
                  (i1("normal"), "PIXEL-HARD, first iteration (the '> <' chevrons, dropped by the judges)"),
                  (i2("normal"), "PIXEL-HARD, second iteration (the whole arcs lit pale on the tick, as bright as SPRAY's hit)"),
                  (n("normal"), "PIXEL-HARD (the pass)")],
                 f"{out}/06_impact_frames_PRE_ARRIVAL_DEEPEST_HOLD_RELEASE.png")
    film(n("repeated"), f"{out}/07_repeated_ticks_sound.mp4", "repeated ticks (one picture per 2 frames)")
    film(n("fast"), f"{out}/08_with_spray_sound.mp4", "fast TEMPO: SPRAY at 1400, then the PRESS tick at 2000", lo=880, hi=2500)
    film(n("fast"), f"{out}/09_with_hard_hands_sound.mp4", "fast TEMPO: HARD HANDS at 3600, then the tick at 4000 (SPRAY right after)", lo=3100, hi=4700)
    film(n("yield"), f"{out}/10_with_jaws_true_speed_sound.mp4", "the tick at 10000 while JAWS bites the same creature: PRESS gives way")
    grid([i1("normal"), i1("normal_push")], ["NO PUSH (first iteration)", "MICRO PUSH, 6 px (first iteration)"],
         f"{out}/11_push_vs_no_push_close_MUTED.mp4", "900,560,520,400", shrink=1.0, lo=5700, hi=6400)
    lines = [l.rstrip("\n") for l in open(n("normal") + ".log", encoding="utf-8", errors="replace") if "\tfield-" in l]
    draws = [l for l in lines if "field-draw" in l]
    open(f"{out}/12_trace.md", "w", encoding="utf-8").write(
        "# PRESS on the playhead (RH_PRESENT_TRACE), the pixel-hard normal take\n\n```\n" + "\n".join(lines) + "\n```\n\n"
        + f"field-draw lines: {len(draws)}; allocation fields: " + ", ".join(sorted({l.split(chr(9))[-1] for l in draws})) + "\n")
    print("done ->", out)


if __name__ == "__main__":
    main()
