#!/usr/bin/env python3
"""umbral_swarm_bite.py -- the GLOOM WHELP's attack strip, re-authored as a BITE (ADR-012, 2026-09-26).

    python tools/asset-pipeline/v2/umbral_swarm_bite.py [--source <original strip>] [--contact <png>] [--out <dir>]

The strip the enemy matrix generated (spec.json enemy_matrix.umbral_swarm.attack) was a crouch in place: the head sank
over frames 1-3, the contact frame (5 of 8, HuntScreen.ContactFraction) equalled the frame before it within 2 px, the
horizontal centroid never moved, and the only fast motion was standing back up after the hit (the foundation study,
production/qa/evidence/bite-readability/). This re-authors the eight frames from the creature's OWN frames by
deterministic edits, so its identity, proportions, material and lighting are untouched:

    0 REST            the original rest frame
    1 PREPARE         compress, pull back                  (the body coils)
    2 PREPARE deep    lower, further back                  (deepest coil)
    3 COMMIT          the head leads forward               (the coil releases)
    4 COMMIT fast     forward, a smear trails the body     (acceleration)
    5 CONTACT         the strongest forward silhouette     (full extension, taller, the head furthest left)
    6 FOLLOW-THROUGH  compressed on landing, still forward
    7 RECOVER         back toward rest

Each pose is the source frame under one affine map about the FEET (the feet do not move: the arena bottom-anchors the
figure, and the row's presentation lunge carries the travel), a shear whose top moves toward the champion, plus a
faint blurred copy behind the two commit frames for the smear. --contact substitutes a hand-picked contact frame (a
PixelLab key-pose edit, checked for identity) for frame 5's transform. The still `umbral_swarm_attack_01.png` (frame 0)
is written beside the strip. The original strip is kept under keypose_sources/ for the record.

PIXELLAB, tried once and REJECTED (2026-09-26): edit_image_pro_flash job ae930771-a594-4091-8e0e-f4c3d289044f, the rest
frame at 256 px with a controlled "same creature, lunging bite, mouth open" instruction. The result lunged, but it was
another creature: a quadruped with a crocodile-like head in profile, one eye, no purple rim light, none of the whelp's
hunched round-headed shape. Identity drift, so no PixelLab frame is in this strip; every pose is the creature's own.
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
CX, YB = 256.0, 494.0   # the frame's centre column and the feet row (every original frame's alpha bottom is 478-494)

# (source frame, x scale, y scale, shear px at the top toward the champion (negative = left), smear px, maw, label)
#   maw: how far the Shadow maw is open on this pose (0 = the normal face; 1 = fully open; a small value = shut on the
#   bite, the teeth meeting). It exists on the commit and contact poses only.
POSES = [
    (0, 1.00, 1.00,   0,  0, 0.00, "REST"),
    (2, 1.04, 0.90, +24,  0, 0.00, "PREPARE: coil, pull back"),
    (4, 1.06, 0.82, +40,  0, 0.00, "PREPARE: deepest coil"),
    (3, 0.95, 1.00, -30,  0, 0.45, "COMMIT: the head leads, the maw opens"),
    (2, 0.90, 1.02, -56, 22, 1.00, "PRE-CONTACT: thrust, maw OPEN, teeth"),
    (1, 0.84, 1.08, -86, 14, 0.12, "CONTACT: full extension, the maw SHUTS"),
    (5, 0.93, 0.94, -34,  0, 0.00, "FOLLOW-THROUGH: compressed, the face closes"),
    (6, 1.00, 0.98,  -8,  0, 0.00, "RECOVER"),
]

MAW_CAVITY = (88, 60, 126, 255)     # the rim-light violet, lighter than the body: a cavity opening in the shadow
MAW_TOOTH = (230, 222, 240, 255)    # the pale of the eyes


def paint_maw(frame, openness):
    """THE SHADOW MAW: character animation art, not an effect. On the commit and contact poses the front-lower part of
    the dark head opens into a short maw: a violet cavity (negative space in the shadow) with three pale teeth hanging
    from its upper lip and two rising from its lower. It is placed from the creature's own eyes (the two pale glows),
    scaled by their spacing, and CLIPPED TO THE HEAD'S OWN SILHOUETTE, so it can never grow a snout or leave the head
    the idle creature has; it opens downward inside the face, and at `openness` near 0 it is a shut slit with the
    teeth meeting (the bite). The idle strip is untouched: the mouth exists only for the attack."""
    if openness <= 0:
        return frame
    a = np.asarray(frame).astype(int)
    bright = (a[:, :, 0] > 170) & (a[:, :, 1] > 170) & (a[:, :, 2] > 170) & (a[:, :, 3] > 128)
    ys, xs = np.nonzero(bright)
    if len(xs) < 50:
        return frame
    ex, ey = xs.mean(), ys.mean()
    s = (xs.max() - xs.min()) / 74.0            # the eyes' span at rest is 74 px
    # the eyes sit in the head's lower front; the maw is the band between them and the chin, hinged at the back
    # the eyes sit at the head's front-bottom; the mouth is the lower arc UNDER and BEHIND them (the chin runs from
    # just under the front eye back to ~38 px under the back eye), hinged at the back, opening down past the chin
    front = (ex - 30 * s, ey + 9 * s)
    hinge = (ex + 40 * s, ey + 24 * s)
    gap = (8 + 30 * openness) * s               # how far the lower jaw drops (past the chin when open: a jaw, not a snout)
    lower_front = (front[0] - 4 * s, front[1] + gap)
    lower_back = (hinge[0] + 2 * s, hinge[1] + gap * 0.45)
    cavity = [front, hinge, lower_back, lower_front]
    layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.polygon(cavity, fill=MAW_CAVITY)
    # the jaw's own dark rim under the cavity, so the open mouth has a lower lip and not only a hole
    d.line([lower_front, lower_back], fill=(22, 16, 30, 255), width=max(2, int(4 * s)))
    # teeth: three from the upper lip pointing down, two from the lower lip pointing up; they meet when the maw shuts
    for t in (0.15, 0.42, 0.70):
        x = front[0] + (hinge[0] - front[0]) * t
        ly = front[1] + (hinge[1] - front[1]) * t
        h = min(gap * 0.62, 14 * s)
        d.polygon([(x - 4.5 * s, ly), (x + 4.5 * s, ly), (x, ly + h)], fill=MAW_TOOTH)
    for t in (0.28, 0.56):
        x = lower_front[0] + (lower_back[0] - lower_front[0]) * t
        ly = lower_front[1] + (lower_back[1] - lower_front[1]) * t
        h = min(gap * 0.5, 10 * s)
        d.polygon([(x - 4 * s, ly), (x + 4 * s, ly), (x, ly - h)], fill=MAW_TOOTH)
    # clip to the head: the creature's own body, allowed a drop below the chin for the open jaw (never sideways,
    # never a snout: the head's proportions stay the idle creature's)
    body = a[:, :, 3] > 100
    drop = int(round(26 * s * openness))
    dilated = body.copy()
    if drop > 0:
        dilated[drop:, :] |= body[:-drop, :]
    mask = Image.fromarray(dilated.astype(np.uint8) * 255, "L")
    clipped = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    clipped.paste(layer, (0, 0), mask)
    out = frame.copy()
    out.alpha_composite(clipped)
    return out


def posed(frame, sx, sy, k, smear):
    """x = cx + sx (x - cx) + k (yb - y) / yb ; y = yb + sy (y - yb): scaled about the feet, sheared so the top moves by k
    and the feet do not. PIL wants the inverse map."""
    e = 1.0 / sy
    f = YB * (1.0 - e)
    a = 1.0 / sx
    b = k * e / (sx * YB)
    c = CX - CX / sx - k * (YB - f) / (sx * YB)
    out = frame.transform((F, F), Image.AFFINE, (a, b, c, 0.0, e, f), resample=Image.BICUBIC)
    if smear:
        trail = out.transform((F, F), Image.AFFINE, (1, 0, -smear, 0, 1, 0), resample=Image.BILINEAR).filter(ImageFilter.GaussianBlur(3))
        r, g, bl, al = trail.split()
        trail = Image.merge("RGBA", (r, g, bl, al.point(lambda v: int(v * 0.40))))
        base = Image.new("RGBA", (F, F), (0, 0, 0, 0))
        base.alpha_composite(trail)
        base.alpha_composite(out)
        out = base
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", default=KEEP if os.path.exists(KEEP) else STRIP, help="the original (crouch) strip")
    ap.add_argument("--contact", default=None, help="a hand-picked 512x512 RGBA contact frame for frame 5")
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
    for i, (sf, sx, sy, k, smear, maw, label) in enumerate(POSES):
        fr = Image.open(a.contact).convert("RGBA").resize((F, F), Image.LANCZOS) if (i == 5 and a.contact) else posed(frames[sf], sx, sy, k, smear)
        fr = paint_maw(fr, maw)
        strip.paste(fr, (i * F, 0))
        for row, im in ((0, frames[i]), (1, fr)):
            bg = Image.new("RGBA", (F, F), (40, 36, 48, 255))
            bg.alpha_composite(im)
            sheet.paste(bg.convert("RGB").resize((260, 260)), (i * 260, 20 + row * 280))
        d.text((i * 260 + 4, 4), f"crouch {i}", fill=(255, 220, 120))
        d.text((i * 260 + 4, 284), f"bite {i}: {label}", fill=(255, 220, 120))
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
