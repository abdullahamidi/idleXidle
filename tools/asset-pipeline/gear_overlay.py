#!/usr/bin/env python3
"""Paint worn equipment ONTO the rig source, then cut it into rig parts.

    python3 tools/asset-pipeline/gear_overlay.py --slot boots --material "purple arcane steel"

Why this exists
---------------
Worn gear used to be an isolated inventory icon, scaled and pinned to a bone. It can never look
right: the icon is drawn with its own perspective, its own light direction, its own outline weight
and its own palette, and none of those match the body it lands on. Two identical boot icons pasted
on two legs read as two stickers, not as a pair of boots. That is what the player kept seeing.

The way 2D games actually do this is a paper doll: the artist paints the armour ON the character,
so the layer shares the pose and the lighting by construction. Inpainting gives exactly that from a
generator — the region is repainted inside the real figure, everything outside the mask stays
pixel-identical, and the result is registered to the body with no scale, pivot or rotation to tune.

Each slot's output is cut with the SAME boxes as the body, so a gear part is a drop-in replacement
for the body part on the same bone. Swapping equipment is then swapping a texture, which is what
"swap out individual parts of the character" was always supposed to mean.
"""

from __future__ import annotations

import argparse
import base64
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import pixellab_client as api  # noqa: E402
from cut_rig_parts import PARTS  # noqa: E402
from pixelpng import Image, read, write  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
BASE = os.path.join(REPO, "assets", "art", "Characters", "Hunter", "hunter_rig_base.png")
OUT_DIR = os.path.join(REPO, "assets", "art", "rig", "worn")
STAGING = os.path.join(HERE, ".staging")

# Slot -> (region to repaint in base pixels, rig parts cut from the result, what to paint).
# The region is the area the armour occupies on the body; the parts are every bone it covers, so
# each one becomes a swappable replacement texture for that bone.
# Slot -> (parts it replaces, [(region to repaint, what to paint there)]).
#
# Regions are split per limb because /inpaint caps each side at 200px including the context margin.
# That split is a feature as much as a constraint: painting each boot in its own pass lets the model
# see that leg's own shading, so the pair does not come out as two copies of one sticker.
SLOTS: dict[str, tuple[list[str], list[tuple[tuple[int, int, int, int], str]]]] = {
    # The head box is 210x168, so with a context margin it would exceed the 200px cap. The helmet
    # only needs the skull and face — the hood above it is the character's, not the helm's.
    "helm": (["head"], [
        # NOT "covering the face and skull" — that wording put a literal SKULL on the character. The
        # generator takes nouns as subjects, not as constraints; the same trap as "no cloak" summoning
        # a cloak. Name the object and its shape, nothing else.
        ((190, 40, 326, 168), "a smooth rounded {material} helmet with a narrow horizontal eye slit"),
    ]),
    "chest": (["torso"], [
        ((188, 150, 324, 272), "one large curved {material} breastplate over the chest"),
    ]),
    "gloves": (["hand_main", "hand_off"], [
        ((110, 244, 180, 304), "a {material} armoured gauntlet, a metal glove covering the whole fist"),
        ((332, 244, 402, 304), "a {material} armoured gauntlet, a metal glove covering the whole fist"),
    ]),
    "boots": (["leg_shin_main", "foot_main", "leg_shin_off", "foot_off"], [
        ((126, 392, 250, 512), "a tall {material} armoured boot, plated shin and toe, worn on the leg"),
        ((252, 392, 384, 512), "a tall {material} armoured boot, plated shin and toe, worn on the leg"),
    ]),
}

# The worn material per trait. These mirror TRAIT_LOOK in build_spec.py, which drives the inventory
# icon, so a Keen boot in the bag and a Keen boot on the character are the same metal.
# Forced palettes, one per trait. The prompt alone could not make a material stick: the model matches
# the surrounding figure, so every "violet" or "gold" piece came back the same muted brown as the
# leather it replaced. /inpaint takes a colour image and honours it.
def ramp(dark: tuple[int, int, int], mid: tuple[int, int, int], light: tuple[int, int, int],
         steps: int = 10) -> list[tuple[int, int, int]]:
    """A single-hue ladder from `dark` through a SATURATED `mid` to `light`.

    Three anchors, not two, and that middle one is the whole point. A straight ramp from near-black
    to a pale tint passes through desaturated grey in the middle, where most of a shaded surface
    actually lives — so a "violet" plate came back looking like plain steel with a purple edge.

    The step count matters too: four tones forced the colour but flattened the form, twelve tones
    that included the body's browns let brown win and the armour came back as the leather it was
    replacing. Ten steps of one hue leaves room to shade and nothing else to reach for.
    """
    out = []
    for i in range(steps):
        t = i / (steps - 1)
        if t <= 0.5:
            a, b, u = dark, mid, t * 2
        else:
            a, b, u = mid, light, (t - 0.5) * 2
        out.append(tuple(round(x + (y - x) * u) for x, y in zip(a, b)))
    return out


# ARMOUR TIERS, keyed to item RARITY, replacing a ten-materials-per-slot matrix.
#
# That matrix was the mistake. Fifty independent generations meant fifty independent dice rolls with
# no quality gate, and it showed: a gold helm above a violet boot above a teal gauntlet, every piece
# designed in isolation and none of them agreeing. Nobody ships armour that way. A handful of tiers,
# each designed once as a COMPLETE set under one palette, is both far easier to get right and a much
# better signal to the player — "my gear got better" reads from the metal changing across the whole
# figure at once, not from four slots disagreeing about colour.
#
# Per-slot visibility is unchanged: equip only boots and only the boots change. What the tier fixes
# is that every piece a player can see at once was designed to sit beside the others.
TIERS: dict[str, tuple[str, list[tuple[int, int, int]]]] = {
    "worn":     ("dark studded leather and blackened iron",
                 ramp((0x0A, 0x08, 0x08), (0x4A, 0x3A, 0x2A), (0xA8, 0x90, 0x70))),
    "steel":    ("polished steel plate with bright edges",
                 ramp((0x0C, 0x0E, 0x14), (0x74, 0x8A, 0xA6), (0xEC, 0xF4, 0xFC))),
    "runic":    ("deep blue-steel plate with glowing rune inlay",
                 ramp((0x06, 0x0C, 0x22), (0x2E, 0x6A, 0xD8), (0xB4, 0xDA, 0xFC))),
    "warplate": ("black plate armour with bright gold trim",
                 ramp((0x0A, 0x08, 0x06), (0xC8, 0x94, 0x1E), (0xFC, 0xEC, 0xA8))),
}

MARGIN = 30   # context kept around each region, so the repaint has the body's own shading to match

# NOT "same lighting and outline as the rest of the figure" — that phrasing told the model to copy
# its surroundings, and it did: the chest came back as the same brown leather it was replacing and
# the helm as an empty hood. Registration already comes from inpainting; the prompt's whole job is
# to insist on the NEW material.
STYLE_TAIL = "dark fantasy RPG pixel art, front view, bold metal plates, strong colour"

NEGATIVE = ("second character, extra limb, floating object, item icon, inventory frame, background, "
            "text, border")


def to_b64(img: Image) -> str:
    os.makedirs(STAGING, exist_ok=True)
    tmp = os.path.join(STAGING, "_b64.png")
    write(tmp, img)
    with open(tmp, "rb") as fh:
        return base64.b64encode(fh.read()).decode("ascii")


def palette_b64(colours: list[tuple[int, int, int]]) -> str | None:
    """A tiny swatch of the tier's colours, handed to /inpaint as a forced palette."""
    if not colours:
        return None
    sw = Image(len(colours) * 8, 8)
    for i, (r, g, b) in enumerate(colours):
        for y in range(8):
            for x in range(8):
                d = (y * sw.w + i * 8 + x) * 4
                sw.px[d:d + 4] = bytes((r, g, b, 255))
    return to_b64(sw)


def crop(src: Image, box: tuple[int, int, int, int]) -> Image:
    l, t, r, b = box
    out = Image(r - l, b - t)
    for y in range(t, b):
        so = (y * src.w + l) * 4
        do = (y - t) * out.w * 4
        out.px[do:do + (r - l) * 4] = src.px[so:so + (r - l) * 4]
    return out


def paste(dst: Image, src: Image, at: tuple[int, int], only: tuple[int, int, int, int]) -> None:
    """Copy src into dst at `at`, but only the pixels inside `only` (dst coordinates)."""
    ox, oy = at
    l, t, r, b = only
    for y in range(max(t, oy), min(b, oy + src.h)):
        for x in range(max(l, ox), min(r, ox + src.w)):
            s = ((y - oy) * src.w + (x - ox)) * 4
            d = (y * dst.w + x) * 4
            dst.px[d:d + 4] = src.px[s:s + 4]


def mask_for(size: tuple[int, int], white: tuple[int, int, int, int]) -> Image:
    w, h = size
    m = Image(w, h)
    for i in range(w * h):
        m.px[i * 4:i * 4 + 4] = b"\x00\x00\x00\xff"
    l, t, r, b = white
    for y in range(max(0, t), min(h, b)):
        for x in range(max(0, l), min(w, r)):
            d = (y * w + x) * 4
            m.px[d:d + 4] = b"\xff\xff\xff\xff"
    return m


def run(slot: str, tier: str, seed: int | None) -> str:
    material, palette = TIERS[tier]
    parts, regions = SLOTS[slot]
    base = read(BASE)
    merged = read(BASE)

    for index, (region, subject) in enumerate(regions):
        l, t, r, b = region
        cl, ct = max(0, l - MARGIN), max(0, t - MARGIN)
        cr, cb = min(base.w, r + MARGIN), min(base.h, b + MARGIN)
        context = crop(base, (cl, ct, cr, cb))
        if context.w > 200 or context.h > 200:
            raise SystemExit(f"{slot} region {index} is {context.w}x{context.h}; /inpaint caps at 200")
        mask = mask_for((context.w, context.h), (l - cl, t - ct, r - cl, b - ct))

        prompt = subject.format(material=material) + ", " + STYLE_TAIL
        payload = api.inpaint(
            to_b64(context), to_b64(mask), prompt, context.w, context.h,
            outline="single color black outline", shading="medium shading", detail="highly detailed",
            negative_description=NEGATIVE + ", skull, bone, skeleton, face, corset, lacing",
            text_guidance_scale=10.0, color_b64=palette_b64(palette), seed=seed,
        )

        os.makedirs(STAGING, exist_ok=True)
        raw_path = os.path.join(STAGING, f"worn_{tier}_{slot}_{index}.raw.png")
        with open(raw_path, "wb") as fh:
            fh.write(payload)

        # Only the repainted REGION goes back. Everything outside it stays the untouched body, so
        # the bones this slot does not cover are byte-identical to the base and no seam can drift.
        paste(merged, read(raw_path), (cl, ct), region)
        print(f"  {slot}[{index}] {context.w}x{context.h} repainted")

    # The whole repainted figure is a debugging artefact, not a shipping asset — it stays in staging,
    # because AssetLibrary keys on bare filenames and a second full-body PNG would just be dead weight.
    write(os.path.join(STAGING, f"worn_{tier}_{slot}_full.png"), merged)

    # Cut with the SAME boxes as the body, so each result is a drop-in replacement texture for that
    # bone. No scale, no pivot, no rotation to tune — the transform is the body part's own.
    os.makedirs(OUT_DIR, exist_ok=True)
    written = []
    for name in parts:
        l, t, r, b = PARTS[name][1]
        write(os.path.join(OUT_DIR, f"worn_{tier}_{name}.png"), crop(merged, (l, t, r, b)))
        written.append(name)
    return f"{len(written)} parts: " + ", ".join(written)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--tier", choices=sorted(TIERS), help="one tier; omit with --all")
    ap.add_argument("--slot", choices=sorted(SLOTS), help="one slot (default: every slot of the tier)")
    ap.add_argument("--all", action="store_true", help="every tier x every slot")
    ap.add_argument("--seed", type=int, default=None)
    args = ap.parse_args()

    tiers = sorted(TIERS) if args.all else ([args.tier] if args.tier else [])
    if not tiers:
        ap.error("pass --tier or --all")
    for tier in tiers:
        for slot in ([args.slot] if args.slot else sorted(SLOTS)):
            print(f"{tier} / {slot}")
            print("   ", run(slot, tier, args.seed))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
