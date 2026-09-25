#!/usr/bin/env python3
"""action_timeline -- the millisecond timeline of every AUTHORED action in a traced film (ADR-011).

    python tools/asset-pipeline/action_timeline.py <film.log> [--cast N] [--markdown]

A film run with RH_PRESENT_TRACE=1 logs, on one clock, every frame the champion shows, the release, every
fight event crossed, every number, flash and sound asked for. This lines them up per cast, relative to the
fight's BEAT (the contact), so "is the contact on the beat, is the release one flight before it, does the
number appear with the knife" is a table, not an impression.

Rows: the HANDOFF that gave the action the figure (the previous action's contact, recovery start, exit pose,
and the recovery's compression), any plain beat that yielded its animation to this action, then clip start,
anticipation, extreme hold, release pose, release sound, projectile creation, first visible blade, contact,
health (the hits crossed), flash, number, impact (a generic puff, if any leaked), contact audio, ticks,
follow-through end, recovery end, idle restart. Plus the other sounds that played inside the phrase and at
what duck.
"""
from __future__ import annotations

import argparse
import sys


def parse(path: str):
    rows = []
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            p = line.rstrip("\n").split("\t")
            if len(p) >= 4 and p[0] == "present":
                rows.append((float(p[1]), float(p[2]), p[3], p[4:]))
    return rows


def casts(rows):
    starts = [i for i, r in enumerate(rows) if r[2] == "clip-start" and len(r[3]) > 1 and r[3][1] == "authored"]
    for k, i in enumerate(starts):
        end = starts[k + 1] if k + 1 < len(starts) else len(rows)
        yield rows[i], rows[i:end], rows[max(0, i - 400):i]


def kv(fields):
    return dict(x.split("=", 1) for x in fields if "=" in x)


def timeline(start, rows, before=()):
    info = kv(start[3])
    beat = float(info["beat"])
    clip = start[3][0]
    shot = None
    frame_of = {}
    out = []

    def add(label, ph, detail=""):
        out.append((label, ph, ph - beat, detail))

    # THE HANDOFF that gave this action the figure (ADR-011): the outgoing clip's contact, its recovery, what it
    # was allowed, and where it exited; and any plain beat that yielded its animation to this action's reservation
    handoff = next((r for r in reversed(before) if r[2] == "handoff" and abs(float(kv(r[3]).get("exit", -9e9)) - start[1]) < 20), None)
    if handoff:
        h = kv(handoff[3])
        prev = handoff[3][0]
        add(f"previous action ({prev}) contact", float(h["contact"]), f"its beat {h['beat']}")
        add(f"previous recovery start", float(h["recovery"]), f"protected frames end here ({h['protected']} ms into its clip)")
        add(f"previous exit pose reached", float(h["exit"]),
            f"recovery {h['motion']} ms nominal (+{h['hold']} ms settle) in {h['available']} ms: x{h['ratio']} ({h['fit']})")
    for r in before:
        if r[2] == "yield" and abs(float(kv(r[3]).get("forBeat", -1)) - beat) < 1:
            y = kv(r[3])
            add(f"{r[3][0]} beat not animated (yield)", float(y["beat"]), f"its exit {y['exit']} > this action's latest start {y['latest']}")

    first = {}
    for clock, ph, kind, f in rows:
        if kind == "shot":
            shot = int(f[0])
            continue
        if kind == "champ-frame" and f[0] == clip:
            n = int(kv(f)["frame"])
            if n not in frame_of:
                frame_of[n] = ph
        elif kind == "champ-frame" and f[0] == "idle" and "idle" not in first:
            first["idle"] = ph
        elif kind in ("release", "contact") and kind not in first:
            first[kind] = (ph, f)
        elif kind == "perf-draw" and "visible" not in first and kv(f).get("released") == "True" and int(kv(f).get("sprites", 0)) > 0:
            first["visible"] = (ph, kv(f).get("sprites"))
        elif kind == "sound":
            first.setdefault("sounds", []).append((ph, f))
        elif kind in ("number", "flash", "contact-tick", "vfx-spawn", "clip-end"):
            first.setdefault(kind, []).append((ph, f))
        elif kind == "event" and f[0] == "Strike" and abs(float(kv(f).get("at", -1)) - beat) < 1:
            first.setdefault("strike", []).append((ph, f))

    add("clip start (ready, f0)", frame_of.get(0, start[1]))
    if 1 in frame_of: add("anticipation (f1)", frame_of[1])
    if 2 in frame_of: add("extreme hold (f2)", frame_of[2])
    if 3 in frame_of: add("release pose (f3)", frame_of[3])
    rel = first.get("release")
    snd = first.get("sounds", [])
    rs = next(((ph, f) for ph, f in snd if "release" in f[0]), None)
    if rs: add("release sound", rs[0], f"{rs[1][0]} {' '.join(rs[1][1:])}")
    if rel: add("projectile creation", rel[0], " ".join(rel[1][1:4]))
    if "visible" in first: add("first visible blade", first["visible"][0], f"{first['visible'][1]} sprites")
    con = first.get("contact")
    if con: add("CONTACT (blades land)", con[0], " ".join(con[1][1:4]))
    # the fight's feedback FOR THIS CAST: what was presented within two frames of the contact (later swings
    # and bites flash and print too, and are not this action's)
    at = con[0] if con else beat
    for label, key in (("health (hits crossed)", "strike"), ("flash", "flash"), ("number", "number")):
        hits = [h for h in first.get(key, []) if abs(h[0] - at) <= 34]
        if hits:
            add(label, hits[0][0], f"{len(hits)} x, last at {hits[-1][0] - beat:+.0f}")
    puffs = [(ph, f) for ph, f in first.get("vfx-spawn", []) if abs(ph - beat) < 40 and "impact" in " ".join(f)]
    add("generic impact puff", puffs[0][0] if puffs else float("nan"), "none (replaced by the blades' own contact)" if not puffs else " ".join(puffs[0][1]))
    cs = next(((ph, f) for ph, f in snd if f[0].endswith("_hit") and "spray" in f[0]), None)
    if cs: add("contact audio", cs[0], f"{cs[1][0]} {' '.join(cs[1][1:])}")
    for ph, f in first.get("contact-tick", []):
        add("contact tick", ph, " ".join(f))
    if 5 in frame_of: add("follow-through end (f4 -> f5)", frame_of[5])
    if 6 in frame_of: add("recovery (f6)", frame_of[6])
    if 7 in frame_of: add("recovery end / settle (f7)", frame_of[7])
    ends = first.get("clip-end", [])
    if ends: add("clip end", ends[0][0])
    if "idle" in first: add("idle restart", first["idle"])
    others = [(ph, f) for ph, f in snd if "spray" not in f[0] and (rel is None or ph >= rel[0]) and ph <= beat + 200]
    out.sort(key=lambda r: (r[1] != r[1], r[1]))   # in time order (stable: one frame's rows keep theirs); NaN last
    return beat, out, others


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("log")
    ap.add_argument("--cast", type=int, default=None, help="only the Nth authored cast (0-based)")
    ap.add_argument("--markdown", action="store_true")
    a = ap.parse_args()
    rows = parse(a.log)
    for n, (start, span, before) in enumerate(casts(rows)):
        if a.cast is not None and n != a.cast:
            continue
        beat, out, others = timeline(start, span, before)
        targets = kv(start[3]).get("targets", "?")
        print(f"\n## cast {n}: beat {beat:.0f} ms, targets {targets}\n")
        if a.markdown:
            print("| moment | playhead ms | vs beat | detail |\n|---|---:|---:|---|")
        for label, ph, d, detail in out:
            if a.markdown:
                print(f"| {label} | {ph:.0f} | {d:+.0f} | {detail} |")
            else:
                print(f"{label:32} {ph:8.0f} {d:+7.0f}  {detail}")
        if others:
            print("\nother sounds inside the phrase (release .. beat+200):")
            for ph, f in others:
                print(f"  {ph:8.0f} {ph - beat:+6.0f}  {' '.join(f)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
