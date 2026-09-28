#!/usr/bin/env python3
"""seeker_mist_fangs.py -- the Seeker's JAWS source art: ONE Shadow fang MADE OF MIST, in sixteen condensation states
(the owner, 2026-09-28: "the teeth themselves should be like mist: instead of appearing at once, they come like mist,
bite, and vanish").

    python tools/asset-pipeline/v2/seeker_mist_fangs.py

The fang is the same shape as the approved Shadow Fang (seeker_fangs.py's outline, at 2x), but it is not a hard
sprite: it is a smoky density field that CONDENSES. State 0 is a loose drift of Shadow mist where the fang will be,
wider than the fang, broken by noise, with stray wisps round it and no edge; each later state tightens the field
toward the fang's silhouette, sharpens its edge, lets the violet rim and the pale point come up out of the dark; state
15 is the condensed fang, still smoky at its root (the mist it came from), clean along its biting edge and point. The
runtime shows ONE state per frame (no cross-fade), stepping forward as the fangs gather and close (the mist becomes teeth), holds the last one while they
bite, and plays them BACKWARD as the teeth let go (the teeth dissolve back into mist), so one strip is both the forming
and the dissolving.

The noise field is FIXED across the states (one seed), only its threshold, softness and spread change, so stepping
through the states reads as the same mist condensing, never as a boil. Deterministic. No PixelLab.

    keypose_sources/history/fxp_seeker_mistfang.png (RETIRED, history)   16 states x (144 x 208), left to right; the point at (72, 178) in each,
                                                    pointing DOWN; straight alpha (the asset library premultiplies)
    tools/asset-pipeline/v2/keypose_sources/seeker_mistfang_sheet.png   the states at 2x on stone grey and on black
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import seeker_fangs as F  # noqa: E402  (the approved fang's outline)

REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "tools", "asset-pipeline", "v2", "keypose_sources", "history")   # RETIRED (2026-09-28): JAWS is the misty maw now (seeker_maw.py); this strip is history and never played
SRC = os.path.join(HERE, "keypose_sources")

W, H = 144, 208            # one state's canvas (room for the loose mist: it is wider than the tooth)
STATES = 16                # the runtime draws ONE state per frame (no cross-fade: two 'over' draws thin the tooth)
SCALE = 2.0                # the approved fang's 40 x 72 outline, at 2x
DX, DY = 28.0, 36.0        # where the 2x outline sits in the canvas: its point lands at (72, 178)
TIP = (F.TIP[0] * SCALE + DX, F.TIP[1] * SCALE + DY)
SS = 3                     # supersample for the silhouette's distance field

BODY = np.array([42.0, 27.0, 70.0])        # the condensed Shadow body: a dark violet that still separates from a black creature
MIST_BODY = np.array([74.0, 48.0, 122.0])  # the same body while it is still mist: a violet smoke, lighter than the creatures'
                                           # black, so the gathering is SEEN (it darkens as it condenses into the tooth)
EDGE = np.array([150.0, 84.0, 250.0])      # the violet rim, once condensed
POINT = np.array([170.0, 150.0, 215.0])    # the point, only at the end: pale violet, never as bright as a creature's eyes
WISP = np.array([100.0, 70.0, 158.0])      # the loose wisps round it: the same violet smoke, a touch lighter


def fang_mask():
    """The fang's silhouette at the state canvas (supersampled, then box-filtered): 1 inside, 0 outside."""
    poly =[(((x / F.S) * SCALE + DX) * SS, ((y / F.S) * SCALE + DY) * SS) for x, y in F.outline()]
    im = Image.new("L", (W * SS, H * SS), 0)
    ImageDraw.Draw(im).polygon(poly, fill=255)
    return np.asarray(im.resize((W, H), Image.BOX)).astype(np.float32) / 255.0


def signed_distance(mask):
    """A signed distance to the silhouette's edge, in px (negative inside), by brute force on the small canvas."""
    inside = mask >= 0.5
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    # the edge pixels: inside pixels with an outside 4-neighbour
    pad = np.pad(inside, 1, constant_values=False)
    edge = inside & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
    ey, ex = np.nonzero(edge)
    d = np.full((H, W), 1e9, np.float32)
    for k in range(0, len(ey), 64):
        dy = ys[..., None] - ey[None, None, k:k + 64]
        dx = xs[..., None] - ex[None, None, k:k + 64]
        d = np.minimum(d, np.sqrt(dx * dx + dy * dy).min(axis=2))
    return np.where(inside, -d, d)


def value_noise(rng, cells):
    ch, cw = max(2, int(H / cells)), max(2, int(W / cells))
    lattice = (rng.random((ch + 1, cw + 1)) * 255).astype(np.uint8)
    return np.asarray(Image.fromarray(lattice, "L").resize((W, H), Image.BICUBIC)).astype(np.float32) / 255.0


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def state(c, sd, n_body, n_wisp, n_fine):
    """One condensation state, c in [0, 1] (0 = loose mist, 1 = the condensed fang). RGBA float in [0, 1]."""
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    # the field: the fang's distance, inflated while loose (the mist is wider than the tooth it becomes), its boundary
    # broken by the fixed noise (wide and ragged while loose, a whisper when condensed), softened by a width that
    # tightens from a fog to a clean edge
    inflate = 13.0 * (1.0 - c) ** 1.2
    ragged = (18.0 * (1.0 - c) ** 1.1 + 1.5) * (n_body - 0.5) * 2.0
    soft = 9.0 * (1.0 - c) ** 1.4 + 0.9
    d = sd - inflate + ragged
    core = 1.0 / (1.0 + np.exp(d / soft))
    # the root stays smoky even when condensed: the mist the tooth came from (the base is the top of the canvas)
    root = smoothstep(TIP[1] - 30.0, TIP[1] - 140.0, ys)          # 0 near the point, 1 at the root
    core = core * (1.0 - 0.55 * root * (0.6 + 0.4 * n_fine) * (0.4 + 0.6 * c))
    # density: loose mist is thin; the condensed tooth is solid
    density = core * (0.55 + 0.45 * c ** 0.8)
    # stray wisps round the tooth while it is still mist (gone once it has condensed)
    around = np.exp(-np.maximum(sd, 0.0) / 22.0) * (sd > -2.0)
    wisps = np.clip((n_wisp - 0.48) * 3.0, 0.0, 1.0) * around * (1.0 - c) ** 1.4 * 0.85
    # the condensed tooth's INSIDE is a little translucent (a creature's eye under it still shows); its rim stays solid
    body_alpha = density * (1.0 - 0.22 * c * c * (1.0 - smoothstep(-6.5, -1.0, d)))
    alpha = np.clip(np.maximum(body_alpha, wisps), 0.0, 1.0)
    # colour: the dark body; the violet rim along the edge once it has condensed; the pale point at the very end
    rim_band = smoothstep(-6.5, -1.0, d) * (d < 1.5)
    rim = rim_band * smoothstep(0.35, 1.0, c) * (1.0 - root) ** 0.8      # the rim runs most of the tooth; none at the smoky root
    point = smoothstep(0.7, 1.0, c) * np.exp(-(((xs - TIP[0]) / 5.0) ** 2 + ((ys - TIP[1] + 5.0) / 9.0) ** 2))
    body = MIST_BODY * (1.0 - c ** 0.7) + BODY * c ** 0.7
    rgb = body[None, None] * np.ones((H, W, 1))
    wisp_share = np.clip(wisps / np.maximum(alpha, 1e-4), 0.0, 1.0)[..., None]
    rgb = rgb * (1.0 - wisp_share) + WISP * wisp_share
    rgb = rgb * (1.0 - rim[..., None]) + EDGE * rim[..., None]
    rgb = rgb * (1.0 - point[..., None]) + POINT * point[..., None]
    alpha = np.maximum(alpha, point * 0.95)
    # nothing touches the canvas edge
    edge = np.minimum.reduce([xs, W - 1 - xs, ys, H - 1 - ys]) / 16.0
    alpha = alpha * smoothstep(0.0, 1.0, edge)
    return np.dstack([rgb / 255.0, alpha[..., None]])


def main():
    os.makedirs(OUT, exist_ok=True)
    mask = fang_mask()
    sd = signed_distance(mask)
    rng = np.random.default_rng(0x5EED)
    n_body = 0.6 * value_noise(rng, 14) + 0.4 * value_noise(rng, 6)
    n_wisp = 0.65 * value_noise(rng, 18) + 0.35 * value_noise(rng, 7)
    n_fine = value_noise(rng, 5)
    strip = Image.new("RGBA", (W * STATES, H), (0, 0, 0, 0))
    for k in range(STATES):
        c = k / (STATES - 1)
        im = Image.fromarray(np.clip(state(c, sd, n_body, n_wisp, n_fine) * 255.0, 0, 255).astype(np.uint8), "RGBA")
        # a touch of blur on the loose states only (the condensed tooth keeps its clean edge)
        radius = 2.2 * (1.0 - c) ** 1.5
        if radius > 0.2:
            im = im.filter(ImageFilter.GaussianBlur(radius))
        strip.alpha_composite(im, (k * W, 0))
    path = os.path.join(OUT, "fxp_seeker_mistfang.png")
    strip.save(path)
    a = np.asarray(strip)[:, :, 3]
    print("wrote", path, strip.size, "peak alpha per state",
          [round(float(a[:, k * W:(k + 1) * W].max()) / 255, 2) for k in range(STATES)],
          "mean alpha per state", [round(float(a[:, k * W:(k + 1) * W].mean()) / 255, 3) for k in range(STATES)])
    # the preview: every state at 2x on stone grey (top) and on black (bottom)
    big = strip.resize((strip.width * 2, strip.height * 2), Image.LANCZOS)
    sheet = Image.new("RGB", (big.width + 20, big.height * 2 + 30), (40, 36, 44))
    for row, bg in enumerate(((70, 67, 72), (10, 8, 12))):
        base = Image.new("RGBA", big.size, bg + (255,))
        base.alpha_composite(big)
        sheet.paste(base.convert("RGB"), (10, 10 + row * (big.height + 10)))
    sheet.save(os.path.join(SRC, "seeker_mistfang_sheet.png"))
    print("wrote", os.path.join(SRC, "seeker_mistfang_sheet.png"))


if __name__ == "__main__":
    main()
