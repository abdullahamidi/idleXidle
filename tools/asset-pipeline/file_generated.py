#!/usr/bin/env python3
"""file_generated — fetch a finished PixelLab MCP image and file it through the still pipeline.

    python tools/asset-pipeline/file_generated.py <job id or URL>=<repo-relative out.png>[:<size>[:<flags>]] ...

    python tools/asset-pipeline/file_generated.py \\
        7c0651a5-e80e-49ec-9053-e07779166b8f=assets/art/Characters/Hunter/icons/status/icon_status_defense.png:256

Each job is downloaded to tools/asset-pipeline/.staging/mcp/<job>.png (gitignored) and pushed through
generate.process — knockout (a border flood-fill for any background the generator left), keep the largest
component (single subject), trim and centre onto a <size> square, integer upscale if the source is smaller —
so a hand-filed asset goes through exactly the gate every manifest asset went through. Flags after a second
colon: `loose` (keep faint alpha), `multi` (do not keep only the largest component — a motif made of pieces).

WHY THIS EXISTS. ingest.py files by manifest DEST, and the manifest's dests disagree with where 570 shipped
files actually live (source_* is filed under UI/icons/sources by the manifest and lives under
ItemsLoot/glyphs/source), so ingesting a replacement creates a second file with the same basename and
AssetLibrary picks whichever is larger. This takes an explicit path instead and writes exactly one file.
"""

from __future__ import annotations

import os
import sys
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
STAGING = os.path.join(HERE, ".staging", "mcp")
sys.path.insert(0, HERE)
import generate  # noqa: E402

MCP = "https://api.pixellab.ai/mcp/images/{job}/download"


def fetch(job_or_url: str, out: str) -> str:
    url = job_or_url if job_or_url.startswith("http") else MCP.format(job=job_or_url)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    req = urllib.request.Request(url, headers={"User-Agent": "idlexidle-asset-pipeline"})
    with urllib.request.urlopen(req, timeout=120) as r, open(out, "wb") as fh:
        fh.write(r.read())
    return out


def main(argv: list[str]) -> int:
    if not argv:
        print(__doc__)
        return 2
    for spec in argv:
        job, _, rest = spec.partition("=")
        parts = rest.split(":")
        rel, size = parts[0], int(parts[1]) if len(parts) > 1 and parts[1] else 256
        flags = set(parts[2].split(",")) if len(parts) > 2 else set()
        key = os.path.splitext(os.path.basename(rel))[0]
        raw = fetch(job, os.path.join(STAGING, key + ".png"))
        out = os.path.join(REPO, rel)
        os.makedirs(os.path.dirname(out), exist_ok=True)
        info = generate.process(raw, out, size, do_knockout=True,
                                loose_alpha="loose" in flags, single_subject="multi" not in flags)
        print(f"{rel}  {info}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
