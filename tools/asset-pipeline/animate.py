#!/usr/bin/env python3
"""Generate 8-frame animation strips from existing sprites.

    python3 tools/asset-pipeline/animate.py --dry-run
    python3 tools/asset-pipeline/animate.py
    python3 tools/asset-pipeline/animate.py --only wisp_idle

Reads `animations.json`, animates each entry's source sprite via
`/animate-with-text-v3`, and assembles the result into the horizontal strip the
runtime expects.

Strip format, from `UiKit.cs:156-165` and `VfxPlayer.cs:65-66`: frames are
assumed SQUARE and laid out in a single row at y=0, so `frameWidth = height`
and `frameCount = width / height`. A width that is not a whole multiple of the
height makes the renderer refuse the draw.

Two details that matter:

* The API caps `first_frame` at 256x256, but the runtime naming convention is
  `_strip8_512`. Frames come back at 256 and are upscaled x2 with
  nearest-neighbour — an integer scale on pixel art is visually lossless, and it
  keeps the file honest about the size its name claims.
* The endpoint returns `frame_count + 1` images: the last one closes the loop
  back to the first. Only the first 8 are kept, which is what makes the loop
  seamless rather than double-hitching on the repeated frame.

Cost is one generation per frame — 8 per strip.
"""

from __future__ import annotations

import argparse
import base64
import json
import os
import sys
import threading
import time
from concurrent import futures

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import pixellab_client as api  # noqa: E402
from knockout import knockout, stats  # noqa: E402
from pixelpng import Image, read, write  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
SPEC = os.path.join(HERE, "animations.json")
STAGING = os.path.join(HERE, ".staging")

API_MAX_INPUT = 256   # /animate-with-text-v3 first_frame ceiling
FRAME_OUT = 512       # runtime frame size implied by the _512 suffix
# Each worker polls its own job, so the poll rate multiplies by worker count.
# At 5s x 6 workers the status GETs themselves started drawing 429s, and the
# retry backoff then stalled jobs that had already finished server-side.
POLL_INTERVAL = 10.0
POLL_TIMEOUT = 900.0


def scale_nearest(img: Image, size: int) -> Image:
    """Nearest-neighbour resample to a square canvas — preserves hard edges."""
    out = Image(size, size)
    sx, sy = img.w / size, img.h / size
    for y in range(size):
        row_src = min(img.h - 1, int(y * sy))
        for x in range(size):
            r, g, b, a = img.get(min(img.w - 1, int(x * sx)), row_src)
            out.set(x, y, r, g, b, a)
    return out


def pad_to_fraction(img: Image, fraction: float) -> Image:
    """Letterbox a sprite so its CONTENT occupies `fraction` of a square canvas.

    The animation endpoint re-frames what it is given: fed a full-body figure that filled
    its canvas, it returned a waist-up bust — every character's strip was their top half,
    and the arena drew a torso standing on the floor with no legs.

    It cannot be told not to. What it can be given is room: a figure occupying 62% of the
    canvas survives the zoom as a whole body, because the crop it applies lands inside the
    margin instead of inside the character. This is the same move the rig source made for
    the cutter — author the input so the tool succeeds, rather than arguing with the tool.
    """
    xs = [x for x in range(img.w)
          if any(img.get(x, y)[3] > 8 for y in range(img.h))]
    ys = [y for y in range(img.h)
          if any(img.get(x, y)[3] > 8 for x in range(img.w))]
    if not xs or not ys:
        return img

    l, r, t, b = xs[0], xs[-1] + 1, ys[0], ys[-1] + 1
    cw, ch = r - l, b - t
    side = max(1, int(round(max(cw, ch) / max(0.05, min(0.95, fraction)))))

    out = Image(side, side)
    # Centred horizontally, and sat on the FLOOR of the canvas rather than centred
    # vertically: a character re-framed from a bottom-anchored source keeps its feet.
    ox = (side - cw) // 2
    oy = side - ch - max(2, side // 40)
    for y in range(ch):
        for x in range(cw):
            cr, cg, cb, ca = img.get(l + x, t + y)
            if ca > 0:
                out.set(ox + x, oy + y, cr, cg, cb, ca)
    return out


def encode_png(img: Image) -> str:
    tmp = os.path.join(STAGING, f"_enc_{threading.get_ident()}.png")
    write(tmp, img)
    with open(tmp, "rb") as fh:
        data = fh.read()
    os.remove(tmp)
    return base64.b64encode(data).decode()


def animate(
    first_frame_b64: str,
    action: str,
    frame_count: int = 8,
    drift_threshold: float | None = None,
    seed: int | None = None,
) -> list[Image]:
    """Run one animation job to completion and return its frames.

    ``drift_threshold`` is the de-flicker control: frames whose foreground
    drifts too far from the first frame get pulled back. Lower it when a strip
    comes back with a frame that breaks up or an element that mutates between
    frames — the default lets the model wander on high-motion actions.
    """
    body: dict = {
        "first_frame": {"type": "base64", "base64": first_frame_b64, "format": "png"},
        "action": action,
        "frame_count": frame_count,
        "no_background": True,
    }
    if drift_threshold is not None:
        body["drift_threshold"] = drift_threshold
    if seed is not None:
        body["seed"] = seed
    started = api._request("POST", "/animate-with-text-v3", body)
    job_id = started.get("background_job_id")
    if not job_id:
        raise api.PixelLabError(f"no background_job_id: {json.dumps(started)[:200]}")

    deadline = time.time() + POLL_TIMEOUT
    while time.time() < deadline:
        time.sleep(POLL_INTERVAL)
        info = api._request("GET", f"/background-jobs/{job_id}")
        status = str(info.get("status", "")).lower()
        if status == "completed":
            images = (info.get("last_response") or {}).get("images") or []
            if len(images) < frame_count:
                raise api.PixelLabError(f"job {job_id} returned {len(images)} frames, need {frame_count}")
            frames = []
            for entry in images[:frame_count]:
                raw = base64.b64decode(entry["base64"] if isinstance(entry, dict) else entry)
                tmp = os.path.join(STAGING, f"_frm_{threading.get_ident()}.png")
                with open(tmp, "wb") as fh:
                    fh.write(raw)
                frames.append(read(tmp))
                os.remove(tmp)
            return frames
        if status == "failed":
            raise api.PixelLabError(f"job {job_id} failed: {json.dumps(info)[:250]}")
    raise api.PixelLabError(f"job {job_id} timed out after {POLL_TIMEOUT:.0f}s")


def build_strip(frames: list[Image], frame_size: int = FRAME_OUT) -> Image:
    """Lay frames out left-to-right in a single row of square cells."""
    strip = Image(frame_size * len(frames), frame_size)
    for i, frame in enumerate(frames):
        cell = scale_nearest(frame, frame_size)
        ox = i * frame_size
        for y in range(frame_size):
            src = y * frame_size * 4
            dst = (y * strip.w + ox) * 4
            strip.px[dst : dst + frame_size * 4] = cell.px[src : src + frame_size * 4]
    return strip


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--only", action="append", default=[], metavar="KEY")
    ap.add_argument("--workers", type=int, default=2, help="parallel animation jobs (each costs 8 generations)")
    args = ap.parse_args()

    with open(SPEC, encoding="utf-8") as fh:
        spec = json.load(fh)

    queue = []
    for entry in spec["animations"]:
        key = entry["key"]
        if args.only and key not in args.only:
            continue
        out = os.path.join(REPO, entry["dest"], f"{key}.png")
        if os.path.exists(out) and not args.force:
            continue
        if entry.get("source"):
            src = os.path.join(REPO, entry["source"])
            if not os.path.exists(src):
                print(f"  [SKIP] {key:34s} source missing: {entry['source']}", file=sys.stderr)
                continue
        elif not entry.get("prompt"):
            print(f"  [SKIP] {key:34s} needs either 'source' or 'prompt'", file=sys.stderr)
            continue
        queue.append(entry)

    if not queue:
        print("Nothing to animate — every strip is present.")
        return 0

    print(f"{len(queue)} strip(s) to generate ({len(queue) * 8} generations):")
    for entry in queue:
        origin = os.path.basename(entry["source"]) if entry.get("source") else "(generated frame 1)"
        print(f"  {entry['key']:34s} <- {origin}  '{entry['action']}'")
    if args.dry_run:
        return 0

    os.makedirs(STAGING, exist_ok=True)
    failures: list[tuple[str, str]] = []
    done = 0
    lock = threading.Lock()

    def one(entry: dict) -> None:
        nonlocal done
        key = entry["key"]
        try:
            if entry.get("source"):
                # Animate an existing sprite so the motion stays on-model.
                source = read(os.path.join(REPO, entry["source"]))
            else:
                # VFX have no sprite to animate from, so draw frame 1 first.
                # Costs 1 extra generation on top of the 8 animation frames.
                raw = os.path.join(STAGING, f"{key}.first.png")
                api.save_image(
                    api.create_image(
                        entry["prompt"],
                        width=API_MAX_INPUT,
                        height=API_MAX_INPUT,
                        no_background=True,
                        outline=entry.get("outline"),
                        shading=entry.get("shading", "medium shading"),
                        detail=entry.get("detail", "medium detail"),
                        text_guidance_scale=entry.get("text_guidance_scale", 9),
                    ),
                    raw,
                )
                source = read(raw)
            if entry.get("pad_fraction"):
                source = pad_to_fraction(source, float(entry["pad_fraction"]))
            first = scale_nearest(source, API_MAX_INPUT)
            frames = animate(
                encode_png(first),
                entry["action"],
                entry.get("frame_count", 8),
                drift_threshold=entry.get("drift_threshold"),
                seed=entry.get("seed"),
            )

            cleaned = []
            for frame in frames:
                if entry.get("knockout", True) and frame.px[3] == 255:
                    frame, _ = knockout(frame)
                cleaned.append(frame)

            strip = build_strip(cleaned, entry.get("frame_size", FRAME_OUT))
            out = os.path.join(REPO, entry["dest"], f"{key}.png")
            os.makedirs(os.path.dirname(out), exist_ok=True)
            write(out, strip)
            info = stats(strip)
            with lock:
                done += 1
                print(f"  [ok]   {key:34s} {info['size']} {info['transparent_pct']:>5}% clear")
        except Exception as exc:  # noqa: BLE001
            with lock:
                failures.append((key, str(exc)))
                print(f"  [FAIL] {key:34s} {str(exc)[:150]}", file=sys.stderr)

    print(f"\nAnimating ({args.workers} at a time)...")
    with futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        list(pool.map(one, queue))

    print(f"\n{done} strip(s) generated, {len(failures)} failed.")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
