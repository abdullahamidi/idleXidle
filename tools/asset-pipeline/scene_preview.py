#!/usr/bin/env python3
"""Composite the HUNT screen offscreen, from the real coordinates and real art.

    python3 tools/asset-pipeline/scene_preview.py [--region nature] [--out preview.png]

The game is MonoGame DesktopGL and needs a display; this environment is headless
WSL, so the screen cannot be launched to check a layout change. This reproduces
the draw order and, critically, the exact fit maths of `UiKit.Sprite`:

    sc = box.Height / srcH
    dest = (box.Center.X - w/2, box.Bottom - box.Height, w, box.Height)

which scales the WHOLE texture — transparent padding included — to fill the box.
That is why characters float: a sprite whose lower rows are empty has its visible
feet sitting above box.Bottom by exactly the height of that padding.

Overlays (--overlay) preview the equipment-on-character work.
"""

from __future__ import annotations

import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from derive import bbox, scale_nearest, slice_strip  # noqa: E402
from pixelpng import Image, read, write  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
ART = os.path.join(REPO, "assets", "art")

W, H = 1920, 1080

# --- literal geometry from SoloExpeditionScreen.cs -------------------------
CURRENT = {
    "GROUND_Y": 735,                            # :48
    "CHAMP_BOX": (560 - 180, 735 - 390, 360, 390),   # :62
    "ENEMY_BOX": (1160 - 175, 735 - 340, 350, 340),  # :65
    "ARENA_RECT": (170, 120, 1320, 650),             # :138
    "BOSS_ANCHOR": (1210, 750),                      # :82
    "PANEL": None,
}

# Proposed: combat shifted right to clear a vertical panel down the left, and
# the ground line dropped onto the regenerated stages' walkable floor band
# (the backdrops now put their root/horizon line at ~74% and the foreground lip
# at ~85%, so 850 sits on the flat ground between them).
#
# GroundY 890: the host nav rail is an OPAQUE bar at (0, 934, 1920, 146)
# (Game1.cs:1852), so anything below 934 is simply hidden. 890 leaves 44 px for
# feet and the contact shadow while still sitting on the stages' floor band.
PROPOSED = {
    "GROUND_Y": 1000,
    "CHAMP_BOX": (760 - 224, 1000 - 500, 448, 500),
    "ENEMY_BOX": (1320 - 218, 1000 - 440, 436, 440),
    "ARENA_RECT": (448, 100, 1106, 940),
    "BOSS_ANCHOR": (1360, 1010),
    "PANEL": (196, 200, 236, 720),
    "NAV": (0, 0, 180, 1080),
}

# Mirrors SoloExpeditionScreen.Sockets: (cx, cy, h) as fractions of the champion box.
SOCKETS = {
    "chest":  (0.50, 0.47, 0.25),
    "boots":  (0.50, 0.90, 0.19),
    "gloves": (0.66, 0.62, 0.12),
    "helm":   (0.50, 0.25, 0.18),
    "weapon": (0.33, 0.60, 0.40),
}
# Back-to-front, matching OverlayOrder.
SOCKET_ORDER = ["chest", "boots", "gloves", "helm", "weapon"]

ENEMY_FOR_SOURCE = {
    "body": "bonecrawler", "mind": "soul_leech", "nature": "wisp",
    "machine": "stone_sentinel", "shadow": "shadeling", "spirit": "rift_guardian",
}


_INDEX: dict[str, str] | None = None


def find(key: str) -> str | None:
    """Locate an asset by key, indexing the tree once.

    Walking assets/art per lookup was fine for two sprites and pathological for
    a gear preview that resolves a dozen — the walk dominated the render.
    """
    global _INDEX
    if _INDEX is None:
        _INDEX = {}
        for dirpath, _dirs, files in os.walk(ART):
            for f in files:
                if f.endswith(".png"):
                    _INDEX.setdefault(f[:-4], os.path.join(dirpath, f))
    return _INDEX.get(key)


def load(key: str) -> Image | None:
    p = find(key)
    return read(p) if p else None


def blit(dst: Image, src: Image, x: int, y: int) -> None:
    """Alpha-composite src onto dst at (x, y)."""
    for sy in range(src.h):
        dy = y + sy
        if not (0 <= dy < dst.h):
            continue
        for sx in range(src.w):
            dx = x + sx
            if not (0 <= dx < dst.w):
                continue
            so = (sy * src.w + sx) * 4
            a = src.px[so + 3]
            if not a:
                continue
            do = (dy * dst.w + dx) * 4
            if a == 255:
                dst.px[do:do + 4] = src.px[so:so + 4]
            else:
                for c in range(3):
                    dst.px[do + c] = (src.px[so + c] * a + dst.px[do + c] * (255 - a)) // 255
                dst.px[do + 3] = 255


def draw_sprite(dst: Image, tex: Image, box, top_crop: float = 0.0, ground_anchor: bool = False) -> tuple[int, int]:
    """Reproduce UiKit.Sprite. Returns (visible_bottom_y, foot_gap_px).

    With ground_anchor the draw is shifted down so the sprite's CONTENT bottom
    lands on box.Bottom — this is the proposed fix, previewed side by side.
    """
    bx, by, bw, bh = box
    crop_y = int(tex.h * max(0.0, min(top_crop, 0.6)))
    src_h = tex.h - crop_y
    sc = bh / src_h
    w = max(1, int(tex.w * sc))

    cropped = Image(tex.w, src_h)
    cropped.px[:] = tex.px[crop_y * tex.w * 4:]
    scaled = scale_nearest(cropped, w, bh)

    x = bx + bw // 2 - w // 2
    y = by + bh - bh  # box.Bottom - box.Height, i.e. box.Y

    box_bottom = by + bh
    bb = bbox(scaled)
    visible_bottom = (y + bb[3]) if bb else box_bottom
    gap = box_bottom - visible_bottom

    if ground_anchor and gap:
        y += gap
        visible_bottom = box_bottom

    blit(dst, scaled, x, y)
    return visible_bottom, gap


def hline(dst: Image, y: int, colour, thickness: int = 3, x0: int = 0, x1: int = W) -> None:
    for yy in range(y, min(dst.h, y + thickness)):
        for xx in range(max(0, x0), min(dst.w, x1)):
            o = (yy * dst.w + xx) * 4
            dst.px[o:o + 3] = bytes(colour)
            dst.px[o + 3] = 255


def rect_outline(dst: Image, r, colour, t: int = 3) -> None:
    x, y, w, h = r
    hline(dst, y, colour, t, x, x + w)
    hline(dst, y + h - t, colour, t, x, x + w)
    for yy in range(max(0, y), min(dst.h, y + h)):
        for xx in (x, x + w - t):
            for k in range(t):
                if 0 <= xx + k < dst.w:
                    o = (yy * dst.w + xx + k) * 4
                    dst.px[o:o + 3] = bytes(colour)
                    dst.px[o + 3] = 255


def build(region: str, ground_anchor: bool, show_guides: bool, L=None, gear: bool = False) -> tuple[Image, dict]:
    L = L or CURRENT
    GROUND_Y = L["GROUND_Y"]; CHAMP_BOX = L["CHAMP_BOX"]; ENEMY_BOX = L["ENEMY_BOX"]
    ARENA_RECT = L["ARENA_RECT"]; PANEL = L["PANEL"]
    canvas = Image(W, H)
    for i in range(W * H):
        canvas.px[i * 4:i * 4 + 4] = bytes((0x1B, 0x16, 0x20, 255))

    bg = load(f"bg_arena_{region}")
    if bg:
        blit(canvas, bg if (bg.w, bg.h) == (W, H) else scale_nearest(bg, W, H), 0, 0)

    info: dict = {}

    hunter = load("hunter_idle")
    if hunter:
        vb, gap = draw_sprite(canvas, hunter, CHAMP_BOX, 0.03, ground_anchor)
        info["hunter_foot_gap"] = gap
        info["hunter_visible_bottom"] = vb

    mob = ENEMY_FOR_SOURCE.get(region, "wisp")
    enemy = load(f"{mob}_idle_01")
    if enemy:
        vb, gap = draw_sprite(canvas, enemy, ENEMY_BOX, 0.08, ground_anchor)
        info["enemy_foot_gap"] = gap
        info["enemy_visible_bottom"] = vb

    if gear:
        bx, by, bw, bh = CHAMP_BOX
        for slot in SOCKET_ORDER:
            cx, cy, hh = SOCKETS[slot]
            tex = load(f"item_{slot}_keen") or load(f"item_slot_{slot}")
            if not tex:
                continue
            h = max(1, int(bh * hh))
            w = max(1, int(tex.w * (h / tex.h)))
            sm = scale_nearest(tex, w, h)
            blit(canvas, sm, int(bx + bw * cx) - w // 2, int(by + bh * cy) - h // 2)

    nav = L.get("NAV")
    if nav:
        nx, ny, nw, nh = nav
        for yy in range(ny, min(H, ny + nh)):
            for xx in range(nx, min(W, nx + nw)):
                o = (yy * W + xx) * 4
                for c in range(3):
                    canvas.px[o + c] = ((0x0C, 0x09, 0x16)[c] * 245 + canvas.px[o + c] * 10) // 255
        for i in range(1, 8):
            hline(canvas, ny + i * (nh // 8), (0x22, 0x1C, 0x30), 2, nx + 26, nx + nw - 26)

    if PANEL:
        px, py, pw, ph = PANEL
        for yy in range(py, min(H, py + ph)):
            for xx in range(px, min(W, px + pw)):
                o = (yy * W + xx) * 4
                for c in range(3):
                    o2 = (0x1B, 0x16, 0x20)[c]
                    canvas.px[o + c] = (o2 * 232 + canvas.px[o + c] * 23) // 255
        rect_outline(canvas, PANEL, (0xF0, 0xA8, 0x30), 3)

    if show_guides:
        hline(canvas, GROUND_Y, (0xF0, 0xA8, 0x30), 3)          # GroundY — gold
        rect_outline(canvas, ARENA_RECT, (0xD8, 0x48, 0x3A), 2)  # arena — red
        rect_outline(canvas, CHAMP_BOX, (0x3F, 0xA9, 0xC9), 2)   # champ box — cyan
        rect_outline(canvas, ENEMY_BOX, (0x5C, 0x8A, 0x3A), 2)   # enemy box — green
    return canvas, info


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--region", default="nature")
    ap.add_argument("--out", default="/tmp/hunt_preview.png")
    ap.add_argument("--scale", type=int, default=2, help="downscale divisor for the written file")
    ap.add_argument("--ground-anchor", action="store_true", help="preview the content-bottom fix")
    ap.add_argument("--no-guides", action="store_true")
    ap.add_argument("--layout", choices=["current", "proposed"], default="current")
    ap.add_argument("--gear", action="store_true", help="draw equipped gear at its sockets")
    args = ap.parse_args()

    canvas, info = build(args.region, args.ground_anchor, not args.no_guides,
                         PROPOSED if args.layout == "proposed" else CURRENT, args.gear)
    out = canvas if args.scale <= 1 else scale_nearest(canvas, W // args.scale, H // args.scale)
    write(args.out, out)

    print(f"region={args.region} ground_anchor={args.ground_anchor} -> {args.out}")
    for k, v in info.items():
        print(f"  {k}: {v}")
    if "hunter_foot_gap" in info:
        print(f"\nGroundY={(PROPOSED if args.layout=='proposed' else CURRENT)['GROUND_Y']}; a positive foot gap is how far the sprite floats above it.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
