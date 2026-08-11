#!/usr/bin/env python3
"""Cut the Hunter rig's body parts out of the full-body sprite, and derive the rig table.

    python3 tools/asset-pipeline/cut_rig_parts.py --write

Why cut rather than generate: the parts must RECONNECT. Generated limbs are each
invented independently, so their proportions, lighting and joint widths never
agree and no pivot can reconcile them — the assembled figure came out as parts
strung 1143 px down the screen. Slicing one coherent sprite guarantees the pieces
fit back together, because they were together to begin with.

It also removes the calibration guesswork. `HunterRig.Parts` carries an Offset
and a Pivot per part "verified by eye"; here both are ARITHMETIC. Every part
declares its joint in absolute sprite coordinates, so:

    Pivot  = joint - box.topleft                     (joint in the part's own pixels)
    Offset = child.joint - parent.joint              (attach point in parent-local space)

Change a box and the numbers follow automatically.
"""

from __future__ import annotations

import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from pixelpng import Image, read, write  # noqa: E402
from scene_preview import find  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(REPO, "assets", "art", "rig")

SOURCE_KEY = "hunter_idle"

# name -> (parent, box(l,t,r,b), joint(x,y), draw_order)
# Boxes and joints are in SOURCE sprite pixels, read off the 512x512 hunter_idle
# with a 32px grid. The joint is each part's PROXIMAL end — the point it rotates
# about and attaches to its parent by.
PARTS: dict[str, tuple[str | None, tuple[int, int, int, int], tuple[int, int], int]] = {
    # name -> (parent, box(l,t,r,b), joint(x,y), draw_order)
    #
    # BOXES MUST BE DISJOINT. Every part is a rectangle cut from ONE sprite, so any
    # two boxes that overlap copy the same pixels into both parts and the figure
    # draws them twice — that is what produced a hunter with four arms and four
    # legs. The columns below are therefore hard-separated:
    #     arms   x 168..214   |  body x 214..304  |  arms x 304..352
    #     main leg x 214..258 |  off leg x 258..304
    # and the body column is split vertically at the waist (266) and hip (302).
    #
    # There is no cloak part: its cut spanned x 170..348, y 118..400, which is
    # most of the body — it was the single biggest source of the doubling. The
    # hood reads as part of the head cut instead.
    #                parent              l    t    r    b        joint          order
    "arm_upper_off":   ("torso",        (304, 128, 348, 238),  (315, 146),  1),
    "arm_fore_off":    ("arm_upper_off", (304, 238, 348, 288), (318, 240),  2),
    "hand_off":        ("arm_fore_off", (304, 288, 352, 334),  (322, 290),  3),
    # Columns below are MEASURED from the sprite, not guessed: at thigh level the
    # tunic is one connected mass (x 108..340), and the legs only separate at the
    # shin (x 174..256 and 268..332) with the feet at 170..224 and 296..338.
    # Earlier boxes sat in the wrong columns, so foot_main came out 15% filled.
    # Shin and foot are also split at y=455 so they do not overlap.
    "leg_thigh_off":   ("pelvis",       (258, 302, 352, 402),  (282, 304),  4),
    "leg_shin_off":    ("leg_thigh_off", (262, 402, 340, 455), (300, 404),  5),
    "foot_off":        ("leg_shin_off", (262, 455, 345, 494),  (317, 458),  6),
    "pelvis":          (None,           (214, 238, 304, 302),  (259, 238),  7),
    "torso":           ("pelvis",       (214, 122, 304, 238),  (259, 238),  8),
    "leg_thigh_main":  ("pelvis",       (108, 302, 258, 402),  (236, 304),  9),
    "leg_shin_main":   ("leg_thigh_main", (168, 402, 262, 455), (215, 404), 10),
    "foot_main":       ("leg_shin_main", (160, 455, 262, 494), (197, 458), 11),
    "head":            ("torso",        (202,  16, 316, 122),  (259, 120), 12),
    "arm_upper_main":  ("torso",        (168, 128, 214, 238),  (205, 146), 13),
    "arm_fore_main":   ("arm_upper_main", (168, 238, 214, 288), (200, 240), 14),
    "hand_main":       ("arm_fore_main", (168, 288, 214, 334), (196, 290), 15),
    # Shoulder caps sit ON THE TORSO, not the arm, so they hold still while the arm
    # swings beneath and cover the gap that opens at the joint. Without them a hard
    # swing (AttackerStrike reaches 1.2 rad) tears the shoulder open.
    # Shoulder joints are covered by DRAWN pauldrons (hunter_pauldron_*), not by a
    # disc cut from the torso. A cut disc carries lighting for the resting pose and
    # shows as a patch the moment the arm swings; a drawn piece is lit for itself.
    # These entries stay so the bones exist — the texture key points at the art.
    "shoulder_main":   ("torso",        (182, 126, 228, 172),  (205, 146), 16),
    "shoulder_off":    ("torso",        (292, 126, 338, 172),  (315, 146), 17),
}


# Regions holding pixels of the sprite's baked-in sword. The blade hangs diagonally
# down-left of the grip and falls inside cuts that own other things — most visibly
# leg_thigh_main, where it then swung with the leg like a floating weapon. Only
# STEEL pixels (bright, low-saturation) inside these boxes are cleared, so the dark
# cloak and the tunic behind the blade survive.
ERASE_STEEL: dict[str, list[tuple[int, int, int, int]]] = {
    "leg_thigh_main": [(0, 0, 98, 95)],
    "hand_main":      [(0, 4, 18, 30)],
    "arm_fore_main":  [(0, 12, 34, 32)],
}


# Regions cleared OUTRIGHT. Colour-keying the blade only removed its steel
# interior — the black outline shares the cloak's luma, so no threshold separates
# them. These boxes cover only blade and empty canvas, so a full clear is safe
# where a colour key was not.
# Caps are masked to a CIRCLE about their joint. A rectangular cap carries the
# cloak edge and background at its corners, and once the arm swings out from under
# it those straight edges read as a patch stuck on the shoulder. A disc has no
# corners to notice.
CIRCLE_MASK: set[str] = set()


# Parts whose art is DRAWN rather than cut. A drawn piece arrives at whatever canvas
# the generator used (128x128) while the rig draws every part at its texture's native
# size, so a raw drop-in renders ~3x oversized and hung off its corner. These entries
# trim the art to its own content, scale it to a width measured in SOURCE sprite pixels
# (the same space every box above lives in), and place the joint by fraction of the
# piece — so a regenerated pauldron of any canvas size still lands on the shoulder.
# Both shoulders come from ONE piece, the off side mirrored. Generating the two sides separately
# produced two different materials (tan leather on one shoulder, grey steel on the other) — armour
# that is meant to be symmetric has to come from a single authored asset.
#   name -> (asset key, width in source px, joint as (u, v) fraction of the piece, mirror)
DRAWN: dict[str, tuple[str, int, tuple[float, float], bool]] = {
    "shoulder_main": ("hunter_pauldron_main", 54, (0.50, 0.26), False),
    "shoulder_off":  ("hunter_pauldron_main", 54, (0.50, 0.26), True),
}


def mirror(img: Image) -> Image:
    out = Image(img.w, img.h)
    for y in range(img.h):
        row = img.px[y * img.w * 4:(y + 1) * img.w * 4]
        for x in range(img.w):
            s = (img.w - 1 - x) * 4
            d = (y * img.w + x) * 4
            out.px[d:d + 4] = row[s:s + 4]
    return out


def content_box(img: Image) -> tuple[int, int, int, int]:
    l, t, r, b = img.w, img.h, 0, 0
    for y in range(img.h):
        row = img.px[y * img.w * 4:(y + 1) * img.w * 4]
        if not any(row[3::4]):
            continue
        t = min(t, y); b = max(b, y + 1)
        for x in range(img.w):
            if row[x * 4 + 3]:
                l = min(l, x); r = max(r, x + 1)
    return (0, 0, img.w, img.h) if r <= l else (l, t, r, b)


def resample(src: Image, w: int, h: int) -> Image:
    """Box-average downscale. A pauldron shrinks ~2.5x; nearest-neighbour at that ratio
    throws away two of every three pixels and the studs turn to noise."""
    out = Image(w, h)
    for y in range(h):
        y0, y1 = y * src.h // h, max(y * src.h // h + 1, (y + 1) * src.h // h)
        for x in range(w):
            x0, x1 = x * src.w // w, max(x * src.w // w + 1, (x + 1) * src.w // w)
            acc = [0, 0, 0, 0]
            n = 0
            for sy in range(y0, y1):
                for sx in range(x0, x1):
                    o = (sy * src.w + sx) * 4
                    a = src.px[o + 3]
                    acc[0] += src.px[o] * a; acc[1] += src.px[o + 1] * a
                    acc[2] += src.px[o + 2] * a; acc[3] += a
                    n += 1
            d = (y * w + x) * 4
            if acc[3]:
                out.px[d] = acc[0] // acc[3]; out.px[d + 1] = acc[1] // acc[3]
                out.px[d + 2] = acc[2] // acc[3]; out.px[d + 3] = acc[3] // max(1, n)
    return out


def drawn_part(key: str, width: int) -> Image:
    path = find(key)
    if not path:
        raise SystemExit(f"drawn part asset '{key}' not found")
    src = read(path)
    l, t, r, b = content_box(src)
    cropped = crop(src, (l, t, r, b))
    h = max(1, round(cropped.h * width / cropped.w))
    return resample(cropped, width, h)


def circle_mask(img: Image, cx: float, cy: float, radius: float) -> int:
    cleared = 0
    for y in range(img.h):
        for x in range(img.w):
            o = (y * img.w + x) * 4
            if img.px[o + 3] <= 8:
                continue
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            if dx * dx + dy * dy > radius * radius:
                img.px[o:o + 4] = b"\x00\x00\x00\x00"
                cleared += 1
    return cleared


ERASE_ALL: dict[str, list[tuple[int, int, int, int]]] = {
    "leg_thigh_main": [(0, 0, 84, 98)],
}


def erase_all(img: Image, boxes: list[tuple[int, int, int, int]]) -> int:
    cleared = 0
    for l, t, r, b in boxes:
        for y in range(max(0, t), min(img.h, b)):
            for x in range(max(0, l), min(img.w, r)):
                o = (y * img.w + x) * 4
                if img.px[o + 3] > 8:
                    img.px[o:o + 4] = b"\x00\x00\x00\x00"
                    cleared += 1
    return cleared


def erase_steel(img: Image, boxes: list[tuple[int, int, int, int]]) -> int:
    """Clear bright low-saturation (steel) pixels inside the given local boxes."""
    cleared = 0
    for l, t, r, b in boxes:
        for y in range(max(0, t), min(img.h, b)):
            for x in range(max(0, l), min(img.w, r)):
                o = (y * img.w + x) * 4
                if img.px[o + 3] <= 8:
                    continue
                red, grn, blu = img.px[o], img.px[o + 1], img.px[o + 2]
                lum = (red * 299 + grn * 587 + blu * 114) // 1000
                if lum > 55 and (max(red, grn, blu) - min(red, grn, blu)) < 48:
                    img.px[o:o + 4] = b"\x00\x00\x00\x00"
                    cleared += 1
    return cleared


def crop(src: Image, box: tuple[int, int, int, int]) -> Image:
    l, t, r, b = box
    out = Image(r - l, b - t)
    for y in range(t, b):
        so = (y * src.w + l) * 4
        do = ((y - t) * out.w) * 4
        out.px[do:do + (r - l) * 4] = src.px[so:so + (r - l) * 4]
    return out


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--write", action="store_true", help="write the part PNGs (default is a report)")
    args = ap.parse_args()

    path = find(SOURCE_KEY)
    if not path:
        print(f"source sprite '{SOURCE_KEY}' not found", file=sys.stderr)
        return 1
    src = read(path)
    print(f"source {SOURCE_KEY} {src.w}x{src.h}\n")

    rows = []
    drawn_cache: dict[str, Image] = {}
    for name, (parent, box, joint, order) in PARTS.items():
        l, t, r, b = box
        if name in DRAWN:
            key, width, (pu, pv), flip = DRAWN[name]
            art = drawn_part(key, width)
            if flip:
                art = mirror(art)
            drawn_cache[name] = art
            pivot = (round(art.w * pu), round(art.h * pv))
        else:
            pivot = (joint[0] - l, joint[1] - t)
        if parent is None:
            offset = (0, 0)
        else:
            pj = PARTS[parent][2]
            offset = (joint[0] - pj[0], joint[1] - pj[1])
        rows.append((name, parent, f"hunter_part_{name}", offset, pivot, order, box))

        if args.write:
            os.makedirs(OUT_DIR, exist_ok=True)
            if name in drawn_cache:
                part = drawn_cache[name]
                print(f"  {name}: drawn art {DRAWN[name][0]} -> {part.w}x{part.h}, pivot {pivot}")
                write(os.path.join(OUT_DIR, f"hunter_part_{name}.png"), part)
                continue
            part = crop(src, box)
            if name in ERASE_ALL:
                print(f"  cleared {erase_all(part, ERASE_ALL[name])} px (blade region) from {name}")
            if name in ERASE_STEEL:
                print(f"  erased {erase_steel(part, ERASE_STEEL[name])} blade px from {name}")
            if name in CIRCLE_MASK:
                r = min(part.w, part.h) / 2.0
                print(f"  masked {circle_mask(part, pivot[0], pivot[1], r)} px outside the disc on {name}")
            write(os.path.join(OUT_DIR, f"hunter_part_{name}.png"), part)

    print("Derived rig table (paste into HunterRig.Parts):\n")
    for name, parent, tex, off, piv, order, box in sorted(rows, key=lambda r: r[5]):
        par = "null" if parent is None else f'"{parent}"'
        print(f'        new HunterPart("{name}", {par}, "{tex}", '
              f'new Vec2({off[0]}, {off[1]}), new Vec2({piv[0]}, {piv[1]}), {order}),')

    if args.write:
        print(f"\nwrote {len(rows)} part PNGs -> assets/art/rig/")
    else:
        print("\n(report only — pass --write to emit the PNGs)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
