#!/usr/bin/env python3
"""P1.7: the measured table of the Phase 1 film, one row per take, over each take's FILMED window (trace clock >= shot 0).

    python production/qa/evidence/vfx-sweep/phase1/measure_phase1.py production/vfx-sweep/review/phase1/p17_*.log

Columns: swings (swing-contact lines), contact == Strike ms (each contact's at= is a swing Strike's at=), step-in peak px
vs the row gap (swing-start peak / gap, and the largest traced swing-step x), flashes on swing ms (peak / ms), sound asks
per swing ms (max), generic sfx_hit 0.38 / fx puff on a swing ms, swing-draw sprites (max) and frames with alloc > 0.
"""
import sys


def kv(xs):
    return dict(x.split("=", 1) for x in xs if "=" in x)


def measure(path):
    rows = []
    for raw in open(path, encoding="utf-8", errors="replace"):
        f = raw.rstrip("\n").split("\t")
        if len(f) >= 4 and f[0] == "present":
            try:
                rows.append((float(f[1]), float(f[2]), f[3], f[4:]))
            except ValueError:
                pass
    shot0 = min(c for c, ph, k, r in rows if k == "shot" and r and r[0] == "0")
    win = [r for r in rows if r[0] >= shot0]
    strikes = {int(kv(r)["at"]) for c, ph, k, r in win if k == "event" and r[0] == "Strike" and kv(r).get("skill") == "False"}
    contacts = [kv(r) for c, ph, k, r in win if k == "swing-contact"]
    cms = [int(x["at"]) for x in contacts]
    on = sum(1 for m in cms if m in strikes)
    starts = [kv(r) for c, ph, k, r in win if k == "swing-start"]
    peaks = [(int(float(s["peak"])), int(float(s["gap"]))) for s in starts]
    steps = [int(kv(r)["x"]) for c, ph, k, r in win if k == "swing-step"]
    contact_ph = {ph for c, ph, k, r in win if k == "swing-contact"}
    flashes = [kv(r) for c, ph, k, r in win if k == "flash" and ph in contact_ph]
    flash_set = sorted({f"{x['peak']}/{x['ms']}" for x in flashes})
    per_ms = {}
    for c, ph, k, r in win:
        if k == "sound" and ph in contact_ph:
            per_ms.setdefault(ph, []).append(f"{r[0]} {r[1]}")
    cues = sorted({s for v in per_ms.values() for s in v})
    generic = sum(1 for c, ph, k, r in win if ph in contact_ph and k == "sound" and r[0] == "sfx_hit" and "vol=0.38" in r)
    puff = sum(1 for c, ph, k, r in win if ph in contact_ph and k == "vfx-spawn" and any("weak" in x.lower() for x in r))
    rel = [kv(r) for c, ph, k, r in win if k == "swing-release"]
    rel_cues = sorted({f"{x.get('cue')} vol={x.get('vol')}" for x in rel})
    draws = [kv(r) for c, ph, k, r in win if k == "swing-draw"]
    sprites = max((int(d["sprites"]) for d in draws), default=0)
    alloc = sum(1 for d in draws if int(d["alloc"]) != 0)
    taut = [kv(r).get("taut") for c, ph, k, r in win if k == "swing-strand"]
    name = path.replace("\\", "/").rsplit("/", 1)[-1][:-4]
    return dict(name=name, window=f"{win[0][1]:.0f}-{max(ph for c, ph, k, r in win):.0f}", swings=len(cms), on=on,
                dup=len(cms) - len(set(cms)), peaks=peaks, step=max(steps, default=0), flashes=len(flashes),
                flash_set=flash_set, max_per_ms=max((len(v) for v in per_ms.values()), default=0), cues=cues,
                generic=generic, puff=puff, rel=len(rel), rel_cues=rel_cues, sprites=sprites, draws=len(draws),
                alloc=alloc, taut=taut)


def main():
    print("| take | window ms | swings | contact == Strike ms | step-in peak px / row gap px (traced max step) | flashes on swing ms | sound asks per swing ms (max) / cues | generic 0.38 thud / puff | releases | swing-draw sprites max / frames / alloc>0 |")
    print("|---|---|---|---|---|---|---|---|---|---|")
    for p in sys.argv[1:]:
        m = measure(p)
        pk = ", ".join(f"{a}/{b}" for a, b in m["peaks"]) or "-"
        rel = f"{m['rel']} ({'; '.join(m['rel_cues'])})" if m["rel"] else "0"
        if m["taut"]:
            rel += f"; strand taut={','.join(t for t in m['taut'] if t)}"
        print(f"| {m['name']} | {m['window']} | {m['swings']} | {m['on']} of {m['swings']} (dup {m['dup']}) | {pk} ({m['step']}) "
              f"| {m['flashes']} ({', '.join(m['flash_set'])}) | {m['max_per_ms']} / {'; '.join(m['cues'])} | {m['generic']} / {m['puff']} "
              f"| {rel} | {m['sprites']} / {m['draws']} / {m['alloc']} |")


if __name__ == "__main__":
    main()
