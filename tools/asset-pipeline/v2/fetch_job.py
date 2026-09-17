"""Download PixelLab MCP job results by job id (no auth: the download URLs are public).

    python tools/asset-pipeline/v2/fetch_job.py <out_dir> <name>=<jobId>[:<index>] ...

Writes <out_dir>/<name>.png (single image, index 0) or <out_dir>/<name>_<i>.png for an
explicit index. An animate_image job's frames are index 0 (the input) .. N; pass `:all`
to fetch 0..16 until a 404. Development-only; nothing at runtime reads PixelLab.
"""
from __future__ import annotations

import os
import sys
import urllib.error
import urllib.request

IMG_BASE = "https://api.pixellab.ai/mcp/images"


def fetch(url: str, out: str) -> bool:
    try:
        with urllib.request.urlopen(url, timeout=60) as r:
            data = r.read()
    except urllib.error.HTTPError as e:
        if e.code == 404:
            return False
        if e.code == 423:   # Locked: the job is still processing — report PENDING, never crash a batch
            print(f"  pending: {url}")
            return False
        raise
    os.makedirs(os.path.dirname(out) or ".", exist_ok=True)
    open(out, "wb").write(data)
    return True


def main(argv: list[str]) -> int:
    if len(argv) < 3:
        print(__doc__)
        return 2
    out_dir = argv[1]
    for spec in argv[2:]:
        name, _, rest = spec.partition("=")
        job, _, idx = rest.partition(":")
        if idx == "all":
            n = 0
            for i in range(0, 17):
                if not fetch(f"{IMG_BASE}/{job}/download?index={i}", os.path.join(out_dir, f"{name}_{i:02d}.png")):
                    break
                n += 1
            print(f"{name}: {n} frames")
        elif idx:
            ok = fetch(f"{IMG_BASE}/{job}/download?index={int(idx)}", os.path.join(out_dir, f"{name}_{int(idx):02d}.png"))
            print(f"{name}[{idx}]: {'ok' if ok else 'MISSING'}")
        else:
            ok = fetch(f"{IMG_BASE}/{job}/download", os.path.join(out_dir, f"{name}.png"))
            print(f"{name}: {'ok' if ok else 'MISSING'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
