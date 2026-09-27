#!/usr/bin/env python3
"""seeker_mist.py -- the Seeker's JAWS SHADOW MIST: two soft wisps, authored procedurally (the Shadow Fangs polish
pass, 2026-09-27). No PixelLab: a dark fog is soft alpha noise inside a few overlapping irregular blobs.

    python tools/asset-pipeline/v2/seeker_mist.py

The fangs are the semantic impact; the mist only connects their appearance and disappearance, so the four fangs read
as ONE Shadow phenomenon that gathers, bites and evaporates. It must never become a smoke explosion or bright magic:

  - near-black at the core (darker than the creatures' own black, so it deepens and never greys), a very restrained
    violet only in the thin outer fringe (Shadow material, not fire);
  - feathered alpha everywhere, zero on every canvas edge (no rectangular edges);
  - a GAUSSIAN-like radial profile: a compact dense core and a long soft fringe (the runtime draws the pocket UNDER the
    creatures, so the core is hidden behind the body and what the player sees is the fringe round the silhouette);
  - irregular: two or three overlapping soft ellipses, their rims bent by low-frequency noise, the density broken by
    two octaves of value noise so it reads as wisps, not a disc.

Deterministic (fixed seeds). Writes the runtime parts, straight alpha (the asset library premultiplies at load):

    assets/art/VFX/parts/fxp_seeker_mist_a.png   176 x 120, the main pocket
    assets/art/VFX/parts/fxp_seeker_mist_b.png   144 x 104, a second, looser wisp (a different seed and blob layout)
    tools/asset-pipeline/v2/keypose_sources/seeker_mist_spans.json  what the art measures (the tests read it)
    tools/asset-pipeline/v2/keypose_sources/seeker_mist_sheet.png   both on grey and on a dark creature proxy

The JSON records each wisp's peak alpha, its CORE span (alpha >= half its peak) and VISIBLE span (alpha >= 8 % of its
peak) as shares of its canvas, the largest alpha on any canvas edge, and the mean colour of its core: ReactionRecipe's
MistWidthShare / MistHeightShare are set from the spans, and jaws_reaction_test pins the recipe against these numbers
rather than against constants typed into the test.
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "assets", "art", "VFX", "parts")
SRC = os.path.join(HERE, "keypose_sources")

CORE = np.array([7.0, 5.0, 11.0])       # near-black, a faint blue-violet cast: darker than the creatures' own black
FRINGE = np.array([26.0, 18.0, 42.0])   # a very restrained violet, only where the fog is thin (never the rim's own hue)
PEAK = 0.72                             # the texture's own peak alpha: the runtime layer (MistPeak) multiplies it;
                                        # with the second wisp stacked on it the pocket's densest point stays ~0.6


def value_noise(rng, h, w, cells):
    """Smooth value noise: a coarse random lattice, bicubic-upsampled (PIL), in [0, 1]."""
    ch, cw = max(2, int(h / cells)), max(2, int(w / cells))
    lattice = (rng.random((ch + 1, cw + 1)) * 255).astype(np.uint8)
    im = Image.fromarray(lattice, "L").resize((w, h), Image.BICUBIC)
    return np.asarray(im).astype(np.float32) / 255.0


def blob(h, w, cx, cy, rx, ry, rim, rng):
    """One soft ellipse whose rim radius is bent by low-frequency angular noise, with a Gaussian-like radial profile."""
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    dx, dy = (xs - cx) / rx, (ys - cy) / ry
    ang = np.arctan2(dy, dx)
    # a few low harmonics with random phase: an irregular, never spiky outline
    bend = np.zeros_like(ang)
    for k, amp in ((2, rim * 0.55), (3, rim * 0.35), (5, rim * 0.18)):
        bend += amp * np.cos(k * ang + rng.random() * 2 * np.pi)
    r = np.sqrt(dx * dx + dy * dy) / (1.0 + bend)
    # a compact dense core and a long soft fringe (half density at r ~0.52, 8 % at r ~1.0)
    return np.exp(-2.6 * r * r)


def wisp(seed, w, h, layout):
    rng = np.random.default_rng(seed)
    dens = np.zeros((h, w), np.float32)
    for (cx, cy, rx, ry, weight) in layout:
        dens = np.maximum(dens, weight * blob(h, w, cx * w, cy * h, rx * w, ry * h, 0.16, rng))
        dens = dens + 0.30 * weight * blob(h, w, cx * w, cy * h, rx * w * 0.8, ry * h * 0.8, 0.2, rng)
    # wisps: two octaves of value noise break the density (never to zero inside the core: it stays a pocket)
    n = 0.62 * value_noise(rng, h, w, 22) + 0.38 * value_noise(rng, h, w, 9)
    dens = dens * (0.60 + 0.65 * n)
    # feather: a soft blur, then force every canvas edge to zero with a smooth border ramp
    dens = dens / max(1e-6, float(dens.max()))
    im = Image.fromarray(np.clip(dens * 255, 0, 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(3.2))
    dens = np.asarray(im).astype(np.float32) / 255.0
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    edge = np.minimum.reduce([xs, w - 1 - xs, ys, h - 1 - ys]) / 12.0
    dens = dens * np.clip(edge, 0.0, 1.0) ** 1.5
    dens = dens / max(1e-6, float(dens.max()))
    alpha = PEAK * dens
    # colour: the core near-black, the thin fringe a restrained violet
    thin = np.clip(1.0 - dens / 0.55, 0.0, 1.0)[..., None]
    rgb = CORE * (1.0 - thin) + FRINGE * thin
    out = np.dstack([rgb, alpha[..., None] * 255.0])
    return np.clip(out, 0, 255).astype(np.uint8)


def measure(img):
    """What the art measures: peak alpha, core and visible spans (canvas shares), edge alpha, the core's mean colour."""
    arr = np.asarray(img).astype(np.float32)
    a = arr[:, :, 3] / 255.0
    peak = float(a.max())
    res = {"peak_alpha": round(peak, 3), "size": [img.width, img.height]}
    for name, thr in (("core", 0.5), ("visible", 0.08)):
        ys, xs = np.nonzero(a >= thr * peak)
        res[name + "_span"] = [round((xs.max() - xs.min() + 1) / a.shape[1], 3), round((ys.max() - ys.min() + 1) / a.shape[0], 3)]
    res["edge_alpha_max"] = round(float(max(a[0].max(), a[-1].max(), a[:, 0].max(), a[:, -1].max())), 4)
    core = a >= 0.5 * peak
    res["core_mean_rgb"] = [round(float(arr[:, :, c][core].mean()), 1) for c in range(3)]
    return res


WISPS = {
    # name: (seed, w, h, [(cx, cy, rx, ry, weight), ...] in canvas shares)
    "a": (0x5AD0, 176, 120, [(0.50, 0.52, 0.40, 0.38, 1.0), (0.36, 0.44, 0.24, 0.26, 0.85), (0.64, 0.58, 0.25, 0.24, 0.8)]),
    "b": (0x5AD7, 144, 104, [(0.48, 0.50, 0.38, 0.36, 0.9), (0.62, 0.40, 0.22, 0.24, 0.75)]),
}


def preview(images):
    cells = []
    for name, img in images.items():
        for bg_name, proxy in (("on stone grey", False), ("under a dark creature proxy", True)):
            im = img.resize((img.width * 3, img.height * 3), Image.LANCZOS)
            base = Image.new("RGBA", im.size, (52, 50, 54, 255))
            base.alpha_composite(im)
            if proxy:   # the pocket lies UNDER the creature: draw the proxy over it
                d = ImageDraw.Draw(base)
                w, h = im.size
                d.ellipse((w * 0.30, h * 0.34, w * 0.70, h * 0.70), fill=(12, 9, 18, 255), outline=(110, 72, 190, 255), width=4)
                d.ellipse((w * 0.36, h * 0.44, w * 0.40, h * 0.49), fill=(240, 240, 250, 255))
            dd = ImageDraw.Draw(base)
            dd.text((8, 6), f"mist_{name} {bg_name}", fill=(235, 200, 120, 255))
            cells.append(base.convert("RGB"))
    W = sum(c.width for c in cells) + 10 * (len(cells) + 1)
    H = max(c.height for c in cells) + 20
    sheet = Image.new("RGB", (W, H), (40, 36, 44))
    x = 10
    for c in cells:
        sheet.paste(c, (x, 10))
        x += c.width + 10
    return sheet


def main():
    os.makedirs(OUT, exist_ok=True)
    images, spans = {}, {}
    for name, (seed, w, h, layout) in WISPS.items():
        img = Image.fromarray(wisp(seed, w, h, layout), "RGBA")
        key = f"fxp_seeker_mist_{name}"
        path = os.path.join(OUT, key + ".png")
        img.save(path)
        images[name] = img
        spans[key] = measure(img)
        print("wrote", path, spans[key])
    with open(os.path.join(SRC, "seeker_mist_spans.json"), "w", encoding="utf-8") as f:
        json.dump(spans, f, indent=2)
    preview(images).save(os.path.join(SRC, "seeker_mist_sheet.png"))
    print("wrote", os.path.join(SRC, "seeker_mist_spans.json"), "and the sheet")


if __name__ == "__main__":
    main()
