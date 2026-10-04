#!/usr/bin/env python3
"""props_smooth -- the shared drawing kit for the champion MISSILE props (vfx sweep Phase 1 / P1.4).

Not run on its own: tools/asset-pipeline/v2/quiver_arrow.py, unbroken_chip.py, oathbound_hook.py and chorus_charm.py
import it.

THE STYLE. The props are drawn in the game's HAND-DRAWN SMOOTH style (the art contract since 2026-07-29: LinearClamp,
non-integer scale, premultiplied at load), not on a hard pixel grid: every shape is a polygon drawn at SS x the texel
grid and box-filtered down, so its silhouette carries partial alpha (check_fx_edges.py SOFT) and nothing on it is a
rectangle edge. A soft dark outline (the champions' own near-black) is grown round the silhouette, as their strips have.

THE CANVAS. Square (check_fx_edges.py measures square frames) with a clear margin of PAD texels all round (EDGE: the
border ring is empty). The object lies along +X, centred: the runtime turns it along its flight. Its CONTENT box (the
texels over the alpha floor) is printed and pinned in the C# look data (`PropContent`), so the head's drawn length and
thickness are the object's, never the square canvas's.

TWO FILES PER PROP: `<name>.png`, the MATERIAL (untinted: drawn as it is, alpha-blended), and `<name>_edge.png`, the
EMISSIVE mask (white, soft): where the object catches light, drawn through VfxBlend.Light at a low brightness.
"""
from __future__ import annotations

import os
import sys

from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "assets", "art", "Props"))
SS = 8                          # supersampling: 8 x 8 samples per texel
OUTLINE = (14, 10, 16, 255)     # the champions' near-black outline
FLOOR = 8                       # the alpha floor check_fx_edges.py and ItemArtMetrics treat as "something is here"


class Canvas:
    """A square canvas of `size` texels drawn at SS x: `poly` / `ellipse` / `line` take TEXEL coordinates."""

    def __init__(self, size: int):
        self.size = size
        self.im = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.im)

    def poly(self, pts, fill):
        self.d.polygon([(x * SS, y * SS) for x, y in pts], fill=fill)

    def ellipse(self, cx, cy, rx, ry, fill):
        self.d.ellipse([(cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS], fill=fill)

    def line(self, pts, fill, width):
        self.d.line([(x * SS, y * SS) for x, y in pts], fill=fill, width=max(1, int(round(width * SS))), joint="curve")


def outlined(fill: Canvas, outline_px: float = 1.1, colour=OUTLINE) -> Image.Image:
    """The fill layer with a soft outline `outline_px` texels wide grown round its silhouette, still at SS x."""
    alpha = fill.im.getchannel("A").point(lambda a: 255 if a > 0 else 0)
    k = int(round(outline_px * SS)) * 2 + 1
    grown = alpha.filter(ImageFilter.MaxFilter(k))
    base = Image.new("RGBA", fill.im.size, colour)
    base.putalpha(grown)
    base.alpha_composite(fill.im)
    return base


def down(im: Image.Image, blur: float = 0.0) -> Image.Image:
    """Box-filter an SS x layer to the texel grid (premultiplied, so the clear texels never darken an edge)."""
    small = im.convert("RGBa").resize((im.width // SS, im.height // SS), Image.BOX)
    if blur > 0:
        small = small.filter(ImageFilter.GaussianBlur(blur))
    return small.convert("RGBA")


def feathered(material: Image.Image, radius: float = 1.0, strength: float = 0.7, colour=OUTLINE) -> Image.Image:
    """THE SOFT FRINGE: a faint outline-coloured falloff `radius` texels wide UNDER the material, so the silhouette ends in
    a ramp of partial alpha (never a one-texel step) without ever making the object itself translucent."""
    a = material.getchannel("A").filter(ImageFilter.GaussianBlur(radius)).point(lambda v: int(v * strength))
    under = Image.new("RGBA", material.size, colour)
    under.putalpha(a)
    under.alpha_composite(material)
    return under


def content_box(im: Image.Image):
    """(x, y, w, h) of the texels over the alpha floor."""
    a = im.getchannel("A").point(lambda v: 255 if v > FLOOR else 0)
    x0, y0, x1, y1 = a.getbbox()
    return x0, y0, x1 - x0, y1 - y0


def gate(im: Image.Image, name: str) -> list[str]:
    """check_fx_edges.py's EDGE and SOFT rules, run here so a script never writes a prop the gate refuses."""
    w, h = im.size
    a = im.getchannel("A")
    px = a.load()
    edge = max([px[x, 0] for x in range(w)] + [px[x, h - 1] for x in range(w)]
               + [px[0, y] for y in range(h)] + [px[w - 1, y] for y in range(h)])
    seen = soft = 0
    for y in range(0, h, 4):
        for x in range(0, w, 4):
            seen += 1
            soft += FLOOR < px[x, y] < 248
    problems = []
    if w != h:
        problems.append(f"{name}: {w}x{h} is not square")
    if edge > 24:
        problems.append(f"{name}: alpha {edge} on the border")
    if soft / seen < 0.02:
        problems.append(f"{name}: only {soft / seen * 100:.2f}% partial alpha")
    return problems


def write(name: str, material: Image.Image, edge: Image.Image, check_only: bool) -> int:
    """Write (or, with --check, compare against) the two files; print the content box the C# data pins."""
    problems = gate(material, name) + gate(edge, name + "_edge")
    for p in problems:
        print("FAIL", p)
    if problems:
        return 1
    box = content_box(material)
    paths = {name: material, name + "_edge": edge}
    status = 0
    for key, im in paths.items():
        path = os.path.join(OUT, key + ".png")
        if check_only:
            if not os.path.exists(path):
                print(f"MISSING {path}")
                status = 1
                continue
            have = Image.open(path).convert("RGBA")
            if have.size != im.size or have.tobytes() != im.tobytes():
                print(f"STALE {path}: the file is not this script's")
                status = 1
            continue
        os.makedirs(OUT, exist_ok=True)
        im.save(path, optimize=True)
    print(f"{name}: {material.size[0]}x{material.size[1]} texels, content box (x, y, w, h) = {box}"
          + ("  [checked]" if check_only and status == 0 else ""))
    return status


def main_for(name: str, draw) -> int:
    """`draw()` returns (material, edge) at the texel grid; `--check` proves the files on disk are its output."""
    material, edge = draw()
    return write(name, material, edge, "--check" in sys.argv[1:])
