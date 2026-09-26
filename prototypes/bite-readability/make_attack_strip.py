#!/usr/bin/env python3
"""make_attack_strip.py -- ENEMY ATTACK READABILITY STUDY (2026-09-26), THROWAWAY: a key-pose prototype of the Gloom
Whelp's attack strip, built by DETERMINISTIC edits of its own frames (no AI, nothing invented): the feet stay where
they are, the body coils back, then leads forward, extends at contact and compresses on landing.

    python prototypes/bite-readability/make_attack_strip.py <out dir>

Writes <out dir>/umbral_swarm_attack_strip8_512.png (the game's 8 x 512 strip format) and a labelled contact sheet.
The game films it through RH_SHOT_STRIP_FILES (AssetLibrary fixture); no repo asset changes.

The current strip (measured): the head drops ~90 px over frames 0-5 and comes back; the horizontal centroid moves
+-5 px. It is a crouch in place. The contact frame (5 of 8, HuntScreen.ContactFraction) is the LOWEST pose, so the
"bite" is the creature ducking. The prototype keeps its silhouette and material and changes only the SPACING of the
body's mass: PREPARE (coil back, low) -> COMMIT (head leads forward, a smear behind) -> CONTACT (full extension,
highest reach) -> FOLLOW-THROUGH (compressed, forward) -> RECOVER.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SRC = os.path.join(REPO, "assets", "art", "Animations", "Enemies", "umbral_swarm_attack", "umbral_swarm_attack_strip8_512.png")
F = 512
CX, YB = 256.0, 494.0       # the frame's centre column and the feet row (every frame's alpha bottom is 478-494)

# (source frame, x scale, y scale, shear px at the top toward the champion (negative = left), smear px, label)
POSES = [
    (0, 1.00, 1.00,   0, 0, "REST"),
    (2, 1.00, 0.94, +14, 0, "PREPARE: coil back, low"),
    (4, 1.00, 0.89, +22, 0, "PREPARE: deepest, pulled back"),
    (3, 0.96, 1.00, -22, 0, "COMMIT: head leads"),
    (2, 0.90, 1.02, -52, 18, "COMMIT: forward, smear behind"),
    (1, 0.88, 1.05, -66, 10, "CONTACT: full extension"),
    (5, 0.94, 0.95, -30, 0, "FOLLOW-THROUGH: compressed"),
    (6, 1.00, 0.98,  -6, 0, "RECOVER"),
]


def posed(frame: Image.Image, sx, sy, k, smear):
    """The frame under x = cx + sx (x - cx) + k (yb - y) / yb, y = yb + sy (y - yb): scaled about the feet, sheared
    so the top moves by k and the feet do not. PIL wants the inverse map."""
    e = 1.0 / sy
    f = YB * (1.0 - e)
    a = 1.0 / sx
    b = k * e / (sx * YB)
    c = CX - CX / sx - k * (YB - f) / (sx * YB)
    out = frame.transform((F, F), Image.AFFINE, (a, b, c, 0.0, e, f), resample=Image.BICUBIC)
    if smear:
        # a faint copy trailing the body (the side away from the champion), blurred along x: motion, not a second body
        trail = out.transform((F, F), Image.AFFINE, (1, 0, -smear, 0, 1, 0), resample=Image.BILINEAR)
        trail = trail.filter(ImageFilter.GaussianBlur(3))
        r, g, bl, al = trail.split()
        al = al.point(lambda v: int(v * 0.42))
        trail = Image.merge("RGBA", (r, g, bl, al))
        base = Image.new("RGBA", (F, F), (0, 0, 0, 0))
        base.alpha_composite(trail)
        base.alpha_composite(out)
        out = base
    return out


def main():
    out_dir = os.path.abspath(sys.argv[1])
    os.makedirs(out_dir, exist_ok=True)
    src = Image.open(SRC).convert("RGBA")
    frames = [src.crop((i * F, 0, (i + 1) * F, F)) for i in range(8)]
    strip = Image.new("RGBA", (8 * F, F), (0, 0, 0, 0))
    sheet = Image.new("RGB", (8 * 300, 2 * 320 + 40), (40, 36, 48))
    d = ImageDraw.Draw(sheet)
    for i, (sf, sx, sy, k, smear, label) in enumerate(POSES):
        fr = posed(frames[sf], sx, sy, k, smear)
        strip.paste(fr, (i * F, 0))
        for row, im in ((0, frames[i]), (1, fr)):
            bg = Image.new("RGBA", (F, F), (40, 36, 48, 255))
            bg.alpha_composite(im)
            sheet.paste(bg.convert("RGB").resize((300, 300)), (i * 300, 20 + row * 320))
        d.text((i * 300 + 4, 4), f"current {i}", fill=(255, 220, 120))
        d.text((i * 300 + 4, 324), f"proto {i}: {label}", fill=(255, 220, 120))
    strip.save(os.path.join(out_dir, "umbral_swarm_attack_strip8_512.png"))
    sheet.save(os.path.join(out_dir, "attack_strip_current_vs_proto.png"))
    print("wrote", out_dir)


if __name__ == "__main__":
    main()
