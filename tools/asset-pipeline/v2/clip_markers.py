#!/usr/bin/env python3
"""clip_markers.py -- PROPOSE an action strip's timing markers and hand sockets, then WRITE the reviewed file (ADR-011).

A timing file (<strip>.clip.json, read by ActionClipTiming.Parse) names an action's key moments -- anticipation,
commit, contact | release, recovery, settle -- and the hand SOCKETS the recipe draws from. Authoring one by hand means
scrubbing eight frames for each; this proposes them from the strip itself, and a person keeps or corrects every
proposal on the contact sheet:

  * MARKERS from FRAME-DELTA ENERGY: how much the premultiplied picture changes into each frame. The frame the
    biggest change arrives in (between frames 2 and 6) is the contact / release candidate; the frame before it is the
    commit; the most pulled-back frame before the commit is the anticipation; recovery is two frames after the
    contact (at most 6), settle is the last frame.
  * SOCKETS from THE BRIGHTEST MOVING BLOB: the pixels that differ from frame 0 (the idle-like rest pose), the
    brightest fifth of them, split into connected blobs; the blob with the most light (weighted toward the front: the
    strips face right) is the striking limb; the socket is the centroid of its brightest texels, and the angle points
    from the body's centre through it. A weapon's blade or a lit fist wins; a torso that only shifts does not.

    python tools/asset-pipeline/v2/clip_markers.py propose <id>[_<clip>] [--out DIR]   # draft json + sheet (review)
    python tools/asset-pipeline/v2/clip_markers.py write   [<id>_<clip> ...]          # the REVIEWED files, into assets
    python tools/asset-pipeline/v2/clip_markers.py sheet   [<id>_<clip> ...] --out DIR # sheets of the committed files

`propose` never touches assets: the draft goes to build/tmp/clip_markers/. `write` takes the REVIEWED table below,
where every value a person judged by eye instead of taking the proposal is marked (the `eye` list), and writes it with
a "source" block naming this tool. Every file written here says "place": "own": the strip keeps its own measured
placement (HuntScreen.PlacedAs), so a timing file alone changes nothing on screen.

Runs under the Python that has numpy (`python`, not the `py` launcher here).
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys
from collections import deque

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ROSTER = os.path.join(REPO, "assets", "art", "Animations", "Roster")
DRAFTS = os.path.join(REPO, "build", "tmp", "clip_markers")
FRAMES = 8
ALPHA = 0.04            # a texel is part of the figure above this coverage
MOVE = 0.18             # a texel MOVES when its premultiplied rgba differs from frame 0's by more than this (sum of 4)
GRID = 4                # blobs are found on a 4x4-texel grid (128 x 128 cells per 512 frame)
BRIGHT_SHARE = 0.15     # the socket is the centroid of the blob's brightest 15 % of texels

TOOL = "tools/asset-pipeline/v2/clip_markers.py"

# THE REVIEWED TABLE lives beside this file as data (clip_markers_reviewed.json, 2026-10-03, P1.1). One entry per
# strip: the timing (the animator's durations and which frames a faster beat may shrink), the markers, the sockets
# ({frame: {name: [x, y, deg]}}, fractions of the 512 frame), and `eye`: every marker / socket judged by eye on the
# contact sheet instead of taken from the proposal, with why. The basic attacks are short (ClipShareOfBeat 0.55 of a
# beat): ~600-700 ms authored, the contact ~300-360 ms in.


def load_reviewed() -> dict:
    path = os.path.join(HERE, "clip_markers_reviewed.json")
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


def strip_path(name: str) -> str:
    """'seeker_attack' -> the strip under assets/art/Animations/Roster/seeker_attack/."""
    return os.path.join(ROSTER, name, f"char_{name}_strip8_512.png")


def load_frames(path: str) -> np.ndarray:
    """The strip as [frame, y, x, rgba] floats in 0..1, rgb PREMULTIPLIED (as the game draws it)."""
    im = np.asarray(Image.open(path).convert("RGBA"), dtype=np.float32) / 255.0
    fw = im.shape[0]
    n = im.shape[1] // fw
    out = np.stack([im[:, i * fw:(i + 1) * fw, :] for i in range(n)])
    out[..., :3] *= out[..., 3:4]
    return out


def energy(fr: np.ndarray) -> list[float]:
    """Mean absolute premultiplied change INTO each frame (frame 0 compared with the last: the loop's seam)."""
    return [float(np.abs(fr[i] - fr[i - 1]).sum(axis=-1).mean()) for i in range(len(fr))]


def reach(fr: np.ndarray) -> list[float]:
    """How far forward (right: the strips face right) the figure reaches, as a fraction of the frame (99.5th pct)."""
    out = []
    for f in fr:
        xs = np.nonzero(f[..., 3] > ALPHA)[1]
        out.append(float(np.percentile(xs, 99.5)) / f.shape[1] if xs.size else 0.0)
    return out


def blobs(mask: np.ndarray) -> list[list[tuple[int, int]]]:
    """Connected (8-way) cells of a boolean grid, largest first."""
    h, w = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    found = []
    for y0, x0 in zip(*np.nonzero(mask)):
        if seen[y0, x0]:
            continue
        seen[y0, x0] = True
        q, cells = deque([(y0, x0)]), []
        while q:
            y, x = q.popleft()
            cells.append((y, x))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((ny, nx))
        found.append(cells)
    found.sort(key=len, reverse=True)
    return found


def socket_for(fr: np.ndarray, f: int) -> list[float] | None:
    """The brightest moving blob's bright centroid on frame f, and the angle from the body's centre through it."""
    cur, rest = fr[f], fr[0]
    size = cur.shape[0]
    moving = (np.abs(cur - rest).sum(axis=-1) > MOVE) & (cur[..., 3] > 0.5)
    luma = cur[..., 0] * 0.299 + cur[..., 1] * 0.587 + cur[..., 2] * 0.114
    if not moving.any():
        return None
    # only the BRIGHT moving texels (a blade, a fist's lit knuckles, a lit sleeve end), and of their blobs the one with
    # the most light, weighted toward the front (the strips face right): the striking limb leads the body
    bright = moving & (luma >= np.quantile(luma[moving], 0.80))
    g = size // GRID
    cell_move = bright.reshape(g, GRID, g, GRID).mean(axis=(1, 3)) > 0.10
    cell_light = (luma * bright).reshape(g, GRID, g, GRID).sum(axis=(1, 3))
    best, best_light = None, 0.0
    for cells in blobs(cell_move):
        if len(cells) < 3:
            continue
        front = sum(x for _, x in cells) / len(cells) / g
        light = sum(float(cell_light[y, x]) for y, x in cells) * (0.5 + front)
        if light > best_light:
            best, best_light = cells, light
    if best is None:
        return None
    sel = np.zeros((g, g), dtype=bool)
    for y, x in best:
        sel[y, x] = True
    px = np.repeat(np.repeat(sel, GRID, axis=0), GRID, axis=1) & bright
    ys, xs = np.nonzero(px)
    lv = luma[ys, xs]
    cut = np.quantile(lv, 1.0 - BRIGHT_SHARE)
    keep = lv >= cut
    sx, sy = float(xs[keep].mean()), float(ys[keep].mean())
    bys, bxs = np.nonzero(cur[..., 3] > ALPHA)
    cx, cy = float(bxs.mean()), float(bys.mean())
    deg = math.degrees(math.atan2(sy - cy, sx - cx))
    return [round(sx / size, 3), round(sy / size, 3), round(deg)]


def propose(name: str, kind: str) -> dict:
    """The draft: markers from the energy peak, sockets on the contact / release frame and its neighbours."""
    fr = load_frames(strip_path(name))
    e, r = energy(fr), reach(fr)
    n = len(fr)
    key = int(max(range(2, min(7, n)), key=lambda i: e[i]))
    commit = max(1, key - 1)
    anticipation = int(min(range(0, commit), key=lambda i: r[i])) if commit > 0 else 0
    if anticipation >= commit:
        anticipation = max(0, commit - 1)
    recovery = min(max(key + 1, min(key + 2, n - 2)), n - 1)
    markers = {"anticipation": anticipation, "commit": commit, kind: key, "recovery": recovery, "settle": n - 1}
    sockets = {}
    for f in range(max(1, key - 1), min(n, key + 2)):
        s = socket_for(fr, f)
        if s:
            sockets[str(f)] = {"Hand": s}
    return {"energy": [round(v, 4) for v in e], "reach": [round(v, 3) for v in r], "markers": markers, "sockets": sockets}


def draw_sheet(name: str, markers: dict, sockets: dict, out: str, cell: int = 256, energies: list | None = None) -> None:
    """Eight cells, the marker named under each, every socket drawn as a cross with its angle as a tick."""
    im = Image.open(strip_path(name)).convert("RGBA")
    fw = im.height
    n = im.width // fw
    sheet = Image.new("RGB", (n * cell, cell + 34), (52, 52, 62))
    d = ImageDraw.Draw(sheet)
    by_frame: dict[int, list[str]] = {}
    for m, f in markers.items():
        by_frame.setdefault(int(f), []).append(m)
    for f in range(n):
        fr = im.crop((f * fw, 0, (f + 1) * fw, fw)).resize((cell, cell), Image.LANCZOS)
        sheet.paste(fr, (f * cell, 0), fr)
        d.rectangle((f * cell, 0, f * cell + cell - 1, cell - 1), outline=(90, 90, 104))
        label = f"{f}  " + "/".join(by_frame.get(f, []))
        if energies:
            label += f"  e={energies[f]:.3f}"
        d.text((f * cell + 4, cell + 4), label, fill=(255, 230, 90))
        for sname, (x, y, deg) in (sockets.get(str(f)) or {}).items():
            px, py = f * cell + x * cell, y * cell
            d.line((px - 7, py, px + 7, py), fill=(255, 40, 200), width=2)
            d.line((px, py - 7, px, py + 7), fill=(255, 40, 200), width=2)
            a = math.radians(deg)
            d.line((px, py, px + 18 * math.cos(a), py + 18 * math.sin(a)), fill=(60, 255, 120), width=2)
            d.text((px + 6, py + 6), sname, fill=(255, 120, 230))
    title = f"{name}"
    d.text((4, cell + 18), title, fill=(200, 200, 210))
    os.makedirs(os.path.dirname(out) or ".", exist_ok=True)
    sheet.save(out)


def kind_of(entry: dict) -> str:
    return "release" if "release" in entry.get("markers", {}) else "contact"


def cmd_propose(a) -> int:
    reviewed = load_reviewed()
    out = a.out or DRAFTS
    for name in a.names:
        kind = kind_of(reviewed.get(name, {})) if not a.kind else a.kind
        p = propose(name, kind)
        os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, f"char_{name}_strip8_512.draft.clip.json"), "w", encoding="utf-8") as fh:
            json.dump({"markers": p["markers"], "sockets": p["sockets"], "energy": p["energy"], "reach": p["reach"]}, fh, indent=1)
        draw_sheet(name, p["markers"], p["sockets"], os.path.join(out, f"{name}_proposed.png"), energies=p["energy"])
        print(f"{name}: markers {p['markers']}")
        print(f"   energy {p['energy']}")
        print(f"   sockets {p['sockets']}")
    return 0


def final_doc(name: str, entry: dict) -> dict:
    """The committed file: the reviewed timing, markers and sockets, "place": "own", and the source block."""
    p = propose(name, kind_of(entry))
    doc = {
        "frameMs": entry["frameMs"],
        "elastic": entry["elastic"],
        "place": "own",
        "markers": entry["markers"],
        "sockets": entry["sockets"],
        "source": {
            "tool": f"{TOOL} write {name}",
            "proposed": {"markers": p["markers"], "sockets": p["sockets"]},
            "eye": entry.get("eye", {}),
        },
    }
    if len(doc["frameMs"]) != FRAMES or len(doc["elastic"]) != FRAMES:
        raise SystemExit(f"{name}: frameMs and elastic need {FRAMES} entries")
    return doc


def cmd_write(a) -> int:
    reviewed = load_reviewed()
    for name in a.names or sorted(reviewed):
        doc = final_doc(name, reviewed[name])
        path = strip_path(name)[:-len(".png")] + ".clip.json"
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            json.dump(doc, fh, indent=1)
            fh.write("\n")
        print(f"wrote {os.path.relpath(path, REPO)}")
    return 0


def cmd_sheet(a) -> int:
    reviewed = load_reviewed()
    for name in a.names or sorted(reviewed):
        path = strip_path(name)[:-len(".png")] + ".clip.json"
        with open(path, encoding="utf-8") as fh:
            doc = json.load(fh)
        out = os.path.join(a.out, f"{name}_markers.png")
        draw_sheet(name, doc["markers"], doc["sockets"], out, cell=a.cell)
        print(f"sheet {out}")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("propose")
    p.add_argument("names", nargs="+")
    p.add_argument("--kind", choices=["contact", "release"])
    p.add_argument("--out")
    w = sub.add_parser("write")
    w.add_argument("names", nargs="*")
    s = sub.add_parser("sheet")
    s.add_argument("names", nargs="*")
    s.add_argument("--out", required=True)
    s.add_argument("--cell", type=int, default=160)
    a = ap.parse_args()
    return {"propose": cmd_propose, "write": cmd_write, "sheet": cmd_sheet}[a.cmd](a)


if __name__ == "__main__":
    sys.exit(main())
