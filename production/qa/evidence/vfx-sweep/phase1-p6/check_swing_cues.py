#!/usr/bin/env python3
"""check_swing_cues.py <take.log> ...: every swing-contact / swing-release line of a trace names the champion's OWN
identity cue (sfx_<champ>_swing_hit 0.36; sfx_quiver_loose 0.18, sfx_chorus_toss / sfx_unbroken_toss 0.16), and exactly
one `sound <that cue>` line sits on the same ms. Prints a table; exits 1 on any miss."""
import collections
import os
import sys

RELEASE = {"quiver": ("sfx_quiver_loose", "0.18"), "chorus": ("sfx_chorus_toss", "0.16"), "unbroken": ("sfx_unbroken_toss", "0.16")}


def check(path: str) -> bool:
    sounds = collections.Counter()
    swings = []
    for line in open(path, encoding="utf-8", errors="replace"):
        f = line.rstrip("\n").split("\t")
        if len(f) < 5 or f[0] != "present":
            continue
        ms = f[2]
        if f[3] == "sound":
            sounds[(ms, f[4], f[5])] += 1
        elif f[3] in ("swing-contact", "swing-release"):
            kv = dict(x.split("=", 1) for x in f[5:] if "=" in x)
            swings.append((ms, f[3], f[4].split(".")[0], kv.get("cue"), "vol=" + kv.get("vol", "?")))
    ok = bool(swings)
    print(f"== {os.path.basename(path)}: {len(swings)} swing lines")
    for ms, kind, champ, cue, vol in swings:
        want = (f"sfx_{champ}_swing_hit", "vol=0.36") if kind == "swing-contact" else (RELEASE[champ][0], "vol=" + RELEASE[champ][1])
        n = sounds[(ms, cue, vol)]
        good = (cue, vol) == want and n == 1
        ok &= good
        print(f"   {ms:>6} {kind:13} cue={cue} {vol}  sound lines on that ms: {n}  {'ok' if good else 'MISS (want ' + want[0] + ' ' + want[1] + ')'}")
    return ok


if __name__ == "__main__":
    results = [check(p) for p in sys.argv[1:]]
    print("ALL OK" if all(results) else "FAILED")
    sys.exit(0 if all(results) else 1)
