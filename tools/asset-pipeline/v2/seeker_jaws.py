#!/usr/bin/env python3
"""seeker_jaws -- the Seeker's JAWS: the iron jaw trap, open and shut, and the chain it hangs on (ADR-011, JAWS).

    python tools/asset-pipeline/v2/seeker_jaws.py

Writes, under assets/art/Props/:
  prop_seeker_jaws_open.png       the MATERIAL, open: two serrated iron jaws spread in a U, hinge and rings below
  prop_seeker_jaws_shut.png       the MATERIAL, shut: the same jaws closed into a toothed ring (the icon's picture)
  prop_seeker_jaws_shut_edge.png  the EMISSIVE mask of the shut teeth, white: the Source glint at the snap
  prop_seeker_chain_link.png      one chain link in two cells, face-on and edge-on, drawn pixel by pixel

THE TWO STATES ARE ONE OBJECT. The open jaws are a PixelLab pixflux image (job 2126658f, seed 2207) in a forced
dark-iron palette (the Seeker's outline and steel, two iron shades); the shut jaws are a PixelLab EDIT of that very
image (job b3c7ee25, seed 3301: "snap the trap SHUT"), so the hinge, the rings, the framing and the iron are the same
pixels' descendants. A second pixflux candidate (891590a1, seed 1101) was rejected: its jaws read as a spiked horn
collar and its teeth vanished at play size. PixelLab draws no motion here: the spring, the snap, the chain's tension,
the recoil, the light and the fall are the runtime's (ReactionPerformance).

THE POST-PASS, deterministic:
  - the edit filled the shut ring's inside with opaque white; every near-white pixel goes transparent, so the
    creature's limb shows THROUGH the ring (a leg caught in a gin), and the edit's near-palette drift is snapped
    back to the palette so the two states are the same iron;
  - the teeth came out rust-brown; they are recoloured to the palette's steel, so they read as the metal that bites
    and carry the tooth-edge glint (material vs Source: the iron is never tinted, the Source is only light);
  - the iron is lifted one shade (see IRON), filmed first at the palette's values and lost against the floor;
  - the edge mask is the shut teeth, the light steel full and the shade half.
Sizes stay the PixelLab canvas (128 px): drawn at ~0.4 of a creature's height (45-120 px on screen), which is a
mild reduction under LinearClamp, never a blow-up.
"""
from __future__ import annotations

import io
import os
import urllib.request

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "assets", "art", "Props"))
IMG = "https://api.pixellab.ai/mcp/images/{job}/download"

OPEN_JOB = "2126658f-f6b1-4c76-8e88-25260a56bf97"     # pixflux, seed 2207: USED (open)
SHUT_JOB = "b3c7ee25-985f-4c3d-9ac1-40ccadbf1d2e"     # edit_image of OPEN_JOB, seed 3301: USED (shut)
REJECTED = {"891590a1-57e2-445a-ae1a-29b4c999b8b2": "pixflux seed 1101: a spiked horn collar, teeth unreadable"}

# the forced palette the open jaws were generated in (and the shut ones are snapped back to)
PALETTE = [(12, 7, 15), (28, 26, 34), (44, 44, 56), (63, 66, 82), (97, 106, 142), (131, 146, 168),
           (186, 205, 220), (228, 238, 245), (63, 33, 34), (97, 58, 42)]
# the rust teeth -> the palette's steel (shade, light)
TEETH = {(63, 33, 34): (97, 106, 142), (97, 58, 42): (131, 146, 168)}
# THE IRON, ONE SHADE UP. Filmed in the arena at the palette's own values, the jaws' body (28-63) sat UNDER the
# floor (~50-70) and against a black creature, so the shape vanished and only the teeth read. Each iron shade moves
# up one step toward the steel; the outline stays, so it is still dark iron with a hard edge, never a light metal.
IRON = {(28, 26, 34): (44, 44, 58), (44, 44, 56): (66, 70, 88), (63, 66, 82): (92, 100, 128)}
EDGE = {(97, 106, 142): 150, (131, 146, 168): 255}   # emissive strength per recoloured tooth colour


def fetch(job: str) -> Image.Image:
    return Image.open(io.BytesIO(urllib.request.urlopen(IMG.format(job=job), timeout=90).read())).convert("RGBA")


def nearest(c: tuple[int, int, int]) -> tuple[int, int, int]:
    return min(PALETTE, key=lambda p: sum((a - b) ** 2 for a, b in zip(p, c)))


def clean(im: Image.Image, whites_out: bool) -> tuple[Image.Image, Image.Image]:
    """Palette-snap, drop the white fill, recolour the teeth; return (material, edge mask)."""
    w, h = im.size
    mat = Image.new("RGBA", im.size, (0, 0, 0, 0))
    edge = Image.new("RGBA", im.size, (255, 255, 255, 0))
    src, m, e = im.load(), mat.load(), edge.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = src[x, y]
            if a < 128:
                continue
            if whites_out and min(r, g, b) >= 235:
                continue                                      # the edit's white fill: transparent
            snapped = nearest((r, g, b))
            c = TEETH.get(snapped, IRON.get(snapped, snapped))
            m[x, y] = c + (255,)
            if c in EDGE:
                e[x, y] = (255, 255, 255, EDGE[c])
    return mat, edge


def chain_link() -> Image.Image:
    """One link, face-on (an oval ring with its hole) and edge-on (a short bar), each 12x7 source px, x2."""
    O, D, M, L = (12, 7, 15, 255), (66, 70, 88, 255), (92, 100, 128, 255), (150, 164, 188, 255)
    face = [
        "..OOOOOOOO..",
        ".OMMLLLLMMO.",
        "OML.OOOO.DMO",
        "OM.O....O.DO",
        "OMD.OOOO.DDO",
        ".ODDDDDDDDO.",
        "..OOOOOOOO..",
    ]
    bar = [
        "............",
        "..OOOOOOOO..",
        ".OMLLLLLLMO.",
        "OMMMMMMMMMDO",
        ".ODDDDDDDDO.",
        "..OOOOOOOO..",
        "............",
    ]
    pal = {"O": O, "D": D, "M": M, "L": L, ".": (0, 0, 0, 0)}
    im = Image.new("RGBA", (24, 7))
    for cell, rows in enumerate((face, bar)):
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                im.putpixel((cell * 12 + x, y), pal[ch])
    return im.resize((48, 14), Image.NEAREST)


def main() -> int:
    os.makedirs(OUT, exist_ok=True)
    open_mat, _ = clean(fetch(OPEN_JOB), whites_out=False)
    shut_mat, shut_edge = clean(fetch(SHUT_JOB), whites_out=True)
    assert open_mat.getbbox() is not None and shut_mat.getbbox() is not None
    open_mat.save(os.path.join(OUT, "prop_seeker_jaws_open.png"))
    shut_mat.save(os.path.join(OUT, "prop_seeker_jaws_shut.png"))
    shut_edge.save(os.path.join(OUT, "prop_seeker_jaws_shut_edge.png"))
    chain_link().save(os.path.join(OUT, "prop_seeker_chain_link.png"))
    for name in ("prop_seeker_jaws_open", "prop_seeker_jaws_shut", "prop_seeker_jaws_shut_edge", "prop_seeker_chain_link"):
        im = Image.open(os.path.join(OUT, name + ".png"))
        print(f"{name:32} {im.size} bbox={im.getbbox()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
