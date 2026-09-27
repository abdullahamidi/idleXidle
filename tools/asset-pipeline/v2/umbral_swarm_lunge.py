#!/usr/bin/env python3
"""umbral_swarm_lunge.py -- the GLOOM WHELP's attack strip, authored as a HEAD-FIRST PREDATORY LUNGE by a joint puppet
(ADR-012 reset, second pass, 2026-09-27).

    python tools/asset-pipeline/v2/umbral_swarm_lunge.py [--out <dir>] [--poses]

WHY A PUPPET. The crouch strip the enemy matrix generated was a crouch in place; whole-sprite shears, a painted mouth and
column-wise warps were all rejected: a column warp cannot turn a crouching body toward its target (root motion OFF it
read crouch -> rear up -> settle). This tool re-poses the creature's own rest frame with a JOINT SKELETON: a dozen
handles (head, neck, shoulder, back, hip, both hands, both feet, the tail's spine) each moved to where the pose wants
them, and the image deformed between them by rigid moving-least-squares (Schaefer 2006, the puppet warp of 2D
animation tools). Every handle moves independently, so the head can drive forward and DOWN while the hips trail and
the tail streams back: the poses are authored joint by joint, not sheared. The creature's pixels, palette, line style,
eyes and the absence of a mouth are untouched; nothing is generated.

THE CANVAS. The rest head already sits at x 39 of 512 and the row draws every attack frame in the idle frame's box, so
a pose cannot push the head past the left edge: the lunge's extension is authored by the HIPS AND REAR MOVING BACK and
the body flattening toward the target, and the row's root motion (BitePresentation.Lunge) carries the whole creature
forward. The acceptance test is the strip with root motion OFF (RH_SHOT_NOROOT=1): the poses alone must read attack.

    0 REST            the original rest frame
    1 COIL            the body compresses and withdraws: head back and down, hips low, arms gathered, tail curled
    2 COIL deep       further
    3 COMMIT          the head thrusts forward and drops to chest height, the torso extends horizontal, the hips trail
    4 COMMIT fast     further, the arms trailing, a faint smear behind
    5 CONTACT         the longest silhouette: the head lowest and furthest forward, the body a line into the target,
                      the rear planted far behind, the tail streaming back
    6 FOLLOW-THROUGH  the front compresses on the target, the hips and tail catch up
    7 RECOVER         between follow-through and rest

Writes the strip and the still, or previews under --out with a key-pose sheet.
"""
import argparse
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
ART = os.path.join(REPO, "assets", "art")
STRIP = os.path.join(ART, "Animations", "Enemies", "umbral_swarm_attack", "umbral_swarm_attack_strip8_512.png")
STILL = os.path.join(ART, "enemies", "enemies", "umbral_swarm", "umbral_swarm_attack_01.png")
KEEP = os.path.join(os.path.dirname(os.path.abspath(__file__)), "keypose_sources", "umbral_swarm_attack_crouch_strip8_512.png")
F = 512

# ── the skeleton on the rest frame (source handles, canvas px) ───────────────────────────────────────────────────
SRC = {
    "head":     (128, 232),   # the head's centre (the eyes sit at its front-lower quarter)
    "crown":    (135, 150),   # the spines above the head: with `head` it sets the head's rotation
    "neck":     (208, 240),
    "shoulder": (245, 228),
    "back":     (285, 262),
    "hip":      (300, 330),
    "elbow1":   (135, 378),   # the near front arm
    "hand1":    (70, 432),
    "hand2":    (150, 462),   # the far front arm's hand
    "knee1":    (245, 400),   # the near hind leg
    "foot1":    (245, 478),
    "foot2":    (290, 492),   # the far hind leg's foot
    "tail0":    (335, 300),   # the tail's spine, base to tip
    "tail1":    (405, 275),
    "tail2":    (440, 225),
    "tail3":    (395, 165),
    "tail4":    (355, 125),
    "tail5":    (420, 92),
}

# ── the poses: each handle's TARGET (unlisted handles stay put) ─────────────────────────────────────────────────
POSES = [
    ("REST", {}),
    ("COIL", {
        "head": (176, 274), "crown": (206, 200), "neck": (238, 268), "shoulder": (268, 256), "back": (302, 292),
        "hip": (318, 360), "elbow1": (168, 400), "hand1": (128, 446), "hand2": (192, 474),
        "knee1": (268, 418), "foot1": (252, 482), "foot2": (294, 493),
        "tail0": (350, 322), "tail1": (410, 306), "tail2": (446, 266), "tail3": (424, 212), "tail4": (388, 180), "tail5": (428, 146),
    }),
    ("COIL deep", {
        "head": (214, 308), "crown": (250, 238), "neck": (262, 292), "shoulder": (290, 280), "back": (318, 312),
        "hip": (330, 380), "elbow1": (196, 416), "hand1": (166, 452), "hand2": (224, 478),
        "knee1": (282, 432), "foot1": (256, 484), "foot2": (300, 494),
        "tail0": (360, 344), "tail1": (416, 330), "tail2": (452, 290), "tail3": (438, 240), "tail4": (402, 214), "tail5": (434, 184),
    }),
    ("COMMIT", {
        "head": (96, 300), "crown": (116, 224), "neck": (166, 292), "shoulder": (206, 282), "back": (258, 286),
        "hip": (322, 326), "elbow1": (132, 386), "hand1": (76, 430), "hand2": (160, 460),
        "knee1": (276, 408), "foot1": (282, 482), "foot2": (332, 493),
        "tail0": (354, 298), "tail1": (420, 272), "tail2": (460, 228), "tail3": (454, 168), "tail4": (432, 118), "tail5": (470, 80),
    }),
    ("COMMIT fast", {
        "head": (82, 308), "crown": (100, 232), "neck": (152, 300), "shoulder": (196, 288), "back": (254, 290),
        "hip": (326, 318), "elbow1": (144, 378), "hand1": (96, 420), "hand2": (172, 452),
        "knee1": (292, 402), "foot1": (302, 482), "foot2": (352, 493),
        "tail0": (358, 292), "tail1": (426, 262), "tail2": (466, 216), "tail3": (468, 156), "tail4": (452, 108), "tail5": (482, 74),
    }),
    ("CONTACT", {
        "head": (74, 316), "crown": (88, 238), "neck": (146, 308), "shoulder": (188, 296), "back": (254, 292),
        "hip": (332, 306), "elbow1": (98, 384), "hand1": (30, 400), "hand2": (112, 440),
        "knee1": (322, 400), "foot1": (338, 484), "foot2": (392, 494),
        "tail0": (368, 286), "tail1": (432, 252), "tail2": (470, 204), "tail3": (472, 146), "tail4": (460, 100), "tail5": (488, 64),
    }),
    ("FOLLOW-THROUGH", {
        "head": (76, 342), "crown": (98, 266), "neck": (140, 330), "shoulder": (178, 320), "back": (226, 318),
        "hip": (272, 332), "elbow1": (112, 402), "hand1": (46, 440), "hand2": (128, 466),
        "knee1": (268, 414), "foot1": (286, 484), "foot2": (330, 494),
        "tail0": (306, 302), "tail1": (350, 252), "tail2": (352, 196), "tail3": (316, 150), "tail4": (272, 116), "tail5": (286, 72),
    }),
    ("RECOVER", {
        "head": (110, 268), "crown": (124, 190), "neck": (186, 262), "shoulder": (230, 246), "back": (280, 272),
        "hip": (300, 334), "elbow1": (128, 384), "hand1": (60, 436), "hand2": (146, 464),
        "knee1": (252, 404), "foot1": (258, 480), "foot2": (300, 492),
        "tail0": (338, 302), "tail1": (404, 278), "tail2": (440, 230), "tail3": (404, 170), "tail4": (362, 128), "tail5": (420, 92),
    }),
]
SMEAR = {"COMMIT fast": 18, "CONTACT": 8}


def mls_rigid(handles_from, handles_to, alpha=1.0):
    """The backward map of a rigid moving-least-squares deformation: for every output pixel v, the source position.
    `handles_from` are the handles' OUTPUT positions, `handles_to` their SOURCE positions (Schaefer et al. 2006)."""
    p = np.asarray(handles_from, dtype=np.float64)   # (n, 2) output-space handles
    q = np.asarray(handles_to, dtype=np.float64)     # (n, 2) source-space handles
    ys, xs = np.mgrid[0:F, 0:F]
    v = np.stack([xs, ys], axis=-1).astype(np.float64).reshape(-1, 2)   # (m, 2)
    d2 = ((v[:, None, :] - p[None, :, :]) ** 2).sum(-1)                  # (m, n)
    w = 1.0 / np.maximum(d2, 1e-6) ** alpha
    wsum = w.sum(1, keepdims=True)
    pstar = (w[:, :, None] * p[None]).sum(1) / wsum
    qstar = (w[:, :, None] * q[None]).sum(1) / wsum
    ph = p[None] - pstar[:, None, :]                                     # (m, n, 2)
    qh = q[None] - qstar[:, None, :]
    dv = v - pstar                                                       # (m, 2)
    # f_vec = sum_i qh_i * A_i,  A_i = w_i [ph_i ; -ph_i^perp] [dv ; -dv^perp]^T   (perp(x, y) = (-y, x))
    # A_i = w_i * [[ph.dv, ph x dv'], ...]: written out per component
    phx, phy = ph[..., 0], ph[..., 1]
    dvx, dvy = dv[:, None, 0], dv[:, None, 1]
    a11 = phx * dvx + phy * dvy
    a12 = phx * (-dvy) + phy * dvx          # ph . (-perp(dv))  with -perp(dv) = (dy, -dx) -> ph_x*dy - ph_y*dx  (sign per paper)
    a12 = phx * dvy - phy * dvx
    a21 = -(-phy * dvx + phx * dvy)         # -perp(ph) . dv  = (phy, -phx) . (dvx, dvy)
    a21 = phy * dvx - phx * dvy
    a22 = phy * dvy + phx * dvx
    fx = (w * (qh[..., 0] * a11 + qh[..., 1] * a21)).sum(1)
    fy = (w * (qh[..., 0] * a12 + qh[..., 1] * a22)).sum(1)
    fl = np.sqrt(fx * fx + fy * fy) + 1e-9
    vl = np.sqrt((dv * dv).sum(1))
    out = np.stack([fx, fy], -1) / fl[:, None] * vl[:, None] + qstar
    return out.reshape(F, F, 2)


def deform(frame, targets, alpha=1.0):
    names = list(SRC.keys())
    src = [SRC[n] for n in names]
    dst = [targets.get(n, SRC[n]) for n in names]
    m = mls_rigid(dst, src, alpha)
    a = np.asarray(frame).astype(np.float32)
    gx, gy = m[..., 0], m[..., 1]
    inside = (gx >= 0) & (gx <= F - 1) & (gy >= 0) & (gy <= F - 1)
    ix0 = np.clip(np.floor(gx).astype(int), 0, F - 2)
    iy0 = np.clip(np.floor(gy).astype(int), 0, F - 2)
    fx = np.clip(gx - ix0, 0, 1)[..., None]
    fy = np.clip(gy - iy0, 0, 1)[..., None]
    s = (a[iy0, ix0] * (1 - fx) * (1 - fy) + a[iy0, ix0 + 1] * fx * (1 - fy)
         + a[iy0 + 1, ix0] * (1 - fx) * fy + a[iy0 + 1, ix0 + 1] * fx * fy)
    s[~inside] = 0
    return Image.fromarray(np.clip(s, 0, 255).astype(np.uint8), "RGBA")


def smear(img, px):
    trail = img.transform((F, F), Image.AFFINE, (1, 0, -px, 0, 1, 0), resample=Image.BILINEAR).filter(ImageFilter.GaussianBlur(3))
    r, g, b, al = trail.split()
    trail = Image.merge("RGBA", (r, g, b, al.point(lambda v: int(v * 0.40))))
    base = Image.new("RGBA", (F, F), (0, 0, 0, 0))
    base.alpha_composite(trail)
    base.alpha_composite(img)
    return base


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", default=KEEP, help="the original (crouch) strip; its frame 0 is the rest pose")
    ap.add_argument("--out", default=None, help="write here instead of the asset tree (a preview)")
    ap.add_argument("--poses", action="store_true", help="also draw the skeleton over each pose in the sheet")
    a = ap.parse_args()
    src = Image.open(a.source).convert("RGBA")
    rest = src.crop((0, 0, F, F))
    strip = Image.new("RGBA", (8 * F, F), (0, 0, 0, 0))
    sheet = Image.new("RGB", (8 * 260, 2 * 280 + 40), (40, 36, 48))
    d = ImageDraw.Draw(sheet)
    frames = []
    for i, (label, targets) in enumerate(POSES):
        fr = rest if not targets else deform(rest, targets)
        if label in SMEAR:
            fr = smear(fr, SMEAR[label])
        frames.append(fr)
        strip.paste(fr, (i * F, 0))
        for row, im in ((0, src.crop((i * F, 0, (i + 1) * F, F))), (1, fr)):
            bg = Image.new("RGBA", (F, F), (40, 36, 48, 255))
            bg.alpha_composite(im)
            if row == 1 and a.poses:
                dd = ImageDraw.Draw(bg)
                for n, (x, y) in SRC.items():
                    tx, ty = targets.get(n, (x, y))
                    dd.ellipse((tx - 5, ty - 5, tx + 5, ty + 5), outline=(255, 120, 80, 255), width=2)
            sheet.paste(bg.convert("RGB").resize((260, 260)), (i * 260, 20 + row * 280))
        d.text((i * 260 + 4, 4), f"crouch {i}", fill=(255, 220, 120))
        d.text((i * 260 + 4, 284), f"lunge {i}: {label}", fill=(255, 220, 120))
    out_strip = os.path.join(a.out, "umbral_swarm_attack_strip8_512.png") if a.out else STRIP
    out_still = os.path.join(a.out, "umbral_swarm_attack_01.png") if a.out else STILL
    if a.out:
        os.makedirs(a.out, exist_ok=True)
    strip.save(out_strip)
    frames[0].save(out_still)
    sheet.save(os.path.join(a.out or os.path.dirname(KEEP), "umbral_swarm_lunge_sheet.png"))
    for i, fr in enumerate(frames):
        print(i, POSES[i][0], fr.getbbox())
    print("wrote", out_strip)


if __name__ == "__main__":
    main()
