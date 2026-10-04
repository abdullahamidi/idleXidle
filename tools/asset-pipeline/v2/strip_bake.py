#!/usr/bin/env python3
"""strip_bake.py -- take the BAKED VFX out of a champion's action strip, so the recipe's own effect is the only one.

Four basic-attack strips were generated with an effect painted into the figure (design.md section 6, CLEAN): a white
flash on the Thornwall's shield bash, a neon-green arc (and its glowing blade) on the Magpie's nick, flying bone charms
and a blue glow on the Chorus's throw, an orange glow and a spark on the Unbroken's fling. A baked flash fights the
recipe's Source-tinted light and fires on the strip's frame, not on the fight's beat. This removes each:

  1. KEY the baked colour, per strip and per frame range: a STRICT core (the effect's own saturated hue), kept only in
     connected pieces of at least `min_area` texels (an armour highlight or a pack gem is smaller than any flash),
     grown into the effect's softer FRINGE within a few texels. The Chorus's flying charms are keyed by being DETACHED
     (alpha pieces not joined to the body), not by colour: her belt carries the same bone charms.
  2. FILL each keyed piece from a DONOR: of the strip's frames (and a small shift), the one whose picture around the
     piece matches best, never through a texel keyed there itself. Where the donor's figure is opaque the texel takes
     the donor's colour (the body behind the flash); where it is clear the texel is cleared (the flash in the air).
  3. FEATHER the cut: opaque texels within one SOURCE pixel (the strip's art-pixel block, at least 2 px) of a texel this
     cleared fade with their distance from it, so no hard cut edge is left where an effect touched the outline.

    python tools/asset-pipeline/v2/strip_bake.py clean [name ...] [--sheet DIR]   # rewrite the strips in assets
    python tools/asset-pipeline/v2/strip_bake.py check [name ...]                 # exit 1 while a baked hue remains
    python tools/asset-pipeline/v2/strip_bake.py sheet [name ...] --out DIR [--rev REV]   # before (git) / after sheet

The originals stay in git history (`git show <rev>:<path>`); nothing is deleted. `clean` is idempotent: a strip with
no baked hue left keys nothing and is rewritten byte-identical in its pixels.
Runs under the Python that has numpy (`python`, not the `py` launcher here).
"""
from __future__ import annotations

import argparse
import io
import os
import subprocess
import sys
from collections import deque

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ROSTER = os.path.join(REPO, "assets", "art", "Animations", "Roster")
BASE_REV = "702576d6"   # the strips as generated (Phase 0's commit): the BEFORE of every sheet


def _white(px):
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    return np.minimum(np.minimum(r, g), b) > 236


def _white_fringe(px):
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    lo, hi = np.minimum(np.minimum(r, g), b), np.maximum(np.maximum(r, g), b)
    return (lo > 175) & (hi - lo < 40)


def _neon_green(px):
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    return (g > 180) & (g - r > 80) & (g - b > 40)


def _green_fringe(px):
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    lo = np.minimum(np.minimum(r, g), b)
    return ((g > 110) & (g - r > 45) & (g - b > 15)) | (lo > 200)   # the arc's white-hot middle is part of it


def _glow_blue(px):
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    return (b > 150) & (b - r > 40)


def _blue_fringe(px):
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    return (b > 110) & (b - r > 25)


def _ember(px):
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    return ((r > 180) & (r - b > 100) & (r - g > 10)) | ((r > 200) & (g > 170) & (b < 140))   # orange, and its yellow core


def _ember_fringe(px):
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    lo = np.minimum(np.minimum(r, g), b)
    # generous on purpose: what the flood reaches is FILLED from a donor frame, so leather it takes comes back as leather
    deep = (r > 15) & (r >= np.maximum(g, b) * 1.5 + 8)   # the glow's near-black maroon shadow tones
    return ((r > 70) & (r - g > 22) & (r - b > 22)) | deep | ((r > 200) & (g > 150) & (b < 160)) | (lo > 190)


# THE CLEANS (design.md section 6). frames: the 0-based frames the effect is baked into (counted on the sheet; design.md
# says "frames 3-5" for the Thornwall's flash, which is 1-based: the flash is on 0-based 2, 3 and 4). grow: how far the
# effect is grown from its core THROUGH fringe-coloured texels, in texels. min_area: the smallest core piece that is
# the effect. reach: also take fringe colour this near the core, joined or not (a glow's tint across dark seams).
# detached: also clear small pieces cut off from the figure (a flash's flying specks, a thrown charm).
CLEANS = {
    "thornwall_attack": dict(frames=[2, 3, 4], core=_white, fringe=_white_fringe, grow=4, min_area=180, detached=True,
                             what="the white impact flash on the shield bash"),
    "magpie_attack": dict(frames=[3, 4, 5, 6, 7], core=_neon_green, fringe=_green_fringe, grow=16, min_area=250, detached=True,
                          what="the neon-green slash arc and its glowing blade"),
    "chorus_attack": dict(frames=[5, 6, 7], core=_glow_blue, fringe=_blue_fringe, grow=4, min_area=120, detached=True,
                          what="the flying bone charms and the blue glow at the hand (the charm held on frame 4 stays)"),
    "unbroken_attack": dict(frames=[4, 5, 6], core=_ember, fringe=_ember_fringe, grow=24, reach=20, min_area=150, detached=True,
                            what="the orange chest / tabard glow, the fireball at the fist and the spark"),
}


def strip_path(name: str) -> str:
    return os.path.join(ROSTER, name, f"char_{name}_strip8_512.png")


def load(path_or_bytes) -> np.ndarray:
    im = Image.open(io.BytesIO(path_or_bytes) if isinstance(path_or_bytes, bytes) else path_or_bytes).convert("RGBA")
    return np.asarray(im, dtype=np.int32).copy()


def frames_of(strip: np.ndarray) -> list[np.ndarray]:
    fw = strip.shape[0]
    return [strip[:, i * fw:(i + 1) * fw] for i in range(strip.shape[1] // fw)]


def components(mask: np.ndarray) -> list[np.ndarray]:
    """8-connected pieces of a boolean mask, each as an (N, 2) array of (y, x)."""
    h, w = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    out = []
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
        out.append(np.array(cells))
    return out


def dilate(mask: np.ndarray, r: int) -> np.ndarray:
    out = mask.copy()
    for _ in range(r):
        m = out.copy()
        m[1:] |= out[:-1]; m[:-1] |= out[1:]; m[:, 1:] |= out[:, :-1]; m[:, :-1] |= out[:, 1:]
        out = m
    return out


def distance_to(mask: np.ndarray, r: int) -> np.ndarray:
    """Chessboard distance (capped at r + 1) from every texel to the nearest True texel of mask."""
    d = np.full(mask.shape, r + 1, dtype=np.int32)
    ring = mask.copy()
    d[ring] = 0
    for k in range(1, r + 1):
        grown = dilate(ring, 1)
        d[grown & ~ring] = k
        ring = grown
    return d


def block_size(frame: np.ndarray) -> int:
    """The strip's art pixel in texels: the column period its colour changes keep (1 for a native-resolution strip)."""
    ch = (frame[:, 1:] != frame[:, :-1]).any(-1).sum(0)
    for k in (4, 3, 2):
        tot = [int(ch[i::k].sum()) for i in range(k)]
        if max(tot) > 0.9 * sum(tot):
            return k
    return 1


RESIDUE = 28   # texels: a core piece of ANY size this close to where the effect was is the effect's residue


def baked_mask(frame: np.ndarray, spec: dict, near: np.ndarray | None = None) -> np.ndarray:
    """The texels of the baked effect on one frame: the core pieces big enough to be it (or any core piece close to
    `near`, where an earlier pass found the effect: its last flecks), grown into their fringe."""
    opaque = frame[..., 3] > 20
    core = spec["core"](frame) & opaque
    keep = np.zeros_like(core)
    close = dilate(near, RESIDUE) if near is not None and near.any() else None
    for c in components(core):
        if len(c) >= spec["min_area"] or (close is not None and close[c[:, 0], c[:, 1]].any()):
            keep[c[:, 0], c[:, 1]] = True
    # grown THROUGH the fringe, a texel at a time: only fringe joined to the core, never a like colour elsewhere
    fringe = spec["fringe"](frame) & opaque
    out = keep.copy()
    for _ in range(spec["grow"]):
        grown = dilate(out, 1) & (fringe | out)
        if np.array_equal(grown, out):
            break
        out = grown
    # reach: fringe-coloured texels NEAR the core though not joined to it (a glow's tint spills across dark seams)
    if spec.get("reach"):
        out |= fringe & dilate(keep, spec["reach"])
    if spec.get("detached"):
        out |= detached(frame)
    return out


def detached(frame: np.ndarray) -> np.ndarray:
    """Opaque pieces not joined to the figure's largest piece (a thrown object in the air)."""
    opaque = frame[..., 3] > 20
    parts = components(opaque)
    out = np.zeros_like(opaque)
    if not parts:
        return out
    parts.sort(key=len, reverse=True)
    for c in parts[1:]:
        if len(c) < 0.05 * len(parts[0]):
            out[c[:, 0], c[:, 1]] = True
    return out


def best_donor(frames: list[np.ndarray], masks: list[np.ndarray], f: int, piece: np.ndarray, search: int = 24):
    """(donor frame, dy, dx) whose picture round the piece matches frame f best, never keyed under the piece."""
    cur = frames[f]
    size = cur.shape[0]
    pm = np.zeros(cur.shape[:2], dtype=bool)
    pm[piece[:, 0], piece[:, 1]] = True
    ring = dilate(pm, 14) & ~dilate(masks[f], 2)
    ry, rx = np.nonzero(ring)
    best = None
    for d in range(len(frames)):
        if d == f:
            continue
        for dy in range(-search, search + 1, 2):
            for dx in range(-search, search + 1, 2):
                py, px = piece[:, 0] - dy, piece[:, 1] - dx
                if py.min() < 0 or px.min() < 0 or py.max() >= size or px.max() >= size:
                    continue
                if masks[d][py, px].mean() > 0.02:
                    continue
                sy, sx = np.clip(ry - dy, 0, size - 1), np.clip(rx - dx, 0, size - 1)
                err = float(np.abs(frames[d][sy, sx] - cur[ry, rx]).mean()) if ry.size else 0.0
                if best is None or err < best[0]:
                    best = (err, d, dy, dx)
    return best


def clean_strip(strip: np.ndarray, spec: dict, near: list | None = None) -> tuple[np.ndarray, list[str], list]:
    """One pass: key, fill from donors, feather. `near`: per frame, where earlier passes found the effect."""
    frames = [f.copy() for f in frames_of(strip)]
    src = [f.copy() for f in frames]
    masks = [baked_mask(f, spec, near[i] if near else None) if i in spec["frames"] else np.zeros(f.shape[:2], dtype=bool)
             for i, f in enumerate(src)]
    feather = max(2, block_size(src[0]))
    log = []
    for f in spec["frames"]:
        if not masks[f].any():
            log.append(f"frame {f}: nothing baked")
            continue
        cleared = np.zeros(masks[f].shape, dtype=bool)
        for piece in components(masks[f]):
            if len(piece) < 4:
                frames[f][piece[:, 0], piece[:, 1]] = 0
                cleared[piece[:, 0], piece[:, 1]] = True
                continue
            pick = best_donor(src, masks, f, piece)
            if pick is None:
                frames[f][piece[:, 0], piece[:, 1]] = 0
                cleared[piece[:, 0], piece[:, 1]] = True
                log.append(f"frame {f}: piece of {len(piece)} cleared (no donor)")
                continue
            err, d, dy, dx = pick
            donor = src[d][piece[:, 0] - dy, piece[:, 1] - dx]
            solid = donor[:, 3] > 20
            frames[f][piece[solid, 0], piece[solid, 1]] = donor[solid]
            frames[f][piece[~solid, 0], piece[~solid, 1]] = 0
            cleared[piece[~solid, 0], piece[~solid, 1]] = True
            log.append(f"frame {f}: piece of {len(piece)} from frame {d} shifted ({dx},{dy}), ring error {err:.1f}; "
                       f"{int(solid.sum())} filled, {int((~solid).sum())} cleared")
        # FEATHER the cut: opaque texels near a cleared one fade with the distance (one source pixel, >= 2 texels)
        if cleared.any():
            dist = distance_to(cleared, feather)
            near = (dist >= 1) & (dist <= feather) & (frames[f][..., 3] > 0)
            fade = dist[near].astype(np.float32) / (feather + 1)
            frames[f][near, 3] = np.round(frames[f][near, 3] * fade).astype(np.int32)
        # a texel left fully clear keeps no colour (straight alpha, but no stray rgb under 0 alpha)
        frames[f][frames[f][..., 3] == 0] = 0
    out = np.concatenate(frames, axis=1)
    return out, log, masks


def feather_cut(before: np.ndarray, after: np.ndarray) -> np.ndarray:
    """THE FEATHER over every pass at once: a later pass may fill a texel beside one an earlier pass cleared, so the
    whole cut (cleared against the original) is softened once more: alpha capped at distance / (one source px + 1)."""
    out = after.copy()
    fw = before.shape[0]
    feather = max(2, block_size(before[:, :fw]))
    for i in range(before.shape[1] // fw):
        b, f = before[:, i * fw:(i + 1) * fw], out[:, i * fw:(i + 1) * fw]
        cleared = (b[..., 3] > 20) & (f[..., 3] == 0)
        if not cleared.any():
            continue
        dist = distance_to(cleared, feather)
        near = (dist >= 1) & (dist <= feather) & (f[..., 3] > 0)
        cap = np.round(255.0 * dist[near] / (feather + 1)).astype(np.int32)
        f[near, 3] = np.minimum(f[near, 3], cap)
        f[f[..., 3] == 0] = 0
    return out


def remaining(strip: np.ndarray, spec: dict) -> list[tuple[int, int]]:
    """(frame, texels) of the baked core still present in the cleaned frames (core pieces >= min_area)."""
    res = []
    for i, f in enumerate(frames_of(strip)):
        if i not in spec["frames"]:
            continue
        core = spec["core"](f) & (f[..., 3] > 20)
        n = sum(len(c) for c in components(core) if len(c) >= spec["min_area"])
        if spec.get("detached"):
            n += int(detached(f).sum())
        res.append((i, n))
    return res


def hard_cuts(before: np.ndarray, after: np.ndarray) -> int:
    """Texels where the clean left a FULL-alpha texel beside a texel it cleared (a hard cut edge): 0 when feathered.

    Only texels the clean left UNCHANGED count: an object painted back in afterwards (the Magpie's dagger,
    magpie_dagger.py) has its own drawn outline beside the cleared air, which is the object's edge, not a cut.
    """
    cleared = (before[..., 3] > 20) & (after[..., 3] == 0)
    if not cleared.any():
        return 0
    unchanged = (np.abs(after.astype(np.int32) - before.astype(np.int32)).sum(axis=-1) == 0)
    return int((dilate(cleared, 1) & (after[..., 3] >= 250) & unchanged).sum())


def save(strip: np.ndarray, path: str) -> None:
    Image.fromarray(strip.astype(np.uint8), "RGBA").save(path, optimize=True)


def git_bytes(rev: str, path: str) -> bytes:
    rel = os.path.relpath(path, REPO).replace(os.sep, "/")
    return subprocess.run(["git", "show", f"{rev}:{rel}"], cwd=REPO, capture_output=True, check=True).stdout


def contact_sheet(before: np.ndarray, after: np.ndarray, frames: list[int], out: str, cell: int = 256, title: str = "") -> None:
    """Rows BEFORE / AFTER of the cleaned frames on a mid ground and on a light ground (a cut edge shows on both)."""
    grounds = [(60, 60, 72), (196, 192, 184)]
    rows = [(before, "before", g) for g in grounds[:1]] + [(after, "after", g) for g in grounds]
    sheet = Image.new("RGB", (len(frames) * cell, len(rows) * cell + 22), (30, 30, 36))
    d = ImageDraw.Draw(sheet)
    for r, (strip, label, ground) in enumerate(rows):
        fs = frames_of(strip)
        for k, f in enumerate(frames):
            im = Image.fromarray(fs[f].astype(np.uint8), "RGBA").resize((cell, cell), Image.LANCZOS)
            bg = Image.new("RGB", (cell, cell), ground)
            bg.paste(im, (0, 0), im)
            sheet.paste(bg, (k * cell, r * cell))
            d.text((k * cell + 4, r * cell + 4), f"{label} {f}", fill=(255, 220, 60))
    d.text((4, len(rows) * cell + 4), title, fill=(220, 220, 230))
    os.makedirs(os.path.dirname(out) or ".", exist_ok=True)
    sheet.save(out)


def cmd_clean(a) -> int:
    for name in a.names or sorted(CLEANS):
        spec = CLEANS[name]
        path = strip_path(name)
        before = load(path)
        # passes until nothing is keyed: a charm joined to the glow is only DETACHED once the glow is gone
        after, log, found = clean_strip(before, spec)
        for p in range(2, 5):
            again, more, masks = clean_strip(after, spec, found)
            found = [a | b for a, b in zip(found, masks)]
            if np.array_equal(again, after):
                break
            after = again
            log += [f"pass {p}: {line}" for line in more if "nothing baked" not in line]
        print(f"{name}: {spec['what']}")
        for line in log:
            print("   " + line)
        after = feather_cut(before, after)
        if not np.array_equal(after, before):
            save(after, path)
            print(f"   wrote {os.path.relpath(path, REPO)}")
        if a.sheet:
            contact_sheet(before, after, spec["frames"], os.path.join(a.sheet, f"{name}_clean.png"), title=name)
    return 0


def cmd_check(a) -> int:
    bad = 0
    for name in a.names or sorted(CLEANS):
        spec = CLEANS[name]
        strip = load(strip_path(name))
        left = remaining(strip, spec)
        total = sum(n for _, n in left)
        try:
            cuts = hard_cuts(load(git_bytes(BASE_REV, strip_path(name))), strip)
        except subprocess.CalledProcessError:
            cuts = 0
        ok = total == 0 and cuts == 0
        bad += 0 if ok else 1
        print(f"{'OK  ' if ok else 'FAIL'} {name}: baked texels left {left}, hard cut texels {cuts}")
    return 1 if bad else 0


def cmd_sheet(a) -> int:
    for name in a.names or sorted(CLEANS):
        spec = CLEANS[name]
        path = strip_path(name)
        before = load(git_bytes(a.rev, path))
        after = load(path)
        out = os.path.join(a.out, f"{name}_before_after.png")
        contact_sheet(before, after, spec["frames"], out, cell=a.cell, title=f"{name}: {spec['what']} (before = {a.rev})")
        print(f"sheet {out}")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    c = sub.add_parser("clean"); c.add_argument("names", nargs="*"); c.add_argument("--sheet")
    k = sub.add_parser("check"); k.add_argument("names", nargs="*")
    s = sub.add_parser("sheet"); s.add_argument("names", nargs="*"); s.add_argument("--out", required=True)
    s.add_argument("--rev", default=BASE_REV); s.add_argument("--cell", type=int, default=200)
    a = ap.parse_args()
    return {"clean": cmd_clean, "check": cmd_check, "sheet": cmd_sheet}[a.cmd](a)


if __name__ == "__main__":
    sys.exit(main())
