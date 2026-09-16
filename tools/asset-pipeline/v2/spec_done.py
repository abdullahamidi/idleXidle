"""Record PixelLab ids for a matrix cell in spec.json's enemy_matrix.done, text-splicing so the rest of
the file stays byte-identical.

    python tools/asset-pipeline/v2/spec_done.py <key> <field>=<id> [<field>=<id> ...]

Fields: character_id, idle, attack, death (character route) or image_job, idle_job, attack_job,
death_job (image route). Re-running with a field replaces it — a re-rolled clip simply overwrites.
"""
from __future__ import annotations

import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SPEC = os.path.join(HERE, "spec.json")


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print(__doc__)
        return 2
    key, pairs = argv[0], dict(a.split("=", 1) for a in argv[1:])
    text = open(SPEC, encoding="utf-8").read()
    spec = json.loads(text)
    done = spec["enemy_matrix"]["done"]
    entry = dict(done.get(key, {}))
    entry.update(pairs)
    # rebuild the whole `done` object as text with the matrix's own indentation
    start = text.index('"enemy_matrix"')
    m = re.compile(r'"done": (\{.*?\n    \})', re.S).search(text, start)
    if m is None:
        m = re.compile(r'"done": \{\}').search(text, start)
    done[key] = entry
    block = json.dumps(done, indent=6).replace("\n", "\n    ")
    new = text[:m.start()] + '"done": ' + block + text[m.end():]
    json.loads(new)
    open(SPEC, "w", encoding="utf-8", newline="\n").write(new)
    print(f"{key}: {entry}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
