#!/usr/bin/env python3
"""Ingest already-generated PixelLab jobs into assets/art.

`generate.py` is the unattended path (REST + token). This is the interactive
path: when generation was driven through the PixelLab MCP tools, the images
already exist server-side and only need fetching, knocking out, and filing.

    python3 tools/asset-pipeline/ingest.py icon_role_attacker=<job-id> ...
    python3 tools/asset-pipeline/ingest.py --from-file jobs.txt

Destinations and canvas sizes come from manifest.json, so an ingested asset is
placed and validated exactly like a generated one. Job download URLs are public,
so no API token is needed here.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from generate import (  # noqa: E402
    MANIFEST, REPO, STAGING, check_path, process,
)

MCP_DOWNLOAD = "https://api.pixellab.ai/mcp/images/{job_id}/download"


def fetch(job_id: str, dest: str) -> str:
    with urllib.request.urlopen(MCP_DOWNLOAD.format(job_id=job_id), timeout=120) as resp:
        payload = resp.read()
    if payload[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"job {job_id}: response was not a PNG ({len(payload)} bytes)")
    with open(dest, "wb") as fh:
        fh.write(payload)
    return dest


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("pairs", nargs="*", metavar="KEY=JOB_ID")
    ap.add_argument("--from-file", metavar="PATH", help="file of KEY=JOB_ID lines (# comments allowed)")
    args = ap.parse_args()

    pairs: list[tuple[str, str]] = []
    for raw in args.pairs:
        key, _, job = raw.partition("=")
        pairs.append((key.strip(), job.strip()))
    if args.from_file:
        with open(args.from_file, encoding="utf-8") as fh:
            for line in fh:
                line = line.split("#", 1)[0].strip()
                if line:
                    key, _, job = line.partition("=")
                    pairs.append((key.strip(), job.strip()))

    if not pairs:
        ap.error("no KEY=JOB_ID pairs given")

    with open(MANIFEST, encoding="utf-8") as fh:
        manifest = json.load(fh)
    defaults = manifest["defaults"]
    by_key = {a["key"]: a for a in manifest["assets"]}

    os.makedirs(STAGING, exist_ok=True)
    failures = 0
    for key, job_id in pairs:
        asset = by_key.get(key)
        if asset is None:
            print(f"  [FAIL] {key:32s} not in manifest.json", file=sys.stderr)
            failures += 1
            continue
        try:
            check_path(os.path.join(asset["dest"], key + ".png"))
            raw = os.path.join(STAGING, f"{key}.raw.png")
            fetch(job_id, raw)
            out = os.path.join(REPO, asset["dest"], f"{key}.png")
            info = process(
                raw, out,
                size=asset.get("width", defaults["width"]),
                do_knockout=asset.get("knockout", defaults["knockout"]),
            )
            rel = os.path.relpath(out, REPO).replace("\\", "/")
            print(f"  [ok]   {key:32s} {info['size']} {info['transparent_pct']:>5}% clear  bg={info.get('background','-')}  -> {rel}")
        except Exception as exc:  # noqa: BLE001
            print(f"  [FAIL] {key:32s} {exc}", file=sys.stderr)
            failures += 1

    print(f"\n{len(pairs) - failures} ingested, {failures} failed.")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
