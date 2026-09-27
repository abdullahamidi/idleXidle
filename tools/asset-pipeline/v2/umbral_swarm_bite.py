#!/usr/bin/env python3
"""umbral_swarm_bite.py -- the GLOOM WHELP's attack strip, re-authored as a PREDATORY LUNGE (ADR-012 reset, 2026-09-27).

    python tools/asset-pipeline/v2/umbral_swarm_bite.py [--source <original strip>] [--out <dir>]

The strip the enemy matrix generated (spec.json enemy_matrix.umbral_swarm.attack) was a crouch in place. Two passes
of whole-sprite shears and a painted mouth were rejected (2026-09-26): the poses stayed one crouch, and the mouth read
as pasted on. This pass keeps the creature EXACTLY as designed (a dark faceless head, two bright eyes, no mouth) and
authors four genuinely different whole-body poses by COLUMN-WISE WARPS of its own frames: every column of the source
gets its own horizontal shift, vertical scale and lift, so the head end and the tail end of the body can do different
things while the feet stay on the ground.

    0 REST            the original rest frame
    1 COIL            centre of mass low and back: the front gathers toward the body, the tail gathers, the head withdraws
    2 COIL deep       lower still, the whole body compressed toward its rear
    3 COMMIT          the torso extends: the head columns lead forward and rise, the tail trails
    4 COMMIT fast     further; a faint blurred copy behind for the smear
    5 CONTACT         the longest forward silhouette: the head furthest forward, aimed down into the Seeker, the rear
                      mass visibly behind and low
    6 FOLLOW-THROUGH  the front compresses from the impact, the trailing parts catch up
    7 RECOVER         back toward rest

The still `umbral_swarm_attack_01.png` (frame 0) is written beside the strip. The crouch original is kept under
keypose_sources/ for the record. No AI: PixelLab drifted the identity twice (a crocodile-headed quadruped) and is not
used; if these warps cannot make the poses read, the honest answer is a redrawn strip, not another transform.
"""
import argparse
import os
import shutil

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
ART = os.path.join(REPO, "assets", "art")
STRIP = os.path.join(ART, "Animations", "Enemies", "umbral_swarm_attack", "umbral_swarm_attack_strip8_512.png")
STILL = os.path.join(ART, "enemies", "enemies", "umbral_swarm", "umbral_swarm_attack_01.png")
KEEP = os.path.join(os.path.dirname(os.path.abspath(__file__)), "keypose_sources", "umbral_swarm_attack_crouch_strip8_512.png")
F = 512
YB = 494.0   # the feet row (every original frame's alpha bottom is 478-494)


def smooth(v):
    v = min(1.0, max(0.0, v))
    return v * v * (3 - 2 * v)


def warp(frame, dx_front, dx_back, sy_front, sy_back, lift_front, lift_back, smear=0, pivot=0.5):
    """A column-wise warp of one 512 px frame. `u` runs 0 at the body's front (left) column to 1 at its back (right);
    each quantity is blended from its front value to its back value with a smoothstep about `pivot`. dx shifts the
    column (negative = toward the champion), sy scales it vertically about the feet, lift moves its torso and head
    (negative = up) while the legs stay planted.
    The forward x map is monotonic (the shifts vary slowly across the body), so it is inverted by interpolation and
    the output is sampled bilinearly."""
    a = np.asarray(frame).astype(np.float32)
    alpha = a[:, :, 3]
    cols = np.nonzero(alpha.max(axis=0) > 16)[0]
    x0, x1 = float(cols.min()), float(cols.max())
    xs = np.arange(F, dtype=np.float32)
    u = np.clip((xs - x0) / max(1.0, x1 - x0), 0, 1)
    w = np.array([smooth((ui - pivot + 0.5) if False else ui) for ui in u], dtype=np.float32)   # 0 front .. 1 back
    # blend front -> back
    dx = dx_front + (dx_back - dx_front) * w
    sy = sy_front + (sy_back - sy_front) * w
    lift = lift_front + (lift_back - lift_front) * w
    fwd = xs + dx                                   # where each source column lands
    # inverse x map: for each output column, the source column (monotonic, so np.interp works)
    src_x = np.interp(xs, fwd, xs, left=-1, right=-1)
    out = np.zeros_like(a)
    valid = src_x >= 0
    sx = src_x[valid]
    # per output column: the source column's vertical map, evaluated at the source column
    sy_c = np.interp(sx, xs, sy)
    lift_c = np.interp(sx, xs, lift)
    ys = np.arange(F, dtype=np.float32)
    # the vertical map, per column: y' = YB + sy (y - YB) + lift * f(y), where f fades the lift out over the legs so
    # the feet stay on the floor whatever the torso and head do (f = 0 within 50 px of the floor, 1 from 170 px up).
    # It is monotonic, so it is inverted by interpolation, one column at a time.
    feet = np.clip((YB - ys - 50.0) / 120.0, 0.0, 1.0)
    src_y = np.empty((F, sx.size), dtype=np.float32)
    for j in range(sx.size):
        fwd_y = YB + sy_c[j] * (ys - YB) + lift_c[j] * feet
        src_y[:, j] = np.interp(ys, fwd_y, ys, left=-1, right=-1)
    # bilinear sample
    gx = np.broadcast_to(sx[None, :], src_y.shape)
    gy = src_y
    ix0 = np.clip(np.floor(gx).astype(int), 0, F - 2)
    iy0 = np.clip(np.floor(gy).astype(int), 0, F - 2)
    fx = np.clip(gx - ix0, 0, 1)[..., None]
    fy = np.clip(gy - iy0, 0, 1)[..., None]
    inside = (gy >= 0) & (gy <= F - 1)   # -1 marks rows the map does not reach
    s = (a[iy0, ix0] * (1 - fx) * (1 - fy) + a[iy0, ix0 + 1] * fx * (1 - fy)
         + a[iy0 + 1, ix0] * (1 - fx) * fy + a[iy0 + 1, ix0 + 1] * fx * fy)
    s[~inside] = 0
    out[:, valid] = s
    img = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")
    if smear:
        trail = img.transform((F, F), Image.AFFINE, (1, 0, -smear, 0, 1, 0), resample=Image.BILINEAR).filter(ImageFilter.GaussianBlur(3))
        r, g, bl, al = trail.split()
        trail = Image.merge("RGBA", (r, g, bl, al.point(lambda v: int(v * 0.40))))
        base = Image.new("RGBA", (F, F), (0, 0, 0, 0))
        base.alpha_composite(trail)
        base.alpha_composite(img)
        img = base
    return img


# (source frame, dx front, dx back, sy front, sy back, lift front, lift back, smear, label)
#   front = the head end (left), back = the tail end (right); dx negative = toward the champion; lift negative = up
POSES = [
    (0,    0,    0, 1.00, 1.00,   0,   0,  0, "REST"),
    (3,  +40,  -34, 0.88, 0.84, +14, +10,  0, "COIL: the front gathers back and down, the tail gathers in, lower"),
    (4,  +56,  -50, 0.80, 0.78, +22, +16,  0, "COIL deep: compressed toward its rear, lowest, the head withdrawn"),
    (0,  -22,  +14, 1.03, 0.94, -16,  +4,  0, "COMMIT: the head leads and rises, the tail trails"),
    (0,  -32,  +20, 1.04, 0.90, -12,  +8, 20, "COMMIT fast: further, the head starting down, a smear behind"),
    (0,  -38,  +26, 0.94, 0.80, +16, +20,  8, "CONTACT: the longest silhouette, the head driven DOWN into the Seeker, the rear low and far behind"),
    (5,  -20,   -6, 0.90, 0.92, +10,  +6,  0, "FOLLOW-THROUGH: the front compresses, the tail catches up"),
    (6,  -12,   -6, 0.98, 0.98,  +2,  +2,  0, "RECOVER"),
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", default=KEEP if os.path.exists(KEEP) else STRIP, help="the original (crouch) strip")
    ap.add_argument("--out", default=None, help="write here instead of the asset tree (a preview)")
    a = ap.parse_args()
    src = Image.open(a.source).convert("RGBA")
    if not os.path.exists(KEEP):
        os.makedirs(os.path.dirname(KEEP), exist_ok=True)
        shutil.copy(a.source, KEEP)
    frames = [src.crop((i * F, 0, (i + 1) * F, F)) for i in range(8)]
    strip = Image.new("RGBA", (8 * F, F), (0, 0, 0, 0))
    sheet = Image.new("RGB", (8 * 260, 2 * 280 + 40), (40, 36, 48))
    d = ImageDraw.Draw(sheet)
    for i, (sf, dxf, dxb, syf, syb, lf, lb, smear, label) in enumerate(POSES):
        fr = frames[sf] if i == 0 else warp(frames[sf], dxf, dxb, syf, syb, lf, lb, smear)
        strip.paste(fr, (i * F, 0))
        for row, im in ((0, frames[i]), (1, fr)):
            bg = Image.new("RGBA", (F, F), (40, 36, 48, 255))
            bg.alpha_composite(im)
            sheet.paste(bg.convert("RGB").resize((260, 260)), (i * 260, 20 + row * 280))
        d.text((i * 260 + 4, 4), f"crouch {i}", fill=(255, 220, 120))
        d.text((i * 260 + 4, 284), f"lunge {i}: {label[:34]}", fill=(255, 220, 120))
    out_strip = os.path.join(a.out, "umbral_swarm_attack_strip8_512.png") if a.out else STRIP
    out_still = os.path.join(a.out, "umbral_swarm_attack_01.png") if a.out else STILL
    if a.out:
        os.makedirs(a.out, exist_ok=True)
    strip.save(out_strip)
    strip.crop((0, 0, F, F)).save(out_still)
    sheet.save(os.path.join(a.out or os.path.dirname(KEEP), "umbral_swarm_bite_sheet.png"))
    print("wrote", out_strip, "and", out_still)


if __name__ == "__main__":
    main()
