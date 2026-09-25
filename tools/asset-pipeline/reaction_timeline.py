#!/usr/bin/env python3
"""reaction_timeline -- every enemy BITE in a traced film, and what the fight and the screen did about it (ADR-011).

    python tools/asset-pipeline/reaction_timeline.py <film.log> [--slot 3] [--detail]

A REACTION (JAWS; REPAY shares its trap Form but is an ACTIVE) takes no champion beat: it answers an enemy's bite. This lines up, per bite, on
the fight's playhead: the bite (EnemyStrike: its attacker slot and amount), the champion's damage, whether the
reaction answered (its Skill event) and its reflected Strike, every number, flash, effect and sound the screen
asked for within the phrase, what the champion's figure was doing at that instant (the committed clip and frame),
and whether a trap clip played. `--detail` prints every trace line in the phrase.

The trace clock restarts every wave; rows are read in log order, so a wave boundary never mixes two phrases.
"""
from __future__ import annotations

import argparse
import sys


def parse(path: str):
    out = []
    with open(path, encoding="utf-8", errors="replace") as fh:
        for n, line in enumerate(fh):
            p = line.rstrip("\n").split("\t")
            if len(p) >= 4 and p[0] == "present":
                out.append((n, float(p[1]), float(p[2]), p[3], p[4:]))
    return out


def kv(fields):
    return dict(x.split("=", 1) for x in fields if "=" in x)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("log")
    ap.add_argument("--slot", type=int, default=3, help="the reaction's skill slot")
    ap.add_argument("--detail", action="store_true")
    ap.add_argument("--window", type=float, default=600.0, help="ms after the bite the phrase is read for")
    a = ap.parse_args()
    rows = parse(a.log)
    clip, frame = "?", "?"
    figure_at = {}
    for i, (n, wall, ph, kind, f) in enumerate(rows):
        if kind == "champ-frame":
            clip, frame = f[0], f[1].split("=")[1]
        figure_at[i] = (clip, frame)
    last_trigger = None
    print(f"{'#':>3} {'bite ms':>8} {'att':>4} {'amt':>4} {'JAWS':>5} {'since last':>10} {'figure doing':>18}  answer")
    k = 0
    for i, (n, wall, ph, kind, f) in enumerate(rows):
        if kind != "event" or f[0] != "EnemyStrike":
            continue
        k += 1
        bite = float(kv(f)["at"])
        attacker = kv(f)["slot"]
        # the phrase: rows in log order from this bite until the next bite, within the window
        phrase = []
        for j in range(i, len(rows)):
            n2, w2, p2, k2, f2 = rows[j]
            if j > i and k2 == "event" and f2[0] == "EnemyStrike":
                break
            if k2 == "shot" or p2 > bite + a.window or (j > i and p2 < bite - 1):
                if p2 > bite + a.window:
                    break
                continue
            phrase.append((p2, k2, f2))
        trig = next((p for p, k2, f2 in phrase if k2 == "event" and f2[0] == "Skill" and kv(f2).get("slot") == str(a.slot)), None)
        since = "" if trig is None or last_trigger is None else f"{bite - last_trigger:.0f}"
        if trig is not None:
            last_trigger = bite
        doing = "/".join(figure_at[i])
        answer = []
        for p2, k2, f2 in phrase:
            d = f"{p2 - bite:+.0f}"
            if k2 == "event" and f2[0] in ("Strike", "EnemyDown", "Down", "Skill") and not (f2[0] == "Skill" and kv(f2).get("slot") != str(a.slot)):
                answer.append(f"{f2[0]}:{kv(f2).get('slot')}:{kv(f2).get('amount')}@{d}")
            elif k2 in ("number", "flash", "callout"):
                answer.append(f"{k2}:{kv(f2).get('slot', f2[0] if f2 else '')}{'=' + kv(f2)['amount'] if 'amount' in kv(f2) else ''}@{d}")
            elif k2 == "vfx-spawn":
                answer.append(f"fx:{f2[0]}/{kv(f2).get('profile')}/{kv(f2).get('subject')}@{d}")
            elif k2 == "sound":
                answer.append(f"snd:{f2[0]}({kv(f2).get('vol')},{kv(f2).get('pitch')})@{d}")
            elif k2 == "clip-start" and f2[0] == "trap":
                answer.append(f"TRAP-CLIP@{d}")
        print(f"{k:>3} {bite:8.0f} {attacker:>4} {kv(f)['amount']:>4} {'yes' if trig is not None else 'no':>5} {since:>10} {doing:>18}  {' '.join(answer)}")
        if a.detail:
            for p2, k2, f2 in phrase:
                print(f"        {p2 - bite:+6.0f}  {k2:12} {' '.join(f2)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
