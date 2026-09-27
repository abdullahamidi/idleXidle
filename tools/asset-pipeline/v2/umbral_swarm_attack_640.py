#!/usr/bin/env python3
"""umbral_swarm_attack_640.py -- the GLOOM WHELP's attack strip on an EXTENDED ART CANVAS, from genuinely redrawn poses
(ADR-012, the third pass, 2026-09-27).

    python tools/asset-pipeline/v2/umbral_swarm_attack_640.py [--out <dir>]

THE CANVAS. The idle frame is 512 x 512 and the rest whelp already spans 428 of its 512 columns with its head at x 39,
so no attack pose authored inside it can put the head substantially closer to the champion: every earlier pass
(shears, a painted mouth, column warps, a joint puppet) hit that wall. This strip is 8 frames of 640 x 640: the
canonical 512 idle frame sits in each frame's BOTTOM-RIGHT corner (x 128.., y 128..) and the 128 extra columns on
the left are the room the lunge extends into; the extra rows are headroom. The renderer (UiKit.ResolveFrame, the
extended-canvas branch) places such a strip on its idle's scale, ground and anchor, so the whelp is never rescaled,
its feet never move, and the row's grounding is untouched.

THE POSES. COIL, COMMIT, CONTACT and FOLLOW-THROUGH are redrawn poses, NOT deformations of the rest image: PixelLab
(edit_image_pro_flash, a 256 x 204 canvas standing for the 640 x 512 art space, a strict same-creature prompt) drew
each pose from the rest frame as its reference; each was then upscaled to production scale and REPAIRED
deterministically here: the silhouette re-thresholded, the fill and the violet rim re-inked from the original's own
colours (so line weight matches the 512 idle), the eyes rebuilt from the pose's white slits with the original eye
colour, and any stray islands dropped. REST and RECOVER reuse the crouch strip's rest frame (the idle pose); the
two COIL frames are the coil pose held; the second COMMIT frame carries a faint smear. Nothing generated is used
raw; nothing is a warp of the rest frame.

    0 REST   1 COIL   2 COIL (held)   3 COMMIT   4 COMMIT (smear)   5 CONTACT   6 FOLLOW-THROUGH   7 RECOVER (= REST)
"""
import argparse
import os

import numpy as np
from PIL import Image, ImageFilter

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
ART = os.path.join(REPO, "assets", "art")
STRIP = os.path.join(ART, "Animations", "Enemies", "umbral_swarm_attack", "umbral_swarm_attack_strip8_512.png")
STILL = os.path.join(ART, "enemies", "enemies", "umbral_swarm", "umbral_swarm_attack_01.png")
HERE = os.path.dirname(os.path.abspath(__file__))
KEEP = os.path.join(HERE, "keypose_sources", "umbral_swarm_attack_crouch_strip8_512.png")
POSES_DIR = os.path.join(HERE, "keypose_sources", "umbral_swarm_lunge_poses")   # the PixelLab pose references (256 x 204)
F = 640          # the extended frame
C = 512          # the canonical idle frame
OFF = F - C      # 128: where the canonical frame sits (x and y)
SOLE = 494       # the rest frame's lowest opaque row in the 512 canvas (its feet), the row every pose stands on
FILL = (11, 8, 14, 255)
RIM = (74, 58, 96, 255)
EYE = (240, 240, 250, 255)


def repair(pose_png, smear=0, dx=0):
    """One redrawn pose (256 x 204, standing for 640 x 512) to a production 640 x 640 frame; `dx` shifts the whole
    pose (positive = back, away from the champion) so the poses' reaches order correctly: CONTACT is the furthest."""
    src = Image.open(pose_png).convert("RGBA")
    up = src.resize((F, C), Image.LANCZOS)                       # 2.5x, soft
    a = np.asarray(up).astype(np.float32)
    alpha = a[:, :, 3]
    mask = alpha > 110                                           # the silhouette, re-thresholded
    # an opening (erode then dilate, 2 px) drops the specks the upscale left without touching the body's spines
    m = Image.fromarray((mask * 255).astype(np.uint8), "L")
    m = m.filter(ImageFilter.MinFilter(5)).filter(ImageFilter.MaxFilter(5))
    opened = np.asarray(m) > 127
    mask = opened | (mask & (np.asarray(m.filter(ImageFilter.MaxFilter(9))) > 127))   # keep thin spines attached to the body
    # the eyes: the pose's bright pixels inside the body's FRONT third (the head leads; a bright speck the redraw left
    # elsewhere on the body is fill, not an eye), grown by a texel
    lum = a[:, :, :3].mean(axis=2)
    cols = np.nonzero(mask.any(axis=0))[0]
    front = np.zeros_like(mask)
    if cols.size:
        front[:, cols.min():cols.min() + (cols.max() - cols.min()) // 3] = True
    eyes = mask & front & (lum > 150)
    # the eyes are the identity: grown by two texels (the redraw's slits are thin at 2.5x) and laid on AFTER the
    # edge softening, so the blur cannot grey them into the black head
    eyes = (np.asarray(Image.fromarray((eyes * 255).astype(np.uint8), "L").filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.MaxFilter(5))) > 127) & mask
    # the rim: the outer 3 px of the silhouette
    inner = np.asarray(Image.fromarray((mask * 255).astype(np.uint8), "L").filter(ImageFilter.MinFilter(7))) > 127
    rim = mask & ~inner
    out = np.zeros((C, F, 4), np.uint8)
    out[mask] = FILL
    out[rim] = RIM
    img = Image.fromarray(out, "RGBA")
    # soften the re-inked edge by one texel so it matches the hand-drawn strip's anti-aliasing
    img = img.filter(ImageFilter.GaussianBlur(0.6))
    eye_layer = np.zeros((C, F, 4), np.uint8)
    eye_layer[eyes] = EYE
    eye_img = Image.fromarray(eye_layer, "RGBA")
    if eyes.sum() < 40 and cols.size:
        # the redraw left this pose's eyes dim (the COMMIT streak): the two slits are rebuilt at the head's front,
        # where they sit on every other frame, slanted like the original's
        from PIL import ImageDraw as _D
        x0 = int(cols.min())
        head = mask[:, x0:x0 + 90]
        rows_ = np.nonzero(head.any(axis=1))[0]
        cy = int(np.median(rows_)) if rows_.size else C // 2
        dd = _D.Draw(eye_img)
        for (ex, ey) in ((x0 + 34, cy - 2), (x0 + 66, cy - 10)):
            dd.polygon([(ex - 9, ey + 3), (ex + 2, ey - 4), (ex + 9, ey - 3), (ex - 2, ey + 4)], fill=EYE)
    img.alpha_composite(eye_img)
    # the sole: the renderer grounds every frame on the IDLE's lowest row, so a pose whose feet the redraw put a few
    # texels lower would sink through the floor; each pose is lifted so its lowest opaque row is the rest frame's
    frame = Image.new("RGBA", (F, F), (0, 0, 0, 0))
    bb = img.getbbox()
    lift = (bb[3] - 1) - SOLE if bb else 0
    frame.alpha_composite(img, (dx, OFF - max(0, lift)))
    if smear:
        trail = frame.transform((F, F), Image.AFFINE, (1, 0, -smear, 0, 1, 0), resample=Image.BILINEAR).filter(ImageFilter.GaussianBlur(3))
        tr, tg, tb, ta = trail.split()
        trail = Image.merge("RGBA", (tr, tg, tb, ta.point(lambda v: int(v * 0.35))))
        base = Image.new("RGBA", (F, F), (0, 0, 0, 0))
        base.alpha_composite(trail)
        base.alpha_composite(frame)
        frame = base
    return frame


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=None, help="write here instead of the asset tree (a preview)")
    a = ap.parse_args()
    rest512 = Image.open(KEEP).convert("RGBA").crop((0, 0, C, C))
    rest = Image.new("RGBA", (F, F), (0, 0, 0, 0))
    rest.alpha_composite(rest512, (OFF, OFF))
    coil = repair(os.path.join(POSES_DIR, "coil.png"))
    # the COMMIT pose is drawn long; it sits 48 px behind the CONTACT so the reach grows into the contact, never back
    commit = repair(os.path.join(POSES_DIR, "commit.png"), dx=48)
    commit_fast = repair(os.path.join(POSES_DIR, "commit.png"), smear=18, dx=24)
    contact = repair(os.path.join(POSES_DIR, "contact.png"))
    follow = repair(os.path.join(POSES_DIR, "follow.png"))
    frames = [rest, coil, coil, commit, commit_fast, contact, follow, rest]
    labels = ["REST", "COIL", "COIL (held)", "COMMIT", "COMMIT (smear)", "CONTACT", "FOLLOW-THROUGH", "RECOVER"]
    strip = Image.new("RGBA", (8 * F, F), (0, 0, 0, 0))
    for i, fr in enumerate(frames):
        strip.paste(fr, (i * F, 0))
    out_strip = os.path.join(a.out, "umbral_swarm_attack_strip8_512.png") if a.out else STRIP
    out_still = os.path.join(a.out, "umbral_swarm_attack_01.png") if a.out else STILL
    if a.out:
        os.makedirs(a.out, exist_ok=True)
    strip.save(out_strip)
    rest512.save(out_still)            # the still is the canonical 512 rest pose (the static fallback draws it as the idle)
    # the key-pose sheet: the four redrawn poses at half size with the canonical frame outlined
    from PIL import ImageDraw
    sheet = Image.new("RGB", (8 * 330, 360), (40, 36, 48))
    d = ImageDraw.Draw(sheet)
    for i, (fr, label) in enumerate(zip(frames, labels)):
        bg = Image.new("RGBA", (F, F), (40, 36, 48, 255))
        dd = ImageDraw.Draw(bg)
        dd.rectangle((OFF, OFF, F - 1, F - 1), outline=(90, 80, 110, 255))
        bg.alpha_composite(fr)
        sheet.paste(bg.convert("RGB").resize((320, 320)), (i * 330 + 5, 30))
        d.text((i * 330 + 6, 8), f"{i} {label}", fill=(255, 220, 120))
    sheet.save(os.path.join(a.out or os.path.join(HERE, "keypose_sources"), "umbral_swarm_attack_640_sheet.png"))
    for i, fr in enumerate(frames):
        print(i, labels[i], fr.getbbox())
    print("wrote", out_strip)


if __name__ == "__main__":
    main()
