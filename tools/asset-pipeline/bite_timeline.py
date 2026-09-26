#!/usr/bin/env python3
"""bite_timeline.py -- ONE BITE'S SENTENCE on the fight's playhead, from a traced film (RH_PRESENT_TRACE=1).

    python tools/asset-pipeline/bite_timeline.py <film.log> [--bite <ms>]

For the bite at <ms> (the first EnemyStrike in the film's shot window when not given) it reports, in ms relative to
the contact: the wind-up's start and its commit (enemy-windup / enemy-commit), the leader creature's root offset
frame by frame (enemy-root / enemy-home), the contact and its effect (EnemyStrike, vfx-spawn impact.bite), the
champion's recoil frame by frame (recoil / recoil-home), the follow-through's end (enemy-follow-end), and the flash
that landed on the struck creature nearest the contact (flash: its peak, life and rise). No clock of its own: every
time is the replay/presentation playhead the trace carries.
"""
import argparse


def parse(path):
    rows = []
    for line in open(path, encoding="utf-8", errors="replace"):
        p = line.rstrip("\n").split("\t")
        if len(p) >= 4 and p[0] == "present":
            rows.append((float(p[1]), float(p[2]), p[3], p[4:]))
    return rows


def kv(fields):
    return dict(f.split("=", 1) for f in fields if "=" in f)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("log")
    ap.add_argument("--bite", type=float, default=None)
    a = ap.parse_args()
    rows = parse(a.log)
    shots = [ph for _, ph, k, _ in rows if k == "shot"]
    lo, hi = (min(shots), max(shots)) if shots else (-1e9, 1e9)
    bites = [ph for _, ph, k, f in rows if k == "event" and f and f[0] == "EnemyStrike" and lo <= ph <= hi]
    if not bites:
        print("no bite in the film's window")
        return 1
    at = a.bite if a.bite is not None else bites[0]
    # the contact's playhead is the frame the event was pumped on; the event's own `at=` is Core's ms
    contact = min(bites, key=lambda ph: abs(ph - at))
    print(f"# the bite at {contact:.0f} (Core at={kv([f for _, ph, k, f in rows if k == 'event' and ph == contact and f[0] == 'EnemyStrike'][0][1:]).get('at')})")
    print("| t (ms) | what |")
    print("|---|---|")
    window = [(ph, k, f) for _, ph, k, f in rows if contact - 1000 <= ph <= contact + 400 and k in
              ("enemy-windup", "enemy-commit", "enemy-root", "enemy-home", "recoil", "recoil-home", "enemy-follow-end",
               "vfx-spawn", "flash", "event")]
    for ph, k, f in window:
        d = kv(f)
        if k == "event":
            if f[0] not in ("EnemyStrike", "Strike"): continue
            what = f"**CONTACT: {f[0]}** amount={d.get('amount')} crit={d.get('crit')}" if f[0] == "EnemyStrike" else f"champion's Strike on slot {d.get('slot')} amount={d.get('amount')} crit={d.get('crit')}"
        elif k == "vfx-spawn":
            if "impact" not in (f[1] if len(f) > 1 else ""): continue
            what = f"effect {f[1]} on {d.get('subject')}"
        elif k == "enemy-windup": what = f"enemy ANTICIPATION starts (bite={d.get('bite')})"
        elif k == "enemy-commit": what = "enemy COMMIT starts"
        elif k == "enemy-root": what = f"leader root x={d.get('x')} (slot {d.get('slot')})"
        elif k == "enemy-home": what = "enemy HOME"
        elif k == "recoil": what = f"champion recoil x={d.get('x')}"
        elif k == "recoil-home": what = "champion recoil HOME"
        elif k == "enemy-follow-end": what = "enemy follow-through ends"
        elif k == "flash": what = f"flash on slot {d.get('slot')}: peak {d.get('peak')}, {d.get('ms')} ms, rise {d.get('rise')}"
        else: what = k
        print(f"| {ph - contact:+.0f} | {what} |")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
