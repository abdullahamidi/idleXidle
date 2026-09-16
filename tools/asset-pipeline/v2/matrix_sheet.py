#!/usr/bin/env python3
"""matrix_sheet — the enemy matrix on one page: six region rows by four archetype columns, each cell the
creature's idle frame 0 drawn the way the arena sizes it, with its name under it; the region's boss in a
fifth column for the boss audit.

    python tools/asset-pipeline/v2/matrix_sheet.py <out.png> [--root assets|staging] [--frame idle|attack]

Sizing follows the renderer, not the PNG: the body is scaled so its OWN top pad fills the box
(UiKit.TopPadFraction / DrawScale, least headroom over every frame of the strip), bottom-anchored, and
the box is the arena's enemy box times HuntScreen.ArchetypeScale. So the sheet answers the contact
question the arena asks: can a Swarm, a Caster, an Armoured and a Bruiser of one region be told apart
by silhouette at the sizes they are really drawn at, and does each region read as one family?

Reads spec.json enemy_matrix for the rows (key, region, archetype, name); the catalogue test keeps that
in step with EnemyPresentation.cs. Needs Pillow.
"""
from __future__ import annotations

import argparse
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
FRAME = 512
ROLES = ("Swarm", "Caster", "Armoured", "Bruiser")
SCALE = {"Swarm": 0.58, "Caster": 0.78, "Armoured": 0.92, "Bruiser": 1.12}   # HuntScreen.ArchetypeScale
BOSSES = {"verdant_hollow": ("thorn_regent", "THORN REGENT"), "cinderworks": ("forge_colossus", "FORGE COLOSSUS"),
          "umbral_reach": ("void_reaper", "VOID REAPER"), "marrow_wastes": ("spirit_matron", "SPIRIT MATRON"),
          "still_archive": ("crystal_lich", "CRYSTAL LICH"), "pale_choir": ("lumen_angel", "LUMEN ANGEL")}
REGION_NAMES = {"verdant_hollow": "VERDANT HOLLOW", "cinderworks": "CINDERWORKS", "umbral_reach": "UMBRAL REACH",
                "marrow_wastes": "MARROW WASTES", "still_archive": "THE STILL ARCHIVE", "pale_choir": "THE PALE CHOIR"}
CELL = 300          # the Bruiser box height on the sheet; the other roles scale down from it
LABEL = 34
GUTTER = 16
ROW_HEAD = 190


def strip_path(root: str, key: str, clip: str, boss: bool = False) -> str:
    if root == "staging" and not boss:
        return os.path.join(REPO, "tools", "asset-pipeline", ".staging", "v2", "matrix", f"{key}_{clip}_strip8_512.png")
    sub = "Bosses" if boss else "Enemies"
    return os.path.join(REPO, "assets", "art", "Animations", sub, f"{key}_{clip}", f"{key}_{clip}_strip8_512.png")


def top_pad(strip: Image.Image) -> float:
    """The least headroom any frame has, as a fraction of the frame (UiKit.TopPadFraction)."""
    alpha = strip.getchannel("A")
    least = FRAME
    for i in range(strip.width // FRAME):
        box = alpha.crop((i * FRAME, 0, (i + 1) * FRAME, FRAME)).getbbox()
        if box:
            least = min(least, box[1])
    return least / FRAME


def bottom_pad(strip: Image.Image) -> float:
    alpha = strip.getchannel("A")
    most = 0
    for i in range(strip.width // FRAME):
        box = alpha.crop((i * FRAME, 0, (i + 1) * FRAME, FRAME)).getbbox()
        if box:
            most = max(most, box[3])
    return (FRAME - most) / FRAME if most else 0.0


def figure(path: str, clip_index: int, box_h: int) -> Image.Image:
    strip = Image.open(path).convert("RGBA")
    pad_top, pad_bottom = top_pad(strip), bottom_pad(strip)
    frame = strip.crop((clip_index * FRAME, 0, (clip_index + 1) * FRAME, FRAME))
    frame = frame.crop((0, int(pad_top * FRAME), FRAME, FRAME - int(pad_bottom * FRAME)))
    scale = box_h / max(1, frame.height)
    return frame.resize((max(1, int(frame.width * scale)), box_h), Image.LANCZOS)


def font(size: int) -> ImageFont.ImageFont:
    for name in ("arialbd.ttf", "DejaVuSans-Bold.ttf", "arial.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("--root", choices=("assets", "staging"), default="assets")
    ap.add_argument("--frame", default="idle")
    ap.add_argument("--index", type=int, default=0)
    a = ap.parse_args(argv)
    spec = json.load(open(os.path.join(HERE, "spec.json"), encoding="utf-8"))
    items = spec["enemy_matrix"]["items"]
    regions: list[str] = []
    for item in items.values():
        if item["region"] not in regions:
            regions.append(item["region"])
    col_w = int(CELL * 1.05)
    width = ROW_HEAD + (len(ROLES) + 1) * (col_w + GUTTER) + GUTTER
    row_h = CELL + LABEL + GUTTER
    height = 60 + len(regions) * row_h
    sheet = Image.new("RGBA", (width, height), (24, 20, 26, 255))
    draw = ImageDraw.Draw(sheet)
    head, small = font(22), font(16)
    for c, role in enumerate(ROLES + ("BOSS",)):
        x = ROW_HEAD + GUTTER + c * (col_w + GUTTER)
        draw.text((x + col_w // 2, 30), role.upper(), fill=(214, 196, 150, 255), font=head, anchor="mm")
    for r, region in enumerate(regions):
        y0 = 60 + r * row_h
        draw.rectangle((0, y0, width, y0 + row_h - GUTTER // 2), fill=(32, 27, 34, 255) if r % 2 == 0 else (28, 24, 30, 255))
        draw.text((GUTTER, y0 + CELL // 2), REGION_NAMES.get(region, region), fill=(214, 196, 150, 255), font=small, anchor="lm")
        cells = []
        for role in ROLES:
            key, item = next((k, v) for k, v in items.items() if v["region"] == region and v["archetype"] == role)
            cells.append((strip_path(a.root, key, a.frame), int(CELL * SCALE[role] / SCALE["Bruiser"]), item["name"]))
        boss_key, boss_name = BOSSES[region]
        cells.append((strip_path("assets", boss_key, a.frame, boss=True), CELL, boss_name))
        for c, (path, box_h, name) in enumerate(cells):
            x = ROW_HEAD + GUTTER + c * (col_w + GUTTER)
            ground = y0 + CELL
            draw.line((x, ground, x + col_w, ground), fill=(70, 60, 60, 255))
            if not os.path.exists(path):
                draw.text((x + col_w // 2, ground - 20), "MISSING", fill=(220, 80, 60, 255), font=small, anchor="mm")
            else:
                fig = figure(path, a.index, box_h)
                if fig.width > col_w:
                    fig = fig.resize((col_w, int(fig.height * col_w / fig.width)), Image.LANCZOS)
                sheet.alpha_composite(fig, (x + (col_w - fig.width) // 2, ground - fig.height))
            draw.text((x + col_w // 2, ground + LABEL // 2 + 2), name, fill=(236, 226, 206, 255), font=small, anchor="mm")
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    sheet.convert("RGB").save(a.out)
    print(f"sheet -> {a.out} ({width}x{height})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
