#!/usr/bin/env python3
"""action_regression -- do two traced films perform the same fight and the same authored actions? (ADR-011)

    python tools/asset-pipeline/action_regression.py <a.log> <b.log> [--also KIND ...]

Compares, on the fight's own playhead (wall time is ignored: a longer wave-clear break shifts every later frame without
changing a single beat):
  - the fight: every `event` line (kind, slot, amount, ms, crit), in order: the same dice must roll the same fight;
  - the ACTIONS: every clip-start, release, contact, contact-tick, handoff, yield, clip-end and root line, and every
    sound an authored action voices (sfx_seeker_spray_*, sfx_seeker_hard_hands_*), keyed by the MOMENT
    (playhead, kind, subject). A moment both films traced must say the same thing. A `root` line only one film has is
    reported and not counted: root is written by Draw when it changes, and a catch-up tick (a slow frame while a PNG
    is written) skips a Draw, so one film may simply not have sampled that frame.
Exits 1 on any difference.

Written for the base JAWS slice (2026-09-25): the accepted references (SPRAY, HARD HANDS) must not move because a
reaction is on screen. The pair is ONE SEEDED fight (RH_SHOT_SEED) filmed with the reaction layer on and off
(RH_REACTION_RECIPES=0). Two builds cannot be compared without the seed: the descent's dice are `new Random()`.
"""
from __future__ import annotations

import argparse
import sys

ACTION = {"clip-start", "release", "contact", "contact-tick", "root", "handoff", "yield", "clip-end"}
VOICES = ("sfx_seeker_spray", "sfx_seeker_hard_hands")
TAB = chr(9)


def lines(path: str, extra: set[str]) -> tuple[list[tuple], list[tuple]]:
    fight, action = [], []
    for raw in open(path, encoding="utf-8", errors="replace"):
        p = raw.rstrip("\n").split(TAB)
        if len(p) < 4 or p[0] != "present":
            continue
        kind = p[3]
        if kind == "event":
            fight.append((p[2], TAB.join(p[4:])))
        elif kind in ACTION or kind in extra or (kind == "sound" and p[4].startswith(VOICES)):
            action.append((p[2], kind, TAB.join(p[4:])))
    return fight, action


def compare_fight(a: list[tuple], b: list[tuple]) -> int:
    n = min(len(a), len(b))
    diff = [i for i in range(n) if a[i] != b[i]]
    print(f"fight    a={len(a):5}  b={len(b):5}  compared in order={n:5}  differing={len(diff)}")
    for i in diff[:4]:
        print(f"           a {a[i]}")
        print(f"           b {b[i]}")
    return len(diff)


def compare_actions(a: list[tuple], b: list[tuple]) -> int:
    def keyed(rows):
        d: dict[tuple, list[str]] = {}
        for ph, kind, rest in rows:
            subject = "" if kind == "root" else rest.split(TAB)[0]
            d.setdefault((ph, kind, subject), []).append(rest)
        return d

    ka, kb = keyed(a), keyed(b)
    common = [k for k in ka if k in kb]
    diff = [k for k in common if ka[k] != kb[k]]
    # a non-root moment one film traced and the other did not, inside the span both cover, is a real difference
    end = min(max((float(k[0]) for k in ka), default=0.0), max((float(k[0]) for k in kb), default=0.0))
    one = [k for k in set(ka) ^ set(kb) if float(k[0]) <= end]
    real = [k for k in one if k[1] != "root"]
    print(f"actions  a={len(a):5}  b={len(b):5}  moments both traced={len(common):5}  differing={len(diff)}"
          f"  root samples in one film only={len(one) - len(real)}  other moments in one film only={len(real)}")
    for k in diff[:4]:
        print(f"           {k}: a {ka[k]}  b {kb[k]}")
    for k in real[:4]:
        print(f"           only in {'a' if k in ka else 'b'}: {k}")
    return len(diff) + len(real)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("a")
    ap.add_argument("b")
    ap.add_argument("--also", nargs="*", default=[], help="more trace kinds to hold equal")
    args = ap.parse_args()
    fa, aa = lines(args.a, set(args.also))
    fb, ab = lines(args.b, set(args.also))
    bad = compare_fight(fa, fb) + compare_actions(aa, ab)
    print("IDENTICAL" if bad == 0 else f"{bad} DIFFERENCES")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
