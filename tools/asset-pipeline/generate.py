#!/usr/bin/env python3
"""Generate any manifest asset that is missing from assets/art.

Usage:
    export PIXELLAB_API_TOKEN='<token>'
    python3 tools/asset-pipeline/generate.py            # generate everything missing
    python3 tools/asset-pipeline/generate.py --dry-run  # list what would be generated
    python3 tools/asset-pipeline/generate.py --only icon_role_attacker
    python3 tools/asset-pipeline/generate.py --force    # regenerate even if present

Every generated PNG goes through background knockout and a validation gate before
it is written into assets/art — PixelLab returns fully opaque images, so an
unprocessed drop-in would render as a solid rectangle over the UI.

Exit status is non-zero if any requested asset failed, so CI and hooks can gate.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import threading
from concurrent import futures

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import pixellab_client as api  # noqa: E402
from knockout import fit_canvas, knockout, stats, trim_and_center, upscale_integer  # noqa: E402
from pixelpng import read, write  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
MANIFEST = os.path.join(HERE, "manifest.json")
STAGING = os.path.join(HERE, ".staging")

# AssetLibrary.ValidateRuntimeAssetPath throws on these substrings, killing the
# game at startup. Refuse to write such a path rather than ship a crash.
FORBIDDEN = (
    "/preview/", "source_reference", "source_sheet", "concept",
    "contact_sheet", "runtime_assets_preview", "poster", "showcase",
    "mood_board", "pitchboard",
)

# A knockout that clears almost nothing means the background was not detected;
# one that clears essentially everything means it ate the subject.
#
# The ceiling is deliberately loose: sparse chrome is legitimately almost all
# empty. A 64x64 corner bracket is a thin L-shape and measured 94.3% clear —
# a correct result that a 92% ceiling rejected. The real "it ate the subject"
# signal is having almost no opaque pixels left at all, which MIN_OPAQUE_PX
# catches directly rather than inferring from a ratio.
MIN_TRANSPARENT_PCT = 8.0
MAX_TRANSPARENT_PCT = 99.0
MIN_OPAQUE_PX = 120


def load_manifest() -> dict:
    with open(MANIFEST, encoding="utf-8") as fh:
        return json.load(fh)


def existing_keys() -> dict[str, str]:
    """Every runtime asset basename already on disk -> its path."""
    found: dict[str, str] = {}
    root = os.path.join(REPO, "assets", "art")
    for dirpath, _dirs, files in os.walk(root):
        norm = dirpath.replace("\\", "/")
        if any(skip in norm + "/" for skip in ("/native/", "/preview/", "/mask/", "/medallion/")):
            continue
        for fname in files:
            if fname.lower().endswith(".png"):
                found.setdefault(os.path.splitext(fname)[0], os.path.join(dirpath, fname))
    return found


def build_prompt(manifest: dict, asset: dict) -> str:
    """The asset's full prompt: its own verbatim `prompt`, or its subject in its style's template.

    A few assets are one-offs whose wording fights every shared style (the drawn pauldron, whose
    style prefix reintroduced the body nouns that summoned a whole cuirass). Those carry a literal
    `prompt` instead of a `subject`."""
    if asset.get("prompt"):
        return asset["prompt"]
    defaults = manifest["defaults"]
    template = manifest["styles"][asset.get("style", "medallion")]
    return template.format(
        subject=asset["subject"],
        style_suffix=defaults["style_suffix"],
    )


def check_path(dest_rel: str) -> None:
    norm = ("/" + dest_rel.replace("\\", "/")).lower()
    for token in FORBIDDEN:
        if token in norm:
            raise ValueError(
                f"destination '{dest_rel}' contains forbidden token '{token}' — "
                "AssetLibrary would throw at startup"
            )


def process(
    raw_path: str,
    out_path: str,
    size: int,
    do_knockout: bool,
    height: int | None = None,
    upscale: int = 1,
    loose_alpha: bool = False,
) -> dict:
    """Knockout + normalise a downloaded PNG, then write it to its final home."""
    img = read(raw_path)
    report: dict = {}
    if do_knockout:
        img, report = knockout(img)
        # Square assets get a padded, centred canvas so icons share an optical
        # weight. Non-square assets (stretched bar art) keep their aspect.
        if height is None or height == size:
            img = trim_and_center(img, size, pad=4)
        else:
            img = fit_canvas(img, size, height)
    # Opaque full-screen art is never trimmed — cropping to "content" on a scene
    # that fills its canvas would shave off the edges the composition needs.
    if upscale > 1:
        img = upscale_integer(img, upscale)
    info = stats(img)

    pct = info["transparent_pct"]
    if do_knockout:
        opaque = img.w * img.h - int(round(pct / 100.0 * img.w * img.h))
        # The floor catches an icon that failed to knock out — those ship as an
        # opaque rectangle over the dark UI, which is the original bug this whole
        # pipeline exists to prevent. But it does NOT apply to full-bleed chrome:
        # a panel or bar legitimately covers its whole canvas, so 2-4%
        # transparency is the correct answer, not a mis-detection.
        floor = 0.0 if loose_alpha else MIN_TRANSPARENT_PCT
        if not (floor <= pct <= MAX_TRANSPARENT_PCT):
            raise ValueError(
                f"knockout produced {pct}% transparency (expected "
                f"{floor}-{MAX_TRANSPARENT_PCT}%) — background likely mis-detected"
            )
        if opaque < MIN_OPAQUE_PX:
            raise ValueError(
                f"only {opaque}px survived knockout (min {MIN_OPAQUE_PX}) — the subject was eaten"
            )

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    write(out_path, img)
    return {**report, **info}


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dry-run", action="store_true", help="list missing assets, generate nothing")
    ap.add_argument("--force", action="store_true", help="regenerate assets that already exist")
    ap.add_argument("--only", action="append", default=[], metavar="KEY", help="restrict to these keys")
    ap.add_argument("--replace", action="store_true",
                    help="regenerate keys that already exist, overwriting them in place")
    args = ap.parse_args()

    manifest = load_manifest()
    defaults = manifest["defaults"]
    on_disk = existing_keys()

    queue = []
    for asset in manifest["assets"]:
        key = asset["key"]
        if args.only and key not in args.only:
            continue
        if key in on_disk and not args.force and not args.replace:
            continue
        check_path(os.path.join(asset["dest"], key + ".png"))
        queue.append(asset)

    if not queue:
        print("Nothing to generate — every manifest asset is present.")
        return 0

    print(f"{len(queue)} asset(s) to generate:")
    for asset in queue:
        print(f"  {asset['key']:32s} -> {asset['dest']}/{asset['key']}.png")
    if args.dry_run:
        return 0

    try:
        api._token()
    except api.MissingToken as exc:
        print(f"\n{exc}", file=sys.stderr)
        return 2

    os.makedirs(STAGING, exist_ok=True)
    failures: list[tuple[str, str]] = []
    done = 0
    lock = threading.Lock()

    def one(asset: dict) -> None:
        """Generate, knock out, and file a single asset."""
        nonlocal done
        key = asset["key"]
        try:
            raw = os.path.join(STAGING, f"{key}.raw.png")
            # If this key already exists on disk, overwrite THAT file rather than
            # writing a second copy at the manifest's dest. Keys are flat
            # basenames, so a second copy would be a collision, and the loader
            # would then have to pick between the old art and the new.
            out = on_disk.get(key) or os.path.join(REPO, asset["dest"], f"{key}.png")

            upscale = int(asset.get("upscale", 1))
            if asset.get("model") == "pixen":
                # Full-screen art: pixen reaches 768/side vs pixflux's 400.
                api.save_image(
                    api.create_image_pixen(
                        build_prompt(manifest, asset),
                        width=asset.get("width", defaults["width"]),
                        height=asset.get("height", defaults["height"]),
                        no_background=asset.get("knockout", False),
                        outline=asset.get("outline"),
                        detail=asset.get("detail"),
                        view=asset.get("view"),
                    ),
                    raw,
                )
            else:
                api.save_image(
                    api.create_image(
                        build_prompt(manifest, asset),
                        width=asset.get("width", defaults["width"]),
                        height=asset.get("height", defaults["height"]),
                        # Per-asset override. A full-bleed fill must be generated WITH a background:
                        # asked for "nothing but colour" on no_background, the model classes the whole
                        # canvas as background and returns a fully transparent image.
                        no_background=asset.get("no_background", defaults["no_background"]),
                        outline=asset.get("outline", defaults["outline"]),
                        shading=asset.get("shading", defaults["shading"]),
                        detail=asset.get("detail", defaults["detail"]),
                        text_guidance_scale=asset.get("text_guidance_scale", defaults["text_guidance_scale"]),
                        negative_description=asset.get("negative_description"),
                    ),
                    raw,
                )

            info = process(
                raw, out,
                size=asset.get("width", defaults["width"]),
                do_knockout=asset.get("knockout", defaults["knockout"]),
                height=asset.get("height", defaults["height"]),
                upscale=upscale,
                # Full-bleed chrome covers its canvas; no transparency floor.
                loose_alpha=asset.get("style") == "chrome",
            )
            with lock:
                done += 1
                print(f"  [ok]   {key:32s} {info['size']} {info['transparent_pct']:>5}% clear")
        except Exception as exc:  # noqa: BLE001 - one bad asset must not abort the run
            with lock:
                failures.append((key, str(exc)))
                print(f"  [FAIL] {key:32s} {str(exc)[:160]}", file=sys.stderr)

    # The endpoint is synchronous, so concurrency is just parallel requests.
    # Stay under the server's in-flight ceiling or it starts returning 429.
    print(f"\nGenerating ({api.MAX_IN_FLIGHT} at a time)...")
    with futures.ThreadPoolExecutor(max_workers=api.MAX_IN_FLIGHT) as pool:
        list(pool.map(one, queue))

    print(f"\n{done} generated, {len(failures)} failed.")
    for key, err in failures:
        print(f"  FAILED {key}: {err}", file=sys.stderr)
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
