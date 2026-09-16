#!/usr/bin/env python3
"""rhart — the 2026-08-22 arena art pass toolkit (Pillow-based).

    python tools/asset-pipeline/v2/rhart.py fetch  <url> <out.png>
    python tools/asset-pipeline/v2/rhart.py strip  --out <strip.png> [--frame 512] [--fit 0.90]
                                                   [--ground 0.035] [--flip] [--keep 8] [--drop-first]
                                                   [--effect] <frame0.png> <frame1.png> ...
    python tools/asset-pipeline/v2/rhart.py gate   <strip.png> [...]
    python tools/asset-pipeline/v2/rhart.py sheet  <out.png> <strip-or-frame.png> [...]
    python tools/asset-pipeline/v2/rhart.py static <strip.png> <out.png> [--index 0]
    python tools/asset-pipeline/v2/rhart.py info   <png> [...]

Why this exists beside the older pipeline: the 2026-08 pass generates through the PixelLab MCP
tools (characters with explicit east/west rotations, animate_image clips, pixen stills), which
hand back TRANSPARENT PNGs by URL. Nothing here needs the flood-fill knockout or the stdlib PNG
codec — Pillow is available now — but the runtime contract is unchanged and is re-stated here
because every check below protects it:

  * a strip is N SQUARE frames in ONE ROW, frame size = image height (UiKit.AnimSprite,
    VfxPlayer); the filename says `_strip8_512` and must be telling the truth;
  * the figure is bottom-anchored: UiKit measures ONE bottom pad for the whole strip, so the
    feet must sit on the same row in every frame (the `strip` command guarantees it by using
    the UNION bounding box across frames, never a per-frame trim — a per-frame trim would pin
    every frame's feet to the floor and delete the motion);
  * facing is authored, not flipped at draw time: the champion strips face RIGHT, every enemy
    and boss strip faces LEFT. `--flip` exists for the one case where a generator returned the
    mirror, and the sheet command exists so a human looks before anything ships.
"""

from __future__ import annotations

import argparse
import io
import os
import statistics
import sys
import urllib.request

from PIL import Image

ALPHA = 8


# ── io ──────────────────────────────────────────────────────────────────────────────────────────

def fetch(url: str, out: str, tries: int = 4) -> str:
    """Download one PNG. Backblaze stalls now and then; a stalled read is retried, short, rather
    than waited out — four tries at 40 s beats one at 120."""
    import time
    data = b""
    last: Exception | None = None
    for attempt in range(tries):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "rh-art-pipeline/2"})
            data = urllib.request.urlopen(req, timeout=40).read()
            break
        except Exception as exc:  # noqa: BLE001 — any transport failure is retried the same way
            last = exc
            time.sleep(1.5 * (attempt + 1))
    else:
        raise SystemExit(f"fetch: {url} failed after {tries} tries: {last}")
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        # Some endpoints hand back a zip or JSON; refuse loudly rather than filing garbage.
        raise SystemExit(f"fetch: {url} did not return a PNG ({len(data)} bytes, head {data[:16]!r})")
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    with open(out, "wb") as fh:
        fh.write(data)
    return out


def load_rgba(path: str) -> Image.Image:
    return Image.open(path).convert("RGBA")


# ── geometry ────────────────────────────────────────────────────────────────────────────────────

def bbox(img: Image.Image, alpha: int = ALPHA):
    """Bounding box of pixels with alpha > `alpha`, or None."""
    a = img.getchannel("A").point(lambda v: 255 if v > alpha else 0)
    return a.getbbox()


def frames_of(strip: Image.Image) -> list[Image.Image]:
    fw = strip.height
    n = max(1, strip.width // fw)
    return [strip.crop((i * fw, 0, (i + 1) * fw, fw)) for i in range(n)]


def components(img: Image.Image, alpha: int = 16) -> list[int]:
    """Sizes of 4-connected opaque blobs, largest first. Cheap flood fill on a downscaled mask
    (the strips are 512 px; counting blobs at 128 is plenty to catch a floating spike)."""
    small = img.resize((max(1, img.width // 4), max(1, img.height // 4)), Image.NEAREST)
    w, h = small.size
    a = small.getchannel("A").load()
    seen = bytearray(w * h)
    sizes: list[int] = []
    for sy in range(h):
        for sx in range(w):
            if seen[sy * w + sx] or a[sx, sy] <= alpha:
                continue
            stack = [(sx, sy)]
            seen[sy * w + sx] = 1
            size = 0
            while stack:
                x, y = stack.pop()
                size += 1
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[ny * w + nx] and a[nx, ny] > alpha:
                        seen[ny * w + nx] = 1
                        stack.append((nx, ny))
            sizes.append(size)
    return sorted(sizes, reverse=True)


# ── strip assembly ──────────────────────────────────────────────────────────────────────────────

def build_strip(paths: list[str], out: str, frame: int = 512, fit: float = 0.90, ground: float = 0.035,
                flip: bool = False, keep: int = 8, drop_first: bool = False, effect: bool = False) -> str:
    imgs = [load_rgba(p) for p in paths]
    if drop_first and len(imgs) > 1:
        imgs = imgs[1:]
    if keep and len(imgs) > keep:
        imgs = imgs[:keep]
    if flip:
        imgs = [im.transpose(Image.FLIP_LEFT_RIGHT) for im in imgs]

    boxes = [bbox(im) for im in imgs]
    if any(b is None for b in boxes):
        raise SystemExit(f"strip: an input frame is empty ({[i for i, b in enumerate(boxes) if b is None]})")

    # The UNION box across all frames: the figure keeps its own motion inside it, and the feet
    # (the union's bottom) land on the same row in every output frame.
    l = min(b[0] for b in boxes); t = min(b[1] for b in boxes)
    r = max(b[2] for b in boxes); btm = max(b[3] for b in boxes)
    uw, uh = r - l, btm - t

    avail = int(frame * fit)
    scale = avail / max(uw, uh)
    if effect:
        # Effects are centred, not grounded, and keep their own canvas framing: scale the whole
        # canvas so a glow that bleeds to the edge is not pulled off-centre by its bounding box.
        scale = frame / max(imgs[0].width, imgs[0].height)

    # Prefer an INTEGER scale for pixel art when it lands within the budget (2x for 256-px
    # sources, 4x for 128); otherwise nearest-neighbour at the fractional scale — still crisp,
    # and the runtime draws with LinearClamp at non-integer ratios anyway.
    if not effect:
        best_int = int(scale)
        if best_int >= 1 and best_int / scale >= 0.80:
            scale = float(best_int)

    strip = Image.new("RGBA", (frame * len(imgs), frame), (0, 0, 0, 0))
    for i, im in enumerate(imgs):
        if effect:
            sw, sh = max(1, round(im.width * scale)), max(1, round(im.height * scale))
            scaled = im.resize((sw, sh), Image.NEAREST)
            ox = i * frame + (frame - sw) // 2
            oy = (frame - sh) // 2
            strip.alpha_composite(scaled, (ox, oy))
            continue
        crop = im.crop((l, t, r, btm))
        sw, sh = max(1, round(uw * scale)), max(1, round(uh * scale))
        scaled = crop.resize((sw, sh), Image.NEAREST)
        # Bottom-centre on a common ground line `ground` up from the frame floor.
        ox = i * frame + (frame - sw) // 2
        oy = frame - int(frame * ground) - sh
        if oy < 0:
            oy = 0
        strip.alpha_composite(scaled, (ox, oy))

    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    strip.save(out, optimize=True)
    return out


# ── the gate ────────────────────────────────────────────────────────────────────────────────────

MAX_SCALE_DRIFT = 0.12     # bbox height vs the median, per frame
MAX_BASELINE_DRIFT = 0.06  # feet wander, in frame heights
MIN_FILL = 0.08            # of the frame area; below this the frame is "empty"
STRAY_FRACTION = 0.015
FALLEN_FLOOR = 0.22        # a prone body is a quarter to a third of its standing height (see gate)     # a second blob above this share of the main one is a floating part


def gate(path: str, effect: bool = False, loose: bool = False, thrown: bool = False,
         fallen: bool = False) -> list[str]:
    """`loose` is for attack / cast / death clips: a pounce or a collapse changes the silhouette's
    height and baseline on purpose, so the drift bars widen (30 % / 10 %) instead of failing every
    honest lunge. Idle clips keep the tight bars — an idle that drifts is a zoom, not a breath.

    `fallen` is for a DEATH clip and replaces the median scale bar with a standing-height one: no
    frame may be taller than the tallest (the figure standing) by more than the loose bar, and none
    may drop below 22 % of it. A body that ends fully fallen is legitimately a quarter to a third of
    its standing height by the last frame (the hollow seer lies at 28 %, the furnace priest at 37 %), which is the one thing the clip is FOR — and a clip that lies
    flat for its second half has no honest MEDIAN at all (the hollow seer's was its own mid-stagger,
    so its standing frame read as 64 % 'drift'). The first matrix deaths (2026-09-16: thorn ogre 32 %,
    bog weaver 49 %, furnace priest 56 %) were all clean collapses the old bar refused; a generator's
    zoom drift never looks like a monotone fall. The gate was failing the success, as `thrown` once
    did. The baseline bar stays at the loose 10 %: a fallen body still lies on the same ground.

    `thrown` is for a PROJECTILE clip, and it turns off the stray-blob check. That check exists to
    catch a limb the generator detached by accident, and it cannot tell one from a thrown object: the
    Magpie's projectile was rejected for "frame 7 has a disconnected blob (66 vs main 4013)" — the
    gem, in flight, which is the one thing the clip is FOR. A projectile whose object never leaves
    the hand is the failure; the gate was failing the success."""
    max_scale = 0.30 if loose else MAX_SCALE_DRIFT
    max_base = 0.10 if (loose or fallen) else MAX_BASELINE_DRIFT
    strip = load_rgba(path)
    problems: list[str] = []
    fw = strip.height
    if strip.width % fw != 0:
        problems.append(f"width {strip.width} is not a whole multiple of height {fw}")
    fr = frames_of(strip)
    if len(fr) != 8:
        problems.append(f"{len(fr)} frames, expected 8")
    heights, bottoms, fills = [], [], []
    for i, f in enumerate(fr):
        b = bbox(f)
        if b is None:
            problems.append(f"frame {i} is empty")
            continue
        heights.append(b[3] - b[1])
        bottoms.append(b[3])
        opaque = f.getchannel("A").point(lambda v: 255 if v > ALPHA else 0).histogram()[255]
        fills.append(opaque / (fw * fw))
        if not effect and not thrown:
            comps = components(f)
            if len(comps) > 1 and comps[1] > comps[0] * STRAY_FRACTION:
                problems.append(f"frame {i} has a disconnected blob ({comps[1]} vs main {comps[0]})")
    if heights and not effect and fallen:
        standing = max(heights)
        for i, h in enumerate(heights):
            if h < standing * FALLEN_FLOOR:
                problems.append(f"frame {i} is under {FALLEN_FLOOR:.0%} of the standing height (h={h}, standing={standing})")
    elif heights and not effect:
        med = statistics.median(heights)
        for i, h in enumerate(heights):
            if abs(h - med) / med > max_scale:
                problems.append(f"frame {i} scale drift {abs(h - med) / med:.0%} (h={h}, median={med:.0f})")
        base = statistics.median(bottoms)
        for i, bt in enumerate(bottoms):
            if abs(bt - base) / fw > max_base:
                problems.append(f"frame {i} baseline drift {abs(bt - base) / fw:.0%}")
    for i, fl in enumerate(fills):
        if fl < (MIN_FILL if not effect else 0.002):
            problems.append(f"frame {i} nearly empty ({fl:.1%} filled)")
    return problems


def whiten(path: str, floor: int = 96) -> str:
    """Strip an effect strip's colour, keeping its shape and its alpha.

    The contract authors effects WHITE/PALE because the casting skill's Source tint multiplies over
    them at play time: the shape says who cast it, the colour says what it is made of
    (arena-art-contract.md §3.3). A generated effect does not reliably come back colourless -- the
    Seeker's first trap arrived in gold and the Metronome's tick bars in blue -- and re-rolling until
    the generator happens to behave costs a generation each time and never actually guarantees it.

    So the rule is enforced here instead of hoped for: luminance, lifted so the darkest surviving
    pixel is `floor`, written back over the same alpha. Applied to every effect strip as it is filed,
    which makes "authored white/pale" a property of the pipeline rather than of the prompt.
    """
    im = load_rgba(path)
    lum = im.convert("L")
    lo, hi = lum.getextrema()
    if hi > lo:
        # Lift the ramp into [floor, 255] so a dark-ish generated effect still reads as pale.
        scale = (255 - floor) / (hi - lo)
        lum = lum.point(lambda v: min(255, int(floor + (v - lo) * scale)))
    out = Image.merge("RGBA", (lum, lum, lum, im.getchannel("A")))
    out.save(path)
    return path


def glow(path: str) -> str:
    """Make an effect's ALPHA follow its own brightness, so black is nothing at all.

    Effects are composited ADDITIVELY (VfxPlayer.Draw), and additive blending already treats black as
    invisible — but only if the pixel is actually black. `whiten` lifts every strip to a floor of 96 so
    mid-grey shapes read, which is right for a small burst and wrong for anything that covers a figure:
    the aura came back with an OPAQUE 107-grey interior and laid a haze over the champion it is supposed
    to wrap (designer, 2026-08-28: "ortasında kucuk yanan alev cikmayacak").

    alpha = alpha x luminance is the standard additive-VFX contract: white stays fully present, black
    disappears, and everything between contributes exactly its own brightness. Applied AFTER whiten,
    which is what makes the flames bright and the middle empty at the same time.
    """
    im = load_rgba(path)
    lum = im.convert("L")
    a = im.getchannel("A")
    out = Image.merge("RGBA", (im.getchannel("R"), im.getchannel("G"), im.getchannel("B"),
                               Image.eval(Image.merge("L", (a,)), lambda v: v)))
    # alpha *= luminance, per pixel
    ap, lp = out.getchannel("A").load(), lum.load()
    na = Image.new("L", im.size)
    np_ = na.load()
    for y in range(im.height):
        for x in range(im.width):
            np_[x, y] = ap[x, y] * lp[x, y] // 255
    out.putalpha(na)
    out.save(path)
    return path


# ── review sheet ────────────────────────────────────────────────────────────────────────────────

def sheet(out: str, paths: list[str], cell: int = 160, label: bool = True) -> str:
    """One row per input: every frame of a strip (or the single image) scaled to `cell`, on a
    checkered mid-grey so both dark and pale sprites read, with the basename at the left."""
    from PIL import ImageDraw
    rows = []
    for p in paths:
        im = load_rgba(p)
        frs = frames_of(im) if im.width >= im.height * 2 else [im]
        rows.append((os.path.basename(p), frs))
    ncol = max(len(f) for _, f in rows)
    labw = 260 if label else 0
    W = labw + ncol * cell
    H = len(rows) * cell
    img = Image.new("RGBA", (W, H), (70, 70, 78, 255))
    d = ImageDraw.Draw(img)
    for y in range(0, H, 16):
        for x in range(labw, W, 16):
            if ((x // 16) + (y // 16)) % 2 == 0:
                d.rectangle([x, y, x + 15, y + 15], fill=(86, 86, 96, 255))
    for ri, (name, frs) in enumerate(rows):
        if label:
            d.text((8, ri * cell + 8), name[:38], fill=(240, 230, 210, 255))
        for ci, f in enumerate(frs):
            s = f.copy()
            s.thumbnail((cell - 4, cell - 4), Image.NEAREST)
            img.alpha_composite(s, (labw + ci * cell + 2, ri * cell + (cell - s.height) - 2))
        d.line([(0, (ri + 1) * cell - 1), (W, (ri + 1) * cell - 1)], fill=(30, 30, 34, 255))
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    img.save(out)
    return out


def static_from(strip_path: str, out: str, index: int = 0) -> str:
    fr = frames_of(load_rgba(strip_path))
    fr[min(index, len(fr) - 1)].save(out, optimize=True)
    return out


# ── cli ─────────────────────────────────────────────────────────────────────────────────────────

def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(prog="rhart")
    sub = ap.add_subparsers(dest="cmd", required=True)

    f = sub.add_parser("fetch"); f.add_argument("url"); f.add_argument("out")

    s = sub.add_parser("strip")
    s.add_argument("--out", required=True); s.add_argument("--frame", type=int, default=512)
    s.add_argument("--fit", type=float, default=0.90); s.add_argument("--ground", type=float, default=0.035)
    s.add_argument("--flip", action="store_true"); s.add_argument("--keep", type=int, default=8)
    s.add_argument("--drop-first", action="store_true"); s.add_argument("--effect", action="store_true")
    s.add_argument("frames", nargs="+")

    g = sub.add_parser("gate"); g.add_argument("--effect", action="store_true")
    g.add_argument("--loose", action="store_true"); g.add_argument("--thrown", action="store_true")
    g.add_argument("paths", nargs="+")
    sh = sub.add_parser("sheet"); sh.add_argument("out"); sh.add_argument("paths", nargs="+")
    sh.add_argument("--cell", type=int, default=160)
    st = sub.add_parser("static"); st.add_argument("strip"); st.add_argument("out"); st.add_argument("--index", type=int, default=0)
    inf = sub.add_parser("info"); inf.add_argument("paths", nargs="+")
    wh = sub.add_parser("whiten"); wh.add_argument("paths", nargs="+")
    wh.add_argument("--floor", type=int, default=96)
    gl = sub.add_parser("glow"); gl.add_argument("paths", nargs="+")

    a = ap.parse_args(argv)
    if a.cmd == "fetch":
        print(fetch(a.url, a.out)); return 0
    if a.cmd == "strip":
        print(build_strip(a.frames, a.out, a.frame, a.fit, a.ground, a.flip, a.keep, a.drop_first, a.effect)); return 0
    if a.cmd == "gate":
        rc = 0
        for p in a.paths:
            probs = gate(p, a.effect, a.loose, getattr(a, 'thrown', False))
            print(("PASS " if not probs else "FAIL ") + p)
            for pr in probs:
                print("   - " + pr)
            rc |= 1 if probs else 0
        return rc
    if a.cmd == "sheet":
        print(sheet(a.out, a.paths, a.cell)); return 0
    if a.cmd == "static":
        print(static_from(a.strip, a.out, a.index)); return 0
    if a.cmd == "whiten":
        for p in a.paths:
            print(whiten(p, a.floor))
        return 0
    if a.cmd == "glow":
        for p in a.paths:
            print(glow(p))
        return 0
    if a.cmd == "info":
        for p in a.paths:
            im = load_rgba(p); b = bbox(im)
            print(f"{p}: {im.width}x{im.height} bbox={b}")
        return 0
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
