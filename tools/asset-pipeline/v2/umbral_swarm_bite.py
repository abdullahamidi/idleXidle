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

from PIL import Image, ImageDraw, ImageFilter

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
ART = os.path.join(REPO, "assets", "art")
STRIP = os.path.join(ART, "Animations", "Enemies", "umbral_swarm_attack", "umbral_swarm_attack_strip8_512.png")
STILL = os.path.join(ART, "enemies", "enemies", "umbral_swarm", "umbral_swarm_attack_01.png")
KEEP = os.path.join(os.path.dirname(os.path.abspath(__file__)), "keypose_sources", "umbral_swarm_attack_crouch_strip8_512.png")
F = 512
CX, YB = 256.0, 494.0   # the frame's centre column and the feet row (every original frame's alpha bottom is 478-494)

# (source frame, x scale, y scale, shear px at the top toward the champion (negative = left), smear px, label)
POSES = [
    (0, 1.00, 1.00,   0,  0, "REST"),
    (2, 1.04, 0.90, +24,  0, "PREPARE: coil, pull back"),
    (4, 1.06, 0.82, +40,  0, "PREPARE: deepest coil"),
    (3, 0.95, 1.00, -30,  0, "COMMIT: the head leads"),
    (2, 0.90, 1.02, -56, 22, "COMMIT: forward, a smear behind"),
    (1, 0.84, 1.08, -86, 14, "CONTACT: full extension"),
    (5, 0.93, 0.94, -34,  0, "FOLLOW-THROUGH: compressed, forward"),
    (6, 1.00, 0.98,  -8,  0, "RECOVER"),
]


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
    for i, (sf, sx, sy, k, smear, label) in enumerate(POSES):
        fr = Image.open(a.contact).convert("RGBA").resize((F, F), Image.LANCZOS) if (i == 5 and a.contact) else posed(frames[sf], sx, sy, k, smear)
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
