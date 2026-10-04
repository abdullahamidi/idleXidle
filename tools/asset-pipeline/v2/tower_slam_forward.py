#!/usr/bin/env python3
"""tower_slam_forward -- the Tower's basic attack hammer brought FORWARD on its contact frames (a hand repaint).

    python tools/asset-pipeline/v2/tower_slam_forward.py            # rewrite frames 4-6 of the attack strip
    python tools/asset-pipeline/v2/tower_slam_forward.py --sheet D  # also write a before / after sheet to D

THE PROBLEM (the Phase 1 review of the real footage, item 04). The generated strip ("leaning hard onto the maul and
driving its head down into the ground") brings the hammer head straight DOWN onto the floor at his own feet on frames
4-6, while the swing's contact (dust, flat ring, flash, number) is on the creature ~360 px away: it read as a ground
slam (UPSET's / SLOW FALL's weight language), not "the hammer hit the creature". The fix the review asked for is a
key-pose edit of frames 4-6 so the arc ends FORWARD, the head out past his lead foot at knee-to-waist height toward the
row; contact stays on frame 5 and the step-in stays 20 % (no bigger step, no ground line).

WHY A HAND REPAINT, NOT A GENERATION. The strip is 512 px a frame; PixelLab's inpaint keeps unmasked bytes but only up
to 256 px, and an edit redraws the whole figure. The hand repaint keeps every pixel of his body that the generator drew:

  * the BODY of frames 4-6 is his settle frame (7): the same standing knight, the hand that held the haft upright now
    at the start of a horizontal haft (frames 4-6 are 60-100 ms each: the hammer, not the stance, carries the swing);
    the upright hammer head beside his lead leg is cut away and the cut edge given his outline;
  * the HEAD is frame 1's hammer head (the rune face, the bevels), mirrored so the haft enters it from his side, and
    turned per frame along the arc: up-forward on 4 (coming over), level on 5 (the contact, at waist-to-knee height
    past the lead foot), dipping on 6 (the follow-through);
  * the HAFT is drawn in his strip's own wood (sampled from frame 1's haft), outline first, from the gripping hand to
    the head.

The originals stay in git history (BASE_REV); the script always starts from them, so it is idempotent. The StrikeHand
sockets it prints (the head's striking face, as fractions of the 512 frame, and the force's direction) are what the
strip's .clip.json carries on frames 4-6.
Runs under the Python that has numpy (`python`, not the `py` launcher here).
"""
from __future__ import annotations

import argparse
import io
import math
import os
import subprocess

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
REL = "assets/art/Animations/Roster/tower_attack/char_tower_attack_strip8_512.png"
BASE_REV = "702576d6"   # the strip as generated
F = 512

OUTLINE = (20, 12, 18, 255)
WOOD = [(52, 30, 38, 255), (86, 52, 46, 255), (128, 82, 56, 255), (168, 116, 72, 255)]   # dark .. light

# frame 1's hammer head: its box, and where the haft enters it (frame-local px)
HEAD_BOX = (104, 98, 189, 214)
HEAD_ENTRY = (184, 168)
HEAD_TO_HANDS = (300 - 190, 240 - 172)   # the haft's direction in frame 1, entry -> hands

# frame 7: the gripping hand (the haft's root) and the upright head beside the lead leg that is cut away
HAND = (338, 326)
CUT = dict(x0=336, y0=339)

# per contact frame: where the head's entry sits (frame-local px) -- the arc down and forward
POSES = {
    4: dict(entry=(410, 268)),   # coming over: up-forward
    5: dict(entry=(424, 326)),   # the contact: level, waist-to-knee height, past the lead foot
    6: dict(entry=(418, 352)),   # the follow-through: dipping
}


def _load_base() -> Image.Image:
    data = subprocess.run(["git", "show", f"{BASE_REV}:{REL}"], cwd=REPO, check=True, capture_output=True).stdout
    return Image.open(io.BytesIO(data)).convert("RGBA")


def _frame(strip: Image.Image, i: int) -> Image.Image:
    return strip.crop((i * F, 0, (i + 1) * F, F))


def _head(f1: Image.Image) -> tuple[Image.Image, tuple[float, float], float]:
    """Frame 1's head, mirrored: the sprite, its haft entry, and the direction (radians, y up) from entry to hands."""
    x0, y0, x1, y1 = HEAD_BOX
    head = f1.crop(HEAD_BOX)
    a = np.array(head)
    # the gauntlet that overlaps the head's lower right in frame 1 is not the hammer
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]]
    arm = (xx + x0 > 178) & (yy + y0 > 182)
    a[arm, 3] = 0
    head = Image.fromarray(a).transpose(Image.FLIP_LEFT_RIGHT)
    entry = (x1 - 1 - HEAD_ENTRY[0], HEAD_ENTRY[1] - y0)
    dx, dy = -HEAD_TO_HANDS[0], HEAD_TO_HANDS[1]
    return head, entry, math.atan2(-dy, dx)


def _cut_upright_head(base: np.ndarray) -> np.ndarray:
    """Clear the upright head beside the lead leg, then give the cut edge his outline."""
    out = base.copy()
    out[CUT["y0"]:, CUT["x0"]:, :] = 0
    # the head's light top face that overhung the leg's edge
    for y in range(CUT["y0"], F):
        for x in range(CUT["x0"] - 14, CUT["x0"]):
            r, g, b, al = out[y, x]
            if al and min(r, g, b) > 150:
                out[y, x] = 0
    # the upright head's foot that showed under the lead leg's boot
    out[440:, CUT["x0"] - 6:CUT["x0"], :] = 0
    # outline the new silhouette edge (opaque texels that now border a cleared one, inside the cut's band)
    alpha = out[..., 3] > 0
    edge = np.zeros_like(alpha)
    for y in range(CUT["y0"] - 2, F - 1):
        for x in range(CUT["x0"] - 16, CUT["x0"] + 2):
            if alpha[y, x] and (not alpha[y, x + 1] or not alpha[y + 1, x] or not alpha[y, x - 1]):
                edge[y, x] = True
    out[edge] = OUTLINE
    return out


def _haft(img: Image.Image, a: tuple[float, float], b: tuple[float, float]) -> None:
    """The haft from a to b: outline, then three bands of wood (light on top)."""
    px = img.load()
    ax, ay = a
    bx, by = b
    n = int(max(abs(bx - ax), abs(by - ay))) + 1
    length = math.hypot(bx - ax, by - ay)
    nx, ny = -(by - ay) / length, (bx - ax) / length   # the normal, pointing "down" for a haft to the right
    for half, colour_of in ((6, lambda o: OUTLINE), (4, None)):
        for s in range(n + 1):
            t = s / n
            cx, cy = ax + (bx - ax) * t, ay + (by - ay) * t
            for o10 in range(-half * 10, half * 10 + 1, 5):
                o = o10 / 10
                x, y = int(round(cx + nx * o)), int(round(cy + ny * o))
                if not (0 <= x < F and 0 <= y < F):
                    continue
                if colour_of is not None:
                    px[x, y] = colour_of(o)
                else:
                    band = 3 if o < -2 else 2 if o < 0.5 else 1 if o < 2.5 else 0
                    px[x, y] = WOOD[band]


def _rotate_smooth(img: Image.Image, degrees: float, centre: tuple[float, float]) -> Image.Image:
    """Turn a sprite smoothly (the art is drawn smooth and filtered LinearClamp): premultiplied, bicubic, then back."""
    a = np.array(img).astype(np.float32) / 255.0
    a[..., :3] *= a[..., 3:4]
    chans = [Image.fromarray((a[..., c] * 255).round().astype(np.uint8)).rotate(degrees, resample=Image.BICUBIC, center=centre)
             for c in range(4)]
    r = np.stack([np.array(ch).astype(np.float32) / 255.0 for ch in chans], axis=-1)
    al = r[..., 3:4]
    r[..., :3] = np.where(al > 1e-4, r[..., :3] / np.maximum(al, 1e-4), 0)
    return Image.fromarray((np.clip(r, 0, 1) * 255).round().astype(np.uint8))


def build(strip: Image.Image) -> tuple[Image.Image, dict]:
    f1 = _frame(strip, 1)
    head, entry, src_dir = _head(f1)
    body = Image.fromarray(_cut_upright_head(np.array(_frame(strip, 7))))
    out = strip.copy()
    sockets = {}
    for i, pose in POSES.items():
        ex, ey = pose["entry"]
        want = math.atan2(-(HAND[1] - ey), HAND[0] - ex)   # entry -> hand, y up
        turn = math.degrees(want - src_dir)
        frame = body.copy()
        # the haft first (behind the hand), from the hand to just inside the head
        _haft(frame, HAND, (ex, ey))
        # the head turned about its entry, then placed with its entry on the pose's
        w, h = head.size
        big = Image.new("RGBA", (w * 3, h * 3), (0, 0, 0, 0))
        big.paste(head, (w, h))
        cx, cy = w + entry[0], h + entry[1]
        rot = _rotate_smooth(big, turn, (cx, cy))
        frame.alpha_composite(rot, (int(round(ex - cx)), int(round(ey - cy))))
        # the gripping hand back on top of the haft (frame 7's own gauntlet pixels)
        hand = body.crop((HAND[0] - 12, HAND[1] - 22, HAND[0] + 12, HAND[1] + 18))
        frame.alpha_composite(hand, (HAND[0] - 12, HAND[1] - 22))
        out.paste(frame, (i * F, 0))
        # the striking face: the head's far side along the haft, at its middle
        ux, uy = math.cos(want + math.pi), -math.sin(want + math.pi)   # entry -> away from the hand (screen y down)
        face = (ex + ux * 50, ey + uy * 50)
        force = (math.degrees(math.atan2(uy, ux)) + 360) % 360
        sockets[i] = [round(face[0] / F, 2), round(face[1] / F, 2), round(force if force < 180 else force - 360)]
    return out, sockets


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--sheet")
    args = ap.parse_args()
    base = _load_base()
    out, sockets = build(base)
    out.save(os.path.join(REPO, REL), optimize=True)
    print("StrikeHand sockets (frame: [x, y, deg]):", sockets)
    if args.sheet:
        os.makedirs(args.sheet, exist_ok=True)
        sheet = Image.new("RGBA", (8 * 256, 2 * 256), (90, 110, 90, 255))
        for row, img in enumerate((base, out)):
            small = img.resize((8 * 256, 256), Image.LANCZOS)
            sheet.alpha_composite(small, (0, row * 256))
        sheet.convert("RGB").save(os.path.join(args.sheet, "tower_slam_forward_sheet.jpg"), quality=88)


if __name__ == "__main__":
    main()
